# Review WP1–WP7 — logic, code and evidence

This folder is a Vietnamese walkthrough of the current RideBound source, not a test
count report. It explains what each boundary is allowed to do, what it must never do,
and how a later maintainer can re-run the evidence. The current verdict is **mechanical
correctness/reproducibility closed through WP7**. It is not a transport-effectiveness or
product verdict.

Read in this order:

1. [WP1–WP3 contract, state, ledger and publication gate](02-wp1-wp3-contract-ledger.md)
2. [Candidate generation, portfolio, hard vector and solver](01-core-candidate-and-solver.md)
3. [FleetPy adapter, lifecycle and actual evidence](03-fleetpy-adapter-and-evidence.md)
4. [File map, invariants and reproduction commands](04-file-map-and-reproduction.md)

The earlier [WP1–WP6 review](../wp1-wp6-final/README.md) remains historical evidence.
This folder supersedes it only for the final WP7-aware source/logic walkthrough.

## Final verdict

- Domain/Application remain independent of FleetPy, EF Core, ASP.NET, map providers and
  OR-Tools. FleetPy Python maps protocol data and launches the published Runner; it does
  not calculate candidates, hard budgets or an alternative solver answer.
- Candidate optimization is bounded and auditable. The service-set cost anchor has a
  stated B1 accepted/cost substitution proof; stable variants and B4 root priority have
  targeted regression evidence. It is not claimed as a globally optimal or novel
  dispatch algorithm.
- The independent WP3 validator remains authoritative. Candidate selection never grants
  a promise publication; only the physical/lock/budget path followed by ACK/checkpoint
  can publish the ledger state.
- Actual FleetPy B1/C1 runs use one pinned Runner artifact per source state, with pinned
  source/config identities, raw transcripts and external verification. They prove
  adapter mechanics and repeat determinism only.
- Candidate-core performance work is bounded by a work-profile gate: the search must
  dequeue the same nodes, build the same distinct slack profiles and retain the same
  candidates. Speed is never accepted as evidence on its own.

The authoritative current receipt is
[`wp7-015-hot-path-and-semantics-closure-evidence-2026-08-17.md`](../../benchmarking/wp7-015-hot-path-and-semantics-closure-evidence-2026-08-17.md);
[`wp7-014`](../../benchmarking/wp7-014-fleetpy-layer2-closure-evidence-2026-08-15.md)
is retained as the historical Runner v6 receipt.
