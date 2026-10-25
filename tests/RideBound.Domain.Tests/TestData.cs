using RideBound.Domain.Common;
using RideBound.Domain.Requests;

namespace RideBound.Domain.Tests;

internal static class TestData
{
    public static readonly VehicleId VehicleOne = new("v-1");

    public static readonly VehicleId VehicleTwo = new("v-2");

    public static readonly RequestId RequestOne = new("r-1");

    public static readonly NodeId NodeZero = new("n-0");

    public static readonly NodeId NodeOne = new("n-1");

    public static readonly NodeId NodeTwo = new("n-2");

    public static RideRequest PendingRequest(
        RequestId? id = null,
        long partySize = 1,
        long earliestPickupMs = 1000,
        long latestPickupMs = 2000,
        long maxRideTimeMs = 1000) =>
        RideRequest.CreatePending(
            id ?? RequestOne,
            new SimTime(1000),
            NodeOne,
            NodeTwo,
            new SimTime(earliestPickupMs),
            new SimTime(latestPickupMs),
            new Duration(maxRideTimeMs),
            partySize,
            "standard",
            "uniform-v1").Value!;
}
