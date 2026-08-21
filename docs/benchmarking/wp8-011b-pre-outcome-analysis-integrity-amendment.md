# Pre-outcome amendment: analysis/source binding — WP8-011b

> Thời điểm: 2026-08-21, trước khi chạy bất kỳ job nào của WP9. Không có
> confirmatory outcome nào tồn tại khi amendment này được lập.

## Lý do bắt buộc

Freeze receipt v1 bind đúng manifest và chương trình lúc đó, nhưng review
adversarial sau freeze phát hiện hai thiếu sót integrity:

1. independent verifier chứng minh bundle tự nhất quán, còn analyzer chưa đối
   chiếu bundle với exact `jobId/cellId/armId/masterSeed/config` trong execution
   plan; đặt nhầm hai bundle hợp lệ có thể đảo dấu delta;
2. preflight chỉ so Git HEAD. Khi working tree đã dirty, sửa nội dung của một file
   vốn mang trạng thái `M` không đổi HEAD hay `git status --porcelain`.

Đây là lỗi provenance/analysis binding, không phải tín hiệu outcome. Chúng được
phát hiện bằng đọc source và mutation test trước smoke/full matrix.

## Thay đổi được phép

- Analyzer primary bắt exact execution-plan row, label nội bộ, scenario SHA-256 và
  repository inventory trước khi đọc metric.
- Matrix/preflight bind một SHA-256 content inventory của mọi file Git-visible
  cùng HEAD trước/sau mỗi run; resume chỉ nhận bundle có cùng inventory.
- `cellId/jobId` phải là path-safe frozen identifier.
- Thêm analyzer robustness mô tả đúng sáu biến thể/five-day subset; nó không có
  confirmatory gate và không thể cứu primary.
- Analyzer primary phát decomposition exact integer giữa pickup-ETA reduction do
  lock định nghĩa và drop-ETA reduction không bị lock; treatment có pickup delta
  khác zero làm analysis fail-closed.
- Matrix được phép chọn hai job có sẵn trong plan làm audited smoke; không tạo plan
  con, không thay denominator và full run chỉ resume sau independent verification.
- Receipt v2 thêm verifier recompute file/tree hashes; receipt v1 được giữ nguyên
  như lịch sử và được v2 tham chiếu.

## Những gì không đổi

Panel 20 cell, 108 arrivals/run, 40 primary + 20 robustness jobs, B1/C1, budget
tight, margin strict `1,0 pp`, endpoint burden, service gate, selection key,
scenario derivatives, Runner artifact và chính sách negative-result đều không đổi.
Seed 19/C2/C1-unbounded vẫn chỉ là robustness mô tả, không tăng N.

Amendment này phải được hash vào freeze receipt v2 trước smoke. Bất kỳ thay đổi
outcome-bearing nào sau smoke vẫn phải invalidate affected run theo preregistration;
không được dùng amendment này làm giấy phép vá sau khi xem kết quả.
