using RideBound.Benchmarking.Bundles;
using RideBound.Benchmarking.EndToEnd;

return await RunAsync(args);

static async Task<int> RunAsync(string[] arguments)
{
    try
    {
        var values = Parse(arguments);
        var receipt = await PublicDerivativePairedHarness.RunAsync(
            new PublicDerivativeHarnessPaths(
                values["--repository"],
                values["--cache"],
                values["--work-root"],
                values["--bundle"],
                values["--receipt"],
                values["--configuration"]));
        Console.Out.WriteLine(
            System.Text.Encoding.UTF8.GetString(BundleEvidenceJson.Encode(receipt)));
        return 0;
    }
    catch (BundleVerificationException exception)
    {
        Console.Error.WriteLine(
            $"wp6.medium-harness-failed: stage={exception.Stage}; "
                + $"code={exception.Code}; path={exception.RelativePath}; "
                + exception.Message);
        return 2;
    }
    catch (Exception exception) when (
        exception is ArgumentException
            or IOException
            or InvalidDataException
            or InvalidOperationException
            or TimeoutException
            or RideBound.Benchmarking.Metrics.MechanicalMetricCalculationException)
    {
        Console.Error.WriteLine(
            $"wp6.medium-harness-failed: {exception.Message}");
        return 2;
    }
}

static IReadOnlyDictionary<string, string> Parse(string[] arguments)
{
    var required = new HashSet<string>(StringComparer.Ordinal)
    {
        "--bundle",
        "--cache",
        "--configuration",
        "--receipt",
        "--repository",
        "--work-root",
    };

    if (arguments.Length != required.Count * 2)
    {
        throw new ArgumentException(
            "usage: RideBound.Wp6MediumHarness --repository <root> "
                + "--cache <verified-cache-outside-repo> "
                + "--work-root <new-external-dir> --bundle <new-dir> "
                + "--receipt <new-file> --configuration Debug|Release");
    }

    var values = new Dictionary<string, string>(StringComparer.Ordinal);

    for (var index = 0; index < arguments.Length; index += 2)
    {
        if (!required.Contains(arguments[index])
            || !values.TryAdd(arguments[index], arguments[index + 1]))
        {
            throw new ArgumentException(
                "Medium harness arguments are invalid or duplicated.");
        }
    }

    return values.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(required)
        ? values
        : throw new ArgumentException("Medium harness arguments are incomplete.");
}
