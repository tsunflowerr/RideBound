# WP3: ledger, ràng buộc và publication

## 1. Vì sao đây không phải `if ETA > x`

WP3 quản lý một đại lượng phụ thuộc toàn bộ lịch sử. Một candidate có thể feasible ở
epoch hiện tại nhưng bị cấm vì các revision trước đã dùng hết budget. Ngược lại, delay
do traffic phải được tách khỏi delay do decision để không phạt policy sai nguồn.

Luồng đầy đủ:

```text
route/state hiện tại
  → schedule cũ
  → áp exogenous travel/state change
  → schedule exogenous
  → áp candidate decision
  → schedule mới
  → project ba bộ promise
  → tính ba delta độc lập
  → phase/freeze/final locks
  → cumulative budget
  → full-fleet publication/certificate
  → ACK mới append ledger
```

## 2. Promise và vector 10 chiều

`RiderPromise` bind request, vehicle, pickup/drop ETA, pickup/drop stop, service-order
tokens, phase và version. `CommitmentVector` có đúng 10 chiều không âm:

1. pickup ETA total variation;
2. drop-off ETA total variation;
3. số pickup ETA material revision;
4. số drop ETA material revision;
5. vehicle switch;
6. pickup relocation distance;
7. drop relocation distance;
8. pickup/drop stop switch;
9. incumbent service-order inversions;
10. inserted stops trước service token.

Các dimension được cộng checked, canonical và component-wise. Không weighted-sum để
che việc một dimension hard đã vượt.

## 3. Schedule và promise projection

`RouteScheduleProjector` đi lại tuyến với cùng directed travel snapshot và service
duration. Với xe đang trên edge, phần còn lại dùng ceil để không làm ETA lạc quan.
Pickup đến sớm chờ earliest. `PromiseProjector` yêu cầu assignment accepted có đủ
remaining pickup/drop; onboard thì không được có pickup mới và vẫn phải có drop.

## 4. Ba delta độc lập

`PromiseDeltaCalculator` tính riêng:

- old → exogenous: biến động do thế giới/traffic;
- exogenous → new: biến động do decision;
- old → new: thay đổi rider nhìn thấy.

Không suy `visible = exogenous + decision`, vì material threshold, switch và inversion
không tuyến tính. ETA dùng total variation qua lịch sử; material count chỉ tăng khi
vượt threshold đã khóa. Service order dùng token ổn định `(requestId, kind, stopId)` và
đếm inversion của incumbent, nên insert request mới không bị nhầm thành đảo toàn tuyến.

## 5. Ledger không hoàn lại

`CommitmentLedger` bắt đầu version 1 với cumulative zero. Mỗi accepted revision append
event mới, version tăng đúng một, cumulative = previous + decision delta. Exogenous
delta và visible delta được giữ làm evidence nhưng không refund budget đã dùng. Vì thế
policy không thể làm ETA xấu rồi tốt lại để “xóa nợ”.

## 6. Budget và locks

`CommitmentBudgetEvaluator` so từng chiều `before + decisionDelta <= hardLimit`; witness
ghi before/delta/after/limit. Zero là hard zero, unbounded là explicit—not magic maximum.

`CommitmentLockEvaluator` xử lý ràng buộc không diễn đạt đủ bằng vector:

- accepted request không chuyển xe theo O-001;
- onboard phase khóa pickup/vehicle;
- freeze horizon khóa service sắp thực thi khi policy bật;
- final confirmation khóa promise cuối khi policy bật.

Locks chạy trước budget để witness nói đúng nguyên nhân structural.

## 7. Validator độc lập và publication gate

`CommitmentDecisionValidator` không tin score/filter của policy. Nó kiểm structural
fleet boundary, gọi physical validator, project schedule/promise, tính delta, áp lock,
áp budget, dựng ledger mới và kiểm proposed fleet hoàn chỉnh. Runner gọi validator này
lần nữa ngay trước khi encode certificate/actions.

Certificate bind input state hash, proposed state hash, policy/config, promise/ledger
versions, delta/budget và exact action IDs. ACK phải trùng pending decision; nếu không,
không ledger nào được committed.

## 8. Incident/breach tách normal operation

`OperationalIncidentLedger` mở incident trên các xe đã biết, snapshot affected riders,
và ghi breach chronology khi safety action buộc vượt normal budget. Nó không reset/refund
ledger và không biến breach thành normal certificate. Safety projection có thể dùng xe
cứu hộ, nên affected-vehicle guard dựa incident/old assignment chứ không ép rescue
vehicle phải nằm trong affected set.

WP3 chưa tối ưu recovery route. Nó chỉ bảo đảm sự cố không bị che và evidence đúng.

## 9. Checkpoint/restore

Checkpoint chứa canonical full state, promise ledgers, incident ledgers, plan pool,
travel snapshot, cursor và previous decision hash. Restore rehydrate qua domain
constructors, kiểm entity cross-reference, route/assignment, time/version/hash và
reachable state. Checkpoint bị cấm lúc có pending decision để không chụp trạng thái
nửa commit.
