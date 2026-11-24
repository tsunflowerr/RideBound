using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using RideBound.Algorithms.Candidates;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;
using RideBound.Contracts.Tests.Fixtures;
using RideBound.Runner.Configuration;
using RideBound.Runner.Protocol;

namespace RideBound.Runner.Tests.Online;

public sealed class OnlineRunnerSessionTests
{
    [Fact]
    public void Bootstrap_batch_produces_typed_B1_actions_and_waits_for_ack()
    {
        var session = CreateInitializedOnlineSession();
        var batch = ReadFixture("wp2/valid-bootstrap-event-batch.json");

        var result = session.Process(batch);

        Assert.Equal("decision", result.Response?.MessageType.Value);
        var decision = DecisionPayloadCodec.Decode(result.Response!.Payload);
        Assert.True(decision.IsSuccess, decision.Error?.Message);
        Assert.Equal(DecisionProductionStatus.Produced, decision.Value!.Status);
        Assert.Equal(DecisionReasonCodes.Accepted, decision.Value.ReasonCode);
        Assert.Equal(CertificateStatus.NotProduced, decision.Value.Certificate.Status);
        Assert.Equal(SolverStatus.NotRun, decision.Value.Solver.Status);
        Assert.Contains(
            decision.Value.Actions,
            value => value.GetProperty("decisionType").GetString()
                == "requestAccepted");
        Assert.Contains(
            decision.Value.Actions,
            value => value.GetProperty("decisionType").GetString()
                == "vehiclePlanUpdated");
        Assert.Equal(
            RunnerSessionStatus.AwaitingDecisionApplied,
            session.Status);
        Assert.Equal(0, session.AppliedEpoch);
        Assert.Equal(1, session.NextEventSequence);
    }

    [Fact]
    public void Exact_retry_is_byte_equivalent_and_next_epoch_requires_ack()
    {
        var session = CreateInitializedOnlineSession();
        var batch = ReadFixture("wp2/valid-bootstrap-event-batch.json");
        var first = session.Process(batch);

        var retry = session.Process(batch);
        var premature = session.Process(CreateTimerBatch(2, 4, 1_100));

        Assert.Equal(
            CanonicalJson.Serialize(first.Response!),
            CanonicalJson.Serialize(retry.Response!));
        Assert.Equal("error", premature.Response?.MessageType.Value);
        Assert.Equal(
            "INVALID_SESSION_STATE",
            premature.Response?.Payload.GetProperty("code").GetString());
        Assert.Equal(0, session.AppliedEpoch);
        Assert.Equal(ProtocolHash.ZeroHash, session.PreviousDecisionHash);
    }

    [Fact]
    public void Two_epoch_online_chain_commits_only_matching_decision_hash()
    {
        var session = CreateInitializedOnlineSession();
        var first = session.Process(
            ReadFixture("wp2/valid-bootstrap-event-batch.json"));
        var firstDecision = DecisionPayloadCodec.Decode(
            first.Response!.Payload).Value!;
        var wrongAck = session.Process(
            CreateDecisionApplied(
                epoch: 1,
                simTime: 1_000,
                new string('f', 64)));

        Assert.Equal("error", wrongAck.Response?.MessageType.Value);
        Assert.Equal(
            "HASH_MISMATCH",
            wrongAck.Response?.Payload.GetProperty("code").GetString());

        session = CreateInitializedOnlineSession();
        first = session.Process(
            ReadFixture("wp2/valid-bootstrap-event-batch.json"));
        firstDecision = DecisionPayloadCodec.Decode(
            first.Response!.Payload).Value!;
        var acknowledged = session.Process(
            CreateDecisionApplied(
                1,
                1_000,
                firstDecision.DecisionHash.Value));

        Assert.Null(acknowledged.Response);
        Assert.Equal(1, session.AppliedEpoch);
        Assert.Equal(4, session.NextEventSequence);
        Assert.Equal(firstDecision.DecisionHash, session.PreviousDecisionHash);

        var second = session.Process(CreateTimerBatch(2, 4, 1_100));
        var secondDecision = DecisionPayloadCodec.Decode(
            second.Response!.Payload);

        Assert.True(secondDecision.IsSuccess, secondDecision.Error?.Message);
        Assert.Equal(
            DecisionProductionStatus.Produced,
            secondDecision.Value!.Status);
        Assert.Equal(
            firstDecision.DecisionHash,
            secondDecision.Value.PreviousDecisionHash);
        Assert.NotEqual(
            firstDecision.DecisionHash,
            secondDecision.Value.DecisionHash);
        Assert.Equal(1, session.AppliedEpoch);
    }

    [Fact]
    public void Request_tamper_changes_online_decision_and_state_hash()
    {
        var originalSession = CreateInitializedOnlineSession();
        var tamperedSession = CreateInitializedOnlineSession();
        var originalBytes =
            ReadFixture("wp2/valid-bootstrap-event-batch.json");
        var tampered = JsonNode.Parse(originalBytes)!;
        tampered["payload"]!["events"]![1]!["payload"]!["request"]![
            "latestPickupMs"] = 1_050;

        var original = DecisionPayloadCodec.Decode(
            originalSession.Process(originalBytes).Response!.Payload).Value!;
        var changed = DecisionPayloadCodec.Decode(
            tamperedSession.Process(
                Encoding.UTF8.GetBytes(tampered.ToJsonString()))
                .Response!.Payload).Value!;

        Assert.NotEqual(original.DecisionHash, changed.DecisionHash);
        Assert.NotEqual(original.StateAfterHash, changed.StateAfterHash);
    }

    [Fact]
    public void Bounded_search_defers_the_request_and_publishes_no_changed_plan()
    {
        var session = CreateInitializedOnlineSession(
            new CandidateGenerationOptions(100, 1, maximumExplorationWorkUnits: 1));
        var result = session.Process(ReadFixture("wp2/valid-bootstrap-event-batch.json"));

        Assert.Equal("decision", result.Response?.MessageType.Value);
        var decision = DecisionPayloadCodec.Decode(result.Response!.Payload);
        Assert.True(decision.IsSuccess, decision.Error?.Message);
        var deferred = Assert.Single(
            decision.Value!.Actions,
            action => action.GetProperty("decisionType").GetString() == "requestDeferred");
        Assert.Equal("CANDIDATE_BOUND", deferred.GetProperty("payload").GetProperty("reasonCode").GetString());
        Assert.DoesNotContain(
            decision.Value.Actions,
            action => action.GetProperty("decisionType").GetString() == "vehiclePlanUpdated");
        Assert.Equal(RunnerSessionStatus.AwaitingDecisionApplied, session.Status);
    }

    [Fact]
    public void Solver_status_is_hashed_and_retry_waits_for_the_matching_ack()
    {
        var configuration = ReadSolverConfiguration();
        var session = CreateInitializedOnlineSession(solverConfiguration: configuration);
        var batch = ReadFixture("wp2/valid-bootstrap-event-batch.json");
        var produced = session.Process(batch);
        var decision = DecisionPayloadCodec.Decode(produced.Response!.Payload).Value!;
        var retry = session.Process(batch);
        var wrongSession = CreateInitializedOnlineSession(solverConfiguration: configuration);
        wrongSession.Process(batch);
        var wrongAck = wrongSession.Process(CreateDecisionApplied(1, 1_000, new string('f', 64)));
        var baseline = CreateInitializedOnlineSession().Process(batch);
        var baselineDecision = DecisionPayloadCodec.Decode(baseline.Response!.Payload).Value!;

        Assert.Equal(SolverStatus.Completed, decision.Solver.Status);
        using var hashProjection = JsonDocument.Parse(DecisionPayloadCodec.Encode(decision, hashProjection: true));
        Assert.Equal("completed", hashProjection.RootElement.GetProperty("solver").GetProperty("status").GetString());
        Assert.Equal(CertificateStatus.NotProduced, decision.Certificate.Status);
        Assert.Equal(baselineDecision.StateAfterHash, decision.StateAfterHash);
        Assert.NotEqual(baselineDecision.DecisionHash, decision.DecisionHash);
        Assert.Equal(CanonicalJson.Serialize(produced.Response), CanonicalJson.Serialize(retry.Response!));
        Assert.Equal("HASH_MISMATCH", wrongAck.Response!.Payload.GetProperty("code").GetString());
        Assert.Equal(RunnerSessionStatus.Failed, wrongSession.Status);
        Assert.Equal(0, wrongSession.AppliedEpoch);
        Assert.Equal(0, session.AppliedEpoch);
        Assert.Null(session.Process(CreateDecisionApplied(1, 1_000, decision.DecisionHash.Value)).Response);
        Assert.Equal(1, session.AppliedEpoch);
        Assert.Equal(decision.DecisionHash, session.PreviousDecisionHash);
    }

    [Theory]
    [InlineData("policyId", "rolling-cost")]
    [InlineData("policyVersion", "2.0.0")]
    [InlineData("policyConfigurationHash", "2222222222222222222222222222222222222222222222222222222222222222")]
    public void Mismatched_solver_manifest_is_rejected_before_any_state_is_staged(
        string field,
        string value)
    {
        var configuration = ReadSolverConfiguration();
        var session = CreateNegotiatedSession(solverConfiguration: configuration);
        var initialize = CreateInitialize(configuration);
        initialize["payload"]!["manifest"]![field] = value;

        var mismatch = session.Process(Encoding.UTF8.GetBytes(initialize.ToJsonString()));

        Assert.Equal("IDENTITY_MISMATCH", mismatch.Response!.Payload.GetProperty("code").GetString());
        Assert.Equal(RunnerSessionStatus.Negotiated, session.Status);
        var corrected = session.Process(Encoding.UTF8.GetBytes(CreateInitialize(configuration).ToJsonString()));
        Assert.Equal("initialized", corrected.Response?.MessageType.Value);
    }

    [Fact]
    public void Solver_manifest_requires_the_configuration_to_be_loaded()
    {
        var session = CreateNegotiatedSession();
        var input = Encoding.UTF8.GetBytes(CreateInitialize(ReadSolverConfiguration()).ToJsonString());

        var response = session.Process(input);

        Assert.Equal("IDENTITY_MISMATCH", response.Response!.Payload.GetProperty("code").GetString());
        Assert.Equal(RunnerSessionStatus.Negotiated, session.Status);
    }

    [Fact]
    public void Native_solver_objective_overflow_publishes_a_validated_no_op()
    {
        var session = CreateInitializedOnlineSession(solverConfiguration: ReadSolverConfiguration());
        var batch = JsonNode.Parse(ReadFixture("wp2/valid-bootstrap-event-batch.json"))!;
        var events = batch["payload"]!["events"]!.AsArray();
        // Each vehicle can serve this request, but the sum of both per-vehicle
        // objective upper bounds exceeds the canonical integer range.
        events[0]!["payload"]!["snapshot"]!["arcs"]![0]!["travelTimeMs"] = 6_000_000_000_000_000L;
        events[1]!["payload"]!["request"]!["latestPickupMs"] = 8_000_000_000_000_000L;
        var vehicle = events[2]!;
        vehicle["payload"]!["vehicle"]!["route"]!["mutableSuffix"] = new JsonArray();
        var secondVehicle = vehicle.DeepClone();
        secondVehicle["eventSeq"] = 4;
        secondVehicle["payload"]!["vehicle"]!["vehicleId"] = "v-2";
        events.Add(secondVehicle);

        var result = session.Process(Encoding.UTF8.GetBytes(batch.ToJsonString()));
        Assert.Equal("decision", result.Response?.MessageType.Value);
        var decoded = DecisionPayloadCodec.Decode(result.Response!.Payload);

        Assert.True(decoded.IsSuccess, decoded.Error?.Message);
        var decision = decoded.Value!;
        Assert.Equal(SolverStatus.SafeFallback, decision.Solver.Status);
        Assert.Equal(
            "requestDeferred",
            Assert.Single(decision.Actions).GetProperty("decisionType").GetString());
        Assert.Equal(RunnerSessionStatus.AwaitingDecisionApplied, session.Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Executable_config_flag_runs_cp_sat_for_the_bound_manifest(bool c1)
    {
        var file = Path.GetTempFileName();
        try
        {
            var configurationBytes = ReadFixture(c1
                ? "runner/ridebound-hard-vector.config.json"
                : "runner/rolling-cost-cp-sat.config.json");
            await File.WriteAllBytesAsync(file, configurationBytes);
            var startInfo = new ProcessStartInfo("dotnet")
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var argument in new[]
                     {
                         typeof(RunnerHost).Assembly.Location, "--mode", "online",
                         c1 ? "--c1-solver-config" : "--rolling-cost-solver-config", file,
                     })
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo);
            Assert.NotNull(process);
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.StandardInput.WriteLineAsync(
                JsonNode.Parse(ReadFixture("hello/valid-hello.json"))!.ToJsonString());
            var initialize = c1
                ? CreateC1Initialize(C1RunnerConfiguration.Decode(configurationBytes))
                : CreateInitialize(RollingCostSolverConfiguration.Decode(configurationBytes));
            await process.StandardInput.WriteLineAsync(initialize.ToJsonString());
            await process.StandardInput.WriteLineAsync(
                JsonNode.Parse(ReadFixture("wp2/valid-bootstrap-event-batch.json"))!.ToJsonString());
            process.StandardInput.Close();
            await process.WaitForExitAsync();

            Assert.Equal(0, process.ExitCode);
            Assert.Empty(await stderrTask);
            var frames = (await stdoutTask).Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(3, frames.Length);
            var lastFrame = JsonNode.Parse(frames[^1])!;
            Assert.Equal("decision", lastFrame["messageType"]!.GetValue<string>());
            Assert.Equal("completed", lastFrame["payload"]!["solver"]!["status"]!.GetValue<string>());
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void C1_relocates_a_waiting_rider_and_replays_the_acknowledged_ledger()
    {
        static string[] Trace()
        {
            var configuration = C1RunnerConfiguration.Decode(
                ReadFixture("runner/ridebound-hard-vector.config.json"));
            var session = CreateC1Session(configuration);
            Assert.Equal("initialized", session.Process(Bytes(CreateC1Initialize(configuration))).Response?.MessageType.Value);
            var boot = JsonNode.Parse(ReadFixture("wp2/valid-bootstrap-event-batch.json"))!;
            var events = boot["payload"]!["events"]!.AsArray();
            events[2]!["payload"]!["vehicle"]!["route"]!["mutableSuffix"] = new JsonArray();
            var receiver = events[2]!.DeepClone();
            receiver["eventSeq"] = 4;
            receiver["payload"]!["vehicle"]!["vehicleId"] = "v-2";
            receiver["payload"]!["vehicle"]!["position"]!["nodeId"] = "n-3";
            events.Add(receiver);
            var accepted = ReadDecision(session, Bytes(boot));
            Assert.Equal(CertificateStatus.Produced, accepted.Decision.Certificate.Status);
            Assert.Empty(accepted.Decision.Certificate.Body!.PublicationIds);
            Assert.Equal(2, accepted.Decision.Certificate.Body.PhysicalPlanCount);
            var acceptedAction = Assert.Single(accepted.Decision.Actions, x => IsAction(x, "requestAccepted"));
            Assert.Equal("v-1", acceptedAction.GetProperty("payload").GetProperty("vehicleId").GetString());
            Assert.DoesNotContain(accepted.Decision.Actions, x => IsAction(x, "promisePublished"));
            Assert.Null(session.Process(CreateDecisionApplied(1, 1_000, accepted.Decision.DecisionHash.Value)).Response);

            var booking = JsonNode.Parse(CreateTimerBatch(2, 5, 1_100))!;
            var bookingEvent = booking["payload"]!["events"]![0]!;
            bookingEvent["eventType"] = "bookingConfirmed";
            bookingEvent["payload"] = new JsonObject { ["requestId"] = "r-1" };
            var confirmed = ReadDecision(session, Bytes(booking));
            var initial = Assert.Single(confirmed.Decision.Actions, x => IsAction(x, "promisePublished"))
                .GetProperty("payload");
            Assert.Equal(1, initial.GetProperty("promiseVersion").GetInt64());
            Assert.Equal("v-1", initial.GetProperty("promise").GetProperty("vehicleId").GetString());
            Assert.Null(session.Process(CreateDecisionApplied(2, 1_100, confirmed.Decision.DecisionHash.Value)).Response);

            var update = JsonNode.Parse(CreateTimerBatch(3, 6, 1_200))!;
            var travelEvent = events[0]!.DeepClone();
            travelEvent["eventSeq"] = 6;
            var snapshot = travelEvent["payload"]!["snapshot"]!;
            snapshot["version"] = 2;
            snapshot["snapshotHash"] = new string('b', 64);
            snapshot["arcs"]!.AsArray().Add(
                new JsonObject { ["fromNodeId"] = "n-3", ["toNodeId"] = "n-1", ["travelTimeMs"] = 10 });
            update["payload"]!["events"]![0] = travelEvent;
            var moved = ReadDecision(session, Bytes(update));
            Assert.Equal(SolverStatus.Completed, moved.Decision.Solver.Status);
            Assert.Equal(confirmed.Decision.StateAfterHash, moved.Decision.StateBeforeHash);
            var routes = moved.Decision.Actions.Where(x => IsAction(x, "vehiclePlanUpdated"))
                .Select(x => x.GetProperty("payload").GetProperty("vehicleId").GetString());
            Assert.Equal(["v-1", "v-2"], routes);
            var revision = Assert.Single(moved.Decision.Actions, x => IsAction(x, "promisePublished"))
                .GetProperty("payload");
            Assert.Equal("v-2", revision.GetProperty("promise").GetProperty("vehicleId").GetString());
            Assert.Equal(2, revision.GetProperty("promiseVersion").GetInt64());
            Assert.Equal(CertificateStatus.Produced, moved.Decision.Certificate.Status);
            var certificate = moved.Decision.Certificate.Body!;
            Assert.True(certificate.NormalOperation);
            Assert.Equal(moved.Decision.StateBeforeHash, certificate.InputStateHash);
            Assert.Equal(moved.Decision.StateAfterHash, certificate.ProposedStateHash);
            Assert.Equal([revision.GetProperty("publicationId").GetString()!], certificate.PublicationIds);
            Assert.Equal(1, certificate.PromiseCount);
            Assert.Empty(certificate.Witnesses);
            Assert.Equal(1, revision.GetProperty("budgetAfter").GetProperty("vehicleSwitchCount").GetInt64());
            Assert.Equal(moved.Frame, ReadDecision(session, Bytes(update)).Frame);
            var pendingCheckpoint = session.Process(CreateCheckpointRequest());
            Assert.Equal("INVALID_SESSION_STATE", pendingCheckpoint.Response?.Payload.GetProperty("code").GetString());

            var following = CreateTimerBatch(4, 7, 1_200);
            Assert.Equal("error", session.Process(following).Response?.MessageType.Value);
            Assert.Equal(2, session.AppliedEpoch);
            Assert.Null(session.Process(CreateDecisionApplied(3, 1_200, moved.Decision.DecisionHash.Value)).Response);
            var checkpoint = ReadCheckpoint(session);
            Assert.Equal(moved.Decision.StateAfterHash, checkpoint.Content.StateHash);
            Assert.Equal(moved.Decision.DecisionHash, checkpoint.Content.PreviousDecisionHash);

            var restarted = CreateC1Session(configuration);
            Assert.Equal("initialized", restarted.Process(Bytes(CreateC1Initialize(configuration))).Response?.MessageType.Value);
            var restored = restarted.Process(CreateRestoreRequest(checkpoint));
            Assert.Equal("restore", restored.Response?.MessageType.Value);
            Assert.Equal("restored", restored.Response?.Payload.GetProperty("status").GetString());
            Assert.Equal(checkpoint.CheckpointHash.Value, restored.Response?.Payload.GetProperty("checkpointHash").GetString());
            Assert.Equal(3, restarted.AppliedEpoch);
            Assert.Equal(7, restarted.NextEventSequence);
            Assert.Equal(checkpoint.CheckpointHash, ReadCheckpoint(restarted).CheckpointHash);

            var next = ReadDecision(session, following);
            Assert.Equal(next.Frame, ReadDecision(restarted, following).Frame);
            Assert.Equal(moved.Decision.StateAfterHash, next.Decision.StateBeforeHash);
            Assert.DoesNotContain(next.Decision.Actions, x => IsAction(x, "promisePublished"));

            var invalid = JsonNode.Parse(moved.Frame)!["payload"]!;
            var publication = invalid["actions"]!.AsArray()
                .Single(x => x!["decisionType"]!.GetValue<string>() == "promisePublished")!;
            publication["payload"]!.AsObject().Remove("budgetAfter");
            Assert.False(DecisionPayloadCodec.Decode(JsonSerializer.SerializeToElement(invalid)).IsSuccess);
            return [accepted.Frame, confirmed.Frame, moved.Frame, next.Frame];
        }

        Assert.Equal(Trace(), Trace());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void C1_requires_the_bound_manifest_configuration(bool loadConfiguration)
    {
        var configuration = C1RunnerConfiguration.Decode(
            ReadFixture("runner/ridebound-hard-vector.config.json"));
        var session = CreateC1Session(loadConfiguration ? configuration : null);
        var initialize = CreateC1Initialize(configuration);
        if (loadConfiguration)
        {
            initialize["payload"]!["manifest"]!["policyConfigurationHash"] = new string('f', 64);
        }

        var result = session.Process(Bytes(initialize));
        Assert.Equal("IDENTITY_MISMATCH", result.Response?.Payload.GetProperty("code").GetString());
        Assert.Equal(0, session.AppliedEpoch);
    }

    [Fact]
    public void C1_rejects_an_unknown_rider_policy_before_reduction()
    {
        var configuration = C1RunnerConfiguration.Decode(
            ReadFixture("runner/ridebound-hard-vector.config.json"));
        var session = CreateC1Session(configuration);
        Assert.Equal("initialized", session.Process(Bytes(CreateC1Initialize(configuration))).Response?.MessageType.Value);
        var original = ReadFixture("wp2/valid-bootstrap-event-batch.json");
        var invalid = JsonNode.Parse(original)!;
        invalid["payload"]!["events"]![1]!["payload"]!["request"]!["commitmentPolicyId"] = "absent-policy";
        var result = session.Process(Bytes(invalid));
        Assert.Equal("SCHEMA_VALIDATION_FAILED", result.Response?.Payload.GetProperty("code").GetString());
        Assert.Equal(0, session.AppliedEpoch);
        Assert.Equal(SolverStatus.Completed, ReadDecision(session, original).Decision.Solver.Status);
    }

    [Fact]
    public void A_genesis_checkpoint_can_only_be_restored_once()
    {
        var checkpoint = ReadCheckpoint(CreateInitializedOnlineSession());
        Assert.Equal(0, checkpoint.Content.AppliedEpoch);

        var restarted = CreateInitializedOnlineSession();
        var restore = CreateRestoreRequest(checkpoint);
        Assert.Equal("restore", restarted.Process(restore).Response?.MessageType.Value);
        Assert.Equal(checkpoint.CheckpointHash, ReadCheckpoint(restarted).CheckpointHash);
        Assert.Equal("error", restarted.Process(restore).Response?.MessageType.Value);
        Assert.Equal(0, restarted.AppliedEpoch);
    }

    [Theory]
    [InlineData("manifest")]
    [InlineData("epoch")]
    [InlineData("state")]
    public void Rehashed_checkpoint_must_match_the_manifest_and_inner_state(string field)
    {
        var checkpoint = ReadCheckpoint(CreateInitializedOnlineSession());
        Sha256Hex.TryCreate(new string('f', 64), out var other);
        var content = field switch
        {
            "manifest" => checkpoint.Content with { ManifestHash = other! },
            "epoch" => checkpoint.Content with { AppliedEpoch = 1 },
            _ => checkpoint.Content with { StateHash = other! },
        };
        var modified = checkpoint with { Content = content, CheckpointHash = CheckpointPayloadCodec.CalculateHash(content) };

        var restarted = CreateInitializedOnlineSession();
        var rejected = restarted.Process(CreateRestoreRequest(modified));
        Assert.Equal("error", rejected.Response?.MessageType.Value);
        Assert.Equal(field == "manifest" ? "MANIFEST_MUTATION" : "HASH_MISMATCH",
            rejected.Response?.Payload.GetProperty("code").GetString());
        Assert.Equal(0, restarted.AppliedEpoch);
    }

    private static CheckpointPayload ReadCheckpoint(RunnerSession session)
    {
        var response = session.Process(CreateCheckpointRequest()).Response;
        Assert.Equal("checkpoint", response?.MessageType.Value);
        var decoded = CheckpointPayloadCodec.Decode(response!.Payload);
        Assert.True(decoded.IsSuccess, decoded.Error?.Message);
        return decoded.Value!;
    }

    private static byte[] CreateCheckpointRequest() =>
        CheckpointEnvelope("checkpoint", new JsonObject());

    private static byte[] CreateRestoreRequest(CheckpointPayload checkpoint) =>
        CheckpointEnvelope("restore", JsonNode.Parse(CheckpointPayloadCodec.Encode(checkpoint))!);

    private static byte[] CheckpointEnvelope(string type, JsonNode payload) =>
        Bytes(new JsonObject
        {
            ["schemaVersion"] = "1.0.0",
            ["messageType"] = type,
            ["runId"] = "wp2-run-001",
            ["scenarioId"] = "wp2-two-epoch-small",
            ["payload"] = payload,
        });

    private static (DecisionPayload Decision, string Frame) ReadDecision(RunnerSession session, byte[] batch)
    {
        var response = session.Process(batch).Response;
        Assert.True(response?.MessageType.Value == "decision", response?.Payload.GetRawText());
        var decoded = DecisionPayloadCodec.Decode(response!.Payload);
        Assert.True(decoded.IsSuccess, decoded.Error?.Message);
        return (decoded.Value!, Encoding.UTF8.GetString(ProtocolEnvelopeCodec.Encode(response)));
    }

    private static bool IsAction(JsonElement action, string type) =>
        action.GetProperty("decisionType").GetString() == type;

    private static byte[] Bytes(JsonNode node) => Encoding.UTF8.GetBytes(node.ToJsonString());

    private static RunnerSession CreateC1Session(C1RunnerConfiguration? configuration)
    {
        var session = new RunnerSession(
            RunnerDefaults.CapabilityRequirements, RunnerExecutionMode.OnlineRollingCost, c1Configuration: configuration);
        Assert.Equal("helloAck", session.Process(ReadFixture("hello/valid-hello.json")).Response?.MessageType.Value);
        return session;
    }

    private static JsonNode CreateC1Initialize(C1RunnerConfiguration configuration)
    {
        var initialize = CreateInitialize(null);
        var manifest = initialize["payload"]!["manifest"]!;
        manifest["policyId"] = C1RunnerConfiguration.PolicyId;
        manifest["policyVersion"] = configuration.PolicyVersion;
        manifest["policyConfigurationHash"] = configuration.ContentHash.Value;
        return initialize;
    }

    private static RollingCostSolverConfiguration ReadSolverConfiguration() =>
        RollingCostSolverConfiguration.Decode(ReadFixture("runner/rolling-cost-cp-sat.config.json"));

    private static RunnerSession CreateInitializedOnlineSession(
        CandidateGenerationOptions? candidateOptions = null,
        RollingCostSolverConfiguration? solverConfiguration = null)
    {
        var session = CreateNegotiatedSession(candidateOptions, solverConfiguration);
        var initialized = session.Process(
            Encoding.UTF8.GetBytes(CreateInitialize(solverConfiguration).ToJsonString()));
        Assert.Equal("initialized", initialized.Response?.MessageType.Value);
        return session;
    }

    private static RunnerSession CreateNegotiatedSession(
        CandidateGenerationOptions? candidateOptions = null,
        RollingCostSolverConfiguration? solverConfiguration = null)
    {
        var session = new RunnerSession(
            RunnerDefaults.CapabilityRequirements,
            RunnerExecutionMode.OnlineRollingCost,
            candidateOptions: candidateOptions,
            solverConfiguration: solverConfiguration);
        var hello = session.Process(ReadFixture("hello/valid-hello.json"));
        Assert.Equal("helloAck", hello.Response?.MessageType.Value);
        return session;
    }

    private static JsonNode CreateInitialize(RollingCostSolverConfiguration? solverConfiguration)
    {
        var initialize = JsonNode.Parse(
            ReadFixture("initialize/valid-initialize-run.json"))!;
        initialize["runId"] = "wp2-run-001";
        initialize["scenarioId"] = "wp2-two-epoch-small";
        var manifest = initialize["payload"]!["manifest"]!;
        manifest["travelTimeSnapshotHash"] = new string('a', 64);
        if (solverConfiguration is not null)
        {
            manifest["policyId"] = RollingCostSolverConfiguration.PolicyId;
            manifest["policyVersion"] = solverConfiguration.PolicyVersion;
            manifest["policyConfigurationHash"] = solverConfiguration.ContentHash.Value;
        }

        return initialize;
    }

    private static byte[] CreateTimerBatch(
        long epoch,
        long eventSequence,
        long simTime) =>
        Encoding.UTF8.GetBytes(
            $$"""
            {
              "schemaVersion":"1.0.0",
              "messageType":"eventBatch",
              "runId":"wp2-run-001",
              "scenarioId":"wp2-two-epoch-small",
              "epochId":{{epoch}},
              "simTimeMs":{{simTime}},
              "payload":{
                "events":[{
                  "eventSeq":{{eventSequence}},
                  "eventType":"timerTick",
                  "payload":{}
                }]
              }
            }
            """);

    private static byte[] CreateDecisionApplied(
        long epoch,
        long simTime,
        string decisionHash) =>
        Encoding.UTF8.GetBytes(
            $$"""
            {
              "schemaVersion":"1.0.0",
              "messageType":"decisionApplied",
              "runId":"wp2-run-001",
              "scenarioId":"wp2-two-epoch-small",
              "epochId":{{epoch}},
              "simTimeMs":{{simTime}},
              "payload":{"decisionHash":"{{decisionHash}}"}
            }
            """);

    private static byte[] ReadFixture(string path) =>
        Encoding.UTF8.GetBytes(FixtureLoader.ReadUtf8(path));
}
