# Audit học thuật và thương mại T3–T9, kèm đề tài thay thế

**Dự án:** OptiGo/BeGo
**Ngày khóa audit:** 21/07/2026
**Mục tiêu:** kiểm tra bảy đề tài T3–T9 có thực sự còn khoảng trống nghiên cứu hay đã được paper/sản phẩm làm khá đầy đủ; nếu va chạm mạnh thì thay bằng hướng có đầu vào–đầu ra rõ, dữ liệu kiểm chứng được và ranh giới tuyên bố trung thực.

## 1. Kết luận ngắn nhất

Không nên tiếp tục giữ nguyên bảy đề tài cũ.

| Đề tài cũ | Va chạm học thuật | Va chạm sản phẩm/thương mại | Quyết định |
|---|---:|---:|---|
| T3 – chuyển xe một lần | Rất cao | Rất cao, kể cả patent gần như trùng ý | **Dừng bản gốc** |
| T4 – anytime Pareto DARP-MP | Cao ở từng thành phần | Trung bình | **Dừng cách phát biểu cũ; chuyển sang chứng nhận chất lượng toàn pipeline** |
| T5 – accessibility cá nhân hóa | Rất cao | Cao | **Giữ miền bài toán nhưng đổi đóng góp** |
| T6 – ETA khoảng tin cậy | Cao | Cao ở ETA/route; thấp hơn ở calibration công khai | **Giữ có điều kiện, thu hẹp mạnh** |
| T7 – decision-focused forecast + reposition | Gần như trùng trực tiếp | Cao | **Dừng** |
| T8 – feeder đồng bộ GTFS | Gần như trùng trực tiếp | Cao | **Dừng** |
| T9 – route-aware POI | Rất cao | Rất cao | **Dừng** |

Bảy vị trí T3–T9 nên được thay bằng các hướng dưới đây. Ghép chúng với T1–T2 đã được phân tích ở tài liệu trước sẽ tạo thành danh mục **chín đề tài tổng thể**; báo cáo này không âm thầm loại T1 hoặc T2.

1. **BeGo-COMMIT:** chèn yêu cầu mới nhưng giới hạn số lần và mức độ thay đổi lời hứa với khách đã nhận.
2. **BeGo-E2ECERT:** chứng nhận và tách riêng sai số do sinh ứng viên, do solver và do bước chọn phương án.
3. **BeGo-VERIFYACCESS:** chọn điểm đón/tuyến accessibility khi dữ liệu thiếu, biết từ chối và chủ động hỏi kiểm tra đúng nơi có giá trị nhất.
4. **BeGo-XETA-SHIFT:** ETA có khoảng rủi ro được hiệu chỉnh dưới dịch chuyển thời gian/thành phố, chỉ thành công khi quyết định route tốt hơn.
5. **BeGo-SOLVERGUARD:** tự chọn solver/chính sách theo trạng thái nhưng có ngưỡng tin cậy và fallback an toàn khi gặp phân phối lạ.
6. **BeGo-FAIRCERT-POOL:** chỉ công nhận một cải thiện fairness khi nó vẫn đúng với mọi cách gán thuộc tính hành khách còn thiếu trong miền giả định đã khai báo, đồng thời xử lý việc kết quả của các hành khách trong cùng xe phụ thuộc lẫn nhau.
7. **BeGo-COMPUTEGUARD:** phân bổ ngân sách tính toán giữa các yêu cầu động để giữ deadline phản hồi và chất lượng vận hành.

Trong số này, bốn hướng đáng ưu tiên nhất là `COMMIT`, `E2ECERT`, `VERIFYACCESS` và `FAIRCERT-POOL`. `COMPUTEGUARD` rất khả thi về hệ thống nhưng novelty phải đặt ở tác động vận hành của lịch phân bổ compute, không phải ở khái niệm anytime nói chung. `XETA-SHIFT` và `SOLVERGUARD` cạnh tranh cao.

## 2. Cách hiểu đúng từ “chưa ai làm”

Không thể chứng minh tuyệt đối rằng không có một công ty hay nhóm nghiên cứu nào trên thế giới đang làm nội bộ. Audit này dùng ba mức:

- **Trùng trực tiếp:** paper/patent/sản phẩm đã có cùng biến quyết định, mục tiêu và bối cảnh chính.
- **Kề sát:** các thành phần đã có, nhưng chưa thấy công trình ghép đúng câu hỏi mới và đánh giá đúng endpoint mới.
- **Chưa tìm thấy công khai:** không đồng nghĩa với độc quyền hay đủ điều kiện cấp patent.

Trang sản phẩm chỉ chứng minh tính năng công khai, không cho biết thuật toán nội bộ. Vì vậy báo cáo không dùng câu “công ty X chắc chắn chưa làm”, mà chỉ nói “chưa thấy bằng chứng công khai”.

## 3. Phạm vi bằng chứng đã đọc

Audit này kế thừa hai corpus toàn văn đã khóa trong workspace:

- [corpus 90 paper](./bego-90-paper-review-and-8-topics.md);
- [corpus 80 paper bổ sung](sources/extra-80-paper-evidence.md);
- [ma trận paper và public data nhắm đúng T1–T9](./bego-public-data-targeted-paper-evidence.md).

Hai corpus có PDF toàn văn, text theo trang, digest toàn tài liệu và kiểm tra trực quan trang đầu. Với audit T3–T9, các bài quyết định còn được đọc lại ở phần mô hình, thí nghiệm, kết luận và giới hạn; không suy kết luận chỉ từ title/abstract.

Vòng kiểm tra đối kháng cuối bổ sung mười PDF toàn văn, tổng cộng 270 trang, nâng corpus đã đọc lên **180 paper toàn văn**:

- [The Time-Consistent Dial-a-Ride Problem, Networks 2022](https://doi.org/10.1002/net.22063), 34 trang;
- [Enhanced Route Planning with Calibrated Uncertainty Sets, Machine Learning 2025](https://link.springer.com/article/10.1007/s10994-024-06697-7), 13 trang;
- [Learning Heuristic Selection with Dynamic Algorithm Configuration, ICAPS 2021](https://arxiv.org/abs/2006.08246), 12 trang;
- [Spatial Supply Repositioning with Censored Demand Data, 2025](https://arxiv.org/abs/2501.19208), 57 trang;
- [Auditing Fairness under Unobserved Confounding, AISTATS 2024](https://proceedings.mlr.press/v238/byun24a.html), 52 trang;
- [The Fragility of Fairness: Causal Sensitivity Analysis for Fair Machine Learning, NeurIPS 2024](https://arxiv.org/abs/2410.09600), 30 trang;
- [Doubly Robust Causal Effect Estimation under Networked Interference via Targeted Learning, ICML 2024](https://proceedings.mlr.press/v235/chen24c.html), 29 trang;
- [Multi-Armed Bandits with Interference: Bridging Causal Inference and Adversarial Bandits, ICML 2025](https://proceedings.mlr.press/v267/jia25b.html), 21 trang;
- [ML4CO Competition: Results and Insights, PMLR 2022](https://proceedings.mlr.press/v176/gasse22a.html), 12 trang;
- [Fair Recommendations with Limited Sensitive Attributes: A Distributionally Robust Optimization Approach, SIGIR 2024](https://arxiv.org/abs/2405.01063), 10 trang.

Các PDF này cũng được trích toàn văn và render trang đầu để kiểm tra trực quan. Chính vòng đọc bổ sung đã làm `LATENTBOUND` bị loại; buộc thu hẹp `COMMIT`, `XETA-SHIFT`, `SOLVERGUARD`; và làm rõ rằng `E2ECERT`, `VERIFYACCESS`, `FAIRCERT-POOL` chỉ có thể nhận novelty ở phần hợp thành chuyên biệt, không phải ở từng kỹ thuật nền riêng lẻ.

Các nguồn web sản phẩm/patent được dùng như lớp bằng chứng khác, không được tính là paper phản biện.

---

## 4. T3 cũ – BeGo-XFER: chuyển xe một lần

### 4.1. Vì sao bản gốc không còn đủ mới

[ATMOS 2025 – Exact and Heuristic Dynamic Taxi Sharing with Transfers](https://doi.org/10.4230/OASIcs.ATMOS.2025.15) đã giải taxi sharing động với một lần chuyển xe, cả exact và heuristic, trên dữ liệu Berlin mật độ cao. Kết quả được báo cáo gồm tăng occupancy khoảng 5,9%, giảm thời gian vận hành khoảng 4,6%, nhưng tăng thời gian hành khách khoảng 3,1%.

[En-route transfer-based dynamic ridesharing](https://www.sciencedirect.com/science/article/pii/S2352146524001170) năm 2024 còn nghiên cứu chính sách transfer bằng reinforcement learning.

Quan trọng hơn, [US10885472B2 – Dynamic transportation pooling](https://patents.google.com/patent/US10885472B2/en) mô tả gần như đúng ý tưởng T3 cũ:

- một hành trình dùng hai hoặc nhiều xe;
- ghép người đang ở trên các xe khác nhau;
- chọn điểm đổi xe theo vị trí, đích đến, dự báo nhu cầu và giao thông;
- chỉ mở handoff khi demand vượt ngưỡng;
- xét thời gian chờ, số lần đổi xe và sở thích;
- có incentive/token để phối hợp handoff.

Như vậy “chỉ transfer khi mật độ đủ cao” không còn là novelty. Nó đã xuất hiện cả trong patent.

Ở phía sản phẩm, [NExT Future Transportation](https://www.get-next.com/fleet/) công khai mô hình các pod có thể nối động, mở cửa liên xe và phân phối lại hành khách theo đích tại điểm do nền tảng chọn. Thiết kế phần cứng khác taxi thường, nhưng vẫn chứng minh transfer động đã được thương mại hóa/thử nghiệm ở cấp khái niệm sản phẩm.

### 4.2. Quyết định

**Dừng T3 bản gốc.** Thêm accessibility vào điểm transfer chưa đủ để cứu novelty, vì accessible routing và uncertainty cũng đã là các nhánh lớn. Nếu vẫn muốn nghiên cứu transfer, câu hỏi phải chuyển sang failure recovery hoặc commitment protection; tuy nhiên hiện thiếu dữ liệu handoff failure thật nên không nằm trong nhóm khuyến nghị cao nhất.

---

## 5. T4 cũ – BeGo-PARETO-MP: anytime Pareto DARP với meeting point

### 5.1. Những phần đã có

[Cortenbach và cộng sự, Transportation Research Part C 2024](https://ris.utwente.nl/ws/portalfiles/portal/461539423/S0968090X24003905.html) đã mô hình DARP có meeting-point selection bằng MILP, preprocessing, valid inequalities, exact cho bài nhỏ và Tabu Search cho bài lớn.

Multiobjective/Pareto DARP không mới. Ví dụ:

- [Data-driven multiobjective DARP with multiple time windows, 2023](https://www.sciencedirect.com/science/article/pii/S0965856423002823) ước lượng Pareto frontier;
- [Multi-objective Dial-a-Ride Problem, 2014](https://www.sciencedirect.com/science/article/pii/S2352146514001720) đã tối ưu nhiều mục tiêu;
- literature review về dynamic VRP ghi nhận hướng tìm/ước lượng Pareto set là một nhánh nghiên cứu đã có.

“Anytime” cũng không phải khoảng trống riêng. [Anytime optimization approach for online DARP, ODYSSEUS 2024](https://publications.polymtl.ca/63034/) đã dùng chính khái niệm này. Exact solver vốn đã có incumbent, lower bound và optimality gap cho mục tiêu đơn.

Phần lõi định tuyến động đã có sản phẩm như [Ecolane](https://www.ecolane.com/), [Road XS](https://www.roadxs.com/dial-a-ride-software/) và [RideCo](https://rideco.com/paratransit/scheduling). Các trang này không cho thấy họ công khai Pareto certificate, nhưng chúng làm giảm mạnh novelty ở câu chuyện “solver chạy càng lâu càng tốt cho DARP”.

### 5.2. Quyết định

**Không dùng bản phát biểu cũ làm đóng góp chính.** Nếu chỉ ghép `meeting points + Pareto + anytime + gap`, hội đồng có thể đánh giá là tích hợp bốn khái niệm đã biết.

T4 được thay bởi `BeGo-E2ECERT` ở phần 13: không chỉ hỏi solver cách tối ưu bao xa trong tập ứng viên đã giữ, mà chứng nhận toàn pipeline và tách sai số sinh ứng viên khỏi sai số solver.

---

## 6. T5 cũ – BeGo-ACCESS: tuyến và điểm đón theo thiết bị hỗ trợ

### 6.1. Những gì đã được làm

[Accessibility for Whom? – CHI 2025](https://doi.org/10.1145/3706598.3713421) đã thu dữ liệu từ 190 người thuộc năm nhóm thiết bị hỗ trợ và đã xây prototype personalized accessibility map/routing.

[RampNet 2025](https://openaccess.thecvf.com/content/ICCV2025W/CV4A11y/html/OMeara_RampNet_A_Two-Stage_Pipeline_for_Bootstrapping_Curb_Ramp_Detection_in_ICCVW_2025_paper.html) đã xây pipeline nhận diện curb ramp quy mô lớn, với hơn 214 nghìn panorama và hơn 849 nghìn nhãn sinh tự động, cộng bộ gold gán tay.

[AccessMap](https://tcat.cs.washington.edu/accessmap/) đã là web routing đặt accessibility lên trước, cho phép xét độ dốc, curb ramp, crossing và hồ sơ di chuyển. Nhiều prototype khác như MyPath, MobiliSIG và Accessible Route Planner cũng đã tồn tại.

Ngay cả uncertainty/reliability cũng không hoàn toàn mới: [Measuring the reliability of wheelchair user route planning based on VGI](https://onlinelibrary.wiley.com/doi/abs/10.1111/tgis.12087) đã xây personalized routing cùng hệ số reliability từ dữ liệu tình nguyện.

[US20210150434A1](https://patents.google.com/patent/US20210150434A1/en) còn mô tả ride-hailing có thiết lập disability, ghép loại xe phù hợp và thay đổi shared route.

### 6.2. Khoảng trống còn đáng làm

Các công trình trên cho thấy ba vấn đề thật:

- dữ liệu crowdsourcing bị thiếu nhãn;
- ảnh đường phố có thể cũ hoặc không phủ khu vực;
- một dự đoán “không thấy rào cản” không được đồng nhất với “tuyến chắc chắn an toàn”.

RampNet tự báo lỗi do metadata và ảnh khác thời điểm, ảnh cũ/thiếu ở khu vực nhỏ; paper CHI 2025 cũng nói prototype chưa thay thế đánh giá trực tiếp ngoài hiện trường.

Vì vậy contribution không nên là “personalized accessible routing” nữa, mà là **ra quyết định khi bằng chứng accessibility không đầy đủ, kiểm soát false-safe và biết khi nào phải hỏi/xác minh**.

### 6.3. Quyết định

Giữ domain nhưng thay T5 bằng `BeGo-VERIFYACCESS` ở phần 14.

---

## 7. T6 cũ – BeGo-XETA: ETA có khoảng tin cậy

### 7.1. Những phần không mới

Prediction interval cho travel time đã có từ lâu, ví dụ [Khosravi và cộng sự, 2011](https://www.sciencedirect.com/science/article/abs/pii/S0968090X11000532). [Predictive inference for travel time on transportation networks](https://arxiv.org/abs/2004.11292) đã nghiên cứu phân phối/uncertainty trên mạng đường.

Conformal prediction cho traffic forecasting cũng đã xuất hiện, ví dụ [Urban Traffic Forecasting with Conformal GNN, 2024](https://arxiv.org/abs/2407.12238). [Utility-Directed Conformal Prediction, ICLR 2025](https://proceedings.iclr.cc/paper_files/paper/2025/hash/0c6b452f1bbfb6905f6bac957d73b321-Abstract-Conference.html) còn trực tiếp nối prediction set với utility của quyết định phía sau.

ETA, traffic-aware routing và alternative routes hiển nhiên đã là tính năng thương mại phổ biến. Vì vậy “dự đoán khoảng ETA rồi chọn route ít trễ” tự nó không đủ mới.

### 7.2. Điều còn có thể bảo vệ

Chưa thấy một công trình công khai trùng toàn bộ giao điểm sau trong ridepool/group mobility:

- calibration có bảo đảm dưới temporal/city shift;
- route-choice loss, không chỉ MAE/coverage;
- selective prediction: từ chối dùng model khi phân phối quá lạ;
- so sánh cùng một protocol ở hai thành phố công khai.

Đây là novelty dạng **protocol + method dưới distribution shift**, không phải phát minh ETA interval.

### 7.3. Quyết định

Giữ có điều kiện dưới tên `BeGo-XETA-SHIFT` ở phần 15. Nếu chỉ fine-tune Transformer rồi thêm conformal split bình thường, đề tài vẫn quá đông và nên bỏ.

---

## 8. T7 cũ – BeGo-XDISPATCH: decision-focused forecast và reposition

### 8.1. Va chạm trực tiếp

[A smart predict-then-optimize framework for vehicle rebalancing, Transportation Research Part B 2026](https://doi.org/10.1016/j.trb.2026.103411) đã đi thẳng vào decision-focused/predict-then-optimize cho vehicle rebalancing.

Các hướng kề sát còn gồm:

- [Predictive vehicle repositioning for online on-demand ridepooling](https://arxiv.org/abs/2308.05507);
- [Deep reinforcement learning for real-world vehicle repositioning](https://arxiv.org/abs/2103.04555);
- các công trình 2026 về causal-aware rebalancing và coupling demand–repositioning.

Sản phẩm cũng làm forecast + rebalance:

- [Autofleet Taxi Operations](https://autofleet.io/taxi-operations);
- [Levy AI Ops](https://fleets.levyelectric.com/help/ai-ops/overview);
- [SWITCH Mobility Demand Prediction Platform](https://marketplace.eiturbanmobility.eu/products/switch-mobility-demand-prediction-platform).

### 8.2. Quyết định

**Dừng T7.** Cross-city test là đánh giá tốt nhưng không biến bài toán decision-focused repositioning thành bài mới. T7 được thay bởi `BeGo-SOLVERGUARD`, chuyển câu hỏi từ “dự báo demand tốt hơn” sang “khi nào được tin solver/policy nào dưới shift”.

---

## 9. T8 cũ – BeGo-TRANSITSYNC: feeder đồng bộ GTFS

### 9.1. Va chạm học thuật

[Novel operational algorithms for ride-pooling feeders, 2024](https://arxiv.org/abs/2411.00787) đã tối ưu ride-pooling làm feeder.

[Coordinating passenger itineraries and vehicle routes in first-mile demand-responsive feeder service, 2026](https://www.sciencedirect.com/science/article/pii/S0965856426000546) còn joint boarding/transfer stop và vehicle routes, rất gần biến quyết định của T8.

[Semi-on-Demand Transit Feeders with SAV and RL, 2025](https://arxiv.org/abs/2509.01883) đã dùng dynamic zonal control cho feeder. Literature về synchronized transfer, buffer và missed connection còn lâu đời hơn.

### 9.2. Va chạm sản phẩm/chuẩn

- [GTFS-Flex](https://gtfs.org/community/extensions/flex/) đã được thông qua rộng vào tháng 3/2024 và mô tả dial-a-ride, point-to-zone, kết nối ga;
- [Via Integrated Transit](https://ridewithvia.com/integrated-transit?lang=en-gb) bán giải pháp kết nối on-demand với fixed-route transit;
- [RideCo Time Snapping](https://www.rideco.com/differentiator/time-snapping) công khai việc bám hub/lịch fixed route và cập nhật thời gian thực.

### 9.3. Quyết định

**Dừng T8.** Ngay cả thêm real-time transit disruption cũng đã kề sát các hệ thống reoptimization và disruption literature. Không có dữ liệu cancellation/missed-transfer cá nhân đủ tốt để biến nó thành hướng ưu tiên. T8 được thay bằng `BeGo-FAIRCERT-POOL`, giải quyết trực tiếp rủi ro tuyên bố fairness từ thuộc tính hành khách bị thiếu thay vì mở thêm một biến thể feeder.

---

## 10. T9 cũ – BeGo-ROUTEPOI: POI có xét đường đi

### 10.1. Va chạm trực tiếp

Research đã có cả trajectory recommendation và itinerary dưới ràng buộc thật:

- [Learning Points and Routes to Recommend Trajectories](https://arxiv.org/abs/1608.07051);
- [Trip Recommendation Meets Real-World Constraints](https://doi.org/10.1145/2948065);
- [Recommending a sequence of interesting places for tourist trips](https://portal.fis.tum.de/en/publications/recommending-a-sequence-of-interesting-places-for-tourist-trips/) tối ưu chuỗi địa điểm dưới travel budget.

Sản phẩm đã rất rõ:

- [Google Maps – Explore along your route](https://blog.google/products-and-platforms/products/maps/explore-along-route-tip/);
- [Roadtrippers](https://roadtrippers.com/about/road-trip-apps/);
- [Wanderlog route optimization](https://help.wanderlog.com/hc/en-us/articles/13545624787867-Optimize-route).

### 10.2. Quyết định

**Dừng T9.** Accuracy–travel-cost Pareto hoặc rerank theo route budget là đóng góp quá nhỏ trong một vùng đã đông cả paper lẫn sản phẩm. T9 được thay bằng `BeGo-COMPUTEGUARD`.

---

## 11. Ma trận quyết định sau audit

| Hướng | Câu hỏi mới thực sự là gì? | Nhãn đánh giá thật? | Mức novelty sau audit | Khả năng làm với BeGo |
|---|---|---:|---:|---:|
| COMMIT | Chèn động nhưng không “lật lời” vô hạn với khách đã nhận | Có: log quyết định/route do hệ thống tạo | Trung bình–cao sau va chạm least-commitment/no-worse | Rất cao |
| E2ECERT | Gap cuối cùng đến từ stage nào, không chỉ solver gap | Có: exact oracle/bounds | Trung bình–cao; certificate từng tầng đã cũ | Rất cao |
| VERIFYACCESS | Khi nào “không biết”, hỏi xác minh điểm nào đáng nhất | Có: manual gold labels | Trung bình–cao; active/selective learning đã cũ | Cao |
| XETA-SHIFT | Khoảng ETA còn đúng và còn giúp chọn route dưới shift không | Có: realized travel time | Trung bình–cao | Trung bình |
| SOLVERGUARD | Khi nào dùng learned/fast solver, khi nào fallback | Có: realized replay/oracle nhỏ | Trung bình–cao | Cao |
| FAIRCERT-POOL | Cải thiện fairness có còn đúng với mọi cấu hình thuộc tính hành khách còn thiếu, khi các hành khách ảnh hưởng lẫn nhau? | Có ground truth trong controlled masking; real case dùng interval/certificate | Trung bình–cao nếu chứng minh được comparative certificate có tái tối ưu | Trung bình–cao |
| COMPUTEGUARD | Chia CPU time thế nào khi nhiều request tới cùng lúc | Có: deadline, cost, acceptance trong replay | Trung bình; scheduling/resource allocation đã rất đông | Rất cao |

### 11.1. Paper, web và sản phẩm gần nhất của bảy hướng mới

| Hướng mới | Bằng chứng công khai gần nhất | Đã có web/sản phẩm làm đúng đề tài chưa? | Kết luận novelty trung thực |
|---|---|---|---|
| COMMIT | Dynamic DARP đã chèn vào xe hoạt động; Multiple-Plan DARP đã có least-commitment/time consensus; TR-C 2026 đã có no-worse protection khi đổi điểm trả; patent đã có delay threshold/ETA update/reassignment consent | Uber Pool công khai tự cập nhật route khi thêm rider và khóa pickup/drop-off; chưa thấy sản phẩm công khai certificate cho toàn chuỗi revision | Chỉ mới nếu nghiên cứu **path-dependent, per-passenger, multi-dimensional revision budget** trên nhiều epoch, không phải một ngưỡng delay ở plan hiện tại |
| E2ECERT | Solver/column generation đã có primal-dual gap và chứng minh không còn cột tốt; MORP 2022 đã candidate-pruning bằng bound | Chưa thấy web ridepool công khai tách candidate loss, solver loss và final-selection loss | Novelty ở phép hợp thành certificate **toàn pipeline** và stage witness; từng certificate riêng lẻ không mới |
| VERIFYACCESS | [Project Sidewalk](https://sidewalk.cs.washington.edu/) và [AccessMap](https://tcat.cs.washington.edu/accessmap/) đã có thu thập nhãn và accessible routing | Có web accessibility, nhưng chưa tìm thấy web công khai ghép active verification, abstention và meeting-point ridepool | Không được nhận accessible routing là mới; phần mới phải là value-of-verification đối với quyết định ridepool |
| XETA-SHIFT | Paper 2025 đã nối calibrated uncertainty set với robust shortest path | [Google Routes Preferred](https://developers.google.com/maps/documentation/routes_preferred) và các routing API bán ETA/traffic routing, nhưng không công khai guarantee coverage dưới cross-city shift | Rất sát paper; chỉ giữ novelty ở shift tuần tự/cross-city và regret của quyết định ridepool |
| SOLVERGUARD | Dynamic algorithm configuration đã chọn heuristic theo từng trạng thái search từ 2021 | Sản phẩm có thể làm nội bộ, nhưng chưa thấy calibrated regret + OOD fallback được công khai | “ML chọn heuristic” đã cũ; phải có risk bound và fallback dưới shift |
| FAIRCERT-POOL | SIGIR 2024 đã DRO worst-case fairness với missing sensitive attributes; NeurIPS 2024 có [fragile_fair](https://github.com/Jakefawkes/fragile_fair); ICML 2024–2025 đã có causal/bandit methods dưới interference | Web `fragile.ml` được paper dẫn nhưng không còn phân giải DNS tại ngày audit; chưa thấy công cụ mobility tái tối ưu hai policy dưới mọi cấu hình thuộc tính thiếu | DRO, sensitivity, fairness bounds và interference đều không mới; khoảng trống chỉ là valid comparative certificate cho **hai combinatorial ridepool policies được tái tối ưu** |
| COMPUTEGUARD | Meta-reasoning điều khiển anytime algorithm có từ 2001; ridepooling đã có fast insertion/anytime solvers | [Google MathOpt](https://developers.google.com/optimization/service/reference/rest/v1/mathopt/solveMathOptModel) và Gurobi đã có time/work/gap limits cho từng solve; chưa thấy scheduler công khai phân bổ CPU giữa nhiều decision jobs ridepool và đo tác động service | Không được nhận time limit/gap stopping hay compute allocation nói chung là mới; đóng góp phải nằm ở scheduler deadline–quality–service cấp stream |

---

## 12. Đề tài mới 1 – BeGo-COMMIT

### Tên đầy đủ

**Commitment-Stable Online Ridepooling with Active-Route Insertion**
**Ridepooling trực tuyến giữ ổn định cam kết khi chèn yêu cầu vào xe đang hoạt động**

### Hiểu đơn giản

Xe A đã nhận hai khách và app đã báo:

- đón An lúc 18:10;
- đón Bình lúc 18:18;
- tới nơi lúc 18:45.

Một yêu cầu mới xuất hiện. Chèn yêu cầu đó có thể tăng số người phục vụ, nhưng nếu sau mỗi yêu cầu mới app lại đổi xe, đổi thứ tự đón hoặc sửa ETA nhiều lần thì hệ thống rất “chập chờn”.

COMMIT vẫn cho phép chèn động vào xe đang chạy, nhưng mỗi khách đã nhận có **ngân sách sửa cam kết**:

- tối đa bao nhiêu lần đổi ETA;
- tổng độ lệch ETA được phép;
- khi nào next stop/vehicle assignment bị khóa;
- không được đổi accepted thành rejected;
- có thể phá cam kết chỉ trong sự cố được khai báo, và phải ghi lý do.

### Khoảng trống so với paper gần nhất

[Multiple plan approach for a dynamic DARP, 2025](https://doi.org/10.1007/s00291-025-00809-y) đã chèn request vào route tương lai của xe đang hoạt động, khóa next node khi xe đang đi và giữ hard time window. Vì vậy **chèn động không phải novelty**.

[Unreliability in ridesharing systems, 2020](https://www.sciencedirect.com/science/article/pii/S0968090X2030735X) đo việc hơn một phần ba request có thể chịu thay đổi đột ngột và phân biệt thời gian được báo đầu tiên với kết quả thực tế. Nhưng paper chủ yếu đo hiện tượng và để bài toán kiểm soát unreliability cho nghiên cứu sau.

[The Time-Consistent Dial-a-Ride Problem, 2022](https://doi.org/10.1002/net.22063) là đối chứng bắt buộc: paper đã tối ưu tính nhất quán của lịch paratransit qua nhiều ngày trong một tuần. Sau khi đọc toàn văn 34 trang, ranh giới còn lại là paper đó làm bài toán **static, request biết trước, consistency giữa các ngày**; nó không kiểm soát chuỗi sửa đổi ngay trong một chuyến đang chạy sau khi app đã nhận khách. Vì vậy COMMIT không được tuyên bố “time consistency” nói chung là mới.

[Multiple plan approach for a dynamic DARP, 2025](https://doi.org/10.1007/s00291-025-00809-y) còn dùng consensus để chọn plan giống các plan khác nhất, gọi rõ đây là **least-commitment strategy**, và đề xuất time-based consensus dựa trên thời điểm sớm nhất hai plan khác nhau. Vì vậy route similarity, trì hoãn cam kết và “chọn plan ít khác” cũng không phải novelty. Điểm khác còn lại là paper không lưu một hợp đồng revision theo từng khách, không ràng buộc số lần/tổng biên độ sửa ETA và không chứng nhận đồng thời các thay đổi vehicle/stop/order trong suốt vòng đời request.

[Where should the last passenger be dropped off? Anticipatory walking in ridepooling, Transportation Research Part C 2026](https://www.sciencedirect.com/science/article/pii/S0968090X26001336) là va chạm mới nhất. Theo trang nhà xuất bản, phương pháp giữ hai schedule và có cơ chế quay về phương án trả đúng đích để hành khách cuối không tệ hơn phương án tham chiếu; như vậy **bảo vệ độ tin cậy/no-worse khi cập nhật điểm trả** cũng không còn mới. Tuy nhiên phạm vi bài này là quyết định điểm trả và đoạn đi bộ của hành khách cuối, không phải ledger ngân sách sửa ETA/xe/stop/thứ tự cho mọi hành khách đã được nhận trong một stream rolling insertion. Trang nhà xuất bản được đọc để audit novelty nhưng PDF toàn văn không tải được ổn định, nên bài này không được tính vào con số 180 paper toàn văn ở trên.

[US11754407B2 – Method and system for shared transport](https://patents.google.com/patent/US11754407B2/en) đã mô tả cập nhật ETA của khách đang trên xe khi thêm rider và cho requester đặt constraint về phần thời gian/quãng đường tăng thêm. [US11674811B2 – Assigning on-demand vehicles based on ETA of fixed-line vehicles](https://patents.google.com/patent/US11674811B2/en) còn đổi xe khi delay vượt threshold, thông báo lợi ích/chậm trễ và có thể xin user đồng ý hoặc đưa incentive. Do đó **per-rider max delay, ETA update, threshold, reassignment và consent riêng lẻ đều không mới**, kể cả ở cấp patent.

Ở cấp thương mại, [Uber Pool](https://www.uber.com/au/en/drive/services/shared-rides/) công khai việc thêm request khi xe đang chạy và tự đổi thứ tự pickup/drop-off; [RideCo Solver](https://www.rideco.com/differentiator/solver) quảng bá việc liên tục tối ưu và chuyển booking giữa các manifest ngay cả sau khi đã đặt. Đây là bằng chứng rõ rằng dynamic reassignment đã được thương mại hóa. Các trang công khai không cho thấy path-wise cumulative revision budget/certificate, nhưng không thể suy từ đó rằng thuật toán nội bộ chắc chắn không có.

Khoảng trống được giữ phải chặt hơn: **ràng buộc path-dependent lên toàn chuỗi lời hứa sau acceptance trong within-trip rolling insertion**. Gọi trạng thái đã công bố cho khách `i` tại epoch `t` là `c_i^t = (ETA, vehicle, stop, order)`. Hệ thống quản lý đồng thời:

- tổng biến thiên tích lũy `Σ_t d(c_i^t, c_i^(t-1)) ≤ B_i`;
- số lần switch/revision `Σ_t 1[c_i^t ≠ c_i^(t-1)] ≤ K_i`;
- hard lock theo giai đoạn và ngoại lệ sự cố có audit trail.

Đây là resource được tiêu dần qua nhiều epoch nên hai plan có cùng ETA cuối vẫn khác nhau nếu một plan đã “lắc” lời hứa năm lần. COMMIT phải đưa ra formulation/algorithm hoặc guarantee cho resource lịch sử này và đo trade-off với acceptance/VHT. Nếu chỉ thêm một constraint `delay ≤ 5 phút` vào insertion hiện tại, đề tài đã trùng kỹ thuật/patent cũ và không còn đủ mạnh.

### Đầu vào

- stream request theo timestamp;
- xe, vị trí hiện tại, tải, route suffix chưa chạy;
- khách đã nhận, ETA/vehicle/stop đã thông báo ở mọi epoch trước;
- hard constraints: capacity, pickup window, max ride time;
- commitment budget theo một policy công khai.
- toàn bộ commitment state/history để tính cumulative drift, không chỉ plan hiện tại.

### Đầu ra

- accept/reject request mới;
- insertion position và route suffix mới;
- log revision theo từng khách;
- certificate cho biết mọi commitment budget còn được giữ hay vi phạm ở đâu.
- witness epoch chỉ rõ revision nào đã tiêu hết budget và request mới nào gây ra nó.

### Dữ liệu và benchmark

- Manhattan DARP 2026 theo thứ tự thời gian;
- NYC TLC chronological replay;
- benchmark dynamic DARP của Ackermann–Rieck;
- exact small instances để kiểm tra feasibility và optimum có/không commitment.

Không cần giả lập “mức hài lòng”. Claim là operational stability, không phải tâm lý người dùng.

### Baseline

- greedy insertion không penalty revision;
- rolling reoptimization chỉ giữ hard time window;
- fixed lock window;
- penalty-only cho route churn;
- no-reassignment;
- multiple-plan approach.

### Chỉ số

- served/accepted rate;
- vehicle hours/VMT;
- mean/p95 wait và detour;
- số revision ETA trên mỗi khách;
- tổng và max magnitude của revision;
- số lần đổi xe/stop/order;
- accepted-to-rejected reversal phải bằng 0;
- commitment violation rate;
- runtime.

### Điều kiện thành công

Ví dụ preregister: giảm ít nhất 30% p95 revision magnitude và 50% số khách bị sửa ETA từ ba lần trở lên, trong khi served rate giảm không quá một điểm phần trăm so với rolling insertion mạnh nhất.

### Ranh giới tuyên bố

Kết quả chứng minh hệ thống ổn định lời hứa vận hành hơn. Nó không tự chứng minh người dùng hài lòng hơn nếu chưa có user study.

---

## 13. Đề tài mới 2 – BeGo-E2ECERT

### Tên đầy đủ

**End-to-End Quality Certification for Meeting-Point Ridepooling Pipelines**
**Chứng nhận chất lượng toàn pipeline cho ridepooling có điểm đón chung**

### Vấn đề bị bỏ qua

Solver báo gap 2% chỉ có nghĩa: trong tập ứng viên mà pipeline đã đưa cho solver, nghiệm cách optimum không quá 2%. Nếu bước trước đã loại mất điểm đón/venue/route tốt nhất, “2%” có thể gây hiểu lầm rất lớn.

Pipeline thật có ít nhất ba nguồn mất chất lượng:

1. **Candidate loss:** loại mất venue, stop, assignment hoặc route cần thiết.
2. **Optimization loss:** solver chưa tìm được nghiệm tốt nhất trong candidate set.
3. **Selection loss:** archive có nghiệm tốt nhưng fairness/policy selector chọn sai.

### Tại sao khác T4 cũ

T4 cũ tập trung incumbent/bound của solver và Pareto archive. E2ECERT chứng nhận **end-to-end**, kể cả các stage đứng trước/sau solver. Đây cũng đánh đúng điểm yếu hiện tại của BeGo: adapter public benchmark còn đơn giản hóa time window và semantics, còn route-pool/candidate cap có thể làm mất reference fairness.

[Learning for routing: a guided review, 2025](https://doi.org/10.1016/j.tre.2025.104278) nhấn mạnh benchmark routing hiện không đồng nhất, nhiều ML solver dùng instance Euclidean uniform và baseline khó so công bằng. [Multiple plan DARP 2025](https://doi.org/10.1007/s00291-025-00809-y) cũng nói dynamic DARP thiếu benchmark dùng chung. Tuy nhiên chưa thấy paper công khai trùng đúng decomposition/certificate toàn pipeline cho meeting-point ridepooling.

[ML4CO Competition: Results and Insights, 2022](https://proceedings.mlr.press/v176/gasse22a.html) là đối chứng trực tiếp ở tầng solver. Toàn văn 12 trang cho thấy cộng đồng đã đánh giá ba task trên MILP thực tế: tìm nghiệm primal khả thi, hỗ trợ chứng nhận dual/branching và cấu hình solver; dùng primal/dual/primal-dual gap integral dưới cùng phần cứng và thời gian. Vì vậy **optimality certificate, primal/dual gap và solver configuration riêng lẻ đều không mới**. Khoảng trống có thể bảo vệ của E2ECERT chỉ là phép hợp thành có kiểm chứng giữa candidate loss trước solver, conditional solver gap và selection loss sau solver cho meeting-point ridepooling, kèm witness quy trách nhiệm đúng stage.

[Online Ridesharing with Meeting Points, PVLDB 2022](https://www.vldb.org/pvldb/vol15/p3963-cheng.pdf) đã chọn meeting-point candidates offline, dùng HMPO graph và bound để prune meeting points/drivers khi insertion; bài còn có constant-approximation cho bài toán chọn core vertices. Trong tối ưu tổ hợp nói chung, column generation cũng dùng pricing/reduced cost để tìm cột còn thiếu hoặc chứng minh restricted master đã tối ưu. Vì vậy **candidate filtering có bound** cũng không mới. E2ECERT phải chứng minh một định lý hợp thành, chẳng hạn `global regret ≤ candidate-loss bound + solver conditional gap + selection regret`, và phải kiểm tra tính đúng của từng hạng bằng exact-small oracle/mutation; nếu chỉ đặt ba con số cạnh nhau thì đề tài chưa đủ mạnh.

### Đầu vào

- instance đầy đủ với typed semantics;
- cấu hình candidate generation/pruning;
- candidate archive;
- incumbent, bound và trace từ solver;
- policy chọn plan cuối;
- exact oracle trên instance nhỏ hoặc valid lower bound trên instance lớn.

### Đầu ra

- end-to-end optimality/regret interval;
- candidate recall/gap;
- solver gap điều kiện trên candidate set;
- selection regret;
- quality status: `Exact`, `Bounded`, `ArchiveApproximate`, `Uncertified`;
- witness: ứng viên/constraint nào gây mất chất lượng.

### Dữ liệu

- DARP with meeting points public benchmark;
- Li & Lim PDPTW;
- Manhattan DARP 2026;
- network-distance variants từ OSM;
- exact-small subset để có ground truth.

Đây là ground truth toán học, không cần sở thích giả.

### Baseline

- solver gap thông thường;
- fixed top-k nearest stops;
- fixed route-pool size;
- exhaustive candidates trên bài nhỏ;
- adaptive expansion không certificate;
- OR-Tools/PyVRP/CP-SAT với cùng validator.

### Chỉ số

- feasibility violation;
- exact candidate recall;
- end-to-end regret;
- độ rộng certificate interval;
- tỷ lệ run được chứng nhận;
- thời gian/memory overhead;
- lỗi được gán đúng stage qua mutation test.

### Điều kiện thành công

- validator bắt 100% mutation vi phạm capacity, precedence, time window và role;
- trên exact-small, certificate luôn chứa true end-to-end gap;
- adaptive candidate expansion giảm candidate loss rõ ràng trong cùng runtime hoặc đạt cùng loss với ít ứng viên hơn;
- báo đúng các trường hợp không thể chứng nhận.

### Rủi ro

Đây là đề tài algorithm engineering/evaluation, không phải app có “AI đẹp mắt”. Đổi lại, nó rất dễ bảo vệ về correctness và tái lập.

---

## 14. Đề tài mới 3 – BeGo-VERIFYACCESS

### Tên đầy đủ

**Risk-Controlled Accessible Pickup Routing with Abstention and Decision-Focused Verification**
**Chọn tuyến/điểm đón tiếp cận được với quyền từ chối và xác minh có mục tiêu**

### Hiểu đơn giản

Hệ thống có ba trạng thái, không phải hai:

- có bằng chứng tuyến đi được;
- có bằng chứng tuyến bị chặn;
- **chưa đủ dữ liệu**.

Khi chưa đủ dữ liệu, hệ thống không tự đổi `unknown` thành `safe`. Nó có thể:

- chọn tuyến khác có bằng chứng tốt hơn;
- abstain: chưa đề xuất chắc chắn;
- yêu cầu kiểm tra một panorama/curb cụ thể;
- chọn câu hỏi xác minh đem lại thay đổi lớn nhất cho quyết định điểm đón.

### Điểm mới

Personalized accessibility routing, VGI reliability và curb-ramp detection đều đã có. Contribution mới phải là:

1. kiểm soát xác suất **false-safe** ở cấp route/pickup decision;
2. selective decision/abstention có calibration;
3. active verification theo **value of information đối với quyết định route**, không hỏi ngẫu nhiên hoặc chỉ hỏi ảnh model thiếu tự tin nhất.

Ngay cả “active learning hướng theo quyết định” cũng không được nhận là ý tưởng mới nói chung. [ActiveAD: Planning-Oriented Active Learning for End-to-End Autonomous Driving, CVPR 2026](https://openaccess.thecvf.com/content/CVPR2026/html/Lu_ActiveAD_Planning-Oriented_Active_Learning_for_End-to-End_Autonomous_Driving_CVPR_2026_paper.html) đã chọn dữ liệu dựa trên giá trị đối với planning, còn [Can Active Learning Preemptively Mitigate Model Mis-specification?, 2024](https://arxiv.org/abs/2408.13690) cho thấy uncertainty sampling có thể thua random khi model bị misspecification. Do đó ranh giới còn lại phải đồng thời có **accessibility-specific false-safe control + calibrated abstention + value-of-verification đối với meeting-point/route regret** trên nhãn barrier thật; chỉ thay uncertainty sampling bằng một score mới là chưa đủ đóng góp.

### Đầu vào

- pedestrian graph;
- label/prediction về curb ramp, missing ramp, obstacle, surface;
- nguồn, timestamp, confidence và disagreement;
- mobility profile hoặc nhóm thiết bị trong dữ liệu CHI;
- các pickup candidates;
- verification budget, ví dụ chỉ được hỏi 5 ảnh.

### Đầu ra

- tuyến và điểm đón;
- risk/coverage certificate;
- `abstain` nếu không có route đủ bằng chứng;
- thứ tự panorama/barrier nên xác minh;
- route cập nhật sau mỗi nhãn mới.

### Dữ liệu

- [RampNet dataset](https://huggingface.co/datasets/projectsidewalk/rampnet-dataset), đặc biệt manual gold set;
- [Accessibility for Whom dataset/code](https://github.com/makeabilitylab/accessibility-for-whom);
- Project Sidewalk labels;
- OSM/Seattle sidewalk graph.

Gold label bị ẩn trong quá trình active-verification experiment rồi được mở khi “query”. Đây là protocol active learning chuẩn: nhãn cốt lõi vẫn là nhãn người thật, không phải tự sinh ra để phương pháp thắng.

### Baseline

- shortest path;
- unknown-as-safe;
- unknown-as-blocked;
- reliability-weighted path kiểu VGI;
- uncertainty sampling;
- random verification;
- shortest-path-edge-only verification;
- oracle manual labels.

### Chỉ số

- false-safe route rate;
- severe-barrier exposure;
- risk coverage và calibration;
- abstention/coverage;
- route overhead;
- decision regret so với oracle;
- giảm regret trên mỗi query;
- geographic/mobility-group breakdown.

### Điều kiện thành công

Ở cùng verification budget, giảm false-safe/decision regret so với uncertainty sampling và random query; không cải thiện bằng cách abstain gần như mọi trường hợp; không nhóm mobility nào có severe-barrier rate xấu hơn baseline phổ quát.

### Ranh giới tuyên bố

Manual gold cho phép chứng minh label/route-decision risk trên các barrier được biểu diễn. Nó không chứng minh an toàn ngoài đời cho mọi dạng khuyết tật, thời tiết hay công trường nếu chưa field test.

---

## 15. Đề tài mới 4 – BeGo-XETA-SHIFT

### Tên đầy đủ

**Shift-Aware Calibrated ETA for Selective Route Decisions**
**ETA được hiệu chỉnh dưới dịch chuyển dữ liệu và biết khi nào không nên tự quyết route**

### Câu hỏi nghiên cứu

Một khoảng ETA đạt 90% coverage trên dữ liệu cũ có còn đạt 90% khi chuyển sang tháng khác, giờ khác hoặc thành phố khác? Nếu không, hệ thống có phát hiện và fallback trước khi chọn route sai không?

### Va chạm trực tiếp phải thừa nhận

[Enhanced Route Planning with Calibrated Uncertainty Sets, 2025](https://link.springer.com/article/10.1007/s10994-024-06697-7) đã làm phần “conformal prediction rồi dùng uncertainty set để chọn đường robust”. Paper dùng CQR-GAE dự báo trọng số cạnh còn thiếu, tạo upper bounds có coverage biên 95%, sau đó giải robust shortest path/VaR; thí nghiệm trên mạng Chicago 546 nút, 2.150 cạnh với split 50/10/40 và đo cả coverage lẫn realized route cost.

Do đó việc nối prediction interval với route decision **không còn là novelty**. XETA-SHIFT chỉ còn đáng làm nếu đóng góp mới nằm ở ít nhất hai điểm:

- calibration tuần tự khi phân phối thay đổi theo thời gian hoặc chuyển thành phố;
- selective fallback trước khi coverage hỏng;
- route-regret và fairness của cả ridepool assignment, không chỉ shortest path một người.

Ở phía sản phẩm, [Google Routes Preferred API](https://developers.google.com/maps/documentation/routes_preferred) đã bán routing/ETA độ trễ thấp cho ridesharing và delivery, nhấn mạnh ETA accuracy và ưu tiên request. Vì vậy “ETA chính xác hơn cho ride-hailing” là nhu cầu đã thương mại hóa mạnh; phần chưa thấy công khai là interval có coverage guarantee dưới temporal/cross-city shift và policy abstention gắn với downstream ridepool regret.

### Đầu vào

- trajectory/road segment history;
- route candidates;
- departure time và feature chung giữa các thành phố;
- calibration window gần nhất;
- OOD/shift indicators.

### Đầu ra

- ETA distribution/interval;
- route được chọn hoặc abstain/fallback;
- coverage-risk certificate theo stratum;
- cờ drift và lý do fallback.

### Dữ liệu

- Porto taxi trajectories;
- T-Drive Beijing;
- chia thời gian tuyệt đối, rolling-origin;
- train một thành phố, external test thành phố còn lại;
- realized travel time là nhãn thật.

### Baseline

- historical average;
- XGBoost/LightGBM;
- DeepTTE/Transformer/DSETA-style;
- quantile regression;
- split conformal cố định;
- rolling conformal;
- point ETA + fixed safety margin;
- oracle realized selector.

### Chỉ số

- MAE/RMSE;
- marginal và worst-stratum coverage;
- interval width/Winkler score;
- route-choice regret;
- late-arrival rate;
- selective risk–coverage curve;
- drift detection delay và false alarm.

### Điều kiện thành công

Không chấp nhận “MAE thấp hơn” là đủ. Phải giữ coverage sau shift tốt hơn calibrated baseline và giảm route-choice regret/late arrival ở cùng mức coverage.

### Mức rủi ro

Rất cao. Paper 2025 đã lấy mất phiên bản dễ nhất của ý tưởng. Nếu chỉ thêm drift detector hoặc đổi dataset rồi giữ nguyên calibrated robust route, đề tài không đủ mới. Chỉ nên chọn khi sẵn sàng xây phương pháp sequential/cross-city có guarantee hoặc một formulation ridepool mới mà baseline shortest-path paper không giải được.

---

## 16. Đề tài mới 5 – BeGo-SOLVERGUARD

### Tên đầy đủ

**Risk-Calibrated Solver and Policy Selection for Dynamic Ridepooling under Distribution Shift**
**Chọn solver/chính sách có kiểm soát rủi ro khi nhu cầu thay đổi**

### Hiểu đơn giản

Không có một solver thắng mọi trạng thái:

- giờ vắng: simple insertion có thể đủ;
- burst request: route-pool hoặc batch matching tốt hơn;
- instance nhỏ: exact solver có thể cho certificate;
- trạng thái lạ: learned policy có thể hỏng nặng.

Hệ thống học chọn solver/parameter cho từng epoch, nhưng chỉ dùng lựa chọn đó khi ước lượng regret đủ tin cậy. Nếu không, fallback về policy mạnh và dễ kiểm tra.

### Khoảng trống

Algorithm selection, configuration và hyper-heuristics đã có trong routing nói chung. [Learning for routing review 2025](https://doi.org/10.1016/j.tre.2025.104278) còn có hẳn taxonomy cho các hướng này. Vì vậy “dùng ML chọn heuristic” không mới.

Va chạm mạnh hơn là [Learning Heuristic Selection with Dynamic Algorithm Configuration, ICAPS 2021](https://arxiv.org/abs/2006.08246). Paper dùng reinforcement learning chọn heuristic tại **mỗi search expansion** dựa trên trạng thái nội bộ của search, chứng minh dynamic configuration tổng quát hơn static selection và có thể giảm số expansion theo cấp số mũ trên một số họ bài toán. Thí nghiệm dùng 100 instance train và 100 instance test chưa thấy trong từng IPC domain. Vì vậy ngay cả “đổi heuristic động trong lúc solver chạy” cũng đã cũ.

Điểm paper đó chưa làm là cross-domain/cross-city OOD, khoảng regret được hiệu chỉnh, safe fallback và endpoint ridepooling. Đây mới là phần SOLVERGUARD được phép nhận làm đóng góp.

Khoảng trống hẹp hơn là:

- online dynamic ridepooling;
- calibrated upper bound/risk của **operational regret**;
- selective use + fallback;
- cross-time/cross-city shift;
- đánh giá bằng replay và exact-small oracle.

### Đầu vào

- state features: demand rate, spatial entropy, fleet load, slack, route-pool size;
- portfolio solver/policy;
- latency budget;
- drift score;
- lịch sử realized objective của từng solver trên validation stream.

### Đầu ra

- solver/policy/parameter được chọn;
- predicted regret interval;
- accept selection hoặc fallback;
- log calibration/audit.

### Dữ liệu

- Manhattan DARP 2026 chronological stream;
- NYC TLC temporal split;
- Chicago external shift;
- public DARP-MP families;
- exact oracle cho bài nhỏ.

### Baseline

- always-greedy;
- always-ALNS/route-pool;
- fixed rule theo request rate;
- standard classifier chọn solver;
- contextual bandit;
- best single solver;
- oracle per-instance selector.

### Chỉ số

- operational regret vs oracle selector;
- served rate, wait, VHT/VMT;
- p95 latency;
- calibration của regret bound;
- fallback rate;
- worst-shift regret;
- compute cost.

### Điều kiện thành công

Giảm regret so với best single solver và classifier thường; bound đạt coverage đã hứa trên temporal test; khi sang Chicago, worst-case degradation thấp hơn selector không guard.

### Ranh giới

Nếu chỉ chứng minh trên random split cùng một thành phố thì đề tài thất bại về novelty. Nếu chỉ dùng RL chọn heuristic theo state thì cũng trùng nền tảng 2021. Phải có shift thật, regret bound được kiểm tra calibration, fallback có lý do và phải báo khi guard abstain quá nhiều.

---

## 17. Đề tài mới 6 – BeGo-FAIRCERT-POOL

### Tên đầy đủ

**Interference-Aware Fairness Certification for Ridepooling with Missing Passenger Attributes**
**Chứng nhận fairness cho ghép xe khi thiếu thuộc tính hành khách và kết quả của mọi người phụ thuộc lẫn nhau**

### Vì sao LATENTBOUND cũ bị loại

[Spatial Supply Repositioning with Censored Demand Data, 2025](https://arxiv.org/abs/2501.19208) va chạm quá trực tiếp với LATENTBOUND. Toàn văn 57 trang đã có:

- mô hình repositioning cho mạng vehicle-sharing với demand chỉ quan sát tới mức supply;
- exact MILP/LP cho bài offline;
- thuật toán online SOAR dùng censored observations;
- lower bound và regret tối ưu theo bậc;
- thí nghiệm synthetic có demand tương quan, 20 lần lặp và confidence interval.

Paper không làm fairness, nhưng đã chiếm lõi “học/ra quyết định reposition từ censored mobility demand”. Chỉ đổi metric sang fairness sẽ là đóng góp yếu, nên audit này **dừng hẳn LATENTBOUND** thay vì cố cứu bằng cách đổi tên.

### Động lực mới, bám đúng vấn đề dữ liệu của BeGo

NYC TLC, Chicago Trips và đa số benchmark DARP không có giới hạn đi bộ thật của từng người, mức khó tiếp cận, mức chịu detour hay đánh giá chủ quan về công bằng. Nếu tự gán mọi người 500 m, ta chỉ chứng minh thuật toán trong một cấu hình. Nếu tự sinh một giá trị khác cho mỗi người rồi tuyên bố fairness tăng, kết luận có thể do chính cách sinh nhãn tạo ra.

FAIRCERT-POOL không đoán một bộ nhãn duy nhất. Nó hỏi:

> Với mọi cách gán giới hạn đi bộ/nhu cầu hỗ trợ còn hợp lý theo survey hoặc ràng buộc tổng hợp, policy A có còn công bằng hơn policy B không?

Nếu có, hệ thống phát certificate. Nếu có một cách gán hợp lý làm đảo kết luận, đầu ra bắt buộc là **chưa kết luận được**, kèm witness gây đảo chiều.

### Paper nền tảng và khoảng trống thật

[Auditing Fairness under Unobserved Confounding, AISTATS 2024](https://proceedings.mlr.press/v238/byun24a.html) đã suy ra lower/upper bounds và confidence intervals khi còn confounder không quan sát; case study dùng N3C cohort có 18,4 triệu hồ sơ tổng trước các bước lọc, ngoài ra còn có semi-synthetic và synthetic experiments. Vì vậy “fairness bounds khi thiếu biến” không mới.

[Fair Recommendations with Limited Sensitive Attributes: A Distributionally Robust Optimization Approach, SIGIR 2024](https://arxiv.org/abs/2405.01063) là tiền thân trực tiếp hơn. Toàn văn 10 trang cho thấy DRFO xây ambiguity set theo total-variation distance, dùng error rate của bộ tái dựng thuộc tính để upper-bound bán kính, rồi giải min–max nhằm giảm worst-case demographic-parity unfairness. Paper đánh giá bằng cách che nhãn giới tính ở tỷ lệ biết 10/30/50/70/90% trên MovieLens-1M và Tenrec, có cả tình huống người dùng không cho tái dựng thuộc tính. Vì vậy **DRO/worst-case fairness với thuộc tính nhạy cảm bị thiếu đã có cả định lý lẫn thực nghiệm**; đây không được coi là đóng góp mới của FAIRCERT-POOL.

[The Fragility of Fairness, NeurIPS 2024](https://arxiv.org/abs/2410.09600) còn tổng quát hơn ở measurement bias: framework mã hóa assumption bằng causal graph, giải optimization để lấy bounds, chạy 14 fairness datasets và có [mã nguồn công khai](https://github.com/Jakefawkes/fragile_fair). Paper cảnh báo assumption không thực tế có thể tạo cảm giác an toàn giả và yêu cầu chạy assumption-lite analysis trước. Quan trọng nhất, phần giới hạn nói framework hiện chưa biểu diễn thuận tiện **interference**, tức outcome của một người bị quyết định/thuộc tính của người khác làm thay đổi.

Tuy nhiên, **interference tự nó không mới**. [Doubly Robust Causal Effect Estimation under Networked Interference via Targeted Learning, ICML 2024](https://proceedings.mlr.press/v235/chen24c.html) ước lượng main, spillover và total causal effects trên mạng với tính chất doubly robust, được thử trên BlogCatalog/Flickr bán tổng hợp và dữ liệu nhà máy điện Hoa Kỳ. [Multi-Armed Bandits with Interference, ICML 2025](https://proceedings.mlr.press/v267/jia25b.html) nghiên cứu online learning khi reward của mỗi unit phụ thuộc treatment của các unit khác, đưa ra switchback/clustered randomization và regret bounds. Hai bài này bác bỏ mọi claim kiểu “lần đầu xét tác động chéo giữa người dùng”.

Điểm chúng chưa làm là so sánh hai policy tối ưu tổ hợp phải **tái tối ưu assignment/route** dưới mọi cách gán thuộc tính hành khách còn khả dĩ, rồi chứng nhận min/max của chênh lệch fairness. Ridepooling có interference rất mạnh: tăng giới hạn đi bộ của An có thể làm đổi meeting point, xe, thứ tự đón, detour và cả việc Bình được nhận hay từ chối. Khoảng trống được giữ là:

- comparative certificate giữa **hai thuật toán tối ưu tổ hợp**, không chỉ audit một classifier cố định;
- uncertainty ở thuộc tính từng hành khách;
- interference qua chung xe, chung capacity và route order;
- certificate về chênh lệch fairness sau khi cả hai policy được chạy lại dưới mỗi cấu hình thuộc tính.

### Đầu vào

- stream chuyến thật: vị trí, thời gian, số người; không giả vờ có walking limit thật;
- graph đường đi bộ và đường xe;
- fleet, capacity và hai policy/solver cần so sánh;
- miền thuộc tính còn thiếu của từng người, ví dụ `w_i ∈ [0, 800] m`;
- ràng buộc tổng hợp lấy từ survey nhỏ hoặc nguồn ngoài: tỷ lệ ở từng khoảng, quan hệ với tuổi/khả năng vận động nếu được phép thu thập;
- metric fairness và metric hiệu quả được preregister trước.

Không dùng một bán kính đồng nhất 0/200/400/500 m làm “dữ liệu thật”. Các giá trị đó chỉ là stress slices; miền uncertainty phải cho phép cấu hình **khác nhau theo từng người** và giữ đúng các ràng buộc tổng hợp đã công bố.

### Đầu ra

- interval của chênh lệch fairness `ΔF = F(A) - F(B)`;
- trạng thái `RobustlyBetter`, `RobustlyWorse` hoặc `Inconclusive`;
- confidence level và assumption set đi kèm certificate;
- witness assignment làm policy A xấu nhất hoặc đảo thứ hạng;
- sensitivity curve cho biết phải thay đổi assumption bao nhiêu thì kết luận mất hiệu lực.

### Hướng phương pháp

1. Dùng replay vị trí/thời gian thật để giữ cấu trúc demand.
2. Xây confidence/uncertainty set cho thuộc tính thiếu, không point-impute tùy ý.
3. Với mỗi cấu hình, chạy lại cả A và B vì assignment của mọi người phụ thuộc lẫn nhau.
4. Giải `min/max ΔF` bằng decomposition, branch-and-bound hoặc MILP relaxation; lower/upper bound chưa khép thì không được phát certificate.
5. Tách statistical uncertainty khỏi optimization gap để biết interval rộng do thiếu data hay do solver.

Đây không phải chỉ “quét nhiều scenario”. Phần nghiên cứu là tìm bound có chứng minh, witness và stopping rule trong một không gian gán thuộc tính tổ hợp rất lớn.

### Dữ liệu và cách kiểm chứng

- **NYC TLC/Chicago Trips:** demand location–time thật, dùng cho case study và external-city test;
- **survey preregistered nhỏ:** thu walking tolerance theo tình huống, nhu cầu hỗ trợ và stated preference; chỉ dùng để ràng buộc phân bố, không gọi là revealed behavior;
- **controlled masking:** tạo ground truth từ distribution đã công bố, chạy policy, sau đó che thuộc tính để kiểm tra certificate có bao phủ true `ΔF`; bước này chứng minh correctness của phương pháp, không chứng minh hành vi xã hội thật;
- **exact-small benchmark:** liệt kê mọi cách gán thuộc tính trên instance nhỏ để xác nhận lower/upper bound;
- **real case:** chỉ claim robust trong assumption set; interval cắt 0 thì báo không kết luận.

Nếu không thể làm survey, đề tài vẫn chạy được với nhiều assumption set từ rộng tới hẹp, nhưng kết quả thực tế có thể thường xuyên là `Inconclusive`. Đó là kết quả khoa học hợp lệ, không phải lỗi cần che.

### Baseline

- mọi người cùng 500 m;
- random point imputation một lần;
- Monte Carlo trên một phân bố giả định;
- worst case độc lập từng người nhưng bỏ qua ràng buộc tổng hợp;
- generic fairness sensitivity tool không chạy lại combinatorial assignment;
- DRFO-style worst-case fairness optimizer trên predictor/recommender cố định;
- exact enumeration trên instance nhỏ.

### Chỉ số

- false-certificate rate và empirical coverage của true `ΔF` trong controlled masking;
- độ rộng interval và tỷ lệ `Inconclusive`;
- runtime/gap của outer bound;
- tỷ lệ robust dominance;
- served rate, VMT/VHT, wait, detour;
- max burden, p90 burden, Gini/generalized-entropy và worst-group burden khi có group label hợp lệ;
- độ nhạy theo chất lượng survey và độ rộng assumption set.

### Điều kiện thành công và ranh giới claim

Phải đạt coverage đã hứa, ví dụ 95%, trên controlled masking và không phát “A tốt hơn” khi exact oracle nhỏ cho thấy có witness đảo chiều. Trên real trip data, claim hợp lệ chỉ là:

> “A cải thiện metric F so với B đối với mọi cấu hình thuộc miền U đã công bố, với confidence/gap đã báo.”

Không được đổi câu đó thành “người dùng thật thấy công bằng hơn”. Muốn claim cảm nhận thật vẫn cần user study đủ mạnh. Chính sự khiêm tốn này giải quyết bias do tự sinh dữ liệu thay vì che nó.

---

## 18. Đề tài mới 7 – BeGo-COMPUTEGUARD

### Tên đầy đủ

**Deadline-Aware Compute Allocation for Online Ridepooling Optimization**
**Phân bổ ngân sách tính toán có deadline cho tối ưu ridepooling trực tuyến**

### Hiểu đơn giản

Trong một giây có thể tới nhiều request. Mỗi request đều muốn solver suy nghĩ lâu để tìm insertion tốt, nhưng backend chỉ có số core/CPU time hữu hạn. Nếu chia đều 500 ms/request, request khó có thể chưa đủ, request dễ lại bị lãng phí thời gian.

COMPUTEGUARD quyết định:

- request/vehicle neighborhood nào cần thêm thời gian;
- khi nào dừng exact/ALNS;
- khi nào chuyển từ solver nhanh sang solver mạnh;
- khi nào trả nghiệm hiện tại để không trễ SLA;
- làm sao giữ acceptance/quality khi demand burst.

### Khoảng trống

Anytime optimization và fast insertion đều đã có. [Monitoring and Control of Anytime Algorithms, 2001](http://rbr.cs.umass.edu/shlomo/papers/HZaij01a.pdf) đã mô hình hóa việc quyết định nên cho anytime algorithm chạy thêm bao lâu dựa trên chất lượng incumbent và triển vọng cải thiện. [Effort Allocation for Deadline-Aware Task and Motion Planning, 2024](https://arxiv.org/abs/2410.05828) tiếp tục phân bổ effort dưới deadline trong robotics. Vì vậy **time/effort allocation nói chung không mới**.

Ngay ở sản phẩm solver, [Google MathOpt](https://developers.google.com/optimization/service/reference/rest/v1/mathopt/solveMathOptModel) đã cho đặt time/node/iteration limit, absolute/relative gap và objective/bound stopping; [Gurobi parameter guidelines](https://docs.gurobi.com/projects/optimizer/en/current/concepts/parameters/guidelines.html) còn có deterministic `WorkLimit`, `MIPGap`, `BestBdStop` và `BestObjStop`. Vì vậy dừng một solve theo deadline/gap là chức năng hàng hóa, không phải nghiên cứu mới.

Trong ridepooling, speed-up heuristic và anytime assignment cũng đã có; Multiple-plan DARP 2025 dùng giới hạn 1 giây cho insertion và 5 giây cho toàn epoch. Tuy nhiên chưa tìm thấy công trình công khai trùng đúng tổ hợp: **phân bổ compute budget giữa nhiều decision jobs ridepool đồng thời**, có preemption/switching, deadline tail-latency, route-quality bound và service outcome trong cùng một mô hình.

[Online Generalized Magician’s Problem with Multiple Workers, UAI 2025](https://proceedings.mlr.press/v286/wu25a.html) đã giải online task admission/worker assignment với stochastic resource consumption và competitive-ratio bound. [Predictability-Centric Scheduling, OSDI 2024](https://www.usenix.org/conference/osdi24/presentation/bin-faisal) đã tối ưu trade-off predictability, performance và fairness cho nhiều GPU jobs. Do đó **multi-job scheduling, worker assignment, preemption, deadline/predictability và resource fairness đều đã cũ**.

Ranh giới còn lại hẹp hơn: mỗi job không có utility cố định mà có đường **solver quality–time** chưa biết hoàn toàn; dừng/chuyển một solve làm thay đổi assignment hiện tại, rồi tạo ngoại tác lên capacity và khả năng nhận request tương lai. Đóng góp phải mô hình hóa joint scheduler–ridepool dynamics này, hoặc đưa ra regret/SLA guarantee đối với service outcome. Nếu luận văn chỉ viết một hàng đợi CPU, dự đoán runtime, hoặc áp dụng EDF/WFQ cho solver jobs, đóng góp sẽ chưa đủ học thuật.

Đây là giao điểm của operations research, real-time systems và algorithm scheduling; phù hợp với backend .NET hiện có.

### Đầu vào

- hàng đợi request/decision jobs;
- state/độ khó ước lượng của mỗi job;
- các solver có quality–time trace;
- số core/memory;
- SLA p95/p99;
- current feasible incumbent và bounds nếu có.

### Đầu ra

- budget/preemption/switch schedule cho từng job;
- nghiệm trả trước deadline;
- quality/latency certificate;
- overload/fallback log.

### Dữ liệu

- Manhattan DARP chronological stream;
- burst windows từ NYC TLC;
- public DARP-MP/PDPTW với replay arrival times;
- measured runtime trace trên chính máy benchmark.

Arrival burst lấy từ timestamp thật. Các stress multiplier chỉ được gọi là stress scenario, không phải demand thật.

### Baseline

- equal time slice;
- FIFO;
- shortest-job-first theo size;
- fixed solver;
- per-request independent timeout;
- oracle schedule biết trước quality–time curve;
- standard anytime solver không global scheduler.

### Chỉ số

- p50/p95/p99 response time;
- deadline miss rate;
- served rate;
- route cost/wait/detour;
- anytime primal integral;
- CPU time, memory và energy proxy;
- quality degradation dưới burst;
- starvation/fairness giữa jobs.

### Điều kiện thành công

Giữ p99 dưới SLA với deadline miss thấp hơn baseline, đồng thời cải thiện hoặc không làm xấu đáng kể served rate/route quality trong cùng compute budget. Phải có overload curve, không chỉ một tải trung bình.

### Ranh giới

Đây là chất lượng hệ thống tối ưu hóa, không phải fairness xã hội. “Fairness giữa jobs” chỉ có nghĩa không để một loại request liên tục bị thiếu compute, trừ khi có định nghĩa xã hội và dữ liệu phù hợp riêng. Không được tuyên bố “lần đầu phân bổ compute cho anytime algorithms”; ranh giới có thể bảo vệ chỉ là scheduler deadline–quality–service cho stream ridepooling.

---

## 19. Xếp hạng cuối cùng

| Hạng | Đề tài | Vì sao nên chọn | Rủi ro chính |
|---:|---|---|---|
| 1 | **BeGo-COMMIT** | Sát BeGo, đầu vào–đầu ra rõ, chèn động thật, không cần nhãn hành vi giả; đóng góp còn lại là resource path-dependent qua nhiều epoch | Least-commitment, delay threshold và reassignment đã có trong paper/patent; nếu không có cumulative/switch-budget formulation thì novelty sụp |
| 2 | **BeGo-E2ECERT** | Học thuật chắc, chứng minh được, sửa đúng yếu điểm benchmark/pipeline hiện tại | Thiên correctness hơn sản phẩm người dùng |
| 3 | **BeGo-VERIFYACCESS** | Khác biệt xã hội mạnh, nhãn gold thật, gap false-safe rõ | Phạm vi barrier và geographic coverage hạn chế |
| 4 | **BeGo-FAIRCERT-POOL** | Trả lời trực tiếp nỗi lo bias do tự sinh walking limit; novelty còn lại ở comparative certificate có tái tối ưu hai policy | DRO missing-attribute fairness và interference đều đã có; phải chứng minh compositional validity, lý thuyết khó và real case có thể thường xuyên `Inconclusive` |
| 5 | **BeGo-COMPUTEGUARD** | Rất hợp backend, benchmark khách quan; có thể mới nếu nối solver quality–time với externality lên service | Multi-job scheduling/meta-reasoning đã cũ; novelty miền ứng dụng hẹp và cần đo runtime rất kỷ luật |
| 6 | **BeGo-SOLVERGUARD** | Modern ML+OR nhưng có guard/fallback, dùng shift thật | Dynamic heuristic selection đã có; dễ thành “thêm classifier” |
| 7 | **BeGo-XETA-SHIFT** | Dataset lớn, endpoint rõ | Paper 2025 đã nối conformal uncertainty với robust route; novelty còn rất hẹp |

## 20. Khuyến nghị chọn

Nếu muốn mở rộng OptiGo/BeGo hiện tại với khả năng hoàn thiện cao nhất, nên chọn **BeGo-COMMIT**. Nó nối trực tiếp T2 đã bàn:

- T2 quyết định có chờ hay ghép ngay;
- COMMIT quyết định chèn yêu cầu mới vào route đang hoạt động nhưng bảo vệ lời hứa với người đã nhận.

Nếu muốn một luận văn thiên toán/độ đúng và tránh hoàn toàn tranh luận về nhãn hành vi, chọn **BeGo-E2ECERT**.

Nếu muốn đề tài khác biệt, có ý nghĩa xã hội và chấp nhận xây thêm perception/accessibility pipeline, chọn **BeGo-VERIFYACCESS**.

Nếu câu hỏi bạn quan tâm nhất là “tự sinh giới hạn đi bộ có làm kết luận fairness bị bias không?”, chọn **BeGo-FAIRCERT-POOL**. Đây là hướng học thuật khó hơn COMMIT nhưng biến chính điểm yếu dữ liệu đó thành đối tượng cần chứng nhận, thay vì giấu nó trong phần tạo dữ liệu.

Không nên chọn lại T3/T7/T8/T9 cũ chỉ vì dễ mô tả. Các hướng đó đã có va chạm trực tiếp đủ mạnh để novelty trở nên khó bảo vệ.

## 21. Những claim tuyệt đối không được dùng

- “Đây là nghiên cứu đầu tiên trên thế giới” – chưa có systematic review đăng ký trước đủ để nói vậy.
- “Chưa công ty nào làm” – chỉ có thể nói chưa thấy tính năng/thuật toán được công khai.
- “Dữ liệu TLC là toàn bộ nhu cầu” – đây là completed/observed trips.
- “Fairness tốt hơn nghĩa là người dùng thấy công bằng hơn” – cần user study nếu claim về cảm nhận.
- “Unknown accessibility nghĩa là safe” – sai về phương pháp lẫn đạo đức.
- “Solver gap 2% nghĩa là toàn hệ thống cách tối ưu 2%” – chỉ đúng nếu candidate generation không làm mất optimum.
- “Stress test là hành vi thật” – 0/200/400/500 m hoặc demand multiplier chỉ là scenario giả định nếu không có nhãn người dùng tương ứng.
- “Monte Carlo trên một phân bố walking limit chứng minh fairness thật” – sai; nó chỉ đúng điều kiện trên phân bố đã chọn. Chỉ được gọi là robust certificate khi kết luận giữ trên toàn assumption/confidence set đã công bố và có kiểm tra coverage.
