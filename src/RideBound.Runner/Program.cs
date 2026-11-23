using RideBound.Contracts.Serialization;
using RideBound.Runner.Configuration;
using RideBound.Runner.Protocol;

var executionMode = RunnerExecutionMode.OnlineRollingCost;
RollingCostSolverConfiguration? solverConfiguration = null;

if (args.Length > 0)
{
    if (args is not (["--mode", "online" or "conformance"]
        or ["--mode", "online", "--rolling-cost-solver-config", _]))
    {
        await Console.Error.WriteLineAsync(
            "Usage: RideBound.Runner [--mode online|conformance] | " +
            "--mode online --rolling-cost-solver-config <path>");
        return 64;
    }

    executionMode = args[1] == "conformance"
        ? RunnerExecutionMode.StructuralConformance
        : RunnerExecutionMode.OnlineRollingCost;

    if (args.Length == 4)
    {
        try
        {
            solverConfiguration = RollingCostSolverConfiguration.Decode(
                await File.ReadAllBytesAsync(args[3]));
        }
        catch (Exception error) when (
            error is IOException or UnauthorizedAccessException or InvalidDataException
                or CanonicalJsonException or ArgumentException)
        {
            await Console.Error.WriteLineAsync($"Invalid rolling-cost solver configuration: {error.Message}");
            return 64;
        }
    }
}

return await RunnerHost.RunAsync(
    Console.OpenStandardInput(),
    Console.OpenStandardOutput(),
    Console.Error,
    executionMode: executionMode,
    solverConfiguration: solverConfiguration);
