using RideBound.Algorithms.Candidates;
using RideBound.Algorithms.Commitments;
using RideBound.Application.State;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Validation;

namespace RideBound.Algorithms.Policies;

internal static class SelectionApplier
{
    private static (InsertionCandidate? Donor, InsertionCandidate? Receiver, RollingCostWitness? Witness)
        SelectedRelocation(OnlineState state, FleetSelection selection)
    {
        var moves = selection.VehiclePlans
            .Where(plan => plan.Candidate.RelocationRole is not null
                || plan.Candidate.RelocatedRequestId is not null)
            .ToArray();
        if (moves.Length == 0)
        {
            return (null, null, null);
        }

        var donor = moves.FirstOrDefault(plan => plan.Candidate.RelocationRole == RelocationRole.Donor);
        var receiver = moves.FirstOrDefault(plan => plan.Candidate.RelocationRole == RelocationRole.Receiver);
        if (moves.Length != 2
            || donor is null || receiver is null
            || donor.VehicleId != donor.Candidate.VehicleId
            || receiver.VehicleId != receiver.Candidate.VehicleId
            || donor.VehicleId == receiver.VehicleId
            || donor.Candidate.IsNoOp || receiver.Candidate.IsNoOp
            || receiver.Candidate.NewRequestIds.Count != 0
            || donor.Candidate.RelocatedRequestId is not RequestId rider
            || receiver.Candidate.RelocatedRequestId != rider
            || !state.Run.Requests.TryGetValue(rider, out var request)
            || request.Lifecycle != RequestLifecycle.WaitingPickup
            || request.AssignedVehicleId != donor.VehicleId)
        {
            var first = moves[0];
            return (null, null, new RollingCostWitness(
                RollingCostFailureCodes.SelectedCandidateInvalid,
                "Select exactly one donor and one receiver of the same waiting rider.",
                first.VehicleId,
                first.Candidate.RelocatedRequestId,
                first.Candidate.CandidateId,
                "relocation"));
        }

        return (donor.Candidate, receiver.Candidate, null);
    }

    internal static RollingCostWitness? ValidateSelection(
        OnlineState state,
        FleetSelection selection,
        PhysicalPlanValidator validator)
    {
        var (donor, receiver, witness) = SelectedRelocation(state, selection);
        if (witness is not null)
        {
            return witness;
        }

        var relocation = donor is null
            ? null
            : new WaitingRiderRelocation(
                donor.RelocatedRequestId!.Value, donor.VehicleId, receiver!.VehicleId);

        foreach (var plan in selection.VehiclePlans)
        {
            var isUnchanged = plan.Candidate.IsNoOp
                && state.Run.Vehicles.TryGetValue(plan.VehicleId, out var vehicle)
                && ReferenceEquals(vehicle.Route, plan.Candidate.Route);
            var validation = isUnchanged
                ? validator.ValidateWithExogenousRelief(
                    state.Run,
                    plan.VehicleId,
                    plan.Candidate.Route,
                    state.TravelTimes!,
                    state.Run.SimulationTime)
                : validator.Validate(
                    new PhysicalValidationContext(
                        state.Run,
                        plan.VehicleId,
                        plan.Candidate.Route,
                        state.TravelTimes!,
                        state.Run.SimulationTime,
                        Relocation: relocation));

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

    internal static RollingCostDecisionResult ApplySelection(
        OnlineState state,
        FleetSelection selection,
        IReadOnlyList<VehicleCandidateSet> generated,
        bool searchIsComplete)
    {
        var (donor, receiver, witness) = SelectedRelocation(state, selection);
        if (witness is not null)
        {
            return RollingCostDecisionResult.Failure(witness);
        }

        var run = state.Run;
        if (donor is not null)
        {
            var paired = CandidateStateApplicator.ApplyRelocationPair(run, donor, receiver!);
            if (!paired.IsSuccess)
            {
                return ApplyFailure(
                    paired.Failure!, donor.VehicleId, donor.CandidateId, donor.RelocatedRequestId);
            }

            run = paired.Value!;
        }
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
            if (plan.Candidate.RelocationRole is not null)
            {
                continue;
            }

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
