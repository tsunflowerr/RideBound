using System.Buffers;
using System.Text.Json;
using RideBound.Algorithms.Candidates;
using RideBound.Algorithms.Policies;
using RideBound.Application.Commitments;
using RideBound.Application.Optimization;

namespace RideBound.Runner.Protocol;

internal static class SolverExecutionEvidenceMapper
{
    public static JsonElement? Map(RollingCostDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);

        if (decision.GenerationDiagnostics is null
            || decision.SelectionExecution is null)
        {
            return null;
        }

        var buffer = new ArrayBufferWriter<byte>();

        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("evidenceVersion", "1.1.0");
            writer.WritePropertyName("generation");
            WriteGeneration(writer, decision.GenerationDiagnostics);
            writer.WritePropertyName("prunedCandidates");
            writer.WriteStartArray();

            foreach (var witness in decision.PrunedCandidates
                         .OrderBy(value => value.VehicleId.Value, StringComparer.Ordinal)
                         .ThenBy(value => value.CandidateId, StringComparer.Ordinal)
                         .ThenBy(value => value.Code, StringComparer.Ordinal))
            {
                WritePruneWitness(writer, witness);
            }

            writer.WriteEndArray();
            writer.WritePropertyName("selection");
            WriteSelection(writer, decision.SelectionExecution);
            writer.WriteEndObject();
            writer.Flush();
        }

        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }

    private static void WriteGeneration(
        Utf8JsonWriter writer,
        CandidateGenerationDiagnostics diagnostics)
    {
        writer.WriteStartObject();
        writer.WriteNumber(
            "totalPendingRequestCount",
            diagnostics.TotalPendingRequestCount);
        writer.WriteNumber("consideredRequestCount", diagnostics.ConsideredRequestCount);
        writer.WriteNumber("omittedRequestCount", diagnostics.OmittedRequestCount);
        writer.WritePropertyName("vehicleLosses");
        writer.WriteStartArray();

        foreach (var loss in diagnostics.VehicleLosses
                     .OrderBy(
                         value => value.VehicleId?.Value ?? string.Empty,
                         StringComparer.Ordinal))
        {
            writer.WriteStartObject();

            if (loss.VehicleId is not null)
            {
                writer.WriteString("vehicleId", loss.VehicleId.Value.Value);
            }

            writer.WriteNumber("explorationWorkUnits", loss.ExplorationWorkUnits);
            writer.WriteNumber(
                "evaluatedCandidatePathCount",
                loss.EvaluatedCandidatePathCount);
            writer.WriteNumber(
                "uniqueFeasibleCandidateCountBeforeCap",
                loss.UniqueFeasibleCandidateCountBeforeCap);
            writer.WriteNumber("retainedCandidateCount", loss.RetainedCandidateCount);
            writer.WriteNumber(
                "physicallyOrSchedulePrunedCount",
                loss.PhysicallyOrSchedulePrunedCount);
            writer.WriteNumber(
                "omittedUnexpandedCandidatePathCount",
                loss.OmittedUnexpandedCandidatePathCount);
            writer.WriteNumber(
                "omittedFeasibleCandidateCountByCap",
                loss.OmittedFeasibleCandidateCountByCap);
            writer.WriteBoolean("workBudgetExhausted", loss.WorkBudgetExhausted);
            writer.WriteBoolean("candidateCapApplied", loss.CandidateCapApplied);
            writer.WriteBoolean(
                "omissionCountWasSaturated",
                loss.OmissionCountWasSaturated);
            writer.WriteNumber(
                "eligibleRepairRequestCount",
                loss.EligibleRepairRequestCount);
            writer.WriteNumber(
                "consideredRepairRequestCount",
                loss.ConsideredRepairRequestCount);
            writer.WriteNumber(
                "omittedRepairRequestCount",
                loss.OmittedRepairRequestCount);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WritePropertyName("omissions");
        writer.WriteStartArray();

        foreach (var omission in diagnostics.Omissions
                     .OrderBy(value => value.Code, StringComparer.Ordinal)
                     .ThenBy(value => value.StableDigest, StringComparer.Ordinal)
                     .ThenBy(
                         value => value.VehicleId?.Value ?? string.Empty,
                         StringComparer.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("code", omission.Code);
            writer.WriteNumber("count", omission.Count);
            writer.WriteString("stableDigest", omission.StableDigest);
            writer.WriteBoolean("countWasSaturated", omission.CountWasSaturated);

            if (omission.VehicleId is not null)
            {
                writer.WriteString("vehicleId", omission.VehicleId.Value.Value);
            }

            if (omission.RequestIds is not null)
            {
                writer.WritePropertyName("requestIds");
                writer.WriteStartArray();

                foreach (var requestId in omission.RequestIds
                             .OrderBy(value => value.Value, StringComparer.Ordinal))
                {
                    writer.WriteStringValue(requestId.Value);
                }

                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WritePropertyName("exogenousServiceQualityBreaches");
        writer.WriteStartArray();

        foreach (var breach in diagnostics.ExogenousServiceQualityBreaches
                     .OrderBy(value => value.VehicleId.Value, StringComparer.Ordinal)
                     .ThenBy(value => value.RequestId.Value, StringComparer.Ordinal)
                     .ThenBy(value => value.Code, StringComparer.Ordinal)
                     .ThenBy(value => value.Dimension, StringComparer.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("vehicleId", breach.VehicleId.Value);
            writer.WriteString("requestId", breach.RequestId.Value);
            writer.WriteString("code", breach.Code);
            writer.WriteString("dimension", breach.Dimension);
            writer.WriteNumber(
                "contractualMilliseconds",
                breach.ContractualMilliseconds);
            writer.WriteNumber(
                "exogenousMilliseconds",
                breach.ExogenousMilliseconds);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WritePruneWitness(
        Utf8JsonWriter writer,
        CandidatePruneWitness witness)
    {
        writer.WriteStartObject();
        writer.WriteString("candidateId", witness.CandidateId);
        writer.WriteString("vehicleId", witness.VehicleId.Value);
        writer.WritePropertyName("newRequestIds");
        writer.WriteStartArray();

        foreach (var requestId in witness.NewRequestIds
                     .OrderBy(value => value.Value, StringComparer.Ordinal))
        {
            writer.WriteStringValue(requestId.Value);
        }

        writer.WriteEndArray();
        writer.WriteString("code", witness.Code);

        if (witness.PhysicalWitness is not null)
        {
            var physical = witness.PhysicalWitness;
            writer.WritePropertyName("physicalWitness");
            writer.WriteStartObject();
            writer.WriteString("code", physical.Code);
            writer.WriteString("vehicleId", physical.VehicleId.Value);
            WriteOptional(writer, "requestId", physical.RequestId?.Value);
            WriteOptional(writer, "stopId", physical.StopId?.Value);
            WriteOptional(writer, "dimension", physical.Dimension);
            WriteOptional(writer, "expected", physical.Expected);
            WriteOptional(writer, "actual", physical.Actual);
            writer.WriteEndObject();
        }

        writer.WritePropertyName("commitmentWitnesses");
        writer.WriteStartArray();

        foreach (var commitment in witness.CommitmentWitnesses
                     ?? Array.Empty<CommitmentValidationWitness>())
        {
            writer.WriteStartObject();
            writer.WriteString("stage", Stage(commitment.Stage));
            writer.WriteString("code", commitment.Code);
            WriteOptional(writer, "vehicleId", commitment.VehicleId?.Value);
            WriteOptional(writer, "requestId", commitment.RequestId?.Value);
            WriteOptional(writer, "dimension", commitment.Dimension);
            WriteOptional(writer, "rule", commitment.Rule);
            WriteOptional(writer, "limit", commitment.Limit);
            WriteOptional(writer, "before", commitment.Before);
            WriteOptional(writer, "delta", commitment.Delta);
            WriteOptional(writer, "after", commitment.After);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteSelection(
        Utf8JsonWriter writer,
        CandidateSelectionExecutionResult execution)
    {
        var diagnostics = execution.Diagnostics;
        writer.WriteStartObject();
        writer.WriteNumber(
            "consumedGenerationWorkUnits",
            diagnostics.ConsumedGenerationWorkUnits);
        writer.WriteNumber(
            "consumedValidationWorkUnits",
            diagnostics.ConsumedValidationWorkUnits);
        writer.WriteNumber("omittedCandidateCount", diagnostics.OmittedCandidateCount);
        WriteOptional(writer, "omissionDigest", diagnostics.OmissionDigest);
        writer.WriteBoolean(
            "omissionCountWasSaturated",
            diagnostics.OmissionCountWasSaturated);
        writer.WriteString("primarySolveStatus", Status(diagnostics.PrimarySolveStatus));
        writer.WritePropertyName("primarySolverDiagnostics");
        WriteSolverDiagnostics(writer, diagnostics.PrimarySolverDiagnostics);
        writer.WriteString("finalSolveStatus", Status(execution.SolveResult.Status));
        writer.WritePropertyName("finalSolverDiagnostics");
        WriteSolverDiagnostics(writer, execution.SolveResult.Diagnostics);
        writer.WriteString("executionPath", Path(diagnostics.ExecutionPath));
        writer.WriteNumber(
            "fallbackValidationAttempts",
            diagnostics.FallbackValidationAttempts);
        writer.WriteBoolean(
            "primaryIncumbentRejected",
            diagnostics.PrimaryIncumbentRejected);
        writer.WritePropertyName("validationWitnesses");
        writer.WriteStartArray();

        foreach (var witness in diagnostics.ValidationWitnesses)
        {
            writer.WriteStartObject();
            writer.WriteString("attemptedPath", Path(witness.AttemptedPath));
            writer.WritePropertyName("selectedOptionIds");
            writer.WriteStartArray();

            foreach (var optionId in witness.SelectedOptionIds.Order(StringComparer.Ordinal))
            {
                writer.WriteStringValue(optionId);
            }

            writer.WriteEndArray();
            writer.WriteString("reasonCode", witness.ReasonCode);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteSolverDiagnostics(
        Utf8JsonWriter writer,
        CandidateSelectionSolverDiagnostics diagnostics)
    {
        writer.WriteStartObject();
        writer.WriteNumber("consumedWorkUnits", diagnostics.ConsumedWorkUnits);
        writer.WriteNumber(
            "consumedDeterministicTimeMicros",
            diagnostics.ConsumedDeterministicTimeMicros);
        writer.WritePropertyName("objectiveBounds");
        writer.WriteStartArray();

        foreach (var bound in diagnostics.ObjectiveBounds)
        {
            writer.WriteStartObject();
            writer.WriteNumber("levelIndex", bound.LevelIndex);
            writer.WriteString("objectiveName", bound.ObjectiveName);
            writer.WriteNumber("incumbentValue", bound.IncumbentValue);
            writer.WriteNumber("bestBound", bound.BestBound);
            writer.WriteNumber("gapNumerator", bound.GapNumerator);
            writer.WriteNumber("gapDenominator", bound.GapDenominator);
            writer.WriteBoolean("isProvenOptimal", bound.IsProvenOptimal);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        WriteOptional(writer, "detailCode", diagnostics.DetailCode);
        writer.WriteEndObject();
    }

    private static void WriteOptional(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is not null)
        {
            writer.WriteString(name, value);
        }
    }

    private static void WriteOptional(Utf8JsonWriter writer, string name, long? value)
    {
        if (value is not null)
        {
            writer.WriteNumber(name, value.Value);
        }
    }

    private static string Status(CandidateSelectionSolveStatus status) =>
        status switch
        {
            CandidateSelectionSolveStatus.Optimal => "optimal",
            CandidateSelectionSolveStatus.Feasible => "feasible",
            CandidateSelectionSolveStatus.Infeasible => "infeasible",
            CandidateSelectionSolveStatus.Unknown => "unknown",
            CandidateSelectionSolveStatus.ModelInvalid => "modelInvalid",
            CandidateSelectionSolveStatus.SafeFallback => "safeFallback",
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };

    private static string Path(CandidateSelectionExecutionPath path) =>
        path switch
        {
            CandidateSelectionExecutionPath.None => "none",
            CandidateSelectionExecutionPath.ValidatedIncumbent =>
                "validatedIncumbent",
            CandidateSelectionExecutionPath.SafeNoOp => "safeNoOp",
            CandidateSelectionExecutionPath.GreedySingleRequest =>
                "greedySingleRequest",
            _ => throw new ArgumentOutOfRangeException(nameof(path)),
        };

    private static string Stage(CommitmentValidationStage stage) =>
        stage switch
        {
            CommitmentValidationStage.State => "state",
            CommitmentValidationStage.Physical => "physical",
            CommitmentValidationStage.Projection => "projection",
            CommitmentValidationStage.Lock => "lock",
            CommitmentValidationStage.Budget => "budget",
            CommitmentValidationStage.Ledger => "ledger",
            _ => throw new ArgumentOutOfRangeException(nameof(stage)),
        };
}
