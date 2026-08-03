using RideBound.Application.Commitments;
using RideBound.Application.Promises;
using RideBound.Application.Scheduling;
using RideBound.Application.State;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;
using RideBound.Domain.Runs;
using RideBound.Domain.Validation;
using RideBound.Domain.Vehicles;

namespace RideBound.Application.Tests.Commitments;

public sealed class CommitmentDecisionValidatorTests
{
    [Fact]
    public void Rebuilds_delta_from_route_and_rejects_candidate_over_hard_budget()
    {
        var fixture = CreateFixture(hardLimit: 0);

        var result = new CommitmentDecisionValidator().Validate(
            fixture.Context);

        Assert.False(result.IsValid);
        var witness = Assert.Single(
            result.Witnesses,
            value => value.Dimension == "drop_eta_total_ms");
        Assert.Equal(CommitmentValidationStage.Budget, witness.Stage);
        Assert.Equal(CommitmentFailureCodes.BudgetExceeded, witness.Code);
        Assert.Equal("drop_eta_total_ms", witness.Dimension);
        Assert.Equal(0, witness.Limit);
        Assert.Equal(10, witness.Delta);
        Assert.Single(
            fixture.Context.ReducedState.Commitments.Histories[
                ApplicationTestData.RequestId].Entries);
    }

    [Fact]
    public void Valid_revision_is_recomputed_and_appended_to_a_new_ledger()
    {
        var fixture = CreateFixture(hardLimit: 10);

        var result = new CommitmentDecisionValidator().Validate(
            fixture.Context);

        Assert.True(result.IsValid, result.Witnesses.FirstOrDefault()?.Message);
        var publication = Assert.Single(result.Publications);
        Assert.Equal(
            10,
            publication.Entry.Deltas.DecisionInduced.DropEtaTotalMs);
        Assert.Equal(2, publication.Entry.PublishedPromise.Version.Value);
        Assert.Equal(10, publication.Entry.BudgetAfter.DropEtaTotalMs);
        Assert.Equal(
            2,
            result.ValidatedState!.Commitments.Histories[
                ApplicationTestData.RequestId].Entries.Count);
        Assert.Single(
            fixture.Context.ReducedState.Commitments.Histories[
                ApplicationTestData.RequestId].Entries);
    }

    [Fact]
    public void Physical_failure_precedes_commitment_budget_checks()
    {
        var fixture = CreateFixture(hardLimit: 0);
        var vehicle = fixture.Context.CandidateState.Run.Vehicles[
            ApplicationTestData.VehicleId];
        var invalidRoute = RoutePlan.Create(
            new PlanVersion(1),
            0,
            [],
            [vehicle.Route.MutableSuffix[1]]).Value!;
        var invalidRun = fixture.Context.ReducedState.Run.UpdateVehicleRoute(
            ApplicationTestData.VehicleId,
            invalidRoute).Value!;
        var invalidContext = fixture.Context with
        {
            CandidateState = fixture.Context.CandidateState with
            {
                Run = invalidRun,
            },
        };

        var result = new CommitmentDecisionValidator().Validate(invalidContext);

        Assert.False(result.IsValid);
        Assert.All(
            result.Witnesses,
            value => Assert.Equal(CommitmentValidationStage.Physical, value.Stage));
        Assert.Equal("ROUTE_CONNECTIVITY", result.Witnesses[0].Code);
    }

    [Fact]
    public void Candidate_cannot_mutate_existing_request_definition()
    {
        var fixture = CreateFixture(hardLimit: 10);
        var candidateRun = fixture.Context.CandidateState.Run;
        var current = candidateRun.Requests[ApplicationTestData.RequestId];
        var mutated = RideRequest.Rehydrate(
            current.Id,
            current.ArrivalTime,
            current.OriginNodeId,
            current.DestinationNodeId,
            current.EarliestPickup,
            current.LatestPickup,
            current.MaxRideTime,
            current.PartySize,
            "silently-changed-service-class",
            current.CommitmentPolicyId,
            current.Lifecycle,
            current.AssignedVehicleId,
            current.ActualPickupTime).Value!;
        var mutatedRun = RideBoundRun.Rehydrate(
            candidateRun.Id,
            candidateRun.ScenarioId,
            candidateRun.AppliedEpoch,
            candidateRun.SimulationTime,
            candidateRun.Requests.Values.Select(
                value => value.Id == mutated.Id ? mutated : value),
            candidateRun.Vehicles.Values).Value!;

        var result = new CommitmentDecisionValidator().Validate(
            fixture.Context with
            {
                CandidateState = fixture.Context.CandidateState with
                {
                    Run = mutatedRun,
                },
            });

        Assert.False(result.IsValid);
        var witness = Assert.Single(result.Witnesses);
        Assert.Equal(CommitmentValidationStage.State, witness.Stage);
        Assert.Equal("COMMITMENT_STATE_BOUNDARY_MISMATCH", witness.Code);
    }

    [Fact]
    public void Candidate_cannot_hide_unaccepted_pending_stops_in_the_route()
    {
        var fixture = CreateFixture(hardLimit: 10);
        var pending = RideRequest.CreatePending(
            new RequestId("r-pending"),
            new SimTime(1_000),
            ApplicationTestData.NodeTwo,
            ApplicationTestData.NodeZero,
            new SimTime(1_000),
            new SimTime(2_000),
            new Duration(1_000),
            1,
            "standard",
            "uniform-v1").Value!;
        var beforeRun = fixture.Context.BeforeEventState.Run.AddRequest(
            pending).Value!;
        var reducedRun = fixture.Context.ReducedState.Run.AddRequest(
            pending).Value!;
        var currentRoute = reducedRun.Vehicles[
            ApplicationTestData.VehicleId].Route;
        var hiddenRoute = currentRoute.ReplaceMutableSuffix(
            currentRoute.MutableSuffix.Concat(
                [
                    new RouteStop(
                        new StopId("hidden-pickup"),
                        pending.OriginNodeId,
                        RouteStopKind.Pickup,
                        pending.Id,
                        new Duration(0)),
                    new RouteStop(
                        new StopId("hidden-drop"),
                        pending.DestinationNodeId,
                        RouteStopKind.DropOff,
                        pending.Id,
                        new Duration(0)),
                ])).Value!;
        var candidateRun = reducedRun.UpdateVehicleRoute(
            ApplicationTestData.VehicleId,
            hiddenRoute).Value!;

        var result = new CommitmentDecisionValidator().Validate(
            fixture.Context with
            {
                BeforeEventState = fixture.Context.BeforeEventState with
                {
                    Run = beforeRun,
                },
                ReducedState = fixture.Context.ReducedState with
                {
                    Run = reducedRun,
                },
                CandidateState = fixture.Context.CandidateState with
                {
                    Run = candidateRun,
                },
            });

        Assert.False(result.IsValid);
        var witness = Assert.Single(result.Witnesses);
        Assert.Equal(CommitmentValidationStage.State, witness.Stage);
        Assert.Equal("COMMITMENT_STATE_BOUNDARY_MISMATCH", witness.Code);
    }

    private static Fixture CreateFixture(long? hardLimit)
    {
        var request = ApplicationTestData.Request();
        var route = RoutePlan.Create(
            new PlanVersion(0),
            0,
            [],
            [
                new RouteStop(
                    new StopId("pickup"),
                    ApplicationTestData.NodeOne,
                    RouteStopKind.Pickup,
                    request.Id,
                    new Duration(0)),
                new RouteStop(
                    new StopId("drop"),
                    ApplicationTestData.NodeTwo,
                    RouteStopKind.DropOff,
                    request.Id,
                    new Duration(0)),
            ]).Value!;
        var vehicle = VehicleState.Create(
            ApplicationTestData.VehicleId,
            4,
            0,
            new NodePosition(ApplicationTestData.NodeZero),
            [],
            [],
            route,
            1).Value!;
        var run = RideBoundRun.Create(
            ApplicationTestData.RunId,
            ApplicationTestData.ScenarioId,
            new SimTime(1_000));
        run = run.AddRequest(request).Value!;
        run = run.BootstrapVehicle(vehicle).Value!;
        run = run.AcceptRequest(request.Id, vehicle.Id).Value!;
        run = run.AdvanceEpoch(1, new SimTime(1_000)).Value!;
        var travel = ApplicationTestData.Travel();
        var schedule = new RouteScheduleProjector().Project(
            run,
            run.Vehicles[vehicle.Id],
            route,
            travel,
            run.SimulationTime).Schedule!;
        var projection = new PromiseProjector().Project(
            run,
            run.Vehicles[vehicle.Id],
            route,
            schedule,
            request.Id).Value!;
        var ledger = CommitmentLedger.Empty.OpenInitial(
            "initial-publication",
            projection,
            1,
            new SimTime(1_000),
            "INITIAL_ACCEPTANCE",
            3).Ledger!;
        var before = new OnlineState(
            run,
            travel,
            4,
            travel.SnapshotHash,
            ledger);
        var reducedRun = run.AdvanceEpoch(2, new SimTime(1_000)).Value!;
        var reduced = before with
        {
            Run = reducedRun,
            NextEventSequence = 5,
        };
        var changedRoute = RoutePlan.Create(
            new PlanVersion(1),
            0,
            [],
            [
                new RouteStop(
                    route.MutableSuffix[0].StopId,
                    route.MutableSuffix[0].NodeId,
                    route.MutableSuffix[0].Kind,
                    route.MutableSuffix[0].RequestId,
                    new Duration(10)),
                route.MutableSuffix[1],
            ]).Value!;
        var changedRun = reducedRun.UpdateVehicleRoute(
            vehicle.Id,
            changedRoute).Value!;
        var candidate = reduced with { Run = changedRun };
        var policy = new CommitmentPolicy(
            request.CommitmentPolicyId,
            CommitmentBudgetBasis.DecisionInduced,
            CommitmentDimensionVocabulary.Ordered.Select(
                dimension => new CommitmentDimensionLimit(
                    dimension,
                    hardLimit,
                    CommitmentPhase.AllActive)),
            new MaterialRevisionRule(1, null));

        return new Fixture(
            new CommitmentValidationContext(
                before,
                reduced,
                candidate,
                new CommitmentPolicyCatalog([policy]),
                EmptyDistances.Instance,
                "test-scope",
                4));
    }

    private sealed record Fixture(CommitmentValidationContext Context);

    private sealed class EmptyDistances : IStopDistanceLookup
    {
        public static EmptyDistances Instance { get; } = new();

        public bool TryGetDistanceMillimeters(
            NodeId fromNodeId,
            NodeId toNodeId,
            out long distanceMillimeters)
        {
            distanceMillimeters = 0;
            return false;
        }
    }
}
