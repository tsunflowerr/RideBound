using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using RideBound.Benchmarking.Contracts;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;

namespace RideBound.Benchmarking.Planning;

public static class BenchmarkPlanCompiler
{
    public const long MaximumMaterializedRunCount = 1_000_000;

    public const string CanonicalJsonPolicyBindingId =
        "canonical-json-sha256-v1";

    private static readonly IReadOnlySet<string> CommonCandidateArmIds =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "b1",
            "b2",
            "b3",
            "b4",
            "c1",
            "c2",
        };

    public static BenchmarkPlanCompilationResult Compile(
        BenchmarkPlanDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (definition.Arms is null || definition.Arms.Count == 0)
        {
            return BenchmarkPlanCompilationResult.Failed(
                "plan.arm-set-empty",
                "$.arms",
                "At least one arm definition is required.");
        }

        if (definition.ScenarioHashes is null || definition.ScenarioHashes.Count == 0)
        {
            return BenchmarkPlanCompilationResult.Failed(
                "plan.scenario-set-empty",
                "$.scenarioHashes",
                "At least one scenario identity is required.");
        }

        if (definition.RunnerArtifact is null)
        {
            return BenchmarkPlanCompilationResult.Failed(
                "plan.runner-artifact-missing",
                "$.runnerArtifact",
                "Runner artifact identity is required before plan materialization.");
        }

        if (definition.ScenarioHashes.Distinct(StringComparer.Ordinal).Count()
            != definition.ScenarioHashes.Count)
        {
            return BenchmarkPlanCompilationResult.Failed(
                "plan.duplicate-scenario",
                "$.scenarioHashes",
                "Scenario identities must be unique before compilation.");
        }

        var armIds = definition.Arms.Select(value => value.ArmId).ToArray();

        if (armIds.Distinct(StringComparer.Ordinal).Count() != armIds.Length)
        {
            return BenchmarkPlanCompilationResult.Failed(
                "plan.duplicate-arm",
                "$.arms",
                "Arm IDs must be unique before compilation.");
        }

        var compiledArms = new List<BenchmarkArm>(definition.Arms.Count);

        for (var index = 0; index < definition.Arms.Count; index++)
        {
            var source = definition.Arms[index];

            if (!IsExactCanonical(source.CanonicalPolicyConfiguration)
                || !IsExactCanonical(source.CanonicalCapabilitySelection))
            {
                return BenchmarkPlanCompilationResult.Failed(
                    "plan.noncanonical-arm-input",
                    $"$.arms[{index}]",
                    "Policy configuration and capability selection must be exact canonical JSON bytes.");
            }

            string policyConfigurationSha256;

            try
            {
                policyConfigurationSha256 = source.PolicyConfigurationBindingId switch
                {
                    CanonicalJsonPolicyBindingId =>
                        FileSha(source.CanonicalPolicyConfiguration),
                    Wp4PolicyConfigurationBinding.BindingId =>
                        Wp4PolicyConfigurationBinding.DecodeExactAndCalculate(
                            source.CanonicalPolicyConfiguration).Value,
                    _ => throw new InvalidDataException(
                        "Policy configuration binding is not registered."),
                };
            }
            catch (InvalidDataException exception)
            {
                return BenchmarkPlanCompilationResult.Failed(
                    source.PolicyConfigurationBindingId
                        == Wp4PolicyConfigurationBinding.BindingId
                            ? "plan.policy-binding-invalid"
                            : "plan.policy-binding-unknown",
                    $"$.arms[{index}].policyConfigurationBindingId",
                    exception.Message);
            }
            var capabilitySelectionSha256 = FileSha(source.CanonicalCapabilitySelection);
            var effectiveConfigurationSha256 = EffectiveConfigurationHash(
                source,
                policyConfigurationSha256,
                capabilitySelectionSha256,
                definition.PairingClassId,
                definition.RunnerArtifact.LaunchContractId);
            compiledArms.Add(
                new BenchmarkArm(
                    source.ArmId,
                    source.PolicyId,
                    source.PolicyVersion,
                    policyConfigurationSha256,
                    effectiveConfigurationSha256,
                    source.CandidateGeneratorId,
                    source.CandidateWorkBudget,
                    source.ValidatorVersion,
                    source.SolverId,
                    source.SolverVersion,
                    source.SolverWorkBudget,
                    capabilitySelectionSha256,
                    definition.PairingClassId));
        }

        var pairingIssue = ValidatePairing(definition.PairingClassId, compiledArms);

        if (pairingIssue is not null)
        {
            return pairingIssue;
        }

        var plan = new BenchmarkPlan(
            BenchmarkContractVersions.V1,
            definition.PlanId,
            definition.EvidenceClass,
            "wp6-mechanical-only-v1",
            definition.ScenarioHashes
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            compiledArms.OrderBy(value => value.ArmId, StringComparer.Ordinal).ToArray(),
            definition.PairingClassId,
            definition.MasterSeedHex,
            definition.WarmupRunCount,
            definition.MeasuredRepeatCount,
            "hash-counterbalanced-v1",
            definition.ResourceProfileId,
            "wp6-failure-v1.0.2",
            "wp6-exclusion-v1",
            definition.MetricRegistryHash,
            definition.RunnerArtifact,
            definition.HarnessSourceSha256,
            definition.OracleSourceSha256);
        var contractError = BenchmarkContractValidator.Validate(plan);

        if (contractError is not null)
        {
            return BenchmarkPlanCompilationResult.Failed(
                "plan.contract-invalid",
                contractError.Path,
                contractError.Message);
        }

        long plannedRunCount;

        try
        {
            plannedRunCount = checked(
                (plan.WarmupRunCount + plan.MeasuredRepeatCount)
                * plan.ScenarioHashes.Count
                * plan.Arms.Count);
        }
        catch (OverflowException)
        {
            plannedRunCount = long.MaxValue;
        }

        if (plannedRunCount > MaximumMaterializedRunCount)
        {
            return BenchmarkPlanCompilationResult.Failed(
                "plan.grid-too-large",
                "$",
                $"Planned grid exceeds the bounded materialization cap of {MaximumMaterializedRunCount} runs.");
        }

        var canonical = BenchmarkContractCodec.Encode(plan);
        var planHash = BenchmarkIdentity.CalculateBenchmarkPlan(canonical);
        var runs = MaterializeGrid(plan, planHash);
        return BenchmarkPlanCompilationResult.Success(
            new CompiledBenchmarkPlan(plan, canonical, planHash, runs));
    }

    public static IReadOnlyList<PlannedBenchmarkRun> MaterializeVerifiedGrid(
        BenchmarkPlan plan,
        string planHash)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var canonical = BenchmarkContractCodec.Encode(plan);
        var actualPlanHash = BenchmarkIdentity.CalculateBenchmarkPlan(canonical);

        if (!string.Equals(actualPlanHash, planHash, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Plan hash does not match the exact canonical benchmark plan.",
                nameof(planHash));
        }

        long plannedRunCount;

        try
        {
            plannedRunCount = checked(
                (plan.WarmupRunCount + plan.MeasuredRepeatCount)
                * plan.ScenarioHashes.Count
                * plan.Arms.Count);
        }
        catch (OverflowException)
        {
            plannedRunCount = long.MaxValue;
        }

        if (plannedRunCount > MaximumMaterializedRunCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(plan),
                $"Plan exceeds the bounded materialization cap of {MaximumMaterializedRunCount} runs.");
        }

        return MaterializeGrid(plan, planHash);
    }

    private static BenchmarkPlanCompilationResult? ValidatePairing(
        string pairingClassId,
        IReadOnlyList<BenchmarkArm> arms)
    {
        if (pairingClassId == "mechanical-single-arm-v1")
        {
            return arms.Count == 1
                ? null
                : BenchmarkPlanCompilationResult.Failed(
                    "pairing.single-arm-required",
                    "$.arms",
                    "mechanical-single-arm-v1 requires exactly one arm.");
        }

        if (pairingClassId == "wp4-multiple-plan-v1")
        {
            return arms.All(
                arm => arm.ArmId == "b5"
                    || arm.ArmId.StartsWith("b5-", StringComparison.Ordinal))
                ? null
                : BenchmarkPlanCompilationResult.Failed(
                    "pairing.b5-separation-required",
                    "$.arms",
                    "wp4-multiple-plan-v1 accepts only B5-family arms.");
        }

        if (pairingClassId != "wp4-common-candidate-v1")
        {
            return BenchmarkPlanCompilationResult.Failed(
                "pairing.unknown-class",
                "$.pairingClassId",
                "Pairing class is not registered by WP6 v1.");
        }

        if (arms.Any(arm => !CommonCandidateArmIds.Contains(arm.ArmId)))
        {
            return BenchmarkPlanCompilationResult.Failed(
                "pairing.arm-not-comparable",
                "$.arms",
                "Common-candidate pairing accepts B1/B2/B3/B4/C1/C2 only.");
        }

        if (arms.Count < 2)
        {
            return BenchmarkPlanCompilationResult.Failed(
                "pairing.multiple-arms-required",
                "$.arms",
                "wp4-common-candidate-v1 requires at least two comparable arms.");
        }

        var first = arms[0];
        var asymmetric = arms.Any(
            arm => arm.CandidateGeneratorId != first.CandidateGeneratorId
                || arm.CandidateWorkBudget != first.CandidateWorkBudget
                || arm.ValidatorVersion != first.ValidatorVersion
                || arm.SolverId != first.SolverId
                || arm.SolverVersion != first.SolverVersion
                || arm.SolverWorkBudget != first.SolverWorkBudget
                || arm.CapabilitySelectionSha256 != first.CapabilitySelectionSha256);
        return asymmetric
            ? BenchmarkPlanCompilationResult.Failed(
                "pairing.asymmetric-mechanics",
                "$.arms",
                "Paired arms must share candidate, validator, solver work and capability mechanics exactly.")
            : null;
    }

    private static IReadOnlyList<PlannedBenchmarkRun> MaterializeGrid(
        BenchmarkPlan plan,
        string planHash)
    {
        var result = new List<PlannedBenchmarkRun>();
        long ordinal = 0;
        var totalCycles = checked(plan.WarmupRunCount + plan.MeasuredRepeatCount);

        for (long repeatIndex = 0; repeatIndex < totalCycles; repeatIndex++)
        {
            var warmup = repeatIndex < plan.WarmupRunCount;

            foreach (var scenarioHash in plan.ScenarioHashes)
            {
                var orderedArms = plan.Arms
                    .Select(
                        arm => new
                        {
                            Arm = arm,
                            Rank = BenchmarkSeed.Derive(
                                plan.MasterSeedHex,
                                scenarioHash,
                                repeatIndex,
                                "arm-run-order",
                                arm.ArmId),
                        })
                    .OrderBy(value => value.Rank.DigestHex, StringComparer.Ordinal)
                    .ThenBy(value => value.Arm.ArmId, StringComparer.Ordinal)
                    .ToArray();

                foreach (var ordered in orderedArms)
                {
                    ordinal = checked(ordinal + 1);
                    var armId = ordered.Arm.ArmId;
                    result.Add(
                        new PlannedBenchmarkRun(
                            BenchmarkIdentity.CalculateRun(
                                planHash,
                                scenarioHash,
                                armId,
                                repeatIndex,
                                0),
                            planHash,
                            scenarioHash,
                            armId,
                            repeatIndex,
                            0,
                            warmup,
                            ordinal,
                            ordered.Rank.DigestHex,
                            BenchmarkSeed.Derive(
                                plan.MasterSeedHex,
                                scenarioHash,
                                repeatIndex,
                                "adapter-rng"),
                            BenchmarkSeed.Derive(
                                plan.MasterSeedHex,
                                scenarioHash,
                                repeatIndex,
                                "simulator-rng"),
                            BenchmarkSeed.Derive(
                                plan.MasterSeedHex,
                                scenarioHash,
                                repeatIndex,
                                "solver-rng",
                                armId),
                            BenchmarkSeed.Derive(
                                plan.MasterSeedHex,
                                scenarioHash,
                                repeatIndex,
                                "failure-injection",
                                armId)));
                }
            }
        }

        if (result.Select(value => value.RunId).Distinct(StringComparer.Ordinal).Count()
            != result.Count)
        {
            throw new InvalidOperationException("Planned run identities collided.");
        }

        return result;
    }

    private static string EffectiveConfigurationHash(
        BenchmarkArmDefinition definition,
        string policyConfigurationSha256,
        string capabilitySelectionSha256,
        string pairingClassId,
        string launchContractId)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes("RideBound.Wp6.EffectiveConfiguration.v1\0"));
        AppendFrame(hash, "policyConfigurationSha256", Convert.FromHexString(policyConfigurationSha256));
        AppendFrame(hash, "policyId", Encoding.UTF8.GetBytes(definition.PolicyId));
        AppendFrame(hash, "policyVersion", Encoding.UTF8.GetBytes(definition.PolicyVersion));
        AppendFrame(hash, "launchContractId", Encoding.UTF8.GetBytes(launchContractId));
        AppendFrame(hash, "candidateGeneratorId", Encoding.UTF8.GetBytes(definition.CandidateGeneratorId));
        AppendFrame(hash, "candidateWorkBudget", Encoding.ASCII.GetBytes(definition.CandidateWorkBudget.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        AppendFrame(hash, "validatorVersion", Encoding.UTF8.GetBytes(definition.ValidatorVersion));
        AppendFrame(hash, "solverId", Encoding.UTF8.GetBytes(definition.SolverId));
        AppendFrame(hash, "solverVersion", Encoding.UTF8.GetBytes(definition.SolverVersion));
        AppendFrame(hash, "solverWorkBudget", Encoding.ASCII.GetBytes(definition.SolverWorkBudget.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        AppendFrame(hash, "capabilitySelectionSha256", Convert.FromHexString(capabilitySelectionSha256));
        AppendFrame(hash, "pairingClassId", Encoding.UTF8.GetBytes(pairingClassId));
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static void AppendFrame(
        IncrementalHash hash,
        string tag,
        ReadOnlySpan<byte> value)
    {
        var tagBytes = Encoding.UTF8.GetBytes(tag);
        Span<byte> tagLength = stackalloc byte[sizeof(ushort)];
        BinaryPrimitives.WriteUInt16BigEndian(tagLength, checked((ushort)tagBytes.Length));
        hash.AppendData(tagLength);
        hash.AppendData(tagBytes);
        Span<byte> valueLength = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64BigEndian(valueLength, checked((ulong)value.Length));
        hash.AppendData(valueLength);
        hash.AppendData(value);
    }

    private static bool IsExactCanonical(byte[] bytes)
    {
        if (bytes is null || bytes.Length == 0)
        {
            return false;
        }

        try
        {
            return bytes.AsSpan().SequenceEqual(CanonicalJson.Canonicalize(bytes));
        }
        catch (CanonicalJsonException)
        {
            return false;
        }
    }

    private static string FileSha(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));
}
