using RideBound.Benchmarking.Contracts;
using RideBound.Benchmarking.Datasets;

namespace RideBound.Benchmarking.Normalization;

public sealed record FleetPyNormalizationConfiguration(
    string ScenarioId,
    string DemandMemberPath,
    string NodeMemberPath,
    string EdgeMemberPath,
    string TravelFactorMemberPath,
    string SourceLocalDate,
    string SourceTimezoneId,
    long SourceWindowStartSeconds,
    long SourceWindowEndSeconds,
    long TravelFactorAtSeconds,
    long RequestTarget,
    long VehicleCount,
    long MaximumNodeCount,
    long VehicleCapacity,
    long PickupWindowMs,
    long MaximumRideTimePermille,
    long DrainDurationMs,
    string SelectionKeyHex,
    string PseudonymizationKeyHex,
    string CommitmentPolicyId,
    string NormalizerSourceSha256);

public sealed record FleetPyNormalizationRequest(
    VerifiedDatasetArtifact Artifact,
    ArchiveExtractionResult Extraction,
    FleetPyNormalizationConfiguration Configuration);

public enum FleetPyNormalizationStatus
{
    Succeeded,
    Failed,
}

public sealed record FleetPyNormalizationIssue(
    string Code,
    string Stage,
    string SafeMessage,
    long? SourceRecordOrdinal = null,
    string? Field = null);

public sealed record NormalizationExclusion(
    long SourceRecordOrdinal,
    string Code,
    string Field,
    string SafeDetail);

public sealed record NormalizationDisposition(
    long SourceRecordOrdinal,
    string Disposition,
    string SelectionRankSha256);

public sealed record PublicDerivativeManifest(
    string SchemaVersion,
    string ScenarioId,
    string DatasetId,
    string PersistentUri,
    string LicenseSpdx,
    string LicenseUri,
    string Citation,
    string SourceArtifactSha256,
    string SourceMemberInventorySha256,
    string SourceSelectionSha256,
    string NormalizerId,
    string NormalizerVersion,
    string NormalizerSourceSha256,
    string ConfigurationSha256,
    string ScenarioContentSha256,
    string ScenarioHash,
    string NormalizationReportHash,
    string SelectionFrameSha256,
    string ExclusionLogSha256,
    string PolicyObservationClass,
    IReadOnlyList<string> TransformationSteps,
    IReadOnlyList<string> ForbiddenClaims);

public sealed record FleetPyNormalizationArtifact(
    ScenarioContent Scenario,
    byte[] ScenarioCanonicalBytes,
    string ScenarioContentSha256,
    string ScenarioHash,
    NormalizationReport Report,
    byte[] ReportCanonicalBytes,
    string ReportHash,
    IReadOnlyList<NormalizationDisposition> Dispositions,
    byte[] DispositionsCanonicalBytes,
    IReadOnlyList<NormalizationExclusion> Exclusions,
    byte[] ExclusionsCanonicalBytes,
    byte[] ConfigurationCanonicalBytes,
    PublicDerivativeManifest DerivativeManifest,
    byte[] DerivativeManifestCanonicalBytes);

public sealed record FleetPyNormalizationResult(
    FleetPyNormalizationStatus Status,
    FleetPyNormalizationArtifact? Artifact,
    FleetPyNormalizationIssue? Issue)
{
    public static FleetPyNormalizationResult Success(
        FleetPyNormalizationArtifact artifact) =>
        new(FleetPyNormalizationStatus.Succeeded, artifact, null);

    public static FleetPyNormalizationResult Failed(
        string code,
        string stage,
        string safeMessage,
        long? sourceRecordOrdinal = null,
        string? field = null) =>
        new(
            FleetPyNormalizationStatus.Failed,
            null,
            new FleetPyNormalizationIssue(
                code,
                stage,
                safeMessage,
                sourceRecordOrdinal,
                field));
}
