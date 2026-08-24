# RB-WP13-007 — H6 option-set sufficiency và missing fields

> Trạng thái: **DONE**
> Ngày: 2026-08-24
> Class: post-outcome exploratory evidence sufficiency
> Không thay đổi H6/WP9/WP10 outcome và không tự cho phép rerun

## 1. Kết quả count-only

Analyzer verify lại 80/80 raw target decisions tại exact first-divergence epoch.
Generation evidence B1/C1 **giống hệt byte-semantics ở 40/40 pair** và complete theo
recorded counters ở 80/80 arm-epoch:

| Recorded condition | Panel A | Panel B | Tổng |
|---|---:|---:|---:|
| Exact-equal B1/C1 generation pair | 20/20 | 20/20 | 40/40 |
| Generation-complete arm-epoch | 40/40 | 40/40 | 80/80 |
| Candidate-cap applied arm-epoch | 0 | 0 | 0 |
| Work-budget-exhausted arm-epoch | 0 | 0 | 0 |
| Generation omission arm-epoch | 0 | 0 | 0 |
| Selection omission arm-epoch | 0 | 0 | 0 |

Generator-retained candidate **counts** cũng bằng nhau: Panel A `254/254`, Panel B
`136/136` (B1/C1 totals). Đây không phải candidate-identity equality: H6 không ghi
full retained candidate IDs, routes hoặc schedules.

## 2. Chỗ khác nhau đã ghi

Sau generation, C1 có thêm đúng 46 commitment-pruned candidates so với B1:

| Panel | B1 pruned | C1 pruned | C1−B1 | C1 commitment candidates |
|---|---:|---:|---:|---:|
| A | 824 | 849 | +25 | 25 |
| B | 332 | 353 | +21 | 21 |
| Tổng | 1.156 | 1.202 | +46 | 46 |

Physical/untyped/mixed prune deltas đều zero. Selection đều
`optimal/validatedIncumbent`, không fallback hoặc incumbent rejection ở 80 arm-epochs.
Tuy vậy C1 consumed thêm 390 validation work units (`430` so với B1 `40`); per-pair
delta min/median/max `5/9/17`. Đây là deterministic recorded work counter, không phải
wall-clock SLA, revision burden hay causal service effect.

## 3. Vì sao bảy links vẫn `notRecorded`

Bảy `005` links vắng candidate identity nằm ở bảy pair (Panel A 3, Panel B 4). Exact
generation equality, zero cap/work/omission và equal retained **counts** loại được một
số aggregate omission explanations, nhưng không chứng minh B1 candidate ID cụ thể có
trong C1 retained set. H6 không ghi:

- full retained candidate identity/route/schedule portfolio;
- objective và commitment vectors cho từng retained candidate;
- ranking position của candidate không được chọn;
- route/schedule của pruned candidate;
- blocker sau witness đầu tiên hoặc feasibility sau clearance.

Vì vậy không được đổi class thành “retained nhưng thua ranking”, không được rerank và
không được tính candidate-level Pareto replay từ H6.

## 4. Evidence-vNext decision

Verdict là `requiredForCandidateLevelPortfolioAndReplayQuestions`. Verdict này chỉ mở
`RB-WP13-008` để thiết kế/version retained-portfolio evidence cho thí nghiệm exploratory
mới. Nó ghi rõ:

- `authorizesExploratoryRerun: false`;
- `h6BackfillProhibited: true`;
- H6/WP9 outcome, panels, margins và receipts vẫn immutable;
- `RB-WP13-009` chỉ có thể chạy sau contract/freeze riêng.

Count-only diagnostics hiện tại không cần vNext; candidate-level reranking, route/
schedule comparison, post-relaxation feasibility và later-blocker enumeration thì cần.

## 5. Exact contract và verification

- schema SHA-256:
  `d043d8141771e0e763d83f4d8860738701f18ab7d2023bec0705214249e0d634`;
- analyzer SHA-256:
  `85cf42e99fedfc6ac22b97961b6d0a3b4c219a21ab7fdf9f922ec7f06555eb75`;
- canonical output: `E:\RideBoundData\wp13\option-set-sufficiency-set-v1.json`;
- output length/SHA-256: `221.925` byte /
  `d71c669bb6da0648ccb9c5a6eaa16d990a152a9ebcd7bf0246b0b251a4037258`.

Verification:

- targeted schema/conservation/version/shape/binding/inventory mutations: 10/10 pass;
- full pinned CPython 3.10/FleetPy suite: 164/164 pass;
- independent verifier không import analyzer quét lại 80 raw targets tới EOF và
  reconcile schema, exact files, domain hashes, 40 records, deltas, panels, verdict và
  record non-nullness: pass;
- required `dotnet test RideBound.slnx`: 856/856 pass, zero skip;
- `git diff --check` và 100-character Python line scan: pass.

Review siết raw-list count trước map, nested physical/commitment binding, candidate-ID
cross-report equality, generation conservation, cap/work flags, digest và duplicate
vehicle/candidate failure paths trước khi artifact cuối được phát.
