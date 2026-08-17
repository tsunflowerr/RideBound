using RideBound.Benchmarking.Contracts;
using RideBound.Benchmarking.Execution;
using RideBound.Benchmarking.Metrics;
using RideBound.Benchmarking.Planning;

namespace RideBound.Benchmarking.EndToEnd;

internal sealed record MechanicalPairedHarnessFixture(
    DatasetDescriptor Dataset,
    byte[] CanonicalDataset,
    ScenarioContent Scenario,
    byte[] CanonicalScenario,
    string ScenarioHash,
    string GraphSha256,
    byte[] CanonicalCapabilitySelection,
    string SourceFixturePath,
    string SourceFixtureSha256,
    IReadOnlyList<string> PinnedInputPaths,
    IReadOnlyList<MechanicalHarnessBundleSource> AdditionalBundleSources,
    Func<PlannedBenchmarkRun, BenchmarkArm, string, string, IExternalProcessConversation>
        CreateConversation);

internal sealed record MechanicalPairedHarnessProfile(
    string ProfileId,
    string PlanId,
    string CommitmentConfigurationFileName,
    IReadOnlyList<string> HarnessSourcePaths,
    string TranscriptSetDomain,
    string DecisionSetDomain,
    string BundleProducerId,
    IReadOnlyList<string> BundleClaims,
    string BundleId,
    string BundleDate,
    ExternalProcessLimits ProcessLimits,
    Action<IReadOnlyList<MetricRow>> ValidateOutcomeCoverage);
