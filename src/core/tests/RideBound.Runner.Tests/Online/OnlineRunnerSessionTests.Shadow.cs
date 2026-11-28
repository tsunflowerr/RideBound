using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using RideBound.Contracts.Protocol;
using RideBound.Runner.Configuration;
using RideBound.Runner.Online;

namespace RideBound.Runner.Tests.Online;

public sealed partial class OnlineRunnerSessionTests
{
    [Theory]
    [InlineData("+Infinity", "guaranteed")]
    [InlineData("-Infinity", "allowed")]
    public void Shadow_class_is_frozen_at_the_first_promise_and_retained_in_checkpoint(
        string threshold, string expectedClass)
    {
        using var network = new ShadowNetworkFixture();
        var configuration = C1RunnerConfiguration.Decode(Bytes(network.Configuration(threshold)));
        var live = OpenC1(configuration);
        _ = BootstrapForServiceQuality(live);
        Assert.False(ReadCheckpoint(live).Content.OnlineState.TryGetProperty("shadowAllocations", out _));

        _ = ConfirmServiceQualityBooking(live);
        var checkpoint = ReadCheckpoint(live);
        var allocation = ReadShadowAllocation(checkpoint);
        Assert.Equal("r-1", allocation.GetProperty("requestId").GetString());
        Assert.Equal(expectedClass, allocation.GetProperty("class").GetString());
        Assert.NotEmpty(allocation.GetProperty("score").GetString()!);

        var next = CreateTimerBatch(3, 5, 1_200);
        var decision = ReadDecision(live, next).Decision;
        Assert.Equal(checkpoint.Content.StateHash, decision.StateBeforeHash);
        var revision = Assert.Single(decision.Actions, action => IsAction(action, "promisePublished"));
        Assert.Equal(2, revision.GetProperty("payload").GetProperty("promiseVersion").GetInt32());
        AcknowledgeServiceQualityDecision(live, decision, 3, 1_200);
        var after = ReadCheckpoint(live);
        Assert.Equal(decision.StateAfterHash, after.Content.StateHash);
        Assert.Equal(CanonicalJsonText(allocation), CanonicalJsonText(ReadShadowAllocation(after)));
    }

    [Fact]
    public void Shadow_scores_the_remaining_route_when_booking_and_boarding_share_a_batch()
    {
        using var network = new ShadowNetworkFixture();
        var configuration = C1RunnerConfiguration.Decode(Bytes(network.Configuration("+Infinity")));
        var session = OpenC1(configuration);
        var route = BootstrapForServiceQuality(session);
        var pickup = JsonNode.Parse(CreateLateBoardingBatch(route))!["payload"]!["events"]!.AsArray();
        var batch = JsonNode.Parse(CreateTimerBatch(2, 4, 1_500))!;
        batch["payload"]!["events"] = new JsonArray(
            new JsonObject
            {
                ["eventSeq"] = 4,
                ["eventType"] = "bookingConfirmed",
                ["payload"] = new JsonObject { ["requestId"] = "r-1" },
            },
            pickup[0]!.DeepClone(),
            pickup[1]!.DeepClone());
        var decision = ReadDecision(session, Bytes(batch)).Decision;
        Assert.Contains(decision.Actions, action => IsAction(action, "promisePublished"));
        AcknowledgeServiceQualityDecision(session, decision, 2, 1_500);
        var checkpoint = ReadCheckpoint(session);
        Assert.Equal("guaranteed", ReadShadowAllocation(checkpoint).GetProperty("class").GetString());
        var rider = Assert.Single(checkpoint.Content.OnlineState.GetProperty("requests").EnumerateArray());
        Assert.Equal("onboard", rider.GetProperty("lifecycle").GetString());
    }

    [Fact]
    public void Shadow_state_decoder_rejects_missing_forged_or_unconfigured_allocations()
    {
        using var network = new ShadowNetworkFixture();
        var configuration = C1RunnerConfiguration.Decode(Bytes(network.Configuration("+Infinity")));
        var session = OpenC1(configuration);
        _ = BootstrapForServiceQuality(session);
        _ = ConfirmServiceQualityBooking(session);
        var state = ReadCheckpoint(session).Content.OnlineState;
        var initialHash = state.GetProperty("travelTimes").GetProperty("snapshotHash").GetString()!;
        Assert.False(OnlineStateCheckpointCodec.Decode(state, initialHash).IsSuccess);

        var tampered = JsonNode.Parse(state.GetRawText())!.AsObject();
        tampered["shadowAllocations"]![0]!["class"] = "allowed";
        using var forged = JsonDocument.Parse(Bytes(tampered));
        Assert.False(OnlineStateCheckpointCodec.Decode(forged.RootElement, initialHash, double.PositiveInfinity).IsSuccess);

        tampered.Remove("shadowAllocations");
        using var missing = JsonDocument.Parse(Bytes(tampered));
        Assert.False(OnlineStateCheckpointCodec.Decode(missing.RootElement, initialHash, double.PositiveInfinity).IsSuccess);
    }

    [Fact]
    public void Shadow_configuration_requires_a_matching_policy_and_network_hash()
    {
        using var network = new ShadowNetworkFixture();
        var root = network.Configuration("+Infinity");
        var missingNetwork = root.DeepClone().AsObject();
        missingNetwork.Remove("shadowAllocation");
        Assert.Throws<InvalidDataException>(() => C1RunnerConfiguration.Decode(Bytes(missingNetwork)));

        var missingPolicy = root.DeepClone();
        missingPolicy["policies"]![0]!["dropEtaShadowAllocation"] = false;
        Assert.Throws<InvalidDataException>(() => C1RunnerConfiguration.Decode(Bytes(missingPolicy)));

        root["shadowAllocation"]!["distancesSha256"] = new string('0', 64);
        Assert.Contains("SHA-256", Assert.Throws<InvalidDataException>(() =>
            C1RunnerConfiguration.Decode(Bytes(root))).Message);
    }

    private static JsonElement ReadShadowAllocation(CheckpointPayload checkpoint) =>
        Assert.Single(checkpoint.Content.OnlineState.GetProperty("shadowAllocations").EnumerateArray());

    private sealed class ShadowNetworkFixture : IDisposable
    {
        private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("rb-shadow-runner-");

        public void Dispose()
        {
            File.Delete(Path.Combine(_directory.FullName, "D.u16"));
            File.Delete(Path.Combine(_directory.FullName, "network.json"));
            _directory.Delete();
        }

        public JsonObject Configuration(string threshold)
        {
            var directory = _directory.FullName;
            byte[] distances = [0, 0, 1, 0, 2, 0, 2, 0, 0, 0, 1, 0, 1, 0, 2, 0, 0, 0];
            var graph = Encoding.UTF8.GetBytes(
                """{"N":3,"nZones":1,"csrStart":[0,1,2,3],"csrTo":[1,2,0],"csrW":[1,1,1],"zoneIdx":[0,0,0]}""");
            File.WriteAllBytes(Path.Combine(directory, "D.u16"), distances);
            File.WriteAllBytes(Path.Combine(directory, "network.json"), graph);

            var root = JsonNode.Parse(ReadFixture("runner/ridebound-hard-vector.config.json"))!.AsObject();
            var policy = root["policies"]![0]!;
            policy["dropEtaShadowAllocation"] = true;
            policy["dropEtaDeadlineSlackPermille"] = 1_000;
            root["shadowAllocation"] = new JsonObject
            {
                ["networkDirectory"] = directory,
                ["distancesSha256"] = Convert.ToHexStringLower(SHA256.HashData(distances)),
                ["graphSha256"] = Convert.ToHexStringLower(SHA256.HashData(graph)),
                ["kappa"] = "0.1",
                ["factors"] = new JsonArray("1"),
                ["pickupWindowMs"] = 600_000,
                ["strideMs"] = 0,
                ["intervalMs"] = 600_000,
                ["alpha"] = "0.15",
                ["phi"] = "0.5",
                ["sampled"] = 2,
                ["priorOrigin"] = new JsonArray(1),
                ["priorDestination"] = new JsonArray(1),
                ["threshold"] = threshold,
                ["binMs"] = 900_000,
                ["networkNodes"] = new JsonObject { ["n-0"] = 0, ["n-1"] = 1, ["n-2"] = 2 },
            };
            return root;
        }
    }
}
