using RideBound.Contracts.Protocol;

namespace RideBound.Runner.Protocol;

/// <summary>Writes a segmented NDJSON response.</summary>
public sealed class StreamedResponse
{
    private readonly IReadOnlyList<ReadOnlyMemory<byte>> _parts;
    private ProtocolEnvelope? _envelope;

    public StreamedResponse(string messageType, IReadOnlyList<ReadOnlyMemory<byte>> parts)
    {
        ArgumentException.ThrowIfNullOrEmpty(messageType);
        ArgumentNullException.ThrowIfNull(parts);
        MessageType = messageType;
        _parts = parts;
    }

    public string MessageType { get; }

    public async ValueTask WriteToAsync(Stream stream, CancellationToken cancellationToken)
    {
        foreach (var part in _parts)
        {
            await stream.WriteAsync(part, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Decodes the stored segments into a protocol envelope.</summary>
    public ProtocolEnvelope ToEnvelope()
    {
        if (_envelope is not null)
        {
            return _envelope;
        }

        var bytes = new byte[_parts.Sum(part => (long)part.Length)];
        var offset = 0;

        foreach (var part in _parts)
        {
            part.Span.CopyTo(bytes.AsSpan(offset));
            offset += part.Length;
        }

        var decoded = ProtocolEnvelopeCodec.Decode(bytes);
        _envelope = decoded.Envelope
            ?? throw new InvalidOperationException("A streamed response is not a protocol envelope.");
        return _envelope;
    }
}
