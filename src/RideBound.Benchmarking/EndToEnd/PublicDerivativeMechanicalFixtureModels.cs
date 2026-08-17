using RideBound.Benchmarking.Contracts;
using RideBound.Contracts.Protocol;

namespace RideBound.Benchmarking.EndToEnd;

public sealed record MechanicalHarnessBundleSource(
    string FullPath,
    string RelativePath,
    string MediaType);

public sealed record PublicDerivativeMechanicalFixtureArtifacts(
    DatasetDescriptor Dataset,
    byte[] CanonicalDataset,
    ScenarioContent Scenario,
    byte[] CanonicalScenario,
    string ScenarioHash,
    string NormalizationReportHash,
    string SourceArtifactSha256,
    string SourceMemberInventorySha256,
    string GraphSha256,
    CapabilitySelection CapabilitySelection,
    byte[] CanonicalCapabilitySelection,
    byte[] HelloEnvelope,
    string SourceFixturePath,
    string SourceFixtureSha256,
    IReadOnlyList<MechanicalHarnessBundleSource> BundleSources);
