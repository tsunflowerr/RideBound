using System.Collections.Frozen;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;

namespace RideBound.Domain.Incidents;

public readonly record struct IncidentId
{
    public IncidentId(string value)
    {
        Value = DomainIdentifier.Require(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public sealed record OperationalIncident
{
    public OperationalIncident(
        IncidentId id,
        string reasonCode,
        IEnumerable<VehicleId> affectedVehicleIds,
        IEnumerable<RequestId> affectedRequestIds,
        long openedEventSequence,
        SimTime openedAt,
        long? resolvedEventSequence = null,
        SimTime? resolvedAt = null)
    {
        ArgumentNullException.ThrowIfNull(affectedVehicleIds);
        ArgumentNullException.ThrowIfNull(affectedRequestIds);

        if (openedEventSequence is < 1 or > DomainLimits.MaxCanonicalInteger)
        {
            throw new ArgumentOutOfRangeException(nameof(openedEventSequence));
        }

        if ((resolvedEventSequence is null) != (resolvedAt is null)
            || resolvedEventSequence is not null
                && (resolvedEventSequence <= openedEventSequence
                    || resolvedEventSequence > DomainLimits.MaxCanonicalInteger)
            || resolvedAt is not null
                && resolvedAt.Value.Milliseconds < openedAt.Milliseconds)
        {
            throw new ArgumentException(
                "Incident resolution must be a later, complete event/time pair.");
        }

        var vehicles = Normalize(affectedVehicleIds, value => value.Value);
        var requests = Normalize(affectedRequestIds, value => value.Value);

        if (vehicles.Length == 0)
        {
            throw new ArgumentException(
                "An operational incident must affect at least one vehicle.",
                nameof(affectedVehicleIds));
        }

        Id = id;
        ReasonCode = DomainIdentifier.Require(reasonCode, nameof(reasonCode));
        AffectedVehicleIds = Array.AsReadOnly(vehicles);
        AffectedRequestIds = Array.AsReadOnly(requests);
        OpenedEventSequence = openedEventSequence;
        OpenedAt = openedAt;
        ResolvedEventSequence = resolvedEventSequence;
        ResolvedAt = resolvedAt;
    }

    public IncidentId Id { get; }

    public string ReasonCode { get; }

    public IReadOnlyList<VehicleId> AffectedVehicleIds { get; }

    public IReadOnlyList<RequestId> AffectedRequestIds { get; }

    public long OpenedEventSequence { get; }

    public SimTime OpenedAt { get; }

    public long? ResolvedEventSequence { get; }

    public SimTime? ResolvedAt { get; }

    public bool IsOpen => ResolvedEventSequence is null;

    public OperationalIncident Resolve(long eventSequence, SimTime resolvedAt) =>
        new(
            Id,
            ReasonCode,
            AffectedVehicleIds,
            AffectedRequestIds,
            OpenedEventSequence,
            OpenedAt,
            eventSequence,
            resolvedAt);

    private static T[] Normalize<T>(
        IEnumerable<T> values,
        Func<T, string> identifier)
    {
        var materialized = values
            .OrderBy(identifier, StringComparer.Ordinal)
            .ToArray();

        if (materialized.Select(identifier).Distinct(StringComparer.Ordinal).Count()
            != materialized.Length)
        {
            throw new ArgumentException("Incident entity sets cannot contain duplicates.");
        }

        return materialized;
    }
}

public sealed record CommitmentBreachRecord
{
    public CommitmentBreachRecord(
        string breachId,
        IncidentId incidentId,
        RequestId requestId,
        PublishedPromise previousPromise,
        PromiseProjection exogenousProjection,
        PromiseProjection safetyProjection,
        ThreeWayPromiseDelta deltas,
        CommitmentVector budgetBefore,
        CommitmentVector attemptedBudgetAfter,
        IEnumerable<string> witnessCodes,
        long sourceEventSequence,
        long recordedEpoch,
        SimTime recordedAt)
    {
        ArgumentNullException.ThrowIfNull(previousPromise);
        ArgumentNullException.ThrowIfNull(exogenousProjection);
        ArgumentNullException.ThrowIfNull(safetyProjection);
        ArgumentNullException.ThrowIfNull(deltas);
        ArgumentNullException.ThrowIfNull(budgetBefore);
        ArgumentNullException.ThrowIfNull(attemptedBudgetAfter);
        ArgumentNullException.ThrowIfNull(witnessCodes);

        if (previousPromise.Projection.RequestId != requestId
            || exogenousProjection.RequestId != requestId
            || safetyProjection.RequestId != requestId)
        {
            throw new ArgumentException(
                "Every breach projection must belong to the affected request.");
        }

        if (sourceEventSequence is < 1 or > DomainLimits.MaxCanonicalInteger
            || recordedEpoch is < 1 or > DomainLimits.MaxCanonicalInteger
            || recordedEpoch < previousPromise.PublishedEpoch
            || recordedAt.Milliseconds
                < previousPromise.PublishedAt.Milliseconds)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceEventSequence));
        }

        var decisionAfter = budgetBefore.Add(deltas.DecisionInduced);
        var visibleAfter = budgetBefore.Add(deltas.Visible);

        if ((!decisionAfter.IsSuccess
                || decisionAfter.Value != attemptedBudgetAfter)
            && (!visibleAfter.IsSuccess
                || visibleAfter.Value != attemptedBudgetAfter))
        {
            throw new ArgumentException(
                "Attempted breach budget must equal budgetBefore plus the " +
                "decision-induced or customer-visible charged delta.",
                nameof(attemptedBudgetAfter));
        }

        var witnesses = witnessCodes
            .Select(value => DomainIdentifier.Require(value, nameof(witnessCodes)))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (witnesses.Length == 0)
        {
            throw new ArgumentException(
                "A breach record requires at least one exact witness code.",
                nameof(witnessCodes));
        }

        BreachId = DomainIdentifier.Require(breachId, nameof(breachId));
        IncidentId = incidentId;
        RequestId = requestId;
        PreviousPromise = previousPromise;
        ExogenousProjection = exogenousProjection;
        SafetyProjection = safetyProjection;
        Deltas = deltas;
        BudgetBefore = budgetBefore;
        AttemptedBudgetAfter = attemptedBudgetAfter;
        WitnessCodes = Array.AsReadOnly(witnesses);
        SourceEventSequence = sourceEventSequence;
        RecordedEpoch = recordedEpoch;
        RecordedAt = recordedAt;
    }

    public string BreachId { get; }

    public IncidentId IncidentId { get; }

    public RequestId RequestId { get; }

    public PublishedPromise PreviousPromise { get; }

    public PromiseProjection ExogenousProjection { get; }

    public PromiseProjection SafetyProjection { get; }

    public ThreeWayPromiseDelta Deltas { get; }

    public CommitmentVector BudgetBefore { get; }

    public CommitmentVector AttemptedBudgetAfter { get; }

    public IReadOnlyList<string> WitnessCodes { get; }

    public long SourceEventSequence { get; }

    public long RecordedEpoch { get; }

    public SimTime RecordedAt { get; }

    public bool NormalOperation => false;
}

public sealed record IncidentLedgerResult
{
    private IncidentLedgerResult(
        OperationalIncidentLedger? ledger,
        DomainFailure? failure)
    {
        Ledger = ledger;
        Failure = failure;
    }

    public bool IsSuccess => Ledger is not null;

    public OperationalIncidentLedger? Ledger { get; }

    public DomainFailure? Failure { get; }

    public static IncidentLedgerResult Success(OperationalIncidentLedger ledger) =>
        new(ledger, null);

    public static IncidentLedgerResult Fail(
        string code,
        string message,
        string? entityId = null,
        string? dimension = null) =>
        new(null, new DomainFailure(code, message, entityId, dimension));
}

public sealed class OperationalIncidentLedger
{
    private readonly FrozenDictionary<IncidentId, OperationalIncident> _incidents;
    private readonly IReadOnlyList<CommitmentBreachRecord> _breaches;

    private OperationalIncidentLedger(
        IEnumerable<KeyValuePair<IncidentId, OperationalIncident>> incidents,
        IEnumerable<CommitmentBreachRecord> breaches)
    {
        _incidents = incidents.ToFrozenDictionary();
        _breaches = Array.AsReadOnly(breaches.ToArray());
    }

    public static OperationalIncidentLedger Empty { get; } = new([], []);

    public IReadOnlyDictionary<IncidentId, OperationalIncident> Incidents =>
        _incidents;

    public IReadOnlyList<CommitmentBreachRecord> Breaches => _breaches;

    public IncidentLedgerResult Open(
        IncidentId incidentId,
        string reasonCode,
        IEnumerable<VehicleId> affectedVehicleIds,
        IEnumerable<RequestId> affectedRequestIds,
        long eventSequence,
        SimTime openedAt)
    {
        if (_incidents.ContainsKey(incidentId))
        {
            return IncidentLedgerResult.Fail(
                IncidentFailureCodes.DuplicateIncident,
                "An incident identifier cannot be opened more than once.",
                incidentId.Value,
                "incidentId");
        }

        var incident = new OperationalIncident(
            incidentId,
            reasonCode,
            affectedVehicleIds,
            affectedRequestIds,
            eventSequence,
            openedAt);

        return IncidentLedgerResult.Success(
            new OperationalIncidentLedger(
                _incidents.Append(
                    new KeyValuePair<IncidentId, OperationalIncident>(
                        incidentId,
                        incident)),
                _breaches));
    }

    public IncidentLedgerResult Resolve(
        IncidentId incidentId,
        long eventSequence,
        SimTime resolvedAt)
    {
        if (!_incidents.TryGetValue(incidentId, out var incident))
        {
            return IncidentLedgerResult.Fail(
                IncidentFailureCodes.UnknownIncident,
                "Only a known open incident can be resolved.",
                incidentId.Value,
                "incidentId");
        }

        if (!incident.IsOpen)
        {
            return IncidentLedgerResult.Fail(
                IncidentFailureCodes.StaleIncidentTransition,
                "The incident has already been resolved.",
                incidentId.Value,
                "incidentId");
        }

        return IncidentLedgerResult.Success(
            new OperationalIncidentLedger(
                _incidents
                    .Where(pair => pair.Key != incidentId)
                    .Append(
                        new KeyValuePair<IncidentId, OperationalIncident>(
                            incidentId,
                            incident.Resolve(eventSequence, resolvedAt))),
                _breaches));
    }

    public IncidentLedgerResult AppendBreach(CommitmentBreachRecord breach)
    {
        ArgumentNullException.ThrowIfNull(breach);

        if (_breaches.Any(
                value => string.Equals(
                    value.BreachId,
                    breach.BreachId,
                    StringComparison.Ordinal)))
        {
            return IncidentLedgerResult.Fail(
                IncidentFailureCodes.DuplicateBreach,
                "A breach identifier cannot be appended more than once.",
                breach.BreachId,
                "breachId");
        }

        if (!_incidents.TryGetValue(breach.IncidentId, out var incident)
            || !incident.IsOpen)
        {
            return IncidentLedgerResult.Fail(
                IncidentFailureCodes.IncidentNotOpen,
                "A breach can only be recorded against an open incident.",
                breach.IncidentId.Value,
                "incidentId");
        }

        if (!incident.AffectedRequestIds.Contains(breach.RequestId))
        {
            return IncidentLedgerResult.Fail(
                IncidentFailureCodes.RiderNotAffected,
                "The breach request is not in the incident's affected-rider set.",
                breach.RequestId.Value,
                "requestId");
        }

        if (!incident.AffectedVehicleIds.Contains(
                breach.PreviousPromise.Projection.VehicleId)
            || !incident.AffectedVehicleIds.Contains(
                breach.ExogenousProjection.VehicleId)
            || breach.SourceEventSequence < incident.OpenedEventSequence
            || breach.RecordedAt.Milliseconds < incident.OpenedAt.Milliseconds)
        {
            return IncidentLedgerResult.Fail(
                IncidentFailureCodes.BreachIncidentMismatch,
                "The breach must follow the incident and originate from one " +
                "of its affected vehicles.",
                breach.BreachId,
                "incidentId");
        }

        return IncidentLedgerResult.Success(
            new OperationalIncidentLedger(
                _incidents,
                _breaches.Append(breach)));
    }
}

public static class IncidentFailureCodes
{
    public const string DuplicateIncident = "DUPLICATE_INCIDENT";
    public const string UnknownIncident = "UNKNOWN_INCIDENT";
    public const string StaleIncidentTransition = "STALE_INCIDENT_TRANSITION";
    public const string UnknownIncidentVehicle = "UNKNOWN_INCIDENT_VEHICLE";
    public const string DuplicateBreach = "DUPLICATE_COMMITMENT_BREACH";
    public const string IncidentNotOpen = "INCIDENT_NOT_OPEN";
    public const string RiderNotAffected = "INCIDENT_RIDER_NOT_AFFECTED";
    public const string BreachIncidentMismatch = "BREACH_INCIDENT_MISMATCH";
}
