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
    [Theory]
    [InlineData("full-v1")]
    [InlineData("light-v1")]
    public async Task Streamed_checkpoint_matches_the_canonical_envelope_over_ndjson(string auditMode)
    {
        var configuration = ReadAuditConfiguration(auditMode);
        var session = OpenC1(configuration);
        var initial = Bytes(CreateC1Initialize(configuration));
        var hello = ReadFixture("hello/valid-hello.json");
        var bootstrap = CreateCheckpointBootstrap();
        var first = ReadDecision(session, bootstrap).Decision;
        var firstAck = CreateDecisionApplied(1, 1_000, first.DecisionHash.Value);
        Assert.Null(session.Process(firstAck).Response);

        var booking = JsonNode.Parse(CreateTimerBatch(2, 4, 1_100))!;
        var bookingEvent = booking["payload"]!["events"]![0]!;
        bookingEvent["eventType"] = "bookingConfirmed";
        bookingEvent["payload"] = new JsonObject { ["requestId"] = "r-1" };
        var second = ReadDecision(session, Bytes(booking)).Decision;
        var secondAck = CreateDecisionApplied(2, 1_100, second.DecisionHash.Value);
        Assert.Null(session.Process(secondAck).Response);

        var checkpointRequest = CreateCheckpointRequest();
        var result = session.Process(checkpointRequest);
        Assert.IsType<StreamedResponse>(result.Streamed);
        var envelope = Assert.IsType<ProtocolEnvelope>(result.Response);
        var parsed = CheckpointPayloadCodec.Decode(envelope.Payload);
        Assert.True(parsed.IsSuccess, parsed.Error?.Message);
        var checkpoint = parsed.Value!;
        var state = checkpoint.Content.OnlineState;
        Assert.Single(state.GetProperty("commitmentLedger").EnumerateArray());
        Assert.Equal(263, state.GetProperty("travelTimes").GetProperty("arcs").GetArrayLength());

        // The conventional path serializes the complete decoded state in one envelope.
        var hash = CheckpointPayloadCodec.CalculateHash(checkpoint.Content);
        using var conventionalPayload = JsonDocument.Parse(
            CheckpointPayloadCodec.Encode(checkpoint with { CheckpointHash = hash }));
        var conventional = CanonicalJson.Serialize(envelope with { Payload = conventionalPayload.RootElement });

        using var output = await RunCheckpointTransport(configuration,
            hello, initial, bootstrap, firstAck, Bytes(booking), secondAck, checkpointRequest);
        var frames = Encoding.UTF8.GetString(output.ToArray()).Split('\n');
        Assert.Equal(6, frames.Length);
        Assert.Equal(string.Empty, frames[^1]);
        Assert.Equal(conventional, Encoding.UTF8.GetBytes(frames[^2]));
        Assert.Equal(5, output.WritesPerFrame.Count);
        Assert.True(output.WritesPerFrame[^1] > 8);
    }

    private static byte[] CreateCheckpointBootstrap()
    {
        var bootstrap = JsonNode.Parse(ReadFixture("wp2/valid-bootstrap-event-batch.json"))!;
        var events = bootstrap["payload"]!["events"]!;
        events[2]!["payload"]!["vehicle"]!["route"]!["mutableSuffix"] = new JsonArray();
        var arcs = events[0]!["payload"]!["snapshot"]!["arcs"]!.AsArray();
        for (var index = 259; index >= 0; index--)
        {
            arcs.Add(new JsonObject
            {
                ["fromNodeId"] = $"extra-{index:D3}",
                ["toNodeId"] = "n-0",
                ["travelTimeMs"] = 250,
            });
        }

        return Bytes(bootstrap);
    }

    private static async Task<CheckpointOutput> RunCheckpointTransport(
        C1RunnerConfiguration configuration, params byte[][] frames)
    {
        using var input = new MemoryStream();
        foreach (var frame in frames)
        {
            input.Write(CanonicalJson.Canonicalize(frame));
            input.WriteByte((byte)'\n');
        }

        input.Position = 0;
        var output = new CheckpointOutput();
        using var diagnostics = new StringWriter();
        var status = await RunnerHost.RunAsync(
            input, output, diagnostics,
            executionMode: RunnerExecutionMode.OnlineRollingCost, c1Configuration: configuration);
        Assert.Equal(0, status);
        Assert.Equal(string.Empty, diagnostics.ToString());
        return output;
    }

    private sealed class CheckpointOutput : MemoryStream
    {
        private int _writes;
        public List<int> WritesPerFrame { get; } = [];

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            _writes++;
            return base.WriteAsync(buffer, cancellationToken);
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            WritesPerFrame.Add(_writes);
            _writes = 0;
            return base.FlushAsync(cancellationToken);
        }
    }
}
