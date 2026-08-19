# WP8 execution plan — `RB-WP8-002..014`

> Queue có thứ tự. Đúng một ticket ở trạng thái Ready tại mỗi thời điểm.
> Khoá bởi ADR-040. Không ticket nào được phép nhìn confirmatory outcome.

## Quyết định nền đã chốt

- **Pilot:** `2018-11-11` (Chủ nhật) và `2018-11-12` (Thứ hai).
- **Confirmatory holdout:** `2018-11-13` → `2018-11-18`, sáu ngày, **không chạm** cho
  tới khi preregistration đã đóng băng và hash.
- **Commitment budget:** suy từ phân phối thực nghiệm trên **chỉ dữ liệu pilot**, khoá
  thành ba mức chặt dần, giữ mức `unbounded` hiện tại làm tầng đối chứng.
- `2018-11-12` chính là ngày WP6/WP7 đã dùng, nên toàn bộ phần dữ liệu "đã bị nhìn" nằm
  gọn trong pilot. Đây là lý do chọn ngày này, và nó phải được ghi trong preregistration.

## Queue

| Ticket | Nội dung | Điều kiện đóng |
|---|---|---|
| `RB-WP8-002` **Ready** | Scenario-grid manifest source-controlled + mở rộng `RideBound.Wp6Normalize` để chạy theo grid thay vì hai profile hard-code | Manifest khai đủ day/sample/window/selectionKey/fleet; CLI từ chối cell không khai; hai clean root sinh derivative byte-exact; không đổi hành vi hai profile cũ |
| `RB-WP8-003` | Sinh và xác minh derivative cho **2 ngày pilot** | Mỗi cell có conservation `input = kept + dropped`, provenance DAG, license/DOI; verifier độc lập; confirmatory chưa được sinh |
| `RB-WP8-004` | Hợp đồng đơn vị thí nghiệm và cách gộp trong-run | Đơn vị = `(scenario, seed, travel realization)`; rider gộp lên run trước khi bootstrap; cấm coi rider trong cùng run là mẫu độc lập (`docs/11` §1, §7) |
| `RB-WP8-005` | Calculator chính xác cho primary endpoint + oracle độc lập | Endpoint phải là **tổng decision-induced burden trên các chiều**, không phải riêng pickup ETA: pilot cho thấy C1 có `prePickupInsertedStopCount = 0` theo cơ chế nên pickup-only là endpoint một arm không thể thua. Có numerator/denominator/missing rõ; oracle BCL-only không ProjectReference khớp byte-exact như chuẩn WP6-008 |
| `RB-WP8-006` | Chạy pilot matrix Layer 2 trên **chỉ 2 ngày pilot** | Paired B1/C1 mỗi cell; typed failure/exclusion; raw transcript giữ; không suy ra kết luận nào |
| `RB-WP8-007` | Ước lượng phương sai paired difference + power analysis → sample size | Sample size từ phương sai pilot và minimum detectable effect có nghĩa vận hành, cộng dự phòng failed run; cấm chọn số tròn (`docs/11` §12) |
| `RB-WP8-008` | Suy commitment budget từ pilot; ba mức strictness + tầng unbounded | Quy tắc suy dẫn khai báo trước; chỉ đọc ngày pilot; chứng minh không đọc confirmatory; mỗi mức có content hash riêng |
| `RB-WP8-009` | Non-inferiority margin, biện minh, và kiểm chất lượng endpoint | Margin đặt trước; neo vào chính sách suy biến "từ chối hết" (revision `=0`, service `=0`); dùng cận một phía; ghi rõ margin không tự bảo vệ khỏi degradation. Thêm hai điều kiện: endpoint **không được zero-inflated tới mức `p50 = p90 = 0`** (pilot cho thấy pickup-ETA rơi vào đúng trạng thái này), và không arm nào được thắng nhờ một hằng đẳng thức cơ chế. Service rate là **cổng đồng thời**, không phải secondary — pilot đã thấy một đơn vị mà C1 phục vụ ít hơn 7,5% |
| `RB-WP8-010` | Estimand, CI, multiplicity, quy tắc failed/excluded | Paired/block bootstrap trên đơn vị thí nghiệm; một primary + một non-inferiority gate; Holm cho key secondary; partition `planned = succeeded + failed + excluded` |
| `RB-WP8-011` | Preregistration document + canonical hash + cơ chế freeze | Đủ 15 mục template `docs/11` §11; hash bind config/binary/scenario/grid/analysis script; sau freeze chỉ ADR mới sửa được |
| `RB-WP8-012` | Materialize grid confirmatory 6 ngày, **không chạy** | Chỉ sinh + hash + niêm phong; bất kỳ thao tác đọc outcome nào phải fail closed |
| `RB-WP8-013` | Audit rò rỉ và cổng đối kháng | Chứng minh confirmatory chưa bị chạm; chứng minh metric/margin/threshold không dẫn xuất từ confirmatory; mutation test cho mỗi đường rò rỉ |
| `RB-WP8-014` | Đóng WP8 | Audit source/claim, ADR, review; đúng một ticket WP9 Ready; **không** kết quả effectiveness nào được công bố ở WP8 |

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
