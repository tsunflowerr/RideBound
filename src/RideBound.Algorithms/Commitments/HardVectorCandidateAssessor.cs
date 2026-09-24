using System.Collections.Frozen;
using RideBound.Algorithms.Candidates;
using RideBound.Application.Commitments;
using RideBound.Application.State;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;

namespace RideBound.Algorithms.Commitments;

/// <param name="IsForcedReference">
/// True only for a safety no-op kept by forced-reference recovery although a
/// commitment gate rejected it. Its utilization is reported as zero: the overrun is
/// counted on the forced-reference objective level and recorded as a typed breach.
/// </param>
public sealed record HardVectorCandidateAssessment(
    string CandidateId,
    long WorstHardUtilizationPartsPerMillion,
    CommitmentVector DecisionInducedRevision,
    bool HasApplicableHardLimit,
    CommitmentVector? WarningExcess = null,
    bool HasApplicableWarning = false,
    bool IsForcedReference = false);

public sealed record HardVectorCandidateAssessmentBatch(
    IReadOnlyList<VehicleCandidateSet> FeasibleCandidateSets,
    IReadOnlyDictionary<string, HardVectorCandidateAssessment> Assessments)
{
    /// <summary>
    /// Vehicles whose safety no-op was kept as a forced reference. Always empty
    /// unless forced-reference recovery was requested.
    /// </summary>
    public IReadOnlySet<VehicleId> ForcedReferenceVehicles { get; init; } =
        FrozenSet<VehicleId>.Empty;
}

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

    /// <param name="requireSafetyNoOp">
    /// Set by a caller whose selection model needs exactly one no-op per vehicle.
    /// Then a vehicle that loses its no-op fails here with the no-op's own
    /// rejection, and an emptied vehicle reports the no-op's rejection. Callers
    /// that do not need the no-op keep the behaviour they had before.
    /// </param>
    /// <param name="forcedReferenceRecovery">
    /// Exploratory recovery (off by default; requires <paramref name="requireSafetyNoOp"/>).
    /// A safety no-op rejected by the commitment validator is validated again with its
    /// vehicle named as forced-reference. If that validation accepts it, which happens
    /// only when every rejection was a deadline or budget gate, the no-op is kept
    /// as a forced option instead of failing the vehicle. Any other rejection keeps
    /// the fail-closed behaviour.
    /// </param>
    public HardVectorCandidateAssessmentResult AssessAndFilter(
        CommitmentMechanismContext context,
        IReadOnlyList<VehicleCandidateSet> rawCandidateSets,
        ICommitmentWarningProfileProvider? warningProfiles = null,
        bool requireSafetyNoOp = false,
        bool forcedReferenceRecovery = false)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(rawCandidateSets);

        if (forcedReferenceRecovery && !requireSafetyNoOp)
        {
            throw new ArgumentException(
                "Forced-reference recovery keeps the safety no-op, so it requires it.",
                nameof(forcedReferenceRecovery));
        }

        var outputSets = new List<VehicleCandidateSet>(rawCandidateSets.Count);
        var assessments = new List<HardVectorCandidateAssessment>();
        var forcedVehicles = new List<VehicleId>();

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
                        InitialPromiseTrigger: context.InitialPromiseTrigger,
                        CollectAllCommitmentWitnesses:
                            context.CollectAllCommitmentWitnesses));

                if (!validation.IsValid)
                {
                    var witness = validation.Witnesses[0];
                    var prune = new CandidatePruneWitness(
                        candidate.CandidateId,
                        set.VehicleId,
                        candidate.NewRequestIds,
                        witness.Code,
                        witness.Message,
                        CommitmentWitnesses: validation.Witnesses);
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

            // The safety no-op is the one candidate whose rejection says why a vehicle
            // lost its fallback. Identify it only when the set is well formed (exactly
            // one no-op); otherwise the selection model reports the malformed set.
            var noOps = requireSafetyNoOp
                ? set.Candidates.Where(value => value.IsNoOp).ToArray()
                : Array.Empty<InsertionCandidate>();
            var noOpPrune = noOps.Length == 1
                ? hardPruned.FirstOrDefault(
                    value => value.CandidateId == noOps[0].CandidateId)
                : null;

            // Only a validator rejection can be forced; a no-op that could not even be
            // applied to the reduced state is a physical failure and stays fatal.
            if (forcedReferenceRecovery
                && noOpPrune is not null
                && hardValidationWitnesses.ContainsKey(noOpPrune.CandidateId))
            {
                var forced = AssessForcedNoOp(
                    context,
                    noOps[0],
                    set.VehicleId,
                    warningProfiles);

                if (forced.Witness is not null)
                {
                    return Failure(forced.Witness, noOps[0], set.VehicleId);
                }

                if (forced.Assessment is not null)
                {
                    retained.Add(noOps[0]);
                    retained.Sort(
                        (left, right) => StringComparer.Ordinal.Compare(
                            left.CandidateId,
                            right.CandidateId));
                    pruned.Remove(noOpPrune);
                    hardPruned.Remove(noOpPrune);
                    assessments.Add(forced.Assessment);
                    forcedVehicles.Add(set.VehicleId);
                    noOpPrune = null;
                }
            }

            if (retained.Count == 0)
            {
                // Report the no-op's own rejection when there is one, so the cause of
                // an empty set is attributable; the ordinal first candidate is usually
                // not the no-op, because candidate ids are content hashes.
                var first = noOpPrune ?? hardPruned
                    .OrderBy(value => value.CandidateId, StringComparer.Ordinal)
                    .FirstOrDefault();
                var citation = noOpPrune is not null
                    ? "No-op rejection"
                    : "First rejection";
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
                              $"vehicle. {citation}: {first.Message}",
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

            if (noOpPrune is not null)
            {
                // A rule removed the safety no-op but kept another candidate. The
                // caller's selection model needs exactly one no-op per vehicle, so
                // this state used to die later as a generic model-mapping failure.
                // Failing here with the no-op's own rejection changes no successful
                // run of such a caller. The cited rejection may come from any stage.
                var noOpValidation = hardValidationWitnesses.TryGetValue(
                    noOpPrune.CandidateId,
                    out var noOpWitness)
                    ? noOpWitness
                    : null;
                return HardVectorCandidateAssessmentResult.Failure(
                    new CommitmentAssessmentWitness(
                        CommitmentFailureCodes.SafetyNoOpRejected,
                        "C1 rejected the safety no-op while other candidates survived. " +
                        $"No-op rejection: {noOpPrune.Message}",
                        noOpPrune.CandidateId,
                        set.VehicleId,
                        noOpValidation?.RequestId,
                        noOpValidation?.Dimension,
                        noOpPrune.Code,
                        noOpValidation?.Before,
                        noOpValidation?.After,
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
                    StringComparer.Ordinal))
            {
                ForcedReferenceVehicles = forcedVehicles.ToFrozenSet(),
            });
    }

    /// <summary>
    /// Validates the rejected no-op again with its vehicle named as forced-reference.
    /// Returns no assessment when the validator still rejects it (the rejection was not
    /// a deadline or budget gate), and a witness only for a ranking failure.
    /// </summary>
    private ForcedNoOpResult AssessForcedNoOp(
        CommitmentMechanismContext context,
        InsertionCandidate noOp,
        VehicleId vehicleId,
        ICommitmentWarningProfileProvider? warningProfiles)
    {
        var updated = CandidateStateApplicator.Apply(
            context.ReducedState.Run,
            noOp);

        if (!updated.IsSuccess)
        {
            return ForcedNoOpResult.NotForcible;
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
                ScopedVehicleId: vehicleId,
                InitialPromiseTrigger: context.InitialPromiseTrigger,
                CollectAllCommitmentWitnesses:
                    context.CollectAllCommitmentWitnesses,
                ForcedReferenceVehicles: new[] { vehicleId }.ToFrozenSet()));

        if (!validation.IsValid || validation.ForcedExemptions.Count == 0)
        {
            return ForcedNoOpResult.NotForcible;
        }

        var revision = AggregateDecisionRevision(validation.Publications);

        if (!revision.IsSuccess)
        {
            return ForcedNoOpResult.Failure(revision.Witness!);
        }

        var utilization = CalculateWorstUtilization(
            validation.ValidatedState!,
            context.Policies,
            vehicleId,
            context.InitialPromiseTrigger,
            forcedReference: true);

        if (!utilization.IsSuccess)
        {
            return ForcedNoOpResult.Failure(utilization.Witness!);
        }

        var warning = CalculateWarningExcess(
            validation.ValidatedState!,
            context.Policies,
            warningProfiles,
            vehicleId,
            context.InitialPromiseTrigger);

        if (!warning.IsSuccess)
        {
            return ForcedNoOpResult.Failure(warning.Witness!);
        }

        return ForcedNoOpResult.Forced(
            new HardVectorCandidateAssessment(
                noOp.CandidateId,
                utilization.PartsPerMillion,
                revision.Value!,
                utilization.HasApplicableHardLimit,
                warning.Value,
                warning.HasApplicableWarning,
                IsForcedReference: true));
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
        InitialPromiseTrigger initialPromiseTrigger,
        bool forcedReference = false)
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

                if (forcedReference)
                {
                    // A forced vehicle may be charged past its limit. Its utilization is
                    // not ranked, so one forced vehicle cannot flatten the fleet maximum.
                    continue;
                }

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

    private sealed record ForcedNoOpResult(
        HardVectorCandidateAssessment? Assessment,
        CommitmentAssessmentWitness? Witness)
    {
        public static ForcedNoOpResult NotForcible { get; } = new(null, null);

        public static ForcedNoOpResult Forced(
            HardVectorCandidateAssessment assessment) => new(assessment, null);

        public static ForcedNoOpResult Failure(
            CommitmentAssessmentWitness witness) => new(null, witness);
    }

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
