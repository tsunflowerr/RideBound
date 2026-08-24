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
    public async Task Cli_explicit_line_budget_accepts_a_bounded_frame_above_legacy_default()
    {
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
        startInfo.ArgumentList.Add("online");
        startInfo.ArgumentList.Add("--maximum-line-bytes");
        startInfo.ArgumentList.Add((2 * 1024 * 1024).ToString(
            System.Globalization.CultureInfo.InvariantCulture));

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);
        var oversizedForLegacy = "{\"padding\":\""
            + new string('x', 1_100_000)
            + "\"}\n";
        await process.StandardInput.WriteAsync(oversizedForLegacy);
        process.StandardInput.Close();
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.Equal(0, process.ExitCode);
        Assert.Contains("SCHEMA_VALIDATION_FAILED", stderr, StringComparison.Ordinal);
        Assert.DoesNotContain("MESSAGE_TOO_LARGE", stderr, StringComparison.Ordinal);
        using var response = JsonDocument.Parse(stdout.Trim());
        Assert.Equal(
            "error",
            response.RootElement.GetProperty("messageType").GetString());
        Assert.Equal(
            "SCHEMA_VALIDATION_FAILED",
            response.RootElement.GetProperty("payload")
                .GetProperty("code")
                .GetString());
    }

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
        startInfo.ArgumentList.Add("--solver-seed-source");
        startInfo.ArgumentList.Add("manifest-master-seed");

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
        Assert.Null(first.Value.Solver.ExecutionEvidence);
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
    public void Audited_solver_evidence_is_hash_bound_complete_and_excludes_wall_time()
    {
        var commitment = CommitmentConfiguration();
        var wp4 = PublishedWp4Configuration(
            commitment,
            emitSolverExecutionEvidence: true);
        var setup = CreateSession(wp4, commitmentConfiguration: commitment);
        var response = setup.Session.Process(
            ReadFixture("wp2/valid-bootstrap-event-batch.json")).Response!;
        var decision = DecisionPayloadCodec.Decode(response.Payload);

        Assert.True(decision.IsSuccess, decision.Error?.Message);
        var evidence = decision.Value!.Solver.ExecutionEvidence;
        Assert.NotNull(evidence);
        Assert.Equal(
            "1.1.0",
            evidence.Value.GetProperty("evidenceVersion").GetString());
        var generation = evidence.Value.GetProperty("generation");
        Assert.True(generation.GetProperty("totalPendingRequestCount").GetInt64() > 0);
        Assert.Equal(
            JsonValueKind.Array,
            generation.GetProperty("exogenousServiceQualityBreaches").ValueKind);
        Assert.All(
            generation.GetProperty("vehicleLosses").EnumerateArray(),
            loss => Assert.False(string.IsNullOrEmpty(
                loss.GetProperty("vehicleId").GetString())));
        var selection = evidence.Value.GetProperty("selection");
        Assert.Equal("optimal", selection.GetProperty("primarySolveStatus").GetString());
        Assert.Equal("optimal", selection.GetProperty("finalSolveStatus").GetString());
        Assert.Equal(
            "validatedIncumbent",
            selection.GetProperty("executionPath").GetString());
        Assert.NotEmpty(
            selection.GetProperty("finalSolverDiagnostics")
                .GetProperty("objectiveBounds")
                .EnumerateArray());
        Assert.DoesNotContain(
            "wallTime",
            evidence.Value.GetRawText(),
            StringComparison.OrdinalIgnoreCase);

        var encoded = DecisionPayloadCodec.Encode(decision.Value);
        using var encodedDocument = JsonDocument.Parse(encoded);
        var roundTrip = DecisionPayloadCodec.Decode(encodedDocument.RootElement);
        Assert.True(roundTrip.IsSuccess, roundTrip.Error?.Message);
        Assert.Equal(
            evidence.Value.GetRawText(),
            roundTrip.Value!.Solver.ExecutionEvidence!.Value.GetRawText());
    }

    [Fact]
    public void Retained_portfolio_profile_is_complete_and_operationally_neutral()
    {
        var commitment = CommitmentConfiguration();
        var legacySetup = CreateSession(
            PublishedWp4Configuration(
                commitment,
                emitSolverExecutionEvidence: true),
            commitmentConfiguration: commitment);
        var portfolioSetup = CreateSession(
            PublishedWp4Configuration(
                commitment,
                emitSolverExecutionEvidence: true,
                retainCandidatePortfolio: true),
            commitmentConfiguration: commitment);
        var batch = ReadFixture("wp2/valid-bootstrap-event-batch.json");
        var legacy = DecisionPayloadCodec.Decode(
            legacySetup.Session.Process(batch).Response!.Payload);
        var retained = DecisionPayloadCodec.Decode(
            portfolioSetup.Session.Process(batch).Response!.Payload);

        Assert.True(legacy.IsSuccess, legacy.Error?.Message);
        Assert.True(retained.IsSuccess, retained.Error?.Message);
        Assert.Equal(legacy.Value!.Status, retained.Value!.Status);
        Assert.Equal(legacy.Value.ReasonCode, retained.Value.ReasonCode);
        Assert.Equal(legacy.Value.StateBeforeHash, retained.Value.StateBeforeHash);
        Assert.Equal(legacy.Value.StateAfterHash, retained.Value.StateAfterHash);
        Assert.Equal(legacy.Value.Solver.Status, retained.Value.Solver.Status);
        Assert.Equal(
            legacy.Value.Actions.Select(value => value.GetRawText()),
            retained.Value.Actions.Select(value => value.GetRawText()));

        var legacyEvidence = legacy.Value.Solver.ExecutionEvidence!.Value;
        var retainedEvidence = retained.Value.Solver.ExecutionEvidence!.Value;
        Assert.Equal(
            "1.1.0",
            legacyEvidence.GetProperty("evidenceVersion").GetString());
        Assert.False(legacyEvidence.TryGetProperty("candidatePortfolio", out _));
        Assert.Equal(
            "1.2.0",
            retainedEvidence.GetProperty("evidenceVersion").GetString());
        var portfolio = retainedEvidence.GetProperty("candidatePortfolio");
        Assert.Equal(
            "1.0.0",
            portfolio.GetProperty("portfolioVersion").GetString());
        Assert.Equal(
            "https://ridebound.local/schemas/wp13/v1/" +
            "runner-retained-candidate-portfolio-evidence.schema.json",
            portfolio.GetProperty("schemaId").GetString());
        Assert.Equal(
            "rollingCost",
            portfolio.GetProperty("objectiveProfile").GetString());

        var problem = portfolio.GetProperty("selectionProblem");
        var objectiveCount = problem.GetProperty("objectiveLevels")
            .GetArrayLength();
        var requestIds = problem.GetProperty("requestIds")
            .EnumerateArray()
            .Select(value => value.GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        var vehicleIds = problem.GetProperty("vehicleIds")
            .EnumerateArray()
            .Select(value => value.GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        var candidates = portfolio.GetProperty("candidates")
            .EnumerateArray()
            .ToArray();
        Assert.NotEmpty(candidates);
        Assert.Equal(
            (long)candidates.Length,
            portfolio.GetProperty("generatedCandidateCount").GetInt64());
        Assert.Equal(
            (long)candidates.Count(
                value => value.GetProperty("policyEligibility").GetString()
                    == "eligible"),
            portfolio.GetProperty("policyEligibleCandidateCount").GetInt64());
        Assert.Equal(
            candidates.Length,
            candidates.Select(
                    value => value.GetProperty("candidateId").GetString())
                .Distinct(StringComparer.Ordinal)
                .Count());
        var eligibleIds = candidates
            .Where(
                value => value.GetProperty("policyEligibility").GetString()
                    == "eligible")
            .Select(value => value.GetProperty("candidateId").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        var selectedIds = portfolio.GetProperty("selectedCandidateIds")
            .EnumerateArray()
            .Select(value => value.GetString()!)
            .ToArray();
        Assert.Equal(vehicleIds.Count, selectedIds.Length);
        Assert.All(selectedIds, value => Assert.Contains(value, eligibleIds));

        foreach (var candidate in candidates)
        {
            Assert.Contains(
                candidate.GetProperty("vehicleId").GetString()!,
                vehicleIds);
            var eligible = candidate.GetProperty("policyEligibility")
                .GetString() == "eligible";

            if (eligible)
            {
                Assert.All(
                    candidate.GetProperty("newRequestIds").EnumerateArray(),
                    value => Assert.Contains(value.GetString()!, requestIds));
            }

            Assert.Equal(
                eligible,
                candidate.TryGetProperty(
                    "objectiveContributions",
                    out var contributions));

            if (eligible)
            {
                Assert.Equal(objectiveCount, contributions.GetArrayLength());
            }

            var route = candidate.GetProperty("route");
            var remainingStopIds = route.GetProperty("frozenPrefix")
                .EnumerateArray()
                .Skip(route.GetProperty("executedStopCount").GetInt32())
                .Concat(route.GetProperty("mutableSuffix").EnumerateArray())
                .Select(value => value.GetProperty("stopId").GetString());
            var scheduledStopIds = candidate.GetProperty("schedule")
                .GetProperty("stops")
                .EnumerateArray()
                .Select(value => value.GetProperty("stopId").GetString());
            Assert.Equal(remainingStopIds, scheduledStopIds);
        }

        Assert.DoesNotContain(
            "wallTime",
            retainedEvidence.GetRawText(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Booking_confirmation_C1_ranks_each_vehicle_scope_before_fleet_validation()
    {
        var commitment = CommitmentConfiguration();
        var wp4 = Wp4RunnerConfiguration.Decode(
            File.ReadAllBytes(Path.Combine(
                RepositoryRoot(),
                "benchmarks",
                "configurations",
                "wp7-fleetpy-ridebound-hard-vector-v1.json")),
            commitment);
        var setup = CreateSession(
            wp4,
            commitmentConfiguration: commitment);
        var bootstrap = JsonNode.Parse(
            FixtureLoader.ReadUtf8("wp2/valid-bootstrap-event-batch.json"))!;
        var events = bootstrap["payload"]!["events"]!.AsArray();
        var secondVehicle = events[2]!.DeepClone();
        secondVehicle["eventSeq"] = 4;
        secondVehicle["payload"]!["vehicle"]!["vehicleId"] = "v-2";
        secondVehicle["payload"]!["vehicle"]!["route"]!["mutableSuffix"] =
            new JsonArray();
        events.Add(secondVehicle);

        var offerResponse = setup.Session.Process(
            Encoding.UTF8.GetBytes(bootstrap.ToJsonString())).Response!;
        var offer = DecisionPayloadCodec.Decode(offerResponse.Payload);
        Assert.True(offer.IsSuccess, offer.Error?.Message);
        Assert.Contains(
            offer.Value!.Actions,
            action => action.GetProperty("decisionType").GetString()
                == "requestAccepted");
        var accepted = offer.Value.Actions.Single(
            action => action.GetProperty("decisionType").GetString()
                == "requestAccepted");
        var selectedVehicle = accepted.GetProperty("payload")
            .GetProperty("vehicleId")
            .GetString()!;
        var selectedRoute = offer.Value.Actions.Single(
            action => action.GetProperty("decisionType").GetString()
                == "vehiclePlanUpdated"
                && action.GetProperty("payload")
                    .GetProperty("vehicleId")
                    .GetString() == selectedVehicle)
            .GetProperty("payload")
            .GetProperty("route")
            .GetRawText();
        _ = setup.Session.Process(
            DecisionApplied(1, 1_000, offer.Value.DecisionHash.Value));

        var bookingResponse = setup.Session.Process(
            Encoding.UTF8.GetBytes(
                """
                {
                  "schemaVersion":"1.0.0",
                  "messageType":"eventBatch",
                  "runId":"wp2-run-001",
                  "scenarioId":"wp2-two-epoch-small",
                  "epochId":2,
                  "simTimeMs":1000,
                  "payload":{"events":[
                    {
                      "eventSeq":5,
                      "eventType":"bookingConfirmed",
                      "payload":{"requestId":"r-1"}
                    }
                  ]}
                }
                """)).Response!;
        var booking = DecisionPayloadCodec.Decode(bookingResponse.Payload);

        Assert.Equal("decision", bookingResponse.MessageType.Value);
        Assert.True(booking.IsSuccess, booking.Error?.Message);
        Assert.Equal(SolverStatus.Completed, booking.Value!.Solver.Status);
        Assert.Single(
            booking.Value.Actions,
            action => action.GetProperty("decisionType").GetString()
                == "promisePublished");
        _ = setup.Session.Process(
            DecisionApplied(2, 1_000, booking.Value.DecisionHash.Value));

        // Half-edge progress is deliberately quantized one permille below the
        // exact elapsed fraction: the old plan moves from pickup ETA 1100 to
        // 1101. This is exogenous observation drift, not a decision changing a
        // final-confirmation lock, so C1 must retain its no-op candidate.
        var progressBatch = JsonNode.Parse(
                """
                {
                  "schemaVersion":"1.0.0",
                  "messageType":"eventBatch",
                  "runId":"wp2-run-001",
                  "scenarioId":"wp2-two-epoch-small",
                  "epochId":3,
                  "simTimeMs":1050,
                  "payload":{"events":[
                    {
                      "eventSeq":6,
                      "eventType":"vehicleAdvanced",
                      "payload":{"vehicle":{
                        "vehicleId":"placeholder",
                        "capacity":4,
                        "occupiedSeats":0,
                        "position":{
                          "kind":"edgeProgress",
                          "edgeId":"edge-n0-n1",
                          "fromNodeId":"n-0",
                          "toNodeId":"n-1",
                          "progressPermille":499
                        },
                        "onboardRequestIds":[],
                        "acceptedRequestIds":["r-1"],
                        "route":{}
                      }}
                    }
                  ]}
                }
                """)!;
        var observedVehicle = progressBatch["payload"]!["events"]![0]![
            "payload"]!["vehicle"]!;
        observedVehicle["vehicleId"] = selectedVehicle;
        observedVehicle["route"] = JsonNode.Parse(selectedRoute);
        var progressResponse = setup.Session.Process(
            Encoding.UTF8.GetBytes(progressBatch.ToJsonString())).Response!;
        var progress = DecisionPayloadCodec.Decode(progressResponse.Payload);

        Assert.Equal("decision", progressResponse.MessageType.Value);
        Assert.True(progress.IsSuccess, progress.Error?.Message);
        var revised = Assert.Single(
            progress.Value!.Actions,
            action => action.GetProperty("decisionType").GetString()
                == "promisePublished");
        var publication = revised.GetProperty("payload");
        Assert.Equal(
            1,
            publication.GetProperty("exogenousDelta")
                .GetProperty("pickupEtaTotalMs")
                .GetInt64());
        Assert.Equal(
            0,
            publication.GetProperty("decisionDelta")
                .GetProperty("pickupEtaTotalMs")
                .GetInt64());
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
    public void Explicit_manifest_seed_mode_passes_the_manifest_seed_to_solver()
    {
        var solver = new CapturingUnknownSolver();
        var setup = CreateSession(
            PublishedWp4Configuration(),
            solver,
            useManifestSolverSeed: true);

        var response = setup.Session.Process(
            ReadFixture("wp2/valid-bootstrap-event-batch.json")).Response!;
        var decision = DecisionPayloadCodec.Decode(response.Payload);

        Assert.True(decision.IsSuccess, decision.Error?.Message);
        Assert.Equal(20260729, solver.ObservedSeed);
        Assert.Equal(SolverStatus.SafeFallback, decision.Value!.Solver.Status);
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
        CommitmentPolicyConfiguration? commitmentConfiguration = null,
        bool useManifestSolverSeed = false)
    {
        var commitment = commitmentConfiguration ?? CommitmentConfiguration();
        var session = NewSession(
            commitment,
            wp4,
            solver,
            useManifestSolverSeed);
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
        ICandidateSelectionSolver? solver = null,
        bool useManifestSolverSeed = false) =>
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
                : new SolverBackedRidePoolingPolicy(solver),
            useManifestSolverSeed: useManifestSolverSeed);

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
        CommitmentPolicyConfiguration? commitmentConfiguration = null,
        bool emitSolverExecutionEvidence = false,
        bool retainCandidatePortfolio = false)
    {
        var commitment = commitmentConfiguration ?? CommitmentConfiguration();
        var json = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "benchmarks",
            "configurations",
            "wp4-rolling-cost-boundary-v1.json"));

        if (emitSolverExecutionEvidence || retainCandidatePortfolio)
        {
            var profile = retainCandidatePortfolio
                ? ",\n  \"solverExecutionEvidenceProfile\": " +
                    "\"retained-portfolio-v1\""
                : string.Empty;
            json = json.Replace(
                "\"policyVersion\": \"wp4-boundary-v1\"",
                "\"policyVersion\": \"wp4-boundary-v1\",\n  " +
                "\"emitSolverExecutionEvidence\": true" + profile,
                StringComparison.Ordinal);
        }

        return Wp4RunnerConfiguration.Decode(
            Encoding.UTF8.GetBytes(json),
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

    private sealed class CapturingUnknownSolver : ICandidateSelectionSolver
    {
        public long? ObservedSeed { get; private set; }

        public CandidateSelectionSolveResult Solve(
            CandidateSelectionProblem problem,
            DeterministicSolverBudget budget)
        {
            ObservedSeed = budget.RandomSeed;
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
