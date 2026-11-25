using System.Security.Cryptography;
using System.Text.Json;
using RideBound.Algorithms.Candidates;
using RideBound.Algorithms.Commitments;
using RideBound.Algorithms.Dispatch;
using RideBound.Algorithms.Policies;
using RideBound.Algorithms.Shareability;
using RideBound.Application.Commitments;
using RideBound.Application.Events;
using RideBound.Application.Optimization;
using RideBound.Application.State;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;
using RideBound.Domain.Common;
using RideBound.Domain.Runs;
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
}

public sealed record RunnerSessionResult(
    ProtocolEnvelope? Response,
    bool ShouldTerminate = false)
{
    private readonly ProtocolEnvelope? _response = Response;

    public StreamedResponse? Streamed { get; private init; }

    public ProtocolEnvelope? Response
    {
        get => _response ?? Streamed?.ToEnvelope();
        init
        {
            _response = value;
            Streamed = null;
        }
    }

    internal static RunnerSessionResult FromStreamed(StreamedResponse response) =>
        new((ProtocolEnvelope?)null) { Streamed = response };
}

public sealed partial class RunnerSession
{
    public const int RetainedBatchResponseCount = 1;

    private readonly CapabilityRequirementProfile _requirements;
    private readonly RunnerExecutionMode _executionMode;
    private readonly OnlineEventMapper _onlineEventMapper;
    private readonly RollingCostPolicy _rollingCostPolicy;
    private readonly CandidateGenerationOptions _candidateOptions;
    private readonly RollingCostSolverConfiguration? _solverConfiguration;
    private readonly OrToolsCandidateSelectionSolver? _solver;
    private readonly C1RunnerConfiguration? _c1Configuration;
    private readonly DispatchStep? _commitmentDispatch;
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
        RollingCostSolverConfiguration? solverConfiguration = null,
        C1RunnerConfiguration? c1Configuration = null)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        if (solverConfiguration is not null && c1Configuration is not null)
        {
            throw new ArgumentException("Choose one online solver configuration.", nameof(c1Configuration));
        }

        if ((solverConfiguration is not null || c1Configuration is not null)
            && (executionMode != RunnerExecutionMode.OnlineRollingCost || candidateOptions is not null))
        {
            throw new ArgumentException(
                "The solver configuration supplies the candidate options and requires online mode.",
                nameof(solverConfiguration));
        }

        _requirements = requirements;
        _executionMode = executionMode;
        _onlineEventMapper = onlineEventMapper ?? new OnlineEventMapper();
        _rollingCostPolicy = rollingCostPolicy ?? new RollingCostPolicy();
        _solverConfiguration = solverConfiguration;
        _c1Configuration = c1Configuration;
        _solver = solverConfiguration is null && c1Configuration is null
            ? null
            : new OrToolsCandidateSelectionSolver();
        _commitmentDispatch = c1Configuration is null
            ? null
            : new DispatchStep(
                new SolverBackedRidePoolingPolicy(_solver!),
                c1Configuration.ShadowAllocation is { } shadow ? new ShadowAllocator(shadow) : null);
        _candidateOptions = c1Configuration?.CandidateGeneration
            ?? solverConfiguration?.CandidateGeneration
            ?? candidateOptions
            ?? new CandidateGenerationOptions(
                maximumCandidatesPerVehicle: 10_000,
                maximumNewRequestsPerVehicle: 4,
                exactSmallMode: false);
    }

    public RunnerSessionStatus Status { get; private set; } = RunnerSessionStatus.New;

    public long AppliedEpoch => _appliedEpoch;

    public long NextEventSequence => _nextEventSequence;

    public Sha256Hex PreviousDecisionHash => _previousDecisionHash;

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

        var manifest = validation.Identity!.Manifest;
        var matchesConfiguration = _c1Configuration?.Matches(manifest)
            ?? _solverConfiguration?.Matches(manifest)
            ?? manifest.PolicyId is not (RollingCostSolverConfiguration.PolicyId or C1RunnerConfiguration.PolicyId);
        if (_executionMode == RunnerExecutionMode.OnlineRollingCost && !matchesConfiguration)
        {
            return Error(
                "IDENTITY_MISMATCH",
                ProtocolFailureDisposition.RejectMessage,
                "The manifest policy ID, version and configuration hash must match the Runner solver configuration.",
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

        if (_executionMode == RunnerExecutionMode.OnlineRollingCost)
        {
            var run = RideBoundRun.Create(
                new RunIdentifier(_identity!.RunId.Value),
                new ScenarioIdentifier(_identity.ScenarioId.Value),
                new SimTime(0));
            _onlineCoordinator = CreateOnlineCoordinator(
                OnlineState.Create(
                    run,
                    _identity.Manifest.TravelTimeSnapshotHash.Value));
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
        var canonicalBatch = CanonicalJson.Canonicalize(ProtocolEnvelopeCodec.Encode(envelope));
        var canonicalBatchHash = Convert.ToHexStringLower(SHA256.HashData(canonicalBatch));

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

        if (_executionMode == RunnerExecutionMode.OnlineRollingCost)
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
                online.Certificate ?? new CertificateShell(
                    CertificateStatus.NotProduced,
                    DecisionPayloadCodec.CertificateNotAvailableReasonCode),
                new SolverStatusShell(online.Solver),
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

        var canonicalInput = CreateCanonicalInputParts(canonicalBatch);
        var canonicalDecision = CanonicalJson.Canonicalize(
            DecisionPayloadCodec.Encode(shell, hashProjection: true));
        var decisionHash = UsesLightAuditHash
            ? ProtocolHash.CalculateDecisionHashLight(
                _previousDecisionHash, _manifestHash, _identity!.Manifest.PolicyVersion, canonicalInput, canonicalDecision)
            : ProtocolHash.CalculateDecisionHash(
                _previousDecisionHash, _manifestHash, _identity!.Manifest.PolicyVersion, canonicalInput, canonicalDecision);
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

        if (_executionMode == RunnerExecutionMode.OnlineRollingCost)
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

    private bool IdentityMatches(ProtocolEnvelope envelope) =>
        _identity is not null
        && envelope.RunId == _identity.RunId
        && envelope.ScenarioId == _identity.ScenarioId;

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

        var mapped = _onlineEventMapper.Map(envelope);

        if (!mapped.IsSuccess)
        {
            return OnlineDecisionBuildResult.Fail(
                "SCHEMA_VALIDATION_FAILED",
                ProtocolFailureDisposition.RejectMessage,
                mapped.Witness!.Message);
        }

        var batch = WithPlanningForecast(mapped.Batch!);
        if (_c1Configuration is { } c1)
        {
            foreach (var arrived in batch.Events.OfType<RequestArrived>())
            {
                if (!c1.TryGetPolicy(arrived.Request.CommitmentPolicyId, out _))
                {
                    return OnlineDecisionBuildResult.Fail(
                        "SCHEMA_VALIDATION_FAILED",
                        ProtocolFailureDisposition.RejectMessage,
                        "The request references a commitment policy absent from the bound configuration.");
                }
            }
        }

        var reduced = _onlineCoordinator.Propose(batch);

        if (!reduced.IsSuccess)
        {
            return OnlineDecisionBuildResult.Fail(
                "SCHEMA_VALIDATION_FAILED",
                ProtocolFailureDisposition.RejectMessage,
                reduced.Witness!.Message);
        }

        var reducedState = reduced.ProposedState!;
        var (decision, proposedState, publications, witness) = DecideOnline(
            _onlineCoordinator.CommittedState, reducedState, batch.Events[^1].EventSequence);
        if (witness is not null)
        {
            _onlineCoordinator.DiscardPendingProposal();
            return OnlineDecisionBuildResult.Fail(
                "INTERNAL_ERROR",
                ProtocolFailureDisposition.FailSession,
                witness.Message);
        }

        var staged = _onlineCoordinator.StageDecisionState(reducedState, proposedState!);
        if (!staged.IsSuccess)
        {
            _onlineCoordinator.DiscardPendingProposal();
            return OnlineDecisionBuildResult.Fail(
                "INTERNAL_ERROR",
                ProtocolFailureDisposition.FailSession,
                staged.Witness!.Message);
        }

        var actions = OnlineDecisionActionMapper.Map(decision!)
            .Concat(OnlineDecisionActionMapper.MapPublications(publications))
            .ToArray();
        var reasonCode = decision!.RequestActions.Any(
            value => value.Outcome == RequestDecisionOutcome.Accepted)
            || decision.RequestActions.Count == 0
                ? DecisionReasonCodes.Accepted
                : DecisionReasonCodes.NoFeasibleInsertion;
        var solverStatus = decision.SelectionExecution?.SolveResult.Status switch
        {
            CandidateSelectionSolveStatus.SafeFallback => SolverStatus.SafeFallback,
            CandidateSelectionSolveStatus.Optimal or CandidateSelectionSolveStatus.Feasible =>
                SolverStatus.Completed,
            _ => SolverStatus.NotRun,
        };
        var stateAfterHash = UsesLightAuditHash
            ? CalculateLightStateIdentityHash(proposedState!)
            : OnlineStateCanonicalizer.CalculateHash(proposedState!);
        var certificate = _c1Configuration is null
            ? null
            : new CertificateShell(
                CertificateStatus.Produced,
                "VALIDATED",
                new CommitmentCertificateBody(
                    CommitmentContractCodec.CurrentCertificateVersion,
                    "commitment-validator-v1",
                    NormalOperation: true,
                    _stateHash,
                    stateAfterHash,
                    publications.Select(value => value.PublicationId).Order(StringComparer.Ordinal).ToArray(),
                    proposedState!.Run.Vehicles.Count,
                    proposedState.Run.Requests.Values.Count(value => value.IsAcceptedActive),
                    []));
        return OnlineDecisionBuildResult.Success(
            stateAfterHash, reasonCode, actions, solverStatus, certificate);
    }

    private (RollingCostDecision? Decision, OnlineState? State, IReadOnlyList<PromisePublication> Publications,
        RollingCostWitness? Witness) DecideOnline(OnlineState before, OnlineState reduced, long sourceEventSequence)
    {
        if (_c1Configuration is { } configuration)
        {
            var context = new CommitmentMechanismContext(
                before, reduced, configuration, configuration, _manifestHash.Value, sourceEventSequence);
            var result = _commitmentDispatch!.Run(context, _candidateOptions, configuration.Solver);
            return (result.Decision, result.StateToStage, result.Publications, result.Witness);
        }

        var rollingCost = _solverConfiguration is null
            ? _rollingCostPolicy.Decide(reduced, _candidateOptions)
            : _rollingCostPolicy.DecideWithSolver(
                reduced, _candidateOptions, _solver!, _solverConfiguration.ExecutionBudget);
        return (rollingCost.Decision, rollingCost.Decision?.ProposedState, [], rollingCost.Witness);
    }

    private InternalEventBatch WithPlanningForecast(InternalEventBatch batch)
    {
        if (_c1Configuration?.PlanningForecast is not { } forecast
            || !batch.Events.Any(value => value is TravelTimesUpdated))
        {
            return batch;
        }

        return batch with
        {
            Events = batch.Events
                .Select(value => value is TravelTimesUpdated travel
                    ? travel with { Snapshot = travel.Snapshot.WithForecast(forecast) }
                    : value)
                .ToArray(),
        };
    }

    private EventReductionCoordinator CreateOnlineCoordinator(OnlineState state) =>
        new(state, new EventReducer(recordLatePickups: _c1Configuration?.RecordLatePickups == true));

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
        SolverStatus Solver,
        CertificateShell? Certificate,
        OnlineDecisionBuildError? Error)
    {
        public static OnlineDecisionBuildResult Success(
            Sha256Hex stateAfterHash,
            string reasonCode,
            IReadOnlyList<JsonElement> actions,
            SolverStatus solverStatus,
            CertificateShell? certificate) =>
            new(stateAfterHash, reasonCode, actions, solverStatus, certificate, null);

        public static OnlineDecisionBuildResult Fail(
            string code,
            ProtocolFailureDisposition disposition,
            string message) =>
            new(
                null,
                null,
                null,
                SolverStatus.NotRun,
                null,
                new OnlineDecisionBuildError(code, disposition, message));
    }

    private sealed record OnlineDecisionBuildError(
        string Code,
        ProtocolFailureDisposition Disposition,
        string Message);
}
