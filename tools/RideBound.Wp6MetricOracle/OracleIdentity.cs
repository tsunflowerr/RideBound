using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace RideBound.Wp6MetricOracle;

internal static class OracleIdentity
{
    public static string SemanticEvidence(
        byte[] input,
        byte[] output,
        byte[] observationIndex) =>
        HashFrames(
            "RideBound.Wp6.SemanticMetricEvidence.v1",
            [
                ("input", input),
                ("output", output),
                ("observationIndex", observationIndex),
            ]);

    public static string ResourceEvidence(byte[] runRecord, byte[] resourceSamples) =>
        HashFrames(
            "RideBound.Wp6.ResourceMetricEvidence.v1",
            [("runRecord", runRecord), ("resourceSamples", resourceSamples)]);

    public static string MetricSet(
        string runId,
        string registryHash,
        byte[] canonicalRows) =>
        HashFrames(
            "RideBound.Wp6.MetricSet.v1",
            [
                ("runId", Encoding.UTF8.GetBytes(runId)),
                ("registryHash", Convert.FromHexString(registryHash)),
                ("canonicalSortedRows", canonicalRows),
            ]);

    public static string File(byte[] bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static string HashFrames(
        string domain,
        IReadOnlyList<(string Tag, byte[] Value)> frames)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes(domain + "\0"));
        Span<byte> tagLength = stackalloc byte[sizeof(ushort)];
        Span<byte> valueLength = stackalloc byte[sizeof(ulong)];

        foreach (var (tagText, value) in frames)
        {
            var tag = Encoding.UTF8.GetBytes(tagText);
            BinaryPrimitives.WriteUInt16BigEndian(tagLength, checked((ushort)tag.Length));
            hash.AppendData(tagLength);
            hash.AppendData(tag);
            BinaryPrimitives.WriteUInt64BigEndian(
                valueLength,
                checked((ulong)value.LongLength));
            hash.AppendData(valueLength);
            hash.AppendData(value);
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
}
