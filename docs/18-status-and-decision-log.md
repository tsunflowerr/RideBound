# Trạng thái và decision log

> Tệp sống — cập nhật ở cuối mọi task RideBound
> Cập nhật gần nhất: 2026-07-30

## 1. Trạng thái tổng thể

| Mục | Trạng thái |
|---|---|
| Research direction | `LOCKED_FOR_IMPLEMENTATION_PLANNING` |
| Documentation | `MIGRATED_AND_VERIFIED_V1` |
| Implementation | `WP1_Q1_COMPLETE; WP2_PHYSICAL_B1_COMPLETE_DEBUG_333_PASS_RELEASE_HOST_POLICY_EXCEPTION` |
| Current work package | `WP3 LEDGER/CERTIFICATE — RB-WP3-001 READY` |
| Repository | `https://github.com/tsunflowerr/RideBound` |
| Main baseline | B1 `rolling-cost` |
| Main treatment | C1 `ridebound-hard-vector` |
| Layer 2 | FleetPy 1.0.2 |
| Layer 3 default | RidePy v2.10.1; AMoD2 alternate |

## 2. Đã hoàn thành

- Kiểm toán kiến trúc BeGo hiện tại.
- Xác nhận `Session` không cho thay pickup sau computation.
- Xác nhận benchmark hiện tại là snapshot, không có promise history.
- Backend 25/25 test pass.
- Frontend 7/7 test pass.
- Đọc/đối chiếu evidence trực tiếp cho RideBound.
- Xác minh official repos/versions:
  - FleetPy 1.0.2 / `053aa9d...`;
  - RidePy v2.10.1 / `bf1863e...`;
  - AMoD2 / `aaa66dd...` tại ngày kiểm tra;
  - AMoDeus;
  - OpenRidepoolSimulator.
- Kiểm source extension points của FleetPy/RidePy/AMoD2.
- Tạo repository Git độc lập `E:\Code\RideBound`.
- Tạo `RideBound.slnx` với 7 source project và 2 test project.
- Khóa dependency Clean Architecture/DDD bằng architecture tests.
- Thêm `global.json`, shared build policy, `.editorconfig`, CI và README.
- Sửa architecture test để đọc `ProjectReference` dùng cả dấu phân cách Windows và
  Linux; bổ sung regression cases cho hai kiểu đường dẫn.
- Mở rộng CI thành các gate format, Release build/test/coverage, dependency audit,
  main runner artifact, Sonar Quality Gate tùy cấu hình và PR-Agent tùy cấu hình.
- Chuyển 23 tài liệu lõi cùng archive evidence liên quan sang `docs/` và tạo root `AGENTS.md`.
- Xác minh 37 tệp Markdown/6.821 dòng: 0 link nội bộ hỏng, 0 code fence lệch,
  0 dấu hiệu mojibake; kèm 3 JSON evidence machine-readable.
- Giữ nguyên cây mã nguồn `E:\Code\BeGo`; không copy `BeGo/src`.
- Xác minh WP0:
  - RideBound restore/build Release: 0 warning, 0 error;
  - RideBound tests hiện tại: 8/8 pass;
  - BeGo backend: 25/25 pass;
  - BeGo frontend: 7/7 pass.
- Hoàn thành `RB-WP1-001`: khóa schema version, unit/range, position union,
  event ordering, envelope/payload/manifest boundary, error taxonomy, canonical
  JSON/hash framing và fixture taxonomy bằng ADR-014.
- Hoàn thành `RB-WP1-002`: thêm Contracts test project, UTF-8 fixture loader và
  smoke fixture dùng chung.
- Hoàn thành `RB-WP1-003`: thêm protocol primitives, typed envelope,
  encode/decode và structural validation có reason code.
- Hoàn thành `RB-WP1-004`: thêm canonical unit conversions, canonical JSON
  byte writer và source-controlled golden byte vector.
- Hoàn thành `RB-WP1-005`: thêm machine-readable JSON Schema v1, schema inventory,
  compatibility matrix và executable version/unknown-field policy.
- Hoàn thành `RB-WP1-006`: thêm hello/helloAck contracts, capability vocabulary
  và deterministic fail-fast/named-downgrade negotiation.
- Hoàn thành `RB-WP1-007`: thêm immutable initialize manifest/initial state
  identity cùng validation chéo envelope/hello/ack không mutate.
- Hoàn thành `RB-WP1-008`: event vocabulary/payload schema và ordering validator
  cho sequence, epoch, simulation time, gap/overlap/overflow.
- Hoàn thành `RB-WP1-009`: decision/error/certificate shell; Q1 luôn ghi rõ
  `notProduced`, không phát action/certificate/solver giả.
- Hoàn thành `RB-WP1-010`: SHA-256 domain separation, tagged length framing,
  manifest/state/decision vectors và chain/tamper/order tests.
- Hoàn thành `RB-WP1-011`: async NDJSON reader/writer có UTF-8/size/EOF/LF/flush
  semantics và diagnostic tách khỏi stdout.
- Hoàn thành `RB-WP1-012`: executable long-lived runner
  `new → negotiated → initialized → awaitingDecisionApplied`, memory pipe và
  child-process integration tests.
- Hoàn thành `RB-WP1-013`: retry nguyên batch trả cached response không advance;
  conflict/overlap/gap/hash/version có đúng disposition; cache giữ một response.
- Hoàn thành `RB-WP1-014`: đúng 10 required golden fixture, full tiny transcript,
  exact expected output/final hash, replay hai lần và tamper proof.
- Hoàn thành `RB-WP1-015`: đóng Q1 bằng ADR-016, traceability/gate evidence và
  tạo ticket refinement duy nhất `RB-WP2-001`.
- Revalidate end-to-end toàn WP1: phát hiện và sửa cache idempotency từng coi
  batch đổi `simTimeMs` là retry hợp lệ; exact retry hiện hash toàn canonical
  eventBatch envelope + payload theo ADR-017.
- Bổ sung regression test cho changed-time duplicate và replay exact transcript
  qua hai runner process sạch; WP1 revalidation inventory đạt 161/161.
- Hoàn thành `RB-WP2-001`: ADR-018 khóa state/reducer/route/reassignment boundary
  và tạo ordered queue `RB-WP2-002..012` trong execution plan WP2.
- Hoàn thành `RB-WP2-002`: typed request/vehicle/position/route/travel contracts,
  strict event payload schema dispatch, thay `fixtureIntent` và thêm
  bootstrap/two-epoch fixtures.
- Hoàn thành `RB-WP2-003`: immutable Domain run/request/vehicle state machine,
  exhaustive lifecycle table và accepted-never-rejected evidence.
- Hoàn thành `RB-WP2-004`: exact frozen prefix, ordered route leg, mutable suffix,
  no-op, planVersion và monotonic reached-stop progress.
- Hoàn thành `RB-WP2-005`: Runner contract mapper, Application internal events,
  manifest-bound travel snapshot, atomic reducer và pending/ack coordinator.
- Hoàn thành `RB-WP2-006`: independent physical validator tự dựng schedule và
  deterministic witness cho capacity/window/max-ride/precedence/connectivity/
  prefix/onboard/accepted/reassignment.
- Hoàn thành `RB-WP2-007`: deterministic pickup/drop insertion generator,
  canonical candidate/stop identity, exact/bounded caps, stable ordering,
  physical prune witness và no-op retention.
- Hoàn thành `RB-WP2-008`: exhaustive B1 fleet selection tối đa accepted count,
  tối thiểu integer route cost, stable tie-break, accept/reject/defer và staged
  apply không reassign incumbent.
- Hoàn thành `RB-WP2-009`: independent exact-small brute-force oracle với 32
  published seeds; generator/selection gap bằng 0 trong bound 2 vehicle/2
  pending request.
- Hoàn thành `RB-WP2-010`: Runner default online produced typed decisions,
  full state/action hash, ACK-only commit và named `--mode conformance` giữ Q1
  transcript oracle.
- Hoàn thành `RB-WP2-011`: source-controlled four-epoch tiny demo chạy hai clean
  self-contained process, byte-exact golden/final hash và tamper proof.
- Hoàn thành `RB-WP2-012`: đóng WP2 physical/B1 bằng ADR-020, đồng bộ gate/
  traceability và tạo đúng một next refinement ticket `RB-WP3-001`.

## 3. Chưa làm

- Chưa có ledger/certificate implementation.
- Chưa có hard commitment budget, promise projection/append, phase-lock gate,
  incident breach accounting hoặc checkpoint.
- Chưa có C1/B2–B5/OR-Tools behavior; Q2 mới hoàn thành phần physical/B1 của
  WP2, chưa phải commitment guarantee.
- Chưa có BeGo/FleetPy/RidePy adapter.
- Chưa tải/freeze dataset cho experiment.
- Chưa pilot hoặc preregister.
- Chưa có bất kỳ kết quả chứng minh RideBound tốt hơn baseline.

## 4. Baseline verification

### RideBound

```text
.NET SDK 10.0.301
Build Release: 0 warnings, 0 errors
Architecture tests: 7 passed
Domain smoke tests: 1 passed
Date: 2026-07-28
```

### RB-WP1-001 protocol decision ticket

```text
dotnet test RideBound.slnx: passed
Architecture tests: 7 passed
Domain smoke tests: 1 passed
Markdown files checked: 48
Broken internal links: 0
Unbalanced code fences: 0
git diff --check: passed
Date: 2026-07-29
```

### RB-WP1-002–004 contract foundation

```text
.NET SDK 10.0.301
Release build: passed, 0 warnings, 0 errors
Contracts tests: 66 passed, 0 failed
Architecture tests from independent artifacts path: 7 passed, 0 failed
dotnet format --verify-no-changes: passed
NuGet direct/transitive vulnerability audit: no vulnerable packages
Full RideBound solution test: 73 passed; Domain smoke 1 blocked/reported failed
by Windows Application Control 0x800711C7, same pre-existing local blocker
Date: 2026-07-29
```

### RB-WP1-005–007 schema, handshake và initialize identity

```text
.NET SDK 10.0.301
Contracts tests: 95 passed, 0 failed
Runner boundary tests: 11 passed, 0 failed
Required dotnet test RideBound.slnx: 114 passed, 0 failed
Release build: passed, 0 warnings, 0 errors
dotnet format --verify-no-changes: passed
NuGet direct/transitive vulnerability audit: no vulnerable packages
Release full-suite attempt: 113 passed; Domain smoke 1 blocked/reported failed
by Windows Application Control 0x800711C7, same configuration-specific blocker
Date: 2026-07-29
```

### RB-WP1-008–015 Q1 closure và current revalidation

```text
.NET SDK 10.0.301
Release build: passed, 0 warnings, 0 errors
Release full solution at Q1 closure: 157 passed, 0 failed
  Contracts at closure: 114 passed
  Runner: 35 passed
  Architecture: 7 passed
  Domain: 1 passed
WP1 inventory after schema/vocabulary assertion và ba regression/E2E tests: 161
Current Debug full solution attempt:
  Contracts: 115 passed, 0 failed
  Architecture: 7 passed, 0 failed
  Domain: 1 passed, 0 failed
  Runner: 38 blocked before discovery by Windows Application Control 0x800711C7
WP1 Release full solution revalidation:
  Contracts: 115 passed, 0 failed
  Runner: 38 passed, 0 failed
  Architecture: 7 passed, 0 failed
  Domain: 1 passed, 0 failed
Current published CIBuild runner:
  RideBound.Runner.dll SHA-256:
  f3baa7daaec9b9167b52d9e110ac536a1bceff64e4f87498cc3e9d1be9d0c7c0
  Two clean processes: exit 0, empty stderr, exact expected output, byte-equivalent
Historical final Release full-suite attempt at Q1 closure:
  123 passed before Runner load
  Runner: 35 blocked before assertions by Windows Application Control
  At that closure point Runner source/tests were unchanged from their 35/35 pass
Real child-process NDJSON test at Q1 closure: passed
Current exact transcript replay through two clean child processes: passed
Changed-simTime duplicate corruption regression: passed
Published transcript replay twice/exact output/final hash at Q1 closure: passed
Tampered event changes decision hash at Q1 closure: passed
Required golden fixture inventory: exactly 10
dotnet format --verify-no-changes: passed
NuGet direct/transitive vulnerability audit: no vulnerable packages
JSON artifact parse audit: 66 files, 0 invalid
Current Markdown audit: 52 files, 0 broken internal links, 0 unbalanced fences
Default Debug full-suite final attempt: Architecture 7 passed; Domain 1 was
blocked; Runner reported 5 passed and 30 load-policy failures; Contracts reported
15 passed and 85 load-policy failures before completing its full 115-case
inventory. Enterprise Code Integrity policy
0283ac0f-fff1-49ae-ada1-8a933130cad6 blocked fresh DLL loads with 0x800711C7.
The Release rerun likewise blocked fresh Runner.dll. Event IDs 3033/3077 confirm
the signing-level policy; no assertion failure is used as correctness evidence.
Policy blocker reproduced for the fresh Debug Runner.Tests.dll but not for the
then-current WP1 Release suite, which passed 161/161.
Date: 2026-07-29
```

### RB-WP2-002–006 typed state, reducer và physical validator

```text
.NET SDK 10.0.301
Required dotnet test RideBound.slnx (Debug): 278 passed, 0 failed
Release full solution: 278 passed, 0 failed
  Contracts: 127 passed
  Domain: 89 passed
  Application: 13 passed
  Runner: 42 passed
  Architecture: 7 passed
Release build: 0 warnings, 0 errors
Whitespace format verification: passed
NuGet direct/transitive vulnerability audit: no vulnerable packages
Source JSON parse audit: 85 files, 0 invalid
Markdown audit: 52 files, 95 local links valid, 0 unbalanced fences
Git diff whitespace/error audit: passed
Typed WP2 schema additions: 16
Published WP2 fixture flow: bootstrap map/reduce/ack + epoch two pass
Lifecycle transition matrix: exhaustive pass
Small route precedence permutations: 24/24 match expected feasibility
Physical mutation dimensions: capacity, pickup window, max ride, precedence,
  connectivity, stop location, frozen prefix, plan version,
  onboard/accepted preservation, reassignment
Q1 exact transcript/hash/idempotency regression: pass
Date: 2026-07-29
```

### RB-WP2-007–012 B1, exact-small, online Runner và WP2 closure

```text
.NET SDK 10.0.301
Release build --warnaserror: passed, 0 warnings, 0 errors
dotnet format --verify-no-changes: passed; 0/137 files changed
Logical source-controlled test inventory: 333
  Contracts: 128
  Domain: 89
  Application: 15
  Algorithms: 45
  Runner: 49
  Architecture: 7
Required dotnet test RideBound.slnx (Debug): 333/333 passed
  Contracts: 128/128 passed
  Domain: 89/89 passed
  Application: 15/15 passed
  Algorithms: 45/45 passed
  Runner: 49/49 passed
  Architecture: 7/7 passed
Release full-solution xUnit attempt:
  Contracts: 128/128 passed
  Domain: 89/89 passed
  Architecture: 7/7 passed
  Application/Algorithms/Runner: Windows Application Control blocked fresh
  unsigned Application/Runner DLL loads with 0x800711C7 before assertions
Policy-safe supplemental execution for Release artifacts:
  Application: 15/15 passed
  Algorithms: 45/45 passed
  Runner non-child-process: 46/46 passed
Runner child-process cases verified separately: 3/3
  Q1 conformance two-process exact replay: passed
  stdout/stderr diagnostic isolation: passed
  WP2 online two-process exact replay: passed
NuGet direct/transitive vulnerability audit: no vulnerable packages
Portable Domain/Application forbidden-dependency scan: passed
Source JSON parse audit: 89 files, 0 invalid
Markdown audit: 53 files, 0 broken local links, 0 unbalanced fences
Git diff whitespace/error audit: passed
Algorithms detail:
  Hand-enumerated generator/policy cases: 13
  Independent exact-small published seeds: 32/32
  Generator gap: 0 in published bound
  Selection gap: 0 in published bound
Tiny online demo:
  epochs: 4
  lifecycle: accept -> pickup/board -> drop/alight
  physical rejection: r-2 / CAPACITY
  clean self-contained processes: 2/2 byte-exact
  final decision hash:
  56825f3591fb5d10f4c258d2c05897c016d82cb91c1318ffa23731c920146680
WP2 scope exclusion audit:
  no ledger/certificate produced, no hard budget, no OR-Tools behavior,
  no simulator adapter
Date: 2026-07-30
```

### CI hardening task

```text
Release build: passed, 0 warnings, 0 errors
Whitespace format verification: passed
NuGet vulnerability audit: no vulnerable direct/transitive packages
Runner publish smoke: passed
Architecture reference graph with normalized separators: passed
Local xUnit execution: blocked by Windows Application Control (0x800711C7)
Linux CI confirmation: pending
Date: 2026-07-28
```

### BeGo backend

```text
.NET SDK 10.0.301
Passed: 25
Failed: 0
Skipped: 0
Date: 2026-07-28
```

### BeGo frontend

```text
Passed: 7
Failed: 0
Warning: package type/module performance warning
Date: 2026-07-28
```

## 5. Next action

WP2 — online state và rolling baseline đã Complete cho phạm vi physical/B1.
Execution/gate evidence nằm trong
[26-wp2-online-baseline-ticket-plan.md](tasks/26-wp2-online-baseline-ticket-plan.md)
và ADR-020. Windows host-policy exception của lần chạy xUnit cuối được giữ riêng
ở mục 4 cho Release; required Debug baseline đã pass 333/333. Exception không
được biến thành Release pass và không mở rộng claim sang commitment.

Ticket duy nhất đang `READY`:

> `RB-WP3-001` — Refine promise/ledger/budget/lock/incident/certificate/
> checkpoint semantics và tạo ordered execution plan WP3; chưa viết ledger code.

Chi tiết:
[27-wp3-ledger-certificate-refinement.md](tasks/27-wp3-ledger-certificate-refinement.md).
Không bắt đầu C1, OR-Tools, adapter hoặc tự chọn mức budget O-002/O-003.

## 6. Open decisions

| ID | Câu hỏi | Khi nào khóa |
|---|---|---|
| O-002 | Budget vector cụ thể và mức loose/medium/tight? | WP8 pilot |
| O-003 | Material ETA revision threshold/bucket? | WP8 pilot |
| O-004 | Service non-inferiority margin cuối? | WP8 prereg |
| O-005 | RidePy hay AMoD2 là Layer 3 final? | WP10 preflight |
| O-006 | FleetPy 1.0.2 có cung cấp exact directed-edge progress ổn định không? Protocol union/capability đã khóa bởi ADR-014. | WP7 executable preflight; nếu không đạt, khai báo `nodeOnly` và fail/downgrade |
| O-007 | HTTP/gRPC có cần cho product v1 ngoài NDJSON? | WP5 |
| O-008 | Cross-city confirmatory hay robustness only? | WP8 |

O-001 đã được khóa bởi ADR-018: B1 WP2 không cho incumbent accepted request đổi
vehicle; WP4 chỉ mở lại bằng ADR superseding và atomic multi-vehicle evidence.

## 7. Decision log

### ADR-001 — 2026-07-27 — Accepted

**Context:** BeGo planner hiện phụ thuộc domain/session và là snapshot.

**Decision:** Xây RideBound thành các project độc lập với portable core; BeGo dùng adapter.

**Consequence:** Tốn contract/mapping ban đầu nhưng tránh khóa core vào product.

### ADR-002 — 2026-07-27 — Accepted

**Decision:** Novelty chỉ đặt ở per-rider, multi-dimensional, cumulative/switch budget qua nhiều epoch kèm certificate.

**Rejected:** claim dynamic insertion/ETA threshold/least-commitment nói chung.

### ADR-003 — 2026-07-27 — Accepted

**Decision:** Layer 1 BeGo và Layer 2 FleetPy là bằng chứng chính; Layer 3 cross-system là bổ sung.

### ADR-004 — 2026-07-27 — Accepted

**Decision:** FleetPy pin `1.0.2` commit `053aa9d4fcfde91c5d303435d5748f9206c071b0`.

### ADR-005 — 2026-07-27 — Accepted

**Decision:** RidePy v2.10.1 là Layer 3 mặc định; AMoD2 là alternate. OpenRidepoolSimulator không nằm critical path.

### ADR-006 — 2026-07-27 — Accepted

**Decision:** B0 BeGo hiện tại chỉ là context; B1 rolling online cùng core là baseline chính.

### ADR-007 — 2026-07-27 — Accepted

**Decision:** Budget synthetic là service-policy stress test, không là user preference truth.

### ADR-008 — 2026-07-27 — Accepted

**Decision:** NDJSON long-lived runner là canonical cross-language interface; in-process BeGo phải pass cùng contract.

### ADR-009 — 2026-07-27 — Accepted

**Decision:** Primary algorithm metric dự kiến tách decision-induced revision khỏi traffic-induced revision.

**Note:** exact primary endpoint chưa preregistered.

### ADR-010 — 2026-07-28 — Accepted

**Context:** Người dùng yêu cầu BeGo và hệ thống nghiên cứu có hai GitHub
repository, lịch sử và vòng đời phát hành riêng.

**Decision:** Đổi tên dự án thành RideBound và đặt tại repository độc lập
`https://github.com/tsunflowerr/RideBound`. Không copy `BeGo/src`. BeGo B0 nằm
ở repository cũ; B1 và C1 cùng nằm trong RideBound để so sánh công bằng.

**Consequence:** Tích hợp BeGo phải qua protocol/scenario/adapter rõ ràng.
Regression BeGo được chạy từ repository ngoài khi task chạm integration.

### ADR-011 — 2026-07-28 — Accepted

**Context:** Cấu trúc `Core` cũ chưa biểu diễn rõ Clean Architecture/DDD.

**Decision:** Dùng các layer `Domain`, `Application`, `Contracts`,
`Algorithms`, `Solvers.OrTools`, `Infrastructure` và `Runner`. Domain không
tham chiếu project/package; Application chỉ tham chiếu Domain. Architecture
tests kiểm project graph và từ khóa framework bị cấm.

**Consequence:** Chỉ thêm adapter/persistence project khi có behavior thật;
không scaffold hàng loạt assembly rỗng.

### ADR-012 — 2026-07-28 — Accepted

**Context:** Architecture test đọc dấu `\` trong `ProjectReference` bằng API path
phụ thuộc hệ điều hành, tạo false positive trên Linux CI. CI WP0 cũng mới chỉ có
restore/build/test nên chưa hiện thực đầy đủ PR fast gates trong tài liệu `15`.

**Decision:** Chuẩn hóa separator trước khi lấy project name và khóa lỗi bằng hai
regression cases Windows/Linux. Tách CI thành format, Release build/test/coverage,
NuGet/dependency review và package runner sau main. Sonar và PR-Agent được khai báo
nhưng chỉ chạy khi repository secrets/variables tương ứng đã tồn tại.

**Consequence:** Architecture rule giữ nguyên; chỉ cách đọc `.csproj` trở nên
cross-platform. AI review không phải required correctness gate. Sonar chỉ được đặt
required sau khi cấu hình và bootstrap scan thành công.

### ADR-013 — 2026-07-28 — Accepted

**Context:** Roadmap đã có 12 work package nhưng chưa có đơn vị delivery đủ nhỏ.
Ticket hóa chi tiết tất cả package ngay khi WP1 chưa khóa contract sẽ tạo dependency
và acceptance criteria dựa trên giả định chưa được kiểm chứng.

**Decision:** Dùng progressive elaboration: giữ WP1–WP12 ở mức topic/outcome/gate,
chỉ chia topic hiện hành thành ticket tuần tự. WP1 có 15 ticket trong `24`; mặc
định WIP là một implementation ticket. Topic kế tiếp chỉ được refinement khi topic
trước đạt exit gate.

**Alternatives considered:** Ticket hóa toàn bộ WP1–WP12 ngay; triển khai trực tiếp
từ deliverable cấp WP.

**Consequence:** Bước tiếp theo luôn nhỏ và kiểm tra được, nhưng backlog xa phải
được refinement lại bằng evidence thực trước khi triển khai.

**Evidence:** `23-delivery-backlog-and-ticket-policy.md`,
`24-wp1-contracts-ticket-plan.md`.

### ADR-014 — 2026-07-29 — Accepted

**Context:** Protocol draft còn cho phép nhiều cách hiểu về `schemaVersion`,
distance unit, node/edge position, batch sequence, field ownership, error
severity và hash concatenation. FleetPy có network position/plan locks trong khi
RidePy có thể chỉ cung cấp node-level state; một contract ngầm chọn một bên sẽ
làm cross-system replay không còn so sánh được.

**Decision:** Protocol v1 bắt đầu ở exact version `1.0.0`; dùng JSON integer-only
trong common safe range, millisecond, millimeter, WGS84 E7 và micro-cost có
`costUnitId`. Position là tagged union `node`/`edgeProgress` với capability
`nodeOnly`/`directedEdgeProgress`. Event sequence liên tiếp trên toàn run, epoch
chỉ tiến sau `decisionApplied`; gap/overlap làm session failed. Envelope chỉ giữ
message routing/identity, payload giữ nội dung message và initialize manifest giữ
config bất biến. Error dùng stable code cùng disposition
`rejectMessage`/`failSession`/`terminateProcess`. Canonical JSON là RFC 8785
subset integer-only; SHA-256 dùng domain prefix và tagged length frames. Fixture
phân loại `schema-only`, `runner-executable` hoặc `future-behavior`.

Chi tiết normative, exact range, field table, framing bytes và checklist nằm
trong `06-event-contract-and-determinism.md`, mục 2–14.

**Alternatives considered:** giữ distance là “mm hoặc meter”; chỉ hỗ trợ node;
ép mọi adapter phát edge progress; nối raw JSON/string khi hash; coi mọi fixture
là executable.

**Consequences:** Contract tests có thể viết exact bytes/error/order mà không tự
chọn semantics. Adapter thiếu edge progress phải công bố `nodeOnly` và
fail/downgrade có tên; không âm thầm bịa position. Mọi thay đổi unit, position
meaning, ordering hoặc hash input sau ADR này là breaking change, cần schema
major, ADR superseding và fixture migration.

**Evidence:** `05-portable-core-architecture.md`,
`06-event-contract-and-determinism.md`, `12-fleetpy-adapter.md`,
`13-cross-system-adapters.md`, `tasks/24-wp1-contracts-ticket-plan.md`.

**Supersedes / superseded by:** Không supersede ADR trước. Đóng phần contract của
O-006; chỉ giữ executable FleetPy capability preflight cho WP7.

### ADR-015 — 2026-07-29 — Accepted

**Context:** ADR-014 khóa semantics chung nhưng RB-WP1-005–007 vẫn cần exact
receiver behavior cho patch/minor/major, capability wire vocabulary và field
identity bất biến của initialize manifest. Nếu receiver tự bỏ field minor,
capability tự mặc định hoặc manifest lặp identity không nhất quán, cross-system
replay có thể dùng input khác nhau mà vẫn tưởng là cùng run.

**Decision:** Version phát hành hiện tại vẫn là exact `1.0.0`; receiver v1 nhận
cùng patch line `1.0.x`, từ chối higher minor nếu chưa có explicit safe-forward
profile và fail session với major khác trước unknown-field check. Safe-forward
profile phải machine-readable và current list rỗng. Capability v1 dùng
single-valued `positionModel`, semantic set có vocabulary cố định và explicit
fleet/request scale. Required capability thiếu/không biết phải fail; downgrade
chỉ hợp lệ khi có `downgradePolicyId`. `initializeRun` giữ run/scenario ID ở
envelope, manifest chỉ giữ content/config hashes, seed, policy, unit conversion,
exact negotiated selection, adapter/simulator, core commit và binary identity.
Pure validation không đọc Git/environment, không mutate active identity và cấm
re-initialize.

**Consequences:** Adapter có machine-readable schema/inventory để kiểm trước khi
gọi runner; patch bug fix không buộc migration semantics. Không có future minor
nào được nhận ngầm. Manifest không lặp ID nên mismatch scenario được kiểm với
envelope/session context, còn nội dung scenario được kiểm bằng hash. Hash values
trong WP1-007 là contract fields do caller cung cấp; calculation/vector vẫn thuộc
RB-WP1-010.

**Evidence:** `benchmarks/schemas/v1`, `ProtocolVersionCompatibility`,
`HelloMessages`, `InitializeRunMessages`, `CapabilityNegotiator`,
`InitializeRunValidator`, Contracts/Runner boundary tests và
`06-event-contract-and-determinism.md` mục 2.1, 4.2, 9.

**Supersedes / superseded by:** Bổ sung cách hiện thực ADR-014; không thay unit,
ordering, lifecycle hay hash framing đã khóa.

### ADR-016 — 2026-07-29 — Accepted

**Context:** RB-WP1-008–015 cần làm event/order/hash/NDJSON/session chạy thật
nhưng WP1 chưa có Domain reducer, solver hay commitment validator. Nếu runner
phát action hoặc certificate rỗng, fixture có thể bị hiểu nhầm là online
behavior đã tồn tại. State identity hash và dedup cache cũng cần exact,
bounded semantics để replay không phụ thuộc runtime.

**Decision:** Event payload v1 giữ exact input order và structural validation
chỉ kiểm schema/sequence/epoch/simulation time. Runner WP1 trả message
`decision` với `status = notProduced`, reason `WP1_STRUCTURAL_ONLY`, không action,
certificate `notProduced` và solver `notRun`; đây là acknowledgement có hash,
không phải routing decision. State identity structural dùng domain
`RideBound.StateIdentityHash.v1\0` và tagged frame
`canonicalStateIdentity`. Manifest/decision hash giữ nguyên ADR-014. Session chỉ
commit epoch/next sequence/previous decision hash sau exact `decisionApplied`.
Dedup giữ đúng một canonical batch/response gần nhất: exact retry trả byte-equivalent
response, cùng key khác payload hoặc partial overlap làm session failed.
Stdout chỉ có canonical NDJSON; diagnostic code đi stderr.

Q1 tính 9 required scenario là `future-behavior`; chỉ duplicate fixture và full
tiny transcript là runner-executable. Mốc đóng Q1 dùng full Release suite 157/157.
Sau khi thêm một assertion đồng bộ vocabulary, Contracts pass 115/115 và inventory
thành 158. Enterprise Code Integrity trên máy này có thể chặn unsigned/fresh DLL
theo lần build với `0x800711C7`; event log 3033/3077 ghi policy
`0283ac0f-fff1-49ae-ada1-8a933130cad6`. Lần full-suite cuối chặn fresh Runner.dll
cả ở Release. Đây là environment/configuration exception được ghi rõ, không phải
bỏ test, sửa policy hay assertion failure.

**Alternatives considered:** phát decision/action giả; dùng object certificate
rỗng; advance state ngay khi nhận event; cache mọi batch không giới hạn; nhận
partial retry; ghi banner/diagnostic vào stdout; bỏ Domain smoke khỏi test count.

**Consequences:** WP2 có executable protocol oracle nhưng vẫn phải cài behavior
thật qua reducer/validator. Replay phát hiện sửa/thứ tự/hash; retry không làm
advance state và memory dedup bounded. Q1 không chứng minh portable cross-system,
online insertion, hard budget hoặc certificate soundness. Default Debug test
phải được chạy lại trên CI/máy không có policy này, nhưng không chặn evidence
Release đã pass đầy đủ.

**Evidence:** `EventBatchMessages`, `DecisionMessages`, `ErrorMessage`,
`ProtocolHash`, `NdjsonReader`, `NdjsonWriter`, `RunnerSession`, `RunnerHost`,
`benchmarks/schemas/v1`, `benchmarks/schemas/fixtures/golden/required`,
`benchmarks/schemas/fixtures/runner`, 115 Contracts tests, 35 Runner tests,
7 Architecture tests và 1 Domain test; exact per-run evidence ở Q1 closure block.

**Supersedes / superseded by:** Bổ sung executable semantics cho ADR-014/015;
không đổi version, unit, event order, manifest/decision hash framing hoặc claim
boundary.

### ADR-017 — 2026-07-29 — Accepted

**Context:** Revalidation end-to-end WP1 phát hiện cache idempotency dùng key
run/scenario/epoch/sequence range nhưng chỉ hash `payload.events`. Một retry giữ
nguyên event payload và sequence nhưng đổi envelope `simTimeMs` được trả lại
decision cũ có time/hash của batch ban đầu. Điều này trái nghĩa “exact retry”,
có thể che transcript corruption và làm canonical input thực tế khác response
được cache.

**Decision:** Identity của retry nguyên batch gồm key run/scenario/epoch/sequence
range và SHA-256 của toàn canonical `eventBatch` envelope + payload. Mọi thay đổi
canonical context, gồm `schemaVersion`, `runId`, `scenarioId`, `epochId`,
`simTimeMs`, hoặc payload dưới cùng batch key là
`DUPLICATE_PAYLOAD_CONFLICT`/`failSession`. Exact retry tiếp tục trả
byte-equivalent cached response và không advance state/hash.

**Alternatives considered:** thêm riêng `simTimeMs` vào cache key rồi phân loại
thành overlap; chỉ so raw JSON bytes; giữ payload-only hash và tin client không
đổi context.

**Consequences:** Property order/whitespace khác nhưng canonical batch tương
đương vẫn idempotent; thay đổi semantic context không còn bị nhận nhầm. Đây là
bug fix patch-compatible cho behavior invalid, không đổi schema/unit/order/hash
framing của decision. Regression test changed-time duplicate và clean-process
replay được giữ trong Runner suite.

**Evidence:** `RunnerSession.CalculateCanonicalBatchHash`,
`Same_duplicate_key_with_changed_simulation_time_fails_session`,
`Published_transcript_replays_twice_through_clean_processes` và
`Canonically_equal_duplicate_ignores_json_formatting`; Runner Release 38/38 và
full solution Release 161/161. Debug fresh Runner test assembly bị host
policy chặn trước discovery; 123 non-Runner test pass.

**Supersedes / superseded by:** Làm rõ “exact retry” trong ADR-016; không
supersede lifecycle/hash-chain decision khác.

### ADR-018 — 2026-07-29 — Accepted

**Context:** Sau Q1, WP2 cần state/reducer/B1 thật nhưng chưa có quyết định chính
xác về ownership, bootstrap vehicle/travel state, atomic event reduction,
frozen-prefix semantics hoặc O-001 reassignment. Đưa Contracts DTO vào Domain,
commit từng event giữa batch hoặc mở reassignment incumbent ngay sẽ phá Clean
Architecture và làm exact-small baseline khó kiểm chứng.

**Decision:** Domain sở hữu run/request/vehicle/route state và invariant thuần;
Application sở hữu internal ordered-event reducer/orchestration; Runner boundary
map Contracts DTO sang internal events. Batch được validate/fold nguyên tử và
domain/plan state chỉ commit tại matching `decisionApplied`. Route dùng exact
executed/locked frozen prefix, mutable suffix và no-op candidate bắt buộc.
Vehicle/travel bootstrap đi qua typed epoch-one events. B1 WP2 cho pending
request chọn vehicle nhưng cấm incumbent accepted request đổi vehicle; mở lại
cần ADR superseding cùng atomic multi-vehicle/exact-small evidence. B1 chỉ dùng
physical constraints, accepted preservation, integer operational cost và stable
tie-break; không có commitment gate.

**Alternatives considered:** đặt reducer trong Contracts; cho Domain nhận
`EventBatchPayload`/`JsonElement`; mutate state từng event; đưa initial mutable
state vào manifest; cho reassignment ngay; kéo OR-Tools/WP3 ledger vào WP2.

**Consequences:** WP2 có queue nhỏ và test-first, giữ Domain/Application độc lập
và cô lập B1 physical baseline. B4/no-reassignment chưa là baseline phân biệt
trong WP2; WP4 phải ghi rõ equivalence hoặc mở reassignment B1 trước khi đánh giá
B4. Q1 structural conformance transcript được giữ bằng path có tên, còn online
B1 không được trả `WP1_STRUCTURAL_ONLY`.

**Evidence:** `04`, `05`, `06`, `08`, `15`,
`tasks/25-wp2-online-state-refinement.md`,
`tasks/26-wp2-online-baseline-ticket-plan.md`.

**Supersedes / superseded by:** Đóng O-001 cho WP2 và bổ sung ADR-011/016; không
đổi protocol v1 hoặc quyết định ledger/certificate của WP3.

### ADR-019 — 2026-07-29 — Accepted

**Context:** `RB-WP2-002..006` cần biến ADR-018 thành types/transition có thể
chạy, nhưng các chi tiết wire route/travel, proposed-state ownership, edge
progress scheduling và witness order chưa có executable semantics. Nếu reducer
nhận raw JSON, snapshot ngoài ghi đè route core hoặc validator tin cost/schedule
do candidate gửi, Q2 correctness không còn độc lập.

**Decision:** Protocol event v1 dispatch exact typed payload theo `eventType`.
Vehicle snapshot mang full canonical position/rider sets và route gồm
`planVersion`, `executedStopCount`, exact `frozenPrefix` cùng `mutableSuffix`;
stop pickup/drop mang request ID, waypoint không mang. Travel snapshot là directed
arc semantic set có version/hash; snapshot đầu phải khớp manifest và bản sau tăng
đúng một. Domain state/route là immutable và transition trả state mới hoặc stable
witness. Runner map toàn wire batch sang internal event trước khi Application
fold; một lỗi bỏ toàn proposed state. Committed state chỉ đổi qua matching
acknowledgement. Physical validator tự suy schedule từ current position,
remaining route và travel lookup; với edge progress nó cộng phần thời gian cạnh
còn lại bằng integer ceiling. Thứ tự witness deterministic bắt planVersion,
frozen prefix, connectivity/schedule, physical service và incumbent preservation.

**Alternatives considered:** giữ `JsonElement` payload; dùng catch-all
`fixtureIntent`; mutate aggregate từng event; cho external vehicle snapshot thay
route core; dùng schedule/cost candidate làm proof; đưa network/solver vào
validator; cho incident behavior chạy sớm.

**Consequences:** Contracts không phụ thuộc Domain; Application chỉ phụ thuộc
Domain; online fixture có thể map/reduce hai epoch mà chưa cần B1. Invalid event
cuối không tạo partial commit. Validator bắt capacity, pickup window,
max-ride-time, precedence, stop location, connectivity, plan/prefix,
onboard/accepted preservation và incumbent reassignment bằng witness máy đọc
được. Incident vẫn typed ở wire nhưng reducer trả unsupported tới WP3. WP2/Q2
chưa hoàn thành cho tới candidate/B1/exact-small/Runner online tickets.

**Evidence:** `OnlineEventModels`, `EventBatchPayloadCodec`,
`RideBoundRun`, `RideRequest`, `VehicleState`, `RoutePlan`,
`TravelTimeSnapshot`, `EventReducer`, `EventReductionCoordinator`,
`OnlineEventMapper`, `PhysicalPlanValidator`; published fixtures trong
`benchmarks/schemas/fixtures/wp2`; Debug/Release 278/278 cùng transition,
mutation và 24-permutation tests.

**Supersedes / superseded by:** Hiện thực hóa ADR-018; không đổi lifecycle/hash
chain của ADR-016/017 và không kéo commitment semantics WP3 vào WP2.

### ADR-020 — 2026-07-30 — Accepted

**Context:** `RB-WP2-007..012` phải biến state/reducer/physical validator của
ADR-018/019 thành baseline B1 chạy online, nhưng vẫn cần giữ candidate
completeness kiểm được, no-op, deterministic selection, independent oracle và
Q1 transcript oracle. Nếu generator tự chứng nhận, exact mode âm thầm truncate,
Runner commit trước ACK hoặc B1 phát certificate commitment, WP2 sẽ vượt claim
và làm mất oracle cho WP3/WP4.

**Decision:** `RideBound.Algorithms` sinh mọi precedence-preserving pickup/drop
insertion trong exact-small bound trên mutable suffix, giữ exact frozen prefix
và luôn xét no-op. Mỗi leaf được `PhysicalPlanValidator` kiểm; exact bound/cap
vượt giới hạn fail rõ, bounded mode cắt theo stable ID và giữ no-op. Fleet
selector exhaustive chọn đúng một candidate/vehicle, không serve request hai
lần, tối đa accepted count, tối thiểu checked integer operational cost rồi
tie-break bằng candidate ID ordinal. Selected candidate được validator độc lập
kiểm lại trước immutable apply; incumbent accepted không đổi vehicle và không
thành rejected. Runner default là online B1; Q1 shell chỉ còn trong named
`--mode conformance`. Full committed online state, event batch và typed actions
đi vào state/decision hash; route/request state chỉ commit sau exact matching
`decisionApplied`. Certificate WP2 luôn `notProduced`.

Exact-small oracle trong test tự enumerate/evaluate, không gọi production
generator/selector/validator. Published differential bound là tối đa 2 vehicle,
2 pending request và 32 deterministic seeds. Tiny demo khóa four-epoch
accept/pickup/board, physical capacity reject, drop/alight transcript cùng exact
final hash. WP2 được đóng chỉ cho physical/B1; P1/P2/P3 commitment, hard budget,
ledger, incident breach, checkpoint và certificate `produced` thuộc WP3.

**Alternatives considered:** chỉ sinh single-request candidate; dùng production
generator làm oracle; dùng weighted scalar/tie ngẫu nhiên; truncate exact mode;
commit ngay khi phát decision; overwrite Q1 golden bằng B1 output; coi
`PhysicalPlanValidator` là commitment certificate; kéo OR-Tools hoặc ledger vào
WP2.

**Consequences:** F-003/F-004 có executable B1 evidence, Q1 vẫn là regression
oracle có tên và WP3 nhận state/hash/ACK boundary thật. Baseline exhaustive chỉ
claim correctness trong published small bound, không claim scale/performance.
Required Debug `dotnet test RideBound.slnx` pass 333/333. Release xUnit vẫn bị
Windows Application Control `0x800711C7` chặn fresh unsigned Application/
Runner DLL trước assertion; policy-safe bundles và process checks pass cho đúng
Release artifacts đó, nhưng không được gọi là Release full-solution xUnit pass.

**Evidence:** `Candidates`, `Policies`, `ExactSmallOracle`,
`OnlineDecisionActionMapper`, `OnlineStateCanonicalizer`, online `RunnerSession`,
typed action schemas/tests, `benchmarks/scenarios/wp2-tiny`,
`scripts/run-wp2-tiny-demo.ps1` và execution plan `26`.

**Supersedes / superseded by:** Hoàn tất ADR-018/019 cho phạm vi WP2; không
supersede protocol/hash framing ADR-014/016/017. WP3 có thể bổ sung commitment
gate/certificate bằng ADR mới nhưng không được đổi physical B1 oracle âm thầm.

## 8. Work package tracker

| WP | Trạng thái | Bắt đầu | Kết thúc | Evidence |
|---|---|---|---|---|
| WP0 Scaffold | Complete | 2026-07-28 | 2026-07-28 | build + 8 RideBound + 25 backend + 7 frontend tests |
| WP1 Contracts | Complete; Q1 Release revalidated with host-policy exception | 2026-07-29 | 2026-07-29 | ADR-014–017 + 157/157 closure + WP1 revalidation 161/161 + replay/hash proof |
| WP2 Online baseline | Complete; physical/B1 gate, Debug 333/333; Release host-policy exception recorded | 2026-07-29 | 2026-07-30 | ADR-018–020 + Debug 333/333 + Release bundles + two-process tiny replay |
| WP3 Ledger/certificate | Ready; `RB-WP3-001` refinement only | 2026-07-30 | — | `tasks/27-wp3-ledger-certificate-refinement.md` |
| WP4 Algorithms/solver | Not started | — | — | — |
| WP5 BeGo integration | Not started | — | — | — |
| WP6 Benchmark harness | Not started | — | — | — |
| WP7 FleetPy | Not started | — | — | — |
| WP8 Pilot/prereg | Not started | — | — | — |
| WP9 Main experiments | Not started | — | — | — |
| WP10 Cross-system | Not started | — | — | — |
| WP11 Product UX | Not started | — | — | — |
| WP12 Paper/release | Not started | — | — | — |

## 9. Change history

- 2026-07-30: Hoàn thành `RB-WP2-007..012`: deterministic candidate generator,
  exhaustive B1 selection/apply, independent exact-small oracle 32/32 seeds,
  default online produced Runner decisions/ACK hash chain và four-epoch tiny
  demo chạy hai clean single-file process với exact final hash. Logical test
  inventory là 333; required Debug solution pass 333/333, Release build/format
  pass và Release blocked suites pass qua policy-safe bundles/process checks.
  Release xUnit host policy chặn fresh unsigned DLL bằng `0x800711C7` trước
  assertion, được ghi như environment exception chứ không tính Release pass.
  ADR-020 đóng WP2 physical/B1; next
  duy nhất là `RB-WP3-001` refinement, chưa có ledger code.
- 2026-07-29: Hoàn thành `RB-WP2-002..006`: typed online schemas/fixtures,
  immutable Domain lifecycle/route, manifest-bound travel snapshot, atomic
  mapper/reducer/ack và independent physical validator. Debug/Release full
  solution pass 278/278; 24 small route permutations và mutation dimensions
  pass. ADR-019 khóa executable semantics; next duy nhất là `RB-WP2-007`.
- 2026-07-29: Revalidate toàn bộ WP1: Release 161/161; Debug pass 123 non-Runner
  test rồi fresh Runner assembly bị host policy chặn trước discovery. Phát hiện
  payload-only dedup hash nhận nhầm retry đổi `simTimeMs`; sửa bằng ADR-017 để
  hash toàn canonical eventBatch, thêm regression conflict và replay qua hai
  clean runner process. Hoàn thành `RB-WP2-001` refinement bằng ADR-018 và
  execution plan `RB-WP2-002..012`; next duy nhất là `RB-WP2-002`.
- 2026-07-29: Hoàn thành `RB-WP1-008..015` và đóng WP1/Q1: event/decision/error
  contracts, hash chain, async NDJSON runner, lifecycle/idempotency/failure
  semantics, đúng 10 golden fixture và exact replay/tamper proof. Release full
  suite pass 157/157 tại mốc đóng; assertion đồng bộ vocabulary thêm sau đó pass,
  đưa Contracts lên 115 và inventory lên 158. Format/build/vulnerability audit
  sạch. Lần full-suite cuối bị enterprise Code Integrity chặn fresh Runner DLL
  với `0x800711C7` trước assertion cả ở Release; Debug cũng bị policy này chặn
  các fresh DLL. Next duy nhất là refinement `RB-WP2-001`, chưa có WP2 code.
- 2026-07-29: Hoàn thành `RB-WP1-005..007`: schema/inventory/compatibility
  assets, hello capability negotiation và immutable initialize identity.
  Required full solution test pass 114/114; Release build/format/dependency audit
  sạch. Release-only Domain smoke vẫn bị Windows Application Control chặn
  `0x800711C7` sau khi 113 test khác pass; next là `RB-WP1-008`.
- 2026-07-29: Hoàn thành `RB-WP1-002..004`: contract fixture harness, typed
  envelope/validation, canonical unit conversion và exact canonical JSON bytes.
  Contracts 66/66 và Architecture 7/7 pass từ independent artifacts path;
  Release build/format/dependency audit sạch. Full solution test vẫn bị Windows
  Application Control chặn Domain smoke (`0x800711C7`); next là `RB-WP1-005`.
- 2026-07-29: Hoàn thành docs ticket `RB-WP1-001` bằng ADR-014 và protocol
  decision checklist; thu hẹp O-006 sang FleetPy executable preflight; chuyển
  `RB-WP1-002` thành next/`READY`. Không thêm runtime/schema code và chưa nâng
  trạng thái implementation của WP1. Full RideBound regression pass 8/8; kiểm
  48 Markdown file có 0 internal link hỏng và 0 code fence lệch.
- 2026-07-28: Chuyển WP1 sang `READY`, thêm delivery policy và 15 ticket chi tiết;
  next action là `RB-WP1-001`. `dotnet test RideBound.slnx` hiện pass 8/8;
  không có implementation status nào được nâng.
- 2026-07-28: Sửa false positive architecture test trên Linux và mở rộng CI quality
  gates; Release build local sạch, test local chờ Linux CI xác nhận do Windows
  Application Control chặn nạp DLL test.
- 2026-07-28: Tách RideBound thành Git repository riêng, hoàn tất WP0 scaffold và chuyển next action sang WP1.
- 2026-07-27: Hoàn tất kiểm tra cấu trúc và mã hóa; chuyển docs sang `COMPLETE_V1_VERIFIED_PENDING_USER_REVIEW`.
- 2026-07-27: Khởi tạo status log và docs v1.
