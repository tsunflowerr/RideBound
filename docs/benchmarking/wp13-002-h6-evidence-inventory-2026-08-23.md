# RB-WP13-002 — H6 evidence inventory và alignment evidence

> Trạng thái: `IN_REVIEW; REQUIRED_DEBUG_CPU_GATE_OPEN`
> Bắt đầu: 2026-08-23; final raw scan/gates: 2026-08-24
> Phân loại: post-outcome exploratory; không có confirmatory gate

## 1. Input bất biến

- Panel A: `E:\RideBoundData\wp9\confirmatory-h6-panela`
- Panel B: `E:\RideBoundData\wp9\confirmatory-h6-panelb`
- 100 bundle, 57.806 decision epoch, mọi transcript được đọc tới EOF và kiểm byte
  length/SHA-256 theo `bundle-manifest.json`.
- Full derived report: `E:\RideBoundData\wp13\h6-evidence-inventory-v1.json`,
  73.102 bytes, SHA-256
  `6d36bc6e781f9fa5c32a024c3f5350271b806f43a7418f148ef5138fa1fff63e`.
- Tool identity: analyzer
  `0563ef2495550345c587ddbe07cf47a0ff88c38bf2b618dae6723c881ab1ab3b`;
  reused solver-evidence verifier
  `3eebec96b8370db2c4879adeaede3e67b7344571299a496953afcbc599dd93e5`.
- Compact source-controlled receipt:
  [`wp13-h6-evidence-inventory-v1-summary.json`](evidence/wp13-h6-evidence-inventory-v1-summary.json).

Analyzer không ghi vào hai H6 root, không gọi Runner/simulator/solver và không
reconstruct candidate.

## 2. Alignment result

| Panel | Bundles | Primary pairs | Decisions | First operational divergence | State hash khác trước divergence | Wire-only reorder trước divergence |
|---|---:|---:|---:|---:|---:|---:|
| A, 8 xe | 60 | 20 | 40.867 | 20/20 trên equal observed input | 20/20 | 15/20 |
| B, 4 xe | 40 | 20 | 16.939 | 20/20 trên equal observed input | 20/20 | 15/20 |

Số equal observed/operational-decision epoch trước divergence có min/median/max
`3/16/85` ở Panel A và `3/17/102` ở Panel B. Đây là mô tả transcript, không phải
time-to-event population statistic.

Manual inspection đã bắt và sửa hai false diagnostics trước khi khóa report:

1. `stateBeforeHash` khác từ epoch 1 ở 40/40 pair dù input/action vẫn giống, vì hash
   bind policy/manifest identity;
2. sample Panel A có cùng semantic promise set nhưng thứ tự khác ở epoch 11 vì
   `MapPublications` sort theo generated `publicationId`. Wire difference được giữ để
   audit nhưng first operational divergence đúng chuyển tới epoch 18, nơi assignment
   của request mới khác vehicle/route trên cùng observed batch.

## 3. Evidence sufficiency

| Evidence | Panel A | Panel B | Verdict |
|---|---:|---:|---|
| Execution evidence v1.0.0 decisions | 40.867 | 16.939 | recorded |
| Vehicle-loss records | 326.936 | 67.756 | recorded |
| Retained candidate count tổng | 341.951 | 71.271 | counts recorded |
| Pruned candidate witnesses | 326.686 | 130.008 | recorded |
| Physical prune witnesses | 325.314 | 129.425 | recorded |
| Commitment witnesses | 1.372 | 583 | recorded |
| Selected route actions | 15.302 | 5.608 | selected routes recorded |
| Full retained-candidate route/schedule portfolio | 0 | 0 | `notRecorded` |
| Route/schedule trong pruned witness | 0 | 0 | `notRecorded` |

H6 đủ để khóa first operational divergence, link selected candidate ID với exact prune
witness khi ID xuất hiện, và báo count-based option-set proxies. H6 không đủ để rerank
toàn retained portfolio, tính slack/compatibility mới cho mọi candidate hoặc mô phỏng
một objective khác. Các phân tích đó cần Runner evidence vNext/rerun exploratory nếu
`RB-WP13-007` xác nhận vẫn cần.

## 4. Claim boundary

- `equalObservedInput` không đồng nghĩa full internal state bằng nhau.
- First operational divergence không tự chứng minh candidate nào gây terminal service
  delta; downstream result chỉ là `trajectoryAssociated`.
- Wire action order vẫn là protocol evidence; analyzer chỉ neutralize publication slots
  cho cross-policy operational comparison, không sửa transcript/hash.
- Không CI, population inference, SLA, satisfaction claim hoặc H6 rescue.

## 5. Reproduction

```powershell
& '<bundled-python>' simulators/fleetpy-ridebound/wp13_h6_evidence_inventory.py `
  --panel 'A=E:\RideBoundData\wp9\confirmatory-h6-panela' `
  --panel 'B=E:\RideBoundData\wp9\confirmatory-h6-panelb' `
  --output 'E:\RideBoundData\wp13\h6-evidence-inventory-v1.json'
```

## 6. Verification và closure

- Targeted WP13: 16/16 pass.
- Full pinned CPython 3.10/FleetPy suite: 111/111 pass, zero skip.
- Final full raw scan: 100/100 bundles, 57.806 decisions, exit 0; independent compact
  binding verifier pass.
- Python AST 2/2; canonical JSON pass; 130 Markdown files/279 internal links,
  0 broken, 0 unbalanced fence; `git diff --check` pass.
- `dotnet build RideBound.slnx -c Release -warnaserror --no-restore`: 0 warning/error.
- `dotnet format whitespace ... --verify-no-changes`: pass.
- NuGet direct/transitive vulnerability audit: 0 known vulnerability.
- Required Debug ban đầu 854/855; chỉ
  `Exact_runner_mechanically_drains_all_medium_public_requests` fail closed tại
  `resource.cpu-time-exceeded`. Targeted diagnostic đo 120.062 ms so với ceiling
  120.000 ms, wall 121.726 ms, peak 2 process; diagnostic assertion đã revert exact.
- CPU sample trace ngoài repo (`7.298.446` byte, SHA-256
  `c336dac72440e1956ac4cc43ca2800c2eecb9059dfab927fb13f8d7871a8e469`) chỉ ra
  repeated canonical-number marker array initialization trên hot path. Bốn production
  call site được đổi sang `ReadOnlySpan<char>.IndexOfAny(char,char,char)`, không đổi
  marker `.`, `e`, `E`, rule `-0`, protocol bytes, solver order hoặc H6 artifact.
- Boundary test được bổ sung cho uppercase `E`; targeted canonical tests 16/16 và
  exact medium public-drain 1/1 pass trong 1 phút 33 giây, vẫn giữ ceiling 120.000 ms,
  128 accepted request và 801 protocol event.
- Required `dotnet test RideBound.slnx`: 856/856 pass, zero skip.

`RB-WP13-002 Done`; `RB-WP13-003 Ready`. Failure lịch sử không bị xóa hoặc
reclassify, và không dùng Release build để thay thế required Debug gate.
