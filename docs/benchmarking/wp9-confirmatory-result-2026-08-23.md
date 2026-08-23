# WP9 confirmatory result — frozen H6 finite panel

Date: 2026-08-23  
Freeze: `H6=84f6eff31addbdd12349a19201d79c872fbd05aaf5e0aa45dd73aee6d5c3dee2`  
Primary comparison: B1 rolling cost versus C1 hard-vector RideBound  
Classification: finite-panel confirmatory result; conditional on the two measured fleet capacities

## Verdict

The preregistered service gate fails at both measured capacities. RideBound's
hard commitment mechanism greatly reduces measured decision-induced revision
burden, but it does so with a service-completion loss far outside the locked
`−1.00 pp` non-inferiority margin.

| Panel | Vehicles | Arrivals/arm | B1 completed | C1 completed | Service delta | Service gate | B1 → C1 burden | Burden gate |
|---|---:|---:|---:|---:|---:|---|---:|---|
| A | 8 | 2,160 | 1,735 | 1,581 | `−154 = −7.1296 pp` | **FAIL** | `74,443,002 → 128,020 ms` | PASS |
| B | 4 | 2,160 | 966 | 860 | `−106 = −4.9074 pp` | **FAIL** | `44,766,809 → 342,974 ms` | PASS |

The two capacity points are separate strata, not one pooled estimand. Panel A
and Panel B intentionally have different `scenarioHash` values; their demand and
travel realizations were independently verified equal, but the initial fleet
state differs. Therefore the between-panel contrast is descriptive heterogeneity,
not a paired test.

## Cell-level service result

Panel A is uniformly negative: 20 negative cells, no zero and no positive cell.
The per-cell range is `−13.8889 pp` to `−0.9259 pp`, with median
`−6.4815 pp`. There is no observed 8-vehicle cell where the mechanism is free.

Panel B has 18 negative, one zero and one positive cell. The per-cell range is
`−12.0370 pp` to `+1.8519 pp`, with median `−5.0926 pp`. The smaller fleet does
not reverse the primary conclusion: its aggregate loss remains almost five
times the preregistered margin.

## Why the burden result must not be oversold

The burden gate carries limited information because the treatment locks some
revision dimensions and can avoid burden by declining service work.

| Panel | Total reduction | Pickup-ETA definitional component | Remaining drop-ETA component | C1 cells with zero total burden |
|---|---:|---:|---:|---:|
| A | `74,314,982 ms` (`99.8280%`) | `9,579,869 ms` (`12.8909%`) | `64,735,113 ms` (`87.1091%`) | 12/20 |
| B | `44,423,835 ms` (`99.2339%`) | `5,056,311 ms` (`11.3820%`) | `39,367,524 ms` (`88.6180%`) | 7/20 |

The pickup component is definitional under the pickup-ETA lock. The remaining
component is mechanically measured rather than automatically zero, but the
service result shows that it cannot be interpreted as “performing the same work
with fewer revisions.” A material part of the reduction accompanies work that
C1 does not complete.

## Preregistered robustness and ablation

Robustness is descriptive and cannot rescue either primary gate. On the five
Panel A robustness cells (`540` arrivals/arm):

| Arm/configuration | Completed | Burden (ms) | Disruptive decisions | Delta vs B1 |
|---|---:|---:|---:|---:|
| B1 tight | 440 | 17,004,794 | 96 | reference |
| C1 unbounded | 420 | 4,783,090 | 32 | `−20 = −3.7037 pp` |
| C1 tight | 400 | 29,494 | 2 | `−40 = −7.4074 pp` |
| C2 loose hybrid | 402 | 1,287,514 | 17 | `−38 = −7.0370 pp` |

The lock/ranking mechanism costs `−3.7037 pp`; the 30-second hard budget adds a
further `−3.7037 pp`. The observed total cost therefore splits exactly 50/50 in
this five-cell ablation. C2 recovers only two completions over tight C1 and
remains `−7.0370 pp` below B1. The analyzer correctly emits
`confirmatoryGate: null` and `descriptiveOnlyCannotRescuePrimary`.

Solver seed 19 is not a replicate. `seed19 − seed7` is exactly zero for both
arms on completed requests, disruptive decisions and total decision-induced
burden. It adds zero confirmatory sample units.

## Precision and inference boundary

Each panel contains 20 fixed cells and only five travel realizations. The
achieved service-rate precision is approximately `1.40 pp`, wider than the
`1.00 pp` margin. The sign-flip p-value floor is `1/2^5 = 0.03125`. These facts
are printed beside the gates as design limitations, not converted into a
population confidence interval or a population p-value.

Permitted conclusion: under this exact frozen H6 panel, C1 fails service
non-inferiority at both 8 and 4 vehicles while passing the mechanical burden
gate. Prohibited conclusions include population generalization, city-wide
external validity, SLA, user satisfaction, fairness, novelty of dynamic
insertion/ETA limits/reassignment, or a universal claim that commitment is cheap.

## Reproducibility and artifact identity

The independent verifier passed all 100 H6 bundles, reconstructed exact demand
and travel event projections, checked cross-panel identity, and validated a
two-repeat deterministic run. Compact evidence is in
[`evidence/wp9-h6-reproducibility-v1.json`](evidence/wp9-h6-reproducibility-v1.json).

- Panel A analysis SHA-256: `72f052d735422b187c6840eeedf8a9167dc9a14c5385d512c7a492579de880e0`.
- Panel B analysis SHA-256: `3f6a339c2ac33c7cc19196cf726b23a42e3397678890851cf7ca980000abbe3f`.
- Robustness analysis SHA-256: `ce87ea7563bd9f3a12c199e3ac3c6641f27a50730f1e51cf16f2775fa909533b`.
- Repeat behavioral hash: `399bc5da6742fa84aa2884881a77499d5579a9a797eaf8d801756f20afc76c93`.
- Panel A receipt: `8c7cf66a64eca018bc7d2d59f74a0ad3d3176eed8f4bafa51f3595432d296a5a`.
- Panel B receipt: `cb86aa4a87acedc062425831148454716f110622120689a36c93f4d06b512165`.

`RB-WP9-009` was implemented only after the frozen matrices completed. Its
post-outcome smoke proves that exogenous breach evidence and ledger persistence
work, but those 43 observations are mechanism-verification evidence and are not
added to the H6 estimand or used to reinterpret either gate.
