using System.Text.Json;
using RideBound.Application.Commitments;
using RideBound.Application.Promises;
using RideBound.Application.Scheduling;
using RideBound.Application.State;
using RideBound.Application.Travel;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;
using RideBound.Domain.Runs;
using RideBound.Domain.Validation;
using RideBound.Domain.Vehicles;
using RideBound.Runner.Online;

namespace RideBound.Runner.Tests.Online;

public sealed class WaitingRiderRelocationCheckpointTests
{
    private static readonly RequestId Rider = new("request-1");
    private static readonly VehicleId Donor = new("vehicle-1");
    private static readonly VehicleId Receiver = new("vehicle-2");
    private static readonly NodeId PickupNode = new("pickup-node");
    private static readonly NodeId DropNode = new("drop-node");

    [Fact]
    public void Checkpoint_records_the_receiver_and_switch_budget_after_relocation()
    {
        var moved = MovedState();
        using var document = JsonDocument.Parse(OnlineStateCanonicalizer.Canonicalize(moved));
        var state = document.RootElement;

        var rider = Assert.Single(state.GetProperty("requests").EnumerateArray());
        Assert.Equal(Rider.Value, rider.GetProperty("requestId").GetString());
        Assert.Equal("waitingPickup", rider.GetProperty("lifecycle").GetString());
        Assert.Equal(Receiver.Value, rider.GetProperty("assignedVehicleId").GetString());

        var vehicles = state.GetProperty("vehicles").EnumerateArray()
            .ToDictionary(vehicle => vehicle.GetProperty("vehicleId").GetString()!);
        Assert.Empty(vehicles[Donor.Value].GetProperty("acceptedRequestIds").EnumerateArray());
        Assert.Equal(Rider.Value,
            Assert.Single(vehicles[Receiver.Value].GetProperty("acceptedRequestIds").EnumerateArray()).GetString());

        var history = Assert.Single(state.GetProperty("commitmentLedger").EnumerateArray());
        var revision = Assert.Single(history.GetProperty("entries").EnumerateArray(),
            entry => entry.GetProperty("kind").GetString() == "revision");
        Assert.Equal(Receiver.Value, revision.GetProperty("publishedPromise").GetProperty("projection")
            .GetProperty("vehicleId").GetString());
        Assert.Equal(Donor.Value, revision.GetProperty("exogenousProjection").GetProperty("vehicleId").GetString());
        Assert.Equal(1, revision.GetProperty("budgetAfter").GetProperty("vehicleSwitchCount").GetInt64());
    }

    private static OnlineState MovedState()
    {
        var request = RideRequest.CreatePending(
            Rider, new SimTime(0), PickupNode, DropNode, new SimTime(0), new SimTime(1_000), new Duration(1_000), 1,
            "standard", "uniform-v1").Value!;
        var pickup = new RouteStop(new StopId("pickup-stop"), PickupNode, RouteStopKind.Pickup, Rider, new Duration(0));
        var drop = new RouteStop(new StopId("drop-stop"), DropNode, RouteStopKind.DropOff, Rider, new Duration(0));
        var donorRoute = RoutePlan.Create(new PlanVersion(0), 0, [], [pickup, drop]).Value!;
        var emptyRoute = RoutePlan.Create(new PlanVersion(0), 0, [], []).Value!;
        var run = RideBoundRun.Create(new RunIdentifier("checkpoint-run"), new ScenarioIdentifier("checkpoint-scenario"), new SimTime(0));
        run = run.AddRequest(request).Value!;
        run = run.BootstrapVehicle(VehicleState.Create(Donor, 4, 0, new NodePosition(PickupNode), [], [], donorRoute, 0).Value!).Value!;
        run = run.BootstrapVehicle(VehicleState.Create(Receiver, 4, 0, new NodePosition(PickupNode), [], [], emptyRoute, 0).Value!).Value!;
        run = run.AcceptRequest(Rider, Donor).Value!;
        run = run.ConfirmWaitingPickup(Rider).Value!;
        run = run.AdvanceEpoch(1, new SimTime(0)).Value!;
        var travel = TravelTimeSnapshot.Create(
            1,
            new string('a', 64),
            [new KeyValuePair<TravelArc, Duration>(new TravelArc(PickupNode, DropNode), new Duration(100))]).Value!;
        var schedule = new RouteScheduleProjector().Project(run, run.Vehicles[Donor], donorRoute, travel, new SimTime(0)).Schedule!;
        var initial = new PromiseProjector().Project(run, run.Vehicles[Donor], donorRoute, schedule, Rider).Value!;
        var ledger = CommitmentLedger.Empty.OpenInitial("publication-1", initial, 1, new SimTime(0), "INITIAL_ACCEPTANCE", 1).Ledger!;
        var before = new OnlineState(run, travel, 2, travel.SnapshotHash, ledger);
        var reduced = before with { Run = run.AdvanceEpoch(2, new SimTime(0)).Value!, NextEventSequence = 3 };
        var candidate = reduced.Run
            .UpdateVehicleRoute(Donor, RoutePlan.Create(new PlanVersion(1), 0, [], []).Value!).Value!
            .UpdateVehicleRoute(Receiver, RoutePlan.Create(new PlanVersion(1), 0, [], [pickup, drop]).Value!).Value!
            .ReassignWaitingRequest(Rider, Receiver).Value!;
        var policy = new CommitmentPolicy(
            "uniform-v1",
            CommitmentDimensionVocabulary.Ordered.Select(
                dimension => new CommitmentDimensionLimit(dimension, null, CommitmentPhase.AllActive)),
            new MaterialRevisionRule(1_000, null),
            finalConfirmationLocks: PromiseLock.PickupStop | PromiseLock.PickupEta,
            ratchetLocks: PromiseLock.PickupEta,
            waitingRiderRelocation: true);

        var validated = new CommitmentDecisionValidator().Validate(
            new CommitmentValidationContext(
                before,
                reduced,
                reduced with { Run = candidate },
                new CommitmentPolicyCatalog([policy]),
                NoDistances.Instance,
                "checkpoint-scope",
                2));

        Assert.True(validated.IsValid, string.Join("; ", validated.Witnesses.Select(value => value.Message)));
        return validated.ValidatedState!;
    }

    private sealed class NoDistances : IStopDistanceLookup
    {
        public static NoDistances Instance { get; } = new();

        public bool TryGetDistanceMillimeters(NodeId fromNodeId, NodeId toNodeId, out long distanceMillimeters)
        {
            distanceMillimeters = 0;
            return false;
        }
    }
}
