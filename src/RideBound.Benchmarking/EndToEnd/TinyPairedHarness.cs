using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RideBound.Benchmarking.Bundles;
using RideBound.Benchmarking.Claims;
using RideBound.Benchmarking.Contracts;
using RideBound.Benchmarking.Execution;
using RideBound.Benchmarking.Metrics;
using RideBound.Benchmarking.Planning;
using RideBound.Benchmarking.Storage;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;

namespace RideBound.Benchmarking.EndToEnd;

public static class TinyPairedHarness
{
    private static readonly string[] DenominatorIds =
        ["planned-runs", "valid-runs"];
    private const string LaunchContractId =
        "runner-ndjson-wp6-manifest-solver-seed-v1";
    private const string MasterSeedHex =
        "8a93591e49b45ec19b42c7d35ec7222499f1250954afad063221668429d46c5a";

    public static async Task<TinyPairedHarnessReceipt> RunAsync(
        TinyHarnessPaths paths,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var tiny = TinyProtocolFixtureCompiler.Compile(
            Path.GetFullPath(paths.RepositoryRoot));
        var fixture = new MechanicalPairedHarnessFixture(
            tiny.Dataset,
            tiny.CanonicalDataset,
            tiny.Scenario,
            tiny.CanonicalScenario,
            tiny.ScenarioHash,
            tiny.GraphSha256,
            tiny.CanonicalCapabilitySelection,
            tiny.SourceFixturePath,
            tiny.SourceFixtureSha256,
            [tiny.SourceFixturePath],
            [
                new MechanicalHarnessBundleSource(
                    tiny.SourceFixturePath,
                    $"data/scenarios/{tiny.ScenarioHash}/source/paired.input.ndjson",
                    "application/x-ndjson"),
            ],
            (run, arm, runnerHash, _) =>
                new RunnerProtocolFixtureConversation(
                    TinyProtocolFixtureCompiler.CreateRunnerFixture(
                        tiny,
                        run,
                        arm,
                        runnerHash)));
        var profile = new MechanicalPairedHarnessProfile(
            "tiny",
            "wp6-tiny-paired-e2e-v1",
            "wp3-boundary-test-v1.json",
            ["tools/RideBound.Wp6TinyHarness"],
            "RideBound.Wp6.TinyTranscriptSet.v1",
            "RideBound.Wp6.TinyDecisionSet.v1",
            "wp6-tiny-paired-harness-v1",
            ["wp6-tiny-protocol-fixture-v1"],
            "wp6-tiny-paired-mechanical-v1",
            "2026-08-11",
            new ExternalProcessLimits(
                WallTimeLimitMs: 30_000,
                CpuTimeLimitMs: 30_000,
                PeakWorkingSetLimitBytes: 1_073_741_824,
                ProcessCountLimit: 16,
                StandardInputLimitBytes: 4_194_304,
                StandardOutputLimitBytes: 4_194_304,
                StandardErrorLimitBytes: 1_048_576,
                SampleIntervalMs: 10),
            RequireTinyOutcomeCoverage);
        return await RunCore(paths, fixture, profile, cancellationToken);
    }

    internal static async Task<TinyPairedHarnessReceipt> RunCore(
        TinyHarnessPaths paths,
        MechanicalPairedHarnessFixture fixture,
        MechanicalPairedHarnessProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(fixture);
        ArgumentNullException.ThrowIfNull(profile);
        ValidateProfile(profile, fixture);
        var resolved = ValidatePaths(paths);
        Directory.CreateDirectory(resolved.WorkRoot);
        var assets = LocateAssets(resolved, profile.CommitmentConfigurationFileName);
        var sourceInventory = CaptureSourceInventory(
            resolved.RepositoryRoot,
            profile.HarnessSourcePaths);
        var sourceInventoryBytes = BundleEvidenceJson.Encode(sourceInventory);
        var sourceInventorySha256 = FileSha(sourceInventoryBytes);
        var harnessSourceSha256 = BundleSourceInventoryIdentity.CalculateComponent(
            "harness",
            sourceInventory.Entries);
        var oracleSourceSha256 = BundleSourceInventoryIdentity.CalculateComponent(
            "oracle",
            sourceInventory.Entries);
        var verifierSourceSha256 = BundleSourceInventoryIdentity.CalculateComponent(
            "verifier",
            sourceInventory.Entries);
        var pins = BuildRuntimePins(assets, fixture.PinnedInputPaths);
        var capturedRuntime = ProcessArtifactIdentity.Capture(pins);
        var runtimeInventory = new BundleRuntimeInventory(
            "1.0.0",
            capturedRuntime.InventorySha256,
            capturedRuntime.Artifacts);
        var runtimeBytes = BundleEvidenceJson.Encode(runtimeInventory);
        var runnerExecutableSha256 = RequiredRuntime(
            capturedRuntime,
            "runner-executable").Sha256;
        var runnerAssemblySha256 = RequiredRuntime(
            capturedRuntime,
            "runner-assembly").Sha256;
        var contractsAssemblySha256 = RequiredRuntime(
            capturedRuntime,
            "contracts-assembly").Sha256;
        var oracleAssemblySha256 = RequiredRuntime(
            capturedRuntime,
            "oracle-assembly").Sha256;
        var verifierAssemblySha256 = RequiredRuntime(
            capturedRuntime,
            "verifier-assembly").Sha256;
        var registry = MechanicalMetricRegistry.Load(assets.RegistryPath);
        var planCompilation = CompilePlan(
            fixture,
            profile.PlanId,
            assets,
            registry.RegistryHash,
            capturedRuntime.InventorySha256,
            runnerExecutableSha256,
            runnerAssemblySha256,
            contractsAssemblySha256,
            harnessSourceSha256,
            oracleSourceSha256);

        if (!planCompilation.IsSuccess)
        {
            throw new InvalidDataException(
                $"Tiny plan compilation failed: {planCompilation.Issue!.Code}: "
                + planCompilation.Issue.Message);
        }

        var compiled = planCompilation.Value!;
        var plan = compiled.Plan;
        var arms = plan.Arms.ToDictionary(value => value.ArmId, StringComparer.Ordinal);
        var intents = compiled.PlannedRuns.Select(
            run => new RunStoreIntent(
                run.RunId,
                compiled.PlanHash,
                run.ScenarioHash,
                run.ArmId,
                run.RepeatIndex,
                run.AttemptIndex,
                arms[run.ArmId].PolicyConfigurationSha256,
                arms[run.ArmId].EffectiveConfigurationSha256,
                run.SolverSeed.DigestHex,
                runnerExecutableSha256,
                harnessSourceSha256,
                run.ExecutionOrdinal,
                run.Warmup,
                capturedRuntime.InventorySha256)).ToArray();
        var storeRoot = Path.Combine(resolved.WorkRoot, "run-store");
        var store = new AppendOnlyRunStore(
            new AppendOnlyRunStoreOptions(
                storeRoot,
                resolved.RepositoryRoot,
                MaximumEvidenceFileBytes: 16_777_216));
        await store.InitializePlanAsync(
            compiled.PlanHash,
            intents,
            DenominatorIds,
            cancellationToken);
        var intentById = intents.ToDictionary(value => value.RunId, StringComparer.Ordinal);
        var commits = new List<RunStoreCommitResult>(intents.Length);
        var processRunsRoot = Path.Combine(resolved.WorkRoot, "runner-processes");

        foreach (var planned in compiled.PlannedRuns.OrderBy(value => value.ExecutionOrdinal))
        {
            var arm = arms[planned.ArmId];
            var wp4Path = planned.ArmId == "b1"
                ? assets.B1ConfigurationPath
                : assets.C1ConfigurationPath;
            var conversation = fixture.CreateConversation(
                planned,
                arm,
                runnerExecutableSha256,
                sourceInventorySha256);
            var request = new ExternalProcessRunRequest(
                planned.RunId,
                resolved.RepositoryRoot,
                processRunsRoot,
                assets.RunnerExecutablePath,
                [
                    "--mode",
                    "commitment",
                    "--policy-config",
                    assets.CommitmentConfigurationPath,
                    "--wp4-config",
                    wp4Path,
                    "--solver-seed-source",
                    "manifest-master-seed",
                ],
                pins,
                capturedRuntime.InventorySha256,
                profile.ProcessLimits,
                conversation);
            var started = CanonicalUtc(DateTimeOffset.UtcNow);
            var result = await ExternalProcessSupervisor.RunAsync(
                request,
                cancellationToken);
            var finished = CanonicalUtc(DateTimeOffset.UtcNow);
            var submission = ExternalProcessTerminalMapper.CreateSubmission(
                intentById[planned.RunId],
                result,
                started,
                finished,
                DenominatorIds);
            var commit = await store.CommitAsync(
                submission,
                cancellationToken: cancellationToken);
            commits.Add(commit);

            if (result.Status != ExternalProcessTerminalStatus.Succeeded)
            {
                throw new InvalidOperationException(
                    $"{profile.ProfileId} Runner process '{planned.RunId}' failed with "
                    + $"'{result.Failure?.Code}': {result.Failure?.SafeMessage}");
            }
        }

        var storeVerification = store.VerifyPlan(compiled.PlanHash);
        var expectedRunCount = compiled.PlannedRuns.Count;

        if (!storeVerification.IsValid
            || storeVerification.PlannedCount != expectedRunCount
            || storeVerification.SucceededCount != expectedRunCount
            || storeVerification.FailedCount != 0
            || storeVerification.ExcludedCount != 0)
        {
            throw new InvalidDataException(
                $"{profile.ProfileId} run grid did not conserve all "
                    + $"{expectedRunCount} planned B1/C1 terminals as successes.");
        }

        var metricEvidence = await CalculateAndVerifyMetrics(
            commits,
            fixture.Scenario.TimeWindow,
            registry,
            harnessSourceSha256,
            assets,
            oracleAssemblySha256,
            resolved.WorkRoot,
            cancellationToken);
        profile.ValidateOutcomeCoverage(metricEvidence.ProductionRows);
        var productionBytes = EncodeRows(metricEvidence.ProductionRows);
        var oracleBytes = ConcatenateOracleRows(metricEvidence.OracleRowsByRun);

        if (!productionBytes.SequenceEqual(oracleBytes))
        {
            throw new MechanicalMetricCalculationException(
                "metric.oracle-mismatch",
                "Global production and independent oracle rows differ.");
        }

        var metricSetHash = BundleMetricSetIdentity.Calculate(
            compiled.PlanHash,
            registry.RegistryHash,
            productionBytes);
        var runReceipts = CreateRunReceipts(
            commits,
            metricEvidence.ProductionByRun);
        var semanticRows = metricEvidence.ProductionRows
            .Where(value => !value.MetricId.StartsWith("resource.", StringComparison.Ordinal))
            .ToArray();
        var semanticMetricSetSha256 = FileSha(EncodeRows(semanticRows));
        var runGridSha256 = FileSha(
            BundleEvidenceJson.Encode(
                compiled.PlannedRuns.OrderBy(value => value.ExecutionOrdinal)
                    .Select(
                        value => new
                        {
                            value.ArmId,
                            value.ExecutionOrdinal,
                            value.RepeatIndex,
                            value.RunId,
                            solverSeed = value.SolverSeed.DigestHex,
                        }).ToArray()));
        var transcriptSetSha256 = HashReceiptFrames(
            profile.TranscriptSetDomain,
            runReceipts,
            value => $"{value.InputSha256}|{value.OutputSha256}|{value.ObservationIndexSha256}");
        var decisionSetSha256 = HashReceiptFrames(
            profile.DecisionSetDomain,
            runReceipts,
            value => value.DecisionSequenceSha256);
        var bundleInputs = Path.Combine(resolved.WorkRoot, "bundle-inputs");
        Directory.CreateDirectory(bundleInputs);
        var bundle = await BuildBundle(
            resolved,
            fixture,
            profile,
            compiled,
            commits,
            registry,
            sourceInventoryBytes,
            sourceInventorySha256,
            harnessSourceSha256,
            oracleSourceSha256,
            verifierSourceSha256,
            runtimeBytes,
            capturedRuntime.InventorySha256,
            runnerExecutableSha256,
            runnerAssemblySha256,
            contractsAssemblySha256,
            metricSetHash,
            productionBytes,
            oracleBytes,
            metricEvidence.OracleSummaryPaths,
            storeRoot,
            bundleInputs,
            cancellationToken);
        var externalReportPath = Path.Combine(
            resolved.WorkRoot,
            "external-bundle-verification.json");
        var verifierExecution = await ExecuteAsync(
            assets.VerifierExecutablePath,
            [
                "--bag",
                bundle.BundleDirectory,
                "--report",
                externalReportPath,
            ],
            resolved.WorkRoot,
            TimeSpan.FromMinutes(2),
            cancellationToken);

        if (verifierExecution.ExitCode != 0)
        {
            throw new InvalidDataException(
                $"External strict bundle verifier rejected the {profile.ProfileId} bundle: "
                + verifierExecution.StandardError);
        }

        var externalReport = BundleEvidenceJson.DecodeExact<ExternalBundleVerificationReport>(
            File.ReadAllBytes(externalReportPath));

        if (!externalReport.IsValid
            || externalReport.VerifierAssemblySha256 != verifierAssemblySha256
            || externalReport.BundleHash != bundle.BundleHash
            || externalReport.PlanHash != compiled.PlanHash
            || externalReport.MetricSetHash != metricSetHash)
        {
            throw new InvalidDataException(
                $"External verification report is not bound to the {profile.ProfileId} bundle identities.");
        }

        var logicalManifestSha256 = FileSha(
            File.ReadAllBytes(Path.Combine(
                bundle.BundleDirectory,
                "data",
                "bundle-manifest.json")));
        var receipt = new TinyPairedHarnessReceipt(
            "1.0.0",
            "mechanical",
            ArtifactClaimProfileCatalog.GetV1().ProfileId,
            compiled.PlanHash,
            fixture.ScenarioHash,
            fixture.SourceFixtureSha256,
            capturedRuntime.InventorySha256,
            sourceInventorySha256,
            runGridSha256,
            transcriptSetSha256,
            decisionSetSha256,
            semanticMetricSetSha256,
            metricSetHash,
            storeVerification.PlannedCount,
            storeVerification.SucceededCount,
            storeVerification.FailedCount,
            storeVerification.ExcludedCount,
            bundle.BundleHash,
            logicalManifestSha256,
            FileSha(File.ReadAllBytes(externalReportPath)),
            bundle.BundleDirectory,
            runReceipts);
        WriteNew(resolved.ReceiptPath, BundleEvidenceJson.Encode(receipt));
        return receipt;
    }

    private static void ValidateProfile(
        MechanicalPairedHarnessProfile profile,
        MechanicalPairedHarnessFixture fixture)
    {
        ValidateProfileCollections(
            profile.BundleClaims,
            fixture.PinnedInputPaths,
            fixture.AdditionalBundleSources);
    }

    internal static void ValidateProfileCollections(
        IReadOnlyList<string> bundleClaims,
        IReadOnlyList<string> pinnedInputPaths,
        IReadOnlyList<MechanicalHarnessBundleSource> additionalBundleSources)
    {
        ArgumentNullException.ThrowIfNull(bundleClaims);
        ArgumentNullException.ThrowIfNull(pinnedInputPaths);
        ArgumentNullException.ThrowIfNull(additionalBundleSources);
        var sourceRelativePaths = additionalBundleSources
            .Select(value => value.RelativePath)
            .ToArray();

        if (bundleClaims.Count == 0
            || !bundleClaims.SequenceEqual(bundleClaims.Order(StringComparer.Ordinal))
            || bundleClaims.Distinct(StringComparer.Ordinal).Count() != bundleClaims.Count
            || pinnedInputPaths.Count == 0
            || pinnedInputPaths.Any(path => !Path.IsPathRooted(path))
            || !pinnedInputPaths.SequenceEqual(pinnedInputPaths.Order(StringComparer.Ordinal))
            || pinnedInputPaths.Distinct(StringComparer.OrdinalIgnoreCase).Count()
                != pinnedInputPaths.Count
            || additionalBundleSources.Count == 0
            || additionalBundleSources.Any(
                value => !Path.IsPathRooted(value.FullPath)
                    || Path.IsPathRooted(value.RelativePath)
                    || string.IsNullOrWhiteSpace(value.RelativePath)
                    || string.IsNullOrWhiteSpace(value.MediaType))
            || !sourceRelativePaths.SequenceEqual(
                sourceRelativePaths.Order(StringComparer.Ordinal))
            || sourceRelativePaths.Distinct(StringComparer.OrdinalIgnoreCase).Count()
                != sourceRelativePaths.Length
            || additionalBundleSources.Select(value => value.FullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count()
                != additionalBundleSources.Count)
        {
            throw new ArgumentException(
                "Mechanical harness claims, pins and bundle sources must be non-empty, "
                    + "rooted where required, sorted ordinal and case-insensitively unique.");
        }
    }

    private static ResolvedPaths ValidatePaths(TinyHarnessPaths paths)
    {
        var repository = Path.GetFullPath(paths.RepositoryRoot);
        var work = Path.GetFullPath(paths.WorkRoot);
        var bundle = Path.GetFullPath(paths.BundleDirectory);
        var receipt = Path.GetFullPath(paths.ReceiptPath);

        if (!File.Exists(Path.Combine(repository, "RideBound.slnx"))
            || paths.BuildConfiguration is not ("Debug" or "Release")
            || Directory.Exists(work)
            || File.Exists(work)
            || Directory.Exists(bundle)
            || File.Exists(bundle)
            || File.Exists(receipt)
            || Directory.Exists(receipt)
            || IsInside(repository, work)
            || IsInside(bundle, receipt))
        {
            throw new ArgumentException(
                "Tiny harness requires a fresh external work root and new bundle/receipt paths.");
        }

        return new ResolvedPaths(
            repository,
            work,
            bundle,
            receipt,
            paths.BuildConfiguration);
    }

    private static HarnessAssets LocateAssets(
        ResolvedPaths paths,
        string commitmentConfigurationFileName)
    {
        var runnerRoot = Path.Combine(
            paths.RepositoryRoot,
            "src",
            "RideBound.Runner",
            "bin",
            paths.BuildConfiguration,
            "net10.0");
        var oracleRoot = Path.Combine(
            paths.RepositoryRoot,
            "tools",
            "RideBound.Wp6MetricOracle",
            "bin",
            paths.BuildConfiguration,
            "net10.0");
        var verifierRoot = Path.Combine(
            paths.RepositoryRoot,
            "tools",
            "RideBound.Wp6BundleVerify",
            "bin",
            paths.BuildConfiguration,
            "net10.0");
        var result = new HarnessAssets(
            AppHost(runnerRoot, "RideBound.Runner"),
            RequiredFile(runnerRoot, "RideBound.Runner.dll"),
            RequiredFile(runnerRoot, "RideBound.Contracts.dll"),
            AppHost(oracleRoot, "RideBound.Wp6MetricOracle"),
            RequiredFile(oracleRoot, "RideBound.Wp6MetricOracle.dll"),
            AppHost(verifierRoot, "RideBound.Wp6BundleVerify"),
            RequiredFile(verifierRoot, "RideBound.Wp6BundleVerify.dll"),
            Assembly.GetEntryAssembly()?.Location
                ?? throw new InvalidOperationException("Harness entry assembly is unavailable."),
            Path.Combine(
                paths.RepositoryRoot,
                "benchmarks",
                "configurations",
                commitmentConfigurationFileName),
            Path.Combine(
                paths.RepositoryRoot,
                "benchmarks",
                "configurations",
                "wp4-rolling-cost-boundary-v1.json"),
            Path.Combine(
                paths.RepositoryRoot,
                "benchmarks",
                "configurations",
                "wp4-ridebound-hard-vector-boundary-v1.json"),
            Path.Combine(
                paths.RepositoryRoot,
                "benchmarks",
                "fixtures",
                "wp6",
                "metrics",
                "mechanical-metric-registry-v1.json"),
            runnerRoot);

        foreach (var file in new[]
        {
            result.CommitmentConfigurationPath,
            result.B1ConfigurationPath,
            result.C1ConfigurationPath,
            result.RegistryPath,
            result.HarnessAssemblyPath,
        })
        {
            if (!File.Exists(file))
            {
                throw new FileNotFoundException(
                    "Mechanical harness build/source asset is missing.",
                    file);
            }
        }

        return result;
    }

    private static BundleSourceInventory CaptureSourceInventory(
        string repository,
        IReadOnlyList<string> profileHarnessSourcePaths) =>
        BundleSourceInventoryCapture.Capture(
            repository,
            [
                new BundleSourceComponentSelection(
                    "harness",
                    new[]
                    {
                        "src/RideBound.Benchmarking.Contracts",
                        "src/RideBound.Benchmarking",
                        "src/RideBound.Contracts",
                        "src/RideBound.Runner/Configuration",
                        "src/RideBound.Runner/Protocol",
                        "src/RideBound.Runner/Program.cs",
                    }.Concat(profileHarnessSourcePaths).ToArray()),
                new BundleSourceComponentSelection(
                    "oracle",
                    ["tools/RideBound.Wp6MetricOracle"]),
                new BundleSourceComponentSelection(
                    "verifier",
                    ["tools/RideBound.Wp6BundleVerify"]),
            ]);

    private static PinnedProcessFile[] BuildRuntimePins(
        HarnessAssets assets,
        IReadOnlyList<string> pinnedInputPaths)
    {
        var values = new List<PinnedProcessFile>
        {
            ProcessArtifactIdentity.Pin("runner-executable", assets.RunnerExecutablePath),
            ProcessArtifactIdentity.Pin("runner-assembly", assets.RunnerAssemblyPath),
            ProcessArtifactIdentity.Pin("contracts-assembly", assets.ContractsAssemblyPath),
            ProcessArtifactIdentity.Pin("harness-assembly", assets.HarnessAssemblyPath),
            ProcessArtifactIdentity.Pin("oracle-assembly", assets.OracleAssemblyPath),
            ProcessArtifactIdentity.Pin("verifier-assembly", assets.VerifierAssemblyPath),
            ProcessArtifactIdentity.Pin("commitment-config", assets.CommitmentConfigurationPath),
            ProcessArtifactIdentity.Pin("wp4-b1-config", assets.B1ConfigurationPath),
            ProcessArtifactIdentity.Pin("wp4-c1-config", assets.C1ConfigurationPath),
        };

        for (var index = 0; index < pinnedInputPaths.Count; index++)
        {
            values.Add(
                ProcessArtifactIdentity.Pin(
                    $"scenario-source-{index:D3}",
                    pinnedInputPaths[index]));
        }
        var reserved = values.Select(value => Path.GetFullPath(value.FullPath))
            .ToHashSet(PathComparer());
        var runnerFiles = Directory.GetFiles(assets.RunnerRoot)
            .Where(path => !path.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase))
            .Where(path => !reserved.Contains(Path.GetFullPath(path)))
            .Order(StringComparer.Ordinal)
            .ToArray();

        for (var index = 0; index < runnerFiles.Length; index++)
        {
            values.Add(
                ProcessArtifactIdentity.Pin(
                    $"runner-file-{index:D3}",
                    runnerFiles[index]));
        }

        var runtimeRoot = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var runtimeFiles = Directory.GetFiles(runtimeRoot)
            .Where(path => !reserved.Contains(Path.GetFullPath(path)))
            .Order(StringComparer.Ordinal)
            .ToArray();

        for (var index = 0; index < runtimeFiles.Length; index++)
        {
            values.Add(
                ProcessArtifactIdentity.Pin(
                    $"dotnet-runtime-{index:D3}",
                    runtimeFiles[index]));
        }

        if (values.Select(value => Path.GetFullPath(value.FullPath))
            .Distinct(PathComparer()).Count() != values.Count)
        {
            throw new InvalidDataException(
                "Mechanical runtime inventory contains an ambiguous duplicate path.");
        }

        return values.OrderBy(value => value.Role, StringComparer.Ordinal).ToArray();
    }

    private static BenchmarkPlanCompilationResult CompilePlan(
        MechanicalPairedHarnessFixture fixture,
        string planId,
        HarnessAssets assets,
        string registryHash,
        string runtimeInventorySha256,
        string runnerExecutableSha256,
        string runnerAssemblySha256,
        string contractsAssemblySha256,
        string harnessSourceSha256,
        string oracleSourceSha256)
    {
        var commitmentBytes = CanonicalJson.Canonicalize(
            File.ReadAllBytes(assets.CommitmentConfigurationPath));
        var commitmentHash = HashValue(commitmentBytes);
        var b1Bytes = CanonicalJson.Canonicalize(
            File.ReadAllBytes(assets.B1ConfigurationPath));
        var c1Bytes = CanonicalJson.Canonicalize(
            File.ReadAllBytes(assets.C1ConfigurationPath));
        var b1Hash = HashValue(b1Bytes);
        var c1Hash = HashValue(c1Bytes);
        var b1Identity = ReadWp4Identity(b1Bytes);
        var c1Identity = ReadWp4Identity(c1Bytes);
        return BenchmarkPlanCompiler.Compile(
            new BenchmarkPlanDefinition(
                planId,
                EvidenceClass.Mechanical,
                [fixture.ScenarioHash],
                [
                    Arm(
                        "b1",
                        b1Identity,
                        Wp4PolicyConfigurationBinding.CreateCanonicalDocument(
                            commitmentHash,
                            b1Hash),
                        fixture.CanonicalCapabilitySelection),
                    Arm(
                        "c1",
                        c1Identity,
                        Wp4PolicyConfigurationBinding.CreateCanonicalDocument(
                            commitmentHash,
                            c1Hash),
                        fixture.CanonicalCapabilitySelection),
                ],
                "wp4-common-candidate-v1",
                MasterSeedHex,
                1,
                3,
                "wp6-local-bounded-process-v1",
                registryHash,
                new RunnerArtifactIdentity(
                    runnerExecutableSha256,
                    runnerAssemblySha256,
                    contractsAssemblySha256,
                    runtimeInventorySha256,
                    LaunchContractId),
                harnessSourceSha256,
                oracleSourceSha256));
    }

    private static BenchmarkArmDefinition Arm(
        string armId,
        Wp4Identity identity,
        byte[] bindingDocument,
        byte[] capabilitySelection) =>
        new(
            armId,
            identity.PolicyId,
            identity.PolicyVersion,
            Wp4PolicyConfigurationBinding.BindingId,
            bindingDocument,
            "wp4-common-generator-v1",
            10_000,
            "commitment-validator-v1",
            "google-ortools-cp-sat",
            "9.15.6755",
            100_000,
            capabilitySelection);

    private static async Task<MetricEvidence> CalculateAndVerifyMetrics(
        IReadOnlyList<RunStoreCommitResult> commits,
        ScenarioTimeWindow timeWindow,
        MechanicalMetricRegistry registry,
        string harnessSourceSha256,
        HarnessAssets assets,
        string oracleAssemblySha256,
        string workRoot,
        CancellationToken cancellationToken)
    {
        var productionRows = new List<MetricRow>();
        var productionByRun = new Dictionary<string, MechanicalMetricCalculationResult>(
            StringComparer.Ordinal);
        var oracleRowsByRun = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        var oracleSummaryPaths = new List<string>();
        var oracleRoot = Path.Combine(workRoot, "oracle");
        Directory.CreateDirectory(oracleRoot);

        foreach (var commit in commits.OrderBy(value => value.RunRecord.RunId, StringComparer.Ordinal))
        {
            var runRoot = commit.RunDirectory;
            var result = MechanicalMetricCalculator.Calculate(
                new MechanicalMetricCalculationInput(
                    commit.RunRecord,
                    timeWindow,
                    File.ReadAllBytes(Path.Combine(runRoot, "run-record.json")),
                    File.ReadAllBytes(Path.Combine(runRoot, "input.ndjson")),
                    File.ReadAllBytes(Path.Combine(runRoot, "output.ndjson")),
                    File.ReadAllBytes(Path.Combine(runRoot, "observation-index.ndjson")),
                    File.ReadAllBytes(Path.Combine(runRoot, "resource-samples.ndjson")),
                    registry,
                    harnessSourceSha256));
            var requestPath = Path.Combine(
                oracleRoot,
                commit.RunRecord.RunId + ".request.json");
            var rowsPath = Path.Combine(
                oracleRoot,
                commit.RunRecord.RunId + ".rows.ndjson");
            var summaryPath = Path.Combine(
                oracleRoot,
                commit.RunRecord.RunId + ".summary.json");
            WriteNew(
                requestPath,
                BundleEvidenceJson.Encode(
                    new
                    {
                        calculatorSourceSha256 = harnessSourceSha256,
                        drainEndMs = timeWindow.DrainEndMs,
                        horizonEndMs = timeWindow.HorizonEndMs,
                        inputTranscriptPath = Path.Combine(runRoot, "input.ndjson"),
                        observationIndexPath = Path.Combine(runRoot, "observation-index.ndjson"),
                        outputTranscriptPath = Path.Combine(runRoot, "output.ndjson"),
                        registryPath = assets.RegistryPath,
                        resourceSamplesPath = Path.Combine(runRoot, "resource-samples.ndjson"),
                        runRecordPath = Path.Combine(runRoot, "run-record.json"),
                        scoreStartMs = timeWindow.ScoreStartMs,
                        warmupStartMs = timeWindow.WarmupStartMs,
                    }));
            var execution = await ExecuteAsync(
                assets.OracleExecutablePath,
                [
                    "--request",
                    requestPath,
                    "--rows-out",
                    rowsPath,
                    "--summary-out",
                    summaryPath,
                ],
                oracleRoot,
                TimeSpan.FromMinutes(1),
                cancellationToken);

            if (execution.ExitCode != 0
                || !string.IsNullOrEmpty(execution.StandardOutput)
                || !string.IsNullOrEmpty(execution.StandardError))
            {
                throw new MechanicalMetricCalculationException(
                    "metric.oracle-mismatch",
                    "Independent metric oracle process failed or emitted unexpected text.");
            }

            var oracleRows = File.ReadAllBytes(rowsPath);
            var summary = BundleEvidenceJson.DecodeExact<OracleExecutionSummary>(
                File.ReadAllBytes(summaryPath));

            if (summary.SchemaVersion != "1.0.0"
                || summary.OracleAssemblySha256 != oracleAssemblySha256
                || summary.MetricSetHash != result.MetricSetHash
                || summary.SemanticEvidenceSha256 != result.SemanticEvidenceSha256
                || summary.ResourceEvidenceSha256 != result.ResourceEvidenceSha256
                || summary.RowCount != result.Rows.Count)
            {
                throw new MechanicalMetricCalculationException(
                    "metric.oracle-mismatch",
                    "Independent oracle summary differs from production identities.");
            }

            MechanicalMetricOracleVerifier.Verify(
                result,
                oracleRows,
                summary.MetricSetHash);
            productionRows.AddRange(result.Rows);
            productionByRun.Add(commit.RunRecord.RunId, result);
            oracleRowsByRun.Add(commit.RunRecord.RunId, oracleRows);
            oracleSummaryPaths.Add(summaryPath);
        }

        return new MetricEvidence(
            productionRows.OrderBy(value => value.RunId, StringComparer.Ordinal)
                .ThenBy(value => value.MetricId, StringComparer.Ordinal)
                .ThenBy(value => ScopeWire(value.ScopeKind), StringComparer.Ordinal)
                .ThenBy(value => value.ScopeId, StringComparer.Ordinal)
                .ThenBy(value => WindowWire(value.WindowId), StringComparer.Ordinal)
                .ToArray(),
            productionByRun,
            oracleRowsByRun,
            oracleSummaryPaths);
    }

    private static void RequireTinyOutcomeCoverage(IReadOnlyList<MetricRow> rows)
    {
        foreach (var run in rows.GroupBy(value => value.RunId, StringComparer.Ordinal))
        {
            long All(string metric) => run.Single(
                value => value.MetricId == metric
                    && value.WindowId == MetricWindowId.All).ValueInteger ?? 0;
            var nonZeroDelta = run.Any(
                value => value.MetricId.StartsWith("decisionDelta.", StringComparison.Ordinal)
                    && value.MetricId.EndsWith(".sum", StringComparison.Ordinal)
                    && value.WindowId == MetricWindowId.All
                    && value.ValueInteger is > 0);

            if (All("request.accepted.count") < 1
                || All("request.completed.count") < 1
                || All("promise.revision.count") < 1
                || All("request.rejected.count") + All("request.deferred.action.count") < 1
                || !nonZeroDelta)
            {
                throw new InvalidDataException(
                    "Tiny fixture did not exercise accept, complete, reject/defer, revision and non-zero commitment delta in every run.");
            }
        }
    }

    private static TinyRunIdentityReceipt[] CreateRunReceipts(
        IReadOnlyList<RunStoreCommitResult> commits,
        IReadOnlyDictionary<string, MechanicalMetricCalculationResult> metrics) =>
        commits.OrderBy(value => value.RunRecord.RunId, StringComparer.Ordinal)
            .Select(
                commit =>
                {
                    var root = commit.RunDirectory;
                    var output = File.ReadAllBytes(Path.Combine(root, "output.ndjson"));
                    var metric = metrics[commit.RunRecord.RunId];
                    var semantic = EncodeRows(
                        metric.Rows.Where(
                            value => !value.MetricId.StartsWith(
                                "resource.",
                                StringComparison.Ordinal)));
                    return new TinyRunIdentityReceipt(
                        commit.RunRecord.RunId,
                        commit.RunRecord.ArmId,
                        commit.RunRecord.RepeatIndex,
                        commit.RunRecord.ExecutionOrdinal,
                        FileSha(File.ReadAllBytes(Path.Combine(root, "input.ndjson"))),
                        FileSha(output),
                        FileSha(File.ReadAllBytes(Path.Combine(root, "observation-index.ndjson"))),
                        DecisionSequenceSha256(output),
                        FileSha(semantic),
                        FileSha(metric.CanonicalRows));
                }).ToArray();

    private static async Task<StrictBagItBundleBuildResult> BuildBundle(
        ResolvedPaths paths,
        MechanicalPairedHarnessFixture fixture,
        MechanicalPairedHarnessProfile profile,
        CompiledBenchmarkPlan compiled,
        IReadOnlyList<RunStoreCommitResult> commits,
        MechanicalMetricRegistry registry,
        byte[] sourceInventoryBytes,
        string sourceInventorySha256,
        string harnessSourceSha256,
        string oracleSourceSha256,
        string verifierSourceSha256,
        byte[] runtimeBytes,
        string runtimeInventorySha256,
        string runnerExecutableSha256,
        string runnerAssemblySha256,
        string contractsAssemblySha256,
        string metricSetHash,
        byte[] productionBytes,
        byte[] oracleBytes,
        IReadOnlyList<string> oracleSummaryPaths,
        string storeRoot,
        string inputRoot,
        CancellationToken cancellationToken)
    {
        var planPath = Write(inputRoot, "benchmark-plan.json", compiled.CanonicalPlanBytes);
        var datasetPath = Write(inputRoot, "dataset.json", fixture.CanonicalDataset);
        var scenarioPath = Write(inputRoot, "scenario.json", fixture.CanonicalScenario);
        var sourcePath = Write(inputRoot, "source-inventory.json", sourceInventoryBytes);
        var runtimePath = Write(inputRoot, "runtime-inventory.json", runtimeBytes);
        var productionPath = Write(inputRoot, "production.ndjson", productionBytes);
        var oraclePath = Write(inputRoot, "oracle.ndjson", oracleBytes);
        var failuresPath = Write(inputRoot, "failures.ndjson", []);
        var exclusionsPath = Write(inputRoot, "exclusions.ndjson", []);
        var registryPath = Write(
            inputRoot,
            "metric-registry.json",
            registry.CanonicalBytes);
        var machine = MachineProvenance(paths.WorkRoot);
        var machinePath = Write(
            inputRoot,
            "machine.json",
            BundleEvidenceJson.Encode(machine));
        var runStorePlanPath = Path.Combine(
            storeRoot,
            compiled.PlanHash,
            "plan-store.json");
        var binding = new BundleReproducibilityBinding(
            "1.0.1",
            compiled.PlanHash,
            metricSetHash,
            ArtifactClaimProfileCatalog.V1Sha256,
            sourceInventorySha256,
            runtimeInventorySha256,
            harnessSourceSha256,
            oracleSourceSha256,
            verifierSourceSha256,
            runnerExecutableSha256,
            runnerAssemblySha256,
            contractsAssemblySha256,
            FileSha(File.ReadAllBytes(machinePath)),
            FileSha(File.ReadAllBytes(registryPath)),
            FileSha(File.ReadAllBytes(runStorePlanPath)));
        var bindingPath = Write(
            inputRoot,
            "reproducibility.json",
            BundleEvidenceJson.Encode(binding));
        var payloads = new List<(string Source, string Relative)>
        {
            (planPath, "data/benchmark-plan.json"),
            (datasetPath, $"data/datasets/{fixture.Dataset.DatasetId}.json"),
            (scenarioPath, $"data/scenarios/{fixture.ScenarioHash}/scenario.json"),
            (failuresPath, "data/failures.ndjson"),
            (exclusionsPath, "data/exclusions.ndjson"),
            (productionPath, "data/metrics/production.ndjson"),
            (oraclePath, "data/metrics/oracle.ndjson"),
            (bindingPath, "data/provenance/reproducibility.json"),
            (runtimePath, "data/provenance/runtime-inventory.json"),
            (machinePath, "data/provenance/machine.json"),
            (registryPath, "data/provenance/metric-registry.json"),
            (runStorePlanPath, "data/provenance/run-store-plan.json"),
            (sourcePath, "data/source-inventory/repository.json"),
        };

        foreach (var summary in oracleSummaryPaths.Order(StringComparer.Ordinal))
        {
            payloads.Add(
                (summary,
                    $"data/provenance/oracle-execution/{Path.GetFileName(summary)}"));
        }

        foreach (var source in fixture.AdditionalBundleSources.OrderBy(
                     value => value.RelativePath,
                     StringComparer.Ordinal))
        {
            payloads.Add((source.FullPath, source.RelativePath));
        }

        foreach (var commit in commits.OrderBy(value => value.RunRecord.RunId, StringComparer.Ordinal))
        {
            foreach (var file in Directory.GetFiles(commit.RunDirectory).Order(StringComparer.Ordinal))
            {
                payloads.Add(
                    (file,
                        $"data/runs/{commit.RunRecord.RunId}/{Path.GetFileName(file)}"));
            }
        }

        var sources = payloads.Select(
                value => new BundlePayloadSource(
                    Path.GetFullPath(value.Source),
                    value.Relative,
                    MediaType(value.Relative),
                    Role(value.Relative),
                    profile.BundleProducerId,
                    profile.BundleClaims))
            .OrderBy(value => value.RelativePath, StringComparer.Ordinal)
            .ToArray();
        return await new StrictBagItBundleBuilder().BuildAsync(
            new StrictBagItBundleRequest(
                paths.BundleDirectory,
                profile.BundleId,
                EvidenceClass.Mechanical,
                ArtifactClaimProfileCatalog.GetV1().ProfileId,
                metricSetHash,
                sourceInventorySha256,
                runtimeInventorySha256,
                profile.BundleDate,
                sources),
            cancellationToken);
    }

    private static BundleMachineProvenance MachineProvenance(string workRoot)
    {
        var drive = new DriveInfo(Path.GetPathRoot(workRoot)!);
        return new BundleMachineProvenance(
            "1.0.0",
            RuntimeInformation.OSDescription,
            RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant(),
            RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant(),
            Environment.ProcessorCount,
            Math.Max(1, GC.GetGCMemoryInfo().TotalAvailableMemoryBytes),
            Environment.Version.ToString(),
            RuntimeInformation.FrameworkDescription,
            drive.DriveFormat,
            "host-power-mode-not-recorded",
            FileSha(Encoding.UTF8.GetBytes(Environment.MachineName)),
            "none");
    }

    private static byte[] ConcatenateOracleRows(
        IReadOnlyDictionary<string, byte[]> rows)
    {
        using var stream = new MemoryStream();

        foreach (var value in rows.OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            using var document = JsonDocument.Parse(value.Value);

            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new MechanicalMetricCalculationException(
                    "metric.oracle-mismatch",
                    "Independent oracle rows must be a canonical JSON array per run.");
            }

            foreach (var row in document.RootElement.EnumerateArray())
            {
                var canonical = CanonicalJson.Canonicalize(
                    Encoding.UTF8.GetBytes(row.GetRawText()));
                stream.Write(canonical);
                stream.WriteByte((byte)'\n');
            }
        }

        return stream.ToArray();
    }

    private static byte[] EncodeRows(IEnumerable<MetricRow> rows)
    {
        using var stream = new MemoryStream();

        foreach (var row in rows.OrderBy(value => value.RunId, StringComparer.Ordinal)
            .ThenBy(value => value.MetricId, StringComparer.Ordinal)
            .ThenBy(value => ScopeWire(value.ScopeKind), StringComparer.Ordinal)
            .ThenBy(value => value.ScopeId, StringComparer.Ordinal)
            .ThenBy(value => WindowWire(value.WindowId), StringComparer.Ordinal))
        {
            stream.Write(BenchmarkContractCodec.Encode(row));
            stream.WriteByte((byte)'\n');
        }

        return stream.ToArray();
    }

    private static string DecisionSequenceSha256(byte[] output)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData("RideBound.Wp6.TinyDecisionSequence.v1\0"u8);

        foreach (var line in ReadLines(output))
        {
            using var document = JsonDocument.Parse(line);

            if (document.RootElement.GetProperty("messageType").GetString() == "decision")
            {
                var decisionHash = document.RootElement.GetProperty("payload")
                    .GetProperty("decisionHash").GetString()!;
                AppendFrame(hash, "decisionHash", Convert.FromHexString(decisionHash));
            }
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static IReadOnlyList<byte[]> ReadLines(byte[] bytes)
    {
        if (bytes.Length == 0 || bytes[^1] != (byte)'\n')
        {
            throw new InvalidDataException("NDJSON identity source is incomplete.");
        }

        var result = new List<byte[]>();
        var offset = 0;

        while (offset < bytes.Length)
        {
            var length = bytes.AsSpan(offset).IndexOf((byte)'\n');
            result.Add(bytes.AsSpan(offset, length).ToArray());
            offset += length + 1;
        }

        return result;
    }

    private static string HashReceiptFrames(
        string domain,
        IEnumerable<TinyRunIdentityReceipt> receipts,
        Func<TinyRunIdentityReceipt, string> value)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes(domain + "\0"));

        foreach (var receipt in receipts.OrderBy(item => item.RunId, StringComparer.Ordinal))
        {
            AppendFrame(hash, "runId", Encoding.UTF8.GetBytes(receipt.RunId));
            AppendFrame(hash, "identity", Encoding.UTF8.GetBytes(value(receipt)));
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static void AppendFrame(
        IncrementalHash hash,
        string tag,
        ReadOnlySpan<byte> value)
    {
        var tagBytes = Encoding.UTF8.GetBytes(tag);
        Span<byte> tagLength = stackalloc byte[sizeof(ushort)];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(
            tagLength,
            checked((ushort)tagBytes.Length));
        hash.AppendData(tagLength);
        hash.AppendData(tagBytes);
        Span<byte> valueLength = stackalloc byte[sizeof(ulong)];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(
            valueLength,
            checked((ulong)value.Length));
        hash.AppendData(valueLength);
        hash.AppendData(value);
    }

    private static async Task<ExternalProcessExecutionResult> ExecuteAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)
            ?? throw new IOException("Could not start an independent WP6 executable.");
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = process.StandardError.ReadToEndAsync(cancellationToken);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        timeoutSource.CancelAfter(timeout);

        try
        {
            await process.WaitForExitAsync(timeoutSource.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Independent WP6 executable exceeded its local limit.");
        }

        return new ExternalProcessExecutionResult(
            process.ExitCode,
            await output,
            await error);
    }

    private static Wp4Identity ReadWp4Identity(byte[] canonical)
    {
        using var document = JsonDocument.Parse(canonical);
        return new Wp4Identity(
            document.RootElement.GetProperty("policyId").GetString()!,
            document.RootElement.GetProperty("policyVersion").GetString()!);
    }

    private static Sha256Hex HashValue(ReadOnlySpan<byte> bytes)
    {
        _ = Sha256Hex.TryCreate(FileSha(bytes), out var value);
        return value!;
    }

    private static ProcessArtifactInventoryEntry RequiredRuntime(
        ProcessArtifactInventory inventory,
        string role) => inventory.Artifacts.Single(value => value.Role == role);

    private static string AppHost(string root, string name)
    {
        var path = OperatingSystem.IsWindows()
            ? Path.Combine(root, name + ".exe")
            : Path.Combine(root, name);
        return File.Exists(path)
            ? path
            : throw new FileNotFoundException("WP6 apphost is missing.", path);
    }

    private static string RequiredFile(string root, string name)
    {
        var path = Path.Combine(root, name);
        return File.Exists(path)
            ? path
            : throw new FileNotFoundException("WP6 assembly is missing.", path);
    }

    private static string Write(string root, string name, byte[] bytes)
    {
        var path = Path.Combine(root, name);
        WriteNew(path, bytes);
        return path;
    }

    private static void WriteNew(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            64 * 1024,
            FileOptions.WriteThrough);
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
    }

    private static BundleArtifactRole Role(string path) => path switch
    {
        "data/benchmark-plan.json" => BundleArtifactRole.Plan,
        "data/failures.ndjson" => BundleArtifactRole.FailureLog,
        "data/exclusions.ndjson" => BundleArtifactRole.ExclusionLog,
        _ when path.StartsWith("data/datasets/", StringComparison.Ordinal) =>
            BundleArtifactRole.Dataset,
        _ when path.StartsWith("data/scenarios/", StringComparison.Ordinal) =>
            BundleArtifactRole.Scenario,
        _ when path.StartsWith("data/runs/", StringComparison.Ordinal) =>
            BundleArtifactRole.RunEvidence,
        _ when path.StartsWith("data/metrics/", StringComparison.Ordinal) =>
            BundleArtifactRole.Metric,
        _ when path.StartsWith("data/provenance/", StringComparison.Ordinal) =>
            BundleArtifactRole.Provenance,
        _ when path.StartsWith("data/source-inventory/", StringComparison.Ordinal) =>
            BundleArtifactRole.SourceInventory,
        _ => throw new InvalidOperationException(path),
    };

    private static string MediaType(string path) =>
        path.EndsWith(".ndjson", StringComparison.Ordinal)
            ? "application/x-ndjson"
            : path.EndsWith(".json", StringComparison.Ordinal)
                ? "application/json"
                : path.EndsWith(".txt", StringComparison.Ordinal)
                    ? "text/plain"
                    : "application/octet-stream";

    private static string ScopeWire(MetricScopeKind value) => value switch
    {
        MetricScopeKind.Epoch => "epoch",
        MetricScopeKind.Request => "request",
        MetricScopeKind.Run => "run",
        MetricScopeKind.Vehicle => "vehicle",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static string WindowWire(MetricWindowId value) => value switch
    {
        MetricWindowId.All => "all",
        MetricWindowId.Drain => "drain",
        MetricWindowId.Scoring => "scoring",
        MetricWindowId.Warmup => "warmup",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static string CanonicalUtc(DateTimeOffset value) =>
        value.UtcDateTime.ToString(
            "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'",
            CultureInfo.InvariantCulture);

    private static string FileSha(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static bool IsInside(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        return relative == "."
            || relative != ".."
                && !relative.StartsWith(
                    ".." + Path.DirectorySeparatorChar,
                    StringComparison.Ordinal)
                && !Path.IsPathRooted(relative);
    }

    private static StringComparer PathComparer() => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private sealed record ResolvedPaths(
        string RepositoryRoot,
        string WorkRoot,
        string BundleDirectory,
        string ReceiptPath,
        string BuildConfiguration);

    private sealed record HarnessAssets(
        string RunnerExecutablePath,
        string RunnerAssemblyPath,
        string ContractsAssemblyPath,
        string OracleExecutablePath,
        string OracleAssemblyPath,
        string VerifierExecutablePath,
        string VerifierAssemblyPath,
        string HarnessAssemblyPath,
        string CommitmentConfigurationPath,
        string B1ConfigurationPath,
        string C1ConfigurationPath,
        string RegistryPath,
        string RunnerRoot);

    private sealed record Wp4Identity(string PolicyId, string PolicyVersion);

    private sealed record MetricEvidence(
        IReadOnlyList<MetricRow> ProductionRows,
        IReadOnlyDictionary<string, MechanicalMetricCalculationResult> ProductionByRun,
        IReadOnlyDictionary<string, byte[]> OracleRowsByRun,
        IReadOnlyList<string> OracleSummaryPaths);
}
