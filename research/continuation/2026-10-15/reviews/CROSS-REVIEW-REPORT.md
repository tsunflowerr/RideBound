# Cross-review of Stage-1b reporting, 2026-10-15

Verdict at the reviewed bytes: **one source-consistency correction remains before a final acceptance**. R46 current-state authentication, cache bypass, undefined-denominator handling, accounting separation and median convention pass the targeted checks below. This is a Codex same-session cross-review by the extractor subagent; it is not an independent Claude/other-model review or authority to freeze/run main.

Reviewed `C:/RideBoundContinuation/2026-10-15/analysis-r46/report_stage1b.py`, SHA-256 `7bd3295cd0633f046367d8f5e365cc8f0907d10afb3df8a935ff1104997786ab`; shared extractor SHA-256 `8aa95cbf009271e91bb7c5f337dd0f93af28f332b8ab5cb48a98a474cebb550e`.

## Required correction

`metadata_problems` at lines 132–145 enforces the exact main/dry-run flags and document-level role mappings, but `load` and `analyse` do not compare retained `runs.json` records' `source` fields to `doc.roleSources[role]`. An archived run record can therefore retain `source = SF-U` under a claimed `CF-U -> CF-U` main mapping without a consistency refusal. The report header and validity rows display the document mapping, not the retained source field. R46 finding 4 requires the true role sources as well as strict mode flags. Compare each run's source with the corresponding role mapping, including allowed dry-run stand-ins, and regression-test mismatched/missing sources before analysis/output. This is an internal metadata consistency check, not a request for full protocol revalidation or a reopening of the accepted coordinated-receipt-rewrite limitation.

The report author agreed this correction is within R46 scope and awaits the coordinator's narrow authorization while script ownership is read only for cross-review. No report script was changed by this reviewer.

## Checks reproduced

Command:

```powershell
& 'E:\RideBoundData\wp7\envs\fleetpy-1.0.2\python.exe' -B 'C:\RideBoundContinuation\2026-10-15\reviews\test_report_r46_regressions.py'
```

**16/16 tests passed** on the above script hashes. Evidence: `C:/RideBoundContinuation/2026-10-15/reviews/cross-review-report-regressions.log`, SHA-256 `0e57c98655a4bb30f42b839f50c30973919c4e4b3ff0c5e2d14682aecbed9ae0`. The regression file is authored by the report subagent; this reviewer independently reran it and inspected the control flow.

- Wrong dry-run flag, unknown mode, numeric/string Boolean substitutes and forbidden document role sources refuse. Metadata is checked before reading the rider TSV, so `mode=dryrun,dryRun=false` cannot silently remove warnings.
- `binding_problems` verifies the unique bound extractor digest and the exact retained source bytes, then compiles those bytes directly. Its shared current-state helper accepts a complete synthetic fixture and refuses current stop/failure files, bogus prerequisite contents, changed pass/inspection receipts and any changed bound evidence file. A timestamp-valid adjacent extractor cache cannot execute its marker.
- A stop written during `analyse` is observed by the final production report check. `main` returns 2 without creating the report file.
- Empty CF-C30 served counts make H3 NOT EVALUABLE with `None` verdict, preserve `0/0` counts and leave the H6 service-retention guard failed. Empty accepted cohorts and NaN/positive-infinity/negative-infinity median differences never print the promise guard as met.
- Ledger arithmetic mismatches stop `analyse` at accounting validity before baseline/CF outcome claims; the separate pure prediction screen does not treat them as recovery explanations or change H1's zero-overshoot threshold. Recovery decision and breach counts retain their distinct units.
- The accepted initial horizon median remains nearest-rank over `p0 - t0`; a four-value fixture returns the lower middle. Main completed-without-alight anomalies refuse, while the dry-run plumbing diagnostic remains visible, implementing the proposed H7 cohort convention without changing its threshold.

An AST comparison to the original report confirms `q`, `nd`, `interaction`, `outcome` and `add` are unchanged. The pooled/cell/window/day interaction identity, baseline constants, five-day/34-cell handling, forecast rules and thresholds were inspected against the sealed plan and R46; no arithmetic changes were identified. Raw archived numerical reproduction and the full self-test remain the coordinator's separate evidence; these synthetic checks do not substitute for them.

## Retained limitations

The shared state helper and standard library run under the trusted frozen interpreter. Current receipt state is authenticated against the extraction snapshot; intentionally replacing every local receipt remains the accepted limitation. The report does not execute transcript readers or simulation code. File output is exclusive but is not a transaction with external receipt writers; the unavoidable interval after the last check is not claimed to be atomically locked. No new simulation jobs, seals, original input edits, model calls or commits were performed during this review.
