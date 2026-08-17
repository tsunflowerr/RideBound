using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace RideBound.Benchmarking.Execution;

public sealed record ProcessArtifactInventoryEntry(
    string Role,
    string FileName,
    long LengthBytes,
    string Sha256);

public sealed record ProcessArtifactInventory(
    string InventorySha256,
    IReadOnlyList<ProcessArtifactInventoryEntry> Artifacts);

public static class ProcessArtifactIdentity
{
    public static string Calculate(IReadOnlyList<PinnedProcessFile> files) =>
        Capture(files).InventorySha256;

    public static string Calculate(
        IReadOnlyList<ProcessArtifactInventoryEntry> artifacts)
    {
        ArgumentNullException.ThrowIfNull(artifacts);

        if (artifacts.Count == 0
            || artifacts.Any(
                value => value is null
                    || !IsRole(value.Role)
                    || !IsSafeFileName(value.FileName)
                    || value.LengthBytes < 0
                    || !IsLowerSha256(value.Sha256))
            || artifacts.Select(value => value.Role)
                .Distinct(StringComparer.Ordinal).Count() != artifacts.Count)
        {
            throw new ArgumentException(
                "Artifact inventory entries must be non-empty, safe, and role-unique.",
                nameof(artifacts));
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes("RideBound.Wp6.RuntimeInventory.v1\0"));

        foreach (var artifact in artifacts.OrderBy(value => value.Role, StringComparer.Ordinal))
        {
            AppendFrame(hash, "role", Encoding.UTF8.GetBytes(artifact.Role));
            AppendFrame(hash, "fileName", Encoding.UTF8.GetBytes(artifact.FileName));
            AppendFrame(
                hash,
                "lengthBytes",
                Encoding.ASCII.GetBytes(
                    artifact.LengthBytes.ToString(
                        System.Globalization.CultureInfo.InvariantCulture)));
            AppendFrame(hash, "sha256", Convert.FromHexString(artifact.Sha256));
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    public static ProcessArtifactInventory Capture(IReadOnlyList<PinnedProcessFile> files)
        => Capture(files, verifyExpectedHashes: true);

    internal static ProcessArtifactInventory Observe(
        IReadOnlyList<PinnedProcessFile> files) =>
        Capture(files, verifyExpectedHashes: false);

    private static ProcessArtifactInventory Capture(
        IReadOnlyList<PinnedProcessFile> files,
        bool verifyExpectedHashes)
    {
        ArgumentNullException.ThrowIfNull(files);

        if (files.Count == 0
            || files.Any(
                value => value is null
                    || !IsRole(value.Role)
                    || !Path.IsPathRooted(value.FullPath)
                    || !IsLowerSha256(value.ExpectedSha256))
            || files.Select(value => value.Role)
                .Distinct(StringComparer.Ordinal).Count() != files.Count)
        {
            throw new ArgumentException(
                "Pinned artifact roles, paths, and SHA-256 values must be exact and unique.",
                nameof(files));
        }

        var entries = new List<ProcessArtifactInventoryEntry>(files.Count);

        foreach (var file in files.OrderBy(value => value.Role, StringComparer.Ordinal))
        {
            var fullPath = Path.GetFullPath(file.FullPath);
            var info = new FileInfo(fullPath);

            if (!info.Exists || (info.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException(
                    $"Pinned runtime role '{file.Role}' is missing or is a reparse point.");
            }

            using var stream = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81_920,
                FileOptions.SequentialScan);
            var length = stream.Length;
            var actualSha256 = Convert.ToHexStringLower(SHA256.HashData(stream));

            if (stream.Position != length || stream.Length != length)
            {
                throw new IOException(
                    $"Pinned runtime role '{file.Role}' changed while it was hashed.");
            }

            if (verifyExpectedHashes
                && !string.Equals(
                actualSha256,
                file.ExpectedSha256,
                StringComparison.Ordinal))
            {
                throw new IOException(
                    $"Pinned runtime role '{file.Role}' does not match its expected SHA-256.");
            }

            entries.Add(
                new ProcessArtifactInventoryEntry(
                    file.Role,
                    info.Name,
                    length,
                    actualSha256));
        }

        return new ProcessArtifactInventory(
            Calculate(entries),
            entries);
    }

    public static PinnedProcessFile Pin(string role, string fullPath)
    {
        if (!IsRole(role))
        {
            throw new ArgumentException(
                "Pinned artifact role must be an exact portable artifact identifier.",
                nameof(role));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        var path = Path.GetFullPath(fullPath);
        var info = new FileInfo(path);

        if (!info.Exists || (info.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("Pinned artifact is missing or is a reparse point.");
        }

        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81_920,
            FileOptions.SequentialScan);
        return new PinnedProcessFile(
            role,
            path,
            Convert.ToHexStringLower(SHA256.HashData(stream)));
    }

    private static bool IsRole(string? value) =>
        value is { Length: >= 1 and <= 128 }
        && value[0] is >= 'a' and <= 'z' or >= '0' and <= '9'
        && value.All(
            character => character is >= 'a' and <= 'z'
                or >= '0' and <= '9'
                or '.' or '_' or '-');

    private static bool IsSafeFileName(string? value) =>
        value is { Length: >= 1 and <= 255 }
        && value is not "." and not ".."
        && !value.Any(char.IsControl)
        && Path.GetFileName(value) == value;

    private static bool IsLowerSha256(string? value) =>
        value is { Length: 64 }
        && value.All(
            character => character is >= '0' and <= '9'
                or >= 'a' and <= 'f');

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
