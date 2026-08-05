# Trạng thái và decision log

> Tệp sống — cập nhật ở cuối mọi task RideBound
> Cập nhật gần nhất: 2026-08-03

## 1. Trạng thái tổng thể

| Mục | Trạng thái |
|---|---|
| Research direction | `LOCKED_FOR_IMPLEMENTATION_PLANNING` |
| Documentation | `MIGRATED_AND_VERIFIED_V1` |
| Implementation | `WP1_Q1_COMPLETE; WP2_COMPLETE; WP3_COMPLETE_14_OF_14; WP4_COMPLETE_14_OF_14` |
| Current work package | `WP5 BEGO — RB-WP5-001 REFINEMENT READY; NO IMPLEMENTATION READY` |
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
- Hoàn thành `RB-WP3-001`: đọc lại paper trực tiếp, khóa ADR-021 và tạo ordered
  queue 14 ticket trong `tasks/28-wp3-ledger-certificate-ticket-plan.md`.
- Hoàn thành `RB-WP3-002`: pure Domain promise/version/service-order model, stable
  10-dimension vector và explicit policy zero/unbounded/phase/material/lock types.
- Hoàn thành `RB-WP3-003`: shared `RouteScheduleProjector`, candidate evaluator
  dùng lại projector và `PromiseProjector` cho initial/onboard promise.
- Hoàn thành `RB-WP3-004`: three-way exogenous/decision/visible delta đủ
  ETA/material/vehicle/stop/order/insertion dimension cùng distance port/witness.
- Hoàn thành `RB-WP3-005`: immutable initial/revision ledger, exact version/P1
  conservation/no-refund và ledger nằm trong pending `OnlineState`/ACK transaction.
- Hoàn thành `RB-WP3-006`: hard vector budget evaluator cho đủ 10 dimension,
  exact before/delta/after witness, hard zero, unbounded và monotonic feasible set.
- Hoàn thành `RB-WP3-007`: accepted assignment, onboard, freeze-horizon và final
  confirmation lock evaluator với policy flag/witness rõ.
- Hoàn thành `RB-WP3-008`: typed incident open/resolve, affected-rider derivation,
  immutable breach record với chronology, vehicle và budget relation; không
  reset/refund normal ledger.
- Hoàn thành `RB-WP3-009`: independent combined validator tự dựng lại physical
  plan, state boundary, promise, three-way delta, locks và hard-vector budget;
  candidate filter chỉ là early pruning, Runner revalidate toàn fleet trước publish.
- Hoàn thành `RB-WP3-010`: certificate/action/schema strict cho normal operation
  và witness; input/proposed state hash cùng publication IDs bị cross-check với
  containing decision/actions.
- Hoàn thành `RB-WP3-011`: named commitment policy configuration/content hash,
  Runner `commitment` mode, atomic promise/ledger/certificate/state hash và
  matching ACK/retry semantics.
- Hoàn thành `RB-WP3-012`: canonical full-state checkpoint/restore với content
  hash, manifest/travel/reachable-state/tamper validation và cấm checkpoint khi
  decision còn pending.
- Hoàn thành `RB-WP3-013`: 10-dimension mutation-killing tests, 64×12 generated
  ledger histories, 16-seed independent exact-small P2/P3, two-process replay và
  checkpoint suffix equivalence.
- Hoàn thành `RB-WP3-014`: audit toàn code WP1–WP3, Browser research recheck,
  ADR-022, review giải thích chi tiết và chỉ `RB-WP4-001` refinement READY.

## 3. Chưa làm

- Chưa có C1/C2 objective, B2–B5 behavior hoặc OR-Tools solver. WP3 mới khóa
  feasibility/correctness và publication certificate, không chứng minh treatment
  hiệu quả hơn B1.
- B1 hiện dùng earliest-feasible schedule, một plan/vehicle và giữ incumbent
  service order. Bounded generator chỉ xét bốn pending request đầu và cap theo
  canonical candidate identity; candidate loss/solver loss chưa được đo.
- Inversion/relocation/vehicle-switch dimensions là validator vocabulary thật,
  nhưng B1 hiện không sinh inversion/relocation và O-001 khóa vehicle switch;
  chúng không được báo là active optimization.
- Incident recovery optimizer chưa có; WP3 chỉ đảm bảo breach được ghi đúng và
  không bị certificate normal-operation che lấp.
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

### RB-WP3-001–014 closure: commitment correctness boundary

```text
Logical source-controlled test inventory: 414
  Contracts: 133
  Domain: 134
  Application: 34
  Algorithms: 48
  Runner: 58
  Architecture: 7

Required command revalidation on 2026-08-03:
  dotnet test RideBound.slnx
  Result: 414/414 passed; exit code 0; 0 failed; 0 skipped.
  Contracts: 133/133 passed.
  Domain: 134/134 passed.
  Application: 34/34 passed.
  Algorithms: 48/48 passed.
  Runner: 58/58 passed.
  Architecture: 7/7 passed.
  Windows Application Control 0x800711C7 is no longer a current blocker.

Historical host-policy evidence on 2026-08-02:
  required attempts were blocked while loading fresh unsigned Contracts,
  Application and Runner DLLs. Code Integrity events 3033/3077 identified Smart
  App Control policy {0283ac0f-fff1-49ae-ada1-8a933130cad6}. This remains an
  environment record, not a current test failure.

Supplemental same-tree assertion evidence:
  Contracts Release: 133/133 passed.
  Domain Debug in required attempt: 134/134 passed.
  Application Debug in required attempt: 34/34 passed.
  Algorithms Debug in required attempt: 48/48 passed.
  Architecture Debug in required attempt: 7/7 passed.
  Runner exact xUnit methods through self-contained policy-safe harness:
    54/54 non-child-process cases passed.
  Runner child-process cases: 4/4 behavior passed independently:
    Q1 transcript two byte-exact clean processes;
    Q1 stdout/stderr diagnostic isolation;
    WP2 demo two byte-exact clean processes;
    WP3 commitment demo + checkpoint restore clean processes.

Quality/replay gates:
  Release build --warnaserror: 0 warnings, 0 errors.
  dotnet format --verify-no-changes --no-restore: passed after import-order fix.
  source JSON parse: 104/104 passed.
  Markdown: 58 files, 112 relative links, balanced fences.
  portable dependency scan: passed; NuGet direct/transitive audit: clean.
  git diff --check: passed (only configured LF→CRLF worktree warnings).
  WP2 final decision hash:
    c95c3f7e651a5ff5f366051538ecc53663696baa13fbec967d769af5f3c5d90f
  WP3 final decision hash:
    54ebbbdda6753654aab43d522d9d24bffefe56426275035d685ecc8588371589
  WP3 final state hash:
    d91c91c661dd3a2d2de6d5e214bef2a55a9384d635520ca7d5bdbe9d15694527
  Checkpoint restore suffix: byte-equal to uninterrupted genesis replay.
  WP2/WP3 scripts write explicit UTF-8 without BOM and reject non-empty stderr,
    removing PowerShell 5/7 native-pipeline encoding ambiguity.

WP3 correctness evidence:
  all 10 vector dimensions have exact-boundary and killing mutations;
  hard zero/unbounded/overflow/unknown vocabulary semantics are explicit;
  64 seeds × 12 generated ledger revisions preserve P1/no-refund;
  16 independent exact-small seeds match P2 normal hard-gate behavior;
  16 P3 seeds prove relaxing ETA hard limit 40→160 cannot shrink feasible set;
  physical/state-boundary/lock/budget order is independently recomputed;
  incident breach, certificate publication IDs and checkpoint relations are
    structurally cross-validated rather than trusted from solver output.

Browser research recheck using the in-app Browser:
  Gaul et al. 2021 rolling-horizon MILP;
  Schulz & Pfeiffer 2026 forward slack/precomputation;
  Geržinič et al. 2023 stated-preference survey;
  Tiwari et al. 2024 weighted/Pareto/lexicographic objectives;
  Ackermann & Rieck 2025 multiple-plan dynamic DARP.
  Outcome: no numeric paper default adopted; hard gate stays outside objective;
  schedule strategy, bounded precompute and multiple-plan belong to WP4.

Claim limit:
  414/414 is now a full-solution Debug xUnit pass on this host.
  WP3 still proves mechanical correctness in published small bounds, not scale,
  effectiveness, solver optimality or user satisfaction.
Final recheck date: 2026-08-03
```

### RB-WP4-002 closure: solver-neutral selection boundary

```text
Logical source-controlled test inventory: 435
  Contracts: 133
  Domain: 134
  Application: 54
  Algorithms: 48
  Runner: 58
  Architecture: 8

Required command on 2026-08-03:
  dotnet test RideBound.slnx
  Result: 435/435 passed; exit code 0; 0 failed; 0 skipped.

Production boundary:
  CandidateSelectionProblem canonicalizes vehicles/requests/options while
  retaining declared lexicographic objective order.
  Exactly one no-op is required per vehicle; a validated solution selects
  exactly one option per vehicle and accepts each request at most once.
  Sum/Maximum aggregation fails closed on canonical-integer overflow.
  Deterministic work/time/seed budget is separate from observed wall time.
  Bound direction, exact rational gap, bound order and incumbent/solution match
  are validated before OPTIMAL/FEASIBLE may be reported.
  OPTIMAL, FEASIBLE, INFEASIBLE, UNKNOWN, MODEL_INVALID and SAFE_FALLBACK remain
  distinct; no Google.OrTools or other solver package entered Application.

Adversarial evidence:
  20 new Application test cases include missing/duplicate no-op, unknown entity,
  duplicate request, invalid vector/range, aggregation overflow, lexicographic
  dominance, reversed bound, exceeded deterministic budget, reordered bound and
  false-optimal rejection. Architecture adds the Application port-location gate.
```

### RB-WP4-003 closure: executable scheduling and conservative slack

```text
Logical source-controlled test inventory: 444
  Contracts: 133
  Domain: 134
  Application: 54
  Algorithms: 57
  Runner: 58
  Architecture: 8

Required command on 2026-08-03:
  dotnet test RideBound.slnx
  Result: 444/444 passed; exit code 0; 0 failed; 0 skipped.
  dotnet format RideBound.slnx --verify-no-changes --no-restore: passed.

Mechanism:
  Backward slack combines pickup-arrival deadline, drop-off ride-time deadline,
  waiting absorption and future-stop slack under a fixed projected schedule.
  The value is named CertifiedDelay: delay <= certificate is sufficient for
  time feasibility; delay > certificate is never treated as infeasibility.
  Cache key binds exact immutable run snapshot, full vehicle snapshot and
  position, structural route fingerprint, evaluation time, travel version/hash.
  Cache is bounded and failures are not cached.
  Cache may rank a frontier node but cannot admit it; PhysicalPlanValidator
  always runs before a profile can enter a retained candidate.
  origin-hold-relocated-wait moves only first-pickup waiting already present to
  a current-node waypoint with real service duration; edge progress and
  unexecuted frozen prefix are refused. The transformed route is fully
  revalidated and must preserve original stop service/departure times and cost.

Mutation/equivalence evidence:
  9 new Algorithms tests cover backward arithmetic, every delay through the
  certificate boundary, executable hold equivalence, edge refusal, independent
  run/vehicle/position/route/time/travel invalidation, travel-duration mutation,
  cached/uncached equality, cache-cannot-bypass-validator and repeated-build hits.
```

### RB-WP4-004 closure: bounded best-first generation and loss accounting

```text
Logical source-controlled test inventory: 449
  Contracts: 133
  Domain: 134
  Application: 54
  Algorithms: 62
  Runner: 58
  Architecture: 8

Required command on 2026-08-03:
  dotnet test RideBound.slnx
  Result: 449/449 passed; exit code 0; 0 failed; 0 skipped.
  dotnet format RideBound.slnx --verify-no-changes --no-restore: passed.

Bounded semantics:
  request priority = (latestPickup, arrivalTime, requestId);
  global search priority = potential accepted count, mandatory-service lower
  bound, conservative forward slack, stable digest;
  one deterministic work unit = one best frontier node dequeued;
  unexpanded frontier subtrees are counted combinatorially, with explicit
  canonical saturation instead of overflow or a fabricated exact number;
  cap retains the required safety no-op, then orders feasible candidates by
  accepted count, exact operational cost, slack and candidate ID.

Loss boundary:
  REQUEST_BOUND_OMISSION identifies known omitted requests;
  WORK_BOUND_OMISSION counts raw paths whose feasibility remains unknown;
  CANDIDATE_CAP_OMISSION counts already validated feasible candidates;
  every category has stable digest and count, separate from later solver loss;
  exact mode fails if request/work/candidate omission would occur.

Evidence:
  5 new Algorithms tests cover urgent-request priority, exact work fail-closed,
  exhaustive path conservation, best-first high-acceptance retention, feasible
  cap conservation/digest stability and work-monotonic acceptance.
```

### RB-WP4-005 closure: B2 revision penalty and B3 fixed freeze

```text
Logical source-controlled test inventory: 458
  Contracts: 133
  Domain: 135
  Application: 54
  Algorithms: 70
  Runner: 58
  Architecture: 8

Required command on 2026-08-03:
  dotnet test RideBound.slnx
  Result: 458/458 passed; exit code 0; 0 failed; 0 skipped.
  dotnet format RideBound.slnx --verify-no-changes --no-restore: passed.

B2 rolling-penalty:
  mechanism provider replaces all ten cumulative hard limits with unbounded and
  removes optional freeze/final-confirmation locks, while preserving material
  rule, budget basis and the global O-001 accepted-assignment lock;
  every raw candidate is assessed through the full validator; assessment does
  not mutate/prune the shared raw pool;
  selector order is accepted count, material ETA revision count, the stable ten
  revision dimensions, canonical operational cost and candidate-ID vector.

B3 fixed-freeze-horizon:
  constructor requires a positive explicit horizon and non-empty valid lock mask;
  all cumulative limits remain unbounded and no numeric default exists;
  freeze activates inclusively at timeToPickup <= horizon and source hard budgets
  cannot accidentally prune outside the configured horizon.

Additional correctness fix:
  CommitmentVector.Add and both exact fleet selectors now fail before canonical
  overflow instead of allowing a non-canonical total or risking runtime overflow.

Evidence:
  8 new Algorithms cases include lexicographic precedence, dimension order,
  explicit configuration, canonical cost, B2 raw-pool preservation over 16
  seeds, and B3 exact horizon boundary; 1 Domain vector-overflow regression.
```

### RB-WP4-006 closure: B4 same-vehicle waiting-incumbent repair

```text
Logical source-controlled test inventory: 465
  Contracts: 133
  Domain: 135
  Application: 54
  Algorithms: 77
  Runner: 58
  Architecture: 8

Required command on 2026-08-03:
  dotnet test RideBound.slnx
  Result: 465/465 passed; exit code 0; 0 failed; 0 skipped.
  dotnet format RideBound.slnx --verify-no-changes --no-restore: passed.

B4 repair boundary:
  disabled by default so the B1 candidate set and choice remain unchanged;
  an enabled positive cap admits only Accepted/WaitingPickup incumbents assigned
  to the same vehicle, not onboard, with one pickup/drop pair wholly inside the
  mutable suffix and no unexecuted frozen request stop;
  a seed removes exactly that pair and enumerates every precedence-preserving
  reinsertion; it never combines two repaired pairs or mutates source routes;
  exact mode fails if the repair-request cap omits an eligible incumbent;
  bounded mode reports repair omission count/digest separately and marks the
  diagnostics incomplete;
  every repaired route is physically revalidated and O-001 still prevents
  cross-vehicle reassignment.

Correctness defect found by adversarial testing:
  the original frontier stable ID reused an order-insensitive omission digest,
  so route permutations could collapse into one search identity;
  search nodes now use an order-sensitive token digest while omission-set
  digests remain canonical and order-insensitive.

Evidence:
  7 new Algorithms tests cover atomic pair reinsertion and input immutability,
  frozen/onboard exclusion, exact cap failure, bounded loss stability, disabled
  B1 equivalence, repaired route diversity and cheaper B4 selection without
  reassignment.
```

### RB-WP4-007 closure: B5 canonical multiple-plan pool

```text
Logical source-controlled test inventory: 477
  Contracts: 133
  Domain: 135
  Application: 57
  Algorithms: 82
  Runner: 62
  Architecture: 8

Required command on 2026-08-03:
  dotnet test RideBound.slnx
  Result: 477/477 passed; exit code 0; 0 failed; 0 skipped.
  dotnet format RideBound.slnx --verify-no-changes --no-restore: passed.

Canonical state/checkpoint boundary:
  pool version zero is the only empty value; every non-empty replacement advances
  the previous version exactly once;
  exact plan SHA-256 binds source epoch, ordered vehicle IDs, route version,
  progress, frozen/mutable order and every executable stop field;
  checkpoint restore recomputes the ID and checks the exact vehicle set,
  request-stop assignment membership, frozen/executed compatibility, physical
  feasibility and distinguished-plan equality with the online run.

B5 selection:
  one shared generated candidate set feeds deterministic fleet enumeration;
  pool size and combination work cap are explicit configuration, with exact
  fail-closed and bounded truncation diagnostics;
  alternatives must preserve the distinguished new-request assignment;
  semantic duplicates are removed, Pareto dominance uses accepted count,
  operational cost and conservative forward slack, then top-K uses greedy
  max-min route distance;
  distinguished control maximizes shared executable-prefix consensus before
  operational/stable tie-breaks;
  only distinguished request actions/routes are applied or exposed.

Executable alternative correction:
  adversarial review found that all candidates originate before publication, so
  a non-distinguished route can have the old or same version after the chosen
  route is applied;
  every different retained alternative is therefore rebuilt at exactly
  distinguished route version + 1 and physically validated against the proposed
  run before it can survive checkpoint restore.

Evidence:
  3 Application identity/version/rehydration cases;
  5 Algorithms dominance, stable diversity/consensus, exact/bounded work,
  assignment compatibility and distinguished-only publication cases;
  4 Runner canonical round-trip, forged-ID, forged-distinguished and actual
  policy-output checkpoint cases.
```

### RB-WP4-008 closure: C1 hard-vector lexicographic policy

```text
Logical source-controlled test inventory: 483
  Contracts: 133
  Domain: 135
  Application: 57
  Algorithms: 88
  Runner: 62
  Architecture: 8

Required command on 2026-08-03:
  dotnet test RideBound.slnx
  Result: 483/483 passed; exit code 0; 0 failed; 0 skipped.
  dotnet format RideBound.slnx --verify-no-changes --no-restore: passed.

C1 hard boundary:
  the common raw physical pool is generated once;
  each candidate is applied and full WP3 commitment validation both removes
  hard-invalid candidates and returns the authoritative validated ledger in one
  pass; C1 neither adds candidates nor repeats the validator as a separate filter;
  feasibility remains exact per request/dimension/phase and never uses PPM.

Lexicographic ranking:
  maximize accepted requests;
  minimize the worst cumulative BudgetAfter/hard-limit ceiling PPM across active
  riders and applicable finite dimensions;
  minimize the stable ten decision-induced revision dimensions in vocabulary
  order, then canonical operational cost and candidate-ID vector;
  UInt128 multiplication prevents overflow at the canonical integer maximum;
  a feasible zero-limit/zero-usage dimension ranks as 1,000,000 ppm because it
  has no reserve, while non-zero usage remains hard-invalid;
  when no applicable finite hard limit exists, utilization and revision ranking
  are disabled so C1 is semantically identical to B1.

Evidence:
  6 new Algorithms tests cover accepted/utilization/revision/cost dominance,
  exact dimension order, one-pass retained-set equality with the reference hard
  filter, 1/3 and canonical-maximum ceiling arithmetic, zero-limit semantics and
  unbounded no-lock exact-small B1 equivalence.
```

### RB-WP4-009 closure: C2 warning/soft-hard hybrid

```text
Logical source-controlled test inventory: 489
  Contracts: 133
  Domain: 135
  Application: 57
  Algorithms: 94
  Runner: 62
  Architecture: 8

Required command on 2026-08-03:
  dotnet test RideBound.slnx
  Result: 489/489 passed; exit code 0; 0 failed; 0 skipped.
  dotnet format RideBound.slnx --verify-no-changes --no-restore: passed.

C2 configuration and hard boundary:
  every warning profile explicitly defines all ten dimensions once; null means
  disabled and no numeric warning default exists;
  an enabled warning requires a finite hard limit and warning <= hard;
  C2 calls the same one-pass C1 validator/assessor, so warning never admits a
  hard-invalid candidate or creates a candidate absent from the shared raw pool.

Objective:
  maximize accepted count, minimize worst hard PPM, then minimize the ordered
  ten-dimension warning-excess vector, ordered decision-induced revision vector,
  canonical operational cost and candidate-ID vector;
  warning excess is accumulated per scoped vehicle/rider with checked canonical
  arithmetic, preserving ms/count/mm dimensions instead of a weighted scalar;
  if every warning is disabled, C2 delegates directly to the C1 selector and
  produces no synthetic warning objective/output.

Evidence:
  6 new Algorithms tests cover warning-before-revision/cost dominance, explicit
  ten-dimension profile shape, exact C1/C2 retained hard set, non-zero boundary
  excess, warning-above-hard rejection and disabled-warning C1 equivalence.
```

### RB-WP4-010 closure: deterministic OR-Tools adapter

```text
Logical source-controlled test inventory: 495
  Contracts: 133
  Domain: 135
  Application: 57
  Algorithms: 94
  Solvers.OrTools: 5
  Runner: 62
  Architecture: 9

Required command on 2026-08-03:
  dotnet test RideBound.slnx
  Result: 495/495 passed; exit code 0; 0 failed; 0 skipped.
  dotnet format RideBound.slnx --verify-no-changes --no-restore: passed.

Adapter boundary and model:
  Google.OrTools 9.15.6755 is pinned only in RideBound.Solvers.OrTools;
  BoolVar selection enforces exactly one option per vehicle and at most one
  assignment per request;
  integer Sum objectives use weighted sums, Maximum objectives use an auxiliary
  integer variable with AddMaxEquality;
  canonical upper-bound arithmetic fails ModelInvalid before native model build
  when an aggregate could exceed Int64.

Lexicographic solve and diagnostics:
  every objective pass rebuilds the model with equality constraints for prior
  objective values proven OPTIMAL; a merely FEASIBLE pass is never fixed as if
  optimal;
  one worker, explicit seed, remaining conflict budget and deterministic-time
  budget make the outcome independent of observed wall time;
  OPTIMAL, FEASIBLE, UNKNOWN, INFEASIBLE and MODEL_INVALID remain distinct;
  selected IDs are revalidated by CandidateSelectionSolution.Create and exact
  bounds are rounded conservatively according to minimization/maximization.

Evidence:
  5 solver tests cover four-pass Sum/Maximum trade-offs plus request uniqueness,
  acceptance-before-cost, eight identical deterministic repetitions, aggregate
  overflow and diagnostic budget/version detail;
  1 architecture test prevents the native package from leaking outside the
  solver adapter project.
```

### RB-WP4-011 closure: deterministic deadline and safe fallback

```text
Logical source-controlled test inventory: 507
  Contracts: 133
  Domain: 135
  Application: 69
  Algorithms: 94
  Solvers.OrTools: 5
  Runner: 62
  Architecture: 9

Required command on 2026-08-03:
  dotnet test RideBound.slnx
  Result: 507/507 passed; exit code 0; 0 failed; 0 skipped.
  dotnet format RideBound.slnx --no-restore: passed.

Budget and loss boundary:
  deterministic execution budget has independent generation-work,
  semantic-validation-work and solver conflict/deterministic-time limits;
  observed wall time remains a metric and cannot change replay selection;
  pre-solve accounting preserves omitted candidate count, canonical lowercase
  SHA-256 digest and saturation, separately from primary solver status/loss.

Independent validation and fallback:
  every primary solution, including OPTIMAL or FEASIBLE, must pass the injected
  semantic/full-state validator before it can leave the executor;
  fallback order is canonical no-op, then every one-request insertion sorted by
  the exact objective vector and selected option IDs;
  each attempted solution consumes one validation work unit and records a typed
  rejection witness with path and selected IDs;
  exhaustion or an entirely rejected portfolio returns UNKNOWN with no solution;
  no incident result can be fabricated at this solver-neutral boundary;
  primary bounds stay in audit diagnostics, while a fallback result has no
  mismatched incumbent bounds.

Evidence:
  12 new Application cases cover validated optimal, truthful feasible, all three
  no-solution statuses, rejected incumbent, ordered single-request rescue,
  validation exhaustion, rejected portfolio, separate candidate/solver loss,
  accounting contract and cross-budget misuse.
```

### RB-WP4-012 closure: named policy/solver Runner integration

```text
Logical source-controlled test inventory: 523
  Contracts: 133
  Domain: 135
  Application: 69
  Algorithms: 101
  Solvers.OrTools: 5
  Runner: 71
  Architecture: 9

Required command on 2026-08-03:
  dotnet test RideBound.slnx
  Result: 523/523 passed; exit code 0; 0 failed; 0 skipped.
  dotnet format RideBound.slnx --no-restore: passed.

Configuration and identity:
  one canonical registry round-trips the seven published B1–B5/C1/C2 names;
  strict WP4 JSON declares generation cap/work/schedule, solver stage budgets and
  only the mechanism-specific B3 freeze, B4 repair, B5 pool or C2 warning fields;
  C2 has one explicit ten-dimension profile per commitment policy and every
  enabled warning is bounded by a finite hard limit;
  WP4 config hash is domain-bound to the commitment config hash, and initialize
  fails before state creation unless manifest policy ID/version/combined hash
  match the loaded implementation exactly.

Solver and publication path:
  B1–B4/C1/C2 generate one shared physical pool and map their exact hierarchy to
  candidate-selection objectives; vehicle-ordered ID ranks preserve the existing
  deterministic final tie-break without weighted scalarization;
  OR-Tools output and fallback pass full semantic validation with the baseline's
  effective commitment provider; Runner independently validates again;
  B5 keeps deterministic plan-pool enumeration and publishes only distinguished;
  ledger, certificate, plan pool and state hash stay in the pending transaction,
  and only matching ACK commits them; solver completed/safeFallback is part of
  the hashed decision shell, so retry is byte-identical.

Evidence:
  7 Algorithms cases cover registry round-trip, B1 request uniqueness/cost, B2
  material+dimension hierarchy, C1 maximum utilization, C2 warning hierarchy,
  unbounded C1=B1 and semantic-validator fallback;
  9 Runner cases cover strict variant config, warning/hard boundary, binding,
  actual OR-Tools decision, retry/wrong ACK/commit, injected UNKNOWN fallback,
  manifest mismatch, B5 ACK/checkpoint restore and real child-process CLI.
```

### RB-WP4-013 closure: independent evidence

```text
Logical source-controlled test inventory: 557
  Contracts: 133
  Domain: 135
  Application: 69
  Algorithms: 134
  Solvers.OrTools: 6
  Runner: 71
  Architecture: 9

Required command on 2026-08-03:
  dotnet test RideBound.slnx
  Result: 557/557 passed; exit code 0; 0 failed; 0 skipped.

Independent correctness:
  B1 production generator/selector matches an independent exact enumerator over
  64 deterministic fixtures within the published 2-vehicle/2-request bound;
  C1 production objective mapper plus actual OR-Tools matches a separately coded
  enumerator over 64 fixtures, selecting identical candidate IDs with OPTIMAL
  status and exact zero gap on every objective level;
  the hard-gate mutation fixture proves the raw set is strictly larger than the
  hard-feasible set, so deleting the gate is observably killed;
  an actual bounded request omission reaches execution count/digest/saturation
  accounting separately from an injected solver UNKNOWN and validated no-op.

Cross-ticket evidence retained:
  cache on/off and route/travel invalidation equivalence; infinite C1=B1 and
  disabled C2=C1; plan-pool checkpoint/tamper; deterministic deadline and
  fallback; ACK/retry publication gates.

Synthetic performance signal:
  4/16/32/128 Boolean-option models all reached exact OPTIMAL. Observed p50 wall
  times were 2.389/12.160/21.406/91.004 ms on .NET 10.0.9, Windows 10.0.26200,
  X64, 12 processors. This is machine-local candidate-selection evidence only;
  it is not a demand-scale, service-quality or effectiveness claim.
```

### RB-WP4-014 closure: full audit and handoff

```text
Quality gates on 2026-08-03:
  dotnet test RideBound.slnx: 557/557 passed
  Release build --no-restore /warnaserror: 0 warnings, 0 errors
  WP4 microbenchmark Release build: 0 warnings, 0 errors
  dotnet format --verify-no-changes: passed
  NuGet direct/transitive vulnerability audit: no vulnerable packages reported
  JSON/Markdown internal-link/fence/diff/process gates: passed

Logic audit:
  reviewed contract/state/physical/commitment/candidate/policy/solver/Runner paths,
  not only test summaries; no production TODO/placeholder or solver dependency
  leak was found. Candidate loss, solver loss and publication failure stay
  distinct. Every solver/fallback selection is independently validated, Runner
  validates again, and only matching ACK commits route/ledger/pool/hash state.

Artifacts:
  ADR-024; tasks/30 complete; docs/reviews/wp1-wp4-final explains WP1-WP4 flow,
  each important production file, paper-to-code optimization, test evidence,
  synthetic curve and unproven claims. Historical wp1-wp3 review is preserved.

Handoff:
  WP4 is Complete and Q2 mechanical correctness is closed. The only READY ticket
  is refinement-only RB-WP5-001; no BeGo implementation ticket exists yet.
```

### Historical CI hardening checkpoint — 2026-07-28

```text
Release build: passed, 0 warnings, 0 errors
Whitespace format verification: passed
NuGet vulnerability audit: no vulnerable direct/transitive packages
Runner publish smoke: passed
Architecture reference graph with normalized separators: passed
Local xUnit execution at this historical checkpoint: blocked by Windows Application Control (0x800711C7)
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

WP1–WP4 đã Complete. `RB-WP4-001..014` Done và ADR-024 đóng core mechanical
correctness. WP3 validator/certificate/checkpoint tiếp tục là publication boundary
cho mọi policy/solver WP4.

Ticket duy nhất `READY`:

> `RB-WP5-001` — refinement BeGo adapter và persistence; no implementation.

Chi tiết:
[31-wp5-bego-integration-refinement.md](tasks/31-wp5-bego-integration-refinement.md).
Refinement phải khóa source provenance, Runner ownership, transaction/recovery,
persistence, rollback và paired Layer-1 evidence. Không tạo migration/endpoint/
adapter implementation, không tự chọn O-002/O-003/O-004 và không mở O-001.

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

### ADR-021 — 2026-07-30 — Accepted

**Context:** WP3 phải thêm promise history, three-way delta, vector budget, phase
locks, incident, certificate và checkpoint lên state/hash/ACK thật của WP2.
Tài liệu `07` trước đó liệt kê `decisionHash` bên trong ledger/certificate, nhưng
nhúng current decision hash vào chính state/body đang được hash sẽ tạo vòng tự
tham chiếu. Candidate evaluator cũng đang sở hữu schedule riêng, dễ làm promise
projection lệch physical schedule. Mức số O-002/O-003 chưa được pilot khóa.

**Decision:** Domain sở hữu stable 10-dimension vocabulary, canonical vector/
explicit policy, promise/version/service tokens, immutable append-only ledger,
budget và phase-lock invariant. Application sở hữu một `RouteScheduleProjector`
dùng chung cho Algorithms và promise flow, `PromiseProjector` cùng
`PromiseDeltaCalculator`; đổi stop node bắt buộc có `IStopDistanceLookup` mm.
Three-way delta tính độc lập `old→exo`, `exo→new`, `old→new`; không giả
`visible=exo+decision`. Initial promise version 1 không tiêu budget; revision tăng
đúng một, round-trip không refund. Ledger nằm trong pending `OnlineState` và chỉ
commit cùng route sau matching ACK.

Ledger dùng stable `publicationId`, không nhúng current `decisionHash` vào state.
Ticket certificate/Runner sau sẽ bind input/state/publication bằng containing
decision envelope hash. `null` hard limit là unbounded, zero là hard zero; không có
default numerical profile. Accepted assignment luôn lock theo O-001; onboard khóa
pickup, freeze/final confirmation chỉ qua explicit policy flags. Incident là breach
record riêng ở ticket `008`.

**Paper/claim evidence:** Full text Multiple-plan dynamic DARP xác nhận dynamic
insertion, plan pool, consensus và least-commitment đã có; Time-consistent DARP
xác nhận consistency/time classes/cost trade-off đã có; forward-looking dispatch
xác nhận rolling/future-aware matching/detour safeguards đã có. RideBound chỉ giữ
claim hẹp per-rider, path-dependent, multi-dimensional cumulative/switch ledger
với machine-checkable certificate.

**Alternatives considered:** schedule commitment riêng trong Algorithms; nén
budget thành weighted scalar; dùng travel time thay stop distance; lưu current
decision hash trong state; chọn default “medium” profile; nối ngay vào Runner khi
incident/validator/schema chưa tồn tại.

**Consequences:** `RB-WP3-001..007` tạo foundation và executable tests nhưng
default B1 Runner vẫn certificate `notProduced`; chưa có C1/P2 guarantee. Queue có
14 ticket, đúng nửa đầu DONE và `RB-WP3-008` là next duy nhất.

**Evidence:** `Domain/Commitments`, `Application/Scheduling`,
`Application/Promises`, refactored `CandidateScheduleEvaluator`, Domain/
Application tests, `tasks/28-wp3-ledger-certificate-ticket-plan.md`; required
Current-tree suite evidence 378/378; host policy exception 0x800711C7 được
ghi riêng, không tính là assertion failure.

**Supersedes / superseded by:** Bổ sung ADR-020 và chỉnh ledger/certificate
self-binding trong `07`; không đổi protocol hash framing ADR-014/016/017, không
mở reassignment O-001 và không chọn O-002/O-003.

### ADR-022 — 2026-08-02 — Accepted

**Context:** `RB-WP3-008..014` phải biến vocabulary/foundation của ADR-021 thành
publication gate thực sự. Audit toàn tuyến phát hiện các đường đi mà test cục bộ
trước đó chưa khóa: candidate có thể mutate state ngoài route, genesis vehicle
có thể preload pending stop, checkpoint có thể chụp pending decision hoặc dựng
state không reachable, certificate có thể không khớp publication actions, breach
có thể không khớp normal ledger và initial state hash chưa bao phủ full state.
Đồng thời B1 chỉ tối ưu exhaustive trong candidate set đã sinh; earliest-feasible,
single-plan, four-pending bound và incumbent-order preservation không phải
state-of-the-art optimization đầy đủ.

**Decision:** Hoàn thành đủ 14 ticket WP3 như một hard correctness boundary.
Incident/breach là immutable ledger riêng. `CommitmentDecisionValidator` tự dựng
lại physical feasibility, immutable state boundary, promise/delta, locks và
budget; candidate filter chỉ early-prune, Runner luôn full-fleet revalidate.
Produced certificate phải bind exact input/proposed state hash và tập publication
ID trong actions. Commitment policy là canonical named config có content hash.
Checkpoint chỉ được phát khi không có pending decision và restore phải qua hash,
manifest, travel identity, genesis/post-genesis reachability cùng ledger/breach
cross-relations. Publication vẫn commit duy nhất tại matching ACK.

**Optimization/claim decision:** Hard vector không được nén vào weighted scalar
hoặc đưa vào objective như soft preference. WP3 chứng minh feasibility/auditability,
không chứng minh C1 tốt hơn B1. Inversion/relocation/vehicle-switch dimensions
được validator hỗ trợ nhưng B1 hiện không chủ động sinh chúng. Schedule strategy,
candidate loss, forward slack/precompute, multiple-plan pool, lexicographic/Pareto
selection và OR-Tools thuộc `RB-WP4-001` refinement. Không sao chép số horizon,
runtime hoặc stated preference từ paper thành default.

**Paper/claim evidence:** In-app Browser đối chiếu Gaul et al. (2021), Schulz &
Pfeiffer (2026), Geržinič et al. (2023), Tiwari et al. (2024), Ackermann & Rieck
(2025). Evidence hỗ trợ rolling horizon, slack/precompute, history sensitivity,
lexicographic/Pareto và multiple-plan baselines nhưng không cấp một universal
numeric policy hoặc novelty claim mới. Mapping chi tiết nằm trong `03` và `21`.

**Alternatives considered:** trust solver-provided delta/certificate; chỉ thêm
if/else vào `RollingCostPolicy`; serialize partial checkpoint; dùng incident để
refund budget; triển khai OR-Tools ngay; gọi current B1 globally optimal; lấy
10–15 phút/99.5%/survey coefficients làm default.

**Consequences:** WP3 Complete; logical inventory 414. Required full-solution
command tại thời điểm chấp nhận ADR bị Windows Application Control `0x800711C7`
chặn fresh DLL, nên
evidence được tách minh bạch thành unaffected suites, 54/54 policy-safe Runner
methods và bốn clean-process cases; không gọi đó là full-solution pass. Chỉ
`RB-WP4-001` refinement READY, không production WP4 implementation nào READY.
Revalidation sau đó ngày 2026-08-03 đã pass full solution 414/414; ADR không đổi
claim boundary vì đây chỉ là thay đổi trạng thái host-policy evidence.

**Evidence:** `Domain/Incidents`, `Application/Commitments`, commitment filter,
strict Contracts/schema, `Runner/Configuration`, `OnlineStateCheckpointCodec`,
WP3 tiny scenario/script, exact-small/property/mutation tests,
`reviews/wp1-wp3/README.md`, `tasks/28` và `tasks/29`; Release build/format và
published replay hashes trong mục 4.

**Supersedes / superseded by:** Hoàn tất executable semantics của ADR-021 và
đóng WP3; không supersede ADR-014/016/017 hash framing, ADR-018 O-001 hoặc
ADR-020 physical B1 semantics. ADR-023 của WP4 chỉ được bổ sung sau refinement.

### ADR-023 — 2026-08-03 — Accepted

**Context:** Audit WP1–WP3 cho thấy hard-vector gate là correctness mechanism
thật nhưng B1 vẫn dùng earliest-feasible, single plan, four-request/ID cap và
Cartesian selector. Nếu C1 được cấp raw candidate khác, prune sau cap không được
ghi, hoặc OR-Tools incumbent tự publish, đánh giá sẽ trộn commitment effect với
compute/candidate bias. Multiple-plan/waiting/repair còn yêu cầu state và
checkpoint semantics, không thể thêm bằng vài nhánh `if/else` trong B1.

**Research evidence:** In-app Browser đọc lại Gaul et al. 2021, Schulz &
Pfeiffer 2026, Tiwari et al. 2024 và Ackermann & Rieck 2025; đọc bổ sung
Mitrović-Minić & Laporte 2004 về drive/wait/dynamic waiting, Masson–Lehuédé–
Péton 2013 và Gschwind 2019 về forward-time-slack/incremental feasibility,
Ackermann & Rieck 2022 về future insertion guidance, cùng official OR-Tools/
NuGet. Evidence hỗ trợ mechanism/baseline, không cho universal horizon, pool
size, weight, budget hoặc effectiveness target.

**Decision:** Khóa đủ 12 quyết định trong `tasks/30`:

1. B1–B5/C1/C2 dùng cùng raw physical candidate set và cap trước policy gate;
   report request/candidate omission, hard-prune và solver loss riêng.
2. Main schedule là `earliest-feasible`; named wait control chuyển waiting slack
   có thật thành current-node hold waypoint, không chỉ sửa ETA nội bộ.
3. Exact mode fail nếu omit. Bounded priority là latest pickup/arrival/ID; cap
   theo accepted count, admissible operational key, slack reserve và stable ID.
4. Slack/precompute cache bind full route/position/time/travel identity; chỉ
   early-prune, cache miss khi key đổi và cached/uncached phải tương đương.
5. Repair chỉ remove/reinsert waiting incumbent trong cùng vehicle, giữ O-001;
   B4 được ghi rõ `no-reassignment-repair`.
6. B5 plan pool/version/distinguished plan nằm trong canonical state/checkpoint;
   alternative incompatible với executed/frozen decisions bị loại.
7. Multi-pass lexicographic là accepted → policy utilization/warning → 10
   revision dimensions → operational cost → candidate-ID vector; không scalar
   hard vector. Normalized utilization chỉ là checked ranking ppm.
8. C1/C2 cùng hard gate; C2 warning chỉ xếp hạng trong hard-feasible set.
9. Solver-neutral port/model ở Application, policy ở Algorithms, package
   `Google.OrTools 9.15.6755` chỉ ở Solvers.OrTools.
10. Replay dùng deterministic work/CP deterministic-time budget, one worker và
    explicit seed. Wall time chỉ metric; status/bound/gap/fallback truthful.
11. Exact-small bound 2 vehicle/2 pending/1 repair incumbent/pool 4, ít nhất 64
    seeds; infinite budget/locks off/earliest/no-repair/single-plan bằng B1.
12. Solver/pool không publish trực tiếp; full WP3 validator, certificate, state
    hash, pending transaction và matching ACK vẫn là gate cuối.

**Alternatives considered:** hard gate thành weighted penalty; C1 sinh thêm raw
candidate sau prune; cap bằng hash ID; cache không bind travel/version; latest ETA
chỉ trên paper mà không có route hold; mở cross-vehicle reassignment; in-memory
plan pool không checkpoint; CP-SAT nhiều thread; dùng wall-clock timeout làm
replay outcome; báo FEASIBLE thành OPTIMAL; lấy số paper làm default.

**Consequences:** `RB-WP4-001` Done, queue `RB-WP4-002..014` được phép thực hiện
tuần tự và chỉ `002` Ready. WP4 vẫn ở claim Implemented/Mechanically valid cho
từng ticket; hiệu quả chỉ được gọi là tín hiệu micro/exact-small trước paired
Layer 1/2. O-001/O-002/O-003/O-004 không đổi.

**Evidence:** `tasks/29`, `tasks/30`, Browser sources trong `21`; required suite
baseline 414/414 trước production WP4.

**Supersedes / superseded by:** Bổ sung ADR-020/022 cho policy/solver quality;
không đổi protocol/hash, reassignment O-001 hay validator/certificate semantics.

### ADR-024 — 2026-08-03 — Accepted

**Context:** `RB-WP4-002..012` đã tạo đầy đủ mechanisms nhưng closure còn cần bằng
chứng độc lập và audit source-level để tránh kết luận từ test happy path. Đặc biệt
cần chứng minh hard gate thực sự loại candidate, mapper/OR-Tools không cùng lỗi với
expected code, bounded omission đi xuyên diagnostics, và machine-local timing
không bị nâng thành effectiveness claim.

**Decision:** Đóng WP4 và Q2 core mechanical correctness vì:

1. B1 generator/selector khớp independent enumeration trên 64 fixtures trong
   published exact-small bound.
2. Production C1 mapper + actual pinned OR-Tools khớp một enumerator độc lập khác
   trên 64 fixtures; mọi objective level optimal với exact gap 0.
3. Hard-gate mutation fixture có raw set lớn hơn hard-feasible set; actual bounded
   omission truyền count/digest tách khỏi solver loss và validated fallback.
4. Cache/infinite-equivalence/plan-pool/checkpoint/deadline/replay/publication gates
   từ tickets trước vẫn pass trong full suite 557/557.
5. Source audit xác nhận objective không scalarize hard vector, solver không tự
   publish, candidate/solver/publication loss tách biệt và matching ACK là commit.
6. Synthetic runtime curve chỉ được ghi là promising machine-local signal; paired
   Layer 1/2, scale, service effect và user satisfaction vẫn unproven.
7. Review `docs/reviews/wp1-wp4-final/` là handoff hiện hành; historical review
   WP1–WP3 được giữ nguyên.

**Alternatives considered:** đóng chỉ vì 523 tests pass; dùng production comparer
làm oracle; ghi microbenchmark thành production SLA; mở luôn BeGo migration mà
chưa khóa process/transaction ownership; xóa historical environment blocker.

**Consequences:** `RB-WP4-001..014` Done, WP4 Complete. Windows Application Control
`0x800711C7` không tái xuất hiện ở closure run nhưng historical record không bị
xóa. Chỉ refinement-only `RB-WP5-001` Ready; không có WP5 implementation ticket.
O-001/O-002/O-003/O-004 và protocol/hash/validator/certificate semantics không đổi.

**Evidence:** closure blocks `RB-WP4-013/014`, `tasks/30`, `tasks/31`, final review,
required suite 557/557, Release/format/vulnerability/JSON/Markdown/process/diff gates.

**Supersedes / superseded by:** Đóng execution của ADR-023; không supersede
ADR-014/016/017/020/022 hoặc claim boundary trong `03`/`21`.

## 8. Work package tracker

| WP | Trạng thái | Bắt đầu | Kết thúc | Evidence |
|---|---|---|---|---|
| WP0 Scaffold | Complete | 2026-07-28 | 2026-07-28 | build + 8 RideBound + 25 backend + 7 frontend tests |
| WP1 Contracts | Complete; Q1 Release revalidated with host-policy exception | 2026-07-29 | 2026-07-29 | ADR-014–017 + 157/157 closure + WP1 revalidation 161/161 + replay/hash proof |
| WP2 Online baseline | Complete; physical/B1 gate, Debug 333/333; Release host-policy exception recorded | 2026-07-29 | 2026-07-30 | ADR-018–020 + Debug 333/333 + Release bundles + two-process tiny replay |
| WP3 Ledger/certificate | Complete; `001..014` DONE; Debug 414/414 | 2026-07-31 | 2026-08-02 | ADR-021/022 + `tasks/28` + full-solution 414/414 + WP3 process/checkpoint replay |
| WP4 Algorithms/solver | Complete; `001..014` Done; Q2 mechanical gate closed | 2026-08-03 | 2026-08-03 | ADR-023/024 + independent oracles + named policy/solver/Runner path + 557/557 + final review |
| WP5 BeGo integration | Refinement ready; only `RB-WP5-001`, no implementation | 2026-08-03 | — | `tasks/31-wp5-bego-integration-refinement.md` |
| WP6 Benchmark harness | Not started | — | — | — |
| WP7 FleetPy | Not started | — | — | — |
| WP8 Pilot/prereg | Not started | — | — | — |
| WP9 Main experiments | Not started | — | — | — |
| WP10 Cross-system | Not started | — | — | — |
| WP11 Product UX | Not started | — | — | — |
| WP12 Paper/release | Not started | — | — | — |

## 9. Change history

- 2026-08-03: Hoàn thành `RB-WP4-013..014` và đóng WP4 bằng ADR-024. B1
  generator/selector khớp independent oracle trên 64 fixtures; production C1
  mapper + actual OR-Tools khớp independent enumerator trên 64 fixtures, mọi
  objective optimal/gap 0. Thêm hard-gate mutation witness, actual bounded-loss
  propagation và synthetic 4–128 option curve. Final source/config/Runner/claim
  audit ở `reviews/wp1-wp4-final`; required suite 557/557, Release/format/package/
  JSON/Markdown/process/diff gates pass. Windows Application Control 0x800711C7
  không tái xuất hiện và chỉ còn historical record. Chỉ `RB-WP5-001` refinement Ready.
- 2026-08-03: Hoàn thành `RB-WP4-012`. Thêm canonical B1–B5/C1/C2 registry,
  strict WP4 configuration và domain-bound commitment+algorithm hash; manifest
  phải khớp policy ID/version/hash. B1–B4/C1/C2 map exact hierarchy sang OR-Tools,
  B5 giữ pool selector; Runner revalidate effective policy rồi stage
  ledger/certificate/plan-pool/state/hash/ACK. Solver status nằm trong hashed
  decision. 7 Algorithms + 9 Runner cases mới, gồm child-process CLI; format sạch,
  required suite 523/523. Chỉ `RB-WP4-013` chuyển Ready.
- 2026-08-03: Hoàn thành `RB-WP4-011`. Tách deterministic work budget cho
  generation/validation/solver, giữ candidate omission digest/saturation độc lập
  solver loss, và buộc mọi incumbent qua semantic validator injected. Portfolio
  fallback thử no-op rồi single-request theo lexicographic/ID; hết validation
  budget hoặc không pass trả Unknown không solution, không bịa incident. 12
  Application cases mới, format sạch, required suite 507/507. Chỉ
  `RB-WP4-012` chuyển Ready.
- 2026-08-03: Hoàn thành `RB-WP4-010`. Thêm project adapter pin
  `Google.OrTools 9.15.6755`, exact-one/at-most-one CP-SAT constraints,
  Sum/Maximum integer objectives và multi-pass lexicographic optimum fixing.
  Một worker/seed/conflict/deterministic-time budget explicit; status và bound
  không bị nâng sai, solution được canonical revalidation. 5 solver cases + 1
  architecture case mới, format sạch, required suite 495/495. Chỉ
  `RB-WP4-011` chuyển Ready.
- 2026-08-03: Hoàn thành `RB-WP4-009`. C2 dùng explicit 10-dimension warning
  profile, cùng one-pass hard gate C1, ordered warning-excess vector trước raw
  revision và không scalar hóa đơn vị. Warning phải finite-hard-bounded; toàn
  warning tắt gọi đúng selector C1. 6 Algorithms cases mới, format sạch,
  required suite 489/489. Chỉ `RB-WP4-010` chuyển Ready.
- 2026-08-03: Hoàn thành `RB-WP4-008`. C1 assess+hard-filter trong cùng WP3
  validator pass, rank accepted/worst exact ceiling PPM/ordered 10-vector/cost/
  IDs; zero hard reserve rank saturated nhưng không đổi feasibility. Khi mọi
  hard limit unbounded, bỏ treatment ranking để semantic decision đúng B1. 6
  Algorithms cases mới, format sạch, required suite 483/483. Chỉ `RB-WP4-009`
  chuyển Ready.
- 2026-08-03: Hoàn thành `RB-WP4-007`. Thêm versioned canonical plan pool vào
  Application state/checkpoint, exact semantic plan ID, shared-pool enumeration
  với exact/bounded work semantics, Pareto dominance, max-min diversity và
  executable-prefix consensus. Chỉ distinguished được apply; alternative khác
  được rebase đúng next route version. Restore kiểm tra identity, assignment/
  frozen/physical và run equality. 12 cases mới (3 Application, 5 Algorithms,
  4 Runner), format sạch, required suite 477/477. Chỉ `RB-WP4-008` chuyển Ready.
- 2026-08-03: Hoàn thành `RB-WP4-006`. Thêm B4 one-pair same-vehicle
  remove/reinsert cho waiting incumbent hoàn toàn trong mutable suffix, explicit
  cap và exact/bounded repair-loss accounting; frozen/onboard/assignment giữ
  nguyên, mọi route qua physical validator. Tách order-sensitive search-node
  digest khỏi order-insensitive omission-set digest sau khi differential case
  phát hiện route permutations bị đồng nhất. 7 Algorithms cases mới; format
  sạch, required suite 465/465. Chỉ `RB-WP4-007` chuyển Ready.
- 2026-08-03: Hoàn thành `RB-WP4-005`. B2 đánh giá cùng raw pool với mọi
  cumulative limit unbounded và chọn accepted/material/10-vector/cost/ID; B3 chỉ
  hard-freeze theo horizon/lock explicit, inclusive boundary, không có numeric
  default và không rò source budget. Sửa canonical overflow ở vector và fleet
  cost. 8 Algorithms + 1 Domain cases mới, gồm B2 16-seed raw preservation;
  format sạch, required suite 458/458. Chỉ `RB-WP4-006` chuyển Ready.
- 2026-08-03: Hoàn thành `RB-WP4-004`. Thay request-ID/hash cap bằng global
  deterministic best-first frontier; thêm work cap, tổ hợp count/saturation và
  stable omission digest. Diagnostics tách request omission, unknown-feasibility
  raw paths và known-feasible cap loss; exact mode fail-closed. 5 Algorithms
  conservation/priority/monotonic cases mới, format sạch, required suite 449/449.
  Chỉ `RB-WP4-005` chuyển Ready.
- 2026-08-03: Hoàn thành `RB-WP4-003`. Thêm backward forward-slack certificate,
  bounded cache bind run/vehicle/position/route/time/travel, executable
  current-node origin hold và revalidation + exact service equivalence. Cache
  không đảo vai validator; vượt certificate không bị suy thành infeasible. 9
  Algorithms mutation/equivalence cases mới, format sạch và required suite
  444/444. Chỉ `RB-WP4-004` chuyển Ready.
- 2026-08-03: Hoàn thành `RB-WP4-002`. Thêm solver-neutral
  `CandidateSelectionProblem`/solution/port trong Application: canonical model,
  đúng một option/vehicle, request uniqueness, ordered lexicographic
  Sum/Maximum, deterministic budget, exact bound/gap diagnostics và status
  truthful tách OPTIMAL/FEASIBLE/INFEASIBLE/UNKNOWN/MODEL_INVALID/SAFE_FALLBACK.
  20 Application adversarial cases + một Architecture boundary case; required
  `dotnet test RideBound.slnx` pass 435/435. Chỉ `RB-WP4-003` chuyển Ready.
- 2026-08-03: Hoàn thành `RB-WP4-001` refinement bằng ADR-023 và ordered queue
  `RB-WP4-002..014`. In-app Browser đọc lại nguồn bắt buộc và bổ sung waiting,
  forward-slack/feasibility, future-guidance cùng official OR-Tools/NuGet. Khóa
  common raw candidate/cap, executable origin hold, same-vehicle repair,
  canonical plan pool, multi-pass objective, deterministic solver budget,
  exact-small equivalence và WP3 publication gate. Chỉ `RB-WP4-002` Ready;
  chưa claim hay ghi production WP4 implemented tại mốc refinement.
- 2026-08-03: Re-run đúng required `dotnet test RideBound.slnx` sau khi Smart App
  Control không còn chặn fresh DLL: full solution pass 414/414 — Contracts 133,
  Domain 134, Application 34, Algorithms 48, Runner 58, Architecture 7; exit 0,
  không failed/skipped. Chuyển `0x800711C7` thành historical environment record,
  không còn là current blocker.
- 2026-08-02: Hoàn thành `RB-WP3-008..014` và đóng WP3 bằng ADR-022. Thêm
  incident/breach separation, independent full-state commitment validator,
  strict certificate/action/schema cross-binding, named configuration hash,
  Runner commitment publication/ACK, canonical checkpoint/restore và evidence
  property/mutation/exact-small/process. Audit toàn WP1–WP3 sửa thêm state-boundary,
  genesis-route, sequence exhaustion, pickup-window, breach/ledger và checkpoint
  reachability bugs; sửa cả WP2/WP3 demo pipe để stdin UTF-8 không BOM và không
  phụ thuộc PowerShell host. Browser recheck 5 paper khóa claim/tối ưu còn thiếu cho WP4.
  Inventory 414; Release build/format và WP1/WP2/WP3 clean replay pass. Required
  full solution bị host policy `0x800711C7`, được tách minh bạch thành suite/
  policy-safe/process evidence. Tạo review `docs/reviews/wp1-wp3/README.md`; chỉ
  `RB-WP4-001` refinement READY, chưa có production WP4 code.
- 2026-08-02: Bắt đầu `RB-WP3-008` sau khi đọc lại toàn bộ tài liệu bắt buộc,
  README, đặc tả và execution plan WP1–WP3; baseline full-solution tái hiện đúng
  Windows Application Control `0x800711C7` đã ghi trước đó. Chưa nâng trạng thái
  implementation; đang audit source và incident/breach boundary trước code.
- 2026-07-30: Revalidate WP2 end-to-end rồi commit riêng `07432ce`. Hoàn thành
  refinement ADR-021/queue 14 ticket và triển khai đúng nửa WP3 `001..007`:
  promise/policy/vector, shared schedule/promise projection, three-way delta,
  append-only ledger trong ACK boundary, budget và phase locks. Debug inventory
  378/378 suite evidence; chưa có incident/certificate/Runner/checkpoint, next duy nhất
  `RB-WP3-008`.
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
