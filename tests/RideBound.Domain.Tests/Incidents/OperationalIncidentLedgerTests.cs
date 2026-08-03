using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Incidents;
using RideBound.Domain.Tests.Commitments;

namespace RideBound.Domain.Tests.Incidents;

public sealed class OperationalIncidentLedgerTests
{
    [Fact]
    public void Resolve_preserves_incident_and_breach_history_without_refund()
    {
        var incidentId = new IncidentId("incident-1");
        var opened = OperationalIncidentLedger.Empty.Open(
            incidentId,
            "ROAD_CLOSED",
            [TestData.VehicleOne],
            [TestData.RequestOne],
            10,
            new SimTime(2_000));
        Assert.True(opened.IsSuccess, opened.Failure?.Message);
        var breach = Breach(incidentId);
        var appended = opened.Ledger!.AppendBreach(breach);
        Assert.True(appended.IsSuccess, appended.Failure?.Message);

        var resolved = appended.Ledger!.Resolve(
            incidentId,
            12,
            new SimTime(3_000));

        Assert.True(resolved.IsSuccess, resolved.Failure?.Message);
        Assert.False(resolved.Ledger!.Incidents[incidentId].IsOpen);
        Assert.Same(breach, Assert.Single(resolved.Ledger.Breaches));
        Assert.Equal(7, breach.BudgetBefore.PickupEtaTotalMs);
        Assert.Equal(11, breach.AttemptedBudgetAfter.PickupEtaTotalMs);
        Assert.False(breach.NormalOperation);
    }

    [Fact]
    public void Duplicate_unknown_and_stale_transitions_are_rejected()
    {
        var id = new IncidentId("incident-1");
        var opened = OperationalIncidentLedger.Empty.Open(
            id,
            "ROAD_CLOSED",
            [TestData.VehicleOne],
            [],
            10,
            new SimTime(2_000)).Ledger!;

        Assert.Equal(
            IncidentFailureCodes.DuplicateIncident,
            opened.Open(
                id,
                "ROAD_CLOSED",
                [TestData.VehicleOne],
                [],
                11,
                new SimTime(2_001)).Failure?.Code);
        Assert.Equal(
            IncidentFailureCodes.UnknownIncident,
            opened.Resolve(
                new IncidentId("unknown"),
                11,
                new SimTime(2_001)).Failure?.Code);
        var resolved = opened.Resolve(id, 11, new SimTime(2_001)).Ledger!;
        Assert.Equal(
            IncidentFailureCodes.StaleIncidentTransition,
            resolved.Resolve(id, 12, new SimTime(2_002)).Failure?.Code);
    }

    [Fact]
    public void Breach_requires_open_incident_and_affected_rider()
    {
        var id = new IncidentId("incident-1");
        var opened = OperationalIncidentLedger.Empty.Open(
            id,
            "ROAD_CLOSED",
            [TestData.VehicleOne],
            [],
            10,
            new SimTime(2_000)).Ledger!;

        Assert.Equal(
            IncidentFailureCodes.RiderNotAffected,
            opened.AppendBreach(Breach(id)).Failure?.Code);
    }

    [Fact]
    public void Breach_budget_and_affected_vehicle_are_cross_checked()
    {
        var id = new IncidentId("incident-1");
        var opened = OperationalIncidentLedger.Empty.Open(
            id,
            "ROAD_CLOSED",
            [TestData.VehicleOne],
            [TestData.RequestOne],
            10,
            new SimTime(2_000)).Ledger!;

        Assert.Throws<ArgumentException>(
            () => Breach(id, attemptedPickupEta: 12));
        Assert.Equal(
            IncidentFailureCodes.BreachIncidentMismatch,
            opened.AppendBreach(
                Breach(id, vehicleId: TestData.VehicleTwo)).Failure?.Code);
    }

    private static CommitmentBreachRecord Breach(
        IncidentId incidentId,
        long attemptedPickupEta = 11,
        VehicleId? vehicleId = null)
    {
        var projection = CommitmentTestData.Projection(vehicleId);
        var previous = new PublishedPromise(
            new PromiseVersion(1),
            1,
            new SimTime(1_000),
            projection);
        var delta = CommitmentTestData.Vector(
            CommitmentDimension.PickupEtaTotalMs,
            4);

        return new CommitmentBreachRecord(
            "breach-1",
            incidentId,
            TestData.RequestOne,
            previous,
            projection,
            projection,
            new ThreeWayPromiseDelta(CommitmentVector.Zero, delta, delta),
            CommitmentTestData.Vector(
                CommitmentDimension.PickupEtaTotalMs,
                7),
            CommitmentTestData.Vector(
                CommitmentDimension.PickupEtaTotalMs,
                attemptedPickupEta),
            ["pickup_eta_total_ms"],
            11,
            2,
            new SimTime(2_000));
    }
}
