# FleetPy adapter and actual evidence walkthrough

## Ownership and boundaries

The source-owned adapter lives under `simulators/fleetpy-ridebound/ridebound_fleetpy/`.
The external checkout is never copied into this repository.

| Module | Responsibility | Deliberate non-responsibility |
|---|---|---|
| `mapping.py` | canonical IDs, decimal ties-to-even units, positions, requests, travel snapshots | Candidate search, commitment calculation or fleet optimization |
| `runner_client.py` | bounded external Runner process, NDJSON, artifact inventory, ACK/checkpoint/restart | local fallback policy |
| `fleet_control.py` | callbacks, offer/confirm/cancel, `VehiclePlan` mapping and reconciliation | bypassing a lock or recomputing a decision |
| `errors.py` | typed fail-closed errors | turning an error into a convenient rejection |

The adapter maps node `(node,None,None)` and directed edge `(start,end,fraction)` exactly.
It rejects partial positions, reverse/self edges, non-finite values and invalid fractions.
Travel snapshots are materialized from the routing engine in integer milliseconds, keyed
by version and direction; it never invents reverse, Euclidean or zero-cost arcs.

## Lifecycle and locks

`RideBoundFleetControl` buffers actual callbacks into gapless event sequence and epoch:
arrival, confirmation, cancellation, status/finished-leg, boarding, alighting, timer and
travel update. It maps Runner suffixes to `PlanStop`/`VehiclePlan`, preserves an active
locked leg exactly and calls FleetPy with `force_assign=False`. Post-assignment it checks
the vehicle plan, request membership and request-to-vehicle index; mismatch fails closed.

The lifecycle matrix covers pending cancellation, capacity rejection, offered decline,
confirmed cancellation, dynamic travel and unsafe restart. All six cases close without a
leaked plan/passenger/index; unsafe restart returns `RBWP7_RESTART_UNSAFE`.

## Actual evidence

Unit tests cover mapping and typed failures. The final gate additionally runs the pinned
upstream `FleetControlBase`, `SimulationVehicle`, `NetworkBasic` and `VehiclePlan`.
Capability probe pins tag/commit/six hashes, 13 callbacks, edge mutation and non-forced
assignment. The tiny real clock runs two semantic-identical repeats per arm.

The public medium case is physical, unlike WP6 instant-drain: 32 vehicles move through a
96-node directed network with 9,120 arcs while 128 requests arrive. Each arm launches
one external Runner artifact three times, retains transcript/run/resource/manifest files
and is checked by a separate verifier for protocol frames, events, epochs and
checkpoints. The current-source artifact is Runner v8
`13bf5d9b…c179e`; the v6 root of ADR-038 is kept unchanged as historical evidence.

## Reading a semantic hash across two Runner binaries

`RunManifestIdentity` includes `binarySha256`, so the manifest hash — and therefore the
checkpoint binding hash and the run's composite semantic hash — **changes whenever the
Runner artifact changes, even if behavior is identical**. That is deliberate provenance,
but it means a semantic hash can only ever be compared within one artifact. Two further
traps sit next to it: the harness `--label` flows into `run_id` and therefore into the
manifest, and the publication id is derived per run, so both must be held fixed before
any comparison is meaningful.

The correct cross-binary differential compares behavioral fields. Run with an identical
label against each artifact and compare publication (`promiseVersion`, `reasonCode`,
`requestId`, `sourceEventSeq`), `requestState`, `travelSnapshotVersion`,
`vehiclePosition`, `nextEpoch`, `nextEventSeq` and the exact physical drain count. For
v7 against v8 every one of those was identical and only the manifest-derived hashes
moved, which is what "behavior-neutral optimization" has to look like when it is true.

This proves faithful adapter mechanics and repeat determinism under pinned inputs. It
does not say which policy is a better transport system; publication counts and raw
CPU/wall/RSS values are retained rather than ranked. Pilot inference remains WP8/WP9.
