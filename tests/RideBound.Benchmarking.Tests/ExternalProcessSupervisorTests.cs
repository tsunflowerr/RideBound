using System.Diagnostics;
using System.Reflection;
using System.Text;
using RideBound.Benchmarking.Execution;

namespace RideBound.Benchmarking.Tests;

public sealed class ExternalProcessSupervisorTests
{
    private static readonly ExternalProcessLimits DefaultLimits = new(
        WallTimeLimitMs: 10_000,
        CpuTimeLimitMs: 10_000,
        PeakWorkingSetLimitBytes: 1_073_741_824,
        ProcessCountLimit: 2,
        StandardInputLimitBytes: 1_048_576,
        StandardOutputLimitBytes: 1_048_576,
        StandardErrorLimitBytes: 1_048_576,
        SampleIntervalMs: 10);

    [Fact]
    public async Task Clean_child_preserves_exact_transcripts_samples_and_launch_identity()
    {
        using var temp = new TestDirectory();
        var request = FakeRequest(temp, "clean", ["consume"], new RawInputConversation("abc"u8.ToArray()));

        var result = await ExternalProcessSupervisor.RunAsync(request);

        Assert.Equal(ExternalProcessTerminalStatus.Succeeded, result.Status);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("abc"u8.ToArray(), File.ReadAllBytes(result.StandardInputPath));
        Assert.Empty(File.ReadAllBytes(result.StandardOutputPath));
        Assert.Empty(File.ReadAllBytes(result.StandardErrorPath));
        Assert.NotEmpty(File.ReadAllBytes(result.ResourceSamplesPath));
        Assert.True(File.Exists(result.ArtifactPreflightPath));
        Assert.True(File.Exists(result.ArtifactPostflightPath));
        Assert.Equal(
            File.ReadAllBytes(result.ArtifactPreflightPath),
            RideBound.Contracts.Serialization.CanonicalJson.Canonicalize(
                File.ReadAllBytes(result.ArtifactPreflightPath)));
        Assert.Equal(
            ProcessLaunchIdentity.Calculate(request.ExecutablePath, request.Arguments),
            result.LaunchCommandSha256);
        Assert.Equal(result.ArtifactPreflightSha256, result.ArtifactPostflightSha256);
        Assert.True(result.PeakProcessCount >= 1);
    }

    [Theory]
    [InlineData("hang", "resource.wall-time-exceeded")]
    [InlineData("cpu-burn", "resource.cpu-time-exceeded")]
    public async Task Time_limits_kill_process_tree_and_retain_evidence(
        string mode,
        string expectedCode)
    {
        using var temp = new TestDirectory();
        var limits = mode == "hang"
            ? DefaultLimits with { WallTimeLimitMs = 100 }
            : DefaultLimits with { WallTimeLimitMs = 10_000, CpuTimeLimitMs = 20 };
        var request = FakeRequest(temp, mode, [mode], limits: limits);

        var result = await ExternalProcessSupervisor.RunAsync(request);

        AssertFailure(result, expectedCode);
    }

    [Fact]
    public async Task Working_set_limit_is_typed_and_retains_samples()
    {
        using var temp = new TestDirectory();
        var request = FakeRequest(
            temp,
            "memory",
            ["hang"],
            limits: DefaultLimits with
            {
                WallTimeLimitMs = 10_000,
                PeakWorkingSetLimitBytes = 1,
            });

        var result = await ExternalProcessSupervisor.RunAsync(request);

        AssertFailure(result, "resource.memory-exceeded");
    }

    [Theory]
    [InlineData("stdout-flood", "resource.stdout-bytes-exceeded")]
    [InlineData("stderr-flood", "resource.stderr-bytes-exceeded")]
    public async Task Output_stream_limits_are_typed(
        string mode,
        string expectedCode)
    {
        using var temp = new TestDirectory();
        var request = FakeRequest(
            temp,
            mode,
            [mode, "65536"],
            limits: DefaultLimits with
            {
                StandardOutputLimitBytes = 1024,
                StandardErrorLimitBytes = 1024,
            });

        var result = await ExternalProcessSupervisor.RunAsync(request);

        AssertFailure(result, expectedCode);
        var evidencePath = mode == "stdout-flood"
            ? result.StandardOutputPath
            : result.StandardErrorPath;
        Assert.Equal(1024, new FileInfo(evidencePath).Length);
        Assert.True(
            mode == "stdout-flood"
                ? result.StandardOutputBytes > 1024
                : result.StandardErrorBytes > 1024);
    }

    [Fact]
    public async Task Input_stream_limit_is_typed_without_transmitting_over_limit_bytes()
    {
        using var temp = new TestDirectory();
        var request = FakeRequest(
            temp,
            "stdin",
            ["consume"],
            new RawInputConversation(new byte[2048]),
            DefaultLimits with { StandardInputLimitBytes = 1024 });

        var result = await ExternalProcessSupervisor.RunAsync(request);

        AssertFailure(result, "resource.stdin-bytes-exceeded");
        Assert.Empty(File.ReadAllBytes(result.StandardInputPath));
    }

    [Fact]
    public async Task Nonzero_exit_is_typed_crash_with_stderr_retained()
    {
        using var temp = new TestDirectory();
        var result = await ExternalProcessSupervisor.RunAsync(
            FakeRequest(temp, "crash", ["crash"]));

        AssertFailure(result, "process.crash");
        Assert.Equal(17, result.ExitCode);
        Assert.Contains(
            "deterministic fake crash",
            File.ReadAllText(result.StandardErrorPath),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Descendant_limit_kills_parent_and_hanging_child()
    {
        using var temp = new TestDirectory();
        var result = await ExternalProcessSupervisor.RunAsync(
            FakeRequest(temp, "child", ["child-hang"]));

        AssertFailure(result, "resource.process-count-exceeded");
        Assert.True(result.PeakProcessCount > 1);
        var childId = int.Parse(
            File.ReadAllText(result.StandardOutputPath).Trim(),
            System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(await ProcessHasExited(childId));
    }

    [Fact]
    public async Task Caller_cancellation_kills_process_and_is_not_reported_as_timeout()
    {
        using var temp = new TestDirectory();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var result = await ExternalProcessSupervisor.RunAsync(
            FakeRequest(temp, "cancel", ["hang"]),
            cancellation.Token);

        Assert.Equal(ExternalProcessTerminalStatus.Cancelled, result.Status);
        Assert.Equal("process.cancelled", result.Failure!.Code);
        Assert.NotEmpty(File.ReadAllBytes(result.ResourceSamplesPath));
    }

    [Fact]
    public async Task Postflight_detects_a_pinned_configuration_mutation()
    {
        using var temp = new TestDirectory();
        var config = Path.Combine(temp.Root, "immutable-config.json");
        File.WriteAllText(config, "{}");
        var request = FakeRequest(temp, "mutate", ["mutate", config]);
        var pins = request.PinnedFiles.Concat([ProcessArtifactIdentity.Pin("config", config)]).ToArray();
        request = request with
        {
            PinnedFiles = pins,
            ExpectedRuntimeInventorySha256 = ProcessArtifactIdentity.Calculate(pins),
        };

        var result = await ExternalProcessSupervisor.RunAsync(request);

        AssertFailure(result, "artifact.mismatch");
        Assert.Equal("postflight", result.Failure!.Stage);
        Assert.NotEqual(result.ArtifactPreflightSha256, result.ArtifactPostflightSha256);
        Assert.True(File.Exists(result.ArtifactPreflightPath));
        Assert.True(File.Exists(result.ArtifactPostflightPath));
    }

    [Fact]
    public async Task Missing_required_output_is_typed_incomplete_protocol()
    {
        using var temp = new TestDirectory();
        var result = await ExternalProcessSupervisor.RunAsync(
            FakeRequest(temp, "incomplete", ["no-output"], new RequiredByteConversation()));

        AssertFailure(result, "protocol.incomplete-output");
        Assert.Equal("completion", result.Failure!.Stage);
    }

    [Theory]
    [InlineData("capability.divergence", "negotiation")]
    [InlineData("solver.unknown", "decision")]
    [InlineData("protocol.invalid-output", "parsing")]
    [InlineData("protocol.incomplete-output", "completion")]
    [InlineData("state.divergence", "validation")]
    public async Task Conversation_failures_use_contract_canonical_stage(
        string code,
        string expectedStage)
    {
        using var temp = new TestDirectory();
        var result = await ExternalProcessSupervisor.RunAsync(
            FakeRequest(
                temp,
                "typed-" + code.Replace('.', '-'),
                ["consume"],
                new TypedFailureConversation(code)));

        AssertFailure(result, code);
        Assert.Equal(expectedStage, result.Failure!.Stage);
    }

    [Fact]
    public async Task Unsupported_conversation_failure_is_canonicalized_not_persisted()
    {
        using var temp = new TestDirectory();
        var result = await ExternalProcessSupervisor.RunAsync(
            FakeRequest(
                temp,
                "typed-unsupported",
                ["consume"],
                new TypedFailureConversation("outcome.depends-on-result")));

        AssertFailure(result, "protocol.invalid-output");
        Assert.Equal("parsing", result.Failure!.Stage);
        Assert.Contains("unsupported", result.Failure.SafeMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Process_start_failure_is_typed_after_pinned_preflight()
    {
        using var temp = new TestDirectory();
        var invalidExecutable = Path.Combine(temp.Root, "not-an-executable.txt");
        File.WriteAllText(invalidExecutable, "not executable");
        var pins = new[]
        {
            ProcessArtifactIdentity.Pin("invalid-executable", invalidExecutable),
        };
        var request = new ExternalProcessRunRequest(
            "start-failure",
            temp.RepositoryRoot,
            Path.Combine(temp.Root, "runs"),
            invalidExecutable,
            [],
            pins,
            ProcessArtifactIdentity.Calculate(pins),
            DefaultLimits,
            new RawInputConversation([]));

        var result = await ExternalProcessSupervisor.RunAsync(request);

        AssertFailure(result, "process.start-failed", expectSamples: false);
        Assert.Equal("execution", result.Failure!.Stage);
        Assert.True(File.Exists(result.ArtifactPreflightPath));
    }

    [Fact]
    public async Task Existing_run_root_is_refused_without_overwrite()
    {
        using var temp = new TestDirectory();
        var request = FakeRequest(temp, "existing", ["consume"]);
        var existing = Path.Combine(request.IsolatedRunsRoot, request.RunId);
        Directory.CreateDirectory(existing);
        var witness = Path.Combine(existing, "user-owned.txt");
        File.WriteAllText(witness, "preserve");

        var result = await ExternalProcessSupervisor.RunAsync(request);

        AssertFailure(result, "artifact.mismatch", expectSamples: false);
        Assert.Equal("preserve", File.ReadAllText(witness));
    }

    [Fact]
    public async Task Executable_must_be_part_of_the_exact_pinned_inventory()
    {
        using var temp = new TestDirectory();
        var request = FakeRequest(temp, "unpinned", ["consume"]);
        var pins = request.PinnedFiles.Skip(1).ToArray();
        request = request with
        {
            PinnedFiles = pins,
            ExpectedRuntimeInventorySha256 = ProcessArtifactIdentity.Calculate(pins),
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => ExternalProcessSupervisor.RunAsync(request));
    }

    [Fact]
    public async Task Published_commitment_fixture_runs_through_exact_external_runner_protocol()
    {
        using var temp = new TestDirectory();
        var repository = RepositoryRoot();
        var configuration = BuildConfiguration();
        var runnerRoot = Path.Combine(
            repository,
            "src",
            "RideBound.Runner",
            "bin",
            configuration,
            "net10.0");
        var runner = Path.Combine(runnerRoot, "RideBound.Runner.dll");
        var scenario = Path.Combine(
            repository,
            "benchmarks",
            "scenarios",
            "wp3-commitment-tiny",
            "commitment-demo.input.ndjson");
        var policyConfiguration = Path.Combine(
            repository,
            "benchmarks",
            "configurations",
            "wp3-boundary-test-v1.json");
        var lines = File.ReadAllLines(scenario)
            .Select(Encoding.UTF8.GetBytes)
            .ToArray();
        var fixture = new RunnerProtocolFixture(
            lines[0],
            lines[1],
            [lines[2], lines[4], lines[6], lines[8]]);
        var dotnet = DotnetHost();
        var pins = ExactRuntimePins(dotnet)
            .Concat(
                Directory.GetFiles(runnerRoot)
                    .Where(path => !path.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase))
                    .Order(StringComparer.Ordinal)
                    .Select((path, index) => ProcessArtifactIdentity.Pin($"runner-{index:D3}", path)))
            .Concat(
            [
                ProcessArtifactIdentity.Pin("policy-config", policyConfiguration),
                ProcessArtifactIdentity.Pin("scenario-source", scenario),
            ])
            .ToArray();
        var arguments = new[]
        {
            runner,
            "--mode",
            "commitment",
            "--policy-config",
            policyConfiguration,
        };
        var request = new ExternalProcessRunRequest(
            "published-runner",
            repository,
            Path.Combine(temp.Root, "runs"),
            dotnet,
            arguments,
            pins,
            ProcessArtifactIdentity.Calculate(pins),
            DefaultLimits with
            {
                WallTimeLimitMs = 20_000,
                CpuTimeLimitMs = 20_000,
                StandardInputLimitBytes = 1_048_576,
                StandardOutputLimitBytes = 1_048_576,
            },
            new RunnerProtocolFixtureConversation(fixture));

        Assert.Contains(
            pins,
            pin => string.Equals(pin.FullPath, runner, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            pins,
            pin => string.Equals(
                Path.GetFileName(pin.FullPath),
                "RideBound.Contracts.dll",
                StringComparison.Ordinal));

        var result = await ExternalProcessSupervisor.RunAsync(request);

        Assert.True(
            result.Status == ExternalProcessTerminalStatus.Succeeded,
            result.Failure?.SafeMessage);
        Assert.Equal(0, result.ExitCode);
        var output = File.ReadAllLines(result.StandardOutputPath);
        Assert.Equal(7, output.Length);
        Assert.Empty(File.ReadAllBytes(result.StandardErrorPath));
        Assert.Equal(result.ArtifactPreflightSha256, result.ArtifactPostflightSha256);
    }

    private static ExternalProcessRunRequest FakeRequest(
        TestDirectory temp,
        string runId,
        IReadOnlyList<string> fakeArguments,
        IExternalProcessConversation? conversation = null,
        ExternalProcessLimits? limits = null)
    {
        var dotnet = DotnetHost();
        var fake = Path.Combine(
            RepositoryRoot(),
            "tools",
            "RideBound.Wp6FakeChild",
            "bin",
            BuildConfiguration(),
            "net10.0",
            "RideBound.Wp6FakeChild.dll");
        var pins = RuntimePins(dotnet)
            .Concat(
            [
                ProcessArtifactIdentity.Pin("fake-child", fake),
                ProcessArtifactIdentity.Pin("fake-child-deps", Path.ChangeExtension(fake, ".deps.json")),
                ProcessArtifactIdentity.Pin(
                    "fake-child-runtime-config",
                    Path.ChangeExtension(fake, ".runtimeconfig.json")),
            ])
            .ToArray();
        var arguments = new[] { fake }.Concat(fakeArguments).ToArray();
        return new ExternalProcessRunRequest(
            runId,
            temp.RepositoryRoot,
            Path.Combine(temp.Root, "runs"),
            dotnet,
            arguments,
            pins,
            ProcessArtifactIdentity.Calculate(pins),
            limits ?? DefaultLimits,
            conversation ?? new RawInputConversation([]));
    }

    private static IReadOnlyList<PinnedProcessFile> RuntimePins(string dotnet)
    {
        var runtimeRoot = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var nativeRuntime = OperatingSystem.IsWindows()
            ? new[] { "coreclr.dll", "hostpolicy.dll" }
            : new[] { "libcoreclr.so", "libhostpolicy.so" };
        var paths = new[] { typeof(object).Assembly.Location }
            .Concat(nativeRuntime.Select(name => Path.Combine(runtimeRoot, name)))
            .Where(File.Exists)
            .ToArray();
        return new[] { ProcessArtifactIdentity.Pin("dotnet-host", dotnet) }
            .Concat(
                paths.Select(
                    (path, index) => ProcessArtifactIdentity.Pin($"runtime-{index:D3}", path)))
            .ToArray();
    }

    private static IReadOnlyList<PinnedProcessFile> ExactRuntimePins(string dotnet)
    {
        var runtimeRoot = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        return new[] { ProcessArtifactIdentity.Pin("dotnet-host", dotnet) }
            .Concat(
                Directory.GetFiles(runtimeRoot)
                    .Order(StringComparer.Ordinal)
                    .Select(
                        (path, index) => ProcessArtifactIdentity.Pin(
                            $"runtime-{index:D3}",
                            path)))
            .ToArray();
    }

    private static string DotnetHost()
    {
        var candidates = new List<string?>
        {
            Environment.GetEnvironmentVariable("DOTNET_HOST_PATH"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "dotnet",
                OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"),
        };
        candidates.AddRange(
            (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(
                    path => Path.Combine(
                        path,
                        OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet")));
        var candidate = candidates.FirstOrDefault(value => value is not null && File.Exists(value))
            ?? throw new InvalidOperationException("An absolute dotnet host was not found.");
        var info = new FileInfo(candidate);
        return info.LinkTarget is null
            ? info.FullName
            : info.ResolveLinkTarget(returnFinalTarget: true)!.FullName;
    }

    private static string RepositoryRoot() =>
        typeof(ExternalProcessSupervisorTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(value => value.Key == "RideBoundRepositoryRoot")
            .Value!;

    private static string BuildConfiguration() =>
        Directory.GetParent(Directory.GetParent(AppContext.BaseDirectory)!.FullName)!.Name;

    private static void AssertFailure(
        ExternalProcessRunResult result,
        string expectedCode,
        bool expectSamples = true)
    {
        Assert.Equal(ExternalProcessTerminalStatus.Failed, result.Status);
        Assert.Equal(expectedCode, result.Failure!.Code);

        if (expectSamples)
        {
            Assert.True(File.Exists(result.ResourceSamplesPath));
            Assert.NotEmpty(File.ReadAllBytes(result.ResourceSamplesPath));
        }
    }

    private static async Task<bool> ProcessHasExited(int processId)
    {
        for (var attempt = 0; attempt < 40; attempt++)
        {
            try
            {
                using var process = Process.GetProcessById(processId);

                if (process.HasExited)
                {
                    return true;
                }
            }
            catch (ArgumentException)
            {
                return true;
            }

            await Task.Delay(25);
        }

        return false;
    }

    private sealed class RequiredByteConversation : IExternalProcessConversation
    {
        public async Task<ProcessConversationResult> ExecuteAsync(
            Stream standardInput,
            Stream standardOutput,
            CancellationToken cancellationToken)
        {
            standardInput.Close();
            var value = new byte[1];

            if (await standardOutput.ReadAsync(value, cancellationToken) == 0)
            {
                throw new EndOfStreamException("Required output byte is absent.");
            }

            return ProcessConversationResult.Success();
        }
    }

    private sealed class TypedFailureConversation(string code) : IExternalProcessConversation
    {
        public async Task<ProcessConversationResult> ExecuteAsync(
            Stream standardInput,
            Stream standardOutput,
            CancellationToken cancellationToken)
        {
            await standardInput.FlushAsync(cancellationToken);
            return ProcessConversationResult.Failed(code, "Typed fixture failure.");
        }
    }
}
