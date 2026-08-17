using System.Text.Json;
using RideBound.Benchmarking.Bundles;
using RideBound.Benchmarking.Contracts;
using RideBound.Benchmarking.Datasets;
using RideBound.Benchmarking.Execution;
using RideBound.Benchmarking.Metrics;
using RideBound.Contracts.Serialization;

namespace RideBound.Benchmarking.EndToEnd;

public sealed record PublicDerivativeHarnessPaths(
    string RepositoryRoot,
    string CacheRoot,
    string WorkRoot,
    string BundleDirectory,
    string ReceiptPath,
    string BuildConfiguration);

public static class PublicDerivativePairedHarness
{
    public static async Task<TinyPairedHarnessReceipt> RunAsync(
        PublicDerivativeHarnessPaths paths,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var repository = Path.GetFullPath(paths.RepositoryRoot);
        var cache = Path.GetFullPath(paths.CacheRoot);
        using var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5,
            AutomaticDecompression = System.Net.DecompressionMethods.None,
        };
        using var client = new HttpClient(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "RideBound-WP6-Public-Harness/1.0");
        var acquisition = await new VerifiedDatasetDownloader(client).AcquireAsync(
            new DatasetAcquisitionRequest(
                DatasetSourceRegistry.FleetPyManhattanV1,
                new DatasetCacheOptions(cache, repository),
                "CC-BY-4.0"),
            cancellationToken);

        if (acquisition.Status != DatasetAcquisitionStatus.Succeeded)
        {
            throw new InvalidDataException(
                $"Public source acquisition failed at '{acquisition.Issue!.Stage}' "
                    + $"with '{acquisition.Issue.Code}': "
                    + acquisition.Issue.SafeMessage);
        }

        var artifact = acquisition.Artifact!;
        var descriptorBytes = BenchmarkContractCodec.Encode(artifact.Descriptor);
        var derivative = PublicDerivativeMechanicalFixtureCompiler.Compile(
            repository,
            descriptorBytes);
        var descriptorPath = Path.Combine(
            Path.GetDirectoryName(artifact.FullPath)!,
            "dataset-descriptor.json");

        if (!File.Exists(descriptorPath)
            || !File.ReadAllBytes(descriptorPath).SequenceEqual(descriptorBytes))
        {
            throw new InvalidDataException(
                "Cached public descriptor is not the exact canonical compiler input.");
        }

        var commitmentConfigurationPath = Path.Combine(
            repository,
            "benchmarks",
            "configurations",
            "wp6-public-mechanical-commitment-v1.json");
        ValidateCommitmentPolicyBinding(
            derivative.Scenario,
            commitmentConfigurationPath);

        var pinnedInputs = new[] { descriptorPath }
            .Concat(derivative.BundleSources.Select(value => value.FullPath))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var fixture = new MechanicalPairedHarnessFixture(
            derivative.Dataset,
            derivative.CanonicalDataset,
            derivative.Scenario,
            derivative.CanonicalScenario,
            derivative.ScenarioHash,
            derivative.GraphSha256,
            derivative.CanonicalCapabilitySelection,
            derivative.SourceFixturePath,
            derivative.SourceFixtureSha256,
            pinnedInputs,
            derivative.BundleSources,
            (run, arm, runnerHash, sourceInventoryHash) =>
                new PublicDerivativeMechanicalDrainConversation(
                    derivative,
                    PublicDerivativeMechanicalFixtureCompiler.CreateInitializeEnvelope(
                        derivative,
                        run,
                        arm,
                        runnerHash,
                        sourceInventoryHash)));
        var profile = new MechanicalPairedHarnessProfile(
            "public-medium",
            "wp6-public-medium-paired-e2e-v1",
            "wp6-public-mechanical-commitment-v1.json",
            ["tools/RideBound.Wp6MediumHarness"],
            "RideBound.Wp6.PublicMediumTranscriptSet.v1",
            "RideBound.Wp6.PublicMediumDecisionSet.v1",
            "wp6-public-medium-paired-harness-v1",
            [
                "fleetpy-manhattan-zenodo-15187906-v1",
                PublicDerivativeMechanicalFixtureCompiler.DriverSemanticsId,
            ],
            "wp6-public-medium-paired-mechanical-v1",
            "2026-08-12",
            new ExternalProcessLimits(
                WallTimeLimitMs: 180_000,
                CpuTimeLimitMs: 180_000,
                PeakWorkingSetLimitBytes: 2_147_483_648,
                ProcessCountLimit: 16,
                StandardInputLimitBytes: 33_554_432,
                StandardOutputLimitBytes: 33_554_432,
                StandardErrorLimitBytes: 1_048_576,
                SampleIntervalMs: 10),
            ValidateMediumCoverage);
        return await TinyPairedHarness.RunCore(
            new TinyHarnessPaths(
                repository,
                paths.WorkRoot,
                paths.BundleDirectory,
                paths.ReceiptPath,
                paths.BuildConfiguration),
            fixture,
            profile,
            cancellationToken);
    }

    internal static void ValidateCommitmentPolicyBinding(
        ScenarioContent scenario,
        string commitmentConfigurationPath)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentException.ThrowIfNullOrWhiteSpace(commitmentConfigurationPath);
        var expectedPolicyIds = scenario.Requests
            .Select(value => value.CommitmentPolicyId)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (expectedPolicyIds.Length != 1)
        {
            throw new InvalidDataException(
                "Public derivative requests must bind exactly one commitment policy ID.");
        }

        byte[] canonical;

        try
        {
            canonical = CanonicalJson.Canonicalize(
                File.ReadAllBytes(commitmentConfigurationPath));
        }
        catch (Exception exception) when (
            exception is IOException or CanonicalJsonException)
        {
            throw new InvalidDataException(
                "Public derivative commitment configuration is absent or noncanonicalizable.",
                exception);
        }

        using var document = JsonDocument.Parse(canonical);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("policies", out var policies)
            || policies.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException(
                "Public derivative commitment configuration lacks a policies array.");
        }

        var configuredPolicyIds = new List<string>();

        foreach (var policy in policies.EnumerateArray())
        {
            if (policy.ValueKind != JsonValueKind.Object
                || !policy.TryGetProperty("policyId", out var policyId)
                || policyId.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(policyId.GetString()))
            {
                throw new InvalidDataException(
                    "Public derivative commitment configuration has an invalid policy ID.");
            }

            configuredPolicyIds.Add(policyId.GetString()!);
        }

        if (configuredPolicyIds.Distinct(StringComparer.Ordinal).Count()
                != configuredPolicyIds.Count
            || !configuredPolicyIds.Contains(expectedPolicyIds[0], StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                $"Public derivative commitment configuration does not uniquely declare "
                    + $"scenario policy '{expectedPolicyIds[0]}'.");
        }
    }

    private static void ValidateMediumCoverage(IReadOnlyList<MetricRow> rows)
    {
        foreach (var run in rows.GroupBy(value => value.RunId, StringComparer.Ordinal))
        {
            long All(string metric) => run.Single(
                value => value.MetricId == metric
                    && value.WindowId == MetricWindowId.All).ValueInteger ?? 0;

            if (All("request.arrived.count") != 128
                || All("request.accepted.count") != 128
                || All("request.completed.count") != 128
                || All("request.rejected.count") != 0
                || All("request.deferred.action.count") != 0
                || All("request.acceptance.ppm") != 1_000_000
                || All("request.completion.ppm") != 1_000_000
                || All("promise.publication.count") < 128
                || All("commitment.breach.count") != 0
                || All("certificate.non-normal.count") != 0)
            {
                throw new InvalidDataException(
                    "Public medium mechanical coverage must conserve 128 "
                        + "arrivals, acceptances and completions without a "
                        + "defer, rejection, breach or non-normal certificate.");
            }
        }
    }
}
