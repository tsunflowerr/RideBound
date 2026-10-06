# FORECAST-PLAN: phụ lục sau niêm phong (kế hoạch SHA-256 a5524912…7337, push 33d8071)

## A1. Ba lần chạy Họ B dừng vì "kế hoạch không khả thi"; xử lý ô bị dừng; sửa phép kiểm dòng tắc đường

Ghi lúc 2026-10-06 khoảng 10:40 giờ máy, **trước** khi chạy `forecast_family_b.py` trên tập kiểm tra.

**Chuyện đã xảy ra.**
- 3/32 lần chạy Họ B có `returncode=1` với lỗi `RBWP7_FLEETPY_PLAN_INFEASIBLE`. Thư mục có `failure-00.json` nhưng không có `summary.json`:
  - `C-30F-d20181116-s10-r1-w08`;
  - `Mplus-30F-d20181116-s10-r1-w08`;
  - `C-30F-d20181114-s10-r3-w08`.
- 29 lần còn lại `status: pass`.
- Tầng tuyết 8/8 và các cổng 3/3 đạt.

**Chẩn đoán**, từ `failure-00.json` của `C-30F-d20181116-s10-r1-w08`:
- FleetPy chạy theo giờ đi đo thật. Nó tính xe tới điểm đón lúc 5.176,67 s, trong khi giờ đón muộn nhất là 5.175 s, nên FleetPy từ chối kế hoạch.
- Thời điểm này thuộc bin 5 của khung 8h. Ở đó tỉ số dự báo niêm phong nhỏ hơn 1: 0,995339 cho 15–16/11 và 0,988249 cho 14/11 (`configs/r-table.json`; kế hoạch §2 đã nêu).
- Vậy bộ điều phối tưởng đường nhanh hơn thực tế một chút và nhận một lượt đón mà xe không kịp làm đúng hạn.
- Đây không phải lỗi hạ tầng và sẽ lặp lại nếu chạy lại, nên theo kế hoạch §7 **không chạy lại**.

**Đã thấy trước khi viết quy tắc dưới đây.**
- Trạng thái các job trong output của driver.
- Đuôi tệp `check-runs-*.log`, trong đó có số quyết định non-normal của vài lần chạy Mplus-30F (344, 218, 295 quyết định). Đây là số đếm, không phải tỉ lệ.
- Chưa tính bất kỳ thước đo nào của Họ B: tỉ lệ bị ép, tỉ lệ thất hứa, số khách, dao động.

**Quy tắc xử lý ô bị dừng.** Kế hoạch chưa quy định trường hợp này; nay quy định:
1. Số lần dừng theo từng nhánh là **một kết quả** của Họ B, được báo riêng.
2. **Quy tắc triển khai:** chỉ khuyên đưa dự báo vào bộ điều phối khi **không có** lần chạy C-30F nào dừng. Một tích hợp có thể làm hệ thống dừng thì không thể được khuyên dùng. Ba điều kiện cũ vẫn giữ.
3. Chuỗi M tính trên các ô mà Mplus-30F chạy xong (15/16). B-C1 và các so sánh của C-30F tính trên các ô mà C-30F chạy xong (14/16). Cả hai được ghi nhãn "chỉ các ô chạy xong".
4. Độ nhạy xấu nhất cho B-C1: tính ô bị dừng là phục vụ 0 khách.

**Sửa phép kiểm dòng tắc đường** (`check_runs.py` → `check_runs_v2.py`).
- Với ô 16/11 mẫu 4 khung 8h, log của lần chạy không dự báo có thêm một dòng `[world] t= 9000s bin=10 … factor=[1.2320, 1.2320]`. Hệ số này là mức giữ nguyên sau khung, và dòng đó có vì lần chạy cũ kéo dài hơn.
- Các dòng bin 0–9 trùng khớp.
- Phép kiểm mới so phần chung (các bin có ở cả hai log) và yêu cầu mọi dòng thừa chỉ là mức giữ nguyên (cùng hệ số với dòng bin 8).

## A2. Corrections after the number-trace review (2026-10-06, after all results; review/NUMBER-TRACE-REVIEW.md)

Written after every result was known; nothing here changes a pass/fail verdict or a sealed rule. Disclosed for honesty.
1. Population error in a descriptive line: forecast_family_b.py's per-window summary (changed by the A1 edit) used
   cells where BOTH forecast arms completed, so its w08 Mplus line covered 6 cells. On the chain-M population (7 w08 cells)
   Mplus-30 forced is 0.466 -> 0.342 and own-break 0.083 -> 0.037 (report-extras/report-extras-2026-10-06.txt). The report
   uses these. PB1's w08 clause therefore HOLDS (0.342 in [0.05, 0.35]); PB1 stays NOT HELD (w07 0.594 > 0.50; verdict
   PERSISTS).
2. Code vs plan text on the cluster-t label: FORECAST-PLAN.md §4 attaches "mong manh với suy luận ít cụm" whenever a
   stratified cluster-t interval has an upper bound >= 0; forecast_family_a.py checked that only for dY. The A3 comparison
   (display minus equal-price pad) has cluster-t [-3.620, +0.071] pp, so A3 carries that label. A1 and A2 do not
   (dY [-10.542, -1.146], dVis [-9.897, -2.939]).
3. Scripts written or changed after the seal (all implement sealed items; none changes a rule):
   family_a_two_sided.py (two-sided display sensitivity, §5), family_snow.py (snow tier, §3/§6 PS1),
   report-extras/report_extras.py (numbers quoted in the report but not printed by the main scripts), and
   forecast_family_b.py (changed in df7f893 for A1; its sealed hash no longer matches).
4. The second stop (C-30F d20181114-s10-r3-w08: FleetPy ETA 2.69 s past a latest pickup in bin 6, where the forecast
   ratio is 1.007) is NOT diagnosed. Only the 16/11 r1 w08 stop (both arms, 1.67 s, bin 5, ratio 0.9953) is.
5. Calendar fact: 2018-11-11 was a Sunday (report-extras), so 2018-11-12 was the observed U.S. federal Veterans Day
   holiday (Sunday holidays are observed on the Monday under the federal holiday rule; rule cited from general knowledge,
   not from a fetched source). 12/11 is a source day of every forecast in the main analysis; the causal-no12 sensitivity
   (A1 fails structurally, fragile inference) is the relevant check.
