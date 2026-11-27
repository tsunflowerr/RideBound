using System.Numerics;
using RideBound.Application.State;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;
using RideBound.Domain.Validation;
using RideBound.Domain.Vehicles;

namespace RideBound.Algorithms.Candidates;

public sealed partial class InsertionCandidateGenerator
{
    private readonly PhysicalPlanValidator _validator;
    private readonly ForwardSlackProfileCache _slackCache;
    private readonly CandidatePortfolioRetainer _portfolioRetainer;
    private readonly WaitingRiderRelocationBuilder _relocationBuilder = new();

    public InsertionCandidateGenerator(
        PhysicalPlanValidator? validator = null,
        CandidateScheduleEvaluator? scheduleEvaluator = null,
        ForwardSlackProfileCache? slackCache = null,
        CandidatePortfolioRetainer? portfolioRetainer = null)
    {
        _validator = validator ?? new PhysicalPlanValidator();
        _slackCache = slackCache ?? new ForwardSlackProfileCache(
            new ForwardSlackProfileBuilder(scheduleEvaluator));
        _portfolioRetainer = portfolioRetainer
            ?? new CandidatePortfolioRetainer();
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
                    "Exact-small mode cannot omit pending requests above its published request bound.",
                    RequestId: pendingRequests[options.MaximumNewRequestsPerVehicle].Id,
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
        var breaches = new List<ExogenousServiceQualityBreach>();

        if (omittedRequests.Length > 0)
        {
            omissions.Add(
                new CandidateOmissionWitness(
                    CandidateGenerationFailureCodes.RequestBoundOmission,
                    omittedRequests.Length,
                    CandidateIdentity.CreateOmissionDigest(
                        omittedRequests.Select(request => request.Id.Value)),
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
            breaches.AddRange(generated.Breaches);
        }

        IReadOnlyList<RelocationCandidateLoss>? relocationLosses = null;
        if (options.WaitingRiderRelocation is { } relocationOptions)
        {
            var relocations = GenerateRelocations(state, requests, options, relocationOptions);
            for (var index = 0; index < vehicleSets.Count; index++)
            {
                vehicleSets[index] = relocations.MergeInto(vehicleSets[index]);
            }

            omissions.AddRange(relocations.Omissions);
            relocationLosses = relocations.Losses.AsReadOnly();
        }

        return CandidateGenerationResult.Success(
            vehicleSets.AsReadOnly(),
            new CandidateGenerationDiagnostics(
                pendingRequests.Length,
                requests.Length,
                omittedRequests.Length,
                vehicleSets.Select(set => set.Loss!).ToArray(),
                omissions.AsReadOnly(),
                breaches.AsReadOnly(),
                relocationLosses));
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

        // Probe the unchanged active route first. Whatever it can no
        // longer honour on the two service-quality dimensions is exogenous by
        // construction. The resulting relaxation is applied only to the safety
        // no-op; every changed candidate stays strictly contractual.
        var probe = _validator.ProbeServiceQuality(
            state.Run,
            vehicle.Id,
            state.TravelTimes!,
            state.Run.SimulationTime);

        if (!probe.IsSuccess)
        {
            return VehicleGenerationResult.Failure(
                new CandidateGenerationWitness(
                    CandidateGenerationFailureCodes.ActiveRouteInfeasible,
                    "The active route violates a structural physical constraint: " +
                    $"{probe.Witness!.Code}: {probe.Witness.Message}" +
                    FormatPhysicalDetails(probe.Witness),
                    vehicle.Id,
                    probe.Witness.RequestId,
                    probe.Witness.Dimension ?? probe.Witness.Code));
        }

        var serviceQuality = probe.Allowance;
        var breaches = serviceQuality.Breaches
            .Select(
                value => new ExogenousServiceQualityBreach(
                    vehicle.Id,
                    value.RequestId,
                    value.Code,
                    value.Dimension,
                    value.ContractualMilliseconds,
                    value.ExogenousMilliseconds))
            .ToArray();

        var noOpFailure = EvaluateNoOp(
            state,
            vehicle,
            serviceQuality,
            feasibleById,
            prunedById);

        if (noOpFailure is not null)
        {
            return VehicleGenerationResult.Failure(noOpFailure);
        }

        // Only the unchanged no-op can use the probed allowance.
        var exploration = options.ExactSmallMode
            ? ExploreExact(state, vehicle, pendingRequests, feasibleById, prunedById)
            : ExploreBestFirst(state, vehicle, pendingRequests, options, feasibleById, prunedById);

        var beforeCap = feasibleById.Values.ToArray();
        IReadOnlyList<InsertionCandidate> retained = beforeCap;
        IReadOnlyList<InsertionCandidate> omittedByCap = [];

        if (beforeCap.Length > options.MaximumCandidatesPerVehicle)
        {
            if (options.ExactSmallMode)
            {
                return VehicleGenerationResult.Failure(
                    new CandidateGenerationWitness(
                        CandidateGenerationFailureCodes.ExactSmallCandidateCapExceeded,
                        "Exact-small candidate count exceeded the configured cap.",
                        vehicle.Id,
                        Dimension: "maximumCandidatesPerVehicle"));
            }

            var ranked = CandidatePortfolioRetainer.Rank(beforeCap).ToArray();
            var retention = _portfolioRetainer.RetainRanked(
                ranked,
                options.MaximumCandidatesPerVehicle);
            retained = retention.Retained;
            omittedByCap = retention.Omitted;
        }

        var omissions = BuildOmissions(exploration, omittedByCap);

        var loss = new VehicleCandidateLoss(
            exploration.WorkUnits,
            exploration.EvaluatedCandidatePathCount,
            beforeCap.Length,
            retained.Count,
            prunedById.Count,
            exploration.OmittedCandidatePathCount,
            omittedByCap.Count,
            exploration.OmittedCandidatePathCount > 0,
            omittedByCap.Count > 0,
            exploration.OmissionCountWasSaturated);
        var wasTruncated = requestsWereOmitted
            || loss.WorkBudgetExhausted
            || loss.CandidateCapApplied;

        return VehicleGenerationResult.Success(
            new VehicleCandidateSet(
                vehicle.Id,
                retained
                    .OrderBy(value => value.CandidateId, StringComparer.Ordinal)
                    .ToArray(),
                prunedById.Values
                    .OrderBy(value => value.CandidateId, StringComparer.Ordinal)
                    .ToArray(),
                wasTruncated,
                loss),
            omissions.AsReadOnly(),
            breaches);
    }

    /// <summary>
    /// Evaluates the unchanged active route as the safety no-op candidate, under the probed
    /// allowance. Returns the failure witness when the route cannot be kept, otherwise null.
    /// </summary>
    private CandidateGenerationWitness? EvaluateNoOp(
        OnlineState state,
        VehicleState vehicle,
        ServiceQualityAllowance serviceQuality,
        Dictionary<string, InsertionCandidate> feasibleById,
        Dictionary<string, CandidatePruneWitness> prunedById)
    {
        var noOpCandidateId = CandidateIdentity.Create(
            state,
            vehicle.Id,
            vehicle.Route,
            []);
        EvaluateRoute(
            state,
            vehicle,
            vehicle.Route,
            [],
            isNoOp: true,
            serviceQuality,
            feasibleById,
            prunedById);

        if (!feasibleById.TryGetValue(noOpCandidateId, out var noOp)
            || !noOp.IsNoOp)
        {
            prunedById.TryGetValue(noOpCandidateId, out var prune);
            return new CandidateGenerationWitness(
                CandidateGenerationFailureCodes.ActiveRouteInfeasible,
                prune is null
                    ? "The active route could not be retained as the safety " +
                      "no-op candidate."
                    : "The active route could not be retained as the safety " +
                      $"no-op candidate: {prune.Code}: {prune.Message}" +
                      FormatPhysicalDetails(prune.PhysicalWitness),
                vehicle.Id,
                prune?.PhysicalWitness?.RequestId,
                prune?.PhysicalWitness?.Dimension ?? prune?.Code);
        }

        return null;
    }

    /// <summary>
    /// The omission witnesses of one vehicle: first the paths the work bound left unexplored,
    /// then the feasible candidates the cap dropped.
    /// </summary>
    private static List<CandidateOmissionWitness> BuildOmissions(
        ExplorationResult exploration,
        IReadOnlyList<InsertionCandidate> omittedByCap)
    {
        var omissions = new List<CandidateOmissionWitness>();

        if (exploration.OmittedCandidatePathCount > 0)
        {
            omissions.Add(
                new CandidateOmissionWitness(
                    CandidateGenerationFailureCodes.WorkBoundOmission,
                    exploration.OmittedCandidatePathCount,
                    CandidateIdentity.CreateOmissionDigest(
                        exploration.OmittedSubtreeIds),
                    CountWasSaturated: exploration.OmissionCountWasSaturated));
        }

        if (omittedByCap.Count > 0)
        {
            omissions.Add(
                new CandidateOmissionWitness(
                    CandidateGenerationFailureCodes.CandidateCapOmission,
                    omittedByCap.Count,
                    CandidateIdentity.CreateOmissionDigest(
                        omittedByCap.Select(candidate => candidate.CandidateId))));
        }

        return omissions;
    }

    private static string FormatPhysicalDetails(
        PhysicalViolationWitness? witness) =>
        witness is null
            ? string.Empty
            : $" [vehicleId={witness.VehicleId.Value};" +
              $"requestId={witness.RequestId?.Value ?? "<none>"};" +
              $"stopId={witness.StopId?.Value ?? "<none>"};" +
              $"dimension={witness.Dimension ?? "<none>"};" +
              $"expected={witness.Expected?.ToString() ?? "<none>"};" +
              $"actual={witness.Actual?.ToString() ?? "<none>"}]";

    private ExplorationResult ExploreExact(
        OnlineState state,
        VehicleState vehicle,
        IReadOnlyList<RideRequest> requests,
        Dictionary<string, InsertionCandidate> feasibleById,
        Dictionary<string, CandidatePruneWitness> prunedById)
    {
        var workUnits = 0L;
        var evaluatedPaths = 0L;
        Visit(CreateNode(0, vehicle.Route.MutableSuffix, []));
        return new ExplorationResult(workUnits, evaluatedPaths, 0, [], false);

        void Visit(ExplorationNode node)
        {
            workUnits++;

            if (node.RequestIndex < requests.Count)
            {
                foreach (var child in Expand(node, requests[node.RequestIndex]))
                {
                    Visit(child);
                }

                return;
            }

            if (node.InsertedRequestIds.Count > 0)
            {
                evaluatedPaths++;
                EvaluateTerminal(state, vehicle, node, feasibleById, prunedById);
            }
        }
    }

    private ExplorationResult ExploreBestFirst(
        OnlineState state,
        VehicleState vehicle,
        IReadOnlyList<RideRequest> requests,
        CandidateGenerationOptions options,
        IDictionary<string, InsertionCandidate> feasibleById,
        IDictionary<string, CandidatePruneWitness> prunedById,
        RelocationRoot? relocationRoot = null)
    {
        var frontier = new PriorityQueue<ExplorationNode, SearchPriority>(
            SearchPriorityComparer.Instance);
        var root = CreateNode(
            0,
            relocationRoot?.Suffix ?? vehicle.Route.MutableSuffix,
            []);
        frontier.Enqueue(
            root,
            CreatePriority(state, vehicle, requests, root, relocationRoot is null));
        var workUnits = 0L;
        var evaluatedPaths = 0L;

        while (workUnits < options.MaximumExplorationWorkUnits
            && frontier.TryDequeue(out var node, out _))
        {
            workUnits++;

            if (node.RequestIndex == requests.Count)
            {
                if (node.InsertedRequestIds.Count == 0)
                {
                    continue;
                }

                evaluatedPaths++;
                EvaluateTerminal(
                    state, vehicle, node, feasibleById, prunedById, relocationRoot?.Relocation);
                continue;
            }

            var children = Expand(node, requests[node.RequestIndex])
                .Select(
                    child => new PrioritizedNode(
                        child,
                        CreatePriority(
                            state,
                            vehicle,
                            requests,
                            child)))
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

        // The combinatorial memo is keyed only by (suffixCount, remaining,
        // hasInserted); nothing in it depends on the individual frontier node.
        // Allocating it per node discarded one dictionary per unexpanded node.
        var subtreeMemo =
            new Dictionary<(int SuffixCount, int Remaining, bool Inserted), BigInteger>();

        while (frontier.TryDequeue(out var omitted, out _))
        {
            var subtreeCount = CountTerminalCandidatePaths(
                omitted,
                requests.Count,
                subtreeMemo);

            if (subtreeCount > 0)
            {
                omittedCount += subtreeCount;
                omittedSubtreeIds.Add(omitted.StableId);
            }
        }

        var saturated = omittedCount > DomainLimits.MaxCanonicalInteger;
        var canonicalOmitted = saturated
            ? DomainLimits.MaxCanonicalInteger
            : (long)omittedCount;

        return new ExplorationResult(
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
            node.InsertedRequestIds);

        // An inserted pickup or drop stop has no service duration (dwell).
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

        var baseStops = node.Suffix;

        // Every child of this expansion serves exactly the same request set, so
        // the inserted-request list is built once and shared as an immutable
        // snapshot instead of being rebuilt per child.
        var insertedBuffer = new RequestId[node.InsertedRequestIds.Count + 1];

        for (var index = 0; index < node.InsertedRequestIds.Count; index++)
        {
            insertedBuffer[index] = node.InsertedRequestIds[index];
        }

        insertedBuffer[^1] = request.Id;
        var inserted = Array.AsReadOnly(insertedBuffer);

        for (var pickupIndex = 0; pickupIndex <= baseStops.Count; pickupIndex++)
        {
            for (var dropIndex = pickupIndex + 1;
                 dropIndex <= baseStops.Count + 1;
                 dropIndex++)
            {
                // Write the child suffix straight into its final array. The old
                // form built an intermediate list per pickup slot and copied it
                // again per drop slot, so every child paid three O(k) copies.
                var withPair = new RouteStop[baseStops.Count + 2];
                var source = 0;

                for (var index = 0; index < withPair.Length; index++)
                {
                    withPair[index] = index == pickupIndex
                        ? pickup
                        : index == dropIndex
                            ? dropOff
                            : baseStops[source++];
                }

                yield return CreateOwnedNode(
                    node.RequestIndex + 1,
                    withPair,
                    inserted);
            }
        }
    }

    private SearchPriority CreatePriority(
        OnlineState state,
        VehicleState vehicle,
        IReadOnlyList<RideRequest> requests,
        ExplorationNode node,
        bool rootIsActiveRoute = true)
    {
        var remaining = requests.Count - node.RequestIndex;
        var potentialAccepted = node.InsertedRequestIds.Count + remaining;
        var mandatoryService = node.MandatoryServiceMilliseconds;

        var route = node.RequestIndex == 0 && rootIsActiveRoute
            ? vehicle.Route
            : node.Project(vehicle).Value;
        long? slack = 0;

        if (route is not null)
        {
            var profile = _slackCache.GetOrBuild(
                state,
                vehicle,
                route,
                state.TravelTimes!,
                state.Run.SimulationTime,
                ServiceQualityAllowance.Strict);
            node.SetSlackLookup(profile);

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
            node);
    }

    private static ExplorationNode CreateNode(
        int requestIndex,
        IEnumerable<RouteStop> suffix,
        IEnumerable<RequestId> insertedRequestIds) =>
        CreateOwnedNode(
            requestIndex,
            suffix.ToArray(),
            Array.AsReadOnly(insertedRequestIds.ToArray()));

    /// <summary>
    /// Builds a node from a suffix array the caller has just allocated and will
    /// not touch again, and from an already immutable inserted-request snapshot.
    /// This avoids re-copying both sequences on the expansion hot path.
    /// </summary>
    private static ExplorationNode CreateOwnedNode(
        int requestIndex,
        RouteStop[] ownedSuffix,
        IReadOnlyList<RequestId> insertedRequestIds) =>
        new(
            requestIndex,
            Array.AsReadOnly(ownedSuffix),
            insertedRequestIds);

    private static BigInteger CountTerminalCandidatePaths(
        ExplorationNode node,
        int requestCount,
        IDictionary<(int SuffixCount, int Remaining, bool Inserted), BigInteger> memo) =>
        Count(
            node.Suffix.Count,
            requestCount - node.RequestIndex,
            node.InsertedRequestIds.Count > 0,
            memo);

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

    private void EvaluateTerminal(
        OnlineState state,
        VehicleState vehicle,
        ExplorationNode node,
        IDictionary<string, InsertionCandidate> feasibleById,
        IDictionary<string, CandidatePruneWitness> prunedById,
        WaitingRiderRelocation? relocation = null)
    {
        var route = node.Project(vehicle);

        if (route.IsSuccess)
        {
            EvaluateRoute(
                state,
                vehicle,
                route.Value!,
                node.InsertedRequestIds,
                isNoOp: false,
                ServiceQualityAllowance.Strict,
                feasibleById,
                prunedById,
                node.SlackLookup,
                relocation,
                relocation is null ? null : RelocationRole.Donor);
        }
    }

    private void EvaluateRoute(
        OnlineState state,
        VehicleState vehicle,
        RoutePlan route,
        IReadOnlyList<RequestId> insertedRequestIds,
        bool isNoOp,
        ServiceQualityAllowance serviceQuality,
        IDictionary<string, InsertionCandidate> feasibleById,
        IDictionary<string, CandidatePruneWitness> prunedById,
        ForwardSlackCacheLookup? prefetchedProfile = null,
        WaitingRiderRelocation? relocation = null,
        RelocationRole? relocationRole = null)
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
                state.Run.SimulationTime,
                serviceQuality,
                relocation));

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

        if (prefetchedProfile is not null
            && !prefetchedProfile.Key.Matches(
                state,
                vehicle,
                route,
                state.TravelTimes!,
                state.Run.SimulationTime,
                serviceQuality))
        {
            throw new InvalidOperationException(
                "A prefetched slack profile must match the exact candidate state and route.");
        }

        var profileLookup = prefetchedProfile ?? _slackCache.GetOrBuild(
                state,
                vehicle,
                route,
                state.TravelTimes!,
                state.Run.SimulationTime,
                serviceQuality);

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

        var profile = profileLookup.Result.Profile!;
        feasibleById.TryAdd(
            candidateId,
            new InsertionCandidate(
                candidateId,
                vehicle.Id,
                route,
                orderedRequests,
                profile.Schedule,
                isNoOp,
                profile.CertifiedDelayAtRouteStartMilliseconds,
                relocation?.RequestId,
                relocationRole));
    }

    /// <summary>
    /// A search node. <see cref="StableId"/> and the projected route are both
    /// derived, deterministic and expensive, so they are computed at most once
    /// and only if something actually asks for them. The frontier holds far
    /// more nodes than the work budget can expand, and the priority comparison
    /// only reaches the identity tie-break when every earlier key ties.
    /// </summary>
    private sealed class ExplorationNode(
        int requestIndex,
        IReadOnlyList<RouteStop> suffix,
        IReadOnlyList<RequestId> insertedRequestIds)
    {
        private string? _stableId;
        private DomainResult<RoutePlan>? _projection;
        private long? _mandatoryServiceMilliseconds;

        public int RequestIndex { get; } = requestIndex;

        public IReadOnlyList<RouteStop> Suffix { get; } = suffix;

        public IReadOnlyList<RequestId> InsertedRequestIds { get; } =
            insertedRequestIds;

        public ForwardSlackCacheLookup? SlackLookup { get; private set; }

        public long MandatoryServiceMilliseconds =>
            _mandatoryServiceMilliseconds ??= Suffix.Aggregate(
                0L,
                (total, stop) => SaturatingAdd(
                    total,
                    stop.ServiceDuration.Milliseconds));

        /// <remarks>
        /// The digest still ends with the token "no-repair", which marked a node outside the removed
        /// waiting-incumbent repair search (B4). Node ids order the frontier on ties and name the
        /// omitted subtrees in the omission digests, so the token keeps both byte-identical.
        /// </remarks>
        public string StableId => _stableId ??= $"{RequestIndex:D8}:" +
            CandidateIdentity.CreateSearchNodeDigest(
                Suffix.Select(stop => stop.StopId.Value)
                    .Concat(InsertedRequestIds.Select(request => request.Value))
                    .Append("no-repair"),
                Suffix.Count + InsertedRequestIds.Count + 1);

        public DomainResult<RoutePlan> Project(VehicleState vehicle) =>
            _projection ??= vehicle.Route.ReplaceMutableSuffix(Suffix);

        public void SetSlackLookup(ForwardSlackCacheLookup lookup)
        {
            ArgumentNullException.ThrowIfNull(lookup);

            if (SlackLookup is not null && SlackLookup != lookup)
            {
                throw new InvalidOperationException(
                    "A search node cannot be ranked against two slack profiles.");
            }

            SlackLookup = lookup;
        }
    }

    private sealed record PrioritizedNode(
        ExplorationNode Node,
        SearchPriority Priority);

    /// <summary>
    /// The tie-break key carries the node rather than its identity string so
    /// that the identity digest is only computed when the comparison actually
    /// reaches it. The resulting order is unchanged.
    /// </summary>
    private readonly record struct SearchPriority(
        int NegativePotentialAcceptedCount,
        long MandatoryServiceLowerBound,
        int SlackBoundClass,
        long NegativeCertifiedSlack,
        ExplorationNode Node);

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
                : StringComparer.Ordinal.Compare(
                    left.Node.StableId,
                    right.Node.StableId);
        }
    }

    private sealed record ExplorationResult(
        long WorkUnits,
        long EvaluatedCandidatePathCount,
        long OmittedCandidatePathCount,
        IReadOnlyList<string> OmittedSubtreeIds,
        bool OmissionCountWasSaturated);

    private sealed record VehicleGenerationResult(
        VehicleCandidateSet? Candidates,
        IReadOnlyList<CandidateOmissionWitness> Omissions,
        CandidateGenerationWitness? Witness,
        IReadOnlyList<ExogenousServiceQualityBreach> Breaches)
    {
        public static VehicleGenerationResult Success(
            VehicleCandidateSet candidates,
            IReadOnlyList<CandidateOmissionWitness> omissions,
            IReadOnlyList<ExogenousServiceQualityBreach> breaches) =>
            new(candidates, omissions, null, breaches);

        public static VehicleGenerationResult Failure(
            CandidateGenerationWitness witness) =>
            new(null, [], witness, []);
    }
}
