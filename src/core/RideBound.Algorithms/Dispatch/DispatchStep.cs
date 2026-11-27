using RideBound.Algorithms.Candidates;
using RideBound.Algorithms.Commitments;
using RideBound.Algorithms.Policies;
using RideBound.Algorithms.Shareability;
using RideBound.Application.Commitments;
using RideBound.Application.State;
using RideBound.Domain.Commitments;

namespace RideBound.Algorithms.Dispatch;

public sealed record DispatchStepResult(
    RollingCostDecision? Decision,
    OnlineState? StateToStage,
    IReadOnlyList<PromisePublication> Publications,
    RollingCostWitness? Witness);

/// <summary>Stages the validated decision together with exogenous breaches and optional Shadow classes.</summary>
public sealed class DispatchStep
{
    private readonly SolverBackedRidePoolingPolicy _policy;
    private readonly ExogenousServiceQualityBreachBridge _breachBridge = new();
    private readonly ShadowAllocator? _shadowAllocator;

    public DispatchStep(SolverBackedRidePoolingPolicy policy)
        : this(policy, null)
    {
    }

    public DispatchStep(SolverBackedRidePoolingPolicy policy, ShadowAllocator? shadowAllocator)
    {
        ArgumentNullException.ThrowIfNull(policy);
        _policy = policy;
        _shadowAllocator = shadowAllocator;
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

        if (_shadowAllocator is not null)
        {
            var opened = validated.Publications
                .Where(value => value.Entry.Kind == CommitmentLedgerEntryKind.InitialPromise)
                .Select(value => value.Entry.PublishedPromise.Projection.RequestId)
                .ToArray();
            try
            {
                state = _shadowAllocator.Allocate(state, opened, state.Run.SimulationTime);
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException
                or KeyNotFoundException or OverflowException)
            {
                return new DispatchStepResult(null, null, [],
                    new RollingCostWitness("SHADOW_ALLOCATION_FAILED", error.Message));
            }
        }

        return new DispatchStepResult(selected.Decision, state, validated.Publications, null);
    }
}
