using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace RideBound.Runner.Tests.Online;

public sealed class CommitmentCheckpointProcessTests
{
    [Fact]
    public async Task Restored_clean_process_matches_genesis_suffix_decisions()
    {
        var scenarioLines = File.ReadAllLines(
            ScenarioPath("commitment-demo.input.ndjson"));
        var uninterrupted = await RunProcess(scenarioLines);
        Assert.Equal(0, uninterrupted.ExitCode);
        Assert.Equal(string.Empty, uninterrupted.StandardError);

        var checkpointRequest =
            "{\"schemaVersion\":\"1.0.0\",\"messageType\":\"checkpoint\"," +
            "\"runId\":\"wp3-demo-run\",\"scenarioId\":\"wp3-commitment-tiny\"," +
            "\"payload\":{}}";
        var prefix = await RunProcess(
            scenarioLines.Take(4)
                .Concat([checkpointRequest, scenarioLines[^1]]));
        Assert.Equal(0, prefix.ExitCode);
        Assert.Equal(string.Empty, prefix.StandardError);
        var checkpoint = Messages(prefix.StandardOutput).Single(
            value => value.RootElement.GetProperty("messageType").GetString()
                == "checkpoint");
        var payload = checkpoint.RootElement.GetProperty("payload");
        var restore =
            "{\"schemaVersion\":\"1.0.0\",\"messageType\":\"restore\"," +
            "\"runId\":\"wp3-demo-run\",\"scenarioId\":\"wp3-commitment-tiny\"," +
            $"\"payload\":{payload.GetRawText()}}}";
        var restored = await RunProcess(
            scenarioLines.Take(2)
                .Concat([restore])
                .Concat(scenarioLines.Skip(4)));

        Assert.Equal(0, restored.ExitCode);
        Assert.Equal(string.Empty, restored.StandardError);
        var uninterruptedSuffix = DecisionLines(
            uninterrupted.StandardOutput).Skip(1).ToArray();
        var restoredSuffix = DecisionLines(restored.StandardOutput);
        Assert.Equal(uninterruptedSuffix, restoredSuffix);
        var restoreAck = Messages(restored.StandardOutput).Single(
            value => value.RootElement.GetProperty("messageType").GetString()
                == "restore");
        Assert.Equal(
            payload.GetProperty("checkpointHash").GetString(),
            restoreAck.RootElement.GetProperty("payload")
                .GetProperty("checkpointHash").GetString());
    }

    private static async Task<ProcessResult> RunProcess(
        IEnumerable<string> inputLines)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add(
            Path.Combine(AppContext.BaseDirectory, "RideBound.Runner.dll"));
        startInfo.ArgumentList.Add("--mode");
        startInfo.ArgumentList.Add("commitment");
        startInfo.ArgumentList.Add("--policy-config");
        startInfo.ArgumentList.Add(ConfigurationPath());

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);
        await process.StandardInput.WriteAsync(
            string.Join('\n', inputLines) + "\n");
        process.StandardInput.Close();
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new ProcessResult(
            process.ExitCode,
            Normalize(stdout),
            Normalize(stderr));
    }

    private static string[] DecisionLines(string output) =>
        output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(
                line => JsonDocument.Parse(line).RootElement
                    .GetProperty("messageType").GetString() == "decision")
            .ToArray();

    private static JsonDocument[] Messages(string output) =>
        output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonDocument.Parse(line))
            .ToArray();

    private static string ScenarioPath(string name) => Path.Combine(
        RepositoryRoot(),
        "benchmarks",
        "scenarios",
        "wp3-commitment-tiny",
        name);

    private static string ConfigurationPath() => Path.Combine(
        RepositoryRoot(),
        "benchmarks",
        "configurations",
        "wp3-boundary-test-v1.json");

    private static string RepositoryRoot() =>
        typeof(CommitmentCheckpointProcessTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(value => value.Key == "RideBoundRepositoryRoot")
            .Value!;

    private static string Normalize(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal);

    private sealed record ProcessResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}
