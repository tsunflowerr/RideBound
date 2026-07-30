# RideBound

**Auditable bounded-revision online ridepooling.**

RideBound nghiên cứu ghép xe trực tuyến trong khi giới hạn có chứng nhận số lần
và mức độ thay đổi lời hứa đã phát cho từng hành khách. Repository này độc lập
với BeGo/OptiGo; BeGo chỉ là một hệ thống nguồn và baseline được kết nối qua
adapter hoặc scenario chuẩn hóa.

## Trạng thái

- WP0, WP1 Contracts/Q1 và WP2 physical/B1 `RB-WP2-001..012` đã hoàn
  thành. WP3 đang thực hiện: `RB-WP3-001..007` đã xong; ticket duy nhất
  tiếp theo là
  [`RB-WP3-008`](docs/tasks/28-wp3-ledger-certificate-ticket-plan.md#rb-wp3-008--incident-lifecycle-và-breach-ledger).
- Đã có protocol/schema v1, canonical JSON/hash, long-lived NDJSON runner,
  typed online input, Domain state/route, atomic event reducer và independent
  physical validator, deterministic insertion/B1, exact-small oracle, online
  produced decision và foundation promise/delta/ledger/budget/phase-lock.
  Chưa có incident breach ledger, combined commitment validator, certificate
  publication, checkpoint, C1/OR-Tools behavior hoặc adapter.
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

Trước khi thay đổi code, đọc `AGENTS.md` và thứ tự tài liệu được chỉ định tại
[`docs/00-index.md`](docs/00-index.md).
