using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace RideBound.Benchmarking.Contracts;

public sealed record BenchmarkSeedValue(string DigestHex, int NonNegativeInt32);

public static class BenchmarkSeed
{
    private const string Domain = "RideBound.Wp6.Seed.v1";
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    private static readonly IReadOnlySet<string> ComponentIds =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "scenario-row-selection",
            "fleet-placement",
            "arm-run-order",
            "adapter-rng",
            "simulator-rng",
            "solver-rng",
            "failure-injection",
            "bootstrap-resample",
        };

    public static BenchmarkSeedValue Derive(
        string masterSeedHex,
        string scenarioHash,
        long repeatIndex,
        string componentId,
        string stableItemId = "")
    {
        var key = DecodeSha(masterSeedHex, nameof(masterSeedHex));
        var scenario = DecodeSha(scenarioHash, nameof(scenarioHash));

        if (repeatIndex is < 0 or > 9_007_199_254_740_991)
        {
            throw new ArgumentOutOfRangeException(nameof(repeatIndex));
        }

        if (!ComponentIds.Contains(componentId))
        {
            throw new ArgumentException(
                "Component ID is not registered by WP6 seed hierarchy v1.",
                nameof(componentId));
        }

        ValidateStableItem(stableItemId);
        using var message = new MemoryStream();
        message.Write(Encoding.UTF8.GetBytes(Domain + "\0"));
        AppendFrame(message, "scenarioHash", scenario);
        AppendFrame(
            message,
            "repeatIndex",
            Encoding.ASCII.GetBytes(
                repeatIndex.ToString(CultureInfo.InvariantCulture)));
        AppendFrame(message, "componentId", Encoding.UTF8.GetBytes(componentId));
        AppendFrame(message, "stableItemId", Encoding.UTF8.GetBytes(stableItemId));
        var digest = HMACSHA256.HashData(key, message.ToArray());
        var int32 = (int)(BinaryPrimitives.ReadUInt32BigEndian(digest) & 0x7fff_ffffu);
        return new BenchmarkSeedValue(Convert.ToHexStringLower(digest), int32);
    }

    public static int ToNonNegativeInt32(string digestHex)
    {
        var digest = DecodeSha(digestHex, nameof(digestHex));
        return (int)(BinaryPrimitives.ReadUInt32BigEndian(digest) & 0x7fff_ffffu);
    }

    private static void AppendFrame(Stream stream, string tag, ReadOnlySpan<byte> value)
    {
        var tagBytes = Encoding.UTF8.GetBytes(tag);
        Span<byte> tagLength = stackalloc byte[sizeof(ushort)];
        BinaryPrimitives.WriteUInt16BigEndian(tagLength, checked((ushort)tagBytes.Length));
        stream.Write(tagLength);
        stream.Write(tagBytes);
        Span<byte> valueLength = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64BigEndian(valueLength, checked((ulong)value.Length));
        stream.Write(valueLength);
        stream.Write(value);
    }

    private static byte[] DecodeSha(string value, string parameterName)
    {
        if (value is not { Length: 64 }
            || value.Any(character => character is not (>= '0' and <= '9')
                and not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException(
                "Value must be exactly 64 lowercase hexadecimal characters.",
                parameterName);
        }

        return Convert.FromHexString(value);
    }

    private static void ValidateStableItem(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        byte[] bytes;

        try
        {
            bytes = StrictUtf8.GetBytes(value);
        }
        catch (EncoderFallbackException exception)
        {
            throw new ArgumentException(
                "Stable item ID must contain valid Unicode scalar values.",
                nameof(value),
                exception);
        }

        if (bytes.Length > 1_024)
        {
            throw new ArgumentException(
                "Stable item ID must be valid UTF-8 with at most 1024 bytes.",
                nameof(value));
        }
    }
}
