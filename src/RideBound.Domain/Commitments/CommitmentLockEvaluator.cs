using RideBound.Domain.Common;
using RideBound.Domain.Requests;

namespace RideBound.Domain.Commitments;

public sealed record CommitmentLockWitness(
    RequestId RequestId,
    string Dimension,
    string Rule);

public sealed class CommitmentLockEvaluator
{
    /// <summary>Rule of a drop ETA later than the initial promise plus the slack.</summary>
    public const string DeadlineCapRule = "deadline_cap";

    /// <summary>Rule of a drop ETA earlier than the initial promise minus the slack.</summary>
    public const string DeadlineFloorRule = "deadline_floor";

    public IReadOnlyList<CommitmentLockWitness> Evaluate(
        RideRequest request,
        PublishedPromise previous,
        PromiseProjection exogenous,
        PromiseProjection candidate,
        SimTime evaluationTime,
        CommitmentPolicy policy,
        PromiseProjection? anchor = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(exogenous);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(policy);

        if (previous.Projection.RequestId != request.Id
            || exogenous.RequestId != request.Id
            || candidate.RequestId != request.Id)
        {
            throw new ArgumentException(
                "Lock evaluation request and promise identities must match.");
        }

        var activeLocks = PromiseLock.Vehicle;
        var rules = new List<(PromiseLock Locks, string Rule)>();

        if (request.Lifecycle == RequestLifecycle.Onboard)
        {
            rules.Add(
                (
                    PromiseLock.Vehicle
                    | PromiseLock.PickupStop
                    | PromiseLock.PickupEta,
                    "onboard"));
        }

        if (request.Lifecycle == RequestLifecycle.WaitingPickup
            && policy.FinalConfirmationLocks != PromiseLock.None)
        {
            rules.Add((policy.FinalConfirmationLocks, "final_confirmation"));
        }

        if (policy.FreezeHorizon is Duration horizon
            && previous.Projection.PickupEta.Milliseconds
                >= evaluationTime.Milliseconds
            && previous.Projection.PickupEta.Milliseconds
                - evaluationTime.Milliseconds
                <= horizon.Milliseconds
            && policy.FreezeHorizonLocks != PromiseLock.None)
        {
            rules.Add((policy.FreezeHorizonLocks, "freeze_horizon"));
        }

        rules.Insert(0, (activeLocks, "accepted_assignment"));
        var witnesses = new List<CommitmentLockWitness>();

        foreach (var (locks, rule) in rules)
        {
            AddWitnesses(
                witnesses,
                request.Id,
                exogenous,
                candidate,
                locks,
                rule,
                policy.RatchetLocks);
        }

        var ordered = witnesses
            .Distinct()
            .OrderBy(value => value.Dimension, StringComparer.Ordinal)
            .ThenBy(value => value.Rule, StringComparer.Ordinal)
            .ToList();

        // The deadline witness goes last, so a lock witness keeps its position
        // and callers reading the first witness still see the lock.
        if (policy.DropEtaDeadlineSlack is Duration slack)
        {
            AddDeadlineWitness(ordered, request, candidate, anchor, slack, policy);
        }

        return ordered.ToArray();
    }

    private static void AddWitnesses(
        ICollection<CommitmentLockWitness> witnesses,
        RequestId requestId,
        PromiseProjection previous,
        PromiseProjection candidate,
        PromiseLock locks,
        string rule,
        PromiseLock ratchetLocks)
    {
        if ((locks & PromiseLock.Vehicle) != 0
            && previous.VehicleId != candidate.VehicleId)
        {
            witnesses.Add(
                new CommitmentLockWitness(requestId, "vehicle_id", rule));
        }

        if ((locks & PromiseLock.PickupStop) != 0
            && (previous.PickupStopId != candidate.PickupStopId
                || previous.PickupNodeId != candidate.PickupNodeId))
        {
            witnesses.Add(
                new CommitmentLockWitness(requestId, "pickup_stop", rule));
        }

        if ((locks & PromiseLock.DropStop) != 0
            && (previous.DropStopId != candidate.DropStopId
                || previous.DropNodeId != candidate.DropNodeId))
        {
            witnesses.Add(
                new CommitmentLockWitness(requestId, "drop_stop", rule));
        }

        if ((locks & PromiseLock.PickupEta) != 0
            && Violates(
                previous.PickupEta,
                candidate.PickupEta,
                (ratchetLocks & PromiseLock.PickupEta) != 0))
        {
            witnesses.Add(
                new CommitmentLockWitness(requestId, "pickup_eta_ms", rule));
        }

        if ((locks & PromiseLock.DropEta) != 0
            && Violates(
                previous.DropEta,
                candidate.DropEta,
                (ratchetLocks & PromiseLock.DropEta) != 0))
        {
            witnesses.Add(
                new CommitmentLockWitness(requestId, "drop_eta_ms", rule));
        }
    }

    /// <summary>
    /// Every lock above compares the candidate with the exogenous projection, so
    /// exogenous drift never trips a lock. The deadline compares it with the
    /// rider's initial promise instead, so drift alone can trip it, including on
    /// the safety no-op. It is a gate that lives next to the locks, not a lock.
    /// </summary>
    private static void AddDeadlineWitness(
        ICollection<CommitmentLockWitness> witnesses,
        RideRequest request,
        PromiseProjection candidate,
        PromiseProjection? anchor,
        Duration slack,
        CommitmentPolicy policy)
    {
        if (anchor is null)
        {
            throw new ArgumentException(
                "A drop ETA deadline requires the initial promise anchor.",
                nameof(anchor));
        }

        if (anchor.RequestId != request.Id)
        {
            throw new ArgumentException(
                "Deadline anchor identity must match the request.",
                nameof(anchor));
        }

        // Both values are canonical non-negative integers below 2^53, so the
        // difference and its negation fit in a long.
        var shift = candidate.DropEta.Milliseconds - anchor.DropEta.Milliseconds;

        if (shift > slack.Milliseconds)
        {
            witnesses.Add(
                new CommitmentLockWitness(request.Id, "drop_eta_ms", DeadlineCapRule));
        }
        else if (policy.DropEtaDeadlineTwoSided && -shift > slack.Milliseconds)
        {
            witnesses.Add(
                new CommitmentLockWitness(request.Id, "drop_eta_ms", DeadlineFloorRule));
        }
    }

    /// <summary>
    /// An exact lock rejects any movement. A ratcheted lock rejects only movement
    /// that makes the promise later, so the decision may still improve it.
    /// </summary>
    private static bool Violates(SimTime before, SimTime after, bool ratcheted) =>
        ratcheted
            ? after.Milliseconds > before.Milliseconds
            : after != before;
}
