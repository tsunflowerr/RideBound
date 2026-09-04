# RB-WP14R-005 — mechanics resource/variance dimension evidence

> Status: **DONE — MECHANICS ONLY**
> Scientific execution: **none**
> Native host scope: **one Windows host session per retained matrix**

## 1. Boundary và design

Ticket này chỉ đo launcher/supervisor/journal/verifier/recovery trên fake-child corpus.
Không FleetPy, Runner scientific configuration, service/burden/route field hay `output/`
bundle nào được đọc. Hierarchy là host session → launcher process → supervised child/job
→ verifier process. Một loop, solver seed hay recovery attempt không được tính là replicate.

Corpus có tám cell cố định: silent exit, binary streams, exact-cap, heartbeat idle,
nonzero exit, lingering grandchild, safe prelaunch partial recovery và 8 MiB large
journal. Mỗi matrix giữ một pilot/cell nhưng loại pilot khỏi summaries, sau đó giữ đúng
năm launcher-process repetitions/cell. Summaries chỉ là lower median/min/max trên từng
resource axis; không CI, outlier deletion, scalar score hay population/SLA claim.

Design áp dụng bài học full-PDF của Kalibera–Jones và Mytkowicz et al. theo hai cách:
phân biệt variation level/process mới thay vì nhân loop thành N; và bind exact host/tool/
runtime/corpus/policy để không nhầm environmental bias với thuật toán. Số năm repetition
là lựa chọn local mechanics-budget để thấy range/median, không copy từ paper.

## 2. Artifact chain bất biến

| Stage | Root/report | SHA-256 | Kết quả |
|---|---|---|---|
| v1 resource pilot | `E:\RideBoundData\wp14r\mechanics-dimension-v1-20260827\mechanics-dimension-report.json` | `a2795a84a29951a98bb91efecf94a9404ad23480a56e42259d7c4aefc986817f` | 42 pass, 6 retained large-journal timeout; requires refinement |
| v2 before optimization | `E:\RideBoundData\wp14r\mechanics-dimension-v2-20260827\mechanics-dimension-report.json` | `96e274a2682c18695b290269380aabe206f4296966f43d1eb4b8cf990dec2ce5` | 48/48 pass; memory envelope pass; high CPU/wall |
| v3 after optimization | `E:\RideBoundData\wp14r\mechanics-dimension-v3-20260827\mechanics-dimension-report.json` | `44dce55e89c9602daeedc601471e5d2873ab959f86c8bb2394460291baf78bce` | 48/48 pass; memory envelope pass |

V1 cho thấy timeout 10 giây cắt large cell sau khoảng 2.5–2.6 MiB retained. Vì pilot
chỉ đọc resource telemetry, corpus v2 tăng riêng large-cell timeout lên 180 giây; giữ
nguyên payload 8 MiB, chunk cap, `1 + 5` repetitions và envelope 256 MiB. V1 không bị
overwrite hay loại khỏi lịch sử.

V3 report dùng artifact schema `1.1.0`, corpus SHA
`54b53427f2e107febcfc9f203cdd21479721c16dc14b20c012c0bbec2837baf2`, policy SHA
`3df877e4151052b7cc6f01c74b602d1a08e04a9cb16b5c75946e72ec26c98f36`, report-input
SHA `df76c96e545590cb401f828cd3f3b70ad3eb9200885611009e11fbc963745445` và WP14R
schema-tree SHA `634e29e144a455413cfd656211d7e92284aad516d14e92541ce4f84b2d693bfe`.

## 3. V3 measured result

Các số dưới đây là năm measured process/cell, không gồm pilot. Wall là median; RSS,
journal bytes và record/fsync là maximum.

| Cell | launcher wall ms | verifier wall ms | launcher RSS MiB | verifier RSS MiB | journal bytes | records/fsync |
|---|---:|---:|---:|---:|---:|---:|
| silentExit | 962.34 | 451.74 | 30.0 | 28.5 | 6,701 | 9 |
| binaryStreams | 1,047.81 | 492.46 | 30.0 | 28.5 | 23,008 | 22 |
| exactCapBoundary | 1,080.58 | 520.87 | 29.6 | 28.9 | 104,783 | 26 |
| heartbeatIdle | 1,282.24 | 513.20 | 30.0 | 28.5 | 9,813 | 15 |
| nonzeroExit | 961.21 | 442.45 | 29.2 | 29.4 | 6,677 | 9 |
| lingeringGrandchild | 1,092.12 | 442.20 | 30.0 | 29.5 | 7,204 | 10 |
| partialRecovery | 783.10 | 406.00 | 30.0 | 28.4 | 1,837 | 1 |
| largeJournal | 4,682.89 | 1,956.60 | 63.8 | 62.8 | 11,365,201 | 284 |

Large-journal measured range:

- launcher wall `4,506.460..5,196.667 ms`, median `4,682.889 ms`;
- launcher CPU median `4,000.000 ms`, peak RSS maximum `66,932,736` bytes;
- verifier wall `1,924.858..2,100.986 ms`, median `1,956.597 ms`;
- verifier CPU median `1,906.250 ms`, peak RSS maximum `65,875,968` bytes;
- journal `11,364,612..11,365,201` bytes, `283..284` records/fsync;
- cả năm lượt observed/retained đúng `8,388,608` bytes, clean exit và vẫn
  `childExitedZeroAwaitingBundleVerification`.

Verifier peak bằng 24.5% envelope `268,435,456` bytes nên fixed 8 MiB cell pass.
Kết luận này không ngoại suy thành bảo đảm cho hai stream cùng chạm hard cap 16 MiB,
không phải SLA và không ước lượng between-host variance.

## 4. Optimization từ evidence, không từ outcome

Source review sau v2 tìm thấy `load_schema` + `check_schema` + validator construction bị
lặp trên từng journal record. Supervisor/verifier nay cache compiled validator trong
một process. Journal mới bind SHA của log/report schema; verifier kiểm provenance trước
và sau semantic validation. Journal legacy không có provenance vẫn đi qua nhánh legacy
rõ ràng; partial provenance hoặc resealed wrong SHA fail closed. Dimension report v3
bind thêm toàn WP14R schema-tree SHA.

Trên cùng corpus/policy, observed large-cell median đổi:

| Axis | v2 trước cache | v3 sau cache | observed ratio |
|---|---:|---:|---:|
| launcher wall | 71,297.27 ms | 4,682.89 ms | 15.23× |
| verifier wall | 35,393.73 ms | 1,956.60 ms | 18.09× |
| verifier peak RSS max | 64,872,448 B | 65,875,968 B | 0.98× |
| journal records median | 402 | 284 | — |

Đây là descriptive before/after trên hai host sessions tuần tự, không randomized causal
experiment. Record count thay đổi do OS pipe chunk timing, nên không quy toàn bộ ratio
cho cache. Kết quả đủ chứng minh implementation mới giữ semantics/tests và giảm observed
wall/CPU trên corpus; nó không chứng minh population performance.

## 5. Verification

```text
Targeted WP14R ledger/supervisor/fault/recovery/dimension: 50/50 passed
Full pinned Python/FleetPy: 292/292 passed, 0 failed, 0 skipped
Required dotnet test RideBound.slnx: 908/908 passed, 0 failed, 0 skipped
Benchmarking.Tests: 148/148 in 1 m 54 s; hard CPU ceiling remains 120 s
dotnet format --verify-no-changes --no-restore: PASS
Historical WP14-v1 freeze reverify: PASS, 160 jobs / 46 files / exact SHA
JSON parse: 1,343/1,343; Draft 2020-12 schemas: 87/87
Markdown: 284 files, 373 local links, 0 broken, 0 unbalanced fences
New/changed WP14R Python lines over 88 characters: 0
git diff --check: PASS (unrelated tracked LF→CRLF notice only)
```

Report v3 được kiểm độc lập: canonical bytes, Draft 2020-12 schema, report-input hash,
schema-tree hash, 48 unique sample IDs, đúng 1 pilot + 5 measured/cell và 48/48 pass.

## 6. Claim boundary và next gate

`RB-WP14R-005` chỉ đóng mechanics resource dimensioning. Nó không verify scientific
bundle, không rescue WP14-v1, không authorize paired resource gate và không biến attempt
thành experimental unit. Ticket kế tiếp duy nhất có thể Ready là `RB-WP14R-006`:
independent ledger verifier + mutation matrix. `007` vẫn Not ready; `008..012` vẫn
Unauthorized.
