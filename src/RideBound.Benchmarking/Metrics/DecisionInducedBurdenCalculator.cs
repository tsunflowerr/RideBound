using System.Numerics;
using System.Text.Json;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;

namespace RideBound.Benchmarking.Metrics;

public sealed record DecisionInducedBurdenResult(
    string SchemaVersion,
    string RunId,
    string ArmId,
    string PolicyId,
    long ArrivedRiderCount,
    long AcceptedRiderCount,
    long RejectedRiderCount,
    long CompletedRiderCount,
    long PickupEtaDecisionDeltaSumMs,
    long DropEtaDecisionDeltaSumMs,
    long MaterialEtaRevisionCount,
    long VehicleSwitchCount,
    long PickupStopRelocationMm,
    long PickupStopSwitchCount,
    long DropStopRelocationMm,
    long DropStopSwitchCount,
    long IncumbentOrderInversionCount,
    long PrePickupInsertedStopCount,
    long TotalDecisionInducedBurdenMs,
    long DisruptiveRevisionFrameCount)
{
    public const string CurrentSchemaVersion = "1.0.0";
}

public static class DecisionInducedBurdenResultCodec
{
    public static byte[] Encode(DecisionInducedBurdenResult value)
    {
        ArgumentNullException.ThrowIfNull(value);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("schemaVersion", value.SchemaVersion);
            writer.WriteString("runId", value.RunId);
            writer.WriteString("armId", value.ArmId);
            writer.WriteString("policyId", value.PolicyId);
            writer.WriteNumber("arrivedRiderCount", value.ArrivedRiderCount);
            writer.WriteNumber("acceptedRiderCount", value.AcceptedRiderCount);
            writer.WriteNumber("rejectedRiderCount", value.RejectedRiderCount);
            writer.WriteNumber("completedRiderCount", value.CompletedRiderCount);
            writer.WriteNumber(
                "pickupEtaDecisionDeltaSumMs",
                value.PickupEtaDecisionDeltaSumMs);
            writer.WriteNumber(
                "dropEtaDecisionDeltaSumMs",
                value.DropEtaDecisionDeltaSumMs);
            writer.WriteNumber(
                "materialEtaRevisionCount",
                value.MaterialEtaRevisionCount);
            writer.WriteNumber("vehicleSwitchCount", value.VehicleSwitchCount);
            writer.WriteNumber(
                "pickupStopRelocationMm",
                value.PickupStopRelocationMm);
            writer.WriteNumber(
                "pickupStopSwitchCount",
                value.PickupStopSwitchCount);
            writer.WriteNumber(
                "dropStopRelocationMm",
                value.DropStopRelocationMm);
            writer.WriteNumber("dropStopSwitchCount", value.DropStopSwitchCount);
            writer.WriteNumber(
                "incumbentOrderInversionCount",
                value.IncumbentOrderInversionCount);
            writer.WriteNumber(
                "prePickupInsertedStopCount",
                value.PrePickupInsertedStopCount);
            writer.WriteNumber(
                "totalDecisionInducedBurdenMs",
                value.TotalDecisionInducedBurdenMs);
            writer.WriteNumber(
                "disruptiveRevisionFrameCount",
                value.DisruptiveRevisionFrameCount);
            writer.WriteEndObject();
        }

        return CanonicalJson.Canonicalize(stream.ToArray());
    }
}

public static class DecisionInducedBurdenCalculator
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

    public static DecisionInducedBurdenResult CalculateFromTranscripts(
        string runId,
        string armId,
        string policyId,
        ReadOnlySpan<byte> inputTranscript,
        ReadOnlySpan<byte> outputTranscript)
    {
        RequireText(runId, nameof(runId));
        RequireText(armId, nameof(armId));
        RequireText(policyId, nameof(policyId));

        var arrivals = new HashSet<string>(StringComparer.Ordinal);
        var bookings = new HashSet<string>(StringComparer.Ordinal);
        var boardings = new HashSet<string>(StringComparer.Ordinal);
        var completions = new HashSet<string>(StringComparer.Ordinal);
        var cancellations = new HashSet<string>(StringComparer.Ordinal);
        var acceptances = new HashSet<string>(StringComparer.Ordinal);
        var rejections = new HashSet<string>(StringComparer.Ordinal);
        var inputContexts = new List<TranscriptContext>();
        var outputContexts = new List<TranscriptContext>();
        var totals = VectorFields.ToDictionary(
            field => field,
            _ => BigInteger.Zero,
            StringComparer.Ordinal);
        BigInteger disruptiveFrames = 0;

        foreach (var envelope in ReadTranscript(inputTranscript.ToArray(), runId))
        {
            if (envelope.MessageType.Value != "eventBatch")
            {
                continue;
            }

            inputContexts.Add(Context(envelope));
            foreach (var item in envelope.Payload.GetProperty("events").EnumerateArray())
            {
                var eventType = item.GetProperty("eventType").GetString();
                var payload = item.GetProperty("payload");

                switch (eventType)
                {
                    case "requestArrived":
                        AddUnique(
                            arrivals,
                            payload.GetProperty("request")
                                .GetProperty("requestId")
                                .GetString(),
                            "request arrival");
                        break;
                    case "bookingConfirmed":
                        AddUnique(
                            bookings,
                            payload.GetProperty("requestId").GetString(),
                            "booking confirmation");
                        break;
                    case "passengerBoarded":
                        AddUnique(
                            boardings,
                            payload.GetProperty("requestId").GetString(),
                            "passenger boarding");
                        break;
                    case "passengerAlighted":
                        AddUnique(
                            completions,
                            payload.GetProperty("requestId").GetString(),
                            "passenger alighting");
                        break;
                    case "requestCancelledBeforeAcceptance":
                    case "requestCancelledAfterAcceptance":
                        AddUnique(
                            cancellations,
                            payload.GetProperty("requestId").GetString(),
                            "request cancellation");
                        break;
                }
            }
        }

        foreach (var envelope in ReadTranscript(outputTranscript.ToArray(), runId))
        {
            if (envelope.MessageType.Value != "decision")
            {
                continue;
            }

            outputContexts.Add(Context(envelope));
            var frameWasDisruptive = false;
            foreach (var action in envelope.Payload.GetProperty("actions").EnumerateArray())
            {
                var type = action.GetProperty("decisionType").GetString();
                var payload = action.GetProperty("payload");

                if (type == "requestAccepted")
                {
                    AddUnique(
                        acceptances,
                        payload.GetProperty("requestId").GetString(),
                        "request acceptance");
                }
                else if (type == "requestRejected")
                {
                    AddUnique(
                        rejections,
                        payload.GetProperty("requestId").GetString(),
                        "request rejection");
                }
                else if (type == "promisePublished")
                {
                    var decisionDelta = payload.GetProperty("decisionDelta");
                    var anyNonZero = false;

                    foreach (var field in VectorFields)
                    {
                        var value = decisionDelta.GetProperty(field).GetInt64();
                        if (value < 0)
                        {
                            throw new InvalidDataException(
                                $"Decision delta '{field}' cannot be negative.");
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

        if (!inputContexts.SequenceEqual(outputContexts))
        {
            throw new InvalidDataException(
                "Event-batch and decision contexts are not an exact ordered match.");
        }

        if (cancellations.Count != 0)
        {
            throw new InvalidDataException(
                "The WP8 terminal burden contract does not permit cancellations.");
        }

        if (acceptances.Overlaps(rejections)
            || !acceptances.Concat(rejections).ToHashSet(StringComparer.Ordinal)
                .SetEquals(arrivals)
            || !bookings.SetEquals(acceptances)
            || !boardings.SetEquals(bookings)
            || !completions.SetEquals(boardings))
        {
            throw new InvalidDataException(
                "Terminal request lifecycle and accepted/rejected outcomes do not conserve arrivals.");
        }

        var pickup = Canonical(totals["pickupEtaTotalMs"], "pickup ETA burden");
        var drop = Canonical(totals["dropEtaTotalMs"], "drop ETA burden");
        var totalBurden = Canonical(
            totals["pickupEtaTotalMs"] + totals["dropEtaTotalMs"],
            "total ETA burden");

        return new DecisionInducedBurdenResult(
            DecisionInducedBurdenResult.CurrentSchemaVersion,
            runId,
            armId,
            policyId,
            arrivals.Count,
            acceptances.Count,
            rejections.Count,
            completions.Count,
            pickup,
            drop,
            Canonical(totals["materialEtaRevisionCount"], "material revisions"),
            Canonical(totals["vehicleSwitchCount"], "vehicle switches"),
            Canonical(totals["pickupStopRelocationMm"], "pickup relocation"),
            Canonical(totals["pickupStopSwitchCount"], "pickup stop switches"),
            Canonical(totals["dropStopRelocationMm"], "drop relocation"),
            Canonical(totals["dropStopSwitchCount"], "drop stop switches"),
            Canonical(
                totals["incumbentOrderInversionCount"],
                "incumbent order inversions"),
            Canonical(
                totals["prePickupInsertedStopCount"],
                "pre-pickup inserted stops"),
            totalBurden,
            Canonical(disruptiveFrames, "disruptive revision frames"));
    }

    private static IReadOnlyList<ProtocolEnvelope> ReadTranscript(
        byte[] bytes,
        string runId)
    {
        var values = new List<ProtocolEnvelope>();

        foreach (var line in ReadCanonicalLines(bytes))
        {
            var decoded = ProtocolEnvelopeCodec.Decode(line);

            if (!decoded.IsSuccess
                || decoded.Envelope!.RunId is not null
                    && decoded.Envelope.RunId.Value != runId)
            {
                throw new InvalidDataException(
                    "Protocol transcript is invalid or cross-linked.");
            }

            values.Add(decoded.Envelope);
        }

        return values;
    }

    private static IReadOnlyList<byte[]> ReadCanonicalLines(byte[] bytes)
    {
        if (bytes.Length == 0 || bytes[^1] != (byte)'\n')
        {
            throw new InvalidDataException(
                "Canonical NDJSON evidence is empty or incomplete.");
        }

        var values = new List<byte[]>();
        var offset = 0;

        while (offset < bytes.Length)
        {
            var length = bytes.AsSpan(offset).IndexOf((byte)'\n');

            if (length <= 0)
            {
                throw new InvalidDataException(
                    "Canonical NDJSON contains an empty frame.");
            }

            var line = bytes.AsSpan(offset, length).ToArray();
            byte[] canonical;

            try
            {
                canonical = CanonicalJson.Canonicalize(line);
            }
            catch (CanonicalJsonException exception)
            {
                throw new InvalidDataException(
                    "NDJSON contains invalid canonical protocol JSON.",
                    exception);
            }

            if (!line.AsSpan().SequenceEqual(canonical))
            {
                throw new InvalidDataException(
                    "NDJSON frame is valid JSON but not canonical JSON.");
            }

            values.Add(line);
            offset += length + 1;
        }

        return values;
    }

    private static TranscriptContext Context(ProtocolEnvelope envelope) =>
        new(
            envelope.RunId?.Value,
            envelope.ScenarioId?.Value,
            envelope.EpochId?.Value,
            envelope.SimTime?.Value);

    private static void AddUnique(
        ISet<string> values,
        string? value,
        string description)
    {
        if (string.IsNullOrWhiteSpace(value) || !values.Add(value))
        {
            throw new InvalidDataException(
                $"Transcript contains a missing or duplicate {description}.");
        }
    }

    private static long Canonical(BigInteger value, string description)
    {
        if (value < BigInteger.Zero
            || value > new BigInteger(ProtocolLimits.MaxCanonicalInteger))
        {
            throw new OverflowException(
                $"{description} is outside the canonical integer range.");
        }

        return (long)value;
    }

    private static void RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be empty.", parameterName);
        }
    }

    private sealed record TranscriptContext(
        string? RunId,
        string? ScenarioId,
        long? EpochId,
        long? SimulationTimeMs);
}
