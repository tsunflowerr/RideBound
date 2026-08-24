# Ma trận truy vết yêu cầu

## 1. Yêu cầu từ mục tiêu dự án

| ID | Yêu cầu | Thiết kế/tài liệu | Artifact dự kiến | Verification | Trạng thái |
|---|---|---|---|---|---|
| R-001 | Hệ thống RideBound độc lập | `05` | `Domain/Application/Runner` trong Git repo riêng | 5 architecture rules + 2 cross-platform path cases | WP0 verified; Linux CI rerun pending |
| R-002 | Gắn được vào BeGo | `02`, `14`, `32` | BeGo adapter/API | integration replay | WP5 `002..014` có Application, persistence, intake/lease, Runner, bootstrap/API, fenced T2/T3 recovery, Applied-only outbox/SignalR, rebuildable timeline, default-off Shadow/Live rollout và paired Layer-1 bundle |
| R-003 | Core mang sang benchmark | `05`, `06`, `24`, `26`, `32`, `36`, `40` | contracts + runner | same binary hash | WP1/WP2 verified; WP5 BeGo and WP7 FleetPy use pinned Runner; WP10 RidePy canonical uses same exact Runner tree, representative Layer 3 subset fails for position capability rather than a second decision implementation |
| R-004 | Layer 1 cùng codebase | `09`, `32`, `34`, `39` | B1/C1 chung RideBound engine; BeGo/WP6 paired harness | paired runs | Mechanical same-Runner evidence verified WP5/WP6/WP9 Layer 1; không nâng thành effectiveness |
| R-005 | Layer 2 simulator chung | `09`, `12`, `35`, `36`, ADR-038/048 | FleetPy adapter gọi same Runner | capability/preflight + actual lifecycle/tiny/medium/confirmatory closed-loop + external verifier | WP7 mechanical complete; WP9 H6 FleetPy effectiveness có 100/100 raw bundle verified và kết quả âm có điều kiện |
| R-006 | Layer 3 framework độc lập | `09`, `13`, `40`, ADR-050/051 | RidePy adapter gọi same Runner | exact source/env + canonical + paired Layer 3 gate | WP10 complete negative: source/env/same-Runner/canonical PASS; paired subset FAIL CLOSED do `nodeOnly` concurrent mid-edge; Layer 3 claim not established |
| R-007 | Giới hạn cumulative revision | `04`, `07`, `28` | ledger/validator | property/mutation tests | WP3 implemented: independent hard-vector recomputation + immutable ledger + certificate |
| R-008 | Nhiều chiều promise | `04`, `07`, `28` | promise vector | golden fixtures | WP3 implemented: 10 dimensions, projection, three-way delta, strict wire actions và tiny replay |
| R-009 | Certificate/witness | `07`, `28` | certificate DTO/validator | invalid-plan mutations | WP3 implemented: physical→lock→budget stages, normal/witness body và decision/publication cross-binding |
| R-010 | Đánh giá bằng data rõ | `10`, `18`, `33`, `34`, WP6 contract | immutable public-source registry + deterministic normalization + scenario identity | source checksum/license/schema/vector gates | WP6 `002..004` complete: strict contracts, verified FleetPy source and exact tiny/medium canonical derivatives |
| R-011 | Metric/statistics rõ | `11`, `18`, `33`, `34`, WP6 contract, WP8/WP9 reports | raw observations + explicit denominator/missingness + independent oracle | golden/mutation/recompute gates | WP9 H6 complete: exact two-panel service/burden gates, locked/earned split, robustness non-rescue và finite-panel precision boundary |
| R-012 | Dùng paper nhưng không làm lại | `03`, `21` | claim ledger + full-PDF provenance | novelty re-audit | WP1–WP8 mapped; full PDFs Alonso-Mora/Gschwind/Simonetto/Engelhardt/Zalesak/Schulz read and hashed; only exact same-state reuse applied, no random/direction/sparse prune |
| R-013 | Nêu công nghệ và tối ưu | `05`, `08`, `12`–`14` | projects/adapters | build/performance | WP4 solver + WP5 DB/Runner/concurrency/local curves implemented; no SLA claim |
| R-014 | Nêu thêm/bỏ gì | `02`, mục 2 dưới | migration plan | code review | Docs v1 |
| R-015 | Agent sau biết tiếp tục | `00`, `17`, `18`, `23`–`40` | root `AGENTS.md` + ordered queue | reading order + closed queue/evidence/review | WP1–WP10 DONE; WP10 negative result/next action/receipts được ghi ở ADR-051 và report |
| R-016 | Thuật ngữ dễ hiểu | `22` | glossary | doc review | Docs v1 |
| R-017 | Không bias bằng dữ liệu giả | `01`, `10`, `11`, WP8 prereg/amendments | pilot/holdout | prereg + leakage audit | Pilot ngày 11–12 tách holdout ngày 14–18; node-cap, integrity và Runner-repin amendments đều pre-outcome confirmatory; margin 1 pp giữ dù pilot bất lợi |
| R-018 | Tái lập | `06`, `15`, `24`, `26`, `28`, `32`–`39`, WP6/WP7/WP8/WP9 evidence | identities + seed tree + strict bundle + pinned FleetPy/Runner + repository content inventory | tamper/no-extra/source/assembly/hash + actual lifecycle/verifier + freeze verifier | H6 recompute 30 file hashes + 5 tree seals; independent verifier PASS 100 raw bundle, cross-panel identity và deterministic repeat; không claim independent reproduction |
| R-019 | Fair paired effectiveness design | `11`, `18`, `37`–`39`, ADR-048 | oriented pair + fixed panel + locked/earned decomposition | arm swap/config/label/scenario/inventory mutations | WP9 complete: cả hai service gate FAIL, burden gate PASS; panel không pool và robustness không rescue |

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
Runner boundary. WP4 đã hiện thực C1/objective/solver; WP5 đã hiện thực BeGo adapter;
WP6 đã hiện thực common harness. WP7 đã thêm FleetPy closed-loop adapter chỉ gọi
published external Runner; closed-loop evidence là mechanical Layer 2, không phải
effectiveness evaluation.

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
| F-010 | Export transcript/metrics | WP5 timeline/export và WP6 immutable transcript, observation index, 36-metric production/oracle rows, strict bundle verified |

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
| RB-WP6-001 | R-010/R-011/R-015/R-018, N-001/N-002/N-007/N-009 | DONE: ADR-026 + equivalent contract v1 + primary-source evidence + ordered queue; no implementation/experiment result |
| RB-WP6-002 | R-010/R-018, N-001/N-007/N-009 | DONE: 10 strict codecs/models + semantic validator, Draft 2020-12 schema inventory, fixtures, two-process six-identity vector; targeted 28/28, full 586/586 |
| RB-WP6-003 | R-010/R-018, N-001/N-007/N-008/N-009 | DONE: exact Zenodo length/MD5/SHA registry, resumable verified content cache, safe ZIP extraction, actual 335-member public receipt; targeted 26/26, full 612/612 |
| RB-WP6-004 | R-010/R-018, N-001/N-002/N-007/N-009 | DONE: ADR-027 acyclic scenario/report identity; verified-member FleetPy normalizer, SCC/dense-node-pool/HMAC/ties-even/conservation; exact 8/2/16/240 + 128/32/96/9120 derivatives; full 619/619 |
| RB-WP6-005 | R-010/R-018, N-001/N-002/N-007/N-009 | DONE: cross-process framed HMAC/int32 vectors, effective binding + strict pairing compiler, permutation/parallel counterbalanced full grid; targeted 32+37, full 627/627 |
| RB-WP6-006 | R-010/R-018, N-001/N-002/N-005/N-007/N-009 | DONE: ADR-028/contract 1.0.2; exact runtime/deploy/config/source pre/postflight, canonical Runner protocol + independent hash-chain checks, typed bounded process-tree/stream evidence; 15 supervisor cases, full 647/647 |
| RB-WP6-007 | R-010/R-011/R-018, N-001/N-002/N-005/N-007/N-009 | DONE: ADR-029/contract 1.0.3; immutable plan/run/raw/index store, typed terminal/log chain, crash/seal/concurrency recovery, plan-bound protocol/artifact verification and full-grid authorized rerun; Benchmarking 77/77, full 673/673 |
| RB-WP6-008 | R-010/R-011/R-018, N-001/N-002/N-005/N-007/N-009 | DONE: 36-definition registry; arrival-cohort/decision-window/lifecycle/resource calculator; 132 exact rows; no-reference external oracle; request/action/promise/vector/window/order/resource/overflow mutations; Benchmarking 86/86, full 682/682 |
| RB-WP6-009 | R-010/R-011/R-015/R-018, N-001/N-002/N-005/N-007/N-009 | DONE: deterministic strict BagIt builder; logical/source/runtime/run-store provenance; 10-stage semantic verifier; fresh-process immutable sidecar; missing/extra/tamper/path/reparse/grid/transcript/log/metric mutations; Benchmarking 92/92, full 688/688 |
| RB-WP6-010 | R-010/R-011/R-015/R-018, N-001/N-002/N-005/N-007/N-009 | DONE: ADR-032; source-locked/generated claim profile/report; scoped Unicode/confusable-aware typed checker; exact stage-10 recomputation; forbidden/caveat/report/provenance/profile mutations; Benchmarking 95/95, full 691/691 |
| RB-WP6-011 | R-010/R-011/R-015/R-018, N-001/N-002/N-005/N-007/N-009 | DONE: ADR-033/contract 1.0.4; B1/C1 × 3 exact external Runner runs in two clean Release processes; real non-zero decision delta, exact semantic identities, oracle summaries, typed mutation matrix, verified mechanical bundle; Benchmarking 104/104, full 705/705 |
| RB-WP6-012 | R-010/R-011/R-015/R-018, N-001/N-002/N-005/N-007/N-009 | DONE: ADR-034/contract 1.0.5; verified Zenodo + two-clean-root exact 128/32/96/9120 derivative; B1/C1 × 3 in two fresh exact Runner/store/oracle/verifier processes; exact semantic identities, valid strict bundles, explicit nonphysical instant-drain boundary; full 710/710 |
| RB-WP6-013 | R-010/R-011/R-015/R-018, N-001/N-002/N-005/N-007/N-009 | DONE: ADR-035/contract 1.0.6; executable 1+3 warm-up/measured grid, complete preflight, 10-doc permutation/parallel, 21 failure-stage/8 exclusion plus process/store/metric/bundle/claim/source matrix; medium D/E 8/8 semantic exact, strict verify valid, full 770/770 |
| RB-WP6-014 | R-010/R-011/R-015/R-018, N-001/N-002/N-005/N-007/N-009 | DONE: ADR-036; source/claim audit, fresh tiny A + medium H/I exact-source semantic repeat, external verifier, final WP1–WP6 review, exact 770/770 |

Queue `30` đã complete và ADR-024 đóng WP4. WP3 validator/certificate tiếp tục
là publication gate độc lập. ADR-025/`tasks/32` đã complete và đóng WP5;
`RB-WP5-001..014` Done. `RB-WP6-001..014` Done; WP7 `RB-WP7-001..014` Done và
ADR-038 đóng mechanical Layer-2 theo `tasks/36`. WP8 `001..014` Done bằng ADR-043;
WP9 queue `tasks/39` đang active, `RB-WP9-001/003` Done, `RB-WP9-002` Ready.

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

## 15. ADR-039 traceability — semantic lock và hot-path closure

ADR-039 không thêm requirement mới. Nó đóng hai lỗ hổng truy vết: một nhóm hành vi đã
tồn tại trong source nhưng chưa có quyết định nào khóa, và một cổng hiệu năng chưa từng
được đo.

| Hành vi | Nơi thực thi | Bằng chứng |
|---|---|---|
| `initialPromiseTrigger` hai giá trị, mặc định `initial-acceptance` | `Wp4RunnerConfiguration.Decode`, `CommitmentDecisionValidator` | `Wp4RunnerConfigurationTests`; config thiếu field giữ nguyên content hash |
| Provisional offer không mở promise và bị loại khỏi hard/warning scope | `CommitmentDecisionValidator`, `HardVectorCandidateAssessor` | tiny clock actual: đúng một publication `INITIAL_BOOKING_CONFIRMATION` |
| Promise mở tại `Accepted → WaitingPickup/Onboard`, kể cả khi board cùng batch | `PromiseProjector` khôi phục pickup đã thực hiện từ frozen prefix | `PromiseProjectorTests` |
| Lock so trên trục exogenous → candidate | `CommitmentLockEvaluator` | `CommitmentLockEvaluatorTests`; `docs/07` §10 |
| `OfferDeclined` sau accept là cancel-after-acceptance | `EventReducer` | `EventReducerTests` |
| `C1_VEHICLE_HAS_NO_FEASIBLE_CANDIDATE` fail-closed, witness typed | `HardVectorCandidateAssessor`, `CommitmentFailureCodes` | `C1_fails_closed_with_a_typed_witness_when_a_vehicle_loses_every_candidate`, `C1_fails_closed_when_a_vehicle_set_carries_no_candidate_at_all` |
| `--maximum-line-bytes`, `--manifest-solver-seed` | `Program`, `Wp4RunnerConfiguration.CreateSolverPolicyOptionsForRun` | `Wp4RunnerConfigurationTests`, `Wp4RunnerIntegrationTests` |
| Event-induced plan update chỉ khi opt-in và route thực sự đổi | `OnlineDecisionActionMapper` | `Wp4RunnerIntegrationTests` |
| Retention legacy fast path giữ nguyên hot path WP1–WP6 | `CandidatePortfolioRetainer` | `CandidatePortfolioRetainerTests` |
| Tối ưu hot path không đổi kết quả | `CandidateIdentity`, `ForwardSlackCacheKey`, `RoutePlan.Create`, `InsertionCandidateGenerator` | `CandidateSearchWorkProfileTests` khóa work unit / evaluated path / feasible-before-cap / omitted path / retained count / số slack profile |

Closure evidence: required `dotnet test RideBound.slnx` pass `798/798` ngày 2026-08-17 —
Contracts 135, Domain 136, Application 73, Algorithms 154, Runner 77, Benchmarking 135,
Benchmarking.Contracts 71, OrTools 7, Architecture 10. Pinned Python adapter `50/50`.
Actual FleetPy gates chạy lại trên Runner v8; chi tiết ở
[`wp7-015-hot-path-and-semantics-closure-evidence-2026-08-17.md`](benchmarking/wp7-015-hot-path-and-semantics-closure-evidence-2026-08-17.md).

## 16. ADR-043 traceability — WP8 closure và WP9 freeze

| Requirement | Cài đặt/evidence | Gate |
|---|---|---|
| Experimental unit không nhân seed/rider | `ExperimentalUnitModels`, `wp8-007`, fixed panel 20 cell | same-unit/orientation/denominator mutations |
| Primary burden và service gate | production calculator + BCL-only oracle + `wp9_fixed_panel_analyze.py` | canonical differential, strict 1 pp boundary, burden không cứu service |
| Fairness treatment-only | `EffectivePolicies`, audited B1/C1 configs | direct policy tests + B1 tight/unbounded behavioral falsification |
| Locked/earned disclosure | pickup/drop exact components trong primary analyzer | treatment pickup khác zero fail-closed; exact integer decomposition |
| Bundle đúng arm/cell/source | execution plan + label + scenario SHA + repository inventory | swapped arm/seed/job/path/source mutations |
| Reproducibility freeze | `WP8-011a/011b/011c`, `freeze-receipt-v3.json`, freeze verifier | 25 explicit file/Runner hashes + derivative/scenario/Runner tree seals PASS |
| Claim boundary | `docs/03`, `docs/21` full-PDF audit, WP1–WP8 final review | không novelty/population/SLA/satisfaction claim |

Baseline hiện hành trước WP9 smoke: `.NET 840/840`, pinned Python 77/77, H4
`2f7e6bf36c16784e06cb3266f9764f3103f2de6fc931f3c8e023bdc1a81a32dd`;
Layer-1 mechanical 8/8 PASS và không được nâng thành effectiveness evidence.

## 17. ADR-045 traceability — ngữ nghĩa service-quality và capacity stratum

| Requirement | Cài đặt/evidence | Gate |
|---|---|---|
| Safety no-op không bị xoá bởi giao thông | `ProbeServiceQuality` + `ServiceQualityAllowance` trong `PhysicalPlanValidator` | 2 regression Domain (ride-time, pickup-window) + 1 regression Algorithms (no-op sống sót) |
| Không rửa vi phạm qua breach | bound `max(contractual, exogenous)` dùng chung ở validator và `ForwardSlackProfile` | Domain: candidate detour bị prune, `Expected` là bound hiệu lực; Algorithms: mọi prune `MAX_RIDE_TIME` đều strictly worse than no-op |
| Structural vẫn fail-closed | probe trả witness thay vì allowance khi lộ trình vi phạm structural | Domain: `CAPACITY` trên active route vẫn fail, allowance rỗng |
| Request mới không được miễn trừ | allowance chỉ có entry cho request trên lộ trình đang chạy | Domain: scoping theo request/dimension; không breach thì `Strict` |
| Đối xứng hai arm | allowance là hàm thuần của `(run, vehicle, travel, time)`, không đọc policy/ledger | ADR-045 §6; bốn call site downstream cùng `ValidateWithExogenousRelief` |
| Breach được ghi nhận, không nuốt | `ExogenousServiceQualityBreach` trong `CandidateGenerationDiagnostics` | Algorithms: breach báo đúng contractual/exogenous; không breach thì rỗng |
| Kết luận có điều kiện theo năng lực | `wp8-011d`, `wp8-010` gate theo từng stratum, `wp8-008` bảng 4/8/16 xe | 20 cell × 2 arm × 2 stratum; mẫu số 2160 riêng từng stratum; cấm claim phổ quát |
| Độ chính xác đạt được được công bố | `wp8-007` đo lại độc lập panel; `wp8-010` bắt in ~1,40 pp cạnh mọi gate | 2.157/2.160 request phân biệt; 5 travel realization; sàn sign-flip 0,03125 |
| Falsification đúng nhân quả | `wp8-010` thay negative control bằng identity chuỗi demand+travel 4 điều kiện | verifier phải PASS cả 4 trước khi cặp vào estimand |

Baseline sau ADR-045: `.NET 851/851` Debug và Release, Release `-warnaserror`
0 warning. `H4` hết hiệu lực vận hành vì ADR-045 và `wp8-011d` đều outcome-bearing;
`RB-WP9-002a` phải repin `H5` trước audited smoke.

Đã đóng bởi ADR-049/RB-WP9-009: evidence v1.1 serialize breach và runtime append
`ExogenousServiceQuality` ledger không charge budget. H6 vẫn dùng v1.0 nên không
có breach count confirmatory; 43 observation từ probe v1.1 chỉ là post-outcome
mechanism evidence.

## 18. ADR-046 traceability — đường phân tích confirmatory

| Requirement | Cài đặt/evidence | Gate |
|---|---|---|
| Analyzer chạy được trên artifact đã đóng băng | `_PRIMARY_ARMS` registry trong `wp9_fixed_panel_analyze.py` | `FrozenManifestBindingTests`: 20/20 cell của manifest thật bind vào plan thật |
| Không phân tích được panel cụt | manifest cell set phải bằng plan primary/robustness cell set, cả hai analyzer | test panel completeness + `denominators differ` |
| Orientation arm không đảo được | `_PRIMARY_ORIENTATION` pin `(b1, c1)` | test swapped orientation bị từ chối |
| Một bundle không phục vụ hai arm | `baselineBundle != treatmentBundle` | test duplicate bundle bị từ chối |
| Hai panel không lẫn nhau | `--panel` parameterize prefix/driver/fixture root; `_validate_frozen_design(plan, panel)` | `CapacityPanelBindingTests`: plan A bị từ chối làm panel B và ngược lại |
| Panel B chia sẻ demand realization của Panel A | đo 20/20 selection frame + request tuple | ghi trong `wp8-011d`; fail closed nếu lệch |
| Freeze bind cả hai panel | `H5` schema 4.0.0: 30 file hash + derivative A/B, scenario plan, Runner tree seal | verifier PASS; 4/4 mutation rejected |

Baseline sau ADR-046: `.NET 851/851` Debug và Release (Release `-warnaserror` 0
warning); pinned Python **86/86**, 0 skip với `RIDEBOUND_FLEETPY_ROOT` đặt sẵn.
`H5=6720accacdb6c1f685d9d27a196a7aa924cada02f535b35d7787074a5e625da6`.

Bài học giữ lại: freeze receipt pin *hash* của một program không chứng minh program
đó *chạy được* trên artifact nó pin. Mọi program nằm trên đường outcome phải có ít
nhất một test thực thi nó trên đúng artifact đã đóng băng.

## 19. ADR-047/048 traceability — H6 và kết quả WP9

| Requirement | Cài đặt/evidence | Gate |
|---|---|---|
| Adapter rounding không lạc quan | `ROUND_FLOOR` trong FleetPy mapping + `DivideRoundUp` Runner | boundary/mutation mapping tests; adapter package tree seal H6 |
| Relief chỉ dành cho safety no-op | changed candidate dùng `ServiceQualityAllowance.Strict` | Algorithms regression; 3/3 diagnostic job từng hỏng hoàn tất |
| Freeze outcome-bearing đầy đủ | H6 schema 5.0.0 | 30 file hash + derivative A/B, scenario, Runner, adapter tree seal PASS |
| Primary result đúng orientation | two canonical panel analyses | 20/20 cell mỗi panel; exact denominators; service và burden gate độc lập |
| Không oversell burden | locked/earned table cạnh service loss | pickup definitional component và zero-burden cell count bắt buộc trong report |
| Robustness không rescue | analyzer output `confirmatoryGate:null` | C1 unbounded/C2/seed19 descriptive only; seed tăng N bằng 0 |
| Reproducibility raw | `wp9_reproducibility_verify.py` + compact JSON receipt | 100/100 bundle; bốn falsification conditions; repeat deterministic; 5 mutations |
| Claim boundary | `wp9-confirmatory-result-2026-08-23.md` | finite panel, 5 travel realization, precision ~1.40 pp, cấm population/SLA/satisfaction |

## 20. ADR-049 traceability — breach evidence/ledger

| Requirement | Cài đặt/evidence | Gate |
|---|---|---|
| Evidence tương thích ngược | mapper v1.1; Contracts/Python nhận 1.0/1.1 | v1.0 H6 pass; unknown version và bad v1.1 shape/value/order fail |
| Không incident giả | `CommitmentBreachKind.ExogenousServiceQuality`, nullable incident chỉ cho kind mới | Domain append succeeds without incident; operational path unchanged |
| Không rửa decision budget | exact no-op exogenous=safety, decision delta zero, budget unchanged | Domain forged projection/delta/budget/witness mutations fail |
| Runtime không nuốt breach | `ExogenousServiceQualityBreachBridge` trước state staging/hash | fail-session on bridge error; real FleetPy 43 evidence = 43 ledger |
| Checkpoint tương thích và strict | old operational shape unchanged; new kind/witness array | exact round-trip + forged serialized budget rejection |
| Outcome boundary | post-outcome evidence report/receipt | 43 observations không trộn H6 và không rescue primary |

## 21. ADR-050 traceability — WP10 RidePy Layer 3 refinement

| Requirement | Cài đặt/evidence | Gate |
|---|---|---|
| Exact independent source | RidePy v2.10.1 `bf1863e…9f14`, exact two submodules, MIT, external checkout | commit + 527-file tree + license/submodule mutation verifier |
| Supported isolated runtime | pinned Linux container base `a365ce6a…0e235` | image build/import/version/runtime receipt; Windows wheel absence recorded |
| Same decision implementation | RidePy adapter calls versioned `RideBound.Runner`; no Python solver/budget/locks | publish-tree pre/post hashes + transcript proof |
| Honest capability | `nodeOnly`, unit party, reassignment false, explicit traffic updates | negotiation/downgrade/mapping/mutation tests |
| Native simulator semantics | subclass `FleetState`, native fast-forward/space/pickup/drop | 2-vehicle/5-request canonical reconciliation |
| Fair Layer 3 comparison | B1/C1 paired on exact same exogenous inputs | frozen subset identity verifier; oriented per-cell analysis |
| Outcome boundary | descriptive heterogeneity only | no pooled H6 gate/CI/SLA/satisfaction/novelty claim |

## 22. ADR-051 traceability — WP10 negative capability closure

| Requirement | Cài đặt/evidence | Gate |
|---|---|---|
| Reproducible source/runtime | external source/env receipt `2b431062…0775`; image `5468b9cb…e573` | source mutations, actual import/Graph smoke và 8/8 verifier tests pass |
| Generic same-Runner client | explicit position/capability parameters, unchanged FleetPy defaults | 14/14 targeted regression; wrong downgrade fails |
| Strict RidePy adapter | identity/time/travel/route mapping; incremental native clock; atomic stoplist apply | pinned-container suite 20/20; no Python solver/lock/budget implementation |
| Canonical equivalence | B1/C1 5/5 completed, 5 pickup + 5 drop, 22 decisions | independent five-class mutation verifier; Runner tree pre/post identity |
| Pre-outcome subset binding | repo manifest `72ca34d2…b337`; external freeze v3 `18a74fa3…6672` | source/image/Runner/adapter/config seals before terminal execution |
| Failure retention/no partial reuse | 22 pass, B1 stress job fail at epoch 17, paired C1 not run | exact failure transcript `0ee5e3ec…a85`; no missing-job denominator laundering |
| Named capability boundary | `RBWP10_NODEONLY_CONCURRENT_MIDEDGE_UNSUPPORTED` | native pickup 116000 ms vs last nodeOnly ETA 178000 ms; no invented progress |
| Descriptive analysis only | 11 valid pairs: B1 54/62, C1 49/62, `−8.06 pp` | strengthened analysis v2 `be3e9077…cca3` binds exact terminal inventory/full Runner/seed; not planned estimand, no CI/pooling/H6 rescue |
| Layer 3 verdict | representative subset gate FAIL CLOSED | WP10 complete negative; cross-system Layer 3 claim not established |

## 23. ADR-052 traceability — final review và exact-reuse optimization

| Requirement | Cài đặt/evidence | Gate |
|---|---|---|
| Full paper thay vì title-only | six local full PDFs, page count + exact SHA-256 in `docs/21` | every page extracted/read; applied/rejected mechanism recorded |
| Không nhập heuristic thiếu loss bound | no subtree/direction/random/sparse prune | search work/evaluated/feasible/omitted/retained counters unchanged |
| Cache identity không yếu đi | immutable run/vehicle refs + exact route/time/travel/allowance comparison | moved vehicle/run/route/travel tests miss; cached/uncached equivalence pass |
| Terminal reuse không bypass validator | `ForwardSlackCacheKey.Matches`; `_validator.Validate` remains before profile use | physical-capacity regression still prunes despite injected successful profile |
| Benchmark có raw provenance | `RideBound.CandidateHotPathBenchmark`; 3 baseline + 3 optimized JSON hashes | exact allocation and six semantic counters; timing labelled machine-local |
| Review không chỉ dựa test count | 1,226-file full-tree scan + high-risk manual read + mutation/differential/actual simulators | `reviews/wp1-wp10-final` findings and residual-risk ledger |
| WP10 analyzer exact terminal inventory | strengthened v2 analyzer binds freeze/full Runner/seed/job/arm/failure | 7/7 mutation classes; analysis SHA `be3e9077…cca3` |
| Runtime restore honesty | exact Docker image archive SHA `4783c541…9a872`, load returns image `5468b9cb…e573` | restore PASS; no future byte-rebuild claim |
| Final quality baseline | isolated `.NET 855/855`, FleetPy 95/95, RidePy 23/23 | concurrent attempt's CPU guard retained; isolated rerun PASS; Release 0 warning/error; format/diff/vulnerability/static gates PASS |
| Separate BeGo baseline not hidden | read-only current dirty tree, no mutation | backend 149 pass + 5 explicit opt-in skip; frontend 9/9 |
| Proper rendered report | `output/pdf/RideBound-WP1-WP10-final-review-2026-08-23.pdf` | 12/12 pages rendered/visually inspected; SHA `06616887…9b20b` |

## 24. ADR-053 traceability — Post-H6 mechanism diagnostics

| Requirement | Cài đặt/evidence | Gate |
|---|---|---|
| H6/WP10 không bị reinterpret | raw roots, freeze receipts, margins/panels/outcomes immutable | derived report read-only; no H6 artifact writes |
| Full-text research thật | Pillac 29, Ulmer 42, Ackermann 35 pages; exact local hashes | 106/106 pages extracted/read; first/middle/last render inspected |
| Cross-arm alignment đúng | equal-observed-input + wire/operational decision projections | policy-bearing state hash hoặc publication-ID reorder alone cannot trigger operational divergence |
| Không causal overclaim | inventory report claim boundary khóa `trajectoryAssociatedNotCausal` | future schema/aggregator phải giữ exact label; chưa claim implementation trước ticket |
| Evidence thiếu không được bịa | sufficiency matrix, explicit `notRecorded` | full retained portfolio/reranking requires evidence vNext |
| Paper không cấp default | no copied budget/horizon/slack/consensus/forecast | any v2 parameter requires WP14–WP16 development freeze |
| Progressive delivery | `tasks/41` refinement + `tasks/42` ordered queue | `RB-WP13-001..013 Done`, WP13 Complete; only `RB-WP14-001` refinement open |
| Canonical-number hot path | allocation-free three-marker span search in protocol, canonicalizer and independent oracle | exact marker semantics (`.`, `e`, `E`, `-0`) retained; targeted drain 1/1 and full Debug 856/856 pass without changing 120 s ceiling |
| Versioned first-divergence contract | `benchmarks/schemas/wp13/v1` + `wp13_first_divergence_records.py` + `wp13-003` evidence | 40/40 exact pairs; conditional no-null evidence; source/schema/tool/panel hashes; noncausal/read-only labels |
| Paired action-level comparison | `wp13_behavioral_comparator.py` + `wp13-004` evidence | 80 transcripts/44.156 decisions to EOF; 40/40 exact target bindings; immediate C1-lower 8, equal 32, C1-higher 0; descriptive/noncausal only |
| Recorded-witness clearance | strict schema + `wp13_recorded_witness_relaxation.py` + `wp13-005` evidence | 40 records/41 links; 28 exact numeric budget and 5 categorical lock clearances; 7 absent remain `notRecorded`; feasibility `notEvaluated` |
| First-divergence mechanism classes | strict schema + `wp13_mechanism_classify.py` + `wp13-006` evidence | 40 records/41 links; pair-level multi-label cross-tab; C1-lower 7 budget + 1 lock, equal 21 budget + 4 lock + 7 indeterminate; descriptive/noncausal only |
| H6 option-set sufficiency | strict schema + `wp13_option_set_sufficiency.py` + `wp13-007` evidence | 80 raw targets; generation exact-equal 40/40 and complete 80/80, zero cap/work/omission; full retained identities/routes/objectives remain `notRecorded`; vNext required but rerun not authorized |
| Runner retained-portfolio vNext | profile `retained-portfolio-v1`, defensive snapshot, exact selection problem, v1.2 mapper/strict decoder/schema + `wp13-008` report | profile-off v1.1 and H6 v1.0 compatible; generated/eligible/selected IDs, full route/schedule/objectives; capture differential; .NET 860/860, Python 168/168; no H6 write/rerun |
| Frozen exploratory retained-portfolio replay | E1 freeze receipt/configs/plans, matrix runner, independent inventory schema/tool + `wp13-009` report | exact 40 pair/80 arm, 8.640 requests, 44.156/44.156 v1.2 decisions, zero failure, 5.516.098.710 raw bytes; descriptive inventory only; H6 immutable |
| Independent E1 falsification | strict receipt schema + `wp13_e1_falsify.py` + `wp13-010` report | byte-exact 80-bundle/44.156-portfolio rebuild; exact 31/31 in-memory mutants rejected at expected layer and typed code; zero unexpected pass/failure |
| Instrumentation-only equivalence | `wp13_e1_h6_equivalence.py` + strict 80-record receipt | E1↔H6 same-arm behavioral projection equal 80/80, mismatch 0; semantic equality 0/80 expected; no mechanism/causal claim |
| Finite-panel candidate aggregation | policy-neutral semantic signature + strict `e1-candidate-descriptive-aggregation` schema/tool/report | 40/40 generated sets equal, 390 signatures/arm, zero collision/ID drift; 41 links = 33 pruned + 7 eligible-not-selected + 1 selected + 0 absent; objective profiles incomparable; rows overlapping/non-additive/noncausal |
| Full WP13 assurance audit | 80-file source-controlled inventory + strict `full-source-logic-claim-audit` schema/tool/report | deep H6 100 bundle/57.806 decisions and E1 80 arm/44.156 portfolios; zero failed DAG edge, zero unsafe conclusion, zero unresolved P0–P2; P2 verifier gap fixed by supplemental guard/regression without mutating frozen source |
| WP13 closure ownership | `RB-WP13-013` closure manifest/decision contract + `wp13-evidence-retention-and-successor-policy-v1` | seven exit gates pass; exclusive-create successor policy, 5.516 GB retain-and-freeze decision and versioned successor-verifier boundary all locked |
| WP14 factor design | ADR-066 + `tasks/43` + `wp14_mechanism_probe.py` + probe receipts | four factors rejected by measurement (one-sided charging, net anchoring, switch and relocation dimensions all produced zero witnesses); six factors F1–F6 accepted with explicit falsification conditions; H6/E1 measurements may explain the past outcome but must never select a future level |
| Treatment description boundary | probe objective measurement + `reviews/wp1-wp13-optimization-and-fairness` | `worst-hard-utilization-ppm` is constant in 100% of measured decisions, so C1 must not be described as ranking by commitment utilisation; the WP9 `lock/ranking` attribution is lock, not ranking |
| Burden claim boundary | probe promise-movement measurement | attributed decision-induced reduction is 99,83% Panel A and 99,23% Panel B, experienced published-to-published reduction is 88,85% and 91,43%, and 100% of riders in both arms still see their promise move at least once |
| Baseline strength boundary | O-001 + Alonso-Mora et al. full text | both arms forbid pre-pickup vehicle reassignment while the standard baseline permits it, so the measured service loss is a lower bound relative to a full-reassignment baseline; neither arm rebalances idle vehicles, so absolute completion rates are not comparable to published figures |
| WP14 authorization boundary | ADR-065 verdict `openExploratoryAblationOnly` | refinement only; new development namespace/cells, H6 Panel A/B excluded from tuning/selection, factor matrix/denominator/analyzer/resource envelope frozen before outcome, paired service–burden frontier not a single post-outcome scalar; H7 and lifecycle policy v2 remain unauthorized |
