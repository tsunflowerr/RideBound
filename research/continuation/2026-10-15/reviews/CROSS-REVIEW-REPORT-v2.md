# Reporting cross-review closure, 2026-10-15

Verdict: **the scoped R46 reporting corrections are accepted in the C-side development copy**. The source-consistency finding in `CROSS-REVIEW-REPORT.md` is closed. This acceptance covers code and targeted regression behavior; it is not an analysis seal, authority to start jobs, certification of hypothetical CF results, or an independent other-model review.

Final report SHA-256: `ecbec711715a75f70a4dc5632b7978821e7c9b9e9767edfa84c5f07d00bdfc12`.

Shared extractor SHA-256: `6ff7f303f51a36d30afd22ea2c40c66290e8b9c1ee5a361461e2dca57083429c`.

## Finding closed

New `require_run_sources(doc, records)` requires each retained record to contain a string role from the four roles and a string `source` equal to `doc.roleSources[role]`. Both disk `load` and in-memory `analyse` invoke it after validating document metadata and before producing outcome claims. Conflicting archived stand-ins under main mappings, inconsistent dry-run mappings, absent/nonstring sources and malformed roles are refused. This prevents document-level labels from silently contradicting the retained run-source fields.

Two new source-specific tests were independently rerun after inspecting the final implementation:

```powershell
& 'E:\RideBoundData\wp7\envs\fleetpy-1.0.2\python.exe' -B 'C:\RideBoundContinuation\2026-10-15\reviews\test_report_r46_regressions_v2.py' 'ReportRegressions.test_main_and_dryrun_reject_conflicting_recorded_sources' 'ReportRegressions.test_missing_and_nonstring_run_source_or_role_are_refused'
```

**2/2 passed** on the final hashes. Evidence: `reviews/cross-review-report-source-v2.log`, SHA-256 `52cfb2a01ce6c5275c14bf904c53b0303a04d2ae9cbc58afb95e97d201b2ae62`. The report author separately records 18/18 passing regressions in the v2 suite; this reviewer independently reran the original 16 scenarios on the earlier report and only the two new source scenarios after this narrow correction. The earlier review/log remain preserved.

## Retained assessment

The previous satisfactory assessments remain: strict main/dry-run mode and flag types, authenticated source execution of the shared checker with no .pyc dependency, current prerequisite/full-inventory/stop checks at report start and before output, NOT EVALUABLE handling for zero H3 denominators/empty accepted cohorts/nonfinite median differences, and accounting validity separated from recovery explanations. The correction adds source consistency only; original endpoint arithmetic, thresholds and forecast definitions remain intact.

The extractor's final CF-scan timing gap was also corrected on the final shared-helper hash. Its final state check now follows all pin/log/anchor checks immediately before output creation. The extractor supplement passes 19/19, including a stop introduced during the last CF log scan; the report author separately reproduced refusal with neither output file in `CROSS-REVIEW-EXTRACT-v2.md`.

Full raw archived numerical reproduction, final self-test evidence, amendment/traceability updates and installation/freeze decisions remain the coordinator's work. The trusted interpreter/OS and accepted deliberate-coordinated-receipt-rewrite limitation are unchanged. No original inputs, jobs, seals, commits or script ownership boundaries were changed by this read-only cross-review.
