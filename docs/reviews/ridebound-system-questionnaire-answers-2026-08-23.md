# RideBound — trả lời 155 câu hỏi về hệ thống

Ngày đối chiếu: 2026-08-23  
Phạm vi trạng thái: WP0–WP10 đã hoàn tất; WP9 cho kết quả xác nhận âm, WP10 cho kết quả Layer 3 âm; WP11–WP12 chưa bắt đầu.

## Cách đọc

Tài liệu này trả lời theo **trạng thái có bằng chứng trong repository**, không đoán ý định cá nhân của chủ dự án. Ba trạng thái logic được phân biệt:

- **Đã khóa**: contract, ADR, preregistration hoặc artifact đã freeze.
- **Đã đo**: kết quả thực nghiệm/benchmark hiện có.
- **Chưa quyết định**: repository không có quyết định hay bằng chứng đủ để trả lời thay chủ dự án.

Khi tài liệu lịch sử mâu thuẫn với trạng thái mới, nguồn ưu tiên là `docs/18-status-and-decision-log.md`, ADR đang hiệu lực, mã nguồn hiện tại và artifact đã freeze. Tài liệu này là bản tổng hợp, không thay thế các nguồn gốc đó và không nới claim boundary.

## Snapshot hiện tại

| Evidence | B1 | C1 | Kết quả |
|---|---:|---:|---|
| WP9 Panel A, 8 xe, 2.160 arrivals/arm | 1.735 completed; 74.443.002 ms burden | 1.581 completed; 128.020 ms burden | service −154 = −7,13 pp: **FAIL**; burden: **PASS** |
| WP9 Panel B, 4 xe, 2.160 arrivals/arm | 966 completed; 44.766.809 ms burden | 860 completed; 342.974 ms burden | service −106 = −4,91 pp: **FAIL**; burden: **PASS** |
| WP10 RidePy Layer 3 | — | — | capability gate: **FAIL**, không chạy effectiveness matrix |

Final repository verification được ghi ở closure: .NET 855/855, FleetPy 95/95, RidePy 23/23; các con số này mô tả source tree đã review trước tài liệu tổng hợp này.

## A. Mục tiêu và tiêu chí thành công

### 1. Nếu chỉ được nói một câu, RideBound muốn giải quyết vấn đề gì?

RideBound nghiên cứu liệu một hệ thống ride-pooling online có thể cấp và kiểm chứng **ngân sách thay đổi lời hứa tích lũy theo từng hành khách, nhiều chiều và xuyên nhiều decision epoch**, mà không làm giảm đáng kể khả năng phục vụ và hiệu quả vận hành hay không.

### 2. Câu hỏi nghiên cứu trung tâm có thay đổi không?

**Chưa đổi về bản chất.** Câu hỏi trung tâm vẫn là đánh đổi giữa kiểm soát cumulative promise revision và service/efficiency. Tuy nhiên H6 đã trả lời âm cho treatment C1 hiện tại trên hai panel: cơ chế làm burden gần bằng không nhưng service giảm 7,13 pp ở 8 xe và 4,91 pp ở 4 xe, đều vượt xa margin −1 pp. Vì vậy bước tiếp theo không được giả định cơ chế hiện tại thành công; phải chọn giữa phân tích nguyên nhân, thiết kế treatment mới, hoặc đóng gói kết quả âm.

### 3. RideBound cuối cùng hướng tới loại dự án nào?

Theo roadmap hiện tại, đây là **kết hợp** của:

- framework nghiên cứu có core và protocol độc lập simulator;
- prototype chứng minh cơ chế ledger/budget/lock/certificate;
- artifact tái lập cho luận văn/paper;
- đường tích hợp production-like với BeGo.

Nó **chưa phải sản phẩm production**. WP11 (Product UX) và WP12 (paper/release) chưa bắt đầu.

### 4. Thành công cuối cùng của dự án là gì?

Research charter ban đầu đòi đồng thời: correctness/certificate, revision giảm, service không kém quá ngưỡng đã đăng ký, runtime khả dụng và evidence xuyên layer. Với C1/H6, tiêu chí effectiveness về service đã **thất bại** dù burden gate pass. Thành công khoa học còn khả thi là một trong hai hướng:

1. tìm treatment mới đạt trade-off tốt hơn bằng một preregistration mới; hoặc
2. công bố trung thực cơ chế, hạ tầng tái lập và kết quả âm có giới hạn rõ.

Publication, production và một mức giảm revision cụ thể ngoài H6 **chưa được khóa** làm điều kiện bắt buộc cuối cùng.

### 5. Vì sao revision/commitment quan trọng?

Vấn đề hệ thống nhắm tới là lời hứa sau khi đã công bố có thể bị churn qua nhiều lần tái tối ưu: pickup/dropoff ETA thay đổi, đổi assignment, đổi pickup/dropoff point, đảo thứ tự incumbent hoặc chen thêm stop trước pickup. Final delta có thể nhỏ nhưng hành khách vẫn đã trải qua nhiều lần nhiễu. RideBound biến lịch sử này thành ledger đơn điệu, certificate kiểm được và hard constraint theo policy.

Đây mới là **proxy kỹ thuật cho độ ổn định lời hứa**; repository chưa có user study chứng minh trực tiếp tác động tới hài lòng.

### 6. “Promise” về mặt sản phẩm nghĩa là gì?

Trong model hiện tại, promise là một snapshot có version cho từng request, gồm ít nhất vehicle assignment, pickup/dropoff stop, pickup/dropoff ETA và thông tin thứ tự/route liên quan. Mỗi lần publish tiếp theo tạo delta, cập nhật ledger và certificate. Về UX tương lai có thể diễn đạt thành “xe nào, đón ở đâu, khoảng khi nào và mức nào được phép thay đổi”, nhưng câu chữ public cụ thể chưa được chọn.

### 7. Ai hưởng lợi trực tiếp?

- **Hành khách**: lời hứa ổn định và giải thích được.
- **Operator/dispatcher**: policy có thể audit, phát hiện breach và tái lập quyết định.
- **Nhà nghiên cứu/kỹ sư**: benchmark, protocol và certificate độc lập simulator.
- **Tài xế** hưởng lợi gián tiếp từ kế hoạch ít churn hơn; chưa có driver-facing treatment hoặc metric riêng.

### 8. Nếu phải đánh đổi thì ưu tiên gì?

Trong implementation hiện tại, thứ tự bắt buộc là:

1. tính đúng, safety/physical feasibility và invariant “accepted không bị reject/reassign”;
2. tối đa service/accepted count;
3. với C1, giảm worst hard-budget utilization rồi revision vector;
4. giảm operational cost;
5. tie-break xác định.

Fairness hiện chỉ là ranh giới báo cáo/chẩn đoán, không phải objective. Một thứ tự ưu tiên sản phẩm giữa ETA stability, ride time, efficiency và fairness **chưa được chủ dự án quyết định**.

## B. B1, C1, C2, ngân sách và lock

### 9. Mô tả B1 bằng lời

B1 (`rolling-cost`) là baseline online rolling insertion: ở mỗi epoch, nó sinh cùng raw physical candidate pool như C1, tối đa số request mới được nhận, rồi tối thiểu hóa operational cost và tie-break theo ID ổn định. Nó vẫn tôn trọng capacity, time windows, max ride time, frozen/executed prefix, accepted assignment và onboard safety, nhưng không dùng cumulative commitment budget của C1 để loại candidate.

### 10. B1 được phép thay đổi gì?

- **Đổi xe passenger đã accepted:** không; invariant O-001 khóa assignment.
- **Đổi thứ tự incumbent stop:** generator hiện không reorder các incumbent tương đối với nhau.
- **Đổi pickup/dropoff ETA:** có thể, vì chèn stop mới vào mutable suffix làm dịch ETA.
- **Thay route sau publish:** có thể thay mutable suffix bằng insertion mới; executed/frozen prefix không đổi.
- **Relocate stop cũ:** generator hiện không thực hiện.

Vì vậy B1 “unbounded commitment” không có nghĩa là bỏ các ràng buộc vật lý hay cho phép reassignment tùy ý.

### 11. C1 khác B1 chính xác ở bước nào?

C1 dùng cùng state, event, raw candidate generator, cap, work budget, solver version và seed. Sau khi có raw physical pool, C1 còn:

1. project promise mới và tính decision-induced delta;
2. kiểm tra phase lock;
3. kiểm tra hard budget theo từng dimension và ledger;
4. loại candidate hard-invalid;
5. dùng objective/ranking commitment-aware;
6. phát hành ledger/certificate sau khi solution đã được validate.

### 12. C1 thêm constraint nào B1 không có?

Trong H6 tight policy:

- `drop_eta_total_ms` có hard cumulative limit 30.000 ms/request;
- `vehicle_switch_count`, `pickup_stop_switch_count`, `drop_stop_switch_count` có hard limit 0;
- final confirmation khóa vehicle, pickup stop và pickup ETA;
- onboard khóa vehicle, pickup stop và pickup ETA.

Các dimension khác là unbounded trong policy này. Một số switch constraint không binding trong H6 vì generator/O-001 đã không tạo hành vi đó.

### 13. C1 chỉ thêm feasibility constraint hay còn đổi objective?

**Cả hai.** C1 lọc hard-invalid candidate và đổi lexicographic objective thành: tối đa accepted → tối thiểu worst hard-budget utilization → tối thiểu vector revision 10D theo thứ tự khóa → tối thiểu operational cost → stable ID. Vì vậy cụm “lock/ranking” trong ablation không được hiểu là chỉ lock.

### 14. 30-second budget nghĩa chính xác là gì?

Đó là hard limit **30.000 ms tổng biến thiên tích lũy của dropoff ETA do quyết định hệ thống gây ra, cho mỗi request, trong toàn lifecycle promise**. Không phải 30 giây mỗi epoch, không phải final delta, không phải global fleet budget. Nó là một dimension (`drop_eta_total_ms`) trong ledger 10D.

### 15. Vì sao chọn 30 giây?

Đây là mức `tight` được khóa từ pilot cùng các band 60/120 giây để thăm dò frontier trước khi mở holdout. Repository không có bằng chứng người dùng hay literature chứng minh 30 giây là ngưỡng cảm nhận đúng. Do đó phải gọi nó là **experimental design parameter**, không phải product guarantee có căn cứ hành vi.

### 16. Mười dimension của ledger là gì?

Theo thứ tự canonical:

1. `pickup_eta_total_ms`;
2. `drop_eta_total_ms`;
3. `material_eta_revision_count`;
4. `vehicle_switch_count`;
5. `pickup_stop_relocation_mm`;
6. `pickup_stop_switch_count`;
7. `drop_stop_relocation_mm`;
8. `drop_stop_switch_count`;
9. `incumbent_order_inversion_count`;
10. `pre_pickup_inserted_stop_count`.

### 17. Budget là per passenger, vehicle, route hay global?

Ledger/balance là **per request/passenger, per dimension, xuyên lifecycle**. Policy có thể dùng cùng giới hạn cho nhiều người nhưng số đã tiêu không gộp theo xe, route hay toàn fleet.

### 18. Budget đã tiêu có được hoàn lại không? Vì sao?

Không. Ledger đo total variation/path length: A→B→A vẫn tiêu cả hai lần thay đổi. Nếu hoàn lại theo final delta, optimizer có thể làm lời hứa dao động nhiều lần rồi quay về gần giá trị đầu và trông như không gây burden. Monotone/no-refund giúp hard bound có ý nghĩa, audit đơn giản và certificate không phải sửa lịch sử.

### 19. Lock của RideBound là gì?

Lock là ràng buộc nói một phần của state/promise không còn được phép thay đổi ở một lifecycle phase hoặc freeze horizon. Nó khác budget: budget cho phép thay đổi tới giới hạn tích lũy; lock yêu cầu đúng bất biến ngay tại candidate.

### 20. Một promise chuyển sang hard lock khi nào?

Không có một thời điểm duy nhất cho mọi field:

- accepted assignment được khóa ngay khi accepted;
- booking confirmation mở promise ledger trong H6 và kích hoạt final-confirmation locks;
- onboard kích hoạt onboard locks;
- executed/frozen route prefix luôn bất biến theo physical state;
- freeze-horizon lock có thể cấu hình, nhưng H6 C1 không dùng horizon tùy chọn đó.

### 21. Hiện có những loại lock nào?

Model/evaluator hỗ trợ assignment/vehicle lock, pickup/dropoff stop lock, pickup/dropoff ETA lock, ordering/prefix constraints và freeze-horizon semantics. H6 tight thực sự khóa vehicle + pickup stop + pickup ETA ở final confirmation/onboard; route prefix đã executed/frozen được physical validator bảo vệ. Không nên nói toàn bộ dropoff route đã hard-lock, vì policy H6 không làm vậy.

### 22. Lock hiện tại hard hay có penalty mềm?

Các lock là **hard**. C2 có warning threshold mềm để ranking excess, nhưng warning không biến hard lock thành penalty và không cho phép candidate vượt hard limit.

### 23. “Ranking” trong “lock/ranking” là gì?

Đó là thứ tự chọn candidate của C1 sau khi qua hard gate: accepted count, worst utilization, từng thành phần revision 10D, cost và stable IDs. Arm `C1-unbounded` trong robustness vẫn giữ final-confirmation locks và ranking này nhưng bỏ giới hạn ETA hữu hạn, nên chênh B1→C1-unbounded được gọi gộp là “lock/ranking price”.

### 24. C2 là gì và khác C1 ở đâu?

C2 (`soft-hard-hybrid`) giữ cùng hard validation như C1, nhưng thêm vector **warning excess** vào lexicographic objective trước raw revision vector. Trong WP9 exploratory arm, C2 dùng loose hard policy 120 giây và warning 60 giây. Nó không cho hard-invalid candidate đi qua.

### 25. C2 được tạo ra để kiểm tra giả thuyết gì?

C2 kiểm tra liệu một hybrid có vùng cảnh báo mềm trước hard bound có thể tìm điểm Pareto trung gian, phục hồi service mà vẫn kiểm soát burden hay không. Kết quả robustness chỉ phục hồi 2 completions so với C1 tight và vẫn −7,04 pp so với B1; analyzer gắn `descriptiveOnlyCannotRescuePrimary`, nên C2 không cứu được H6 primary.

## C. Lifecycle, trạng thái xe và event

### 26. Lifecycle request thực tế có những state nào?

Enum hiện tại là:

`Pending`, `Accepted`, `WaitingPickup`, `Onboard`, `Completed`, `Rejected`, `CancelledBeforeAcceptance`, `CancelledAfterAcceptance`.

Luồng thường: `Pending → Accepted → WaitingPickup → Onboard → Completed`; nhánh kết thúc khác là `Pending → Rejected/CancelledBeforeAcceptance` hoặc `Accepted/WaitingPickup → CancelledAfterAcceptance`. Không có state `Assigned` hay `IncidentFailed` riêng trong enum hiện tại.

### 27. Từ thời điểm nào passenger được coi là committed?

Trong H6/FleetPy, initial promise trigger là **booking confirmation**, tương ứng chuyển sang `WaitingPickup`. `Accepted` trước đó là offer/provisional assignment; invariant accepted-assignment vẫn bảo vệ xe, nhưng ledger promise chưa mở cho tới confirmation.

### 28. Trước pickup, passenger có thể bị từ chối sau khi accept không?

Không có transition `Accepted → Rejected`. Request có thể bị hủy/decline và đi vào `CancelledAfterAcceptance`, nhưng optimizer không được dùng “reject lại” để thoát commitment. WP9 terminal contract thậm chí cấm cancellation để bảo toàn exact arrived=accepted+rejected.

### 29. Sau accept có được reassignment sang xe khác không?

Không. O-001 và hard vehicle-switch/assignment semantics giữ accepted request trên cùng xe; candidate vi phạm bị loại.

### 30. Sau pickup còn được thay đổi gì?

Pickup stop/ETA và vehicle đã là lịch sử/bị khóa. Dropoff ETA và mutable suffix phía trước dropoff về mô hình có thể thay đổi nếu physical constraints, lock và budget cho phép. H6 không hard-lock dropoff ETA, nhưng generator hiện cũng không relocate stop cũ hay reorder incumbent; thay đổi chủ yếu đến từ chèn request mới vào suffix.

### 31. Frozen prefix nghĩa chính xác là gì?

Là danh sách stop đầu route đã executed hoặc bị khóa, kèm `executedStopCount`, mà mọi candidate phải giữ nguyên **đúng thứ tự và nội dung**. Chỉ `mutableSuffix` phía sau mới được xây candidate mới.

### 32. Khi xe đã bắt đầu chạy tới một stop, route đó có immutable không?

Trong FleetPy adapter, directed leg đang chạy được coi là hard-frozen; adapter không force-assign route làm sai vị trí giữa cạnh. Core protocol còn biểu diễn được directed-edge progress. Ở RidePy native CPE chỉ có node snapshot, nên chưa chứng minh được invariant mid-edge tương đương; đó chính là capability gate WP10 thất bại.

### 33. Xe dùng mô hình vị trí nào?

Core contract là tagged union:

- `node` với `nodeId`; hoặc
- directed edge với `fromNodeId`, `toNodeId`, `edgeId`, `progressPermille` từ 1–999.

Không dùng raw GPS trong Domain. Simulator adapter phải ánh xạ state native sang một trong hai dạng mà không bịa dữ liệu. FleetPy cung cấp edge progress; RidePy adapter hiện chỉ có `nodeOnly`.

### 34. Decision chạy khi nào?

Event-driven, với nhiều trigger đã định nghĩa: request arrival, booking/stop reach/board/alight, travel-time update, timer tick, cancellation và incident. Adapter batch các event có cùng simulation time theo protocol; H6 scenario chủ yếu có 108 request-arrival events rồi các lifecycle event do driver phát sinh.

### 35. Có nhiều event cùng timestamp không?

Có. Protocol cho phép event batch chứa nhiều event cùng `simulationTimeMs`.

### 36. Thứ tự event cùng timestamp được quyết định thế nào?

Mỗi event có `eventSeq` toàn cục tăng đơn điệu. Runner áp dụng chính xác thứ tự này; canonicalization và transcript digest bảo vệ thứ tự. Wall-clock, thread schedule hay thứ tự dictionary không được phép quyết định kết quả.

## D. Candidate generation

### 37. Khi request mới tới, RideBound bắt đầu sinh candidate thế nào?

Runner reduce event vào canonical online state, lấy các request `Pending` theo thứ tự `(latestPickup, arrivalTime, requestId)`, rồi với từng xe bắt đầu từ route hiện tại/no-op. Generator best-first lần lượt thử nhánh bỏ qua request hoặc chèn pickup/dropoff vào mutable suffix, đánh giá schedule/physical feasibility và giữ một portfolio hữu hạn, xác định.

### 38. Candidate là gì?

Một vehicle candidate là **một route plan hoàn chỉnh cho xe ở epoch đó**, kèm tập request mới được insert, schedule/cost, provenance và generation evidence. Insertion positions là cách sinh candidate; solver nhìn candidate plan đã sinh chứ không nhìn một cặp request–vehicle rời rạc.

### 39. Với route A→B→C và request X→Y, thử vị trí nào?

Nếu cả ba stop nằm trong mutable suffix, pickup X có thể ở bốn khe: trước A, A–B, B–C, sau C. Với mỗi vị trí pickup, dropoff Y được thử ở mọi khe phía sau pickup để giữ precedence. Tổng số cặp vị trí là `4 + 3 + 2 + 1 = 10`, cộng nhánh skip/no-op. Mỗi cặp còn phải qua validator; frozen prefix không phải vùng insertion.

### 40. Có enumerate tất cả hay prune trước?

Trong bounded production mode, generator **không hứa enumerate toàn bộ không gian lớn**. Nó dùng deterministic best-first search, prune infeasible branch và dừng theo work/candidate bounds. Trong exact-small scope, oracle độc lập enumerate đầy đủ để so sánh.

### 41. Các rule prune cụ thể là gì?

Các lớp chính gồm:

- sai cấu trúc route, pickup sau dropoff, duplicate/unknown request;
- network không kết nối hoặc schedule không tính được;
- capacity/occupied-seat violation;
- thay frozen/executed prefix, accepted assignment hoặc onboard state;
- vi phạm pickup window, max ride time, plan version/location;
- branch không còn tiềm năng tốt theo deterministic frontier bound;
- vượt request-bundle, work-unit hoặc candidate-retention cap.

Commitment lock/budget không được dùng để tạo raw pool khác giữa B1 và C1; nó được assess sau raw generation.

### 42. Candidate generation có những limit nào?

Có cap số candidate/vehicle, tối đa request mới/vehicle, deterministic exploration work units và tùy chọn repair-request scope. WP9 không dùng wall-clock timeout, beam width hoặc một route-depth cutoff riêng làm budget chính. Route dài vẫn làm số insertion/work tăng và có thể chạm bound.

### 43. “Bounded candidates” trong WP9 bị bound bởi gì?

Cấu hình chung B1/C1 là:

- tối đa 100 candidate/vehicle;
- tối đa 2 request mới/vehicle candidate;
- 10.000 generation work units;
- 10.000 validation work units;
- retention theo service-set stability/earliest-feasible semantics;
- solver work/conflict budget 100.000 và deterministic-time 1.000.000 micro-units.

Evidence còn báo exact/saturated omission count và digest để không im lặng che việc cắt không gian.

### 44. Candidate bị loại theo thứ tự nào?

Thứ tự hiện tại, giản lược nhưng đúng implementation, là:

1. reduce event và kiểm state/version;
2. sinh common raw route proposals từ mutable suffix;
3. kiểm structure/network/precedence/capacity/frozen/accepted/onboard/schedule;
4. giữ portfolio/cap chung và phát generation evidence;
5. với C1/C2: apply candidate → project promise → phase lock → per-dimension budget → ledger/full commitment validation;
6. giữ candidate hard-valid, ánh xạ objective vector;
7. OR-Tools chọn một candidate/xe;
8. independent full-fleet validation và certificate trước publish/ACK.

Do fairness requirement, physical portfolio/cap chung xảy ra trước treatment-specific hard gate; không phải C1 được sinh một pool thuận lợi riêng.

### 45. Candidate nào bị loại vì physical infeasibility?

Candidate không bảo toàn route structure/precedence, vượt capacity, không reachable, vi phạm pickup window/max ride time, sửa frozen prefix, đổi accepted vehicle, làm sai onboard/occupied-seat state, dùng stale plan version hoặc không có schedule hợp lệ. Safety no-op có cơ chế báo exogenous breach thay vì che lỗi.

### 46. Candidate nào bị loại vì commitment?

Candidate vật lý hợp lệ nhưng sau projection:

- thay field đang bị phase lock;
- làm một ledger dimension vượt hard limit;
- không khớp prior certificate/promise lineage;
- gây switch bị hard-zero;
- hoặc không qua full commitment validator.

Warning excess của C2 chỉ đổi ranking; bản thân warning không loại candidate nếu hard limits vẫn thỏa.

### 47. Có log request-level “B1 nhận, C1 reject vì X” không?

Raw transcripts có action theo request, promise/certificate và candidate-generation evidence. Tuy nhiên analyzer WP9 **chưa dựng phép ghép nhân quả request-level xuyên hai arm** kiểu “B1 phục vụ request R, C1 mất đúng vì constraint X”. Hai arm đã đi qua state trajectory khác nhau, nên reason tại epoch cuối không tự động là nguyên nhân phản thực.

### 48. Có thống kê rejection reason theo từng constraint không?

Có rejection reason/witness ở từng run và reason chỉ được nêu cụ thể khi các witness phù hợp, nhưng báo cáo H6 **không có bảng phân loại 154 request** theo budget/lock/ranking/capacity/... Muốn có bảng đó cần một post-outcome analyzer mới, ghi rõ exploratory và định nghĩa attribution trước khi chạy.

### 49. Candidate tốt có thể bị prune trước OR-Tools không?

Về nguyên tắc **có** trong bounded mode; OR-Tools chỉ chọn những gì generator đưa vào. Nhưng các run H6 đã audit báo không saturation/omission, solver hoàn tất tối ưu và không fallback, nên không có bằng chứng generator cap là nguyên nhân aggregate của kết quả WP9.

### 50. Có exact-small oracle không?

Có. Oracle độc lập enumerate trong published small scope (tối đa 2 xe, 2 pending request; repair fixture có scope riêng), chạy 64 fixtures/seeds và so production B1/C1+OR-Tools với optimum. Gap bằng 0 trong scope đó. Điều này kiểm logic nhỏ, không chứng minh global optimum ở instance lớn.

## E. Mô hình OR-Tools và runtime decision

### 51. Đầu vào OR-Tools trông như thế nào?

Mô hình khái niệm:

```text
Vehicles   = các vehicleId ở epoch hiện tại
Requests   = pending request có mặt trong candidate pool
Candidates = route plan đã sinh/validate cho từng xe, gồm no-op
Constraints= đúng 1 candidate/xe; mỗi request được nhận tối đa 1 lần
Objective  = các level integer lexicographic theo arm
```

`CandidateSelectionModel`/`CandidateSelectionCandidate` nằm ở Application để không phụ thuộc OR-Tools; adapter CP-SAT chuyển model này sang native variables/constraints.

### 52. OR-Tools có tự xây route không?

Không. Route/insertion/schedule đã được RideBound sinh và validate. OR-Tools chỉ chọn một tổ hợp candidate plans tương thích toàn fleet.

### 53. Solver đang giải loại bài toán gì?

Đây là **CP-SAT candidate selection**, gần assignment/set packing trên các plan đã enumerate, không dùng OR-Tools Routing để xây tuyến. Nó kết hợp đúng-một-plan-per-vehicle với at-most-one-assignment-per-request.

### 54. Decision variable chính là gì?

Mỗi `(vehicle, candidate)` có Boolean `x[v,c] ∈ {0,1}`. Constraint tổng `x` của mỗi xe bằng 1; tổng `x` của các candidate chứa cùng một request không vượt 1.

### 55. Constraint solver hiện gồm gì?

- chọn đúng một candidate cho mỗi xe, bao gồm no-op;
- mỗi pending request được nhận nhiều nhất một lần;
- domain/integer bounds cho objective terms;
- ở các pass sau, cố định giá trị objective pass trước **chỉ khi pass trước proven optimal**.

Physical, schedule, lock và budget chủ yếu được validate trước model, rồi solution còn bị revalidate toàn state sau solver. O-001 không chỉ dựa vào CP-SAT.

### 56. Objective tối ưu theo thứ tự nào?

- **B1:** tối đa accepted count → tối thiểu operational cost → stable candidate-ID ranks.
- **C1:** tối đa accepted → tối thiểu worst hard-budget utilization → tối thiểu lần lượt 10 thành phần decision-induced revision → tối thiểu cost → stable IDs.
- **C2:** như C1 nhưng thêm warning-excess vector sau hard utilization và trước raw revision.

`completed` là metric cuối run; objective online trực tiếp là số request mới được accepted trong selection.

### 57. Weighted sum hay lexicographic?

Lexicographic nhiều pass. Không cộng service, burden và cost thành một scalar có weight tùy ý.

### 58. Các weight hiện tại là bao nhiêu?

Không có cross-objective weights. Mỗi level có dominance tuyệt đối theo thứ tự trên; bên trong level dùng tổng hoặc maximum integer canonical. Lý do là tránh một hệ số scale vô tình đổi ưu tiên service/commitment và giữ tie-break giải thích được.

### 59. Ranking xảy ra trước hay trong OR-Tools?

Cả hai tầng có vai trò khác nhau: generator/retainer dùng deterministic priority để tạo common bounded portfolio; sau đó các objective levels của B1/C1/C2 được encode trong `CandidateSelectionModel` và tối ưu bởi OR-Tools. Stable ID ranking là pass cuối của solver. “Ranking price” trong robustness bao gồm treatment-specific selection objective, không chỉ một sort trước solver.

### 60. OR-Tools có timeout không?

Nó có **deterministic work/conflict/time budget**, seed cố định và một worker; không dùng wall-clock để quyết định incumbent. Supervisor/harness vẫn có wall/CPU/RSS limits để đánh dấu run failure, nhưng chúng không được phép đổi semantic decision silently. WP9 dùng solver work budget 100.000 và deterministic-time 1.000.000 micro-units.

### 61. Nếu hết budget/không có solution thì fallback làm gì?

Executor chỉ nhận solution đã qua validation. Fallback thử canonical no-op, rồi các single-request option theo đúng objective/tie-break. Nếu validation budget cũng cạn hoặc không có safe option, nó trả `UNKNOWN`/no solution thay vì publish một incumbent chưa kiểm. H6 freeze yêu cầu không fallback trong audited jobs.

### 62. Solver có thể trả feasible thay vì optimal không?

Có status `FEASIBLE`, cùng `OPTIMAL`, `UNKNOWN`, `INFEASIBLE`, `MODEL_INVALID`. Một pass chỉ `FEASIBLE` không được cố định như optimum để chạy pass lexicographic sau. Trong H6 audited runs, evidence yêu cầu các selection pass cần thiết hoàn tất tối ưu; seed 7/19 vì thế không tạo chênh lệch.

### 63. Có cần global optimality không?

Mục tiêu production-research hiện tại là **bounded deterministic online optimization có certificate**, không phải chứng minh optimum toàn cục của không gian route lớn. Global optimality chỉ được chứng minh trong exact-small oracle scope; ở quy mô H6, claim phải giới hạn ở optimum trên candidate portfolio đã audit.

## F. Mất service trong WP9 và định nghĩa metric

### 64. Có dữ liệu request-level cho 154 completion mất ở Panel A không?

Có raw input/output transcript và per-request promise/certificate trong từng bundle. Nhưng con số 154 là chênh **aggregate completions** `1735 − 1581`, không phải một danh sách đã ghép sẵn 154 request nhân quả. Analyzer hiện chưa xuất dataset “B1 served/C1 not served” đã match và attribution.

### 65. Có thể phân loại 154 request theo budget/lock/ranking/no candidate/capacity/solver/timeout không?

**Chưa thể từ báo cáo hiện có.** Có thể loại trừ một số giải thích ở cấp run: H6 không có candidate omission/saturation được báo, solver hoàn tất optimal và không fallback/timeout. Nhưng capacity/physical rejection ở state cuối có thể là hậu quả của quyết định treatment sớm hơn; không được gắn nguyên nhân request-level nếu chưa làm counterfactual replay. Budget, lock và ranking cũng cần ablation hoặc witness nhất quán, không chỉ đọc final rejection code.

### 66. Phân rã “một nửa lock/ranking, một nửa budget” được tính thế nào?

Trên 5 robustness cells, mỗi arm có denominator 540:

| Arm | Completed | Chênh B1 |
|---|---:|---:|
| B1 | 440 | — |
| C1 unbounded | 420 | −20 = −3,70 pp |
| C1 tight 30 s | 400 | −40 = −7,41 pp |
| C2 loose hybrid | 402 | −38 = −7,04 pp |

`B1 → C1-unbounded` giữ lock/ranking nhưng bỏ finite ETA budget, nên −20 được gán cho arm-level “lock/ranking price”. `C1-unbounded → C1-tight` thêm budget 30 s và mất thêm đúng 20. Đây là **decomposition bằng arm counterfactual trên 5 cell**, không phải causal attribution cho từng request hay toàn bộ 20-cell panel.

### 67. Có request chỉ cần budget 31 giây là nhận được không?

Chưa biết. Repository chưa tính minimum extra budget/slack cho từng request bị mất. Không thể suy từ tổng burden hay final rejection reason.

### 68. Hay đa số cần 2–5 phút?

Chưa biết; không có distribution đó trong artifact phân tích hiện tại.

### 69. Distribution của required revision đối với request bị reject ra sao?

Chưa được đo. Để trả lời đúng cần một exploratory analyzer mới: tại các epoch tương ứng, lưu best otherwise-feasible witness, tính mức nới nhỏ nhất theo từng dimension, replay khi state trajectories đã lệch và định nghĩa rõ request matching. Các bin `≤10 s`, `10–30 s`, `30–60 s`, `>60 s` trong câu hỏi hiện chỉ là ví dụ, không phải kết quả.

### 70. Revision burden của B1 phân bố ra sao?

WP9 report khóa aggregate và pickup/drop split; raw transcript cho phép dựng per-rider history. Báo cáo chưa xuất histogram/quantile để kết luận “nhiều thay đổi nhỏ” hay “ít thay đổi lớn”. Pilot từng thấy dấu hiệu heavy-tail và có material/disruptive counts, nhưng không được dùng để thay distribution confirmatory. Cần phân tích mới nếu đây là câu hỏi quyết định treatment tiếp theo.

### 71. 0,17% burden của C1 có nghĩa gần như không thay đổi gì không?

Đúng về **measured decision-induced ETA total variation**: Panel A giảm từ 74.443.002 ms xuống 128.020 ms và 12/20 cell C1 bằng 0. Nhưng diễn giải sản phẩm phải rất cẩn thận: pickup component giảm về 0 phần lớn do lock theo định nghĩa, và C1 thường từ chối thêm công việc thay vì phục vụ rồi tối ưu revision tốt hơn. Burden gate vì thế mang rất ít thông tin khi service gate fail.

### 72. Có thật sự muốn burden gần zero không, hay 10–30% vẫn chấp nhận?

**Chưa quyết định.** H6 chỉ khóa một treatment và margin, không khóa product utility curve. Dữ liệu hiện tại gợi ý nên đo frontier service–burden nhiều mức và có thể chấp nhận burden cao hơn C1 nếu service phục hồi đáng kể; ngưỡng 10/20/30% phải là quyết định nghiên cứu/sản phẩm mới, không sửa ngược H6.

### 73. Công thức chính xác của revision burden là gì?

Primary burden WP9 là:

```text
TotalDecisionInducedBurdenMs
  = Σ mọi promisePublished action (
      decisionDelta.pickupEtaTotalMs
    + decisionDelta.dropEtaTotalMs)
```

Chỉ thành phần **decision-induced** được tính vào primary burden; exogenous travel update được tách riêng. Các count/switch/relocation/inversion khác được báo như dimensions riêng, không cộng vào millisecond total vì khác đơn vị.

### 74. ETA 10:00 → 10:30 → 10:10 tính burden thế nào?

Tính total variation: `|+30| + |−20| = 50 phút`, không phải final delta 10 phút. Hướng sớm/muộn bị lấy trị tuyệt đối trước khi cộng ledger.

### 75. Pickup ETA và dropoff ETA có weight giống nhau không?

Trong primary total burden, có: 1 ms pickup và 1 ms dropoff được cộng ngang nhau. Chúng vẫn được lưu riêng và có thể có hard budget khác nhau; H6 tight chỉ đặt finite 30 s lên dropoff ETA.

### 76. Assignment change có “đắt” hơn ETA change không?

Không có conversion weight kiểu “một switch = N giây”. Vehicle switch là dimension count riêng và trong H6 bị hard limit 0, nên về feasibility nó bị cấm tuyệt đối thay vì bị định giá mềm. B1 cũng bị O-001 cấm reassignment của accepted request.

### 77. Route/order change được tính thế nào?

Không cộng trực tiếp quãng đường route mới vào promise burden. Ledger đếm:

- số cặp incumbent bị đảo thứ tự (`incumbent_order_inversion_count`);
- số stop mới chèn trước pickup (`pre_pickup_inserted_stop_count`);
- relocation distance và stop-switch counts cho pickup/dropoff;
- ETA total variation do thay route gây ra.

Operational travel cost là objective/metric khác.

### 78. Có phân biệt ETA sớm hơn và muộn hơn không?

Ledger burden đối xứng theo trị tuyệt đối: sớm 20 giây và muộn 20 giây cùng tiêu 20.000 ms. Promise delta/provenance vẫn có old/new value để audit hướng, nhưng hard cumulative total hiện không ưu tiên delay hơn advance.

### 79. “Disruptive count” chính xác là gì?

`disruptiveRevisionFrameCount` tăng **một lần cho mỗi decision frame** nếu trong frame đó có ít nhất một `promisePublished` action có bất kỳ decision-induced component nào trong vector 10D khác 0. Nhiều rider hoặc nhiều nonzero dimensions trong cùng decision vẫn chỉ tính một disruptive frame. Đây không phải số request bị ảnh hưởng và không có threshold cảm nhận riêng.

### 80. Vì sao burden được coi là proxy cho passenger experience?

Nó nắm bắt một thuộc tính hợp lý: người dùng thấy khó tin tưởng khi ETA/assignment/stop/order bị đổi lặp lại, kể cả cuối cùng quay về gần ban đầu. Total variation, material count và switch dimensions đo churn tốt hơn final delta. Tuy vậy đây chỉ là **engineering proxy có construct validity hợp lý**, chưa phải thước đo satisfaction đã được validate bằng người dùng.

### 81. Có nghiên cứu/user evidence cho thấy 30 giây có ý nghĩa không?

Không có evidence như vậy trong repository. Related work hỗ trợ tầm quan trọng chung của reliability/time consistency/commitment, không biện minh riêng ngưỡng 30 giây cho khách RideBound.

### 82. Vậy 30 giây chỉ là experimental design parameter?

Đúng. Nó là mức tight khóa sau pilot để tạo phép thử cơ chế, không phải SLA hay threshold tâm lý người dùng.

### 83. Service rate tính chính xác thế nào?

```text
serviceRate = số requestId phân biệt có passengerAlighted
            / số requestId phân biệt có requestArrived
```

WP9 dùng paired aggregate trên toàn 20 cell của mỗi panel; denominator mỗi arm là `20 × 108 = 2.160` arrivals.

### 84. Denominator có phải tất cả arrivals không?

Đúng. Không dùng accepted làm denominator, vì như vậy treatment có thể né thất bại bằng cách reject nhiều hơn.

### 85. Passenger cancellation có nằm trong denominator không?

Theo định nghĩa chung, arrival đã vào denominator trừ khi một protocol phân tích khác preregister exclusion rõ ràng. Riêng WP9 terminal burden contract **không cho cancellation** và xác minh `arrivals = acceptances ∪ rejections`, `bookings = acceptances`, `boardings = bookings`, `completions = boardings`. Vì vậy câu hỏi cancellation không ảnh hưởng con số H6.

### 86. Failed pickup có tính completed không?

Không. Chỉ `passengerAlighted` mới là completed; accept hoặc booking/pickup attempt không đủ.

### 87. Accepted nhưng chưa hoàn thành trước horizon tính thế nào?

Scenario dừng arrivals sau 2 giờ rồi có drain tối đa thêm 2 giờ. Request chỉ được tính completed nếu alight trong run/drain hợp lệ; nếu vẫn chưa alight khi kết thúc thì không vào numerator và terminal conservation sẽ làm bundle fail nếu contract yêu cầu hoàn tất accepted lifecycle.

### 88. Vì sao chọn service margin −1 pp?

Margin này được khóa như một guardrail bảo thủ để ngăn cơ chế “đạt burden bằng cách reject công việc”, rồi giữ nguyên sau pilot/holdout freeze. Nó đặt yêu cầu khó: treatment phải gần B1 về completion. Kết quả cho thấy precision đạt khoảng 1,40 pp còn rộng hơn margin 1 pp.

### 89. −1 pp xuất phát từ đâu?

Không phải threshold được validate từ literature, business, supervisor hay formal power calculation. Nó là **preregistered conservative design choice**; design adequacy về sau cho thấy panel hữu hạn không đủ precision để suy luận dân số ở margin này. Vì thế không được gọi nó là product tolerance.

### 90. Tương lai mất bao nhiêu service là chấp nhận được?

Repository chưa có business/user utility để cho một con số. Phán đoán nghiên cứu từ H6 chỉ cho phép nói: 4,91–7,13 pp là không chấp nhận theo gate hiện tại và quá lớn để gọi “materially unchanged”. Ngưỡng tương lai nên được chọn sau khi có frontier burden–service, giá trị mỗi completion, chi phí churn và user evidence; không nên tự đặt lại −1 pp sau khi nhìn outcome.

## G. Dữ liệu và thiết kế thí nghiệm WP9

### 91. Demand trong WP9 là synthetic, real hay trace replay?

Đây là **deterministically normalized replay derivative từ dữ liệu Manhattan/FleetPy công khai thật**. Arrival, OD và nguồn row đến từ dataset; chọn/canonicalize/pseudonymize là deterministic. Commitment policy/service-class overlay là synthetic experimental treatment, và physical distance trong derivative dùng quy tắc tổng hợp từ travel time nên không được gọi toàn bộ scenario là raw production trace.

### 92. Request arrival distribution lấy từ đâu?

Từ các file demand FleetPy Manhattan `2018-11-DD_sample_10_1..4.csv` trong release công khai. Normalizer chọn đúng 108 row/cell bằng selection key khóa, giữ thời điểm đến thực trong cửa sổ 08:00–10:00 America/New_York, rồi dịch về simulation time 0–7.200.000 ms.

### 93. Travel times lấy từ đâu?

Từ directed Manhattan base network (`nodes.csv`, `edges.csv`) kết hợp file day-specific `YYYY-MM-DD_tt_factors.csv`, sau đó normalizer dựng shortest-path closure canonical trên node pool đã chọn. Mỗi scenario chứa một snapshot 9.120 directed arcs; distance field của derivative dùng quy tắc `travel-time-seconds × 10` synthetic meters và không phải khoảng cách địa lý gốc chính xác.

### 94. “Five travel realizations” được tạo thế nào?

Năm ngày holdout `2018-11-14` đến `2018-11-18` mỗi ngày dùng file `tt_factors` riêng, tạo 5 travel-day realizations. Bốn demand sample trong cùng ngày dùng chung factor/snapshot ngày đó. WP9 không random-generate thêm travel bằng solver seed; vì vậy 20 cells chỉ có 5 travel clusters độc lập theo thiết kế này.

### 95. Hai mươi cell Panel A hình thành bởi dimension nào?

`5 ngày travel × 4 demand sample files/ngày = 20 fixed cells`, cùng cửa sổ peak 2 giờ, request target 108, vehicle capacity 4 và fleet size 8. Mỗi cell được chạy paired B1/C1 trên cùng request/travel realization.

### 96. Panel B khác Panel A ngoài 8→4 xe ở đâu?

Panel B giữ đúng cùng 20 request sets, OD/timing, travel snapshot và policy/solver settings, nhưng materialize fleet 4 xe với initial positions của stratum 4 xe. Vì fleet state/positions và subsequent trajectory khác, hai panel được phân tích riêng, không pool; capacity per vehicle vẫn là 4.

### 97. Có mức fleet-size/capacity-stratum khác không?

Confirmatory WP9 chỉ có hai fleet-size strata: 8 xe và 4 xe. Pilot/load probe từng dùng các mức khác như 16/32 xe cho mechanical/performance work, nhưng không phải confirmatory effectiveness strata và không được trộn vào H6.

### 98. Vehicle capacity mỗi xe là bao nhiêu?

4 passenger/seats trong cả Panel A và B. Mọi request WP9 có `partySize = 1`.

### 99. Fleet homogeneous hay nhiều loại xe?

Homogeneous theo contract thí nghiệm: cùng capacity 4 và không có vehicle class/cost/service capability khác nhau. Xe khác nhau ở ID và initial position.

### 100. Spatial network lớn bao nhiêu?

Canonical scenario derivative có 96 node và complete directed shortest-path closure `96 × 95 = 9.120` non-self arcs, khu vực Manhattan. Đây là closure trên subset, không phải tuyên bố base graph chỉ có 96 node. Báo cáo WP9 chưa xuất distribution trip distance; vì distance là synthetic từ travel time, không nên suy mileage thực.

### 101. Experiment horizon dài bao lâu?

Arrival/scoring horizon là 2 giờ: 08:00–10:00 local, `horizonEndMs = 7.200.000`. Sau đó có drain tối đa thêm 2 giờ, tới `drainEndMs = 14.400.000`, với step 600 giây và tối đa 24 drain steps.

### 102. 2.160 arrivals là một run hay tổng nhiều run?

Là tổng **20 runs/cells của một arm trong một panel**: `20 × 108 = 2.160`. Một paired panel có 2.160 arrivals ở B1 và cùng 2.160 arrivals ở C1.

### 103. Có rush-hour, congestion hoặc travel update không?

Cửa sổ demand là peak 08:00–10:00 và travel factor đến từ dữ liệu theo ngày/giờ, nhưng normalizer WP9 khóa **một travel snapshot cho toàn run** (`snapshotCount = 1`). Không có dynamic mid-run `TravelTimeUpdated` trong 108 source events của confirmatory scenario. Do đó H6 không kiểm nghiệm online response với congestion update thay đổi theo thời gian.

### 104. Dataset có đại diện cho scenario cuối cùng không?

Nó đại diện cho một **finite Manhattan peak-window simulator panel**, đủ cho confirmatory claim có điều kiện. Nó không đại diện đã chứng minh cho thành phố khác, mixed fleet, cancellations/incidents, GPS noise, dynamic congestion, production demand, satisfaction hay SLA. External validity và population inference bị cấm trong artifact.

### 105. Vì sao solver seed 7 và 19 giống hệt?

Candidate ordering, model, one-worker CP-SAT, integer objectives và stable tie-break đều deterministic; audited passes đạt optimum. Seed chỉ có thể ảnh hưởng solver search/tie behavior, không tạo demand/travel unit mới. Trên 5 robustness cells, seed19−seed7 bằng đúng 0 cho completed, burden và disruptive frames ở cả B1/C1, xác nhận seed là non-replicate.

### 106. Candidate ordering có deterministic hoàn toàn không?

Có trong contract hiện tại: stable request/vehicle/candidate ordering, canonical IDs/hashes, deterministic frontier priority, một solver worker và không dùng wall-clock. Determinism còn được replay/digest tests và cross-run bundle verification kiểm tra.

### 107. Travel realizations là nguồn stochastic chính đúng không?

Không phải duy nhất. Có 5 travel-day realizations **và** 4 demand files mỗi ngày. Tuy nhiên dependence mạnh nhất là bốn cells cùng ngày chia sẻ travel factor, nên effective travel-level independence chỉ là 5 clusters. Solver seed không được tính là nguồn stochastic độc lập.

### 108. Có demand realization khác nhau không?

Có: bốn file sample demand/ngày. Audit cho thấy source sample files cùng ngày overlap 8,3–10,7%, nhưng sau selection 2.160 panel slots có 2.157 request IDs phân biệt, chỉ 3 lần reuse. Panel A/B cố ý dùng cùng demand realization để so capacity.

### 109. Tương lai có nên mở rộng experimental units không?

Có nếu muốn population inference hoặc claim generality mạnh hơn. Cần thêm độc lập travel/demand days, scenario strata, có thể thành phố/dataset/simulator khác; không tăng N giả bằng nhiều solver seed trên cùng realization. Sampling frame và clustering phải khóa trước outcome.

### 110. Compute budget và runtime WP9 là bao nhiêu?

Budget mỗi job được khóa bằng wall/CPU/RSS/process caps của harness, generation/validation/solver work units và một measured repeat/arm. Một cặp diagnostic Panel B ghi B1 `738.769 ms` và C1 `650.215 ms` (khoảng 12,3 và 10,8 phút cho từng job). Repository không công bố một tổng wall-clock canonical cho toàn WP9; các panel có thể chạy song song nên cộng per-job không bằng elapsed thực. Vì vậy không nên biến ước lượng “khoảng 90 phút còn lại” trong handoff thành số scientific chính thức.

## H. FleetPy, BeGo, RidePy và ba layer

### 111. FleetPy đóng vai trò gì?

FleetPy là **primary simulator + benchmark environment của Layer 2** và là nơi chạy confirmatory H6 trên dữ liệu công khai. Adapter chịu trách nhiệm dịch state/event/decision nhưng phải gọi đúng cùng versioned Runner; không được reimplement cơ chế trong Python.

### 112. BeGo là gì trong project?

BeGo là host/backend production-like độc lập được tích hợp ở Layer 1 qua adapter/default-off Shadow/Live modes, API/persistence/telemetry và mechanical replay. Nó không phải source tree để copy vào RideBound và không phải baseline nghiên cứu B1. WP9 Layer-1 evidence chứng minh integration mechanics, không chứng minh production SLA hay user effectiveness.

### 113. RidePy được thêm để chứng minh gì?

RidePy là simulator stack độc lập ở Layer 3 nhằm stress-test portability của protocol/Runner và generality ngoài FleetPy. WP10 đã cho kết quả âm ở capability gate: native controlled process không cung cấp mid-edge directed-position evidence đủ để giữ semantic equivalence, nên không chạy effectiveness matrix giả tạo.

### 114. Layer 1, 2, 3 định nghĩa thế nào?

| Layer | Hệ thống | Mục tiêu evidence |
|---|---|---|
| 1 | BeGo | tích hợp production-like, replay/shadow/live mechanics, persistence/API |
| 2 | FleetPy | primary simulator, public-data benchmark, confirmatory effectiveness |
| 3 | RidePy | independent simulator-stack portability/generality check |

Mỗi layer trả lời câu hỏi khác nhau; pass mechanics không tự nâng thành effectiveness claim.

### 115. Cross-system generality có phải claim quan trọng cho paper không?

Đó là claim **mong muốn**, nhưng hiện chưa được establish vì Layer 3 fail. Paper vẫn có thể có giá trị với claim hẹp về mechanism, certificate, reproducibility và finite FleetPy result âm. Nếu tiêu đề/đóng góp đòi “simulator-independent demonstrated generality”, phải bổ sung evidence mới; kiến trúc độc lập dependency một mình chưa đủ.

### 116. RidePy Layer 3 không đạt thì có nhất thiết phải sửa không?

Không bắt buộc để bảo toàn kết quả WP9 hoặc viết một paper có claim hẹp. Có thể ghi known limitation/capability-negative result. Chỉ bắt buộc sửa nếu mục tiêu tiếp theo chọn cross-simulator semantic equivalence làm acceptance criterion.

### 117. Có quyền sửa RidePy adapter để expose mid-edge position không?

RideBound kiểm soát adapter của mình nên có thể sửa nó. Nhưng adapter không được suy diễn/bịa `edgeId/progress` mà native simulator không quan sát được. Việc sửa vendor/simulator source hoặc fork dài hạn là quyết định provenance/maintenance mới; repository hiện không ghi một quyền hay cam kết cụ thể ngoài việc dùng dependency hợp lệ.

### 118. Native RidePy có thực sự không cung cấp mid-edge state?

Trong controlled-process execution path đã pin ở WP10, capability probe chỉ quan sát được node-level state và không có directed edge fraction tương đương contract. Đó là kết quả đo của phiên bản/path cụ thể, không phải tuyên bố vĩnh viễn về mọi API RidePy tương lai.

### 119. Có chấp nhận thay WP1/WP2 protocol để thêm edgeId/progress/remainingTravelTime không?

Protocol v1 **đã có** directed `edgeId` + `progressPermille`, nên thiếu hụt nằm ở nguồn native RidePy chứ không phải chỉ thiếu field contract. `remainingTravelTime` không tự đồng nghĩa với canonical position. Có thể evolve protocol bằng ADR/version major và negative/conformance tests nếu simulator mới cung cấp semantics tốt hơn, nhưng không sửa ngược freeze H6/WP10 hoặc gọi artifact cũ là tương đương.

### 120. Có giữ nguyên Domain/Application độc lập OR-Tools/simulator/framework không?

**Có theo kiến trúc đang khóa.** Domain và Application chỉ chứa model/use-case/port thuần; OR-Tools, EF/ASP.NET, map provider và simulator ở adapter/infrastructure ngoài. Đây là hard repository boundary cần giữ trong mọi refinement, trừ khi chủ dự án cố ý thay charter bằng ADR lớn—điều hiện không được khuyến nghị.

## I. Kiến trúc và hiệu năng

### 121. Có phần kiến trúc nào quá phức tạp hoặc nên refactor không?

Không có correctness blocker chưa xử lý trong final WP1–WP10 review. Các vùng phức tạp nhất là bounded candidate generation/retention/cache, full commitment projection-validation-certificate chain, protocol checkpoint/replay và benchmark verifier. Độ phức tạp phần lớn do reproducibility/fail-closed requirements, nhưng candidate hot path là nơi hợp lý nhất để tiếp tục profile và đơn giản hóa nội bộ. Bất kỳ refactor nào cũng phải giữ semantics bằng oracle, mutation/conformance tests và artifact provenance.

### 122. Candidate generation nằm ở đâu?

Chủ yếu trong project `RideBound.Algorithms`, namespace/folder `Candidates`: `InsertionCandidateGenerator`, `CandidateScheduleEvaluator`, `CandidatePortfolioRetainer`, `ForwardSlackProfile`, candidate models/identity và repair helpers. Orchestration policy ở `SolverBackedRidePoolingPolicy`/`SolverBackedFleetSelection`.

### 123. Ledger/budget/lock nằm ở đâu?

- `RideBound.Domain/Commitments`: dimension, vector, policy, budget/ledger primitives.
- `RideBound.Application/Promises` và `Application/Commitments`: projection, delta, decision validator.
- `RideBound.Algorithms/Commitments`: candidate assessor/filter/warning profile.
- `RideBound.Runner/Online`: state canonicalization, action mapping, checkpoint/evidence integration.

Domain không biết solver hoặc simulator.

### 124. Solver model nằm ở class nào?

Các class/port chính:

- `CandidateSelectionModel` và related model types;
- `ICandidateSelectionSolver`/solution/execution trong `RideBound.Application.Optimization`;
- `OrToolsCandidateSelectionSolver` trong `RideBound.Solvers.OrTools`;
- `SolverBackedFleetSelection` và `SolverBackedRidePoolingPolicy` trong Algorithms.

Safe validation/fallback nằm ở application execution boundary, không giao toàn quyền cho native solver.

### 125. Runner là process/service kiểu gì?

Là một **long-lived CLI process** dùng versioned NDJSON protocol qua stdin/stdout; diagnostic log đi stderr. Adapter gửi initialize/eventBatch/checkpoint messages và nhận ACK/decision/errors. Không phải HTTP/gRPC service trong core Runner hiện tại; BeGo host có API riêng ở integration layer.

### 126. Một decision end-to-end mất bao lâu?

Chưa có một số end-to-end duy nhất cho mọi load. Evidence hiện có:

- CP-SAT candidate-selection microbenchmark p50 khoảng 2,389 / 12,160 / 21,406 / 91,004 ms cho 4 / 16 / 32 / 128 Boolean options;
- benchmark `Generate` sau tối ưu khoảng 16,6 ms (suffix 4), 170 ms (8), 587 ms (12), 1.018 ms (16), lấy min của 3 lần trên máy đo;
- actual WP9 whole-run jobs kéo dài nhiều phút vì gồm hàng trăm epoch, simulator, transcript và verification.

Các số trên không được cộng thô thành một SLA; cần benchmark end-to-end theo load class nếu chuẩn bị product.

### 127. Có latency target không?

Chưa có product SLA kiểu `<100 ms`, `<1 s` hay `<5 s` được khóa. Hiện chỉ có deterministic work/resource limits và fail-closed behavior.

### 128. Hệ thống cuối cùng có cần real-time không?

Mục tiêu online ride-pooling đòi quyết định đủ nhanh theo event, nên **bounded real-time/near-real-time là intent**. Nhưng target cụ thể phải gắn với fleet size, event rate, hardware, UX timeout và fallback policy; repository chưa chọn con số. Với generation suffix 16 đã khoảng 1 s trong microbenchmark, claim `<100 ms` hiện không có bằng chứng.

## J. Paper, claim và Product UX

### 129. Có định submit RideBound thành paper không?

Roadmap WP12 có thesis/paper/reproducibility package, nên **paper là hướng dự kiến**. Repository chưa ghi quyết định submission đã chốt, tên venue hay ngày nộp.

### 130. Conference/journal mục tiêu là gì?

**Chưa quyết định/không có trong repo.** Không nên tự gán ITS, transportation, software systems hay HCI venue khi chưa biết framing và yêu cầu advisor.

### 131. Contribution cuối cùng có thể claim là gì?

Claim boundary cho phép tập trung vào tổ hợp:

- per-rider, path-dependent, multi-dimensional cumulative revision ledger/budget;
- lifecycle/freeze-aware hard locks và rolling multi-epoch enforcement;
- machine-checkable promise/certificate/provenance;
- portable versioned Runner + reproducibility/evidence architecture;
- định lượng trade-off và kết quả âm có điều kiện trong fixed panels.

Không claim dynamic insertion, ETA limits, reassignment, route similarity, least commitment, time consistency hay satisfaction là novel. Cũng chưa được claim cross-simulator effectiveness.

### 132. Paper nên chứng minh effectiveness, mechanism, trade-off hay system design?

Với evidence hiện tại, framing mạnh nhất là **mechanism + system/reproducibility design + measured trade-off/negative effectiveness result**. Không thể viết “C1 effective without material service loss”. Nếu có treatment mới preregistered và pass, effectiveness có thể được thêm; không nên ép outcome cũ thành positive.

### 133. Negative H6 có chấp nhận được cho publication không?

Về khoa học, có thể chấp nhận nếu phương pháp, freeze, ablation, giới hạn precision và claim boundary được trình bày trung thực. Kết quả “hard commitments đạt burden gần 0 chủ yếu bằng refusal và làm service giảm ở mọi capacity panel” là thông tin có giá trị. Khả năng venue nhận bài là đánh giá bên ngoài chưa có trong repo.

### 134. Có bắt buộc tìm treatment mới thành công không?

Không bắt buộc để hoàn thành một nghiên cứu âm có trách nhiệm. Bắt buộc chỉ nếu mục tiêu cá nhân/venue đòi positive intervention hoặc product path. Nếu tiếp tục treatment, phải dùng WP mới, pilot mới, freeze/prereg mới; không tune rồi sửa H6.

### 135. Có yêu cầu supervisor/advisor/reviewer cụ thể không?

**Không có dữ liệu trong repository.** Cần chủ dự án bổ sung nếu muốn roadmap phản ánh yêu cầu đó.

### 136. Deadline research/project là khi nào?

**Chưa được ghi.** Không thể suy deadline từ ngày hoàn thành WP9/WP10.

### 137. WP11 Product UX ban đầu định làm gì?

Roadmap dự kiến rider promise UI và operator audit UI: hiển thị promise hiện tại/lịch sử revision, certificate/breach/explanation, live update qua SignalR, incident-safe language, privacy/retention và UX tests. WP11 chưa bắt đầu nên scope có thể refinement lại sau quyết định về H6.

### 138. Có UI/prototype hiện tại chưa?

BeGo có frontend/integration surface production-like và RideBound có API/persistence/live pipeline foundations, nhưng **RideBound Product UX theo WP11 chưa được triển khai/đóng gate**. Không nên gọi integration screen hiện có là validated passenger product.

### 139. User của UI là ai?

Concept hiện có ít nhất hai mặt:

- passenger/rider: xem promise và thay đổi;
- operator/dispatcher/researcher: audit decision, ledger, certificate, incident và evidence.

Primary persona và quyền truy cập cụ thể chưa được chọn; driver UI chưa nằm trong scope rõ.

### 140. Product sẽ hiển thị promise gì?

Model có thể hiển thị vehicle, pickup point, pickup ETA/window, dropoff ETA và lịch sử revision/material change. Câu như “sẽ không thay đổi quá ±X phút” chỉ hợp lệ khi X là product policy đã validate; không được dùng 30 giây H6 làm marketing guarantee mặc định.

### 141. Passenger có được biết mức commitment không?

Về hướng thiết kế, nên hiển thị lời hứa và giải thích thay đổi vì đó là lý do tồn tại của certificate. Nhưng mức chi tiết (remaining budget, tier name, exact bound hay ngôn ngữ đơn giản) **chưa được quyết định hoặc user-tested**.

### 142. Có thể có nhiều loại service/commitment level không?

Model đã có `serviceClass` và policy theo request; tài liệu từng nêu strict/standard/flexible như khả năng. Vì vậy kiến trúc hỗ trợ concept nhiều tier, nhưng chưa có product catalog, pricing, eligibility hay validation. `tight/medium/loose` hiện là experimental bands, không phải ba gói bán cho khách.

### 143. Có business cost cho reject request không?

Không có cost tiền tệ/CLV được định lượng trong repo. Solver hiện biểu diễn ưu tiên gián tiếp bằng lexicographic maximize accepted trước các objective khác.

### 144. Có business cost cho đổi ETA không?

Không có utility/cost tiền tệ được validate. Hiện chỉ có technical burden vector và hard/warning thresholds.

### 145. Nếu định lượng được hai cost, có muốn solver tối ưu trực tiếp không?

Kiến trúc cho phép thêm policy/objective version mới để tối ưu trade-off đã định lượng. Tuy nhiên hard safety, accepted-assignment invariant và lời hứa được bán như guarantee không được scalarize thành “vi phạm nếu đủ rẻ”. Nên tách hard constraints khỏi soft business utility, preregister objective mới và so với B1/C1 bằng evidence mới.

## K. Nguồn lực, khả năng thay đổi và phần đã freeze

### 146. Hiện có bao nhiêu người làm RideBound?

**Repository không ghi.** Không thể suy team size từ commit, tên tác giả hay lượng artifact.

### 147. Làm full-time hay part-time?

**Không có dữ liệu.** Đây là thông tin chủ dự án cần xác nhận.

### 148. Roadmap mong muốn 3 tháng, 6 tháng hay 1 năm+?

**Chưa quyết định.** WP roadmap mô tả dependency/gates chứ không cam kết calendar duration. Trước khi ước lượng cần chọn nhánh sau H6 và biết staffing/deadline.

### 149. Có giới hạn hardware/compute không?

Không có hard organizational ceiling được ghi. Harness có resource envelope per job và final evidence ghi máy/môi trường chạy, nhưng đó là reproducibility control, không phải tuyên bố ngân sách phần cứng. Các benchmark hiện cho thấy candidate generation là cost đáng lưu ý khi suffix dài.

### 150. Có ngân sách cloud không?

**Không có dữ liệu trong repo.** Thí nghiệm hiện được thiết kế chạy deterministic/local với artifact pin; không có cloud account/cost plan được coi là nguồn sự thật.

### 151. Có bắt buộc dùng .NET + OR-Tools không?

.NET là implementation/platform hiện tại và OR-Tools là solver adapter đã audit, nhưng charter chỉ bắt Domain/Application độc lập dependency. Không có yêu cầu “vĩnh viễn không được thay”. Thay platform toàn bộ sẽ rất tốn evidence; thay solver qua port là khả thi hơn.

### 152. Có chấp nhận thay solver trong tương lai không?

Có về kiến trúc: implement `ICandidateSelectionSolver` adapter khác, giữ integer lexicographic semantics, deterministic budgets/status/fallback, rồi chạy conformance, exact-small oracle và benchmark equivalence. Artifact đã freeze vẫn phải giữ solver/version cũ trong provenance.

### 153. Có chấp nhận thay candidate generator mạnh không?

Có, nếu là work package/version mới và chứng minh không làm lệch fairness hay correctness: common raw pool cho arms, exact-small differential, omission/saturation evidence, deterministic replay và performance tests. Đây thậm chí là vùng tiềm năng lớn để tìm treatment/service frontier tốt hơn, nhưng không được sửa im lặng rồi so với H6 như cùng estimand.

### 154. Có chấp nhận thay cấu trúc commitment/ledger không?

Có thể evolve, nhưng đây là core scientific construct nên cần ADR/version migration, invariant proofs/tests, certificate compatibility và claim update. Monotone per-rider path-dependent semantics không nên bỏ chỉ để làm metric đẹp; có thể thêm dimensions, asymmetric accounting hoặc policy tiers trong phiên bản mới. Ledger/certificate của H6 phải bất biến và còn verify được.

### 155. Phần nào không được đụng vì đã freeze?

Không được sửa ngược:

- H6 preregistration, fixed panel membership, B1/C1 configs, margin/gates và WP9 raw/result bundles;
- WP10 freeze/capability decision và final negative outcome;
- hashes/provenance/Runner-adapter versions gắn với artifact đã công bố;
- lịch sử ADR/status và claim boundary của kết quả đã quan sát.

Source tương lai vẫn được thay bằng ADR/WP/version mới. Protocol v1 có thể được supersede bằng major version, nhưng artifact v1 và scientific outcome cũ phải tiếp tục verify; “freeze” không có nghĩa là cấm mọi phát triển repository.

## L. Thứ tự ưu tiên đề xuất

Repository không chứa thứ tự ưu tiên cá nhân của chủ dự án. Dựa trên trạng thái WP10 closure và việc H6 fail, thứ tự làm việc **đề xuất** là:

1. **Reproducibility/evidence** — giữ kết quả âm đáng tin và không mất freeze.
2. **Chứng minh/trả lời hypothesis khoa học** — chấp nhận cả câu trả lời âm; không đồng nghĩa phải ép positive result.
3. **Tìm trade-off tốt nhất** — trước hết phân tích required-revision/rejection frontier, sau đó mới thiết kế treatment.
4. **Giữ service gần B1** — failure lớn nhất hiện tại và là guardrail chống reject-all.
5. **Giảm revision mạnh** — nhưng đo trên tập phục vụ có ý nghĩa, không thưởng cơ chế vì từ chối.
6. **Clean architecture** — bảo vệ tính đúng và cho phép thay solver/simulator có kiểm soát.
7. **Tạo novelty đủ cho paper** — novelty phải bám claim boundary và evidence, không đổi tên known ideas.
8. **Cross-simulator generalization** — quan trọng nếu chọn portability claim; không chặn paper hẹp từ WP9.
9. **Performance/latency** — profile sau khi treatment semantics rõ, rồi khóa load-class SLA.
10. **Product/UX** — cần user evidence để chọn threshold/tier; WP11 có thể refinement sau scientific decision.
11. **Đưa lên production** — cuối cùng, sau effectiveness, SLA, incident/privacy và operator/rider validation.

Nếu mục tiêu thực tế của chủ dự án là ra sản phẩm sớm hoặc có deadline submission gần, thứ tự 7–11 cần được sắp lại bằng quyết định ngoài repository.

## Nguồn chính đã đối chiếu

- [Research charter](../01-research-charter.md)
- [Related work và claim boundary](../03-related-work-and-claim-boundary.md)
- [Problem model và notation](../04-problem-model-and-notation.md)
- [Portable core architecture](../05-portable-core-architecture.md)
- [Event contract và determinism](../06-event-contract-and-determinism.md)
- [Commitment ledger và certificates](../07-commitment-ledger-and-certificates.md)
- [Algorithms, baselines và solver](../08-algorithms-baselines-and-solver.md)
- [Three-layer evaluation](../09-three-layer-evaluation.md)
- [Data/scenario/replay](../10-data-scenarios-and-demand-replay.md)
- [Metrics, statistics và preregistration](../11-metrics-statistics-and-preregistration.md)
- [FleetPy adapter](../12-fleetpy-adapter.md)
- [Cross-system adapters](../13-cross-system-adapters.md)
- [BeGo integration](../14-bego-integration-api-persistence-ux.md)
- [Roadmap/work packages](../16-roadmap-and-work-packages.md)
- [Live status và decision log](../18-status-and-decision-log.md)
- [Requirement traceability](../19-requirement-traceability.md)
- [WP9 confirmatory result](../benchmarking/wp9-confirmatory-result-2026-08-23.md)
- [WP10 RidePy Layer 3 result](../benchmarking/wp10-ridepy-layer3-negative-capability-result-2026-08-23.md)
- [Final WP1–WP10 review](./wp1-wp10-final/README.md)
- [WP9 task plan](../tasks/39-wp9-main-experiment-ticket-plan.md)
- [WP10 task plan](../tasks/40-wp10-ridepy-layer3-ticket-plan.md)
- Source code chính: `src/RideBound.Domain`, `src/RideBound.Application`, `src/RideBound.Algorithms`, `src/RideBound.Solvers.OrTools`, `src/RideBound.Runner`, `src/RideBound.Benchmarking`.
