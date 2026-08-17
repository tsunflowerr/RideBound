# RideBound

**Auditable bounded-revision online ridepooling.**

RideBound nghiên cứu ghép xe trực tuyến trong khi giới hạn có chứng nhận số lần
và mức độ thay đổi lời hứa đã phát cho từng hành khách. Repository này độc lập
với BeGo/OptiGo; BeGo chỉ là một hệ thống nguồn và baseline được kết nối qua
adapter hoặc scenario chuẩn hóa.

## Trạng thái

- WP0–WP6 đã hoàn thành về mechanical correctness/reproducibility. WP6 có strict
  contracts/schemas, verified FleetPy Manhattan source, deterministic normalizer,
  fair plan/pairing/seed compiler, external-only Runner supervisor, append-only raw
  store, production + independent metric oracle, strict BagIt-compatible bundle và
  machine-readable mechanical-only claim checker.
- Fresh closure ngày 2026-08-13: tiny 8/8; public-medium H/I 8/8 mỗi process,
  16/16 semantic top-level và 72/72 semantic per-run fields exact; external verifier
  pass. Required `dotnet test RideBound.slnx` pass 770/770; WAC `0x800711C7` không
  tái hiện.
- WP7 FleetPy Layer 2 đã hoàn thành mechanical closure: Candidate portfolio opt-in có
  bounded dominance/oracle gate; adapter gọi external Runner, không port core sang
  Python; actual B1/C1 lifecycle, tiny và medium public physical loops đều được
  verifier độc lập kiểm. Chi tiết ở
  [`docs/benchmarking/wp7-014-fleetpy-layer2-closure-evidence-2026-08-15.md`](docs/benchmarking/wp7-014-fleetpy-layer2-closure-evidence-2026-08-15.md).
- ADR-039 khóa bằng ADR các ngữ nghĩa từng vào source mà chưa có quyết định
  (`initialPromiseTrigger`, baseline lock exogenous, cancel-after-acceptance, fail-closed
  C1 với witness typed, CLI flag Runner, event-induced plan update) và đóng một vòng tối
  ưu hot path **bất biến về kết quả**: chi phí thật nằm ở khóa memo slack chứ không ở
  validator, `19,63 → 0,64 µs` mỗi lookup, thời gian mỗi `Generate` giảm `23–39%` với
  toàn bộ counter giữ nguyên. Required suite `798/798`, pinned Python `50/50`, actual
  gates chạy lại trên Runner v8. Chi tiết ở
  [`docs/benchmarking/wp7-015-hot-path-and-semantics-closure-evidence-2026-08-17.md`](docs/benchmarking/wp7-015-hot-path-and-semantics-closure-evidence-2026-08-17.md).
- Kết quả hiện tại **không** chứng minh C1 tốt hơn B1, transport effectiveness,
  production SLA, fairness hoặc user satisfaction. WP8 mới khóa pilot/preregistration.
- Review hiện hành:
  [`docs/reviews/wp1-wp7-final/README.md`](docs/reviews/wp1-wp7-final/README.md).
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
  RideBound.Benchmarking.Contracts/ contract/schema/identity WP6
  RideBound.Benchmarking/      dataset→Runner→metric→bundle pipeline
tests/
  RideBound.Domain.Tests/
  RideBound.ArchitectureTests/
  RideBound.Application.Tests/
  RideBound.Algorithms.Tests/
  RideBound.Contracts.Tests/
  RideBound.Runner.Tests/
  RideBound.Solvers.OrTools.Tests/
  RideBound.Benchmarking.Contracts.Tests/
  RideBound.Benchmarking.Tests/
tools/                         WP6 acquire/normalize/harness/oracle/verifier CLI
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

Review hiện hành giải thích từng boundary/file từ WP1 đến WP6 nằm tại
[`docs/reviews/wp1-wp6-final/README.md`](docs/reviews/wp1-wp6-final/README.md);
các review WP1–WP3/WP4/WP5 được giữ như historical handoff.

Trước khi thay đổi code, đọc `AGENTS.md` và thứ tự tài liệu được chỉ định tại
[`docs/00-index.md`](docs/00-index.md).
