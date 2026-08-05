using RideBound.Runner.Configuration;
using RideBound.Runner.Protocol;

var executionMode = RunnerExecutionMode.OnlineRollingCost;
CommitmentPolicyConfiguration? commitmentConfiguration = null;
Wp4RunnerConfiguration? wp4Configuration = null;

if (args.Length > 0)
{
    var isSimpleMode = args.Length == 2
        && args[1] is "online" or "conformance";
    var isCommitmentMode = args.Length is 4 or 6
        && args[1] == "commitment"
        && string.Equals(args[2], "--policy-config", StringComparison.Ordinal)
        && (args.Length == 4
            || string.Equals(args[4], "--wp4-config", StringComparison.Ordinal));

    if (!isSimpleMode && !isCommitmentMode
        || !string.Equals(args[0], "--mode", StringComparison.Ordinal)
        || args[1] is not ("online" or "conformance" or "commitment"))
    {
        await Console.Error.WriteLineAsync(
            "Usage: RideBound.Runner [--mode online|conformance] | " +
            "--mode commitment --policy-config <path> " +
            "[--wp4-config <path>]");
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

            if (args.Length == 6)
            {
                wp4Configuration = Wp4RunnerConfiguration.Decode(
                    await File.ReadAllBytesAsync(args[5]),
                    commitmentConfiguration);
            }
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
    commitmentPolicyConfigurationHash: commitmentConfiguration is null
        ? null
        : wp4Configuration?.BindToCommitmentConfiguration(
            commitmentConfiguration.ContentHash)
            ?? commitmentConfiguration.ContentHash,
    wp4Configuration: wp4Configuration);
