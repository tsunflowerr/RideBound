using System.Xml.Linq;

namespace RideBound.ArchitectureTests;

public sealed class DependencyRuleTests
{
    private static readonly IReadOnlyDictionary<string, string[]> AllowedReferences =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["RideBound.Domain"] = [],
            ["RideBound.Contracts"] = [],
            ["RideBound.Application"] = ["RideBound.Domain"],
            ["RideBound.Algorithms"] =
                ["RideBound.Application", "RideBound.Domain"],
            ["RideBound.Solvers.OrTools"] =
                ["RideBound.Application", "RideBound.Domain"],
            ["RideBound.Infrastructure"] =
                ["RideBound.Application", "RideBound.Contracts", "RideBound.Domain"],
            ["RideBound.Runner"] =
                [
                    "RideBound.Algorithms",
                    "RideBound.Application",
                    "RideBound.Contracts",
                    "RideBound.Infrastructure",
                    "RideBound.Solvers.OrTools",
                ],
        };

    [Fact]
    public void Project_references_follow_clean_architecture()
    {
        var repositoryRoot = FindRepositoryRoot();
        var projectFiles = Directory.GetFiles(
            Path.Combine(repositoryRoot, "src"),
            "*.csproj",
            SearchOption.AllDirectories);

        var failures = new List<string>();

        foreach (var projectFile in projectFiles)
        {
            var projectName = Path.GetFileNameWithoutExtension(projectFile);
            Assert.True(
                AllowedReferences.TryGetValue(projectName, out var allowed),
                $"Architecture policy is missing project '{projectName}'.");

            var actual = ReadProjectReferences(projectFile);
            var unexpected = actual.Except(allowed!, StringComparer.Ordinal).ToArray();

            if (unexpected.Length > 0)
            {
                failures.Add(
                    $"{projectName} has forbidden references: {string.Join(", ", unexpected)}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Theory]
    [InlineData(@"..\RideBound.Domain\RideBound.Domain.csproj", "RideBound.Domain")]
    [InlineData("../RideBound.Domain/RideBound.Domain.csproj", "RideBound.Domain")]
    public void Project_reference_names_are_portable_across_path_styles(
        string projectReference,
        string expectedProjectName)
    {
        Assert.Equal(expectedProjectName, GetProjectName(projectReference));
    }

    [Theory]
    [InlineData("RideBound.Domain")]
    [InlineData("RideBound.Application")]
    [InlineData("RideBound.Contracts")]
    public void Inner_projects_do_not_reference_external_packages(string projectName)
    {
        var projectFile = FindProjectFile(projectName);
        var document = XDocument.Load(projectFile);
        var packages = document
            .Descendants("PackageReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => value is not null)
            .ToArray();

        Assert.True(
            packages.Length == 0,
            $"{projectName} must not reference packages: {string.Join(", ", packages)}");
    }

    [Fact]
    public void Domain_and_application_do_not_contain_framework_dependencies()
    {
        var repositoryRoot = FindRepositoryRoot();
        var forbiddenTerms = new[]
        {
            "OptiGo",
            "Microsoft.EntityFrameworkCore",
            "Microsoft.AspNetCore",
            "Google.OrTools",
            "FleetPy",
            "RidePy",
            "AMoD2",
        };
        var failures = new List<string>();

        foreach (var projectName in new[] { "RideBound.Domain", "RideBound.Application" })
        {
            var projectDirectory = Path.Combine(repositoryRoot, "src", projectName);

            var sourceFiles = Directory
                .GetFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
                .Where(path => !IsBuildOutput(path));

            foreach (var sourceFile in sourceFiles)
            {
                var source = File.ReadAllText(sourceFile);

                foreach (var forbiddenTerm in forbiddenTerms)
                {
                    if (source.Contains(forbiddenTerm, StringComparison.Ordinal))
                    {
                        failures.Add(
                            $"{Path.GetRelativePath(repositoryRoot, sourceFile)} contains " +
                            $"forbidden dependency term '{forbiddenTerm}'.");
                    }
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private static string FindProjectFile(string projectName)
    {
        return Path.Combine(
            FindRepositoryRoot(),
            "src",
            projectName,
            $"{projectName}.csproj");
    }

    private static bool IsBuildOutput(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase);
    }

    private static string[] ReadProjectReferences(string projectFile)
    {
        var document = XDocument.Load(projectFile);

        return document
            .Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => value is not null)
            .Select(value => GetProjectName(value!))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string GetProjectName(string projectReference)
    {
        var normalized = projectReference.Replace('\\', '/');
        var fileName = normalized[(normalized.LastIndexOf('/') + 1)..];
        return Path.GetFileNameWithoutExtension(fileName);
    }

    private static string FindRepositoryRoot()
    {
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
            "Could not find the RideBound repository root.");
    }
}
