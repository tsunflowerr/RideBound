using System.Security.Cryptography;
using System.Text.Json.Nodes;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;
using RideBound.Runner.Configuration;

namespace RideBound.Runner.Tests.Online;

public sealed partial class OnlineRunnerSessionTests
{
    [Fact]
    public void Split_c1_configuration_binds_both_files_and_canonicalizes_formatting()
    {
        var (commitment, dispatch) = SplitC1Configuration(ReadFixture("runner/ridebound-hard-vector.config.json"));
        var commitmentBytes = Bytes(commitment);
        var dispatchBytes = Bytes(dispatch);
        var configured = C1RunnerConfiguration.DecodeSeparated(commitmentBytes, dispatchBytes);
        var expected = Wp4PolicyConfigurationBinding.Calculate(HashConfiguration(commitmentBytes), HashConfiguration(dispatchBytes));

        Assert.Equal(expected, configured.ContentHash);
        var formatted = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(dispatch,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        Assert.Equal(expected, C1RunnerConfiguration.DecodeSeparated(commitmentBytes, formatted).ContentHash);

        commitment["policies"]![0]!["materialRevisionRule"]!["rawEtaThresholdMs"] = 1_001;
        var changedPolicy = C1RunnerConfiguration.DecodeSeparated(Bytes(commitment), dispatchBytes);
        Assert.NotEqual(expected, changedPolicy.ContentHash);

        dispatch["auditHash"] = "light-v1";
        var changedDispatch = C1RunnerConfiguration.DecodeSeparated(commitmentBytes, Bytes(dispatch));
        Assert.NotEqual(expected, changedDispatch.ContentHash);
        Assert.True(changedDispatch.UsesLightAuditHash);

        var wrongManifest = CreateC1Session(changedDispatch).Process(Bytes(CreateC1Initialize(configured))).Response;
        Assert.Equal("error", wrongManifest?.MessageType.Value);
        Assert.Equal("initialized",
            CreateC1Session(configured).Process(Bytes(CreateC1Initialize(configured))).Response?.MessageType.Value);
    }

    [Fact]
    public void Split_c1_configuration_rejects_unknown_fields_and_requires_each_version()
    {
        var (commitment, dispatch) = SplitC1Configuration(ReadFixture("runner/ridebound-hard-vector.config.json"));
        commitment["configurationVersion"] = "2.0.0";
        Assert.Throws<InvalidDataException>(() => C1RunnerConfiguration.DecodeSeparated(Bytes(commitment), Bytes(dispatch)));
        commitment["configurationVersion"] = "1.0.0";

        dispatch["policies"] = commitment["policies"]!.DeepClone();
        var error = Assert.Throws<InvalidDataException>(
            () => C1RunnerConfiguration.DecodeSeparated(Bytes(commitment), Bytes(dispatch)));
        Assert.Equal("Unknown field 'policies' in dispatch.", error.Message);
        dispatch.Remove("policies");

        commitment.Remove("stopDistances");
        error = Assert.Throws<InvalidDataException>(
            () => C1RunnerConfiguration.DecodeSeparated(Bytes(commitment), Bytes(dispatch)));
        Assert.Equal("Missing field 'stopDistances' in commitment.", error.Message);
    }

    private static (JsonObject Commitment, JsonObject Dispatch) SplitC1Configuration(byte[] combined)
    {
        var dispatch = JsonNode.Parse(combined)!.AsObject();
        var commitment = new JsonObject
        {
            ["configurationVersion"] = dispatch["configurationVersion"]!.DeepClone(),
            ["policies"] = dispatch["policies"]!.DeepClone(),
            ["stopDistances"] = dispatch["stopDistances"]!.DeepClone(),
        };
        dispatch.Remove("policies");
        dispatch.Remove("stopDistances");
        return (commitment, dispatch);
    }

    private static Sha256Hex HashConfiguration(byte[] bytes)
    {
        Assert.True(Sha256Hex.TryCreate(
            Convert.ToHexStringLower(SHA256.HashData(CanonicalJson.Canonicalize(bytes))), out var digest));
        return digest!;
    }
}
