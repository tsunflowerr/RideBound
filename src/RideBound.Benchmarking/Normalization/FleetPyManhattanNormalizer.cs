using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RideBound.Benchmarking.Contracts;
using RideBound.Benchmarking.Datasets;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;

namespace RideBound.Benchmarking.Normalization;

public sealed class FleetPyManhattanNormalizer
{
    public const string NormalizerId = "fleetpy-manhattan-normalizer";
    public const string NormalizerVersion = "1.0.0";
    public const string SelectionRuleId =
        "greedy-induced-coverage-node-pool-hmac-row-v1";

    private const long MaximumCanonicalInteger = 9_007_199_254_740_991;

    private static readonly JsonSerializerOptions AuxiliaryJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<FleetPyNormalizationResult> NormalizeAsync(
        FleetPyNormalizationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Artifact);
        ArgumentNullException.ThrowIfNull(request.Extraction);
        ArgumentNullException.ThrowIfNull(request.Configuration);

        try
        {
            var preflight = ValidatePreflight(request);

            if (preflight is not null)
            {
                return preflight;
            }

            var configuration = request.Configuration;
            var inventory = request.Extraction.Inventory!;
            var memberPaths = new[]
            {
                configuration.DemandMemberPath,
                configuration.NodeMemberPath,
                configuration.EdgeMemberPath,
                configuration.TravelFactorMemberPath,
            };
            var resolved = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var memberPath in memberPaths)
            {
                var member = inventory.Members.SingleOrDefault(
                    value => string.Equals(
                        value.RelativePath,
                        memberPath,
                        StringComparison.Ordinal));

                if (member is null)
                {
                    return FleetPyNormalizationResult.Failed(
                        "source.member-not-registered",
                        "preflight",
                        $"Required archive member '{memberPath}' is absent from the verified inventory.");
                }

                var fullPath = ResolveVerifiedMember(
                    request.Extraction.ExtractionRoot!,
                    memberPath);
                var digest = await VerifiedDatasetDownloader.HashFileAsync(
                    fullPath,
                    cancellationToken);

                if (digest.LengthBytes != member.LengthBytes
                    || !string.Equals(digest.Sha256, member.Sha256, StringComparison.Ordinal))
                {
                    return FleetPyNormalizationResult.Failed(
                        "source.member-checksum-mismatch",
                        "preflight",
                        $"Required archive member '{memberPath}' changed after verified extraction.");
                }

                resolved.Add(memberPath, fullPath);
            }

            var factor = ReadTravelFactor(
                resolved[configuration.TravelFactorMemberPath],
                configuration.TravelFactorAtSeconds);
            var nodes = ReadNodes(resolved[configuration.NodeMemberPath]);
            var arcs = ReadArcs(
                resolved[configuration.EdgeMemberPath],
                nodes,
                factor);
            var graph = new DirectedTravelGraph(nodes, arcs);
            var demand = ReadDemand(
                resolved[configuration.DemandMemberPath],
                configuration,
                graph);

            if (demand.Eligible.Count < configuration.RequestTarget)
            {
                return FleetPyNormalizationResult.Failed(
                    "source.insufficient-eligible-records",
                    "selection",
                    "The registered source does not contain enough eligible records for the requested derivative bound.");
            }

            var selection = SelectRequests(
                demand.Eligible,
                configuration,
                request.Artifact.Sha256,
                graph);
            var selected = selection.Selected;

            if (selected.Count != configuration.RequestTarget)
            {
                return FleetPyNormalizationResult.Failed(
                    "source.node-cap-prevents-target",
                    "selection",
                    "HMAC-ranked source selection cannot reach the request target without exceeding the declared node cap.");
            }

            var selectedOrdinals = selected
                .Select(value => value.SourceRecordOrdinal)
                .ToHashSet();
            var dispositions = demand.Eligible
                .Select(
                    value => new NormalizationDisposition(
                        value.SourceRecordOrdinal,
                        selectedOrdinals.Contains(value.SourceRecordOrdinal)
                            ? "selected"
                            : "notSelected",
                        value.SelectionRankSha256))
                .OrderBy(value => value.SourceRecordOrdinal)
                .ToArray();
            var dispositionBytes = Canonicalize(dispositions);
            var exclusionBytes = Canonicalize(demand.Exclusions);
            var sourceSelectionBytes = Canonicalize(
                selected
                    .OrderBy(value => value.SourceRecordOrdinal)
                    .Select(
                        value => new SourceSelectionRow(
                            value.SourceRecordOrdinal,
                            NodeId(value.OriginNode),
                            NodeId(value.DestinationNode),
                            value.ArrivalSourceMs,
                            value.SelectionRankSha256))
                    .ToArray());
            var sourceSelectionSha256 = Sha256(sourceSelectionBytes);
            var configurationBytes = Canonicalize(configuration);
            var configurationSha256 = Sha256(configurationBytes);
            var scenario = BuildScenario(
                request,
                graph,
                selected,
                selection.NodePool,
                demand.Exclusions.Count,
                sourceSelectionSha256,
                configurationSha256);
            var scenarioError = BenchmarkContractValidator.Validate(scenario);

            if (scenarioError is not null)
            {
                return FleetPyNormalizationResult.Failed(
                    "normalizer.scenario-contract-invalid",
                    "validation",
                    $"Generated scenario violates {scenarioError.Path}: {scenarioError.Message}");
            }

            var scenarioBytes = BenchmarkContractCodec.Encode(scenario);
            var scenarioContentSha256 = Sha256(scenarioBytes);
            var scenarioHash = BenchmarkIdentity.CalculateScenario(scenarioBytes);
            var report = new NormalizationReport(
                BenchmarkContractVersions.V1_0_1,
                $"report-{scenarioHash[..24]}",
                request.Artifact.DatasetId,
                request.Artifact.Sha256,
                inventory.InventorySha256,
                NormalizerId,
                NormalizerVersion,
                configuration.NormalizerSourceSha256,
                configurationSha256,
                demand.InputRecordCount,
                demand.Eligible.Count,
                selected.Count,
                demand.Exclusions.Count,
                Sha256(dispositionBytes),
                Sha256(exclusionBytes),
                "ties-to-even-v1",
                "ridebound-event-order-v1",
                SelectionRuleId,
                scenarioContentSha256,
                scenarioHash);
            var reportError = BenchmarkContractValidator.Validate(report);

            if (reportError is not null)
            {
                return FleetPyNormalizationResult.Failed(
                    "normalizer.report-contract-invalid",
                    "validation",
                    $"Generated report violates {reportError.Path}: {reportError.Message}");
            }

            var reportBytes = BenchmarkContractCodec.Encode(report);
            var reportHash = BenchmarkIdentity.CalculateNormalizationReport(reportBytes);
            var derivativeManifest = new PublicDerivativeManifest(
                "1.0.0",
                configuration.ScenarioId,
                request.Artifact.DatasetId,
                request.Artifact.Descriptor.PersistentUri,
                request.Artifact.Descriptor.LicenseSpdx,
                request.Artifact.Descriptor.LicenseUri,
                request.Artifact.Descriptor.Citation,
                request.Artifact.Sha256,
                inventory.InventorySha256,
                sourceSelectionSha256,
                NormalizerId,
                NormalizerVersion,
                configuration.NormalizerSourceSha256,
                configurationSha256,
                scenarioContentSha256,
                scenarioHash,
                reportHash,
                report.SelectionFrameSha256,
                report.ExclusionLogSha256,
                "syntheticPolicyOverlay",
                [
                    "verify-exact-archive-and-member-inventory",
                    "parse-registered-csv-members-with-stable-row-ordinals",
                    "exclude-invalid-or-not-strongly-connected-source-rows",
                    "greedily-maximize-induced-source-row-coverage-under-explicit-node-cap",
                    "hmac-rank-policy-independent-rows-within-the-locked-node-pool",
                    "convert-decimal-seconds-to-integer-ms-with-ties-to-even",
                    "discard-source-self-loops-that-cannot-appear-in-scenario-topology",
                    "compute-directed-shortest-paths-without-reverse-or-euclidean-imputation",
                    "pseudonymize-request-identifiers-with-domain-separated-hmac",
                    "emit-canonical-scenario-then-bind-it-into-conservation-report",
                ],
                request.Artifact.Descriptor.ForbiddenClaim
                    .Order(StringComparer.Ordinal)
                    .ToArray());
            var derivativeBytes = Canonicalize(derivativeManifest);

            return FleetPyNormalizationResult.Success(
                new FleetPyNormalizationArtifact(
                    scenario,
                    scenarioBytes,
                    scenarioContentSha256,
                    scenarioHash,
                    report,
                    reportBytes,
                    reportHash,
                    dispositions,
                    dispositionBytes,
                    demand.Exclusions,
                    exclusionBytes,
                    configurationBytes,
                    derivativeManifest,
                    derivativeBytes));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (SourceNormalizationException exception)
        {
            return FleetPyNormalizationResult.Failed(
                exception.Code,
                exception.Stage,
                exception.Message);
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or InvalidDataException
                or FormatException
                or OverflowException
                or CryptographicException
                or ArgumentException)
        {
            return FleetPyNormalizationResult.Failed(
                "source.normalization-failed",
                "normalization",
                exception.Message);
        }
    }

    private static FleetPyNormalizationResult? ValidatePreflight(
        FleetPyNormalizationRequest request)
    {
        var configuration = request.Configuration;

        if (request.Extraction.Status != ArchiveExtractionStatus.Succeeded
            || request.Extraction.Inventory is null
            || request.Extraction.ExtractionRoot is null
            || !string.Equals(
                request.Extraction.Inventory.ArchiveSha256,
                request.Artifact.Sha256,
                StringComparison.Ordinal))
        {
            return FleetPyNormalizationResult.Failed(
                "source.extraction-not-verified",
                "preflight",
                "Normalizer requires a successful extraction bound to the verified archive SHA-256.");
        }

        if (!string.Equals(
            request.Artifact.DatasetId,
            DatasetSourceRegistry.FleetPyManhattanV1.DatasetId,
            StringComparison.Ordinal))
        {
            return FleetPyNormalizationResult.Failed(
                "source.dataset-not-supported",
                "preflight",
                "This normalizer accepts only the locked FleetPy Manhattan v1 dataset registration.");
        }

        if (configuration.RequestTarget <= 0
            || configuration.VehicleCount <= 0
            || configuration.MaximumNodeCount < 2
            || configuration.VehicleCount > configuration.MaximumNodeCount
            || configuration.RequestTarget > 128
            || configuration.VehicleCount > 32
            || configuration.MaximumNodeCount > 96
            || configuration.VehicleCapacity <= 0
            || configuration.PickupWindowMs < 0
            || configuration.MaximumRideTimePermille < 1_000
            || configuration.DrainDurationMs < 0
            || configuration.SourceWindowStartSeconds < 0
            || configuration.SourceWindowEndSeconds
                <= configuration.SourceWindowStartSeconds
            || configuration.SourceWindowEndSeconds > 86_400
            || configuration.TravelFactorAtSeconds < 0
            || configuration.TravelFactorAtSeconds >= 86_400
            || !IsSha(configuration.NormalizerSourceSha256)
            || !TryDecodeKey(configuration.SelectionKeyHex, out _)
            || !TryDecodeKey(configuration.PseudonymizationKeyHex, out _))
        {
            return FleetPyNormalizationResult.Failed(
                "normalizer.configuration-invalid",
                "preflight",
                "Normalizer configuration violates contract v1 bounds or key/hash syntax.");
        }

        if (new[]
            {
                configuration.DemandMemberPath,
                configuration.NodeMemberPath,
                configuration.EdgeMemberPath,
                configuration.TravelFactorMemberPath,
            }.Distinct(StringComparer.Ordinal).Count() != 4)
        {
            return FleetPyNormalizationResult.Failed(
                "normalizer.configuration-invalid",
                "preflight",
                "Every registered source member role must resolve to a distinct path.");
        }

        return null;
    }

    private static ScenarioContent BuildScenario(
        FleetPyNormalizationRequest request,
        DirectedTravelGraph graph,
        IReadOnlyList<DemandRecord> selected,
        IReadOnlySet<int> selectedNodePool,
        int excludedCount,
        string sourceSelectionSha256,
        string configurationSha256)
    {
        var configuration = request.Configuration;
        var selectedNodes = selectedNodePool.Order().ToArray();
        var selectedNodeSet = selectedNodes.ToHashSet();
        var arcs = new List<ScenarioTravelArc>(
            selectedNodes.Length * (selectedNodes.Length - 1));
        var distancesBySource = new Dictionary<int, IReadOnlyDictionary<int, long>>();

        foreach (var source in selectedNodes)
        {
            var distances = graph.ShortestPathsFrom(source, selectedNodeSet);
            distancesBySource.Add(source, distances);

            foreach (var target in selectedNodes)
            {
                if (source == target)
                {
                    continue;
                }

                arcs.Add(
                    new ScenarioTravelArc(
                        NodeId(source),
                        NodeId(target),
                        distances[target]));
            }
        }

        arcs.Sort(
            (left, right) =>
            {
                var from = string.CompareOrdinal(left.FromNodeId, right.FromNodeId);
                return from != 0
                    ? from
                    : string.CompareOrdinal(left.ToNodeId, right.ToNodeId);
            });
        var snapshotHash = Sha256(
            Canonicalize(
                arcs.Select(
                    value => new SnapshotHashArc(
                        value.FromNodeId,
                        value.ToNodeId,
                        value.TravelTimeMs)).ToArray()));
        var sourceWindow = ResolveSourceWindow(configuration);
        var windowStartMs = checked(configuration.SourceWindowStartSeconds * 1_000);
        var horizonEndMs = checked(
            (configuration.SourceWindowEndSeconds
                - configuration.SourceWindowStartSeconds) * 1_000);
        var drainEndMs = checked(horizonEndMs + configuration.DrainDurationMs);
        var requests = selected
            .Select(
                value =>
                {
                    var arrival = checked(value.ArrivalSourceMs - windowStartMs);
                    var direct = distancesBySource[value.OriginNode][value.DestinationNode];
                    var maxRide = ScaleIntegerTiesToEven(
                        direct,
                        configuration.MaximumRideTimePermille,
                        1_000);
                    return new ScenarioRequest(
                        PseudonymousRequestId(
                            configuration.PseudonymizationKeyHex,
                            request.Artifact.Sha256,
                            configuration.DemandMemberPath,
                            value.SourceRecordOrdinal),
                        value.SourceRecordOrdinal,
                        arrival,
                        NodeId(value.OriginNode),
                        NodeId(value.DestinationNode),
                        arrival,
                        checked(arrival + configuration.PickupWindowMs),
                        Math.Max(direct, maxRide),
                        1,
                        "fleetpy-tlc-public-derivative-v1",
                        configuration.CommitmentPolicyId,
                        "syntheticPolicyOverlay",
                        $"source-row-{value.SourceRecordOrdinal:D8}");
                })
            .OrderBy(value => value.RequestId, StringComparer.Ordinal)
            .ToArray();

        if (requests.Select(value => value.RequestId).Distinct(StringComparer.Ordinal).Count()
            != requests.Length)
        {
            throw new InvalidDataException("Pseudonymous request ID collision detected.");
        }

        var fleetNodes = selectedNodes
            .Select(
                node => new
                {
                    Node = node,
                    Rank = HmacHex(
                        configuration.SelectionKeyHex,
                        "RideBound.Wp6.FleetPlacement.v1",
                        request.Artifact.Sha256,
                        configuration.ScenarioId,
                        node.ToString(CultureInfo.InvariantCulture)),
                })
            .OrderBy(value => value.Rank, StringComparer.Ordinal)
            .ThenBy(value => value.Node)
            .Take(checked((int)configuration.VehicleCount))
            .ToArray();
        var fleet = fleetNodes
            .Select(
                (value, index) => new ScenarioVehicle(
                    $"veh-{index:D3}",
                    configuration.VehicleCapacity,
                    0,
                    new NodeScenarioPosition(NodeId(value.Node)),
                    [],
                    [],
                    new ScenarioRoute(0, 0, [], []),
                    $"derived-fleet-{value.Rank[..24]}"))
            .OrderBy(value => value.VehicleId, StringComparer.Ordinal)
            .ToArray();
        var events = requests
            .OrderBy(value => value.ArrivalTimeMs)
            .ThenBy(value => value.SourceRecordOrdinal)
            .ThenBy(value => value.RequestId, StringComparer.Ordinal)
            .Select(
                (value, index) =>
                {
                    var payload = EncodeRequestArrived(value);
                    return new ScenarioEvent(
                        index + 1,
                        value.ArrivalTimeMs,
                        "requestArrived",
                        value.SourceRecordOrdinal,
                        value.RequestId,
                        false,
                        Convert.ToHexStringLower(payload),
                        Sha256(payload),
                        value.SourceProvenanceId);
                })
            .ToArray();
        var invariantHash = Sha256(
            Canonicalize(
                new[]
                {
                    "complete-directed-selected-topology-v1",
                    "distinct-request-and-source-ordinal-v1",
                    "hmac-source-selection-conservation-v1",
                    "no-imputed-arcs-v1",
                    "ridebound-event-order-v1",
                    "synthetic-policy-overlay-label-v1",
                    "ties-to-even-v1",
                }));

        return new ScenarioContent(
            BenchmarkContractVersions.V1_0_1,
            configuration.ScenarioId,
            ScenarioKind.PublicDerivative,
            EvidenceClass.Mechanical,
            request.Artifact.DatasetId,
            request.Artifact.Sha256,
            sourceSelectionSha256,
            NormalizerId,
            NormalizerVersion,
            configuration.NormalizerSourceSha256,
            configurationSha256,
            "ridebound-event-order-v1",
            "wp6-fleetpy-public-derivative-driver-v1",
            new ScenarioTimeWindow(
                configuration.SourceTimezoneId,
                sourceWindow.StartUtc,
                sourceWindow.EndUtc,
                0,
                0,
                horizonEndMs,
                drainEndMs,
                "event-driven-v1"),
            fleet,
            requests,
            [new ScenarioTravelSnapshot(1, snapshotHash, arcs)],
            events,
            new ScenarioValidationSummary(
                fleet.Length,
                requests.Length,
                selectedNodes.Length,
                arcs.Count,
                1,
                events.Length,
                excludedCount,
                selected.Count,
                0,
                0,
                0,
                0,
                invariantHash));
    }

    private static DemandReadResult ReadDemand(
        string path,
        FleetPyNormalizationConfiguration configuration,
        DirectedTravelGraph graph)
    {
        var eligible = new List<DemandRecord>();
        var exclusions = new List<NormalizationExclusion>();
        var sourceIds = new HashSet<string>(StringComparer.Ordinal);
        var key = Convert.FromHexString(configuration.SelectionKeyHex);
        var windowStartMs = checked(configuration.SourceWindowStartSeconds * 1_000);
        var windowEndMs = checked(configuration.SourceWindowEndSeconds * 1_000);
        long inputCount = 0;

        foreach (var row in StrictCsv.Read(
            path,
            ["request_id", "rq_time", "start", "end"]))
        {
            inputCount++;

            if (row.ParseError is not null || row.Fields?.Count != 4)
            {
                exclusions.Add(Exclusion(row.Ordinal, "source.invalid-record", "csv", "Malformed demand CSV row."));
                continue;
            }

            var fields = row.Fields;

            if (fields[0].Length == 0 || !sourceIds.Add(fields[0]))
            {
                exclusions.Add(Exclusion(row.Ordinal, "source.duplicate-request-id", "request_id", "Missing or duplicate source request identifier."));
                continue;
            }

            if (!TryMilliseconds(fields[1], out var sourceTimeMs))
            {
                exclusions.Add(Exclusion(row.Ordinal, "source.invalid-time", "rq_time", "Request time is not a finite canonical decimal convertible to ms."));
                continue;
            }

            if (sourceTimeMs < windowStartMs || sourceTimeMs >= windowEndMs)
            {
                exclusions.Add(Exclusion(row.Ordinal, "source.outside-window", "rq_time", "Request lies outside the preregistered source window."));
                continue;
            }

            if (!int.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out var origin)
                || !int.TryParse(fields[3], NumberStyles.None, CultureInfo.InvariantCulture, out var destination)
                || origin == destination)
            {
                exclusions.Add(Exclusion(row.Ordinal, "source.invalid-record", "start/end", "Request node identifiers are malformed or equal."));
                continue;
            }

            if (!graph.Contains(origin)
                || !graph.Contains(destination)
                || !graph.IsStronglyConnected(origin, destination))
            {
                exclusions.Add(Exclusion(row.Ordinal, "source.unreachable-node-pair", "start/end", "Request endpoints are absent or not in one directed strongly connected component."));
                continue;
            }

            var rank = HmacHex(
                key,
                "RideBound.Wp6.SourceSelection.v1",
                configuration.DemandMemberPath,
                row.Ordinal.ToString(CultureInfo.InvariantCulture));
            eligible.Add(
                new DemandRecord(
                    row.Ordinal,
                    sourceTimeMs,
                    origin,
                    destination,
                    rank));
        }

        return new DemandReadResult(
            inputCount,
            eligible,
            exclusions.OrderBy(value => value.SourceRecordOrdinal).ToArray());
    }

    private static SourceSelectionResult SelectRequests(
        List<DemandRecord> eligible,
        FleetPyNormalizationConfiguration configuration,
        string sourceArtifactSha256,
        DirectedTravelGraph graph)
    {
        var reranked = eligible
            .Select(
                value => value with
                {
                    SelectionRankSha256 = HmacHex(
                        configuration.SelectionKeyHex,
                        "RideBound.Wp6.SourceSelection.v1",
                        sourceArtifactSha256,
                        configuration.DemandMemberPath,
                        value.SourceRecordOrdinal.ToString(CultureInfo.InvariantCulture)),
                })
            .OrderBy(value => value.SelectionRankSha256, StringComparer.Ordinal)
            .ThenBy(value => value.SourceRecordOrdinal)
            .ToArray();

        var rankByOrdinal = reranked.ToDictionary(
            value => value.SourceRecordOrdinal,
            value => value.SelectionRankSha256);

        for (var index = 0; index < eligible.Count; index++)
        {
            eligible[index] = eligible[index] with
            {
                SelectionRankSha256 = rankByOrdinal[eligible[index].SourceRecordOrdinal],
            };
        }

        var pairCounts = eligible
            .GroupBy(
                value => OrderedPair(value.OriginNode, value.DestinationNode))
            .ToDictionary(group => group.Key, group => group.Count());
        var endpointFrequencies = eligible
            .SelectMany(value => new[] { value.OriginNode, value.DestinationNode })
            .GroupBy(value => value)
            .ToDictionary(group => group.Key, group => group.Count());
        var seedPair = pairCounts
            .Select(
                pair => new
                {
                    Pair = pair.Key,
                    Count = pair.Value,
                    Rank = HmacHex(
                        configuration.SelectionKeyHex,
                        "RideBound.Wp6.NodePoolSeedPair.v1",
                        sourceArtifactSha256,
                        pair.Key.First.ToString(CultureInfo.InvariantCulture),
                        pair.Key.Second.ToString(CultureInfo.InvariantCulture)),
                })
            .OrderByDescending(value => value.Count)
            .ThenBy(value => value.Rank, StringComparer.Ordinal)
            .ThenBy(value => value.Pair.First)
            .ThenBy(value => value.Pair.Second)
            .First();
        var nodePool = new HashSet<int>
        {
            seedPair.Pair.First,
            seedPair.Pair.Second,
        };
        var nodeRanks = endpointFrequencies.Keys.ToDictionary(
            node => node,
            node => HmacHex(
                configuration.SelectionKeyHex,
                "RideBound.Wp6.NodePoolTieBreak.v1",
                sourceArtifactSha256,
                node.ToString(CultureInfo.InvariantCulture)));
        var candidateNodes = endpointFrequencies.Keys
            .Where(node => graph.IsStronglyConnected(seedPair.Pair.First, node))
            .ToArray();

        while (nodePool.Count < configuration.MaximumNodeCount)
        {
            var next = candidateNodes
                .Where(node => !nodePool.Contains(node))
                .Select(
                    node => new
                    {
                        Node = node,
                        MarginalCoverage = nodePool.Sum(
                            existing => pairCounts.GetValueOrDefault(
                                OrderedPair(node, existing))),
                        EndpointFrequency = endpointFrequencies[node],
                        Rank = nodeRanks[node],
                    })
                .OrderByDescending(value => value.MarginalCoverage)
                .ThenByDescending(value => value.EndpointFrequency)
                .ThenBy(value => value.Rank, StringComparer.Ordinal)
                .ThenBy(value => value.Node)
                .FirstOrDefault();

            if (next is null)
            {
                break;
            }

            nodePool.Add(next.Node);
        }

        var selected = reranked
            .Where(
                value => nodePool.Contains(value.OriginNode)
                    && nodePool.Contains(value.DestinationNode))
            .Take(checked((int)configuration.RequestTarget))
            .ToArray();
        return new SourceSelectionResult(selected, nodePool);
    }

    private static NodePair OrderedPair(int first, int second) =>
        first < second ? new NodePair(first, second) : new NodePair(second, first);

    private static IReadOnlyCollection<int> ReadNodes(string path)
    {
        var nodes = new HashSet<int>();

        foreach (var row in StrictCsv.Read(
            path,
            ["node_index", "is_stop_only", "pos_x", "pos_y"]))
        {
            if (row.ParseError is not null
                || row.Fields?.Count != 4
                || !int.TryParse(row.Fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out var node)
                || !bool.TryParse(row.Fields[1], out _)
                || !decimal.TryParse(row.Fields[2], NumberStyles.Float, CultureInfo.InvariantCulture, out _)
                || !decimal.TryParse(row.Fields[3], NumberStyles.Float, CultureInfo.InvariantCulture, out _)
                || !nodes.Add(node))
            {
                throw new InvalidDataException(
                    $"Malformed or duplicate network node at source ordinal {row.Ordinal}.");
            }
        }

        return nodes;
    }

    private static IReadOnlyCollection<WeightedArc> ReadArcs(
        string path,
        IReadOnlyCollection<int> nodes,
        decimal factor)
    {
        var arcs = new List<WeightedArc>();
        var nodeSet = nodes.ToHashSet();

        foreach (var row in StrictCsv.Read(
            path,
            ["from_node", "to_node", "distance", "travel_time", "source_edge_id"]))
        {
            if (row.ParseError is not null
                || row.Fields?.Count != 5
                || !int.TryParse(row.Fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out var from)
                || !int.TryParse(row.Fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var to)
                || !decimal.TryParse(row.Fields[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var distance)
                || distance <= 0
                || !decimal.TryParse(row.Fields[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
                || seconds <= 0
                || !nodeSet.Contains(from)
                || !nodeSet.Contains(to))
            {
                throw new InvalidDataException(
                    $"Malformed network arc at source ordinal {row.Ordinal}.");
            }

            if (from == to)
            {
                continue;
            }

            arcs.Add(new WeightedArc(from, to, ScaleSeconds(seconds, factor)));
        }

        return arcs;
    }

    private static decimal ReadTravelFactor(string path, long atSeconds)
    {
        decimal? selected = null;

        foreach (var row in StrictCsv.Read(
            path,
            [
                "simulation_time",
                "time_interval",
                "travel_time_factor",
                "travel requests",
                "average travel duration",
                "standard error",
                "25%",
                "50%",
                "75%",
            ]))
        {
            if (row.ParseError is not null
                || row.Fields?.Count != 9
                || !long.TryParse(row.Fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out var time)
                || !decimal.TryParse(row.Fields[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var factor)
                || factor <= 0)
            {
                throw new InvalidDataException(
                    $"Malformed travel-factor row at source ordinal {row.Ordinal}.");
            }

            if (time == atSeconds)
            {
                if (selected is not null)
                {
                    throw new InvalidDataException("Travel-factor timestamp is duplicated.");
                }

                selected = factor;
            }
        }

        return selected
            ?? throw new InvalidDataException("Configured travel-factor timestamp is absent.");
    }

    private static SourceWindow ResolveSourceWindow(
        FleetPyNormalizationConfiguration configuration)
    {
        DateOnly date;
        TimeZoneInfo zone;

        try
        {
            date = DateOnly.ParseExact(
                configuration.SourceLocalDate,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture);
            zone = TimeZoneInfo.FindSystemTimeZoneById(configuration.SourceTimezoneId);
        }
        catch (FormatException exception)
        {
            throw new SourceNormalizationException(
                "source.local-date-invalid",
                "time-normalization",
                "Source local date must be an exact yyyy-MM-dd calendar date.",
                exception);
        }
        catch (TimeZoneNotFoundException exception)
        {
            throw new SourceNormalizationException(
                "source.timezone-invalid",
                "time-normalization",
                "Source IANA timezone is unavailable on this runtime.",
                exception);
        }
        catch (InvalidTimeZoneException exception)
        {
            throw new SourceNormalizationException(
                "source.timezone-invalid",
                "time-normalization",
                "Source timezone definition is invalid on this runtime.",
                exception);
        }

        var localStart = DateTime.SpecifyKind(
            date.ToDateTime(TimeOnly.MinValue)
                .AddSeconds(configuration.SourceWindowStartSeconds),
            DateTimeKind.Unspecified);
        var localEnd = DateTime.SpecifyKind(
            date.ToDateTime(TimeOnly.MinValue)
                .AddSeconds(configuration.SourceWindowEndSeconds),
            DateTimeKind.Unspecified);

        if (zone.IsInvalidTime(localStart) || zone.IsInvalidTime(localEnd))
        {
            throw new SourceNormalizationException(
                "source.dst-invalid-time",
                "time-normalization",
                "Source window crosses a nonexistent local DST time.");
        }

        if (zone.IsAmbiguousTime(localStart) || zone.IsAmbiguousTime(localEnd))
        {
            throw new SourceNormalizationException(
                "source.dst-ambiguous-time",
                "time-normalization",
                "Source window crosses an ambiguous local DST time.");
        }

        return new SourceWindow(
            FormatUtc(TimeZoneInfo.ConvertTimeToUtc(localStart, zone)),
            FormatUtc(TimeZoneInfo.ConvertTimeToUtc(localEnd, zone)));
    }

    private static string ResolveVerifiedMember(string root, string relativePath)
    {
        if (relativePath.Length == 0
            || relativePath.Contains('\\', StringComparison.Ordinal)
            || Path.IsPathRooted(relativePath)
            || relativePath.Split('/').Any(
                segment => segment is "" or "." or ".."))
        {
            throw new InvalidDataException("Registered member path is not canonical.");
        }

        var fullRoot = Path.GetFullPath(root);
        var fullPath = Path.GetFullPath(
            Path.Combine(fullRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var relative = Path.GetRelativePath(fullRoot, fullPath);

        if (relative == ".."
            || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || Path.IsPathRooted(relative)
            || !File.Exists(fullPath))
        {
            throw new InvalidDataException("Registered member escaped the verified extraction root.");
        }

        var file = new FileInfo(fullPath);

        if ((file.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("Registered member path traverses a reparse point.");
        }

        for (var current = file.Directory; current is not null; current = current.Parent)
        {
            if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("Registered member path traverses a reparse point.");
            }

            if (string.Equals(current.FullName, fullRoot, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
        }

        return fullPath;
    }

    private static byte[] EncodeRequestArrived(ScenarioRequest request)
    {
        EventSequence.TryCreate(1, out var eventSequence);
        var batch = EventBatchPayloadCodec.Encode(
            new EventBatchPayload(
                [
                    new ProtocolEvent(
                        eventSequence,
                        EventType.RequestArrived,
                        new RequestArrivedEventPayload(
                            new RequestContract(
                                request.RequestId,
                                request.ArrivalTimeMs,
                                request.OriginNodeId,
                                request.DestinationNodeId,
                                request.EarliestPickupMs,
                                request.LatestPickupMs,
                                request.MaxRideTimeMs,
                                request.PartySize,
                                request.ServiceClass,
                                request.CommitmentPolicyId))),
                ]));
        using var document = JsonDocument.Parse(batch);
        var payload = document.RootElement
            .GetProperty("events")[0]
            .GetProperty("payload")
            .GetRawText();
        return CanonicalJson.Canonicalize(Encoding.UTF8.GetBytes(payload));
    }

    private static NormalizationExclusion Exclusion(
        long ordinal,
        string code,
        string field,
        string detail) =>
        new(ordinal, code, field, detail);

    private static bool TryMilliseconds(string text, out long milliseconds)
    {
        milliseconds = 0;

        if (!decimal.TryParse(
            text,
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out var seconds)
            || seconds < 0)
        {
            return false;
        }

        try
        {
            var rounded = decimal.Round(
                checked(seconds * 1_000m),
                0,
                MidpointRounding.ToEven);

            if (rounded > MaximumCanonicalInteger)
            {
                return false;
            }

            milliseconds = decimal.ToInt64(rounded);
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static long ScaleSeconds(decimal seconds, decimal factor)
    {
        var scaled = decimal.Round(
            checked(seconds * factor * 1_000m),
            0,
            MidpointRounding.ToEven);

        if (scaled <= 0 || scaled > MaximumCanonicalInteger)
        {
            throw new OverflowException("Travel time is outside canonical integer bounds.");
        }

        return decimal.ToInt64(scaled);
    }

    private static long ScaleIntegerTiesToEven(
        long value,
        long numerator,
        long denominator)
    {
        var scaled = decimal.Round(
            checked((decimal)value * numerator / denominator),
            0,
            MidpointRounding.ToEven);

        if (scaled <= 0 || scaled > MaximumCanonicalInteger)
        {
            throw new OverflowException("Scaled integer is outside canonical bounds.");
        }

        return decimal.ToInt64(scaled);
    }

    private static string PseudonymousRequestId(
        string keyHex,
        string sourceArtifactSha256,
        string memberPath,
        long ordinal) =>
        "req-" + HmacHex(
            keyHex,
            "RideBound.Wp6.RequestPseudonym.v1",
            sourceArtifactSha256,
            memberPath,
            ordinal.ToString(CultureInfo.InvariantCulture))[..32];

    private static string HmacHex(string keyHex, string domain, params string[] values) =>
        HmacHex(Convert.FromHexString(keyHex), domain, values);

    private static string HmacHex(byte[] key, string domain, params string[] values)
    {
        using var hmac = new HMACSHA256(key);
        using var stream = new MemoryStream();
        var domainBytes = Encoding.UTF8.GetBytes(domain + "\0");
        stream.Write(domainBytes);
        Span<byte> length = stackalloc byte[sizeof(long)];

        foreach (var value in values)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(length, bytes.Length);
            stream.Write(length);
            stream.Write(bytes);
        }

        return Convert.ToHexStringLower(hmac.ComputeHash(stream.ToArray()));
    }

    private static bool TryDecodeKey(string value, out byte[] bytes)
    {
        bytes = [];

        if (!IsSha(value))
        {
            return false;
        }

        bytes = Convert.FromHexString(value);
        return true;
    }

    private static bool IsSha(string value) =>
        value is { Length: 64 }
        && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static string NodeId(int node) => $"node-{node:D6}";

    private static string Sha256(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static byte[] Canonicalize<T>(T value) =>
        CanonicalJson.Canonicalize(
            JsonSerializer.SerializeToUtf8Bytes(value, AuxiliaryJsonOptions));

    private static string FormatUtc(DateTime value) =>
        value.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture);

    private sealed record DemandRecord(
        long SourceRecordOrdinal,
        long ArrivalSourceMs,
        int OriginNode,
        int DestinationNode,
        string SelectionRankSha256);

    private sealed record DemandReadResult(
        long InputRecordCount,
        List<DemandRecord> Eligible,
        IReadOnlyList<NormalizationExclusion> Exclusions);

    private sealed record SourceSelectionRow(
        long SourceRecordOrdinal,
        string OriginNodeId,
        string DestinationNodeId,
        long ArrivalSourceMs,
        string SelectionRankSha256);

    private sealed record SnapshotHashArc(
        string FromNodeId,
        string ToNodeId,
        long TravelTimeMs);

    private sealed record SourceWindow(string StartUtc, string EndUtc);

    private sealed record SourceSelectionResult(
        IReadOnlyList<DemandRecord> Selected,
        IReadOnlySet<int> NodePool);

    private readonly record struct NodePair(int First, int Second);

    private sealed class SourceNormalizationException : Exception
    {
        public SourceNormalizationException(
            string code,
            string stage,
            string message,
            Exception? innerException = null)
            : base(message, innerException)
        {
            Code = code;
            Stage = stage;
        }

        public string Code { get; }

        public string Stage { get; }
    }
}
