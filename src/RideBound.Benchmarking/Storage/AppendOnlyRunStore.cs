using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RideBound.Benchmarking.Contracts;
using RideBound.Benchmarking.Execution;
using RideBound.Contracts.Serialization;

namespace RideBound.Benchmarking.Storage;

public sealed record AppendOnlyRunStoreOptions(
    string StoreRoot,
    string RepositoryRoot,
    long MaximumEvidenceFileBytes = 268_435_456,
    long LockRetryDelayMs = 10);

public sealed class AppendOnlyRunStore
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static readonly string ZeroHash = new('0', 64);
    private readonly AppendOnlyRunStoreOptions options;

    public AppendOnlyRunStore(AppendOnlyRunStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateOptions(options);
        this.options = options with
        {
            StoreRoot = Path.GetFullPath(options.StoreRoot),
            RepositoryRoot = Path.GetFullPath(options.RepositoryRoot),
        };
    }

    public Task InitializePlanAsync(
        string planHash,
        IReadOnlyList<RunStoreIntent> intents,
        IReadOnlyList<string> denominatorIds,
        CancellationToken cancellationToken = default) =>
        InitializePlanCoreAsync(
            planHash,
            intents,
            denominatorIds,
            supersession: null,
            cancellationToken);

    private async Task InitializePlanCoreAsync(
        string planHash,
        IReadOnlyList<RunStoreIntent> intents,
        IReadOnlyList<string> denominatorIds,
        RunStoreSupersession? supersession,
        CancellationToken cancellationToken)
    {
        ValidateHash(planHash, nameof(planHash));
        ArgumentNullException.ThrowIfNull(intents);
        ArgumentNullException.ThrowIfNull(denominatorIds);

        if (intents.Count == 0)
        {
            throw new ArgumentException("A run store plan must contain at least one intent.");
        }

        var orderedIntents = intents.OrderBy(value => value.RunId, StringComparer.Ordinal).ToArray();

        if (orderedIntents.Select(value => value.RunId)
                .Distinct(StringComparer.Ordinal).Count() != orderedIntents.Length
            || orderedIntents.Select(value => value.ExecutionOrdinal)
                .Distinct().Count() != orderedIntents.Length
            || orderedIntents.Select(GridKey)
                .Distinct(StringComparer.Ordinal).Count() != orderedIntents.Length)
        {
            throw new ArgumentException(
                "Run store intents must have unique IDs, ordinals, and semantic grid cells.");
        }

        foreach (var intent in orderedIntents)
        {
            ValidateIntent(intent, planHash);
        }

        var denominators = denominatorIds.Order(StringComparer.Ordinal).ToArray();
        ValidateDenominators(denominators);
        var manifest = new RunStorePlanManifest(
            "1.0.0",
            planHash,
            denominators,
            orderedIntents);
        var canonicalManifest = EncodeInternal(manifest);
        var canonicalSupersession = supersession is null
            ? null
            : EncodeInternal(supersession);
        Directory.CreateDirectory(options.StoreRoot);
        EnsureNotReparseDirectory(options.StoreRoot);
        var rootLockPath = Path.Combine(options.StoreRoot, ".store.lock");
        await using var rootLock = await AcquireLock(rootLockPath, cancellationToken);
        var planRoot = PlanRoot(planHash);
        var manifestPath = Path.Combine(planRoot, "plan-store.json");

        if (Directory.Exists(planRoot))
        {
            EnsureNotReparseDirectory(planRoot);
            RequireExactFile(manifestPath, canonicalManifest, "Existing plan store differs.");

            if (canonicalSupersession is not null)
            {
                RequireExactFile(
                    Path.Combine(planRoot, "supersedes.json"),
                    canonicalSupersession,
                    "Existing superseding-plan authorization differs.");
            }
            else if (File.Exists(Path.Combine(planRoot, "supersedes.json")))
            {
                throw new InvalidDataException(
                    "Superseding plan must be reopened through its authorized relation.");
            }

            return;
        }

        var staging = planRoot + ".initializing";

        if (Directory.Exists(staging))
        {
            MoveAside(
                staging,
                Path.Combine(options.StoreRoot, ".incomplete-plans", planHash));
        }

        Directory.CreateDirectory(staging);
        await WriteNew(Path.Combine(staging, "plan-store.json"), canonicalManifest);

        if (canonicalSupersession is not null)
        {
            await WriteNew(
                Path.Combine(staging, "supersedes.json"),
                canonicalSupersession,
                cancellationToken);
        }

        var intentsRoot = Path.Combine(staging, "intents");
        Directory.CreateDirectory(intentsRoot);

        foreach (var intent in orderedIntents)
        {
            await WriteNew(
                Path.Combine(intentsRoot, intent.RunId + ".json"),
                EncodeInternal(intent));
        }

        Directory.CreateDirectory(Path.Combine(staging, "runs"));
        Directory.CreateDirectory(Path.Combine(staging, ".staging"));
        Directory.CreateDirectory(Path.Combine(staging, ".incomplete"));
        Directory.CreateDirectory(Path.Combine(staging, "locks", "runs"));
        Directory.CreateDirectory(Path.Combine(staging, "logs", "terminal-events"));
        Directory.CreateDirectory(Path.Combine(staging, "logs", ".staging"));
        Directory.Move(staging, planRoot);
    }

    public async Task InitializeSupersedingPlanAsync(
        string previousPlanHash,
        string newPlanHash,
        string authorizationId,
        IReadOnlyList<RunStoreIntent> newIntents,
        IReadOnlyList<string> denominatorIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(newIntents);
        ArgumentNullException.ThrowIfNull(denominatorIds);

        if (previousPlanHash == newPlanHash
            || !IsArtifactId(authorizationId))
        {
            throw new ArgumentException(
                "Authorized rerun requires a distinct plan and stable authorization ID.");
        }

        var previous = ReadPlan(previousPlanHash);
        var previousVerification = VerifyPlan(previousPlanHash);

        if (!previousVerification.IsValid)
        {
            throw new InvalidOperationException(
                "A superseding grid cannot select from an incomplete prior plan.");
        }

        var previousGrid = previous.Runs
            .Select(GridKey)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var newGrid = newIntents
            .Select(GridKey)
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (!previousGrid.SequenceEqual(newGrid, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                "Authorized rerun must preserve the entire prior semantic grid.");
        }

        var newDenominators = denominatorIds.Order(StringComparer.Ordinal).ToArray();

        if (!previous.DenominatorIds.SequenceEqual(newDenominators))
        {
            throw new ArgumentException(
                "Authorized rerun must preserve the prior denominator set.");
        }

        var previousManifestPath = Path.Combine(PlanRoot(previousPlanHash), "plan-store.json");
        var relation = new RunStoreSupersession(
            "1.0.0",
            previousPlanHash,
            newPlanHash,
            authorizationId,
            Convert.ToHexStringLower(
                SHA256.HashData(File.ReadAllBytes(previousManifestPath))));
        await InitializePlanCoreAsync(
            newPlanHash,
            newIntents,
            denominatorIds,
            relation,
            cancellationToken);
    }

    public async Task<RunStoreCommitResult> CommitAsync(
        TerminalRunSubmission submission,
        IRunStoreFaultInjector? faultInjector = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submission);
        var plan = ReadPlan(submission.Intent.PlanHash);
        ValidateSubmission(submission, plan);
        var planRoot = PlanRoot(submission.Intent.PlanHash);
        var intentPath = Path.Combine(planRoot, "intents", submission.Intent.RunId + ".json");
        RequireExactFile(
            intentPath,
            EncodeInternal(submission.Intent),
            "Submission does not match its immutable planned intent.");
        var runLockPath = Path.Combine(
            planRoot,
            "locks",
            "runs",
            submission.Intent.RunId + ".lock");
        await using var runLock = await AcquireLock(runLockPath, cancellationToken);
        faultInjector?.OnBoundary(
            RunStoreWriteBoundary.IntentValidated,
            submission.Intent.RunId);
        var finalRoot = Path.Combine(planRoot, "runs", submission.Intent.RunId);
        var stagingRoot = Path.Combine(planRoot, ".staging", submission.Intent.RunId);

        if (Directory.Exists(finalRoot))
        {
            var existing = VerifyRunDirectory(plan, submission.Intent, finalRoot);

            if (!SubmissionMatchesExisting(submission, existing, finalRoot))
            {
                throw new InvalidOperationException(
                    "An immutable terminal run already exists with different evidence.");
            }

            return new RunStoreCommitResult(existing, finalRoot, true);
        }

        if (Directory.Exists(stagingRoot)
            && TryRecoverLoggedStaging(
                plan,
                planRoot,
                stagingRoot,
                finalRoot,
                submission.Intent))
        {
            var recovered = VerifyRunDirectory(plan, submission.Intent, finalRoot);
            return new RunStoreCommitResult(recovered, finalRoot, true);
        }

        if (Directory.Exists(stagingRoot))
        {
            MoveAside(
                stagingRoot,
                Path.Combine(planRoot, ".incomplete", submission.Intent.RunId));
        }

        Directory.CreateDirectory(stagingRoot);
        var evidence = new Dictionary<RawRunEvidenceRole, RunFileEvidence>();

        foreach (var source in submission.EvidenceSources.OrderBy(value => value.Role))
        {
            var targetName = EvidenceFileName(source.Role);
            var target = Path.Combine(stagingRoot, targetName);
            var copied = await CopyVerifiedEvidence(source, target, cancellationToken);
            evidence.Add(
                source.Role,
                new RunFileEvidence(
                    RunRelativePath(submission.Intent.RunId, targetName),
                    copied.LengthBytes,
                    copied.Sha256));
            faultInjector?.OnBoundary(
                RunStoreWriteBoundary.EvidenceCopied,
                submission.Intent.RunId,
                source.Role);
        }

        var preflightLaunch = ValidateArtifactReceipt(
            Path.Combine(stagingRoot, EvidenceFileName(RawRunEvidenceRole.ArtifactPreflight)),
            "preflight",
            submission.ArtifactPreflightSha256);
        var postflightLaunch = ValidateArtifactReceipt(
            Path.Combine(stagingRoot, EvidenceFileName(RawRunEvidenceRole.ArtifactPostflight)),
            "postflight",
            submission.ArtifactPostflightSha256);

        if (preflightLaunch != postflightLaunch)
        {
            throw new InvalidDataException(
                "Artifact receipts bind different process launch commands.");
        }

        ValidateResourceSamples(
            Path.Combine(stagingRoot, EvidenceFileName(RawRunEvidenceRole.ResourceSamples)),
            requireAtLeastOne: submission.TerminalStatus == RunTerminalStatus.Succeeded);
        var inputBytes = await File.ReadAllBytesAsync(
            Path.Combine(stagingRoot, EvidenceFileName(RawRunEvidenceRole.Input)),
            cancellationToken);
        var outputBytes = await File.ReadAllBytesAsync(
            Path.Combine(stagingRoot, EvidenceFileName(RawRunEvidenceRole.Output)),
            cancellationToken);
        var observations = ProtocolObservationIndexer.Build(
            submission.Intent,
            inputBytes,
            outputBytes,
            submission.TerminalStatus == RunTerminalStatus.Succeeded);
        var observationBytes = ProtocolObservationIndexer.Encode(observations);
        var observationPath = Path.Combine(stagingRoot, "observation-index.ndjson");
        await WriteNew(observationPath, observationBytes);
        var observationEvidence = Evidence(
            RunRelativePath(submission.Intent.RunId, "observation-index.ndjson"),
            observationBytes);
        faultInjector?.OnBoundary(
            RunStoreWriteBoundary.ObservationIndexWritten,
            submission.Intent.RunId);

        FileStream? logLock = null;

        try
        {
            TerminalLogState? logState = null;

            if (submission.TerminalStatus is RunTerminalStatus.Failed
                or RunTerminalStatus.Excluded)
            {
                logLock = await AcquireLock(
                    Path.Combine(planRoot, "locks", "terminal-events.lock"),
                    cancellationToken);
                logState = VerifyLog(planRoot, plan, requireRunDirectories: false);
            }

            FailureRecord? failure = null;
            ExclusionRecord? exclusion = null;
            string? detailFileName = null;
            var sequence = logState?.NextSequence ?? 0;

            if (submission.Failure is not null)
            {
                var sourceEvidence = evidence[submission.Failure.EvidenceRole];
                failure = CreateFailure(submission, sequence, sourceEvidence);
                detailFileName = "failure-record.json";
                await WriteNew(
                    Path.Combine(stagingRoot, detailFileName),
                    BenchmarkContractCodec.Encode(failure));
            }
            else if (submission.Exclusion is not null)
            {
                var sourceEvidence = evidence[submission.Exclusion.EvidenceRole];
                exclusion = CreateExclusion(submission, sequence, sourceEvidence);
                detailFileName = "exclusion-record.json";
                await WriteNew(
                    Path.Combine(stagingRoot, detailFileName),
                    BenchmarkContractCodec.Encode(exclusion));
            }

            faultInjector?.OnBoundary(
                RunStoreWriteBoundary.TerminalDetailWritten,
                submission.Intent.RunId);
            var runRecord = CreateRunRecord(
                submission,
                evidence,
                observationEvidence,
                failure?.FailureRecordId,
                exclusion?.ExclusionRecordId);
            await WriteNew(
                Path.Combine(stagingRoot, "run-record.json"),
                BenchmarkContractCodec.Encode(runRecord));
            faultInjector?.OnBoundary(
                RunStoreWriteBoundary.RunRecordWritten,
                submission.Intent.RunId);

            if (detailFileName is not null)
            {
                var detailBytes = await File.ReadAllBytesAsync(
                    Path.Combine(stagingRoot, detailFileName),
                    cancellationToken);
                var detailId = failure?.FailureRecordId ?? exclusion!.ExclusionRecordId;
                await AppendLogSegment(
                    planRoot,
                    logState!,
                    submission.Intent.RunId,
                    failure is null ? "exclusion" : "failure",
                    detailId,
                    Convert.ToHexStringLower(SHA256.HashData(detailBytes)),
                    cancellationToken);
                faultInjector?.OnBoundary(
                    RunStoreWriteBoundary.LogSegmentCommitted,
                    submission.Intent.RunId);
            }

            Directory.Move(stagingRoot, finalRoot);
            faultInjector?.OnBoundary(
                RunStoreWriteBoundary.RunDirectoryCommitted,
                submission.Intent.RunId);
            return new RunStoreCommitResult(runRecord, finalRoot, false);
        }
        finally
        {
            if (logLock is not null)
            {
                await logLock.DisposeAsync();
            }
        }
    }

    public RunStoreVerificationResult VerifyPlan(string planHash)
    {
        var plan = ReadPlan(planHash);
        var issues = new List<RunStoreVerificationIssue>();
        long succeeded = 0;
        long failed = 0;
        long excluded = 0;
        long pending = 0;
        var planRoot = PlanRoot(planHash);
        var intended = plan.Runs.ToDictionary(value => value.RunId, StringComparer.Ordinal);
        var runsRoot = Path.Combine(planRoot, "runs");

        foreach (var directory in Directory.GetDirectories(runsRoot))
        {
            var runId = Path.GetFileName(directory);

            if (!intended.ContainsKey(runId))
            {
                issues.Add(new RunStoreVerificationIssue(
                    "run.extra",
                    Relative(directory),
                    "Terminal directory has no planned intent."));
            }
        }

        foreach (var intent in plan.Runs)
        {
            var runRoot = Path.Combine(runsRoot, intent.RunId);

            if (!Directory.Exists(runRoot))
            {
                pending++;
                issues.Add(new RunStoreVerificationIssue(
                    "run.missing-terminal",
                    Relative(runRoot),
                    "Planned run has no terminal record."));
                continue;
            }

            try
            {
                var record = VerifyRunDirectory(plan, intent, runRoot);

                switch (record.TerminalStatus)
                {
                    case RunTerminalStatus.Succeeded:
                        succeeded++;
                        break;
                    case RunTerminalStatus.Failed:
                        failed++;
                        break;
                    case RunTerminalStatus.Excluded:
                        excluded++;
                        break;
                }
            }
            catch (Exception exception) when (
                exception is IOException
                    or UnauthorizedAccessException
                    or InvalidDataException
                    or ArgumentException
                    or InvalidOperationException
                    or CryptographicException
                    or JsonException
                    or KeyNotFoundException)
            {
                issues.Add(new RunStoreVerificationIssue(
                    "run.invalid",
                    Relative(runRoot),
                    SafeDiagnostic(exception)));
            }
        }

        try
        {
            var log = VerifyLog(planRoot, plan, requireRunDirectories: true);

            if (log.EventCount != failed + excluded)
            {
                issues.Add(new RunStoreVerificationIssue(
                    "log.denominator-mismatch",
                    "logs/terminal-events",
                    "Terminal event count differs from failed plus excluded runs."));
            }
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or InvalidDataException
                or ArgumentException
                or InvalidOperationException
                or CryptographicException
                or JsonException
                or KeyNotFoundException)
        {
            issues.Add(new RunStoreVerificationIssue(
                "log.invalid",
                "logs/terminal-events",
                SafeDiagnostic(exception)));
        }

        try
        {
            VerifySupersession(planRoot, plan, new HashSet<string>(StringComparer.Ordinal));
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or InvalidDataException
                or ArgumentException
                or JsonException
                or KeyNotFoundException)
        {
            issues.Add(new RunStoreVerificationIssue(
                "rerun.authorization-invalid",
                "supersedes.json",
                SafeDiagnostic(exception)));
        }

        return new RunStoreVerificationResult(
            plan.Runs.Count,
            succeeded,
            failed,
            excluded,
            pending,
            issues);
    }

    private void VerifySupersession(
        string planRoot,
        RunStorePlanManifest plan,
        ISet<string> visitedPlans)
    {
        if (!visitedPlans.Add(plan.PlanHash))
        {
            throw new InvalidDataException("Superseding-plan authorization contains a cycle.");
        }

        var path = Path.Combine(planRoot, "supersedes.json");

        if (!File.Exists(path))
        {
            return;
        }

        var relation = DecodeInternal<RunStoreSupersession>(RequireCanonical(path));
        var previousManifest = Path.Combine(
            PlanRoot(relation.PreviousPlanHash),
            "plan-store.json");

        if (relation.SchemaVersion != "1.0.0"
            || relation.NewPlanHash != plan.PlanHash
            || relation.PreviousPlanHash == plan.PlanHash
            || !IsArtifactId(relation.AuthorizationId)
            || !File.Exists(previousManifest)
            || Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(previousManifest)))
                != relation.PreviousPlanManifestSha256)
        {
            throw new InvalidDataException("Superseding-plan authorization is invalid.");
        }

        var previous = ReadPlan(relation.PreviousPlanHash);

        if (!plan.DenominatorIds.SequenceEqual(previous.DenominatorIds)
            || !plan.Runs.Select(GridKey).Order(StringComparer.Ordinal).SequenceEqual(
                previous.Runs.Select(GridKey).Order(StringComparer.Ordinal),
                StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "Superseding plan changed its prior denominator or semantic grid.");
        }

        VerifyCompletePlanEvidence(previous);
        VerifySupersession(
            PlanRoot(previous.PlanHash),
            previous,
            visitedPlans);
    }

    private void VerifyCompletePlanEvidence(RunStorePlanManifest plan)
    {
        var planRoot = PlanRoot(plan.PlanHash);
        var runsRoot = Path.Combine(planRoot, "runs");
        var plannedIds = plan.Runs.Select(value => value.RunId)
            .ToHashSet(StringComparer.Ordinal);
        var actualIds = Directory.GetDirectories(runsRoot)
            .Select(Path.GetFileName)
            .ToHashSet(StringComparer.Ordinal);

        if (!actualIds.SetEquals(plannedIds))
        {
            throw new InvalidDataException(
                "Superseded plan does not have exactly one terminal directory per intent.");
        }

        long terminalEventCount = 0;

        foreach (var intent in plan.Runs)
        {
            var record = VerifyRunDirectory(
                plan,
                intent,
                Path.Combine(runsRoot, intent.RunId));

            if (record.TerminalStatus is RunTerminalStatus.Failed or RunTerminalStatus.Excluded)
            {
                terminalEventCount++;
            }
        }

        var log = VerifyLog(planRoot, plan, requireRunDirectories: true);

        if (log.EventCount != terminalEventCount)
        {
            throw new InvalidDataException(
                "Superseded plan terminal log differs from its immutable runs.");
        }
    }

    public async Task<IReadOnlyList<RunStoreCommitResult>> SealIncompleteRunsAsync(
        string planHash,
        string observedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var plan = ReadPlan(planHash);
        var results = new List<RunStoreCommitResult>();

        foreach (var intent in plan.Runs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (Directory.Exists(Path.Combine(PlanRoot(planHash), "runs", intent.RunId)))
            {
                continue;
            }

            var evidenceSources = await CreateRecoveryEvidence(
                intent,
                cancellationToken);
            var submission = new TerminalRunSubmission(
                intent,
                RunTerminalStatus.Failed,
                observedAtUtc,
                observedAtUtc,
                0,
                0,
                0,
                0,
                ZeroHash,
                ZeroHash,
                evidenceSources,
                Failure: new RunFailureInput(
                    "harness.persistence-incomplete",
                    "persistence",
                    0,
                    "append-only-run-store",
                    RawRunEvidenceRole.StandardError,
                    "Harness stopped before immutable terminal evidence was committed.",
                    plan.DenominatorIds));
            try
            {
                results.Add(
                    await CommitAsync(
                        submission,
                        cancellationToken: cancellationToken));
            }
            catch (InvalidOperationException) when (
                Directory.Exists(
                    Path.Combine(PlanRoot(planHash), "runs", intent.RunId)))
            {
                _ = VerifyRunDirectory(
                    plan,
                    intent,
                    Path.Combine(PlanRoot(planHash), "runs", intent.RunId));
            }
        }

        return results;
    }

    private static FailureRecord CreateFailure(
        TerminalRunSubmission submission,
        long sequence,
        RunFileEvidence evidence)
    {
        var input = submission.Failure!;
        var id = CalculateDetailId(
            "RideBound.Wp6.FailureRecord.v1\0",
            submission.Intent.RunId,
            input.Code,
            input.Stage,
            evidence.Sha256);
        return new FailureRecord(
            BenchmarkContractVersions.V1_0_2,
            sequence,
            id,
            submission.Intent.RunId,
            submission.Intent.PlanHash,
            submission.Intent.ScenarioHash,
            submission.Intent.ArmId,
            submission.Intent.RepeatIndex,
            submission.Intent.AttemptIndex,
            input.Code,
            input.Stage,
            input.FirstObservedMonotonicOffsetMs,
            input.SourceComponent,
            evidence.RelativePath,
            evidence.Sha256,
            input.SafeMessage,
            "none",
            input.AffectedDenominatorIds);
    }

    private static ExclusionRecord CreateExclusion(
        TerminalRunSubmission submission,
        long sequence,
        RunFileEvidence evidence)
    {
        var input = submission.Exclusion!;
        var id = CalculateDetailId(
            "RideBound.Wp6.ExclusionRecord.v1\0",
            submission.Intent.RunId,
            input.RuleId,
            input.SubjectKind,
            evidence.Sha256);
        return new ExclusionRecord(
            BenchmarkContractVersions.V1,
            sequence,
            id,
            input.RuleId,
            input.RuleVersion,
            input.RuleSetHash,
            input.Stage,
            input.SubjectKind,
            input.SubjectId,
            input.BeforeOutcome,
            evidence.RelativePath,
            evidence.Sha256,
            input.RetainedDenominatorIds,
            input.SafeReason,
            submission.Intent.ScenarioHash,
            submission.Intent.ArmId,
            submission.Intent.RepeatIndex);
    }

    private static RunRecord CreateRunRecord(
        TerminalRunSubmission submission,
        IReadOnlyDictionary<RawRunEvidenceRole, RunFileEvidence> evidence,
        RunFileEvidence observationEvidence,
        string? failureRecordId,
        string? exclusionRecordId)
    {
        var intent = submission.Intent;
        return new RunRecord(
            BenchmarkContractVersions.V1,
            intent.RunId,
            intent.PlanHash,
            intent.ScenarioHash,
            intent.ArmId,
            intent.RepeatIndex,
            intent.AttemptIndex,
            intent.PolicyConfigurationSha256,
            intent.EffectiveConfigurationSha256,
            intent.ComponentSeedHex,
            intent.RunnerArtifactSha256,
            intent.HarnessSourceSha256,
            intent.ExecutionOrdinal,
            intent.Warmup,
            submission.TerminalStatus,
            submission.StartedAtUtc,
            submission.FinishedAtUtc,
            submission.WallTimeMs,
            submission.CpuTimeMs,
            submission.PeakWorkingSetBytes,
            submission.SpawnedProcessCount,
            submission.ArtifactPreflightSha256,
            submission.ArtifactPostflightSha256,
            evidence[RawRunEvidenceRole.Input],
            evidence[RawRunEvidenceRole.Output],
            evidence[RawRunEvidenceRole.StandardError],
            evidence[RawRunEvidenceRole.ResourceSamples],
            observationEvidence,
            submission.ExitCode,
            submission.LastEpochId,
            submission.LastEventHash,
            submission.LastDecisionHash,
            submission.LastCheckpointHash,
            failureRecordId,
            exclusionRecordId);
    }

    private RunRecord VerifyRunDirectory(
        RunStorePlanManifest plan,
        RunStoreIntent intent,
        string runRoot) =>
        VerifyPortableRunDirectory(intent, plan.DenominatorIds, runRoot);

    internal static RunRecord VerifyPortableRunDirectory(
        RunStoreIntent intent,
        IReadOnlyList<string> denominatorIds,
        string runRoot)
    {
        var expectedNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "run-record.json",
            "input.ndjson",
            "output.ndjson",
            "stderr.txt",
            "resource-samples.ndjson",
            "artifact-preflight.json",
            "artifact-postflight.json",
            "observation-index.ndjson",
        };
        var record = DecodeContract<RunRecord>(Path.Combine(runRoot, "run-record.json"));

        var rootInfo = new DirectoryInfo(runRoot);

        if ((rootInfo.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("Terminal run directory is a reparse point.");
        }

        if (!IntentMatches(record, intent))
        {
            throw new InvalidDataException("Terminal record is cross-linked to another intent.");
        }

        VerifyEvidence(runRoot, record.InputFile, "input.ndjson");
        VerifyEvidence(runRoot, record.OutputFile, "output.ndjson");
        VerifyEvidence(runRoot, record.StderrFile, "stderr.txt");
        VerifyEvidence(runRoot, record.ResourceSamplesFile, "resource-samples.ndjson");
        VerifyEvidence(runRoot, record.ObservationIndexFile, "observation-index.ndjson");
        ValidateResourceSamples(
            Path.Combine(runRoot, "resource-samples.ndjson"),
            requireAtLeastOne: record.TerminalStatus == RunTerminalStatus.Succeeded);
        var preflightLaunch = ValidateArtifactReceipt(
            Path.Combine(runRoot, "artifact-preflight.json"),
            "preflight",
            record.ArtifactPreflightSha256);
        var postflightLaunch = ValidateArtifactReceipt(
            Path.Combine(runRoot, "artifact-postflight.json"),
            "postflight",
            record.ArtifactPostflightSha256);

        if (preflightLaunch != postflightLaunch)
        {
            throw new InvalidDataException(
                "Artifact receipts bind different process launch commands.");
        }

        VerifyObservationIndex(intent, record, runRoot);

        if (record.TerminalStatus == RunTerminalStatus.Failed)
        {
            expectedNames.Add("failure-record.json");
            var failure = DecodeContract<FailureRecord>(Path.Combine(runRoot, "failure-record.json"));

            if (failure.FailureRecordId != record.FailureRecordId
                || failure.RunId != record.RunId
                || !failure.AffectedDenominatorIds.SequenceEqual(denominatorIds))
            {
                throw new InvalidDataException("Failure record identity/denominators mismatch.");
            }

            VerifyDetailEvidence(runRoot, failure.EvidenceRelativePath, failure.EvidenceSha256);
            RequireSafeText(failure.SafeMessage, nameof(failure.SafeMessage));
        }
        else if (record.TerminalStatus == RunTerminalStatus.Excluded)
        {
            expectedNames.Add("exclusion-record.json");
            var exclusion = DecodeContract<ExclusionRecord>(
                Path.Combine(runRoot, "exclusion-record.json"));

            if (exclusion.ExclusionRecordId != record.ExclusionRecordId
                || exclusion.BeforeOutcome is false
                || exclusion.ScenarioHash != record.ScenarioHash
                || exclusion.ArmId != record.ArmId
                || exclusion.RepeatIndex != record.RepeatIndex
                || !exclusion.RetainedDenominatorIds.SequenceEqual(denominatorIds))
            {
                throw new InvalidDataException("Exclusion record identity/denominators mismatch.");
            }

            VerifyDetailEvidence(runRoot, exclusion.EvidenceRelativePath, exclusion.EvidenceSha256);
            RequireSafeText(exclusion.SafeReason, nameof(exclusion.SafeReason));
        }

        else if (record.ArtifactPreflightSha256 != intent.ExpectedArtifactInventorySha256
            || record.ArtifactPostflightSha256 != intent.ExpectedArtifactInventorySha256)
        {
            throw new InvalidDataException(
                "Succeeded run artifact identity differs from its planned inventory.");
        }

        var actualNames = Directory.GetFiles(runRoot)
            .Select(Path.GetFileName)
            .ToHashSet(StringComparer.Ordinal);

        if (!actualNames.SetEquals(expectedNames)
            || Directory.GetDirectories(runRoot).Length != 0
            || record.TerminalStatus != RunTerminalStatus.Succeeded
                && actualNames.Any(value => value!.Contains("metric", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException("Run directory has missing, extra, or forbidden metric files.");
        }

        return record;
    }

    private static void VerifyObservationIndex(
        RunStoreIntent intent,
        RunRecord record,
        string runRoot)
    {
        var indexPath = Path.Combine(runRoot, "observation-index.ndjson");
        var actual = File.ReadAllBytes(indexPath);
        var expectedRows = ProtocolObservationIndexer.Build(
            intent,
            File.ReadAllBytes(Path.Combine(runRoot, "input.ndjson")),
            File.ReadAllBytes(Path.Combine(runRoot, "output.ndjson")),
            record.TerminalStatus == RunTerminalStatus.Succeeded);
        var expected = ProtocolObservationIndexer.Encode(expectedRows);

        if (!actual.SequenceEqual(expected))
        {
            throw new InvalidDataException("Observation index differs from raw transcripts.");
        }
    }

    private TerminalLogState VerifyLog(
        string planRoot,
        RunStorePlanManifest plan,
        bool requireRunDirectories)
    {
        var logRoot = Path.Combine(planRoot, "logs", "terminal-events");
        var files = Directory.GetFiles(logRoot, "*.json")
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (Directory.GetFiles(logRoot).Length != files.Length
            || Directory.GetDirectories(logRoot).Length != 0)
        {
            throw new InvalidDataException("Terminal log contains an unregistered artifact.");
        }
        long expectedSequence = 1;
        var previousHash = ZeroHash;
        var seenRuns = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in files)
        {
            var canonical = RequireCanonical(file);
            var entry = DecodeInternal<TerminalLogEntry>(canonical);
            var entryHash = Convert.ToHexStringLower(SHA256.HashData(canonical));
            var expectedName = $"{entry.RecordSequence:D20}-{entryHash}.json";

            if (entry.SchemaVersion != "1.0.0"
                || entry.RecordSequence != expectedSequence
                || entry.PreviousEntrySha256 != previousHash
                || !string.Equals(Path.GetFileName(file), expectedName, StringComparison.Ordinal)
                || !seenRuns.Add(entry.RunId)
                || !plan.Runs.Any(value => value.RunId == entry.RunId)
                || entry.RecordKind is not ("failure" or "exclusion"))
            {
                throw new InvalidDataException("Terminal log sequence/hash chain is invalid.");
            }

            if (requireRunDirectories)
            {
                VerifyLogTarget(planRoot, entry);
            }

            expectedSequence++;
            previousHash = entryHash;
        }

        return new TerminalLogState(expectedSequence, previousHash, files.Length);
    }

    private static void VerifyLogTarget(string planRoot, TerminalLogEntry entry)
    {
        var detailName = entry.RecordKind == "failure"
            ? "failure-record.json"
            : "exclusion-record.json";
        var detailPath = Path.Combine(planRoot, "runs", entry.RunId, detailName);

        if (!File.Exists(detailPath))
        {
            throw new InvalidDataException("Terminal log points to a missing run detail.");
        }

        var bytes = RequireCanonical(detailPath);

        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != entry.RecordSha256)
        {
            throw new InvalidDataException("Terminal log record hash mismatch.");
        }

        using var document = JsonDocument.Parse(bytes);
        var recordIdName = entry.RecordKind == "failure"
            ? "failureRecordId"
            : "exclusionRecordId";

        if (document.RootElement.GetProperty(recordIdName).GetString() != entry.RecordId)
        {
            throw new InvalidDataException("Terminal log record ID mismatch.");
        }

        if (document.RootElement.GetProperty("recordSequence").GetInt64()
                != entry.RecordSequence
            || entry.RecordKind == "failure"
                && document.RootElement.GetProperty("runId").GetString() != entry.RunId)
        {
            throw new InvalidDataException("Terminal log record sequence/run mismatch.");
        }
    }

    private static async Task AppendLogSegment(
        string planRoot,
        TerminalLogState state,
        string runId,
        string recordKind,
        string recordId,
        string recordSha256,
        CancellationToken cancellationToken)
    {
        var entry = new TerminalLogEntry(
            "1.0.0",
            state.NextSequence,
            state.PreviousEntrySha256,
            recordKind,
            recordId,
            runId,
            recordSha256);
        var bytes = EncodeInternal(entry);
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var logRoot = Path.Combine(planRoot, "logs", "terminal-events");
        var final = Path.Combine(logRoot, $"{state.NextSequence:D20}-{hash}.json");
        var pending = Path.Combine(
            planRoot,
            "logs",
            ".staging",
            $"pending-{state.NextSequence:D20}-{runId}.json");

        if (File.Exists(pending))
        {
            MoveAside(pending, Path.Combine(planRoot, ".incomplete", "log-segments"));
        }

        await WriteNew(pending, bytes, cancellationToken);
        File.Move(pending, final);
    }

    private bool TryRecoverLoggedStaging(
        RunStorePlanManifest plan,
        string planRoot,
        string stagingRoot,
        string finalRoot,
        RunStoreIntent intent)
    {
        var runRecordPath = Path.Combine(stagingRoot, "run-record.json");

        if (!File.Exists(runRecordPath))
        {
            return false;
        }

        var record = DecodeContract<RunRecord>(runRecordPath);

        if (!IntentMatches(record, intent))
        {
            return false;
        }

        if (record.TerminalStatus == RunTerminalStatus.Succeeded)
        {
            _ = VerifyRunDirectory(plan, intent, stagingRoot);
            Directory.Move(stagingRoot, finalRoot);
            return true;
        }

        var detailName = record.TerminalStatus == RunTerminalStatus.Failed
            ? "failure-record.json"
            : "exclusion-record.json";
        var detailPath = Path.Combine(stagingRoot, detailName);

        if (!File.Exists(detailPath))
        {
            return false;
        }

        var detailHash = Convert.ToHexStringLower(SHA256.HashData(RequireCanonical(detailPath)));
        _ = VerifyLog(planRoot, plan, requireRunDirectories: false);
        var entries = Directory.GetFiles(
            Path.Combine(planRoot, "logs", "terminal-events"),
            "*.json");
        var logged = entries.Any(
            path =>
            {
                var entry = DecodeInternal<TerminalLogEntry>(RequireCanonical(path));
                return entry.RunId == intent.RunId && entry.RecordSha256 == detailHash;
            });

        if (!logged)
        {
            return false;
        }

        _ = VerifyRunDirectory(plan, intent, stagingRoot);
        Directory.Move(stagingRoot, finalRoot);
        return true;
    }

    private async Task<CopiedEvidence> CopyVerifiedEvidence(
        RawRunEvidenceSource source,
        string target,
        CancellationToken cancellationToken)
    {
        if (!Path.IsPathRooted(source.FullPath)
            || source.ExpectedLengthBytes < 0
            || source.ExpectedLengthBytes > options.MaximumEvidenceFileBytes)
        {
            throw new ArgumentException("Raw evidence source declaration is invalid.");
        }

        var sourcePath = Path.GetFullPath(source.FullPath);

        if (IsUnder(options.StoreRoot, sourcePath))
        {
            throw new ArgumentException("Raw evidence source must be outside the run store.");
        }

        var info = new FileInfo(sourcePath);

        if (!info.Exists || (info.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("Raw evidence source is missing or is a reparse point.");
        }

        await using var input = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        await using var output = new FileStream(
            target,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81_920];
        long length = 0;

        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken);

            if (read == 0)
            {
                break;
            }

            length = checked(length + read);

            if (length > options.MaximumEvidenceFileBytes)
            {
                throw new IOException("Raw evidence crossed the configured file limit.");
            }

            hash.AppendData(buffer.AsSpan(0, read));
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        await output.FlushAsync(cancellationToken);
        var sha256 = Convert.ToHexStringLower(hash.GetHashAndReset());

        if (length != source.ExpectedLengthBytes
            || sha256 != source.ExpectedSha256)
        {
            throw new InvalidDataException("Raw evidence changed before immutable ingestion.");
        }

        return new CopiedEvidence(length, sha256);
    }

    private async Task<IReadOnlyList<RawRunEvidenceSource>> CreateRecoveryEvidence(
        RunStoreIntent intent,
        CancellationToken cancellationToken)
    {
        var siblingRoot = Path.Combine(
            Path.GetDirectoryName(options.StoreRoot)!,
            ".ridebound-wp6-recovery-sources",
            intent.PlanHash,
            intent.RunId);
        var ordinal = 1;
        string root;

        do
        {
            root = Path.Combine(
                siblingRoot,
                ordinal.ToString("D6", CultureInfo.InvariantCulture));
            ordinal++;
        }
        while (Directory.Exists(root));

        Directory.CreateDirectory(root);
        await WriteNew(Path.Combine(root, "input.ndjson"), [], cancellationToken);
        await WriteNew(Path.Combine(root, "output.ndjson"), [], cancellationToken);
        await WriteNew(Path.Combine(root, "stderr.txt"), [], cancellationToken);
        await WriteNew(Path.Combine(root, "resource-samples.ndjson"), [], cancellationToken);
        await WriteNew(
            Path.Combine(root, "artifact-preflight.json"),
            RecoveryArtifactReceipt("preflight"),
            cancellationToken);
        await WriteNew(
            Path.Combine(root, "artifact-postflight.json"),
            RecoveryArtifactReceipt("postflight"),
            cancellationToken);
        return Enum.GetValues<RawRunEvidenceRole>()
            .Select(
                role => RawRunEvidenceSource.Pin(
                    role,
                    Path.Combine(root, EvidenceFileName(role))))
            .ToArray();
    }

    private static byte[] RecoveryArtifactReceipt(string stage) =>
        EncodeInternal(
            new
            {
                SchemaVersion = "1.0.0",
                Stage = stage,
                Status = "artifact-unavailable",
                InventorySha256 = ZeroHash,
                LaunchCommandSha256 = ZeroHash,
                Artifacts = Array.Empty<object>(),
            });

    private void ValidateSubmission(
        TerminalRunSubmission submission,
        RunStorePlanManifest plan)
    {
        ValidateIntent(submission.Intent, plan.PlanHash);
        var roles = submission.EvidenceSources.Select(value => value.Role).ToArray();

        if (roles.Length != Enum.GetValues<RawRunEvidenceRole>().Length
            || roles.Distinct().Count() != roles.Length
            || Enum.GetValues<RawRunEvidenceRole>().Except(roles).Any())
        {
            throw new ArgumentException("Terminal submission requires every raw evidence role exactly once.");
        }

        foreach (var source in submission.EvidenceSources)
        {
            if (!Path.IsPathRooted(source.FullPath)
                || source.ExpectedLengthBytes < 0
                || source.ExpectedLengthBytes > options.MaximumEvidenceFileBytes
                || IsUnder(options.StoreRoot, Path.GetFullPath(source.FullPath)))
            {
                throw new ArgumentException("Raw evidence source declaration is invalid.");
            }

            ValidateHash(source.ExpectedSha256, nameof(source.ExpectedSha256));
        }

        var statusShapeValid = submission.TerminalStatus switch
        {
            RunTerminalStatus.Succeeded => submission.Failure is null
                && submission.Exclusion is null
                && submission.ExitCode == 0,
            RunTerminalStatus.Failed => submission.Failure is not null
                && submission.Exclusion is null,
            RunTerminalStatus.Excluded => submission.Failure is null
                && submission.Exclusion is not null
                && submission.Exclusion.BeforeOutcome,
            _ => false,
        };

        if (!statusShapeValid)
        {
            throw new ArgumentException("Terminal status and detail shape are inconsistent.");
        }

        if (submission.Failure is not null)
        {
            RequireSafeText(submission.Failure.SafeMessage, nameof(submission.Failure.SafeMessage));

            if (!submission.Failure.AffectedDenominatorIds.SequenceEqual(plan.DenominatorIds))
            {
                throw new ArgumentException("Failure denominator set differs from the plan.");
            }
        }

        if (submission.Exclusion is not null)
        {
            RequireSafeText(submission.Exclusion.SafeReason, nameof(submission.Exclusion.SafeReason));

            if (!submission.Exclusion.RetainedDenominatorIds.SequenceEqual(plan.DenominatorIds))
            {
                throw new ArgumentException("Exclusion denominator set differs from the plan.");
            }
        }

        ValidateHash(submission.ArtifactPreflightSha256, nameof(submission.ArtifactPreflightSha256));
        ValidateHash(submission.ArtifactPostflightSha256, nameof(submission.ArtifactPostflightSha256));

        if (submission.WallTimeMs < 0
            || submission.CpuTimeMs < 0
            || submission.PeakWorkingSetBytes < 0
            || submission.SpawnedProcessCount < 0
            || !IsCanonicalUtc(submission.StartedAtUtc)
            || !IsCanonicalUtc(submission.FinishedAtUtc)
            || DateTimeOffset.Parse(
                    submission.FinishedAtUtc,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal)
                < DateTimeOffset.Parse(
                    submission.StartedAtUtc,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal)
            || submission.TerminalStatus == RunTerminalStatus.Succeeded
                && submission.SpawnedProcessCount == 0)
        {
            throw new ArgumentException("Terminal timing/resource fields are invalid.");
        }

        var evidence = submission.EvidenceSources.ToDictionary(
            value => value.Role,
            value => new RunFileEvidence(
                RunRelativePath(submission.Intent.RunId, EvidenceFileName(value.Role)),
                value.ExpectedLengthBytes,
                value.ExpectedSha256));
        FailureRecord? failure = null;
        ExclusionRecord? exclusion = null;

        if (submission.Failure is not null)
        {
            if (submission.Failure.FirstObservedMonotonicOffsetMs > submission.WallTimeMs)
            {
                throw new ArgumentException(
                    "Failure observation offset exceeds the terminal wall clock.");
            }

            failure = CreateFailure(
                submission,
                sequence: 1,
                evidence[submission.Failure.EvidenceRole]);
            _ = BenchmarkContractCodec.Encode(failure);
        }
        else if (submission.Exclusion is not null)
        {
            exclusion = CreateExclusion(
                submission,
                sequence: 1,
                evidence[submission.Exclusion.EvidenceRole]);
            _ = BenchmarkContractCodec.Encode(exclusion);
        }

        var emptyObservation = new RunFileEvidence(
            RunRelativePath(submission.Intent.RunId, "observation-index.ndjson"),
            0,
            Convert.ToHexStringLower(SHA256.HashData([])));
        _ = BenchmarkContractCodec.Encode(
            CreateRunRecord(
                submission,
                evidence,
                emptyObservation,
                failure?.FailureRecordId,
                exclusion?.ExclusionRecordId));
    }

    private static bool SubmissionMatchesExisting(
        TerminalRunSubmission submission,
        RunRecord record,
        string runRoot)
    {
        if (record.TerminalStatus != submission.TerminalStatus
            || record.StartedAtUtc != submission.StartedAtUtc
            || record.FinishedAtUtc != submission.FinishedAtUtc
            || record.WallTimeMs != submission.WallTimeMs
            || record.CpuTimeMs != submission.CpuTimeMs
            || record.PeakWorkingSetBytes != submission.PeakWorkingSetBytes
            || record.SpawnedProcessCount != submission.SpawnedProcessCount
            || record.ArtifactPreflightSha256 != submission.ArtifactPreflightSha256
            || record.ArtifactPostflightSha256 != submission.ArtifactPostflightSha256
            || record.ExitCode != submission.ExitCode
            || record.LastEpochId != submission.LastEpochId
            || record.LastEventHash != submission.LastEventHash
            || record.LastDecisionHash != submission.LastDecisionHash
            || record.LastCheckpointHash != submission.LastCheckpointHash)
        {
            return false;
        }

        foreach (var source in submission.EvidenceSources)
        {
            var path = Path.Combine(runRoot, EvidenceFileName(source.Role));
            var info = new FileInfo(path);

            if (!info.Exists
                || info.Length != source.ExpectedLengthBytes
                || Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)))
                    != source.ExpectedSha256)
            {
                return false;
            }
        }

        if (submission.Failure is not null)
        {
            var existing = DecodeContract<FailureRecord>(
                Path.Combine(runRoot, "failure-record.json"));
            return existing.Code == submission.Failure.Code
                && existing.Stage == submission.Failure.Stage
                && existing.FirstObservedMonotonicOffsetMs
                    == submission.Failure.FirstObservedMonotonicOffsetMs
                && existing.SourceComponent == submission.Failure.SourceComponent
                && existing.SafeMessage == submission.Failure.SafeMessage
                && existing.AffectedDenominatorIds.SequenceEqual(
                    submission.Failure.AffectedDenominatorIds);
        }

        if (submission.Exclusion is not null)
        {
            var existing = DecodeContract<ExclusionRecord>(
                Path.Combine(runRoot, "exclusion-record.json"));
            return existing.RuleId == submission.Exclusion.RuleId
                && existing.RuleVersion == submission.Exclusion.RuleVersion
                && existing.RuleSetHash == submission.Exclusion.RuleSetHash
                && existing.Stage == submission.Exclusion.Stage
                && existing.SubjectKind == submission.Exclusion.SubjectKind
                && existing.SubjectId == submission.Exclusion.SubjectId
                && existing.BeforeOutcome == submission.Exclusion.BeforeOutcome
                && existing.SafeReason == submission.Exclusion.SafeReason
                && existing.RetainedDenominatorIds.SequenceEqual(
                    submission.Exclusion.RetainedDenominatorIds);
        }

        return true;
    }

    private static void ValidateIntent(RunStoreIntent intent, string planHash)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ValidateHash(intent.ExpectedArtifactInventorySha256, nameof(intent.ExpectedArtifactInventorySha256));
        var expectedRunId = BenchmarkIdentity.CalculateRun(
            planHash,
            intent.ScenarioHash,
            intent.ArmId,
            intent.RepeatIndex,
            intent.AttemptIndex);

        if (intent.PlanHash != planHash || intent.RunId != expectedRunId)
        {
            throw new ArgumentException("Run intent identity does not match its plan address.");
        }

        var empty = new RunFileEvidence("runs/x/empty", 0, ZeroHash);
        var probe = new RunRecord(
            BenchmarkContractVersions.V1,
            intent.RunId,
            intent.PlanHash,
            intent.ScenarioHash,
            intent.ArmId,
            intent.RepeatIndex,
            intent.AttemptIndex,
            intent.PolicyConfigurationSha256,
            intent.EffectiveConfigurationSha256,
            intent.ComponentSeedHex,
            intent.RunnerArtifactSha256,
            intent.HarnessSourceSha256,
            intent.ExecutionOrdinal,
            intent.Warmup,
            RunTerminalStatus.Succeeded,
            "2000-01-01T00:00:00Z",
            "2000-01-01T00:00:00Z",
            0,
            0,
            0,
            1,
            intent.ExpectedArtifactInventorySha256,
            intent.ExpectedArtifactInventorySha256,
            empty,
            empty,
            empty,
            empty,
            empty,
            ExitCode: 0);
        _ = BenchmarkContractCodec.Encode(probe);
    }

    private static void ValidateDenominators(IReadOnlyList<string> values)
    {
        if (values.Count == 0
            || values.Distinct(StringComparer.Ordinal).Count() != values.Count
            || values.Any(value => !IsArtifactId(value)))
        {
            throw new ArgumentException("Denominator IDs must be a non-empty sorted unique artifact set.");
        }
    }

    private static void RequireSafeText(string value, string parameter)
    {
        var forbidden = new[]
        {
            "token", "secret", "password", "bearer", "authorization",
            "requestid", "vehicleid", "nodeid", "subject", "latitude",
            "longitude", "address",
        };

        if (string.IsNullOrWhiteSpace(value)
            || Encoding.UTF8.GetByteCount(value) > 512
            || value.Any(char.IsControl)
            || value.Contains('/')
            || value.Contains('\\')
            || value.Contains('@')
            || forbidden.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("Terminal safe text contains private or unsafe material.", parameter);
        }
    }

    private static string ValidateArtifactReceipt(string path, string stage, string expectedHash)
    {
        var bytes = RequireCanonical(path);
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;
        var allowedRoot = new HashSet<string>(StringComparer.Ordinal)
        {
            "schemaVersion",
            "stage",
            "status",
            "inventorySha256",
            "launchCommandSha256",
            "artifacts",
        };

        if (root.ValueKind != JsonValueKind.Object
            || root.EnumerateObject().Any(property => !allowedRoot.Contains(property.Name))
            || root.GetProperty("schemaVersion").GetString() != "1.0.0"
            || root.GetProperty("stage").GetString() != stage
            || root.GetProperty("inventorySha256").GetString() != expectedHash)
        {
            throw new InvalidDataException("Artifact receipt does not match terminal identity.");
        }

        ValidateHash(
            root.GetProperty("inventorySha256").GetString()!,
            "inventorySha256");
        ValidateHash(
            root.GetProperty("launchCommandSha256").GetString()!,
            "launchCommandSha256");
        var launchCommandSha256 = root.GetProperty("launchCommandSha256").GetString()!;
        var artifacts = root.GetProperty("artifacts");

        if (artifacts.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("Artifact receipt inventory must be an array.");
        }

        string? previousRole = null;
        var entries = new List<ProcessArtifactInventoryEntry>();

        foreach (var artifact in artifacts.EnumerateArray())
        {
            if (artifact.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("Artifact receipt entry is malformed.");
            }

            var properties = artifact.EnumerateObject().Select(value => value.Name).ToArray();

            if (!properties.Order(StringComparer.Ordinal).SequenceEqual(
                    new[] { "fileName", "lengthBytes", "role", "sha256" })
                || artifact.GetProperty("lengthBytes").GetInt64() < 0
                || Path.GetFileName(artifact.GetProperty("fileName").GetString())
                    != artifact.GetProperty("fileName").GetString())
            {
                throw new InvalidDataException("Artifact receipt entry is malformed.");
            }

            var role = artifact.GetProperty("role").GetString()!;
            ValidateHash(artifact.GetProperty("sha256").GetString()!, "artifact.sha256");

            if (string.IsNullOrWhiteSpace(role)
                || previousRole is not null
                    && StringComparer.Ordinal.Compare(previousRole, role) >= 0)
            {
                throw new InvalidDataException("Artifact receipt roles are not unique/sorted.");
            }

            previousRole = role;
            entries.Add(
                new ProcessArtifactInventoryEntry(
                    role,
                    artifact.GetProperty("fileName").GetString()!,
                    artifact.GetProperty("lengthBytes").GetInt64(),
                    artifact.GetProperty("sha256").GetString()!));
        }

        if (root.TryGetProperty("status", out var status))
        {
            if (status.GetString() != "artifact-unavailable"
                || expectedHash != ZeroHash
                || launchCommandSha256 != ZeroHash
                || entries.Count != 0)
            {
                throw new InvalidDataException(
                    "Unavailable artifact receipt must use the exact empty zero identity.");
            }

            return launchCommandSha256;
        }

        if (entries.Count == 0
            || ProcessArtifactIdentity.Calculate(entries) != expectedHash)
        {
            throw new InvalidDataException(
                "Artifact receipt inventory hash does not derive from its exact entries.");
        }

        return launchCommandSha256;
    }

    private static void VerifyEvidence(
        string runRoot,
        RunFileEvidence evidence,
        string expectedFileName)
    {
        var runId = Path.GetFileName(runRoot);

        if (evidence.RelativePath != RunRelativePath(runId, expectedFileName))
        {
            throw new InvalidDataException("Run evidence role/path mismatch.");
        }

        var path = Path.Combine(runRoot, expectedFileName);
        var info = new FileInfo(path);

        if (!info.Exists
            || (info.Attributes & FileAttributes.ReparsePoint) != 0
            || info.Length != evidence.LengthBytes
            || Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))) != evidence.Sha256)
        {
            throw new InvalidDataException("Run evidence length/hash mismatch.");
        }
    }

    private static void VerifyDetailEvidence(
        string runRoot,
        string relativePath,
        string expectedSha256)
    {
        var runId = Path.GetFileName(runRoot);
        var fileName = Path.GetFileName(relativePath);
        var allowed = Enum.GetValues<RawRunEvidenceRole>()
            .Select(role => RunRelativePath(runId, EvidenceFileName(role)));

        if (!allowed.Contains(relativePath, StringComparer.Ordinal))
        {
            throw new InvalidDataException("Terminal detail evidence locator is outside its run.");
        }

        var path = Path.Combine(runRoot, fileName);

        if (!File.Exists(path)
            || Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)))
                != expectedSha256)
        {
            throw new InvalidDataException("Terminal detail evidence locator/hash mismatch.");
        }
    }

    private static void ValidateResourceSamples(string path, bool requireAtLeastOne)
    {
        var bytes = File.ReadAllBytes(path);

        if (bytes.Length == 0)
        {
            if (requireAtLeastOne)
            {
                throw new InvalidDataException("Succeeded run has no resource sample evidence.");
            }

            return;
        }

        if (bytes[^1] != (byte)'\n')
        {
            throw new InvalidDataException("Resource samples do not end on an LF frame boundary.");
        }

        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "elapsedMs",
            "observedCpuTimeMs",
            "observedWorkingSetBytes",
            "observedProcessCount",
        };
        long previousElapsed = -1;
        var offset = 0;

        while (offset < bytes.Length)
        {
            var relativeLf = bytes.AsSpan(offset).IndexOf((byte)'\n');

            if (relativeLf <= 0)
            {
                throw new InvalidDataException("Resource samples contain an empty frame.");
            }

            var line = bytes.AsSpan(offset, relativeLf);
            offset += relativeLf + 1;
            var canonical = CanonicalJson.Canonicalize(line);

            if (!line.SequenceEqual(canonical))
            {
                throw new InvalidDataException("Resource sample is not exact canonical JSON.");
            }

            using var document = JsonDocument.Parse(canonical);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object
                || root.EnumerateObject().Select(value => value.Name)
                    .ToHashSet(StringComparer.Ordinal).SetEquals(allowed) is false)
            {
                throw new InvalidDataException("Resource sample fields are incomplete or unknown.");
            }

            var elapsed = RequireNonnegativeInteger(root, "elapsedMs");
            _ = RequireNonnegativeInteger(root, "observedCpuTimeMs");
            _ = RequireNonnegativeInteger(root, "observedWorkingSetBytes");
            _ = RequireNonnegativeInteger(root, "observedProcessCount");

            if (elapsed < previousElapsed)
            {
                throw new InvalidDataException("Resource sample monotonic time regressed.");
            }

            previousElapsed = elapsed;
        }
    }

    private static long RequireNonnegativeInteger(JsonElement root, string name)
    {
        var value = root.GetProperty(name);

        if (value.ValueKind != JsonValueKind.Number
            || !value.TryGetInt64(out var number)
            || number < 0)
        {
            throw new InvalidDataException("Resource sample value is not a nonnegative integer.");
        }

        return number;
    }

    private static bool IntentMatches(RunRecord record, RunStoreIntent intent) =>
        record.RunId == intent.RunId
        && record.PlanHash == intent.PlanHash
        && record.ScenarioHash == intent.ScenarioHash
        && record.ArmId == intent.ArmId
        && record.RepeatIndex == intent.RepeatIndex
        && record.AttemptIndex == intent.AttemptIndex
        && record.PolicyConfigurationSha256 == intent.PolicyConfigurationSha256
        && record.EffectiveConfigurationSha256 == intent.EffectiveConfigurationSha256
        && record.ComponentSeedHex == intent.ComponentSeedHex
        && record.RunnerArtifactSha256 == intent.RunnerArtifactSha256
        && record.HarnessSourceSha256 == intent.HarnessSourceSha256
        && record.ExecutionOrdinal == intent.ExecutionOrdinal
        && record.Warmup == intent.Warmup;

    private RunStorePlanManifest ReadPlan(string planHash)
    {
        ValidateHash(planHash, nameof(planHash));
        var path = Path.Combine(PlanRoot(planHash), "plan-store.json");
        EnsureNotReparseDirectory(PlanRoot(planHash));
        EnsurePlanLayoutDirectoriesSafe(PlanRoot(planHash));
        var bytes = RequireCanonical(path);
        var plan = DecodeInternal<RunStorePlanManifest>(bytes);

        if (plan.SchemaVersion != "1.0.0"
            || plan.PlanHash != planHash
            || !plan.Runs.SequenceEqual(plan.Runs.OrderBy(value => value.RunId, StringComparer.Ordinal))
            || !plan.DenominatorIds.SequenceEqual(
                plan.DenominatorIds.Order(StringComparer.Ordinal)))
        {
            throw new InvalidDataException("Run store plan manifest is not canonical semantically.");
        }

        ValidateDenominators(plan.DenominatorIds);

        foreach (var intent in plan.Runs)
        {
            ValidateIntent(intent, planHash);
        }

        var intentsRoot = Path.Combine(PlanRoot(planHash), "intents");
        var intentFiles = Directory.GetFiles(intentsRoot, "*.json")
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (intentFiles.Length != plan.Runs.Count
            || Directory.GetFiles(intentsRoot).Length != intentFiles.Length
            || Directory.GetDirectories(intentsRoot).Length != 0)
        {
            throw new InvalidDataException("Immutable intent inventory differs from the plan manifest.");
        }

        foreach (var intent in plan.Runs)
        {
            RequireExactFile(
                Path.Combine(intentsRoot, intent.RunId + ".json"),
                EncodeInternal(intent),
                "Immutable intent file differs from the plan manifest.");
        }

        return plan;
    }

    private string PlanRoot(string planHash) => Path.Combine(options.StoreRoot, planHash);

    private string Relative(string path) =>
        Path.GetRelativePath(options.StoreRoot, path).Replace('\\', '/');

    private async Task<FileStream> AcquireLock(
        string path,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(path)
                && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("Run store lock file is a reparse point.");
            }

            try
            {
                return new FileStream(
                    path,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None);
            }
            catch (IOException)
            {
                await Task.Delay(
                    TimeSpan.FromMilliseconds(options.LockRetryDelayMs),
                    cancellationToken);
            }
        }
    }

    private static async Task WriteNew(
        string path,
        byte[] bytes,
        CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read);
        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
        stream.Flush(flushToDisk: true);
    }

    private static byte[] EncodeInternal<T>(T value)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
        return CanonicalJson.Canonicalize(bytes);
    }

    private static T DecodeInternal<T>(byte[] canonical)
    {
        var value = JsonSerializer.Deserialize<T>(canonical, JsonOptions);
        return value ?? throw new InvalidDataException("Internal store document is empty.");
    }

    private static T DecodeContract<T>(string path)
        where T : class, IBenchmarkDocument
    {
        var result = BenchmarkContractCodec.Decode<T>(RequireCanonical(path));
        return result.IsSuccess
            ? result.Value!
            : throw new InvalidDataException(
                $"Contract document is invalid at {result.Error!.Path}.");
    }

    private static byte[] RequireCanonical(string path)
    {
        var info = new FileInfo(path);

        if (!info.Exists || (info.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("Required immutable store file is missing or unsafe.");
        }

        var bytes = File.ReadAllBytes(path);
        var canonical = CanonicalJson.Canonicalize(bytes);

        if (!bytes.SequenceEqual(canonical))
        {
            throw new InvalidDataException("Store JSON is not exact canonical bytes.");
        }

        return bytes;
    }

    private static void RequireExactFile(string path, byte[] expected, string message)
    {
        if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(expected))
        {
            throw new InvalidDataException(message);
        }
    }

    private static RunFileEvidence Evidence(string relativePath, byte[] bytes) =>
        new(
            relativePath,
            bytes.LongLength,
            Convert.ToHexStringLower(SHA256.HashData(bytes)));

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

    private static string RunRelativePath(string runId, string fileName) =>
        $"runs/{runId}/{fileName}";

    private static string GridKey(RunStoreIntent intent) => string.Join(
        '|',
        intent.ScenarioHash,
        intent.ArmId,
        intent.RepeatIndex.ToString(CultureInfo.InvariantCulture),
        intent.Warmup ? "warmup" : "measured");

    private static string CalculateDetailId(
        string domain,
        params string[] values)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes(domain));
        Span<byte> length = stackalloc byte[sizeof(ulong)];

        foreach (var value in values)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(
                length,
                checked((ulong)bytes.Length));
            hash.AppendData(length);
            hash.AppendData(bytes);
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static void MoveAside(string source, string destinationRoot)
    {
        Directory.CreateDirectory(destinationRoot);
        var ordinal = 1;

        while (true)
        {
            var destination = Path.Combine(
                destinationRoot,
                ordinal.ToString("D6", CultureInfo.InvariantCulture));

            if (!Directory.Exists(destination) && !File.Exists(destination))
            {
                if (Directory.Exists(source))
                {
                    Directory.Move(source, destination);
                }
                else
                {
                    File.Move(source, destination);
                }

                return;
            }

            ordinal++;
        }
    }

    private static string SafeDiagnostic(Exception exception) =>
        exception switch
        {
            InvalidDataException => "Immutable run evidence failed semantic verification.",
            CryptographicException => "Immutable run evidence failed cryptographic verification.",
            _ => "Immutable run evidence could not be verified.",
        };

    private static bool IsUnder(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        return relative == "."
            || relative != ".."
                && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && !Path.IsPathRooted(relative);
    }

    private static void EnsureNotReparseDirectory(string path)
    {
        var info = new DirectoryInfo(path);

        if (!info.Exists || (info.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("Run store directory is missing or is a reparse point.");
        }
    }

    private static void EnsurePlanLayoutDirectoriesSafe(string planRoot)
    {
        var relativeDirectories = new[]
        {
            "intents",
            "runs",
            ".staging",
            ".incomplete",
            "locks",
            Path.Combine("locks", "runs"),
            "logs",
            Path.Combine("logs", "terminal-events"),
            Path.Combine("logs", ".staging"),
        };

        foreach (var relativeDirectory in relativeDirectories)
        {
            EnsureNotReparseDirectory(Path.Combine(planRoot, relativeDirectory));
        }
    }

    private static void ValidateOptions(AppendOnlyRunStoreOptions value)
    {
        if (!Path.IsPathRooted(value.StoreRoot)
            || !Path.IsPathRooted(value.RepositoryRoot)
            || IsUnder(Path.GetFullPath(value.RepositoryRoot), Path.GetFullPath(value.StoreRoot))
            || value.MaximumEvidenceFileBytes <= 0
            || value.LockRetryDelayMs is < 1 or > 1_000)
        {
            throw new ArgumentException(
                "Run store root must be absolute/outside the repository with positive bounds.");
        }

        if (Directory.Exists(value.StoreRoot))
        {
            EnsureNotReparseDirectory(Path.GetFullPath(value.StoreRoot));
        }
    }

    private static void ValidateHash(string value, string parameter)
    {
        if (value.Length != 64
            || value.Any(character => character is not (>= '0' and <= '9')
                and not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException("Value must be exact lowercase SHA-256.", parameter);
        }
    }

    private static bool IsArtifactId(string value) =>
        value.Length is >= 1 and <= 128
        && value[0] is >= 'a' and <= 'z' or >= '0' and <= '9'
        && value.All(character => character is >= 'a' and <= 'z'
            or >= '0' and <= '9'
            or '.' or '_' or '-');

    private static bool IsCanonicalUtc(string value) =>
        System.Text.RegularExpressions.Regex.IsMatch(
            value,
            "^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(\\.[0-9]{1,7})?Z$",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant)
        && DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out _);

    private static JsonSerializerOptions CreateJsonOptions() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false) },
    };

    private sealed record RunStorePlanManifest(
        string SchemaVersion,
        string PlanHash,
        IReadOnlyList<string> DenominatorIds,
        IReadOnlyList<RunStoreIntent> Runs);

    private sealed record TerminalLogEntry(
        string SchemaVersion,
        long RecordSequence,
        string PreviousEntrySha256,
        string RecordKind,
        string RecordId,
        string RunId,
        string RecordSha256);

    private sealed record TerminalLogState(
        long NextSequence,
        string PreviousEntrySha256,
        long EventCount);

    private sealed record RunStoreSupersession(
        string SchemaVersion,
        string PreviousPlanHash,
        string NewPlanHash,
        string AuthorizationId,
        string PreviousPlanManifestSha256);

    private sealed record CopiedEvidence(long LengthBytes, string Sha256);
}
