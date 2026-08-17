# Thiết kế đánh giá ba lớp

## 1. Hai lớp chính, một lớp bổ sung

| Lớp | Môi trường | Vai trò bằng chứng |
|---|---|---|
| 1 | BeGo common codebase/replay harness | Chính: tác động trong hệ thống mục tiêu |
| 2 | FleetPy common simulator | Chính: benchmark chuẩn, modular, dữ liệu công khai |
| 3 | RidePy hoặc AMoD2 | Bổ sung: robustness qua một event/simulator stack độc lập |

Layer 3 không thay Layer 1/2. Nó kiểm tra kết quả có phải artifact của một simulator hay không.

## 2. Layer 1 — BeGo

### Thiết kế

`RideBound.Runner` chạy hai policy:

- B1 `rolling-cost`;
- C1 `ridebound-hard-vector`.

BeGo adapter cung cấp cùng event stream và travel snapshots. Đây là so sánh cùng codebase chặt nhất.

### B0 hiện tại

Planner BeGo hiện tại là snapshot/static. B0 chỉ cho biết “hệ thống trước khi mở rộng làm gì”; không được dùng là đối chứng duy nhất vì không chịu request động.

### Bằng chứng cần xuất

- decision transcript;
- promise/ledger transcript;
- certificate;
- route/request lifecycle;
- metrics per rider/epoch/run;
- manifest và binary hash.

## 3. Layer 2 — FleetPy

Khóa ban đầu:

- FleetPy `1.0.2`;
- commit `053aa9d4fcfde91c5d303435d5748f9206c071b0`;
- pin container digest sau khi build;
- ghi license và dataset DOI.

FleetPy phù hợp vì:

- agent-based MoD simulation;
- vehicle/user/operator interaction;
- assignment/repositioning/routing modules;
- benchmark Manhattan, Chicago, Munich;
- output user/operator statistics;
- paper 2026 nhấn mạnh reproducibility/comparability.

Adapter gọi cùng runner cho B1 và C1.

## 4. Layer 3 — cross-system

### Lựa chọn mặc định: RidePy

Pin:

- `v2.10.1`;
- commit `bf1863e49a432f2f1f6230f86b2777a5ef5b9f14`.

Lý do:

- dispatcher là extension point rõ;
- event log và analytics có sẵn;
- transport space có plane/graph;
- ít nặng hơn MATSim/C++ stack.

### Lựa chọn thay thế: AMoD2

AMoD2 hấp dẫn vì:

- Manhattan data;
- Greedy, SBA, OSP;
- OSP cho reassignment;
- quy mô lớn.

Nhưng cần Gurobi/build C++ hoặc sửa solver. Chỉ chọn sau preflight.

### Không ưu tiên

- OpenRidepoolSimulator: dependency MOSEK cũ, ít commit.
- AMoDeus: MATSim/Java integration lớn; stretch target.

Layer 3 hoàn thành khi **ít nhất một** lựa chọn pass, không bắt buộc tất cả.

## 5. So sánh “cùng điều kiện”

Trong một layer, B1 và C1 phải có:

- cùng demand order;
- cùng travel realization;
- cùng fleet/capacity;
- cùng user cancellation model;
- cùng event batching;
- cùng candidate cap;
- cùng solver budget;
- cùng hardware/container;
- paired seed.

Giữa các layer, không yêu cầu output bitwise giống vì simulator có event scheduling khác. Phải công bố semantic differences.

## 6. Capability matrix

Mỗi adapter điền:

| Capability | BeGo | FleetPy | RidePy | AMoD2 |
|---|---:|---:|---:|---:|
| Edge progress | TBD | TBD | TBD | TBD |
| Dynamic travel update | TBD | TBD | TBD | TBD |
| Vehicle reassignment | TBD | TBD | TBD | Có ở OSP, adapter TBD |
| Stop relocation | TBD | TBD | TBD | TBD |
| Old-plan projection | TBD | TBD | TBD | TBD |
| Incident event | TBD | TBD | TBD | TBD |
| Same runner binary | bắt buộc | bắt buộc | bắt buộc | bắt buộc |

Không điền từ suy đoán; adapter preflight phải kiểm tra bằng test.

## 7. Experimental phases

### E0 — protocol conformance

Canonical tiny scenario, không traffic dynamic.

### E1 — correctness

Exact-small oracle, certificate, deterministic replay.

### E2 — pilot

Ít scenario/seed để đo variance và runtime; không dùng để tuyên bố kết quả cuối.

### E3 — preregistered main run

BeGo + FleetPy trên grid scenario đã khóa.

### E4 — cross-system

Subset đại diện trong RidePy/AMoD2.

### E5 — robustness/ablation

Budget, demand, fleet, traffic shock, solver deadline.

## 8. Chống leakage và tuning theo test

- Tách pilot scenario/time range khỏi confirmatory set.
- Chỉ pilot được dùng chỉnh weight/threshold.
- Sau preregistration, config hash bị khóa.
- Full-run failure chỉ sửa bug có test; sau sửa phải rerun toàn bộ affected conditions.
- Không bỏ seed xấu trừ khi manifest/runner invalid theo rule định trước.

## 9. Compute fairness

Ba cách report:

1. Equal wall-clock budget.
2. Equal candidate cap.
3. Quality-runtime Pareto curve.

Main comparison dùng equal deadline và common candidate generator. Pareto curve là phân tích bổ sung.

## 10. Điều kiện Layer 2/3 được công nhận

- upstream version/commit/container pin;
- clean environment build;
- adapter contract tests;
- event transcript lưu;
- capability matrix;
- at least one native sanity scenario;
- no algorithm reimplementation;
- B1/C1 paired runs;
- raw results + evaluation script hash.

## 11. Kết luận khi các layer không đồng thuận

Không lấy đa số phiếu. Điều tra:

- event batching;
- travel-time semantics;
- reassignment capability;
- position/freeze semantics;
- candidate truncation;
- solver deadline;
- demand filtering.

Report heterogeneity là kết quả. Chỉ gọi “robust” khi hướng hiệu ứng và CI phù hợp với tiêu chí đã đăng ký.

## 12. Common harness boundary đã khóa cho WP6

ADR-026 và [WP6 contract v1](benchmarking/wp6-contract-v1.md) khóa một đường đo
chung đứng ngoài policy/core:

- normalizer tạo canonical scenario bất biến từ nguồn public đã pin;
- plan tạo paired run theo compatibility class, không ép B5 vào candidate semantics
  của B1/B2/B3/B4/C1/C2;
- seed 256-bit được dẫn xuất theo label bằng HMAC-SHA-256, không phụ thuộc thứ tự
  thực thi;
- mỗi planned run gọi đúng external pinned Runner process và tạo đúng một terminal
  record `succeeded`, `failed` hoặc `excluded`;
- metrics chỉ được suy từ raw transcript với numerator/denominator/missingness rõ;
- bundle strict BagIt-compatible tự kiểm source/config/binary/result hashes và claim
  profile.

WP6 chỉ đóng reproducibility/mechanical harness. FleetPy control adapter và bằng
chứng effectiveness vẫn thuộc WP7–WP9.

**Trạng thái closure 2026-08-13:** ADR-036 đóng WP6 sau fresh tiny A và public-medium
H/I trên exact source cuối. H/I có 0 mismatch trên 16 top-level + 72 per-run semantic
fields; resource/full/bundle identity khác hợp lệ và đều externally verified. Đây vẫn là Layer-0/common
harness evidence, chưa phải Layer-2 FleetPy closed-loop.
