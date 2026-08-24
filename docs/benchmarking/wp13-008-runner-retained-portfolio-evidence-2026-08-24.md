# RB-WP13-008 — Runner retained-portfolio evidence vNext

> Ngày khóa: 2026-08-24  
> Trạng thái: `Done`  
> Phạm vi: instrumentation contract; không simulator run, không H6 backfill

## 1. Kết quả

Runner có profile opt-in `retained-portfolio-v1`. Khi profile vắng, audited solver
evidence tiếp tục dùng v1.1.0 và không có field mới. Khi profile bật, Runner phát
v1.2.0 với một `candidatePortfolio` đầy đủ, deterministic và được bind vào decision
hash/configuration hash.

Portfolio ghi:

- exact generated physical candidates trước policy gate;
- exact policy-eligible subset bằng option set của `CandidateSelectionProblem`;
- exact candidate IDs cuối cùng được chọn, gồm cả no-op không thể suy ra từ action;
- candidate/vehicle/new-request identity, full immutable route và remaining schedule;
- policy eligibility, ordered objective levels và exact per-option contributions;
- không ghi objective vector giả cho candidate đã bị policy prune.

Đây là evidence contract cho exploratory execution tương lai. Không có H6 artifact,
margin, panel, result, budget, cap, ranking hay policy behavior nào bị sửa.

## 2. Fail-closed và tương thích

Config reject profile lạ, profile trên non-solver path, hoặc profile khi base solver
evidence chưa bật. Snapshot reject duplicate/missing/non-identity subset,
cross-vehicle candidate và selected IDs không tạo thành exact feasible solution.

Decoder giữ v1.0.0/v1.1.0 lịch sử, cấm `candidatePortfolio` ở hai version đó, yêu cầu
portfolio ở v1.2.0 và kiểm strict nested fields/types/enums/order. Nó còn reconcile:

- declared/generated/eligible counts với records;
- unique globally ordered candidate IDs;
- mỗi vehicle có đúng một eligible no-op;
- selected IDs eligible, vehicle-ordered, request-disjoint;
- eligible request IDs thuộc exact solver problem;
- objective contribution count khớp objective levels;
- route stop identity/request binding và remaining-route/schedule equality;
- canonical nonnegative integers và ordered schedule times.

Policy-pruned candidate được phép mang request chưa xuất hiện trong solver problem;
đây là trường hợp hợp lệ vì problem chỉ chứa post-policy eligible options.

## 3. Capture-on/off differential

Cùng bootstrap input được chạy qua hai Runner session sạch, một session chỉ bật base
evidence và một session bật thêm retained portfolio. Status, reason, state-before,
state-after, solver status và ordered operational actions giống nhau. Khác biệt chỉ
nằm ở configuration/decision hash và evidence payload đã chủ ý version hóa.

Default path không copy portfolio. Bản sao phòng vệ của request/schedule collections
chỉ được tạo khi profile bật; exact solver problem được giữ từ model mapping đã dùng,
không recompute objective trong mapper.

## 4. Source identity

| Thành phần | Byte | SHA-256 |
|---|---:|---|
| strict portfolio schema | 5.674 | `1e1592507343a2c868bdf5901aac39c3f1a41e768f54c37f262b31fc3f1bbe81` |
| snapshot/decision model | 11.761 | `56b647e70bdd7101532b6f9d4e92006293d89173c768ba0fa7832970809a365e` |
| exact solver-problem handoff | 22.476 | `d633d0e2ef137307a52caf824987bbcb8cce70372bb207cfa2e0b705b36f5ef7` |
| policy capture path | 21.210 | `2e963b86c3b5f40eaf976a325f33ef80a45b0192d79f346241590801bfd8a400` |
| strict protocol decoder | 60.359 | `22dd15af5947d7b03b02163a9ee36fa9478696e1c2d139dd829b60ba8fa0d0c8` |
| Runner config | 24.567 | `b550c14c82a2521131a78b6aeaecd4eb446dac1ce311eedb7393c5662dc06e54` |
| evidence mapper | 25.071 | `75996041dc33b2d6439ea4fc116d9a172ee1a31afb95b009b61a7b3147d06027` |
| independent JSON Schema mutations | 5.104 | `2f0d94bf643390d0c97169bf0885dc2312cc0ae50b3df03297c9e3c17ea983a7` |

Không tạo external result artifact cho ticket này vì simulator execution và H6
backfill nằm ngoài scope.

## 5. Review theo file và verification

Review thủ công đã lần theo từng file thay đổi của ticket: model snapshot, solver
mapping, policy capture, config decoder, evidence mapper, protocol decoder, schema và
bốn test surfaces. Review tìm và sửa hai gap trước closure: nested collections ban đầu
chưa được defensive-copy; selected no-op IDs ban đầu chưa được ghi. Review cũng sửa
validator để không đòi request của policy-pruned candidate phải thuộc post-policy
solver problem.

Verification cuối:

- targeted `.NET` config/snapshot/mapper/contract differential + mutations: 4/4;
- targeted Draft 2020-12 schema/mutation suite: 4/4;
- required `dotnet test RideBound.slnx`: 860/860, zero skip;
- full pinned CPython 3.10/FleetPy suite: 168/168, zero skip;
- `dotnet format --verify-no-changes`: 0 file cần format;
- `git diff --check` và Python 100-character scan: pass;
- không có generated WP13 `pyc`, không có write trong H6 roots.

## 6. Claim và queue boundary

Ticket này chỉ chứng minh khả năng ghi evidence đầy đủ và backward-compatible. Nó
không chứng minh relaxation cải thiện service, không đánh giá Pareto frontier và không
cho phép cứu kết quả confirmatory H6. `RB-WP13-009` chuyển `Ready` để refine một freeze
exploratory mới; không được chạy replay trước khi inputs, arms, paired units, failure
treatment, artifact hashes và descriptive-only analysis được khóa riêng.
