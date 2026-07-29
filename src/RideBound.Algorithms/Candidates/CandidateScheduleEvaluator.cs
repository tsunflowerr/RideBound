using RideBound.Application.State;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;
using RideBound.Domain.Validation;
using RideBound.Domain.Vehicles;

namespace RideBound.Algorithms.Candidates;

public sealed record CandidateScheduleEvaluationResult
{
    private CandidateScheduleEvaluationResult(
        CandidateSchedule? schedule,
        string? code,
        string? message)
    {
        Schedule = schedule;
        Code = code;
        Message = message;
    }

    public bool IsSuccess => Schedule is not null;

    public CandidateSchedule? Schedule { get; }

    public string? Code { get; }

    public string? Message { get; }

    public static CandidateScheduleEvaluationResult Success(
        CandidateSchedule schedule) =>
        new(schedule, null, null);

    public static CandidateScheduleEvaluationResult Failure(
        string code,
        string message) =>
        new(null, code, message);
}

public sealed class CandidateScheduleEvaluator
{
    public CandidateScheduleEvaluationResult Evaluate(
        OnlineState state,
        VehicleState vehicle,
        RoutePlan candidateRoute,
        ITravelTimeLookup travelTimes,
        SimTime evaluationTime)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(vehicle);
        ArgumentNullException.ThrowIfNull(candidateRoute);
        ArgumentNullException.ThrowIfNull(travelTimes);

        var time = evaluationTime;
        NodeId currentNode;

        if (vehicle.Position is NodePosition node)
        {
            currentNode = node.NodeId;
        }
        else if (vehicle.Position is EdgeProgressPosition edge)
        {
            if (!travelTimes.TryGetTravelTime(
                    edge.FromNodeId,
                    edge.ToNodeId,
                    out var fullEdgeTime))
            {
                return Failure(
                    $"Travel snapshot has no current directed edge " +
                    $"'{edge.FromNodeId}->{edge.ToNodeId}'.");
            }

            long remaining;

            try
            {
                remaining = DivideRoundUp(
                    checked(
                        fullEdgeTime.Milliseconds
                        * (1000 - edge.ProgressPermille)),
                    1000);
                time = Add(time, new Duration(remaining));
            }
            catch (Exception error) when (
                error is OverflowException or ArgumentOutOfRangeException)
            {
                return Failure("Current edge schedule exceeds canonical time.");
            }

            currentNode = edge.ToNodeId;
        }
        else
        {
            return Failure("Vehicle position type is unsupported.");
        }

        var stops = new List<ScheduledStop>();

        foreach (var stop in candidateRoute.RemainingStops)
        {
            if (!travelTimes.TryGetTravelTime(
                    currentNode,
                    stop.NodeId,
                    out var travelTime))
            {
                return Failure(
                    $"Travel snapshot has no directed arc " +
                    $"'{currentNode}->{stop.NodeId}'.");
            }

            SimTime arrival;
            SimTime serviceStart;
            SimTime departure;

            try
            {
                arrival = Add(time, travelTime);
                serviceStart = GetServiceStart(state, stop, arrival);
                departure = Add(serviceStart, stop.ServiceDuration);
            }
            catch (Exception error) when (
                error is OverflowException or ArgumentOutOfRangeException)
            {
                return Failure("Candidate schedule exceeds canonical time.");
            }

            stops.Add(
                new ScheduledStop(
                    stop.StopId,
                    arrival,
                    serviceStart,
                    departure));
            time = departure;
            currentNode = stop.NodeId;
        }

        var cost = time.Milliseconds - evaluationTime.Milliseconds;

        if (cost < 0)
        {
            return Failure("Candidate operational cost cannot be negative.");
        }

        return CandidateScheduleEvaluationResult.Success(
            new CandidateSchedule(stops.AsReadOnly(), cost));
    }

    private static SimTime GetServiceStart(
        OnlineState state,
        RouteStop stop,
        SimTime arrival)
    {
        if (stop.Kind != RouteStopKind.Pickup
            || stop.RequestId is not RequestId requestId
            || !state.Run.Requests.TryGetValue(requestId, out var request))
        {
            return arrival;
        }

        return arrival.Milliseconds < request.EarliestPickup.Milliseconds
            ? request.EarliestPickup
            : arrival;
    }

    private static SimTime Add(SimTime time, Duration duration) =>
        time + duration;

    private static long DivideRoundUp(long value, long divisor) =>
        value / divisor + (value % divisor == 0 ? 0 : 1);

    private static CandidateScheduleEvaluationResult Failure(string message) =>
        CandidateScheduleEvaluationResult.Failure(
            CandidateGenerationFailureCodes.ScheduleEvaluationFailed,
            message);
}
