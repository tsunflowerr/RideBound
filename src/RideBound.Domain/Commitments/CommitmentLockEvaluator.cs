using RideBound.Domain.Common;
using RideBound.Domain.Requests;

namespace RideBound.Domain.Commitments;

public sealed record CommitmentLockWitness(
    RequestId RequestId,
    string Dimension,
    string Rule);

public sealed class CommitmentLockEvaluator
{
    public IReadOnlyList<CommitmentLockWitness> Evaluate(
        RideRequest request,
        PublishedPromise previous,
        PromiseProjection exogenous,
        PromiseProjection candidate,
        SimTime evaluationTime,
        CommitmentPolicy policy)
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
                rule);
        }

        return witnesses
            .Distinct()
            .OrderBy(value => value.Dimension, StringComparer.Ordinal)
            .ThenBy(value => value.Rule, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AddWitnesses(
        ICollection<CommitmentLockWitness> witnesses,
        RequestId requestId,
        PromiseProjection previous,
        PromiseProjection candidate,
        PromiseLock locks,
        string rule)
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
            && previous.PickupEta != candidate.PickupEta)
        {
            witnesses.Add(
                new CommitmentLockWitness(requestId, "pickup_eta_ms", rule));
        }

        if ((locks & PromiseLock.DropEta) != 0
            && previous.DropEta != candidate.DropEta)
        {
            witnesses.Add(
                new CommitmentLockWitness(requestId, "drop_eta_ms", rule));
        }
    }
}
