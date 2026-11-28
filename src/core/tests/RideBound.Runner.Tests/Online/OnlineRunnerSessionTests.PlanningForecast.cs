using System.Text.Json;
using System.Text.Json.Nodes;
using RideBound.Contracts.Protocol;
using RideBound.Runner.Configuration;

namespace RideBound.Runner.Tests.Online;

public sealed partial class OnlineRunnerSessionTests
{
    [Fact]
    public void Planning_forecast_binds_the_exact_profile_to_the_c1_configuration()
    {
        var baseline = ReadServiceQualityConfiguration();
        var configured = ReadPlanningForecastConfiguration(
            """{"profile":["1","1.0638210000000001","0.98"],"binMs":900000}""");

        Assert.Null(baseline.PlanningForecast);
        Assert.NotEqual(baseline.ContentHash, configured.ContentHash);
        var forecast = Assert.IsType<Domain.Validation.TravelTimeForecast>(configured.PlanningForecast);
        Assert.Equal(900_000, forecast.BinMilliseconds);
        Assert.Equal([1.0, 1.0638210000000001, 0.98], forecast.Profile);
        Assert.Throws<NotSupportedException>(() => ((IList<double>)forecast.Profile)[0] = 2);
    }

    [Theory]
    [InlineData("""{"profile":[],"binMs":60000}""")]
    [InlineData("""{"profile":["0"],"binMs":60000}""")]
    [InlineData("""{"profile":["1e309"],"binMs":60000}""")]
    [InlineData("""{"profile":[1],"binMs":60000}""")]
    [InlineData("""{"profile":["1"],"binMs":59999}""")]
    [InlineData("""{"profile":["1"]}""")]
    [InlineData("""{"profile":["1"],"binMs":60000,"clipPickup":true}""")]
    public void Invalid_planning_forecast_configuration_is_rejected(string value) =>
        Assert.Throws<InvalidDataException>(() => ReadPlanningForecastConfiguration(value));

    [Theory]
    [InlineData(false, 60_050, 60_150, 60_175, 60_295)]
    [InlineData(true, 60_100, 60_300, 60_350, 60_590)]
    public void Planning_uses_the_bound_forecast_across_traffic_updates(
        bool enableForecast, long firstPickup, long firstDrop, long updatedPickup, long updatedDrop)
    {
        var configuration = enableForecast
            ? ReadPlanningForecastConfiguration("""{"profile":["1","2"],"binMs":60000}""")
            : ReadServiceQualityConfiguration();
        var live = OpenC1(configuration);
        var bootstrap = CreateForecastBootstrapBatch();
        var offered = ReadDecision(live, bootstrap).Decision;
        Assert.Contains(offered.Actions, action => IsAction(action, "requestAccepted"));
        AcknowledgeServiceQualityDecision(live, offered, 1, 59_500);

        var checkpoint = ReadCheckpoint(live);
        var travel = checkpoint.Content.OnlineState.GetProperty("travelTimes");
        Assert.False(travel.TryGetProperty("planningForecast", out _));
        Assert.Equal(100, travel.GetProperty("arcs")[0].GetProperty("travelTimeMs").GetInt64());

        var booking = JsonNode.Parse(CreateTimerBatch(2, 4, 59_950))!;
        var bookingEvent = booking["payload"]!["events"]![0]!;
        bookingEvent["eventType"] = "bookingConfirmed";
        bookingEvent["payload"] = new JsonObject { ["requestId"] = "r-1" };

        var confirmed = ReadDecision(live, Bytes(booking));
        AssertForecastPromise(confirmed.Decision, firstPickup, firstDrop);
        AcknowledgeServiceQualityDecision(live, confirmed.Decision, 2, 59_950);

        var traffic = TrafficSnapshotBatch(3, 5, 59_975, 2, toPickup: 200, toDrop: 120);
        var updated = ReadDecision(live, traffic);
        AssertForecastPromise(updated.Decision, updatedPickup, updatedDrop);
    }

    private static C1RunnerConfiguration ReadPlanningForecastConfiguration(string value)
    {
        var root = JsonNode.Parse(ReadFixture("runner/ridebound-hard-vector.config.json"))!;
        root["planningForecast"] = JsonNode.Parse(value);
        return C1RunnerConfiguration.Decode(Bytes(root));
    }

    private static byte[] CreateForecastBootstrapBatch()
    {
        var batch = JsonNode.Parse(ReadFixture("wp2/valid-bootstrap-event-batch.json"))!;
        batch["simTimeMs"] = 59_500;
        var events = batch["payload"]!["events"]!;
        var request = events[1]!["payload"]!["request"]!;
        request["arrivalTimeMs"] = 59_500;
        request["earliestPickupMs"] = 59_500;
        request["latestPickupMs"] = 62_000;
        events[2]!["payload"]!["vehicle"]!["route"]!["mutableSuffix"] = new JsonArray();
        return Bytes(batch);
    }

    private static void AssertForecastPromise(DecisionPayload decision, long pickup, long drop)
    {
        var promise = Assert.Single(decision.Actions, action => IsAction(action, "promisePublished"))
            .GetProperty("payload").GetProperty("promise");
        Assert.Equal(pickup, promise.GetProperty("pickupEtaMs").GetInt64());
        Assert.Equal(drop, promise.GetProperty("dropEtaMs").GetInt64());
    }
}
