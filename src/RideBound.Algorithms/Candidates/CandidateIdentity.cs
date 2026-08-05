using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using RideBound.Application.State;
using RideBound.Domain.Common;
using RideBound.Domain.Routes;

namespace RideBound.Algorithms.Candidates;

internal static class CandidateIdentity
{
    private static readonly byte[] CandidateDomain =
        "RideBound.InsertionCandidate.v1\0"u8.ToArray();

    private static readonly byte[] StopDomain =
        "RideBound.GeneratedStop.v1\0"u8.ToArray();

    private static readonly byte[] RouteDomain =
        "RideBound.ScheduleRoute.v1\0"u8.ToArray();

    private static readonly byte[] HoldStopDomain =
        "RideBound.OriginHoldStop.v1\0"u8.ToArray();

    private static readonly byte[] OmissionDomain =
        "RideBound.CandidateOmission.v1\0"u8.ToArray();

    private static readonly byte[] SearchNodeDomain =
        "RideBound.CandidateSearchNode.v1\0"u8.ToArray();

    public static string Create(
        OnlineState state,
        VehicleId vehicleId,
        RoutePlan route,
        IReadOnlyList<RequestId> newRequestIds)
    {
        var buffer = new ArrayBufferWriter<byte>();
        buffer.Write(CandidateDomain);
        WriteFrame(buffer, "vehicleId", vehicleId.Value);
        WriteFrame(
            buffer,
            "evaluationTimeMs",
            state.Run.SimulationTime.Milliseconds.ToString(
                System.Globalization.CultureInfo.InvariantCulture));
        WriteFrame(
            buffer,
            "travelSnapshotHash",
            state.TravelTimes?.SnapshotHash ?? string.Empty);
        WriteFrame(
            buffer,
            "planVersion",
            route.Version.Value.ToString(
                System.Globalization.CultureInfo.InvariantCulture));
        WriteFrame(
            buffer,
            "executedStopCount",
            route.ExecutedStopCount.ToString(
                System.Globalization.CultureInfo.InvariantCulture));

        foreach (var requestId in newRequestIds.OrderBy(
                     value => value.Value,
                     StringComparer.Ordinal))
        {
            WriteFrame(buffer, "newRequestId", requestId.Value);
        }

        foreach (var stop in route.FrozenPrefix)
        {
            WriteStop(buffer, "frozenStop", stop);
        }

        foreach (var stop in route.MutableSuffix)
        {
            WriteStop(buffer, "mutableStop", stop);
        }

        return $"candidate-v1-{Convert.ToHexStringLower(
            SHA256.HashData(buffer.WrittenSpan))}";
    }

    public static StopId CreateStopId(RequestId requestId, RouteStopKind kind)
    {
        var buffer = new ArrayBufferWriter<byte>();
        buffer.Write(StopDomain);
        WriteFrame(
            buffer,
            "kind",
            kind == RouteStopKind.Pickup ? "pickup" : "dropOff");
        WriteFrame(buffer, "requestId", requestId.Value);
        var prefix = kind == RouteStopKind.Pickup ? "rb-p-" : "rb-d-";
        return new StopId(
            $"{prefix}{Convert.ToHexStringLower(
                SHA256.HashData(buffer.WrittenSpan))}");
    }

    public static string CreateRouteFingerprint(RoutePlan route)
    {
        ArgumentNullException.ThrowIfNull(route);
        var buffer = new ArrayBufferWriter<byte>();
        buffer.Write(RouteDomain);
        WriteFrame(
            buffer,
            "planVersion",
            route.Version.Value.ToString(
                System.Globalization.CultureInfo.InvariantCulture));
        WriteFrame(
            buffer,
            "executedStopCount",
            route.ExecutedStopCount.ToString(
                System.Globalization.CultureInfo.InvariantCulture));

        foreach (var stop in route.FrozenPrefix)
        {
            WriteStop(buffer, "frozenStop", stop);
        }

        foreach (var stop in route.MutableSuffix)
        {
            WriteStop(buffer, "mutableStop", stop);
        }

        return Convert.ToHexStringLower(SHA256.HashData(buffer.WrittenSpan));
    }

    public static StopId CreateHoldStopId(string sourceCandidateId)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourceCandidateId);
        var buffer = new ArrayBufferWriter<byte>();
        buffer.Write(HoldStopDomain);
        WriteFrame(buffer, "sourceCandidateId", sourceCandidateId);
        return new StopId(
            $"rb-h-{Convert.ToHexStringLower(
                SHA256.HashData(buffer.WrittenSpan))}");
    }

    public static string CreateOmissionDigest(IEnumerable<string> stableIds)
    {
        ArgumentNullException.ThrowIfNull(stableIds);
        var buffer = new ArrayBufferWriter<byte>();
        buffer.Write(OmissionDomain);

        foreach (var stableId in stableIds.Order(StringComparer.Ordinal))
        {
            WriteFrame(buffer, "omittedId", stableId);
        }

        return Convert.ToHexStringLower(SHA256.HashData(buffer.WrittenSpan));
    }

    public static string CreateSearchNodeDigest(IEnumerable<string> orderedTokens)
    {
        ArgumentNullException.ThrowIfNull(orderedTokens);
        var buffer = new ArrayBufferWriter<byte>();
        buffer.Write(SearchNodeDomain);

        foreach (var token in orderedTokens)
        {
            WriteFrame(buffer, "orderedToken", token);
        }

        return Convert.ToHexStringLower(SHA256.HashData(buffer.WrittenSpan));
    }

    private static void WriteStop(
        IBufferWriter<byte> writer,
        string tag,
        RouteStop stop)
    {
        WriteFrame(writer, tag, stop.StopId.Value);
        WriteFrame(writer, "nodeId", stop.NodeId.Value);
        WriteFrame(writer, "kind", stop.Kind.ToString());
        WriteFrame(writer, "requestId", stop.RequestId?.Value ?? string.Empty);
        WriteFrame(
            writer,
            "serviceDurationMs",
            stop.ServiceDuration.Milliseconds.ToString(
                System.Globalization.CultureInfo.InvariantCulture));
    }

    private static void WriteFrame(
        IBufferWriter<byte> writer,
        string tag,
        string value)
    {
        var tagBytes = Encoding.UTF8.GetBytes(tag);
        var valueBytes = Encoding.UTF8.GetBytes(value);
        var header = writer.GetSpan(sizeof(ushort));
        BinaryPrimitives.WriteUInt16BigEndian(header, checked((ushort)tagBytes.Length));
        writer.Advance(sizeof(ushort));
        writer.Write(tagBytes);
        header = writer.GetSpan(sizeof(ulong));
        BinaryPrimitives.WriteUInt64BigEndian(header, (ulong)valueBytes.Length);
        writer.Advance(sizeof(ulong));
        writer.Write(valueBytes);
    }
}
