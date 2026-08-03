using RideBound.Algorithms.Candidates;
using RideBound.Algorithms.Commitments;
using RideBound.Application.Commitments;
using RideBound.Application.State;
using RideBound.Application.Travel;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Routes;
using RideBound.Domain.Validation;

namespace RideBound.Algorithms.Tests.Oracle;

public sealed class ExactSmallCommitmentDifferentialTests
{
    [Fact]
    public void Production_commitment_filter_matches_independent_small_oracle()
    {
        const int publishedSeedCount = 16;

        for (var seed = 0; seed < publishedSeedCount; seed++)
        {
            var fixture = CreateState(seed);
            var generated = new InsertionCandidateGenerator().Generate(
                fixture.State,
                CandidateGenerationOptions.ExactSmall);
            Assert.True(
                generated.IsSuccess,
                $"seed={seed}; {generated.Witness?.Message}");
            var oracleCandidates = ExactSmallOracle.Generate(fixture.State)
                [AlgorithmTestData.VehicleOne];
            var expected = oracleCandidates
                .Where(
                    value => OracleAllows(
                        fixture.State,
                        value.SemanticKey,
                        fixture.IncumbentId,
                        fixture.BaselinePickup,
                        fixture.BaselineDrop,
                        fixture.HardEtaLimit))
                .Select(value => value.SemanticKey)
                .Order(StringComparer.Ordinal)
                .ToArray();
            var filter = new CommitmentCandidateFilter(
                fixture.BeforeEventState,
                new CommitmentPolicyCatalog([fixture.Policy]),
                NoDistances.Instance,
                $"commitment-oracle-{seed}",
                1);
            var generatedByVehicle = Assert.IsAssignableFrom<
                IReadOnlyList<VehicleCandidateSet>>(
                generated.VehicleCandidates);
            var retained = Assert.Single(
                filter.Filter(
                    fixture.State,
                    generatedByVehicle));
            var actual = retained.Candidates
                .Select(SemanticKey)
                .Order(StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void Loosening_eta_budget_never_removes_an_exact_small_candidate()
    {
        const int publishedSeedCount = 16;

        for (var seed = 0; seed < publishedSeedCount; seed++)
        {
            var fixture = CreateState(seed);
            var generated = new InsertionCandidateGenerator().Generate(
                fixture.State,
                CandidateGenerationOptions.ExactSmall);
            Assert.True(generated.IsSuccess, generated.Witness?.Message);
            var candidateSets = generated.VehicleCandidates!;
            var tight = RetainedKeys(
                fixture,
                candidateSets,
                EtaPolicy(40));
            var loose = RetainedKeys(
                fixture,
                candidateSets,
                EtaPolicy(160));

            Assert.All(
                tight,
                key => Assert.Contains(key, loose));
        }
    }

    private static IReadOnlySet<string> RetainedKeys(
        Fixture fixture,
        IReadOnlyList<VehicleCandidateSet> candidates,
        CommitmentPolicy policy)
    {
        var filter = new CommitmentCandidateFilter(
            fixture.BeforeEventState,
            new CommitmentPolicyCatalog([policy]),
            NoDistances.Instance,
            "commitment-monotonicity",
            1);
        return filter.Filter(fixture.State, candidates)
            .Single()
            .Candidates
            .Select(SemanticKey)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static CommitmentPolicy EtaPolicy(long hardLimit) =>
        new(
            "uniform-v1",
            CommitmentBudgetBasis.DecisionInduced,
            CommitmentDimensionVocabulary.Ordered.Select(
                dimension => new CommitmentDimensionLimit(
                    dimension,
                    dimension is CommitmentDimension.PickupEtaTotalMs
                        or CommitmentDimension.DropEtaTotalMs
                        ? hardLimit
                        : null,
                    CommitmentPhase.AllActive)),
            new MaterialRevisionRule(1_000, null));

    private static Fixture CreateState(int seed)
    {
        var incumbent = AlgorithmTestData.PendingRequest(
            "incumbent",
            AlgorithmTestData.NodeOne,
            AlgorithmTestData.NodeTwo,
            latestPickup: 5_000,
            maxRideTime: 5_000);
        var dynamicOrigin = seed % 2 == 0
            ? AlgorithmTestData.NodeThree
            : AlgorithmTestData.NodeTwo;
        var dynamicDestination = dynamicOrigin == AlgorithmTestData.NodeThree
            ? AlgorithmTestData.NodeOne
            : AlgorithmTestData.NodeThree;
        var dynamicRequest = AlgorithmTestData.PendingRequest(
            "dynamic",
            dynamicOrigin,
            dynamicDestination,
            latestPickup: 5_000,
            maxRideTime: 5_000);
        var pickupStop = new RouteStop(
            new StopId("incumbent-pickup"),
            incumbent.OriginNodeId,
            RouteStopKind.Pickup,
            incumbent.Id,
            new Duration(0));
        var dropStop = new RouteStop(
            new StopId("incumbent-drop"),
            incumbent.DestinationNodeId,
            RouteStopKind.DropOff,
            incumbent.Id,
            new Duration(0));
        var state = AlgorithmTestData.CreateState(
            [incumbent, dynamicRequest],
            [AlgorithmTestData.Vehicle(mutableSuffix: [pickupStop, dropStop])],
            arcs: CompleteArcs(seed));
        var run = state.Run.AcceptRequest(
            incumbent.Id,
            AlgorithmTestData.VehicleOne).Value!;
        var beforeRun = run;
        run = run.AdvanceEpoch(1, run.SimulationTime).Value!;
        state = state with { Run = run, NextEventSequence = 2 };
        var baseline = OracleEtas(
            state,
            "E:incumbent-pickup|E:incumbent-drop",
            incumbent.Id);
        var projection = new PromiseProjection(
            incumbent.Id,
            AlgorithmTestData.VehicleOne,
            pickupStop.StopId,
            pickupStop.NodeId,
            dropStop.StopId,
            dropStop.NodeId,
            baseline.Pickup,
            baseline.Drop,
            [
                new PromiseServiceToken(
                    pickupStop.StopId,
                    incumbent.Id,
                    RouteStopKind.Pickup),
                new PromiseServiceToken(
                    dropStop.StopId,
                    incumbent.Id,
                    RouteStopKind.DropOff),
            ]);
        var ledger = CommitmentLedger.Empty.OpenInitial(
            $"oracle-initial-{seed}",
            projection,
            1,
            state.Run.SimulationTime,
            "INITIAL_ACCEPTANCE",
            2).Ledger!;
        state = state with { Commitments = ledger };
        var before = state with
        {
            Run = beforeRun,
            NextEventSequence = 1,
        };
        var limit = seed % 4 * 40L;
        var policy = new CommitmentPolicy(
            "uniform-v1",
            CommitmentBudgetBasis.DecisionInduced,
            CommitmentDimensionVocabulary.Ordered.Select(
                dimension => new CommitmentDimensionLimit(
                    dimension,
                    dimension is CommitmentDimension.PickupEtaTotalMs
                        or CommitmentDimension.DropEtaTotalMs
                        ? limit
                        : null,
                    CommitmentPhase.AllActive)),
            new MaterialRevisionRule(1_000, null));

        return new Fixture(
            before,
            state,
            incumbent.Id,
            baseline.Pickup,
            baseline.Drop,
            limit,
            policy);
    }

    private static bool OracleAllows(
        OnlineState state,
        string semanticKey,
        RequestId incumbentId,
        SimTime baselinePickup,
        SimTime baselineDrop,
        long hardLimit)
    {
        var candidate = OracleEtas(state, semanticKey, incumbentId);
        return Difference(candidate.Pickup, baselinePickup) <= hardLimit
            && Difference(candidate.Drop, baselineDrop) <= hardLimit;
    }

    private static (SimTime Pickup, SimTime Drop) OracleEtas(
        OnlineState state,
        string semanticKey,
        RequestId incumbentId)
    {
        var vehicle = state.Run.Vehicles[AlgorithmTestData.VehicleOne];
        var time = state.Run.SimulationTime;
        var node = ((NodePosition)vehicle.Position).NodeId;
        SimTime? incumbentPickup = null;
        SimTime? incumbentDrop = null;

        foreach (var token in semanticKey.Split('|'))
        {
            var kind = token[..1];
            var id = token[2..];
            RouteStop stop;

            if (kind == "E")
            {
                stop = vehicle.Route.MutableSuffix.Single(
                    value => value.StopId.Value == id);
            }
            else
            {
                var requestId = new RequestId(id);
                var request = state.Run.Requests[requestId];
                stop = new RouteStop(
                    new StopId($"oracle-{kind}-{id}"),
                    kind == "P"
                        ? request.OriginNodeId
                        : request.DestinationNodeId,
                    kind == "P"
                        ? RouteStopKind.Pickup
                        : RouteStopKind.DropOff,
                    requestId,
                    new Duration(0));
            }

            if (node != stop.NodeId)
            {
                Assert.True(
                    state.TravelTimes!.TryGetTravelTime(
                        node,
                        stop.NodeId,
                        out var travel));
                time += travel;
            }

            if (stop.Kind == RouteStopKind.Pickup)
            {
                var request = state.Run.Requests[stop.RequestId!.Value];

                if (time.Milliseconds < request.EarliestPickup.Milliseconds)
                {
                    time = request.EarliestPickup;
                }
            }

            if (stop.RequestId == incumbentId
                && stop.Kind == RouteStopKind.Pickup)
            {
                incumbentPickup = time;
            }

            if (stop.RequestId == incumbentId
                && stop.Kind == RouteStopKind.DropOff)
            {
                incumbentDrop = time;
            }

            time += stop.ServiceDuration;
            node = stop.NodeId;
        }

        return (
            incumbentPickup
                ?? throw new InvalidOperationException("Oracle lost pickup."),
            incumbentDrop
                ?? throw new InvalidOperationException("Oracle lost drop-off."));
    }

    private static IReadOnlyList<KeyValuePair<TravelArc, Duration>> CompleteArcs(
        int seed)
    {
        var nodes = new[]
        {
            AlgorithmTestData.NodeZero,
            AlgorithmTestData.NodeOne,
            AlgorithmTestData.NodeTwo,
            AlgorithmTestData.NodeThree,
        };
        var arcs = new List<KeyValuePair<TravelArc, Duration>>();

        for (var from = 0; from < nodes.Length; from++)
        {
            for (var to = 0; to < nodes.Length; to++)
            {
                if (from == to)
                {
                    continue;
                }

                arcs.Add(
                    new KeyValuePair<TravelArc, Duration>(
                        new TravelArc(nodes[from], nodes[to]),
                        new Duration(
                            70 + ((from + 1) * 19 + (to + 1) * 23 + seed)
                                % 90)));
            }
        }

        return arcs;
    }

    private static long Difference(SimTime left, SimTime right) =>
        Math.Abs(left.Milliseconds - right.Milliseconds);

    private static string SemanticKey(InsertionCandidate candidate) =>
        string.Join(
            "|",
            candidate.Route.MutableSuffix.Select(
                stop => stop.RequestId is RequestId requestId
                    && candidate.NewRequestIds.Contains(requestId)
                    ? $"{(stop.Kind == RouteStopKind.Pickup ? "P" : "D")}:" +
                        requestId.Value
                    : $"E:{stop.StopId.Value}"));

    private sealed record Fixture(
        OnlineState BeforeEventState,
        OnlineState State,
        RequestId IncumbentId,
        SimTime BaselinePickup,
        SimTime BaselineDrop,
        long HardEtaLimit,
        CommitmentPolicy Policy);

    private sealed class NoDistances : IStopDistanceLookup
    {
        public static NoDistances Instance { get; } = new();

        public bool TryGetDistanceMillimeters(
            NodeId fromNodeId,
            NodeId toNodeId,
            out long distanceMillimeters)
        {
            distanceMillimeters = 0;
            return false;
        }
    }
}
