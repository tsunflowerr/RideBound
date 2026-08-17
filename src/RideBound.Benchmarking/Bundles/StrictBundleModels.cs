using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RideBound.Benchmarking.Contracts;
using RideBound.Benchmarking.Execution;
using RideBound.Benchmarking.Storage;
using RideBound.Contracts.Serialization;

namespace RideBound.Benchmarking.Bundles;

public enum BundleVerificationStage
{
    PathSafety = 1,
    Layout = 2,
    BagItIntegrity = 3,
    LogicalManifest = 4,
    Provenance = 5,
    PlanConservation = 6,
    TranscriptProtocol = 7,
    TerminalLogs = 8,
    Metrics = 9,
    Claims = 10,
}

public sealed record BundleVerificationIssue(
    BundleVerificationStage Stage,
    string Code,
    string RelativePath,
    string SafeMessage);

public sealed record StrictBundleVerificationResult(
    bool IsValid,
    string? BundleHash,
    string? PlanHash,
    string? MetricSetHash,
    long PayloadFileCount,
    BundleVerificationIssue? Issue);

public sealed class BundleVerificationException(
    BundleVerificationStage stage,
    string code,
    string relativePath,
    string safeMessage,
    Exception? innerException = null) : Exception(safeMessage, innerException)
{
    public BundleVerificationStage Stage { get; } = stage;

    public string Code { get; } = code;

    public string RelativePath { get; } = relativePath;
}

public sealed record BundlePayloadSource(
    string SourcePath,
    string RelativePath,
    string MediaType,
    BundleArtifactRole Role,
    string ProducerActivityId,
    IReadOnlyList<string> SourceEntityIds);

public sealed record StrictBagItBundleRequest(
    string DestinationDirectory,
    string BundleId,
    EvidenceClass EvidenceClass,
    string ClaimProfileId,
    string MetricSetHash,
    string SourceInventorySha256,
    string RuntimeInventorySha256,
    string BaggingDate,
    IReadOnlyList<BundlePayloadSource> PayloadSources);

public sealed record StrictBagItBundleBuildResult(
    string BundleDirectory,
    string BundleHash,
    string PlanHash,
    string MetricSetHash,
    long PayloadFileCount,
    long PayloadLengthBytes);

public sealed record BundleSourceInventoryEntry(
    string ComponentId,
    string RelativePath,
    long LengthBytes,
    string Sha256);

public sealed record BundleSourceInventory(
    string SchemaVersion,
    string GitCommit,
    bool GitDirty,
    string GitStatusSha256,
    IReadOnlyList<BundleSourceInventoryEntry> Entries);

public sealed record BundleRuntimeInventory(
    string SchemaVersion,
    string InventorySha256,
    IReadOnlyList<ProcessArtifactInventoryEntry> Artifacts);

public sealed record BundleRunStorePlan(
    string SchemaVersion,
    string PlanHash,
    IReadOnlyList<string> DenominatorIds,
    IReadOnlyList<RunStoreIntent> Runs);

public sealed record BundleMachineProvenance(
    string SchemaVersion,
    string OsDescription,
    string OsArchitecture,
    string ProcessArchitecture,
    long LogicalProcessorCount,
    long TotalMemoryBytes,
    string DotnetRuntimeVersion,
    string DotnetFrameworkDescription,
    string FileSystemType,
    string PowerModeNote,
    string MachineNameSha256,
    string ContainerImageDigest);

public sealed record BundleReproducibilityBinding(
    string SchemaVersion,
    string PlanHash,
    string MetricSetHash,
    string ClaimProfileSha256,
    string SourceInventorySha256,
    string RuntimeInventorySha256,
    string HarnessSourceSha256,
    string OracleSourceSha256,
    string VerifierSourceSha256,
    string RunnerExecutableSha256,
    string RunnerAssemblySha256,
    string ContractsAssemblySha256,
    string MachineProvenanceSha256,
    string MetricRegistrySha256,
    string RunStorePlanSha256);

public sealed record BundlePackagingVerificationReport(
    string SchemaVersion,
    string VerifierId,
    string VerificationOrderId,
    string Status,
    string PlanHash,
    string MetricSetHash);

public sealed record ExternalBundleVerificationReport(
    string SchemaVersion,
    string BundleDirectoryName,
    string VerifierAssemblySha256,
    bool IsValid,
    string BundleHash,
    string PlanHash,
    string MetricSetHash,
    string FailedStage,
    string Code,
    string RelativePath,
    string SafeMessage);

public static class BundleEvidenceJson
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static byte[] Encode<T>(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return CanonicalJson.Canonicalize(JsonSerializer.SerializeToUtf8Bytes(value, Options));
    }

    public static T DecodeExact<T>(ReadOnlySpan<byte> bytes)
    {
        byte[] canonical;

        try
        {
            canonical = CanonicalJson.Canonicalize(bytes);
        }
        catch (CanonicalJsonException exception)
        {
            throw new InvalidDataException("Evidence JSON is not canonicalizable.", exception);
        }

        if (!bytes.SequenceEqual(canonical))
        {
            throw new InvalidDataException("Evidence JSON must use exact canonical bytes.");
        }

        try
        {
            return JsonSerializer.Deserialize<T>(canonical, Options)
                ?? throw new InvalidDataException("Evidence JSON must not decode to null.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Evidence JSON has an invalid strict shape.", exception);
        }
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectRequiredConstructorParameters = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
        };
        options.Converters.Add(
            new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false));
        return options;
    }
}

public static class BundleSourceInventoryIdentity
{
    public static string CalculateComponent(
        string componentId,
        IReadOnlyList<BundleSourceInventoryEntry> entries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(componentId);
        ArgumentNullException.ThrowIfNull(entries);
        var selected = entries
            .Where(value => string.Equals(value.ComponentId, componentId, StringComparison.Ordinal))
            .OrderBy(value => value.RelativePath, StringComparer.Ordinal)
            .ToArray();

        if (selected.Length == 0)
        {
            throw new ArgumentException("Source component inventory is empty.", nameof(entries));
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes("RideBound.Wp6.SourceComponent.v1\0"));
        AppendFrame(hash, "componentId", Encoding.UTF8.GetBytes(componentId));

        foreach (var entry in selected)
        {
            ValidateEntry(entry);
            AppendFrame(hash, "relativePath", Encoding.UTF8.GetBytes(entry.RelativePath));
            AppendFrame(
                hash,
                "lengthBytes",
                Encoding.ASCII.GetBytes(
                    entry.LengthBytes.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            AppendFrame(hash, "sha256", Convert.FromHexString(entry.Sha256));
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    internal static void ValidateInventory(BundleSourceInventory value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value.SchemaVersion != "1.0.0"
            || !IsSha(value.GitStatusSha256)
            || !IsGitCommit(value.GitCommit)
            || value.Entries.Count == 0)
        {
            throw new InvalidDataException("Source inventory header is invalid.");
        }

        var ordered = value.Entries
            .OrderBy(entry => entry.ComponentId, StringComparer.Ordinal)
            .ThenBy(entry => entry.RelativePath, StringComparer.Ordinal)
            .ToArray();

        if (!value.Entries.SequenceEqual(ordered)
            || ordered.Select(entry => entry.RelativePath)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count() != ordered.Length)
        {
            throw new InvalidDataException(
                "Source inventory entries must be sorted and case-insensitively unique.");
        }

        foreach (var entry in value.Entries)
        {
            ValidateEntry(entry);
        }

        foreach (var component in new[] { "harness", "oracle", "verifier" })
        {
            _ = CalculateComponent(component, value.Entries);
        }
    }

    private static void ValidateEntry(BundleSourceInventoryEntry entry)
    {
        if (!StrictBundlePath.IsArtifactId(entry.ComponentId)
            || !StrictBundlePath.IsSafeRelativePath(entry.RelativePath, requireDataPrefix: false)
            || entry.LengthBytes < 0
            || !IsSha(entry.Sha256))
        {
            throw new InvalidDataException("Source inventory entry is invalid.");
        }
    }

    private static void AppendFrame(
        IncrementalHash hash,
        string tag,
        ReadOnlySpan<byte> value)
    {
        var tagBytes = Encoding.UTF8.GetBytes(tag);
        Span<byte> tagLength = stackalloc byte[sizeof(ushort)];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(
            tagLength,
            checked((ushort)tagBytes.Length));
        hash.AppendData(tagLength);
        hash.AppendData(tagBytes);
        Span<byte> valueLength = stackalloc byte[sizeof(ulong)];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(
            valueLength,
            checked((ulong)value.Length));
        hash.AppendData(valueLength);
        hash.AppendData(value);
    }

    internal static bool IsSha(string value) =>
        value is not null
        && value.Length == 64
        && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool IsGitCommit(string value) =>
        value == "unborn"
        || value is not null
            && value.Length is 40 or 64
            && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}

public static class BundleMetricSetIdentity
{
    public static string Calculate(
        string planHash,
        string registryHash,
        ReadOnlySpan<byte> canonicalAllRunRows)
    {
        if (!BundleSourceInventoryIdentity.IsSha(planHash)
            || !BundleSourceInventoryIdentity.IsSha(registryHash)
            || canonicalAllRunRows.Length == 0
            || canonicalAllRunRows[^1] != (byte)'\n')
        {
            throw new ArgumentException("Bundle metric identity input is invalid.");
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes("RideBound.Wp6.BundleMetricSet.v1\0"));
        Append(hash, "planHash", Convert.FromHexString(planHash));
        Append(hash, "registryHash", Convert.FromHexString(registryHash));
        Append(hash, "canonicalAllRunRows", canonicalAllRunRows);
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static void Append(IncrementalHash hash, string tag, ReadOnlySpan<byte> value)
    {
        var tagBytes = Encoding.UTF8.GetBytes(tag);
        Span<byte> tagLength = stackalloc byte[sizeof(ushort)];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(
            tagLength,
            checked((ushort)tagBytes.Length));
        hash.AppendData(tagLength);
        hash.AppendData(tagBytes);
        Span<byte> valueLength = stackalloc byte[sizeof(ulong)];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(
            valueLength,
            checked((ulong)value.Length));
        hash.AppendData(valueLength);
        hash.AppendData(value);
    }
}

internal static class StrictBundlePath
{
    public static bool IsArtifactId(string value) =>
        value is not null
        && value.Length is >= 1 and <= 128
        && value[0] is >= 'a' and <= 'z' or >= '0' and <= '9'
        && value.All(character => character is >= 'a' and <= 'z'
            or >= '0' and <= '9'
            or '.' or '_' or '-');

    public static bool IsSafeRelativePath(string value, bool requireDataPrefix)
    {
        if (string.IsNullOrWhiteSpace(value)
            || Path.IsPathRooted(value)
            || value.Contains('\\')
            || value.Contains('%')
            || value.Any(char.IsControl)
            || value != value.Normalize(NormalizationForm.FormC)
            || requireDataPrefix && !value.StartsWith("data/", StringComparison.Ordinal))
        {
            return false;
        }

        var components = value.Split('/');
        return components.All(
            component => component.Length is > 0 and <= 255
                && component is not "." and not ".."
                && !component.EndsWith(' ')
                && !component.EndsWith('.')
                && component.IndexOfAny(['<', '>', ':', '"', '|', '?', '*']) < 0
                && !IsWindowsDeviceName(component));
    }

    private static bool IsWindowsDeviceName(string component)
    {
        var stem = component.Split('.')[0];
        return stem.Equals("con", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("prn", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("aux", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("nul", StringComparison.OrdinalIgnoreCase)
            || stem.Length == 4
                && (stem.StartsWith("com", StringComparison.OrdinalIgnoreCase)
                    || stem.StartsWith("lpt", StringComparison.OrdinalIgnoreCase))
                && stem[3] is >= '1' and <= '9';
    }
}
