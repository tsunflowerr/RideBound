using System.Text;
using System.Text.Json;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;
using RideBound.Contracts.Tests.Fixtures;

namespace RideBound.Contracts.Tests.Protocol;

public sealed class GoldenFixtureTests
{
    [Fact]
    public void Required_inventory_contains_the_duplicate_event_fixture()
    {
        var root = FixtureLoader.GetFixturePath("golden/required");

        var directory = Assert.Single(Directory.GetDirectories(root));

        Assert.Equal("09-duplicate-event-idempotent", Path.GetFileName(directory));
    }

    [Fact]
    public void Required_metadata_describes_the_runner_executable_fixture()
    {
        var root = FixtureLoader.GetFixturePath("golden/required");
        var metadata = Assert.Single(Directory.GetFiles(root, "metadata.json", SearchOption.AllDirectories));

        using var document = JsonDocument.Parse(File.ReadAllText(metadata));
        var value = document.RootElement;
        Assert.Equal(
            ["expectedOutcome", "expectedValidator", "fixtureId", "minimumWorkPackage", "supportLevel"],
            value.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal("pass", value.GetProperty("expectedOutcome").GetString());
        Assert.Equal("runner-executable", value.GetProperty("supportLevel").GetString());
        Assert.Equal("duplicate-event-idempotent", value.GetProperty("fixtureId").GetString());
    }

    [Fact]
    public void Required_input_is_a_canonicalizable_event_batch()
    {
        var root = FixtureLoader.GetFixturePath("golden/required");
        var input = Assert.Single(Directory.GetFiles(root, "input.json", SearchOption.AllDirectories));
        var bytes = File.ReadAllBytes(input);

        var canonical = CanonicalJson.Canonicalize(bytes);
        var envelope = ProtocolEnvelopeCodec.Decode(bytes);

        Assert.NotEmpty(canonical);
        Assert.True(envelope.IsSuccess, input);
        Assert.Equal("eventBatch", envelope.Envelope?.MessageType.Value);
        var payload = EventBatchPayloadCodec.Decode(envelope.Envelope!.Payload);
        Assert.True(payload.IsSuccess, input);
        Assert.Single(payload.Value!.Events);
    }

    [Fact]
    public void Hash_vector_asset_is_valid_utf8_json_with_lowercase_hashes()
    {
        var json = FixtureLoader.ReadUtf8("hash/protocol-hash-vectors.json");
        using var document = JsonDocument.Parse(Encoding.UTF8.GetBytes(json));

        foreach (var section in document.RootElement.EnumerateObject())
        {
            var hash = section.Value.GetProperty("expectedSha256").GetString();
            Assert.Matches("^[0-9a-f]{64}$", hash!);
        }
    }
}
