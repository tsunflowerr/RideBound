# WP3 — ordered ticket plan cho ledger và certificate

> Topic: `RB-WP3`
> Trạng thái: `COMPLETE — 14/14 ticket DONE`
> Cập nhật: 2026-08-02
> Dependency: `RB-WP2-012 DONE`
> Quyết định chuẩn: ADR-021 trong `../18-status-and-decision-log.md`
> Ticket implementation WP3 hiện hành: `NONE`; handoff duy nhất là `RB-WP4-001 READY`

## 1. Outcome và giới hạn

WP3 thêm vào B1 physical một lớp commitment độc lập, có thể dựng lại promise,
tính ba loại delta, kiểm vector budget/lock, ghi ledger append-only, phân tách
incident, phát certificate và checkpoint qua đúng Runner/hash/ACK của WP2.

WP3 không:

- chọn mức budget loose/medium/tight hoặc material threshold cuối cùng;
- claim least-commitment, time consistency hay dynamic insertion là mới;
- thêm C1, B2–B5, OR-Tools, adapter hoặc database;
- cho candidate chưa publish tiêu budget;
- gọi P2 là guarantee trước khi ticket `009`, `011` và `013` pass.

## 2. Quyết định đã khóa

1. Domain sở hữu promise, dimension vocabulary, policy, ledger, budget và lock
   invariant; Application sở hữu schedule/promise projection và three-way delta;
   Algorithms dùng lại projector Application; Runner chỉ map/compose.
2. Promise candidate là `PromiseProjection`; chỉ bản đã publish mới có
   `PromiseVersion`, epoch và simulation time.
3. Initial promise dùng version 1, tạo baseline với budget zero. Revision tăng
   đúng một version và chỉ append sau matching `decisionApplied`.
4. Service-order evidence dùng stable token `(requestId, stopKind, stopId)`.
   Inversion đếm cặp incumbent service token chung bị đảo; inserted-stop đếm stop
   ID mới nằm trước pickup của rider.
5. Stop relocation cần `IStopDistanceLookup`; đổi node mà thiếu khoảng cách
   canonical millimeter là lỗi có dimension, không tự đổi travel time thành
   khoảng cách.
6. `exogenous`, `decisionInduced`, `visible` được tính độc lập; không áp đẳng thức
   cộng do trị tuyệt đối.
7. Vector có đúng 10 dimension trong `07`; `null` hard limit là unbounded, `0` là
   hard zero. Profile số chỉ là input có tên, không có default giả.
8. Accepted assignment luôn khóa theo O-001. Onboard khóa vehicle/pickup;
   freeze/final-confirmation chỉ bật bằng policy flag rõ.
9. Ledger không nhúng current `decisionHash` vào state để tránh hash tự tham
   chiếu. Ledger dùng `publicationId`; decision envelope/certificate ở `010–011`
   bind publication với input/state/decision hash.
10. Incident breach là record riêng, không reset ledger và không được ghi
    `normalOperation budget satisfied`.

## 3. Ordered queue

| Ticket | Kết quả chính | Trạng thái |
|---|---|---|
| `RB-WP3-001` | refinement, ADR-021 và queue này | DONE |
| `RB-WP3-002` | promise/policy/vector Domain model | DONE |
| `RB-WP3-003` | shared route schedule + promise projector | DONE |
| `RB-WP3-004` | three-way delta engine | DONE |
| `RB-WP3-005` | append-only ledger + OnlineState/ACK boundary | DONE |
| `RB-WP3-006` | hard vector budget evaluator/witness | DONE |
| `RB-WP3-007` | phase-lock evaluator | DONE |
| `RB-WP3-008` | incident lifecycle và breach ledger | DONE |
| `RB-WP3-009` | independent commitment validator | DONE |
| `RB-WP3-010` | certificate/action contracts và schemas | DONE |
| `RB-WP3-011` | Runner atomic publication/hash/ACK integration | DONE |
| `RB-WP3-012` | canonical checkpoint/restore | DONE |
| `RB-WP3-013` | mutation/property/exact-small/replay evidence | DONE |
| `RB-WP3-014` | WP3 closure và WP4 refinement handoff | DONE |

## 4. Common rules và gate

Mọi ticket code:

- chạy `dotnet test RideBound.slnx`;
- giữ Domain/Application không phụ thuộc Contracts/framework/simulator/solver;
- không sửa BeGo/vendor/raw research artifact;
- dùng integer canonical và deterministic ordering;
- cập nhật `18`/`19` khi status, contract, metric hoặc next action đổi;
- rollback theo ticket không làm mất ledger đã publish.

Exit gate WP3 chỉ đạt sau `014` khi incident separation, checkpoint equivalence,
P1/P2/P3 property/mutation evidence và produced certificate qua Runner cùng pass.

## 5. Ticket chi tiết

### RB-WP3-001 — refinement ledger/certificate

**Purpose.** Khóa ownership, semantics, hash/ACK boundary và queue trước code.

**In scope.** Audit WP2; paper/claim boundary; ADR-021; 14 ticket; risk/rollback.
**Out.** Runtime, schema, numeric pilot profile.

**Artifacts.** `27`, tài liệu này, ADR-021, cập nhật `00/04–08/15/16/18/19/23`.

**Rules.** Chỉ một next ticket sau refinement; không nâng implementation status.

**BDD.**

```gherkin
Given WP2 chỉ commit state sau matching decisionApplied
When WP3 được refinement
Then route, promise và ledger dùng cùng pending-state transaction
And current decision hash không tự nhúng vào state hash của chính nó
```

**Acceptance/verification.** Mười quyết định trong `27` có đáp án; queue phủ đủ
deliverable/gate; link/diff sạch; `dotnet test RideBound.slnx`.

**Traceability.** R-007–R-009, R-015; F-005–F-009; N-003/N-004/N-007.
**Rollback.** Revert ADR/plan/status cùng thay đổi, không migration.

### RB-WP3-002 — promise, vector và policy model

**Purpose.** Tạo pure Domain vocabulary cho promise và budget mà không bịa profile.

**In scope.** 10 dimension stable; canonical vector; `PromiseProjection`,
`PublishedPromise`, version; policy basis/phases/material rule/lock flags.
**Out.** Projection, ledger, protocol DTO, default budget số.

**Artifacts.** `Domain/Commitments/CommitmentDimension`,
`CommitmentVector`, `CommitmentPolicy`, `RiderPromise`; Domain tests.

**Rules.** `null` limit = unbounded; zero = hard zero; policy phải khai đủ 10
dimension đúng một lần; service token giữ request/stop kind semantics.

**BDD.**

```gherkin
Given một profile test có đủ 10 dimension
When tạo vector/promise/policy
Then mọi số nằm trong canonical range
And không có dimension bị nén thành weighted scalar
```

**Acceptance/verification.** Vocabulary/order exact; invalid version/vector/policy
fail; initial promise type chưa tiêu budget; Domain suite pass.

**Traceability.** R-007/R-008; F-005/F-006; N-001/N-009.
**Rollback.** Xóa types/tests trước khi có wire/state migration.

### RB-WP3-003 — shared schedule và promise projector

**Purpose.** Dùng một schedule projection cho candidate và commitment.

**In scope.** `RouteScheduleProjector`; refactor candidate evaluator gọi projector;
project active rider promise; carry realized pickup cho onboard.
**Out.** Delta, ledger append, candidate budget pruning.

**Artifacts.** `Application/Scheduling`, `Application/Promises/PromiseProjector`,
adapter mỏng trong `CandidateScheduleEvaluator`; Application/Algorithms tests.

**Rules.** Current position, remaining edge integer ceiling, pickup wait và current
travel snapshot là input; projector không gọi map/network/wall clock.

**BDD.**

```gherkin
Given cùng run, route, position và travel snapshot
When candidate evaluator và promise flow dựng schedule
Then cả hai dùng cùng RouteScheduleProjector
And onboard promise giữ pickup đã thực hiện, chỉ project drop còn lại
```

**Acceptance/verification.** Node/edge cases deterministic; pickup window honored;
missing arc fail; existing 45 Algorithms tests không đổi kết quả.

**Traceability.** R-003/R-008; F-004/F-005; N-001/N-002.
**Rollback.** Trả candidate evaluator về implementation cũ và xóa projector trước
khi ticket `004+` phụ thuộc.

### RB-WP3-004 — three-way delta

**Purpose.** Tính riêng exogenous, decision-induced và visible delta.

**In scope.** ETA/material, vehicle, stop relocation/switch, incumbent inversion,
pre-pickup insertion; distance port; exact failure witness.
**Out.** Budget mutation, incident exemption, metric aggregation.

**Artifacts.** `PromiseDeltaCalculator`, `IStopDistanceLookup`, tests tất cả
dimension và trường hợp `visible != exogenous + decision`.

**Rules.** So sánh `old→exo`, `exo→new`, `old→new` độc lập; relocation dùng mm;
material rule là input có tên; no hidden approximation.

**BDD.**

```gherkin
Given ETA cũ 10, exogenous 11 và candidate 10.5
When tính three-way delta
Then exogenous là 1, decision là 0.5 và visible là 0.5
And engine không cộng hai delta đầu để suy visible
```

**Acceptance/verification.** Mọi dimension có positive/boundary test; đổi node
thiếu distance trả đúng dimension; permutation order deterministic.

**Traceability.** R-007/R-008; F-005/F-006; N-001/N-003.
**Rollback.** Xóa calculator/port/tests; schedule projector vẫn dùng được.

### RB-WP3-005 — append-only ledger và atomic state boundary

**Purpose.** Ghi lịch sử promise immutable và đặt nó trong transaction ACK của WP2.

**In scope.** Initial entry version 1/zero; revision exact-next; budget before/after;
publication/source/reason; immutable history; `OnlineState.Commitments`; reducer
preserve và coordinator stage/ACK.
**Out.** Incident entries, certificate, Runner serialization/hash integration.

**Artifacts.** `CommitmentLedger`; `OnlineState`/reducer/coordinator changes; P1
conservation, stale version và ACK tests.

**Rules.** Candidate chưa publish không append; round-trip không refund; unique
publication/version; current decision binding để `011`, không tạo hash vòng.

**BDD.**

```gherkin
Given ledger pickup ETA đã tiêu 4 và candidate quay về ETA cũ với delta 4
When append revision sau matching ACK
Then cumulative là 8
And state trước ACK cùng history cũ vẫn bất biến
```

**Acceptance/verification.** Initial zero; P1 addition exact; stale version fail;
pending ledger không xuất hiện trong committed state; full regression pass.

**Traceability.** R-007/R-008; F-005/F-006; N-003/N-004.
**Rollback.** Vì chưa có Runner publication, bỏ field/state/tests và ledger types;
không rewrite history production.

### RB-WP3-006 — hard vector budget gate

**Purpose.** Kiểm từng dimension độc lập với exact witness, không mutate ledger.

**In scope.** Phase applicability; zero/unbounded; checked addition; stable witness
order; monotonic feasible-set property khi nới limit.
**Out.** Candidate orchestration, incident override, certificate.

**Artifacts.** `CommitmentBudgetEvaluator` và dimension/property tests.

**Rules.** Equality với limit pass; một-over fail; unbounded không tạo utilization
giả; overflow fail; evaluator không append.

**BDD.**

```gherkin
Given before 7, delta 4 và hard limit 10
When budget evaluator chạy
Then reject với before=7 delta=4 after=11
And dimension là vocabulary machine-readable chính xác
```

**Acceptance/verification.** Boundary cho cả 10 dimension; hard zero; unbounded;
441 cặp before/delta chứng minh nới 20→40 không loại case đã pass.

**Traceability.** R-007/R-009; F-006/F-007; N-003.
**Rollback.** Xóa evaluator/tests, ledger/model giữ nguyên.

### RB-WP3-007 — phase-lock gate

**Purpose.** Áp O-001, onboard, freeze horizon và final confirmation trước budget.

**In scope.** Assignment always locked; onboard pickup; explicit freeze/final flags;
stable dimension/rule witness.
**Out.** Executed-prefix physical check, incident override, Runner integration.

**Artifacts.** `CommitmentLockEvaluator` và lifecycle/boundary tests.

**Rules.** Executed prefix vẫn do physical validator; `WaitingPickup` là final
confirmation event hiện hành; không có default freeze horizon.

**BDD.**

```gherkin
Given rider onboard và candidate đổi pickup ETA
When lock evaluator chạy
Then reject bằng pickup_eta_ms/onboard
And thay đổi drop ETA vẫn chuyển sang budget gate
```

**Acceptance/verification.** Accepted reassignment, onboard, exact horizon và
confirmation flag tests; Domain suite pass.

**Traceability.** R-007/R-009; F-007; N-003.
**Rollback.** Xóa evaluator/tests; physical O-001 của WP2 vẫn giữ.

### RB-WP3-008 — incident lifecycle và breach ledger

**Purpose.** Cho safety fallback có record riêng mà không giả budget satisfied.

**In scope.** Typed internal incident open/close; affected riders; breach entry;
normal-operation separation; no reset/refund.
**Out.** Product incident UX, external persistence, solver fallback policy.

**Artifacts.** Domain incident state/breach record; Application reducer; tests.

**Rules.** Chỉ event rõ danh tính mở/đóng incident; breach không dùng revision entry
bình thường; close incident không xóa history.

**BDD.**

```gherkin
Given road closure làm old plan physical-infeasible
When safe fallback vượt budget trong incident đang mở
Then append breach với source event và affected riders
And normalOperation=false, cumulative history không giảm
```

**Acceptance/verification.** Unknown/duplicate/stale incident tests; forced breach;
close/replay; full solution.

**Traceability.** R-009; F-008; N-003/N-004.
**Rollback.** Feature path chưa publish có thể revert; đã publish chỉ disable new
incident decisions, không delete records.

### RB-WP3-009 — independent commitment validator

**Purpose.** Dựng lại physical/promise/delta/lock/budget từ full before-state.

**In scope.** Compose physical validator + projector + delta + lock + budget; exact
witness order; proposed ledger transition validation.
**Out.** Wire certificate, solver objective, incident optimization.

**Artifacts.** `CommitmentDecisionValidator`; mutation tests cho mọi dimension.

**Rules.** Không tin schedule/cost/delta/balance do candidate gửi; không mutate;
physical/lock trước budget; reject commitment có ít nhất một witness.

**BDD.**

```gherkin
Given candidate báo delta zero nhưng route đổi pickup ETA
When independent validator dựng lại từ before-state
Then dùng delta dựng lại, không dùng số candidate gửi
And reject nếu budget thực vượt
```

**Acceptance/verification.** P1/P2 core evidence; mutation kill từng check; stable
witness; no framework dependency.

**Traceability.** R-007–R-009; F-007; N-003.
**Rollback.** Giữ lower-level model/ledger, gỡ orchestration validator.

### RB-WP3-010 — certificate và action contracts

**Purpose.** Phát DTO/schema versioned có witness, không tạo hash tự tham chiếu.

**In scope.** Produced certificate body; witness/breach/action payload; strict
schema/codec; input/state/publication binding; compatibility.
**Out.** Runner decision production, persistence, UI.

**Artifacts.** Contracts types, JSON Schemas, fixtures và codec tests.

**Rules.** Certificate body không chứa current decision hash trong hash projection;
containing decision envelope bind body; produced chỉ khi validator đã chạy.

**BDD.**

```gherkin
Given validator pass normal operation
When encode certificate v1
Then body bind input/state/publication IDs và exact counts
And decision envelope hash bind toàn certificate body
```

**Acceptance/verification.** Strict unknown/null/range/version tests; golden
witness; schema inventory; tamper changes decision hash.

**Traceability.** R-009; F-005/F-007/F-008; N-001/N-009.
**Rollback.** Giữ `notProduced` schema path; remove new optional/union branch trước
khi release.

### RB-WP3-011 — Runner atomic publication

**Purpose.** Chạy validator, map promise/certificate, hash và ACK trong một path.

**In scope.** B1 candidate commitment prune; initial/revision staging; state
canonicalizer gồm ledger; certificate produced; exact retry; matching ACK commit.
**Out.** Checkpoint, OR-Tools/C1, adapter.

**Artifacts.** Runner composition/mappers/canonicalizer và online transcript tests.

**Rules.** Retry cùng batch trả exact bytes, không append hai lần; wrong ACK không
commit; no-op/exogenous-only revision semantics rõ; WP1 conformance giữ nguyên.

**BDD.**

```gherkin
Given produced decision có promise publication và ledger append đang pending
When nhận wrong ACK rồi exact retry rồi matching ACK
Then wrong ACK không commit, retry byte-identical
And matching ACK commit route/promise/ledger đúng một lần
```

**Acceptance/verification.** Two-epoch process replay; hash tamper; state before/
after; certificate produced; Debug/full relevant Release evidence.

**Traceability.** R-003/R-007–R-009; F-005–F-008; N-001/N-004/N-009.
**Rollback.** Runner flag quay về WP2 `notProduced`; không xóa ledger committed.

### RB-WP3-012 — checkpoint/restore

**Purpose.** Khôi phục exact online/ledger/hash state không replay toàn lịch sử.

**In scope.** Canonical checkpoint content/version/hash; restore validation;
pending-state prohibition; equivalence/conflict.
**Out.** Database/blob persistence và adapter transport.

**Artifacts.** Application checkpoint model/codec boundary, Runner command và tests.

**Rules.** Chỉ checkpoint committed state; gồm run/travel/ledger/sequence/previous
decision hash; version conflict fail; không đọc database trong core.

**BDD.**

```gherkin
Given checkpoint sau epoch k
When restore rồi chạy suffix events
Then decisions/hash bằng replay full từ genesis
And corrupted/version-conflict checkpoint bị từ chối
```

**Acceptance/verification.** Byte/canonical vectors; restore equivalence; tamper;
cross-process.

**Traceability.** F-009; N-001/N-004/N-007/N-009.
**Rollback.** Disable checkpoint input; replay genesis vẫn canonical.

### RB-WP3-013 — independent evidence bundle

**Purpose.** Đóng property/mutation/oracle/replay evidence cho P1/P2/P3.

**In scope.** Independent exact-small enumerator; random deterministic histories;
all dimension mutations; incident separation; checkpoint replay; tiny bundle.
**Out.** Scale/performance, pilot, effectiveness claim.

**Artifacts.** Test-only oracle, source-controlled scenario/results và runner script.

**Rules.** Oracle không gọi production validator/generator; publish bound/seeds;
không gọi demo là experiment.

**BDD.**

```gherkin
Given exact-small histories trong published bound
When production và independent oracle kiểm candidate sets
Then feasible/rejected/witness outcome bằng nhau
And nới budget không mất candidate từng feasible
```

**Acceptance/verification.** P1/P2/P3; mutation score for required checks; two clean
processes exact hash; dependency/format/vulnerability gates.

**Traceability.** R-007–R-009/R-018; F-005–F-009; N-001/N-003/N-007.
**Rollback.** Revert evidence assets/tests, không đổi production state.

### RB-WP3-014 — closure và WP4 handoff

**Purpose.** Audit exit gate, giới hạn claim và tạo đúng một refinement WP4.

**In scope.** Gate report, ADR closure, traceability/status/index, risk, count,
environment exceptions, WP4 refinement ticket.
**Out.** C1/solver implementation.

**Artifacts.** `00/15/16/18/19/23/28`, closure ADR và task WP4 refinement.

**Rules.** Chỉ complete khi incident/checkpoint/P1–P3/certificate process evidence
đủ; không chọn O-002/O-003; không claim effectiveness.

**BDD.**

```gherkin
Given mọi WP3 ticket implementation đã pass
When closure audit chạy
Then WP3 được ghi Complete với evidence cụ thể
And chỉ một RB-WP4-001 refinement là READY
```

**Acceptance/verification.** Full solution/build/format/schema/link/diff/dependency
audit; clean-process bundle; next action duy nhất.

**Traceability.** R-007–R-009/R-015/R-018; F-005–F-009; N-001–N-004/N-007/N-009.
**Rollback.** Nếu gate thiếu, giữ WP3 `IN_PROGRESS`, không tạo implementation WP4.

## 6. Risk matrix

| Risk | Gate | Ticket xử lý | Rollback |
|---|---|---|---|
| Hai schedule implementation lệch nhau | shared projector + Algorithms regression | `003` | quay về adapter mỏng đã test |
| Travel time bị dùng giả làm distance | missing-distance witness | `004` | disable stop relocation dimension, không suy đoán |
| Hash tự tham chiếu | publication ID + envelope binding | `001`,`010`,`011` | giữ certificate `notProduced` |
| Candidate mutate ledger | pending/ACK/property tests | `005`,`009`,`011` | discard pending proposal |
| Profile số bị hiểu là user truth | no default; named test profiles | `002`,`006`,`014` | remove profile khỏi runtime config |
| Incident làm đẹp budget metric | breach record + separated certificate | `008`,`013` | disable incident optimization path |
| Checkpoint che divergence | genesis-vs-restore equivalence | `012`,`013` | force genesis replay |

## 7. Trạng thái đóng WP3

`RB-WP3-001..014` đã hoàn thành. Nửa sau thêm incident/breach ledger, validator
độc lập, certificate/action/schema strict, Runner commitment mode với atomic
publication/hash/ACK, canonical checkpoint/restore, property/mutation-killing/
exact-small/replay evidence và closure audit.

Các sửa lỗi tìm được khi audit toàn tuyến, không chỉ khi viết ticket cục bộ:

- candidate filter phải accept request mới trước khi project promise;
- initial online state hash phải là full canonical state;
- checkpoint bị cấm khi decision còn chờ ACK;
- parser distance phải đọc đúng `distanceMm` và cấm same-node ambiguity;
- genesis vehicle không được preload pending rider trong route;
- certificate hashes/publication IDs phải khớp containing decision/actions;
- breach budget/incident vehicle và restored breach/ledger promise phải khớp;
- pickup thực tế phải nằm trong accepted pickup window.

WP3 chứng minh cơ chế correctness trong published small bound; chưa claim scale,
effectiveness, solver optimality hoặc user satisfaction. Ticket kế tiếp duy nhất là
`RB-WP4-001` trong
[29-wp4-algorithms-solver-refinement.md](29-wp4-algorithms-solver-refinement.md).
