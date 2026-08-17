using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace RideBound.Benchmarking.Execution;

public static class ProcessLaunchIdentity
{
    public static string Calculate(
        string executablePath,
        IReadOnlyList<string> arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(arguments);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes("RideBound.Wp6.LaunchCommand.v1\0"));
        AppendFrame(hash, "executableFileName", Path.GetFileName(executablePath));

        for (var index = 0; index < arguments.Count; index++)
        {
            AppendFrame(
                hash,
                $"argument[{index.ToString(System.Globalization.CultureInfo.InvariantCulture)}]",
                arguments[index]);
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static void AppendFrame(
        IncrementalHash hash,
        string tag,
        string value)
    {
        var tagBytes = Encoding.UTF8.GetBytes(tag);
        var valueBytes = Encoding.UTF8.GetBytes(value);
        Span<byte> tagLength = stackalloc byte[sizeof(ushort)];
        BinaryPrimitives.WriteUInt16BigEndian(tagLength, checked((ushort)tagBytes.Length));
        hash.AppendData(tagLength);
        hash.AppendData(tagBytes);
        Span<byte> valueLength = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64BigEndian(valueLength, checked((ulong)valueBytes.Length));
        hash.AppendData(valueLength);
        hash.AppendData(valueBytes);
    }
}
