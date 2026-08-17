using System.Security.Cryptography;
using System.Text;
using RideBound.Benchmarking.Datasets;
using RideBound.Benchmarking.Normalization;

namespace RideBound.Benchmarking.Tests;

public sealed class FleetPyManhattanNormalizerTests
{
    [Fact]
    public async Task Dense_node_pool_hmac_selection_and_conservation_are_exact()
    {
        using var temp = new TestDirectory();
        var fixture = CreateFixture(temp, includeInvalidRows: true);
        var normalizer = new FleetPyManhattanNormalizer();

        var first = await normalizer.NormalizeAsync(fixture.Request);
        var second = await normalizer.NormalizeAsync(fixture.Request);

        Assert.Equal(FleetPyNormalizationStatus.Succeeded, first.Status);
        Assert.Equal(FleetPyNormalizationStatus.Succeeded, second.Status);
        var artifact = first.Artifact!;
        Assert.Equal(4, artifact.Scenario.Requests.Count);
        Assert.Equal(2, artifact.Scenario.Fleet.Count);
        Assert.Equal(4, artifact.Scenario.ValidationSummary.NodeCount);
        Assert.Equal(12, artifact.Scenario.ValidationSummary.DirectedArcCount);
        Assert.All(
            artifact.Scenario.TravelSnapshots.Single().Arcs,
            arc => Assert.True(arc.TravelTimeMs > 0));
        Assert.Equal(
            1_000,
            artifact.Scenario.TravelSnapshots.Single().Arcs.Single(
                arc => arc.FromNodeId == "node-000000"
                    && arc.ToNodeId == "node-000001").TravelTimeMs);
        Assert.Equal(
            2_000,
            artifact.Scenario.TravelSnapshots.Single().Arcs.Single(
                arc => arc.FromNodeId == "node-000001"
                    && arc.ToNodeId == "node-000000").TravelTimeMs);
        Assert.Equal(
            artifact.Report.InputRecordCount,
            artifact.Report.EligibleRecordCount + artifact.Report.ExcludedRecordCount);
        Assert.Equal(
            artifact.Report.EligibleRecordCount,
            artifact.Dispositions.Count);
        Assert.Equal(
            artifact.Report.SelectedRecordCount,
            artifact.Dispositions.Count(value => value.Disposition == "selected"));
        Assert.Contains(
            artifact.Exclusions,
            value => value.Code == "source.unreachable-node-pair");
        Assert.Contains(
            artifact.Exclusions,
            value => value.Code == "source.duplicate-request-id");
        Assert.Contains(
            artifact.Exclusions,
            value => value.Code == "source.invalid-time");
        Assert.DoesNotContain(
            "raw-request-",
            Encoding.UTF8.GetString(artifact.ScenarioCanonicalBytes),
            StringComparison.Ordinal);
        Assert.Equal(
            artifact.ScenarioCanonicalBytes,
            second.Artifact!.ScenarioCanonicalBytes);
        Assert.Equal(artifact.ReportCanonicalBytes, second.Artifact.ReportCanonicalBytes);
        Assert.Equal(
            artifact.DerivativeManifestCanonicalBytes,
            second.Artifact.DerivativeManifestCanonicalBytes);
    }

    [Fact]
    public async Task Edge_enumeration_order_does_not_change_scenario_semantics()
    {
        using var firstTemp = new TestDirectory();
        using var secondTemp = new TestDirectory();
        var firstFixture = CreateFixture(firstTemp, includeInvalidRows: false);
        var secondFixture = CreateFixture(
            secondTemp,
            includeInvalidRows: false,
            reverseEdgeRows: true);
        var normalizer = new FleetPyManhattanNormalizer();

        var first = await normalizer.NormalizeAsync(firstFixture.Request);
        var second = await normalizer.NormalizeAsync(secondFixture.Request);

        Assert.Equal(FleetPyNormalizationStatus.Succeeded, first.Status);
        Assert.Equal(FleetPyNormalizationStatus.Succeeded, second.Status);
        Assert.Equal(
            first.Artifact!.ScenarioCanonicalBytes,
            second.Artifact!.ScenarioCanonicalBytes);
        Assert.NotEqual(
            first.Artifact.Report.SourceMemberInventorySha256,
            second.Artifact.Report.SourceMemberInventorySha256);
    }

    [Fact]
    public async Task Member_tamper_and_ambiguous_dst_fail_closed()
    {
        using var tamperTemp = new TestDirectory();
        var tamperFixture = CreateFixture(tamperTemp, includeInvalidRows: false);
        await File.AppendAllTextAsync(tamperFixture.DemandPath, "raw-request-x,5,0,1\n");

        var tampered = await new FleetPyManhattanNormalizer()
            .NormalizeAsync(tamperFixture.Request);

        Assert.Equal(FleetPyNormalizationStatus.Failed, tampered.Status);
        Assert.Equal("source.member-checksum-mismatch", tampered.Issue!.Code);

        using var dstTemp = new TestDirectory();
        var dstFixture = CreateFixture(dstTemp, includeInvalidRows: false);
        var ambiguous = dstFixture.Request with
        {
            Configuration = dstFixture.Request.Configuration with
            {
                SourceLocalDate = "2018-11-04",
                SourceWindowStartSeconds = 0,
                SourceWindowEndSeconds = 5_400,
            },
        };
        var dst = await new FleetPyManhattanNormalizer().NormalizeAsync(ambiguous);

        Assert.Equal(FleetPyNormalizationStatus.Failed, dst.Status);
        Assert.Equal("source.dst-ambiguous-time", dst.Issue!.Code);
        Assert.Contains("DST", dst.Issue.SafeMessage, StringComparison.Ordinal);

        using var missingTemp = new TestDirectory();
        var missingFixture = CreateFixture(missingTemp, includeInvalidRows: false);
        var missingInventory = missingFixture.Request.Extraction.Inventory! with
        {
            Members = missingFixture.Request.Extraction.Inventory!.Members
                .Where(
                    value => !string.Equals(
                        value.RelativePath,
                        missingFixture.Request.Configuration.DemandMemberPath,
                        StringComparison.Ordinal))
                .ToArray(),
        };
        var missing = await new FleetPyManhattanNormalizer().NormalizeAsync(
            missingFixture.Request with
            {
                Extraction = missingFixture.Request.Extraction with
                {
                    Inventory = missingInventory,
                },
            });
        Assert.Equal(FleetPyNormalizationStatus.Failed, missing.Status);
        Assert.Equal("source.member-not-registered", missing.Issue!.Code);
    }

    [Fact]
    public async Task Policy_label_cannot_influence_selected_source_rows()
    {
        using var temp = new TestDirectory();
        var fixture = CreateFixture(temp, includeInvalidRows: false);
        var changed = fixture.Request with
        {
            Configuration = fixture.Request.Configuration with
            {
                CommitmentPolicyId = "different-synthetic-overlay-v1",
            },
        };
        var normalizer = new FleetPyManhattanNormalizer();

        var first = await normalizer.NormalizeAsync(fixture.Request);
        var second = await normalizer.NormalizeAsync(changed);

        Assert.Equal(
            first.Artifact!.Scenario.SourceSelectionSha256,
            second.Artifact!.Scenario.SourceSelectionSha256);
        Assert.Equal(
            first.Artifact.DispositionsCanonicalBytes,
            second.Artifact.DispositionsCanonicalBytes);
        Assert.NotEqual(
            first.Artifact.ScenarioContentSha256,
            second.Artifact.ScenarioContentSha256);
    }

    private static NormalizerFixture CreateFixture(
        TestDirectory temp,
        bool includeInvalidRows,
        bool reverseEdgeRows = false)
    {
        var extractionRoot = Path.Combine(temp.ExtractionRoot, "verified");
        var basePath = Path.Combine(extractionRoot, "FleetPy_Manhattan");
        var demandPath = Path.Combine(basePath, "demand.csv");
        var nodePath = Path.Combine(basePath, "nodes.csv");
        var edgePath = Path.Combine(basePath, "edges.csv");
        var factorPath = Path.Combine(basePath, "factors.csv");
        Directory.CreateDirectory(basePath);
        File.WriteAllText(
            nodePath,
            "node_index,is_stop_only,pos_x,pos_y\n"
            + string.Join(
                '\n',
                Enumerable.Range(0, 6).Select(node => $"{node},False,{node}.0,{node}.0"))
            + "\n",
            new UTF8Encoding(false));
        var edgeRows = new List<string>();

        for (var from = 0; from < 4; from++)
        {
            for (var to = 0; to < 4; to++)
            {
                if (from != to)
                {
                    var seconds = from == 1 && to == 0 ? "2.0" : "1.0005";
                    edgeRows.Add($"{from},{to},10.0,{seconds},");
                }
            }
        }

        edgeRows.Add("4,5,10.0,1.0,");
        edgeRows.Add("5,4,10.0,1.0,");
        edgeRows.Add("5,5,1.0,1.0,");

        if (reverseEdgeRows)
        {
            edgeRows.Reverse();
        }

        File.WriteAllText(
            edgePath,
            "from_node,to_node,distance,travel_time,source_edge_id\n"
            + string.Join('\n', edgeRows)
            + "\n",
            new UTF8Encoding(false));
        File.WriteAllText(
            factorPath,
            "simulation_time,time_interval,travel_time_factor,travel requests,average travel duration,standard error,25%,50%,75%\n"
            + "0,0,1.0,1,1,1,1,1,1\n",
            new UTF8Encoding(false));
        var demandRows = new List<string>
        {
            "raw-request-0,0,0,1",
            "raw-request-1,1,1,2",
            "raw-request-2,2,2,3",
            "raw-request-3,3,3,0",
            "raw-request-4,4,0,2",
            "raw-request-5,5,1,3",
            "raw-request-6,6,2,0",
            "raw-request-7,7,3,1",
            "raw-request-8,8,4,5",
        };

        if (includeInvalidRows)
        {
            demandRows.Add("raw-request-9,9,0,4");
            demandRows.Add("raw-request-0,10,0,1");
            demandRows.Add("raw-request-overflow,999999999999999999999999,0,1");
            demandRows.Add("unterminated,\"11,0,1");
        }

        File.WriteAllText(
            demandPath,
            "request_id,rq_time,start,end\n"
            + string.Join('\n', demandRows)
            + "\n",
            new UTF8Encoding(false));
        var paths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["FleetPy_Manhattan/demand.csv"] = demandPath,
            ["FleetPy_Manhattan/nodes.csv"] = nodePath,
            ["FleetPy_Manhattan/edges.csv"] = edgePath,
            ["FleetPy_Manhattan/factors.csv"] = factorPath,
        };
        var members = paths
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .Select(
                value => new ArchiveMember(
                    value.Key,
                    new FileInfo(value.Value).Length,
                    new FileInfo(value.Value).Length,
                    Sha(File.ReadAllBytes(value.Value))))
            .ToArray();
        var inventoryHash = Sha(
            Encoding.UTF8.GetBytes(
                string.Join(
                    '\n',
                    members.Select(
                        value => $"{value.RelativePath}|{value.LengthBytes}|{value.Sha256}"))));
        var artifactSha = new string('a', 64);
        var registration = DatasetSourceRegistry.FleetPyManhattanV1;
        var descriptor = new RideBound.Benchmarking.Contracts.DatasetDescriptor(
            RideBound.Benchmarking.Contracts.BenchmarkContractVersions.V1,
            registration.DatasetId,
            registration.DatasetKind,
            registration.Title,
            registration.ReleaseVersion,
            registration.PersistentUri.AbsoluteUri,
            registration.DownloadUri.AbsoluteUri,
            "2026-08-09T00:00:00.0000000Z",
            registration.PublisherArtifactName,
            registration.LicenseSpdx,
            registration.LicenseUri.AbsoluteUri,
            registration.Citation,
            registration.Composition,
            registration.CollectionLimit,
            registration.AllowedUse,
            registration.ForbiddenClaim,
            registration.DirectIdentifierStatus,
            registration.LocationPrecisionClass,
            registration.RetentionClass,
            registration.MaintenanceNote,
            1,
            new string('b', 32),
            artifactSha);
        var artifact = new VerifiedDatasetArtifact(
            registration.DatasetId,
            Path.Combine(temp.CacheRoot, "source.zip"),
            $"sha256:{artifactSha}",
            1,
            new string('b', 32),
            artifactSha,
            descriptor,
            false);
        var extraction = ArchiveExtractionResult.Success(
            extractionRoot,
            new ArchiveInventory(
                artifactSha,
                inventoryHash,
                members.Sum(value => value.LengthBytes),
                members),
            false);
        var configuration = new FleetPyNormalizationConfiguration(
            "test-public-derivative",
            "FleetPy_Manhattan/demand.csv",
            "FleetPy_Manhattan/nodes.csv",
            "FleetPy_Manhattan/edges.csv",
            "FleetPy_Manhattan/factors.csv",
            "2018-11-12",
            "America/New_York",
            0,
            100,
            0,
            4,
            2,
            4,
            4,
            10_000,
            1_500,
            10_000,
            new string('1', 64),
            new string('2', 64),
            "test-synthetic-policy-overlay-v1",
            new string('c', 64));
        return new NormalizerFixture(
            new FleetPyNormalizationRequest(artifact, extraction, configuration),
            demandPath);
    }

    private static string Sha(byte[] bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));

    private sealed record NormalizerFixture(
        FleetPyNormalizationRequest Request,
        string DemandPath);
}
