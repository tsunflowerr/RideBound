using RideBound.Domain.Commitments;
using RideBound.Domain.Common;

namespace RideBound.Domain.Tests.Commitments;

public sealed class CommitmentVectorTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(DomainLimits.MaxCanonicalInteger + 1)]
    public void Vector_rejects_values_outside_canonical_range(long value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CommitmentVector(
                value,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0));
    }
}
