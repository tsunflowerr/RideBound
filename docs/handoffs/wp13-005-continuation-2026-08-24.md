# RideBound handoff — WP13-005 continuation

> Date: 2026-08-24
> Historical checkpoint at creation: `RB-WP13-005 IN PROGRESS`
> H6/WP9/WP10: immutable

> Superseded later on 2026-08-24: `RB-WP13-005 DONE`, `RB-WP13-006 READY`.
> Final `005` artifact SHA is `cdd9a28d…9e411`; see live status and the `wp13-005`
> evidence report rather than the intermediate hashes below.

## Completed in this continuation

- Closed `RB-WP13-004` under ADR-056.
- Comparator report: `E:\RideBoundData\wp13\behavioral-comparator-v1.json`,
  79,864 bytes, SHA-256
  `3717f093c62c37a339da0b826323fb1604a684bd9990630d9d9dc5563fd4f7e3`.
- Comparator source SHA
  `f2c55e1f7fbe9cb341cb6c75764a192254aa2e375de0547780c94c83b01dd0ee`;
  targeted 13/13, full pinned Python 135/135, required .NET 856/856.
- Result `004`: A C1-lower/equal = 3/17; B = 5/15; no C1-higher.

## Current WP13-005 implementation

New files:

- `benchmarks/schemas/wp13/v1/recorded-witness-relaxation-set.schema.json`;
- `simulators/fleetpy-ridebound/wp13_recorded_witness_relaxation.py`;
- `simulators/fleetpy-ridebound/tests/test_wp13_recorded_witness_relaxation.py`.

The first full run correctly failed at schema because witness `vehicleId` was omitted
from the schema. The schema and a real link-shape regression test were fixed. Targeted
tests are now 10/10 pass.

The corrected full run passed and produced:

- path: `E:\RideBoundData\wp13\recorded-witness-relaxation-set-v1.json`;
- length: 70,531 bytes;
- SHA-256: `af1bd7ab2a1593bd25bdd34f1c99f441804a63ed4cb37de30ff6f80b54658e9e`;
- calculator source SHA:
  `8ba6346cd434e16eb5345be11a8c863472bb3db9dccd273fe3ddbbe7cd86e9d5`;
- schema SHA:
  `e2abdd376072f0c3ad95e1282295707cd7b7f18e14a26e3d0cafbda3c7cd640d`;
- 40 records, 41 B1 actionful selected-candidate links;
- statuses: 33 `prunedWithCommitmentWitness`, 7
  `absentRetainedOrOmittedNotRecorded`, 1 `selectedByC1`, 0 non-commitment prune;
- clearances: 28 numeric budget limit increases, 5 categorical lock disablements;
- Panel A: 21 links = 17 commitment, 3 absent, 1 selected; 14 budget + 3 lock;
- Panel B: 20 links = 16 commitment, 4 absent; 14 budget + 2 lock;
- canonical JSON check passed.

Interpretation must remain `recordedWitnessOnly`: the validator is fail-fast, so
post-clearance candidate feasibility is `notEvaluated`; retained portfolio is
`notRecorded`. Never say these 33 candidates become feasible after the reported
clearance.

## Immediate next steps

1. Review schema/calculator/test source line by line; remove unused schema definition
   `linkBaseProperties` if still present, then update schema hash constant and rerun the
   full external artifact if any source/schema byte changes.
2. Add independent invariant reconciliation for the external artifact, including
   budget arithmetic, source hashes, no nulls and record/panel aggregates.
3. Run final targeted tests, full pinned Python suite (expected 145 tests), and exact
   `dotnet test RideBound.slnx` (expected 856).
4. Add source-controlled evidence summary and `wp13-005` report; add ADR-057; sync
   docs 00/16/18/19/tasks 23/42. Only then mark `005 Done`, `006 Ready`.
5. Remove only newly generated `wp13_recorded_witness_relaxation*.pyc` files if any;
   preserve all user files, especially
   `docs/reviews/ridebound-system-questionnaire-answers-2026-08-23.md`.

The active goal remains open; continue WP13 sequentially through its exit decision,
then only open WP14 if the documented gate permits it.
