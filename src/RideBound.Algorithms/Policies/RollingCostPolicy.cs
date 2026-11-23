using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using RideBound.Algorithms.Candidates;
using RideBound.Application.Optimization;
using RideBound.Application.State;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Runs;
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
                new RollingCostWitness(
                    RollingCostFailureCodes.CandidateGenerationFailed,
                    generated.Witness!.Message,
                    generated.Witness.VehicleId,
                    generated.Witness.RequestId,
                    Dimension: generated.Witness.Dimension));
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

        var validationFailure = ValidateSelection(
            state,
            selection.Selection!);

        if (validationFailure is not null)
        {
            return RollingCostDecisionResult.Failure(validationFailure);
        }

        var applied = ApplySelection(
            state,
            selection.Selection!,
            generated.VehicleCandidates!,
            generated.Diagnostics!.IsComplete);

        return applied.IsSuccess
            ? applied
            : RollingCostDecisionResult.Failure(applied.Witness!);
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
            var empty = ApplySelection(
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

    private static DomainResult<CandidateSelectionPreSolveAccounting> CreateAccounting(
        CandidateGenerationDiagnostics diagnostics,
        DeterministicCandidateSelectionExecutionBudget budget)
    {
        var generationWork = 0L;
        foreach (var loss in diagnostics.VehicleLosses)
        {
            if (loss.ExplorationWorkUnits > budget.MaximumGenerationWorkUnits - generationWork)
            {
                return DomainResult<CandidateSelectionPreSolveAccounting>.Fail(
                    CandidateSelectionFailureCodes.InvalidExecutionAccounting,
                    "Fleet generation work exceeds the configured execution budget.",
                    dimension: "preSolveAccounting");
            }

            generationWork += loss.ExplorationWorkUnits;
        }

        var omittedCount = 0L;
        var saturated = false;
        foreach (var omission in diagnostics.Omissions)
        {
            saturated |= omission.CountWasSaturated;
            if (omission.Count > DomainLimits.MaxCanonicalInteger - omittedCount)
            {
                omittedCount = DomainLimits.MaxCanonicalInteger;
                saturated = true;
            }
            else
            {
                omittedCount += omission.Count;
            }
        }

        return CandidateSelectionPreSolveAccounting.Create(
            budget,
            generationWork,
            consumedValidationWorkUnits: 0,
            omittedCount,
            omittedCount == 0 ? null : AggregateOmissionDigest(diagnostics.Omissions),
            saturated);
    }

    private static string AggregateOmissionDigest(IReadOnlyList<CandidateOmissionWitness> omissions)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData("RideBound.Wp4OmissionAccounting.v1\0"u8);
        foreach (var omission in omissions
                     .OrderBy(value => value.Code, StringComparer.Ordinal)
                     .ThenBy(value => value.StableDigest, StringComparer.Ordinal))
        {
            Append(omission.Code);
            Append(omission.Count.ToString(CultureInfo.InvariantCulture));
            Append(omission.StableDigest);
            Append(omission.CountWasSaturated ? "1" : "0");
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());

        void Append(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            Span<byte> length = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
            hash.AppendData(length);
            hash.AppendData(bytes);
        }
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
            var failure = policy.ValidateSelection(state, selection);
            if (failure is not null)
            {
                return CandidateSelectionValidationResult.Invalid(failure.Code, failure.Message);
            }

            var applied = ApplySelection(
                state,
                selection,
                generation.VehicleCandidates!,
                generation.Diagnostics!.IsComplete);
            Decision = applied.Decision;
            return applied.IsSuccess
                ? CandidateSelectionValidationResult.Valid()
                : CandidateSelectionValidationResult.Invalid(applied.Witness!.Code, applied.Witness.Message);
        }
    }

    private RollingCostWitness? ValidateSelection(
        OnlineState state,
        FleetSelection selection)
    {
        foreach (var plan in selection.VehiclePlans)
        {
            var isUnchanged = plan.Candidate.IsNoOp
                && state.Run.Vehicles.TryGetValue(plan.VehicleId, out var vehicle)
                && ReferenceEquals(vehicle.Route, plan.Candidate.Route);
            var validation = isUnchanged
                ? _validator.ValidateWithExogenousRelief(
                    state.Run,
                    plan.VehicleId,
                    plan.Candidate.Route,
                    state.TravelTimes!,
                    state.Run.SimulationTime)
                : _validator.Validate(
                    new PhysicalValidationContext(
                        state.Run,
                        plan.VehicleId,
                        plan.Candidate.Route,
                        state.TravelTimes!,
                        state.Run.SimulationTime));

            if (!validation.IsFeasible)
            {
                return new RollingCostWitness(
                    RollingCostFailureCodes.SelectedCandidateInvalid,
                    validation.Witness!.Message,
                    plan.VehicleId,
                    validation.Witness.RequestId,
                    plan.Candidate.CandidateId,
                    validation.Witness.Dimension);
            }
        }

        return null;
    }

    private static RollingCostDecisionResult ApplySelection(
        OnlineState state,
        FleetSelection selection,
        IReadOnlyList<VehicleCandidateSet> generated,
        bool searchIsComplete)
    {
        var run = state.Run;
        var selectedRequests = selection.VehiclePlans
            .SelectMany(value => value.Candidate.NewRequestIds)
            .ToHashSet();
        var feasibleRequests = generated
            .SelectMany(value => value.Candidates)
            .SelectMany(value => value.NewRequestIds)
            .ToHashSet();
        var pruned = generated
            .SelectMany(value => value.PrunedCandidates)
            .OrderBy(value => value.CandidateId, StringComparer.Ordinal)
            .ToArray();

        foreach (var plan in selection.VehiclePlans.OrderBy(
                     value => value.VehicleId.Value,
                     StringComparer.Ordinal))
        {
            if (!plan.Candidate.IsNoOp)
            {
                var route = run.UpdateVehicleRoute(
                    plan.VehicleId,
                    plan.Candidate.Route);

                if (!route.IsSuccess)
                {
                    return ApplyFailure(
                        route.Failure!,
                        plan.VehicleId,
                        plan.Candidate.CandidateId);
                }

                run = route.Value!;
            }

            foreach (var requestId in plan.Candidate.NewRequestIds.OrderBy(
                         value => value.Value,
                         StringComparer.Ordinal))
            {
                var accepted = run.AcceptRequest(requestId, plan.VehicleId);

                if (!accepted.IsSuccess)
                {
                    return ApplyFailure(
                        accepted.Failure!,
                        plan.VehicleId,
                        plan.Candidate.CandidateId,
                        requestId);
                }

                run = accepted.Value!;
            }
        }

        var actions = new List<RequestDecisionAction>();
        var pending = state.Run.Requests.Values
            .Where(value => value.Lifecycle == RequestLifecycle.Pending)
            .OrderBy(value => value.Id.Value, StringComparer.Ordinal)
            .ToArray();

        foreach (var request in pending)
        {
            if (selectedRequests.Contains(request.Id))
            {
                var selectedPlan = selection.VehiclePlans.Single(
                    value => value.Candidate.NewRequestIds.Contains(request.Id));
                actions.Add(
                    new RequestDecisionAction(
                        request.Id,
                        RequestDecisionOutcome.Accepted,
                        RollingCostReasonCodes.Accepted,
                        selectedPlan.VehicleId,
                        selectedPlan.Candidate.CandidateId));
                continue;
            }

            if (feasibleRequests.Contains(request.Id))
            {
                actions.Add(
                    new RequestDecisionAction(
                        request.Id,
                        RequestDecisionOutcome.Deferred,
                        RollingCostReasonCodes.FleetSelectionConflict));
                continue;
            }

            var relatedPrunes = pruned
                .Where(value => value.NewRequestIds.Contains(request.Id))
                .ToArray();

            if (!searchIsComplete || relatedPrunes.Length == 0)
            {
                actions.Add(
                    new RequestDecisionAction(
                        request.Id,
                        RequestDecisionOutcome.Deferred,
                        "CANDIDATE_BOUND"));
                continue;
            }

            var rejected = run.RejectRequest(request.Id);

            if (!rejected.IsSuccess)
            {
                return ApplyFailure(rejected.Failure!, requestId: request.Id);
            }

            run = rejected.Value!;
            actions.Add(
                new RequestDecisionAction(
                    request.Id,
                    RequestDecisionOutcome.Rejected,
                    FindRejectionReason(request.Id, relatedPrunes)));
        }

        return RollingCostDecisionResult.Success(
            new RollingCostDecision(
                state with { Run = run },
                selection.VehiclePlans,
                actions.AsReadOnly(),
                pruned,
                selection.AcceptedRequestCount,
                selection.OperationalCost));
    }

    private static string FindRejectionReason(
        RequestId requestId,
        IReadOnlyList<CandidatePruneWitness> pruned)
    {
        var witness = pruned
            .Where(value => value.NewRequestIds.Contains(requestId))
            .OrderBy(
                value => value.NewRequestIds.Count == 1 ? 0 : 1)
            .ThenBy(value => value.CandidateId, StringComparer.Ordinal)
            .FirstOrDefault();

        return witness?.Code ?? RollingCostReasonCodes.NoFeasibleInsertion;
    }

    private static RollingCostDecisionResult ApplyFailure(
        DomainFailure failure,
        VehicleId? vehicleId = null,
        string? candidateId = null,
        RequestId? requestId = null) =>
        RollingCostDecisionResult.Failure(
            new RollingCostWitness(
                RollingCostFailureCodes.DecisionApplyFailed,
                failure.Message,
                vehicleId,
                requestId,
                candidateId,
                failure.Dimension));
}
