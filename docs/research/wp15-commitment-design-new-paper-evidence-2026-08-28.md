# WP15 — full-text evidence từ paper mới cho commitment design

> Ngày kiểm tra: 2026-08-28 (Asia/Bangkok)
> Phạm vi: thiết kế commitment cho WP15; **không** authorize execution, không đọc
> scientific outcome của WP14R, không diễn giải lại H6/E1/WP14-v1
> Yêu cầu nguồn: paper **mới tải từ web**, không dùng lại corpus local đã có

## 1. Vì sao cần corpus mới

Toàn bộ corpus trước đây (Alonso-Mora 2017, Kalibera–Jones 2013, Mytkowicz 2009,
Curtsinger–Berger 2013, Gschwind, Schulz–Pfeiffer, Lespay ConVRP review, và các
mục ở [21-paper-to-design-evidence.md](../21-paper-to-design-evidence.md)) đã được
dùng để dựng WP4–WP14R. Chúng không trả lời câu hỏi của WP15:

> Nếu ràng buộc *số lần và mức độ sửa lời hứa* làm mất 7,13 pp dịch vụ, thì có cách
> nào đặt lời hứa ngay từ đầu để không phải sửa nó?

Đó là câu hỏi về **thiết kế lời hứa**, không phải về ràng buộc sửa lời hứa. Không
paper nào trong corpus cũ nhắm vào nó.

## 2. Corpus mới — provenance đầy đủ

Tải trực tiếp từ arXiv ngày 2026-08-28, lưu **ngoài** repository:

```text
E:\RideBoundData\research\pdf-20260828-wp15-commitment-design
inventory: fulltext-inventory.json
inventory SHA-256: 70e437b260fa88b930099eb5f3af0f5823643c545f9d0ad89f3450bce6edfb70
```

| arXiv | Paper | Trang | Byte | SHA-256 |
|---|---|---:|---:|---|
| [2306.13356](https://arxiv.org/abs/2306.13356) | Lotze, Marszal, Schröder, Timme — *Taming Travel Time Fluctuations through Adaptive Stop Pooling* | 25/25 | 5.048.189 | `f6b8262e…94ede` |
| [2508.01032](https://arxiv.org/abs/2508.01032) | Hosseini, Rostami, Araghi — *Service Time Window Design in Last-Mile Delivery* | 47/47 | 2.190.090 | `9ffaa231…b27825` |
| [2602.04599](https://arxiv.org/abs/2602.04599) | Milosevic et al. — *Stochastic Decision Horizons for Constrained Reinforcement Learning* | 58/58 | 12.697.144 | `d022e657…aace3a` |
| [2605.11798](https://arxiv.org/abs/2605.11798) | Laupichler, Andre, Kandler, Sanders, Vortisch — *Advancing Dynamic Ride-Pooling Simulation — A Highly Scalable Dispatcher* | 35/35 | 3.013.341 | `a8903413…4bd6b911` |

Tổng **165/165 trang** extract text không rỗng, không file nào mã hoá, mỗi trang được
ghi ra một file `.txt` riêng bằng `pypdf` 3.1.0.

### Ranh giới trung thực về độ sâu đã đọc

Đây là điều phải ghi rõ để không lặp lại lỗi "title-only evidence":

- **Extract + kiểm tra cơ học: 165/165 trang.** Mọi trang đều có text layer.
- **Đọc sâu (nguyên văn, từng dòng):** `2508.01032` các trang 1, 5, 6, 8, 9, 10, 11,
  12, 30 (setup, literature gap, contributions, hai criterion, hàm chi phí, Proposition 1,
  điều kiện stationarity, conclusion); `2605.11798` trang 5 (dispatcher và toàn bộ bốn
  ràng buộc feasibility).
- **Đọc có mục tiêu (grep + đoạn liên quan):** `2602.04599` cơ chế continuation
  probability; `2306.13356` định nghĩa fluctuation và kết quả.
- **Chưa đọc sâu:** phần chứng minh phụ lục của `2508.01032`, toàn bộ phần thuật toán
  shortest-path/CH của `2605.11798`, phần lý thuyết CaI/entropy của `2602.04599`.

Không mục nào dưới đây dựa trên abstract. Abstract chỉ dùng để **chọn** paper.

## 3. `2508.01032` — thiết kế cửa sổ có mức bảo đảm

Đây là paper quan trọng nhất cho WP15.

### Cơ chế

Cho một route đã định trước, thời điểm tới khách `k` là biến ngẫu nhiên
`τ_k = y_k^T t̃`. Tác giả đặt hai criterion:

```text
C1: ℓ_k ≤ τ_k(y_k, t̃) ≤ u_k
C2: (u_k − ℓ_k) nhỏ nhất
```

C1 một mình có nghiệm tầm thường `[0, +∞]`; C2 chặn nó lại. Vi phạm được đo bằng hai
đại lượng earliness/tardiness

```text
h_ℓ = max(ℓ_k − τ_k, 0)      h_u = max(τ_k − u_k, 0)
```

rồi gộp thành chi phí `H_k = a_w(u_k − ℓ_k) + a_ℓ H_ℓ + a_u H_u`, trong đó `a_w` là giá
của độ rộng cửa sổ và `a_ℓ`, `a_u` là giá của vi phạm sớm/muộn.

### Kết quả có thể chuyển giao

`H_k` lồi và khả vi; điều kiện stationarity cho nghiệm **dạng đóng theo quantile**:

```text
Pr(τ_k ≤ ℓ̄_k) = a_w / a_ℓ
Pr(τ_k ≤ ū_k) = 1 − a_w / a_u
```

Đây là cấu trúc critical-fractile kiểu newsvendor. Hệ quả trực tiếp: **mức bảo đảm
dịch vụ chính là tỷ số penalty**, không phải một con số chọn tuỳ ý. Nếu
`a_w ≪ min(a_ℓ, a_u)` thì cửa sổ tối ưu bung ra `[0, +∞]` — nghĩa là "muốn không bao
giờ vi phạm" tương đương "không hứa gì cả".

Hai extension đáng chú ý: cửa sổ **rộng cố định** `w` cho lý do hợp đồng/vận hành, và
chính sách **cho xe đến sớm chờ** thay vì chịu penalty earliness.

Bản DRO thay phân phối đã biết bằng ambiguity set chỉ có mean và covariance, cho cùng
dạng đóng theo worst-case. Trong out-of-sample test, bản stochastic bị vi phạm một
phần nhỏ còn bản DRO luôn nằm trong risk tolerance.

### Đối chiếu với RideBound

| | Hosseini et al. | RideBound hiện tại |
|---|---|---|
| Đối tượng hứa | **cửa sổ** `[ℓ, u]` | **điểm** ETA |
| Nguồn ràng buộc | phân phối travel time | không mô hình hoá bất định |
| Mức bảo đảm | dẫn xuất từ `a_w/a_ℓ` | `hardLimit` chọn tay (30 s) |
| Cách xử lý sai lệch | hấp thụ trong độ rộng | **sửa lời hứa** rồi tính vào budget |
| Sửa lời hứa | không có khái niệm | trung tâm của mô hình |

Đây là chẩn đoán sắc nhất: RideBound hứa một **điểm** rồi phải chi budget mỗi lần thực
tế lệch khỏi điểm đó. Phân phối lưỡng cực đã đo (76,8% request tiêu thụ bằng 0; p90 =
154.821 ms) chính là dấu hiệu của việc hứa quá chặt so với phương sai thật.

### Điều **không** được lấy

- Không copy `a_w`, `a_ℓ`, `a_u` hay bất kỳ mức quantile nào của paper.
- **Không** lấy mô hình integrated routing + window. Paper báo rằng CPLEX chạy 5 giờ vẫn
  không đóng được gap cho instance 21 khách. RideBound là online, 108 request/cell,
  drain 2 giờ — mô hình integrated đó không dùng được. Chỉ **stage (i)**, tức thiết kế
  cửa sổ khi route đã cho, là O(1) mỗi khách và mới khả thi online.
- Bối cảnh là last-mile delivery một chuyến/ngày, không phải ridepooling online có
  onboard rider; không suy rộng kết quả out-of-sample của họ sang panel RideBound.

## 4. `2605.11798` — Mt-KaRRi: xác nhận độc lập cho promise-anchored constraint

Đây là phát hiện có giá trị nhất cho **claim boundary** của bài báo RideBound.

Mt-KaRRi (2026-05) là dispatcher ridepooling scale lớn: ~1 ms/request, hàng triệu
traveler/giờ. Nó dùng insertion heuristic đơn giản, và **bốn** ràng buộc feasibility.
Hai ràng buộc cuối, nguyên văn từ trang 5:

> "Assume that a different rider `r′` of `ν` previously accepted a ride with a tentative
> pickup time `T_p`. If `I` delays the pickup of `r′` to later than `T_p + t_max_wait`
> […] then `I` is considered infeasible."
>
> "consider an existing rider `r′` who accepted a ride with a tentative trip time `T_t`.
> If `I` increases the trip time of `r′` to more than `α·T_t + β` […] then `I` is
> infeasible. Thus, every rider is guaranteed a latest possible arrival time **relative
> to the trip time they originally accepted**."

Và quan trọng hơn, tác giả giải thích **vì sao họ đổi** khỏi công thức gốc của
Laupichler & Sanders (2024):

> "The original code uses wait time and trip time constraints based on the request time
> and direct shortest-path travel time from origin to destination. At the same time,
> travelers are offered rides that break these constraints if no other ride is
> available. With this, **individual riders can restrict vehicles to not allow any
> detours, making pooling impossible and paralyzing fleets.** Thus, we use constraints
> relative to the pickup time and trip time of the accepted ride, which ensures that
> some detours are always possible."

### Ba hệ quả cho RideBound

1. **Xác nhận độc lập rằng promise-anchored là đúng object.** Trước đó tồn tại lo ngại
   rằng Alonso-Mora ràng buộc theo *outcome* (`t^d_r ≤ t^*_r + Δ`, so với chuyến lý
   tưởng) mới là object đúng, còn RideBound ràng buộc theo *lời hứa* là lệch chuẩn. Một
   dispatcher state-of-the-art năm 2026 độc lập hội tụ về đúng anchor "lời hứa đã được
   khách chấp nhận". Tiền đề của RideBound được củng cố, không bị bác.

2. **Cơ chế H6 negative result được xác nhận từ bên ngoài.** Failure mode mà Mt-KaRRi
   mô tả — ràng buộc quá cứng làm rider "**paralyze fleets**", không còn detour nào khả
   thi — là đúng cái RideBound đo được: 534/339 vehicle choice set bị gate làm rỗng,
   143/212 request bị chặn ngay. Đây không còn là quan sát nội bộ; nó là failure mode
   đã được một nhóm khác gặp và ghi lại.

3. **Đóng góp của RideBound vẫn phân biệt được.** Mt-KaRRi dùng **budget mỗi lần**
   (`T_p + t_max_wait`, `α·T_t + β`), tức một trần trên độ lệch tức thời. RideBound dùng
   **budget tích luỹ nhiều chiều trên toàn bộ chuỗi lời hứa**, kèm certificate máy kiểm
   được. Hai thứ khác nhau và Mt-KaRRi không claim cái sau.

### Điều **không** được lấy

- Không claim 1 ms/request cho RideBound. Mt-KaRRi là insertion heuristic + contraction
  hierarchies; RideBound là multi-pass CP-SAT lexicographic + full physical validator
  có certificate. Khác design point, không so trực tiếp.
- Không lấy `t_max_wait`, `α`, `β`, `w_detour`, `w_walk`.
- Mt-KaRRi có mode choice + walking; RideBound không. Không so completion rate tuyệt đối.

## 5. `2602.04599` — horizon ngẫu nhiên thay vì tiêu budget

SDH đặt một continuation probability `α(s,a) ∈ [0,1]`: vi phạm ràng buộc **đẩy `α`
xuống, làm ngắn effective horizon**, và tác giả nói rõ đây là cách thay cho việc
"spending a budget".

Ánh xạ hợp lệ: nó đặt tên cho một trục thiết kế mà WP15 đang cần. F1 của WP14 là
freeze horizon **tĩnh** (300 s / 600 s). SDH cho thấy horizon có thể là **hàm của
trạng thái** — càng gần vi phạm thì càng khoá chặt. Đó chính là ý "lifecycle-aware /
graduated commitment" của WP15, nhưng có một framing nguyên tắc.

Điều **không** áp dụng: SDH là RL/CMDP, soft và xác suất. RideBound bắt buộc phải phát
certificate máy kiểm được cho từng quyết định; một horizon xác suất không certify được
và không thể fail-closed. Không dùng thuật toán, không dùng số run, không dùng recipe
entropy của paper. Chỉ giữ **ý tưởng horizon phụ thuộc trạng thái**.

## 6. `2306.13356` — giảm nguồn phương sai thay vì ràng buộc revision

Adaptive stop pooling cho khách đi bộ tới điểm đón/trả chung, khoảng cách đi bộ tối đa
được điều chỉnh theo demand. Kết quả mô phỏng: giảm mạnh travel time fluctuation ở
**cùng fleet size**, thậm chí cải thiện travel time trung bình.

Đây là trục thứ ba, khác hẳn hai trục RideBound đang có: thay vì (a) cho sửa lời hứa
rồi tính budget, hoặc (b) hứa rộng hơn theo quantile, thì (c) **giảm chính phương sai**
sinh ra nhu cầu sửa.

Điều **không** áp dụng ngay: RideBound khoá `pickup_stop_switch_count` và
`drop_stop_switch_count` ở `hardLimit = 0` cho cả hai arm, và đo được **0 witness** trên
44.156 decision. Stop pooling đòi đổi tập stop — tức đổi ngữ nghĩa của chính hai
dimension đó. Không được bật nó như một factor mà không có ADR riêng, vì nó thay đổi
định nghĩa lời hứa chứ không chỉ nới ràng buộc.

## 7. Tổng hợp — ba trục cho WP15

| Trục | Nguồn | Trạng thái |
|---|---|---|
| A. Ràng buộc revision của lời hứa điểm | RideBound hiện tại (C1/H6) | Đã đo, **âm** `−7,13 pp` |
| B. Hứa một cửa sổ có mức bảo đảm dẫn xuất từ risk | `2508.01032` | **Mới**, chưa từng thử |
| C. Horizon phụ thuộc trạng thái thay vì budget phẳng | `2602.04599` + F1 của WP14 | **Mới**, một phần đã có hạ tầng |
| D. Giảm nguồn phương sai (stop/walking) | `2306.13356` | Mới, đổi định nghĩa lời hứa ⇒ ADR riêng |

Xác nhận ngang: `2605.11798` chứng thực anchor "lời hứa đã accepted" là đúng, và
chứng thực failure mode "ràng buộc cứng làm rỗng tập khả thi".

## 8. Ranh giới claim của chính research note này

- Không authorize `RB-WP14R-008..012`, không authorize WP15 execution, không mở H7.
- Không đổi margin, panel, denominator, factor level hay failure treatment nào.
- Không lấy một tham số số học nào từ bốn paper.
- Không claim RideBound nhanh hơn, tốt hơn hay tương đương bất kỳ paper nào.
- Trục B/C/D chỉ là **hướng thiết kế có bảo chứng literature**; chúng chỉ trở thành
  ticket khi WP14R đóng được frontier và owner authorize bằng ADR mới.
