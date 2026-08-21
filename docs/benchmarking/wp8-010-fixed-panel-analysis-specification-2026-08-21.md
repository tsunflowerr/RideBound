# Fixed-panel analysis specification — RB-WP8-010

## Đơn vị và pairing

Đơn vị là `(scenarioHash, demandRealizationHash, travelRealizationHash)`. Seed
solver không thuộc identity. Mỗi pair phải có đúng arm baseline/treatment đã khai,
cùng unit, cùng arrival count **và cùng `fleetSize`**; đổi chỗ arm, cùng arm, khác
demand hoặc khác stratum năng lực đều fail closed.

Panel có 20 cell và **hai capacity stratum** `veh8`/`veh4` theo
`wp8-011d-pre-outcome-capacity-stratum-amendment.md`. Stratum không tăng N: nó là
nhân tố trong-đơn-vị, nên mỗi stratum có mẫu số riêng `20 × 108 = 2160` mỗi arm và
hai stratum không bao giờ được gộp vào một mẫu số.

## Confirmatory estimand

Panel gồm đúng 20 cell holdout. Với mỗi cell, calculator gộp rider/action thành
run-level observation trước khi trừ treatment−B1. Báo tất cả 20 delta, median,
mean, min/max và aggregate totals, **tách theo từng stratum**. Không bootstrap
rider và không coi solver seed là replication.

Gatekeeping — áp dụng độc lập cho từng stratum:

1. `Δ_service_panel > −1,0 pp` phải đạt;
2. primary burden panel phải thấp hơn B1 và paired direction của từng cell phải
   được công bố, kể cả cell ngược dấu;
3. secondary diagnostics dùng để giải thích, không cứu một gate trượt.

`veh8` là primary đã prereg; `veh4` là co-primary có điều kiện. Một stratum đạt
không cứu stratum kia trượt, và ngược lại. Capacity interaction chỉ là diagnostic
mô tả, không có gate.

## Độ chính xác đạt được phải công bố

Nửa bề rộng 95% ước tính trên panel là khoảng **1,40 pp**, lớn hơn margin
**1,0 pp**: thiết kế không đủ sức phân giải chính margin của nó. Con số này phải
xuất hiện trong báo cáo WP9 cạnh mọi kết luận gate, không được để ẩn. Panel có 20
cell nhưng chỉ **5 travel realization** (travel factor là hằng số theo ngày), nên
sàn p-value của exact sign-flip là `1/2^5 = 0,03125` và không population claim nào
khả dụng ở alpha một phía 0,025. Chi tiết đo đạc ở `wp8-007`.

Vì panel là census của danh sách scenario đã khóa, phép đánh giá confirmatory là
finite-panel exact evaluation. Bootstrap/date-cluster sensitivity được phép nhưng
phải dán nhãn exploratory; không p-value/CI nào được dùng để claim dân số.

## Failure, exclusion và rerun

`planned = succeeded + failed + excluded`; terminal record duy nhất cho mỗi run.
Chỉ data-schema rule khóa trước được exclude. Adapter/protocol/certificate/verifier
failure là failed, không phải metric 0. Infrastructure failure được chạy lại toàn
pair theo run ID mới và receipt supersession; không chạy lại vì outcome xấu.

Mọi confirmatory bundle phải qua verifier với `--require-audited-solver-evidence`:
generation conservation, không saturation/omission, solver completed/optimal,
incumbent validated, không fallback và exact objective bounds. Nếu gate đó không
đạt, run không vào estimand.

## Secondary và robustness

Secondary: material revision count, disruptive-decision count, pre-pickup insertion
và pickup/drop split. C1 unbounded tách giá lock khỏi giá budget; C2 và solver seeds
là exploratory robustness, không tăng N và không thay primary treatment đã đóng băng.

## Falsification: identity chuỗi demand+travel, không phải exogenous burden

Bản nháp trước dùng **exogenous burden làm negative control**: kỳ vọng hai arm có
cùng exogenous burden, và coi chênh lệch là tín hiệu falsification. Điều đó **sai
về nhân quả** và bị bỏ.

Exogenous burden không phải đại lượng ngoại sinh với arm. Nó được tính từ chiếu
lộ trình *đang chạy của chính xe đó* dưới travel snapshot hiện hành
(`PhysicalPlanValidator.ProbeServiceQuality`, `CommitmentDecisionValidator`
dựng `exogenousProjection` từ `oldVehicle.Route`). Từ epoch thứ hai trở đi, lộ
trình đang chạy đã là hàm của mọi quyết định trước đó, nên treatment thay đổi
chính cái baseline mà "exogenous" được đo trên. Hai arm có exogenous burden bằng
nhau không chứng minh gì, và khác nhau cũng không bác bỏ gì: đó là hậu quả hạ
nguồn của treatment, không phải input độc lập. Dùng nó làm negative control là
điều kiện hoá trên một collider.

Thay bằng instrument đúng: **khóa identity của input ngoại sinh bằng hash**. Với
mỗi cell và mỗi arm, verifier phải chứng minh hai arm nhận đúng cùng một chuỗi
ngoại sinh trước khi bất kỳ quyết định nào được đọc:

1. `demandRealizationHash` và `travelRealizationHash` của hai arm bằng nhau, và
   cùng khớp `(scenarioHash, demandRealizationHash, travelRealizationHash)` của
   cell trong panel đã đóng băng;
2. `sourceArtifactSha256`, `sourceMemberInventorySha256`,
   `normalizerSourceSha256`, `selectionFrameSha256` và
   `scenarioContentSha256` khớp derivative đã freeze;
3. chuỗi event ngoại sinh — `requestArrived` và cập nhật travel — khớp theo
   thứ tự và theo nội dung canonical, với cùng arrival denominator;
4. behavioral projection hash của một arm chạy hai lần phải bằng nhau
   (equivalence hành vi), trong khi `semanticHash` chỉ dùng cho
   provenance/integrity — đây là điểm đã sửa ở `wp8-008`.

Chỉ khi bốn điều kiện trên PASS thì cặp mới vào estimand. Đây là falsification
instrument hợp lệ vì nó kiểm *input*, thứ mà treatment theo thiết kế không được
phép chạm tới, thay vì kiểm một *output* trung gian mà treatment chắc chắn chạm tới.

Exogenous burden vẫn được **báo cáo** như diagnostic mô tả để giải thích cơ chế,
nhưng không phải control, không phải gate, và không được dùng để cứu hay bác một
kết luận nào.
