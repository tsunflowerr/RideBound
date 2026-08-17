using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;

namespace RideBound.Benchmarking.Contracts;

public static partial class BenchmarkContractValidator
{
    private static readonly IReadOnlyDictionary<string, string> FailureStages =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["input.invalid"] = "preflight",
            ["artifact.mismatch"] = "preflightOrPostflight",
            ["capability.divergence"] = "negotiation",
            ["process.start-failed"] = "execution",
            ["process.crash"] = "execution",
            ["process.cancelled"] = "execution",
            ["harness.persistence-incomplete"] = "persistence",
            ["resource.wall-time-exceeded"] = "execution",
            ["resource.cpu-time-exceeded"] = "execution",
            ["resource.memory-exceeded"] = "execution",
            ["resource.process-count-exceeded"] = "execution",
            ["resource.stdin-bytes-exceeded"] = "execution",
            ["resource.stdout-bytes-exceeded"] = "execution",
            ["resource.stderr-bytes-exceeded"] = "execution",
            ["solver.unknown"] = "decision",
            ["protocol.invalid-output"] = "parsing",
            ["protocol.incomplete-output"] = "completion",
            ["state.divergence"] = "validation",
            ["metric.oracle-mismatch"] = "metrics",
            ["bundle.invalid"] = "packaging",
        };

    private static readonly IReadOnlySet<string> ExclusionRules =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "source.license-not-accepted",
            "source.checksum-mismatch",
            "source.invalid-record",
            "source.unreachable-node-pair",
            "scenario.exceeds-declared-capability",
            "scenario.unsupported-position-model",
            "arm.missing-required-capability",
            "arm.incomparable-pairing-class",
        };

    private static readonly IReadOnlySet<string> PairingClasses =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "wp4-common-candidate-v1",
            "wp4-multiple-plan-v1",
            "mechanical-single-arm-v1",
        };

    public static BenchmarkContractError? Validate(IBenchmarkDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var expectedVersion = document is FailureRecord
            ? BenchmarkContractVersions.V1_0_2
            : document is NormalizationReport or ScenarioContent
            ? BenchmarkContractVersions.V1_0_1
            : BenchmarkContractVersions.V1;
        var versionError = Exact(
            document.SchemaVersion,
            expectedVersion,
            "$.schemaVersion");

        if (versionError is not null)
        {
            return versionError;
        }

        return document switch
        {
            DatasetDescriptor value => ValidateDataset(value),
            NormalizationReport value => ValidateNormalization(value),
            ScenarioContent value => ValidateScenario(value),
            BenchmarkPlan value => ValidatePlan(value),
            RunRecord value => ValidateRun(value),
            ObservationIndexRow value => ValidateObservation(value),
            FailureRecord value => ValidateFailure(value),
            ExclusionRecord value => ValidateExclusion(value),
            MetricRow value => ValidateMetric(value),
            LogicalBundleManifest value => ValidateBundle(value),
            _ => Error(
                BenchmarkContractErrorCode.InvalidValue,
                "$",
                $"Unsupported benchmark document type '{document.GetType().Name}'."),
        };
    }

    private static BenchmarkContractError? ValidateDataset(DatasetDescriptor value)
    {
        return First(
            ArtifactId(value.DatasetId, "$.datasetId"),
            Text(value.Title, "$.title"),
            Version(value.ReleaseVersion, "$.releaseVersion", allowTwoPart: true),
            AbsoluteUri(value.PersistentUri, "$.persistentUri", allowDoi: true),
            AbsoluteUri(value.DownloadUri, "$.downloadUri", allowDoi: false),
            Utc(value.RetrievedAtUtc, "$.retrievedAtUtc"),
            FileName(value.PublisherArtifactName, "$.publisherArtifactName"),
            OptionalNonNegative(
                value.PublisherArtifactLengthBytes,
                "$.publisherArtifactLengthBytes"),
            OptionalMd5(value.PublisherMd5, "$.publisherMd5"),
            OptionalSha(value.SourceArtifactSha256, "$.sourceArtifactSha256"),
            Text(value.LicenseSpdx, "$.licenseSpdx"),
            AbsoluteUri(value.LicenseUri, "$.licenseUri", allowDoi: false),
            Text(value.Citation, "$.citation"),
            Text(value.Composition, "$.composition"),
            Text(value.CollectionLimit, "$.collectionLimit"),
            SortedOpaqueSet(value.AllowedUse, "$.allowedUse", allowEmpty: false),
            SortedOpaqueSet(value.ForbiddenClaim, "$.forbiddenClaim", allowEmpty: false),
            Text(value.MaintenanceNote, "$.maintenanceNote"));
    }

    private static BenchmarkContractError? ValidateNormalization(
        NormalizationReport value)
    {
        var error = First(
            ArtifactId(value.ReportId, "$.reportId"),
            ArtifactId(value.DatasetId, "$.datasetId"),
            Sha(value.SourceArtifactSha256, "$.sourceArtifactSha256"),
            Sha(value.SourceMemberInventorySha256, "$.sourceMemberInventorySha256"),
            ArtifactId(value.NormalizerId, "$.normalizerId"),
            Version(value.NormalizerVersion, "$.normalizerVersion"),
            Sha(value.NormalizerSourceSha256, "$.normalizerSourceSha256"),
            Sha(value.ConfigurationSha256, "$.configurationSha256"),
            NonNegative(value.InputRecordCount, "$.inputRecordCount"),
            NonNegative(value.EligibleRecordCount, "$.eligibleRecordCount"),
            NonNegative(value.SelectedRecordCount, "$.selectedRecordCount"),
            NonNegative(value.ExcludedRecordCount, "$.excludedRecordCount"),
            Sha(value.SelectionFrameSha256, "$.selectionFrameSha256"),
            Sha(value.ExclusionLogSha256, "$.exclusionLogSha256"),
            Exact(value.RoundingRuleId, "ties-to-even-v1", "$.roundingRuleId"),
            Exact(value.EventOrderingId, "ridebound-event-order-v1", "$.eventOrderingId"),
            ArtifactId(value.SelectionRuleId, "$.selectionRuleId"),
            Sha(value.ScenarioContentSha256, "$.scenarioContentSha256"),
            Sha(value.ScenarioHash, "$.scenarioHash"));

        if (error is not null)
        {
            return error;
        }

        if (value.InputRecordCount != value.EligibleRecordCount + value.ExcludedRecordCount)
        {
            return Conditional(
                "$.inputRecordCount",
                "inputRecordCount must equal eligibleRecordCount + excludedRecordCount.");
        }

        return value.SelectedRecordCount <= value.EligibleRecordCount
            ? null
            : Conditional(
                "$.selectedRecordCount",
                "selectedRecordCount cannot exceed eligibleRecordCount.");
    }

    private static BenchmarkContractError? ValidateScenario(ScenarioContent value)
    {
        var error = First(
            ArtifactId(value.ScenarioId, "$.scenarioId"),
            ArtifactId(value.DatasetId, "$.datasetId"),
            Sha(value.SourceArtifactSha256, "$.sourceArtifactSha256"),
            Sha(value.SourceSelectionSha256, "$.sourceSelectionSha256"),
            ArtifactId(value.NormalizerId, "$.normalizerId"),
            Version(value.NormalizerVersion, "$.normalizerVersion"),
            Sha(value.NormalizerSourceSha256, "$.normalizerSourceSha256"),
            Sha(value.NormalizerConfigurationSha256, "$.normalizerConfigurationSha256"),
            Exact(value.EventOrderingId, "ridebound-event-order-v1", "$.eventOrderingId"),
            ArtifactId(value.DriverSemanticsId, "$.driverSemanticsId"),
            ValidateTimeWindow(value.TimeWindow),
            SortedUnique(
                value.Fleet,
                vehicle => vehicle.VehicleId,
                "$.fleet",
                allowEmpty: false),
            SortedUnique(
                value.Requests,
                request => request.RequestId,
                "$.requests",
                allowEmpty: true),
            Sequence(value.TravelSnapshots, "$.travelSnapshots", allowEmpty: false),
            Sequence(value.Events, "$.events", allowEmpty: true));

        if (error is not null)
        {
            return error;
        }

        for (var index = 0; index < value.Fleet.Count; index++)
        {
            error = ValidateVehicle(value.Fleet[index], $"$.fleet[{index}]");

            if (error is not null)
            {
                return error;
            }
        }

        var sourceOrdinals = new HashSet<long>();

        for (var index = 0; index < value.Requests.Count; index++)
        {
            var request = value.Requests[index];
            error = ValidateRequest(request, value.TimeWindow, $"$.requests[{index}]");

            if (error is not null)
            {
                return error;
            }

            if (!sourceOrdinals.Add(request.SourceRecordOrdinal))
            {
                return Invalid(
                    $"$.requests[{index}].sourceRecordOrdinal",
                    "Request sourceRecordOrdinal values must be unique.");
            }
        }

        long previousSnapshotVersion = 0;

        for (var index = 0; index < value.TravelSnapshots.Count; index++)
        {
            var snapshot = value.TravelSnapshots[index];
            error = ValidateTravelSnapshot(snapshot, $"$.travelSnapshots[{index}]");

            if (error is not null)
            {
                return error;
            }

            if (snapshot.Version <= previousSnapshotVersion)
            {
                return Invalid(
                    $"$.travelSnapshots[{index}].version",
                    "Travel snapshot versions must be strictly increasing.");
            }

            previousSnapshotVersion = snapshot.Version;
        }

        for (var index = 0; index < value.Events.Count; index++)
        {
            error = ValidateEvent(
                value.Events[index],
                index == 0 ? null : value.Events[index - 1],
                value.TimeWindow,
                $"$.events[{index}]");

            if (error is not null)
            {
                return error;
            }
        }

        return ValidateSummary(value);
    }

    private static BenchmarkContractError? ValidateTimeWindow(ScenarioTimeWindow value)
    {
        if (value is null)
        {
            return Missing("$.timeWindow");
        }

        var error = First(
            Text(value.SourceTimezoneId, "$.timeWindow.sourceTimezoneId"),
            Utc(value.SourceWindowStartUtc, "$.timeWindow.sourceWindowStartUtc"),
            Utc(value.SourceWindowEndUtc, "$.timeWindow.sourceWindowEndUtc"),
            ExactInteger(value.WarmupStartMs, 0, "$.timeWindow.warmupStartMs"),
            NonNegative(value.ScoreStartMs, "$.timeWindow.scoreStartMs"),
            Positive(value.HorizonEndMs, "$.timeWindow.horizonEndMs"),
            NonNegative(value.DrainEndMs, "$.timeWindow.drainEndMs"),
            ArtifactId(value.BatchingId, "$.timeWindow.batchingId"));

        if (error is not null)
        {
            return error;
        }

        if (ParseUtc(value.SourceWindowStartUtc) >= ParseUtc(value.SourceWindowEndUtc))
        {
            return Conditional(
                "$.timeWindow.sourceWindowEndUtc",
                "Source window end must be after source window start.");
        }

        return value.ScoreStartMs < value.HorizonEndMs
            && value.HorizonEndMs <= value.DrainEndMs
            ? null
            : Conditional(
                "$.timeWindow",
                "Required ordering is warmupStartMs <= scoreStartMs < horizonEndMs <= drainEndMs.");
    }

    private static BenchmarkContractError? ValidateVehicle(
        ScenarioVehicle value,
        string path)
    {
        var error = First(
            Opaque(value.VehicleId, $"{path}.vehicleId"),
            Positive(value.Capacity, $"{path}.capacity"),
            NonNegative(value.OccupiedSeats, $"{path}.occupiedSeats"),
            ValidatePosition(value.Position, $"{path}.position"),
            SortedOpaqueSet(value.OnboardRequestIds, $"{path}.onboardRequestIds", true),
            SortedOpaqueSet(value.AcceptedRequestIds, $"{path}.acceptedRequestIds", true),
            ValidateRoute(value.InitialRoute, $"{path}.initialRoute"),
            Opaque(value.SourceProvenanceId, $"{path}.sourceProvenanceId"));

        if (error is not null)
        {
            return error;
        }

        if (value.OccupiedSeats > value.Capacity)
        {
            return Conditional(
                $"{path}.occupiedSeats",
                "occupiedSeats cannot exceed capacity.");
        }

        return value.OnboardRequestIds.Except(
            value.AcceptedRequestIds,
            StringComparer.Ordinal).Any()
            ? Conditional(
                $"{path}.acceptedRequestIds",
                "acceptedRequestIds must include every onboard request.")
            : null;
    }

    private static BenchmarkContractError? ValidatePosition(
        ScenarioPosition value,
        string path)
    {
        return value switch
        {
            null => Missing(path),
            NodeScenarioPosition node => Opaque(node.NodeId, $"{path}.nodeId"),
            EdgeProgressScenarioPosition edge => First(
                Opaque(edge.FromNodeId, $"{path}.fromNodeId"),
                Opaque(edge.ToNodeId, $"{path}.toNodeId"),
                Opaque(edge.EdgeId, $"{path}.edgeId"),
                Range(edge.ProgressPermille, 1, 999, $"{path}.progressPermille"),
                string.Equals(edge.FromNodeId, edge.ToNodeId, StringComparison.Ordinal)
                    ? Invalid(path, "Edge endpoints must be distinct.")
                    : null),
            _ => Invalid(path, "Unsupported position kind."),
        };
    }

    private static BenchmarkContractError? ValidateRoute(ScenarioRoute value, string path)
    {
        if (value is null)
        {
            return Missing(path);
        }

        var error = First(
            NonNegative(value.PlanVersion, $"{path}.planVersion"),
            NonNegative(value.ExecutedStopCount, $"{path}.executedStopCount"),
            Sequence(value.FrozenPrefix, $"{path}.frozenPrefix", true),
            Sequence(value.MutableSuffix, $"{path}.mutableSuffix", true));

        if (error is not null)
        {
            return error;
        }

        if (value.ExecutedStopCount > value.FrozenPrefix.Count)
        {
            return Conditional(
                $"{path}.executedStopCount",
                "executedStopCount cannot exceed frozenPrefix count.");
        }

        var stopIds = new HashSet<string>(StringComparer.Ordinal);
        var stops = value.FrozenPrefix.Concat(value.MutableSuffix).ToArray();

        for (var index = 0; index < stops.Length; index++)
        {
            var stop = stops[index];
            var stopPath = $"{path}.stops[{index}]";
            error = First(
                Opaque(stop.StopId, $"{stopPath}.stopId"),
                Opaque(stop.NodeId, $"{stopPath}.nodeId"),
                OptionalOpaque(stop.RequestId, $"{stopPath}.requestId"),
                NonNegative(stop.ServiceDurationMs, $"{stopPath}.serviceDurationMs"));

            if (error is not null)
            {
                return error;
            }

            if (!stopIds.Add(stop.StopId))
            {
                return Invalid($"{stopPath}.stopId", "Route stop IDs must be unique.");
            }

            if (stop.Kind is ScenarioRouteStopKind.Pickup or ScenarioRouteStopKind.DropOff
                && stop.RequestId is null)
            {
                return Conditional(
                    $"{stopPath}.requestId",
                    "Pickup/drop-off stops require requestId.");
            }

            if (stop.Kind == ScenarioRouteStopKind.Waypoint && stop.RequestId is not null)
            {
                return Conditional(
                    $"{stopPath}.requestId",
                    "Waypoint stops must omit requestId.");
            }
        }

        return null;
    }

    private static BenchmarkContractError? ValidateRequest(
        ScenarioRequest value,
        ScenarioTimeWindow window,
        string path)
    {
        var error = First(
            Opaque(value.RequestId, $"{path}.requestId"),
            NonNegative(value.SourceRecordOrdinal, $"{path}.sourceRecordOrdinal"),
            NonNegative(value.ArrivalTimeMs, $"{path}.arrivalTimeMs"),
            Opaque(value.OriginNodeId, $"{path}.originNodeId"),
            Opaque(value.DestinationNodeId, $"{path}.destinationNodeId"),
            NonNegative(value.EarliestPickupMs, $"{path}.earliestPickupMs"),
            NonNegative(value.LatestPickupMs, $"{path}.latestPickupMs"),
            Positive(value.MaxRideTimeMs, $"{path}.maxRideTimeMs"),
            Positive(value.PartySize, $"{path}.partySize"),
            Opaque(value.ServiceClass, $"{path}.serviceClass"),
            Opaque(value.CommitmentPolicyId, $"{path}.commitmentPolicyId"),
            EnumString(
                value.PolicyObservationClass,
                ["fixtureDefined", "observed", "syntheticPolicyOverlay"],
                $"{path}.policyObservationClass"),
            Opaque(value.SourceProvenanceId, $"{path}.sourceProvenanceId"));

        if (error is not null)
        {
            return error;
        }

        if (value.ArrivalTimeMs < window.WarmupStartMs
            || value.ArrivalTimeMs > window.HorizonEndMs
            || value.EarliestPickupMs < value.ArrivalTimeMs
            || value.LatestPickupMs < value.EarliestPickupMs)
        {
            return Conditional(
                path,
                "Request time ordering must be warmup <= arrival <= earliest <= latest and arrival <= horizon.");
        }

        return string.Equals(
            value.OriginNodeId,
            value.DestinationNodeId,
            StringComparison.Ordinal)
            ? Conditional(
                $"{path}.destinationNodeId",
                "Origin and destination must be distinct for executable scenarios.")
            : null;
    }

    private static BenchmarkContractError? ValidateTravelSnapshot(
        ScenarioTravelSnapshot value,
        string path)
    {
        var error = First(
            Positive(value.Version, $"{path}.version"),
            Sha(value.SnapshotHash, $"{path}.snapshotHash"),
            Sequence(value.Arcs, $"{path}.arcs", allowEmpty: false));

        if (error is not null)
        {
            return error;
        }

        (string From, string To)? previous = null;

        for (var index = 0; index < value.Arcs.Count; index++)
        {
            var arc = value.Arcs[index];
            var arcPath = $"{path}.arcs[{index}]";
            error = First(
                Opaque(arc.FromNodeId, $"{arcPath}.fromNodeId"),
                Opaque(arc.ToNodeId, $"{arcPath}.toNodeId"),
                Positive(arc.TravelTimeMs, $"{arcPath}.travelTimeMs"));

            if (error is not null)
            {
                return error;
            }

            if (string.Equals(arc.FromNodeId, arc.ToNodeId, StringComparison.Ordinal))
            {
                return Invalid(arcPath, "Travel arc endpoints must be distinct.");
            }

            var key = (arc.FromNodeId, arc.ToNodeId);

            if (previous is not null && CompareArc(previous.Value, key) >= 0)
            {
                return Invalid(
                    arcPath,
                    "Travel arcs must be unique and sorted by fromNodeId/toNodeId ordinal.");
            }

            previous = key;
        }

        return null;
    }

    private static BenchmarkContractError? ValidateEvent(
        ScenarioEvent value,
        ScenarioEvent? previous,
        ScenarioTimeWindow window,
        string path)
    {
        var error = First(
            Positive(value.EventSequence, $"{path}.eventSequence"),
            Range(value.SimTimeMs, window.WarmupStartMs, window.DrainEndMs, $"{path}.simTimeMs"),
            Opaque(value.EventType, $"{path}.eventType"),
            EventTypeRank(value.EventType) != int.MaxValue
                ? null
                : Invalid($"{path}.eventType", "Event type is not registered by ridebound-event-order-v1."),
            NonNegative(value.SourceRecordOrdinal, $"{path}.sourceRecordOrdinal"),
            Opaque(value.StableSubjectId, $"{path}.stableSubjectId"),
            CanonicalJsonHex(value.PayloadCanonicalJsonHex, $"{path}.payloadCanonicalJsonHex"),
            Sha(value.PayloadSha256, $"{path}.payloadSha256"),
            Opaque(value.SourceProvenanceId, $"{path}.sourceProvenanceId"));

        if (error is not null)
        {
            return error;
        }

        var payload = Convert.FromHexString(value.PayloadCanonicalJsonHex);
        var actualHash = Convert.ToHexStringLower(SHA256.HashData(payload));

        if (!string.Equals(actualHash, value.PayloadSha256, StringComparison.Ordinal))
        {
            return Conditional(
                $"{path}.payloadSha256",
                "payloadSha256 does not match payloadCanonicalJsonHex bytes.");
        }

        if (previous is null)
        {
            return null;
        }

        if (value.EventSequence <= previous.EventSequence)
        {
            return Invalid(
                $"{path}.eventSequence",
                "Event sequences must be strictly increasing.");
        }

        if (value.SimTimeMs < previous.SimTimeMs)
        {
            return Invalid($"{path}.simTimeMs", "Event time must be nondecreasing.");
        }

        if (value.SimTimeMs == previous.SimTimeMs
            && !value.SourceSequencePreserved
            && !previous.SourceSequencePreserved
            && CompareEventOrder(previous, value) >= 0)
        {
            return Invalid(path, "Tied events violate ridebound-event-order-v1.");
        }

        return null;
    }

    private static BenchmarkContractError? ValidateSummary(ScenarioContent value)
    {
        var summary = value.ValidationSummary;

        if (summary is null)
        {
            return Missing("$.validationSummary");
        }

        var error = First(
            NonNegative(summary.VehicleCount, "$.validationSummary.vehicleCount"),
            NonNegative(summary.RequestCount, "$.validationSummary.requestCount"),
            NonNegative(summary.NodeCount, "$.validationSummary.nodeCount"),
            NonNegative(summary.DirectedArcCount, "$.validationSummary.directedArcCount"),
            NonNegative(summary.SnapshotCount, "$.validationSummary.snapshotCount"),
            NonNegative(summary.EventCount, "$.validationSummary.eventCount"),
            NonNegative(summary.ExcludedSourceRowCount, "$.validationSummary.excludedSourceRowCount"),
            NonNegative(summary.SelectedSourceRowCount, "$.validationSummary.selectedSourceRowCount"),
            ExactInteger(summary.DuplicateIdCount, 0, "$.validationSummary.duplicateIdCount"),
            ExactInteger(summary.UnreachableSelectedRowCount, 0, "$.validationSummary.unreachableSelectedRowCount"),
            ExactInteger(summary.InvalidTimeRowCount, 0, "$.validationSummary.invalidTimeRowCount"),
            ExactInteger(summary.OverflowRowCount, 0, "$.validationSummary.overflowRowCount"),
            Sha(summary.InvariantSetHash, "$.validationSummary.invariantSetHash"));

        if (error is not null)
        {
            return error;
        }

        var firstSnapshot = value.TravelSnapshots[0];
        var nodeIds = firstSnapshot.Arcs
            .SelectMany(arc => new[] { arc.FromNodeId, arc.ToNodeId })
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var expectedArcCount = checked(nodeIds.Length * (nodeIds.Length - 1));
        var firstTopology = firstSnapshot.Arcs
            .Select(arc => (arc.FromNodeId, arc.ToNodeId))
            .ToArray();
        var requiredNodeIds = value.Requests
            .SelectMany(request => new[] { request.OriginNodeId, request.DestinationNodeId })
            .Concat(value.Fleet.SelectMany(VehicleNodeIds))
            .Distinct(StringComparer.Ordinal);
        var completeTopology = firstSnapshot.Arcs.Count == expectedArcCount
            && requiredNodeIds.All(nodeId => nodeIds.Contains(nodeId, StringComparer.Ordinal))
            && value.TravelSnapshots.All(
                snapshot => snapshot.Arcs
                    .Select(arc => (arc.FromNodeId, arc.ToNodeId))
                    .SequenceEqual(firstTopology));

        if (!completeTopology)
        {
            return Conditional(
                "$.travelSnapshots",
                "Travel snapshots must share one complete directed non-self topology covering every scenario node.");
        }

        var countsMatch = summary.VehicleCount == value.Fleet.Count
            && summary.RequestCount == value.Requests.Count
            && summary.NodeCount == nodeIds.Length
            && summary.DirectedArcCount == firstSnapshot.Arcs.Count
            && summary.SnapshotCount == value.TravelSnapshots.Count
            && summary.EventCount == value.Events.Count
            && summary.SelectedSourceRowCount >= value.Requests.Count;

        return countsMatch
            ? null
            : Conditional(
                "$.validationSummary",
                "Validation summary counts do not match canonical scenario arrays.");
    }

    private static BenchmarkContractError? ValidatePlan(BenchmarkPlan value)
    {
        var error = First(
            ArtifactId(value.PlanId, "$.planId"),
            value.EvidenceClass is EvidenceClass.Mechanical or EvidenceClass.Development
                ? null
                : Invalid("$.evidenceClass", "WP6 plan permits mechanical/development only."),
            Exact(value.ClaimProfileId, "wp6-mechanical-only-v1", "$.claimProfileId"),
            SortedShaSet(value.ScenarioHashes, "$.scenarioHashes", false),
            SortedUnique(value.Arms, arm => arm.ArmId, "$.arms", false),
            EnumString(value.PairingClassId, PairingClasses, "$.pairingClassId"),
            Sha(value.MasterSeedHex, "$.masterSeedHex"),
            NonNegative(value.WarmupRunCount, "$.warmupRunCount"),
            Range(value.MeasuredRepeatCount, 3, ProtocolLimits.MaxCanonicalInteger, "$.measuredRepeatCount"),
            Exact(value.RunOrderId, "hash-counterbalanced-v1", "$.runOrderId"),
            ArtifactId(value.ResourceProfileId, "$.resourceProfileId"),
            Exact(value.FailureRuleSetId, "wp6-failure-v1.0.2", "$.failureRuleSetId"),
            Exact(value.ExclusionRuleSetId, "wp6-exclusion-v1", "$.exclusionRuleSetId"),
            Sha(value.MetricRegistryHash, "$.metricRegistryHash"),
            ValidateRunnerArtifact(value.RunnerArtifact),
            Sha(value.HarnessSourceSha256, "$.harnessSourceSha256"),
            Sha(value.OracleSourceSha256, "$.oracleSourceSha256"));

        if (error is not null)
        {
            return error;
        }

        for (var index = 0; index < value.Arms.Count; index++)
        {
            error = ValidateArm(value.Arms[index], value.PairingClassId, $"$.arms[{index}]");

            if (error is not null)
            {
                return error;
            }
        }

        if (value.PairingClassId == "mechanical-single-arm-v1" && value.Arms.Count != 1)
        {
            return Conditional("$.arms", "mechanical-single-arm-v1 requires exactly one arm.");
        }

        if (value.PairingClassId == "wp4-common-candidate-v1")
        {
            var first = value.Arms[0];

            if (value.Arms.Count < 2
                || value.Arms.Any(
                    arm => arm.ArmId is not ("b1" or "b2" or "b3" or "b4" or "c1" or "c2"))
                || value.Arms.Any(
                arm => arm.CandidateGeneratorId != first.CandidateGeneratorId
                    || arm.CandidateWorkBudget != first.CandidateWorkBudget
                    || arm.ValidatorVersion != first.ValidatorVersion
                    || arm.SolverId != first.SolverId
                    || arm.SolverVersion != first.SolverVersion
                    || arm.SolverWorkBudget != first.SolverWorkBudget
                    || arm.CapabilitySelectionSha256 != first.CapabilitySelectionSha256))
            {
                return Conditional(
                    "$.arms",
                    "wp4-common-candidate-v1 requires at least two allowed arms and identical candidate, validator, solver-work and capability mechanics.");
            }
        }

        if (value.PairingClassId == "wp4-multiple-plan-v1"
            && value.Arms.Any(
                arm => arm.ArmId != "b5"
                    && !arm.ArmId.StartsWith("b5-", StringComparison.Ordinal)))
        {
            return Conditional(
                "$.arms",
                "wp4-multiple-plan-v1 accepts only B5-family arms.");
        }

        return null;
    }

    private static BenchmarkContractError? ValidateRunnerArtifact(
        RunnerArtifactIdentity value)
    {
        return value is null
            ? Missing("$.runnerArtifact")
            : First(
                Sha(value.RunnerExecutableSha256, "$.runnerArtifact.runnerExecutableSha256"),
                Sha(value.RunnerAssemblySha256, "$.runnerArtifact.runnerAssemblySha256"),
                Sha(value.ContractsAssemblySha256, "$.runnerArtifact.contractsAssemblySha256"),
                Sha(value.RuntimeInventorySha256, "$.runnerArtifact.runtimeInventorySha256"),
                ArtifactId(value.LaunchContractId, "$.runnerArtifact.launchContractId"));
    }

    private static BenchmarkContractError? ValidateArm(
        BenchmarkArm value,
        string pairingClassId,
        string path)
    {
        return First(
            ArtifactId(value.ArmId, $"{path}.armId"),
            Opaque(value.PolicyId, $"{path}.policyId"),
            Opaque(value.PolicyVersion, $"{path}.policyVersion"),
            Sha(value.PolicyConfigurationSha256, $"{path}.policyConfigurationSha256"),
            Sha(value.EffectiveConfigurationSha256, $"{path}.effectiveConfigurationSha256"),
            ArtifactId(value.CandidateGeneratorId, $"{path}.candidateGeneratorId"),
            NonNegative(value.CandidateWorkBudget, $"{path}.candidateWorkBudget"),
            Opaque(value.ValidatorVersion, $"{path}.validatorVersion"),
            Opaque(value.SolverId, $"{path}.solverId"),
            Opaque(value.SolverVersion, $"{path}.solverVersion"),
            NonNegative(value.SolverWorkBudget, $"{path}.solverWorkBudget"),
            Sha(value.CapabilitySelectionSha256, $"{path}.capabilitySelectionSha256"),
            Exact(value.PairingClassId, pairingClassId, $"{path}.pairingClassId"));
    }

    private static BenchmarkContractError? ValidateRun(RunRecord value)
    {
        var error = First(
            ArtifactId(value.RunId, "$.runId"),
            Sha(value.PlanHash, "$.planHash"),
            Sha(value.ScenarioHash, "$.scenarioHash"),
            ArtifactId(value.ArmId, "$.armId"),
            NonNegative(value.RepeatIndex, "$.repeatIndex"),
            NonNegative(value.AttemptIndex, "$.attemptIndex"),
            Sha(value.PolicyConfigurationSha256, "$.policyConfigurationSha256"),
            Sha(value.EffectiveConfigurationSha256, "$.effectiveConfigurationSha256"),
            Sha(value.ComponentSeedHex, "$.componentSeedHex"),
            Sha(value.RunnerArtifactSha256, "$.runnerArtifactSha256"),
            Sha(value.HarnessSourceSha256, "$.harnessSourceSha256"),
            Positive(value.ExecutionOrdinal, "$.executionOrdinal"),
            Utc(value.StartedAtUtc, "$.startedAtUtc"),
            Utc(value.FinishedAtUtc, "$.finishedAtUtc"),
            NonNegative(value.WallTimeMs, "$.wallTimeMs"),
            NonNegative(value.CpuTimeMs, "$.cpuTimeMs"),
            NonNegative(value.PeakWorkingSetBytes, "$.peakWorkingSetBytes"),
            NonNegative(value.SpawnedProcessCount, "$.spawnedProcessCount"),
            OptionalSafeInteger(value.ExitCode, "$.exitCode"),
            Sha(value.ArtifactPreflightSha256, "$.artifactPreflightSha256"),
            Sha(value.ArtifactPostflightSha256, "$.artifactPostflightSha256"),
            ValidateRunFile(value.InputFile, "$.inputFile"),
            ValidateRunFile(value.OutputFile, "$.outputFile"),
            ValidateRunFile(value.StderrFile, "$.stderrFile"),
            ValidateRunFile(value.ResourceSamplesFile, "$.resourceSamplesFile"),
            ValidateRunFile(value.ObservationIndexFile, "$.observationIndexFile"),
            OptionalOpaque(value.LastEpochId, "$.lastEpochId"),
            OptionalSha(value.LastEventHash, "$.lastEventHash"),
            OptionalSha(value.LastDecisionHash, "$.lastDecisionHash"),
            OptionalSha(value.LastCheckpointHash, "$.lastCheckpointHash"),
            OptionalArtifactId(value.FailureRecordId, "$.failureRecordId"),
            OptionalArtifactId(value.ExclusionRecordId, "$.exclusionRecordId"));

        if (error is not null)
        {
            return error;
        }

        if (ParseUtc(value.StartedAtUtc) > ParseUtc(value.FinishedAtUtc))
        {
            return Conditional("$.finishedAtUtc", "finish must not precede start.");
        }

        return value.TerminalStatus switch
        {
            RunTerminalStatus.Succeeded when value.ExitCode == 0
                && value.FailureRecordId is null
                && value.ExclusionRecordId is null => null,
            RunTerminalStatus.Failed when value.FailureRecordId is not null
                && value.ExclusionRecordId is null => null,
            RunTerminalStatus.Excluded when value.ExclusionRecordId is not null
                && value.FailureRecordId is null => null,
            _ => Conditional(
                "$.terminalStatus",
                "Terminal status does not agree with exit/failure/exclusion fields."),
        };
    }

    private static BenchmarkContractError? ValidateRunFile(
        RunFileEvidence value,
        string path)
    {
        return value is null
            ? Missing(path)
            : First(
                RelativePath(value.RelativePath, $"{path}.relativePath"),
                NonNegative(value.LengthBytes, $"{path}.lengthBytes"),
                Sha(value.Sha256, $"{path}.sha256"));
    }

    private static BenchmarkContractError? ValidateObservation(ObservationIndexRow value)
    {
        var error = First(
            Positive(value.RecordSequence, "$.recordSequence"),
            ArtifactId(value.RunId, "$.runId"),
            Sha(value.ScenarioHash, "$.scenarioHash"),
            ArtifactId(value.ArmId, "$.armId"),
            NonNegative(value.RepeatIndex, "$.repeatIndex"),
            NonNegative(value.AttemptIndex, "$.attemptIndex"),
            Positive(value.LineNumber, "$.lineNumber"),
            Sha(value.EnvelopeSha256, "$.envelopeSha256"),
            OptionalNonNegative(value.EpochId, "$.epochId"),
            OptionalNonNegative(value.SimTimeMs, "$.simTimeMs"),
            OptionalNonNegative(value.EventSequence, "$.eventSequence"),
            SortedOpaqueSet(value.RequestIds, "$.requestIds", true),
            SortedOpaqueSet(value.VehicleIds, "$.vehicleIds", true),
            OptionalSha(value.DecisionHash, "$.decisionHash"),
            OptionalSha(value.CertificateHash, "$.certificateHash"));

        if (error is not null)
        {
            return error;
        }

        return value.RecordKind switch
        {
            ObservationRecordKind.InputEvent when value.TranscriptRole == TranscriptRole.Input
                && value.EpochId is not null
                && value.SimTimeMs is not null
                && value.EventSequence is not null => null,
            ObservationRecordKind.OutputDecision when value.TranscriptRole == TranscriptRole.Output
                && value.DecisionHash is not null => null,
            ObservationRecordKind.DecisionAck when value.TranscriptRole == TranscriptRole.Input
                && value.DecisionHash is not null => null,
            ObservationRecordKind.Checkpoint when value.TranscriptRole == TranscriptRole.Output => null,
            ObservationRecordKind.RunTerminal => null,
            _ => Conditional(
                "$.recordKind",
                "Observation conditional context fields do not match recordKind/transcriptRole."),
        };
    }

    private static BenchmarkContractError? ValidateFailure(FailureRecord value)
    {
        var error = First(
            Positive(value.RecordSequence, "$.recordSequence"),
            ArtifactId(value.FailureRecordId, "$.failureRecordId"),
            ArtifactId(value.RunId, "$.runId"),
            Sha(value.PlanHash, "$.planHash"),
            Sha(value.ScenarioHash, "$.scenarioHash"),
            ArtifactId(value.ArmId, "$.armId"),
            NonNegative(value.RepeatIndex, "$.repeatIndex"),
            NonNegative(value.AttemptIndex, "$.attemptIndex"),
            FailureStages.ContainsKey(value.Code)
                ? null
                : Invalid("$.code", "Unknown WP6 failure code."),
            Opaque(value.Stage, "$.stage"),
            NonNegative(value.FirstObservedMonotonicOffsetMs, "$.firstObservedMonotonicOffsetMs"),
            Opaque(value.SourceComponent, "$.sourceComponent"),
            RelativePath(value.EvidenceRelativePath, "$.evidenceRelativePath"),
            Sha(value.EvidenceSha256, "$.evidenceSha256"),
            Text(value.SafeMessage, "$.safeMessage"),
            Exact(value.RetryAuthorization, "none", "$.retryAuthorization"),
            SortedOpaqueSet(value.AffectedDenominatorIds, "$.affectedDenominatorIds", false));

        if (error is not null)
        {
            return error;
        }

        var expectedStage = FailureStages[value.Code];
        return expectedStage == "preflightOrPostflight"
            ? value.Stage is "preflight" or "postflight"
                ? null
                : Invalid("$.stage", "artifact.mismatch stage must be preflight or postflight.")
            : Exact(value.Stage, expectedStage, "$.stage");
    }

    private static BenchmarkContractError? ValidateExclusion(ExclusionRecord value)
    {
        return First(
            Positive(value.RecordSequence, "$.recordSequence"),
            ArtifactId(value.ExclusionRecordId, "$.exclusionRecordId"),
            ExclusionRules.Contains(value.RuleId)
                ? null
                : Invalid("$.ruleId", "Unknown WP6 exclusion rule."),
            Version(value.RuleVersion, "$.ruleVersion"),
            Sha(value.RuleSetHash, "$.ruleSetHash"),
            Opaque(value.Stage, "$.stage"),
            Opaque(value.SubjectKind, "$.subjectKind"),
            Opaque(value.SubjectId, "$.subjectId"),
            OptionalSha(value.ScenarioHash, "$.scenarioHash"),
            OptionalArtifactId(value.ArmId, "$.armId"),
            OptionalNonNegative(value.RepeatIndex, "$.repeatIndex"),
            value.BeforeOutcome
                ? null
                : Conditional("$.beforeOutcome", "Exclusion must be decided before outcome."),
            RelativePath(value.EvidenceRelativePath, "$.evidenceRelativePath"),
            Sha(value.EvidenceSha256, "$.evidenceSha256"),
            SortedOpaqueSet(value.RetainedDenominatorIds, "$.retainedDenominatorIds", false),
            Text(value.SafeReason, "$.safeReason"));
    }

    private static BenchmarkContractError? ValidateMetric(MetricRow value)
    {
        var error = First(
            Sha(value.MetricRegistryHash, "$.metricRegistryHash"),
            Opaque(value.MetricId, "$.metricId"),
            Version(value.MetricVersion, "$.metricVersion"),
            ArtifactId(value.RunId, "$.runId"),
            Sha(value.ScenarioHash, "$.scenarioHash"),
            ArtifactId(value.ArmId, "$.armId"),
            NonNegative(value.RepeatIndex, "$.repeatIndex"),
            NonNegative(value.AttemptIndex, "$.attemptIndex"),
            Opaque(value.ScopeId, "$.scopeId"),
            OptionalSafeInteger(value.ValueInteger, "$.valueInteger"),
            Opaque(value.UnitId, "$.unitId"),
            OptionalSafeInteger(value.NumeratorInteger, "$.numeratorInteger"),
            OptionalOpaque(value.DenominatorId, "$.denominatorId"),
            OptionalNonNegative(value.DenominatorInteger, "$.denominatorInteger"),
            OptionalOpaque(value.MissingReasonId, "$.missingReasonId"),
            Sha(value.RawEvidenceSha256, "$.rawEvidenceSha256"),
            Sha(value.CalculatorSourceSha256, "$.calculatorSourceSha256"));

        if (error is not null)
        {
            return error;
        }

        return value.ValueStatus switch
        {
            MetricValueStatus.Observed when value.ValueInteger is not null
                && value.MissingReasonId is null => null,
            MetricValueStatus.Missing when value.ValueInteger is null
                && value.MissingReasonId is not null => null,
            MetricValueStatus.NotApplicable when value.ValueInteger is null
                && value.NumeratorInteger is null
                && value.DenominatorInteger is null
                && value.MissingReasonId is not null => null,
            _ => Conditional(
                "$.valueStatus",
                "Metric value/missing fields do not match valueStatus."),
        };
    }

    private static BenchmarkContractError? ValidateBundle(LogicalBundleManifest value)
    {
        var error = First(
            ArtifactId(value.BundleId, "$.bundleId"),
            value.EvidenceClass is EvidenceClass.Mechanical or EvidenceClass.Development
                ? null
                : Invalid("$.evidenceClass", "WP6 bundle permits mechanical/development only."),
            Exact(value.ClaimProfileId, "wp6-mechanical-only-v1", "$.claimProfileId"),
            Sha(value.PlanHash, "$.planHash"),
            Sha(value.MetricSetHash, "$.metricSetHash"),
            Sha(value.SourceInventorySha256, "$.sourceInventorySha256"),
            Sha(value.RuntimeInventorySha256, "$.runtimeInventorySha256"),
            SortedUnique(value.Artifacts, artifact => artifact.RelativePath, "$.artifacts", false));

        if (error is not null)
        {
            return error;
        }

        var caseInsensitivePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < value.Artifacts.Count; index++)
        {
            var artifact = value.Artifacts[index];
            var path = $"$.artifacts[{index}]";
            error = First(
                RelativePath(artifact.RelativePath, $"{path}.relativePath", requireDataPrefix: true),
                NonNegative(artifact.LengthBytes, $"{path}.lengthBytes"),
                Sha(artifact.Sha256, $"{path}.sha256"),
                Text(artifact.MediaType, $"{path}.mediaType"),
                Opaque(artifact.ProducerActivityId, $"{path}.producerActivityId"),
                SortedOpaqueSet(artifact.SourceEntityIds, $"{path}.sourceEntityIds", false));

            if (error is not null)
            {
                return error;
            }

            if (artifact.RelativePath == "data/bundle-manifest.json")
            {
                return Conditional(
                    $"{path}.relativePath",
                    "Logical manifest must not list itself.");
            }

            if (!caseInsensitivePaths.Add(artifact.RelativePath))
            {
                return Invalid(
                    $"{path}.relativePath",
                    "Artifact paths have a case-insensitive collision.");
            }
        }

        return null;
    }

    private static BenchmarkContractError? CanonicalJsonHex(string value, string path)
    {
        var hexError = LowerHex(value, path, exactLength: null, requireEvenLength: true);

        if (hexError is not null)
        {
            return hexError;
        }

        try
        {
            var bytes = Convert.FromHexString(value);
            var canonical = CanonicalJson.Canonicalize(bytes);

            return bytes.AsSpan().SequenceEqual(canonical)
                ? null
                : Invalid(path, "Embedded JSON bytes must already be canonical.");
        }
        catch (CanonicalJsonException exception)
        {
            return Invalid(path, $"Embedded JSON is invalid: {exception.Message}");
        }
    }

    private static BenchmarkContractError? ArtifactId(string value, string path) =>
        value is not null && ArtifactIdRegex().IsMatch(value)
            ? null
            : Invalid(path, "Value must match [a-z0-9][a-z0-9._-]{0,127}.");

    private static BenchmarkContractError? OptionalArtifactId(string? value, string path) =>
        value is null ? null : ArtifactId(value, path);

    private static BenchmarkContractError? Opaque(string value, string path)
    {
        if (string.IsNullOrEmpty(value))
        {
            return Invalid(path, "Identifier must not be empty.");
        }

        return Encoding.UTF8.GetByteCount(value) <= 128
            ? null
            : Invalid(path, "Identifier exceeds 128 UTF-8 bytes.");
    }

    private static BenchmarkContractError? OptionalOpaque(string? value, string path) =>
        value is null ? null : Opaque(value, path);

    private static BenchmarkContractError? Text(
        string value,
        string path,
        int maximumUtf8Bytes = int.MaxValue)
    {
        if (string.IsNullOrEmpty(value))
        {
            return Invalid(path, "Text must not be empty.");
        }

        return Encoding.UTF8.GetByteCount(value) <= maximumUtf8Bytes
            ? null
            : Invalid(path, $"Text exceeds {maximumUtf8Bytes} UTF-8 bytes.");
    }

    private static BenchmarkContractError? Version(
        string value,
        string path,
        bool allowTwoPart = false)
    {
        var valid = ContractVersionRegex().IsMatch(value)
            || allowTwoPart && ReleaseVersionRegex().IsMatch(value);
        return valid ? null : Invalid(path, "Version is not canonical decimal dotted form.");
    }

    private static BenchmarkContractError? Sha(string value, string path) =>
        LowerHex(value, path, 64, false);

    private static BenchmarkContractError? OptionalSha(string? value, string path) =>
        value is null ? null : Sha(value, path);

    private static BenchmarkContractError? OptionalMd5(string? value, string path) =>
        value is null ? null : LowerHex(value, path, 32, false);

    private static BenchmarkContractError? LowerHex(
        string value,
        string path,
        int? exactLength,
        bool requireEvenLength)
    {
        if (string.IsNullOrEmpty(value)
            || exactLength is not null && value.Length != exactLength
            || requireEvenLength && (value.Length & 1) != 0
            || value.Any(character => character is not (>= '0' and <= '9')
                and not (>= 'a' and <= 'f')))
        {
            return Invalid(path, "Value must be lowercase hexadecimal with the required length.");
        }

        return null;
    }

    private static BenchmarkContractError? Utc(string value, string path)
    {
        if (!UtcRegex().IsMatch(value)
            || !DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out _))
        {
            return Invalid(path, "Timestamp must be canonical RFC 3339 UTC ending Z.");
        }

        return null;
    }

    private static DateTimeOffset ParseUtc(string value) =>
        DateTimeOffset.Parse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

    private static BenchmarkContractError? AbsoluteUri(
        string value,
        string path,
        bool allowDoi)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme is "https" or "http" || allowDoi && uri.Scheme == "doi")
            ? null
            : Invalid(path, "URI must be an absolute HTTP(S) URI.");
    }

    private static BenchmarkContractError? FileName(string value, string path)
    {
        return !string.IsNullOrEmpty(value)
            && value.IndexOfAny(['/', '\\']) < 0
            && value is not "." and not ".."
            ? null
            : Invalid(path, "Publisher artifact name must be one safe filename.");
    }

    private static BenchmarkContractError? RelativePath(
        string value,
        string path,
        bool requireDataPrefix = false)
    {
        if (string.IsNullOrEmpty(value)
            || value.StartsWith("/", StringComparison.Ordinal)
            || value.Contains('\\')
            || value.Contains("//", StringComparison.Ordinal)
            || value.Split('/').Any(segment => segment is "" or "." or "..")
            || requireDataPrefix && !value.StartsWith("data/", StringComparison.Ordinal))
        {
            return Invalid(path, "Path must be canonical relative slash form without traversal.");
        }

        return value.Any(character => char.IsControl(character))
            ? Invalid(path, "Path contains a control character.")
            : null;
    }

    private static BenchmarkContractError? NonNegative(long value, string path) =>
        Range(value, 0, ProtocolLimits.MaxCanonicalInteger, path);

    private static BenchmarkContractError? Positive(long value, string path) =>
        Range(value, 1, ProtocolLimits.MaxCanonicalInteger, path);

    private static BenchmarkContractError? OptionalNonNegative(long? value, string path) =>
        value is null ? null : NonNegative(value.Value, path);

    private static BenchmarkContractError? OptionalSafeInteger(long? value, string path) =>
        value is null
            ? null
            : Range(
                value.Value,
                ProtocolLimits.MinCanonicalInteger,
                ProtocolLimits.MaxCanonicalInteger,
                path);

    private static BenchmarkContractError? Range(
        long value,
        long minimum,
        long maximum,
        string path) =>
        value >= minimum && value <= maximum
            ? null
            : Invalid(path, $"Integer must be in [{minimum}, {maximum}].");

    private static BenchmarkContractError? ExactInteger(long value, long expected, string path) =>
        value == expected
            ? null
            : Invalid(path, $"Integer must equal {expected}.");

    private static BenchmarkContractError? Exact(
        string value,
        string expected,
        string path) =>
        string.Equals(value, expected, StringComparison.Ordinal)
            ? null
            : Invalid(path, $"Value must equal '{expected}'.");

    private static BenchmarkContractError? EnumString(
        string value,
        IEnumerable<string> allowed,
        string path) =>
        allowed.Contains(value, StringComparer.Ordinal)
            ? null
            : Invalid(path, "Value is outside the locked vocabulary.");

    private static BenchmarkContractError? SortedOpaqueSet(
        IReadOnlyList<string> values,
        string path,
        bool allowEmpty)
    {
        var collectionError = Sequence(values, path, allowEmpty);

        if (collectionError is not null)
        {
            return collectionError;
        }

        string? previous = null;

        for (var index = 0; index < values.Count; index++)
        {
            var error = Opaque(values[index], $"{path}[{index}]");

            if (error is not null)
            {
                return error;
            }

            if (previous is not null
                && StringComparer.Ordinal.Compare(previous, values[index]) >= 0)
            {
                return Invalid(path, "Set values must be unique and ordinal-sorted.");
            }

            previous = values[index];
        }

        return null;
    }

    private static BenchmarkContractError? SortedShaSet(
        IReadOnlyList<string> values,
        string path,
        bool allowEmpty)
    {
        var error = SortedUnique(values, item => item, path, allowEmpty);

        if (error is not null)
        {
            return error;
        }

        for (var index = 0; index < values.Count; index++)
        {
            error = Sha(values[index], $"{path}[{index}]");

            if (error is not null)
            {
                return error;
            }
        }

        return null;
    }

    private static BenchmarkContractError? SortedUnique<T>(
        IReadOnlyList<T> values,
        Func<T, string> key,
        string path,
        bool allowEmpty)
    {
        var collectionError = Sequence(values, path, allowEmpty);

        if (collectionError is not null)
        {
            return collectionError;
        }

        string? previous = null;

        for (var index = 0; index < values.Count; index++)
        {
            var current = key(values[index]);

            if (previous is not null
                && StringComparer.Ordinal.Compare(previous, current) >= 0)
            {
                return Invalid(path, "Set rows must be unique and ordinal-sorted by key.");
            }

            previous = current;
        }

        return null;
    }

    private static BenchmarkContractError? Sequence<T>(
        IReadOnlyList<T> values,
        string path,
        bool allowEmpty)
    {
        if (values is null)
        {
            return Missing(path);
        }

        return allowEmpty || values.Count > 0
            ? null
            : Invalid(path, "Array must not be empty.");
    }

    private static int CompareArc(
        (string From, string To) left,
        (string From, string To) right)
    {
        var from = StringComparer.Ordinal.Compare(left.From, right.From);
        return from != 0 ? from : StringComparer.Ordinal.Compare(left.To, right.To);
    }

    private static int CompareEventOrder(ScenarioEvent left, ScenarioEvent right)
    {
        var typeRank = EventTypeRank(left.EventType).CompareTo(EventTypeRank(right.EventType));

        if (typeRank != 0)
        {
            return typeRank;
        }

        var ordinal = left.SourceRecordOrdinal.CompareTo(right.SourceRecordOrdinal);
        return ordinal != 0
            ? ordinal
            : StringComparer.Ordinal.Compare(left.StableSubjectId, right.StableSubjectId);
    }

    private static int EventTypeRank(string eventType) => eventType switch
    {
        "travelTimesUpdated" => 10,
        "incidentResolved" => 20,
        "incidentOpened" => 30,
        "vehicleReachedStop" => 40,
        "passengerAlighted" => 50,
        "passengerBoarded" => 60,
        "vehicleAdvanced" => 70,
        "requestCancelled" or "bookingCancelled" => 80,
        "bookingConfirmed" or "offerDeclined" => 90,
        "requestArrived" => 100,
        "timerTick" => 110,
        _ => int.MaxValue,
    };

    private static IEnumerable<string> VehicleNodeIds(ScenarioVehicle vehicle)
    {
        switch (vehicle.Position)
        {
            case NodeScenarioPosition node:
                yield return node.NodeId;
                break;
            case EdgeProgressScenarioPosition edge:
                yield return edge.FromNodeId;
                yield return edge.ToNodeId;
                break;
        }

        foreach (var stop in vehicle.InitialRoute.FrozenPrefix.Concat(
            vehicle.InitialRoute.MutableSuffix))
        {
            yield return stop.NodeId;
        }
    }

    private static BenchmarkContractError? First(
        params BenchmarkContractError?[] errors) => errors.FirstOrDefault(error => error is not null);

    private static BenchmarkContractError Error(
        BenchmarkContractErrorCode code,
        string path,
        string message) => new(code, path, message);

    private static BenchmarkContractError Invalid(string path, string message) =>
        Error(BenchmarkContractErrorCode.InvalidValue, path, message);

    private static BenchmarkContractError Conditional(string path, string message) =>
        Error(BenchmarkContractErrorCode.ConditionalFieldViolation, path, message);

    private static BenchmarkContractError Missing(string path) =>
        Error(BenchmarkContractErrorCode.MissingRequiredField, path, "Required value is missing.");

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{0,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex ArtifactIdRegex();

    [GeneratedRegex("^(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)$", RegexOptions.CultureInvariant)]
    private static partial Regex ContractVersionRegex();

    [GeneratedRegex("^(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)$", RegexOptions.CultureInvariant)]
    private static partial Regex ReleaseVersionRegex();

    [GeneratedRegex("^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(\\.[0-9]{1,7})?Z$", RegexOptions.CultureInvariant)]
    private static partial Regex UtcRegex();
}
