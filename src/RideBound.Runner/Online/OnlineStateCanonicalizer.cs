using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RideBound.Application.State;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;

namespace RideBound.Runner.Online;

public static class OnlineStateCanonicalizer
{
    private static readonly byte[] DomainPrefix =
        "RideBound.OnlineStateHash.v1\0"u8.ToArray();

    public static byte[] Canonicalize(OnlineState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var buffer = new ArrayBufferWriter<byte>();

        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("runId", state.Run.Id.Value);
            writer.WriteString("scenarioId", state.Run.ScenarioId.Value);
            writer.WriteNumber("appliedEpoch", state.Run.AppliedEpoch);
            writer.WriteNumber(
                "simulationTimeMs",
                state.Run.SimulationTime.Milliseconds);
            writer.WriteNumber("nextEventSeq", state.NextEventSequence);
            writer.WritePropertyName("requests");
            writer.WriteStartArray();

            foreach (var request in state.Run.Requests.Values.OrderBy(
                         value => value.Id.Value,
                         StringComparer.Ordinal))
            {
                WriteRequest(writer, request);
            }

            writer.WriteEndArray();
            writer.WritePropertyName("vehicles");
            writer.WriteStartArray();

            foreach (var vehicle in state.Run.Vehicles.Values.OrderBy(
                         value => value.Id.Value,
                         StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteString("vehicleId", vehicle.Id.Value);
                writer.WriteNumber("capacity", vehicle.Capacity);
                writer.WriteNumber("occupiedSeats", vehicle.OccupiedSeats);
                writer.WritePropertyName("position");
                WritePosition(writer, vehicle.Position);
                WriteRequestSet(
                    writer,
                    "onboardRequestIds",
                    vehicle.OnboardRequestIds);
                WriteRequestSet(
                    writer,
                    "acceptedRequestIds",
                    vehicle.AcceptedRequestIds);
                writer.WritePropertyName("route");
                WriteRoute(writer, vehicle.Route);
                writer.WriteNumber(
                    "lastObservedEpoch",
                    vehicle.LastObservedEpoch);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            if (state.TravelTimes is not null)
            {
                writer.WritePropertyName("travelTimes");
                writer.WriteStartObject();
                writer.WriteNumber("version", state.TravelTimes.Version);
                writer.WriteString(
                    "snapshotHash",
                    state.TravelTimes.SnapshotHash);
                writer.WritePropertyName("arcs");
                writer.WriteStartArray();

                foreach (var arc in state.TravelTimes.TravelTimes.OrderBy(
                             value => value.Key.FromNodeId.Value,
                             StringComparer.Ordinal)
                         .ThenBy(
                             value => value.Key.ToNodeId.Value,
                             StringComparer.Ordinal))
                {
                    writer.WriteStartObject();
                    writer.WriteString(
                        "fromNodeId",
                        arc.Key.FromNodeId.Value);
                    writer.WriteString("toNodeId", arc.Key.ToNodeId.Value);
                    writer.WriteNumber(
                        "travelTimeMs",
                        arc.Value.Milliseconds);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        return CanonicalJson.Canonicalize(buffer.WrittenSpan);
    }

    public static Sha256Hex CalculateHash(OnlineState state)
    {
        var canonical = Canonicalize(state);
        var buffer = new ArrayBufferWriter<byte>();
        buffer.Write(DomainPrefix);
        WriteFrame(buffer, "canonicalOnlineState", canonical);
        var text = Convert.ToHexStringLower(SHA256.HashData(buffer.WrittenSpan));
        Sha256Hex.TryCreate(text, out var hash);
        return hash!;
    }

    private static void WriteRequest(Utf8JsonWriter writer, RideRequest request)
    {
        writer.WriteStartObject();
        writer.WriteString("requestId", request.Id.Value);
        writer.WriteNumber("arrivalTimeMs", request.ArrivalTime.Milliseconds);
        writer.WriteString("originNodeId", request.OriginNodeId.Value);
        writer.WriteString(
            "destinationNodeId",
            request.DestinationNodeId.Value);
        writer.WriteNumber(
            "earliestPickupMs",
            request.EarliestPickup.Milliseconds);
        writer.WriteNumber(
            "latestPickupMs",
            request.LatestPickup.Milliseconds);
        writer.WriteNumber(
            "maxRideTimeMs",
            request.MaxRideTime.Milliseconds);
        writer.WriteNumber("partySize", request.PartySize);
        writer.WriteString("serviceClass", request.ServiceClass);
        writer.WriteString(
            "commitmentPolicyId",
            request.CommitmentPolicyId);
        writer.WriteString("lifecycle", ToProtocolValue(request.Lifecycle));

        if (request.AssignedVehicleId is VehicleId vehicleId)
        {
            writer.WriteString("assignedVehicleId", vehicleId.Value);
        }

        if (request.ActualPickupTime is SimTime pickupTime)
        {
            writer.WriteNumber(
                "actualPickupTimeMs",
                pickupTime.Milliseconds);
        }

        writer.WriteEndObject();
    }

    private static void WritePosition(
        Utf8JsonWriter writer,
        VehiclePosition position)
    {
        writer.WriteStartObject();

        switch (position)
        {
            case NodePosition node:
                writer.WriteString("kind", "node");
                writer.WriteString("nodeId", node.NodeId.Value);
                break;
            case EdgeProgressPosition edge:
                writer.WriteString("kind", "edgeProgress");
                writer.WriteString("fromNodeId", edge.FromNodeId.Value);
                writer.WriteString("toNodeId", edge.ToNodeId.Value);
                writer.WriteString("edgeId", edge.EdgeId);
                writer.WriteNumber(
                    "progressPermille",
                    edge.ProgressPermille);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(position));
        }

        writer.WriteEndObject();
    }

    private static void WriteRequestSet(
        Utf8JsonWriter writer,
        string propertyName,
        IEnumerable<RequestId> values)
    {
        writer.WritePropertyName(propertyName);
        writer.WriteStartArray();

        foreach (var requestId in values.OrderBy(
                     value => value.Value,
                     StringComparer.Ordinal))
        {
            writer.WriteStringValue(requestId.Value);
        }

        writer.WriteEndArray();
    }

    private static void WriteRoute(Utf8JsonWriter writer, RoutePlan route)
    {
        writer.WriteStartObject();
        writer.WriteNumber("planVersion", route.Version.Value);
        writer.WriteNumber("executedStopCount", route.ExecutedStopCount);
        writer.WritePropertyName("frozenPrefix");
        WriteStops(writer, route.FrozenPrefix);
        writer.WritePropertyName("mutableSuffix");
        WriteStops(writer, route.MutableSuffix);
        writer.WriteEndObject();
    }

    private static void WriteStops(
        Utf8JsonWriter writer,
        IEnumerable<RouteStop> stops)
    {
        writer.WriteStartArray();

        foreach (var stop in stops)
        {
            writer.WriteStartObject();
            writer.WriteString("stopId", stop.StopId.Value);
            writer.WriteString("nodeId", stop.NodeId.Value);
            writer.WriteString(
                "kind",
                stop.Kind switch
                {
                    RideBound.Domain.Routes.RouteStopKind.Waypoint =>
                        "waypoint",
                    RideBound.Domain.Routes.RouteStopKind.Pickup =>
                        "pickup",
                    RideBound.Domain.Routes.RouteStopKind.DropOff =>
                        "dropOff",
                    _ => throw new ArgumentOutOfRangeException(nameof(stop)),
                });

            if (stop.RequestId is RequestId requestId)
            {
                writer.WriteString("requestId", requestId.Value);
            }

            writer.WriteNumber(
                "serviceDurationMs",
                stop.ServiceDuration.Milliseconds);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static string ToProtocolValue(RequestLifecycle lifecycle) =>
        lifecycle switch
        {
            RequestLifecycle.Pending => "pending",
            RequestLifecycle.Accepted => "accepted",
            RequestLifecycle.WaitingPickup => "waitingPickup",
            RequestLifecycle.Onboard => "onboard",
            RequestLifecycle.Completed => "completed",
            RequestLifecycle.Rejected => "rejected",
            RequestLifecycle.CancelledBeforeAcceptance =>
                "cancelledBeforeAcceptance",
            RequestLifecycle.CancelledAfterAcceptance =>
                "cancelledAfterAcceptance",
            _ => throw new ArgumentOutOfRangeException(nameof(lifecycle)),
        };

    private static void WriteFrame(
        IBufferWriter<byte> writer,
        string tag,
        ReadOnlySpan<byte> value)
    {
        var tagBytes = Encoding.UTF8.GetBytes(tag);
        var header = writer.GetSpan(sizeof(ushort));
        BinaryPrimitives.WriteUInt16BigEndian(
            header,
            checked((ushort)tagBytes.Length));
        writer.Advance(sizeof(ushort));
        writer.Write(tagBytes);
        header = writer.GetSpan(sizeof(ulong));
        BinaryPrimitives.WriteUInt64BigEndian(header, (ulong)value.Length);
        writer.Advance(sizeof(ulong));
        writer.Write(value);
    }
}
