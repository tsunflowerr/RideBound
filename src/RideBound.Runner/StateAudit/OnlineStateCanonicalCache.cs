using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using RideBound.Application.State;
using RideBound.Application.Travel;
using RideBound.Contracts.Serialization;
using RideBound.Runner.Online;

namespace RideBound.Runner.StateAudit;

/// <summary>Reuses canonical fragments by immutable object identity within a Runner session.</summary>
internal sealed class OnlineStateCanonicalCache
{
    private readonly ConditionalWeakTable<OnlineState, CanonicalSegments> _states = new();
    private readonly ConditionalWeakTable<TravelTimeSnapshot, CanonicalSegments> _travel = new();
    private readonly ConditionalWeakTable<object, byte[]> _entities = new();

    public CanonicalSegments? FindState(OnlineState state) =>
        _states.TryGetValue(state, out var result) ? result : null;

    public CanonicalSegments State(OnlineState state) =>
        FindState(state) ?? _states.GetValue(state, value => OnlineStateCanonicalizer.CanonicalizeSegments(value, this));

    public CanonicalSegments Travel(TravelTimeSnapshot snapshot) =>
        _travel.GetValue(snapshot, OnlineStateCanonicalizer.CanonicalizeTravelTimes);

    // Each entity type is written by a single projection. The arrays are exposed only
    // as read-only ranges, and the weak keys release them when the entities are retired.
    public ReadOnlyMemory<byte> Entity<T>(T item, Action<Utf8JsonWriter, T> write)
        where T : class
    {
        if (_entities.TryGetValue(item, out var canonical))
        {
            return canonical;
        }

        return _entities.GetValue(item, key =>
        {
            var buffer = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(buffer))
            {
                write(writer, (T)key);
            }

            return CanonicalJson.Canonicalize(buffer.WrittenSpan);
        });
    }
}
