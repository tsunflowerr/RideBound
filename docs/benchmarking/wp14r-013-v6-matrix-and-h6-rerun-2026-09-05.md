# RB-WP14R-009 — Entered-edge position defect, the v6 matrix, and the H6 re-run

The WP14 matrix halted twice in the same place. This records what was actually
wrong, what the corrected run produced, and what the confirmatory experiment
looks like once it is re-run under the correction.

## 1. The defect

`RBWP7_FLEETPY_PLAN_INFEASIBLE` killed the v1 matrix at job 41 of 160 and the v5
matrix at the same job. Job 41 is the first `w17` cell: the forty jobs before it
are all `w08`, which is why both runs died at exactly that boundary rather than
somewhere random.

An instrumented reproduction captured the failing decision. Vehicle 4 sat at
FleetPy position `(86, 8, 0.000126)` — 0.126 permille along the edge from node 86
to node 8:

| | |
|---|---:|
| Reported to the core | `{"kind": "node", "nodeId": <node 86>}` |
| Core's cost for 86 → 39 | 574.616 s |
| True cost from the vehicle's position | 792.244 s |
| Cost to reach node 86 again | 1352.586 s |
| Divergence | 217.628 s |
| Believed slack | 25.384 s |
| Realised pickup-window violation | 192.244 s |

[`mapping.py`](../../simulators/fleetpy-ridebound/ridebound_fleetpy/mapping.py)
floored edge progress to permille and, at zero, returned the *from*-node. The
comment justified this as keeping RideBound behind the true position. That
reasoning holds *along* an edge and fails *across* one: on a directed network a
vehicle cannot un-enter an edge, so the node behind it is not a conservative
under-estimate but a different place, 13.5 km away by road.

The core was never wrong.
[`RouteScheduleProjector.cs`](../../src/RideBound.Application/Scheduling/RouteScheduleProjector.cs)
already models an edge position exactly: it adds the remaining edge time and
continues from `ToNodeId`. It simply never received one.

The fix keeps an entered edge as `kind: edgeProgress` and clamps
`progressPermille` to at least 1 — the smallest value `EdgeProgressPermille`
admits, and therefore the most conservative point the wire can express.
Exact-zero progress still maps to the from-node, because the vehicle has not
committed to the edge yet.

An earlier hypothesis — that positions were stale because only discrete events
marked a vehicle dirty — was implemented, tested and **disproved**: the halt
recurred unchanged. That change was reverted and `fleet_control.py` is back to
its frozen bytes `bbe7e373…`.

## 2. The v6 matrix

160 of 160 jobs, every one succeeding on its first of two permitted attempts.

| | |
|---|---|
| Jobs | 160/160 `terminalSuccess` |
| Attempts per job | `{1: 160}` — no job needed a second |
| Host preflight gates | 160/160 pass, 8.79–9.89 GiB observed against an 8.00 GiB floor |
| Independent verification | 160/160 `valid`, re-run as a second pass after completion |
| Recovery receipts | 0 |
| Repository inventory | one hash, `1a2102eb…`, across all 160 bundles |
| Wall time | 28.55 h of job time, median 10.8 min/job |
| Retained bundles | 17.13 GiB against a 20 GiB protocol ceiling |

No predecessor got this far. Freeze v2 burned both attempts on its first job, v3
was rejected by its own verifier, v4 could not sign its gate, v5 ran 40 jobs and
halted at job 41 — the job the corrected adapter now clears on the first attempt.

## 3. What the matrix found

Ten arms produced **five** distinct outcomes.

| Arm | Served | Rate | vs `b1-ref` | Attributed burden | Reduction |
|---|---:|---:|---:|---:|---:|
| `b1-ref` | 1629 | 94.2708% | — | 59,250,164 ms | — |
| `c1-nobudget` | 1584 | 91.6667% | −2.6042 pp | 7,496,399 ms | 87.35% |
| `c1-budget120` | 1563 | 90.4514% | −3.8194 pp | 2,554,106 ms | 95.69% |
| `c1-budget60` | 1540 | 89.1204% | −5.1505 pp | 716,125 ms | 98.79% |
| six arms, tied | 1534 | 88.7731% | −5.4977 pp | 83,373 ms | 99.86% |

The tied six are `c1-freeze300`, `c1-freeze300ratchet`, `c1-freeze600`,
`c1-h6ref`, `c1-nopickuplock` and `c1-ratchet`: identical on every counter across
all sixteen cells. **Three of the four ablation factors — freeze horizon, ratchet
and pickup lock — moved nothing.** Only the ETA budget axis separates arms, and
along it the five points form a strictly monotone service-versus-burden frontier.

### Why three factors are inert

All three constrain how a promise may be *revised*. The C1 baseline records nine
disruptive decisions across the whole panel against 427 for `b1-ref`, so there is
almost nothing for them to constrain. The budget is the factor that permits
revision in the first place: removing it yields 67 disruptive decisions and 1584
served; tightening it to 60 yields 22 and 1540.

### F2, measured directly

F2 adds `ratchetLocks`: an ETA lock there is violated only when a candidate
pushes a promise **later**, while earlier is permitted — the one-sided mechanism
of Alonso-Mora et al. Its value therefore depends on revisions that move a
promise earlier existing at all.

Over 421,752 revisions in 160 bundles:

| | earlier | later | equal |
|---|---:|---:|---:|
| Decision-induced | **0** | **683** | 0 |
| Exogenous only, drop | 214,924 | 206,224 | — |
| Exogenous only, pickup | 102,949 | 107,307 | — |

Every decision that moved a promise moved it later. Not most — all 683.
`ratchetAdmissibleObservations` is 0, so the permissive half of F2 had nothing to
permit. This is not a wiring fault: the mechanism is correct and the regime never
produced the case it was built to exploit.

The exogenous columns are the control. Drift is 51.03% and 48.96% earlier, which
is what noise looks like, while the decision-induced column is perfectly
one-sided. A broken `decisionDelta` filter would make the two look alike.

Two independent tools cross-check: decision-moved-later totals 70,523,899 ms
against 70,517,032 ms of summed attributed burden, a 0.0097% difference arising
from different aggregation boundaries.

Evidence:
[`wp14r-011`](evidence/wp14r-011-v6-full-matrix-frontier-v1-summary.json) (sha
`08ab70cd…`),
[`wp14r-012`](evidence/wp14r-012-v6-promise-direction-v1-summary.json) (sha
`86fdde53…`).

## 4. The H6 re-run

ADR-047 had already recorded that this rounding is outcome-bearing, so the
confirmatory experiment was re-run rather than reasoned about. New bundle roots;
the retained originals untouched and still in WP14's forbidden set.

**Every published figure reproduced exactly.**

| | Panel A published | Panel A re-run | Panel B published | Panel B re-run |
|---|---:|---:|---:|---:|
| Baseline completed | 1735 | 1735 | 966 | 966 |
| Treatment completed | 1581 | 1581 | 860 | 860 |
| Delta | −154 | −154 | −106 | −106 |
| Baseline burden | 74,443,002 ms | 74,443,002 ms | 44,766,809 ms | 44,766,809 ms |
| Treatment burden | 128,020 ms | 128,020 ms | 342,974 ms | 342,974 ms |
| Total reduction | 74,314,982 ms | 74,314,982 ms | 44,423,835 ms | 44,423,835 ms |
| Pickup locked component | 9,579,869 ms | 9,579,869 ms | 5,056,311 ms | 5,056,311 ms |
| Drop earned component | 64,735,113 ms | 64,735,113 ms | 39,367,524 ms | 39,367,524 ms |

Service gate FAIL at −7.1296 pp and −4.9074 pp against the −1.00 pp preregistered
margin; burden gate PASS at 99.8280% and 99.2339%. **The negative result stands
unchanged.**

Reproduction to the millisecond means the trajectories were identical, which is
consistent with the mechanism: the defect bites only when a vehicle is inside the
first 1/2000th of an edge at a decision instant. On WP14's evening panel that
happened and halted the matrix. On H6's panel it never changed an outcome.

Robustness is unchanged and remains `descriptiveOnlyCannotRescuePrimary`. Both
seed contrasts are exactly zero, confirming the preregistered
`solverSeedsAreReplicates: false`.

Evidence:
[`wp9-h6-rerun`](evidence/wp9-h6-rerun-under-corrected-adapter-v1.json) (sha
`3f2c3444…`).

### What this does and does not establish

It establishes that running the H6 design under the current, corrected adapter
yields the published numbers. It does **not** establish that the adapter H6
originally ran under was byte-identical to today's: those bytes are
unrecoverable, and
[`source-divergence-v5.json`](../../benchmarks/scenarios/wp9-confirmatory/source-divergence-v5.json)
records that with `recoveryVerified: false` on both divergences.

## 5. Provenance defects fixed along the way

**A freeze that did not seal its own builder.** `wp14_freeze_v2.py` was derived
from v1 and kept naming v1's builder and test in `STATIC_REPOSITORY_FILES`, so a
v2 receipt would have verified while its own source drifted freely. Now
self-sealing, pinned by `Wp14FreezeV2SelfSealTests`.

**A seal over compiled bytecode.** WP9's `_tree_sha256` filtered with
`path.name not in excluded`; `__pycache__` is a *directory*, so passing
`{"__pycache__"}` excluded nothing and every seal hashed `.pyc` files. A seal
over bytecode cannot survive a recompilation, which is why the H6-era adapter
seal `7fc7e1b8…` is reproducible from no state of the package — not the working
tree, not any of the four commits that ever touched it. v6 tests the relative
path's parts, matching `wp14_freeze.py`. Pinned by a test asserting that v5 is
bytecode-sensitive and v6 is not.

**A declaration sealed by the seal it describes.** Writing
`source-divergence-v5.json` into `benchmarks/scenarios/wp9-confirmatory/` moved
`scenarioPlanSealSha256`, for the same reason the freeze receipts are already
excluded from it. Excluding it restored the seal to H6's own
`7177f916…`, which is itself the proof that the declaration was the only
perturbation.

**A hardcoded builder.** `wp14_run_matrix.py` and the frontier tooling loaded
`wp14_freeze.py` unconditionally, so a v2 receipt failed with "freeze receipt
differs from its sources" — a message that reads as corruption rather than a
stale builder. Both now select by declared freeze identity, which stays safe
because the selected builder still rebuilds the whole receipt and compares it.

**A claim boundary that could lie about its own coverage.**
`wp14r_slice_frontier.py` asserted
`descriptiveSliceNotThePreregisteredSixteenCellFrontier` as a constant. That was
true when written, over a matrix halted at 40 jobs, and false over 160 of 160.
Coverage is now derived from observation. The tool had no tests; it has nine.

## 6. A correction to an interim claim

Mid-run this work reported that all ten arms produced distinct behavioral hashes
and concluded that every ablation factor affected behaviour. **That conclusion
was wrong.** `bundleVerification.behavioralHash` covers execution evidence
including solver timing, so behaviourally identical arms hash differently — as
`behaviourally_identical` in the frontier tool already documented. Compared on
outcome counters, six of the ten arms are indistinguishable. The finding in
section 3 supersedes the interim claim.
