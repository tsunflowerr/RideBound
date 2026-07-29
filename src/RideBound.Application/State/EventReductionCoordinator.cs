using RideBound.Application.Events;

namespace RideBound.Application.State;

public sealed class EventReductionCoordinator
{
    private readonly EventReducer _reducer;
    private OnlineState? _pendingState;

    public EventReductionCoordinator(
        OnlineState initialState,
        EventReducer? reducer = null)
    {
        ArgumentNullException.ThrowIfNull(initialState);
        CommittedState = initialState;
        _reducer = reducer ?? new EventReducer();
    }

    public OnlineState CommittedState { get; private set; }

    public OnlineState? PendingState => _pendingState;

    public EventReductionResult Propose(InternalEventBatch batch)
    {
        if (_pendingState is not null)
        {
            return EventReductionResult.Failure(
                new EventReductionWitness(
                    EventReductionFailureCodes.PendingTransitionExists,
                    "A proposed state is already waiting for decisionApplied."));
        }

        var result = _reducer.Reduce(CommittedState, batch);

        if (result.IsSuccess)
        {
            _pendingState = result.ProposedState;
        }

        return result;
    }

    public EventReductionResult ApplyDecisionAcknowledgement(long epoch)
    {
        if (_pendingState is null)
        {
            return EventReductionResult.Failure(
                new EventReductionWitness(
                    EventReductionFailureCodes.NoPendingTransition,
                    "There is no proposed state to acknowledge."));
        }

        if (_pendingState.Run.AppliedEpoch != epoch)
        {
            return EventReductionResult.Failure(
                new EventReductionWitness(
                    EventReductionFailureCodes.AcknowledgementMismatch,
                    "decisionApplied epoch does not match the proposed state.",
                    Dimension: "epoch"));
        }

        CommittedState = _pendingState;
        _pendingState = null;
        return EventReductionResult.Success(CommittedState);
    }
}
