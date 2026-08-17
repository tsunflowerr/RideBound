using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using RideBound.Benchmarking.Contracts;
using RideBound.Benchmarking.Execution;
using RideBound.Benchmarking.Storage;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;

namespace RideBound.Benchmarking.Tests;

public sealed class AppendOnlyRunStoreTests
{
    private const string PlanHash =
        "1111111111111111111111111111111111111111111111111111111111111111";
    private const string ScenarioHash =
        "2222222222222222222222222222222222222222222222222222222222222222";
    private const string NewPlanHash =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static readonly ProcessArtifactInventoryEntry FixtureArtifact =
        new("runner", "runner.dll", 4, new string('e', 64));
    private static readonly string ArtifactHash = ProcessArtifactIdentity.Calculate(
        [FixtureArtifact]);
    private static readonly string[] Denominators = ["planned-runs", "valid-runs"];

    [Fact]
    public async Task Success_is_immutable_idempotent_indexed_and_conserved()
    {
        using var temp = new TestDirectory();
        var store = CreateStore(temp);
        var intent = Intent("b1", executionOrdinal: 1);
        await store.InitializePlanAsync(PlanHash, [intent], Denominators);
        var submission = Submission(temp, intent, RunTerminalStatus.Succeeded);

        var first = await store.CommitAsync(submission);
        var second = await store.CommitAsync(submission);
        var verification = store.VerifyPlan(PlanHash);

        Assert.False(first.ReusedExistingTerminal);
        Assert.True(second.ReusedExistingTerminal);
        Assert.True(verification.IsValid, FormatIssues(verification));
        Assert.Equal((1, 1, 0, 0, 0), Counts(verification));
        var index = File.ReadAllLines(
            Path.Combine(first.RunDirectory, "observation-index.ndjson"));
        Assert.Equal(3, index.Length);
        Assert.Equal(
            ["inputEvent", "decisionAck", "outputDecision"],
            index.Select(ReadRecordKind));
        Assert.Equal(
            Enum.GetValues<RawRunEvidenceRole>().Length + 2,
            Directory.GetFiles(first.RunDirectory).Length);
    }

    [Fact]
    public async Task Failure_and_exclusion_share_one_gapless_hash_chain_and_no_metric_file()
    {
        using var temp = new TestDirectory();
        var store = CreateStore(temp);
        var failureIntent = Intent("b1", 1);
        var exclusionIntent = Intent("c1", 2);
        await store.InitializePlanAsync(
            PlanHash,
            [failureIntent, exclusionIntent],
            Denominators);

        var failed = await store.CommitAsync(
            Submission(temp, failureIntent, RunTerminalStatus.Failed));
        var excluded = await store.CommitAsync(
            Submission(temp, exclusionIntent, RunTerminalStatus.Excluded));
        var verification = store.VerifyPlan(PlanHash);

        Assert.True(verification.IsValid, FormatIssues(verification));
        Assert.Equal((2, 0, 1, 1, 0), Counts(verification));
        Assert.DoesNotContain(
            Directory.GetFiles(failed.RunDirectory),
            path => path.Contains("metric", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            Directory.GetFiles(excluded.RunDirectory),
            path => path.Contains("metric", StringComparison.OrdinalIgnoreCase));
        var logs = LogFiles(temp, PlanHash);
        Assert.Equal(2, logs.Length);
        Assert.StartsWith("00000000000000000001-", Path.GetFileName(logs[0]), StringComparison.Ordinal);
        Assert.StartsWith("00000000000000000002-", Path.GetFileName(logs[1]), StringComparison.Ordinal);
        using var firstLog = JsonDocument.Parse(File.ReadAllBytes(logs[0]));
        using var secondLog = JsonDocument.Parse(File.ReadAllBytes(logs[1]));
        var firstHash = Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(logs[0])));
        Assert.Equal(
            firstHash,
            secondLog.RootElement.GetProperty("previousEntrySha256").GetString());
        Assert.Equal(
            new string('0', 64),
            firstLog.RootElement.GetProperty("previousEntrySha256").GetString());
    }

    [Fact]
    public async Task Solver_unknown_terminalizes_as_typed_decision_failure()
    {
        using var temp = new TestDirectory();
        var store = CreateStore(temp);
        var intent = Intent("c1", 1);
        await store.InitializePlanAsync(PlanHash, [intent], Denominators);
        var submission = Submission(temp, intent, RunTerminalStatus.Failed) with
        {
            Failure = new RunFailureInput(
                "solver.unknown",
                "decision",
                500,
                "runner-supervisor",
                RawRunEvidenceRole.Output,
                "Solver returned an unknown terminal status.",
                Denominators),
        };

        var commit = await store.CommitAsync(submission);
        var decoded = BenchmarkContractCodec.Decode<FailureRecord>(
            File.ReadAllBytes(Path.Combine(commit.RunDirectory, "failure-record.json")));

        Assert.True(decoded.IsSuccess, decoded.Error?.ToString());
        Assert.Equal("solver.unknown", decoded.Value!.Code);
        Assert.Equal("decision", decoded.Value.Stage);
        Assert.True(store.VerifyPlan(PlanHash).IsValid);
    }

    [Fact]
    public async Task Concurrent_arms_cannot_overwrite_or_cross_link_directories()
    {
        using var temp = new TestDirectory();
        var store = CreateStore(temp);
        var intents = Enumerable.Range(0, 12)
            .Select(index => Intent($"arm-{index:D2}", index + 1))
            .ToArray();
        await store.InitializePlanAsync(PlanHash, intents, Denominators);

        await Task.WhenAll(
            intents.Select(
                intent => store.CommitAsync(
                    Submission(temp, intent, RunTerminalStatus.Succeeded))));
        var verification = store.VerifyPlan(PlanHash);

        Assert.True(verification.IsValid, FormatIssues(verification));
        Assert.Equal((12, 12, 0, 0, 0), Counts(verification));

        foreach (var intent in intents)
        {
            var path = Path.Combine(
                StoreRoot(temp),
                PlanHash,
                "runs",
                intent.RunId,
                "run-record.json");
            var decoded = BenchmarkContractCodec.Decode<RunRecord>(File.ReadAllBytes(path));
            Assert.True(decoded.IsSuccess, decoded.Error?.ToString());
            Assert.Equal(intent.RunId, decoded.Value!.RunId);
            Assert.Equal(intent.ArmId, decoded.Value.ArmId);
        }
    }

    [Fact]
    public async Task Duplicate_attempt_inside_one_plan_is_rejected_as_selective_retry()
    {
        using var temp = new TestDirectory();
        var store = CreateStore(temp);
        var first = Intent("b1", 1);
        var duplicateRunId = BenchmarkIdentity.CalculateRun(
            PlanHash,
            ScenarioHash,
            first.ArmId,
            first.RepeatIndex,
            attemptIndex: 1);
        var duplicateAttempt = first with
        {
            RunId = duplicateRunId,
            AttemptIndex = 1,
            ExecutionOrdinal = 2,
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => store.InitializePlanAsync(
                PlanHash,
                [first, duplicateAttempt],
                Denominators));
        Assert.False(Directory.Exists(Path.Combine(StoreRoot(temp), PlanHash)));
    }

    [Theory]
    [InlineData(RunStoreWriteBoundary.IntentValidated)]
    [InlineData(RunStoreWriteBoundary.EvidenceCopied)]
    [InlineData(RunStoreWriteBoundary.ObservationIndexWritten)]
    [InlineData(RunStoreWriteBoundary.TerminalDetailWritten)]
    [InlineData(RunStoreWriteBoundary.RunRecordWritten)]
    [InlineData(RunStoreWriteBoundary.LogSegmentCommitted)]
    [InlineData(RunStoreWriteBoundary.RunDirectoryCommitted)]
    public async Task Every_write_boundary_recovers_to_one_terminal_attempt(
        RunStoreWriteBoundary boundary)
    {
        using var temp = new TestDirectory();
        var store = CreateStore(temp);
        var intent = Intent("b1", 1);
        await store.InitializePlanAsync(PlanHash, [intent], Denominators);
        var submission = Submission(temp, intent, RunTerminalStatus.Failed);

        await Assert.ThrowsAsync<InjectedStoreCrashException>(
            () => store.CommitAsync(submission, new ThrowOnce(boundary)));
        var recovered = await store.CommitAsync(submission);
        var verification = store.VerifyPlan(PlanHash);

        Assert.Equal(intent.RunId, recovered.RunRecord.RunId);
        Assert.True(verification.IsValid, FormatIssues(verification));
        Assert.Equal((1, 0, 1, 0, 0), Counts(verification));
        Assert.Single(LogFiles(temp, PlanHash));
    }

    [Fact]
    public async Task Seal_turns_uncommitted_planned_intent_into_typed_persistence_failure()
    {
        using var temp = new TestDirectory();
        var store = CreateStore(temp);
        var complete = Intent("b1", 1);
        var missing = Intent("c1", 2);
        await store.InitializePlanAsync(PlanHash, [complete, missing], Denominators);
        await store.CommitAsync(Submission(temp, complete, RunTerminalStatus.Succeeded));
        var before = store.VerifyPlan(PlanHash);

        var sealedRuns = await store.SealIncompleteRunsAsync(
            PlanHash,
            "2026-08-09T12:00:00Z");
        var after = store.VerifyPlan(PlanHash);

        Assert.False(before.IsValid);
        Assert.Equal(1, before.PendingCount);
        var sealedRun = Assert.Single(sealedRuns);
        Assert.Equal(missing.RunId, sealedRun.RunRecord.RunId);
        Assert.True(after.IsValid, FormatIssues(after));
        Assert.Equal((2, 1, 1, 0, 0), Counts(after));
        var failure = BenchmarkContractCodec.Decode<FailureRecord>(
            File.ReadAllBytes(Path.Combine(sealedRun.RunDirectory, "failure-record.json")));
        Assert.True(failure.IsSuccess, failure.Error?.ToString());
        Assert.Equal("harness.persistence-incomplete", failure.Value!.Code);
        Assert.Equal("persistence", failure.Value.Stage);
    }

    [Fact]
    public async Task Seal_and_inflight_commit_converge_to_the_single_lock_winner()
    {
        using var temp = new TestDirectory();
        var store = CreateStore(temp);
        var intent = Intent("b1", 1);
        await store.InitializePlanAsync(PlanHash, [intent], Denominators);
        using var pause = new PauseAtBoundary(RunStoreWriteBoundary.IntentValidated);
        var commitTask = Task.Run(
            () => store.CommitAsync(
                Submission(temp, intent, RunTerminalStatus.Succeeded),
                pause));
        Assert.True(pause.Entered.Wait(TimeSpan.FromSeconds(10)));
        var sealTask = store.SealIncompleteRunsAsync(
            PlanHash,
            "2026-08-11T00:00:00Z");
        pause.Release.Set();
        var committed = await commitTask;
        var sealedRuns = await sealTask;

        Assert.Equal(RunTerminalStatus.Succeeded, committed.RunRecord.TerminalStatus);
        Assert.Empty(sealedRuns);
        var verification = store.VerifyPlan(PlanHash);
        Assert.True(verification.IsValid, FormatIssues(verification));
        Assert.Equal((1, 1, 0, 0, 0), Counts(verification));
    }

    [Fact]
    public async Task Outcome_based_exclusion_and_private_safe_text_fail_before_terminal_write()
    {
        using var temp = new TestDirectory();
        var store = CreateStore(temp);
        var intent = Intent("b1", 1);
        await store.InitializePlanAsync(PlanHash, [intent], Denominators);
        var outcomeBased = Submission(temp, intent, RunTerminalStatus.Excluded);
        outcomeBased = outcomeBased with
        {
            Exclusion = outcomeBased.Exclusion! with { BeforeOutcome = false },
        };
        await Assert.ThrowsAsync<ArgumentException>(() => store.CommitAsync(outcomeBased));

        var privateFailure = Submission(temp, intent, RunTerminalStatus.Failed);
        privateFailure = privateFailure with
        {
            Failure = privateFailure.Failure! with
            {
                SafeMessage = "Bearer token abc123 leaked from requestId.",
            },
        };
        await Assert.ThrowsAsync<ArgumentException>(() => store.CommitAsync(privateFailure));
        var unknownFailure = Submission(temp, intent, RunTerminalStatus.Failed);
        unknownFailure = unknownFailure with
        {
            Failure = unknownFailure.Failure! with { Code = "unknown.failure" },
        };
        await Assert.ThrowsAsync<ArgumentException>(() => store.CommitAsync(unknownFailure));
        Assert.False(
            Directory.Exists(Path.Combine(StoreRoot(temp), PlanHash, "runs", intent.RunId)));
        Assert.False(
            Directory.Exists(Path.Combine(StoreRoot(temp), PlanHash, ".staging", intent.RunId)));
    }

    [Fact]
    public async Task Malformed_resource_samples_fail_before_terminal_commit()
    {
        using var temp = new TestDirectory();
        var store = CreateStore(temp);
        var intent = Intent("b1", 1);
        await store.InitializePlanAsync(PlanHash, [intent], Denominators);
        var submission = Submission(temp, intent, RunTerminalStatus.Succeeded);
        var samples = submission.EvidenceSources.Single(
            value => value.Role == RawRunEvidenceRole.ResourceSamples);
        File.WriteAllBytes(samples.FullPath, "{\"elapsedMs\":1}\n"u8.ToArray());
        var repinned = RawRunEvidenceSource.Pin(
            RawRunEvidenceRole.ResourceSamples,
            samples.FullPath);
        submission = submission with
        {
            EvidenceSources = submission.EvidenceSources
                .Select(value => value.Role == repinned.Role ? repinned : value)
                .ToArray(),
        };

        await Assert.ThrowsAsync<InvalidDataException>(() => store.CommitAsync(submission));
        Assert.False(
            Directory.Exists(Path.Combine(StoreRoot(temp), PlanHash, "runs", intent.RunId)));
    }

    [Theory]
    [InlineData("forged-inventory")]
    [InlineData("changed-launch")]
    public async Task Artifact_receipts_are_rederived_and_bind_one_launch(string mutation)
    {
        using var temp = new TestDirectory();
        var store = CreateStore(temp);
        var intent = Intent("b1", 1);
        await store.InitializePlanAsync(PlanHash, [intent], Denominators);
        var submission = Submission(temp, intent, RunTerminalStatus.Succeeded);
        var role = mutation == "forged-inventory"
            ? RawRunEvidenceRole.ArtifactPreflight
            : RawRunEvidenceRole.ArtifactPostflight;
        var stage = role == RawRunEvidenceRole.ArtifactPreflight
            ? "preflight"
            : "postflight";
        var bytes = mutation == "forged-inventory"
            ? ArtifactReceipt(stage, artifactSha256: new string('f', 64))
            : ArtifactReceipt(stage, launchCommandSha256: new string('b', 64));
        submission = ReplaceEvidence(submission, role, bytes);

        await Assert.ThrowsAsync<InvalidDataException>(() => store.CommitAsync(submission));
        Assert.False(
            Directory.Exists(Path.Combine(StoreRoot(temp), PlanHash, "runs", intent.RunId)));
    }

    [Fact]
    public async Task Verifier_rejects_cross_run_evidence_locator_with_valid_local_bytes()
    {
        using var temp = new TestDirectory();
        var store = CreateStore(temp);
        var intent = Intent("b1", 1);
        await store.InitializePlanAsync(PlanHash, [intent], Denominators);
        var committed = await store.CommitAsync(
            Submission(temp, intent, RunTerminalStatus.Succeeded));
        var recordPath = Path.Combine(committed.RunDirectory, "run-record.json");
        var decoded = BenchmarkContractCodec.Decode<RunRecord>(File.ReadAllBytes(recordPath));
        Assert.True(decoded.IsSuccess, decoded.Error?.ToString());
        File.WriteAllBytes(
            recordPath,
            BenchmarkContractCodec.Encode(
                decoded.Value! with
                {
                    InputFile = decoded.Value.InputFile with
                    {
                        RelativePath = "runs/cross-linked-run/input.ndjson",
                    },
                }));

        var verification = store.VerifyPlan(PlanHash);

        Assert.False(verification.IsValid);
        Assert.Contains(verification.Issues, issue => issue.Code == "run.invalid");
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public async Task Checkpoint_must_bind_the_last_applied_epoch_and_simulation_time(
        long checkpointEpoch,
        bool expectedValid)
    {
        using var temp = new TestDirectory();
        var store = CreateStore(temp);
        var intent = Intent("b1", 1);
        await store.InitializePlanAsync(PlanHash, [intent], Denominators);
        var submission = Submission(temp, intent, RunTerminalStatus.Succeeded);
        var (input, output) = SuccessfulCheckpointTranscripts(intent, checkpointEpoch);
        submission = ReplaceTranscriptEvidence(submission, input, output);

        if (!expectedValid)
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => store.CommitAsync(submission));
            return;
        }

        await store.CommitAsync(submission);
        Assert.True(store.VerifyPlan(PlanHash).IsValid);
    }

    [Fact]
    public async Task Log_tamper_gap_and_denominator_mutation_are_detected()
    {
        using var tamperTemp = new TestDirectory();
        var tamperStore = await CreateTwoFailureStore(tamperTemp);
        var tamperLog = LogFiles(tamperTemp, PlanHash)[0];
        File.AppendAllText(tamperLog, " ");
        Assert.False(tamperStore.VerifyPlan(PlanHash).IsValid);

        using var gapTemp = new TestDirectory();
        var gapStore = await CreateTwoFailureStore(gapTemp);
        var gapLogs = LogFiles(gapTemp, PlanHash);
        File.Move(gapLogs[0], Path.Combine(gapTemp.Root, "removed-log.json"));
        Assert.False(gapStore.VerifyPlan(PlanHash).IsValid);

        using var denominatorTemp = new TestDirectory();
        var denominatorStore = await CreateTwoFailureStore(denominatorTemp);
        var firstIntent = Intent("b1", 1);
        var failurePath = Path.Combine(
            StoreRoot(denominatorTemp),
            PlanHash,
            "runs",
            firstIntent.RunId,
            "failure-record.json");
        var decoded = BenchmarkContractCodec.Decode<FailureRecord>(File.ReadAllBytes(failurePath));
        Assert.True(decoded.IsSuccess, decoded.Error?.ToString());
        File.WriteAllBytes(
            failurePath,
            BenchmarkContractCodec.Encode(
                decoded.Value! with { AffectedDenominatorIds = ["wrong-denominator"] }));
        Assert.False(denominatorStore.VerifyPlan(PlanHash).IsValid);
    }

    [Fact]
    public async Task Authorized_rerun_creates_a_complete_new_plan_and_preserves_prior_evidence()
    {
        using var temp = new TestDirectory();
        var store = CreateStore(temp);
        var prior = Intent("b1", 1);
        await store.InitializePlanAsync(PlanHash, [prior], Denominators);
        var priorCommit = await store.CommitAsync(
            Submission(temp, prior, RunTerminalStatus.Failed));
        var priorRecordHash = Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(
                File.ReadAllBytes(Path.Combine(priorCommit.RunDirectory, "run-record.json"))));
        var rerun = Intent("b1", 1, NewPlanHash);

        await store.InitializeSupersedingPlanAsync(
            PlanHash,
            NewPlanHash,
            "authorized-bugfix-grid-001",
            [rerun],
            Denominators);
        await store.CommitAsync(Submission(temp, rerun, RunTerminalStatus.Succeeded));

        Assert.True(store.VerifyPlan(PlanHash).IsValid);
        Assert.True(store.VerifyPlan(NewPlanHash).IsValid);
        Assert.True(File.Exists(Path.Combine(StoreRoot(temp), NewPlanHash, "supersedes.json")));
        Assert.Equal(
            priorRecordHash,
            Convert.ToHexStringLower(
                System.Security.Cryptography.SHA256.HashData(
                    File.ReadAllBytes(Path.Combine(priorCommit.RunDirectory, "run-record.json")))));
    }

    [Fact]
    public async Task Authorized_rerun_rejects_selective_grid_and_binds_complete_prior_evidence()
    {
        using var temp = new TestDirectory();
        var store = CreateStore(temp);
        var priorB1 = Intent("b1", 1);
        var priorC1 = Intent("c1", 2);
        await store.InitializePlanAsync(PlanHash, [priorB1, priorC1], Denominators);
        var priorCommit = await store.CommitAsync(
            Submission(temp, priorB1, RunTerminalStatus.Succeeded));
        await store.CommitAsync(Submission(temp, priorC1, RunTerminalStatus.Failed));

        await Assert.ThrowsAsync<ArgumentException>(
            () => store.InitializeSupersedingPlanAsync(
                PlanHash,
                NewPlanHash,
                "selective-rerun-is-forbidden",
                [Intent("b1", 1, NewPlanHash)],
                Denominators));
        Assert.False(Directory.Exists(Path.Combine(StoreRoot(temp), NewPlanHash)));

        var newB1 = Intent("b1", 1, NewPlanHash);
        var newC1 = Intent("c1", 2, NewPlanHash);
        await store.InitializeSupersedingPlanAsync(
            PlanHash,
            NewPlanHash,
            "complete-grid-rerun-001",
            [newB1, newC1],
            Denominators);
        await store.CommitAsync(Submission(temp, newB1, RunTerminalStatus.Succeeded));
        await store.CommitAsync(Submission(temp, newC1, RunTerminalStatus.Succeeded));
        Assert.True(store.VerifyPlan(NewPlanHash).IsValid);

        File.AppendAllText(Path.Combine(priorCommit.RunDirectory, "input.ndjson"), " ");
        var verification = store.VerifyPlan(NewPlanHash);

        Assert.False(verification.IsValid);
        Assert.Contains(
            verification.Issues,
            issue => issue.Code == "rerun.authorization-invalid");
    }

    private static AppendOnlyRunStore CreateStore(TestDirectory temp) =>
        new(
            new AppendOnlyRunStoreOptions(
                StoreRoot(temp),
                temp.RepositoryRoot,
                MaximumEvidenceFileBytes: 2_000_000));

    private static string StoreRoot(TestDirectory temp) => Path.Combine(temp.Root, "store");

    private static RunStoreIntent Intent(
        string armId,
        long executionOrdinal,
        string planHash = PlanHash)
    {
        var runId = BenchmarkIdentity.CalculateRun(planHash, ScenarioHash, armId, 0, 0);
        return new RunStoreIntent(
            runId,
            planHash,
            ScenarioHash,
            armId,
            0,
            0,
            new string('1', 64),
            new string('4', 64),
            new string('6', 64),
            new string('5', 64),
            new string('7', 64),
            executionOrdinal,
            false,
            ArtifactHash);
    }

    private static TerminalRunSubmission Submission(
        TestDirectory temp,
        RunStoreIntent intent,
        RunTerminalStatus status)
    {
        var evidence = CreateEvidence(temp, intent, status);
        return new TerminalRunSubmission(
            intent,
            status,
            "2026-08-09T12:00:00Z",
            "2026-08-09T12:00:01Z",
            1000,
            500,
            10_000_000,
            2,
            ArtifactHash,
            ArtifactHash,
            evidence,
            ExitCode: status == RunTerminalStatus.Succeeded ? 0 : 17,
            Failure: status == RunTerminalStatus.Failed
                ? new RunFailureInput(
                    "process.crash",
                    "execution",
                    500,
                    "runner-supervisor",
                    RawRunEvidenceRole.StandardError,
                    "External process exited before protocol completion.",
                    Denominators)
                : null,
            Exclusion: status == RunTerminalStatus.Excluded
                ? new RunExclusionInput(
                    "arm.missing-required-capability",
                    "1.0.0",
                    new string('9', 64),
                    "preflight",
                    "benchmark-arm",
                    intent.ArmId,
                    true,
                    RawRunEvidenceRole.ArtifactPreflight,
                    Denominators,
                    "Declared capability is absent before execution.")
                : null);
    }

    private static IReadOnlyList<RawRunEvidenceSource> CreateEvidence(
        TestDirectory temp,
        RunStoreIntent intent,
        RunTerminalStatus status)
    {
        var root = Path.Combine(temp.Root, "sources", intent.RunId, status.ToString());
        Directory.CreateDirectory(root);
        var (input, output) = status == RunTerminalStatus.Excluded
            ? (Array.Empty<byte>(), Array.Empty<byte>())
            : SuccessfulTranscripts(intent);
        File.WriteAllBytes(Path.Combine(root, "input.ndjson"), input);
        File.WriteAllBytes(Path.Combine(root, "output.ndjson"), output);
        File.WriteAllText(
            Path.Combine(root, "stderr.txt"),
            status == RunTerminalStatus.Failed ? "deterministic failure\n" : string.Empty);
        File.WriteAllBytes(
            Path.Combine(root, "resource-samples.ndjson"),
            Encoding.UTF8.GetBytes(
                "{\"elapsedMs\":1,\"observedCpuTimeMs\":1," +
                "\"observedProcessCount\":1,\"observedWorkingSetBytes\":1}\n"));
        File.WriteAllBytes(
            Path.Combine(root, "artifact-preflight.json"),
            ArtifactReceipt("preflight"));
        File.WriteAllBytes(
            Path.Combine(root, "artifact-postflight.json"),
            ArtifactReceipt("postflight"));
        return Enum.GetValues<RawRunEvidenceRole>()
            .Select(
                role => RawRunEvidenceSource.Pin(
                    role,
                    Path.Combine(root, EvidenceFileName(role))))
            .ToArray();
    }

    private static (byte[] Input, byte[] Output) SuccessfulTranscripts(RunStoreIntent intent)
    {
        var repository = FindRepositoryRoot();
        var sourceInput = File.ReadAllLines(
            Path.Combine(
                repository,
                "benchmarks",
                "schemas",
                "fixtures",
                "runner",
                "full-tiny-transcript.input.ndjson"));
        var sourceOutput = File.ReadAllLines(
            Path.Combine(
                repository,
                "benchmarks",
                "schemas",
                "fixtures",
                "runner",
                "full-tiny-transcript.expected.ndjson"));
        var initialize = JsonNode.Parse(sourceInput[1])!.AsObject();
        initialize["runId"] = intent.RunId;
        var manifest = initialize["payload"]!["manifest"]!.AsObject();
        manifest["scenarioContentHash"] = intent.ScenarioHash;
        manifest["binarySha256"] = intent.RunnerArtifactSha256;
        manifest["policyConfigurationHash"] = intent.PolicyConfigurationSha256;
        manifest["masterSeed"] = BenchmarkSeed.ToNonNegativeInt32(intent.ComponentSeedHex);
        sourceInput[1] = initialize.ToJsonString();
        var decodedInitialize = ProtocolEnvelopeCodec.Decode(
            CanonicalJson.Canonicalize(Encoding.UTF8.GetBytes(sourceInput[1])));
        Assert.True(decodedInitialize.IsSuccess, decodedInitialize.Error?.Message);
        var decodedPayload = InitializeRunPayloadCodec.Decode(decodedInitialize.Envelope!.Payload);
        Assert.True(decodedPayload.IsSuccess, decodedPayload.Error?.Message);
        var manifestHash = ProtocolHash.CalculateManifestHash(decodedPayload.Value!.Manifest);
        var initialized = JsonNode.Parse(sourceOutput[1])!.AsObject();
        initialized["runId"] = intent.RunId;
        initialized["payload"]!["manifestHash"] = manifestHash.Value;
        sourceOutput[1] = initialized.ToJsonString();
        var decisionHash = JsonDocument.Parse(sourceOutput[2]).RootElement
            .GetProperty("payload")
            .GetProperty("decisionHash")
            .GetString()!;
        var acknowledgement =
            "{\"epochId\":1,\"messageType\":\"decisionApplied\",\"payload\":{" +
            $"\"decisionHash\":\"{decisionHash}\"}},\"runId\":\"run-001\"," +
            "\"scenarioId\":\"manhattan-20260729-a\",\"schemaVersion\":\"1.0.0\"," +
            "\"simTimeMs\":100}";
        var inputLines = new[]
        {
            sourceInput[0],
            sourceInput[1],
            sourceInput[2],
            acknowledgement,
            sourceInput[^1],
        };
        var outputLines = sourceOutput.Take(3).ToArray();
        return (
            CanonicalTranscript(inputLines, intent.RunId),
            CanonicalTranscript(outputLines, intent.RunId));
    }

    private static (byte[] Input, byte[] Output) SuccessfulCheckpointTranscripts(
        RunStoreIntent intent,
        long appliedEpoch)
    {
        var (input, output) = SuccessfulTranscripts(intent);
        var inputLines = SplitTranscript(input).ToList();
        var outputLines = SplitTranscript(output).ToList();
        var initializedEnvelope = DecodeEnvelope(outputLines[1]);
        var initialized = InitializedPayloadCodec.Decode(initializedEnvelope.Payload);
        var decisionEnvelope = DecodeEnvelope(outputLines[2]);
        var decision = DecisionPayloadCodec.Decode(decisionEnvelope.Payload);
        Assert.True(initialized.IsSuccess, initialized.Error?.Message);
        Assert.True(decision.IsSuccess, decision.Error?.Message);
        using var onlineState = JsonDocument.Parse("{}");
        var content = new CheckpointContent(
            initialized.Value!.ManifestHash,
            decision.Value!.StateAfterHash,
            decision.Value.DecisionHash,
            appliedEpoch,
            2,
            100,
            onlineState.RootElement.Clone());
        var checkpointPayload = new CheckpointPayload(
            CheckpointPayloadCodec.CurrentVersion,
            CheckpointPayloadCodec.CalculateHash(content),
            content);
        inputLines.Insert(
            inputLines.Count - 1,
            EncodeEnvelope(
                "checkpoint",
                "{}"u8.ToArray(),
                decisionEnvelope.RunId,
                decisionEnvelope.ScenarioId));
        outputLines.Add(
            EncodeEnvelope(
                "checkpoint",
                CheckpointPayloadCodec.Encode(checkpointPayload),
                decisionEnvelope.RunId,
                decisionEnvelope.ScenarioId));
        return (JoinTranscript(inputLines), JoinTranscript(outputLines));
    }

    private static TerminalRunSubmission ReplaceTranscriptEvidence(
        TerminalRunSubmission submission,
        byte[] input,
        byte[] output)
    {
        var inputSource = submission.EvidenceSources.Single(
            value => value.Role == RawRunEvidenceRole.Input);
        var outputSource = submission.EvidenceSources.Single(
            value => value.Role == RawRunEvidenceRole.Output);
        File.WriteAllBytes(inputSource.FullPath, input);
        File.WriteAllBytes(outputSource.FullPath, output);
        var replacements = new Dictionary<RawRunEvidenceRole, RawRunEvidenceSource>
        {
            [RawRunEvidenceRole.Input] = RawRunEvidenceSource.Pin(
                RawRunEvidenceRole.Input,
                inputSource.FullPath),
            [RawRunEvidenceRole.Output] = RawRunEvidenceSource.Pin(
                RawRunEvidenceRole.Output,
                outputSource.FullPath),
        };
        return submission with
        {
            EvidenceSources = submission.EvidenceSources
                .Select(value => replacements.GetValueOrDefault(value.Role, value))
                .ToArray(),
        };
    }

    private static TerminalRunSubmission ReplaceEvidence(
        TerminalRunSubmission submission,
        RawRunEvidenceRole role,
        byte[] bytes)
    {
        var source = submission.EvidenceSources.Single(value => value.Role == role);
        File.WriteAllBytes(source.FullPath, bytes);
        var replacement = RawRunEvidenceSource.Pin(role, source.FullPath);
        return submission with
        {
            EvidenceSources = submission.EvidenceSources
                .Select(value => value.Role == role ? replacement : value)
                .ToArray(),
        };
    }

    private static IReadOnlyList<byte[]> SplitTranscript(byte[] transcript) =>
        Encoding.UTF8.GetString(transcript)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(Encoding.UTF8.GetBytes)
            .ToArray();

    private static byte[] JoinTranscript(IEnumerable<byte[]> lines)
    {
        using var stream = new MemoryStream();

        foreach (var line in lines)
        {
            stream.Write(line);
            stream.WriteByte((byte)'\n');
        }

        return stream.ToArray();
    }

    private static ProtocolEnvelope DecodeEnvelope(byte[] bytes)
    {
        var decoded = ProtocolEnvelopeCodec.Decode(bytes);
        Assert.True(decoded.IsSuccess, decoded.Error?.Message);
        return decoded.Envelope!;
    }

    private static byte[] EncodeEnvelope(
        string messageType,
        byte[] payload,
        RunId? runId,
        ScenarioId? scenarioId)
    {
        Assert.True(ProtocolMessageType.TryParse(messageType, out var type));
        using var document = JsonDocument.Parse(payload);
        return CanonicalJson.Serialize(
            new ProtocolEnvelope(
                ProtocolVersion.Current,
                type!,
                document.RootElement.Clone(),
                runId,
                scenarioId));
    }

    private static byte[] CanonicalTranscript(IEnumerable<string> lines, string runId)
    {
        using var stream = new MemoryStream();

        foreach (var line in lines)
        {
            var replaced = line.Replace("run-001", runId, StringComparison.Ordinal);
            var canonical = CanonicalJson.Canonicalize(Encoding.UTF8.GetBytes(replaced));
            stream.Write(canonical);
            stream.WriteByte((byte)'\n');
        }

        return stream.ToArray();
    }

    private static byte[] ArtifactReceipt(
        string stage,
        string? artifactSha256 = null,
        string? launchCommandSha256 = null) =>
        CanonicalJson.Canonicalize(
            Encoding.UTF8.GetBytes(
                "{\"artifacts\":[{\"fileName\":\"" + FixtureArtifact.FileName +
                "\",\"lengthBytes\":" + FixtureArtifact.LengthBytes +
                ",\"role\":\"" + FixtureArtifact.Role +
                "\",\"sha256\":\"" + (artifactSha256 ?? FixtureArtifact.Sha256) +
                "\"}],\"inventorySha256\":\"" + ArtifactHash +
                "\",\"launchCommandSha256\":\"" +
                (launchCommandSha256 ?? new string('a', 64)) +
                "\",\"schemaVersion\":\"1.0.0\",\"stage\":\"" + stage + "\"}"));

    private static string EvidenceFileName(RawRunEvidenceRole role) => role switch
    {
        RawRunEvidenceRole.Input => "input.ndjson",
        RawRunEvidenceRole.Output => "output.ndjson",
        RawRunEvidenceRole.StandardError => "stderr.txt",
        RawRunEvidenceRole.ResourceSamples => "resource-samples.ndjson",
        RawRunEvidenceRole.ArtifactPreflight => "artifact-preflight.json",
        RawRunEvidenceRole.ArtifactPostflight => "artifact-postflight.json",
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };

    private static async Task<AppendOnlyRunStore> CreateTwoFailureStore(TestDirectory temp)
    {
        var store = CreateStore(temp);
        var first = Intent("b1", 1);
        var second = Intent("c1", 2);
        await store.InitializePlanAsync(PlanHash, [first, second], Denominators);
        await store.CommitAsync(Submission(temp, first, RunTerminalStatus.Failed));
        await store.CommitAsync(Submission(temp, second, RunTerminalStatus.Failed));
        Assert.True(store.VerifyPlan(PlanHash).IsValid);
        return store;
    }

    private static string[] LogFiles(TestDirectory temp, string planHash) =>
        Directory.GetFiles(
                Path.Combine(StoreRoot(temp), planHash, "logs", "terminal-events"),
                "*.json")
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static string ReadRecordKind(string line)
    {
        using var document = JsonDocument.Parse(line);
        return document.RootElement.GetProperty("recordKind").GetString()!;
    }

    private static (long, long, long, long, long) Counts(RunStoreVerificationResult value) =>
        (value.PlannedCount, value.SucceededCount, value.FailedCount, value.ExcludedCount, value.PendingCount);

    private static string FormatIssues(RunStoreVerificationResult value) =>
        string.Join("; ", value.Issues.Select(issue => $"{issue.Code}:{issue.RelativePath}"));

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "RideBound.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private sealed class ThrowOnce(RunStoreWriteBoundary target) : IRunStoreFaultInjector
    {
        private bool thrown;

        public void OnBoundary(
            RunStoreWriteBoundary boundary,
            string runId,
            RawRunEvidenceRole? evidenceRole = null)
        {
            _ = runId;
            _ = evidenceRole;

            if (!thrown && boundary == target)
            {
                thrown = true;
                throw new InjectedStoreCrashException();
            }
        }
    }

    private sealed class PauseAtBoundary(RunStoreWriteBoundary target)
        : IRunStoreFaultInjector, IDisposable
    {
        public ManualResetEventSlim Entered { get; } = new(false);

        public ManualResetEventSlim Release { get; } = new(false);

        public void OnBoundary(
            RunStoreWriteBoundary boundary,
            string runId,
            RawRunEvidenceRole? evidenceRole = null)
        {
            _ = runId;
            _ = evidenceRole;

            if (boundary == target)
            {
                Entered.Set();
                Release.Wait(TimeSpan.FromSeconds(10));
            }
        }

        public void Dispose()
        {
            Entered.Dispose();
            Release.Dispose();
        }
    }

    private sealed class InjectedStoreCrashException : Exception;
}
