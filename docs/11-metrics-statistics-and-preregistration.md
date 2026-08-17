# Metrics, thống kê và preregistration

## 1. Đơn vị phân tích

- Rider-level: revision burden, wait, detour.
- Vehicle-level: VHT/VMT, occupancy.
- Epoch-level: runtime, candidate count, reject reason.
- Run-level: service rate và aggregates.
- Experimental unit chính: **scenario-seed pair**, không coi từng rider phụ thuộc trong cùng run là mẫu độc lập.

## 2. Primary outcome đề xuất

`p95_decision_pickup_eta_total_variation_ms`

Quy trình:

1. Với mỗi rider đã accept, cộng decision-induced pickup ETA variation qua vòng đời.
2. Tính `p95` trong mỗi run.
3. So paired difference giữa C1 và B1 qua các scenario-seed.

Lý do:

- bám đúng cơ chế path-dependent;
- tập trung đuôi xấu;
- không cần nhãn hành vi;
- tách được phần algorithm kiểm soát.

Primary outcome chỉ khóa chính thức sau pilot/preregistration.

## 3. Key secondary outcomes

### Commitment

- material ETA revision count/rider;
- tỷ lệ rider có `>= 3` material revisions;
- pickup/drop ETA total variation;
- max single ETA shift;
- vehicle switch count;
- pickup/drop stop switch và relocation distance;
- incumbent order inversion;
- stop insertions before pickup;
- worst normalized budget utilization;
- commitment violation rate;
- incident breach rate.

### Customer-visible

- visible ETA total variation;
- visible revision count;
- first-promised vs realized pickup/drop error.

Không gọi visible error hoàn toàn do dispatch vì traffic/model error cũng góp phần.

### Service/operations

- request accepted rate;
- completed served rate;
- accepted-to-rejected reversal count, mục tiêu `0`;
- mean/p95 wait;
- mean/p95 detour;
- VHT/VMT;
- empty/rebalancing distance nếu simulator hỗ trợ;
- occupancy/load;
- rejection reasons.

### Compute

- end-to-end decision time `p50/p95/p99`;
- candidate generation, solver, validator time;
- timeout/fallback rate;
- candidates generated/pruned;
- solver status/gap.

## 4. Distributional burden

Report:

- mean, median, p90, p95, p99, max;
- Gini revision burden;
- top-10% share;
- fraction above policy threshold.

Đây là phân phối gánh nặng giữa rider, không tự động là demographic fairness.

## 5. Service non-inferiority

RideBound không có ý nghĩa nếu giảm revision bằng cách reject gần hết yêu cầu.

Đề xuất:

\[
\Delta_{service} =
ServiceRate_{RideBound} - ServiceRate_{B1}
\]

RideBound đạt non-inferiority nếu cận dưới CI của `Δ_service` lớn hơn `-m`, trong đó margin `m` được khóa trước full run.

`m = 1` điểm phần trăm là mục tiêu minh họa hợp lý để pilot, chưa phải con số cuối.

Operational cost có thể có margin riêng hoặc report Pareto; không tự chọn margin sau khi nhìn confirmatory results.

## 6. Paired design

Với mỗi `(scenario, seed, travel realization)` chạy B1 và C1. Tính:

\[
D_j = Metric_{C1,j} - Metric_{B1,j}
\]

Sử dụng:

- paired/block bootstrap 95% CI trên experimental units;
- median paired difference;
- Wilcoxon signed-rank như sensitivity nếu phù hợp;
- standardized/nonparametric effect size.

Không chạy unpaired test nếu có paired design.

## 7. Phụ thuộc trong dữ liệu

Rider cùng run phụ thuộc do chia xe/route. Hai cách:

- aggregate mỗi run rồi bootstrap run/scenario block;
- hierarchical model với random effect scenario/run trong phân tích bổ sung.

Không giả định hàng triệu rider là hàng triệu mẫu độc lập.

## 8. Multiple comparisons

- Một primary endpoint.
- Một service non-inferiority gate.
- Key secondary family dùng Holm correction.
- Exploratory metrics ghi rõ exploratory, không dùng claim confirmatory.

## 9. Stratification

Report effect theo:

- demand intensity;
- supply-demand ratio;
- spatial pattern;
- window tightness;
- traffic dynamics;
- budget strictness;
- simulator/city.

Subgroup nhỏ không được dùng kết luận chắc nếu prereg chưa nêu.

## 10. Pilot

Pilot được phép:

- đo variance;
- kiểm runtime;
- sửa bug;
- chọn margin/threshold có lý;
- giảm factor grid vì compute.

Pilot không được:

- trở thành confirmatory data;
- chọn primary metric chỉ vì có p-value đẹp;
- chọn seed thuận lợi;
- giấu cấu hình thất bại.

Sau pilot, tạo preregistration file có hash và freeze.

## 11. Preregistration template

```text
Research questions and hypotheses
Primary outcome exact computation
Non-inferiority margin
Secondary metric family
Datasets/time ranges/scenario IDs
Inclusion/exclusion rules
Policies and config hashes
Simulator/adapters/container digests
Seeds and sample-size rationale
Compute budgets
Statistical estimand and CI method
Multiplicity correction
Missing/failed run handling
Stop/restart rules
Planned tables/plots
```

## 12. Sample size

Không chọn bằng “ít nhất 90” máy móc. Sau pilot:

- ước lượng variance của paired differences;
- chọn minimum detectable effect có ý nghĩa vận hành;
- dùng simulation/power analysis ở cấp scenario block;
- cộng dự phòng failed runs;
- khóa danh sách scenario/seed.

Số run lớn không sửa được dataset bias hoặc sai experimental unit.

## 13. Failed run

Phân loại:

- `VALID_POLICY_REJECTION`: kết quả hợp lệ.
- `SOLVER_TIMEOUT_WITH_VALID_FALLBACK`: giữ và report.
- `ADAPTER_ERROR`: invalid run.
- `DATA_INVALID`: exclude theo pre-rule.
- `CERTIFICATE_FAILURE`: implementation failure; không dùng như algorithm outcome.
- `INFRASTRUCTURE_FAILURE`: rerun theo rule.

Mọi exclusion có log. Không bỏ run vì metric xấu.

WP6 cụ thể hóa quy tắc này thành partition hữu hạn:

```text
planned = succeeded + failed + excluded
```

Mỗi `runId` có đúng một terminal record. Timeout, process exit, protocol/schema/hash,
resource-limit và metric-oracle mismatch là typed failure; chúng không được đổi thành
giá trị metric 0. Exclusion chỉ hợp lệ khi pre-rule đã khóa trước khi nhìn outcome và
phải ghi rule/evidence. Mọi metric record phải mang numerator, denominator, unit,
missing-count/reason và source transcript digest. Aggregate không được âm thầm bỏ
missing/failed run; production calculator phải được so với oracle độc lập không gọi
lại chính calculator đó.

Closure WP6 ngày 2026-08-13 giữ đúng partition ở tiny A và medium H/I: mỗi process
`planned=8, succeeded=8, failed=0, excluded=0`. Production/oracle 132 rows mỗi run
byte-exact; 72/72 per-run semantic fields lặp lại. Full resource rows khác 8/8 và
được giữ, không dùng làm effectiveness/SLA. Aggregate CI/non-inferiority vẫn chưa chạy
vì thuộc WP8/WP9.

## 14. Success criteria

Một kết luận RideBound mạnh cần đồng thời:

1. Primary revision outcome cải thiện với CI phù hợp.
2. Service rate đạt non-inferiority.
3. Accepted-to-rejected reversal bằng 0 trong normal operation.
4. Certificate failure bằng 0.
5. Runtime đáp ứng deadline đã khóa.
6. Hướng hiệu ứng được lặp trong FleetPy.
7. Cross-system layer không mâu thuẫn không giải thích được.

Nếu chỉ đạt 1 nhưng không đạt 2, kết luận là trade-off không thực dụng ở cấu hình đó.

## 15. Bảng/plot bắt buộc

- Revision-service Pareto curve.
- ECDF/CDF của per-rider total variation.
- Tail plot p90–p99.
- Acceptance/non-inferiority CI.
- Runtime distribution.
- Reject reason breakdown.
- Budget utilization heatmap theo dimension.
- Layer/city effect forest plot.
- Incident vs normal-operation breakdown.
