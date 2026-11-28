using RideBound.Algorithms.Candidates;
using RideBound.Application.Optimization;
using RideBound.Application.State;
using RideBound.Domain.Common;
using RideBound.Domain.Validation;

namespace RideBound.Algorithms.Policies;

public sealed class RollingCostPolicy
{
    private readonly InsertionCandidateGenerator _generator;
    private readonly CandidateFleetSelector _selector;
    private readonly PhysicalPlanValidator _validator;

    public RollingCostPolicy(
        InsertionCandidateGenerator? generator = null,
        CandidateFleetSelector? selector = null,
        PhysicalPlanValidator? validator = null)
    {
        _generator = generator ?? new InsertionCandidateGenerator();
        _selector = selector ?? new CandidateFleetSelector();
        _validator = validator ?? new PhysicalPlanValidator();
    }

    public RollingCostDecisionResult Decide(
        OnlineState state,
        CandidateGenerationOptions options) =>
        DecideCore(state, options, null);

    public RollingCostDecisionResult DecideWithSolver(
        OnlineState state,
        CandidateGenerationOptions options,
        ICandidateSelectionSolver solver,
        DeterministicCandidateSelectionExecutionBudget executionBudget)
    {
        ArgumentNullException.ThrowIfNull(solver);
        ArgumentNullException.ThrowIfNull(executionBudget);
        return DecideCore(state, options, (solver, executionBudget));
    }

    private RollingCostDecisionResult DecideCore(
        OnlineState state,
        CandidateGenerationOptions options,
        (ICandidateSelectionSolver Solver, DeterministicCandidateSelectionExecutionBudget Budget)? configuration)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(options);

        if (options.WaitingRiderRelocation is not null)
        {
            return RollingCostDecisionResult.Failure(
                RollingCostWitness.UncoupledRelocation());
        }

        var generated = _generator.Generate(state, options);

        if (!generated.IsSuccess)
        {
            return RollingCostDecisionResult.Failure(
                CommitmentMechanismPolicies.GenerationFailure(generated.Witness!));
        }

        if (configuration is { } configured)
        {
            return Solve(state, generated, configured.Solver, configured.Budget);
        }

        var selection = _selector.Select(generated.VehicleCandidates!);

        if (!selection.IsSuccess)
        {
            return RollingCostDecisionResult.Failure(selection.Witness!);
        }

        return CommitmentMechanismPolicies.ValidateAndApply(
            state, selection.Selection!, generated.VehicleCandidates!, generated.Diagnostics!.IsComplete, _validator);
    }

    private RollingCostDecisionResult Solve(
        OnlineState state,
        CandidateGenerationResult generation,
        ICandidateSelectionSolver solver,
        DeterministicCandidateSelectionExecutionBudget budget)
    {
        var accounting = CreateAccounting(generation.Diagnostics!, budget);
        if (!accounting.IsSuccess)
        {
            return RollingCostDecisionResult.Failure(
                new RollingCostWitness(
                    accounting.Failure!.Code,
                    accounting.Failure.Message,
                    Dimension: accounting.Failure.Dimension));
        }

        var candidates = generation.VehicleCandidates!;
        if (candidates.Count == 0)
        {
            var empty = SelectionApplier.ApplySelection(
                state,
                new FleetSelection([], 0, 0),
                candidates,
                generation.Diagnostics!.IsComplete);
            return empty.IsSuccess
                ? RollingCostDecisionResult.Success(
                    empty.Decision! with { GenerationDiagnostics = generation.Diagnostics })
                : empty;
        }

        var validator = new SolverFleetValidator(this, state, generation);
        var selected = new SolverBackedFleetSelector(solver).Select(
            candidates,
            budget,
            accounting.Value!,
            validator);
        if (!selected.IsSuccess)
        {
            return RollingCostDecisionResult.Failure(selected.Witness!);
        }

        // Return the proposed state that passed the executor's independent validation.
        return RollingCostDecisionResult.Success(
            validator.Decision! with
            {
                SelectionExecution = selected.Selection!.Execution,
                GenerationDiagnostics = generation.Diagnostics,
            });
    }

    internal static DomainResult<CandidateSelectionPreSolveAccounting> CreateAccounting(
        CandidateGenerationDiagnostics diagnostics,
        DeterministicCandidateSelectionExecutionBudget budget,
        long validationWork = 0)
    {
        var generationWork = 0L;
        var workUnits = diagnostics.VehicleLosses.Select(loss => loss.ExplorationWorkUnits)
            .Concat((diagnostics.RelocationLosses ?? []).Select(loss => loss.WorkUnits));
        foreach (var work in workUnits)
        {
            if (work > budget.MaximumGenerationWorkUnits - generationWork)
            {
                return DomainResult<CandidateSelectionPreSolveAccounting>.Fail(
                    CandidateSelectionFailureCodes.InvalidExecutionAccounting,
                    "Fleet generation work exceeds the configured execution budget.",
                    dimension: "preSolveAccounting");
            }

            generationWork += work;
        }

        return CandidateSelectionPreSolveAccounting.Create(budget, generationWork, validationWork);
    }

    private sealed class SolverFleetValidator(
        RollingCostPolicy policy,
        OnlineState state,
        CandidateGenerationResult generation) : IFleetSelectionValidator
    {
        public RollingCostDecision? Decision { get; private set; }

        public CandidateSelectionValidationResult Validate(FleetSelection selection)
        {
            Decision = null;
            var applied = CommitmentMechanismPolicies.ValidateAndApply(
                state, selection, generation.VehicleCandidates!, generation.Diagnostics!.IsComplete, policy._validator);
            Decision = applied.Decision;
            return applied.IsSuccess
                ? CandidateSelectionValidationResult.Valid()
                : CandidateSelectionValidationResult.Invalid(applied.Witness!.Code, applied.Witness.Message);
        }
    }
}
