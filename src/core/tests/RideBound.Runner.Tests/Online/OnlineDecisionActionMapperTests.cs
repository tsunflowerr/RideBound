using System.Text;
using RideBound.Algorithms.Candidates;
using RideBound.Algorithms.Policies;
using RideBound.Application.State;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Tests.Fixtures;
using RideBound.Domain.Common;
using RideBound.Domain.Runs;
using RideBound.Runner.Online;

namespace RideBound.Runner.Tests.Online;

public sealed class OnlineDecisionActionMapperTests
{
    [Fact]
    public void Accepted_action_and_route_bind_the_selected_vehicle_and_candidate()
    {
        var decision = CreateDecision(partySize: 1);
        var selected = Assert.Single(decision.VehiclePlans);
        var accepted = Assert.Single(decision.RequestActions);
        Assert.Equal(RequestDecisionOutcome.Accepted, accepted.Outcome);

        var mapped = OnlineDecisionActionMapper.Map(decision);

        Assert.Equal(2, mapped.Count);
        Assert.Equal("requestAccepted", mapped[0].GetProperty("decisionType").GetString());
        var request = mapped[0].GetProperty("payload");
        Assert.Equal(accepted.RequestId.Value, request.GetProperty("requestId").GetString());
        Assert.Equal(selected.VehicleId.Value, request.GetProperty("vehicleId").GetString());
        Assert.Equal(selected.Candidate.CandidateId, request.GetProperty("candidateId").GetString());

        Assert.Equal("vehiclePlanUpdated", mapped[1].GetProperty("decisionType").GetString());
        var plan = mapped[1].GetProperty("payload");
        Assert.Equal(selected.VehicleId.Value, plan.GetProperty("vehicleId").GetString());
        Assert.Equal(selected.Candidate.CandidateId, plan.GetProperty("candidateId").GetString());
        var route = plan.GetProperty("route");
        Assert.Equal(selected.Candidate.Route.Version.Value, route.GetProperty("planVersion").GetInt64());
        Assert.Equal(selected.Candidate.Route.ExecutedStopCount, route.GetProperty("executedStopCount").GetInt32());
        Assert.Equal(
            selected.Candidate.Route.FrozenPrefix.Select(stop => stop.StopId.Value),
            route.GetProperty("frozenPrefix").EnumerateArray().Select(stop => stop.GetProperty("stopId").GetString()));
        Assert.Equal(
            selected.Candidate.Route.MutableSuffix.Select(stop => stop.StopId.Value),
            route.GetProperty("mutableSuffix").EnumerateArray().Select(stop => stop.GetProperty("stopId").GetString()));
        Assert.Equal(
            selected.Candidate.Route.MutableSuffix.Select(stop => stop.ServiceDuration.Milliseconds),
            route.GetProperty("mutableSuffix").EnumerateArray().Select(stop => stop.GetProperty("serviceDurationMs").GetInt64()));
    }

    [Fact]
    public void Capacity_rejection_preserves_reason_and_omits_unchanged_vehicle_plan()
    {
        var decision = CreateDecision(partySize: 5);
        Assert.All(decision.VehiclePlans, plan => Assert.True(plan.Candidate.IsNoOp));
        Assert.Equal(0, decision.AcceptedRequestCount);

        var mapped = OnlineDecisionActionMapper.Map(decision);

        var action = Assert.Single(mapped);
        Assert.Equal("requestRejected", action.GetProperty("decisionType").GetString());
        var payload = action.GetProperty("payload");
        Assert.Equal("r-1", payload.GetProperty("requestId").GetString());
        Assert.Equal("CAPACITY", payload.GetProperty("reasonCode").GetString());
        Assert.False(payload.TryGetProperty("vehicleId", out _));
        Assert.False(payload.TryGetProperty("candidateId", out _));
    }

    private static RollingCostDecision CreateDecision(int partySize)
    {
        var result = new RollingCostPolicy().Decide(
            CreateReducedState(partySize),
            CandidateGenerationOptions.ExactSmall);
        Assert.True(result.IsSuccess, result.Witness?.Message);
        return result.Decision!;
    }

    [Fact]
    public void Deferred_request_keeps_its_reason_without_publishing_a_vehicle_plan()
    {
        var decision = new RollingCostDecision(
            CreateReducedState(partySize: 1),
            [],
            [new RequestDecisionAction(new RequestId("r-1"), RequestDecisionOutcome.Deferred, "WAIT_FOR_INPUT")],
            [],
            0,
            0);

        var action = Assert.Single(OnlineDecisionActionMapper.Map(decision));

        Assert.Equal("requestDeferred", action.GetProperty("decisionType").GetString());
        var payload = action.GetProperty("payload");
        Assert.Equal("r-1", payload.GetProperty("requestId").GetString());
        Assert.Equal("WAIT_FOR_INPUT", payload.GetProperty("reasonCode").GetString());
    }

    private static OnlineState CreateReducedState(int partySize)
    {
        var json = FixtureLoader.ReadUtf8("wp2/valid-bootstrap-event-batch.json");
        json = json.Replace(
            "\"partySize\": 1",
            $"\"partySize\": {partySize}",
            StringComparison.Ordinal);
        var decoded = ProtocolEnvelopeCodec.Decode(Encoding.UTF8.GetBytes(json));
        Assert.True(decoded.IsSuccess, decoded.Error?.Message);
        var mapped = new OnlineEventMapper().Map(decoded.Envelope!);
        Assert.True(mapped.IsSuccess, mapped.Witness?.Message);
        var batch = mapped.Batch!;
        var state = OnlineState.Create(
            RideBoundRun.Create(batch.RunId, batch.ScenarioId, new SimTime(0)),
            new string('a', 64));
        var reduced = new EventReductionCoordinator(state).Propose(batch);
        Assert.True(reduced.IsSuccess, reduced.Witness?.Message);

        return reduced.ProposedState!;
    }
}
