using RideBound.Algorithms.Candidates;
using RideBound.Domain.Common;
using RideBound.Domain.Routes;

namespace RideBound.Algorithms.Tests.Candidates;

public sealed class InsertionCandidateGeneratorTests
{
    private readonly InsertionCandidateGenerator _generator = new();

    [Fact]
    public void Empty_route_has_one_insertion_and_one_no_op()
    {
        var request = AlgorithmTestData.PendingRequest();
        var vehicle = AlgorithmTestData.Vehicle();
        var state = AlgorithmTestData.CreateState([request], [vehicle]);

        var result = _generator.Generate(
            state,
            new CandidateGenerationOptions(100, 1, exactSmallMode: true));

        Assert.True(result.IsSuccess, result.Witness?.Message);
        var candidates = Assert.Single(result.VehicleCandidates!).Candidates;
        Assert.Equal(2, candidates.Count);
        Assert.Single(candidates, value => value.IsNoOp);
        var insertion = Assert.Single(candidates, value => !value.IsNoOp);
        Assert.Equal([request.Id], insertion.NewRequestIds);
        Assert.Equal(
            [RouteStopKind.Pickup, RouteStopKind.DropOff],
            insertion.Route.MutableSuffix.Select(value => value.Kind));
    }

    [Fact]
    public void Existing_mutable_stop_has_all_hand_enumerated_pair_positions()
    {
        var request = AlgorithmTestData.PendingRequest();
        var waypoint = new RouteStop(
            new StopId("waypoint"),
            AlgorithmTestData.NodeThree,
            RouteStopKind.Waypoint,
            null,
            new Duration(0));
        var vehicle = AlgorithmTestData.Vehicle(mutableSuffix: [waypoint]);
        var state = AlgorithmTestData.CreateState([request], [vehicle]);

        var result = _generator.Generate(
            state,
            new CandidateGenerationOptions(100, 1, exactSmallMode: true));

        Assert.True(result.IsSuccess, result.Witness?.Message);
        var candidates = Assert.Single(result.VehicleCandidates!).Candidates;
        Assert.Equal(4, candidates.Count);
        Assert.Equal(
            3,
            candidates.Count(value => !value.IsNoOp));
        Assert.Equal(
            candidates.OrderBy(value => value.CandidateId, StringComparer.Ordinal),
            candidates);
    }

    [Fact]
    public void Frozen_prefix_is_never_an_insertion_position_and_input_is_unchanged()
    {
        var request = AlgorithmTestData.PendingRequest();
        var frozen = new RouteStop(
            new StopId("frozen"),
            AlgorithmTestData.NodeZero,
            RouteStopKind.Waypoint,
            null,
            new Duration(0));
        var route = RoutePlan.Create(
            new PlanVersion(4),
            1,
            [frozen],
            []).Value!;
        var vehicle = RideBound.Domain.Vehicles.VehicleState.Create(
            AlgorithmTestData.VehicleOne,
            4,
            0,
            new NodePosition(AlgorithmTestData.NodeZero),
            [],
            [],
            route,
            1).Value!;
        var state = AlgorithmTestData.CreateState([request], [vehicle]);

        var first = _generator.Generate(
            state,
            new CandidateGenerationOptions(100, 1, exactSmallMode: true));
        var second = _generator.Generate(
            state,
            new CandidateGenerationOptions(100, 1, exactSmallMode: true));

        Assert.True(first.IsSuccess, first.Witness?.Message);
        Assert.True(second.IsSuccess, second.Witness?.Message);
        Assert.Same(route, vehicle.Route);
        Assert.Equal([frozen], route.FrozenPrefix);
        Assert.All(
            first.VehicleCandidates!.Single().Candidates,
            candidate => Assert.Equal([frozen], candidate.Route.FrozenPrefix));
        Assert.Equal(
            first.VehicleCandidates!.Single().Candidates.Select(
                value => value.CandidateId),
            second.VehicleCandidates!.Single().Candidates.Select(
                value => value.CandidateId));
    }

    [Fact]
    public void Physical_invalid_candidates_are_pruned_with_witness()
    {
        var request = AlgorithmTestData.PendingRequest(partySize: 5);
        var vehicle = AlgorithmTestData.Vehicle(capacity: 4);
        var state = AlgorithmTestData.CreateState([request], [vehicle]);

        var result = _generator.Generate(
            state,
            new CandidateGenerationOptions(100, 1, exactSmallMode: true));

        Assert.True(result.IsSuccess, result.Witness?.Message);
        var set = Assert.Single(result.VehicleCandidates!);
        Assert.Single(set.Candidates);
        Assert.True(set.Candidates[0].IsNoOp);
        var prune = Assert.Single(set.PrunedCandidates);
        Assert.Equal("CAPACITY", prune.Code);
        Assert.Equal(request.Id, Assert.Single(prune.NewRequestIds));
        Assert.NotNull(prune.PhysicalWitness);
    }

    [Fact]
    public void Exact_mode_fails_instead_of_silently_truncating()
    {
        var request = AlgorithmTestData.PendingRequest();
        var waypoint = new RouteStop(
            new StopId("waypoint"),
            AlgorithmTestData.NodeThree,
            RouteStopKind.Waypoint,
            null,
            new Duration(0));
        var state = AlgorithmTestData.CreateState(
            [request],
            [AlgorithmTestData.Vehicle(mutableSuffix: [waypoint])]);

        var result = _generator.Generate(
            state,
            new CandidateGenerationOptions(2, 1, exactSmallMode: true));

        Assert.False(result.IsSuccess);
        Assert.Equal(
            CandidateGenerationFailureCodes.ExactSmallCandidateCapExceeded,
            result.Witness?.Code);
    }

    [Fact]
    public void Bounded_mode_retains_no_op_under_stable_cap()
    {
        var request = AlgorithmTestData.PendingRequest();
        var waypoint = new RouteStop(
            new StopId("waypoint"),
            AlgorithmTestData.NodeThree,
            RouteStopKind.Waypoint,
            null,
            new Duration(0));
        var state = AlgorithmTestData.CreateState(
            [request],
            [AlgorithmTestData.Vehicle(mutableSuffix: [waypoint])]);
        var options =
            new CandidateGenerationOptions(2, 1, exactSmallMode: false);

        var first = _generator.Generate(state, options);
        var second = _generator.Generate(state, options);

        Assert.True(first.IsSuccess, first.Witness?.Message);
        var set = Assert.Single(first.VehicleCandidates!);
        Assert.True(set.WasTruncated);
        Assert.Equal(2, set.Candidates.Count);
        Assert.Contains(set.Candidates, value => value.IsNoOp);
        Assert.Equal(
            set.Candidates.Select(value => value.CandidateId),
            second.VehicleCandidates!.Single().Candidates.Select(
                value => value.CandidateId));
    }

    [Fact]
    public void Two_requests_enumerate_every_precedence_preserving_empty_route_order()
    {
        var firstRequest = AlgorithmTestData.PendingRequest("request-1");
        var secondRequest = AlgorithmTestData.PendingRequest(
            "request-2",
            AlgorithmTestData.NodeTwo,
            AlgorithmTestData.NodeThree);
        var state = AlgorithmTestData.CreateState(
            [secondRequest, firstRequest],
            [AlgorithmTestData.Vehicle()]);

        var result = _generator.Generate(
            state,
            new CandidateGenerationOptions(100, 2, exactSmallMode: true));

        Assert.True(result.IsSuccess, result.Witness?.Message);
        var candidates = Assert.Single(result.VehicleCandidates!).Candidates;
        Assert.Equal(9, candidates.Count);
        Assert.Equal(
            6,
            candidates.Count(value => value.NewRequestIds.Count == 2));
    }
}
