# WP7 actual FleetPy tiny scenario

This source-controlled fixture is intentionally mechanical. It contains a
three-node directed network, one four-seat vehicle, one request and one dynamic
travel-time factor update. `actual_fleetpy_clock_preflight.py` binds the runtime
paths to the exact FleetPy 1.0.2 checkout, exact published Runner and exact B1/C1
configuration, then executes the upstream `BatchOfferSimulation.run()` clock.

The fixture proves callback, offer/confirmation, physical movement,
boarding/alighting, dynamic travel update, decision ACK/checkpoint and raw state
reconciliation. It is not public-data evidence and must not be used for
effectiveness, SLA, fairness, satisfaction or novelty claims.
