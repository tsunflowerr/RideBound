using System.Reflection;

namespace RideBound.Benchmarking.Contracts.Tests;

internal static class FixturePaths
{
    public static string RepositoryRoot { get; } = FindRepositoryRoot();

    public static string PositiveRoot { get; } = Path.Combine(
        RepositoryRoot,
        "benchmarks",
        "fixtures",
        "wp6",
        "contracts",
        "positive");

    public static string NegativeRoot { get; } = Path.Combine(
        RepositoryRoot,
        "benchmarks",
        "fixtures",
        "wp6",
        "contracts",
        "negative");

    public static string SchemaRoot { get; } = Path.Combine(
        RepositoryRoot,
        "benchmarks",
        "schemas",
        "wp6",
        "v1");

    private static string FindRepositoryRoot()
    {
        var configured = typeof(FixturePaths)
            .Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "RideBoundRepositoryRoot")
            .Value;

        if (configured is null || !File.Exists(Path.Combine(configured, "RideBound.slnx")))
        {
            throw new DirectoryNotFoundException("RideBound repository root is unavailable.");
        }

        return configured;
    }
}
