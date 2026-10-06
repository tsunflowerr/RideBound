# Independent recomputation of Family A (FORECAST-PLAN, rule causal-no15)

Date: 2026-10-06. Verifier: an independent agent. I wrote my own code from the task brief and
`tang3/evidence/METRICS-SPEC.md`. I also read `optimize/FORECAST-PLAN.md`, but only for context.
I did not open or run any of the code under test (`display_layer.py`, `promise_stretch.py`,
`forecast_family_a.py`, `family_a_two_sided.py`, `t38_*.py`, `sweep_analyze.py`, `audit_vep.py`,
`preflight_world*.py`). I did not open any `*-report.txt`, `*-cells.tsv`, `*-pads.json` or `*.out` file.
I computed all my numbers before I looked at the claimed values.

## Scripts (all new, all in this folder)

| File | Role |
|---|---|
| `ind_extract.py` | Reads the 42 raw transcripts (21 cells × {C-30, U}). Checks `frameSha256` on every frame. Takes the last `runnerToAdapter` checkpoint's `onlineState` and collects the first `passengerAlighted` time for each rider. Writes `extracted/<ARM>-<CELL>.json`. |
| `ind_analyze.py` | Builds the forecast profile, the stretch, the cell metrics, k_w, the oracle, the ledger and the bootstrap. Writes `ind_analyze_output.txt`, `ind_results.json` and `ind_cells_table.tsv` (per cell). |
| `ind_oracle34.py` | Supplementary. Runs the oracle over the 13 weekend T3.8 test cells as well (see item "oracle"). Writes `ind_oracle34_output.txt`. |
| `probe_transcript.py`, `probe_ledger.py`, `probe_tail.py` | Exploration probes I used to learn the frame and ledger layout. |
| `extract-run.log`, `oracle34-run.log` | Console logs. |

Command: `E:/RideBoundData/wp7/envs/fleetpy-1.0.2/python.exe -B <script>` with `PYTHONIOENCODING=utf-8`.

## Integrity and data checks (population, both arms)

- All frames passed the `frameSha256` check (0 failures). Every run has a final checkpoint.
- C-30: 1471 promised riders. U: 1512 promised riders. Every promised rider alighted exactly once, so promised = served.
- No rider alighted without a ledger history. No `requestId` appears twice in a ledger.
- Every revision's `previousPromise.dropEtaMs` equals the prior entry's published drop ETA (0 chain breaks).
- Every revision changes the drop ETA (0 unchanged), and no entry has `p < t`.
- No entry is published after A. Most riders' last entry is published at t = A with p = A (W = 0).
- Exact arithmetic (`fractions.Fraction`) and float arithmetic give the same classification for every rider and every metric (0 differences).

## Ambiguities I resolved

1. **C-30-d20181114-s10-r3-w17.** The brief says this cell is "not in the population". But d20181114-s10-r3-w17 *is* listed in the population.
   - I applied the stated rule and used `C-30-d20181114-s10-r3-w17-rerun1`.
   - The original folder has no `summary.json` and **no final checkpoint**, so the rerun is the only usable run.
2. **W ≤ 0.** The stretch is defined only for W > 0. For W ≤ 0 I display D = p.
   - Only W = 0 occurs: the final entry at alighting. There S = t = p anyway.
3. **"Drop revision" and the Vis denominator.** I treat every ledger entry as a drop revision. The denominator is served riders.
   - Neither choice changes anything here: every revision changes the drop ETA, and promised = served.
4. **k_w population.** k_w uses the C-30 riders of the 21 population cells, as the brief says.
   - Using served riders or all promised riders gives the same value, because the two sets are equal.
5. **Bootstrap RNG.** By default I give each statistic a fresh `random.Random(20261007)`.
   - I also ran one RNG shared across statistics in the order dY → dVis → dPad. All three intervals came out identical. (Only 300 distinct replicate values are possible, so this is plausible.)
6. **"Dispatch part 0" in the oracle.** I took this to mean Σ(p − e) = 0 over non-initial revisions.
   - The stricter reading, every revision has p = e, selects the same riders.

## Results (my computation)

### k_w (from promises only)

| w07 | w08 | w17 |
|---|---|---|
| 0.0336954 | 0.0098642 | 0.0001443 |

### C-30 (unweighted cell means)

| group | cells | Y_naive | Y_disp | Y_pad | E_naive | E_disp | Vis_naive | Vis_disp | extension (s) |
|---|---|---|---|---|---|---|---|---|---|
| w07 | 8 | 0.1444 | 0.0027 | 0.0424 | 0.0000 | 0.0020 | 0.1707 | 0.0174 | 27.8 |
| w08 | 8 | 0.0135 | 0.0018 | 0.0086 | 0.0000 | 0.0000 | 0.0256 | 0.0104 | 8.1 |
| w17 | 5 | 0.0000 | 0.0000 | 0.0000 | 0.0000 | 0.0000 | 0.0000 | 0.0000 | 0.1 |
| all 21 | 21 | 0.0601 | 0.0017 | 0.0194 | 0.0000 | 0.0008 | 0.0748 | 0.0106 | 13.7 |

### U (unweighted cell means)

| group | cells | Y_naive | Y_disp | E_naive | E_disp | Vis_naive | Vis_disp | extension (s) |
|---|---|---|---|---|---|---|---|---|
| w07 | 8 | 0.1511 | 0.0096 | 0.0000 | 0.0019 | 0.1753 | 0.0217 | 27.8 |
| w08 | 8 | 0.0617 | 0.0505 | 0.0000 | 0.0000 | 0.0716 | 0.0603 | 8.2 |
| w17 | 5 | 0.0676 | 0.0676 | 0.0000 | 0.0000 | 0.0697 | 0.0697 | 0.1 |
| all 21 | 21 | 0.0972 | 0.0390 | 0.0000 | 0.0007 | 0.1107 | 0.0478 | 13.8 |

### Oracle (C-30 riders with dispatch part 0, |A − S_true(t0, P0)| ≤ 2 s)

| population | riders passing / riders checked | max deviation |
|---|---|---|
| 21 population cells | 1452 / 1452 | 1.649 s |
| 13 weekend T3.8 test cells | 853 / 853 | 1.134 s |
| all 34 T3.8 test cells | 2305 / 2305 | 1.649 s |

### Lateness ledger (C-30, 21 cells; 68 riders with A − P0 > 60 s)

| total A − P0 | dispatch | anticipated (D0 − P0) | unanticipated |
|---|---|---|---|
| 6217.4 s | 81.7 s | 5352.6 s | 783.0 s |

### Inference (C-30, 21 cells; stratified day-window cluster bootstrap, B = 10000)

| statistic | point | 95% interval (250th and 9751st of 10000) |
|---|---|---|
| dY = Y_disp − Y_naive | −5.844 pp | [−8.779, −3.232] pp |
| dVis = Vis_disp − Vis_naive | −6.418 pp | [−8.286, −4.305] pp |
| dPad = Y_disp − Y_pad | −1.775 pp | [−2.829, −0.679] pp |

dY by day-window, in pp:

| window | 14/11 | 15/11 | 16/11 |
|---|---|---|---|
| w07 | −13.107 | −21.880 | −7.169 |
| w08 | −1.370 | −1.315 | −0.895 |
| w17 | 0.000 | (not in population) | 0.000 |

## Comparison with the experimenter's claimed values (done last)

| Item | Claimed | Mine | Verdict |
|---|---|---|---|
| k_w07 | 0.0336954 | 0.0336954 | match |
| k_w08 | 0.0098642 | 0.0098642 | match |
| k_w17 | 0.0001443 | 0.0001443 | match |
| C-30 21 Y_naive | 0.0601 | 0.0601 | match |
| C-30 21 Y_disp | 0.0017 | 0.0017 | match |
| C-30 21 Y_pad | 0.0194 | 0.0194 | match |
| C-30 21 E_naive | 0.0000 | 0.0000 | match |
| C-30 21 E_disp | 0.0008 | 0.0008 | match |
| C-30 21 Vis_naive | 0.0748 | 0.0748 | match |
| C-30 21 Vis_disp | 0.0106 | 0.0106 | match |
| C-30 21 extension | 13.7 s | 13.7 s | match |
| C-30 w07 Y_naive | 0.1444 | 0.1444 | match |
| C-30 w07 Y_disp | 0.0027 | 0.0027 | match |
| C-30 w07 Y_pad | 0.0424 | 0.0424 | match |
| C-30 w07 Vis_disp | 0.0174 | 0.0174 | match |
| C-30 w07 extension | 27.8 s | 27.8 s | match |
| U 21 Y_naive | 0.0972 | 0.0972 | match |
| U 21 Y_disp | 0.0390 | 0.0390 | match |
| oracle | 2305/2305 | 1452/1452 on the 21-cell population; 2305/2305 on all 34 T3.8 test cells | **population mismatch** (see below) |
| ledger total | 6217.4 s | 6217.4 s | match |
| ledger dispatch | 81.7 s | 81.7 s | match |
| ledger anticipated | 5352.6 s | 5352.6 s | match |
| ledger unanticipated | 783.0 s | 783.0 s | match |
| dY | −5.844 [−8.779, −3.232] | −5.844 [−8.779, −3.232] | match |
| dVis | −6.418 [−8.286, −4.305] | −6.418 [−8.286, −4.305] | match |
| dPad | −1.775 [−2.829, −0.679] | −1.775 [−2.829, −0.679] | match |

**The oracle mismatch is about population, not values.** The claimed 2305/2305 cannot come from the
21-cell population, which has only 1471 served C-30 riders. I reproduce exactly 2305/2305 over all
34 T3.8 test cells: the 21 population cells plus the 13 weekend cells on 17/11 and 18/11. The oracle
needs no forecast profile, so it is defined on weekend days too. On the 21-cell population the result
is 1452/1452. Both pass at 100%. The report should say that the oracle line covers 34 cells, or quote
1452/1452 for the 21-cell population.

Everything else I could reproduce. No value disagreed after rounding to the precision shown.
