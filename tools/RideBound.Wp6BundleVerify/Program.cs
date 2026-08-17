using System.Security.Cryptography;
using RideBound.Benchmarking.Bundles;

return await RunAsync(args);

static async Task<int> RunAsync(string[] arguments)
{
    if (!TryReadArguments(arguments, out var bag, out var report))
    {
        Console.Error.WriteLine(
            "usage: RideBound.Wp6BundleVerify --bag <directory> --report <outside-file>");
        return 64;
    }

    var bagRoot = Path.GetFullPath(bag!);
    var reportPath = Path.GetFullPath(report!);

    if (File.Exists(reportPath)
        || Directory.Exists(reportPath)
        || IsInside(bagRoot, reportPath))
    {
        Console.Error.WriteLine(
            "bundle.verify.report-path: report must be a new file outside the sealed bag");
        return 64;
    }

    var assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
    var assemblySha256 = FileSha(assemblyPath);
    var result = new StrictBagItBundleVerifier(assemblySha256).Verify(bagRoot);
    var external = new ExternalBundleVerificationReport(
        "1.0.0",
        Path.GetFileName(bagRoot),
        assemblySha256,
        result.IsValid,
        result.BundleHash ?? string.Empty,
        result.PlanHash ?? string.Empty,
        result.MetricSetHash ?? string.Empty,
        result.Issue?.Stage.ToString() ?? string.Empty,
        result.Issue?.Code ?? string.Empty,
        result.Issue?.RelativePath ?? string.Empty,
        result.Issue?.SafeMessage ?? string.Empty);
    var bytes = BundleEvidenceJson.Encode(external);
    Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
    await using (var stream = new FileStream(
        reportPath,
        FileMode.CreateNew,
        FileAccess.Write,
        FileShare.None,
        16_384,
        FileOptions.Asynchronous | FileOptions.WriteThrough))
    {
        await stream.WriteAsync(bytes);
        await stream.FlushAsync();
        stream.Flush(flushToDisk: true);
    }

    if (result.IsValid)
    {
        Console.Out.WriteLine(result.BundleHash);
        return 0;
    }

    Console.Error.WriteLine(
        $"{result.Issue!.Stage}:{result.Issue.Code}:{result.Issue.RelativePath}");
    return 2;
}

static bool TryReadArguments(
    IReadOnlyList<string> values,
    out string? bag,
    out string? report)
{
    bag = null;
    report = null;

    for (var index = 0; index < values.Count; index += 2)
    {
        if (index + 1 >= values.Count)
        {
            return false;
        }

        if (values[index] == "--bag" && bag is null)
        {
            bag = values[index + 1];
        }
        else if (values[index] == "--report" && report is null)
        {
            report = values[index + 1];
        }
        else
        {
            return false;
        }
    }

    return bag is not null && report is not null;
}

static bool IsInside(string root, string path)
{
    var relative = Path.GetRelativePath(root, path);
    return relative != ".."
        && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
        && !Path.IsPathRooted(relative);
}

static string FileSha(string path)
{
    using var stream = new FileStream(
        path,
        FileMode.Open,
        FileAccess.Read,
        FileShare.Read,
        81_920,
        FileOptions.SequentialScan);
    return Convert.ToHexStringLower(SHA256.HashData(stream));
}
