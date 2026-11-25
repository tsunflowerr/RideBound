using System.Buffers;
using System.Text.Json;
using RideBound.Application.State;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;
using RideBound.Runner.StateAudit;

namespace RideBound.Runner.Protocol;

public sealed partial class RunnerSession
{
    private static readonly byte[] EventBatchMember = "{\"eventBatch\":"u8.ToArray();
    private static readonly byte[] OnlineStateMember = ",\"onlineStateBefore\":"u8.ToArray();
    private static readonly byte[] StateIdentityMember = ",\"stateIdentityBefore\":"u8.ToArray();
    private static readonly byte[] InputEnd = "}"u8.ToArray();

    private readonly OnlineStateCanonicalCache _canonicalCache = new();

    private bool UsesLightAuditHash => _c1Configuration?.UsesLightAuditHash == true;

    // The member names, including those in stateIdentityBefore, are in canonical ordinal order.
    private IReadOnlyList<ReadOnlyMemory<byte>> CreateCanonicalInputParts(byte[] canonicalBatch)
    {
        var identity = new ArrayBufferWriter<byte>(160);
        using (var writer = new Utf8JsonWriter(identity))
        {
            writer.WriteStartObject();
            writer.WriteNumber("epochId", _appliedEpoch);
            writer.WriteNumber("nextEventSeq", _nextEventSequence);
            writer.WriteNumber("simTimeMs", _simulationTimeMilliseconds);
            writer.WriteString("stateHash", _stateHash.Value);
            writer.WriteEndObject();
        }

        return _onlineCoordinator is { } coordinator && !UsesLightAuditHash
            ?
            [
                EventBatchMember, canonicalBatch,
                OnlineStateMember, .. _canonicalCache.State(coordinator.CommittedState).Parts,
                StateIdentityMember, identity.WrittenMemory, InputEnd,
            ]
            : [EventBatchMember, canonicalBatch, StateIdentityMember, identity.WrittenMemory, InputEnd];
    }

    private static Sha256Hex CalculateLightStateIdentityHash(OnlineState state)
    {
        if (!EpochId.TryCreate(state.Run.AppliedEpoch, out var epoch)
            || !EventSequence.TryCreate(state.NextEventSequence, out var sequence)
            || !SimulationTimeMilliseconds.TryCreate(state.Run.SimulationTime.Milliseconds, out var time))
        {
            throw new InvalidOperationException("The online state has an invalid protocol identity.");
        }

        return ProtocolHash.CalculateStateIdentityHash(epoch, sequence, time);
    }
}
