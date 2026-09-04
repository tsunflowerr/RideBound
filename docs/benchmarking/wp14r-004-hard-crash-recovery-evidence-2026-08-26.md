# RB-WP14R-004 — hard-crash và stale-open recovery evidence

> Ngày: 2026-08-26 (Asia/Bangkok)  
> Verdict: **DONE — MECHANICS ONLY**  
> Scientific workload/FleetPy outcome: **không chạy/không đọc**

## 1. Kết quả

`RB-WP14R-004` đóng fault-injection boundary còn mở ở `003`. Một outer supervisor
được hard-kill thật tại các publication point đã predeclare; journal/ledger giữ partial
evidence, Windows Job Object đóng process tree sau owner death, stale-open recovery chỉ
mở đúng một attempt mới khi tree safety có durable proof.

Hai outcome fail-closed quan trọng:

- incomplete first journal không bind được supervisor PID nên **exhaust**, dù fixture
  biết child chưa launch;
- durable `launchIntent` nhưng chưa có durable typed create failure hoặc
  `childStarted` là **launch ambiguous** và cũng exhaust. Không dùng thông tin ngoài
  ledger để biến nó thành retry.

Ticket này không rescue WP14-v1, không tạo replicate, không tăng denominator và không
authorize `RB-WP14R-008..012`, WP15 hay H7.

## 2. Artifact

| Artifact | Vai trò |
|---|---|
| `benchmarks/schemas/wp14r/v1/recovery-receipt.schema.json` | strict canonical receipt cho stale-open decision/tree probe |
| `simulators/fleetpy-ridebound/wp14r_stale_open_recovery.py` | verify journal, probe/cleanup tree, exclusive receipt và ledger terminalization |
| `simulators/fleetpy-ridebound/wp14r_fault_injection.py` | child/recovery worker + fsynced barrier + parent hard kill |
| `simulators/fleetpy-ridebound/tests/test_wp14r_fault_recovery.py` | actual Windows fault matrix, mutation/state-machine và POSIX contract tests |
| `wp14r_supervised_process.py` | thêm durable `launchIntent`, supervisor PID và internal-only fault hook |
| `wp14r_attempt_ledger.py` | recovery receipt binding; uncertain tree luôn exhaust |

Fault hook không được expose qua production supervisor CLI. Harness config/barrier nằm
ngoài ledger/raw roots, được exclusive-create và control root overlap bị reject.

## 3. Durable state refinement

Publication order hiện hành:

```text
attempt-start
  -> process.log exclusive-create
  -> supervisorStart + fsync (bind supervisor PID/source/command)
  -> launchIntent + fsync
  -> Popen
  -> OS containment attach
  -> childStarted + fsync (bind child PID + containment)
  -> chunks/EOF/heartbeat
  -> childExit
  -> supervisorTerminal
  -> independent bundle verification
  -> attempt-terminal
```

Recovery cần expected supervisor PID từ launcher. Nếu có complete `supervisorStart`, PID
phải exact-equal journal; owner phải absent trước và ngay trước terminal publication.
Process log, supervisor source và recovery source đều được đo lại; receipt bind exact
start/command/log/tool hashes. Crash sau receipt nhưng trước terminal được resume bằng
đúng receipt bytes cũ, không overwrite.

`launcherRecoveredOrphanedStart` chỉ hợp lệ khi receipt nói `treeSafe=true` và tree
khác `uncertain`. `processTreeUncertain` buộc `attemptsExhausted`, kể cả attempt 1. Exit
zero complete journal vẫn Open chờ independent bundle verifier; recovery không hạ nó
thành mechanical failure để chạy lại.

## 4. Fault matrix quan sát trên Windows

| Kill point | Durable prefix | Recovery verdict |
|---|---|---|
| `beforeSupervisorStart` | no complete record/PID binding | tree uncertain; exhausted |
| `afterSupervisorStart` | start, no intent | proven prelaunch; one recovery |
| `afterLaunchIntent` | intent, no child identity | launch ambiguous; exhausted |
| `afterProcessCreatedBeforeContainment` | intent only | launch ambiguous; exhausted |
| `afterContainmentBeforeChildStarted` | intent only | durable evidence vẫn ambiguous; exhausted |
| `afterChildStarted` | containment + child PID | Job close kills child; one recovery |
| `afterStreamChunk` | verified raw-byte prefix | Job close kills tree; one recovery |
| `afterProcessExitBeforeStreamEof` | parent exit, pipe held by grandchild | Job close kills grandchild; one recovery |
| `afterStreamsEofBeforeChildExit` | both EOF, no childExit | child absent/tree safe; one recovery |
| `afterChildExit` | exact exit/tree record | tree safe; one recovery |
| `afterSupervisorTerminal` nonzero | valid complete mechanics log | one recovery |
| `afterSupervisorTerminal` zero | valid complete exit-zero log | no retry; await bundle verifier |
| `afterRecoveryReceiptBeforeTerminal` | immutable receipt, no terminal | rerun reuses exact receipt then terminalizes |

`afterContainmentBeforeChildStarted` cố ý vẫn exhaust: kernel đã cleanup trong test nhưng
journal chưa bind child/containment, nên recovery không dùng knowledge từ harness barrier.
Đây là distinction giữa *điều test biết* và *điều ledger được phép claim*.

## 5. Adversarial/source review

Vòng đọc tay đã tìm và sửa:

1. uncertainty trước đây vẫn sinh `recoveryAuthorized`; nay tree/classification
   uncertainty luôn exhaust;
2. không có durable marker phân biệt prelaunch với Popen window; thêm `launchIntent`
   `fsync` trước process creation;
3. fault-hook exception ngay sau Popen ban đầu có thể bị ghi `createProcess`; stage nay
   chuyển sang containment ngay khi Popen trả về;
4. orphan classification trước đây có thể gắn tree `uncertain`; nay cần recovery receipt
   và proven-safe tree;
5. preterminal receipt có thể bị preseed/resealed nếu chỉ kiểm binding; recovery nay tái
   tính toàn bộ journal/launch/tree/disposition state trước khi reuse;
6. recovery receipt trước đây chưa cross-bind terminal process-log inventory/treeSafe;
   ledger inspector nay kiểm cả hai;
7. hard-kill control root có thể bị đặt trong ledger/raw root; ancestry/overlap gate nay
   reject trước khi tạo file;
8. live supervisor, changed log/source, receipt mutation, success retry và attempt 3 đều
   fail closed.

Actual Windows test chứng minh child + grandchild chết dưới anonymous Job Object với
`KILL_ON_JOB_CLOSE`. POSIX recovery target được unit-test là exact recorded process group
và refuse current group; chưa có native POSIX host run ở checkpoint Windows này.

## 6. Verification

```text
Targeted ledger + supervisor + fault/recovery: 39/39 passed
  ledger: 13
  supervisor: 16
  fault/recovery: 10 test methods, 13 hard-crash matrix cases
Full pinned Python/FleetPy: 281/281 passed, 0 failed, 0 skipped
Required dotnet test RideBound.slnx: 908/908 passed, 0 failed, 0 skipped
Benchmarking.Tests: 148/148 in 1 m 59 s; hard CPU ceiling remains 120 s
dotnet format --verify-no-changes --no-restore: PASS
Historical WP14-v1 freeze reverify: PASS, 160 jobs / 46 files / exact SHA
JSON parse: 1,342/1,342; Draft 2020-12 schemas: 86/86
Markdown: 283 files, 371 local links, 0 broken, 0 unbalanced fences
New/changed WP14R Python lines over 88 characters: 0
git diff --check: PASS (unrelated tracked LF→CRLF notice only)
```

## 7. Claim và resource boundary còn lại

- Fault cases dùng fake Python child, không FleetPy/Runner scientific matrix.
- PID reuse chỉ gây conservative refusal: một unrelated live reused PID không bị kill.
- POSIX có contract test, chưa có native runtime evidence ở host Windows.
- Verifier hiện materialize journal khi semantic-check; peak memory/log/heartbeat cost
  và repetition ở host/process/job/verifier levels chưa được dimension. Đây chính là
  phạm vi `RB-WP14R-005`, không được coi `004` là resource gate.
- Independent implementation/mutation verifier vẫn thuộc `006`; `004` không tự gọi
  same-process recovery validation là independence.

## 8. Next

Chỉ `RB-WP14R-005` chuyển Ready: dimension mechanics-only resource/variance theo đúng
variation levels từ full-PDF review, không đọc service/burden/routes và không copy số
repetition từ paper. `006/007` vẫn Not ready; `008..012` vẫn Unauthorized.
