using System.Reflection;
using System.Text;
using RideBound.Contracts.Serialization;
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

    [Fact]
    public void Drop_eta_deadline_is_read_when_present()
    {
        var configuration = CommitmentPolicyConfiguration.Decode(
            PolicyJson("\"dropEtaDeadlineSlackMs\":30000,\"dropEtaDeadlineTwoSided\":true"));

        Assert.True(configuration.TryGetPolicy("uniform-v1", out var policy));
        Assert.Equal(new Duration(30_000), policy.DropEtaDeadlineSlack);
        Assert.True(policy.DropEtaDeadlineTwoSided);
    }

    [Fact]
    public void Drop_eta_deadline_is_one_sided_unless_asked()
    {
        var configuration = CommitmentPolicyConfiguration.Decode(
            PolicyJson("\"dropEtaDeadlineSlackMs\":60000"));

        Assert.True(configuration.TryGetPolicy("uniform-v1", out var policy));
        Assert.Equal(new Duration(60_000), policy.DropEtaDeadlineSlack);
        Assert.False(policy.DropEtaDeadlineTwoSided);
    }

    [Fact]
    public void Drop_eta_deadline_is_disabled_when_absent()
    {
        var configuration = CommitmentPolicyConfiguration.Decode(PolicyJson(null));

        Assert.True(configuration.TryGetPolicy("uniform-v1", out var policy));
        Assert.Null(policy.DropEtaDeadlineSlack);
        Assert.False(policy.DropEtaDeadlineTwoSided);
    }

    [Theory]
    [InlineData("\"dropEtaDeadlineSlackMs\":0", "must be positive")]
    [InlineData("\"dropEtaDeadlineSlackMs\":-1", "canonical non-negative integer")]
    [InlineData(
        "\"dropEtaDeadlineSlackMs\":30000,\"dropEtaDeadlineTwoSided\":\"yes\"",
        "must be a boolean")]
    [InlineData(
        "\"dropEtaDeadlineSlackMs\":30000,\"dropEtaDeadlineTwoSided\":1",
        "must be a boolean")]
    [InlineData(
        "\"dropEtaDeadlineTwoSided\":true",
        "requires 'dropEtaDeadlineSlackMs'")]
    [InlineData(
        "\"dropEtaDeadlineSlack\":30000",
        "Unknown field 'dropEtaDeadlineSlack'")]
    public void Invalid_drop_eta_deadline_is_rejected(
        string extraFields,
        string expectedMessage)
    {
        var error = Assert.Throws<InvalidDataException>(
            () => CommitmentPolicyConfiguration.Decode(PolicyJson(extraFields)));

        Assert.Contains(expectedMessage, error.Message);
    }

    [Fact]
    public void Drop_eta_deadline_given_as_a_string_is_rejected()
    {
        // The shared integer reader used for every integer field of this file
        // (including freezeHorizonMs) calls TryGetInt64, which throws
        // InvalidOperationException on a string element. That is the reader's
        // existing behaviour; Program catches it and exits with code 64.
        Assert.ThrowsAny<InvalidOperationException>(
            () => CommitmentPolicyConfiguration.Decode(
                PolicyJson("\"dropEtaDeadlineSlackMs\":\"30000\"")));
    }

    [Theory]
    [InlineData("\"dropEtaDeadlineSlackMs\":null")]
    [InlineData("\"dropEtaDeadlineSlackMs\":30000,\"dropEtaDeadlineTwoSided\":null")]
    public void A_null_deadline_field_is_rejected_by_canonical_json(string extraFields)
    {
        // Canonicalisation rejects every JSON null before any field is read.
        var error = Assert.Throws<CanonicalJsonException>(
            () => CommitmentPolicyConfiguration.Decode(PolicyJson(extraFields)));

        Assert.Contains("Null is not permitted", error.Message);
    }

    [Fact]
    public void A_one_sided_flag_without_a_slack_leaves_the_deadline_disabled()
    {
        var configuration = CommitmentPolicyConfiguration.Decode(
            PolicyJson("\"dropEtaDeadlineTwoSided\":false"));

        Assert.True(configuration.TryGetPolicy("uniform-v1", out var policy));
        Assert.Null(policy.DropEtaDeadlineSlack);
        Assert.False(policy.DropEtaDeadlineTwoSided);
    }

    private static byte[] PolicyJson(string? extraFields)
    {
        var limits = string.Join(
            ",",
            CommitmentDimensionVocabulary.Ordered.Select(
                value =>
                    $"{{\"dimension\":\"{CommitmentDimensionVocabulary.ToProtocolValue(value)}\"," +
                    "\"applicablePhases\":[\"accepted\"]}"));
        var extra = extraFields is null ? string.Empty : "," + extraFields;
        return Encoding.UTF8.GetBytes(
            $"{{\"configurationVersion\":\"1.0.0\",\"policies\":[{{" +
            "\"policyId\":\"uniform-v1\",\"budgetBasis\":\"decisionInduced\"," +
            $"\"limits\":[{limits}],\"materialRevisionRule\":{{\"rawEtaThresholdMs\":1}}" +
            $"{extra}}}],\"stopDistances\":[]}}");
    }

    private static string RepositoryRoot() =>
        typeof(CommitmentPolicyConfigurationTests)
            .Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(value => value.Key == "RideBoundRepositoryRoot")
            .Value!;
}
