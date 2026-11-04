using RideBound.Runner.Protocol;

var executionMode = RunnerExecutionMode.OnlineRollingCost;

if (args.Length > 0)
{
    if (args.Length != 2
        || !string.Equals(args[0], "--mode", StringComparison.Ordinal)
        || args[1] is not ("online" or "conformance"))
    {
        await Console.Error.WriteLineAsync(
            "Usage: RideBound.Runner [--mode online|conformance]");
        return 64;
    }

    executionMode = args[1] == "conformance"
        ? RunnerExecutionMode.StructuralConformance
        : RunnerExecutionMode.OnlineRollingCost;
}

return await RunnerHost.RunAsync(
    Console.OpenStandardInput(),
    Console.OpenStandardOutput(),
    Console.Error,
    executionMode: executionMode);
