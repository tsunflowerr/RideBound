using RideBound.Domain.Commitments;
using RideBound.Domain.Common;

namespace RideBound.Domain.Tests.Commitments;

public sealed class CommitmentLedgerTests
{
    [Fact]
    public void Initial_promise_opens_version_one_without_consuming_budget()
    {
        var opened = CommitmentLedger.Empty.OpenInitial(
            "pub-1",
            CommitmentTestData.Projection(),
            1,
            new SimTime(1_000),
            "INITIAL_ACCEPTANCE",
            3);

        Assert.True(opened.IsSuccess, opened.Failure?.Message);
        Assert.Empty(CommitmentLedger.Empty.Histories);
        var current = opened.Ledger!.Histories[TestData.RequestOne].Current;
        Assert.Equal(CommitmentLedgerEntryKind.InitialPromise, current.Kind);
        Assert.Equal(1, current.PublishedPromise.Version.Value);
        Assert.Equal(CommitmentVector.Zero, current.BudgetBefore);
        Assert.Equal(CommitmentVector.Zero, current.BudgetAfter);
        Assert.Equal(CommitmentVector.Zero, current.Deltas.DecisionInduced);
    }

    [Fact]
    public void Revision_conserves_vector_and_does_not_refund_a_round_trip()
    {
        var opened = CommitmentLedger.Empty.OpenInitial(
            "pub-1",
            CommitmentTestData.Projection(),
            1,
            new SimTime(1_000),
            "INITIAL_ACCEPTANCE",
            3).Ledger!;
        var changed = CommitmentTestData.Projection(
            vehicleId: TestData.VehicleTwo,
            pickupEta: 10_004);
        var delta = new CommitmentVector(
            4,
            0,
            1,
            1,
            0,
            0,
            0,
            0,
            0,
            0);
        var first = opened.AppendRevision(
            "pub-2",
            TestData.RequestOne,
            new PromiseVersion(1),
            CommitmentTestData.Projection(),
            changed,
            new ThreeWayPromiseDelta(
                CommitmentVector.Zero,
                delta,
                delta),
            CommitmentBudgetBasis.DecisionInduced,
            2,
            new SimTime(2_000),
            "REPLAN",
            4);

        Assert.True(first.IsSuccess, first.Failure?.Message);
        var returned = CommitmentTestData.Projection();
        var returnDelta = new CommitmentVector(
            4,
            0,
            1,
            1,
            0,
            0,
            0,
            0,
            0,
            0);
        var second = first.Ledger!.AppendRevision(
            "pub-3",
            TestData.RequestOne,
            new PromiseVersion(2),
            changed,
            returned,
            new ThreeWayPromiseDelta(
                CommitmentVector.Zero,
                returnDelta,
                returnDelta),
            CommitmentBudgetBasis.DecisionInduced,
            3,
            new SimTime(3_000),
            "REPLAN",
            5);

        Assert.True(second.IsSuccess, second.Failure?.Message);
        var history = second.Ledger!.Histories[TestData.RequestOne];
        Assert.Collection(
            history.Entries,
            _ => { },
            _ => { },
            _ => { });
        Assert.Equal(8, history.Current.BudgetAfter.PickupEtaTotalMs);
        Assert.Equal(2, history.Current.BudgetAfter.VehicleSwitchCount);
        Assert.Equal(
            history.Current.BudgetBefore.Add(returnDelta).Value,
            history.Current.BudgetAfter);
        Assert.Single(opened.Histories[TestData.RequestOne].Entries);
    }

    [Fact]
    public void Stale_version_conflict_leaves_original_history_unchanged()
    {
        var ledger = CommitmentLedger.Empty.OpenInitial(
            "pub-1",
            CommitmentTestData.Projection(),
            1,
            new SimTime(1_000),
            "INITIAL_ACCEPTANCE",
            3).Ledger!;

        var result = ledger.AppendRevision(
            "pub-2",
            TestData.RequestOne,
            new PromiseVersion(2),
            CommitmentTestData.Projection(),
            CommitmentTestData.Projection(pickupEta: 10_001),
            new ThreeWayPromiseDelta(
                CommitmentVector.Zero,
                CommitmentTestData.Vector(
                    CommitmentDimension.PickupEtaTotalMs,
                    1),
                CommitmentTestData.Vector(
                    CommitmentDimension.PickupEtaTotalMs,
                    1)),
            CommitmentBudgetBasis.DecisionInduced,
            2,
            new SimTime(2_000),
            "REPLAN",
            4);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            CommitmentFailureCodes.LedgerConflict,
            result.Failure?.Code);
        Assert.Single(ledger.Histories[TestData.RequestOne].Entries);
    }

    [Fact]
    public void Publication_identifier_is_unique_across_riders()
    {
        var first = CommitmentLedger.Empty.OpenInitial(
            "pub-1",
            CommitmentTestData.Projection(),
            1,
            new SimTime(1_000),
            "INITIAL_ACCEPTANCE",
            3).Ledger!;
        var requestTwo = new RequestId("r-2");
        var pickup = new StopId("pickup-2");
        var drop = new StopId("drop-2");
        var secondProjection = new PromiseProjection(
            requestTwo,
            TestData.VehicleOne,
            pickup,
            TestData.NodeOne,
            drop,
            TestData.NodeTwo,
            new SimTime(11_000),
            new SimTime(21_000),
            [
                new PromiseServiceToken(
                    pickup,
                    requestTwo,
                    RideBound.Domain.Routes.RouteStopKind.Pickup),
                new PromiseServiceToken(
                    drop,
                    requestTwo,
                    RideBound.Domain.Routes.RouteStopKind.DropOff),
            ]);

        var duplicate = first.OpenInitial(
            "pub-1",
            secondProjection,
            2,
            new SimTime(2_000),
            "INITIAL_ACCEPTANCE",
            4);

        Assert.False(duplicate.IsSuccess);
        Assert.Equal("publicationId", duplicate.Failure?.Dimension);
        Assert.Single(first.Histories);
    }

    [Fact]
    public void Revision_rejects_unknown_budget_basis()
    {
        var ledger = CommitmentLedger.Empty.OpenInitial(
            "pub-1",
            CommitmentTestData.Projection(),
            1,
            new SimTime(1_000),
            "INITIAL_ACCEPTANCE",
            3).Ledger!;

        var result = ledger.AppendRevision(
            "pub-2",
            TestData.RequestOne,
            new PromiseVersion(1),
            CommitmentTestData.Projection(),
            CommitmentTestData.Projection(pickupEta: 10_001),
            new ThreeWayPromiseDelta(
                CommitmentVector.Zero,
                CommitmentTestData.Vector(
                    CommitmentDimension.PickupEtaTotalMs,
                    1),
                CommitmentTestData.Vector(
                    CommitmentDimension.PickupEtaTotalMs,
                    1)),
            (CommitmentBudgetBasis)99,
            2,
            new SimTime(2_000),
            "REPLAN",
            4);

        Assert.False(result.IsSuccess);
        Assert.Equal("budgetBasis", result.Failure?.Dimension);
        Assert.Single(ledger.Histories[TestData.RequestOne].Entries);
    }
}
