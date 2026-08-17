# ADR-039 — hot-path and undeclared-semantics closure evidence

> Date: 2026-08-17
>
> Evidence class: mechanical correctness, reproducibility and measured cost
>
> Claim profile: no effectiveness, SLA, fairness, satisfaction or novelty result

## Why this exists

Two debts were paid here. A group of real semantic changes had reached source without
any ADR locking them, so the specification no longer described the system. And the
medium benchmark had been hitting a CPU ceiling whose cause had been guessed at, never
measured.

Nothing in this document upgrades the claim boundary. WP7 remains closed at mechanical
Layer 2.

## Semantics that are now locked

| Behavior | Rule |
|---|---|
| `initialPromiseTrigger` | `initial-acceptance` (default, WP1–WP6) or `booking-confirmation`; a config without the field must parse as the default and keep its content hash |
| Provisional offer | Under `booking-confirmation` an `Accepted` request is physically validated but carries no promise, and is out of scope for hard-vector and warning accounting |
| First promise | Opens on `Accepted → WaitingPickup/Onboard` with `INITIAL_BOOKING_CONFIRMATION`, including when confirmation and boarding reduce in one batch |
| Phase lock axis | Candidate is compared against the **exogenous** projection; the published promise only fixes which horizon is locked, so traffic drift is recorded but is not a lock violation |
| Declined offer | `OfferDeclined` on an `Accepted` request is cancel-after-acceptance, not rejection |
| Empty treatment set | `C1_VEHICLE_HAS_NO_FEASIBLE_CANDIDATE` fails the run closed, with `requestId`, `dimension`, `underlyingCode`, `before`, `after` and generated/rejected counts in typed fields |
| Runner CLI | `--maximum-line-bytes` bounded to `1 MiB..64 MiB`; `--manifest-solver-seed` opt-in |
| Event-induced plan update | Emitted only when the caller opts in and the route genuinely differs from the prior one |

## What the measurement found

The audit started with a micro-harness on a loaded vehicle, not with a hypothesis.

| Component | Cost per call |
|---|---:|
| `PhysicalPlanValidator.Validate` | 0.72 µs |
| `RoutePlan.ReplaceMutableSuffix` | 3.94 µs |
| `SHA256.HashData` (1.6 KB) | 4.44 µs |
| **`ForwardSlackCacheKey.Create`** | **19.63 µs** |

One `Generate` on a 16-stop vehicle performs roughly 39,000 slack-cache lookups. The
bottleneck was therefore identity recomputation — a framed SHA-256 over every stop on
every lookup — and not feasibility reasoning. The validator was never the problem.

## What changed, and why it is byte-neutral

- `ForwardSlackCacheKey` compares routes by exact structure (version, executed count,
  frozen prefix, mutable suffix, element-wise). This is exactly what the fingerprint
  encoded. The key is process-local and appears in no published identity, so its
  construction is unobservable; removing the hash also removes the collision risk.
- Identity framing encodes UTF-8 directly into the writer, instead of allocating one
  array for the tag and one for the value on every frame. The emitted bytes are
  unchanged, so every published identity is unchanged.
- `RoutePlan.Create` detects duplicate stops by counting instead of `GroupBy`, and still
  reports the first duplicated key in first-occurrence order — the same key the previous
  expression selected.
- Search-node identity and the projected route are computed lazily and memoized, so a
  node nobody interrogates never pays a SHA-256, and a terminal node projects its route
  once rather than twice.
- The generator ranks once and passes that order to the retainer, behind a linear
  fail-closed order check.

| mutable suffix | before | after | change |
|---:|---:|---:|---:|
| 4 stops | 25.3 ms | 16.6 ms | −34% |
| 8 stops | 220 ms | 170 ms | −23% |
| 12 stops | 897 ms | 587 ms | −35% |
| 16 stops | 1662 ms | 1018 ms | −39% |

Timings are the minimum of three runs. `ForwardSlackCacheKey.Create` fell from
19.63 µs to 0.64 µs. The mechanical medium benchmark now completes in `1 m 50 s`,
under its unchanged 120-second CPU ceiling.

The evidence that matters is not the timing. `CandidateSearchWorkProfileTests` locks the
exact work units, evaluated paths, feasible-before-cap count, omitted paths, retained
count and distinct slack-profile builds at four route sizes:

| incumbents | work units | evaluated | feasible | omitted paths | slack profiles |
|---:|---:|---:|---:|---:|---:|
| 2 | 468 | 450 | 451 | 0 | 452 |
| 4 | 3,108 | 3,060 | 3,061 | 0 | 3,062 |
| 6 | 10,000 | 9,908 | 9,909 | 1,194 | 11,013 |
| 8 | 10,000 | 9,848 | 9,849 | 19,528 | 28,845 |

## Rejected: lazy priority

Deferring the slack computation until a node is popped cannot pay here. Inserted stops
carry zero service duration, so every insertion child of a node ties on both cheap
priority keys; an admissible lower bound is therefore flat across nearly the whole
frontier. The search would refine every node anyway and add heap churn on top. Recorded
as a negative result rather than silently dropped.

## Not adopted: constant-time feasibility testing

Gschwind & Drexl, *ALNS with a Constant-Time Feasibility Test for the Dial-a-Ride
Problem*, Transportation Science 53(2):480–491, 2019,
[10.1287/trsc.2018.0837](https://doi.org/10.1287/trsc.2018.0837), evaluates only the two
inserted nodes and reports an average `3.8×` speed-up over the Cordeau–Laporte
eight-step scheme. It is exact rather than a pruning heuristic, so it would not weaken
the comparator the way a direction or random filter would. It is a WP8 candidate and was
deliberately not implemented: the publisher page returned `403` and the Mainz preprint
`404` at the time of the audit, so only abstract-level metadata was available, and no
code may be written from a source that has not been read. It also covers only the
temporal dimension, whereas this validator additionally decides capacity, connectivity,
frozen prefix and commitment budget — so at most it could replace the schedule/slack
stage, still followed by the full validator.

## Cross-binary differential

`RunManifestIdentity` binds `binarySha256`. A new Runner artifact therefore produces a
different manifest hash, a different checkpoint binding hash and a different composite
semantic hash **even when behavior is identical**. Two adjacent traps: the harness
`--label` flows into `run_id` and hence into the manifest, and the publication id is
derived per run.

Running the tiny FleetPy clock against Runner v7 and Runner v8 under an identical label
gave identical values for every behavioral field — publication `promiseVersion`,
`reasonCode`, `requestId`, `sourceEventSeq`; `requestState`; `travelSnapshotVersion`;
`vehiclePosition`; `nextEpoch`; `nextEventSeq`; and `exactPhysicalBoundaryDrainCount`.
Only `manifestHash` and `checkpointBindingHash` differed, which is the binding working
as designed.

## Immutable inputs

| Input | Locked identity |
|---|---|
| FleetPy | tag `1.0.2`, commit `053aa9d4fcfde91c5d303435d5748f9206c071b0`, MIT |
| Python | CPython `3.10.20`, `win-64` lock in `simulators/fleetpy-ridebound/environment.lock.yml` |
| Runner | `E:\RideBoundData\wp7\runner\candidate-portfolio-v8-identity-hotpath\RideBound.Runner.dll` SHA-256 `13bf5d9b1dfbcb677d2d64c24038dba2c9adc22e664d2a6adecbf1905dcc179e` |
| B1 config | `benchmarks/configurations/wp7-fleetpy-rolling-cost-v1.json` |
| C1 config | `benchmarks/configurations/wp7-fleetpy-ridebound-hard-vector-v1.json` |
| public medium source | WP6 scenario SHA-256 `9f19aee5441449a4fdb952c48d82373fe1c030eb0e53e982c9a7604678867bca` |
| external evidence root | `E:\RideBoundData\wp7\results\candidate-portfolio-v8-identity-hotpath-20260817` |

## Actual adapter gates on Runner v8

| Gate | B1 | C1 |
|---|---|---|
| capability probe | pass | pass |
| Runner preflight | pass | pass |
| actual FleetControl preflight | pass | pass |
| lifecycle matrix | 6/6 | 6/6 |
| FleetPy clock tiny | 2 exact repeats | 2 exact repeats |
| public-medium physical loop | see below | see below |

### Resource caveat for this medium matrix

Part of the B1 arm ran while a targeted .NET test project and the format gate were
executing on the same machine. The raw wall/CPU/RSS values in `run-0*.json` are
therefore contaminated by concurrent load and **must not** be compared against the
Runner v6 matrix or used for any timing statement. They are retained unaltered as raw
diagnostics, which is the standing policy for these records; no claim in this document
depends on them. Semantic identity and repeat determinism are unaffected by load.

<!-- MEDIUM-RESULTS -->

## Final quality gates

| Gate | Result |
|---|---|
| `dotnet test RideBound.slnx` | **798/798**, 0 failed, 0 skipped |
| Contracts / Domain / Application | 135 / 136 / 73 |
| Algorithms / Runner / Architecture | 154 / 77 / 10 |
| Benchmarking / Benchmarking.Contracts / OrTools | 135 / 71 / 7 |
| `dotnet format --verify-no-changes` | clean |
| `dotnet build -c Release -warnaserror` | 0 warnings, 0 errors |
| pinned Python adapter (`unittest`) | **50/50**, no skip |
| Windows Application Control `0x800711C7` | did not recur |

## Reproduction boundary

Result files stay external and must not be copied into the repository. Never overwrite
`candidate-portfolio-v6`, `-v7` or `-v8` or their result roots: publish a new named
runner and evidence directory after any source, binary or config change. Before WP8,
retain the raw data but do not inspect it to choose an effectiveness metric, threshold,
policy or preregistration margin.
