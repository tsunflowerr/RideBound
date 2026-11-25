using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using RideBound.Algorithms.Candidates;
using RideBound.Algorithms.Policies;
using RideBound.Algorithms.Shareability;
using RideBound.Application.Commitments;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Validation;
using static RideBound.Runner.Configuration.JsonConfig;
using static RideBound.Runner.Configuration.RollingCostSolverConfiguration;

namespace RideBound.Runner.Configuration;

public sealed class C1RunnerConfiguration : ICommitmentPolicyProvider, IStopDistanceLookup
{
    public const string PolicyId = RidePoolingPolicyRegistry.RideBoundHardVector;

    private static readonly string[] DispatchFields =
        ["configurationVersion", "policyId", "policyVersion", "candidateGeneration", "solverExecution"];
    private static readonly string[] DispatchOptions =
        ["lateBoarding", "shadowAllocation", "planningForecast", "auditHash"];

    private readonly CommitmentPolicyCatalog _policies;
    private readonly Dictionary<(NodeId From, NodeId To), long> _distances;

    private C1RunnerConfiguration(
        Sha256Hex contentHash,
        string policyVersion,
        CandidateGenerationOptions generation,
        SolverBackedRidePoolingPolicyOptions solver,
        CommitmentPolicy[] policies,
        Dictionary<(NodeId From, NodeId To), long> distances,
        bool recordLatePickups,
        ShadowAllocationInputs? shadowAllocation,
        TravelTimeForecast? planningForecast,
        bool usesLightAuditHash)
    {
        ContentHash = contentHash;
        PolicyVersion = policyVersion;
        CandidateGeneration = generation;
        Solver = solver;
        RecordLatePickups = recordLatePickups;
        ShadowAllocation = shadowAllocation;
        PlanningForecast = planningForecast;
        UsesLightAuditHash = usesLightAuditHash;
        _policies = new CommitmentPolicyCatalog(policies);
        _distances = distances;
    }

    public Sha256Hex ContentHash { get; }
    public string PolicyVersion { get; }
    public CandidateGenerationOptions CandidateGeneration { get; }
    public SolverBackedRidePoolingPolicyOptions Solver { get; }
    public bool RecordLatePickups { get; }
    public ShadowAllocationInputs? ShadowAllocation { get; }
    public TravelTimeForecast? PlanningForecast { get; }
    public bool UsesLightAuditHash { get; }

    public bool Matches(RunManifestIdentity manifest) =>
        manifest.PolicyId == PolicyId
        && manifest.PolicyVersion == PolicyVersion
        && manifest.PolicyConfigurationHash == ContentHash;

    public static C1RunnerConfiguration Decode(ReadOnlySpan<byte> utf8Json)
    {
        var canonical = CanonicalJson.Canonicalize(utf8Json);
        using var document = JsonDocument.Parse(canonical);
        var root = document.RootElement;
        RequireObject(root, "$", [.. DispatchFields, "policies", "stopDistances"], DispatchOptions);
        return Read(root, root, ConfigurationHash(canonical));
    }

    /// <summary>Reads the C1 commitment settings and dispatch settings from separate files.</summary>
    public static C1RunnerConfiguration DecodeSeparated(
        ReadOnlySpan<byte> commitmentJson, ReadOnlySpan<byte> dispatchJson)
    {
        var canonicalCommitment = CanonicalJson.Canonicalize(commitmentJson);
        var canonicalDispatch = CanonicalJson.Canonicalize(dispatchJson);
        using var commitment = JsonDocument.Parse(canonicalCommitment);
        using var dispatch = JsonDocument.Parse(canonicalDispatch);
        RequireFields(commitment.RootElement, "commitment", "configurationVersion", "policies", "stopDistances");
        RequireObject(dispatch.RootElement, "dispatch", DispatchFields, DispatchOptions);
        if (Text(commitment.RootElement, "configurationVersion") != "1.0.0")
        {
            throw new InvalidDataException("Commitment configurationVersion must be 1.0.0.");
        }

        var binding = Wp4PolicyConfigurationBinding.Calculate(
            ConfigurationHash(canonicalCommitment), ConfigurationHash(canonicalDispatch));
        return Read(dispatch.RootElement, commitment.RootElement, binding);
    }

    private static Sha256Hex ConfigurationHash(ReadOnlySpan<byte> canonical)
    {
        Sha256Hex.TryCreate(Convert.ToHexStringLower(SHA256.HashData(canonical)), out var hash);
        return hash!;
    }

    private static C1RunnerConfiguration Read(JsonElement root, JsonElement commitment, Sha256Hex contentHash)
    {
        if (Text(root, "configurationVersion") != "1.0.0" || Text(root, "policyId") != PolicyId)
        {
            throw new InvalidDataException("C1 requires configurationVersion 1.0.0 and policyId ridebound-hard-vector.");
        }

        var policyVersion = Text(root, "policyVersion");
        if (!OpaqueIdentifier.IsValid(policyVersion))
        {
            throw new InvalidDataException("policyVersion must be a valid protocol identifier.");
        }

        var policies = Array(commitment, "policies").Select(ReadPolicy).ToArray();
        if (policies.Length == 0)
        {
            throw new InvalidDataException("At least one commitment policy is required.");
        }

        var hasShadow = root.TryGetProperty("shadowAllocation", out var shadowElement);
        if (policies.Any(policy => policy.DropEtaShadowAllocation != hasShadow))
        {
            throw new InvalidDataException(
                "shadowAllocation and dropEtaShadowAllocation on every commitment policy must be configured together.");
        }

        var shadow = hasShadow ? ShadowAllocationConfiguration.Read(shadowElement) : null;
        var generation = ReadCandidateGeneration(root.GetProperty("candidateGeneration"), allowRelocation: true);
        if ((generation.WaitingRiderRelocation is not null) != policies.Any(policy => policy.WaitingRiderRelocation))
        {
            throw new InvalidDataException(
                "Candidate relocation and at least one relocation-enabled rider policy must be configured together.");
        }

        var distances = new Dictionary<(NodeId From, NodeId To), long>();
        foreach (var item in Array(commitment, "stopDistances"))
        {
            RequireFields(item, "stopDistances[]", "fromNodeId", "toNodeId", "distanceMm");
            var from = new NodeId(Text(item, "fromNodeId"));
            var to = new NodeId(Text(item, "toNodeId"));
            var distance = Integer(item, "distanceMm", 0, DomainLimits.MaxCanonicalInteger);
            if (from == to || !distances.TryAdd((from, to), distance))
            {
                throw new InvalidDataException("Stop-distance arcs must be unique and join different nodes.");
            }
        }

        var recordLatePickups = root.TryGetProperty("lateBoarding", out _)
            ? Text(root, "lateBoarding") switch
            {
                "record-v1" => true,
                "reject" => false,
                _ => throw new InvalidDataException("lateBoarding must be record-v1 or reject."),
            }
            : false;

        var forecast = root.TryGetProperty("planningForecast", out var forecastElement)
            ? ReadPlanningForecast(forecastElement)
            : null;
        var usesLightAuditHash = root.TryGetProperty("auditHash", out _)
            ? Text(root, "auditHash") switch
            {
                "full-v1" => false,
                "light-v1" => true,
                _ => throw new InvalidDataException("auditHash must be full-v1 or light-v1."),
            }
            : false;
        var solver = new SolverBackedRidePoolingPolicyOptions(
            RidePoolingPolicyKind.RideBoundHardVector,
            ReadExecutionBudget(root.GetProperty("solverExecution")));
        return new C1RunnerConfiguration(
            contentHash, policyVersion, generation, solver, policies, distances,
            recordLatePickups, shadow, forecast, usesLightAuditHash);
    }

    public bool TryGetPolicy(string policyId, out CommitmentPolicy policy) =>
        _policies.TryGetPolicy(policyId, out policy);

    public bool TryGetDistanceMillimeters(NodeId from, NodeId to, out long distance)
    {
        if (from == to)
        {
            distance = 0;
            return true;
        }

        return _distances.TryGetValue((from, to), out distance);
    }

    private static TravelTimeForecast ReadPlanningForecast(JsonElement element)
    {
        RequireFields(element, "planningForecast", "profile", "binMs");
        var bin = Integer(element, "binMs", TravelTimeForecast.MinimumBinMilliseconds, DomainLimits.MaxCanonicalInteger);
        // Canonical JSON uses integer numbers. Forecast factors use invariant decimal strings.
        var profile = Array(element, "profile")
            .Select(value =>
                value.ValueKind == JsonValueKind.String
                && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var factor)
                && double.IsFinite(factor)
                && factor > 0
                    ? factor
                    : throw new InvalidDataException("planningForecast.profile must contain finite positive decimal strings."))
            .ToArray();
        if (profile.Length == 0)
        {
            throw new InvalidDataException("planningForecast.profile must be non-empty.");
        }

        return new TravelTimeForecast(profile, bin);
    }

    private static CommitmentPolicy ReadPolicy(JsonElement element)
    {
        RequireObject(element, "policy",
            ["policyId", "limits", "materialRevisionRule"], "finalConfirmationLocks", "ratchetLocks",
            "waitingRiderRelocation", "dropEtaBudgetSlackPermille", "dropEtaDeadlineSlackPermille", "dropEtaShadowAllocation");
        var revision = element.GetProperty("materialRevisionRule");
        RequireObject(revision, "materialRevisionRule", [], "rawEtaThresholdMs", "displayBucketWidthMs");
        return new CommitmentPolicy(
            Text(element, "policyId"),
            Array(element, "limits").Select(ReadLimit),
            new MaterialRevisionRule(
                OptionalPositiveInteger(revision, "rawEtaThresholdMs"),
                OptionalPositiveInteger(revision, "displayBucketWidthMs")),
            finalConfirmationLocks: ReadLocks(element, "finalConfirmationLocks"),
            ratchetLocks: ReadLocks(element, "ratchetLocks"),
            dropEtaBudgetSlackPermille: (int?)OptionalPositiveInteger(element, "dropEtaBudgetSlackPermille", 1000),
            dropEtaDeadlineSlackPermille: (int?)OptionalPositiveInteger(element, "dropEtaDeadlineSlackPermille", 1000),
            waitingRiderRelocation: OptionalBoolean(element, "waitingRiderRelocation"),
            dropEtaShadowAllocation: OptionalBoolean(element, "dropEtaShadowAllocation"));
    }

    private static CommitmentDimensionLimit ReadLimit(JsonElement element)
    {
        RequireObject(element, "limit", ["dimension", "applicablePhases"], "hardLimit");
        var dimensionText = Text(element, "dimension");
        var dimension = CommitmentDimensionVocabulary.Ordered
            .Cast<CommitmentDimension?>()
            .SingleOrDefault(
                value => CommitmentDimensionVocabulary.ToProtocolValue(
                        value!.Value)
                    == dimensionText)
            ?? throw new InvalidDataException(
                $"Unknown commitment dimension '{dimensionText}'.");
        var phases = CommitmentPhase.None;

        foreach (var value in Array(element, "applicablePhases"))
        {
            var parsed = (value.ValueKind == JsonValueKind.String ? value.GetString() : null) switch
            {
                "accepted" => CommitmentPhase.Accepted,
                "waitingPickup" => CommitmentPhase.WaitingPickup,
                "onboard" => CommitmentPhase.Onboard,
                _ => throw new InvalidDataException(
                    "Unknown commitment applicable phase."),
            };

            if ((phases & parsed) != 0)
            {
                throw new InvalidDataException(
                    $"Duplicate commitment phase '{value.GetString()}'.");
            }

            phases |= parsed;
        }

        return new CommitmentDimensionLimit(
            dimension,
            element.TryGetProperty("hardLimit", out _)
                ? Integer(element, "hardLimit", 0, DomainLimits.MaxCanonicalInteger)
                : null,
            phases);
    }

    private static PromiseLock ReadLocks(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out _))
        {
            return PromiseLock.None;
        }

        var locks = PromiseLock.None;

        foreach (var value in Array(element, propertyName))
        {
            var parsed = (value.ValueKind == JsonValueKind.String ? value.GetString() : null) switch
            {
                "vehicle" => PromiseLock.Vehicle,
                "pickupStop" => PromiseLock.PickupStop,
                "dropStop" => PromiseLock.DropStop,
                "pickupEta" => PromiseLock.PickupEta,
                "dropEta" => PromiseLock.DropEta,
                _ => throw new InvalidDataException("Unknown promise lock."),
            };

            if ((locks & parsed) != 0)
            {
                throw new InvalidDataException(
                    $"Duplicate promise lock '{value.GetString()}'.");
            }

            locks |= parsed;
        }

        return locks;
    }
}
