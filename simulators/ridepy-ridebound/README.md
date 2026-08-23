# RidePy adapter

WP10 Layer 3 dùng exact RidePy `v2.10.1`/`bf1863e…9f14` trong Linux container và
gọi cùng versioned RideBound Runner. Vendor checkout/build/output nằm ngoài repo.

Ticket/gate đã đóng: [`docs/tasks/40-wp10-ridepy-layer3-ticket-plan.md`](../../docs/tasks/40-wp10-ridepy-layer3-ticket-plan.md).
Canonical pass nhưng representative subset fail closed bằng
`RBWP10_NODEONLY_CONCURRENT_MIDEDGE_UNSUPPORTED`; Layer 3 claim chưa được thiết lập.

Verify source pin:

```powershell
python -B wp10_source_verify.py --source-root E:\RideBoundData\wp10\ridepy-v2.10.1
```

Build environment (Docker context là exact external checkout):

```powershell
docker build --file E:\Code\RideBound\simulators\ridepy-ridebound\Dockerfile `
  --tag ridebound/ridepy-wp10:2.10.1 `
  E:\RideBoundData\wp10\ridepy-v2.10.1
```

Không thêm hard-vector/budget/solver logic vào Python adapter. Capability hiện hành
là `nodeOnly`, unit party và main reassignment disabled; mọi deviation phải có tên.

Outcome report:
[`wp10-ridepy-layer3-negative-capability-result-2026-08-23.md`](../../docs/benchmarking/wp10-ridepy-layer3-negative-capability-result-2026-08-23.md).
External freeze/output/receipts được giữ dưới `E:\RideBoundData\wp10`; không copy
vendor checkout hoặc raw bundles vào repository.
