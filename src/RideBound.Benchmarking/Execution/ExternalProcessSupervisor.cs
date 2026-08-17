using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using RideBound.Contracts.Serialization;

namespace RideBound.Benchmarking.Execution;

public static partial class ExternalProcessSupervisor
{
    public static async Task<ExternalProcessRunResult> RunAsync(
        ExternalProcessRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);
        var runsRoot = Path.GetFullPath(request.IsolatedRunsRoot);
        var runRoot = Path.Combine(runsRoot, request.RunId);
        var inputPath = Path.Combine(runRoot, "stdin.ndjson");
        var outputPath = Path.Combine(runRoot, "stdout.ndjson");
        var errorPath = Path.Combine(runRoot, "stderr.log");
        var resourceSamplesPath = Path.Combine(runRoot, "resource-samples.ndjson");
        var artifactPreflightPath = Path.Combine(runRoot, "artifact-preflight.json");
        var artifactPostflightPath = Path.Combine(runRoot, "artifact-postflight.json");
        var launchCommandSha256 = ProcessLaunchIdentity.Calculate(
            request.ExecutablePath,
            request.Arguments);

        if (Directory.Exists(runRoot) || File.Exists(runRoot))
        {
            return PreflightFailure(
                runRoot,
                inputPath,
                outputPath,
                errorPath,
                resourceSamplesPath,
                artifactPreflightPath,
                artifactPostflightPath,
                "artifact.mismatch",
                "Run isolation root already exists and was not reused.",
                launchCommandSha256: launchCommandSha256);
        }

        ProcessArtifactInventory preflightInventory;

        try
        {
            preflightInventory = ProcessArtifactIdentity.Capture(request.PinnedFiles);
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or CryptographicException
                or ArgumentException)
        {
            return PreflightFailure(
                runRoot,
                inputPath,
                outputPath,
                errorPath,
                resourceSamplesPath,
                artifactPreflightPath,
                artifactPostflightPath,
                "artifact.mismatch",
                exception.Message,
                launchCommandSha256: launchCommandSha256);
        }

        if (!string.Equals(
            preflightInventory.InventorySha256,
            request.ExpectedRuntimeInventorySha256,
            StringComparison.Ordinal))
        {
            return PreflightFailure(
                runRoot,
                inputPath,
                outputPath,
                errorPath,
                resourceSamplesPath,
                artifactPreflightPath,
                artifactPostflightPath,
                "artifact.mismatch",
                "Runtime inventory does not match the plan-bound identity.",
                preflightInventory.InventorySha256,
                launchCommandSha256);
        }

        Directory.CreateDirectory(runsRoot);
        Directory.CreateDirectory(runRoot);
        var tempRoot = Path.Combine(runRoot, "tmp");
        Directory.CreateDirectory(tempRoot);
        await WriteArtifactReceipt(
            artifactPreflightPath,
            "preflight",
            launchCommandSha256,
            preflightInventory);
        var startInfo = CreateStartInfo(request, runRoot, tempRoot);
        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

        try
        {
            if (!process.Start())
            {
                return PreflightFailure(
                    runRoot,
                    inputPath,
                    outputPath,
                    errorPath,
                    resourceSamplesPath,
                    artifactPreflightPath,
                    artifactPostflightPath,
                    "process.start-failed",
                    "External process did not start.",
                    preflightInventory.InventorySha256,
                    launchCommandSha256,
                    "execution");
            }
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
                or System.ComponentModel.Win32Exception
                or IOException)
        {
            return PreflightFailure(
                runRoot,
                inputPath,
                outputPath,
                errorPath,
                resourceSamplesPath,
                artifactPreflightPath,
                artifactPostflightPath,
                "process.start-failed",
                "External process could not be started.",
                preflightInventory.InventorySha256,
                launchCommandSha256,
                "execution");
        }

        var stopwatch = Stopwatch.StartNew();
        var samples = new List<ProcessResourceSample>();
        var cpuByProcess = new Dictionary<int, long>();
        var enforcementKind = OperatingSystem.IsWindows()
            ? "windows-toolhelp-sampled-process-tree-v1"
            : "root-process-sampling-v1";
        ExternalProcessFailure? failure = null;
        long peakWorkingSet = 0;
        long peakProcessCount = 0;
        long observedCpu = 0;
        peakProcessCount = 1;
        var inputCapture = new FileStream(inputPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        var outputCapture = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        var errorCapture = new FileStream(errorPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        var standardInput = new BoundedRecordingWriteStream(
            process.StandardInput.BaseStream,
            inputCapture,
            request.Limits.StandardInputLimitBytes);
        var standardOutput = new BoundedRecordingReadStream(
            process.StandardOutput.BaseStream,
            outputCapture,
            request.Limits.StandardOutputLimitBytes,
            "resource.stdout-bytes-exceeded");
        var standardError = new BoundedRecordingReadStream(
            process.StandardError.BaseStream,
            errorCapture,
            request.Limits.StandardErrorLimitBytes,
            "resource.stderr-bytes-exceeded");
        using var executionCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        var conversationTask = RunConversation(
            request.Conversation,
            standardInput,
            standardOutput,
            executionCancellation.Token);
        var stderrTask = DrainStandardError(standardError, executionCancellation.Token);

        try
        {
            while (!process.HasExited)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    failure = new ExternalProcessFailure(
                        "process.cancelled",
                        "execution",
                        "Run was cancelled by its caller.",
                        enforcementKind);
                    break;
                }

                var usage = ProcessTreeSnapshot.Observe(process.Id, cpuByProcess);
                observedCpu = Math.Max(observedCpu, usage.CpuTimeMs);
                peakWorkingSet = Math.Max(peakWorkingSet, usage.WorkingSetBytes);
                peakProcessCount = Math.Max(peakProcessCount, usage.ProcessCount);
                samples.Add(
                    new ProcessResourceSample(
                        stopwatch.ElapsedMilliseconds,
                        usage.CpuTimeMs,
                        usage.WorkingSetBytes,
                        usage.ProcessCount));
                failure = LimitFailure(
                    request.Limits,
                    stopwatch.ElapsedMilliseconds,
                    observedCpu,
                    peakWorkingSet,
                    peakProcessCount,
                    enforcementKind);

                if (failure is not null)
                {
                    break;
                }

                if (conversationTask.IsFaulted)
                {
                    failure = ConversationExceptionFailure(
                        conversationTask.Exception!.GetBaseException(),
                        enforcementKind);
                    break;
                }

                if (conversationTask.IsCompletedSuccessfully
                    && !conversationTask.Result.IsSuccess)
                {
                    failure = ConversationFailure(
                        conversationTask.Result,
                        enforcementKind);
                    break;
                }

                if (stderrTask.IsFaulted)
                {
                    failure = ConversationExceptionFailure(
                        stderrTask.Exception!.GetBaseException(),
                        enforcementKind);
                    break;
                }

                await Task.Delay(
                    TimeSpan.FromMilliseconds(request.Limits.SampleIntervalMs),
                    CancellationToken.None);
            }

            if (failure is not null && !process.HasExited)
            {
                KillProcessTree(process);
            }

            await process.WaitForExitAsync(CancellationToken.None);

            try
            {
                await stderrTask;
            }
            catch (OperationCanceledException)
            {
                // The process has exited; partial stderr is retained.
            }
            catch (Exception) when (failure is not null)
            {
                // The first terminal failure owns classification; partial evidence remains.
            }
            catch (Exception exception) when (failure is null)
            {
                failure = ConversationExceptionFailure(exception, enforcementKind);
            }

            ProcessConversationResult? conversation = null;

            try
            {
                conversation = await conversationTask;
            }
            catch (OperationCanceledException) when (failure is not null)
            {
                // A resource/caller failure owns the terminal classification.
            }
            catch (Exception) when (failure is not null)
            {
                // The first terminal failure owns classification; partial evidence remains.
            }
            catch (Exception exception) when (failure is null)
            {
                failure = ConversationExceptionFailure(exception, enforcementKind);
            }

            if (failure is null && conversation is { IsSuccess: false })
            {
                failure = ConversationFailure(conversation, enforcementKind);
            }

            if (failure is null && process.ExitCode != 0)
            {
                failure = new ExternalProcessFailure(
                    "process.crash",
                    "execution",
                    $"External process exited with code {process.ExitCode}.",
                    enforcementKind);
            }
        }
        finally
        {
            executionCancellation.Cancel();
            await DisposeQuietly(standardInput);
            await DisposeQuietly(standardOutput);
            await DisposeQuietly(standardError);
        }

        stopwatch.Stop();
        samples.Add(
            new ProcessResourceSample(
                stopwatch.ElapsedMilliseconds,
                observedCpu,
                peakWorkingSet,
                peakProcessCount));
        await WriteResourceSamples(resourceSamplesPath, samples);
        ProcessArtifactInventory? postflightInventory = null;
        string postflight;

        try
        {
            postflightInventory = ProcessArtifactIdentity.Observe(request.PinnedFiles);
            postflight = postflightInventory.InventorySha256;
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or CryptographicException
                or ArgumentException)
        {
            postflight = new string('0', 64);
            failure ??= new ExternalProcessFailure(
                "artifact.mismatch",
                "postflight",
                exception.Message,
                enforcementKind);
            await WriteArtifactFailureReceipt(
                artifactPostflightPath,
                launchCommandSha256);
        }

        if (postflightInventory is not null)
        {
            await WriteArtifactReceipt(
                artifactPostflightPath,
                "postflight",
                launchCommandSha256,
                postflightInventory);
        }

        if (failure is null
            && !string.Equals(
                preflightInventory.InventorySha256,
                postflight,
                StringComparison.Ordinal))
        {
            failure = new ExternalProcessFailure(
                "artifact.mismatch",
                "postflight",
                "Pinned runtime/configuration inventory changed during execution.",
                enforcementKind);
        }

        var status = failure?.Code == "process.cancelled"
            ? ExternalProcessTerminalStatus.Cancelled
            : failure is null
                ? ExternalProcessTerminalStatus.Succeeded
                : ExternalProcessTerminalStatus.Failed;
        return new ExternalProcessRunResult(
            status,
            process.ExitCode,
            runRoot,
            inputPath,
            outputPath,
            errorPath,
            resourceSamplesPath,
            artifactPreflightPath,
            artifactPostflightPath,
            standardInput.Bytes,
            standardOutput.Bytes,
            standardError.Bytes,
            stopwatch.ElapsedMilliseconds,
            observedCpu,
            peakWorkingSet,
            peakProcessCount,
            preflightInventory.InventorySha256,
            postflight,
            launchCommandSha256,
            samples,
            failure);
    }

    private static ProcessStartInfo CreateStartInfo(
        ExternalProcessRunRequest request,
        string runRoot,
        string tempRoot)
    {
        var startInfo = new ProcessStartInfo(request.ExecutablePath)
        {
            WorkingDirectory = runRoot,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        var systemRoot = Environment.GetEnvironmentVariable("SystemRoot");
        startInfo.Environment.Clear();

        if (!string.IsNullOrEmpty(systemRoot))
        {
            startInfo.Environment["SystemRoot"] = systemRoot;
            startInfo.Environment["WINDIR"] = systemRoot;
        }

        startInfo.Environment["TEMP"] = tempRoot;
        startInfo.Environment["TMP"] = tempRoot;
        startInfo.Environment["DOTNET_CLI_HOME"] = runRoot;
        startInfo.Environment["DOTNET_BUNDLE_EXTRACT_BASE_DIR"] =
            Path.Combine(runRoot, "bundle-extract");
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        startInfo.Environment["DOTNET_NOLOGO"] = "1";
        return startInfo;
    }

    private static async Task<ProcessConversationResult> RunConversation(
        IExternalProcessConversation conversation,
        Stream input,
        Stream output,
        CancellationToken cancellationToken)
    {
        try
        {
            return await conversation.ExecuteAsync(input, output, cancellationToken);
        }
        catch (StreamLimitExceededException)
        {
            throw;
        }
        catch (EndOfStreamException exception)
        {
            return ProcessConversationResult.Failed(
                "protocol.incomplete-output",
                exception.Message);
        }
    }

    private static async Task DrainStandardError(
        Stream standardError,
        CancellationToken cancellationToken)
    {
        await standardError.CopyToAsync(Stream.Null, cancellationToken);
    }

    private static ExternalProcessFailure? LimitFailure(
        ExternalProcessLimits limits,
        long wallTime,
        long cpuTime,
        long workingSet,
        long processCount,
        string enforcementKind)
    {
        if (wallTime > limits.WallTimeLimitMs)
        {
            return new ExternalProcessFailure(
                "resource.wall-time-exceeded",
                "execution",
                "Monotonic wall-time limit was exceeded.",
                enforcementKind);
        }

        if (cpuTime > limits.CpuTimeLimitMs)
        {
            return new ExternalProcessFailure(
                "resource.cpu-time-exceeded",
                "execution",
                "Observed process-tree CPU-time limit was exceeded.",
                enforcementKind);
        }

        if (workingSet > limits.PeakWorkingSetLimitBytes)
        {
            return new ExternalProcessFailure(
                "resource.memory-exceeded",
                "execution",
                "Observed process-tree working-set limit was exceeded.",
                enforcementKind);
        }

        return processCount > limits.ProcessCountLimit
            ? new ExternalProcessFailure(
                "resource.process-count-exceeded",
                "execution",
                "Observed process-tree count limit was exceeded.",
                enforcementKind)
            : null;
    }

    private static ExternalProcessFailure ConversationExceptionFailure(
        Exception exception,
        string enforcementKind)
    {
        return exception is StreamLimitExceededException limit
            ? new ExternalProcessFailure(
                limit.Code,
                "execution",
                limit.Message,
                enforcementKind)
            : new ExternalProcessFailure(
                "protocol.incomplete-output",
                "completion",
                exception.Message,
                enforcementKind);
    }

    private static ExternalProcessFailure ConversationFailure(
        ProcessConversationResult result,
        string enforcementKind)
    {
        var suppliedCode = result.FailureCode ?? "protocol.incomplete-output";
        var (code, stage, safeMessage) = suppliedCode switch
        {
            "capability.divergence" =>
                (suppliedCode, "negotiation", result.SafeMessage),
            "solver.unknown" =>
                (suppliedCode, "decision", result.SafeMessage),
            "protocol.invalid-output" =>
                (suppliedCode, "parsing", result.SafeMessage),
            "protocol.incomplete-output" =>
                (suppliedCode, "completion", result.SafeMessage),
            "state.divergence" =>
                (suppliedCode, "validation", result.SafeMessage),
            _ =>
                ("protocol.invalid-output", "parsing",
                    "External conversation returned an unsupported failure code."),
        };
        return new ExternalProcessFailure(
            code,
            stage,
            safeMessage ?? "External conversation did not complete.",
            enforcementKind);
    }

    private static void KillProcessTree(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // It already exited between observation and enforcement.
        }
    }

    private static async ValueTask DisposeQuietly(Stream stream)
    {
        try
        {
            await stream.DisposeAsync();
        }
        catch (IOException)
        {
            // Evidence already captured; terminal failure classification is retained.
        }
    }

    private static void ValidateRequest(ExternalProcessRunRequest request)
    {
        if (!RunIdPattern().IsMatch(request.RunId))
        {
            throw new ArgumentException("Run ID is not a safe artifact path segment.");
        }

        var repository = Path.GetFullPath(request.RepositoryRoot);
        var runsRoot = Path.GetFullPath(request.IsolatedRunsRoot);

        if (!Path.IsPathRooted(request.IsolatedRunsRoot)
            || IsUnder(repository, runsRoot))
        {
            throw new ArgumentException(
                "Isolated runs root must be absolute and outside the repository.");
        }

        if (!Path.IsPathRooted(request.RepositoryRoot)
            || !Path.IsPathRooted(request.ExecutablePath)
            || !File.Exists(request.ExecutablePath)
            || request.PinnedFiles.Count == 0
            || request.PinnedFiles.Select(value => value.Role)
                .Distinct(StringComparer.Ordinal).Count() != request.PinnedFiles.Count
            || request.Conversation is null)
        {
            throw new ArgumentException("Process launch or pinned inventory is incomplete.");
        }

        var executable = Path.GetFullPath(request.ExecutablePath);
        var pathComparison = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        var pinnedPaths = request.PinnedFiles
            .Select(value => Path.GetFullPath(value.FullPath))
            .ToArray();

        if (!pinnedPaths.Contains(executable, pathComparison)
            || pinnedPaths.Distinct(pathComparison).Count() != pinnedPaths.Length
            || request.PinnedFiles.Any(
                value => string.IsNullOrWhiteSpace(value.Role)
                    || !Path.IsPathRooted(value.FullPath)
                    || !LowerSha256Pattern().IsMatch(value.ExpectedSha256))
            || request.Arguments.Any(value => value is null)
            || !LowerSha256Pattern().IsMatch(request.ExpectedRuntimeInventorySha256))
        {
            throw new ArgumentException(
                "Executable, arguments, and every exact pinned artifact must be unambiguous.");
        }

        var limits = request.Limits;

        if (limits is null
            || limits.WallTimeLimitMs <= 0
            || limits.CpuTimeLimitMs <= 0
            || limits.PeakWorkingSetLimitBytes <= 0
            || limits.ProcessCountLimit <= 0
            || limits.StandardInputLimitBytes <= 0
            || limits.StandardOutputLimitBytes <= 0
            || limits.StandardErrorLimitBytes <= 0
            || limits.SampleIntervalMs is < 1 or > 1_000)
        {
            throw new ArgumentException("Process limits must be positive and bounded.");
        }
    }

    private static bool IsUnder(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        return relative == "."
            || relative != ".."
                && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && !Path.IsPathRooted(relative);
    }

    private static ExternalProcessRunResult PreflightFailure(
        string runRoot,
        string inputPath,
        string outputPath,
        string errorPath,
        string resourceSamplesPath,
        string artifactPreflightPath,
        string artifactPostflightPath,
        string code,
        string message,
        string? preflight = null,
        string? launchCommandSha256 = null,
        string stage = "preflight") =>
        new(
            ExternalProcessTerminalStatus.Failed,
            null,
            runRoot,
            inputPath,
            outputPath,
            errorPath,
            resourceSamplesPath,
            artifactPreflightPath,
            artifactPostflightPath,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            preflight ?? new string('0', 64),
            new string('0', 64),
            launchCommandSha256 ?? new string('0', 64),
            [],
            new ExternalProcessFailure(code, stage, message, "not-started"));

    private static async Task WriteResourceSamples(
        string path,
        IReadOnlyList<ProcessResourceSample> samples)
    {
        await using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read);
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        foreach (var sample in samples)
        {
            var bytes = CanonicalJson.Canonicalize(
                JsonSerializer.SerializeToUtf8Bytes(sample, options));
            await stream.WriteAsync(bytes);
            await stream.WriteAsync("\n"u8.ToArray());
        }

        await stream.FlushAsync();
    }

    private static async Task WriteArtifactReceipt(
        string path,
        string stage,
        string launchCommandSha256,
        ProcessArtifactInventory inventory)
    {
        var receipt = new
        {
            SchemaVersion = "1.0.0",
            Stage = stage,
            inventory.InventorySha256,
            LaunchCommandSha256 = launchCommandSha256,
            inventory.Artifacts,
        };
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
        var canonical = CanonicalJson.Canonicalize(
            JsonSerializer.SerializeToUtf8Bytes(receipt, options));
        await using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read);
        await stream.WriteAsync(canonical);
        await stream.FlushAsync();
    }

    private static async Task WriteArtifactFailureReceipt(
        string path,
        string launchCommandSha256)
    {
        var receipt = new
        {
            SchemaVersion = "1.0.0",
            Stage = "postflight",
            Status = "artifact-unavailable",
            InventorySha256 = new string('0', 64),
            LaunchCommandSha256 = launchCommandSha256,
            Artifacts = Array.Empty<ProcessArtifactInventoryEntry>(),
        };
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
        var canonical = CanonicalJson.Canonicalize(
            JsonSerializer.SerializeToUtf8Bytes(receipt, options));
        await using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read);
        await stream.WriteAsync(canonical);
        await stream.FlushAsync();
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{0,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex RunIdPattern();

    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex LowerSha256Pattern();
}
