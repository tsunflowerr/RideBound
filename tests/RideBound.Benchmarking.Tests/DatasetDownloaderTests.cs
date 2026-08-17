using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using RideBound.Benchmarking.Contracts;
using RideBound.Benchmarking.Datasets;

namespace RideBound.Benchmarking.Tests;

public sealed class DatasetDownloaderTests
{
    [Fact]
    public async Task Explicit_license_good_checksums_and_content_address_are_required()
    {
        using var temp = new TestDirectory();
        var bytes = "verified-public-archive"u8.ToArray();
        var registration = RegistrationFor(bytes);
        var handler = new CountingHandler(
            request => Response(request, HttpStatusCode.OK, bytes));
        var downloader = new VerifiedDatasetDownloader(
            new HttpClient(handler),
            new FixedTimeProvider(new DateTimeOffset(2026, 8, 9, 1, 2, 3, TimeSpan.Zero)));
        var request = Request(temp, registration, registration.LicenseSpdx);

        var first = await downloader.AcquireAsync(request);
        var second = await downloader.AcquireAsync(request);

        Assert.Equal(DatasetAcquisitionStatus.Succeeded, first.Status);
        Assert.Equal(DatasetAcquisitionStatus.Succeeded, second.Status);
        Assert.False(first.Artifact!.ReusedExistingBytes);
        Assert.True(second.Artifact!.ReusedExistingBytes);
        Assert.Equal(1, handler.CallCount);
        Assert.Equal(first.Artifact.Sha256, second.Artifact.Sha256);
        Assert.Equal(first.Artifact.ContentAddress, second.Artifact.ContentAddress);
        Assert.Equal(
            BenchmarkContractCodec.Encode(first.Artifact.Descriptor),
            BenchmarkContractCodec.Encode(second.Artifact.Descriptor));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(first.Artifact.FullPath));
        Assert.True(
            (File.GetAttributes(first.Artifact.FullPath) & FileAttributes.ReadOnly) != 0);
        Assert.False(IsUnder(temp.RepositoryRoot, first.Artifact.FullPath));
    }

    [Fact]
    public async Task Missing_license_acceptance_is_typed_exclusion_without_http_request()
    {
        using var temp = new TestDirectory();
        var bytes = "archive"u8.ToArray();
        var registration = RegistrationFor(bytes);
        var handler = new CountingHandler(
            request => Response(request, HttpStatusCode.OK, bytes));
        var downloader = new VerifiedDatasetDownloader(new HttpClient(handler));

        var result = await downloader.AcquireAsync(
            Request(temp, registration, "license-not-accepted"));

        Assert.Equal(DatasetAcquisitionStatus.Excluded, result.Status);
        Assert.Equal("source.license-not-accepted", result.Issue!.Code);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task Registered_valid_object_is_reused_without_contacting_publisher()
    {
        using var temp = new TestDirectory();
        var goodBytes = "good-archive"u8.ToArray();
        var registration = RegistrationFor(goodBytes);
        var goodDownloader = new VerifiedDatasetDownloader(
            new HttpClient(
                new CountingHandler(
                    request => Response(request, HttpStatusCode.OK, goodBytes))));
        var request = Request(temp, registration, registration.LicenseSpdx);
        var good = await goodDownloader.AcquireAsync(request);
        Assert.Equal(DatasetAcquisitionStatus.Succeeded, good.Status);

        var badBytes = "bad-archive"u8.ToArray();
        var badHandler = new CountingHandler(
            request => Response(request, HttpStatusCode.OK, badBytes));
        var badDownloader = new VerifiedDatasetDownloader(
            new HttpClient(badHandler));
        var reused = await badDownloader.AcquireAsync(request);

        Assert.Equal(DatasetAcquisitionStatus.Succeeded, reused.Status);
        Assert.True(reused.Artifact!.ReusedExistingBytes);
        Assert.Equal(0, badHandler.CallCount);
        Assert.Equal(goodBytes, await File.ReadAllBytesAsync(good.Artifact!.FullPath));
    }

    [Fact]
    public async Task Modified_registered_object_fails_closed_without_network_or_replacement()
    {
        using var temp = new TestDirectory();
        var goodBytes = "good-archive"u8.ToArray();
        var registration = RegistrationFor(goodBytes);
        var request = Request(temp, registration, registration.LicenseSpdx);
        var first = await new VerifiedDatasetDownloader(
                new HttpClient(
                    new CountingHandler(
                        message => Response(message, HttpStatusCode.OK, goodBytes))))
            .AcquireAsync(request);
        Assert.Equal(DatasetAcquisitionStatus.Succeeded, first.Status);

        File.SetAttributes(first.Artifact!.FullPath, FileAttributes.Normal);
        await File.WriteAllBytesAsync(
            first.Artifact.FullPath,
            "evil-archive"u8.ToArray());
        var handler = new CountingHandler(
            message => Response(message, HttpStatusCode.OK, goodBytes));
        var result = await new VerifiedDatasetDownloader(new HttpClient(handler))
            .AcquireAsync(request);

        Assert.Equal(DatasetAcquisitionStatus.Failed, result.Status);
        Assert.Equal("cache.registered-object-mismatch", result.Issue!.Code);
        Assert.Equal(0, handler.CallCount);
        Assert.Equal(
            "evil-archive"u8.ToArray(),
            await File.ReadAllBytesAsync(first.Artifact.FullPath));
    }

    [Fact]
    public async Task Same_length_checksum_mismatch_is_reported_before_promotion()
    {
        using var temp = new TestDirectory();
        var expected = "aaaa"u8.ToArray();
        var actual = "bbbb"u8.ToArray();
        var registration = RegistrationFor(expected);
        var downloader = new VerifiedDatasetDownloader(
            new HttpClient(
                new CountingHandler(
                    request => Response(request, HttpStatusCode.OK, actual))));

        var result = await downloader.AcquireAsync(
            Request(temp, registration, registration.LicenseSpdx));

        Assert.Equal(DatasetAcquisitionStatus.Failed, result.Status);
        Assert.Equal("source.checksum-mismatch", result.Issue!.Code);
        Assert.Empty(Directory.GetFiles(temp.CacheRoot, registration.PublisherArtifactName, SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Cache_inside_repository_and_non_allowlisted_redirect_fail_closed()
    {
        using var temp = new TestDirectory();
        var bytes = "archive"u8.ToArray();
        var registration = RegistrationFor(bytes);
        var handler = new CountingHandler(
            request =>
            {
                var response = Response(request, HttpStatusCode.OK, bytes);
                response.RequestMessage = new HttpRequestMessage(
                    HttpMethod.Get,
                    "https://unexpected.example/archive.zip");
                return response;
            });
        var downloader = new VerifiedDatasetDownloader(new HttpClient(handler));
        var insideRequest = new DatasetAcquisitionRequest(
            registration,
            new DatasetCacheOptions(
                Path.Combine(temp.RepositoryRoot, "cache"),
                temp.RepositoryRoot,
                1_000),
            registration.LicenseSpdx);

        var inside = await downloader.AcquireAsync(insideRequest);
        var redirect = await downloader.AcquireAsync(
            Request(temp, registration, registration.LicenseSpdx));

        Assert.Equal("cache.invalid", inside.Issue!.Code);
        Assert.Equal("download.redirect-not-allowlisted", redirect.Issue!.Code);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task Announced_or_streamed_size_above_cap_fails_and_cleans_staging()
    {
        using var temp = new TestDirectory();
        var bytes = new byte[64];
        var registration = RegistrationFor(bytes) with
        {
            PublisherArtifactLengthBytes = null,
        };
        var handler = new CountingHandler(
            request => Response(request, HttpStatusCode.OK, bytes));
        var downloader = new VerifiedDatasetDownloader(new HttpClient(handler));
        var request = new DatasetAcquisitionRequest(
            registration,
            new DatasetCacheOptions(temp.CacheRoot, temp.RepositoryRoot, 32),
            registration.LicenseSpdx);

        var result = await downloader.AcquireAsync(request);

        Assert.Equal(DatasetAcquisitionStatus.Failed, result.Status);
        Assert.Equal("download.length-mismatch", result.Issue!.Code);
        var staging = Path.Combine(temp.CacheRoot, ".staging");
        Assert.True(!Directory.Exists(staging) || Directory.GetFiles(staging).Length == 0);

        using var chunkedTemp = new TestDirectory();
        var chunkedHandler = new CountingHandler(
            request => new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new UnknownLengthContent(bytes),
            });
        var chunkedDownloader = new VerifiedDatasetDownloader(new HttpClient(chunkedHandler));
        var chunkedRequest = new DatasetAcquisitionRequest(
            registration,
            new DatasetCacheOptions(chunkedTemp.CacheRoot, chunkedTemp.RepositoryRoot, 32),
            registration.LicenseSpdx);
        var chunked = await chunkedDownloader.AcquireAsync(chunkedRequest);
        Assert.Equal("download.size-exceeded", chunked.Issue!.Code);
    }

    [Fact]
    public async Task Verified_range_resume_copies_partial_and_hashes_the_complete_object()
    {
        using var temp = new TestDirectory();
        var bytes = Enumerable.Range(0, 256).Select(value => (byte)value).ToArray();
        var registration = RegistrationFor(bytes);
        var staging = Path.Combine(temp.CacheRoot, ".staging");
        Directory.CreateDirectory(staging);
        var partialPath = Path.Combine(staging, "resume.part");
        await File.WriteAllBytesAsync(partialPath, bytes[..100]);
        var handler = new CountingHandler(
            request =>
            {
                Assert.Equal(100, request.Headers.Range!.Ranges.Single().From);
                var response = Response(
                    request,
                    HttpStatusCode.PartialContent,
                    bytes[100..]);
                response.Content.Headers.ContentRange = new ContentRangeHeaderValue(
                    100,
                    bytes.Length - 1,
                    bytes.Length);
                return response;
            });
        var downloader = new VerifiedDatasetDownloader(new HttpClient(handler));
        var request = Request(temp, registration, registration.LicenseSpdx) with
        {
            ResumePartialPath = partialPath,
        };

        var result = await downloader.AcquireAsync(request);

        Assert.Equal(DatasetAcquisitionStatus.Succeeded, result.Status);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(result.Artifact!.FullPath));
        Assert.Equal(bytes[..100], await File.ReadAllBytesAsync(partialPath));
    }

    [Fact]
    public async Task Wrong_content_range_fails_and_server_ignoring_range_restarts_safely()
    {
        using var invalidTemp = new TestDirectory();
        var bytes = "complete-object"u8.ToArray();
        var registration = RegistrationFor(bytes);
        var invalidStaging = Path.Combine(invalidTemp.CacheRoot, ".staging");
        Directory.CreateDirectory(invalidStaging);
        var invalidPartial = Path.Combine(invalidStaging, "resume.part");
        await File.WriteAllBytesAsync(invalidPartial, bytes[..4]);
        var invalidHandler = new CountingHandler(
            request =>
            {
                var response = Response(request, HttpStatusCode.PartialContent, bytes[4..]);
                response.Content.Headers.ContentRange = new ContentRangeHeaderValue(
                    5,
                    bytes.Length - 1,
                    bytes.Length);
                return response;
            });
        var invalid = await new VerifiedDatasetDownloader(new HttpClient(invalidHandler))
            .AcquireAsync(
                Request(invalidTemp, registration, registration.LicenseSpdx) with
                {
                    ResumePartialPath = invalidPartial,
                });
        Assert.Equal("download.invalid-content-range", invalid.Issue!.Code);

        using var restartTemp = new TestDirectory();
        var restartStaging = Path.Combine(restartTemp.CacheRoot, ".staging");
        Directory.CreateDirectory(restartStaging);
        var restartPartial = Path.Combine(restartStaging, "resume.part");
        await File.WriteAllBytesAsync(restartPartial, bytes[..4]);
        var restartHandler = new CountingHandler(
            request => Response(request, HttpStatusCode.OK, bytes));
        var restarted = await new VerifiedDatasetDownloader(new HttpClient(restartHandler))
            .AcquireAsync(
                Request(restartTemp, registration, registration.LicenseSpdx) with
                {
                    ResumePartialPath = restartPartial,
                });
        Assert.Equal(DatasetAcquisitionStatus.Succeeded, restarted.Status);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(restarted.Artifact!.FullPath));
    }

    [Fact]
    public void Fleetpy_registry_is_immutable_version_specific_and_claim_limited()
    {
        var registration = DatasetSourceRegistry.FleetPyManhattanV1;

        Assert.Equal("fleetpy-manhattan-zenodo-15187906-v1", registration.DatasetId);
        Assert.Equal("1.0", registration.ReleaseVersion);
        Assert.Equal("https://doi.org/10.5281/zenodo.15187906", registration.PersistentUri.AbsoluteUri);
        Assert.Equal("FleetPy_Manhattan.zip", registration.PublisherArtifactName);
        Assert.Equal("8b11882ae9c6d87f666bf6e006806744", registration.PublisherMd5);
        Assert.Equal("CC-BY-4.0", registration.LicenseSpdx);
        Assert.Contains("observedCommitmentPreference", registration.ForbiddenClaim);
        Assert.Contains("userSatisfaction", registration.ForbiddenClaim);
        Assert.Equal(408_878_341, registration.PublisherArtifactLengthBytes);
        Assert.Equal(
            "d9e86f33645e5eec287d387f8d63ad41ddf41d4ef648138b65d636482e2c599e",
            registration.SourceArtifactSha256);
    }

    private static DatasetAcquisitionRequest Request(
        TestDirectory temp,
        DatasetSourceRegistration registration,
        string acceptedLicense) =>
        new(
            registration,
            new DatasetCacheOptions(temp.CacheRoot, temp.RepositoryRoot, 1_000_000),
            acceptedLicense);

    private static DatasetSourceRegistration RegistrationFor(byte[] bytes)
    {
        return DatasetSourceRegistry.FleetPyManhattanV1 with
        {
            DatasetId = "test-dataset-v1",
            DownloadUri = new Uri("https://zenodo.org/test/archive.zip"),
            PublisherArtifactName = "archive.zip",
            PublisherArtifactLengthBytes = bytes.LongLength,
            PublisherMd5 = Md5(bytes),
            SourceArtifactSha256 = Sha(bytes),
        };
    }

    private static HttpResponseMessage Response(
        HttpRequestMessage request,
        HttpStatusCode statusCode,
        byte[] bytes)
    {
        return new HttpResponseMessage(statusCode)
        {
            RequestMessage = request,
            Content = new ByteArrayContent(bytes),
        };
    }

    private static string Md5(byte[] bytes) =>
        Convert.ToHexStringLower(MD5.HashData(bytes));

    private static string Sha(byte[] bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static bool IsUnder(string root, string path)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(path));
        return relative == "."
            || !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && relative != ".."
                && !Path.IsPathRooted(relative);
    }

    private sealed class CountingHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(responseFactory(request));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class UnknownLengthContent(byte[] bytes) : HttpContent
    {
        protected override Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context) => stream.WriteAsync(bytes).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
