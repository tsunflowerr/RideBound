using System.Security.Cryptography;
using RideBound.Benchmarking.Contracts;
using RideBound.Benchmarking.Datasets;
using RideBound.Benchmarking.EndToEnd;
using RideBound.Benchmarking.Execution;
using RideBound.Benchmarking.Planning;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;

namespace RideBound.Benchmarking.Tests;

public sealed class PublicDerivativeMechanicalDrainConversationTests
{
    [Fact]
    public async Task Exact_runner_mechanically_drains_all_medium_public_requests()
    {
        var repository = StrictBundleTestFixture.FindRepositoryRoot();
        var configuration = Directory.GetParent(
            Directory.GetParent(AppContext.BaseDirectory)!.FullName)!.Name;
        var runnerRoot = Path.Combine(
            repository,
            "src",
            "RideBound.Runner",
            "bin",
            configuration,
            "net10.0");
        var runner = Path.Combine(
            runnerRoot,
            OperatingSystem.IsWindows() ? "RideBound.Runner.exe" : "RideBound.Runner");
        var commitmentPath = Path.Combine(
            repository,
            "benchmarks",
            "configurations",
            "wp6-public-mechanical-commitment-v1.json");
        var wp4Path = Path.Combine(
            repository,
            "benchmarks",
            "configurations",
            "wp4-rolling-cost-boundary-v1.json");
        var fixture = PublicDerivativeMechanicalFixtureCompiler.Compile(
            repository,
            BenchmarkContractCodec.Encode(CreateDescriptor()));
        var commitmentHash = HashCanonicalFile(commitmentPath);
        var wp4Hash = HashCanonicalFile(wp4Path);
        _ = Sha256Hex.TryCreate(commitmentHash, out var commitmentIdentity);
        _ = Sha256Hex.TryCreate(wp4Hash, out var wp4Identity);
        var binding = Wp4PolicyConfigurationBinding.Calculate(
            commitmentIdentity!,
            wp4Identity!);
        var seed = new BenchmarkSeedValue(new string('a', 64), 12_345);
        var run = new PlannedBenchmarkRun(
            new string('b', 64),
            new string('c', 64),
            fixture.ScenarioHash,
            "b1",
            0,
            0,
            false,
            0,
            new string('d', 64),
            seed,
            seed,
            seed,
            seed);
        var arm = new BenchmarkArm(
            "b1",
            "rolling-cost",
            "wp4-boundary-v1",
            binding.Value,
            new string('e', 64),
            "wp4-common-generator-v1",
            10_000,
            "commitment-validator-v1",
            "google-ortools-cp-sat",
            "9.15.6755",
            100_000,
            Sha(fixture.CanonicalCapabilitySelection),
            "wp4-common-candidate-v1");
        var runnerHash = Sha(File.ReadAllBytes(runner));
        var initialize = PublicDerivativeMechanicalFixtureCompiler.CreateInitializeEnvelope(
            fixture,
            run,
            arm,
            runnerHash,
            new string('f', 40));
        var conversation = new PublicDerivativeMechanicalDrainConversation(
            fixture,
            initialize);
        var pins = Directory.GetFiles(runnerRoot)
            .Where(path => !path.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .Select(
                (path, index) => ProcessArtifactIdentity.Pin(
                    $"runner-{index:D3}",
                    path))
            .Concat(
            [
                ProcessArtifactIdentity.Pin("commitment-config", commitmentPath),
                ProcessArtifactIdentity.Pin("wp4-config", wp4Path),
                ProcessArtifactIdentity.Pin("scenario", fixture.SourceFixturePath),
            ])
            .ToArray();
        using var temp = new TestDirectory();
        var request = new ExternalProcessRunRequest(
            run.RunId,
            repository,
            Path.Combine(temp.Root, "runs"),
            runner,
            [
                "--mode",
                "commitment",
                "--policy-config",
                commitmentPath,
                "--wp4-config",
                wp4Path,
                "--solver-seed-source",
                "manifest-master-seed",
            ],
            pins,
            ProcessArtifactIdentity.Calculate(pins),
            new ExternalProcessLimits(
                // Keep the CPU regression ceiling strict while allowing the full-solution
                // test scheduler to pause this process behind other test assemblies.
                WallTimeLimitMs: 180_000,
                CpuTimeLimitMs: 120_000,
                PeakWorkingSetLimitBytes: 2_147_483_648,
                ProcessCountLimit: 16,
                StandardInputLimitBytes: 16_777_216,
                StandardOutputLimitBytes: 16_777_216,
                StandardErrorLimitBytes: 1_048_576,
                SampleIntervalMs: 10),
            conversation);

        var result = await ExternalProcessSupervisor.RunAsync(request);

        Assert.True(
            result.Status == ExternalProcessTerminalStatus.Succeeded,
            $"{result.Failure?.Code}: {result.Failure?.SafeMessage}");
        Assert.Equal(0, result.ExitCode);
        Assert.NotNull(conversation.Summary);
        Assert.Equal(128, conversation.Summary.RequestCount);
        Assert.Equal(
            conversation.Summary.RequestCount,
            conversation.Summary.AcceptedRequestCount
                + conversation.Summary.RejectedRequestCount);
        Assert.Equal(128, conversation.Summary.AcceptedRequestCount);
        Assert.Equal(0, conversation.Summary.RejectedRequestCount);
        Assert.Equal(801, conversation.Summary.ProtocolEventCount);
        Assert.Empty(File.ReadAllBytes(result.StandardErrorPath));
        Assert.Equal(result.ArtifactPreflightSha256, result.ArtifactPostflightSha256);
    }

    private static DatasetDescriptor CreateDescriptor()
    {
        var registration = DatasetSourceRegistry.FleetPyManhattanV1;
        return new DatasetDescriptor(
            BenchmarkContractVersions.V1,
            registration.DatasetId,
            registration.DatasetKind,
            registration.Title,
            registration.ReleaseVersion,
            registration.PersistentUri.AbsoluteUri,
            registration.DownloadUri.AbsoluteUri,
            "2026-08-12T00:00:00Z",
            registration.PublisherArtifactName,
            registration.LicenseSpdx,
            registration.LicenseUri.AbsoluteUri,
            registration.Citation,
            registration.Composition,
            registration.CollectionLimit,
            registration.AllowedUse,
            registration.ForbiddenClaim,
            registration.DirectIdentifierStatus,
            registration.LocationPrecisionClass,
            registration.RetentionClass,
            registration.MaintenanceNote,
            registration.PublisherArtifactLengthBytes,
            registration.PublisherMd5,
            registration.SourceArtifactSha256);
    }

    private static string HashCanonicalFile(string path) =>
        Sha(CanonicalJson.Canonicalize(File.ReadAllBytes(path)));

    private static string Sha(ReadOnlySpan<byte> value) =>
        Convert.ToHexStringLower(SHA256.HashData(value));
}
