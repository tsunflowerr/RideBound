# RB-WP13-005 — Recorded-witness relaxation calculator

> Trạng thái: **DONE**
> Ngày: 2026-08-24
> Class: post-outcome exploratory mechanism diagnostics
> Không thay đổi H6/WP9/WP10 outcome

## 1. Kết quả

Calculator bind 41 B1 actionful selected candidates tại exact first-divergence epoch:

| Trạng thái link | Panel A | Panel B | Tổng |
|---|---:|---:|---:|
| C1 prune có commitment witness | 17 | 16 | 33 |
| C1 cũng select candidate | 1 | 0 | 1 |
| Không có trong C1 selected/pruned evidence | 3 | 4 | 7 |
| C1 prune không có commitment witness | 0 | 0 | 0 |

33 exact links chứa 28 numeric budget witnesses, tất cả ở
`drop_eta_total_ms`, và năm categorical lock witnesses, tất cả ở
`pickup_eta_ms/final_confirmation`.

Numeric additive increase để xóa recorded witness có min/median/max
`10.128 / 93.060 / 301.765 ms`. Phân bố được khóa trước khi làm tròn:

| Additive increase | Count |
|---|---:|
| 0–10 s | 0 |
| >10–30 s | 4 |
| >30–60 s | 3 |
| >60–120 s | 12 |
| >120 s | 9 |

Đây không phải histogram của request “được cứu”: validator fail-fast nên sau khi xóa
witness hiện tại có thể lộ blocker kế tiếp. Candidate feasibility sau clearance luôn
`notEvaluated`.

## 2. Exact contract và output

- schema SHA-256:
  `7834f04e8868bf8ea673e28f5d5f4a590ab269f641a912820967bcdb8b18fb1c`;
- calculator SHA-256:
  `1ee0abdc060c8cd2d51a3ea6c1331dd059cb8a5b471fa3df6747e3ec61a5acff`;
- canonical output: `E:\RideBoundData\wp13\recorded-witness-relaxation-set-v1.json`;
- output length/SHA-256: `70.531` byte /
  `cdd9a28dd12b91253aa4f848e074d3563312bd0cc13569bc98f17898f739e411`.

Mỗi link bind candidate/vehicle/accepted requests, B1 plan action hash, C1 prune hoặc
selected-action hash, raw manifest/transcript receipts và source behavioral record.
Budget clearance kiểm `after = before + delta`, `after > limit`, rồi emit
`requiredLimit = after` và `additiveLimitIncrease = after - limit`. Lock clearance chỉ
emit categorical `disableRecordedRuleForDimension`; không tạo numeric amount giả.

## 3. Evidence boundary

Calculator tái lập exact report `004` bằng full protocol/solver scan rồi đọc raw targets
tới EOF để lấy generated candidate IDs và witnesses. Candidate vắng khỏi cả C1 selected
actions lẫn `prunedCandidates` được ghi
`absentRetainedOrOmittedNotRecorded`, không suy thành retained hay omitted vì H6 không
lưu full portfolio.

Không được dùng output này để nói:

- 33 candidates sẽ feasible hoặc 33 requests sẽ được phục vụ sau relaxation;
- tăng budget lên một con số cụ thể sẽ phục hồi completed service;
- budget/lock là causal decomposition của Panel A/B loss;
- bảy candidate vắng evidence thuộc ranking, cap hay omission class nào.

Những phân loại tiếp theo thuộc `006`; evidence sufficiency/missing fields thuộc `007`.

## 4. Verification

- targeted schema/link/arithmetic/identity mutation tests: 11/11 pass;
- full pinned CPython 3.10/FleetPy suite: 146/146 pass;
- independent schema/hash/domain-hash/arithmetic/no-null/aggregate verification: pass;
- required `dotnet test RideBound.slnx`: 856/856 pass, zero skip;
- `git diff --check` và 100-character Python line scan: pass;
- two-pass raw scan giữ H6 roots read-only.

Review phát hiện và sửa schema thiếu witness `vehicleId`, duplicate accepted request
cross-candidate, mixed physical/commitment witness và candidate/witness code mismatch.
`RB-WP13-006` là queue head Ready duy nhất.
