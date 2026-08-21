using System.Numerics;
using RideBound.Contracts.Protocol;

namespace RideBound.Benchmarking.Contracts;

public sealed record ExperimentalUnitIdentity(
    string ScenarioHash,
    string DemandRealizationHash,
    string TravelRealizationHash,
    string UnitId) : IBenchmarkDocument
{
    public const string CurrentSchemaVersion = "1.0.0";

    public string SchemaVersion => CurrentSchemaVersion;

    public static ExperimentalUnitIdentity Create(
        string scenarioHash,
        string demandRealizationHash,
        string travelRealizationHash)
    {
        var unitId = BenchmarkIdentity.CalculateExperimentalUnit(
            scenarioHash,
            demandRealizationHash,
            travelRealizationHash);

        return new ExperimentalUnitIdentity(
            scenarioHash,
            demandRealizationHash,
            travelRealizationHash,
            unitId);
    }
}

/// <summary>
/// Exact terminal observation for one arm on one experimental unit. Rates are
/// deliberately not stored as binary floating point; a consumer derives them
/// from the integer numerator and denominator.
/// </summary>
public sealed record RunLevelObservation(
    string RunId,
    string UnitId,
    string ArmId,
    string PolicyId,
    long ArrivedRiderCount,
    long AcceptedRiderCount,
    long RejectedRiderCount,
    long CompletedRiderCount,
    long PickupEtaDecisionDeltaSumMs,
    long DropEtaDecisionDeltaSumMs,
    long TotalDecisionInducedBurdenMs,
    long TotalExogenousBurdenMs,
    long TotalVisibleBurdenMs,
    long MaterialRevisionCount,
    long PrePickupInsertedStopCount,
    long DisruptiveRevisionFrameCount)
{
    public static RunLevelObservation Create(
        string runId,
        string unitId,
        string armId,
        string policyId,
        long arrivedRiderCount,
        long acceptedRiderCount,
        long rejectedRiderCount,
        long completedRiderCount,
        long pickupEtaDecisionDeltaSumMs,
        long dropEtaDecisionDeltaSumMs,
        long totalDecisionInducedBurdenMs,
        long totalExogenousBurdenMs,
        long totalVisibleBurdenMs,
        long materialRevisionCount,
        long prePickupInsertedStopCount,
        long disruptiveRevisionFrameCount)
    {
        RequireText(runId, nameof(runId));
        RequireText(unitId, nameof(unitId));
        RequireText(armId, nameof(armId));
        RequireText(policyId, nameof(policyId));

        var values = new[]
        {
            arrivedRiderCount,
            acceptedRiderCount,
            rejectedRiderCount,
            completedRiderCount,
            pickupEtaDecisionDeltaSumMs,
            dropEtaDecisionDeltaSumMs,
            totalDecisionInducedBurdenMs,
            totalExogenousBurdenMs,
            totalVisibleBurdenMs,
            materialRevisionCount,
            prePickupInsertedStopCount,
            disruptiveRevisionFrameCount,
        };

        if (values.Any(value => value is < 0 or > ProtocolLimits.MaxCanonicalInteger))
        {
            throw new ArgumentOutOfRangeException(
                nameof(arrivedRiderCount),
                "Observation counters must be non-negative canonical integers.");
        }

        if (new BigInteger(acceptedRiderCount) + rejectedRiderCount
                != arrivedRiderCount
            || completedRiderCount > acceptedRiderCount)
        {
            throw new ArgumentException(
                "Accepted and rejected riders must partition arrivals, and completed riders must be accepted.");
        }

        if (new BigInteger(pickupEtaDecisionDeltaSumMs)
                + dropEtaDecisionDeltaSumMs
                != totalDecisionInducedBurdenMs)
        {
            throw new ArgumentException(
                "Decision-induced burden must equal its exact pickup/drop component sum.");
        }

        return new RunLevelObservation(
            runId,
            unitId,
            armId,
            policyId,
            arrivedRiderCount,
            acceptedRiderCount,
            rejectedRiderCount,
            completedRiderCount,
            pickupEtaDecisionDeltaSumMs,
            dropEtaDecisionDeltaSumMs,
            totalDecisionInducedBurdenMs,
            totalExogenousBurdenMs,
            totalVisibleBurdenMs,
            materialRevisionCount,
            prePickupInsertedStopCount,
            disruptiveRevisionFrameCount);
    }

    private static void RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be empty.", parameterName);
        }
    }
}

/// <summary>
/// Predeclares orientation as well as arm and policy identities. A pairing API
/// that accepts only two observations cannot distinguish a valid pair from a
/// silently swapped baseline/treatment pair.
/// </summary>
public sealed record PairedComparisonDesign(
    string BaselineArmId,
    string BaselinePolicyId,
    string TreatmentArmId,
    string TreatmentPolicyId)
{
    public static PairedComparisonDesign Create(
        string baselineArmId,
        string baselinePolicyId,
        string treatmentArmId,
        string treatmentPolicyId)
    {
        foreach (var (value, name) in new[]
                 {
                     (baselineArmId, nameof(baselineArmId)),
                     (baselinePolicyId, nameof(baselinePolicyId)),
                     (treatmentArmId, nameof(treatmentArmId)),
                     (treatmentPolicyId, nameof(treatmentPolicyId)),
                 })
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Value cannot be empty.", name);
            }
        }

        if (string.Equals(baselineArmId, treatmentArmId, StringComparison.Ordinal)
            || string.Equals(
                baselinePolicyId,
                treatmentPolicyId,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Baseline and treatment must have distinct arm and policy identities.");
        }

        return new PairedComparisonDesign(
            baselineArmId,
            baselinePolicyId,
            treatmentArmId,
            treatmentPolicyId);
    }
}

public sealed record PairedExperimentalUnitComparison(
    string UnitId,
    RunLevelObservation BaselineObservation,
    RunLevelObservation TreatmentObservation,
    long DeltaDecisionInducedBurdenMs,
    long DeltaCompletedRiderCount,
    long CompletionRateSharedDenominator,
    long DeltaMaterialRevisionCount,
    long DeltaDisruptiveRevisionFrameCount)
{
    public static PairedExperimentalUnitComparison Create(
        PairedComparisonDesign design,
        RunLevelObservation baseline,
        RunLevelObservation treatment)
    {
        ArgumentNullException.ThrowIfNull(design);
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(treatment);

        if (baseline.ArmId != design.BaselineArmId
            || baseline.PolicyId != design.BaselinePolicyId
            || treatment.ArmId != design.TreatmentArmId
            || treatment.PolicyId != design.TreatmentPolicyId)
        {
            throw new InvalidOperationException(
                "Observation orientation does not match the predeclared paired design.");
        }

        if (string.Equals(baseline.RunId, treatment.RunId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("A run cannot be paired with itself.");
        }

        if (!string.Equals(baseline.UnitId, treatment.UnitId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Cannot pair observations from different experimental units: '{baseline.UnitId}' vs '{treatment.UnitId}'.");
        }

        if (baseline.ArrivedRiderCount != treatment.ArrivedRiderCount)
        {
            throw new InvalidOperationException(
                "Paired observations must contain the same demand realization and arrival count.");
        }

        return new PairedExperimentalUnitComparison(
            baseline.UnitId,
            baseline,
            treatment,
            checked(
                treatment.TotalDecisionInducedBurdenMs
                - baseline.TotalDecisionInducedBurdenMs),
            checked(treatment.CompletedRiderCount - baseline.CompletedRiderCount),
            baseline.ArrivedRiderCount,
            checked(treatment.MaterialRevisionCount - baseline.MaterialRevisionCount),
            checked(
                treatment.DisruptiveRevisionFrameCount
                - baseline.DisruptiveRevisionFrameCount));
    }
}
