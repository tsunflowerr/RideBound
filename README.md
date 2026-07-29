# RideBound

**Auditable bounded-revision online ridepooling.**

RideBound nghiên cứu ghép xe trực tuyến trong khi giới hạn có chứng nhận số lần
và mức độ thay đổi lời hứa đã phát cho từng hành khách. Repository này độc lập
với BeGo/OptiGo; BeGo chỉ là một hệ thống nguồn và baseline được kết nối qua
adapter hoặc scenario chuẩn hóa.

## Trạng thái

- WP0 scaffold và WP1 Contracts/Q1 đã hoàn thành; ticket tiếp theo là refinement
  [`RB-WP2-001`](docs/tasks/25-wp2-online-state-refinement.md).
- Đã có protocol/schema v1, canonical JSON/hash, long-lived NDJSON runner,
  session lifecycle, idempotent retry và golden replay proof. Chưa có Domain
  event reducer, online algorithm, solver, ledger/certificate thật hoặc adapter.
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

Trước khi thay đổi code, đọc `AGENTS.md` và thứ tự tài liệu được chỉ định tại
[`docs/00-index.md`](docs/00-index.md).
