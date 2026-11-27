using RideBound.Application.State;
using RideBound.Application.Travel;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;
using RideBound.Domain.Runs;
using RideBound.Domain.Validation;
using RideBound.Domain.Vehicles;

namespace RideBound.Application.Tests.Validation;

public sealed class TravelSnapshotPhysicalValidationTests
{
    [Fact]
    public void Application_snapshot_validates_a_waiting_rider_without_mutating_state()
    {
        var initial = ApplicationTestData.InitialState();
        var run = WaitingRun();
        var snapshot = ApplicationTestData.Travel();
        var observed = initial with { Run = run, TravelTimes = snapshot };

        var first = Validate(observed.Run, observed.TravelTimes!);
        var replay = Validate(observed.Run, observed.TravelTimes!);

        Assert.True(first.IsFeasible, first.Witness?.Message);
        Assert.Equal(first, replay);
        Assert.Same(run, observed.Run);
        Assert.Same(snapshot, observed.TravelTimes);
        Assert.Equal(1, observed.NextEventSequence);
        Assert.Equal(new string('a', 64), observed.ExpectedInitialTravelTimeSnapshotHash);
        Assert.Equal(RequestLifecycle.WaitingPickup, run.Requests[ApplicationTestData.RequestId].Lifecycle);
        Assert.Equal(0, run.Vehicles[ApplicationTestData.VehicleId].OccupiedSeats);
        Assert.Empty(initial.Run.Requests);
        Assert.Empty(initial.Run.Vehicles);
        Assert.Null(initial.TravelTimes);
    }

    [Fact]
    public void Mutating_the_source_arcs_cannot_change_a_validated_snapshot()
    {
        var run = WaitingRun();
        var arcs = new Dictionary<TravelArc, Duration>
        {
            [new TravelArc(ApplicationTestData.NodeZero, ApplicationTestData.NodeOne)] = new(100),
            [new TravelArc(ApplicationTestData.NodeOne, ApplicationTestData.NodeTwo)] = new(100),
        };
        var snapshot = TravelTimeSnapshot.Create(1, new string('a', 64), arcs).Value!;
        var before = Validate(run, snapshot);

        arcs.Clear();
        arcs.Add(
            new TravelArc(ApplicationTestData.NodeZero, ApplicationTestData.NodeOne),
            new Duration(5000));
        var after = Validate(run, snapshot);

        Assert.True(before.IsFeasible, before.Witness?.Message);
        Assert.Equal(before, after);
        Assert.Equal(2, snapshot.TravelTimes.Count);
        Assert.True(snapshot.TryGetTravelTime(
            ApplicationTestData.NodeZero,
            ApplicationTestData.NodeOne,
            out var duration));
        Assert.Equal(100, duration.Milliseconds);
        Assert.Equal(1, snapshot.Version);
        Assert.Equal(new string('a', 64), snapshot.SnapshotHash);
    }

    [Fact]
    public void New_snapshot_returns_an_exact_pickup_witness_and_keeps_the_old_snapshot_valid()
    {
        var run = WaitingRun();
        var original = ApplicationTestData.Travel();
        var delayed = Snapshot(
            version: 2,
            hash: 'b',
            new KeyValuePair<TravelArc, Duration>(
                new TravelArc(ApplicationTestData.NodeZero, ApplicationTestData.NodeOne),
                new Duration(1100)),
            new KeyValuePair<TravelArc, Duration>(
                new TravelArc(ApplicationTestData.NodeOne, ApplicationTestData.NodeTwo),
                new Duration(100)));

        var before = Validate(run, original);
        var changed = Validate(run, delayed);
        var replay = Validate(run, original);

        Assert.True(before.IsFeasible, before.Witness?.Message);
        Assert.Equal(before, replay);
        Assert.False(changed.IsFeasible);
        var witness = Assert.IsType<PhysicalViolationWitness>(changed.Witness);
        Assert.Equal(PhysicalViolationCodes.PickupWindow, witness.Code);
        Assert.Equal(ApplicationTestData.VehicleId, witness.VehicleId);
        Assert.Equal(ApplicationTestData.RequestId, witness.RequestId);
        Assert.Equal(new StopId("pickup"), witness.StopId);
        Assert.Equal("latestPickupMs", witness.Dimension);
        Assert.Equal(2000, witness.Expected);
        Assert.Equal(2100, witness.Actual);
        Assert.Equal(RequestLifecycle.WaitingPickup, run.Requests[ApplicationTestData.RequestId].Lifecycle);
        Assert.Equal(0, run.Vehicles[ApplicationTestData.VehicleId].Route.Version.Value);
    }

    [Fact]
    public void Missing_forward_arc_is_not_inferred_from_a_reverse_arc()
    {
        var run = WaitingRun();
        var reversed = Snapshot(
            version: 2,
            hash: 'b',
            new KeyValuePair<TravelArc, Duration>(
                new TravelArc(ApplicationTestData.NodeOne, ApplicationTestData.NodeZero),
                new Duration(100)),
            new KeyValuePair<TravelArc, Duration>(
                new TravelArc(ApplicationTestData.NodeOne, ApplicationTestData.NodeTwo),
                new Duration(100)));

        var result = Validate(run, reversed);

        Assert.False(result.IsFeasible);
        var witness = Assert.IsType<PhysicalViolationWitness>(result.Witness);
        Assert.Equal(PhysicalViolationCodes.RouteConnectivity, witness.Code);
        Assert.Equal(ApplicationTestData.VehicleId, witness.VehicleId);
        Assert.Equal(new StopId("pickup"), witness.StopId);
        Assert.Equal("routeConnectivity", witness.Dimension);
        Assert.True(Validate(run, ApplicationTestData.Travel()).IsFeasible);
    }

    [Theory]
    [InlineData('A')]
    [InlineData('g')]
    [InlineData(' ')]
    public void Bootstrap_identity_rejects_noncanonical_pins_without_changing_the_run(char character)
    {
        var run = WaitingRun();

        Assert.Throws<ArgumentException>(
            () => OnlineState.Create(run, new string(character, 64)));

        Assert.Equal(RequestLifecycle.WaitingPickup, run.Requests[ApplicationTestData.RequestId].Lifecycle);
        Assert.Equal(0, run.Vehicles[ApplicationTestData.VehicleId].OccupiedSeats);
        Assert.Equal(2, run.Vehicles[ApplicationTestData.VehicleId].Route.MutableSuffix.Count);
    }

    private static TravelTimeSnapshot Snapshot(
        long version,
        char hash,
        params KeyValuePair<TravelArc, Duration>[] arcs) =>
        TravelTimeSnapshot.Create(version, new string(hash, 64), arcs).Value!;

    private static PhysicalValidationResult Validate(
        RideBoundRun run,
        ITravelTimeLookup snapshot) =>
        new PhysicalPlanValidator().Validate(
            new PhysicalValidationContext(
                run,
                ApplicationTestData.VehicleId,
                run.Vehicles[ApplicationTestData.VehicleId].Route,
                snapshot,
                new SimTime(1000)));

    private static RideBoundRun WaitingRun()
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
            0).Value!;
        var run = ApplicationTestData.InitialState().Run
            .AddRequest(ApplicationTestData.Request()).Value!
            .BootstrapVehicle(vehicle).Value!
            .AcceptRequest(ApplicationTestData.RequestId, ApplicationTestData.VehicleId).Value!
            .ConfirmWaitingPickup(ApplicationTestData.RequestId).Value!
            .AdvanceEpoch(1, new SimTime(1000)).Value!;
        return run;
    }
}
