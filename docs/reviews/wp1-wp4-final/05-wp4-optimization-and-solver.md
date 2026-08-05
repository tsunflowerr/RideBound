# WP4 — tối ưu candidate, policies và solver

## 1. Portable lexicographic model

`CandidateSelectionProblem` canonicalize vehicles/requests/options, yêu cầu đúng
một no-op mỗi vehicle và contribution canonical cho từng ordered objective.
`CandidateSelectionSolution` kiểm đúng một option mỗi vehicle, request uniqueness
và tự aggregate `Sum`/`Maximum` với overflow protection. Đây là model độc lập
solver; policy order được biểu diễn trực tiếp, không nhét thành weighted sum.

`SolverBackedFleetSelector` map policy hierarchy:

| Policy | Ordered objectives sau accepted count |
|---|---|
| B1/B3/B4 | operational cost → one stable ID-rank level per vehicle |
| B2 | material revision count → 10 revision dimensions → cost → IDs |
| C1 | maximum hard utilization ppm → 10 revisions → cost → IDs |
| C2 | maximum hard utilization → 10 warning excess → 10 revisions → cost → IDs |

Nếu không có active hard limit, C1 rơi đúng về B1. Nếu không có enabled warning,
C2 rơi đúng về C1. Per-vehicle ID rank levels giữ ordinal vector tie-break mà
không tạo hệ số trọng số lớn/overflow.

## 2. Forward slack, cache và executable waiting

`ForwardSlackProfileBuilder` chiếu earliest schedule rồi backward-propagate local
pickup/ride-time slack qua waiting. Certificate chỉ chứng minh một pure delay
không phá hard deadline; vượt certificate không tự chứng minh infeasible.

Cache key bind run snapshot, vehicle/position, exact route fingerprint, evaluation
time và travel version/hash. Cache đổi kết quả tính toán lặp, không đổi semantics;
cached/uncached equivalence và invalidation đều có tests.

`OriginHoldCandidateTransformer` chỉ chuyển waiting đã tồn tại ở first mutable
pickup thành waypoint service tại current node. Nó không áp dụng trên edge hoặc
khi frozen prefix còn lại. Transformed route được physical validate, dựng profile
lại và phải giữ exact service/departure/cost; nếu không thì prune. Đây là waiting
control executable, không chỉ sửa ETA metadata.

## 3. Deterministic bounded best-first generation

`InsertionCandidateGenerator` dùng priority queue theo potential accepted count,
mandatory service lower bound, slack class/reserve và stable search ID. Priority
chỉ quyết thứ tự dưới work cap; physical validator vẫn quyết feasibility.

Khi work/candidate/request/repair cap omit:

- request omission ghi exact ordered IDs;
- frontier subtree count dùng combinatorial count và saturating flag;
- feasible candidates bị cap có stable digest;
- exact-small mode fail thay vì omit;
- bounded mode truyền count/digest đến pre-solve diagnostics.

Search-node digest giữ ordered route tokens; vì vậy hai suffix khác order không bị
de-duplicate nhầm. No-op luôn được giữ kể cả candidate cap bằng một.

## 4. B2–B5 mechanisms

### B2 — rolling penalty

`CommitmentCandidateAssessor` dùng một policy copy bỏ cumulative limits/locks
nhưng giữ material revision rule. Mọi raw candidate được đánh giá, không hard-prune,
rồi selector minimize material + full revision vector sau acceptance. B2 vì vậy
đo disruption preference, không lẫn hard feasibility treatment của C1.

### B3 — fixed freeze horizon

`MechanismCommitmentPolicyProvider.FixedFreeze` thêm explicit positive horizon và
known lock mask, vẫn bỏ cumulative limits. Candidate filter dùng inclusive freeze
semantics. Config thiếu/thừa freeze field fail; không có horizon mặc định lấy từ
paper.

### B4 — no-reassignment repair

`WaitingIncumbentRepairSeedBuilder` chọn waiting incumbent đã assigned đúng vehicle,
có complete mutable pickup/drop pair và chưa onboard. Mỗi seed atomically remove
đúng một pair rồi reinsert precedence-preserving trên **cùng vehicle**. Nó không
combine nhiều repair, không chạm frozen/onboard và không mở O-001 reassignment.
Repair request cap/loss tách riêng.

### B5 — least-commitment consensus

`MultiplePlanFleetSelector` enumerate globally consistent fleet combinations trong
explicit work cap, chọn operational baseline assignment, loại alternative không
cùng accepted assignment hoặc không executable, canonicalize/de-duplicate, Pareto
filter theo acceptance/cost/min slack, rồi max-min retain diverse plans. Distinguished
plan maximize shared executable-prefix consensus; chỉ plan đó publish.

Alternative routes được rebase lên version sau distinguished route. `VersionedPlanPool`
bind exact route order/progress/version/stops vào SHA-256 ID, có one distinguished
same-epoch plan và advance pool version. Pool nằm trong canonical state/checkpoint;
tamper ID/version/distinguished relation bị reject.

## 5. C1 và C2

`HardVectorCandidateAssessor` dùng cùng WP3 validator pass để vừa hard-filter vừa
tính worst cumulative utilization. PPM dùng `UInt128` và ceiling integer; nó chỉ
xếp hạng, không quyết hard feasibility. Zero hard limit với zero usage được biểu
diễn boundary một triệu ppm để tránh chia zero.

C1 minimize worst utilization rồi full decision-induced revision vector. C2 dùng
cùng hard-feasible set, thêm explicit 10-dimensional warning excess trước revision.
Warning chỉ enabled nếu có finite hard limit và không vượt hard; disabled warning
được biểu diễn bằng omitted `warningLimit` trong strict canonical JSON.

## 6. OR-Tools multi-pass adapter

`OrToolsCandidateSelectionSolver` dựng Boolean option variables, equality một
option/vehicle và at-most-one assignment/request. Maximum objective dùng explicit
max variable; sum upper bounds được checked.

Mỗi lexicographic level rebuild model, fix equality của các level **đã OPTIMAL**,
set current objective và solve với one worker, explicit seed, remaining conflict
work và deterministic time. `FEASIBLE` không bị nâng thành `OPTIMAL` và không được
dùng để khóa pass sau. Best bound được làm tròn theo objective sense và diagnostics
giữ exact rational gap. Selected IDs được dựng lại thành portable solution để kiểm
constraints/aggregation trước khi trả.

## 7. Deadline và safe fallback

Generation, validation và solver có budget riêng. `SafeCandidateSelectionExecutor`
thử theo thứ tự:

1. solver incumbent nếu có;
2. canonical all-no-op;
3. từng one-request selection sorted theo exact objectives + IDs.

Mỗi attempt tiêu một validation work unit. Hết budget hoặc tất cả invalid trả
`UNKNOWN` không solution. Valid fallback trả `SAFE_FALLBACK` với diagnostics mới
không giả solver bounds; primary solver diagnostics vẫn được giữ riêng. Không có
incident override giả.

## 8. Runner integration

Registry có đúng bảy canonical names. Strict WP4 config cho common generation,
solver budgets và chỉ mechanism-specific B3/B4/B5/C2 fields. Content hash được
domain-bind với WP3 commitment config; manifest mismatch fail trước state creation.

B1–B4/C1/C2 đi qua production mapper + OR-Tools; B5 đi qua plan pool. Sau policy
validation, Runner vẫn full-validate lại, tạo certificate/hash, stage state và chờ
matching ACK. Solver status dùng shell có sẵn nên WP4 không đổi protocol hash
contract.
