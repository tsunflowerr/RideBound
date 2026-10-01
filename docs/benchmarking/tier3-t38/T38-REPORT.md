# T3.8 + T3.9 trên panel A, chỉ tắc đường thật: báo cáo (THĂM DÒ, CÓ NIÊM PHONG TRƯỚC)

- **Kế hoạch:** `T38-PLAN.md`, SHA-256 `78954a15…8a60`. Push lên `research/tier3-no-worse` lúc 06:44 ngày 1/10 (commit `323a8d9`), **trước job đầu tiên**.
- **Khoảng dự phòng γ:** `GAMMA-t38-v1.json` (`2bdd3b0e…98cd`). Push lúc 23:11 ngày 1/10 (commit `19517b0`), **trước job kiểm tra đầu tiên** (23:11:40).
- **Phụ lục:** `T38-AMENDMENTS.md` (A1, A2, A3).
- **Chạy:** 15 job hiệu chỉnh (06:45–07:24 ngày 1/10, cộng một job chạy lại) và 170 job kiểm tra (23:11 ngày 1/10 – 06:19 ngày 2/10). Output `C:\RideBoundData\research\tier3-t38-v1\`.
- **Phân tích:** `t38_analyze.py` → `analysis-2026-10-02-report.txt`, `-jobs.tsv`.
- **Thăm dò:** ngưỡng, biên và cách chia do Claude chọn theo ủy quyền của chủ nghiên cứu ngày 1/10, sau khi đã xem Việc A, B, w07x và điểm số trên panel phát triển (kế hoạch §0). Chủ nghiên cứu chưa xem lại các giá trị này.

## 1. Hợp lệ

- 170/170 job kiểm tra và 15/15 job hiệu chỉnh có `status: pass`. Hai job phải chạy lại một lần vì lỗi hạ tầng: một ở hiệu chỉnh (máy ngủ, A1), một ở kiểm tra (hết thời gian chờ khi máy quá tải, A3). Thư mục lỗi của cả hai được giữ nguyên.
- Audit v/e/p `ok`. Log thế giới đúng ngày, đúng khung, mốc `bin × 900 s`, hệ số khớp tệp hệ số. Cùng inventory `0da63eaf…4cbe`.
- Mỗi nhánh có đúng một `policyConfigurationHash` trên mọi ô (5 mã cho 5 nhánh). 0 vấn đề.
- Cả 34 ô kiểm tra có đủ 5 nhánh `status-pass`.

## 2. H3: khoảng dự phòng (T3.8). ĐẠT theo quy tắc của kế hoạch

- **Hiệu chỉnh:** 15 ô, α = 1/10, k = 15, nên γ là điểm số lớn nhất = **222,259 s**. Khoảng dời giờ trả tối đa = 30 + 222,259 = **252,259 s**.
  - Điểm số hiệu chỉnh trải từ 30,2 s (khung 17h) đến 222,3 s (khung 7h).
  - γ do **một** ô quyết định: 14/11 mẫu 2, khung 7h. Điểm cao thứ hai là 165,1 s.
- **Kiểm tra:** 2/34 ô vượt γ = **5,9%**. Hai ô là 14/11 mẫu 3 khung 7h (252,5 s) và 15/11 mẫu 2 khung 7h (226,4 s). Cả hai chỉ vượt γ 4–30 s.
  - **H3a** (≤ 3/34): đạt.
  - **H3b** (β + γ ≤ 300 s): đạt (252,3 s). Mức 120 s: không đạt.
  - Kiểm tính đúng: ở mọi ô được phủ, khách bị dời nhiều nhất ≤ β + γ (0 ngoại lệ).
- **Cận trên Clopper–Pearson 95% một phía** của tỉ lệ vi phạm: **17,4%** (2/34). Theo kế hoạch con số này chỉ để báo, không phải điều kiện đạt. Nó không nằm dưới 10%.
- **Điều cần nói thẳng: độ phủ thay đổi theo khung giờ.**

| Khung | Ô kiểm tra | Vi phạm | Cận trên CP 95% | Điểm số S_j (trung vị / lớn nhất) |
|---|---|---|---|---|
| 7–9h | 9 | **2** | 55% | 192,3 / 252,5 s |
| 8–10h | 14 | 0 | 19,3% | 105,6 / 142,8 s |
| 17–19h | 11 | 0 | 23,8% | 56,6 / 85,5 s |

  - Cả hai vi phạm đều ở 7–9h: 7/9 ô được phủ (78%), thấp hơn 90%. Độ phủ chung 94% đạt nhờ các khung còn lại.
  - Cùng một γ = 222 s quá rộng cho 17–19h (điểm số tối đa 85,5 s) và vừa đủ, thậm chí hơi hẹp, cho 7–9h. Tập hiệu chỉnh chỉ có 4 ô 7–9h.
  - Kế hoạch yêu cầu độ phủ chung, nên H3a đạt. Báo thêm cảnh báo này là chính xác hơn việc chỉ nói "đạt".
- **Theo ngày:** 14/11 có 1 vi phạm, 15/11 có 1, các ngày 16–18/11 không có. Các số liệu này chỉ là mô tả.

## 3. H2: số khách và gánh phục hồi (T3.9, 34 ô kiểm tra). ĐẠT theo quy tắc của kế hoạch

- **H2a, cùng số khách:** đạt.
  - Trung bình `C-30` − `Mplus-30` = **0,00** khách mỗi ô, khoảng 95% [−0,15; +0,12].
  - Trung bình `C-30` − `V-30` = **+0,06**, khoảng 95% [−0,12; +0,21].
  - Cả hai nằm sâu trong [−2; +2]. Không ô nào có chênh > 2.
  - Tổng 34 ô: `C-30` 2.338, `Mplus-30` 2.338, `V-30` 2.336, `Mplus-60` 2.347.
- **H2b, không cần xử lý kẹt:** đạt. 0 quyết định non-normal ở cả 34 ô kiểm tra (và 0/15 ô hiệu chỉnh, tức 0/49).
- **H2c, trải nghiệm không kém:** đạt. Chênh E2 trung bình +0,0009, khoảng 95% [−0,0001; +0,0022], còn xa mức 0,02.
- **Sàn so với `U`:** đạt, nhưng đây là **chi phí có thật**. `U` phục vụ 2.434, còn `C-30` phục vụ 2.338: kém **96 khách** trên 3.672 yêu cầu (2,6%). Trung bình **2,82** khách mỗi ô, khoảng 95% [1,79; 3,82], nằm dưới sàn 5 đã chốt. Chi phí tập trung ở:
  - 8–10h: −39 khách (1.014 → 975);
  - 17–19h: −52 khách (904 → 852);
  - 7–9h: chỉ −5 khách (516 → 511).
- **Khác biệt chính nằm ở lời hứa, không ở số khách** (mô tả):

| | `C-30` | `Mplus-30` | `Mplus-60` | `V-30` |
|---|---|---|---|---|
| Phục vụ (34 ô) | 2.338 | 2.338 | 2.347 | 2.336 |
| Tỉ lệ quyết định bị ép | 0% | 41,3% | 22,5% | 53,8% |
| Khách bị phá lời hứa của chính luật đó | 0% | 14,5% | 4,2% | 21,6% |
| Số ô có ít nhất một lời hứa bị phá | 0/34 | 28/34 | 22/34 | 34/34 |

  - Ở 7–9h, riêng `Mplus-30` phá lời hứa của chính nó ở **36,5%** khách (9/9 ô) và bị ép ở 75,6% quyết định.
  - Ở 17–19h `Mplus-30` phá lời hứa ở 1,0% khách (5/11 ô), và bị ép ở 5,0% quyết định.
  - `C-30` và `Mplus-30` có cùng chuỗi hành động đội xe ở 30/34 ô; `C-30` và `V-30` ở 27/34 ô.
- `Mplus-60` phục vụ nhiều hơn `C-30` 9 khách trên 34 ô (+0,26%). Chênh này nằm ngoài H2 (H2 không so với `Mplus-60`) nên chỉ báo mô tả.

## 4. Dự đoán đã niêm phong: 7/8 đúng, 1 sai

| # | Kết quả |
|---|---|
| P1 | **Đúng.** γ = 222,3 s, trong [100; 250] |
| P2 | **Đúng.** β + γ = 252,3 s, trong (120; 300] |
| P3 | **Đúng.** 2 vi phạm (≤ 3), cả hai ở 7–9h |
| P4 | **Đúng.** 0 quyết định non-normal ở cả 49 job `C-30` |
| P5 | **Đúng.** H2a đạt và không ô nào có \|chênh\| > 2 |
| P6 | **Đúng.** H2c và sàn đạt; trung bình F = +2,82, trong [0; 3] (sát biên trên) |
| P7 | **Sai.** Dự đoán `Mplus-30` phá lời hứa ở ≥ 8/9 ô 7–9h (thực tế 9/9, đúng) và ở ≤ 3/11 ô 17–19h (thực tế **5/11**, sai) |
| P8 | **Đúng.** Tỉ lệ bị ép của `V-30` (53,8%) lớn hơn `Mplus-30` (41,3%) |

## 5. Đọc đúng tầm

- **Điều đứng được trên 34 ô kiểm tra:** `C-30` phục vụ **cùng số khách** với luật hạn chót một phía và luật theo giờ khách thấy (chênh trung bình, kể cả khoảng tin cậy 95%, trong ±0,25 khách mỗi ô), không bao giờ phải xử lý kẹt, và không bao giờ phá lời hứa của chính nó, trong khi hai luật kia phải ép 41–54% quyết định và phá lời hứa của chính mình ở 14,5–21,6% khách.
- **Điều không đứng được:**
  - Không có bằng chứng "C phục vụ nhiều hơn". Kết quả là tương đương về số khách, đúng như giả thuyết đã được duyệt ngày 30/9.
  - `C-30` kém `U` 2,6% khách, chi phí có thật. Nó nằm dưới sàn do Claude chọn, chủ nghiên cứu chưa duyệt sàn 5.
  - Khoảng dự phòng hiệu lực ở mức chung. Theo khung giờ, 7–9h chỉ phủ 7/9.
  - H3a đạt bằng quy tắc "≤ 3/34" của kế hoạch. Cận trên CP 95% là 17,4%, không dưới 10%.
- **Chỗ yếu của thiết kế:**
  - Thăm dò. Ngưỡng, biên, cách chia do Claude chọn sau khi đã xem kết quả trước.
  - Thế giới chỉ thật, nên mỗi (ngày, mẫu, khung) chỉ cho một episode; không có ngẫu nhiên.
  - Panel 49 ô, mỗi ngày chỉ một đến hai nhóm hiệu chỉnh. γ do một ô quyết định.
  - 11 ô 7–9h và 17–19h không dựng được, nên khung 7–9h chỉ có 13 ô trên tổng 49 (4 hiệu chỉnh, 9 kiểm tra).
  - Ba khung của cùng một (ngày, mẫu) cùng nguồn nhu cầu, chồng 3–20/108 dòng. Cách chia theo nhóm đã tránh đặt chúng ở hai bên.
  - Chạy lại hai job do lỗi hạ tầng. Phân tích dùng các job chạy lại, không dùng thư mục lỗi.
  - Ô `d20181114-s10-r1` ở tập hiệu chỉnh (đã bị xem ở smoke).

## 6. Kiểm độc lập

Một agent viết mã riêng (`independent/iv_extract.py`, `iv_analyze.py`; không đọc script phân tích của scratchpad), tính lại từ bản ghi
thô của 185 lần chạy. **Mọi mục đều khớp, không mục nào lệch** (`independent/RESULT.md`, `iv_analyze.out.txt`):
- γ = 222.259 ms (n = 15, k = 15, từ ô 14/11 mẫu 2 khung 7h), cả 15 điểm số hiệu chỉnh bằng điểm số trong tệp γ;
- 2 vi phạm ở đúng hai ô đã nêu (252.492 và 226.400 ms), 32/34 ô được phủ, không có điểm số vô hạn;
- tổng số khách phục vụ của 5 nhánh giống hệt; trung bình và khoảng tin cậy bootstrap của D_M, D_V, F, ΔE2 giống hệt (ΔE2: +0,000857, [−0,000082; +0,002218]);
- 0 quyết định non-normal ở cả 49 job `C-30`; tỉ lệ phá lời hứa của chính luật, kể cả theo khung giờ, giống hệt;
- ở cả 32 ô được phủ, khách bị dời nhiều nhất ≤ 252.259 ms (lớn nhất 216.005 ms);
- chéo với đặc tả: |x|, |c|, |z| của giờ trả bằng các trường chênh lệch trong sổ cái ở cả 382.006 lần sửa giờ.

Chi tiết agent nêu thêm (số liệu thật, không đổi kết luận):
- `U` − `C-30` lệch hơn 2 khách ở **21/34** ô; khoảng từ −4 đến +9 khách mỗi ô. Chi phí so với `U` không đồng đều giữa các ô, trung bình +2,82 che điều đó.
- Chênh lớn nhất của `C-30` so với `Mplus-30` và `V-30` là 2 khách, ở một ô (14/11 mẫu 3 khung 8h: `C-30` 73, hai nhánh kia 75).
- Các tỉ lệ ở báo cáo này là trung bình **không trọng số theo ô**. Gộp theo khách, tỉ lệ phá lời hứa thấp hơn: `Mplus-30` 12,4% (thay vì 14,5%), `Mplus-60` 3,4% (thay vì 4,2%).
- Mọi khách được hứa đều đã xuống xe ở mọi lần chạy, nên mẫu số "đã hứa" và "đã xuống xe" cho cùng số.
- Số khách phục vụ lấy từ điểm kiểm tra cuối; `summary.json` không có số này, nên không có nguồn thứ hai để so.
