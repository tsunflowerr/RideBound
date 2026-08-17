using System.Text.Json;
using RideBound.Benchmarking.Contracts;
using RideBound.Benchmarking.EndToEnd;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;

namespace RideBound.Benchmarking.Execution;

public sealed record PublicDerivativeMechanicalConversationSummary(
    long RequestCount,
    long AcceptedRequestCount,
    long RejectedRequestCount,
    long ProtocolEventCount,
    long FinalEpoch);

public sealed class PublicDerivativeMechanicalDrainConversation(
    PublicDerivativeMechanicalFixtureArtifacts fixture,
    byte[] initializeEnvelope) : IExternalProcessConversation
{
    public PublicDerivativeMechanicalConversationSummary? Summary { get; private set; }

    public async Task<ProcessConversationResult> ExecuteAsync(
        Stream standardInput,
        Stream standardOutput,
        CancellationToken cancellationToken)
    {
        var handshake = await PerformHandshake(
            standardInput,
            standardOutput,
            cancellationToken);

        if (handshake.Failure is not null)
        {
            return handshake.Failure;
        }

        var orderedRequests = OrderRequests();
        long nextSequence = 1;
        long epoch = 1;
        long accepted = 0;
        long rejected = 0;

        for (var index = 0; index < orderedRequests.Count; index++)
        {
            var request = orderedRequests[index];
            var arrivalEvents = new List<ProtocolEvent>();

            if (index == 0)
            {
                arrivalEvents.Add(CreateTravelEvent(ref nextSequence));

                foreach (var vehicle in fixture.Scenario.Fleet.OrderBy(
                             value => value.VehicleId,
                             StringComparer.Ordinal))
                {
                    arrivalEvents.Add(CreateVehicleEvent(vehicle, ref nextSequence));
                }
            }

            arrivalEvents.Add(CreateRequestEvent(request, ref nextSequence));
            var exchange = await ExchangeDecision(
                arrivalEvents,
                epoch,
                request.ArrivalTimeMs,
                handshake.Initialize!,
                standardInput,
                standardOutput,
                cancellationToken);

            if (exchange.Failure is not null)
            {
                return exchange.Failure;
            }

            var outcome = AnalyzeArrivalDecision(exchange.Decision!, request);

            if (outcome.Failure is not null)
            {
                return outcome.Failure;
            }

            if (index == 0)
            {
                var checkpointFailure = await CheckCheckpoint(
                    handshake,
                    exchange.Decision!,
                    epoch,
                    standardInput,
                    standardOutput,
                    cancellationToken);

                if (checkpointFailure is not null)
                {
                    return checkpointFailure;
                }
            }

            if (outcome.Drain is null)
            {
                rejected++;
                epoch++;
                continue;
            }

            accepted++;
            epoch++;
            var drainExchange = await ExchangeDecision(
                CreateDrainEvents(outcome.Drain, ref nextSequence),
                epoch,
                request.ArrivalTimeMs,
                handshake.Initialize!,
                standardInput,
                standardOutput,
                cancellationToken);

            if (drainExchange.Failure is not null)
            {
                return drainExchange.Failure;
            }

            var drainFailure = ValidateDrainDecision(drainExchange.Decision!);

            if (drainFailure is not null)
            {
                return drainFailure;
            }

            epoch++;
        }

        Summary = new PublicDerivativeMechanicalConversationSummary(
            orderedRequests.Count,
            accepted,
            rejected,
            nextSequence - 1,
            epoch - 1);
        var shutdown = RunnerProtocolFixtureConversation.CreateEnvelope(
            "shutdown",
            "{}"u8.ToArray());
        await RunnerProtocolFixtureConversation.WriteCanonical(
            standardInput,
            shutdown,
            cancellationToken);
        standardInput.Close();
        var extra = new byte[1];
        var extraCount = await standardOutput.ReadAsync(extra, cancellationToken);
        return extraCount == 0
            ? ProcessConversationResult.Success()
            : ProcessConversationResult.Failed(
                "protocol.invalid-output",
                "Runner emitted extra bytes after the public derivative conversation.");
    }

    private async Task<HandshakeResult> PerformHandshake(
        Stream standardInput,
        Stream standardOutput,
        CancellationToken cancellationToken)
    {
        var hello = RunnerProtocolFixtureConversation.DecodeInput(
            fixture.HelloEnvelope,
            "hello");
        var helloPayload = HelloPayloadCodec.Decode(hello.Payload);

        if (!helloPayload.IsSuccess)
        {
            return HandshakeResult.Failed(helloPayload.Error!.Message);
        }

        await RunnerProtocolFixtureConversation.WriteEnvelope(
            standardInput,
            fixture.HelloEnvelope,
            cancellationToken);
        var helloAck = await RunnerProtocolFixtureConversation.ReadEnvelope(
            standardOutput,
            cancellationToken);

        if (!RunnerProtocolFixtureConversation.IsType(helloAck, "helloAck"))
        {
            return HandshakeResult.Failed(
                RunnerProtocolFixtureConversation.Unexpected("helloAck", helloAck));
        }

        var ackPayload = HelloAckPayloadCodec.Decode(helloAck.Payload);

        if (!ackPayload.IsSuccess)
        {
            return HandshakeResult.Failed(ackPayload.Error!.Message);
        }

        var initialize = RunnerProtocolFixtureConversation.DecodeInput(
            initializeEnvelope,
            "initializeRun");
        var initializePayload = InitializeRunPayloadCodec.Decode(initialize.Payload);

        if (!initializePayload.IsSuccess)
        {
            return HandshakeResult.Failed(initializePayload.Error!.Message);
        }

        if (!helloPayload.Value!.SupportedSchemaVersions.Contains(
                ackPayload.Value!.SelectedSchemaVersion)
            || ackPayload.Value.SelectedSchemaVersion
                != initializePayload.Value!.Manifest.ProtocolVersion
            || !RunnerProtocolFixtureConversation.CapabilitySelectionsEqual(
                ackPayload.Value.CapabilitySelection,
                initializePayload.Value.Manifest.CapabilitySelection))
        {
            return HandshakeResult.Failed(
                ProcessConversationResult.Failed(
                    "capability.divergence",
                    "Runner capability selection differs from the public fixture manifest."));
        }

        await RunnerProtocolFixtureConversation.WriteEnvelope(
            standardInput,
            initializeEnvelope,
            cancellationToken);
        var initialized = await RunnerProtocolFixtureConversation.ReadEnvelope(
            standardOutput,
            cancellationToken);

        if (!RunnerProtocolFixtureConversation.IsType(initialized, "initialized")
            || initialized.RunId != initialize.RunId
            || initialized.ScenarioId != initialize.ScenarioId)
        {
            return HandshakeResult.Failed(
                RunnerProtocolFixtureConversation.Unexpected(
                    "matching initialized",
                    initialized));
        }

        var initializedPayload = InitializedPayloadCodec.Decode(initialized.Payload);

        if (!initializedPayload.IsSuccess
            || initializedPayload.Value!.ManifestHash
                != ProtocolHash.CalculateManifestHash(initializePayload.Value.Manifest))
        {
            return HandshakeResult.Failed(
                ProcessConversationResult.Failed(
                    "state.divergence",
                    initializedPayload.Error?.Message
                        ?? "Runner manifest hash differs from public fixture input."));
        }

        return HandshakeResult.Success(
            initialize,
            initializePayload.Value,
            initializedPayload.Value);
    }

    private async Task<DecisionExchangeResult> ExchangeDecision(
        IReadOnlyList<ProtocolEvent> events,
        long epoch,
        long simTime,
        ProtocolEnvelope initialize,
        Stream standardInput,
        Stream standardOutput,
        CancellationToken cancellationToken)
    {
        _ = EpochId.TryCreate(epoch, out var epochId);
        _ = SimulationTimeMilliseconds.TryCreate(simTime, out var simulationTime);
        var envelope = RunnerProtocolFixtureConversation.CreateEnvelope(
            "eventBatch",
            EventBatchPayloadCodec.Encode(new EventBatchPayload(events)),
            initialize.RunId,
            initialize.ScenarioId,
            epochId,
            simulationTime);
        await RunnerProtocolFixtureConversation.WriteCanonical(
            standardInput,
            envelope,
            cancellationToken);
        var decision = await RunnerProtocolFixtureConversation.ReadEnvelope(
            standardOutput,
            cancellationToken);

        if (!RunnerProtocolFixtureConversation.IsType(decision, "decision")
            || decision.RunId != initialize.RunId
            || decision.ScenarioId != initialize.ScenarioId
            || decision.EpochId != epochId
            || decision.SimTime != simulationTime)
        {
            return DecisionExchangeResult.Failed(
                RunnerProtocolFixtureConversation.Unexpected(
                    "decision with exact public event context",
                    decision));
        }

        var payload = DecisionPayloadCodec.Decode(decision.Payload);

        if (!payload.IsSuccess)
        {
            return DecisionExchangeResult.Failed(
                ProcessConversationResult.Failed(
                    "protocol.invalid-output",
                    payload.Error!.Message));
        }

        var acknowledgement = RunnerProtocolFixtureConversation.CreateEnvelope(
            "decisionApplied",
            DecisionAppliedPayloadCodec.Encode(
                new DecisionAppliedPayload(payload.Value!.DecisionHash)),
            decision.RunId,
            decision.ScenarioId,
            decision.EpochId,
            decision.SimTime);
        await RunnerProtocolFixtureConversation.WriteCanonical(
            standardInput,
            acknowledgement,
            cancellationToken);
        return DecisionExchangeResult.Success(payload.Value);
    }

    private async Task<ProcessConversationResult?> CheckCheckpoint(
        HandshakeResult handshake,
        DecisionPayload decision,
        long epoch,
        Stream standardInput,
        Stream standardOutput,
        CancellationToken cancellationToken)
    {
        var checkpointRequest = RunnerProtocolFixtureConversation.CreateEnvelope(
            "checkpoint",
            "{}"u8.ToArray(),
            handshake.Initialize!.RunId,
            handshake.Initialize.ScenarioId);
        await RunnerProtocolFixtureConversation.WriteCanonical(
            standardInput,
            checkpointRequest,
            cancellationToken);
        var checkpoint = await RunnerProtocolFixtureConversation.ReadEnvelope(
            standardOutput,
            cancellationToken);

        if (!RunnerProtocolFixtureConversation.IsType(checkpoint, "checkpoint")
            || checkpoint.RunId != handshake.Initialize.RunId
            || checkpoint.ScenarioId != handshake.Initialize.ScenarioId)
        {
            return RunnerProtocolFixtureConversation.Unexpected(
                "checkpoint after first public request",
                checkpoint);
        }

        var payload = CheckpointPayloadCodec.Decode(checkpoint.Payload);

        if (!payload.IsSuccess)
        {
            return ProcessConversationResult.Failed(
                "protocol.invalid-output",
                payload.Error!.Message);
        }

        if (payload.Value!.Content.ManifestHash
                != handshake.Initialized!.ManifestHash
            || payload.Value.Content.AppliedEpoch != epoch
            || payload.Value.Content.PreviousDecisionHash != decision.DecisionHash)
        {
            return ProcessConversationResult.Failed(
                "state.divergence",
                "Public fixture checkpoint does not bind the first applied decision.");
        }

        return null;
    }

    private IReadOnlyList<ScenarioRequest> OrderRequests()
    {
        var byId = fixture.Scenario.Requests.ToDictionary(
            value => value.RequestId,
            StringComparer.Ordinal);
        var ordered = new List<ScenarioRequest>();

        foreach (var sourceEvent in fixture.Scenario.Events.OrderBy(
                     value => value.EventSequence))
        {
            if (!byId.TryGetValue(sourceEvent.StableSubjectId, out var request)
                || sourceEvent.EventType != "requestArrived"
                || sourceEvent.SimTimeMs != request.ArrivalTimeMs
                || sourceEvent.SourceRecordOrdinal != request.SourceRecordOrdinal)
            {
                throw new InvalidDataException(
                    "Public source event order does not exactly identify a request.");
            }

            ordered.Add(request);
        }

        if (ordered.Count != byId.Count
            || ordered.Select(value => value.RequestId)
                .Distinct(StringComparer.Ordinal).Count() != byId.Count)
        {
            throw new InvalidDataException(
                "Public source event order is not a bijection over selected requests.");
        }

        return ordered;
    }

    private ProtocolEvent CreateTravelEvent(ref long nextSequence)
    {
        var snapshot = fixture.Scenario.TravelSnapshots.Single();
        _ = Sha256Hex.TryCreate(snapshot.SnapshotHash, out var snapshotHash);
        return Event(
            ref nextSequence,
            EventType.TravelTimesUpdated,
            new TravelTimesUpdatedEventPayload(
                new TravelTimeSnapshotContract(
                    snapshot.Version,
                    snapshotHash!,
                    snapshot.Arcs.Select(
                        value => new TravelArcContract(
                            value.FromNodeId,
                            value.ToNodeId,
                            value.TravelTimeMs)).ToArray())));
    }

    private static ProtocolEvent CreateVehicleEvent(
        ScenarioVehicle vehicle,
        ref long nextSequence) =>
        Event(
            ref nextSequence,
            EventType.VehicleAdvanced,
            new VehicleAdvancedEventPayload(
                new VehicleSnapshotContract(
                    vehicle.VehicleId,
                    vehicle.Capacity,
                    vehicle.OccupiedSeats,
                    Position(vehicle.Position),
                    vehicle.OnboardRequestIds,
                    vehicle.AcceptedRequestIds,
                    Route(vehicle.InitialRoute))));

    private static ProtocolEvent CreateRequestEvent(
        ScenarioRequest request,
        ref long nextSequence) =>
        Event(
            ref nextSequence,
            EventType.RequestArrived,
            new RequestArrivedEventPayload(
                new RequestContract(
                    request.RequestId,
                    request.ArrivalTimeMs,
                    request.OriginNodeId,
                    request.DestinationNodeId,
                    request.EarliestPickupMs,
                    request.LatestPickupMs,
                    request.MaxRideTimeMs,
                    request.PartySize,
                    request.ServiceClass,
                    request.CommitmentPolicyId)));

    private static IReadOnlyList<ProtocolEvent> CreateDrainEvents(
        DrainInstruction instruction,
        ref long nextSequence) =>
        [
            Event(
                ref nextSequence,
                EventType.BookingConfirmed,
                new RequestReferenceEventPayload(instruction.RequestId)),
            Event(
                ref nextSequence,
                EventType.VehicleReachedStop,
                new VehicleReachedStopEventPayload(
                    instruction.VehicleId,
                    instruction.PickupStopId,
                    instruction.PlanVersion,
                    new NodePositionContract(instruction.PickupNodeId))),
            Event(
                ref nextSequence,
                EventType.PassengerBoarded,
                new PassengerEventPayload(
                    instruction.VehicleId,
                    instruction.RequestId,
                    instruction.PlanVersion)),
            Event(
                ref nextSequence,
                EventType.VehicleReachedStop,
                new VehicleReachedStopEventPayload(
                    instruction.VehicleId,
                    instruction.DropStopId,
                    instruction.PlanVersion,
                    new NodePositionContract(instruction.DropNodeId))),
            Event(
                ref nextSequence,
                EventType.PassengerAlighted,
                new PassengerEventPayload(
                    instruction.VehicleId,
                    instruction.RequestId,
                    instruction.PlanVersion)),
        ];

    private static ArrivalOutcome AnalyzeArrivalDecision(
        DecisionPayload decision,
        ScenarioRequest request)
    {
        var outcomes = decision.Actions.Where(
                action => action.GetProperty("decisionType").GetString()
                    is "requestAccepted" or "requestRejected" or "requestDeferred")
            .ToArray();

        if (outcomes.Length != 1
            || outcomes[0].GetProperty("payload")
                .GetProperty("requestId").GetString() != request.RequestId)
        {
            return ArrivalOutcome.Failed(
                "Each public arrival must receive exactly one matching terminal action.");
        }

        var outcomeType = outcomes[0].GetProperty("decisionType").GetString();

        if (outcomeType == "requestRejected")
        {
            return ArrivalOutcome.Rejected();
        }

        if (outcomeType != "requestAccepted")
        {
            return ArrivalOutcome.Failed(
                $"The instant-drain mechanical driver cannot leave request "
                    + $"'{request.RequestId}' deferred with reason "
                    + $"'{outcomes[0].GetProperty("payload").GetProperty("reasonCode").GetString()}' "
                    + $"(decision '{decision.ReasonCode}', solver '{decision.Solver.Status}').");
        }

        var accepted = outcomes[0].GetProperty("payload");
        var vehicleId = accepted.GetProperty("vehicleId").GetString()!;
        var candidateId = accepted.GetProperty("candidateId").GetString()!;
        var plans = decision.Actions.Where(
                action => action.GetProperty("decisionType").GetString()
                    == "vehiclePlanUpdated"
                    && action.GetProperty("payload")
                        .GetProperty("vehicleId").GetString() == vehicleId
                    && action.GetProperty("payload")
                        .GetProperty("candidateId").GetString() == candidateId)
            .ToArray();

        if (plans.Length != 1)
        {
            return ArrivalOutcome.Failed(
                "An accepted public request must bind one exact vehicle/candidate plan.");
        }

        var route = plans[0].GetProperty("payload").GetProperty("route");
        var frozen = route.GetProperty("frozenPrefix");
        var mutable = route.GetProperty("mutableSuffix").EnumerateArray().ToArray();

        if (route.GetProperty("executedStopCount").GetInt64()
                != frozen.GetArrayLength()
            || frozen.EnumerateArray().Any(
                stop => stop.TryGetProperty("requestId", out var requestId)
                    && requestId.GetString() == request.RequestId)
            || mutable.Length != 2
            || !StopMatches(
                mutable[0],
                "pickup",
                request.RequestId,
                request.OriginNodeId)
            || !StopMatches(
                mutable[1],
                "dropOff",
                request.RequestId,
                request.DestinationNodeId))
        {
            return ArrivalOutcome.Failed(
                $"Accepted public route does not preserve only historical frozen stops "
                    + "before the exact new pickup/drop pair "
                    + $"for '{request.RequestId}' ({request.OriginNodeId} -> "
                    + $"{request.DestinationNodeId}): {route.GetRawText()}");
        }

        return ArrivalOutcome.Accepted(
            new DrainInstruction(
                request.RequestId,
                vehicleId,
                route.GetProperty("planVersion").GetInt64(),
                mutable[0].GetProperty("stopId").GetString()!,
                mutable[0].GetProperty("nodeId").GetString()!,
                mutable[1].GetProperty("stopId").GetString()!,
                mutable[1].GetProperty("nodeId").GetString()!));
    }

    private static ProcessConversationResult? ValidateDrainDecision(
        DecisionPayload decision)
    {
        var allocationAction = decision.Actions.FirstOrDefault(
            action => action.GetProperty("decisionType").GetString()
                is "requestAccepted" or "requestRejected" or "requestDeferred"
                    or "vehiclePlanUpdated");
        return allocationAction.ValueKind == JsonValueKind.Undefined
            ? null
            : ProcessConversationResult.Failed(
                "state.divergence",
                "An instant-drain epoch unexpectedly emitted an allocation action.");
    }

    private static bool StopMatches(
        JsonElement stop,
        string kind,
        string requestId,
        string nodeId) =>
        stop.GetProperty("kind").GetString() == kind
        && stop.GetProperty("requestId").GetString() == requestId
        && stop.GetProperty("nodeId").GetString() == nodeId;

    private static ProtocolEvent Event(
        ref long nextSequence,
        EventType eventType,
        ProtocolEventPayload payload)
    {
        _ = EventSequence.TryCreate(nextSequence, out var sequence);
        nextSequence++;
        return new ProtocolEvent(sequence, eventType, payload);
    }

    private static PositionContract Position(ScenarioPosition position) =>
        position switch
        {
            NodeScenarioPosition node => new NodePositionContract(node.NodeId),
            EdgeProgressScenarioPosition edge => new EdgeProgressPositionContract(
                edge.FromNodeId,
                edge.ToNodeId,
                edge.EdgeId,
                edge.ProgressPermille),
            _ => throw new InvalidDataException("Unknown public vehicle position."),
        };

    private static RoutePlanContract Route(ScenarioRoute route) =>
        new(
            route.PlanVersion,
            route.ExecutedStopCount,
            route.FrozenPrefix.Select(Stop).ToArray(),
            route.MutableSuffix.Select(Stop).ToArray());

    private static RouteStopContract Stop(ScenarioRouteStop stop) =>
        new(
            stop.StopId,
            stop.NodeId,
            stop.Kind switch
            {
                ScenarioRouteStopKind.Waypoint => RouteStopKind.Waypoint,
                ScenarioRouteStopKind.Pickup => RouteStopKind.Pickup,
                ScenarioRouteStopKind.DropOff => RouteStopKind.DropOff,
                _ => throw new ArgumentOutOfRangeException(nameof(stop)),
            },
            stop.RequestId,
            stop.ServiceDurationMs);

    private sealed record HandshakeResult(
        ProtocolEnvelope? Initialize,
        InitializeRunPayload? InitializePayload,
        InitializedPayload? Initialized,
        ProcessConversationResult? Failure)
    {
        public static HandshakeResult Success(
            ProtocolEnvelope initialize,
            InitializeRunPayload initializePayload,
            InitializedPayload initialized) =>
            new(initialize, initializePayload, initialized, null);

        public static HandshakeResult Failed(string message) =>
            Failed(RunnerProtocolFixtureConversation.InvalidPayload(message));

        public static HandshakeResult Failed(ProcessConversationResult failure) =>
            new(null, null, null, failure);
    }

    private sealed record DecisionExchangeResult(
        DecisionPayload? Decision,
        ProcessConversationResult? Failure)
    {
        public static DecisionExchangeResult Success(DecisionPayload decision) =>
            new(decision, null);

        public static DecisionExchangeResult Failed(ProcessConversationResult failure) =>
            new(null, failure);
    }

    private sealed record ArrivalOutcome(
        DrainInstruction? Drain,
        ProcessConversationResult? Failure)
    {
        public static ArrivalOutcome Accepted(DrainInstruction instruction) =>
            new(instruction, null);

        public static ArrivalOutcome Rejected() => new(null, null);

        public static ArrivalOutcome Failed(string message) =>
            new(
                null,
                ProcessConversationResult.Failed("state.divergence", message));
    }

    private sealed record DrainInstruction(
        string RequestId,
        string VehicleId,
        long PlanVersion,
        string PickupStopId,
        string PickupNodeId,
        string DropStopId,
        string DropNodeId);
}
