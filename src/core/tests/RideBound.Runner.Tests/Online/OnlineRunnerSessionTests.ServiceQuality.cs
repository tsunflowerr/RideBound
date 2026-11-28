using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;
using RideBound.Runner.Configuration;
using RideBound.Runner.Protocol;

namespace RideBound.Runner.Tests.Online;

public sealed partial class OnlineRunnerSessionTests
{
    [Fact]
    public void Traffic_breach_is_hashed_once_after_ack_and_retained_after_recovery()
    {
        var configuration = ReadServiceQualityConfiguration();
        var session = OpenC1(configuration);
        _ = BootstrapForServiceQuality(session);
        var initialPromise = ConfirmServiceQualityBooking(session);
        var before = ReadCheckpoint(session);
        Assert.False(before.Content.OnlineState.TryGetProperty("incidentLedger", out _));

        var shock = TrafficSnapshotBatch(3, 5, 1_200, 2, 1_200, toDrop: 1_100);
        var reduced = ReadDecision(session, shock);
        Assert.Equal(before.Content.StateHash, reduced.Decision.StateBeforeHash);
        Assert.NotEqual(before.Content.StateHash, reduced.Decision.StateAfterHash);
        Assert.Equal(SolverStatus.Completed, reduced.Decision.Solver.Status);
        Assert.True(reduced.Decision.Certificate.Body!.NormalOperation);
        Assert.Equal(reduced.Decision.StateAfterHash, reduced.Decision.Certificate.Body.ProposedStateHash);
        Assert.DoesNotContain(reduced.Decision.Actions, action => IsAction(action, "vehiclePlanUpdated"));
        Assert.Equal(reduced.Frame, ReadDecision(session, shock).Frame);
        var blocked = session.Process(CreateCheckpointRequest()).Response;
        Assert.Equal("INVALID_SESSION_STATE", blocked?.Payload.GetProperty("code").GetString());

        AcknowledgeServiceQualityDecision(session, reduced.Decision, 3, 1_200);
        var checkpoint = ReadCheckpoint(session);
        Assert.Equal(reduced.Decision.StateAfterHash, checkpoint.Content.StateHash);
        Assert.Equal(reduced.Decision.DecisionHash, checkpoint.Content.PreviousDecisionHash);
        var incidents = checkpoint.Content.OnlineState.GetProperty("incidentLedger");
        Assert.Empty(incidents.GetProperty("incidents").EnumerateArray());
        var breach = Assert.Single(incidents.GetProperty("breaches").EnumerateArray());
        Assert.Equal("exogenousServiceQuality", breach.GetProperty("kind").GetString());
        Assert.Equal("r-1", breach.GetProperty("requestId").GetString());
        Assert.Equal(5, breach.GetProperty("sourceEventSeq").GetInt64());
        Assert.Equal(3, breach.GetProperty("recordedEpoch").GetInt64());
        Assert.False(breach.GetProperty("normalOperation").GetBoolean());
        Assert.Equal(initialPromise, breach.GetProperty("previousPromise").GetProperty("projection").GetRawText());

        var measurements = breach.GetProperty("serviceQualityWitnesses").EnumerateArray().ToArray();
        Assert.Collection(measurements,
            ride => AssertMeasurement(ride, "MAX_RIDE_TIME", "maxRideTimeMs", 1_000, 1_100),
            pickup => AssertMeasurement(pickup, "PICKUP_WINDOW", "latestPickupMs", 2_000, 2_400));
        Assert.Equal(
            breach.GetProperty("exogenousProjection").GetRawText(),
            breach.GetProperty("safetyProjection").GetRawText());
        var deltas = breach.GetProperty("deltas");
        Assert.Equal(deltas.GetProperty("exogenous").GetRawText(), deltas.GetProperty("visible").GetRawText());
        Assert.All(deltas.GetProperty("decisionInduced").EnumerateObject(),
            value => Assert.Equal(0, value.Value.GetInt64()));
        Assert.Equal(breach.GetProperty("budgetBefore").GetRawText(),
            breach.GetProperty("attemptedBudgetAfter").GetRawText());

        var recovery = TrafficSnapshotBatch(4, 6, 1_300, 3, 100);
        var recovered = ReadDecision(session, recovery).Decision;
        AcknowledgeServiceQualityDecision(session, recovered, 4, 1_300);
        var recoveredLedger = ReadCheckpoint(session).Content.OnlineState.GetProperty("incidentLedger");
        Assert.Equal(breach.GetProperty("breachId").GetString(),
            Assert.Single(recoveredLedger.GetProperty("breaches").EnumerateArray()).GetProperty("breachId").GetString());
    }

    [Fact]
    public void Provisional_booking_under_the_same_traffic_shock_has_no_commitment_breach()
    {
        var session = OpenC1(ReadServiceQualityConfiguration());
        _ = BootstrapForServiceQuality(session);
        var shock = TrafficSnapshotBatch(2, 4, 1_200, 2, 1_200);
        var decision = ReadDecision(session, shock).Decision;
        Assert.Equal(SolverStatus.Completed, decision.Solver.Status);
        Assert.DoesNotContain(decision.Actions, action => IsAction(action, "promisePublished"));
        AcknowledgeServiceQualityDecision(session, decision, 2, 1_200);
        var state = ReadCheckpoint(session).Content.OnlineState;
        Assert.False(state.TryGetProperty("commitmentLedger", out _));
        Assert.False(state.TryGetProperty("incidentLedger", out _));
    }

    [Theory]
    [InlineData("record-v1")]
    [InlineData("reject")]
    public void Late_boarding_mode_controls_the_checkpoint_state(string mode)
    {
        var configuration = ReadServiceQualityConfiguration(mode);
        var live = OpenC1(configuration);
        var route = BootstrapForServiceQuality(live);
        _ = ConfirmServiceQualityBooking(live);
        var checkpoint = ReadCheckpoint(live);
        var boarding = CreateLateBoardingBatch(route);
        if (mode == "reject")
        {
            var error = live.Process(boarding).Response;
            Assert.Equal("error", error?.MessageType.Value);
            Assert.Equal("SCHEMA_VALIDATION_FAILED", error?.Payload.GetProperty("code").GetString());
            Assert.Equal(checkpoint.CheckpointHash, ReadCheckpoint(live).CheckpointHash);
            return;
        }

        var decision = ReadDecision(live, boarding).Decision;
        AcknowledgeServiceQualityDecision(live, decision, 3, 2_500);
        var final = ReadCheckpoint(live);
        var state = final.Content.OnlineState;
        var rider = Assert.Single(state.GetProperty("requests").EnumerateArray());
        Assert.Equal("onboard", rider.GetProperty("lifecycle").GetString());
        Assert.Equal(2_000, rider.GetProperty("latestPickupMs").GetInt64());
        Assert.Equal(2_500, rider.GetProperty("actualPickupTimeMs").GetInt64());
        var incidentLedger = state.GetProperty("incidentLedger");
        Assert.Empty(incidentLedger.GetProperty("breaches").EnumerateArray());
        var recorded = Assert.Single(incidentLedger.GetProperty("latePickups").EnumerateArray());
        Assert.Equal("r-1", recorded.GetProperty("requestId").GetString());
        Assert.Equal(2_000, recorded.GetProperty("latestPickupMs").GetInt64());
        Assert.Equal(2_500, recorded.GetProperty("actualPickupMs").GetInt64());
        Assert.Equal(6, recorded.GetProperty("sourceEventSeq").GetInt64());
    }

    [Fact]
    public void Late_boarding_setting_is_bound_to_the_configuration_hash()
    {
        var original = ReadServiceQualityConfiguration();
        var enabled = ReadServiceQualityConfiguration("record-v1");
        Assert.False(original.RecordLatePickups);
        Assert.True(enabled.RecordLatePickups);
        Assert.NotEqual(original.ContentHash, enabled.ContentHash);
        Assert.Throws<InvalidDataException>(() => ReadServiceQualityConfiguration("unknown"));
    }

    private static C1RunnerConfiguration ReadServiceQualityConfiguration(string? lateBoarding = null)
    {
        var bytes = ReadFixture("runner/ridebound-hard-vector.config.json");
        if (lateBoarding is null)
        {
            return C1RunnerConfiguration.Decode(bytes);
        }

        var node = JsonNode.Parse(bytes)!;
        node["lateBoarding"] = lateBoarding;
        return C1RunnerConfiguration.Decode(Bytes(node));
    }

    private static JsonElement BootstrapForServiceQuality(RunnerSession session)
    {
        var batch = JsonNode.Parse(ReadFixture("wp2/valid-bootstrap-event-batch.json"))!;
        batch["payload"]!["events"]![2]!["payload"]!["vehicle"]!["route"]!["mutableSuffix"] = new JsonArray();
        var decision = ReadDecision(session, Bytes(batch)).Decision;
        Assert.Contains(decision.Actions, action => IsAction(action, "requestAccepted"));
        var route = Assert.Single(decision.Actions, action => IsAction(action, "vehiclePlanUpdated"))
            .GetProperty("payload").GetProperty("route");
        AcknowledgeServiceQualityDecision(session, decision, 1, 1_000);
        return route;
    }

    private static string ConfirmServiceQualityBooking(RunnerSession session)
    {
        var batch = JsonNode.Parse(CreateTimerBatch(2, 4, 1_100))!;
        var booking = batch["payload"]!["events"]![0]!;
        booking["eventType"] = "bookingConfirmed";
        booking["payload"] = new JsonObject { ["requestId"] = "r-1" };
        var decision = ReadDecision(session, Bytes(batch)).Decision;
        var promise = Assert.Single(decision.Actions, action => IsAction(action, "promisePublished"))
            .GetProperty("payload").GetProperty("promise");
        AcknowledgeServiceQualityDecision(session, decision, 2, 1_100);
        return CanonicalJsonText(promise);
    }

    private static void AssertMeasurement(
        JsonElement measurement, string code, string dimension, long contractual, long observed)
    {
        Assert.Equal(code, measurement.GetProperty("code").GetString());
        Assert.Equal(dimension, measurement.GetProperty("dimension").GetString());
        Assert.Equal(contractual, measurement.GetProperty("contractualMilliseconds").GetInt64());
        Assert.Equal(observed, measurement.GetProperty("exogenousMilliseconds").GetInt64());
    }

    private static string CanonicalJsonText(JsonElement element) =>
        Encoding.UTF8.GetString(CanonicalJson.Canonicalize(JsonSerializer.SerializeToUtf8Bytes(element)));

    private static byte[] TrafficSnapshotBatch(
        long epoch, long sequence, long time, int version, int toPickup, int toDrop = 100)
    {
        var bootstrap = JsonNode.Parse(ReadFixture("wp2/valid-bootstrap-event-batch.json"))!;
        var traffic = bootstrap["payload"]!["events"]![0]!.DeepClone();
        traffic["eventSeq"] = sequence;
        var snapshot = traffic["payload"]!["snapshot"]!;
        snapshot["version"] = version;
        snapshot["snapshotHash"] = new string(version == 2 ? 'b' : 'c', 64);
        snapshot["arcs"]![0]!["travelTimeMs"] = toPickup;
        snapshot["arcs"]![1]!["travelTimeMs"] = toDrop;
        var batch = JsonNode.Parse(CreateTimerBatch(epoch, sequence, time))!;
        batch["payload"]!["events"]![0] = traffic;
        return Bytes(batch);
    }

    private static byte[] CreateLateBoardingBatch(JsonElement route)
    {
        var pickup = Assert.Single(route.GetProperty("mutableSuffix").EnumerateArray(),
            stop => stop.GetProperty("kind").GetString() == "pickup");
        var plan = route.GetProperty("planVersion").GetInt64();
        var batch = JsonNode.Parse(CreateTimerBatch(3, 5, 2_500))!;
        batch["payload"]!["events"] = new JsonArray(
            new JsonObject
            {
                ["eventType"] = "vehicleReachedStop",
                ["eventSeq"] = 5,
                ["payload"] = new JsonObject
                {
                    ["vehicleId"] = "v-1",
                    ["stopId"] = pickup.GetProperty("stopId").GetString(),
                    ["planVersion"] = plan,
                    ["position"] = new JsonObject { ["kind"] = "node", ["nodeId"] = "n-1" },
                },
            },
            new JsonObject
            {
                ["eventType"] = "passengerBoarded",
                ["eventSeq"] = 6,
                ["payload"] = new JsonObject
                {
                    ["vehicleId"] = "v-1",
                    ["requestId"] = "r-1",
                    ["planVersion"] = plan,
                },
            });
        return Bytes(batch);
    }

    private static void AcknowledgeServiceQualityDecision(
        RunnerSession session, DecisionPayload decision, long epoch, long time) =>
        Assert.Null(session.Process(CreateDecisionApplied(epoch, time, decision.DecisionHash.Value)).Response);
}
