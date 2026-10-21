using System.Reflection;
using System.Text;

namespace RideBound.Contracts.Tests.Fixtures;

internal static class FixtureLoader
{
    private const string FixtureRoot = "benchmarks/schemas/fixtures";

    public static string ReadUtf8(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        var normalizedPath = relativePath.Replace('\\', '/');

        if (Path.IsPathRooted(normalizedPath)
            || normalizedPath.Split('/').Any(segment => segment == ".."))
        {
            throw new ArgumentException(
                "Fixture path must stay relative to benchmarks/schemas/fixtures.",
                nameof(relativePath));
        }

        var repositoryRoot = FindRepositoryRoot();
        var fullPath = Path.Combine(
            repositoryRoot,
            FixtureRoot.Replace('/', Path.DirectorySeparatorChar),
            normalizedPath.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                $"Fixture '{normalizedPath}' was not found under '{FixtureRoot}'.",
                normalizedPath);
        }

        return File.ReadAllText(fullPath, new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true));
    }

    private static string FindRepositoryRoot()
    {
        var configuredRoot = typeof(FixtureLoader)
            .Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .SingleOrDefault(
                attribute => attribute.Key == "RideBoundRepositoryRoot")
            ?.Value;

        if (configuredRoot is not null
            && File.Exists(Path.Combine(configuredRoot, "RideBound.slnx")))
        {
            return configuredRoot;
        }

        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "RideBound.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not find the RideBound repository root from the test output directory.");
    }
}
