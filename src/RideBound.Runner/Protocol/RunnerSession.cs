using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;
using RideBound.Algorithms.Candidates;
using RideBound.Algorithms.Commitments;
using RideBound.Algorithms.Policies;
using RideBound.Application.Commitments;
using RideBound.Application.Optimization;
using RideBound.Application.State;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;
using RideBound.Domain.Common;
using RideBound.Domain.Runs;
using RideBound.Domain.Validation;
using RideBound.Runner.Configuration;
using RideBound.Runner.Online;
using RideBound.Solvers.OrTools;

namespace RideBound.Runner.Protocol;

public enum RunnerSessionStatus
{
    New,
    Negotiated,
    Initialized,
    AwaitingDecisionApplied,
    Failed,
    Shutdown,
}

public enum RunnerExecutionMode
{
    StructuralConformance,
    OnlineRollingCost,
    OnlineCommitment,
}

public sealed record RunnerSessionResult(
    ProtocolEnvelope? Response,
    bool ShouldTerminate = false);

public sealed class RunnerSession
{
    public const int RetainedBatchResponseCount = 1;

    private readonly CapabilityRequirementProfile _requirements;
    private readonly RunnerExecutionMode _executionMode;
    private readonly OnlineEventMapper _onlineEventMapper;
    private readonly RollingCostPolicy _rollingCostPolicy;
    private readonly CandidateGenerationOptions _candidateOptions;
    private readonly ICommitmentPolicyProvider? _commitmentPolicies;
    private readonly IStopDistanceLookup? _stopDistances;
    private readonly Sha256Hex? _commitmentPolicyConfigurationHash;
    private readonly CommitmentDecisionValidator _commitmentValidator;
    private readonly Wp4RunnerConfiguration? _wp4Configuration;
    private readonly SolverBackedRidePoolingPolicy? _solverBackedPolicy;
    private readonly bool _useManifestSolverSeed;
    private readonly MultiplePlanConsensusPolicy _multiplePlanPolicy;
    private HelloPayload? _hello;
    private HelloAckPayload? _helloAcknowledgement;
    private InitializedSessionIdentity? _identity;
    private PendingDecision? _pending;
    private CachedBatch? _lastBatch;
    private long _appliedEpoch;
    private long _nextEventSequence = 1;
    private long _simulationTimeMilliseconds;
    private Sha256Hex _manifestHash = ProtocolHash.ZeroHash;
    private Sha256Hex _stateHash = ProtocolHash.ZeroHash;
    private Sha256Hex _previousDecisionHash = ProtocolHash.ZeroHash;
    private EventReductionCoordinator? _onlineCoordinator;

    public RunnerSession(
        CapabilityRequirementProfile requirements,
        RunnerExecutionMode executionMode =
            RunnerExecutionMode.StructuralConformance,
        OnlineEventMapper? onlineEventMapper = null,
        RollingCostPolicy? rollingCostPolicy = null,
        CandidateGenerationOptions? candidateOptions = null,
        ICommitmentPolicyProvider? commitmentPolicies = null,
        IStopDistanceLookup? stopDistances = null,
        Sha256Hex? commitmentPolicyConfigurationHash = null,
        CommitmentDecisionValidator? commitmentValidator = null,
        Wp4RunnerConfiguration? wp4Configuration = null,
        SolverBackedRidePoolingPolicy? solverBackedPolicy = null,
        MultiplePlanConsensusPolicy? multiplePlanPolicy = null,
        bool useManifestSolverSeed = false)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        _requirements = requirements;
        _executionMode = executionMode;
        _onlineEventMapper = onlineEventMapper ?? new OnlineEventMapper();
        _rollingCostPolicy = rollingCostPolicy ?? new RollingCostPolicy();
        _candidateOptions = wp4Configuration?.CandidateGeneration
            ?? candidateOptions
            ?? new CandidateGenerationOptions(
                maximumCandidatesPerVehicle: 10_000,
                maximumNewRequestsPerVehicle: 4,
                exactSmallMode: false);
        _commitmentPolicies = commitmentPolicies;
        _stopDistances = stopDistances;
        _commitmentPolicyConfigurationHash = commitmentPolicyConfigurationHash;
        _commitmentValidator = commitmentValidator
            ?? new CommitmentDecisionValidator();
        _wp4Configuration = wp4Configuration;
        _useManifestSolverSeed = useManifestSolverSeed;
        _solverBackedPolicy = wp4Configuration is not null
                && wp4Configuration.SolverPolicyOptions is not null
            ? solverBackedPolicy
                ?? new SolverBackedRidePoolingPolicy(
                    new OrToolsCandidateSelectionSolver(),
                    commitmentValidator: _commitmentValidator)
            : null;
        _multiplePlanPolicy = multiplePlanPolicy
            ?? new MultiplePlanConsensusPolicy();

        if (executionMode == RunnerExecutionMode.OnlineCommitment
            && (commitmentPolicies is null
                || stopDistances is null
                || commitmentPolicyConfigurationHash is null))
        {
            throw new ArgumentException(
                "Commitment mode requires an explicit policy catalog, " +
                "stop-distance lookup, and canonical configuration hash.");
        }

        if (wp4Configuration is not null
            && executionMode != RunnerExecutionMode.OnlineCommitment)
        {
            throw new ArgumentException(
                "A WP4 policy configuration requires commitment execution mode.",
                nameof(wp4Configuration));
        }

        if (useManifestSolverSeed
            && wp4Configuration?.SolverPolicyOptions is null)
        {
            throw new ArgumentException(
                "Manifest solver seeding requires a solver-backed WP4 configuration.",
                nameof(useManifestSolverSeed));
        }
    }

    public RunnerSessionStatus Status { get; private set; } = RunnerSessionStatus.New;

    public long AppliedEpoch => _appliedEpoch;

    public long NextEventSequence => _nextEventSequence;

    public Sha256Hex PreviousDecisionHash => _previousDecisionHash;

    public OnlineState? CommittedOnlineState => _onlineCoordinator?.CommittedState;

    public RunnerSessionResult Process(ReadOnlySpan<byte> utf8Json)
    {
        if (Status == RunnerSessionStatus.Shutdown)
        {
            return new RunnerSessionResult(null, ShouldTerminate: true);
        }

        var envelopeResult = ProtocolEnvelopeCodec.Decode(utf8Json);

        if (!envelopeResult.IsSuccess)
        {
            var envelopeError = envelopeResult.Error!;
            var protocolCode = MapEnvelopeError(envelopeError.Code);
            return Error(
                protocolCode,
                envelopeError.Disposition,
                envelopeError.Message);
        }

        var envelope = envelopeResult.Envelope!;

        if (envelope.MessageType.Value == "shutdown")
        {
            if (envelope.Payload.EnumerateObject().Any())
            {
                return Error(
                    "UNKNOWN_FIELD",
                    ProtocolFailureDisposition.RejectMessage,
                    "shutdown payload must be empty.",
                    envelope);
            }

            Status = RunnerSessionStatus.Shutdown;
            return new RunnerSessionResult(null, ShouldTerminate: true);
        }

        if (Status == RunnerSessionStatus.Failed)
        {
            return Error(
                "INVALID_SESSION_STATE",
                ProtocolFailureDisposition.RejectMessage,
                "A failed session accepts only shutdown.",
                envelope);
        }

        return envelope.MessageType.Value switch
        {
            "hello" => ProcessHello(envelope),
            "initializeRun" => ProcessInitialize(envelope),
            "eventBatch" => ProcessEventBatch(envelope),
            "decisionApplied" => ProcessDecisionApplied(envelope),
            "checkpoint" => ProcessCheckpoint(envelope),
            "restore" => ProcessRestore(envelope),
            _ => Error(
                "INVALID_SESSION_STATE",
                ProtocolFailureDisposition.RejectMessage,
                $"Message '{envelope.MessageType.Value}' is not valid in state '{Status}'.",
                envelope),
        };
    }

    private RunnerSessionResult ProcessHello(ProtocolEnvelope envelope)
    {
        if (Status != RunnerSessionStatus.New)
        {
            return Error(
                "INVALID_SESSION_STATE",
                ProtocolFailureDisposition.RejectMessage,
                "hello is valid only for a new session.",
                envelope);
        }

        var payloadResult = HelloPayloadCodec.Decode(envelope.Payload);

        if (!payloadResult.IsSuccess)
        {
            return PayloadError(payloadResult.Error!, envelope);
        }

        var negotiation = CapabilityNegotiator.Negotiate(
            payloadResult.Value!,
            _requirements);

        if (!negotiation.IsSuccess)
        {
            var error = negotiation.Error!;
            ProtocolErrorCodes.TryGetDisposition(
                error.ProtocolCode,
                out var disposition);
            return Error(
                error.ProtocolCode,
                disposition,
                error.Message,
                envelope);
        }

        _hello = payloadResult.Value;
        _helloAcknowledgement = negotiation.Acknowledgement;
        Status = RunnerSessionStatus.Negotiated;

        return new RunnerSessionResult(
            CreateEnvelope(
                "helloAck",
                HelloAckPayloadCodec.Encode(_helloAcknowledgement!)));
    }

    private RunnerSessionResult ProcessInitialize(ProtocolEnvelope envelope)
    {
        if (Status != RunnerSessionStatus.Negotiated
            || _hello is null
            || _helloAcknowledgement is null)
        {
            return Error(
                "INVALID_SESSION_STATE",
                ProtocolFailureDisposition.RejectMessage,
                "initializeRun requires a successful hello negotiation.",
                envelope);
        }

        var payloadResult = InitializeRunPayloadCodec.Decode(envelope.Payload);

        if (!payloadResult.IsSuccess)
        {
            return PayloadError(payloadResult.Error!, envelope);
        }

        var validation = InitializeRunValidator.Validate(
            envelope,
            payloadResult.Value!,
            new InitializeRunValidationContext(
                _hello,
                _helloAcknowledgement,
                envelope.RunId,
                envelope.ScenarioId,
                _identity));

        if (!validation.IsSuccess)
        {
            return Error(
                validation.Error!.ProtocolCode,
                ProtocolFailureDisposition.RejectMessage,
                validation.Error.Message,
                envelope);
        }

        if (_executionMode == RunnerExecutionMode.OnlineCommitment
            && payloadResult.Value!.Manifest.PolicyConfigurationHash
                != _commitmentPolicyConfigurationHash)
        {
            return Error(
                "HASH_MISMATCH",
                ProtocolFailureDisposition.FailSession,
                "Manifest policyConfigurationHash does not match the " +
                "loaded commitment policy configuration.",
                envelope);
        }

        if (_wp4Configuration is not null
            && (!StringComparer.Ordinal.Equals(
                    payloadResult.Value!.Manifest.PolicyId,
                    _wp4Configuration.PolicyId)
                || !StringComparer.Ordinal.Equals(
                    payloadResult.Value.Manifest.PolicyVersion,
                    _wp4Configuration.PolicyVersion)))
        {
            return Error(
                "HASH_MISMATCH",
                ProtocolFailureDisposition.FailSession,
                "Manifest policyId/policyVersion does not match the loaded WP4 policy configuration.",
                envelope);
        }
        if (_useManifestSolverSeed
            && payloadResult.Value!.Manifest.MasterSeed > int.MaxValue)
        {
            return Error(
                "SCHEMA_VALIDATION_FAILED",
                ProtocolFailureDisposition.FailSession,
                "Manifest solver seed exceeds the deterministic Int32 range.",
                envelope);
        }

        EpochId.TryCreate(0, out var epochId);
        EventSequence.TryCreate(1, out var nextEventSequence);
        SimulationTimeMilliseconds.TryCreate(0, out var simTime);
        _manifestHash = ProtocolHash.CalculateManifestHash(
            payloadResult.Value!.Manifest);
        _stateHash = ProtocolHash.CalculateStateIdentityHash(
            epochId,
            nextEventSequence,
            simTime);
        _identity = validation.Identity;

        if (IsOnlineMode)
        {
            var run = RideBoundRun.Create(
                new RunIdentifier(_identity!.RunId.Value),
                new ScenarioIdentifier(_identity.ScenarioId.Value),
                new SimTime(0));
            _onlineCoordinator = new EventReductionCoordinator(
                OnlineState.Create(
                    run,
                    _identity.Manifest.TravelTimeSnapshotHash.Value));
            _stateHash = OnlineStateCanonicalizer.CalculateHash(
                _onlineCoordinator.CommittedState);
        }

        Status = RunnerSessionStatus.Initialized;

        var initialized = new InitializedPayload(
            _manifestHash,
            new InitialStateIdentity(
                epochId,
                nextEventSequence,
                simTime,
                _stateHash));

        return new RunnerSessionResult(
            CreateEnvelope(
                "initialized",
                InitializedPayloadCodec.Encode(initialized),
                envelope.RunId,
                envelope.ScenarioId));
    }

    private RunnerSessionResult ProcessEventBatch(ProtocolEnvelope envelope)
    {
        if (Status is not (
            RunnerSessionStatus.Initialized
            or RunnerSessionStatus.AwaitingDecisionApplied))
        {
            return Error(
                "INVALID_SESSION_STATE",
                ProtocolFailureDisposition.RejectMessage,
                "eventBatch requires an initialized session.",
                envelope);
        }

        if (!IdentityMatches(envelope))
        {
            return Error(
                "IDENTITY_MISMATCH",
                ProtocolFailureDisposition.RejectMessage,
                "Event context differs from initialized run identity.",
                envelope);
        }

        var payloadResult = EventBatchPayloadCodec.Decode(envelope.Payload);

        if (!payloadResult.IsSuccess)
        {
            return PayloadError(payloadResult.Error!, envelope);
        }

        var payload = payloadResult.Value!;
        var firstSequence = payload.Events[0].EventSequence.Value;
        var lastSequence = payload.Events[^1].EventSequence.Value;
        var key = new BatchKey(
            envelope.RunId!,
            envelope.ScenarioId!,
            envelope.EpochId!.Value.Value,
            firstSequence,
            lastSequence);
        var canonicalBatchHash = CalculateCanonicalBatchHash(envelope);

        if (_lastBatch is not null && _lastBatch.Key == key)
        {
            if (string.Equals(
                    _lastBatch.CanonicalBatchHash,
                    canonicalBatchHash,
                    StringComparison.Ordinal))
            {
                return new RunnerSessionResult(_lastBatch.Response);
            }

            return Error(
                "DUPLICATE_PAYLOAD_CONFLICT",
                ProtocolFailureDisposition.FailSession,
                "A retried batch key contains different canonical payload bytes.",
                envelope);
        }

        if (_lastBatch is not null
            && firstSequence <= _lastBatch.Key.LastEventSequence)
        {
            return Error(
                "EVENT_SEQUENCE_OVERLAP",
                ProtocolFailureDisposition.FailSession,
                "A partial event batch overlap is not a valid retry.",
                envelope);
        }

        if (Status == RunnerSessionStatus.AwaitingDecisionApplied)
        {
            return Error(
                "INVALID_SESSION_STATE",
                ProtocolFailureDisposition.RejectMessage,
                "The previous decision must be acknowledged before another batch.",
                envelope);
        }

        var orderingError = EventBatchOrderingValidator.Validate(
            envelope,
            payload,
            new EventBatchOrderingState(
                _appliedEpoch,
                _nextEventSequence,
                _simulationTimeMilliseconds));

        if (orderingError is not null)
        {
            ProtocolErrorCodes.TryGetDisposition(
                orderingError.ProtocolCode,
                out var disposition);
            return Error(
                orderingError.ProtocolCode,
                disposition,
                orderingError.Message,
                envelope);
        }

        var nextSequence = checked(lastSequence + 1);
        var nextEpoch = envelope.EpochId!.Value.Value;
        var nextSimTime = envelope.SimTime!.Value.Value;
        EpochId.TryCreate(nextEpoch, out var nextEpochId);
        EventSequence.TryCreate(nextSequence, out var nextEventSequence);
        SimulationTimeMilliseconds.TryCreate(nextSimTime, out var nextSimulationTime);
        Sha256Hex stateAfterHash;
        var zero = ProtocolHash.ZeroHash;
        DecisionPayload shell;

        if (IsOnlineMode)
        {
            var online = BuildOnlineDecision(envelope);

            if (online.Error is not null)
            {
                return Error(
                    online.Error.Code,
                    online.Error.Disposition,
                    online.Error.Message,
                    envelope);
            }

            stateAfterHash = online.StateAfterHash!;
            shell = new DecisionPayload(
                DecisionProductionStatus.Produced,
                online.ReasonCode!,
                online.Actions!,
                online.Certificate!,
                online.Solver!,
                _stateHash,
                stateAfterHash,
                _previousDecisionHash,
                zero);
        }
        else
        {
            stateAfterHash = ProtocolHash.CalculateStateIdentityHash(
                nextEpochId,
                nextEventSequence,
                nextSimulationTime);
            shell = new DecisionPayload(
                DecisionProductionStatus.NotProduced,
                DecisionPayloadCodec.StructuralOnlyReasonCode,
                [],
                new CertificateShell(
                    CertificateStatus.NotProduced,
                    DecisionPayloadCodec.CertificateNotAvailableReasonCode),
                new SolverStatusShell(SolverStatus.NotRun),
                _stateHash,
                stateAfterHash,
                _previousDecisionHash,
                zero);
        }

        var canonicalInput = CreateCanonicalInput(envelope);
        var canonicalDecision = CanonicalJson.Canonicalize(
            DecisionPayloadCodec.Encode(shell, hashProjection: true));
        var decisionHash = ProtocolHash.CalculateDecisionHash(
            _previousDecisionHash,
            _manifestHash,
            _identity!.Manifest.PolicyVersion,
            canonicalInput,
            canonicalDecision);
        var decision = shell with { DecisionHash = decisionHash };
        var response = CreateEnvelope(
            "decision",
            DecisionPayloadCodec.Encode(decision),
            envelope.RunId,
            envelope.ScenarioId,
            envelope.EpochId,
            envelope.SimTime);

        _pending = new PendingDecision(
            nextEpoch,
            nextSequence,
            nextSimTime,
            stateAfterHash,
            decisionHash);
        _lastBatch = new CachedBatch(key, canonicalBatchHash, response);
        Status = RunnerSessionStatus.AwaitingDecisionApplied;

        return new RunnerSessionResult(response);
    }

    private RunnerSessionResult ProcessDecisionApplied(ProtocolEnvelope envelope)
    {
        if (Status != RunnerSessionStatus.AwaitingDecisionApplied
            || _pending is null)
        {
            return Error(
                "INVALID_SESSION_STATE",
                ProtocolFailureDisposition.RejectMessage,
                "decisionApplied requires a pending decision.",
                envelope);
        }

        if (!IdentityMatches(envelope)
            || envelope.EpochId!.Value.Value != _pending.Epoch
            || envelope.SimTime!.Value.Value != _pending.SimulationTimeMilliseconds)
        {
            return Error(
                "IDENTITY_MISMATCH",
                ProtocolFailureDisposition.RejectMessage,
                "decisionApplied context differs from the pending decision.",
                envelope);
        }

        var payloadResult = DecisionAppliedPayloadCodec.Decode(envelope.Payload);

        if (!payloadResult.IsSuccess)
        {
            return PayloadError(payloadResult.Error!, envelope);
        }

        if (payloadResult.Value!.DecisionHash != _pending.DecisionHash)
        {
            return Error(
                "HASH_MISMATCH",
                ProtocolFailureDisposition.FailSession,
                "decisionApplied hash differs from the pending decision hash.",
                envelope);
        }

        if (IsOnlineMode)
        {
            var acknowledged =
                _onlineCoordinator!.ApplyDecisionAcknowledgement(
                    _pending.Epoch);

            if (!acknowledged.IsSuccess)
            {
                return Error(
                    "INTERNAL_ERROR",
                    ProtocolFailureDisposition.FailSession,
                    acknowledged.Witness!.Message,
                    envelope);
            }
        }

        _appliedEpoch = _pending.Epoch;
        _nextEventSequence = _pending.NextEventSequence;
        _simulationTimeMilliseconds = _pending.SimulationTimeMilliseconds;
        _stateHash = _pending.StateHash;
        _previousDecisionHash = _pending.DecisionHash;
        _pending = null;
        Status = RunnerSessionStatus.Initialized;
        return new RunnerSessionResult(null);
    }

    private RunnerSessionResult ProcessCheckpoint(ProtocolEnvelope envelope)
    {
        if (Status != RunnerSessionStatus.Initialized
            || !IsOnlineMode
            || _onlineCoordinator is null
            || _pending is not null
            || !IdentityMatches(envelope))
        {
            return Error(
                "INVALID_SESSION_STATE",
                ProtocolFailureDisposition.RejectMessage,
                "checkpoint requires an initialized online session with no pending decision.",
                envelope);
        }

        if (envelope.Payload.EnumerateObject().Any())
        {
            return Error(
                "UNKNOWN_FIELD",
                ProtocolFailureDisposition.RejectMessage,
                "checkpoint request payload must be empty.",
                envelope);
        }

        using var stateDocument = JsonDocument.Parse(
            OnlineStateCanonicalizer.Canonicalize(
                _onlineCoordinator.CommittedState));
        var content = new CheckpointContent(
            _manifestHash,
            _stateHash,
            _previousDecisionHash,
            _appliedEpoch,
            _nextEventSequence,
            _simulationTimeMilliseconds,
            stateDocument.RootElement.Clone());
        var hash = CheckpointPayloadCodec.CalculateHash(content);
        var payload = new CheckpointPayload(
            CheckpointPayloadCodec.CurrentVersion,
            hash,
            content);

        return new RunnerSessionResult(
            CreateEnvelope(
                "checkpoint",
                CheckpointPayloadCodec.Encode(payload),
                envelope.RunId,
                envelope.ScenarioId));
    }

    private RunnerSessionResult ProcessRestore(ProtocolEnvelope envelope)
    {
        if (Status != RunnerSessionStatus.Initialized
            || !IsOnlineMode
            || _onlineCoordinator is null
            || _pending is not null
            || _appliedEpoch != 0
            || !IdentityMatches(envelope))
        {
            return Error(
                "INVALID_SESSION_STATE",
                ProtocolFailureDisposition.RejectMessage,
                "restore requires a newly initialized online session with no pending decision.",
                envelope);
        }

        var decoded = CheckpointPayloadCodec.Decode(envelope.Payload);

        if (!decoded.IsSuccess)
        {
            return PayloadError(decoded.Error!, envelope);
        }

        var checkpoint = decoded.Value!;

        if (checkpoint.Content.ManifestHash != _manifestHash)
        {
            return Error(
                "MANIFEST_MUTATION",
                ProtocolFailureDisposition.FailSession,
                "Checkpoint manifest hash differs from the initialized run.",
                envelope);
        }

        var restored = OnlineStateCheckpointCodec.Decode(
            checkpoint.Content.OnlineState);

        if (!restored.IsSuccess)
        {
            return Error(
                "SCHEMA_VALIDATION_FAILED",
                ProtocolFailureDisposition.RejectMessage,
                restored.Error!,
                envelope);
        }

        var state = restored.State!;
        var calculatedStateHash = OnlineStateCanonicalizer.CalculateHash(state);

        if (state.Run.Id.Value != _identity!.RunId.Value
            || state.Run.ScenarioId.Value != _identity.ScenarioId.Value
            || state.Run.AppliedEpoch != checkpoint.Content.AppliedEpoch
            || state.NextEventSequence != checkpoint.Content.NextEventSequence
            || state.Run.SimulationTime.Milliseconds
                != checkpoint.Content.SimulationTimeMs
            || calculatedStateHash != checkpoint.Content.StateHash
            || !string.Equals(
                state.ExpectedInitialTravelTimeSnapshotHash,
                _identity.Manifest.TravelTimeSnapshotHash.Value,
                StringComparison.Ordinal))
        {
            return Error(
                "HASH_MISMATCH",
                ProtocolFailureDisposition.FailSession,
                "Checkpoint state identity does not match its content or initialized run.",
                envelope);
        }

        _onlineCoordinator = new EventReductionCoordinator(state);
        _appliedEpoch = checkpoint.Content.AppliedEpoch;
        _nextEventSequence = checkpoint.Content.NextEventSequence;
        _simulationTimeMilliseconds = checkpoint.Content.SimulationTimeMs;
        _stateHash = checkpoint.Content.StateHash;
        _previousDecisionHash = checkpoint.Content.PreviousDecisionHash;
        _lastBatch = null;

        return new RunnerSessionResult(
            CreateEnvelope(
                "restore",
                RestoreAcknowledgedPayloadCodec.Encode(
                    new RestoreAcknowledgedPayload(
                        "restored",
                        checkpoint.CheckpointHash)),
                envelope.RunId,
                envelope.ScenarioId));
    }

    private bool IdentityMatches(ProtocolEnvelope envelope) =>
        _identity is not null
        && envelope.RunId == _identity.RunId
        && envelope.ScenarioId == _identity.ScenarioId;

    private byte[] CreateCanonicalInput(ProtocolEnvelope envelope)
    {
        var envelopeBytes = ProtocolEnvelopeCodec.Encode(envelope);
        using var envelopeDocument = JsonDocument.Parse(envelopeBytes);
        var buffer = new ArrayBufferWriter<byte>();

        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("eventBatch");
            envelopeDocument.RootElement.WriteTo(writer);
            writer.WritePropertyName("stateIdentityBefore");
            writer.WriteStartObject();
            writer.WriteNumber("epochId", _appliedEpoch);
            writer.WriteNumber("nextEventSeq", _nextEventSequence);
            writer.WriteNumber("simTimeMs", _simulationTimeMilliseconds);
            writer.WriteString("stateHash", _stateHash.Value);
            writer.WriteEndObject();

            if (IsOnlineMode)
            {
                writer.WritePropertyName("onlineStateBefore");
                using var onlineDocument = JsonDocument.Parse(
                    OnlineStateCanonicalizer.Canonicalize(
                        _onlineCoordinator!.CommittedState));
                onlineDocument.RootElement.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        return CanonicalJson.Canonicalize(buffer.WrittenSpan);
    }

    private static string CalculateCanonicalBatchHash(ProtocolEnvelope envelope)
    {
        var canonical = CanonicalJson.Canonicalize(
            ProtocolEnvelopeCodec.Encode(envelope));
        return Convert.ToHexStringLower(SHA256.HashData(canonical));
    }

    private OnlineDecisionBuildResult BuildOnlineDecision(
        ProtocolEnvelope envelope)
    {
        if (_onlineCoordinator is null)
        {
            return OnlineDecisionBuildResult.Fail(
                "INTERNAL_ERROR",
                ProtocolFailureDisposition.FailSession,
                "Online coordinator was not initialized.");
        }

        var beforeEventState = _onlineCoordinator.CommittedState;
        var mapped = _onlineEventMapper.Map(envelope);

        if (!mapped.IsSuccess)
        {
            return OnlineDecisionBuildResult.Fail(
                "SCHEMA_VALIDATION_FAILED",
                ProtocolFailureDisposition.RejectMessage,
                mapped.Witness!.Message);
        }

        var reduced = _onlineCoordinator.Propose(mapped.Batch!);

        if (!reduced.IsSuccess)
        {
            return OnlineDecisionBuildResult.Fail(
                "SCHEMA_VALIDATION_FAILED",
                ProtocolFailureDisposition.RejectMessage,
                reduced.Witness!.Message);
        }

        CommitmentCandidateFilter? commitmentFilter = null;
        ICommitmentPolicyProvider validationPolicies = _commitmentPolicies!;
        RollingCostDecisionResult decision;

        var publicationScope = CalculateCanonicalBatchHash(envelope);
        var sourceEventSequence = mapped.Batch!.Events[^1].EventSequence;

        if (_wp4Configuration is not null)
        {
            var context = new CommitmentMechanismContext(
                beforeEventState,
                reduced.ProposedState!,
                _commitmentPolicies!,
                _stopDistances!,
                publicationScope,
                sourceEventSequence,
                InitialPromiseTrigger: _wp4Configuration.InitialPromiseTrigger);

            if (_wp4Configuration.MultiplePlanOptions is not null)
            {
                validationPolicies = MechanismCommitmentPolicyProvider
                    .RevisionPenalty(_commitmentPolicies!);
                commitmentFilter = new CommitmentCandidateFilter(
                    beforeEventState,
                    validationPolicies,
                    _stopDistances!,
                    publicationScope,
                    sourceEventSequence,
                    _commitmentValidator,
                    _wp4Configuration.InitialPromiseTrigger);
                var multiple = _multiplePlanPolicy.Decide(
                    reduced.ProposedState!,
                    _candidateOptions,
                    _wp4Configuration.MultiplePlanOptions,
                    commitmentFilter);

                if (!multiple.IsSuccess)
                {
                    _onlineCoordinator.DiscardPendingProposal();
                    return OnlineDecisionBuildResult.Fail(
                        "INTERNAL_ERROR",
                        ProtocolFailureDisposition.FailSession,
                        multiple.Witness!.Message);
                }

                decision = RollingCostDecisionResult.Success(
                    multiple.Decision!.DistinguishedDecision with
                    {
                        GenerationDiagnostics =
                            multiple.Decision.GenerationDiagnostics,
                    });
            }
            else
            {
                var solved = _solverBackedPolicy!.Decide(
                    context,
                    _candidateOptions,
                    _useManifestSolverSeed
                        ? _wp4Configuration.CreateSolverPolicyOptionsForRun(
                            _identity!.Manifest.MasterSeed)
                        : _wp4Configuration.SolverPolicyOptions!,
                    _wp4Configuration);

                if (!solved.IsSuccess)
                {
                    _onlineCoordinator.DiscardPendingProposal();
                    return OnlineDecisionBuildResult.Fail(
                        "INTERNAL_ERROR",
                        ProtocolFailureDisposition.FailSession,
                        solved.Witness!.Message);
                }

                decision = RollingCostDecisionResult.Success(
                    solved.Decision!.Decision);
                validationPolicies = solved.Decision.EffectivePolicies;
            }
        }
        else
        {
            if (_executionMode == RunnerExecutionMode.OnlineCommitment)
            {
                commitmentFilter = new CommitmentCandidateFilter(
                    beforeEventState,
                    _commitmentPolicies!,
                    _stopDistances!,
                    publicationScope,
                    sourceEventSequence,
                    _commitmentValidator,
                    _wp4Configuration?.InitialPromiseTrigger
                        ?? InitialPromiseTrigger.InitialAcceptance);
            }

            decision = _rollingCostPolicy.Decide(
                reduced.ProposedState!,
                _candidateOptions,
                commitmentFilter);
        }

        if (!decision.IsSuccess)
        {
            _onlineCoordinator.DiscardPendingProposal();
            return OnlineDecisionBuildResult.Fail(
                "INTERNAL_ERROR",
                ProtocolFailureDisposition.FailSession,
                decision.Witness!.Message);
        }

        var stateToStage = decision.Decision!.ProposedState;
        IReadOnlyList<PromisePublication> publications = [];
        CertificateShell certificate;

        if (_executionMode == RunnerExecutionMode.OnlineCommitment)
        {
            var validation = _commitmentValidator.Validate(
                new CommitmentValidationContext(
                    beforeEventState,
                    reduced.ProposedState!,
                    decision.Decision.ProposedState,
                    validationPolicies,
                    _stopDistances!,
                    publicationScope,
                    sourceEventSequence,
                    InitialPromiseTrigger:
                        _wp4Configuration?.InitialPromiseTrigger
                            ?? InitialPromiseTrigger.InitialAcceptance));

            if (!validation.IsValid)
            {
                _onlineCoordinator.DiscardPendingProposal();
                var witness = validation.Witnesses[0];
                return OnlineDecisionBuildResult.Fail(
                    witness.Code,
                    ProtocolFailureDisposition.RejectMessage,
                    witness.Message);
            }

            stateToStage = validation.ValidatedState!;
            publications = validation.Publications;
            var afterHash = OnlineStateCanonicalizer.CalculateHash(stateToStage);
            certificate = new CertificateShell(
                CertificateStatus.Produced,
                "VALIDATED",
                new CommitmentCertificateBody(
                    "1.0.0",
                    "commitment-validator-v1",
                    true,
                    _stateHash,
                    afterHash,
                    publications.Select(value => value.PublicationId).ToArray(),
                    stateToStage.Run.Vehicles.Count,
                    stateToStage.Run.Requests.Values.Count(
                        value => value.IsAcceptedActive),
                    []));
        }
        else
        {
            certificate = new CertificateShell(
                CertificateStatus.NotProduced,
                DecisionPayloadCodec.CertificateNotAvailableReasonCode);
        }

        var staged = _onlineCoordinator.StageDecisionState(
            reduced.ProposedState!,
            stateToStage);

        if (!staged.IsSuccess)
        {
            _onlineCoordinator.DiscardPendingProposal();
            return OnlineDecisionBuildResult.Fail(
                "INTERNAL_ERROR",
                ProtocolFailureDisposition.FailSession,
                staged.Witness!.Message);
        }

        var actions = OnlineDecisionActionMapper.Map(
                decision.Decision,
                beforeEventState,
                _wp4Configuration?.InitialPromiseTrigger
                    == InitialPromiseTrigger.BookingConfirmation)
            .Concat(OnlineDecisionActionMapper.MapPublications(publications))
            .ToArray();
        var reasonCode = decision.Decision.RequestActions.Any(
            value => value.Outcome == RequestDecisionOutcome.Accepted)
            || decision.Decision.RequestActions.Count == 0
                ? DecisionReasonCodes.Accepted
                : DecisionReasonCodes.NoFeasibleInsertion;
        var solverStatus = decision.Decision.SelectionExecution?.SolveResult.Status
            switch
        {
            CandidateSelectionSolveStatus.SafeFallback =>
                SolverStatus.SafeFallback,
            CandidateSelectionSolveStatus.Optimal
                or CandidateSelectionSolveStatus.Feasible =>
                SolverStatus.Completed,
            _ when _wp4Configuration?.MultiplePlanOptions is not null =>
                SolverStatus.Completed,
            _ => SolverStatus.NotRun,
        };
        return OnlineDecisionBuildResult.Success(
            OnlineStateCanonicalizer.CalculateHash(
                stateToStage),
            reasonCode,
            actions,
            certificate,
            new SolverStatusShell(solverStatus));
    }

    private RunnerSessionResult PayloadError(
        ProtocolPayloadError error,
        ProtocolEnvelope envelope)
    {
        var code = error.Code == ProtocolPayloadErrorCode.UnknownField
            ? "UNKNOWN_FIELD"
            : "SCHEMA_VALIDATION_FAILED";
        return Error(
            code,
            ProtocolFailureDisposition.RejectMessage,
            error.Message,
            envelope);
    }

    private RunnerSessionResult Error(
        string code,
        ProtocolFailureDisposition disposition,
        string message,
        ProtocolEnvelope? context = null)
    {
        if (disposition == ProtocolFailureDisposition.FailSession)
        {
            Status = RunnerSessionStatus.Failed;
        }

        var payload = new ErrorPayload(
            code,
            disposition,
            ErrorPayloadCodec.Sanitize(message));

        return new RunnerSessionResult(
            CreateEnvelope(
                "error",
                ErrorPayloadCodec.Encode(payload),
                context?.RunId,
                context?.ScenarioId,
                context?.EpochId,
                context?.SimTime),
            ShouldTerminate:
                disposition == ProtocolFailureDisposition.TerminateProcess);
    }

    private static ProtocolEnvelope CreateEnvelope(
        string messageType,
        byte[] payloadBytes,
        RunId? runId = null,
        ScenarioId? scenarioId = null,
        EpochId? epochId = null,
        SimulationTimeMilliseconds? simTime = null)
    {
        ProtocolMessageType.TryParse(messageType, out var parsedMessageType);
        using var document = JsonDocument.Parse(payloadBytes);

        return new ProtocolEnvelope(
            ProtocolVersion.Current,
            parsedMessageType!,
            document.RootElement.Clone(),
            runId,
            scenarioId,
            epochId,
            simTime);
    }

    private static string MapEnvelopeError(ProtocolEnvelopeErrorCode code) =>
        code switch
        {
            ProtocolEnvelopeErrorCode.MalformedJson => "MALFORMED_JSON",
            ProtocolEnvelopeErrorCode.UnknownField => "UNKNOWN_FIELD",
            ProtocolEnvelopeErrorCode.InvalidSchemaVersion =>
                "INVALID_SCHEMA_VERSION",
            ProtocolEnvelopeErrorCode.UnknownMessageType => "UNKNOWN_MESSAGE_TYPE",
            ProtocolEnvelopeErrorCode.UnsupportedSchemaMajor =>
                "UNSUPPORTED_SCHEMA_MAJOR",
            ProtocolEnvelopeErrorCode.UnsupportedSchemaMinor =>
                "UNSUPPORTED_SCHEMA_MINOR",
            _ => "SCHEMA_VALIDATION_FAILED",
        };

    private sealed record BatchKey(
        RunId RunId,
        ScenarioId ScenarioId,
        long Epoch,
        long FirstEventSequence,
        long LastEventSequence);

    private sealed record CachedBatch(
        BatchKey Key,
        string CanonicalBatchHash,
        ProtocolEnvelope Response);

    private sealed record PendingDecision(
        long Epoch,
        long NextEventSequence,
        long SimulationTimeMilliseconds,
        Sha256Hex StateHash,
        Sha256Hex DecisionHash);

    private sealed record OnlineDecisionBuildResult(
        Sha256Hex? StateAfterHash,
        string? ReasonCode,
        IReadOnlyList<JsonElement>? Actions,
        CertificateShell? Certificate,
        SolverStatusShell? Solver,
        OnlineDecisionBuildError? Error)
    {
        public static OnlineDecisionBuildResult Success(
            Sha256Hex stateAfterHash,
            string reasonCode,
            IReadOnlyList<JsonElement> actions,
            CertificateShell certificate,
            SolverStatusShell solver) =>
            new(stateAfterHash, reasonCode, actions, certificate, solver, null);

        public static OnlineDecisionBuildResult Fail(
            string code,
            ProtocolFailureDisposition disposition,
            string message) =>
            new(
                null,
                null,
                null,
                null,
                null,
                new OnlineDecisionBuildError(code, disposition, message));
    }

    private sealed record OnlineDecisionBuildError(
        string Code,
        ProtocolFailureDisposition Disposition,
        string Message);

    private bool IsOnlineMode =>
        _executionMode is RunnerExecutionMode.OnlineRollingCost
            or RunnerExecutionMode.OnlineCommitment;
}
