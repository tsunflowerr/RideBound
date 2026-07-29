# WP1 — Contracts và canonical replay: execution plan

> Topic ID: `RB-WP1`
> Trạng thái: `COMPLETE — Q1 RELEASE VERIFIED`
> Cập nhật: 2026-07-29
> Ticket tiếp theo: `RB-WP2-002` trong
> `26-wp2-online-baseline-ticket-plan.md`
> Exit gate: Q1 Contracts

## 1. Outcome

WP1 tạo ranh giới protocol duy nhất để BeGo, FleetPy và simulator Layer 3 có
thể gọi cùng một `RideBound.Runner` và kiểm tra replay xác định.

Kết thúc topic phải có:

- protocol schema v1 với unit, ordering và version semantics đã khóa;
- canonical JSON và SHA-256 decision hash chain;
- runner tối thiểu xử lý `hello`, `initializeRun`, `eventBatch` và `error`;
- duplicate/idempotency/version failure semantics;
- 10 golden fixtures và transcript replay tạo cùng hash;
- gate report chứng minh Q1, không claim thuật toán online đã tồn tại.

## 2. Non-goals

WP1 không:

- implement request/vehicle domain state machine;
- quyết định accept/reject bằng rolling insertion;
- implement promise ledger, budget, certificate validation hoặc incident logic;
- thêm OR-Tools, EF Core, ASP.NET, BeGo/FleetPy dependency;
- tạo HTTP/gRPC;
- tạo adapter simulator;
- claim online insertion, hard budget hoặc portable core đã hoàn thành.

Golden fixture có thể mô tả shape dành cho behavior tương lai, nhưng runner WP1
không được giả lập thuật toán để làm fixture “pass”.

## 3. Quyết định phải giữ

- NDJSON long-lived stdin/stdout là interface canonical.
- `stdout` chỉ có protocol message; diagnostic đi `stderr`.
- Simulation time là integer millisecond từ origin, không dùng wall clock.
- Identifier là opaque string; không dựa vào thứ tự GUID.
- Route/list có semantic order không được sort khi canonicalize.
- Set chỉ được sort bằng comparer ordinal đã công bố.
- Không dùng floating point làm key, unit canonical hoặc hash input.
- Domain và Contracts là hai lá độc lập; WP1 ưu tiên Contracts/Runner/test.
- Breaking semantic change cần ADR, schema major và fixture migration.
- Không có DTO mới nếu không có fixture hoặc test chứng minh contract.

## 4. Ordered queue

| Thứ tự | Ticket | Kết quả chính | Trạng thái |
|---:|---|---|---|
| 1 | RB-WP1-001 | Protocol decisions/ADR được khóa | `DONE` |
| 2 | RB-WP1-002 | Contract test harness trong solution | `DONE` |
| 3 | RB-WP1-003 | Protocol primitives và envelope | `DONE` |
| 4 | RB-WP1-004 | Canonical JSON và unit rules | `DONE` |
| 5 | RB-WP1-005 | Schema/version compatibility | `DONE` |
| 6 | RB-WP1-006 | Hello/capability negotiation | `DONE` |
| 7 | RB-WP1-007 | Initialize-run/manifest identity | `DONE` |
| 8 | RB-WP1-008 | Event batch và ordering validation | `DONE` |
| 9 | RB-WP1-009 | Decision/error/certificate shell | `DONE` |
| 10 | RB-WP1-010 | Decision hash chain | `DONE` |
| 11 | RB-WP1-011 | NDJSON reader/writer | `DONE` |
| 12 | RB-WP1-012 | Runner session tối thiểu | `DONE` |
| 13 | RB-WP1-013 | Idempotency và failure semantics | `DONE` |
| 14 | RB-WP1-014 | Golden fixtures và replay proof | `DONE` |
| 15 | RB-WP1-015 | Q1 gate closure và handoff | `DONE` |

Chỉ ticket đầu tiên có dependency đã đạt mới được chuyển `READY`. Không bắt đầu
ticket sau chỉ vì ticket trước “gần xong”.

---

## RB-WP1-001 — Khóa protocol boundary và open decisions

**Mục đích**

Loại bỏ các điểm mơ hồ có thể làm canonical bytes, compatibility hoặc adapter
semantics khác nhau trước khi tạo type code.

**Description**

Rà `06`, `05`, FleetPy/RidePy capability evidence hiện có và chốt bằng ADR:

- exact `schemaVersion` format cho v1;
- distance/coordinate/cost unit và overflow range;
- node-only và edge-progress position representation;
- event batch ordering và epoch/event gap behavior;
- field nào nằm trong envelope, payload và manifest;
- error code taxonomy cùng recoverable/fatal classification;
- canonical hash input, domain separator và length framing;
- fixture taxonomy: schema-only, runner-executable, future-behavior.

**Trong phạm vi**

- cập nhật docs/ADR/traceability;
- đóng hoặc thu hẹp O-006;
- lập decision checklist có lựa chọn, rationale và consequence.

**Ngoài phạm vi**

- thêm C# DTO/schema;
- khóa vehicle reassignment O-001;
- thay đổi claim hoặc metric.

**Phụ thuộc**

- WP0 complete;
- `01`, `05`, `06`, `15`, `18`, `19` đã được đọc.

**Artifacts**

- ADR mới trong `18`;
- protocol decision table cập nhật trong `06`;
- trace link trong `19`.

**Rules**

- Không để “mm hoặc meter” trong unit đã khóa.
- Không dùng nối chuỗi không có length/domain separation cho hash.
- Capability thiếu phải fail fast hoặc downgrade có khai báo; không âm thầm bỏ.
- Quyết định không thể khóa từ evidence hiện có phải thành open decision có owner
  và ticket giải quyết, không được đoán.

**BDD**

```gherkin
Scenario: Hai adapter diễn giải cùng position
  Given manifest khai báo position capability đã được khóa
  When BeGo và FleetPy biểu diễn cùng vị trí canonical
  Then hai payload có cùng unit và semantics
  And khác biệt capability được biểu diễn rõ, không suy đoán ngầm

Scenario: Một breaking interpretation được đề xuất
  Given schema version hiện tại là v1
  When semantics của unit, ordering hoặc hash input bị thay đổi
  Then thay đổi được phân loại breaking
  And cần ADR, major-version plan và fixture migration
```

**Acceptance criteria**

- Mọi bullet trong description có một quyết định hoặc open decision có điều kiện
  khóa rõ.
- `06`, `18`, `19` không mâu thuẫn.
- Reviewer có thể viết envelope/canonical tests mà không tự chọn unit/ordering.
- Không có code runtime được thêm trong ticket này.

**Verification**

- kiểm link Markdown;
- tìm `TBD`, “hoặc” và unit mơ hồ trong phần protocol đã khóa;
- review decision checklist theo `17`.

**Rollback**

ADR không bị xóa. Nếu đổi, tạo ADR superseding.

**Completion evidence — 2026-07-29**

- ADR-014 trong `../18-status-and-decision-log.md`;
- normative decision table và exact protocol rules trong
  `../06-event-contract-and-determinism.md`, mục 2–14;
- traceability closure trong `../19-requirement-traceability.md`, mục 8;
- O-006 đã thu hẹp còn FleetPy executable capability preflight ở WP7;
- `dotnet test RideBound.slnx` pass 8/8 (7 architecture + 1 domain);
- 48 Markdown file: 0 internal link hỏng, 0 code fence lệch; `git diff --check`
  pass;
- không thêm C# DTO/schema/runtime code; ticket tiếp theo là `RB-WP1-002`.

---

## RB-WP1-002 — Tạo contract test project và fixture harness

**Mục đích**

Thiết lập test boundary trước khi thêm DTO để mọi contract mới có evidence và
được chạy bởi solution/CI.

**Description**

Tạo `tests/RideBound.Contracts.Tests`, thêm vào `RideBound.slnx`, tham chiếu
`RideBound.Contracts`, và tạo fixture loader chỉ đọc file UTF-8 từ
`benchmarks/schemas/fixtures`. Fixture path phải hoạt động trên Windows/Linux.

**Trong phạm vi**

- test project, solution entry, shared fixture path helper;
- một smoke fixture tối thiểu để chứng minh harness.

**Ngoài phạm vi**

- protocol production types;
- package schema validator nếu ADR chưa chọn;
- application/integration test project.

**Phụ thuộc**

- RB-WP1-001 `DONE`.

**Artifacts**

- `tests/RideBound.Contracts.Tests`;
- `benchmarks/schemas/fixtures/README.md`;
- smoke fixture và test.

**Rules**

- Fixture là source-controlled UTF-8 với LF ở canonical assets.
- Test không phụ thuộc current working directory.
- Không copy fixture vào nhiều project.
- Không thêm package ngoài test stack nếu chưa có lý do và audit.

**BDD**

```gherkin
Scenario: Chạy test từ output directory
  Given test assembly được chạy ngoài repository root
  When fixture loader mở smoke fixture
  Then fixture được tìm bằng repository-relative rule
  And nội dung UTF-8 được đọc không đổi

Scenario: Fixture không tồn tại
  Given test yêu cầu một fixture path sai
  When loader đọc fixture
  Then test fail với đường dẫn tương đối dễ chẩn đoán
```

**Acceptance criteria**

- Project xuất hiện trong solution và CI test discovery.
- Smoke test pass trên path separator độc lập OS.
- Architecture tests vẫn pass.
- Không tạo DTO không dùng.

**Verification**

```powershell
dotnet test RideBound.slnx
```

**Completion evidence — 2026-07-29**

- thêm `tests/RideBound.Contracts.Tests` vào solution, chỉ dùng test stack hiện có;
- fixture loader đọc UTF-8 strict từ `benchmarks/schemas/fixtures`, chặn path
  traversal, báo path tương đối và dùng build-time repository metadata nên chạy
  được cả khi output nằm ngoài repository;
- smoke fixture và 5 harness cases pass; toàn project Contracts hiện pass 66/66;
- ArchitectureTests chạy từ independent artifacts path pass 7/7;
- full solution test đã chạy nhưng Domain smoke local bị Windows Application
  Control chặn `0x800711C7`; không có assertion failure thuộc ticket này.

---

## RB-WP1-003 — Cài protocol primitives và envelope v1

**Mục đích**

Tạo lớp bao thống nhất để mọi message có identity, epoch, sequence và simulation
time có thể kiểm tra.

**Description**

Thêm type nhỏ cho `schemaVersion`, `messageType`, `runId`, `scenarioId`,
`epochId`, `eventSeq`, `simTimeMs` và `payload`. Phân biệt field bắt buộc,
optional và field không hợp lệ theo quyết định RB-WP1-001.

**Trong phạm vi**

- contract DTO/primitives;
- enum/string mapping versioned;
- validation ở contract boundary, không chứa business invariant.

**Ngoài phạm vi**

- event payload cụ thể;
- runner state mutation;
- domain typed IDs.

**Phụ thuộc**

- RB-WP1-002 `DONE`.

**Artifacts**

- `Contracts/Protocol` primitives/envelope;
- valid và invalid envelope fixtures/tests.

**Rules**

- JSON property names là exact lower camel case đã fixture hóa.
- Không nhận negative epoch/sequence/time nếu decision đã khóa cấm.
- Unknown `messageType` phải được phân loại, không map về default enum.
- Opaque ID không trim/case-fold ngầm.

**BDD**

```gherkin
Scenario: Đọc envelope hợp lệ
  Given một fixture có đủ field bắt buộc theo schema v1
  When contract boundary deserialize và validate
  Then field identity giữ nguyên chính xác
  And payload chưa bị diễn giải bởi envelope

Scenario: Message type không biết
  Given envelope có messageType ngoài vocabulary v1
  When boundary validate
  Then kết quả là lỗi versioned xác định
  And không mutate runner state
```

**Acceptance criteria**

- Round trip giữ semantics của mọi field.
- Invalid required field có reason code/test cụ thể.
- `RideBound.Contracts` vẫn không tham chiếu project/package ngoài.

**Verification**

```powershell
dotnet test RideBound.slnx
```

**Completion evidence — 2026-07-29**

- thêm typed `ProtocolVersion`, `ProtocolMessageType`, `RunId`, `ScenarioId`,
  `EpochId`, `EventSequence`, `SimulationTimeMilliseconds`;
- thêm `ProtocolEnvelopeCodec` với exact lower-camel field, context rule theo
  message type, unknown/duplicate/missing/type/range error classification;
- payload được clone dưới dạng `JsonElement`, chưa diễn giải business semantics;
- 3 protocol fixtures và tests chứng minh round-trip, opaque ID, unknown type
  versioned, field/range lỗi cụ thể và encoder không nhận envelope invalid;
- Release build 0 warning/0 error; Contracts tests nằm trong tổng 66/66 pass.

---

## RB-WP1-004 — Cài canonical JSON và unit normalization

**Mục đích**

Bảo đảm cùng semantic input tạo cùng byte sequence trên mọi lần chạy .NET và có
quy tắc đủ rõ để adapter ngôn ngữ khác tái tạo.

**Description**

Cài serializer canonical theo RB-WP1-001: UTF-8, property order, enum string,
null/default policy, integer formatting, escaping và newline. Thêm unit value
tests và golden byte vectors.

**Trong phạm vi**

- `CanonicalJson`;
- explicit serialization options/converters;
- canonical byte fixtures.

**Ngoài phạm vi**

- hash chain;
- sort route/ordered list;
- Python/C++ implementation.

**Phụ thuộc**

- RB-WP1-003 `DONE`.

**Artifacts**

- canonical serializer;
- byte-level golden vectors;
- unit/overflow tests.

**Rules**

- Không phụ thuộc locale, timezone hoặc host newline.
- Ordered list giữ thứ tự.
- Semantic set phải normalize bằng ordinal comparator tại boundary đã khai báo.
- Không serialize wall-clock/log/runtime vào canonical state.
- Reject overflow; không saturate hoặc round ngầm.

**BDD**

```gherkin
Scenario: Canonical bytes không phụ thuộc locale
  Given cùng một envelope và hai process có locale khác nhau
  When cả hai canonicalize
  Then byte sequence UTF-8 giống hệt

Scenario: Route order khác nhau
  Given hai route chứa cùng stop nhưng thứ tự khác
  When canonicalize
  Then canonical bytes khác nhau
  Because route order có semantics
```

**Acceptance criteria**

- Golden bytes so sánh exact, không chỉ so object equality.
- Tests cover Unicode, escaping, min/max unit và ordered collections.
- Serializer output dùng LF theo contract.

**Verification**

```powershell
dotnet test RideBound.slnx
```

**Completion evidence — 2026-07-29**

- thêm value types/conversion cho ms, mm, WGS84 E7, edge permille và micro-cost;
  conversion dùng `decimal` + `MidpointRounding.ToEven`, reject negative/overflow;
- thêm canonical JSON byte writer: UTF-8 không BOM/newline, recursive ordinal
  property order, ordered array preservation, exact integer-only range, Unicode
  scalar validation và RFC-style escaping;
- golden input + source-controlled expected hex chứng minh exact bytes;
- tests cover culture independence, Unicode BMP/non-BMP, escape/control,
  duplicate/null/fraction/exponent/negative-zero, min/max, route order và unit
  boundaries; Contracts tests 66/66 pass;
- `dotnet format --verify-no-changes` và Release build pass.

---

## RB-WP1-005 — Đặc tả schema và compatibility v1

**Mục đích**

Cho adapter và fixture có schema machine-readable, đồng thời ngăn minor/patch
version âm thầm đổi semantics.

**Description**

Tạo schema assets trong `benchmarks/schemas`, vocabulary/version policy và test
valid/invalid/unknown-field theo ADR. Schema phải trỏ được về contract type và
fixture, không nhân đôi semantics mâu thuẫn.

**Trong phạm vi**

- schema assets v1;
- compatibility matrix major/minor/patch;
- validation tests ở mức contract.

**Ngoài phạm vi**

- code generation cross-language;
- backward support cho version chưa tồn tại;
- business feasibility.

**Phụ thuộc**

- RB-WP1-004 `DONE`.

**Artifacts**

- schema files;
- compatibility fixtures;
- schema-to-contract inventory.

**Rules**

- Semantic breaking change luôn là major.
- Optional additive field chỉ là minor khi receiver cũ có safe behavior đã nêu.
- Patch không đổi canonical semantics.
- `$id`/schema reference là stable và không chứa path máy local.

**BDD**

```gherkin
Scenario: Major version không được hỗ trợ
  Given runner hỗ trợ protocol major 1
  When nhận envelope major 2
  Then trả version error fatal trước mutation

Scenario: Optional minor field được hỗ trợ
  Given policy cho phép unknown optional field của minor version
  When receiver v1 đọc message
  Then behavior tương thích đúng compatibility matrix
```

**Acceptance criteria**

- Mỗi message đã có có schema và fixture.
- Compatibility behavior có test, không chỉ mô tả.
- Schema paths dùng được từ repository và artifact bundle.

**Verification**

```powershell
dotnet test RideBound.slnx
```

**Completion evidence — 2026-07-29**

- thêm Draft 2020-12 schema assets, stable `$id`, relative `$ref`, schema inventory
  và machine-readable compatibility matrix trong `benchmarks/schemas/v1`;
- `ProtocolVersionCompatibility` cùng envelope boundary phân biệt exact/patch,
  unsupported minor, unsupported major và unknown field theo disposition đã khóa;
- compatibility fixtures/tests chứng minh patch-compatible, minor reject, major
  fail-session và explicit-safe-minor policy; current safe profile list rỗng;
- schema tests parse mọi asset, kiểm `$id` không chứa local path, local `$ref`
  resolve từ artifact directory và inventory map được về contract type/fixture.

---

## RB-WP1-006 — Cài hello và capability negotiation

**Mục đích**

Fail fast trước run nếu simulator không cung cấp semantics mà policy yêu cầu.

**Description**

Thêm `hello`/`helloAck` payload, capability vocabulary và selection result.
Capability tối thiểu gồm position model, dynamic travel time, stop relocation,
vehicle reassignment, cancellations, event ordering và old-plan projection.

**Trong phạm vi**

- contract types/schema/fixtures;
- pure negotiation function tại runner boundary nếu cần.

**Ngoài phạm vi**

- simulator probing;
- policy implementation;
- silent fallback.

**Phụ thuộc**

- RB-WP1-005 `DONE`.

**Artifacts**

- hello/ack contracts;
- capability fixtures và negotiation tests.

**Rules**

- Required capability thiếu → fail fast hoặc explicit declared downgrade.
- Unknown required capability không được bỏ qua.
- Negotiated result phải xuất hiện trong init identity/hash input theo ADR.

**BDD**

```gherkin
Scenario: Đủ capability bắt buộc
  Given client và runner cùng hỗ trợ capability bắt buộc
  When hello được xử lý
  Then helloAck chọn tập capability xác định

Scenario: Thiếu old-plan projection bắt buộc
  Given policy yêu cầu tách traffic-induced revision
  And client không hỗ trợ old-plan projection
  When hello được xử lý
  Then negotiation fail với reason code cụ thể
  And run chưa được initialize
```

**Acceptance criteria**

- Capability selection không phụ thuộc input set order.
- Required/optional/downgrade paths đều có test.
- Không có capability mặc định không được công bố.

**Verification**

```powershell
dotnet test RideBound.slnx
```

**Completion evidence — 2026-07-29**

- thêm typed `hello`/`helloAck`, position/capability vocabulary, scale limits và
  strict payload codecs/schema/fixtures;
- thêm pure `CapabilityNegotiator` tại runner boundary; required/optional,
  deterministic set order, unknown required, missing old-plan projection,
  scale limit và named downgrade đều có test;
- selection không thể công bố capability/position/scale client chưa offer và
  không tạo run state; exact result được dùng tiếp trong manifest identity.

---

## RB-WP1-007 — Cài initialize-run và manifest identity

**Mục đích**

Khóa identity/config của một run để replay và result bundle có thể chứng minh
đang chạy cùng scenario/policy/binary.

**Description**

Thêm `initializeRun`/`initialized`, manifest identity và state identity ban đầu:
scenario, seed, policy version, core commit/binary hash, adapter/simulator
version, rounding/capability selection.

**Trong phạm vi**

- contract/schema/fixtures;
- validation chéo identity với envelope/hello.

**Ngoài phạm vi**

- đọc Git hoặc tự hash executable tại domain;
- persistence/checkpoint;
- thuật toán.

**Phụ thuộc**

- RB-WP1-006 `DONE`.

**Artifacts**

- init contracts;
- manifest identity schema;
- valid/mismatch tests.

**Rules**

- `runId`/`scenarioId` bất biến sau initialize.
- Seed/config/version là explicit; không lấy từ environment ngầm.
- Re-initialize session active là protocol error.
- Identity mismatch không mutate state.

**BDD**

```gherkin
Scenario: Initialize sau handshake
  Given hello negotiation thành công
  When initializeRun có identity nhất quán
  Then runner trả initialized với initial state identity

Scenario: Scenario ID không khớp envelope
  Given initialize payload và envelope có scenarioId khác nhau
  When message được xử lý
  Then runner trả identity mismatch
  And session vẫn chưa initialized
```

**Acceptance criteria**

- Required reproducibility identity có schema/test.
- Không có wall-clock trong canonical init identity.
- Repeated/mismatched init behavior xác định.

**Verification**

```powershell
dotnet test RideBound.slnx
```

**Completion evidence — 2026-07-29**

- thêm immutable `RunManifestIdentity`, `initializeRun`/`initialized` codecs,
  schema và valid/invalid fixtures;
- manifest khóa seed, policy/config, scenario/graph/travel hashes, unit conversion,
  negotiated capability, adapter/simulator, core commit và binary hash; không
  lặp run/scenario ID hoặc nhận wall-clock/hostname/path;
- pure `InitializeRunValidator` kiểm envelope/session context với hello/ack/
  manifest; mismatch và re-initialize trả exact protocol code mà không tạo
  identity mới;
- `tests/RideBound.Runner.Tests` được thêm vào solution để test negotiation/init
  boundary mà chưa cài NDJSON session hoặc thuật toán;
- required full solution test pass 114/114; Release build/format/dependency audit
  sạch. Release-only Domain smoke bị Windows Application Control chặn
  `0x800711C7` sau khi 113 test khác pass.

---

## RB-WP1-008 — Cài event batch contract và ordering validation

**Mục đích**

Chuẩn hóa input stream trước khi WP2 cài reducer/domain state.

**Description**

Thêm event batch/event vocabulary v1 và structural validation cho
`eventSeq`, `epochId`, `simTimeMs`, batch order. Payload business có thể ở dạng
contract đã fixture hóa; không mutate domain.

**Trong phạm vi**

- control/input event schemas từ `06`;
- ordering/gap validation thuần protocol.

**Ngoài phạm vi**

- lifecycle transition;
- route/vehicle physical validation;
- quyết định accept/reject.

**Phụ thuộc**

- RB-WP1-007 `DONE`.

**Artifacts**

- event contracts/schema;
- ordered, out-of-order, gap và empty-batch fixtures.

**Rules**

- Batch order theo `eventSeq`.
- Gap/duplicate behavior theo ADR, không auto-fill.
- Event cùng `simTimeMs` vẫn giữ exact sequence.
- Structural acceptance không có nghĩa business-valid.

**BDD**

```gherkin
Scenario: Nhiều event cùng simulation time
  Given batch có eventSeq liên tiếp và cùng simTimeMs
  When validate batch
  Then order theo eventSeq được giữ nguyên

Scenario: Event sequence có gap
  Given expected eventSeq là 11
  When batch bắt đầu bằng eventSeq 12
  Then trả sequence-gap error theo severity đã khóa
  And không chuyển expected sequence
```

**Acceptance criteria**

- Mọi message input ở `06` có vocabulary/schema entry.
- Ordering, gap và overflow có test.
- Không có domain state machine trong Contracts.

**Verification**

```powershell
dotnet test RideBound.slnx
```

---

## RB-WP1-009 — Cài decision, certificate shell và error contract

**Mục đích**

Khóa output shape và machine-readable failure trước khi thuật toán/validator
được cài.

**Description**

Thêm decision envelope/payload, reason codes, solver diagnostics shell,
certificate shell, state/hash fields và error payload. “Shell” chỉ khóa shape;
không tạo certificate hợp lệ giả.

**Trong phạm vi**

- contract/schema/fixtures;
- recoverable/fatal error classification.

**Ngoài phạm vi**

- ledger delta computation;
- physical/commitment validation;
- solver invocation.

**Phụ thuộc**

- RB-WP1-008 `DONE`.

**Artifacts**

- decision/error contracts;
- reason-code inventory;
- golden valid/invalid shapes.

**Rules**

- Reason code versioned, free text chỉ bổ sung.
- Error không được chứa stack trace/path/secret trong stdout.
- Certificate chưa được tính phải biểu diễn rõ unsupported/not-produced; không
  dùng object rỗng dễ bị hiểu là valid.
- Solver `FEASIBLE` không được map thành `OPTIMAL`.

**BDD**

```gherkin
Scenario: Protocol error được trả machine-readable
  Given input malformed
  When runner tạo error response
  Then response có stable error code và severity
  And diagnostic nhạy cảm không xuất hiện trong stdout

Scenario: WP1 chưa có certificate
  Given runner chưa có commitment validator
  When output shape được fixture hóa
  Then certificate không bị biểu diễn như một chứng nhận hợp lệ
```

**Acceptance criteria**

- Error taxonomy khớp RB-WP1-001.
- Decision shape có state/hash linkage nhưng không claim business behavior.
- Fixtures phân biệt contract-only với runner-executable.

**Verification**

```powershell
dotnet test RideBound.slnx
```

---

## RB-WP1-010 — Cài deterministic decision hash chain

**Mục đích**

Phát hiện transcript bị thiếu, đổi thứ tự hoặc sửa input/decision.

**Description**

Cài SHA-256 hash chain đúng framing/domain separation đã khóa. Hash nhận
canonical bytes, previous hash, policy version và các input identity được ADR
chọn. Không đọc wall-clock/filesystem trong core calculation.

**Trong phạm vi**

- hash value type/calculator ở boundary phù hợp;
- published cross-language hash vectors.

**Ngoài phạm vi**

- cryptographic signing;
- artifact binary hashing implementation nếu không cần cho decision chain;
- business decision generation.

**Phụ thuộc**

- RB-WP1-004 và RB-WP1-009 `DONE`.

**Artifacts**

- hash calculator;
- initial/next/tampered golden vectors.

**Rules**

- Dùng bytes có length framing/domain separator, không nối text mơ hồ.
- Previous hash bắt buộc sau genesis.
- Hex/base64 representation có casing chính xác đã khóa.
- Constant-time comparison chỉ cần nếu security boundary yêu cầu; không overclaim.

**BDD**

```gherkin
Scenario: Replay cùng transcript
  Given cùng genesis, canonical state, decision và policy version
  When tính chain hai lần
  Then hash bytes và text representation giống hệt

Scenario: Một event bị sửa
  Given transcript đã có final hash
  When một canonical input byte thay đổi
  Then hash tại epoch đó và mọi hash sau đều khác
```

**Acceptance criteria**

- Hash vectors có input bytes hiển thị/kiểm tra được.
- Tests cover genesis, multi-step, tamper và ordering.
- Không có nondeterministic field trong hash input.

**Verification**

```powershell
dotnet test RideBound.slnx
```

---

## RB-WP1-011 — Cài NDJSON reader/writer

**Mục đích**

Cung cấp framing cross-language an toàn cho runner dài hạn mà không làm bẩn
protocol stdout.

**Description**

Cài async line reader/writer cho UTF-8 NDJSON, line length limit, EOF,
malformed UTF-8/JSON và flush semantics. Diagnostic được inject/ghi `stderr`.

**Trong phạm vi**

- transport framing trong Runner;
- test qua memory streams/pipes.

**Ngoài phạm vi**

- runner session state;
- process watchdog ở adapter;
- HTTP/gRPC.

**Phụ thuộc**

- RB-WP1-005 `DONE`.

**Artifacts**

- `NdjsonReader`, `NdjsonWriter`;
- framing/error tests.

**Rules**

- Một line là một complete JSON object.
- Writer phát đúng một LF và flush theo protocol.
- Không ghi banner/log vào stdout.
- Oversized line fail có giới hạn, không allocate vô hạn.
- EOF giữa JSON trả deterministic framing error.

**BDD**

```gherkin
Scenario: Đọc hai message liên tiếp
  Given stream UTF-8 có hai JSON object phân cách bằng LF
  When reader chạy
  Then trả đúng hai message theo thứ tự

Scenario: Diagnostic được phát sinh
  Given một malformed line
  When runner báo diagnostic
  Then stdout vẫn chỉ chứa error protocol object
  And diagnostic text chỉ đi stderr
```

**Acceptance criteria**

- Memory/pipe tests cover CRLF input policy, LF output, EOF và size limit.
- No console stdout call ngoài protocol writer.
- Reader không swallow cancellation.

**Verification**

```powershell
dotnet test RideBound.slnx
```

---

## RB-WP1-012 — Cài runner session tối thiểu

**Mục đích**

Chứng minh protocol có lifecycle executable trước khi thêm online engine.

**Description**

Cài session state `new → negotiated → initialized`, dispatch
`hello`, `initializeRun`, structural `eventBatch`, và `error`. Event hợp lệ chỉ
tạo deterministic protocol acknowledgement/state identity theo scope WP1; không
tạo routing decision.

**Trong phạm vi**

- Runner composition/dispatch;
- session transition và error non-mutation;
- stdin/stdout integration tests.

**Ngoài phạm vi**

- WP2 reducer;
- request acceptance;
- certificate/solver.

**Phụ thuộc**

- RB-WP1-006 đến RB-WP1-011 `DONE`.

**Artifacts**

- `RunnerSession`/composition;
- happy-path và invalid-order transcripts.

**Rules**

- `initializeRun` trước `hello` bị từ chối.
- Malformed/invalid message không mutate session.
- Runner không tự đoán decision applied.
- Cancellation/EOF kết thúc có semantics đã test.

**BDD**

```gherkin
Scenario: Happy path tối thiểu
  Given runner mới khởi động
  When client gửi hello, initializeRun và structural eventBatch hợp lệ
  Then runner trả response đúng thứ tự
  And không xuất routing decision giả

Scenario: Initialize trước hello
  Given runner ở trạng thái new
  When client gửi initializeRun
  Then runner trả invalid-session-state
  And vẫn ở trạng thái new
```

**Acceptance criteria**

- Transcript integration test chạy process hoặc host boundary thực.
- Stdout parse được hoàn toàn như NDJSON.
- State transition table có test cho mọi cạnh cho phép/cấm.

**Verification**

```powershell
dotnet test RideBound.slnx
```

---

## RB-WP1-013 — Cài idempotency và version failure semantics

**Mục đích**

Ngăn duplicate retry làm thay đổi state và phát hiện data corruption khi cùng
identity có payload khác.

**Description**

Theo dõi identity/hash tối thiểu trong session. Cùng batch key và cùng canonical
`eventBatch` envelope + payload trả idempotent response; cùng key nhưng đổi
context như `simTimeMs` hoặc đổi payload làm run fail theo ADR. Thêm
major/minor/version/session/epoch gap paths.

**Trong phạm vi**

- protocol session idempotency;
- deterministic response/error replay.

**Ngoài phạm vi**

- distributed persistence/dedup;
- checkpoint restore;
- business event reducer.

**Phụ thuộc**

- RB-WP1-012 `DONE`.

**Artifacts**

- dedup state;
- duplicate/version/gap transcript tests.

**Rules**

- Duplicate cùng payload không advance state/hash.
- Duplicate khác payload là corruption, không “last write wins”.
- Cache/dedup bound hoặc lifecycle phải được nêu để tránh unbounded memory.
- Fatal session không tiếp tục nhận business messages như bình thường.

**BDD**

```gherkin
Scenario: Retry cùng event
  Given eventSeq 10 đã được nhận
  When cùng eventSeq và canonical payload được gửi lại
  Then runner trả idempotent response tương đương
  And state identity không đổi

Scenario: Cùng sequence nhưng payload khác
  Given eventSeq 10 đã được nhận
  When eventSeq 10 được gửi với payload hash khác
  Then runner trả data-corruption fatal error
  And không áp payload mới
```

**Acceptance criteria**

- Same/different duplicate behavior có exact transcript assertions.
- Unsupported major, invalid minor và epoch gap có test.
- Memory/lifecycle behavior của dedup state được document.

**Verification**

```powershell
dotnet test RideBound.slnx
```

---

## RB-WP1-014 — Hoàn thiện golden fixtures và replay/hash proof

**Mục đích**

Tạo oracle dùng chung cho .NET và adapter tương lai, đồng thời chứng minh replay
protocol xác định ở phạm vi WP1.

**Description**

Hoàn thiện 10 fixture theo `06`, gắn metadata phân loại:

- `schema-only`: shape tương lai, chưa executable;
- `runner-executable`: behavior WP1 thực sự hỗ trợ;
- `future-behavior`: expected WP2/WP3, không tính vào Q1 runtime pass.

Tạo transcript replay tool/test đọc runner-executable fixtures, chạy ít nhất hai
lần và so canonical responses/hash. Fixture 4–8 không được giả lập ledger/incident
để tạo pass giả.

**Trong phạm vi**

- đủ 10 fixture inventory;
- full tiny WP1 transcript;
- replay comparison và fixture validation.

**Ngoài phạm vi**

- triển khai semantics WP2/WP3;
- Python/C++ adapter validation;
- checkpoint state restoration thực.

**Phụ thuộc**

- RB-WP1-010 đến RB-WP1-013 `DONE`.

**Artifacts**

- 10 fixture directories/files;
- fixture manifest;
- replay tool/test và expected hashes.

**Rules**

- Mỗi fixture ghi purpose, support level và expected validator.
- Không gọi `future-behavior` là passing runtime scenario.
- Expected hash là source-controlled vector, không tự update khi test chạy.
- Update golden cần review diff và rationale.

**BDD**

```gherkin
Scenario: Replay cùng transcript hai lần
  Given một runner-executable transcript và cùng manifest
  When chạy từ clean process hai lần
  Then canonical response sequence giống hệt
  And final hash giống expected golden hash

Scenario: Fixture ledger chưa được implement
  Given fixture commitment-budget thuộc future-behavior
  When chạy Q1 verification
  Then fixture được schema-validate
  But không được báo là runner behavior đã pass
```

**Acceptance criteria**

- Inventory có đúng 10 fixture bắt buộc, không thiếu.
- Q1 report tách schema coverage và executable coverage.
- Same transcript/same hash pass từ clean runner.
- Một tampered transcript fail đúng epoch.

**Verification**

```powershell
dotnet test RideBound.slnx
```

---

## RB-WP1-015 — Đóng Q1 và handoff WP2

**Mục đích**

Chỉ đánh dấu WP1 complete khi deliverable và exit gate có evidence kiểm tra được,
đồng thời cung cấp điểm bắt đầu rõ cho WP2.

**Description**

Chạy full verification, audit dependency/contract/fixtures, cập nhật `00`, `16`,
`18`, `19`, đóng/carry open decisions và tạo WP2 refinement ticket. Không bắt đầu
WP2 code trong ticket này.

**Trong phạm vi**

- gate report;
- docs/status/traceability;
- artifact inventory và exact test counts.

**Ngoài phạm vi**

- WP2 domain code;
- claim portable/online/certificate;
- BeGo regression nếu contract chưa được tích hợp sang BeGo.

**Phụ thuộc**

- RB-WP1-001 đến RB-WP1-014 `DONE`.

**Artifacts**

- Q1 gate evidence trong `18`;
- WP1 requirements chuyển `Implemented/Verified` với file/test cụ thể trong `19`;
- next action là WP2 refinement, không phải thuật toán tùy ý.

**Rules**

- `WP1 Complete` chỉ khi mọi exit gate trong `16` đạt.
- Test count và môi trường được ghi chính xác.
- Open issue không bị xóa; carry với owner/impact.
- Docs không nói runner có online insertion.

**BDD**

```gherkin
Scenario: Tất cả Q1 gate đạt
  Given schema/golden/version/idempotency/replay tests pass
  And cùng transcript tạo cùng hash
  When gate review hoàn tất
  Then WP1 được đánh dấu Complete
  And WP2 refinement trở thành next action

Scenario: Một replay hash còn không ổn định
  Given test khác hash giữa hai clean run
  When gate review diễn ra
  Then WP1 không được đánh dấu Complete
  And blocker cùng evidence được ghi trong status log
```

**Acceptance criteria**

- `dotnet test RideBound.slnx` pass với exact count được ghi.
- Contract/golden/hash/version/idempotency tests đều có evidence.
- Internal doc links và code fences hợp lệ.
- `18` có một next ticket duy nhất.
- `19` không ghi `Verified` chỉ dựa vào docs.

**Verification**

```powershell
dotnet test RideBound.slnx
```

## 5. Topic-level risk controls

| Risk | Dấu hiệu | Control |
|---|---|---|
| Overdesign contract | DTO không có fixture/consumer | no DTO without fixture |
| False implementation claim | future fixture được tính là runtime pass | fixture support level |
| Hash không portable | object equality pass nhưng bytes khác | byte vectors + framing ADR |
| Runner stdout bẩn | banner/log chen NDJSON | pipe integration test |
| Domain leakage | business invariant nằm Contracts | architecture/review gate |
| Version drift | optional field đổi semantics | compatibility matrix |
| Scope creep sang WP2 | event batch tạo routing decision | non-goal + transcript assertion |

## 6. WP1 exit-gate checklist

- [x] Protocol schema v1 được khóa và versioned.
- [x] Canonical unit/JSON/hash rules không còn mơ hồ.
- [x] Có 10 golden fixtures với support level trung thực.
- [x] Runner xử lý hello/init/event/error tối thiểu.
- [x] Duplicate cùng payload idempotent.
- [x] Duplicate khác payload và version/gap lỗi đúng taxonomy.
- [x] Same clean transcript tạo same response/hash.
- [x] Full solution Release pass 157/157 tại mốc đóng; current revalidation sau
  assertion vocabulary và exact-retry regression đạt Release 161/161. Debug
  pass 123 non-Runner test rồi fresh Runner assembly bị Application Control chặn
  trước discovery. Exact environment/correctness evidence nằm trong `18`.
- [x] `18` và `19` có evidence file/test cụ thể.
- [x] Không có online algorithm/ledger/solver/adapter bị làm sớm.

## 7. Lệnh bắt đầu

`RB-WP1-001` đến `RB-WP1-015` đã hoàn thành và revalidate. Q1 đã đóng;
`RB-WP2-001` refinement cũng đã Done. Ticket duy nhất `READY` là `RB-WP2-002`
trong `26-wp2-online-baseline-ticket-plan.md`. Trước khi thực hiện:

```powershell
Get-Content docs/tasks/26-wp2-online-baseline-ticket-plan.md -Encoding utf8
Get-Content docs/18-status-and-decision-log.md -Encoding utf8
Get-Content docs/04-problem-model-and-notation.md -Encoding utf8
Get-Content docs/08-algorithms-baselines-and-solver.md -Encoding utf8
dotnet test RideBound.slnx -c Release --no-build --no-restore
```
