# R46 report correction, 2026-10-15

Status: candidate report fixes verified on synthetic failure scenarios; no Stage-1b main jobs, commit, publish, seal,
or frozen-file changes were performed. The full historical self-test and final inventory review remain coordinator work.

The sealed `STAGE1B-PLAN.md`, original report/self-test, and `R46-ANALYSIS-REVIEW-CODEX.md` were read without alteration.
All changes and fresh regression fixtures are under `C:/RideBoundContinuation/2026-10-15`.

## Findings addressed

1. **R46-3, current state and complete retained inventory.** Main reporting now verifies the recorded binding bytes,
   the current report, and the bound extractor source identity, then compiles the exact verified extractor bytes in a
   fresh module object. It does not import an extractor by name, use a cached module, or accept adjacent `.pyc` code.
   The shared `require_main_state(expected=doc["studyState"], harness=...)` implementation authenticates current
   Gate-0/smoke receipt content and runner identity, accepted inspection linkage, latches, manifest/binding equality,
   the amendment review, and **every** retained inventory file. It compares the complete state to the extraction
   snapshot. This is called both before analysis and immediately before opening report output, including reports
   stopped by baseline/validity checks. Any refusal returns exit 2 with no report written. The report adds no new
   project helper beyond the extractor already present in the logical analysis inventory.

2. **R46-4, mode/flag/source identity.** `load()` and `analyse()` both enforce strict types and exactly
   `(mode="main", dryRun=false)` or `(mode="dryrun", dryRun=true)`. Main role sources are exactly their corresponding
   four CF/SF roles. Dry-run SF roles retain `SF-U`/`SF-C30`; each CF stand-in is exactly one of `SF-U`, `SF-C30`,
   `SF-C120`. Missing, foreign, extra or inconsistent metadata refuses before analysis; a direct in-memory call
   cannot strip dry-run warnings by flipping the flag.

3. **R46-5, undefined reliability and promise guards.** H3 requires a positive served denominator and preserves the
   original exact integer threshold arithmetic when positive. Zero served returns `H3=None` and explicitly prints
   `NOT EVALUABLE`, while retaining counts and H6's failed service guard. An empty accepted cohort or NaN/infinite
   median difference returns a promise guard with `longer=None`, prints `NOT EVALUABLE`, and never says `guard met`.

4. **R46 P3, accounting consistency.** Any `ledgerBudgetMismatches` field in any role fails the report's input
   accounting assessment: `accounting_validity=False`, `stopped="accounting-validity"`, exit 1, before CF tables or
   hypothesis verdicts. Mismatched fields are listed as accounting defects. They cannot be waived by a written
   recovery explanation. The pure `predictions()` diagnostic retains H1's original cap/forced-no-worse explanation
   screen and excludes ledger defects from its explanation lists; if supplied a ledger defect it prints a separate
   `ACCOUNTING VALIDITY FAIL` and returns `accountingValidity=False`. A clean cap screen in that isolated diagnostic
   does not rescue overall validity. Non-normal decisions and breach entries remain separately counted units.

## Definition decisions for the hashed amendment

- Keep the median cohort as accepted riders with a first drop promise. Use the **initial quote horizon** `p0-t0`,
  with nearest-rank median (`ceil(0.5*n)`, lower middle for an even sample); retain the served-cohort median as a
  diagnostic. The arithmetic has not changed. An undefined comparison is not a passed 30-second claim guard.
- Preserve the H7 screen `max|c| <= 120000 ms < sum|c|` and threshold of 10. Refuse main evidence with
  completed-without-alight anomalies, so the plan's completed cohort equals the endpoint's completed-and-alighted
  served cohort on admitted main evidence. Dry-run evidence retains anomaly counts and served-screen diagnostics.
- Ledger accounting is an additional implementation consistency condition, distinct from sealed H1's unchanged cap
  and explanation thresholds; it must not be described as another recovery kind.
- Describe current-state closure as the **shared bound extractor source checker executed from authenticated bytes**.
  The standard library/interpreter/OS trust boundary and disclosed deliberate coordinated receipt rewrite limitation
  remain. Local development regressions and their fixtures are evidence, not report runtime dependencies.

## Independent validation

Command:

```powershell
& 'E:/RideBoundData/wp7/envs/fleetpy-1.0.2/python.exe' -B `
  'C:/RideBoundContinuation/2026-10-15/reviews/test_report_r46_regressions.py'
```

Result: **16 unittest cases passed**, including strict/inconsistent metadata; load-time metadata admission;
empty CF-C30 reliability/cohort guards; NaN, positive/negative infinity; accounting defects separate from unchanged
H1 cap/recovery thresholds; H7 main anomaly refusal with dry diagnostics; unchanged even-sample horizon median;
complete real-checker fixture acceptance; stop receipts after extraction; prerequisite and extra retained-file drift;
bogus Gate-0 receipt content; executable timestamp-cache substitution; and a stop appearing during analysis that
prevents output entirely. The fixture files are synthetic, clearly named, and preserved under the review directory.
No archived run folders were read or written by this independent suite.

The first regression run had one test-selector failure: it selected the overlap line with the same role-pair prefix
instead of the later median-horizon line. The test selector was corrected; the implementation was unchanged. Run 2
passed 15 tests; the additional explicit H1/accounting separation case made run 3 pass 16 tests.

| Artifact | SHA-256 |
|---|---|
| `analysis-r46/report_stage1b.py` | `7bd3295cd0633f046367d8f5e365cc8f0907d10afb3df8a935ff1104997786ab` |
| `reviews/test_report_r46_regressions.py` | `195c7830de2d1511681dcb6db69743e959aee2e3c07b8aaa1db69a6342473501` |
| `reviews/report-r46-regressions-run3.log` | `eeddd7cefdaa9ac8ae686d197cc8a0805e538027cc5d13879cb218478c137c18` |
| Shared extractor tested at run 3 | `7ff4eefef88bc053dd8daa21c099ce94dba82d9a4b638773e51322da78e63e59` |

These are candidate identities, not a new analysis freeze. Rerun the failure scenarios if their inputs change, then
record the final combined evidence and named inventory review before any owner-approved freeze/main admission.
