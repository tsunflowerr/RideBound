using RideBound.Runner.Configuration;
using RideBound.Runner.Protocol;

var executionMode = RunnerExecutionMode.OnlineRollingCost;
CommitmentPolicyConfiguration? commitmentConfiguration = null;

if (args.Length > 0)
{
    if (args.Length is not (2 or 4)
        || !string.Equals(args[0], "--mode", StringComparison.Ordinal)
        || args[1] is not ("online" or "conformance" or "commitment")
        || args.Length == 4
            && (!string.Equals(
                    args[2],
                    "--policy-config",
                    StringComparison.Ordinal)
                || args[1] != "commitment")
        || args[1] == "commitment" && args.Length != 4)
    {
        await Console.Error.WriteLineAsync(
            "Usage: RideBound.Runner [--mode online|conformance] | " +
            "--mode commitment --policy-config <path>");
        return 64;
    }

    executionMode = args[1] switch
    {
        "conformance" => RunnerExecutionMode.StructuralConformance,
        "commitment" => RunnerExecutionMode.OnlineCommitment,
        _ => RunnerExecutionMode.OnlineRollingCost,
    };

    if (executionMode == RunnerExecutionMode.OnlineCommitment)
    {
        try
        {
            commitmentConfiguration = CommitmentPolicyConfiguration.Decode(
                await File.ReadAllBytesAsync(args[3]));
        }
        catch (Exception error) when (
            error is IOException
                or UnauthorizedAccessException
                or InvalidDataException
                or ArgumentException
                or InvalidOperationException
                or System.Text.Json.JsonException
                or RideBound.Contracts.Serialization.CanonicalJsonException)
        {
            await Console.Error.WriteLineAsync(
                $"Invalid commitment policy configuration: {error.Message}");
            return 64;
        }
    }
}

return await RunnerHost.RunAsync(
    Console.OpenStandardInput(),
    Console.OpenStandardOutput(),
    Console.Error,
    executionMode: executionMode,
    commitmentPolicies: commitmentConfiguration,
    stopDistances: commitmentConfiguration,
    commitmentPolicyConfigurationHash: commitmentConfiguration?.ContentHash);
