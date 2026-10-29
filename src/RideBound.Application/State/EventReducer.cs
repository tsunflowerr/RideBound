using RideBound.Application.Events;
using RideBound.Application.Travel;
using RideBound.Domain.Common;
using RideBound.Domain.Runs;

namespace RideBound.Application.State;

public sealed class EventReducer
{
    public EventReductionResult Reduce(
        OnlineState state,
        InternalEventBatch batch)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(batch);

        var structuralFailure = ValidateBatch(state, batch);

        if (structuralFailure is not null)
        {
            return EventReductionResult.Failure(structuralFailure);
        }

        var run = state.Run;
        var travelTimes = state.TravelTimes;

        for (var index = 0; index < batch.Events.Count; index++)
        {
            var onlineEvent = batch.Events[index];
            var applied = ApplyEvent(
                run,
                travelTimes,
                state.ExpectedInitialTravelTimeSnapshotHash,
                batch.Epoch,
                onlineEvent);

            if (applied.Failure is not null)
            {
                return EventReductionResult.Failure(
                    ToWitness(applied.Failure, index, onlineEvent.EventSequence));
            }

            run = applied.Run!;
            travelTimes = applied.TravelTimes;
        }

        if (travelTimes is null)
        {
            return EventReductionResult.Failure(
                new EventReductionWitness(
                    EventReductionFailureCodes.TravelSnapshotRequired,
                    "The first proposed online state requires a travel snapshot."));
        }

        if (run.Vehicles.Count == 0)
        {
            return EventReductionResult.Failure(
                new EventReductionWitness(
                    EventReductionFailureCodes.VehicleBootstrapRequired,
                    "The first proposed online state requires at least one vehicle."));
        }

        var advanced = run.AdvanceEpoch(batch.Epoch, batch.SimulationTime);

        if (!advanced.IsSuccess)
        {
            return EventReductionResult.Failure(
                ToWitness(advanced.Failure!));
        }

        var nextSequence = batch.Events[^1].EventSequence + 1;
        return EventReductionResult.Success(
            new OnlineState(
                advanced.Value!,
                travelTimes,
                nextSequence,
                state.ExpectedInitialTravelTimeSnapshotHash));
    }

    private static EventReductionWitness? ValidateBatch(
        OnlineState state,
        InternalEventBatch batch)
    {
        if (batch.RunId != state.Run.Id
            || batch.ScenarioId != state.Run.ScenarioId)
        {
            return new EventReductionWitness(
                EventReductionFailureCodes.IdentityMismatch,
                "Internal event batch identity does not match the run.");
        }

        if (batch.Epoch != state.Run.AppliedEpoch + 1)
        {
            return new EventReductionWitness(
                EventReductionFailureCodes.InvalidEpoch,
                "Internal event batch epoch must advance by one.",
                Dimension: "epoch");
        }

        if (batch.SimulationTime.Milliseconds
            < state.Run.SimulationTime.Milliseconds)
        {
            return new EventReductionWitness(
                EventReductionFailureCodes.InvalidEventTime,
                "Batch simulation time cannot move backwards.",
                Dimension: "simTimeMs");
        }

        if (batch.Events.Count == 0)
        {
            return new EventReductionWitness(
                EventReductionFailureCodes.EmptyBatch,
                "Internal event batch cannot be empty.");
        }

        var expectedSequence = state.NextEventSequence;

        for (var index = 0; index < batch.Events.Count; index++)
        {
            var onlineEvent = batch.Events[index];

            if (onlineEvent.EventSequence != expectedSequence)
            {
                return new EventReductionWitness(
                    EventReductionFailureCodes.InvalidEventSequence,
                    "Internal event sequence must be globally consecutive.",
                    index,
                    onlineEvent.EventSequence,
                    Dimension: "eventSeq");
            }

            if (onlineEvent.SimulationTime != batch.SimulationTime)
            {
                return new EventReductionWitness(
                    EventReductionFailureCodes.InvalidEventTime,
                    "Every internal event must use the batch simulation time.",
                    index,
                    onlineEvent.EventSequence,
                    Dimension: "simTimeMs");
            }

            expectedSequence++;
        }

        return null;
    }

    private static EventApplyResult ApplyEvent(
        RideBoundRun run,
        TravelTimeSnapshot? travelTimes,
        string expectedInitialTravelTimeSnapshotHash,
        long epoch,
        OnlineEvent onlineEvent)
    {
        DomainResult<RideBoundRun>? runResult = onlineEvent switch
        {
            RequestArrived arrived => run.AddRequest(arrived.Request),
            BookingConfirmed confirmed =>
                run.ConfirmWaitingPickup(confirmed.RequestId),
            OfferDeclined declined => run.RejectRequest(declined.RequestId),
            RequestCancelledBeforeAcceptance cancelled =>
                run.CancelBeforeAcceptance(cancelled.RequestId),
            RequestCancelledAfterAcceptance cancelled =>
                run.CancelAfterAcceptance(cancelled.RequestId),
            VehicleAdvanced advanced when run.Vehicles.ContainsKey(
                advanced.Observation.Id) =>
                run.ObserveVehicle(advanced.Observation),
            VehicleAdvanced advanced when epoch == 1 =>
                run.BootstrapVehicle(advanced.Observation),
            VehicleAdvanced advanced =>
                DomainResult<RideBoundRun>.Fail(
                    RunFailureCodes.UnknownVehicle,
                    "Unknown vehicle cannot be introduced after bootstrap.",
                    advanced.Observation.Id.Value,
                    "vehicleId"),
            VehicleReachedStop reached =>
                run.ReachStop(
                    reached.VehicleId,
                    reached.StopId,
                    reached.PlanVersion,
                    reached.Position,
                    epoch),
            PassengerBoarded boarded =>
                run.Board(
                    boarded.VehicleId,
                    boarded.RequestId,
                    boarded.PlanVersion,
                    boarded.SimulationTime),
            PassengerAlighted alighted =>
                run.Alight(
                    alighted.VehicleId,
                    alighted.RequestId,
                    alighted.PlanVersion),
            TimerTick => DomainResult<RideBoundRun>.Success(run),
            TravelTimesUpdated => null,
            _ => DomainResult<RideBoundRun>.Fail(
                "UNSUPPORTED_INTERNAL_EVENT",
                $"Internal event '{onlineEvent.GetType().Name}' is unsupported."),
        };

        if (runResult is not null && !runResult.IsSuccess)
        {
            return EventApplyResult.Fail(runResult.Failure!);
        }

        if (onlineEvent is TravelTimesUpdated travel)
        {
            if (travelTimes is null
                && !string.Equals(
                    travel.Snapshot.SnapshotHash,
                    expectedInitialTravelTimeSnapshotHash,
                    StringComparison.Ordinal))
            {
                return EventApplyResult.Fail(
                    new DomainFailure(
                        EventReductionFailureCodes.TravelSnapshotIdentityMismatch,
                        "Initial travel snapshot hash does not match the run manifest.",
                        Dimension: "snapshotHash"));
            }

            if (travelTimes is not null
                && travel.Snapshot.Version != travelTimes.Version + 1)
            {
                return EventApplyResult.Fail(
                    new DomainFailure(
                        TravelFailureCodes.StaleSnapshot,
                        "Travel snapshot version must advance by exactly one.",
                        Dimension: "version"));
            }

            travelTimes = travel.Snapshot;
        }

        return EventApplyResult.Success(runResult?.Value ?? run, travelTimes);
    }

    private static EventReductionWitness ToWitness(
        DomainFailure failure,
        int? index = null,
        long? sequence = null) =>
        new(
            failure.Code,
            failure.Message,
            index,
            sequence,
            failure.EntityId,
            failure.Dimension);

    private sealed record EventApplyResult(
        RideBoundRun? Run,
        TravelTimeSnapshot? TravelTimes,
        DomainFailure? Failure)
    {
        public static EventApplyResult Success(
            RideBoundRun run,
            TravelTimeSnapshot? travelTimes) =>
            new(run, travelTimes, null);

        public static EventApplyResult Fail(DomainFailure failure) =>
            new(null, null, failure);
    }
}
