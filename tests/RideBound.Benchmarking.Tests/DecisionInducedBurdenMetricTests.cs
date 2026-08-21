using System.Diagnostics;
using System.Text;
using RideBound.Benchmarking.Metrics;
using RideBound.Contracts.Serialization;

namespace RideBound.Benchmarking.Tests;

public sealed class DecisionInducedBurdenMetricTests
{
    private const string RunId = "test-run-wp8-005";

    [Fact]
    public void Calculator_enforces_lifecycle_and_sums_the_full_10d_vector()
    {
        var result = DecisionInducedBurdenCalculator.CalculateFromTranscripts(
            RunId,
            "arm-b1",
            "rolling-cost",
            ValidInput(),
            ValidOutput());

        Assert.Equal(RunId, result.RunId);
        Assert.Equal(2, result.ArrivedRiderCount);
        Assert.Equal(1, result.AcceptedRiderCount);
        Assert.Equal(1, result.RejectedRiderCount);
        Assert.Equal(1, result.CompletedRiderCount);
        Assert.Equal(10_000, result.PickupEtaDecisionDeltaSumMs);
        Assert.Equal(170_000, result.DropEtaDecisionDeltaSumMs);
        Assert.Equal(180_000, result.TotalDecisionInducedBurdenMs);
        Assert.Equal(1, result.MaterialEtaRevisionCount);
        Assert.Equal(3, result.PrePickupInsertedStopCount);
        Assert.Equal(1, result.DropStopSwitchCount);
        Assert.Equal(1, result.DisruptiveRevisionFrameCount);
    }

    [Fact]
    public void Result_codec_is_canonical_and_contains_no_floating_point_rate()
    {
        var result = DecisionInducedBurdenCalculator.CalculateFromTranscripts(
            RunId,
            "arm-b1",
            "rolling-cost",
            ValidInput(),
            ValidOutput());
        var encoded = DecisionInducedBurdenResultCodec.Encode(result);
        var text = Encoding.UTF8.GetString(encoded);

        Assert.Equal(encoded, CanonicalJson.Canonicalize(encoded));
        Assert.DoesNotContain("completionRate", text, StringComparison.Ordinal);
        Assert.Contains("\"arrivedRiderCount\":2", text, StringComparison.Ordinal);
        Assert.Contains("\"completedRiderCount\":1", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Reference_free_oracle_matches_production_byte_for_byte_and_rejects_mutation()
    {
        var input = ValidInput();
        var output = ValidOutput();
        var production = DecisionInducedBurdenResultCodec.Encode(
            DecisionInducedBurdenCalculator.CalculateFromTranscripts(
                RunId,
                "arm-b1",
                "rolling-cost",
                input,
                output));

        var oracle = ExecuteOracle(input, output);

        Assert.Equal(0, oracle.ExitCode);
        Assert.Equal(string.Empty, oracle.StandardOutput);
        Assert.Equal(string.Empty, oracle.StandardError);
        Assert.Equal(production, oracle.Result);

        var mutated = input.ToArray();
        var marker = Encoding.UTF8.GetBytes("req-2");
        var index = mutated.AsSpan().IndexOf(marker);
        Assert.True(index >= 0);
        mutated[index + marker.Length - 1] = (byte)'3';
        var rejected = ExecuteOracle(mutated, output);

        Assert.Equal(2, rejected.ExitCode);
        Assert.StartsWith(
            "oracle.input-invalid:",
            rejected.StandardError,
            StringComparison.Ordinal);
        Assert.Null(rejected.Result);
    }

    [Theory]
    [InlineData("requestArrived", "request arrival")]
    [InlineData("bookingConfirmed", "booking confirmation")]
    [InlineData("passengerBoarded", "passenger boarding")]
    [InlineData("passengerAlighted", "passenger alighting")]
    public void Duplicate_lifecycle_evidence_is_rejected(
        string eventType,
        string expectedMessage)
    {
        var duplicate = eventType == "requestArrived"
            ? "{\"eventSeq\":9,\"eventType\":\"requestArrived\",\"payload\":{\"request\":{\"requestId\":\"req-1\"}},\"simTimeMs\":2000}"
            : $"{{\"eventSeq\":9,\"eventType\":\"{eventType}\",\"payload\":{{\"requestId\":\"req-1\"}},\"simTimeMs\":2000}}";
        var input = CanonicalNdjson(
            InputBatch(1, 1_000, ArrivalEvents()),
            InputBatch(2, 2_000, TerminalEvents() + "," + duplicate));

        var error = Assert.Throws<InvalidDataException>(() =>
            DecisionInducedBurdenCalculator.CalculateFromTranscripts(
                RunId,
                "arm-b1",
                "rolling-cost",
                input,
                ValidOutput()));

        Assert.Contains(expectedMessage, error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Accepted_and_rejected_must_be_an_exact_partition_of_arrivals()
    {
        var missingRejection = CanonicalNdjson(
            OutputDecision(1, 1_000, AcceptedAndPromises()),
            OutputDecision(2, 2_000, string.Empty));

        Assert.Throws<InvalidDataException>(() =>
            DecisionInducedBurdenCalculator.CalculateFromTranscripts(
                RunId,
                "arm-b1",
                "rolling-cost",
                ValidInput(),
                missingRejection));
    }

    [Fact]
    public void Event_and_decision_contexts_must_match_in_order()
    {
        var wrongEpoch = CanonicalNdjson(
            OutputDecision(1, 1_000, AcceptedRejectedAndPromises()),
            OutputDecision(3, 2_000, string.Empty));

        Assert.Throws<InvalidDataException>(() =>
            DecisionInducedBurdenCalculator.CalculateFromTranscripts(
                RunId,
                "arm-b1",
                "rolling-cost",
                ValidInput(),
                wrongEpoch));
    }

    [Fact]
    public void Valid_but_noncanonical_ndjson_is_rejected()
    {
        var noncanonical = Encoding.UTF8.GetBytes(
            "{ \"messageType\": \"eventBatch\", \"schemaVersion\": \"1.0.0\", \"runId\": \"test-run-wp8-005\", \"scenarioId\": \"sc-1\", \"epochId\": 1, \"simTimeMs\": 1000, \"payload\": { \"events\": [] } }\n");

        Assert.Throws<InvalidDataException>(() =>
            DecisionInducedBurdenCalculator.CalculateFromTranscripts(
                RunId,
                "arm-b1",
                "rolling-cost",
                noncanonical,
                ValidOutput()));
    }

    private static byte[] ValidInput() => CanonicalNdjson(
        InputBatch(1, 1_000, ArrivalEvents()),
        InputBatch(2, 2_000, TerminalEvents()));

    private static byte[] ValidOutput() => CanonicalNdjson(
        OutputDecision(1, 1_000, AcceptedRejectedAndPromises()),
        OutputDecision(2, 2_000, string.Empty));

    private static string InputBatch(int epoch, int simTime, string events) =>
        $"{{\"schemaVersion\":\"1.0.0\",\"messageType\":\"eventBatch\",\"runId\":\"{RunId}\",\"scenarioId\":\"sc-1\",\"epochId\":{epoch},\"simTimeMs\":{simTime},\"payload\":{{\"events\":[{events}]}}}}";

    private static string OutputDecision(int epoch, int simTime, string actions) =>
        $"{{\"schemaVersion\":\"1.0.0\",\"messageType\":\"decision\",\"runId\":\"{RunId}\",\"scenarioId\":\"sc-1\",\"epochId\":{epoch},\"simTimeMs\":{simTime},\"payload\":{{\"actions\":[{actions}]}}}}";

    private static string ArrivalEvents() =>
        "{\"eventSeq\":1,\"eventType\":\"requestArrived\",\"payload\":{\"request\":{\"requestId\":\"req-1\"}},\"simTimeMs\":1000}," +
        "{\"eventSeq\":2,\"eventType\":\"requestArrived\",\"payload\":{\"request\":{\"requestId\":\"req-2\"}},\"simTimeMs\":1000}";

    private static string TerminalEvents() =>
        "{\"eventSeq\":3,\"eventType\":\"bookingConfirmed\",\"payload\":{\"requestId\":\"req-1\"},\"simTimeMs\":2000}," +
        "{\"eventSeq\":4,\"eventType\":\"passengerBoarded\",\"payload\":{\"requestId\":\"req-1\",\"vehicleId\":\"veh-1\"},\"simTimeMs\":2000}," +
        "{\"eventSeq\":5,\"eventType\":\"passengerAlighted\",\"payload\":{\"requestId\":\"req-1\"},\"simTimeMs\":2000}";

    private static string AcceptedRejectedAndPromises() =>
        AcceptedAndPromises() +
        ",{\"decisionType\":\"requestRejected\",\"payload\":{\"requestId\":\"req-2\",\"reasonCode\":\"NO_FEASIBLE_INSERTION\"}}";

    private static string AcceptedAndPromises() =>
        "{\"decisionType\":\"requestAccepted\",\"payload\":{\"requestId\":\"req-1\"}}," +
        Promise("req-1", 10_000, 50_000, 0, 1, 1) + "," +
        Promise("req-1", 0, 120_000, 1, 2, 0);

    private static string Promise(
        string requestId,
        long pickup,
        long drop,
        long material,
        long prePickup,
        long dropStopSwitch) =>
        $"{{\"decisionType\":\"promisePublished\",\"payload\":{{\"requestId\":\"{requestId}\",\"decisionDelta\":{{\"dropEtaTotalMs\":{drop},\"dropStopRelocationMm\":0,\"dropStopSwitchCount\":{dropStopSwitch},\"incumbentOrderInversionCount\":0,\"materialEtaRevisionCount\":{material},\"pickupEtaTotalMs\":{pickup},\"pickupStopRelocationMm\":0,\"pickupStopSwitchCount\":0,\"prePickupInsertedStopCount\":{prePickup},\"vehicleSwitchCount\":0}}}}}}";

    private static byte[] CanonicalNdjson(params string[] lines)
    {
        using var stream = new MemoryStream();
        foreach (var line in lines)
        {
            var canonical = CanonicalJson.Canonicalize(Encoding.UTF8.GetBytes(line));
            stream.Write(canonical);
            stream.WriteByte((byte)'\n');
        }

        return stream.ToArray();
    }

    private static OracleProcessResult ExecuteOracle(byte[] input, byte[] output)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "ridebound-wp8-burden-oracle-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var inputPath = Path.Combine(root, "input.ndjson");
            var outputPath = Path.Combine(root, "output.ndjson");
            var resultPath = Path.Combine(root, "burden.json");
            File.WriteAllBytes(inputPath, input);
            File.WriteAllBytes(outputPath, output);
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
            start.ArgumentList.Add("--burden-run-id");
            start.ArgumentList.Add(RunId);
            start.ArgumentList.Add("--burden-arm-id");
            start.ArgumentList.Add("arm-b1");
            start.ArgumentList.Add("--burden-policy-id");
            start.ArgumentList.Add("rolling-cost");
            start.ArgumentList.Add("--input-transcript");
            start.ArgumentList.Add(inputPath);
            start.ArgumentList.Add("--output-transcript");
            start.ArgumentList.Add(outputPath);
            start.ArgumentList.Add("--burden-out");
            start.ArgumentList.Add(resultPath);

            using var process = Process.Start(start)
                ?? throw new InvalidOperationException("Burden oracle did not start.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30_000))
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException("Burden oracle timed out.");
            }

            return new OracleProcessResult(
                process.ExitCode,
                stdout.GetAwaiter().GetResult(),
                stderr.GetAwaiter().GetResult(),
                File.Exists(resultPath) ? File.ReadAllBytes(resultPath) : null);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

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

        throw new DirectoryNotFoundException("RideBound repository root was not found.");
    }

    private sealed record OracleProcessResult(
        int ExitCode,
        string StandardOutput,
        string StandardError,
        byte[]? Result);
}
