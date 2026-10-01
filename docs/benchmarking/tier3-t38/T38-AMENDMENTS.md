# T3.8/T3.9: phụ lục sau khi niêm phong kế hoạch (`T38-PLAN.md`, SHA-256 78954a15…8a60)

Mọi thay đổi so với kế hoạch được ghi ở đây, kèm thời điểm và lý do. Kế hoạch gốc không bị sửa.

## A1. Một job hiệu chỉnh bị máy ngủ làm dở; chạy lại một lần (2026-10-01, ghi lúc 22:5x giờ máy)

- **Chuyện đã xảy ra.** Job `C-30-d20181118-s10-r1-w17` (ô 18/11 mẫu 1, khung 17:00–19:00) bắt đầu 07:14 giờ máy.
  Nhật ký hệ thống ghi máy vào chế độ ngủ lúc 07:24:39 (Kernel-Power sự kiện 42) và thức dậy lúc 22:41 (sự kiện 506/507).
  Tệp `transcript-00.ndjson` của job ngừng ghi lúc 07:24, dài 59,2 MB. Thư mục không có `summary.json`, cũng không có log
  (`logs/` có 14 log cho 15 job). 14 job còn lại đều `status-pass`.
- **Tiến trình.** Bộ chạy `run_t38.py run calib` bị kết thúc cùng phiên làm việc (không còn tiến trình python nào).
  Các tiến trình dotnet đang chạy là nút MSBuild và phần mở rộng C# của VS Code, không phải mô phỏng.
- **Phân loại.** Lỗi hạ tầng (máy ngủ), thuộc loại mà kế hoạch §2 đã quy định: chạy lại **một lần** vào `<job>-rerun1`, giữ
  nguyên thư mục lỗi. Không phải lỗi của chính sách hay của kịch bản. Đây là cùng loại với phụ lục A2 của phép quét ngày
  2026-09-25 (`../deadline/AMENDMENTS.md`).
- **Cách làm.** Script mới `run_t38_rerun.py` (không sửa `run_t38.py` đã niêm phong). Script chỉ chạy khi thư mục lỗi
  tồn tại mà không có `summary.json`, và khi thư mục `-rerun1` chưa có. Lệnh chạy y hệt lệnh của `run_t38.py`, chỉ khác tên
  output và nhãn (`C-30-d20181118-s10-r1-w17-rerun1`, nhãn `…-rerun1-t38`). Thư mục lỗi không bị đụng tới.
- **Ảnh hưởng đến phân tích.** `t38_calibrate.py` và `t38_analyze.py` cần biết rằng với một job mà thư mục gốc không có
  summary, kết quả lấy từ `-rerun1`. Hai script được sửa chỉ ở chỗ đó (hàm `effective`), mã băm mới ghi bên dưới. Điểm số
  và mọi quy tắc của kế hoạch không đổi.
- **Ngoài phạm vi sửa.** Không đổi cách chia tập, γ, ngưỡng, dự đoán.

## A2. Kết quả hiệu chỉnh (2026-10-01, ghi trước job kiểm tra đầu tiên)

- 15/15 job hiệu chỉnh có kết quả (14 job gốc + `C-30-d20181118-s10-r1-w17-rerun1`, 946 s, `status-pass`); kiểm hợp lệ: 0 vấn đề.
- `calibrate-2026-10-01.log`: γ = hạng thứ 15 trong 15 điểm số = **222,259 s**; β + γ = **252,259 s** ≤ 300 s (hữu ích: đúng); ≤ 120 s: sai.
- `GAMMA-t38-v1.json` SHA-256 2bdd3b0e83a6cd3d09550a4551f063101c68a97b783fe49a8729f1ace9f198cd. Tệp này được commit và push trước pha kiểm tra.

Mã băm các script sau A1 (hai script đổi so với bản niêm phong, chỉ ở hàm `effective`; `test_t38.py` thêm 3 kiểm thử, 25/25 đạt):

```
b1a91e9b1b0dd543511480cd9bcbf2d41f0300fbaed8eb386f0ac9ed6b7e5e96 *t38_calibrate.py
12f41b9a3e2fd9d4c14e905933783c097fa30dc57e607a445c860ef8c65842d2 *t38_analyze.py
0803ee67159b3d6f1d04330cec00cae1815417d8ee42c379b87b516ea5abf9b6 *test_t38.py
a880d73320e4dab24e150e81eb73a8dbefc135a8bb6b398fbb2da21e29a6814e *run_t38_rerun.py
9cc8df75c4c76543f026f82e320cfc7e2cd7d760de00f3e074db55e2040d3b15 *calibrate-2026-10-01.log
2c440fee6952a5f5c4c8ce1d6712d10489a34a721cc5bd220b334ba9f062789c *rerun-C-30-d20181118-s10-r1-w17-2026-10-01.out
```
