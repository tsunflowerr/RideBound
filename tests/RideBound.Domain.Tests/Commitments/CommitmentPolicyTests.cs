using RideBound.Domain.Commitments;
using RideBound.Domain.Common;

namespace RideBound.Domain.Tests.Commitments;

public sealed class CommitmentPolicyTests
{
    [Fact]
    public void Dimension_limit_rejects_unknown_dimension()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CommitmentDimensionLimit(
                (CommitmentDimension)99,
                null,
                CommitmentPhase.AllActive));
    }

    [Fact]
    public void Policy_rejects_unknown_budget_basis()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreatePolicy((CommitmentBudgetBasis)99));
    }

    [Fact]
    public void Policy_rejects_unknown_lock_bits()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreatePolicy(
                CommitmentBudgetBasis.DecisionInduced,
                new Duration(1_000),
                (PromiseLock)64));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CreatePolicy(
                CommitmentBudgetBasis.DecisionInduced,
                finalLocks: (PromiseLock)64));
    }

    private static CommitmentPolicy CreatePolicy(
        CommitmentBudgetBasis budgetBasis,
        Duration? freezeHorizon = null,
        PromiseLock freezeLocks = PromiseLock.None,
        PromiseLock finalLocks = PromiseLock.None) =>
        new(
            "validated-policy",
            budgetBasis,
            CommitmentDimensionVocabulary.Ordered.Select(
                dimension => new CommitmentDimensionLimit(
                    dimension,
                    null,
                    CommitmentPhase.AllActive)),
            new MaterialRevisionRule(1, null),
            freezeHorizon,
            freezeLocks,
            finalLocks);
}
