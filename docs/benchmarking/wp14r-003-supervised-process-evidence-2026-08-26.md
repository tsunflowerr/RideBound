# RB-WP14R-003 — supervised incremental process evidence

> Kết quả: `DONE`
> Phạm vi: fake-child/process mechanics; không FleetPy matrix, không scientific outcome
> Authority: ledger `RB-WP14R-002` vẫn quyết định attempt terminal/success

## 1. Kết quả

Đã thêm supervisor mới `wp14r_supervised_process.py`; không sửa runner WP14-v1.
Supervisor chỉ chạy trên current open attempt, recompute exact command binding trước
launch và exclusive-create `process.log`. Journal là canonical LF NDJSON, gapless
sequence và SHA-256 chain; từng record được `fsync` trước khi tiếp tục.

Artifacts:

- `benchmarks/schemas/wp14r/v1/supervision-log-record.schema.json`;
- `benchmarks/schemas/wp14r/v1/supervision-report.schema.json`;
- `simulators/fleetpy-ridebound/wp14r_supervised_process.py`;
- `simulators/fleetpy-ridebound/tests/test_wp14r_supervised_process.py`.

WP14-v1 freeze receipt reverify vẫn PASS exact 160 jobs/46 files/SHA
`1ce26ff0f7d87c30d050e57107ad3e118af7f4b88fe04e62e48376ab34c37a55`.

## 2. Command/privacy boundary

Command binding hash bao phủ:

- resolved executable path và stable executable SHA-256;
- raw argument array trong hash;
- resolved working directory;
- sorted allowlist của inherited environment và exact values trong hash.

Journal chỉ công bố executable identity, argument count/hash, environment names và
environment-binding hash; không ghi raw arguments hoặc environment values. Duplicate/
invalid/missing environment names/values và command mutation fail trước process log/
launch. Caller phải allowlist đủ runtime environment; supervisor không dump hoặc tự
inherit toàn bộ host environment.

## 3. Streaming và bounded state

- supervisor-start record được `fsync` trước `Popen`;
- stdout/stderr đọc bằng hai bounded producer threads và queue size 8, nên child nhận
  backpressure thay vì làm RAM tăng vô hạn;
- raw bytes, kể cả NUL/non-UTF8, được base64-preserve theo chunk; mỗi chunk bind stream,
  sequence, byte count, SHA và cumulative count;
- per-stream cap giữ verified prefix, kill contained tree và terminal typed
  `stdoutLimit`/`stderrLimit`;
- heartbeat/wall timeout dùng monotonic clock; UTC chỉ provenance;
- reader/launch/containment/cancellation/nonzero/tree-leak states tách riêng;
- verifier chấp nhận truncated last line chỉ khi verified prefix chưa terminal; bytes
  thêm sau terminal là tamper, không phải partial hợp lệ.

Journal verifier validate toàn bộ schema/canonical bytes/chain/time/chunk/base64/EOF/
stream totals và terminal state. Report luôn gắn:

```text
mechanicalOnly
doesNotVerifyBundleOrOutcome
exitZeroAwaitsIndependentBundleVerification
doesNotSupersedeWp14V1
```

Exit 0 chỉ thành `childExitedZeroAwaitingBundleVerification`; ledger attempt vẫn Open.
Chỉ independent bundle verifier và `terminalize_attempt(... pass ...)` mới có thể tạo
terminal success. Integration test synthetic chứng minh supervisor journal compose với
ledger terminal receipt mà không bypass manifest/verifier/behavioral-hash gates.

## 4. Process-tree boundary

Trên Windows, child được gắn vào Job Object có
`JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`; outer supervisor mất handle thì OS đóng job và
kill members. Trên POSIX dùng new session/process group với TERM→KILL bounded cleanup.
Containment attachment failure sau process creation không bị ghi sai thành “process
never started”: nó kill direct child rồi giữ `treeUncertain`, vì grandchild race không
thể bị phủ nhận.

Fake child/grandchild thật xác nhận lingering grandchild bị phân loại
`treeLeakTerminated`, không được nâng thành clean exit. Ticket `004` vẫn phải kill outer
supervisor/child ở nhiều boundary và kiểm recovery sau crash; `003` không claim fault
matrix hoặc stale-open recovery đã đóng.

## 5. Source review ngoài test count

Các defect/gap phát hiện và sửa trong review:

1. JSON Schema `unevaluatedProperties` đặt sai layer làm mọi event fail — base object
   nay strict `additionalProperties:false`, event payload vẫn typed riêng.
2. Windows 64-bit job handle thiếu ctypes `restype/argtypes` có thể bị truncate — đã
   pin signatures và structures thực.
3. Containment attach fail ban đầu bị ghi như create-process fail — nay giữ stage và
   `treeUncertain` trung thực.
4. Containment/launch branch để hở stdout/stderr handles — đã đóng explicit; targeted
   suite pass với `ResourceWarning` nâng thành error.
5. Verifier ban đầu chỉ kiểm SHA chain, chưa kiểm resealed semantic mutation — nay kiểm
   state order, chunk/EOF observed hash, normal EOF, limit/failure/tree/terminal mapping.
6. Windows environment names cần case-insensitive duplicate gate; args/env NUL hoặc
   non-string bị reject trước launch.

## 6. Verification

```text
Targeted supervisor: 15/15 passed with ResourceWarning treated as error
Targeted ledger + supervisor: 28/28 passed
Full pinned Python/FleetPy: 270/270 passed, 0 failed, 0 skipped
Required dotnet test RideBound.slnx: 908/908 passed, 0 failed, 0 skipped
Benchmarking.Tests: 148/148 in 1 m 46 s; CPU ceiling remains 120 s
dotnet format --verify-no-changes --no-restore: PASS
Repository JSON parse: 1,341/1,341
Draft 2020-12 schemas: 85/85
Markdown: 282 files, 369 local links, 0 broken, 0 unbalanced fences
New WP14R Python lines > 88: 0
git diff --check: PASS; unrelated wp13_full_audit.py LF→CRLF notice retained
Historical WP14-v1 freeze verify: PASS, 160 jobs / 46 files / exact SHA
```

## 7. Next

`RB-WP14R-004` là ticket Ready duy nhất: fault-inject outer supervisor/child/writer ở
các predeclared boundaries, prove Job Object/process-group cleanup, verify partial
journal prefixes, terminalize stale opens và authorize đúng tối đa một recovery.
Không scientific job nào được launch ở `003`; `008..012` vẫn unauthorized.
