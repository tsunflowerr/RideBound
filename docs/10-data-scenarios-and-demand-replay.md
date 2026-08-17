# Dữ liệu, scenario và demand replay

## 1. Không có một dataset làm được mọi việc

RideBound cần đồng thời:

- request theo thời gian;
- vehicle state;
- travel times;
- ground truth route execution;
- lịch sử lời hứa do chính algorithm phát.

Không dataset công khai thông thường nào chứa sẵn toàn bộ. Promise history phải được tạo bằng replay của policy, nhưng demand/travel geometry nên đến từ nguồn độc lập.

## 2. Nguồn dữ liệu đề xuất

### FleetPy benchmark datasets

Repo FleetPy công bố benchmark:

- Manhattan: [Zenodo DOI 10.5281/zenodo.15187906](https://doi.org/10.5281/zenodo.15187906);
- Chicago;
- Munich.

Đây là nguồn chính cho Layer 2. Khi tải:

- giữ nguyên archive;
- lưu SHA-256;
- ghi license/citation;
- không commit file lớn nếu license/quy mô không phù hợp.

### NYC TLC trip records

Nguồn chính thức: [NYC TLC Trip Record Data](https://www.nyc.gov/site/tlc/about/tlc-trip-record-data.page).

Dùng để lấy:

- phân bố thời gian/yêu cầu;
- origin-destination zone;
- độ cao điểm theo giờ/ngày.

Giới hạn:

- chủ yếu là chuyến đã xảy ra, không thấy nhu cầu bị reject;
- không có promise/revision;
- không đại diện trực tiếp willingness-to-share;
- lọc/sampling tạo selection bias.

Do đó chỉ dùng làm exogenous demand trace, không dùng làm nhãn hài lòng hoặc budget cá nhân.

### DARP meeting-point hiện có trong repo

Dùng cho:

- small/candidate correctness;
- meeting-point stress;
- đối chiếu backward compatibility.

Không dùng làm primary online stream nếu chưa có rule release-time độc lập và hard constraints đầy đủ.

### Li & Lim PDPTW hiện có

V1 importer BeGo chưa enforce time window cứng. Chỉ dùng sau khi RideBound importer:

- đọc đúng pickup-delivery pairs;
- enforce time window;
- giữ capacity/service time;
- có test với known feasible/infeasible cases.

### Dynamic DARP của Ackermann–Rieck

Ưu tiên nếu instance package chính thức và quyền sử dụng được xác minh. Nếu không lấy được:

- không tự tái tạo rồi gọi là benchmark của tác giả;
- có thể tạo stress scenario “inspired by” với manifest riêng.

## 3. Dữ liệu tự sinh có bias không?

Có thể, nếu generator hoặc parameter được chọn sau khi nhìn kết quả. Dữ liệu tự sinh vẫn cần cho corner case nhưng phải:

- generator độc lập policy;
- factor grid khóa trước;
- cùng seed cho B1/C1;
- code và distribution công khai;
- giữ confirmatory set chưa tune;
- không dùng để claim hành vi thật.

Synthetic data chứng minh algorithmic properties và stress robustness, không chứng minh người dùng thực sẽ phản ứng như giả định.

## 4. Scenario dimensions

Factor grid tối thiểu:

| Dimension | Mức ví dụ |
|---|---|
| Demand intensity | thấp, vừa, cao, surge |
| Fleet supply | dư, cân bằng, thiếu |
| Vehicle capacity | 2, 4, 6 |
| Epoch interval | event-driven, 30s, 60s |
| Pickup-window tightness | loose, medium, tight |
| Max detour/ride time | loose, medium, tight |
| Spatial pattern | clustered, random, mixed |
| Travel dynamics | static, smooth peak, shock/closure |
| Commitment budget | infinite, loose, medium, tight |
| Reassignment | off/on khi capability cho phép |

Mức chính thức khóa sau pilot; không chạy mọi tổ hợp nếu không đủ compute. Dùng design có coverage rõ.

## 5. Budget scenarios

### Uniform

Mọi rider cùng budget. Đây là main benchmark dễ giải thích.

### Heterogeneous synthetic

Budget lấy từ vài profile đã định trước, ví dụ `strict`, `standard`, `flexible`. Đây là stress test thuật toán nhiều policy, không đại diện nhóm người thật.

### User-provided

Chỉ dùng khi product có input thật/consent. Cần report missingness và selection bias.

Không suy budget từ tuổi/khuyết tật/thu nhập nếu chưa có ethics/data governance phù hợp.

## 6. Traffic scenarios

- Static deterministic matrix.
- Time-dependent deterministic matrix.
- Stochastic realization với cùng realized trace cho paired policies.
- Sudden edge shock.
- Vehicle breakdown incident.

Để đo decision-induced revision, lưu travel snapshot trước/sau mỗi update hoặc đủ dữ liệu để đánh giá lại old plan.

## 7. Scenario manifest

Mỗi run tham chiếu canonical scenario và plan bất biến. Ví dụ YAML/TBD cũ đã được
thay thế bởi field-level equivalent contract tại
[WP6 contract v1](benchmarking/wp6-contract-v1.md); executable JSON Schema và
published identity vectors thuộc `RB-WP6-002`.

Nguồn public đầu tiên được khóa là FleetPy Manhattan v1,
DOI `10.5281/zenodo.15187906`, CC BY 4.0, archive publisher MD5
`8b11882ae9c6d87f666bf6e006806744`. Downloader phải giữ archive trong ignored
raw cache, kiểm publisher MD5 và local SHA-256, rồi safe-extract vào content-addressed
directory. Không commit archive 408.9 MB. Scenario phải ghi rõ demand subset,
time window, fleet, node/arc ordering, unreachable-pair semantics, travel snapshot,
normalizer version và mọi source/output digest; không có `TBD`, float hoặc null.

## 8. Data pipeline

```text
immutable raw archive
-> verified extraction
-> deterministic normalization
-> canonical event stream
-> manifest/hash
-> paired policy replay
-> raw transcript
-> derived metrics
```

Không sửa file raw. Mọi normalization có version và unit test.

## 9. Data validation

- timestamp tăng hợp lệ;
- origin/destination tồn tại trong graph;
- direct travel time hữu hạn;
- pickup window hợp lệ;
- party size không vượt mọi vehicle capacity nếu không chủ ý;
- duplicate request ID;
- outlier/invalid coordinate;
- timezone/DST;
- demand count trước/sau filter;
- source checksum.

Validation failure rate phải xuất thành report, không âm thầm drop.

## 10. Pilot và holdout

- Pilot: một số ngày/khung giờ/scenario ID khóa.
- Development: synthetic/tiny fixtures.
- Confirmatory: ngày/khung giờ khác, không dùng tune.
- Cross-city: Chicago/Munich nếu data sẵn, dùng external robustness.

Không gọi cross-city là external validity hoàn chỉnh vì simulator vẫn là mô hình.

## 11. Artifact lưu

Giữ:

- manifest;
- normalized event stream hoặc script tạo lại;
- checksums;
- policy config;
- environment lock;
- raw decision transcript;
- raw per-rider metrics;
- aggregate script;
- exclusion log.

Nếu raw data không được redistrib, giữ downloader + checksum + transformation recipe.

## 12. Trạng thái WP6 closure

FleetPy Manhattan archive đã được tải vào ignored verified cache, kiểm exact release/
license/length/MD5/SHA-256 và safe-extract. Tiny/medium canonical derivatives có
conservation report; medium H/I dùng cùng scenario hash `88a8730a...e88`. Đây là
development mechanical derivative, không phải confirmatory set và không thay yêu cầu
WP7 closed-loop semantics hoặc WP8 holdout/preregistration.
