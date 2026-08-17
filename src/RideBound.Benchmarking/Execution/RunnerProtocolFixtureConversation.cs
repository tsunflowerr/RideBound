using System.Text;
using System.Text.Json;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;

namespace RideBound.Benchmarking.Execution;

public sealed record RunnerProtocolFixture(
    byte[] HelloEnvelope,
    byte[] InitializeEnvelope,
    IReadOnlyList<byte[]> EventBatchEnvelopes,
    bool RequestCheckpointAfterFirstDecision = true);

public sealed class RunnerProtocolFixtureConversation(
    RunnerProtocolFixture fixture) : IExternalProcessConversation
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public async Task<ProcessConversationResult> ExecuteAsync(
        Stream standardInput,
        Stream standardOutput,
        CancellationToken cancellationToken)
    {
        var hello = DecodeInput(fixture.HelloEnvelope, "hello");
        var helloPayload = HelloPayloadCodec.Decode(hello.Payload);

        if (!helloPayload.IsSuccess)
        {
            return InvalidPayload(helloPayload.Error!.Message);
        }

        await WriteEnvelope(standardInput, fixture.HelloEnvelope, cancellationToken);
        var helloAck = await ReadEnvelope(standardOutput, cancellationToken);

        if (!IsType(helloAck, "helloAck"))
        {
            return Unexpected("helloAck", helloAck);
        }

        var helloAckPayload = HelloAckPayloadCodec.Decode(helloAck.Payload);

        if (!helloAckPayload.IsSuccess)
        {
            return InvalidPayload(helloAckPayload.Error!.Message);
        }

        var initialize = DecodeInput(fixture.InitializeEnvelope, "initializeRun");
        var initializePayload = InitializeRunPayloadCodec.Decode(initialize.Payload);

        if (!initializePayload.IsSuccess)
        {
            return InvalidPayload(initializePayload.Error!.Message);
        }

        if (!helloPayload.Value!.SupportedSchemaVersions.Contains(
                helloAckPayload.Value!.SelectedSchemaVersion)
            || helloAckPayload.Value.SelectedSchemaVersion
                != initializePayload.Value!.Manifest.ProtocolVersion
            || !CapabilitySelectionsEqual(
                helloAckPayload.Value.CapabilitySelection,
                initializePayload.Value.Manifest.CapabilitySelection))
        {
            return ProcessConversationResult.Failed(
                "capability.divergence",
                "Runner capability selection differs from the fixture-bound manifest.");
        }

        await WriteEnvelope(standardInput, fixture.InitializeEnvelope, cancellationToken);
        var initialized = await ReadEnvelope(standardOutput, cancellationToken);

        if (!IsType(initialized, "initialized")
            || initialized.RunId != initialize.RunId
            || initialized.ScenarioId != initialize.ScenarioId)
        {
            return Unexpected("matching initialized", initialized);
        }

        var initializedPayload = InitializedPayloadCodec.Decode(initialized.Payload);

        if (!initializedPayload.IsSuccess
            || initializedPayload.Value!.ManifestHash
                != ProtocolHash.CalculateManifestHash(initializePayload.Value.Manifest))
        {
            return ProcessConversationResult.Failed(
                "state.divergence",
                initializedPayload.Error?.Message
                    ?? "Runner initialized manifest hash differs from exact fixture input.");
        }

        for (var index = 0; index < fixture.EventBatchEnvelopes.Count; index++)
        {
            var eventBatchBytes = fixture.EventBatchEnvelopes[index];
            var eventBatch = DecodeInput(eventBatchBytes, "eventBatch");
            var eventPayload = EventBatchPayloadCodec.Decode(eventBatch.Payload);

            if (!eventPayload.IsSuccess)
            {
                return InvalidPayload(eventPayload.Error!.Message);
            }

            await WriteEnvelope(standardInput, eventBatchBytes, cancellationToken);
            var decision = await ReadEnvelope(standardOutput, cancellationToken);

            if (!IsType(decision, "decision")
                || decision.RunId != eventBatch.RunId
                || decision.ScenarioId != eventBatch.ScenarioId
                || decision.EpochId != eventBatch.EpochId
                || decision.SimTime != eventBatch.SimTime)
            {
                return Unexpected("decision with exact event context", decision);
            }

            var decodedDecision = DecisionPayloadCodec.Decode(decision.Payload);

            if (!decodedDecision.IsSuccess)
            {
                return ProcessConversationResult.Failed(
                    "protocol.invalid-output",
                    decodedDecision.Error!.Message);
            }

            var acknowledgement = CreateEnvelope(
                "decisionApplied",
                DecisionAppliedPayloadCodec.Encode(
                    new DecisionAppliedPayload(decodedDecision.Value!.DecisionHash)),
                decision.RunId,
                decision.ScenarioId,
                decision.EpochId,
                decision.SimTime);
            await WriteCanonical(standardInput, acknowledgement, cancellationToken);

            if (index == 0 && fixture.RequestCheckpointAfterFirstDecision)
            {
                var checkpointRequest = CreateEnvelope(
                    "checkpoint",
                    "{}"u8.ToArray(),
                    decision.RunId,
                    decision.ScenarioId);
                await WriteCanonical(standardInput, checkpointRequest, cancellationToken);
                var checkpoint = await ReadEnvelope(standardOutput, cancellationToken);

                if (!IsType(checkpoint, "checkpoint")
                    || checkpoint.RunId != decision.RunId
                    || checkpoint.ScenarioId != decision.ScenarioId)
                {
                    return Unexpected("checkpoint", checkpoint);
                }

                var checkpointPayload = CheckpointPayloadCodec.Decode(checkpoint.Payload);

                if (!checkpointPayload.IsSuccess)
                {
                    return ProcessConversationResult.Failed(
                        "protocol.invalid-output",
                        checkpointPayload.Error!.Message);
                }

                if (checkpointPayload.Value!.Content.ManifestHash
                        != initializedPayload.Value.ManifestHash
                    || checkpointPayload.Value.Content.AppliedEpoch
                        != decision.EpochId!.Value.Value
                    || checkpointPayload.Value.Content.PreviousDecisionHash
                        != decodedDecision.Value.DecisionHash)
                {
                    return ProcessConversationResult.Failed(
                        "state.divergence",
                        "Checkpoint does not bind the initialized manifest and applied decision.");
                }
            }
        }

        var shutdown = CreateEnvelope("shutdown", "{}"u8.ToArray());
        await WriteCanonical(standardInput, shutdown, cancellationToken);
        standardInput.Close();
        var extra = new byte[1];
        var extraCount = await standardOutput.ReadAsync(extra, cancellationToken);

        return extraCount == 0
            ? ProcessConversationResult.Success()
            : ProcessConversationResult.Failed(
                "protocol.invalid-output",
                "Runner emitted extra bytes after the completed fixture conversation.");
    }

    internal static ProtocolEnvelope DecodeInput(byte[] bytes, string expectedType)
    {
        _ = StrictUtf8.GetString(bytes);
        var canonical = CanonicalJson.Canonicalize(bytes);
        var decoded = ProtocolEnvelopeCodec.Decode(canonical);

        if (!decoded.IsSuccess || !IsType(decoded.Envelope!, expectedType))
        {
            throw new InvalidDataException(
                $"Fixture input is not a valid canonicalizable '{expectedType}' envelope.");
        }

        return decoded.Envelope!;
    }

    internal static async Task<ProtocolEnvelope> ReadEnvelope(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var bytes = new List<byte>();
        var one = new byte[1];

        while (true)
        {
            var read = await stream.ReadAsync(one, cancellationToken);

            if (read == 0)
            {
                throw new EndOfStreamException(
                    "Runner stdout ended before the required protocol response.");
            }

            if (one[0] == (byte)'\n')
            {
                break;
            }

            if (one[0] == (byte)'\r')
            {
                throw new InvalidDataException("Runner stdout must use exact LF framing.");
            }

            bytes.Add(one[0]);
        }

        _ = StrictUtf8.GetString(bytes.ToArray());
        var encoded = bytes.ToArray();
        var canonical = CanonicalJson.Canonicalize(encoded);

        if (!encoded.SequenceEqual(canonical))
        {
            throw new InvalidDataException("Runner output envelope is not canonical JSON.");
        }

        var decoded = ProtocolEnvelopeCodec.Decode(canonical);

        if (!decoded.IsSuccess)
        {
            throw new InvalidDataException(decoded.Error!.Message);
        }

        return decoded.Envelope!;
    }

    internal static async Task WriteEnvelope(
        Stream stream,
        byte[] envelope,
        CancellationToken cancellationToken) =>
        await WriteCanonical(
            stream,
            CanonicalJson.Canonicalize(envelope),
            cancellationToken);

    internal static async Task WriteCanonical(
        Stream stream,
        byte[] canonical,
        CancellationToken cancellationToken)
    {
        await stream.WriteAsync(canonical, cancellationToken);
        await stream.WriteAsync("\n"u8.ToArray(), cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    internal static byte[] CreateEnvelope(
        string messageType,
        byte[] payload,
        RunId? runId = null,
        ScenarioId? scenarioId = null,
        EpochId? epochId = null,
        SimulationTimeMilliseconds? simTime = null)
    {
        ProtocolMessageType.TryParse(messageType, out var parsedType);
        using var document = JsonDocument.Parse(payload);
        return CanonicalJson.Serialize(
            new ProtocolEnvelope(
                ProtocolVersion.Current,
                parsedType!,
                document.RootElement.Clone(),
                runId,
                scenarioId,
                epochId,
                simTime));
    }

    internal static bool IsType(ProtocolEnvelope envelope, string type) =>
        string.Equals(envelope.MessageType.Value, type, StringComparison.Ordinal);

    internal static ProcessConversationResult Unexpected(
        string expected,
        ProtocolEnvelope actual) =>
        ProcessConversationResult.Failed(
            "protocol.invalid-output",
            $"Expected {expected}; received '{actual.MessageType.Value}'.");

    internal static ProcessConversationResult InvalidPayload(string message) =>
        ProcessConversationResult.Failed("protocol.invalid-output", message);

    internal static bool CapabilitySelectionsEqual(
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
