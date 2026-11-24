using RideBound.Algorithms.Candidates;
using RideBound.Algorithms.Commitments;
using RideBound.Algorithms.Policies;
using RideBound.Application.Commitments;
using RideBound.Application.State;

namespace RideBound.Algorithms.Dispatch;

public sealed record DispatchStepResult(
    RollingCostDecision? Decision,
    OnlineState? StateToStage,
    IReadOnlyList<PromisePublication> Publications,
    RollingCostWitness? Witness);

/// <summary>Stages the validated policy decision together with exogenous breach records.</summary>
public sealed class DispatchStep
{
    private readonly SolverBackedRidePoolingPolicy _policy;
    private readonly ExogenousServiceQualityBreachBridge _breachBridge = new();

    public DispatchStep(SolverBackedRidePoolingPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        _policy = policy;
    }

    public DispatchStepResult Run(
        CommitmentMechanismContext context,
        CandidateGenerationOptions generationOptions,
        SolverBackedRidePoolingPolicyOptions policyOptions)
    {
        var solved = _policy.Decide(context, generationOptions, policyOptions);
        if (solved.Decision is not { } selected)
        {
            return new DispatchStepResult(null, null, [], solved.Witness);
        }

        var validated = selected.CommitmentValidation;
        var state = validated.ValidatedState!;
        if (selected.Decision.GenerationDiagnostics is { ExogenousServiceQualityBreaches.Count: > 0 } diagnostics)
        {
            var bridged = _breachBridge.Apply(
                context.ReducedState,
                state,
                diagnostics,
                selected.EffectivePolicies,
                context.StopDistances,
                context.SourceEventSequence);
            if (!bridged.IsSuccess)
            {
                return new DispatchStepResult(
                    null,
                    null,
                    [],
                    new RollingCostWitness(
                        "EXOGENOUS_BREACH_BRIDGE_FAILED",
                        bridged.Error!));
            }

            state = bridged.State!;
        }

        return new DispatchStepResult(selected.Decision, state, validated.Publications, null);
    }
}
