using System.Numerics;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace RideBound.Wp6MetricOracle;

internal sealed record OracleRequest(
    string RegistryPath,
    string RunRecordPath,
    string InputTranscriptPath,
    string OutputTranscriptPath,
    string ObservationIndexPath,
    string ResourceSamplesPath,
    long WarmupStartMs,
    long ScoreStartMs,
    long HorizonEndMs,
    long DrainEndMs,
    string CalculatorSourceSha256)
{
    public static OracleRequest Load(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        var root = document.RootElement;
        return new OracleRequest(
            FullPath(root, "registryPath"),
            FullPath(root, "runRecordPath"),
            FullPath(root, "inputTranscriptPath"),
            FullPath(root, "outputTranscriptPath"),
            FullPath(root, "observationIndexPath"),
            FullPath(root, "resourceSamplesPath"),
            Integer(root, "warmupStartMs"),
            Integer(root, "scoreStartMs"),
            Integer(root, "horizonEndMs"),
            Integer(root, "drainEndMs"),
            Text(root, "calculatorSourceSha256"));
    }

    private static string FullPath(JsonElement root, string name)
    {
        var path = Path.GetFullPath(Text(root, name));
        return File.Exists(path)
            ? path
            : throw new OracleException("oracle.input-invalid", "Oracle evidence file is missing.");
    }

    private static string Text(JsonElement root, string name) =>
        root.GetProperty(name).ValueKind == JsonValueKind.String
            ? root.GetProperty(name).GetString()!
            : throw new OracleException("oracle.input-invalid", "Oracle request field is invalid.");

    private static long Integer(JsonElement root, string name) =>
        root.GetProperty(name).TryGetInt64(out var value)
            ? value
            : throw new OracleException("oracle.input-invalid", "Oracle request integer is invalid.");
}

internal sealed record OracleResult(
    byte[] CanonicalRows,
    string MetricSetHash,
    string SemanticEvidenceSha256,
    string ResourceEvidenceSha256,
    int RowCount)
{
    public byte[] Summary()
    {
        var assembly = File.ReadAllBytes(Assembly.GetExecutingAssembly().Location);
        return OracleJson.EncodeCanonical(writer =>
        {
            writer.Add("metricSetHash", MetricSetHash);
            writer.Add("oracleAssemblySha256", OracleIdentity.File(assembly));
            writer.Add("resourceEvidenceSha256", ResourceEvidenceSha256);
            writer.Add("rowCount", RowCount);
            writer.Add("schemaVersion", "1.0.0");
            writer.Add("semanticEvidenceSha256", SemanticEvidenceSha256);
        });
    }
}

internal static class OracleCalculator
{
    private const string RegistryHash =
        "0747499608638ab085e1a89dfb6edf3d1ebff7d4bf267bd880ad1b6ccaa2f1a5";
    private static readonly string[] Windows = ["all", "warmup", "scoring", "drain"];
    private static readonly string[] Dimensions =
    [
        "dropEtaTotalMs",
        "dropStopRelocationMm",
        "dropStopSwitchCount",
        "incumbentOrderInversionCount",
        "materialEtaRevisionCount",
        "pickupEtaTotalMs",
        "pickupStopRelocationMm",
        "pickupStopSwitchCount",
        "prePickupInsertedStopCount",
        "vehicleSwitchCount",
    ];

    public static OracleResult Calculate(OracleRequest request)
    {
        ValidateWindow(request);
        RequireHash(request.CalculatorSourceSha256);
        var registryBytes = File.ReadAllBytes(request.RegistryPath);
        var definitions = ReadRegistry(registryBytes);
        var runRecordBytes = File.ReadAllBytes(request.RunRecordPath);
        var run = ReadRunRecord(runRecordBytes);
        var input = File.ReadAllBytes(request.InputTranscriptPath);
        var output = File.ReadAllBytes(request.OutputTranscriptPath);
        var index = File.ReadAllBytes(request.ObservationIndexPath);
        var resources = File.ReadAllBytes(request.ResourceSamplesPath);
        VerifyEvidence(run.InputFile, input);
        VerifyEvidence(run.OutputFile, output);
        VerifyEvidence(run.ObservationIndexFile, index);
        VerifyEvidence(run.ResourceSamplesFile, resources);
        ValidateObservationIndex(index, run.RunId);
        var semanticHash = OracleIdentity.SemanticEvidence(input, output, index);
        var resourceHash = OracleIdentity.ResourceEvidence(runRecordBytes, resources);
        var state = ReadSemanticEvidence(request, run.RunId, input, output);
        var resourceValues = ReadResources(resources, run);
        var rows = new List<OracleMetricRow>(132);

        foreach (var definition in definitions.Where(value => value.SourceKind == "rawTranscript"))
        {
            foreach (var window in Windows)
            {
                rows.Add(SemanticRow(request, run, definition, window, state, semanticHash));
            }
        }

        foreach (var definition in definitions.Where(value => value.SourceKind == "rawSupervisor"))
        {
            var value = definition.MetricId switch
            {
                "resource.wall-time-ms" => resourceValues.WallTimeMs,
                "resource.cpu-time-ms" => resourceValues.CpuTimeMs,
                "resource.peak-working-set-bytes" => resourceValues.PeakWorkingSetBytes,
                "resource.process-count-max" => resourceValues.ProcessCount,
                _ => throw Invalid("Metric registry has an unknown resource metric."),
            };
            rows.Add(Observed(run, request, definition, "all", value, resourceHash));
        }

        var ordered = rows.OrderBy(value => value.RunId, StringComparer.Ordinal)
            .ThenBy(value => value.MetricId, StringComparer.Ordinal)
            .ThenBy(value => value.ScopeKind, StringComparer.Ordinal)
            .ThenBy(value => value.ScopeId, StringComparer.Ordinal)
            .ThenBy(value => value.WindowId, StringComparer.Ordinal)
            .ToArray();
        var canonicalRows = EncodeRows(ordered);
        return new OracleResult(
            canonicalRows,
            OracleIdentity.MetricSet(run.RunId, RegistryHash, canonicalRows),
            semanticHash,
            resourceHash,
            ordered.Length);
    }

    private static IReadOnlyList<OracleDefinition> ReadRegistry(byte[] bytes)
    {
        if (bytes.Length < 2 || bytes[^1] != (byte)'\n')
        {
            throw Invalid("Metric registry is not LF terminated.");
        }

        var payload = bytes.AsSpan(0, bytes.Length - 1).ToArray();
        using var document = OracleJson.ParseCanonical(payload);

        if (OracleIdentity.File(bytes) != RegistryHash)
        {
            throw Invalid("Metric registry identity is not the immutable v1 identity.");
        }

        var root = document.RootElement;

        if (Text(root, "schemaVersion") != "1.0.0"
            || Text(root, "registryId") != "wp6-mechanical-v1"
            || Text(root, "registryVersion") != "1.0.0")
        {
            throw Invalid("Metric registry header is invalid.");
        }

        var definitions = new List<OracleDefinition>();

        foreach (var item in root.GetProperty("definitions").EnumerateArray())
        {
            definitions.Add(
                new OracleDefinition(
                    Text(item, "metricId"),
                    Text(item, "metricVersion"),
                    Text(item, "unitId"),
                    Text(item, "valueKind"),
                    Text(item, "sourceKind"),
                    Text(item, "windowScope"),
                    item.GetProperty("rawOracleRequired").GetBoolean(),
                    item.TryGetProperty("denominatorId", out var denominator)
                        ? denominator.GetString()
                        : null));
        }

        if (definitions.Count != 36
            || definitions.Select(value => value.MetricId)
                .Distinct(StringComparer.Ordinal).Count() != definitions.Count
            || !definitions.SequenceEqual(
                definitions.OrderBy(value => value.MetricId, StringComparer.Ordinal)))
        {
            throw Invalid("Metric registry count or order is invalid.");
        }

        return definitions;
    }

    private static OracleRun ReadRunRecord(byte[] bytes)
    {
        using var document = OracleJson.ParseCanonical(bytes);
        var root = document.RootElement;

        if (Text(root, "schemaVersion") != "1.0.0"
            || Text(root, "terminalStatus") != "succeeded"
            || Integer(root, "exitCode") != 0
            || root.TryGetProperty("failureRecordId", out _)
            || root.TryGetProperty("exclusionRecordId", out _))
        {
            throw new OracleException(
                "oracle.terminal-not-succeeded",
                "Only a succeeded terminal run can produce oracle metrics.");
        }

        return new OracleRun(
            Text(root, "runId"),
            Text(root, "scenarioHash"),
            Text(root, "armId"),
            Nonnegative(root, "repeatIndex"),
            Nonnegative(root, "attemptIndex"),
            Nonnegative(root, "wallTimeMs"),
            Nonnegative(root, "cpuTimeMs"),
            Nonnegative(root, "peakWorkingSetBytes"),
            Nonnegative(root, "spawnedProcessCount"),
            Evidence(root, "inputFile"),
            Evidence(root, "outputFile"),
            Evidence(root, "observationIndexFile"),
            Evidence(root, "resourceSamplesFile"));
    }

    private static OracleEvidence Evidence(JsonElement root, string name)
    {
        var value = root.GetProperty(name);
        return new OracleEvidence(
            Nonnegative(value, "lengthBytes"),
            Text(value, "sha256"));
    }

    private static void VerifyEvidence(OracleEvidence evidence, byte[] bytes)
    {
        if (evidence.LengthBytes != bytes.LongLength
            || evidence.Sha256 != OracleIdentity.File(bytes))
        {
            throw Invalid("Raw evidence differs from the terminal record.");
        }
    }

    private static void ValidateObservationIndex(byte[] bytes, string runId)
    {
        foreach (var line in OracleJson.ReadCanonicalLines(bytes, allowEmpty: true))
        {
            using var document = JsonDocument.Parse(line);

            if (Text(document.RootElement, "runId") != runId)
            {
                throw Invalid("Observation index is cross-linked.");
            }
        }
    }

    private static SemanticState ReadSemanticEvidence(
        OracleRequest request,
        string runId,
        byte[] input,
        byte[] output)
    {
        var state = new SemanticState();
        long? previousInputTime = null;
        long? previousInputEpoch = null;

        foreach (var line in OracleJson.ReadCanonicalLines(input, allowEmpty: false))
        {
            using var document = JsonDocument.Parse(line);
            var envelope = document.RootElement;
            ValidateRunLink(envelope, runId);

            if (Text(envelope, "messageType") != "eventBatch")
            {
                ValidateOrder(envelope, ref previousInputTime, ref previousInputEpoch);
                continue;
            }

            var time = Nonnegative(envelope, "simTimeMs");
            ValidateOrder(envelope, ref previousInputTime, ref previousInputEpoch);
            _ = Phase(time, request);

            foreach (var item in envelope.GetProperty("payload")
                         .GetProperty("events").EnumerateArray())
            {
                var eventType = Text(item, "eventType");
                var payload = item.GetProperty("payload");

                if (eventType == "requestArrived")
                {
                    var requestId = Text(payload.GetProperty("request"), "requestId");

                    if (!state.Arrivals.TryAdd(requestId, time))
                    {
                        throw Invalid("Request arrival is duplicated.");
                    }
                }
                else if (eventType == "passengerAlighted")
                {
                    if (!state.Completions.TryAdd(Text(payload, "requestId"), time))
                    {
                        throw Invalid("Request completion is duplicated.");
                    }
                }
            }
        }

        long? previousOutputTime = null;
        long? previousOutputEpoch = null;

        foreach (var line in OracleJson.ReadCanonicalLines(output, allowEmpty: false))
        {
            using var document = JsonDocument.Parse(line);
            var envelope = document.RootElement;
            ValidateRunLink(envelope, runId);

            if (Text(envelope, "messageType") != "decision")
            {
                ValidateOrder(envelope, ref previousOutputTime, ref previousOutputEpoch);
                continue;
            }

            var time = Nonnegative(envelope, "simTimeMs");
            ValidateOrder(envelope, ref previousOutputTime, ref previousOutputEpoch);
            var phase = Phase(time, request);
            state.For("all").DecisionCount++;
            state.For(phase).DecisionCount++;
            var payload = envelope.GetProperty("payload");
            var certificate = payload.GetProperty("certificate");

            if (certificate.TryGetProperty("body", out var body)
                && body.GetProperty("normalOperation").ValueKind == JsonValueKind.False)
            {
                state.For("all").NonNormalCertificateCount++;
                state.For(phase).NonNormalCertificateCount++;
            }

            foreach (var action in payload.GetProperty("actions").EnumerateArray())
            {
                ReadAction(action, time, phase, state);
            }
        }

        ValidateOutcomes(state);
        return state;
    }

    private static void ReadAction(
        JsonElement action,
        long time,
        string phase,
        SemanticState state)
    {
        var type = Text(action, "decisionType");
        var payload = action.GetProperty("payload");

        switch (type)
        {
            case "requestAccepted":
                if (!state.Acceptances.TryAdd(Text(payload, "requestId"), time))
                {
                    throw Invalid("Request acceptance is duplicated.");
                }

                break;
            case "requestRejected":
                if (!state.Rejections.TryAdd(Text(payload, "requestId"), time))
                {
                    throw Invalid("Request rejection is duplicated.");
                }

                break;
            case "requestDeferred":
                state.Deferred.Add(new TimedRequest(Text(payload, "requestId"), time));
                break;
            case "promisePublished":
                AddPromise(payload, state.For("all"));
                AddPromise(payload, state.For(phase));
                break;
            case "commitmentBreachDeclared":
                state.For("all").BreachCount++;
                state.For(phase).BreachCount++;
                break;
        }
    }

    private static void AddPromise(JsonElement payload, Accumulator accumulator)
    {
        accumulator.PromisePublicationCount++;

        if (Nonnegative(payload, "promiseVersion") > 1)
        {
            accumulator.PromiseRevisionCount++;
        }

        var delta = payload.GetProperty("decisionDelta");

        foreach (var dimension in Dimensions)
        {
            accumulator.Add(dimension, new BigInteger(Nonnegative(delta, dimension)));
        }
    }

    private static void ValidateOutcomes(SemanticState state)
    {
        if (state.Acceptances.Keys.Intersect(
                state.Rejections.Keys,
                StringComparer.Ordinal).Any())
        {
            throw Invalid("Request has conflicting terminal outcomes.");
        }

        foreach (var (requestId, acceptedAt) in state.Acceptances)
        {
            if (!state.Arrivals.TryGetValue(requestId, out var arrivedAt)
                || acceptedAt < arrivedAt)
            {
                throw Invalid("Accepted request has no preceding arrival.");
            }
        }

        foreach (var (requestId, rejectedAt) in state.Rejections)
        {
            if (!state.Arrivals.TryGetValue(requestId, out var arrivedAt)
                || rejectedAt < arrivedAt)
            {
                throw Invalid("Rejected request has no preceding arrival.");
            }
        }

        foreach (var (requestId, completedAt) in state.Completions)
        {
            if (!state.Arrivals.ContainsKey(requestId))
            {
                continue;
            }

            if (!state.Acceptances.TryGetValue(requestId, out var acceptedAt)
                || completedAt < acceptedAt)
            {
                throw Invalid("Known completion has no preceding acceptance.");
            }
        }

        foreach (var action in state.Deferred)
        {
            if (!state.Arrivals.TryGetValue(action.RequestId, out var arrivedAt)
                || action.SimTimeMs < arrivedAt)
            {
                throw Invalid("Deferred request has no preceding arrival.");
            }

            var terminalAt = state.Acceptances.GetValueOrDefault(
                action.RequestId,
                state.Rejections.GetValueOrDefault(action.RequestId, long.MaxValue));

            if (action.SimTimeMs > terminalAt)
            {
                throw Invalid("Request was deferred after a terminal outcome.");
            }
        }
    }

    private static OracleMetricRow SemanticRow(
        OracleRequest request,
        OracleRun run,
        OracleDefinition definition,
        string window,
        SemanticState state,
        string evidenceHash)
    {
        var accumulator = state.For(window);
        var cohort = state.Arrivals.Where(value => InWindow(value.Value, window, request))
            .Select(value => value.Key).ToHashSet(StringComparer.Ordinal);
        var arrived = cohort.Count;
        var accepted = cohort.Count(state.Acceptances.ContainsKey);
        var rejected = cohort.Count(value =>
            state.Rejections.ContainsKey(value) && !state.Acceptances.ContainsKey(value));
        var completed = cohort.Count(value =>
            state.Completions.ContainsKey(value) && state.Acceptances.ContainsKey(value));
        var deferred = state.Deferred.Count(value => cohort.Contains(value.RequestId));

        return definition.MetricId switch
        {
            "certificate.non-normal.count" => Observed(
                run, request, definition, window, accumulator.NonNormalCertificateCount, evidenceHash),
            "commitment.breach.count" => Observed(
                run, request, definition, window, accumulator.BreachCount, evidenceHash),
            "decision.epoch.count" => Observed(
                run, request, definition, window, accumulator.DecisionCount, evidenceHash),
            "promise.publication.count" => Observed(
                run, request, definition, window, accumulator.PromisePublicationCount, evidenceHash),
            "promise.revision.count" => Observed(
                run, request, definition, window, accumulator.PromiseRevisionCount, evidenceHash),
            "request.arrived.count" => Observed(
                run, request, definition, window, arrived, evidenceHash),
            "request.accepted.count" => Observed(
                run, request, definition, window, accepted, evidenceHash),
            "request.rejected.count" => Observed(
                run, request, definition, window, rejected, evidenceHash),
            "request.deferred.action.count" => Observed(
                run, request, definition, window, deferred, evidenceHash),
            "request.completed.count" => Observed(
                run, request, definition, window, completed, evidenceHash),
            "request.acceptance.ppm" => Ratio(
                run, request, definition, window, accepted, arrived, evidenceHash),
            "request.completion.ppm" => Ratio(
                run, request, definition, window, completed, accepted, evidenceHash),
            _ => Delta(run, request, definition, window, accumulator, evidenceHash),
        };
    }

    private static OracleMetricRow Delta(
        OracleRun run,
        OracleRequest request,
        OracleDefinition definition,
        string window,
        Accumulator accumulator,
        string evidenceHash)
    {
        const string prefix = "decisionDelta.";
        var suffix = definition.MetricId.StartsWith(prefix, StringComparison.Ordinal)
            ? definition.MetricId[prefix.Length..]
            : throw Invalid("Metric registry has an unknown semantic metric.");
        var split = suffix.LastIndexOf('.');
        var dimension = split > 0 ? suffix[..split] : string.Empty;
        var aggregate = split > 0 ? suffix[(split + 1)..] : string.Empty;

        if (!Dimensions.Contains(dimension, StringComparer.Ordinal))
        {
            throw Invalid("Metric registry has an unknown vector dimension.");
        }

        if (aggregate == "sum")
        {
            return Observed(
                run,
                request,
                definition,
                window,
                Safe(accumulator.Sums.GetValueOrDefault(dimension)),
                evidenceHash);
        }

        if (aggregate != "max")
        {
            throw Invalid("Metric registry has an unknown vector aggregate.");
        }

        return accumulator.Maximums.TryGetValue(dimension, out var maximum)
            ? Observed(run, request, definition, window, Safe(maximum), evidenceHash)
            : Missing(run, request, definition, window, "no-publication", evidenceHash);
    }

    private static OracleMetricRow Ratio(
        OracleRun run,
        OracleRequest request,
        OracleDefinition definition,
        string window,
        long numerator,
        long denominator,
        string evidenceHash) =>
        denominator == 0
            ? Missing(
                run,
                request,
                definition,
                window,
                "denominator-zero",
                evidenceHash,
                numerator,
                denominator)
            : Observed(
                run,
                request,
                definition,
                window,
                PartsPerMillion(numerator, denominator),
                evidenceHash,
                numerator,
                denominator);

    private static OracleMetricRow Observed(
        OracleRun run,
        OracleRequest request,
        OracleDefinition definition,
        string window,
        long value,
        string evidenceHash,
        long? numerator = null,
        long? denominator = null) =>
        new(
            RegistryHash,
            definition.MetricId,
            run.RunId,
            run.ScenarioHash,
            run.ArmId,
            run.RepeatIndex,
            run.AttemptIndex,
            window,
            "observed",
            definition.UnitId,
            evidenceHash,
            request.CalculatorSourceSha256,
            value,
            numerator,
            denominator is null ? null : definition.DenominatorId,
            denominator,
            null);

    private static OracleMetricRow Missing(
        OracleRun run,
        OracleRequest request,
        OracleDefinition definition,
        string window,
        string reason,
        string evidenceHash,
        long? numerator = null,
        long? denominator = null) =>
        new(
            RegistryHash,
            definition.MetricId,
            run.RunId,
            run.ScenarioHash,
            run.ArmId,
            run.RepeatIndex,
            run.AttemptIndex,
            window,
            "missing",
            definition.UnitId,
            evidenceHash,
            request.CalculatorSourceSha256,
            null,
            numerator,
            denominator is null ? null : definition.DenominatorId,
            denominator,
            reason);

    private static ResourceValues ReadResources(byte[] bytes, OracleRun run)
    {
        long elapsed = -1;
        long cpu = 0;
        long workingSet = 0;
        long processCount = 0;
        var rows = 0;

        foreach (var line in OracleJson.ReadCanonicalLines(bytes, allowEmpty: false))
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            var names = root.EnumerateObject().Select(value => value.Name)
                .ToHashSet(StringComparer.Ordinal);

            if (!names.SetEquals(
                ["elapsedMs", "observedCpuTimeMs", "observedWorkingSetBytes", "observedProcessCount"]))
            {
                throw Invalid("Resource sample fields are invalid.");
            }

            var current = Nonnegative(root, "elapsedMs");

            if (current < elapsed)
            {
                throw Invalid("Resource sample time regressed.");
            }

            elapsed = current;
            cpu = Math.Max(cpu, Nonnegative(root, "observedCpuTimeMs"));
            workingSet = Math.Max(workingSet, Nonnegative(root, "observedWorkingSetBytes"));
            processCount = Math.Max(processCount, Nonnegative(root, "observedProcessCount"));
            rows++;
        }

        if (rows == 0
            || elapsed != run.WallTimeMs
            || cpu != run.CpuTimeMs
            || workingSet != run.PeakWorkingSetBytes
            || processCount != run.SpawnedProcessCount)
        {
            throw Invalid("Terminal resources differ from raw samples.");
        }

        return new ResourceValues(elapsed, cpu, workingSet, processCount);
    }

    private static byte[] EncodeRows(IReadOnlyList<OracleMetricRow> rows)
    {
        using var stream = new MemoryStream();
        stream.WriteByte((byte)'[');

        for (var index = 0; index < rows.Count; index++)
        {
            if (index != 0)
            {
                stream.WriteByte((byte)',');
            }

            stream.Write(rows[index].Encode());
        }

        stream.WriteByte((byte)']');
        return stream.ToArray();
    }

    private static void ValidateRunLink(JsonElement envelope, string runId)
    {
        if (envelope.TryGetProperty("runId", out var linked)
            && linked.GetString() != runId)
        {
            throw Invalid("Protocol transcript is cross-linked.");
        }
    }

    private static void ValidateOrder(
        JsonElement envelope,
        ref long? previousTime,
        ref long? previousEpoch)
    {
        if (!envelope.TryGetProperty("simTimeMs", out var timeProperty))
        {
            return;
        }

        var time = timeProperty.GetInt64();
        var epoch = Nonnegative(envelope, "epochId");

        if (previousTime is not null
            && (time < previousTime.Value || epoch < previousEpoch!.Value))
        {
            throw Invalid("Protocol transcript time or epoch order regressed.");
        }

        previousTime = time;
        previousEpoch = epoch;
    }

    private static string Phase(long time, OracleRequest request)
    {
        if (time < request.WarmupStartMs || time > request.DrainEndMs)
        {
            throw Invalid("Metric event lies outside the declared run window.");
        }

        return time < request.ScoreStartMs
            ? "warmup"
            : time <= request.HorizonEndMs ? "scoring" : "drain";
    }

    private static bool InWindow(long time, string window, OracleRequest request) =>
        window switch
        {
            "all" => time >= request.WarmupStartMs && time <= request.DrainEndMs,
            "warmup" => time >= request.WarmupStartMs && time < request.ScoreStartMs,
            "scoring" => time >= request.ScoreStartMs && time <= request.HorizonEndMs,
            "drain" => time > request.HorizonEndMs && time <= request.DrainEndMs,
            _ => false,
        };

    private static void ValidateWindow(OracleRequest request)
    {
        if (request.WarmupStartMs < 0
            || request.WarmupStartMs > request.ScoreStartMs
            || request.ScoreStartMs > request.HorizonEndMs
            || request.HorizonEndMs > request.DrainEndMs)
        {
            throw Invalid("Metric windows are not ordered.");
        }
    }

    private static long PartsPerMillion(long numerator, long denominator)
    {
        var quotient = BigInteger.DivRem(
            new BigInteger(numerator) * 1_000_000,
            denominator,
            out var remainder);
        var comparison = (remainder * 2).CompareTo(denominator);

        if (comparison > 0 || comparison == 0 && !quotient.IsEven)
        {
            quotient++;
        }

        return Safe(quotient);
    }

    private static long Safe(BigInteger value)
    {
        if (value < BigInteger.Zero || value > OracleJson.SafeIntegerMaximum)
        {
            throw new OracleException(
                "oracle.overflow",
                "Metric exceeded the safe integer range.");
        }

        return (long)value;
    }

    private static string Text(JsonElement root, string name)
    {
        var value = root.GetProperty(name);
        return value.ValueKind == JsonValueKind.String
            ? value.GetString()!
            : throw Invalid("Expected a string field.");
    }

    private static long Integer(JsonElement root, string name)
    {
        var value = root.GetProperty(name);
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var result)
            ? result
            : throw Invalid("Expected an integer field.");
    }

    private static long Nonnegative(JsonElement root, string name)
    {
        var value = Integer(root, name);
        return value is >= 0 and <= OracleJson.SafeIntegerMaximum
            ? value
            : throw Invalid("Expected a nonnegative safe integer.");
    }

    private static void RequireHash(string value)
    {
        if (value.Length != 64
            || value.Any(character => character is not (>= '0' and <= '9')
                and not (>= 'a' and <= 'f')))
        {
            throw Invalid("Expected lowercase SHA-256.");
        }
    }

    private static OracleException Invalid(string message) =>
        new("oracle.input-invalid", message);

    private sealed record OracleDefinition(
        string MetricId,
        string MetricVersion,
        string UnitId,
        string ValueKind,
        string SourceKind,
        string WindowScope,
        bool RawOracleRequired,
        string? DenominatorId);

    private sealed record OracleEvidence(long LengthBytes, string Sha256);

    private sealed record OracleRun(
        string RunId,
        string ScenarioHash,
        string ArmId,
        long RepeatIndex,
        long AttemptIndex,
        long WallTimeMs,
        long CpuTimeMs,
        long PeakWorkingSetBytes,
        long SpawnedProcessCount,
        OracleEvidence InputFile,
        OracleEvidence OutputFile,
        OracleEvidence ObservationIndexFile,
        OracleEvidence ResourceSamplesFile);

    private sealed record ResourceValues(
        long WallTimeMs,
        long CpuTimeMs,
        long PeakWorkingSetBytes,
        long ProcessCount);

    private sealed class SemanticState
    {
        private readonly Dictionary<string, Accumulator> accumulators =
            Windows.ToDictionary(value => value, _ => new Accumulator(), StringComparer.Ordinal);

        public Dictionary<string, long> Arrivals { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, long> Acceptances { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, long> Rejections { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, long> Completions { get; } = new(StringComparer.Ordinal);

        public List<TimedRequest> Deferred { get; } = [];

        public Accumulator For(string window) => accumulators[window];
    }

    private sealed record TimedRequest(string RequestId, long SimTimeMs);

    private sealed class Accumulator
    {
        public long DecisionCount { get; set; }

        public long PromisePublicationCount { get; set; }

        public long PromiseRevisionCount { get; set; }

        public long BreachCount { get; set; }

        public long NonNormalCertificateCount { get; set; }

        public Dictionary<string, BigInteger> Sums { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, BigInteger> Maximums { get; } = new(StringComparer.Ordinal);

        public void Add(string dimension, BigInteger value)
        {
            Sums[dimension] = Sums.GetValueOrDefault(dimension) + value;

            if (!Maximums.TryGetValue(dimension, out var current) || value > current)
            {
                Maximums[dimension] = value;
            }
        }
    }

    private sealed record OracleMetricRow(
        string MetricRegistryHash,
        string MetricId,
        string RunId,
        string ScenarioHash,
        string ArmId,
        long RepeatIndex,
        long AttemptIndex,
        string WindowId,
        string ValueStatus,
        string UnitId,
        string RawEvidenceSha256,
        string CalculatorSourceSha256,
        long? ValueInteger,
        long? NumeratorInteger,
        string? DenominatorId,
        long? DenominatorInteger,
        string? MissingReasonId)
    {
        public string ScopeKind => "run";

        public string ScopeId => RunId;

        public byte[] Encode() => OracleJson.EncodeCanonical(writer =>
        {
            writer.Add("armId", ArmId);
            writer.Add("attemptIndex", AttemptIndex);
            writer.Add("calculatorSourceSha256", CalculatorSourceSha256);

            if (DenominatorId is not null)
            {
                writer.Add("denominatorId", DenominatorId);
            }

            if (DenominatorInteger is not null)
            {
                writer.Add("denominatorInteger", DenominatorInteger.Value);
            }

            writer.Add("metricId", MetricId);
            writer.Add("metricRegistryHash", MetricRegistryHash);
            writer.Add("metricVersion", "1.0.0");

            if (MissingReasonId is not null)
            {
                writer.Add("missingReasonId", MissingReasonId);
            }

            if (NumeratorInteger is not null)
            {
                writer.Add("numeratorInteger", NumeratorInteger.Value);
            }

            writer.Add("rawEvidenceSha256", RawEvidenceSha256);
            writer.Add("repeatIndex", RepeatIndex);
            writer.Add("runId", RunId);
            writer.Add("scenarioHash", ScenarioHash);
            writer.Add("schemaVersion", "1.0.0");
            writer.Add("scopeId", ScopeId);
            writer.Add("scopeKind", ScopeKind);
            writer.Add("unitId", UnitId);

            if (ValueInteger is not null)
            {
                writer.Add("valueInteger", ValueInteger.Value);
            }

            writer.Add("valueStatus", ValueStatus);
            writer.Add("windowId", WindowId);
        });
    }
}

internal sealed class OracleException(string code, string safeMessage, Exception? inner = null)
    : Exception(safeMessage, inner)
{
    public string Code { get; } = code;
}
