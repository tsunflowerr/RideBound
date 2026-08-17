using RideBound.Benchmarking.Contracts;
using RideBound.Benchmarking.Execution;

namespace RideBound.Benchmarking.Storage;

public static class ExternalProcessTerminalMapper
{
    public static IReadOnlyList<RawRunEvidenceSource> PinRawEvidence(
        ExternalProcessRunResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        try
        {
            return
            [
                RawRunEvidenceSource.Pin(RawRunEvidenceRole.Input, result.StandardInputPath),
                RawRunEvidenceSource.Pin(RawRunEvidenceRole.Output, result.StandardOutputPath),
                RawRunEvidenceSource.Pin(
                    RawRunEvidenceRole.StandardError,
                    result.StandardErrorPath),
                RawRunEvidenceSource.Pin(
                    RawRunEvidenceRole.ResourceSamples,
                    result.ResourceSamplesPath),
                RawRunEvidenceSource.Pin(
                    RawRunEvidenceRole.ArtifactPreflight,
                    result.ArtifactPreflightPath),
                RawRunEvidenceSource.Pin(
                    RawRunEvidenceRole.ArtifactPostflight,
                    result.ArtifactPostflightPath),
            ];
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                "External result lacks the complete evidence set; terminalize the intent as persistence-incomplete.",
                exception);
        }
    }

    public static TerminalRunSubmission CreateSubmission(
        RunStoreIntent intent,
        ExternalProcessRunResult result,
        string startedAtUtc,
        string finishedAtUtc,
        IReadOnlyList<string> denominatorIds,
        string? lastEpochId = null,
        string? lastEventHash = null,
        string? lastDecisionHash = null,
        string? lastCheckpointHash = null)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(denominatorIds);
        var succeeded = result.Status == ExternalProcessTerminalStatus.Succeeded;
        return new TerminalRunSubmission(
            intent,
            succeeded ? RunTerminalStatus.Succeeded : RunTerminalStatus.Failed,
            startedAtUtc,
            finishedAtUtc,
            result.WallTimeMs,
            result.CpuTimeMs,
            result.PeakWorkingSetBytes,
            result.PeakProcessCount,
            result.ArtifactPreflightSha256,
            result.ArtifactPostflightSha256,
            PinRawEvidence(result),
            result.ExitCode,
            lastEpochId,
            lastEventHash,
            lastDecisionHash,
            lastCheckpointHash,
            succeeded ? null : MapFailure(result, denominatorIds));
    }

    private static RunFailureInput MapFailure(
        ExternalProcessRunResult result,
        IReadOnlyList<string> denominatorIds)
    {
        var failure = result.Failure
            ?? throw new ArgumentException("Failed external result lacks terminal failure.");
        var evidenceRole = failure.Code switch
        {
            "artifact.mismatch" when failure.Stage == "postflight" =>
                RawRunEvidenceRole.ArtifactPostflight,
            "artifact.mismatch" => RawRunEvidenceRole.ArtifactPreflight,
            "resource.stdout-bytes-exceeded" => RawRunEvidenceRole.Output,
            "resource.stderr-bytes-exceeded" or "process.crash" =>
                RawRunEvidenceRole.StandardError,
            "input.invalid" or "resource.stdin-bytes-exceeded" =>
                RawRunEvidenceRole.Input,
            "capability.divergence" or "solver.unknown" or "state.divergence"
                or "protocol.invalid-output" or "protocol.incomplete-output" =>
                RawRunEvidenceRole.Output,
            _ => RawRunEvidenceRole.ResourceSamples,
        };
        return new RunFailureInput(
            failure.Code,
            failure.Stage,
            result.WallTimeMs,
            "external-process-supervisor",
            evidenceRole,
            failure.SafeMessage,
            denominatorIds);
    }
}
