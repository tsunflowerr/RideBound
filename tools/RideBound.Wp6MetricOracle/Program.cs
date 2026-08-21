using System.Text.Json;
using RideBound.Wp6MetricOracle;

return Run(args);

static int Run(string[] arguments)
{
    try
    {
        var options = Arguments(arguments);
        if (options.ContainsKey("--request"))
        {
            var request = OracleRequest.Load(FullPath(options, "--request"));
            var result = OracleCalculator.Calculate(request);
            WriteNew(FullPath(options, "--rows-out"), result.CanonicalRows);
            WriteNew(FullPath(options, "--summary-out"), result.Summary());
        }
        else
        {
            var result = DecisionInducedBurdenOracle.Calculate(
                options["--burden-run-id"],
                options["--burden-arm-id"],
                options["--burden-policy-id"],
                File.ReadAllBytes(FullPath(options, "--input-transcript")),
                File.ReadAllBytes(FullPath(options, "--output-transcript")));
            WriteNew(FullPath(options, "--burden-out"), result.Encode());
        }

        return 0;
    }
    catch (OracleException exception)
    {
        Console.Error.WriteLine($"{exception.Code}: {exception.Message}");
        return 2;
    }
    catch (Exception exception) when (
        exception is ArgumentException
            or IOException
            or JsonException
            or InvalidOperationException
            or OverflowException)
    {
        Console.Error.WriteLine("oracle.input-invalid: Oracle input could not be verified.");
        return 2;
    }
}

static Dictionary<string, string> Arguments(string[] arguments)
{
    if (arguments.Length is not 6 and not 12)
    {
        throw new ArgumentException("Oracle argument count is invalid.");
    }

    var values = new Dictionary<string, string>(StringComparer.Ordinal);

    for (var index = 0; index < arguments.Length; index += 2)
    {
        if (!values.TryAdd(arguments[index], arguments[index + 1]))
        {
            throw new ArgumentException("Oracle argument is duplicated.");
        }
    }

    var keys = values.Keys.ToHashSet(StringComparer.Ordinal);
    var metricMode = keys.SetEquals(
        ["--request", "--rows-out", "--summary-out"]);
    var burdenMode = keys.SetEquals(
        [
            "--burden-run-id",
            "--burden-arm-id",
            "--burden-policy-id",
            "--input-transcript",
            "--output-transcript",
            "--burden-out",
        ]);

    if (!metricMode && !burdenMode)
    {
        throw new ArgumentException("Oracle arguments are invalid.");
    }

    return values;
}

static string FullPath(IReadOnlyDictionary<string, string> values, string key) =>
    Path.GetFullPath(values[key]);

static void WriteNew(string path, byte[] bytes)
{
    var parent = Path.GetDirectoryName(path)
        ?? throw new ArgumentException("Oracle output directory is missing.");
    Directory.CreateDirectory(parent);

    using var stream = new FileStream(
        path,
        FileMode.CreateNew,
        FileAccess.Write,
        FileShare.None,
        bufferSize: 64 * 1024,
        FileOptions.WriteThrough);
    stream.Write(bytes);
    stream.Flush(flushToDisk: true);
}
