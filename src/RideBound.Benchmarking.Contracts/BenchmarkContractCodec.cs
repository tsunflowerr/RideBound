using System.Text.Json;
using System.Text.Json.Serialization;
using RideBound.Contracts.Serialization;

namespace RideBound.Benchmarking.Contracts;

public enum BenchmarkContractErrorCode
{
    MalformedJson,
    DuplicateProperty,
    InvalidUnicode,
    NullNotAllowed,
    NonIntegerNumber,
    IntegerOutOfRange,
    MissingRequiredField,
    UnknownField,
    InvalidFieldType,
    InvalidValue,
    ConditionalFieldViolation,
    SelfHashForbidden,
}

public sealed record BenchmarkContractError(
    BenchmarkContractErrorCode Code,
    string Path,
    string Message);

public sealed record BenchmarkDecodeResult<T>
    where T : class, IBenchmarkDocument
{
    private BenchmarkDecodeResult(
        T? value,
        byte[]? canonicalBytes,
        BenchmarkContractError? error)
    {
        Value = value;
        CanonicalBytes = canonicalBytes;
        Error = error;
    }

    public bool IsSuccess => Value is not null;

    public T? Value { get; }

    public byte[]? CanonicalBytes { get; }

    public BenchmarkContractError? Error { get; }

    internal static BenchmarkDecodeResult<T> Success(T value, byte[] canonicalBytes) =>
        new(value, canonicalBytes, null);

    internal static BenchmarkDecodeResult<T> Failure(BenchmarkContractError error) =>
        new(null, null, error);
}

public static class BenchmarkContractCodec
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static BenchmarkDecodeResult<T> Decode<T>(ReadOnlySpan<byte> utf8Json)
        where T : class, IBenchmarkDocument
    {
        EnsureSupportedDocumentType(typeof(T));

        byte[] canonical;

        try
        {
            canonical = CanonicalJson.Canonicalize(utf8Json);
        }
        catch (CanonicalJsonException exception)
        {
            return BenchmarkDecodeResult<T>.Failure(MapCanonicalError(exception));
        }

        try
        {
            using var document = JsonDocument.Parse(canonical);
            var forbiddenError = FindForbiddenSelfHash(typeof(T), document.RootElement);

            if (forbiddenError is not null)
            {
                return BenchmarkDecodeResult<T>.Failure(forbiddenError);
            }

            var value = JsonSerializer.Deserialize<T>(canonical, Options);

            if (value is null)
            {
                return BenchmarkDecodeResult<T>.Failure(
                    new BenchmarkContractError(
                        BenchmarkContractErrorCode.InvalidFieldType,
                        "$",
                        "Benchmark document must be a JSON object."));
            }

            var validationError = BenchmarkContractValidator.Validate(value);

            return validationError is null
                ? BenchmarkDecodeResult<T>.Success(value, canonical)
                : BenchmarkDecodeResult<T>.Failure(validationError);
        }
        catch (JsonException exception)
        {
            return BenchmarkDecodeResult<T>.Failure(MapSerializerError(exception));
        }
        catch (NotSupportedException exception)
        {
            return BenchmarkDecodeResult<T>.Failure(
                new BenchmarkContractError(
                    BenchmarkContractErrorCode.InvalidValue,
                    "$",
                    exception.Message));
        }
    }

    public static byte[] Encode<T>(T value)
        where T : class, IBenchmarkDocument
    {
        ArgumentNullException.ThrowIfNull(value);
        EnsureSupportedDocumentType(typeof(T));

        var validationError = BenchmarkContractValidator.Validate(value);

        if (validationError is not null)
        {
            throw new ArgumentException(
                $"Invalid benchmark document at '{validationError.Path}': " +
                validationError.Message,
                nameof(value));
        }

        var encoded = JsonSerializer.SerializeToUtf8Bytes(value, Options);
        return CanonicalJson.Canonicalize(encoded);
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectRequiredConstructorParameters = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
        };
        options.Converters.Add(
            new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false));
        return options;
    }

    private static BenchmarkContractError? FindForbiddenSelfHash(
        Type type,
        JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var forbiddenField = type == typeof(ScenarioContent)
            ? "scenarioHash"
            : type == typeof(BenchmarkPlan)
                ? "planHash"
                : type == typeof(LogicalBundleManifest)
                    ? "bundleHash"
                    : null;

        return forbiddenField is not null && root.TryGetProperty(forbiddenField, out _)
            ? new BenchmarkContractError(
                BenchmarkContractErrorCode.SelfHashForbidden,
                $"$.{forbiddenField}",
                $"Self-referential field '{forbiddenField}' is forbidden.")
            : null;
    }

    private static BenchmarkContractError MapCanonicalError(
        CanonicalJsonException exception)
    {
        var code = exception.Code switch
        {
            CanonicalJsonErrorCode.MalformedJson =>
                BenchmarkContractErrorCode.MalformedJson,
            CanonicalJsonErrorCode.DuplicateProperty =>
                BenchmarkContractErrorCode.DuplicateProperty,
            CanonicalJsonErrorCode.InvalidUnicode =>
                BenchmarkContractErrorCode.InvalidUnicode,
            CanonicalJsonErrorCode.NullNotAllowed =>
                BenchmarkContractErrorCode.NullNotAllowed,
            CanonicalJsonErrorCode.NonIntegerNumber =>
                BenchmarkContractErrorCode.NonIntegerNumber,
            CanonicalJsonErrorCode.IntegerOutOfRange =>
                BenchmarkContractErrorCode.IntegerOutOfRange,
            _ => BenchmarkContractErrorCode.MalformedJson,
        };

        return new BenchmarkContractError(code, exception.Path, exception.Message);
    }

    private static BenchmarkContractError MapSerializerError(JsonException exception)
    {
        var message = exception.Message;
        var code = message.Contains("unmapped", StringComparison.OrdinalIgnoreCase)
            || message.Contains("could not be mapped", StringComparison.OrdinalIgnoreCase)
            ? BenchmarkContractErrorCode.UnknownField
            : message.Contains("required properties", StringComparison.OrdinalIgnoreCase)
                ? BenchmarkContractErrorCode.MissingRequiredField
                : message.Contains("JSON value could not be converted", StringComparison.Ordinal)
                    ? BenchmarkContractErrorCode.InvalidValue
                    : BenchmarkContractErrorCode.InvalidFieldType;

        return new BenchmarkContractError(code, exception.Path ?? "$", message);
    }

    private static void EnsureSupportedDocumentType(Type type)
    {
        if (type != typeof(DatasetDescriptor)
            && type != typeof(NormalizationReport)
            && type != typeof(ScenarioContent)
            && type != typeof(BenchmarkPlan)
            && type != typeof(RunRecord)
            && type != typeof(ObservationIndexRow)
            && type != typeof(FailureRecord)
            && type != typeof(ExclusionRecord)
            && type != typeof(MetricRow)
            && type != typeof(LogicalBundleManifest))
        {
            throw new NotSupportedException(
                $"Type '{type.FullName}' is not a WP6 benchmark document.");
        }
    }
}
