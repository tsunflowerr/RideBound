namespace RideBound.Benchmarking.Execution;

internal sealed class StreamLimitExceededException(string code, string message)
    : IOException(message)
{
    public string Code { get; } = code;
}

internal sealed class BoundedRecordingReadStream(
    Stream source,
    Stream capture,
    long maximumBytes,
    string failureCode) : Stream
{
    private long bytes;

    public long Bytes => Interlocked.Read(ref bytes);

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = source.Read(buffer, offset, count);
        Record(buffer.AsSpan(offset, read));
        return read;
    }

    public override int Read(Span<byte> buffer)
    {
        var read = source.Read(buffer);
        Record(buffer[..read]);
        return read;
    }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        var read = await source.ReadAsync(buffer, cancellationToken);
        await RecordAsync(buffer[..read], cancellationToken);
        return read;
    }

    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        var read = await source.ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
        await RecordAsync(buffer.AsMemory(offset, read), cancellationToken);
        return read;
    }

    private void Record(ReadOnlySpan<byte> value)
    {
        if (value.Length == 0)
        {
            return;
        }

        var total = checked(Interlocked.Add(ref bytes, value.Length));
        var previous = total - value.Length;
        var allowed = checked((int)Math.Clamp(maximumBytes - previous, 0, value.Length));

        if (allowed > 0)
        {
            capture.Write(value[..allowed]);
        }

        if (total > maximumBytes)
        {
            throw new StreamLimitExceededException(
                failureCode,
                $"Stream exceeded its declared {maximumBytes}-byte limit.");
        }

    }

    private async ValueTask RecordAsync(
        ReadOnlyMemory<byte> value,
        CancellationToken cancellationToken)
    {
        if (value.Length == 0)
        {
            return;
        }

        var total = checked(Interlocked.Add(ref bytes, value.Length));
        var previous = total - value.Length;
        var allowed = checked((int)Math.Clamp(maximumBytes - previous, 0, value.Length));

        if (allowed > 0)
        {
            await capture.WriteAsync(value[..allowed], cancellationToken);
        }

        if (total > maximumBytes)
        {
            throw new StreamLimitExceededException(
                failureCode,
                $"Stream exceeded its declared {maximumBytes}-byte limit.");
        }

    }

    public override void Flush() => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) =>
        throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            source.Dispose();
            capture.Dispose();
        }

        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        await source.DisposeAsync();
        await capture.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}

internal sealed class BoundedRecordingWriteStream(
    Stream destination,
    Stream capture,
    long maximumBytes) : Stream
{
    private long bytes;

    public long Bytes => Interlocked.Read(ref bytes);

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        EnsureAllowed(count);
        destination.Write(buffer, offset, count);
        capture.Write(buffer, offset, count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        EnsureAllowed(buffer.Length);
        destination.Write(buffer);
        capture.Write(buffer);
    }

    public override async ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        EnsureAllowed(buffer.Length);
        await destination.WriteAsync(buffer, cancellationToken);
        await capture.WriteAsync(buffer, cancellationToken);
    }

    public override async Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        EnsureAllowed(count);
        await destination.WriteAsync(buffer.AsMemory(offset, count), cancellationToken);
        await capture.WriteAsync(buffer.AsMemory(offset, count), cancellationToken);
    }

    private void EnsureAllowed(int count)
    {
        var total = checked(Interlocked.Add(ref bytes, count));

        if (total > maximumBytes)
        {
            throw new StreamLimitExceededException(
                "resource.stdin-bytes-exceeded",
                $"Standard input exceeded its declared {maximumBytes}-byte limit.");
        }
    }

    public override void Flush()
    {
        destination.Flush();
        capture.Flush();
    }

    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        await destination.FlushAsync(cancellationToken);
        await capture.FlushAsync(cancellationToken);
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) =>
        throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            destination.Dispose();
            capture.Dispose();
        }

        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        await destination.DisposeAsync();
        await capture.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
