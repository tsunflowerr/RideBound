using RideBound.Algorithms.Candidates;
using RideBound.Algorithms.Commitments;
using RideBound.Algorithms.Policies;
using RideBound.Application.Commitments;
using RideBound.Application.Optimization;
using RideBound.Application.State;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Runs;
using RideBound.Domain.Validation;

namespace RideBound.Algorithms.Tests.Policies;

public sealed class SolverBackedFleetSelectionTests
{
    [Fact]
    public void Policy_registry_round_trips_every_unique_published_name()
    {
        var ids = Enum.GetValues<RidePoolingPolicyKind>()
            .Select(RidePoolingPolicyRegistry.ToPolicyId)
            .ToArray();

        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.All(
            Enum.GetValues<RidePoolingPolicyKind>(),
            kind =>
            {
                Assert.True(
                    RidePoolingPolicyRegistry.TryParse(
                        RidePoolingPolicyRegistry.ToPolicyId(kind),
                        out var parsed));
                Assert.Equal(kind, parsed);
            });
        Assert.False(RidePoolingPolicyRegistry.TryParse("unknown", out _));
    }

    [Fact]
    public void Rolling_cost_mapping_enforces_request_uniqueness_and_cost_order()
    {
        var request = new RequestId("request-1");
        var first = AlgorithmTestData.VehicleOne;
        var second = AlgorithmTestData.VehicleTwo;
        var sets = new[]
        {
            Set(first, Candidate("v1-noop", first, [], 0), Candidate("v1-accept", first, [request], 10)),
            Set(second, Candidate("v2-noop", second, [], 0), Candidate("v2-accept", second, [request], 20)),
        };

        var result = Select(sets, SolverBackedObjectiveProfile.RollingCost);

        Assert.True(result.IsSuccess, result.Witness?.Message);
        Assert.Equal(
            ["v1-accept", "v2-noop"],
            result.Selection!.Selection.VehiclePlans
                .Select(value => value.Candidate.CandidateId));
        Assert.Equal(1, result.Selection.Selection.AcceptedRequestCount);
        Assert.Equal(10, result.Selection.Selection.OperationalCost);
    }

    [Fact]
    public void Revision_mapping_preserves_material_then_dimension_hierarchy()
    {
        var request = new RequestId("request-1");
        var vehicle = AlgorithmTestData.VehicleOne;
        var noOp = Candidate("noop", vehicle, [], 0);
        var lowerRevision = Candidate("lower-revision", vehicle, [request], 100);
        var cheaper = Candidate("cheaper", vehicle, [request], 1);
        var material = Candidate("material", vehicle, [request], 0);
        var assessments = new Dictionary<string, CandidateCommitmentAssessment>(
            StringComparer.Ordinal)
        {
            [noOp.CandidateId] = Revision(noOp, CommitmentVector.Zero),
            [lowerRevision.CandidateId] = Revision(lowerRevision, Vector(pickup: 1)),
            [cheaper.CandidateId] = Revision(cheaper, Vector(pickup: 2)),
            [material.CandidateId] = Revision(material, Vector(material: 1)),
        };

        var result = Select(
            [Set(vehicle, noOp, lowerRevision, cheaper, material)],
            SolverBackedObjectiveProfile.RevisionPenalty,
            revisionAssessments: assessments);

        Assert.True(result.IsSuccess, result.Witness?.Message);
        Assert.Equal(
            "lower-revision",
            Assert.Single(result.Selection!.Selection.VehiclePlans)
                .Candidate.CandidateId);
        Assert.Equal(
            1,
            result.Selection.Selection.DecisionInducedRevision!.PickupEtaTotalMs);
    }

    [Fact]
    public void Hard_vector_mapping_uses_maximum_utilization_before_revision_and_cost()
    {
        var request = new RequestId("request-1");
        var vehicle = AlgorithmTestData.VehicleOne;
        var noOp = Candidate("noop", vehicle, [], 0);
        var highUtilization = Candidate("high-util", vehicle, [request], 0);
        var lowUtilization = Candidate("low-util", vehicle, [request], 100);
        var assessments = new Dictionary<string, HardVectorCandidateAssessment>(
            StringComparer.Ordinal)
        {
            [noOp.CandidateId] = Hard(noOp, 0, CommitmentVector.Zero),
            [highUtilization.CandidateId] = Hard(highUtilization, 900_000, CommitmentVector.Zero),
            [lowUtilization.CandidateId] = Hard(lowUtilization, 100_000, Vector(pickup: 999)),
        };

        var result = Select(
            [Set(vehicle, noOp, highUtilization, lowUtilization)],
            SolverBackedObjectiveProfile.HardVector,
            hardAssessments: assessments);

        Assert.True(result.IsSuccess, result.Witness?.Message);
        Assert.Equal(
            "low-util",
            Assert.Single(result.Selection!.Selection.VehiclePlans)
                .Candidate.CandidateId);
        Assert.Equal(100_000, result.Selection.Selection.WorstHardUtilizationPartsPerMillion);
    }

    [Fact]
    public void Soft_hard_mapping_places_warning_vector_before_revision_and_cost()
    {
        var request = new RequestId("request-1");
        var vehicle = AlgorithmTestData.VehicleOne;
        var noOp = Candidate("noop", vehicle, [], 0);
        var lowerWarning = Candidate("lower-warning", vehicle, [request], 100);
        var lowerRevision = Candidate("lower-revision", vehicle, [request], 1);
        var assessments = new Dictionary<string, HardVectorCandidateAssessment>(
            StringComparer.Ordinal)
        {
            [noOp.CandidateId] = Hard(
                noOp,
                0,
                CommitmentVector.Zero,
                warning: CommitmentVector.Zero),
            [lowerWarning.CandidateId] = Hard(
                lowerWarning,
                100_000,
                Vector(pickup: 10),
                warning: Vector(pickup: 1)),
            [lowerRevision.CandidateId] = Hard(
                lowerRevision,
                100_000,
                Vector(pickup: 1),
                warning: Vector(pickup: 2)),
        };

        var result = Select(
            [Set(vehicle, noOp, lowerWarning, lowerRevision)],
            SolverBackedObjectiveProfile.SoftHardHybrid,
            hardAssessments: assessments);

        Assert.True(result.IsSuccess, result.Witness?.Message);
        Assert.Equal(
            "lower-warning",
            Assert.Single(result.Selection!.Selection.VehiclePlans)
                .Candidate.CandidateId);
    }

    [Fact]
    public void Unbounded_hard_profile_is_semantically_rolling_cost()
    {
        var request = new RequestId("request-1");
        var vehicle = AlgorithmTestData.VehicleOne;
        var noOp = Candidate("noop", vehicle, [], 0);
        var cheapHighRevision = Candidate("cheap", vehicle, [request], 1);
        var expensiveLowRevision = Candidate("expensive", vehicle, [request], 100);
        var sets = new[] { Set(vehicle, noOp, cheapHighRevision, expensiveLowRevision) };
        var assessments = new Dictionary<string, HardVectorCandidateAssessment>(
            StringComparer.Ordinal)
        {
            [noOp.CandidateId] = Hard(noOp, 0, CommitmentVector.Zero, hasHard: false),
            [cheapHighRevision.CandidateId] = Hard(
                cheapHighRevision,
                1_000_000,
                Vector(pickup: 999),
                hasHard: false),
            [expensiveLowRevision.CandidateId] = Hard(
                expensiveLowRevision,
                0,
                CommitmentVector.Zero,
                hasHard: false),
        };

        var c1 = Select(
            sets,
            SolverBackedObjectiveProfile.HardVector,
            hardAssessments: assessments);
        var b1 = Select(sets, SolverBackedObjectiveProfile.RollingCost);

        Assert.Equal(
            Assert.Single(b1.Selection!.Selection.VehiclePlans).Candidate.CandidateId,
            Assert.Single(c1.Selection!.Selection.VehiclePlans).Candidate.CandidateId);
        Assert.Equal("cheap", Assert.Single(c1.Selection.Selection.VehiclePlans).Candidate.CandidateId);
        Assert.Null(c1.Selection.Selection.DecisionInducedRevision);
    }

    [Fact]
    public void Solver_incumbent_rejected_by_semantic_validator_uses_validated_no_op()
    {
        var request = new RequestId("request-1");
        var vehicle = AlgorithmTestData.VehicleOne;
        var sets = new[]
        {
            Set(
                vehicle,
                Candidate("noop", vehicle, [], 0),
                Candidate("accept", vehicle, [request], 1)),
        };
        var result = Select(
            sets,
            SolverBackedObjectiveProfile.RollingCost,
            validator: new FleetValidator(
                selection => selection.AcceptedRequestCount == 0));

        Assert.True(result.IsSuccess, result.Witness?.Message);
        Assert.Equal(
            CandidateSelectionSolveStatus.SafeFallback,
            result.Selection!.Execution.SolveResult.Status);
        Assert.Equal(
            "noop",
            Assert.Single(result.Selection.Selection.VehiclePlans).Candidate.CandidateId);
        Assert.True(result.Selection.Execution.Diagnostics.PrimaryIncumbentRejected);
    }

    [Fact]
    public void Bounded_generation_loss_reaches_execution_diagnostics_separately_from_solver_loss()
    {
        var firstRequest = AlgorithmTestData.PendingRequest("request-1");
        var secondRequest = AlgorithmTestData.PendingRequest(
            "request-2",
            AlgorithmTestData.NodeThree,
            AlgorithmTestData.NodeOne);
        var unadvanced = AlgorithmTestData.CreateState(
            [firstRequest, secondRequest],
            [AlgorithmTestData.Vehicle()]);
        var state = unadvanced with
        {
            Run = unadvanced.Run.AdvanceEpoch(
                1,
                unadvanced.Run.SimulationTime).Value!,
            NextEventSequence = 2,
        };
        var before = OnlineState.Create(
            RideBoundRun.Create(
                AlgorithmTestData.RunId,
                AlgorithmTestData.ScenarioId,
                new SimTime(0)),
            state.ExpectedInitialTravelTimeSnapshotHash);
        var policy = new CommitmentPolicy(
            "uniform-v1",
            CommitmentBudgetBasis.DecisionInduced,
            CommitmentDimensionVocabulary.Ordered.Select(
                dimension => new CommitmentDimensionLimit(
                    dimension,
                    null,
                    CommitmentPhase.AllActive)),
            new MaterialRevisionRule(1, null));
        var solverBudget = DeterministicSolverBudget.Create(
            1000,
            1000,
            1).Value!;
        var executionBudget =
            DeterministicCandidateSelectionExecutionBudget.Create(
                100_000,
                100_000,
                solverBudget).Value!;
        var result = new SolverBackedRidePoolingPolicy(
            new UnknownTestSolver()).Decide(
                new CommitmentMechanismContext(
                    before,
                    state,
                    new CommitmentPolicyCatalog([policy]),
                    NoDistances.Instance,
                    "bounded-loss",
                    1),
                new CandidateGenerationOptions(
                    maximumCandidatesPerVehicle: 100,
                    maximumNewRequestsPerVehicle: 1,
                    exactSmallMode: false,
                    maximumExplorationWorkUnits: 100_000),
                new SolverBackedRidePoolingPolicyOptions(
                    RidePoolingPolicyKind.RollingCost,
                    executionBudget));

        Assert.True(result.IsSuccess, result.Witness?.Message);
        Assert.Equal(1, result.Decision!.Decision.GenerationDiagnostics!.OmittedRequestCount);
        Assert.True(
            result.Decision.Decision.SelectionExecution!.Diagnostics
                .CandidateLossOccurred);
        Assert.True(
            result.Decision.Decision.SelectionExecution.Diagnostics
                .SolverLossOccurred);
        Assert.Equal(
            CandidateSelectionSolveStatus.SafeFallback,
            result.Decision.Decision.SelectionExecution.SolveResult.Status);
    }

    private static SolverBackedFleetSelectionResult Select(
        IReadOnlyList<VehicleCandidateSet> sets,
        SolverBackedObjectiveProfile profile,
        IReadOnlyDictionary<string, CandidateCommitmentAssessment>?
            revisionAssessments = null,
        IReadOnlyDictionary<string, HardVectorCandidateAssessment>?
            hardAssessments = null,
        IFleetSelectionValidator? validator = null)
    {
        var solverBudget = DeterministicSolverBudget.Create(1_000, 1_000, 1).Value!;
        var budget = DeterministicCandidateSelectionExecutionBudget.Create(
            1_000,
            1_000,
            solverBudget).Value!;
        var accounting = CandidateSelectionPreSolveAccounting.Create(
            budget,
            1,
            0,
            0).Value!;
        return new SolverBackedFleetSelector(new ExactTestSolver()).Select(
            sets,
            profile,
            budget,
            accounting,
            validator ?? new FleetValidator(_ => true),
            revisionAssessments,
            hardAssessments);
    }

    private static VehicleCandidateSet Set(
        VehicleId vehicleId,
        params InsertionCandidate[] candidates) =>
        new(vehicleId, candidates, [], false);

    private static InsertionCandidate Candidate(
        string id,
        VehicleId vehicleId,
        IReadOnlyList<RequestId> requests,
        long cost) =>
        new(
            id,
            vehicleId,
            AlgorithmTestData.Vehicle(id: vehicleId).Route,
            requests,
            new CandidateSchedule([], cost),
            requests.Count == 0);

    private static CandidateCommitmentAssessment Revision(
        InsertionCandidate candidate,
        CommitmentVector revision) =>
        new(candidate.CandidateId, revision);

    private static HardVectorCandidateAssessment Hard(
        InsertionCandidate candidate,
        long utilization,
        CommitmentVector revision,
        bool hasHard = true,
        CommitmentVector? warning = null) =>
        new(
            candidate.CandidateId,
            utilization,
            revision,
            hasHard,
            warning,
            warning is not null);

    private static CommitmentVector Vector(
        long pickup = 0,
        long drop = 0,
        long material = 0) =>
        new(pickup, drop, material, 0, 0, 0, 0, 0, 0, 0);

    private sealed class FleetValidator(Func<FleetSelection, bool> validate) :
        IFleetSelectionValidator
    {
        public CandidateSelectionValidationResult Validate(
            FleetSelection selection) =>
            validate(selection)
                ? CandidateSelectionValidationResult.Valid()
                : CandidateSelectionValidationResult.Invalid(
                    "TEST_FLEET_REJECTED",
                    "The test fleet validator rejected the selection.");
    }

    private sealed class ExactTestSolver : ICandidateSelectionSolver
    {
        public CandidateSelectionSolveResult Solve(
            CandidateSelectionProblem problem,
            DeterministicSolverBudget budget)
        {
            CandidateSelectionSolution? best = null;
            long work = 0;
            Enumerate(0, []);

            if (best is null)
            {
                return CandidateSelectionSolveResult.Infeasible(
                    Diagnostics(problem, budget, work, []),
                    "TEST_INFEASIBLE",
                    "No assignment exists.");
            }

            var bounds = problem.ObjectiveLevels
                .Select(
                    (level, index) => ObjectiveSolveBound.Create(
                        index,
                        level,
                        best.ObjectiveValues[index],
                        best.ObjectiveValues[index]).Value!)
                .ToArray();
            return CandidateSelectionSolveResult.Optimal(
                best,
                Diagnostics(problem, budget, work, bounds));

            void Enumerate(int vehicleIndex, IReadOnlyList<string> selected)
            {
                if (vehicleIndex == problem.VehicleIds.Count)
                {
                    work++;
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
                             value => value.VehicleId
                                 == problem.VehicleIds[vehicleIndex]))
                {
                    Enumerate(vehicleIndex + 1, selected.Append(option.OptionId).ToArray());
                }
            }
        }

        private static CandidateSelectionSolverDiagnostics Diagnostics(
            CandidateSelectionProblem problem,
            DeterministicSolverBudget budget,
            long work,
            IReadOnlyList<ObjectiveSolveBound> bounds) =>
            CandidateSelectionSolverDiagnostics.Create(
                problem,
                budget,
                Math.Min(work, budget.MaximumWorkUnits),
                1,
                0,
                bounds).Value!;
    }

    private sealed class UnknownTestSolver : ICandidateSelectionSolver
    {
        public CandidateSelectionSolveResult Solve(
            CandidateSelectionProblem problem,
            DeterministicSolverBudget budget)
        {
            var diagnostics = CandidateSelectionSolverDiagnostics.Create(
                problem,
                budget,
                0,
                0,
                0,
                []).Value!;
            return CandidateSelectionSolveResult.Unknown(
                diagnostics,
                "TEST_UNKNOWN",
                "The test solver did not produce an incumbent.");
        }
    }

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
