using System.Text;
using System.Text.Json.Nodes;
using RideBound.Contracts.Serialization;
using RideBound.Contracts.Tests.Fixtures;
using RideBound.Runner.Configuration;

namespace RideBound.Runner.Tests.Online;

public sealed class RollingCostSolverConfigurationTests
{
    [Fact]
    public void Hash_ignores_field_order_but_binds_the_solver_seed_and_budgets()
    {
        var original = ReadConfiguration();
        var reversed = new JsonObject(original.Reverse().Select(
            field => KeyValuePair.Create(field.Key, field.Value?.DeepClone())));
        var originalConfiguration = Decode(original);

        Assert.Equal(originalConfiguration.ContentHash, Decode(reversed).ContentHash);
        Assert.Equal(100, originalConfiguration.CandidateGeneration.MaximumCandidatesPerVehicle);
        Assert.Equal(10_000, originalConfiguration.CandidateGeneration.MaximumExplorationWorkUnits);
        Assert.Equal(100_000, originalConfiguration.ExecutionBudget.MaximumGenerationWorkUnits);
        Assert.Equal(1_000, originalConfiguration.ExecutionBudget.MaximumValidationWorkUnits);
        Assert.Equal(7, originalConfiguration.ExecutionBudget.SolverBudget.RandomSeed);
        reversed["solverExecution"]!["randomSeed"] = 8;
        Assert.NotEqual(originalConfiguration.ContentHash, Decode(reversed).ContentHash);
    }

    [Theory]
    [InlineData("configurationVersion", "2.0.0")]
    [InlineData("policyId", "rolling-cost")]
    [InlineData("policyVersion", "")]
    public void Unsupported_config_identity_is_rejected(string key, string value)
    {
        var config = ReadConfiguration();
        config[key] = value;
        Assert.Throws<InvalidDataException>(() => Decode(config));
    }

    [Fact]
    public void Missing_unknown_and_duplicate_fields_are_rejected()
    {
        var config = ReadConfiguration();
        config["solverExecution"]!.AsObject().Remove("maximumValidationWorkUnits");
        Assert.Throws<InvalidDataException>(() => Decode(config));
        config = ReadConfiguration();
        config["candidateGeneration"]!["unknown"] = 1;
        Assert.Throws<InvalidDataException>(() => Decode(config));
        var duplicate = "{\"policyId\":\"rolling-cost-cp-sat\"," + ReadConfiguration().ToJsonString()[1..];
        var error = Assert.Throws<CanonicalJsonException>(
            () => RollingCostSolverConfiguration.Decode(Encoding.UTF8.GetBytes(duplicate)));
        Assert.Equal(CanonicalJsonErrorCode.DuplicateProperty, error.Code);
    }

    [Theory]
    [InlineData("maximumSolverWorkUnits", 0)]
    [InlineData("maximumValidationWorkUnits", -1)]
    [InlineData("randomSeed", 2_147_483_648L)]
    public void Invalid_execution_limits_are_rejected(string field, long value)
    {
        var config = ReadConfiguration();
        config["solverExecution"]![field] = value;
        Assert.Throws<InvalidDataException>(() => Decode(config));
    }

    [Fact]
    public void Configuration_rejects_a_different_adapter_version()
    {
        var config = ReadConfiguration();
        config["solverExecution"]!["adapterVersion"] = "other-adapter";
        Assert.Throws<InvalidDataException>(() => Decode(config));
    }

    private static JsonObject ReadConfiguration() =>
        JsonNode.Parse(FixtureLoader.ReadUtf8("runner/rolling-cost-cp-sat.config.json"))!.AsObject();

    private static RollingCostSolverConfiguration Decode(JsonObject config) =>
        RollingCostSolverConfiguration.Decode(Encoding.UTF8.GetBytes(config.ToJsonString()));
}
