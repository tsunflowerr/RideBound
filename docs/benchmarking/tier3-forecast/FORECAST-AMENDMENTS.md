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
