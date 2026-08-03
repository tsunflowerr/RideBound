# RideBound

**Auditable bounded-revision online ridepooling.**

RideBound nghiên cứu ghép xe trực tuyến trong khi giới hạn có chứng nhận số lần
và mức độ thay đổi lời hứa đã phát cho từng hành khách. Repository này độc lập
với BeGo/OptiGo; BeGo chỉ là một hệ thống nguồn và baseline được kết nối qua
adapter hoặc scenario chuẩn hóa.

## Trạng thái

- WP0, WP1 Contracts/Q1, WP2 physical/B1 và WP3 ledger/certificate
  `RB-WP3-001..014` đã hoàn thành. Ticket duy nhất tiếp theo là refinement
  [`RB-WP4-001`](docs/tasks/29-wp4-algorithms-solver-refinement.md); chưa có
  production WP4 implementation nào READY.
- Đã có protocol/schema v1, canonical JSON/hash, long-lived NDJSON runner,
  typed online input, Domain state/route, atomic event reducer và independent
  physical validator, deterministic insertion/B1, exact-small oracle, online
  produced decision, promise/delta/ledger/budget/phase-lock, incident/breach,
  independent commitment validator, strict produced certificate và canonical
  checkpoint/restore. Chưa có C1/C2, B2–B5, OR-Tools behavior hoặc adapter.
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

Review giải thích chi tiết từng boundary/file từ WP1 đến WP3 nằm tại
[`docs/reviews/wp1-wp3/README.md`](docs/reviews/wp1-wp3/README.md).

Trước khi thay đổi code, đọc `AGENTS.md` và thứ tự tài liệu được chỉ định tại
[`docs/00-index.md`](docs/00-index.md).
