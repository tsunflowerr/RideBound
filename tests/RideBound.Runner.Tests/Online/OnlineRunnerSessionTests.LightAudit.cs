using System.Text.Json.Nodes;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;
using RideBound.Runner.Configuration;

namespace RideBound.Runner.Tests.Online;

public sealed partial class OnlineRunnerSessionTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("full-v1", false)]
    [InlineData("light-v1", true)]
    public void C1_audit_mode_selects_the_decision_input_and_hash_domain(string? auditHash, bool light)
    {
        var configuration = ReadAuditConfiguration(auditHash);
        Assert.Equal(light, configuration.UsesLightAuditHash);
        if (auditHash is not null)
        {
            Assert.NotEqual(ReadAuditConfiguration(null).ContentHash, configuration.ContentHash);
        }

        var session = OpenC1(configuration);
        var before = ReadCheckpoint(session).Content;
        var bootstrap = ReadFixture("wp2/valid-bootstrap-event-batch.json");
        var decision = ReadDecision(session, bootstrap).Decision;

        var input = new JsonObject
        {
            ["eventBatch"] = JsonNode.Parse(bootstrap),
            ["stateIdentityBefore"] = new JsonObject
            {
                ["epochId"] = before.AppliedEpoch,
                ["nextEventSeq"] = before.NextEventSequence,
                ["simTimeMs"] = before.SimulationTimeMs,
                ["stateHash"] = before.StateHash.Value,
            },
        };
        if (!light)
        {
            input["onlineStateBefore"] = JsonNode.Parse(before.OnlineState.GetRawText());
        }

        var canonicalInput = CanonicalJson.Canonicalize(Bytes(input));
        var canonicalDecision = CanonicalJson.Canonicalize(DecisionPayloadCodec.Encode(decision, hashProjection: true));
        var expected = light
            ? ProtocolHash.CalculateDecisionHashLight(
                before.PreviousDecisionHash, before.ManifestHash, configuration.PolicyVersion, [canonicalInput], canonicalDecision)
            : ProtocolHash.CalculateDecisionHash(
                before.PreviousDecisionHash, before.ManifestHash, configuration.PolicyVersion, canonicalInput, canonicalDecision);

        Assert.Equal(expected, decision.DecisionHash);
        Assert.Equal(before.StateHash, decision.StateBeforeHash);
        Assert.Equal(decision.StateAfterHash, decision.Certificate.Body!.ProposedStateHash);
        var identity = AuditStateIdentity(1, 4, 1_000);
        if (light)
        {
            Assert.Equal(identity, decision.StateAfterHash);
        }
        else
        {
            Assert.NotEqual(identity, decision.StateAfterHash);
        }
    }

    [Fact]
    public void Light_audit_checkpoint_preserves_the_ledger_and_replays_the_decision_chain()
    {
        var configuration = ReadAuditConfiguration("light-v1");
        var live = OpenC1(configuration);
        _ = BootstrapForServiceQuality(live);
        _ = ConfirmServiceQualityBooking(live);
        var checkpoint = ReadCheckpoint(live);
        Assert.Equal(AuditStateIdentity(2, 5, 1_100), checkpoint.Content.StateHash);
        Assert.Single(checkpoint.Content.OnlineState.GetProperty("commitmentLedger").EnumerateArray());

        var restored = OpenC1(configuration);
        Assert.Equal("restore", restored.Process(CreateRestoreRequest(checkpoint)).Response?.MessageType.Value);
        Assert.Equal(checkpoint.CheckpointHash, ReadCheckpoint(restored).CheckpointHash);

        var next = CreateTimerBatch(3, 5, 1_200);
        var uninterrupted = ReadDecision(live, next);
        var replayed = ReadDecision(restored, next);
        Assert.Equal(uninterrupted.Frame, replayed.Frame);
        Assert.Equal(uninterrupted.Frame, ReadDecision(restored, next).Frame);
        Assert.Equal(checkpoint.Content.PreviousDecisionHash, replayed.Decision.PreviousDecisionHash);
        Assert.Equal(AuditStateIdentity(3, 6, 1_200), replayed.Decision.StateAfterHash);
        AcknowledgeServiceQualityDecision(restored, replayed.Decision, 3, 1_200);
        Assert.Equal(replayed.Decision.DecisionHash, ReadCheckpoint(restored).Content.PreviousDecisionHash);
    }

    [Fact]
    public void Light_audit_still_checks_the_checkpoint_state_content_hash_and_manifest()
    {
        var configuration = ReadAuditConfiguration("light-v1");
        var live = OpenC1(configuration);
        BootstrapAndAcknowledge(live);
        var checkpoint = ReadCheckpoint(live);
        var restore = JsonNode.Parse(CreateRestoreRequest(checkpoint))!;
        restore["payload"]!["content"]!["onlineState"]!["vehicles"]![0]!["capacity"] = 5;

        var changed = OpenC1(configuration).Process(Bytes(restore)).Response;
        Assert.Equal("error", changed?.MessageType.Value);
        Assert.Contains("Checkpoint content hash mismatch", changed?.Payload.GetProperty("message").GetString());

        var mismatched = OpenC1(ReadAuditConfiguration(null)).Process(CreateRestoreRequest(checkpoint)).Response;
        Assert.Equal("MANIFEST_MUTATION", mismatched?.Payload.GetProperty("code").GetString());
    }

    [Fact]
    public void C1_rejects_an_unknown_audit_hash_mode() =>
        Assert.Throws<InvalidDataException>(() => ReadAuditConfiguration("light-v2"));

    private static C1RunnerConfiguration ReadAuditConfiguration(string? auditHash)
    {
        if (auditHash is null)
        {
            return ReadServiceQualityConfiguration();
        }

        var root = JsonNode.Parse(ReadFixture("runner/ridebound-hard-vector.config.json"))!;
        root["auditHash"] = auditHash;
        return C1RunnerConfiguration.Decode(Bytes(root));
    }

    private static Sha256Hex AuditStateIdentity(long epoch, long nextSequence, long time)
    {
        Assert.True(EpochId.TryCreate(epoch, out var epochId));
        Assert.True(EventSequence.TryCreate(nextSequence, out var sequence));
        Assert.True(SimulationTimeMilliseconds.TryCreate(time, out var simTime));
        return ProtocolHash.CalculateStateIdentityHash(epochId, sequence, simTime);
    }
}
