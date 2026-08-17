using System.Diagnostics;
using System.Text.Json;

namespace RideBound.Benchmarking.Contracts.Tests;

public sealed class BenchmarkSeedTests
{
    [Fact]
    public async Task Published_seed_vectors_match_two_clean_processes()
    {
        using var document = JsonDocument.Parse(
            File.ReadAllBytes(VectorPath()));
        var expectedLines = new List<string>();

        foreach (var item in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            var value = BenchmarkSeed.Derive(
                item.GetProperty("masterSeedHex").GetString()!,
                item.GetProperty("scenarioHash").GetString()!,
                item.GetProperty("repeatIndex").GetInt64(),
                item.GetProperty("componentId").GetString()!,
                item.GetProperty("stableItemId").GetString()!);
            Assert.Equal(
                item.GetProperty("expectedDigestHex").GetString(),
                value.DigestHex);
            Assert.Equal(
                item.GetProperty("expectedNonNegativeInt32").GetInt32(),
                value.NonNegativeInt32);
            Assert.Equal(
                value.NonNegativeInt32,
                BenchmarkSeed.ToNonNegativeInt32(value.DigestHex));
            expectedLines.Add(
                string.Join(
                    '|',
                    item.GetProperty("caseId").GetString(),
                    value.DigestHex,
                    value.NonNegativeInt32));
        }

        var expected = string.Join(Environment.NewLine, expectedLines);
        var first = await RunVectorProcess();
        var second = await RunVectorProcess();
        Assert.Equal(expected, first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Address_changes_have_avalanche_and_invalid_addresses_fail_closed()
    {
        const string master =
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        const string scenario =
            "3371cfd2a0b25e8c85812aa66e0338c7a03c5aa97661c4e13c8764343277b83e";
        var first = BenchmarkSeed.Derive(master, scenario, 0, "solver-rng", "b1");
        var second = BenchmarkSeed.Derive(master, scenario, 0, "solver-rng", "c1");
        var differingBits = Convert.FromHexString(first.DigestHex)
            .Zip(Convert.FromHexString(second.DigestHex))
            .Sum(pair => System.Numerics.BitOperations.PopCount((uint)(pair.First ^ pair.Second)));

        Assert.InRange(differingBits, 80, 176);
        Assert.InRange(first.NonNegativeInt32, 0, int.MaxValue);
        Assert.Throws<ArgumentException>(
            () => BenchmarkSeed.Derive(master, scenario, 0, "not-registered"));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => BenchmarkSeed.Derive(master, scenario, -1, "solver-rng"));
        Assert.Throws<ArgumentException>(
            () => BenchmarkSeed.Derive(master.ToUpperInvariant(), scenario, 0, "solver-rng"));
    }

    private static string VectorPath() =>
        Path.Combine(
            FixturePaths.RepositoryRoot,
            "benchmarks",
            "fixtures",
            "wp6",
            "planning",
            "seed-vectors.json");

    private static async Task<string> RunVectorProcess()
    {
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var toolPath = Path.Combine(
            FixturePaths.RepositoryRoot,
            "tools",
            "RideBound.Wp6SeedVectors",
            "bin",
            configuration,
            "net10.0",
            "RideBound.Wp6SeedVectors.dll");
        Assert.True(File.Exists(toolPath), $"Seed vector tool was not built: {toolPath}");
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add(toolPath);
        startInfo.ArgumentList.Add(FixturePaths.RepositoryRoot);

        using var process = Process.Start(startInfo)!;
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, stderr);
        return stdout.Trim();
    }
}
