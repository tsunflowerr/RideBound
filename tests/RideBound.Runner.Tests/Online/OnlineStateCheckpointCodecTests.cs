using System.Text.Json;
using System.Text.Json.Nodes;
using RideBound.Application.State;
using RideBound.Application.Travel;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Incidents;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;
using RideBound.Domain.Runs;
using RideBound.Domain.Validation;
using RideBound.Domain.Vehicles;
using RideBound.Runner.Online;

namespace RideBound.Runner.Tests.Online;

public sealed class OnlineStateCheckpointCodecTests
{
    [Fact]
    public void Closed_incident_with_prior_breach_round_trips_exactly()
    {
        var requestId = new RequestId("request-1");
        var vehicleId = new VehicleId("vehicle-1");
        var pickupNode = new NodeId("pickup-node");
        var dropNode = new NodeId("drop-node");
        var pickupStop = new StopId("pickup-stop");
        var dropStop = new StopId("drop-stop");
        var request = RideRequest.CreatePending(
            requestId,
            new SimTime(0),
            pickupNode,
            dropNode,
            new SimTime(0),
            new SimTime(1_000),
            new Duration(1_000),
            1,
            "standard",
            "uniform-v1").Value!;
        var route = RoutePlan.Create(
            new PlanVersion(0),
            0,
            [],
            [
                new RouteStop(
                    pickupStop,
                    pickupNode,
                    RouteStopKind.Pickup,
                    requestId,
                    new Duration(0)),
                new RouteStop(
                    dropStop,
                    dropNode,
                    RouteStopKind.DropOff,
                    requestId,
                    new Duration(0)),
            ]).Value!;
        var vehicle = VehicleState.Create(
            vehicleId,
            4,
            0,
            new NodePosition(pickupNode),
            [],
            [],
            route,
            0).Value!;
        var run = RideBoundRun.Create(
            new RunIdentifier("checkpoint-run"),
            new ScenarioIdentifier("checkpoint-scenario"),
            new SimTime(0));
        run = run.AddRequest(request).Value!;
        run = run.BootstrapVehicle(vehicle).Value!;
        run = run.AcceptRequest(requestId, vehicleId).Value!;
        run = run.AdvanceEpoch(1, new SimTime(3)).Value!;
        var projection = new PromiseProjection(
            requestId,
            vehicleId,
            pickupStop,
            pickupNode,
            dropStop,
            dropNode,
            new SimTime(0),
            new SimTime(100),
            [
                new PromiseServiceToken(
                    pickupStop,
                    requestId,
                    RouteStopKind.Pickup),
                new PromiseServiceToken(
                    dropStop,
                    requestId,
                    RouteStopKind.DropOff),
            ]);
        var promises = CommitmentLedger.Empty.OpenInitial(
            "publication-1",
            projection,
            1,
            new SimTime(0),
            "INITIAL_ACCEPTANCE",
            1).Ledger!;
        var incidentId = new IncidentId("incident-1");
        var incidents = OperationalIncidentLedger.Empty.Open(
            incidentId,
            "ROAD_CLOSED",
            [vehicleId],
            [requestId],
            2,
            new SimTime(1)).Ledger!;
        var delta = new CommitmentVector(1, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        incidents = incidents.AppendBreach(
            new CommitmentBreachRecord(
                "breach-1",
                incidentId,
                requestId,
                promises.Histories[requestId].Current.PublishedPromise,
                projection,
                projection,
                new ThreeWayPromiseDelta(
                    CommitmentVector.Zero,
                    delta,
                    delta),
                CommitmentVector.Zero,
                delta,
                ["pickup_eta_total_ms"],
                3,
                1,
                new SimTime(2))).Ledger!;
        incidents = incidents.Resolve(
            incidentId,
            4,
            new SimTime(3)).Ledger!;
        incidents = incidents.AppendBreach(
            CommitmentBreachRecord.CreateExogenousServiceQuality(
                "exogenous-breach-1",
                requestId,
                promises.Histories[requestId].Current.PublishedPromise,
                projection,
                projection,
                new ThreeWayPromiseDelta(
                    CommitmentVector.Zero,
                    CommitmentVector.Zero,
                    CommitmentVector.Zero),
                CommitmentVector.Zero,
                CommitmentVector.Zero,
                [PhysicalViolationCodes.MaxRideTime],
                [
                    new ServiceQualityBreach(
                        requestId,
                        PhysicalViolationCodes.MaxRideTime,
                        "maxRideTimeMs",
                        100,
                        101),
                ],
                4,
                1,
                new SimTime(3))).Ledger!;
        var travel = TravelTimeSnapshot.Create(
            1,
            new string('a', 64),
            [
                new KeyValuePair<TravelArc, Duration>(
                    new TravelArc(pickupNode, dropNode),
                    new Duration(100)),
            ]).Value!;
        var state = new OnlineState(
            run,
            travel,
            5,
            travel.SnapshotHash,
            promises,
            incidents);
        using var document = JsonDocument.Parse(
            OnlineStateCanonicalizer.Canonicalize(state));

        var decoded = OnlineStateCheckpointCodec.Decode(document.RootElement);

        Assert.True(decoded.IsSuccess, decoded.Error);
        var restoredIncident = Assert.Single(
            decoded.State!.Incidents.Incidents).Value;
        Assert.False(restoredIncident.IsOpen);
        Assert.Collection(
            decoded.State.Incidents.Breaches,
            breach => Assert.Equal("breach-1", breach.BreachId),
            breach =>
            {
                Assert.Equal("exogenous-breach-1", breach.BreachId);
                Assert.Equal(
                    CommitmentBreachKind.ExogenousServiceQuality,
                    breach.Kind);
                Assert.Null(breach.IncidentId);
                Assert.Single(breach.ServiceQualityWitnesses);
                Assert.Equal(breach.BudgetBefore, breach.AttemptedBudgetAfter);
            });
        Assert.Equal(
            OnlineStateCanonicalizer.Canonicalize(state),
            OnlineStateCanonicalizer.Canonicalize(decoded.State));

        var tampered = JsonNode.Parse(document.RootElement.GetRawText())!;
        tampered["incidentLedger"]!["breaches"]![0]!["previousPromise"]![
            "projection"]!["pickupEtaMs"] = 1;
        using var tamperedDocument = JsonDocument.Parse(tampered.ToJsonString());
        var rejected = OnlineStateCheckpointCodec.Decode(
            tamperedDocument.RootElement);

        Assert.False(rejected.IsSuccess);
        Assert.Contains("promise/run boundary", rejected.Error);

        var forgedExogenous = JsonNode.Parse(document.RootElement.GetRawText())!;
        forgedExogenous["incidentLedger"]!["breaches"]![1]![
            "attemptedBudgetAfter"]!["pickupEtaTotalMs"] = 1;
        using var forgedDocument = JsonDocument.Parse(
            forgedExogenous.ToJsonString());
        var forgedResult = OnlineStateCheckpointCodec.Decode(
            forgedDocument.RootElement);

        Assert.False(forgedResult.IsSuccess);
        Assert.Contains("unchanged budget", forgedResult.Error);
    }
}
