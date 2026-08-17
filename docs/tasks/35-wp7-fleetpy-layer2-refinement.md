# RB-WP7-001 — refinement Candidate core và FleetPy Layer 2

> Trạng thái: `DONE`  
> Work package: `WP7`  
> Loại ticket: refinement + executable capability preflight; chưa phải
> confirmatory experiment  
> Dependency: WP1–WP6 Complete; ADR-014, ADR-020, ADR-022, ADR-024, ADR-026..036  
> Ticket implementation WP7 được phép trước khi ticket này DONE: `NONE`
> Decision: ADR-037; closure: ADR-038  
> Ordered queue: [36-wp7-fleetpy-layer2-ticket-plan.md](36-wp7-fleetpy-layer2-ticket-plan.md)

## 1. Outcome

Khóa decision-complete contract cho hai phần liên quan trực tiếp:

```text
bounded Candidate generation
  → common B1/C1 candidate portfolio có loss accounting
  → exact/differential quality gate

FleetPy callback/state/position
  → canonical RideBound Runner event batches
  → validated decision suffix
  → FleetPy VehiclePlan không phá locked/current leg
  → raw-output reconciliation
```

Ticket chỉ chuyển `DONE` khi có ADR, paper-to-design gate, capability matrix và
ordered implementation queue với đúng một ticket nhỏ nhất `READY`.

## 2. Non-goals

- không mở O-001 reassignment đã bị loại khỏi main comparison;
- không chọn O-002/O-003/O-004;
- không dùng native FleetPy Alonso-Mora/Simonetto làm treatment hoặc viết lại
  RideBound trong Python;
- không dùng `force_assign=True` để vượt qua locked VehicleRouteLeg;
- không gọi WP6 instant-drain là FleetPy closed-loop/effectiveness;
- không nhận heuristic paper là tối ưu nếu chưa có oracle/no-regression evidence;
- không dùng random request/vehicle order, random direction vector hoặc forecast;
- không chạy pilot/prereg/main experiment thuộc WP8–WP9.

## 3. Bằng chứng source/paper đã kiểm

1. Alonso-Mora et al. mô hình hóa RV → feasible trips → RTV → ILP; subset/RV
   pruning chỉ an toàn dưới giả định feasibility/travel phù hợp, không tự động đúng
   cho mọi directed sparse snapshot của RideBound.
2. Engelhardt–Dandl–Bogenberger giữ V2RB đã tính và tiền lọc vehicle; paper báo
   speed-up nhưng cũng mất khoảng 30% secondary objective trong cấu hình nêu ra.
   Random order/direction filter vì vậy bị loại khỏi default.
3. Zalesak et al. 2025 mô tả Weighted Set Packing, H1–H5, column generation và
   solution stability. OOF/LRP giữ thứ tự/prefix hiện tại; đây là cơ sở để thử một
   stable portfolio anchor, không phải novelty claim.
4. FleetPy tag `1.0.2` source xác nhận callback `user_request`,
   `user_confirms_booking`, `user_cancels_request`, `receive_status_update`,
   boarding/alighting, `time_trigger`, travel-time update và
   `assign_vehicle_plan`.
5. FleetPy `NetworkBase` định nghĩa position tuple ổn định
   `(start_node_id, end_node_id, relative_pos)` với `relative_pos ∈ [0,1]`;
   `SimulationVehicle._move` cập nhật trực tiếp `veh_obj.pos`. O-006 có thể đóng
   bằng executable type/range/direction preflight thay vì hạ mặc định xuống node-only.
6. FleetPy từ chối thay current locked leg trừ khi ép lock; adapter RideBound phải
   so exact active-leg destination/locked suffix và fail closed, không ép.

## 4. Candidate-core defect và design gate

Current cap xếp từng candidate theo accepted-count → cost → slack → stable ID rồi
lấy `K-1` candidate ngoài no-op. Khi một request/service set có nhiều route variants,
các variants đó có thể chiếm toàn bộ cap:

- với B1, variant đắt hơn của cùng vehicle/service set bị cost anchor thống trị và
  không thêm lựa chọn cho set-packing;
- với C1, cap chạy trước hard assessment nên có thể bỏ variant giữ incumbent prefix/
  ETA tốt hơn rồi hard gate loại các variant còn lại;
- ở fleet level, duplicate service-set variants có thể làm mất một service set bổ
  sung và giảm tổng accepted count.

Candidate change chỉ được bật cho config mới nếu đạt cả bốn gate:

1. **B1 dominance:** với cùng vehicle + exact service set, giữ cost anchor; một
   candidate legacy bị thay phải có anchor không đắt hơn và cùng conflict columns.
2. **Stability anchor:** trong từng accepted-count tier, giữ thêm route variant có
   incumbent-prefix/schedule-disruption profile tốt nhất khi cap còn chỗ; profile
   dùng integer, deterministic, không đọc outcome hoặc policy arm.
3. **Exact oracle:** exhaustive/adversarial fleet portfolios chứng minh new retained
   pool không kém legacy B1 objective và có ít nhất một strict-positive witness;
   C1 fixture phải chứng minh stable anchor sống qua real hard validator.
4. **Loss/identity:** omitted count/digest vẫn exact; strategy là config-bound enum,
   config WP1–WP6 không có field mới phải giữ legacy behavior/hash. WP7 và future
   experiment chỉ dùng strategy mới sau gate.

Nếu C1 oracle cho regression không giải thích/không khống chế được, strategy mới
không được làm default. Không thay bằng một `if` đặc thù fixture.

## 5. Quyết định WP7 phải khóa

1. exact FleetPy tag/commit/license/environment lock;
2. adapter class/module discovery và constructor contract;
3. request identity, time unit, party size, pickup/drop node mapping;
4. offer creation, rejection, booking confirmation và cancellation semantics;
5. exact thời điểm khởi tạo first promise: sau `user_confirms_booking`;
6. status/finished-leg/boarding/alighting/time-trigger ordering;
7. directed edge-progress extraction và declared fallback capability;
8. travel snapshot update, sparse/dense rule và ties-to-even milliseconds;
9. RideBound suffix → PlanStop/VehiclePlan mapping;
10. locked/current-leg equivalence, no-force assignment và typed mismatch;
11. same published Runner/runtime/config/source pre/postflight;
12. Runner process lifecycle, ACK/checkpoint/restart/failure retention;
13. raw FleetPy → WP6 observation/result reconciliation;
14. tiny/medium B1/C1 closed-loop protocol, deterministic seeds và claim boundary.

## 6. Mười capability preflight bắt buộc

1. import đúng pinned FleetPy module/class;
2. request callback + exact ID/time/node mapping;
3. offer/rejection có thể đọc lại qua FleetPy API;
4. confirmation/cancellation không phát sai initial revision;
5. status update trả finished VRL theo đúng thứ tự;
6. boarding/alighting lifecycle không double emit;
7. `veh_obj.pos` node/edge/fraction type-range-direction hợp lệ;
8. PlanStop/VehiclePlan round-trip giữ request membership/timing;
9. locked/current VRL mismatch fail closed và không gọi force assign;
10. cùng Runner hash, canonical transcript/checkpoint và raw reconciliation.

## 7. Acceptance của refinement

- Candidate paper-to-design matrix nêu rõ adopt/reject/defer và claim limit;
- mechanical B1 dominance + C1 real-validator witness được thiết kế thành test, không
  chỉ nhận xét prose;
- FleetPy capability matrix có source symbol, executable probe và failure code cho
  từng hàng;
- adapter contract giữ Domain/Application độc lập simulator và Python chỉ gọi Runner;
- environment/source pin không copy vendor tree vào repository;
- ordered `RB-WP7-002..N` có dependency, BDD, rollback và đúng một ticket `READY`;
- cập nhật `00/08/12/15/16/18/19/20/21/23` và required full solution pass.

## 8. Stop/rollback

Nếu pinned FleetPy không expose stable directed-edge progress, ghi capability
`nodeOnly`/typed downgrade; không suy diễn fraction từ clock. Nếu assignment cần
`force_assign=True`, stop và sửa mapping/clock contract. Nếu Candidate portfolio
không đạt strict-positive + no-regression gate, giữ legacy strategy và ghi negative
result. Không được giảm acceptance để tuyên bố WP7 hoàn thành.

## 9. Initial refinement evidence — 2026-08-13

- Browser audit đọc primary papers Alonso-Mora, Engelhardt et al. 2020 và Zalesak
  et al. 2025 cùng exact FleetPy tag source; random/direction filter, reassignment và
  forecast bị reject/defer đúng claim/ràng buộc.
- Source xác nhận 13 abstract callback, locked-leg behavior và exact directed edge
  position tuple; O-006 chuyển từ open prose sang executable preflight decision.
- Candidate defect được hiện thực qua `RB-WP7-002`: B1 dominance, fleet strict
  `2 → 4`, 32-seed C1 real-validator/production no-regression + strict positive,
  permutation/conservation/config legacy gates.
- Exact old configs không có `retentionStrategy` vẫn parse legacy; strategy mới chỉ
  opt-in bằng config content hash mới.
- FleetPy external clone đã xác minh tag/commit/license/env source; portable exact
  environment bootstrap là scope queue head `RB-WP7-003`.
- Format sạch; required `dotnet test RideBound.slnx` pass 776/776, WAC không tái hiện.

## 10. Final closure — 2026-08-16

ADR-038 confirms the ordered implementation queue `RB-WP7-001..014` as Done. A
current-source audit corrected B4 repair-root priority and added the introduced-request
portfolio invariant; at that closure the full .NET suite was 790/790 and pinned Python
was 49/49, on Runner v6.
Actual FleetPy preflight/lifecycle/tiny/medium B1/C1 runs use one Runner artifact per
source state and the medium bundles are independently verified. This upgrades the
mechanical Layer-2 evidence only; no effectiveness, SLA, fairness, satisfaction or
novelty result follows.

ADR-039 (2026-08-17) supersedes those receipts for current source: the suite is
798/798, pinned Python is 50/50, and every actual gate was re-run on Runner v8
`13bf5d9b…c179e`. It also records that the actual semantic hash binds `binarySha256`,
so it can never be compared across Runner artifacts.
