# Research evidence archive

Thư mục này giữ các báo cáo nghiên cứu, evidence matrix và audit đã dẫn đến
RideBound. Tên lịch sử như BeGo hoặc COMMIT được giữ nguyên để bảo toàn nguồn
gốc; claim hiện hành vẫn phải theo `../01-research-charter.md` và
`../03-related-work-and-claim-boundary.md`.

## Nội dung

- Sáu báo cáo tổng hợp trong thư mục này.
- `wp5-distributed-integration-evidence-2026-08-05.md`: paper/official primary
  evidence cho transaction/outbox/idempotency/lease/recovery của ADR-025, gồm
  mapping bổ sung RIFL và Gray–Cheriton leases đã áp dụng ở RB-WP5-008/009 cùng
  end-to-end/rebuildable audit boundary đã kiểm ở RB-WP5-010; mục 11 ghi
  LDFI/Elle/QuickCheck/mutation/performance mechanisms và giới hạn claim của
  independent evidence RB-WP5-013; mục 12 ghi ba source-level closure fix và
  final gate RB-WP5-014.
- `sources/`: audit và báo cáo kỹ thuật nguồn được các báo cáo trỏ tới.
- `evidence/`: ma trận và JSON evidence của corpus 80 bài bổ sung.

PDF, digest thô và vendor checkout không được đưa lên Git vì kích thước và quyền
phân phối. Các ma trận giữ landing page/DOI công khai; đường dẫn corpus cục bộ
được ghi dưới dạng provenance, không phải liên kết repository.

Không cần đọc toàn bộ archive cho mọi task. Chỉ đọc khi thay đổi research claim,
related work, dataset choice hoặc paper-to-design mapping.
