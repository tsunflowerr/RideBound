# Bản đồ file và cách tái hiện WP1–WP7

Tài liệu này là một bản đồ để đọc mã theo luồng thực thi. Nó không thay thế các test
hay chứng cứ ngoài repository; tên file có thể được mở từ root `E:\Code\RideBound`.

## 1. Luồng code cần đọc trước

| Lớp | File/nhóm file | Vai trò và invariant chính |
|---|---|---|
| Contract | `src/RideBound.Contracts/` | Event, response, config và canonical JSON có version/hash; field unknown hay payload không hợp lệ fail closed. |
| State | `src/RideBound.Domain/` | Reducer là owner duy nhất của request/vehicle/route lifecycle; prefix đã thực thi không bị candidate sửa. |
| Commitment | `src/RideBound.Application/Commitment/` | Projection → lock → vector budget là ba stage độc lập; certificate bind đúng response/promise/ledger. |
| Candidate | `src/RideBound.Algorithms/Candidates/InsertionCandidateGenerator.cs` | Enumerate pickup/drop precedence-preserving; bounded best-first xếp cả repair root bằng chính projected suffix của root. |
| Portfolio | `src/RideBound.Algorithms/Candidates/CandidatePortfolioRetainer.cs` | Với opt-in WP7, cap giữ no-op, cost anchor theo service set, rồi stability anchor; retained candidate không được giấu request ngoài `NewRequestIds`. |
| Hard vector | `src/RideBound.Algorithms/Commitment/HardVectorCandidateAssessor.cs` | Recompute full candidate state theo từng vehicle/request, không tin score cache hoặc chỉ kiểm ETA. |
| Solver | `src/RideBound.Solvers.OrTools/` và `src/RideBound.Application/Policies/` | Conflict/service-set model, lexicographic objective, typed solver status và fallback được validate lại trước publish. |
| Runner | `src/RideBound.Runner/` | NDJSON lifecycle, init/event/decision/ACK/checkpoint; Python chỉ là client của binary này. |
| FleetPy bridge | `simulators/fleetpy-ridebound/ridebound_fleetpy/` | Chuyển callback/position/plan của FleetPy thành protocol; không có copy Candidate, validator hay policy C#. |
| Independent checks | `tests/RideBound.Algorithms.Tests/`, `tests/RideBound.Runner.Tests/`, `simulators/fleetpy-ridebound/tests/` | Oracle, mutation, lifecycle, lock, process and actual-adapter regressions. |

## 2. Các ràng buộc có thể kiểm trực tiếp

1. Một candidate không thể qua chỉ nhờ `if` ETA: physical route, frozen prefix,
   lock và 10 dimension commitment đều được kiểm lại.
2. `CommitmentLockEvaluator` dùng plan **exogenous → candidate**, còn plan trước chỉ
   xác định horizon đang bị khóa. Nhờ đó traffic/exogenous change không bị báo sai
   là policy revision.
3. Portfolio mới chỉ có hiệu lực khi config chọn
   `ServiceSetStabilityPortfolioV1`; config cũ giữ legacy byte/content semantics.
   No-op được giữ và candidate non-no-op phải khai báo đúng tập request mới.
4. Repair B4 là remove/reinsert trên cùng xe, không reassignment. Root repair phải
   được xếp theo suffix đã repair; nếu dùng route cũ, work cap có thể tiêu vào một
   đánh giá không đại diện. Regression `WaitingIncumbentRepairTests` khóa điểm này.
5. FleetPy chỉ dùng `force_assign=False`; active locked leg phải equivalent hoặc run
   fail typed. Position edge có hướng, không suy từ clock và không tự thêm reverse arc.

## 3. Lệnh tái hiện hiện hành

```powershell
Set-Location E:\Code\RideBound
dotnet test RideBound.slnx
dotnet format RideBound.slnx --verify-no-changes --no-restore

$env:RIDEBOUND_FLEETPY_ROOT = 'E:\RideBoundData\wp7\FleetPy-1.0.2'
E:\RideBoundData\wp7\envs\fleetpy-1.0.2-repro\python.exe -m unittest discover `
  -s simulators\fleetpy-ridebound\tests -p 'test_*.py'
```

Kết quả source-state closure là .NET `798/798`, Python adapter `50/50`. Artifact
actual FleetPy nằm ngoài Git tại
`E:\RideBoundData\wp7\results\candidate-portfolio-v8-identity-hotpath-20260817`
(Runner v8 `13bf5d9b…c179e`); root v6 được giữ nguyên như bằng chứng lịch sử của
ADR-038 và không bị ghi đè;
manifest và exact command/hash đọc tại
[`wp7-014-fleetpy-layer2-closure-evidence-2026-08-15.md`](../../benchmarking/wp7-014-fleetpy-layer2-closure-evidence-2026-08-15.md).
Các số này chứng minh cơ học/repeatability của scope đã khai báo, không chứng minh
C1 hiệu quả hơn B1, SLA, fairness hay user satisfaction.

## 4. Cách review thay đổi sau này

- Nếu thay generator/retainer, chạy exact-small/differential tests trước rồi kiểm
  retention digest và service-set conflict columns. Một benchmark nhanh hơn không
  đủ để chấp nhận pruning.
- Nếu thay commitment, tạo witness cho physical, lock và từng vector dimension;
  không sửa certificate serializer để che validator mismatch.
- Nếu thay adapter, trước hết chạy capability probe trên checkout sạch; sau đó
  preflight, lifecycle, tiny và medium bằng **cùng published Runner hash**.
- Nếu muốn nói về effectiveness, dừng ở đây và mở WP8 preregistration. Không dùng
  raw run WP7 để chọn ngưỡng/tuning hay công bố causal quality claim.
