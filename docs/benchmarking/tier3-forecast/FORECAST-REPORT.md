# Hứa giờ có tính trước tắc đường: báo cáo (THĂM DÒ, NIÊM PHONG TRƯỚC)

**Nguồn gốc và mốc thời gian.**
- **Kế hoạch:** `FORECAST-PLAN.md`, SHA-256 `a5524912…7337`.
  - Commit `33d8071`; push lúc 08:27:18 ngày 6/10, **trước mọi job** (thư mục output đầu tiên tạo lúc 08:27:26).
  - Thiết kế do vòng tranh luận ba lập trường cộng trọng tài chốt (`debate1-verdict-2026-10-06.json`).
- **Phụ lục:**
  - A1 (`df7f893`, 10:35:47): xử lý ô bị dừng, viết trước khi tính bất kỳ thước đo Họ B nào.
  - A2: các sửa sau khi có kết quả, theo bản rà của người chấm `review/NUMBER-TRACE-REVIEW.md`. A2 không đổi kết luận đạt/trượt nào.
- **Chạy:** 43 job, gồm 3 cổng, 32 Họ B và 8 tuyết, output ở `C:\RideBoundData\research\tier3-forecast-v1`. Trước niêm phong có thêm 5 lần chạy phát triển ở `tier3-forecast-checks-v1`.
- **Họ A không chạy mô phỏng mới.** Nó phát lại 170 lần chạy kiểm tra T3.8 và không phụ thuộc cổng. Cổng E1/E2 chỉ dùng để cho phép dùng lại 32 lần chạy không dự báo làm đối chứng của Họ B.
- **Số liệu** lấy từ các tệp output đi kèm script:
  - `test-family-a-<quy tắc>-2026-10-06-*`;
  - `test-family-a-two-sided-2026-10-06-report.txt`;
  - `test-family-b-2026-10-06-report.txt`;
  - `test-snow-2026-10-06-report.txt`;
  - `report-extras/report-extras-2026-10-06.txt` (các số không in sẵn trong các tệp trên);
  - `check-gates-2026-10-06.log`, `check-runs-v2-tier{1,2}-2026-10-06.log`, `check-runs-snow-2026-10-06.log`;
  - log kiểm thử trong `report-extras/`.

## 1. Hợp lệ

- **Cổng E1 và E2 đạt.** Mplus-30 tắt dự báo (E1) và C-30 với dự báo giả có tỉ số luôn bằng 1 (E2), chạy bằng lớp bọc v1.3, đều **tương đương hoàn toàn** lần chạy T3.8 cùng ô:
  - cùng quyết định có thời điểm;
  - cùng bảng v/e/p, tập khách, giờ trả;
  - cùng từng khung dữ liệu sau khi bỏ trường có muối theo lần chạy.

  S1 (Mplus-30F trên ô phát triển) chạy hết. Đối chứng dương của phép so (U khác C-30) và phép tự kiểm đột biến được chạy **trước** niêm phong (`equiv-selftest-2026-10-06-c.txt`).
- **Họ B:** 29/32 lần chạy đạt; 3 lần dừng vì `RBWP7_FLEETPY_PLAN_INFEASIBLE` (§4).
- **Tầng tuyết:** 8/8 lần chạy đạt.
- **Các phép kiểm sau mỗi tầng:**
  - mọi dòng tỉ số dự báo khớp bảng niêm phong tới 1e−6;
  - dòng tắc đường trùng lần chạy không dự báo ở các bin chung;
  - 0 quyết định non-normal ở 14 lần chạy C-30F chạy xong.

## 2. Họ A (chính): lớp lời hứa có dự báo; C-30; 21 ô ngày thường

Mọi kết quả Họ A mang nhãn **"đo lường có đăng ký trước trên các ngày-khung đã được xem trước"** (kế hoạch §0).

| Thước đo (trung bình ô) | Không dự báo | Đệm cùng giá | **Lớp lời hứa** |
|---|---|---|---|
| Khách trễ hơn lời hứa đầu > 60 s (Y), 21 ô | 6,01% | 1,94% | **0,17%** |
| … khung 7h (8 ô) | 14,44% | 4,24% | **0,27%** |
| … khung 8h (8 ô) | 1,35% | 0,86% | **0,18%** |
| … khung 17h (5 ô) | 0% | 0% | 0% |
| Khách đến sớm hơn lời hứa > 60 s (E) | 0% | — | 0,08% |
| Giờ trả hiển thị dao động tổng > 60 s (Vis) | 7,48% | 7,48% (đúng theo cấu tạo, vì đệm là hằng số) | **1,06%** |
| Sai số tuyệt đối trung bình \|A − lời hứa\| | 17,1 s | — | 7,0 s |
| Trễ > 120 s | 1,03% | — | 0% |

**Quy tắc niêm phong.** Bootstrap theo cụm ngày-khung, phân tầng theo khung.
- **A1 ĐẠT.**
  - ΔY = −5,84 điểm %, khoảng 95% [−8,78; −3,23], cluster-t (df = 5) [−10,54; −1,15].
  - ΔY < 0 ở cả 3 buổi sáng: 14/11 −13,1; 15/11 −21,9; 16/11 −7,2 điểm %.
  - ΔE = +0,08 điểm %.
  - Bỏ từng ngày, ΔY vẫn âm: −6,25 / −3,54 / −8,21 điểm %.
- **A2 ĐẠT.** ΔVis = −6,42 điểm %, khoảng [−8,29; −4,31], cluster-t [−9,90; −2,94].
- **A3 ĐẠT, mang nhãn "mong manh với suy luận ít cụm".**
  - Y(lớp lời hứa) − Y(đệm cùng giá theo khung) = −1,77 điểm %, khoảng bootstrap [−2,83; −0,68].
  - Cluster-t là [−3,62; **+0,07**]. Kế hoạch §4 gắn nhãn khi bất kỳ khoảng cluster-t nào có cận trên ≥ 0, trong khi mã chỉ kiểm cho ΔY. Chỗ lệch này được ghi ở phụ lục A2.
- **Báo kèm:**
  - đệm toàn thành phố cùng giá: −3,02 điểm %, khoảng [−4,74; −1,23];
  - bootstrap theo ô kiểu T3.8: ΔY [−9,26; −2,75];
  - trung bình triển khai trên 34 ô (ô cuối tuần không có dự báo, chênh 0): −3,61 điểm %;
  - lớp lời hứa cho Mplus-30 và V-30 cũng cho Y 0,17% trên 21 ô.

**Giá phải trả.**
- Lời hứa trả dài thêm trung bình 13,7 s (trung bình các p90 theo ô là 33,3 s); khung 7h dài thêm 27,8 s (66,3 s).
- Lời hứa đón đầu tiên dài thêm 3,2 s (khung 7h 6,5 s).
- **Mâu thuẫn vận hành:** 34/1.471 khách (2,3%; 26 ở khung 7h) có **lời hứa đón đầu tiên** hiển thị muộn hơn hạn đón muộn nhất. Nguyên nhân: phép kiểm khả thi vẫn dùng giờ không dự báo. Muốn triển khai thì phải xử lý chỗ này.

**Sổ chia trễ ba phần** (C-30, khách trễ > 60 s so với lời hứa không dự báo, 21 ô; tổng 6.217 s):
- do điều phối 82 s (1,3%);
- đường lường trước được 5.353 s (86,1%);
- không lường trước được 783 s (12,6%).

Kéo dãn theo **tắc đường thật** khớp giờ trả trong 2 s cho mọi khách có phần do điều phối bằng 0: 1.452/1.452 trên 21 ô, và 2.305/2.305 trên cả 34 ô.

**Bảng bổ trợ** (Y, 21 ô):

| | không dự báo | lớp lời hứa |
|---|---|---|
| U | 9,72% | 3,90% |
| C-30 | 6,01% | 0,17% |

Luật của khóa luận và lớp lời hứa gỡ **hai nguyên nhân khác nhau**: trễ do điều phối, và trễ do đường lường trước được. Ở khung 17h, U vẫn trễ 6,76% mà lớp lời hứa không gỡ được, trong khi C-30 ở đó là 0%.

**Độ nhạy.**

| Biến thể | Y lớp lời hứa | ΔY | Kết quả A1–A3 |
|---|---|---|---|
| Nguồn "bỏ một ngày" | 0,17% | như nguồn chính | đạt cả ba |
| Nguồn có 15/11 | 0,17% | như nguồn chính | đạt cả ba |
| Nguồn bỏ 12/11 | 0,10% (15 ô) | −6,25 điểm %, khoảng [−9,28; −3,23] | **A1 trượt** (về cấu trúc) |
| Hiển thị hai phía | như nguồn chính | — | xem ghi chú |

- **Nguồn bỏ 12/11.** 12/11/2018 là ngày nghỉ liên bang bù cho Veterans Day, vì 11/11 là Chủ nhật, và hồ sơ tắc đường sáng ngày đó nhẹ hơn. Ngày này là ngày nguồn cho mọi dự báo của phân tích chính.
  - Khi bỏ nó, 14/11 mất dự báo, nên chỉ còn 2 buổi sáng, trong khi A1 đòi 3.
  - Cluster-t với df = 2 là [−18,9; +6,4], tức suy luận rất mong manh.
- **Hiển thị hai phía:** buổi chiều thêm dao động (0% → 0,86%) và rút ngắn lời hứa 8,2 s. Điều này ủng hộ hiển thị một phía.

**Tuyết** (đối chứng âm, 8 ô 15/11 14–16h và 15–17h, C-30):
- Y không dự báo 7,19%, lớp lời hứa 5,94%; chỉ gỡ được 17,4% (w14 9,2%, w15 24,8%).
- **92,5%** số giây trễ thuộc loại "không lường trước được": do điều phối 9 s, lường trước được 251 s, không lường trước được 3.186 s.
- Buổi sáng ngày thường thì ngược lại: 86,1% lường trước được. Hai loại này **tương ứng** với cách 49 CFR 37.131(f)(3)(ii) và Phụ lục D tách "lường trước được lúc xếp lịch" khỏi "không lường trước được".

## 3. Đọc kết quả Họ A theo góc thực tế

- **Bằng chứng ngoài mô phỏng chỉ gợi ý, chưa chứng minh.**
  - Via New York (Cohen và cộng sự 2022, dữ liệu thật và thử nghiệm thực địa): khách bị trễ nhiều so với giờ đã báo (ngưỡng 8–10 phút) về sau đi ít hơn và chi ít hơn.
  - Giao đồ ăn (Harter 2025, Mao 2025, Liang 2025; tôi chỉ đọc phần tóm tắt): trễ so với giờ đã báo đi kèm việc mua lại chậm hơn hoặc hủy nhiều hơn, và trễ có hại hơn đến sớm.
  - Chưa nguồn nào cho thấy đó là lý do chính khiến khách bỏ đi. Mọi ngưỡng trong các nguồn đều ở mức phút, không phải 60 s.
- **Trong mô phỏng** (một hệ số tắc đường chung toàn thành phố, đo từ dữ liệu thật), lớp lời hứa gần như xóa loại trễ so với lời hứa đầu: 6,01% → 0,17%, khung 7h 14,44% → 0,27%. Giá là lời hứa dài thêm khoảng nửa phút vào giờ cao điểm; giờ hiển thị ổn định hơn chứ không kém đi.
- **Phần lợi không chỉ do hứa dài hơn.** Đệm cố định cùng tổng thời gian chỉ xuống 1,94%. Nhưng phép so này (A3) mang nhãn "mong manh với suy luận ít cụm".
- **Không nói "giảm khiếu nại" hay "tăng doanh thu".** Mô phỏng không có hủy chuyến, và ngưỡng trong các hợp đồng thật là 10–15 phút, trong khi ở đây trễ > 300 s đã bằng 0 ở mọi nhánh có trần.

## 4. Họ B (phụ): đưa dự báo vào bộ điều phối (lớp bọc v1.3, K = 2; 16 ô khung 7h và 8h)

**Ba lần dừng.**
- C-30F và Mplus-30F ở `d20181116-s10-r1-w08`: FleetPy, chạy theo giờ đi thật, tính xe tới điểm đón muộn hơn hạn 1,67 s. Hạn này rơi vào bin 5, nơi tỉ số dự báo là 0,9953 < 1, tức bộ điều phối tưởng đường nhanh hơn thực tế 0,5%.
- C-30F ở `d20181114-s10-r3-w08`: trễ 2,69 s so với một hạn đón ở bin 6, nơi tỉ số là 1,007. Lần dừng này **chưa được chẩn đoán**.
- Đây là rủi ro tích hợp quan sát được (bộ điều phối có một bảng giờ đi tĩnh), không phải lỗi hạ tầng.

**Chuỗi M** (Mplus-30F so với Mplus-30; 15 ô mà Mplus-30F chạy xong).
- **Thất hứa của chính luật** (A > lời hứa đầu + 30 s): **ARTEFACT**.
  - Giảm 69,4%, giảm ở 6/6 ngày-khung; chênh −17,15 điểm %, khoảng 97,5% [−18,66; −14,92].
  - Khung 7h (8 ô): 39,0% → 11,0%. Khung 8h (7 ô): 8,3% → 3,7%.
- **Tỉ lệ quyết định bị ép:** **PERSISTS**.
  - Giảm 24,1%, sát ngưỡng 25%. Khoảng của chênh tương ứng mức giảm tương đối khoảng 11–41%, nên kết luận này nằm sát lằn ranh.
  - Khung 7h: 76,9% → 59,4%. Khung 8h (7 ô): 46,6% → 34,2%.

**Ý nghĩa cho H2 của khóa luận.**
- Phần lớn lời hứa bị phá của luật hạn chót đến từ **lời hứa ban đầu lạc quan**. Con số "M⁺ thất hứa 36,5% khung 7h" của T3.9 phải đọc là phụ thuộc vào cách hứa.
- **Sự thật đứng được:** khi đã có dự báo, Mplus-30F vẫn bị ép ở 47,6% quyết định (trung bình 15 ô chạy xong), còn C-30F là 0.
- H2a lặp lại: phục vụ C-30F − Mplus-30F là +0,07 khách mỗi ô, khoảng [0,00; +0,19].

**Chuỗi C.**
- B-C1 đạt **chỉ trên 14 ô chạy xong**: C-30F − C-30 = −0,14 khách mỗi ô, khoảng [−1,14; +0,24].
- Độ nhạy xấu nhất (tính ô bị dừng là 0 khách) trượt: −9,75, khoảng [−19,00; +0,08].

**Quy tắc triển khai chọn lớp lời hứa**, vì ba lý do:
- có 2 lần C-30F dừng;
- dao động 17,9% so với 1,4% của lớp lời hứa trên cùng 14 ô (khung 7h 17,1% → 29,2%);
- trễ > 60 s là 1,84% so với 0,25%.

**Kết luận phụ, trong phạm vi lớp bọc v1.3 và 16 ô:** dự báo nên nằm ở lớp lời hứa, không nên nằm trong bảng giờ đi tĩnh của bộ điều phối.

## 5. Dự đoán niêm phong: 17/19 đúng (2 có điều kiện); 7/9 ở các dự đoán có thể sai thật (PB, PS)

| # | Kết quả | # | Kết quả |
|---|---|---|---|
| PA1 | Đúng (0,17%; ΔY −5,84) | PB1 | **Sai.** Khung 7h bị ép 0,594 > 0,50; chuỗi M ra PERSISTS. Vế khung 8h (0,342 ∈ [0,05; 0,35]) thì đúng |
| PA2 | Đúng (0,27%; 14/11 giữ phần dư lớn nhất, 1,06%) | PB2 | Đúng (0,110 ≤ 0,15) |
| PA3 | Đúng (+0,08; ≤ +0,20 ở mỗi khung) | PB3 | Đúng trên 14 ô chạy xong (tăng ở 4/6 ngày-khung) |
| PA4 | Đúng (27,8 / 8,1 / 0,1 s; đón 6,5 s) | PB4 | Đúng, **có điều kiện**: −0,14 trên ô chạy xong, theo quy tắc viết sau khi đã thấy các lần dừng |
| PA5 | Đúng (1,74%; ΔVis −6,42) | PB5 | Đúng (lớp lời hứa) |
| PA6 | Đúng (4,24% so với 0,27%) | PB6 | Đúng, **có điều kiện**: 0 ở 14 lần chạy xong; 2 lần dừng không thể xác nhận |
| PA7 | Đúng (U 3,90%; U khung 17h 6,76%) | PB7 | Đúng (2,87% ≤ 4%) |
| PA8 | Đúng (2.305/2.305 trên 34 ô) | PB8 | Đúng ([0,00; +0,19]) |
| PA9 | Đúng (nguồn "bỏ một ngày" lệch 0,0 điểm %) | PC1 | Đúng. Đối chứng dương chạy trước niêm phong |
| | | PS1 | **Sai** (gỡ 17,4% > 15%; hai vế còn lại đúng) |

Dự đoán PA được viết sau khi đã xem trước dữ liệu, nên mang ít trọng lượng.

## 6. Giới hạn (phải nói khi trình bày)

- **Gần như tất định.**
  - Tắc đường trong mô phỏng là một hệ số chung cho cả thành phố. Kéo dãn theo tắc đường thật khớp giờ trả cho 100% khách có phần do điều phối bằng 0.
  - Kết quả Họ A vì vậy là "phần trễ giờ cao điểm đoán trước được từ 2–3 ngày trước, quy ra lời hứa cho từng khách". Đó là **mức trần** cho loại dự báo theo giờ trong ngày, không phải hiệu quả ngoài đời.
- **Đã xem trước.** Ô hiệu chỉnh trùng ngày-khung với ô kiểm tra.
- **Ít cụm.** 8 ngày-khung, 3 buổi sáng. A3 mang nhãn mong manh. Khi bỏ 12/11 (ngày nghỉ bù), A1 trượt và suy luận rất mong manh.
- **Lịch sử mỏng.** Một tuần dữ liệu, có một ngày nghỉ lễ làm nguồn.
- **Giả định của lớp lời hứa.**
  - Khách không phản ứng (không có hủy chuyến).
  - Kiểm khả thi đón vẫn dùng giờ không dự báo, nên 2,3% khách có lời hứa đón đầu tiên vượt hạn đón.
- **Họ B dùng lớp bọc v1.3.** Trên ô phát triển, v1.4 tầm nhìn 15 phút giảm dao động so với v1.3 (0,167 so với 0,224), nhưng v1.4 tầm nhìn 30 phút lại tăng (0,314).
- **Không tuyên bố mới.** Dự báo giờ đi theo giờ trong ngày, đệm lời hứa và hạn chót đều không mới. Đóng góp là **phép đo** và **công cụ chia trễ ba phần** trên tắc đường đo từ dữ liệu thật.

## 7. Kiểm độc lập và rà số liệu

- **Họ A** (`independent-A/RESULT.md`).
  - Mã riêng, tính từ bản ghi thô. 25/26 mục khớp, gồm k_w, Y, E, Vis, độ dài thêm, sổ chia trễ và ba khoảng bootstrap (tới ba chữ số thập phân).
  - Mục còn lại là phạm vi phép đếm kéo dãn theo tắc đường thật: 2.305 là trên 34 ô; trên 21 ô là 1.452.
  - **Không kiểm:** cluster-t, khoảng của ΔE, độ dài thêm của lời hứa đón, p90, MAE, trễ > 120 s, 34 lời hứa đón vượt hạn, các độ nhạy, và các nhánh M/V.
- **Họ B và tuyết** (`independent-B/RESULT.md`).
  - Mã riêng, tính từ 74 bản ghi thô. Mọi mục được hỏi đều khớp: ba lần dừng, chuỗi M kể cả khoảng, B-C1, dao động và trễ của C-30F, 0 non-normal, H2a dưới dự báo, các số tuyết và sổ chia trễ tuyết (8,6 / 251,5 / 3.185,9 s).
  - Agent còn chỉ ra thêm: vế khung 8h của PB1 đúng trên quần thể chuỗi M. Báo cáo này đã sửa theo đó (phụ lục A2.1).
- **Rà số liệu** (`review/NUMBER-TRACE-REVIEW.md`). Một agent người chấm truy mọi con số và tìm 5 lỗi chặn, không lỗi nào đổi kết luận đạt/trượt:
  1. quần thể khung 8h;
  2. lý do chấm PB1;
  3. nhãn cluster-t của A3;
  4. tài liệu `docs/18` thiếu lưu ý;
  5. một câu nói quá ở §3.

  Cả năm lỗi đã được sửa trong bản này, kèm phần lớn lỗi nhỏ.
