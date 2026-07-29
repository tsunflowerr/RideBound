using RideBound.Application.State;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;
using RideBound.Domain.Validation;
using RideBound.Domain.Vehicles;

namespace RideBound.Algorithms.Candidates;

public sealed class InsertionCandidateGenerator
{
    private readonly PhysicalPlanValidator _validator;
    private readonly CandidateScheduleEvaluator _scheduleEvaluator;

    public InsertionCandidateGenerator(
        PhysicalPlanValidator? validator = null,
        CandidateScheduleEvaluator? scheduleEvaluator = null)
    {
        _validator = validator ?? new PhysicalPlanValidator();
        _scheduleEvaluator =
            scheduleEvaluator ?? new CandidateScheduleEvaluator();
    }

    public CandidateGenerationResult Generate(
        OnlineState state,
        CandidateGenerationOptions options)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(options);

        if (state.TravelTimes is null)
        {
            return CandidateGenerationResult.Failure(
                new CandidateGenerationWitness(
                    CandidateGenerationFailureCodes.TravelSnapshotRequired,
                    "Candidate generation requires a travel snapshot.",
                    Dimension: "travelTimes"));
        }

        var pendingRequests = state.Run.Requests.Values
            .Where(value => value.Lifecycle == RequestLifecycle.Pending)
            .OrderBy(value => value.Id.Value, StringComparer.Ordinal)
            .ToArray();

        if (options.ExactSmallMode
            && pendingRequests.Length > options.MaximumNewRequestsPerVehicle)
        {
            return CandidateGenerationResult.Failure(
                new CandidateGenerationWitness(
                    CandidateGenerationFailureCodes.ExactSmallRequestBoundExceeded,
                    "Exact-small mode cannot omit pending requests above its " +
                    "published request bound.",
                    RequestId: pendingRequests[
                        options.MaximumNewRequestsPerVehicle].Id,
                    Dimension: "maximumNewRequestsPerVehicle"));
        }

        var requests = pendingRequests
            .Take(options.MaximumNewRequestsPerVehicle)
            .ToArray();
        var vehicleSets = new List<VehicleCandidateSet>();

        foreach (var vehicle in state.Run.Vehicles.Values.OrderBy(
                     value => value.Id.Value,
                     StringComparer.Ordinal))
        {
            var generated = GenerateForVehicle(
                state,
                vehicle,
                requests,
                options);

            if (generated.Witness is not null)
            {
                return CandidateGenerationResult.Failure(generated.Witness);
            }

            vehicleSets.Add(generated.Candidates!);
        }

        return CandidateGenerationResult.Success(vehicleSets.AsReadOnly());
    }

    private VehicleGenerationResult GenerateForVehicle(
        OnlineState state,
        VehicleState vehicle,
        IReadOnlyList<RideRequest> pendingRequests,
        CandidateGenerationOptions options)
    {
        var feasibleById =
            new Dictionary<string, InsertionCandidate>(StringComparer.Ordinal);
        var prunedById =
            new Dictionary<string, CandidatePruneWitness>(StringComparer.Ordinal);

        EvaluateRoute(
            state,
            vehicle,
            vehicle.Route,
            [],
            isNoOp: true,
            feasibleById,
            prunedById);

        var initialSuffix = vehicle.Route.MutableSuffix.ToArray();
        Explore(
            requestIndex: 0,
            pendingRequests,
            initialSuffix,
            [],
            state,
            vehicle,
            feasibleById,
            prunedById);

        var ordered = feasibleById.Values
            .OrderBy(value => value.CandidateId, StringComparer.Ordinal)
            .ToList();
        var wasTruncated = false;

        if (ordered.Count > options.MaximumCandidatesPerVehicle)
        {
            if (options.ExactSmallMode)
            {
                return VehicleGenerationResult.Failure(
                    new CandidateGenerationWitness(
                        CandidateGenerationFailureCodes
                            .ExactSmallCandidateCapExceeded,
                        "Exact-small candidate count exceeded the configured " +
                        "cap; no candidate was silently omitted.",
                        vehicle.Id,
                        Dimension: "maximumCandidatesPerVehicle"));
            }

            var noOp = ordered.SingleOrDefault(value => value.IsNoOp);
            var retained = ordered
                .Where(value => !value.IsNoOp)
                .Take(
                    options.MaximumCandidatesPerVehicle
                    - (noOp is null ? 0 : 1))
                .ToList();

            if (noOp is not null)
            {
                retained.Add(noOp);
            }

            ordered = retained
                .OrderBy(value => value.CandidateId, StringComparer.Ordinal)
                .ToList();
            wasTruncated = true;
        }

        return VehicleGenerationResult.Success(
            new VehicleCandidateSet(
                vehicle.Id,
                ordered.AsReadOnly(),
                prunedById.Values
                    .OrderBy(value => value.CandidateId, StringComparer.Ordinal)
                    .ToArray(),
                wasTruncated));
    }

    private void Explore(
        int requestIndex,
        IReadOnlyList<RideRequest> requests,
        IReadOnlyList<RouteStop> suffix,
        IReadOnlyList<RequestId> insertedRequestIds,
        OnlineState state,
        VehicleState vehicle,
        IDictionary<string, InsertionCandidate> feasibleById,
        IDictionary<string, CandidatePruneWitness> prunedById)
    {
        if (requestIndex == requests.Count)
        {
            if (insertedRequestIds.Count == 0)
            {
                return;
            }

            var routeResult = vehicle.Route.ReplaceMutableSuffix(suffix);

            if (!routeResult.IsSuccess)
            {
                return;
            }

            EvaluateRoute(
                state,
                vehicle,
                routeResult.Value!,
                insertedRequestIds,
                isNoOp: false,
                feasibleById,
                prunedById);
            return;
        }

        Explore(
            requestIndex + 1,
            requests,
            suffix,
            insertedRequestIds,
            state,
            vehicle,
            feasibleById,
            prunedById);

        var request = requests[requestIndex];
        var pickup = new RouteStop(
            CandidateIdentity.CreateStopId(request.Id, RouteStopKind.Pickup),
            request.OriginNodeId,
            RouteStopKind.Pickup,
            request.Id,
            new Duration(0));
        var dropOff = new RouteStop(
            CandidateIdentity.CreateStopId(request.Id, RouteStopKind.DropOff),
            request.DestinationNodeId,
            RouteStopKind.DropOff,
            request.Id,
            new Duration(0));

        for (var pickupIndex = 0; pickupIndex <= suffix.Count; pickupIndex++)
        {
            var withPickup = suffix.ToList();
            withPickup.Insert(pickupIndex, pickup);

            for (var dropIndex = pickupIndex + 1;
                 dropIndex <= withPickup.Count;
                 dropIndex++)
            {
                var withPair = withPickup.ToList();
                withPair.Insert(dropIndex, dropOff);
                Explore(
                    requestIndex + 1,
                    requests,
                    withPair,
                    insertedRequestIds.Append(request.Id).ToArray(),
                    state,
                    vehicle,
                    feasibleById,
                    prunedById);
            }
        }
    }

    private void EvaluateRoute(
        OnlineState state,
        VehicleState vehicle,
        RoutePlan route,
        IReadOnlyList<RequestId> insertedRequestIds,
        bool isNoOp,
        IDictionary<string, InsertionCandidate> feasibleById,
        IDictionary<string, CandidatePruneWitness> prunedById)
    {
        var orderedRequests = insertedRequestIds
            .OrderBy(value => value.Value, StringComparer.Ordinal)
            .ToArray();
        var candidateId = CandidateIdentity.Create(
            state,
            vehicle.Id,
            route,
            orderedRequests);
        var validation = _validator.Validate(
            new PhysicalValidationContext(
                state.Run,
                vehicle.Id,
                route,
                state.TravelTimes!,
                state.Run.SimulationTime));

        if (!validation.IsFeasible)
        {
            prunedById.TryAdd(
                candidateId,
                new CandidatePruneWitness(
                    candidateId,
                    vehicle.Id,
                    orderedRequests,
                    validation.Witness!.Code,
                    validation.Witness.Message,
                    validation.Witness));
            return;
        }

        var schedule = _scheduleEvaluator.Evaluate(
            state,
            vehicle,
            route,
            state.TravelTimes!,
            state.Run.SimulationTime);

        if (!schedule.IsSuccess)
        {
            prunedById.TryAdd(
                candidateId,
                new CandidatePruneWitness(
                    candidateId,
                    vehicle.Id,
                    orderedRequests,
                    schedule.Code!,
                    schedule.Message!));
            return;
        }

        feasibleById.TryAdd(
            candidateId,
            new InsertionCandidate(
                candidateId,
                vehicle.Id,
                route,
                orderedRequests,
                schedule.Schedule!,
                isNoOp));
    }

    private sealed record VehicleGenerationResult(
        VehicleCandidateSet? Candidates,
        CandidateGenerationWitness? Witness)
    {
        public static VehicleGenerationResult Success(
            VehicleCandidateSet candidates) =>
            new(candidates, null);

        public static VehicleGenerationResult Failure(
            CandidateGenerationWitness witness) =>
            new(null, witness);
    }
}
