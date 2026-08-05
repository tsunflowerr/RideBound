# Testing, reproducibility và quality gates

## 1. Test pyramid

```text
Formal/property invariants
Unit tests
Golden contract tests
Exact-small differential tests
Replay/snapshot tests
Adapter integration tests
Simulator end-to-end tests
Performance/soak tests
Full experimental validation
```

Không dùng full simulator run để thay unit/property tests.

## 2. Core unit tests

- promise delta từng dimension;
- cumulative total variation;
- switch đi rồi quay lại vẫn tiêu hai lần;
- initial promise không tính revision;
- material vs raw revision;
- budget zero/infinite;
- phase lock;
- accepted-never-rejected;
- incident breach separation;
- stable tie-break;
- overflow/unit conversion.

## 3. Property tests

### Ledger

- cumulative counters monotonic;
- ledger after sequence bằng fold của deltas;
- replay same events cho same ledger.

### Feasible set

- nới budget không loại candidate cũ;
- thêm hard lock không mở rộng feasible set;
- infinite budget RideBound-degenerate bằng B1.

### Route

- pickup trước drop;
- load trong `[0, capacity]`;
- frozen prefix giữ nguyên;
- onboard rider có drop.

### Hash/idempotency

- serialize/deserialize không đổi canonical hash;
- duplicate event cùng payload không đổi state;
- duplicate event khác payload fail.

## 4. Exact-small differential tests

Exact enumerator kiểm:

- candidate completeness ở size nhỏ;
- lexicographic optimum;
- commitment prune witness;
- OR-Tools status/objective.

Report riêng:

- generator gap;
- solver gap;
- final selection gap.

Không dùng current BeGo public importer làm oracle.

## 5. Golden protocol tests

Fixtures trong `benchmarks/schemas/fixtures`:

- valid/invalid messages;
- version compatibility;
- error codes;
- full tiny transcript;
- checkpoint.

.NET, FleetPy adapter và cross-system adapter dùng cùng fixtures.

## 6. Replay tests

- fresh run vs transcript replay;
- checkpoint restore vs no-crash;
- event batch grouping rule;
- same seed/config/binary;
- stable decision hash chain;
- old-plan projection.

Nondeterministic performance mode không thay deterministic regression suite.

## 7. Validator mutation tests

Cố ý làm hỏng proposed decision:

- vượt capacity;
- swap drop trước pickup;
- phá locked stop;
- xóa accepted rider;
- vượt một budget;
- sửa ledger delta;
- đổi plan version;
- tamper hash.

Validator phải bắt đúng và trả witness.

## 8. Adapter tests

### BeGo

- mapping ID/unit;
- bootstrap route;
- route cost snapshot;
- transaction/outbox;
- feature flag/rollback.

### FleetPy

- callback mapping;
- offer-confirmation lifecycle;
- `VehiclePlan` conversion;
- lock preservation;
- assign/ack.

### RidePy/AMoD2

- global fleet snapshot;
- multi-plan apply atomicity;
- event order;
- capability fallback;
- native validator reconciliation.

## 9. Performance tests

- warm/cold runner startup;
- long-lived pipe throughput;
- p50/p95/p99 decision latency;
- fleet/request scale curve;
- memory growth;
- checkpoint size/time;
- 24h simulation soak;
- timeout/fallback behavior.

Máy test ghi CPU, RAM, OS, runtime, container digest. Không so runtime giữa máy khác mà không chuẩn hóa.

## 10. Existing regression gates

Mốc 2026-07-27:

- backend: 25 pass;
- frontend: 7 pass.

Mọi integration change giữ ít nhất các test này. Nếu test count đổi, giải thích test thêm/xóa trong status log.

## 11. CI stages

### PR fast

- format/lint;
- build;
- unit/property sample;
- golden schema;
- existing tests.

### Main branch

- exact-small;
- deterministic replay;
- database integration;
- adapter smoke.

### Nightly

- larger property seeds;
- FleetPy/cross-system integration;
- performance regression;
- dependency/license scan.

### Release/experiment

- clean container rebuild;
- full manifest validation;
- prereg config hash;
- artifact checksum;
- paired experiment.

### CI đã triển khai cho WP0

Các workflow trong `.github/workflows` hiện thực hóa gate ban đầu như sau:

- `ci.yml` chạy khi mở/cập nhật PR, push `main`, hoặc chạy thủ công:
  - kiểm tra whitespace bằng `dotnet format`;
  - build Release với warning là error;
  - chạy toàn bộ test và thu OpenCover/TRX làm evidence;
  - audit package transitive và dependency diff của PR;
  - chỉ sau khi main pass mới publish runner artifact có tên gắn commit SHA.
- `sonar.yml` build/test lại bên trong SonarScanner for .NET và chờ Quality Gate.
  Workflow chỉ bật khi repository có secret `SONAR_TOKEN` và variables
  `SONAR_PROJECT_KEY`, `SONAR_ORGANIZATION`; thiếu cấu hình thì phát notice thay vì
  làm mọi PR fail.
- `pr-agent.yml` cung cấp review/description tự động cho PR không phải draft. Workflow
  chỉ bật khi có secret `OPENAI_KEY`; review AI là tín hiệu hỗ trợ, không thay thế
  format, build, test, architecture rule hoặc human approval.
- Dependabot cập nhật NuGet và GitHub Actions hàng tuần. Dependency review yêu cầu
  public repository hoặc GitHub Advanced Security nếu repository là private.

Branch protection nên yêu cầu tối thiểu `Code formatting`,
`Build, test and coverage`, `NuGet vulnerability audit` và `Dependency review`.
Chỉ thêm `Sonar quality gate` vào required checks sau khi ba giá trị Sonar ở trên
đã được cấu hình và một lần scan main thành công.

## 12. Reproducibility bundle

Mỗi result bundle có:

```text
README
manifest
preregistration hash
source commit
binary hash
container digests
simulator commits
dataset checksums
policy configs
seeds
raw transcripts
metric tables
analysis scripts
exclusion/failure log
environment fingerprint
```

Một người khác phải chạy được tiny/medium reproduction trước khi gọi release.

## 13. Quality gates theo mức

### Q0 — docs

- claim boundary và traceability đầy đủ.

### Q1 — contracts

- schema/golden/hash pass.

Q1 được đóng ngày 2026-07-29 cho phạm vi contract/runner WP1: full Release
solution 157/157 pass tại mốc đóng, đúng 10 required fixture, exact transcript
replay/hash và tamper proof pass. Sau assertion vocabulary và revalidation
exact-retry, WP1-only inventory là 161: Contracts 115, Runner 38, Architecture 7
và Domain 1 cùng pass ở Release. Host-policy history được giữ trong `18`. Đây
không phải Q2 core correctness; 9 future-behavior fixture chỉ được
schema-validate tại mốc Q1.

### Q2 — core correctness

- invariants/property/exact-small pass.

WP2 đã hoàn thành phần physical/B1 của Q2: typed online fixtures, exhaustive
request lifecycle, route properties, atomic reducer/replay, independent physical
validator, deterministic insertion/B1 và independent exact-small oracle.
Published bound 2 vehicle/2 pending request qua 32/32 seeds với generator/
selection gap bằng 0; tiny four-epoch replay/tamper cũng pass.

Logical inventory sau WP2 là 333 (Contracts 128, Domain 89, Application 15,
Algorithms 45, Runner 49, Architecture 7). Required Debug
`dotnet test RideBound.slnx` pass 333/333; Release build/format pass. Release
xUnit bị Windows Application Control chặn fresh unsigned DLL bằng `0x800711C7`
ở ba suite, nhưng đúng artifacts đó pass qua policy-safe bundles/process checks.
Exception nằm trong `18` và không được tính là Release full-solution xUnit pass.

Q2 core correctness được đóng ngày 2026-08-03 bởi WP4 sau exact-small/infinite-
budget/actual-solver equivalence. WP3 đã đóng P1/P2/P3 commitment correctness;
điều đó không biến WP2 B1 thành treatment và không chứng minh effectiveness.

ADR-023 đã khóa WP4 exact-small bound: tối đa 2 vehicle, 2 pending request,
1 waiting incumbent repair/vehicle, plan-pool cap 4 và ít nhất 64 deterministic
seeds. Báo riêng raw/exact feasible candidates, bounded retained/hard-pruned,
enumerator optimum, solver incumbent/bound/gap và final semantic decision.
Cache on/off, route/position/time/travel invalidation, origin-hold equivalence,
infinite-budget B1 degeneration, OR-Tools status và fallback đều là gate bắt buộc.

Tại checkpoint lịch sử sau `RB-WP3-001..007`, logical inventory là 378:
Contracts 128, Domain 126,
Application 23, Algorithms 45, Runner 49 và Architecture 7. Cùng source tree,
các suite đã pass 378/378 khi chạy tách; full-suite process có exception môi
trường Windows Application Control trước assertion, được ghi trong `18`.
Evidence mới bao gồm:

- exact boundary cho đủ 10 budget dimension, hard zero và unbounded;
- canonical overflow có exact-dimension witness; enum/lock không hợp lệ bị chặn;
- 441 cặp before/delta kiểm monotonic feasible set khi nới limit 20 lên 40;
- initial promise zero, P1 vector conservation, stale version và no-hidden-refund;
- publication id global-unique và promise service order không thể pickup sau drop;
- node/edge shared schedule, initial/onboard promise projection;
- three-way delta đủ dimension và case `visible != exogenous + decision`;
- pending ledger chỉ commit cùng matching ACK;
- accepted/onboard/freeze/final-confirmation lock witnesses.

Ở checkpoint đó đây mới là nửa đầu WP3; các phần còn thiếu nay đã được hoàn thành
trong `RB-WP3-008..013` và được đóng bởi ADR-022.

### WP3 closure evidence

WP3 bổ sung các gate sau ngoài suite thông thường:

- 64 seed × 12 revision history cho P1 conservation/no-refund trên đủ 10 chiều;
- killing projection mutation cho từng dimension và stage-order/state-boundary
  tests cho validator độc lập;
- exact-small commitment oracle 16 seed không gọi production validator/generator;
- P3: candidate đã feasible không mất đi khi nới pickup-ETA hard limit 40 → 160;
- incident open/resolve/breach separation và breach-budget cross-check;
- certificate tamper/cross-binding tests;
- clean-process replay hai lần và checkpoint-restore suffix equivalence trong
  `scripts/run-wp3-commitment-demo.ps1`;
- dependency/build/format/schema/diff checks ở closure audit.

Inventory closure là 414: Contracts 133, Domain 134, Application 34, Algorithms
48, Runner 58 và Architecture 7. Required `dotnet test RideBound.slnx` pass
414/414 ngày 2026-08-03. Policy-safe harness và bốn clean-process cases vẫn là
evidence bổ sung; Windows Application Control `0x800711C7` chỉ còn là historical
host-policy record trong `18`.

Các test “mutation” ở đây là explicit mutation-killing cases source-controlled,
không phải một mutation-score phần trăm từ công cụ ngoài. Không được ghi một điểm
mutation giả khi chưa chạy tool đo độc lập.

Mốc WP4 sau `RB-WP4-011`: logical inventory 507 — Contracts 133, Domain 135,
Application 69, Algorithms 94, Solvers.OrTools 5, Runner 62 và Architecture 9.
Required `dotnet test RideBound.slnx` pass 507/507 ngày 2026-08-03; format pass.
Evidence mới gồm truthful CP-SAT status/bound/replay và deterministic fallback
không dùng unvalidated incumbent, nhưng Q2 vẫn chưa đóng trước oracle/performance
ticket `RB-WP4-013`.

Mốc sau `RB-WP4-012`: logical inventory 523 — Contracts 133, Domain 135,
Application 69, Algorithms 101, Solvers.OrTools 5, Runner 71, Architecture 9.
Required suite pass 523/523. Bổ sung objective-mapping equivalence cases, actual
OR-Tools Runner decision, UNKNOWN→validated fallback, manifest/config binding,
wrong-ACK atomicity, B5 checkpoint restore và child-process `--wp4-config` smoke.
Đây vẫn là mechanical/integration evidence; scale curve và 64+ seed independent
oracle được bổ sung ở `RB-WP4-013`.

### WP4 closure và Q2 gate

Inventory closure là 557 — Contracts 133, Domain 135, Application 69,
Algorithms 134, Solvers.OrTools 6, Runner 71, Architecture 9. Required
`dotnet test RideBound.slnx` pass 557/557 ngày 2026-08-03, 0 failed/skipped.
Release warning-as-error 0 warning/error, format verify pass, vulnerability audit
không báo direct/transitive package và diff check pass.

Evidence ngoài expected-case suite:

- B1 generator/selector khớp independent enumerator trên 64 fixtures;
- C1 production mapper + actual OR-Tools khớp independent oracle trên 64 fixtures,
  mọi objective level `OPTIMAL`, exact gap numerator 0;
- hard-gate removal mutation bị giết trên fixture có raw > hard-feasible set;
- actual bounded request omission truyền count/digest đến execution diagnostics
  tách solver `UNKNOWN` và validated safe fallback;
- cache equivalence/invalidation, infinite C1=B1, C2-disabled=C1, plan-pool
  checkpoint/tamper và deadline/fallback gates pass;
- synthetic 4/16/32/128-variable curve đều exact optimal, p50 wall quan sát
  2.389/12.160/21.406/91.004 ms trên máy audit.

Q2 được đóng cho **core mechanical correctness**. Synthetic timing không đóng
Q3–Q6, không chứng minh demand-scale performance, service effectiveness hoặc
user satisfaction; các claim đó vẫn cần paired replay/preregistration.

### Q3 — BeGo

- same-codebase paired replay pass.

### Q4 — FleetPy

- Layer 2 preflight + paired runs pass.

### Q5 — cross-system

- một independent adapter pass.

### Q6 — confirmatory evidence

- preregistered runs, stats và artifact release.

Q2 đã đạt mechanical gate; vẫn không bắt đầu full confirmatory experiments trước
WP5–WP8 adapter, harness, pilot và preregistration gates.

## 14. Definition of done cho một code task

- Scope khớp work package.
- Test mới chứng minh behavior.
- Existing regression pass.
- Docs/ADR/status cập nhật.
- Không để placeholder `TBD` trong contract đã khóa.
- Output không overclaim.
- Không sửa vendor checkout.
- Không đưa secret/data lớn vào Git.
