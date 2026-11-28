using System.Buffers.Binary;
using System.Security.Cryptography;
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

        return WritePayload(payload.CheckpointVersion, payload.CheckpointHash, payload.Content);
    }

    private static byte[] WritePayload(string version, Sha256Hex hash, CheckpointContent content) =>
        ProtocolPayloadReader.Write(
            writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("checkpointVersion", version);
                writer.WriteString("checkpointHash", hash.Value);
                writer.WritePropertyName("content");
                WriteContent(writer, content);
                writer.WriteEndObject();
            });

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

        try
        {
            if (CalculateHash(content.Value!) != hash)
            {
                return Invalid(
                    "$.payload.checkpointHash",
                    "Checkpoint content hash mismatch.");
            }
        }
        catch (CanonicalJsonException canonicalError)
        {
            return Invalid("$.payload.content", canonicalError.Message);
        }

        return ProtocolPayloadDecodeResult<CheckpointPayload>.Success(
            new CheckpointPayload(version.Value!, hash!, content.Value!));
    }

    public static Sha256Hex CalculateHash(CheckpointContent content)
    {
        ValidateContent(content);
        var canonical = CanonicalJson.Canonicalize(
            ProtocolPayloadReader.Write(writer => WriteContent(writer, content)));
        return HashCanonicalContent(canonical, [], default);
    }

    /// <summary>Hashes an empty checkpoint shell with the supplied canonical state bytes.</summary>
    public static Sha256Hex CalculateHash(
        CheckpointContent shell,
        IReadOnlyList<ReadOnlyMemory<byte>> canonicalOnlineState)
    {
        ArgumentNullException.ThrowIfNull(canonicalOnlineState);
        RequireEmptyOnlineState(shell);
        var canonical = CanonicalJson.Canonicalize(
            ProtocolPayloadReader.Write(writer => WriteContent(writer, shell)));
        var (before, after) = SplitAtEmptyOnlineState(canonical);
        return HashCanonicalContent(before, canonicalOnlineState, after);
    }

    /// <summary>Encodes a checkpoint shell whose empty onlineState must be filled before sending.</summary>
    public static byte[] EncodeShell(Sha256Hex checkpointHash, CheckpointContent shell)
    {
        ArgumentNullException.ThrowIfNull(checkpointHash);
        RequireEmptyOnlineState(shell);
        return WritePayload(CurrentVersion, checkpointHash, shell);
    }

    /// <summary>Returns the bytes before and after the empty onlineState object.</summary>
    public static (ReadOnlyMemory<byte> Before, ReadOnlyMemory<byte> After) SplitAtEmptyOnlineState(
        byte[] canonical)
    {
        ArgumentNullException.ThrowIfNull(canonical);
        var reader = new Utf8JsonReader(canonical);
        var isStateValue = false;
        var valueStart = -1;
        while (reader.Read())
        {
            if (isStateValue && reader.TokenType == JsonTokenType.StartObject)
            {
                var offset = checked((int)reader.TokenStartIndex);
                if (canonical.AsSpan(offset).StartsWith("{}"u8))
                {
                    if (valueStart != -1)
                    {
                        throw new ArgumentException(
                            "Canonical checkpoint JSON has more than one empty onlineState member.", nameof(canonical));
                    }

                    valueStart = offset;
                }
            }

            isStateValue = reader.TokenType == JsonTokenType.PropertyName && reader.ValueTextEquals("onlineState"u8);
        }

        if (valueStart == -1)
        {
            throw new ArgumentException(
                "Canonical checkpoint JSON must contain an empty onlineState member.", nameof(canonical));
        }

        return (canonical.AsMemory(0, valueStart), canonical.AsMemory(valueStart + 2));
    }

    private static void RequireEmptyOnlineState(CheckpointContent shell)
    {
        ValidateContent(shell);
        if (shell.OnlineState.EnumerateObject().Any())
        {
            throw new ArgumentException("A checkpoint shell must have an empty online state.", nameof(shell));
        }
    }

    private static Sha256Hex HashCanonicalContent(
        ReadOnlyMemory<byte> before,
        IReadOnlyList<ReadOnlyMemory<byte>> parts,
        ReadOnlyMemory<byte> after)
    {
        var length = (long)before.Length + after.Length;
        foreach (var part in parts)
        {
            length += part.Length;
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(HashDomain);
        var tag = "canonicalCheckpointContent"u8;
        Span<byte> header = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt16BigEndian(header, (ushort)tag.Length);
        hash.AppendData(header[..sizeof(ushort)]);
        hash.AppendData(tag);
        BinaryPrimitives.WriteUInt64BigEndian(header, (ulong)length);
        hash.AppendData(header);
        hash.AppendData(before.Span);
        foreach (var part in parts)
        {
            hash.AppendData(part.Span);
        }

        hash.AppendData(after.Span);
        Sha256Hex.TryCreate(Convert.ToHexStringLower(hash.GetHashAndReset()), out var result);
        return result!;
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
