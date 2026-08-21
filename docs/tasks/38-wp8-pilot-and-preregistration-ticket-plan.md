# WP8 execution plan — `RB-WP8-002..014`

> Queue có thứ tự. Đúng một ticket ở trạng thái Ready tại mỗi thời điểm.
> Khoá bởi ADR-040. Không ticket nào được phép nhìn confirmatory outcome.

## Quyết định nền đã chốt

- **Pilot:** `2018-11-12` và `2018-11-13`. `2018-11-11` bị loại vì không có file travel-factor day-specific tương thích schema (chỉ có bản hourly 4 cột); đây là ràng buộc dữ liệu phát hiện trước khi nhìn outcome.
- **Confirmatory holdout:** `2018-11-14` → `2018-11-18`, năm ngày, **không chạm** cho
  tới khi preregistration đã đóng băng và hash.
- **Commitment budget:** suy từ phân phối thực nghiệm trên **chỉ dữ liệu pilot**, khoá
  thành ba mức chặt dần, giữ mức `unbounded` hiện tại làm tầng đối chứng.
- `2018-11-12` chính là ngày WP6/WP7 đã dùng, nên toàn bộ phần dữ liệu "đã bị nhìn" nằm
  gọn trong pilot. Đây là lý do chọn ngày này, và nó phải được ghi trong preregistration.

## Queue

| Ticket | Nội dung | Điều kiện đóng |
|---|---|---|
| `RB-WP8-002` **Done** | Scenario-grid manifest source-controlled + mở rộng `RideBound.Wp6Normalize` để chạy theo grid thay vì hai profile hard-code | Manifest khai đủ day/sample/window/selectionKey/fleet; CLI từ chối cell không khai; hai clean root sinh derivative byte-exact; không đổi hành vi hai profile cũ |
| `RB-WP8-003` **Done** | Sinh và xác minh derivative cho **2 ngày pilot** | Mỗi cell có conservation `input = kept + dropped`, provenance DAG, license/DOI; verifier độc lập; confirmatory chưa được sinh |
| `RB-WP8-004` **Done** | Hợp đồng đơn vị thí nghiệm và cách gộp trong-run | Đơn vị = `(scenarioHash, demandRealizationHash, travelRealizationHash)`; solver seed chỉ robustness; rider gộp lên run; pairing chặn cùng arm/đổi chỗ/lệch arrivals |
| `RB-WP8-005` **Done** | Calculator chính xác cho primary endpoint + oracle độc lập | Endpoint phải là **tổng decision-induced burden trên các chiều**, không phải riêng pickup ETA: pilot cho thấy chiều này zero-inflated tới `p50 = p90 = 0` nên `p95` treo trên 5–6 rider trong ~110. Có numerator/denominator/missing rõ; oracle BCL-only không ProjectReference khớp byte-exact như chuẩn WP6-008 |
| `RB-WP8-006` **Done** | Chạy pilot matrix Layer 2 trên **chỉ 2 ngày pilot** | Paired B1/C1 mỗi cell; typed failure/exclusion; raw transcript giữ; không suy ra kết luận nào |
| `RB-WP8-007` **Done** | Kiểm design adequacy và cỡ mẫu khả dụng | Bác `N=62`: panel có đúng 20 demand/travel realization; seed solver không tăng N; fixed-panel estimand thay population inference |
| `RB-WP8-008` **Done** | Frontier C1 theo budget | 25/25 pass; burden monotonic; tách giá lock/ranking khỏi giá budget; baseline equivalence dùng behavioral hash, không semantic hash |
| `RB-WP8-009` **Done** | Non-inferiority margin và kiểm chất lượng endpoint | Giữ `m=1,0 pp`; service là gate; bắt buộc phân rã locked/earned và diagnostic đuôi |
| `RB-WP8-010` **Done** | Estimand, failure/exclusion và robustness | Fixed-panel exact aggregate; không bootstrap rider/seed; audited solver evidence là admission gate |
| `RB-WP8-011` **Done** | Preregistration document + canonical hash + cơ chế freeze | 15 mục, `H0=c653c3ce…`; amendment pre-outcome bind riêng; config/binary/analysis exact |
| `RB-WP8-012` **Done** | Materialize grid confirmatory 5 ngày, **không chạy** | v1 fail node coverage; v2 uniform 108 requests materialize/reuse exact 20/20; historical `H2=97af95cf…`/`H3=d028eae4…`, current Runner-tree receipt `H4=2f7e6bf3…` |
| `RB-WP8-013` **Done** | Audit rò rỉ và cổng đối kháng | No policy outcome; selection từ H0; mutation/pairing/solver/source-inventory gates |
| `RB-WP8-014` **Done** | Đóng WP8 | Source/claim closure; mở WP9, `RB-WP9-001` đóng bằng current H4 verifier; không effectiveness claim ở WP8 |

## Ràng buộc xuyên suốt

1. Không ticket nào đọc `E:\RideBoundData\wp7\results\**` để chọn metric, threshold,
   policy hay margin. Dữ liệu đó chỉ chứng minh cơ chế.
2. Frozen-source rule của `docs/15` áp dụng cho mọi ma trận: không commit, không sửa
   script harness hay input provenance khi đang chạy.
3. Mọi run gọi đúng một Runner artifact đã pin; đổi source thì publish artifact mới và
   thư mục evidence mới, không ghi đè.
4. WP8 không mở `O-001`/`O-002`/`O-003`/`O-004` và không đổi thuật toán.
5. Pilot được phép sửa bug và chỉnh threshold; confirmatory thì không. Nếu bug được sửa
   sau khi confirmatory đã chạy, mọi điều kiện bị ảnh hưởng phải chạy lại toàn bộ.
