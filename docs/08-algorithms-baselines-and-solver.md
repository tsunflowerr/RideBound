# Thuật toán, baseline và chiến lược solver

## 1. Nguyên tắc so sánh

Baseline và RideBound phải dùng chung:

- event stream;
- travel-time snapshots;
- vehicle/request initial state;
- candidate generator;
- hard physical constraints;
- solver backend/version;
- compute/time budget;
- deterministic seed và tie-break.

Khác biệt chính chỉ là commitment mechanism. Nếu RideBound được thêm nhiều candidate hoặc thời gian solve hơn, so sánh không còn cô lập tác động.

## 2. Baseline registry v1

| Mã | Policy | Vai trò |
|---|---|---|
| B0 | `bego-static-current` | Bối cảnh sản phẩm cũ; không phải đối chứng online chính |
| B1 | `rolling-cost` | Rolling insertion/reoptimization, chỉ hard service constraints |
| B2 | `rolling-penalty` | Phạt revision trong objective nhưng không có hard cumulative budget |
| B3 | `fixed-freeze-horizon` | Khóa thay đổi trong một khoảng thời gian cố định trước pickup |
| B4 | `no-reassignment` | Sau accept không đổi vehicle; route suffix vẫn có thể đổi |
| B5 | `least-commitment-consensus` | Multiple-plan/consensus khi triển khai khả thi |
| C1 | `ridebound-hard-vector` | RideBound hard vector budget |
| C2 | `commit-soft-hard-hybrid` | Warning/penalty trước, hard gate tại limit |

Kết quả chính bắt buộc so C1 với B1. B2–B5 là mechanism baselines.

### 2.1. B1 implemented boundary sau WP2

WP2 đã cài B1 correctness baseline bằng exhaustive pickup/drop insertion trên
mutable suffix và exhaustive fleet selection cho instance nhỏ:

- exact mode không âm thầm truncate; bounded mode stable-cap và giữ no-op;
- mọi candidate qua independent physical validator, selected candidate được
  kiểm lại trước publish;
- tối đa accepted count, tối thiểu checked integer route cost, rồi candidate-ID
  ordinal tie-break;
- incumbent accepted không reassign trong WP2;
- test-only oracle độc lập khớp feasible set/cost/outcome trên 32 deterministic
  seeds trong published bound tối đa 2 vehicle/2 pending request;
- default Runner online dùng B1, còn `--mode conformance` chỉ giữ Q1 oracle.

Đây không phải scale/performance evidence. B1 WP2 chưa tính promise delta,
không prune commitment budget và không phát commitment certificate. Các phần đó
bắt đầu ở WP3; C1/OR-Tools vẫn thuộc WP4.

### 2.2. WP3 commitment boundary sau `RB-WP3-001..014`

- schedule candidate và promise cùng gọi `RouteScheduleProjector`;
- `PromiseProjector` dựng promise active rider, gồm carry-forward realized pickup
  cho onboard;
- `PromiseDeltaCalculator` tính riêng exogenous/decision/visible trên đủ 10
  dimension;
- ledger immutable/versioned nằm trong pending `OnlineState` và chỉ commit cùng
  matching ACK;
- budget/phase-lock evaluator có exact witness; candidate filter early-prune và
  Runner full-fleet validator recompute trước produced certificate.

Incident/breach, independent combined validator, strict certificate và Runner
commitment mode đã executable. Tuy nhiên default online B1 vẫn là WP2 physical
baseline và `conformance` vẫn là Q1 oracle; chỉ named `commitment` mode bật hard
gate. Không gọi WP3 là C1 solver/effectiveness evidence.

## 3. Candidate plan

Mỗi candidate plan cho một vehicle gồm:

- route suffix mới;
- tập request mới được chèn;
- incumbent request bị ảnh hưởng;
- physical schedule;
- operational cost components;
- promise projection;
- ledger deltas;
- budget/lock feasibility;
- deterministic candidate ID.

Candidate generation có thể dùng:

- exhaustive insertion cho small instances;
- greedy insertion;
- request-trip-vehicle graph;
- bounded beam;
- large-neighborhood improvement.

V1 bắt đầu bằng exhaustive/bounded insertion đủ rõ để kiểm chứng, sau đó mới scale.

## 4. Candidate generation pipeline

1. Freeze executed/locked prefix.
2. Chọn vehicle khả dĩ bằng capacity và lower bound.
3. Enumerate pickup/drop insertion positions.
4. Recompute schedule.
5. Prune physical infeasibility.
6. Compute promise/ledger delta.
7. Với RideBound, prune hard budget/lock.
8. Stable-sort và cap theo rule chung.
9. Đưa candidate còn lại vào assignment solver.

Để công bằng, B1 vẫn tính delta cho metric nhưng không prune theo commitment.

## 5. Mô hình chọn candidate

Cho `P_v` là tập candidate plan của vehicle `v`. Biến:

\[
x_{v,p}\in\{0,1\}
\]

Chọn đúng một plan, kể cả no-op:

\[
\sum_{p\in P_v}x_{v,p}=1
\]

Mỗi request mới được serve tối đa một lần:

\[
\sum_{v,p:i\in p}x_{v,p}\le 1
\]

Candidate đã kiểm feasibility cục bộ; solver kiểm thêm:

- không trùng request;
- fleet/global policy;
- reassignment consistency;
- compute/resource constraints nếu có.

Nếu reassignment incumbent giữa xe được cho phép, candidate model phải biểu diễn removal/addition atomically; v1 có thể khóa reassignment trong giai đoạn đầu để giảm độ phức tạp, nhưng phải có B4 và roadmap mở rộng.

## 6. Lexicographic solve

Khuyến nghị:

1. Tối đa accepted count.
2. Giữ accepted count tối ưu, tối thiểu worst budget utilization.
3. Giữ hai mức trên, tối thiểu total decision-induced revision.
4. Tối thiểu operational cost.
5. Stable tie-break.

Có hai cách:

- nhiều lần solve với constraint khóa optimum trước;
- scale integer weights có chứng minh dominance.

Ưu tiên nhiều lần solve để dễ audit. Nếu deadline không cho phép, dùng hierarchical bound rõ ràng và test dominance.

## 7. RideBound v1 pseudocode

```text
function Decide(context):
    state = ReduceEvents(context.previousState, context.eventBatch)
    frozen = FreezeExecutedAndLockedPrefix(state)

    candidates = GenerateCommonCandidates(state, frozen)
    evaluated = []

    for candidate in StableOrder(candidates):
        schedule = EvaluatePhysicalSchedule(candidate)
        if not schedule.feasible:
            record physical witness
            continue

        delta = ComputePromiseAndLedgerDelta(state, candidate)
        gate = CheckCommitmentBudgetsAndLocks(state, delta)
        if not gate.allowed:
            record commitment witness
            continue

        evaluated.add(candidate + schedule + delta)

    selected = LexicographicSolve(evaluated, solveBudget)
    proposed = BuildDecision(selected)
    certificate = IndependentValidate(state, proposed)

    if certificate.invalid:
        return SafeNoOpOrIncidentFallback(certificate)

    return PublishableDecision(proposed, certificate)
```

## 8. Safe fallback

Theo thứ tự:

1. Valid no-op plans giữ route cũ.
2. Greedy feasible insertion đã validator pass.
3. Reject/defer request mới.
4. Nếu state cũ đã physical infeasible do incident: incident recovery.

Không publish solver incumbent chưa qua validator.

## 9. OR-Tools

OR-Tools hiện có trong repo, nhưng RideBound phải đặt dependency trong `RideBound.Solvers.OrTools`.

V1 có thể dùng:

- CP-SAT cho candidate selection integer;
- Routing solver cho candidate generation nhỏ nếu phù hợp;
- exact enumerator riêng cho oracle nhỏ.

Mọi run ghi:

- OR-Tools version;
- deterministic settings;
- threads;
- time limit;
- status;
- best objective/bound;
- gap;
- fallback.

`FEASIBLE` không được báo thành `OPTIMAL`.

## 10. Compute budget

Hai budget tách biệt:

- decision deadline từ simulator/product;
- solver budget nội bộ.

Candidate generation và validation cũng tiêu thời gian, nên report:

- candidate generation ms;
- delta/ledger evaluation ms;
- solver ms;
- validator ms;
- total wall/process ms.

Performance runs dùng machine fingerprint và warm-up. Regression determinism có thể single-thread.

## 11. Ablation

Tối thiểu:

- bỏ cumulative ETA budget;
- bỏ switch budget;
- bỏ order dimension;
- bỏ hard lock;
- chỉ penalty, không hard gate;
- chỉ so promise hiện tại với initial promise;
- không tách exogenous/decision;
- uniform vs heterogeneous policy budget.

Ablation phải dùng cùng scenario pairs. Không cần chạy mọi ablation ở Layer 3; Layer 1/2 đủ, Layer 3 chỉ xác nhận hướng chính.

## 12. Exact-small oracle

Với instance nhỏ:

- enumerate mọi insertion/assignment hợp lệ;
- tính objective lexicographic chính xác;
- so candidate generator + solver với oracle;
- báo candidate loss và solver loss riêng.

Giới hạn `n`, số xe và horizon được chọn qua benchmark thực tế, không hard-code claim trước. Oracle nhỏ là công cụ correctness, không chứng minh scale.

## 13. Scale-up

Sau v1:

- incremental schedule evaluation;
- caching travel estimates theo snapshot;
- dominance pruning có proof/test;
- request-trip-vehicle graph;
- parallel candidate evaluation với stable merge;
- warm-start solver;
- rolling checkpoint.

Không tối ưu performance trước khi golden replay và certificate soundness pass.

## 14. Audit implementation sau WP3 và hướng tối ưu WP4

WP3 đã nối cổng cam kết vào B1 theo hai lớp:

1. `CommitmentCandidateFilter` dựng candidate state, accept đúng request mới và
   loại candidate theo physical → projection → lock → vector budget.
2. `CommitmentDecisionValidator` độc lập dựng lại toàn fleet trước publication;
   Runner chỉ stage route + promise + ledger + certificate rồi commit khi ACK đúng.

Đây là tối ưu tập khả thi thật: candidate vi phạm một trong 10 hard dimensions bị
loại trước fleet selection, còn candidate hợp lệ vẫn được B1 xếp hạng theo
accepted-count/cost. Hard gate không bị nén thành một `if` trên ETA hay một weighted
score. Exact-small oracle 16 seed và tính đơn điệu khi nới ETA limit kiểm tra tập
candidate, không chỉ kiểm tra output cuối.

Ranh giới cần nói rõ:

- B1 hiện chỉ chèn request mới và giữ nguyên thứ tự incumbent, nên
  `incumbent_order_inversion_count` bằng 0 cho candidate do B1 sinh; dimension vẫn
  được validator/delta/test thực thi để dùng cho policy có repair/reorder ở WP4.
- Request v1 có origin/destination cố định, nên relocation khác node không được B1
  sinh; calculator và distance port đã kiểm chứng nhưng cần meeting-point policy
  riêng mới kích hoạt runtime.
- O-001 khóa reassignment, nên `vehicle_switch_count` là hard-zero trong normal
  operation; không được gọi phần này là tối ưu reassignment.
- non-exact generator chỉ xét 4 pending request đầu và cap theo candidate ID, chưa
  phải best-first/dominance/slack pruning; fleet selector còn exhaustive Cartesian.
- schedule là earliest-feasible và single-plan; chưa có modified dynamic wait,
  plan pool, distinguished plan, future-potential hoặc idle-time improvement.

Các giới hạn cuối là backlog tối ưu WP4, không phải lý do nới validator WP3. WP4
phải giữ cùng physical/commitment validator, cùng canonical runner và cùng
exact-small oracle để đo candidate loss, solver loss và deadline riêng.
