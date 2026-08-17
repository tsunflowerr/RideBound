using RideBound.Benchmarking.Contracts;
using RideBound.Benchmarking.EndToEnd;
using RideBound.Benchmarking.Planning;
using RideBound.Contracts.Protocol;

namespace RideBound.Benchmarking.Tests;

public sealed class TinyProtocolFixtureCompilerTests
{
    [Fact]
    public void Source_transcript_compiles_to_exact_executable_scenario_and_runner_fixture()
    {
        var repository = StrictBundleTestFixture.FindRepositoryRoot();
        var first = TinyProtocolFixtureCompiler.Compile(repository);
        var second = TinyProtocolFixtureCompiler.Compile(repository);

        Assert.Equal(first.CanonicalDataset, second.CanonicalDataset);
        Assert.Equal(first.CanonicalScenario, second.CanonicalScenario);
        Assert.Equal(first.ScenarioHash, second.ScenarioHash);
        Assert.Single(first.Scenario.Fleet);
        Assert.Equal(3, first.Scenario.Requests.Count);
        Assert.Equal(3, first.Scenario.ValidationSummary.NodeCount);
        Assert.Equal(6, first.Scenario.ValidationSummary.DirectedArcCount);
        Assert.Equal(2, first.Scenario.TravelSnapshots.Count);
        Assert.Equal(16, first.Scenario.Events.Count);
        Assert.Equal(
            Enumerable.Range(1, 16).Select(value => (long)value),
            first.Scenario.Events.Select(value => value.EventSequence));
        Assert.Contains(
            first.Scenario.Events,
            value => value.EventType == "passengerAlighted");
        Assert.All(first.Scenario.Events, value => Assert.True(value.SourceSequencePreserved));

        var seed = new BenchmarkSeedValue(new string('a', 64), 12345);
        var run = new PlannedBenchmarkRun(
            new string('b', 64),
            new string('c', 64),
            first.ScenarioHash,
            "c1",
            2,
            0,
            false,
            6,
            new string('d', 64),
            seed,
            seed,
            seed,
            seed);
        var arm = new BenchmarkArm(
            "c1",
            "ridebound-hard-vector",
            "wp4-boundary-v1",
            new string('1', 64),
            new string('2', 64),
            "wp4-common-generator-v1",
            10_000,
            "commitment-validator-v1",
            "google-ortools-cp-sat",
            "9.15.6755",
            100_000,
            new string('3', 64),
            "wp4-common-candidate-v1");
        var fixture = TinyProtocolFixtureCompiler.CreateRunnerFixture(
            first,
            run,
            arm,
            new string('4', 64));
        var initialize = ProtocolEnvelopeCodec.Decode(fixture.InitializeEnvelope);
        var payload = InitializeRunPayloadCodec.Decode(initialize.Envelope!.Payload);

        Assert.True(initialize.IsSuccess, initialize.Error?.Message);
        Assert.True(payload.IsSuccess, payload.Error?.Message);
        Assert.Equal(run.RunId, initialize.Envelope.RunId!.Value);
        Assert.Equal(first.Scenario.ScenarioId, initialize.Envelope.ScenarioId!.Value);
        Assert.Equal(seed.NonNegativeInt32, payload.Value!.Manifest.MasterSeed);
        Assert.Equal(arm.PolicyConfigurationSha256, payload.Value.Manifest.PolicyConfigurationHash.Value);
        Assert.Equal(first.ScenarioHash, payload.Value.Manifest.ScenarioContentHash.Value);
        Assert.Equal(new string('4', 64), payload.Value.Manifest.BinarySha256.Value);
        Assert.Equal(6, fixture.EventBatchEnvelopes.Count);
    }
}
