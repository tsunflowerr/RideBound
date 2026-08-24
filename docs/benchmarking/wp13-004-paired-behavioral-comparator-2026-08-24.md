# RB-WP13-004 — Paired behavioral comparator

> Trạng thái: **DONE**
> Ngày: 2026-08-24
> Class: post-outcome exploratory mechanism diagnostics
> Không thay đổi H6/WP9/WP10 outcome

## 1. Kết quả

Comparator đọc lại exact first-divergence epoch của 40 B1/C1 pair từ raw primary
transcripts. Kết quả immediate request disposition và primary difference class là:

| Panel | Pair | C1 nhận ít hơn ngay tại epoch | Số nhận bằng nhau | Tổng delta nhận C1−B1 | Primary disposition | Primary vehicle assignment | Primary plan |
|---|---:|---:|---:|---:|---:|---:|---:|
| A — 8 xe | 20 | 3 | 17 | −3 | 3 | 16 | 1 |
| B — 4 xe | 20 | 5 | 15 | −5 | 5 | 11 | 4 |
| **Tổng** | **40** | **8** | **32** | **−8** | **8** | **27** | **5** |

Không có pair nào C1 nhận nhiều request hơn B1 ngay tại epoch đầu tiên hai arm có hành
vi operational khác nhau. Trong 32 pair có accepted count bằng nhau, 27 pair đổi exact
accepted vehicle; năm pair còn lại có primary difference ở vehicle plan. Vì classes là
multi-label, cả 40/40 pair đồng thời có `vehiclePlanDifference` và
`promiseProjectionDifference` tại epoch đó.

Đây không phải decomposition của completed-service loss. Nó chỉ mô tả action được quan
sát tại một epoch đã định vị trước bằng contract `003`; trajectory sau epoch và completed
outcome không được comparator này đánh giá.

## 2. Exact input, tool và output

Input contract:

- first-divergence record set: `102.746` byte, SHA-256
  `bef27519b5dae4482029be83cd8d1c2b1e0ef2afa72ea63e6645f4991e425618`;
- record schema SHA-256:
  `47e24bb394a3949b54ffbfe697e24dfee01eed679b98b0954375f3138fa3d8b8`;
- record generator SHA-256:
  `4f52b76baa3f34b16975a57abb1acb44901e2adcd7afc507f95b5ae4987f74f8`;
- source inventory report: `73.102` byte, SHA-256
  `6d36bc6e781f9fa5c32a024c3f5350271b806f43a7418f148ef5138fa1fff63e`;
- inventory analyzer SHA-256:
  `0563ef2495550345c587ddbe07cf47a0ff88c38bf2b618dae6723c881ab1ab3b`;
- solver-evidence verifier SHA-256:
  `3eebec96b8370db2c4879adeaede3e67b7344571299a496953afcbc599dd93e5`.

Comparator source `simulators/fleetpy-ridebound/wp13_behavioral_comparator.py` có
SHA-256 `f2c55e1f7fbe9cb341cb6c75764a192254aa2e375de0547780c94c83b01dd0ee`.
Nó fail closed nếu bất kỳ identity trên, frozen panel inventory, bundle/arm/unit/scenario,
target epoch/time/projection hash, frame/file receipt, solver evidence hoặc terminal
closure khác.

Canonical external report nằm ngoài immutable roots:

- path: `E:\RideBoundData\wp13\behavioral-comparator-v1.json`;
- length: `79.864` byte;
- SHA-256: `3717f093c62c37a339da0b826323fb1604a684bd9990630d9d9dc5563fd4f7e3`.

Compact source-controlled binding nằm tại
`docs/benchmarking/evidence/wp13-behavioral-comparator-v1-summary.json`.

## 3. Coverage và contract

Comparator quét đủ 80 primary transcripts đến terminal shutdown và EOF, xác minh
44.156 decision cùng solver evidence v1.0.0 ở mọi decision. Nó bind exact raw manifest
và transcript hashes về từng comparison record. Per-request records không chứa `null`;
vehicle ID chỉ hiện diện khi disposition là `accepted`.

Ordered difference classes là:

1. `requestDispositionDifference`;
2. `acceptedVehicleAssignmentDifference`;
3. `requestActionPayloadDifference`;
4. `vehiclePlanDifference`;
5. `promiseProjectionDifference`;
6. `solverStatusDifference`;
7. `otherActionDifference`.

Primary class là class đầu tiên đúng trong thứ tự này; `differenceClasses` giữ toàn bộ
classes đúng để không làm mất plan/publication differences. Outcome actions của mỗi arm
phải phủ song ánh exact arrived request IDs; duplicate hoặc missing outcome làm cả run
thất bại.

## 4. Ranh giới diễn giải

Report tự gắn nhãn `postOutcomeExploratory`, `descriptiveNotCausal`,
`downstreamService: notEvaluatedByThisArtifact` và `confirmatoryGate: null`. Vì vậy:

- −8 là immediate accepted-count delta trên 40 first-divergence epochs, không phải
  completed-arrival denominator hay estimator của service effect;
- 27 vehicle-assignment differences và 40 plan/publication differences không chứng minh
  lock, budget hay ranking là nguyên nhân;
- không có CI, population inference, H6 rescue hoặc policy-v2 selection;
- candidate/prune witness attribution và minimal relaxation chỉ được mở ở ticket `005`.

## 5. Reproduction

```powershell
$env:PYTHONDONTWRITEBYTECODE = '1'
$env:RIDEBOUND_FLEETPY_ROOT = 'E:\RideBoundData\wp7\FleetPy-1.0.2'
& 'E:\RideBoundData\wp7\envs\fleetpy-1.0.2-repro\python.exe' `
  'simulators\fleetpy-ridebound\wp13_behavioral_comparator.py' `
  --record-set 'E:\RideBoundData\wp13\first-divergence-record-set-v1.json' `
  --panel 'A=E:\RideBoundData\wp9\confirmatory-h6-panela' `
  --panel 'B=E:\RideBoundData\wp9\confirmatory-h6-panelb' `
  --output 'E:\RideBoundData\wp13\behavioral-comparator-v1.json'
```

## 6. Verification

- targeted classification/binding/receipt/mutation tests: 13/13 pass;
- full pinned CPython 3.10/FleetPy suite: 135/135 pass, zero skip;
- independent canonical-byte/hash, 40-unit uniqueness, per-request no-null và
  record-to-aggregate reconciliation: pass;
- required `dotnet test RideBound.slnx`: 856/856 pass, zero skip;
- `git diff --check` và 100-character Python line scan: pass;
- source report, record set và hai H6 roots không bị sửa.

Review lần hai bổ sung explicit source-inventory/verifier identity binding, exact
bundle arm/unit/scenario binding, full-tail traversal test và aggregate multi-label
reconciliation. `RB-WP13-005` là queue head Ready duy nhất.
