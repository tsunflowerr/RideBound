using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;

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

public sealed record RunnerSessionResult(
    ProtocolEnvelope? Response,
    bool ShouldTerminate = false);

public sealed class RunnerSession
{
    public const int RetainedBatchResponseCount = 1;

    private readonly CapabilityRequirementProfile _requirements;
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

    public RunnerSession(CapabilityRequirementProfile requirements)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        _requirements = requirements;
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
        var payloadHash = CalculatePayloadHash(payload);

        if (_lastBatch is not null && _lastBatch.Key == key)
        {
            if (string.Equals(
                    _lastBatch.PayloadHash,
                    payloadHash,
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
        var stateAfterHash = ProtocolHash.CalculateStateIdentityHash(
            nextEpochId,
            nextEventSequence,
            nextSimulationTime);
        var zero = ProtocolHash.ZeroHash;
        var shell = new DecisionPayload(
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
        _lastBatch = new CachedBatch(key, payloadHash, response);
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
            writer.WriteEndObject();
        }

        return CanonicalJson.Canonicalize(buffer.WrittenSpan);
    }

    private static string CalculatePayloadHash(EventBatchPayload payload)
    {
        var canonical = CanonicalJson.Canonicalize(
            EventBatchPayloadCodec.Encode(payload));
        return Convert.ToHexStringLower(SHA256.HashData(canonical));
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
        string PayloadHash,
        ProtocolEnvelope Response);

    private sealed record PendingDecision(
        long Epoch,
        long NextEventSequence,
        long SimulationTimeMilliseconds,
        Sha256Hex StateHash,
        Sha256Hex DecisionHash);
}
