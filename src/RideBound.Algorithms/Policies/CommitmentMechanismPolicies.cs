using RideBound.Algorithms.Candidates;
using RideBound.Application.State;
using RideBound.Domain.Validation;

namespace RideBound.Algorithms.Policies;

internal static class CommitmentMechanismPolicies
{
    public static RollingCostDecisionResult ValidateAndApply(
        OnlineState state,
        FleetSelection selection,
        IReadOnlyList<VehicleCandidateSet> candidates,
        bool searchIsComplete,
        PhysicalPlanValidator validator)
    {
        var witness = SelectionApplier.ValidateSelection(state, selection, validator);
        return witness is null
            ? SelectionApplier.ApplySelection(state, selection, candidates, searchIsComplete)
            : RollingCostDecisionResult.Failure(witness);
    }

    public static RollingCostWitness GenerationFailure(CandidateGenerationWitness witness) =>
        new(
            RollingCostFailureCodes.CandidateGenerationFailed,
            witness.Message,
            witness.VehicleId,
            witness.RequestId,
            Dimension: witness.Dimension);
}
