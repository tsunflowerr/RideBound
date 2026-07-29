using System.Text.Json;

namespace RideBound.Contracts.Protocol;

public enum EventType
{
    RequestArrived,
    BookingConfirmed,
    OfferDeclined,
    RequestCancelledBeforeAcceptance,
    RequestCancelledAfterAcceptance,
    VehicleAdvanced,
    VehicleReachedStop,
    PassengerBoarded,
    PassengerAlighted,
    TravelTimesUpdated,
    TimerTick,
    IncidentOpened,
    IncidentResolved,
}

public sealed record ProtocolEvent(
    EventSequence EventSequence,
    EventType EventType,
    JsonElement Payload);

public sealed record EventBatchPayload(IReadOnlyList<ProtocolEvent> Events);

public sealed record DecisionAppliedPayload(Sha256Hex DecisionHash);

public static class EventTypeVocabulary
{
    private static readonly IReadOnlyDictionary<string, EventType> ByWireValue =
        Enum.GetValues<EventType>().ToDictionary(
            ToProtocolValue,
            value => value,
            StringComparer.Ordinal);

    public static bool TryParse(string? value, out EventType eventType) =>
        ByWireValue.TryGetValue(value ?? string.Empty, out eventType);

    public static string ToProtocolValue(EventType eventType) =>
        eventType switch
        {
            EventType.RequestArrived => "requestArrived",
            EventType.BookingConfirmed => "bookingConfirmed",
            EventType.OfferDeclined => "offerDeclined",
            EventType.RequestCancelledBeforeAcceptance =>
                "requestCancelledBeforeAcceptance",
            EventType.RequestCancelledAfterAcceptance =>
                "requestCancelledAfterAcceptance",
            EventType.VehicleAdvanced => "vehicleAdvanced",
            EventType.VehicleReachedStop => "vehicleReachedStop",
            EventType.PassengerBoarded => "passengerBoarded",
            EventType.PassengerAlighted => "passengerAlighted",
            EventType.TravelTimesUpdated => "travelTimesUpdated",
            EventType.TimerTick => "timerTick",
            EventType.IncidentOpened => "incidentOpened",
            EventType.IncidentResolved => "incidentResolved",
            _ => throw new ArgumentOutOfRangeException(nameof(eventType)),
        };
}

public static class EventBatchPayloadCodec
{
    private static readonly IReadOnlySet<string> Fields =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "events",
        };

    private static readonly IReadOnlySet<string> EventFields =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "eventSeq",
            "eventType",
            "payload",
        };

    public static ProtocolPayloadDecodeResult<EventBatchPayload> Decode(
        JsonElement payload)
    {
        var objectError = ProtocolPayloadReader.ValidateObject(
            payload,
            "$.payload",
            Fields);

        if (objectError is not null)
        {
            return ProtocolPayloadDecodeResult<EventBatchPayload>.Failure(objectError);
        }

        var eventsProperty = ProtocolPayloadReader.ReadRequiredProperty(
            payload,
            "$.payload",
            "events");

        if (!eventsProperty.IsSuccess)
        {
            return ProtocolPayloadDecodeResult<EventBatchPayload>.Failure(
                eventsProperty.Error!);
        }

        if (eventsProperty.Value.ValueKind != JsonValueKind.Array)
        {
            return Failure(
                ProtocolPayloadErrorCode.InvalidFieldType,
                "$.payload.events",
                "Field '$.payload.events' must be an array.");
        }

        var events = new List<ProtocolEvent>();
        var index = 0;

        foreach (var element in eventsProperty.Value.EnumerateArray())
        {
            var path = $"$.payload.events[{index}]";
            var eventObjectError = ProtocolPayloadReader.ValidateObject(
                element,
                path,
                EventFields);

            if (eventObjectError is not null)
            {
                return ProtocolPayloadDecodeResult<EventBatchPayload>.Failure(
                    eventObjectError);
            }

            var sequence = ProtocolPayloadReader.ReadRequiredInteger(
                element,
                path,
                "eventSeq",
                minimum: 1);
            var eventTypeText = ProtocolPayloadReader.ReadRequiredString(
                element,
                path,
                "eventType");
            var eventPayload = ProtocolPayloadReader.ReadRequiredProperty(
                element,
                path,
                "payload");
            var error = HelloPayloadCodec.FirstError(
                sequence.Error,
                eventTypeText.Error,
                eventPayload.Error);

            if (error is not null)
            {
                return ProtocolPayloadDecodeResult<EventBatchPayload>.Failure(error);
            }

            if (!EventSequence.TryCreate(sequence.Value, out var eventSequence))
            {
                return Failure(
                    ProtocolPayloadErrorCode.ValueOutOfRange,
                    $"{path}.eventSeq",
                    $"Field '{path}.eventSeq' is outside the event sequence range.");
            }

            if (!EventTypeVocabulary.TryParse(eventTypeText.Value, out var eventType))
            {
                return Failure(
                    ProtocolPayloadErrorCode.InvalidValue,
                    $"{path}.eventType",
                    $"Event type '{eventTypeText.Value}' is unknown for protocol v1.");
            }

            if (eventPayload.Value.ValueKind != JsonValueKind.Object)
            {
                return Failure(
                    ProtocolPayloadErrorCode.InvalidFieldType,
                    $"{path}.payload",
                    $"Field '{path}.payload' must be an object.");
            }

            events.Add(
                new ProtocolEvent(
                    eventSequence,
                    eventType,
                    eventPayload.Value.Clone()));
            index++;
        }

        if (events.Count == 0)
        {
            return Failure(
                ProtocolPayloadErrorCode.InvalidValue,
                "$.payload.events",
                "An event batch must contain at least one event.");
        }

        return ProtocolPayloadDecodeResult<EventBatchPayload>.Success(
            new EventBatchPayload(events));
    }

    public static byte[] Encode(EventBatchPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (payload.Events.Count == 0)
        {
            throw new ArgumentException(
                "An event batch must contain at least one event.",
                nameof(payload));
        }

        return ProtocolPayloadReader.Write(
            writer =>
            {
                writer.WriteStartObject();
                writer.WritePropertyName("events");
                writer.WriteStartArray();

                foreach (var protocolEvent in payload.Events)
                {
                    if (protocolEvent.Payload.ValueKind != JsonValueKind.Object)
                    {
                        throw new ArgumentException(
                            "Every event payload must be an object.",
                            nameof(payload));
                    }

                    writer.WriteStartObject();
                    writer.WriteNumber(
                        "eventSeq",
                        protocolEvent.EventSequence.Value);
                    writer.WriteString(
                        "eventType",
                        EventTypeVocabulary.ToProtocolValue(protocolEvent.EventType));
                    writer.WritePropertyName("payload");
                    protocolEvent.Payload.WriteTo(writer);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            });
    }

    private static ProtocolPayloadDecodeResult<EventBatchPayload> Failure(
        ProtocolPayloadErrorCode code,
        string field,
        string message) =>
        ProtocolPayloadDecodeResult<EventBatchPayload>.Failure(
            new ProtocolPayloadError(code, field, message));
}

public static class DecisionAppliedPayloadCodec
{
    private static readonly IReadOnlySet<string> Fields =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "decisionHash",
        };

    public static ProtocolPayloadDecodeResult<DecisionAppliedPayload> Decode(
        JsonElement payload)
    {
        var objectError = ProtocolPayloadReader.ValidateObject(
            payload,
            "$.payload",
            Fields);

        if (objectError is not null)
        {
            return ProtocolPayloadDecodeResult<DecisionAppliedPayload>.Failure(
                objectError);
        }

        var hashText = ProtocolPayloadReader.ReadRequiredString(
            payload,
            "$.payload",
            "decisionHash");

        if (!hashText.IsSuccess)
        {
            return ProtocolPayloadDecodeResult<DecisionAppliedPayload>.Failure(
                hashText.Error!);
        }

        if (!Sha256Hex.TryCreate(hashText.Value, out var hash))
        {
            return ProtocolPayloadDecodeResult<DecisionAppliedPayload>.Failure(
                new ProtocolPayloadError(
                    ProtocolPayloadErrorCode.InvalidValue,
                    "$.payload.decisionHash",
                    "decisionHash must be 64 lowercase hexadecimal characters."));
        }

        return ProtocolPayloadDecodeResult<DecisionAppliedPayload>.Success(
            new DecisionAppliedPayload(hash!));
    }

    public static byte[] Encode(DecisionAppliedPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return ProtocolPayloadReader.Write(
            writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("decisionHash", payload.DecisionHash.Value);
                writer.WriteEndObject();
            });
    }
}
