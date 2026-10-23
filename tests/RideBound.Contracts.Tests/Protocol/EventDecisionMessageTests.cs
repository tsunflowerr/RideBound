using System.Text;
using System.Text.Json;
using RideBound.Contracts.Protocol;

namespace RideBound.Contracts.Tests.Protocol;

public sealed class EventDecisionMessageTests
{
    [Fact]
    public void Event_batch_preserves_order_and_every_v1_event_type()
    {
        var eventTypes = Enum.GetValues<EventType>();
        var events = eventTypes.Select(
                (eventType, index) =>
                {
                    EventSequence.TryCreate(index + 1, out var sequence);
                    return new ProtocolEvent(
                        sequence,
                        eventType,
                        ParseObject("{}"));
                })
            .ToArray();
        var encoded = EventBatchPayloadCodec.Encode(new EventBatchPayload(events));
        using var document = JsonDocument.Parse(encoded);

        var decoded = EventBatchPayloadCodec.Decode(document.RootElement);

        Assert.True(decoded.IsSuccess);
        Assert.Equal(eventTypes, decoded.Value!.Events.Select(value => value.EventType));
        Assert.Equal(
            Enumerable.Range(1, eventTypes.Length).Select(value => (long)value),
            decoded.Value.Events.Select(value => value.EventSequence.Value));
    }

    [Theory]
    [InlineData("""{"events":[]}""", ProtocolPayloadErrorCode.InvalidValue)]
    [InlineData(
        """{"events":[{"eventSeq":1,"eventType":"unknown","payload":{}}]}""",
        ProtocolPayloadErrorCode.InvalidValue)]
    [InlineData(
        """{"events":[{"eventSeq":1,"eventType":"timerTick","payload":[] }]}""",
        ProtocolPayloadErrorCode.InvalidFieldType)]
    [InlineData(
        """{"events":[{"eventSeq":1,"eventType":"timerTick","payload":{},"extra":1}]}""",
        ProtocolPayloadErrorCode.UnknownField)]
    public void Event_batch_rejects_invalid_structure(
        string json,
        ProtocolPayloadErrorCode expected)
    {
        using var document = JsonDocument.Parse(json);

        var result = EventBatchPayloadCodec.Decode(document.RootElement);

        Assert.False(result.IsSuccess);
        Assert.Equal(expected, result.Error?.Code);
    }

    [Fact]
    public void Wp1_decision_shell_cannot_claim_actions_or_validation()
    {
        var json = """
            {
              "status":"notProduced",
              "reasonCode":"WP1_STRUCTURAL_ONLY",
              "actions":[{"decisionType":"requestAccepted","payload":{}}],
              "certificate":{
                "status":"notProduced",
                "reasonCode":"COMMITMENT_VALIDATOR_NOT_AVAILABLE"
              },
              "solver":{"status":"notRun"},
              "stateBeforeHash":"0000000000000000000000000000000000000000000000000000000000000000",
              "stateAfterHash":"0000000000000000000000000000000000000000000000000000000000000000",
              "previousDecisionHash":"0000000000000000000000000000000000000000000000000000000000000000",
              "decisionHash":"0000000000000000000000000000000000000000000000000000000000000000"
            }
            """;
        using var document = JsonDocument.Parse(json);

        var result = DecisionPayloadCodec.Decode(document.RootElement);

        Assert.False(result.IsSuccess);
        Assert.Contains("cannot contain actions", result.Error?.Message);
    }

    [Fact]
    public void Decision_actions_preserve_every_v1_type_in_order()
    {
        Sha256Hex.TryCreate(
            "0000000000000000000000000000000000000000000000000000000000000000",
            out var zero);
        var decisionTypes = Enum.GetValues<DecisionType>();
        var payload = new DecisionPayload(
            DecisionProductionStatus.Produced,
            DecisionReasonCodes.Accepted,
            decisionTypes.Select(
                    value => ParseObject(
                        $$"""
                        {
                          "decisionType":"{{DecisionTypeVocabulary.ToProtocolValue(value)}}",
                          "payload":{}
                        }
                        """))
                .ToArray(),
            new CertificateShell(CertificateStatus.Produced, "VALIDATED"),
            new SolverStatusShell(SolverStatus.Completed),
            zero!,
            zero!,
            zero!,
            zero!);
        var encoded = DecisionPayloadCodec.Encode(payload);
        using var document = JsonDocument.Parse(encoded);

        var decoded = DecisionPayloadCodec.Decode(document.RootElement);

        Assert.True(decoded.IsSuccess);
        Assert.Equal(
            decisionTypes,
            decoded.Value!.Actions.Select(
                value =>
                {
                    DecisionTypeVocabulary.TryParse(
                        value.GetProperty("decisionType").GetString(),
                        out var decisionType);
                    return decisionType;
                }));
    }

    [Fact]
    public void Unknown_decision_reason_is_rejected_as_contract_drift()
    {
        var json = """
            {
              "status":"notProduced",
              "reasonCode":"SOMETHING_NEW",
              "actions":[],
              "certificate":{
                "status":"notProduced",
                "reasonCode":"COMMITMENT_VALIDATOR_NOT_AVAILABLE"
              },
              "solver":{"status":"notRun"},
              "stateBeforeHash":"0000000000000000000000000000000000000000000000000000000000000000",
              "stateAfterHash":"0000000000000000000000000000000000000000000000000000000000000000",
              "previousDecisionHash":"0000000000000000000000000000000000000000000000000000000000000000",
              "decisionHash":"0000000000000000000000000000000000000000000000000000000000000000"
            }
            """;
        using var document = JsonDocument.Parse(json);

        var result = DecisionPayloadCodec.Decode(document.RootElement);

        Assert.False(result.IsSuccess);
        Assert.Equal("$.payload.reasonCode", result.Error?.Field);
    }

    [Fact]
    public void Error_taxonomy_requires_code_and_disposition_to_match()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "code":"EVENT_SEQUENCE_GAP",
              "disposition":"rejectMessage",
              "message":"wrong severity"
            }
            """);

        var result = ErrorPayloadCodec.Decode(document.RootElement);

        Assert.False(result.IsSuccess);
        Assert.Equal(ProtocolPayloadErrorCode.InvalidValue, result.Error?.Code);
    }

    [Fact]
    public void Error_encoder_sanitizes_multiline_diagnostic()
    {
        var encoded = ErrorPayloadCodec.Encode(
            new ErrorPayload(
                "MALFORMED_JSON",
                ProtocolFailureDisposition.RejectMessage,
                "bad\r\nline"));

        Assert.DoesNotContain((byte)'\r', encoded);
        Assert.DoesNotContain((byte)'\n', encoded);
        using var document = JsonDocument.Parse(encoded);
        Assert.Equal(
            "bad  line",
            document.RootElement.GetProperty("message").GetString());
    }

    private static JsonElement ParseObject(string json)
    {
        using var document = JsonDocument.Parse(Encoding.UTF8.GetBytes(json));
        return document.RootElement.Clone();
    }
}
