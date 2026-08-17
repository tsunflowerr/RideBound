using System.Text.Json;
using RideBound.Benchmarking.Contracts;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: RideBound.Wp6SeedVectors <repository-root>");
    return 2;
}

using var document = JsonDocument.Parse(
    File.ReadAllBytes(
        Path.Combine(
            Path.GetFullPath(args[0]),
            "benchmarks",
            "fixtures",
            "wp6",
            "planning",
            "seed-vectors.json")));

foreach (var item in document.RootElement.GetProperty("cases").EnumerateArray())
{
    var value = BenchmarkSeed.Derive(
        item.GetProperty("masterSeedHex").GetString()!,
        item.GetProperty("scenarioHash").GetString()!,
        item.GetProperty("repeatIndex").GetInt64(),
        item.GetProperty("componentId").GetString()!,
        item.GetProperty("stableItemId").GetString()!);
    Console.WriteLine(
        string.Join(
            '|',
            item.GetProperty("caseId").GetString(),
            value.DigestHex,
            value.NonNegativeInt32));
}

return 0;
