using RideBound.Application.State;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;
using RideBound.Domain.Validation;

namespace RideBound.Algorithms.Candidates;

/// <summary>
/// The G2 part of <see cref="InsertionCandidateGenerator"/> (waiting-rider relocation): candidates
/// that move a waiting rider from its vehicle (the donor) to another vehicle (a receiver).
/// <see cref="WaitingRiderRelocationBuilder"/> builds their routes.
/// </summary>
public sealed partial class InsertionCandidateGenerator
{
    // Require a feasible donor-only route and at least one feasible receiver
    // before exploring pending insertions on the donor.
    private RelocationGenerationResult GenerateRelocations(
        OnlineState state,
        IReadOnlyList<RideRequest> requests,
        CandidateGenerationOptions options,
        WaitingRiderRelocationGenerationOptions relocationOptions)
    {
        var result = new RelocationGenerationResult();

        foreach (var donor in state.Run.Vehicles.Values.OrderBy(
                     value => value.Id.Value,
                     StringComparer.Ordinal))
        {
            var seeds = _relocationBuilder.Eligible(state, donor);

            if (seeds.Count == 0)
            {
                continue;
            }

            var work = 0L;
            var evaluatedReceivers = 0L;
            var feasibleReceivers = 0L;
            var feasibleDonors = 0L;
            var omittedDonorPaths = 0L;
            var saturated = false;
            var withoutReceiver = 0L;

            foreach (var seed in seeds)
            {
                var targets = _relocationBuilder.Targets(
                    state,
                    donor,
                    seed.Request,
                    relocationOptions.MaximumTargetVehicles);

                if (targets.Count == 0)
                {
                    withoutReceiver++;
                    continue;
                }

                // The donor's own checks ignore the receiver, so the context names the first
                // target; each receiver route is checked with its own vehicle below.
                var donorContext = new WaitingRiderRelocation(
                    seed.Request.Id,
                    donor.Id,
                    targets[0].Id);
                var donorFeasible =
                    new Dictionary<string, InsertionCandidate>(StringComparer.Ordinal);
                var donorPruned =
                    new Dictionary<string, CandidatePruneWitness>(StringComparer.Ordinal);
                var donorOnly = donor.Route.ReplaceMutableSuffix(seed.DonorSuffix);
                work = SaturatingAdd(work, 1);

                if (donorOnly.IsSuccess)
                {
                    EvaluateRoute(
                        state,
                        donor,
                        donorOnly.Value!,
                        [],
                        isNoOp: false,
                        ServiceQualityAllowance.Strict,
                        donorFeasible,
                        donorPruned,
                        relocation: donorContext,
                        relocationRole: RelocationRole.Donor);
                }

                if (donorFeasible.Count == 0)
                {
                    continue;
                }

                var receiverFeasible =
                    new Dictionary<string, InsertionCandidate>(StringComparer.Ordinal);
                var receiverPruned =
                    new Dictionary<string, CandidatePruneWitness>(StringComparer.Ordinal);

                foreach (var receiver in targets)
                {
                    var receiverContext = new WaitingRiderRelocation(
                        seed.Request.Id,
                        donor.Id,
                        receiver.Id);

                    foreach (var route in WaitingRiderRelocationBuilder.ReceiverRoutes(
                                 receiver,
                                 seed.Pickup,
                                 seed.DropOff))
                    {
                        work = SaturatingAdd(work, 1);
                        evaluatedReceivers = SaturatingAdd(evaluatedReceivers, 1);
                        EvaluateRoute(
                            state,
                            receiver,
                            route,
                            [],
                            isNoOp: false,
                            ServiceQualityAllowance.Strict,
                            receiverFeasible,
                            receiverPruned,
                            relocation: receiverContext,
                            relocationRole: RelocationRole.Receiver);
                    }
                }

                if (receiverFeasible.Count == 0)
                {
                    withoutReceiver++;
                    continue;
                }

                var exploration = ExploreBestFirst(
                    state,
                    donor,
                    requests,
                    options,
                    donorFeasible,
                    donorPruned,
                    new RelocationRoot(donorContext, seed.DonorSuffix));

                work = SaturatingAdd(work, exploration.WorkUnits);
                saturated |= exploration.OmissionCountWasSaturated
                    || exploration.OmittedCandidatePathCount
                        > DomainLimits.MaxCanonicalInteger - omittedDonorPaths;
                omittedDonorPaths = SaturatingAdd(
                    omittedDonorPaths,
                    exploration.OmittedCandidatePathCount);

                if (exploration.OmittedCandidatePathCount > 0)
                {
                    result.Omissions.Add(
                        new CandidateOmissionWitness(
                            CandidateGenerationFailureCodes.RelocationWorkBoundOmission,
                            exploration.OmittedCandidatePathCount,
                            CandidateIdentity.CreateOmissionDigest(
                                exploration.OmittedSubtreeIds),
                            [seed.Request.Id],
                            exploration.OmissionCountWasSaturated));
                    result.Truncated.Add(donor.Id);
                }

                feasibleDonors = SaturatingAdd(feasibleDonors, donorFeasible.Count);
                feasibleReceivers = SaturatingAdd(feasibleReceivers, receiverFeasible.Count);
                result.Add(donorFeasible.Values, donorPruned.Values);
                result.Add(receiverFeasible.Values, receiverPruned.Values);
            }

            result.Losses.Add(
                new RelocationCandidateLoss(
                    donor.Id,
                    seeds.Count,
                    work,
                    evaluatedReceivers,
                    feasibleReceivers,
                    feasibleDonors,
                    omittedDonorPaths,
                    saturated,
                    withoutReceiver));
        }

        return result;
    }

    /// <summary>The root of a G2 donor search: D0's suffix and the declared move.</summary>
    private sealed record RelocationRoot(
        WaitingRiderRelocation Relocation,
        IReadOnlyList<RouteStop> Suffix);

    /// <summary>
    /// G2 output, kept per vehicle until it is appended to the ordinary sets after retention.
    /// </summary>
    private sealed class RelocationGenerationResult
    {
        private readonly Dictionary<VehicleId, List<InsertionCandidate>> _candidates = [];
        private readonly Dictionary<VehicleId, List<CandidatePruneWitness>> _pruned = [];

        public List<CandidateOmissionWitness> Omissions { get; } = [];

        public List<RelocationCandidateLoss> Losses { get; } = [];

        public HashSet<VehicleId> Truncated { get; } = [];

        public void Add(
            IEnumerable<InsertionCandidate> candidates,
            IEnumerable<CandidatePruneWitness> pruned)
        {
            foreach (var candidate in candidates)
            {
                (_candidates.TryGetValue(candidate.VehicleId, out var list)
                    ? list
                    : _candidates[candidate.VehicleId] = []).Add(candidate);
            }

            foreach (var witness in pruned)
            {
                (_pruned.TryGetValue(witness.VehicleId, out var list)
                    ? list
                    : _pruned[witness.VehicleId] = []).Add(witness);
            }
        }

        /// <summary>
        /// The set unchanged when G2 produced nothing for its vehicle; otherwise the union,
        /// still ordered by candidate id. The ordinary candidate cap excludes G2.
        /// </summary>
        public VehicleCandidateSet MergeInto(VehicleCandidateSet set)
        {
            var hasCandidates = _candidates.TryGetValue(set.VehicleId, out var candidates);
            var hasPruned = _pruned.TryGetValue(set.VehicleId, out var pruned);
            var truncated = Truncated.Contains(set.VehicleId);

            if (!hasCandidates && !hasPruned && !truncated)
            {
                return set;
            }

            return set with
            {
                Candidates = set.Candidates
                    .Concat(candidates ?? [])
                    .OrderBy(value => value.CandidateId, StringComparer.Ordinal)
                    .ToArray(),
                PrunedCandidates = set.PrunedCandidates
                    .Concat(pruned ?? [])
                    .OrderBy(value => value.CandidateId, StringComparer.Ordinal)
                    .ToArray(),
                WasTruncated = set.WasTruncated || truncated,
            };
        }
    }
}
