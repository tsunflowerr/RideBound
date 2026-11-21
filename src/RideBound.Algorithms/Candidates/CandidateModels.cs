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

public sealed record InsertionCandidate(
    string CandidateId,
    VehicleId VehicleId,
    RoutePlan Route,
    IReadOnlyList<RequestId> NewRequestIds,
    CandidateSchedule Schedule,
    bool IsNoOp,
    long? CertifiedForwardSlackMilliseconds = null);

public sealed record CandidatePruneWitness(
    string CandidateId,
    VehicleId VehicleId,
    IReadOnlyList<RequestId> NewRequestIds,
    string Code,
    string Message,
    PhysicalViolationWitness? PhysicalWitness = null);

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
    bool OmissionCountWasSaturated = false);

/// <summary>
/// A service-quality deadline the vehicle's unchanged active route can no longer
/// meet under the current travel snapshot. It is attributed to traffic,
/// not to the decision about to be taken, and it never removes the safety no-op.
/// <see cref="ExogenousMilliseconds"/> is the exact no-op observation. Every
/// changed candidate stays strictly contractual; only the unchanged
/// safety route may use this observation as its relaxed bound.
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
    IReadOnlyList<ExogenousServiceQualityBreach> ExogenousServiceQualityBreaches)
{
    public bool IsComplete => OmittedRequestCount == 0
        && VehicleLosses.All(
            loss => loss.OmittedUnexpandedCandidatePathCount == 0
                && loss.OmittedFeasibleCandidateCountByCap == 0);
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
        bool exactSmallMode = false,
        long maximumExplorationWorkUnits = 100_000)
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

        if (maximumExplorationWorkUnits is < 1
            or > DomainLimits.MaxCanonicalInteger)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumExplorationWorkUnits));
        }

        MaximumCandidatesPerVehicle = maximumCandidatesPerVehicle;
        MaximumNewRequestsPerVehicle = maximumNewRequestsPerVehicle;
        MaximumExplorationWorkUnits = maximumExplorationWorkUnits;
        ExactSmallMode = exactSmallMode;
    }

    public int MaximumCandidatesPerVehicle { get; }

    public int MaximumNewRequestsPerVehicle { get; }

    public long MaximumExplorationWorkUnits { get; }

    public bool ExactSmallMode { get; }

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
    public const string PlanVersionOverflow = "PLAN_VERSION_OVERFLOW";
    public const string ScheduleEvaluationFailed = "SCHEDULE_EVALUATION_FAILED";
    public const string SlackProfileFailed = "SLACK_PROFILE_FAILED";
    public const string RequestBoundOmission = "REQUEST_BOUND_OMISSION";
    public const string WorkBoundOmission = "WORK_BOUND_OMISSION";
    public const string CandidateCapOmission = "CANDIDATE_CAP_OMISSION";
    public const string ExactSmallRequestBoundExceeded =
        "EXACT_SMALL_REQUEST_BOUND_EXCEEDED";
    public const string ExactSmallCandidateCapExceeded =
        "EXACT_SMALL_CANDIDATE_CAP_EXCEEDED";
}
