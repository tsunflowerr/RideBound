# RideBound

**Auditable bounded-revision online ridepooling.**

RideBound nghiên cứu ghép xe trực tuyến trong khi giới hạn có chứng nhận số lần
và mức độ thay đổi lời hứa đã phát cho từng hành khách. Repository này độc lập
với BeGo/OptiGo; BeGo chỉ là một hệ thống nguồn và baseline được kết nối qua
adapter hoặc scenario chuẩn hóa.

## Trạng thái

- WP0–WP5 đã hoàn thành. WP5 `RB-WP5-001..014` đã có guarded PostgreSQL schema,
  T1 idempotent intake/lease store, pinned long-lived Runner supervisor và
  deterministic privacy-preserving bootstrap mapper, authenticated HTTP API,
  fenced T2/T3 exact replay/crash recovery, ordered at-least-once SignalR relay
  rebuildable privacy-preserving audit timeline, default-off Shadow/Live rollout
  và same-input B1/C1 Layer-1 replay bundle tự kiểm checksum. Independent evidence
  đã kiểm randomized oracle, process-crash tại đủ durable boundary, 2–4 worker,
  5/5 required mutation và bounded local queue curves. Closure audit còn khóa
  subject-link append-only, chọn absolute outbox head rồi chỉ claim khi head operation
  đã `Applied`, và xử lý
  mỗi run bằng DI scope độc lập; backend Debug/Release đạt 154/154, 0 skip. Review:
  [`docs/reviews/wp1-wp5-final/README.md`](docs/reviews/wp1-wp5-final/README.md).
  Việc tiếp theo duy nhất là refinement-only
  [`RB-WP6-001`](docs/tasks/33-wp6-common-benchmark-harness-refinement.md) `READY`.
- Đã có protocol/schema v1, canonical JSON/hash, long-lived NDJSON runner,
  typed online input, Domain state/route, atomic event reducer và independent
  physical validator, deterministic insertion/B1, exact-small oracle, online
  produced decision, promise/delta/ledger/budget/phase-lock, incident/breach,
  independent commitment validator, strict produced certificate, canonical
  checkpoint/restore, B1–B5/C1/C2, plan pool, bounded generation, validated
  fallback và pinned multi-pass OR-Tools. BeGo đã có bootstrap/API→Runner→T2/T3
  end-to-end; paired replay hiện chỉ là mechanical/correctness evidence. Independent
  failure/concurrency/performance evidence đã hoàn thành ở mức cơ học/cục bộ;
  FleetPy, benchmark harness và bằng chứng hiệu quả vẫn chưa được thực hiện.
- Nguồn sự thật: [`docs/18-status-and-decision-log.md`](docs/18-status-and-decision-log.md).
- Lộ trình: [`docs/16-roadmap-and-work-packages.md`](docs/16-roadmap-and-work-packages.md).

## Cấu trúc

```text
src/
  RideBound.Domain/            DDD domain thuần, không phụ thuộc framework
  RideBound.Application/       use case và port
  RideBound.Contracts/         contract/protocol versioned
  RideBound.Algorithms/        policy và thuật toán
  RideBound.Solvers.OrTools/   adapter solver
  RideBound.Infrastructure/    implementation các port ngoài
  RideBound.Runner/            composition root và NDJSON runner
tests/
  RideBound.Domain.Tests/
  RideBound.ArchitectureTests/
  RideBound.Application.Tests/
  RideBound.Algorithms.Tests/
  RideBound.Contracts.Tests/
  RideBound.Runner.Tests/
benchmarks/                    schema, scenario và manifest tái lập
simulators/                    adapter FleetPy/RidePy/AMoD2
docs/                          đặc tả nghiên cứu và kỹ thuật
```

Dependency luôn hướng vào trong: Domain không tham chiếu project khác;
Application chỉ tham chiếu Domain. Framework, database, BeGo và simulator
không được xuất hiện trong Domain/Application.

## Bắt đầu

Yêu cầu .NET SDK được pin trong `global.json`.

```powershell
dotnet restore RideBound.slnx
dotnet build RideBound.slnx --no-restore
dotnet test RideBound.slnx --no-build
```

Chạy tiny WP2 online replay hai process sạch:

```powershell
./scripts/run-wp2-tiny-demo.ps1
```

Chạy WP3 commitment/certificate replay và checkpoint restore:

```powershell
./scripts/run-wp3-commitment-demo.ps1
```

Review hiện hành giải thích từng boundary/file từ WP1 đến WP5 nằm tại
[`docs/reviews/wp1-wp5-final/README.md`](docs/reviews/wp1-wp5-final/README.md);
các review WP1–WP3 và WP1–WP4 được giữ như historical handoff.

Trước khi thay đổi code, đọc `AGENTS.md` và thứ tự tài liệu được chỉ định tại
[`docs/00-index.md`](docs/00-index.md).
