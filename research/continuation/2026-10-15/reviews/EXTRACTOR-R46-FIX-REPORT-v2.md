# Extractor R46 fixes, final ordering correction, 2026-10-15

This v2 report supplements `EXTRACTOR-R46-FIX-REPORT.md`; the earlier report/log are preserved. The full implementation mechanisms described there remain: all ten pinned helpers execute exact verified source-byte snapshots, cached helper bytecode is bypassed, and the unchanged forecast parser consumes immutable verified factor bytes. `require_main_state(expected=None, harness=HERE)` authenticates current prerequisite contents/build links, smoke inspection and every retained analysis file against the extraction snapshot.

The same-session report cross-review found a concrete remaining timing gap: the extractor's full final state check preceded the CF log/anchor scan. A stop created in that later scan could therefore escape the state check. The coordinator explicitly authorized the narrow correction after reviewing that proof.

## Final changes

- The full final state check now follows external-pin checks, CF output-inventory checks, every CF log/anchor scan and their error/refusal checks. It runs immediately before the first exclusive output creation. A stop created during the final CF scan is now refused without either output file.
- Main-mode interpreter identity is checked before `require_main_state()`. A wrong interpreter retains its existing explicit frozen-interpreter refusal even if the current study evidence is invalid or missing.
- No numerical definitions, forecast parser/profile arithmetic, source/data pins, thresholds, `HERE` paths or helper API changed in this follow-up.

Prior extractor source preserved at `C:/RideBoundContinuation/2026-10-15/reviews/extract_stage1b-before-final-order.py`, SHA-256 `8aa95cbf009271e91bb7c5f337dd0f93af28f332b8ab5cb48a98a474cebb550e`.

Final development extractor: `C:/RideBoundContinuation/2026-10-15/analysis-r46/extract_stage1b.py`, SHA-256 **`6ff7f303f51a36d30afd22ea2c40c66290e8b9c1ee5a361461e2dca57083429c`**.

## Evidence

```powershell
& 'E:\RideBoundData\wp7\envs\fleetpy-1.0.2\python.exe' -B 'C:\RideBoundContinuation\2026-10-15\reviews\test_extract_r46_regressions.py'
```

**19/19 tests passed**. The new precise timing regression creates `INPUT-DRIFT-STOP.txt` during the last final CF log scan and requires the real extraction control flow to refuse, with neither TSV nor JSON created. Another regression proves the frozen-interpreter check precedes invalid-study authentication. The earlier 17 failure scenarios remain covered.

| Artifact | SHA-256 |
|---|---|
| `reviews/test_extract_r46_regressions.py` | `f5c03ff62af408c165445c04f8eadc6fbeb3eeb65673460fda6ef4cea2bda164` |
| `reviews/extract-r46-regressions-2026-10-15-v2.log` | `37333098d1680629b5b483ccb139d782a36dfad8fe497867e69e472a78a10259` |

The ten C-side valid poisoned helper caches still cannot bypass source pins. The frozen forecast retains `D0=1847580.4741262272 ms` under the transient substitution that makes the old disk parser produce `10969246.551866643 ms`. No original factor or helper was changed.

## Scope and remaining limits

Only the authorized C-side extractor, its supplemental regression file, preserved source copy and new v2 report/log were written. No E-side edits, simulation jobs, seals, commits, environment edits or cleanup occurred. These are synthetic integrity regressions; the coordinator owns the full archived numerical self-test evidence and final installation/review decision.

Residual trust remains as disclosed in the first report: interpreter/OS/process memory and the sealed receipt identities are trusted; analysis does not execute or rehash the unused live Runner directory. Receipt changes are refused read only, without inventing a new durable latch. Deliberately coordinated replacement of all local receipts remains the accepted limitation.

Independent filesystem writers are not locked by analysis. A stop arriving after the final check or an I/O/process failure during sequential output writes can still leave a partial pair; reporting's integrity/current-state checks prevent that pair from silently becoming an accepted result. The demonstrated long CF-scan window is closed; no atomic multi-file transaction is claimed.
