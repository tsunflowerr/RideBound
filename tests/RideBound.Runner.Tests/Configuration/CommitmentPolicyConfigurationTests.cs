using System.Reflection;
using System.Text;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Runner.Configuration;

namespace RideBound.Runner.Tests.Configuration;

public sealed class CommitmentPolicyConfigurationTests
{
    [Fact]
    public void Published_configuration_has_stable_canonical_hash_and_exact_policy()
    {
        var configuration = CommitmentPolicyConfiguration.Decode(
            File.ReadAllBytes(Path.Combine(
                RepositoryRoot(),
                "benchmarks",
                "configurations",
                "wp3-boundary-test-v1.json")));

        Assert.Equal(
            "d1be06163dd38de567e4489100acd05b74c41cc454300f7b7286b459355e928f",
            configuration.ContentHash.Value);
        Assert.True(configuration.TryGetPolicy("uniform-v1", out var policy));
        Assert.Equal(10, policy.Limits.Count);
        Assert.Equal(
            0,
            policy.Limits[
                CommitmentDimension.VehicleSwitchCount]
                .HardLimit);
        Assert.True(configuration.TryGetDistanceMillimeters(
            new NodeId("same"),
            new NodeId("same"),
            out var zero));
        Assert.Equal(0, zero);
        Assert.False(configuration.TryGetDistanceMillimeters(
            new NodeId("a"),
            new NodeId("b"),
            out _));
    }

    [Fact]
    public void Unknown_fields_are_rejected_before_policy_construction()
    {
        var json =
            """
            {
              "configurationVersion":"1.0.0",
              "policies":[],
              "stopDistances":[],
              "silentDefault":true
            }
            """;

        var error = Assert.Throws<InvalidDataException>(
            () => CommitmentPolicyConfiguration.Decode(
                Encoding.UTF8.GetBytes(json)));

        Assert.Contains("Unknown field 'silentDefault'", error.Message);
    }

    [Theory]
    [InlineData(
        "[\"accepted\",\"accepted\"]",
        "Duplicate commitment phase")]
    [InlineData(
        "[\"accepted\"]",
        "Same-node stop distance")]
    public void Ambiguous_phase_or_same_node_distance_is_rejected(
        string phases,
        string expectedMessage)
    {
        var distance = expectedMessage.StartsWith(
            "Same-node",
            StringComparison.Ordinal)
                ? "{\"fromNodeId\":\"n-1\",\"toNodeId\":\"n-1\",\"distanceMm\":7}"
                : string.Empty;
        var limits = string.Join(
            ",",
            CommitmentDimensionVocabulary.Ordered.Select(
                value =>
                    $"{{\"dimension\":\"{CommitmentDimensionVocabulary.ToProtocolValue(value)}\"," +
                    $"\"applicablePhases\":{phases}}}"));
        var json =
            $"{{\"configurationVersion\":\"1.0.0\",\"policies\":[{{" +
            "\"policyId\":\"uniform-v1\",\"budgetBasis\":\"decisionInduced\"," +
            $"\"limits\":[{limits}],\"materialRevisionRule\":{{\"rawEtaThresholdMs\":1}}" +
            $"}}],\"stopDistances\":[{distance}]}}";

        var error = Assert.Throws<InvalidDataException>(
            () => CommitmentPolicyConfiguration.Decode(
                Encoding.UTF8.GetBytes(json)));

        Assert.Contains(expectedMessage, error.Message);
    }

    private static string RepositoryRoot() =>
        typeof(CommitmentPolicyConfigurationTests)
            .Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(value => value.Key == "RideBoundRepositoryRoot")
            .Value!;
}
