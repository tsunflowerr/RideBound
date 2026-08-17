using System.Security.Cryptography;
using RideBound.Benchmarking.Bundles;
using RideBound.Benchmarking.Contracts;

namespace RideBound.Benchmarking.Claims;

public sealed record ArtifactClaimCaveat(
    string CaveatId,
    string RelativePath,
    string ExactText);

public sealed record ArtifactClaimForbiddenRule(
    string RuleId,
    string Category,
    IReadOnlyList<string> Phrases);

public sealed record ArtifactClaimProfile(
    string SchemaVersion,
    string ProfileId,
    string DecisionId,
    string NormalizationId,
    long MaxSurfaceUtf8Bytes,
    IReadOnlyList<string> EvidenceUris,
    IReadOnlyList<string> ScannedSelections,
    IReadOnlyList<ArtifactClaimCaveat> RequiredCaveats,
    IReadOnlyList<ArtifactClaimForbiddenRule> ForbiddenRules);

public sealed record ArtifactClaimBoundaryFlags(
    bool ConfirmatoryEvidence,
    bool IndependentTeamEvidence,
    bool AcmBadgeEvidence,
    bool PublicTripPreferenceEvidence,
    bool PublicTripSatisfactionEvidence,
    bool ResourceMeasurementsLocalControlsOnly,
    bool SameTeamCleanProcessOnly);

public sealed record ArtifactClaimWitness(
    string Code,
    string RuleId,
    string Category,
    string RelativePath,
    string Selector,
    string OriginalExcerpt,
    string NormalizedWitness);

public sealed record ArtifactClaimCheckReport(
    string SchemaVersion,
    string ProfileId,
    string ProfileSha256,
    string DecisionId,
    string NormalizationId,
    string Status,
    ArtifactClaimBoundaryFlags BoundaryFlags,
    IReadOnlyList<string> ScannedSelections,
    IReadOnlyList<string> SatisfiedCaveatIds,
    IReadOnlyList<ArtifactClaimWitness> Witnesses);

public sealed record ArtifactClaimCheckInput(
    ReadOnlyMemory<byte> ReadmeBytes,
    LogicalBundleManifest Manifest,
    BenchmarkPlan Plan,
    BundlePackagingVerificationReport VerificationReport,
    BundleMachineProvenance MachineProvenance,
    BundleSourceInventory SourceInventory);

public sealed record ArtifactClaimCheckResult(
    bool IsValid,
    ArtifactClaimCheckReport Report);

public static class ArtifactClaimProfileCatalog
{
    private static readonly ArtifactClaimProfile Profile = CreateProfile();
    private static readonly byte[] CanonicalBytes = BundleEvidenceJson.Encode(Profile);

    public static string V1Sha256 { get; } =
        Convert.ToHexStringLower(SHA256.HashData(CanonicalBytes));

    public static ArtifactClaimProfile GetV1() =>
        BundleEvidenceJson.DecodeExact<ArtifactClaimProfile>(CanonicalBytes);

    public static byte[] GetV1CanonicalBytes() => [.. CanonicalBytes];

    private static ArtifactClaimProfile CreateProfile()
    {
        var profile = new ArtifactClaimProfile(
            "1.0.0",
            "wp6-mechanical-only-v1",
            "ADR-032",
            "wp6-claim-nfkc-casefold-confusable-v1",
            65_536,
            [
                "https://pmc.ncbi.nlm.nih.gov/articles/PMC3383002/",
                "https://www.acm.org/publications/badging-terms",
                "https://www.nationalacademies.org/read/25303/chapter/2",
                "https://www.nature.com/articles/s41562-016-0021",
                "https://www.unicode.org/reports/tr39/",
            ],
            [
                "README.md#document",
                "data/benchmark-plan.json#claimProfileId",
                "data/benchmark-plan.json#evidenceClass",
                "data/benchmark-plan.json#exclusionRuleSetId",
                "data/benchmark-plan.json#failureRuleSetId",
                "data/benchmark-plan.json#planId",
                "data/benchmark-plan.json#resourceProfileId",
                "data/bundle-manifest.json#bundleId",
                "data/bundle-manifest.json#claimProfileId",
                "data/bundle-manifest.json#evidenceClass",
                "data/provenance/machine.json#containerImageDigest",
                "data/provenance/machine.json#fileSystemType",
                "data/provenance/machine.json#powerModeNote",
                "data/source-inventory/repository.json#gitDirty",
                "data/verification-report.json#status",
                "data/verification-report.json#verificationOrderId",
                "data/verification-report.json#verifierId",
            ],
            [
                new(
                    "boundary.evidence-class",
                    "README.md",
                    "Evidence class: mechanical/development."),
                new(
                    "boundary.independent-claims",
                    "README.md",
                    "No effectiveness, non-inferiority, production SLA, ACM badge, independent reproducibility or replicability claim."),
                new(
                    "boundary.non-confirmatory",
                    "README.md",
                    "This bundle is non-confirmatory mechanical evidence."),
                new(
                    "boundary.public-trip-semantics",
                    "README.md",
                    "Public trip data does not contain observed commitment preference or satisfaction."),
                new(
                    "boundary.resource-controls",
                    "README.md",
                    "Resource measurements are local experiment controls only, not production latency, throughput or SLA evidence."),
                new(
                    "boundary.same-team",
                    "README.md",
                    "Same-team clean-process repeatability only."),
            ],
            [
                new(
                    "acm-badge",
                    "external-certification",
                    [
                        "acm artifact available",
                        "acm artifact evaluated",
                        "acm badge",
                        "artifact evaluated badge",
                        "results replicated badge",
                        "results reproduced badge",
                    ]),
                new(
                    "confirmatory",
                    "evidence-class",
                    ["confirmation study", "confirmatory", "hypothesis confirmed"]),
                new(
                    "effectiveness",
                    "scientific-effect",
                    [
                        "better performance",
                        "effective",
                        "effectiveness",
                        "improved service",
                        "improves service",
                        "operationally better",
                        "outperforms",
                        "performs better",
                        "superior",
                    ]),
                new(
                    "independent-reproduction",
                    "external-reproduction",
                    [
                        "independent reproducibility",
                        "independent reproduction",
                        "independently reproduced",
                        "reproduced",
                        "results reproduced",
                    ]),
                new(
                    "non-inferiority",
                    "statistical-conclusion",
                    ["non inferior", "non inferiority", "noninferior", "noninferiority"]),
                new(
                    "novelty",
                    "novelty",
                    [
                        "first ever",
                        "first method",
                        "first of its kind",
                        "first system",
                        "novel",
                        "novel eta",
                        "sota",
                        "state of the art",
                    ]),
                new(
                    "production-readiness",
                    "deployment-readiness",
                    [
                        "deployment ready",
                        "production capable",
                        "production grade",
                        "production ready",
                        "production use",
                        "ready for production",
                        "ready to deploy",
                        "real world ready",
                    ]),
                new(
                    "replication",
                    "external-replication",
                    ["independent replication", "replicability", "replicated", "results replicated"]),
                new(
                    "sla",
                    "service-level",
                    ["latency guarantee", "meets sla", "service level agreement", "sla", "throughput guarantee"]),
                new(
                    "user-satisfaction",
                    "unobserved-user-outcome",
                    [
                        "passenger satisfaction",
                        "rider preference",
                        "rider satisfaction",
                        "user preference",
                        "user satisfaction",
                        "users prefer",
                    ]),
            ]);

        Validate(profile);
        return profile;
    }

    private static void Validate(ArtifactClaimProfile profile)
    {
        if (profile.ProfileId != "wp6-mechanical-only-v1"
            || profile.DecisionId != "ADR-032"
            || profile.MaxSurfaceUtf8Bytes is <= 0 or > 1_048_576
            || !IsSortedUnique(profile.EvidenceUris)
            || !IsSortedUnique(profile.ScannedSelections)
            || !IsSortedUnique(profile.RequiredCaveats.Select(value => value.CaveatId))
            || !IsSortedUnique(profile.ForbiddenRules.Select(value => value.RuleId))
            || profile.RequiredCaveats.Any(
                value => value.RelativePath != "README.md"
                    || string.IsNullOrWhiteSpace(value.ExactText))
            || profile.ForbiddenRules.Any(
                value => string.IsNullOrWhiteSpace(value.Category)
                    || !IsSortedUnique(value.Phrases)))
        {
            throw new InvalidOperationException("The source-locked WP6 claim profile is invalid.");
        }
    }

    private static bool IsSortedUnique(IEnumerable<string> values)
    {
        var materialized = values.ToArray();
        return materialized.Length > 0
            && materialized.SequenceEqual(materialized.Order(StringComparer.Ordinal))
            && materialized.Distinct(StringComparer.Ordinal).Count() == materialized.Length
            && materialized.All(value => !string.IsNullOrWhiteSpace(value));
    }
}
