using RideBound.Application.State;
using RideBound.Application.Travel;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;
using RideBound.Domain.Runs;
using RideBound.Domain.Vehicles;

namespace RideBound.Algorithms.Candidates;

public sealed record ForwardSlackStop(
    StopId StopId,
    SimTime ArrivalTime,
    SimTime ServiceStartTime,
    SimTime DepartureTime,
    long WaitingBeforeServiceMilliseconds,
    long? LocalDeadlineSlackMilliseconds,
    long? CertifiedDelayBeforeArrivalMilliseconds);

/// <summary>
/// A conservative delay certificate for a fixed route and fixed projected pickup
/// schedule. Null delay means unbounded by the remaining hard time constraints.
/// The profile can prove that a pure delay is safe; exceeding it never proves
/// infeasibility and therefore cannot replace PhysicalPlanValidator.
/// </summary>
public sealed record ForwardSlackProfile(
    CandidateSchedule Schedule,
    IReadOnlyList<ForwardSlackStop> Stops,
    long? CertifiedDelayAtRouteStartMilliseconds);

public sealed record ForwardSlackProfileBuildResult
{
    private ForwardSlackProfileBuildResult(
        ForwardSlackProfile? profile,
        string? code,
        string? message)
    {
        Profile = profile;
        Code = code;
        Message = message;
    }

    public bool IsSuccess => Profile is not null;

    public ForwardSlackProfile? Profile { get; }

    public string? Code { get; }

    public string? Message { get; }

    public static ForwardSlackProfileBuildResult Success(
        ForwardSlackProfile profile) =>
        new(profile, null, null);

    public static ForwardSlackProfileBuildResult Failure(
        string code,
        string message) =>
        new(null, code, message);
}

public interface IForwardSlackProfileBuilder
{
    ForwardSlackProfileBuildResult Build(
        OnlineState state,
        VehicleState vehicle,
        RoutePlan route,
        TravelTimeSnapshot travelTimes,
        SimTime evaluationTime);
}

public sealed class ForwardSlackProfileBuilder : IForwardSlackProfileBuilder
{
    private readonly CandidateScheduleEvaluator _scheduleEvaluator;

    public ForwardSlackProfileBuilder(
        CandidateScheduleEvaluator? scheduleEvaluator = null)
    {
        _scheduleEvaluator = scheduleEvaluator ?? new CandidateScheduleEvaluator();
    }

    public ForwardSlackProfileBuildResult Build(
        OnlineState state,
        VehicleState vehicle,
        RoutePlan route,
        TravelTimeSnapshot travelTimes,
        SimTime evaluationTime)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(vehicle);
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(travelTimes);

        var evaluated = _scheduleEvaluator.Evaluate(
            state,
            vehicle,
            route,
            travelTimes,
            evaluationTime);

        if (!evaluated.IsSuccess)
        {
            return Failure(evaluated.Message!);
        }

        var routeStops = route.RemainingStops.ToArray();
        var scheduledStops = evaluated.Schedule!.Stops.ToArray();

        if (routeStops.Length != scheduledStops.Length
            || routeStops.Where(
                    (stop, index) => stop.StopId != scheduledStops[index].StopId)
                .Any())
        {
            return Failure(
                "Projected schedule does not align with the remaining route.");
        }

        var pickupServiceTimes = new Dictionary<RequestId, SimTime>();

        for (var index = 0; index < routeStops.Length; index++)
        {
            if (routeStops[index].Kind == RouteStopKind.Pickup
                && routeStops[index].RequestId is RequestId requestId)
            {
                pickupServiceTimes[requestId] =
                    scheduledStops[index].ServiceStartTime;
            }
        }

        var localSlack = new long?[routeStops.Length];
        var waiting = new long[routeStops.Length];

        for (var index = 0; index < routeStops.Length; index++)
        {
            var stop = routeStops[index];
            var scheduled = scheduledStops[index];
            waiting[index] = scheduled.ServiceStartTime.Milliseconds
                - scheduled.ArrivalTime.Milliseconds;

            if (waiting[index] < 0)
            {
                return Failure("Projected service starts before arrival.");
            }

            if (stop.Kind == RouteStopKind.Waypoint)
            {
                continue;
            }

            if (stop.RequestId is not RequestId requestId
                || !state.Run.Requests.TryGetValue(requestId, out var request))
            {
                return Failure(
                    "Slack profile cannot resolve a route request.");
            }

            if (stop.Kind == RouteStopKind.Pickup)
            {
                localSlack[index] = request.LatestPickup.Milliseconds
                    - scheduled.ArrivalTime.Milliseconds;
            }
            else
            {
                SimTime? pickupTime = request.ActualPickupTime;

                if (pickupTime is null
                    && pickupServiceTimes.TryGetValue(
                        request.Id,
                        out var projectedPickup))
                {
                    pickupTime = projectedPickup;
                }

                if (pickupTime is null)
                {
                    return Failure(
                        "Drop-off has no actual or projected pickup time.");
                }

                var rideTime = scheduled.ArrivalTime.Milliseconds
                    - pickupTime.Value.Milliseconds;
                localSlack[index] = request.MaxRideTime.Milliseconds - rideTime;
            }

            if (localSlack[index] < 0)
            {
                return Failure(
                    "Route already exceeds a pickup or ride-time deadline.");
            }
        }

        var certified = new long?[routeStops.Length];

        for (var index = routeStops.Length - 1; index >= 0; index--)
        {
            var propagated = index == routeStops.Length - 1
                ? null
                : AddSaturating(waiting[index], certified[index + 1]);
            certified[index] = Minimum(localSlack[index], propagated);
        }

        var entries = routeStops.Select(
                (stop, index) => new ForwardSlackStop(
                    stop.StopId,
                    scheduledStops[index].ArrivalTime,
                    scheduledStops[index].ServiceStartTime,
                    scheduledStops[index].DepartureTime,
                    waiting[index],
                    localSlack[index],
                    certified[index]))
            .ToArray();

        return ForwardSlackProfileBuildResult.Success(
            new ForwardSlackProfile(
                evaluated.Schedule,
                Array.AsReadOnly(entries),
                entries.Length == 0
                    ? null
                    : entries[0].CertifiedDelayBeforeArrivalMilliseconds));
    }

    private static long? AddSaturating(long left, long? right)
    {
        if (right is null)
        {
            return null;
        }

        return left > DomainLimits.MaxCanonicalInteger - right.Value
            ? DomainLimits.MaxCanonicalInteger
            : left + right.Value;
    }

    private static long? Minimum(long? left, long? right)
    {
        if (left is null)
        {
            return right;
        }

        if (right is null)
        {
            return left;
        }

        return Math.Min(left.Value, right.Value);
    }

    private static ForwardSlackProfileBuildResult Failure(string message) =>
        ForwardSlackProfileBuildResult.Failure(
            CandidateGenerationFailureCodes.SlackProfileFailed,
            message);
}

public sealed record ForwardSlackCacheKey(
    RideBoundRun RunSnapshot,
    VehicleState VehicleSnapshot,
    string PositionFingerprint,
    string RouteFingerprint,
    SimTime EvaluationTime,
    long TravelSnapshotVersion,
    string TravelSnapshotHash)
{
    public static ForwardSlackCacheKey Create(
        OnlineState state,
        VehicleState vehicle,
        RoutePlan route,
        TravelTimeSnapshot travelTimes,
        SimTime evaluationTime)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(vehicle);
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(travelTimes);

        return new ForwardSlackCacheKey(
            state.Run,
            vehicle,
            PositionIdentity(vehicle.Position),
            CandidateIdentity.CreateRouteFingerprint(route),
            evaluationTime,
            travelTimes.Version,
            travelTimes.SnapshotHash);
    }

    private static string PositionIdentity(VehiclePosition position) =>
        position switch
        {
            NodePosition node => $"node:{node.NodeId.Value}",
            EdgeProgressPosition edge =>
                $"edge:{edge.FromNodeId.Value}:{edge.ToNodeId.Value}:" +
                $"{edge.EdgeId}:{edge.ProgressPermille}",
            _ => $"unknown:{position.GetType().FullName}",
        };
}

public sealed record ForwardSlackCacheLookup(
    ForwardSlackProfileBuildResult Result,
    bool WasCacheHit,
    ForwardSlackCacheKey Key);

public sealed class ForwardSlackProfileCache
{
    private readonly object _gate = new();
    private readonly IForwardSlackProfileBuilder _builder;
    private readonly int _maximumEntries;
    private readonly Dictionary<ForwardSlackCacheKey, ForwardSlackProfile> _entries = [];

    public ForwardSlackProfileCache(
        IForwardSlackProfileBuilder? builder = null,
        int maximumEntries = 10_000)
    {
        if (maximumEntries < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumEntries));
        }

        _builder = builder ?? new ForwardSlackProfileBuilder();
        _maximumEntries = maximumEntries;
    }

    public long HitCount { get; private set; }

    public long MissCount { get; private set; }

    public int EntryCount
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    public ForwardSlackCacheLookup GetOrBuild(
        OnlineState state,
        VehicleState vehicle,
        RoutePlan route,
        TravelTimeSnapshot travelTimes,
        SimTime evaluationTime)
    {
        var key = ForwardSlackCacheKey.Create(
            state,
            vehicle,
            route,
            travelTimes,
            evaluationTime);

        lock (_gate)
        {
            if (_entries.TryGetValue(key, out var cached))
            {
                HitCount++;
                return new ForwardSlackCacheLookup(
                    ForwardSlackProfileBuildResult.Success(cached),
                    true,
                    key);
            }

            MissCount++;
        }

        var built = _builder.Build(
            state,
            vehicle,
            route,
            travelTimes,
            evaluationTime);

        if (!built.IsSuccess)
        {
            return new ForwardSlackCacheLookup(built, false, key);
        }

        lock (_gate)
        {
            if (_entries.Count >= _maximumEntries)
            {
                _entries.Clear();
            }

            _entries[key] = built.Profile!;
        }

        return new ForwardSlackCacheLookup(built, false, key);
    }
}
