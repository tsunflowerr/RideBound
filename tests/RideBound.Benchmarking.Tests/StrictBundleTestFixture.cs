using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using RideBound.Benchmarking.Bundles;
using RideBound.Benchmarking.Contracts;
using RideBound.Benchmarking.Execution;
using RideBound.Benchmarking.Metrics;
using RideBound.Benchmarking.Planning;
using RideBound.Benchmarking.Storage;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;

namespace RideBound.Benchmarking.Tests;

internal sealed record StrictBundleFixture(
    StrictBagItBundleBuildResult Build,
    string BundleRoot,
    string PlanHash,
    string RunId,
    string MetricSetHash,
    string VerifierAssemblyPath);

internal static class StrictBundleTestFixture
{
    private static readonly string[] DenominatorIds = ["planned-runs", "valid-runs"];

    public static async Task<StrictBundleFixture> CreateAsync(
        TestDirectory temp,
        string bundleName = "bundle-a",
        bool mixedTerminals = false)
    {
        var repository = FindRepositoryRoot();
        var sourceRoot = Path.Combine(temp.Root, "bundle-inputs", bundleName);
        Directory.CreateDirectory(sourceRoot);
        var registrySource = Path.Combine(
            repository,
            "benchmarks",
            "fixtures",
            "wp6",
            "metrics",
            "mechanical-metric-registry-v1.json");
        var registry = MechanicalMetricRegistry.Load(registrySource);
        var scenarioSource = Path.Combine(
            repository,
            "benchmarks",
            "fixtures",
            "wp6",
            "contracts",
            "positive",
            "scenario-content.json");
        var scenarioDecoded = BenchmarkContractCodec.Decode<ScenarioContent>(
            File.ReadAllBytes(scenarioSource));
        Assert.True(scenarioDecoded.IsSuccess, scenarioDecoded.Error?.ToString());
        var scenarioBytes = scenarioDecoded.CanonicalBytes!;
        var scenario = scenarioDecoded.Value!;
        var scenarioHash = BenchmarkIdentity.CalculateScenario(scenarioBytes);
        var datasetSource = Path.Combine(
            repository,
            "benchmarks",
            "fixtures",
            "wp6",
            "contracts",
            "positive",
            "dataset-descriptor.json");
        var datasetDecoded = BenchmarkContractCodec.Decode<DatasetDescriptor>(
            File.ReadAllBytes(datasetSource));
        Assert.True(datasetDecoded.IsSuccess, datasetDecoded.Error?.ToString());
        var sourceInventory = CreateSourceInventory(repository);
        var sourceInventoryBytes = BundleEvidenceJson.Encode(sourceInventory);
        var sourceInventoryPath = Write(
            sourceRoot,
            "source-inventory.json",
            sourceInventoryBytes);
        var sourceInventorySha256 = FileSha(sourceInventoryPath);
        var harnessSourceSha256 = BundleSourceInventoryIdentity.CalculateComponent(
            "harness",
            sourceInventory.Entries);
        var oracleSourceSha256 = BundleSourceInventoryIdentity.CalculateComponent(
            "oracle",
            sourceInventory.Entries);
        var verifierSourceSha256 = BundleSourceInventoryIdentity.CalculateComponent(
            "verifier",
            sourceInventory.Entries);
        var verifierAssemblyPath = ToolAssembly(
            repository,
            "RideBound.Wp6BundleVerify",
            "RideBound.Wp6BundleVerify.dll");
        var oracleAssemblyPath = ToolAssembly(
            repository,
            "RideBound.Wp6MetricOracle",
            "RideBound.Wp6MetricOracle.dll");
        var runnerAssemblyPath = ProjectAssembly(
            repository,
            "src",
            "RideBound.Runner",
            "RideBound.Runner.dll");
        var contractsAssemblyPath = typeof(ProtocolEnvelope).Assembly.Location;
        var harnessAssemblyPath = typeof(StrictBagItBundleBuilder).Assembly.Location;
        var runtimeEntries = new[]
        {
            RuntimeEntry("contracts-assembly", contractsAssemblyPath),
            RuntimeEntry("harness-assembly", harnessAssemblyPath),
            RuntimeEntry("oracle-assembly", oracleAssemblyPath),
            RuntimeEntry("runner-assembly", runnerAssemblyPath),
            RuntimeEntry("runner-executable", runnerAssemblyPath),
            RuntimeEntry("verifier-assembly", verifierAssemblyPath),
        }.OrderBy(value => value.Role, StringComparer.Ordinal).ToArray();
        var runtimeSha256 = ProcessArtifactIdentity.Calculate(runtimeEntries);
        var runtimeInventory = new BundleRuntimeInventory(
            "1.0.0",
            runtimeSha256,
            runtimeEntries);
        var runtimePath = Write(
            sourceRoot,
            "runtime-inventory.json",
            BundleEvidenceJson.Encode(runtimeInventory));
        var runnerSha256 = runtimeEntries.Single(value => value.Role == "runner-executable").Sha256;
        var contractsSha256 = runtimeEntries.Single(value => value.Role == "contracts-assembly").Sha256;
        var plan = CreatePlan(
            scenarioHash,
            registry.RegistryHash,
            runnerSha256,
            contractsSha256,
            runtimeSha256,
            harnessSourceSha256,
            oracleSourceSha256);
        var planBytes = BenchmarkContractCodec.Encode(plan);
        var planHash = BenchmarkIdentity.CalculateBenchmarkPlan(planBytes);
        var plannedRuns = BenchmarkPlanCompiler.MaterializeVerifiedGrid(plan, planHash);
        var intents = plannedRuns
            .Select(
                planned => new RunStoreIntent(
                    planned.RunId,
                    planHash,
                    planned.ScenarioHash,
                    planned.ArmId,
                    planned.RepeatIndex,
                    planned.AttemptIndex,
                    plan.Arms[0].PolicyConfigurationSha256,
                    plan.Arms[0].EffectiveConfigurationSha256,
                    planned.SolverSeed.DigestHex,
                    plan.RunnerArtifact.RunnerExecutableSha256,
                    plan.HarnessSourceSha256,
                    planned.ExecutionOrdinal,
                    planned.Warmup,
                    runtimeSha256))
            .ToArray();
        var storeRoot = Path.Combine(temp.Root, "stores", bundleName);
        var store = new AppendOnlyRunStore(
            new AppendOnlyRunStoreOptions(
                storeRoot,
                temp.RepositoryRoot,
                MaximumEvidenceFileBytes: 4_000_000));
        await store.InitializePlanAsync(planHash, intents, DenominatorIds);
        var commits = new List<RunStoreCommitResult>();

        for (var index = 0; index < intents.Length; index++)
        {
            var intent = intents[index];
            var terminalStatus = mixedTerminals
                ? index switch
                {
                    0 => RunTerminalStatus.Succeeded,
                    1 => RunTerminalStatus.Failed,
                    _ => RunTerminalStatus.Excluded,
                }
                : RunTerminalStatus.Succeeded;
            var evidence = CreateRunEvidence(
                repository,
                sourceRoot,
                intent,
                runtimeEntries,
                runtimeSha256,
                terminalStatus);
            var submission = new TerminalRunSubmission(
                intent,
                terminalStatus,
                "2026-08-11T10:00:00Z",
                "2026-08-11T10:00:01Z",
                1_000,
                500,
                10_000_000,
                2,
                runtimeSha256,
                runtimeSha256,
                evidence,
                ExitCode: terminalStatus == RunTerminalStatus.Succeeded ? 0 : 17,
                Failure: terminalStatus == RunTerminalStatus.Failed
                    ? new RunFailureInput(
                        "process.crash",
                        "execution",
                        500,
                        "runner-supervisor",
                        RawRunEvidenceRole.StandardError,
                        "External process exited before protocol completion.",
                        DenominatorIds)
                    : null,
                Exclusion: terminalStatus == RunTerminalStatus.Excluded
                    ? new RunExclusionInput(
                        "arm.missing-required-capability",
                        "1.0.0",
                        new string('9', 64),
                        "preflight",
                        "benchmark-arm",
                        intent.ArmId,
                        true,
                        RawRunEvidenceRole.ArtifactPreflight,
                        DenominatorIds,
                        "Declared capability is absent before execution.")
                    : null);
            commits.Add(await store.CommitAsync(submission));
        }

        var storeVerification = store.VerifyPlan(planHash);
        Assert.True(
            storeVerification.IsValid,
            string.Join(";", storeVerification.Issues.Select(value => value.Code)));
        var metricRows = new List<MetricRow>();
        var metricResults = new Dictionary<string, MechanicalMetricCalculationResult>(
            StringComparer.Ordinal);

        foreach (var commit in commits.Where(
            value => value.RunRecord.TerminalStatus == RunTerminalStatus.Succeeded))
        {
            var canonicalRunRecord = File.ReadAllBytes(
                Path.Combine(commit.RunDirectory, "run-record.json"));
            var metricResult = MechanicalMetricCalculator.Calculate(
                new MechanicalMetricCalculationInput(
                    commit.RunRecord,
                    scenario.TimeWindow,
                    canonicalRunRecord,
                    File.ReadAllBytes(Path.Combine(commit.RunDirectory, "input.ndjson")),
                    File.ReadAllBytes(Path.Combine(commit.RunDirectory, "output.ndjson")),
                    File.ReadAllBytes(Path.Combine(commit.RunDirectory, "observation-index.ndjson")),
                    File.ReadAllBytes(Path.Combine(commit.RunDirectory, "resource-samples.ndjson")),
                    registry,
                    harnessSourceSha256));
            metricRows.AddRange(metricResult.Rows);
            metricResults.Add(commit.RunRecord.RunId, metricResult);
        }

        var productionBytes = EncodeNdjson(
            metricRows.OrderBy(value => value.RunId, StringComparer.Ordinal)
                .ThenBy(value => value.MetricId, StringComparer.Ordinal)
                .ThenBy(value => value.ScopeKind.ToString(), StringComparer.Ordinal)
                .ThenBy(value => value.ScopeId, StringComparer.Ordinal)
                .ThenBy(value => MetricWindowWire(value.WindowId), StringComparer.Ordinal));
        var metricSetHash = BundleMetricSetIdentity.Calculate(
            planHash,
            registry.RegistryHash,
            productionBytes);
        var productionPath = Write(sourceRoot, "production.ndjson", productionBytes);
        var oraclePath = Write(sourceRoot, "oracle.ndjson", productionBytes);
        var failuresPath = Write(
            sourceRoot,
            "failures.ndjson",
            EncodeDetailLog(commits, "failure-record.json"));
        var exclusionsPath = Write(
            sourceRoot,
            "exclusions.ndjson",
            EncodeDetailLog(commits, "exclusion-record.json"));
        var machine = new BundleMachineProvenance(
            "1.0.0",
            "Windows test host",
            "x64",
            "x64",
            Environment.ProcessorCount,
            16_000_000_000,
            Environment.Version.ToString(),
            System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            "NTFS",
            "test-host-unspecified",
            FileSha(Encoding.UTF8.GetBytes(Environment.MachineName)),
            "none");
        var machinePath = Write(
            sourceRoot,
            "machine.json",
            BundleEvidenceJson.Encode(machine));
        var runStorePlanPath = Path.Combine(storeRoot, planHash, "plan-store.json");
        var reproducibility = new BundleReproducibilityBinding(
            "1.0.1",
            planHash,
            metricSetHash,
            RideBound.Benchmarking.Claims.ArtifactClaimProfileCatalog.V1Sha256,
            sourceInventorySha256,
            runtimeSha256,
            harnessSourceSha256,
            oracleSourceSha256,
            verifierSourceSha256,
            plan.RunnerArtifact.RunnerExecutableSha256,
            plan.RunnerArtifact.RunnerAssemblySha256,
            plan.RunnerArtifact.ContractsAssemblySha256,
            FileSha(machinePath),
            FileSha(registrySource),
            FileSha(runStorePlanPath));
        var reproducibilityPath = Write(
            sourceRoot,
            "reproducibility.json",
            BundleEvidenceJson.Encode(reproducibility));
        var planPath = Write(sourceRoot, "benchmark-plan.json", planBytes);
        var scenarioPath = Write(sourceRoot, "scenario.json", scenarioBytes);
        var datasetPath = Write(
            sourceRoot,
            "dataset.json",
            datasetDecoded.CanonicalBytes!);
        var registryPath = Write(
            sourceRoot,
            "metric-registry.json",
            File.ReadAllBytes(registrySource));
        var payloads = new List<(string Source, string Relative)>
        {
            (planPath, "data/benchmark-plan.json"),
            (datasetPath, $"data/datasets/{datasetDecoded.Value!.DatasetId}.json"),
            (scenarioPath, $"data/scenarios/{scenarioHash}/scenario.json"),
            (failuresPath, "data/failures.ndjson"),
            (exclusionsPath, "data/exclusions.ndjson"),
            (productionPath, "data/metrics/production.ndjson"),
            (oraclePath, "data/metrics/oracle.ndjson"),
            (reproducibilityPath, "data/provenance/reproducibility.json"),
            (runtimePath, "data/provenance/runtime-inventory.json"),
            (machinePath, "data/provenance/machine.json"),
            (registryPath, "data/provenance/metric-registry.json"),
            (runStorePlanPath, "data/provenance/run-store-plan.json"),
            (sourceInventoryPath, "data/source-inventory/repository.json"),
        };
        var oracleAssemblySha256 = runtimeEntries.Single(
            value => value.Role == "oracle-assembly").Sha256;

        foreach (var pair in metricResults.OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            var summaryPath = Write(
                sourceRoot,
                pair.Key + ".summary.json",
                BundleEvidenceJson.Encode(
                    new
                    {
                        metricSetHash = pair.Value.MetricSetHash,
                        oracleAssemblySha256,
                        resourceEvidenceSha256 = pair.Value.ResourceEvidenceSha256,
                        rowCount = pair.Value.Rows.Count,
                        schemaVersion = "1.0.0",
                        semanticEvidenceSha256 = pair.Value.SemanticEvidenceSha256,
                    }));
            payloads.Add(
                (summaryPath,
                    $"data/provenance/oracle-execution/{pair.Key}.summary.json"));
        }

        foreach (var commit in commits)
        {
            foreach (var file in Directory.GetFiles(commit.RunDirectory).Order(StringComparer.Ordinal))
            {
                payloads.Add(
                    (file, $"data/runs/{commit.RunRecord.RunId}/{Path.GetFileName(file)}"));
            }
        }

        var sources = payloads
            .Select(
                value => new BundlePayloadSource(
                    Path.GetFullPath(value.Source),
                    value.Relative,
                    MediaType(value.Relative),
                    Role(value.Relative),
                    "wp6-test-fixture-v1",
                    new[] { "fixture" }))
            .OrderBy(value => value.RelativePath, StringComparer.Ordinal)
            .ToArray();
        var destination = Path.Combine(temp.Root, bundleName);
        var build = await new StrictBagItBundleBuilder().BuildAsync(
            new StrictBagItBundleRequest(
                destination,
                "wp6-test-bundle",
                EvidenceClass.Mechanical,
                "wp6-mechanical-only-v1",
                metricSetHash,
                sourceInventorySha256,
                runtimeSha256,
                "2026-08-11",
                sources));
        return new StrictBundleFixture(
            build,
            destination,
            planHash,
            plannedRuns[0].RunId,
            metricSetHash,
            verifierAssemblyPath);
    }

    public static string FindRepositoryRoot()
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

    public static string FileSha(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    public static string FileSha(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static BundleSourceInventory CreateSourceInventory(string repository)
        => BundleSourceInventoryCapture.Capture(
            repository,
            [
                new BundleSourceComponentSelection(
                    "harness",
                    ["src/RideBound.Benchmarking"]),
                new BundleSourceComponentSelection(
                    "oracle",
                    ["tools/RideBound.Wp6MetricOracle"]),
                new BundleSourceComponentSelection(
                    "verifier",
                    ["tools/RideBound.Wp6BundleVerify"]),
            ]);

    private static BenchmarkPlan CreatePlan(
        string scenarioHash,
        string registryHash,
        string runnerSha256,
        string contractsSha256,
        string runtimeSha256,
        string harnessSourceSha256,
        string oracleSourceSha256) =>
        new(
            BenchmarkContractVersions.V1,
            "wp6-bundle-test-plan",
            EvidenceClass.Mechanical,
            "wp6-mechanical-only-v1",
            [scenarioHash],
            [
                new BenchmarkArm(
                    "b1",
                    "rolling-cost",
                    "1.0.0",
                    new string('1', 64),
                    new string('4', 64),
                    "wp4-common-generator-v1",
                    1_000,
                    "wp3-validator-v1",
                    "none",
                    "1.0.0",
                    0,
                    new string('3', 64),
                    "mechanical-single-arm-v1"),
            ],
            "mechanical-single-arm-v1",
            new string('5', 64),
            0,
            3,
            "hash-counterbalanced-v1",
            "wp6-local-mechanical-v1",
            "wp6-failure-v1.0.2",
            "wp6-exclusion-v1",
            registryHash,
            new RunnerArtifactIdentity(
                runnerSha256,
                runnerSha256,
                contractsSha256,
                runtimeSha256,
                "runner-ndjson-v1"),
            harnessSourceSha256,
            oracleSourceSha256);

    private static IReadOnlyList<RawRunEvidenceSource> CreateRunEvidence(
        string repository,
        string sourceRoot,
        RunStoreIntent intent,
        IReadOnlyList<ProcessArtifactInventoryEntry> runtimeEntries,
        string runtimeSha256,
        RunTerminalStatus terminalStatus)
    {
        var root = Path.Combine(sourceRoot, "run-source", intent.RunId);
        Directory.CreateDirectory(root);
        var transcripts = terminalStatus == RunTerminalStatus.Excluded
            ? (Input: Array.Empty<byte>(), Output: Array.Empty<byte>())
            : CreateTranscripts(repository, intent);
        Write(root, "input.ndjson", transcripts.Input);
        Write(root, "output.ndjson", transcripts.Output);
        Write(
            root,
            "stderr.txt",
            terminalStatus == RunTerminalStatus.Failed
                ? "deterministic failure\n"u8.ToArray()
                : []);
        Write(
            root,
            "resource-samples.ndjson",
            "{\"elapsedMs\":1000,\"observedCpuTimeMs\":500,\"observedProcessCount\":2,\"observedWorkingSetBytes\":10000000}\n"u8.ToArray());
        Write(
            root,
            "artifact-preflight.json",
            ArtifactReceipt("preflight", runtimeEntries, runtimeSha256));
        Write(
            root,
            "artifact-postflight.json",
            ArtifactReceipt("postflight", runtimeEntries, runtimeSha256));
        return new[]
        {
            (RawRunEvidenceRole.Input, "input.ndjson"),
            (RawRunEvidenceRole.Output, "output.ndjson"),
            (RawRunEvidenceRole.StandardError, "stderr.txt"),
            (RawRunEvidenceRole.ResourceSamples, "resource-samples.ndjson"),
            (RawRunEvidenceRole.ArtifactPreflight, "artifact-preflight.json"),
            (RawRunEvidenceRole.ArtifactPostflight, "artifact-postflight.json"),
        }.Select(value => RawRunEvidenceSource.Pin(value.Item1, Path.Combine(root, value.Item2)))
            .ToArray();
    }

    private static (byte[] Input, byte[] Output) CreateTranscripts(
        string repository,
        RunStoreIntent intent)
    {
        var inputPath = Path.Combine(
            repository,
            "benchmarks",
            "schemas",
            "fixtures",
            "runner",
            "full-tiny-transcript.input.ndjson");
        var outputPath = Path.Combine(
            repository,
            "benchmarks",
            "schemas",
            "fixtures",
            "runner",
            "full-tiny-transcript.expected.ndjson");
        var input = File.ReadAllLines(inputPath);
        var output = File.ReadAllLines(outputPath);
        var initialize = JsonNode.Parse(input[1])!.AsObject();
        initialize["runId"] = intent.RunId;
        var manifest = initialize["payload"]!["manifest"]!.AsObject();
        manifest["scenarioContentHash"] = intent.ScenarioHash;
        manifest["binarySha256"] = intent.RunnerArtifactSha256;
        manifest["policyConfigurationHash"] = intent.PolicyConfigurationSha256;
        manifest["masterSeed"] = BenchmarkSeed.ToNonNegativeInt32(intent.ComponentSeedHex);
        var initializeBytes = CanonicalNode(initialize);
        var initializeEnvelope = ProtocolEnvelopeCodec.Decode(initializeBytes);
        Assert.True(initializeEnvelope.IsSuccess, initializeEnvelope.Error?.Message);
        var initializePayload = InitializeRunPayloadCodec.Decode(initializeEnvelope.Envelope!.Payload);
        Assert.True(initializePayload.IsSuccess, initializePayload.Error?.Message);
        var manifestHash = ProtocolHash.CalculateManifestHash(initializePayload.Value!.Manifest);
        var initialized = JsonNode.Parse(output[1])!.AsObject();
        initialized["runId"] = intent.RunId;
        initialized["payload"]!["manifestHash"] = manifestHash.Value;
        var eventBatch = JsonNode.Parse(input[2])!.AsObject();
        eventBatch["runId"] = intent.RunId;
        var decision = JsonNode.Parse(output[2])!.AsObject();
        decision["runId"] = intent.RunId;
        var decisionHash = decision["payload"]!["decisionHash"]!.GetValue<string>();
        var acknowledgement = JsonNode.Parse(
            "{\"epochId\":1,\"messageType\":\"decisionApplied\",\"payload\":{}," +
            "\"runId\":\"x\",\"scenarioId\":\"manhattan-20260729-a\"," +
            "\"schemaVersion\":\"1.0.0\",\"simTimeMs\":100}")!.AsObject();
        acknowledgement["runId"] = intent.RunId;
        acknowledgement["payload"]!["decisionHash"] = decisionHash;
        return (
            EncodeTranscript(
            [
                CanonicalJson.Canonicalize(Encoding.UTF8.GetBytes(input[0])),
                initializeBytes,
                CanonicalNode(eventBatch),
                CanonicalNode(acknowledgement),
                CanonicalJson.Canonicalize(Encoding.UTF8.GetBytes(input[^1])),
            ]),
            EncodeTranscript(
            [
                CanonicalJson.Canonicalize(Encoding.UTF8.GetBytes(output[0])),
                CanonicalNode(initialized),
                CanonicalNode(decision),
            ]));
    }

    private static byte[] ArtifactReceipt(
        string stage,
        IReadOnlyList<ProcessArtifactInventoryEntry> artifacts,
        string inventorySha256) =>
        BundleEvidenceJson.Encode(
            new
            {
                Artifacts = artifacts,
                InventorySha256 = inventorySha256,
                LaunchCommandSha256 = new string('e', 64),
                SchemaVersion = "1.0.0",
                Stage = stage,
            });

    private static byte[] EncodeNdjson(IEnumerable<MetricRow> rows)
    {
        using var stream = new MemoryStream();

        foreach (var row in rows)
        {
            stream.Write(BenchmarkContractCodec.Encode(row));
            stream.WriteByte((byte)'\n');
        }

        return stream.ToArray();
    }

    private static byte[] EncodeDetailLog(
        IEnumerable<RunStoreCommitResult> commits,
        string fileName)
    {
        var rows = commits
            .Select(commit => Path.Combine(commit.RunDirectory, fileName))
            .Where(File.Exists)
            .Select(
                path =>
                {
                    var bytes = File.ReadAllBytes(path);
                    using var document = JsonDocument.Parse(bytes);
                    return (
                        Sequence: document.RootElement.GetProperty("recordSequence").GetInt64(),
                        Bytes: bytes);
                })
            .OrderBy(value => value.Sequence)
            .ToArray();
        using var stream = new MemoryStream();

        foreach (var row in rows)
        {
            stream.Write(row.Bytes);
            stream.WriteByte((byte)'\n');
        }

        return stream.ToArray();
    }

    private static string MetricWindowWire(MetricWindowId value) => value switch
    {
        MetricWindowId.All => "all",
        MetricWindowId.Drain => "drain",
        MetricWindowId.Scoring => "scoring",
        MetricWindowId.Warmup => "warmup",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static byte[] EncodeTranscript(IEnumerable<byte[]> rows)
    {
        using var stream = new MemoryStream();

        foreach (var row in rows)
        {
            stream.Write(row);
            stream.WriteByte((byte)'\n');
        }

        return stream.ToArray();
    }

    private static byte[] CanonicalNode(JsonNode node) =>
        CanonicalJson.Canonicalize(Encoding.UTF8.GetBytes(node.ToJsonString()));

    private static ProcessArtifactInventoryEntry RuntimeEntry(string role, string path)
    {
        var info = new FileInfo(path);
        Assert.True(info.Exists, path);
        return new ProcessArtifactInventoryEntry(role, info.Name, info.Length, FileSha(path));
    }

    private static string ToolAssembly(string repository, string project, string file) =>
        ProjectAssembly(repository, "tools", project, file);

    private static string ProjectAssembly(
        string repository,
        string folder,
        string project,
        string file)
    {
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var path = Path.Combine(
            repository,
            folder,
            project,
            "bin",
            configuration,
            "net10.0",
            file);
        Assert.True(File.Exists(path), path);
        return path;
    }

    private static string Write(string root, string name, ReadOnlySpan<byte> bytes)
    {
        var path = Path.Combine(root, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes.ToArray());
        return path;
    }

    private static BundleArtifactRole Role(string path) => path switch
    {
        "data/benchmark-plan.json" => BundleArtifactRole.Plan,
        "data/failures.ndjson" => BundleArtifactRole.FailureLog,
        "data/exclusions.ndjson" => BundleArtifactRole.ExclusionLog,
        "data/claim-check.json" => BundleArtifactRole.ClaimCheck,
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
}
