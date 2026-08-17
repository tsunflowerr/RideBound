using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using RideBound.Benchmarking.Contracts;

namespace RideBound.Benchmarking.Datasets;

public sealed class VerifiedDatasetDownloader
{
    private const int CopyBufferBytes = 128 * 1024;
    private const long ProgressFlushBytes = 64L * 1024 * 1024;

    private readonly HttpClient httpClient;
    private readonly TimeProvider timeProvider;

    public VerifiedDatasetDownloader(
        HttpClient httpClient,
        TimeProvider? timeProvider = null)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<DatasetAcquisitionResult> AcquireAsync(
        DatasetAcquisitionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Registration);
        ArgumentNullException.ThrowIfNull(request.Cache);

        if (!string.Equals(
            request.AcceptedLicenseSpdx,
            request.Registration.LicenseSpdx,
            StringComparison.Ordinal))
        {
            return DatasetAcquisitionResult.Excluded(
                "source.license-not-accepted",
                "license",
                $"License '{request.Registration.LicenseSpdx}' was not explicitly accepted.");
        }

        DatasetCacheLayout layout;

        try
        {
            layout = DatasetCacheLayout.Create(request.Cache);
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return DatasetAcquisitionResult.Failed(
                "cache.invalid",
                "preflight",
                exception.Message);
        }

        Directory.CreateDirectory(layout.StagingRoot);
        Directory.CreateDirectory(layout.ObjectsRoot);
        layout.EnsureSafeDescendant(layout.StagingRoot);
        layout.EnsureSafeDescendant(layout.ObjectsRoot);

        var cached = await TryReuseRegisteredObjectAsync(
            layout,
            request.Registration,
            cancellationToken);

        if (cached is not null)
        {
            return cached;
        }

        var tempName = RandomNumberGenerator.GetHexString(24, lowercase: true) + ".part";
        var tempPath = Path.Combine(layout.StagingRoot, tempName);

        try
        {
            using var message = new HttpRequestMessage(
                HttpMethod.Get,
                request.Registration.DownloadUri);
            message.Headers.Accept.ParseAdd("application/octet-stream");
            var resume = layout.ResolveResumePartial(
                request.ResumePartialPath,
                request.Cache.MaximumArchiveBytes);

            if (resume is not null)
            {
                message.Headers.Range = new RangeHeaderValue(resume.LengthBytes, null);
            }

            using var response = await httpClient.SendAsync(
                message,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (response.StatusCode is < HttpStatusCode.OK or >= HttpStatusCode.MultipleChoices)
            {
                return DatasetAcquisitionResult.Failed(
                    "download.http-status",
                    "download",
                    $"Publisher returned HTTP {(int)response.StatusCode}.");
            }

            var finalUri = response.RequestMessage?.RequestUri;

            if (finalUri is null
                || !string.Equals(finalUri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
                || !request.Registration.AllowedDownloadHosts.Contains(finalUri.Host))
            {
                return DatasetAcquisitionResult.Failed(
                    "download.redirect-not-allowlisted",
                    "download",
                    "The final download origin is not in the immutable source allowlist.");
            }

            var useResume = resume is not null
                && response.StatusCode == HttpStatusCode.PartialContent;
            var contentRange = response.Content.Headers.ContentRange;

            if (useResume
                && (contentRange is null
                    || !string.Equals(contentRange.Unit, "bytes", StringComparison.OrdinalIgnoreCase)
                    || contentRange.From != resume!.LengthBytes
                    || contentRange.Length is null
                    || contentRange.Length <= resume.LengthBytes
                    || contentRange.Length > request.Cache.MaximumArchiveBytes)
                || resume is null && response.StatusCode == HttpStatusCode.PartialContent)
            {
                return DatasetAcquisitionResult.Failed(
                    "download.invalid-content-range",
                    "download",
                    "Publisher partial response does not match the requested resume offset.");
            }

            var contentLength = response.Content.Headers.ContentLength;
            var announcedTotalLength = useResume ? contentRange!.Length : contentLength;

            if (announcedTotalLength is < 0
                || announcedTotalLength > request.Cache.MaximumArchiveBytes
                || request.Registration.PublisherArtifactLengthBytes is long expectedLength
                    && announcedTotalLength is long announcedLength
                    && announcedLength != expectedLength)
            {
                return DatasetAcquisitionResult.Failed(
                    "download.length-mismatch",
                    "download",
                    "Publisher Content-Length violates the registered archive bounds.");
            }

            var streamed = await StreamToNewFileAsync(
                response,
                tempPath,
                request.Cache.MaximumArchiveBytes,
                useResume ? resume!.FullPath : null,
                cancellationToken);

            if (announcedTotalLength is long responseLength
                    && streamed.LengthBytes != responseLength
                || request.Registration.PublisherArtifactLengthBytes is long registeredLength
                    && streamed.LengthBytes != registeredLength)
            {
                return DatasetAcquisitionResult.Failed(
                    "download.length-mismatch",
                    "verification",
                    "Downloaded byte length does not match publisher metadata.");
            }

            if (request.Registration.PublisherMd5 is not null
                && !string.Equals(
                    request.Registration.PublisherMd5,
                    streamed.Md5,
                    StringComparison.Ordinal)
                || request.Registration.SourceArtifactSha256 is not null
                    && !string.Equals(
                        request.Registration.SourceArtifactSha256,
                        streamed.Sha256,
                        StringComparison.Ordinal))
            {
                return DatasetAcquisitionResult.Failed(
                    "source.checksum-mismatch",
                    "verification",
                    "Downloaded bytes do not match the immutable publisher checksum.");
            }

            var objectDirectory = Path.Combine(
                layout.ObjectsRoot,
                "sha256",
                streamed.Sha256[..2],
                streamed.Sha256);
            var finalPath = Path.Combine(
                objectDirectory,
                request.Registration.PublisherArtifactName);
            layout.EnsureSafeDescendant(objectDirectory);
            Directory.CreateDirectory(objectDirectory);
            layout.EnsureSafeDescendant(objectDirectory);

            var reused = false;

            if (File.Exists(finalPath))
            {
                var existing = await HashFileAsync(finalPath, cancellationToken);

                if (existing != streamed)
                {
                    return DatasetAcquisitionResult.Failed(
                        "cache.content-address-collision",
                        "promotion",
                        "Existing content-addressed object does not match its address.");
                }

                reused = true;
                File.Delete(tempPath);
            }
            else
            {
                try
                {
                    File.Move(tempPath, finalPath, overwrite: false);
                    File.SetAttributes(finalPath, FileAttributes.ReadOnly);
                }
                catch (IOException) when (File.Exists(finalPath))
                {
                    var existing = await HashFileAsync(finalPath, cancellationToken);

                    if (existing != streamed)
                    {
                        return DatasetAcquisitionResult.Failed(
                            "cache.content-address-collision",
                            "promotion",
                            "Concurrent cache promotion produced mismatching bytes.");
                    }

                    reused = true;
                    File.Delete(tempPath);
                }
            }

            var descriptorPath = Path.Combine(objectDirectory, "dataset-descriptor.json");
            var descriptor = await ReadOrCreateDescriptorAsync(
                descriptorPath,
                request.Registration,
                streamed,
                cancellationToken);

            return DatasetAcquisitionResult.Success(
                new VerifiedDatasetArtifact(
                    request.Registration.DatasetId,
                    finalPath,
                    $"sha256:{streamed.Sha256}",
                    streamed.LengthBytes,
                    streamed.Md5,
                    streamed.Sha256,
                    descriptor,
                    reused));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (DatasetAcquisitionException exception)
        {
            return DatasetAcquisitionResult.Failed(
                exception.Code,
                "download",
                exception.Message);
        }
        catch (Exception exception) when (
            exception is HttpRequestException
                or IOException
                or UnauthorizedAccessException
                or CryptographicException)
        {
            return DatasetAcquisitionResult.Failed(
                "download.io-failure",
                "download",
                exception.Message);
        }
        finally
        {
            TryDeleteTemporaryFile(tempPath);
        }
    }

    private async Task<DatasetDescriptor> ReadOrCreateDescriptorAsync(
        string descriptorPath,
        DatasetSourceRegistration registration,
        FileDigest digest,
        CancellationToken cancellationToken)
    {
        if (File.Exists(descriptorPath))
        {
            var existingBytes = await File.ReadAllBytesAsync(descriptorPath, cancellationToken);
            var decoded = BenchmarkContractCodec.Decode<DatasetDescriptor>(existingBytes);

            if (!decoded.IsSuccess
                || decoded.Value!.PublisherArtifactLengthBytes != digest.LengthBytes
                || !string.Equals(
                    decoded.Value.SourceArtifactSha256,
                    digest.Sha256,
                    StringComparison.Ordinal))
            {
                throw new IOException(
                    "Existing dataset descriptor does not match content-addressed bytes.");
            }

            return decoded.Value;
        }

        var descriptor = new DatasetDescriptor(
            BenchmarkContractVersions.V1,
            registration.DatasetId,
            registration.DatasetKind,
            registration.Title,
            registration.ReleaseVersion,
            registration.PersistentUri.AbsoluteUri,
            registration.DownloadUri.AbsoluteUri,
            timeProvider.GetUtcNow().ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'"),
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
            digest.LengthBytes,
            digest.Md5,
            digest.Sha256);
        var canonical = BenchmarkContractCodec.Encode(descriptor);

        try
        {
            await using var stream = new FileStream(
                descriptorPath,
                new FileStreamOptions
                {
                    Access = FileAccess.Write,
                    Mode = FileMode.CreateNew,
                    Share = FileShare.None,
                    Options = FileOptions.Asynchronous | FileOptions.WriteThrough,
                });
            await stream.WriteAsync(canonical, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
        catch (IOException) when (File.Exists(descriptorPath))
        {
            return await ReadOrCreateDescriptorAsync(
                descriptorPath,
                registration,
                digest,
                cancellationToken);
        }

        return descriptor;
    }

    private async Task<DatasetAcquisitionResult?> TryReuseRegisteredObjectAsync(
        DatasetCacheLayout layout,
        DatasetSourceRegistration registration,
        CancellationToken cancellationToken)
    {
        if (registration.SourceArtifactSha256 is null
            || registration.PublisherArtifactLengthBytes is null
            || registration.PublisherMd5 is null)
        {
            return null;
        }

        var objectDirectory = Path.Combine(
            layout.ObjectsRoot,
            "sha256",
            registration.SourceArtifactSha256[..2],
            registration.SourceArtifactSha256);
        var objectPath = Path.Combine(objectDirectory, registration.PublisherArtifactName);
        layout.EnsureSafeDescendant(objectDirectory);

        if (!File.Exists(objectPath))
        {
            return null;
        }

        var digest = await HashFileAsync(objectPath, cancellationToken);

        if (digest.LengthBytes != registration.PublisherArtifactLengthBytes
            || !string.Equals(digest.Md5, registration.PublisherMd5, StringComparison.Ordinal)
            || !string.Equals(
                digest.Sha256,
                registration.SourceArtifactSha256,
                StringComparison.Ordinal))
        {
            return DatasetAcquisitionResult.Failed(
                "cache.registered-object-mismatch",
                "preflight",
                "Registered content-addressed object was modified or is corrupt.");
        }

        var descriptor = await ReadOrCreateDescriptorAsync(
            Path.Combine(objectDirectory, "dataset-descriptor.json"),
            registration,
            digest,
            cancellationToken);
        return DatasetAcquisitionResult.Success(
            new VerifiedDatasetArtifact(
                registration.DatasetId,
                objectPath,
                $"sha256:{digest.Sha256}",
                digest.LengthBytes,
                digest.Md5,
                digest.Sha256,
                descriptor,
                ReusedExistingBytes: true));
    }

    private static async Task<FileDigest> StreamToNewFileAsync(
        HttpResponseMessage response,
        string path,
        long maximumBytes,
        string? prefixPath,
        CancellationToken cancellationToken)
    {
        using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        using var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = new FileStream(
            path,
            new FileStreamOptions
            {
                Access = FileAccess.Write,
                Mode = FileMode.CreateNew,
                Share = FileShare.None,
                BufferSize = CopyBufferBytes,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
            });
        var buffer = new byte[CopyBufferBytes];
        long length = 0;
        long lastFlushedLength = 0;

        if (prefixPath is not null)
        {
            await using var prefix = new FileStream(
                prefixPath,
                new FileStreamOptions
                {
                    Access = FileAccess.Read,
                    Mode = FileMode.Open,
                    Share = FileShare.Read,
                    BufferSize = CopyBufferBytes,
                    Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
                });

            while (true)
            {
                var prefixRead = await prefix.ReadAsync(buffer, cancellationToken);

                if (prefixRead == 0)
                {
                    break;
                }

                length = checked(length + prefixRead);

                if (length > maximumBytes)
                {
                    throw new DatasetAcquisitionException(
                        "download.size-exceeded",
                        "Resume partial exceeds the configured byte cap.");
                }

                md5.AppendData(buffer, 0, prefixRead);
                sha256.AppendData(buffer, 0, prefixRead);
                await output.WriteAsync(buffer.AsMemory(0, prefixRead), cancellationToken);
            }

            await output.FlushAsync(cancellationToken);
            lastFlushedLength = length;
        }

        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken);

            if (read == 0)
            {
                break;
            }

            length = checked(length + read);

            if (length > maximumBytes)
            {
                throw new DatasetAcquisitionException(
                    "download.size-exceeded",
                    "Downloaded archive exceeds the configured byte cap.");
            }

            md5.AppendData(buffer, 0, read);
            sha256.AppendData(buffer, 0, read);
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);

            if (length - lastFlushedLength >= ProgressFlushBytes)
            {
                await output.FlushAsync(cancellationToken);
                lastFlushedLength = length;
            }
        }

        await output.FlushAsync(cancellationToken);
        return new FileDigest(
            length,
            Convert.ToHexStringLower(md5.GetHashAndReset()),
            Convert.ToHexStringLower(sha256.GetHashAndReset()));
    }

    internal static async Task<FileDigest> HashFileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        using var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using var stream = new FileStream(
            path,
            new FileStreamOptions
            {
                Access = FileAccess.Read,
                Mode = FileMode.Open,
                Share = FileShare.Read,
                BufferSize = CopyBufferBytes,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
            });
        var buffer = new byte[CopyBufferBytes];
        long length = 0;

        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);

            if (read == 0)
            {
                break;
            }

            length = checked(length + read);
            md5.AppendData(buffer, 0, read);
            sha256.AppendData(buffer, 0, read);
        }

        return new FileDigest(
            length,
            Convert.ToHexStringLower(md5.GetHashAndReset()),
            Convert.ToHexStringLower(sha256.GetHashAndReset()));
    }

    private static void TryDeleteTemporaryFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // The operation already reports its primary typed failure.
        }
        catch (UnauthorizedAccessException)
        {
            // The operation already reports its primary typed failure.
        }
    }

    internal sealed record FileDigest(long LengthBytes, string Md5, string Sha256);

    private sealed class DatasetAcquisitionException(string code, string message)
        : IOException(message)
    {
        public string Code { get; } = code;
    }

    private sealed record DatasetCacheLayout(
        string CacheRoot,
        string StagingRoot,
        string ObjectsRoot)
    {
        public static DatasetCacheLayout Create(DatasetCacheOptions options)
        {
            if (options.MaximumArchiveBytes <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    "Maximum archive bytes must be positive.");
            }

            var cacheRoot = Path.GetFullPath(options.CacheRoot);
            var repositoryRoot = Path.GetFullPath(options.RepositoryRoot);
            var relative = Path.GetRelativePath(repositoryRoot, cacheRoot);

            if (relative == "."
                || !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                    && relative != ".."
                    && !Path.IsPathRooted(relative))
            {
                throw new ArgumentException(
                    "Dataset cache must be outside the repository root.",
                    nameof(options));
            }

            EnsureExistingPathHasNoReparsePoint(cacheRoot);
            Directory.CreateDirectory(cacheRoot);
            EnsureExistingPathHasNoReparsePoint(cacheRoot);

            var stagingRoot = Path.Combine(cacheRoot, ".staging");
            var objectsRoot = Path.Combine(cacheRoot, "objects");
            Directory.CreateDirectory(stagingRoot);
            Directory.CreateDirectory(objectsRoot);
            EnsureExistingPathHasNoReparsePoint(stagingRoot);
            EnsureExistingPathHasNoReparsePoint(objectsRoot);

            return new DatasetCacheLayout(
                cacheRoot,
                stagingRoot,
                objectsRoot);
        }

        public void EnsureSafeDescendant(string path)
        {
            var fullPath = Path.GetFullPath(path);
            var relative = Path.GetRelativePath(CacheRoot, fullPath);

            if (relative == ".."
                || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || Path.IsPathRooted(relative))
            {
                throw new IOException("Cache operation escaped the configured cache root.");
            }

            EnsureExistingPathHasNoReparsePoint(fullPath);
        }

        public ResumePartial? ResolveResumePartial(string? path, long maximumBytes)
        {
            if (path is null)
            {
                return null;
            }

            var fullPath = Path.GetFullPath(path);
            var relative = Path.GetRelativePath(StagingRoot, fullPath);

            if (relative == ".."
                || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || Path.IsPathRooted(relative)
                || !string.Equals(Path.GetExtension(fullPath), ".part", StringComparison.Ordinal)
                || !File.Exists(fullPath)
                || (File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException(
                    "Resume partial must be a regular .part file inside the cache staging root.");
            }

            var length = new FileInfo(fullPath).Length;

            if (length <= 0 || length >= maximumBytes)
            {
                throw new IOException("Resume partial length is outside allowed bounds.");
            }

            return new ResumePartial(fullPath, length);
        }

        private static void EnsureExistingPathHasNoReparsePoint(string path)
        {
            var current = new DirectoryInfo(path);

            while (current is not null)
            {
                if (current.Exists
                    && (current.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new IOException(
                        $"Cache path traverses reparse point '{current.FullName}'.");
                }

                current = current.Parent;
            }
        }
    }

    private sealed record ResumePartial(string FullPath, long LengthBytes);
}
