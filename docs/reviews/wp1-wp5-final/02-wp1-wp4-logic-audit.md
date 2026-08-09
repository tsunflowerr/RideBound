# Logic audit WP1–WP4

## WP1 — protocol và determinism

WP1 khóa wire semantics trước khi có thuật toán. `ProtocolEnvelopeCodec` parse một
NDJSON object nghiêm ngặt; validators yêu cầu đúng field, schema, identity,
safe-integer và canonical unit. `CanonicalJson` sắp object key ordinal, giữ array
order, cấm float/NaN và invalid Unicode. `ProtocolHash` dùng domain tag riêng cho
manifest, event, decision, certificate, checkpoint; vì vậy cùng bytes ở hai loại
artifact không bị coi là cùng semantic hash.

Audit source xác nhận:

- hello chỉ chọn subset capability đã offer, downgrade phải có policy ID;
- initialize bind binary SHA/core commit/config hash/source conversions;
- event batch bắt buộc epoch/event sequence liên tục và simulation time không lùi;
- decision/ACK/checkpoint cùng run/scenario/epoch/hash;
- NDJSON reader/writer có byte/line bound và strict UTF-8.

Điểm tối ưu: canonical bytes được hash/lưu một lần và tái dùng; ordering deterministic
loại nondeterminism trước khi benchmark. Đây là protocol engineering, không phải
novel transport claim.

## WP2 — online state, physical validator và B1

`OnlineState` là immutable aggregate. `EventReducer` áp dụng event theo batch và
`EventReductionCoordinator` chỉ trả state mới sau khi cả batch hợp lệ. Route có
executed prefix bất biến và future suffix có thể tối ưu. Travel snapshot mang
version/hash để cache không sống qua dữ liệu giao thông mới.

`PhysicalPlanValidator` recompute capacity, stop order, time windows, maximum ride
time, vehicle/request binding và route progress từ plan, không tin score/cờ feasible
của candidate. B1 tạo insertion candidate rồi chọn deterministic rolling cost.

Audit source xác nhận accepted request không tự biến mất, prefix không rewrite,
time/capacity fail closed và exact-small oracle không gọi production selector.
WP2 chỉ chứng minh physical/B1 mechanics; chưa có promise guarantee.

## WP3 — promise, ledger, independent publication gate

`RiderPromise` giữ versioned published values. `PromiseDeltaCalculator` tách:

- exogenous delta do travel/state bên ngoài;
- decision delta do policy;
- visible/material delta được công bố.

`CommitmentVector` có 10 chiều; `CommitmentLedger` append immutable entry;
budget/lock evaluators tính lại consumption/remaining state. `CommitmentDecisionValidator`
thực hiện thứ tự physical → lock → budget, tạo normal certificate hoặc exact
witness. Runner chỉ publish pending decision sau validator; `decisionApplied`
phải match decision hash trước khi checkpoint trở thành state mới.

Đây không phải vài điều kiện rời: vector arithmetic, ledger chain, certificate
body/hash, publication action IDs và checkpoint hash đều cross-bind. Incident
exogenous được ghi riêng, không “rửa” thành policy violation hay ngược lại.

## WP4 — candidate optimization và solver

Candidate generation dùng bounded best-first enumeration, admissible lower bound,
forward slack và cache có key theo state/route/travel version. Origin hold là
schedule transformer thực, B4 repair chỉ remove/reinsert trong cùng vehicle để giữ
no-reassignment. B5 giữ deterministic plan pool và consensus/dominance semantics.

Policy boundaries:

- B1 rolling cost;
- B2 revision penalty;
- B3 freeze/lock;
- B4 intra-route repair;
- B5 multiple plan;
- C1 hard-vector filter + lexicographic fleet selection;
- C2 soft/hard hybrid.

OR-Tools nằm riêng trong `RideBound.Solvers.OrTools`. Multi-pass solve khóa từng
lexicographic optimum thay vì dùng trọng số tùy ý; scaled dominance có overflow
guard. `UNKNOWN`, timeout hoặc invalid output đi qua validator-pass fallback, không
publish trực tiếp.

Audit source và review cũ thống nhất: common candidate/compute accounting tách
candidate loss, solver loss và publication loss; infinite commitment budget trong
exact-small boundary suy biến về B1 semantics. Microbenchmark chỉ là synthetic
mechanism evidence.

## Cross-WP invariant

Mỗi WP thêm một boundary nhưng không bỏ boundary cũ. WP4 solver không đi vòng
WP3 validator; WP3 không tự sửa physical result của WP2; WP2 reducer không nới
WP1 identity/order/hash. Đây là lý do architecture tests có giá trị, nhưng verdict
vẫn dựa trên source path và independent recomputation chứ không chỉ test pass.
