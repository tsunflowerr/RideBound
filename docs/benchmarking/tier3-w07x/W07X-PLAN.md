# Việc w07x: tìm vùng mức hứa mà luật của khóa luận và hạn chót có thể khác nhau về số khách (THĂM DÒ)

Trạng thái: viết 2026-10-01, niêm phong bằng SHA-256 (`W07X-PLAN.sha256`), rồi commit và push lên nhánh nghiên cứu
`research/tier3-no-worse` **trước job đầu tiên**, để có mốc thời gian do bên thứ ba (máy chủ git) giữ.

## 0. Ghi rõ trước: vùng mức hứa được chọn sau khi đã xem kết quả

- Vùng 90–240 s được chọn **sau khi** đã thấy kết quả Việc B, trong đó chỉ mức 120 s cho quyết định lệch (2/8 ô). Vì vậy đây
  là thăm dò. Mọi mức đều được báo, không chọn lọc mức nào.
- Không dùng panel A. Không bắt đầu T3.8/T3.9. Không sửa luận văn trước khi chủ nghiên cứu đồng ý.

## 1. Bối cảnh, đã kiểm lại từ tệp gốc ngày 2026-10-01

- Việc B (`../w07/analysis/full-2026-09-30-report.txt`, `-jobs.tsv`), 8 ô khung 7:00–9:00, chỉ tắc đường thật, luật phục hồi (b):
  `C-x` và `Mplus-x` cùng từng quyết định ở 8/8 ô với x = 30, 60, 300 s; ở 120 s lệch 2/8 ô; phục vụ 666 so với 664 (lệch +1
  ở ô 12/11 mẫu 1 và 13/11 mẫu 1).
- Số cách ghép bị chặn (`M7_gatePrunes`, cộng 8 ô): `C` 221 / 186 / 119 / 85; `Mplus` 227 / 192 / 129 / 85 (30 / 60 / 120 / 300 s).
- Phần trễ do hệ thống lớn nhất của một khách (`M4_maxC_ms`) dưới `C-30`: tối đa 10,05 s trên 8 ô.
- Mỗi lần dời giờ do hệ thống có trung vị 115,6 s, nhưng đo trên cấu hình không cam kết B1 của panel tĩnh (luận văn
  `08-chuong4.tex:895`; kế hoạch hợp nhất T1.9), **không** phải trên các ô 7:00–9:00.
- Trôi dạt lớn nhất của một khách trong mỗi ô dưới `C-30` (tổng \|x\| giờ trả): 94–204 s (`../calib/h3-estimate-2026-09-30.txt`).
- Giả thuyết cần thử (của chủ nghiên cứu): hai luật chỉ khác nhau khi mức hứa ngang cỡ một lần dời giờ và ngang phần trễ do
  đường, khoảng 90–240 s.

## 2. Thiết kế

| Mục | Giá trị |
|---|---|
| Ô | 8 ô của Việc B: `d2018111{2,3}-s10-r{1..4}-w07`, fixture `C:\RideBoundData\research\fixtures-w07-v1`, driver `../w07/drivers/` |
| Thế giới | chỉ ngày đo thật của ô (`../w07/worlds/W5-<ngày>-w07.json`), wrapper v1.2 (`../world/preflight_world_v1_2.py`) |
| Luật phục hồi | (b): Runner `C:\RideBoundData\research\runner-tier3-v2` (tree `ac251ef3…cff3f`) + `../configs/wp4-tier3-v2.json` |
| Nhánh mới (13) | `C-x`, `Mplus-x` với x ∈ {90, 150, 180, 240} s; `V-x` với x ∈ {90, 120, 150, 180, 240} s. Cấu hình ở `configs/`, sinh bằng `make_configs_w07x.py` |
| Chứng minh cấu hình | `make_configs_w07x.log`: 19/19 tệp cấu hình cũ dựng lại trùng từng byte; mỗi cặp `C-x`/`Mplus-x` chỉ khác đúng hai khóa (`dropEtaDeadlineSlackMs`, `limits[drop_eta_total_ms].hardLimit`); mỗi `V-x` chỉ khác `C-x` ở `budgetBasis`; `V-120` mới trùng byte với `../configs/V-120.json` |
| Dùng lại | `U`, `C-120`, `Mplus-120`, `C-300`, `Mplus-300` và (để so) `C-30/60`, `Mplus-30/60` từ `C:\RideBoundData\research\tier3-w07-v1`: cùng lệnh, cùng Runner, WP4, wrapper, thế giới, fixture, driver, inventory; cấu hình trùng byte với bản dựng lại. Không chạy lại |
| Chạy | `run_w07x.py`: 104 job = 13 nhánh × 8 ô; cwd `E:\Code\RideBound-deadline` (không sửa), `--expected-repository-inventory-sha256 0da63eaf…4cbe`; output **mới** `C:\RideBoundData\research\tier3-w07x-v1`; log `logs/` mode `x`; công tắc `STOP`; nhãn `<job>-w07x`; chỉ khi cắm sạc (chờ tối đa 20 phút nếu đọc nguồn điện sai) |
| Ước tính | ~5 giờ (Việc B: 176 s mỗi job với 6 luồng); ~8,7 GB; ổ C: còn 91,9 GB trước khi chạy, ~83 GB sau |

## 3. Thước đo (cho mỗi mức x, cho mọi nhánh; như Việc B)

- Số khách hoàn tất `U` / `C` / `Mplus` / `V`, từng ô và tổng 8 ô.
- Số ô mà `C-x` và `Mplus-x` (và `V-x` với `C-x`) có cùng chuỗi hành động đội xe (`pilot_analyze.fleet_decisions`).
- Số cách ghép bị chặn (`M7_gatePrunes`); phần trễ do hệ thống lớn nhất (`M4_maxC_ms`).
- Tỉ lệ thất hứa theo thước của chính mỗi luật: `C` (C_r > x), `Mplus` (A > x), `V` (V_r > x, V_r = tổng \|z\| giờ trả); **và** theo
  cùng thước hạn chót (A > x) cho cả ba.
- Tỉ lệ quyết định bị ép; E2 (tỉ lệ khách có V_r > 60 s).
- **Cơ chế (mô tả):** ở mỗi ô mà `C-x` và `Mplus-x` lệch, tìm quyết định đầu tiên khác nhau; tìm cách ghép mà một luật cho
  còn luật kia chặn (mã chặn, khách bị ảnh hưởng, phần trễ do đường và do hệ thống của khách đó lúc ấy). Theo mẫu
  `../sweep/sweep_mechanism.py`.
- Kiểm hợp lệ trước khi đọc: 104/104 `status: pass` (hoặc phân loại lỗi theo `failure-00.json`); audit v/e/p `ok`; log thế giới
  như Việc B (dòng v1.2, kiểm ngày, mốc đúng `bin × 900 s`, hệ số khớp tệp hệ số); cùng hash kịch bản mỗi ô; cùng inventory;
  `policyConfigurationHash` trong `initializeRun` giống nhau giữa các ô của cùng một nhánh và khác nhau giữa các nhánh.

## 4. Dự đoán (viết trước khi chạy; sai thì báo sai)

| # | Dự đoán | Cơ sở |
|---|---|---|
| R1 | **Bất biến.** 0 quyết định non-normal ở cả 32 job `C-x` | Định lý 4; 0/32 ở Việc B |
| R2 | Ở **mỗi** x ∈ {90, 150, 180, 240}, `C-x` và `Mplus-x` lệch chuỗi hành động ở **ít nhất 1/8** và **nhiều nhất 4/8** ô | 120 s: 2/8; 60 và 300 s: 0/8 |
| R3 | **Dự đoán rỗng về số khách.** Ở mỗi x ∈ {90, 150, 180, 240}: \|tổng `C-x` − tổng `Mplus-x`\| ≤ 2 khách trên 8 ô, và `C-x` > `Mplus-x` ở ≤ 2/8 ô. Tức tiêu chí "C phục vụ nhiều hơn" (tổng chênh ≥ 3 khách **và** ở ≥ 3 ô) **không** đạt ở mức nào | 120 s: +2 khách ở 2 ô; với (b), `M⁺` bám sát `C` (Việc A, Việc B, quét no-worse) |
| R4 | Ở mỗi x ∈ {90, 120, 150, 180, 240}, tỉ lệ quyết định bị ép (trung bình 8 ô) của `V-x` **lớn hơn** của `Mplus-x` | `V` cộng dồn \|z\| cả hai chiều, kể cả dao động nhỏ do thực thi; 30 s: 63,2% so với 58,6% |
| R5 | Ở mỗi x ∈ {90, 120, 150, 180, 240}: \|tổng `V-x` − tổng `C-x`\| ≤ 2 khách | Việc A và Việc B: với (b), `V-30` = `C-30` về số khách ở mọi ô có tắc |
| R6 | Tỉ lệ thất hứa của `Mplus` (A > x, trung bình 8 ô), theo x = 60, 90, 120, 150, 180, 240, 300 (60/120/300 lấy từ Việc B: 7,25%, 0,9%, 0,2%), **không tăng**; giá trị ở 90 s nằm trong **[1,5%; 5,0%]**; ở 150, 180, 240 s mỗi giá trị **≤ 1,0%** | nội suy giữa 60 và 120 s |
| R7 | Ở mỗi x mới, tỉ lệ khách của `C-x` trễ quá lời hứa đầu + x (thước hạn chót) lệch tỉ lệ của `Mplus-x` **không quá 1 điểm phần trăm** | ở 30/60/300 s hai luật cùng quyết định nên bằng đúng; ở 120 s lệch 0,3 điểm |
| R8 | Tỉ lệ thất hứa theo thước của chính `V` (V_r > x) không tăng theo x ∈ {30, 90, 120, 150, 180, 240}, và ở mỗi mức ≥ tỉ lệ A > x của chính `V-x` | lời hứa giờ trả cuối trùng giờ trả thật (kiểm độc lập Việc A), nên V_r ≥ \|A\| cho từng khách |

Tiêu chí ở R3 chỉ dùng cho bước thăm dò này. Nó không phải δ của H2 và không chốt quy tắc quyết định nào của T3.8/T3.9.

## 5. Không làm

- Không đổi thước đo hay dự đoán sau khi chạy; phân tích thêm thì ghi là mô tả, kèm ngày.
- Không nói "`C` phục vụ nhiều hơn" nếu chênh ≤ 2 khách hoặc chỉ ở 1–2 ô.
- Không sửa, build hay ghi fixture trong `E:\Code\RideBound-deadline`. Không xóa tệp nào.
- Báo kết quả cho chủ nghiên cứu trước; chỉ sửa luận văn khi được đồng ý.
