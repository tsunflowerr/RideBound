using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RideBound.Benchmarking.Datasets;
using RideBound.Benchmarking.Normalization;

if (!TryParse(args, out var options, out var error))
{
    Console.Error.WriteLine(error);
    Console.Error.WriteLine(
        "Usage: normalize --cache <absolute-path-outside-repo> "
        + "--repository <RideBound-root> --accept-license CC-BY-4.0 "
        + "--profile <tiny|medium|all>");
    return 2;
}

var repositoryRoot = Path.GetFullPath(options.RepositoryRoot);
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
client.DefaultRequestHeaders.UserAgent.ParseAdd("RideBound-WP6-Normalizer/1.0");
var acquisition = await new VerifiedDatasetDownloader(client).AcquireAsync(
    new DatasetAcquisitionRequest(
        registration,
        new DatasetCacheOptions(options.CacheRoot, repositoryRoot),
        options.AcceptedLicense));

if (acquisition.Status != DatasetAcquisitionStatus.Succeeded)
{
    Console.Error.WriteLine(JsonSerializer.Serialize(acquisition.Issue));
    return 3;
}

var extraction = await new SafeZipExtractor().ExtractAsync(
    acquisition.Artifact!,
    new ZipExtractionOptions(
        options.CacheRoot,
        repositoryRoot,
        new ZipExtractionLimits()));

if (extraction.Status != ArchiveExtractionStatus.Succeeded)
{
    Console.Error.WriteLine(JsonSerializer.Serialize(extraction.Issue));
    return 4;
}

var sourceHash = FleetPyNormalizerSourceIdentity.Calculate(repositoryRoot);

if (options.GridManifest is not null)
{
    var manifestPath = Path.GetFullPath(options.GridManifest);
    var manifest = JsonSerializer.Deserialize<GridManifest>(
        await File.ReadAllBytesAsync(manifestPath),
        new JsonSerializerOptions(JsonSerializerDefaults.Web));

    if (manifest is null
        || string.IsNullOrWhiteSpace(manifest.GridId)
        || manifest.Cells.Count == 0
        || manifest.Cells.Select(cell => cell.CellId)
            .Distinct(StringComparer.Ordinal).Count() != manifest.Cells.Count)
    {
        Console.Error.WriteLine(
            "Grid manifest requires a gridId and cells with unique cellId values.");
        return 6;
    }

    foreach (var cell in manifest.Cells.OrderBy(
                 value => value.CellId,
                 StringComparer.Ordinal))
    {
        var cellResult = await new FleetPyManhattanNormalizer().NormalizeAsync(
            new FleetPyNormalizationRequest(
                acquisition.Artifact!,
                extraction,
                GridConfiguration(cell, sourceHash)));

        if (cellResult.Status != FleetPyNormalizationStatus.Succeeded)
        {
            Console.Error.WriteLine(JsonSerializer.Serialize(cellResult.Issue));
            return 5;
        }

        var cellRoot = Path.Combine(
            repositoryRoot,
            "benchmarks",
            "fixtures",
            "wp6",
            "public",
            "fleetpy-manhattan-v1",
            manifest.GridId,
            cell.CellId);
        var cellWritten = await WriteDerivativeAsync(
            cellRoot,
            cellResult.Artifact!,
            acquisition.Artifact!.Descriptor.Citation);
        Console.WriteLine(
            JsonSerializer.Serialize(
                new
                {
                    gridId = manifest.GridId,
                    cell.CellId,
                    outputRoot = cellRoot,
                    cellResult.Artifact!.ScenarioHash,
                    cellResult.Artifact.ReportHash,
                    requestCount = cellResult.Artifact.Scenario.Requests.Count,
                    vehicleCount = cellResult.Artifact.Scenario.Fleet.Count,
                    nodeCount = cellResult.Artifact.Scenario.ValidationSummary.NodeCount,
                    directedArcCount =
                        cellResult.Artifact.Scenario.ValidationSummary.DirectedArcCount,
                    cellResult.Artifact.Report.InputRecordCount,
                    cellResult.Artifact.Report.EligibleRecordCount,
                    cellResult.Artifact.Report.SelectedRecordCount,
                    cellResult.Artifact.Report.ExcludedRecordCount,
                    reusedExactDerivative = !cellWritten,
                }));
    }

    return 0;
}

var profiles = options.Profile == "all"
    ? new[] { "tiny", "medium" }
    : new[] { options.Profile! };

foreach (var profile in profiles)
{
    var configuration = Configuration(profile, sourceHash);
    var result = await new FleetPyManhattanNormalizer().NormalizeAsync(
        new FleetPyNormalizationRequest(
            acquisition.Artifact!,
            extraction,
            configuration));

    if (result.Status != FleetPyNormalizationStatus.Succeeded)
    {
        Console.Error.WriteLine(JsonSerializer.Serialize(result.Issue));
        return 5;
    }

    var outputRoot = Path.Combine(
        repositoryRoot,
        "benchmarks",
        "fixtures",
        "wp6",
        "public",
        "fleetpy-manhattan-v1",
        profile);
    var written = await WriteDerivativeAsync(
        outputRoot,
        result.Artifact!,
        acquisition.Artifact!.Descriptor.Citation);
    Console.WriteLine(
        JsonSerializer.Serialize(
            new
            {
                profile,
                outputRoot,
                result.Artifact!.ScenarioHash,
                result.Artifact.ReportHash,
                requestCount = result.Artifact.Scenario.Requests.Count,
                vehicleCount = result.Artifact.Scenario.Fleet.Count,
                nodeCount = result.Artifact.Scenario.ValidationSummary.NodeCount,
                directedArcCount = result.Artifact.Scenario.ValidationSummary.DirectedArcCount,
                result.Artifact.Report.InputRecordCount,
                result.Artifact.Report.EligibleRecordCount,
                result.Artifact.Report.SelectedRecordCount,
                result.Artifact.Report.ExcludedRecordCount,
                reusedExactDerivative = !written,
            }));
}

return 0;

static FleetPyNormalizationConfiguration GridConfiguration(
    GridCell cell,
    string sourceHash) =>
    new(
        cell.ScenarioId,
        cell.DemandMemberPath,
        "FleetPy_Manhattan/networks/Manhattan_2019_corrected/base/nodes.csv",
        "FleetPy_Manhattan/networks/Manhattan_2019_corrected/base/edges.csv",
        cell.TravelFactorMemberPath,
        cell.SourceLocalDate,
        "America/New_York",
        cell.SourceWindowStartSeconds,
        cell.SourceWindowEndSeconds,
        cell.TravelFactorAtSeconds,
        cell.RequestTarget,
        cell.VehicleCount,
        cell.MaximumNodeCount,
        cell.VehicleCapacity,
        cell.PickupWindowMs,
        cell.MaximumRideTimePermille,
        cell.DrainDurationMs,
        Sha(cell.SelectionLabel),
        Sha(cell.PseudonymizationLabel),
        cell.CommitmentPolicyId,
        sourceHash);

static FleetPyNormalizationConfiguration Configuration(
    string profile,
    string sourceHash)
{
    var isTiny = profile == "tiny";
    return new FleetPyNormalizationConfiguration(
        isTiny
            ? "fleetpy-manhattan-v1-tiny-mechanical"
            : "fleetpy-manhattan-v1-medium-mechanical",
        "FleetPy_Manhattan/demand/Manhattan_2018/matched/Manhattan_2019_corrected/2018-11-12_sample_10_1.csv",
        "FleetPy_Manhattan/networks/Manhattan_2019_corrected/base/nodes.csv",
        "FleetPy_Manhattan/networks/Manhattan_2019_corrected/base/edges.csv",
        "FleetPy_Manhattan/networks/Manhattan_2019_corrected/2018-11-12_tt_factors.csv",
        "2018-11-12",
        "America/New_York",
        0,
        86_400,
        0,
        isTiny ? 8 : 128,
        isTiny ? 2 : 32,
        isTiny ? 16 : 96,
        4,
        600_000,
        1_500,
        7_200_000,
        Sha("ridebound-wp6-public-source-selection-v1"),
        Sha("ridebound-wp6-public-request-pseudonymization-v1"),
        "wp6-synthetic-policy-overlay-v1",
        sourceHash);
}

static async Task<bool> WriteDerivativeAsync(
    string outputRoot,
    FleetPyNormalizationArtifact artifact,
    string citation)
{
    var attribution = Encoding.UTF8.GetBytes(
        "# FleetPy Manhattan public derivative\n\n"
        + $"Scenario hash: `{artifact.ScenarioHash}`  \n"
        + $"Normalization report hash: `{artifact.ReportHash}`  \n"
        + "Source DOI: <https://doi.org/10.5281/zenodo.15187906>  \n"
        + "License: [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/)  \n\n"
        + $"Citation: {citation}\n\n"
        + "The source contains no observed RideBound commitment preference or satisfaction label. "
        + "All commitment-policy assignments are an explicit `syntheticPolicyOverlay`. "
        + "This derivative supports mechanical reproducibility only, not effectiveness, "
        + "non-inferiority, fairness, production-SLA or user-satisfaction claims.\n");
    var files = new Dictionary<string, byte[]>(StringComparer.Ordinal)
    {
        ["ATTRIBUTION.md"] = attribution,
        ["derivative-manifest.json"] = artifact.DerivativeManifestCanonicalBytes,
        ["normalization-exclusions.json"] = artifact.ExclusionsCanonicalBytes,
        ["normalization-report.json"] = artifact.ReportCanonicalBytes,
        ["normalizer-configuration.json"] = artifact.ConfigurationCanonicalBytes,
        ["scenario-content.json"] = artifact.ScenarioCanonicalBytes,
        ["selection-frame.json"] = artifact.DispositionsCanonicalBytes,
    };

    if (Directory.Exists(outputRoot))
    {
        var existing = Directory.GetFiles(outputRoot, "*", SearchOption.TopDirectoryOnly)
            .ToDictionary(path => Path.GetFileName(path)!, StringComparer.Ordinal);

        if (existing.Count != files.Count
            || files.Any(
                pair => !existing.TryGetValue(pair.Key, out var path)
                    || !File.ReadAllBytes(path!).SequenceEqual(pair.Value)))
        {
            throw new IOException(
                $"Existing derivative '{outputRoot}' differs from deterministic output; it was not overwritten.");
        }

        return false;
    }

    var parent = Directory.GetParent(outputRoot)?.FullName
        ?? throw new IOException("Derivative output has no parent directory.");
    Directory.CreateDirectory(parent);
    var staging = Path.Combine(
        parent,
        "." + Path.GetFileName(outputRoot) + "." + Guid.NewGuid().ToString("N") + ".staging");

    try
    {
        Directory.CreateDirectory(staging);

        foreach (var file in files)
        {
            await File.WriteAllBytesAsync(Path.Combine(staging, file.Key), file.Value);
        }

        Directory.Move(staging, outputRoot);
        return true;
    }
    finally
    {
        if (Directory.Exists(staging))
        {
            Directory.Delete(staging, recursive: true);
        }
    }
}

static string Sha(string value) =>
    Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

static bool TryParse(
    string[] arguments,
    out CommandOptions options,
    out string error)
{
    options = default!;
    error = string.Empty;

    if (arguments.Length < 9
        || !string.Equals(arguments[0], "normalize", StringComparison.Ordinal))
    {
        error = "The only action is 'normalize'.";
        return false;
    }

    string? cache = null;
    string? repository = null;
    string? license = null;
    string? profile = null;
    string? grid = null;

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
            case "--profile" when index + 1 < arguments.Length:
                profile = arguments[++index];
                break;
            case "--grid" when index + 1 < arguments.Length:
                grid = arguments[++index];
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

    if ((profile is null) == (grid is null))
    {
        error = "Exactly one of --profile or --grid is required.";
        return false;
    }

    if (profile is not null and not ("tiny" or "medium" or "all"))
    {
        error = "--profile must be tiny, medium or all.";
        return false;
    }

    options = new CommandOptions(cache, repository, license, profile, grid);
    return true;
}

internal sealed record CommandOptions(
    string CacheRoot,
    string RepositoryRoot,
    string AcceptedLicense,
    string? Profile,
    string? GridManifest);

/// <summary>
/// One declared cell of an experiment grid. Every field the normalizer can vary is
/// stated explicitly so a grid is auditable from source control alone; nothing is
/// inferred and nothing is defaulted silently.
/// </summary>
internal sealed record GridCell(
    string CellId,
    string ScenarioId,
    string DemandMemberPath,
    string TravelFactorMemberPath,
    string SourceLocalDate,
    long SourceWindowStartSeconds,
    long SourceWindowEndSeconds,
    long TravelFactorAtSeconds,
    int RequestTarget,
    int VehicleCount,
    int MaximumNodeCount,
    int VehicleCapacity,
    long PickupWindowMs,
    long MaximumRideTimePermille,
    long DrainDurationMs,
    string SelectionLabel,
    string PseudonymizationLabel,
    string CommitmentPolicyId);

internal sealed record GridManifest(string GridId, IReadOnlyList<GridCell> Cells);
