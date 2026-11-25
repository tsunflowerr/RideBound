using System.Text.Json;
using RideBound.Application.State;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;
using RideBound.Runner.Online;

namespace RideBound.Runner.Protocol;

public sealed partial class RunnerSession
{
    private bool _hasRestored;

    private RunnerSessionResult ProcessCheckpoint(ProtocolEnvelope envelope)
    {
        if (Status != RunnerSessionStatus.Initialized
            || _onlineCoordinator is null
            || _pending is not null
            || !IdentityMatches(envelope))
        {
            return Error(
                "INVALID_SESSION_STATE",
                ProtocolFailureDisposition.RejectMessage,
                "checkpoint requires an initialized online session without a pending decision.",
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

        var committed = _onlineCoordinator.CommittedState;
        var canonical = OnlineStateCanonicalizer.Canonicalize(committed);
        if (CheckpointStateHash(committed, canonical) != _stateHash)
        {
            return Error(
                "INTERNAL_ERROR",
                ProtocolFailureDisposition.FailSession,
                "Committed state does not match the acknowledged hash.",
                envelope);
        }

        using var document = JsonDocument.Parse(canonical);
        var content = new CheckpointContent(
            _manifestHash, _stateHash, _previousDecisionHash,
            _appliedEpoch, _nextEventSequence, _simulationTimeMilliseconds, document.RootElement);
        var payload = new CheckpointPayload(
            CheckpointPayloadCodec.CurrentVersion, CheckpointPayloadCodec.CalculateHash(content), content);
        return new RunnerSessionResult(
            CreateEnvelope("checkpoint", CheckpointPayloadCodec.Encode(payload), envelope.RunId, envelope.ScenarioId));
    }

    private RunnerSessionResult ProcessRestore(ProtocolEnvelope envelope)
    {
        if (Status != RunnerSessionStatus.Initialized
            || _onlineCoordinator is null
            || _pending is not null
            || _appliedEpoch != 0
            || _hasRestored
            || !IdentityMatches(envelope))
        {
            return Error(
                "INVALID_SESSION_STATE",
                ProtocolFailureDisposition.RejectMessage,
                "restore requires a newly initialized online session without a pending decision.",
                envelope);
        }

        var decoded = CheckpointPayloadCodec.Decode(envelope.Payload);
        if (!decoded.IsSuccess)
        {
            return PayloadError(decoded.Error!, envelope);
        }

        var checkpoint = decoded.Value!;
        var content = checkpoint.Content;
        if (content.ManifestHash != _manifestHash)
        {
            return Error(
                "MANIFEST_MUTATION",
                ProtocolFailureDisposition.FailSession,
                "Checkpoint manifest hash differs from the initialized run.",
                envelope);
        }

        var restored = OnlineStateCheckpointCodec.Decode(
            content.OnlineState, _identity!.Manifest.TravelTimeSnapshotHash.Value,
            _c1Configuration?.ShadowAllocation?.Threshold);
        if (!restored.IsSuccess)
        {
            return Error(
                "SCHEMA_VALIDATION_FAILED",
                ProtocolFailureDisposition.RejectMessage,
                restored.Error!,
                envelope);
        }

        var state = restored.State!;
        var canonical = OnlineStateCanonicalizer.Canonicalize(state);
        if (state.Run.Id.Value != _identity.RunId.Value
            || state.Run.ScenarioId.Value != _identity.ScenarioId.Value
            || state.Run.AppliedEpoch != content.AppliedEpoch
            || state.NextEventSequence != content.NextEventSequence
            || state.Run.SimulationTime.Milliseconds != content.SimulationTimeMs
            || CheckpointStateHash(state, canonical) != content.StateHash
            || (content.AppliedEpoch == 0) != (content.PreviousDecisionHash == ProtocolHash.ZeroHash))
        {
            return Error(
                "HASH_MISMATCH",
                ProtocolFailureDisposition.FailSession,
                "Checkpoint state or decision chain differs from its declared identity.",
                envelope);
        }

        if (_c1Configuration is { } c1
                ? state.Run.Requests.Values.Any(request => !c1.TryGetPolicy(request.CommitmentPolicyId, out _))
                : state.Commitments.Histories.Count != 0)
        {
            return Error(
                "SCHEMA_VALIDATION_FAILED",
                ProtocolFailureDisposition.RejectMessage,
                "Checkpoint commitment state conflicts with the bound policy.",
                envelope);
        }

        _onlineCoordinator = CreateOnlineCoordinator(state);
        _appliedEpoch = content.AppliedEpoch;
        _nextEventSequence = content.NextEventSequence;
        _simulationTimeMilliseconds = content.SimulationTimeMs;
        _stateHash = content.StateHash;
        _previousDecisionHash = content.PreviousDecisionHash;
        _lastBatch = null;
        _hasRestored = true;

        return new RunnerSessionResult(
            CreateEnvelope(
                "restore",
                RestoreAcknowledgedPayloadCodec.Encode(new RestoreAcknowledgedPayload("restored", checkpoint.CheckpointHash)),
                envelope.RunId,
                envelope.ScenarioId));
    }

    private Sha256Hex CheckpointStateHash(OnlineState state, ReadOnlySpan<byte> canonical) =>
        state.Run.AppliedEpoch == 0
            ? _stateHash
            : OnlineStateCanonicalizer.CalculateCanonicalHash(canonical);
}
