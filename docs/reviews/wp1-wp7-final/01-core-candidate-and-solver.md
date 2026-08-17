# Candidate core and solver walkthrough

## Data flow

```text
OnlineState + immutable travel snapshot
  -> InsertionCandidateGenerator
  -> per-vehicle feasible candidates + explicit loss witnesses
  -> optional CandidatePortfolioRetainer cap
  -> B1/C1 policy assessment
  -> exact assignment model / solver or validated fallback
  -> FullFleetValidator
  -> WP3 CommitmentDecisionValidator
  -> Runner decision, ACK, checkpoint
```

The important separation is that generation/retention is an optimization of the search
space, while validation is a correctness authority. A candidate omitted by a published
work/request/cap bound is counted and digested; it is never silently called infeasible.

## Generation: why it is not simple insertion `if/else`

`src/RideBound.Algorithms/Candidates/InsertionCandidateGenerator.cs` first orders pending
requests deterministically by `(latestPickup, arrivalTime, requestId)`. It creates an
explicit no-op candidate, explores insertion/skip choices best-first under a work budget,
runs the physical validator and schedule/slack machinery for each terminal route, and
records every prune and omitted subtree. Exact-small rejects instead of silently
truncating; production bounds report the loss.

The priority is lexicographic rather than a hand-tuned scalar: potential accepted count,
mandatory service, certified slack availability/value, then a stable digest. This avoids
unstated weights and makes the work-budget behavior reproducible. `ForwardSlackProfile`
is a search aid, not the final feasibility proof; the full validator is still executed.

For B4, `WaitingIncumbentRepairSeedBuilder` creates atomic same-vehicle waiting-pickup
pair repair seeds. The repaired request is neither reassigned nor split. The corrected
priority code projects every repair root's own `ReplaceMutableSuffix(seed)` route before
asking the slack cache. Without that projection a root's score could describe the old
route and a tight cap could spend work on a worse root. The regression
`Bounded_B4_root_priority_projects_every_repair_seed_before_work_is_spent` injects a
recording profile builder and proves that every seed route is scored before the one-unit
budget can choose an expansion.

## Portfolio: the actual B1 guarantee

`CandidatePortfolioRetainer` begins with the legacy rank:

```text
more accepted requests -> lower operational cost -> certified slack -> CandidateId
```

When the cap applies, `ServiceSetStabilityPortfolioV1` operates by accepted-count tier.
For a service set (the unordered `NewRequestIds` on one vehicle), its cost anchor is the
first legacy-ranked member: it is therefore the least-cost member under every preceding
legacy tie-break. The portfolio reserves such anchors before duplicate route variants;
it may reserve a stable alternative based on preserved incumbent prefix and schedule
shift, then fills remaining positions in legacy order. The no-op is always kept.

For each legacy retained candidate, the portfolio contains a candidate for the same
vehicle and same service set whose cost is no higher. The assignment model's request
conflict columns are exactly that service set, so replacing every selected legacy plan
with its anchor preserves all accepted requests and cannot increase the total cost. This
is the B1 claim. It intentionally does **not** claim that an arbitrary CandidateId tie
is preserved, that a stable anchor always has lower cost, or that C1 objective dominance
follows mathematically after hard filtering.

`ValidateRelationToNoOp` is part of this proof boundary. Under the opt-in capped path it
requires all no-op incumbent stops to remain byte-equal by `StopId`, and it requires the
new route request IDs to exactly equal declared `NewRequestIds`. Thus a malformed route
cannot pretend to be a service set while hiding a second request. The legacy fast path
is deliberately unchanged for WP1–WP6 configs.

Tests demonstrate the theorem at each cap and exhaustive small variant counts, a fleet
adversarial strict result `2 -> 4`, deterministic permutations, exact omission
conservation and rejection of hidden/cross-vehicle portfolios. The 128 seeded C1 gate
is empirical/exact for its bounded fixture: it uses the real hard validator and requires
no regression plus one strict-positive witness. It is evidence, not a universal theorem.

## C1 and solver correctness

`HardVectorCandidateAssessor` computes candidate-specific commitment deltas from the
same mechanism context and filters hard-invalid candidates. It calculates maximum
utilization by exact integer ceiling parts-per-million, scopes it to applicable limits,
and leaves warnings to C2. `SolverBackedRidePoolingPolicy` generates the common raw
pool once, then applies only the named B1/B2/B3/B4/C1/C2 mechanism. This is crucial for
a fair comparison: C1 does not get a Python-only or separately generated pool.

`SolverBackedFleetSelector` maps a lexicographic objective list to the solver: accepted
count, applicable hard utilization and revision/warning vectors, operational cost, then
per-vehicle stable candidate ranks. It does not hide constraints in a weighted scalar.
`SafeCandidateSelectionExecutor` validates solver/fallback selections independently;
`FullFleetValidator` then runs physical validation, applies selected routes and invokes
the WP3 commitment validator before the decision can return.

## What the search actually spends its time on

This was measured before anything was changed, and the measurement contradicted the
obvious guess. The physical validator is cheap — about `0.72 µs` per route. The cost was
in *recomputing identity*: the slack memo keyed on a framed SHA-256 fingerprint of every
stop, at roughly `19.6 µs` per lookup, and one `Generate` on a loaded vehicle performs
about `39,000` lookups. Identity computation, not feasibility reasoning, was the search.

The fix keeps the distinguishing power and drops the cost. `ForwardSlackCacheKey` now
compares routes by exact structure — version, executed count, frozen prefix and mutable
suffix element-wise — which is precisely what the fingerprint encoded, minus the
collision risk. That key never leaves the process and is not part of any published
identity, so nothing observable depends on how it is computed. Lookup cost fell to
`0.64 µs`. Alongside it: identity framing writes UTF-8 straight into its buffer instead
of allocating two arrays per frame, `RoutePlan.Create` finds duplicate stops by counting
rather than by `GroupBy` while still reporting the same key, search-node identity and
the projected route are computed lazily and memoized, and the generator ranks once and
hands that order to the retainer behind a fail-closed order check.

| mutable suffix | before | after |
|---:|---:|---:|
| 4 stops | 25.3 ms | 16.6 ms |
| 8 stops | 220 ms | 170 ms |
| 12 stops | 897 ms | 587 ms |
| 16 stops | 1662 ms | 1018 ms |

The number that matters is not in that table. `CandidateSearchWorkProfileTests` pins the
exact work units, evaluated paths, feasible-before-cap count, omitted paths, retained
count and **distinct slack profiles built** for four route sizes; every one of them is
unchanged. A faster search that explored a different tree would fail that test.

One proposal was rejected. *Lazy priority* — enqueue on a cheap lower bound and only
compute slack when a node is popped — cannot help here, because inserted stops carry
zero service duration, so every insertion child of a node ties on both cheap keys. The
bound would be flat across almost the whole frontier, forcing the search to refine every
node anyway and adding heap churn on top. It is recorded as a negative result so it is
not proposed again without a tighter bound.

The literature has a real answer that was **not** adopted here: Gschwind & Drexl (2019)
test insertion feasibility in amortized constant time by evaluating only the two
inserted nodes, reporting a `3.8×` speed-up over the classical eight-step scheme. It is
exact rather than a pruning heuristic, so unlike a direction or random filter it would
not weaken the comparator. It is a WP8 candidate, not a WP7 change: the full text was
not reachable at the time of the audit, and it covers only the temporal dimension while
this validator also decides capacity, connectivity, frozen prefix and commitment budget.

The core conclusion is modest but strong: every optimization is bounded, deterministic,
loss-accounted and revalidated. It is not a claim that arbitrary online DARP instances
are solved optimally or that one policy improves passenger outcomes.
