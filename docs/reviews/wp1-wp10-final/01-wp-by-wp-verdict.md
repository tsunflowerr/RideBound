# WP-by-WP logic verdict

| WP | Boundary revalidated | Verdict |
|---|---|---|
| WP1 | Strict versioned envelopes, canonical integer JSON, hash framing/chain, NDJSON lifecycle, retry identity | PASS. Duplicate/unknown/noncanonical input and changed retry context fail closed; no replay advance found. |
| WP2 | Immutable request/vehicle/route state, atomic reducer/ACK, frozen prefix, independent physical validator, B1 insertion | PASS. No adapter can overwrite core-owned load/route/rider state; accepted/onboard conservation and no-reassignment remain enforced. |
| WP3 | Promise projection, three-way delta, 10D ledger/budget, locks, incident/breach, certificate, checkpoint | PASS. Absolute deltas are not falsely additive; budgets are monotone/no-refund; certificate and checkpoint bind exact state and policy. |
| WP4 | B1–B5/C1/C2 generation, bounded loss, portfolio, lexicographic CP-SAT, fallback, execution evidence | PASS within declared bounds. Exact-small/differential evidence and deterministic work profile hold; no claim of global optimality outside audited solver status. |
| WP5 | Independent BeGo adapter boundary and recorded PostgreSQL/process/outbox evidence | PASS for the recorded mechanical integration scope. Domain/Application remain free of BeGo/EF/ASP.NET; no BeGo tree was copied. This review does not turn Layer 1 into effectiveness evidence. |
| WP6 | Public-data plan, child process/resource control, normalizer, metrics, independent oracle, artifact store/bundle verifier | PASS. Outcome fields are recomputed rather than trusted; process identity, canonical artifacts and mutation failures remain fail closed. |
| WP7 | Exact FleetPy 1.0.2 pin, edge-progress mapping, callback ordering, locked plans, Runner client, actual closed loop | PASS for Layer 2 mechanical integration. Python contains mapping/lifecycle only, not RideBound solver, budget or lock logic; current suite is 95/95. |
| WP8 | Unit/pair orientation, pilot/frontier, endpoint, finite panel, amendments, preregistration/freeze | PASS as experimental design for a finite panel. Solver seed is not a replicate; service equality at −1 pp fails; robustness cannot rescue primary. |
| WP9 | H6 freeze, 100 raw bundles, exact panel analyzer, burden decomposition, reproducibility, post-outcome breach evidence | COMPLETE WITH NEGATIVE RESULT. Both capacity strata fail service decisively; burden reduction is mostly definitional/refusal-mediated and is not reported as a standalone win. |
| WP10 | Exact RidePy source/image, native fleet lifecycle, same Runner, canonical reconciliation, frozen subset, terminal inventory analyzer | COMPLETE WITH NEGATIVE CAPABILITY RESULT. Canonical passes; concurrent mid-edge state cannot be represented by `nodeOnly`, so subset fails closed and Layer 3 is not established. |

## Cross-WP invariants

1. Domain and Application contain no EF Core, ASP.NET, provider, OR-Tools, FleetPy or
   RidePy dependency.
2. All simulator adapters call the same versioned Runner and contain no alternate
   RideBound decision implementation.
3. Candidate truncation remains explicit loss, never silently recast as infeasibility.
4. Every published decision is revalidated against physical and effective-policy
   constraints before commit; ACK/checkpoint is the durable boundary.
5. Experiment orientation, denominators, inputs, code/artifact identity and terminal
   inventory are bound before an estimand is computed.
6. Negative results and failure transcripts remain in the evidence set; neither a
   failed job nor an unfavorable valid output is silently discarded.
