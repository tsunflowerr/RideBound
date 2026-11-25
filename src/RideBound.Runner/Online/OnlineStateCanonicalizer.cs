using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using RideBound.Application.State;
using RideBound.Application.Travel;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Incidents;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;
using RideBound.Domain.Vehicles;
using RideBound.Runner.StateAudit;

namespace RideBound.Runner.Online;

public static class OnlineStateCanonicalizer
{
    private static readonly byte[] DomainPrefix =
        "RideBound.OnlineStateHash.v1\0"u8.ToArray();

    private static readonly byte[] FrameTag = "canonicalOnlineState"u8.ToArray();
    private const int ArrayChunkSize = 128;

    public static byte[] Canonicalize(OnlineState state) => CanonicalizeSegments(state).ToArray();

    public static Sha256Hex CalculateHash(OnlineState state) => HashV1(CanonicalizeSegments(state));

    internal static CanonicalSegments CanonicalizeSegments(OnlineState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var builder = new CanonicalSegmentBuilder();
        var root = new CanonicalObject(builder.Glue);
        root.Member("appliedEpoch"u8).Integer(state.Run.AppliedEpoch);

        if (state.Commitments.Histories.Count != 0)
        {
            root.Member("commitmentLedger"u8);
            WriteCommitmentLedger(builder, state.Commitments);
        }

        if (state.Incidents.Incidents.Count != 0 || state.Incidents.Breaches.Count != 0
            || state.Incidents.LatePickups.Count != 0)
        {
            root.Member("incidentLedger"u8);
            WriteIncidentLedger(builder, state.Incidents);
        }

        root.Member("nextEventSeq"u8).Integer(state.NextEventSequence);
        root.Member("requests"u8);
        WriteCanonicalArray(builder, state.Run.Requests.Values.OrderBy(
            request => request.Id.Value, StringComparer.Ordinal), WriteRequest);
        root.Member("runId"u8).String(state.Run.Id.Value);
        root.Member("scenarioId"u8).String(state.Run.ScenarioId.Value);

        if (state.Commitments.ShadowAllocations.Count != 0)
        {
            root.Member("shadowAllocations"u8);
            WriteCanonicalArray(builder, state.Commitments.ShadowAllocations.OrderBy(
                pair => pair.Key.Value, StringComparer.Ordinal), WriteShadowAllocation);
        }

        root.Member("simulationTimeMs"u8).Integer(state.Run.SimulationTime.Milliseconds);

        if (state.TravelTimes is { } travel)
        {
            root.Member("travelTimes"u8);
            WriteTravelTimes(builder, travel);
        }

        root.Member("vehicles"u8);
        WriteCanonicalArray(builder, state.Run.Vehicles.Values.OrderBy(
            vehicle => vehicle.Id.Value, StringComparer.Ordinal), WriteVehicle);
        root.End();
        return builder.Build();
    }

    internal static Sha256Hex HashV1(CanonicalSegments canonical)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(DomainPrefix);
        Span<byte> frame = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt16BigEndian(frame, (ushort)FrameTag.Length);
        hash.AppendData(frame[..sizeof(ushort)]);
        hash.AppendData(FrameTag);
        BinaryPrimitives.WriteUInt64BigEndian(frame, (ulong)canonical.Length);
        hash.AppendData(frame);
        foreach (var part in canonical.Parts)
        {
            hash.AppendData(part.Span);
        }

        Sha256Hex.TryCreate(Convert.ToHexStringLower(hash.GetHashAndReset()), out var result);
        return result!;
    }

    // Canonical JSON is compositional. Canonicalize a bounded group of array entries at a
    // time, then use just its bytes between the square brackets.
    private static void WriteCanonicalArray<T>(
        CanonicalSegmentBuilder builder,
        IEnumerable<T> values,
        Action<Utf8JsonWriter, T> write)
    {
        builder.Glue.Byte((byte)'[');
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);
        var first = true;
        foreach (var chunk in values.Chunk(ArrayChunkSize))
        {
            if (!first)
            {
                builder.Glue.Byte((byte)',');
            }

            first = false;
            writer.WriteStartArray();
            foreach (var value in chunk)
            {
                write(writer, value);
            }

            writer.WriteEndArray();
            writer.Flush();
            var canonical = CanonicalJson.Canonicalize(buffer.WrittenSpan);
            builder.Fragment(canonical.AsMemory(1, canonical.Length - 2));
            buffer.ResetWrittenCount();
            writer.Reset(buffer);
        }

        builder.Glue.Byte((byte)']');
    }

    private static void WriteCommitmentLedger(CanonicalSegmentBuilder builder, CommitmentLedger ledger)
    {
        var glue = builder.Glue;
        glue.Byte((byte)'[');
        var first = true;
        foreach (var history in ledger.Histories.Values.OrderBy(
                     value => value.RequestId.Value, StringComparer.Ordinal))
        {
            if (!first)
            {
                glue.Byte((byte)',');
            }

            first = false;
            var item = new CanonicalObject(glue);
            item.Member("entries"u8);
            WriteCanonicalArray(builder, history.Entries, WriteLedgerEntry);
            item.Member("requestId"u8).String(history.RequestId.Value);
            item.End();
        }

        glue.Byte((byte)']');
    }

    private static void WriteIncidentLedger(CanonicalSegmentBuilder builder, OperationalIncidentLedger ledger)
    {
        var item = new CanonicalObject(builder.Glue);
        item.Member("breaches"u8);
        WriteCanonicalArray(builder, ledger.Breaches, WriteBreach);
        item.Member("incidents"u8);
        WriteCanonicalArray(builder, ledger.Incidents.Values.OrderBy(
            incident => incident.Id.Value, StringComparer.Ordinal), WriteIncident);

        if (ledger.LatePickups.Count != 0)
        {
            item.Member("latePickups"u8);
            WriteCanonicalArray(builder, ledger.LatePickups, WriteLatePickup);
        }

        item.End();
    }

    private static void WriteTravelTimes(CanonicalSegmentBuilder builder, TravelTimeSnapshot travel)
    {
        var item = new CanonicalObject(builder.Glue);
        item.Member("arcs"u8);
        WriteCanonicalArray(builder, travel.TravelTimes.OrderBy(
            arc => arc.Key.FromNodeId.Value, StringComparer.Ordinal)
            .ThenBy(arc => arc.Key.ToNodeId.Value, StringComparer.Ordinal), WriteArc);
        item.Member("snapshotHash"u8).String(travel.SnapshotHash);
        item.Member("version"u8).Integer(travel.Version);
        item.End();
    }

    private static void WriteShadowAllocation(
        Utf8JsonWriter writer, KeyValuePair<RequestId, ShadowAllocation> pair)
    {
        writer.WriteStartObject();
        writer.WriteString("requestId", pair.Key.Value);
        writer.WriteString("class", pair.Value.Class switch
        {
            ShadowAllocationClass.Guaranteed => "guaranteed",
            ShadowAllocationClass.Allowed => "allowed",
            _ => throw new InvalidOperationException("Unknown commitment allocation class."),
        });
        writer.WriteString("score", pair.Value.Score);
        writer.WriteEndObject();
    }

    private static void WriteVehicle(Utf8JsonWriter writer, VehicleState vehicle)
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

    private static void WriteArc(Utf8JsonWriter writer, KeyValuePair<TravelArc, Duration> arc)
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

    private static void WriteLedgerEntry(Utf8JsonWriter writer, CommitmentLedgerEntry entry)
    {
        writer.WriteStartObject();
        writer.WriteString("kind", entry.Kind == CommitmentLedgerEntryKind.InitialPromise
            ? "initialPromise"
            : "revision");
        writer.WriteString("publicationId", entry.PublicationId);
        writer.WriteString("reasonCode", entry.ReasonCode);
        writer.WriteNumber("sourceEventSeq", entry.SourceEventSequence);
        WritePublishedPromise(writer, "publishedPromise", entry.PublishedPromise);
        if (entry.PreviousPromise is { } previous)
        {
            WritePublishedPromise(writer, "previousPromise", previous);
        }

        writer.WritePropertyName("exogenousProjection");
        CommitmentContractCodec.WritePromise(
            writer, OnlineDecisionActionMapper.MapPromise(entry.ExogenousProjection));
        WriteVector(writer, "exogenousDelta", entry.Deltas.Exogenous);
        WriteVector(writer, "decisionDelta", entry.Deltas.DecisionInduced);
        WriteVector(writer, "visibleDelta", entry.Deltas.Visible);
        WriteVector(writer, "budgetBefore", entry.BudgetBefore);
        WriteVector(writer, "budgetAfter", entry.BudgetAfter);
        writer.WriteEndObject();
    }

    private static void WriteIncident(Utf8JsonWriter writer, OperationalIncident incident)
    {
        writer.WriteStartObject();
        writer.WriteString("incidentId", incident.Id.Value);
        writer.WriteString("reasonCode", incident.ReasonCode);
        WriteRequestSet(writer, "affectedRequestIds", incident.AffectedRequestIds);
        WriteIdentifiers(writer, "affectedVehicleIds", incident.AffectedVehicleIds.Select(value => value.Value));
        writer.WriteNumber("openedEventSeq", incident.OpenedEventSequence);
        writer.WriteNumber("openedAtMs", incident.OpenedAt.Milliseconds);
        if (incident.ResolvedEventSequence is long resolved)
        {
            writer.WriteNumber("resolvedEventSeq", resolved);
            writer.WriteNumber("resolvedAtMs", incident.ResolvedAt!.Value.Milliseconds);
        }

        writer.WriteEndObject();
    }

    private static void WriteLatePickup(Utf8JsonWriter writer, ObservedLatePickup late)
    {
        writer.WriteStartObject();
        writer.WriteString("requestId", late.RequestId.Value);
        writer.WriteString("vehicleId", late.VehicleId.Value);
        writer.WriteNumber("latestPickupMs", late.LatestPickup.Milliseconds);
        writer.WriteNumber("actualPickupMs", late.ActualPickup.Milliseconds);
        writer.WriteNumber("sourceEventSeq", late.SourceEventSequence);
        writer.WriteNumber("recordedEpoch", late.RecordedEpoch);
        writer.WriteEndObject();
    }

    private static void WritePublishedPromise(
        Utf8JsonWriter writer, string field, PublishedPromise promise)
    {
        writer.WritePropertyName(field);
        writer.WriteStartObject();
        writer.WriteNumber("version", promise.Version.Value);
        writer.WriteNumber("publishedEpoch", promise.PublishedEpoch);
        writer.WriteNumber("publishedAtMs", promise.PublishedAt.Milliseconds);
        writer.WritePropertyName("projection");
        CommitmentContractCodec.WritePromise(
            writer, OnlineDecisionActionMapper.MapPromise(promise.Projection));
        writer.WriteEndObject();
    }

    private static void WriteVector(Utf8JsonWriter writer, string field, CommitmentVector vector)
    {
        writer.WritePropertyName(field);
        CommitmentContractCodec.WriteVector(writer, OnlineDecisionActionMapper.MapVector(vector));
    }

    private static void WriteBreach(Utf8JsonWriter writer, CommitmentBreachRecord breach)
    {
        writer.WriteStartObject();
        writer.WriteString("breachId", breach.BreachId);
        writer.WriteString("requestId", breach.RequestId.Value);
        writer.WriteBoolean("normalOperation", breach.NormalOperation);
        switch (breach.Kind)
        {
            case CommitmentBreachKind.OperationalIncident:
                writer.WriteString("incidentId", breach.IncidentId!.Value.Value);
                break;
            case CommitmentBreachKind.ExogenousServiceQuality:
                writer.WriteString("kind", "exogenousServiceQuality");
                writer.WritePropertyName("serviceQualityWitnesses");
                writer.WriteStartArray();
                foreach (var witness in breach.ServiceQualityWitnesses)
                {
                    writer.WriteStartObject();
                    writer.WriteString("requestId", witness.RequestId.Value);
                    writer.WriteString("code", witness.Code);
                    writer.WriteString("dimension", witness.Dimension);
                    writer.WriteNumber("contractualMilliseconds", witness.ContractualMilliseconds);
                    writer.WriteNumber("exogenousMilliseconds", witness.ExogenousMilliseconds);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                break;
            default:
                throw new InvalidOperationException("Unknown commitment breach kind.");
        }

        WritePublishedPromise(writer, "previousPromise", breach.PreviousPromise);
        writer.WritePropertyName("exogenousProjection");
        CommitmentContractCodec.WritePromise(writer, OnlineDecisionActionMapper.MapPromise(breach.ExogenousProjection));
        writer.WritePropertyName("safetyProjection");
        CommitmentContractCodec.WritePromise(writer, OnlineDecisionActionMapper.MapPromise(breach.SafetyProjection));
        writer.WritePropertyName("deltas");
        writer.WriteStartObject();
        WriteVector(writer, "exogenous", breach.Deltas.Exogenous);
        WriteVector(writer, "decisionInduced", breach.Deltas.DecisionInduced);
        WriteVector(writer, "visible", breach.Deltas.Visible);
        writer.WriteEndObject();
        WriteVector(writer, "budgetBefore", breach.BudgetBefore);
        WriteVector(writer, "attemptedBudgetAfter", breach.AttemptedBudgetAfter);
        WriteIdentifiers(writer, "witnessCodes", breach.WitnessCodes);
        writer.WriteNumber("recordedAtMs", breach.RecordedAt.Milliseconds);
        writer.WriteNumber("recordedEpoch", breach.RecordedEpoch);
        writer.WriteNumber("sourceEventSeq", breach.SourceEventSequence);
        writer.WriteEndObject();
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
        IEnumerable<RequestId> values) =>
        WriteIdentifiers(writer, propertyName, values.Select(value => value.Value));

    private static void WriteIdentifiers(
        Utf8JsonWriter writer,
        string propertyName,
        IEnumerable<string> values)
    {
        writer.WritePropertyName(propertyName);
        writer.WriteStartArray();
        foreach (var value in values.Order(StringComparer.Ordinal))
        {
            writer.WriteStringValue(value);
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
}
