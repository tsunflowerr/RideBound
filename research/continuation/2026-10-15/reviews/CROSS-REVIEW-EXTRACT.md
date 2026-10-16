# Read-only cross-review of R46 extractor candidate, 2026-10-15

Reviewed candidate SHA-256: `8aa95cbf009271e91bb7c5f337dd0f93af28f332b8ab5cb48a98a474cebb550e`.
Basis: sealed Stage-1b plan §§3, 7, 12, 14; R46 findings 1–3; original imported reader source closure;
candidate extractor source. No source, archived run, frozen file, or live receipt was modified by this review.

**Verdict: one remaining R46-3 correction before a ready-to-freeze finding.** The authenticated source/data closure
changes address R46-1/2 as designed. The current-state API substantially addresses R46-3, but its last invocation
precedes the final CF log/anchor scan, leaving a demonstrated stop-handling gap before output.

## Finding

**P2 — A durable stop appearing during the final log/anchor scan is bypassed by output.**

In `extract()`, all output bytes are prepared, `pin_problems()` is called, then `require_main_state(study)` runs.
The function subsequently scans main output layout and all 68 CF job logs/admission anchors. It checks those scan
problems, then opens the TSV/JSON. It does not recheck the current study state or durable latches after the later
scan. A stop or prerequisite/inventory drift arriving in that interval therefore allows output despite an invalid
current study state.

Controlled reproduction ran the actual candidate `extract()` control flow with in-memory substitutes for worker
results, cell/run plumbing and pin work; it launched **zero** jobs and read **zero** archived outcomes. The shared
real `require_main_state` authenticated a synthetic local complete harness at both calls. The final CF-log callback
then created `INPUT-DRIFT-STOP.txt` in that fixture. Observed result:

```text
code=0
stateChecks=2
stopExists=True
ridersWritten=True
runsWritten=True
```

The fixture is under `reviews/report-r46-vvw43fbu/final-cf-scan-proof`, preserved unchanged. Evidence:
`cross-extract-final-cf-scan-proof-run2.log`, SHA-256
`90fe82ce3430daa7229ca5c97e3c005c8084dc86554d2acd9b6f56c52dcff8d9`.
The earlier proof log had no injected stop because the mock's missing folders skipped initial log checks; the corrected
callback targeted the final scan by the held-state check counter. No claim is made that these empty synthetic rows
are a valid panel; the demonstration isolates the production output-admission ordering.

**Required change:** invoke `require_main_state(study)` after all final pin, layout, log/anchor and accumulated-error
checks, immediately before opening the first output. Keep the start snapshot and final comparison. Add a regression
that introduces the local stop during the final CF-log scan and asserts refusal/no TSV/no JSON.

## Closure checks that are satisfactory

- **Verified source execution:** `readers()` captures all ten pinned source files through `verified_bytes()` and
  retains the authenticated bytes. A first-priority meta-path finder serves those bytes for direct and transitive
  named imports; the loader calls `compile(bytes)` and never delegates to a source/bytecode loader. Existing pinned
  modules in `sys.modules` are refused. Every pinned module must be the object recorded by this loader at its pinned
  path, and newly imported non-interpreter modules outside the pinned set refuse.
- **Observed transitive closure:** the reused readers' module-scope project imports are the pinned `accounting`,
  `promise_stretch`, `preflight_world_v1_3`, `preflight_world`, `preflight_world_v1_1`, `preflight_world_v1_2`, and
  `world_v1`. Dynamic wrapper imports are in their unused `main()` functions, not the display/profile path. There is
  no new local helper requiring an additional analysis-inventory entry.
- **Immutable forecast consumption:** factor bytes are verified and retained in a mapping proxy before readers
  operate. Only v1.3's `preflight_world` reference is wrapped with a read-only factor-folder facade. Its existing
  `absolute_profile()` uses `path.exists()` then `path.open(encoding="utf-8")`; that exact parser receives `BytesIO`
  / `TextIOWrapper` over retained bytes, with the same CSV parsing, bins and forecast averaging. Disk replacements
  after initial capture cannot change the consumed forecast input. Unknown factor paths and writes refuse. The
  audit records the logical factor paths; this records consumption, not another disk read.
- **Final persistent pin checks:** `pin_problems()` still compares all reader source/data identities before writing.
  Persistent drift refuses; a transient disk replacement cannot alter already retained executed source/factor bytes.
  `load_cells()` hashes and parses the same captured split bytes.
- **Shared study checker:** it authenticates the sealed runner manifest, receipt terminators and runner DLL/tree
  links, accepted smoke inspection and exact smoke digest, manifest/binding inventory equality, required entries,
  every bound file's current bytes, and amendment review metadata. It rescans latches during its own scan and compares
  the complete held snapshot. Neither it nor reporting writes/clears stop receipts.
- **Definitions remain stable:** the reader algorithms are unchanged; served remains completed intersect alighted,
  first p0/t0 and the causal-no15 forecast remain fixed. H7 anomaly refusal is performed in the candidate report,
  and ledger mismatches remain a recorded extractor diagnostic that the report separately fails as accounting.

No request is made to reopen the accepted interpreter/OS trust boundary, stat limitations, behavioural-only
middle-decision validation, historical runtime-equality limitation, or deliberate coordinated replacement of all
local receipts. Five days and previewed controls continue to limit research claims.

This initial verdict must be amended by a read-only verification of the final ordering and failure regression once
the owner of the extractor applies the correction; it is not an approval to freeze or launch main.
