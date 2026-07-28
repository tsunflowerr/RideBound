# Adapter kiểm chứng chéo

## 1. Mục tiêu

Layer 3 trả lời: cải thiện có tồn tại khi event scheduling, vehicle state và analytics đến từ một framework độc lập hay không?

Không cần feature parity tuyệt đối. Cần:

- same runner;
- same policy configs;
- capability matrix trung thực;
- paired baseline/RideBound;
- giải thích semantic deviations.

## 2. Quyết định mặc định

Thứ tự:

1. RidePy là adapter mặc định.
2. AMoD2 là lựa chọn thay thế/bổ sung sau preflight.
3. AMoDeus là stretch.
4. OpenRidepoolSimulator chỉ dùng khi có lý do lịch sử cụ thể.

Layer 3 được coi hoàn thành khi RidePy **hoặc** AMoD2 pass gate.

## 3. RidePy

### Upstream

- Repo: [PhysicsOfMobility/ridepy](https://github.com/PhysicsOfMobility/ridepy)
- Tag: `v2.10.1`
- Commit: `bf1863e49a432f2f1f6230f86b2777a5ef5b9f14`
- Paper: [RidePy: A fast and modular framework for simulating ridepooling systems](https://doi.org/10.21105/joss.06241)

### Source findings

RidePy có:

- `FleetState`;
- `VehicleState`;
- request/pickup/delivery/internal events;
- JSONL event output;
- dispatcher callable.

Dispatcher mặc định nhận **một request và stoplist của một vehicle**, trả updated stoplist/cost. `FleetState._apply_request_solution` chọn vehicle có cost nhỏ nhất. Vì RideBound cần nhìn toàn fleet, revision history và có thể sửa nhiều vehicle, chỉ viết một dispatcher callable là chưa đủ.

### Adapter đúng

Tạo subclass `FleetState`, ví dụ `CommitFleetState`:

- override `handle_transportation_request`;
- fast-forward vehicle state bằng API RidePy;
- serialize toàn fleet/ledgers sang runner;
- áp nhiều stoplist updates atomically;
- phát RidePy acceptance/rejection event tương ứng;
- phát sidecar promise/certificate events.

Có thể tái dùng `VehicleState.fast_forward_time`, `TransportSpace` và event analytics. Không sửa core thuật toán bằng Python.

### Hạn chế cần khóa

- RidePy built-in flow xử lý request tuần tự; đây phù hợp event-driven RideBound nhưng khác FleetPy batch.
- Nếu không hỗ trợ vehicle reassignment atomically, Layer 3 pin `reassignment=false` cho cả B1/C1.
- Traffic dynamics/old-plan projection cần preflight theo `TransportSpace`.

## 4. AMoD2

### Upstream

- Repo: [Leot6/AMoD2](https://github.com/Leot6/AMoD2)
- Commit đã kiểm tra ngày 2026-07-27: `aaa66dd728e5da754c31b1e1ac8ad09228a01f2d`
- License: MIT
- C++ simulator, Manhattan data.

### Source findings

- Dispatch cycle mặc định cấu hình theo giây, demo là 30s.
- Có GI, SBA, OSP.
- `dispatch_osp_impl.hpp` đặt `enable_reoptimization = true`.
- OSP xét các order `PICKING` và `PENDING`.
- Có guarantee orders đang picking được gán.
- Vehicle giữ schedule/waypoints; schedule validator có sẵn.

### Điểm gắn

Thêm `DispatcherMethod::EXTERNAL_COMMIT`:

1. Platform advance vehicle theo cycle.
2. Dựng canonical batch từ orders/vehicles/schedules.
3. Gọi runner.
4. Map route suffix thành `Waypoint`.
5. Chạy AMoD2 `ValidateSchedule`.
6. Apply schedules atomically.
7. Ghi sidecar transcript.

Không chèn RideBound constraint trực tiếp vào `dispatch_osp_impl.hpp`; làm vậy sẽ tạo bản thuật toán C++ khác.

### Solver

AMoD2 native OSP thường dùng Gurobi. RideBound B1/C1 vẫn dùng runner/OR-Tools. Native OSP là supplementary sanity baseline. Nếu build Gurobi không khả thi, không thay đổi main RideBound comparison; chỉ bỏ native OSP theo preflight log.

## 5. AMoDeus

- Repo: [amodeus-science/amodeus](https://github.com/amodeus-science/amodeus).
- Java/MATSim, GPL-2.0.
- Có dispatch API và thuật toán AMoD/ride sharing.

Chỉ chọn nếu:

- đã có MATSim environment;
- license/integration phù hợp;
- RidePy/AMoD2 không đạt capability bắt buộc;
- thời gian dự án cho phép.

## 6. OpenRidepoolSimulator

- Repo: [MAS-Research/OpenRidepoolSimulator](https://github.com/MAS-Research/OpenRidepoolSimulator).
- C++/MIT.
- README nêu MOSEK 8.1.0.56 và có thể không tương thích bản mới.
- Chỉ 4 commit tại thời điểm kiểm tra.

Có giá trị để hiểu implementation gần PNAS 2017, nhưng maintenance/dependency risk cao. Không đưa vào critical path.

## 7. Preflight scorecard

| Tiêu chí | RidePy | AMoD2 | AMoDeus | OpenRidepool |
|---|---:|---:|---:|---:|
| Build sạch trên môi trường dự án | TBD | TBD | TBD | TBD |
| License phù hợp | Có | Có | cần review GPL | Có |
| Event/vehicle state rõ | Cao | Cao | Cao | Trung bình |
| External dispatcher effort | Trung bình | Trung bình–cao | Cao | Cao |
| Reassignment | cần subclass/capability | Có trong OSP | có thể | TBD |
| Dynamic travel projection | TBD | hạn chế/TBD | có thể | TBD |
| Reproducibility docs | Cao | Trung bình | Trung bình | Thấp |
| Khuyến nghị | Mặc định | Thay thế | Stretch | Di sản |

`TBD` chỉ được đổi sau executable preflight.

## 8. Canonical cross-system scenario

Một graph nhỏ, 2 xe, 5 request:

- hai request accept ban đầu;
- một request mới gây insertion;
- một traffic update;
- một request không thể nhận vì budget;
- một pickup và drop-off.

Adapter pass khi:

- lifecycle đồng nhất;
- physical feasibility đồng nhất;
- B1/C1 decision direction giải thích được;
- ledger/certificate hash từ runner giống nhau với canonical inputs;
- deviations chỉ đến từ simulator state projection, được log.

## 9. Kết luận được phép

Nếu RidePy/AMoD2 cùng hướng:

> Kết quả ổn định qua hai simulation stacks đã kiểm tra.

Không viết:

> Thuật toán tổng quát cho mọi simulator hoặc mọi thành phố.
