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
    bool IsNoOp);

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
    bool WasTruncated);

public sealed record CandidateGenerationResult
{
    private CandidateGenerationResult(
        IReadOnlyList<VehicleCandidateSet>? vehicleCandidates,
        CandidateGenerationWitness? witness)
    {
        VehicleCandidates = vehicleCandidates;
        Witness = witness;
    }

    public bool IsSuccess => VehicleCandidates is not null;

    public IReadOnlyList<VehicleCandidateSet>? VehicleCandidates { get; }

    public CandidateGenerationWitness? Witness { get; }

    public static CandidateGenerationResult Success(
        IReadOnlyList<VehicleCandidateSet> vehicleCandidates) =>
        new(vehicleCandidates, null);

    public static CandidateGenerationResult Failure(
        CandidateGenerationWitness witness) =>
        new(null, witness);
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
        bool exactSmallMode)
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

        MaximumCandidatesPerVehicle = maximumCandidatesPerVehicle;
        MaximumNewRequestsPerVehicle = maximumNewRequestsPerVehicle;
        ExactSmallMode = exactSmallMode;
    }

    public int MaximumCandidatesPerVehicle { get; }

    public int MaximumNewRequestsPerVehicle { get; }

    public bool ExactSmallMode { get; }

    public static CandidateGenerationOptions ExactSmall { get; } =
        new(
            maximumCandidatesPerVehicle: 100_000,
            maximumNewRequestsPerVehicle: 2,
            exactSmallMode: true);
}

public static class CandidateGenerationFailureCodes
{
    public const string TravelSnapshotRequired = "TRAVEL_SNAPSHOT_REQUIRED";
    public const string ExactSmallRequestBoundExceeded =
        "EXACT_SMALL_REQUEST_BOUND_EXCEEDED";
    public const string ExactSmallCandidateCapExceeded =
        "EXACT_SMALL_CANDIDATE_CAP_EXCEEDED";
    public const string PlanVersionOverflow = "PLAN_VERSION_OVERFLOW";
    public const string ScheduleEvaluationFailed = "SCHEDULE_EVALUATION_FAILED";
}
