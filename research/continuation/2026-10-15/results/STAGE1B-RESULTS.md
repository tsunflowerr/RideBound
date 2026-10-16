# Kết quả Stage1b — 15/10/2026

Stage1b đã hoàn tất68/68 main, cả68 summary pass/run succeeded. Driver kết thúc
exit0 lúc11:28:00+07 sau full frozen-input/post-phase verification. Extractor
exit0 lúc11:31:45; reporter exit0 lúc11:40:13. Bản trích xuất có9.638 dòng khách,
136 role-runs từ136 thư mục:68 CF mới và68 SF lưu trữ. Tất cả19 giá trị SF cùng
mẫu dấu25/25 ô w08/w17 được tái lập. Không lỗi cộng sổ, lifecycle hoặc input drift.

83/83 kiểm tra số học độc lập đạt: metadata/mã băm TSV, số khách/mẫu số của bảng
chính, các đồng nhất thức gộp/từng ô/từng ngày/khung giờ và các ngưỡngH2–H7.
Đây là kiểm lại từ extraction; không nhận là dựng lại độc lập mọi dòng raw hoặc
dự báo. Chương trình trích xuất đã kiểm raw theo reader và inventory đã khóa.

## So sánh bốn nhóm

Mỗi nhóm có3.672 yêu cầu trên34 ô thuộc5 ngày. SF là bộ chọn ưu tiên ổn định;
CF là bộ chọn ưu tiên chi phí kế hoạch; U không đặt trần revision trả, C30 đặt
trần30 giây cho tổng độ lớn thay đổi lời hứa trả do điều phối. “Muộn” ở bảng này
so với **lời hứa trả đầu tiên**, chưa cộng phần hiển thị dự báo. Lượt ghép là
khách hoàn thành có khoảng thời gian cùng xe với khách khác; không phải số chuyến.

| Nhóm | Phục vụ /3.672 yêu cầu | Muộn>60s /khách phục vụ | Hoàn thành ghép /khách phục vụ |
|---|---:|---:|---:|
| SF-U | 2.434 (66,29%) | 203/2.434 (8,34%) | 555/2.434 (22,80%) |
| SF-C30 | 2.338 (63,67%) | 76/2.338 (3,25%) | 313/2.338 (13,39%) |
| CF-U | 2.502 (68,14%) | 379/2.502 (15,15%) | 1.027/2.502 (41,05%) |
| CF-C30 | 2.364 (64,38%) | 72/2.364 (3,05%) | 432/2.364 (18,27%) |

Ở nhómC30, đổi SF→CF tăng26 khách phục vụ,119 lượt hoàn thành ghép và30 khách
đúng hẹn trong ngưỡng60s. Muộn>60s giảm4 khách. Tổng mức thay đổi do điều phối
lớn nhất29,657s;0/2.364 khách vượt30s. Với lời hứa có dự báo, CF-C30 có11/2.364
khách muộn>60s (0,47%), SF-C30 có10/2.338 (0,43%); số đúng hẹn lần lượt2.353
và2.328 trên3.672 yêu cầu. Median horizon lời hứa đầu CF-C30 ngắn hơn SF-C30
5,418s; các guard median+30s đều đạt.

Các đánh đổi vẫn hiện diện: ở C30, số khách bị thay lời hứa trả tăng33→106,
khách bị thay ít nhất hai lần tăng0→2; late pickups43→44. Khi bỏ trần, SF→CF
tăng68 khách phục vụ và472 lượt hoàn thành ghép, đồng thời tăng176 khách muộn>60s,
giảm108 khách đúng hẹn trong ngưỡng60s. Trong CF, đặt C30 thay U giảm138 khách
phục vụ và595 lượt hoàn thành ghép, giảm307 khách muộn>60s, tăng169 khách đúng
hẹn trong ngưỡng60s. Các tập khách phục vụ khác nhau giữa các nhóm.

## Chỉ tiêu chính và điểm dừng

I_L=(379−72)−(203−76)=+180 rider-runs; I_served=+42 và I_timely60=−138,
đúng đồng nhất thức180=42−(−138). I_L dương ở23/25 ô w08/w17,29/34 ô tổng thể
và5/5 ngày. Leave-one-day-out I_L từ+132 đến+162 chỉ là mô tả, không là khoảng
tin cậy. Đây là tương tác giữa bộ chọn và trần theo định nghĩa của kế hoạch;
không gọi là180 khách được cứu do một nguyên nhân riêng lẻ.

Theo các ngưỡng nội bộ đã khóa: G0 vàH1–H6 đạt; **H7 không đạt**. CF-U có7/2.502
khách phục vụ với mọi bước đổi≤120s nhưng tổng>120s, ngưỡng yêu cầu≥10. Vì vậy
**đóng nhánh mở rộng Stage2 theo kế hoạch; không xây/chạy per-step arm**. H7 là
điểm dừng phân bổ tài nguyên, không chứng minh cơ chế tích lũy không có tác dụng.
C30 có một recorded cumulative-only prune witness; yêu cầu mới liên quan cuối
cùng không bị từ chối. Witness này không thay thế phép so sánh thực nghiệm với
per-step và không cho phép claim lợi ích outcome của tích lũy so với per-step.

## Phạm vi và chứng cứ

Toàn bộ là kết quả thăm dò trên34 ô đã được xem trước,5 ngày phụ thuộc, một
simulator và các hệ số giao thông toàn thành phố. Không có34 lặp độc lập,
khoảng tin cậy, bảo đảm tổng quát, kết luận nhân quả thuần, tiết kiệm chi phí
thực/lợi nhuận hoặc hài lòng người dùng. Giữ nguyên dữ liệu/kết quả H6/E1 gốc và claim hẹp.
Gate0 chỉ behavioural như amendment gốc; không nhận đã replay đầy đủ bằng.NET.

Thay đổi tài nguyên được công khai:20 main đầu dưới sàn C45GiB,48 main sau dưới
sàn C30GiB theo quyền trực tiếp của người dùng. E40GiB,AC-only,reserve410MiB/job,
sáu workers và toàn bộ scientific inputs/analysis/binding giữ nguyên. Driver v7
không bị sửa; entrypoint tài nguyên riêng nạp bytes đã xác thực rồi chỉ đổi
FLOOR_MIB trong bộ nhớ. Original seal, analysis seal và policy seal vẫn khớp.

- Báo cáo gốc đầy đủ: [stage1b-report.txt](stage1b-report.txt).
- Dữ liệu trích xuất: [stage1b-runs.json](stage1b-runs.json), [stage1b-riders.tsv](stage1b-riders.tsv).
- Final main admission: ../evidence/main-final-admission.json.
- Logs và start/finish receipts: ../evidence/analysis-extract-* và analysis-report-*.
- Kiểm số học: ../evidence/analysis-independent-arithmetic-check.json; code ../verify-stage1b-arithmetic.py.
- Chính sách tài nguyên: ../resource30-v3/STAGE1B-AMENDMENTS.md; pin7c862409…11950.

Nguồn analysis ban đầu là Claude; sửa/review/kiểm lại trong phiên tiếp nối này
là Codex. Không nhận review Claude/Antigravity mới. Không sửa core C# trong phiên
tiếp nối; baseline.NET đã ghi là1.033 pass+1 known clean-tree failure,exit1, không
nâng thành xanh. Luận văn thật chưa sửa; bản PDF74 trang vẫn chờ owner duyệt.
