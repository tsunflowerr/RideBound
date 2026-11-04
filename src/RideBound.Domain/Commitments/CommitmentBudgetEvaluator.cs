using RideBound.Domain.Common;
using RideBound.Domain.Requests;

namespace RideBound.Domain.Commitments;

public sealed record CommitmentBudgetWitness(
    RequestId RequestId,
    string Dimension,
    long Limit,
    long Before,
    long Delta,
    long After);

public sealed record CommitmentBudgetEvaluation
{
    private CommitmentBudgetEvaluation(
        CommitmentVector? after,
        IReadOnlyList<CommitmentBudgetWitness> witnesses)
    {
        After = after;
        Witnesses = witnesses;
    }

    public bool IsAllowed => After is not null && Witnesses.Count == 0;

    public CommitmentVector? After { get; }

    public IReadOnlyList<CommitmentBudgetWitness> Witnesses { get; }

    public static CommitmentBudgetEvaluation Allowed(
        CommitmentVector after) =>
        new(after, []);

    public static CommitmentBudgetEvaluation Rejected(
        IReadOnlyList<CommitmentBudgetWitness> witnesses) =>
        new(null, witnesses);
}

public sealed class CommitmentBudgetEvaluator
{
    public CommitmentBudgetEvaluation Evaluate(
        RequestId requestId,
        RequestLifecycle lifecycle,
        CommitmentVector before,
        CommitmentVector delta,
        CommitmentPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(delta);
        ArgumentNullException.ThrowIfNull(policy);
        var added = before.Add(delta);

        if (!added.IsSuccess)
        {
            var dimension = CommitmentDimensionVocabulary.Ordered.Single(
                candidate =>
                    CommitmentDimensionVocabulary.ToProtocolValue(candidate)
                    == added.Failure!.Dimension);

            return CommitmentBudgetEvaluation.Rejected(
            [
                new CommitmentBudgetWitness(
                    requestId,
                    added.Failure!.Dimension!,
                    DomainLimits.MaxCanonicalInteger,
                    before.Get(dimension),
                    delta.Get(dimension),
                    checked(before.Get(dimension) + delta.Get(dimension))),
            ]);
        }

        var phase = ToPhase(lifecycle);
        var after = added.Value!;
        var witnesses = new List<CommitmentBudgetWitness>();

        foreach (var dimension in CommitmentDimensionVocabulary.Ordered)
        {
            var limit = policy.Limits[dimension];

            if ((limit.ApplicablePhases & phase) == 0
                || limit.HardLimit is not long hardLimit)
            {
                continue;
            }

            var afterValue = after.Get(dimension);

            if (afterValue > hardLimit)
            {
                witnesses.Add(
                    new CommitmentBudgetWitness(
                        requestId,
                        CommitmentDimensionVocabulary.ToProtocolValue(dimension),
                        hardLimit,
                        before.Get(dimension),
                        delta.Get(dimension),
                        afterValue));
            }
        }

        return witnesses.Count == 0
            ? CommitmentBudgetEvaluation.Allowed(after)
            : CommitmentBudgetEvaluation.Rejected(
                witnesses.AsReadOnly());
    }

    private static CommitmentPhase ToPhase(RequestLifecycle lifecycle) =>
        lifecycle switch
        {
            RequestLifecycle.Accepted => CommitmentPhase.Accepted,
            RequestLifecycle.WaitingPickup => CommitmentPhase.WaitingPickup,
            RequestLifecycle.Onboard => CommitmentPhase.Onboard,
            _ => CommitmentPhase.None,
        };
}
