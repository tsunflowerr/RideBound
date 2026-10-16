# Nghiên cứu liên quan và ranh giới claim

## 1. Kết luận trước

Dynamic insertion, reassignment, ETA update, delay threshold, route similarity và least-commitment đều đã có tiền lệ. Phạm vi đóng góp được bảo vệ là tổ hợp được hiện thực và kiểm trong nghiên cứu:

> **per-passenger + path-dependent + multi-dimensional + cumulative/switch budget + multi-epoch rolling insertion + machine-checkable certificate**

Tổ hợp này không tự chứng minh quyền ưu tiên trên toàn bộ văn liệu. Không gọi từng thành phần hoặc việc thêm `delay <= 5 phút` là mới.

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

Paper chỉ ra việc assignment/schedule thay đổi có thể tạo bất ổn đáng kể và phân biệt thông tin báo ban đầu với kết quả thực. Phần 6 còn kiểm soát độ muộn trước quyết định bằng cơ chế siết mốc tham chiếu; không được mô tả nguồn này như chỉ đo bất ổn sau chuyến đi.

Không claim việc phát hiện unreliability, kiểm soát trước quyết định hoặc đánh đổi dịch vụ–ổn định là mới.

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
| [Gschwind & Drexl, Transportation Science 2019](https://doi.org/10.1287/trsc.2018.0837) | Constant-time insertion feasibility test, exact, báo `3,8×` so eight-step | Full PDF 39 trang đã đọc/hash ngày 2026-08-23. **Không nhập full constant-time test** vì model RideBound còn capacity/connectivity/frozen-prefix/commitment và travel contract không khóa triangle inequality. Chỉ áp exact same-state/route cache reuse; benchmark riêng không dùng `3,8×` làm claim |

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

## 10. Ranh giới bản khóa luận sau kiểm nguồn — 2026-10-05

Lượt biên tập khóa luận bên ngoài repository bổ sung các tiền lệ gần nhất, theo
[ADR-074 và hồ sơ kiểm](benchmarking/thesis-final-audit-2026-10-05.md):

- [Fielbaum và Alonso-Mora 2020](https://doi.org/10.1016/j.trc.2020.102831)
  có cả phép đo hậu nghiệm và kiểm soát trước quyết định; hai phần phải được phân biệt.
- [Elmi và cộng sự 2026](https://doi.org/10.1016/j.trc.2026.105897)
  có độ chùng thông thường và cơ chế bảo đảm cho khách được chọn. Bảo vệ từng khách
  hoặc bảo đảm ETA riêng lẻ không phải novelty của RideBound.
- [Schulz và Pfeiffer 2026](https://doi.org/10.1007/s00291-026-00847-0)
  dùng hạn chót trả từ thông báo đầu, với độ chùng 900 giây trong thiết lập đã đọc.
  So với các trần 30/60/120 giây phải báo khác biệt độ chặt; phép so trong RideBound
  là so hình thức cổng trên cùng hệ, không tái lập toàn thuật toán của paper.
- [Haferkamp 2024](https://doi.org/10.1016/j.ejtl.2024.100134)
  biến giờ đón đã chọn thành ràng buộc có độ lệch nhỏ cho phép. Không nhận việc giữ
  giờ đón đã thỏa thuận là mới.

Phần giữ tuyến là phép chiếu mô hình tại cùng trạng thái, không nhận diện đầy đủ
tác động nhân quả của giao thông. Tam giác nghiêm không tự chứng minh vi phạm giả
thiết bao hàm H. Kết quả âm H6 vẫn âm; T38 có chuyến nguồn chung và phụ thuộc theo
ngày nên chưa cấp bảo đảm quần thể hoặc độ phủ 90% cho ngày mới. Lượt biên tập này
không nâng trạng thái implementation, claim ladder hay quyền mở work package.

## 11. Nhu cầu doanh nghiệp và giá trị tăng thêm — 2026-10-05

[Kiểm nhu cầu ADR-075](research/ridebound-business-need-audit-2026-10-05.md)
đối chiếu văn liệu, sản phẩm và kết quả nội bộ. Timefold có phạt mềm thay lịch
đã thông báo; Google có khóa/nới thời gian, thứ tự và xe khi tái tối ưu; một số
chuyến đặt trước Lyft có cam kết bằng credit. Đây là bằng chứng cho nhu cầu
giảm gián đoạn và giữ kết quả phục vụ, không xác nhận nhu cầu mua đúng ledger C.

Không nhận giảm C là tăng hài lòng, giảm trễ thực, tăng doanh thu hoặc giảm công
lao động. C có thể bằng 0 khi khách đến muộn; nó cũng có thể tính phí một sửa
chữa khôi phục ETA đã báo. H6 mất 7,13 điểm phần trăm phục vụ; T38 chưa cho thấy
ưu thế trên E2 chung và so vi phạm lời hứa riêng không phải so cùng một SLA.

Giá trị hiện được bảo vệ là đặc tả/hiện thực/kiểm nghĩa vụ và kết quả đánh đổi có
giới hạn. Giá trị thực tiễn tăng thêm **chưa xác nhận**. Muốn nâng claim cần một
người vận hành có vấn đề, chi phí và ngưỡng chấp nhận cụ thể; so với hạn chót,
khóa và phạt mềm trên kết quả thực hoặc công việc audit thực. Không tìm thấy
người mua trong lượt tìm có mục tiêu không chứng minh không ai cần ở mọi nơi.

## 12. Ràng buộc dịch vụ sau survey toàn văn — 2026-10-06

[Survey ADR-076](research/ridepooling-service-constraints-survey-2026-10-06.md)
đối chiếu 9 bài/210 trang PDF, cùng nguồn bổ trợ và code/result hiện có. Pickup
window, onboard ride cap, total delay, deadline từ offer đầu và ledger C là các
nghĩa vụ khác nhau. Không trích “10 phút” hoặc “1,5 lần” khi bỏ gốc thời gian,
intercept/cap hoặc việc có tính waiting.

MOIA/Zwick 2020 có sweep thời gian chờ; 10 phút tốt hơn 15 phút trong cấu hình
greedy đã đọc, không phải chuẩn ngành hoặc mức tối ưu của RideBound. Bài MOIA
2022 giữ wait 10 phút, nhưng công thức detour có intercept/cap; PDF chưa xác
nhận waiting inclusion của chính công thức đó. Mahfouz 2026 tính cả waiting
trong 1,5 lần direct + 5 phút. Pure onboard 1,5 lần hiện tại chưa được hiệu
chỉnh theo sở thích khách. Ngân sách C 30 giây chưa có căn cứ WTP/ROI bên ngoài.

Cohen/Via 2022 có thí nghiệm bồi hoàn và đầu ra usage/spending thực; ETA error
là **lỗi giờ đón**, không phải drop ETA. Can thiệp credit không chứng minh giảm
drop lateness một phút hoặc C thấp làm tăng giữ khách/lợi nhuận. Nhu cầu reliability
có chứng cứ, giá trị tăng thêm của ledger vẫn chưa xác nhận.

Forecast replay mới 2026-10-06 gần endpoint hiển thị hơn, nhưng 0,17% là mean
21 cell active; all34 là 0,51%, các baseline khác cũng được lợi. Không nâng
thành bảo đảm quần thể, human peer review hoặc chứng nhận trách nhiệm pháp lý.
Survey không thay contract, cứu H6, nghiệm thu extension bên ngoài hoặc cấp
quyền chạy matrix/triển khai mới.
