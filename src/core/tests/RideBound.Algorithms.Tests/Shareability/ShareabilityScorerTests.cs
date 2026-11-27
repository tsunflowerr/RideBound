using RideBound.Algorithms.Shareability;

namespace RideBound.Algorithms.Tests.Shareability;

public sealed class ShareabilityScorerTests
{
    [Fact]
    public void CausalScoreMatchesToyGoldenForPartialArcAndFullVehicle()
    {
        // Cell 0 of the dev toy golden fixture, seed 20261103.
        var network = ShareabilityNetwork.Create(
            4,
            [0, 50, 2, 95, 162, 0, 93, 117, 138, 146, 0, 93, 45, 53, 47, 0],
            [0, 2, 4, 5, 7],
            [1, 2, 2, 3, 3, 0, 1],
            [50, 2, 93, 117, 93, 45, 53],
            [0, 1, 1, 1],
            2);
        var rates = ZoneDemandRates.Create(
            network,
            [5317569, 6155189, 1102433],
            [0, 1, 0],
            [1, 1, 0],
            [0, 0],
            [0, 0],
            0.15596026836428792, 0.1717389328405261, 322, 600000);
        var scorer = new ShareabilityScorer(network, new ShareabilityCell(
            0.5614830804988742,
            [1.142757113999687, 1.1250604558270425, 1.2178139725700021],
            [1, 0, 2, 3],
            600000, 30000, rates, Oracle: false));
        Assert.Equal(1, rates.OriginFallbacks);
        Assert.Equal(1, rates.DestinationFallbacks);
        var causal = rates.Causal(1);
        var oracle = rates.Oracle(1);
        AssertBits(0.009260418875880229, causal.Origin[0]);
        AssertBits(0.009260418875880227, causal.Destination[1]);
        AssertBits(0.0016666666666666668, oracle.Origin[0]);
        AssertBits(0, oracle.Destination[1]);

        var partialArc = new ShareabilityContext(
            4682107.886900568, 5, 1, 4682107.886900568, new ShareabilityArc(0, 470), 2, 5,
            [
                new(1, true, 5576504.546169656, 572343, 1),
                new(3, false, 6472220.40645252, 210651, 1),
                new(1, false, 7329775.40645252, 757321, 1),
            ],
            0, 2, 0);
        var partialArcExpected = new ShareabilityScore(43.21528808708222, 21.59172056073419, 21.623567526348033, 3, 0, 2, 3, 0);

        var fullVehicle = new ShareabilityContext(
            2951418.191444971, 3, 0, 2979791.191444971, new ShareabilityArc(1, 39), 5, 5,
            [
                new(2, true, 3139191.191444971, 0, 2),
                new(2, false, 3789530.191444971, 0, 2),
                new(0, false, 4482679.191444971, 320719.868539331, 1),
                new(3, false, 4538638.191444971, 518751.7568237856, 2),
                new(1, false, 4821307.191444971, 337825.10847925395, 2),
            ],
            0, 1, 0);
        var fullVehicleExpected = new ShareabilityScore(0, 0, 0, 2, 1, 1, 5, 0);

        AssertScore(partialArcExpected, scorer.Score(partialArc));
        AssertScore(fullVehicleExpected, scorer.Score(fullVehicle));
    }

    [Fact]
    public async Task OracleScoreMatchesToyGoldenAcrossConcurrentCalls()
    {
        // Cell 3 of the dev toy golden fixture, seed 20261103.
        var network = ShareabilityNetwork.Create(
            3,
            [0, 53, 58, 36, 0, 5, 31, 43, 0],
            [0, 3, 5, 8],
            [1, 1, 2, 2, 0, 0, 0, 1],
            [53, 75, 106, 5, 66, 67, 31, 43],
            [0, 1, 2],
            3);
        var rates = ZoneDemandRates.Create(
            network,
            [3198651, 5363231, 4097901, 6538214, 6796288, 2251150, 1100904, 6621117, 2853912, 3178441, 3899616, 681686, 4934757, 5775410],
            [2, 2, 2, 1, 0, 1, 1, 1, 0, 2, 0, 2, 2, 0],
            [1, 1, 0, 2, 2, 0, 1, 1, 0, 1, 1, 1, 2, 2],
            [5, 1, 15],
            [7, 11, 7],
            0.5889605416217819, 0.5243094761390239, 411, 600000);
        var scorer = new ShareabilityScorer(network, new ShareabilityCell(
            0.9937985979486257,
            [1.0374202515697106, 1.112646494433284, 1.120062857749872, 1.199728613975458, 1.165335977356881, 1.0392778809415177, 1.0239834106294439, 1.1365505262743683, 1.2844774980563671],
            [0, 2, 1],
            600000, 30000, rates, Oracle: true));

        var route = new ShareabilityContext(
            2398009, 2, 0, 2398009, null, 2, 2,
            [
                new(1, false, 3250163.991832569, 265167.67388240295, 1),
                new(2, false, 3879025.991832569, 310700, 2),
                new(1, false, 3980996.991832569, 0, 1),
                new(2, true, 4617124.407473826, 160051.39039005758, 1),
                new(2, true, 4988574.407473826, 537356, 2),
                new(0, false, 5348716.407473826, 594323.2760014187, 2),
                new(0, true, 5949843.407473826, 897994.0191562828, 2),
                new(0, true, 6799284.0123187555, 667706, 1),
            ],
            4, 5, 0);
        var routeExpected = new ShareabilityScore(12.412611833936165, 12.07914701912135, 0.33346481481481344, 8, 4, 5, 6, 0);

        var partialArc = new ShareabilityContext(
            451477.1281005726, 0, 0, 456977.1281005726, new ShareabilityArc(2, 470), 0, 3,
            [
                new(0, true, 795931.5364276022, 798516, 1),
                new(2, false, 1029615.5364276022, 555778, 2),
            ],
            0, 1, 0);
        var partialArcExpected = new ShareabilityScore(2.296362722180197, 2.296362722180197, 0, 2, 0, 1, 2, 0);

        var tasks = Enumerable.Range(0, 16)
            .Select(_ => Task.Run(() => (Route: scorer.Score(route), Arc: scorer.Score(partialArc))));
        var results = await Task.WhenAll(tasks);
        foreach (var result in results)
        {
            AssertScore(routeExpected, result.Route);
            AssertScore(partialArcExpected, result.Arc);
        }
    }

    [Fact]
    public void TinyTravelScaleStillFinishesAtThePickupWindowBoundary()
    {
        var network = ShareabilityNetwork.Create(
            3, [0, 1, 2, 2, 0, 1, 1, 2, 0], [0, 1, 2, 3], [1, 2, 0], [1, 1, 1], [0, 0, 0], 1);
        var rates = ZoneDemandRates.Create(network, [], [], [], [3600], [3600], 1, 0, 0);
        var scorer = new ShareabilityScorer(
            network, new ShareabilityCell(1e-21, [1], [0, 1, 2], 1000, 0, rates, Oracle: false));

        var result = scorer.Score(new ShareabilityContext(
            0, 0, 0, 0, null, 0, 2,
            [new(1, true, 1000, 100, 1), new(2, false, 2000, 100, 1)],
            0, 1, 0));

        AssertScore(new ShareabilityScore(2, 2, 0, 3, 1, 2, 3, 0), result);
    }

    private static void AssertScore(ShareabilityScore expected, ShareabilityScore actual)
    {
        AssertBits(expected.Score, actual.Score);
        AssertBits(expected.Waiting, actual.Waiting);
        AssertBits(expected.Driving, actual.Driving);
        Assert.Equal(expected.Instants, actual.Instants);
        Assert.Equal(expected.PickupMainNode, actual.PickupMainNode);
        Assert.Equal(expected.DropMainNode, actual.DropMainNode);
        Assert.Equal(expected.MainNodes, actual.MainNodes);
        Assert.Equal(expected.ArcInstants, actual.ArcInstants);
    }

    private static void AssertBits(double expected, double actual) =>
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
}
