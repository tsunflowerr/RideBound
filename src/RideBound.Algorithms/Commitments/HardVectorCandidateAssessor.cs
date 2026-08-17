using System.Collections.Frozen;
using RideBound.Algorithms.Candidates;
using RideBound.Application.Commitments;
using RideBound.Application.State;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;

namespace RideBound.Algorithms.Commitments;

public sealed record HardVectorCandidateAssessment(
    string CandidateId,
    long WorstHardUtilizationPartsPerMillion,
    CommitmentVector DecisionInducedRevision,
    bool HasApplicableHardLimit,
    CommitmentVector? WarningExcess = null,
    bool HasApplicableWarning = false);

public sealed record HardVectorCandidateAssessmentBatch(
    IReadOnlyList<VehicleCandidateSet> FeasibleCandidateSets,
    IReadOnlyDictionary<string, HardVectorCandidateAssessment> Assessments);

public sealed record HardVectorCandidateAssessmentResult
{
    private HardVectorCandidateAssessmentResult(
        HardVectorCandidateAssessmentBatch? batch,
        CommitmentAssessmentWitness? witness)
    {
        Batch = batch;
        Witness = witness;
    }

    public bool IsSuccess => Batch is not null;

    public HardVectorCandidateAssessmentBatch? Batch { get; }

    public CommitmentAssessmentWitness? Witness { get; }

    public static HardVectorCandidateAssessmentResult Success(
        HardVectorCandidateAssessmentBatch batch) => new(batch, null);

    public static HardVectorCandidateAssessmentResult Failure(
        CommitmentAssessmentWitness witness) => new(null, witness);
}

/// <summary>
/// Performs the C1 hard gate and ranking assessment in the same validator pass.
/// Raw candidates are never added or mutated; only validator-invalid candidates
/// are removed. Normalization is exact ceiling PPM and never decides feasibility.
/// </summary>
public sealed class HardVectorCandidateAssessor
{
    public const long PartsPerMillion = 1_000_000;

    private readonly CommitmentDecisionValidator _validator;

    public HardVectorCandidateAssessor(
        CommitmentDecisionValidator? validator = null)
    {
        _validator = validator ?? new CommitmentDecisionValidator();
    }

    public HardVectorCandidateAssessmentResult AssessAndFilter(
        CommitmentMechanismContext context,
        IReadOnlyList<VehicleCandidateSet> rawCandidateSets,
        ICommitmentWarningProfileProvider? warningProfiles = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(rawCandidateSets);
        var outputSets = new List<VehicleCandidateSet>(rawCandidateSets.Count);
        var assessments = new List<HardVectorCandidateAssessment>();

        foreach (var set in rawCandidateSets.OrderBy(
                     value => value.VehicleId.Value,
                     StringComparer.Ordinal))
        {
            var retained = new List<InsertionCandidate>();
            var pruned = set.PrunedCandidates.ToList();
            var hardPruned = new List<CandidatePruneWitness>();
            var hardValidationWitnesses =
                new Dictionary<string, CommitmentValidationWitness>(
                    StringComparer.Ordinal);

            foreach (var candidate in set.Candidates.OrderBy(
                         value => value.CandidateId,
                         StringComparer.Ordinal))
            {
                var updated = CandidateStateApplicator.Apply(
                    context.ReducedState.Run,
                    candidate);

                if (!updated.IsSuccess)
                {
                    var prune = new CandidatePruneWitness(
                        candidate.CandidateId,
                        set.VehicleId,
                        candidate.NewRequestIds,
                        updated.Failure!.Code,
                        updated.Failure.Message);
                    pruned.Add(prune);
                    hardPruned.Add(prune);
                    continue;
                }

                var validation = _validator.Validate(
                    new CommitmentValidationContext(
                        context.BeforeEventState,
                        context.ReducedState,
                        context.ReducedState with { Run = updated.Value! },
                        context.Policies,
                        context.StopDistances,
                        context.PublicationScope,
                        context.SourceEventSequence,
                        RevisionReasonCode: "C1_HARD_VECTOR",
                        ScopedVehicleId: set.VehicleId,
                        InitialPromiseTrigger: context.InitialPromiseTrigger));

                if (!validation.IsValid)
                {
                    var witness = validation.Witnesses[0];
                    var prune = new CandidatePruneWitness(
                        candidate.CandidateId,
                        set.VehicleId,
                        candidate.NewRequestIds,
                        witness.Code,
                        witness.Message);
                    pruned.Add(prune);
                    hardPruned.Add(prune);
                    hardValidationWitnesses.Add(
                        candidate.CandidateId,
                        witness);
                    continue;
                }

                var revision = AggregateDecisionRevision(validation.Publications);

                if (!revision.IsSuccess)
                {
                    return Failure(
                        revision.Witness!,
                        candidate,
                        set.VehicleId);
                }

                var utilization = CalculateWorstUtilization(
                    validation.ValidatedState!,
                    context.Policies,
                    set.VehicleId,
                    context.InitialPromiseTrigger);

                if (!utilization.IsSuccess)
                {
                    return Failure(
                        utilization.Witness!,
                        candidate,
                        set.VehicleId);
                }

                var warning = CalculateWarningExcess(
                    validation.ValidatedState!,
                    context.Policies,
                    warningProfiles,
                    set.VehicleId,
                    context.InitialPromiseTrigger);

                if (!warning.IsSuccess)
                {
                    return Failure(
                        warning.Witness!,
                        candidate,
                        set.VehicleId);
                }

                retained.Add(candidate);
                assessments.Add(
                    new HardVectorCandidateAssessment(
                        candidate.CandidateId,
                        utilization.PartsPerMillion,
                        revision.Value!,
                        utilization.HasApplicableHardLimit,
                        warning.Value,
                        warning.HasApplicableWarning));
            }

            if (retained.Count == 0)
            {
                var first = hardPruned
                    .OrderBy(value => value.CandidateId, StringComparer.Ordinal)
                    .FirstOrDefault();
                var firstValidation = first is not null
                    && hardValidationWitnesses.TryGetValue(
                        first.CandidateId,
                        out var value)
                        ? value
                        : null;
                return HardVectorCandidateAssessmentResult.Failure(
                    new CommitmentAssessmentWitness(
                        CommitmentFailureCodes.VehicleHasNoFeasibleCandidate,
                        first is null
                            ? "C1 reached a vehicle without any generated " +
                              "candidate; even the safety no-op was absent."
                            : "C1 rejected every generated candidate for this " +
                              $"vehicle. First rejection: {first.Message}",
                        first?.CandidateId,
                        set.VehicleId,
                        firstValidation?.RequestId,
                        firstValidation?.Dimension,
                        first?.Code,
                        firstValidation?.Before,
                        firstValidation?.After,
                        set.Candidates.Count,
                        hardPruned.Count));
            }

            outputSets.Add(
                new VehicleCandidateSet(
                    set.VehicleId,
                    retained.AsReadOnly(),
                    pruned
                        .OrderBy(value => value.CandidateId, StringComparer.Ordinal)
                        .ToArray(),
                    set.WasTruncated,
                    set.Loss));
        }

        return HardVectorCandidateAssessmentResult.Success(
            new HardVectorCandidateAssessmentBatch(
                outputSets.AsReadOnly(),
                assessments.ToFrozenDictionary(
                    value => value.CandidateId,
                    StringComparer.Ordinal)));
    }

    private static VectorResult AggregateDecisionRevision(
        IReadOnlyList<PromisePublication> publications)
    {
        var aggregate = CommitmentVector.Zero;

        foreach (var publication in publications)
        {
            var added = aggregate.Add(
                publication.Entry.Deltas.DecisionInduced);

            if (!added.IsSuccess)
            {
                return VectorResult.Failure(
                    new CommitmentAssessmentWitness(
                        added.Failure!.Code,
                        added.Failure.Message,
                        Dimension: added.Failure.Dimension));
            }

            aggregate = added.Value!;
        }

        return VectorResult.Success(aggregate);
    }

    private static UtilizationResult CalculateWorstUtilization(
        OnlineState state,
        ICommitmentPolicyProvider policies,
        VehicleId scopedVehicleId,
        InitialPromiseTrigger initialPromiseTrigger)
    {
        long worst = 0;
        var hasLimit = false;

        foreach (var request in state.Run.Requests.Values
                     .Where(
                         value => value.IsAcceptedActive
                             && value.AssignedVehicleId == scopedVehicleId)
                     .OrderBy(value => value.Id.Value, StringComparer.Ordinal))
        {
            if (initialPromiseTrigger == InitialPromiseTrigger.BookingConfirmation
                && request.Lifecycle == RequestLifecycle.Accepted
                && !state.Commitments.Histories.ContainsKey(request.Id))
            {
                continue;
            }

            if (!policies.TryGetPolicy(request.CommitmentPolicyId, out var policy)
                || !StringComparer.Ordinal.Equals(
                    request.CommitmentPolicyId,
                    policy.PolicyId)
                || !state.Commitments.Histories.TryGetValue(
                    request.Id,
                    out var history))
            {
                return UtilizationResult.Failure(
                    new CommitmentAssessmentWitness(
                        "COMMITMENT_POLICY_OR_LEDGER_NOT_FOUND",
                        "An active request requires an exact policy and ledger " +
                        "history before hard utilization can be ranked.",
                        RequestId: request.Id,
                        Dimension: "commitmentPolicyId"));
            }

            var phase = ToPhase(request.Lifecycle);

            foreach (var dimension in CommitmentDimensionVocabulary.Ordered)
            {
                var configured = policy.Limits[dimension];

                if ((configured.ApplicablePhases & phase) == 0
                    || configured.HardLimit is not long hardLimit)
                {
                    continue;
                }

                hasLimit = true;
                var value = history.Current.BudgetAfter.Get(dimension);

                if (hardLimit == 0)
                {
                    if (value != 0)
                    {
                        return UtilizationResult.Failure(
                            new CommitmentAssessmentWitness(
                                CommitmentFailureCodes.BudgetExceeded,
                                "A zero hard limit has non-zero validated usage.",
                                RequestId: request.Id,
                                Dimension: CommitmentDimensionVocabulary
                                    .ToProtocolValue(dimension)));
                    }

                    worst = PartsPerMillion;
                    continue;
                }

                worst = Math.Max(
                    worst,
                    CeilingPartsPerMillion(value, hardLimit));
            }
        }

        return UtilizationResult.Success(worst, hasLimit);
    }

    public static long CeilingPartsPerMillion(long value, long hardLimit)
    {
        if (value < 0
            || hardLimit < 0
            || value > hardLimit
            || value > DomainLimits.MaxCanonicalInteger
            || hardLimit > DomainLimits.MaxCanonicalInteger)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        if (hardLimit == 0)
        {
            return PartsPerMillion;
        }

        var scaled = (UInt128)(ulong)value
            * (UInt128)(ulong)PartsPerMillion;
        var quotient = scaled / (UInt128)(ulong)hardLimit;
        var remainder = scaled % (UInt128)(ulong)hardLimit;
        var ceiling = quotient + (remainder == 0 ? 0u : 1u);
        return checked((long)ceiling);
    }

    private static WarningResult CalculateWarningExcess(
        OnlineState state,
        ICommitmentPolicyProvider policies,
        ICommitmentWarningProfileProvider? warningProfiles,
        VehicleId scopedVehicleId,
        InitialPromiseTrigger initialPromiseTrigger)
    {
        if (warningProfiles is null)
        {
            return WarningResult.Success(CommitmentVector.Zero, false);
        }

        var values = new long[CommitmentDimensionVocabulary.Ordered.Count];
        var hasWarning = false;

        foreach (var request in state.Run.Requests.Values
                     .Where(
                         value => value.IsAcceptedActive
                             && value.AssignedVehicleId == scopedVehicleId)
                     .OrderBy(value => value.Id.Value, StringComparer.Ordinal))
        {
            if (initialPromiseTrigger == InitialPromiseTrigger.BookingConfirmation
                && request.Lifecycle == RequestLifecycle.Accepted
                && !state.Commitments.Histories.ContainsKey(request.Id))
            {
                continue;
            }

            if (!policies.TryGetPolicy(request.CommitmentPolicyId, out var policy)
                || !warningProfiles.TryGetProfile(
                    request.CommitmentPolicyId,
                    out var warningProfile)
                || !StringComparer.Ordinal.Equals(
                    warningProfile.PolicyId,
                    policy.PolicyId)
                || !state.Commitments.Histories.TryGetValue(
                    request.Id,
                    out var history))
            {
                return WarningResult.Failure(
                    new CommitmentAssessmentWitness(
                        "COMMITMENT_WARNING_PROFILE_NOT_FOUND",
                        "C2 requires an exact warning profile, policy and ledger " +
                        "history for every active request.",
                        RequestId: request.Id,
                        Dimension: "commitmentPolicyId"));
            }

            var phase = ToPhase(request.Lifecycle);

            for (var index = 0;
                 index < CommitmentDimensionVocabulary.Ordered.Count;
                 index++)
            {
                var dimension = CommitmentDimensionVocabulary.Ordered[index];
                var warning = warningProfile.Limits[dimension].WarningLimit;

                if (warning is not long warningLimit)
                {
                    continue;
                }

                var hard = policy.Limits[dimension];

                if (hard.HardLimit is not long hardLimit
                    || warningLimit > hardLimit)
                {
                    return WarningResult.Failure(
                        new CommitmentAssessmentWitness(
                            "INVALID_COMMITMENT_WARNING_LIMIT",
                            "An enabled warning requires a finite hard limit and " +
                            "cannot exceed it.",
                            RequestId: request.Id,
                            Dimension: CommitmentDimensionVocabulary
                                .ToProtocolValue(dimension)));
                }

                if ((hard.ApplicablePhases & phase) == 0)
                {
                    continue;
                }

                hasWarning = true;
                var usage = history.Current.BudgetAfter.Get(dimension);
                var excess = usage > warningLimit ? usage - warningLimit : 0;

                if (values[index] > DomainLimits.MaxCanonicalInteger - excess)
                {
                    return WarningResult.Failure(
                        new CommitmentAssessmentWitness(
                            CommitmentFailureCodes.VectorOverflow,
                            "Aggregate warning excess exceeds the canonical range.",
                            RequestId: request.Id,
                            Dimension: CommitmentDimensionVocabulary
                                .ToProtocolValue(dimension)));
                }

                values[index] += excess;
            }
        }

        return WarningResult.Success(
            new CommitmentVector(
                values[0],
                values[1],
                values[2],
                values[3],
                values[4],
                values[5],
                values[6],
                values[7],
                values[8],
                values[9]),
            hasWarning);
    }

    private static CommitmentPhase ToPhase(RequestLifecycle lifecycle) =>
        lifecycle switch
        {
            RequestLifecycle.Accepted => CommitmentPhase.Accepted,
            RequestLifecycle.WaitingPickup => CommitmentPhase.WaitingPickup,
            RequestLifecycle.Onboard => CommitmentPhase.Onboard,
            _ => CommitmentPhase.None,
        };

    private static HardVectorCandidateAssessmentResult Failure(
        CommitmentAssessmentWitness witness,
        InsertionCandidate candidate,
        VehicleId vehicleId) =>
        HardVectorCandidateAssessmentResult.Failure(
            witness with
            {
                CandidateId = candidate.CandidateId,
                VehicleId = vehicleId,
            });

    private sealed record VectorResult(
        CommitmentVector? Value,
        CommitmentAssessmentWitness? Witness)
    {
        public bool IsSuccess => Value is not null;

        public static VectorResult Success(CommitmentVector value) =>
            new(value, null);

        public static VectorResult Failure(CommitmentAssessmentWitness witness) =>
            new(null, witness);
    }

    private sealed record UtilizationResult(
        long PartsPerMillion,
        bool HasApplicableHardLimit,
        CommitmentAssessmentWitness? Witness)
    {
        public bool IsSuccess => Witness is null;

        public static UtilizationResult Success(
            long partsPerMillion,
            bool hasApplicableHardLimit) =>
            new(partsPerMillion, hasApplicableHardLimit, null);

        public static UtilizationResult Failure(
            CommitmentAssessmentWitness witness) =>
            new(0, false, witness);
    }

    private sealed record WarningResult(
        CommitmentVector? Value,
        bool HasApplicableWarning,
        CommitmentAssessmentWitness? Witness)
    {
        public bool IsSuccess => Value is not null;

        public static WarningResult Success(
            CommitmentVector value,
            bool hasApplicableWarning) =>
            new(value, hasApplicableWarning, null);

        public static WarningResult Failure(
            CommitmentAssessmentWitness witness) =>
            new(null, false, witness);
    }
}
