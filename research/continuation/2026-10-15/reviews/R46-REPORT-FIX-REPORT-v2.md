# R46 report correction v2, 2026-10-15

This supplements `R46-REPORT-FIX-REPORT.md` and records the final narrow source-consistency correction authorized by
the coordinator. The initial report source is preserved byte for byte in
`reviews/report_stage1b-before-source-consistency.py`; the original regression/log/report artifacts are unchanged.
No E: source, run, frozen file, live receipt, job, commit, publish, or seal was changed or launched.

## Final report behavior

The prior R46-3/4/5 and accounting/H7 fixes remain: authenticated exact-byte shared extractor checker, full current
receipt/inventory/stop rechecks before analysis and before output; strict main/dry-run mode and role mappings;
undefined H3 and median guards explicitly NOT EVALUABLE; ledger mismatches fail separate accounting validity;
completed-without-alight main evidence refuses; accepted first-drop-promise p0−t0 nearest-rank median is unchanged.

Additionally, `require_run_sources()` now requires every run to be an object with a strict allowed string `role`
and a strict string `source` exactly equal to `doc.roleSources[run.role]`. Both `load()` (before the TSV is read) and
`analyse()` (before metrics) enforce this. Main records claiming `source="SF-U"` under `CF-U`, and dry-run records
claiming a real `CF-U` source under an archived stand-in, refuse. Missing/nonstring role or source also refuses.
This checks internal metadata consistency; it does not expand analysis into a new artifact/protocol validator.

## Independent regression result

```powershell
& 'E:/RideBoundData/wp7/envs/fleetpy-1.0.2/python.exe' -B `
  'C:/RideBoundContinuation/2026-10-15/reviews/test_report_r46_regressions_v2.py'
```

**18/18 unittest cases passed** on the current report and final extractor below. The two added cases cover
conflicting main and dry-run sources through both load/analyse and strict missing/nonstring recorded fields.
The 16 prior failure-scenario cases still pass with source fields added to their synthetic panel fixtures.
The bound-checker cache substitution scenario still demonstrates that an executable adjacent timestamp `.pyc`
cannot replace the authenticated source checker. The final report stop injection still refuses without output.

| Artifact | SHA-256 |
|---|---|
| `analysis-r46/report_stage1b.py` | `ecbec711715a75f70a4dc5632b7978821e7c9b9e9767edfa84c5f07d00bdfc12` |
| Preserved pre-consistency report | `7bd3295cd0633f046367d8f5e365cc8f0907d10afb3df8a935ff1104997786ab` |
| `reviews/test_report_r46_regressions_v2.py` | `83ac93cc6b287d951a5de9d4a05e5e34e118092bd6367c53083d4dfee56d7c8e` |
| `reviews/report-r46-regressions-v2-run1.log` | `5948bff5f24f42859e617c17757423b9f2c14bc1768f8b172ca0eb66b23505fd` |
| Shared extractor tested | `6ff7f303f51a36d30afd22ea2c40c66290e8b9c1ee5a361461e2dca57083429c` |

The coordinator owns the combined historical self-test, its remaining expectation updates, and final candidate
amendment/inventory evidence. These identities are candidate validation records, not an analysis freeze or launch.
