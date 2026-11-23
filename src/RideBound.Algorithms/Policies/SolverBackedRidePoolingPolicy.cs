using RideBound.Algorithms.Candidates;
using RideBound.Algorithms.Commitments;
using RideBound.Application.Commitments;
using RideBound.Application.Optimization;
using RideBound.Domain.Common;
using RideBound.Domain.Validation;

namespace RideBound.Algorithms.Policies;

public enum RidePoolingPolicyKind
{
    RideBoundHardVector,
}

public static class RidePoolingPolicyRegistry
{
    public const string RideBoundHardVector = "ridebound-hard-vector";

    public static bool TryParse(
        string? policyId,
        out RidePoolingPolicyKind policyKind)
    {
        policyKind = policyId switch
        {
            RideBoundHardVector => RidePoolingPolicyKind.RideBoundHardVector,
            _ => default,
        };
        return policyId is RideBoundHardVector;
    }

    public static string ToPolicyId(RidePoolingPolicyKind policyKind) =>
        policyKind switch
        {
            RidePoolingPolicyKind.RideBoundHardVector => RideBoundHardVector,
            _ => throw new ArgumentOutOfRangeException(nameof(policyKind)),
        };
}

public sealed record SolverBackedRidePoolingPolicyOptions
{
    public SolverBackedRidePoolingPolicyOptions(
        RidePoolingPolicyKind policyKind,
        DeterministicCandidateSelectionExecutionBudget executionBudget)
    {
        ArgumentNullException.ThrowIfNull(executionBudget);

        if (!Enum.IsDefined(policyKind))
        {
            throw new ArgumentOutOfRangeException(nameof(policyKind));
        }

        PolicyKind = policyKind;
        ExecutionBudget = executionBudget;
    }

    public RidePoolingPolicyKind PolicyKind { get; }

    public DeterministicCandidateSelectionExecutionBudget ExecutionBudget { get; }
}

/// <summary>
/// The proposed fleet decision and the validator's corresponding state and
/// promise publications. The caller controls when to publish the result.
/// </summary>
public sealed record SolverBackedPolicyDecision(
    RollingCostDecision Decision,
    ICommitmentPolicyProvider EffectivePolicies,
    CommitmentDecisionValidationResult CommitmentValidation);

public sealed record SolverBackedPolicyDecisionResult
{
    private SolverBackedPolicyDecisionResult(
        SolverBackedPolicyDecision? decision,
        RollingCostWitness? witness)
    {
        Decision = decision;
        Witness = witness;
    }

    public bool IsSuccess => Decision is not null;

    public SolverBackedPolicyDecision? Decision { get; }

    public RollingCostWitness? Witness { get; }

    public static SolverBackedPolicyDecisionResult Success(
        SolverBackedPolicyDecision decision) => new(decision, null);

    public static SolverBackedPolicyDecisionResult Failure(
        RollingCostWitness witness) => new(null, witness);
}

/// <summary>
/// Production policy path for C1. It generates the shared physical
/// candidate set once, performs only the named mechanism gate/assessment, maps
/// the exact hierarchy to the solver port, and independently validates every
/// solver or fallback fleet selection before returning it to Runner.
/// </summary>
public sealed class SolverBackedRidePoolingPolicy
{
    private readonly InsertionCandidateGenerator _generator;
    private readonly HardVectorCandidateAssessor _hardAssessor;
    private readonly SolverBackedFleetSelector _selector;
    private readonly CommitmentDecisionValidator _commitmentValidator;
    private readonly PhysicalPlanValidator _physicalValidator;

    public SolverBackedRidePoolingPolicy(
        ICandidateSelectionSolver solver,
        InsertionCandidateGenerator? generator = null,
        HardVectorCandidateAssessor? hardAssessor = null,
        CommitmentDecisionValidator? commitmentValidator = null,
        PhysicalPlanValidator? physicalValidator = null)
    {
        ArgumentNullException.ThrowIfNull(solver);
        _generator = generator ?? new InsertionCandidateGenerator();
        _hardAssessor = hardAssessor ?? new HardVectorCandidateAssessor();
        _selector = new SolverBackedFleetSelector(solver);
        _commitmentValidator = commitmentValidator
            ?? new CommitmentDecisionValidator();
        _physicalValidator = physicalValidator ?? new PhysicalPlanValidator();
    }

    public SolverBackedPolicyDecisionResult Decide(
        CommitmentMechanismContext context,
        CandidateGenerationOptions generationOptions,
        SolverBackedRidePoolingPolicyOptions policyOptions)
    {
        ArgumentNullException.ThrowIfNull(context);

        ArgumentNullException.ThrowIfNull(generationOptions);
        ArgumentNullException.ThrowIfNull(policyOptions);

        var generated = _generator.Generate(
            context.ReducedState,
            generationOptions);

        if (!generated.IsSuccess)
        {
            return SolverBackedPolicyDecisionResult.Failure(
                new RollingCostWitness(
                    RollingCostFailureCodes.CandidateGenerationFailed,
                    generated.Witness!.Message,
                    generated.Witness.VehicleId,
                    generated.Witness.RequestId,
                    Dimension: generated.Witness.Dimension));
        }

        var validationWork = generated.VehicleCandidates!.Sum(set => (long)set.Candidates.Count);
        var accounting = RollingCostPolicy.CreateAccounting(
            generated.Diagnostics!, policyOptions.ExecutionBudget, validationWork);
        if (!accounting.IsSuccess)
        {
            return SolverBackedPolicyDecisionResult.Failure(
                new RollingCostWitness(
                    accounting.Failure!.Code,
                    accounting.Failure.Message,
                    Dimension: accounting.Failure.Dimension));
        }

        var assessed = _hardAssessor.AssessAndFilter(
            context, generated.VehicleCandidates!, requireSafetyNoOp: true);
        if (!assessed.IsSuccess)
        {
            return AssessmentFailure(assessed.Witness!);
        }

        var candidates = assessed.Batch!.FeasibleCandidateSets;
        var validator = new FullFleetValidator(
            context, candidates, generated.Diagnostics!.IsComplete,
            _commitmentValidator, _physicalValidator);

        if (candidates.Count == 0)
        {
            var validation = validator.Validate(new FleetSelection([], 0, 0));
            return validation.IsValid
                ? Success(validator, generated.Diagnostics, null, context.Policies)
                : SolverBackedPolicyDecisionResult.Failure(
                    new RollingCostWitness(validation.ReasonCode!, validation.Message!));
        }

        var selected = _selector.SelectCoupled(
            candidates, policyOptions.ExecutionBudget, accounting.Value!, validator);
        return selected.IsSuccess
            ? Success(validator, generated.Diagnostics, selected.Selection!.Execution, context.Policies)
            : SolverBackedPolicyDecisionResult.Failure(selected.Witness!);
    }

    private static SolverBackedPolicyDecisionResult Success(
        FullFleetValidator validator,
        CandidateGenerationDiagnostics diagnostics,
        CandidateSelectionExecutionResult? execution,
        ICommitmentPolicyProvider policies) =>
        SolverBackedPolicyDecisionResult.Success(
            new SolverBackedPolicyDecision(
                validator.Decision! with
                {
                    SelectionExecution = execution,
                    GenerationDiagnostics = diagnostics,
                },
                policies,
                validator.CommitmentValidation!));

    private static SolverBackedPolicyDecisionResult AssessmentFailure(
        CommitmentAssessmentWitness witness) =>
        SolverBackedPolicyDecisionResult.Failure(
            new RollingCostWitness(
                "COMMITMENT_ASSESSMENT_FAILED",
                witness.Message,
                witness.VehicleId,
                witness.RequestId,
                witness.CandidateId,
                witness.Dimension));

    private sealed class FullFleetValidator(
        CommitmentMechanismContext context,
        IReadOnlyList<VehicleCandidateSet> candidates,
        bool searchIsComplete,
        CommitmentDecisionValidator commitmentValidator,
        PhysicalPlanValidator physicalValidator) : IFleetSelectionValidator
    {
        public RollingCostDecision? Decision { get; private set; }
        public CommitmentDecisionValidationResult? CommitmentValidation { get; private set; }

        public CandidateSelectionValidationResult Validate(FleetSelection selection)
        {
            Decision = null;
            CommitmentValidation = null;
            var physicalFailure = SelectionApplier.ValidateSelection(
                context.ReducedState,
                selection,
                physicalValidator);

            if (physicalFailure is not null)
            {
                return CandidateSelectionValidationResult.Invalid(
                    physicalFailure.Code,
                    physicalFailure.Message);
            }

            var applied = SelectionApplier.ApplySelection(
                context.ReducedState,
                selection,
                candidates,
                searchIsComplete);

            if (!applied.IsSuccess)
            {
                return CandidateSelectionValidationResult.Invalid(
                    applied.Witness!.Code,
                    applied.Witness.Message);
            }

            var validated = commitmentValidator.Validate(
                new CommitmentValidationContext(
                    context.BeforeEventState,
                    context.ReducedState,
                    applied.Decision!.ProposedState,
                    context.Policies,
                    context.StopDistances,
                    context.PublicationScope,
                    context.SourceEventSequence,
                    RevisionReasonCode: "WP4_SOLVER_SELECTION"));
            if (!validated.IsValid)
            {
                return CandidateSelectionValidationResult.Invalid(
                    validated.Witnesses[0].Code,
                    validated.Witnesses[0].Message);
            }

            Decision = applied.Decision;
            CommitmentValidation = validated;
            return CandidateSelectionValidationResult.Valid();
        }
    }
}
