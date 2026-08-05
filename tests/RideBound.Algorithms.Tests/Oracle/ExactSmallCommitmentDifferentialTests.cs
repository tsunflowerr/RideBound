using RideBound.Algorithms.Candidates;
using RideBound.Algorithms.Commitments;
using RideBound.Algorithms.Policies;
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
    public void B2_assesses_every_raw_candidate_without_hard_budget_pruning()
    {
        var fixture = CreateState(seed: 0);
        var generated = new InsertionCandidateGenerator().Generate(
            fixture.State,
            CandidateGenerationOptions.ExactSmall);
        Assert.True(generated.IsSuccess, generated.Witness?.Message);
        var raw = generated.VehicleCandidates!;
        var rawIds = raw.Single().Candidates
            .Select(candidate => candidate.CandidateId)
            .ToArray();
        var context = MechanismContext(fixture, "b2-assessment");

        var assessed = new CommitmentCandidateAssessor()
            .AssessRevisionPenalty(context, raw);
        var hardFiltered = new CommitmentCandidateFilter(
            fixture.BeforeEventState,
            new CommitmentPolicyCatalog([fixture.Policy]),
            NoDistances.Instance,
            "hard-reference",
            1).Filter(fixture.State, raw);

        Assert.True(assessed.IsSuccess, assessed.Witness?.Message);
        Assert.Equal(rawIds.Length, assessed.Assessments!.Count);
        Assert.Contains(
            assessed.Assessments.Values,
            assessment => assessment.DecisionInducedRevision
                != CommitmentVector.Zero);
        Assert.True(hardFiltered.Single().Candidates.Count < rawIds.Length);
        Assert.Equal(
            rawIds,
            raw.Single().Candidates.Select(candidate => candidate.CandidateId));

        var decision = new RevisionPenaltyPolicy().Decide(
            context,
            CandidateGenerationOptions.ExactSmall);
        Assert.True(decision.IsSuccess, decision.Witness?.Message);
        Assert.NotNull(decision.Decision!.DecisionInducedRevision);
    }

    [Fact]
    public void B2_preserves_the_raw_candidate_set_across_published_small_seeds()
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
            var raw = generated.VehicleCandidates!;

            var assessed = new CommitmentCandidateAssessor()
                .AssessRevisionPenalty(
                    MechanismContext(fixture, $"b2-seed-{seed}"),
                    raw);

            Assert.True(
                assessed.IsSuccess,
                $"seed={seed}; {assessed.Witness?.Message}");
            Assert.Equal(
                raw.Sum(set => set.Candidates.Count),
                assessed.Assessments!.Count);
        }
    }

    [Fact]
    public void B3_freezes_only_inside_explicit_horizon_and_never_uses_source_budgets()
    {
        var fixture = CreateState(seed: 0);
        var generated = new InsertionCandidateGenerator().Generate(
            fixture.State,
            CandidateGenerationOptions.ExactSmall);
        Assert.True(generated.IsSuccess, generated.Witness?.Message);
        var raw = generated.VehicleCandidates!;
        var context = MechanismContext(fixture, "b3-freeze");
        var timeToPickup = fixture.BaselinePickup.Milliseconds
            - fixture.State.Run.SimulationTime.Milliseconds;
        Assert.True(timeToPickup > 1);
        var outsidePolicies = MechanismCommitmentPolicyProvider.FixedFreeze(
            context.Policies,
            new Duration(timeToPickup - 1),
            PromiseLock.PickupEta | PromiseLock.DropEta);
        var insidePolicies = MechanismCommitmentPolicyProvider.FixedFreeze(
            context.Policies,
            new Duration(timeToPickup),
            PromiseLock.PickupEta | PromiseLock.DropEta);
        var outside = new CommitmentCandidateFilter(
            fixture.BeforeEventState,
            outsidePolicies,
            NoDistances.Instance,
            "b3-outside",
            1).Filter(fixture.State, raw);
        var inside = new CommitmentCandidateFilter(
            fixture.BeforeEventState,
            insidePolicies,
            NoDistances.Instance,
            "b3-inside",
            1).Filter(fixture.State, raw);

        Assert.Equal(
            raw.Single().Candidates.Count,
            outside.Single().Candidates.Count);
        Assert.True(
            inside.Single().Candidates.Count < raw.Single().Candidates.Count);
        Assert.Contains(
            inside.Single().PrunedCandidates,
            witness => witness.Code == CommitmentFailureCodes.PhaseLock);

        var decision = new FixedFreezeHorizonPolicy(
            new Duration(timeToPickup),
            PromiseLock.PickupEta | PromiseLock.DropEta).Decide(
                context,
                CandidateGenerationOptions.ExactSmall);
        Assert.True(decision.IsSuccess, decision.Witness?.Message);
    }

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

    [Fact]
    public void C1_assessor_matches_independent_filter_and_kills_hard_gate_removal_mutation()
    {
        var fixture = CreateState(seed: 3);
        var generated = new InsertionCandidateGenerator().Generate(
            fixture.State,
            CandidateGenerationOptions.ExactSmall);
        Assert.True(generated.IsSuccess, generated.Witness?.Message);
        var raw = generated.VehicleCandidates!;
        var context = MechanismContext(fixture, "c1-assessment");

        var assessed = new HardVectorCandidateAssessor().AssessAndFilter(
            context,
            raw);
        var reference = new CommitmentCandidateFilter(
            fixture.BeforeEventState,
            context.Policies,
            context.StopDistances,
            "c1-reference",
            1).Filter(fixture.State, raw);

        Assert.True(assessed.IsSuccess, assessed.Witness?.Message);
        Assert.True(
            raw.Single().Candidates.Count
                > assessed.Batch!.FeasibleCandidateSets.Single().Candidates.Count,
            "The published fixture must contain a hard-invalid candidate so removing the hard gate is killed.");
        Assert.Equal(
            reference.Single().Candidates.Select(value => value.CandidateId),
            assessed.Batch.FeasibleCandidateSets.Single().Candidates
                .Select(value => value.CandidateId));
        Assert.All(
            assessed.Batch.Assessments.Values,
            value => Assert.InRange(
                value.WorstHardUtilizationPartsPerMillion,
                0,
                HardVectorCandidateAssessor.PartsPerMillion));
        Assert.Contains(
            assessed.Batch.Assessments.Values,
            value => value.WorstHardUtilizationPartsPerMillion > 0);

        var decision = new RideBoundHardVectorPolicy().Decide(
            context,
            CandidateGenerationOptions.ExactSmall);
        Assert.True(decision.IsSuccess, decision.Witness?.Message);
        Assert.NotNull(decision.Decision!.WorstHardUtilizationPartsPerMillion);
    }

    [Fact]
    public void C1_normalization_uses_exact_ceiling_without_long_overflow()
    {
        Assert.Equal(
            333_334,
            HardVectorCandidateAssessor.CeilingPartsPerMillion(1, 3));
        Assert.Equal(
            1_000_000,
            HardVectorCandidateAssessor.CeilingPartsPerMillion(
                DomainLimits.MaxCanonicalInteger - 1,
                DomainLimits.MaxCanonicalInteger));
        Assert.Equal(
            1_000_000,
            HardVectorCandidateAssessor.CeilingPartsPerMillion(0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => HardVectorCandidateAssessor.CeilingPartsPerMillion(2, 1));
    }

    [Fact]
    public void C1_with_unbounded_limits_and_no_locks_is_semantically_equal_to_B1()
    {
        var fixture = CreateState(seed: 2);
        var policy = new CommitmentPolicy(
            "uniform-v1",
            CommitmentBudgetBasis.DecisionInduced,
            CommitmentDimensionVocabulary.Ordered.Select(
                dimension => new CommitmentDimensionLimit(
                    dimension,
                    null,
                    CommitmentPhase.AllActive)),
            new MaterialRevisionRule(1_000, null));
        var context = new CommitmentMechanismContext(
            fixture.BeforeEventState,
            fixture.State,
            new CommitmentPolicyCatalog([policy]),
            NoDistances.Instance,
            "c1-unbounded-equivalence",
            1);

        var b1 = new RollingCostPolicy().Decide(
            fixture.State,
            CandidateGenerationOptions.ExactSmall);
        var c1 = new RideBoundHardVectorPolicy().Decide(
            context,
            CandidateGenerationOptions.ExactSmall);

        Assert.True(b1.IsSuccess, b1.Witness?.Message);
        Assert.True(c1.IsSuccess, c1.Witness?.Message);
        Assert.Equal(b1.Decision!.RequestActions, c1.Decision!.RequestActions);
        Assert.Equal(
            b1.Decision.VehiclePlans.Select(value => value.Candidate.CandidateId),
            c1.Decision.VehiclePlans.Select(value => value.Candidate.CandidateId));
        Assert.All(
            b1.Decision.VehiclePlans.Zip(c1.Decision.VehiclePlans),
            pair => Assert.True(
                pair.First.Candidate.Route.IsSemanticallyEqual(
                    pair.Second.Candidate.Route)));
    }

    [Fact]
    public void C2_keeps_the_exact_C1_hard_feasible_set_and_computes_warning_excess()
    {
        var fixture = CreateState(seed: 3);
        var context = MechanismContext(fixture, "c2-warning");
        var generated = new InsertionCandidateGenerator().Generate(
            fixture.State,
            CandidateGenerationOptions.ExactSmall);
        Assert.True(generated.IsSuccess, generated.Witness?.Message);
        var raw = generated.VehicleCandidates!;
        var warnings = new CommitmentWarningProfileCatalog(
            [WarningProfile(10)]);

        var c1 = new HardVectorCandidateAssessor().AssessAndFilter(context, raw);
        var c2 = new HardVectorCandidateAssessor().AssessAndFilter(
            context,
            raw,
            warnings);

        Assert.True(c1.IsSuccess, c1.Witness?.Message);
        Assert.True(c2.IsSuccess, c2.Witness?.Message);
        Assert.Equal(
            c1.Batch!.FeasibleCandidateSets.Single().Candidates
                .Select(value => value.CandidateId),
            c2.Batch!.FeasibleCandidateSets.Single().Candidates
                .Select(value => value.CandidateId));
        Assert.Contains(
            c2.Batch.Assessments.Values,
            value => value.WarningExcess != CommitmentVector.Zero);

        var decision = new CommitSoftHardHybridPolicy(warnings).Decide(
            context,
            CandidateGenerationOptions.ExactSmall);
        Assert.True(decision.IsSuccess, decision.Witness?.Message);
        Assert.NotNull(decision.Decision!.WarningExcess);
    }

    [Fact]
    public void C2_with_all_warnings_disabled_is_semantically_equal_to_C1()
    {
        var fixture = CreateState(seed: 3);
        var context = MechanismContext(fixture, "c2-disabled");
        var disabled = new CommitmentWarningProfileCatalog(
            [WarningProfile(null)]);

        var c1 = new RideBoundHardVectorPolicy().Decide(
            context,
            CandidateGenerationOptions.ExactSmall);
        var c2 = new CommitSoftHardHybridPolicy(disabled).Decide(
            context,
            CandidateGenerationOptions.ExactSmall);

        Assert.True(c1.IsSuccess, c1.Witness?.Message);
        Assert.True(c2.IsSuccess, c2.Witness?.Message);
        Assert.Equal(c1.Decision!.RequestActions, c2.Decision!.RequestActions);
        Assert.Equal(
            c1.Decision.VehiclePlans.Select(value => value.Candidate.CandidateId),
            c2.Decision.VehiclePlans.Select(value => value.Candidate.CandidateId));
        Assert.Equal(
            c1.Decision.WorstHardUtilizationPartsPerMillion,
            c2.Decision.WorstHardUtilizationPartsPerMillion);
        Assert.Null(c2.Decision.WarningExcess);
    }

    [Fact]
    public void C2_rejects_warning_above_its_finite_hard_limit()
    {
        var fixture = CreateState(seed: 3);
        var context = MechanismContext(fixture, "c2-invalid-warning");
        var generated = new InsertionCandidateGenerator().Generate(
            fixture.State,
            CandidateGenerationOptions.ExactSmall);
        Assert.True(generated.IsSuccess, generated.Witness?.Message);
        var invalid = new CommitmentWarningProfileCatalog(
            [WarningProfile(fixture.HardEtaLimit + 1)]);

        var assessed = new HardVectorCandidateAssessor().AssessAndFilter(
            context,
            generated.VehicleCandidates!,
            invalid);

        Assert.False(assessed.IsSuccess);
        Assert.Equal(
            "INVALID_COMMITMENT_WARNING_LIMIT",
            assessed.Witness!.Code);
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

    private static CommitmentWarningProfile WarningProfile(long? etaWarning) =>
        new(
            "uniform-v1",
            CommitmentDimensionVocabulary.Ordered.Select(
                dimension => new CommitmentWarningLimit(
                    dimension,
                    dimension is CommitmentDimension.PickupEtaTotalMs
                        or CommitmentDimension.DropEtaTotalMs
                        ? etaWarning
                        : null)));

    private static CommitmentMechanismContext MechanismContext(
        Fixture fixture,
        string scope) =>
        new(
            fixture.BeforeEventState,
            fixture.State,
            new CommitmentPolicyCatalog([fixture.Policy]),
            NoDistances.Instance,
            scope,
            1);

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
