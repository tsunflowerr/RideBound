using System.IO.Compression;
using System.Security.Cryptography;
using RideBound.Benchmarking.Contracts;
using RideBound.Benchmarking.Datasets;

namespace RideBound.Benchmarking.Tests;

public sealed class SafeZipExtractorTests
{
    [Fact]
    public async Task Good_archive_has_sorted_content_inventory_atomic_extraction_and_idempotent_reuse()
    {
        using var temp = new TestDirectory();
        var archivePath = CreateZip(
            temp,
            [
                new ZipMemberSpec("root/z.txt", "z"u8.ToArray()),
                new ZipMemberSpec("root/a.txt", "alpha"u8.ToArray()),
            ]);
        var artifact = CreateArtifact(archivePath);
        var extractor = new SafeZipExtractor();
        var options = Options(temp);

        var first = await extractor.ExtractAsync(artifact, options);
        var second = await extractor.ExtractAsync(artifact, options);

        Assert.Equal(ArchiveExtractionStatus.Succeeded, first.Status);
        Assert.Equal(ArchiveExtractionStatus.Succeeded, second.Status);
        Assert.False(first.ReusedExistingExtraction);
        Assert.True(second.ReusedExistingExtraction);
        Assert.Equal(first.Inventory!.InventorySha256, second.Inventory!.InventorySha256);
        Assert.Equal(
            first.Inventory.Members.Select(
                value => (value.RelativePath, value.LengthBytes, value.Sha256)),
            second.Inventory.Members.Select(
                value => (value.RelativePath, value.LengthBytes, value.Sha256)));
        Assert.Equal(
            ["root/a.txt", "root/z.txt"],
            first.Inventory!.Members.Select(member => member.RelativePath).ToArray());
        Assert.Equal(
            "alpha",
            await File.ReadAllTextAsync(Path.Combine(first.ExtractionRoot!, "root", "a.txt")));
        Assert.All(
            Directory.GetFiles(first.ExtractionRoot!, "*", SearchOption.AllDirectories),
            file => Assert.True(
                (File.GetAttributes(file) & FileAttributes.ReadOnly) != 0));
        Assert.False(IsUnder(temp.RepositoryRoot, first.ExtractionRoot!));
    }

    public static TheoryData<string, string> InvalidPaths => new()
    {
        { "../escaped.txt", "archive.path-traversal" },
        { "/absolute.txt", "archive.invalid-path" },
        { "C:/drive.txt", "archive.invalid-path" },
        { "a\\backslash.txt", "archive.invalid-path" },
        { "a//empty.txt", "archive.path-traversal" },
        { "a/./dot.txt", "archive.path-traversal" },
        { "a/../parent.txt", "archive.path-traversal" },
        { "NUL.txt", "archive.path-traversal" },
        { "trailing./file.txt", "archive.path-traversal" },
        { "e\u0301.txt", "archive.invalid-path" },
    };

    [Theory]
    [MemberData(nameof(InvalidPaths))]
    public async Task Unsafe_path_is_rejected_before_any_member_write(
        string unsafePath,
        string expectedCode)
    {
        using var temp = new TestDirectory();
        var archivePath = CreateZip(
            temp,
            [new ZipMemberSpec(unsafePath, "attack"u8.ToArray())]);
        var artifact = CreateArtifact(archivePath);

        var result = await new SafeZipExtractor().ExtractAsync(artifact, Options(temp));

        Assert.Equal(ArchiveExtractionStatus.Failed, result.Status);
        Assert.Equal(expectedCode, result.Issue!.Code);
        Assert.Empty(
            Directory.Exists(temp.ExtractionRoot)
                ? Directory.GetFiles(temp.ExtractionRoot, "*", SearchOption.AllDirectories)
                : []);
        Assert.False(File.Exists(Path.Combine(temp.Root, "escaped.txt")));
    }

    [Fact]
    public async Task Duplicate_and_case_colliding_paths_are_rejected()
    {
        using var firstTemp = new TestDirectory();
        var duplicate = CreateZip(
            firstTemp,
            [
                new ZipMemberSpec("a.txt", "one"u8.ToArray()),
                new ZipMemberSpec("a.txt", "two"u8.ToArray()),
            ]);
        var duplicateResult = await new SafeZipExtractor().ExtractAsync(
            CreateArtifact(duplicate),
            Options(firstTemp));
        Assert.Equal("archive.duplicate-path", duplicateResult.Issue!.Code);

        using var secondTemp = new TestDirectory();
        var collision = CreateZip(
            secondTemp,
            [
                new ZipMemberSpec("A.txt", "one"u8.ToArray()),
                new ZipMemberSpec("a.txt", "two"u8.ToArray()),
            ]);
        var collisionResult = await new SafeZipExtractor().ExtractAsync(
            CreateArtifact(collision),
            Options(secondTemp));
        Assert.Equal("archive.case-collision", collisionResult.Issue!.Code);

        using var thirdTemp = new TestDirectory();
        var prefixCollision = CreateZip(
            thirdTemp,
            [
                new ZipMemberSpec("a", "file"u8.ToArray()),
                new ZipMemberSpec("a/b.txt", "child"u8.ToArray()),
            ]);
        var prefixResult = await new SafeZipExtractor().ExtractAsync(
            CreateArtifact(prefixCollision),
            Options(thirdTemp));
        Assert.Equal("archive.file-directory-collision", prefixResult.Issue!.Code);
    }

    [Fact]
    public async Task Symlink_or_reparse_metadata_is_rejected()
    {
        using var temp = new TestDirectory();
        var symlinkAttributes = unchecked((int)0xA1FF0000);
        var archivePath = CreateZip(
            temp,
            [new ZipMemberSpec("link", "target"u8.ToArray(), symlinkAttributes)]);

        var result = await new SafeZipExtractor().ExtractAsync(
            CreateArtifact(archivePath),
            Options(temp));

        Assert.Equal(ArchiveExtractionStatus.Failed, result.Status);
        Assert.Equal("archive.link-entry", result.Issue!.Code);
    }

    [Fact]
    public async Task Entry_total_count_and_compression_ratio_bombs_fail_preflight()
    {
        using var entryTemp = new TestDirectory();
        var entryArchive = CreateZip(
            entryTemp,
            [new ZipMemberSpec("big.bin", new byte[64])]);
        var entryResult = await new SafeZipExtractor().ExtractAsync(
            CreateArtifact(entryArchive),
            Options(entryTemp, new ZipExtractionLimits(10, 32, 128, 250)));
        Assert.Equal("archive.entry-size-exceeded", entryResult.Issue!.Code);

        using var totalTemp = new TestDirectory();
        var totalArchive = CreateZip(
            totalTemp,
            [
                new ZipMemberSpec("a.bin", RandomNumberGenerator.GetBytes(30)),
                new ZipMemberSpec("b.bin", RandomNumberGenerator.GetBytes(30)),
            ]);
        var totalResult = await new SafeZipExtractor().ExtractAsync(
            CreateArtifact(totalArchive),
            Options(totalTemp, new ZipExtractionLimits(10, 40, 50, 250)));
        Assert.Equal("archive.total-size-exceeded", totalResult.Issue!.Code);

        using var countTemp = new TestDirectory();
        var countArchive = CreateZip(
            countTemp,
            [
                new ZipMemberSpec("a", [1]),
                new ZipMemberSpec("b", [2]),
                new ZipMemberSpec("c", [3]),
            ]);
        var countResult = await new SafeZipExtractor().ExtractAsync(
            CreateArtifact(countArchive),
            Options(countTemp, new ZipExtractionLimits(2, 100, 200, 250)));
        Assert.Equal("archive.entry-count-exceeded", countResult.Issue!.Code);

        using var ratioTemp = new TestDirectory();
        var ratioArchive = CreateZip(
            ratioTemp,
            [new ZipMemberSpec("zeros.bin", new byte[20_000])]);
        var ratioResult = await new SafeZipExtractor().ExtractAsync(
            CreateArtifact(ratioArchive),
            Options(ratioTemp, new ZipExtractionLimits(10, 30_000, 30_000, 2)));
        Assert.Equal("archive.compression-ratio-exceeded", ratioResult.Issue!.Code);
    }

    [Fact]
    public async Task Changed_archive_and_tampered_existing_extraction_fail_closed()
    {
        using var sourceTemp = new TestDirectory();
        var sourceArchive = CreateZip(
            sourceTemp,
            [new ZipMemberSpec("a.txt", "alpha"u8.ToArray())]);
        var staleArtifact = CreateArtifact(sourceArchive);
        await File.AppendAllTextAsync(sourceArchive, "tamper");
        var changed = await new SafeZipExtractor().ExtractAsync(
            staleArtifact,
            Options(sourceTemp));
        Assert.Equal("archive.source-mismatch", changed.Issue!.Code);

        using var extractionTemp = new TestDirectory();
        var archive = CreateZip(
            extractionTemp,
            [new ZipMemberSpec("a.txt", "alpha"u8.ToArray())]);
        var artifact = CreateArtifact(archive);
        var extractor = new SafeZipExtractor();
        var first = await extractor.ExtractAsync(artifact, Options(extractionTemp));
        var extractedFile = Path.Combine(first.ExtractionRoot!, "a.txt");
        File.SetAttributes(extractedFile, FileAttributes.Normal);
        await File.WriteAllTextAsync(extractedFile, "tampered");

        var tampered = await extractor.ExtractAsync(artifact, Options(extractionTemp));
        Assert.Equal("archive.existing-extraction-mismatch", tampered.Issue!.Code);

        await File.WriteAllTextAsync(extractedFile, "alpha");
        File.SetAttributes(extractedFile, FileAttributes.ReadOnly);
        Directory.CreateDirectory(Path.Combine(first.ExtractionRoot!, "unexpected-empty"));
        var extraDirectory = await extractor.ExtractAsync(artifact, Options(extractionTemp));
        Assert.Equal("archive.existing-extraction-mismatch", extraDirectory.Issue!.Code);
    }

    [Fact]
    public async Task Inventory_identity_is_independent_of_zip_entry_order_and_compression_bytes()
    {
        using var firstTemp = new TestDirectory();
        var firstArchive = CreateZip(
            firstTemp,
            [
                new ZipMemberSpec("b.txt", "beta"u8.ToArray()),
                new ZipMemberSpec("a.txt", "alpha"u8.ToArray()),
            ],
            CompressionLevel.Optimal);
        var first = await new SafeZipExtractor().ExtractAsync(
            CreateArtifact(firstArchive),
            Options(firstTemp));

        using var secondTemp = new TestDirectory();
        var secondArchive = CreateZip(
            secondTemp,
            [
                new ZipMemberSpec("a.txt", "alpha"u8.ToArray()),
                new ZipMemberSpec("b.txt", "beta"u8.ToArray()),
            ],
            CompressionLevel.NoCompression);
        var second = await new SafeZipExtractor().ExtractAsync(
            CreateArtifact(secondArchive),
            Options(secondTemp));

        Assert.Equal(ArchiveExtractionStatus.Succeeded, first.Status);
        Assert.Equal(ArchiveExtractionStatus.Succeeded, second.Status);
        Assert.NotEqual(first.Inventory!.ArchiveSha256, second.Inventory!.ArchiveSha256);
        Assert.Equal(first.Inventory.InventorySha256, second.Inventory.InventorySha256);
        Assert.Equal(
            first.Inventory.Members.Select(value => (value.RelativePath, value.LengthBytes, value.Sha256)),
            second.Inventory.Members.Select(value => (value.RelativePath, value.LengthBytes, value.Sha256)));
    }

    private static ZipExtractionOptions Options(
        TestDirectory temp,
        ZipExtractionLimits? limits = null) =>
        new(
            temp.ExtractionRoot,
            temp.RepositoryRoot,
            limits ?? new ZipExtractionLimits(100, 1_000_000, 2_000_000, 250));

    private static string CreateZip(
        TestDirectory temp,
        IReadOnlyList<ZipMemberSpec> members,
        CompressionLevel compressionLevel = CompressionLevel.Optimal)
    {
        var path = Path.Combine(temp.Root, $"archive-{Guid.NewGuid():N}.zip");

        using var stream = File.Create(path);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);

        foreach (var member in members)
        {
            var entry = archive.CreateEntry(member.Path, compressionLevel);

            if (member.ExternalAttributes is int attributes)
            {
                entry.ExternalAttributes = attributes;
            }

            using var entryStream = entry.Open();
            entryStream.Write(member.Bytes);
        }

        return path;
    }

    private static VerifiedDatasetArtifact CreateArtifact(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var md5 = Convert.ToHexStringLower(MD5.HashData(bytes));
        var registration = DatasetSourceRegistry.FleetPyManhattanV1;
        var descriptor = new DatasetDescriptor(
            BenchmarkContractVersions.V1,
            registration.DatasetId,
            registration.DatasetKind,
            registration.Title,
            registration.ReleaseVersion,
            registration.PersistentUri.AbsoluteUri,
            registration.DownloadUri.AbsoluteUri,
            "2026-08-09T00:00:00Z",
            registration.PublisherArtifactName,
            registration.LicenseSpdx,
            registration.LicenseUri.AbsoluteUri,
            registration.Citation,
            registration.Composition,
            registration.CollectionLimit,
            registration.AllowedUse,
            registration.ForbiddenClaim,
            registration.DirectIdentifierStatus,
            registration.LocationPrecisionClass,
            registration.RetentionClass,
            registration.MaintenanceNote,
            bytes.LongLength,
            md5,
            sha);
        return new VerifiedDatasetArtifact(
            registration.DatasetId,
            path,
            $"sha256:{sha}",
            bytes.LongLength,
            md5,
            sha,
            descriptor,
            ReusedExistingBytes: false);
    }

    private static bool IsUnder(string root, string path)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(path));
        return relative == "."
            || !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && relative != ".."
                && !Path.IsPathRooted(relative);
    }

    private sealed record ZipMemberSpec(
        string Path,
        byte[] Bytes,
        int? ExternalAttributes = null);
}
