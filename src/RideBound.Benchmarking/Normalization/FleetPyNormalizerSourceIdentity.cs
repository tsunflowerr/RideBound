using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace RideBound.Benchmarking.Normalization;

public static class FleetPyNormalizerSourceIdentity
{
    private static readonly IReadOnlyList<string> RelativePaths =
    [
        "DirectedTravelGraph.cs",
        "FleetPyManhattanNormalizer.cs",
        "FleetPyNormalizationModels.cs",
        "FleetPyNormalizerSourceIdentity.cs",
        "StrictCsv.cs",
    ];

    public static string Calculate(string repositoryRoot)
    {
        var sourceRoot = Path.Combine(
            Path.GetFullPath(repositoryRoot),
            "src",
            "RideBound.Benchmarking",
            "Normalization");
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes("RideBound.Wp6.NormalizerSource.v1\0"));

        foreach (var relativePath in RelativePaths)
        {
            var path = Path.Combine(sourceRoot, relativePath);

            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "Normalizer source inventory is incomplete.",
                    path);
            }

            AppendFrame(hash, relativePath, File.ReadAllBytes(path));
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static void AppendFrame(
        IncrementalHash hash,
        string tag,
        ReadOnlySpan<byte> value)
    {
        var tagBytes = Encoding.UTF8.GetBytes(tag);
        Span<byte> tagLength = stackalloc byte[sizeof(ushort)];
        BinaryPrimitives.WriteUInt16BigEndian(tagLength, checked((ushort)tagBytes.Length));
        hash.AppendData(tagLength);
        hash.AppendData(tagBytes);
        Span<byte> valueLength = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64BigEndian(valueLength, checked((ulong)value.Length));
        hash.AppendData(valueLength);
        hash.AppendData(value);
    }
}
