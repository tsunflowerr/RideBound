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

    [Fact]
    public async Task Executable_config_flag_runs_cp_sat_for_the_bound_manifest()
    {
        var file = Path.GetTempFileName();
        try
        {
            var configurationBytes = ReadFixture("runner/rolling-cost-cp-sat.config.json");
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
                         "--rolling-cost-solver-config", file,
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
            await process.StandardInput.WriteLineAsync(
                CreateInitialize(RollingCostSolverConfiguration.Decode(configurationBytes)).ToJsonString());
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
