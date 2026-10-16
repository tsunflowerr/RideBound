# Extractor R46 fixes, development copy, 2026-10-15

The development extractor addresses R46 findings 1–3. It has not been installed in the original harness, sealed, or used for a Stage-1b simulation job. Only the C-side development extractor and new regression/evidence files were changed. Original scripts, sealed files, archived runs, factors and Python environment were read only. This report records work by the Codex continuation agent; it does not claim a new Claude session or independent other-model review.

Original extractor: `E:/Code/Report_INT3508/ridebound-scratchpad/tang3/stage1b/harness/extract_stage1b.py`, SHA-256 `87bf04bab94317eeaa57f6f8ce740071b5f879dffcfd7abdde03c02db5b085f9`.

Development extractor: `C:/RideBoundContinuation/2026-10-15/analysis-r46/extract_stage1b.py`, SHA-256 `8aa95cbf009271e91bb7c5f337dd0f93af28f332b8ab5cb48a98a474cebb550e`.

## Implementation

1. Every one of the ten pinned modules is read into a source-byte snapshot and its exact bytes are SHA-verified. A scoped import finder routes direct and transitive pinned imports to a loader which compiles those retained bytes. It never asks Python's ordinary source loader for executable cached bytecode. Module identity/path and import-closure checks remain. An already-loaded pinned module is refused unless it came from the current initialized reader set.

2. Split/factor bytes are SHA-verified at reader initialization. The unchanged v1.3 CSV/profile parser receives the three forecast source CSVs from immutable bytes via a read-only path facade. No profile arithmetic, source-day rule, bin selection, hold rule, display rule, endpoint or numerical threshold is changed. Parent and workers capture their own verified snapshots. A final external-pin check rejects persistent disk drift; transient factor reads cannot change the retained input. The path audit continues to record facade reads but is not presented as content authentication by itself.

3. `study_state(harness=HERE)` validates the Gate-0/smoke final verdict lines, DLL/tree identity links to the sealed v3 manifest, and smoke inspection's actual smoke digest, accepted status and nonempty inspector. The runner manifest itself is pinned to the sealed-plan SHA; its body/tree hash and file count are checked. Binding/freeze parsing rejects malformed or duplicate entries, checks equal inventories and verifies every retained file, including evidence beyond the two scripts. Required inventory entries include both scripts, runner manifest and amendment; the bound amendment's verified bytes supply the same review/coverage conditions used by the driver.

4. New shared API `require_main_state(expected=None, harness=HERE) -> dict` refuses defective current evidence and, when `expected` is supplied, any difference from that entire held snapshot. Keys are `latches`, the three prerequisite receipt filenames with their digests, `runnerIdentity`, `bindingSha256`, `analysisManifestSha256`, and `bindingInventory`. Reporting must authenticate and compile the exact bound extractor source bytes before calling this helper, and call it at start and immediately before writing a report. The helper is read only and never clears or rewrites a stop receipt.

5. Extraction prepares both output byte strings before rechecking current study state, full retained inventory, external pins, CF output inventory and CF log/admission anchors. Invalid final evidence refuses before either TSV or JSON is created. `HERE` still refers to the containing harness; no runtime path override or extra local runtime dependency was added.

## Regression evidence

Command, run with the prescribed frozen interpreter and bytecode writes disabled:

```powershell
& 'E:\RideBoundData\wp7\envs\fleetpy-1.0.2\python.exe' -B 'C:\RideBoundContinuation\2026-10-15\reviews\test_extract_r46_regressions.py'
```

Result: **17/17 tests passed**, including parameterized verdict/build/inspection and latch cases. Saved evidence: `C:/RideBoundContinuation/2026-10-15/reviews/extract-r46-regressions-2026-10-15.log`. New regression file SHA-256: `e60929d764a182f29342cc95de37024fe014cbd347b007a16210a9bf47d7c4f8`.

- Ten physical timestamp-valid poisoned caches were created only beside C copies of the helpers. Ordinary `SourceFileLoader.get_code` accepted all ten. New readers executed all ten source snapshots, with no poison markers.
- The reference 14-Nov/w08 forecast remained `D0 = 1847580.4741262272 ms`. A transient in-memory CSV substitution would make the original disk-reading path yield `10969246.551866643 ms`; the frozen-byte path retained the reference value and made zero disk factor reads during that substituted profile call. No original factor changed.
- The extraction control flow was exercised with synthetic worker results and real shared state checks. A new durable stop, a replacement otherwise-valid Gate-0 receipt, changed nonreader bound self-test evidence, and persistent pinned-data drift each left neither output file.
- Additional checks reject arbitrary prerequisite bytes, wrong DLL/tree links, missing pass terminators, invalid inspection fields, preloaded unverified helper modules, malformed/duplicate binding/freeze records, and a stop appearing during the retained-inventory scan.
- Numerical function definitions and raw archived numerical reproduction remain the responsibility of the updated full self-test. These targeted tests do not claim to recompute all 102 archived runs, prove current main results, or rerun .NET production tests.

Test artifacts are retained under C review directories; no cleanup deletes inputs or artifacts. The supplement is development evidence outside the installed runtime and does not add a runtime import dependency. The final reviewed analysis inventory should reference the actual final self-test and evidence files chosen by the coordinator.

## Residual trust and boundaries

The prescribed interpreter, its standard library/native components, OS, trusted pinned helper source and process memory remain trusted. The receipt build check authenticates the sealed manifest identity; it does not rehash the unused current Runner directory, because analysis executes no Runner code. The driver retains its own before/after-phase live-tree verification.

Two ordinary output files are written sequentially with exclusive creation. A stop arriving after the final checks or an I/O/process failure during the writes can leave a partial pair; the JSON/TSV integrity checks prevent that pair from becoming an accepted report. This change does not introduce a transaction or filesystem lock across independent writers.

Receipt changes are compared to the extraction snapshot and refused without writing a new drift latch. Existing durable stops are always honored. A deliberately coordinated replacement of all scripts, manifest, binding, receipts, logs and admission anchors remains the already-disclosed file-based trust limitation; it is not reopened here.

No sealed-plan definitions, median/H7 choices, research claims, production repository boundaries or next-stage authorization were changed by this extractor subtask. Those conventions remain for the coordinator's amendment/review and report work.
