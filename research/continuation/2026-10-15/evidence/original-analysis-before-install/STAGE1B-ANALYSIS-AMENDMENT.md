# Stage 1b analysis amendment (DRAFT): analysis code and its dependencies

Status: **DRAFT**, written 2026-10-14 by the analysis-script agent (Claude), before any Stage-1b job output existed
(`C:/RideBoundData/research/tier3-stage1b-v1` absent; Gate 0 not started). Plan: `STAGE1B-PLAN.md` (sealed copy of
draft v7; sections 5-8 and 12 are unchanged from draft v6). This file is the amendment record required by plan §12 and
`run_stage1b-v7.py:895-904`; it must be listed in `stage1b-analysis-freeze.sha256`, which the coordinator writes after
review, before any main job starts (plan §8, §12). This draft does not create that manifest.

**Before the freeze is written, the coordinator must replace the placeholder on the `inventory-reviewed-by:` line
(section F) with the reviewers' names.** The driver only checks that some text follows `inventory-reviewed-by: `
(`run_stage1b-v7.py:901-902`), so it would also accept the placeholder.

## A. Files to list in `stage1b-analysis-freeze.sha256` (bare names, harness folder)

| File | Role | Used by the analysis run |
|---|---|---|
| `extract_stage1b.py` | Step 1: per-rider and per-run extraction; holds every pin of section B | executed |
| `report_stage1b.py` | Step 2: tables, primary endpoint, guards, predictions (standard library only) | executed |
| `runner-tier3-v3.sha256.tsv` | Data: the extractor reads the `RideBound.Runner.dll` line (expected CF `binarySha256`) | read (also sealed, plan §13) |
| `STAGE1B-ANALYSIS-AMENDMENT.md` | This record (required in the manifest) | no |
| `test_analysis_stage1b.py` | Self-test: synthetic checks plus plumbing dry runs on archived data | no (evidence) |
| `analysis-selftest-2026-10-14.log` | Self-test log | no (evidence) |
| `ANALYSIS-SCRIPTS-REPORT.md` | Mapping from the plan's definitions to code. Not created by the agent, whose tool refused report files; the coordinator may save it from the agent's final message. Optional: list it only if it exists and will not be edited again | no (documentation) |

Do not list these by-products of the development work (they are not inputs of the analysis): `.sfpins.tmp` (output of
the one-off computation of the SF bundle pins), `analysis-dev-2026-10-14/` (development dry runs), and
`analysis-selftest-2026-10-14/` (the self-test work folder; its outputs are summarized in the log).

## B. Dependencies outside the harness folder: pinned inside `extract_stage1b.py`

The freeze manifest accepts only bare file names in the harness folder (`ANALYSIS_LINE`, `run_stage1b-v7.py:858`), so
no external file can be listed there. `extract_stage1b.py` instead holds the SHA-256 of every external input and
verifies it on every run (`pin_problems`, `readers`, `verify_bundle`). Freezing `extract_stage1b.py` therefore freezes
these inputs too.

| External input | Pinned as | SHA-256 (read 2026-10-14) | Also in the Stage-1b input freeze? |
|---|---|---|---|
| `tang3/evidence/audit_vep.py` | `PINNED_MODULES` | ffa86b31…807f | yes |
| `tang3/oracle/accounting.py` | `PINNED_MODULES` | 0eb831be…e55d | yes |
| `optimize/display_layer.py` | `PINNED_MODULES` | 0f3d8b64…eb87 | **no** (pin only) |
| `optimize/promise_stretch.py` | `PINNED_MODULES` | e35c38b7…91a5 | **no** (pin only) |
| `tang3/world/preflight_world_v1_3.py` | `PINNED_MODULES` | 0bcad103…2047 | **no** (pin only) |
| `tang3/world/preflight_world.py` | `PINNED_MODULES` | 9467611c…396d | yes |
| `tang3/world/preflight_world_v1_1.py` | `PINNED_MODULES` | 7df4a458…7725 | yes |
| `tang3/world/preflight_world_v1_2.py` | `PINNED_MODULES` | df28b6cb…a262 (= `WRAPPER_SHA256`) | yes |
| `tang3/world/world_v1.py` | `PINNED_MODULES` | 40830b34…c6da | yes |
| `optimize/codex-debate/claude-work/r13-final-judge/cumulative_decisive_check.py` | `PINNED_MODULES` | 24fb60e1…772e | **no** (pin only) |
| `tang3/t38/split-t38-v1.json` | `PINNED_DATA` | b72ac0f7…03f1 | yes |
| factor CSVs 2018-11-12 and 2018-11-13 (`E:/RideBoundData/wp6/.../Manhattan_2019_corrected/`) | `PINNED_DATA` | 89f64a74…7317, 44229076…2b0c | **no** (pin only; days 14-18 only are in the input freeze) |
| factor CSV 2018-11-14 | `PINNED_DATA` | 0f09e265…f85f | yes |
| 68 archived SF comparators `C:/RideBoundData/research/tier3-t38-v1/{U,C-30}-<cell>` (the `-rerun1` folder for C-30 d20181114-r3-w17) | `SF_BUNDLE_PINS`: SHA-256 of each `bundle-manifest.json`, which binds `summary.json`, `run-00.json` and `transcript-00.ndjson` by SHA-256 and length | 68 values; every listed transcript SHA-256 equals Codex's `R13-raw-verification.json` (68/68) | only the 4 Gate-0 references |
| the 68 CF main outputs `C:/RideBoundData/research/tier3-stage1b-v1/main/CF-{U,C-30}-<cell>` | not pinned (they do not exist yet). Instead: the bundle manifest is self-consistent, the identity matches (label, policy binding, v3 DLL, core, seed, policy id/version, inventory, receipts, 108 requests), the per-cell episode matches its SF pair, the job log shows `returncode=0` and the admission anchor names the analysis binding | — | — |
| interpreter `E:/RideBoundData/wp7/envs/fleetpy-1.0.2/python.exe` and its standard library | `JOB_PYTHON` (main mode refuses any other interpreter) | — | yes (`stage1b-external-python-env.sha256.tsv`) |

`test_analysis_stage1b.py` additionally imports the original Stage-1a helpers (`forecast_family_a.py`,
`budget_use_check.py` and their import graph) to prove that the four verbatim copies (`event_times`, `sf_effective`,
`day_of`, `q`) equal their originals. These imports happen only in the self-test, never in the analysis run.

Runtime evidence, also read by the analysis but not as code: `GATE0-BEHAVIOURAL-PASSED.txt`, `SMOKE-PASSED.txt`,
`SMOKE-INSPECTION.txt`, `stage1b-analysis-binding.txt`, `stage1b-analysis-freeze.sha256` (the manifest the binding
names; every file the binding lists must still have its bound SHA-256), `logs/main/<job>.log` and
`logs/main/<job>.admitted`. The extractor refuses if any `*-FAILED.txt` or `INPUT-DRIFT-STOP.txt` exists.

## C. Why I believe the list is complete (`inventory-coverage: complete`)

1. **Import closure, enforced.** `readers()` (extractor) verifies the pins, imports the three readers, and refuses if
   any pinned module was loaded from a path other than its pinned one, or if any newly loaded module is neither pinned
   nor inside the interpreter. Test A9 removes `world_v1` from the pins and confirms the refusal. `report_stage1b.py`
   imports only the standard library (`collections, hashlib, json, os, pathlib, re, statistics, sys`).
2. **Read closure, enforced.** While a run folder is read, an audit hook records every `open()`. The run refuses if
   any read falls outside the run folder, the pinned files and the interpreter, or if any file is opened for writing.
   Test A9 shows that the live hook flags both an unpinned read and a write. In all 136 role-runs of dry run B1 the hook
   recorded at least 5 opens per run folder, and the only other files read were the causal-no15 factor CSVs of the
   forecast-active cells: 14/11 read 12/11 and 13/11; 15/11 and 16/11 read 12/11, 13/11 and 14/11; 17/11 and 18/11 read
   none.
3. **Data closure.** The split, the factor CSVs and the 68 SF bundles are pinned. CF outputs are checked for identity
   and completeness, as listed above. The interpreter is the frozen job interpreter.
4. **No other code is executed.** The analysis is exactly two command lines (section E). The self-test is evidence and
   is not run during the analysis.

Remaining, disclosed gaps (not inventory items):
- the interpreter's standard library is trusted through the Stage-1b input freeze, not re-hashed by the analysis;
- operating-system components are not covered;
- CF outputs cannot be pinned in advance. Between the end of the main phase and the analysis they are protected only by
  their own bundle manifests and by the driver's post-phase checks. A coordinated rewrite of a run folder and its
  bundle manifest would not be detected (plan §14, "deliberate coordinated rewrite").

## D. What the scripts compute

The plan-to-code mapping (file:line) is in the agent's final message (to be saved as `ANALYSIS-SCRIPTS-REPORT.md` if
wanted) and in the docstrings of both scripts. In short: plan §5 (I_L, I_served, I_timely60 and their identity, pooled, per cell,
per window and per day; the full 2x2 of timely / late / not-served counts; served-set overlap; leave-one-day-out
I_L), §7 (every guard for all four roles, including the 30-s median-promise guard), and §6 (G0 and H1-H7 as written,
with H1 marked `PENDING` while forced or no-worse records still need a written explanation). Results are labelled
exploratory, as plan §3 and §1.1 require.

## E. How the coordinator runs the analysis (after the main phase and its post-phase check)

    E:/RideBoundData/wp7/envs/fleetpy-1.0.2/python.exe -B extract_stage1b.py main <prefix>
    E:/RideBoundData/wp7/envs/fleetpy-1.0.2/python.exe -B report_stage1b.py <prefix> <prefix>-report.txt

Run both from the harness folder. Neither writes anywhere except the given outputs, both opened with mode `x`. An
output under `C:/RideBoundData` or `E:/RideBoundData` is refused. Exit status of the extractor: 0 written,
2 refused, 3 unexpected error (nothing written).

## F. Review record

inventory-reviewed-by: <to be filled by the coordinator>
inventory-coverage: complete
