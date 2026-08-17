using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using RideBound.Contracts.Serialization;

namespace RideBound.Benchmarking.Metrics;

public sealed record MechanicalMetricDefinition(
    string MetricId,
    string MetricVersion,
    string UnitId,
    string ValueKind,
    string SourceKind,
    string WindowScope,
    bool RawOracleRequired,
    string? DenominatorId = null);

public sealed record MetricExtensionRegistration(
    string MetricId,
    string MetricVersion,
    string UnitId,
    string DefinitionId,
    string DenominatorId,
    string RawEvidenceRole,
    string OracleSourceSha256,
    bool RawOracleRequired,
    bool UsesSelfReportedAggregate);

public sealed class MechanicalMetricRegistry
{
    public const string V1RegistryHash =
        "0747499608638ab085e1a89dfb6edf3d1ebff7d4bf267bd880ad1b6ccaa2f1a5";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private MechanicalMetricRegistry(
        string registryId,
        string registryVersion,
        string registryHash,
        IReadOnlyList<MechanicalMetricDefinition> definitions,
        byte[] canonicalBytes)
    {
        RegistryId = registryId;
        RegistryVersion = registryVersion;
        RegistryHash = registryHash;
        Definitions = definitions;
        CanonicalBytes = canonicalBytes;
    }

    public string RegistryId { get; }

    public string RegistryVersion { get; }

    public string RegistryHash { get; }

    public IReadOnlyList<MechanicalMetricDefinition> Definitions { get; }

    public byte[] CanonicalBytes { get; }

    public static MechanicalMetricRegistry Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var info = new FileInfo(Path.GetFullPath(path));

        if (!info.Exists || (info.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("Mechanical metric registry is missing or unsafe.");
        }

        var bytes = File.ReadAllBytes(info.FullName);
        var canonical = CanonicalJson.Canonicalize(bytes);

        // The checked-in registry is a single RFC 8785 canonical JSON document
        // with exactly one LF record terminator.  The terminator is part of the
        // immutable file identity, but not part of the JSON canonicalization.
        if (bytes.Length != canonical.Length + 1
            || bytes[^1] != (byte)'\n'
            || !bytes.AsSpan(0, canonical.Length).SequenceEqual(canonical))
        {
            throw new InvalidDataException(
                "Mechanical metric registry must be canonical JSON followed by one LF.");
        }

        var document = JsonSerializer.Deserialize<RegistryDocument>(bytes, JsonOptions)
            ?? throw new InvalidDataException("Mechanical metric registry is empty.");

        if (document.SchemaVersion != "1.0.0"
            || document.RegistryId != "wp6-mechanical-v1"
            || document.RegistryVersion != "1.0.0"
            || document.Definitions.Count != 36
            || !document.Definitions.SequenceEqual(
                document.Definitions.OrderBy(value => value.MetricId, StringComparer.Ordinal))
            || document.Definitions.Select(value => value.MetricId)
                .Distinct(StringComparer.Ordinal).Count() != document.Definitions.Count)
        {
            throw new InvalidDataException("Mechanical metric registry identity/order is invalid.");
        }

        foreach (var definition in document.Definitions)
        {
            ValidateDefinition(definition);
        }

        var registryHash = Convert.ToHexStringLower(SHA256.HashData(bytes));

        if (registryHash != V1RegistryHash)
        {
            throw new InvalidDataException(
                "Mechanical metric registry v1 bytes differ from the immutable published registry.");
        }

        return new MechanicalMetricRegistry(
            document.RegistryId,
            document.RegistryVersion,
            registryHash,
            document.Definitions,
            bytes);
    }

    public MechanicalMetricDefinition Require(string metricId)
    {
        var definition = Definitions.SingleOrDefault(
            value => string.Equals(value.MetricId, metricId, StringComparison.Ordinal));
        return definition
            ?? throw new InvalidOperationException("Metric is absent from the exact registry.");
    }

    public static void ValidateExtension(MetricExtensionRegistration extension)
    {
        ArgumentNullException.ThrowIfNull(extension);

        if (!IsOpaque(extension.MetricId)
            || !IsVersion(extension.MetricVersion)
            || !IsOpaque(extension.UnitId)
            || !IsOpaque(extension.DefinitionId)
            || !IsOpaque(extension.DenominatorId)
            || !IsOpaque(extension.RawEvidenceRole)
            || !IsSha256(extension.OracleSourceSha256)
            || !extension.RawOracleRequired
            || extension.UsesSelfReportedAggregate)
        {
            throw new ArgumentException(
                "Metric extension requires explicit semantics, raw evidence, and an independent oracle.",
                nameof(extension));
        }
    }

    private static void ValidateDefinition(MechanicalMetricDefinition value)
    {
        var sources = new[] { "rawTranscript", "rawSupervisor" };
        var scopes = new[] { "allWindows", "allOnly" };
        var kinds = new[] { "count", "sum", "maximum", "ratioPpm", "resource" };

        if (!IsOpaque(value.MetricId)
            || value.MetricVersion != "1.0.0"
            || !IsOpaque(value.UnitId)
            || !sources.Contains(value.SourceKind, StringComparer.Ordinal)
            || !scopes.Contains(value.WindowScope, StringComparer.Ordinal)
            || !kinds.Contains(value.ValueKind, StringComparer.Ordinal)
            || !value.RawOracleRequired
            || value.DenominatorId is not null && !IsOpaque(value.DenominatorId)
            || value.ValueKind == "ratioPpm" != (value.DenominatorId is not null)
            || value.SourceKind == "rawSupervisor" != (value.WindowScope == "allOnly"))
        {
            throw new InvalidDataException("Mechanical metric definition is invalid.");
        }
    }

    private static bool IsOpaque(string? value) =>
        value is { Length: >= 1 and <= 128 }
        && System.Text.Encoding.UTF8.GetByteCount(value) <= 128
        && !value.Any(char.IsControl);

    private static bool IsVersion(string value) =>
        System.Text.RegularExpressions.Regex.IsMatch(
            value,
            "^(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)$",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static bool IsSha256(string? value) =>
        value is { Length: 64 }
        && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private sealed record RegistryDocument(
        string SchemaVersion,
        string RegistryId,
        string RegistryVersion,
        IReadOnlyList<MechanicalMetricDefinition> Definitions);
}
