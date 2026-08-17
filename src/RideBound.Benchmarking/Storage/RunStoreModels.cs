using System.Security.Cryptography;
using RideBound.Benchmarking.Contracts;

namespace RideBound.Benchmarking.Storage;

public enum RawRunEvidenceRole
{
    Input,
    Output,
    StandardError,
    ResourceSamples,
    ArtifactPreflight,
    ArtifactPostflight,
}

public sealed record RawRunEvidenceSource(
    RawRunEvidenceRole Role,
    string FullPath,
    long ExpectedLengthBytes,
    string ExpectedSha256)
{
    public static RawRunEvidenceSource Pin(RawRunEvidenceRole role, string fullPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        var path = Path.GetFullPath(fullPath);
        var info = new FileInfo(path);

        if (!info.Exists || (info.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("Raw run evidence is missing or is a reparse point.");
        }

        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81_920,
            FileOptions.SequentialScan);
        var length = stream.Length;
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(stream));

        if (stream.Position != length || stream.Length != length)
        {
            throw new IOException("Raw run evidence changed while its identity was pinned.");
        }

        return new RawRunEvidenceSource(
            role,
            path,
            length,
            sha256);
    }
}

public sealed record RunStoreIntent(
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
    string ExpectedArtifactInventorySha256);

public sealed record RunFailureInput(
    string Code,
    string Stage,
    long FirstObservedMonotonicOffsetMs,
    string SourceComponent,
    RawRunEvidenceRole EvidenceRole,
    string SafeMessage,
    IReadOnlyList<string> AffectedDenominatorIds);

public sealed record RunExclusionInput(
    string RuleId,
    string RuleVersion,
    string RuleSetHash,
    string Stage,
    string SubjectKind,
    string SubjectId,
    bool BeforeOutcome,
    RawRunEvidenceRole EvidenceRole,
    IReadOnlyList<string> RetainedDenominatorIds,
    string SafeReason);

public sealed record TerminalRunSubmission(
    RunStoreIntent Intent,
    RunTerminalStatus TerminalStatus,
    string StartedAtUtc,
    string FinishedAtUtc,
    long WallTimeMs,
    long CpuTimeMs,
    long PeakWorkingSetBytes,
    long SpawnedProcessCount,
    string ArtifactPreflightSha256,
    string ArtifactPostflightSha256,
    IReadOnlyList<RawRunEvidenceSource> EvidenceSources,
    long? ExitCode = null,
    string? LastEpochId = null,
    string? LastEventHash = null,
    string? LastDecisionHash = null,
    string? LastCheckpointHash = null,
    RunFailureInput? Failure = null,
    RunExclusionInput? Exclusion = null);

public enum RunStoreWriteBoundary
{
    IntentValidated,
    EvidenceCopied,
    ObservationIndexWritten,
    TerminalDetailWritten,
    RunRecordWritten,
    LogSegmentCommitted,
    RunDirectoryCommitted,
}

public interface IRunStoreFaultInjector
{
    void OnBoundary(
        RunStoreWriteBoundary boundary,
        string runId,
        RawRunEvidenceRole? evidenceRole = null);
}

public sealed record RunStoreCommitResult(
    RunRecord RunRecord,
    string RunDirectory,
    bool ReusedExistingTerminal);

public sealed record RunStoreVerificationIssue(
    string Code,
    string RelativePath,
    string SafeMessage);

public sealed record RunStoreVerificationResult(
    long PlannedCount,
    long SucceededCount,
    long FailedCount,
    long ExcludedCount,
    long PendingCount,
    IReadOnlyList<RunStoreVerificationIssue> Issues)
{
    public bool IsValid => Issues.Count == 0
        && PlannedCount == SucceededCount + FailedCount + ExcludedCount
        && PendingCount == 0;
}
