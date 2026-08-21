# RB-WP8-001 — Pilot và preregistration refinement

> Trạng thái: Done — 2026-08-21; WP8 đóng bằng ADR-043, freeze vận hành hiện hành H4
>
> Đây là ticket refinement. Nó **không** viết production code và **không** tạo kết quả.
> Nó quyết định thí nghiệm sẽ trông như thế nào, rồi khoá quyết định đó bằng ADR trước
> khi bất kỳ con số nào được nhìn.

## 1. Vì sao WP8 tồn tại

Hết WP7, RideBound có một đường so sánh **cơ học đã công bằng**: hai arm dùng chung
scenario, seed, Runner binary, work budget, candidate pool và đường publication; hai
config chỉ khác đúng `policyId`. Cái chưa có là **một thí nghiệm**.

Cụ thể, ba thứ còn thiếu, và không thứ nào tự đóng được bằng chạy thêm run:

1. **n = 1 hiện thực nhu cầu.** Ba repeat mỗi arm là kiểm tính tất định, không phải ba
   mẫu. Không có phương sai thì không có suy luận.
2. **Chưa preregister.** Endpoint, denominator, margin, quy tắc loại trừ và phương pháp
   phân tích chưa khoá. Nhìn số rồi mới chọn là HARKing.
3. **Treatment chưa được kiểm ở phần đắt nhất của nó.** Xem §4.

## 2. Dữ liệu thật đã đủ để đóng khoảng trống thứ nhất

Đây là kết quả kiểm tra nguồn, không phải đề xuất. Bộ FleetPy Manhattan v1 đã tải và
xác minh (Zenodo DOI `10.5281/zenodo.15187906`, CC BY 4.0) chứa **289 file demand**:

| Chiều phân tầng (`docs/11` §9) | Nguồn thật tương ứng |
|---|---|
| Ngày / day-of-week | 8 ngày liên tiếp `2018-11-11` → `2018-11-18`, gồm cuối tuần |
| Cường độ nhu cầu | `sample_{5,10,20,50,75}_{1..4}` — sample chính thức của publisher |
| Tỷ lệ đặt trước | `res{5,10,25,50,75,100}_{1..5}` |
| Traffic dynamics | `tt_factors` theo ngày và theo giờ |
| Cửa sổ thời gian | `sourceWindowStartSeconds` / `EndSeconds` |
| Supply–demand ratio | `vehicleCount` |
| Window tightness | `pickupWindowMs`, `maximumRideTimePermille` |

`FleetPyNormalizationConfiguration` **đã nhận đủ** mọi tham số trên. Chỉ có
`tools/RideBound.Wp6Normalize` là hard-code đúng một cell
(`2018-11-12_sample_10_1.csv`, cửa sổ `0..86400`, 128 request / 32 xe). Vì vậy việc
sinh grid là mở rộng CLI, không phải viết lại normalizer.

Hệ quả quan trọng: **grid thí nghiệm dựng được hoàn toàn từ dữ liệu công khai thật.**
Không có demand tự chế, không có tinh chỉnh nào có lợi cho treatment.

## 3. Tách pilot khỏi confirmatory

`docs/09` §8 bắt buộc tách. Tám ngày cho một cách tách tự nhiên và khoá được **trước**
khi nhìn bất kỳ outcome nào. Nguyên tắc: pilot chỉ dùng để ước lượng phương sai, kiểm
runtime, sửa bug và biện minh margin; confirmatory set không được chạm tới cho đến khi
preregistration đã đóng băng và hash.

Quy tắc bắt buộc kèm theo: nếu một bug được sửa sau khi confirmatory đã chạy, mọi điều
kiện bị ảnh hưởng phải chạy lại toàn bộ; không được vá lẻ.

## 4. Vấn đề thật cần quyết: chính sách cam kết đang là synthetic

Đây là hạn chế lớn nhất về mặt nội dung và nó **không phải lỗi**, mà là hệ quả của việc
dữ liệu công khai không chứa chính sách hứa hẹn nào.

`wp6-public-mechanical-commitment-v1.json` khai 10 chiều nhưng chỉ 3 chiều có
`hardLimit`, và cả ba đều bằng `0`:

| Chiều | hardLimit | Ghi chú |
|---|---|---|
| `vehicle_switch_count` | 0 | luôn thoả sẵn vì O-001 đã khoá reassignment |
| `pickup_stop_switch_count` | 0 | generator không di dời stop incumbent |
| `drop_stop_switch_count` | 0 | như trên |
| 7 chiều còn lại | *(không có)* | unbounded |

Hệ quả: trong cấu hình hiện tại, **phần "hard vector" của C1 gần như không ràng buộc gì**.
Ba giới hạn bằng 0 bão hoà đồng đều trên mọi candidate nên không phân biệt được ai với
ai. Thứ thực sự tách C1 khỏi B1 hiện nay là **thứ tự ưu tiên revision**, không phải ngân
sách cứng.

Điều đó không làm phép so sánh mất công bằng — hai arm vẫn cùng điều kiện. Nhưng nó làm
**claim** bị lệch nếu ta gọi kết quả là bằng chứng cho "hard commitment budget".

## 5. Phương pháp non-inferiority — nguồn primary

Đọc full text, không dùng snippet:

| Nguồn | Nguyên tắc áp dụng |
|---|---|
| *Non-inferiority statistics and equivalence studies* (PMC7808096) | Margin phải được **đặt và ghi lại trước khi thí nghiệm bắt đầu**; quyết định dùng cận một phía không vượt margin |
| Cùng nguồn | Margin phải neo vào một tham chiếu; nếu margin rộng hơn tham chiếu thì "không thua kém" trở nên vô nghĩa |
| *Choice of NI margins does not protect against degradation* (PMC4117500) | Có margin **không** tự bảo vệ khỏi suy giảm hiệu quả; phần lớn NI trial biện minh margin sơ sài |
| EMA CHMP guideline on the choice of the non-inferiority margin | Được ghi nhận là nguồn quy chuẩn; PDF không đọc được dạng text tại ngày kiểm tra nên **không** trích nội dung |

Ánh xạ sang RideBound: vai trò "placebo" do **chính sách suy biến từ chối hết yêu cầu**
đảm nhiệm — nó có revision `= 0` và service rate `= 0`. Margin trên service rate vì thế
phải đủ hẹp để C1 không thể thắng endpoint chính bằng cách hạ dịch vụ. `docs/11` §5 đề
xuất `m = 1` điểm phần trăm làm minh hoạ; con số cuối phải được biện minh từ phương sai
pilot cộng lập luận vận hành, và phải khoá trước confirmatory.

## 5.1 Kết quả pilot đầu tiên đã bác bỏ điểm vận hành cũ

Chạy pilot trên chính điểm vận hành WP6/WP7 (128 request rải 24 giờ, 32 xe) cho kết quả
dứt khoát, đọc trực tiếp từ transcript thô của cả hai arm:

| | B1 | C1 |
|---|---:|---:|
| decision-induced pickup ETA (tổng) | **0** | **0** |
| exogenous = visible | 3.459 ms | 3.640 ms |
| material revision | 3 | 0 |
| Σdelta so với budget tích luỹ | 0 lệch | 0 lệch |

100% chuyển động lời hứa là ngoại sinh. Ở mật độ này đội xe gần như luôn rảnh nên thuật
toán chưa bao giờ phải xáo trộn một lời hứa nào. **Primary endpoint đề xuất ở `docs/11`
§2 bằng 0 đồng nhất**, nên không cỡ mẫu nào cứu được: thí nghiệm cầm chắc null và null
đó không nói gì về treatment.

Điểm vận hành thay thế dùng **cấu trúc thật của dữ liệu**, không bịa tải: cửa sổ cao
điểm thật `08:00–10:00` (2.286 bản ghi nguồn) với 8 xe thay vì 32. Chỉ đổi cửa sổ và số
xe; mọi tham số khác giữ nguyên. Ở đó thuật toán thực sự phải chèn stop trước pickup của
khách hiện hữu và gây revision thật — `publicationCount` tăng từ 482 lên 3.904, promise
version cao nhất từ 13 lên 90.

Việc đổi điểm vận hành diễn ra **trước khi** nhìn bất kỳ so sánh B1/C1 nào ở điểm mới, và
được thúc đẩy bởi một quan sát cơ chế (`decisionDelta ≡ 0`), không phải bởi kết quả. Nó
phải nằm trong preregistration.

## 5.2 Endpoint pickup-ETA bị zero-inflated tới mức không dùng được

Ở điểm có tải, pickup-ETA decision variation bằng 0 cho C1 ở cả ba run đã chạy, và bằng
0 cho **cả B1** ở một run. Với các run còn lại của B1 thì chỉ 5–6 rider trên khoảng 110
có giá trị khác 0, nên `p50 = p90 = 0` ở mọi run.

Ban đầu có vẻ như C1 ép chiều này về 0 theo cấu tạo, nhưng dữ liệu bác bỏ điều đó:
`C-d20181112-r2-c1` có `prePickupInsertedStopCount = 3` mà pickup decision variation vẫn
bằng 0 — tức C1 **có** chèn trước pickup của khách hiện hữu, chỉ là chèn vào phần slack
sẵn có nên không đẩy ETA nào. Vậy đây không phải hằng đẳng thức.

Vấn đề thật thì vẫn nghiêm trọng và đủ để loại endpoint này: `p95` của một phân phối mà
90% giá trị bằng 0 chỉ phản ánh một nhúm rider, nên nó cực kỳ nhạy với việc thêm bớt vài
quan sát. Đó là một primary outcome giòn, không phải một phép đo ổn định.

Gánh nặng cũng **chuyển chiều** chứ không biến mất: burden của C1 dồn gần như toàn bộ
sang drop ETA. Endpoint vì thế phải là **tổng decision-induced burden trên các chiều**,
để việc dịch chuyển hiện ra thay vì bị giấu. `RB-WP8-005` phải khoá theo hướng này, và
`RB-WP8-009` phải kiểm cả hai điều: endpoint không được zero-inflated tới mức p50 = p90
= 0, và không arm nào được thắng nhờ một hằng đẳng thức cơ chế.

## 5.3 Đánh đổi dịch vụ đã xuất hiện ngay ở pilot

Trong hai đơn vị paired đầu tiên, một đơn vị cho thấy C1 phục vụ **ít hơn 8 rider**
(99 so với 107, tức −7,5%) trong khi giảm burden. Đơn vị kia phục vụ đúng bằng nhau.

Đây chính xác là kịch bản `docs/11` §5 cảnh báo: giảm revision bằng cách nhận ít khách
hơn. Nó cũng cho thấy cổng non-inferiority không phải thủ tục hình thức — một mức giảm
7,5 điểm phần trăm sẽ vượt xa margin minh hoạ `m = 1` điểm phần trăm.

Không được rút kết luận nào từ hai quan sát. Điều rút ra được là: **service rate phải là
cổng đồng thời, không phải secondary metric**, và cỡ mẫu phải đủ để ước lượng nó chứ
không chỉ ước lượng burden.

## 6. Những gì WP8-001 phải khoá

1. grid scenario từ dữ liệu thật: ngày, sample fraction, cửa sổ, selection key, fleet size;
2. tách pilot / confirmatory holdout, khoá trước khi chạy;
3. đơn vị thí nghiệm và cách gộp trong-run (`docs/11` §1, §7);
4. công thức chính xác của primary endpoint;
5. non-inferiority margin và cách biện minh;
6. sample size từ phương sai pilot, không phải con số tròn;
7. quy tắc failed/excluded và multiplicity;
8. phạm vi claim, đặc biệt là câu hỏi ở §4;
9. định dạng preregistration file và cách hash/đóng băng.

## 7. Điều WP8-001 không được làm

- không nhìn kết quả medium WP7 để chọn metric, threshold, policy hay margin;
- không đổi thuật toán để kết quả đẹp hơn;
- không mở O-001/O-002/O-003/O-004;
- không tuyên bố bất kỳ kết quả effectiveness nào.

## 5.4 Hai arm không đối xứng — điều này quyết định cách được phép kết luận

Đọc `SolverBackedRidePoolingPolicy.Decide` cho thấy khác biệt cấu trúc, không phải khác
biệt tham số:

- `RollingCost` (B1) đi thẳng qua `switch` mà **không có bộ lọc cam kết nào**; solver
  nhận toàn bộ pool candidate.
- `RideBoundHardVector` (C1) chạy `_hardAssessor.AssessAndFilter`, **loại bỏ** mọi
  candidate không qua validator cam kết.

Vì vậy **pool của C1 là tập con của pool của B1**. Cùng solver, cùng accepted-count xếp
đầu trong objective, nên accepted count của C1 **không thể lớn hơn** B1 ở từng epoch, và
chênh lệch tích luỹ qua các epoch.

Ba hệ quả bắt buộc cho preregistration:

1. Phát biểu "C1 phục vụ ít hơn" gần như là hệ quả cấu trúc, **không phải kết quả thực
   nghiệm**. Preregister nó như một hypothesis là vô nghĩa.
2. Ngược lại, bất kỳ headline dạng "C1 tốt hơn B1" đều **sai về bản chất phép so**: đây
   không phải hai đối thủ ngang hàng mà là *có ràng buộc* so với *không ràng buộc*.
3. Kết luận hợp lệ duy nhất là **đường đánh đổi**: thêm bảo đảm cam kết thì mất bao nhiêu
   dịch vụ. Đó đúng là `revision-service Pareto curve` mà `docs/11` §15 đã liệt kê là
   bảng/plot bắt buộc.

Kiểm chứng kèm theo: trong bốn đơn vị pilot, **100% rejection ở cả hai arm là
`PICKUP_WINDOW` hoặc `MAX_RIDE_TIME`** — không một rejection nào do ngân sách cam kết.
Nghĩa là thiếu hụt dịch vụ do `PhysicalPlanValidator` tuyên bố, một trọng tài dùng chung
và giống hệt nhau ở hai arm, chứ không do luật nào dự án tự viết ra cho có lợi.

## 5.5 Ba giới hạn cứng hiện **không ràng buộc gì** — kiểm chứng bằng thực nghiệm

§4 mới chỉ nghi ngờ điều này từ việc đọc config. Pilot đã kiểm chứng trực tiếp.

Một stratum nới lỏng được tạo bằng cách bỏ `hardLimit` khỏi `pickup_stop_switch_count`
và `drop_stop_switch_count`, giữ `vehicle_switch_count = 0` vì O-001 ràng buộc cả hai
arm. Chạy lại C1 trên cùng cell `C-d20181112-r2`:

| | completed | burden | material | prePickupInserted |
|---|---:|---:|---:|---:|
| C1 @ strict (cả ba limit = 0) | 99 | 758.435 | 10 | 3 |
| C1 @ relaxed (bỏ hai stop-switch limit) | **99** | **758.435** | **10** | **3** |

Giống hệt tới từng chữ số. Kết luận: **hai giới hạn đó chưa bao giờ cắt một candidate
nào**. Generator không di dời stop của khách hiện hữu, nên chúng luôn tự thoả.

Ba hệ quả:

1. Trong cấu hình hiện tại, **C1 thuần tuý là một chính sách xếp hạng theo revision**.
   Phần "hard vector" — thứ được coi là đóng góp chính của treatment — đang không hoạt
   động. Mọi claim về "hard commitment budget" hiện **không có bằng chứng nào chống lưng**.
2. Thiệt hại dịch vụ **không đến từ ràng buộc cứng**. Nó đến từ thứ tự ưu tiên: trong số
   các plan cùng accepted count, C1 chọn plan ít xáo trộn nhất, và plan đó đặt đội xe vào
   vị trí kém hơn cho các yêu cầu đến sau. Đây là tổn thất **myopic tích luỹ**, không phải
   pruning.
3. Núm điều chỉnh để dựng Pareto vì thế **không phải** các hard limit. Nó là vị trí của
   revision vector trong thứ tự lexicographic so với cost. Ackermann & Rieck cùng dòng
   service-consistency đều đã cảnh báo rằng tối ưu cứng một mục tiêu làm giảm flexibility;
   cơ chế soft-limit của C2 (`commit-soft-hard-hybrid`) là điểm trung gian **đã được khai
   báo sẵn** trong dự án và là ứng viên đúng cho frontier.

`RB-WP8-008` vì thế đo trực tiếp C1 qua bốn mức drop-ETA budget và tách hai giá:
B1 → C1 unbounded là giá của lock/ranking; C1 unbounded → C1 finite là giá thêm
của budget. C2 không được dùng để cứu cổng chính sau khi nhìn pilot; nó chỉ còn là
ablation thăm dò WP9 với nhãn claim riêng.

## 5.6 Hiệu chỉnh đơn vị thí nghiệm sau audit

`masterSeed` đi vào solver tie-breaking, không tạo demand hay travel realization mới.
Do đó đơn vị canonical là `(scenarioHash, demandRealizationHash,
travelRealizationHash)`. Năm ngày holdout × bốn sample của publisher tạo đúng 20
đơn vị hữu hạn, không phải 62. WP9 đánh giá fixed panel và không claim population
non-inferiority; thay đổi seed chỉ là robustness. Quyết định này giữ nguyên margin
1,0 pp và tránh pseudo-replication.
