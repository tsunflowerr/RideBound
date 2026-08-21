using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text.Json;

namespace RideBound.Wp6MetricOracle;

internal sealed record OracleBurdenResult(
    string RunId,
    string ArmId,
    string PolicyId,
    long ArrivedRiderCount,
    long AcceptedRiderCount,
    long RejectedRiderCount,
    long CompletedRiderCount,
    IReadOnlyDictionary<string, long> VectorTotals,
    long TotalDecisionInducedBurdenMs,
    long DisruptiveRevisionFrameCount)
{
    public byte[] Encode() => OracleJson.EncodeCanonical(
        writer =>
        {
            writer.Add("schemaVersion", "1.0.0");
            writer.Add("runId", RunId);
            writer.Add("armId", ArmId);
            writer.Add("policyId", PolicyId);
            writer.Add("arrivedRiderCount", ArrivedRiderCount);
            writer.Add("acceptedRiderCount", AcceptedRiderCount);
            writer.Add("rejectedRiderCount", RejectedRiderCount);
            writer.Add("completedRiderCount", CompletedRiderCount);
            writer.Add(
                "pickupEtaDecisionDeltaSumMs",
                VectorTotals["pickupEtaTotalMs"]);
            writer.Add(
                "dropEtaDecisionDeltaSumMs",
                VectorTotals["dropEtaTotalMs"]);
            writer.Add(
                "materialEtaRevisionCount",
                VectorTotals["materialEtaRevisionCount"]);
            writer.Add("vehicleSwitchCount", VectorTotals["vehicleSwitchCount"]);
            writer.Add(
                "pickupStopRelocationMm",
                VectorTotals["pickupStopRelocationMm"]);
            writer.Add(
                "pickupStopSwitchCount",
                VectorTotals["pickupStopSwitchCount"]);
            writer.Add(
                "dropStopRelocationMm",
                VectorTotals["dropStopRelocationMm"]);
            writer.Add(
                "dropStopSwitchCount",
                VectorTotals["dropStopSwitchCount"]);
            writer.Add(
                "incumbentOrderInversionCount",
                VectorTotals["incumbentOrderInversionCount"]);
            writer.Add(
                "prePickupInsertedStopCount",
                VectorTotals["prePickupInsertedStopCount"]);
            writer.Add(
                "totalDecisionInducedBurdenMs",
                TotalDecisionInducedBurdenMs);
            writer.Add(
                "disruptiveRevisionFrameCount",
                DisruptiveRevisionFrameCount);
        });
}

internal static class DecisionInducedBurdenOracle
{
    private static readonly string[] VectorFields =
    [
        "pickupEtaTotalMs",
        "dropEtaTotalMs",
        "materialEtaRevisionCount",
        "vehicleSwitchCount",
        "pickupStopRelocationMm",
        "pickupStopSwitchCount",
        "dropStopRelocationMm",
        "dropStopSwitchCount",
        "incumbentOrderInversionCount",
        "prePickupInsertedStopCount",
    ];

    public static OracleBurdenResult Calculate(
        string runId,
        string armId,
        string policyId,
        byte[] inputTranscriptBytes,
        byte[] outputTranscriptBytes)
    {
        RequireText(runId);
        RequireText(armId);
        RequireText(policyId);
        var arrivals = new HashSet<string>(StringComparer.Ordinal);
        var bookings = new HashSet<string>(StringComparer.Ordinal);
        var boardings = new HashSet<string>(StringComparer.Ordinal);
        var completions = new HashSet<string>(StringComparer.Ordinal);
        var cancellations = new HashSet<string>(StringComparer.Ordinal);
        var acceptances = new HashSet<string>(StringComparer.Ordinal);
        var rejections = new HashSet<string>(StringComparer.Ordinal);
        var inputContexts = new List<Context>();
        var outputContexts = new List<Context>();
        var totals = VectorFields.ToDictionary(
            field => field,
            _ => BigInteger.Zero,
            StringComparer.Ordinal);
        BigInteger disruptiveFrames = 0;

        foreach (var line in OracleJson.ReadCanonicalLines(
                     inputTranscriptBytes,
                     allowEmpty: false))
        {
            using var document = OracleJson.ParseCanonical(line);
            var root = document.RootElement;
            RequireRun(root, runId);

            if (Text(root, "messageType") != "eventBatch")
            {
                continue;
            }

            inputContexts.Add(ReadContext(root));
            foreach (var item in root.GetProperty("payload")
                         .GetProperty("events")
                         .EnumerateArray())
            {
                var eventType = Text(item, "eventType");
                var payload = item.GetProperty("payload");

                switch (eventType)
                {
                    case "requestArrived":
                        AddUnique(
                            arrivals,
                            Text(payload.GetProperty("request"), "requestId"));
                        break;
                    case "bookingConfirmed":
                        AddUnique(bookings, Text(payload, "requestId"));
                        break;
                    case "passengerBoarded":
                        AddUnique(boardings, Text(payload, "requestId"));
                        break;
                    case "passengerAlighted":
                        AddUnique(completions, Text(payload, "requestId"));
                        break;
                    case "requestCancelledBeforeAcceptance":
                    case "requestCancelledAfterAcceptance":
                        AddUnique(cancellations, Text(payload, "requestId"));
                        break;
                }
            }
        }

        foreach (var line in OracleJson.ReadCanonicalLines(
                     outputTranscriptBytes,
                     allowEmpty: false))
        {
            using var document = OracleJson.ParseCanonical(line);
            var root = document.RootElement;
            RequireRun(root, runId);

            if (Text(root, "messageType") != "decision")
            {
                continue;
            }

            outputContexts.Add(ReadContext(root));
            var frameWasDisruptive = false;
            foreach (var action in root.GetProperty("payload")
                         .GetProperty("actions")
                         .EnumerateArray())
            {
                var type = Text(action, "decisionType");
                var payload = action.GetProperty("payload");

                if (type == "requestAccepted")
                {
                    AddUnique(acceptances, Text(payload, "requestId"));
                }
                else if (type == "requestRejected")
                {
                    AddUnique(rejections, Text(payload, "requestId"));
                }
                else if (type == "promisePublished")
                {
                    var vector = payload.GetProperty("decisionDelta");
                    var names = vector.EnumerateObject()
                        .Select(property => property.Name)
                        .ToHashSet(StringComparer.Ordinal);

                    if (!names.SetEquals(VectorFields))
                    {
                        Invalid("Decision delta does not contain the exact 10D vector.");
                    }

                    var anyNonZero = false;
                    foreach (var field in VectorFields)
                    {
                        if (!vector.GetProperty(field).TryGetInt64(out var value)
                            || value < 0)
                        {
                            Invalid("Decision delta is not a non-negative integer.");
                        }

                        totals[field] += value;
                        anyNonZero |= value != 0;
                    }

                    frameWasDisruptive |= anyNonZero;
                }
            }

            if (frameWasDisruptive)
            {
                disruptiveFrames++;
            }
        }

        if (!inputContexts.SequenceEqual(outputContexts)
            || cancellations.Count != 0
            || acceptances.Overlaps(rejections)
            || !acceptances.Concat(rejections).ToHashSet(StringComparer.Ordinal)
                .SetEquals(arrivals)
            || !bookings.SetEquals(acceptances)
            || !boardings.SetEquals(bookings)
            || !completions.SetEquals(boardings))
        {
            Invalid("Transcript context or terminal lifecycle conservation failed.");
        }

        var canonicalTotals = totals.ToDictionary(
            pair => pair.Key,
            pair => Canonical(pair.Value),
            StringComparer.Ordinal);
        return new OracleBurdenResult(
            runId,
            armId,
            policyId,
            arrivals.Count,
            acceptances.Count,
            rejections.Count,
            completions.Count,
            canonicalTotals,
            Canonical(totals["pickupEtaTotalMs"] + totals["dropEtaTotalMs"]),
            Canonical(disruptiveFrames));
    }

    private static Context ReadContext(JsonElement root) =>
        new(
            Text(root, "runId"),
            Text(root, "scenarioId"),
            Integer(root, "epochId"),
            Integer(root, "simTimeMs"));

    private static void RequireRun(JsonElement root, string runId)
    {
        if (root.TryGetProperty("runId", out var run)
            && (run.ValueKind != JsonValueKind.String
                || !string.Equals(
                    run.GetString(),
                    runId,
                    StringComparison.Ordinal)))
        {
            Invalid("Protocol transcript is cross-linked.");
        }
    }

    private static string Text(JsonElement value, string property)
    {
        if (!value.TryGetProperty(property, out var element)
            || element.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(element.GetString()))
        {
            Invalid("Required transcript text is missing.");
        }

        return element.GetString()!;
    }

    private static long Integer(JsonElement value, string property)
    {
        var result = 0L;
        if (!value.TryGetProperty(property, out var element)
            || !element.TryGetInt64(out result))
        {
            Invalid("Required transcript integer is missing.");
        }

        return result;
    }

    private static void AddUnique(ISet<string> values, string value)
    {
        if (!values.Add(value))
        {
            Invalid("Transcript contains duplicate lifecycle or outcome evidence.");
        }
    }

    private static long Canonical(BigInteger value)
    {
        if (value < BigInteger.Zero
            || value > new BigInteger(OracleJson.SafeIntegerMaximum))
        {
            throw new OracleException(
                "oracle.overflow",
                "Burden result exceeds the canonical integer range.");
        }

        return (long)value;
    }

    private static void RequireText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Invalid("Burden identity is invalid.");
        }
    }

    [DoesNotReturn]
    private static void Invalid(string message) =>
        throw new OracleException("oracle.input-invalid", message);

    private sealed record Context(
        string RunId,
        string ScenarioId,
        long EpochId,
        long SimulationTimeMs);
}
