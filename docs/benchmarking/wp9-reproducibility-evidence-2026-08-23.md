# WP9 H6 reproducibility evidence

Date: 2026-08-23  
Verifier: `simulators/fleetpy-ridebound/wp9_reproducibility_verify.py`  
Result: **PASS**

The verifier independently read every raw transcript and bundle manifest rather
than trusting analyzer output. It verified 60 Panel A bundles and 40 Panel B
bundles, exact matrix/manifest coverage, terminal receipts with no failure,
scenario/derivative/normalizer/selection/driver provenance, and the frozen H6
Runner and tree seals.

For every cell it reconstructed the preflight's lexical source-node indexing,
the adapter's opaque node/request mapping, the exact `travelTimesUpdated` frame,
and all `requestArrived` frames. Baseline and treatment projections match the
reconstructed derivative exactly. Demand and travel realization hashes match
across capacity panels while scenario hashes remain distinct because fleet state
is part of scenario identity.

The four falsification conditions from WP8-010 pass:

1. Experimental-unit identity binds scenario, demand realization and travel realization.
2. Derivative provenance cross-links all source and normalization artifacts.
3. Both arms receive exactly the reconstructed exogenous demand/travel sequence.
4. A two-repeat H6 bundle has one behavioral projection hash and one checkpoint hash.

Seed 19 changes no measured aggregate in either arm but remains a solver
robustness setting, not a new experimental unit; confirmatory N increases by 0.

The exact frozen verifier SHA-256 is
`872135877c1241c591975a1f745a095c466df78558bb9201c386e63bf121a490`.
It was restored byte-for-byte for the closure run, then the backward-compatible
v1.1 evidence verifier was restored. Machine-readable closure facts and all
artifact hashes are in
[`evidence/wp9-h6-reproducibility-v1.json`](evidence/wp9-h6-reproducibility-v1.json).

Mutation coverage independently rejects changed demand, changed travel, repeat
behavior divergence, treating solver seed as unit identity, and event-order
changes. These checks establish artifact reproducibility for this finite panel;
they do not establish independent reproduction by another team or population
external validity.
