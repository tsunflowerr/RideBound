using RideBound.Benchmarking.Contracts;

namespace RideBound.Benchmarking.Datasets;

public sealed record DatasetCacheOptions(
    string CacheRoot,
    string RepositoryRoot,
    long MaximumArchiveBytes = 600_000_000);

public sealed record DatasetAcquisitionRequest(
    DatasetSourceRegistration Registration,
    DatasetCacheOptions Cache,
    string AcceptedLicenseSpdx,
    string? ResumePartialPath = null);

public enum DatasetAcquisitionStatus
{
    Succeeded,
    Excluded,
    Failed,
}

public sealed record DatasetAcquisitionIssue(
    string Code,
    string Stage,
    string SafeMessage);

public sealed record VerifiedDatasetArtifact(
    string DatasetId,
    string FullPath,
    string ContentAddress,
    long LengthBytes,
    string Md5,
    string Sha256,
    DatasetDescriptor Descriptor,
    bool ReusedExistingBytes);

public sealed record DatasetAcquisitionResult(
    DatasetAcquisitionStatus Status,
    VerifiedDatasetArtifact? Artifact,
    DatasetAcquisitionIssue? Issue)
{
    public static DatasetAcquisitionResult Success(VerifiedDatasetArtifact artifact) =>
        new(DatasetAcquisitionStatus.Succeeded, artifact, null);

    public static DatasetAcquisitionResult Excluded(
        string code,
        string stage,
        string safeMessage) =>
        new(
            DatasetAcquisitionStatus.Excluded,
            null,
            new DatasetAcquisitionIssue(code, stage, safeMessage));

    public static DatasetAcquisitionResult Failed(
        string code,
        string stage,
        string safeMessage) =>
        new(
            DatasetAcquisitionStatus.Failed,
            null,
            new DatasetAcquisitionIssue(code, stage, safeMessage));
}
