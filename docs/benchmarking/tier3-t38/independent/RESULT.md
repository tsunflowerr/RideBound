# T3.8 / T3.9 independent verification (RESULT)

Verifier: independent code written from METRICS-SPEC.md, T38-PLAN.md and the task specification.
Not read or run: t38_analyze.py, t38_calibrate.py, w07x_analyze.py, sweep_analyze.py, audit_vep.py,
pilot_analyze.py, conformal.py, any *-report.txt or analysis-* output (the analysis-* names appeared in a
directory listing only; their contents were never opened). GAMMA-t38-v1.json was read only after my gamma was
computed, and the experimenter's claimed values are compared only in the table at the end.

Scripts (all new, in this folder):
- iv_extract.py : reads each transcript-00.ndjson, decodes frames, builds per-run cache (cache/*.json) with decisions,
  acknowledgements, passengerAlighted times, requests lifecycle and the per-rider v/e/p drop history from the final checkpoint.
- iv_analyze.py : computes items 1 to 7. Raw printed output: iv_analyze.out.txt.
- explore1.py, explore2.py, explore3.py : throw-away structure exploration of one transcript.

Data used: 15 calibration runs (C-30) and 34 x 5 test runs = 185 runs; all 185 summary.json have status pass.
Rerun folders used: C-30-d20181118-s10-r1-w17-rerun1 (calibration) and C-30-d20181114-s10-r3-w17-rerun1 (test);
no folder without summary.json was used.

## 1. Calibration
Scores S_j (ms): w07: r1 133573; r2 222259; d15 r3 165116; d16 r3 97509. w08: 81240; 133835; 55167; 92357; 89000; 99988.
w17: 30210; 50393; 53640; 47309; 66119. (Full per-cell list in iv_analyze.out.txt.) All 15 episodes: every promised rider alighted, so no infinite score.
n = 15, k = ceil(16 x 9 / 10) = 15 (integer arithmetic), gamma = 222259 ms (cell d20181114-s10-r2-w07). Identical to gammaMs in GAMMA-t38-v1.json (222259, k 15, n 15, infinite false), and all 15 per-cell scores equal the file's calibrationScoresMs.

## 2. Test scores
34 scores computed. 32 covered, 2 violating:
- d20181114-s10-r3-w07: 252492 ms (252.5 s)
- d20181115-s10-r2-w07: 226400 ms (226.4 s)
Largest covered score: d20181115-s10-r1-w07 205726 ms. No test episode has an infinite score (0 promised riders without a passengerAlighted event in all 185 runs).

## 3. Served (completed lifecycle in the final checkpoint)
Totals over the 34 test cells: U 2434, C-30 2338, Mplus-30 2338, Mplus-60 2347, V-30 2336.
Per-cell table is in iv_analyze.out.txt. Mean differences: D_M 0.0000 (range -2..+1), D_V +0.0588 (range -2..+1), F +2.8235 (range -4..+9).
Number of cells with |difference| > 2: C-30 minus Mplus-30: 0; C-30 minus V-30: 0; U minus C-30: 21 of 34.
(The largest |C-30 minus Mplus-30| and |C-30 minus V-30| is 2, in d20181114-s10-r3-w08, where C-30 served 73 and both comparison arms 75.)

## 4. Paired cell bootstrap (B = 10000, random.Random(20261002), 34 randrange(34) draws per replicate, shared across statistics, interval = sorted[249], sorted[9750])
| statistic | mean | 95% interval |
|---|---|---|
| D_M | +0.0000 | [-0.1471, +0.1176] |
| D_V | +0.0588 | [-0.1176, +0.2059] |
| F | +2.8235 | [+1.7941, +3.8235] |
| dE2_V | +0.000857 | [-0.000082, +0.002218] |
| dE2_M | +0.000857 | [-0.000082, +0.002218] |

E2 means over test cells: U 0.1096, C-30 0.0578, Mplus-30 0.0570, Mplus-60 0.0596, V-30 0.0570.

## 5. Non-normal decisions (acknowledged decisions with certificate.body.normalOperation == false)
- C-30: 0 in each of the 49 runs (15 calibration + 34 test); also 0 when counting all decisions, not only acknowledged ones.
- Mplus-30: mean share 0.4131 over 34 cells (7710 of 20103 acknowledged decisions; 28 cells above zero).
- V-30: mean share 0.5378 (10442 of 20090; 34/34 cells above zero).
- Extra: Mplus-60 0.2253 (4031 of 20163; 22 cells), U 0.

## 6. Own-promise-broken share (mean over the 34 test cells; cells with share > 0)
| arm / rule | all | w07 (9 cells) | w08 (14) | w17 (11) |
|---|---|---|---|---|
| C-30, C_r > 30 s | 0.0000 (0/34) | 0 (0/9) | 0 (0/14) | 0 (0/11) |
| Mplus-30, A > 30 s | 0.1448 (28/34) | 0.3653 (9/9) | 0.1087 (14/14) | 0.0104 (5/11) |
| Mplus-60, A > 60 s | 0.0423 (22/34) | 0.1267 (9/9) | 0.0203 (12/14) | 0.0013 (1/11) |
| V-30, V_r > 30 s | 0.2158 (34/34) | 0.4209 (9/9) | 0.1779 (14/14) | 0.0961 (11/11) |

Pooled over riders instead of the mean of cell shares: Mplus-30 289/2338 = 0.1236, Mplus-60 79/2347 = 0.0337 (not the claimed convention).

## 7. Correctness check
In all 32 covered C-30 test episodes, max_r V_r <= 30000 + gamma = 252259 ms holds; the worst max V among covered episodes is 216005 ms. No failures.
Extra: the maximum C_r in covered episodes is 29239 ms (<= 30000). Across all 34 C-30 test episodes the maximum V is 252492 ms (the uncovered d20181114-s10-r3-w07), which exceeds 252259, as expected for an uncovered episode.

## Sanity and cross-checks
- Spec cross-check 1: over 382006 drop revisions in all 185 runs, |e-v|, |p-e|, |p-v| equal deltas.exogenous / decisionInduced / visible .dropEtaTotalMs in 100% of revisions (0 mismatches).
- Alight time convention: A uses the simTimeMs of the adapterToRunner eventBatch that contains the passengerAlighted event. Check: in the first 6 test cells of Mplus-30, alight time minus the final published drop ETA is exactly 0 for all 441 alighted riders, so the event timestamp is the true alight time.
- Acknowledged decisions = distinct decisionHash with a later decisionApplied; acknowledged count equals the total decision count in all 185 runs.

## Ambiguities resolved (and effect)
1. Mplus lateness denominator: the task says riders who alighted. Since every promised rider alighted in all runs, "alighted" and "promised" denominators give identical numbers.
2. "Share ... mean over the 34 cells" read as the unweighted mean of per-cell shares; no cell has zero promised riders.
3. Served taken from the final checkpoint requests (lifecycle completed); summary.json does not contain a served count, so there was no second source to cross-compare.
4. p0 for Mplus lateness = initialPromise entry publishedPromise.projection.dropEtaMs.
5. Items I could not reproduce: none. After computing, I also compared all 15 individual calibration scores with calibrationScoresMs in GAMMA-t38-v1.json: 15/15 identical.

## Comparison with the experimenter's claims
| item | claimed | independent | result |
|---|---|---|---|
| gamma | 222259 ms | 222259 ms | match |
| test violations | 2 (d20181114-s10-r3-w07 252.5 s; d20181115-s10-r2-w07 226.4 s) | 2, same two cells, 252492 and 226400 ms | match |
| served totals | U 2434, C-30 2338, Mplus-30 2338, Mplus-60 2347, V-30 2336 | identical | match |
| mean F | +2.8235 | +2.8235 | match |
| mean D_M and interval | 0.0000, [-0.1471, +0.1176] | 0.0000, [-0.1471, +0.1176] | match |
| mean D_V and interval | +0.0588, [-0.1176, +0.2059] | +0.0588, [-0.1176, +0.2059] | match |
| dE2 mean and interval (both) | +0.0009, [-0.0001, +0.0022] | +0.000857, [-0.000082, +0.002218] (both) | match (to 4 decimals) |
| non-normal in C-30 | 0 | 0 in 49 of 49 runs | match |
| Mplus-30 own-promise-broken | 0.145 (28/34) | 0.1448 (28/34) | match |
| Mplus-60 own-promise-broken | 0.042 (22/34) | 0.0423 (22/34) | match |
| V-30 own-promise-broken | 0.216 (34/34) | 0.2158 (34/34) | match |
| w07 Mplus-30 | 0.365 (9/9) | 0.3653 (9/9) | match |
| w17 Mplus-30 | 0.010 (5/11) | 0.0104 (5/11) | match |
| item 7 invariant | (plan: must hold) | holds in 32/32 covered episodes | consistent |

No mismatches found.
