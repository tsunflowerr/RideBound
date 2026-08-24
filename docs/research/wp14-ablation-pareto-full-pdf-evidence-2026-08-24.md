# WP14 exploratory ablation — full-PDF evidence

> Trạng thái: `EVIDENCE_READ_COMPLETE`
> Ngày: 2026-08-24
> Phạm vi: thiết kế `RB-WP14-001` refinement; không sửa H6, preregistration hoặc
> verdict WP9

## 1. Câu hỏi thiết kế

WP13 đã đóng với kết luận: generated candidate sets bằng nhau ở 40/40 pair, nên
service loss của C1 không nằm ở generation mà ở commitment gate. Tài liệu này trả
lời đúng một câu hỏi tiếp theo: **literature định nghĩa và nới ràng buộc
commitment/consistency như thế nào, và cấu trúc thí nghiệm nào là chuẩn để báo cáo
đánh đổi service–commitment?**

Không tài liệu nào dưới đây được dùng để đổi margin, panel, denominator,
failed-job treatment, hoặc để chọn giá trị tham số cho bất kỳ run nào. Mọi giá trị
số của paper được ghi lại như context, không phải default.

## 2. Full PDF đã đọc

Bản cục bộ nằm ngoài repository tại
`E:\RideBoundData\research\pdf-20260824-wp14`, kèm `fulltext-inventory.json`.

| Tài liệu | Trang | SHA-256 | Nguồn |
|---|---:|---|---|
| Alonso-Mora, Samaranayake, Wallar, Frazzoli, Rus, *On-demand high-capacity ride-sharing via dynamic trip-vehicle assignment*, PNAS 114(3) | 6/6 | `edbb62156e36479b742a1a7381e5920673a4b6a3130bba39aed74cb8364c12ea` | `https://people.csail.mit.edu/jalonsom/docs/17-alonsomora-ridesharing-pnas.pdf` |
| ibid., *Supplemental methods* | 20/20 | `c12ee2aad4c9cd7b0f7d9e8cac8ea76148d2059d87a1b52e9e92864d2de23495` | `https://autonomousrobots.nl/assets/files/publications/17-alonsomora-ridesharing-pnas-supplemental-method.pdf` |
| Lespay et al., *A case study of Consistent Vehicle Routing Problem with Time Windows* | 23/23 | `f8285ce3b4450eced280437b08883269c321e7e554bde038e84ff55f7c294ed7` | `https://arxiv.org/pdf/1912.05929` |
| *Near-on-Demand Mobility: The Benefits of User Flexibility for Ride-Pooling Services* | 15/15 | `84f068ed6da7982bd0b578b4bc5a65f80cb168a908b605a8904fcb2086840dbb` | `https://arxiv.org/pdf/2011.00823` |
| *Heuristics for Customer-focused Ride-pooling Assignment* | 13/13 | `64e14581e2336d8c6eb22f9d08bd37b68b7d26b86f84ee4017e75beb7cab588d` | `https://arxiv.org/pdf/2107.11318` |

Tổng 77/77 trang có text, trích bằng `poppler pdftotext -layout`. Hai nguồn bị
chặn HTTP 403 (`pnas.org/doi/pdf`, `pubsonline.informs.org` cho GenConVRP gốc)
được ghi lại như receipt và **không** tính là full-text evidence; nội dung
GenConVRP dưới đây chỉ được dùng qua phần khảo sát của Lespay et al.

## 3. Kết luận được áp dụng

### 3.1 Literature ràng buộc *kết quả*, RideBound ràng buộc *độ chỉnh sửa lời hứa*

Alonso-Mora et al. định nghĩa tập ràng buộc `Z` gồm đúng ba mục:

- `t^p_r ≤ t^{pl}_r ≤ t^r_r + Ω` — maximum waiting time;
- `t^d_r ≤ t^*_r + Δ` — maximum travel delay, với `t^*_r` là thời điểm sớm nhất có
  thể đến đích nếu đi shortest path ngay lúc request;
- capacity `n^v_pass ≤ ν`.

Đây là **service-level guarantee trên outcome tuyệt đối**, neo vào chuyến đi lý
tưởng. Nó hoàn toàn không giới hạn việc lời hứa bị sửa bao nhiêu lần.

RideBound `drop_eta_total_ms` là đối tượng khác hẳn: tổng biến phân
`Σ|Δ|` của lời hứa đã publish trong suốt vòng đời request. Hai object này không
thay thế nhau và không được trình bày như cùng một loại ràng buộc.

**Áp dụng:** WP14 phải báo cáo song song hai họ ràng buộc, và tài liệu claim phải
nói rõ RideBound đo *promise revision*, không phải *outcome level of service*.

### 3.2 Cơ chế commitment của literature là ratchet một chiều, không phải freeze

Alonso-Mora et al., Results:

> “If a request is matched to a vehicle at any given iteration, its latest pickup
> time is reduced to the expected pickup time by that vehicle and the cost `c_ko`
> of ignoring it is increased for subsequent iterations. A request might be
> rematched to a different vehicle in subsequent iterations as long as its waiting
> time does not increase and until it is picked up by some vehicle. Once a request
> is picked up, it remains in that vehicle and cannot be rematched.”

Nghĩa là: (i) pickup chỉ được phép **tốt lên**; (ii) đổi xe trước pickup **được
phép**; (iii) “dính” với xe cũ là một **penalty tăng dần** trong objective, không
phải hard lock; (iv) chỉ onboard mới bất biến.

C1 hiện tại nghiêm ngặt hơn cả ba điểm: `finalConfirmationLocks` cấm **mọi** thay
đổi pickup ETA kể cả cải thiện, trong **toàn bộ** phase `waitingPickup`; O-001 cấm
đổi xe ở cả hai arm; và không có thành phần penalty nào.

**Áp dụng:** WP14 phải đưa “ratchet một chiều” và “freeze theo horizon” vào factor
matrix như hai lựa chọn tách bạch với “freeze toàn phase”.

### 3.3 Baseline của RideBound yếu hơn baseline chuẩn của literature

Vì O-001 khóa `accepted assignment` cho **cả hai** arm, B1 không được rematch
trước pickup, trong khi baseline Alonso-Mora được. Ràng buộc này đối xứng nên
không làm sai phép so sánh, nhưng nó làm baseline yếu đi. Do đó `−7,1296 pp` và
`−4,9074 pp` của H6 là **cận dưới** của mất mát so với một baseline reassignment
đầy đủ, chứ không phải cận trên.

**Áp dụng:** ghi rõ điều này trong limitation; WP14 không được sửa O-001 để “làm
đẹp” kết quả, và cũng không được nói C1 gần baseline chuẩn hơn thực tế.

### 3.4 Khi hard consistency quá chặt, literature chuyển sang penalty và ε-constraint

Lespay et al. tổng kết ConVRP: “Both time and person consistencies are treated as
hard constraints: the maximum arrival time difference of each customer is bounded”.
Và tiếp theo:

> “it was pointed out that the consistency requirements of ConVRP may be too
> restrictive. Thus, a relaxed ConVRP, called Generalized ConVRP (GenConVRP), was
> introduced. … **The time consistency is not enforced by constraints but is
> penalized in the objective function.**”

> “a multi-objective optimization approach was proposed. … Routing cost, time
> consistency, and person consistency are considered as **independent objectives**
> … Two exact approaches based on the **ε-constraint** framework were proposed.”

Đây là tiền lệ trực tiếp và gần nhất với bài toán RideBound: chính xác cùng một
chẩn đoán (hard consistency quá chặt) và cùng một hướng xử lý (penalty + đa mục
tiêu + ε-constraint để dựng frontier).

Một kết quả phụ đáng chú ý: nới thời điểm khởi hành để hấp thụ lệch giờ “leads to
improved time consistency, while the travel times remain almost unchanged”. Tương
đương trong ride-pooling là **giữ xe chờ để bảo toàn lời hứa** thay vì sửa lời hứa.

**Áp dụng:** ba factor được literature bảo chứng — penalty thay hard gate,
ε-constraint để dựng frontier, và compensating hold. Không copy trọng số penalty
của paper.

### 3.5 Thiết kế thí nghiệm: full factorial + báo cáo đa outcome

*Near-on-Demand Mobility* chạy 2.880 scenario từ tổ hợp fleet size × capacity ×
advance horizon × willingness-to-share × LOS strictness × traffic, decision epoch
30 s, rồi báo cáo bốn dependent variable (VMR, shared-trip fraction, wait time,
delay time). LOS strictness được đặt thành ba mức `(5,10)`, `(7,15)`, `(10,20)`
phút và tác động của việc nới có **diminishing returns**.

*Heuristics for Customer-focused Ride-pooling Assignment* báo cáo “a trade-off
among heuristics between throughput and customer matching time” — nghĩa là hai
trục phải đứng cạnh nhau, không gộp thành một scalar.

**Áp dụng:** WP14 dùng factorial trên development cells mới, báo frontier hai trục
service–burden, và không xếp hạng cấu hình bằng một scalar hậu outcome. Không copy
mức LOS hay số scenario của paper.

## 4. Phần bị từ chối hoặc hoãn

- Không lấy default số học nào từ paper: `Ω = 2 min`, `Δ = 2Ω`, batch `30 s`,
  LOS `(5,10)/(7,15)/(10,20)` chỉ là context, không phải giá trị cho WP14.
- Không đưa rebalancing/repositioning vào WP14. Alonso-Mora báo rebalancing tăng
  service rate khoảng 20%, nhưng thêm nó sẽ thay đổi cả hai arm và phá so sánh với
  H6; nó thuộc một work package riêng nếu được mở.
- Không đưa demand forecasting, scenario sampling, ADP hay RL vào WP14.
- Không dùng GenConVRP gốc (403) như primary source; chỉ dùng phần khảo sát đã đọc
  full-text của Lespay et al.
- Không dùng bất kỳ số đo nào của H6/E1 panel để **chọn** mức factor cho WP14; xem
  ràng buộc leakage trong `tasks/43`.

## 5. Hệ quả cho delivery

Literature xác nhận hướng đi nhưng không cấp tham số. Do đó `RB-WP14-001` chỉ
được phép: khóa định nghĩa factor, khóa gate, khóa cách báo frontier, và lập
ordered queue. Mọi mức số phải được derive từ một development panel mới và freeze
trước outcome.
