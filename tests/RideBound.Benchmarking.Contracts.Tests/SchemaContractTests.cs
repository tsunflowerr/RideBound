using System.Reflection;
using System.Text.Json;
using RideBound.Benchmarking.Contracts;

namespace RideBound.Benchmarking.Contracts.Tests;

public sealed class SchemaContractTests
{
    [Fact]
    public void Inventory_has_exactly_ten_versioned_document_schemas_and_resolved_assets()
    {
        using var inventory = JsonDocument.Parse(
            File.ReadAllBytes(Path.Combine(FixturePaths.SchemaRoot, "schema-inventory.json")));
        Assert.Equal("1.0.3", inventory.RootElement.GetProperty("schemaVersion").GetString());
        Assert.Equal(
            "https://json-schema.org/draft/2020-12/schema",
            inventory.RootElement.GetProperty("draft").GetString());
        Assert.True(File.Exists(Path.Combine(FixturePaths.SchemaRoot, "common.schema.json")));

        var documents = inventory.RootElement.GetProperty("documents").EnumerateArray().ToArray();
        Assert.Equal(10, documents.Length);
        Assert.Equal(10, documents.Select(row => row.GetProperty("documentKind").GetString()).Distinct(StringComparer.Ordinal).Count());

        foreach (var row in documents)
        {
            Assert.True(
                File.Exists(Path.Combine(FixturePaths.SchemaRoot, row.GetProperty("schema").GetString()!)));
            Assert.True(
                File.Exists(Path.Combine(FixturePaths.PositiveRoot, row.GetProperty("positiveFixture").GetString()!)));
        }
    }

    [Fact]
    public void Runtime_top_level_properties_and_required_constructor_fields_match_schemas()
    {
        using var inventory = JsonDocument.Parse(
            File.ReadAllBytes(Path.Combine(FixturePaths.SchemaRoot, "schema-inventory.json")));

        foreach (var row in inventory.RootElement.GetProperty("documents").EnumerateArray())
        {
            var runtimeType = ResolveRuntimeType(row.GetProperty("runtimeType").GetString()!);
            using var schema = JsonDocument.Parse(
                File.ReadAllBytes(
                    Path.Combine(FixturePaths.SchemaRoot, row.GetProperty("schema").GetString()!)));
            var schemaProperties = schema.RootElement.GetProperty("properties")
                .EnumerateObject()
                .Select(property => property.Name)
                .Order(StringComparer.Ordinal)
                .ToArray();
            var runtimeProperties = runtimeType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => JsonNamingPolicy.CamelCase.ConvertName(property.Name))
                .Order(StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(runtimeProperties, schemaProperties);

            var schemaRequired = schema.RootElement.GetProperty("required")
                .EnumerateArray()
                .Select(value => value.GetString()!)
                .Order(StringComparer.Ordinal)
                .ToArray();
            var runtimeRequired = runtimeType.GetConstructors().Single()
                .GetParameters()
                .Where(parameter => !parameter.HasDefaultValue)
                .Select(parameter => JsonNamingPolicy.CamelCase.ConvertName(parameter.Name!))
                .Order(StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(runtimeRequired, schemaRequired);
        }
    }

    [Fact]
    public void Every_positive_fixture_passes_executable_schema_and_runtime_codec()
    {
        using var inventory = JsonDocument.Parse(
            File.ReadAllBytes(Path.Combine(FixturePaths.SchemaRoot, "schema-inventory.json")));
        using var validator = new Draft202012SubsetValidator();

        foreach (var row in inventory.RootElement.GetProperty("documents").EnumerateArray())
        {
            var fixturePath = Path.Combine(
                FixturePaths.PositiveRoot,
                row.GetProperty("positiveFixture").GetString()!);
            using var fixture = JsonDocument.Parse(File.ReadAllBytes(fixturePath));
            var schemaPath = Path.Combine(
                FixturePaths.SchemaRoot,
                row.GetProperty("schema").GetString()!);
            var errors = validator.Validate(schemaPath, fixture.RootElement);
            Assert.True(
                errors.Count == 0,
                $"{Path.GetFileName(fixturePath)}:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
        }
    }

    [Fact]
    public void All_schema_references_resolve_inside_wp6_v1_directory()
    {
        foreach (var schemaPath in Directory.GetFiles(FixturePaths.SchemaRoot, "*.json"))
        {
            using var schema = JsonDocument.Parse(File.ReadAllBytes(schemaPath));
            Walk(schema.RootElement, reference =>
            {
                if (reference.StartsWith('#'))
                {
                    return;
                }

                var relative = reference.Split('#', 2)[0];
                var resolved = Path.GetFullPath(
                    Path.Combine(FixturePaths.SchemaRoot, relative));
                Assert.StartsWith(
                    Path.GetFullPath(FixturePaths.SchemaRoot),
                    resolved,
                    StringComparison.OrdinalIgnoreCase);
                Assert.True(File.Exists(resolved), $"Missing schema reference {reference}");
            });
        }
    }

    private static Type ResolveRuntimeType(string name) => name switch
    {
        nameof(DatasetDescriptor) => typeof(DatasetDescriptor),
        nameof(NormalizationReport) => typeof(NormalizationReport),
        nameof(ScenarioContent) => typeof(ScenarioContent),
        nameof(BenchmarkPlan) => typeof(BenchmarkPlan),
        nameof(RunRecord) => typeof(RunRecord),
        nameof(ObservationIndexRow) => typeof(ObservationIndexRow),
        nameof(FailureRecord) => typeof(FailureRecord),
        nameof(ExclusionRecord) => typeof(ExclusionRecord),
        nameof(MetricRow) => typeof(MetricRow),
        nameof(LogicalBundleManifest) => typeof(LogicalBundleManifest),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    private static void Walk(JsonElement element, Action<string> reference)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.NameEquals("$ref"))
                {
                    reference(property.Value.GetString()!);
                }

                Walk(property.Value, reference);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                Walk(item, reference);
            }
        }
    }
}
