# Hiến chương nghiên cứu RideBound

## 1. Bối cảnh và động lực

Một thuật toán ridepooling trực tuyến thường nhận yêu cầu mới, thử chèn điểm đón/trả vào route các xe và chọn phương án giảm chi phí hoặc tăng số người được phục vụ. Cách làm này có thể đạt hiệu suất vận hành tốt nhưng tạo ra một vấn đề khác: sau khi ứng dụng đã nhận khách, mỗi lần tái tối ưu có thể lại đổi ETA, đổi xe, đổi điểm đón hoặc đổi thứ tự phục vụ.

Hai phương án có thể có cùng route cuối cùng nhưng trải nghiệm vận hành rất khác:

- Phương án A chỉ sửa ETA một lần.
- Phương án B sửa ETA năm lần, đổi xe hai lần rồi cuối cùng quay về đúng ETA của A.

Nếu chỉ nhìn trạng thái cuối, A và B giống nhau. Nếu nhìn toàn bộ lịch sử lời hứa, B bất ổn hơn rõ ràng. Đây là tính **phụ thuộc đường đi của quyết định**: chất lượng hiện tại phụ thuộc cả chuỗi cập nhật trước đó, không chỉ plan cuối.

RideBound biến lịch sử đó thành trạng thái được tối ưu và kiểm tra.

## 2. Bài toán nằm ở ngách nào?

Đây là giao điểm của:

- dynamic dial-a-ride/online ridepooling;
- rolling-horizon reoptimization;
- service reliability và schedule stability;
- constrained online optimization;
- auditability của hệ thống quyết định tuần tự.

Nó không phải ngách “tìm route ngắn nhất”, “ghép xe động” hay “ETA chính xác” nói chung. Điểm hẹp cần bảo vệ là:

> ngân sách thay đổi lời hứa **theo từng hành khách, nhiều chiều, tích lũy qua nhiều epoch**, kèm hard lock, ledger và certificate kiểm tra được.

## 3. Câu hỏi nghiên cứu

### RQ1 — Khả thi

Có thể áp dụng ngân sách revision cứng trong rolling insertion mà vẫn duy trì tỷ lệ phục vụ gần baseline hay không?

### RQ2 — Trade-off

Đổi lại mức giảm revision burden, hệ thống phải trả bao nhiêu về accepted/served rate, VHT/VMT, wait, detour và runtime?

### RQ3 — Đuôi phân phối

RideBound có giảm được nhóm hành khách chịu thay đổi nhiều nhất, thay vì chỉ giảm trung bình toàn hệ thống hay không?

### RQ4 — Tính tổng quát

Kết quả có giữ hướng trong BeGo replay, FleetPy và một simulator độc lập khác hay chỉ là đặc tính của một codebase?

### RQ5 — Cơ chế

Thành phần nào tạo ra cải thiện: cumulative budget, switch budget, hard lock, lexicographic objective hay cách tách revision do traffic và do quyết định?

## 4. Giả thuyết

- **H1:** RideBound giảm `p95` của tổng biến thiên ETA đón do quyết định so với rolling baseline.
- **H2:** RideBound giảm tỷ lệ hành khách có ít nhất ba lần sửa lời hứa đáng kể.
- **H3:** Mức giảm trên đạt được với service-rate loss nằm trong biên non-inferiority được đăng ký trước.
- **H4:** Cải thiện có cùng dấu ở BeGo và FleetPy; cross-system layer cho kết quả cùng hướng hoặc giải thích được bằng khác biệt capability/event semantics.
- **H5:** Khi ngân sách được đặt vô hạn và hard lock bị tắt, RideBound suy biến về baseline tương ứng.

Các con số như “giảm 30%” hay “service loss không quá 1 điểm phần trăm” hiện là **mục tiêu thiết kế minh họa**, chưa phải kết quả hay ngưỡng chính thức. Ngưỡng chính thức chỉ được khóa sau pilot và trước full run.

## 5. Đóng góp dự kiến

### C1 — Mô hình

Định nghĩa lời hứa nhiều chiều:

`ETA đón/trả + xe + điểm đón/trả + quan hệ thứ tự`

và ledger tổng biến thiên/số lần switch qua toàn vòng đời request.

### C2 — Thuật toán

Rolling insertion có:

- hard feasibility gate;
- vector commitment budget;
- freeze prefix;
- lexicographic hoặc epsilon-constrained optimization;
- fallback sự cố có audit trail.

### C3 — Certificate

Mỗi decision trả:

- budget trước và sau;
- revision delta;
- invariant đã kiểm tra;
- witness nếu request/candidate bị loại hoặc xảy ra breach.

### C4 — Portable artifact

Cùng một core/runner chạy trong:

- BeGo;
- FleetPy;
- ít nhất một framework độc lập.

### C5 — Protocol đánh giá

Thiết kế paired replay, metric revision do quyết định, customer-visible revision, service non-inferiority và artifact tái lập.

## 6. Claim được phép

Nếu thí nghiệm ủng hộ:

- “Giảm bất ổn lời hứa do quyết định trong các benchmark đã công bố.”
- “Giữ ngân sách revision cứng trong các epoch bình thường theo validator/certificate.”
- “Trade-off service/cost/runtime nằm trong khoảng đo được.”
- “Core có tính portable qua các simulator đã chạy.”

## 7. Claim không được phép

- “Lần đầu tiên có dynamic insertion.”
- “Lần đầu tiên bảo vệ ETA hoặc giới hạn delay.”
- “Lần đầu tiên có least-commitment.”
- “Tăng sự hài lòng của người dùng” nếu không có nghiên cứu người dùng thật.
- “Công bằng cho nhóm yếu thế” khi không có protected attributes và thiết kế fairness phù hợp.
- “Tối ưu toàn cục” nếu candidate generation hoặc solver không có bound tương ứng.
- “Tổng quát cho mọi thành phố/hệ thống” chỉ từ Manhattan hoặc một simulator.

## 8. Quan hệ với fairness

RideBound có thể đo phân phối revision burden:

- maximum;
- `p95`, `p99`;
- Gini;
- tỷ lệ người chịu nhiều revision.

Đây là **công bằng phân phối gánh nặng vận hành**, chưa phải demographic fairness. Không gọi nó là fairness theo giới, tuổi, khuyết tật hoặc thu nhập nếu không có nhãn thật, consent và phân tích riêng.

## 9. Ngân sách cá nhân có bị dữ liệu giả làm bias không?

Trong benchmark chính, budget là **policy của dịch vụ**, không phải dự đoán sở thích thật của người dùng.

Ba lớp phải báo riêng:

1. Uniform budget: mọi người cùng policy; dùng để kiểm tra cơ chế.
2. Heterogeneous synthetic budget: stress test; chỉ chứng minh thuật toán chịu được không đồng nhất.
3. User-provided/observed budget: chỉ dùng cho claim cá nhân hóa khi có dữ liệu thật và quy trình consent.

Không được dùng budget ngẫu nhiên rồi kết luận “người dùng thích hệ thống hơn”.

## 10. Điều kiện dừng hoặc đổi đề tài

Cần dừng/thu hẹp nếu một trong các điều sau xảy ra:

- Tìm thấy prior work đã có đúng ledger nhiều chiều, cumulative budget theo từng rider và certificate toàn vòng đời trong rolling insertion.
- Core không thể tái sử dụng ngoài BeGo mà phải viết lại thuật toán.
- Sau pilot hợp lệ, mọi cấu hình budget hữu ích đều làm service rate giảm quá lớn và không có Pareto region thực dụng.
- Không thể phân biệt revision do traffic với revision do quyết định.
- Benchmark không giữ cùng demand, travel time, candidate pool và compute budget giữa baseline/RideBound.

Việc dừng phải được ghi trong [18-status-and-decision-log.md](18-status-and-decision-log.md), không được âm thầm đổi metric sau khi thấy kết quả.
