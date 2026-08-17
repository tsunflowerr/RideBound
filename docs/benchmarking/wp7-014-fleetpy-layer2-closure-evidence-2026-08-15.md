# RB-WP7-014 — FleetPy Layer 2 closure evidence

> **Historical receipt.** Every number here is bound to Runner v6 and to the source
> state of 2026-08-15, and is retained unchanged for that reason. The current-source
> receipt is
> [`wp7-015-hot-path-and-semantics-closure-evidence-2026-08-17.md`](wp7-015-hot-path-and-semantics-closure-evidence-2026-08-17.md).
> Note in particular that the semantic hashes below cannot be compared against any
> other Runner artifact: `RunManifestIdentity` binds `binarySha256` by design.

> Date: 2026-08-15
>
> Evidence class: mechanical integration, reproducibility and correctness
>
> Claim profile: no effectiveness, SLA, fairness, satisfaction or novelty result

## Result

WP7 is closed for the stated mechanical Layer-2 scope. The candidate portfolio has a
bounded structural proof and exact-small gate; the actual FleetPy 1.0.2 adapter invokes
the same immutable RideBound Runner v6 for B1 and C1; preflight, lifecycle, tiny and
public-medium physical loops all complete with artifact receipt equality and independent
transcript verification.

This is deliberately not a comparison claiming that C1 is better than B1. The two arms
have different decision policies and therefore different semantic hashes and publication
counts. Their raw resource values are retained as diagnostics, not normalized performance
or service metrics.

## Immutable inputs

| Input | Locked identity |
|---|---|
| FleetPy | tag `1.0.2`, annotated tag `ca5a245243094236c84a0e93b32819ee502beeff`, commit `053aa9d4fcfde91c5d303435d5748f9206c071b0`, MIT |
| Python | CPython `3.10.20`, `win-64` lock in `simulators/fleetpy-ridebound/environment.lock.yml` |
| Runner | `E:\RideBoundData\wp7\runner\candidate-portfolio-v6\RideBound.Runner.dll` SHA-256 `8a227fcd44e2c8e9814821bce317ea07f59c6fe9766dd26b6b8533a8129b75a2` |
| B1 config | `benchmarks/configurations/wp7-fleetpy-rolling-cost-v1.json` |
| C1 config | `benchmarks/configurations/wp7-fleetpy-ridebound-hard-vector-v1.json` |
| public medium source | canonical WP6 scenario SHA-256 `9f19aee5441449a4fdb952c48d82373fe1c030eb0e53e982c9a7604678867bca` |
| external evidence root | `E:\RideBoundData\wp7\results\candidate-portfolio-v6-20260815` |

The capability probe verifies the clean checkout, tag/commit, environment packages and
six source file hashes before importing the adapter. It also executes the upstream
position behavior: the directed position `(11,12,0.375)` round-trips and
`SimulationVehicle._move` mutates it to `(11,12,0.625)`. The adapter passes
`force_assign=False` and checks the locked route rather than overriding FleetPy locks.

## Candidate-core evidence

`CandidatePortfolioRetainer` is not an arbitrary conditional. For every legacy-retained
candidate at a bounded cap, its exact vehicle/service-set has a retained portfolio cost
anchor with no greater operational cost. That set uses the same solver conflict columns,
so replacing a legacy plan by its anchor preserves accepted requests and cannot increase
that plan's cost. The proof does not claim preservation of the final CandidateId
tie-break, a global optimum for all arbitrary generators, or a universal C1 theorem.

The opt-in portfolio first uses accepted-count tier and legacy rank to keep a cheapest
representative of each service set, then reserves a stable route variant when there is
room, then uses legacy fill. The stable comparison is deterministic: unchanged incumbent
prefix, inserted stops before incumbent pickups, and integer service-start shifts. New
route checks require every no-op incumbent stop to be preserved exactly and require all
introduced request stops to equal `NewRequestIds`; this makes the service-set proof
meaningful instead of relying on a label alone.

The B4 search repair correction evaluates each repair root on its own mutable suffix.
Previously a repair root could be scored using the unrepaired route's slack under a tight
best-first work budget. The regression records every projected repair route before the
budget can choose a root, so the new calculation is an optimization of actual bounded
search ordering, not merely an `if` branch.

Evidence includes per-cap B1 anchor dominance, exhaustive small variants, a strict
adversarial fleet result `2 -> 4` accepted requests, 128 exact-small C1 real-validator
seeds with a strict-positive witness, permutation/loss accounting checks, legacy config
compatibility, and the production-policy differential. See
`docs/reviews/wp1-wp7-final/01-core-candidate-and-solver.md` for code-level detail.

## Actual adapter gates

All commands used the pinned Python with `-X dev -W error::ResourceWarning`.

| Gate | B1 | C1 | Required assertion |
|---|---:|---:|---|
| Runner preflight | pass | pass | hello/init, provisional offer does not publish, confirmation publishes exactly one promise, ACK/checkpoint and pre/post artifact receipts |
| actual FleetControl preflight | pass | pass | offer, confirmation, physical board/alight, directed travel update, restart/checkpoint, non-forced plan application |
| lifecycle matrix | 6/6 | 6/6 | pending cancel, capacity rejection, offer decline, confirmed cancel, dynamic travel, unsafe restart typed `RBWP7_RESTART_UNSAFE` |
| FleetPy clock tiny | 2 exact repeats | 2 exact repeats | actual clock, offer/confirm/board/alight/travel/checkpoint and complete physical drain |
| public-medium physical loop | 3 exact repeats | 3 exact repeats | all 128 requests, real directed routing and the external Runner |

The source-controlled tiny runs produced repeat-stable semantic hashes
`9c05414a...494735` for B1 and `a7e65aa4...d8de0` for C1. FleetPy 1.0.2 emits an
upstream warning that `BatchOfferSimulation` does not override a future-ABC hook. The
pinned version deliberately permits that hook and all actual lifecycle checks pass; it is
recorded as an upstream-version caveat, not suppressed or treated as a result.

## Public-medium actual output

Independent verifier input is the generated bundle, not the process's success message.

| Arm | semantic hash | transcript/checkpoint facts per repeat | bundle manifest SHA-256 |
|---|---|---|---|
| hard vector C1 | `192add771df37f8906ef9b7dfac6b10dc3433a34d82b49dd240fea4ee3ab2eea` | 128 requests, 13,277 events, 3,082 frames, 1,025 epochs, 495 publications, checkpoint `33a6fa...7b94a` | `e8f03b56137d9ca54ebeef802cb5c3da0e3cab600c73c08ce42a4c13ae41274e` |
| rolling-cost B1 | `4499d1f19641d1fc7925db56a3a21e9a561b1cbf6b8124bfcb290675e0dd1cd3` | 128 requests, 13,277 events, 3,082 frames, 1,025 epochs, 482 publications, checkpoint `564dec...29c2` | `829eb76645a4c751af5a3bf25f298ed9608ac320351a1713a054b43c9838689f` |

Within each arm all three transcripts have the same SHA-256 and semantic/checkpoint
identity. The raw `run-00..02.json` files retain wall, user CPU, system CPU and RSS
values. C1 wall time is respectively 641,400 / 355,448 / 379,196 ms; B1 wall time is
372,699 / 362,541 / 360,005 ms. Cold/warm variation and different policy work mean these
numbers cannot support an arm-performance conclusion.

## Final quality gates

The format gate is clean, the full .NET solution passes 790/790, and the actual Python
suite passes 49/49 with no skip. Windows Application Control did not block Contracts or
Runner in this final .NET run. A prior CPU ceiling failure is retained as a transient
resource negative, and the same exact medium test subsequently passed alone and again in
the final full suite without raising its 120-second CPU limit.

## Reproduction boundary

Result files are intentionally external and must not be copied into the repository.
Never overwrite `candidate-portfolio-v6` or its result root: produce a new named runner
and evidence directory after any source/binary/config change. Before WP8, retain the
present raw data but do not inspect it to choose an effectiveness metric, threshold,
policy or preregistration margin.
