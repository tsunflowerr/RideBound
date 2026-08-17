using RideBound.Benchmarking.Contracts;

namespace RideBound.Benchmarking.Planning;

public sealed record BenchmarkArmDefinition(
    string ArmId,
    string PolicyId,
    string PolicyVersion,
    string PolicyConfigurationBindingId,
    byte[] CanonicalPolicyConfiguration,
    string CandidateGeneratorId,
    long CandidateWorkBudget,
    string ValidatorVersion,
    string SolverId,
    string SolverVersion,
    long SolverWorkBudget,
    byte[] CanonicalCapabilitySelection);

public sealed record BenchmarkPlanDefinition(
    string PlanId,
    EvidenceClass EvidenceClass,
    IReadOnlyList<string> ScenarioHashes,
    IReadOnlyList<BenchmarkArmDefinition> Arms,
    string PairingClassId,
    string MasterSeedHex,
    long WarmupRunCount,
    long MeasuredRepeatCount,
    string ResourceProfileId,
    string MetricRegistryHash,
    RunnerArtifactIdentity RunnerArtifact,
    string HarnessSourceSha256,
    string OracleSourceSha256);

public sealed record PlannedBenchmarkRun(
    string RunId,
    string PlanHash,
    string ScenarioHash,
    string ArmId,
    long RepeatIndex,
    long AttemptIndex,
    bool Warmup,
    long ExecutionOrdinal,
    string ArmOrderRankHex,
    BenchmarkSeedValue AdapterSeed,
    BenchmarkSeedValue SimulatorSeed,
    BenchmarkSeedValue SolverSeed,
    BenchmarkSeedValue FailureInjectionSeed);

public sealed record CompiledBenchmarkPlan(
    BenchmarkPlan Plan,
    byte[] CanonicalPlanBytes,
    string PlanHash,
    IReadOnlyList<PlannedBenchmarkRun> PlannedRuns);

public sealed record BenchmarkPlanCompilationIssue(
    string Code,
    string Path,
    string Message);

public sealed record BenchmarkPlanCompilationResult(
    CompiledBenchmarkPlan? Value,
    BenchmarkPlanCompilationIssue? Issue)
{
    public bool IsSuccess => Value is not null;

    public static BenchmarkPlanCompilationResult Success(CompiledBenchmarkPlan value) =>
        new(value, null);

    public static BenchmarkPlanCompilationResult Failed(
        string code,
        string path,
        string message) =>
        new(null, new BenchmarkPlanCompilationIssue(code, path, message));
}
