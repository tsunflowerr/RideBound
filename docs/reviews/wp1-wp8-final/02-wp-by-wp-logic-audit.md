# WP1–WP8 logic walkthrough

## WP1–WP2: protocol tới physical state

Input đi qua strict envelope/version/canonical parser, hash chain và ACK binding rồi
mới tới atomic event reducer. Reducer dựng online request/vehicle/travel state; physical
validator tính connectivity, time, capacity, pickup/drop order, accepted/onboard và
frozen prefix từ candidate state. Adapter không thể bypass bằng cách force FleetPy
assignment vì quyết định áp dụng phải round-trip route/identity và checkpoint.

## WP3: promise và cumulative burden

Mỗi publication lưu previous promise, exogenous counterfactual và published promise.
Mười chiều dùng absolute delta, budget cộng dồn no-refund và overflow fail-closed.
Lock được kiểm trước budget; certificate bind candidate/decision/publication/witness.
Ba delta visible/exogenous/decision không phải đại số cộng, vì đều là khoảng cách
absolute giữa các projection khác nhau.

## WP4: candidate tới solver

Generator enumerate insertion/repair dưới deterministic caps, dựng schedule/slack và
giữ diagnostics phần bị cap. C1/C2 vẫn chạy full physical + commitment assessor;
fleet selection bind một candidate/vehicle và service-set consistency. CP-SAT adapter
không nằm trong Application, có exact-small oracle/fallback tests và audited execution
evidence. Không suy global optimum khi evidence không nói vậy.

Tối ưu pre-WP9 chỉ tái sử dụng exact schedule/slack trong cùng state/route và cache
stable identity; không thêm heuristic prune. Work-profile counters và output giữ exact,
process time giảm khoảng 20–23% trên fixture đo. Đây không phải SLA.

## WP5–WP7: external boundaries

WP5 evidence giữ BeGo là adapter ngoài repository và cùng versioned Runner. WP6 thêm
source/plan/process/store/metric/oracle/bundle chain; verifier không tin status chung mà
tính lại manifest/transcript/checkpoint/metrics. WP7 mapping giữ directed edge progress,
FleetPy callback clock, locked stops và lifecycle order; actual 1.0.2 test sử dụng pin
ngoài repo, không mock lại simulator semantics.

## WP8: experimental design

Experimental unit là scenario+demand+travel realization, không phải rider hay solver
seed. Holdout khả dụng chỉ có 20 unit/5 ngày nên estimand là finite-panel aggregate.
Primary là total decision-induced pickup+drop ETA burden; service completion là cổng
đồng thời strict `m=1,0 pp`. Pilot/frontier không được dùng để nới margin.

Preregistration H0 được giữ nguyên; node-cap amendment đổi đồng nhất request target
128→108 trước outcome. Analysis-integrity amendment thứ hai chỉ bind đúng bundle/source
và decomposition, không đổi endpoint/panel/margin/config/Runner. Freeze receipt v1 được
giữ lịch sử; v2 supersede và có executable hash verifier.
