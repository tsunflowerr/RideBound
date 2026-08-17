using System.Text.Json.Serialization;

namespace RideBound.Benchmarking.Contracts;

public static class BenchmarkContractVersions
{
    public const string V1 = "1.0.0";
    public const string V1_0_1 = "1.0.1";
    public const string V1_0_2 = "1.0.2";
}

public interface IBenchmarkDocument
{
    string SchemaVersion { get; }
}

public enum DatasetKind
{
    [JsonStringEnumMemberName("public")]
    Public,
    [JsonStringEnumMemberName("synthetic")]
    Synthetic,
    [JsonStringEnumMemberName("restrictedReal")]
    RestrictedReal,
}

public enum DirectIdentifierStatus
{
    [JsonStringEnumMemberName("noneObserved")]
    NoneObserved,
    [JsonStringEnumMemberName("removedBySource")]
    RemovedBySource,
    [JsonStringEnumMemberName("presentRestricted")]
    PresentRestricted,
}

public enum LocationPrecisionClass
{
    [JsonStringEnumMemberName("roadNode")]
    RoadNode,
    [JsonStringEnumMemberName("zone")]
    Zone,
    [JsonStringEnumMemberName("coordinateE7")]
    CoordinateE7,
    [JsonStringEnumMemberName("synthetic")]
    Synthetic,
}

public enum RetentionClass
{
    [JsonStringEnumMemberName("localRawCache")]
    LocalRawCache,
    [JsonStringEnumMemberName("redistributableDerivative")]
    RedistributableDerivative,
    [JsonStringEnumMemberName("restrictedNoRedistribution")]
    RestrictedNoRedistribution,
}

public sealed record DatasetDescriptor(
    string SchemaVersion,
    string DatasetId,
    DatasetKind DatasetKind,
    string Title,
    string ReleaseVersion,
    string PersistentUri,
    string DownloadUri,
    string RetrievedAtUtc,
    string PublisherArtifactName,
    string LicenseSpdx,
    string LicenseUri,
    string Citation,
    string Composition,
    string CollectionLimit,
    IReadOnlyList<string> AllowedUse,
    IReadOnlyList<string> ForbiddenClaim,
    DirectIdentifierStatus DirectIdentifierStatus,
    LocationPrecisionClass LocationPrecisionClass,
    RetentionClass RetentionClass,
    string MaintenanceNote,
    long? PublisherArtifactLengthBytes = null,
    string? PublisherMd5 = null,
    string? SourceArtifactSha256 = null) : IBenchmarkDocument;

public sealed record NormalizationReport(
    string SchemaVersion,
    string ReportId,
    string DatasetId,
    string SourceArtifactSha256,
    string SourceMemberInventorySha256,
    string NormalizerId,
    string NormalizerVersion,
    string NormalizerSourceSha256,
    string ConfigurationSha256,
    long InputRecordCount,
    long EligibleRecordCount,
    long SelectedRecordCount,
    long ExcludedRecordCount,
    string SelectionFrameSha256,
    string ExclusionLogSha256,
    string RoundingRuleId,
    string EventOrderingId,
    string SelectionRuleId,
    string ScenarioContentSha256,
    string ScenarioHash) : IBenchmarkDocument;

public enum ScenarioKind
{
    [JsonStringEnumMemberName("protocolFixture")]
    ProtocolFixture,
    [JsonStringEnumMemberName("publicDerivative")]
    PublicDerivative,
    [JsonStringEnumMemberName("syntheticStress")]
    SyntheticStress,
}

public enum EvidenceClass
{
    [JsonStringEnumMemberName("mechanical")]
    Mechanical,
    [JsonStringEnumMemberName("development")]
    Development,
    [JsonStringEnumMemberName("pilot")]
    Pilot,
    [JsonStringEnumMemberName("confirmatory")]
    Confirmatory,
}

public sealed record ScenarioTimeWindow(
    string SourceTimezoneId,
    string SourceWindowStartUtc,
    string SourceWindowEndUtc,
    long WarmupStartMs,
    long ScoreStartMs,
    long HorizonEndMs,
    long DrainEndMs,
    string BatchingId);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(NodeScenarioPosition), "node")]
[JsonDerivedType(typeof(EdgeProgressScenarioPosition), "edgeProgress")]
public abstract record ScenarioPosition;

public sealed record NodeScenarioPosition(string NodeId) : ScenarioPosition;

public sealed record EdgeProgressScenarioPosition(
    string FromNodeId,
    string ToNodeId,
    string EdgeId,
    long ProgressPermille) : ScenarioPosition;

public enum ScenarioRouteStopKind
{
    [JsonStringEnumMemberName("waypoint")]
    Waypoint,
    [JsonStringEnumMemberName("pickup")]
    Pickup,
    [JsonStringEnumMemberName("dropOff")]
    DropOff,
}

public sealed record ScenarioRouteStop(
    string StopId,
    string NodeId,
    ScenarioRouteStopKind Kind,
    long ServiceDurationMs,
    string? RequestId = null);

public sealed record ScenarioRoute(
    long PlanVersion,
    long ExecutedStopCount,
    IReadOnlyList<ScenarioRouteStop> FrozenPrefix,
    IReadOnlyList<ScenarioRouteStop> MutableSuffix);

public sealed record ScenarioVehicle(
    string VehicleId,
    long Capacity,
    long OccupiedSeats,
    ScenarioPosition Position,
    IReadOnlyList<string> OnboardRequestIds,
    IReadOnlyList<string> AcceptedRequestIds,
    ScenarioRoute InitialRoute,
    string SourceProvenanceId);

public sealed record ScenarioRequest(
    string RequestId,
    long SourceRecordOrdinal,
    long ArrivalTimeMs,
    string OriginNodeId,
    string DestinationNodeId,
    long EarliestPickupMs,
    long LatestPickupMs,
    long MaxRideTimeMs,
    long PartySize,
    string ServiceClass,
    string CommitmentPolicyId,
    string PolicyObservationClass,
    string SourceProvenanceId);

public sealed record ScenarioTravelArc(
    string FromNodeId,
    string ToNodeId,
    long TravelTimeMs);

public sealed record ScenarioTravelSnapshot(
    long Version,
    string SnapshotHash,
    IReadOnlyList<ScenarioTravelArc> Arcs);

public sealed record ScenarioEvent(
    long EventSequence,
    long SimTimeMs,
    string EventType,
    long SourceRecordOrdinal,
    string StableSubjectId,
    bool SourceSequencePreserved,
    string PayloadCanonicalJsonHex,
    string PayloadSha256,
    string SourceProvenanceId);

public sealed record ScenarioValidationSummary(
    long VehicleCount,
    long RequestCount,
    long NodeCount,
    long DirectedArcCount,
    long SnapshotCount,
    long EventCount,
    long ExcludedSourceRowCount,
    long SelectedSourceRowCount,
    long DuplicateIdCount,
    long UnreachableSelectedRowCount,
    long InvalidTimeRowCount,
    long OverflowRowCount,
    string InvariantSetHash);

public sealed record ScenarioContent(
    string SchemaVersion,
    string ScenarioId,
    ScenarioKind ScenarioKind,
    EvidenceClass EvidenceClass,
    string DatasetId,
    string SourceArtifactSha256,
    string SourceSelectionSha256,
    string NormalizerId,
    string NormalizerVersion,
    string NormalizerSourceSha256,
    string NormalizerConfigurationSha256,
    string EventOrderingId,
    string DriverSemanticsId,
    ScenarioTimeWindow TimeWindow,
    IReadOnlyList<ScenarioVehicle> Fleet,
    IReadOnlyList<ScenarioRequest> Requests,
    IReadOnlyList<ScenarioTravelSnapshot> TravelSnapshots,
    IReadOnlyList<ScenarioEvent> Events,
    ScenarioValidationSummary ValidationSummary) : IBenchmarkDocument;

public sealed record RunnerArtifactIdentity(
    string RunnerExecutableSha256,
    string RunnerAssemblySha256,
    string ContractsAssemblySha256,
    string RuntimeInventorySha256,
    string LaunchContractId);

public sealed record BenchmarkArm(
    string ArmId,
    string PolicyId,
    string PolicyVersion,
    string PolicyConfigurationSha256,
    string EffectiveConfigurationSha256,
    string CandidateGeneratorId,
    long CandidateWorkBudget,
    string ValidatorVersion,
    string SolverId,
    string SolverVersion,
    long SolverWorkBudget,
    string CapabilitySelectionSha256,
    string PairingClassId);

public sealed record BenchmarkPlan(
    string SchemaVersion,
    string PlanId,
    EvidenceClass EvidenceClass,
    string ClaimProfileId,
    IReadOnlyList<string> ScenarioHashes,
    IReadOnlyList<BenchmarkArm> Arms,
    string PairingClassId,
    string MasterSeedHex,
    long WarmupRunCount,
    long MeasuredRepeatCount,
    string RunOrderId,
    string ResourceProfileId,
    string FailureRuleSetId,
    string ExclusionRuleSetId,
    string MetricRegistryHash,
    RunnerArtifactIdentity RunnerArtifact,
    string HarnessSourceSha256,
    string OracleSourceSha256) : IBenchmarkDocument;

public enum RunTerminalStatus
{
    [JsonStringEnumMemberName("succeeded")]
    Succeeded,
    [JsonStringEnumMemberName("failed")]
    Failed,
    [JsonStringEnumMemberName("excluded")]
    Excluded,
}

public sealed record RunFileEvidence(
    string RelativePath,
    long LengthBytes,
    string Sha256);

public sealed record RunRecord(
    string SchemaVersion,
    string RunId,
    string PlanHash,
    string ScenarioHash,
    string ArmId,
    long RepeatIndex,
    long AttemptIndex,
    string PolicyConfigurationSha256,
    string EffectiveConfigurationSha256,
    string ComponentSeedHex,
    string RunnerArtifactSha256,
    string HarnessSourceSha256,
    long ExecutionOrdinal,
    bool Warmup,
    RunTerminalStatus TerminalStatus,
    string StartedAtUtc,
    string FinishedAtUtc,
    long WallTimeMs,
    long CpuTimeMs,
    long PeakWorkingSetBytes,
    long SpawnedProcessCount,
    string ArtifactPreflightSha256,
    string ArtifactPostflightSha256,
    RunFileEvidence InputFile,
    RunFileEvidence OutputFile,
    RunFileEvidence StderrFile,
    RunFileEvidence ResourceSamplesFile,
    RunFileEvidence ObservationIndexFile,
    long? ExitCode = null,
    string? LastEpochId = null,
    string? LastEventHash = null,
    string? LastDecisionHash = null,
    string? LastCheckpointHash = null,
    string? FailureRecordId = null,
    string? ExclusionRecordId = null) : IBenchmarkDocument;

public enum ObservationRecordKind
{
    [JsonStringEnumMemberName("inputEvent")]
    InputEvent,
    [JsonStringEnumMemberName("outputDecision")]
    OutputDecision,
    [JsonStringEnumMemberName("decisionAck")]
    DecisionAck,
    [JsonStringEnumMemberName("checkpoint")]
    Checkpoint,
    [JsonStringEnumMemberName("runTerminal")]
    RunTerminal,
}

public enum TranscriptRole
{
    [JsonStringEnumMemberName("input")]
    Input,
    [JsonStringEnumMemberName("output")]
    Output,
}

public sealed record ObservationIndexRow(
    string SchemaVersion,
    long RecordSequence,
    ObservationRecordKind RecordKind,
    string RunId,
    string ScenarioHash,
    string ArmId,
    long RepeatIndex,
    long AttemptIndex,
    TranscriptRole TranscriptRole,
    long LineNumber,
    string EnvelopeSha256,
    IReadOnlyList<string> RequestIds,
    IReadOnlyList<string> VehicleIds,
    long? EpochId = null,
    long? SimTimeMs = null,
    long? EventSequence = null,
    string? DecisionHash = null,
    string? CertificateHash = null) : IBenchmarkDocument;

public sealed record FailureRecord(
    string SchemaVersion,
    long RecordSequence,
    string FailureRecordId,
    string RunId,
    string PlanHash,
    string ScenarioHash,
    string ArmId,
    long RepeatIndex,
    long AttemptIndex,
    string Code,
    string Stage,
    long FirstObservedMonotonicOffsetMs,
    string SourceComponent,
    string EvidenceRelativePath,
    string EvidenceSha256,
    string SafeMessage,
    string RetryAuthorization,
    IReadOnlyList<string> AffectedDenominatorIds) : IBenchmarkDocument;

public sealed record ExclusionRecord(
    string SchemaVersion,
    long RecordSequence,
    string ExclusionRecordId,
    string RuleId,
    string RuleVersion,
    string RuleSetHash,
    string Stage,
    string SubjectKind,
    string SubjectId,
    bool BeforeOutcome,
    string EvidenceRelativePath,
    string EvidenceSha256,
    IReadOnlyList<string> RetainedDenominatorIds,
    string SafeReason,
    string? ScenarioHash = null,
    string? ArmId = null,
    long? RepeatIndex = null) : IBenchmarkDocument;

public enum MetricScopeKind
{
    [JsonStringEnumMemberName("run")]
    Run,
    [JsonStringEnumMemberName("epoch")]
    Epoch,
    [JsonStringEnumMemberName("request")]
    Request,
    [JsonStringEnumMemberName("vehicle")]
    Vehicle,
}

public enum MetricWindowId
{
    [JsonStringEnumMemberName("all")]
    All,
    [JsonStringEnumMemberName("warmup")]
    Warmup,
    [JsonStringEnumMemberName("scoring")]
    Scoring,
    [JsonStringEnumMemberName("drain")]
    Drain,
}

public enum MetricValueStatus
{
    [JsonStringEnumMemberName("observed")]
    Observed,
    [JsonStringEnumMemberName("missing")]
    Missing,
    [JsonStringEnumMemberName("notApplicable")]
    NotApplicable,
}

public sealed record MetricRow(
    string SchemaVersion,
    string MetricRegistryHash,
    string MetricId,
    string MetricVersion,
    string RunId,
    string ScenarioHash,
    string ArmId,
    long RepeatIndex,
    long AttemptIndex,
    MetricScopeKind ScopeKind,
    string ScopeId,
    MetricWindowId WindowId,
    MetricValueStatus ValueStatus,
    string UnitId,
    string RawEvidenceSha256,
    string CalculatorSourceSha256,
    long? ValueInteger = null,
    long? NumeratorInteger = null,
    string? DenominatorId = null,
    long? DenominatorInteger = null,
    string? MissingReasonId = null) : IBenchmarkDocument;

public enum BundleArtifactRole
{
    [JsonStringEnumMemberName("plan")]
    Plan,
    [JsonStringEnumMemberName("dataset")]
    Dataset,
    [JsonStringEnumMemberName("scenario")]
    Scenario,
    [JsonStringEnumMemberName("runEvidence")]
    RunEvidence,
    [JsonStringEnumMemberName("failureLog")]
    FailureLog,
    [JsonStringEnumMemberName("exclusionLog")]
    ExclusionLog,
    [JsonStringEnumMemberName("metric")]
    Metric,
    [JsonStringEnumMemberName("provenance")]
    Provenance,
    [JsonStringEnumMemberName("sourceInventory")]
    SourceInventory,
    [JsonStringEnumMemberName("claimCheck")]
    ClaimCheck,
    [JsonStringEnumMemberName("verificationReport")]
    VerificationReport,
}

public sealed record BundleArtifact(
    string RelativePath,
    long LengthBytes,
    string Sha256,
    string MediaType,
    BundleArtifactRole Role,
    string ProducerActivityId,
    IReadOnlyList<string> SourceEntityIds);

public sealed record LogicalBundleManifest(
    string SchemaVersion,
    string BundleId,
    EvidenceClass EvidenceClass,
    string ClaimProfileId,
    string PlanHash,
    string MetricSetHash,
    string SourceInventorySha256,
    string RuntimeInventorySha256,
    IReadOnlyList<BundleArtifact> Artifacts) : IBenchmarkDocument;
