using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using RideBound.Algorithms.Policies;
using RideBound.Application.Optimization;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;
using RideBound.Contracts.Tests.Fixtures;
using RideBound.Runner.Configuration;
using RideBound.Runner.Protocol;

namespace RideBound.Runner.Tests.Online;

public sealed class Wp4RunnerIntegrationTests
{
    [Fact]
    public async Task Wp4_cli_runs_real_child_process_with_bound_configuration()
    {
        var commitment = CommitmentConfiguration();
        var wp4 = PublishedWp4Configuration(commitment);
        var lines = File.ReadAllLines(Path.Combine(
            RepositoryRoot(),
            "benchmarks",
            "scenarios",
            "wp3-commitment-tiny",
            "commitment-demo.input.ndjson"));
        var initialize = JsonNode.Parse(lines[1])!;
        var manifest = initialize["payload"]!["manifest"]!;
        manifest["policyId"] = wp4.PolicyId;
        manifest["policyVersion"] = wp4.PolicyVersion;
        manifest["policyConfigurationHash"] =
            wp4.BindToCommitmentConfiguration(commitment.ContentHash).Value;
        lines[1] = initialize.ToJsonString();
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add(
            Path.Combine(AppContext.BaseDirectory, "RideBound.Runner.dll"));
        startInfo.ArgumentList.Add("--mode");
        startInfo.ArgumentList.Add("commitment");
        startInfo.ArgumentList.Add("--policy-config");
        startInfo.ArgumentList.Add(Path.Combine(
            RepositoryRoot(),
            "benchmarks",
            "configurations",
            "wp3-boundary-test-v1.json"));
        startInfo.ArgumentList.Add("--wp4-config");
        startInfo.ArgumentList.Add(Path.Combine(
            RepositoryRoot(),
            "benchmarks",
            "configurations",
            "wp4-rolling-cost-boundary-v1.json"));

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);
        await process.StandardInput.WriteAsync(
            string.Join('\n', lines.Take(3)) + "\n");
        process.StandardInput.Close();
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.Equal(0, process.ExitCode);
        Assert.Equal(string.Empty, stderr);
        var responses = stdout.Split(
            ['\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, responses.Length);
        using var decision = JsonDocument.Parse(responses[2]);
        Assert.Equal(
            "completed",
            decision.RootElement.GetProperty("payload")
                .GetProperty("solver")
                .GetProperty("status")
                .GetString());
    }

    [Fact]
    public void OrTools_policy_is_manifest_bound_hashed_retried_and_ack_committed()
    {
        var setup = CreateSession(PublishedWp4Configuration());
        var batch = ReadFixture("wp2/valid-bootstrap-event-batch.json");

        var firstResponse = setup.Session.Process(batch).Response!;
        var retryResponse = setup.Session.Process(batch).Response!;
        var first = DecisionPayloadCodec.Decode(firstResponse.Payload);

        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.Equal(SolverStatus.Completed, first.Value!.Solver.Status);
        Assert.Equal(CertificateStatus.Produced, first.Value.Certificate.Status);
        Assert.Contains(
            first.Value.Actions,
            action => action.GetProperty("decisionType").GetString()
                == "requestAccepted");
        Assert.Equal(
            CanonicalJson.Serialize(firstResponse),
            CanonicalJson.Serialize(retryResponse));
        Assert.Equal(0, setup.Session.AppliedEpoch);

        var wrong = setup.Session.Process(
            DecisionApplied(2, 1_000, first.Value.DecisionHash.Value));
        Assert.Equal("error", wrong.Response?.MessageType.Value);
        Assert.Equal(0, setup.Session.AppliedEpoch);

        var applied = setup.Session.Process(
            DecisionApplied(1, 1_000, first.Value.DecisionHash.Value));
        Assert.Null(applied.Response);
        Assert.Equal(1, setup.Session.AppliedEpoch);
        Assert.Single(setup.Session.CommittedOnlineState!.Commitments.Histories);
    }

    [Fact]
    public void Unknown_solver_uses_full_validator_no_op_and_reports_safe_fallback()
    {
        var setup = CreateSession(
            PublishedWp4Configuration(),
            new UnknownSolver());

        var response = setup.Session.Process(
            ReadFixture("wp2/valid-bootstrap-event-batch.json")).Response!;
        var decision = DecisionPayloadCodec.Decode(response.Payload);

        Assert.True(decision.IsSuccess, decision.Error?.Message);
        Assert.Equal(SolverStatus.SafeFallback, decision.Value!.Solver.Status);
        Assert.Equal(CertificateStatus.Produced, decision.Value.Certificate.Status);
        Assert.Contains(
            decision.Value.Actions,
            action => action.GetProperty("decisionType").GetString()
                == "requestDeferred");
        Assert.DoesNotContain(
            decision.Value.Actions,
            action => action.GetProperty("decisionType").GetString()
                == "requestAccepted");
        Assert.Empty(decision.Value.Certificate.Body!.PublicationIds);
    }

    [Fact]
    public void Manifest_policy_identity_mismatch_fails_before_online_state_changes()
    {
        var commitment = CommitmentConfiguration();
        var wp4 = PublishedWp4Configuration(commitment);
        var session = NewSession(commitment, wp4);
        Assert.Equal(
            "helloAck",
            session.Process(ReadFixture("hello/valid-hello.json"))
                .Response?.MessageType.Value);
        var initialize = InitializePayload(wp4, commitment);
        initialize["payload"]!["manifest"]!["policyId"] =
            "ridebound-hard-vector";

        var rejected = session.Process(
            Encoding.UTF8.GetBytes(initialize.ToJsonString()));

        Assert.Equal("error", rejected.Response?.MessageType.Value);
        Assert.Equal(
            "HASH_MISMATCH",
            rejected.Response?.Payload.GetProperty("code").GetString());
        Assert.Equal(RunnerSessionStatus.Failed, session.Status);
        Assert.Null(session.CommittedOnlineState);
    }

    [Fact]
    public void B5_plan_pool_commits_only_on_ack_and_survives_checkpoint_restore()
    {
        var commitment = CommitmentConfiguration();
        var b5 = B5Configuration(commitment);
        var setup = CreateSession(b5, commitmentConfiguration: commitment);
        var decision = DecisionPayloadCodec.Decode(
            setup.Session.Process(
                ReadFixture("wp2/valid-bootstrap-event-batch.json"))
                .Response!.Payload).Value!;

        Assert.Equal(SolverStatus.Completed, decision.Solver.Status);
        Assert.Empty(setup.Session.CommittedOnlineState!.PlanPool.Plans);
        _ = setup.Session.Process(
            DecisionApplied(1, 1_000, decision.DecisionHash.Value));
        Assert.NotEmpty(setup.Session.CommittedOnlineState!.PlanPool.Plans);

        var checkpointResponse = setup.Session.Process(CheckpointRequest()).Response!;
        var restoredSetup = CreateSession(
            b5,
            commitmentConfiguration: commitment);
        var restored = restoredSetup.Session.Process(
            RestoreRequest(checkpointResponse.Payload));

        Assert.Equal("restore", restored.Response?.MessageType.Value);
        Assert.Equal(
            setup.Session.CommittedOnlineState.PlanPool.Version,
            restoredSetup.Session.CommittedOnlineState!.PlanPool.Version);
        Assert.Equal(
            setup.Session.CommittedOnlineState.PlanPool.DistinguishedPlanId,
            restoredSetup.Session.CommittedOnlineState.PlanPool.DistinguishedPlanId);
    }

    private static SessionSetup CreateSession(
        Wp4RunnerConfiguration wp4,
        ICandidateSelectionSolver? solver = null,
        CommitmentPolicyConfiguration? commitmentConfiguration = null)
    {
        var commitment = commitmentConfiguration ?? CommitmentConfiguration();
        var session = NewSession(commitment, wp4, solver);
        Assert.Equal(
            "helloAck",
            session.Process(ReadFixture("hello/valid-hello.json"))
                .Response?.MessageType.Value);
        var initialized = session.Process(
            Encoding.UTF8.GetBytes(
                InitializePayload(wp4, commitment).ToJsonString()));
        Assert.Equal(
            "initialized",
            initialized.Response?.MessageType.Value);
        return new SessionSetup(session, commitment, wp4);
    }

    private static RunnerSession NewSession(
        CommitmentPolicyConfiguration commitment,
        Wp4RunnerConfiguration wp4,
        ICandidateSelectionSolver? solver = null) =>
        new(
            RunnerDefaults.CapabilityRequirements,
            RunnerExecutionMode.OnlineCommitment,
            commitmentPolicies: commitment,
            stopDistances: commitment,
            commitmentPolicyConfigurationHash:
                wp4.BindToCommitmentConfiguration(commitment.ContentHash),
            wp4Configuration: wp4,
            solverBackedPolicy: solver is null
                ? null
                : new SolverBackedRidePoolingPolicy(solver));

    private static JsonNode InitializePayload(
        Wp4RunnerConfiguration wp4,
        CommitmentPolicyConfiguration commitment)
    {
        var initialize = JsonNode.Parse(
            FixtureLoader.ReadUtf8("initialize/valid-initialize-run.json"))!;
        initialize["runId"] = "wp2-run-001";
        initialize["scenarioId"] = "wp2-two-epoch-small";
        var manifest = initialize["payload"]!["manifest"]!;
        manifest["travelTimeSnapshotHash"] = new string('a', 64);
        manifest["policyId"] = wp4.PolicyId;
        manifest["policyVersion"] = wp4.PolicyVersion;
        manifest["policyConfigurationHash"] =
            wp4.BindToCommitmentConfiguration(commitment.ContentHash).Value;
        return initialize;
    }

    private static Wp4RunnerConfiguration PublishedWp4Configuration(
        CommitmentPolicyConfiguration? commitmentConfiguration = null)
    {
        var commitment = commitmentConfiguration ?? CommitmentConfiguration();
        return Wp4RunnerConfiguration.Decode(
            File.ReadAllBytes(Path.Combine(
                RepositoryRoot(),
                "benchmarks",
                "configurations",
                "wp4-rolling-cost-boundary-v1.json")),
            commitment);
    }

    private static Wp4RunnerConfiguration B5Configuration(
        CommitmentPolicyConfiguration commitment) =>
        Wp4RunnerConfiguration.Decode(
            Encoding.UTF8.GetBytes(
                """
                {
                  "configurationVersion":"1.0.0",
                  "policyId":"least-commitment-consensus",
                  "policyVersion":"wp4-b5-test-v1",
                  "candidateGeneration":{
                    "maximumCandidatesPerVehicle":100,
                    "maximumNewRequestsPerVehicle":2,
                    "exactSmallMode":false,
                    "scheduleStrategy":"earliest-feasible",
                    "maximumExplorationWorkUnits":10000
                  },
                  "multiplePlan":{
                    "maximumPlanCount":4,
                    "maximumCombinationWorkUnits":10000,
                    "requireCompleteEnumeration":true
                  }
                }
                """),
            commitment);

    private static CommitmentPolicyConfiguration CommitmentConfiguration() =>
        CommitmentPolicyConfiguration.Decode(
            File.ReadAllBytes(Path.Combine(
                RepositoryRoot(),
                "benchmarks",
                "configurations",
                "wp3-boundary-test-v1.json")));

    private static byte[] ReadFixture(string path) =>
        Encoding.UTF8.GetBytes(FixtureLoader.ReadUtf8(path));

    private static byte[] DecisionApplied(
        long epoch,
        long simTime,
        string hash) =>
        Encoding.UTF8.GetBytes(
            $$"""
            {
              "schemaVersion":"1.0.0",
              "messageType":"decisionApplied",
              "runId":"wp2-run-001",
              "scenarioId":"wp2-two-epoch-small",
              "epochId":{{epoch}},
              "simTimeMs":{{simTime}},
              "payload":{"decisionHash":"{{hash}}"}
            }
            """);

    private static byte[] CheckpointRequest() =>
        Encoding.UTF8.GetBytes(
            """
            {
              "schemaVersion":"1.0.0",
              "messageType":"checkpoint",
              "runId":"wp2-run-001",
              "scenarioId":"wp2-two-epoch-small",
              "payload":{}
            }
            """);

    private static byte[] RestoreRequest(JsonElement payload) =>
        Encoding.UTF8.GetBytes(
            $$"""
            {
              "schemaVersion":"1.0.0",
              "messageType":"restore",
              "runId":"wp2-run-001",
              "scenarioId":"wp2-two-epoch-small",
              "payload":{{payload.GetRawText()}}
            }
            """);

    private static string RepositoryRoot() =>
        typeof(Wp4RunnerIntegrationTests)
            .Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(value => value.Key == "RideBoundRepositoryRoot")
            .Value!;

    private sealed record SessionSetup(
        RunnerSession Session,
        CommitmentPolicyConfiguration Commitment,
        Wp4RunnerConfiguration Wp4);

    private sealed class UnknownSolver : ICandidateSelectionSolver
    {
        public CandidateSelectionSolveResult Solve(
            CandidateSelectionProblem problem,
            DeterministicSolverBudget budget)
        {
            var diagnostics = CandidateSelectionSolverDiagnostics.Create(
                problem,
                budget,
                0,
                0,
                0,
                []).Value!;
            return CandidateSelectionSolveResult.Unknown(
                diagnostics,
                "TEST_UNKNOWN",
                "The injected test solver did not produce an incumbent.");
        }
    }
}
