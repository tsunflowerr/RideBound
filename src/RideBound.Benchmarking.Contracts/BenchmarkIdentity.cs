using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using RideBound.Contracts.Serialization;

namespace RideBound.Benchmarking.Contracts;

public static class BenchmarkIdentity
{
    private const string ScenarioDomain = "RideBound.Wp6.Scenario.v1";
    private const string NormalizationReportDomain =
        "RideBound.Wp6.NormalizationReport.v1";
    private const string BenchmarkPlanDomain = "RideBound.Wp6.BenchmarkPlan.v1";
    private const string RunDomain = "RideBound.Wp6.Run.v1";
    private const string MetricSetDomain = "RideBound.Wp6.MetricSet.v1";
    private const string BundleDomain = "RideBound.Wp6.Bundle.v1";

    public static string CalculateScenario(ReadOnlySpan<byte> canonicalScenarioContent) =>
        CalculateCanonicalDocument(
            ScenarioDomain,
            "canonicalScenarioContent",
            canonicalScenarioContent);

    public static string CalculateNormalizationReport(ReadOnlySpan<byte> canonicalReport) =>
        CalculateCanonicalDocument(
            NormalizationReportDomain,
            "canonicalReport",
            canonicalReport);

    public static string CalculateBenchmarkPlan(ReadOnlySpan<byte> canonicalPlanContent) =>
        CalculateCanonicalDocument(
            BenchmarkPlanDomain,
            "canonicalPlanContent",
            canonicalPlanContent);

    public static string CalculateRun(
        string planHash,
        string scenarioHash,
        string armId,
        long repeatIndex,
        long attemptIndex)
    {
        var planHashBytes = DecodeSha(planHash, nameof(planHash));
        var scenarioHashBytes = DecodeSha(scenarioHash, nameof(scenarioHash));
        ValidateArtifactId(armId, nameof(armId));
        ValidateNonNegative(repeatIndex, nameof(repeatIndex));
        ValidateNonNegative(attemptIndex, nameof(attemptIndex));

        return Calculate(
            RunDomain,
            [
                new HashFrame("planHash", planHashBytes),
                new HashFrame("scenarioHash", scenarioHashBytes),
                new HashFrame("armId", Encoding.UTF8.GetBytes(armId)),
                new HashFrame("repeatIndex", CanonicalIntegerBytes(repeatIndex)),
                new HashFrame("attemptIndex", CanonicalIntegerBytes(attemptIndex)),
            ]);
    }

    public static string CalculateMetricSet(
        string runId,
        string registryHash,
        ReadOnlySpan<byte> canonicalSortedRows)
    {
        ValidateArtifactId(runId, nameof(runId));
        var registryHashBytes = DecodeSha(registryHash, nameof(registryHash));
        RequireCanonical(canonicalSortedRows, nameof(canonicalSortedRows));

        return Calculate(
            MetricSetDomain,
            [
                new HashFrame("runId", Encoding.UTF8.GetBytes(runId)),
                new HashFrame("registryHash", registryHashBytes),
                new HashFrame("canonicalSortedRows", canonicalSortedRows.ToArray()),
            ]);
    }

    public static string CalculateBundle(ReadOnlySpan<byte> canonicalBundleManifest) =>
        CalculateCanonicalDocument(
            BundleDomain,
            "canonicalBundleManifest",
            canonicalBundleManifest);

    public static string CalculateFileSha256(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static string CalculateCanonicalDocument(
        string domain,
        string tag,
        ReadOnlySpan<byte> canonicalDocument)
    {
        RequireCanonical(canonicalDocument, nameof(canonicalDocument));
        return Calculate(domain, [new HashFrame(tag, canonicalDocument.ToArray())]);
    }

    private static string Calculate(string domain, IReadOnlyList<HashFrame> frames)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes(domain + "\0"));

        foreach (var frame in frames)
        {
            AppendFrame(hash, frame.Tag, frame.Value);
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static void AppendFrame(
        IncrementalHash hash,
        string tag,
        ReadOnlySpan<byte> value)
    {
        var tagBytes = Encoding.UTF8.GetBytes(tag);

        if (tagBytes.Length > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(tag), "Hash frame tag is too long.");
        }

        Span<byte> tagLength = stackalloc byte[sizeof(ushort)];
        BinaryPrimitives.WriteUInt16BigEndian(tagLength, (ushort)tagBytes.Length);
        hash.AppendData(tagLength);
        hash.AppendData(tagBytes);

        Span<byte> valueLength = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64BigEndian(valueLength, (ulong)value.Length);
        hash.AppendData(valueLength);
        hash.AppendData(value);
    }

    private static void RequireCanonical(ReadOnlySpan<byte> value, string parameterName)
    {
        var canonical = CanonicalJson.Canonicalize(value);

        if (!value.SequenceEqual(canonical))
        {
            throw new ArgumentException(
                "Identity input must already be RideBound Canonical JSON v1 bytes.",
                parameterName);
        }
    }

    private static byte[] DecodeSha(string value, string parameterName)
    {
        if (value is null
            || value.Length != 64
            || value.Any(character => character is not (>= '0' and <= '9')
                and not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException(
                "Value must be exactly 64 lowercase hexadecimal characters.",
                parameterName);
        }

        return Convert.FromHexString(value);
    }

    private static void ValidateArtifactId(string value, string parameterName)
    {
        if (value is null
            || value.Length is < 1 or > 128
            || value[0] is not (>= 'a' and <= 'z') and not (>= '0' and <= '9')
            || value.Any(
                character => character is not (>= 'a' and <= 'z')
                    and not (>= '0' and <= '9')
                    and not '.' and not '_' and not '-'))
        {
            throw new ArgumentException("Value is not a canonical artifact ID.", parameterName);
        }
    }

    private static void ValidateNonNegative(long value, string parameterName)
    {
        if (value is < 0 or > 9_007_199_254_740_991)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static byte[] CanonicalIntegerBytes(long value) =>
        Encoding.ASCII.GetBytes(value.ToString(CultureInfo.InvariantCulture));

    private sealed record HashFrame(string Tag, byte[] Value);
}
