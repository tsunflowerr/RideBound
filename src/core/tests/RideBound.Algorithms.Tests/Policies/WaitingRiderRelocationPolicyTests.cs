using RideBound.Algorithms.Candidates;
using RideBound.Algorithms.Commitments;
using RideBound.Algorithms.Policies;
using RideBound.Application.Commitments;
using RideBound.Application.Optimization;
using RideBound.Application.Promises;
using RideBound.Application.Scheduling;
using RideBound.Application.State;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;
using RideBound.Domain.Validation;
using RideBound.Domain.Vehicles;

namespace RideBound.Algorithms.Tests.Policies;

/// <summary>
/// G2 end to end below the Runner: generation, pair assessment, selection and apply, then the
/// Runner's full-fleet validation of the proposed state. The waiting rider w rides vehicle-1
/// (at node-0; pickup node-1, drop node-2, completion 220 ms); vehicle-2 idles at node-1, w's
/// origin, so moving w there completes it in 110 ms and empties vehicle-1. Cost-first takes
/// the move; without G2 the same state keeps w where it is.
/// </summary>
public sealed class WaitingRiderRelocationPolicyTests
{
    private static readonly RequestId Waiting = new("waiting");

    [Fact]
    public void Cost_first_moves_the_waiting_rider_and_validates_the_fleet()
    {
        var fixture = Fixture(relocation: true);
        var solver = new EnumeratingSolver();

        var result = new SolverBackedRidePoolingPolicy(solver).Decide(
            fixture.Context,
            Generation(relocation: true),
            CostFirst());

        Assert.True(result.IsSuccess, result.Witness?.Message);
        var decision = result.Decision!.Decision;
        var proposed = decision.ProposedState.Run;
        Assert.Equal(AlgorithmTestData.VehicleTwo, proposed.Requests[Waiting].AssignedVehicleId);
        Assert.Equal(RequestLifecycle.WaitingPickup, proposed.Requests[Waiting].Lifecycle);
        Assert.Empty(proposed.Vehicles[AlgorithmTestData.VehicleOne].Route.MutableSuffix);
        Assert.Empty(proposed.Vehicles[AlgorithmTestData.VehicleOne].AcceptedRequestIds);
        Assert.Equal([Waiting], proposed.Vehicles[AlgorithmTestData.VehicleTwo].AcceptedRequestIds);
        Assert.Equal(
            [RelocationRole.Donor, RelocationRole.Receiver],
            decision.VehiclePlans
                .OrderBy(plan => plan.VehicleId.Value, StringComparer.Ordinal)
                .Select(plan => plan.Candidate.RelocationRole));
        Assert.Contains(solver.LastProblem!.ObjectiveLevels, level => level.Name == "relocation-candidate-count");

        // Validate the proposed state again with the independent commitment validator.
        var final = new CommitmentDecisionValidator().Validate(
            new CommitmentValidationContext(
                fixture.Context.BeforeEventState,
                fixture.Context.ReducedState,
                decision.ProposedState,
                fixture.Context.Policies,
                fixture.Context.StopDistances,
                fixture.Context.PublicationScope,
                fixture.Context.SourceEventSequence));

        Assert.True(final.IsValid, string.Join("; ", final.Witnesses.Select(value => value.Message)));
        var entry = Assert.Single(final.Publications).Entry;
        var validated = result.Decision.CommitmentValidation;
        Assert.True(validated.IsValid);
        Assert.Equal(
            AlgorithmTestData.VehicleTwo,
            Assert.Single(validated.Publications).Entry.PublishedPromise.Projection.VehicleId);
        Assert.Equal(
            AlgorithmTestData.VehicleTwo,
            validated.ValidatedState!.Run.Requests[Waiting].AssignedVehicleId);
        Assert.Equal(AlgorithmTestData.VehicleTwo, entry.PublishedPromise.Projection.VehicleId);
        Assert.Equal(1, entry.BudgetAfter.VehicleSwitchCount);
        Assert.True(entry.PublishedPromise.Projection.PickupEta.Milliseconds
            <= entry.ExogenousProjection.PickupEta.Milliseconds);
    }

    [Fact]
    public void A_donor_assessment_does_not_depend_on_its_partner_receiver()
    {
        // DESIGN 3.3: with two possible receivers (vehicle-2 and vehicle-3, both idle at w's origin),
        // the donor-only route is assessed identically whichever receivers exist.
        var both = Assessed(Fixture(relocation: true, thirdVehicleAt: AlgorithmTestData.NodeOne).Context, dropVehicle: null);
        var onlyThird = Assessed(Fixture(relocation: true, thirdVehicleAt: AlgorithmTestData.NodeOne).Context, dropVehicle: AlgorithmTestData.VehicleTwo);

        var donorOnly = both.Batch!.FeasibleCandidateSets
            .Single(set => set.VehicleId == AlgorithmTestData.VehicleOne)
            .Candidates
            .Single(value => value.RelocationRole == RelocationRole.Donor && value.NewRequestIds.Count == 0);
        Assert.Equal(both.Batch.Assessments[donorOnly.CandidateId], onlyThird.Batch!.Assessments[donorOnly.CandidateId]);
        Assert.Equal(
            2,
            both.Batch.FeasibleCandidateSets
                .SelectMany(set => set.Candidates)
                .Count(value => value.RelocationRole == RelocationRole.Receiver));
    }

    private static HardVectorCandidateAssessmentResult Assessed(CommitmentMechanismContext context, VehicleId? dropVehicle)
    {
        var generated = new InsertionCandidateGenerator().Generate(context.ReducedState, Generation(relocation: true));
        Assert.True(generated.IsSuccess, generated.Witness?.Message);
        var sets = generated.VehicleCandidates!
            .Select(set => set.VehicleId == dropVehicle
                ? set with { Candidates = set.Candidates.Where(value => value.RelocationRole is null).ToArray() }
                : set)
            .ToArray();
        var result = new HardVectorCandidateAssessor().AssessAndFilter(context, sets, requireSafetyNoOp: true);
        Assert.True(result.IsSuccess, result.Witness?.Message);
        return result;
    }

    [Fact]
    public void Without_G2_the_same_state_keeps_the_rider_and_the_archived_levels()
    {
        var fixture = Fixture(relocation: false);
        var solver = new EnumeratingSolver();

        var result = new SolverBackedRidePoolingPolicy(solver).Decide(
            fixture.Context,
            Generation(relocation: false),
            CostFirst());

        Assert.True(result.IsSuccess, result.Witness?.Message);
        var proposed = result.Decision!.Decision.ProposedState.Run;
        Assert.Equal(AlgorithmTestData.VehicleOne, proposed.Requests[Waiting].AssignedVehicleId);
        Assert.DoesNotContain(solver.LastProblem!.ObjectiveLevels, level => level.Name == "relocation-candidate-count");
        Assert.All(solver.LastProblem.Options, option => Assert.Null(option.RelocationOut ?? option.RelocationIn));
    }

    [Fact]
    public void A_move_that_would_delay_the_pickup_is_pruned_by_the_ratchet()
    {
        // vehicle-2 now idles at node-3: 120 ms to the origin, later than the kept 110 ms.
        var fixture = Fixture(relocation: true, receiverPosition: AlgorithmTestData.NodeThree);
        var solver = new EnumeratingSolver();

        var result = new SolverBackedRidePoolingPolicy(solver).Decide(
            fixture.Context,
            Generation(relocation: true),
            CostFirst());

        Assert.True(result.IsSuccess, result.Witness?.Message);
        var decision = result.Decision!.Decision;
        Assert.Equal(AlgorithmTestData.VehicleOne, decision.ProposedState.Run.Requests[Waiting].AssignedVehicleId);
        var receiverPrune = Assert.Single(
            decision.PrunedCandidates,
            value => value.VehicleId == AlgorithmTestData.VehicleTwo && value.Code == CommitmentFailureCodes.PhaseLock);
        Assert.Contains(receiverPrune.CommitmentWitnesses!, value => value.Dimension == "pickup_eta_ms");
        Assert.Contains(
            decision.PrunedCandidates,
            value => value.VehicleId == AlgorithmTestData.VehicleOne
                && value.Code == HardVectorCandidateAssessor.RelocationNoReceiver);
    }

    [Fact]
    public void Equal_cost_prefers_keeping_the_waiting_rider_after_considering_both_routes()
    {
        var fixture = Fixture(relocation: true, receiverPosition: AlgorithmTestData.NodeZero);
        var solver = new EnumeratingSolver();

        var result = new SolverBackedRidePoolingPolicy(solver).Decide(
            fixture.Context, Generation(relocation: true), CostFirst());

        Assert.True(result.IsSuccess, result.Witness?.Message);
        Assert.Collection(
            solver.LastProblem!.ObjectiveLevels.Take(3),
            level => Assert.Equal("accepted-request-count", level.Name),
            level => Assert.Equal("operational-cost", level.Name),
            level => Assert.Equal("relocation-candidate-count", level.Name));
        Assert.Contains(solver.LastProblem.Options, option => option.RelocationOut == Waiting);
        Assert.Contains(solver.LastProblem.Options, option => option.RelocationIn == Waiting);
        Assert.Equal(
            AlgorithmTestData.VehicleOne,
            result.Decision!.Decision.ProposedState.Run.Requests[Waiting].AssignedVehicleId);
        Assert.Empty(result.Decision.CommitmentValidation.Publications);
    }

    [Fact]
    public void Relocation_generation_is_charged_against_the_fleet_budget()
    {
        var fixture = Fixture(relocation: true);
        var solver = new EnumeratingSolver();
        var state = fixture.Context.ReducedState;
        var generated = new InsertionCandidateGenerator().Generate(state, Generation(relocation: true));
        var ordinaryWork = generated.Diagnostics!.VehicleLosses.Sum(loss => loss.ExplorationWorkUnits);

        var result = new SolverBackedRidePoolingPolicy(solver).Decide(
            fixture.Context, Generation(relocation: true), CostFirst(generationBudget: ordinaryWork));

        Assert.False(result.IsSuccess);
        Assert.Equal(CandidateSelectionFailureCodes.InvalidExecutionAccounting, result.Witness?.Code);
        Assert.Null(solver.LastProblem);
    }

    [Fact]
    public void Solver_without_an_incumbent_returns_the_validated_no_op_fleet()
    {
        var fixture = Fixture(relocation: true);
        var solver = new EnumeratingSolver(returnUnknown: true);

        var result = new SolverBackedRidePoolingPolicy(solver).Decide(
            fixture.Context, Generation(relocation: true), CostFirst());

        Assert.True(result.IsSuccess, result.Witness?.Message);
        Assert.Contains(solver.LastProblem!.Options, option => option.RelocationOut == Waiting);
        var decision = result.Decision!.Decision;
        Assert.Equal(CandidateSelectionSolveStatus.SafeFallback, decision.SelectionExecution!.SolveResult.Status);
        Assert.Equal(AlgorithmTestData.VehicleOne, decision.ProposedState.Run.Requests[Waiting].AssignedVehicleId);
        Assert.All(decision.VehiclePlans, plan => Assert.True(plan.Candidate.IsNoOp));
        Assert.True(result.Decision.CommitmentValidation.IsValid);
    }

    private static CandidateGenerationOptions Generation(bool relocation) =>
        new(
            100,
            2,
            waitingRiderRelocation: relocation ? new WaitingRiderRelocationGenerationOptions() : null);

    private static SolverBackedRidePoolingPolicyOptions CostFirst(long generationBudget = 100_000) =>
        new(
            RidePoolingPolicyKind.RideBoundHardVector,
            DeterministicCandidateSelectionExecutionBudget.Create(
                generationBudget,
                100_000,
                DeterministicSolverBudget.Create(100_000, 100_000, 1).Value!).Value!);

    private static (CommitmentMechanismContext Context, CommitmentPolicy Policy) Fixture(
        bool relocation,
        NodeId? receiverPosition = null,
        NodeId? thirdVehicleAt = null)
    {
        var request = AlgorithmTestData.PendingRequest(Waiting.Value, AlgorithmTestData.NodeOne, AlgorithmTestData.NodeTwo);
        var pickup = new RouteStop(new StopId("waiting-pickup"), request.OriginNodeId, RouteStopKind.Pickup, request.Id, new Duration(0));
        var drop = new RouteStop(new StopId("waiting-drop"), request.DestinationNodeId, RouteStopKind.DropOff, request.Id, new Duration(0));
        var vehicles = new List<VehicleState>
        {
            AlgorithmTestData.Vehicle(mutableSuffix: [pickup, drop]),
            AlgorithmTestData.Vehicle(AlgorithmTestData.VehicleTwo, position: receiverPosition ?? AlgorithmTestData.NodeOne),
        };

        if (thirdVehicleAt is NodeId third)
        {
            vehicles.Add(AlgorithmTestData.Vehicle(new VehicleId("vehicle-3"), position: third));
        }

        var state = AlgorithmTestData.CreateState([request], vehicles);
        var run = state.Run.AcceptRequest(request.Id, AlgorithmTestData.VehicleOne).Value!;
        run = run.ConfirmWaitingPickup(request.Id).Value!;
        var beforeRun = run;
        run = run.AdvanceEpoch(1, run.SimulationTime).Value!;
        var vehicle = run.Vehicles[AlgorithmTestData.VehicleOne];
        var schedule = new RouteScheduleProjector().Project(run, vehicle, vehicle.Route, state.TravelTimes!, run.SimulationTime).Schedule!;
        var initial = new PromiseProjector().Project(run, vehicle, vehicle.Route, schedule, request.Id).Value!;
        var ledger = CommitmentLedger.Empty.OpenInitial(
            "initial-waiting",
            initial,
            1,
            run.SimulationTime,
            "INITIAL_ACCEPTANCE",
            1).Ledger!;
        var reduced = state with { Run = run, NextEventSequence = 2, Commitments = ledger };
        var before = reduced with { Run = beforeRun, NextEventSequence = 1 };
        var policy = new CommitmentPolicy(
            "uniform-v1",
            CommitmentDimensionVocabulary.Ordered.Select(
                dimension => new CommitmentDimensionLimit(dimension, null, CommitmentPhase.AllActive)),
            new MaterialRevisionRule(1_000, null),
            finalConfirmationLocks: relocation
                ? PromiseLock.PickupStop | PromiseLock.PickupEta
                : PromiseLock.Vehicle | PromiseLock.PickupStop | PromiseLock.PickupEta,
            ratchetLocks: relocation ? PromiseLock.PickupEta : PromiseLock.None,
            waitingRiderRelocation: relocation);

        return (
            new CommitmentMechanismContext(
                before,
                reduced,
                new CommitmentPolicyCatalog([policy]),
                NoDistances.Instance,
                "g2-policy",
                1),
            policy);
    }

    /// <summary>Exhaustive lexicographic solver over every per-vehicle choice (test oracle).</summary>
    private sealed class EnumeratingSolver(bool returnUnknown = false) : ICandidateSelectionSolver
    {
        public CandidateSelectionProblem? LastProblem { get; private set; }

        public CandidateSelectionSolveResult Solve(
            CandidateSelectionProblem problem,
            DeterministicSolverBudget budget)
        {
            LastProblem = problem;
            if (returnUnknown)
            {
                var noIncumbent = CandidateSelectionSolverDiagnostics.Create(
                    problem, budget, 0, 0, 0, []).Value!;
                return CandidateSelectionSolveResult.Unknown(
                    noIncumbent, "TEST_NO_INCUMBENT", "No incumbent in this test.");
            }

            CandidateSelectionSolution? best = null;
            Enumerate(0, []);

            var diagnostics = CandidateSelectionSolverDiagnostics.Create(
                problem,
                budget,
                1,
                1,
                0,
                best is null
                    ? []
                    : problem.ObjectiveLevels
                        .Select((level, index) => ObjectiveSolveBound.Create(
                            index,
                            level,
                            best.ObjectiveValues[index],
                            best.ObjectiveValues[index]).Value!)
                        .ToArray()).Value!;
            return best is null
                ? CandidateSelectionSolveResult.Infeasible(diagnostics, "TEST_INFEASIBLE", "No assignment exists.")
                : CandidateSelectionSolveResult.Optimal(best, diagnostics);

            void Enumerate(int vehicleIndex, IReadOnlyList<string> selected)
            {
                if (vehicleIndex == problem.VehicleIds.Count)
                {
                    var solution = CandidateSelectionSolution.Create(problem, selected);

                    if (solution.IsSuccess
                        && (best is null
                            || LexicographicObjectiveComparer.Compare(
                                solution.Value!.ObjectiveValues,
                                best.ObjectiveValues,
                                problem.ObjectiveLevels) < 0))
                    {
                        best = solution.Value;
                    }

                    return;
                }

                foreach (var option in problem.Options.Where(
                             value => value.VehicleId == problem.VehicleIds[vehicleIndex]))
                {
                    Enumerate(vehicleIndex + 1, selected.Append(option.OptionId).ToArray());
                }
            }
        }
    }

    private sealed class NoDistances : IStopDistanceLookup
    {
        public static NoDistances Instance { get; } = new();

        public bool TryGetDistanceMillimeters(NodeId fromNodeId, NodeId toNodeId, out long distanceMillimeters)
        {
            distanceMillimeters = 0;
            return false;
        }
    }
}
