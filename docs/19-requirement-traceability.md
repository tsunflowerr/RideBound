# Ma trận truy vết yêu cầu

## 1. Yêu cầu từ mục tiêu dự án

| ID | Yêu cầu | Thiết kế/tài liệu | Artifact dự kiến | Verification | Trạng thái |
|---|---|---|---|---|---|
| R-001 | Hệ thống RideBound độc lập | `05` | `Domain/Application/Runner` trong Git repo riêng | 5 architecture tests | WP0 verified |
| R-002 | Gắn được vào BeGo | `02`, `14` | BeGo adapter/API | integration replay | Planned |
| R-003 | Core mang sang benchmark | `05`, `06` | contracts + runner | same binary hash | Planned |
| R-004 | Layer 1 cùng codebase | `09` | B1/C1 chung RideBound engine; BeGo export adapter | paired runs | Planned |
| R-005 | Layer 2 simulator chung | `09`, `12` | FleetPy adapter | Layer 2 gate | Planned |
| R-006 | Layer 3 framework độc lập | `09`, `13` | RidePy/AMoD2 adapter | Layer 3 gate | Planned |
| R-007 | Giới hạn cumulative revision | `04`, `07` | ledger/validator | property/mutation tests | Planned |
| R-008 | Nhiều chiều promise | `04`, `07` | promise vector | golden fixtures | Planned |
| R-009 | Certificate/witness | `07` | certificate DTO/validator | invalid-plan mutations | Planned |
| R-010 | Đánh giá bằng data rõ | `10` | manifests/data pipeline | checksums/validation | Planned |
| R-011 | Metric/statistics rõ | `11` | analysis package | prereg/full report | Planned |
| R-012 | Dùng paper nhưng không làm lại | `03`, `21` | claim ledger | novelty re-audit | Docs v1 |
| R-013 | Nêu công nghệ và tối ưu | `05`, `08`, `12`–`14` | projects/adapters | build/performance | Docs v1 |
| R-014 | Nêu thêm/bỏ gì | `02`, mục 2 dưới | migration plan | code review | Docs v1 |
| R-015 | Agent sau biết tiếp tục | `00`, `17`, `18` | root `AGENTS.md` | reading order + status audit | Verified |
| R-016 | Thuật ngữ dễ hiểu | `22` | glossary | doc review | Docs v1 |
| R-017 | Không bias bằng dữ liệu giả | `01`, `10`, `11` | pilot/holdout | prereg audit | Planned |
| R-018 | Tái lập | `06`, `15` | hashes/bundle | clean reproduction | Planned |

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

Chỉ thêm `RideBound.Adapters.BeGo`, `RideBound.Persistence` và các test project
khác tại work package có behavior thật; không scaffold assembly rỗng.

### WP0 artifacts đã có

- `RideBound.slnx`, `global.json`, `Directory.Build.props`;
- 7 source project và 2 test project;
- `tests/RideBound.ArchitectureTests/DependencyRuleTests.cs`;
- root `README.md`, `AGENTS.md`, GitHub Actions CI;
- `benchmarks/`, `simulators/`, `scripts/`, `artifacts/` có boundary README;
- 6/6 RideBound tests cùng 25/25 backend và 7/7 frontend BeGo regression pass.

### Runtime/system

- event protocol;
- long-lived runner;
- online vehicle/request state;
- promise ledger;
- certificate validator;
- paired replay harness;
- simulator adapters;
- experiment artifact pipeline.

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
| F-001 | Nhận ordered event batch | protocol golden |
| F-002 | Advance/freeze vehicle state | state property |
| F-003 | Accept/reject/defer request | lifecycle tests |
| F-004 | Generate/evaluate route candidates | exact-small |
| F-005 | Publish promise | ledger tests |
| F-006 | Track cumulative/switch budgets | property tests |
| F-007 | Reject candidate vượt budget | witness golden |
| F-008 | Handle incident breach riêng | incident tests |
| F-009 | Checkpoint/restore | replay equivalence |
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
