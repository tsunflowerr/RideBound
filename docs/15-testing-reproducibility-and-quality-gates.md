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

### Q2 — core correctness

- invariants/property/exact-small pass.

### Q3 — BeGo

- same-codebase paired replay pass.

### Q4 — FleetPy

- Layer 2 preflight + paired runs pass.

### Q5 — cross-system

- một independent adapter pass.

### Q6 — confirmatory evidence

- preregistered runs, stats và artifact release.

Không bắt đầu full experiments khi Q2 chưa đạt.

## 14. Definition of done cho một code task

- Scope khớp work package.
- Test mới chứng minh behavior.
- Existing regression pass.
- Docs/ADR/status cập nhật.
- Không để placeholder `TBD` trong contract đã khóa.
- Output không overclaim.
- Không sửa vendor checkout.
- Không đưa secret/data lớn vào Git.
