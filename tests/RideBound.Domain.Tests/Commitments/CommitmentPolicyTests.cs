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

    [Fact]
    public void Policy_has_no_deadline_by_default()
    {
        var policy = CreatePolicy(CommitmentBudgetBasis.DecisionInduced);

        Assert.Null(policy.DropEtaDeadlineSlack);
        Assert.False(policy.DropEtaDeadlineTwoSided);
    }

    [Fact]
    public void Policy_keeps_the_configured_deadline()
    {
        var policy = CreateDeadlinePolicy(new Duration(30_000), twoSided: true);

        Assert.Equal(new Duration(30_000), policy.DropEtaDeadlineSlack);
        Assert.True(policy.DropEtaDeadlineTwoSided);
    }

    [Fact]
    public void Policy_rejects_a_two_sided_deadline_without_a_slack()
    {
        var error = Assert.Throws<ArgumentException>(
            () => CreateDeadlinePolicy(null, twoSided: true));

        Assert.Equal("dropEtaDeadlineTwoSided", error.ParamName);
    }

    private static CommitmentPolicy CreateDeadlinePolicy(
        Duration? slack,
        bool twoSided) =>
        new(
            "validated-policy",
            CommitmentBudgetBasis.DecisionInduced,
            CommitmentDimensionVocabulary.Ordered.Select(
                dimension => new CommitmentDimensionLimit(
                    dimension,
                    null,
                    CommitmentPhase.AllActive)),
            new MaterialRevisionRule(1, null),
            dropEtaDeadlineSlack: slack,
            dropEtaDeadlineTwoSided: twoSided);

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
