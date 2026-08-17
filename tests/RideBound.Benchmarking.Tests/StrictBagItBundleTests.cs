using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using RideBound.Benchmarking.Bundles;
using RideBound.Benchmarking.Claims;
using RideBound.Benchmarking.Contracts;
using RideBound.Contracts.Serialization;

namespace RideBound.Benchmarking.Tests;

public sealed class StrictBagItBundleTests
{
    [Fact]
    public void Source_inventory_binds_dirty_worktree_files_not_only_base_commit()
    {
        var repository = StrictBundleTestFixture.FindRepositoryRoot();
        var inventory = BundleSourceInventoryCapture.Capture(
            repository,
            [
                new BundleSourceComponentSelection(
                    "harness",
                    ["src/RideBound.Benchmarking"]),
                new BundleSourceComponentSelection(
                    "oracle",
                    ["tools/RideBound.Wp6MetricOracle"]),
                new BundleSourceComponentSelection(
                    "verifier",
                    ["tools/RideBound.Wp6BundleVerify"]),
            ]);

        Assert.True(inventory.GitDirty);
        Assert.Matches("^[0-9a-f]{40}$", inventory.GitCommit);
        Assert.Contains(
            inventory.Entries,
            value => value.RelativePath
                == "src/RideBound.Benchmarking/Bundles/StrictBagItBundleVerifier.cs");

        foreach (var entry in inventory.Entries)
        {
            var fullPath = Path.Combine(
                repository,
                entry.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.Equal(new FileInfo(fullPath).Length, entry.LengthBytes);
            Assert.Equal(StrictBundleTestFixture.FileSha(fullPath), entry.Sha256);
        }

        var harnessHash = BundleSourceInventoryIdentity.CalculateComponent(
            "harness",
            inventory.Entries);
        var changed = inventory.Entries.ToArray();
        var index = Array.FindIndex(changed, value => value.ComponentId == "harness");
        changed[index] = changed[index] with { Sha256 = new string('0', 64) };
        Assert.NotEqual(
            harnessHash,
            BundleSourceInventoryIdentity.CalculateComponent("harness", changed));
    }

    [Fact]
    public async Task Builder_is_deterministic_atomic_immutable_and_self_verifying()
    {
        using var firstTemp = new TestDirectory();
        using var secondTemp = new TestDirectory();
        var first = await StrictBundleTestFixture.CreateAsync(firstTemp, "bundle-a");
        var second = await StrictBundleTestFixture.CreateAsync(secondTemp, "bundle-b");
        var firstVerification = new StrictBagItBundleVerifier().Verify(first.BundleRoot);
        var secondVerification = new StrictBagItBundleVerifier().Verify(second.BundleRoot);

        Assert.True(firstVerification.IsValid, Format(firstVerification));
        Assert.True(secondVerification.IsValid, Format(secondVerification));
        Assert.Equal(first.Build.BundleHash, second.Build.BundleHash);
        Assert.Equal(first.PlanHash, second.PlanHash);
        Assert.Equal(first.MetricSetHash, second.MetricSetHash);
        AssertDirectoriesEqual(first.BundleRoot, second.BundleRoot);
        Assert.DoesNotContain(
            "bundleHash",
            File.ReadAllText(Path.Combine(first.BundleRoot, "data", "bundle-manifest.json")),
            StringComparison.Ordinal);
        Assert.Contains(
            "data/bundle-manifest.json",
            File.ReadAllText(Path.Combine(first.BundleRoot, "manifest-sha256.txt")),
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "tagmanifest-sha256.txt",
            File.ReadAllText(Path.Combine(first.BundleRoot, "tagmanifest-sha256.txt")),
            StringComparison.Ordinal);

        await Assert.ThrowsAsync<IOException>(
            () => StrictBundleTestFixture.CreateAsync(firstTemp, "bundle-a"));
    }

    [Fact]
    public async Task Claim_profile_and_report_are_canonical_source_locked_and_scoped()
    {
        using var temp = new TestDirectory();
        var fixture = await StrictBundleTestFixture.CreateAsync(temp);
        var profilePath = Path.Combine(
            fixture.BundleRoot,
            "data",
            "provenance",
            "claim-profile.json");
        var profileBytes = File.ReadAllBytes(profilePath);
        var profile = BundleEvidenceJson.DecodeExact<ArtifactClaimProfile>(profileBytes);
        var report = BundleEvidenceJson.DecodeExact<ArtifactClaimCheckReport>(
            File.ReadAllBytes(Path.Combine(fixture.BundleRoot, "data", "claim-check.json")));

        Assert.Equal(ArtifactClaimProfileCatalog.GetV1CanonicalBytes(), profileBytes);
        Assert.Equal("ADR-032", profile.DecisionId);
        Assert.Equal("wp6-mechanical-only-v1", profile.ProfileId);
        Assert.Equal(ArtifactClaimProfileCatalog.V1Sha256, report.ProfileSha256);
        Assert.Equal("passed", report.Status);
        Assert.Empty(report.Witnesses);
        Assert.Equal(profile.RequiredCaveats.Count, report.SatisfiedCaveatIds.Count);
        Assert.Equal(profile.ScannedSelections, report.ScannedSelections);
        Assert.False(report.BoundaryFlags.ConfirmatoryEvidence);
        Assert.False(report.BoundaryFlags.IndependentTeamEvidence);
        Assert.False(report.BoundaryFlags.AcmBadgeEvidence);
        Assert.True(report.BoundaryFlags.ResourceMeasurementsLocalControlsOnly);
        Assert.True(report.BoundaryFlags.SameTeamCleanProcessOnly);
        Assert.DoesNotContain(
            report.ScannedSelections,
            value => value.StartsWith("data/runs/", StringComparison.Ordinal)
                || value.StartsWith("data/scenarios/", StringComparison.Ordinal)
                || value.StartsWith("data/datasets/", StringComparison.Ordinal));
        Assert.Contains("https://www.acm.org/publications/badging-terms", profile.EvidenceUris);
        Assert.Contains(
            "https://www.nationalacademies.org/read/25303/chapter/2",
            profile.EvidenceUris);
        Assert.Contains("https://www.nature.com/articles/s41562-016-0021", profile.EvidenceUris);
        Assert.Contains(
            "https://pmc.ncbi.nlm.nih.gov/articles/PMC3383002/",
            profile.EvidenceUris);
        Assert.Contains("https://www.unicode.org/reports/tr39/", profile.EvidenceUris);

        var manifest = DecodeManifest(
            Path.Combine(fixture.BundleRoot, "data", "bundle-manifest.json"));
        var planResult = BenchmarkContractCodec.Decode<BenchmarkPlan>(
            File.ReadAllBytes(Path.Combine(fixture.BundleRoot, "data", "benchmark-plan.json")));
        Assert.True(planResult.IsSuccess, planResult.Error?.ToString());
        var packagingReport = BundleEvidenceJson.DecodeExact<BundlePackagingVerificationReport>(
            File.ReadAllBytes(
                Path.Combine(fixture.BundleRoot, "data", "verification-report.json")));
        var machine = BundleEvidenceJson.DecodeExact<BundleMachineProvenance>(
            File.ReadAllBytes(
                Path.Combine(fixture.BundleRoot, "data", "provenance", "machine.json")));
        var source = BundleEvidenceJson.DecodeExact<BundleSourceInventory>(
            File.ReadAllBytes(
                Path.Combine(
                    fixture.BundleRoot,
                    "data",
                    "source-inventory",
                    "repository.json")));
        var readmeBytes = File.ReadAllBytes(Path.Combine(fixture.BundleRoot, "README.md"));
        var direct = ArtifactClaimChecker.Check(
            new ArtifactClaimCheckInput(
                readmeBytes,
                manifest,
                planResult.Value!,
                packagingReport,
                machine,
                source));
        Assert.True(direct.IsValid);
        Assert.Equal(
            BundleEvidenceJson.Encode(report),
            BundleEvidenceJson.Encode(direct.Report));

        var invalidReadme = Encoding.UTF8.GetBytes(
            Encoding.UTF8.GetString(readmeBytes) + "The result is s.u.p.e.r.i.o.r.\n");
        var invalid = ArtifactClaimChecker.Check(
            new ArtifactClaimCheckInput(
                invalidReadme,
                manifest,
                planResult.Value!,
                packagingReport,
                machine,
                source));
        var witness = Assert.Single(invalid.Report.Witnesses);
        Assert.False(invalid.IsValid);
        Assert.Equal("claim.forbidden.effectiveness", witness.Code);
        Assert.Equal("effectiveness", witness.RuleId);
        Assert.Equal("README.md", witness.RelativePath);
        Assert.Equal("document", witness.Selector);
        Assert.Equal("superior", witness.NormalizedWitness);
    }

    [Fact]
    public async Task Forbidden_synonym_and_unicode_mutations_fail_with_typed_claim_witness()
    {
        using var temp = new TestDirectory();
        var fixture = await StrictBundleTestFixture.CreateAsync(temp);
        var mutations = new[]
        {
            ("case", "The method is EFFECTIVE.", "claim.forbidden.effectiveness"),
            ("punctuation", "The method is e.f.f.e.c.t.i.v.e.", "claim.forbidden.effectiveness"),
            ("confusable", "The method is \u0435ffective.", "claim.forbidden.effectiveness"),
            ("ignorable", "The method is effec\u200btive.", "claim.obfuscation-character"),
            ("noninferior", "The result is nOn-InFeRiOr.", "claim.forbidden.non-inferiority"),
            ("sla", "The benchmark meets S.L.A.", "claim.forbidden.sla"),
            ("production", "This system is production-ready.", "claim.forbidden.production-readiness"),
            ("production-synonym", "This system is deployment ready.", "claim.forbidden.production-readiness"),
            ("novelty", "This is the first-ever method.", "claim.forbidden.novelty"),
            ("satisfaction", "It improves user satisfaction.", "claim.forbidden.user-satisfaction"),
            ("acm", "ACM Artifact Evaluated.", "claim.forbidden.acm-badge"),
            ("reproduced", "Results reproduced.", "claim.forbidden.independent-reproduction"),
            ("replicated", "Results replicated.", "claim.forbidden.replication"),
            ("effect-synonym", "It outperforms the baseline.", "claim.forbidden.effectiveness"),
        };

        foreach (var (name, text, code) in mutations)
        {
            var root = CloneAndMutate(
                temp,
                fixture,
                "claim-" + name,
                value =>
                {
                    File.AppendAllText(
                        Path.Combine(value, "README.md"),
                        text + "\n",
                        new UTF8Encoding(false));
                    RewriteTagManifest(value);
                });
            AssertClaimFailure(root, code, "README.md");
        }
    }

    [Fact]
    public async Task Missing_caveat_forged_report_and_provenance_claim_block_bundle_validity()
    {
        using var temp = new TestDirectory();
        var fixture = await StrictBundleTestFixture.CreateAsync(temp);
        var missing = CloneAndMutate(
            temp,
            fixture,
            "claim-missing-caveat",
            root =>
            {
                var readmePath = Path.Combine(root, "README.md");
                var readme = File.ReadAllText(readmePath).Replace(
                    "Same-team clean-process repeatability only.\n",
                    string.Empty,
                    StringComparison.Ordinal);
                File.WriteAllText(readmePath, readme, new UTF8Encoding(false));
                RewriteTagManifest(root);
            });
        AssertClaimFailure(missing, "claim.caveat-missing", "README.md");

        var reportLabel = CloneAndMutate(
            temp,
            fixture,
            "claim-report-label",
            root =>
            {
                var path = Path.Combine(root, "data", "verification-report.json");
                var report = BundleEvidenceJson.DecodeExact<BundlePackagingVerificationReport>(
                    File.ReadAllBytes(path));
                File.WriteAllBytes(
                    path,
                    BundleEvidenceJson.Encode(report with { Status = "production-ready" }));
                ResealBag(root, updateLogicalArtifacts: true);
            });
        AssertClaimFailure(
            reportLabel,
            "claim.report-label-invalid",
            "data/verification-report.json");

        var provenance = CloneAndMutate(
            temp,
            fixture,
            "claim-provenance",
            root =>
            {
                var machinePath = Path.Combine(root, "data", "provenance", "machine.json");
                var machine = BundleEvidenceJson.DecodeExact<BundleMachineProvenance>(
                    File.ReadAllBytes(machinePath));
                File.WriteAllBytes(
                    machinePath,
                    BundleEvidenceJson.Encode(
                        machine with { PowerModeNote = "production-ready" }));
                RewriteBindingHash(root, "MachineProvenanceSha256", StrictBundleTestFixture.FileSha(machinePath));
                ResealBag(root, updateLogicalArtifacts: true);
            });
        AssertClaimFailure(
            provenance,
            "claim.forbidden.production-readiness",
            "data/provenance/machine.json");

        var forged = CloneAndMutate(
            temp,
            fixture,
            "claim-forged-report",
            root =>
            {
                var path = Path.Combine(root, "data", "claim-check.json");
                var report = BundleEvidenceJson.DecodeExact<ArtifactClaimCheckReport>(
                    File.ReadAllBytes(path));
                File.WriteAllBytes(
                    path,
                    BundleEvidenceJson.Encode(
                        report with
                        {
                            BoundaryFlags = report.BoundaryFlags with { AcmBadgeEvidence = true },
                        }));
                ResealBag(root, updateLogicalArtifacts: true);
            });
        AssertClaimFailure(forged, "claim.report-mismatch", "data/claim-check.json");

        var profile = CloneAndMutate(
            temp,
            fixture,
            "claim-profile-switch",
            root =>
            {
                var path = Path.Combine(root, "data", "provenance", "claim-profile.json");
                var decoded = BundleEvidenceJson.DecodeExact<ArtifactClaimProfile>(
                    File.ReadAllBytes(path));
                File.WriteAllBytes(
                    path,
                    BundleEvidenceJson.Encode(decoded with { DecisionId = "ADR-999" }));
                RewriteBindingHash(root, "ClaimProfileSha256", StrictBundleTestFixture.FileSha(path));
                ResealBag(root, updateLogicalArtifacts: true);
            });
        AssertClaimFailure(
            profile,
            "claim.profile-unsupported",
            "data/provenance/claim-profile.json");
    }

    [Fact]
    public async Task Fresh_process_verifier_emits_external_sidecar_without_mutating_bag()
    {
        using var temp = new TestDirectory();
        var fixture = await StrictBundleTestFixture.CreateAsync(temp);
        var before = Inventory(fixture.BundleRoot);
        var report = Path.Combine(temp.Root, "external-verification.json");
        var start = new ProcessStartInfo
        {
            FileName = "dotnet",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add(fixture.VerifierAssemblyPath);
        start.ArgumentList.Add("--bag");
        start.ArgumentList.Add(fixture.BundleRoot);
        start.ArgumentList.Add("--report");
        start.ArgumentList.Add(report);
        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("Could not launch external bundle verifier.");
        var standardOutput = await process.StandardOutput.ReadToEndAsync();
        var standardError = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.Equal(0, process.ExitCode);
        Assert.Equal(string.Empty, standardError);
        Assert.Equal(fixture.Build.BundleHash, standardOutput.Trim());
        Assert.True(File.Exists(report));
        var decoded = BundleEvidenceJson.DecodeExact<ExternalBundleVerificationReport>(
            File.ReadAllBytes(report));
        Assert.True(decoded.IsValid);
        Assert.Equal(fixture.Build.BundleHash, decoded.BundleHash);
        Assert.Equal(StrictBundleTestFixture.FileSha(fixture.VerifierAssemblyPath), decoded.VerifierAssemblySha256);
        Assert.Equal(before, Inventory(fixture.BundleRoot));
    }

    [Fact]
    public async Task Ordered_mutations_fail_at_path_layout_hash_logical_provenance_transcript_and_metric_stages()
    {
        using var temp = new TestDirectory();
        var fixture = await StrictBundleTestFixture.CreateAsync(temp);

        AssertStage(
            CloneAndMutate(temp, fixture, "layout", root =>
                File.Delete(Path.Combine(root, "README.md"))),
            BundleVerificationStage.Layout);
        AssertStage(
            CloneAndMutate(temp, fixture, "extra", root =>
                File.WriteAllText(Path.Combine(root, "unexpected.txt"), "extra")),
            BundleVerificationStage.Layout);
        AssertStage(
            CloneAndMutate(temp, fixture, "hash", root =>
                File.AppendAllText(Path.Combine(root, "data", "claim-check.json"), " ")),
            BundleVerificationStage.BagItIntegrity);
        AssertStage(
            CloneAndMutate(
                temp,
                fixture,
                "script",
                root =>
                {
                    File.AppendAllText(Path.Combine(root, "verify.ps1"), "# altered\n");
                    RewriteTagManifest(root);
                }),
            BundleVerificationStage.BagItIntegrity);
        AssertStage(
            CloneAndMutate(
                temp,
                fixture,
                "logical",
                root =>
                {
                    var path = Path.Combine(root, "data", "bundle-manifest.json");
                    var manifest = DecodeManifest(path);
                    var artifacts = manifest.Artifacts.ToArray();
                    artifacts[0] = artifacts[0] with { LengthBytes = artifacts[0].LengthBytes + 1 };
                    File.WriteAllBytes(path, BenchmarkContractCodec.Encode(manifest with { Artifacts = artifacts }));
                    ResealBag(root, updateLogicalArtifacts: false);
                }),
            BundleVerificationStage.LogicalManifest);
        AssertStage(
            CloneAndMutate(
                temp,
                fixture,
                "logical-type",
                root =>
                {
                    var path = Path.Combine(root, "data", "bundle-manifest.json");
                    var manifest = DecodeManifest(path);
                    var artifacts = manifest.Artifacts.ToArray();
                    artifacts[0] = artifacts[0] with { MediaType = "text/plain" };
                    File.WriteAllBytes(path, BenchmarkContractCodec.Encode(manifest with { Artifacts = artifacts }));
                    ResealBag(root, updateLogicalArtifacts: false);
                }),
            BundleVerificationStage.LogicalManifest);
        AssertStage(
            CloneAndMutate(
                temp,
                fixture,
                "provenance",
                root =>
                {
                    var path = Path.Combine(root, "data", "provenance", "reproducibility.json");
                    var binding = BundleEvidenceJson.DecodeExact<BundleReproducibilityBinding>(
                        File.ReadAllBytes(path));
                    File.WriteAllBytes(
                        path,
                        BundleEvidenceJson.Encode(
                            binding with { HarnessSourceSha256 = new string('0', 64) }));
                    ResealBag(root, updateLogicalArtifacts: true);
                }),
            BundleVerificationStage.Provenance);
        AssertStage(
            CloneAndMutate(
                temp,
                fixture,
                "scenario-identity",
                root =>
                {
                    var scenarioPath = Directory.GetFiles(
                        Path.Combine(root, "data", "scenarios"),
                        "scenario.json",
                        SearchOption.AllDirectories).Single();
                    var scenario = BenchmarkContractCodec.Decode<ScenarioContent>(
                        File.ReadAllBytes(scenarioPath));
                    Assert.True(scenario.IsSuccess, scenario.Error?.ToString());
                    File.WriteAllBytes(
                        scenarioPath,
                        BenchmarkContractCodec.Encode(
                            scenario.Value! with { ScenarioId = "mutated-scenario" }));
                    ResealBag(root, updateLogicalArtifacts: true);
                }),
            BundleVerificationStage.Provenance);
        AssertStage(
            CloneAndMutate(
                temp,
                fixture,
                "plan-grid",
                root =>
                {
                    var storePath = Path.Combine(root, "data", "provenance", "run-store-plan.json");
                    var storePlan = BundleEvidenceJson.DecodeExact<BundleRunStorePlan>(
                        File.ReadAllBytes(storePath));
                    var runs = storePlan.Runs.ToArray();
                    runs[0] = runs[0] with { ComponentSeedHex = new string('0', 64) };
                    File.WriteAllBytes(
                        storePath,
                        BundleEvidenceJson.Encode(storePlan with { Runs = runs }));
                    var bindingPath = Path.Combine(root, "data", "provenance", "reproducibility.json");
                    var binding = BundleEvidenceJson.DecodeExact<BundleReproducibilityBinding>(
                        File.ReadAllBytes(bindingPath));
                    File.WriteAllBytes(
                        bindingPath,
                        BundleEvidenceJson.Encode(
                            binding with
                            {
                                RunStorePlanSha256 = StrictBundleTestFixture.FileSha(storePath),
                            }));
                    ResealBag(root, updateLogicalArtifacts: true);
                }),
            BundleVerificationStage.PlanConservation);
        AssertStage(
            CloneAndMutate(
                temp,
                fixture,
                "transcript",
                root =>
                {
                    var path = Path.Combine(root, "data", "runs", fixture.RunId, "input.ndjson");
                    var bytes = File.ReadAllBytes(path);
                    bytes[0] = bytes[0] == (byte)'{' ? (byte)'[' : (byte)'{';
                    File.WriteAllBytes(path, bytes);
                    ResealBag(root, updateLogicalArtifacts: true);
                }),
            BundleVerificationStage.TranscriptProtocol);
        AssertStage(
            CloneAndMutate(
                temp,
                fixture,
                "metric",
                root =>
                {
                    var path = Path.Combine(root, "data", "metrics", "oracle.ndjson");
                    var bytes = File.ReadAllBytes(path);
                    bytes[^2] = bytes[^2] == (byte)'}' ? (byte)' ' : (byte)'}';
                    File.WriteAllBytes(path, bytes);
                    ResealBag(root, updateLogicalArtifacts: true);
                }),
            BundleVerificationStage.Metrics);
        AssertStage(
            CloneAndMutate(
                temp,
                fixture,
                "metric-correlated",
                root =>
                {
                    var production = Path.Combine(root, "data", "metrics", "production.ndjson");
                    var lines = File.ReadAllLines(production);
                    var first = BenchmarkContractCodec.Decode<MetricRow>(
                        Encoding.UTF8.GetBytes(lines[0]));
                    Assert.True(first.IsSuccess, first.Error?.ToString());
                    Assert.NotNull(first.Value!.ValueInteger);
                    lines[0] = Encoding.UTF8.GetString(
                        BenchmarkContractCodec.Encode(
                            first.Value with { ValueInteger = first.Value.ValueInteger + 1 }));
                    var bytes = Encoding.UTF8.GetBytes(string.Join('\n', lines) + "\n");
                    File.WriteAllBytes(production, bytes);
                    File.WriteAllBytes(
                        Path.Combine(root, "data", "metrics", "oracle.ndjson"),
                        bytes);
                    ResealBag(root, updateLogicalArtifacts: true);
                }),
            BundleVerificationStage.Metrics);
        AssertStage(
            CloneAndMutate(
                temp,
                fixture,
                "oracle-summary",
                root =>
                {
                    var path = Directory.GetFiles(
                        Path.Combine(root, "data", "provenance", "oracle-execution"),
                        "*.summary.json").Order(StringComparer.Ordinal).First();
                    var summary = JsonNode.Parse(File.ReadAllBytes(path))!.AsObject();
                    summary["semanticEvidenceSha256"] = new string('0', 64);
                    File.WriteAllBytes(
                        path,
                        CanonicalJson.Canonicalize(
                            Encoding.UTF8.GetBytes(summary.ToJsonString())));
                    ResealBag(root, updateLogicalArtifacts: true);
                }),
            BundleVerificationStage.Metrics);
    }

    [Fact]
    public async Task Mixed_terminal_bundle_conserves_global_failure_exclusion_order()
    {
        using var temp = new TestDirectory();
        var fixture = await StrictBundleTestFixture.CreateAsync(
            temp,
            "mixed-bundle",
            mixedTerminals: true);
        var valid = new StrictBagItBundleVerifier().Verify(fixture.BundleRoot);
        Assert.True(valid.IsValid, Format(valid));
        var mutated = CloneAndMutate(
            temp,
            fixture,
            "terminal-log",
            root =>
            {
                File.WriteAllBytes(Path.Combine(root, "data", "exclusions.ndjson"), []);
                ResealBag(root, updateLogicalArtifacts: true);
            });
        AssertStage(mutated, BundleVerificationStage.TerminalLogs);
    }

    [Fact]
    public async Task Traversal_case_collision_reparse_and_external_report_overwrite_are_rejected()
    {
        using var temp = new TestDirectory();
        var fixture = await StrictBundleTestFixture.CreateAsync(temp);
        var traversal = CloneAndMutate(
            temp,
            fixture,
            "traversal",
            root =>
            {
                var path = Path.Combine(root, "manifest-sha256.txt");
                var lines = File.ReadAllLines(path);
                lines[0] = lines[0][..66] + "../escape";
                File.WriteAllText(path, string.Join('\n', lines) + "\n", new UTF8Encoding(false));
                RewriteTagManifest(root);
            });
        AssertStage(traversal, BundleVerificationStage.BagItIntegrity);

        var sourcePath = Path.Combine(temp.Root, "case-source.json");
        File.WriteAllBytes(sourcePath, "{}"u8.ToArray());
        var duplicate = new BundlePayloadSource(
            sourcePath,
            "data/CLAIM-check.json",
            "application/json",
            BundleArtifactRole.ClaimCheck,
            "fixture",
            ["fixture"]);
        var manifestPath = Path.Combine(fixture.BundleRoot, "data", "bundle-manifest.json");
        var manifest = DecodeManifest(manifestPath);
        Assert.NotNull(manifest);
        var invalidRequest = new StrictBagItBundleRequest(
            Path.Combine(temp.Root, "case-bundle"),
            "case-bundle",
            EvidenceClass.Mechanical,
            "wp6-mechanical-only-v1",
            fixture.MetricSetHash,
            manifest.SourceInventorySha256,
            manifest.RuntimeInventorySha256,
            "2026-08-11",
            [
                duplicate,
                duplicate with { RelativePath = "data/claim-check.json" },
            ]);
        await Assert.ThrowsAsync<ArgumentException>(
            () => new StrictBagItBundleBuilder().BuildAsync(invalidRequest));

        var linkRoot = CloneAndMutate(temp, fixture, "reparse", _ => { });
        var link = Path.Combine(linkRoot, "data", "linked");
        CreateJunction(link, Path.Combine(linkRoot, "data", "runs"));
        AssertStage(linkRoot, BundleVerificationStage.PathSafety);
        Directory.Delete(link);

        var report = Path.Combine(temp.Root, "existing-report.json");
        File.WriteAllText(report, "owned");
        var (exitCode, _, standardError) = await RunVerifier(fixture, report);
        Assert.Equal(64, exitCode);
        Assert.Contains("report-path", standardError, StringComparison.Ordinal);
        Assert.Equal("owned", File.ReadAllText(report));
    }

    private static string CloneAndMutate(
        TestDirectory temp,
        StrictBundleFixture fixture,
        string name,
        Action<string> mutation)
    {
        var target = Path.Combine(temp.Root, "mutations", name);
        CopyDirectory(fixture.BundleRoot, target);
        mutation(target);
        return target;
    }

    private static void AssertStage(string root, BundleVerificationStage stage)
    {
        var result = new StrictBagItBundleVerifier().Verify(root);
        Assert.False(result.IsValid);
        Assert.NotNull(result.Issue);
        Assert.Equal(stage, result.Issue.Stage);
    }

    private static void AssertClaimFailure(string root, string code, string path)
    {
        var result = new StrictBagItBundleVerifier().Verify(root);
        Assert.False(result.IsValid);
        Assert.NotNull(result.Issue);
        Assert.Equal(BundleVerificationStage.Claims, result.Issue.Stage);
        Assert.Equal(code, result.Issue.Code);
        Assert.Equal(path, result.Issue.RelativePath);
    }

    private static void RewriteBindingHash(string root, string field, string sha256)
    {
        var path = Path.Combine(root, "data", "provenance", "reproducibility.json");
        var binding = BundleEvidenceJson.DecodeExact<BundleReproducibilityBinding>(
            File.ReadAllBytes(path));
        var updated = field switch
        {
            "ClaimProfileSha256" => binding with { ClaimProfileSha256 = sha256 },
            "MachineProvenanceSha256" => binding with { MachineProvenanceSha256 = sha256 },
            _ => throw new InvalidOperationException(field),
        };
        File.WriteAllBytes(path, BundleEvidenceJson.Encode(updated));
    }

    private static LogicalBundleManifest DecodeManifest(string path)
    {
        var result = BenchmarkContractCodec.Decode<LogicalBundleManifest>(File.ReadAllBytes(path));
        Assert.True(result.IsSuccess, result.Error?.ToString());
        return result.Value!;
    }

    private static void ResealBag(string root, bool updateLogicalArtifacts)
    {
        var logicalPath = Path.Combine(root, "data", "bundle-manifest.json");

        if (updateLogicalArtifacts)
        {
            var logical = DecodeManifest(logicalPath);
            var updated = logical.Artifacts
                .Select(
                    artifact =>
                    {
                        var full = Path.Combine(
                            root,
                            artifact.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                        var bytes = File.ReadAllBytes(full);
                        return artifact with
                        {
                            LengthBytes = bytes.LongLength,
                            Sha256 = StrictBundleTestFixture.FileSha(bytes),
                        };
                    })
                .ToArray();
            File.WriteAllBytes(
                logicalPath,
                BenchmarkContractCodec.Encode(logical with { Artifacts = updated }));
        }

        RewriteBagInfo(root);
        RewritePayloadManifest(root);
        RewriteTagManifest(root);
    }

    private static void RewriteBagInfo(string root)
    {
        var path = Path.Combine(root, "bag-info.txt");
        var lines = File.ReadAllLines(path);
        var payloadFiles = Directory.GetFiles(
            Path.Combine(root, "data"),
            "*",
            SearchOption.AllDirectories);
        var payloadLength = payloadFiles.Sum(file => new FileInfo(file).Length);
        lines[3] = $"Payload-Oxum: {payloadLength}.{payloadFiles.LongLength}";
        File.WriteAllText(path, string.Join('\n', lines) + "\n", new UTF8Encoding(false));
    }

    private static void RewritePayloadManifest(string root)
    {
        var files = Directory.GetFiles(
                Path.Combine(root, "data"),
                "*",
                SearchOption.AllDirectories)
            .OrderBy(path => Relative(root, path), StringComparer.Ordinal)
            .ToArray();
        WriteManifest(Path.Combine(root, "manifest-sha256.txt"), root, files);
    }

    private static void RewriteTagManifest(string root)
    {
        var files = new[]
        {
            "README.md",
            "bag-info.txt",
            "bagit.txt",
            "manifest-sha256.txt",
            "verify.ps1",
        }.Select(path => Path.Combine(root, path));
        WriteManifest(Path.Combine(root, "tagmanifest-sha256.txt"), root, files);
    }

    private static void WriteManifest(string path, string root, IEnumerable<string> files)
    {
        var text = string.Concat(
            files.OrderBy(value => Relative(root, value), StringComparer.Ordinal)
                .Select(
                    value => StrictBundleTestFixture.FileSha(value)
                        + "  " + Relative(root, value) + "\n"));
        File.WriteAllText(path, text, new UTF8Encoding(false));
    }

    private static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunVerifier(
        StrictBundleFixture fixture,
        string report)
    {
        var start = new ProcessStartInfo
        {
            FileName = "dotnet",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add(fixture.VerifierAssemblyPath);
        start.ArgumentList.Add("--bag");
        start.ArgumentList.Add(fixture.BundleRoot);
        start.ArgumentList.Add("--report");
        start.ArgumentList.Add(report);
        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("Could not launch verifier.");
        var standardOutput = await process.StandardOutput.ReadToEndAsync();
        var standardError = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, standardOutput, standardError);
    }

    private static void AssertDirectoriesEqual(string left, string right)
    {
        var leftInventory = Inventory(left);
        var rightInventory = Inventory(right);
        Assert.Equal(leftInventory, rightInventory);
    }

    private static IReadOnlyList<string> Inventory(string root) =>
        Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Select(
                path => Relative(root, path) + ":"
                    + StrictBundleTestFixture.FileSha(path))
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    private static void CreateJunction(string link, string target)
    {
        var start = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/d /c mklink /J \"{link}\" \"{target}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("Could not launch junction creator.");
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);
        Assert.True((File.GetAttributes(link) & FileAttributes.ReparsePoint) != 0);
    }

    private static string Relative(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');

    private static string Format(StrictBundleVerificationResult result) =>
        result.Issue is null
            ? "valid"
            : $"{result.Issue.Stage}:{result.Issue.Code}:{result.Issue.RelativePath}:" +
                result.Issue.SafeMessage;
}
