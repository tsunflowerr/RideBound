using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using RideBound.Contracts.Protocol;

namespace RideBound.Contracts.Serialization;

public static class ProtocolHash
{
    private static readonly byte[] ManifestDomain =
        "RideBound.ManifestHash.v1\0"u8.ToArray();

    private static readonly byte[] DecisionDomain =
        "RideBound.DecisionHash.v1\0"u8.ToArray();

    private static readonly byte[] StateIdentityDomain =
        "RideBound.StateIdentityHash.v1\0"u8.ToArray();

    public static Sha256Hex CalculateManifestHash(RunManifestIdentity manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var encoded = ProtocolPayloadReader.Write(
            writer => RunManifestIdentityCodec.Write(writer, manifest));
        var canonical = CanonicalJson.Canonicalize(encoded);

        return Calculate(
            ManifestDomain,
            [new HashFrame("canonicalManifest", canonical)]);
    }

    public static Sha256Hex CalculateDecisionHash(
        Sha256Hex previousDecisionHash,
        Sha256Hex manifestHash,
        string policyVersion,
        ReadOnlySpan<byte> canonicalInputState,
        ReadOnlySpan<byte> canonicalDecision)
    {
        ArgumentNullException.ThrowIfNull(previousDecisionHash);
        ArgumentNullException.ThrowIfNull(manifestHash);

        if (!OpaqueIdentifier.IsValid(policyVersion))
        {
            throw new ArgumentException(
                "policyVersion must be an opaque protocol identifier.",
                nameof(policyVersion));
        }

        return Calculate(
            DecisionDomain,
            [
                new HashFrame(
                    "previousDecisionHash",
                    Convert.FromHexString(previousDecisionHash.Value)),
                new HashFrame(
                    "manifestHash",
                    Convert.FromHexString(manifestHash.Value)),
                new HashFrame(
                    "policyVersion",
                    Encoding.UTF8.GetBytes(policyVersion)),
                new HashFrame(
                    "canonicalInputState",
                    canonicalInputState.ToArray()),
                new HashFrame(
                    "canonicalDecision",
                    canonicalDecision.ToArray()),
            ]);
    }

    public static Sha256Hex CalculateStateIdentityHash(
        EpochId epochId,
        EventSequence nextEventSequence,
        SimulationTimeMilliseconds simTime)
    {
        var encoded = ProtocolPayloadReader.Write(
            writer =>
            {
                writer.WriteStartObject();
                writer.WriteNumber("epochId", epochId.Value);
                writer.WriteNumber("nextEventSeq", nextEventSequence.Value);
                writer.WriteNumber("simTimeMs", simTime.Value);
                writer.WriteEndObject();
            });
        var canonical = CanonicalJson.Canonicalize(encoded);

        return Calculate(
            StateIdentityDomain,
            [new HashFrame("canonicalStateIdentity", canonical)]);
    }

    public static Sha256Hex ZeroHash { get; } = CreateHex(new byte[32]);

    private static Sha256Hex Calculate(
        ReadOnlySpan<byte> domain,
        IReadOnlyList<HashFrame> frames)
    {
        using var incremental = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        incremental.AppendData(domain);

        foreach (var frame in frames)
        {
            AppendFrame(incremental, frame.Tag, frame.Value);
        }

        return CreateHex(incremental.GetHashAndReset());
    }

    private static void AppendFrame(
        IncrementalHash hash,
        string tag,
        ReadOnlySpan<byte> value)
    {
        var tagBytes = Encoding.UTF8.GetBytes(tag);

        if (tagBytes.Length > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tag),
                "Hash frame tag is too long.");
        }

        Span<byte> tagLength = stackalloc byte[sizeof(ushort)];
        BinaryPrimitives.WriteUInt16BigEndian(tagLength, (ushort)tagBytes.Length);
        hash.AppendData(tagLength);
        hash.AppendData(tagBytes);

        Span<byte> valueLength = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64BigEndian(valueLength, (ulong)value.Length);
        hash.AppendData(valueLength);
        hash.AppendData(value);
    }

    private static Sha256Hex CreateHex(ReadOnlySpan<byte> hash)
    {
        var text = Convert.ToHexStringLower(hash);
        Sha256Hex.TryCreate(text, out var result);
        return result!;
    }

    private sealed record HashFrame(string Tag, byte[] Value);
}
