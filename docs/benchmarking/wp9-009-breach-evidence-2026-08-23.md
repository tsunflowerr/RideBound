# RB-WP9-009 breach evidence and ledger bridge

Date: 2026-08-23  
Classification: post-outcome mechanism verification; not confirmatory evidence

`solver.executionEvidence` version `1.1.0` adds the canonical
`generation.exogenousServiceQualityBreaches` array. Version `1.0.0` remains
accepted so every frozen H6 bundle is still independently verifiable. The strict
Python verifier rejects unknown versions, missing/extra fields, non-canonical or
duplicate breach ordering, non-integer values, and observations where
`exogenousMilliseconds <= contractualMilliseconds`.

The runtime bridge independently reprojects the reduced pre-decision no-op route,
uses that same projection as both exogenous and safety projection, calculates the
three-way delta, and appends an `ExogenousServiceQuality` breach without creating
a fake operational incident. Domain invariants require:

- exact exogenous/safety projection equality;
- zero decision-induced delta;
- exogenous and visible deltas equal;
- `budgetBefore == attemptedBudgetAfter`;
- at least one canonical `PICKUP_WINDOW/latestPickupMs` or
  `MAX_RIDE_TIME/maxRideTimeMs` witness with an actual overrun;
- witness codes exactly equal the distinct serialized witness codes.

Historical operational-incident checkpoint bytes keep their old shape. New
exogenous records carry an explicit kind and service-quality witness array.
Decode validates every serialized projection, delta, budget and witness rather
than silently normalizing forged redundant fields.

The real FleetPy probe on Panel A/B1 cell `d20181114-s10-r1` passed independent
audited verification with 800 epochs, 3,830 events and 108 requests. Evidence
v1.1 recorded 43 breach observations in 43 epochs; the final checkpoint contains
exactly 43 exogenous ledger records and 43 service-quality witnesses. Across all
records, decision-induced nonzero count, budget-changed count and invalid-witness
count are all zero. Both `PICKUP_WINDOW` and `MAX_RIDE_TIME` occur. A second
Panel B/C1 probe passed 353 evidence-v1.1 epochs with the empty branch remaining
exactly empty.

The probe used a post-outcome Runner and therefore cannot rescue, alter or add
observations to the H6 service/burden estimands. Full hashes are recorded in
[`evidence/wp9-009-breach-bridge-smoke-v1.json`](evidence/wp9-009-breach-bridge-smoke-v1.json).
