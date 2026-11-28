using System.Text.Json;
using RideBound.Application.State;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;
using RideBound.Runner.Online;
using RideBound.Runner.StateAudit;

namespace RideBound.Runner.Protocol;

public sealed partial class RunnerSession
{
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
        var canonical = OnlineStateCanonicalizer.CanonicalizeSegments(committed);
        if ((_canonicalCache.FindState(committed) is { } cached && !cached.ContentEquals(canonical))
            || CheckpointStateHash(committed, canonical) != _stateHash)
        {
            return Error(
                "INTERNAL_ERROR",
                ProtocolFailureDisposition.FailSession,
                "Committed state does not match the acknowledged hash.",
                envelope);
        }

        using var emptyState = JsonDocument.Parse("{}");
        var shell = new CheckpointContent(
            _manifestHash, _stateHash, _previousDecisionHash,
            _appliedEpoch, _nextEventSequence, _simulationTimeMilliseconds, emptyState.RootElement);
        var hash = CheckpointPayloadCodec.CalculateHash(shell, canonical.Parts);
        var canonicalShell = CanonicalJson.Serialize(CreateEnvelope(
            "checkpoint", CheckpointPayloadCodec.EncodeShell(hash, shell), envelope.RunId, envelope.ScenarioId));
        var (before, after) = CheckpointPayloadCodec.SplitAtEmptyOnlineState(canonicalShell);

        return RunnerSessionResult.FromStreamed(new StreamedResponse(
            "checkpoint", [before, .. canonical.Parts, after]));
    }

    private Sha256Hex CheckpointStateHash(OnlineState state, CanonicalSegments canonical) =>
        (UsesLightAuditHash, state.Run.AppliedEpoch) switch
        {
            (true, _) => CalculateLightStateIdentityHash(state),
            (_, 0) => _stateHash,
            _ => OnlineStateCanonicalizer.HashV1(canonical),
        };
}
