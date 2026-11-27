namespace RideBound.Runner.StateAudit;

/// <summary>Ordered canonical JSON byte ranges and their combined length.</summary>
internal sealed class CanonicalSegments(IReadOnlyList<ReadOnlyMemory<byte>> parts, long length)
{
    public IReadOnlyList<ReadOnlyMemory<byte>> Parts { get; } = parts;
    public long Length { get; } = length;

    public byte[] ToArray()
    {
        var result = GC.AllocateUninitializedArray<byte>(checked((int)Length));
        var offset = 0;

        foreach (var part in Parts)
        {
            part.Span.CopyTo(result.AsSpan(offset));
            offset += part.Length;
        }

        return result;
    }

    public bool ContentEquals(CanonicalSegments other)
    {
        if (Length != other.Length)
        {
            return false;
        }

        var otherIndex = 0;
        var otherOffset = 0;
        foreach (var part in Parts)
        {
            var remaining = part.Span;
            while (!remaining.IsEmpty)
            {
                var comparison = other.Parts[otherIndex].Span[otherOffset..];
                var count = Math.Min(remaining.Length, comparison.Length);
                if (!remaining[..count].SequenceEqual(comparison[..count]))
                {
                    return false;
                }

                remaining = remaining[count..];
                otherOffset += count;
                if (otherOffset == other.Parts[otherIndex].Length)
                {
                    otherIndex++;
                    otherOffset = 0;
                }
            }
        }

        return true;
    }

    public bool ContentEquals(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != Length)
        {
            return false;
        }

        foreach (var part in Parts)
        {
            if (!part.Span.SequenceEqual(bytes[..part.Length]))
            {
                return false;
            }

            bytes = bytes[part.Length..];
        }

        return true;
    }
}

/// <summary>
/// Collects structural JSON and canonical fragments in output order. Structural ranges are
/// resolved once the byte buffer has reached its final size.
/// </summary>
internal sealed class CanonicalSegmentBuilder
{
    private readonly List<(ReadOnlyMemory<byte> Fragment, int Offset, int GlueLength)> _pending = [];
    private int _glueStart;
    private long _length;

    public CanonicalBuffer Glue { get; } = new();

    public void Fragment(ReadOnlyMemory<byte> fragment)
    {
        FlushGlue();
        _pending.Add((fragment, 0, 0));
        _length += fragment.Length;
    }

    public CanonicalSegments Build()
    {
        FlushGlue();
        var glue = Glue.Array;
        var parts = new ReadOnlyMemory<byte>[_pending.Count];
        for (var index = 0; index < parts.Length; index++)
        {
            var (fragment, offset, glueLength) = _pending[index];
            parts[index] = glueLength == 0 ? fragment : glue.AsMemory(offset, glueLength);
        }

        return new CanonicalSegments(parts, _length);
    }

    private void FlushGlue()
    {
        var length = Glue.Length - _glueStart;
        if (length > 0)
        {
            _pending.Add((default, _glueStart, length));
            _length += length;
            _glueStart = Glue.Length;
        }
    }
}
