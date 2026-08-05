using System.Numerics;
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
    private readonly ForwardSlackProfileCache _slackCache;
    private readonly OriginHoldCandidateTransformer _originHoldTransformer;
    private readonly WaitingIncumbentRepairSeedBuilder _repairSeedBuilder;

    public InsertionCandidateGenerator(
        PhysicalPlanValidator? validator = null,
        CandidateScheduleEvaluator? scheduleEvaluator = null,
        ForwardSlackProfileCache? slackCache = null,
        OriginHoldCandidateTransformer? originHoldTransformer = null,
        WaitingIncumbentRepairSeedBuilder? repairSeedBuilder = null)
    {
        _validator = validator ?? new PhysicalPlanValidator();
        _slackCache = slackCache ?? new ForwardSlackProfileCache(
            new ForwardSlackProfileBuilder(scheduleEvaluator));
        _originHoldTransformer =
            originHoldTransformer ?? new OriginHoldCandidateTransformer();
        _repairSeedBuilder =
            repairSeedBuilder ?? new WaitingIncumbentRepairSeedBuilder();
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
            .OrderBy(value => value.LatestPickup.Milliseconds)
            .ThenBy(value => value.ArrivalTime.Milliseconds)
            .ThenBy(value => value.Id.Value, StringComparer.Ordinal)
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
        var omittedRequests = pendingRequests
            .Skip(options.MaximumNewRequestsPerVehicle)
            .ToArray();
        var vehicleSets = new List<VehicleCandidateSet>();
        var omissions = new List<CandidateOmissionWitness>();

        if (omittedRequests.Length > 0)
        {
            omissions.Add(
                new CandidateOmissionWitness(
                    CandidateGenerationFailureCodes.RequestBoundOmission,
                    omittedRequests.Length,
                    CandidateIdentity.CreateOmissionDigest(
                        omittedRequests.Select(request => request.Id.Value)),
                    "Pending requests beyond the deterministic " +
                    "(latestPickup, arrivalTime, requestId) bound were omitted.",
                    RequestIds: Array.AsReadOnly(
                        omittedRequests.Select(request => request.Id).ToArray())));
        }

        foreach (var vehicle in state.Run.Vehicles.Values.OrderBy(
                     value => value.Id.Value,
                     StringComparer.Ordinal))
        {
            var generated = GenerateForVehicle(
                state,
                vehicle,
                requests,
                options,
                omittedRequests.Length > 0);

            if (generated.Witness is not null)
            {
                return CandidateGenerationResult.Failure(generated.Witness);
            }

            vehicleSets.Add(generated.Candidates!);
            omissions.AddRange(generated.Omissions);
        }

        return CandidateGenerationResult.Success(
            vehicleSets.AsReadOnly(),
            new CandidateGenerationDiagnostics(
                pendingRequests.Length,
                requests.Length,
                omittedRequests.Length,
                vehicleSets.Select(set => set.Loss!).ToArray(),
                omissions.AsReadOnly()));
    }

    private VehicleGenerationResult GenerateForVehicle(
        OnlineState state,
        VehicleState vehicle,
        IReadOnlyList<RideRequest> pendingRequests,
        CandidateGenerationOptions options,
        bool requestsWereOmitted)
    {
        var feasibleById =
            new Dictionary<string, InsertionCandidate>(StringComparer.Ordinal);
        var prunedById =
            new Dictionary<string, CandidatePruneWitness>(StringComparer.Ordinal);
        var repair = options.MaximumRepairRequestsConsideredPerVehicle == 0
            ? new WaitingIncumbentRepairSeedResult([], [], [], [])
            : _repairSeedBuilder.Build(
                state,
                vehicle,
                options.MaximumRepairRequestsConsideredPerVehicle);

        if (options.ExactSmallMode && repair.OmittedRequestIds.Count > 0)
        {
            return VehicleGenerationResult.Failure(
                new CandidateGenerationWitness(
                    CandidateGenerationFailureCodes
                        .ExactSmallRepairRequestBoundExceeded,
                    "Exact-small B4 repair cannot omit an eligible waiting " +
                    "incumbent above its explicit request cap.",
                    vehicle.Id,
                    repair.OmittedRequestIds[0],
                    "maximumRepairRequestsConsideredPerVehicle"));
        }

        EvaluateRoute(
            state,
            vehicle,
            vehicle.Route,
            [],
            isNoOp: true,
            repairedIncumbentRequestId: null,
            options,
            feasibleById,
            prunedById);

        var exploration = ExploreBestFirst(
            state,
            vehicle,
            pendingRequests,
            repair.Seeds,
            options,
            feasibleById,
            prunedById);

        if (exploration.Witness is not null)
        {
            return VehicleGenerationResult.Failure(exploration.Witness);
        }

        var beforeCap = feasibleById.Values.ToArray();
        var ranked = beforeCap
            .OrderByDescending(value => value.NewRequestIds.Count)
            .ThenBy(value => value.Schedule.OperationalCost)
            .ThenBy(value => value.CertifiedForwardSlackMilliseconds is null ? 0 : 1)
            .ThenByDescending(value => value.CertifiedForwardSlackMilliseconds ?? 0)
            .ThenBy(value => value.CandidateId, StringComparer.Ordinal)
            .ToList();
        var omittedByCap = Array.Empty<InsertionCandidate>();

        if (ranked.Count > options.MaximumCandidatesPerVehicle)
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

            var noOp = ranked.Single(value => value.IsNoOp);
            var retained = ranked
                .Where(value => !value.IsNoOp)
                .Take(options.MaximumCandidatesPerVehicle - 1)
                .ToList();
            retained.Add(noOp);
            var retainedIds = retained
                .Select(candidate => candidate.CandidateId)
                .ToHashSet(StringComparer.Ordinal);
            omittedByCap = ranked
                .Where(candidate => !retainedIds.Contains(candidate.CandidateId))
                .ToArray();
            ranked = retained;
        }

        var omissions = new List<CandidateOmissionWitness>();

        if (repair.OmittedRequestIds.Count > 0)
        {
            omissions.Add(
                new CandidateOmissionWitness(
                    CandidateGenerationFailureCodes.RepairRequestBoundOmission,
                    repair.OmittedRequestIds.Count,
                    CandidateIdentity.CreateOmissionDigest(
                        repair.OmittedRequestIds.Select(id => id.Value)),
                    "Eligible same-vehicle waiting incumbents beyond the " +
                    "explicit B4 repair-request cap were omitted.",
                    vehicle.Id,
                    repair.OmittedRequestIds));
        }

        if (exploration.OmittedCandidatePathCount > 0)
        {
            omissions.Add(
                new CandidateOmissionWitness(
                    CandidateGenerationFailureCodes.WorkBoundOmission,
                    exploration.OmittedCandidatePathCount,
                    CandidateIdentity.CreateOmissionDigest(
                        exploration.OmittedSubtreeIds),
                    "Deterministic work cap left raw candidate exploration " +
                    "paths unexpanded; their feasibility is unknown.",
                    vehicle.Id,
                    CountWasSaturated: exploration.OmissionCountWasSaturated));
        }

        if (omittedByCap.Length > 0)
        {
            omissions.Add(
                new CandidateOmissionWitness(
                    CandidateGenerationFailureCodes.CandidateCapOmission,
                    omittedByCap.Length,
                    CandidateIdentity.CreateOmissionDigest(
                        omittedByCap.Select(candidate => candidate.CandidateId)),
                    "Feasible candidates were omitted by the deterministic " +
                    "accepted-count/cost/slack/stable-ID cap.",
                    vehicle.Id));
        }

        var loss = new VehicleCandidateLoss(
            exploration.WorkUnits,
            exploration.EvaluatedCandidatePathCount,
            beforeCap.Length,
            ranked.Count,
            prunedById.Count,
            exploration.OmittedCandidatePathCount,
            omittedByCap.Length,
            exploration.OmittedCandidatePathCount > 0,
            omittedByCap.Length > 0,
            exploration.OmissionCountWasSaturated,
            repair.EligibleRequestIds.Count,
            repair.ConsideredRequestIds.Count,
            repair.OmittedRequestIds.Count);
        var wasTruncated = requestsWereOmitted
            || loss.WorkBudgetExhausted
            || loss.CandidateCapApplied
            || loss.OmittedRepairRequestCount > 0;

        return VehicleGenerationResult.Success(
            new VehicleCandidateSet(
                vehicle.Id,
                ranked
                    .OrderBy(value => value.CandidateId, StringComparer.Ordinal)
                    .ToArray(),
                prunedById.Values
                    .OrderBy(value => value.CandidateId, StringComparer.Ordinal)
                    .ToArray(),
                wasTruncated,
                loss),
            omissions.AsReadOnly());
    }

    private ExplorationResult ExploreBestFirst(
        OnlineState state,
        VehicleState vehicle,
        IReadOnlyList<RideRequest> requests,
        IReadOnlyList<WaitingIncumbentRepairSeed> repairSeeds,
        CandidateGenerationOptions options,
        IDictionary<string, InsertionCandidate> feasibleById,
        IDictionary<string, CandidatePruneWitness> prunedById)
    {
        var frontier = new PriorityQueue<ExplorationNode, SearchPriority>(
            SearchPriorityComparer.Instance);
        var roots = new[]
            {
                CreateNode(
                    requestIndex: 0,
                    vehicle.Route.MutableSuffix,
                    [],
                    repairedIncumbentRequestId: null),
            }
            .Concat(
                repairSeeds.Select(
                    seed => CreateNode(
                        requestIndex: 0,
                        seed.Route.MutableSuffix,
                        [],
                        seed.RequestId)))
            .GroupBy(node => node.StableId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();

        foreach (var root in roots)
        {
            frontier.Enqueue(root, CreatePriority(state, vehicle, requests, root));
        }
        var workUnits = 0L;
        var evaluatedPaths = 0L;

        while (workUnits < options.MaximumExplorationWorkUnits
            && frontier.TryDequeue(out var node, out _))
        {
            workUnits++;

            if (node.RequestIndex == requests.Count)
            {
                if (node.InsertedRequestIds.Count == 0
                    && node.RepairedIncumbentRequestId is null)
                {
                    continue;
                }

                evaluatedPaths++;
                var routeResult = vehicle.Route.ReplaceMutableSuffix(node.Suffix);

                if (routeResult.IsSuccess)
                {
                    EvaluateRoute(
                        state,
                        vehicle,
                        routeResult.Value!,
                        node.InsertedRequestIds,
                        isNoOp: false,
                        node.RepairedIncumbentRequestId,
                        options,
                        feasibleById,
                        prunedById);
                }

                continue;
            }

            var children = Expand(node, requests[node.RequestIndex])
                .Select(
                    child => new PrioritizedNode(
                        child,
                        CreatePriority(state, vehicle, requests, child)))
                .ToList();
            children.Sort(
                (left, right) => SearchPriorityComparer.Instance.Compare(
                    left.Priority,
                    right.Priority));

            foreach (var prioritized in children)
            {
                frontier.Enqueue(prioritized.Node, prioritized.Priority);
            }
        }

        var omittedCount = BigInteger.Zero;
        var omittedSubtreeIds = new List<string>();

        while (frontier.TryDequeue(out var omitted, out _))
        {
            var subtreeCount = CountTerminalCandidatePaths(
                omitted,
                requests.Count);

            if (subtreeCount > 0)
            {
                omittedCount += subtreeCount;
                omittedSubtreeIds.Add(omitted.StableId);
            }
        }

        if (omittedCount > 0 && options.ExactSmallMode)
        {
            return ExplorationResult.Failure(
                new CandidateGenerationWitness(
                    CandidateGenerationFailureCodes.ExactSmallWorkCapExceeded,
                    "Exact-small exploration exceeded the deterministic work " +
                    "cap; no raw candidate path was silently omitted.",
                    vehicle.Id,
                    Dimension: "maximumExplorationWorkUnits"));
        }

        var saturated = omittedCount > DomainLimits.MaxCanonicalInteger;
        var canonicalOmitted = saturated
            ? DomainLimits.MaxCanonicalInteger
            : (long)omittedCount;

        return ExplorationResult.Success(
            workUnits,
            evaluatedPaths,
            canonicalOmitted,
            omittedSubtreeIds.AsReadOnly(),
            saturated);
    }

    private static IEnumerable<ExplorationNode> Expand(
        ExplorationNode node,
        RideRequest request)
    {
        yield return CreateNode(
            node.RequestIndex + 1,
            node.Suffix,
            node.InsertedRequestIds,
            node.RepairedIncumbentRequestId);

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

        for (var pickupIndex = 0;
             pickupIndex <= node.Suffix.Count;
             pickupIndex++)
        {
            var withPickup = node.Suffix.ToList();
            withPickup.Insert(pickupIndex, pickup);

            for (var dropIndex = pickupIndex + 1;
                 dropIndex <= withPickup.Count;
                 dropIndex++)
            {
                var withPair = withPickup.ToList();
                withPair.Insert(dropIndex, dropOff);
                yield return CreateNode(
                    node.RequestIndex + 1,
                    withPair,
                    node.InsertedRequestIds.Append(request.Id).ToArray(),
                    node.RepairedIncumbentRequestId);
            }
        }
    }

    private SearchPriority CreatePriority(
        OnlineState state,
        VehicleState vehicle,
        IReadOnlyList<RideRequest> requests,
        ExplorationNode node)
    {
        var remaining = requests.Count - node.RequestIndex;
        var potentialAccepted = node.InsertedRequestIds.Count + remaining;
        var mandatoryService = 0L;

        foreach (var stop in node.Suffix)
        {
            mandatoryService = SaturatingAdd(
                mandatoryService,
                stop.ServiceDuration.Milliseconds);
        }

        var route = node.RequestIndex == 0
            ? vehicle.Route
            : vehicle.Route.ReplaceMutableSuffix(node.Suffix).Value;
        long? slack = 0;

        if (route is not null)
        {
            var profile = _slackCache.GetOrBuild(
                state,
                vehicle,
                route,
                state.TravelTimes!,
                state.Run.SimulationTime);

            if (profile.Result.IsSuccess)
            {
                slack = profile.Result.Profile!
                    .CertifiedDelayAtRouteStartMilliseconds;
            }
        }

        return new SearchPriority(
            -potentialAccepted,
            mandatoryService,
            slack is null ? 0 : 1,
            -(slack ?? 0),
            node.StableId);
    }

    private static ExplorationNode CreateNode(
        int requestIndex,
        IEnumerable<RouteStop> suffix,
        IEnumerable<RequestId> insertedRequestIds,
        RequestId? repairedIncumbentRequestId)
    {
        var routeStops = suffix.ToArray();
        var requests = insertedRequestIds.ToArray();
        var stableId = $"{requestIndex:D8}:" +
            CandidateIdentity.CreateSearchNodeDigest(
                routeStops.Select(stop => stop.StopId.Value)
                    .Concat(requests.Select(request => request.Value))
                    .Append(repairedIncumbentRequestId?.Value ?? "no-repair"));
        return new ExplorationNode(
            requestIndex,
            Array.AsReadOnly(routeStops),
            Array.AsReadOnly(requests),
            repairedIncumbentRequestId,
            stableId);
    }

    private static BigInteger CountTerminalCandidatePaths(
        ExplorationNode node,
        int requestCount)
    {
        var memo = new Dictionary<(int SuffixCount, int Remaining, bool Inserted), BigInteger>();
        return Count(
            node.Suffix.Count,
            requestCount - node.RequestIndex,
            node.InsertedRequestIds.Count > 0
                || node.RepairedIncumbentRequestId is not null,
            memo);
    }

    private static BigInteger Count(
        int suffixCount,
        int remaining,
        bool hasInserted,
        IDictionary<(int, int, bool), BigInteger> memo)
    {
        // Every remaining request can independently be skipped or inserted at
        // least one way. At 54 remaining requests, 2^54 - 1 already exceeds the
        // canonical reporting range, so recursion can truthfully saturate early.
        if (remaining >= 54)
        {
            return new BigInteger(DomainLimits.MaxCanonicalInteger) + 1;
        }

        var key = (suffixCount, remaining, hasInserted);

        if (memo.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (remaining == 0)
        {
            return hasInserted ? BigInteger.One : BigInteger.Zero;
        }

        var insertionPositions = new BigInteger(suffixCount + 1)
            * (suffixCount + 2)
            / 2;
        var result = Count(suffixCount, remaining - 1, hasInserted, memo)
            + insertionPositions
            * Count(suffixCount + 2, remaining - 1, true, memo);
        memo[key] = result;
        return result;
    }

    private static long SaturatingAdd(long left, long right) =>
        left > DomainLimits.MaxCanonicalInteger - right
            ? DomainLimits.MaxCanonicalInteger
            : left + right;

    private void EvaluateRoute(
        OnlineState state,
        VehicleState vehicle,
        RoutePlan route,
        IReadOnlyList<RequestId> insertedRequestIds,
        bool isNoOp,
        RequestId? repairedIncumbentRequestId,
        CandidateGenerationOptions options,
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

        if (feasibleById.ContainsKey(candidateId)
            || prunedById.ContainsKey(candidateId))
        {
            return;
        }

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

        var profileLookup = _slackCache.GetOrBuild(
            state,
            vehicle,
            route,
            state.TravelTimes!,
            state.Run.SimulationTime);

        if (!profileLookup.Result.IsSuccess)
        {
            prunedById.TryAdd(
                candidateId,
                new CandidatePruneWitness(
                    candidateId,
                    vehicle.Id,
                    orderedRequests,
                    profileLookup.Result.Code!,
                    profileLookup.Result.Message!));
            return;
        }

        var selectedRoute = route;
        var selectedCandidateId = candidateId;
        var selectedProfile = profileLookup.Result.Profile!;
        var selectedStrategy = CandidateScheduleStrategy.EarliestFeasible;
        var relocatedWait = 0L;

        if (!isNoOp
            && options.ScheduleStrategy
                == CandidateScheduleStrategy.OriginHoldRelocatedWait)
        {
            var transformed = _originHoldTransformer.Transform(
                vehicle,
                route,
                selectedProfile,
                candidateId);

            if (transformed.WasApplied)
            {
                var transformedId = CandidateIdentity.Create(
                    state,
                    vehicle.Id,
                    transformed.Route,
                    orderedRequests);
                var transformedValidation = _validator.Validate(
                    new PhysicalValidationContext(
                        state.Run,
                        vehicle.Id,
                        transformed.Route,
                        state.TravelTimes!,
                        state.Run.SimulationTime));

                if (!transformedValidation.IsFeasible)
                {
                    prunedById.TryAdd(
                        transformedId,
                        new CandidatePruneWitness(
                            transformedId,
                            vehicle.Id,
                            orderedRequests,
                            transformedValidation.Witness!.Code,
                            transformedValidation.Witness.Message,
                            transformedValidation.Witness));
                    return;
                }

                var transformedProfile = _slackCache.GetOrBuild(
                    state,
                    vehicle,
                    transformed.Route,
                    state.TravelTimes!,
                    state.Run.SimulationTime);

                if (!transformedProfile.Result.IsSuccess
                    || !HasExactServiceEquivalence(
                        selectedProfile,
                        transformedProfile.Result.Profile!))
                {
                    prunedById.TryAdd(
                        transformedId,
                        new CandidatePruneWitness(
                            transformedId,
                            vehicle.Id,
                            orderedRequests,
                            CandidateGenerationFailureCodes
                                .ScheduleStrategyEquivalenceFailed,
                            "Origin-hold transformation did not preserve exact " +
                            "service/departure times and operational cost."));
                    return;
                }

                selectedRoute = transformed.Route;
                selectedCandidateId = transformedId;
                selectedProfile = transformedProfile.Result.Profile!;
                selectedStrategy =
                    CandidateScheduleStrategy.OriginHoldRelocatedWait;
                relocatedWait = transformed.RelocatedWaitMilliseconds;
            }
        }

        feasibleById.TryAdd(
            selectedCandidateId,
            new InsertionCandidate(
                selectedCandidateId,
                vehicle.Id,
                selectedRoute,
                orderedRequests,
                selectedProfile.Schedule,
                isNoOp,
                selectedStrategy,
                relocatedWait,
                selectedProfile.CertifiedDelayAtRouteStartMilliseconds,
                repairedIncumbentRequestId));
    }

    private static bool HasExactServiceEquivalence(
        ForwardSlackProfile earliest,
        ForwardSlackProfile originHold)
    {
        if (originHold.Stops.Count != earliest.Stops.Count + 1
            || originHold.Schedule.OperationalCost
                != earliest.Schedule.OperationalCost)
        {
            return false;
        }

        for (var index = 0; index < earliest.Stops.Count; index++)
        {
            var original = earliest.Stops[index];
            var transformed = originHold.Stops[index + 1];

            if (original.StopId != transformed.StopId
                || original.ServiceStartTime != transformed.ServiceStartTime
                || original.DepartureTime != transformed.DepartureTime
                || index > 0 && original.ArrivalTime != transformed.ArrivalTime)
            {
                return false;
            }
        }

        return true;
    }

    private sealed record ExplorationNode(
        int RequestIndex,
        IReadOnlyList<RouteStop> Suffix,
        IReadOnlyList<RequestId> InsertedRequestIds,
        RequestId? RepairedIncumbentRequestId,
        string StableId);

    private sealed record PrioritizedNode(
        ExplorationNode Node,
        SearchPriority Priority);

    private readonly record struct SearchPriority(
        int NegativePotentialAcceptedCount,
        long MandatoryServiceLowerBound,
        int SlackBoundClass,
        long NegativeCertifiedSlack,
        string StableId);

    private sealed class SearchPriorityComparer : IComparer<SearchPriority>
    {
        public static SearchPriorityComparer Instance { get; } = new();

        public int Compare(SearchPriority left, SearchPriority right)
        {
            var comparison = left.NegativePotentialAcceptedCount.CompareTo(
                right.NegativePotentialAcceptedCount);
            comparison = comparison != 0
                ? comparison
                : left.MandatoryServiceLowerBound.CompareTo(
                    right.MandatoryServiceLowerBound);
            comparison = comparison != 0
                ? comparison
                : left.SlackBoundClass.CompareTo(right.SlackBoundClass);
            comparison = comparison != 0
                ? comparison
                : left.NegativeCertifiedSlack.CompareTo(
                    right.NegativeCertifiedSlack);
            return comparison != 0
                ? comparison
                : StringComparer.Ordinal.Compare(left.StableId, right.StableId);
        }
    }

    private sealed record ExplorationResult(
        long WorkUnits,
        long EvaluatedCandidatePathCount,
        long OmittedCandidatePathCount,
        IReadOnlyList<string> OmittedSubtreeIds,
        bool OmissionCountWasSaturated,
        CandidateGenerationWitness? Witness)
    {
        public static ExplorationResult Success(
            long workUnits,
            long evaluatedCandidatePathCount,
            long omittedCandidatePathCount,
            IReadOnlyList<string> omittedSubtreeIds,
            bool omissionCountWasSaturated) =>
            new(
                workUnits,
                evaluatedCandidatePathCount,
                omittedCandidatePathCount,
                omittedSubtreeIds,
                omissionCountWasSaturated,
                null);

        public static ExplorationResult Failure(
            CandidateGenerationWitness witness) =>
            new(0, 0, 0, [], false, witness);
    }

    private sealed record VehicleGenerationResult(
        VehicleCandidateSet? Candidates,
        IReadOnlyList<CandidateOmissionWitness> Omissions,
        CandidateGenerationWitness? Witness)
    {
        public static VehicleGenerationResult Success(
            VehicleCandidateSet candidates,
            IReadOnlyList<CandidateOmissionWitness> omissions) =>
            new(candidates, omissions, null);

        public static VehicleGenerationResult Failure(
            CandidateGenerationWitness witness) =>
            new(null, [], witness);
    }
}
