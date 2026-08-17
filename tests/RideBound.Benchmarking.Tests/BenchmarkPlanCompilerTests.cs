using RideBound.Benchmarking.Contracts;
using RideBound.Benchmarking.Planning;
using RideBound.Contracts.Protocol;

namespace RideBound.Benchmarking.Tests;

public sealed class BenchmarkPlanCompilerTests
{
    [Fact]
    public void Compiler_materializes_complete_counterbalanced_grid_before_outcomes()
    {
        var result = BenchmarkPlanCompiler.Compile(Definition());

        Assert.True(result.IsSuccess, result.Issue?.ToString());
        var compiled = result.Value!;
        Assert.Equal(16, compiled.PlannedRuns.Count);
        Assert.Equal(16, compiled.PlannedRuns.Select(value => value.RunId).Distinct().Count());
        Assert.Equal(Enumerable.Range(1, 16).Select(value => (long)value), compiled.PlannedRuns.Select(value => value.ExecutionOrdinal));
        Assert.Equal(4, compiled.PlannedRuns.Count(value => value.Warmup));
        Assert.Equal(12, compiled.PlannedRuns.Count(value => !value.Warmup));
        Assert.All(compiled.PlannedRuns.Where(value => value.Warmup), value => Assert.Equal(0, value.RepeatIndex));
        Assert.DoesNotContain(compiled.PlannedRuns.Where(value => !value.Warmup), value => value.RepeatIndex == 0);

        foreach (var pair in compiled.PlannedRuns
            .GroupBy(value => (value.ScenarioHash, value.RepeatIndex)))
        {
            Assert.Equal(2, pair.Count());
            Assert.Single(pair.Select(value => value.AdapterSeed.DigestHex).Distinct());
            Assert.Single(pair.Select(value => value.SimulatorSeed.DigestHex).Distinct());
            Assert.Equal(2, pair.Select(value => value.SolverSeed.DigestHex).Distinct().Count());
            Assert.Equal(
                pair.OrderBy(value => value.ArmOrderRankHex, StringComparer.Ordinal)
                    .ThenBy(value => value.ArmId, StringComparer.Ordinal)
                    .Select(value => value.ArmId),
                pair.Select(value => value.ArmId));
        }
    }

    [Fact]
    public async Task Input_permutation_and_parallel_compilation_are_byte_and_order_exact()
    {
        var definition = Definition();
        var permuted = definition with
        {
            ScenarioHashes = definition.ScenarioHashes.Reverse().ToArray(),
            Arms = definition.Arms.Reverse().ToArray(),
        };
        var expected = BenchmarkPlanCompiler.Compile(definition).Value!;
        var reordered = BenchmarkPlanCompiler.Compile(permuted).Value!;

        Assert.Equal(expected.CanonicalPlanBytes, reordered.CanonicalPlanBytes);
        Assert.Equal(expected.PlanHash, reordered.PlanHash);
        Assert.Equal(expected.PlannedRuns, reordered.PlannedRuns);

        var tasks = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() => BenchmarkPlanCompiler.Compile(permuted).Value!))
            .ToArray();
        var parallel = await Task.WhenAll(tasks);
        Assert.All(parallel, value => Assert.Equal(expected.PlanHash, value.PlanHash));
        Assert.All(parallel, value => Assert.Equal(expected.PlannedRuns, value.PlannedRuns));
    }

    [Fact]
    public void Asymmetric_or_cross_family_pairing_fails_closed()
    {
        var definition = Definition();
        var changedWork = definition with
        {
            Arms =
            [
                definition.Arms[0],
                definition.Arms[1] with { SolverWorkBudget = 101 },
            ],
        };
        var changedCapability = definition with
        {
            Arms =
            [
                definition.Arms[0],
                definition.Arms[1] with
                {
                    CanonicalCapabilitySelection = "{\"nodeOnly\":false}"u8.ToArray(),
                },
            ],
        };
        var b5Mixed = definition with
        {
            PairingClassId = "wp4-multiple-plan-v1",
            Arms =
            [
                definition.Arms[0] with { ArmId = "b5" },
                definition.Arms[1],
            ],
        };

        Assert.Equal(
            "pairing.asymmetric-mechanics",
            BenchmarkPlanCompiler.Compile(changedWork).Issue!.Code);
        Assert.Equal(
            "pairing.asymmetric-mechanics",
            BenchmarkPlanCompiler.Compile(changedCapability).Issue!.Code);
        Assert.Equal(
            "pairing.b5-separation-required",
            BenchmarkPlanCompiler.Compile(b5Mixed).Issue!.Code);
    }

    [Fact]
    public void Compiler_hashes_caller_config_without_choosing_open_research_values()
    {
        var definition = Definition();
        var first = BenchmarkPlanCompiler.Compile(definition).Value!;
        var changed = definition with
        {
            Arms =
            [
                definition.Arms[0] with
                {
                    CanonicalPolicyConfiguration = "{\"callerValue\":2,\"policy\":\"c1\"}"u8.ToArray(),
                },
                definition.Arms[1],
            ],
        };
        var second = BenchmarkPlanCompiler.Compile(changed).Value!;

        Assert.NotEqual(first.PlanHash, second.PlanHash);
        Assert.NotEqual(
            first.Plan.Arms.Single(value => value.ArmId == "c1").PolicyConfigurationSha256,
            second.Plan.Arms.Single(value => value.ArmId == "c1").PolicyConfigurationSha256);
        Assert.DoesNotContain(
            "O-002",
            System.Text.Encoding.UTF8.GetString(first.CanonicalPlanBytes),
            StringComparison.OrdinalIgnoreCase);

        var noncanonical = definition with
        {
            Arms =
            [
                definition.Arms[0] with
                {
                    CanonicalPolicyConfiguration = "{ \"policy\": \"b1\" }"u8.ToArray(),
                },
                definition.Arms[1],
            ],
        };
        Assert.Equal(
            "plan.noncanonical-arm-input",
            BenchmarkPlanCompiler.Compile(noncanonical).Issue!.Code);

        var oversized = definition with { WarmupRunCount = 1_000_000 };
        Assert.Equal(
            "plan.grid-too-large",
            BenchmarkPlanCompiler.Compile(oversized).Issue!.Code);
    }

    [Fact]
    public void Compiler_derives_exact_wp4_runner_binding_from_both_config_components()
    {
        Assert.True(Sha256Hex.TryCreate(new string('1', 64), out var commitment));
        Assert.True(Sha256Hex.TryCreate(new string('2', 64), out var b1Wp4));
        Assert.True(Sha256Hex.TryCreate(new string('3', 64), out var c1Wp4));
        var definition = Definition();
        definition = definition with
        {
            Arms =
            [
                definition.Arms[0] with
                {
                    PolicyConfigurationBindingId =
                        Wp4PolicyConfigurationBinding.BindingId,
                    CanonicalPolicyConfiguration =
                        Wp4PolicyConfigurationBinding.CreateCanonicalDocument(
                            commitment!,
                            c1Wp4!),
                },
                definition.Arms[1] with
                {
                    PolicyConfigurationBindingId =
                        Wp4PolicyConfigurationBinding.BindingId,
                    CanonicalPolicyConfiguration =
                        Wp4PolicyConfigurationBinding.CreateCanonicalDocument(
                            commitment!,
                            b1Wp4!),
                },
            ],
        };

        var compiled = BenchmarkPlanCompiler.Compile(definition);

        Assert.True(compiled.IsSuccess, compiled.Issue?.ToString());
        Assert.Equal(
            Wp4PolicyConfigurationBinding.Calculate(commitment!, c1Wp4!).Value,
            compiled.Value!.Plan.Arms.Single(value => value.ArmId == "c1")
                .PolicyConfigurationSha256);
        Assert.Equal(
            Wp4PolicyConfigurationBinding.Calculate(commitment!, b1Wp4!).Value,
            compiled.Value.Plan.Arms.Single(value => value.ArmId == "b1")
                .PolicyConfigurationSha256);

        var unknown = definition with
        {
            Arms =
            [
                definition.Arms[0] with
                {
                    PolicyConfigurationBindingId = "unregistered-v1",
                },
                definition.Arms[1],
            ],
        };
        Assert.Equal(
            "plan.policy-binding-unknown",
            BenchmarkPlanCompiler.Compile(unknown).Issue!.Code);

        var malformed = definition with
        {
            Arms =
            [
                definition.Arms[0] with
                {
                    CanonicalPolicyConfiguration =
                        "{\"bindingId\":\"ridebound-wp4-policy-binding-v1\"}"u8.ToArray(),
                },
                definition.Arms[1],
            ],
        };
        Assert.Equal(
            "plan.policy-binding-invalid",
            BenchmarkPlanCompiler.Compile(malformed).Issue!.Code);
    }

    [Fact]
    public void Planning_and_seed_sources_have_no_hidden_rng_or_runtime_order_source()
    {
        var root = FindRepositoryRoot();
        var paths = Directory.GetFiles(
                Path.Combine(root, "src", "RideBound.Benchmarking", "Planning"),
                "*.cs")
            .Append(
                Path.Combine(
                    root,
                    "src",
                    "RideBound.Benchmarking.Contracts",
                    "BenchmarkSeed.cs"));
        var forbidden = new[]
        {
            "new Random",
            "Random.Shared",
            "Guid.NewGuid",
            "DateTime.Now",
            "DateTime.UtcNow",
            "DateTimeOffset.Now",
            "Environment.TickCount",
            "Thread.Current",
            "Process.GetCurrentProcess",
            ".GetHashCode(",
        };

        foreach (var path in paths)
        {
            var source = File.ReadAllText(path);
            Assert.All(
                forbidden,
                token => Assert.DoesNotContain(token, source, StringComparison.Ordinal));
        }
    }

    private static BenchmarkPlanDefinition Definition()
    {
        var capabilities = "{\"nodeOnly\":true}"u8.ToArray();
        return new BenchmarkPlanDefinition(
            "wp6-planning-test",
            EvidenceClass.Mechanical,
            [
                "88a8730afb6149052fbe97672e5cf77f9bd352b47a7039735b7e985140370e88",
                "3371cfd2a0b25e8c85812aa66e0338c7a03c5aa97661c4e13c8764343277b83e",
            ],
            [
                new BenchmarkArmDefinition(
                    "c1",
                    "ridebound-hard-vector",
                    "1.0.0",
                    BenchmarkPlanCompiler.CanonicalJsonPolicyBindingId,
                    "{\"policy\":\"c1\"}"u8.ToArray(),
                    "wp4-common-generator-v1",
                    1_000,
                    "wp3-validator-v1",
                    "deterministic-search",
                    "1.0.0",
                    100,
                    capabilities),
                new BenchmarkArmDefinition(
                    "b1",
                    "rolling-cost",
                    "1.0.0",
                    BenchmarkPlanCompiler.CanonicalJsonPolicyBindingId,
                    "{\"policy\":\"b1\"}"u8.ToArray(),
                    "wp4-common-generator-v1",
                    1_000,
                    "wp3-validator-v1",
                    "deterministic-search",
                    "1.0.0",
                    100,
                    capabilities),
            ],
            "wp4-common-candidate-v1",
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            1,
            3,
            "wp6-local-mechanical-v1",
            new string('6', 64),
            new RunnerArtifactIdentity(
                new string('7', 64),
                new string('8', 64),
                new string('9', 64),
                new string('a', 64),
                "runner-ndjson-v1"),
            new string('b', 64),
            new string('c', 64));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "RideBound.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("RideBound repository root not found.");
    }
}
