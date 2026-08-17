using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace RideBound.Benchmarking.Metrics;

public static class MetricEvidenceIdentity
{
    public static string CalculateSemantic(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> output,
        ReadOnlySpan<byte> observationIndex) =>
        Calculate(
            "RideBound.Wp6.SemanticMetricEvidence.v1",
            [
                new EvidenceFrame("input", input.ToArray()),
                new EvidenceFrame("output", output.ToArray()),
                new EvidenceFrame("observationIndex", observationIndex.ToArray()),
            ]);

    public static string CalculateResource(
        ReadOnlySpan<byte> canonicalRunRecord,
        ReadOnlySpan<byte> resourceSamples) =>
        Calculate(
            "RideBound.Wp6.ResourceMetricEvidence.v1",
            [
                new EvidenceFrame("runRecord", canonicalRunRecord.ToArray()),
                new EvidenceFrame("resourceSamples", resourceSamples.ToArray()),
            ]);

    private static string Calculate(string domain, IReadOnlyList<EvidenceFrame> frames)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes(domain + "\0"));
        Span<byte> tagLength = stackalloc byte[sizeof(ushort)];
        Span<byte> valueLength = stackalloc byte[sizeof(ulong)];

        foreach (var frame in frames)
        {
            var tag = Encoding.UTF8.GetBytes(frame.Tag);
            BinaryPrimitives.WriteUInt16BigEndian(tagLength, checked((ushort)tag.Length));
            hash.AppendData(tagLength);
            hash.AppendData(tag);
            BinaryPrimitives.WriteUInt64BigEndian(
                valueLength,
                checked((ulong)frame.Value.LongLength));
            hash.AppendData(valueLength);
            hash.AppendData(frame.Value);
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private sealed record EvidenceFrame(string Tag, byte[] Value);
}
