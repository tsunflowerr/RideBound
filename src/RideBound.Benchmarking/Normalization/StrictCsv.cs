using System.Text;

namespace RideBound.Benchmarking.Normalization;

internal static class StrictCsv
{
    public static IEnumerable<CsvRow> Read(
        string path,
        IReadOnlyList<string> expectedHeader)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            64 * 1024,
            FileOptions.SequentialScan);
        Span<byte> prefix = stackalloc byte[3];
        var prefixLength = stream.Read(prefix);

        if (prefixLength == 3
            && prefix[0] == 0xef
            && prefix[1] == 0xbb
            && prefix[2] == 0xbf)
        {
            throw new InvalidDataException("CSV input must be UTF-8 without BOM.");
        }

        stream.Position = 0;
        using var reader = new StreamReader(
            stream,
            new UTF8Encoding(false, true),
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 64 * 1024,
            leaveOpen: false);
        var headerLine = reader.ReadLine()
            ?? throw new InvalidDataException("CSV input is empty.");
        var header = ParseLine(headerLine);

        if (!header.SequenceEqual(expectedHeader, StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                $"CSV header does not match the registered member contract: '{headerLine}'.");
        }

        long ordinal = 0;
        string? line;

        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0)
            {
                yield return new CsvRow(ordinal++, line, null, "csv.empty-row");
                continue;
            }

            IReadOnlyList<string>? fields = null;
            string? error = null;

            try
            {
                fields = ParseLine(line);
            }
            catch (InvalidDataException exception)
            {
                error = exception.Message;
            }

            yield return new CsvRow(ordinal++, line, fields, error);
        }
    }

    private static IReadOnlyList<string> ParseLine(string line)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var quoted = false;
        var quoteClosed = false;

        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];

            if (inQuotes)
            {
                if (character != '"')
                {
                    field.Append(character);
                    continue;
                }

                if (index + 1 < line.Length && line[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                    continue;
                }

                inQuotes = false;
                quoteClosed = true;
                continue;
            }

            if (character == ',')
            {
                fields.Add(field.ToString());
                field.Clear();
                quoted = false;
                quoteClosed = false;
                continue;
            }

            if (character == '"')
            {
                if (field.Length != 0 || quoted || quoteClosed)
                {
                    throw new InvalidDataException("CSV quote appears outside a field boundary.");
                }

                inQuotes = true;
                quoted = true;
                continue;
            }

            if (quoteClosed)
            {
                throw new InvalidDataException(
                    "CSV quoted field has trailing characters before the delimiter.");
            }

            field.Append(character);
        }

        if (inQuotes)
        {
            throw new InvalidDataException("CSV quoted field is not terminated.");
        }

        fields.Add(field.ToString());
        return fields;
    }
}

internal sealed record CsvRow(
    long Ordinal,
    string RawLine,
    IReadOnlyList<string>? Fields,
    string? ParseError);
