# WP10 RidePy Layer 3 — negative capability result

> Ngày đóng: 2026-08-23  
> Quyết định: ADR-050/051  
> Phạm vi: cơ học và paired descriptive heterogeneity; không rescue WP9 H6

## 1. Kết luận

RidePy adapter chạy đúng cùng versioned RideBound Runner trong canonical scenario,
nhưng **không qua representative paired-subset gate**. `nodeOnly` không mang đủ
thông tin để project lời hứa khi một event của xe A tạo epoch trong lúc xe B đang ở
giữa một directed edge. Vì ADR-050 cấm suy diễn progress, adapter dừng fail closed.

WP10 vì vậy hoàn tất với **negative capability result**. Layer 3 cross-system claim
chưa được thiết lập. Kết quả này không sửa, pool hoặc cứu kết quả confirmatory âm H6.

## 2. Nguồn và môi trường

| Thành phần | Pin/evidence |
|---|---|
| RidePy | tag `v2.10.1`, commit `bf1863e49a432f2f1f6230f86b2777a5ef5b9f14` |
| Source tree | 527 file, `d99ffac8…d891e`; MIT `87e0c317…1798` |
| Submodules | `lru-cache@13f30ad3…ddfcf`; `googletest@a2b8a8e0…1f47` |
| Container base | `mcr.microsoft.com/dotnet/runtime@sha256:a365ce6a…0e235` |
| Built image | `ridebound/ridepy-wp10:2.10.1`, ID `5468b9cb…e573` |
| Runtime | Linux, Python 3.12.3, .NET runtime 10.0.11 |
| Source/env receipt | external SHA-256 `2b431062…0775`, status `pass` |
| Runner publish | 119 files, tree `be2b9b14…1d53`; DLL `38da6c3a…ffb4e` |

Image build initially exposed a real upstream-runtime issue: installation metadata
succeeded but import failed because RidePy 2.10.1 imports `pkg_resources`. The image
now pins `setuptools==80.9.0`, and its build gate performs an actual RidePy import and
`Graph` smoke instead of trusting package metadata.

## 3. Adapter và canonical evidence

Adapter:

- subclasses native RidePy `FleetState` and uses `VehicleState`/`TransportSpace`;
- negotiates `nodeOnly` explicitly with the same RideBound Runner;
- maps strict request/time/node identities and complete directed travel snapshots;
- applies all returned stoplists only after every affected vehicle validates;
- advances the native event clock one service timestamp at a time;
- preserves native ordinal when pickup/drop events share a timestamp;
- contains no candidate search, solver, lock or budget rule in Python.

Canonical B1 and C1 each completed 5/5 requests, emitted 5 pickups, 5 drops and 22
Runner decisions. Runner publish-tree hashes were identical before/after. Independent
verification caught binding, extra-inventory, file-hash, native-reconciliation and
transcript-frame mutations. The final request used a different vehicle across arms;
that is descriptive policy heterogeneity, not a service-effectiveness result.

## 4. Frozen subset

Manifest `wp10-ridepy-paired-subset-v1.json` was frozen before final outcome execution:

- three cells: uncongested, insertion pressure, travel-update stress;
- four exogenous realizations per cell;
- B1/C1 paired on requests, initial fleet, graph/update schedule and master seed;
- 24 planned arm jobs;
- unit party, standard service, reassignment disabled;
- manifest SHA-256 `72ca34d2…b337`;
- freeze v3 SHA-256 `18a74fa3…6672`, binding source receipt, image, full Runner
  publish tree and ten outcome-bearing adapter files.

Freeze v1/v2 and their outputs remain retained. They exposed adapter event-ordering
defects and were never reused as outcomes. The final v3 attempt is the only terminal
attempt interpreted here.

## 5. Kết quả mô tả

| Cell | Valid pairs | Arrived/arm | B1 complete | C1 complete | C1−B1 |
|---|---:|---:|---:|---:|---:|
| Uncongested | 4 | 16 | 16 | 16 | 0.00 pp |
| Insertion pressure | 4 | 28 | 20 | 18 | −7.14 pp |
| Travel-update stress | 3 | 18 | 18 | 15 | −16.67 pp |
| **Valid-pair total** | **11** | **62** | **54** | **49** | **−8.06 pp** |

Đây chỉ là 11 cặp hoàn chỉnh còn lại, không phải planned-subset estimand. Không có CI,
population inference, cross-system effectiveness claim hoặc phép so sánh thống kê với
H6. Dấu âm phù hợp hướng WP9 nhưng không được pool.

## 6. Failure quyết định

`travel-update-stress-r3/B1` dừng tại epoch 17, `simTimeMs=116000`, sau khi native
RidePy đã pickup request `rq-d9d824…b770a`. Runner trước đó chỉ quan sát được node cuối
của xe, nên pickup ETA công bố cuối là `178000`; native pickup sớm hơn 62.000 ms.
Fallback validation bác promise projection với:

```text
INTERNAL_ERROR: Fallback validation failed:
PROMISE_PROJECTION_FAILED: Promise stops, ETA order and service order must be valid.
```

Mã capability-level là `RBWP10_NODEONLY_CONCURRENT_MIDEDGE_UNSUPPORTED`. C1 của cùng
job không chạy, đúng no-partial-reuse policy. Transcript failure SHA-256 là
`0ee5e3ec…a85`. Đây không còn là lỗi sắp event: batch cuối đã interleave đúng
`vehicleReachedStop → passengerAlighted → vehicleReachedStop → passengerBoarded`.

## 7. Independent verification và boundary

Analyzer/verifier đọc đúng 11 valid pairs cùng đúng một failure và một paired arm
không chạy; nó bác missing/extra terminal output, đồng thời bind full Runner publish
receipt, solver seed, source/config/arm, file hash, native reconciliation và NDJSON
frame hash. Bảy mutation self-test đều bị bắt. Closure review phát hiện rồi sửa việc
analyzer v1 chưa ép exact terminal inventory/Runner/seed; raw outcome không đổi.
Receipt v2 ngoài repo có SHA-256 `be3e9077…cca3` và
`status=negativeCapabilityResult`.

Không được khắc phục bằng cách giả directed-edge fraction, suy ra progress từ thời
gian, bỏ stress cell, rerun riêng C1, đổi manifest hoặc chuyển simulator sau khi thấy
outcome. AMoD2 có thể là một work package mới, nhưng không phải phép thay thế hậu
kết quả cho WP10 này.

## 8. Verdict

| Gate | Verdict |
|---|---|
| Exact source/environment | PASS |
| Same Runner/no reimplementation | PASS |
| Canonical lifecycle/physical/protocol | PASS |
| Frozen representative paired subset | **FAIL CLOSED** |
| Independent evidence integrity | PASS |
| Layer 3 claim | **NOT ESTABLISHED** |

Kết luận khoa học hữu ích là hẹp nhưng rõ: RidePy 2.10.1 với `nodeOnly` đủ cho
canonical và nhiều paired jobs, nhưng không đủ cho workload có các xe đồng thời ở
giữa cạnh. Giới hạn này cần được xử lý ở contract/simulator capability, không bằng
một heuristic adapter không quan sát được.
