using System.Diagnostics;
using System.Reflection;

if (args.Length == 0)
{
    await Console.Error.WriteLineAsync("A deterministic fake-child mode is required.");
    return 64;
}

switch (args[0])
{
    case "consume":
        await Console.OpenStandardInput().CopyToAsync(Stream.Null);
        return 0;
    case "no-output":
        await Console.OpenStandardInput().CopyToAsync(Stream.Null);
        return 0;
    case "hang":
        await Task.Delay(Timeout.InfiniteTimeSpan);
        return 0;
    case "cpu-burn":
        var accumulator = 0UL;

        while (true)
        {
            accumulator = unchecked((accumulator * 6364136223846793005UL) + 1UL);
            GC.KeepAlive(accumulator);
        }
    case "stdout-flood":
        await WriteBytes(Console.OpenStandardOutput(), ParseCount(args));
        return 0;
    case "stderr-flood":
        await WriteBytes(Console.OpenStandardError(), ParseCount(args));
        return 0;
    case "crash":
        await Console.Error.WriteLineAsync("deterministic fake crash");
        return 17;
    case "child-hang":
        return await SpawnHangingChild();
    case "mutate":
        if (args.Length != 2)
        {
            return 64;
        }

        await File.AppendAllTextAsync(args[1], "mutation");
        return 0;
    default:
        await Console.Error.WriteLineAsync($"Unknown fake-child mode '{args[0]}'.");
        return 64;
}

static long ParseCount(string[] values) =>
    values.Length == 2
        && long.TryParse(
            values[1],
            System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture,
            out var count)
        && count > 0
        ? count
        : throw new ArgumentException("A positive byte count is required.");

static async Task WriteBytes(Stream stream, long count)
{
    var buffer = Enumerable.Repeat((byte)'x', 8_192).ToArray();

    while (count > 0)
    {
        var take = checked((int)Math.Min(count, buffer.Length));
        await stream.WriteAsync(buffer.AsMemory(0, take));
        await stream.FlushAsync();
        count -= take;
    }
}

static async Task<int> SpawnHangingChild()
{
    var host = Environment.ProcessPath
        ?? throw new InvalidOperationException("Managed host path is unavailable.");
    var startInfo = new ProcessStartInfo(host)
    {
        RedirectStandardOutput = false,
        RedirectStandardError = false,
        RedirectStandardInput = false,
        UseShellExecute = false,
        CreateNoWindow = true,
    };

    if (string.Equals(
        Path.GetFileNameWithoutExtension(host),
        "dotnet",
        StringComparison.OrdinalIgnoreCase))
    {
        startInfo.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
    }

    startInfo.ArgumentList.Add("hang");
    using var child = Process.Start(startInfo)
        ?? throw new InvalidOperationException("Fake descendant did not start.");
    await Console.Out.WriteLineAsync(
        child.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
    await Console.Out.FlushAsync();
    await Task.Delay(Timeout.InfiniteTimeSpan);
    return 0;
}
