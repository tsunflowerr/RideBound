using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using RideBound.Algorithms.Candidates;
using RideBound.Application.State;
using RideBound.Application.Travel;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;
using RideBound.Domain.Runs;
using RideBound.Domain.Validation;
using RideBound.Domain.Vehicles;

const int repetitions = 7;
const int keyIterations = 250_000;
var outputPath = args.Length == 2 && args[0] == "--output"
    ? Path.GetFullPath(args[1])
    : null;
var keyMeasurements = new List<KeyMeasurement>();
var generatorMeasurements = new List<GeneratorMeasurement>();

foreach (var incumbentCount in new[] { 2, 4, 8 })
{
    var state = CreateLoadedState(incumbentCount);
    var vehicle = state.Run.Vehicles.Values.Single();
    var route = vehicle.Route;
    var samples = new List<Sample>();
    var checksum = 0;

    for (var index = 0; index < 10_000; index++)
    {
        checksum ^= ForwardSlackCacheKey.Create(
                state,
                vehicle,
                route,
                state.TravelTimes!,
                state.Run.SimulationTime,
                ServiceQualityAllowance.Strict)
            .GetHashCode();
    }

    for (var repetition = 0; repetition < repetitions; repetition++)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var beforeAllocated = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = Stopwatch.StartNew();

        for (var index = 0; index < keyIterations; index++)
        {
            checksum ^= ForwardSlackCacheKey.Create(
                    state,
                    vehicle,
                    route,
                    state.TravelTimes!,
                    state.Run.SimulationTime,
                    ServiceQualityAllowance.Strict)
                .GetHashCode();
        }

        stopwatch.Stop();
        samples.Add(
            new Sample(
                ElapsedMicroseconds(stopwatch),
                GC.GetAllocatedBytesForCurrentThread() - beforeAllocated));
    }

    keyMeasurements.Add(
        new KeyMeasurement(
            route.RemainingStops.Count(),
            keyIterations,
            repetitions,
            Median(samples.Select(value => value.WallTimeMicros)),
            Median(samples.Select(value => value.AllocatedBytes)),
            checksum));
}

var generationOptions = new CandidateGenerationOptions(
    maximumCandidatesPerVehicle: 100,
    maximumNewRequestsPerVehicle: 2,
    exactSmallMode: false,
    scheduleStrategy: CandidateScheduleStrategy.EarliestFeasible,
    maximumExplorationWorkUnits: 10_000);

foreach (var incumbentCount in new[] { 2, 4, 6 })
{
    var state = CreateLoadedState(incumbentCount);
    _ = RunGeneration(state, generationOptions);
    var samples = new List<Sample>();
    VehicleCandidateLoss? lastLoss = null;
    long lastMisses = 0;

    for (var repetition = 0; repetition < repetitions; repetition++)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var beforeAllocated = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = Stopwatch.StartNew();
        var measured = RunGeneration(state, generationOptions);
        stopwatch.Stop();
        samples.Add(
            new Sample(
                ElapsedMicroseconds(stopwatch),
                GC.GetAllocatedBytesForCurrentThread() - beforeAllocated));
        lastLoss = measured.Loss;
        lastMisses = measured.SlackMisses;
    }

    generatorMeasurements.Add(
        new GeneratorMeasurement(
            incumbentCount,
            incumbentCount * 2,
            repetitions,
            Median(samples.Select(value => value.WallTimeMicros)),
            Median(samples.Select(value => value.AllocatedBytes)),
            lastLoss!.ExplorationWorkUnits,
            lastLoss.EvaluatedCandidatePathCount,
            lastLoss.UniqueFeasibleCandidateCountBeforeCap,
            lastLoss.OmittedUnexpandedCandidatePathCount,
            lastLoss.RetainedCandidateCount,
            lastMisses));
}

var report = new Report(
    "candidate-hot-path-exact-reuse-v1",
    DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
    Environment.Version.ToString(),
    RuntimeInformation.OSDescription,
    RuntimeInformation.ProcessArchitecture.ToString(),
    Environment.ProcessorCount,
    "Machine-local timing and allocation evidence only. Exact work counters and differential outputs govern semantic equivalence.",
    keyMeasurements,
    generatorMeasurements);
var json = JsonSerializer.Serialize(
    report,
    new JsonSerializerOptions { WriteIndented = true });

if (outputPath is not null)
{
    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
    File.WriteAllText(outputPath, json + Environment.NewLine);
}

Console.WriteLine(json);

static GenerationResult RunGeneration(
    OnlineState state,
    CandidateGenerationOptions options)
{
    var cache = new ForwardSlackProfileCache(
        new ForwardSlackProfileBuilder(),
        maximumEntries: 1_000_000);
    var generated = new InsertionCandidateGenerator(slackCache: cache)
        .Generate(state, options);

    if (!generated.IsSuccess)
    {
        throw new InvalidOperationException(generated.Witness!.Message);
    }

    return new GenerationResult(
        generated.Diagnostics!.VehicleLosses.Single(),
        cache.MissCount);
}

static OnlineState CreateLoadedState(int incumbentCount)
{
    var nodes = Enumerable.Range(0, 2 * incumbentCount + 6)
        .Select(index => new NodeId($"n{index:D3}"))
        .ToArray();
    var arcs = new List<KeyValuePair<TravelArc, Duration>>();

    for (var from = 0; from < nodes.Length; from++)
    {
        for (var to = 0; to < nodes.Length; to++)
        {
            if (from != to)
            {
                arcs.Add(
                    new KeyValuePair<TravelArc, Duration>(
                        new TravelArc(nodes[from], nodes[to]),
                        new Duration(60 + Math.Abs(to - from) * 7)));
            }
        }
    }

    var requests = new List<RideRequest>();
    var suffix = new List<RouteStop>();

    for (var index = 0; index < incumbentCount; index++)
    {
        var request = RideRequest.CreatePending(
            new RequestId($"inc-{index:D2}"),
            new SimTime(0),
            nodes[2 * index + 1],
            nodes[2 * index + 2],
            new SimTime(0),
            new SimTime(900_000),
            new Duration(900_000),
            1,
            "standard",
            "uniform-v1").Value!;
        requests.Add(request);
        suffix.Add(
            new RouteStop(
                new StopId($"inc-{index:D2}-p"),
                request.OriginNodeId,
                RouteStopKind.Pickup,
                request.Id,
                new Duration(0)));
        suffix.Add(
            new RouteStop(
                new StopId($"inc-{index:D2}-d"),
                request.DestinationNodeId,
                RouteStopKind.DropOff,
                request.Id,
                new Duration(0)));
    }

    for (var index = 0; index < 2; index++)
    {
        requests.Add(
            RideRequest.CreatePending(
                new RequestId($"new-{index:D2}"),
                new SimTime(0),
                nodes[^(2 * index + 2)],
                nodes[^(2 * index + 1)],
                new SimTime(0),
                new SimTime(900_000),
                new Duration(900_000),
                1,
                "standard",
                "uniform-v1").Value!);
    }

    var route = RoutePlan.Create(new PlanVersion(0), 0, [], suffix).Value!;
    var vehicle = VehicleState.Create(
        new VehicleId("vehicle-1"),
        incumbentCount + 4,
        0,
        new NodePosition(nodes[0]),
        [],
        [],
        route,
        1).Value!;
    var run = RideBoundRun.Create(
        new RunIdentifier("hot-path-benchmark"),
        new ScenarioIdentifier("hot-path-benchmark"),
        new SimTime(0));

    foreach (var request in requests)
    {
        run = run.AddRequest(request).Value!;
    }

    run = run.BootstrapVehicle(vehicle).Value!;

    foreach (var request in requests.Take(incumbentCount))
    {
        run = run.AcceptRequest(request.Id, vehicle.Id).Value!;
    }

    var travel = TravelTimeSnapshot.Create(1, new string('a', 64), arcs).Value!;
    return new OnlineState(
        run,
        travel,
        1,
        travel.SnapshotHash,
        RideBound.Domain.Commitments.CommitmentLedger.Empty);
}

static long ElapsedMicroseconds(Stopwatch stopwatch) =>
    checked(stopwatch.ElapsedTicks * 1_000_000 / Stopwatch.Frequency);

static long Median(IEnumerable<long> values)
{
    var ordered = values.Order().ToArray();
    return ordered[ordered.Length / 2];
}

internal sealed record Sample(long WallTimeMicros, long AllocatedBytes);

internal sealed record KeyMeasurement(
    int RouteStopCount,
    int Iterations,
    int Repetitions,
    long WallTimeP50Micros,
    long AllocatedBytesP50,
    int Checksum);

internal sealed record GeneratorMeasurement(
    int IncumbentCount,
    int MutableStopCount,
    int Repetitions,
    long WallTimeP50Micros,
    long AllocatedBytesP50,
    long ExplorationWorkUnits,
    long EvaluatedCandidatePathCount,
    long UniqueFeasibleCandidateCountBeforeCap,
    long OmittedUnexpandedCandidatePathCount,
    long RetainedCandidateCount,
    long SlackProfileMissCount);

internal sealed record GenerationResult(
    VehicleCandidateLoss Loss,
    long SlackMisses);

internal sealed record Report(
    string BenchmarkId,
    string ObservedAtUtc,
    string DotnetRuntime,
    string OperatingSystem,
    string ProcessArchitecture,
    int ProcessorCount,
    string InterpretationGuard,
    IReadOnlyList<KeyMeasurement> KeyMeasurements,
    IReadOnlyList<GeneratorMeasurement> GeneratorMeasurements);
