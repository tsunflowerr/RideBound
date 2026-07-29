# WP2 — Online state và rolling baseline: execution plan

> Topic ID: `RB-WP2`
> Trạng thái: `IN_PROGRESS — RB-WP2-001..006 DONE`
> Cập nhật: 2026-07-29
> Ticket tiếp theo: `RB-WP2-007`
> Exit gate: WP2 online baseline / một phần Q2 core correctness

## 1. Outcome

WP2 biến protocol/replay oracle của WP1 thành một baseline online B1 có state
thật, nhưng chưa có commitment ledger. Kết thúc topic phải có:

- state machine của run, request và vehicle bằng kiểu Domain thuần;
- reducer áp ordered event batch nguyên tử;
- route tách frozen/executed prefix và mutable suffix;
- physical validator cho capacity, pickup window, max ride time, precedence,
  connectivity, onboard/accepted preservation và frozen prefix;
- B1 `rolling-cost` sinh/chọn insertion xác định, luôn có no-op plan;
- exact-small oracle độc lập và differential/property tests;
- tiny CLI demo có replay xác định;
- bằng chứng `accepted` không quay lại `rejected`;
- Q1 protocol/hash/transcript oracle vẫn xanh;
- không có budget, ledger, commitment certificate hoặc OR-Tools.

## 2. Non-goals

WP2 không:

- tính promise delta, total variation hoặc switch budget;
- phát commitment certificate hợp lệ;
- cài incident breach accounting/checkpoint của WP3;
- cài C1, B2–B5, lexicographic commitment objective hoặc OR-Tools của WP4;
- tích hợp BeGo, FleetPy hoặc RidePy;
- tối ưu scale trước khi exact-small correctness pass;
- cho incumbent request đã accept đổi vehicle;
- gọi Q1 `notProduced` shell là online decision.

## 3. Quyết định kiến trúc đã khóa

### 3.1. Ownership

| Thành phần | Owner | Quy tắc |
|---|---|---|
| Wire schema, event payload, decision action | `RideBound.Contracts` | Versioned DTO/schema; không chứa invariant |
| Run/request/vehicle/route state | `RideBound.Domain` | Kiểu và transition thuần; không tham chiếu Contracts |
| Ordered internal events và reducer orchestration | `RideBound.Application` | Chỉ tham chiếu Domain; batch apply nguyên tử |
| Contract → internal-event mapping | boundary trong `RideBound.Runner` | Không đưa `EventBatchPayload` vào Domain/Application |
| Candidate generation/B1 selection | `RideBound.Algorithms` | Chỉ tham chiếu Application/Domain |
| Physical invariant | Domain validator + Application travel snapshot/port | Không gọi network, DB hoặc simulator |
| NDJSON lifecycle/hash/idempotency | `RideBound.Runner` | Giữ semantics WP1 |

Contract mapper chỉ chuyển đổi và kiểm provenance/unit. Nó không quyết định
accept/reject, không sửa route và không chứa thuật toán insertion.

### 3.2. State và transition

Request lifecycle WP2:

```text
Pending -> Accepted -> WaitingPickup -> Onboard -> Completed
Pending -> Rejected
Pending -> CancelledBeforeAcceptance
Accepted/WaitingPickup -> CancelledAfterAcceptance
```

Không có transition bình thường `Accepted -> Rejected`. `Deferred` là outcome
của một epoch nhưng request vẫn `Pending`. Incident/failure riêng thuộc WP3.

Vehicle state tối thiểu:

```text
vehicleId + canonical position + capacity
+ onboard/accepted request IDs
+ executed/frozen prefix + mutable suffix
+ planVersion + last applied epoch
```

Adapter là nguồn của observation ngoại sinh như position, reached stop,
boarding/alighting và travel snapshot. Core là nguồn của request decision,
assignment và mutable suffix đã publish. Snapshot ngoài không được âm thầm ghi
đè plan core.

### 3.3. Bootstrap và batch atomicity

- Vehicle lần đầu chỉ được tạo từ full `vehicleAdvanced` snapshot trong bootstrap
  batch của epoch đầu; vehicle ID lạ ở epoch sau bị từ chối.
- Travel-time snapshot đầu tiên phải có identity/hash khớp manifest trước khi
  candidate generation chạy.
- Request được tạo bởi `requestArrived`; các event sau chỉ tham chiếu ID đã biết.
- Reducer map và validate toàn batch trước, fold theo exact `eventSeq` rồi trả
  candidate state mới; một event lỗi làm cả batch không commit.
- Observed state, decision state và hash chỉ commit sau exact
  `decisionApplied`, đồng bộ lifecycle WP1.

### 3.4. Route

- Executed stop và leg đang chạy/explicitly locked tạo frozen prefix.
- Candidate chỉ được thay mutable suffix.
- Pickup phải trước drop-off; onboard rider phải còn drop-off.
- Mỗi vehicle luôn có một no-op candidate giữ nguyên suffix.
- No-op không đồng nghĩa hợp lệ nếu state đầu vào đã physical-infeasible; trường
  hợp đó trả witness, không tự bịa incident recovery.

### 3.5. O-001 — reassignment

B1 trong WP2 **không cho đổi vehicle của request đã accept**. Request pending mới
vẫn được chọn vehicle. Candidate di chuyển incumbent accepted request sang
vehicle khác bị physical/policy validator loại với reason ổn định.

Lý do:

- giữ candidate của một vehicle độc lập trong baseline correctness đầu tiên;
- tránh apply removal/addition nhiều vehicle không nguyên tử;
- không trộn mechanism stability của B4/C1 vào B1 trước khi ledger tồn tại;
- cho phép WP4 mở reassignment bằng ADR superseding, atomic multi-vehicle model
  và exact-small evidence riêng.

Capability `vehicleReassignment` vẫn tồn tại trong protocol, nhưng không là
required capability của B1 WP2.

### 3.6. B1 `rolling-cost`

Thứ tự mục tiêu WP2:

1. physical feasibility;
2. không bỏ/reject request đã accept;
3. tối đa số request pending được accept trong giới hạn instance;
4. tối thiểu integer operational cost;
5. stable tie-break theo candidate/vehicle/request ID ordinal.

B1 không kiểm commitment budget. Nó có thể tính đủ projection cần cho test
tương lai nhưng không prune candidate theo revision.

## 4. Ordered queue

| Thứ tự | Ticket | Kết quả chính | Trạng thái |
|---:|---|---|---|
| 1 | RB-WP2-001 | Refinement, ADR và ordered queue | `DONE` |
| 2 | RB-WP2-002 | Typed online input contracts và fixtures | `DONE` |
| 3 | RB-WP2-003 | Domain run/request/vehicle state machine | `DONE` |
| 4 | RB-WP2-004 | Route frozen prefix/mutable suffix và no-op | `DONE` |
| 5 | RB-WP2-005 | Contract mapper và atomic event reducer | `DONE` |
| 6 | RB-WP2-006 | Independent physical validator | `DONE` |
| 7 | RB-WP2-007 | Deterministic insertion candidate generator | `READY` |
| 8 | RB-WP2-008 | B1 rolling-cost selection và apply model | `PROPOSED` |
| 9 | RB-WP2-009 | Exact-small oracle và differential/property tests | `PROPOSED` |
| 10 | RB-WP2-010 | Runner/decision integration giữ Q1 oracle | `PROPOSED` |
| 11 | RB-WP2-011 | Tiny CLI demo và executable replay | `PROPOSED` |
| 12 | RB-WP2-012 | WP2 gate closure và handoff WP3 | `PROPOSED` |

Chỉ ticket có dependency đã `DONE` mới được chuyển `READY`. WIP implementation
mặc định là một ticket.

---

## RB-WP2-002 — Typed online input contracts và fixtures

**Trạng thái:** `DONE` — typed DTO/codecs, strict schema dispatch và
bootstrap/two-epoch fixtures đã executable ở contract/mapper boundary.

**Mục đích**

Thay `fixtureIntent` placeholder bằng payload v1 có dữ liệu đủ để dựng state
online, trước khi tạo Domain behavior.

**Trong phạm vi**

- typed contract/schema cho request, vehicle snapshot, position, route stop/plan
  và travel-time snapshot tối thiểu;
- typed payload cho các event WP2 dùng:
  `requestArrived`, hai cancellation, `vehicleAdvanced`,
  `vehicleReachedStop`, `passengerBoarded`, `passengerAlighted`,
  `travelTimesUpdated`, `timerTick`;
- valid/invalid fixtures cho bootstrap và hai epoch nhỏ;
- schema/runtime vocabulary consistency tests;
- tạo `RideBound.Application.Tests` harness rỗng tối thiểu nếu cần cho ticket sau.

**Ngoài phạm vi**

- Domain state/reducer;
- accept/reject hoặc route generation;
- incident semantics, ledger/certificate;
- đổi envelope/order/hash v1.

**Rules**

- integer canonical unit, opaque ID và exact event order giữ WP1;
- vehicle bootstrap snapshot khai báo đầy đủ planVersion, route và rider set;
- travel snapshot có version/hash và directed arc integer travel time;
- DTO không tham chiếu Domain;
- mỗi DTO có fixture/schema/test; không dùng catch-all dictionary cho field đã khóa.

**BDD**

```gherkin
Scenario: Bootstrap payload đủ dữ liệu
  Given epoch đầu có travel snapshot, một vehicle snapshot và requestArrived
  When contract boundary decode
  Then mọi ID, unit, route order và snapshot identity được giữ chính xác
  And chưa có Domain state nào bị mutate

Scenario: Vehicle snapshot thiếu mutable suffix
  Given vehicleAdvanced bootstrap không có required route state
  When payload được validate
  Then message bị từ chối bằng schema reason ổn định
```

**Acceptance criteria**

- typed bootstrap/two-epoch fixtures round-trip và canonicalize exact;
- schemas chặn unknown/duplicate/null/fraction/out-of-range field;
- Q1 10-fixture taxonomy vẫn trung thực; fixture được nâng support chỉ khi có
  runtime behavior ở ticket sau;
- `dotnet test RideBound.slnx` pass.

**Traceability:** F-001, F-002, F-003, N-001, N-009.

---

## RB-WP2-003 — Domain run/request/vehicle state machine

**Trạng thái:** `DONE` — immutable lifecycle/aggregate transitions và
accepted-never-rejected exhaustive transition matrix đã pass.

**Mục đích**

Tạo state và lifecycle thuần để reducer/algorithm không dựa vào Contracts DTO.

**Trong phạm vi**

- typed IDs/time/count/position cần cho Domain;
- `RideBoundRun`, request state và vehicle state;
- transition request/boarding/alighting/cancellation;
- ownership/provenance của observation và core decision;
- unit/property tests cho allowed/forbidden transitions.

**Ngoài phạm vi**

- route insertion, travel oracle và physical validator;
- promise/ledger/budget/certificate;
- persistence/checkpoint.

**Rules**

- Domain không tham chiếu project/package;
- request accepted không chuyển rejected;
- unknown/duplicate ID và stale planVersion không mutate state;
- state không đọc wall clock, filesystem, random hoặc environment.

**BDD**

```gherkin
Scenario: Request đã accept bị reject lại
  Given request đang Accepted hoặc WaitingPickup
  When transition Rejected được yêu cầu
  Then transition fail với witness lifecycle
  And state trước giữ nguyên

Scenario: Boarding đúng assignment
  Given request WaitingPickup đã gán đúng vehicle
  When passengerBoarded được áp
  Then request thành Onboard
  And vehicle onboard/load được cập nhật nguyên tử
```

**Acceptance criteria**

- transition table có test cho mọi cạnh cho phép/cấm;
- duplicate/stale event không tạo state kép;
- accepted-never-rejected property pass;
- Architecture tests giữ Domain độc lập.

**Traceability:** F-002, F-003, N-001.

---

## RB-WP2-004 — Route frozen prefix, mutable suffix và no-op

**Trạng thái:** `DONE` — exact prefix, implicit ordered legs, monotonic progress,
versioned suffix replacement và no-op đã có property/regression tests.

**Mục đích**

Khóa phần route đã thực thi/đang khóa và biểu diễn phần còn được chèn.

**Trong phạm vi**

- route stop/leg, pickup/drop-off role và planVersion;
- executed/frozen prefix + mutable suffix;
- advance/reached-stop transition;
- no-op plan và prefix comparison;
- route property tests.

**Ngoài phạm vi**

- tính schedule/travel time;
- candidate enumeration;
- reassignment incumbent;
- commitment phase lock của WP3.

**Rules**

- prefix frozen so sánh exact ordered identity, không chỉ count;
- pickup trước drop-off;
- onboard rider luôn có drop-off trong prefix/suffix còn lại;
- advance chỉ tăng executed progress; không rollback;
- no-op giữ byte/semantic-equivalent suffix và planVersion rule đã khóa.

**BDD**

```gherkin
Scenario: Candidate sửa stop đã execute
  Given route có frozen prefix hai stop
  When candidate thay stop thứ hai
  Then candidate bị từ chối với FROZEN_PREFIX witness

Scenario: No-op
  Given route hợp lệ và không nhận request mới
  When tạo no-op candidate
  Then frozen prefix và mutable suffix không đổi
```

**Acceptance criteria**

- prefix mutation, pickup/drop precedence, onboard drop và monotonic progress có
  property tests;
- route equality/order không phụ thuộc hash-set iteration;
- không có travel/network dependency.

**Traceability:** F-002, F-004, N-001.

---

## RB-WP2-005 — Contract mapper và atomic event reducer

**Trạng thái:** `DONE` — Runner mapper map toàn batch trước; Application reducer
trả proposed state bất biến và coordinator chỉ commit sau matching
`decisionApplied` acknowledgement.

**Mục đích**

Nối ordered `EventBatchPayload` vào internal events mà không làm Contracts rò
vào Domain/Application.

**Trong phạm vi**

- mapper tại Runner boundary;
- internal event types và Application `EventReducer`;
- bootstrap epoch, request/vehicle/travel/lifecycle event fold;
- batch-level validate-then-commit result;
- reducer replay/property tests.

**Ngoài phạm vi**

- candidate/decision;
- NDJSON retry/hash thay đổi;
- incident recovery và checkpoint.

**Rules**

- exact input order, không sort/buffer;
- map toàn batch trước khi fold;
- một event invalid làm cả proposed state bị bỏ;
- committed state chỉ đổi sau `decisionApplied`;
- reducer không nhận `JsonElement`, `EventBatchPayload` hoặc simulator type.

**BDD**

```gherkin
Scenario: Event cuối batch invalid
  Given hai event đầu hợp lệ và event cuối tham chiếu vehicle lạ
  When reducer xử lý batch
  Then trả witness tại event cuối
  And không event nào trong batch được commit

Scenario: Replay cùng batch
  Given cùng state và internal event batch
  When reducer chạy hai lần
  Then proposed state giống nhau
```

**Acceptance criteria**

- bootstrap/two-epoch fixture dựng đúng proposed state;
- mapper tests và reducer tests tách biệt;
- no partial mutation, same input/same state proof;
- Q1 runner tests vẫn pass.

**Traceability:** F-001, F-002, F-003, N-001, N-004.

---

## RB-WP2-006 — Independent physical validator

**Trạng thái:** `DONE` — validator tự dựng schedule từ state/route/travel
snapshot và trả deterministic machine witness cho mọi physical violation trong
scope.

**Mục đích**

Ngăn B1 publish route vi phạm physical/service constraints bằng validator không
tin kết quả candidate generator.

**Trong phạm vi**

- validation context dùng canonical travel snapshot;
- capacity, pickup-before-drop, pickup window, max ride time, route connectivity;
- frozen prefix, onboard drop, accepted preservation, planVersion;
- machine-readable physical witness;
- mutation/property tests.

**Ngoài phạm vi**

- commitment budget/lock/certificate;
- incident override;
- solver diagnostics.

**Rules**

- tự diễn schedule từ state/route/travel snapshot;
- không dùng cost/delta do candidate gửi như proof;
- một violation trả exact request/vehicle/stop/dimension khi có;
- accepted incumbent không được mất assignment/route.

**BDD**

```gherkin
Scenario: Drop trước pickup
  Given candidate đặt drop-off trước pickup
  When validator chạy
  Then physical feasibility false
  And witness chỉ đúng precedence violation

Scenario: Xóa accepted incumbent
  Given request đã accept còn active
  When candidate không chứa pickup/drop còn lại của request
  Then validator từ chối
  And không map thành business rejection của request cũ
```

**Acceptance criteria**

- mutation tests bắt capacity/TW/max-ride/precedence/prefix/onboard/accepted lỗi;
- valid no-op pass;
- validator không gọi DB/network/solver;
- witness deterministic.

**Traceability:** F-002, F-004; Q2 physical invariant evidence.

---

## RB-WP2-007 — Deterministic insertion candidate generator

**Mục đích**

Sinh đủ pickup/drop insertion cho instance nhỏ mà không sửa frozen prefix.

**Trong phạm vi**

- candidate ID canonical;
- enumerate vehicle, pickup và drop positions trong mutable suffix;
- schedule evaluation qua Application travel snapshot/port;
- early physical prune có witness;
- no-op candidate mỗi vehicle;
- stable order và explicit candidate cap/config.

**Ngoài phạm vi**

- global assignment solve;
- commitment delta/prune;
- OR-Tools, beam/LNS hoặc parallel optimization.

**Rules**

- B1 và policy tương lai dùng chung generator;
- không loại candidate theo promise revision;
- cùng input sinh cùng candidate ID/order;
- cap không được âm thầm mất candidate trong exact-small mode.

**BDD**

```gherkin
Scenario: Một request và route rỗng
  Given một vehicle khả thi
  When enumerate insertion
  Then có đúng pickup-before-drop candidate hợp lệ và no-op

Scenario: Insert trước frozen prefix
  Given vehicle có frozen stop
  When enumerate
  Then không candidate nào chèn trước hoặc vào prefix
```

**Acceptance criteria**

- hand-enumerated fixtures khớp count/order;
- route input không mutate;
- deterministic candidate IDs/tie order;
- physical-invalid candidate bị prune với witness, không exception.

**Traceability:** F-004, N-001.

---

## RB-WP2-008 — B1 rolling-cost selection và apply model

**Mục đích**

Chọn một tập candidate physical-feasible cho fleet theo baseline B1, chưa có
commitment constraint.

**Trong phạm vi**

- deterministic small-instance assignment enumerator;
- maximize accepted count, minimize integer operational cost, stable tie-break;
- accept/reject/defer action và proposed vehicle suffix;
- pending decision state chờ acknowledgement;
- no-reassignment incumbent enforcement.

**Ngoài phạm vi**

- budget/ledger/certificate;
- OR-Tools hoặc scale claim;
- B2–B5/C1.

**Rules**

- đúng một plan, kể cả no-op, cho mỗi vehicle;
- request mới serve tối đa một lần;
- incumbent accepted không đổi vehicle/không bị reject;
- không publish result chưa qua RB-WP2-006 validator;
- `FEASIBLE`/`OPTIMAL` vocabulary không được bịa cho enumerator.

**BDD**

```gherkin
Scenario: Hai vehicle cùng nhận được request
  Given hai insertion cùng accept count nhưng cost khác
  When B1 chọn
  Then chọn cost integer nhỏ hơn

Scenario: Tie hoàn toàn
  Given accept count và cost bằng nhau
  When B1 chọn
  Then candidate ID ordinal quyết định ổn định
```

**Acceptance criteria**

- accept/reject/defer và no-op paths có test;
- multi-vehicle request uniqueness pass;
- no reassignment/accepted-never-rejected pass;
- validator được gọi trước publish.

**Traceability:** F-003, F-004, N-001.

---

## RB-WP2-009 — Exact-small oracle và differential/property tests

**Mục đích**

Phát hiện candidate loss hoặc selection sai bằng implementation độc lập.

**Trong phạm vi**

- brute-force oracle không gọi production candidate generator/selector;
- tiny instances nhiều route/load/window;
- compare feasible set, accepted count, cost và stable outcome;
- report generator gap và selection gap riêng;
- deterministic/property seed inventory.

**Ngoài phạm vi**

- performance/scale benchmark;
- OR-Tools differential;
- commitment prune/P1–P3.

**Rules**

- oracle ưu tiên rõ/rất nhỏ hơn nhanh;
- failure in đầy đủ reproducible seed/input;
- không dùng BeGo importer làm oracle;
- không sửa expected bằng production output tự động.

**BDD**

```gherkin
Scenario: Production bỏ sót insertion khả thi
  Given oracle tìm thấy plan accept request
  And production candidate set không có plan tương đương
  When differential test chạy
  Then report generator gap

Scenario: Candidate đủ nhưng chọn sai
  Given cùng feasible set
  When production chọn cost/tie-break khác oracle
  Then report selection gap
```

**Acceptance criteria**

- canonical hand cases và generated small seeds pass;
- generator/selection gap bằng 0 trong giới hạn đã công bố;
- accepted-never-rejected và route properties chạy trên generated cases;
- runtime của oracle không bị trình bày như scale evidence.

**Traceability:** F-004, N-001; WP2 exact-small exit gate.

---

## RB-WP2-010 — Runner/decision integration giữ Q1 oracle

**Mục đích**

Đưa B1 decision thật qua lifecycle/hash v1 mà vẫn giữ oracle structural Q1 làm
regression rõ tên.

**Trong phạm vi**

- compose mapper/reducer/B1/validator ở Runner boundary;
- produced decision actions cho fixture WP2;
- pending domain state chỉ commit sau matching `decisionApplied`;
- giữ `notProduced` structural conformance path của Q1 bằng mode/handler có tên,
  không để adapter nhầm là B1;
- transcript tests cho retry, tamper, hash chain hai epoch.

**Ngoài phạm vi**

- commitment certificate `produced`;
- ledger/checkpoint/incident breach;
- adapter/simulator.

**Rules**

- default online B1 không trả `WP1_STRUCTURAL_ONLY`;
- Q1 conformance mode không được gọi là online;
- duplicate exact batch byte-equivalent; changed context/payload fatal;
- decision hash bao phủ input state và produced action deterministic;
- state/plan commit duy nhất tại `decisionApplied`.

**BDD**

```gherkin
Scenario: Decision chưa được acknowledge
  Given runner đã phát B1 decision
  When batch epoch kế tiếp đến trước decisionApplied
  Then bị từ chối
  And domain state/hash chưa advance

Scenario: Hai epoch online
  Given bootstrap/request batch rồi matching decisionApplied
  When epoch hai được xử lý
  Then previousDecisionHash bằng decision hash epoch một
  And produced route dùng state đã commit
```

**Acceptance criteria**

- Q1 exact transcript/hash test vẫn pass trong conformance path;
- WP2 two-epoch transcript produced decision/retry/apply/hash chain pass;
- certificate vẫn explicit `notProduced`;
- stdout chỉ protocol, child-process test pass.

**Traceability:** R-003, F-001–F-004, N-001, N-004, N-009.

---

## RB-WP2-011 — Tiny CLI demo và executable replay

**Mục đích**

Chứng minh B1 từ bootstrap tới accept/pickup/drop bằng artifact chạy được, không
dùng simulator hoặc data lớn.

**Trong phạm vi**

- tiny graph/travel/request manifest source-controlled;
- CLI command/mode chạy B1;
- ít nhất hai epoch, request accept và một physical reject/no-op;
- exact output/final hash và replay hai clean process;
- user-facing README giới hạn claim.

**Ngoài phạm vi**

- performance claim;
- commitment behavior;
- BeGo/FleetPy integration.

**BDD**

```gherkin
Scenario: Tiny demo chạy hai lần
  Given cùng manifest, input, seed và binary
  When hai clean process chạy
  Then action sequence và final hash giống exact golden

Scenario: Demo bị tamper
  Given một arc time hoặc request field đổi
  When replay so golden
  Then decision/final hash khác tại epoch bị tác động
```

**Acceptance criteria**

- one-command demo chạy từ clean build;
- request lifecycle/route/validator output kiểm được;
- no ledger/certificate claim;
- deterministic/tamper tests pass.

**Traceability:** R-018, F-001–F-004, N-001, N-007.

---

## RB-WP2-012 — WP2 gate closure và handoff WP3

**Mục đích**

Chỉ đóng WP2 khi online state/B1/validator/oracle/demo có evidence và tạo một
điểm bắt đầu duy nhất cho ledger/certificate.

**Trong phạm vi**

- full verification/gate report;
- status/roadmap/traceability/decision updates;
- exact test counts/artifact inventory;
- refinement ticket WP3, chưa viết ledger.

**Ngoài phạm vi**

- WP3 code;
- Q2 commitment guarantee;
- adapter/experiment claim.

**Acceptance criteria**

- lifecycle/route property tests pass;
- physical mutation tests pass;
- exact-small generator/selection gap bằng 0 trong published bound;
- accepted-to-rejected reversal bằng 0;
- tiny CLI replay/tamper pass;
- Q1 oracle và full `dotnet test RideBound.slnx` pass;
- `18` có đúng một next ticket WP3 refinement;
- docs nói rõ Q2 mới chỉ có physical/B1 phần WP2, chưa có hard budget.

## 5. Topic-level risk controls

| Risk | Dấu hiệu | Control |
|---|---|---|
| Contracts rò vào Domain | `JsonElement`/DTO trong Domain/Application | architecture + mapper boundary tests |
| Partial batch mutation | event đầu đã commit khi event cuối fail | immutable/proposed state + atomic reducer |
| Prefix bị viết lại | candidate chỉ so executed count | exact ordered prefix comparison |
| B1 âm thầm có commitment | prune vì ETA/vehicle revision | no commitment rule + differential cases |
| Reassignment nửa vời | remove/add incumbent ở hai vehicle không nguyên tử | O-001 off + validator witness |
| Generator tự xác nhận đúng | oracle gọi production generator | independent brute-force implementation |
| Q1 oracle bị overwrite | regenerate expected hash theo B1 | named conformance path + immutable vector |
| Scope creep WP3/WP4 | budget/certificate/OR-Tools xuất hiện | ticket non-goals + architecture/package audit |

## 6. WP2 exit-gate checklist

- [x] Typed WP2 input payload/schema/fixtures.
- [x] Run/request/vehicle state machine.
- [x] Frozen prefix/mutable suffix/no-op route.
- [x] Atomic event reducer không phụ thuộc Contracts.
- [x] Independent physical validator và mutation tests.
- [ ] Deterministic candidate generator.
- [ ] B1 rolling-cost selection, no incumbent reassignment.
- [ ] Exact-small differential/property evidence.
- [ ] Produced online decisions qua versioned runner lifecycle.
- [ ] Tiny CLI clean-process replay/tamper proof.
- [x] Accepted-never-rejected.
- [x] Q1 protocol/hash/transcript regression xanh.
- [x] Không có commitment ledger/certificate/OR-Tools/adapter bị làm sớm.

## 7. Lệnh bắt đầu RB-WP2-007

```powershell
Get-Content docs/18-status-and-decision-log.md -Encoding utf8
Get-Content docs/tasks/26-wp2-online-baseline-ticket-plan.md -Encoding utf8
Get-Content docs/08-algorithms-baselines-and-solver.md -Encoding utf8
rg -n "InsertionCandidate|PhysicalPlanValidator|RoutePlan" src tests
dotnet test RideBound.slnx -c Release --no-build --no-restore
```

## 8. Completion evidence RB-WP2-002..006 — 2026-07-29

- `RideBound.Contracts`: typed request/vehicle/position/route/travel DTO, strict
  event payload dispatch và 16 schema mới; placeholder `fixtureIntent` đã được
  thay bằng payload v1 có nghĩa.
- `RideBound.Domain`: run/request/vehicle aggregate, exhaustive lifecycle table,
  exact route prefix/suffix/leg/progress và independent
  `PhysicalPlanValidator`.
- `RideBound.Application`: internal events, versioned travel snapshot, atomic
  `EventReducer` và pending/ack coordinator; source không tham chiếu Contracts.
- `RideBound.Runner`: `OnlineEventMapper` là anti-corruption boundary duy nhất
  từ wire DTO sang internal event.
- Published bootstrap và epoch-two fixtures map/reduce/ack thành công; event cuối
  invalid không để lại partial state.
- Physical mutation/generated evidence bắt capacity, pickup window,
  max-ride-time, precedence, connectivity, stop location, frozen prefix,
  onboard/accepted preservation, plan version và incumbent reassignment.
- Cả Debug lẫn Release full solution pass **278/278**:
  Contracts 127, Domain 89, Application 13, Runner 42, Architecture 7.
- Release build `--warnaserror` pass với 0 warning/0 error; format verification,
  dependency vulnerability audit, 85 JSON source artifacts, 52 Markdown files,
  95 local links và `git diff --check` đều sạch.
- Chưa có candidate generator/B1 selection/online produced decision; vì vậy Q2
  và WP2 vẫn chưa đóng. Next implementation duy nhất là `RB-WP2-007`.
