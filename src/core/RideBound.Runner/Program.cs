using RideBound.Contracts.Serialization;
using RideBound.Runner.Configuration;
using RideBound.Runner.Protocol;

var executionMode = RunnerExecutionMode.OnlineRollingCost;
RollingCostSolverConfiguration? solverConfiguration = null;
C1RunnerConfiguration? c1Configuration = null;

if (args.Length > 0)
{
    if (args is not (["--mode", "online" or "conformance"]
        or ["--mode", "online", "--rolling-cost-solver-config" or "--c1-solver-config", _]
        or ["--mode", "online", "--c1-solver-config", _, "--commitment-config", _]))
    {
        await Console.Error.WriteLineAsync(
            "Usage: RideBound.Runner [--mode online|conformance] | " +
            "--mode online --rolling-cost-solver-config <path> | " +
            "--mode online --c1-solver-config <path> [--commitment-config <path>]");
        return 64;
    }

    executionMode = args[1] == "conformance"
        ? RunnerExecutionMode.StructuralConformance
        : RunnerExecutionMode.OnlineRollingCost;

    if (args.Length >= 4)
    {
        var isC1 = args[2] == "--c1-solver-config";
        try
        {
            var contents = await File.ReadAllBytesAsync(args[3]);
            if (args.Length == 6)
            {
                var commitment = await File.ReadAllBytesAsync(args[5]);
                c1Configuration = C1RunnerConfiguration.DecodeSeparated(commitment, contents);
            }
            else if (isC1)
            {
                c1Configuration = C1RunnerConfiguration.Decode(contents);
            }
            else
            {
                solverConfiguration = RollingCostSolverConfiguration.Decode(contents);
            }
        }
        catch (Exception error) when (
            error is IOException or UnauthorizedAccessException or InvalidDataException
                or CanonicalJsonException or ArgumentException)
        {
            var policyName = isC1 ? "C1" : "rolling-cost";
            await Console.Error.WriteLineAsync($"Invalid {policyName} solver configuration: {error.Message}");
            return 64;
        }
    }
}

return await RunnerHost.RunAsync(
    Console.OpenStandardInput(),
    Console.OpenStandardOutput(),
    Console.Error,
    executionMode: executionMode,
    solverConfiguration: solverConfiguration,
    c1Configuration: c1Configuration);
