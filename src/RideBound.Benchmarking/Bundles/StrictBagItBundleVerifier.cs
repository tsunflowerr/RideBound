using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using RideBound.Benchmarking.Claims;
using RideBound.Benchmarking.Contracts;
using RideBound.Benchmarking.EndToEnd;
using RideBound.Benchmarking.Execution;
using RideBound.Benchmarking.Metrics;
using RideBound.Benchmarking.Planning;
using RideBound.Benchmarking.Storage;

namespace RideBound.Benchmarking.Bundles;

public sealed class StrictBagItBundleVerifier
{
    private readonly string? expectedVerifierAssemblySha256;

    public StrictBagItBundleVerifier(string? expectedVerifierAssemblySha256 = null)
    {
        if (expectedVerifierAssemblySha256 is not null
            && !BundleSourceInventoryIdentity.IsSha(expectedVerifierAssemblySha256))
        {
            throw new ArgumentException(
                "Expected verifier assembly identity must be exact lowercase SHA-256.",
                nameof(expectedVerifierAssemblySha256));
        }

        this.expectedVerifierAssemblySha256 = expectedVerifierAssemblySha256;
    }

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly string[] RootFiles =
    [
        "README.md",
        "bag-info.txt",
        "bagit.txt",
        "manifest-sha256.txt",
        "tagmanifest-sha256.txt",
        "verify.ps1",
    ];

    public StrictBundleVerificationResult Verify(string bundleDirectory)
    {
        var state = new VerificationState(bundleDirectory);

        try
        {
            VerifyPathSafety(state);
            VerifyLayout(state);
            VerifyBagIt(state);
            VerifyLogicalManifest(state);
            VerifyProvenance(state, expectedVerifierAssemblySha256);
            VerifyPlanConservation(state);
            VerifyTranscripts(state);
            VerifyTerminalLogs(state);
            VerifyMetrics(state);
            VerifyClaims(state);
            return new StrictBundleVerificationResult(
                true,
                state.BundleHash,
                state.PlanHash,
                state.LogicalManifest!.MetricSetHash,
                state.PayloadPaths.Count,
                null);
        }
        catch (BundleVerificationException exception)
        {
            return Invalid(
                state,
                new BundleVerificationIssue(
                    exception.Stage,
                    exception.Code,
                    exception.RelativePath,
                    exception.Message));
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or InvalidDataException
                or ArgumentException
                or InvalidOperationException
                or CryptographicException
                or System.Text.Json.JsonException
                or KeyNotFoundException
                or OverflowException)
        {
            return Invalid(
                state,
                new BundleVerificationIssue(
                    state.CurrentStage,
                    "bundle.verification-error",
                    state.CurrentPath,
                    SafeDiagnostic(exception)));
        }
    }

    private static StrictBundleVerificationResult Invalid(
        VerificationState state,
        BundleVerificationIssue issue) =>
        new(
            false,
            state.BundleHash,
            state.PlanHash,
            state.LogicalManifest?.MetricSetHash,
            state.PayloadPaths.Count,
            issue);

    private static void VerifyPathSafety(VerificationState state)
    {
        state.Enter(BundleVerificationStage.PathSafety, ".");
        var root = new DirectoryInfo(state.Root);

        if (!root.Exists || (root.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            Fail(state, "path.root-unsafe", ".", "Bundle root is missing or is a reparse point.");
        }

        var pending = new Stack<DirectoryInfo>();
        pending.Push(root);
        var casePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalizationPaths = new HashSet<string>(StringComparer.Ordinal);

        while (pending.Count > 0)
        {
            var directory = pending.Pop();

            foreach (var entry in directory.EnumerateFileSystemInfos())
            {
                var relative = Relative(state.Root, entry.FullName);
                state.CurrentPath = relative;

                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0
                    || !StrictBundlePath.IsSafeRelativePath(relative, requireDataPrefix: false))
                {
                    Fail(
                        state,
                        "path.unsafe-entry",
                        relative,
                        "Bundle entry is a reparse point or has an unsafe path.");
                }

                var normalized = relative.Normalize(NormalizationForm.FormC).ToUpperInvariant();

                if (!casePaths.Add(relative) || !normalizationPaths.Add(normalized))
                {
                    Fail(
                        state,
                        "path.collision",
                        relative,
                        "Bundle contains a case or Unicode-normalization path collision.");
                }

                if (entry is DirectoryInfo childDirectory)
                {
                    pending.Push(childDirectory);
                }
                else if (entry is FileInfo)
                {
                    state.AllPaths.Add(relative);
                }
                else
                {
                    Fail(state, "path.unsupported-entry", relative, "Bundle entry type is unsupported.");
                }
            }
        }

        state.AllPaths.Sort(StringComparer.Ordinal);
        state.PayloadPaths.AddRange(
            state.AllPaths.Where(path => path.StartsWith("data/", StringComparison.Ordinal)));
    }

    private static void VerifyLayout(VerificationState state)
    {
        state.Enter(BundleVerificationStage.Layout, ".");
        var rootFiles = Directory.GetFiles(state.Root)
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var rootDirectories = Directory.GetDirectories(state.Root)
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (!rootFiles.SequenceEqual(RootFiles.Order(StringComparer.Ordinal))
            || !rootDirectories.SequenceEqual(new[] { "data" }))
        {
            Fail(
                state,
                "layout.root-inventory",
                ".",
                "Bundle root must contain the exact strict tag-file inventory and data directory.");
        }

        var required = new[]
        {
            "data/benchmark-plan.json",
            "data/bundle-manifest.json",
            "data/claim-check.json",
            "data/exclusions.ndjson",
            "data/failures.ndjson",
            "data/metrics/oracle.ndjson",
            "data/metrics/production.ndjson",
            "data/provenance/machine.json",
            "data/provenance/claim-profile.json",
            "data/provenance/metric-registry.json",
            "data/provenance/reproducibility.json",
            "data/provenance/run-store-plan.json",
            "data/provenance/runtime-inventory.json",
            "data/source-inventory/repository.json",
            "data/verification-report.json",
        };

        if (required.Any(path => !state.PayloadPaths.Contains(path, StringComparer.Ordinal))
            || !state.PayloadPaths.Any(
                path => path.StartsWith("data/datasets/", StringComparison.Ordinal))
            || !state.PayloadPaths.Any(
                path => path.StartsWith("data/scenarios/", StringComparison.Ordinal))
            || !state.PayloadPaths.Any(
                path => path.StartsWith("data/runs/", StringComparison.Ordinal)))
        {
            Fail(state, "layout.missing-required", "data", "Required payload layout is incomplete.");
        }

        foreach (var path in state.PayloadPaths)
        {
            if (!IsAllowedPayloadPath(path))
            {
                Fail(
                    state,
                    "layout.unregistered-path",
                    path,
                    "Payload path is outside the strict WP6 layout.");
            }
        }
    }

    private static void VerifyBagIt(VerificationState state)
    {
        state.Enter(BundleVerificationStage.BagItIntegrity, "bagit.txt");
        var expectedDeclaration = StrictUtf8.GetBytes(
            "BagIt-Version: 1.0\nTag-File-Character-Encoding: UTF-8\n");

        if (!File.ReadAllBytes(Full(state, "bagit.txt")).SequenceEqual(expectedDeclaration))
        {
            Fail(
                state,
                "bagit.declaration-invalid",
                "bagit.txt",
                "Bag declaration must be exact BagIt 1.0 UTF-8 with LF framing and no BOM.");
        }

        VerifyBagInfoAndScript(state);

        var payloadManifest = ParseManifest(state, "manifest-sha256.txt", payloadOnly: true);
        var tagManifest = ParseManifest(state, "tagmanifest-sha256.txt", payloadOnly: false);
        var expectedPayload = state.PayloadPaths.Order(StringComparer.Ordinal).ToArray();
        var expectedTags = RootFiles
            .Where(path => path != "tagmanifest-sha256.txt")
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (!payloadManifest.Keys.SequenceEqual(expectedPayload)
            || !tagManifest.Keys.SequenceEqual(expectedTags))
        {
            Fail(
                state,
                "bagit.manifest-inventory",
                "manifest-sha256.txt",
                "BagIt manifests do not list the exact payload/tag union once each.");
        }

        VerifyManifestHashes(state, payloadManifest);
        VerifyManifestHashes(state, tagManifest);
    }

    private static void VerifyLogicalManifest(VerificationState state)
    {
        const string path = "data/bundle-manifest.json";
        state.Enter(BundleVerificationStage.LogicalManifest, path);
        var bytes = File.ReadAllBytes(Full(state, path));
        var decoded = BenchmarkContractCodec.Decode<LogicalBundleManifest>(bytes);

        if (!decoded.IsSuccess || !bytes.AsSpan().SequenceEqual(decoded.CanonicalBytes))
        {
            Fail(
                state,
                "logical.contract-invalid",
                path,
                "Logical bundle manifest is not exact canonical contract JSON.");
        }

        var manifest = decoded.Value!;
        var expectedPaths = state.PayloadPaths
            .Where(value => value != path)
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (manifest.BundleId != state.BagExternalIdentifier
            || !manifest.Artifacts.Select(value => value.RelativePath).SequenceEqual(expectedPaths))
        {
            Fail(
                state,
                "logical.union-mismatch",
                path,
                "Logical artifact inventory differs from all payload files except itself.");
        }

        foreach (var artifact in manifest.Artifacts)
        {
            state.CurrentPath = artifact.RelativePath;
            var fullPath = Full(state, artifact.RelativePath);
            var info = new FileInfo(fullPath);
            var actualRole = ExpectedRole(artifact.RelativePath);
            var actualMediaType = ExpectedMediaType(artifact.RelativePath);

            if (info.Length != artifact.LengthBytes
                || FileSha(fullPath) != artifact.Sha256
                || artifact.Role != actualRole
                || artifact.MediaType != actualMediaType)
            {
                Fail(
                    state,
                    "logical.artifact-mismatch",
                    artifact.RelativePath,
                    "Logical artifact length/hash/role/media type differs from the payload.");
            }
        }

        state.LogicalManifest = manifest;
        state.BundleHash = BenchmarkIdentity.CalculateBundle(bytes);
    }

    private static void VerifyBagInfoAndScript(VerificationState state)
    {
        const string bagInfoPath = "bag-info.txt";
        var bytes = File.ReadAllBytes(Full(state, bagInfoPath));
        string text;

        try
        {
            text = StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException exception)
        {
            throw new BundleVerificationException(
                BundleVerificationStage.BagItIntegrity,
                "bagit.info-utf8",
                bagInfoPath,
                "Bag metadata is not strict UTF-8.",
                exception);
        }

        if (bytes.Length == 0
            || bytes[^1] != (byte)'\n'
            || bytes.Contains((byte)'\r'))
        {
            Fail(
                state,
                "bagit.info-framing",
                bagInfoPath,
                "Bag metadata must use exact LF framing and no BOM.");
        }

        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var payloadLength = state.PayloadPaths.Sum(
            path => new FileInfo(Full(state, path)).Length);
        var expectedOxum = payloadLength.ToString(CultureInfo.InvariantCulture)
            + "." + state.PayloadPaths.Count.ToString(CultureInfo.InvariantCulture);

        if (lines.Length != 4
            || lines[0] != "Bag-Software-Agent: RideBound strict-bagit-v1"
            || !lines[1].StartsWith("Bagging-Date: ", StringComparison.Ordinal)
            || !DateOnly.TryParseExact(
                lines[1]["Bagging-Date: ".Length..],
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _)
            || !lines[2].StartsWith("External-Identifier: ", StringComparison.Ordinal)
            || !StrictBundlePath.IsArtifactId(
                lines[2]["External-Identifier: ".Length..])
            || lines[3] != "Payload-Oxum: " + expectedOxum)
        {
            Fail(
                state,
                "bagit.info-invalid",
                bagInfoPath,
                "Bag metadata does not match the strict deterministic profile or payload oxum.");
        }

        state.BagExternalIdentifier = lines[2]["External-Identifier: ".Length..];
        var expectedScript = StrictUtf8.GetBytes(
            "param(\n" +
            "  [Parameter(Mandatory=$true)][string]$VerifierAssembly,\n" +
            "  [string]$ReportPath = (Join-Path (Split-Path -Parent $PSScriptRoot) ((Split-Path -Leaf $PSScriptRoot) + '.verification.json'))\n" +
            ")\n" +
            "$ErrorActionPreference = 'Stop'\n" +
            "dotnet $VerifierAssembly --bag $PSScriptRoot --report $ReportPath\n" +
            "exit $LASTEXITCODE\n");

        if (!File.ReadAllBytes(Full(state, "verify.ps1")).SequenceEqual(expectedScript))
        {
            Fail(
                state,
                "bagit.verifier-script-invalid",
                "verify.ps1",
                "Bundled verification script differs from the reviewed strict template.");
        }
    }

    private static void VerifyProvenance(
        VerificationState state,
        string? expectedVerifierAssemblySha256)
    {
        state.Enter(BundleVerificationStage.Provenance, "data/benchmark-plan.json");
        var logical = state.LogicalManifest!;
        var planBytes = File.ReadAllBytes(Full(state, "data/benchmark-plan.json"));
        var planResult = BenchmarkContractCodec.Decode<BenchmarkPlan>(planBytes);

        if (!planResult.IsSuccess || !planBytes.AsSpan().SequenceEqual(planResult.CanonicalBytes))
        {
            Fail(
                state,
                "provenance.plan-invalid",
                "data/benchmark-plan.json",
                "Benchmark plan is not exact canonical contract JSON.");
        }

        var plan = planResult.Value!;
        var planHash = BenchmarkIdentity.CalculateBenchmarkPlan(planBytes);

        if (planHash != logical.PlanHash
            || plan.EvidenceClass != logical.EvidenceClass
            || plan.ClaimProfileId != logical.ClaimProfileId)
        {
            Fail(
                state,
                "provenance.plan-binding",
                "data/benchmark-plan.json",
                "Benchmark plan identity/profile differs from the logical manifest.");
        }

        VerifyDatasetAndScenarioBindings(state, plan);

        var sourcePath = "data/source-inventory/repository.json";
        var sourceBytes = File.ReadAllBytes(Full(state, sourcePath));
        var source = DecodeEvidence<BundleSourceInventory>(state, sourcePath, sourceBytes);
        BundleSourceInventoryIdentity.ValidateInventory(source);
        var runtimePath = "data/provenance/runtime-inventory.json";
        var runtime = DecodeEvidence<BundleRuntimeInventory>(
            state,
            runtimePath,
            File.ReadAllBytes(Full(state, runtimePath)));

        if (runtime.SchemaVersion != "1.0.0"
            || runtime.Artifacts.Count == 0
            || !runtime.Artifacts.SequenceEqual(
                runtime.Artifacts.OrderBy(value => value.Role, StringComparer.Ordinal))
            || ProcessArtifactIdentity.Calculate(runtime.Artifacts) != runtime.InventorySha256)
        {
            Fail(
                state,
                "provenance.runtime-invalid",
                runtimePath,
                "Runtime inventory identity/order does not derive from exact artifacts.");
        }

        var machinePath = "data/provenance/machine.json";
        var machine = DecodeEvidence<BundleMachineProvenance>(
            state,
            machinePath,
            File.ReadAllBytes(Full(state, machinePath)));
        ValidateMachine(state, machine, machinePath);
        var bindingPath = "data/provenance/reproducibility.json";
        var binding = DecodeEvidence<BundleReproducibilityBinding>(
            state,
            bindingPath,
            File.ReadAllBytes(Full(state, bindingPath)));
        var registryPath = "data/provenance/metric-registry.json";
        var registry = MechanicalMetricRegistry.Load(Full(state, registryPath));
        var runStorePath = "data/provenance/run-store-plan.json";
        var runStoreBytes = File.ReadAllBytes(Full(state, runStorePath));
        var runStorePlan = DecodeEvidence<BundleRunStorePlan>(state, runStorePath, runStoreBytes);
        var harnessSourceHash = BundleSourceInventoryIdentity.CalculateComponent(
            "harness",
            source.Entries);
        var oracleSourceHash = BundleSourceInventoryIdentity.CalculateComponent(
            "oracle",
            source.Entries);
        var verifierSourceHash = BundleSourceInventoryIdentity.CalculateComponent(
            "verifier",
            source.Entries);
        var requiredRuntime = runtime.Artifacts.ToDictionary(value => value.Role, StringComparer.Ordinal);

        foreach (var role in new[]
        {
            "contracts-assembly",
            "harness-assembly",
            "oracle-assembly",
            "runner-assembly",
            "runner-executable",
            "verifier-assembly",
        })
        {
            if (!requiredRuntime.ContainsKey(role))
            {
                Fail(
                    state,
                    "provenance.runtime-role-missing",
                    runtimePath,
                    "Runtime inventory omits a required executable/assembly role.");
            }
        }

        if (FileSha(Full(state, sourcePath)) != logical.SourceInventorySha256
            || runtime.InventorySha256 != logical.RuntimeInventorySha256
            || runtime.InventorySha256 != plan.RunnerArtifact.RuntimeInventorySha256
            || harnessSourceHash != plan.HarnessSourceSha256
            || oracleSourceHash != plan.OracleSourceSha256
            || registry.RegistryHash != plan.MetricRegistryHash
            || requiredRuntime["runner-executable"].Sha256
                != plan.RunnerArtifact.RunnerExecutableSha256
            || requiredRuntime["runner-assembly"].Sha256
                != plan.RunnerArtifact.RunnerAssemblySha256
            || requiredRuntime["contracts-assembly"].Sha256
                != plan.RunnerArtifact.ContractsAssemblySha256
            || expectedVerifierAssemblySha256 is not null
                && requiredRuntime["verifier-assembly"].Sha256
                    != expectedVerifierAssemblySha256
            || binding.SchemaVersion != "1.0.1"
            || binding.PlanHash != planHash
            || binding.MetricSetHash != logical.MetricSetHash
            || binding.ClaimProfileSha256
                != FileSha(Full(state, "data/provenance/claim-profile.json"))
            || binding.SourceInventorySha256 != logical.SourceInventorySha256
            || binding.RuntimeInventorySha256 != logical.RuntimeInventorySha256
            || binding.HarnessSourceSha256 != harnessSourceHash
            || binding.OracleSourceSha256 != oracleSourceHash
            || binding.VerifierSourceSha256 != verifierSourceHash
            || binding.RunnerExecutableSha256 != plan.RunnerArtifact.RunnerExecutableSha256
            || binding.RunnerAssemblySha256 != plan.RunnerArtifact.RunnerAssemblySha256
            || binding.ContractsAssemblySha256 != plan.RunnerArtifact.ContractsAssemblySha256
            || binding.MachineProvenanceSha256 != FileSha(Full(state, machinePath))
            || binding.MetricRegistrySha256 != FileSha(Full(state, registryPath))
            || binding.RunStorePlanSha256 != FileSha(Full(state, runStorePath)))
        {
            Fail(
                state,
                "provenance.binding-mismatch",
                bindingPath,
                "Source/runtime/Runner/oracle/machine/registry identities do not cross-bind exactly.");
        }

        state.Plan = plan;
        state.PlanHash = planHash;
        state.SourceInventory = source;
        state.RuntimeInventory = runtime;
        state.MachineProvenance = machine;
        state.ReproducibilityBinding = binding;
        state.RunStorePlan = runStorePlan;
        state.Registry = registry;
    }

    private static void VerifyDatasetAndScenarioBindings(
        VerificationState state,
        BenchmarkPlan plan)
    {
        var scenarioDirectories = Directory.GetDirectories(Full(state, "data/scenarios"))
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (!scenarioDirectories.SequenceEqual(plan.ScenarioHashes.Order(StringComparer.Ordinal)))
        {
            Fail(
                state,
                "provenance.scenario-union",
                "data/scenarios",
                "Scenario directories differ from the exact benchmark-plan scenario set.");
        }

        var datasetIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var scenarioHash in plan.ScenarioHashes)
        {
            var path = $"data/scenarios/{scenarioHash}/scenario.json";

            if (!File.Exists(Full(state, path)))
            {
                Fail(
                    state,
                    "provenance.scenario-missing",
                    path,
                    "Scenario directory omits its canonical scenario content.");
            }

            var scenario = DecodeContractExact<ScenarioContent>(state, path);
            var canonical = BenchmarkContractCodec.Encode(scenario);

            if (BenchmarkIdentity.CalculateScenario(canonical) != scenarioHash
                || scenario.EvidenceClass != plan.EvidenceClass)
            {
                Fail(
                    state,
                    "provenance.scenario-identity",
                    path,
                    "Scenario content identity/evidence class differs from its plan address.");
            }

            datasetIds.Add(scenario.DatasetId);
            state.Scenarios.Add(scenarioHash, scenario);
        }

        var datasetFiles = Directory.GetFiles(Full(state, "data/datasets"), "*.json")
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (Directory.GetFiles(Full(state, "data/datasets")).Length != datasetFiles.Length
            || Directory.GetDirectories(Full(state, "data/datasets")).Length != 0
            || !datasetFiles.Select(Path.GetFileNameWithoutExtension)
                .Order(StringComparer.Ordinal).SequenceEqual(datasetIds.Order(StringComparer.Ordinal)))
        {
            Fail(
                state,
                "provenance.dataset-union",
                "data/datasets",
                "Dataset descriptor files differ from the exact scenarios' dataset set.");
        }

        foreach (var datasetId in datasetIds)
        {
            var path = $"data/datasets/{datasetId}.json";
            var descriptor = DecodeContractExact<DatasetDescriptor>(state, path);

            if (descriptor.DatasetId != datasetId)
            {
                Fail(
                    state,
                    "provenance.dataset-identity",
                    path,
                    "Dataset descriptor ID differs from its payload address.");
            }
        }
    }

    private static void VerifyPlanConservation(VerificationState state)
    {
        state.Enter(BundleVerificationStage.PlanConservation, "data/provenance/run-store-plan.json");
        var plan = state.Plan!;
        var storePlan = state.RunStorePlan!;
        var expectedRuns = BenchmarkPlanCompiler.MaterializeVerifiedGrid(plan, state.PlanHash!);

        if (storePlan.SchemaVersion != "1.0.0"
            || storePlan.PlanHash != state.PlanHash
            || storePlan.DenominatorIds.Count == 0
            || !storePlan.DenominatorIds.SequenceEqual(
                storePlan.DenominatorIds.Order(StringComparer.Ordinal))
            || storePlan.DenominatorIds.Distinct(StringComparer.Ordinal).Count()
                != storePlan.DenominatorIds.Count
            || storePlan.DenominatorIds.Any(value => !StrictBundlePath.IsArtifactId(value))
            || !storePlan.Runs.SequenceEqual(
                storePlan.Runs.OrderBy(value => value.RunId, StringComparer.Ordinal))
            || storePlan.Runs.Count != expectedRuns.Count)
        {
            Fail(
                state,
                "plan.store-plan-invalid",
                "data/provenance/run-store-plan.json",
                "Run-store plan is not a complete canonical denominator/grid declaration.");
        }

        var intents = storePlan.Runs.ToDictionary(value => value.RunId, StringComparer.Ordinal);
        var expectedIds = expectedRuns.Select(value => value.RunId).Order(StringComparer.Ordinal).ToArray();
        var actualRunDirectories = Directory.GetDirectories(Full(state, "data/runs"))
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (!intents.Keys.Order(StringComparer.Ordinal).SequenceEqual(expectedIds)
            || !actualRunDirectories.SequenceEqual(expectedIds))
        {
            Fail(
                state,
                "plan.run-union-mismatch",
                "data/runs",
                "Plan, run-store intents, and terminal run directories are not a one-to-one union.");
        }

        foreach (var expected in expectedRuns)
        {
            var intent = intents[expected.RunId];
            var arm = plan.Arms.Single(value => value.ArmId == expected.ArmId);

            if (intent.PlanHash != expected.PlanHash
                || intent.ScenarioHash != expected.ScenarioHash
                || intent.ArmId != expected.ArmId
                || intent.RepeatIndex != expected.RepeatIndex
                || intent.AttemptIndex != expected.AttemptIndex
                || intent.ExecutionOrdinal != expected.ExecutionOrdinal
                || intent.Warmup != expected.Warmup
                || intent.PolicyConfigurationSha256 != arm.PolicyConfigurationSha256
                || intent.EffectiveConfigurationSha256 != arm.EffectiveConfigurationSha256
                || intent.ComponentSeedHex != expected.SolverSeed.DigestHex
                || intent.RunnerArtifactSha256 != plan.RunnerArtifact.RunnerExecutableSha256
                || intent.HarnessSourceSha256 != plan.HarnessSourceSha256
                || intent.ExpectedArtifactInventorySha256
                    != plan.RunnerArtifact.RuntimeInventorySha256)
            {
                Fail(
                    state,
                    "plan.intent-mismatch",
                    "data/provenance/run-store-plan.json",
                    "Run intent differs from the pre-outcome materialized plan grid.");
            }

            var runRecordPath = $"data/runs/{expected.RunId}/run-record.json";
            var record = DecodeContractExact<RunRecord>(state, runRecordPath);

            if (!RecordMatchesIntent(record, intent))
            {
                Fail(
                    state,
                    "plan.terminal-cross-link",
                    runRecordPath,
                    "Terminal run record is cross-linked or differs from its immutable intent.");
            }

            state.RunRecords.Add(record.RunId, record);
        }
    }

    private static void VerifyTranscripts(VerificationState state)
    {
        state.Enter(BundleVerificationStage.TranscriptProtocol, "data/runs");

        foreach (var intent in state.RunStorePlan!.Runs)
        {
            var relative = $"data/runs/{intent.RunId}";
            state.CurrentPath = relative;

            try
            {
                var verified = AppendOnlyRunStore.VerifyPortableRunDirectory(
                    intent,
                    state.RunStorePlan.DenominatorIds,
                    Full(state, relative));

                if (!BenchmarkContractCodec.Encode(verified).AsSpan().SequenceEqual(
                    File.ReadAllBytes(Full(state, relative + "/run-record.json"))))
                {
                    Fail(
                        state,
                        "transcript.record-divergence",
                        relative,
                        "Portable run verification returned a different terminal record.");
                }
            }
            catch (BundleVerificationException)
            {
                throw;
            }
            catch (Exception exception) when (
                exception is IOException
                    or InvalidDataException
                    or ArgumentException
                    or InvalidOperationException
                    or CryptographicException
                    or System.Text.Json.JsonException)
            {
                throw new BundleVerificationException(
                    BundleVerificationStage.TranscriptProtocol,
                    "transcript.run-invalid",
                    relative,
                    "Run evidence, artifact receipts, resources, transcript, ACK, or checkpoint is invalid.",
                    exception);
            }
        }
    }

    private static void VerifyTerminalLogs(VerificationState state)
    {
        state.Enter(BundleVerificationStage.TerminalLogs, "data/failures.ndjson");
        var failures = ReadNdjson<FailureRecord>(state, "data/failures.ndjson");
        var exclusions = ReadNdjson<ExclusionRecord>(state, "data/exclusions.ndjson");
        var expectedFailureBytes = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var expectedExclusionBytes = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        foreach (var record in state.RunRecords.Values)
        {
            if (record.TerminalStatus == RunTerminalStatus.Failed)
            {
                var path = $"data/runs/{record.RunId}/failure-record.json";
                expectedFailureBytes.Add(record.FailureRecordId!, File.ReadAllBytes(Full(state, path)));
            }
            else if (record.TerminalStatus == RunTerminalStatus.Excluded)
            {
                var path = $"data/runs/{record.RunId}/exclusion-record.json";
                expectedExclusionBytes.Add(record.ExclusionRecordId!, File.ReadAllBytes(Full(state, path)));
            }
        }

        if (!failures.Select(value => value.Value.FailureRecordId)
                .ToHashSet(StringComparer.Ordinal).SetEquals(expectedFailureBytes.Keys)
            || !exclusions.Select(value => value.Value.ExclusionRecordId)
                .ToHashSet(StringComparer.Ordinal).SetEquals(expectedExclusionBytes.Keys)
            || failures.Any(value => !value.Bytes.SequenceEqual(
                expectedFailureBytes[value.Value.FailureRecordId]))
            || exclusions.Any(value => !value.Bytes.SequenceEqual(
                expectedExclusionBytes[value.Value.ExclusionRecordId])))
        {
            Fail(
                state,
                "terminal-log.union-mismatch",
                "data/failures.ndjson",
                "Failure/exclusion logs differ from exact terminal detail records.");
        }

        var combined = failures
            .Select(value => (value.Value.RecordSequence, Id: value.Value.FailureRecordId))
            .Concat(exclusions.Select(value => (value.Value.RecordSequence, Id: value.Value.ExclusionRecordId)))
            .OrderBy(value => value.RecordSequence)
            .ToArray();

        if (combined.Select(value => value.RecordSequence)
                .Where((sequence, index) => sequence != index + 1L).Any()
            || combined.Select(value => value.Id).Distinct(StringComparer.Ordinal).Count()
                != combined.Length)
        {
            Fail(
                state,
                "terminal-log.sequence-invalid",
                "data/failures.ndjson",
                "Failure/exclusion record sequence is not one global gapless append order.");
        }
    }

    private static void VerifyMetrics(VerificationState state)
    {
        state.Enter(BundleVerificationStage.Metrics, "data/metrics/production.ndjson");
        const string productionPath = "data/metrics/production.ndjson";
        const string oraclePath = "data/metrics/oracle.ndjson";
        var productionBytes = File.ReadAllBytes(Full(state, productionPath));
        var oracleBytes = File.ReadAllBytes(Full(state, oraclePath));

        if (!productionBytes.SequenceEqual(oracleBytes))
        {
            Fail(
                state,
                "metric.oracle-mismatch",
                oraclePath,
                "Production and independent oracle metric NDJSON differ byte-for-byte.");
        }

        var rows = ReadNdjson<MetricRow>(state, productionPath);
        var succeeded = state.RunRecords.Values
            .Where(value => value.TerminalStatus == RunTerminalStatus.Succeeded)
            .Select(value => value.RunId)
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (rows.Count == 0 || !rows.Select(value => value.Value.RunId)
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).SequenceEqual(succeeded))
        {
            Fail(
                state,
                "metric.run-union-mismatch",
                productionPath,
                "Metric rows must exist exactly for every succeeded run and no other run.");
        }

        var expectedRowsPerRun = state.Registry!.Definitions.Sum(
            definition => definition.WindowScope == "allWindows" ? 4 : 1);
        var orderedRows = rows.Select(value => value.Value)
            .OrderBy(value => value.RunId, StringComparer.Ordinal)
            .ThenBy(value => value.MetricId, StringComparer.Ordinal)
            .ThenBy(value => ScopeWire(value.ScopeKind), StringComparer.Ordinal)
            .ThenBy(value => value.ScopeId, StringComparer.Ordinal)
            .ThenBy(value => WindowWire(value.WindowId), StringComparer.Ordinal)
            .ToArray();
        const string oracleSummaryRoot = "data/provenance/oracle-execution";
        var summaryFiles = Directory.Exists(Full(state, oracleSummaryRoot))
            ? Directory.GetFiles(Full(state, oracleSummaryRoot))
                .Select(Path.GetFileName)
                .Order(StringComparer.Ordinal)
                .ToArray()
            : [];

        if (summaryFiles.Length != 0
            && !summaryFiles.SequenceEqual(
                succeeded.Select(value => value + ".summary.json")))
        {
            Fail(
                state,
                "metric.oracle-summary-union-mismatch",
                oracleSummaryRoot,
                "Oracle execution summaries must form an exact one-to-one successful-run union.");
        }

        if (!rows.Select(value => value.Value).SequenceEqual(orderedRows))
        {
            Fail(
                state,
                "metric.order-invalid",
                productionPath,
                "Metric rows are not in canonical all-run ordering.");
        }

        foreach (var group in orderedRows.GroupBy(value => value.RunId, StringComparer.Ordinal))
        {
            var record = state.RunRecords[group.Key];
            var values = group.ToArray();

            if (values.Length != expectedRowsPerRun
                || values.Any(
                    row => row.MetricRegistryHash != state.Registry.RegistryHash
                        || row.ScenarioHash != record.ScenarioHash
                        || row.ArmId != record.ArmId
                        || row.RepeatIndex != record.RepeatIndex
                        || row.AttemptIndex != record.AttemptIndex
                        || row.ScopeKind != MetricScopeKind.Run
                        || row.ScopeId != record.RunId
                        || row.CalculatorSourceSha256 != state.Plan!.HarnessSourceSha256)
                || !HasExactRegistryCoverage(state.Registry, values))
            {
                Fail(
                    state,
                    "metric.coverage-invalid",
                    productionPath,
                    "Metric rows do not provide exact registry/window/run coverage.");
            }

            var recomputed = VerifyRawMetricRecomputation(
                state,
                record,
                values,
                productionPath);

            if (summaryFiles.Length != 0)
            {
                VerifyOracleExecutionSummary(state, record, recomputed);
            }
        }

        var metricSetHash = BundleMetricSetIdentity.Calculate(
            state.PlanHash!,
            state.Registry.RegistryHash,
            productionBytes);

        if (metricSetHash != state.LogicalManifest!.MetricSetHash)
        {
            Fail(
                state,
                "metric.set-hash-mismatch",
                productionPath,
                "Bundle metric-set identity does not derive from exact all-run rows.");
        }
    }

    private static MechanicalMetricCalculationResult VerifyRawMetricRecomputation(
        VerificationState state,
        RunRecord record,
        IReadOnlyList<MetricRow> bundledRows,
        string metricPath)
    {
        var runRoot = $"data/runs/{record.RunId}";

        try
        {
            var recomputed = MechanicalMetricCalculator.Calculate(
                new MechanicalMetricCalculationInput(
                    record,
                    state.Scenarios[record.ScenarioHash].TimeWindow,
                    File.ReadAllBytes(Full(state, runRoot + "/run-record.json")),
                    File.ReadAllBytes(Full(state, runRoot + "/input.ndjson")),
                    File.ReadAllBytes(Full(state, runRoot + "/output.ndjson")),
                    File.ReadAllBytes(Full(state, runRoot + "/observation-index.ndjson")),
                    File.ReadAllBytes(Full(state, runRoot + "/resource-samples.ndjson")),
                    state.Registry!,
                    state.Plan!.HarnessSourceSha256));

            if (!recomputed.Rows.SequenceEqual(bundledRows))
            {
                Fail(
                    state,
                    "metric.raw-recompute-mismatch",
                    metricPath,
                    "Bundled metric rows differ from production recomputation over exact raw evidence.");
            }

            return recomputed;
        }
        catch (BundleVerificationException)
        {
            throw;
        }
        catch (MechanicalMetricCalculationException exception)
        {
            throw new BundleVerificationException(
                BundleVerificationStage.Metrics,
                "metric.raw-recompute-invalid",
                metricPath,
                "Raw evidence could not reproduce the bundled production metric rows.",
                exception);
        }
    }

    private static void VerifyOracleExecutionSummary(
        VerificationState state,
        RunRecord record,
        MechanicalMetricCalculationResult recomputed)
    {
        var relative = $"data/provenance/oracle-execution/{record.RunId}.summary.json";
        var summary = DecodeEvidence<OracleExecutionSummary>(
            state,
            relative,
            File.ReadAllBytes(Full(state, relative)));
        var oracleAssembly = state.RuntimeInventory!.Artifacts.Single(
            value => value.Role == "oracle-assembly");

        if (summary.SchemaVersion != "1.0.0"
            || summary.OracleAssemblySha256 != oracleAssembly.Sha256
            || summary.MetricSetHash != recomputed.MetricSetHash
            || summary.SemanticEvidenceSha256 != recomputed.SemanticEvidenceSha256
            || summary.ResourceEvidenceSha256 != recomputed.ResourceEvidenceSha256
            || summary.RowCount != recomputed.Rows.Count)
        {
            Fail(
                state,
                "metric.oracle-summary-mismatch",
                relative,
                "External oracle summary differs from the independently recomputed per-run identities.");
        }
    }

    private static void VerifyClaims(VerificationState state)
    {
        state.Enter(BundleVerificationStage.Claims, "data/claim-check.json");
        const string profilePath = "data/provenance/claim-profile.json";
        var profileBytes = File.ReadAllBytes(Full(state, profilePath));

        if (!profileBytes.SequenceEqual(ArtifactClaimProfileCatalog.GetV1CanonicalBytes())
            || FileSha(Full(state, profilePath)) != ArtifactClaimProfileCatalog.V1Sha256
            || state.ReproducibilityBinding!.ClaimProfileSha256
                != ArtifactClaimProfileCatalog.V1Sha256)
        {
            Fail(
                state,
                "claim.profile-unsupported",
                profilePath,
                "Claim profile differs from the ADR-locked mechanical-only profile.");
        }

        var reportPath = "data/verification-report.json";
        var packagingReport = DecodeEvidence<BundlePackagingVerificationReport>(
            state,
            reportPath,
            File.ReadAllBytes(Full(state, reportPath)));
        var result = ArtifactClaimChecker.Check(
            new ArtifactClaimCheckInput(
                File.ReadAllBytes(Full(state, "README.md")),
                state.LogicalManifest!,
                state.Plan!,
                packagingReport,
                state.MachineProvenance!,
                state.SourceInventory!));

        if (!result.IsValid)
        {
            var witness = result.Report.Witnesses[0];
            Fail(
                state,
                witness.Code,
                witness.RelativePath,
                $"Claim boundary failed at selector '{witness.Selector}' for rule '{witness.RuleId}'.");
        }

        var claimCheckPath = "data/claim-check.json";
        var actual = File.ReadAllBytes(Full(state, claimCheckPath));
        var expected = BundleEvidenceJson.Encode(result.Report);

        if (!actual.SequenceEqual(expected))
        {
            Fail(
                state,
                "claim.report-mismatch",
                claimCheckPath,
                "Claim-check artifact differs from independent recomputation over scoped surfaces.");
        }
    }

    private static bool HasExactRegistryCoverage(
        MechanicalMetricRegistry registry,
        IReadOnlyList<MetricRow> rows)
    {
        var expected = registry.Definitions
            .SelectMany(
                definition => (definition.WindowScope == "allWindows"
                        ? new[]
                        {
                            MetricWindowId.All,
                            MetricWindowId.Drain,
                            MetricWindowId.Scoring,
                            MetricWindowId.Warmup,
                        }
                        : new[] { MetricWindowId.All })
                    .Select(window => (definition.MetricId, definition.MetricVersion, definition.UnitId, window)))
            .OrderBy(value => value.MetricId, StringComparer.Ordinal)
            .ThenBy(value => WindowWire(value.window), StringComparer.Ordinal)
            .ToArray();
        var actual = rows
            .Select(value => (value.MetricId, value.MetricVersion, value.UnitId, value.WindowId))
            .OrderBy(value => value.MetricId, StringComparer.Ordinal)
            .ThenBy(value => WindowWire(value.WindowId), StringComparer.Ordinal)
            .ToArray();
        return actual.SequenceEqual(expected);
    }

    private static IReadOnlyList<NdjsonRow<T>> ReadNdjson<T>(
        VerificationState state,
        string relativePath)
        where T : class, IBenchmarkDocument
    {
        var bytes = File.ReadAllBytes(Full(state, relativePath));

        if (bytes.Length == 0)
        {
            return [];
        }

        if (bytes[^1] != (byte)'\n' || bytes.Contains((byte)'\r'))
        {
            Fail(
                state,
                "ndjson.framing-invalid",
                relativePath,
                "NDJSON evidence must use exact non-empty LF frames.");
        }

        var result = new List<NdjsonRow<T>>();
        var offset = 0;

        while (offset < bytes.Length)
        {
            var lf = bytes.AsSpan(offset).IndexOf((byte)'\n');

            if (lf <= 0)
            {
                Fail(state, "ndjson.empty-frame", relativePath, "NDJSON contains an empty frame.");
            }

            var line = bytes.AsSpan(offset, lf).ToArray();
            offset += lf + 1;
            var decoded = BenchmarkContractCodec.Decode<T>(line);

            if (!decoded.IsSuccess || !line.AsSpan().SequenceEqual(decoded.CanonicalBytes))
            {
                Fail(
                    state,
                    "ndjson.contract-invalid",
                    relativePath,
                    "NDJSON frame is not exact canonical benchmark contract JSON.");
            }

            result.Add(new NdjsonRow<T>(decoded.Value!, line));
        }

        return result;
    }

    private static T DecodeContractExact<T>(VerificationState state, string path)
        where T : class, IBenchmarkDocument
    {
        var bytes = File.ReadAllBytes(Full(state, path));
        var decoded = BenchmarkContractCodec.Decode<T>(bytes);

        if (!decoded.IsSuccess || !bytes.AsSpan().SequenceEqual(decoded.CanonicalBytes))
        {
            Fail(state, "contract.invalid", path, "Payload contract is not exact canonical JSON.");
        }

        return decoded.Value!;
    }

    private static T DecodeEvidence<T>(VerificationState state, string path, byte[] bytes)
    {
        try
        {
            return BundleEvidenceJson.DecodeExact<T>(bytes);
        }
        catch (InvalidDataException exception)
        {
            throw new BundleVerificationException(
                state.CurrentStage,
                "provenance.shape-invalid",
                path,
                "Provenance artifact has an invalid strict canonical shape.",
                exception);
        }
    }

    private static SortedDictionary<string, string> ParseManifest(
        VerificationState state,
        string relativePath,
        bool payloadOnly)
    {
        var bytes = File.ReadAllBytes(Full(state, relativePath));

        if (bytes.Length == 0 || bytes[^1] != (byte)'\n' || bytes.Contains((byte)'\r'))
        {
            Fail(
                state,
                "bagit.manifest-framing",
                relativePath,
                "Manifest must contain exact LF-terminated UTF-8 lines.");
        }

        string text;

        try
        {
            text = StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException exception)
        {
            throw new BundleVerificationException(
                BundleVerificationStage.BagItIntegrity,
                "bagit.manifest-utf8",
                relativePath,
                "Manifest is not strict UTF-8.",
                exception);
        }

        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);

        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length < 67 || line[64..66] != "  ")
            {
                Fail(
                    state,
                    "bagit.manifest-line",
                    relativePath,
                    "Manifest line does not use exact lowercase SHA-256 and two-space separator.");
            }

            var sha = line[..64];
            var path = line[66..];

            if (!BundleSourceInventoryIdentity.IsSha(sha)
                || !StrictBundlePath.IsSafeRelativePath(path, requireDataPrefix: payloadOnly)
                || !payloadOnly && path.StartsWith("data/", StringComparison.Ordinal)
                || !result.TryAdd(path, sha))
            {
                Fail(
                    state,
                    "bagit.manifest-entry",
                    relativePath,
                    "Manifest path/hash is unsafe, duplicated, or in the wrong namespace.");
            }
        }

        return result;
    }

    private static void VerifyManifestHashes(
        VerificationState state,
        IReadOnlyDictionary<string, string> manifest)
    {
        foreach (var entry in manifest)
        {
            state.CurrentPath = entry.Key;

            if (FileSha(Full(state, entry.Key)) != entry.Value)
            {
                Fail(
                    state,
                    "bagit.hash-mismatch",
                    entry.Key,
                    "Manifest SHA-256 differs from exact file bytes.");
            }
        }
    }

    private static void ValidateMachine(
        VerificationState state,
        BundleMachineProvenance machine,
        string path)
    {
        var values = new[]
        {
            machine.OsDescription,
            machine.OsArchitecture,
            machine.ProcessArchitecture,
            machine.DotnetRuntimeVersion,
            machine.DotnetFrameworkDescription,
            machine.FileSystemType,
            machine.PowerModeNote,
            machine.ContainerImageDigest,
        };

        if (machine.SchemaVersion != "1.0.0"
            || machine.LogicalProcessorCount <= 0
            || machine.TotalMemoryBytes <= 0
            || !BundleSourceInventoryIdentity.IsSha(machine.MachineNameSha256)
            || values.Any(value => string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl)))
        {
            Fail(
                state,
                "provenance.machine-invalid",
                path,
                "Machine provenance is incomplete or unsafe.");
        }
    }

    private static bool RecordMatchesIntent(RunRecord record, RunStoreIntent intent) =>
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
        && record.Warmup == intent.Warmup
        && record.ArtifactPreflightSha256 == intent.ExpectedArtifactInventorySha256
        && record.ArtifactPostflightSha256 == intent.ExpectedArtifactInventorySha256;

    private static BundleArtifactRole ExpectedRole(string path) => path switch
    {
        "data/benchmark-plan.json" => BundleArtifactRole.Plan,
        "data/failures.ndjson" => BundleArtifactRole.FailureLog,
        "data/exclusions.ndjson" => BundleArtifactRole.ExclusionLog,
        "data/claim-check.json" => BundleArtifactRole.ClaimCheck,
        "data/verification-report.json" => BundleArtifactRole.VerificationReport,
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
        _ => throw new InvalidDataException("Payload role is not registered."),
    };

    private static string ExpectedMediaType(string path) =>
        path.EndsWith(".ndjson", StringComparison.Ordinal)
            ? "application/x-ndjson"
            : path.EndsWith(".json", StringComparison.Ordinal)
                ? "application/json"
                : path.EndsWith(".txt", StringComparison.Ordinal)
                    ? "text/plain"
                    : "application/octet-stream";

    private static bool IsAllowedPayloadPath(string path)
    {
        if (path is "data/benchmark-plan.json"
            or "data/bundle-manifest.json"
            or "data/claim-check.json"
            or "data/exclusions.ndjson"
            or "data/failures.ndjson"
            or "data/metrics/oracle.ndjson"
            or "data/metrics/production.ndjson"
            or "data/provenance/machine.json"
            or "data/provenance/claim-profile.json"
            or "data/provenance/metric-registry.json"
            or "data/provenance/reproducibility.json"
            or "data/provenance/run-store-plan.json"
            or "data/provenance/runtime-inventory.json"
            or "data/source-inventory/repository.json"
            or "data/verification-report.json")
        {
            return true;
        }

        var parts = path.Split('/');
        return parts.Length == 3
                && parts[0] == "data"
                && parts[1] == "datasets"
                && parts[2].EndsWith(".json", StringComparison.Ordinal)
            || parts.Length >= 4
                && parts[0] == "data"
                && parts[1] == "scenarios"
                && BundleSourceInventoryIdentity.IsSha(parts[2])
            || parts.Length == 4
                && parts[0] == "data"
                && parts[1] == "runs"
                && StrictBundlePath.IsArtifactId(parts[2])
            || parts.Length == 4
                && parts[0] == "data"
                && parts[1] == "provenance"
                && parts[2] == "oracle-execution"
                && parts[3].EndsWith(".summary.json", StringComparison.Ordinal)
                && StrictBundlePath.IsArtifactId(parts[3][..^".summary.json".Length]);
    }

    private static string ScopeWire(MetricScopeKind value) => value switch
    {
        MetricScopeKind.Run => "run",
        MetricScopeKind.Epoch => "epoch",
        MetricScopeKind.Request => "request",
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

    private static string FileSha(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81_920,
            FileOptions.SequentialScan);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    private static string Full(VerificationState state, string relativePath)
    {
        var root = state.RootWithSeparator;
        var full = Path.GetFullPath(
            Path.Combine(state.Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            Fail(
                state,
                "path.traversal",
                relativePath,
                "Resolved bundle path escapes the root.");
        }

        return full;
    }

    private static string Relative(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');

    private static void Fail(
        VerificationState state,
        string code,
        string path,
        string message) =>
        throw new BundleVerificationException(state.CurrentStage, code, path, message);

    private static string SafeDiagnostic(Exception exception) => exception switch
    {
        UnauthorizedAccessException => "Bundle entry could not be read.",
        CryptographicException => "Bundle cryptographic validation failed.",
        OverflowException => "Bundle numeric validation overflowed.",
        _ => exception.Message.Length <= 256
            && !exception.Message.Contains(Path.DirectorySeparatorChar)
                ? exception.Message
                : "Bundle verification failed at the current ordered stage.",
    };

    private sealed class VerificationState
    {
        public VerificationState(string root)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(root);
            Root = Path.GetFullPath(root);
            RootWithSeparator = Root + Path.DirectorySeparatorChar;
        }

        public string Root { get; }

        public string RootWithSeparator { get; }

        public BundleVerificationStage CurrentStage { get; private set; } =
            BundleVerificationStage.PathSafety;

        public string CurrentPath { get; set; } = ".";

        public List<string> AllPaths { get; } = [];

        public List<string> PayloadPaths { get; } = [];

        public LogicalBundleManifest? LogicalManifest { get; set; }

        public string? BundleHash { get; set; }

        public string? BagExternalIdentifier { get; set; }

        public BenchmarkPlan? Plan { get; set; }

        public string? PlanHash { get; set; }

        public BundleSourceInventory? SourceInventory { get; set; }

        public BundleRuntimeInventory? RuntimeInventory { get; set; }

        public BundleMachineProvenance? MachineProvenance { get; set; }

        public BundleReproducibilityBinding? ReproducibilityBinding { get; set; }

        public BundleRunStorePlan? RunStorePlan { get; set; }

        public MechanicalMetricRegistry? Registry { get; set; }

        public Dictionary<string, RunRecord> RunRecords { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, ScenarioContent> Scenarios { get; } = new(StringComparer.Ordinal);

        public void Enter(BundleVerificationStage stage, string path)
        {
            CurrentStage = stage;
            CurrentPath = path;
        }
    }

    private sealed record NdjsonRow<T>(T Value, byte[] Bytes);
}
