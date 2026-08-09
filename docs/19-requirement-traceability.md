# Ma trận truy vết yêu cầu

## 1. Yêu cầu từ mục tiêu dự án

| ID | Yêu cầu | Thiết kế/tài liệu | Artifact dự kiến | Verification | Trạng thái |
|---|---|---|---|---|---|
| R-001 | Hệ thống RideBound độc lập | `05` | `Domain/Application/Runner` trong Git repo riêng | 5 architecture rules + 2 cross-platform path cases | WP0 verified; Linux CI rerun pending |
| R-002 | Gắn được vào BeGo | `02`, `14`, `32` | BeGo adapter/API | integration replay | WP5 `002..014` có Application, persistence, intake/lease, Runner, bootstrap/API, fenced T2/T3 recovery, Applied-only outbox/SignalR, rebuildable timeline, default-off Shadow/Live rollout và paired Layer-1 bundle |
| R-003 | Core mang sang benchmark | `05`, `06`, `24`, `26`, `32` | contracts + runner | same binary hash | WP1/WP2 runner verified; WP5 BeGo bootstrap/decision/recovery uses pinned published artifact hash; cross-simulator proof pending |
| R-004 | Layer 1 cùng codebase | `09` | B1/C1 chung RideBound engine; BeGo export adapter | paired runs | Planned |
| R-005 | Layer 2 simulator chung | `09`, `12` | FleetPy adapter | Layer 2 gate | Planned |
| R-006 | Layer 3 framework độc lập | `09`, `13` | RidePy/AMoD2 adapter | Layer 3 gate | Planned |
| R-007 | Giới hạn cumulative revision | `04`, `07`, `28` | ledger/validator | property/mutation tests | WP3 implemented: independent hard-vector recomputation + immutable ledger + certificate |
| R-008 | Nhiều chiều promise | `04`, `07`, `28` | promise vector | golden fixtures | WP3 implemented: 10 dimensions, projection, three-way delta, strict wire actions và tiny replay |
| R-009 | Certificate/witness | `07`, `28` | certificate DTO/validator | invalid-plan mutations | WP3 implemented: physical→lock→budget stages, normal/witness body và decision/publication cross-binding |
| R-010 | Đánh giá bằng data rõ | `10` | manifests/data pipeline | checksums/validation | Planned |
| R-011 | Metric/statistics rõ | `11` | analysis package | prereg/full report | Planned |
| R-012 | Dùng paper nhưng không làm lại | `03`, `21` | claim ledger | novelty re-audit | WP1–WP5 mechanisms mapped; final re-audit DONE trong `reviews/wp1-wp5-final/07-paper-to-code-and-optimization.md` |
| R-013 | Nêu công nghệ và tối ưu | `05`, `08`, `12`–`14` | projects/adapters | build/performance | WP4 solver + WP5 DB/Runner/concurrency/local curves implemented; no SLA claim |
| R-014 | Nêu thêm/bỏ gì | `02`, mục 2 dưới | migration plan | code review | Docs v1 |
| R-015 | Agent sau biết tiếp tục | `00`, `17`, `18`, `23`–`33` | root `AGENTS.md` + ordered current-topic tickets | reading order + one unambiguous current ticket | Verified; WP5 `001..014` DONE, only `RB-WP6-001` READY (refinement-only) |
| R-016 | Thuật ngữ dễ hiểu | `22` | glossary | doc review | Docs v1 |
| R-017 | Không bias bằng dữ liệu giả | `01`, `10`, `11` | pilot/holdout | prereg audit | Planned |
| R-018 | Tái lập | `06`, `15`, `24`, `26`, `28`, `32`, `33` | hashes/bundle | clean reproduction | WP1–WP5 verified at mechanical gates; paired + independent source/assembly/hash-bound WP5 artifacts verified; experiment bundle still WP6+ |

## 2. Những gì cần thêm

### Source projects đã scaffold trong WP0

- `RideBound.Contracts`
- `RideBound.Domain`
- `RideBound.Application`
- `RideBound.Algorithms`
- `RideBound.Solvers.OrTools`
- `RideBound.Infrastructure`
- `RideBound.Runner`
- `RideBound.Domain.Tests`
- `RideBound.ArchitectureTests`
- `RideBound.Contracts.Tests` (WP1)
- `RideBound.Application.Tests` (WP2)
- `RideBound.Algorithms.Tests` (WP2)
- `RideBound.Runner.Tests` (WP1)

ADR-025 khóa placement WP5: adapter/API/EF/SignalR nằm trong tracked BeGo `src`
và chỉ gọi exact hashed/versioned Runner artifact qua NDJSON. Không thêm project
reference xuyên repository hoặc scaffold `RideBound.Adapters.BeGo`/
`RideBound.Persistence` rỗng. RideBound chỉ nhận harness/docs/evidence cần tái lập;
core không biết BeGo.

### WP0 artifacts đã có

- `RideBound.slnx`, `global.json`, `Directory.Build.props`;
- 7 source project và 2 test project;
- `tests/RideBound.ArchitectureTests/DependencyRuleTests.cs`;
- root `README.md`, `AGENTS.md`, GitHub Actions CI;
- PR template, Dependabot, format/test/coverage/dependency gates, conditional
  Sonar Quality Gate và conditional PR-Agent review;
- `benchmarks/`, `simulators/`, `scripts/`, `artifacts/` có boundary README;
- 8/8 RideBound tests hiện tại cùng baseline 25/25 backend và 7/7 frontend BeGo.

### Runtime/system

- event protocol;
- long-lived runner;
- online vehicle/request state;
- promise ledger;
- certificate validator;
- paired replay harness;
- simulator adapters;
- experiment artifact pipeline.

WP1 runtime boundary đã được cài theo `24`: contract harness,
primitives/envelope, canonical JSON, schema/version policy, hello/init,
event/decision/error shell, hash chain, NDJSON session và idempotent retry đều
có code/test. WP2 hiện đã có typed online payload, vehicle/request state, route,
atomic reducer, physical validator, deterministic candidate/B1 selection,
independent exact-small oracle và online produced decision. WP3 đã thêm ledger,
hard commitment validator/certificate, incident/breach và checkpoint trên cùng
Runner boundary. C1 objective/solver và adapter vẫn thuộc WP4+.

### Product hoặc BeGo integration

- RideBound endpoints/client nằm ở repository BeGo khi WP5 yêu cầu;
- persistence;
- SignalR events;
- promise/revision timeline;
- incident UX.

## 3. Những gì cần bỏ, không dùng hoặc deprecate trong phạm vi RideBound

Không nhất thiết xóa ngay khỏi repo:

- Không dùng `HybridOutingRoutePlanner` làm RideBound engine.
- Không dùng `Session.LatestOptimizationSnapshotJson` làm ledger.
- Không dùng current static assignment làm online baseline chính.
- Không dùng Li & Lim v1 hiện tại làm bằng chứng hard time-window.
- Không dùng synthetic user budget làm nhãn hành vi thật.
- Không dùng per-epoch process spawn.
- Không dùng adapter-specific reimplementation.
- Không dùng một weighted scalar duy nhất để che hard budget dimension.
- Không dùng customer satisfaction/demographic fairness claim khi thiếu data.
- Không đưa OpenRidepoolSimulator/MOSEK cũ vào critical path.

Sau khi RideBound ổn định có thể deprecate benchmark claims quá mạnh trong `benchmarks/public/README.md`; trước đó phải giữ compatibility và thêm caveat, không xóa dữ liệu tùy tiện.

## 4. Functional requirements

| ID | Requirement | Test/evidence |
|---|---|---|
| F-001 | Nhận ordered event batch | WP1 protocol golden + WP2 mapper/atomic reducer verified |
| F-002 | Advance/freeze vehicle state | Domain/reducer/route properties verified cho WP2-003..006 |
| F-003 | Accept/reject/defer request | lifecycle + B1 typed actions/no-reassignment/ACK apply verified |
| F-004 | Generate/evaluate route candidates | deterministic generation, independent physical validation và exact-small differential verified |
| F-005 | Publish promise | WP3 verified: initial/revision actions bind ledger, certificate, state hash và matching ACK |
| F-006 | Track cumulative/switch budgets | WP3 verified: immutable ledger + 10-dimension property/boundary/mutation-killing tests |
| F-007 | Reject candidate vượt budget | WP3 verified: candidate filter + independent physical/lock/budget recomputation and witness |
| F-008 | Handle incident breach riêng | WP3 verified: typed open/resolve, affected riders, immutable breach chronology/budget relation |
| F-009 | Checkpoint/restore | WP3 verified: canonical hash/tamper/reachable-state checks + genesis-suffix process equivalence |
| F-010 | Export transcript/metrics | bundle validation |

## 5. Non-functional requirements

| ID | Requirement | Verification |
|---|---|---|
| N-001 | Deterministic replay | decision hash |
| N-002 | Cross-language portability | same runner |
| N-003 | Auditability | append-only + certificate |
| N-004 | Idempotency | duplicate event tests |
| N-005 | Performance deadline | p95/p99 tests |
| N-006 | Không làm hỏng BeGo độc lập | external BeGo 25+7 regression |
| N-007 | Reproducibility | clean bundle run |
| N-008 | Security/privacy | auth/retention review |
| N-009 | Versionability | schema compatibility tests |

## 6. Research claims → evidence

| Claim | Cần evidence tối thiểu | Không đủ |
|---|---|---|
| Giữ hard budget | validator + property/mutation + zero normal breach | chỉ log |
| Giảm revision | paired Layer 1/2 + CI | một demo |
| Không giảm service đáng kể | prereg non-inferiority | mean difference |
| Portable | same binary ở 3 layers | ba bản code |
| Robust cross-system | Layer 3 paired + capability report | native baseline khác input |
| Fairer burden | distribution metrics, scope rõ | demographic claim |

## 7. Audit rule

Mỗi khi work package hoàn thành:

- đổi `Planned` thành `Implemented/Verified`;
- điền file/test/result cụ thể;
- nếu requirement bị cắt, ghi rationale trong decision log;
- không để status “Verified” chỉ dựa trên docs.

## 8. WP1 ticket traceability

| Ticket | Requirement chính | Evidence dự kiến |
|---|---|---|
| RB-WP1-001 | N-001, N-002, N-009 | ADR-014 + normative decision table `06` mục 2–14; docs verified, runtime vẫn planned |
| RB-WP1-002 | R-003, N-009 | `RideBound.Contracts.Tests`, fixture loader + smoke tests |
| RB-WP1-003 | R-003, N-009 | protocol primitives/envelope codec + structural fixtures/tests |
| RB-WP1-004 | R-003, N-001 | canonical units/JSON + exact UTF-8 golden hex + 63 Contracts tests |
| RB-WP1-005 | R-003, R-008, N-009 | schemas + compatibility matrix/tests; implemented |
| RB-WP1-006 | N-002, N-009 | hello/ack schema, fixture và negotiation tests; implemented |
| RB-WP1-007 | R-018, N-001, N-009 | init/manifest schema, identity validation tests; implemented |
| RB-WP1-008–009 | F-001, N-002, N-009 | event/decision/error schemas/codecs và structural shell tests; verified |
| RB-WP1-010 | N-001, N-003 | domain-separated length-frame SHA-256 code + published vectors; verified |
| RB-WP1-011–013 | N-001, N-004 | async transport, process/session, exact full-batch retry, duplicate/version/gap tests; verified |
| RB-WP1-014 | R-018, N-001, N-007 | exactly 10 fixtures + exact replay/hash/tamper proof; verified |
| RB-WP1-015 | R-003, R-015, R-018 | Q1 Release gate report + `RB-WP2-001` handoff; complete |

### 8.1. RB-WP1-001 closure

| Contract concern | Normative evidence | Trạng thái |
|---|---|---|
| exact schema version/compatibility boundary | `06` mục 2, ADR-014 | Khóa cho v1; implementation ở `003`/`005` |
| unit, rounding và overflow | `06` mục 3, ADR-014 | Khóa cho v1; test vectors ở `004` |
| node/edge-progress position | `06` mục 7/9, ADR-014 | Contract khóa; FleetPy extraction preflight ở WP7 |
| batch/epoch/event ordering | `06` mục 2/12, ADR-014 | Khóa cho v1; implementation ở `008`/`013` |
| envelope/payload/manifest ownership | `06` mục 2/4.1, ADR-014 | Khóa cho v1 |
| error taxonomy/severity | `06` mục 12.2, ADR-014 | Khóa cho v1; implementation ở `009`/`011`–`013` |
| canonical bytes/hash framing | `06` mục 10, ADR-014 | Khóa cho v1; vectors/code ở `004`/`010` |
| fixture support taxonomy | `06` mục 13, ADR-014 | Khóa cho v1; assets ở `002`/`014` |

`RB-WP1-001` là docs/ADR evidence; runtime verification được bổ sung bởi
`RB-WP1-002..015` và closure evidence dưới đây.

### 8.2. RB-WP1-002–004 implementation evidence

| Ticket | Implementation | Verification | Trạng thái |
|---|---|---|---|
| `002` | `tests/RideBound.Contracts.Tests`, `benchmarks/schemas/fixtures` | fixture path/UTF-8/missing/traversal tests | Implemented; task tests verified |
| `003` | `ProtocolPrimitives`, `ProtocolEnvelope`, `ProtocolEnvelopeCodec` | version, identity, context, round-trip và invalid fixtures | Implemented; task tests verified |
| `004` | `CanonicalUnits`, `CanonicalJson`, canonical input/hex vector | byte equality, culture, Unicode, order, range/overflow tests | Implemented; task tests verified |

Evidence ngày 2026-07-29: Contracts 66/66 và Architecture 7/7 pass, Release
build/format/vulnerability audit pass. Full solution local chưa thể xác nhận xanh
do Windows Application Control chặn 1 Domain smoke với `0x800711C7` sau khi 73
test khác pass; Q1 và các
requirement cấp WP vẫn chưa `Verified` cho tới các ticket còn lại và gate `015`.

### 8.3. RB-WP1-005–007 implementation evidence

| Ticket | Implementation | Verification | Trạng thái |
|---|---|---|---|
| `005` | `benchmarks/schemas/v1`, `ProtocolVersionCompatibility`, version-aware envelope validation | schema ID/ref/inventory, patch/minor/major/unknown-field matrix tests | Implemented; task tests verified |
| `006` | `HelloMessages`, `CapabilityNegotiator`, hello/ack schema + fixtures | required/optional/order/unknown/missing/scale/named-downgrade tests | Implemented; task tests verified |
| `007` | `InitializeRunMessages`, `InitializeRunValidator`, manifest/init schemas + fixtures | identity match/mismatch, unoffered capability, re-init, unit/hash/wall-clock boundary tests | Implemented; task tests verified |

Evidence ngày 2026-07-29: Contracts 95/95, Runner boundary 11/11 và required
full solution 114/114 pass; Release build/format/vulnerability audit pass.
Release-only full-suite attempt có 113 pass và Domain smoke bị Windows
Application Control chặn `0x800711C7`. Đây là schema/handshake/identity evidence,
không tự nó là deterministic decision hash hoặc executable NDJSON evidence;
các phần đó được đóng riêng bởi `008–015` dưới đây.

### 8.4. RB-WP1-008–015 Q1 closure evidence

| Ticket | Implementation | Verification | Trạng thái |
|---|---|---|---|
| `008` | `EventBatchMessages`, event schemas, `EventBatchOrderingValidator` | vocabulary, empty/order/gap/overlap/epoch/time/overflow tests | Implemented/Verified |
| `009` | `DecisionMessages`, `ErrorMessage`, decision/error schemas | not-produced shell, hash classification, stable error disposition/sanitization tests | Implemented/Verified |
| `010` | `ProtocolHash` + `fixtures/hash/protocol-hash-vectors.json` | genesis, next-chain, order/tamper và exact lowercase SHA-256 vectors | Implemented/Verified |
| `011` | `NdjsonReader`, `NdjsonWriter` | LF/CRLF/UTF-8/size/EOF/flush/cancellation memory tests | Implemented/Verified |
| `012` | `RunnerSession`, `RunnerHost`, executable `Program` | state edges, memory pipe và real child-process stdout tests | Implemented/Verified |
| `013` | bounded one-response cache + failure lifecycle | exact canonical envelope+payload retry/no-advance; changed payload/time, hash/version/epoch/sequence tests | Implemented/Verified; ADR-017 |
| `014` | `fixtures/golden/required` và `fixtures/runner` | exactly 10 metadata/input pairs; replay twice in memory + two clean processes exact output/hash; tamper changes hash | Implemented/Verified |
| `015` | ADR-016, Q1 report và `tasks/25-wp2-online-state-refinement.md` | Release full solution + docs/artifact audit | Complete |

Evidence ngày 2026-07-29:

- Release full solution tại mốc đóng Q1: 157/157 pass — Contracts 114, Runner
  35, Architecture 7, Domain 1;
- inventory hiện tại là 161 sau assertion đồng bộ vocabulary, changed-time retry
  regression và clean-process replay; Release pass Contracts 115/115, Runner
  38/38, Architecture 7/7 và Domain 1/1;
- `dotnet format ... --verify-no-changes`: pass;
- Release build: 0 warning, 0 error;
- NuGet direct/transitive vulnerability audit: không có package bị báo;
- historical Release full-suite attempt tại Q1 pass 123 test trước khi toàn bộ
  35 Runner test bị chặn lúc nạp fresh Runner.dll; tại thời điểm đó Runner
  source/test không đổi từ mốc pass 35/35;
- historical Debug full-suite tại Q1: Architecture 7 pass; Domain bị chặn;
  Runner báo 5 pass/30 load-policy failure và Contracts báo 15 pass/85
  load-policy failure trước khi hoàn tất inventory. Enterprise Code Integrity
  policy `0283ac0f-fff1-49ae-ada1-8a933130cad6` chặn fresh DLL với
  `0x800711C7`; event 3033/3077 xác nhận signing-level policy. Tại lần WP1
  revalidation sau đó, Debug pass 123 non-Runner test rồi fresh Runner assembly
  lại bị policy chặn; Release pass 161/161.

Phạm vi được Verified ở Q1 là schema/canonical hash/runner lifecycle/replay.
R-003 chưa chứng minh same binary trong adapter, N-002 chưa chứng minh portable
cross-system và N-007 chưa phải experiment bundle hoàn chỉnh. Trạng thái
F-001–F-004 sau WP2-002..006 được cập nhật riêng dưới đây; F-005–F-010 và
ledger/certificate/online selection vẫn Planned.

## 9. WP2 ticket traceability

| Ticket | Requirement chính | Evidence dự kiến/trạng thái |
|---|---|---|
| RB-WP2-001 | R-015, F-001–F-004, N-001 | ADR-018 + execution plan `26`; refinement Done, no WP2 code |
| RB-WP2-002 | F-001, F-002, F-003, N-001, N-009 | Implemented/Verified: typed contracts, 16 schemas, bootstrap/two-epoch fixtures |
| RB-WP2-003 | F-002, F-003, N-001 | Implemented/Verified: Domain lifecycle matrix + aggregate atomic tests |
| RB-WP2-004 | F-002, F-004, N-001 | Implemented/Verified: prefix/suffix/leg/no-op/progress properties |
| RB-WP2-005 | F-001–F-003, N-001, N-004 | Implemented/Verified: Runner mapper + atomic reducer/replay/ack tests |
| RB-WP2-006 | F-002, F-004 | Implemented/Verified: physical mutation/generated/witness tests |
| RB-WP2-007 | F-004, N-001 | Implemented/Verified: deterministic insertion/count/order/cap/no-op/prune tests |
| RB-WP2-008 | F-003, F-004, N-001 | Implemented/Verified: B1 accept/reject/defer, no-reassignment, validator recheck và stable selection |
| RB-WP2-009 | F-004, N-001 | Implemented/Verified: independent exact-small 32 seeds, generator/selection gap 0 |
| RB-WP2-010 | R-003, F-001–F-004, N-001, N-004, N-009 | Implemented/Verified: produced B1 decision, ACK-only apply/hash/retry và Q1 conformance mode |
| RB-WP2-011 | R-018, F-001–F-004, N-001, N-007 | Implemented/Verified: four-epoch two-clean-process replay/final hash/tamper proof |
| RB-WP2-012 | R-015, R-018 | Done: ADR-020, WP2 gate report + only `RB-WP3-001` refinement handoff |

### 9.1. RB-WP2-001 closure

- Domain sở hữu state/invariant; Application sở hữu reducer/orchestration;
  Runner map Contracts sang internal events.
- Batch apply nguyên tử và state/plan chỉ commit tại `decisionApplied`.
- Route tách exact frozen prefix/mutable suffix và có no-op candidate.
- O-001 đóng cho WP2: incumbent accepted request không đổi vehicle.
- B1 chỉ có physical constraints/accepted preservation/cost/tie-break; chưa có
  commitment constraint.
- Ordered queue và acceptance criteria đầy đủ nằm trong
  `tasks/26-wp2-online-baseline-ticket-plan.md`.
- Tại mốc refinement, ticket implementation duy nhất `READY` là
  `RB-WP2-007`; ticket này nay đã `DONE` theo mục 9.3.

### 9.2. RB-WP2-002–006 implementation evidence

| Ticket | Artifact chính | Verification |
|---|---|---|
| `002` | `OnlineEventModels`, strict payload codecs/schema và `fixtures/wp2` | typed round-trip, schema/runtime vocabulary, invalid unknown/null/fraction/duplicate/range |
| `003` | `RideBoundRun`, `RideRequest`, `VehicleState` | exhaustive transition table, duplicate/stale/atomic boarding, accepted-never-rejected |
| `004` | `RoutePlan`, `RouteStop`, `RouteLeg` | exact prefix/no-op/version/progress, generated suffix preservation |
| `005` | `OnlineEventMapper`, internal events, `EventReducer`, coordinator | published two-epoch map/reduce/ack, invalid-last-event rollback, replay, snapshot hash/version |
| `006` | `PhysicalPlanValidator` + travel lookup port | 24 stop permutations và mutations cho mọi physical dimension trong scope |

Evidence ngày 2026-07-29:

- required Debug command `dotnet test RideBound.slnx`: 278/278 pass;
- Release full solution: 278/278 pass — Contracts 127, Domain 89,
  Application 13, Runner 42, Architecture 7;
- Domain/Application không có Contracts/framework/simulator/solver dependency;
- Q1 exact transcript/hash/retry tests tiếp tục pass;
- Tại mốc `002..006`, F-001/F-002 và physical-evaluation phần F-004 đã
  implemented/verified; F-003/B1 và candidate generation khi đó còn chờ
  `RB-WP2-007..008`. Trạng thái closure hiện tại nằm ở mục 9.3.

### 9.3. RB-WP2-007–012 implementation/closure evidence

| Ticket | Artifact chính | Verification |
|---|---|---|
| `007` | `InsertionCandidateGenerator`, canonical candidate/stop identity, schedule evaluator | empty/one-stop/two-request counts, exact cap fail, bounded no-op retention, prefix/no-mutation/prune witness |
| `008` | `CandidateFleetSelector`, `RollingCostPolicy`, decision-state staging | lower cost/tie/request uniqueness, accept/reject/defer, incumbent preservation, independent revalidation, ACK-only commit |
| `009` | test-only `ExactSmallOracle` | 32 deterministic seeds; feasible set/cost/accepted/outcome equal, generator gap 0, selection gap 0 trong bound 2×2 |
| `010` | typed online action codecs/schemas, online `RunnerSession`, full state canonicalizer | produced actions, exact retry, premature batch reject, matching/wrong ACK, two-epoch chain, tamper hash, Q1 named conformance |
| `011` | `benchmarks/scenarios/wp2-tiny` + `run-wp2-tiny-demo.ps1` | two byte-exact clean processes, accept/board/capacity-reject/drop/alight, final hash locked |
| `012` | ADR-020 + docs `00/15/16/18/19/23/26/27` | scope/gate/next-action audit; one WP3 refinement ready |

Evidence ngày 2026-07-30:

- logical inventory 333 — Contracts 128, Domain 89, Application 15,
  Algorithms 45, Runner 49, Architecture 7;
- required Debug `dotnet test RideBound.slnx` pass 333/333;
- Release xUnit pass Contracts 128, Domain 89, Architecture 7; Application 15,
  Algorithms 45 và Runner non-process 46 pass bằng policy-safe bundles, ba
  Runner child-process checks pass riêng;
- Release `--warnaserror` build và format verification pass;
- tiny demo chạy 2/2 clean self-contained process, byte-exact; final hash
  `56825f3591fb5d10f4c258d2c05897c016d82cb91c1318ffa23731c920146680`;
- Release full-solution xUnit bị Windows Application Control chặn fresh unsigned
  Application/Runner DLL bằng `0x800711C7` trước assertions ba suite. Đây là
  environment exception, không được tính là Release xUnit pass;
- F-001–F-004 và phần physical/B1 của Q2 đã implemented/verified trong published
  bounds. F-005–F-009, P1/P2/P3 commitment, ledger/certificate/checkpoint vẫn
  thuộc WP3; C1/OR-Tools thuộc WP4.

## 10. WP3 traceability

| Ticket | Requirement chính | Evidence/trạng thái |
|---|---|---|
| RB-WP3-001 | R-007–R-009, R-015, F-005–F-009, N-003/N-004/N-007 | DONE: ADR-021 + queue `28` |
| RB-WP3-002 | R-007/R-008, F-005/F-006, N-009 | DONE: promise/policy/vector types + Domain tests |
| RB-WP3-003 | R-003/R-008, F-004/F-005, N-001/N-002 | DONE: shared schedule, promise projection, 45 Algorithms regression |
| RB-WP3-004 | R-007/R-008, F-005/F-006, N-001/N-003 | DONE: all-dimension three-way delta/distance witness |
| RB-WP3-005 | R-007/R-008, F-005/F-006, N-003/N-004 | DONE: initial/revision ledger, P1 conservation, pending/ACK atomicity |
| RB-WP3-006 | R-007/R-009, F-006/F-007, N-003 | DONE: exact boundary cho 10 dimensions, zero/unbounded, 441 monotonic samples |
| RB-WP3-007 | R-007/R-009, F-007, N-003 | DONE: accepted/onboard/freeze/final-confirmation lock witnesses |
| RB-WP3-008 | R-009, F-008, N-003/N-004 | DONE: incident lifecycle, affected-rider derivation, immutable breach ledger/chronology |
| RB-WP3-009 | R-007–R-009, F-007, N-003 | DONE: independent physical→state-boundary→lock→budget validator and candidate filter |
| RB-WP3-010 | R-009, F-005/F-007/F-008, N-001/N-009 | DONE: strict certificate/action codecs and schemas with cross-binding checks |
| RB-WP3-011 | R-003/R-007–R-009, F-005–F-008, N-001/N-004/N-009 | DONE: commitment mode, named policy hash, atomic publication/state/hash/ACK |
| RB-WP3-012 | F-009, N-001/N-004/N-007/N-009 | DONE: canonical checkpoint/restore, hash/tamper/reachable-state validation |
| RB-WP3-013 | R-007–R-009/R-018, F-005–F-009, N-001/N-003/N-007 | DONE: all-dimension mutations, 64×12 ledger histories, 16-seed exact-small P2/P3, clean replay |
| RB-WP3-014 | R-007–R-009/R-015/R-018, F-005–F-009 | DONE: ADR-022 closure, code/research review and `RB-WP4-001 READY` |

Evidence closure: inventory 414 — Contracts 133, Domain 134, Application 34,
Algorithms 48, Runner 58, Architecture 7. Required `dotnet test RideBound.slnx`
pass 414/414 ngày 2026-08-03; policy-safe 54 Runner non-process methods và bốn
clean-process cases vẫn là evidence bổ sung. Release build `--warnaserror`,
format, WP1/WP2/WP3 exact replay, checkpoint suffix, schema/link/dependency/diff
audit được ghi trong `18`. O-002/O-003 vẫn để WP8; không có runtime user-default
giả. Các lần `0x800711C7` trước đó chỉ là historical host-policy record.

## 11. WP4 traceability

| Ticket | Requirement chính | Trạng thái/evidence dự kiến |
|---|---|---|
| RB-WP4-001 | R-003/R-004/R-007–R-009/R-012/R-013/R-015, F-004–F-007, N-001–N-005/N-007/N-009 | DONE: Browser research + ADR-023 + ordered queue `30`; không production code trong refinement |
| RB-WP4-002 | F-004, N-001/N-002/N-005/N-009 | DONE: canonical solver-neutral problem/solution/port; exact assignment/request constraints, ordered Sum/Maximum objectives, deterministic budget, truthful bound/gap/status; 435/435 |
| RB-WP4-003 | F-004, N-001/N-005 | DONE: conservative backward slack, full cache key/invalidation, executable current-node hold + physical/exact-service revalidation; 444/444 |
| RB-WP4-004 | F-004, N-001/N-005 | DONE: deterministic best-first/work cap, exact fail-closed, request/raw-unknown/feasible-cap loss count+digest; 449/449 |
| RB-WP4-005 | R-007/R-008, F-003/F-004, N-001/N-003 | DONE: B2 non-pruning revision lexicographic + B3 explicit inclusive freeze/unbounded cumulative; 16-seed raw preservation; 458/458 |
| RB-WP4-006 | R-007/R-008, F-003/F-004, N-001/N-003 | DONE: B4 atomic one-pair same-vehicle waiting-incumbent repair, exact/bounded repair-loss accounting, order-sensitive search identity; 465/465 |
| RB-WP4-007 | R-007/R-008, F-003–F-007, N-001/N-003/N-005 | DONE: versioned canonical B5 pool, dominance/diversity/consensus, executable alternative rebase, checkpoint tamper gates; 477/477 |
| RB-WP4-008 | R-007/R-008, F-003–F-007, N-001/N-003 | DONE: one-pass hard gate + exact cumulative worst PPM + ordered revision/cost/ID; unbounded C1=B1; 483/483 |
| RB-WP4-009 | R-007/R-008, F-003–F-007, N-001/N-003 | DONE: explicit 10-vector warning profile/excess before revision, same C1 hard set, disabled C2=C1; 489/489 |
| RB-WP4-010 | R-003/R-013, F-004, N-001/N-002/N-005/N-009 | DONE: isolated pinned OR-Tools 9.15.6755 CP-SAT adapter, exact constraints, multi-pass optimum fixing, deterministic budgets, truthful status/bounds; 495/495 |
| RB-WP4-011 | R-003/R-013, F-003–F-007, N-001–N-005/N-009 | DONE: separate stage work budgets/loss, independent incumbent validation, no-op→single-request fallback, fail-closed exhaustion; 507/507 |
| RB-WP4-012 | R-003/R-013, F-003–F-007, N-001–N-005/N-009 | DONE: seven-name registry, manifest/config hash binding, exact solver objective mapper, Runner validator/certificate/hash/ACK/checkpoint/CLI integration; 523/523 |
| RB-WP4-013 | R-012/R-015/R-018, N-001/N-003/N-005/N-007 | DONE: 64-seed B1 oracle; 64-seed production C1 mapper + actual OR-Tools independent differential, all levels optimal/gap 0; hard-gate mutation, actual bounded-loss propagation, synthetic 4–128 option curve; 557/557 |
| RB-WP4-014 | R-012/R-015/R-018, N-001/N-003/N-005/N-007 | DONE: ADR-024, source/config/Runner/claim audit, final WP1–WP4 review, all quality gates và only `RB-WP5-001 READY` |
| RB-WP5-001 | R-003/R-013/R-015/R-018, N-001/N-003/N-007/N-009 | DONE: ADR-025 khóa BeGo source/provenance, Runner ownership, transaction/recovery, persistence, rollback và paired Layer-1 protocol; no production implementation |
| RB-WP5-002 | R-002/R-013/R-015, N-003/N-004/N-009 | DONE: pure BeGo Application contracts/ports; exhaustive run/operation transition, idempotency fingerprint, strict UTF-8/frame/hash/order/checkpoint guards; 32 targeted, BeGo 57/57, RideBound 557/557 |
| RB-WP5-003 | R-002/R-014, N-003/N-004/N-008/N-009 | DONE: 11-table EF/migration, composite same-run FK, canonical bytes/range/time columns, five append-only DB triggers, guarded rollback; PostgreSQL 17 real gate |
| RB-WP5-004 | R-002, N-001/N-003/N-004/N-005 | DONE: T1 row-locked intake, exact frame binding/idempotency, bounded DB queue, ordered `SKIP LOCKED` lease/reclaim with database time; PostgreSQL race 5/5, Release 40/40, BeGo 64 pass/1 opt-in skip, RideBound 557/557 |
| RB-WP5-005 | R-002/R-003, N-001/N-002/N-005/N-009 | DONE: pinned hash/core, bounded long-lived Runner pool, strict NDJSON/schema/context, timeout/tree cleanup, exact ACK/checkpoint critical section; real published Runner online gate |
| RB-WP5-006 | R-002/R-003/R-013, N-001/N-002/N-008/N-009 | DONE: immutable pre-I/O source capture, run-local HMAC pseudonyms, restricted raw-ID links, E7/ms ties-to-even, complete bounded directed matrix, field provenance, exact negotiated manifest/domain hash; real Runner bootstrap; BeGo 98/98 |
| RB-WP5-007 | R-002/R-014, N-004/N-006/N-008/N-009 | DONE: host/member auth, strict bounded HTTP DTO, semantic fingerprint independent server sequence, composite advisory-locked create idempotency, exact cached response, RFC Problem Details/rate gate, PostgreSQL→published Runner API path; 116/116 |
| RB-WP5-008 | R-002/R-003/R-018, F-001/F-005–F-009, N-001/N-003/N-004/N-007 | DONE: atomic T2 decision/certificate/projection/outbox, fenced matching ACK/T3 checkpoint, exact fresh reconstruction, typed divergence; 8 real crash windows, BeGo 125/125, RideBound 557/557 |
| RB-WP5-009 | R-002/R-013, N-003/N-004/N-005/N-006 | DONE: per-run-head DB-time lease/fence, no-lock SignalR I/O, stable sequence/message/hash wire envelope, client duplicate/stale suppression, safe payload/auth gates; real PG + Runner 131/131 |
| RB-WP5-010 | R-002/R-018, F-010, N-003/N-005/N-007/N-008 | DONE: exact keyset/member ownership, operator-only raw evidence, append-log rebuild/live hash, pseudonymous export, privacy/log mutations, indexed PostgreSQL plan; 138/138 |
| RB-WP5-011 | R-002/R-014, N-006/N-008/N-009 | DONE: default-off DI/API, exact artifact preflight, immutable Shadow/Live namespace, namespace-filtered decision claim, live-only relay, restart recovery, old Session snapshots unchanged; 147/147 |
| RB-WP5-012 | R-003/R-004/R-018, N-001/N-002/N-007/N-009 | DONE: exact source/config/binary preflight; B1/C1 × two clean processes; common normalized input, exact repeat hashes, materialized certificates, independently checked checkpoints, self-verifying source/assembly-bound bundle; BeGo 152/152 |
| RB-WP5-013 | R-012/R-013/R-018, N-001/N-003–N-007 | DONE: 16.384-step independent oracle, exact-set 2/3/4-worker claim, actual process crash at 8+4 boundaries, 5/5 explicit mutants, raw local curves + self-verifying artifact; 153/153 |
| RB-WP5-014 | R-012/R-015/R-018, N-001–N-009 | DONE: source/claim/evidence audit; subject-link append-only, absolute-head-then-Applied/non-null operation outbox và independent scoped per-run batch; WP1–WP5 review/verdict; BeGo 154/154 Debug/Release |
| RB-WP6-001 | R-010/R-011/R-015/R-018, N-001/N-002/N-007/N-009 | READY: refinement-only common scenario/result/metric/failure/bundle harness boundary; no implementation or experiment |

Queue `30` đã complete và ADR-024 đóng WP4. WP3 validator/certificate tiếp tục
là publication gate độc lập. ADR-025/`tasks/32` đã complete và đóng WP5;
`RB-WP5-001..014` Done. `tasks/33` có đúng `RB-WP6-001 Ready`, refinement-only.

WP4 closure evidence: required `dotnet test RideBound.slnx` pass 557/557 ngày
2026-08-03 — Contracts 133, Domain 135, Application 69, Algorithms 134, Solver 6,
Runner 71, Architecture 9. `RB-WP4-002` thêm 20 Application adversarial cases + một
Architecture boundary case; `003` thêm 9 slack/cache/hold mutation-equivalence
cases; `004` thêm 5 priority/conservation/loss/monotonic cases; `005` thêm 8
B2/B3 cases và 1 Domain overflow regression; `006` thêm 7 B4 repair/exclusion/
loss/equivalence cases; `007` thêm 3 Application + 5 Algorithms + 4 Runner cases
cho plan identity/version, dominance/diversity/consensus, work bound,
distinguished-only publication và checkpoint/tamper. Observed wall time không
tham gia deterministic key. `008` thêm 6 Algorithms cases cho C1 objective,
one-pass hard-filter equivalence, exact PPM và unbounded B1 equivalence; `009`
thêm 6 Algorithms cases cho warning ordering/config/hard-set/excess/equivalence;
`010` thêm 5 solver cases cho constraints/objective/status/replay/overflow/budget
và 1 architecture case khóa package vào đúng adapter project; `011` thêm 12
Application cases cho stage accounting, truthful incumbent status, independent
validation, ordered no-op/single-request fallback, exhaustion và loss separation;
`012` thêm 7 Algorithms + 9 Runner cases cho objective mapper, config/hash/manifest
binding, actual OR-Tools/fallback status, ACK transaction, B5 restore và child CLI.
`013` mở B1 oracle lên 64 cases, thêm một 64-seed actual OR-Tools C1 differential,
hard-gate mutation witness, actual bounded omission propagation và synthetic
microbenchmark; `014` audit source/claim/gates và tạo final review folder.
