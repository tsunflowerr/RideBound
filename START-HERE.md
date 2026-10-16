# Tiếp tục RideBound trên máy khác — 16/10/2026

Đây là nhánh bàn giao mã ưu tiên chi phí cùng hồ sơ nghiên cứu mới nhất.
Không cần thay đổi lịch sử commit. Nhánh `main` giữ lịch sử và trạng thái riêng.

```powershell
git clone --branch codex/research-handoff-20261016 https://github.com/tsunflowerr/RideBound.git
cd RideBound
dotnet test RideBound.slnx
```

Nguồn khóa luận và các chương trình nghiên cứu ở repo thứ hai:

```powershell
git clone --branch codex/ridebound-handoff-20261016 https://github.com/tsunflowerr/Report_INT3508.git
```

## Đọc trước khi sửa

1. `AGENTS.md`, `docs/00-index.md`, `docs/01-research-charter.md`.
2. `docs/18-status-and-decision-log.md` và `docs/19-requirement-traceability.md`.
3. `research/continuation/2026-10-15/results/STAGE1B-RESULTS.md`.
4. `research/continuation/2026-10-16/thesis-readiness-audit/THESIS-READINESS-AUDIT.md`.
5. `research/continuation/2026-10-16/cost-cap-sensitivity-v1/PLAN.md`.

Nếu tài liệu lịch sử ghi chưa được commit/push, chưa cập nhật khóa luận hoặc sàn
C45 GiB: đó là mốc cũ. Người dùng đã cho cập nhật khóa luận, đổi C30 GiB và cho
push/bổ sung C60/C120. Các kế hoạch/receipt đã niêm phong được giữ nguyên byte;
quyền mới được ghi riêng trong hồ sơ hiện tại.

## Khảo sát bổ sung đã hoàn tất

C60/C120: 2/2 smoke và68/68 main; kiểm cuối/phân tích exit0,4355/4355 đối chiếu.
Đọc [kết quả và đánh đổi](research/continuation/2026-10-16/COST-CAP-RESULTS.md)
và [bốn tệp phân tích gốc](research/continuation/2026-10-16/cost-cap-sensitivity-v1/results/REPORT.md).
C30/C60/C120/không trần lần lượt phục vụ2364/2396/2430/2502,muộn>60s72/91/212/379.
C60 là điểm đánh đổi trung gian trên bộ thử đã thấy;không claim trần tối ưu.
Đợt đã xong,không resume/rerun launcher hoặc analysis. PDF77trang chưa chèn
các kết quả mới; audit mức sẵn sàng bảo vệ/WP12 còn mở.

## Kết quả C30 và so sánh selector trước đó

68/68 lượt chính ưu tiên chi phí ở C30/không trần đã pass và kiểm cuối exit 0.
Mỗi cấu hình dưới đây có 3.672 yêu cầu qua 34 ô/5 ngày phụ thuộc:

| Cấu hình | Phục vụ | Muộn hơn mốc kế hoạch ban đầu >60s | Hoàn thành có ghép xe |
|---|---:|---:|---:|
| Ưu tiên ổn định + C30 | 2338 | 76 | 313 |
| Ưu tiên chi phí kế hoạch + C30 | 2364 | 72 | 432 |
| Ưu tiên chi phí kế hoạch, không trần | 2502 | 379 | 1027 |

Cùng C30, ưu tiên chi phí đổi lại số khách hoàn thành bị sửa giờ tăng 33→106,
sửa lặp 0→2, đón muộn 43→44; muộn theo mốc hiển thị có dự báo 10→11.
Không kết luận bộ chọn nào thắng mọi mặt, chi phí thực giảm, C30 tối ưu hoặc
khách hàng hài lòng hơn. Hai selector lịch sử dùng các build khác nhau.

H7 có 7 lịch sử phù hợp ngưỡng giả định120, dưới cổng10. Không mở thí nghiệm
cumulative-vs-per-step; H6/E1 cũ vẫn là kết quả âm. Các trần bổ sung C60/C120
là nghiên cứu độ nhạy riêng trên đúng runner cost-first v3, không sửa kết quả cũ.

## Mã và dữ liệu cần phân biệt

- Nhánh này bao gồm commit nguồn runner `dce84ec4e51b9b7489402d81bb0dda28afbda0a0`.
- Runner đã chạy: tree `d08c0057d8239c6c51cceb0d2ba2d7f41dc899a9ac085671c363ede388a0b4b8`;
  DLL `1839a54413b2729cf353db7ef8b2359d32f4698709bba75a96eca492e36c69a9`.
- Build mới từ mã nguồn có thể khác byte. Không tự nhận là binary đã chạy.
- TSV người đi, JSON tổng hợp, báo cáo, hash và receipt nhẹ nằm trong
  `research/continuation/`. `raw-bundle-index.json` lưu hash từng tệp raw.
- Transcript của 68 lượt cũ khoảng 4,63 GiB; dữ liệu gốc, FleetPy, Python env,
  published runner, vendor và paper tải về vẫn ở máy gốc; không đưa lên Git.
- Các script lịch sử có đường dẫn C:/E: và kiểm inventory/env/binary cứng.
  Clone repo đủ để đọc/sửa nguồn; chưa đủ để chạy lại mô phỏng. Muốn chạy ở máy
  khác phải cung cấp dữ liệu/runtime, viết manifest thực thi mới và kiểm lại.
- Không khởi động cùng nghiên cứu trên máy công ty trong khi driver máy gốc
  đang chạy. Tham khảo checkpoint được xuất sang GitHub; trạng thái đó có thời điểm.

## Khóa luận và việc còn mở

Repo Report_INT3508 chứa `ridebound/main.pdf` (77 trang), nguồn LaTeX và hồ sơ
kiểm. SHA PDF: `26c8dea1334e68091b421a831bf14e809e76a396e0444b75398d662a8921dd69`.
Đã giao bản thảo đầy đủ, chưa đóng mức sẵn sàng nộp/bảo vệ: còn cách chọn mẫu,
giới hạn suy luận thống kê, hợp đồng tích hợp dự báo/giờ đón và mẫu áp dụng.

Baseline test trên checkout cost-first sạch có một lỗi đã biết:
`StrictBagItBundleTests.Source_inventory_binds_dirty_worktree_files_not_only_base_commit`
đòi working tree dirty. Ghi nhận 1033 pass/1034 và exit1 ở lần kiểm trước;
log mới đi kèm phải được đọc theo actual exit, không nhận suite xanh.

Không có quyền triển khai, mở WP15/Stage2, xóa corpora hoặc sửa dữ liệu để cứu
kết quả. Khi sửa mã, chạy `dotnet test RideBound.slnx` và cập nhật docs18/19.
