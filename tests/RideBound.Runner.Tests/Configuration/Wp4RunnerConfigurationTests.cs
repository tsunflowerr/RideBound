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
