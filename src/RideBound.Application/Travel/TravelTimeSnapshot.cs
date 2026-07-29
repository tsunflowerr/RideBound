using System.Collections.Frozen;
using RideBound.Domain.Common;
using RideBound.Domain.Validation;

namespace RideBound.Application.Travel;

public readonly record struct TravelArc(NodeId FromNodeId, NodeId ToNodeId);

public sealed class TravelTimeSnapshot : ITravelTimeLookup
{
    private readonly FrozenDictionary<TravelArc, Duration> _travelTimes;

    private TravelTimeSnapshot(
        long version,
        string snapshotHash,
        IEnumerable<KeyValuePair<TravelArc, Duration>> travelTimes)
    {
        Version = version;
        SnapshotHash = snapshotHash;
        _travelTimes = travelTimes.ToFrozenDictionary();
    }

    public long Version { get; }

    public string SnapshotHash { get; }

    public IReadOnlyDictionary<TravelArc, Duration> TravelTimes => _travelTimes;

    public static DomainResult<TravelTimeSnapshot> Create(
        long version,
        string snapshotHash,
        IEnumerable<KeyValuePair<TravelArc, Duration>> travelTimes)
    {
        ArgumentNullException.ThrowIfNull(travelTimes);

        if (version is < 1 or > DomainLimits.MaxCanonicalInteger)
        {
            return DomainResult<TravelTimeSnapshot>.Fail(
                TravelFailureCodes.InvalidSnapshot,
                "Travel snapshot version must be a positive canonical integer.",
                dimension: "version");
        }

        if (!IsLowerSha256(snapshotHash))
        {
            return DomainResult<TravelTimeSnapshot>.Fail(
                TravelFailureCodes.InvalidSnapshot,
                "Travel snapshot hash must be 64 lowercase hexadecimal characters.",
                dimension: "snapshotHash");
        }

        var arcs = travelTimes.ToArray();

        if (arcs.Length == 0)
        {
            return DomainResult<TravelTimeSnapshot>.Fail(
                TravelFailureCodes.InvalidSnapshot,
                "Travel snapshot must contain at least one directed arc.",
                dimension: "arcs");
        }

        if (arcs.Select(pair => pair.Key).Distinct().Count() != arcs.Length
            || arcs.Any(pair => pair.Key.FromNodeId == pair.Key.ToNodeId))
        {
            return DomainResult<TravelTimeSnapshot>.Fail(
                TravelFailureCodes.InvalidSnapshot,
                "Travel snapshot arcs must be unique with distinct endpoints.",
                dimension: "arcs");
        }

        return DomainResult<TravelTimeSnapshot>.Success(
            new TravelTimeSnapshot(version, snapshotHash, arcs));
    }

    public bool TryGetTravelTime(
        NodeId fromNodeId,
        NodeId toNodeId,
        out Duration travelTime)
    {
        if (fromNodeId == toNodeId)
        {
            travelTime = new Duration(0);
            return true;
        }

        return _travelTimes.TryGetValue(
            new TravelArc(fromNodeId, toNodeId),
            out travelTime);
    }

    private static bool IsLowerSha256(string? value) =>
        value is { Length: 64 }
        && value.All(character => character is >= '0' and <= '9'
            or >= 'a' and <= 'f');
}

public static class TravelFailureCodes
{
    public const string InvalidSnapshot = "INVALID_TRAVEL_SNAPSHOT";
    public const string StaleSnapshot = "STALE_TRAVEL_SNAPSHOT";
}
