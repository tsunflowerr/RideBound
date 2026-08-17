using System.Text.Json;
using RideBound.Wp6MetricOracle;

return Run(args);

static int Run(string[] arguments)
{
    try
    {
        var options = Arguments(arguments);
        var request = OracleRequest.Load(options["--request"]);
        var result = OracleCalculator.Calculate(request);
        WriteNew(options["--rows-out"], result.CanonicalRows);
        WriteNew(options["--summary-out"], result.Summary());
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
    if (arguments.Length != 6)
    {
        throw new ArgumentException("Expected request, rows output and summary output arguments.");
    }

    var values = new Dictionary<string, string>(StringComparer.Ordinal);

    for (var index = 0; index < arguments.Length; index += 2)
    {
        if (!values.TryAdd(arguments[index], Path.GetFullPath(arguments[index + 1])))
        {
            throw new ArgumentException("Oracle argument is duplicated.");
        }
    }

    if (!values.Keys.ToHashSet(StringComparer.Ordinal)
            .SetEquals(["--request", "--rows-out", "--summary-out"]))
    {
        throw new ArgumentException("Oracle arguments are invalid.");
    }

    return values;
}

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
