using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RideBound.Contracts.Serialization;

namespace RideBound.Contracts.Protocol;

public sealed record CheckpointContent(
    Sha256Hex ManifestHash,
    Sha256Hex StateHash,
    Sha256Hex PreviousDecisionHash,
    long AppliedEpoch,
    long NextEventSequence,
    long SimulationTimeMs,
    JsonElement OnlineState);

public sealed record CheckpointPayload(
    string CheckpointVersion,
    Sha256Hex CheckpointHash,
    CheckpointContent Content);

public sealed record RestoreAcknowledgedPayload(
    string Status,
    Sha256Hex CheckpointHash);

public static class CheckpointPayloadCodec
{
    public const string CurrentVersion = "1.0.0";

    private static readonly byte[] HashDomain =
        "RideBound.CheckpointHash.v1\0"u8.ToArray();

    private static readonly IReadOnlySet<string> Fields =
        Set("checkpointVersion", "checkpointHash", "content");

    private static readonly IReadOnlySet<string> ContentFields = Set(
        "manifestHash",
        "stateHash",
        "previousDecisionHash",
        "appliedEpoch",
        "nextEventSeq",
        "simTimeMs",
        "onlineState");

    public static byte[] Encode(CheckpointPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ValidateContent(payload.Content);

        if (!string.Equals(
                payload.CheckpointVersion,
                CurrentVersion,
                StringComparison.Ordinal)
            || CalculateHash(payload.Content) != payload.CheckpointHash)
        {
            throw new ArgumentException(
                "Checkpoint version/hash does not match its content.",
                nameof(payload));
        }

        return ProtocolPayloadReader.Write(
            writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("checkpointVersion", payload.CheckpointVersion);
                writer.WriteString("checkpointHash", payload.CheckpointHash.Value);
                writer.WritePropertyName("content");
                WriteContent(writer, payload.Content);
                writer.WriteEndObject();
            });
    }

    public static ProtocolPayloadDecodeResult<CheckpointPayload> Decode(
        JsonElement payload)
    {
        var objectError = ProtocolPayloadReader.ValidateObject(
            payload,
            "$.payload",
            Fields);
        var version = ProtocolPayloadReader.ReadRequiredString(
            payload,
            "$.payload",
            "checkpointVersion",
            requireOpaqueValue: false);
        var hashText = ProtocolPayloadReader.ReadRequiredString(
            payload,
            "$.payload",
            "checkpointHash",
            requireOpaqueValue: false);
        var contentProperty = ProtocolPayloadReader.ReadRequiredProperty(
            payload,
            "$.payload",
            "content");
        var error = HelloPayloadCodec.FirstError(
            objectError,
            version.Error,
            hashText.Error,
            contentProperty.Error);

        if (error is not null)
        {
            return ProtocolPayloadDecodeResult<CheckpointPayload>.Failure(error);
        }

        if (!string.Equals(version.Value, CurrentVersion, StringComparison.Ordinal))
        {
            return Invalid(
                "$.payload.checkpointVersion",
                "Unknown checkpoint version.");
        }

        if (!Sha256Hex.TryCreate(hashText.Value, out var hash))
        {
            return Invalid(
                "$.payload.checkpointHash",
                "checkpointHash must be lowercase SHA-256.");
        }

        var content = ReadContent(contentProperty.Value);

        if (!content.IsSuccess)
        {
            return ProtocolPayloadDecodeResult<CheckpointPayload>.Failure(
                content.Error!);
        }

        if (CalculateHash(content.Value!) != hash)
        {
            return Invalid(
                "$.payload.checkpointHash",
                "Checkpoint content hash mismatch.");
        }

        return ProtocolPayloadDecodeResult<CheckpointPayload>.Success(
            new CheckpointPayload(version.Value!, hash!, content.Value!));
    }

    public static Sha256Hex CalculateHash(CheckpointContent content)
    {
        ValidateContent(content);
        var canonical = CanonicalJson.Canonicalize(
            ProtocolPayloadReader.Write(writer => WriteContent(writer, content)));
        var buffer = new ArrayBufferWriter<byte>();
        buffer.Write(HashDomain);
        var tag = Encoding.UTF8.GetBytes("canonicalCheckpointContent");
        var header = buffer.GetSpan(sizeof(ushort));
        BinaryPrimitives.WriteUInt16BigEndian(header, checked((ushort)tag.Length));
        buffer.Advance(sizeof(ushort));
        buffer.Write(tag);
        header = buffer.GetSpan(sizeof(ulong));
        BinaryPrimitives.WriteUInt64BigEndian(header, (ulong)canonical.Length);
        buffer.Advance(sizeof(ulong));
        buffer.Write(canonical);
        var text = Convert.ToHexStringLower(SHA256.HashData(buffer.WrittenSpan));
        Sha256Hex.TryCreate(text, out var hash);
        return hash!;
    }

    private static ProtocolPayloadDecodeResult<CheckpointContent> ReadContent(
        JsonElement element)
    {
        const string path = "$.payload.content";
        var objectError = ProtocolPayloadReader.ValidateObject(
            element,
            path,
            ContentFields);
        var manifest = ProtocolPayloadReader.ReadRequiredString(
            element, path, "manifestHash", requireOpaqueValue: false);
        var state = ProtocolPayloadReader.ReadRequiredString(
            element, path, "stateHash", requireOpaqueValue: false);
        var previous = ProtocolPayloadReader.ReadRequiredString(
            element, path, "previousDecisionHash", requireOpaqueValue: false);
        var epoch = ProtocolPayloadReader.ReadRequiredInteger(
            element, path, "appliedEpoch", minimum: 0);
        var sequence = ProtocolPayloadReader.ReadRequiredInteger(
            element, path, "nextEventSeq", minimum: 1);
        var simTime = ProtocolPayloadReader.ReadRequiredInteger(
            element, path, "simTimeMs", minimum: 0);
        var onlineState = ProtocolPayloadReader.ReadRequiredProperty(
            element, path, "onlineState");
        var error = HelloPayloadCodec.FirstError(
            objectError,
            manifest.Error,
            state.Error,
            previous.Error,
            epoch.Error,
            sequence.Error,
            simTime.Error,
            onlineState.Error);

        if (error is not null)
        {
            return ProtocolPayloadDecodeResult<CheckpointContent>.Failure(error);
        }

        if (!Sha256Hex.TryCreate(manifest.Value, out var manifestHash)
            || !Sha256Hex.TryCreate(state.Value, out var stateHash)
            || !Sha256Hex.TryCreate(previous.Value, out var previousHash))
        {
            return ProtocolPayloadDecodeResult<CheckpointContent>.Failure(
                new ProtocolPayloadError(
                    ProtocolPayloadErrorCode.InvalidValue,
                    path,
                    "Checkpoint hash fields must be lowercase SHA-256."));
        }

        if (onlineState.Value.ValueKind != JsonValueKind.Object)
        {
            return ProtocolPayloadDecodeResult<CheckpointContent>.Failure(
                new ProtocolPayloadError(
                    ProtocolPayloadErrorCode.InvalidFieldType,
                    $"{path}.onlineState",
                    "onlineState must be an object."));
        }

        return ProtocolPayloadDecodeResult<CheckpointContent>.Success(
            new CheckpointContent(
                manifestHash!,
                stateHash!,
                previousHash!,
                epoch.Value,
                sequence.Value,
                simTime.Value,
                onlineState.Value.Clone()));
    }

    private static void WriteContent(
        Utf8JsonWriter writer,
        CheckpointContent content)
    {
        writer.WriteStartObject();
        writer.WriteString("manifestHash", content.ManifestHash.Value);
        writer.WriteString("stateHash", content.StateHash.Value);
        writer.WriteString(
            "previousDecisionHash",
            content.PreviousDecisionHash.Value);
        writer.WriteNumber("appliedEpoch", content.AppliedEpoch);
        writer.WriteNumber("nextEventSeq", content.NextEventSequence);
        writer.WriteNumber("simTimeMs", content.SimulationTimeMs);
        writer.WritePropertyName("onlineState");
        content.OnlineState.WriteTo(writer);
        writer.WriteEndObject();
    }

    private static void ValidateContent(CheckpointContent content)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (content.AppliedEpoch is < 0 or > ProtocolLimits.MaxCanonicalInteger
            || content.NextEventSequence is < 1 or > ProtocolLimits.MaxCanonicalInteger
            || content.SimulationTimeMs is < 0 or > ProtocolLimits.MaxCanonicalInteger
            || content.OnlineState.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Checkpoint content is not canonical.", nameof(content));
        }
    }

    private static ProtocolPayloadDecodeResult<CheckpointPayload> Invalid(
        string field,
        string message) =>
        ProtocolPayloadDecodeResult<CheckpointPayload>.Failure(
            new ProtocolPayloadError(
                ProtocolPayloadErrorCode.InvalidValue,
                field,
                message));

    private static IReadOnlySet<string> Set(params string[] values) =>
        new HashSet<string>(values, StringComparer.Ordinal);
}

public static class RestoreAcknowledgedPayloadCodec
{
    private static readonly IReadOnlySet<string> Fields =
        new HashSet<string>(["status", "checkpointHash"], StringComparer.Ordinal);

    public static byte[] Encode(RestoreAcknowledgedPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (!string.Equals(payload.Status, "restored", StringComparison.Ordinal))
        {
            throw new ArgumentException("Restore status must be 'restored'.", nameof(payload));
        }

        return ProtocolPayloadReader.Write(
            writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("status", payload.Status);
                writer.WriteString("checkpointHash", payload.CheckpointHash.Value);
                writer.WriteEndObject();
            });
    }

    public static ProtocolPayloadDecodeResult<RestoreAcknowledgedPayload> Decode(
        JsonElement payload)
    {
        var objectError = ProtocolPayloadReader.ValidateObject(
            payload,
            "$.payload",
            Fields);
        var status = ProtocolPayloadReader.ReadRequiredString(
            payload,
            "$.payload",
            "status",
            requireOpaqueValue: false);
        var hashText = ProtocolPayloadReader.ReadRequiredString(
            payload,
            "$.payload",
            "checkpointHash",
            requireOpaqueValue: false);
        var error = HelloPayloadCodec.FirstError(
            objectError,
            status.Error,
            hashText.Error);

        if (error is not null
            || !string.Equals(status.Value, "restored", StringComparison.Ordinal)
            || !Sha256Hex.TryCreate(hashText.Value, out var hash))
        {
            return ProtocolPayloadDecodeResult<RestoreAcknowledgedPayload>.Failure(
                error ?? new ProtocolPayloadError(
                    ProtocolPayloadErrorCode.InvalidValue,
                    "$.payload",
                    "Restore acknowledgement is invalid."));
        }

        return ProtocolPayloadDecodeResult<RestoreAcknowledgedPayload>.Success(
            new RestoreAcknowledgedPayload(status.Value!, hash!));
    }
}
