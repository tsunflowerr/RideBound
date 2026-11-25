using System.Security.Cryptography;
using System.Text.Json;
using RideBound.Algorithms.Candidates;
using RideBound.Application.Optimization;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;
using RideBound.Solvers.OrTools;
using static RideBound.Runner.Configuration.JsonConfig;

namespace RideBound.Runner.Configuration;

public sealed class RollingCostSolverConfiguration
{
    public const string PolicyId = "rolling-cost-cp-sat";

    private RollingCostSolverConfiguration(
        Sha256Hex contentHash,
        string policyVersion,
        CandidateGenerationOptions candidateGeneration,
        DeterministicCandidateSelectionExecutionBudget executionBudget)
    {
        ContentHash = contentHash;
        PolicyVersion = policyVersion;
        CandidateGeneration = candidateGeneration;
        ExecutionBudget = executionBudget;
    }

    public Sha256Hex ContentHash { get; }

    public string PolicyVersion { get; }

    public CandidateGenerationOptions CandidateGeneration { get; }

    public DeterministicCandidateSelectionExecutionBudget ExecutionBudget { get; }

    public bool Matches(RunManifestIdentity manifest) =>
        manifest.PolicyId == PolicyId
        && manifest.PolicyVersion == PolicyVersion
        && manifest.PolicyConfigurationHash == ContentHash;

    public static RollingCostSolverConfiguration Decode(ReadOnlySpan<byte> utf8Json)
    {
        var canonical = CanonicalJson.Canonicalize(utf8Json);
        using var document = JsonDocument.Parse(canonical);
        var root = document.RootElement;
        RequireFields(
            root,
            "$",
            "configurationVersion",
            "policyId",
            "policyVersion",
            "candidateGeneration",
            "solverExecution");

        if (Text(root, "configurationVersion") != "1.0.0")
        {
            throw new InvalidDataException("configurationVersion must be 1.0.0.");
        }

        if (Text(root, "policyId") != PolicyId)
        {
            throw new InvalidDataException($"policyId must be {PolicyId}.");
        }

        var policyVersion = Text(root, "policyVersion");
        if (!OpaqueIdentifier.IsValid(policyVersion))
        {
            throw new InvalidDataException("policyVersion must be a valid protocol identifier.");
        }

        var candidateOptions = ReadCandidateGeneration(root.GetProperty("candidateGeneration"));
        var executionBudget = ReadExecutionBudget(root.GetProperty("solverExecution"));
        Sha256Hex.TryCreate(Convert.ToHexStringLower(SHA256.HashData(canonical)), out var contentHash);
        return new RollingCostSolverConfiguration(contentHash!, policyVersion, candidateOptions, executionBudget);
    }

    internal static CandidateGenerationOptions ReadCandidateGeneration(
        JsonElement generation, bool allowRelocation = false)
    {
        string[] fields = ["maximumCandidatesPerVehicle", "maximumNewRequestsPerVehicle", "maximumExplorationWorkUnits"];
        WaitingRiderRelocationGenerationOptions? relocationOptions = null;
        if (allowRelocation
            && generation.ValueKind == JsonValueKind.Object
            && generation.TryGetProperty("waitingRiderRelocation", out var relocation))
        {
            RequireFields(generation, "candidateGeneration", [..fields, "waitingRiderRelocation"]);
            if (relocation.ValueKind == JsonValueKind.Object && !relocation.EnumerateObject().Any())
            {
                relocationOptions = new WaitingRiderRelocationGenerationOptions();
            }
            else
            {
                RequireFields(relocation, "candidateGeneration.waitingRiderRelocation", "maximumTargetVehicles");
                relocationOptions = new WaitingRiderRelocationGenerationOptions(
                    (int)Integer(relocation, "maximumTargetVehicles", 1, int.MaxValue));
            }
        }
        else
        {
            RequireFields(generation, "candidateGeneration", fields);
        }

        return new CandidateGenerationOptions(
            (int)Integer(generation, "maximumCandidatesPerVehicle", 1, int.MaxValue),
            (int)Integer(generation, "maximumNewRequestsPerVehicle", 0, int.MaxValue),
            maximumExplorationWorkUnits: PositiveInteger(generation, "maximumExplorationWorkUnits"),
            waitingRiderRelocation: relocationOptions);
    }

    internal static DeterministicCandidateSelectionExecutionBudget ReadExecutionBudget(JsonElement solver)
    {
        RequireFields(
            solver,
            "solverExecution",
            "adapterVersion",
            "maximumGenerationWorkUnits",
            "maximumValidationWorkUnits",
            "maximumSolverWorkUnits",
            "maximumSolverDeterministicTimeMicros",
            "randomSeed");
        if (Text(solver, "adapterVersion") != OrToolsCandidateSelectionSolver.AdapterVersion)
        {
            throw new InvalidDataException(
                $"adapterVersion must be {OrToolsCandidateSelectionSolver.AdapterVersion}.");
        }

        var solverBudget = DeterministicSolverBudget.Create(
            PositiveInteger(solver, "maximumSolverWorkUnits"),
            PositiveInteger(solver, "maximumSolverDeterministicTimeMicros"),
            Integer(solver, "randomSeed", 0, int.MaxValue));
        if (!solverBudget.IsSuccess)
        {
            throw new InvalidDataException(solverBudget.Failure!.Message);
        }

        var executionBudget = DeterministicCandidateSelectionExecutionBudget.Create(
            PositiveInteger(solver, "maximumGenerationWorkUnits"),
            PositiveInteger(solver, "maximumValidationWorkUnits"),
            solverBudget.Value!);
        if (!executionBudget.IsSuccess)
        {
            throw new InvalidDataException(executionBudget.Failure!.Message);
        }

        return executionBudget.Value!;
    }
}
