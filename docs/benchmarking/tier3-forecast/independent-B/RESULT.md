# Independent recomputation of Family B and the snow tier (FORECAST-PLAN)

Date: 2026-10-06. Verifier: independent agent B. I wrote all the code myself from the task brief,
`tang3/evidence/METRICS-SPEC.md`, `optimize/FORECAST-PLAN.md` and `optimize/FORECAST-AMENDMENTS.md`.
I read `independent-A/RESULT.md` for ideas only; I did not open its code.

I did not open or run any code under test: `forecast_family_b.py`, `family_snow.py`,
`forecast_family_a.py`, `display_layer.py`, `promise_stretch.py`, `check_runs*.py`,
`sweep_analyze.py`, `audit_vep.py`, `t38_analyze.py` and `pilot_analyze.py`. I also did not open any
`*-report.txt`, `*.out` or `*-cells.tsv` under `optimize/` or `tang3/`.

I computed every number before I looked at the claimed values.

## Scripts and outputs (all new, all in this folder)

| File | Role |
|---|---|
| `probe_b.py`, `probe_b2.py` | Exploration probes to learn the frame, ledger and event layout. |
| `b_extract.py` | First extraction pass, into `extracted/`. **Superseded; not used.** The output was named by run folder only, so the two gate runs in `tier3-forecast-v1` (`Mplus-30-d20181115-s10-r4-w08`, `C-30-d20181115-s10-r2-w07`) overwrote the T3.8 naive files with the same names. Kept, not deleted. Log: `extract-run.log`. |
| `b_extract2.py` | Extraction used for every result. Writes `runs/<parent folder>__<run>.json` for 74 runs: 32 naive, 32 forecast, 8 snow, 2 gate. Log: `extract2-run.log`. |
| `b_analyze.py` | All Family B and snow metrics, the bootstraps and the integrity checks. Output: `b_analyze_output.txt` (console copy `b_analyze-run.log`), `b_results.json`, `b_cells.tsv` (per run). |
| `b_sensitivity.py` | Re-runs the bootstrap under other readings of the spec. Output: `b_sensitivity_output.txt`. |
| `b_snow_denominators.txt` | Served, promised and alighted counts for the snow runs. |

Command: `E:/RideBoundData/wp7/envs/fleetpy-1.0.2/python.exe -B <script>`, with `PYTHONIOENCODING=utf-8`.

All cell-level metrics, and the bootstrap itself, use exact rationals (`fractions.Fraction`). The
snow display layer is computed both exactly and in floats. The two give 0 differences in value or in
classification.

## Integrity checks (all 61 completed runs plus the 8 snow runs)

- `frameSha256` matched the base64-decoded bytes on every frame of all 74 transcripts.
- **Run status.** A run has `summary.json` if and only if it has a final `runnerToAdapter` checkpoint.
  - Failed: exactly 3 forecast runs. No naive run failed.
  - All 3 have the same `failureMessage` prefix: `RBWP7_FLEETPY_PLAN_INFEASIBLE`, `failureType` `AdapterFailure`.
  - In C-30F-d20181116-s10-r1-w08 and Mplus-30F-d20181116-s10-r1-w08 the message shows a stop with latest arrival 5175 and ETA 5176.6. This matches the diagnosis in amendment A1.
- **Decisions.** In each completed run, every produced decision is acknowledged, and every acknowledgement comes after its decision.
  - No acknowledged decision lacks a certificate body.
  - Each failed run has exactly one unacknowledged final decision.
- **Riders.** In every completed run, served (`completed`) = promised (ledger histories) = promised riders who alighted.
  - Every alighted rider has exactly one `passengerAlighted` event and a ledger history.
  - No completed rider is missing an alight, and no non-completed rider alighted.
- **Ledger (METRICS-SPEC cross-checks 1 and 2).**
  - The chain is continuous for both coordinates (every revision's `v` equals the previous entry's `p`).
  - For every drop revision, `|z|`, `|c|` and `|x|` equal the entry's `deltas.visible`, `deltas.decisionInduced` and `deltas.exogenous` `dropEtaTotalMs`.
  - Every ledger `publicationId` appears exactly once among the `promisePublished` actions of acknowledged decisions, with equal ETAs and `requestId`, and no acknowledged publication is missing from the ledger.
  - 0 issues in total.
- **Gate runs (extra).** The two gate runs in `tier3-forecast-v1` agree exactly with their T3.8 counterparts on these counts: served, acknowledged, non-normal, promised, own-break, late60 and visible60.

## Definitions as implemented

- **served:** number of `requests` with `lifecycle == "completed"` in the final checkpoint.
- **forced:** acknowledged decisions with `certificate.body.normalOperation == false`, divided by acknowledged decisions. Decisions are counted by distinct `decisionHash`, and the acknowledgement must come after the decision.
- **A:** the envelope `simTimeMs` of the rider's (only) `passengerAlighted` event, minus the drop ETA of entry 0 (`initialPromise`).
  - **own_break:** share of A > 30 000 ms.
  - **late60:** share of A > 60 000 ms.
  - Denominator for both: promised riders who alighted.
- **visible60:** share of promised riders whose Σ|p − v| over the drop coordinate of every `revision` entry is > 60 000 ms.
- **Bootstrap.**
  - Strata: w07, then w08. Clusters: day-windows, sorted by day.
  - In each replicate and each stratum, draw k clusters with `rng.randrange(k)`, where k is the number of clusters in the stratum. Pool their cells and take the cell mean.
  - Weight each stratum by its fixed share of cells (cells in stratum / total cells).
  - B = 10 000, with a fresh `random.Random(20261007)` for each statistic.
  - 97.5% interval: the 125th and 9876th sorted values (1-based). 95% interval: the 250th and 9751st.
- **Snow display layer.**
  - Profile: m(b) is the mean, over 12/11, 13/11 and 14/11, of `travel_time_factor` at `window start + min(b,8)·900` s. The window start is 50 400 s for w14 and 54 000 s for w15.
  - Weight: g = 1 in the current bin b0 = ⌊t/900 000⌋, and g(b) = m(b)/m(b0) in later bins.
  - S solves ∫ 1/g dτ = W, where W = p − t. Each later bin is walked in turn, and the weight is constant from bin 8 on.
  - D = max(p, S). When W ≤ 0, D = p (only W = 0 can occur).

## Results (my computation)

### Failed forecast runs

| Run | Status |
|---|---|
| C-30F-d20181114-s10-r3-w08 | failed (`RBWP7_FLEETPY_PLAN_INFEASIBLE`) |
| C-30F-d20181116-s10-r1-w08 | failed (`RBWP7_FLEETPY_PLAN_INFEASIBLE`) |
| Mplus-30F-d20181116-s10-r1-w08 | failed (`RBWP7_FLEETPY_PLAN_INFEASIBLE`) |

### Chain M (Mplus-30F − Mplus-30, the 15 cells where Mplus-30F completed; strata w07 = 3 clusters / 8 cells, w08 = 3 clusters / 7 cells)

| Metric | Naive mean | Forecast mean | Reduction | Diff (pp) | 97.5% interval (pp) | Day-windows decreasing | Verdict |
|---|---|---|---|---|---|---|---|
| forced | 0.627703 | 0.476159 | 24.143% | −15.15 | [−25.52, −6.76] | 6/6 | **PERSISTS** (reduction < 25%) |
| own_break (> 30 s) | 0.247258 | 0.075755 | 69.362% | −17.15 | [−18.66, −14.92] | 6/6 | **ARTEFACT** |

Change in forced, by day-window (pp):

| Window | 14/11 | 15/11 | 16/11 |
|---|---|---|---|
| w07 | −27.04 | −5.41 | −23.31 |
| w08 | −9.38 | −2.12 | −30.99 (2 cells) |

Change in own-break, by day-window (pp):

| Window | 14/11 | 15/11 | 16/11 |
|---|---|---|---|
| w07 | −24.22 | −30.05 | −28.62 |
| w08 | −5.18 | −3.11 | −6.56 |

Means by window, over cells where both arms completed:

| Window | Cells | Forced: naive → forecast | Own-break: naive → forecast |
|---|---|---|---|
| w07 | 8 | 0.7692 → 0.5939 | 0.3905 → 0.1099 |
| w08 | 7 | 0.4660 → 0.3416 | 0.0836 → 0.0367 |

### Chain C (14 cells where C-30F completed)

- **Served, C-30F − C-30.**
  - Per-cell differences: w07 +0, −2, +4, −6, +1, +1, −1, −1; w08 −2, +1, +2, −1, +2, +0.
  - Mean −0.1429, 97.5% interval [−1.1429, +0.2381].
  - **B-C1 PASS** (lower bound ≥ −2).
  - Extra: in the worst case, scoring the 2 failed C-30F runs as 0 served (amendment A1, rule 4), the result over 16 cells is −9.75 [−19.00, +0.083] and **B-C1 FAILS**.
- **C-30F over its 14 completed cells.**
  - Mean visible60 = 0.1792 and mean late60 = 0.0184.
  - For context, C-30 naive over the same cells: visible60 0.1093, late60 0.0874.
- **Non-normal acknowledged decisions in the completed C-30F runs:** 0. The 2 failed C-30F transcripts also contain 0 non-normal decisions, counting both acknowledged and produced.
- **Served, C-30F − Mplus-30F, over the 14 cells where both completed.**
  - Mean +0.0714, 95% interval [+0.0000, +0.1905].
  - Only one cell is not 0: d20181116-s10-r1-w07, at +1.

### Snow (C-30 naive, 15/11, w14 and w15, r1 to r4; served = promised = alighted in all 8 runs)

Profile m(b), for bins 0 to 8:

| Window start | m(b) | g(b)/m(0) |
|---|---|---|
| 50 400 s | 3.7447 … 3.7619 | 0.997 to 1.021 |
| 54 000 s | 3.7756 … 3.7726 | 0.969 to 1.013 |

The profile therefore anticipates almost no growth.

Late riders by cell:

| Cell | Served | Late, naive | Late, display |
|---|---|---|---|
| r1-w14 | 86 | 2 | 1 |
| r1-w15 | 75 | 6 | 5 |
| r2-w14 | 74 | 6 | 6 |
| r2-w15 | 66 | 4 | 4 |
| r3-w14 | 80 | 6 | 6 |
| r3-w15 | 85 | 1 | 0 |
| r4-w14 | 75 | 7 | 6 |
| r4-w15 | 60 | 9 | 6 |

- **Y_naive** (mean over the 8 cells) = 0.0719.
- **Y_disp** = 0.0594.
- The display removes 17.40% of Y_naive (relative), or 1.25 pp.
- **Ledger** over the 41 riders with A − P0 > 60 s:
  - total A − P0 = 3445.933 s;
  - dispatch = 8.566 s;
  - anticipated (D0 − P0) = 251.496 s;
  - unanticipated = 3185.871 s;
  - **unanticipated share 92.45%**;
  - the three parts sum exactly to the total.

## Ambiguities I resolved, and sensitivity (`b_sensitivity_output.txt`)

1. **Stratum weight in the bootstrap.** I used the fixed share of cells (cells in stratum / total cells).
   - The alternative is the grand mean of the pooled draws, which reweights each replicate by the drawn cell counts. That gives slightly different intervals:
     - forced [−25.41, −6.21];
     - own-break [−20.29, −13.17];
     - Chain C [−0.909, +0.200];
     - C-30F − Mplus-30F [0, 0.1875].
   - No verdict changes. The claimed intervals match the fixed-weight reading.
2. **One RNG or a fresh RNG per statistic.** If one RNG is shared across statistics (in the order forced, own-break, Chain C, C−M), only the Chain C upper bound moves, from +0.2381 to +0.1769.
   - The claimed +0.238 matches a fresh `random.Random(20261007)` per statistic, or equivalently identical draws per statistic.
   - B-C1 passes either way.
3. **Percentile indices.** The plan's "index 124 and 9875" read as 0-based is the same as the brief's 125th and 9876th 1-based.
   - Off-by-one readings give identical end points here, because the replicates have many ties.
4. **Denominators.** own_break and late60 use promised riders who alighted, and visible60 uses promised riders.
   - In every completed run, served = promised = alighted, so any of these denominators gives the same values.
5. **"Non-initial drop revisions".** I take every `revision` entry. Pickup-only revisions contribute |z| = 0 to the drop sum, so the choice is immaterial.
6. **Day-window decrease count.** I use the mean cell difference within each day-window, which is less than 0 in all 6.
7. **Snow ledger with W ≤ 0.** D = p. Only the final entry at alighting has W = 0, and that never affects D0, which comes from entry 0.

## Comparison with the experimenter's claims (done last)

| Item | Claimed | Mine | Verdict |
|---|---|---|---|
| Failed runs | C-30F d20181114-s10-r3-w08, C-30F d20181116-s10-r1-w08, Mplus-30F d20181116-s10-r1-w08 | same 3 runs (no naive run failed) | match |
| Chain M forced verdict | PERSISTS | PERSISTS | match |
| Chain M forced reduction | 24.1% | 24.143% | match |
| Chain M forced diff | −15.15 pp | −15.15 pp | match |
| Chain M forced 97.5% CI | [−25.52, −6.76] | [−25.52, −6.76] | match |
| Chain M forced day-windows | 6/6 | 6/6 | match |
| Chain M own-break verdict | ARTEFACT | ARTEFACT | match |
| Chain M own-break reduction | 69.4% | 69.362% | match |
| Chain M own-break diff | −17.15 pp | −17.15 pp | match |
| Chain M own-break 97.5% CI | [−18.66, −14.92] | [−18.66, −14.92] | match |
| Chain M own-break day-windows | 6/6 | 6/6 | match |
| B-C1 | −0.143 [−1.143, +0.238] PASS | −0.1429 [−1.1429, +0.2381] PASS | match |
| C-30F visible60 (14 cells) | 0.1792 | 0.1792 | match |
| C-30F late60 (14 cells) | 0.0184 | 0.0184 | match |
| Non-normal decisions, completed C-30F | 0 | 0 | match |
| Served, C-30F − Mplus-30F | +0.071 [+0.000, +0.190] | +0.0714 [+0.0000, +0.1905] | match |
| w07 Mplus forced | 0.769 → 0.594 | 0.7692 → 0.5939 | match |
| w07 Mplus own-break | 0.390 → 0.110 | 0.3905 → 0.1099 | match |
| Snow Y_naive | 0.0719 | 0.0719 | match |
| Snow Y_disp | 0.0594 | 0.0594 | match |
| Snow unanticipated share | 92.5% | 92.45% | match |
| Snow ledger dispatch | 9 s | 8.566 s | match (rounded) |
| Snow ledger anticipated | 251 s | 251.496 s | match (rounded) |
| Snow ledger unanticipated | 3186 s | 3185.871 s | match (rounded) |

**Every claimed item reproduces.** No value disagreed after rounding to the precision shown.

## Observations for the report (not in the list of claims; for the caller to check)

- **Worst case for B-C1.** Under amendment A1 rule 4, scoring a failed run as 0 served, B-C1 **fails**: −9.75 [−19.00, +0.083]. The report should show this next to the PASS on completed cells only.
- **PS1, middle clause.** The display layer removes 17.4% of the snow Y_naive (relative; 1.25 pp absolute). The sealed PS1 predicted ≤ 15%.
  - If PS1 is scored as a whole, it fails on this clause.
  - The other two clauses hold: Y_naive 7.19% is inside [3%, 15%], and the unanticipated share 92.45% is ≥ 90%.
  - This depends on reading "gỡ ≤ 15% phần đó" as a relative share.
- **Other sealed Family B predictions** (numbers only):
  - PB1: Mplus-30F forced is 0.594 at w07, outside [0.10, 0.50], and 0.342 at w08, inside [0.05, 0.35]. Chain M forced is PERSISTS, not PARTIAL or ARTEFACT.
  - PB2: Mplus-30F own-break at w07 is 0.110.
  - PB3: visible60(C-30F) − visible60(C-30) is > 0 in 4/6 day-windows (14 completed cells): +0.097, +0.130, +0.128, 0.000, +0.012, −0.015.
  - PB6: 0 non-normal in all 14 completed C-30F runs. The 2 failed runs also had 0, but they did not complete.
  - PB7: C-30F late60 at w07 is 0.0287.
  - PB8: the 95% interval is [0, 0.19].
