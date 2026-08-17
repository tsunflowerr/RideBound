# WP6 common benchmark contract v1

> Decision status: locked by ADR-026 / `RB-WP6-001`  
> Contract version: `1.0.6`  
> `1.0.1` correction: scenario identity is computed before the normalization report;
> the impossible report ↔ scenario hash cycle in `1.0.0` is forbidden by ADR-027.
> `1.0.2` correction: failure-record `1.0.1` and `wp6-failure-v1.0.1`
> enumerate caller cancellation plus every declared process/stream resource breach;
> ADR-028 forbids collapsing those failures into crash/protocol categories.  
> `1.0.3` correction: failure-record `1.0.2` and `wp6-failure-v1.0.2`
> add typed interrupted-persistence recovery; ADR-029 forbids reporting a harness
> write crash as a Runner process crash.  
> `1.0.4` correction: ADR-033 binds both Runner-visible policy files, explicitly
> sources the solver seed from the per-run manifest, permits independently verified
> per-run oracle execution summaries and fixes protocol failure-stage mapping. No
> existing JSON document field or failure code is removed.  
> `1.0.5` correction: ADR-034 binds the medium public derivative to an explicit
> synthetic-policy config and nonphysical instant-drain driver; source claims/pins are
> ordinal-preflighted before execution, and only semantic—not sampled-resource—identity
> is required across fresh processes. No public JSON field or failure code is removed.  
> `1.0.6` correction: ADR-035 makes the declared warm-up executable, validates every
> sorted/unique provenance collection and public policy binding before a run, derives
> terminal conservation from the compiled grid, and canonicalizes unsupported
> conversation failures before persistence. It also locks semantic/resource evidence
> separation for the adversarial gate. No public JSON field or failure code is removed.  
> Scope: common benchmark mechanics only; no confirmatory effectiveness claim

## 1. Purpose and ownership

This document is the decision-complete, non-executable contract that precedes WP6
JSON schemas and code. Ticket `RB-WP6-002` must translate these tables into strict
types/schemas without changing semantics. Any semantic change requires an ADR and a
version bump; an implementation detail that preserves canonical bytes and behavior
does not.

Pipeline:

```text
immutable source artifact
→ verified deterministic normalizer
→ canonical scenario + exact adapter/Runner input
→ one terminal run record + raw transcripts/resources/failures
→ deterministic metric rows + independent oracle comparison
→ strict BagIt-compatible self-verifying bundle
```

Ownership boundaries:

| Concern | Owner | Forbidden shortcut |
|---|---|---|
| source/license/download | WP6 dataset registry | adapter-local undocumented URL/file |
| normalized scenario identity | WP6 scenario contract | filename or mutable database ID |
| event/decision semantics | versioned RideBound protocol/Runner | metric/harness reimplementation of core |
| simulation execution | adapter/fixture driver | WP6 metric code inventing vehicle state |
| policy decision | exact pinned `RideBound.Runner` process | linked/in-process alternative policy path |
| raw evidence | WP6 run store | aggregate-only output |
| metric definition | WP6 metric registry | simulator/Runner self-reported aggregate |
| metric recomputation | independent oracle | calling production calculator |
| bundle integrity | WP6 strict BagIt profile | zip file alone |
| claim wording | WP6 claim checker | README prose bypass |

Domain and Application remain independent of WP6, simulator, filesystem and process
types. A future benchmark-contract library may depend on `RideBound.Contracts` for
canonical/protocol primitives; `RideBound.Contracts` must not depend on it.

## 2. Global lexical and canonical rules

### 2.1 Primitive rules

| Primitive | Rule |
|---|---|
| contract version | exact `MAJOR.MINOR.PATCH` decimal string |
| identifier | 1–128 UTF-8 bytes; artifact/path IDs additionally match `[a-z0-9][a-z0-9._-]{0,127}` |
| SHA-256 | exactly 64 lowercase hex characters |
| MD5 source checksum | exactly 32 lowercase hex; source verification only, never semantic identity |
| canonical integer | decimal JSON integer in `[-9007199254740991, 9007199254740991]`; no exponent, fraction or `-0` |
| quantity | non-negative canonical integer; unit is explicit in field name and metric registry |
| timestamp | RFC 3339 UTC string ending `Z`, used only for provenance; simulation semantics use integer ms |
| absent optional field | omitted; canonical documents never encode semantic absence as `null` |
| set | array sorted by the field-specific ordinal key, unique, marked as semantic set in schema |
| sequence | array order is semantic and must be preserved |

### 2.2 Canonical JSON

WP6 canonical documents use `RideBound Canonical JSON v1`, the strict accepted-domain
subset already defined in WP1:

- UTF-8 without BOM;
- no comments/trailing commas/duplicate properties;
- valid Unicode only; raw string value preserved without Unicode normalization;
- object properties recursively sorted by ordinal UTF-16 code units;
- arrays preserve semantic order;
- strings, booleans and canonical integers only; no `null` or floating point;
- no insignificant whitespace.

This accepted domain is compatible with the relevant RFC 8785 rules but is narrower.
WP6 must not claim general RFC 8785 conformance.

### 2.3 Hash framing

Every semantic hash uses SHA-256 over:

```text
UTF8(domain + NUL)
for each frame in declared order:
  uint16_be(tag_utf8_length)
  tag_utf8
  uint64_be(value_byte_length)
  value_bytes
```

Domains:

| Identity | Domain | Frames |
|---|---|---|
| scenario | `RideBound.Wp6.Scenario.v1` | `canonicalScenarioContent` |
| normalizer report | `RideBound.Wp6.NormalizationReport.v1` | `canonicalReport` |
| benchmark plan | `RideBound.Wp6.BenchmarkPlan.v1` | `canonicalPlanContent` |
| run | `RideBound.Wp6.Run.v1` | `planHash`, `scenarioHash`, `armId`, `repeatIndex`, `attemptIndex` |
| metric set | `RideBound.Wp6.MetricSet.v1` | `runId`, `registryHash`, `canonicalSortedRows` |
| bundle metric set | `RideBound.Wp6.BundleMetricSet.v1` | `planHash`, `registryHash`, exact LF-framed all-run metric rows |
| logical bundle | `RideBound.Wp6.Bundle.v1` | `canonicalBundleManifest` |

Content files also receive plain SHA-256 for BagIt/file integrity. Plain file hash and
domain-separated semantic identity are deliberately different fields.

Frame value encoding is executable and locked by `RB-WP6-002`:

- SHA-256 identity fields are decoded to their exact 32 digest bytes;
- identifiers are exact UTF-8 bytes;
- integers are canonical decimal UTF-8 without sign padding;
- canonical documents are exact RideBound Canonical JSON v1 UTF-8 bytes.

Published vectors are in
`benchmarks/fixtures/wp6/contracts/identity-vectors.json`; changing any encoding,
domain, tag, frame order or value requires a version/ADR change.

## 3. Dataset descriptor v1

Every dataset, including synthetic fixtures, has one immutable descriptor.

| Field | Type | Semantics |
|---|---|---|
| `schemaVersion` | version | `1.0.0` |
| `datasetId` | artifact ID | stable logical release ID |
| `datasetKind` | enum | `public`, `synthetic`, `restrictedReal` |
| `title` | string | source title |
| `releaseVersion` | string | source release, never `latest` |
| `persistentUri` | string | DOI or authoritative immutable landing page |
| `downloadUri` | string | exact allowlisted artifact URL |
| `retrievedAtUtc` | UTC string | provenance only; excluded from dataset content identity |
| `publisherArtifactName` | string | exact filename |
| `publisherArtifactLengthBytes` | integer | exact downloaded byte length |
| `publisherMd5` | checksum | required when publisher supplies it |
| `sourceArtifactSha256` | SHA-256 | exact downloaded bytes; required before normalization |
| `licenseSpdx` | string | e.g. `CC-BY-4.0` |
| `licenseUri` | string | authoritative license page |
| `citation` | string | required attribution text |
| `composition` | string | what records/files mean |
| `collectionLimit` | string | known source/selection limits |
| `allowedUse` | sorted strings | explicit allowed benchmark uses |
| `forbiddenClaim` | sorted strings | claims data cannot support |
| `directIdentifierStatus` | enum | `noneObserved`, `removedBySource`, `presentRestricted` |
| `locationPrecisionClass` | enum | `roadNode`, `zone`, `coordinateE7`, `synthetic` |
| `retentionClass` | enum | `localRawCache`, `redistributableDerivative`, `restrictedNoRedistribution` |
| `maintenanceNote` | string | update/deprecation/source contact note |

Locked public descriptor:

- `datasetId = fleetpy-manhattan-zenodo-15187906-v1`
- `releaseVersion = 1.0`
- DOI `10.5281/zenodo.15187906`
- `licenseSpdx = CC-BY-4.0`
- source file `FleetPy_Manhattan.zip`
- publisher MD5 `8b11882ae9c6d87f666bf6e006806744`

The exact length and local SHA-256 are filled only by a successful downloader; a
descriptor with either missing is not normalization-ready. This is measured source
provenance, not an open design decision.

Raw-cache rules:

1. cache root is explicitly supplied and must be outside tracked payload;
2. downloader writes a new temporary file, verifies length/checksums, then atomically
   promotes it to content-addressed read-only storage;
3. existing path with mismatching bytes fails; it is never overwritten in place;
4. extraction rejects absolute paths, `..`, symlink/reparse entries, case collisions,
   duplicate paths, decompression bombs and files not in the recorded inventory;
5. normalizer reads verified extraction only and never mutates it.

## 4. Normalization report v1

| Field | Type | Semantics |
|---|---|---|
| `schemaVersion` | version | `1.0.1` |
| `reportId` | artifact ID | content-bound report ID |
| `datasetId` | artifact ID | descriptor identity |
| `sourceArtifactSha256` | SHA-256 | downloaded raw bytes |
| `sourceMemberInventorySha256` | SHA-256 | canonical sorted extracted member path/length/hash list |
| `normalizerId` | artifact ID | `fleetpy-manhattan-normalizer` or fixture generator ID |
| `normalizerVersion` | version | semantic normalizer version |
| `normalizerSourceSha256` | SHA-256 | exact source inventory hash |
| `configurationSha256` | SHA-256 | canonical transform config |
| `inputRecordCount` | integer | all parsed source records before rules |
| `eligibleRecordCount` | integer | records passing preregistered eligibility rules |
| `selectedRecordCount` | integer | deterministic selection count |
| `excludedRecordCount` | integer | `input - eligible`, exact |
| `selectionFrameSha256` | SHA-256 | selected/not-selected disposition frame for every eligible row |
| `exclusionLogSha256` | SHA-256 | append-only normalization exclusions |
| `roundingRuleId` | enum | `ties-to-even-v1` |
| `eventOrderingId` | enum | `ridebound-event-order-v1` |
| `selectionRuleId` | artifact ID | hash-ranking/window rule |
| `scenarioContentSha256` | SHA-256 | canonical scenario file bytes |
| `scenarioHash` | SHA-256 | domain-separated scenario identity |

Conservation invariants:

```text
inputRecordCount = eligibleRecordCount + excludedRecordCount
selectedRecordCount <= eligibleRecordCount
every source record has exactly one selected/not-selected/excluded disposition
```

Not-selected eligible rows are not “exclusions”; they are selection-frame members and
their count/hash remain in the report.

## 5. Scenario content v1

`scenario-content.json` is self-contained for semantic inputs but contains no
self-referential `scenarioHash`. Its canonical bytes create that hash.

### 5.1 Top-level fields

| Field | Type | Semantics |
|---|---|---|
| `schemaVersion` | version | `1.0.1` |
| `scenarioId` | artifact ID | human-stable label, not identity alone |
| `scenarioKind` | enum | `protocolFixture`, `publicDerivative`, `syntheticStress` |
| `evidenceClass` | enum | `mechanical`, `development`, `pilot`, `confirmatory` |
| `datasetId` | artifact ID | source descriptor |
| `sourceArtifactSha256` | SHA-256 | raw source |
| `sourceSelectionSha256` | SHA-256 | exact selected source row/member identities |
| `normalizerId` | artifact ID | transform owner |
| `normalizerVersion` | version | semantic transform version |
| `normalizerSourceSha256` | SHA-256 | code identity |
| `normalizerConfigurationSha256` | SHA-256 | transform configuration |
| `eventOrderingId` | enum | `ridebound-event-order-v1` |
| `driverSemanticsId` | artifact ID | fixture/FleetPy/RidePy driver semantic version |
| `timeWindow` | object | source/scoring horizon below |
| `fleet` | sorted array | explicit initial vehicles |
| `requests` | sorted array | complete normalized request population |
| `travelSnapshots` | ordered array | exact directed travel realizations |
| `events` | ordered array | exogenous/fixture driver event plan |
| `validationSummary` | object | counts and invariant hash |

Identity/provenance order is a directed acyclic graph:

```text
verified source + member inventory + config + selection
→ canonical scenario bytes → scenarioContentSha256 + scenarioHash
→ normalization report (which records those two hashes)
→ normalizationReportHash → logical bundle
```

Scenario content deliberately does not contain `normalizationReportHash`. Requiring
that field while the report also contains `scenarioContentSha256` and `scenarioHash`
would require a cryptographic fixed point and cannot produce an executable artifact.
The scenario still binds source artifact, source selection, normalizer source/version/
configuration and validation summary; the later report and bundle bind conservation,
exclusions and scenario outputs.

### 5.2 Time window

| Field | Rule |
|---|---|
| `sourceTimezoneId` | IANA timezone string; FleetPy Manhattan uses `America/New_York` |
| `sourceWindowStartUtc` / `sourceWindowEndUtc` | RFC 3339 provenance strings |
| `warmupStartMs` | always `0` in normalized simulation time |
| `scoreStartMs` | `>= warmupStartMs` |
| `horizonEndMs` | `> scoreStartMs` |
| `drainEndMs` | `>= horizonEndMs`; requests cannot arrive after horizon, already accepted work may drain |
| `batchingId` | `event-driven-v1` for WP6 fixtures; adapter must declare any other value |

Warm-up events are retained in raw transcript and state. Metric rows whose registry
scope is `scoringWindow` exclude events before `scoreStartMs`; failure/resource rows
never exclude warm-up cost. Horizon values are scenario inputs, not hidden defaults.

`RB-WP6-008` locks the first executable metric boundary without changing wire schema
or the already published metric meanings:

- `warmup = [warmupStartMs, scoreStartMs)`;
- `scoring = [scoreStartMs, horizonEndMs]`;
- `drain = (horizonEndMs, drainEndMs]`;
- `all = [warmupStartMs, drainEndMs]`;
- request arrived/accepted/rejected/completed/acceptance/completion and defer-action
  rows use the request's arrival cohort, so a completion during drain does not move a
  scoring arrival into a different denominator;
- decision/certificate/promise/breach/vector rows use decision-envelope time;
- transcript time/epoch and request arrival→defer/terminal→completion lifecycle must
  be valid before metrics are emitted. Failed/excluded or inconsistent evidence emits
  no success metric rows.

These rules fill the previously unimplemented boundary of contract `1.0.3`; no field,
schema identity, registry definition or published hash vector changes, so there is no
contract-version bump. Any future change to these intervals/cohorts requires a metric
version and ADR rather than silently reusing `1.0.0`.

### 5.3 Fleet row

| Field | Type/rule |
|---|---|
| `vehicleId` | unique opaque ID; sort key |
| `capacity` | positive integer |
| `occupiedSeats` | `0..capacity` |
| `position` | exact protocol node/edge-progress union |
| `onboardRequestIds` | sorted unique IDs |
| `acceptedRequestIds` | sorted unique IDs, disjoint from unknown IDs |
| `initialRoute` | exact protocol route contract |
| `sourceProvenanceId` | points to source/derivation row |

No vehicle is inferred from future request destinations. Generated fleet placement
uses the declared seed component and stable node hash ranking.

### 5.4 Request row

| Field | Type/rule |
|---|---|
| `requestId` | unique pseudonymous ID; sort key |
| `sourceRecordOrdinal` | non-negative stable source ordinal |
| `arrivalTimeMs` | within warm-up/horizon |
| `originNodeId` / `destinationNodeId` | graph nodes, different unless explicit zero-trip fixture |
| `earliestPickupMs` / `latestPickupMs` | `arrival <= earliest <= latest` |
| `maxRideTimeMs` | positive and `>=` direct travel time unless deliberate invalid fixture |
| `partySize` | positive |
| `serviceClass` | opaque versioned class |
| `commitmentPolicyId` | opaque ID supplied by scenario; WP6 does not choose O-002/O-003 |
| `sourceProvenanceId` | source/derivation row |

FleetPy/TLC does not contain commitment policy labels. Public derivative fixtures use
an explicitly synthetic policy assignment and are labeled `syntheticPolicyOverlay`;
it must not be called observed rider preference.

### 5.5 Travel snapshot row

Uses the existing protocol `travel-time-snapshot` contract:

- positive monotonically increasing version;
- exact snapshot hash;
- unique directed `(fromNodeId,toNodeId)` arcs, sorted ordinal;
- finite positive `travelTimeMs` for distinct nodes;
- complete directed reachability for every node pair reachable by a scenario action.

Unreachable handling is fail-closed:

1. source rows outside the declared strongly connected component receive typed
   `source.unreachable-node-pair` normalization exclusion;
2. a selected request/fleet/route with a missing required arc invalidates scenario;
3. normalizer never inserts zero, maximum integer, Euclidean fallback or reverse arc;
4. runtime snapshot update with missing previously required arc is invalid input.

### 5.6 Event ordering

If an upstream adapter provides a validated total source sequence, preserve it. For
source rows without one, order by:

```text
(simTimeMs, eventTypeRank, sourceRecordOrdinal, stableSubjectId)
```

`ridebound-event-order-v1` ranks:

| Rank | Event family |
|---:|---|
| 10 | `travelTimesUpdated` |
| 20 | `incidentResolved` |
| 30 | `incidentOpened` |
| 40 | `vehicleReachedStop` |
| 50 | `passengerAlighted` |
| 60 | `passengerBoarded` |
| 70 | `vehicleAdvanced` |
| 80 | cancellation events |
| 90 | `bookingConfirmed` / `offerDeclined` |
| 100 | `requestArrived` |
| 110 | `timerTick` |

Within a semantically atomic upstream batch, upstream order wins and the adapter
records `sourceSequencePreserved=true`. A tie cannot be resolved by collection or
thread scheduling.

The executable v1 event row stores `eventSequence`, `simTimeMs`, `eventType`,
`sourceRecordOrdinal`, `stableSubjectId`, `sourceSequencePreserved`,
`payloadCanonicalJsonHex`, `payloadSha256` and `sourceProvenanceId`. The hex field is
exact canonical protocol payload bytes: validation decodes it, requires byte-for-byte
canonical form and recomputes the plain SHA-256. This avoids an untyped nested JSON
escape hatch while retaining exact material for the later driver.

### 5.7 Validation summary

Required exact counts: vehicles, requests, nodes, directed arcs, snapshots, events,
excluded source rows, selected source rows, duplicate IDs, unreachable selected rows,
invalid time rows and overflow rows. All invalid counts must be zero for executable
scenario. `invariantSetHash` binds the versioned validator rule IDs.

## 6. Benchmark plan and pairing v1

### 6.1 Plan content

| Field | Semantics |
|---|---|
| `schemaVersion` | `1.0.0` |
| `planId` | artifact ID |
| `evidenceClass` | WP6 accepts only `mechanical` or `development` |
| `claimProfileId` | `wp6-mechanical-only-v1` |
| `scenarioHashes` | sorted unique scenario identities |
| `arms` | sorted unique arm records |
| `pairingClassId` | declared comparability class |
| `masterSeedHex` | 64 lowercase hex; public benchmark seed, not a secret |
| `warmupRunCount` | non-negative; not included in semantic outcome metrics |
| `measuredRepeatCount` | positive; WP6 gates require at least 3 |
| `runOrderId` | `hash-counterbalanced-v1` |
| `resourceProfileId` | immutable resource rule set |
| `failureRuleSetId` | `wp6-failure-v1.0.2` |
| `exclusionRuleSetId` | `wp6-exclusion-v1` |
| `metricRegistryHash` | exact metric definitions |
| `runnerArtifact` | exact executable/assembly/runtime inventory |
| `harnessSourceSha256` | source inventory hash |
| `oracleSourceSha256` | independent source inventory hash |

`runnerArtifact` has exactly `runnerExecutableSha256`, `runnerAssemblySha256`,
`contractsAssemblySha256`, `runtimeInventorySha256` and `launchContractId`. It owns
identity only; filesystem paths and process handles stay outside the contract assembly.

### 6.2 Arm record

| Field | Semantics |
|---|---|
| `armId` | stable benchmark arm ID |
| `policyId` / `policyVersion` | exact existing Runner policy identity |
| `policyConfigurationSha256` | exact normalized policy config |
| `effectiveConfigurationSha256` | config plus protocol/candidate/work binding |
| `candidateGeneratorId` / `candidateWorkBudget` | common when pairing requires it |
| `validatorVersion` | exact WP3 publication validator |
| `solverId` / `solverVersion` / `solverWorkBudget` | exact solver semantics |
| `capabilitySelectionSha256` | exact negotiated capabilities |
| `pairingClassId` | must equal plan class for paired inference |

Allowed initial comparability classes:

- `wp4-common-candidate-v1`: B1/B2/B3/B4/C1/C2 only when common raw candidate,
  validation and deterministic work budgets match exactly;
- `wp4-multiple-plan-v1`: B5 comparisons kept separate because candidate/pool
  semantics differ;
- `mechanical-single-arm-v1`: conformance/reproduction only, no policy comparison.

WP6 does not choose budget vector, material revision threshold or non-inferiority
margin. It only hashes caller-supplied versioned configurations.

### 6.3 Isolation and order

- one fresh Runner process per `(scenario, arm, repeat, attempt)`;
- no writable cache/state reused across arms or measured repeats;
- staged input/config bytes verified before and after process execution;
- arm order for each scenario/repeat is ascending HMAC-derived rank of `armId`;
- ties use ordinal `armId`;
- warm-up runs are separate run IDs and cannot donate output/cache to measured runs;
- executable repeat indices are one collision-free address space: warm-up uses
  `[0,warmupRunCount)`, measured uses
  `[warmupRunCount,warmupRunCount+measuredRepeatCount)`; phase is derived from this
  range and is not an unbound extra input to run identity;
- selective rerun is forbidden. An authorized bug-fix rerun creates a new plan or
  attempt for the entire affected grid, preserving superseded evidence.

## 7. Seed hierarchy v1

### 7.1 Derivation

Master seed is exactly 32 bytes represented by `masterSeedHex`. For each derived
component:

```text
HMAC-SHA256(
  key = masterSeedBytes,
  message = framed(
    domain = "RideBound.Wp6.Seed.v1\0",
    scenarioHash,
    repeatIndexCanonicalUtf8,
    componentId,
    stableItemIdOrEmpty))
```

Registered component IDs:

- `scenario-row-selection`
- `fleet-placement`
- `arm-run-order`
- `adapter-rng`
- `simulator-rng`
- `solver-rng`
- `failure-injection`
- `bootstrap-resample` (future statistics only; not used by WP6 metrics)

The full 32-byte digest is recorded. A component that requires signed non-negative
`int32` uses `bigEndianUInt32(first4) & 0x7fffffff`; zero is valid. Any other
conversion requires a component/version change.

For an exact Runner arm, the per-run `solver-rng` conversion is also the
`initializeRun.manifest.masterSeed`. The Runner may consume that value only when its
pinned launch contract contains the explicit
`--solver-seed-source manifest-master-seed` opt-in. Omitting the flag preserves the
Runner configuration's source seed; silently preferring either source is forbidden.
The plan's policy configuration identity must describe every Runner-visible policy
file. WP4 arms therefore bind the exact WP3 commitment configuration SHA-256 and WP4
algorithm configuration SHA-256 with `ridebound-wp4-policy-binding-v1`; hashing only
one of the two files is an asymmetric/unbound arm.

### 7.2 No-hidden-RNG rule

Code reachable from selection/order/simulation/solver configuration must not use
clock, GUID, process/thread ID, `Random.Shared`, implicit default seed, mutable global
RNG or unstable enumeration. Static analysis/search plus permutation/parallel tests
are required; a simulator with unavoidable hidden RNG is capability-excluded until
its seed/control is explicit.

## 8. Raw evidence contracts v1

### 8.1 Required per-run files

```text
runs/<runId>/run-record.json
runs/<runId>/input.ndjson
runs/<runId>/output.ndjson          # required after process start, may be empty on crash
runs/<runId>/stderr.txt
runs/<runId>/resource-samples.ndjson
runs/<runId>/artifact-preflight.json
runs/<runId>/artifact-postflight.json
runs/<runId>/observation-index.ndjson
runs/<runId>/failure-record.json       # failed only
runs/<runId>/exclusion-record.json     # excluded only
```

No transcript line is rewritten. NDJSON line number and canonical envelope hash form
the evidence locator.

`resource-samples.ndjson` is canonical LF-framed NDJSON. Every non-empty row has
exactly non-negative integer `elapsedMs`, `observedCpuTimeMs`,
`observedWorkingSetBytes`, `observedProcessCount`; elapsed time cannot regress and a
succeeded run must have at least one sample. Artifact receipts independently rederive
the domain-separated runtime inventory from sorted role/file/length/SHA entries and
pre/postflight must bind the same launch-command hash. `artifact-unavailable` is
accepted only as the exact empty zero identity for typed persistence recovery.

### 8.2 Observation index row

The harness emits an index without replacing raw transcript:

| Field | Semantics |
|---|---|
| `schemaVersion` | `1.0.0` |
| `recordSequence` | append-only positive sequence |
| `recordKind` | `inputEvent`, `outputDecision`, `decisionAck`, `checkpoint`, `runTerminal` |
| `runId`, `scenarioHash`, `armId`, `repeatIndex`, `attemptIndex` | complete run identity |
| `transcriptRole` | `input` or `output` |
| `lineNumber` | positive exact NDJSON line |
| `envelopeSha256` | canonical envelope bytes |
| `epochId`, `simTimeMs` | required when protocol context has them |
| `eventSequence` | required for event row |
| `requestIds`, `vehicleIds` | sorted IDs extracted from that envelope |
| `decisionHash` | required for decision/ACK when present |
| `certificateHash` | plain SHA-256 of exact RideBound-canonical certificate-body JSON; required when decision carries a certificate body |

This satisfies event/request/vehicle/run/scenario/arm/repeat traceability without
pretending an index is the raw source.

The v1 store assigns index sequence deterministically to accepted input rows first,
then accepted output rows; each row still locates its original transcript role/line.
`runTerminal` remains a reserved schema enum and is not emitted by the v1 index because
`run-record.json` is the single authoritative terminal object.

### 8.3 Terminal run record

Exactly one terminal record exists for every planned measured/warm-up run.

Common fields:

- schema/version and complete run identity;
- plan/scenario/arm/config/seed/Runner/source hashes;
- `executionOrdinal`, `warmup` boolean and `terminalStatus`;
- UTC provenance start/finish plus monotonic `wallTimeMs`;
- `cpuTimeMs`, `peakWorkingSetBytes`, spawned process count and exit code when known;
- pre/post artifact hashes;
- input/output/stderr/resource/index file length/hash;
- last valid epoch/event/decision/checkpoint hashes when present;
- failure/exclusion record ID when terminal status requires it.

`terminalStatus` is one of `succeeded`, `failed`, `excluded`. Conditional fields are
omitted, never null. A succeeded run requires clean exit, complete expected protocol
shutdown, valid hash/ACK/checkpoint chain and no resource breach.

The executable terminal model names the five required file evidences `inputFile`,
`outputFile`, `stderrFile`, `resourceSamplesFile` and `observationIndexFile`; each is
`relativePath`, `lengthBytes`, `sha256`. `succeeded` requires exit code 0 and neither
failure nor exclusion ID; `failed` requires exactly a failure ID; `excluded` requires
exactly an exclusion ID.

### 8.4 Append-only publication and rerun rules

The store publishes a plan and each terminal run by directory rename from a private
staging root. It rejects reparse-point layout directories, duplicate semantic grid
cells, cross-run evidence locators, extra run files and any source evidence whose
length/SHA changed between pin and copy. Failure/exclusion records share one gapless
plan-level sequence and previous-entry SHA chain. Verification regenerates the
observation index from raw transcripts and revalidates protocol context, manifest
scenario/config/binary identity, decision/ACK chain, checkpoint applied epoch/time,
artifact inventory and denominator conservation.

A retry with exactly the same terminal identity/evidence is idempotent. Any different
terminal result for that run is rejected. An authorized rerun atomically publishes a
new plan together with `supersedes.json`, must preserve the complete semantic grid and
denominator set, recursively verifies all prior terminal evidence, and never changes
the superseded plan. Sealing an interrupted plan races under the same per-run lock: the
single terminal winner is retained; every remaining intent becomes typed
`harness.persistence-incomplete` rather than silently disappearing.

## 9. Failure and exclusion v1

### 9.1 Failure taxonomy

| Code | Stage | Meaning |
|---|---|---|
| `input.invalid` | preflight | schema/semantic scenario or plan invalid |
| `artifact.mismatch` | pre/postflight | binary/config/source/input bytes differ |
| `capability.divergence` | negotiation | negotiated capability differs from plan |
| `process.start-failed` | execution | Runner could not start |
| `process.crash` | execution | unexpected non-zero/terminated process |
| `process.cancelled` | execution | caller cancelled the run; partial evidence remains terminal |
| `harness.persistence-incomplete` | persistence | harness stopped after plan intent but before an immutable terminal commit |
| `resource.wall-time-exceeded` | execution | declared monotonic wall limit crossed |
| `resource.cpu-time-exceeded` | execution | declared CPU limit crossed |
| `resource.memory-exceeded` | execution | declared memory limit crossed |
| `resource.process-count-exceeded` | execution | observed process-tree count crossed the declared limit |
| `resource.stdin-bytes-exceeded` | execution | attempted protocol input crossed the declared byte limit |
| `resource.stdout-bytes-exceeded` | execution | observed protocol output crossed the declared byte limit |
| `resource.stderr-bytes-exceeded` | execution | observed diagnostic output crossed the declared byte limit |
| `solver.unknown` | decision | truthful solver status is unknown/no solution |
| `protocol.invalid-output` | parsing | malformed/unsupported/extra output |
| `protocol.incomplete-output` | completion | missing decision/ACK/checkpoint/shutdown evidence |
| `state.divergence` | validation | recomputed hash/state/checkpoint mismatch |
| `metric.oracle-mismatch` | metrics | production/oracle rows differ |
| `bundle.invalid` | packaging | file/type/hash/inventory/provenance invalid |

`solver.unknown` remains a run failure for metrics that require a valid decision; it
is not transformed into zero objective or infeasible.

Failure record required fields: record sequence/ID, complete run identity, code,
stage, first observed monotonic offset, source component, evidence path/hash, human
safe message, retry authorization (`none` at WP6), and affected denominator IDs.

### 9.2 Exclusion taxonomy

Exclusion is allowed only before inspecting arm outcome and only by a rule present in
the plan's exact ruleset hash.

Initial rule IDs:

- `source.license-not-accepted`
- `source.checksum-mismatch`
- `source.invalid-record`
- `source.unreachable-node-pair`
- `scenario.exceeds-declared-capability`
- `scenario.unsupported-position-model`
- `arm.missing-required-capability`
- `arm.incomparable-pairing-class`

Exclusion row required fields: sequence/ID, rule ID/version/source hash, stage,
subject kind/ID, scenario/arm/repeat when applicable, `beforeOutcome=true`, evidence
path/hash, retained denominator IDs and human-safe reason. A ruleset cannot exclude
“bad objective”, “slow relative result”, “outlier after inspection” or failed run.

### 9.3 Denominator conservation

For every benchmark plan:

```text
planned runs = succeeded + failed + excluded
```

Every aggregate reports all three. Valid-run-only descriptive metrics may be shown
only beside planned-run failure/exclusion counts; they cannot replace the planned
denominator.

## 10. Metric registry and row v1

### 10.1 Metric row

| Field | Semantics |
|---|---|
| `schemaVersion` | `1.0.0` |
| `metricRegistryHash` | exact definitions/source |
| `metricId` / `metricVersion` | immutable meaning |
| `runId`, `scenarioHash`, `armId`, `repeatIndex`, `attemptIndex` | complete run identity |
| `scopeKind` | `run`, `epoch`, `request`, `vehicle` |
| `scopeId` | run ID or exact entity ID |
| `windowId` | `all`, `warmup`, `scoring`, `drain` |
| `valueStatus` | `observed`, `missing`, `notApplicable` |
| `valueInteger` | present only when observed |
| `unitId` | registry unit |
| `numeratorInteger` | required for ratio/mean when observed |
| `denominatorId` / `denominatorInteger` | exact denominator semantics |
| `rawEvidenceSha256` | transcript/index subset identity |
| `calculatorSourceSha256` | production metric source |

No IEEE float is stored. Ratios use parts-per-million with ties-to-even integer
rounding; means retain numerator/denominator and optionally scaled integer display.
Overflow checked with wider intermediate arithmetic; out-of-range is typed failure,
never saturation unless metric definition explicitly says so.

### 10.2 Mechanical metric registry v1

Semantic metrics recomputed from raw input/output transcript:

| Metric ID | Unit | Definition / denominator |
|---|---|---|
| `request.arrived.count` | count | distinct valid `requestArrived.requestId` in window |
| `request.accepted.count` | count | distinct IDs with first valid `requestAccepted` |
| `request.rejected.count` | count | distinct IDs with valid `requestRejected` and never accepted |
| `request.deferred.action.count` | count | valid defer actions, not unique requests |
| `request.completed.count` | count | distinct IDs with valid `passengerAlighted` |
| `decision.epoch.count` | count | valid decision envelopes |
| `promise.publication.count` | count | valid `promisePublished` actions |
| `promise.revision.count` | count | valid promise publications with `promiseVersion > 1` |
| `commitment.breach.count` | count | valid breach declarations |
| `certificate.non-normal.count` | count | valid certificates where `normalOperation=false` |
| `decisionDelta.<dimension>.sum` | native vector unit | exact sum over ten decision-delta dimensions |
| `decisionDelta.<dimension>.max` | native vector unit | exact maximum; missing when no publication |
| `request.acceptance.ppm` | ppm | accepted unique / arrived unique; missing at denominator 0 |
| `request.completion.ppm` | ppm | completed unique / accepted unique; missing at denominator 0 |

Resource metrics come from append-only supervisor samples and terminal record:

- `resource.wall-time-ms`
- `resource.cpu-time-ms`
- `resource.peak-working-set-bytes`
- `resource.process-count-max`

WP7/adapter metrics may extend the registry only with raw provenance and exact unit/
denominator/missing semantics. Runner/simulator-provided aggregate columns are never
accepted as the sole source.

### 10.3 Independent oracle

The independent oracle:

1. is a separate executable/test source tree with no project reference to production
   metric calculator or its models;
2. parses canonical raw NDJSON with `JsonDocument`/primitive code only;
3. reconstructs request/action/publication state from transcript;
4. computes every semantic registry row and its evidence hash;
5. sorts by `(runId, metricId, scopeKind, scopeId, windowId)`;
6. compares exact canonical rows and metric-set hash.

Production/oracle mismatch is `metric.oracle-mismatch`, invalidates the run bundle
and blocks aggregate/statistical analysis. Resource metrics are independently checked
against raw supervisor samples, not invented from transcript.

## 11. Resource accounting v1

Resource profile fields:

- hard/observed `wallLimitMs`, `cpuLimitMs`, `peakWorkingSetLimitBytes`,
  `maxProcessCount`, stdout/stderr/input byte limits;
- deterministic candidate/generation/validation/solver work budgets already owned
  by policy config;
- process-sample interval and enforcement kind (`osHard`, `supervisorKill`,
  `observedOnly`);
- process startup included/excluded flags for each reported metric;
- warm-up and measured repeat counts;
- machine/runtime metadata requirements.

Required provenance: OS/version, architecture, CPU model when available, logical
processor count, total memory, .NET runtime/SDK, container image digest if any,
filesystem type when material, power mode note, git commit, dirty/source inventory,
assembly hashes and command line with secrets removed.

Limits are local experiment controls. Claim checker rejects wording that turns them
into production throughput, latency or SLA evidence.

## 12. Strict BagIt-compatible bundle profile v1

### 12.1 Layout

```text
bagit.txt
bag-info.txt
manifest-sha256.txt
tagmanifest-sha256.txt
README.md
verify.ps1
data/bundle-manifest.json
data/benchmark-plan.json
data/datasets/*.json
data/scenarios/<scenarioHash>/*
data/runs/<runId>/*
data/failures.ndjson
data/exclusions.ndjson
data/metrics/production.ndjson
data/metrics/oracle.ndjson
data/provenance/*.json
data/provenance/oracle-execution/<runId>.summary.json
data/provenance/claim-profile.json
data/source-inventory/*.json
data/claim-check.json
data/verification-report.json
```

`bundle-manifest.json` lists every logical artifact under `data/` except itself with
path, length, SHA-256, media type, role, producer activity ID and source entities. It
also stores plan/metric-set/source/runtime hashes and logical bundle identity. The
BagIt payload manifest hashes `bundle-manifest.json`, resolving self-reference.

The logical manifest has exactly `schemaVersion`, `bundleId`, `evidenceClass`,
`claimProfileId`, `planHash`, `metricSetHash`, `sourceInventorySha256`,
`runtimeInventorySha256`, and an ordinal-sorted `artifacts` array. Each artifact has
`relativePath`, `lengthBytes`, `sha256`, `mediaType`, `role`, `producerActivityId`
and sorted `sourceEntityIds`. `bundleHash` is forbidden inside its own content.

`data/provenance/run-store-plan.json` is the exact canonical export of the immutable
WP6 plan-store declaration: `planHash`, sorted denominator IDs and sorted complete
`RunStoreIntent` grid. It closes fields that cannot be reconstructed from the public
plan alone, including exact per-run component seed and expected runtime inventory.
The bundle-level `metricSetHash` is
`RideBound.Wp6.BundleMetricSet.v1(planHash, registryHash, exact LF-framed rows)`;
the per-run `RideBound.Wp6.MetricSet.v1` identity remains unchanged.

If oracle execution summaries are present, their allowed set is exactly one
`data/provenance/oracle-execution/<runId>.summary.json` for every successful run and
no other path. Each exact canonical summary binds the independently launched oracle
assembly SHA-256, recomputed per-run metric-set hash, semantic evidence SHA-256,
resource-evidence SHA-256 and row count. Verification stage 9 independently
recomputes those values; a production/oracle row equality check alone is not enough.

`data/source-inventory/repository.json` records Git HEAD, raw porcelain status hash,
dirty flag and sorted `componentId/path/length/SHA-256` entries for harness, oracle and
verifier source. Component source hashes are rederived from those exact entries. A
base commit without working-tree entries is insufficient. Runtime provenance lists
and rederives role/file/length/SHA identities for Runner executable/assembly,
Contracts, harness, oracle and verifier assemblies; machine, metric registry and
run-store-plan file hashes are cross-bound by `reproducibility.json`. Claim profile
SHA-256 is also cross-bound; adding this required field advances the internal
reproducibility evidence shape to `1.0.1` without changing public benchmark-contract
fields.

Executable Draft 2020-12 schemas, schema inventory and positive/negative fixtures
live under `benchmarks/schemas/wp6/v1/` and
`benchmarks/fixtures/wp6/contracts/`. Runtime/schema top-level parity and all schema
references are test-gated.

### 12.2 Verification order

1. normalize paths and reject absolute/traversal/symlink/reparse/case collision;
2. require exact root/tag/data layout and no extra file;
3. validate BagIt version/encoding, exact payload oxum/reviewed verifier script and
   both SHA-256 manifests;
4. validate logical manifest schema, roles, lengths, hashes and union completeness;
5. validate source/config/Runner/runtime pre/postflight identities;
6. validate plan conservation and one terminal record per planned run;
7. validate transcript protocol/hash/ACK/checkpoint chains;
8. validate failure/exclusion append order and denominator conservation;
9. validate production/oracle metric equality, complete registry/window/run coverage,
   bundle metric-set identity, production recomputation from exact raw evidence and
   every supplied independent-oracle execution summary against runtime/raw hashes;
10. verify the exact ADR-locked claim profile, independently recompute the scoped
    claim report, reject forbidden/caveat/provenance mutations and only then emit a
    new external verification report.

`verification-report.json` inside a sealed bundle reports packaging-time verification.
An external verifier must not rewrite the sealed bag; it emits its report beside the
bag or into a new derived bag.

`RideBound.Wp6BundleVerify` hashes its own executing assembly and requires the same
`verifier-assembly` role in runtime provenance. Its report path must be new and outside
the bag. Existing destination bags, stale build staging and existing sidecars are
never overwritten. BagIt validity means byte/inventory completeness; RideBound's
later semantic stages are separately required and neither one proves scientific
effectiveness or independent reproduction.

## 13. Claim profile v1

WP6 bundle must say:

```text
Evidence class: mechanical/development.
Same-team clean-process repeatability only.
This bundle is non-confirmatory mechanical evidence.
No effectiveness, non-inferiority, production SLA, ACM badge,
independent reproducibility or replicability claim.
Resource measurements are local experiment controls only, not production latency,
throughput or SLA evidence.
Public trip data does not contain observed commitment preference or satisfaction.
```

`wp6-mechanical-only-v1` is canonical machine-readable JSON generated from the
source-locked profile catalog. Builder reserves and generates both profile and
`claim-check.json`; callers cannot supply them and the verifier has no profile CLI
switch. Any future profile requires an ADR, external evidence, source/profile change
and a new hash.

The checker reads only explicit claim-bearing selections: README; bundle manifest and
plan identity/evidence/resource labels; packaging-report labels; machine provenance
`fileSystemType`/`powerModeNote`/`containerImageDigest`; and repository `gitDirty`.
It never searches run transcripts, scenario/public-trip rows, dataset citations,
failure logs, metric rows or source-code prose. Each required negative caveat is
matched exactly once and masked before forbidden-phrase scanning, so truthful caveats
do not trigger their own forbidden words.

Matching is bounded and deterministic: NFKC, invariant case folding, diacritic removal,
selected Greek/Cyrillic confusable mapping, plus separate punctuation-as-separator and
punctuation-removed skeletons. Non-whitespace control, format/default-ignorable,
private-use and unassigned characters are rejected. A failure carries stable code,
rule/category, relative path, selector, bounded original excerpt and normalized
witness. This is a scoped anti-bypass profile, not a claim of full UTS #39 conformance
or general natural-language understanding.

The profile rejects, unless a future superseding profile supplies required external
evidence: `confirmatory`, `effective` and common performance synonyms,
`non-inferior`, production/deployment readiness, `SLA`, ACM badge vocabulary,
`Results Reproduced`, `replicated`, novelty/first/SOTA and user/rider satisfaction or
preference. Stage 10 recomputes the exact valid report; a forged report invalidates the
bundle even after all BagIt/logical hashes are consistently resealed.

## 14. Field-level traceability matrix

| Source field/artifact | Normalized field | Runner boundary | Raw evidence | Metric/use |
|---|---|---|---|---|
| Zenodo file bytes/MD5 | `sourceArtifactSha256`, descriptor | manifest `scenarioContentHash` transitively | pre/postflight | provenance only |
| source trip row ordinal/time | request ID + `arrivalTimeMs` | `requestArrived` | input transcript/event index | arrived denominator |
| source origin/destination node | request node IDs | request + travel arcs | input transcript | feasibility context, not satisfaction |
| source/network travel matrix | directed `travelTimeMs` | `travelTimesUpdated` | input transcript | state reconstruction only |
| declared fleet config/seed | explicit vehicle rows | `vehicleAdvanced` bootstrap | input transcript | vehicle/run scope |
| scenario tie rule | ordered event sequence | `eventSeq`/batch order | input transcript | replay identity |
| arm policy config | config/effective hashes | initialize manifest | preflight + initialized | pairing/provenance |
| HMAC seed labels | derived component seed | adapter/simulator/solver config | run record | repeat identity |
| Runner decision action | — | exact output envelope | output transcript/index | accept/defer/revision/breach metrics |
| decision certificate/hash | — | exact output/ACK/checkpoint | output/input transcript | validity gate, non-normal count |
| supervisor sample | — | outside core | resource samples | resource metrics |
| failure/exclusion rule | — | no zero/drop conversion | terminal/log row | planned denominator conservation |

## 15. Threat/failure model

| Threat | Required defense | Stop condition |
|---|---|---|
| dataset substitution/license ambiguity | DOI/version/license + exact bytes/checksums | no normalize |
| unsafe archive extraction | path/symlink/case/size guards | no extraction |
| nondeterministic normalization | canonical source ordinals, hash ranking, two-process equality | no executable scenario |
| hidden RNG/order | HMAC hierarchy + static/permutation/parallel gate | capability exclusion |
| arm asymmetry | pairing class + exact config/work/binary hashes | no paired claim |
| cache/process contamination | fresh process and isolated writable root | run failure |
| timeout/crash hidden as zero | typed terminal failure | bundle invalid if missing row |
| selective rerun/survivorship | immutable plan grid + attempt conservation | claim checker fail |
| metric circularity | raw transcript + independent oracle | bundle invalid |
| manual artifact edit | BagIt/source/producer hashes | bundle invalid |
| bundle traversal/extra/tamper | strict profile verifier | bundle invalid |
| dirty/unbound source or binary | source inventory + assembly/runtime hashes | reproducibility claim fail |
| clock/hardware confounding | monotonic timing + machine metadata + randomized order | runtime claim fail |
| privacy/claim leakage | dataset descriptor + safe logs + claim checker | export blocked |

## 16. Tiny and medium gates

### 16.1 Tiny gate

- source-controlled protocol/public derivative fixture;
- at most 8 requests, 2 vehicles, 16 nodes and 256 directed arcs;
- at least one acceptance, one rejection/defer, one promise revision and one
  commitment dimension with non-zero decision delta;
- three measured repeats for at least B1 and C1 mechanical paths;
- two clean harness processes produce exact plan/scenario/source/runtime/grid,
  per-run input/output/observation/decision and semantic-metric hashes. Physical
  resource rows and the logical/physical bundle identities containing them may differ
  while their registry, provenance and semantic subset remain exact;
- production and independent oracle rows match exactly;
- missing/extra/tamper/timeout/crash/unknown mutations are all rejected/typed.

### 16.2 Medium public-data gate

- deterministic derivative of FleetPy Manhattan v1 with attribution;
- 128 selected eligible requests, 32 vehicles, at most 96 selected nodes and 9,120
  non-self directed arcs; if the verified source cannot satisfy this without semantic
  fabrication, ticket stops and ADR is amended rather than silently changing data;
- selection is HMAC hash-rank within an explicit source time window, independent of
  policy output;
- scenario normalization is reproduced in two clean processes from the same verified
  archive;
- the Runner-visible commitment config must declare the exact synthetic policy ID
  embedded in the public derivative. A missing/mismatched policy is a binding failure,
  not permission to loosen the validator, relabel the data or accept a fallback result;
- the mechanical driver must validate negotiation, initialize identity, decision/ACK/
  checkpoint context, accepted request/vehicle/candidate/plan binding and lifecycle
  completion. It may instant-drain at source time only when its nonphysical semantics
  and forbidden KPI uses are machine/prose bound;
- source entity claims and artifact pins are unique and sorted ordinal at a preflight
  before expensive Runner execution;
- at least three measured mechanical repeats per B1/C1 arm in each of two fresh
  harness processes; no confirmatory aggregate;
- plan/scenario/source/runtime/grid/transcript/decision/semantic metric and all per-run
  semantic identities match across fresh processes. Real monotonic wall/CPU/memory
  samples, full metric rows and logical/physical bundle identities containing them may
  differ while remaining complete and provenance-bound;
- raw-to-metric equality, denominator conservation, resource/provenance capture and
  strict bundle verification pass.

WP6 medium gate may validate normalizer/scenario/bundle mechanics without claiming a
closed-loop FleetPy policy effect. WP7 owns the FleetPy control adapter and simulator
semantics. In particular, zero wait/ride values emitted by an instant-drain mechanical
driver are nonphysical test artifacts and must never be ranked, aggregated or reported
as effectiveness, service, fairness, non-inferiority, satisfaction or SLA evidence.

### 16.3 Adversarial closure gate

- Tiny and medium paired plans execute one isolated warm-up plus at least three
  measured repeats per arm. Warm-up and measured runs share the compiled identity
  address space but never share a process, writable root, output or cache.
- Plan conservation is derived from the complete compiled run grid. A harness must
  not use a literal expected run count, because adding a declared warm-up or arm would
  otherwise create an unbound terminal denominator.
- Claim IDs, absolute artifact pins and bundle source records are non-empty,
  ordinal-sorted and case-insensitively unique before any expensive run. Bundle source
  paths/media types are validated at the same preflight.
- A public-derivative scenario must contain one exact commitment-policy identity and
  the Runner-visible configuration must declare it. Empty, multiple or mismatched
  policies fail before process execution.
- Conversation failures may retain only the contract-owned negotiation, decision,
  parsing, completion and validation codes/stages. Any other code supplied by a driver
  is canonicalized to `protocol.invalid-output` at `parsing`; arbitrary taxonomy must
  never reach the immutable store.
- Required determinism covers canonical documents, scenario/plan/source/runtime/run
  grid, transcript, decision and semantic metric identities. UTC provenance,
  monotonic wall/CPU/memory samples, full resource metrics and manifests/bundles that
  contain those samples are intentionally not semantic equality keys.
- The required-mutant result reports the exact declared failure/exclusion/store/
  metric/bundle/claim mutation matrix. Passing it may be stated as 100% of that matrix
  killed, never as a repository-wide or general mutation score.
- Every local resource stratum, including slower treatment rows and failed resource
  controls, is retained and caveated. It cannot be converted into an effectiveness,
  non-inferiority, production-throughput, latency or SLA conclusion.

## 17. Acceptance and change rule

Contract v1 is executable-ready only when:

- schema/type implementation rejects unknown/missing/duplicate/overflow fields;
- all hash/seed vectors are cross-process stable;
- dataset descriptor/normalizer emit exact conservation reports;
- every planned run has exactly one terminal state;
- failures, exclusions and negative output are retained;
- production/oracle semantic metrics are byte-identical at tiny bound;
- medium public derivative is reproducible from verified source;
- bundle rejects missing, extra, tamper, path and provenance mutations;
- claim profile passes and no O-002/O-003/O-004 value is chosen here.

If implementation discovers an impossible field/semantics conflict, stop the active
ticket, record exact evidence and amend ADR-026/this contract. Do not make an
adapter-local exception or preserve schema while silently changing meaning.

## 18. Implementation closure record

ADR-036 closed `RB-WP6-014` on 2026-08-13 without changing contract `1.0.6`.
Fresh tiny A and public-medium H/I passed the exact external Runner/store/oracle/
bundle chain on the final exact source; H/I matched 16 top-level and 72 per-run
semantic fields with zero
mismatch while all eight resource-inclusive rows differed legitimately. See
[WP6-014 closure evidence](wp6-014-source-claim-closure-evidence-2026-08-13.md).
This is mechanical closure only; WP7 FleetPy closed-loop and WP8 preregistration
remain outside this contract.
