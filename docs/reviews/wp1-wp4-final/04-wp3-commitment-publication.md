# WP3 — promise, commitment ledger và publication

## 10-dimensional hard vector

`CommitmentDimension` khóa thứ tự mười chiều: pickup/drop ETA variation, material
revision count, vehicle switch, pickup/drop relocation distance và stop switch,
incumbent inversion, pre-pickup insertion. `CommitmentVector` là non-negative
canonical integer vector có component-wise checked addition; không scalarize.

`CommitmentPolicy` phải khai đủ mười limit đúng một lần, phase applicability,
budget basis, material rule và locks. `null` nghĩa unbounded; `0` là hard zero.
`CommitmentBudgetEvaluator` kiểm từng dimension và trả witness chính xác ở equality/
one-over/overflow. `CommitmentLockEvaluator` áp assignment/onboard/freeze/final
locks trước budget.

## Promise và three-way delta

`PromiseProjector` lấy route + shared schedule để dựng vehicle/stop/ETA và remaining
service token order. Với rider onboard, pickup đã publication được carry-forward
trong khi drop tiếp tục được chiếu.

`PromiseDeltaCalculator` tách:

```text
exogenous        = old published -> old plan under current world
decision-induced = exogenous projection -> proposed plan
visible          = old published -> proposed plan
```

Ba vector được tính độc lập; không cộng đại số `exogenous + decision` vì absolute
variation và identity switches không có dấu. Inversion token chứa request/kind/
stop ID, relocation dùng explicit directed distance lookup.

## Ledger và incident separation

`CommitmentLedger` append immutable exact-next promise version, unique publication
ID và cumulative budget không refund. `OperationalIncidentLedger` giữ open/resolve/
breach history riêng; normal optimizer không được “bịa incident” để vượt budget.
Checkpoint restore cross-check breach previous promise/budget với entry thật.

## Candidate gate và full validator

`CommitmentCandidateFilter` apply candidate route **và accept new request** vào
candidate run, sau đó gọi scoped `CommitmentDecisionValidator`. Scoped validation
giảm work lặp nhưng chỉ tạo candidate gate.

Runner gọi validator lần nữa không scope. Validator tự dựng lại:

1. immutable state boundary;
2. physical plan;
3. promise projection;
4. locks;
5. three-way delta và hard budget;
6. exact ledger publication.

Nó không nhận schedule/delta/budget do solver khai. Vì vậy xoá hard prune ở
algorithm vẫn không thể publish invalid state; mutation test WP4 còn chứng minh
việc xoá gate làm selection/evidence sai và bị test phát hiện.

## Certificate, hash và ACK

Sau validation, Runner tạo promise actions và certificate body bind input/output
state hashes + exact publication IDs. State gồm ledger/incident/plan pool được
stage, decision hash bind toàn response, matching ACK mới commit. Retry trước ACK
trả cùng bytes và không append ledger hai lần.

## Evidence

- all-dimension mutation tests cho delta/lock/budget;
- 64 × 12 generated ledger histories cho conservation/no-refund;
- exact-small commitment differential và relaxation property;
- checkpoint/certificate/breach tamper tests;
- WP3 clean-process continuation và WP4 Runner tests giữ certificate/ACK path khi
  policy/solver mới được bật.

WP3 chứng minh cơ chế correctness, không chứng minh hard-vector policy mang lại
user satisfaction hoặc service improvement. C1/C2 ở WP4 chỉ xếp hạng trong set
đã qua chính gate này.
