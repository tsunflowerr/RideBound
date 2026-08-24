using System.Buffers;
using System.Buffers.Text;
using System.Text;
using System.Text.Json;

namespace RideBound.Wp6MetricOracle;

internal static class OracleJson
{
    internal const long SafeIntegerMaximum = 9_007_199_254_740_991;

    public static byte[] Canonicalize(ReadOnlySpan<byte> utf8Json)
    {
        using var document = JsonDocument.Parse(
            utf8Json.ToArray(),
            new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
            });
        var output = new ArrayBufferWriter<byte>();
        WriteElement(output, document.RootElement);
        return output.WrittenSpan.ToArray();
    }

    public static JsonDocument ParseCanonical(ReadOnlySpan<byte> bytes)
    {
        var canonical = Canonicalize(bytes);

        if (!bytes.SequenceEqual(canonical))
        {
            throw new OracleException("oracle.input-invalid", "Evidence JSON is not canonical.");
        }

        return JsonDocument.Parse(bytes.ToArray());
    }

    public static IReadOnlyList<byte[]> ReadCanonicalLines(byte[] bytes, bool allowEmpty)
    {
        if (bytes.Length == 0)
        {
            return allowEmpty
                ? []
                : throw new OracleException("oracle.input-invalid", "NDJSON evidence is empty.");
        }

        if (bytes[^1] != (byte)'\n')
        {
            throw new OracleException("oracle.input-invalid", "NDJSON evidence is incomplete.");
        }

        var lines = new List<byte[]>();
        var offset = 0;

        while (offset < bytes.Length)
        {
            var length = bytes.AsSpan(offset).IndexOf((byte)'\n');

            if (length <= 0)
            {
                throw new OracleException("oracle.input-invalid", "NDJSON has an empty frame.");
            }

            var line = bytes.AsSpan(offset, length).ToArray();
            offset += length + 1;
            _ = ParseCanonical(line).DisposeAfterValidation();
            lines.Add(line);
        }

        return lines;
    }

    public static byte[] EncodeCanonical(Action<CanonicalObjectWriter> write)
    {
        var writer = new CanonicalObjectWriter();
        write(writer);
        return writer.Complete();
    }

    private static void WriteElement(ArrayBufferWriter<byte> output, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var properties = new List<JsonProperty>();
                var names = new HashSet<string>(StringComparer.Ordinal);

                foreach (var property in element.EnumerateObject())
                {
                    if (!names.Add(property.Name))
                    {
                        throw new OracleException(
                            "oracle.input-invalid",
                            "JSON contains a duplicate property.");
                    }

                    properties.Add(property);
                }

                properties.Sort(static (left, right) =>
                    StringComparer.Ordinal.Compare(left.Name, right.Name));
                Byte(output, (byte)'{');

                for (var index = 0; index < properties.Count; index++)
                {
                    if (index != 0)
                    {
                        Byte(output, (byte)',');
                    }

                    String(output, properties[index].Name);
                    Byte(output, (byte)':');
                    WriteElement(output, properties[index].Value);
                }

                Byte(output, (byte)'}');
                break;
            case JsonValueKind.Array:
                Byte(output, (byte)'[');
                var itemIndex = 0;

                foreach (var item in element.EnumerateArray())
                {
                    if (itemIndex++ != 0)
                    {
                        Byte(output, (byte)',');
                    }

                    WriteElement(output, item);
                }

                Byte(output, (byte)']');
                break;
            case JsonValueKind.String:
                String(output, element.GetString()!);
                break;
            case JsonValueKind.Number:
                var raw = element.GetRawText();

                if (raw.AsSpan().IndexOfAny('.', 'e', 'E') >= 0
                    || raw == "-0"
                    || !element.TryGetInt64(out var integer)
                    || integer is < -SafeIntegerMaximum or > SafeIntegerMaximum)
                {
                    throw new OracleException(
                        "oracle.input-invalid",
                        "JSON number is not a canonical safe integer.");
                }

                Integer(output, integer);
                break;
            case JsonValueKind.True:
                Ascii(output, "true"u8);
                break;
            case JsonValueKind.False:
                Ascii(output, "false"u8);
                break;
            default:
                throw new OracleException(
                    "oracle.input-invalid",
                    "Null and unsupported JSON tokens are forbidden.");
        }
    }

    private static void String(ArrayBufferWriter<byte> output, string value)
    {
        Byte(output, (byte)'"');

        foreach (var rune in value.EnumerateRunes())
        {
            switch (rune.Value)
            {
                case '"': Ascii(output, "\\\""u8); continue;
                case '\\': Ascii(output, "\\\\"u8); continue;
                case '\b': Ascii(output, "\\b"u8); continue;
                case '\t': Ascii(output, "\\t"u8); continue;
                case '\n': Ascii(output, "\\n"u8); continue;
                case '\f': Ascii(output, "\\f"u8); continue;
                case '\r': Ascii(output, "\\r"u8); continue;
            }

            if (rune.Value <= 0x1f)
            {
                const string digits = "0123456789abcdef";
                Span<byte> escaped = new byte[6];
                escaped[0] = (byte)'\\';
                escaped[1] = (byte)'u';
                escaped[2] = (byte)'0';
                escaped[3] = (byte)'0';
                escaped[4] = (byte)digits[(rune.Value >> 4) & 0xf];
                escaped[5] = (byte)digits[rune.Value & 0xf];
                Ascii(output, escaped);
                continue;
            }

            var target = output.GetSpan(4);
            output.Advance(rune.EncodeToUtf8(target));
        }

        Byte(output, (byte)'"');
    }

    private static void Integer(ArrayBufferWriter<byte> output, long value)
    {
        var target = output.GetSpan(20);

        if (!Utf8Formatter.TryFormat(value, target, out var written))
        {
            throw new InvalidOperationException("Cannot encode integer.");
        }

        output.Advance(written);
    }

    private static void Byte(ArrayBufferWriter<byte> output, byte value)
    {
        output.GetSpan(1)[0] = value;
        output.Advance(1);
    }

    private static void Ascii(ArrayBufferWriter<byte> output, ReadOnlySpan<byte> value)
    {
        value.CopyTo(output.GetSpan(value.Length));
        output.Advance(value.Length);
    }

    internal sealed class CanonicalObjectWriter
    {
        private readonly SortedDictionary<string, object> values =
            new(StringComparer.Ordinal);

        public void Add(string name, string value) => values.Add(name, value);

        public void Add(string name, long value) => values.Add(name, value);

        public void Add(string name, byte[] canonicalJson) => values.Add(name, canonicalJson);

        public byte[] Complete()
        {
            var output = new ArrayBufferWriter<byte>();
            Byte(output, (byte)'{');
            var index = 0;

            foreach (var (name, value) in values)
            {
                if (index++ != 0)
                {
                    Byte(output, (byte)',');
                }

                String(output, name);
                Byte(output, (byte)':');

                if (value is string text)
                {
                    String(output, text);
                }
                else if (value is long integer)
                {
                    Integer(output, integer);
                }
                else
                {
                    Ascii(output, (byte[])value);
                }
            }

            Byte(output, (byte)'}');
            return output.WrittenSpan.ToArray();
        }
    }

    private static bool DisposeAfterValidation(this JsonDocument document)
    {
        document.Dispose();
        return true;
    }
}
