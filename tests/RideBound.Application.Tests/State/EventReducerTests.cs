using RideBound.Application.Events;
using RideBound.Application.State;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;
using RideBound.Domain.Runs;
using RideBound.Domain.Vehicles;

namespace RideBound.Application.Tests.State;

public sealed class EventReducerTests
{
    [Fact]
    public void Bootstrap_is_proposed_then_committed_only_by_matching_acknowledgement()
    {
        var coordinator = new EventReductionCoordinator(
            ApplicationTestData.InitialState());

        var proposed = coordinator.Propose(ApplicationTestData.BootstrapBatch());

        Assert.True(proposed.IsSuccess, proposed.Witness?.Message);
        Assert.Empty(coordinator.CommittedState.Run.Requests);
        Assert.Empty(coordinator.CommittedState.Run.Vehicles);
        Assert.Equal(0, coordinator.CommittedState.Run.AppliedEpoch);
        Assert.Single(coordinator.PendingState!.Run.Requests);
        Assert.Single(coordinator.PendingState.Run.Vehicles);
        Assert.NotNull(coordinator.PendingState.TravelTimes);

        var wrongAck = coordinator.ApplyDecisionAcknowledgement(2);

        Assert.False(wrongAck.IsSuccess);
        Assert.Equal(
            EventReductionFailureCodes.AcknowledgementMismatch,
            wrongAck.Witness?.Code);
        Assert.Equal(0, coordinator.CommittedState.Run.AppliedEpoch);

        var applied = coordinator.ApplyDecisionAcknowledgement(1);

        Assert.True(applied.IsSuccess);
        Assert.Equal(1, coordinator.CommittedState.Run.AppliedEpoch);
        Assert.Equal(4, coordinator.CommittedState.NextEventSequence);
        Assert.Null(coordinator.PendingState);
    }

    [Fact]
    public void Invalid_last_event_discards_every_prior_fold()
    {
        var initial = ApplicationTestData.InitialState();
        var reducer = new EventReducer();

        var result = reducer.Reduce(
            initial,
            ApplicationTestData.BootstrapBatch(
                appendUnknownVehicleEvent: true));

        Assert.False(result.IsSuccess);
        Assert.Equal("UNKNOWN_VEHICLE", result.Witness?.Code);
        Assert.Equal(3, result.Witness?.EventIndex);
        Assert.Equal(4, result.Witness?.EventSequence);
        Assert.Empty(initial.Run.Requests);
        Assert.Empty(initial.Run.Vehicles);
        Assert.Null(initial.TravelTimes);
        Assert.Equal(1, initial.NextEventSequence);
    }

    [Fact]
    public void Same_state_and_batch_produce_equivalent_proposed_state()
    {
        var reducer = new EventReducer();
        var initial = ApplicationTestData.InitialState();
        var batch = ApplicationTestData.BootstrapBatch();

        var first = reducer.Reduce(initial, batch);
        var second = reducer.Reduce(initial, batch);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(
            first.ProposedState!.Run.AppliedEpoch,
            second.ProposedState!.Run.AppliedEpoch);
        Assert.Equal(
            first.ProposedState.NextEventSequence,
            second.ProposedState.NextEventSequence);
        Assert.Equal(
            first.ProposedState.TravelTimes!.SnapshotHash,
            second.ProposedState.TravelTimes!.SnapshotHash);
        Assert.Equal(
            first.ProposedState.Run.Requests.Keys,
            second.ProposedState.Run.Requests.Keys);
        Assert.True(
            first.ProposedState.Run.Vehicles[ApplicationTestData.VehicleId]
                .Route
                .IsSemanticallyEqual(
                    second.ProposedState.Run.Vehicles[
                        ApplicationTestData.VehicleId].Route));
        Assert.Empty(initial.Run.Requests);
    }

    [Fact]
    public void Second_epoch_reached_stop_advances_route_after_first_ack()
    {
        var coordinator = new EventReductionCoordinator(
            ApplicationTestData.InitialState());
        _ = coordinator.Propose(ApplicationTestData.BootstrapBatch());
        _ = coordinator.ApplyDecisionAcknowledgement(1);
        var time = new SimTime(1100);
        var batch = new InternalEventBatch(
            ApplicationTestData.RunId,
            ApplicationTestData.ScenarioId,
            2,
            time,
            [
                new VehicleReachedStop(
                    4,
                    time,
                    ApplicationTestData.VehicleId,
                    new StopId("waypoint"),
                    new PlanVersion(0),
                    new NodePosition(ApplicationTestData.NodeOne)),
                new TimerTick(5, time),
            ]);

        var result = coordinator.Propose(batch);

        Assert.True(result.IsSuccess, result.Witness?.Message);
        var route = result.ProposedState!.Run.Vehicles[
            ApplicationTestData.VehicleId].Route;
        Assert.Equal(1, route.ExecutedStopCount);
        Assert.Single(route.FrozenPrefix);
        Assert.Empty(route.MutableSuffix);
        Assert.Equal(1, coordinator.CommittedState.Run.AppliedEpoch);
    }

    [Fact]
    public void Gap_or_wrong_time_is_rejected_before_fold()
    {
        var initial = ApplicationTestData.InitialState();
        var time = new SimTime(1000);
        var gap = new InternalEventBatch(
            ApplicationTestData.RunId,
            ApplicationTestData.ScenarioId,
            1,
            time,
            [new TimerTick(2, time)]);
        var wrongTime = new InternalEventBatch(
            ApplicationTestData.RunId,
            ApplicationTestData.ScenarioId,
            1,
            time,
            [new TimerTick(1, new SimTime(1001))]);
        var reducer = new EventReducer();

        Assert.Equal(
            EventReductionFailureCodes.InvalidEventSequence,
            reducer.Reduce(initial, gap).Witness?.Code);
        Assert.Equal(
            EventReductionFailureCodes.InvalidEventTime,
            reducer.Reduce(initial, wrongTime).Witness?.Code);
        Assert.Equal(0, initial.Run.AppliedEpoch);
    }

    [Fact]
    public void Another_batch_cannot_be_proposed_while_ack_is_pending()
    {
        var coordinator = new EventReductionCoordinator(
            ApplicationTestData.InitialState());
        _ = coordinator.Propose(ApplicationTestData.BootstrapBatch());

        var second = coordinator.Propose(ApplicationTestData.BootstrapBatch());

        Assert.False(second.IsSuccess);
        Assert.Equal(
            EventReductionFailureCodes.PendingTransitionExists,
            second.Witness?.Code);
        Assert.Equal(0, coordinator.CommittedState.Run.AppliedEpoch);
    }

    [Fact]
    public void Initial_travel_snapshot_must_match_manifest_identity()
    {
        var initial = OnlineState.Create(
            RideBound.Domain.Runs.RideBoundRun.Create(
                ApplicationTestData.RunId,
                ApplicationTestData.ScenarioId,
                new SimTime(0)),
            new string('f', 64));

        var result = new EventReducer().Reduce(
            initial,
            ApplicationTestData.BootstrapBatch());

        Assert.False(result.IsSuccess);
        Assert.Equal(
            EventReductionFailureCodes.TravelSnapshotIdentityMismatch,
            result.Witness?.Code);
        Assert.Equal(0, result.Witness?.EventIndex);
        Assert.Equal(1, result.Witness?.EventSequence);
        Assert.Empty(initial.Run.Vehicles);
        Assert.Null(initial.TravelTimes);
    }

    [Fact]
    public void Pending_offer_decline_and_cancellation_fold_in_exact_order()
    {
        var secondId = new RequestId("r-2");
        var second = RideRequest.CreatePending(
            secondId,
            new SimTime(1000),
            ApplicationTestData.NodeOne,
            ApplicationTestData.NodeTwo,
            new SimTime(1000),
            new SimTime(2000),
            new Duration(1000),
            1,
            "standard",
            "uniform-v1").Value!;
        var run = ApplicationTestData.InitialState().Run
            .AddRequest(ApplicationTestData.Request()).Value!
            .AddRequest(second).Value!
            .BootstrapVehicle(ApplicationTestData.Vehicle()).Value!
            .AdvanceEpoch(1, new SimTime(1000)).Value!;
        var state = Committed(run);
        var time = new SimTime(1100);
        var batch = new InternalEventBatch(
            ApplicationTestData.RunId,
            ApplicationTestData.ScenarioId,
            2,
            time,
            [
                new OfferDeclined(4, time, ApplicationTestData.RequestId),
                new RequestCancelledBeforeAcceptance(5, time, secondId),
            ]);

        var result = new EventReducer().Reduce(state, batch);

        Assert.True(result.IsSuccess, result.Witness?.Message);
        Assert.Equal(
            RequestLifecycle.Rejected,
            result.ProposedState!.Run.Requests[
                ApplicationTestData.RequestId].Lifecycle);
        Assert.Equal(
            RequestLifecycle.CancelledBeforeAcceptance,
            result.ProposedState.Run.Requests[secondId].Lifecycle);
        Assert.Equal(6, result.ProposedState.NextEventSequence);
        Assert.Equal(
            RequestLifecycle.Pending,
            state.Run.Requests[ApplicationTestData.RequestId].Lifecycle);
    }

    [Fact]
    public void Booking_boarding_and_alighting_update_request_and_vehicle_together()
    {
        var route = RoutePlan.Create(
            new PlanVersion(0),
            0,
            [],
            [
                new RouteStop(
                    new StopId("pickup"),
                    ApplicationTestData.NodeOne,
                    RouteStopKind.Pickup,
                    ApplicationTestData.RequestId,
                    new Duration(0)),
                new RouteStop(
                    new StopId("drop"),
                    ApplicationTestData.NodeTwo,
                    RouteStopKind.DropOff,
                    ApplicationTestData.RequestId,
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
        var run = ApplicationTestData.InitialState().Run
            .AddRequest(ApplicationTestData.Request()).Value!
            .BootstrapVehicle(vehicle).Value!
            .AcceptRequest(
                ApplicationTestData.RequestId,
                ApplicationTestData.VehicleId).Value!
            .AdvanceEpoch(1, new SimTime(1000)).Value!;
        var state = Committed(run);
        var time = new SimTime(1100);
        var batch = new InternalEventBatch(
            ApplicationTestData.RunId,
            ApplicationTestData.ScenarioId,
            2,
            time,
            [
                new BookingConfirmed(
                    4,
                    time,
                    ApplicationTestData.RequestId),
                new PassengerBoarded(
                    5,
                    time,
                    ApplicationTestData.VehicleId,
                    ApplicationTestData.RequestId,
                    new PlanVersion(0)),
                new PassengerAlighted(
                    6,
                    time,
                    ApplicationTestData.VehicleId,
                    ApplicationTestData.RequestId,
                    new PlanVersion(0)),
            ]);

        var result = new EventReducer().Reduce(state, batch);

        Assert.True(result.IsSuccess, result.Witness?.Message);
        Assert.Equal(
            RequestLifecycle.Completed,
            result.ProposedState!.Run.Requests[
                ApplicationTestData.RequestId].Lifecycle);
        var proposedVehicle = result.ProposedState.Run.Vehicles[
            ApplicationTestData.VehicleId];
        Assert.Equal(0, proposedVehicle.OccupiedSeats);
        Assert.Empty(proposedVehicle.OnboardRequestIds);
        Assert.Empty(proposedVehicle.AcceptedRequestIds);
        Assert.Equal(
            RequestLifecycle.Accepted,
            state.Run.Requests[ApplicationTestData.RequestId].Lifecycle);
    }

    [Fact]
    public void Accepted_cancellation_removes_only_mutable_stops_atomically()
    {
        var route = RoutePlan.Create(
            new PlanVersion(0),
            0,
            [],
            [
                new RouteStop(
                    new StopId("pickup"),
                    ApplicationTestData.NodeOne,
                    RouteStopKind.Pickup,
                    ApplicationTestData.RequestId,
                    new Duration(0)),
                new RouteStop(
                    new StopId("drop"),
                    ApplicationTestData.NodeTwo,
                    RouteStopKind.DropOff,
                    ApplicationTestData.RequestId,
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
        var run = ApplicationTestData.InitialState().Run
            .AddRequest(ApplicationTestData.Request()).Value!
            .BootstrapVehicle(vehicle).Value!
            .AcceptRequest(
                ApplicationTestData.RequestId,
                ApplicationTestData.VehicleId).Value!
            .AdvanceEpoch(1, new SimTime(1000)).Value!;
        var state = Committed(run);
        var time = new SimTime(1100);
        var batch = new InternalEventBatch(
            ApplicationTestData.RunId,
            ApplicationTestData.ScenarioId,
            2,
            time,
            [
                new RequestCancelledAfterAcceptance(
                    4,
                    time,
                    ApplicationTestData.RequestId),
            ]);

        var result = new EventReducer().Reduce(state, batch);

        Assert.True(result.IsSuccess, result.Witness?.Message);
        Assert.Equal(
            RequestLifecycle.CancelledAfterAcceptance,
            result.ProposedState!.Run.Requests[
                ApplicationTestData.RequestId].Lifecycle);
        Assert.Empty(
            result.ProposedState.Run.Vehicles[
                ApplicationTestData.VehicleId].Route.MutableSuffix);
        Assert.Equal(
            0,
            state.Run.Vehicles[
                ApplicationTestData.VehicleId].Route.Version.Value);
        Assert.Equal(
            1,
            result.ProposedState.Run.Vehicles[
                ApplicationTestData.VehicleId].Route.Version.Value);
    }

    [Fact]
    public void Travel_snapshot_version_gap_discards_batch()
    {
        var run = ApplicationTestData.InitialState().Run
            .AddRequest(ApplicationTestData.Request()).Value!
            .BootstrapVehicle(ApplicationTestData.Vehicle()).Value!
            .AdvanceEpoch(1, new SimTime(1000)).Value!;
        var state = Committed(run);
        var time = new SimTime(1100);
        var batch = new InternalEventBatch(
            ApplicationTestData.RunId,
            ApplicationTestData.ScenarioId,
            2,
            time,
            [
                new TravelTimesUpdated(
                    4,
                    time,
                    ApplicationTestData.Travel(3, 'c')),
            ]);

        var result = new EventReducer().Reduce(state, batch);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            RideBound.Application.Travel.TravelFailureCodes.StaleSnapshot,
            result.Witness?.Code);
        Assert.Equal(1, state.TravelTimes?.Version);
        Assert.Equal(1, state.Run.AppliedEpoch);
    }

    private static OnlineState Committed(RideBoundRun run) =>
        new(
            run,
            ApplicationTestData.Travel(),
            4,
            new string('a', 64),
            RideBound.Domain.Commitments.CommitmentLedger.Empty);
}
