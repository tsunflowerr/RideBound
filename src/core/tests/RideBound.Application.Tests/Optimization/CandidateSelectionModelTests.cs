using RideBound.Application.Optimization;
using RideBound.Domain.Common;

namespace RideBound.Application.Tests.Optimization;

public sealed class CandidateSelectionModelTests
{
    private static readonly VehicleId VehicleOne = new("v-1");
    private static readonly VehicleId VehicleTwo = new("v-2");
    private static readonly RequestId RequestOne = new("r-1");

    [Fact]
    public void Problem_canonicalizes_entities_but_preserves_lexicographic_level_order()
    {
        var result = CreateProblemResult();

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var problem = result.Value!;
        Assert.Equal([VehicleOne, VehicleTwo], problem.VehicleIds);
        Assert.Equal([RequestOne], problem.RequestIds);
        Assert.Equal(
            ["accepted", "penalty", "operational-cost"],
            problem.ObjectiveLevels.Select(level => level.Name));
        Assert.Equal(
            ["v1-accept", "v1-noop", "v2-accept", "v2-noop"],
            problem.Options.Select(option => option.OptionId));
    }

    [Fact]
    public void Problem_requires_unique_vehicles_and_objective_names()
    {
        var duplicateVehicle = CandidateSelectionProblem.Create(
            [VehicleOne, VehicleOne],
            [],
            Levels(),
            [NoOp("noop", VehicleOne)]);
        var duplicateObjective = CandidateSelectionProblem.Create(
            [VehicleOne],
            [],
            [
                new CandidateSelectionObjectiveLevel(
                    "same",
                    CandidateSelectionObjectiveSense.Minimize),
                new CandidateSelectionObjectiveLevel(
                    "same",
                    CandidateSelectionObjectiveSense.Maximize),
            ],
            [
                new CandidateSelectionOption(
                    "noop",
                    VehicleOne,
                    [],
                    [0, 0],
                    true),
            ]);

        Assert.Equal(
            CandidateSelectionFailureCodes.InvalidProblem,
            duplicateVehicle.Failure?.Code);
        Assert.Equal("vehicleIds", duplicateVehicle.Failure?.Dimension);
        Assert.Equal(
            CandidateSelectionFailureCodes.InvalidProblem,
            duplicateObjective.Failure?.Code);
        Assert.Equal("objectiveLevels", duplicateObjective.Failure?.Dimension);
    }

    [Fact]
    public void Problem_requires_exactly_one_no_op_for_each_vehicle()
    {
        var missing = CandidateSelectionProblem.Create(
            [VehicleOne, VehicleTwo],
            [],
            Levels(),
            [NoOp("v1-noop", VehicleOne)]);
        var duplicate = CandidateSelectionProblem.Create(
            [VehicleOne],
            [],
            Levels(),
            [NoOp("noop-a", VehicleOne), NoOp("noop-b", VehicleOne)]);

        Assert.Equal("options", missing.Failure?.Dimension);
        Assert.Equal(VehicleTwo.Value, missing.Failure?.EntityId);
        Assert.Equal("options", duplicate.Failure?.Dimension);
    }

    [Fact]
    public void Problem_rejects_no_op_that_accepts_a_request()
    {
        var result = CandidateSelectionProblem.Create(
            [VehicleOne],
            [RequestOne],
            Levels(),
            [
                new CandidateSelectionOption(
                    "bad-noop",
                    VehicleOne,
                    [RequestOne],
                    [0, 0, 0],
                    true),
            ]);

        Assert.Equal(
            CandidateSelectionFailureCodes.InvalidProblem,
            result.Failure?.Code);
        Assert.Equal("isNoOp", result.Failure?.Dimension);
    }

    [Fact]
    public void Problem_rejects_unknown_entities_and_duplicate_request_in_option()
    {
        var unknownVehicle = CandidateSelectionProblem.Create(
            [VehicleOne],
            [],
            Levels(),
            [NoOp("unknown", VehicleTwo)]);
        var duplicateRequest = CandidateSelectionProblem.Create(
            [VehicleOne],
            [RequestOne],
            Levels(),
            [
                NoOp("noop", VehicleOne),
                new CandidateSelectionOption(
                    "duplicate-request",
                    VehicleOne,
                    [RequestOne, RequestOne],
                    [2, 0, 0],
                    false),
            ]);

        Assert.Equal("vehicleId", unknownVehicle.Failure?.Dimension);
        Assert.Equal("requestIds", duplicateRequest.Failure?.Dimension);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(9_007_199_254_740_992)]
    public void Problem_rejects_noncanonical_objective_contribution(long value)
    {
        var result = CandidateSelectionProblem.Create(
            [VehicleOne],
            [],
            Levels(),
            [
                new CandidateSelectionOption(
                    "noop",
                    VehicleOne,
                    [],
                    [value, 0, 0],
                    true),
            ]);

        Assert.Equal(
            "objectiveContributions",
            result.Failure?.Dimension);
    }

    [Fact]
    public void Problem_rejects_wrong_objective_vector_length()
    {
        var result = CandidateSelectionProblem.Create(
            [VehicleOne],
            [],
            Levels(),
            [
                new CandidateSelectionOption(
                    "noop",
                    VehicleOne,
                    [],
                    [0, 0],
                    true),
            ]);

        Assert.Equal(
            "objectiveContributions",
            result.Failure?.Dimension);
    }

    [Fact]
    public void Solution_enforces_one_option_per_vehicle_and_request_uniqueness()
    {
        var problem = CreateProblemResult().Value!;
        var sameVehicleTwice = CandidateSelectionSolution.Create(
            problem,
            ["v1-noop", "v1-accept"]);
        var requestTwice = CandidateSelectionSolution.Create(
            problem,
            ["v1-accept", "v2-accept"]);

        Assert.Equal("vehicleId", sameVehicleTwice.Failure?.Dimension);
        Assert.Equal("requestId", requestTwice.Failure?.Dimension);
    }

    [Fact]
    public void Solution_sums_each_level_and_uses_vehicle_order()
    {
        var problem = CreateProblemResult().Value!;

        var result = CandidateSelectionSolution.Create(
            problem,
            ["v2-noop", "v1-accept"]);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(["v1-accept", "v2-noop"], result.Value!.SelectedOptionIds);
        Assert.Equal([1, 50, 50], result.Value.ObjectiveValues);
    }

    [Fact]
    public void Solution_fails_closed_on_canonical_sum_overflow()
    {
        var level = new CandidateSelectionObjectiveLevel(
            "sum",
            CandidateSelectionObjectiveSense.Minimize);
        var problem = CandidateSelectionProblem.Create(
            [VehicleOne, VehicleTwo],
            [],
            [level],
            [
                new CandidateSelectionOption(
                    "v1-noop",
                    VehicleOne,
                    [],
                    [DomainLimits.MaxCanonicalInteger],
                    true),
                new CandidateSelectionOption(
                    "v2-noop",
                    VehicleTwo,
                    [],
                    [1],
                    true),
            ]).Value!;

        var result = CandidateSelectionSolution.Create(
            problem,
            ["v1-noop", "v2-noop"]);

        Assert.Equal(
            CandidateSelectionFailureCodes.ObjectiveOverflow,
            result.Failure?.Code);
    }

    [Fact]
    public void Lexicographic_comparison_never_trades_a_higher_level_for_lower_levels()
    {
        var levels = Levels();

        var moreAcceptedButExpensive = new long[] { 2, 1000, 1000 };
        var lessAcceptedButCheap = new long[] { 1, 0, 0 };
        var sameAcceptedLowerPenalty = new long[] { 2, 999, 5000 };

        Assert.True(
            LexicographicObjectiveComparer.Compare(
                moreAcceptedButExpensive,
                lessAcceptedButCheap,
                levels) < 0);
        Assert.True(
            LexicographicObjectiveComparer.Compare(
                moreAcceptedButExpensive,
                sameAcceptedLowerPenalty,
                levels) > 0);
    }

    [Theory]
    [InlineData("donor-a", "receiver-a", "noop-c", "noop-d", true)]
    [InlineData("donor-a", "noop-b", "noop-c", "noop-d", false)]
    [InlineData("noop-a", "receiver-a", "noop-c", "noop-d", false)]
    [InlineData("donor-a", "noop-b", "noop-c", "receiver-b", false)]
    [InlineData("donor-a", "receiver-a", "donor-b", "receiver-b", false)]
    [InlineData("noop-a", "noop-b", "noop-c", "noop-d", true)]
    public void Selection_requires_a_matching_pair_and_at_most_one_move(
        string first,
        string second,
        string third,
        string fourth,
        bool expectedValid)
    {
        var a = VehicleOne;
        var b = VehicleTwo;
        var c = new VehicleId("v-3");
        var d = new VehicleId("v-4");
        var riderA = new RequestId("waiting-a");
        var riderB = new RequestId("waiting-b");
        var model = CandidateSelectionProblem.Create(
            [d, b, a, c],
            [],
            Levels(),
            [
                NoOp("noop-a", a),
                NoOp("noop-b", b),
                NoOp("noop-c", c),
                NoOp("noop-d", d),
                new("donor-a", a, [], [0, 0, 2], false, RelocationOut: riderA),
                new("receiver-a", b, [], [0, 0, 3], false, RelocationIn: riderA),
                new("donor-b", c, [], [0, 0, 4], false, RelocationOut: riderB),
                new("receiver-b", d, [], [0, 0, 5], false, RelocationIn: riderB),
            ]);

        Assert.True(model.IsSuccess, model.Failure?.Message);
        var selected = CandidateSelectionSolution.Create(model.Value!, [fourth, third, second, first]);

        Assert.Equal(expectedValid, selected.IsSuccess);
        if (expectedValid)
        {
            Assert.Equal([first, second, third, fourth], selected.Value!.SelectedOptionIds);
            Assert.Equal(first == "donor-a" ? 5 : 0, selected.Value.ObjectiveValues[2]);
        }
        else
        {
            Assert.Equal(CandidateSelectionFailureCodes.InvalidSolution, selected.Failure?.Code);
            Assert.Equal("relocation", selected.Failure?.Dimension);
        }
    }

    [Fact]
    public void Problem_rejects_a_receiver_that_also_accepts_a_new_request()
    {
        var result = CandidateSelectionProblem.Create(
            [VehicleOne],
            [RequestOne],
            Levels(),
            [
                NoOp("noop", VehicleOne),
                new("invalid-receiver", VehicleOne, [RequestOne], [1, 0, 0], false,
                    RelocationIn: new RequestId("waiting")),
            ]);

        Assert.Equal(CandidateSelectionFailureCodes.InvalidProblem, result.Failure?.Code);
        Assert.Equal("relocation", result.Failure?.Dimension);
    }

    [Theory]
    [InlineData(0, 1, 0)]
    [InlineData(1, 0, 0)]
    [InlineData(1, 1, -1)]
    [InlineData(1, 1, 2_147_483_648)]
    public void Deterministic_budget_rejects_unsupported_values(
        long work,
        long deterministicMicros,
        long seed)
    {
        var result = DeterministicSolverBudget.Create(
            work,
            deterministicMicros,
            seed);

        Assert.Equal(
            CandidateSelectionFailureCodes.InvalidBudget,
            result.Failure?.Code);
    }

    [Fact]
    public void Diagnostics_cannot_exceed_deterministic_budget_or_reorder_bounds()
    {
        var problem = CreateProblemResult().Value!;
        var budget = Budget();
        var accepted = ObjectiveSolveBound.Create(
            0,
            problem.ObjectiveLevels[0],
            1).Value!;
        var wrongFirst = ObjectiveSolveBound.Create(
            1,
            problem.ObjectiveLevels[1],
            30).Value!;

        var overBudget = CandidateSelectionSolverDiagnostics.Create(
            problem,
            budget,
            consumedWorkUnits: 101,
            consumedDeterministicTimeMicros: 500,
            []);
        var reordered = CandidateSelectionSolverDiagnostics.Create(
            problem,
            budget,
            consumedWorkUnits: 1,
            consumedDeterministicTimeMicros: 1,
            [wrongFirst, accepted]);

        Assert.Equal(
            CandidateSelectionFailureCodes.InvalidDiagnostics,
            overBudget.Failure?.Code);
        Assert.Equal(
            CandidateSelectionFailureCodes.InvalidDiagnostics,
            reordered.Failure?.Code);
    }

    [Fact]
    public void Optimal_status_requires_exact_bounds_matching_solution()
    {
        var problem = CreateProblemResult().Value!;
        var solution = CandidateSelectionSolution.Create(
            problem,
            ["v1-accept", "v2-noop"]).Value!;
        var exactBounds = problem.ObjectiveLevels
            .Select(
                (level, index) => ObjectiveSolveBound.Create(
                    index,
                    level,
                    solution.ObjectiveValues[index]).Value!)
            .ToArray();
        var diagnostics = Diagnostics(problem, exactBounds);

        var result = CandidateSelectionSolveResult.Optimal(
            solution,
            diagnostics);

        Assert.Equal(CandidateSelectionSolveStatus.Optimal, result.Status);
        Assert.Same(solution, result.Solution);

        var mismatchedBounds = exactBounds.ToArray();
        mismatchedBounds[2] = ObjectiveSolveBound.Create(
            2,
            problem.ObjectiveLevels[2],
            49).Value!;
        var mismatchedDiagnostics = Diagnostics(problem, mismatchedBounds);

        Assert.Throws<ArgumentException>(
            () => CandidateSelectionSolveResult.Optimal(
                solution,
                mismatchedDiagnostics));
    }

    [Fact]
    public void Result_statuses_distinguish_no_solution_and_safe_fallback()
    {
        var problem = CreateProblemResult().Value!;
        var solution = CandidateSelectionSolution.Create(
            problem,
            ["v1-noop", "v2-noop"]).Value!;
        var diagnostics = Diagnostics(problem, []);

        var statuses = new[]
        {
            CandidateSelectionSolveResult.Feasible(solution, diagnostics),
            CandidateSelectionSolveResult.SafeFallback(
                solution,
                diagnostics,
                "BUDGET_EXHAUSTED",
                "Safe no-op fallback selected."),
            CandidateSelectionSolveResult.Infeasible(
                diagnostics,
                "INFEASIBLE",
                "No assignment exists."),
            CandidateSelectionSolveResult.Unknown(
                diagnostics,
                "UNKNOWN",
                "Search ended without a conclusion."),
            CandidateSelectionSolveResult.ModelInvalid(
                diagnostics,
                "MODEL_INVALID",
                "Solver rejected the translated model."),
        };

        Assert.Equal(
            [
                CandidateSelectionSolveStatus.Feasible,
                CandidateSelectionSolveStatus.SafeFallback,
                CandidateSelectionSolveStatus.Infeasible,
                CandidateSelectionSolveStatus.Unknown,
                CandidateSelectionSolveStatus.ModelInvalid,
            ],
            statuses.Select(result => result.Status));
        Assert.NotNull(statuses[0].Solution);
        Assert.NotNull(statuses[1].Solution);
        Assert.All(statuses.Skip(2), result => Assert.Null(result.Solution));
    }

    private static DomainResult<CandidateSelectionProblem> CreateProblemResult() =>
        CandidateSelectionProblem.Create(
            [VehicleTwo, VehicleOne],
            [RequestOne],
            Levels(),
            [
                new CandidateSelectionOption(
                    "v2-noop",
                    VehicleTwo,
                    [],
                    [0, 20, 20],
                    true),
                new CandidateSelectionOption(
                    "v2-accept",
                    VehicleTwo,
                    [RequestOne],
                    [1, 40, 40],
                    false),
                new CandidateSelectionOption(
                    "v1-noop",
                    VehicleOne,
                    [],
                    [0, 10, 10],
                    true),
                new CandidateSelectionOption(
                    "v1-accept",
                    VehicleOne,
                    [RequestOne],
                    [1, 30, 30],
                    false),
            ]);

    private static IReadOnlyList<CandidateSelectionObjectiveLevel> Levels() =>
        [
            new CandidateSelectionObjectiveLevel(
                "accepted",
                CandidateSelectionObjectiveSense.Maximize),
            new CandidateSelectionObjectiveLevel(
                "penalty",
                CandidateSelectionObjectiveSense.Minimize),
            new CandidateSelectionObjectiveLevel(
                "operational-cost",
                CandidateSelectionObjectiveSense.Minimize),
        ];

    private static CandidateSelectionOption NoOp(
        string optionId,
        VehicleId vehicleId) =>
        new(optionId, vehicleId, [], [0, 0, 0], true);

    private static DeterministicSolverBudget Budget() =>
        DeterministicSolverBudget.Create(
            maximumWorkUnits: 100,
            maximumDeterministicTimeMicros: 1_000,
            randomSeed: 1).Value!;

    private static CandidateSelectionSolverDiagnostics Diagnostics(
        CandidateSelectionProblem problem,
        IReadOnlyList<ObjectiveSolveBound> bounds) =>
        CandidateSelectionSolverDiagnostics.Create(
            problem,
            Budget(),
            consumedWorkUnits: 10,
            consumedDeterministicTimeMicros: 100,
            bounds).Value!;
}
