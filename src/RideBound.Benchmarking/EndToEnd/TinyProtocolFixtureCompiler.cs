using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using RideBound.Benchmarking.Contracts;
using RideBound.Benchmarking.Execution;
using RideBound.Benchmarking.Planning;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;

namespace RideBound.Benchmarking.EndToEnd;

public static class TinyProtocolFixtureCompiler
{
    private const string ScenarioId = "wp6-wp3-commitment-tiny-v1";
    private const string DatasetId = "ridebound-wp3-commitment-tiny-v1";
    private const string SourceRelativePath =
        "benchmarks/fixtures/wp6/e2e/tiny/paired.input.ndjson";
    private const string CompilerRelativePath =
        "src/RideBound.Benchmarking/EndToEnd/TinyProtocolFixtureCompiler.cs";
    public static TinyProtocolFixtureArtifacts Compile(string repositoryRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        var root = Path.GetFullPath(repositoryRoot);
        var sourcePath = Resolve(root, SourceRelativePath);
        var lines = ReadCanonicalLines(sourcePath);

        if (lines.Count is < 3 or > 34)
        {
            throw new InvalidDataException(
                "Tiny protocol fixture must retain one hello, one initialize and between one and 32 event-batch templates.");
        }

        RequireEnvelope(lines[0], "hello");
        RequireEnvelope(lines[1], "initializeRun");
        var eventBatchLineIndexes = Enumerable.Range(2, lines.Count - 2).ToArray();
        var eventBatches = lines.Skip(2).ToArray();

        foreach (var eventBatch in eventBatches)
        {
            RequireEnvelope(eventBatch, "eventBatch");
        }

        var sourceBytes = File.ReadAllBytes(sourcePath);
        var sourceSha = FileSha(sourceBytes);
        var compilerSha = FileSha(File.ReadAllBytes(Resolve(root, CompilerRelativePath)));
        var selectedBytes = EncodeTranscript(eventBatches);
        var selectionSha = FileSha(selectedBytes);
        var configurationBytes = CanonicalJson.Canonicalize(
            JsonSerializer.SerializeToUtf8Bytes(
                new
                {
                    driverSemanticsId = "wp6-wp3-protocol-fixture-driver-v1",
                    eventBatchLineIndexes,
                    sourceRelativePath = SourceRelativePath,
                }));
        var configurationSha = FileSha(configurationBytes);
        var parsed = ParseScenarioArrays(eventBatches);
        var graphBytes = CanonicalJson.Canonicalize(
            JsonSerializer.SerializeToUtf8Bytes(
                parsed.TravelSnapshots[0].Arcs.Select(
                    arc => new
                    {
                        fromNodeId = arc.FromNodeId,
                        toNodeId = arc.ToNodeId,
                    })));
        var graphSha = FileSha(graphBytes);
        var invariantSha = FileSha(
            "wp6-wp3-protocol-fixture-v1\0complete-directed-topology\0strict-event-sequence\0fixture-defined-policy\0"u8);
        var scenario = new ScenarioContent(
            BenchmarkContractVersions.V1_0_1,
            ScenarioId,
            ScenarioKind.ProtocolFixture,
            EvidenceClass.Mechanical,
            DatasetId,
            sourceSha,
            selectionSha,
            "wp6-wp3-protocol-fixture-compiler",
            "1.0.0",
            compilerSha,
            configurationSha,
            "ridebound-event-order-v1",
            "wp6-wp3-protocol-fixture-driver-v1",
            new ScenarioTimeWindow(
                "UTC",
                "2026-08-11T00:00:00Z",
                "2026-08-11T00:00:01Z",
                0,
                1_000,
                checked(parsed.Events.Max(value => value.SimTimeMs) + 1),
                checked(parsed.Events.Max(value => value.SimTimeMs) + 1),
                "event-driven-v1"),
            parsed.Fleet,
            parsed.Requests,
            parsed.TravelSnapshots,
            parsed.Events,
            new ScenarioValidationSummary(
                parsed.Fleet.Count,
                parsed.Requests.Count,
                parsed.TravelSnapshots[0].Arcs.SelectMany(
                        arc => new[] { arc.FromNodeId, arc.ToNodeId })
                    .Distinct(StringComparer.Ordinal)
                    .LongCount(),
                parsed.TravelSnapshots[0].Arcs.Count,
                parsed.TravelSnapshots.Count,
                parsed.Events.Count,
                0,
                parsed.Requests.Count,
                0,
                0,
                0,
                0,
                invariantSha));
        var scenarioBytes = BenchmarkContractCodec.Encode(scenario);
        var scenarioHash = BenchmarkIdentity.CalculateScenario(scenarioBytes);
        var dataset = new DatasetDescriptor(
            BenchmarkContractVersions.V1,
            DatasetId,
            DatasetKind.Synthetic,
            "RideBound WP3 commitment protocol fixture",
            "1.0.0",
            "https://github.com/tsunflowerr/RideBound/tree/main/benchmarks/scenarios/wp3-commitment-tiny",
            "https://github.com/tsunflowerr/RideBound/raw/main/benchmarks/scenarios/wp3-commitment-tiny/commitment-demo.input.ndjson",
            "2026-08-11T00:00:00Z",
            "commitment-demo.input.ndjson",
            "LicenseRef-RideBound-Research-Fixture",
            "https://github.com/tsunflowerr/RideBound",
            "RideBound source-controlled WP3 commitment tiny fixture v1.",
            $"{parsed.Fleet.Count} vehicle, {parsed.Requests.Count} requests, "
                + $"{parsed.TravelSnapshots.Count} complete three-node travel snapshots "
                + $"and {eventBatches.Length} deterministic epochs.",
            "Synthetic protocol fixture only; it contains no observed trip preference or satisfaction.",
            ["mechanicalBenchmark", "protocolDevelopment"],
            ["algorithmEffectiveness", "userSatisfaction"],
            DirectIdentifierStatus.NoneObserved,
            LocationPrecisionClass.Synthetic,
            RetentionClass.RedistributableDerivative,
            "Immutable source-controlled fixture; changes require a new scenario identity.",
            sourceBytes.LongLength,
            SourceArtifactSha256: sourceSha);
        var datasetBytes = BenchmarkContractCodec.Encode(dataset);
        using var initialize = JsonDocument.Parse(lines[1]);
        var capability = CanonicalJson.Canonicalize(
            Encoding.UTF8.GetBytes(
                initialize.RootElement.GetProperty("payload")
                    .GetProperty("manifest")
                    .GetProperty("capabilitySelection")
                    .GetRawText()));
        return new TinyProtocolFixtureArtifacts(
            dataset,
            datasetBytes,
            scenario,
            scenarioBytes,
            scenarioHash,
            graphSha,
            capability,
            lines[0],
            lines[1],
            eventBatches,
            sourcePath,
            sourceSha);
    }

    public static RunnerProtocolFixture CreateRunnerFixture(
        TinyProtocolFixtureArtifacts fixture,
        PlannedBenchmarkRun plannedRun,
        BenchmarkArm arm,
        string runnerExecutableSha256)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        ArgumentNullException.ThrowIfNull(plannedRun);
        ArgumentNullException.ThrowIfNull(arm);
        var initialize = JsonNode.Parse(fixture.InitializeTemplate)
            ?? throw new InvalidDataException("Tiny initialize template is empty.");
        initialize["runId"] = plannedRun.RunId;
        initialize["scenarioId"] = fixture.Scenario.ScenarioId;
        var manifest = initialize["payload"]!["manifest"]!;
        manifest["masterSeed"] = plannedRun.SolverSeed.NonNegativeInt32;
        manifest["policyId"] = arm.PolicyId;
        manifest["policyVersion"] = arm.PolicyVersion;
        manifest["policyConfigurationHash"] = arm.PolicyConfigurationSha256;
        manifest["scenarioContentHash"] = fixture.ScenarioHash;
        manifest["graphSnapshotHash"] = fixture.GraphSha256;
        manifest["travelTimeSnapshotHash"] =
            fixture.Scenario.TravelSnapshots[0].SnapshotHash;
        manifest["binarySha256"] = runnerExecutableSha256;
        var initializeBytes = Canonicalize(initialize);
        var batches = fixture.EventBatchTemplates
            .Select(
                template =>
                {
                    var node = JsonNode.Parse(template)
                        ?? throw new InvalidDataException(
                            "Tiny event-batch template is empty.");
                    node["runId"] = plannedRun.RunId;
                    node["scenarioId"] = fixture.Scenario.ScenarioId;
                    return Canonicalize(node);
                })
            .ToArray();
        return new RunnerProtocolFixture(
            fixture.HelloEnvelope,
            initializeBytes,
            batches,
            RequestCheckpointAfterFirstDecision: true);
    }

    private static ParsedScenario ParseScenarioArrays(
        IReadOnlyList<byte[]> eventBatches)
    {
        var requests = new List<ScenarioRequest>();
        var fleet = new List<ScenarioVehicle>();
        var snapshots = new List<ScenarioTravelSnapshot>();
        var events = new List<ScenarioEvent>();

        foreach (var batchBytes in eventBatches)
        {
            using var batch = JsonDocument.Parse(batchBytes);
            var root = batch.RootElement;
            var simTime = root.GetProperty("simTimeMs").GetInt64();

            foreach (var item in root.GetProperty("payload")
                .GetProperty("events").EnumerateArray())
            {
                var sequence = item.GetProperty("eventSeq").GetInt64();
                var eventType = item.GetProperty("eventType").GetString()!;
                var payload = item.GetProperty("payload");
                var payloadBytes = CanonicalJson.Canonicalize(
                    Encoding.UTF8.GetBytes(payload.GetRawText()));
                var stableSubjectId = StableSubject(eventType, payload, sequence);
                var sourceOrdinal = sequence;

                if (eventType == "travelTimesUpdated")
                {
                    var snapshot = payload.GetProperty("snapshot");
                    var arcs = new List<ScenarioTravelArc>();

                    foreach (var arc in snapshot.GetProperty("arcs").EnumerateArray())
                    {
                        arcs.Add(
                            new ScenarioTravelArc(
                                arc.GetProperty("fromNodeId").GetString()!,
                                arc.GetProperty("toNodeId").GetString()!,
                                arc.GetProperty("travelTimeMs").GetInt64()));
                    }

                    snapshots.Add(
                        new ScenarioTravelSnapshot(
                            snapshot.GetProperty("version").GetInt64(),
                            snapshot.GetProperty("snapshotHash").GetString()!,
                            arcs.OrderBy(value => value.FromNodeId, StringComparer.Ordinal)
                                .ThenBy(value => value.ToNodeId, StringComparer.Ordinal)
                                .ToArray()));
                }
                else if (eventType == "requestArrived")
                {
                    var request = payload.GetProperty("request");
                    sourceOrdinal = requests.Count;
                    requests.Add(
                        new ScenarioRequest(
                            request.GetProperty("requestId").GetString()!,
                            sourceOrdinal,
                            request.GetProperty("arrivalTimeMs").GetInt64(),
                            request.GetProperty("originNodeId").GetString()!,
                            request.GetProperty("destinationNodeId").GetString()!,
                            request.GetProperty("earliestPickupMs").GetInt64(),
                            request.GetProperty("latestPickupMs").GetInt64(),
                            request.GetProperty("maxRideTimeMs").GetInt64(),
                            request.GetProperty("partySize").GetInt64(),
                            request.GetProperty("serviceClass").GetString()!,
                            request.GetProperty("commitmentPolicyId").GetString()!,
                            "fixtureDefined",
                            $"fixture-request-{sourceOrdinal:D2}"));
                }
                else if (eventType == "vehicleAdvanced")
                {
                    fleet.Add(ParseVehicle(payload.GetProperty("vehicle"), sequence));
                }

                events.Add(
                    new ScenarioEvent(
                        sequence,
                        simTime,
                        eventType,
                        sourceOrdinal,
                        stableSubjectId,
                        true,
                        Convert.ToHexStringLower(payloadBytes),
                        FileSha(payloadBytes),
                        $"fixture-event-{sequence:D2}"));
            }
        }

        if (snapshots.Count == 0 || fleet.Count == 0)
        {
            throw new InvalidDataException(
                "Tiny protocol fixture omits bootstrap travel or fleet state.");
        }

        return new ParsedScenario(
            fleet.OrderBy(value => value.VehicleId, StringComparer.Ordinal).ToArray(),
            requests.OrderBy(value => value.RequestId, StringComparer.Ordinal).ToArray(),
            snapshots.OrderBy(value => value.Version).ToArray(),
            events.OrderBy(value => value.EventSequence).ToArray());
    }

    private static ScenarioVehicle ParseVehicle(JsonElement value, long sequence)
    {
        var position = value.GetProperty("position");

        if (position.GetProperty("kind").GetString() != "node")
        {
            throw new InvalidDataException(
                "Tiny protocol fixture requires the source-controlled node position.");
        }

        var route = value.GetProperty("route");
        return new ScenarioVehicle(
            value.GetProperty("vehicleId").GetString()!,
            value.GetProperty("capacity").GetInt64(),
            value.GetProperty("occupiedSeats").GetInt64(),
            new NodeScenarioPosition(position.GetProperty("nodeId").GetString()!),
            Strings(value.GetProperty("onboardRequestIds")),
            Strings(value.GetProperty("acceptedRequestIds")),
            new ScenarioRoute(
                route.GetProperty("planVersion").GetInt64(),
                route.GetProperty("executedStopCount").GetInt64(),
                [],
                []),
            $"fixture-vehicle-{sequence:D2}");
    }

    private static string StableSubject(
        string eventType,
        JsonElement payload,
        long sequence) => eventType switch
        {
            "travelTimesUpdated" => "travel-v1",
            "requestArrived" => payload.GetProperty("request")
                .GetProperty("requestId").GetString()!,
            "vehicleAdvanced" => payload.GetProperty("vehicle")
                .GetProperty("vehicleId").GetString()!,
            "bookingConfirmed" => payload.GetProperty("requestId").GetString()!,
            "vehicleReachedStop" => payload.GetProperty("vehicleId").GetString()!,
            "passengerBoarded" or "passengerAlighted" =>
                payload.GetProperty("requestId").GetString()!,
            _ => $"fixture-subject-{sequence:D2}",
        };

    private static string[] Strings(JsonElement values) =>
        values.EnumerateArray().Select(value => value.GetString()!).ToArray();

    private static IReadOnlyList<byte[]> ReadCanonicalLines(string path)
    {
        var bytes = File.ReadAllBytes(path);

        if (bytes.Length == 0 || bytes[^1] != (byte)'\n'
            || bytes.Contains((byte)'\r'))
        {
            throw new InvalidDataException(
                "Tiny protocol fixture must use complete LF-framed NDJSON.");
        }

        var lines = new List<byte[]>();
        var offset = 0;

        while (offset < bytes.Length)
        {
            var length = bytes.AsSpan(offset).IndexOf((byte)'\n');

            if (length <= 0)
            {
                throw new InvalidDataException(
                    "Tiny protocol fixture contains an empty NDJSON frame.");
            }

            var line = bytes.AsSpan(offset, length).ToArray();
            offset += length + 1;
            var canonical = CanonicalJson.Canonicalize(line);

            lines.Add(canonical);
        }

        return lines;
    }

    private static void RequireEnvelope(byte[] bytes, string messageType)
    {
        var decoded = ProtocolEnvelopeCodec.Decode(bytes);

        if (!decoded.IsSuccess
            || decoded.Envelope!.MessageType.Value != messageType)
        {
            throw new InvalidDataException(
                $"Tiny protocol fixture requires a valid '{messageType}' frame.");
        }
    }

    private static byte[] Canonicalize(JsonNode node) =>
        CanonicalJson.Canonicalize(Encoding.UTF8.GetBytes(node.ToJsonString()));

    private static byte[] EncodeTranscript(IEnumerable<byte[]> frames)
    {
        using var stream = new MemoryStream();

        foreach (var frame in frames)
        {
            stream.Write(frame);
            stream.WriteByte((byte)'\n');
        }

        return stream.ToArray();
    }

    private static string Resolve(string root, string relative)
    {
        var path = Path.GetFullPath(
            Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            || !File.Exists(path))
        {
            throw new FileNotFoundException(
                "Tiny protocol fixture dependency is missing.",
                path);
        }

        return path;
    }

    private static string FileSha(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));

    private sealed record ParsedScenario(
        IReadOnlyList<ScenarioVehicle> Fleet,
        IReadOnlyList<ScenarioRequest> Requests,
        IReadOnlyList<ScenarioTravelSnapshot> TravelSnapshots,
        IReadOnlyList<ScenarioEvent> Events);
}
