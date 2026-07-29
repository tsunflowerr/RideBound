using System.Text;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;
using RideBound.Contracts.Tests.Fixtures;

namespace RideBound.Contracts.Tests.Serialization;

public sealed class ProtocolHashTests
{
    [Fact]
    public void Published_manifest_vector_matches_exact_sha256()
    {
        var manifest = ReadManifest();

        var hash = ProtocolHash.CalculateManifestHash(manifest);

        Assert.Equal(
            "1f5d3769c091b3ac9e20ac7eeecd386cc0f0235d03cceccd6422db8518ab4534",
            hash.Value);
    }

    [Fact]
    public void Published_decision_vector_matches_exact_sha256()
    {
        Sha256Hex.TryCreate(
            "1111111111111111111111111111111111111111111111111111111111111111",
            out var manifestHash);
        var input = CanonicalJson.Canonicalize(
            """{"epochId":1,"events":[1,2]}"""u8);
        var decision = CanonicalJson.Canonicalize(
            """{"actions":[],"status":"notProduced"}"""u8);

        var hash = ProtocolHash.CalculateDecisionHash(
            ProtocolHash.ZeroHash,
            manifestHash!,
            "1.0.0",
            input,
            decision);

        Assert.Equal(
            "9939c29a73091766d1acda00984496c5e54aa66fa56fcd258604852accb98c4f",
            hash.Value);
    }

    [Fact]
    public void Chain_changes_for_previous_hash_tamper_and_event_order()
    {
        Sha256Hex.TryCreate(
            "1111111111111111111111111111111111111111111111111111111111111111",
            out var manifestHash);
        var decision = CanonicalJson.Canonicalize(
            """{"actions":[],"status":"notProduced"}"""u8);
        var ordered = CanonicalJson.Canonicalize("""{"events":[1,2]}"""u8);
        var reordered = CanonicalJson.Canonicalize("""{"events":[2,1]}"""u8);
        var first = ProtocolHash.CalculateDecisionHash(
            ProtocolHash.ZeroHash,
            manifestHash!,
            "1.0.0",
            ordered,
            decision);
        var reorderedHash = ProtocolHash.CalculateDecisionHash(
            ProtocolHash.ZeroHash,
            manifestHash!,
            "1.0.0",
            reordered,
            decision);
        var chained = ProtocolHash.CalculateDecisionHash(
            first,
            manifestHash!,
            "1.0.0",
            ordered,
            decision);

        Assert.NotEqual(first, reorderedHash);
        Assert.NotEqual(first, chained);
        Assert.Equal(
            "30f1408d3b64055d80e108ac66214a161d4459bb2c11a94eb41fc2db0dcae5ea",
            first.Value);
        Assert.Equal(
            "bef7c661ca166a3da3c04d672adb1e1566ed87e6a0150a48e376a8b40bcd7529",
            chained.Value);
        Assert.Matches("^[0-9a-f]{64}$", chained.Value);
    }

    [Fact]
    public void Initial_state_identity_vector_is_stable()
    {
        EpochId.TryCreate(0, out var epoch);
        EventSequence.TryCreate(1, out var sequence);
        SimulationTimeMilliseconds.TryCreate(0, out var simTime);

        var hash = ProtocolHash.CalculateStateIdentityHash(epoch, sequence, simTime);

        Assert.Equal(
            "6e628e8e7e2dd18386c05ce6c7c114f2ef617cfd8b122265f3bfdfb9a20052fa",
            hash.Value);
    }

    private static RunManifestIdentity ReadManifest()
    {
        var fixture = FixtureLoader.ReadUtf8("initialize/valid-initialize-run.json");
        var envelope = ProtocolEnvelopeCodec.Decode(Encoding.UTF8.GetBytes(fixture));
        return InitializeRunPayloadCodec.Decode(envelope.Envelope!.Payload)
            .Value!
            .Manifest;
    }
}
