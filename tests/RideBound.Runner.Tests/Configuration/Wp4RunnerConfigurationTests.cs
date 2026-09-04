using System.Reflection;
using System.Text;
using RideBound.Algorithms.Candidates;
using RideBound.Algorithms.Policies;
using RideBound.Application.Commitments;
using RideBound.Domain.Commitments;
using RideBound.Runner.Configuration;
using RideBound.Solvers.OrTools;

namespace RideBound.Runner.Tests.Configuration;

public sealed class Wp4RunnerConfigurationTests
{
    [Theory]
    [InlineData("wp7-fleetpy-rolling-cost-v1.json", "rolling-cost")]
    [InlineData(
        "wp7-fleetpy-ridebound-hard-vector-v1.json",
        "ridebound-hard-vector")]
    public void Wp7_fleetpy_configs_explicitly_bind_the_portfolio_strategy(
        string fileName,
        string policyId)
    {
        var commitment = CommitmentConfiguration();
        var configuration = Wp4RunnerConfiguration.Decode(
            File.ReadAllBytes(Path.Combine(
                RepositoryRoot(),
                "benchmarks",
                "configurations",
                fileName)),
            commitment);

        Assert.Equal(policyId, configuration.PolicyId);
        Assert.Equal("wp7-fleetpy-v1", configuration.PolicyVersion);
        Assert.Equal(
            InitialPromiseTrigger.BookingConfirmation,
            configuration.InitialPromiseTrigger);
        Assert.Equal(
            CandidateRetentionStrategy.ServiceSetStabilityPortfolioV1,
            configuration.CandidateGeneration.RetentionStrategy);
        Assert.NotEqual(
            configuration.ContentHash,
            configuration.BindToCommitmentConfiguration(
                commitment.ContentHash));
    }

    [Theory]
    [InlineData("wp9-fleetpy-rolling-cost-audited-v1.json", "rolling-cost")]
    [InlineData(
        "wp9-fleetpy-ridebound-hard-vector-audited-v1.json",
        "ridebound-hard-vector")]
    public void Wp9_primary_configs_require_audited_solver_evidence(
        string fileName,
        string policyId)
    {
        var configuration = Wp4RunnerConfiguration.Decode(
            File.ReadAllBytes(Path.Combine(
                RepositoryRoot(),
                "benchmarks",
                "configurations",
                fileName)),
            CommitmentConfiguration());

        Assert.Equal(policyId, configuration.PolicyId);
        Assert.Equal("wp9-confirmatory-audited-v1", configuration.PolicyVersion);
        Assert.True(configuration.EmitSolverExecutionEvidence);
        Assert.Equal(
            CandidateRetentionStrategy.ServiceSetStabilityPortfolioV1,
            configuration.CandidateGeneration.RetentionStrategy);
    }

    [Fact]
    public void Wp9_exploratory_c2_config_binds_warning_to_finite_hard_limit()
    {
        var repository = RepositoryRoot();
        var commitment = CommitmentPolicyConfiguration.Decode(
            File.ReadAllBytes(Path.Combine(
                repository,
                "benchmarks",
                "configurations",
                "wp8-drop-eta-budget-loose-v1.json")));
        var configuration = Wp4RunnerConfiguration.Decode(
            File.ReadAllBytes(Path.Combine(
                repository,
                "benchmarks",
                "configurations",
                "wp9-fleetpy-soft-hard-hybrid-audited-v1.json")),
            commitment);

        Assert.Equal(
            RidePoolingPolicyRegistry.CommitSoftHardHybrid,
            configuration.PolicyId);
        Assert.True(configuration.EmitSolverExecutionEvidence);
        Assert.True(configuration.TryGetProfile(
            "wp6-synthetic-policy-overlay-v1",
            out var profile));
        Assert.Equal(
            60_000,
            profile.Limits[CommitmentDimension.DropEtaTotalMs].WarningLimit);
    }

    [Fact]
    public void Published_configuration_is_strict_and_binds_both_configuration_hashes()
    {
        var commitment = CommitmentConfiguration();
        var bytes = File.ReadAllBytes(Path.Combine(
            RepositoryRoot(),
            "benchmarks",
            "configurations",
            "wp4-rolling-cost-boundary-v1.json"));

        var first = Wp4RunnerConfiguration.Decode(bytes, commitment);
        var second = Wp4RunnerConfiguration.Decode(bytes, commitment);
        var binding = first.BindToCommitmentConfiguration(
            commitment.ContentHash);

        Assert.Equal(RidePoolingPolicyRegistry.RollingCost, first.PolicyId);
        Assert.Equal("wp4-boundary-v1", first.PolicyVersion);
        Assert.Equal(
            OrToolsCandidateSelectionSolver.AdapterVersion,
            "google-ortools-9.15.6755");
        Assert.NotNull(first.SolverPolicyOptions);
        Assert.Null(first.MultiplePlanOptions);
        Assert.Equal(
            CandidateRetentionStrategy.LegacyAcceptedCountCostSlack,
            first.CandidateGeneration.RetentionStrategy);
        Assert.Equal(
            InitialPromiseTrigger.InitialAcceptance,
            first.InitialPromiseTrigger);
        Assert.False(first.EmitSolverExecutionEvidence);
        Assert.Equal(first.ContentHash, second.ContentHash);
        Assert.NotEqual(first.ContentHash, binding);
        Assert.NotEqual(commitment.ContentHash, binding);
        Assert.Equal(64, binding.Value.Length);
    }

    [Fact]
    public void Solver_execution_evidence_is_explicit_opt_in_and_hash_bound()
    {
        var commitment = CommitmentConfiguration();
        var legacy = Wp4RunnerConfiguration.Decode(
            Encoding.UTF8.GetBytes(PublishedJson()),
            commitment);
        var optedInJson = PublishedJson().Replace(
            "\"policyVersion\": \"wp4-boundary-v1\"",
            "\"policyVersion\": \"wp4-boundary-v1\",\n  " +
            "\"emitSolverExecutionEvidence\": true",
            StringComparison.Ordinal);
        var optedIn = Wp4RunnerConfiguration.Decode(
            Encoding.UTF8.GetBytes(optedInJson),
            commitment);

        Assert.False(legacy.EmitSolverExecutionEvidence);
        Assert.True(optedIn.EmitSolverExecutionEvidence);
        Assert.NotEqual(legacy.ContentHash, optedIn.ContentHash);

        var invalid = optedInJson.Replace(
            "\"emitSolverExecutionEvidence\": true",
            "\"emitSolverExecutionEvidence\": 1",
            StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(
            () => Wp4RunnerConfiguration.Decode(
                Encoding.UTF8.GetBytes(invalid),
                commitment));
    }

    [Fact]
    public void Retained_portfolio_profile_is_strict_solver_only_and_seed_preserving()
    {
        var commitment = CommitmentConfiguration();
        var baseJson = PublishedJson();
        var optedInJson = baseJson.Replace(
            "\"policyVersion\": \"wp4-boundary-v1\"",
            "\"policyVersion\": \"wp4-boundary-v1\",\n  " +
            "\"emitSolverExecutionEvidence\": true,\n  " +
            "\"solverExecutionEvidenceProfile\": " +
            "\"retained-portfolio-v1\"",
            StringComparison.Ordinal);

        var configuration = Wp4RunnerConfiguration.Decode(
            Encoding.UTF8.GetBytes(optedInJson),
            commitment);

        Assert.Equal(
            Wp4RunnerConfiguration.RetainedPortfolioEvidenceProfile,
            configuration.SolverExecutionEvidenceProfile);
        Assert.True(configuration.EmitSolverExecutionEvidence);
        Assert.True(
            configuration.SolverPolicyOptions!
                .CaptureCandidatePortfolioEvidence);
        var seeded = configuration.CreateSolverPolicyOptionsForRun(19);
        Assert.True(seeded.CaptureCandidatePortfolioEvidence);
        Assert.Equal(19, seeded.ExecutionBudget.SolverBudget.RandomSeed);
        Assert.NotEqual(
            Wp4RunnerConfiguration.Decode(
                Encoding.UTF8.GetBytes(baseJson),
                commitment).ContentHash,
            configuration.ContentHash);

        var noBaseEvidence = optedInJson.Replace(
            "\"emitSolverExecutionEvidence\": true,\n  ",
            string.Empty,
            StringComparison.Ordinal);
        Assert.Contains(
            "emitSolverExecutionEvidence=true",
            Assert.Throws<InvalidDataException>(
                () => Wp4RunnerConfiguration.Decode(
                    Encoding.UTF8.GetBytes(noBaseEvidence),
                    commitment)).Message);

        var unknown = optedInJson.Replace(
            "retained-portfolio-v1",
            "unknown-portfolio",
            StringComparison.Ordinal);
        Assert.Contains(
            "Unknown solverExecutionEvidenceProfile",
            Assert.Throws<InvalidDataException>(
                () => Wp4RunnerConfiguration.Decode(
                    Encoding.UTF8.GetBytes(unknown),
                    commitment)).Message);

        const string nonSolver =
            """
            {
              "configurationVersion":"1.0.0",
              "policyId":"least-commitment-consensus",
              "policyVersion":"wp13-evidence-negative-v1",
              "candidateGeneration":{
                "maximumCandidatesPerVehicle":10,
                "maximumNewRequestsPerVehicle":1,
                "exactSmallMode":false,
                "scheduleStrategy":"earliest-feasible",
                "maximumExplorationWorkUnits":100
              },
              "multiplePlan":{
                "maximumPlanCount":2,
                "maximumCombinationWorkUnits":100,
                "requireCompleteEnumeration":true
              },
              "emitSolverExecutionEvidence":true,
              "solverExecutionEvidenceProfile":"retained-portfolio-v1"
            }
            """;
        Assert.Contains(
            "solver-backed execution",
            Assert.Throws<InvalidDataException>(
                () => Wp4RunnerConfiguration.Decode(
                    Encoding.UTF8.GetBytes(nonSolver),
                    commitment)).Message);
    }

    [Fact]
    public void Full_witness_collection_needs_its_own_profile()
    {
        var commitment = CommitmentConfiguration();
        var baseJson = PublishedJson();

        // Off by default, so the validator keeps its fail-fast path and its cost.
        Assert.False(
            Wp4RunnerConfiguration.Decode(
                Encoding.UTF8.GetBytes(baseJson),
                commitment).CollectAllCommitmentWitnesses);

        // The frozen E1 configurations declare retained-portfolio-v1. Widening
        // that profile in place would change their recorded witnesses and so
        // their decision hashes, so it must never collect the full set.
        var retained = Wp4RunnerConfiguration.Decode(
            Encoding.UTF8.GetBytes(
                WithProfile(
                    baseJson,
                    Wp4RunnerConfiguration.RetainedPortfolioEvidenceProfile)),
            commitment);
        Assert.False(retained.CollectAllCommitmentWitnesses);
        Assert.True(
            retained.SolverPolicyOptions!.CaptureCandidatePortfolioEvidence);

        var fullWitness = Wp4RunnerConfiguration.Decode(
            Encoding.UTF8.GetBytes(
                WithProfile(
                    baseJson,
                    Wp4RunnerConfiguration
                        .RetainedPortfolioFullWitnessEvidenceProfile)),
            commitment);
        Assert.True(fullWitness.CollectAllCommitmentWitnesses);
        Assert.True(
            fullWitness.SolverPolicyOptions!.CaptureCandidatePortfolioEvidence);
        Assert.NotEqual(retained.ContentHash, fullWitness.ContentHash);

        Assert.Throws<InvalidDataException>(
            () => Wp4RunnerConfiguration.Decode(
                Encoding.UTF8.GetBytes(
                    WithProfile(baseJson, "retained-portfolio-v9")),
                commitment));
    }

    [Fact]
    public void Frozen_e1_configurations_do_not_collect_the_full_witness_set()
    {
        var commitment = CommitmentConfiguration();

        foreach (var name in new[]
                 {
                     "wp13-e1-fleetpy-rolling-cost-retained-v1.json",
                     "wp13-e1-fleetpy-ridebound-hard-vector-retained-v1.json",
                 })
        {
            var json = File.ReadAllBytes(
                Path.Combine(
                    RepositoryRoot(),
                    "benchmarks",
                    "configurations",
                    name));
            var configuration = Wp4RunnerConfiguration.Decode(json, commitment);

            Assert.Equal(
                Wp4RunnerConfiguration.RetainedPortfolioEvidenceProfile,
                configuration.SolverExecutionEvidenceProfile);
            Assert.False(
                configuration.CollectAllCommitmentWitnesses,
                $"{name} must keep the evidence its freeze receipt recorded");
        }
    }

    private static string WithProfile(string baseJson, string profile) =>
        baseJson.Replace(
            "\"policyVersion\": \"wp4-boundary-v1\"",
            "\"policyVersion\": \"wp4-boundary-v1\",\n  " +
            "\"emitSolverExecutionEvidence\": true,\n  " +
            "\"solverExecutionEvidenceProfile\": " +
            $"\"{profile}\"",
            StringComparison.Ordinal);

    [Fact]
    public void Constant_level_skip_is_optional_and_off_unless_declared()
    {
        var commitment = CommitmentConfiguration();
        var baseJson = PublishedJson();

        // Every configuration written before RB-WP14-002 omits the field and must
        // keep the solver work, and therefore the decision hash, it recorded.
        var published = Wp4RunnerConfiguration.Decode(
            Encoding.UTF8.GetBytes(baseJson),
            commitment);

        Assert.False(
            published.SolverPolicyOptions!.ExecutionBudget.SolverBudget
                .SkipConstantObjectiveLevels);
        Assert.False(
            published.CreateSolverPolicyOptionsForRun(19).ExecutionBudget
                .SolverBudget.SkipConstantObjectiveLevels);

        var optedInJson = baseJson.Replace(
            "\"randomSeed\": 7",
            "\"randomSeed\": 7,\n    \"skipConstantObjectiveLevels\": true",
            StringComparison.Ordinal);
        var optedIn = Wp4RunnerConfiguration.Decode(
            Encoding.UTF8.GetBytes(optedInJson),
            commitment);

        Assert.True(
            optedIn.SolverPolicyOptions!.ExecutionBudget.SolverBudget
                .SkipConstantObjectiveLevels);
        Assert.True(
            optedIn.CreateSolverPolicyOptionsForRun(19).ExecutionBudget
                .SolverBudget.SkipConstantObjectiveLevels);

        // Opting in is a different configuration and must not share a hash.
        Assert.NotEqual(published.ContentHash, optedIn.ContentHash);

        var explicitlyOff = baseJson.Replace(
            "\"randomSeed\": 7",
            "\"randomSeed\": 7,\n    \"skipConstantObjectiveLevels\": false",
            StringComparison.Ordinal);

        Assert.False(
            Wp4RunnerConfiguration.Decode(
                    Encoding.UTF8.GetBytes(explicitlyOff),
                    commitment)
                .SolverPolicyOptions!.ExecutionBudget.SolverBudget
                .SkipConstantObjectiveLevels);

        var wrongType = baseJson.Replace(
            "\"randomSeed\": 7",
            "\"randomSeed\": 7,\n    \"skipConstantObjectiveLevels\": \"yes\"",
            StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(
            () => Wp4RunnerConfiguration.Decode(
                Encoding.UTF8.GetBytes(wrongType),
                commitment));
    }

    [Fact]
    public void Candidate_retention_strategy_is_explicit_for_new_configs_and_legacy_when_absent()
    {
        var commitment = CommitmentConfiguration();
        var legacy = Wp4RunnerConfiguration.Decode(
            Encoding.UTF8.GetBytes(PublishedJson()),
            commitment);
        var portfolioJson = PublishedJson().Replace(
            "\"scheduleStrategy\": \"earliest-feasible\"",
            "\"scheduleStrategy\": \"earliest-feasible\",\n    " +
            "\"retentionStrategy\": \"service-set-stability-portfolio-v1\"",
            StringComparison.Ordinal);
        var portfolio = Wp4RunnerConfiguration.Decode(
            Encoding.UTF8.GetBytes(portfolioJson),
            commitment);

        Assert.Equal(
            CandidateRetentionStrategy.LegacyAcceptedCountCostSlack,
            legacy.CandidateGeneration.RetentionStrategy);
        Assert.Equal(
            CandidateRetentionStrategy.ServiceSetStabilityPortfolioV1,
            portfolio.CandidateGeneration.RetentionStrategy);
        Assert.Equal(
            InitialPromiseTrigger.InitialAcceptance,
            portfolio.InitialPromiseTrigger);

        var unknown = portfolioJson.Replace(
            "service-set-stability-portfolio-v1",
            "unknown-retention",
            StringComparison.Ordinal);
        Assert.Contains(
            "retentionStrategy",
            Assert.Throws<InvalidDataException>(
                () => Wp4RunnerConfiguration.Decode(
                    Encoding.UTF8.GetBytes(unknown),
                    commitment)).Message);

        var unknownTrigger = portfolioJson.Replace(
            "\"policyVersion\": \"wp4-boundary-v1\"",
            "\"policyVersion\": \"wp4-boundary-v1\",\n  " +
            "\"initialPromiseTrigger\": \"unknown-trigger\"",
            StringComparison.Ordinal);
        Assert.Contains(
            "initialPromiseTrigger",
            Assert.Throws<InvalidDataException>(
                () => Wp4RunnerConfiguration.Decode(
                    Encoding.UTF8.GetBytes(unknownTrigger),
                    commitment)).Message);
    }

    [Fact]
    public void Policy_specific_fields_are_required_and_forbidden_exactly()
    {
        var commitment = CommitmentConfiguration();
        var rolling = PublishedJson();
        var missingFreeze = rolling.Replace(
            "\"rolling-cost\"",
            "\"fixed-freeze-horizon\"",
            StringComparison.Ordinal);
        var unexpectedRepair = rolling.Replace(
            "\n  \"solverExecution\"",
            "\n  \"repair\": {\"maximumRequestsConsideredPerVehicle\":1},\n  \"solverExecution\"",
            StringComparison.Ordinal);

        Assert.Contains(
            "variant fields",
            Assert.Throws<InvalidDataException>(
                () => Wp4RunnerConfiguration.Decode(
                    Encoding.UTF8.GetBytes(missingFreeze),
                    commitment)).Message);
        Assert.Contains(
            "variant fields",
            Assert.Throws<InvalidDataException>(
                () => Wp4RunnerConfiguration.Decode(
                    Encoding.UTF8.GetBytes(unexpectedRepair),
                    commitment)).Message);
    }

    [Fact]
    public void C2_requires_all_profiles_and_rejects_warning_above_hard_limit()
    {
        var commitment = CommitmentConfiguration();
        var limits = string.Join(
            ",",
            CommitmentDimensionVocabulary.Ordered.Select(
                dimension =>
                    $"{{\"dimension\":\"{CommitmentDimensionVocabulary.ToProtocolValue(dimension)}\"}}"));
        var warningProfiles =
            $"[{{\"policyId\":\"uniform-v1\",\"limits\":[{limits}]}}]";
        var c2 = PublishedJson()
            .Replace(
                "\"rolling-cost\"",
                "\"commit-soft-hard-hybrid\"",
                StringComparison.Ordinal)
            .Replace(
                "\n  \"solverExecution\"",
                $"\n  \"warningProfiles\":{warningProfiles},\n  \"solverExecution\"",
                StringComparison.Ordinal);

        var valid = Wp4RunnerConfiguration.Decode(
            Encoding.UTF8.GetBytes(c2),
            commitment);
        Assert.True(valid.TryGetProfile("uniform-v1", out var profile));
        Assert.All(profile.Limits.Values, value => Assert.Null(value.WarningLimit));

        var invalid = c2.Replace(
            "\"dimension\":\"vehicle_switch_count\"",
            "\"dimension\":\"vehicle_switch_count\",\"warningLimit\":1",
            StringComparison.Ordinal);
        Assert.Contains(
            "cannot exceed",
            Assert.Throws<InvalidDataException>(
                () => Wp4RunnerConfiguration.Decode(
                    Encoding.UTF8.GetBytes(invalid),
                    commitment)).Message);
    }

    [Fact]
    public void B5_uses_explicit_plan_pool_budget_and_forbids_solver_block()
    {
        var commitment = CommitmentConfiguration();
        var b5 =
            """
            {
              "configurationVersion":"1.0.0",
              "policyId":"least-commitment-consensus",
              "policyVersion":"wp4-b5-test-v1",
              "candidateGeneration":{
                "maximumCandidatesPerVehicle":100,
                "maximumNewRequestsPerVehicle":2,
                "exactSmallMode":false,
                "scheduleStrategy":"earliest-feasible",
                "maximumExplorationWorkUnits":10000
              },
              "multiplePlan":{
                "maximumPlanCount":4,
                "maximumCombinationWorkUnits":10000,
                "requireCompleteEnumeration":true
              }
            }
            """;

        var configuration = Wp4RunnerConfiguration.Decode(
            Encoding.UTF8.GetBytes(b5),
            commitment);

        Assert.Null(configuration.SolverPolicyOptions);
        Assert.Equal(4, configuration.MultiplePlanOptions!.MaximumPlanCount);
        Assert.True(configuration.MultiplePlanOptions.RequireCompleteEnumeration);
    }

    private static CommitmentPolicyConfiguration CommitmentConfiguration() =>
        CommitmentPolicyConfiguration.Decode(
            File.ReadAllBytes(Path.Combine(
                RepositoryRoot(),
                "benchmarks",
                "configurations",
                "wp3-boundary-test-v1.json")));

    private static string PublishedJson() =>
        File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "benchmarks",
            "configurations",
            "wp4-rolling-cost-boundary-v1.json"));

    private static string RepositoryRoot() =>
        typeof(Wp4RunnerConfigurationTests)
            .Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(value => value.Key == "RideBoundRepositoryRoot")
            .Value!;
}
