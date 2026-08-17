using RideBound.Benchmarking.EndToEnd;

namespace RideBound.Benchmarking.Tests;

public sealed class MechanicalPairedHarnessPreflightTests
{
    [Fact]
    public void Claims_pins_and_sources_must_be_sorted_unique_and_rooted()
    {
        using var temp = new TestDirectory();
        var first = Path.Combine(temp.Root, "a.json");
        var second = Path.Combine(temp.Root, "b.json");
        var claims = new[] { "claim-a", "claim-b" };
        var pins = new[] { first, second };
        var sources = new[]
        {
            new MechanicalHarnessBundleSource(
                first,
                "data/scenarios/a/source/a.json",
                "application/json"),
            new MechanicalHarnessBundleSource(
                second,
                "data/scenarios/a/source/b.json",
                "application/json"),
        };

        TinyPairedHarness.ValidateProfileCollections(claims, pins, sources);

        Assert.Throws<ArgumentException>(
            () => TinyPairedHarness.ValidateProfileCollections(
                claims.Reverse().ToArray(),
                pins,
                sources));
        Assert.Throws<ArgumentException>(
            () => TinyPairedHarness.ValidateProfileCollections(
                claims,
                pins.Reverse().ToArray(),
                sources));
        Assert.Throws<ArgumentException>(
            () => TinyPairedHarness.ValidateProfileCollections(
                claims,
                pins,
                sources.Reverse().ToArray()));
        Assert.Throws<ArgumentException>(
            () => TinyPairedHarness.ValidateProfileCollections(
                claims,
                [first, first.ToUpperInvariant()],
                sources));
        Assert.Throws<ArgumentException>(
            () => TinyPairedHarness.ValidateProfileCollections(
                claims,
                pins,
                [sources[0], sources[1] with { RelativePath = sources[0].RelativePath }]));
    }
}
