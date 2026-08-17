namespace RideBound.Benchmarking.Metrics;

public static class MechanicalMetricOracleVerifier
{
    public static void Verify(
        MechanicalMetricCalculationResult production,
        ReadOnlySpan<byte> oracleCanonicalRows,
        string oracleMetricSetHash)
    {
        ArgumentNullException.ThrowIfNull(production);
        ArgumentException.ThrowIfNullOrWhiteSpace(oracleMetricSetHash);

        if (!production.CanonicalRows.AsSpan().SequenceEqual(oracleCanonicalRows)
            || !string.Equals(
                production.MetricSetHash,
                oracleMetricSetHash,
                StringComparison.Ordinal))
        {
            throw new MechanicalMetricCalculationException(
                "metric.oracle-mismatch",
                "Production and independent oracle metric rows differ.");
        }
    }
}
