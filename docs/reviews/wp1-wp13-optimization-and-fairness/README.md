# Review WP1–WP13 — vì sao benchmark thấp, code đã tối ưu chưa, và so sánh có công bằng không

> Ngày: 2026-08-24
> Phạm vi: đọc logic từng file trên đường tối ưu hóa, đo lại bằng raw evidence đã
> freeze, không chạy lại simulator, không sửa H6
> Trạng thái: `REVIEW_COMPLETE`; mọi con số dưới đây đến từ artifact đã hash

Review này không dựa vào test suite để kết luận. Test chỉ chứng minh code làm đúng
điều nó nói; câu hỏi ở đây là điều nó nói có đúng là điều nên làm hay không. Vì
vậy mỗi kết luận đều được đo lại từ 80 bundle E1 v1.2 đã đóng băng
(44.156 solver decisions), bằng một probe có source control và test riêng.

Công cụ đo: [`wp14_mechanism_probe.py`](../../../simulators/fleetpy-ridebound/wp14_mechanism_probe.py),
test [`test_wp14_mechanism_probe.py`](../../../simulators/fleetpy-ridebound/tests/test_wp14_mechanism_probe.py),
receipt [`wp14-001-mechanism-probe-v1-summary.json`](../../benchmarking/evidence/wp14-001-mechanism-probe-v1-summary.json).

## 0. Kiểm tra độ tin cậy trước khi kết luận

Probe được viết độc lập với analyzer của WP9 và chạy trên E1 (replay có
instrumentation v1.2), rồi đối chiếu với con số H6 đã công bố:

| Đại lượng | WP9 công bố | Probe đo lại | Khớp |
|---|---:|---:|---|
| Panel A B1 completed | 1.735 | 1.735 | có |
| Panel A C1 completed | 1.581 | 1.581 | có |
| Panel B B1 completed | 966 | 966 | có |
| Panel B C1 completed | 860 | 860 | có |
| Panel A B1 burden (ms) | 74.443.002 | 74.443.002 | có |
| Panel A C1 burden (ms) | 128.020 | 128.020 | có |
| Panel B B1 burden (ms) | 44.766.809 | 44.766.809 | có |
| Panel B C1 burden (ms) | 342.974 | 342.974 | có |
| Panel A pickup component | 9.579.869 | 9.579.869 | có |
| Panel B pickup component | 5.056.311 | 5.056.311 | có |

Khớp tuyệt đối 10/10. Kết quả H6 tái lập được bằng một đường đo hoàn toàn khác.

## 1. Vì sao benchmark thấp — câu trả lời ngắn

C1 mất `−154` (Panel A) và `−106` (Panel B) completions **không phải** vì sinh
candidate kém, không phải vì solver kém, không phải vì cap/work budget, và gần như
không phải vì ranking. Toàn bộ cơ chế mất dịch vụ quy về **đúng hai con số cấu
hình**, và cả hai đều nằm trong một file JSON:

[`wp8-drop-eta-budget-tight-v1.json`](../../../benchmarks/configurations/wp8-drop-eta-budget-tight-v1.json)

1. `drop_eta_total_ms.hardLimit = 30000` — tổng biến phân `Σ|Δ|` của drop-ETA
   trong toàn vòng đời request không được vượt 30 giây.
2. `finalConfirmationLocks = [vehicle, pickupStop, pickupEta]` — áp dụng cho
   **toàn bộ** phase `waitingPickup`, nên pickup ETA bị đóng băng tuyệt đối kể từ
   lúc booking confirmation.

Đo trên toàn bộ 80 bundle:

| | Panel A C1 | Panel B C1 |
|---|---:|---:|
| commitment prunes | 940 | 583 |
| … `COMMITMENT_BUDGET_EXCEEDED` / `drop_eta_total_ms` | 780 | 491 |
| … `COMMITMENT_PHASE_LOCK` / `pickup_eta_ms` / `final_confirmation` | 160 | 92 |
| vehicle choice set bị gate làm rỗng hết option actionful | 534 | 339 |
| request bị chặn ngay lập tức (không còn đường nào phục vụ) | 143 | 212 |
| completions mất so với B1 | 154 | 106 |

**Không có witness nào khác tồn tại.** 8 trên 10 dimension của commitment vector
không sinh một witness nào trong cả 44.156 decision. `vehicle_switch_count` và hai
`*_stop_switch_count` có `hardLimit = 0` nhưng không bao giờ bị vi phạm, vì
generator không sinh candidate đổi xe hay đổi stop — O-001 đã cấm ở tầng domain
cho **cả hai** arm.

143 và 212 request bị chặn tức thời so với 154 và 106 completions bị mất: cùng cỡ
độ lớn. Đây là association mô tả, không phải decomposition nhân quả — một request
bị chặn ở epoch này vẫn có thể được nhận sau, và Panel B chặn nhiều hơn nhưng mất
ít hơn vì baseline của nó vốn đã bỏ rất nhiều khách.

## 2. Vì sao nới budget không cứu được — hình dạng phân phối

Câu hỏi tự nhiên là “vậy nâng 30 s lên 60 s hoặc 120 s thì sao”. Đo phân phối
tiêu thụ tích luỹ `drop_eta_total_ms` trên arm **không bị ràng buộc** (B1), tức là
lượng chỉnh sửa mà một chính sách tự do thực sự cần:

| Ngưỡng | Panel A B1 (n=1.735) | Panel B B1 (n=966) |
|---|---:|---:|
| tiêu thụ bằng 0 | 1.332 (76,8%) | 706 (73,1%) |
| `> 0 ms` | 403 (23,2%) | 260 (26,9%) |
| `> 30.000 ms` | 369 (21,3%) | 230 (23,8%) |
| `> 60.000 ms` | 341 (19,7%) | 207 (21,4%) |
| `> 120.000 ms` | 230 (13,3%) | 140 (14,5%) |
| `> 300.000 ms` | 48 (2,8%) | 30 (3,1%) |
| p90 / p95 / p100 (ms) | 154.821 / 234.222 / 573.491 | 159.580 / 244.069 / 630.599 |

Phân phối **lưỡng cực**: hoặc không đụng gì, hoặc đụng rất lớn. Gần như không có
khối lượng nào nằm giữa 0 và 30 giây.

Hệ quả trực tiếp: budget vô hướng là một **knob rất tệ** để dựng Pareto frontier.
Đi từ 30 s lên 60 s chỉ gỡ được 1,6 điểm phần trăm số request bị ràng buộc
(369 → 341). Đi tận lên 300 s — tức là gần như từ bỏ lời hứa — vẫn còn 2,8% bị
ràng buộc. Đây chính là lý do ablation của WP9 thấy C1 unbounded vẫn `−3,7037 pp`.

Điều này cũng định hình lại câu hỏi nghiên cứu. Việc phải đánh đổi không phải là
“sửa ETA một chút để phục vụ thêm khách”, mà là: **nhận thêm một khách, đổi lại
đẩy drop-ETA của một khách đang chờ lùi 2,5–9,5 phút.** Hard commitment chính là
luật từ chối đánh đổi đó. H6 nói cái giá của việc từ chối là 7,13 pp.

## 3. Ba phát hiện đọc từ logic mà test không bắt được

### 3.1 Một lexicographic level của C1 là hằng số — chứng minh được, và đo được

[`HardVectorCandidateAssessor.CalculateWorstUtilization`](../../../src/RideBound.Algorithms/Commitments/HardVectorCandidateAssessor.cs)
có nhánh:

```csharp
if (hardLimit == 0)
{
    if (value != 0) { /* fail */ }
    worst = PartsPerMillion;   // 1_000_000
    continue;
}
```

Vì cấu hình H6 đặt `hardLimit = 0` cho ba dimension **luôn luôn thoả**, `worst`
bị ép lên 1.000.000 — trần tuyệt đối — cho mọi candidate của mọi vehicle đang có
rider đã mở promise. `Math.Max` phía sau không bao giờ vượt được nữa.

Level `worst-hard-utilization-ppm` do đó là hằng số. Đo trên recorded objective
bounds:

- Panel A C1: hằng số `1000000` ở **13.004/13.004** decision (100,00%);
- Panel B C1: **8.022/8.022** (100,00%).

Đây là level lexicographic **hạng hai**, ngay dưới `accepted-request-count`. Nó
là thành phần “ranking theo mức tiêu thụ commitment” — điểm phân biệt được tuyên
bố của treatment. Trong cấu hình đã đo, nó không mang một bit thông tin nào.

### 3.2 Ranking của C1 gần như không hoạt động; mất mát nằm hoàn toàn ở gate

C1 chèn 11 level giữa `accepted-request-count` và `operational-cost`. Đo khả năng
phân biệt bên trong tập option mà solver thực sự nhận (Panel A, 3.688 vehicle
choice set có ≥2 option):

| Level | phân biệt được |
|---|---:|
| `worst-hard-utilization-ppm` | 0,000% |
| `revision:pickup_eta_total_ms` | 0,000% |
| `revision:vehicle_switch_count` | 0,000% |
| `revision:pickup_stop_relocation_mm` | 0,000% |
| `revision:pickup_stop_switch_count` | 0,000% |
| `revision:drop_stop_relocation_mm` | 0,000% |
| `revision:drop_stop_switch_count` | 0,000% |
| `revision:incumbent_order_inversion_count` | 0,000% |
| `revision:drop_eta_total_ms` | 2,088% |
| `revision:material_eta_revision_count` | 2,088% |
| `revision:pre_pickup_inserted_stop_count` | 1,193% |
| `operational-cost` | 100,000% |

Nếu xếp lại theo thứ tự của B1 (`accepted`, rồi `operational-cost`) trên **cùng**
tập eligible, lựa chọn cục bộ chỉ đổi ở 28/3.794 = 0,738% vehicle set.

Kết luận: trong cấu hình H6, C1 **không phải** “B1 cộng ranking commitment-aware”.
Nó là “B1 áp lên tập option đã bị gate cắt”. Điều này khớp với WP13 (generated set
bằng nhau, mất mát ở prune) và bổ sung phần WP13 cố ý không đo: objective profile.

Đây cũng là một giới hạn phải ghi trong bài: attribution `lock/ranking −3,7037 pp`
của WP9 thực chất là **lock**, không phải ranking — vì `hardTreatmentActive` vẫn
bật ở arm “C1 unbounded” nên hierarchy vẫn còn nguyên ở đó, mà nó gần như bất hoạt.

### 3.3 “Giảm burden 99,83%” là con số attributed, không phải con số rider cảm nhận

Metric của gate là `Σ decisionDelta.pickupEtaTotalMs + dropEtaTotalMs` — chỉ tính
phần **do quyết định của operator gây ra**. Đo thêm phần rider thực sự nhìn thấy,
tức tổng biến phân giữa hai lần publish liên tiếp bất kể nguyên nhân:

| | Panel A B1 | Panel A C1 | giảm | Panel B B1 | Panel B C1 | giảm |
|---|---:|---:|---:|---:|---:|---:|
| attributed (metric gate) | 74.443.002 | 128.020 | **99,83%** | 44.766.809 | 342.974 | **99,23%** |
| experienced (rider thấy) | 83.576.558 | 9.322.567 | **88,85%** | 48.881.344 | 4.191.282 | **91,43%** |
| request có promise từng đổi | 1.734/1.735 | **1.581/1.581** | — | 966/966 | **860/860** | — |
| request bị tính burden | 426 | 16 | — | 277 | 31 | — |

Ba điều rút ra:

1. Giảm thật vẫn rất lớn, nhưng là **88,85%/91,43%**, không phải 99,83%/99,23%.
2. **100% rider ở cả hai arm đều thấy lời hứa của mình thay đổi ít nhất một lần.**
   C1 không cung cấp “ETA không đổi”; nó cung cấp “operator không bao giờ chủ động
   đổi ETA của bạn”. Đây là hai lời tuyên bố rất khác nhau và tài liệu claim hiện
   tại chưa tách chúng.
3. Exogenous drift gần như bằng nhau giữa hai arm (pickup 2.482.019 với 2.506.462;
   drop 6.709.435 với 6.688.485). Đây là một kiểm tra nội tại rất mạnh: hai arm
   thực sự chạy trên cùng một hiện thực giao thông.

## 4. Code đã tối ưu chưa

### 4.1 Đúng: các bảo đảm chính đều chặt

- Solver là lexicographic đa pass có fixing optimum thật, không phải weighted sum
  giả lexicographic ([`OrToolsCandidateSelectionSolver`](../../../src/RideBound.Solvers.OrTools/OrToolsCandidateSelectionSolver.cs)).
- Gate fail-closed: `HardVectorCandidateAssessor` dừng cả run nếu một vehicle mất
  hết option kể cả no-op, thay vì lặng lẽ trả no-op.
- `CandidateStateApplicator` không mutate state gốc; snapshot copy collection.
- Candidate cap và work budget **không bao giờ chạm**: 0/104.672 vehicle-epoch
  Panel A và 0/32.412 Panel B có `candidateCapApplied`, `workBudgetExhausted`, hay
  `omittedFeasibleCandidateCountByCap`. Không arm nào mất option vì truncation.

### 4.2 Thiếu sót lớn nhất: 97% pass CP-SAT là thừa, và chứng minh được là thừa

Một lexicographic level mà **mọi** candidate của **mọi** vehicle đều đóng góp cùng
một giá trị thì hằng số trên toàn tập khả thi (aggregation `sum` và `maximum` đều
tách được trên ràng buộc chọn đúng một candidate mỗi vehicle). Pass CP-SAT cho
level đó chỉ đang chứng minh optimum của một hằng số, và ràng buộc
`objective == optimum` mà nó sinh ra là vô hiệu.

Đo trên toàn bộ:

| Nhóm | level dựng | level thoái hoá | tỷ lệ |
|---|---:|---:|---:|
| Panel A B1 | 141.330 | 133.409 | **94,40%** |
| Panel A C1 | 273.884 | 266.765 | **97,40%** |
| Panel B B1 | 53.016 | 49.735 | **93,81%** |
| Panel B C1 | 136.860 | 133.822 | **97,78%** |

Chi tiết Panel A C1: `worst-hard-utilization-ppm` 13.004/13.004 (100%),
`revision:*` 129.851/130.040 (99,85%), `candidate-id-rank:*` 100.878/104.672
(96,38%), và ngay cả `accepted-request-count`/`operational-cost` cũng thoái hoá
88% thời gian vì đa số decision chỉ có một option mỗi vehicle.

C1 giải trung bình **20,93 model CP-SAT mỗi decision**; chỉ khoảng 0,54 trong số
đó thực sự quyết định điều gì.

Một pre-solve check `O(candidates × levels)` — rẻ hơn nhiều bậc so với dựng và
giải một model — sẽ bỏ qua các pass này với **quyết định bất biến**. Đây không
phải micro-optimization: WP13-002 đã từng fail đúng ceiling CPU 120 s
(120.062/120.000 ms) trên medium public drain, và một ablation matrix của WP14 sẽ
nhân chi phí đó lên nhiều lần.

Tối ưu này **trung tính giữa hai arm** (giúp cả B1 lẫn C1) nên không làm lệch so
sánh. Nó được xếp thành ticket implementation đầu tiên của WP14 kèm nghĩa vụ chứng
minh và mutation test, chứ không sửa trong một ticket refinement.

### 4.3 Thiếu sót vừa: attribution prune bị cắt ngắn

[`CommitmentDecisionValidator`](../../../src/RideBound.Application/Commitments/CommitmentDecisionValidator.cs)
duyệt request theo thứ tự ID và **return ngay** khi request đầu tiên fail; trong
một request, lock được kiểm trước budget và cũng return ngay. Do đó witness ghi
được là “first failing request, first failing layer”, không phải tập đầy đủ.

Trong dữ liệu đã đo, mỗi candidate bị prune chỉ mang đúng một witness, và hai
dimension quan sát được rời nhau, nên tỷ lệ 83%/17% vẫn dùng được như mô tả. Nhưng
nó là attribution phụ thuộc thứ tự, không phải phân rã đầy đủ, và tài liệu phải
nói vậy. Fail-fast là đúng cho hot path; successor evidence profile nên ghi đủ
witness khi profile bật.

### 4.4 Thiếu sót nhỏ: hai cơ chế đã cài đặt nhưng chưa từng được đo

- `CommitmentPolicy.FreezeHorizon` + `FreezeHorizonLocks` tồn tại, có validation,
  có test, và **chưa có một configuration nào trong repo dùng nó**. Toàn bộ 5 file
  commitment config đều để trống `freezeHorizonMs` và thay bằng
  `finalConfirmationLocks` áp lên cả phase. Cơ chế ít phá hoại hơn hẳn — và là
  cơ chế chuẩn trong literature — chưa bao giờ được chạy.
- `CommitmentBudgetBasis.CustomerVisible` tồn tại nhưng cả 5 config đều dùng
  `decisionInduced`.

### 4.5 Một chi tiết ngữ nghĩa

`CommitmentLockEvaluator` so `previous.PickupEta != candidate.PickupEta`, nên một
thay đổi làm rider được đón **sớm hơn** cũng bị coi là vi phạm lock. Với pickup
điều này còn bào chữa được (rider lên kế hoạch quanh giờ đã hứa). Với drop thì
không, nhưng drop dùng budget chứ không dùng lock, và budget cộng `|Δ|` nên cải
thiện cũng đốt budget như suy giảm.

Điều đáng nói là: **đo ra thì chuyện này không quan trọng.** Trong 403 request
Panel A B1 có tiêu thụ drop-ETA, **403/403 (100%) là suy giảm ròng**, 0 cải thiện;
tổng `|net displacement|` bằng 99,9% tổng biến phân. Nghĩa là hai “cải tiến” nghe
rất hợp lý — chỉ tính phần xấu đi, hoặc neo vào net thay vì total variation — sẽ
**không thay đổi gì cả**. Chúng bị loại khỏi factor matrix của WP14 nhờ phép đo
này, không phải nhờ lập luận.

## 5. So sánh có công bằng không

### 5.1 Những điểm đã kiểm và đạt

| Kiểm tra | Kết quả |
|---|---|
| Cấu hình hai arm | Giống hệt nhau trừ `policyId`; cùng cap, cùng work budget, cùng `randomSeed: 7`, cùng adapter OR-Tools |
| B1 có thực sự không bị ràng buộc | **0** commitment prune trong 14.133 decision Panel A và 8.836 Panel B |
| Candidate generation | WP13 xác nhận generated set bằng nhau exact 40/40 pair; probe xác nhận cap không bao giờ chạm |
| Physical prune | `PICKUP_WINDOW`, `MAX_RIDE_TIME` do generator sinh, chung cho cả hai arm, không tính vào gate |
| Retention khi cap chạm | `CandidatePortfolioRetainer` giữ **cả** biến thể rẻ nhất (có lợi cho B1) **và** biến thể ổn định nhất (có lợi cho C1) cho từng service set; phase A ưu tiên bản rẻ nhất trước. Trong H6 cap không chạm nên điểm này chỉ là dự phòng |
| Hiện thực ngoại sinh | Exogenous drift chênh nhau dưới 1% giữa hai arm ở cả hai dimension |
| Dữ liệu | Xem 5.3 |

### 5.2 Điểm bất đối xứng duy nhất tìm được — và nó nghiêng về phía bất lợi cho luận điểm của chúng ta

`CommitmentLockEvaluator` hard-code `activeLocks = PromiseLock.Vehicle` với rule
`accepted_assignment`, **không phụ thuộc policy**. Nó áp cho cả B1. Kết hợp với
O-001/ADR-018, nghĩa là B1 cũng không được đổi xe cho request đã accept.

Baseline chuẩn của literature thì được. Alonso-Mora et al. nói rõ: “A request
might be rematched to a different vehicle in subsequent iterations as long as its
waiting time does not increase and until it is picked up by some vehicle.”

Vậy B1 của RideBound **yếu hơn** baseline chuẩn. Ràng buộc này đối xứng nên phép
so sánh vẫn hợp lệ, nhưng hệ quả phải nói thẳng: `−7,1296 pp` và `−4,9074 pp` là
**cận dưới** của cái giá so với một baseline reassignment đầy đủ, không phải cận
trên. Không có chỗ nào trong thiết kế làm treatment trông tốt hơn thực tế; chỗ
lệch duy nhất làm nó trông **đỡ tệ hơn** thực tế.

Ngoài ra, cả hai arm đều không có rebalancing/repositioning xe rỗi. Alonso-Mora
báo rebalancing tăng service rate khoảng 20%. Vì thiếu ở cả hai arm nên so sánh
nội bộ vẫn công bằng, nhưng **tỷ lệ hoàn thành tuyệt đối** (80,3% Panel A,
44,7% Panel B) không được đem so với con số của literature.

### 5.3 Dữ liệu có thật không

| Điểm kiểm | Kết quả |
|---|---|
| Nguồn | FleetPy Manhattan public derivative, TLC 2018, Zenodo DOI `10.5281/zenodo.15187906`, CC BY 4.0 |
| Đường dẫn | 24.974 record nguồn → 2.757 eligible → 108 selected mỗi cell |
| Lý do loại | **Duy nhất một mã**: `source.outside-window` (22.217 record). Không có bộ lọc nào theo outcome, theo độ khó, hay theo lợi thế của arm nào |
| Chọn mẫu | `greedy-induced-coverage-node-pool-hmac-row-v1` — deterministic, khoá HMAC cố định, quyết định trước khi biết kết quả |
| Cửa sổ | 2018-11-14 13:00–15:00 UTC, horizon 7.200.000 ms, drain tới 14.400.000 ms |
| Commitment policy | `wp6-synthetic-policy-overlay-v1` — **được khai báo rõ là synthetic overlay** trong ATTRIBUTION.md của mỗi derivative, vì dữ liệu nguồn không chứa preference/satisfaction thật |
| Ràng buộc vật lý | pickup window 600.000 ms, max ride time 1.500 permille — chung cho cả hai arm |
| Cùng realization | Panel A/B dùng cùng demand và travel realization đã verify bằng nhau; chỉ khác initial fleet state |

Không tìm thấy dấu hiệu “chế biến” dữ liệu theo hướng có lợi. Điểm phải nói rõ
trong bài là commitment policy **là tổng hợp**, không đến từ dữ liệu quan sát —
điều này repo đã ghi sẵn trong attribution và trong claim boundary.

## 6. Rủi ro claim còn lại

| Rủi ro | Trạng thái | Hành động |
|---|---|---|
| “Giảm burden 99,8%” bị đọc thành “ETA của rider ổn định” | **Chưa được tách trong tài liệu** | Bổ sung attributed vs experienced vào claim boundary; xem 3.3 |
| “C1 xếp hạng theo mức tiêu thụ commitment” | Không đúng trong cấu hình đã đo | Ghi rõ level đó là hằng số; xem 3.1 |
| Attribution WP9 `lock/ranking −3,7037 pp` | Thực chất là lock, không phải ranking | Ghi lại trong limitation |
| Tỷ lệ hoàn thành tuyệt đối so với literature | Không so được (không có rebalancing) | Chỉ so nội bộ giữa hai arm |
| Baseline yếu hơn chuẩn literature | Có, hướng bất lợi cho treatment | Ghi là cận dưới |
| Prune attribution phụ thuộc thứ tự | Có | Successor evidence ghi đủ witness |

## 7. Việc kế tiếp

Refinement `RB-WP14-001` trong
[`tasks/43`](../../tasks/43-wp14-exploratory-ablation-refinement.md) chuyển các
phát hiện trên thành factor matrix và gate, với ba ràng buộc cứng: không dùng bất
kỳ số đo nào của H6/E1 panel để **chọn** mức factor, không sửa H6, và không chọn
policy v2. Ordered queue nằm ở
[`tasks/44`](../../tasks/44-wp14-exploratory-ablation-ticket-plan.md).
