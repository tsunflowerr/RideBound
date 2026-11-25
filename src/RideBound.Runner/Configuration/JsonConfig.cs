using System.Text.Json;
using RideBound.Domain.Common;

namespace RideBound.Runner.Configuration;

/// <summary>Shared strict JSON readers for the Runner configurations.</summary>
internal static class JsonConfig
{
    public static void RequireFields(JsonElement element, string path, params string[] fields) =>
        RequireObject(element, path, fields);

    public static string Text(JsonElement element, string name)
    {
        var value = element.GetProperty(name);
        return value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new InvalidDataException($"{name} must be a non-empty string.");
    }

    public static long PositiveInteger(JsonElement element, string name) =>
        Integer(element, name, 1, DomainLimits.MaxCanonicalInteger);

    public static long Integer(JsonElement element, string name, long minimum, long maximum)
    {
        var value = element.GetProperty(name);
        if (value.ValueKind == JsonValueKind.Number
            && value.TryGetInt64(out var parsed)
            && parsed >= minimum && parsed <= maximum)
        {
            return parsed;
        }

        throw new InvalidDataException($"{name} must be an integer in [{minimum}, {maximum}].");
    }

    public static bool OptionalBoolean(JsonElement element, string name) =>
        element.TryGetProperty(name, out var flag)
            ? flag.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => throw new InvalidDataException($"{name} must be a boolean."),
            }
            : false;

    public static long? OptionalPositiveInteger(
        JsonElement element, string name, long maximum = DomainLimits.MaxCanonicalInteger) =>
        element.TryGetProperty(name, out _) ? Integer(element, name, 1, maximum) : null;

    public static JsonElement.ArrayEnumerator Array(JsonElement element, string name)
    {
        var array = element.GetProperty(name);
        return array.ValueKind == JsonValueKind.Array
            ? array.EnumerateArray()
            : throw new InvalidDataException($"{name} must be an array.");
    }

    public static void RequireObject(
        JsonElement element, string path, string[] required, params string[] optional)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"{path} must be an object.");
        }

        foreach (var property in element.EnumerateObject())
        {
            if (!required.Contains(property.Name, StringComparer.Ordinal)
                && !optional.Contains(property.Name, StringComparer.Ordinal))
            {
                throw new InvalidDataException($"Unknown field '{property.Name}' in {path}.");
            }
        }

        foreach (var field in required)
        {
            if (!element.TryGetProperty(field, out _))
            {
                throw new InvalidDataException($"Missing field '{field}' in {path}.");
            }
        }
    }
}
