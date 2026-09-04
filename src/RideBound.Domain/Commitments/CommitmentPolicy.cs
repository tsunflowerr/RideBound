using System.Collections.Frozen;
using RideBound.Domain.Common;

namespace RideBound.Domain.Commitments;

[Flags]
public enum CommitmentPhase
{
    None = 0,
    Accepted = 1,
    WaitingPickup = 2,
    Onboard = 4,
    AllActive = Accepted | WaitingPickup | Onboard,
}

[Flags]
public enum PromiseLock
{
    None = 0,
    Vehicle = 1,
    PickupStop = 2,
    DropStop = 4,
    PickupEta = 8,
    DropEta = 16,
}

public enum CommitmentBudgetBasis
{
    DecisionInduced,
    CustomerVisible,
}

public sealed record CommitmentDimensionLimit
{
    public CommitmentDimensionLimit(
        CommitmentDimension dimension,
        long? hardLimit,
        CommitmentPhase applicablePhases)
    {
        if (!Enum.IsDefined(dimension))
        {
            throw new ArgumentOutOfRangeException(nameof(dimension));
        }

        if (hardLimit is < 0 or > DomainLimits.MaxCanonicalInteger)
        {
            throw new ArgumentOutOfRangeException(nameof(hardLimit));
        }

        if (applicablePhases == CommitmentPhase.None
            || (applicablePhases & ~CommitmentPhase.AllActive) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(applicablePhases));
        }

        Dimension = dimension;
        HardLimit = hardLimit;
        ApplicablePhases = applicablePhases;
    }

    public CommitmentDimension Dimension { get; }

    public long? HardLimit { get; }

    public CommitmentPhase ApplicablePhases { get; }
}

public sealed record MaterialRevisionRule
{
    public MaterialRevisionRule(
        long? rawEtaThresholdMs,
        long? displayBucketWidthMs)
    {
        if (rawEtaThresholdMs is < 1 or > DomainLimits.MaxCanonicalInteger)
        {
            throw new ArgumentOutOfRangeException(nameof(rawEtaThresholdMs));
        }

        if (displayBucketWidthMs is < 1 or > DomainLimits.MaxCanonicalInteger)
        {
            throw new ArgumentOutOfRangeException(nameof(displayBucketWidthMs));
        }

        if (rawEtaThresholdMs is null && displayBucketWidthMs is null)
        {
            throw new ArgumentException(
                "A material revision rule requires a raw threshold or display bucket.");
        }

        RawEtaThresholdMs = rawEtaThresholdMs;
        DisplayBucketWidthMs = displayBucketWidthMs;
    }

    public long? RawEtaThresholdMs { get; }

    public long? DisplayBucketWidthMs { get; }

    public bool IsMaterial(
        SimTime oldPickup,
        SimTime newPickup,
        SimTime oldDrop,
        SimTime newDrop)
    {
        var pickupDelta = AbsoluteDifference(oldPickup, newPickup);
        var dropDelta = AbsoluteDifference(oldDrop, newDrop);
        var thresholdChanged = RawEtaThresholdMs is long threshold
            && (pickupDelta >= threshold || dropDelta >= threshold);
        var bucketChanged = DisplayBucketWidthMs is long bucket
            && (oldPickup.Milliseconds / bucket
                    != newPickup.Milliseconds / bucket
                || oldDrop.Milliseconds / bucket
                    != newDrop.Milliseconds / bucket);

        return thresholdChanged || bucketChanged;
    }

    private static long AbsoluteDifference(SimTime left, SimTime right) =>
        left.Milliseconds >= right.Milliseconds
            ? left.Milliseconds - right.Milliseconds
            : right.Milliseconds - left.Milliseconds;
}

public sealed class CommitmentPolicy
{
    private readonly FrozenDictionary<
        CommitmentDimension,
        CommitmentDimensionLimit> _limits;

    public CommitmentPolicy(
        string policyId,
        CommitmentBudgetBasis budgetBasis,
        IEnumerable<CommitmentDimensionLimit> limits,
        MaterialRevisionRule materialRevisionRule,
        Duration? freezeHorizon = null,
        PromiseLock freezeHorizonLocks = PromiseLock.None,
        PromiseLock finalConfirmationLocks = PromiseLock.None,
        PromiseLock ratchetLocks = PromiseLock.None)
    {
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(materialRevisionRule);
        PolicyId = DomainIdentifier.Require(policyId, nameof(policyId));
        var materialized = limits.ToArray();
        const PromiseLock allLocks =
            PromiseLock.Vehicle
            | PromiseLock.PickupStop
            | PromiseLock.DropStop
            | PromiseLock.PickupEta
            | PromiseLock.DropEta;

        if (!Enum.IsDefined(budgetBasis))
        {
            throw new ArgumentOutOfRangeException(nameof(budgetBasis));
        }

        if (materialized.Length != CommitmentDimensionVocabulary.Ordered.Count
            || materialized
                .Select(value => value.Dimension)
                .Distinct()
                .Count() != CommitmentDimensionVocabulary.Ordered.Count)
        {
            throw new ArgumentException(
                "A commitment policy must define every dimension exactly once.",
                nameof(limits));
        }

        if (freezeHorizon is null && freezeHorizonLocks != PromiseLock.None)
        {
            throw new ArgumentException(
                "Freeze-horizon locks require a freeze horizon.",
                nameof(freezeHorizonLocks));
        }

        if ((freezeHorizonLocks & ~allLocks) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(freezeHorizonLocks));
        }

        if ((finalConfirmationLocks & ~allLocks) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(finalConfirmationLocks));
        }

        const PromiseLock orderedLocks =
            PromiseLock.PickupEta | PromiseLock.DropEta;

        // A ratchet needs an order on the field. Vehicle and stop identities have
        // none, so only the two ETA fields can be relaxed this way.
        if ((ratchetLocks & ~orderedLocks) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ratchetLocks),
                "Only pickup and drop ETA locks can be relaxed to a ratchet.");
        }

        BudgetBasis = budgetBasis;
        _limits = materialized.ToFrozenDictionary(value => value.Dimension);
        MaterialRevisionRule = materialRevisionRule;
        FreezeHorizon = freezeHorizon;
        FreezeHorizonLocks = freezeHorizonLocks;
        FinalConfirmationLocks = finalConfirmationLocks;
        RatchetLocks = ratchetLocks;
    }

    public string PolicyId { get; }

    public CommitmentBudgetBasis BudgetBasis { get; }

    public IReadOnlyDictionary<
        CommitmentDimension,
        CommitmentDimensionLimit> Limits => _limits;

    public MaterialRevisionRule MaterialRevisionRule { get; }

    public Duration? FreezeHorizon { get; }

    public PromiseLock FreezeHorizonLocks { get; }

    public PromiseLock FinalConfirmationLocks { get; }

    /// <summary>
    /// Locked ETA fields the decision may still improve. A field named here is
    /// violated only when the candidate moves it later than the exogenous
    /// projection; moving it earlier is allowed. This is the one-sided guarantee
    /// used in the ride-pooling literature, where a matched request's latest
    /// pickup time is tightened to the expected pickup time and never loosened.
    /// Empty by default, so a lock keeps its exact-equality meaning.
    /// </summary>
    public PromiseLock RatchetLocks { get; }
}
