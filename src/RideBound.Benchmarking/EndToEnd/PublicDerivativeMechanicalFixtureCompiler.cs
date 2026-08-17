using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RideBound.Benchmarking.Contracts;
using RideBound.Benchmarking.Planning;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;

namespace RideBound.Benchmarking.EndToEnd;

public static class PublicDerivativeMechanicalFixtureCompiler
{
    public const string DriverSemanticsId =
        "wp6-public-derivative-instant-drain-driver-v1";

    private const string DatasetId = "fleetpy-manhattan-zenodo-15187906-v1";
    private const string ProfileRelativeRoot =
        "benchmarks/fixtures/wp6/public/fleetpy-manhattan-v1/medium";

    public static PublicDerivativeMechanicalFixtureArtifacts Compile(
        string repositoryRoot,
        ReadOnlySpan<byte> canonicalDatasetDescriptor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        var repository = Path.GetFullPath(repositoryRoot);
        var root = ResolveDirectory(repository, ProfileRelativeRoot);
        var dataset = BenchmarkContractCodec.Decode<DatasetDescriptor>(
            canonicalDatasetDescriptor);

        if (!dataset.IsSuccess
            || !canonicalDatasetDescriptor.SequenceEqual(dataset.CanonicalBytes))
        {
            throw new InvalidDataException(
                dataset.Error?.ToString()
                    ?? "Public dataset descriptor bytes are not exact canonical bytes.");
        }

        var canonicalDataset = dataset.CanonicalBytes
            ?? throw new InvalidDataException(
                "Public dataset descriptor canonical bytes are missing.");

        var scenarioPath = ResolveFile(root, "scenario-content.json");
        var reportPath = ResolveFile(root, "normalization-report.json");
        var manifestPath = ResolveFile(root, "derivative-manifest.json");
        var scenarioBytes = File.ReadAllBytes(scenarioPath);
        var reportBytes = File.ReadAllBytes(reportPath);
        var manifestBytes = File.ReadAllBytes(manifestPath);
        var scenario = BenchmarkContractCodec.Decode<ScenarioContent>(scenarioBytes);
        var report = BenchmarkContractCodec.Decode<NormalizationReport>(reportBytes);

        if (!scenario.IsSuccess || !scenarioBytes.SequenceEqual(scenario.CanonicalBytes))
        {
            throw new InvalidDataException(
                scenario.Error?.ToString()
                    ?? "Medium public scenario bytes are not exact canonical bytes.");
        }

        if (!report.IsSuccess || !reportBytes.SequenceEqual(report.CanonicalBytes))
        {
            throw new InvalidDataException(
                report.Error?.ToString()
                    ?? "Medium normalization report bytes are not exact canonical bytes.");
        }

        var content = scenario.Value!;
        var normalization = report.Value!;
        var scenarioHash = BenchmarkIdentity.CalculateScenario(scenarioBytes);
        var reportHash = BenchmarkIdentity.CalculateNormalizationReport(reportBytes);
        var sourceFixtureSha256 = Sha(scenarioBytes);

        using var manifest = JsonDocument.Parse(manifestBytes);
        var manifestRoot = manifest.RootElement;

        if (dataset.Value!.DatasetId != DatasetId
            || content.DatasetId != DatasetId
            || normalization.DatasetId != DatasetId
            || dataset.Value.SourceArtifactSha256 != content.SourceArtifactSha256
            || content.SourceArtifactSha256 != normalization.SourceArtifactSha256
            || normalization.ScenarioHash != scenarioHash
            || normalization.ScenarioContentSha256 != sourceFixtureSha256
            || manifestRoot.GetProperty("scenarioHash").GetString() != scenarioHash
            || manifestRoot.GetProperty("normalizationReportHash").GetString()
                != reportHash
            || manifestRoot.GetProperty("sourceArtifactSha256").GetString()
                != content.SourceArtifactSha256)
        {
            throw new InvalidDataException(
                "Medium public dataset/scenario/report/manifest identity chain diverges.");
        }

        if (content.ScenarioKind != ScenarioKind.PublicDerivative
            || content.EvidenceClass != EvidenceClass.Mechanical
            || content.DriverSemanticsId
                != "wp6-fleetpy-public-derivative-driver-v1"
            || content.Requests.Count != 128
            || content.Fleet.Count != 32
            || content.TravelSnapshots.Count != 1
            || content.ValidationSummary.NodeCount != 96
            || content.ValidationSummary.DirectedArcCount != 9_120
            || content.Events.Count != 128
            || content.Events.Any(value => value.EventType != "requestArrived")
            || normalization.InputRecordCount
                != normalization.EligibleRecordCount + normalization.ExcludedRecordCount
            || normalization.SelectedRecordCount != content.Requests.Count)
        {
            throw new InvalidDataException(
                "Medium public derivative does not satisfy the locked 128/32/96/9120 mechanical contract.");
        }

        var snapshot = content.TravelSnapshots.Single();
        var graphBytes = CanonicalJson.Canonicalize(
            JsonSerializer.SerializeToUtf8Bytes(
                snapshot.Arcs.Select(
                    value => new
                    {
                        fromNodeId = value.FromNodeId,
                        toNodeId = value.ToNodeId,
                    })));
        var capabilitySelection = new CapabilitySelection(
            CapabilitySelectionStatus.Accepted,
            PositionModel.DirectedEdgeProgress,
            [
                CapabilityId.DynamicTravelTimes,
                CapabilityId.ExactEventOrdering,
                CapabilityId.OldPlanProjection,
            ],
            5_000,
            100_000);
        var capabilityBytes = EncodeCapabilitySelection(capabilitySelection);
        var hello = new HelloPayload(
            DriverSemanticsId,
            "1.0.0",
            [ProtocolVersion.Current],
            PositionModel.DirectedEdgeProgress,
            [
                CapabilityId.Cancellations,
                CapabilityId.DynamicTravelTimes,
                CapabilityId.ExactEventOrdering,
                CapabilityId.OldPlanProjection,
            ],
            5_000,
            100_000);
        var helloEnvelope = Envelope(
            "hello",
            HelloPayloadCodec.Encode(hello));
        var sources = new[]
        {
            Source(root, scenarioHash, "ATTRIBUTION.md", "text/markdown"),
            Source(root, scenarioHash, "derivative-manifest.json", "application/json"),
            Source(root, scenarioHash, "normalization-exclusions.json", "application/json"),
            Source(root, scenarioHash, "normalization-report.json", "application/json"),
            Source(root, scenarioHash, "normalizer-configuration.json", "application/json"),
            Source(root, scenarioHash, "scenario-content.json", "application/json"),
            Source(root, scenarioHash, "selection-frame.json", "application/json"),
        };

        return new PublicDerivativeMechanicalFixtureArtifacts(
            dataset.Value,
            canonicalDataset,
            content,
            scenarioBytes,
            scenarioHash,
            reportHash,
            content.SourceArtifactSha256,
            normalization.SourceMemberInventorySha256,
            Sha(graphBytes),
            capabilitySelection,
            capabilityBytes,
            helloEnvelope,
            scenarioPath,
            sourceFixtureSha256,
            sources);
    }

    public static byte[] CreateInitializeEnvelope(
        PublicDerivativeMechanicalFixtureArtifacts fixture,
        PlannedBenchmarkRun run,
        BenchmarkArm arm,
        string runnerExecutableSha256,
        string coreCommitSha256)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(arm);
        RequireHash(runnerExecutableSha256, out var runnerHash);
        RequireHash(fixture.GraphSha256, out var graphHash);
        RequireHash(fixture.ScenarioHash, out var scenarioHash);
        RequireHash(fixture.Scenario.TravelSnapshots.Single().SnapshotHash, out var travelHash);
        RequireHash(arm.PolicyConfigurationSha256, out var policyHash);

        if (!SourceCommitSha.TryCreate(coreCommitSha256, out var coreCommit)
            || !SourceCommitSha.TryCreate(
                fixture.SourceArtifactSha256,
                out var sourceIdentity))
        {
            throw new InvalidDataException(
                "Medium public Runner manifest commit identities are invalid.");
        }

        RunId.TryCreate(run.RunId, out var runId);
        ScenarioId.TryCreate(fixture.Scenario.ScenarioId, out var scenarioId);
        var payload = new InitializeRunPayload(
            new RunManifestIdentity(
                ProtocolVersion.Current,
                run.SolverSeed.NonNegativeInt32,
                arm.PolicyId,
                arm.PolicyVersion,
                policyHash!,
                scenarioHash!,
                graphHash!,
                travelHash!,
                "abstract-generalized-cost-v1",
                [
                    new SourceUnitConversion(
                        "distance",
                        "meter",
                        "millimeter",
                        "roundTiesToEven"),
                    new SourceUnitConversion(
                        "time",
                        "second",
                        "millisecond",
                        "roundTiesToEven"),
                ],
                fixture.CapabilitySelection,
                new AdapterIdentity(DriverSemanticsId, "1.0.0"),
                new SimulatorIdentity(
                    "wp6-public-instant-drain-fixture",
                    "1.0.0",
                    sourceIdentity!),
                coreCommit!,
                runnerHash!));
        return Envelope(
            "initializeRun",
            InitializeRunPayloadCodec.Encode(payload),
            runId,
            scenarioId);
    }

    private static MechanicalHarnessBundleSource Source(
        string root,
        string scenarioHash,
        string name,
        string mediaType) =>
        new(
            ResolveFile(root, name),
            $"data/scenarios/{scenarioHash}/provenance/{name}",
            mediaType);

    private static byte[] EncodeCapabilitySelection(CapabilitySelection selection)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            CapabilitySelectionCodec.Write(writer, selection);
        }

        return CanonicalJson.Canonicalize(stream.ToArray());
    }

    private static byte[] Envelope(
        string messageType,
        byte[] payload,
        RunId? runId = null,
        ScenarioId? scenarioId = null)
    {
        ProtocolMessageType.TryParse(messageType, out var parsedType);
        using var document = JsonDocument.Parse(payload);
        return CanonicalJson.Serialize(
            new ProtocolEnvelope(
                ProtocolVersion.Current,
                parsedType!,
                document.RootElement.Clone(),
                runId,
                scenarioId));
    }

    private static void RequireHash(string value, out Sha256Hex? hash)
    {
        if (!Sha256Hex.TryCreate(value, out hash))
        {
            throw new InvalidDataException("Medium public fixture hash is invalid.");
        }
    }

    private static string ResolveDirectory(string root, string relative)
    {
        var path = Path.GetFullPath(
            Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            || !Directory.Exists(path))
        {
            throw new DirectoryNotFoundException(path);
        }

        return path;
    }

    private static string ResolveFile(string root, string relative)
    {
        var path = Path.GetFullPath(
            Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            || !File.Exists(path))
        {
            throw new FileNotFoundException(
                "Medium public derivative dependency is missing.",
                path);
        }

        return path;
    }

    private static string Sha(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));
}
