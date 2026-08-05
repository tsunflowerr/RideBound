# WP2 — online state, physical feasibility và B1

## Immutable state và lifecycle

`RideRequest`, `VehicleState`, `RoutePlan` và `RideBoundRun` giữ lifecycle/cross-
aggregate invariants. Assignment sau accept không tự đổi; route có executed/frozen
prefix và mutable suffix; changed route phải advance version; onboard/load/
accepted sets phải khớp request lifecycle. `Rehydrate` dựng lại typed objects rồi
kiểm toàn bộ quan hệ thay vì tin checkpoint JSON.

`EventReducer` kiểm identity/epoch/time/sequence, fold vào local state và chỉ trả
new state khi cả batch hợp lệ. Genesis cấm preload request-owned stops hoặc rider
state giả. `EventReductionCoordinator` tách committed, proposed và staged decision
state để ACK là transaction boundary duy nhất.

## Shared schedule

`RouteScheduleProjector` là nguồn tính schedule chung cho candidate và promise:

1. bắt đầu từ node hoặc phần directed edge còn lại;
2. dùng integer ceiling cho edge progress;
3. cộng directed travel + wait đến earliest pickup + service;
4. kiểm missing arc và overflow thành typed failure;
5. operational cost là elapsed route duration từ evaluation time.

Không có một schedule implementation riêng trong WP3 hay solver adapter. Đây là
điều kiện để candidate được chọn và promise được publication nói cùng một sự thật.

## Independent physical validator

`PhysicalPlanValidator` kiểm route mà không tin generator:

- version và exact frozen prefix;
- route connectivity/current node hoặc edge;
- pickup/drop đúng node, precedence và uniqueness;
- pickup window, capacity, max ride time;
- bảo tồn onboard/incumbent service;
- no reassignment theo O-001;
- overflow/missing lookup trả witness.

Generator gọi validator cho từng candidate; publication validator gọi lại sau
fleet selection. Hai lần gọi phục vụ hai trust boundary khác nhau.

## B1 generator và selector

WP2 B1 dựng pickup/drop insertion trong mutable suffix, stable hash ID, de-duplicate,
physical validate và schedule. Exact-small fail nếu request/candidate bound buộc
omit. Bounded mode luôn giữ no-op và report truncation.

`CandidateFleetSelector` enumerate một option mỗi vehicle, reject duplicate request
assignment và so hierarchy:

1. maximize accepted requests;
2. minimize checked operational cost;
3. stable candidate-ID vector.

`RollingCostPolicy` revalidate, apply routes rồi accept selected requests. Request
ngoài bound/deferred, fleet conflict và physical prune có reason class riêng.

## Claim chính xác

B1 là optimum chính xác **trong raw candidate set đã sinh**, không phải global
DARP optimum. WP4 giữ B1 semantics khi locks/treatments tắt nhưng thay production
selection bằng portable model + OR-Tools và giữ exact enumerator làm oracle.

## Evidence

- Domain 135 tests cho lifecycle, route, physical boundary và overflow.
- Application schedule/reducer/coordinator tests cho atomicity và node/edge cases.
- B1 generator/selector independent oracle hiện chạy 64 deterministic fixtures.
- WP2 tiny transcript và WP3/WP4 child-process tests giữ exact output/hash replay.
