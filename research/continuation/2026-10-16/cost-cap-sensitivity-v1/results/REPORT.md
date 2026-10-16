# Kết quả độ nhạy trần biến động kế hoạch — ưu tiên chi phí

Thăm dò trên 34 ô đã từng được xem; cùng runner v3. Không có kiểm định mức hài lòng, tối ưu toàn cục, lợi nhuận hay so sánh cộng dồn với từng lần.

| Trần | Phục vụ | Muộn >60s (p0) | Đúng trong 60s | Ghép chung | Bị sửa giờ* | Sửa lặp* | Đón muộn | Muộn >60s (D0) |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| CF-C30 | 2364 | 72 | 2292 | 432 | 106 | 2 | 44 | 11 |
| CF-C60 | 2396 | 91 | 2305 | 545 | 166 | 3 | 45 | 21 |
| CF-C120 | 2430 | 212 | 2218 | 737 | 284 | 14 | 47 | 142 |
| CF-U | 2502 | 379 | 2123 | 1027 | 434 | 50 | 39 | 318 |

*Trong số khách hoàn thành. p0 là mốc kế hoạch ban đầu; D0 là mốc phát lại có dự báo. C là tổng biến động do điều phối, không phải thời gian khách chờ.

C30/U tái sử dụng kết quả đã khóa; C60/C120 là 68 lượt chính mới. Toàn bộ ô/ngày nằm trong results.json. Không diễn giải bộ thử này là kiểm định trên ngày chưa thấy.

H6/E1 cũ và H7=7<10 được giữ nguyên; nghiên cứu này không mở nhánh per-step hoặc sửa tích hợp dự báo.
