using RideBound.Domain.Common;

namespace RideBound.Domain.Validation;

public interface ITravelTimeLookup
{
    bool TryGetTravelTime(
        NodeId fromNodeId,
        NodeId toNodeId,
        out Duration travelTime);
}
