# WP4 — ordered ticket plan cho policies và solver

> Topic: `RB-WP4`
> Trạng thái: `COMPLETE — RB-WP4-001..014 DONE`
> Cập nhật: 2026-08-03
> Dependency: `RB-WP3-014 DONE`
> Quyết định chuẩn: ADR-023 trong `../18-status-and-decision-log.md`
> Ticket implementation hiện hành: `NONE`; handoff: `RB-WP5-001 READY`

## 1. Outcome và giới hạn

WP4 biến B1 correctness baseline thành một policy/solver stack có thể so sánh
công bằng: cùng raw physical candidate pool, cùng cap/work budget, hard gate tách
objective, diagnostics tách candidate loss khỏi solver loss và mọi output vẫn đi
qua validator/certificate/hash/ACK của WP3.

WP4 không:

- chọn budget O-002, material threshold O-003 hoặc margin O-004;
- mở reassignment O-001 hay meeting-point relocation;
- gọi B5/multiple-plan, waiting, future potential hoặc lexicographic là mới;
- dùng wall clock để đổi deterministic replay outcome;
- tích hợp BeGo/FleetPy/RidePy hoặc đưa pilot thành confirmatory evidence;
- claim effectiveness/scale trước WP5–WP9.

## 2. Quyết định đã khóa bởi ADR-023

1. B1–B5/C1/C2 nhận cùng raw physical candidate set và cap trước policy gate.
   C1/C2 chỉ được loại hard-invalid candidate; không được sinh thêm candidate.
2. `earliest-feasible` là distinguished schedule chính. Đối chứng
   `origin-hold-relocated-wait` chỉ chuyển waiting slack có thật thành waypoint
   tại current node; không bịa service time và không áp dụng ở edge progress.
3. Exact mode fail nếu phải omit. Bounded mode ưu tiên request theo
   `(latestPickup, arrivalTime, requestId)` và candidate theo accepted count,
   operational lower bound, forward slack rồi stable ID; mọi omission có count.
4. Forward-slack/cache là early pruning có điều kiện đủ, không là validator.
   Cache key bind run/vehicle/position/route/time/travel version+hash; cache miss
   khi một thành phần đổi và cached/uncached phải semantic-equivalent.
5. Repair chỉ remove/reinsert pickup+drop của waiting incumbent trong cùng xe,
   giữ frozen prefix và O-001. B4 được gọi rõ `no-reassignment-repair`.
6. B5 lưu versioned plan pool trong canonical online/checkpoint state. Chỉ
   distinguished plan được publish; alternative phải còn compatible với
   executed/frozen decisions. Pool dominance/diversity và consensus đều stable.
7. Objective dùng multi-pass lexicographic: accepted count; policy-specific hard
   utilization/warning; 10 revision dimensions theo vocabulary; operational
   cost; candidate-ID vector. Không dùng một weighted scalar tùy ý.
8. C1/C2 cùng hard gate. C2 chỉ thêm warning-excess objective trước raw revision;
   warning không thể làm hard violation feasible.
9. Solver-neutral problem/result/port nằm trong Application; policy/mapping trong
   Algorithms; `Google.OrTools` chỉ nằm trong `RideBound.Solvers.OrTools`.
10. Replay dùng deterministic work cap và CP-SAT deterministic-time limit,
    one worker/explicit seed. Wall/process time chỉ là metric. Status, bound, gap
    và fallback không được báo sai; mọi fallback phải validator-pass.
11. Published exact-small bound là tối đa 2 vehicle, 2 pending request, 1 waiting
    incumbent repair/vehicle và plan-pool cap 4; ít nhất 64 deterministic seeds.
    Infinite budgets + locks off + earliest + no repair + single plan phải bằng B1.
12. Solver/plan-pool output không tự publish: full WP3 validator, certificate,
    proposed-state hash, pending transaction và matching ACK vẫn là gate cuối.

## 3. Ordered queue

| Ticket | Kết quả chính | Trạng thái |
|---|---|---|
| `RB-WP4-001` | refinement, Browser research, ADR-023 và queue này | DONE |
| `RB-WP4-002` | solver-neutral lexicographic problem/result/diagnostics | DONE |
| `RB-WP4-003` | schedule strategy, forward slack và cache/invalidation | DONE |
| `RB-WP4-004` | bounded best-first generation và candidate-loss accounting | DONE |
| `RB-WP4-005` | B2 revision-penalty và B3 fixed-freeze baselines | DONE |
| `RB-WP4-006` | B4 same-vehicle remove/reinsert repair | DONE |
| `RB-WP4-007` | B5 canonical multiple-plan pool + distinguished/consensus | DONE |
| `RB-WP4-008` | C1 hard-vector lexicographic policy | DONE |
| `RB-WP4-009` | C2 warning/soft-hard hybrid | DONE |
| `RB-WP4-010` | OR-Tools 9.15.6755 deterministic CP-SAT adapter | DONE |
| `RB-WP4-011` | deterministic deadline, safe fallback và diagnostics | DONE |
| `RB-WP4-012` | Runner policy/publication/checkpoint integration | DONE |
| `RB-WP4-013` | independent oracle, equivalence, loss/performance evidence | DONE |
| `RB-WP4-014` | WP4 closure, promising-signal audit và WP5 handoff | DONE |

Chỉ ticket có dependency trước đó `DONE` mới chuyển `READY`; mặc định WIP một
implementation ticket.

## 4. Ticket chi tiết

### RB-WP4-002 — solver-neutral model và diagnostics

**Purpose.** Khóa model chọn đúng một plan/vehicle, request uniqueness, ordered
objective levels, deterministic budget và truthful result mà không kéo OR-Tools
vào portable core.

**Artifacts.** `Application/Optimization` interfaces/records; unit/architecture
tests cho invalid model, overflow, status/bound/gap và stable objective order.

**BDD.**

```gherkin
Given một candidate selection problem có nhiều objective level
When model được tạo
Then mỗi vehicle có ít nhất một plan và mỗi option thuộc đúng một vehicle
And request, objective, bound và work budget đều là canonical integer
```

**Acceptance.** Domain/Application không có solver package; status phân biệt
optimal/feasible/infeasible/unknown/model-invalid/fallback; full solution pass.

**Outcome 2026-08-03.** DONE. `Application/Optimization` chứa model đã
canonicalize, đúng một no-op/vehicle, request uniqueness, ordered Sum/Maximum
lexicographic levels, validated solution aggregation, deterministic
work/time/seed budget, exact rational gap, ordered diagnostics và sáu status tách
biệt. Không thêm package solver. Evidence: 20 Application cases mới, một
Architecture boundary case và required suite 435/435.

### RB-WP4-003 — schedule, forward slack và cache

**Purpose.** Giảm full schedule recomputation bằng evidence có key/invalidation,
đồng thời tạo một wait/hold control thi hành được trên route.

**Artifacts.** forward-slack profile/cache; origin-hold candidate transformer;
cached/uncached, route/position/time/travel invalidation và physical mutation tests.

**Rules.** Hold waypoint chỉ dùng current node và waiting slack đã chứng minh;
exact service equivalence phải test. Cache không được quyết định feasibility cuối.

**Outcome 2026-08-03.** DONE. Backward forward-slack profile kết hợp pickup
deadline, ride-time deadline và wait absorption nhưng chỉ phát
`CertifiedDelay`, không kết luận infeasible ở chiều ngược. Cache bind exact run
snapshot, full vehicle/position, structural route fingerprint, evaluation time,
travel version+hash; bounded clear-all tránh tăng vô hạn. Cache có thể xếp hạng
frontier nhưng không thể admit candidate; admission luôn qua physical validator.
Control origin-hold chuyển wait thật thành
waypoint ở current node, cấm edge/unexecuted frozen prefix, revalidate và so exact
service/departure/cost. Evidence: 9 Algorithms cases mới, format sạch, required
suite 444/444.

### RB-WP4-004 — bounded generation và loss accounting

**Purpose.** Thay ID-cap/four-ID request bias bằng priority/bound có thể audit và
tách omission khỏi solver error.

**Artifacts.** generation diagnostics, admissible precheck, best-first retained
set, exact fail-fast, candidate loss report và deterministic-cap tests.

**Acceptance.** Exact feasible set không đổi; bounded omission count chính xác;
C1/B1 raw pool identity equal trước hard gate.

**Outcome 2026-08-03.** DONE. Pending priority là
`(latestPickup, arrivalTime, requestId)`. Global frontier ưu tiên potential
accepted count, mandatory-service lower bound, certified slack, stable digest;
work unit là node được dequeue. Frontier còn lại được đếm tổ hợp, canonical
saturation có cờ. Retention giữ safety no-op rồi dùng accepted count, exact
operational cost, slack, ID. Diagnostics tách request-bound, unknown-feasibility
work omission và known-feasible cap omission bằng count+digest; exact fail-closed.
Evidence: 5 Algorithms cases mới, format sạch, required suite 449/449.

### RB-WP4-005 — B2/B3

**Purpose.** Thêm hai mechanism baselines không lẫn hard cumulative treatment.

**Rules.** B2 không hard-prune commitment, chỉ lexicographic material/raw revision
trước cost. B3 dùng explicit fixed horizon/locks, mọi cumulative limit unbounded.
Không có numeric default nếu config không khai.

**Outcome 2026-08-03.** DONE. Provider B2/B3 tạo lại đủ 10 limit thành
unbounded; B2 tắt freeze/final locks nhưng giữ O-001, material rule và budget
basis để đo. Assessor dùng full validator với unbounded policy, không sửa raw
pool. Selector B2 dùng accepted → material → ordered 10-vector → canonical cost
→ IDs. B3 bắt buộc horizon dương + lock mask explicit, filter đúng inclusive
boundary và không dùng source cumulative limit. Sửa vector/fleet canonical
overflow fail-closed. Evidence: 8 Algorithms + 1 Domain cases mới, 16-seed raw
pool preservation, format sạch, required suite 458/458.

### RB-WP4-006 — B4 repair

**Purpose.** Kích hoạt reorder/inversion thật bằng same-vehicle remove/reinsert,
không mở reassignment.

**Rules.** Chỉ waiting incumbent có cả pickup/drop trong mutable suffix; pair được
remove/insert nguyên tử; frozen/onboard/assignment giữ nguyên; cap explicit.

**Outcome 2026-08-03.** DONE. B4 mặc định tắt nên B1 giữ nguyên semantic. Khi
kích hoạt với cap dương, builder chỉ chọn accepted/waiting incumbent được gán
đúng vehicle, chưa onboard, có đúng một pickup/drop hoàn toàn trong mutable
suffix; remove pair rồi liệt kê mọi vị trí reinsert bảo toàn precedence, mỗi seed
chỉ sửa một pair. Exact mode fail-closed nếu cap bỏ sót incumbent; bounded mode
ghi riêng repair omission count/digest và đánh dấu diagnostics incomplete. Mọi
route sửa đều qua physical validator; selector vẫn giữ O-001 nên không thể
reassignment. Trong lúc kiểm thử phát hiện digest search node cũ sắp xếp token và
làm đồng nhất các route permutation; đã tách order-sensitive search-node digest
khỏi omission-set digest. Evidence: 7 Algorithms cases mới cho atomicity,
immutability, frozen/onboard exclusion, exact/bounded cap, B1 equivalence và lựa
chọn repair rẻ hơn; format sạch, required suite 465/465.

### RB-WP4-007 — B5 plan pool

**Purpose.** Duy trì nhiều fleet plan qua epoch/checkpoint, có một distinguished
plan và deterministic consensus control.

**Rules.** Top-K sau dominance; exact semantic plan ID; alternative incompatible
với executed/frozen distinguished decision bị loại; chỉ distinguished publish.

**Outcome 2026-08-03.** DONE. `VersionedPlanPool` là state Application immutable,
version tăng đúng một, plan ID SHA-256 bind epoch + ordered vehicle + route
version/progress/frozen/mutable/stop semantics; transient candidate/solver score
không tham gia ID. Selector enumerate một shared raw pool với explicit work cap,
exact fail-closed/bounded diagnostics; giữ đúng distinguished request assignment,
full physical + frozen validation, semantic dedup, Pareto accepted/cost/slack,
greedy max-min route diversity và executable-prefix consensus với stable tie-break.
Chỉ distinguished được apply; alternative được rebase thành
`distinguished route version + 1` để thật sự chuyển được sau publication. Pool
nằm trong canonical state/checkpoint; restore kiểm tra ID, vehicle set,
request-stop membership, physical route và distinguished-run equality. Evidence:
3 Application identity/version, 5 Algorithms dominance/diversity/consensus/work/
publication và 4 Runner round-trip/tamper/cross-layer cases; format sạch,
required suite 477/477.

### RB-WP4-008 — C1

**Purpose.** Chọn trong hard-feasible set theo accepted → worst normalized hard
utilization → stable 10-dimension decision revision → cost → ID.

**Rules.** Utilization dùng checked ceiling parts-per-million chỉ để rank; từng
hard dimension vẫn được validator kiểm exact. Infinite-budget degeneration test.

**Outcome 2026-08-03.** DONE. C1 sinh common raw pool đúng một lần rồi assessor
vừa hard-filter vừa đo trong cùng full WP3 validator pass; không double validate
hoặc sinh thêm candidate. Worst utilization lấy cumulative `BudgetAfter` của mọi
active rider theo phase/hard limit thực, exact ceiling PPM bằng `UInt128`; zero
limit/zero usage được rank là saturated 1,000,000 ppm vì không còn reserve, nhưng
PPM không tham gia feasibility. Fleet objective là accepted → worst PPM → ordered
10-dimension decision revision → canonical cost → IDs. Khi không có finite hard
limit, utilization và revision level được bỏ để semantic decision suy biến đúng
B1. Evidence: 6 Algorithms cases mới cho dominance/order, hard-filter equivalence,
canonical-max arithmetic và unbounded B1 equivalence; format sạch, required suite
483/483.

### RB-WP4-009 — C2

**Purpose.** Thêm soft warning pressure nhưng giữ cùng hard feasible set C1.

**Rules.** Warning phải explicit và không lớn hơn hard limit; rank warning excess
trước raw revision; xóa warning phải làm C2 semantic-equivalent C1.

**Outcome 2026-08-03.** DONE. Warning profile khai đúng một entry cho đủ 10
dimension; `null` tắt, ngưỡng bật phải có finite hard limit và không vượt hard.
C2 dùng chính one-pass C1 assessor nên không đổi hard-feasible set; warning excess
là ordered 10-vector cộng theo rider/vehicle bằng canonical checked arithmetic,
không trộn ms/count/mm thành weighted scalar. Objective là accepted → worst hard
PPM → warning-excess vector → decision-revision vector → cost → IDs. Nếu mọi
warning tắt, selector gọi thẳng C1 code path và output không bịa warning vector.
Evidence: 6 Algorithms cases mới cho ordering, profile shape, exact hard-set,
excess, invalid threshold và disabled equivalence; format sạch, required suite
489/489.

### RB-WP4-010 — OR-Tools adapter

**Purpose.** Solve candidate-selection model bằng CP-SAT trong project solver.

**Settings.** Package `Google.OrTools 9.15.6755`; integer-only; one worker;
explicit seed; deterministic-time limit; multi-pass objective fixing; no mapping
`FEASIBLE`/`UNKNOWN` thành `OPTIMAL`.

**Acceptance.** Exact enumerator differential, objective/bound/gap/status tests,
package/architecture audit và deterministic repeat.

**Outcome 2026-08-03.** DONE. Adapter CP-SAT nằm riêng trong solver project và
pin `Google.OrTools 9.15.6755`; model dùng BoolVar, exact-one per vehicle,
at-most-one per request, integer Sum/Maximum objective và auxiliary max variable.
Mỗi lexicographic pass dựng lại model rồi khóa đúng optimum của các pass trước;
chỉ khóa khi CP-SAT trả `OPTIMAL`. Một worker, seed, conflict budget và
deterministic-time budget đều explicit; wall time chỉ là metric. `OPTIMAL`,
`FEASIBLE`, `UNKNOWN`, `INFEASIBLE`, `MODEL_INVALID` được giữ riêng, solution
được revalidate bằng canonical Application model và bound làm tròn theo đúng
objective sense. Evidence: 5 solver cases cho constraints/objective trade-off,
acceptance priority, eight-run deterministic replay, overflow và budget/detail;
1 architecture case khóa package boundary; format sạch, required suite 495/495.

### RB-WP4-011 — deadline/fallback

**Purpose.** Tách generation/validation/solve work budget và không publish partial
incumbent chưa kiểm.

**Fallback order.** Validator-pass incumbent; valid no-op; deterministic greedy
single-request insertion; reject/defer; incident recovery chỉ khi typed incident
đang mở. WP4 không bịa incident nếu old state đã infeasible.

**Outcome 2026-08-03.** DONE. Execution budget tách generation work, semantic
validation work và solver conflict/deterministic-time; wall time không tham gia
outcome. Pre-solve accounting buộc candidate omission có lowercase SHA-256 digest
và giữ cờ saturated, nên candidate loss không bị nhập với solver loss. Mọi primary
solution (`OPTIMAL`, `FEASIBLE` hoặc fallback từ solver khác) phải qua injected
independent semantic validator. Sau đó portfolio thử canonical no-op rồi các
single-request solution theo objective lexicographic + option-ID order; mỗi lần
thử tiêu đúng một validation work unit. Hết budget hoặc không phương án nào pass
trả `UNKNOWN` không solution; không tạo incident recovery. Bound của primary được
giữ trong audit diagnostics nhưng xóa khỏi fallback result để không gắn incumbent
bound sai cho fallback. Evidence: 12 Application cases, format sạch, required
suite 507/507.

### RB-WP4-012 — Runner integration

**Purpose.** Chọn named B1–B5/C1/C2, stage plan pool/ledger/certificate/diagnostics
trên cùng state/hash/ACK/checkpoint path.

**Acceptance.** Wrong ACK/retry/checkpoint suffix; baseline metrics publication;
certificate failure không thành policy rejection; conformance/WP2/WP3 hashes giữ.

**Outcome 2026-08-03.** DONE. Canonical registry có bảy tên B1–B5/C1/C2 và
strict WP4 config khai candidate cap/work/schedule, policy-specific B3/B4/B5/C2
fields và OR-Tools stage budgets; không có numeric default ẩn. WP4 config hash
được domain-bind với commitment config hash, rồi Runner buộc manifest policy ID,
version và combined hash khớp trước tạo online state. B1–B4/C1/C2 sinh shared raw
pool một lần, map đúng objective hierarchy sang portable model, chạy OR-Tools và
full-validator mọi incumbent/fallback; B5 giữ deterministic multiple-plan path.
Runner revalidate bằng effective baseline policy, chỉ stage distinguished plan,
ledger/certificate/plan pool/state hash trong pending transaction và xuất
`completed`/`safeFallback` qua solver shell đã nằm trong decision hash. Evidence:
7 Algorithms cases cho registry + B1/B2/C1/C2/unbounded/fallback mapping; 9 Runner
cases cho strict config/binding, real OR-Tools decision, retry/wrong ACK/commit,
UNKNOWN fallback, manifest mismatch, B5 checkpoint restore và child process CLI;
format sạch, required suite 523/523.

### RB-WP4-013 — independent evidence

**Purpose.** Kiểm logic ngoài expected cases bằng oracle, properties, mutations,
bounded-loss, deterministic replay và performance curves.

**Required evidence.** 64+ seeds; exact generator/solver/final gap 0; infinite
equivalence; hard-gate killing mutation; cache equivalence; plan-pool checkpoint;
deadline fallback; OR-Tools vs enumerator; scale curve được gắn nhãn microbenchmark.

**Outcome.** DONE ngày 2026-08-03. Generator/selector B1 khớp independent
enumerator trên 64 seed; production C1 mapper + actual OR-Tools khớp một
enumerator khác trên 64 seed, trả `OPTIMAL` và exact gap 0 ở mọi objective level.
Hard-gate test chứng minh raw set lớn hơn hard-feasible set nên mutation bỏ gate
bị giết. Một bounded generation thực tạo request omission rồi truyền count/digest
đến execution diagnostics tách khỏi solver `UNKNOWN`/safe fallback. Existing
evidence giữ cache equivalence/invalidation, infinite C1=B1, plan-pool
checkpoint/tamper và deadline fallback. Synthetic microbenchmark 4/16/32/128
Boolean options đều exact `OPTIMAL`; p50 quan sát lần lượt 2.389/12.160/21.406/
91.004 ms trên máy audit. Đây là đường cong máy-local, không phải scale hay
effectiveness claim. Required suite: 557/557.

### RB-WP4-014 — closure

**Purpose.** Audit mọi ticket/requirement, đánh giá tín hiệu hứa hẹn nhưng không
nâng claim, tạo review dễ đọc và đúng một refinement WP5.

**Artifacts.** ADR closure/status/traceability/gate report; folder review WP4 có
changed-file map, flow, code walkthrough, trade-off và phần chưa chứng minh.

**Outcome.** DONE ngày 2026-08-03. ADR-024 đóng WP4 sau audit source/test/config/
Runner transaction; review mới `docs/reviews/wp1-wp4-final/` giải thích lại toàn
bộ WP1–WP4, paper-to-code, từng file quan trọng, failure paths và claim boundary.
Debug 557/557, Release warning-as-error, format, vulnerability, JSON, Markdown,
child-process và diff gates pass. Tín hiệu hứa hẹn chỉ gồm exact-small agreement,
gap 0 và synthetic latency curve; paired Layer 1/2 chưa chạy. Chỉ refinement
`RB-WP5-001` được READY, chưa có WP5 implementation ticket.

## 5. Exit gate

- [x] B2–B5/C1/C2 có behavior/test riêng và registry có tên.
- [x] Common candidate/compute boundary và loss accounting executable.
- [x] Forward slack/cache/hold/repair/plan pool có equivalence/invalidation tests.
- [x] OR-Tools status/bound/gap/deadline/fallback truthful.
- [x] Infinite-budget/locks-off exact-small semantic equality với B1.
- [x] Không invalid decision nào được publish; WP3 validator/certificate vẫn độc lập.
- [x] Required full solution, Release build, format, package, schema/link/diff gates pass.
- [x] Review WP4 giải thích code và kết luận signal không overclaim.

## 6. Rollback

Mỗi policy/solver được chọn bằng named configuration. Khi một mechanism fail gate,
Runner quay về B1 + WP3 validator/certificate; không rewrite ledger/checkpoint đã
publish. Cache/hold/repair/pool/OR-Tools đều có đường disable riêng và exact
enumerator/no-op vẫn là oracle/fallback.
