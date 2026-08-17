using System.Globalization;
using System.Text;
using RideBound.Benchmarking.Contracts;

namespace RideBound.Benchmarking.Claims;

public static class ArtifactClaimChecker
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static ArtifactClaimCheckResult Check(ArtifactClaimCheckInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Manifest);
        ArgumentNullException.ThrowIfNull(input.Plan);
        ArgumentNullException.ThrowIfNull(input.VerificationReport);
        ArgumentNullException.ThrowIfNull(input.MachineProvenance);
        ArgumentNullException.ThrowIfNull(input.SourceInventory);

        var profile = ArtifactClaimProfileCatalog.GetV1();
        var witnesses = new List<ArtifactClaimWitness>();
        var satisfiedCaveats = new List<string>();
        var readme = DecodeReadme(input.ReadmeBytes.Span, profile, witnesses);
        var maskedReadme = MaskRequiredCaveats(
            readme,
            profile.RequiredCaveats,
            satisfiedCaveats,
            witnesses);

        ValidateTypedBoundary(input, witnesses);
        var surfaces = CreateSurfaces(input, maskedReadme);

        foreach (var surface in surfaces)
        {
            ScanSurface(surface, profile, witnesses);
        }

        var flags = new ArtifactClaimBoundaryFlags(
            ConfirmatoryEvidence: false,
            IndependentTeamEvidence: false,
            AcmBadgeEvidence: false,
            PublicTripPreferenceEvidence: false,
            PublicTripSatisfactionEvidence: false,
            ResourceMeasurementsLocalControlsOnly: true,
            SameTeamCleanProcessOnly: true);
        var isValid = witnesses.Count == 0;
        var report = new ArtifactClaimCheckReport(
            "1.0.0",
            profile.ProfileId,
            ArtifactClaimProfileCatalog.V1Sha256,
            profile.DecisionId,
            profile.NormalizationId,
            isValid ? "passed" : "failed",
            flags,
            profile.ScannedSelections,
            satisfiedCaveats.Order(StringComparer.Ordinal).ToArray(),
            witnesses);
        return new ArtifactClaimCheckResult(isValid, report);
    }

    private static string DecodeReadme(
        ReadOnlySpan<byte> bytes,
        ArtifactClaimProfile profile,
        ICollection<ArtifactClaimWitness> witnesses)
    {
        if (bytes.Length == 0
            || bytes.Length > profile.MaxSurfaceUtf8Bytes
            || bytes[0] is 0xef && bytes.Length >= 3 && bytes[1] == 0xbb && bytes[2] == 0xbf
            || bytes[^1] != (byte)'\n'
            || bytes.Contains((byte)'\r'))
        {
            witnesses.Add(
                Witness(
                    "claim.surface-framing",
                    "surface-framing",
                    "claim-surface",
                    "README.md",
                    "document",
                    "README framing is outside the profile.",
                    "strict-utf8-lf"));
            return string.Empty;
        }

        try
        {
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            witnesses.Add(
                Witness(
                    "claim.surface-utf8",
                    "surface-utf8",
                    "claim-surface",
                    "README.md",
                    "document",
                    "README is not strict UTF-8.",
                    "strict-utf8"));
            return string.Empty;
        }
    }

    private static string MaskRequiredCaveats(
        string readme,
        IEnumerable<ArtifactClaimCaveat> caveats,
        ICollection<string> satisfiedCaveats,
        ICollection<ArtifactClaimWitness> witnesses)
    {
        var masked = readme;

        foreach (var caveat in caveats)
        {
            var expectedLine = caveat.ExactText + "\n";
            var first = readme.IndexOf(expectedLine, StringComparison.Ordinal);
            var last = readme.LastIndexOf(expectedLine, StringComparison.Ordinal);

            if (first < 0 || first != last)
            {
                witnesses.Add(
                    Witness(
                        "claim.caveat-missing",
                        caveat.CaveatId,
                        "required-caveat",
                        caveat.RelativePath,
                        "document",
                        "Required caveat is missing or duplicated.",
                        caveat.CaveatId));
                continue;
            }

            satisfiedCaveats.Add(caveat.CaveatId);
            masked = masked.Replace(
                caveat.ExactText,
                new string(' ', caveat.ExactText.Length),
                StringComparison.Ordinal);
        }

        return masked;
    }

    private static void ValidateTypedBoundary(
        ArtifactClaimCheckInput input,
        ICollection<ArtifactClaimWitness> witnesses)
    {
        if (input.Manifest.EvidenceClass is not (EvidenceClass.Mechanical or EvidenceClass.Development)
            || input.Plan.EvidenceClass is not (EvidenceClass.Mechanical or EvidenceClass.Development))
        {
            witnesses.Add(
                Witness(
                    "claim.evidence-class-forbidden",
                    "boundary.evidence-class",
                    "evidence-class",
                    "data/bundle-manifest.json",
                    "evidenceClass",
                    input.Manifest.EvidenceClass.ToString(),
                    "mechanical-or-development-only"));
        }

        if (input.Manifest.ClaimProfileId != "wp6-mechanical-only-v1"
            || input.Plan.ClaimProfileId != "wp6-mechanical-only-v1")
        {
            witnesses.Add(
                Witness(
                    "claim.profile-id-forbidden",
                    "boundary.profile-id",
                    "claim-profile",
                    "data/bundle-manifest.json",
                    "claimProfileId",
                    input.Manifest.ClaimProfileId,
                    "wp6-mechanical-only-v1"));
        }

        if (input.VerificationReport.VerifierId != "ridebound-strict-bagit-verifier-v1"
            || input.VerificationReport.VerificationOrderId
                != "wp6-bundle-verification-order-v1"
            || input.VerificationReport.Status != "verified-before-atomic-publication")
        {
            witnesses.Add(
                Witness(
                    "claim.report-label-invalid",
                    "boundary.verification-report",
                    "report-label",
                    "data/verification-report.json",
                    "status",
                    input.VerificationReport.Status,
                    "packaging-verification-label"));
        }
    }

    private static IReadOnlyList<ClaimSurface> CreateSurfaces(
        ArtifactClaimCheckInput input,
        string maskedReadme) =>
        [
            new("README.md", "document", maskedReadme),
            new("data/benchmark-plan.json", "claimProfileId", input.Plan.ClaimProfileId),
            new("data/benchmark-plan.json", "evidenceClass", EvidenceClassWire(input.Plan.EvidenceClass)),
            new("data/benchmark-plan.json", "exclusionRuleSetId", input.Plan.ExclusionRuleSetId),
            new("data/benchmark-plan.json", "failureRuleSetId", input.Plan.FailureRuleSetId),
            new("data/benchmark-plan.json", "planId", input.Plan.PlanId),
            new("data/benchmark-plan.json", "resourceProfileId", input.Plan.ResourceProfileId),
            new("data/bundle-manifest.json", "bundleId", input.Manifest.BundleId),
            new("data/bundle-manifest.json", "claimProfileId", input.Manifest.ClaimProfileId),
            new("data/bundle-manifest.json", "evidenceClass", EvidenceClassWire(input.Manifest.EvidenceClass)),
            new("data/provenance/machine.json", "containerImageDigest", input.MachineProvenance.ContainerImageDigest),
            new("data/provenance/machine.json", "fileSystemType", input.MachineProvenance.FileSystemType),
            new("data/provenance/machine.json", "powerModeNote", input.MachineProvenance.PowerModeNote),
            new("data/source-inventory/repository.json", "gitDirty", input.SourceInventory.GitDirty ? "true" : "false"),
            new("data/verification-report.json", "status", input.VerificationReport.Status),
            new("data/verification-report.json", "verificationOrderId", input.VerificationReport.VerificationOrderId),
            new("data/verification-report.json", "verifierId", input.VerificationReport.VerifierId),
        ];

    private static void ScanSurface(
        ClaimSurface surface,
        ArtifactClaimProfile profile,
        ICollection<ArtifactClaimWitness> witnesses)
    {
        if (Encoding.UTF8.GetByteCount(surface.Value) > profile.MaxSurfaceUtf8Bytes)
        {
            witnesses.Add(
                Witness(
                    "claim.surface-too-large",
                    "boundary.surface-size",
                    "claim-surface",
                    surface.RelativePath,
                    surface.Selector,
                    "Claim surface exceeds the bounded profile.",
                    "surface-size"));
            return;
        }

        var skeleton = BuildSkeleton(surface.Value, out var unsafeCategory);

        if (unsafeCategory is not null)
        {
            witnesses.Add(
                Witness(
                    "claim.obfuscation-character",
                    "boundary.unicode-obfuscation",
                    "unicode-obfuscation",
                    surface.RelativePath,
                    surface.Selector,
                    Excerpt(surface.Value),
                    unsafeCategory));
        }

        foreach (var rule in profile.ForbiddenRules)
        {
            foreach (var phrase in rule.Phrases)
            {
                var phraseSkeleton = BuildSkeleton(phrase, out _);

                if (!ContainsPhrase(skeleton.Separated, phraseSkeleton.Separated)
                    && !ContainsPhrase(skeleton.PunctuationJoined, phraseSkeleton.PunctuationJoined))
                {
                    continue;
                }

                witnesses.Add(
                    Witness(
                        "claim.forbidden." + rule.RuleId,
                        rule.RuleId,
                        rule.Category,
                        surface.RelativePath,
                        surface.Selector,
                        Excerpt(surface.Value),
                        phraseSkeleton.Separated));
                break;
            }
        }
    }

    private static ClaimSkeleton BuildSkeleton(string value, out string? unsafeCategory)
    {
        var separated = new StringBuilder(value.Length);
        var punctuationJoined = new StringBuilder(value.Length);
        unsafeCategory = null;
        var normalized = value.Normalize(NormalizationForm.FormKC)
            .Normalize(NormalizationForm.FormD);

        foreach (var rawRune in normalized.EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rawRune);

            if (category is UnicodeCategory.NonSpacingMark
                or UnicodeCategory.SpacingCombiningMark
                or UnicodeCategory.EnclosingMark)
            {
                continue;
            }

            if (Rune.IsWhiteSpace(rawRune))
            {
                AppendSpace(separated);
                AppendSpace(punctuationJoined);
                continue;
            }

            if (category is UnicodeCategory.Control
                or UnicodeCategory.Format
                or UnicodeCategory.Surrogate
                or UnicodeCategory.PrivateUse
                or UnicodeCategory.OtherNotAssigned)
            {
                unsafeCategory ??= category.ToString();
                AppendSpace(separated);
                continue;
            }

            var mapped = MapToAscii(Rune.ToLowerInvariant(rawRune));

            if (mapped is not null)
            {
                separated.Append(mapped.Value);
                punctuationJoined.Append(mapped.Value);
                continue;
            }

            AppendSpace(separated);

            if (category is UnicodeCategory.UppercaseLetter
                or UnicodeCategory.LowercaseLetter
                or UnicodeCategory.TitlecaseLetter
                or UnicodeCategory.ModifierLetter
                or UnicodeCategory.OtherLetter
                or UnicodeCategory.DecimalDigitNumber
                or UnicodeCategory.LetterNumber
                or UnicodeCategory.OtherNumber)
            {
                AppendSpace(punctuationJoined);
            }
        }

        return new ClaimSkeleton(
            CollapseSpaces(separated),
            CollapseSpaces(punctuationJoined));
    }

    private static char? MapToAscii(Rune value)
    {
        if (value.Value is >= 'a' and <= 'z' or >= '0' and <= '9')
        {
            return (char)value.Value;
        }

        return value.Value switch
        {
            0x00f0 or 0x0111 or 0x0257 or 0x03b4 or 0x0501 => 'd',
            0x00f8 or 0x03bf or 0x043e => 'o',
            0x0127 or 0x03b7 or 0x04bb => 'h',
            0x0131 or 0x03b9 or 0x0456 => 'i',
            0x0142 or 0x04cf => 'l',
            0x0167 or 0x03c4 or 0x0442 => 't',
            0x03b1 or 0x0430 => 'a',
            0x03b2 or 0x0432 => 'b',
            0x03b5 or 0x0435 => 'e',
            0x03ba or 0x043a => 'k',
            0x03bc or 0x043c => 'm',
            0x03bd => 'v',
            0x03c1 or 0x0440 => 'p',
            0x03c2 or 0x03c3 or 0x0441 or 0x0455 => 's',
            0x03c5 or 0x057d => 'u',
            0x03c7 or 0x0445 => 'x',
            0x03b3 or 0x0443 => 'y',
            0x03b6 => 'z',
            0x0458 => 'j',
            0x051b => 'q',
            0x051d => 'w',
            _ => null,
        };
    }

    private static bool ContainsPhrase(string value, string phrase) =>
        phrase.Length > 0
        && (" " + value + " ").Contains(" " + phrase + " ", StringComparison.Ordinal);

    private static void AppendSpace(StringBuilder builder)
    {
        if (builder.Length > 0 && builder[^1] != ' ')
        {
            builder.Append(' ');
        }
    }

    private static string CollapseSpaces(StringBuilder builder) =>
        builder.ToString().Trim();

    private static ArtifactClaimWitness Witness(
        string code,
        string ruleId,
        string category,
        string path,
        string selector,
        string excerpt,
        string normalizedWitness) =>
        new(
            code,
            ruleId,
            category,
            path,
            selector,
            Excerpt(excerpt),
            normalizedWitness);

    private static string Excerpt(string value)
    {
        var singleLine = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return singleLine.Length <= 96 ? singleLine : singleLine[..96];
    }

    private static string EvidenceClassWire(EvidenceClass value) => value switch
    {
        EvidenceClass.Mechanical => "mechanical",
        EvidenceClass.Development => "development",
        EvidenceClass.Pilot => "pilot",
        EvidenceClass.Confirmatory => "confirmatory",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private sealed record ClaimSurface(string RelativePath, string Selector, string Value);

    private sealed record ClaimSkeleton(string Separated, string PunctuationJoined);
}
