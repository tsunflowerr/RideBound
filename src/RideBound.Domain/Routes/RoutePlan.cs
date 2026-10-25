using System.Collections.ObjectModel;
using RideBound.Domain.Common;

namespace RideBound.Domain.Routes;

public enum RouteStopKind
{
    Waypoint,
    Pickup,
    DropOff,
}

public sealed record RouteStop
{
    public RouteStop(
        StopId stopId,
        NodeId nodeId,
        RouteStopKind kind,
        RequestId? requestId,
        Duration serviceDuration)
    {
        if (kind == RouteStopKind.Waypoint && requestId is not null
            || kind != RouteStopKind.Waypoint && requestId is null)
        {
            throw new ArgumentException(
                "Waypoint stops omit requestId; pickup/drop-off stops require it.");
        }

        StopId = stopId;
        NodeId = nodeId;
        Kind = kind;
        RequestId = requestId;
        ServiceDuration = serviceDuration;
    }

    public StopId StopId { get; }

    public NodeId NodeId { get; }

    public RouteStopKind Kind { get; }

    public RequestId? RequestId { get; }

    public Duration ServiceDuration { get; }
}

public sealed record RouteLeg(
    NodeId FromNodeId,
    NodeId ToNodeId,
    StopId DestinationStopId);

public sealed class RoutePlan
{
    private RoutePlan(
        PlanVersion version,
        int executedStopCount,
        IReadOnlyList<RouteStop> frozenPrefix,
        IReadOnlyList<RouteStop> mutableSuffix)
    {
        Version = version;
        ExecutedStopCount = executedStopCount;
        FrozenPrefix = frozenPrefix;
        MutableSuffix = mutableSuffix;
    }

    public PlanVersion Version { get; }

    public int ExecutedStopCount { get; }

    public IReadOnlyList<RouteStop> FrozenPrefix { get; }

    public IReadOnlyList<RouteStop> MutableSuffix { get; }

    public IEnumerable<RouteStop> AllStops => FrozenPrefix.Concat(MutableSuffix);

    public IEnumerable<RouteStop> RemainingStops =>
        FrozenPrefix.Skip(ExecutedStopCount).Concat(MutableSuffix);

    public IEnumerable<RouteLeg> GetRemainingLegs(NodeId startNode)
    {
        var from = startNode;

        foreach (var stop in RemainingStops)
        {
            yield return new RouteLeg(from, stop.NodeId, stop.StopId);
            from = stop.NodeId;
        }
    }

    public static DomainResult<RoutePlan> Create(
        PlanVersion version,
        long executedStopCount,
        IEnumerable<RouteStop> frozenPrefix,
        IEnumerable<RouteStop> mutableSuffix)
    {
        ArgumentNullException.ThrowIfNull(frozenPrefix);
        ArgumentNullException.ThrowIfNull(mutableSuffix);
        var frozen = frozenPrefix.ToArray();
        var mutable = mutableSuffix.ToArray();

        if (executedStopCount < 0
            || executedStopCount > frozen.Length
            || executedStopCount > int.MaxValue)
        {
            return DomainResult<RoutePlan>.Fail(
                RouteFailureCodes.InvalidExecutedProgress,
                "Executed stop count must be within the frozen prefix.",
                dimension: "executedStopCount");
        }

        var duplicate = frozen.Concat(mutable)
            .GroupBy(stop => stop.StopId)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            return DomainResult<RoutePlan>.Fail(
                RouteFailureCodes.DuplicateStop,
                $"Route stop '{duplicate.Key}' appears more than once.",
                duplicate.Key.Value,
                "stopId");
        }

        return DomainResult<RoutePlan>.Success(
            new RoutePlan(
                version,
                (int)executedStopCount,
                Array.AsReadOnly(frozen),
                Array.AsReadOnly(mutable)));
    }

    public RoutePlan CreateNoOp() => this;

    public DomainResult<RoutePlan> ReplaceMutableSuffix(
        IEnumerable<RouteStop> mutableSuffix)
    {
        ArgumentNullException.ThrowIfNull(mutableSuffix);
        PlanVersion nextVersion;

        try
        {
            nextVersion = Version.Next();
        }
        catch (OverflowException)
        {
            return DomainResult<RoutePlan>.Fail(
                RouteFailureCodes.PlanVersionOverflow,
                "Plan version cannot advance.",
                dimension: "planVersion");
        }

        return Create(
            nextVersion,
            ExecutedStopCount,
            FrozenPrefix,
            mutableSuffix);
    }

    public DomainResult<RoutePlan> AdvanceReachedStop(StopId reachedStopId)
    {
        RouteStop? expected;
        var fromMutable = false;

        if (ExecutedStopCount < FrozenPrefix.Count)
        {
            expected = FrozenPrefix[ExecutedStopCount];
        }
        else if (MutableSuffix.Count > 0)
        {
            expected = MutableSuffix[0];
            fromMutable = true;
        }
        else
        {
            return DomainResult<RoutePlan>.Fail(
                RouteFailureCodes.NoRemainingStop,
                "The route has no remaining stop.",
                reachedStopId.Value,
                "stopId");
        }

        if (expected.StopId != reachedStopId)
        {
            return DomainResult<RoutePlan>.Fail(
                RouteFailureCodes.UnexpectedReachedStop,
                $"Expected stop '{expected.StopId}', received '{reachedStopId}'.",
                reachedStopId.Value,
                "stopId");
        }

        var frozen = fromMutable
            ? FrozenPrefix.Concat([expected]).ToArray()
            : FrozenPrefix.ToArray();
        var mutable = fromMutable
            ? MutableSuffix.Skip(1).ToArray()
            : MutableSuffix.ToArray();

        return Create(
            Version,
            ExecutedStopCount + 1L,
            frozen,
            mutable);
    }

    public DomainResult<RoutePlan> RemoveRequestFromMutableSuffix(RequestId requestId)
    {
        if (FrozenPrefix
            .Skip(ExecutedStopCount)
            .Any(stop => stop.RequestId == requestId))
        {
            return DomainResult<RoutePlan>.Fail(
                RouteFailureCodes.FrozenPrefix,
                "Cannot remove a request stop from the unexecuted frozen prefix.",
                requestId.Value,
                "frozenPrefix");
        }

        var remaining = MutableSuffix
            .Where(stop => stop.RequestId != requestId)
            .ToArray();

        return remaining.Length == MutableSuffix.Count
            ? DomainResult<RoutePlan>.Fail(
                RouteFailureCodes.RequestStopsMissing,
                "The route has no mutable stop for the request.",
                requestId.Value,
                "mutableSuffix")
            : ReplaceMutableSuffix(remaining);
    }

    public bool HasExactFrozenPrefix(RoutePlan candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return ExecutedStopCount == candidate.ExecutedStopCount
            && FrozenPrefix.SequenceEqual(candidate.FrozenPrefix);
    }

    public bool IsSemanticallyEqual(RoutePlan other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Version == other.Version
            && ExecutedStopCount == other.ExecutedStopCount
            && FrozenPrefix.SequenceEqual(other.FrozenPrefix)
            && MutableSuffix.SequenceEqual(other.MutableSuffix);
    }
}

public static class RouteFailureCodes
{
    public const string InvalidExecutedProgress = "INVALID_EXECUTED_PROGRESS";
    public const string DuplicateStop = "DUPLICATE_STOP";
    public const string PlanVersionOverflow = "PLAN_VERSION_OVERFLOW";
    public const string NoRemainingStop = "NO_REMAINING_STOP";
    public const string UnexpectedReachedStop = "UNEXPECTED_REACHED_STOP";
    public const string FrozenPrefix = "FROZEN_PREFIX";
    public const string RequestStopsMissing = "REQUEST_STOPS_MISSING";
}
