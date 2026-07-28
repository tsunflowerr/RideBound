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
