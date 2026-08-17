using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using RideBound.Contracts.Serialization;

namespace RideBound.Benchmarking.Contracts.Tests;

internal sealed class Draft202012SubsetValidator : IDisposable
{
    private readonly Dictionary<string, JsonDocument> documents =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> Validate(string schemaPath, JsonElement instance)
    {
        var errors = new List<string>();
        var fullPath = Path.GetFullPath(schemaPath);
        ValidateNode(instance, Load(fullPath).RootElement, fullPath, "$", errors);
        return errors;
    }

    public void Dispose()
    {
        foreach (var document in documents.Values)
        {
            document.Dispose();
        }
    }

    private void ValidateNode(
        JsonElement instance,
        JsonElement schema,
        string schemaPath,
        string instancePath,
        List<string> errors)
    {
        if (schema.ValueKind == JsonValueKind.True)
        {
            return;
        }

        if (schema.ValueKind == JsonValueKind.False)
        {
            errors.Add($"{instancePath}: false schema");
            return;
        }

        if (schema.TryGetProperty("$ref", out var reference))
        {
            var (targetPath, targetSchema) = ResolveReference(
                schemaPath,
                reference.GetString()!);
            ValidateNode(instance, targetSchema, targetPath, instancePath, errors);
        }

        if (schema.TryGetProperty("type", out var type)
            && !MatchesType(instance, type.GetString()!))
        {
            errors.Add($"{instancePath}: expected type {type.GetString()}");
            return;
        }

        if (schema.TryGetProperty("const", out var constant)
            && !JsonElement.DeepEquals(instance, constant))
        {
            errors.Add($"{instancePath}: const mismatch");
        }

        if (schema.TryGetProperty("enum", out var enumValues)
            && !enumValues.EnumerateArray().Any(value => JsonElement.DeepEquals(instance, value)))
        {
            errors.Add($"{instancePath}: enum mismatch");
        }

        ValidateCombinators(instance, schema, schemaPath, instancePath, errors);

        switch (instance.ValueKind)
        {
            case JsonValueKind.Object:
                ValidateObject(instance, schema, schemaPath, instancePath, errors);
                break;
            case JsonValueKind.Array:
                ValidateArray(instance, schema, schemaPath, instancePath, errors);
                break;
            case JsonValueKind.String:
                ValidateString(instance, schema, instancePath, errors);
                break;
            case JsonValueKind.Number:
                ValidateNumber(instance, schema, instancePath, errors);
                break;
        }
    }

    private void ValidateCombinators(
        JsonElement instance,
        JsonElement schema,
        string schemaPath,
        string instancePath,
        List<string> errors)
    {
        if (schema.TryGetProperty("allOf", out var allOf))
        {
            foreach (var child in allOf.EnumerateArray())
            {
                ValidateNode(instance, child, schemaPath, instancePath, errors);
            }
        }

        if (schema.TryGetProperty("anyOf", out var anyOf))
        {
            var matches = anyOf.EnumerateArray().Count(
                child => IsValid(instance, child, schemaPath, instancePath));

            if (matches == 0)
            {
                errors.Add($"{instancePath}: no anyOf branch matched");
            }
        }

        if (schema.TryGetProperty("oneOf", out var oneOf))
        {
            var matches = oneOf.EnumerateArray().Count(
                child => IsValid(instance, child, schemaPath, instancePath));

            if (matches != 1)
            {
                errors.Add($"{instancePath}: expected one oneOf branch, got {matches}");
            }
        }

        if (schema.TryGetProperty("not", out var not)
            && IsValid(instance, not, schemaPath, instancePath))
        {
            errors.Add($"{instancePath}: forbidden not schema matched");
        }

        if (schema.TryGetProperty("if", out var condition))
        {
            var conditionMatches = IsValid(instance, condition, schemaPath, instancePath);

            if (conditionMatches && schema.TryGetProperty("then", out var thenSchema))
            {
                ValidateNode(instance, thenSchema, schemaPath, instancePath, errors);
            }
            else if (!conditionMatches && schema.TryGetProperty("else", out var elseSchema))
            {
                ValidateNode(instance, elseSchema, schemaPath, instancePath, errors);
            }
        }
    }

    private void ValidateObject(
        JsonElement instance,
        JsonElement schema,
        string schemaPath,
        string instancePath,
        List<string> errors)
    {
        if (schema.TryGetProperty("required", out var required))
        {
            foreach (var requiredProperty in required.EnumerateArray())
            {
                var name = requiredProperty.GetString()!;

                if (!instance.TryGetProperty(name, out _))
                {
                    errors.Add($"{instancePath}.{name}: required");
                }
            }
        }

        schema.TryGetProperty("properties", out var properties);

        foreach (var property in instance.EnumerateObject())
        {
            if (properties.ValueKind == JsonValueKind.Object
                && properties.TryGetProperty(property.Name, out var propertySchema))
            {
                ValidateNode(
                    property.Value,
                    propertySchema,
                    schemaPath,
                    $"{instancePath}.{property.Name}",
                    errors);
                continue;
            }

            if (schema.TryGetProperty("additionalProperties", out var additional)
                && additional.ValueKind == JsonValueKind.False)
            {
                errors.Add($"{instancePath}.{property.Name}: additional property");
            }
        }
    }

    private void ValidateArray(
        JsonElement instance,
        JsonElement schema,
        string schemaPath,
        string instancePath,
        List<string> errors)
    {
        var items = instance.EnumerateArray().ToArray();

        if (schema.TryGetProperty("minItems", out var minItems)
            && items.Length < minItems.GetInt32())
        {
            errors.Add($"{instancePath}: minItems");
        }

        if (schema.TryGetProperty("uniqueItems", out var unique)
            && unique.GetBoolean())
        {
            var keys = items
                .Select(item => Convert.ToHexString(CanonicalJson.Canonicalize(item.GetRawTextBytes())))
                .ToArray();

            if (keys.Distinct(StringComparer.Ordinal).Count() != keys.Length)
            {
                errors.Add($"{instancePath}: uniqueItems");
            }
        }

        if (schema.TryGetProperty("items", out var itemSchema))
        {
            for (var index = 0; index < items.Length; index++)
            {
                ValidateNode(
                    items[index],
                    itemSchema,
                    schemaPath,
                    $"{instancePath}[{index}]",
                    errors);
            }
        }
    }

    private static void ValidateString(
        JsonElement instance,
        JsonElement schema,
        string instancePath,
        List<string> errors)
    {
        var value = instance.GetString()!;

        if (schema.TryGetProperty("minLength", out var minLength)
            && value.Length < minLength.GetInt32())
        {
            errors.Add($"{instancePath}: minLength");
        }

        if (schema.TryGetProperty("maxLength", out var maxLength)
            && value.Length > maxLength.GetInt32())
        {
            errors.Add($"{instancePath}: maxLength");
        }

        if (schema.TryGetProperty("x-maxUtf8Bytes", out var maxUtf8Bytes)
            && System.Text.Encoding.UTF8.GetByteCount(value) > maxUtf8Bytes.GetInt32())
        {
            errors.Add($"{instancePath}: x-maxUtf8Bytes");
        }

        if (schema.TryGetProperty("pattern", out var pattern)
            && !Regex.IsMatch(value, pattern.GetString()!, RegexOptions.CultureInvariant))
        {
            errors.Add($"{instancePath}: pattern");
        }

        if (schema.TryGetProperty("format", out var format)
            && format.GetString() == "uri"
            && !Uri.TryCreate(value, UriKind.Absolute, out _))
        {
            errors.Add($"{instancePath}: uri format");
        }
    }

    private static void ValidateNumber(
        JsonElement instance,
        JsonElement schema,
        string instancePath,
        List<string> errors)
    {
        if (!instance.TryGetInt64(out var value))
        {
            errors.Add($"{instancePath}: integer conversion");
            return;
        }

        if (schema.TryGetProperty("minimum", out var minimum)
            && value < minimum.GetInt64())
        {
            errors.Add($"{instancePath}: minimum");
        }

        if (schema.TryGetProperty("maximum", out var maximum)
            && value > maximum.GetInt64())
        {
            errors.Add($"{instancePath}: maximum");
        }
    }

    private bool IsValid(
        JsonElement instance,
        JsonElement schema,
        string schemaPath,
        string instancePath)
    {
        var localErrors = new List<string>();
        ValidateNode(instance, schema, schemaPath, instancePath, localErrors);
        return localErrors.Count == 0;
    }

    private (string Path, JsonElement Schema) ResolveReference(
        string currentSchemaPath,
        string reference)
    {
        var parts = reference.Split('#', 2);
        var targetPath = string.IsNullOrEmpty(parts[0])
            ? currentSchemaPath
            : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(currentSchemaPath)!, parts[0]));
        var element = Load(targetPath).RootElement;

        if (parts.Length == 2 && parts[1].Length > 0)
        {
            foreach (var encodedSegment in parts[1].TrimStart('/').Split('/'))
            {
                var segment = Uri.UnescapeDataString(encodedSegment)
                    .Replace("~1", "/", StringComparison.Ordinal)
                    .Replace("~0", "~", StringComparison.Ordinal);
                element = element.GetProperty(segment);
            }
        }

        return (targetPath, element);
    }

    private JsonDocument Load(string path)
    {
        if (!documents.TryGetValue(path, out var document))
        {
            document = JsonDocument.Parse(File.ReadAllBytes(path));
            documents.Add(path, document);
        }

        return document;
    }

    private static bool MatchesType(JsonElement instance, string type) => type switch
    {
        "object" => instance.ValueKind == JsonValueKind.Object,
        "array" => instance.ValueKind == JsonValueKind.Array,
        "string" => instance.ValueKind == JsonValueKind.String,
        "boolean" => instance.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "integer" => instance.ValueKind == JsonValueKind.Number
            && instance.TryGetInt64(out _)
            && instance.GetRawText().IndexOfAny(['.', 'e', 'E']) < 0,
        _ => throw new NotSupportedException($"Unsupported schema type '{type}'."),
    };
}

internal static class JsonElementExtensions
{
    public static byte[] GetRawTextBytes(this JsonElement element) =>
        System.Text.Encoding.UTF8.GetBytes(element.GetRawText());
}
