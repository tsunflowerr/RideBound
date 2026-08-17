using System.Security.Cryptography;
using System.Text.Json;
using RideBound.Benchmarking.Contracts;

namespace RideBound.Benchmarking.Tests;

public sealed class FleetPyPublicDerivativeTests
{
    [Theory]
    [InlineData("tiny", 8, 2, 16, 240)]
    [InlineData("medium", 128, 32, 96, 9_120)]
    public void Published_derivative_is_canonical_conserved_and_hash_bound(
        string profile,
        int requestCount,
        int vehicleCount,
        int nodeCount,
        int arcCount)
    {
        var root = Path.Combine(
            FindRepositoryRoot(),
            "benchmarks",
            "fixtures",
            "wp6",
            "public",
            "fleetpy-manhattan-v1",
            profile);
        var scenarioBytes = File.ReadAllBytes(Path.Combine(root, "scenario-content.json"));
        var reportBytes = File.ReadAllBytes(Path.Combine(root, "normalization-report.json"));
        var scenario = BenchmarkContractCodec.Decode<ScenarioContent>(scenarioBytes);
        var report = BenchmarkContractCodec.Decode<NormalizationReport>(reportBytes);
        Assert.True(scenario.IsSuccess, scenario.Error?.ToString());
        Assert.True(report.IsSuccess, report.Error?.ToString());
        Assert.Equal(scenarioBytes, scenario.CanonicalBytes);
        Assert.Equal(reportBytes, report.CanonicalBytes);
        Assert.Equal(requestCount, scenario.Value!.Requests.Count);
        Assert.Equal(vehicleCount, scenario.Value.Fleet.Count);
        Assert.Equal(nodeCount, scenario.Value.ValidationSummary.NodeCount);
        Assert.Equal(arcCount, scenario.Value.ValidationSummary.DirectedArcCount);
        Assert.Equal(
            report.Value!.InputRecordCount,
            report.Value.EligibleRecordCount + report.Value.ExcludedRecordCount);
        Assert.Equal(requestCount, report.Value.SelectedRecordCount);

        using var dispositionDocument = JsonDocument.Parse(
            File.ReadAllBytes(Path.Combine(root, "selection-frame.json")));
        var dispositions = dispositionDocument.RootElement.EnumerateArray().ToArray();
        Assert.Equal(report.Value.EligibleRecordCount, dispositions.LongLength);
        Assert.Equal(
            report.Value.SelectedRecordCount,
            dispositions.LongCount(
                value => value.GetProperty("disposition").GetString() == "selected"));
        Assert.Equal(
            report.Value.SelectionFrameSha256,
            Sha(File.ReadAllBytes(Path.Combine(root, "selection-frame.json"))));
        Assert.Equal(
            report.Value.ExclusionLogSha256,
            Sha(File.ReadAllBytes(Path.Combine(root, "normalization-exclusions.json"))));
        Assert.Equal(Sha(scenarioBytes), report.Value.ScenarioContentSha256);
        Assert.Equal(
            BenchmarkIdentity.CalculateScenario(scenarioBytes),
            report.Value.ScenarioHash);

        using var manifest = JsonDocument.Parse(
            File.ReadAllBytes(Path.Combine(root, "derivative-manifest.json")));
        Assert.Equal(
            BenchmarkIdentity.CalculateNormalizationReport(reportBytes),
            manifest.RootElement.GetProperty("normalizationReportHash").GetString());
        Assert.Equal(
            "syntheticPolicyOverlay",
            manifest.RootElement.GetProperty("policyObservationClass").GetString());
        Assert.Equal(
            "CC-BY-4.0",
            manifest.RootElement.GetProperty("licenseSpdx").GetString());
        Assert.DoesNotContain(
            "normalizationReportHash",
            System.Text.Encoding.UTF8.GetString(scenarioBytes),
            StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "RideBound.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("RideBound repository root not found.");
    }

    private static string Sha(byte[] bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));
}
