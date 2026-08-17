namespace RideBound.Benchmarking.Execution;

public sealed record PinnedProcessFile(
    string Role,
    string FullPath,
    string ExpectedSha256);

public sealed record ExternalProcessLimits(
    long WallTimeLimitMs,
    long CpuTimeLimitMs,
    long PeakWorkingSetLimitBytes,
    long ProcessCountLimit,
    long StandardInputLimitBytes,
    long StandardOutputLimitBytes,
    long StandardErrorLimitBytes,
    long SampleIntervalMs = 25);

public sealed record ExternalProcessRunRequest(
    string RunId,
    string RepositoryRoot,
    string IsolatedRunsRoot,
    string ExecutablePath,
    IReadOnlyList<string> Arguments,
    IReadOnlyList<PinnedProcessFile> PinnedFiles,
    string ExpectedRuntimeInventorySha256,
    ExternalProcessLimits Limits,
    IExternalProcessConversation Conversation);

public sealed record ProcessResourceSample(
    long ElapsedMs,
    long ObservedCpuTimeMs,
    long ObservedWorkingSetBytes,
    long ObservedProcessCount);

public sealed record ProcessConversationResult(
    bool IsSuccess,
    string? FailureCode = null,
    string? SafeMessage = null)
{
    public static ProcessConversationResult Success() => new(true);

    public static ProcessConversationResult Failed(string code, string message) =>
        new(false, code, message);
}

public interface IExternalProcessConversation
{
    Task<ProcessConversationResult> ExecuteAsync(
        Stream standardInput,
        Stream standardOutput,
        CancellationToken cancellationToken);
}

public enum ExternalProcessTerminalStatus
{
    Succeeded,
    Failed,
    Cancelled,
}

public sealed record ExternalProcessFailure(
    string Code,
    string Stage,
    string SafeMessage,
    string EnforcementKind);

public sealed record ExternalProcessRunResult(
    ExternalProcessTerminalStatus Status,
    int? ExitCode,
    string RunRoot,
    string StandardInputPath,
    string StandardOutputPath,
    string StandardErrorPath,
    string ResourceSamplesPath,
    string ArtifactPreflightPath,
    string ArtifactPostflightPath,
    long StandardInputBytes,
    long StandardOutputBytes,
    long StandardErrorBytes,
    long WallTimeMs,
    long CpuTimeMs,
    long PeakWorkingSetBytes,
    long PeakProcessCount,
    string ArtifactPreflightSha256,
    string ArtifactPostflightSha256,
    string LaunchCommandSha256,
    IReadOnlyList<ProcessResourceSample> ResourceSamples,
    ExternalProcessFailure? Failure);

public sealed class RawInputConversation(byte[] input) : IExternalProcessConversation
{
    public async Task<ProcessConversationResult> ExecuteAsync(
        Stream standardInput,
        Stream standardOutput,
        CancellationToken cancellationToken)
    {
        await standardInput.WriteAsync(input, cancellationToken);
        await standardInput.FlushAsync(cancellationToken);
        standardInput.Close();
        await standardOutput.CopyToAsync(Stream.Null, cancellationToken);
        return ProcessConversationResult.Success();
    }
}
