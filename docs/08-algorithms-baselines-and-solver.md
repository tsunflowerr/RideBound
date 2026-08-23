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
| B4 | `no-reassignment-repair` | Sau accept không đổi vehicle; route suffix có bounded same-vehicle repair |
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

Sau `RB-WP4-011`, rule này executable trong
`SafeCandidateSelectionExecutor`: primary solution luôn qua injected independent
semantic validator; fallback thử canonical no-op rồi single-request solution theo
objective/ID order. Mỗi lần thử tiêu một validation work unit. Nếu hết budget
hoặc mọi phương án bị bác, kết quả là `UNKNOWN` không solution. Solver-neutral
executor không có API tạo incident; incident recovery chỉ được Runner mở khi state
có typed incident hợp lệ.

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

Sau `RB-WP4-010`, CP-SAT adapter pin `Google.OrTools 9.15.6755`, one worker và
explicit seed/conflict/deterministic-time limits. Lexicographic pass chỉ khóa
objective trước khi pass đó đã `OPTIMAL`; status/bound giữ nguyên semantics và
solution được canonical revalidation.

Sau `RB-WP4-012`, B1–B4/C1/C2 dùng production mapper sang chính model này.
Mapper biểu diễn accepted → policy vector → cost → candidate-ID vector bằng
ordered objective levels; ID tie-break là một rank objective riêng cho từng
vehicle canonical, không dựa vào thứ tự native solver. B5 tiếp tục dùng bounded
multiple-plan enumerator vì output của nó là cả pool, không chỉ một assignment.
Strict WP4 config được bind cùng commitment config vào manifest hash và Runner
đưa `completed`/`safeFallback` vào solver shell của hashed decision.

## 10. Compute budget

Deterministic selection có ba work budget tách biệt:

- candidate generation work;
- semantic validation work;
- solver conflict/deterministic-time work.

Ngoài ra deployment có hai lớp thời gian:

- decision deadline từ simulator/product;
- solver budget nội bộ.

Candidate generation và validation cũng tiêu thời gian, nên report:

- candidate generation ms;
- delta/ledger evaluation ms;
- solver ms;
- validator ms;
- total wall/process ms.

Performance runs dùng machine fingerprint và warm-up. Regression determinism có thể single-thread.
Wall/process time chỉ là metric; deterministic work exhaustion mới được thay đổi
replay outcome. Candidate omission count/digest/saturation được report riêng với
solver status/bound để không nhập candidate loss thành solver loss.

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

## 14. Historical audit sau WP3 đã dẫn đến WP4

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
- Tại mốc WP3, non-exact generator chỉ xét request bound và cap theo candidate ID,
  chưa có best-first/slack/loss accounting; WP4 đã thay boundary production bằng
  deterministic best-first + portable solver nhưng giữ exact enumerator làm oracle.
- Tại mốc WP3, schedule là earliest-feasible và single-plan; WP4 đã thêm named
  executable origin-hold, repair và checkpointable multiple-plan pool.

Các giới hạn lịch sử không phải lý do nới validator WP3. WP4 đã giữ cùng physical/
commitment validator, canonical Runner và exact-small oracle để đo candidate loss,
solver loss và deadline riêng.

## 15. Semantics đã khóa cho WP4 bởi ADR-023

- Raw physical candidate pool/cap được tạo một lần và chia sẻ cho B1–B5/C1/C2;
  hard gate C1/C2 chạy sau common cap. Omission, physical prune, commitment prune
  và solver loss là bốn số riêng.
- Exact mode không omit. Bounded mode ưu tiên request theo deadline/arrival/ID và
  candidate theo acceptance, operational lower bound, forward slack, stable ID.
- Main schedule vẫn earliest-feasible. Wait control chỉ relocate waiting slack
  thành current-node waypoint có service duration, nên action có thể thi hành.
- B4 là same-vehicle remove/reinsert repair, không reassignment. B5 giữ versioned
  canonical pool và chỉ publish distinguished plan.
- C1 multi-pass: accepted → worst normalized utilization → 10 revision dimensions
  → cost → ID. C2 thêm explicit warning excess nhưng hard gate không đổi.
- OR-Tools chỉ nhận solver-neutral Application model. Regression dùng one worker,
  explicit seed/deterministic-time; status/bound/gap/fallback giữ nguyên nghĩa.

Chi tiết ticket, published exact-small bound và rollback nằm trong
[30-wp4-algorithms-solver-ticket-plan.md](tasks/30-wp4-algorithms-solver-ticket-plan.md).

## 16. WP4 closure evidence

`RB-WP4-001..014` và ADR-024 đã đóng các semantics trên. Production B1–B4/C1/C2
dùng shared generation, exact objective mapper, pinned OR-Tools và independently
validated fallback; B5 dùng bounded canonical plan pool và chỉ publish
distinguished plan. Runner full-validate lại rồi giữ ledger/certificate/hash/ACK
transaction của WP3.

Required suite pass 557/557. B1 generator/selector khớp independent oracle trên
64 fixtures; C1 production mapper + actual OR-Tools khớp independent enumerator
trên 64 fixtures với mọi level optimal/gap 0. Hard-gate mutation, actual bounded
loss propagation, cache/infinite-budget/plan-pool/deadline gates pass. Synthetic
4–128 Boolean-option curve chỉ là machine-local promising signal; không phải
evidence scale, service improvement hoặc user satisfaction. Final walkthrough ở
[reviews/wp1-wp4-final/README.md](reviews/wp1-wp4-final/README.md).

## 17. WP7 bounded Candidate refinement

WP7 không thay objective, hard validator hay solver boundary của WP4. Nó sửa hai điểm
trong bounded candidate stage, chỉ cho config opt-in:

- `ServiceSetStabilityPortfolioV1` giữ no-op, cost anchor cho từng exact
  vehicle/service set trong accepted-count tier, rồi deterministic stability anchor và
  legacy fill. Với B1, anchor có cùng conflict columns và cost không cao hơn plan cũ;
  đây là proof substitution có scope rõ, không phải global optimum.
- B4 repair root được priority theo route suffix sau repair. Khi work cap chặt, score
  route cũ có thể chọn root sai; regression ghi projection của từng root trước budget
  quyết định work đầu tiên.

Portfolio phải preserve exact các no-op incumbent stop và phải khai đúng tập request
introduced trong `NewRequestIds`. Điều này chặn route semantics khác bị gắn nhầm cùng
service set. C1 vẫn chạy hard-vector assessor và full decision validator; stable anchor
không tự cấp quyền publish. Evidence/code map/claim limit xem
[final WP1–WP7 review](reviews/wp1-wp7-final/01-core-candidate-and-solver.md).

## 18. Chi phí thật của bounded generation — đo ở ADR-039

Cho tới ADR-039 chưa ai đo bounded candidate stage; giả định mặc nhiên là validator tốn
kém. Số đo nói ngược lại.

| Thành phần | Chi phí mỗi lần |
|---|---:|
| `PhysicalPlanValidator.Validate` | 0,72 µs |
| `RoutePlan.ReplaceMutableSuffix` | 3,94 µs |
| `SHA256.HashData` (1,6 KB) | 4,44 µs |
| `ForwardSlackCacheKey.Create` (cũ, có fingerprint) | 19,63 µs |

Một `Generate` trên xe 16 stop thực hiện khoảng 39.000 lần tra khóa memo slack. Vì vậy
điểm nóng là **tính lại identity**, không phải suy luận khả thi. Khóa memo đã chuyển
sang so sánh cấu trúc chính xác (`0,64 µs`); khả năng phân biệt không đổi vì đó đúng là
thứ fingerprint mã hóa, và khóa này không nằm trong bất kỳ identity công bố nào.

Ràng buộc bắt buộc: mọi tối ưu ở tầng này phải giữ nguyên **toàn bộ** work unit,
evaluated path, feasible-before-cap, omitted path, retained count và số slack profile
riêng biệt. `CandidateSearchWorkProfileTests` khóa các số đó ở bốn kích thước route.
Nhanh hơn mà duyệt cây khác thì test đó fail — và đó mới là điều kiện, không phải
đồng hồ.

Hai kết luận về hướng đi tiếp:

- *Lazy priority* **không dùng được** ở thiết kế hiện tại: stop chèn có
  `ServiceDuration = 0` nên mọi insertion child đồng hạng ở cả hai key rẻ, khiến cận
  dưới phẳng trên gần như toàn frontier.
- Full constant-time feasibility test (Gschwind & Drexl 2019) vẫn **không được
  áp dụng** sau khi đã đọc full PDF. Nó exact trong model của paper nhưng chỉ phủ
  chiều thời gian; validator ở đây còn quyết capacity, connectivity, frozen prefix
  và commitment budget, còn travel snapshot chung không cam kết triangle inequality.
  ADR-052 chỉ bỏ allocation/key verification exact bị lặp; full validator và toàn
  work profile giữ nguyên. Evidence ở
  [post-WP10 optimization](benchmarking/post-wp10-exact-reuse-optimization-2026-08-23.md).

## 19. Hai lớp ràng buộc physical — khóa bởi ADR-045

`PhysicalPlanValidator` chia constraint thành hai lớp có ngữ nghĩa khác nhau.

**Structural** — `ROUTE_CONNECTIVITY`, `PRECEDENCE`, `CAPACITY`, `FROZEN_PREFIX`,
`ONBOARD_PRESERVATION`, `ACCEPTED_PRESERVATION`, `PLAN_VERSION`, `STOP_LOCATION`,
`INVALID_POSITION`, `INVALID_ROUTE_STOP`, `SCHEDULE_OVERFLOW`, `UNKNOWN_*`. Đây là
bất biến của một kế hoạch well-formed. Vi phạm là defect chứ không phải giao
thông, nên vẫn strict ở mọi call site và fail-closed như cũ. Ride time âm cũng
thuộc lớp này: đó là lỗi precedence, không phải deadline.

**Service-quality** — `MAX_RIDE_TIME` và `PICKUP_WINDOW`. Đây là lời hứa về thời
gian và có thể bị phá vỡ mà không ai quyết định gì. Chúng là ràng buộc **lúc tạo
kế hoạch**, không phải bất biến liên tục.

Cơ chế:

1. `ProbeServiceQuality(run, vehicleId, travelTimes, evaluationTime)` chiếu lộ
   trình **không đổi** của xe dưới travel snapshot hiện hành và trả
   `ServiceQualityAllowance` chứa mọi deadline nó không còn đáp ứng.
2. Bound hiệu lực cho mỗi request là `max(contractual, exogenous)`. No-op an toàn
   luôn thoả bound này nên không bao giờ bị prune vì lý do service-quality; đó là
   điều giữ cho xe không rơi về 0 candidate.
3. Vì `exogenous` đúng bằng giá trị mà *không làm gì* đã hiện thực hoá, không
   candidate nào được phép tệ hơn no-op trên dimension đang breach. Witness khi
   prune báo `Expected` là bound hiệu lực, không phải bound hợp đồng.
4. Request chưa nằm trên lộ trình đang chạy không có entry nào trong allowance,
   nên mọi request mới chèn vẫn bị enforce contractual tuyệt đối.
5. Breach được phát ra thành `ExogenousServiceQualityBreach` trong
   `CandidateGenerationDiagnostics`. Nó là diagnostic: không prune candidate nào
   và không fail epoch nào.

`ForwardSlackProfile` certify delay theo đúng bound mà validator enforce và cache
key bind digest của allowance; nếu hai bên lệch nhau, một route validator chấp
nhận vẫn có thể mất slack certificate rồi bị prune. Bốn đường re-validate
downstream — `RollingCostPolicy`, `MultiplePlanPolicy`, `CommitmentDecisionValidator`
và `OnlineStateCheckpointCodec` — dùng `ValidateWithExogenousRelief` vì lý do đó.

Allowance là hàm thuần của `(run, vehicle, travelSnapshot, evaluationTime)` và áp
dụng đồng nhất ở cả hai arm, nên nó không dịch chuyển arm nào so với arm nào.
