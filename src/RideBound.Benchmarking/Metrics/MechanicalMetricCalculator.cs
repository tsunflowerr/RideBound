using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using RideBound.Benchmarking.Contracts;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;

namespace RideBound.Benchmarking.Metrics;

public static class MechanicalMetricCalculator
{
    private const long SafeIntegerMaximum = 9_007_199_254_740_991;
    private static readonly MetricWindowId[] Windows =
    [
        MetricWindowId.All,
        MetricWindowId.Warmup,
        MetricWindowId.Scoring,
        MetricWindowId.Drain,
    ];
    private static readonly VectorDimension[] Dimensions =
    [
        new("dropEtaTotalMs", "millisecond"),
        new("dropStopRelocationMm", "millimeter"),
        new("dropStopSwitchCount", "count"),
        new("incumbentOrderInversionCount", "count"),
        new("materialEtaRevisionCount", "count"),
        new("pickupEtaTotalMs", "millisecond"),
        new("pickupStopRelocationMm", "millimeter"),
        new("pickupStopSwitchCount", "count"),
        new("prePickupInsertedStopCount", "count"),
        new("vehicleSwitchCount", "count"),
    ];

    public static MechanicalMetricCalculationResult Calculate(
        MechanicalMetricCalculationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        try
        {
            return CalculateCore(input);
        }
        catch (MechanicalMetricCalculationException)
        {
            throw;
        }
        catch (OverflowException exception)
        {
            throw new MechanicalMetricCalculationException(
                "metric.overflow",
                "Mechanical metric arithmetic crossed the canonical integer range.",
                exception);
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or InvalidDataException
                or InvalidOperationException
                or JsonException
                or CryptographicException)
        {
            throw new MechanicalMetricCalculationException(
                "metric.input-invalid",
                "Raw metric evidence is invalid or inconsistent.",
                exception);
        }
    }

    private static MechanicalMetricCalculationResult CalculateCore(
        MechanicalMetricCalculationInput input)
    {
        var record = input.RunRecord;

        if (record.TerminalStatus != RunTerminalStatus.Succeeded)
        {
            throw new MechanicalMetricCalculationException(
                "metric.terminal-not-succeeded",
                "Only a verified succeeded terminal run can produce outcome metrics.");
        }

        RequireHash(input.CalculatorSourceSha256, nameof(input.CalculatorSourceSha256));
        ValidateWindow(input.TimeWindow);
        var canonicalRecord = BenchmarkContractCodec.Encode(record);

        if (!canonicalRecord.SequenceEqual(input.CanonicalRunRecord))
        {
            throw new InvalidDataException("Canonical run record differs from its decoded value.");
        }

        RequireEvidence(record.InputFile, input.InputTranscript);
        RequireEvidence(record.OutputFile, input.OutputTranscript);
        RequireEvidence(record.ObservationIndexFile, input.ObservationIndex);
        RequireEvidence(record.ResourceSamplesFile, input.ResourceSamples);
        var semanticEvidence = MetricEvidenceIdentity.CalculateSemantic(
            input.InputTranscript,
            input.OutputTranscript,
            input.ObservationIndex);
        var resourceEvidence = MetricEvidenceIdentity.CalculateResource(
            input.CanonicalRunRecord,
            input.ResourceSamples);
        var state = ParseSemanticEvidence(input);
        var rows = new List<MetricRow>(132);

        foreach (var definition in input.Registry.Definitions.Where(
                     value => value.SourceKind == "rawTranscript"))
        {
            foreach (var window in Windows)
            {
                rows.Add(CreateSemanticRow(input, definition, window, state, semanticEvidence));
            }
        }

        var resources = ParseAndVerifyResources(input.ResourceSamples, record);

        foreach (var definition in input.Registry.Definitions.Where(
                     value => value.SourceKind == "rawSupervisor"))
        {
            rows.Add(
                Observed(
                    input,
                    definition,
                    MetricWindowId.All,
                    ResourceValue(definition.MetricId, resources),
                    resourceEvidence));
        }

        var ordered = rows.OrderBy(value => value.RunId, StringComparer.Ordinal)
            .ThenBy(value => value.MetricId, StringComparer.Ordinal)
            .ThenBy(value => ScopeWire(value.ScopeKind), StringComparer.Ordinal)
            .ThenBy(value => value.ScopeId, StringComparer.Ordinal)
            .ThenBy(value => WindowWire(value.WindowId), StringComparer.Ordinal)
            .ToArray();
        var canonicalRows = EncodeRows(ordered);
        var metricSetHash = BenchmarkIdentity.CalculateMetricSet(
            record.RunId,
            input.Registry.RegistryHash,
            canonicalRows);
        return new MechanicalMetricCalculationResult(
            ordered,
            canonicalRows,
            metricSetHash,
            semanticEvidence,
            resourceEvidence);
    }

    private static SemanticState ParseSemanticEvidence(
        MechanicalMetricCalculationInput input)
    {
        var state = new SemanticState();

        foreach (var envelope in ReadTranscript(input.InputTranscript, input.RunRecord.RunId))
        {
            if (envelope.MessageType.Value != "eventBatch")
            {
                continue;
            }

            var decoded = EventBatchPayloadCodec.Decode(envelope.Payload);

            if (!decoded.IsSuccess)
            {
                throw new InvalidDataException(decoded.Error!.Message);
            }

            var simTime = envelope.SimTime!.Value.Value;
            _ = PhaseWindow(simTime, input.TimeWindow);

            foreach (var item in envelope.Payload.GetProperty("events").EnumerateArray())
            {
                var eventType = item.GetProperty("eventType").GetString();
                var payload = item.GetProperty("payload");

                if (eventType == "requestArrived")
                {
                    var requestId = payload.GetProperty("request")
                        .GetProperty("requestId").GetString()!;

                    if (!state.ArrivalTimes.TryAdd(requestId, simTime))
                    {
                        throw new InvalidDataException("Request arrival is duplicated.");
                    }
                }
                else if (eventType == "passengerAlighted")
                {
                    if (!state.Completions.TryAdd(
                            payload.GetProperty("requestId").GetString()!,
                            simTime))
                    {
                        throw new InvalidDataException("Request completion is duplicated.");
                    }
                }
            }
        }

        foreach (var envelope in ReadTranscript(input.OutputTranscript, input.RunRecord.RunId))
        {
            if (envelope.MessageType.Value != "decision")
            {
                continue;
            }

            var decoded = DecisionPayloadCodec.Decode(envelope.Payload);

            if (!decoded.IsSuccess)
            {
                throw new InvalidDataException(decoded.Error!.Message);
            }

            var simTime = envelope.SimTime!.Value.Value;
            var phase = PhaseWindow(simTime, input.TimeWindow);
            state.For(MetricWindowId.All).DecisionCount++;
            state.For(phase).DecisionCount++;

            if (decoded.Value!.Certificate.Body is { NormalOperation: false })
            {
                state.For(MetricWindowId.All).NonNormalCertificateCount++;
                state.For(phase).NonNormalCertificateCount++;
            }

            foreach (var action in envelope.Payload.GetProperty("actions").EnumerateArray())
            {
                ParseAction(action, simTime, phase, state);
            }
        }

        ValidateRequestOutcomes(state);
        return state;
    }

    private static void ParseAction(
        JsonElement action,
        long simTime,
        MetricWindowId phase,
        SemanticState state)
    {
        var type = action.GetProperty("decisionType").GetString();
        var payload = action.GetProperty("payload");

        switch (type)
        {
            case "requestAccepted":
                if (!state.Acceptances.TryAdd(
                        payload.GetProperty("requestId").GetString()!,
                        simTime))
                {
                    throw new InvalidDataException("Request acceptance is duplicated.");
                }

                break;
            case "requestRejected":
                if (!state.Rejections.TryAdd(
                        payload.GetProperty("requestId").GetString()!,
                        simTime))
                {
                    throw new InvalidDataException("Request rejection is duplicated.");
                }

                break;
            case "requestDeferred":
                state.DeferredActions.Add(
                    new TimedRequest(
                        payload.GetProperty("requestId").GetString()!,
                        simTime));
                break;
            case "promisePublished":
                AddPromise(payload, state.For(MetricWindowId.All));
                AddPromise(payload, state.For(phase));
                break;
            case "commitmentBreachDeclared":
                state.For(MetricWindowId.All).BreachCount++;
                state.For(phase).BreachCount++;
                break;
        }
    }

    private static void AddPromise(JsonElement payload, WindowAccumulator accumulator)
    {
        accumulator.PromisePublicationCount++;

        if (payload.GetProperty("promiseVersion").GetInt64() > 1)
        {
            accumulator.PromiseRevisionCount++;
        }

        var vector = payload.GetProperty("decisionDelta");

        foreach (var dimension in Dimensions)
        {
            accumulator.AddDelta(
                dimension.Name,
                new BigInteger(vector.GetProperty(dimension.Name).GetInt64()));
        }
    }

    private static void ValidateRequestOutcomes(SemanticState state)
    {
        if (state.Acceptances.Keys.Intersect(
                state.Rejections.Keys,
                StringComparer.Ordinal).Any())
        {
            throw new InvalidDataException("Request has conflicting terminal outcomes.");
        }

        foreach (var (requestId, acceptedAt) in state.Acceptances)
        {
            if (!state.ArrivalTimes.TryGetValue(requestId, out var arrivedAt)
                || acceptedAt < arrivedAt)
            {
                throw new InvalidDataException("Accepted request has no preceding arrival.");
            }
        }

        foreach (var (requestId, rejectedAt) in state.Rejections)
        {
            if (!state.ArrivalTimes.TryGetValue(requestId, out var arrivedAt)
                || rejectedAt < arrivedAt)
            {
                throw new InvalidDataException("Rejected request has no preceding arrival.");
            }
        }

        foreach (var (requestId, completedAt) in state.Completions)
        {
            if (!state.ArrivalTimes.ContainsKey(requestId))
            {
                continue;
            }

            if (!state.Acceptances.TryGetValue(requestId, out var acceptedAt)
                || completedAt < acceptedAt)
            {
                throw new InvalidDataException(
                    "Known request completion has no preceding acceptance.");
            }
        }

        foreach (var action in state.DeferredActions)
        {
            if (!state.ArrivalTimes.TryGetValue(action.RequestId, out var arrivedAt)
                || action.SimTimeMs < arrivedAt)
            {
                throw new InvalidDataException("Deferred request has no preceding arrival.");
            }

            var terminalAt = state.Acceptances.GetValueOrDefault(
                action.RequestId,
                state.Rejections.GetValueOrDefault(action.RequestId, long.MaxValue));

            if (action.SimTimeMs > terminalAt)
            {
                throw new InvalidDataException("Request was deferred after a terminal outcome.");
            }
        }
    }

    private static MetricRow CreateSemanticRow(
        MechanicalMetricCalculationInput input,
        MechanicalMetricDefinition definition,
        MetricWindowId window,
        SemanticState state,
        string evidenceSha256)
    {
        var accumulator = state.For(window);
        var cohort = state.ArrivalTimes
            .Where(value => IsInWindow(value.Value, window, input.TimeWindow))
            .Select(value => value.Key)
            .ToHashSet(StringComparer.Ordinal);
        var arrived = cohort.Count;
        var accepted = cohort.Count(state.Acceptances.ContainsKey);
        var rejected = cohort.Count(
            requestId => state.Rejections.ContainsKey(requestId)
                && !state.Acceptances.ContainsKey(requestId));
        var completed = cohort.Count(
            requestId => state.Completions.ContainsKey(requestId)
                && state.Acceptances.ContainsKey(requestId));
        var deferred = state.DeferredActions.Count(value => cohort.Contains(value.RequestId));

        return definition.MetricId switch
        {
            "certificate.non-normal.count" =>
                Observed(input, definition, window, accumulator.NonNormalCertificateCount, evidenceSha256),
            "commitment.breach.count" =>
                Observed(input, definition, window, accumulator.BreachCount, evidenceSha256),
            "decision.epoch.count" =>
                Observed(input, definition, window, accumulator.DecisionCount, evidenceSha256),
            "promise.publication.count" =>
                Observed(input, definition, window, accumulator.PromisePublicationCount, evidenceSha256),
            "promise.revision.count" =>
                Observed(input, definition, window, accumulator.PromiseRevisionCount, evidenceSha256),
            "request.arrived.count" =>
                Observed(input, definition, window, arrived, evidenceSha256),
            "request.accepted.count" =>
                Observed(input, definition, window, accepted, evidenceSha256),
            "request.rejected.count" =>
                Observed(input, definition, window, rejected, evidenceSha256),
            "request.deferred.action.count" =>
                Observed(input, definition, window, deferred, evidenceSha256),
            "request.completed.count" =>
                Observed(input, definition, window, completed, evidenceSha256),
            "request.acceptance.ppm" =>
                Ratio(input, definition, window, accepted, arrived, evidenceSha256),
            "request.completion.ppm" =>
                Ratio(input, definition, window, completed, accepted, evidenceSha256),
            _ when TryDelta(definition.MetricId, out var dimension, out var aggregate) =>
                Delta(input, definition, window, accumulator, dimension!, aggregate!, evidenceSha256),
            _ => throw new InvalidDataException("Registry contains an unsupported semantic metric."),
        };
    }

    private static MetricRow Delta(
        MechanicalMetricCalculationInput input,
        MechanicalMetricDefinition definition,
        MetricWindowId window,
        WindowAccumulator accumulator,
        string dimension,
        string aggregate,
        string evidenceSha256)
    {
        if (aggregate == "sum")
        {
            return Observed(
                input,
                definition,
                window,
                ToSafeLong(accumulator.DeltaSums.GetValueOrDefault(dimension)),
                evidenceSha256);
        }

        if (!accumulator.DeltaMaximums.TryGetValue(dimension, out var maximum))
        {
            return Missing(input, definition, window, "no-publication", evidenceSha256);
        }

        return Observed(
            input,
            definition,
            window,
            ToSafeLong(maximum),
            evidenceSha256);
    }

    private static MetricRow Ratio(
        MechanicalMetricCalculationInput input,
        MechanicalMetricDefinition definition,
        MetricWindowId window,
        long numerator,
        long denominator,
        string evidenceSha256)
    {
        if (denominator == 0)
        {
            return Missing(
                input,
                definition,
                window,
                "denominator-zero",
                evidenceSha256,
                numerator,
                denominator);
        }

        return Observed(
            input,
            definition,
            window,
            PartsPerMillion(numerator, denominator),
            evidenceSha256,
            numerator,
            denominator);
    }

    private static MetricRow Observed(
        MechanicalMetricCalculationInput input,
        MechanicalMetricDefinition definition,
        MetricWindowId window,
        long value,
        string evidenceSha256,
        long? numerator = null,
        long? denominator = null) =>
        new(
            BenchmarkContractVersions.V1,
            input.Registry.RegistryHash,
            definition.MetricId,
            definition.MetricVersion,
            input.RunRecord.RunId,
            input.RunRecord.ScenarioHash,
            input.RunRecord.ArmId,
            input.RunRecord.RepeatIndex,
            input.RunRecord.AttemptIndex,
            MetricScopeKind.Run,
            input.RunRecord.RunId,
            window,
            MetricValueStatus.Observed,
            definition.UnitId,
            evidenceSha256,
            input.CalculatorSourceSha256,
            value,
            numerator,
            denominator is null ? null : definition.DenominatorId,
            denominator);

    private static MetricRow Missing(
        MechanicalMetricCalculationInput input,
        MechanicalMetricDefinition definition,
        MetricWindowId window,
        string reason,
        string evidenceSha256,
        long? numerator = null,
        long? denominator = null) =>
        new(
            BenchmarkContractVersions.V1,
            input.Registry.RegistryHash,
            definition.MetricId,
            definition.MetricVersion,
            input.RunRecord.RunId,
            input.RunRecord.ScenarioHash,
            input.RunRecord.ArmId,
            input.RunRecord.RepeatIndex,
            input.RunRecord.AttemptIndex,
            MetricScopeKind.Run,
            input.RunRecord.RunId,
            window,
            MetricValueStatus.Missing,
            definition.UnitId,
            evidenceSha256,
            input.CalculatorSourceSha256,
            ValueInteger: null,
            NumeratorInteger: numerator,
            DenominatorId: denominator is null ? null : definition.DenominatorId,
            DenominatorInteger: denominator,
            MissingReasonId: reason);

    private static ResourceValues ParseAndVerifyResources(byte[] bytes, RunRecord record)
    {
        long elapsed = -1;
        long cpu = 0;
        long workingSet = 0;
        long processCount = 0;
        var rowCount = 0;

        foreach (var line in ReadCanonicalLines(bytes))
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            var names = root.EnumerateObject().Select(value => value.Name)
                .ToHashSet(StringComparer.Ordinal);

            if (!names.SetEquals(
                [
                    "elapsedMs",
                    "observedCpuTimeMs",
                    "observedWorkingSetBytes",
                    "observedProcessCount",
                ]))
            {
                throw new InvalidDataException("Resource sample fields are invalid.");
            }

            var currentElapsed = Nonnegative(root, "elapsedMs");

            if (currentElapsed < elapsed)
            {
                throw new InvalidDataException("Resource sample time regressed.");
            }

            elapsed = currentElapsed;
            cpu = Math.Max(cpu, Nonnegative(root, "observedCpuTimeMs"));
            workingSet = Math.Max(workingSet, Nonnegative(root, "observedWorkingSetBytes"));
            processCount = Math.Max(processCount, Nonnegative(root, "observedProcessCount"));
            rowCount++;
        }

        if (rowCount == 0
            || elapsed != record.WallTimeMs
            || cpu != record.CpuTimeMs
            || workingSet != record.PeakWorkingSetBytes
            || processCount != record.SpawnedProcessCount)
        {
            throw new InvalidDataException("Terminal resources differ from raw samples.");
        }

        return new ResourceValues(elapsed, cpu, workingSet, processCount);
    }

    private static long ResourceValue(string metricId, ResourceValues resources) =>
        metricId switch
        {
            "resource.wall-time-ms" => resources.WallTimeMs,
            "resource.cpu-time-ms" => resources.CpuTimeMs,
            "resource.peak-working-set-bytes" => resources.PeakWorkingSetBytes,
            "resource.process-count-max" => resources.ProcessCount,
            _ => throw new InvalidDataException("Registry contains an unsupported resource metric."),
        };

    private static IReadOnlyList<ProtocolEnvelope> ReadTranscript(byte[] bytes, string runId)
    {
        var values = new List<ProtocolEnvelope>();
        long? previousSimTime = null;
        long? previousEpoch = null;

        foreach (var line in ReadCanonicalLines(bytes))
        {
            var decoded = ProtocolEnvelopeCodec.Decode(line);

            if (!decoded.IsSuccess
                || decoded.Envelope!.RunId is not null
                    && decoded.Envelope.RunId.Value != runId)
            {
                throw new InvalidDataException("Protocol transcript is invalid or cross-linked.");
            }

            if (decoded.Envelope.SimTime is not null)
            {
                var currentSimTime = decoded.Envelope.SimTime.Value.Value;
                var currentEpoch = decoded.Envelope.EpochId!.Value.Value;

                if (previousSimTime is not null
                    && (currentSimTime < previousSimTime.Value
                        || currentEpoch < previousEpoch!.Value))
                {
                    throw new InvalidDataException(
                        "Protocol transcript time or epoch order regressed.");
                }

                previousSimTime = currentSimTime;
                previousEpoch = currentEpoch;
            }

            values.Add(decoded.Envelope);
        }

        return values;
    }

    private static IReadOnlyList<byte[]> ReadCanonicalLines(byte[] bytes)
    {
        if (bytes.Length == 0 || bytes[^1] != (byte)'\n')
        {
            throw new InvalidDataException("Canonical NDJSON evidence is empty or incomplete.");
        }

        var values = new List<byte[]>();
        var offset = 0;

        while (offset < bytes.Length)
        {
            var length = bytes.AsSpan(offset).IndexOf((byte)'\n');

            if (length <= 0)
            {
                throw new InvalidDataException("Canonical NDJSON contains an empty frame.");
            }

            var line = bytes.AsSpan(offset, length).ToArray();
            offset += length + 1;
            var canonical = CanonicalJson.Canonicalize(line);

            if (!line.SequenceEqual(canonical))
            {
                throw new InvalidDataException("NDJSON row is not canonical JSON.");
            }

            values.Add(line);
        }

        return values;
    }

    private static MetricWindowId PhaseWindow(long simTime, ScenarioTimeWindow window)
    {
        if (simTime < window.WarmupStartMs || simTime > window.DrainEndMs)
        {
            throw new InvalidDataException("Metric event time lies outside the declared run window.");
        }

        if (simTime < window.ScoreStartMs)
        {
            return MetricWindowId.Warmup;
        }

        return simTime <= window.HorizonEndMs
            ? MetricWindowId.Scoring
            : MetricWindowId.Drain;
    }

    private static bool IsInWindow(
        long simTime,
        MetricWindowId window,
        ScenarioTimeWindow bounds) =>
        window switch
        {
            MetricWindowId.All =>
                simTime >= bounds.WarmupStartMs && simTime <= bounds.DrainEndMs,
            MetricWindowId.Warmup =>
                simTime >= bounds.WarmupStartMs && simTime < bounds.ScoreStartMs,
            MetricWindowId.Scoring =>
                simTime >= bounds.ScoreStartMs && simTime <= bounds.HorizonEndMs,
            MetricWindowId.Drain =>
                simTime > bounds.HorizonEndMs && simTime <= bounds.DrainEndMs,
            _ => false,
        };

    private static void ValidateWindow(ScenarioTimeWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (window.WarmupStartMs < 0
            || window.WarmupStartMs > window.ScoreStartMs
            || window.ScoreStartMs > window.HorizonEndMs
            || window.HorizonEndMs > window.DrainEndMs)
        {
            throw new InvalidDataException("Scenario metric windows are not ordered.");
        }
    }

    private static void RequireEvidence(RunFileEvidence evidence, byte[] bytes)
    {
        if (evidence.LengthBytes != bytes.LongLength
            || evidence.Sha256 != Convert.ToHexStringLower(SHA256.HashData(bytes)))
        {
            throw new InvalidDataException("Metric raw evidence differs from terminal identity.");
        }
    }

    private static long Nonnegative(JsonElement root, string property)
    {
        var value = root.GetProperty(property);

        if (value.ValueKind != JsonValueKind.Number
            || !value.TryGetInt64(out var number)
            || number < 0
            || number > SafeIntegerMaximum)
        {
            throw new InvalidDataException("Resource value is outside canonical range.");
        }

        return number;
    }

    private static bool TryDelta(string metricId, out string? dimension, out string? aggregate)
    {
        dimension = null;
        aggregate = null;

        if (!metricId.StartsWith("decisionDelta.", StringComparison.Ordinal))
        {
            return false;
        }

        var remainder = metricId["decisionDelta.".Length..];
        var split = remainder.LastIndexOf('.');

        if (split <= 0)
        {
            return false;
        }

        var parsedDimension = remainder[..split];
        var parsedAggregate = remainder[(split + 1)..];
        dimension = parsedDimension;
        aggregate = parsedAggregate;
        return Dimensions.Any(value => value.Name == parsedDimension)
            && aggregate is "sum" or "max";
    }

    private static long PartsPerMillion(long numerator, long denominator)
    {
        var scaled = new BigInteger(numerator) * 1_000_000;
        var quotient = BigInteger.DivRem(scaled, denominator, out var remainder);
        var comparison = (remainder * 2).CompareTo(denominator);

        if (comparison > 0 || comparison == 0 && !quotient.IsEven)
        {
            quotient++;
        }

        return ToSafeLong(quotient);
    }

    private static long ToSafeLong(BigInteger value)
    {
        if (value < 0 || value > SafeIntegerMaximum)
        {
            throw new OverflowException();
        }

        return (long)value;
    }

    private static byte[] EncodeRows(IReadOnlyList<MetricRow> rows)
    {
        using var stream = new MemoryStream();

        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();

            foreach (var row in rows)
            {
                writer.WriteRawValue(BenchmarkContractCodec.Encode(row), skipInputValidation: true);
            }

            writer.WriteEndArray();
        }

        return CanonicalJson.Canonicalize(stream.ToArray());
    }

    private static string WindowWire(MetricWindowId value) => value switch
    {
        MetricWindowId.All => "all",
        MetricWindowId.Warmup => "warmup",
        MetricWindowId.Scoring => "scoring",
        MetricWindowId.Drain => "drain",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static string ScopeWire(MetricScopeKind value) => value switch
    {
        MetricScopeKind.Run => "run",
        MetricScopeKind.Epoch => "epoch",
        MetricScopeKind.Request => "request",
        MetricScopeKind.Vehicle => "vehicle",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static void RequireHash(string value, string parameter)
    {
        if (value.Length != 64
            || value.Any(character => character is not (>= '0' and <= '9')
                and not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException("Value must be exact lowercase SHA-256.", parameter);
        }
    }

    private sealed record VectorDimension(string Name, string UnitId);

    private sealed record ResourceValues(
        long WallTimeMs,
        long CpuTimeMs,
        long PeakWorkingSetBytes,
        long ProcessCount);

    private sealed class SemanticState
    {
        private readonly Dictionary<MetricWindowId, WindowAccumulator> windows =
            Windows.ToDictionary(value => value, _ => new WindowAccumulator());

        public Dictionary<string, long> ArrivalTimes { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, long> Acceptances { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, long> Rejections { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, long> Completions { get; } = new(StringComparer.Ordinal);

        public List<TimedRequest> DeferredActions { get; } = [];

        public WindowAccumulator For(MetricWindowId window) => windows[window];
    }

    private sealed record TimedRequest(string RequestId, long SimTimeMs);

    private sealed class WindowAccumulator
    {
        public long DecisionCount { get; set; }

        public long PromisePublicationCount { get; set; }

        public long PromiseRevisionCount { get; set; }

        public long BreachCount { get; set; }

        public long NonNormalCertificateCount { get; set; }

        public Dictionary<string, BigInteger> DeltaSums { get; } =
            new(StringComparer.Ordinal);

        public Dictionary<string, BigInteger> DeltaMaximums { get; } =
            new(StringComparer.Ordinal);

        public void AddDelta(string dimension, BigInteger value)
        {
            DeltaSums[dimension] = DeltaSums.GetValueOrDefault(dimension) + value;

            if (!DeltaMaximums.TryGetValue(dimension, out var current) || value > current)
            {
                DeltaMaximums[dimension] = value;
            }
        }
    }
}
