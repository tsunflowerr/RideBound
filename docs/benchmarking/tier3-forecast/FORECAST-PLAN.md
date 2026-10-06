# Hứa giờ có tính trước tắc đường: lớp lời hứa (Họ A) và đưa dự báo vào bộ điều phối (Họ B), chỉ tắc đường thật (THĂM DÒ, NIÊM PHONG TRƯỚC)

Viết ngày 2026-10-06, niêm phong bằng SHA-256 (`FORECAST-PLAN.sha256`). Kế hoạch được commit và push lên `research/tier3-no-worse` **trước
job đầu tiên** (kể cả các job cổng).

## 0. Ai quyết, và đã xem gì trước khi niêm phong

**Thẩm quyền.**
- Chủ nghiên cứu giao toàn quyền ngày 2026-10-06. Câu nguyên văn: "cho bạn toàn quyền chủ động thực hiện kế hoạch… các agents phải debate… có agents khác đóng vai reviewer".
- Thiết kế do một vòng tranh luận chốt. Có ba lập trường đề xuất độc lập rồi phản biện chéo (phương pháp, thực tế, hoài nghi), sau đó một trọng tài phán quyết.
  - Đề xuất và phản biện: `debate1-all-results-2026-10-06.json`.
  - Phán quyết: `debate1-verdict-2026-10-06.json`.
  - Dữ kiện chung: `EVIDENCE-PACK-2026-10-06.md`, `EVIDENCE-ADDENDUM-1-2026-10-06.md`, `EVIDENCE-ADDENDUM-2-2026-10-06.md`.
- Kế hoạch này theo phán quyết. Những điểm Claude (người điều phối) đổi được liệt kê ở §9.

**Đã xem trước khi niêm phong.** Các bản xem trước này làm Họ A **không còn mù** với dữ liệu. Họ A được gọi là "đo lường có đăng ký trước trên các ngày-khung đã được xem trước".
1. **Kết quả cũ.** Kết quả T3.8/T3.9 trên cùng 34 ô kiểm tra (`../tang3/t38/T38-REPORT.md`), và phân tích trải nghiệm khách sau sự kiện (`rider-experience-2026-10-05-report.txt`).
2. **Mức đoán trước được của tắc đường** trên mọi ngày, kể cả các ngày kiểm tra (`forecast-predictability-2026-10-06.txt`, `day-profiles-2026-10-06.txt`).
3. **Các lần chạy phát triển** trên ô 12/11 mẫu 1 khung 7h: v1.3 K=2, v1.4 (ba biến thể) và đối chứng tắt dự báo (`smoke-breakdown-*.txt`, `compare-v13-control-2026-10-06.txt`).
4. **Kiểm phép kéo dãn với tắc đường thật** trên 8 ô phát triển (`validate-stretch-2026-10-06.txt`): 658/660 khách có phần do điều phối bằng 0, và với cả 658 khách sai lệch ≤ 1,00 s.
5. **Chạy thử toàn bộ Họ A trên 8 ô phát triển**, nguồn "bỏ một ngày" (`dev-family-a-loo-2026-10-06-report.txt`):
   - C-30: trễ > 60 s từ 7,25% xuống 0%; đệm cùng giá 1,80%; dao động 8,78% → 0,43%; giờ hứa dài thêm 21,2 s.
   - 27 lời hứa đón hiển thị sau cửa sổ đón.
6. **Tính toán chưa lưu của các agent tranh luận** trên 15 ô hiệu chỉnh T3.8. Các ô này dùng chung 12/13 ngày-khung với tập kiểm tra. Các số đó **không** được dùng làm căn cứ ở đây.
7. **Bảng tỉ số dự báo** (`configs/r-table.json`). Bảng này chỉ dựng từ đầu vào.

**Chưa tính** bất kỳ kết quả có dự báo nào trên ô kiểm tra. `forecast_family_a.py` từ chối tập kiểm tra khi chưa có `FORECAST-PLAN.sha256`.

## 1. Câu hỏi

- **Q-A.** Lời hứa giờ trả hiển thị cho khách có tính trước tắc đường theo giờ trong ngày có giảm số khách đến trễ hơn lời hứa không?
  - Với cái giá nào: lời hứa dài hơn, khách đến sớm.
  - Có làm giờ hiển thị dao động hơn không.
  - Lợi đến từ **thông tin giờ trong ngày** hay chỉ vì hứa dài hơn.
- **Q-B1.** Luật hạn chót bị ép nhiều có phải chỉ vì lời hứa ban đầu quá lạc quan không? Đây là phép thử độ bền cho kết luận H2 của chính khóa luận.
- **Q-B2.** Đưa dự báo vào bộ điều phối, vốn chỉ có một bảng giờ đi tĩnh, tốn bao nhiêu khách và bao nhiêu dao động, so với chỉ dùng lớp lời hứa?
- **Q-S.** Trận tuyết 15/11 (khung 14h và 15h) là đối chứng âm, tức tắc đường **không lường trước được**:
  - lớp lời hứa gỡ được bao nhiêu;
  - bao nhiêu giây trễ thuộc loại "không lường trước được".

## 2. Can thiệp

- **Hồ sơ dự báo m(b)** (`display_layer.py`, `promise_stretch.py`).
  - Lấy trung bình hệ số tắc đường tuyệt đối theo khung 15 phút của các **ngày trước đó cùng loại**. Không bao giờ dùng ngày 15/11 (sự cố) hay chính ngày đang xét.
  - Chỉ bật khi có ≥ 2 ngày nguồn: 14/11 ← 12, 13; 15/11 và 16/11 ← 12, 13, 14. Các ngày 12/11, 13/11, 17/11, 18/11 tắt.
  - Neo vào mức hiện tại đo được; các ngày khác chỉ cho **hình dạng tăng trưởng**. Giữ nguyên mức sau bin 8, như thế giới mô phỏng.
- **Họ A, lớp lời hứa (chính, không chạy mô phỏng mới).**
  - Mỗi lời hứa và mỗi lần sửa của bộ điều phối, công bố lúc t với giờ p, được hiển thị là D = max(p, S(t, p − t)).
  - S kéo dãn phần thời gian còn lại theo đà tăng dự báo sau khung hiện tại. Hiển thị một phía: không bao giờ hứa sớm hơn.
  - Đánh giá bằng phát lại chính xác 170 lần chạy kiểm tra T3.8.
- **Họ B, đưa vào bộ điều phối (phụ).**
  - Lớp bọc v1.3 giữ nguyên, K = 2, cùng nguồn. Cấu hình ở `configs/forecast-k2-<ngày>.json`; tỉ số kỳ vọng ở `configs/r-table.json`.
  - Tỉ số < 1 chỉ có ở khung 8h bin 5 (thấp nhất 0,9882) và được giữ, không chặn sàn.

## 3. Tập ô

- **Họ A, quần thể chính:** 21 ô kiểm tra T3.8 ngày thường có dự báo bật, gồm 8 ngày-khung trong 3 tầng.
  - w07: 14/11 r3 r4; 15/11 r1 r2 r4; 16/11 r1 r2 r4.
  - w08: cùng các ô như w07.
  - w17: 14/11 r3 r4; 16/11 r1 r2 r4.
  - Báo kèm trung bình triển khai trên 34 ô (ô cuối tuần có chênh 0 theo cấu tạo).
- **Họ B:** 16 ô ngày thường khung 07 và 08 (6 ngày-khung). Khung 17h bị loại vì tỉ số ở đó ≈ 1, tức không khác cách không dự báo.
- **Phát triển:** chỉ task B (12–13/11 khung 7h). 15 ô hiệu chỉnh **không** được dùng cho lựa chọn nào.
- **Tuyết:** 8 ô 15/11 khung 14–16h và 15–17h, mẫu r1–r4 (`snow/prepare-snow-2026-10-06.log`, 0 vấn đề), chạy C-30 tắt dự báo.

## 4. Quy tắc quyết định

### Họ A (chính; C-30; 21 ô)

**Đo cho mỗi khách được phục vụ.**
- t0, P0 = thời điểm và giờ của lời hứa giờ trả đầu tiên.
- A = lúc xuống xe. D0 = lời hứa đầu được hiển thị.
- Y = tỉ lệ A − D0 > 60 s (bản không dự báo dùng P0).
- E = tỉ lệ A − D0 < −60 s.
- Vis = tỉ lệ khách có tổng |D_i − D_{i−1}| > 60 s (bản không dự báo là visible60).

**Ước lượng.** Trung bình không trọng số trên 21 ô của chênh cặp.

**Khoảng ghi nhận.** Bootstrap theo cụm ngày-khung, phân tầng theo khung.
- Trọng số tầng 8/21, 8/21, 5/21.
- B = 10.000, `random.Random(20261007)`, bách phân vị tại chỉ số 249 và 9750.

**Đệm cùng giá (A3).**
- k_w = Σ(D0 − P0) / Σ(P0 − t0) trên mọi khách C-30 ngày thường của khung w.
- k_w **chỉ tính từ lời hứa** (mã có assert) và được ghi ra tệp trước khi đọc giờ trả.
- Lời hứa đệm = P0 + k_w (P0 − t0).

**Chuỗi cố định** (bước sau chỉ xét khi bước trước đạt):
- **A1 (độ chính xác):** đạt khi có đủ:
  - ΔY ≤ −2,0 điểm %;
  - UB95(ΔY) < 0;
  - ΔY < 0 ở cả 3 ngày-khung ngày thường khung 7h;
  - ΔE ≤ +1,0 điểm %.
- **A2 (không thêm dao động):** UB95(ΔVis) ≤ +1,0 điểm %.
- **A3 (thông tin, không chỉ đệm):** UB95[Y(lớp lời hứa) − Y(đệm theo khung)] < 0.

**Nhãn.**
- Mọi kết quả Họ A mang nhãn "đo lường có đăng ký trước trên các ngày-khung đã được xem trước".
- Nếu bỏ một ngày (14, 15 hoặc 16/11) mà ΔY ≥ 0, thêm nhãn "do một ngày quyết định".
- Nếu khoảng cluster-t (df = 5) có cận trên ≥ 0, thêm nhãn "mong manh với suy luận ít cụm".

### Họ B (phụ; 16 ô)

**Điều kiện dùng lại lần chạy cũ.** Chỉ dùng lại lần chạy không dự báo của T3.8 khi cổng E1 và E2 đạt.

**Khoảng.** Bootstrap phân tầng w07/w08, mỗi tầng 3 ngày-khung, cùng hạt giống, khoảng 97,5% (chỉ số 124 và 9875).

**Chuỗi M** (`Mplus-30F` − `Mplus-30`, tỉ lệ quyết định bị ép; tỉ lệ thất hứa A > lời hứa đầu của chính nó + 30 s xét cùng cách):
- **ARTEFACT** khi có đủ ba điều: giảm tương đối ≥ 50%, giảm ở 6/6 ngày-khung, UB97,5 < 0.
- **PARTIAL** khi giảm ≥ 25% và UB < 0.
- **INCONCLUSIVE** khi giảm ≥ 25% và UB ≥ 0.
- **PERSISTS** khi giảm < 25%.

**Chuỗi C.**
- **B-C1** đạt khi LB97,5[phục vụ(C-30F) − phục vụ(C-30)] ≥ −2 khách mỗi ô.
- **Quy tắc triển khai.** Khuyên đưa dự báo vào bộ điều phối chỉ khi đủ cả ba điều:
  - B-C1 đạt;
  - visible60(C-30F) ≤ Vis(C-30 lớp lời hứa) + 1 điểm %;
  - late60(C-30F) ≤ Y(C-30 lớp lời hứa) + 1 điểm %.

  Ngược lại thì khuyên dùng lớp lời hứa.

Mọi thứ khác chỉ là mô tả, có khoảng tin cậy, không dùng ngôn ngữ "có ý nghĩa thống kê".

## 5. Thước đo phụ (mô tả)

- **Giá khách thấy.**
  - Độ dài thêm của lời hứa trả và lời hứa đón: trung bình và p90.
  - Số lời hứa đón hiển thị sau cửa sổ đón. Đây là mâu thuẫn vận hành, vì phép kiểm khả thi vẫn dùng giờ không dự báo.
- **Độ chính xác.** MAE (so với 24,24 s của Via), độ lệch, late30/120/300, early60.
- **Độ ổn định.** visible60, visible120.
- **Sổ chia trễ ba phần** cho mỗi khách trễ so với P0: do điều phối Σ(p − e); do đường lường trước được (D0 − P0); không lường trước được (phần còn lại). Đối chiếu với 49 CFR 37.131(f)(3)(ii) và Phụ lục D.
- **Bảng {U, C-30} × {không dự báo, lớp lời hứa}**, và lớp lời hứa cho Mplus-30, Mplus-60, V-30.
- **Họ B:** phục vụ, chờ, đi, late60 trên khách chung, dao động, số giây dời sau và dời sớm, mức trùng hành động, C-30F so với Mplus-30F (H2a dưới dự báo).
- **Độ nhạy:** nguồn "bỏ một ngày", nguồn có 15/11, nguồn bỏ 12/11, hiển thị hai phía, đệm toàn thành phố.
- **Tiền:** chỉ minh họa (5 USD Via, 10 USD Access-A-Ride), đặt cạnh độ chênh thang đo. Không phải kết luận.

## 6. Dự đoán (niêm phong; sai thì báo sai)

Dự đoán Họ A gần như chắc đúng vì đã xem trước, nên mang ít trọng lượng bằng chứng. Dự đoán Họ B và dự đoán tuyết mới là những dự đoán có thể sai thật.

| # | Dự đoán |
|---|---|
| PA1 | A1 đạt; Y lớp lời hứa của C-30 (21 ô) ≤ 1,5% (không dự báo 6,01%), tức ΔY ≤ −4,5 điểm % |
| PA2 | Khung 7h ngày thường: Y lớp lời hứa ≤ 3,0% (không dự báo 14,44%), và 14/11 khung 7h giữ phần dư lớn nhất |
| PA3 | E tăng ≤ +0,5 điểm % trên 21 ô và ≤ +1,0 điểm % ở mỗi khung |
| PA4 | Lời hứa trả dài thêm trung bình: khung 7h 15–35 s, khung 8h 3–12 s, khung 17h ≤ 2 s; lời hứa đón khung 7h ≤ 10 s |
| PA5 | A2 đạt và dao động giảm: Vis lớp lời hứa khung 7h ≤ 8% (không dự báo 17,07%); ΔVis trên 21 ô ≤ −2 điểm % |
| PA6 | A3 đạt; ở khung 7h, đệm cùng giá để lại Y ít nhất gấp 2 lớp lời hứa và cao hơn ít nhất 2 điểm % |
| PA7 | Bổ trợ: Y lớp lời hứa của U trên 21 ô vẫn ≥ 3,0% trong khi C-30 ≤ 1,5%; U khung 17h vẫn ≥ 5% |
| PA8 | ≥ 99% khách C-30 có phần do điều phối bằng 0 thỏa \|A − S_đúng(t0, P0 − t0)\| ≤ 2 s |
| PA9 | Nguồn "bỏ một ngày" và nguồn chính lệch nhau ≤ 1,0 điểm % ở Y lớp lời hứa của C-30 trên 21 ô |
| PB1 | Tỉ lệ bị ép của Mplus-30F trong [0,10; 0,50] ở khung 7h (không dự báo 0,769) và [0,05; 0,35] ở khung 8h (0,456); chuỗi M ra PARTIAL hoặc ARTEFACT (tin cậy thấp) |
| PB2 | Mplus-30F thất hứa (A > lời hứa đầu + 30 s) ở khung 7h ≤ 0,15 (không dự báo 0,390) |
| PB3 | Đưa vào bộ điều phối làm tăng dao động: visible60 của C-30F trên 16 ô cao hơn không dự báo, ở ≥ 4/6 ngày-khung |
| PB4 | B-C1 đạt; phục vụ C-30F − C-30 trong [−2,0; 0] mỗi ô (tin cậy thấp-vừa) |
| PB5 | Quy tắc triển khai chọn lớp lời hứa |
| PB6 | 0 quyết định non-normal ở cả 16 lần chạy C-30F |
| PB7 | C-30F khung 7h: late60 so với lời hứa đầu của chính nó ≤ 4% (không dự báo 14,44%) |
| PB8 | Khoảng 95% của phục vụ C-30F − Mplus-30F nằm trong [−2; +2] mỗi ô (H2a lặp lại dưới dự báo) |
| PC1 | E1 và E2: 0 khác biệt về quyết định có thời điểm, bảng v/e/p, tập khách, giờ trả và khung dữ liệu; đối chứng dương (U so với C-30) khác |
| PS1 | Tuyết: late60 của C-30 không dự báo trung bình ô trong [3%; 15%]; lớp lời hứa gỡ ≤ 15% phần đó; ≥ 90% số giây trễ thuộc loại "không lường trước được" |

## 7. Kế hoạch chạy (`run_forecast.py`; lớp bọc v1.3 không đổi; output `C:\RideBoundData\research\tier3-forecast-v1`)

1. **Cổng, 3 job, chạy trước mọi job kiểm tra.**
   - S1: Mplus-30F chạy thử trên ô phát triển. Chỉ kiểm triển khai.
   - E1: Mplus-30 tắt dự báo trên ô `d20181115-s10-r4-w08`.
   - E2: C-30 K = 1 (giả, tỉ số luôn 1) trên ô `d20181115-s10-r2-w07`.
   - Hai ô chọn bằng `random.Random(20261008)` (`make-forecast-configs-2026-10-06.log`).
   - `check_gates.py` so E1/E2 với lần chạy T3.8 bằng `compare_equivalence.py`, rồi ghi `GATES-PASSED.txt`.
   - Nếu trượt: chạy lại 16 lần không dự báo (C-30, Mplus-30 × 8 ô khung 7h) và chỉ giữ Họ B ở khung 7h.
2. **Tầng 1, 16 job:** C-30F, Mplus-30F × 8 ô ngày thường khung 7h.
3. **Tầng 2, 16 job:** C-30F, Mplus-30F × 8 ô ngày thường khung 8h.
4. **Tầng tuyết, 8 job:** C-30 tắt dự báo × 8 ô tuyết.
5. **Dự phòng:** tối đa 3 lần chạy lại vì lỗi hạ tầng (máy ngủ, quá hạn phản hồi, quá CPU), mỗi lần vào `<job>-rerun1` và ghi phụ lục.

Thứ tự trong mỗi tầng xáo bằng `random.Random(20261008)`. Mỗi job phải qua ba kiểm:
- cổng đĩa: C: còn trống − 51.200 MiB ≥ 410 MiB;
- đang cắm sạc;
- không có tệp STOP.

Tổng cộng 43 job cam kết cộng 3 dự phòng, ≈ 3,5 GiB. Ổ C: còn 66.095 MiB lúc lập kế hoạch.

## 8. Kiểm tra trước niêm phong (đã làm)

- **Lớp bọc v1.3:**
  - kiểm thử 23/23 và 8/8 đột biến bị bắt;
  - tắt dự báo tái tạo v1.2 hoàn toàn trên ô phát triển (`equiv-dev-control-off-2026-10-06-c.txt`, EQUIVALENT).
- **Lớp lời hứa và phép kéo dãn:**
  - kiểm thử 23/23, 8/8 đột biến bị bắt (`test_display_layer.py`, `mutate_display_layer.py`);
  - phép kéo dãn với tắc đường thật đúng cho 658/658 khách phát triển.
- **Suy luận theo cụm:** 7/7 phép kiểm tổng hợp (`test_family_a_stats.py`).
- **Phép so tương đương:** tự kiểm đạt (đột biến thời điểm quyết định và giờ trả đều bị bắt; U khác C-30); xem `equiv-selftest-2026-10-06-c.txt`.
  - Phát hiện khi làm: thứ tự `actions` và `breachId` có muối theo lần chạy, nên được chuẩn hóa. Ghi trong mã.
- **Chạy thử toàn quy trình** Họ A và Họ B trên dữ liệu phát triển: `dev-family-a-loo-2026-10-06-report.txt` và `dev-family-b-dryrun-2026-10-06-report.txt`.
- **Đầu vào mọi nhóm job:** 0 vấn đề (`run_forecast.py plan`). Tầng 1–2 bị khóa cho tới khi cổng đạt.
- **Sau mỗi tầng:** `check_runs.py` kiểm dòng tỉ số theo bảng niêm phong (sai số 1e−6), dòng `[world]` trùng lần chạy không dự báo, và 0 quyết định non-normal cho C-30F.

## 9. Điểm Claude quyết khác hoặc thêm so với phán quyết (theo ủy quyền)

1. **Tuyết là tầng cam kết**, chạy sau tầng 2, không còn là tùy chọn. Lý do: chủ nghiên cứu đã đưa trận tuyết vào hướng đi.
   - Khung trình bày theo phán quyết: đối chứng âm "không lường trước được", **không** phải bài thử khắc nghiệt. Mức tăng trong khung tuyết (+40–46%) nhẹ hơn giờ cao điểm sáng ngày thường (+47–66%), vì thế giới mô phỏng tính tương đối so với đầu khung.
2. **Tầng thế giới có nhiễu không chạy trong kế hoạch này.** Độ lớn nhiễu chưa được hiệu chỉnh, nên mọi kết quả sẽ phụ thuộc một tham số tùy ý. Lời phản bác "kết quả gần như tất định" được trả lời bằng cách báo sai số dự báo theo từng ngày-khung và ghi rõ giới hạn. Nếu chạy thêm thì phải có phụ lục mới.
3. **Mức sàn ổ đĩa** đọc theo GiB (51.200 MiB). Đây là cách hiểu chặt hơn.
4. **Chưa kiểm:** 12/11/2018 có phải ngày nghỉ bù Veterans Day không. Vì vậy có độ nhạy "bỏ 12/11".

## 10. Mối đe dọa chính (tóm tắt phán quyết F7)

- **Gần như tất định.**
  - Mô phỏng dùng một hệ số tắc đường chung cho cả thành phố, và thế giới cùng dự báo lấy từ một nguồn hệ số.
  - Vì vậy kết quả được trình bày là "phần trễ giờ cao điểm đoán trước được, quy ra lời hứa cho từng khách". Đó là **mức trần** cho loại dự báo theo giờ trong ngày, không phải hiệu quả ngoài đời.
- **Đã xem trước.** Ô hiệu chỉnh trùng ngày-khung với ô kiểm tra (ICC khung 7h 0,917).
- **Ít cụm.** Chỉ 8 ngày-khung, 3 sáng ngày thường.
- **Lịch sử mỏng.** Một tuần dữ liệu; 14/11 chỉ dựa vào 12 và 13/11.
- **Giả định của lớp lời hứa.**
  - Khách không phản ứng (mô phỏng không có hủy chuyến).
  - Kiểm khả thi đón vẫn dùng giờ không dự báo.
  - Thời gian dừng đỗ cũng bị kéo dãn.
- **Thang đo.** 60 s ở đây, so với 10–15 phút trong các hợp đồng thật.
- **Mọi lựa chọn của người phân tích** (hiển thị một phía, nguồn trước đó, bỏ 15/11, tối thiểu 2 ngày) đều có độ nhạy, và đều được chốt sau khi đã xem dữ liệu phát triển.

## 11. Sau khi chạy

- Kiểm từng tầng bằng `check_runs.py`.
- Chạy `forecast_family_a.py test causal-no15`, các độ nhạy và `forecast_family_b.py`.
- Một agent viết mã riêng tính lại từ bản ghi thô. Một agent khác truy từng con số trong báo cáo về tệp output.
- Viết báo cáo `FORECAST-REPORT.md`, gồm cả dự đoán đúng và sai.
- Cập nhật docs/18 và docs/19 trên nhánh nghiên cứu.
- Mọi thay đổi so với kế hoạch ghi vào `FORECAST-AMENDMENTS.md`.
