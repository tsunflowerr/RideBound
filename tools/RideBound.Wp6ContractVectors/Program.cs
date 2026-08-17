using RideBound.Benchmarking.Contracts;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: RideBound.Wp6ContractVectors <repository-root>");
    return 2;
}

var fixtureRoot = Path.Combine(
    Path.GetFullPath(args[0]),
    "benchmarks",
    "fixtures",
    "wp6",
    "contracts",
    "positive");

static BenchmarkDecodeResult<T> Read<T>(string root, string file)
    where T : class, IBenchmarkDocument =>
    BenchmarkContractCodec.Decode<T>(File.ReadAllBytes(Path.Combine(root, file)));

var scenario = Read<ScenarioContent>(fixtureRoot, "scenario-content.json");
var report = Read<NormalizationReport>(fixtureRoot, "normalization-report.json");
var plan = Read<BenchmarkPlan>(fixtureRoot, "benchmark-plan.json");
var metric = Read<MetricRow>(fixtureRoot, "metric-row.json");
var bundle = Read<LogicalBundleManifest>(fixtureRoot, "bundle-manifest.json");

foreach (var result in new (bool Success, BenchmarkContractError? Error)[]
{
    (scenario.IsSuccess, scenario.Error),
    (report.IsSuccess, report.Error),
    (plan.IsSuccess, plan.Error),
    (metric.IsSuccess, metric.Error),
    (bundle.IsSuccess, bundle.Error),
})
{
    if (!result.Success)
    {
        Console.Error.WriteLine($"{result.Error!.Code}|{result.Error.Path}|{result.Error.Message}");
        return 3;
    }
}

var scenarioHash = BenchmarkIdentity.CalculateScenario(scenario.CanonicalBytes!);
var reportHash = BenchmarkIdentity.CalculateNormalizationReport(report.CanonicalBytes!);
var planHash = BenchmarkIdentity.CalculateBenchmarkPlan(plan.CanonicalBytes!);
var runId = BenchmarkIdentity.CalculateRun(planHash, scenarioHash, "b1", 0, 0);
var metricRows = new byte[metric.CanonicalBytes!.Length + 2];
metricRows[0] = (byte)'[';
metric.CanonicalBytes.CopyTo(metricRows, 1);
metricRows[^1] = (byte)']';
var metricSetHash = BenchmarkIdentity.CalculateMetricSet(
    runId,
    metric.Value!.MetricRegistryHash,
    metricRows);
var bundleHash = BenchmarkIdentity.CalculateBundle(bundle.CanonicalBytes!);

Console.WriteLine(
    string.Join(
        '|',
        scenarioHash,
        reportHash,
        planHash,
        runId,
        metricSetHash,
        bundleHash));
return 0;
