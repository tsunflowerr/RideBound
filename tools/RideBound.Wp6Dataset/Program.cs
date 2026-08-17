using System.Text.Json;
using RideBound.Benchmarking.Datasets;

if (!TryParse(args, out var options, out var usageError))
{
    Console.Error.WriteLine(usageError);
    Console.Error.WriteLine(
        "Usage: download --cache <absolute-path-outside-repo> " +
        "--repository <RideBound-root> --accept-license CC-BY-4.0 " +
        "[--resume-partial <staging.part>] [--extract]");
    return 2;
}

var registration = DatasetSourceRegistry.FleetPyManhattanV1;
using var handler = new HttpClientHandler
{
    AllowAutoRedirect = true,
    MaxAutomaticRedirections = 5,
    AutomaticDecompression = System.Net.DecompressionMethods.None,
};
using var client = new HttpClient(handler)
{
    Timeout = Timeout.InfiniteTimeSpan,
};
client.DefaultRequestHeaders.UserAgent.ParseAdd("RideBound-WP6-Dataset/1.0");

Console.WriteLine(
    $"Downloading immutable source {registration.DatasetId} from " +
    $"{registration.DownloadUri.Host}; publisher MD5 will be verified.");
var downloader = new VerifiedDatasetDownloader(client);
var acquisition = await downloader.AcquireAsync(
    new DatasetAcquisitionRequest(
        registration,
        new DatasetCacheOptions(options.CacheRoot, options.RepositoryRoot),
        options.AcceptedLicense,
        options.ResumePartialPath));

if (acquisition.Status != DatasetAcquisitionStatus.Succeeded)
{
    Console.Error.WriteLine(
        JsonSerializer.Serialize(
            new
            {
                status = acquisition.Status.ToString(),
                issue = acquisition.Issue,
            }));
    return acquisition.Status == DatasetAcquisitionStatus.Excluded ? 3 : 4;
}

var artifact = acquisition.Artifact!;
Console.WriteLine(
    JsonSerializer.Serialize(
        new
        {
            status = "verified",
            artifact.DatasetId,
            artifact.LengthBytes,
            artifact.Md5,
            artifact.Sha256,
            artifact.ContentAddress,
            artifact.FullPath,
            artifact.ReusedExistingBytes,
        }));

if (!options.Extract)
{
    return 0;
}

Console.WriteLine("Preflighting every ZIP member before atomic extraction.");
var extraction = await new SafeZipExtractor().ExtractAsync(
    artifact,
    new ZipExtractionOptions(
        options.CacheRoot,
        options.RepositoryRoot,
        new ZipExtractionLimits()));

if (extraction.Status != ArchiveExtractionStatus.Succeeded)
{
    Console.Error.WriteLine(
        JsonSerializer.Serialize(
            new
            {
                status = extraction.Status.ToString(),
                issue = extraction.Issue,
            }));
    return 5;
}

Console.WriteLine(
    JsonSerializer.Serialize(
        new
        {
            status = "extracted",
            extraction.ExtractionRoot,
            extraction.Inventory!.ArchiveSha256,
            extraction.Inventory.InventorySha256,
            extraction.Inventory.TotalUncompressedBytes,
            memberCount = extraction.Inventory.Members.Count,
            extraction.ReusedExistingExtraction,
        }));
return 0;

static bool TryParse(
    string[] arguments,
    out CommandOptions options,
    out string error)
{
    options = default!;
    error = string.Empty;

    if (arguments.Length < 7
        || !string.Equals(arguments[0], "download", StringComparison.Ordinal))
    {
        error = "The only opt-in action is 'download'.";
        return false;
    }

    string? cache = null;
    string? repository = null;
    string? license = null;
    string? resumePartial = null;
    var extract = false;

    for (var index = 1; index < arguments.Length; index++)
    {
        switch (arguments[index])
        {
            case "--cache" when index + 1 < arguments.Length:
                cache = arguments[++index];
                break;
            case "--repository" when index + 1 < arguments.Length:
                repository = arguments[++index];
                break;
            case "--accept-license" when index + 1 < arguments.Length:
                license = arguments[++index];
                break;
            case "--resume-partial" when index + 1 < arguments.Length:
                resumePartial = arguments[++index];
                break;
            case "--extract":
                extract = true;
                break;
            default:
                error = $"Unknown or incomplete argument '{arguments[index]}'.";
                return false;
        }
    }

    if (cache is null || repository is null || license is null)
    {
        error = "--cache, --repository and --accept-license are required.";
        return false;
    }

    options = new CommandOptions(cache, repository, license, resumePartial, extract);
    return true;
}

internal sealed record CommandOptions(
    string CacheRoot,
    string RepositoryRoot,
    string AcceptedLicense,
    string? ResumePartialPath,
    bool Extract);
