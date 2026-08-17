# WP1–WP3 contract, state and publication walkthrough

## WP1: protocol is the boundary

`RideBound.Contracts` defines versioned NDJSON, canonical hashes, initialize manifest
identity and explicit event/action payloads. `RideBound.Runner` owns the external process
conversation and never trusts an adapter's local state as authority. The adapter must
negotiate hello/init, submit a gapless event batch, receive a decision, ACK its hash and
checkpoint. Hash/config/source identity is checked before and after external process use.

This is why the FleetPy adapter cannot link the core or repair an answer locally: the only
way to change a decision is to change an explicit Runner input and create a transcript.

## WP2: state reduction and physical feasibility

`RideBound.Application/State/EventReducer.cs` reduces the ordered online event stream
into immutable `OnlineState`. Domain route types distinguish executed frozen prefix from
the mutable suffix. `PhysicalPlanValidator` is independent of the candidate heuristic:
it checks capacity, pickup/drop order, time windows, maximum ride duration, continuity
and executed-prefix protection using the travel snapshot at the decision boundary.

The decisive invariant is that a candidate cannot rewrite historical work. A FleetPy
finished leg becomes frozen/executed before the next Runner decision, so adapter code
cannot manufacture a route that retroactively serves a rider.

## WP3: ledger and publication authority

`CommitmentDecisionValidator` projects both the exogenous continuation of the reduced
route and the candidate continuation. `CommitmentLockEvaluator` uses the old published
promise only to decide whether the freeze horizon has activated; phase locks compare
**exogenous -> candidate**. Real vehicle movement may shift an estimate by milliseconds:
that exogenous drift is recorded, but is not a policy lock violation. A candidate change
still trips the lock. The regression
`Exogenous_drift_does_not_trip_final_lock_but_candidate_delta_does` protects this rule.

The validator calculates three-way deltas, charges the declared budget basis, checks hard
limits, appends an immutable ledger entry and creates a promise publication. Candidate
selection cannot bypass these stages. A Runner decision becomes an applied effect only
after matching `decisionApplied` ACK; checkpoint carries the restartable state/hash.

## Why WP7 follows the same chain

FleetPy offers are provisional. The adapter maps the offer route but withholds a promise;
only `user_confirms_booking` becomes `bookingConfirmed` and creates exactly one
`INITIAL_BOOKING_CONFIRMATION`. A pre-confirmation cancel creates no promise. Actual
FleetControl preflight and the six-case lifecycle matrix exercise these branches.

The chain is physical state -> candidate/solver -> full-fleet validator -> promise
validator -> Runner decision -> ACK -> checkpoint -> FleetPy round-trip reconciliation.
