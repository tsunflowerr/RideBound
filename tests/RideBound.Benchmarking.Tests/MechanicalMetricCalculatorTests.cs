using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RideBound.Benchmarking.Contracts;
using RideBound.Benchmarking.Metrics;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;

namespace RideBound.Benchmarking.Tests;

public sealed class MechanicalMetricCalculatorTests
{
    private static readonly ScenarioTimeWindow Window = new(
        "UTC",
        "2026-08-11T00:00:00Z",
        "2026-08-11T00:20:00Z",
        0,
        1_000,
        1_150,
        1_200,
        "event-driven-v1");

    [Fact]
    public void Registry_is_immutable_and_rejects_self_reported_extensions()
    {
        var registry = Registry();

        Assert.Equal(MechanicalMetricRegistry.V1RegistryHash, registry.RegistryHash);
        Assert.Equal(36, registry.Definitions.Count);
        Assert.Throws<ArgumentException>(
            () => MechanicalMetricRegistry.ValidateExtension(
                new MetricExtensionRegistration(
                    "adapter.self-reported.mean",
                    "1.0.0",
                    "millisecond",
                    "adapter-column-v1",
                    "adapter-rows",
                    "simulatorAggregate",
                    new string('a', 64),
                    RawOracleRequired: false,
                    UsesSelfReportedAggregate: true)));
    }

    [Fact]
    public void Calculator_emits_exact_cohort_missing_resource_and_metric_set_rows()
    {
        var input = FixtureInput();

        var first = MechanicalMetricCalculator.Calculate(input);
        var second = MechanicalMetricCalculator.Calculate(input);

        Assert.Equal(132, first.Rows.Count);
        Assert.Equal(first.CanonicalRows, second.CanonicalRows);
        Assert.Equal(first.MetricSetHash, second.MetricSetHash);
        Assert.Equal(
            2,
            Row(first, "request.arrived.count", MetricWindowId.Scoring).ValueInteger);
        Assert.Equal(
            1,
            Row(first, "request.accepted.count", MetricWindowId.Scoring).ValueInteger);
        Assert.Equal(
            1,
            Row(first, "request.rejected.count", MetricWindowId.Scoring).ValueInteger);
        Assert.Equal(
            1,
            Row(first, "request.completed.count", MetricWindowId.Scoring).ValueInteger);
        var acceptance = Row(first, "request.acceptance.ppm", MetricWindowId.Scoring);
        Assert.Equal(MetricValueStatus.Observed, acceptance.ValueStatus);
        Assert.Equal(500_000, acceptance.ValueInteger);
        Assert.Equal(1, acceptance.NumeratorInteger);
        Assert.Equal(2, acceptance.DenominatorInteger);
        var completion = Row(first, "request.completion.ppm", MetricWindowId.Scoring);
        Assert.Equal(1_000_000, completion.ValueInteger);
        var drain = Row(first, "request.acceptance.ppm", MetricWindowId.Drain);
        Assert.Equal(MetricValueStatus.Missing, drain.ValueStatus);
        Assert.Null(drain.ValueInteger);
        Assert.Equal(0, drain.DenominatorInteger);
        Assert.Equal("denominator-zero", drain.MissingReasonId);
        Assert.Equal(
            MetricValueStatus.Missing,
            Row(first, "decisionDelta.pickupEtaTotalMs.max", MetricWindowId.Scoring)
                .ValueStatus);
        Assert.Equal(
            0,
            Row(first, "decisionDelta.pickupEtaTotalMs.sum", MetricWindowId.Scoring)
                .ValueInteger);
        Assert.Equal(
            1_200,
            Row(first, "resource.wall-time-ms", MetricWindowId.All).ValueInteger);
        Assert.Equal(
            100,
            Row(first, "resource.peak-working-set-bytes", MetricWindowId.All)
                .ValueInteger);
    }

    [Fact]
    public void Failed_terminal_and_cross_request_mutation_cannot_emit_metrics()
    {
        var input = FixtureInput();
        var failedRecord = input.RunRecord with
        {
            TerminalStatus = RunTerminalStatus.Failed,
            ExitCode = 17,
            FailureRecordId = "failure-1",
        };
        var terminalException = Assert.Throws<MechanicalMetricCalculationException>(
            () => MechanicalMetricCalculator.Calculate(
                input with
                {
                    RunRecord = failedRecord,
                    CanonicalRunRecord = BenchmarkContractCodec.Encode(failedRecord),
                }));
        Assert.Equal("metric.terminal-not-succeeded", terminalException.Code);

        var excludedRecord = input.RunRecord with
        {
            TerminalStatus = RunTerminalStatus.Excluded,
            ExitCode = null,
            ExclusionRecordId = "exclusion-1",
        };
        var exclusionException = Assert.Throws<MechanicalMetricCalculationException>(
            () => MechanicalMetricCalculator.Calculate(
                input with
                {
                    RunRecord = excludedRecord,
                    CanonicalRunRecord = BenchmarkContractCodec.Encode(excludedRecord),
                }));
        Assert.Equal("metric.terminal-not-succeeded", exclusionException.Code);

        var mutatedBytes = Encoding.UTF8.GetBytes(
            Encoding.UTF8.GetString(input.InputTranscript)
                .Replace("\"requestId\":\"r-1\"", "\"requestId\":\"r-x\"", StringComparison.Ordinal));
        var mutatedRecord = input.RunRecord with
        {
            InputFile = Evidence(input.RunRecord.RunId, "input.ndjson", mutatedBytes),
        };
        var mutationException = Assert.Throws<MechanicalMetricCalculationException>(
            () => MechanicalMetricCalculator.Calculate(
                input with
                {
                    RunRecord = mutatedRecord,
                    CanonicalRunRecord = BenchmarkContractCodec.Encode(mutatedRecord),
                    InputTranscript = mutatedBytes,
                }));
        Assert.Equal("metric.input-invalid", mutationException.Code);
    }

    [Fact]
    public void Resource_sample_and_terminal_mismatch_is_detected()
    {
        var input = FixtureInput();
        var resources = Encoding.UTF8.GetBytes(
            "{\"elapsedMs\":1200,\"observedCpuTimeMs\":20," +
            "\"observedProcessCount\":1,\"observedWorkingSetBytes\":99}\n");
        var record = input.RunRecord with
        {
            ResourceSamplesFile = Evidence(input.RunRecord.RunId, "resource-samples.ndjson", resources),
        };
        var exception = Assert.Throws<MechanicalMetricCalculationException>(
            () => MechanicalMetricCalculator.Calculate(
                input with
                {
                    RunRecord = record,
                    CanonicalRunRecord = BenchmarkContractCodec.Encode(record),
                    ResourceSamples = resources,
                }));

        Assert.Equal("metric.input-invalid", exception.Code);
    }

    [Fact]
    public void Separate_reference_free_oracle_matches_every_row_and_metric_set_byte_exactly()
    {
        var input = FixtureInput();
        var production = MechanicalMetricCalculator.Calculate(input);
        var oracle = RunOracle(input);

        Assert.Equal(132, oracle.RowCount);
        Assert.Equal(production.CanonicalRows, oracle.Rows);
        Assert.Equal(production.MetricSetHash, oracle.MetricSetHash);
        Assert.Equal(production.SemanticEvidenceSha256, oracle.SemanticEvidenceSha256);
        Assert.Equal(production.ResourceEvidenceSha256, oracle.ResourceEvidenceSha256);
        Assert.Matches("^[0-9a-f]{64}$", oracle.OracleAssemblySha256);
        MechanicalMetricOracleVerifier.Verify(
            production,
            oracle.Rows,
            oracle.MetricSetHash);

        var mutated = oracle.Rows.ToArray();
        mutated[^2] = mutated[^2] == (byte)']' ? (byte)'[' : (byte)']';
        var exception = Assert.Throws<MechanicalMetricCalculationException>(
            () => MechanicalMetricOracleVerifier.Verify(
                production,
                mutated,
                oracle.MetricSetHash));
        Assert.Equal("metric.oracle-mismatch", exception.Code);
    }

    [Fact]
    public void Oracle_project_has_no_production_or_contract_project_reference()
    {
        var project = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "tools",
                "RideBound.Wp6MetricOracle",
                "RideBound.Wp6MetricOracle.csproj"));

        Assert.DoesNotContain("ProjectReference", project, StringComparison.Ordinal);
        Assert.DoesNotContain("RideBound.Benchmarking", project, StringComparison.Ordinal);
        Assert.DoesNotContain("RideBound.Contracts", project, StringComparison.Ordinal);

        var source = string.Join(
            '\n',
            Directory.GetFiles(
                    Path.GetDirectoryName(Path.GetFullPath(
                        Path.Combine(
                            FindRepositoryRoot(),
                            "tools",
                            "RideBound.Wp6MetricOracle",
                            "RideBound.Wp6MetricOracle.csproj")))!,
                    "*.cs",
                    SearchOption.TopDirectoryOnly)
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));
        Assert.DoesNotContain("using RideBound.Benchmarking", source, StringComparison.Ordinal);
        Assert.DoesNotContain("using RideBound.Contracts", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MechanicalMetricCalculator", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Promise_vector_window_and_order_mutations_are_bound_into_exact_rows()
    {
        var input = WithPromiseDecisions(FixtureInput(), [3]);
        var production = MechanicalMetricCalculator.Calculate(input);
        var oracle = RunOracle(input);
        Assert.Equal(production.CanonicalRows, oracle.Rows);
        Assert.Equal(3, Row(production, "decisionDelta.pickupEtaTotalMs.sum", MetricWindowId.Scoring)
            .ValueInteger);
        Assert.Equal(3, Row(production, "decisionDelta.pickupEtaTotalMs.max", MetricWindowId.Scoring)
            .ValueInteger);
        Assert.Equal(1, Row(production, "promise.publication.count", MetricWindowId.Scoring)
            .ValueInteger);

        var shiftedWindow = input with
        {
            TimeWindow = input.TimeWindow with { ScoreStartMs = 1_201 },
        };
        var shifted = MechanicalMetricCalculator.Calculate(shiftedWindow);
        Assert.Equal(shifted.CanonicalRows, RunOracle(shiftedWindow).Rows);
        Assert.NotEqual(production.MetricSetHash, shifted.MetricSetHash);
        Assert.Equal(0, Row(shifted, "promise.publication.count", MetricWindowId.Scoring)
            .ValueInteger);
        Assert.Equal(1, Row(shifted, "promise.publication.count", MetricWindowId.Warmup)
            .ValueInteger);

        var reorderedOutput = SwapCanonicalLines(input.OutputTranscript, 2, 3);
        var reorderedRecord = input.RunRecord with
        {
            OutputFile = Evidence(input.RunRecord.RunId, "output.ndjson", reorderedOutput),
        };
        var reorderedInput = input with
        {
            RunRecord = reorderedRecord,
            CanonicalRunRecord = BenchmarkContractCodec.Encode(reorderedRecord),
            OutputTranscript = reorderedOutput,
        };
        var reordered = Assert.Throws<MechanicalMetricCalculationException>(
            () => MechanicalMetricCalculator.Calculate(reorderedInput));
        Assert.Equal("metric.input-invalid", reordered.Code);
        var reorderedOracle = ExecuteOracle(reorderedInput);
        Assert.Equal(2, reorderedOracle.ExitCode);
        Assert.StartsWith(
            "oracle.input-invalid:",
            reorderedOracle.StandardError,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Conflicting_action_outcome_mutation_is_rejected_by_both_calculators()
    {
        var input = FixtureInput();
        var output = Encoding.UTF8.GetBytes(
            Encoding.UTF8.GetString(input.OutputTranscript).Replace(
                "\"requestId\":\"r-1\"",
                "\"requestId\":\"r-2\"",
                StringComparison.Ordinal));
        var record = input.RunRecord with
        {
            OutputFile = Evidence(input.RunRecord.RunId, "output.ndjson", output),
        };
        var mutated = input with
        {
            RunRecord = record,
            CanonicalRunRecord = BenchmarkContractCodec.Encode(record),
            OutputTranscript = output,
        };

        var production = Assert.Throws<MechanicalMetricCalculationException>(
            () => MechanicalMetricCalculator.Calculate(mutated));
        Assert.Equal("metric.input-invalid", production.Code);
        var oracle = ExecuteOracle(mutated);
        Assert.Equal(2, oracle.ExitCode);
        Assert.StartsWith("oracle.input-invalid:", oracle.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public void Wider_intermediate_overflow_is_typed_by_production_and_oracle()
    {
        var input = WithPromiseDecisions(
            FixtureInput(),
            [ProtocolLimits.MaxCanonicalInteger, ProtocolLimits.MaxCanonicalInteger]);

        var production = Assert.Throws<MechanicalMetricCalculationException>(
            () => MechanicalMetricCalculator.Calculate(input));
        Assert.Equal("metric.overflow", production.Code);

        var oracle = ExecuteOracle(input);
        Assert.Equal(2, oracle.ExitCode);
        Assert.Equal(string.Empty, oracle.StandardOutput);
        Assert.StartsWith("oracle.overflow:", oracle.StandardError, StringComparison.Ordinal);
        Assert.Null(oracle.Rows);
        Assert.Null(oracle.Summary);
    }

    private static MetricRow Row(
        MechanicalMetricCalculationResult result,
        string metricId,
        MetricWindowId window) =>
        result.Rows.Single(value => value.MetricId == metricId && value.WindowId == window);

    private static MechanicalMetricCalculationInput FixtureInput()
    {
        var repository = FindRepositoryRoot();
        var input = CanonicalNdjson(
            Path.Combine(repository, "benchmarks", "scenarios", "wp2-tiny", "online-demo.input.ndjson"));
        var output = CanonicalNdjson(
            Path.Combine(repository, "benchmarks", "scenarios", "wp2-tiny", "online-demo.expected.ndjson"));
        var observation = Array.Empty<byte>();
        var resources = Encoding.UTF8.GetBytes(
            "{\"elapsedMs\":1200,\"observedCpuTimeMs\":20," +
            "\"observedProcessCount\":1,\"observedWorkingSetBytes\":100}\n");
        var record = Record(input, output, observation, resources);
        return new MechanicalMetricCalculationInput(
            record,
            Window,
            BenchmarkContractCodec.Encode(record),
            input,
            output,
            observation,
            resources,
            Registry(),
            new string('c', 64));
    }

    private static MechanicalMetricCalculationInput WithPromiseDecisions(
        MechanicalMetricCalculationInput input,
        IReadOnlyList<long> pickupEtaTotals)
    {
        Sha256Hex.TryCreate(new string('0', 64), out var zeroHash);
        var zero = new CommitmentVectorContract(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        var promise = new PromiseProjectionContract(
            "r-1",
            "v-1",
            "pickup-r-1",
            "n-1",
            "drop-r-1",
            "n-2",
            1_150,
            1_200,
            [
                new PromiseServiceTokenContract(
                    "pickup-r-1",
                    "r-1",
                    RouteStopKind.Pickup),
                new PromiseServiceTokenContract(
                    "drop-r-1",
                    "r-1",
                    RouteStopKind.DropOff),
            ]);
        var publicationIds = pickupEtaTotals.Select(
            (_, index) => $"metric-publication-{index + 1}").ToArray();
        var actions = pickupEtaTotals.Select(
                (value, index) => OnlineDecisionActionCodec.Encode(
                    new OnlineDecisionAction(
                        DecisionType.PromisePublished,
                        new PromisePublishedActionPayload(
                            publicationIds[index],
                            index + 1,
                            index == 0 ? "INITIAL_ACCEPTANCE" : "ROUTE_REVISION",
                            7,
                            promise,
                            zero,
                            zero with { PickupEtaTotalMs = value },
                            zero,
                            zero,
                            zero))))
            .ToArray();
        var certificate = new CommitmentCertificateBody(
            "1.0.0",
            "metric-oracle-test-validator",
            true,
            zeroHash!,
            zeroHash!,
            publicationIds,
            1,
            publicationIds.Length,
            []);
        var decision = new DecisionPayload(
            DecisionProductionStatus.Produced,
            DecisionReasonCodes.Accepted,
            actions,
            new CertificateShell(CertificateStatus.Produced, "VALIDATED", certificate),
            new SolverStatusShell(SolverStatus.Completed),
            zeroHash!,
            zeroHash!,
            zeroHash!,
            zeroHash!);
        using var payloadDocument = JsonDocument.Parse(DecisionPayloadCodec.Encode(decision));
        ProtocolMessageType.TryParse("decision", out var messageType);
        RunId.TryCreate(input.RunRecord.RunId, out var runId);
        ScenarioId.TryCreate("wp2-tiny-demo", out var scenarioId);
        EpochId.TryCreate(5, out var epochId);
        SimulationTimeMilliseconds.TryCreate(1_200, out var simTime);
        var envelope = new ProtocolEnvelope(
            ProtocolVersion.Current,
            messageType!,
            payloadDocument.RootElement.Clone(),
            runId,
            scenarioId,
            epochId,
            simTime);
        var line = CanonicalJson.Serialize(envelope);
        var output = new byte[input.OutputTranscript.Length + line.Length + 1];
        input.OutputTranscript.CopyTo(output, 0);
        line.CopyTo(output, input.OutputTranscript.Length);
        output[^1] = (byte)'\n';
        var record = input.RunRecord with
        {
            OutputFile = Evidence(input.RunRecord.RunId, "output.ndjson", output),
        };
        return input with
        {
            RunRecord = record,
            TimeWindow = input.TimeWindow with
            {
                HorizonEndMs = 1_300,
                DrainEndMs = 1_400,
            },
            CanonicalRunRecord = BenchmarkContractCodec.Encode(record),
            OutputTranscript = output,
        };
    }

    private static byte[] SwapCanonicalLines(byte[] bytes, int left, int right)
    {
        var lines = Encoding.UTF8.GetString(bytes)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        (lines[left], lines[right]) = (lines[right], lines[left]);
        return Encoding.UTF8.GetBytes(string.Join('\n', lines) + "\n");
    }

    private static byte[] CanonicalNdjson(string path)
    {
        using var output = new MemoryStream();

        foreach (var line in File.ReadLines(path))
        {
            var canonical = CanonicalJson.Canonicalize(Encoding.UTF8.GetBytes(line));
            output.Write(canonical);
            output.WriteByte((byte)'\n');
        }

        return output.ToArray();
    }

    private static OracleOutput RunOracle(MechanicalMetricCalculationInput input)
    {
        var process = ExecuteOracle(input);
        Assert.Equal(0, process.ExitCode);
        Assert.Equal(string.Empty, process.StandardOutput);
        Assert.Equal(string.Empty, process.StandardError);
        Assert.NotNull(process.Rows);
        Assert.NotNull(process.Summary);
        using var summary = JsonDocument.Parse(process.Summary);
        var value = summary.RootElement;
        return new OracleOutput(
            process.Rows,
            value.GetProperty("metricSetHash").GetString()!,
            value.GetProperty("semanticEvidenceSha256").GetString()!,
            value.GetProperty("resourceEvidenceSha256").GetString()!,
            value.GetProperty("oracleAssemblySha256").GetString()!,
            value.GetProperty("rowCount").GetInt32());
    }

    private static OracleProcessOutput ExecuteOracle(MechanicalMetricCalculationInput input)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "ridebound-wp6-metric-oracle-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var runRecordPath = Write(root, "run-record.json", input.CanonicalRunRecord);
            var inputPath = Write(root, "input.ndjson", input.InputTranscript);
            var outputPath = Write(root, "output.ndjson", input.OutputTranscript);
            var indexPath = Write(root, "observation-index.ndjson", input.ObservationIndex);
            var resourcePath = Write(root, "resource-samples.ndjson", input.ResourceSamples);
            var rowsPath = Path.Combine(root, "rows.json");
            var summaryPath = Path.Combine(root, "summary.json");
            var requestPath = Write(
                root,
                "request.json",
                JsonSerializer.SerializeToUtf8Bytes(
                    new
                    {
                        registryPath = Path.Combine(
                            FindRepositoryRoot(),
                            "benchmarks",
                            "fixtures",
                            "wp6",
                            "metrics",
                            "mechanical-metric-registry-v1.json"),
                        runRecordPath,
                        inputTranscriptPath = inputPath,
                        outputTranscriptPath = outputPath,
                        observationIndexPath = indexPath,
                        resourceSamplesPath = resourcePath,
                        warmupStartMs = input.TimeWindow.WarmupStartMs,
                        scoreStartMs = input.TimeWindow.ScoreStartMs,
                        horizonEndMs = input.TimeWindow.HorizonEndMs,
                        drainEndMs = input.TimeWindow.DrainEndMs,
                        calculatorSourceSha256 = input.CalculatorSourceSha256,
                    }));
            var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
            var oracleDll = Path.Combine(
                FindRepositoryRoot(),
                "tools",
                "RideBound.Wp6MetricOracle",
                "bin",
                configuration,
                "net10.0",
                "RideBound.Wp6MetricOracle.dll");
            Assert.True(File.Exists(oracleDll), oracleDll);
            var start = new ProcessStartInfo("dotnet")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            start.ArgumentList.Add(oracleDll);
            start.ArgumentList.Add("--request");
            start.ArgumentList.Add(requestPath);
            start.ArgumentList.Add("--rows-out");
            start.ArgumentList.Add(rowsPath);
            start.ArgumentList.Add("--summary-out");
            start.ArgumentList.Add(summaryPath);

            using var process = Process.Start(start)
                ?? throw new InvalidOperationException("Independent oracle did not start.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30_000))
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException("Independent oracle timed out.");
            }

            return new OracleProcessOutput(
                process.ExitCode,
                stdout.GetAwaiter().GetResult(),
                stderr.GetAwaiter().GetResult(),
                File.Exists(rowsPath) ? File.ReadAllBytes(rowsPath) : null,
                File.Exists(summaryPath) ? File.ReadAllBytes(summaryPath) : null);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string Write(string root, string name, byte[] bytes)
    {
        var path = Path.Combine(root, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static RunRecord Record(
        byte[] input,
        byte[] output,
        byte[] observation,
        byte[] resources)
    {
        const string runId = "wp2-demo-run";
        return new RunRecord(
            BenchmarkContractVersions.V1,
            runId,
            new string('1', 64),
            new string('2', 64),
            "b1",
            0,
            0,
            new string('3', 64),
            new string('4', 64),
            new string('5', 64),
            new string('6', 64),
            new string('7', 64),
            1,
            false,
            RunTerminalStatus.Succeeded,
            "2026-08-11T00:00:00Z",
            "2026-08-11T00:00:01Z",
            1_200,
            20,
            100,
            1,
            new string('8', 64),
            new string('8', 64),
            Evidence(runId, "input.ndjson", input),
            Evidence(runId, "output.ndjson", output),
            Evidence(runId, "stderr.txt", []),
            Evidence(runId, "resource-samples.ndjson", resources),
            Evidence(runId, "observation-index.ndjson", observation),
            ExitCode: 0);
    }

    private static RunFileEvidence Evidence(string runId, string name, byte[] bytes) =>
        new(
            $"runs/{runId}/{name}",
            bytes.LongLength,
            Convert.ToHexStringLower(SHA256.HashData(bytes)));

    private static MechanicalMetricRegistry Registry() =>
        MechanicalMetricRegistry.Load(
            Path.Combine(
                FindRepositoryRoot(),
                "benchmarks",
                "fixtures",
                "wp6",
                "metrics",
                "mechanical-metric-registry-v1.json"));

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "RideBound.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private sealed record OracleOutput(
        byte[] Rows,
        string MetricSetHash,
        string SemanticEvidenceSha256,
        string ResourceEvidenceSha256,
        string OracleAssemblySha256,
        int RowCount);

    private sealed record OracleProcessOutput(
        int ExitCode,
        string StandardOutput,
        string StandardError,
        byte[]? Rows,
        byte[]? Summary);
}
