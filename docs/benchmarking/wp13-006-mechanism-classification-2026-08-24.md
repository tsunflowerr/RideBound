# RB-WP13-006 — First-divergence mechanism classification

> Trạng thái: **DONE**
> Ngày: 2026-08-24
> Class: post-outcome exploratory mechanism diagnostics
> Không thay đổi H6/WP9/WP10 outcome

## 1. Kết quả

Classifier merge exact 40 behavioral records của `004` với 40 relaxation records và
41 candidate links của `005`. Evidence classes tại first divergence là:

| Evidence class | Panel A | Panel B | Tổng |
|---|---:|---:|---:|
| Recorded budget witness | 14 | 14 | 28 |
| Recorded lock witness | 3 | 2 | 5 |
| Ranking/search omission indeterminate | 3 | 4 | 7 |
| Shared selected candidate | 1 | 0 | 1 |
| Recorded physical prune code | 0 | 0 | 0 |
| Unsupported recorded prune | 0 | 0 | 0 |

Đây là **pair-level multi-label occurrences**, không phải một partition causal và
không mặc định bằng số candidate links. Một Panel-A pair chứa cả budget witness và
shared-selected evidence vì hai B1 candidates có quan hệ C1 khác nhau.

## 2. Cross-tab với immediate acceptance

| Evidence class | C1 lower | Equal | C1 higher |
|---|---:|---:|---:|
| Recorded budget witness | 7 | 21 | 0 |
| Recorded lock witness | 1 | 4 | 0 |
| Ranking/search omission indeterminate | 0 | 7 | 0 |
| Shared selected candidate | 1 | 0 | 0 |

Cả 8/8 pair có immediate C1-lower đều đồng xuất hiện recorded commitment witness:
7 budget và 1 lock. Tuy nhiên 25 pair khác cũng có recorded commitment witness trong
khi immediate accepted count bằng nhau (21 budget, 4 lock). Bảng này chỉ chứng minh
evidence co-occurrence ở epoch đầu tiên; nó không chứng minh witness gây ra completed-
service loss, không đánh giá downstream trajectory và không cho phép suy tỷ lệ
budget-vs-lock causal.

Panel A có budget lower/equal `3/11`, lock `0/3`, indeterminate `0/3`; Panel B có
budget `4/10`, lock `1/1`, indeterminate `0/4`. Không pair nào C1-higher.

## 3. Exact contract và output

- schema SHA-256:
  `060ef7d063a502752e8cd52765f2e3acdb442b1e1670f612cd4e133d1f25249d`;
- classifier SHA-256:
  `bf11f7e131f20483b1a1e78eaabdc1357e8b319d6be8800a86039612c1c8b14a`;
- canonical output: `E:\RideBoundData\wp13\mechanism-classification-set-v1.json`;
- output length/SHA-256: `44.745` byte /
  `bcc6bed3b1dd8d9c280d7a09125b6fe2e4508eb40bd47ae4da1e2c2fb9f9e9eb`.

Input identities được bind exact: behavioral report `3717f093…4f7e3`, relaxation
report `cdd9a28d…9e411`, cùng source/schema hashes. Mỗi output record bind domain hash
của behavioral comparison, relaxation record và từng candidate link. Raw list length,
40 unique panel/unit inventory, epoch/time, sign/relation và candidate uniqueness đều
fail closed.

## 4. Evidence boundary

- `recordedBudgetWitness`/`recordedLockWitness` chỉ mô tả witness đã ghi ở exact link;
- `rankingOrSearchOmissionIndeterminate` không tách được ranking, cap hay work/search
  omission vì H6 không lưu full retained portfolio;
- `sharedSelectedCandidate` không phải “candidate được cứu” và có thể đồng xuất hiện
  class khác ở cùng pair;
- candidate feasibility và downstream trajectory đều `notEvaluated`;
- confirmatory gate là `null`; kết quả `descriptiveNotCausal`, không rescue H6.

`RB-WP13-007` phải inventory covariates thực sự có trong H6 và phát explicit missing-
field report trước khi quyết định có cần Runner evidence vNext.

## 5. Verification

- targeted classification/schema/source/duplicate/mutation tests: 8/8 pass;
- full pinned CPython 3.10/FleetPy suite: 154/154 pass;
- independent verifier không import classifier: schema, exact file identities,
  canonical framing, domain hashes, 40 bindings, 41 classifications, aggregates,
  panel summaries, claim boundary và record non-nullness đều pass;
- required `dotnet test RideBound.slnx`: 856/856 pass, zero skip;
- `git diff --check` và 100-character Python line scan: pass.

Review phát hiện và sửa trường hợp raw input có 41 records nhưng duplicate thứ 41 bị
dictionary projection che mất. Final builder khóa raw length trước khi dựng unique map
và regression test giữ lỗi này fail closed.
