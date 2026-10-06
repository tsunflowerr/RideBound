# Number-trace and hostile review: FORECAST-REPORT.md and the docs/18 / docs/19 updates

Reviewer: independent agent. Date: 2026-10-06. I did not edit any file except this one.

## 0. Scope and method

**Documents under review**
- A = `optimize/FORECAST-REPORT.md` (174 lines). It is cited below as `A:<line>`.
- B = `C:\Code\RideBound-noworse\docs\18-status-and-decision-log.md`, §9 entries dated 2026-10-06, 2026-10-02 and 2026-10-01 (uncommitted diff, lines 5176–5211). Also `docs\19-requirement-traceability.md` §32 (uncommitted diff).

**Sources checked**
- Every file listed in the brief.
- `forecast_family_a.py` and `forecast_family_b.py`. I read these for definitions only and did not run them.
- The `failure-00.json` of the 3 stopped runs, read only.
- `git log` and the reflog of `research/tier3-no-worse`.
- `business-need/SOURCES-2026-10-05.md`.
- `tang3/t38/independent/RESULT.md`.
- `day-profiles-2026-10-06.txt`.
- `smoke-breakdown-v14-2026-10-06.txt`.
- `docs/03-related-work-and-claim-boundary.md`.

**Recomputation**
- I wrote my own read-only scripts in my session scratchpad (`a1.py` … `a5.py`). They aggregate the `*-cells.tsv` files and the per-cell lines of `test-family-b-2026-10-06-report.txt`.
- I re-implemented the stratified bootstrap from its definition to get more digits. I did not rerun any analysis script and I did not touch any run.

**Status codes used below**
- **OK**: the number is found in an output and rounds correctly.
- **OK-TSV**: OK, but the number comes from a cells TSV or per-cell lines, not from a report line.
- **ROUND**: the rounding is wrong.
- **POP**: the number is attributed to the wrong population, arm or window.
- **NF**: not found in any output.
- **CONTRA**: contradicted by an output.
- **CLAIM**: a wording, overclaim or rule-statement problem.

---

## 1. Required corrections, summary

### Blocking (fix before the report or docs are used)

| ID | Where | Problem | Fix (short) |
|---|---|---|---|
| B1 | A:108 | **POP.** "khung 8h: 49,0% → 35,1%" is stated under "Chuỗi M (… 15 ô chạy xong)". It is really computed on **6** w08 cells (the cells where both arms completed). On the 7 w08 cells of Chain M the value is **46,6% → 34,2%**. | Replace (see §4.1). |
| B2 | A:126 (PB1), A:163–173 (§7) | **CONTRA.** PB1's w08 clause is graded wrong: on the Chain M population w08 forced = 0,342, which is inside [0,05; 0,35]. Independent-B reported exactly this (`independent-B/RESULT.md:219`), but §7 says "Mọi mục khớp" and omits that observation. PB1 stays **Sai**, but for the w07 clause and PERSISTS only. | Rewrite the PB1 cell and the §7 sentence (see §4.1). |
| B3 | A:50–51, A:96; docs/18; docs/19 | **CLAIM (pre-registered label misstated).** The sealed rule adds the label "mong manh với suy luận ít cụm" whenever "khoảng cluster-t (df = 5) có cận trên ≥ 0" (`FORECAST-PLAN.md:100`). The verdict says "a stratified cluster-t interval" (`debate1-verdict` F-Family-A labels). Neither restricts the rule to ΔY. dPad has cluster-t [−3,620; **+0,071**] (`test-family-a-causal-no15-…-report.txt:36`). The sealed script checks dY only (`forecast_family_a.py:268`). So "Không có nhãn cảnh báo nào" and "Cluster-t chỉ dùng để gắn nhãn cho A1" restate the rule more narrowly than the sealed text. | Attach the label to A3. State that the code and the plan text differ. Carry the caveat into §3, docs/18 and docs/19. |
| B4 | docs/18 2026-10-06 | **Hidden negative and missing mandatory label.** (a) "khi bỏ 12/11 khỏi nguồn, suy luận mong manh (2 buổi sáng)" hides that **A1 FAILS** in that sensitivity (`test-family-a-causal-no12-…out:40`: "A1 FAIL"). (b) The entry gives 6,01% → 0,17% without the mandatory Family A label ("đo lường có đăng ký trước trên các ngày-khung đã được xem trước", plan §4 line 98). It also lacks the near-deterministic / ceiling caveat (plan §10). | Add all three (see §5.1). |
| B5 | A:92–95 | **CLAIM (real-world overclaim).** "Dữ liệu thật … cho thấy điều làm khách bỏ đi là trễ so với giờ đã báo". The sources show something weaker. Cohen et al. 2022 find that frustrated riders ride and spend less afterwards, with frustration defined at 8–10 min (SOURCES:41–45). Harter, Mao and Liang are **abstract-only** (SOURCES:66, 78, 90). None shows lateness is *the* reason riders leave. The scale gap is minutes vs 60 s (SOURCES:118). Then "Trên mô phỏng tắc đường thật, lớp lời hứa gần như xóa loại trễ đó" ties a 60 s simulated metric to retention. That jumps the claim ladder (docs/03 §6: "Không nhảy từ tầng 2 lên tầng 6"). | Rewrite (see §4.3). |

### Minor (should fix; none changes a pass/fail)

| ID | Where | Problem |
|---|---|---|
| M1 | A:50 | ROUND. dPad = −1,7746 pp, so it rounds to **−1,77**, not −1,78. The output prints −1.775, which caused a double rounding. |
| M2 | docs/18 2026-10-06 | ROUND. Snow ledger shares "0,3% / 7,3% / 92,5%": dispatch 8,566 / 3.445,933 s = **0,25%**, which rounds to 0,2%. The "0,3%" comes from the rounded "9 s". |
| M3 | A:23 | POP. "Vì vậy dùng lại được 170 lần chạy cũ làm đối chứng." The E1/E2 gate licenses reuse of the **32** T3.8 naive runs (C-30, Mplus-30 × 16 cells) as Family B controls (plan §4 line 104; verdict Family B setup). Family A replays all 170 runs and never depended on the gate (plan §2 line 50). |
| M4 | A:105; docs/18 | The stop diagnosis is over-generalised. "muộn hơn hạn 1,67 s, ở bin 5" is true only for the 16/11 r1 w08 cell, where both arms stopped on the same stop: latest 5175, ETA 5176,666 in `failure-00.json`. In `C-30F-d20181114-s10-r3-w08` the stop has latest arrival 5870 and ETA 5872,686, so it is **2,69 s** late. The deadline falls in bin 6, where r = 1,007172. This stop was not diagnosed. "vài phần nghìn" also fails on 14/11, where r = 0,988249 (1,2%). |
| M5 | A:129, A:131, A:128 | PB4 and PB6 are "Đúng" only on completed runs. The completed-cells primary analysis comes from amendment A1, written **after** the stops were seen. PB3's prediction says "trên 16 ô"; the 4/6 is on 14 cells. These should be labelled "đúng có điều kiện". The counts are then 17/19 (2 conditional), and 7/9 among the genuinely falsifiable PB/PS predictions. |
| M6 | A:134 (PC1) | PC1's positive-control clause ("U so với C-30 khác") was observed **before** sealing, on a development cell (`equiv-selftest-2026-10-06-c.txt`, 08:21). `check_gates.py` does not rerun a positive control. Say so. |
| M7 | A:108, A:111 | PERSISTS is knife-edge. The reduction is 24,1% against a 25% threshold. The interval on the difference [−25,52; −6,76] pp implies a relative reduction of about **10,8%–40,7%**, which spans the PARTIAL threshold. "Khác biệt … đứng được" should rest on the robust fact: Mplus-30F is forced on 47,6% of decisions (15 cells, 0,476159) while C-30F has 0. |
| M8 | A:88; docs/18 | CLAIM. "Sổ chia trễ phân biệt **đúng** hai loại mà 49 CFR 37.131(f)(3)(ii) và Phụ lục D phân biệt" and docs/18 "Khớp cách phân biệt". The regulation is an ADA paratransit pattern-or-practice test (SOURCES:138–139). "Anticipated" in the ledger means "inside this particular forecast built from 2–3 prior days". Snow being "unanticipated" is partly by construction, because 15/11 is excluded from the sources. Use "tương ứng với" or "được định nghĩa theo tinh thần của". |
| M9 | A:12 | "Mọi số trong báo cáo đến từ tệp output có script đi kèm: [list]" is incomplete. These numbers come from files outside the list: 1.471, "26 ở khung 7h", 1,06% (PA2), U w17 "toàn bộ do điều phối", and 4/6 (PB3) come from the cells TSVs or per-cell lines. 1.452 and 853 come from `independent-A/RESULT.md`. 1,67 s comes from `FORECAST-AMENDMENTS.md` / `failure-00.json`. 36,5% comes from `T38-REPORT.md:62`. |
| M10 | A:3–6 | Sealed-hash disclosure. `forecast_family_b.py` no longer matches the sealed hash in `FORECAST-PLAN.sha256`: sealed `4c83df7b…`, current `9695bddb…`, changed in `df7f893` for A1. `family_a_two_sided.py` (08:27:53) and `family_snow.py` (08:44:12) were written after sealing and are not in the sealed list, though both predate any test-data analysis. The B1 population bug was introduced by the A1 edit: the sealed version used `ws = [c for c in cells if c.endswith(w)]` over all 16 cells. Say this in the header. |
| M11 | A:150 | The v1.4 claim "giảm được nhưng không hết dao động" is selective. `smoke-breakdown-v14`: v1.3 visible60 0,224; v1.4 d900 0,167 (lower); v1.4 d1800 **0,314** (higher than v1.3); naive 0,057–0,080. All on one development cell. |
| M12 | A:82, A:145, plan §9.4 | "12/11 … có thể là ngày nghỉ bù Veterans Day; chưa kiểm" understates something checkable. 11/11/2018 was a **Sunday**, so the federal observed holiday was **Monday 12/11/2018** (5 U.S.C. 6103(b)). `day-profiles-2026-10-06.txt` shows 12/11 lighter at 08:00 (2,797 vs 3,344–3,536 on 13–15/11). 12/11 is a source day for **every** forecast in the main analysis. Whether NYC traffic was holiday-like can stay "chưa kiểm", but the calendar fact should be stated. |
| M13 | A:155–162 | Independent-A's scope is overstated by omission. It did **not** check: cluster-t intervals; the dE interval; the pickup extension (3,2 / 6,5 s); p90; MAE; late120; the 34 pickups after the window; leave-one-day-out; the 34-cell deployment mean; any sensitivity variant; or the Mplus/V arms. Say so. |
| M14 | A:41 | The pad row "7,48% (đệm hằng số không đổi dao động)" is not measured. It holds by construction (`forecast_family_a.py:16–17`: "revisions shift by the same constant, so pad churn = naive churn"). Write "theo cấu tạo". |
| M15 | A:55 | "p90 33,3 s" / "p90 66,3 s" are **means of per-cell p90s** (`cell_metrics` then `statistics.mean`), not pooled rider p90s. Label them. |
| M16 | A:95, A:151, A:142 | "mô phỏng tắc đường thật" / "trên tắc đường thật" should read: "mô phỏng dùng một hệ số tắc đường đo thật, chung cho cả thành phố". A:142–143 already explains this; keep the wording consistent. |
| M17 | A:120 | "dự báo nên nằm ở lớp lời hứa … chừng nào bộ điều phối chưa hiểu giờ đi phụ thuộc thời điểm". Scope this to "với lớp bọc v1.3 (tỉ số cố định theo khung 15 phút), K = 2, 16 ô". |
| M18 | A:28–29 | Ratio lines were checked for the 29 completed runs only. The world-line rule was relaxed post hoc (A1; `check-runs-tier2-2026-10-06.log` v1 had 2 world-line differences). Mention both. |
| M19 | A:149 vs A:57 | "2,3% lời hứa đón" (§6) vs "34/1.471 khách" (§2). `pick_after_window` is evaluated on each rider's **first** pickup promise only (`forecast_family_a.py:87–99`). Use "2,3% khách (lời hứa đón đầu tiên)" in both places. |
| M20 | A:73–84 | Omitted co-reports. The plan §5 sensitivity list includes "đệm toàn thành phố" (dPadCity −3,018 [−4,740; −1,225], cluster-t [−5,961; −0,075]), and §5 lists display results for Mplus-30, Mplus-60 and V-30. The verdict asks that the T3.8-style cell bootstrap be co-reported ([−9,258; −2,753]). None are in A. All are favourable, so this is not hiding a negative. |
| M21 | docs/18 2026-10-02 | "chênh mỗi ô trong ±0,25 khách" misstates T38-REPORT. The **mean** per-cell difference and its 95% interval lie within ±0,25 (T38-REPORT:43–45, 82). The largest single-cell difference is **2** riders (T38-REPORT:110). |
| M22 | docs/18 2026-10-02 | "2.332 khách chung" applies only to C-30 vs M⁺-30 (`rider-experience…report.txt:111`). "mọi trần giảm một nửa số khách thấy giờ trả đổi > 1 phút" holds over 34 cells (visible60 0,1096 → 0,0570–0,0596, −46% to −48%). At w07 the drop is only −9% to −11% (0,1699 → 0,1519–0,1550). Also, H3 omits "mức 120 s: không đạt" and "cận trên CP 17,4% không dưới 10%" (T38-REPORT:24, 26). |
| M23 | docs/18 2026-10-06 | The entry omits that B-C1 passes only on completed cells and **fails** in the worst case. It omits the independent check of Family B and snow. "15 ô chạy xong" for Chain M and "14 lần chạy xong" for "C-30F vẫn 0 non-normal" are missing. "17/19" should say that 2 are conditional. |
| M24 | docs/19 §32 | Path conventions are mixed. The header says paths are relative to `ridebound-scratchpad`. One row uses `ridebound-scratchpad/optimize/independent-A/RESULT.md` (this would double the prefix) and one uses a repo path (`docs/benchmarking/tier3-forecast/...`). "kéo dãn … khớp 2.305/2.305 khách" should say "2.305/2.305 khách C-30 có phần do điều phối bằng 0, trên 34 ô (1.452/1.452 trên 21 ô)". |
| M25 | docs/19 §32 | "23 test, 8/8 đột biến" (display layer) and "7 test tổng hợp" have no saved test log in `optimize/`. The mutant list does have 8 entries (`mutate_display_layer.py:15–30`). "23/23" is also exactly the v1.3 world-wrapper count (`EVIDENCE-ADDENDUM-1:8`), and plan §8 repeats "23/23, 8/8" for both components. Confirm the display-layer count and save the logs. |
| M26 | docs/18 (process) | AGENTS.md asks for "the relevant decision entry" and next actions to be updated when claims or next actions change. Only the header and §9 changed. |

---

## 2. Provenance checks that passed

| Claim | Evidence |
|---|---|
| Plan SHA-256 `a5524912…7337` (A:3) | `sha256sum` of `optimize/FORECAST-PLAN.md`, of `docs/benchmarking/tier3-forecast/FORECAST-PLAN.md`, and of `git show 33d8071:…/FORECAST-PLAN.md`: all three are `a5524912b834…74d37337`. |
| Pushed before any job (A:4) | Commit `33d8071` 08:27:14 (+0700). Reflog: "update by push" 08:27:18. `tier3-forecast-v1` created 08:27:26 (parent mtime). Gate driver finished 08:42:41 after 915 s, so it started ≈ 08:27:26. |
| A1 pushed before any Family B metric (A:6) | Commit `df7f893` 10:35:45, push 10:35:47. `test-family-b-2026-10-06-report.txt` 10:41. Small inconsistency inside `FORECAST-AMENDMENTS.md:5`: it says "khoảng 10:40", but the file mtime is 10:34:36. |
| Earlier plan hashes (docs/19 §32 row 1) | w07x: blob in `aa98fa5` = `72f8b481…5be6` = `W07X-PLAN.sha256`. T3.8: blob in `323a8d9` = `78954a15…8a60` = `T38-PLAN.sha256`. |
| Other sealed files | All other files listed in `FORECAST-PLAN.sha256` match, except `forecast_family_b.py` (M10). |
| 43 jobs, 5 development runs (A:7–11) | Driver outputs 3 + 16 + 16 + 8. `tier3-forecast-checks-v1` holds 5 folders. |
| Independent-A count 25/26 (A:155) | `independent-A/RESULT.md:106–133` has 26 comparison rows: 25 match, 1 is the population mismatch. |
| Independent-B on 74 runs (A:163) | `independent-B/RESULT.md:20`. |

---

## 3. Number trace, Document A

### §0–§1 (A:1–30)

| A:line | Claim | Source | Status |
|---|---|---|---|
| 8–9 | 3 gates 08:27–08:43; 32 + 8 "khoảng 08:45–10:30" | driver outs; tier1 started ≈ 08:43, snow finished 10:32:45 | OK (approximate) |
| 20–22 | E1/E2: decisions, table, served, alighted, frames all the same | `check-gates…log:3–4` | OK |
| 23 | "dùng lại được 170 lần chạy cũ" | plan §2 / §4 | POP (M3) |
| 24 | S1 ran to completion | `check-gates…log:2` | OK |
| 26 | 29/32, 3 stops | `check-runs-v2-tier2…log:15–18` | OK |
| 27 | snow 8/8 | `check-runs-snow…log` | OK |
| 28 | ratios to 1e−6 | `check_runs_v2.py:52`; 29 completed runs | OK (M18) |
| 29 | world lines equal on common bins | v2 logs | OK (rule relaxed by A1, M18) |
| 30 | 0 non-normal decisions in completed C-30F runs | v2 logs; `test-family-b…:38` | OK |

### §2 Family A (A:32–88)

All "report.txt" references below are to `test-family-a-causal-no15-2026-10-06-report.txt`.

| A:line | Claim | Source | Status |
|---|---|---|---|
| 36 | Y 6,01 / 1,94 / 0,17% (21 cells) | report.txt:8 (0.0601 / 0.0194 / 0.0017) | OK |
| 37 | w07 14,44 / 4,24 / 0,27% | report.txt:9 | OK |
| 38 | w08 1,35 / 0,86 / 0,18% | report.txt:10 | OK |
| 39 | w17 0 / 0 / 0 | report.txt:11 | OK |
| 40 | E: naive 0%, display 0,08% | report.txt:8 (0.0000 / 0.0008) | OK |
| 41 | Vis: naive 7,48%, display 1,06%; pad 7,48% | report.txt:8. Pad value is by construction. | OK (M14) |
| 42 | MAE 17,1 → 7,0 s | report.txt:8 | OK |
| 43 | late120 1,03% → 0% | report.txt:8 | OK |
| 46 | ΔY −5,84 [−8,78; −3,23]; cluster-t [−10,54; −1,15] | report.txt:33. My recompute: −5,844260 [−8,779391; −3,232047]. | OK |
| 47 | 14/11 −13,1; 15/11 −21,9; 16/11 −7,2 | report.txt:38 | OK |
| 48 | ΔE +0,08 | report.txt:34 (+0.077; recompute +0,076805) | OK |
| 49 | ΔVis −6,42 [−8,29; −4,31] | report.txt:35. Recompute: UB −4,305094, so −4,31 is right. | OK |
| 50 | A3 −1,78 [−2,83; −0,68]; cluster-t [−3,62; +0,07] | report.txt:36. Point recompute −1,774640. | ROUND (M1); label (B3) |
| 51 | "Không có nhãn"; leave-one-day-out −6,25 / −3,54 / −8,21 | report.txt:39–40. My recompute of leave-one-day-out from the TSV agrees. | numbers OK; label CLAIM (B3) |
| 52 | 34-cell mean −3,61 | report.txt:41; TSV recompute −3,610 | OK |
| 55 | ext 13,7 (p90 33,3); w07 27,8 (p90 66,3) | report.txt:8–9 | OK (M15) |
| 56 | pickup extension 3,2 s; w07 6,5 s | report.txt:8–9 | OK |
| 57 | 34/1.471 (2,3%), 26 at w07 | report.txt:29 gives 34 over all cells. From the TSV: 21-cell C-30 served = 1.471; `pick_after_window` w07 = 26, w08 = 8, weekend = 0. | OK-TSV (M9, M19) |
| 59–62 | ledger 6.217 = 82 (1,3%) + 5.353 (86,1%) + 783 (12,6%) | report.txt:30; shares 1,314 / 86,091 / 12,594% | OK |
| 66–69 | U 9,72 → 3,90; C-30 6,01 → 0,17 | report.txt:3, 8 | OK |
| 71 | U w17 6,76% "toàn bộ do điều phối"; C-30 0% | report.txt:6; TSV U w17 ledger: total 5.612,1 s, dispatch 5.679,7, anticipated 0,0, unanticipated −67,6 | OK-TSV (M9) |
| 77 | leave-one-day-out source: 0,17%, ΔY as main, A1–A3 pass | `…loo-no15….out:8,33,40` | OK |
| 78 | source with 15/11: 0,17%, as main, pass | `…with15….out:8,33,40` | OK |
| 79 | source without 12/11: 0,10% (15 cells), −6,25 [−9,28; −3,23], A1 fails structurally | `…no12….out:8,33,38,40` (only 2 w07 day-windows; `forecast_family_a.py:261` requires 3) | OK |
| 82 | cluster-t df = 2 [−18,9; +6,4] | `…no12….out:33` | OK |
| 83 | two-sided: w17 Vis 0% → 0,86%; −8,2 s | `test-family-a-two-sided…report.txt:5` | OK |
| 86 | snow Y 7,19% → 5,94%, removes 17,4% (w14 9,2%, w15 24,8%) | `test-snow…:10,12,13` | OK |
| 87 | 92,5%; 9 / 251 / 3.186 s | `test-snow…:10`; independent-B 8,566 / 251,496 / 3.185,871 | OK |
| 88 | "phân biệt đúng" 49 CFR | SOURCES:125–160 | CLAIM (M8) |

### §3 (A:90–97)

| A:line | Claim | Status |
|---|---|---|
| 92–94 | "điều làm khách bỏ đi là trễ so với giờ đã báo" (Via, Harter, Mao, Liang) | CLAIM (B5) |
| 95 | 6,01 → 0,17; 14,44 → 0,27; "khoảng nửa phút" (w07 mean 27,8 s); more stable | numbers OK; "mô phỏng tắc đường thật" wording (M16) |
| 96 | pad leaves Y at 1,94% (A3) | OK, but needs the A3 label (B3) |
| 97 | late300 = 0 in every capped arm | TSV: max late300 is 0 for C-30, Mplus-30, Mplus-60 and V-30 over 34 cells, naive and display. OK-TSV. The "10–15 phút" thresholds trace to Access-A-Ride (15 min) and the DoorDash guarantee (10 min) (SOURCES:172, 497). Acceptable. |

### §4 Family B (A:99–120)

All "family-b" references below are to `test-family-b-2026-10-06-report.txt`.

| A:line | Claim | Source | Status |
|---|---|---|---|
| 101–103 | 3 stops (which runs) | family-b:31 | OK |
| 105 | 1,67 s, bin 5 | true for the 16/11 r1 w08 cell only | partly (M4) |
| 107 | own-break ARTEFACT −69,4%, 6/6, −17,15 [−18,66; −14,92]; w07 39,0 → 11,0 | family-b:34, 41 | OK |
| 108 | forced PERSISTS 24,1%, 6/6, −15,15 [−25,52; −6,76]; w07 76,9 → 59,4 | family-b:33, 41 | OK |
| 108 | **w08 49,0% → 35,1%** | family-b:42 is computed over `ccells ∩ mcells` = 6 cells (`forecast_family_b.py:182`). On the 7 Chain M cells: 0,4660 → 0,3416 (my recompute from family-b:24–30; `independent-B/RESULT.md:108`). | **POP (B1)** |
| 110 | T3.9 "36,5% khung 7h" | `T38-REPORT.md:62` (9 w07 cells, one of them a weekend cell) | OK |
| 111 | 59,4% at w07; C-30F 0 | family-b:2–15, 41 | OK (M7) |
| 112 | H2a +0,07 [0,00; +0,19] | family-b:39 (95%; 14 cells) | OK |
| 114 | B-C1 −0,14 [−1,14; +0,24] (14 cells) | family-b:35 (97.5%) | OK |
| 115 | worst case fails | family-b:36 (−9,750 [−19,000; +0,083]) | OK |
| 117–119 | 2 C-30F stops; 17,9% vs 1,4%; w07 17,1% → 29,2%; 1,84% vs 0,25% | family-b:31, 37, 41 | OK. The original 3-condition rule (without A1's no-stop clause) would also pick the display layer, because both the visible and late conditions fail. Worth saying. |

### §5 Predictions (A:122–137)

See §4.2 below.

### §6 Limits (A:139–151)

| A:line | Claim | Status |
|---|---|---|
| 142 | stretch matches 100% of riders with dispatch part 0 | OK (report.txt:28, 2305/2305) |
| 145 | 8 day-windows, 3 mornings; 12/11 "chưa kiểm" | OK, but understates (M12) |
| 149 | 2,3% | OK (M19) |
| 150 | v1.4 "giảm được nhưng không hết dao động" | selective (M11) |
| 151 | no novelty claim; contribution = measurement plus 3-part ledger "trên tắc đường thật" | AGENTS.md boundary respected. Wording (M16). |

### §7 Independent checks (A:153–173)

| A:line | Claim | Status |
|---|---|---|
| 155 | 25/26 | OK |
| 156–160 | list of matched items | OK, but scope is narrower than implied (M13) |
| 162 | 1.452/1.452 (21 cells), 853/853 (13 cells) | OK (`independent-A/ind_oracle34_output.txt`) |
| 163–171 | Family B and snow items "Mọi mục khớp" | OK for the items listed. Independent-B did not compare the w08 line. |
| 173 | "Agent nhắc lại ba điểm …" | **omits** independent-B's PB1 observation (0,342 at w08, inside the interval), which contradicts A:126 (B2) |

---

## 4. Detailed findings and exact corrections

### 4.1 B1 + B2: wrong population for w08, and the PB1 grading

**Evidence**
- `forecast_family_b.py:182` (post-A1 version): `ws = [c for c in ccells if c.endswith(w) and c in mcells]`. For w08 this drops `d20181114-s10-r3-w08`, where C-30F stopped but Mplus-30F completed.
- family-b:24–30 per-cell Mplus forced, naive → forecast:
  - 0.321 → 0.284 (d14 r3);
  - 0.265 → 0.115;
  - 0.609 → 0.584;
  - 0.470 → 0.316;
  - 0.336 → 0.450;
  - 0.493 → 0.188;
  - 0.768 → 0.454.
- 7-cell means: **0,4660 → 0,3416**. 6-cell means (without d14 r3): 0,4902 → 0,3512, which is what family-b:42 prints.
- Own-break w08 behaves the same way: 7 cells 0,0836 → 0,0367; 6 cells 0,082 → 0,041.
- `independent-B/RESULT.md:105–108` gives the 7-cell values. Line 219 says: "PB1: … 0.342 at w08, inside [0.05, 0.35]."

**A:108, replace the last sentence with:**
> Khung 7h: 76,9% → 59,4% (8 ô); khung 8h: 46,6% → 34,2% (7 ô Mplus-30F chạy xong; trên 6 ô mà cả hai nhánh chạy xong là 49,0% → 35,1%).

**A:126 (PB1), replace with:**
> **Sai** (khung 7h bị ép 0,594 > 0,50; chuỗi M ra PERSISTS. Khung 8h 0,342 nằm trong [0,05; 0,35] trên 7 ô của chuỗi M, nên vế này đúng)

**A:173, add:**
> … và một điểm báo cáo bản trước đã ghi sai: ở khung 8h tỉ lệ bị ép của Mplus-30F trên 7 ô của chuỗi M là 0,342, nằm trong khoảng PB1 dự đoán; dòng w08 của `test-family-b-2026-10-06-report.txt` tính trên 6 ô (giao hai nhánh) do sửa đổi theo A1.

Also fix the w08 summary line in a re-issued Family B output, or annotate it. Note that this bug entered with the A1 edit (M10).

### 4.2 Prediction grading against FORECAST-PLAN.md §6

| # | Sealed text (summary) | Observed | Report grade | Reviewer grade |
|---|---|---|---|---|
| PA1 | A1 passes; Y ≤ 1,5%; ΔY ≤ −4,5 | A1 PASS; 0,17%; −5,84 | Đúng | Đúng |
| PA2 | w07 Y ≤ 3,0%; 14/11 w07 keeps the largest residual | 0,27%. TSV day-window display Y at w07: 14/11 **1,06%**, 15/11 0, 16/11 0 | Đúng | Đúng (OK-TSV) |
| PA3 | ΔE ≤ +0,5 over 21 cells; ≤ +1,0 in each window | +0,08; w07 +0,20, w08 0, w17 0 | Đúng | Đúng |
| PA4 | ext w07 15–35 s, w08 3–12 s, w17 ≤ 2 s; pickup w07 ≤ 10 s | 27,8 / 8,1 / 0,1; 6,5 | Đúng | Đúng |
| PA5 | A2 passes; Vis w07 ≤ 8%; ΔVis ≤ −2 | PASS; 1,74%; −6,42 | Đúng | Đúng |
| PA6 | A3 passes; w07 pad Y ≥ 2× display and ≥ +2 pp | PASS (but see B3); 4,24 vs 0,27 | Đúng | Đúng. Mention the A3 label. |
| PA7 | U display Y ≥ 3,0% while C-30 ≤ 1,5%; U w17 ≥ 5% | 3,90; 0,17; 6,76 | Đúng | Đúng |
| PA8 | ≥ 99% of C-30 riders with dispatch part 0 within 2 s | 2.305/2.305 (34 cells); 1.452/1.452 (21 cells) | Đúng | Đúng |
| PA9 | leave-one-day-out vs main differ ≤ 1,0 pp in display Y | 0,001693 vs 0,001693 (TSV) | Đúng | Đúng |
| PB1 | forced w07 in [0,10; 0,50], w08 in [0,05; 0,35]; PARTIAL or ARTEFACT | 0,594; **0,342** (7 cells); PERSISTS | Sai, citing w08 0,351 | **Sai**, but the w08 reason is wrong (B2) |
| PB2 | own-break w07 ≤ 0,15 | 0,110 | Đúng | Đúng |
| PB3 | C-30F visible60 above naive on 16 cells, in ≥ 4/6 day-windows | 14 cells: 0,1791 vs 0,1093. Day-windows (per-cell lines family-b:2–15): d14w07 ↑, d15w07 ↑, d16w07 ↑, d14w08 = (one cell, 0 → 0), d15w08 ↑, d16w08 ↓, so 4/6 | Đúng | Đúng, on 14 cells (M5) |
| PB4 | B-C1 passes; served difference in [−2; 0] | −0,14 [−1,14; +0,24] on 14 cells; worst case FAIL | Đúng on completed cells | Đúng có điều kiện (M5) |
| PB5 | deployment rule picks the display layer | picks the display layer | Đúng | Đúng. It also holds without A1. |
| PB6 | 0 non-normal in all 16 C-30F runs | 0 in 14 completed; 0 in the partial transcripts of the 2 stopped runs (independent-B:120) | Đúng (14) | Đúng có điều kiện (M5) |
| PB7 | C-30F w07 late60 ≤ 4% | 2,87% | Đúng | Đúng |
| PB8 | 95% interval of C-30F − Mplus-30F within [−2; +2] | [0,00; +0,19] (14 cells) | Đúng | Đúng |
| PC1 | E1/E2 show 0 differences; positive control differs | gates pass; positive control **pre-seal only** | Đúng | Đúng. Disclose the pre-seal positive control (M6). |
| PS1 | naive Y in [3; 15]%; removal ≤ 15%; ≥ 90% unanticipated | 7,19 (yes); 17,4 (**no**); 92,5 (yes) | Sai | Sai |

**Count.** 17/19 is arithmetically right: 9 PA + 7 PB + PC1. With M5 it should read "17/19 (PB4, PB6 chỉ đúng trên các ô/lần chạy xong theo phụ lục A1)". Among the genuinely falsifiable PB/PS predictions the score is 7/9, two of them conditional.

### 4.3 B5: rewrite of A:92–95

**Proposed text:**
> - Trong các nguồn đã kiểm, dữ liệu thật cho thấy trễ so với giờ đã báo **gắn với** việc khách dùng dịch vụ ít đi hoặc mua lại chậm hơn: Via New York (Cohen và cộng sự 2022: khách bị "bực bội" đi và chi ít hơn sau đó; ngưỡng bực bội 8–10 phút); giao đồ ăn (Harter 2025, Mao 2025, Liang 2025; chỉ đọc tóm tắt). Không nguồn nào cho thấy đó là lý do chính khiến khách bỏ đi, và mọi ngưỡng đo được ở mức phút, không phải 60 s.
> - Trên mô phỏng dùng một hệ số tắc đường đo thật chung cho cả thành phố, lớp lời hứa gần như xóa loại trễ > 60 s so với lời hứa đầu: 6,01% → 0,17%, khung 7h 14,44% → 0,27%. …

### 4.4 B3: label wording for A:50–51

**Proposed text:**
> **A3 ĐẠT** theo khoảng ghi nhận: −1,77 điểm %, [−2,83; −0,68]. Cluster-t cho [−3,62; +0,07], cận trên ≥ 0. Kế hoạch (§4) gắn nhãn "mong manh với suy luận ít cụm" khi một khoảng cluster-t có cận trên ≥ 0; mã niêm phong (`forecast_family_a.py:268`) chỉ xét ΔY nên in "labels: none". Báo cáo theo cách đọc chặt hơn: **A3 mang nhãn "mong manh với suy luận ít cụm"**. A1 và A2 không mang nhãn này. Nhãn "đo lường có đăng ký trước trên các ngày-khung đã được xem trước" áp cho mọi kết quả Họ A.

Also change A:96 to:
> Phần lợi không chỉ do hứa dài hơn (A3, mang nhãn mong manh với suy luận ít cụm): …

---

## 5. Document B

### 5.1 docs/18, entry 2026-10-06

| Text | Source | Status / fix |
|---|---|---|
| plan `a5524912…7337`, push `33d8071`; A1 `df7f893` | git, reflog | OK |
| "Nguồn: `docs/benchmarking/tier3-forecast/`" | folder exists and is tracked | OK. Note that it does not contain the test outputs (`test-family-*`, `test-snow`, the independent checks); those live only in the scratchpad. |
| 6,01% → 0,17%; w07 14,44% → 0,27%; ΔY −5,84 [−8,78; −3,23]; Vis 7,48% → 1,06%; ext 13,7 s (w07 27,8 s); pad 1,94% | report.txt | OK. Missing the mandatory label and the ceiling caveat (B4). |
| "A1, A2, A3 đều đạt" | report.txt:40 | add the A3 label (B3) |
| "34/1.471 lời hứa đón" | TSV | OK. Better: "34/1.471 khách (lời hứa đón đầu tiên)". |
| "khi bỏ 12/11 … suy luận mong manh (2 buổi sáng)" | no12.out:40 A1 FAIL | **hidden negative (B4)** |
| ledger: weekday mornings 1,3 / 86,1 / 12,6% | report.txt:30 | OK. The population is all 21 weekday cells, but w17 contributes 0 s. |
| snow 0,3 / 7,3 / 92,5% | independent-B:153–157 | **ROUND: 0,3 → 0,2 (0,25%)** (M2) |
| "Khớp cách phân biệt của 49 CFR …" | SOURCES | CLAIM (M8) |
| 3/32 stops "(dự báo lạc quan … ở bin có tỉ số < 1)" | failure-00.json | partly (M4) |
| 17,9% vs 1,4%; display layer chosen | family-b:37 | OK |
| M⁺ own-break −69,4% ARTEFACT; forced −24,1% PERSISTS; w07 76,9 → 59,4; C-30F 0 non-normal | family-b:33–34, 38, 41 | OK. Add "15 ô" / "14 lần chạy xong" and the knife-edge note (M7, M23). |
| 17/19 (PB1, PS1 wrong) | §4.2 | add "2 đúng có điều kiện" (M5) |
| independent Family A 25/26 | independent-A | OK. Also add independent-B (M23). |

**Proposed replacement for the Family A sub-bullet and the limits sentence:**
> Họ A (chính, không chạy mới; nhãn: đo lường có đăng ký trước trên các ngày-khung đã được xem trước; thế giới là một hệ số tắc đường chung toàn thành phố nên kết quả là mức trần cho dự báo theo giờ trong ngày, không phải hiệu quả ngoài đời): … Quy tắc niêm phong A1, A2, A3 đều đạt; A3 mang nhãn "mong manh với suy luận ít cụm" (cluster-t [−3,62; +0,07]). Giới hạn: 34/1.471 khách có lời hứa đón đầu tiên hiển thị vượt cửa sổ đón; khi bỏ 12/11 (ngày nghỉ liên bang bù Veterans Day) khỏi nguồn, A1 trượt về cấu trúc (chỉ còn 2 buổi sáng; cluster-t df = 2 [−18,9; +6,4]).

### 5.2 docs/18, entry 2026-10-02 (checked against T38-REPORT.md and rider-experience)

| Text | Source | Status |
|---|---|---|
| `323a8d9`, `19517b0`, `24aa747` | git log | OK |
| 49 episodes (60 − 11), 15 + 34, 15 + 170 jobs, 2 reruns (A1, A3) | T38-REPORT:6, 12, 92 | OK |
| γ = 222,259 s; β + γ = 252,3 s; 2/34 violations, both at w07; w07 covers 7/9; CP 17,4%; pass | T38-REPORT:19–37 | OK. Omits "120 s: không đạt" and "CP not below 10%" (M22). |
| C-30 = M⁺-30 = 2.338, V-30 2.336 | T38-REPORT:46 | OK |
| "chênh mỗi ô trong ±0,25 khách" | T38-REPORT:43–45, 82, 110 | **misstated (M21)**. Fix: "chênh trung bình mỗi ô, kể cả khoảng 95%, trong ±0,25 khách; lớn nhất ở một ô là 2 khách". |
| 0 non-normal in 49 C-30 jobs; −96 riders vs U (2,6%) | T38-REPORT:47, 49 | OK |
| 7/8 predictions; independent check 13/13 | T38-REPORT:67; `t38/independent/RESULT.md:82–94` (13 rows) | OK |
| "mọi thước đo phía khách … như nhau (2.332 khách chung)"; "giảm một nửa số khách thấy giờ trả đổi > 1 phút" | rider-experience:111, 20 | OK in aggregate. 2.332 is C-30 vs M⁺-30 only; w07 drop is only about 10% (M22). |

### 5.3 docs/18, entry 2026-10-01 (checked against W07X-REPORT.md)

| Text | Source | Status |
|---|---|---|
| `aa98fa5` pushed before any job; 104/104 pass | W07X-REPORT:3–6, 14; git | OK |
| C vs M⁺ differ only at 90 s (1/8 cells) and 120 s (2/8), +1 rider each; equal at 150–240 s | W07X-REPORT:24–28, 32 | OK |
| mechanism: traffic 61–79 s + one dispatch shift 61–68 s | W07X-REPORT:58–59 | OK |
| 6/8 predictions; independent check 7/7 | W07X-REPORT:35, 77 | OK |

No correction needed for this entry.

### 5.4 docs/19 §32

| Row | Status |
|---|---|
| seal and push | OK (hashes verified; §2) |
| T3.8 H3 (30 tests, 11/11 mutants) | traced to `T38-PLAN.md:95`; no saved test log. Acceptable. |
| T3.9 H2 (interval ±0,25 within [−2; 2]; 0/49; 13/13) | OK |
| reuse of old runs (E1/E2) | OK. The stripped-field list matches `compare_equivalence.py:7–9, 33–40`. It strips **all** `*Hash` keys, not only run-salted ones; A:20 says "trường có muối", so mention that too. |
| display layer: 23 tests, 8/8; 7 synthetic tests; A1–A3 pass; 25/26 | test counts not logged (M25); A3 label (B3); path (M24) |
| ledger: identity on 7 runs; 2.305/2.305 | `contract-split-check-2026-10-06.txt` has 7 runs (OK). It labels itself "exploratory feasibility check, not a thesis result". Reword 2.305 (M24). |
| closed loop: 3/32 stops; display layer chosen | OK |
| known limits | OK. Includes the ceiling caveat that docs/18 lacks. |

---

## 6. Claim-boundary check (AGENTS.md, docs/03)

- **No novelty claims** for dynamic insertion, ETA limits, reassignment, route similarity, least-commitment, time consistency or user satisfaction: complied with in A:151 and docs/18. A:151 calls the "công cụ chia trễ ba phần" a contribution. That is acceptable only as a measurement contribution: Via and 49 CFR already separate controllable from uncontrollable delay (SOURCES:28, 284). Suggested wording: "đóng góp là phép đo (không phải tuyên bố mới)".
- **Money, complaints, revenue:** A:97 explicitly disclaims them. Good.
- **User satisfaction / real-world effect:** B5 is the only overreach. A:143's "mức trần … không phải hiệu quả ngoài đời" is good and should be repeated in docs/18 (B4).
- **Causality:** within a deterministic paired replay, "gỡ hai nguyên nhân khác nhau" (A:71) is an accounting identity from the ledger, so it is acceptable. "Dự báo nên nằm ở lớp lời hứa" (A:120) needs a scope (M17).

## 7. Files used for recomputation (read-only)

- `test-family-a-causal-no15-2026-10-06-cells.tsv`, `…loo-no15…-cells.tsv`, `…with15…-cells.tsv` and `…no12…-cells.tsv`: means by window and population, served, oracle, `pick_after_window`, U ledger, late300, leave-one-day-out, and the stratified bootstrap re-implemented from `FORECAST-PLAN.md` §4.
- `test-family-b-2026-10-06-report.txt` lines 2–30: w08 means on 6 and on 7 cells; PB3 day-windows.
- `C:\RideBoundData\research\tier3-forecast-v1\{C-30F-d20181114-s10-r3-w08, C-30F-d20181116-s10-r1-w08, Mplus-30F-d20181116-s10-r1-w08}\failure-00.json`: failureMessage.
- `git log` and the reflog of `research/tier3-no-worse` in `C:\Code\RideBound-noworse`; `sha256sum` of the plan files and blobs.
