# Final source, logic and evidence review — WP1 through WP10

> Review date: 2026-08-23  
> Source state: post-WP9 H6 outcome, post-WP10 terminal attempt, post-ADR-052 optimization  
> Verdict scope: repository correctness/integrity evidence, not a formal proof

## Verdict

No unresolved correctness or evidence-integrity blocker was found in the reviewed
RideBound repository. That verdict is deliberately narrower than “all code is
correct”: it combines whole-tree machine inspection, manual review of every
outcome-bearing boundary, independent/mutation/differential tests, actual FleetPy
and RidePy execution, and explicit residual risks.

The scientific outcomes remain negative:

- WP9 service gate failed at both 8 vehicles (`−7.1296 pp`) and 4 vehicles
  (`−4.9074 pp`). The burden gate cannot rescue it.
- WP10 canonical execution passed, but the representative subset failed closed on
  `RBWP10_NODEONLY_CONCURRENT_MIDEDGE_UNSUPPORTED`; Layer 3 was not established.

The review did not change either outcome, margin, arm, denominator or freeze.

## What was inspected

- 1,226 reviewable files in the pre-report inventory were opened in a full-tree
  machine pass (302,565,761 bytes; 145,082 text lines); the new benchmark tool and
  all later patches were then read directly.
- Every production/test Python file parsed successfully; every JSON document parsed;
  Markdown internal links and fences were checked; UTF-8 decoding passed.
- Domain/Application dependency bans, project references, package placement,
  nondeterminism, floating point, broad catches, TODO/stub patterns and adapter-side
  decision reimplementation were scanned across the whole tree.
- High-risk paths were read manually: canonical protocol/hash/retry, lifecycle and
  physical validation, promise/delta/ledger/budget/locks/certificates, candidate
  generation/solver/fallback, benchmark/oracle/bundle verification, FleetPy mapping
  and clock, WP9 analyzers/freeze/reproducibility, RidePy native reconciliation and
  WP10 freeze/analyzer/failure retention.

## Findings that changed artifacts

1. **WP10 analyzer completeness defect — fixed.** It previously accepted an
   incomplete set of otherwise valid pairs and did not bind master seed or the full
   Runner receipt to the freeze. The analyzer now requires the exact terminal job/
   arm/failure inventory, freeze hash and 128-file Runner/config/source/adapter
   receipt. Seven mutation classes pass; strengthened analysis v2 has SHA-256
   `be3e90771ae0216e891a8284b5457b1f635db214aa52681ce8d468b4007dcca3`.
2. **Hot-path redundant identity work — fixed.** ADR-052 removes an allocation
   already implied by immutable `VehicleState` and verifies prefetched keys without
   constructing another key. Allocation falls deterministically while search/output
   counters remain exact; see the
   [optimization evidence](../../benchmarking/post-wp10-exact-reuse-optimization-2026-08-23.md).
3. **Formatting drift — fixed.** The format gate found mixed line endings and import
   ordering in files changed during WP9 closure. The mechanical rewrite was applied;
   the verify-only format gate and `git diff --check` now pass.
4. **WP10 image recoverability — strengthened.** The exact executed image was saved
   outside the repo as `E:\RideBoundData\wp10\ridepy-wp10-2.10.1-image.tar`
   (695,427,072 bytes; SHA-256
   `4783c541c256d1551677684eb5182cc43a8845d6bb3c5dc34778aadc9fc9a872`) and loaded
   back to image ID `sha256:5468b9cb…e573`.

## Current verification snapshot

| Gate | Result |
|---|---|
| Required `dotnet test RideBound.slnx` | **855/855**, zero fail/skip |
| Release solution build `/warnaserror` | zero warnings/errors |
| `dotnet format --verify-no-changes` | PASS |
| NuGet direct + transitive vulnerability audit | zero known vulnerable packages |
| FleetPy pinned Python suite | **95/95**, zero skip |
| RidePy pinned-container suite | **23/23** |
| BeGo read-only current baseline | backend **149 pass + 5 explicit opt-in skip**; frontend **9/9** |
| WP10 canonical verifier | PASS; 5/5 mutation classes |
| WP10 strengthened subset analyzer | PASS; 7/7 mutation classes |
| Full-tree syntax/JSON/Markdown/UTF-8/static scans | PASS; intentional negative fixture retained |
| Exact Docker image archive/load | PASS; restored the same image ID |
| Optimization benchmark | 3 baseline + 3 optimized processes; semantic counters exact |

One non-final attempt intentionally remains visible: running Release build/format in
parallel with the suite caused the medium public-drain test to trip its process-tree
CPU ceiling (`854 pass / 1 resource.cpu-time-exceeded`). Nothing was reclassified or
the ceiling loosened. The exact required suite was rerun alone and passed **855/855**;
that isolated run is the final baseline above.

Detailed WP-by-WP conclusions are in
[`01-wp-by-wp-verdict.md`](01-wp-by-wp-verdict.md). Residual risks and rejected
claims are in [`02-residual-risks.md`](02-residual-risks.md).

The rendered final report is
[`RideBound-WP1-WP10-final-review-2026-08-23.pdf`](../../../output/pdf/RideBound-WP1-WP10-final-review-2026-08-23.pdf)
(12 A4 pages, 100,056 bytes, SHA-256
`066168872d7ead11362b3f0f7b5832e8e1147bb655f281cc3ec08d939c29b20b`).
