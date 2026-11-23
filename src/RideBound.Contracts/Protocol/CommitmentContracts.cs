using System.Text.Json;

namespace RideBound.Contracts.Protocol;

public sealed record CommitmentVectorContract(
    long PickupEtaTotalMs,
    long DropEtaTotalMs,
    long MaterialEtaRevisionCount,
    long VehicleSwitchCount,
    long PickupStopRelocationMm,
    long PickupStopSwitchCount,
    long DropStopRelocationMm,
    long DropStopSwitchCount,
    long IncumbentOrderInversionCount,
    long PrePickupInsertedStopCount);

public sealed record PromiseServiceTokenContract(
    string StopId,
    string? RequestId,
    RouteStopKind Kind);

public sealed record PromiseProjectionContract(
    string RequestId,
    string VehicleId,
    string PickupStopId,
    string PickupNodeId,
    string DropStopId,
    string DropNodeId,
    long PickupEtaMs,
    long DropEtaMs,
    IReadOnlyList<PromiseServiceTokenContract> ServiceOrder);

public sealed record PromisePublishedActionPayload(
    string PublicationId,
    long PromiseVersion,
    string ReasonCode,
    long SourceEventSequence,
    PromiseProjectionContract Promise,
    CommitmentVectorContract ExogenousDelta,
    CommitmentVectorContract DecisionDelta,
    CommitmentVectorContract VisibleDelta,
    CommitmentVectorContract BudgetBefore,
    CommitmentVectorContract BudgetAfter) : OnlineDecisionActionPayload;

public static class CommitmentContractCodec
{
    private static readonly IReadOnlySet<string> VectorFields = Fields(
        "pickupEtaTotalMs",
        "dropEtaTotalMs",
        "materialEtaRevisionCount",
        "vehicleSwitchCount",
        "pickupStopRelocationMm",
        "pickupStopSwitchCount",
        "dropStopRelocationMm",
        "dropStopSwitchCount",
        "incumbentOrderInversionCount",
        "prePickupInsertedStopCount");

    private static readonly IReadOnlySet<string> PromiseFields = Fields(
        "requestId",
        "vehicleId",
        "pickupStopId",
        "pickupNodeId",
        "dropStopId",
        "dropNodeId",
        "pickupEtaMs",
        "dropEtaMs",
        "serviceOrder");

    private static readonly IReadOnlySet<string> TokenFields =
        Fields("stopId", "requestId", "kind");

    public static void WriteVector(
        Utf8JsonWriter writer,
        CommitmentVectorContract vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        var values = VectorValues(vector);

        if (values.Any(value => value is < 0 or > ProtocolLimits.MaxCanonicalInteger))
        {
            throw new ArgumentOutOfRangeException(nameof(vector));
        }

        writer.WriteStartObject();
        writer.WriteNumber("pickupEtaTotalMs", vector.PickupEtaTotalMs);
        writer.WriteNumber("dropEtaTotalMs", vector.DropEtaTotalMs);
        writer.WriteNumber("materialEtaRevisionCount", vector.MaterialEtaRevisionCount);
        writer.WriteNumber("vehicleSwitchCount", vector.VehicleSwitchCount);
        writer.WriteNumber("pickupStopRelocationMm", vector.PickupStopRelocationMm);
        writer.WriteNumber("pickupStopSwitchCount", vector.PickupStopSwitchCount);
        writer.WriteNumber("dropStopRelocationMm", vector.DropStopRelocationMm);
        writer.WriteNumber("dropStopSwitchCount", vector.DropStopSwitchCount);
        writer.WriteNumber("incumbentOrderInversionCount", vector.IncumbentOrderInversionCount);
        writer.WriteNumber("prePickupInsertedStopCount", vector.PrePickupInsertedStopCount);
        writer.WriteEndObject();
    }

    public static ProtocolPayloadError? ValidateVector(
        JsonElement element,
        string path)
    {
        var objectError = ProtocolPayloadReader.ValidateObject(
            element,
            path,
            VectorFields);

        if (objectError is not null)
        {
            return objectError;
        }

        foreach (var field in VectorFields.Order(StringComparer.Ordinal))
        {
            var value = ProtocolPayloadReader.ReadRequiredInteger(
                element,
                path,
                field,
                minimum: 0);

            if (!value.IsSuccess)
            {
                return value.Error;
            }
        }

        return null;
    }

    public static void WritePromise(
        Utf8JsonWriter writer,
        PromiseProjectionContract promise)
    {
        ArgumentNullException.ThrowIfNull(promise);
        RequireIdentifier(promise.RequestId, nameof(promise.RequestId));
        RequireIdentifier(promise.VehicleId, nameof(promise.VehicleId));
        RequireIdentifier(promise.PickupStopId, nameof(promise.PickupStopId));
        RequireIdentifier(promise.PickupNodeId, nameof(promise.PickupNodeId));
        RequireIdentifier(promise.DropStopId, nameof(promise.DropStopId));
        RequireIdentifier(promise.DropNodeId, nameof(promise.DropNodeId));
        RequireCanonical(promise.PickupEtaMs, nameof(promise.PickupEtaMs));
        RequireCanonical(promise.DropEtaMs, nameof(promise.DropEtaMs));
        writer.WriteStartObject();
        writer.WriteString("requestId", promise.RequestId);
        writer.WriteString("vehicleId", promise.VehicleId);
        writer.WriteString("pickupStopId", promise.PickupStopId);
        writer.WriteString("pickupNodeId", promise.PickupNodeId);
        writer.WriteString("dropStopId", promise.DropStopId);
        writer.WriteString("dropNodeId", promise.DropNodeId);
        writer.WriteNumber("pickupEtaMs", promise.PickupEtaMs);
        writer.WriteNumber("dropEtaMs", promise.DropEtaMs);
        writer.WritePropertyName("serviceOrder");
        writer.WriteStartArray();

        foreach (var token in promise.ServiceOrder)
        {
            RequireIdentifier(token.StopId, nameof(token.StopId));
            writer.WriteStartObject();
            writer.WriteString("stopId", token.StopId);

            if (token.RequestId is not null)
            {
                RequireIdentifier(token.RequestId, nameof(token.RequestId));
                writer.WriteString("requestId", token.RequestId);
            }

            writer.WriteString("kind", RouteStopKindVocabulary.ToProtocolValue(token.Kind));
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    public static ProtocolPayloadError? ValidatePromise(
        JsonElement element,
        string path)
    {
        var objectError = ProtocolPayloadReader.ValidateObject(
            element,
            path,
            PromiseFields);
        var request = ProtocolPayloadReader.ReadRequiredString(element, path, "requestId");
        var vehicle = ProtocolPayloadReader.ReadRequiredString(element, path, "vehicleId");
        var pickupStop = ProtocolPayloadReader.ReadRequiredString(element, path, "pickupStopId");
        var pickupNode = ProtocolPayloadReader.ReadRequiredString(element, path, "pickupNodeId");
        var dropStop = ProtocolPayloadReader.ReadRequiredString(element, path, "dropStopId");
        var dropNode = ProtocolPayloadReader.ReadRequiredString(element, path, "dropNodeId");
        var pickupEta = ProtocolPayloadReader.ReadRequiredInteger(element, path, "pickupEtaMs", minimum: 0);
        var dropEta = ProtocolPayloadReader.ReadRequiredInteger(element, path, "dropEtaMs", minimum: 0);
        var order = ProtocolPayloadReader.ReadRequiredProperty(element, path, "serviceOrder");
        var error = HelloPayloadCodec.FirstError(
            objectError, request.Error, vehicle.Error, pickupStop.Error,
            pickupNode.Error, dropStop.Error, dropNode.Error, pickupEta.Error,
            dropEta.Error, order.Error);

        if (error is not null)
        {
            return error;
        }

        if (dropEta.Value < pickupEta.Value
            || order.Value.ValueKind != JsonValueKind.Array)
        {
            return InvalidPayload(path, "Promise ETA/order values are invalid.");
        }

        var index = 0;

        foreach (var token in order.Value.EnumerateArray())
        {
            var tokenPath = $"{path}.serviceOrder[{index}]";
            var tokenObject = ProtocolPayloadReader.ValidateObject(
                token,
                tokenPath,
                TokenFields);
            var stop = ProtocolPayloadReader.ReadRequiredString(token, tokenPath, "stopId");
            var requestId = ProtocolPayloadReader.ReadOptionalString(token, tokenPath, "requestId");
            var kind = ProtocolPayloadReader.ReadRequiredString(
                token,
                tokenPath,
                "kind",
                requireOpaqueValue: false);
            error = HelloPayloadCodec.FirstError(
                tokenObject,
                stop.Error,
                requestId.Error,
                kind.Error);

            if (error is not null)
            {
                return error;
            }

            if (!RouteStopKindVocabulary.TryParse(kind.Value, out var parsed)
                || parsed == RouteStopKind.Waypoint && requestId.Value is not null
                || parsed != RouteStopKind.Waypoint && requestId.Value is null)
            {
                return InvalidPayload(tokenPath, "Promise service token is invalid.");
            }

            index++;
        }

        return index == 0
            ? InvalidPayload($"{path}.serviceOrder", "Promise service order cannot be empty.")
            : null;
    }

    private static long[] VectorValues(CommitmentVectorContract value) =>
    [
        value.PickupEtaTotalMs,
        value.DropEtaTotalMs,
        value.MaterialEtaRevisionCount,
        value.VehicleSwitchCount,
        value.PickupStopRelocationMm,
        value.PickupStopSwitchCount,
        value.DropStopRelocationMm,
        value.DropStopSwitchCount,
        value.IncumbentOrderInversionCount,
        value.PrePickupInsertedStopCount,
    ];

    private static void RequireIdentifier(string value, string parameterName)
    {
        if (!OpaqueIdentifier.IsValid(value))
        {
            throw new ArgumentException(
                "Identifier must contain 1 to 128 valid UTF-8 bytes.",
                parameterName);
        }
    }

    private static void RequireCanonical(long value, string parameterName)
    {
        if (value is < 0 or > ProtocolLimits.MaxCanonicalInteger)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static ProtocolPayloadError InvalidPayload(string path, string message) =>
        new(ProtocolPayloadErrorCode.InvalidValue, path, message);

    private static IReadOnlySet<string> Fields(params string[] values) =>
        new HashSet<string>(values, StringComparer.Ordinal);
}
