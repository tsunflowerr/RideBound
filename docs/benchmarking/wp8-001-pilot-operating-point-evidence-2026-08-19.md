# WP8 pilot — operating point and endpoint evidence

> Date: 2026-08-19
>
> Evidence class: **pilot**. Not confirmatory, not an effectiveness result.
>
> Confirmatory holdout `2018-11-14` → `2018-11-18` has not been generated or run.

## What this pilot was for

To find out whether the experiment `docs/11` proposes can measure anything at all before
it is preregistered. It found two reasons why the design as written could not, and both
were discovered from mechanism, not from comparing arms.

## Finding 1 — the WP6/WP7 operating point cannot discriminate

Both arms were run on the frozen tree at the operating point WP6 and WP7 had validated:
128 requests spread across a full 24-hour day with 32 vehicles.

| | B1 rolling-cost | C1 hard-vector |
|---|---:|---:|
| promised riders | 128 | 128 |
| publications | 482 | 495 |
| **decision-induced pickup ETA total variation** | **0** | **0** |
| exogenous pickup ETA variation | 3,459 ms | 3,640 ms |
| visible pickup ETA variation | 3,459 ms | 3,640 ms |
| material ETA revisions | 3 | 0 |
| Σ per-publication delta vs cumulative budget | 0 mismatches | 0 mismatches |

Exogenous equals visible exactly, and the decision-induced component is zero for every
rider in both arms. All promise movement comes from traffic and vehicle progress; none
comes from the algorithm. At this density the fleet is almost always idle, so inserting a
new request rarely disturbs a live promise.

The consequence is decisive: the primary outcome proposed in `docs/11` §2 is identically
zero in both arms, so **no sample size can rescue it here**. An experiment at this
operating point is guaranteed to return a null that says nothing about the treatment.

## Finding 2 — the proposed primary endpoint is too zero-inflated to use

A contended operating point was then built from the data's own structure: the real
`08:00–10:00` peak window (2,286 eligible source rows) with 8 vehicles instead of 32.
Only the window and the fleet size changed; every other parameter is untouched.

At that point the algorithm genuinely has to disturb live promises — publications rise
from 482 to 3,904 and the highest promise version from 13 to 90.

Pickup-ETA decision variation there is zero for C1 in all three runs measured, and zero
for **B1 as well** in one of them. Where it is non-zero it covers only 5–6 riders out of
roughly 110, so `p50 = p90 = 0` in every run.

An early reading of this as a structural identity was wrong and is recorded as such:
`C-d20181112-r2-c1` has `prePickupInsertedStopCount = 3` while still showing zero
pickup-ETA decision variation, so C1 does insert ahead of incumbent pickups — it inserts
into existing slack without moving any ETA. There is no mechanical identity.

The disqualifying problem is different and remains: a `p95` computed over a distribution
that is 90% zeros reflects a handful of riders and moves sharply when a few observations
are added or removed. That is a brittle primary outcome.

The burden also **moves** rather than vanishing: C1's decision-induced burden lands
almost entirely on drop ETA. A fair endpoint must therefore sum decision-induced burden
across dimensions so the shift is visible. `RB-WP8-005` and `RB-WP8-009` were amended
accordingly.

## Finding 3 — the service trade-off is real and appeared immediately

In the first two paired units, one showed C1 serving **8 fewer riders** (99 against 107,
−7.5%) while reducing burden; the other served exactly the same number. Two observations
support no conclusion, but they do establish that `docs/11` §5's warning is live: service
rate must be a simultaneous gate rather than a secondary metric, and an illustrative
1-percentage-point margin would be far exceeded by a 7.5-point drop.

## Paired result at the contended operating point

Same scenario, same seed, same Runner artifact, same work and solver budgets, same
candidate pool, same promise trigger. The two arm configurations differ in exactly one
field, `policyId`.

Four paired units, each a distinct real demand realization: two days
(`2018-11-12`, `2018-11-13`) drawn from the publisher's own 10% sample replicates, all
at the contended operating point. Decision burden is the summed decision-induced pickup
plus drop ETA variation over all riders. Service is counted at completion
(`passengerAlighted / requestArrived`), not at promise.

| unit | completed B1/C1 | decision burden B1 | decision burden C1 | Δ burden | material revisions B1/C1 |
|---|---|---:|---:|---:|---|
| `C-d20181112-r2` | 107 / 99 | 3,023,018 | 758,435 | −2,264,583 | 33 / 10 |
| `C-d20181113-r1` | 117 / 110 | 2,531,022 | 552,796 | −1,978,226 | 31 / 5 |
| `C-d20181113-r2` | 120 / 119 | 4,461,860 | 331,675 | −4,130,185 | 43 / 4 |
| `L1-peak2h-veh8` | 117 / 117 | 4,755,808 | 760,220 | −3,995,588 | 46 / 12 |

Burden: all four differences negative, median `−3,130,086 ms`, mean `−3,092,146 ms`,
standard deviation `1,128,334 ms`. The reduction is 75–93% and the direction is
consistent.

Completion-rate differences (C1 − B1): `−6.25`, `−5.47`, `−0.78`, `0.00` percentage
points. Mean `−3.13 pp`, standard deviation `3.19 pp`.

### Reading this against the project's own success criteria

`docs/11` §5 sets an illustrative non-inferiority margin of one percentage point on
service rate, and §14 states that improving the revision outcome while failing service
non-inferiority is "a trade-off that is not practical at that configuration".

The observed mean service deficit is roughly three times that margin, and three of four
units are below it. On the project's own pre-stated criteria, **C1 as currently
configured does not clear the service gate at this operating point**. Larger samples
would sharpen that conclusion rather than reverse it: the deficit is a location problem,
not a precision problem.

The burden variance is what the pilot was for. With `σ ≈ 1.13e6 ms` against a mean
difference of `−3.09e6 ms`, the burden effect is roughly 2.7 standard deviations from
zero and needs few units to establish. The service comparison is the binding constraint:
at `σ ≈ 3.2 pp`, excluding a 1 pp margin would need on the order of tens of paired units
even if the true difference were zero — and here it is not zero.

No confidence interval is quoted. Four units is a variance estimate, not an effect
estimate, and quoting a CI here would dress up a pilot as a result.

## Why the operating point change is not cherry-picking

The change was driven by a mechanism observation — `decisionDelta ≡ 0` — made before any
B1/C1 comparison at the new point existed, and it uses the dataset's own peak window
rather than invented load. `docs/09` §7 defines exactly this as the E2 pilot's job, and
`docs/11` §10 permits a pilot to change thresholds and reduce the factor grid.

The obligation this creates is that the operating point must be written into the
preregistration before any confirmatory day is touched. It has not been used to select
an outcome: the confirmatory holdout remains ungenerated.

## Claim boundary

This is pilot evidence on two of eight days, at one operating point, with a synthetic
commitment-budget overlay whose three hard limits are all zero and all already satisfied
by `O-001`. It establishes that the comparison can be made to measure something and
supplies variance for a later power calculation. It is not an effectiveness,
non-inferiority, fairness, satisfaction, SLA or novelty result, and no such claim is
authorized by it.
