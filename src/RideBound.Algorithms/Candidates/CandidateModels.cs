using RideBound.Application.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Routes;
using RideBound.Domain.Validation;

namespace RideBound.Algorithms.Candidates;

public sealed record ScheduledStop(
    StopId StopId,
    SimTime ArrivalTime,
    SimTime ServiceStartTime,
    SimTime DepartureTime);

public sealed record CandidateSchedule(
    IReadOnlyList<ScheduledStop> Stops,
    long OperationalCost);

public enum CandidateScheduleStrategy
{
    EarliestFeasible,
    OriginHoldRelocatedWait,
}

public enum CandidateRetentionStrategy
{
    LegacyAcceptedCountCostSlack,
    ServiceSetStabilityPortfolioV1,
}

public sealed record InsertionCandidate(
    string CandidateId,
    VehicleId VehicleId,
    RoutePlan Route,
    IReadOnlyList<RequestId> NewRequestIds,
    CandidateSchedule Schedule,
    bool IsNoOp,
    CandidateScheduleStrategy ScheduleStrategy =
        CandidateScheduleStrategy.EarliestFeasible,
    long RelocatedWaitMilliseconds = 0,
    long? CertifiedForwardSlackMilliseconds = null,
    RequestId? RepairedIncumbentRequestId = null);

public sealed record CandidatePruneWitness(
    string CandidateId,
    VehicleId VehicleId,
    IReadOnlyList<RequestId> NewRequestIds,
    string Code,
    string Message,
    PhysicalViolationWitness? PhysicalWitness = null,
    IReadOnlyList<CommitmentValidationWitness>? CommitmentWitnesses = null);

public sealed record VehicleCandidateSet(
    VehicleId VehicleId,
    IReadOnlyList<InsertionCandidate> Candidates,
    IReadOnlyList<CandidatePruneWitness> PrunedCandidates,
    bool WasTruncated,
    VehicleCandidateLoss? Loss = null);

public sealed record CandidateOmissionWitness(
    string Code,
    long Count,
    string StableDigest,
    string Message,
    VehicleId? VehicleId = null,
    IReadOnlyList<RequestId>? RequestIds = null,
    bool CountWasSaturated = false);

public sealed record VehicleCandidateLoss(
    long ExplorationWorkUnits,
    long EvaluatedCandidatePathCount,
    long UniqueFeasibleCandidateCountBeforeCap,
    long RetainedCandidateCount,
    long PhysicallyOrSchedulePrunedCount,
    long OmittedUnexpandedCandidatePathCount,
    long OmittedFeasibleCandidateCountByCap,
    bool WorkBudgetExhausted,
    bool CandidateCapApplied,
    bool OmissionCountWasSaturated = false,
    long EligibleRepairRequestCount = 0,
    long ConsideredRepairRequestCount = 0,
    long OmittedRepairRequestCount = 0,
    VehicleId? VehicleId = null);

/// <summary>
/// A service-quality deadline the vehicle's unchanged active route can no longer
/// meet under the current travel snapshot (ADR-045). It is attributed to traffic,
/// not to the decision about to be taken, and it never removes the safety no-op.
/// <see cref="ExogenousMilliseconds"/> doubles as the anti-laundering bound: no
/// candidate in this epoch may be worse than it on this dimension.
/// </summary>
public sealed record ExogenousServiceQualityBreach(
    VehicleId VehicleId,
    RequestId RequestId,
    string Code,
    string Dimension,
    long ContractualMilliseconds,
    long ExogenousMilliseconds);

public sealed record CandidateGenerationDiagnostics(
    long TotalPendingRequestCount,
    long ConsideredRequestCount,
    long OmittedRequestCount,
    IReadOnlyList<VehicleCandidateLoss> VehicleLosses,
    IReadOnlyList<CandidateOmissionWitness> Omissions,
    /// <summary>
    /// Exogenous service-quality breaches observed this epoch, ordered by
    /// vehicle, then request, then dimension. These are diagnostic: they never
    /// prune a candidate and never fail the epoch.
    /// </summary>
    IReadOnlyList<ExogenousServiceQualityBreach> ExogenousServiceQualityBreaches)
{

    public bool IsComplete => OmittedRequestCount == 0
        && VehicleLosses.All(
            loss => loss.OmittedUnexpandedCandidatePathCount == 0
                && loss.OmittedFeasibleCandidateCountByCap == 0
                && loss.OmittedRepairRequestCount == 0);
}

public sealed record CandidateGenerationResult
{
    private CandidateGenerationResult(
        IReadOnlyList<VehicleCandidateSet>? vehicleCandidates,
        CandidateGenerationDiagnostics? diagnostics,
        CandidateGenerationWitness? witness)
    {
        VehicleCandidates = vehicleCandidates;
        Diagnostics = diagnostics;
        Witness = witness;
    }

    public bool IsSuccess => VehicleCandidates is not null;

    public IReadOnlyList<VehicleCandidateSet>? VehicleCandidates { get; }

    public CandidateGenerationDiagnostics? Diagnostics { get; }

    public CandidateGenerationWitness? Witness { get; }

    public static CandidateGenerationResult Success(
        IReadOnlyList<VehicleCandidateSet> vehicleCandidates,
        CandidateGenerationDiagnostics diagnostics) =>
        new(vehicleCandidates, diagnostics, null);

    public static CandidateGenerationResult Failure(
        CandidateGenerationWitness witness) =>
        new(null, null, witness);
}

public sealed record CandidateGenerationWitness(
    string Code,
    string Message,
    VehicleId? VehicleId = null,
    RequestId? RequestId = null,
    string? Dimension = null);

public sealed record CandidateGenerationOptions
{
    public CandidateGenerationOptions(
        int maximumCandidatesPerVehicle,
        int maximumNewRequestsPerVehicle,
        bool exactSmallMode,
        CandidateScheduleStrategy scheduleStrategy =
            CandidateScheduleStrategy.EarliestFeasible,
        long maximumExplorationWorkUnits = 100_000,
        int maximumRepairRequestsConsideredPerVehicle = 0,
        CandidateRetentionStrategy retentionStrategy =
            CandidateRetentionStrategy.LegacyAcceptedCountCostSlack)
    {
        if (maximumCandidatesPerVehicle < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumCandidatesPerVehicle));
        }

        if (maximumNewRequestsPerVehicle < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumNewRequestsPerVehicle));
        }

        if (!Enum.IsDefined(scheduleStrategy))
        {
            throw new ArgumentOutOfRangeException(nameof(scheduleStrategy));
        }

        if (maximumExplorationWorkUnits is < 1
            or > DomainLimits.MaxCanonicalInteger)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumExplorationWorkUnits));
        }

        if (maximumRepairRequestsConsideredPerVehicle < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumRepairRequestsConsideredPerVehicle));
        }

        if (!Enum.IsDefined(retentionStrategy))
        {
            throw new ArgumentOutOfRangeException(nameof(retentionStrategy));
        }

        MaximumCandidatesPerVehicle = maximumCandidatesPerVehicle;
        MaximumNewRequestsPerVehicle = maximumNewRequestsPerVehicle;
        ExactSmallMode = exactSmallMode;
        ScheduleStrategy = scheduleStrategy;
        MaximumExplorationWorkUnits = maximumExplorationWorkUnits;
        MaximumRepairRequestsConsideredPerVehicle =
            maximumRepairRequestsConsideredPerVehicle;
        RetentionStrategy = retentionStrategy;
    }

    public int MaximumCandidatesPerVehicle { get; }

    public int MaximumNewRequestsPerVehicle { get; }

    public bool ExactSmallMode { get; }

    public CandidateScheduleStrategy ScheduleStrategy { get; }

    public long MaximumExplorationWorkUnits { get; }

    /// <summary>
    /// Zero disables the B4 neighborhood. A positive value bounds how many
    /// eligible waiting incumbents may each seed one atomic pair-repair move.
    /// </summary>
    public int MaximumRepairRequestsConsideredPerVehicle { get; }

    public CandidateRetentionStrategy RetentionStrategy { get; }

    public CandidateGenerationOptions WithRepairRequestCap(int maximumRequests) =>
        new(
            MaximumCandidatesPerVehicle,
            MaximumNewRequestsPerVehicle,
            ExactSmallMode,
            ScheduleStrategy,
            MaximumExplorationWorkUnits,
            maximumRequests,
            RetentionStrategy);

    public static CandidateGenerationOptions ExactSmall { get; } =
        new(
            maximumCandidatesPerVehicle: 100_000,
            maximumNewRequestsPerVehicle: 2,
            exactSmallMode: true);
}

public static class CandidateGenerationFailureCodes
{
    public const string ActiveRouteInfeasible = "ACTIVE_ROUTE_INFEASIBLE";
    public const string TravelSnapshotRequired = "TRAVEL_SNAPSHOT_REQUIRED";
    public const string ExactSmallRequestBoundExceeded =
        "EXACT_SMALL_REQUEST_BOUND_EXCEEDED";
    public const string ExactSmallCandidateCapExceeded =
        "EXACT_SMALL_CANDIDATE_CAP_EXCEEDED";
    public const string ExactSmallWorkCapExceeded =
        "EXACT_SMALL_WORK_CAP_EXCEEDED";
    public const string PlanVersionOverflow = "PLAN_VERSION_OVERFLOW";
    public const string ScheduleEvaluationFailed = "SCHEDULE_EVALUATION_FAILED";
    public const string SlackProfileFailed = "SLACK_PROFILE_FAILED";
    public const string ScheduleStrategyEquivalenceFailed =
        "SCHEDULE_STRATEGY_EQUIVALENCE_FAILED";
    public const string RequestBoundOmission = "REQUEST_BOUND_OMISSION";
    public const string WorkBoundOmission = "WORK_BOUND_OMISSION";
    public const string CandidateCapOmission = "CANDIDATE_CAP_OMISSION";
    public const string RepairRequestBoundOmission =
        "REPAIR_REQUEST_BOUND_OMISSION";
    public const string ExactSmallRepairRequestBoundExceeded =
        "EXACT_SMALL_REPAIR_REQUEST_BOUND_EXCEEDED";
}
