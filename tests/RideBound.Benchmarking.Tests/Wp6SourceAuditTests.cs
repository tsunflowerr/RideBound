namespace RideBound.Benchmarking.Tests;

public sealed class Wp6SourceAuditTests
{
    [Fact]
    public void Semantic_sources_have_no_mutable_rng_and_only_allowlisted_nondeterminism()
    {
        var repository = StrictBundleTestFixture.FindRepositoryRoot();
        var roots = new[]
        {
            Path.Combine(repository, "src", "RideBound.Benchmarking"),
            Path.Combine(repository, "src", "RideBound.Benchmarking.Contracts"),
        }.Concat(
            Directory.GetDirectories(Path.Combine(repository, "tools"), "RideBound.Wp6*"));
        var files = roots.SelectMany(
                root => Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var forbidden = new[]
        {
            "new Random(",
            "Random.Shared",
            "System.Random",
            "DateTime.Now",
            "DateTime.UtcNow",
            "Environment.TickCount",
            ".GetHashCode(",
        };

        foreach (var path in files)
        {
            var source = File.ReadAllText(path);
            Assert.All(
                forbidden,
                token => Assert.DoesNotContain(token, source, StringComparison.Ordinal));
        }

        Assert.Equal(
            new[]
            {
                "src/RideBound.Benchmarking/Datasets/SafeZipExtractor.cs|RandomNumberGenerator|1",
                "src/RideBound.Benchmarking/Datasets/VerifiedDatasetDownloader.cs|RandomNumberGenerator|1",
                "src/RideBound.Benchmarking/EndToEnd/TinyPairedHarness.cs|DateTimeOffset.UtcNow|2",
                "tools/RideBound.Wp6Normalize/Program.cs|Guid.NewGuid|1",
            },
            Occurrences(repository, files));
    }

    [Fact]
    public void Metric_calculation_has_one_producer_and_one_verifier_recomputation_path()
    {
        var repository = StrictBundleTestFixture.FindRepositoryRoot();
        var root = Path.Combine(repository, "src", "RideBound.Benchmarking");
        var callSites = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Select(
                path => new
                {
                    Path = path,
                    Count = Count(
                        File.ReadAllText(path),
                        "MechanicalMetricCalculator.Calculate("),
                })
            .Where(value => value.Count > 0)
            .Select(
                value => $"{Relative(repository, value.Path)}|{value.Count}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "src/RideBound.Benchmarking/Bundles/StrictBagItBundleVerifier.cs|1",
                "src/RideBound.Benchmarking/EndToEnd/TinyPairedHarness.cs|1",
            },
            callSites);
    }

    private static string[] Occurrences(
        string repository,
        IReadOnlyList<string> files)
    {
        var audited = new[]
        {
            "RandomNumberGenerator",
            "DateTimeOffset.UtcNow",
            "Guid.NewGuid",
        };
        return files.SelectMany(
                path => audited.Select(
                    token => new
                    {
                        Path = path,
                        Token = token,
                        Count = Count(File.ReadAllText(path), token),
                    }))
            .Where(value => value.Count > 0)
            .Select(
                value => $"{Relative(repository, value.Path)}|{value.Token}|{value.Count}")
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static int Count(string value, string token)
    {
        var count = 0;
        var offset = 0;

        while ((offset = value.IndexOf(token, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += token.Length;
        }

        return count;
    }

    private static string Relative(string repository, string path) =>
        Path.GetRelativePath(repository, path).Replace('\\', '/');
}
