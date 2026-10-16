# Báo cáo benchmark cuối — RideBound

Toàn bộ số liệu từ hai lần chạy hoàn tất: thí nghiệm xác nhận **H6** (100 job) và
lần chạy mở rộng **WP14** (160 job). Tổng 28,55 giờ + ~14 giờ tính toán, 21,64 GiB
bằng chứng.

Phần 1 định nghĩa mọi thuật ngữ dùng trong các bảng phía sau. Người không đọc mã
nguồn vẫn theo được toàn bộ báo cáo nếu đọc phần này trước.

---

# 1. Từ điển thuật ngữ

## 1.1 Bài toán

Hệ thống điều phối **xe đi chung** (ridepooling). Khách đặt xe, hệ thống hứa hai
mốc giờ: **giờ đón** (pickup ETA) và **giờ tới nơi** (drop ETA). Sau đó khách mới
liên tục đến, và hệ thống **tính lại tuyến** để nhét thêm khách — mỗi lần tính lại
có thể đẩy giờ hẹn của khách cũ muộn đi.

Căng thẳng cốt lõi: **xếp khách hiệu quả cần được phép đổi ý; nhưng đổi ý thì lời
hứa mất giá trị.**

Câu hỏi nghiên cứu: **giữ lời hứa phải trả giá bao nhiêu bằng số khách được phục
vụ?**

## 1.2 Hai đại lượng đo

**Tỉ lệ phục vụ** — bao nhiêu khách trong số khách đến được chở đi thành công.

**Gánh nặng do quyết định** (*decision-induced burden*) — tổng số mili-giây mà
**chính quyết định của hệ thống** đẩy giờ hẹn của khách muộn đi. Đây là điểm mấu
chốt về đo lường: nó **tách riêng** khỏi phần muộn do tắc đường. Tắc đường thì
không ai điều khiển được; còn việc hệ thống tự ý tính lại rồi làm khách muộn thì
là lỗi của hệ thống. Chỉ phần thứ hai được tính.

## 1.3 "Cell" là gì

Một **cell** = một tình huống mô phỏng cụ thể, cố định hoàn toàn:

| Thành phần | Giá trị |
|---|---|
| Ngày dữ liệu thật | ví dụ 2018-11-14 (dữ liệu chuyến đi Manhattan công khai) |
| Mẫu nhu cầu | `sample_10_<r>` — mẫu số 10, bản lặp `r` = 1…4 |
| Khung giờ | 2 tiếng, ví dụ 08:00–10:00 |
| Số khách | **108** |
| Số xe | 8 (hoặc 4, tuỳ panel) |
| Số chỗ mỗi xe | 4 |
| Cửa sổ đón cho phép | 10 phút |
| Thời gian đi tối đa | 1,5× đường đi thẳng |

Tên cell đọc như sau: `d20181114-s10-r1-w08` = ngày **2018-11-14**, mẫu **10**, bản
lặp **1**, khung **08:00**.

Cell là **đơn vị so sánh ghép cặp**: B1 và C1 chạy trên **đúng cùng một cell** —
cùng ngày, cùng khách, cùng xe, cùng bản đồ, cùng hệ số tắc đường. Nên chênh lệch
giữa hai bên chỉ có thể do thuật toán, không do dữ liệu.

## 1.4 Ba thuật toán

### B1 — `rolling-cost` (đối chứng)

Cách làm phổ biến trong ngành. Mỗi khi có khách mới, giải lại bài toán xếp tuyến
để tối thiểu chi phí. **Không tự ràng buộc gì cả** — được đổi xe của khách, đổi
điểm đón, đẩy giờ hẹn muộn bao nhiêu cũng được. Nó chỉ quan tâm tổng chi phí tại
thời điểm hiện tại.

### C1 — `ridebound-hard-vector` (phương pháp của luận văn)

Cũng giải bài toán xếp tuyến, nhưng **tự ràng buộc bằng những lời hứa có chứng
nhận**. Tại thời điểm xác nhận đặt xe, ba thứ bị **khoá cứng**
(`finalConfirmationLocks`):

- `vehicle` — khách **không bao giờ** bị chuyển sang xe khác
- `pickupStop` — điểm đón **không bao giờ** bị dịch
- `pickupEta` — **giờ đón đã hứa không bao giờ bị đẩy muộn**

Còn **giờ tới nơi** thì được phép đẩy muộn, nhưng **có ngân sách**: tổng cộng
không quá N mili-giây do quyết định của hệ thống gây ra. Mọi ứng viên tuyến vi
phạm ràng buộc đều bị loại trước khi giải, nên lời hứa không phải là "cố gắng" mà
là **bảo đảm kiểm chứng được**.

### C2 — `commit-soft-hard-hybrid` (chỉ dùng ở phần robustness)

Bản lai: thay vì chỉ khoá cứng, nó thêm **ngưỡng cảnh báo mềm**
(`warningProfiles`) — vượt ngưỡng thì bị phạt trong hàm mục tiêu thay vì bị loại
thẳng.

## 1.5 "Ngân sách", "chặt", "lỏng", "không ngân sách"

Ngân sách là **giới hạn cứng cho tổng số mili-giây mà giờ tới nơi của một khách
được phép bị đẩy muộn bởi quyết định của hệ thống** (`drop_eta_total_ms`,
`hardLimit`).

| Tên trong báo cáo | Giá trị thật | Nghĩa |
|---|---|---|
| **chặt** (`tight`) | 30.000 ms = **30 giây** | Hệ thống chỉ được tự làm khách muộn tối đa 30 giây |
| **lỏng** (`loose`) | 120.000 ms = **120 giây** | Nới lên 2 phút |
| **không ngân sách** (`unbounded`) | không giới hạn | Bỏ hẳn giới hạn giờ tới nơi — **nhưng ba khoá cứng ở 1.4 vẫn còn** |

Lưu ý quan trọng: "không ngân sách" **không phải** là B1. Nó vẫn không được đổi
xe, không được dịch điểm đón, không được đẩy giờ đón. Nó chỉ tự do ở giờ tới nơi.


## 1.7 Đơn vị

- **đpt** = điểm phần trăm (chênh lệch giữa hai tỉ lệ phần trăm)
- **ms** = mili-giây (1.000 ms = 1 giây)
- Mọi con số dưới đây là **tổng trên toàn panel**, trừ khi ghi rõ là mỗi cell

---

# 2. H6 — thí nghiệm xác nhận

## 2.2 Panel A — bối cảnh và kết quả

**Bối cảnh:** 20 cell × **8 xe** × 4 chỗ. Mỗi cell 108 khách → **2.160 khách mỗi
thuật toán**. 8 xe cho 108 khách trong 2 tiếng là **đội xe tương đối dư**: B1 phục
vụ được 80,3% khách.

**Kết quả:**

| | B1 (`rolling-cost`) | C1 (chặt, 30 s) |
|---|---:|---:|
| Khách đến | 2.160 | 2.160 |
| Khách được phục vụ | **1.735** (80,3241%) | **1.581** (73,1944%) |
| Chênh lệch | — | **−154 khách = −7,1296 đpt** |
| Gánh nặng do quyết định | **74.443.002 ms** (≈ 20,7 giờ) | **128.020 ms** (≈ 2,1 phút) |
| Giảm gánh nặng | — | **99,8280%** |
| **Cổng gánh nặng** | — | **ĐẠT** |

Nhận xét: **C1 gần như xoá sạch việc hệ thống tự làm khách muộn — từ 20,7 giờ
xuống 2,1 phút — nhưng phải bỏ lại 154 khách không phục vụ được.**

### Toàn bộ 20 cell (không chỉ trung bình)

| Cell | B1 | C1 | Δ | Gánh nặng B1 (ms) | Gánh nặng C1 (ms) | Gián đoạn B1 | Gián đoạn C1 |
|---|---:|---:|---:|---:|---:|---:|---:|
| d20181114-r1 | 99 | 92 | −7 | 4.616.801 | 0 | 26 | 0 |
| d20181114-r2 | 94 | 87 | −7 | 4.711.146 | 0 | 25 | 0 |
| d20181114-r3 | 94 | 86 | −8 | 3.230.326 | 26.663 | 34 | 1 |
| d20181114-r4 | 101 | 94 | −7 | 4.963.423 | 0 | 27 | 0 |
| d20181115-r1 | 90 | 79 | −11 | 4.747.099 | 0 | 21 | 0 |
| d20181115-r2 | 97 | 93 | −4 | 1.683.385 | 0 | 15 | 0 |
| d20181115-r3 | 90 | 81 | −9 | 5.283.507 | 6.542 | 23 | 1 |
| d20181115-r4 | 99 | 88 | −11 | 5.163.581 | 0 | 30 | 0 |
| d20181116-r1 | 91 | 85 | −6 | 1.976.291 | 0 | 14 | 0 |
| d20181116-r2 | 91 | 76 | **−15** | 5.304.407 | 30.095 | 31 | 3 |
| d20181116-r3 | 87 | 85 | −2 | 3.072.271 | 17.557 | 24 | 2 |
| d20181116-r4 | 83 | 77 | −6 | 4.165.769 | 0 | 22 | 1 |
| d20181117-r1 | 80 | 74 | −6 | 2.457.573 | 29.494 | 17 | 2 |
| d20181117-r2 | 83 | 82 | **−1** | 2.644.304 | 2.458 | 22 | 1 |
| d20181117-r3 | 66 | 60 | −6 | 2.484.557 | 0 | 16 | 0 |
| d20181117-r4 | 81 | 71 | −10 | 3.995.958 | 4.916 | 25 | 1 |
| d20181118-r1 | 80 | 70 | −10 | 3.207.030 | 0 | 18 | 0 |
| d20181118-r2 | 74 | 67 | −7 | 3.364.047 | 0 | 17 | 1 |
| d20181118-r3 | 68 | 62 | −6 | 3.894.250 | 10.295 | 22 | 3 |
| d20181118-r4 | 87 | 72 | **−15** | 3.477.277 | 0 | 22 | 0 |

**Phân bố chênh lệch phục vụ:**

| Xấu nhất | Phân vị 25 | Trung vị | Phân vị 75 | Trung bình | Tốt nhất |
|---:|---:|---:|---:|---:|---:|
| **−15 khách** | −10,00 | **−7,0** | −6,00 | −7,70 | **−1 khách** |

Nửa giữa số cell (phân vị 25 đến 75) nằm trong khoảng **−10 đến −6 khách**.

- **C1 phục vụ ít hơn ở 20/20 cell — không một ngoại lệ nào.**
- C1 có gánh nặng **bằng 0 tuyệt đối** ở **12/20 cell**.
- Số quyết định gây gián đoạn: B1 từ 14 đến 34 mỗi cell; C1 từ **0 đến 3**.

Con số **20/20** là điểm mạnh thống kê quan trọng nhất của Panel A: thiệt hại phục
vụ mang tính **hệ thống**, không phải hiệu ứng của vài cell may rủi. Không thể bác
bằng lý lẽ "chỉ là nhiễu".

## 2.3 Panel B — bối cảnh và kết quả

**Bối cảnh:** **cùng đúng 20 cell đó**, nhưng chỉ **4 xe** thay vì 8. Đây là kịch
bản **đội xe khan hiếm**: B1 chỉ phục vụ được 44,7% khách. Mục đích là kiểm tra
kết luận có đổi chiều khi nguồn lực bị bóp lại hay không.

| | B1 | C1 (chặt, 30 s) |
|---|---:|---:|
| Khách được phục vụ | **966** (44,7222%) | **860** (39,8148%) |
| Chênh lệch | — | **−106 khách = −4,9074 đpt** |
| Gánh nặng do quyết định | **44.766.809 ms** (≈ 12,4 giờ) | **342.974 ms** (≈ 5,7 phút) |
| Giảm gánh nặng | — | **99,2339%** |
| **Cổng gánh nặng** | — | **ĐẠT** |

**Phân bố chênh lệch phục vụ:**

| Xấu nhất | Phân vị 25 | Trung vị | Phân vị 75 | Trung bình | Tốt nhất |
|---:|---:|---:|---:|---:|---:|
| **−13 khách** | −6,00 | **−5,5** | −3,25 | −5,30 | **+2 khách** |

- C1 ít hơn ở **18/20** cell, **bằng nhau 1** cell, **nhiều hơn 1** cell
  (`d20181117-r4`: **+2 khách**).
- C1 gánh nặng bằng 0 ở **7/20** cell.

**Kết luận từ hai panel:** thiệt hại **nhỏ hơn** khi xe khan hiếm (−4,91 so với
−7,13 đpt), nhưng **vẫn vượt ngưỡng gấp gần 5 lần**. Kết luận không đổi chiều. Cell
`d20181117-r4` cho thấy ở đội xe nhỏ đôi khi ràng buộc lại *có lợi* — nhưng đó là
1/20 cell, không đủ để đảo kết luận.

## 2.4 Phân rã phần giảm gánh nặng — vì sao không được thổi phồng con số 99,8%

Con số "giảm 99,8%" nghe rất mạnh, nhưng phải tách làm hai phần:

| Panel | Tổng giảm | Phần **do định nghĩa** (khoá giờ đón) | Phần **đo thật** (giờ tới nơi) |
|---|---:|---:|---:|
| A | 74.314.982 ms | 9.579.869 ms (**12,89%**) | 64.735.113 ms (**87,11%**) |
| B | 44.423.835 ms | 5.056.311 ms (**11,38%**) | 39.367.524 ms (**88,62%**) |

Khoảng **12%** phần giảm là **tự động theo định nghĩa**: C1 khoá cứng giờ đón, nên
gánh nặng giờ đón của nó **bắt buộc** bằng 0, không phải vì nó giỏi hơn. Còn ~88%
là đo thật ở chiều giờ tới nơi.


## 2.5 Robustness (5 cell) — nó là gì và để làm gì

**Robustness** = kiểm tra kết luận có **mong manh** không. Nếu chỉ đổi một tham số
nhỏ mà kết luận đảo chiều thì kết luận đó không đáng tin.

Nó chạy trên **5 cell** (cell `r1` của mỗi ngày trong 5 ngày) với **4 biến thể**,
tổng 20 job. Nó là **mô tả** (`descriptiveOnlyCannotRescuePrimary`): dùng để hiểu,
**không** dùng để tuyên bố thành công. Đã tiền đăng ký như vậy.

Bốn biến thể và câu hỏi mỗi cái trả lời:

| Biến thể | Là gì | Trả lời câu hỏi |
|---|---|---|
| **C1 chặt** | C1, ngân sách 30 giây | Mốc đối chiếu |
| **C1 không ngân sách** | C1, bỏ giới hạn giờ tới nơi (3 khoá cứng vẫn còn) | Ngân sách gây ra bao nhiêu phần thiệt hại? |
| **C2 hybrid lỏng** | Bản lai, cảnh báo mềm, 120 giây | Ràng buộc mềm có tốt hơn khoá cứng? |
| **seed 19** | B1 và C1 chặt, đổi hạt giống bộ giải | Kết quả có phải do may mắn của bộ giải? |

Kết quả trên 5 cell (540 khách mỗi biến thể):

| Đối chiếu | Δ phục vụ | Δ số lần gây gián đoạn | Δ gánh nặng (ms) |
|---|---:|---:|---:|
| C1 chặt − B1 | **−40** | −94 | −16.975.300 |
| C1 không ngân sách − B1 | **−20** | −64 | −12.221.704 |
| C2 hybrid lỏng − B1 | **−38** | −79 | −15.717.280 |
| C1 chặt − C1 không ngân sách | −20 | −30 | −4.753.596 |
| **seed 19 − seed 7 (B1)** | **0** | **0** | **0** |
| **seed 19 − seed 7 (C1)** | **0** | **0** | **0** |

Ba điều đọc ra được:

1. **Bỏ ngân sách giảm được một nửa thiệt hại** (−20 thay vì −40 khách) nhưng vẫn
   thiệt. Nghĩa là **thiệt hại không chỉ do ngân sách** — ba khoá cứng (xe, điểm
   đón, giờ đón) tự chúng đã gây một nửa.
2. **Bản lai mềm không cứu được gì**: −38 so với −40, gần như y hệt khoá cứng.
3. **Hai hạt giống cho kết quả bằng nhau tuyệt đối (0/0/0).** Đây xác nhận điều đã
   tiền đăng ký (`solverSeedsAreReplicates: false`): hạt giống **không** sinh biến
   thiên, nên **không được đếm như bản lặp** để làm mẫu trông lớn hơn thực tế. Đây
   là một cái bẫy thống kê phổ biến mà luận văn tránh được và chứng minh được là đã
   tránh.

---

# 3. WP14 — lần chạy mở rộng

## 3.1 WP14 hỏi điều gì và khác H6 ra sao

H6 đã cho biết **cái giá tồn tại**. WP14 hỏi tiếp: **cái giá đó đến từ đâu, và có
làm nó rẻ hơn được không?**

| | H6 | WP14 |
|---|---|---|
| Tính chất | **Xác nhận** — có ngưỡng, kết luận đạt/không đạt | **Thăm dò** — không có ngưỡng, không kết luận đạt/không |
| Ngày dữ liệu | 14–18/11/2018 | **12–13/11/2018** |
| Khung giờ | 08:00–10:00 | 08:00–10:00 **và 17:00–19:00** |
| Số cell | 20 | 16 |
| Số thuật toán | 2 | **10** |
| Số job | 100 | **160** |

Hai bộ ngày **không giao nhau** (đã kiểm tra 0 giao trên cả 7 trục). Đây là chủ ý:
WP14 **không được** dùng để xác nhận hay cứu H6. Nó là tập dữ liệu riêng để thăm
dò cơ chế.

Thêm khung **17:00–19:00** (giờ cao điểm chiều) là điểm mới: H6 chỉ có buổi sáng.

## 3.2 Mười biến thể được thử

Tất cả đều là C1, mỗi cái **khác bản đối chiếu đúng một điều đã khai báo**:

| Arm | Khác bản đối chiếu ở điều gì |
|---|---|
| `b1-ref` | Là B1, không phải C1 — mốc đối chứng |
| `c1-h6ref` | **Bản đối chiếu** — đúng cấu hình C1 của H6 (ngân sách 30 giây) |
| `c1-budget60` | Ngân sách 30 → **60 giây** |
| `c1-budget120` | Ngân sách 30 → **120 giây** |
| `c1-nobudget` | **Bỏ hẳn** ngân sách giờ tới nơi |
| `c1-freeze300` | **F1**: thêm khoá giờ đón trong **5 phút** cuối trước khi đón |
| `c1-freeze600` | **F1**: khoá trong **10 phút** cuối |
| `c1-ratchet` | **F2**: khoá giờ đón thành **một chiều** — cấm đẩy muộn, **cho phép đẩy sớm** |
| `c1-freeze300ratchet` | Cả F1 (5 phút) và F2 |
| `c1-nopickuplock` | **Bỏ** khoá giờ đón |

**F2 (ratchet)** là cơ chế lấy từ Alonso-Mora và cộng sự. Ý tưởng: khoá hai chiều
là quá thô — nếu tính lại mà giờ đón **sớm hơn** thì khách có lợi, sao lại cấm? Nên
chỉ cấm chiều làm muộn.

## 3.3 Kết quả: 10 biến thể chỉ cho 5 kết cục

Mỗi arm chạy 16 cell × 108 khách = **1.728 khách**.

| Arm | Phục vụ | Tỉ lệ | vs B1 | Gánh nặng (ms) | Giảm | Khách bị tính gánh nặng | Gián đoạn |
|---|---:|---:|---:|---:|---:|---:|---:|
| `b1-ref` | **1.629** | 94,2708% | — | 59.250.164 | — | 388 | 427 |
| `c1-nobudget` | 1.584 | 91,6667% | −2,6042 | 7.496.399 | 87,35% | 62 | 67 |
| `c1-budget120` | 1.563 | 90,4514% | −3,8194 | 2.554.106 | 95,69% | 41 | 43 |
| `c1-budget60` | 1.540 | 89,1204% | −5,1505 | 716.125 | 98,79% | 20 | 22 |
| **6 arm trùng nhau** | 1.534 | 88,7731% | −5,4977 | 83.373 | **99,86%** | 6 | 9 |

Sáu arm cho **kết quả giống hệt nhau trên cả 8 bộ đếm, ở cả 16 cell**:
`c1-h6ref`, `c1-freeze300`, `c1-freeze600`, `c1-ratchet`, `c1-freeze300ratchet`,
`c1-nopickuplock`.

**Nghĩa là: ba trong bốn cơ chế được thử — cửa sổ đóng băng (F1), ratchet (F2), và
khoá điểm đón — không đổi được một con số nào.** Chỉ **ngân sách** có tác dụng.

### Biên đánh đổi 5 điểm

Năm kết cục tạo thành một **biên đơn điệu chặt** trên cả hai trục — mỗi bước đổi
phục vụ lấy độ tin cậy của lời hứa, không điểm nào bị trội:

```
phục vụ 94,27%  ←→  gánh nặng 59.250.164 ms   B1 (không hứa gì)
        91,67%  ←→              7.496.399 ms   C1 không ngân sách
        90,45%  ←→              2.554.106 ms   C1 ngân sách 120 s
        89,12%  ←→                716.125 ms   C1 ngân sách  60 s
        88,77%  ←→                 83.373 ms   C1 ngân sách  30 s
```

Đây là **kết quả có giá trị thực tiễn nhất của WP14**: người vận hành đọc bảng này
là chọn được điểm làm việc. Muốn xoá 99,86% việc tự làm khách muộn thì mất 5,50
đpt; chấp nhận xoá 87,35% thì chỉ mất 2,60 đpt.

### Toàn bộ 16 cell (B1 so với `c1-h6ref`)

| Cell | B1 | C1 | Δ | Gánh nặng B1 (ms) | Gánh nặng C1 (ms) |
|---|---:|---:|---:|---:|---:|
| d20181112-r1-w08 | 104 | 98 | −6 | 6.270.875 | 0 |
| d20181112-r1-w17 | 102 | 95 | −7 | 4.316.778 | 5.353 |
| d20181112-r2-w08 | 103 | 100 | −3 | 4.439.928 | 8.923 |
| d20181112-r2-w17 | 105 | 102 | −3 | 3.388.856 | 0 |
| d20181112-r3-w08 | 106 | 98 | −8 | 4.357.875 | 0 |
| d20181112-r3-w17 | 101 | 83 | **−18** | 4.497.047 | 0 |
| d20181112-r4-w08 | 102 | 93 | −9 | 3.410.263 | 0 |
| d20181112-r4-w17 | 99 | 93 | −6 | 1.738.143 | 0 |
| d20181113-r1-w08 | 102 | 101 | −1 | 2.455.997 | 0 |
| d20181113-r1-w17 | 96 | 88 | −8 | 3.285.178 | 15.791 |
| d20181113-r2-w08 | 104 | 104 | **0** | 4.455.942 | 0 |
| d20181113-r2-w17 | 100 | 97 | −3 | 3.988.040 | 27.641 |
| d20181113-r3-w08 | 103 | 98 | −5 | 2.994.979 | 0 |
| d20181113-r3-w17 | 100 | 92 | −8 | 2.955.492 | 19.742 |
| d20181113-r4-w08 | 101 | 97 | −4 | 3.379.956 | 5.923 |
| d20181113-r4-w17 | 101 | 95 | −6 | 3.314.815 | 0 |

**Phân bố:**

| Xấu nhất | Phân vị 25 | Trung vị | Phân vị 75 | Trung bình | Tốt nhất |
|---:|---:|---:|---:|---:|---:|
| **−18 khách** | −8,00 | **−6,0** | −3,00 | −5,94 | **0 khách** |

Ít hơn ở **15/16** cell, bằng nhau **1** cell. C1 gánh nặng bằng 0 ở **10/16** cell.

Theo khung giờ: buổi sáng (w08) trung bình −4,50 khách; buổi chiều (w17) −7,38
khách. **Giờ cao điểm chiều đắt hơn** — thông tin mới mà H6 không có, vì H6 chỉ có
buổi sáng.
