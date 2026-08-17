using RideBound.Benchmarking.Contracts;
using RideBound.Benchmarking.Datasets;
using RideBound.Benchmarking.EndToEnd;
using RideBound.Benchmarking.Planning;
using RideBound.Contracts.Protocol;

namespace RideBound.Benchmarking.Tests;

public sealed class PublicDerivativeMechanicalFixtureCompilerTests
{
    [Fact]
    public void Medium_derivative_compiles_to_the_locked_public_identity_chain()
    {
        var repository = StrictBundleTestFixture.FindRepositoryRoot();
        var descriptor = CreateDescriptor(
            DatasetSourceRegistry.FleetPyManhattanV1.SourceArtifactSha256!);
        var descriptorBytes = BenchmarkContractCodec.Encode(descriptor);
        var first = PublicDerivativeMechanicalFixtureCompiler.Compile(
            repository,
            descriptorBytes);
        var second = PublicDerivativeMechanicalFixtureCompiler.Compile(
            repository,
            descriptorBytes);

        Assert.Equal(first.CanonicalDataset, second.CanonicalDataset);
        Assert.Equal(first.CanonicalScenario, second.CanonicalScenario);
        Assert.Equal(first.ScenarioHash, second.ScenarioHash);
        Assert.Equal(128, first.Scenario.Requests.Count);
        Assert.Equal(32, first.Scenario.Fleet.Count);
        Assert.Equal(96, first.Scenario.ValidationSummary.NodeCount);
        Assert.Equal(9_120, first.Scenario.ValidationSummary.DirectedArcCount);
        Assert.Equal(
            DatasetSourceRegistry.FleetPyManhattanV1.SourceArtifactSha256,
            first.SourceArtifactSha256);
        Assert.Equal(7, first.BundleSources.Count);
        Assert.All(first.BundleSources, source => Assert.True(File.Exists(source.FullPath)));

        var hello = ProtocolEnvelopeCodec.Decode(first.HelloEnvelope);
        var helloPayload = HelloPayloadCodec.Decode(hello.Envelope!.Payload);

        Assert.True(hello.IsSuccess, hello.Error?.Message);
        Assert.True(helloPayload.IsSuccess, helloPayload.Error?.Message);
        Assert.Contains(
            CapabilityId.ExactEventOrdering,
            helloPayload.Value!.Capabilities);
        Assert.DoesNotContain(
            CapabilityId.VehicleReassignment,
            first.CapabilitySelection.Capabilities);
    }

    [Fact]
    public void Initialize_envelope_binds_exact_run_seed_arm_and_public_source()
    {
        var repository = StrictBundleTestFixture.FindRepositoryRoot();
        var fixture = PublicDerivativeMechanicalFixtureCompiler.Compile(
            repository,
            BenchmarkContractCodec.Encode(
                CreateDescriptor(
                    DatasetSourceRegistry.FleetPyManhattanV1.SourceArtifactSha256!)));
        var seed = new BenchmarkSeedValue(new string('a', 64), 12_345);
        var run = new PlannedBenchmarkRun(
            new string('b', 64),
            new string('c', 64),
            fixture.ScenarioHash,
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
        var encoded = PublicDerivativeMechanicalFixtureCompiler.CreateInitializeEnvelope(
            fixture,
            run,
            arm,
            new string('4', 64),
            new string('5', 40));
        var envelope = ProtocolEnvelopeCodec.Decode(encoded);
        var payload = InitializeRunPayloadCodec.Decode(envelope.Envelope!.Payload);

        Assert.True(envelope.IsSuccess, envelope.Error?.Message);
        Assert.True(payload.IsSuccess, payload.Error?.Message);
        Assert.Equal(run.RunId, envelope.Envelope.RunId!.Value);
        Assert.Equal(fixture.Scenario.ScenarioId, envelope.Envelope.ScenarioId!.Value);
        Assert.Equal(seed.NonNegativeInt32, payload.Value!.Manifest.MasterSeed);
        Assert.Equal(
            arm.PolicyConfigurationSha256,
            payload.Value.Manifest.PolicyConfigurationHash.Value);
        Assert.Equal(
            fixture.SourceArtifactSha256,
            payload.Value.Manifest.Simulator.UpstreamCommitSha.Value);
        Assert.Equal(
            PublicDerivativeMechanicalFixtureCompiler.DriverSemanticsId,
            payload.Value.Manifest.Adapter.AdapterId);
    }

    [Fact]
    public void Compiler_rejects_a_descriptor_with_a_different_source_artifact()
    {
        var repository = StrictBundleTestFixture.FindRepositoryRoot();
        var bytes = BenchmarkContractCodec.Encode(CreateDescriptor(new string('f', 64)));

        var exception = Assert.Throws<InvalidDataException>(
            () => PublicDerivativeMechanicalFixtureCompiler.Compile(repository, bytes));

        Assert.Contains("identity chain diverges", exception.Message);
    }

    [Fact]
    public void Public_harness_preflight_binds_the_scenario_policy_before_runner_work()
    {
        var repository = StrictBundleTestFixture.FindRepositoryRoot();
        var fixture = PublicDerivativeMechanicalFixtureCompiler.Compile(
            repository,
            BenchmarkContractCodec.Encode(
                CreateDescriptor(
                    DatasetSourceRegistry.FleetPyManhattanV1.SourceArtifactSha256!)));
        var exact = Path.Combine(
            repository,
            "benchmarks",
            "configurations",
            "wp6-public-mechanical-commitment-v1.json");
        var wrong = Path.Combine(
            repository,
            "benchmarks",
            "configurations",
            "wp3-boundary-test-v1.json");

        PublicDerivativePairedHarness.ValidateCommitmentPolicyBinding(
            fixture.Scenario,
            exact);
        var exception = Assert.Throws<InvalidDataException>(
            () => PublicDerivativePairedHarness.ValidateCommitmentPolicyBinding(
                fixture.Scenario,
                wrong));

        Assert.Contains(
            "wp6-synthetic-policy-overlay-v1",
            exception.Message,
            StringComparison.Ordinal);
    }

    private static DatasetDescriptor CreateDescriptor(string artifactSha256)
    {
        var registration = DatasetSourceRegistry.FleetPyManhattanV1;
        return new DatasetDescriptor(
            BenchmarkContractVersions.V1,
            registration.DatasetId,
            registration.DatasetKind,
            registration.Title,
            registration.ReleaseVersion,
            registration.PersistentUri.AbsoluteUri,
            registration.DownloadUri.AbsoluteUri,
            "2026-08-12T00:00:00Z",
            registration.PublisherArtifactName,
            registration.LicenseSpdx,
            registration.LicenseUri.AbsoluteUri,
            registration.Citation,
            registration.Composition,
            registration.CollectionLimit,
            registration.AllowedUse,
            registration.ForbiddenClaim,
            registration.DirectIdentifierStatus,
            registration.LocationPrecisionClass,
            registration.RetentionClass,
            registration.MaintenanceNote,
            registration.PublisherArtifactLengthBytes,
            registration.PublisherMd5,
            artifactSha256);
    }
}
