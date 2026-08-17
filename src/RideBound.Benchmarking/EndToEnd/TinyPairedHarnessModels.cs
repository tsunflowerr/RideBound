using RideBound.Benchmarking.Contracts;
using RideBound.Benchmarking.Execution;

namespace RideBound.Benchmarking.EndToEnd;

public sealed record TinyProtocolFixtureArtifacts(
    DatasetDescriptor Dataset,
    byte[] CanonicalDataset,
    ScenarioContent Scenario,
    byte[] CanonicalScenario,
    string ScenarioHash,
    string GraphSha256,
    byte[] CanonicalCapabilitySelection,
    byte[] HelloEnvelope,
    byte[] InitializeTemplate,
    IReadOnlyList<byte[]> EventBatchTemplates,
    string SourceFixturePath,
    string SourceFixtureSha256);

public sealed record TinyHarnessPaths(
    string RepositoryRoot,
    string WorkRoot,
    string BundleDirectory,
    string ReceiptPath,
    string BuildConfiguration);

public sealed record TinyRunIdentityReceipt(
    string RunId,
    string ArmId,
    long RepeatIndex,
    long ExecutionOrdinal,
    string InputSha256,
    string OutputSha256,
    string ObservationIndexSha256,
    string DecisionSequenceSha256,
    string SemanticMetricRowsSha256,
    string FullMetricRowsSha256);

public sealed record TinyPairedHarnessReceipt(
    string SchemaVersion,
    string EvidenceClass,
    string ClaimProfileId,
    string PlanHash,
    string ScenarioHash,
    string SourceFixtureSha256,
    string RuntimeInventorySha256,
    string SourceInventorySha256,
    string RunGridSha256,
    string TranscriptSetSha256,
    string DecisionSetSha256,
    string SemanticMetricSetSha256,
    string FullMetricSetHash,
    long PlannedRunCount,
    long SucceededRunCount,
    long FailedRunCount,
    long ExcludedRunCount,
    string BundleHash,
    string LogicalManifestSha256,
    string ExternalVerificationReportSha256,
    string BundleDirectory,
    IReadOnlyList<TinyRunIdentityReceipt> Runs);

internal sealed record OracleExecutionSummary(
    string SchemaVersion,
    string MetricSetHash,
    string OracleAssemblySha256,
    string ResourceEvidenceSha256,
    long RowCount,
    string SemanticEvidenceSha256);

internal sealed record ExternalProcessExecutionResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);
