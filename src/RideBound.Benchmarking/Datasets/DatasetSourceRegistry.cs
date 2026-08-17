using RideBound.Benchmarking.Contracts;

namespace RideBound.Benchmarking.Datasets;

public sealed record DatasetSourceRegistration(
    string DatasetId,
    DatasetKind DatasetKind,
    string Title,
    string ReleaseVersion,
    Uri PersistentUri,
    Uri DownloadUri,
    string PublisherArtifactName,
    long? PublisherArtifactLengthBytes,
    string? PublisherMd5,
    string? SourceArtifactSha256,
    string LicenseSpdx,
    Uri LicenseUri,
    string Citation,
    string Composition,
    string CollectionLimit,
    IReadOnlyList<string> AllowedUse,
    IReadOnlyList<string> ForbiddenClaim,
    DirectIdentifierStatus DirectIdentifierStatus,
    LocationPrecisionClass LocationPrecisionClass,
    RetentionClass RetentionClass,
    string MaintenanceNote,
    IReadOnlySet<string> AllowedDownloadHosts);

public static class DatasetSourceRegistry
{
    public static DatasetSourceRegistration FleetPyManhattanV1 { get; } =
        new(
            DatasetId: "fleetpy-manhattan-zenodo-15187906-v1",
            DatasetKind: DatasetKind.Public,
            Title: "FleetPy: Input Data for Manhattan Case Study",
            ReleaseVersion: "1.0",
            PersistentUri: new Uri("https://doi.org/10.5281/zenodo.15187906"),
            DownloadUri: new Uri(
                "https://zenodo.org/records/15187906/files/FleetPy_Manhattan.zip?download=1"),
            PublisherArtifactName: "FleetPy_Manhattan.zip",
            PublisherArtifactLengthBytes: 408_878_341,
            PublisherMd5: "8b11882ae9c6d87f666bf6e006806744",
            SourceArtifactSha256:
                "d9e86f33645e5eec287d387f8d63ad41ddf41d4ef648138b65d636482e2c599e",
            LicenseSpdx: "CC-BY-4.0",
            LicenseUri: new Uri("https://creativecommons.org/licenses/by/4.0/"),
            Citation:
                "Engelhardt, R. & Dandl, F. (2025). FleetPy: Input Data for " +
                "Manhattan Case Study (Version 1.0) [Dataset]. Zenodo. " +
                "https://doi.org/10.5281/zenodo.15187906",
            Composition:
                "NYC TLC trips from 2018-11-11 through 2018-11-18 filtered to " +
                "Manhattan O/D, an OSM Manhattan network, travel matrices/factors " +
                "and zone derivatives for FleetPy.",
            CollectionLimit:
                "Taxi trips are a selected service population from one week in 2018; " +
                "the release has no observed RideBound commitment preference or " +
                "user-satisfaction label.",
            AllowedUse: ["mechanicalBenchmark", "normalizerDevelopment"],
            ForbiddenClaim: ["observedCommitmentPreference", "userSatisfaction"],
            DirectIdentifierStatus: DirectIdentifierStatus.RemovedBySource,
            LocationPrecisionClass: LocationPrecisionClass.RoadNode,
            RetentionClass: RetentionClass.LocalRawCache,
            MaintenanceNote:
                "Immutable Zenodo record version 1.0. Registry amendment is required " +
                "for a different record, file or checksum.",
            AllowedDownloadHosts: new HashSet<string>(
                ["zenodo.org"],
                StringComparer.OrdinalIgnoreCase));

    public static IReadOnlyList<DatasetSourceRegistration> All { get; } =
        [FleetPyManhattanV1];

    public static DatasetSourceRegistration GetRequired(string datasetId) =>
        All.Single(
            registration => string.Equals(
                registration.DatasetId,
                datasetId,
                StringComparison.Ordinal));
}
