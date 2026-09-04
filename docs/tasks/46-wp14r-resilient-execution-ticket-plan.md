# WP14R — resilient execution: ordered queue

> Work package: `WP14R ACTIVE — FREEZE V2 AUTHORIZED`
> Refinement: `RB-WP14R-001 DONE`
> Active implementation ticket: `NONE`; `RB-WP14R-008 READY — AC PREFLIGHT BLOCKED`
> Quy tắc: chỉ một ticket implementation active

## 1. Queue

| ID | Kết quả review được | Trạng thái | Dependency |
|---|---|---|---|
| RB-WP14R-001 | Full-PDF/source audit, ADR-071, recovery boundary, ordered queue | Done | WP14-009 |
| RB-WP14R-002 | Strict immutable attempt ledger + schema + state-machine tests | Done | 001 |
| RB-WP14R-003 | Supervisor, incremental log/heartbeat và host/process/job telemetry | Done | 002 |
| RB-WP14R-004 | Fault injection, process-tree cleanup và stale-open recovery evidence | Done | 003 |
| RB-WP14R-005 | Mechanics-only resource/variance dimensioning, không đọc outcome | Done | 004 |
| RB-WP14R-006 | Independent ledger verifier và mutation matrix | Done | 005 |
| RB-WP14R-007 | Protocol/freeze-v2 authorization decision | Done | 006 |
| RB-WP14R-008 | Paired B1/C1 resource gate dưới freeze v2 | Ready — host precondition blocked | 007 |
| RB-WP14R-009 | Development matrix dưới freeze v2 | Unauthorized | 008 |
| RB-WP14R-010 | Independent matrix/bundle verifier | Unauthorized | 009 |
| RB-WP14R-011 | Development-only two-axis frontier report | Unauthorized | 010 |
| RB-WP14R-012 | Full source/logic/claim audit và closure decision | Unauthorized | 011 |

Các ticket xa chỉ có một dòng cho tới khi queue head đóng. `Unauthorized` mạnh hơn
`Not ready`: không được chạy chỉ vì implementation mechanics đã tồn tại.

## 2. RB-WP14R-002 — Immutable attempt ledger

### Purpose

Khóa danh tính và trạng thái từng attempt trước khi có supervisor mới. Đây là state
machine cơ học, không launch simulator và không đọc scientific outcome.

### In scope

- strict Draft 2020-12 schemas cho attempt record và inspection report;
- canonical UTF-8 receipts, exclusive-create, SHA-256 cross-binding;
- exact `attempt-01`/`attempt-02`, không gap/overwrite/attempt 3;
- attempt 2 chỉ sau terminal mechanical failure đã authorize recovery;
- no retry after independently verified pass;
- log/output inventory và tamper detection sau terminal;
- explicit stale open state và terminal class `launcherRecoveredOrphanedStart`;
- forbidden-root overlap, symlink/junction và path escape fail closed;
- CLI `start`, `terminalize`, `inspect` để ticket sau tái sử dụng.

### Out of scope

- child process launch, heartbeat, timeout, process-tree kill;
- xác định nguyên nhân termination của WP14-v1;
- chạy dataset/FleetPy/Runner hoặc tạo outcome;
- independent verifier thứ hai — thuộc `006`.

### Acceptance

1. Start/terminal receipts validate schema và byte-canonical.
2. Prior-open, gap, extra attempt, success retry, receipt/output/log tamper bị reject.
3. Pass bắt buộc có bundle manifest, verifier identity/hash và behavioral hash.
4. Failure thứ nhất authorize đúng một recovery; failure thứ hai exhausted.
5. Inspection chọn first valid attempt duy nhất và tự gắn claim `mechanicalOnly`.
6. Targeted tests, full pinned Python và required `dotnet test RideBound.slnx` pass;
   JSON/schema/format/link/diff gates sạch.

## 3. Ràng buộc toàn queue

- Không sửa `wp14_run_matrix.py` hoặc bất kỳ file trong freeze receipt v1.
- Không ghi/move/delete raw roots H6, E1 hay WP14-v1 partial.
- Không đọc completion/burden/route outcome để authorize recovery.
- Không suy attempt count thành sample size, CI hoặc replication.
- Mọi protocol version mới dùng namespace `wp14r` và explicit ADR.
- Một failure receipt không bị thay bằng lần chạy thuận lợi hơn.

## 3A. RB-WP14R-003 — Supervised incremental process evidence

### Purpose

Thay failure boundary `subprocess.run(capture_output=True)` bằng một tool WP14R mới
ghi được evidence trước và trong lúc child chạy. Ticket vẫn dùng fake child/mechanics;
không chạy FleetPy matrix và không verify scientific bundle.

### Contract cần chứng minh

- existing open attempt/start receipt là authority; supervisor recompute exact command
  binding và refuse mismatch trước launch;
- command binding gồm executable bytes, raw arguments trong hash, working directory và
  allowlisted inherited-environment value hashes; log không phát raw args/env values;
- `process.log` exclusive-create, canonical LF NDJSON, gapless sequence và SHA chain;
- supervisor-start được `flush`/`fsync` trước child; stdout/stderr được base64-preserve
  incremental với per-stream sequence/hash/count/cap;
- monotonic clock quyết định heartbeat/timeout; UTC chỉ provenance;
- child nằm trong explicit containment (`windowsJobObject` hoặc `posixProcessGroup`),
  tree status và cleanup result được ghi trung thực;
- stream EOF, child exit, tree status và terminal journal record chỉ publish theo state
  machine; incomplete journal vẫn verify được như partial, không thành success;
- journal verifier đọc EOF, validate schema/canonical bytes/chain/chunk/stream totals và
  emit mechanical-only report; nó không đọc `output/`;
- exit zero chỉ có nghĩa `awaitingBundleVerification`; supervisor không tự ghi ledger
  terminal pass. Ledger terminal receipt ở `002` vẫn là authority sau verifier riêng.

### Bounded failure classes

`launchFailure`, `childExitFailure`, `wallTimeout`, `stdoutLimit`, `stderrLimit`,
`readerFailure`, `treeLeakTerminated`, `treeUncertain`, `cancelled`. Log cap/overflow
phải giữ verified prefix và typed terminal; không giữ unbounded bytes trong RAM.

### Acceptance

Fake-child tests bao phủ stdout/stderr interleave, non-UTF8, large/cap, nonzero exit,
timeout, command/env mutation, exclusive log, partial log, chain/chunk mutation,
grandchild containment metadata và exit-zero-awaiting-verifier boundary. Ticket `004`
sẽ kill supervisor/child ở nhiều boundary để chứng minh crash recovery/tree cleanup;
`003` không được claim fault matrix đã đóng.

## 3B. RB-WP14R-004 — Hard-crash fault matrix và stale-open recovery

### Purpose

Chứng minh bằng process termination thật rằng durable journal/ledger không biến một
crash thành success, không để process tree sống âm thầm và chỉ authorize recovery khi
tree safety đã được chứng minh. Matrix chỉ dùng fake child; không đọc scientific output.

### Predeclared fault points

1. `beforeSupervisorStart`: journal đã exclusive-create nhưng start record chưa `fsync`;
2. `afterSupervisorStart`: durable start, trước durable launch intent;
3. `afterLaunchIntent`: có ý định launch nhưng chưa có durable child identity;
4. `afterProcessCreatedBeforeContainment`: child có thể tồn tại, chưa containment;
5. `afterChildStarted`: containment + child identity đã durable;
6. `afterStreamChunk`: ít nhất một bounded raw-byte chunk đã durable;
7. `afterProcessExitBeforeStreamEof`: parent exit đã quan sát, pipe/tree chưa đóng;
8. `afterStreamsEofBeforeChildExit`: hai EOF durable, chưa `childExit`;
9. `afterChildExit`: tree/exit durable, chưa supervisor terminal;
10. `afterSupervisorTerminal`: complete mechanics journal, ledger vẫn Open.

### Contract cần chứng minh

- `launchIntent` phải `fsync` ngay trước `Popen`; prefix có intent nhưng chưa durable
  containment là ambiguous và **không** authorize retry;
- fault hook là dependency nội bộ chỉ dùng bởi harness, không có production CLI switch;
  parent phải hard-kill outer worker sau một exclusive/fsynced barrier ACK;
- recovery nhận expected supervisor PID từ launcher, chứng minh launcher đã chết và
  kiểm lại stable process-log inventory trước publication;
- complete/partial/truncated journal được verifier hiện hành phân loại; malformed,
  noncanonical hoặc semantically resealed evidence bị reject, không được sửa/cleanup;
- durable `childStarted` bind PID + OS containment. Windows chỉ coi safe sau launcher/
  child absence dưới `KILL_ON_JOB_CLOSE`; POSIX terminate/check exact process group;
- incomplete first record không bind được launcher PID nên phải exhaust; chỉ durable
  supervisor-start chưa có launch intent hoặc typed create-process failure mới chứng minh
  prelaunch safe; launch-ambiguous/tree-uncertain tuyệt đối không mở attempt 2;
- safe stale open có immutable recovery receipt bind start/command/tool/log/tree probe;
  receipt được reuse idempotently nếu crash trước ledger terminal, không overwrite;
- `launcherRecoveredOrphanedStart` chỉ hợp lệ với recovery receipt và tree status khác
  `uncertain`; `processTreeUncertain` không bao giờ có `recoveryAuthorized`;
- complete exit-zero journal giữ Open để chờ independent bundle verifier, không bị đổi
  thành retryable launcher failure;
- attempt 1 safe failure mở đúng attempt 2; attempt 2 failure exhaust; no attempt 3,
  no retry after pass, attempts không phải experimental units.

### Acceptance

Targeted tests phải chạy toàn bộ fault points bằng subprocess thật, kiểm parent/child/
grandchild absence trên Windows hiện tại, partial-prefix classification, ambiguous-tree
exhaustion, receipt mutation, recovery crash-window reuse và exact two-attempt state
machine. Sau đó full pinned Python, required `.NET`, format/JSON/schema/Markdown/diff và
WP14-v1 read-only reverify đều pass. Khi Done chỉ `RB-WP14R-005` chuyển Ready.

## 4. Closure `RB-WP14R-002`

Done. Ledger v1 khóa exact two-attempt state machine, canonical/exclusive receipts,
start→terminal hashes, stable log/output inventory, mechanical-only recovery và strict
inspection. Review source sửa partial write, inventory race, recovery binding poison,
renamed-entry escape và inconsistent process/verifier semantics. Evidence:
[`wp14r-002-immutable-attempt-ledger-2026-08-26.md`](../benchmarking/wp14r-002-immutable-attempt-ledger-2026-08-26.md).
Targeted 13/13, pinned Python/FleetPy 255/255 zero skip và required .NET 908/908.
`RB-WP14R-003` sau đó chuyển In progress; chưa launch simulator hoặc tạo scientific
outcome.

## 5. Closure `RB-WP14R-003`

Done. Supervisor bind exact command/environment identity trước launch, journal stdout/
stderr/heartbeat/process/tree incremental bằng canonical hash-chain NDJSON, giữ bounded
raw-byte prefix và tự gắn `notRun`/mechanical-only boundary. Windows Job Object và
POSIX process group có typed cleanup; exit 0 vẫn để ledger Open chờ independent bundle
verifier. Evidence:
[`wp14r-003-supervised-process-evidence-2026-08-26.md`](../benchmarking/wp14r-003-supervised-process-evidence-2026-08-26.md).
Targeted supervisor 15/15, WP14R 28/28, full pinned Python 270/270 và required
.NET 908/908. Chỉ `RB-WP14R-004` chuyển Ready; fault matrix chưa chạy.

## 6. Closure `RB-WP14R-004`

Done. Durable `launchIntent` tách prelaunch khỏi ambiguous Popen window; hard-kill
harness đi qua 11 supervisor boundary và recovery receipt→terminal boundary. Recovery
bind launcher PID/source/start/command/log/tree evidence, reuse exact immutable receipt
sau crash và chỉ mở attempt 2 khi tree safe. Incomplete first record, pre-containment
ambiguity và mọi tree uncertainty đều exhaust; exit-zero complete journal vẫn chờ bundle
verifier. Evidence:
[`wp14r-004-hard-crash-recovery-evidence-2026-08-26.md`](../benchmarking/wp14r-004-hard-crash-recovery-evidence-2026-08-26.md).
Targeted WP14R 39/39, full pinned Python 281/281, required .NET 908/908 và historical
freeze-v1 reverify pass. Chỉ `RB-WP14R-005` chuyển Ready; scientific `008..012` vẫn
unauthorized.

## 7. RB-WP14R-005 — Mechanics-only resource/variance dimensioning

### Purpose và hierarchy

Đo chi phí cơ học của launcher/supervisor/journal/verifier/recovery trước independent
verifier ở `006`. Hierarchy được ghi tường minh là host session → launcher process →
supervised child/job → verifier process. Host hiện tại chỉ tạo một host session nên
không ước lượng between-host variance; mỗi repetition phải là process mới, không lấy
loop, solver seed hoặc recovery attempt làm replicate.

### Fixed corpus và repetition

Corpus v1 có đúng tám cell: silent exit, binary stdout/stderr, exact stream-cap boundary,
heartbeat idle, nonzero exit, lingering grandchild, safe prelaunch partial recovery và
large journal. Mỗi cell có một pilot resource-only bị loại khỏi summary, sau đó năm
launcher-process repetitions được giữ toàn bộ. Năm lần được chọn để thấy range/median
và process-to-process instability trong ngân sách local mechanics; đây không phải số
copy từ paper, không tạo CI/sample-size hay population claim. Không cell nào dùng FleetPy,
Runner scientific configuration hoặc đọc `output/`.

### Measurement/report contract

- monotonic wall, process CPU và peak RSS riêng cho supervisor/recovery và verifier;
- journal bytes/records/fsync-count, observed/retained bytes từng stream, child/process
  identity, typed terminal/tree status và cleanup latency khi journal chứng minh được;
- strict canonical report bind source hashes, Python executable/runtime, dependency,
  host fingerprint, corpus/policy hashes, forbidden roots và mọi retained sample;
- pilot và failed measurement vẫn được giữ, không drop outlier hoặc chọn best attempt;
- raw per-cell samples cùng median/min/max mô tả từng axis, không scalar score;
- verifier memory envelope predeclare 256 MiB absolute peak cho large-journal cell.
  Nếu fail phải dừng/refactor streaming và chứng minh semantic/mutation equivalence;
- dimension root mới không overlap H6/E1/WP14-v1. Fault/recovery contract `004` là input
  bất biến; safe prelaunch point được predeclare, không rerun nhiều point để chọn case.

### Acceptance

Targeted schema/corpus/monitor/recovery/report/failure-retention tests; một retained
mechanics matrix/report repeatable; source review verifier memory scaling; full pinned
Python, required `.NET`, format/JSON/schema/Markdown/diff và WP14-v1 read-only freeze
reverify pass. Chỉ sau closure mới cho `RB-WP14R-006 Ready`.

## 8. Closure `RB-WP14R-005`

Done. Ba immutable mechanics roots giữ pilot fail, before-optimization pass và
after-optimization pass. V1 giữ sáu typed large-journal timeout; v2/v3 đều 48/48 pass.
V3 fixed 8 MiB cell có verifier peak `65,875,968` bytes, pass envelope 256 MiB;
không ngoại suy tới theoretical two-stream cap. Compiled-validator cache cùng schema
provenance binding giảm observed large-cell median launcher/verifier từ
`71.30/35.39 s` xuống `4.68/1.96 s`, nhưng before/after chỉ descriptive và record count
khác theo pipe timing. Evidence:
[`wp14r-005-mechanics-resource-dimension-evidence-2026-08-27.md`](../benchmarking/wp14r-005-mechanics-resource-dimension-evidence-2026-08-27.md).
Targeted WP14R 50/50, full pinned Python 292/292, required .NET 908/908 và freeze-v1
reverify pass. Chỉ `RB-WP14R-006` chuyển Ready; `007` Not ready và scientific
`008..012` vẫn unauthorized.

## 9. RB-WP14R-006 — Independent verifier và mutation matrix

### Contract

- verifier riêng có strict schema/report/source identity và không import/call
  `inspect_ledger`, `verify_process_log`, writer canonicalizer hay recovery classifier;
- input chỉ là retained ledger root, job ID và explicit forbidden roots; verifier
  read-only, không launch/terminalize/recover/write artifact;
- recompute canonical receipts, attempt/retry state, timestamp order, journal streaming
  chain/chunk/EOF/state, recovery binding, inventory và tool/schema provenance;
- output content chỉ được hash inventory; bundle manifest chỉ kiểm presence/hash, không
  parse scientific fields;
- valid cross-implementation fixtures gồm complete-open, partial-recovery và explicit
  legacy provenance branch;
- từng mutant được copy sang isolated root, giữ lại và phải reject đúng typed code;
  mutation count không phải replicate/sample size.

### Fixed mutation classes

Path overlap, real link/junction, canonical JSON, start/terminal/recovery hash binding,
attempt gap/extra, journal chain/chunk/EOF/state/terminal append và schema/source
provenance. Mọi correction/pilot phải giữ root cũ, không rerun để chọn pass thuận lợi.

### Acceptance

Cross-implementation valid-state equivalence; toàn fixed mutation class caught đúng
code; streaming source review và race injection pass; full pinned Python, required
`.NET`, format/JSON/schema/Markdown/diff cùng WP14-v1 read-only freeze pass. Chỉ sau
closure mới cho `RB-WP14R-007 Ready`; `008..012` vẫn Unauthorized.

## 10. Closure `RB-WP14R-006`

Done. Verifier độc lập tái dựng ledger/journal/recovery semantics từ bytes/path, không
filesystem mutation và không import implementation verifier cũ. Source review phát
hiện/sửa Windows Python 3.10 junction blind spot cùng hai TOCTOU window ở streamed log
và output tree inventory. Authoritative fixture receipt SHA `1e4a6450…72104`; mutation
report SHA `9d8aacf4…72e1e`; clean/recovery/legacy valid và exact 15/15 mutation class
caught đúng code, gồm junction thật. Evidence:
[`wp14r-006-independent-verifier-evidence-2026-08-27.md`](../benchmarking/wp14r-006-independent-verifier-evidence-2026-08-27.md).
Targeted WP14R 64/64, full pinned Python/FleetPy 306/306 zero skip, required .NET
908/908 và historical freeze-v1 exact reverify pass. Chỉ `RB-WP14R-007` chuyển Ready;
paired/scientific `008..012` chưa được authorize.

## 11. Closure `RB-WP14R-007`

Done. ADR-072 authorize canonical protocol freeze v2 SHA `6b340108…a31237`, tham
chiếu byte-exact 160-job WP14-v1 design mà không sửa 46 file cũ. Audit integration sửa
command/recovery contradiction bằng fixed wrapper: cùng command hash cho attempt 1/2,
nhưng output vẫn tách theo immutable open-attempt. Receipt bind 24 source/schema/test
files, exact current provenance của mechanics `002..006`, ba authoritative gate
artifacts và ba full PDF/34 trang.

Host gate khóa exact Windows fingerprint, AC online, Balanced GUID, 10×1 s CPU,
8 GiB memory và 25 GiB disk; không log process name/command line. Exact paired order là
B1 rồi C1, sequential, hai valid/zero fail, resource-only/no outcome. Matrix order đã
hash trước và chỉ mở bằng canonical paired-gate pass.

Preflight thật giữ receipt SHA `642b23ef…cd622` và fail đúng
`POWER_SOURCE_NOT_AC`; CPU/memory/disk/scheme đều pass. Failure không tiêu thụ attempt,
không launch B1/C1 và không bị sửa threshold để lấy pass. Targeted WP14R 95/95, pinned
Python/FleetPy 337/337 zero skip, required .NET 908/908; freeze v2/v1 exact reverify,
JSON/schema/Markdown/format/diff pass. Full evidence được ghi trong
[`wp14r-007-protocol-freeze-v2-authorization-2026-08-28.md`](../benchmarking/wp14r-007-protocol-freeze-v2-authorization-2026-08-28.md).

Chỉ `RB-WP14R-008` chuyển Ready về protocol/implementation nhưng bị exact host
precondition chặn tới khi AC online. `009..012`, WP15 và H7 vẫn Unauthorized.

## 12. Pre-launch analysis 2026-08-29 — không phải ticket

Trong lúc `008` bị chặn bởi host precondition, mọi thứ kiểm được **trước** matrix đã
được kiểm. Không ADR, không authorize, zero scientific job. Đầy đủ ở
[`wp14r-prelaunch-analysis-2026-08-29.md`](../benchmarking/wp14r-prelaunch-analysis-2026-08-29.md).

Bốn kết quả ảnh hưởng trực tiếp tới `008`, `009` và `011`:

1. **Blocker đã đổi.** Preflight mới pass AC/scheme/CPU/disk và chỉ còn
   `MEMORY_BELOW_MINIMUM` (thiếu `56.111.104` B). Sleep/hibernate trên AC đều Never,
   nên không phải nghi vấn cho lần chấm dứt C1 của WP14-v1.
2. **Analyzer chạy sạch trên bundle thật, nhưng không trả lời được F2.**
   `read_bundle` pass end-to-end trên bundle 125 MB; tuy nhiên
   `pickupEtaImprovementCount` tính trên promise đã publish và **không** lọc
   `decisionDelta`, nên nó gộp exogenous drift. Analyzer nằm trong 46 file freeze-bound
   ⇒ chỉ được bổ sung công cụ, không được sửa.
3. **Dự đoán đăng ký trước cho `011`: F2 vô hiệu.** Trên 80 bundle E1 có 916 lần
   decision dịch chuyển ETA và **0 lần sớm hơn**; dev-panel B1 cho 48 và 0. Vì vậy
   `c1-ratchet` được dự đoán trùng hành vi `c1-h6ref`, `c1-freeze300ratchet` trùng
   `c1-freeze300`. Dự đoán **không** cho phép bỏ arm; freeze giữ exact 160 job.
4. **Rủi ro trần byte cao hơn rủi ro thời gian.** Chiếu ADR-070 đạt 93,31% của
   `maximumOutputBytes`; điểm hoà mỗi bundle C1 là `135.215.555` B trong khi bundle B1
   đã `125.237.277` B, và cả hai arm bật `retained-portfolio-full-witness-v1`. Nếu
   paired gate cho thấy C1 vượt ngưỡng đó thì `009` cần freeze v3 **trước outcome**,
   không được sửa giữa run.

Hai công cụ bổ sung, không nằm trong freeze nào và không có thẩm quyền riêng:
`wp14r_promise_direction.py` (7 test) và `wp14r_matrix_driver.py` (8 test). Cả hai
freeze vẫn rebuild-verify exact sau khi thêm chúng.

## 13. Closure `RB-WP14R-008` — FAIL CLOSED 2026-08-29

Preflight pass lần đầu (`observation-0004`, memory `8.917.082.112` B) và attempt 1 mở.
Child chết ngay ở `read_freeze`: nó verify lại receipt trước khi làm gì, nhưng
`hostPolicy.requiredHostFingerprintSha256` phụ thuộc `platform.machine()`, mà trên
Windows hàm này đọc `PROCESSOR_ARCHITECTURE` — biến **không** có trong
`inheritedEnvironmentNames`. Recovery mở đúng attempt 2, chết giống hệt, không attempt 3.

```text
receipt yêu cầu                     efebacc0…c6f81
parent (env đầy đủ)                 efebacc0…c6f81  ✅
child (đúng allowlist)              85e172e3…48f8   ❌
child + PROCESSOR_ARCHITECTURE      efebacc0…c6f81  ✅
```

Defect tất định, độc lập host: mọi launch freeze v2 trên Windows đều hỏng ở job đầu.
Test không bắt được vì không test nào chạy `verify_receipt` dưới đúng allowlist của
child — đó là regression test còn thiếu.

Hệ quả: job 1 `exhausted`, paired gate không thể đạt `2 valid / 0 failed`, `009..012`
không được authorize dưới freeze v2, C1 chưa từng được chạm, zero scientific outcome.
Mở khoá cần **freeze v3 trước outcome** do chủ nghiên cứu quyết; khuyến nghị thêm
`PROCESSOR_ARCHITECTURE` vào allowlist kèm regression test dưới allowlist của child.
Đầy đủ:
[`wp14r-008-paired-gate-fail-closed-2026-08-29.md`](../benchmarking/wp14r-008-paired-gate-fail-closed-2026-08-29.md).

## 14. `RB-WP14R-008R` — paired gate dưới freeze v3

ADR-073 authorize freeze v3 sau khi `008` chứng minh freeze v2 không thể chạy. Ba thay
đổi, không hơn:

| # | Thay đổi | Lý do |
|---|---|---|
| 1 | `PROCESSOR_ARCHITECTURE` vào `inheritedEnvironmentNames` | Sửa đúng nguyên nhân; kiến trúc trở thành term được hash trong command binding thay vì suy ra ngầm |
| 2 | Ledger/control root mới `development-v3-*` | Attempt đã exhausted của v2 nằm yên tại chỗ |
| 3 | Hai root v2 vào forbidden set (5 → 7 root) | v3 không bao giờ ghi đè được bằng chứng failure của v2 |

Thiết kế khoa học **không đổi**: cùng base freeze v1 byte-exact `1ce26ff0…37a55`, cùng
160 job, cùng paired gate B1→C1, cùng host threshold.

`wp14r_freeze_v2.py` giữ **byte-identical** để authorization v2 và failure của nó còn
verify được. Thay vào đó `wp14r_scientific_protocol.py` được sửa để chọn builder theo
`freezeId` của receipt; thay đổi này khai báo trong
[`source-divergence-v2.json`](../../benchmarks/scenarios/wp14r-development/source-divergence-v2.json)
với bytes gốc đã archive và tái dựng khớp exact `36b4ad3d…631dd`.

**Sửa tại supervisor là bất khả thi** và điều đó quyết định phương án:
`wp14r_supervised_process.py` bị `validate_mechanics_gate_provenance` khoá theo fixture
của gate `006`, nên đổi nó sẽ phá luôn bằng chứng mechanics `002..006`.

Receipt v3: `freeze-v3-authorization.json`, SHA
`07baeda2b79f31b5d79318755afbe917b7a8a47a7509b3f09a1756a484fa9227`, 160 jobs.

Regression còn thiếu ở v2 nay đã có trong `test_wp14r_freeze_v3.py`: `verify_receipt`
chạy trong subprocess với **đúng** allowlist của child, và host fingerprint dưới
allowlist phải bằng fingerprint của parent và bằng giá trị trong receipt. Đây mới là
thứ ngăn lớp lỗi này lặp lại.
