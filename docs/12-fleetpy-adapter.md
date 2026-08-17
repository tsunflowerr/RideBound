# Adapter FleetPy

## 1. Upstream được khóa

- Repo: [TUM-VT/FleetPy](https://github.com/TUM-VT/FleetPy)
- Tag: `1.0.2`
- Commit: `053aa9d4fcfde91c5d303435d5748f9206c071b0`
- License: MIT
- Paper: [FleetPy: an open source simulator for reproducible research on mobility-on-demand services](https://doi.org/10.1186/s12544-026-00823-3), xuất bản 2026-07-15

Không phát triển adapter trên `main` trôi nổi. Upgrade cần ADR và rerun contract/main experiments.

## 2. Extension points đã kiểm tra trong source 1.0.2

`src/fleetctrl/FleetControlBase.py` cung cấp:

- `receive_status_update(vid, simulation_time, list_finished_VRL, force_update)`;
- `user_request(rq, simulation_time)`;
- `user_confirms_booking(rid, simulation_time)`;
- `user_cancels_request(rid, simulation_time)`;
- `acknowledge_boarding(rid, vid, simulation_time)`;
- `acknowledge_alighting(rid, vid, simulation_time)`;
- `assign_vehicle_plan(...)`;
- `time_trigger(simulation_time)`;
- `_call_time_trigger_request_batch(...)`.

FleetPy có `VehiclePlan`, `PlanStop`, `veh_plans`, `rid_to_assigned_vid` và lock flags trên plan stops. Đây là các điểm map sang event/route prefix của RideBound.

## 3. Thiết kế class

Tạo module ngoài source vendor:

```text
simulators/fleetpy-ridebound/
  ridebound_fleetpy/
    fleet_control.py
    mapping.py
    runner_client.py
    errors.py
  actual_*.py
  capability_probe.py
  tests/
  environment.lock.yml
  README.md
```

`RideBoundFleetControl` kế thừa `FleetControlBase` sau capability preflight. Không sửa
trực tiếp checkout FleetPy. Study config nạp class adapter theo cơ chế FleetPy; Python
chỉ map protocol/callback và gọi Runner published ngoài repository.

## 4. Mapping callback

| FleetPy callback/state | RideBound message |
|---|---|
| `user_request` | `requestArrived` |
| `user_confirms_booking` | `bookingConfirmed`/publish initial promise |
| `user_cancels_request` | cancellation event theo lifecycle |
| `receive_status_update` | `vehicleAdvanced` + finished legs |
| `acknowledge_boarding` | `passengerBoarded` |
| `acknowledge_alighting` | `passengerAlighted` |
| `time_trigger` | `timerTick`/batch decision |
| network travel update | `travelTimesUpdated` |
| runner route suffix | `VehiclePlan` + `assign_vehicle_plan` |

## 5. Offer và commitment

FleetPy có hai bước offer/booking. Main experiment phải chọn một semantics và giữ cố định:

- **Khuyến nghị:** commitment bắt đầu khi `user_confirms_booking`.
- Route trước confirmation là provisional; không tiêu ledger.
- Initial promise publish sau confirmation không tính revision.
- Demand-response model trong main benchmark nên deterministic/immediate để không trộn user-choice model với dispatch policy.

Nếu dùng offer là binding promise, đó là experiment khác và phải có policy/config riêng.

## 6. Vehicle/route mapping

Adapter dựng:

- current vehicle position từ simulation vehicle;
- executed/finished legs từ `list_finished_VRL`;
- route suffix từ `VehiclePlan.list_plan_stops`;
- pickup/drop riders từ boarding/alighting dictionaries;
- lock state từ `PlanStop.is_locked()` và `is_locked_end()`;
- ETA/time-window từ vehicle plan schedule.

Quy tắc freeze:

- FleetPy locked stop luôn được xem là hard frozen.
- Leg đang chạy không được đổi destination nếu FleetPy vehicle API không hỗ trợ an toàn.
- Nếu RideBound lock chặt hơn FleetPy lock, adapter dựng plan mới nhưng giữ RideBound prefix.
- Không gọi `force_assign=True` để phá lock trong normal operation.

## 7. Áp decision

1. Validate response schema/hash.
2. Map node/position về FleetPy positions.
3. Dựng `VehiclePlan` cho từng vehicle bị đổi.
4. Gọi plan feasibility/update bằng routing engine.
5. Gọi `assign_vehicle_plan`.
6. Tạo/refresh offer cho request mới.
7. Gửi `decisionApplied`.

Nếu FleetPy từ chối plan mà certificate core cho là hợp lệ:

- không force;
- ghi adapter mismatch;
- fail run hoặc safe no-op theo pre-rule;
- tạo golden regression.

## 8. Travel-time oracle

Hai lựa chọn:

### Pull

Runner hỏi adapter qua một travel matrix snapshot đã materialize cho nodes liên quan.

### Push

Adapter gửi sparse matrix/arc estimates trong `eventBatch`.

V1 ưu tiên push sparse matrix cho candidate nodes để runner không gọi ngược Python. Snapshot phải có version/hash.

FleetPy dùng giây; adapter chuyển sang integer millisecond theo rounding rule.

## 9. Baseline

- B1 và C1 đều gọi runner.
- Không dùng FleetPy native algorithm làm B1 chính.
- FleetPy native insertion/batch algorithm có thể là `FNATIVE` sanity baseline bổ sung.

Điều này đảm bảo khác biệt B1/C1 là commitment policy, không phải Python vs C# implementation.

## 10. Output

Ngoài output chuẩn FleetPy:

- `commit_protocol.jsonl`;
- `commit_decisions.jsonl`;
- `commit_promises.jsonl`;
- `commit_certificates.jsonl`;
- `commit_adapter_errors.jsonl`;
- `commit_manifest.yaml`;
- `commit_metric_rows.parquet/csv`.

Link mỗi FleetPy user/vehicle row với canonical request/vehicle ID.

## 11. Preflight tests

1. FleetPy example study chạy sạch ở tag pin.
2. Adapter hello/init.
3. Một request accept và hoàn tất.
4. Hai request tạo route revision.
5. Lock stop không bị phá.
6. User cancel trước confirmation.
7. Boarding/alighting lifecycle đúng.
8. Network update tạo exogenous projection.
9. Same scenario B1/C1 paired.
10. Transcript replay trong standalone runner cho cùng decision hash.

## 12. Known risks

- FleetPy offer semantics khác immediate acceptance của RidePy.
- FleetPy plan locks có semantics riêng.
- Network position có thể phức tạp hơn node-only contract.
- Multi-operator/pricing/charging không cần trong v1.
- `force_assign` có thể che lỗi; cấm trong normal experiment.

## 13. Exit gate Layer 2

- Pin version/container/data hash.
- 10 preflight tests pass.
- At least one medium scenario hoàn tất không adapter error.
- B1/C1 dùng cùng runner binary.
- Promise/vehicle lifecycle reconciles với FleetPy logs.
- Main metric script cho cùng kết quả khi đọc transcript hoặc derived table.

## 14. WP7 mechanical closure — 2026-08-16

`RB-WP7-001..014` đã đóng theo ADR-038. Cùng immutable Runner v6 được gọi cho B1 và
C1 trên FleetPy 1.0.2 pin: Runner/FleetControl preflight, lifecycle matrix, actual
FleetPy clock tiny và public-medium physical loop đều pass; medium có ba repeat mỗi arm
và bundle được verifier độc lập đọc từ transcript/manifest. Full .NET suite là 798/798
và pinned Python adapter suite là 50/50, không skip.

Semantic hash của actual FleetPy bind `binarySha256` trong `RunManifestIdentity`, nên nó
**không so sánh được giữa hai Runner artifact**. Muốn đối chiếu hai binary thì phải so
các trường hành vi (publication, request state, vehicle position, epoch/sequence, drain
count), không so hash tổng hợp.

Lớp này chỉ chứng minh mapping, lifecycle, replay/checkpoint và same-Runner mechanics.
Publication count, semantic hash hay raw wall time giữa arm không phải effectiveness,
SLA, fairness, satisfaction hay superiority evidence. Raw result và exact hashes nằm
ngoài repository, được index trong
[`wp7-014-fleetpy-layer2-closure-evidence-2026-08-15.md`](benchmarking/wp7-014-fleetpy-layer2-closure-evidence-2026-08-15.md).
