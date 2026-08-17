using System.Diagnostics;
using System.Text;
using RideBound.Benchmarking.Bundles;
using RideBound.Benchmarking.Contracts;
using RideBound.Benchmarking.EndToEnd;

namespace RideBound.Benchmarking.Tests;

public sealed class TinyPairedHarnessProcessTests
{
    [Fact]
    public async Task Two_clean_harness_processes_reproduce_all_semantic_identities()
    {
        using var temp = new TestDirectory();
        var first = await RunAsync(temp.Root, "first");
        var second = await RunAsync(temp.Root, "second");

        Assert.Equal(first.PlanHash, second.PlanHash);
        Assert.Equal(first.ScenarioHash, second.ScenarioHash);
        Assert.Equal(first.SourceFixtureSha256, second.SourceFixtureSha256);
        Assert.Equal(first.RuntimeInventorySha256, second.RuntimeInventorySha256);
        Assert.Equal(first.SourceInventorySha256, second.SourceInventorySha256);
        Assert.Equal(first.RunGridSha256, second.RunGridSha256);
        Assert.Equal(first.TranscriptSetSha256, second.TranscriptSetSha256);
        Assert.Equal(first.DecisionSetSha256, second.DecisionSetSha256);
        Assert.Equal(first.SemanticMetricSetSha256, second.SemanticMetricSetSha256);
        Assert.Equal(8, first.PlannedRunCount);
        Assert.Equal(8, first.SucceededRunCount);
        Assert.Equal(0, first.FailedRunCount);
        Assert.Equal(0, first.ExcludedRunCount);
        Assert.Equal(
            SemanticRuns(first.Runs),
            SemanticRuns(second.Runs));
        Assert.NotEqual(first.BundleDirectory, second.BundleDirectory);
        Assert.True(Directory.Exists(first.BundleDirectory));
        Assert.True(Directory.Exists(second.BundleDirectory));
        var planResult = BenchmarkContractCodec.Decode<BenchmarkPlan>(
            File.ReadAllBytes(
                Path.Combine(
                    first.BundleDirectory,
                    "data",
                    "benchmark-plan.json")));
        Assert.True(planResult.IsSuccess, planResult.Error?.ToString());
        Assert.Equal(1, planResult.Value!.WarmupRunCount);
        Assert.Equal(3, planResult.Value.MeasuredRepeatCount);
        Assert.Equal(2, first.Runs.Count(value => value.RepeatIndex == 0));
        Assert.Equal(6, first.Runs.Count(value => value.RepeatIndex is >= 1 and <= 3));
        var firstArmByRepeat = first.Runs
            .GroupBy(value => value.RepeatIndex)
            .OrderBy(value => value.Key)
            .Select(
                value => value.OrderBy(run => run.ExecutionOrdinal)
                    .First()
                    .ArmId)
            .ToArray();
        Assert.Equal(2, firstArmByRepeat.Distinct(StringComparer.Ordinal).Count());
    }

    private static string[] SemanticRuns(IReadOnlyList<TinyRunIdentityReceipt> runs) =>
        runs.OrderBy(value => value.RunId, StringComparer.Ordinal)
            .Select(
                value => string.Join(
                    '|',
                    value.RunId,
                    value.ArmId,
                    value.RepeatIndex,
                    value.ExecutionOrdinal,
                    value.InputSha256,
                    value.OutputSha256,
                    value.ObservationIndexSha256,
                    value.DecisionSequenceSha256,
                    value.SemanticMetricRowsSha256))
            .ToArray();

    private static async Task<TinyPairedHarnessReceipt> RunAsync(
        string root,
        string processId)
    {
        var repository = StrictBundleTestFixture.FindRepositoryRoot();
        var configuration = Directory.GetParent(
            Directory.GetParent(AppContext.BaseDirectory)!.FullName)!.Name;
        var executable = Path.Combine(
            repository,
            "tools",
            "RideBound.Wp6TinyHarness",
            "bin",
            configuration,
            "net10.0",
            OperatingSystem.IsWindows()
                ? "RideBound.Wp6TinyHarness.exe"
                : "RideBound.Wp6TinyHarness");
        Assert.True(File.Exists(executable), executable);
        var processRoot = Path.Combine(root, processId);
        var workRoot = Path.Combine(processRoot, "work");
        var bundleRoot = Path.Combine(processRoot, "bundle");
        var receiptPath = Path.Combine(processRoot, "receipt.json");
        Directory.CreateDirectory(processRoot);
        var start = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = repository,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        foreach (var argument in new[]
        {
            "--repository", repository,
            "--work-root", workRoot,
            "--bundle", bundleRoot,
            "--receipt", receiptPath,
            "--configuration", configuration,
        })
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("Tiny harness process did not start.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Tiny harness process exceeded two minutes.");
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        Assert.True(
            process.ExitCode == 0,
            $"exit={process.ExitCode}; stderr={stderr}; stdout={stdout}");
        Assert.Equal(string.Empty, stderr);
        Assert.True(File.Exists(receiptPath), receiptPath);
        var receiptBytes = File.ReadAllBytes(receiptPath);
        Assert.Equal(
            Encoding.UTF8.GetString(receiptBytes),
            stdout.TrimEnd('\r', '\n'));
        return BundleEvidenceJson.DecodeExact<TinyPairedHarnessReceipt>(receiptBytes);
    }
}
