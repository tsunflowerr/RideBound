using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using RideBound.Application.Optimization;
using RideBound.Domain.Common;
using RideBound.Solvers.OrTools;

const int repetitionCount = 7;
var scenarios = new[]
{
    new Scenario(2, 2),
    new Scenario(4, 4),
    new Scenario(8, 4),
    new Scenario(16, 8),
};
var measurements = new List<Measurement>();

foreach (var scenario in scenarios)
{
    var problem = CreateProblem(scenario.VehicleCount, scenario.OptionsPerVehicle);
    var budget = DeterministicSolverBudget.Create(
        maximumWorkUnits: 10_000_000,
        maximumDeterministicTimeMicros: 100_000_000,
        randomSeed: 20260803).Value!;
    var solver = new OrToolsCandidateSelectionSolver();
    _ = solver.Solve(problem, budget);
    var wallMicros = new List<long>();
    var deterministicMicros = new List<long>();
    var workUnits = new List<long>();

    for (var repetition = 0; repetition < repetitionCount; repetition++)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = solver.Solve(problem, budget);
        stopwatch.Stop();

        if (result.Status != CandidateSelectionSolveStatus.Optimal)
        {
            throw new InvalidOperationException(
                $"Microbenchmark requires exact completion; got {result.Status}.");
        }

        wallMicros.Add(
            checked(stopwatch.ElapsedTicks * 1_000_000 / Stopwatch.Frequency));
        deterministicMicros.Add(
            result.Diagnostics.ConsumedDeterministicTimeMicros);
        workUnits.Add(result.Diagnostics.ConsumedWorkUnits);
    }

    wallMicros.Sort();
    deterministicMicros.Sort();
    workUnits.Sort();
    measurements.Add(
        new Measurement(
            scenario.VehicleCount,
            scenario.OptionsPerVehicle,
            problem.Options.Count,
            problem.RequestIds.Count,
            problem.ObjectiveLevels.Count,
            repetitionCount,
            Percentile(wallMicros, 50),
            Percentile(wallMicros, 95),
            Percentile(deterministicMicros, 50),
            Percentile(workUnits, 50),
            "optimal"));
}

var report = new Report(
    "wp4-candidate-selection-synthetic-microbenchmark-v1",
    DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
    OrToolsCandidateSelectionSolver.AdapterVersion,
    Environment.Version.ToString(),
    RuntimeInformation.OSDescription,
    RuntimeInformation.ProcessArchitecture.ToString(),
    Environment.ProcessorCount,
    "Observed wall time is descriptive only; deterministic solver counters govern replay outcomes.",
    measurements);
Console.WriteLine(
    JsonSerializer.Serialize(
        report,
        new JsonSerializerOptions { WriteIndented = true }));

static CandidateSelectionProblem CreateProblem(
    int vehicleCount,
    int optionsPerVehicle)
{
    var vehicles = Enumerable.Range(0, vehicleCount)
        .Select(index => new VehicleId($"vehicle-{index:D2}"))
        .ToArray();
    var requestCount = Math.Max(vehicleCount, optionsPerVehicle - 1);
    var requests = Enumerable.Range(0, requestCount)
        .Select(index => new RequestId($"request-{index:D2}"))
        .ToArray();
    var levels = new[]
    {
        new CandidateSelectionObjectiveLevel(
            "accepted",
            CandidateSelectionObjectiveSense.Maximize,
            CandidateSelectionObjectiveAggregation.Sum),
        new CandidateSelectionObjectiveLevel(
            "worst-policy",
            CandidateSelectionObjectiveSense.Minimize,
            CandidateSelectionObjectiveAggregation.Maximum),
        new CandidateSelectionObjectiveLevel(
            "revision",
            CandidateSelectionObjectiveSense.Minimize,
            CandidateSelectionObjectiveAggregation.Sum),
        new CandidateSelectionObjectiveLevel(
            "cost",
            CandidateSelectionObjectiveSense.Minimize,
            CandidateSelectionObjectiveAggregation.Sum),
    };
    var options = new List<CandidateSelectionOption>();

    for (var vehicle = 0; vehicle < vehicleCount; vehicle++)
    {
        options.Add(
            new CandidateSelectionOption(
                $"v{vehicle:D2}-noop",
                vehicles[vehicle],
                [],
                [0, vehicle * 17, 0, vehicle * 11],
                true));

        for (var option = 1; option < optionsPerVehicle; option++)
        {
            var request = requests[(vehicle + option - 1) % requestCount];
            options.Add(
                new CandidateSelectionOption(
                    $"v{vehicle:D2}-o{option:D2}",
                    vehicles[vehicle],
                    [request],
                    [
                        1,
                        (vehicle * 101 + option * 37) % 1_000_001,
                        (vehicle * 29 + option * 13) % 1000,
                        (vehicle * 43 + option * 19) % 10_000,
                    ],
                    false));
        }
    }

    return CandidateSelectionProblem.Create(
        vehicles,
        requests,
        levels,
        options).Value!;
}

static long Percentile(IReadOnlyList<long> sorted, int percentile)
{
    var index = (int)Math.Ceiling(percentile / 100d * sorted.Count) - 1;
    return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
}

internal sealed record Scenario(int VehicleCount, int OptionsPerVehicle);

internal sealed record Measurement(
    int VehicleCount,
    int OptionsPerVehicle,
    int BoolVariableCount,
    int RequestCount,
    int ObjectiveLevelCount,
    int Repetitions,
    long WallTimeP50Micros,
    long WallTimeP95Micros,
    long DeterministicTimeP50Micros,
    long ConflictWorkP50,
    string Status);

internal sealed record Report(
    string BenchmarkId,
    string ObservedAtUtc,
    string SolverAdapter,
    string DotnetRuntime,
    string OperatingSystem,
    string ProcessArchitecture,
    int ProcessorCount,
    string InterpretationGuard,
    IReadOnlyList<Measurement> Measurements);
