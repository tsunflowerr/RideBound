# Risk register và kiểm soát phạm vi

## 1. Risk register

| ID | Rủi ro | Xác suất | Tác động | Trigger | Giảm thiểu |
|---|---|---:|---:|---|---|
| K-01 | Prior work trùng claim | Trung bình | Rất cao | Tìm thấy ledger/budget tương đương | Re-audit trước WP8/WP12; thu hẹp claim |
| K-02 | Service rate sụp khi budget chặt | Cao | Cao | Pilot non-inferiority fail mọi mức hữu ích | Pareto/budget tiers; báo negative result |
| K-03 | Synthetic data thiên vị | Cao | Cao | Cải thiện chỉ trên generator tự tạo | FleetPy/TLC/holdout; paired prereg |
| K-04 | RideBound có compute nhiều hơn baseline | Trung bình | Cao | Candidate/time khác | Common generator/deadline; runtime Pareto |
| K-05 | Core bị khóa vào BeGo | Trung bình | Rất cao | Core reference OptiGo/EF/API | Architecture test; contracts trung lập |
| K-06 | Adapter viết lại thuật toán | Trung bình | Rất cao | Logic budget xuất hiện trong Python/C++ | Same runner requirement; review |
| K-07 | Simulator semantics không tương đương | Cao | Cao | Layer results trái dấu | Capability matrix; canonical scenario |
| K-08 | Không tách traffic/decision revision | Trung bình | Cao | Adapter thiếu old-plan projection | Capability gate; static main subset |
| K-09 | Certificate không độc lập | Trung bình | Cao | Validator dùng delta solver | Recompute/mutation tests |
| K-10 | Incident làm hard guarantee sai | Trung bình | Cao | Budget breach bị tính kept | Separate incident ledger/report |
| K-11 | Existing benchmark overclaim | Cao | Trung bình | Li & Lim vẫn không enforce TW | Không dùng primary; deprecation/caveat |
| K-12 | Data/license không redistributable | Trung bình | Cao | Không thể release artifact | Downloader/checksum/recipe |
| K-13 | RidePy adapter quá sâu | Trung bình | Trung bình | FleetState override không ổn định | AMoD2 alternate; bounded preflight |
| K-14 | AMoD2 solver/build nặng | Cao | Trung bình | Gurobi/toolchain blocker | Runner/OR-Tools; RidePy default |
| K-15 | Scope explosion | Cao | Cao | Thêm pricing/RL/transfer/UX sớm | Work package gates; cut list |
| K-16 | User satisfaction overclaim | Cao | Cao | Draft dùng “hài lòng” | Claim audit; user study riêng |
| K-17 | Demographic fairness overclaim | Trung bình | Cao | Dùng Gini gọi là group fairness | Terminology gate |
| K-18 | Reproducibility drift | Trung bình | Cao | Tag/data/env không pin | Hash/digest/lockfile |
| K-19 | Runtime không đáp ứng online | Trung bình | Cao | p95 vượt epoch deadline | safe fallback, cap, incremental eval |
| K-20 | Dirty worktree/user files bị ghi đè | Trung bình | Cao | Untracked docs tồn tại | `git status`, scoped patches, no reset |
| K-21 | Earliest-feasible/single-plan che lựa chọn linh hoạt hơn | Cao | Cao | C1 chỉ hơn/kém B1 do schedule convention | WP4 named wait/hold + multiple-plan baselines; report strategy separately |
| K-22 | Candidate cap gây bias nhưng bị gọi là solver loss | Cao | Cao | exact mode tốt, bounded mode silent-omit | Tách candidate loss/solver loss; admissible bound và deterministic cap audit |
| K-23 | Thêm solve time làm giảm future flexibility | Trung bình | Cao | current cost giảm nhưng future accept/revision xấu | B5 distinguished plan/pool, fixed deadline, measure future deviations; không giả monotonic quality |

## 2. Scope budget

V1 phải hoàn thành:

- B1;
- C1;
- ledger/certificate;
- BeGo replay;
- FleetPy;
- một Layer 3;
- prereg evaluation.

V1 không bắt buộc:

- production deployment;
- AMoDeus;
- tất cả native baselines;
- user study;
- real-time traffic API;
- pricing/rebalancing novelty;
- meeting point personalization.

## 3. Stop/go gates

### Sau WP2

Go nếu online baseline và deterministic replay đúng. Dừng nếu state/protocol không ổn.

### Sau WP4

Go nếu exact-small/infinite-budget equivalence, candidate/solver loss accounting,
certificate và deadline/fallback pass. Không chuyển sang simulator nếu core có
invalid decision hoặc C1/B1 dùng compute/candidate boundary không công bằng.

### Sau WP7

Go pilot nếu FleetPy medium runs reconcile. Nếu không, sửa semantic adapter trước.

### Sau WP8

Go confirmatory nếu:

- có ít nhất một Pareto region hợp lý;
- runtime khả thi;
- margin/prereg khóa;
- không unresolved correctness error.

Nếu pilot cho thấy trade-off xấu, có thể vẫn công bố negative result nhưng không tuyên bố product ready.

## 4. Cơ chế chống scope creep

Mọi feature mới trả lời:

1. Có cần để test RQ1–RQ5 không?
2. Có nằm critical path không?
3. Có làm thay đổi claim/prereg không?
4. Có thể để sau WP9 không?

Nếu không cần, đưa backlog, không chèn vào current WP.

## 5. Red flags

Dừng review ngay nếu thấy:

- `OptiGo.Domain` reference trong Core;
- `DateTime.UtcNow` trong replay decision;
- Python/C++ tự tính commitment budget;
- xóa accepted rider để đạt feasibility;
- reset ledger khi đổi xe;
- report chỉ mean không có tail;
- RideBound chạy nhiều thời gian hơn mà không báo;
- filter run sau khi thấy kết quả;
- claim satisfaction/fairness không có study/data;
- đổi primary metric sau confirmatory run.

## 6. Fallback đề tài

Nếu novelty RideBound bị trùng hoàn toàn, portable framework/certificate vẫn có giá trị engineering nhưng luận văn cần đổi claim. Các hướng kế cận đã audit:

- E2ECERT: certificate toàn pipeline;
- COMPUTEGUARD: compute allocation cấp stream;
- VERIFYACCESS: active verification cho accessibility.

Không chuyển hướng âm thầm; cần quyết định của người dùng và charter mới.
