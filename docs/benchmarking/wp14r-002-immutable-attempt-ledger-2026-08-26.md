# RB-WP14R-002 — immutable attempt ledger

> Kết quả: `DONE`
> Phạm vi: mechanics-only; không launch simulation, không đọc outcome
> Successor: không supersede WP14 freeze/result v1

## 1. Kết quả

Đã thêm strict attempt ledger v1 cho successor WP14R. Mỗi job có tối đa hai thư
mục `attempt-01`/`attempt-02`; start và terminal receipt là canonical UTF-8 JSON,
exclusive-create và cross-bind bằng SHA-256. Attempt 2 chỉ được mở sau attempt 1
terminal mechanical failure; valid bundle chặn retry; failure thứ hai exhausted.

Artifacts:

- `benchmarks/schemas/wp14r/v1/attempt-record.schema.json`;
- `benchmarks/schemas/wp14r/v1/ledger-inspection.schema.json`;
- `simulators/fleetpy-ridebound/wp14r_attempt_ledger.py`;
- `simulators/fleetpy-ridebound/tests/test_wp14r_attempt_ledger.py`.

Không file nào trong 46-file freeze v1 bị sửa bởi ticket này.

## 2. Contract đã khóa

```text
<ledger>/<jobId>/attempt-01/
  attempt-start.json
  process.log                 # optional while ticket 003 is not implemented
  output/                     # partial or valid bundle
  attempt-terminal.json

<ledger>/<jobId>/attempt-02/  # only after recoveryAuthorized
  ...
```

Start receipt bind ledger version, job/attempt ID, exact freeze receipt, job binding,
command binding, timestamp, previous attempt và fixed recovery/outcome-access policy.
Terminal receipt bind start hash, exit classification/code, elapsed time, process-tree
state, exact log/output inventory, bundle-verifier evidence và derived disposition.

Policy cố định:

- `maximumAttempts = 2`;
- `attemptsAreExperimentalUnits = false`;
- `mayReadScientificOutcomeToAuthorizeRecovery = false`;
- `retryAfterValidBundle = false`;
- pass cần `bundle-manifest.json`, verifier ID/hash, behavioral hash, process exit 0
  và process tree `exitedCleanly`;
- orphaned start không được bịa process exit code;
- `notRun` không được bịa verifier/behavioral evidence;
- verifier `fail` phải có verifier identity/hash.

Inspection đọc lại canonical bytes/schema/cross-binding, recompute inventory và reject
gap, attempt thừa, entry lạ, recovery đổi job binding, timestamp đảo, receipt/log/output
tamper, link/junction, path escape và overlap raw root. Báo cáo tự gắn bốn caveat:
`mechanicalOnly`, `attemptsNotExperimentalUnits`,
`noScientificOutcomeAuthorization`, `doesNotSupersedeWp14V1`.

## 3. Vòng review source ngoài test count

Review thủ công phát hiện và sửa trước closure:

1. `os.write` có thể partial — chuyển sang loop cho tới khi ghi đủ và `fsync`.
2. `stat` rồi hash có race — inventory đọc byte count/hash cùng pass, so
   size/mtime trước-sau và fail nếu file đổi trong lúc đọc.
3. Attempt 2 ban đầu có thể ghi binding mới rồi chỉ bị inspector phát hiện — nay
   reject trước publication và không làm độc ledger.
4. Một attempt bị rename khỏi pattern có thể bị bỏ qua — nay mọi entry ngoài exact
   contract bị reject.
5. Pass ban đầu chưa buộc process exit/tree nhất quán — nay yêu cầu exit 0/clean;
   failure/orphan/verifier branches có semantic guards riêng.
6. Receipt symlink/junction và timestamp terminal trước start nay fail closed.

Known boundary: ticket này không làm supervisor, heartbeat hoặc process-tree kill;
đó là `RB-WP14R-003`. Một crash đúng giữa khi tạo attempt directory và ghi start sẽ
để lại ledger invalid, không bị âm thầm coi là “chưa có attempt”; fault/recovery policy
cho boundary này thuộc `RB-WP14R-004` và không được cleanup bằng delete.

## 4. Verification

```text
Targeted pinned Python:
  13/13 passed, 0 failed, 0 skipped

Full pinned Python/FleetPy:
  RIDEBOUND_FLEETPY_ROOT=E:\RideBoundData\wp7\FleetPy-1.0.2
  255/255 passed, 0 failed, 0 skipped

Required .NET:
  dotnet build-server shutdown
  dotnet test RideBound.slnx
  908/908 passed, 0 failed, 0 skipped
  Benchmarking.Tests 148/148 in 1 m 55 s, dưới hard CPU ceiling 120 s
```

Hai lần Python discovery không có `RIDEBOUND_FLEETPY_ROOT` trước đó pass các test
không opt-in nhưng skip 13 FleetPy contract tests; chúng **không** được tính baseline.
Baseline chính thức ở trên đã đặt đúng root và có zero skip.

Static gates và counts cuối được ghi trong status log sau khi chạy toàn bộ JSON/schema,
format, Markdown/link và diff checks.

```text
dotnet format RideBound.slnx --verify-no-changes --no-restore: PASS
Repository JSON parse: 1.339/1.339
Draft 2020-12 schema check: 83/83
External full-PDF inventory hash/length: 2/2
Historical WP14-v1 freeze verify: PASS, 160 jobs / 46 files,
  SHA-256 1ce26ff0f7d87c30d050e57107ad3e118af7f4b88fe04e62e48376ab34c37a55
Markdown: 280 files; 366 local links; 0 broken; 0 unbalanced fences
Repository DOCX inventory: 0 files
New Python line length <= 88: PASS
git diff --check: PASS (one pre-existing LF→CRLF notice on dirty wp13_full_audit.py)
```

## 5. Claim boundary và next

Ledger chứng minh state transition/integrity trong phạm vi filesystem mechanics; nó
không chứng minh process supervision, host durability, effectiveness, SLA hay outcome.
`RB-WP14R-003` là ticket Ready duy nhất để thêm supervised streaming evidence trên
contract này. Scientific execution `008..012` vẫn unauthorized.
