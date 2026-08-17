# Nghiên cứu liên quan và ranh giới claim

## 1. Kết luận trước

Dynamic insertion, reassignment, ETA update, delay threshold, route similarity và least-commitment đều đã có tiền lệ. RideBound chỉ còn đủ mới khi giữ đúng tổ hợp:

> **per-passenger + path-dependent + multi-dimensional + cumulative/switch budget + multi-epoch rolling insertion + machine-checkable certificate**

Nếu bỏ “path-dependent” hoặc chỉ thêm `delay <= 5 phút`, novelty không còn đủ mạnh.

## 2. Các va chạm trực tiếp

### Multiple-plan dynamic DARP — Ackermann & Rieck, 2025

Nguồn: [Multiple plan approach for a dynamic dial-a-ride problem](https://doi.org/10.1007/s00291-025-00809-y).

Paper đã:

- xử lý request động;
- chèn request khi xe đang vận hành;
- ra quyết định accept/reject nhanh;
- giữ pool nhiều plan;
- chọn plan bằng best-plan hoặc consensus;
- mô tả rõ least-commitment;
- đề xuất time-based consensus dựa trên thời điểm sớm nhất hai plan khác nhau;
- giữ hard time window và maximum ride time.

Paper không cho RideBound quyền nhận:

- dynamic insertion;
- multiple plan;
- route similarity;
- trì hoãn cam kết nói chung.

Khoảng trống còn lại: không có ledger budget nhiều chiều theo từng rider qua toàn bộ chuỗi revision, không bound tổng ETA drift/số switch vehicle-stop-order và không xuất certificate theo rider.

### Time-Consistent DARP — Tellez và cộng sự, 2022

Nguồn: [The Time-Consistent Dial-a-Ride Problem](https://doi.org/10.1002/net.22063).

Paper tối ưu consistency của giờ phục vụ qua nhiều ngày trong tuần cho paratransit:

- request biết trước theo từng period;
- consistency được đo bằng time classes;
- trade-off với chi phí qua epsilon-constraint;
- LNS + set partitioning.

Khác biệt: đó là multi-period static planning giữa các ngày, không phải lịch sử sửa lời hứa trong một chuyến đang chạy. Vì vậy không claim “time consistency” nói chung là mới.

### Unreliability in ridesharing systems — 2020

Nguồn: [Unreliability in ridesharing systems](https://www.sciencedirect.com/science/article/pii/S0968090X2030735X).

Paper chỉ ra việc assignment/schedule thay đổi có thể tạo bất ổn đáng kể và phân biệt thông tin báo ban đầu với kết quả thực. Đây là động lực trực tiếp để RideBound chuyển từ đo hiện tượng sang kiểm soát nó.

Không claim việc phát hiện unreliability là mới.

### Anticipatory walking in ridepooling — 2026

Nguồn: [Where should the last passenger be dropped off? Anticipatory walking in ridepooling](https://www.sciencedirect.com/science/article/pii/S0968090X26001336).

Paper dùng schedule tham chiếu và cơ chế no-worse khi thay đổi điểm trả của hành khách cuối. Điều này làm hẹp claim:

- bảo vệ no-worse cho một update không mới;
- thay đổi điểm đón/trả có safeguard không mới.

RideBound khác ở ledger toàn vòng đời cho mọi rider và nhiều chiều promise.

## 3. Patent và sản phẩm

### US11754407B2

[Method and system for shared transport](https://patents.google.com/patent/US11754407B2/en) mô tả cập nhật ETA khi thêm rider và cho phép constraint về thời gian/quãng đường tăng thêm.

### US11674811B2

[Assigning on-demand vehicles based on ETA of fixed-line vehicles](https://patents.google.com/patent/US11674811B2/en) có threshold, reassignment, notification, consent/incentive.

### Sản phẩm

Các mô tả công khai của ridepool thương mại cho thấy route có thể cập nhật khi thêm rider. Không giả định sản phẩm chưa công khai là chưa có kỹ thuật nội bộ.

Kết luận: ETA update, max delay, reassignment và consent riêng lẻ không phải novelty.

## 4. Paper nền thuật toán và benchmark

| Nguồn | Vai trò với RideBound | Không được suy diễn |
|---|---|---|
| [Alonso-Mora et al., PNAS 2017](https://doi.org/10.1073/pnas.1611675114) | Request-trip-vehicle graph và batch assignment | Không phải commitment ledger |
| [FleetPy, ETRR 2026](https://doi.org/10.1186/s12544-026-00823-3) | Simulator chung, benchmark Manhattan/Chicago/Munich | Không tự chứng minh thuật toán tốt |
| [RidePy, JOSS 2024](https://doi.org/10.21105/joss.06241) | Simulator độc lập, dispatcher/event analytics | Không có sẵn RideBound semantics |
| [Optimal Online Dispatch, ICRA 2021](https://www.cs.bham.ac.uk/~parkerdx/papers/icra21samod.pdf) | OSP/online dispatch baseline trong AMoD2 | Tối ưu schedule không đồng nghĩa ổn định lời hứa |
| Savelsbergh 1992, forward time slack | Định nghĩa gốc của slack mà `ForwardSlackProfile` hiện thực | Slack là early prune, không thay full validator |
| Cordeau & Laporte 2003, eight-step scheme | Mô tả đúng lớp chi phí RideBound đang trả khi dựng lại lịch cho từng candidate | Không phải chuẩn tối ưu; chỉ là baseline chi phí |
| [Gschwind & Drexl, Transportation Science 2019](https://doi.org/10.1287/trsc.2018.0837) | Constant-time insertion feasibility test, exact, báo `3,8×` so eight-step | **Chưa áp dụng.** Full text chưa đọc được ngày 2026-08-17; chỉ phủ chiều thời gian, không phủ capacity/connectivity/frozen prefix/commitment budget |

## 5. Framework đã xác minh ngày 2026-07-27

### FleetPy

- Repo chính thức: [TUM-VT/FleetPy](https://github.com/TUM-VT/FleetPy).
- Version khóa ban đầu: `1.0.2`, commit `053aa9d4fcfde91c5d303435d5748f9206c071b0`.
- Tag ngày 2026-03-24.
- MIT; có benchmark Manhattan, Chicago, Munich.
- Paper framework xuất bản 2026-07-15.

Kết luận: Layer 2 chính.

### RidePy

- Repo: [PhysicsOfMobility/ridepy](https://github.com/PhysicsOfMobility/ridepy).
- Version khóa ban đầu: `v2.10.1`, commit `bf1863e49a432f2f1f6230f86b2777a5ef5b9f14`.
- Event log, `FleetState`, `VehicleState`, dispatcher, analytics.

Kết luận: lựa chọn mặc định cho Layer 3 vì interface rõ và dễ tái lập.

### AMoD2

- Repo: [Leot6/AMoD2](https://github.com/Leot6/AMoD2).
- C++ với Manhattan data; có Greedy Insertion, SBA, OSP và reassignment.
- Có phụ thuộc build/solver nặng, thường dùng Gurobi.

Kết luận: adapter bổ sung có điều kiện; có giá trị vì native OSP cho phép reoptimization.

### AMoDeus

- Repo: [amodeus-science/amodeus](https://github.com/amodeus-science/amodeus).
- Java/MATSim, version repo nêu `2.1.1`, GPL-2.0.

Kết luận: stretch target; chi phí tích hợp cao.

### OpenRidepoolSimulator

- Repo: [MAS-Research/OpenRidepoolSimulator](https://github.com/MAS-Research/OpenRidepoolSimulator).
- C++/MIT, gần thiết kế PNAS 2017.
- Chỉ 4 commit tại thời điểm kiểm tra; README nêu MOSEK `8.1.0.56` và cảnh báo có thể không tương thích phiên bản mới.

Kết luận: đối chứng di sản, không phải dependency bắt buộc.

## 6. Claim ladder

Mỗi claim phải đạt từ dưới lên:

1. **Implemented:** code có và test pass.
2. **Mechanically valid:** certificate/validator xác nhận invariant.
3. **Internally effective:** tốt hơn baseline cùng core trong BeGo replay.
4. **Simulator robust:** cùng hướng trong FleetPy.
5. **Cross-system robust:** cùng hướng trong framework độc lập.
6. **Externally valid:** chỉ khi có dữ liệu/triển khai thật phù hợp.

Không nhảy từ tầng 2 lên tầng 6.

## 7. Checklist novelty trước mỗi bản thảo

- Claim có dùng từ “first”, “novel” hoặc “unprecedented” không?
- Đã đối chiếu Multiple-Plan DARP, TC-DARP, unreliability và anticipatory walking chưa?
- Metric có phụ thuộc lịch sử nhiều epoch hay chỉ so plan hiện tại?
- Có budget theo rider và theo dimension không?
- Có witness/certificate hay chỉ log?
- Có tách traffic-induced và decision-induced revision không?
- Có baseline least-commitment/penalty/lock horizon không?
- Paper mới sau ngày audit đã được tìm bổ sung chưa?

Nếu một câu trả lời là “không”, dừng claim và cập nhật evidence log.

## 8. Recheck trực tuyến khi đóng WP3 — 2026-08-02

Browser trong ứng dụng đã được dùng để đọc lại publisher/DOI của năm nguồn bổ
sung: Gaul–Klamroth–Stiglmayr 2021, Schulz–Pfeiffer 2026, Geržinič et al. 2023,
Tiwari–Nassir–Lavieri 2024 và Ackermann–Rieck 2025. Bảng chi tiết nằm trong
[21-paper-to-design-evidence.md](21-paper-to-design-evidence.md).

Audit này làm hẹp, không mở rộng, claim:

- rolling-horizon insertion, immediate response, forward slack, precomputation,
  multiple-plan và least-commitment đều đã có tiền lệ;
- weighted-sum, Pareto và lexicographic objective đều là kỹ thuật đã biết;
- quan sát preference/survey chỉ là động lực đo bất ổn, không phải căn cứ đặt
  budget số hoặc tuyên bố hài lòng người dùng;
- RideBound tiếp tục chỉ bảo vệ tổ hợp per-rider, path-dependent, 10 chiều,
  cumulative/switch ledger qua nhiều epoch và certificate máy kiểm tra được.

Vì vậy WP3 không được gọi là “solver tối ưu mới”. Nó là correctness mechanism.
Hiệu quả vận hành và các tối ưu plan-pool/slack/solver phải được kiểm chứng ở WP4+
trên cùng candidate/compute budget trước khi nâng claim ladder.

## 9. Recheck khi đóng WP6 — 2026-08-13

In-app Browser đối chiếu lại Alonso-Mora 2017, Simonetto 2019, Santi 2014,
Ackermann–Rieck 2025, Engelhardt et al. speed-up heuristic và FleetPy sources.
Kết quả không mở novelty claim:

- request-trip-vehicle graph, sparse assignment, shareability network, multiple-plan,
  direction/distance filtering và dynamic insertion đều có tiền lệ;
- paper speed-up thừa nhận quality trade-off, nên WP4 không được cắt candidate khi
  chưa có exact/differential loss evidence và full revalidation;
- multiple-plan/consensus không luôn tốt, nên B5 là comparator tách biệt;
- WP6 public derivative là mechanics/provenance evidence, không phải FleetPy
  closed-loop hoặc effectiveness result.

ADR-036 chỉ đóng mechanical correctness/reproducibility của WP1–WP6. Claim ladder
vẫn dừng ở tầng 2; tầng 3–5 phải chờ WP7–WP10 và preregistration.
