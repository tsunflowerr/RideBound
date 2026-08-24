# WP14 — Exploratory ablation và Pareto frontier: refinement

> Ticket: `RB-WP14-001`
> Trạng thái: `DONE`
> Authorization: ADR-065 verdict `openExploratoryAblationOnly`; ADR-066 khoá thiết kế
> Không mở: H7, lifecycle policy v2, H6 backfill, causal/population inference

## 1. Ticket này quyết định gì và không quyết định gì

Quyết định: định nghĩa factor, định nghĩa gate, cách báo frontier, ràng buộc
leakage, và ordered queue.

Không quyết định: giá trị số của bất kỳ factor nào, cấu hình nào thắng, có chạy
matrix hay không. Mở refinement **không phải** là quyết định chạy matrix.

## 2. Ba ràng buộc cứng

### C1 — Không leakage từ panel đã đóng

Review WP1–WP13 đo được phân phối tiêu thụ commitment trên H6/E1 panel, và phân
phối đó rất giàu thông tin. Dùng nó để **giải thích** kết quả đã có là hợp lệ và
đã làm. Dùng nó để **chọn** mức factor cho WP14 thì không: đó đúng là kiểu leak
mà ADR-065 cấm.

Vì vậy: mọi mức số của factor phải được derive từ một development panel mới; H6
Panel A/B không tham gia tuning hay selection dưới bất kỳ hình thức nào. Analyzer
của WP14 phải fail closed nếu input inventory chạm vào H6 root.

Hệ quả có thể khó chịu và được chấp nhận: WP14 có thể chọn một grid mà, nhìn lại,
là không tối ưu cho H6 panel. Đó là cái giá đúng phải trả.

### C2 — Frontier, không phải scalar

Báo cáo là tập điểm `(service, burden)` với nhãn cấu hình. Không có một scalar nào
xếp hạng cấu hình hậu outcome. Nếu cần chọn theo ε-constraint thì `ε` phải được
freeze trước outcome.

### C3 — Không rescue H6

Không cấu hình nào của WP14 được dùng để diễn giải lại, sửa, hay “giải thích đi”
kết quả H6. H6 negative result giữ nguyên authority.

## 3. Factor đã được loại — và lý do là phép đo, không phải lập luận

Bốn factor nghe hợp lý bị loại vì đo ra là vô hiệu. Ghi lại để không ai đề xuất
lại:

| Factor bị loại | Lý do đo được |
|---|---|
| Chỉ tính phần ETA xấu đi (one-sided charging) | 403/403 request có tiêu thụ ở Panel A B1 đều là suy giảm ròng; 0 cải thiện. Không có gì để bỏ tính |
| Neo vào net displacement thay vì total variation | `Σ|net|` bằng 99,9% tổng biến phân; trung bình 1,20 lần revision/request, tối đa 3 |
| Nới `vehicle_switch_count` hoặc `*_stop_switch_count` | 0 witness trong 44.156 decision; O-001 đã cấm ở tầng domain cho cả hai arm nên các limit này không bao giờ ràng buộc |
| Nới 7 dimension commitment còn lại | 0 witness trong 44.156 decision |

## 4. Factor được nhận

Mỗi factor đi kèm: cơ chế trong code, bảo chứng literature, và lý do đo được.

### F1 — Phạm vi của pickup-ETA lock: cả phase, hay chỉ trong freeze horizon

- **Cơ chế:** thay `finalConfirmationLocks ⊇ {pickupEta}` bằng
  `freezeHorizonMs` + `freezeHorizonLocks`. Cả hai đã có trong
  `CommitmentPolicy`, có validation và có test; **chưa configuration nào dùng**.
- **Literature:** Alonso-Mora et al. không freeze pickup — họ dùng ratchet một
  chiều (`latest pickup time is reduced to the expected pickup time`) và cho
  rematch trước pickup. Freeze theo horizon là dạng chặt hơn nhưng vẫn có giới hạn
  thời gian, khác hẳn freeze cả phase.
- **Đo được:** lock này gây 160/940 prune Panel A và 92/583 Panel B, và 25/143
  cùng 41/212 request bị chặn. Đồng thời phần pickup của burden là **định nghĩa**
  — nó bằng 0 theo cấu tạo. Đây là factor có tỷ lệ lợi/hại tốt nhất.
- **Mức:** derive từ development panel. Ít nhất phải có mức “cả phase” (hiện
  hành) làm tham chiếu.

### F2 — Ratchet một chiều cho pickup

- **Cơ chế:** cho phép candidate làm pickup ETA **sớm hơn**, chặn muộn hơn. Hiện
  `CommitmentLockEvaluator` so `!=` nên chặn cả hai chiều.
- **Literature:** đúng cơ chế của Alonso-Mora et al.
- **Đo được:** chưa quan sát trực tiếp được vì lock hiện chặn trước. Cần đo trong
  development panel; nếu cải thiện pickup không xảy ra thì F2 vô hiệu và phải bị
  loại giống bốn factor ở mục 3.
- **Cảnh báo thiết kế:** rider lên kế hoạch quanh giờ đã hứa, nên “sớm hơn” không
  hiển nhiên là tốt. F2 phải được báo cùng số lần rider bị đón sớm hơn lời hứa,
  không được trình bày như cải thiện thuần.

### F3 — Hard gate với penalty band

- **Cơ chế:** thay vì gate nhị phân, cho phép một dải: vượt hạn mức trong dải thì
  bị phạt trong objective; vượt dải thì vẫn hard reject. Hạ tầng đã có một phần —
  `SoftHardHybridPolicy` và `CommitmentWarningProfile` cài đặt warning-excess
  vector — nhưng nó xếp ordered vector chứ không phải penalty thực sự, và vẫn
  hard-filter theo hard limit.
- **Literature:** tiền lệ trực tiếp nhất. Lespay et al. tổng kết: hard consistency
  của ConVRP “may be too restrictive”, nên GenConVRP “time consistency is not
  enforced by constraints but is penalized in the objective function”, và bản đa
  mục tiêu dùng ε-constraint để dựng frontier.
- **Đo được:** phân phối lưỡng cực ở mục 2 của review cho thấy budget vô hướng là
  knob tệ. Penalty band là cách duy nhất trong danh sách này có thể tạo ra điểm
  frontier ở giữa.

### F4 — Vị trí của `operational-cost` trong hierarchy

- **Cơ chế:** hiện `operational-cost` nằm ở level 13, dưới 11 level revision.
- **Đo được:** 8/11 level đó là hằng số và toàn bộ hierarchy chỉ đổi lựa chọn cục
  bộ ở 0,738% vehicle set. Nghĩa là factor này **được dự đoán là gần vô hiệu**.
- **Vì sao vẫn giữ:** vì dự đoán đó đến từ H6 panel, và dưới gate lỏng hơn (F1/F3)
  tập option sẽ lớn hơn, nên hierarchy có thể trở nên hoạt động. Giữ F4 làm
  falsification: nếu nó vẫn vô hiệu trên development panel thì treatment phải được
  mô tả lại là “gate”, không phải “gate + ranking”.

### F5 — Hold để bảo toàn lời hứa

- **Cơ chế:** cho xe chờ để hấp thụ trôi ETA thay vì publish revision.
  `OriginHoldCandidateTransformer` và `relocatedWaitMs` đã tồn tại.
- **Literature:** Lespay et al. dẫn biến thể ConVRP nới thời điểm khởi hành:
  “leads to improved time consistency, while the travel times remain almost
  unchanged”.
- **Rủi ro:** hold làm giảm throughput. Phải báo cùng utilization.

### F6 — Mục tiêu phân phối thay vì hạn mức đồng nhất

- **Cơ chế:** ràng buộc theo phân vị của fleet (ví dụ “≤ X% rider có bất kỳ
  revision nào”) thay vì hạn mức cứng cho từng rider.
- **Đo được:** thiệt hại tập trung: 23,2% rider chịu toàn bộ, còn lại chịu 0. Một
  hạn mức đồng nhất là công cụ sai hình dạng cho một phân phối lưỡng cực.
- **Cảnh báo công bằng:** mục tiêu phân phối có thể tối ưu bằng cách dồn toàn bộ
  thiệt hại lên một nhóm nhỏ. Bắt buộc báo kèm phân vị đuôi per-rider, không chỉ
  tổng.

## 5. Gate của WP14

| Gate | Điều kiện |
|---|---|
| Leakage | Analyzer fail closed nếu inventory chạm H6 root; factor level phải trỏ tới development panel manifest |
| Pre-outcome freeze | Factor matrix, denominator, analyzer source hash, resource envelope được freeze và hash trước run đầu tiên |
| Resource | Ước lượng byte/decision và tổng envelope trước matrix; có cancellation receipt; dry-run schema + tiny trước |
| Reporting | Frontier hai trục; cấm scalar ranking hậu outcome; báo cả đuôi per-rider |
| Falsification | Mỗi factor phải có điều kiện “vô hiệu” rõ ràng và phải được báo khi xảy ra, giống bốn factor ở mục 3 |
| Claim | Không causal, không CI/p-value population, không rescue H6, không chọn policy v2 |

## 6. Điều kiện fail closed

Nếu một ràng buộc không freeze được, hoặc resource envelope không đạt, hoặc
development panel không dựng được mà không chạm H6, thì WP14 execution dừng và ghi
lý do. Không có đường vòng qua H6 panel.

## 7. Ordered queue

Xem [`44-wp14-exploratory-ablation-ticket-plan.md`](44-wp14-exploratory-ablation-ticket-plan.md).
Ticket implementation đầu tiên là `RB-WP14-002` — tối ưu solver trung tính giữa hai
arm — vì nó là điều kiện tài nguyên cho toàn bộ matrix.
