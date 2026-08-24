# RB-WP13-003 — Versioned first-divergence record set

> Trạng thái: **DONE**
> Ngày: 2026-08-24
> Class: post-outcome exploratory mechanism diagnostics
> Không thay đổi H6/WP9/WP10 outcome

## 1. Kết quả

`RB-WP13-003` chuyển projection đã kiểm chứng ở `002` thành contract versioned cho
toàn bộ 40 exact B1/C1 paired unit:

| Panel | Record | Operational divergence trên equal input | Equal-prefix epoch min / median / max |
|---|---:|---:|---:|
| A — 8 xe | 20 | 20 | 3 / 16 / 85 |
| B — 4 xe | 20 | 20 | 3 / 17 / 102 |
| **Tổng** | **40** | **40** | **3 / 16,5 / 102** |

Đây là vị trí hành vi quan sát được bắt đầu khác, không phải causal attribution. Cả
40 record đều giữ nhãn `equalObservedInputNotFullInternalState`; state hash khác ở
40/40 pair trước divergence không được dùng làm alignment. Wire-only publication
order khác sớm hơn ở 30/40 pair vẫn được tách riêng.

## 2. Exact input và output

Nguồn duy nhất là canonical inventory report đã đóng ở `002`:

- path: `E:\RideBoundData\wp13\h6-evidence-inventory-v1.json`;
- length: `73.102` byte;
- SHA-256: `6d36bc6e781f9fa5c32a024c3f5350271b806f43a7418f148ef5138fa1fff63e`;
- analyzer source SHA-256:
  `0563ef2495550345c587ddbe07cf47a0ff88c38bf2b618dae6723c881ab1ab3b`;
- solver-evidence verifier SHA-256:
  `3eebec96b8370db2c4879adeaede3e67b7344571299a496953afcbc599dd93e5`.

Output derived nằm ngoài hai immutable H6 roots:

- path: `E:\RideBoundData\wp13\first-divergence-record-set-v1.json`;
- length: `102.746` byte;
- SHA-256: `bef27519b5dae4482029be83cd8d1c2b1e0ef2afa72ea63e6645f4991e425618`;
- schema SHA-256:
  `47e24bb394a3949b54ffbfe697e24dfee01eed679b98b0954375f3138fa3d8b8`;
- generator SHA-256:
  `4f52b76baa3f34b16975a57abb1acb44901e2adcd7afc507f95b5ae4987f74f8`.

Compact source-controlled binding nằm tại
`docs/benchmarking/evidence/wp13-first-divergence-record-set-v1-summary.json`.

## 3. Contract

Schema Draft 2020-12 strict nằm tại
`benchmarks/schemas/wp13/v1/first-divergence-record-set.schema.json`. Mỗi record bind:

- panel/unit/source-scenario và exact B1/C1 labels;
- equal-prefix decision count;
- state-hash mismatch và wire-only difference chỉ như audit fields;
- classification cùng relation `equal`, `different`, `notComparable` hoặc
  `equalThroughTranscript`;
- arm evidence chỉ khi thực sự recorded: epoch/time, observed/operational/wire
  projection hashes, ordered event/action types;
- source-report, panel-inventory, schema và generator hashes;
- noncausal/read-only claim boundary.

Không dùng `null` trong record set. Conditional schema yêu cầu evidence của cả hai arm
cho paired decision/input divergence, đúng một arm cho transcript-length divergence và
không arm evidence cho `noneObserved`. Generator còn kiểm:

- equal-input hash phải bằng, operational-decision hash phải khác;
- divergence epoch phải bằng `equal-prefix + 1`;
- first state/wire mismatch phải thật sự trước divergence;
- panel counts, classification counts, unit uniqueness và frozen inventory phải khớp
  report `002`.

## 4. Ranh giới diễn giải

Record này chưa trả lời candidate nào bị loại vì lock/budget/ranking, chưa tính minimal
relaxation và chưa nối completed-service delta. Những việc đó thuộc `004..007`.
Không được dùng 40/40 divergence để claim causal effect, population inference, rescue
primary H6 hoặc chọn policy v2.

## 5. Reproduction

```powershell
$env:PYTHONDONTWRITEBYTECODE = '1'
& 'E:\RideBoundData\wp7\envs\fleetpy-1.0.2-repro\python.exe' `
  'simulators\fleetpy-ridebound\wp13_first_divergence_records.py' `
  --inventory-report 'E:\RideBoundData\wp13\h6-evidence-inventory-v1.json' `
  --immutable-root 'E:\RideBoundData\wp9\confirmatory-h6-panela' `
  --immutable-root 'E:\RideBoundData\wp9\confirmatory-h6-panelb' `
  --output 'E:\RideBoundData\wp13\first-divergence-record-set-v1.json'
```

## 6. Verification

- targeted schema/projection/receipt/mutation tests: 11/11 pass;
- full pinned CPython 3.10/FleetPy suite: 122/122 pass, zero skip;
- independent `jsonschema` Draft 2020-12 + file-hash + binding + invariant check:
  pass trên 40/40 record;
- required `dotnet test RideBound.slnx`: 856/856 pass, zero skip;
- source report và hai H6 roots không bị sửa.

Review lần một đã loại dependency ngầm vào analyzer module, thêm safe-integer bound,
hash/epoch consistency, aggregate state-mismatch reconciliation, unique records và
explicit immutable-root output guard. `RB-WP13-004` là queue head kế tiếp; chưa có
mechanism conclusion nào được mở sớm.
