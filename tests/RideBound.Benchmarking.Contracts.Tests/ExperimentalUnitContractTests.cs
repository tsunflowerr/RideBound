using System.Text;
using RideBound.Benchmarking.Contracts;

namespace RideBound.Benchmarking.Contracts.Tests;

public sealed class ExperimentalUnitContractTests
{
    private static readonly PairedComparisonDesign Design =
        PairedComparisonDesign.Create(
            "arm-b1",
            "rolling-cost",
            "arm-c1",
            "ridebound-hard-vector");

    [Fact]
    public void Experimental_unit_identity_is_deterministic_and_demand_bound()
    {
        var first = ExperimentalUnitIdentity.Create(Hash('a'), Hash('b'), Hash('c'));
        var replay = ExperimentalUnitIdentity.Create(Hash('a'), Hash('b'), Hash('c'));
        var differentDemand = ExperimentalUnitIdentity.Create(
            Hash('a'),
            Hash('d'),
            Hash('c'));

        Assert.Equal(first.UnitId, replay.UnitId);
        Assert.NotEqual(first.UnitId, differentDemand.UnitId);
        Assert.Matches("^[0-9a-f]{64}$", first.UnitId);
    }

    [Fact]
    public void Experimental_unit_codec_names_the_demand_realization_not_a_solver_seed()
    {
        var original = ExperimentalUnitIdentity.Create(Hash('a'), Hash('b'), Hash('c'));
        var encoded = BenchmarkContractCodec.Encode(original);
        var text = Encoding.UTF8.GetString(encoded);
        var decoded = BenchmarkContractCodec.Decode<ExperimentalUnitIdentity>(encoded);

        Assert.Contains("\"demandRealizationHash\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("masterSeed", text, StringComparison.Ordinal);
        Assert.True(decoded.IsSuccess, decoded.Error?.Message);
        Assert.Equal(original, decoded.Value);
    }

    [Fact]
    public void Experimental_unit_codec_rejects_a_tampered_identity()
    {
        var tampered = new ExperimentalUnitIdentity(
            Hash('a'),
            Hash('b'),
            Hash('c'),
            Hash('f'));

        Assert.Throws<ArgumentException>(() => BenchmarkContractCodec.Encode(tampered));
    }

    [Fact]
    public void Observation_keeps_exact_counts_and_component_sums()
    {
        var observation = Observation(
            "run-b1",
            "unit-1",
            "arm-b1",
            "rolling-cost",
            accepted: 107,
            rejected: 21,
            pickup: 599_604,
            drop: 2_423_414,
            material: 33,
            disruptiveFrames: 30);

        Assert.Equal(128, observation.ArrivedRiderCount);
        Assert.Equal(107, observation.CompletedRiderCount);
        Assert.Equal(3_023_018, observation.TotalDecisionInducedBurdenMs);
        Assert.Equal(128, observation.AcceptedRiderCount + observation.RejectedRiderCount);
    }

    [Fact]
    public void Observation_does_not_assume_visible_equals_exogenous_plus_decision()
    {
        var observation = RunLevelObservation.Create(
            "run",
            "unit",
            "arm",
            "policy",
            arrivedRiderCount: 1,
            acceptedRiderCount: 1,
            rejectedRiderCount: 0,
            completedRiderCount: 1,
            pickupEtaDecisionDeltaSumMs: 30,
            dropEtaDecisionDeltaSumMs: 20,
            totalDecisionInducedBurdenMs: 50,
            totalExogenousBurdenMs: 40,
            totalVisibleBurdenMs: 10,
            materialRevisionCount: 0,
            prePickupInsertedStopCount: 0,
            disruptiveRevisionFrameCount: 1);

        Assert.Equal(10, observation.TotalVisibleBurdenMs);
        Assert.NotEqual(
            observation.TotalVisibleBurdenMs,
            observation.TotalDecisionInducedBurdenMs
            + observation.TotalExogenousBurdenMs);
    }

    [Theory]
    [InlineData(100, 80, 19, 80)]
    [InlineData(100, 80, 20, 81)]
    public void Observation_rejects_invalid_terminal_lifecycle_counts(
        long arrived,
        long accepted,
        long rejected,
        long completed)
    {
        Assert.Throws<ArgumentException>(() => RunLevelObservation.Create(
            "run",
            "unit",
            "arm",
            "policy",
            arrived,
            accepted,
            rejected,
            completed,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0));
    }

    [Fact]
    public void Pairing_calculates_integer_deltas_with_a_shared_denominator()
    {
        var baseline = Observation(
            "run-b1",
            "unit-1",
            "arm-b1",
            "rolling-cost",
            accepted: 107,
            rejected: 21,
            pickup: 599_604,
            drop: 2_423_414,
            material: 33,
            disruptiveFrames: 30);
        var treatment = Observation(
            "run-c1",
            "unit-1",
            "arm-c1",
            "ridebound-hard-vector",
            accepted: 99,
            rejected: 29,
            pickup: 0,
            drop: 758_435,
            material: 10,
            disruptiveFrames: 10);

        var comparison = PairedExperimentalUnitComparison.Create(
            Design,
            baseline,
            treatment);

        Assert.Equal(-2_264_583, comparison.DeltaDecisionInducedBurdenMs);
        Assert.Equal(-8, comparison.DeltaCompletedRiderCount);
        Assert.Equal(128, comparison.CompletionRateSharedDenominator);
        Assert.Equal(-23, comparison.DeltaMaterialRevisionCount);
        Assert.Equal(-20, comparison.DeltaDisruptiveRevisionFrameCount);
    }

    [Fact]
    public void Pairing_rejects_swapped_arms_even_when_the_unit_matches()
    {
        var baseline = Observation(
            "run-b1",
            "unit-1",
            "arm-b1",
            "rolling-cost",
            107,
            21);
        var treatment = Observation(
            "run-c1",
            "unit-1",
            "arm-c1",
            "ridebound-hard-vector",
            99,
            29);

        Assert.Throws<InvalidOperationException>(() =>
            PairedExperimentalUnitComparison.Create(Design, treatment, baseline));
    }

    [Fact]
    public void Pairing_rejects_different_units_arrivals_and_duplicate_run_ids()
    {
        var baseline = Observation(
            "run-shared",
            "unit-1",
            "arm-b1",
            "rolling-cost",
            107,
            21);

        Assert.Throws<InvalidOperationException>(() =>
            PairedExperimentalUnitComparison.Create(
                Design,
                baseline,
                Observation(
                    "run-c1",
                    "unit-2",
                    "arm-c1",
                    "ridebound-hard-vector",
                    99,
                    29)));
        Assert.Throws<InvalidOperationException>(() =>
            PairedExperimentalUnitComparison.Create(
                Design,
                baseline,
                Observation(
                    "run-c1",
                    "unit-1",
                    "arm-c1",
                    "ridebound-hard-vector",
                    98,
                    29,
                    arrived: 127)));
        Assert.Throws<InvalidOperationException>(() =>
            PairedExperimentalUnitComparison.Create(
                Design,
                baseline,
                Observation(
                    "run-shared",
                    "unit-1",
                    "arm-c1",
                    "ridebound-hard-vector",
                    99,
                    29)));
    }

    [Fact]
    public void Design_rejects_identical_or_ambiguous_arm_contracts()
    {
        Assert.Throws<ArgumentException>(() => PairedComparisonDesign.Create(
            "arm",
            "policy-b",
            "arm",
            "policy-c"));
        Assert.Throws<ArgumentException>(() => PairedComparisonDesign.Create(
            "arm-b",
            "policy",
            "arm-c",
            "policy"));
    }

    private static RunLevelObservation Observation(
        string runId,
        string unitId,
        string armId,
        string policyId,
        long accepted,
        long rejected,
        long arrived = 128,
        long pickup = 0,
        long drop = 0,
        long material = 0,
        long disruptiveFrames = 0)
    {
        var burden = checked(pickup + drop);
        const long exogenous = 500_000;
        return RunLevelObservation.Create(
            runId,
            unitId,
            armId,
            policyId,
            arrived,
            accepted,
            rejected,
            accepted,
            pickup,
            drop,
            burden,
            exogenous,
            checked(burden + exogenous),
            material,
            prePickupInsertedStopCount: 0,
            disruptiveRevisionFrameCount: disruptiveFrames);
    }

    private static string Hash(char value) => new(value, 64);
}
