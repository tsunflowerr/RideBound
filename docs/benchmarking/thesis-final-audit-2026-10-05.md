# Kiểm toán bản khóa luận cuối — 2026-10-05

Đây là hồ sơ biên tập và đối chiếu chứng cứ của khóa luận bên ngoài repository.
Nguồn sự thật về tiến độ vẫn là [status log](../18-status-and-decision-log.md);
lượt này không nâng implementation, claim ladder hoặc work package.

## Artifact và phạm vi

Thư mục làm việc: E:/Code/Report_INT3508/ridebound-scratchpad/thesis-final-2026-10-04.
Nguồn LaTeX ở source/, bản dựng ở build/, hồ sơ kiểm ở audit/; tách khỏi cây
nghiên cứu RideBound. Sản phẩm bàn giao chính và duy nhất là PDF. Nguồn và hồ sơ
kiểm phục vụ truy vết, không thay bằng chứng thực nghiệm đã niêm phong.

| Trường bản PDF cuối | Trạng thái |
|---|---|
| Artifact bàn giao | E:/Code/Report_INT3508/ridebound-scratchpad/thesis-final-2026-10-04/thesis_final.pdf |
| Số trang cuối | 68 trang vật lý, gồm bìa và mọi phần đầu/cuối |
| SHA-256 cuối | e7a4712663ad2167f2bbbffd7330fa0c64a580964cff558b3d3dc2ecba14454c |

Bản bàn giao có cùng bytes với `build/main.pdf` đã render và kiểm từng trang
ở `audit/final-layout-v4`. Không dùng băm của một lượt dựng trung gian.

## Kiểm bản bàn giao

- 68/68 trang đã xem riêng ở kích thước đầy đủ: root kiểm 001–012 và 066–068;
  ba người kiểm còn lại lần lượt 013–030, 031–048 và 049–065. Các biên bản
  `audit/visual-qa-final-*.md` ghi cùng SHA-256; không còn lỗi hiển thị.
- 19 tệp TeX có 40 label, 46 lời gọi tham chiếu, 24 mục tài liệu đều được trích;
  không thiếu/trùng/không dùng label hoặc citation. Năm hình và 14 bảng được gọi
  trong nội dung; hai bảng ký hiệu đầu sách không đánh số.
- `audit/final-pdf-integrity.json`: A4, đủ footer i–x rồi1–56; 22 bộ font đều
  nhúng, 237 liên kết có đích hợp lệ, 92 bookmark. Giữ Times New Roman13pt,
  giãn1,5, lề trái3cm và các lề khác2cm.
- Log TeX cuối:0error/overfull/underfull/tham chiếu thiếu/ký tự mất/đích trùng.
  Tectonic vẫn báo truy cập font Windows qua đường dẫn tuyệt đối và Fontconfig
  không có default config; font thực đã nhúng và kiểm hiển thị. Không tuyên bố
  build tái lập byte trên một máy thiếu font tương ứng.
- 123/123 phép kiểm số sau phản biện qua trên chính tệp bàn giao;1.869 token số
  được quét cùng ngữ cảnh,13 thước đo trung tâm được truy mọi vị trí. Báo cáo
  và script đọc chứng cứ đã lưu, không tạo dữ liệu hoặc kết quả mới.
- 32 câu hỏi bảo vệ có vị trí căn cứ theo trang PDF trong
  `audit/defense-questions.md`; không thêm FAQ vào bản khóa luận.

Mười quality gate về khoa học, trích dẫn, số, novelty, khả năng đọc, tiếng Việt,
hình, bảng, số trang và PDF QA được ghi tại `audit/quality-gates-final.md`.
Tên/học hàm GVHD trên hai bìa vẫn chờ tác giả cung cấp; cần điền trước nộp
chính thức, không suy đoán tên hoặc gọi bản hiện tại đủ thông tin hành chính.

audit/preservation-check.json xác nhận 18 tệp gốc và 76 tệp của
review-packet-v1 đều không có mismatch. Ba báo cáo phản biện độc lập và không
được xem báo cáo của nhau, đã đóng băng; băm A/B/C ở audit/reviews/frozen-reports.json.
Các sửa sau phản biện không ghi ngược vào packet hoặc báo cáo.
audit/forensic-consistency.md và JSON cùng tên ghi 123 phép kiểm tất định sau
phản biện, tất cả qua; đây là lớp kiểm biên tập, không phải phản biện thứ tư,
thử nghiệm mới hoặc số mẫu độc lập mới.

## Các tiền lệ gần nhất

Theo phần bổ sung của [ranh giới claim](../03-related-work-and-claim-boundary.md):

| Nguồn đã đối chiếu | Ranh giới giữ trong khóa luận |
|---|---|
| [Fielbaum–Alonso-Mora 2020, DOI 10.1016/j.trc.2020.102831](https://doi.org/10.1016/j.trc.2020.102831) | Có cả phép đo hậu nghiệm và kiểm soát trước quyết định; không nhận hai điều này là mới. |
| [Elmi và cộng sự 2026, DOI 10.1016/j.trc.2026.105897](https://doi.org/10.1016/j.trc.2026.105897) | Độ chùng và bảo đảm cho khách được chọn đã có; bảo vệ từng khách hoặc ETA riêng lẻ không phải novelty. |
| [Schulz–Pfeiffer 2026, DOI 10.1007/s00291-026-00847-0](https://doi.org/10.1007/s00291-026-00847-0) | Hạn chót trả từ thông báo đầu, độ chùng 900 giây trong thiết lập đã đọc; khác trần 30/60/120 giây về độ chặt. |
| [Haferkamp 2024, DOI 10.1016/j.ejtl.2024.100134](https://doi.org/10.1016/j.ejtl.2024.100134) | Giờ đón đã chọn thành ràng buộc với lệch nhỏ cho phép; giữ giờ đã thỏa thuận không phải novelty. |

Các nhánh so trong RideBound hiện thực hình thức cổng trên cùng hệ, không tái
lập toàn bộ thuật toán hoặc mô hình lựa chọn của bốn nguồn này. Mốc chiếu giữ
tuyến là đối chiếu trong mô hình tại cùng trạng thái, không nhận diện đầy đủ
tác động nhân quả của giao thông. Tam giác nghiêm không tự chứng minh vi phạm
giả thiết bao hàm H.

## Kết quả và giới hạn được giữ

[H6 gốc](wp9-confirmatory-result-2026-08-23.md) vẫn có kết quả âm: Panel A mất
154/2.160 lượt hoàn tất, tức 7,1296 điểm phần trăm, báo tròn 7,13 điểm, vượt biên
không thua kém 1 điểm đã khóa. Giảm độ dời được đo không cứu tiêu chí dịch vụ;
các nhánh thăm dò về sau không đổi verdict này.

T38 là thăm dò có kế hoạch niêm phong trước các nhánh mục tiêu. Có 15 ô hiệu
chỉnh trong 6 nhóm và 34 ô kiểm tra trong 14 nhóm; 34 ô nhân năm nhánh tạo 170
lần chạy kiểm tra thành công. Nhóm ngày/mẫu
tách nhau nhưng vẫn chung 75 chuyến nguồn, cùng ngày và có khung giờ chồng lấn.
Không nhận độc lập, tính hoán đổi hoặc bảo đảm độ phủ quần thể 90% cho ngày mới.
Độ phủ điểm 32/34 khác độ phủ biên nhìn thấy thực 33/34; biên tất định vẫn cần
điều kiện trên phần thay đổi khi giữ tuyến. Miễn trừ được ghi là vi phạm,
không biến thành vận hành bình thường hay hài lòng của khách.

Nguồn C# đã đọc ở C:/Code/RideBound-noworse, HEAD
24aa747147d49db79675d170829e6f3bf3afc3cc, khác các artifact đã chạy:

- H6 dùng bộ thực thi h6-confirmatory-v1 đã ghim bởi freeze v5; B1 rollingCost
  và C1 hardVector chưa có tầng đếm miễn trừ hoặc phục hồi no-worse.
- Tầng 2 dùng runner-deadline-v1 và hardVector, chưa bật phục hồi.
- T38 dùng runner-tier3-v2, hardVector cho cả năm nhánh và phục hồi
  no-worse-than-reference-v1, cùng lateBoarding=record-v1. Nhánh U bỏ trần thời
  gian nhưng giữ các trần định danh bằng không và khóa xác nhận, không phải B1.

audit/selector-revision.md ghi con trỏ mã, cấu hình, receipt/DLL và các khung
trao đổi được kiểm băm. Không suy bộ giải lịch sử chỉ từ tên policy hoặc đồng
nhất HEAD hiện đọc với mọi bộ thực thi niêm phong.

## Ranh giới thao tác

Không sửa code, bộ thực thi, dữ liệu, cấu hình, raw transcript hoặc kết quả
nghiên cứu; không xây RideBound, chạy lại mô phỏng hoặc nhận test baseline mới.
Lượt này không phát sinh code change nên không ghi dotnet test như một gate đã
chạy. Các tệp dirty và untracked của người dùng được giữ, không clean/reset,
xóa hay gom vào công việc biên tập. Quyền mở WP và next action giữ theo status log.
