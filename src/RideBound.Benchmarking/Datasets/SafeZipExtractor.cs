using System.Buffers;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RideBound.Contracts.Serialization;

namespace RideBound.Benchmarking.Datasets;

public sealed record ZipExtractionLimits(
    int MaximumEntries = 10_000,
    long MaximumEntryUncompressedBytes = 400_000_000,
    long MaximumTotalUncompressedBytes = 2_000_000_000,
    long MaximumCompressionRatio = 250);

public sealed record ZipExtractionOptions(
    string ExtractionCacheRoot,
    string RepositoryRoot,
    ZipExtractionLimits Limits);

public sealed record ArchiveMember(
    string RelativePath,
    long LengthBytes,
    long CompressedLengthBytes,
    string Sha256);

public sealed record ArchiveInventory(
    string ArchiveSha256,
    string InventorySha256,
    long TotalUncompressedBytes,
    IReadOnlyList<ArchiveMember> Members);

public enum ArchiveExtractionStatus
{
    Succeeded,
    Failed,
}

public sealed record ArchiveExtractionIssue(
    string Code,
    string SafeMessage,
    string? EntryPath);

public sealed record ArchiveExtractionResult(
    ArchiveExtractionStatus Status,
    string? ExtractionRoot,
    ArchiveInventory? Inventory,
    bool ReusedExistingExtraction,
    ArchiveExtractionIssue? Issue)
{
    public static ArchiveExtractionResult Success(
        string extractionRoot,
        ArchiveInventory inventory,
        bool reused) =>
        new(
            ArchiveExtractionStatus.Succeeded,
            extractionRoot,
            inventory,
            reused,
            null);

    public static ArchiveExtractionResult Failed(
        string code,
        string safeMessage,
        string? entryPath = null) =>
        new(
            ArchiveExtractionStatus.Failed,
            null,
            null,
            false,
            new ArchiveExtractionIssue(code, safeMessage, entryPath));
}

public sealed class SafeZipExtractor
{
    private const int BufferBytes = 128 * 1024;

    public async Task<ArchiveExtractionResult> ExtractAsync(
        VerifiedDatasetArtifact artifact,
        ZipExtractionOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Limits);

        string cacheRoot;

        try
        {
            ValidateLimits(options.Limits);
            cacheRoot = ValidateExternalCacheRoot(
                options.ExtractionCacheRoot,
                options.RepositoryRoot);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException)
        {
            return ArchiveExtractionResult.Failed("archive.cache-invalid", exception.Message);
        }

        try
        {
            var archiveDigest = await VerifiedDatasetDownloader.HashFileAsync(
                artifact.FullPath,
                cancellationToken);

            if (archiveDigest.LengthBytes != artifact.LengthBytes
                || !string.Equals(archiveDigest.Md5, artifact.Md5, StringComparison.Ordinal)
                || !string.Equals(archiveDigest.Sha256, artifact.Sha256, StringComparison.Ordinal))
            {
                return ArchiveExtractionResult.Failed(
                    "archive.source-mismatch",
                    "Archive bytes changed after dataset verification.");
            }

            var inventoryResult = await BuildInventoryAsync(
                artifact.FullPath,
                artifact.Sha256,
                options.Limits,
                cancellationToken);

            if (inventoryResult.Issue is not null)
            {
                return ArchiveExtractionResult.Failed(
                    inventoryResult.Issue.Code,
                    inventoryResult.Issue.SafeMessage,
                    inventoryResult.Issue.EntryPath);
            }

            var inventory = inventoryResult.Inventory!;
            var finalRoot = Path.Combine(
                cacheRoot,
                "extracted",
                "sha256",
                artifact.Sha256[..2],
                artifact.Sha256);

            if (Directory.Exists(finalRoot))
            {
                var validExisting = await VerifyExistingExtractionAsync(
                    finalRoot,
                    inventory,
                    cancellationToken);

                return validExisting
                    ? ArchiveExtractionResult.Success(finalRoot, inventory, reused: true)
                    : ArchiveExtractionResult.Failed(
                        "archive.existing-extraction-mismatch",
                        "Existing extraction does not match the verified archive inventory.");
            }

            var stagingParent = Path.Combine(cacheRoot, ".staging");
            Directory.CreateDirectory(stagingParent);
            var stagingRoot = Path.Combine(
                stagingParent,
                "extract-" + RandomNumberGenerator.GetHexString(24, lowercase: true));
            Directory.CreateDirectory(stagingRoot);

            try
            {
                var extractionIssue = await ExtractIntoStagingAsync(
                    artifact.FullPath,
                    stagingRoot,
                    inventory,
                    cancellationToken);

                if (extractionIssue is not null)
                {
                    return ArchiveExtractionResult.Failed(
                        extractionIssue.Code,
                        extractionIssue.SafeMessage,
                        extractionIssue.EntryPath);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(finalRoot)!);

                try
                {
                    Directory.Move(stagingRoot, finalRoot);
                }
                catch (IOException) when (Directory.Exists(finalRoot))
                {
                    var validExisting = await VerifyExistingExtractionAsync(
                        finalRoot,
                        inventory,
                        cancellationToken);

                    if (!validExisting)
                    {
                        return ArchiveExtractionResult.Failed(
                            "archive.existing-extraction-mismatch",
                            "Concurrent extraction produced mismatching bytes.");
                    }

                    return ArchiveExtractionResult.Success(finalRoot, inventory, reused: true);
                }

                SetExtractedFilesReadOnly(finalRoot);
                return ArchiveExtractionResult.Success(finalRoot, inventory, reused: false);
            }
            finally
            {
                TryDeleteStagingDirectory(stagingRoot, stagingParent);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is InvalidDataException
                or IOException
                or UnauthorizedAccessException
                or CryptographicException)
        {
            return ArchiveExtractionResult.Failed("archive.invalid", exception.Message);
        }
    }

    private static async Task<InventoryBuildResult> BuildInventoryAsync(
        string archivePath,
        string archiveSha256,
        ZipExtractionLimits limits,
        CancellationToken cancellationToken)
    {
        await using var archiveStream = new FileStream(
            archivePath,
            new FileStreamOptions
            {
                Access = FileAccess.Read,
                Mode = FileMode.Open,
                Share = FileShare.Read,
                BufferSize = BufferBytes,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
            });
        using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: false);

        if (archive.Entries.Count > limits.MaximumEntries)
        {
            return InventoryBuildResult.Fail(
                "archive.entry-count-exceeded",
                "Archive entry count exceeds the configured cap.");
        }

        var exactPaths = new HashSet<string>(StringComparer.Ordinal);
        var foldedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pathKinds = new Dictionary<string, PathKindEntry>(
            StringComparer.OrdinalIgnoreCase);
        var members = new List<ArchiveMember>();
        long totalLength = 0;

        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pathResult = ValidateEntryPath(entry.FullName);

            if (pathResult.Issue is not null)
            {
                return InventoryBuildResult.Fail(
                    pathResult.Issue.Code,
                    pathResult.Issue.SafeMessage,
                    entry.FullName);
            }

            var normalizedPath = pathResult.NormalizedPath!;

            if (!exactPaths.Add(normalizedPath))
            {
                return InventoryBuildResult.Fail(
                    "archive.duplicate-path",
                    "Archive contains a duplicate normalized path.",
                    normalizedPath);
            }

            if (!foldedPaths.Add(normalizedPath))
            {
                return InventoryBuildResult.Fail(
                    "archive.case-collision",
                    "Archive contains paths that collide on a case-insensitive filesystem.",
                    normalizedPath);
            }

            var topologyIssue = RegisterPathTopology(
                pathKinds,
                normalizedPath,
                pathResult.IsDirectory);

            if (topologyIssue is not null)
            {
                return InventoryBuildResult.Fail(
                    topologyIssue.Code,
                    topologyIssue.SafeMessage,
                    normalizedPath);
            }

            if (IsLinkOrReparseEntry(entry))
            {
                return InventoryBuildResult.Fail(
                    "archive.link-entry",
                    "Archive symlink/reparse entries are forbidden.",
                    normalizedPath);
            }

            if (pathResult.IsDirectory)
            {
                continue;
            }

            if (entry.Length < 0
                || entry.CompressedLength < 0
                || entry.Length > limits.MaximumEntryUncompressedBytes)
            {
                return InventoryBuildResult.Fail(
                    "archive.entry-size-exceeded",
                    "Archive member exceeds the configured byte cap.",
                    normalizedPath);
            }

            totalLength = checked(totalLength + entry.Length);

            if (totalLength > limits.MaximumTotalUncompressedBytes)
            {
                return InventoryBuildResult.Fail(
                    "archive.total-size-exceeded",
                    "Archive total uncompressed size exceeds the configured cap.",
                    normalizedPath);
            }

            var compressedFloor = Math.Max(1, entry.CompressedLength);

            if (entry.Length > 0
                && compressedFloor <= long.MaxValue / limits.MaximumCompressionRatio
                && entry.Length > compressedFloor * limits.MaximumCompressionRatio)
            {
                return InventoryBuildResult.Fail(
                    "archive.compression-ratio-exceeded",
                    "Archive member compression ratio exceeds the configured cap.",
                    normalizedPath);
            }

            var digest = await HashEntryAsync(entry, limits, cancellationToken);

            if (digest.LengthBytes != entry.Length)
            {
                return InventoryBuildResult.Fail(
                    "archive.entry-length-mismatch",
                    "Decompressed member length differs from the ZIP directory.",
                    normalizedPath);
            }

            members.Add(
                new ArchiveMember(
                    normalizedPath,
                    digest.LengthBytes,
                    entry.CompressedLength,
                    digest.Sha256));
        }

        members.Sort(
            static (left, right) =>
                StringComparer.Ordinal.Compare(left.RelativePath, right.RelativePath));
        var canonicalInventory = CanonicalInventory(members);
        var inventory = new ArchiveInventory(
            archiveSha256,
            Convert.ToHexStringLower(SHA256.HashData(canonicalInventory)),
            totalLength,
            members);
        return InventoryBuildResult.Success(inventory);
    }

    private static async Task<ArchiveExtractionIssue?> ExtractIntoStagingAsync(
        string archivePath,
        string stagingRoot,
        ArchiveInventory inventory,
        CancellationToken cancellationToken)
    {
        await using var archiveStream = File.OpenRead(archivePath);
        using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: false);
        var membersByPath = inventory.Members.ToDictionary(
            member => member.RelativePath,
            StringComparer.Ordinal);

        foreach (var entry in archive.Entries)
        {
            var pathResult = ValidateEntryPath(entry.FullName);

            if (pathResult.IsDirectory)
            {
                continue;
            }

            var relativePath = pathResult.NormalizedPath!;
            var expected = membersByPath[relativePath];
            var targetPath = Path.GetFullPath(
                Path.Combine(stagingRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            var stagingPrefix = Path.GetFullPath(stagingRoot) + Path.DirectorySeparatorChar;

            if (!targetPath.StartsWith(stagingPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return new ArchiveExtractionIssue(
                    "archive.path-escape",
                    "Archive target escaped the staging root.",
                    relativePath);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using var input = entry.Open();
            await using var output = new FileStream(
                targetPath,
                new FileStreamOptions
                {
                    Access = FileAccess.Write,
                    Mode = FileMode.CreateNew,
                    Share = FileShare.None,
                    BufferSize = BufferBytes,
                    Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
                });
            var buffer = new byte[BufferBytes];
            long length = 0;

            while (true)
            {
                var read = await input.ReadAsync(buffer, cancellationToken);

                if (read == 0)
                {
                    break;
                }

                length = checked(length + read);

                if (length > expected.LengthBytes)
                {
                    return new ArchiveExtractionIssue(
                        "archive.entry-length-mismatch",
                        "Member expanded beyond its verified preflight length.",
                        relativePath);
                }

                sha.AppendData(buffer, 0, read);
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }

            await output.FlushAsync(cancellationToken);
            var actualSha = Convert.ToHexStringLower(sha.GetHashAndReset());

            if (length != expected.LengthBytes
                || !string.Equals(actualSha, expected.Sha256, StringComparison.Ordinal))
            {
                return new ArchiveExtractionIssue(
                    "archive.member-changed",
                    "Member bytes differ between preflight and extraction.",
                    relativePath);
            }
        }

        return null;
    }

    private static async Task<bool> VerifyExistingExtractionAsync(
        string extractionRoot,
        ArchiveInventory inventory,
        CancellationToken cancellationToken)
    {
        if ((File.GetAttributes(extractionRoot) & FileAttributes.ReparsePoint) != 0)
        {
            return false;
        }

        var files = Directory.GetFiles(extractionRoot, "*", SearchOption.AllDirectories)
            .Select(
                path => new
                {
                    FullPath = path,
                    RelativePath = Path.GetRelativePath(extractionRoot, path)
                        .Replace(Path.DirectorySeparatorChar, '/'),
                })
            .OrderBy(value => value.RelativePath, StringComparer.Ordinal)
            .ToArray();

        var expectedDirectories = inventory.Members
            .SelectMany(member => ParentPaths(member.RelativePath))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var actualDirectories = Directory
            .GetDirectories(extractionRoot, "*", SearchOption.AllDirectories)
            .Select(
                path => new
                {
                    FullPath = path,
                    RelativePath = Path.GetRelativePath(extractionRoot, path)
                        .Replace(Path.DirectorySeparatorChar, '/'),
                })
            .OrderBy(value => value.RelativePath, StringComparer.Ordinal)
            .ToArray();

        if (files.Length != inventory.Members.Count
            || !actualDirectories.Select(value => value.RelativePath)
                .SequenceEqual(expectedDirectories, StringComparer.Ordinal)
            || actualDirectories.Any(
                value => (File.GetAttributes(value.FullPath) & FileAttributes.ReparsePoint) != 0))
        {
            return false;
        }

        for (var index = 0; index < files.Length; index++)
        {
            var file = files[index];
            var expected = inventory.Members[index];

            if (!string.Equals(file.RelativePath, expected.RelativePath, StringComparison.Ordinal)
                || (File.GetAttributes(file.FullPath) & FileAttributes.ReparsePoint) != 0)
            {
                return false;
            }

            var digest = await VerifiedDatasetDownloader.HashFileAsync(
                file.FullPath,
                cancellationToken);

            if (digest.LengthBytes != expected.LengthBytes
                || !string.Equals(digest.Sha256, expected.Sha256, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static ArchiveExtractionIssue? RegisterPathTopology(
        IDictionary<string, PathKindEntry> pathKinds,
        string path,
        bool isDirectory)
    {
        var parents = ParentPaths(path).ToArray();

        foreach (var parent in parents)
        {
            var issue = RegisterOnePath(pathKinds, parent, isDirectory: true);

            if (issue is not null)
            {
                return issue;
            }
        }

        return RegisterOnePath(pathKinds, path, isDirectory);
    }

    private static ArchiveExtractionIssue? RegisterOnePath(
        IDictionary<string, PathKindEntry> pathKinds,
        string path,
        bool isDirectory)
    {
        if (!pathKinds.TryGetValue(path, out var existing))
        {
            pathKinds.Add(path, new PathKindEntry(path, isDirectory));
            return null;
        }

        if (!string.Equals(existing.CanonicalPath, path, StringComparison.Ordinal))
        {
            return new ArchiveExtractionIssue(
                "archive.case-collision",
                "Archive path prefixes collide on a case-insensitive filesystem.",
                path);
        }

        return existing.IsDirectory == isDirectory || existing.IsDirectory && isDirectory
            ? null
            : new ArchiveExtractionIssue(
                "archive.file-directory-collision",
                "Archive uses one path as both a file and directory.",
                path);
    }

    private static IEnumerable<string> ParentPaths(string path)
    {
        var offset = 0;

        while (true)
        {
            offset = path.IndexOf('/', offset);

            if (offset < 0)
            {
                yield break;
            }

            yield return path[..offset];
            offset++;
        }
    }

    private static async Task<EntryDigest> HashEntryAsync(
        ZipArchiveEntry entry,
        ZipExtractionLimits limits,
        CancellationToken cancellationToken)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using var stream = entry.Open();
        var buffer = new byte[BufferBytes];
        long length = 0;

        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);

            if (read == 0)
            {
                break;
            }

            length = checked(length + read);

            if (length > limits.MaximumEntryUncompressedBytes
                || length > limits.MaximumTotalUncompressedBytes)
            {
                throw new InvalidDataException(
                    "Archive member exceeded byte caps while being decompressed.");
            }

            sha.AppendData(buffer, 0, read);
        }

        return new EntryDigest(length, Convert.ToHexStringLower(sha.GetHashAndReset()));
    }

    private static EntryPathResult ValidateEntryPath(string fullName)
    {
        if (string.IsNullOrEmpty(fullName)
            || fullName.Contains('\\')
            || fullName.StartsWith("/", StringComparison.Ordinal)
            || fullName.Contains('\0')
            || fullName.Any(char.IsControl)
            || fullName.Contains(':')
            || !fullName.IsNormalized(NormalizationForm.FormC)
            || Encoding.UTF8.GetByteCount(fullName) > 4_096)
        {
            return EntryPathResult.Fail(
                "archive.invalid-path",
                "Archive path is absolute, ambiguous or contains forbidden characters.");
        }

        var isDirectory = fullName.EndsWith("/", StringComparison.Ordinal);
        var normalized = isDirectory ? fullName[..^1] : fullName;
        var segments = normalized.Split('/');

        if (string.IsNullOrEmpty(normalized)
            || segments.Any(
                segment => segment is "" or "." or ".."
                    || segment.EndsWith(' ')
                    || segment.EndsWith('.')
                    || Encoding.UTF8.GetByteCount(segment) > 255
                    || IsWindowsDeviceName(segment)))
        {
            return EntryPathResult.Fail(
                "archive.path-traversal",
                "Archive path contains traversal or an empty segment.");
        }

        return EntryPathResult.Success(normalized, isDirectory);
    }

    private static bool IsWindowsDeviceName(string segment)
    {
        var stem = segment.Split('.', 2)[0];
        return stem.Equals("con", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("prn", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("aux", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("nul", StringComparison.OrdinalIgnoreCase)
            || stem.Length == 4
                && (stem.StartsWith("com", StringComparison.OrdinalIgnoreCase)
                    || stem.StartsWith("lpt", StringComparison.OrdinalIgnoreCase))
                && stem[3] is >= '1' and <= '9';
    }

    private static bool IsLinkOrReparseEntry(ZipArchiveEntry entry)
    {
        var unixFileType = (entry.ExternalAttributes >> 16) & 0xF000;
        var windowsAttributes = (FileAttributes)(entry.ExternalAttributes & 0xFFFF);
        return unixFileType == 0xA000
            || (windowsAttributes & FileAttributes.ReparsePoint) != 0;
    }

    private static byte[] CanonicalInventory(IReadOnlyList<ArchiveMember> members)
    {
        var buffer = new ArrayBufferWriter<byte>();

        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartArray();

            foreach (var member in members)
            {
                writer.WriteStartObject();
                writer.WriteString("relativePath", member.RelativePath);
                writer.WriteNumber("lengthBytes", member.LengthBytes);
                writer.WriteString("sha256", member.Sha256);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        return CanonicalJson.Canonicalize(buffer.WrittenSpan);
    }

    private static string ValidateExternalCacheRoot(string cacheRoot, string repositoryRoot)
    {
        var fullCacheRoot = Path.GetFullPath(cacheRoot);
        var fullRepositoryRoot = Path.GetFullPath(repositoryRoot);
        var relative = Path.GetRelativePath(fullRepositoryRoot, fullCacheRoot);

        if (relative == "."
            || !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && relative != ".."
                && !Path.IsPathRooted(relative))
        {
            throw new ArgumentException("Extraction cache must be outside the repository.");
        }

        EnsureNoReparseInExistingAncestors(fullCacheRoot);
        Directory.CreateDirectory(fullCacheRoot);
        EnsureNoReparseInExistingAncestors(fullCacheRoot);
        return fullCacheRoot;
    }

    private static void EnsureNoReparseInExistingAncestors(string path)
    {
        for (var current = new DirectoryInfo(path); current is not null; current = current.Parent)
        {
            if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException($"Path traverses reparse point '{current.FullName}'.");
            }
        }
    }

    private static void ValidateLimits(ZipExtractionLimits limits)
    {
        if (limits.MaximumEntries <= 0
            || limits.MaximumEntryUncompressedBytes <= 0
            || limits.MaximumTotalUncompressedBytes <= 0
            || limits.MaximumCompressionRatio <= 0
            || limits.MaximumEntryUncompressedBytes > limits.MaximumTotalUncompressedBytes)
        {
            throw new ArgumentException("ZIP extraction limits are inconsistent.");
        }
    }

    private static void SetExtractedFilesReadOnly(string root)
    {
        foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, File.GetAttributes(file) | FileAttributes.ReadOnly);
        }
    }

    private static void TryDeleteStagingDirectory(string stagingRoot, string stagingParent)
    {
        if (!Directory.Exists(stagingRoot))
        {
            return;
        }

        var resolvedRoot = Path.GetFullPath(stagingRoot);
        var resolvedParent = Path.GetFullPath(stagingParent) + Path.DirectorySeparatorChar;

        if (!resolvedRoot.StartsWith(resolvedParent, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        foreach (var file in Directory.GetFiles(resolvedRoot, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        try
        {
            Directory.Delete(resolvedRoot, recursive: true);
        }
        catch (IOException)
        {
            // The caller already has the primary typed archive result.
        }
        catch (UnauthorizedAccessException)
        {
            // The caller already has the primary typed archive result.
        }
    }

    private sealed record EntryDigest(long LengthBytes, string Sha256);

    private sealed record PathKindEntry(string CanonicalPath, bool IsDirectory);

    private sealed record InventoryBuildResult(
        ArchiveInventory? Inventory,
        ArchiveExtractionIssue? Issue)
    {
        public static InventoryBuildResult Success(ArchiveInventory inventory) =>
            new(inventory, null);

        public static InventoryBuildResult Fail(
            string code,
            string message,
            string? path = null) =>
            new(null, new ArchiveExtractionIssue(code, message, path));
    }

    private sealed record EntryPathResult(
        string? NormalizedPath,
        bool IsDirectory,
        ArchiveExtractionIssue? Issue)
    {
        public static EntryPathResult Success(string path, bool directory) =>
            new(path, directory, null);

        public static EntryPathResult Fail(string code, string message) =>
            new(null, false, new ArchiveExtractionIssue(code, message, null));
    }
}
