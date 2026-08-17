using RideBound.Benchmarking.Contracts;

namespace RideBound.Benchmarking.Metrics;

public sealed record MechanicalMetricCalculationInput(
    RunRecord RunRecord,
    ScenarioTimeWindow TimeWindow,
    byte[] CanonicalRunRecord,
    byte[] InputTranscript,
    byte[] OutputTranscript,
    byte[] ObservationIndex,
    byte[] ResourceSamples,
    MechanicalMetricRegistry Registry,
    string CalculatorSourceSha256);

public sealed record MechanicalMetricCalculationResult(
    IReadOnlyList<MetricRow> Rows,
    byte[] CanonicalRows,
    string MetricSetHash,
    string SemanticEvidenceSha256,
    string ResourceEvidenceSha256);

public sealed class MechanicalMetricCalculationException(
    string code,
    string safeMessage,
    Exception? innerException = null) : Exception(safeMessage, innerException)
{
    public string Code { get; } = code;
}
