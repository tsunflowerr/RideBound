using System.Text.Json;
using RideBound.Algorithms.Policies;
using RideBound.Contracts.Protocol;
using RideBound.Domain.Routes;
using ContractRouteStopKind = RideBound.Contracts.Protocol.RouteStopKind;
using DomainRouteStopKind = RideBound.Domain.Routes.RouteStopKind;

namespace RideBound.Runner.Online;

public static class OnlineDecisionActionMapper
{
    public static IReadOnlyList<JsonElement> Map(
        RollingCostDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        var actions = new List<JsonElement>();

        foreach (var action in decision.RequestActions.OrderBy(
                     value => value.RequestId.Value,
                     StringComparer.Ordinal))
        {
            actions.Add(MapRequestAction(action));
        }

        foreach (var plan in decision.VehiclePlans
                     .Where(value => !value.Candidate.IsNoOp)
                     .OrderBy(
                         value => value.VehicleId.Value,
                         StringComparer.Ordinal))
        {
            actions.Add(
                OnlineDecisionActionCodec.Encode(
                    new OnlineDecisionAction(
                        DecisionType.VehiclePlanUpdated,
                        new VehiclePlanUpdatedActionPayload(
                            plan.VehicleId.Value,
                            plan.Candidate.CandidateId,
                            MapRoute(plan.Candidate.Route)))));
        }

        return actions.AsReadOnly();
    }

    private static JsonElement MapRequestAction(RequestDecisionAction action)
    {
        return action.Outcome switch
        {
            RequestDecisionOutcome.Accepted =>
                OnlineDecisionActionCodec.Encode(
                    new OnlineDecisionAction(
                        DecisionType.RequestAccepted,
                        new RequestAcceptedActionPayload(
                            action.RequestId.Value,
                            action.VehicleId!.Value.Value,
                            action.CandidateId!))),
            RequestDecisionOutcome.Rejected =>
                OnlineDecisionActionCodec.Encode(
                    new OnlineDecisionAction(
                        DecisionType.RequestRejected,
                        new RequestOutcomeActionPayload(
                            action.RequestId.Value,
                            action.ReasonCode))),
            RequestDecisionOutcome.Deferred =>
                OnlineDecisionActionCodec.Encode(
                    new OnlineDecisionAction(
                        DecisionType.RequestDeferred,
                        new RequestOutcomeActionPayload(
                            action.RequestId.Value,
                            action.ReasonCode))),
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
    }

    private static RoutePlanContract MapRoute(RoutePlan route) =>
        new(
            route.Version.Value,
            route.ExecutedStopCount,
            route.FrozenPrefix.Select(MapStop).ToArray(),
            route.MutableSuffix.Select(MapStop).ToArray());

    private static RouteStopContract MapStop(RouteStop stop) =>
        new(
            stop.StopId.Value,
            stop.NodeId.Value,
            stop.Kind switch
            {
                DomainRouteStopKind.Waypoint => ContractRouteStopKind.Waypoint,
                DomainRouteStopKind.Pickup => ContractRouteStopKind.Pickup,
                DomainRouteStopKind.DropOff => ContractRouteStopKind.DropOff,
                _ => throw new ArgumentOutOfRangeException(nameof(stop)),
            },
            stop.RequestId?.Value,
            stop.ServiceDuration.Milliseconds);
}
