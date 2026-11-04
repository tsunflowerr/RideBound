using System.Text.Json;

namespace RideBound.Contracts.Protocol;

public abstract record OnlineDecisionActionPayload;

public sealed record RequestAcceptedActionPayload(
    string RequestId,
    string VehicleId,
    string CandidateId) : OnlineDecisionActionPayload;

public sealed record RequestOutcomeActionPayload(
    string RequestId,
    string ReasonCode) : OnlineDecisionActionPayload;

public sealed record VehiclePlanUpdatedActionPayload(
    string VehicleId,
    string CandidateId,
    RoutePlanContract Route) : OnlineDecisionActionPayload;

public sealed record OnlineDecisionAction(
    DecisionType DecisionType,
    OnlineDecisionActionPayload Payload);

public static class OnlineDecisionActionCodec
{
    private static readonly IReadOnlySet<string> AcceptedFields =
        Fields("requestId", "vehicleId", "candidateId");

    private static readonly IReadOnlySet<string> RequestOutcomeFields =
        Fields("requestId", "reasonCode");

    private static readonly IReadOnlySet<string> VehiclePlanFields =
        Fields("vehicleId", "candidateId", "route");

    public static JsonElement Encode(OnlineDecisionAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var bytes = ProtocolPayloadReader.Write(
            writer =>
            {
                writer.WriteStartObject();
                writer.WriteString(
                    "decisionType",
                    DecisionTypeVocabulary.ToProtocolValue(
                        action.DecisionType));
                writer.WritePropertyName("payload");
                WritePayload(writer, action);
                writer.WriteEndObject();
            });
        using var document = JsonDocument.Parse(bytes);
        return document.RootElement.Clone();
    }

    internal static ProtocolPayloadError? ValidatePayload(
        DecisionType decisionType,
        JsonElement payload,
        string path)
    {
        return decisionType switch
        {
            DecisionType.RequestAccepted =>
                ValidateAccepted(payload, path),
            DecisionType.RequestRejected or DecisionType.RequestDeferred =>
                ValidateRequestOutcome(payload, path),
            DecisionType.VehiclePlanUpdated =>
                ValidateVehiclePlan(payload, path),
            _ => null,
        };
    }

    private static void WritePayload(
        Utf8JsonWriter writer,
        OnlineDecisionAction action)
    {
        switch (action.DecisionType, action.Payload)
        {
            case (
                DecisionType.RequestAccepted,
                RequestAcceptedActionPayload accepted):
                RequireIdentifier(accepted.RequestId, nameof(accepted.RequestId));
                RequireIdentifier(accepted.VehicleId, nameof(accepted.VehicleId));
                RequireIdentifier(
                    accepted.CandidateId,
                    nameof(accepted.CandidateId));
                writer.WriteStartObject();
                writer.WriteString("requestId", accepted.RequestId);
                writer.WriteString("vehicleId", accepted.VehicleId);
                writer.WriteString("candidateId", accepted.CandidateId);
                writer.WriteEndObject();
                break;
            case (
                DecisionType.RequestRejected or DecisionType.RequestDeferred,
                RequestOutcomeActionPayload outcome):
                RequireIdentifier(outcome.RequestId, nameof(outcome.RequestId));
                RequireIdentifier(outcome.ReasonCode, nameof(outcome.ReasonCode));
                writer.WriteStartObject();
                writer.WriteString("requestId", outcome.RequestId);
                writer.WriteString("reasonCode", outcome.ReasonCode);
                writer.WriteEndObject();
                break;
            case (
                DecisionType.VehiclePlanUpdated,
                VehiclePlanUpdatedActionPayload plan):
                RequireIdentifier(plan.VehicleId, nameof(plan.VehicleId));
                RequireIdentifier(plan.CandidateId, nameof(plan.CandidateId));
                writer.WriteStartObject();
                writer.WriteString("vehicleId", plan.VehicleId);
                writer.WriteString("candidateId", plan.CandidateId);
                writer.WritePropertyName("route");
                OnlineContractCodec.WriteRoutePlan(writer, plan.Route);
                writer.WriteEndObject();
                break;
            default:
                throw new ArgumentException(
                    $"Payload '{action.Payload.GetType().Name}' does not match " +
                    $"online decision type '{action.DecisionType}'.",
                    nameof(action));
        }
    }

    private static ProtocolPayloadError? ValidateAccepted(
        JsonElement payload,
        string path)
    {
        var objectError = ProtocolPayloadReader.ValidateObject(
            payload,
            path,
            AcceptedFields);
        var requestId = ProtocolPayloadReader.ReadRequiredString(
            payload,
            path,
            "requestId");
        var vehicleId = ProtocolPayloadReader.ReadRequiredString(
            payload,
            path,
            "vehicleId");
        var candidateId = ProtocolPayloadReader.ReadRequiredString(
            payload,
            path,
            "candidateId");
        return HelloPayloadCodec.FirstError(
            objectError,
            requestId.Error,
            vehicleId.Error,
            candidateId.Error);
    }

    private static ProtocolPayloadError? ValidateRequestOutcome(
        JsonElement payload,
        string path)
    {
        var objectError = ProtocolPayloadReader.ValidateObject(
            payload,
            path,
            RequestOutcomeFields);
        var requestId = ProtocolPayloadReader.ReadRequiredString(
            payload,
            path,
            "requestId");
        var reasonCode = ProtocolPayloadReader.ReadRequiredString(
            payload,
            path,
            "reasonCode");
        return HelloPayloadCodec.FirstError(
            objectError,
            requestId.Error,
            reasonCode.Error);
    }

    private static ProtocolPayloadError? ValidateVehiclePlan(
        JsonElement payload,
        string path)
    {
        var objectError = ProtocolPayloadReader.ValidateObject(
            payload,
            path,
            VehiclePlanFields);
        var vehicleId = ProtocolPayloadReader.ReadRequiredString(
            payload,
            path,
            "vehicleId");
        var candidateId = ProtocolPayloadReader.ReadRequiredString(
            payload,
            path,
            "candidateId");
        var routeElement = ProtocolPayloadReader.ReadRequiredProperty(
            payload,
            path,
            "route");
        var error = HelloPayloadCodec.FirstError(
            objectError,
            vehicleId.Error,
            candidateId.Error,
            routeElement.Error);

        if (error is not null)
        {
            return error;
        }

        var route = OnlineContractCodec.ReadRoutePlan(
            routeElement.Value,
            $"{path}.route");
        return route.Error;
    }

    private static IReadOnlySet<string> Fields(params string[] values) =>
        new HashSet<string>(values, StringComparer.Ordinal);

    private static void RequireIdentifier(string value, string parameterName)
    {
        if (!OpaqueIdentifier.IsValid(value))
        {
            throw new ArgumentException(
                "Identifier must contain 1 to 128 valid UTF-8 bytes.",
                parameterName);
        }
    }
}
