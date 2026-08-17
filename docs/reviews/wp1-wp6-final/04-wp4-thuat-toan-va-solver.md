# WP4: thuật toán và solver

## 1. Candidate generation có work budget minh bạch

`InsertionCandidateGenerator` sắp pending request theo latest pickup, arrival và ID.
Exact-small enumerate đủ trong bound. Bounded mode dùng best-first lower-bound:
potential accepted, mandatory service cost, slack class/reserve và stable ID.

Feasibility chỉ được kết luận sau khi route terminal qua physical validator. Search cap,
request cap, feasible cap và repair omission có diagnostic riêng. Ranking giữ nhiều
accepted, cost thấp, slack tốt, stable ID và luôn giữ no-op. Vì vậy cap có thể gây
omission nhưng omission không bị gọi là infeasible/global optimum.

`ForwardSlackProfile` chứng nhận phần route còn slack bao nhiêu; cache bind state,
vehicle, route, position, evaluation time và travel snapshot. Cache miss/không đủ
certificate chỉ làm mất tối ưu tốc độ, không bỏ validation.

`OriginHoldCandidateTransformer` chỉ đẩy waiting về origin khi executable; transformed
route được revalidate và phải giữ exact service/departure/cost semantics đã công bố.

`WaitingIncumbentRepairSeedBuilder` chỉ repair same-vehicle waiting incumbent. Nó không
mở cross-vehicle reassignment.

## 2. Policy families

- B1 rolling cost: accepted count max, route cost min, stable ID.
- B2 revision penalty: thêm revision objective nhưng vẫn qua hard publication gate.
- B3 soft/hard hybrid: soft preference không được override hard vector.
- B4 hard vector/repair: giữ no-reassignment và only same-vehicle repair.
- B5 multiple-plan: tách riêng vì không cùng comparator mechanics với common-candidate.
- C1/C2: treatment variants dùng commitment semantics đã khóa.

## 3. Multiple-plan policy

`MultiplePlanConsensusPolicy` enumerate fleet combination nhất quán trong work budget.
Alternative phải giữ cùng new assignment và executable. Pool canonical/de-duplicate,
Pareto theo accepted/cost/min-slack, rồi max-min diversity. Distinguished plan được
chọn bằng shared-prefix consensus; alternatives chỉ là future option và được rebase
từ distinguished version. Chỉ distinguished plan được apply.

Điều này lấy cảm hứng từ multiple-plan DARP nhưng cố ý không copy assumption “luôn có
lợi”: paper cho thấy consensus/preemptive stop đôi lúc kém. Vì thế B5 là baseline riêng,
không được gộp vào paired B1/C1 claim.

## 4. OR-Tools adapter

Model có một Bool cho mỗi candidate option, exactly-one mỗi vehicle và at-most-one mỗi
request. Objective multi-pass thực thi lexicographic Sum/Maximum; chỉ objective pass đã
`Optimal` mới được khóa bound cho pass sau. Nếu solver trả `Feasible`, adapter trả ngay
với status/bounds thật, không giả optimal.

Determinism dùng một worker, seed explicit, conflict/deterministic-time budget. Output
được dựng lại thành portable solution rồi production code vẫn gọi validators/fallback.
UNKNOWN/MODEL_INVALID/INFEASIBLE không trở thành arbitrary solution.

## 5. Fair comparison ở WP6

`wp4-common-candidate-v1` chỉ nhận B1/B2/B3/B4/C1/C2, cần ít nhất hai arm và buộc cùng:
candidate generator/work, validator, solver/version/work và capability selection.
Policy config dùng semantic WP4 binding, không chỉ hash raw JSON. B5 bắt buộc pairing
class riêng. Đây là lý do comparator không phải vài `if` đổi policy ID.
