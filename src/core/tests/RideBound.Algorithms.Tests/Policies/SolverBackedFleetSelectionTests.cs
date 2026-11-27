using RideBound.Algorithms.Candidates;
using RideBound.Algorithms.Policies;
using RideBound.Application.Optimization;
using RideBound.Domain.Common;

namespace RideBound.Algorithms.Tests.Policies;

public sealed class SolverBackedFleetSelectionTests
{
    [Fact]
    public void Empty_vehicle_candidate_set_reports_infeasibility_not_id_collision()
    {
        var result = Select(
            [Set(AlgorithmTestData.VehicleOne)]);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            SolverBackedSelectionFailureCodes.ModelMappingFailed,
            result.Witness!.Code);
        Assert.Equal(
            "Every vehicle needs at least one feasible candidate.",
            result.Witness.Message);
    }

    [Fact]
    public void Fleet_selectors_reject_candidates_requiring_relocation_coupling()
    {
        var vehicle = AlgorithmTestData.VehicleOne;
        var donor = Candidate("donor", vehicle, [], 0) with
        {
            IsNoOp = false,
            RelocatedRequestId = new RequestId("waiting"),
            RelocationRole = RelocationRole.Donor,
        };
        var sets = new[] { Set(vehicle, Candidate("noop", vehicle, [], 10), donor) };

        var standard = new CandidateFleetSelector().Select(sets);
        var solverBacked = Select(sets);

        Assert.Equal(RollingCostFailureCodes.RelocationRequiresCoupledSelection, standard.Witness?.Code);
        Assert.Equal(RollingCostFailureCodes.RelocationRequiresCoupledSelection, solverBacked.Witness?.Code);
    }

    [Fact]
    public void Rolling_cost_entry_points_reject_relocation_options_before_solving()
    {
        var state = AlgorithmTestData.CreateState([], [AlgorithmTestData.Vehicle()]);
        var options = new CandidateGenerationOptions(
            100, 1, waitingRiderRelocation: new WaitingRiderRelocationGenerationOptions());
        var policy = new RollingCostPolicy();

        var standard = policy.Decide(state, options);
        var solverBacked = policy.DecideWithSolver(state, options, new UnexpectedSolver(), Budget());

        Assert.Equal(RollingCostFailureCodes.RelocationRequiresCoupledSelection, standard.Witness?.Code);
        Assert.Equal(RollingCostFailureCodes.RelocationRequiresCoupledSelection, solverBacked.Witness?.Code);
    }

    [Fact]
    public void Duplicate_candidate_ids_report_the_global_identity_collision()
    {
        var result = Select(
            [
                Set(
                    AlgorithmTestData.VehicleOne,
                    Candidate("duplicate", AlgorithmTestData.VehicleOne, [], 0)),
                Set(
                    AlgorithmTestData.VehicleTwo,
                    Candidate("duplicate", AlgorithmTestData.VehicleTwo, [], 0)),
            ]);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            SolverBackedSelectionFailureCodes.ModelMappingFailed,
            result.Witness!.Code);
        Assert.Equal(
            "Candidate IDs must be globally unique across the fleet.",
            result.Witness.Message);
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

        var result = Select(sets);

        Assert.True(result.IsSuccess, result.Witness?.Message);
        Assert.Equal(
            ["v1-accept", "v2-noop"],
            result.Selection!.Selection.VehiclePlans
                .Select(value => value.Candidate.CandidateId));
        Assert.Equal(1, result.Selection.Selection.AcceptedRequestCount);
        Assert.Equal(10, result.Selection.Selection.OperationalCost);
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

    private static SolverBackedFleetSelectionResult Select(
        IReadOnlyList<VehicleCandidateSet> sets,
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
            budget,
            accounting,
            validator ?? new FleetValidator(_ => true));
    }

    [Fact]
    public void Candidate_must_belong_to_its_vehicle_set()
    {
        var result = Select(
            [
                Set(
                    AlgorithmTestData.VehicleOne,
                    Candidate("wrong-vehicle", AlgorithmTestData.VehicleTwo, [], 0)),
            ]);

        Assert.Equal(SolverBackedSelectionFailureCodes.ModelMappingFailed, result.Witness?.Code);
        Assert.Contains("declared vehicle", result.Witness!.Message);
    }

    [Fact]
    public void Solver_solution_from_another_problem_uses_the_validated_no_op()
    {
        var budget = Budget();
        var accounting = CandidateSelectionPreSolveAccounting.Create(budget, 0, 0, 0).Value!;
        var vehicle = AlgorithmTestData.VehicleOne;
        var result = new SolverBackedFleetSelector(new ForeignSolutionSolver()).Select(
            [Set(vehicle, Candidate("no-op", vehicle, [], 0))],
            budget,
            accounting,
            new FleetValidator(_ => true));

        Assert.True(result.IsSuccess, result.Witness?.Message);
        var selected = result.Selection!;
        Assert.Equal(CandidateSelectionSolveStatus.SafeFallback, selected.Execution.SolveResult.Status);
        Assert.Equal("no-op", Assert.Single(selected.Selection.VehiclePlans).Candidate.CandidateId);
        Assert.Equal(
            SolverBackedSelectionFailureCodes.ModelMappingFailed,
            Assert.Single(selected.Execution.Diagnostics.ValidationWitnesses).ReasonCode);
    }

    [Fact]
    public void Solver_entry_preserves_the_rolling_cost_order_and_applies_the_decision()
    {
        var first = AlgorithmTestData.PendingRequest("request-1");
        var second = AlgorithmTestData.PendingRequest("request-2");
        var state = AlgorithmTestData.CreateState(
            [first, second],
            [AlgorithmTestData.Vehicle(AlgorithmTestData.VehicleOne),
                AlgorithmTestData.Vehicle(AlgorithmTestData.VehicleTwo)]);
        var options = new CandidateGenerationOptions(100, 2, exactSmallMode: true);
        var policy = new RollingCostPolicy();
        var expected = policy.Decide(state, options);
        var actual = policy.DecideWithSolver(state, options, new ExactTestSolver(), Budget());

        Assert.True(expected.IsSuccess, expected.Witness?.Message);
        Assert.True(actual.IsSuccess, actual.Witness?.Message);
        Assert.Equal(expected.Decision!.RequestActions, actual.Decision!.RequestActions);
        Assert.Equal(expected.Decision.OperationalCost, actual.Decision.OperationalCost);
        Assert.Equal(
            expected.Decision.VehiclePlans.Select(plan => plan.Candidate.CandidateId),
            actual.Decision.VehiclePlans.Select(plan => plan.Candidate.CandidateId));
        Assert.Equal(
            expected.Decision.ProposedState.Run.Requests[second.Id],
            actual.Decision.ProposedState.Run.Requests[second.Id]);
        Assert.Equal(
            CandidateSelectionSolveStatus.Optimal,
            actual.Decision.SelectionExecution!.SolveResult.Status);
    }

    [Fact]
    public void Bounded_generation_and_solver_loss_are_reported_separately()
    {
        var first = AlgorithmTestData.PendingRequest("request-1");
        var second = AlgorithmTestData.PendingRequest(
            "request-2",
            AlgorithmTestData.NodeThree,
            AlgorithmTestData.NodeOne);
        var state = AlgorithmTestData.CreateState([first, second], [AlgorithmTestData.Vehicle()]);
        var policy = new RollingCostPolicy();
        var result = policy.DecideWithSolver(
            state,
            new CandidateGenerationOptions(100, 1),
            new UnknownTestSolver(),
            Budget());

        Assert.True(result.IsSuccess, result.Witness?.Message);
        var decision = result.Decision!;
        Assert.Equal(1, decision.GenerationDiagnostics!.OmittedRequestCount);
        var execution = decision.SelectionExecution!;
        Assert.True(execution.Diagnostics.CandidateLossOccurred);
        Assert.True(execution.Diagnostics.SolverLossOccurred);
        Assert.Matches("^[0-9a-f]{64}$", execution.Diagnostics.OmissionDigest!);
        Assert.Equal(CandidateSelectionSolveStatus.SafeFallback, execution.SolveResult.Status);
        Assert.All(decision.RequestActions, action => Assert.Equal(RequestDecisionOutcome.Deferred, action.Outcome));
        Assert.Same(state.Run, decision.ProposedState.Run);
    }

    [Fact]
    public void Generation_over_budget_fails_before_calling_the_solver()
    {
        var state = AlgorithmTestData.CreateState(
            [AlgorithmTestData.PendingRequest()],
            [AlgorithmTestData.Vehicle()]);
        var result = new RollingCostPolicy().DecideWithSolver(
            state,
            new CandidateGenerationOptions(100, 1),
            new UnexpectedSolver(),
            Budget(generation: 1));

        Assert.Equal(CandidateSelectionFailureCodes.InvalidExecutionAccounting, result.Witness?.Code);
    }

    private static DeterministicCandidateSelectionExecutionBudget Budget(long generation = 1_000) =>
        DeterministicCandidateSelectionExecutionBudget.Create(
            generation,
            1_000,
            DeterministicSolverBudget.Create(1_000, 1_000, 1).Value!).Value!;

    [Fact]
    public void Empty_fleet_defers_pending_requests_without_calling_the_solver()
    {
        var state = AlgorithmTestData.CreateState([AlgorithmTestData.PendingRequest()], []);
        var result = new RollingCostPolicy().DecideWithSolver(
            state,
            new CandidateGenerationOptions(100, 1),
            new UnexpectedSolver(),
            Budget());

        Assert.True(result.IsSuccess, result.Witness?.Message);
        var decision = result.Decision!;
        Assert.Empty(decision.VehiclePlans);
        Assert.Equal(RequestDecisionOutcome.Deferred, Assert.Single(decision.RequestActions).Outcome);
        Assert.Same(state.Run, decision.ProposedState.Run);
        Assert.Null(decision.SelectionExecution);
    }

    private sealed class UnexpectedSolver : ICandidateSelectionSolver
    {
        public CandidateSelectionSolveResult Solve(
            CandidateSelectionProblem problem,
            DeterministicSolverBudget budget) =>
            throw new InvalidOperationException("The solver must not run after an accounting failure.");
    }

    private sealed class ForeignSolutionSolver : ICandidateSelectionSolver
    {
        public CandidateSelectionSolveResult Solve(
            CandidateSelectionProblem problem,
            DeterministicSolverBudget budget)
        {
            var foreign = CandidateSelectionProblem.Create(
                problem.VehicleIds,
                [],
                problem.ObjectiveLevels,
                [new CandidateSelectionOption("foreign-no-op", problem.VehicleIds.Single(),
                    [], new long[problem.ObjectiveLevels.Count], true)]).Value!;
            return new ExactTestSolver().Solve(foreign, budget);
        }
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
}
