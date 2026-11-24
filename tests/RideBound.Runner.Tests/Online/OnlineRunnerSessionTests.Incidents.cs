using System.Text.Json;
using System.Text.Json.Nodes;
using RideBound.Contracts.Protocol;
using RideBound.Runner.Configuration;
using RideBound.Runner.Protocol;

namespace RideBound.Runner.Tests.Online;

public sealed partial class OnlineRunnerSessionTests
{
    private const string IncidentId = "road-closure-1";

    [Fact]
    public void Incident_open_and_resolution_survive_acknowledgement_and_restore()
    {
        var configuration = C1RunnerConfiguration.Decode(
            ReadFixture("runner/ridebound-hard-vector.config.json"));
        var session = OpenC1(configuration);
        BootstrapAndAcknowledge(session);
        var beforeIncident = ReadCheckpoint(session);
        Assert.False(beforeIncident.Content.OnlineState.TryGetProperty("incidentLedger", out _));

        var opening = ReadDecision(session, CreateIncidentOpening(IncidentId, "v-1", 2, 4, 1_100));
        Assert.Equal(beforeIncident.Content.StateHash, opening.Decision.StateBeforeHash);
        Assert.NotEqual(beforeIncident.Content.StateHash, opening.Decision.StateAfterHash);
        Assert.Equal(opening.Decision.StateAfterHash, opening.Decision.Certificate.Body!.ProposedStateHash);
        Assert.DoesNotContain(opening.Decision.Actions, value => IsAction(value, "commitmentBreachDeclared"));
        Assert.Equal(1, session.AppliedEpoch);
        var blocked = session.Process(CreateCheckpointRequest()).Response;
        Assert.Equal("INVALID_SESSION_STATE", blocked?.Payload.GetProperty("code").GetString());

        Assert.Null(session.Process(CreateDecisionApplied(2, 1_100, opening.Decision.DecisionHash.Value)).Response);
        var openCheckpoint = ReadCheckpoint(session);
        Assert.Equal(opening.Decision.StateAfterHash, openCheckpoint.Content.StateHash);
        var opened = ReadOnlyIncident(openCheckpoint);
        Assert.Equal(IncidentId, opened.GetProperty("incidentId").GetString());
        Assert.Equal("ROAD_CLOSED", opened.GetProperty("reasonCode").GetString());
        Assert.Equal(4, opened.GetProperty("openedEventSeq").GetInt64());
        Assert.Equal(1_100, opened.GetProperty("openedAtMs").GetInt64());
        Assert.Equal("v-1", Assert.Single(opened.GetProperty("affectedVehicleIds").EnumerateArray()).GetString());
        Assert.Equal("r-1", Assert.Single(opened.GetProperty("affectedRequestIds").EnumerateArray()).GetString());
        Assert.False(opened.TryGetProperty("resolvedEventSeq", out _));

        var restarted = OpenC1(configuration);
        Assert.Equal("restore", restarted.Process(CreateRestoreRequest(openCheckpoint)).Response?.MessageType.Value);
        Assert.Equal(openCheckpoint.CheckpointHash, ReadCheckpoint(restarted).CheckpointHash);
        var resolution = CreateIncidentResolution(IncidentId, 3, 5, 1_200);
        var fromLive = ReadDecision(session, resolution);
        var fromRestored = ReadDecision(restarted, resolution);
        Assert.Equal(fromLive.Frame, fromRestored.Frame);
        Assert.Equal(openCheckpoint.Content.StateHash, fromRestored.Decision.StateBeforeHash);
        Assert.Equal(openCheckpoint.Content.PreviousDecisionHash, fromRestored.Decision.PreviousDecisionHash);

        var ack = CreateDecisionApplied(3, 1_200, fromLive.Decision.DecisionHash.Value);
        Assert.Null(session.Process(ack).Response);
        Assert.Null(restarted.Process(ack).Response);
        var closedCheckpoint = ReadCheckpoint(restarted);
        Assert.Equal(ReadCheckpoint(session).CheckpointHash, closedCheckpoint.CheckpointHash);
        var closed = ReadOnlyIncident(closedCheckpoint);
        Assert.Equal(IncidentId, closed.GetProperty("incidentId").GetString());
        Assert.Equal(4, closed.GetProperty("openedEventSeq").GetInt64());
        Assert.Equal(5, closed.GetProperty("resolvedEventSeq").GetInt64());
        Assert.Equal(1_200, closed.GetProperty("resolvedAtMs").GetInt64());
    }

    [Theory]
    [InlineData("unknown-vehicle")]
    [InlineData("missing-incident")]
    [InlineData("duplicate-incident")]
    public void Invalid_incident_leaves_the_accepted_checkpoint_available_for_retry(string invalidEvent)
    {
        var session = CreateInitializedOnlineSession();
        BootstrapAndAcknowledge(session);
        var opening = ReadDecision(session, CreateIncidentOpening(IncidentId, "v-1", 2, 4, 1_100));
        Assert.Null(session.Process(CreateDecisionApplied(2, 1_100, opening.Decision.DecisionHash.Value)).Response);
        var checkpoint = ReadCheckpoint(session);

        var invalid = invalidEvent switch
        {
            "unknown-vehicle" => CreateIncidentOpening("second-closure", "missing-vehicle", 3, 5, 1_200),
            "missing-incident" => CreateIncidentResolution("missing-incident", 3, 5, 1_200),
            _ => CreateIncidentOpening(IncidentId, "v-1", 3, 5, 1_200),
        };
        var rejected = session.Process(invalid).Response;
        Assert.Equal("error", rejected?.MessageType.Value);
        Assert.Equal("SCHEMA_VALIDATION_FAILED", rejected?.Payload.GetProperty("code").GetString());
        Assert.Equal(RunnerSessionStatus.Initialized, session.Status);
        Assert.Equal(checkpoint.CheckpointHash, ReadCheckpoint(session).CheckpointHash);
        Assert.Equal(2, session.AppliedEpoch);
        Assert.Equal(5, session.NextEventSequence);

        var valid = ReadDecision(session, CreateIncidentResolution(IncidentId, 3, 5, 1_200));
        Assert.Equal(checkpoint.Content.StateHash, valid.Decision.StateBeforeHash);
        Assert.Null(session.Process(CreateDecisionApplied(3, 1_200, valid.Decision.DecisionHash.Value)).Response);
        Assert.Equal(5, ReadOnlyIncident(ReadCheckpoint(session)).GetProperty("resolvedEventSeq").GetInt64());
    }

    [Fact]
    public void An_invalid_resolution_rolls_back_the_opening_from_the_same_batch()
    {
        var session = CreateInitializedOnlineSession();
        BootstrapAndAcknowledge(session);
        var checkpoint = ReadCheckpoint(session);

        var opening = CreateIncidentOpening(IncidentId, "v-1", 2, 4, 1_100);
        var mixed = JsonNode.Parse(opening)!;
        var invalidResolution = JsonNode.Parse(CreateIncidentResolution("missing-incident", 2, 5, 1_100))!;
        mixed["payload"]!["events"]!.AsArray().Add(
            invalidResolution["payload"]!["events"]![0]!.DeepClone());

        var rejected = session.Process(Bytes(mixed)).Response;
        Assert.Equal("error", rejected?.MessageType.Value);
        Assert.Equal("SCHEMA_VALIDATION_FAILED", rejected?.Payload.GetProperty("code").GetString());
        Assert.Equal(checkpoint.CheckpointHash, ReadCheckpoint(session).CheckpointHash);
        Assert.Equal(4, session.NextEventSequence);

        var retried = ReadDecision(session, opening);
        Assert.Null(session.Process(CreateDecisionApplied(2, 1_100, retried.Decision.DecisionHash.Value)).Response);
        Assert.Equal(IncidentId, ReadOnlyIncident(ReadCheckpoint(session)).GetProperty("incidentId").GetString());
    }

    private static RunnerSession OpenC1(C1RunnerConfiguration configuration)
    {
        var session = CreateC1Session(configuration);
        var initialized = session.Process(Bytes(CreateC1Initialize(configuration)));
        Assert.Equal("initialized", initialized.Response?.MessageType.Value);
        return session;
    }

    private static void BootstrapAndAcknowledge(RunnerSession session)
    {
        var bootstrap = ReadDecision(session, ReadFixture("wp2/valid-bootstrap-event-batch.json"));
        Assert.Contains(bootstrap.Decision.Actions, value => IsAction(value, "requestAccepted"));
        Assert.Null(session.Process(CreateDecisionApplied(1, 1_000, bootstrap.Decision.DecisionHash.Value)).Response);
    }

    private static JsonElement ReadOnlyIncident(CheckpointPayload checkpoint)
    {
        var ledger = checkpoint.Content.OnlineState.GetProperty("incidentLedger");
        Assert.Empty(ledger.GetProperty("breaches").EnumerateArray());
        return Assert.Single(ledger.GetProperty("incidents").EnumerateArray());
    }

    private static byte[] CreateIncidentOpening(string id, string vehicle, long epoch, long sequence, long time) =>
        CreateIncidentBatch(epoch, sequence, time, "incidentOpened", new JsonObject
        {
            ["incidentId"] = id,
            ["reasonCode"] = "ROAD_CLOSED",
            ["vehicleIds"] = new JsonArray(JsonValue.Create(vehicle)),
        });

    private static byte[] CreateIncidentResolution(string id, long epoch, long sequence, long time) =>
        CreateIncidentBatch(epoch, sequence, time, "incidentResolved", new JsonObject { ["incidentId"] = id });

    private static byte[] CreateIncidentBatch(long epoch, long sequence, long time, string type, JsonObject payload)
    {
        var batch = JsonNode.Parse(CreateTimerBatch(epoch, sequence, time))!;
        var onlineEvent = batch["payload"]!["events"]![0]!;
        onlineEvent["eventType"] = type;
        onlineEvent["payload"] = payload;
        return Bytes(batch);
    }
}
