using System.Text;

namespace RideBound.Runner.Protocol;

public enum NdjsonReadKind
{
    Message,
    EndOfStream,
    Error,
}

public sealed record NdjsonReadResult(
    NdjsonReadKind Kind,
    byte[]? Utf8Json = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public sealed class NdjsonReader
{
    public const int DefaultMaximumLineBytes = 1_048_576;

    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private readonly Stream _stream;
    private readonly int _maximumLineBytes;
    private readonly byte[] _readBuffer = new byte[8192];
    private int _readOffset;
    private int _readCount;

    public NdjsonReader(
        Stream stream,
        int maximumLineBytes = DefaultMaximumLineBytes)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanRead)
        {
            throw new ArgumentException("NDJSON stream must be readable.", nameof(stream));
        }

        if (maximumLineBytes < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumLineBytes));
        }

        _stream = stream;
        _maximumLineBytes = maximumLineBytes;
    }

    public async ValueTask<NdjsonReadResult> ReadAsync(
        CancellationToken cancellationToken = default)
    {
        var line = new List<byte>();
        var tooLarge = false;

        while (true)
        {
            var next = await ReadByteAsync(cancellationToken).ConfigureAwait(false);

            if (next is null)
            {
                if (line.Count == 0 && !tooLarge)
                {
                    return new NdjsonReadResult(NdjsonReadKind.EndOfStream);
                }

                return new NdjsonReadResult(
                    NdjsonReadKind.Error,
                    ErrorCode: "INCOMPLETE_FRAME_EOF",
                    ErrorMessage: "Input ended before the NDJSON line-feed delimiter.");
            }

            if (next.Value == (byte)'\n')
            {
                if (tooLarge)
                {
                    return new NdjsonReadResult(
                        NdjsonReadKind.Error,
                        ErrorCode: "MESSAGE_TOO_LARGE",
                        ErrorMessage: $"NDJSON message exceeds {_maximumLineBytes} bytes.");
                }

                if (line.Count > 0 && line[^1] == (byte)'\r')
                {
                    line.RemoveAt(line.Count - 1);
                }

                var bytes = line.ToArray();

                try
                {
                    _ = StrictUtf8.GetString(bytes);
                }
                catch (DecoderFallbackException)
                {
                    return new NdjsonReadResult(
                        NdjsonReadKind.Error,
                        ErrorCode: "MALFORMED_UTF8",
                        ErrorMessage: "NDJSON message is not valid UTF-8.");
                }

                return new NdjsonReadResult(NdjsonReadKind.Message, bytes);
            }

            if (!tooLarge)
            {
                if (line.Count == _maximumLineBytes)
                {
                    tooLarge = true;
                    line.Clear();
                }
                else
                {
                    line.Add(next.Value);
                }
            }
        }
    }

    private async ValueTask<byte?> ReadByteAsync(CancellationToken cancellationToken)
    {
        if (_readOffset >= _readCount)
        {
            _readCount = await _stream.ReadAsync(
                _readBuffer.AsMemory(),
                cancellationToken).ConfigureAwait(false);
            _readOffset = 0;

            if (_readCount == 0)
            {
                return null;
            }
        }

        return _readBuffer[_readOffset++];
    }
}
