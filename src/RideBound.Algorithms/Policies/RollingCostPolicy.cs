using RideBound.Algorithms.Candidates;
using RideBound.Algorithms.Commitments;
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
        CandidateGenerationOptions options,
        CommitmentCandidateFilter? commitmentFilter = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(options);

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

        var candidates = commitmentFilter is null
            ? generated.VehicleCandidates!
            : commitmentFilter.Filter(state, generated.VehicleCandidates!);
        var selection = _selector.Select(candidates);

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
            candidates);

        return applied.IsSuccess
            ? applied
            : RollingCostDecisionResult.Failure(applied.Witness!);
    }

    private RollingCostWitness? ValidateSelection(
        OnlineState state,
        FleetSelection selection)
    {
        foreach (var plan in selection.VehiclePlans)
        {
            var validation = _validator.Validate(
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
        IReadOnlyList<VehicleCandidateSet> generated)
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

            if (relatedPrunes.Length == 0)
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
