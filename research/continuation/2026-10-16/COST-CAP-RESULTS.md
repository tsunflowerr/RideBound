# Kết quả khảo sát trần 30/60/120 giây và không trần

Cập nhật 2026-10-16T18:14:17.513937+07:00. Đợt C60/C120 đã hoàn tất lúc 17:24:02 ngày 16/10/2026 (+07):
2/2 lượt thử, 68/68 lượt chính, kiểm đầu vào cuối đợt và phân tích đều exit 0.
Không còn tiến trình chạy hoặc lượt chờ. Không chạy lại mô phỏng hay phân tích.

## Thiết kế và cách đọc

Cả bốn cấu hình dùng cùng bộ chọn ưu tiên chi phí kế hoạch, runner v3, seed 7
và cùng 34 ô thí nghiệm: mỗi cấu hình có 3.672 yêu cầu, 108 yêu cầu/ô.
C30/không trần là kết quả đã khóa được tái sử dụng; C60/C120 là 68 lượt mới.
Các ô thuộc năm ngày có phụ thuộc và đều đã được xem trước nghiên cứu này.

C là tổng trị tuyệt đối của các thay đổi ETA trả khách do quyết định điều phối,
tính riêng cho từng khách trong toàn lịch sử. Trần 30/60/120 giây giới hạn tổng
đó; không phải giới hạn thời gian chờ, trễ thực tế, hay mức khách chấp nhận.
p0 là ETA trả ban đầu của kế hoạch; D0 là mốc hiển thị phát lại có dự báo.
Muộn >60s được tính trên khách hoàn thành và đã xuống xe, so với mốc tương ứng.
Trong ngưỡng 60s = số hoàn thành trừ số muộn >60s, bao gồm cả đến sớm.
Các cột ghép xe/sửa ETA/sửa lặp cũng chỉ tính trong số khách hoàn thành.

| Trần | Phục vụ | Muộn >60s so với p0 | Trong ngưỡng 60s | Có ghép xe | Bị sửa ETA | Sửa ETA từ 2 lần | Đón muộn | Muộn >60s so với D0 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| C30 | 2364 | 72 | 2292 | 432 | 106 | 2 | 44 | 11 |
| C60 | 2396 | 91 | 2305 | 545 | 166 | 3 | 45 | 21 |
| C120 | 2430 | 212 | 2218 | 737 | 284 | 14 | 47 | 142 |
| Không trần | 2502 | 379 | 2123 | 1027 | 434 | 50 | 39 | 318 |

## Đánh đổi quan sát được

- Từ C30 lên C60: phục vụ tăng 32, ghép xe tăng 113; muộn quá một phút tăng
  19. Số khách hoàn thành trong ngưỡng một phút tăng 13 (2.292 → 2.305).
  Khách bị sửa ETA tăng 106 → 166, sửa lặp 2 → 3, đón muộn 44 → 45,
  muộn theo mốc có dự báo 11 → 21.
- Từ C60 lên C120: phục vụ tăng 34, ghép xe tăng 192; muộn quá một phút tăng
  121. Số hoàn thành trong ngưỡng một phút giảm 87 (2.305 → 2.218).
  Khách bị sửa ETA tăng 166 → 284, sửa lặp 3 → 14, đón muộn 45 → 47,
  muộn theo mốc có dự báo 21 → 142.
- Từ C120 sang không trần: phục vụ tăng 72 và ghép xe tăng 290, đổi lại muộn
  quá một phút tăng 167, số hoàn thành trong ngưỡng giảm 95; sửa lặp tăng
  14 → 50. Đón muộn lại giảm 47 → 39: không phải mọi chỉ số đều đơn điệu.

Đây là chênh lệch tổng giữa các cấu hình, không phải phân tích chuyển trạng thái
của cùng một nhóm khách. Trong bộ thử này C60 là điểm đánh đổi trung gian và
có số hoàn thành trong ngưỡng một phút cao nhất trong bốn mức đã khảo sát.
Điều đó chưa chứng minh C60 tối ưu, C30 được chọn sai, hay một mức phù hợp
cho mọi bối cảnh. Tăng mức cho phép làm hệ thống linh hoạt hơn nhưng cũng cho
phép đổi kế hoạch nhiều hơn; mức trễ thực tế còn chịu tác động của giao thông.

## Kiểm chứng và bằng chứng

Driver đã kiểm đủ bundle, native exit, identity, ledger, trần, toàn bộ đầu vào
cũ/mới trước và sau các giai đoạn; root log kết thúc exit 0. Manifest nghiên cứu:
`cedecbb1a97d31ec8ab295fd16c99fb607f14d622779247d8df6d9b219b42d6f`. 68 bundle main và hai bundle smoke còn nguyên ở máy gốc.

Checker mới ngoài harness đã khóa đạt **4.355/4.355** đối chiếu: hash đầu vào/
kết quả/receipt, 136 cặp cấu hình–ô, số học TSV/JSON theo ô/ngày/tổng, các mốc
C30/không trần, lifecycle, accounting và trần. Không có vượt trần trên khách
được hứa, không có ledger mismatch. Checker này không phải một bộ giải mã raw
hay một triển khai dự báo độc lập. Bản kết quả phân tích gốc chỉ có đúng bốn
tệp trong `cost-cap-sensitivity-v1/results/`; không sửa hay thêm tệp vào đó.

Mức C lớn nhất trên toàn bộ khách được hứa lần lượt là 29.657, 59.622,
119.657 và 709.119 mili giây. Phân vị 95 trên khách hoàn thành lần lượt là
0, 5.017, 65.208 và 163.550 mili giây. Bảng theo từng ô/ngày, số hoàn thành
trong ngưỡng 120/300 giây và từng khách nằm trong JSON/TSV gốc.

.NET kiểm lại: 1.033 pass/1.034, exit 1 với lỗi đã biết trên checkout sạch
`StrictBagItBundleTests.Source_inventory_binds_dirty_worktree_files_not_only_base_commit`.
Không nhận toàn bộ suite xanh; không sửa lõi C# trong bước đóng báo cáo này.
AC và sàn C30/E40 GiB được giữ; tại kiểm cuối 18:12 còn AC100%, C35,81/E40,76 GiB.

## Giới hạn và khóa luận

Kết quả là khảo sát độ nhạy trên bộ ô đã thấy, không có tập ngày mới độc lập,
kiểm định mức hài lòng, khẳng định giảm chi phí thực/lợi nhuận hay tối ưu toàn
cục. Dự báo chỉ phát lại mốc hiển thị trong phân tích, chưa tham gia phản hồi
vào quyết định điều phối của nghiên cứu này. H6/E1 và H7=7<10 giữ nguyên;
không mở so sánh cộng dồn với từng lần hoặc Stage2.

PDF khóa luận 77 trang hiện có giữ SHA256 `26c8dea1334e68091b421a831bf14e809e76a396e0444b75398d662a8921dd69`.
Báo cáo mới được bàn giao riêng trước khi sửa nội dung khóa luận. Audit mức
sẵn sàng bảo vệ, hợp đồng dự báo/giờ đón, chọn mẫu và WP12 còn mở.

## Đọc và tiếp tục ở máy khác

Đọc báo cáo này, `cost-cap-sensitivity-v1/results/REPORT.md`, `results.json`,
`riders.tsv`, `results.sha256`, `analysis-complete.json` và checker proof.
Nguồn runner nằm trong nhánh bàn giao RideBound; raw transcript, dữ liệu,
runtime, binary và vendor vẫn ở máy gốc. Metadata bundle, log, kế hoạch,
manifest và hash được bàn giao; clone chưa đủ để chạy lại trên máy mới.
Đợt này đã kết thúc: không resume/rerun launcher hoặc analysis. Không xóa dữ liệu.
Lịch theo giờ sẽ được tắt sau khi báo kết quả và push; báo thức 06:20 giữ PAUSED.
