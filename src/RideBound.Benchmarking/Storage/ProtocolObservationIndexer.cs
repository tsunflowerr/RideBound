using System.Security.Cryptography;
using System.Text.Json;
using RideBound.Benchmarking.Contracts;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;

namespace RideBound.Benchmarking.Storage;

internal static class ProtocolObservationIndexer
{
    public static IReadOnlyList<ObservationIndexRow> Build(
        RunStoreIntent intent,
        ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> output,
        bool requireCompleteTranscripts)
    {
        var rows = new List<ObservationIndexRow>();
        var inputEnvelopes = new List<ProtocolEnvelope>();
        var outputEnvelopes = new List<ProtocolEnvelope>();
        AddTranscript(
            rows,
            inputEnvelopes,
            intent,
            input,
            TranscriptRole.Input,
            requireCompleteTranscripts);
        AddTranscript(
            rows,
            outputEnvelopes,
            intent,
            output,
            TranscriptRole.Output,
            requireCompleteTranscripts);

        if (requireCompleteTranscripts)
        {
            ValidateSuccessfulConversation(intent, inputEnvelopes, outputEnvelopes);
        }

        return rows;
    }

    public static byte[] Encode(IReadOnlyList<ObservationIndexRow> rows)
    {
        using var stream = new MemoryStream();

        foreach (var row in rows)
        {
            var bytes = BenchmarkContractCodec.Encode(row);
            stream.Write(bytes);
            stream.WriteByte((byte)'\n');
        }

        return stream.ToArray();
    }

    private static void AddTranscript(
        ICollection<ObservationIndexRow> rows,
        ICollection<ProtocolEnvelope> envelopes,
        RunStoreIntent intent,
        ReadOnlySpan<byte> transcript,
        TranscriptRole role,
        bool requireComplete)
    {
        var offset = 0;
        long lineNumber = 0;

        while (offset < transcript.Length)
        {
            var relativeLf = transcript[offset..].IndexOf((byte)'\n');

            if (relativeLf < 0)
            {
                if (requireComplete)
                {
                    throw new InvalidDataException("Protocol transcript ends with an incomplete frame.");
                }

                return;
            }

            lineNumber++;
            var line = transcript.Slice(offset, relativeLf);
            offset += relativeLf + 1;

            if (line.Length == 0 || line.Contains((byte)'\r'))
            {
                if (requireComplete)
                {
                    throw new InvalidDataException("Protocol transcript contains invalid LF framing.");
                }

                return;
            }

            byte[] canonical;

            try
            {
                canonical = CanonicalJson.Canonicalize(line);
            }
            catch (CanonicalJsonException) when (!requireComplete)
            {
                return;
            }

            if (!line.SequenceEqual(canonical))
            {
                if (requireComplete)
                {
                    throw new InvalidDataException("Protocol transcript contains noncanonical JSON.");
                }

                return;
            }

            var decoded = ProtocolEnvelopeCodec.Decode(canonical);

            if (!decoded.IsSuccess)
            {
                if (requireComplete)
                {
                    throw new InvalidDataException(decoded.Error!.Message);
                }

                return;
            }

            if (decoded.Envelope!.RunId is not null
                && decoded.Envelope.RunId.Value != intent.RunId)
            {
                if (requireComplete)
                {
                    throw new InvalidDataException(
                        "Protocol transcript is cross-linked to another run ID.");
                }

                return;
            }

            envelopes.Add(decoded.Envelope);

            AddEnvelopeRows(
                rows,
                intent,
                decoded.Envelope!,
                role,
                lineNumber,
                Convert.ToHexStringLower(SHA256.HashData(canonical)),
                requireComplete);
        }
    }

    private static void AddEnvelopeRows(
        ICollection<ObservationIndexRow> rows,
        RunStoreIntent intent,
        ProtocolEnvelope envelope,
        TranscriptRole role,
        long lineNumber,
        string envelopeSha256,
        bool requireComplete)
    {
        if (role == TranscriptRole.Input && envelope.MessageType.Value == "eventBatch")
        {
            var batch = EventBatchPayloadCodec.Decode(envelope.Payload);

            if (!batch.IsSuccess)
            {
                InvalidOrSkip(batch.Error!.Message, requireComplete);
                return;
            }

            var eventElements = envelope.Payload.GetProperty("events").EnumerateArray().ToArray();

            for (var index = 0; index < batch.Value!.Events.Count; index++)
            {
                var ids = CollectIds(eventElements[index].GetProperty("payload"));
                rows.Add(
                    CreateRow(
                        rows.Count + 1,
                        ObservationRecordKind.InputEvent,
                        intent,
                        role,
                        lineNumber,
                        envelopeSha256,
                        ids.RequestIds,
                        ids.VehicleIds,
                        envelope.EpochId!.Value.Value,
                        envelope.SimTime!.Value.Value,
                        batch.Value.Events[index].EventSequence.Value));
            }

            return;
        }

        if (role == TranscriptRole.Input && envelope.MessageType.Value == "decisionApplied")
        {
            var acknowledgement = DecisionAppliedPayloadCodec.Decode(envelope.Payload);

            if (!acknowledgement.IsSuccess)
            {
                InvalidOrSkip(acknowledgement.Error!.Message, requireComplete);
                return;
            }

            rows.Add(
                CreateRow(
                    rows.Count + 1,
                    ObservationRecordKind.DecisionAck,
                    intent,
                    role,
                    lineNumber,
                    envelopeSha256,
                    [],
                    [],
                    envelope.EpochId!.Value.Value,
                    envelope.SimTime!.Value.Value,
                    decisionHash: acknowledgement.Value!.DecisionHash.Value));
            return;
        }

        if (role == TranscriptRole.Output && envelope.MessageType.Value == "decision")
        {
            var decision = DecisionPayloadCodec.Decode(envelope.Payload);

            if (!decision.IsSuccess)
            {
                InvalidOrSkip(decision.Error!.Message, requireComplete);
                return;
            }

            var ids = CollectIds(envelope.Payload);
            rows.Add(
                CreateRow(
                    rows.Count + 1,
                    ObservationRecordKind.OutputDecision,
                    intent,
                    role,
                    lineNumber,
                    envelopeSha256,
                    ids.RequestIds,
                    ids.VehicleIds,
                    envelope.EpochId!.Value.Value,
                    envelope.SimTime!.Value.Value,
                    decisionHash: decision.Value!.DecisionHash.Value,
                    certificateHash: CertificateHash(envelope.Payload)));
            return;
        }

        if (role == TranscriptRole.Output && envelope.MessageType.Value == "checkpoint")
        {
            var checkpoint = CheckpointPayloadCodec.Decode(envelope.Payload);

            if (!checkpoint.IsSuccess)
            {
                InvalidOrSkip(checkpoint.Error!.Message, requireComplete);
                return;
            }

            var ids = CollectIds(envelope.Payload);
            rows.Add(
                CreateRow(
                    rows.Count + 1,
                    ObservationRecordKind.Checkpoint,
                    intent,
                    role,
                    lineNumber,
                    envelopeSha256,
                    ids.RequestIds,
                    ids.VehicleIds));
        }
    }

    private static ObservationIndexRow CreateRow(
        long sequence,
        ObservationRecordKind kind,
        RunStoreIntent intent,
        TranscriptRole role,
        long lineNumber,
        string envelopeSha256,
        IReadOnlyList<string> requestIds,
        IReadOnlyList<string> vehicleIds,
        long? epochId = null,
        long? simTimeMs = null,
        long? eventSequence = null,
        string? decisionHash = null,
        string? certificateHash = null) =>
        new(
            BenchmarkContractVersions.V1,
            sequence,
            kind,
            intent.RunId,
            intent.ScenarioHash,
            intent.ArmId,
            intent.RepeatIndex,
            intent.AttemptIndex,
            role,
            lineNumber,
            envelopeSha256,
            requestIds,
            vehicleIds,
            epochId,
            simTimeMs,
            eventSequence,
            decisionHash,
            certificateHash);

    private static (IReadOnlyList<string> RequestIds, IReadOnlyList<string> VehicleIds)
        CollectIds(JsonElement element)
    {
        var requests = new HashSet<string>(StringComparer.Ordinal);
        var vehicles = new HashSet<string>(StringComparer.Ordinal);
        Walk(element, requests, vehicles);
        return (
            requests.Order(StringComparer.Ordinal).ToArray(),
            vehicles.Order(StringComparer.Ordinal).ToArray());
    }

    private static void Walk(
        JsonElement element,
        ISet<string> requests,
        ISet<string> vehicles)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String
                    && property.NameEquals("requestId"))
                {
                    requests.Add(property.Value.GetString()!);
                }
                else if (property.Value.ValueKind == JsonValueKind.String
                    && property.NameEquals("vehicleId"))
                {
                    vehicles.Add(property.Value.GetString()!);
                }
                else if (property.Value.ValueKind == JsonValueKind.Array
                    && property.NameEquals("requestIds"))
                {
                    AddStrings(property.Value, requests);
                }
                else if (property.Value.ValueKind == JsonValueKind.Array
                    && property.NameEquals("vehicleIds"))
                {
                    AddStrings(property.Value, vehicles);
                }

                Walk(property.Value, requests, vehicles);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                Walk(item, requests, vehicles);
            }
        }
    }

    private static void AddStrings(JsonElement array, ISet<string> target)
    {
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                target.Add(item.GetString()!);
            }
        }
    }

    private static string? CertificateHash(JsonElement decisionPayload)
    {
        var certificate = decisionPayload.GetProperty("certificate");

        if (!certificate.TryGetProperty("body", out var body))
        {
            return null;
        }

        var canonical = CanonicalJson.Canonicalize(
            System.Text.Encoding.UTF8.GetBytes(body.GetRawText()));
        return Convert.ToHexStringLower(SHA256.HashData(canonical));
    }

    private static void InvalidOrSkip(string message, bool requireComplete)
    {
        if (requireComplete)
        {
            throw new InvalidDataException(message);
        }
    }

    private static void ValidateSuccessfulConversation(
        RunStoreIntent intent,
        IReadOnlyList<ProtocolEnvelope> input,
        IReadOnlyList<ProtocolEnvelope> output)
    {
        if (input.Count < 5
            || output.Count < 3
            || input[0].MessageType.Value != "hello"
            || input[1].MessageType.Value != "initializeRun"
            || input[^1].MessageType.Value != "shutdown"
            || output[0].MessageType.Value != "helloAck"
            || output[1].MessageType.Value != "initialized"
            || !SameRunContext(input[1], output[1]))
        {
            throw new InvalidDataException(
                "Succeeded transcript lacks the required handshake/terminal sequence.");
        }

        var hello = HelloPayloadCodec.Decode(input[0].Payload);
        var helloAck = HelloAckPayloadCodec.Decode(output[0].Payload);
        var initialize = InitializeRunPayloadCodec.Decode(input[1].Payload);
        var initialized = InitializedPayloadCodec.Decode(output[1].Payload);

        if (!hello.IsSuccess
            || !helloAck.IsSuccess
            || !initialize.IsSuccess
            || !initialized.IsSuccess
            || !hello.Value!.SupportedSchemaVersions.Contains(
                helloAck.Value!.SelectedSchemaVersion)
            || helloAck.Value.SelectedSchemaVersion != initialize.Value!.Manifest.ProtocolVersion
            || !CapabilitySelectionsEqual(
                helloAck.Value.CapabilitySelection,
                initialize.Value.Manifest.CapabilitySelection)
            || initialized.Value!.ManifestHash
                != ProtocolHash.CalculateManifestHash(initialize.Value.Manifest)
            || initialize.Value.Manifest.ScenarioContentHash.Value != intent.ScenarioHash
            || initialize.Value.Manifest.PolicyConfigurationHash.Value
                != intent.PolicyConfigurationSha256
            || initialize.Value.Manifest.MasterSeed
                != BenchmarkSeed.ToNonNegativeInt32(intent.ComponentSeedHex)
            || initialize.Value.Manifest.BinarySha256.Value != intent.RunnerArtifactSha256)
        {
            throw new InvalidDataException(
                "Succeeded transcript handshake or plan-bound manifest identity diverges.");
        }

        var expectedRunId = input[1].RunId;
        var expectedScenarioId = input[1].ScenarioId;

        if (input.Concat(output).Any(
            envelope => envelope.RunId is not null
                && (envelope.RunId != expectedRunId
                    || envelope.ScenarioId != expectedScenarioId)))
        {
            throw new InvalidDataException(
                "Succeeded transcript contains cross-run or cross-scenario context.");
        }

        var inputIndex = 2;
        var outputIndex = 2;
        var decisionCount = 0;
        var previousDecisionHash = ProtocolHash.ZeroHash;
        long lastAppliedEpoch = 0;
        long lastSimulationTimeMs = 0;

        while (inputIndex < input.Count - 1)
        {
            var request = input[inputIndex++];

            if (request.MessageType.Value == "checkpoint")
            {
                if (outputIndex >= output.Count
                    || output[outputIndex].MessageType.Value != "checkpoint"
                    || !SameRunContext(request, output[outputIndex]))
                {
                    throw new InvalidDataException(
                        "Checkpoint request/response context is incomplete.");
                }

                var checkpoint = CheckpointPayloadCodec.Decode(output[outputIndex].Payload);

                if (!checkpoint.IsSuccess
                    || checkpoint.Value!.Content.ManifestHash
                        != initialized.Value.ManifestHash
                    || checkpoint.Value.Content.PreviousDecisionHash != previousDecisionHash
                    || checkpoint.Value.Content.AppliedEpoch != lastAppliedEpoch
                    || checkpoint.Value.Content.SimulationTimeMs != lastSimulationTimeMs)
                {
                    throw new InvalidDataException(
                        "Checkpoint does not bind the initialized manifest/applied state chain.");
                }

                outputIndex++;
                continue;
            }

            if (request.MessageType.Value != "eventBatch"
                || outputIndex >= output.Count
                || output[outputIndex].MessageType.Value != "decision"
                || !SameEpochContext(request, output[outputIndex]))
            {
                throw new InvalidDataException(
                    "Event/decision sequence or exact epoch context is incomplete.");
            }

            var decision = DecisionPayloadCodec.Decode(output[outputIndex].Payload);

            if (!decision.IsSuccess
                || decision.Value!.PreviousDecisionHash != previousDecisionHash
                || inputIndex >= input.Count - 1
                || input[inputIndex].MessageType.Value != "decisionApplied"
                || !SameEpochContext(request, input[inputIndex]))
            {
                throw new InvalidDataException("Decision lacks its exact applied acknowledgement.");
            }

            var acknowledgement = DecisionAppliedPayloadCodec.Decode(input[inputIndex].Payload);

            if (!acknowledgement.IsSuccess
                || acknowledgement.Value!.DecisionHash != decision.Value!.DecisionHash)
            {
                throw new InvalidDataException("Decision acknowledgement hash mismatch.");
            }

            inputIndex++;
            outputIndex++;
            decisionCount++;
            previousDecisionHash = decision.Value.DecisionHash;
            lastAppliedEpoch = request.EpochId!.Value.Value;
            lastSimulationTimeMs = request.SimTime!.Value.Value;
        }

        if (decisionCount == 0 || outputIndex != output.Count)
        {
            throw new InvalidDataException(
                "Succeeded transcript has no decision or contains extra output.");
        }
    }

    private static bool SameRunContext(ProtocolEnvelope left, ProtocolEnvelope right) =>
        left.RunId == right.RunId && left.ScenarioId == right.ScenarioId;

    private static bool SameEpochContext(ProtocolEnvelope left, ProtocolEnvelope right) =>
        SameRunContext(left, right)
        && left.EpochId == right.EpochId
        && left.SimTime == right.SimTime;

    private static bool CapabilitySelectionsEqual(
        CapabilitySelection left,
        CapabilitySelection right) =>
        left.Status == right.Status
        && left.PositionModel == right.PositionModel
        && left.MaxFleetSize == right.MaxFleetSize
        && left.MaxRequestCount == right.MaxRequestCount
        && string.Equals(
            left.DowngradePolicyId,
            right.DowngradePolicyId,
            StringComparison.Ordinal)
        && left.Capabilities.SequenceEqual(right.Capabilities);
}
