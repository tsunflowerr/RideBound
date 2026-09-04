# Handoff WP14R — tiếp tục resilient execution

> Cập nhật: 2026-08-28 (Asia/Bangkok)
> Trạng thái authoritative: đọc `docs/18-status-and-decision-log.md` trước
> Queue head duy nhất: `RB-WP14R-008 READY — AC PREFLIGHT BLOCKED`
> Dành cho agent tiếp theo/Gemini; không coi đoạn chat cũ là source of truth

## 1. Tóm tắt bắt buộc

WP14 freeze v1 đã dừng **FAIL CLOSED** ở `RB-WP14-009`: B1 valid, C1 partial thiếu
manifest, không retry/replacement; `RB-WP14-010..014` không được authorize. Freeze
receipt vẫn verify PASS exact 160 jobs/46 repository files, SHA-256:

```text
1ce26ff0f7d87c30d050e57107ad3e118af7f4b88fe04e62e48376ab34c37a55
```

ADR-071 mở successor riêng `WP14R` chỉ cho execution mechanics. Nó không rescue
WP14-v1, không mở WP15/H7 và không cho phép scientific matrix trước ticket `007`.

Đã hoàn thành:

- `RB-WP14R-001`: full-PDF/source failure audit + refinement + queue;
- `RB-WP14R-002`: strict immutable two-attempt ledger;
- `RB-WP14R-003`: bounded supervised process journal + OS process-tree containment;
- `RB-WP14R-004`: actual hard-crash matrix + immutable stale-open recovery;
- `RB-WP14R-005`: mechanics resource dimensioning + schema-validator optimization;
- `RB-WP14R-006`: independent verifier + 15-class retained mutation matrix.
- `RB-WP14R-007`: exact protocol freeze-v2 authorization + host conditioning.

`RB-WP14R-008` Ready về protocol, nhưng current host preflight fail
`POWER_SOURCE_NOT_AC`; `009..012` vẫn Unauthorized.

## 2. Đọc trước khi sửa

Theo `AGENTS.md`, bắt buộc đọc đầy đủ:

1. `docs/00-index.md`;
2. `docs/01-research-charter.md`;
3. `docs/18-status-and-decision-log.md`;
4. WP14/WP14R trong `docs/16-roadmap-and-work-packages.md`;
5. `docs/tasks/45-wp14r-resilient-execution-refinement.md`;
6. `docs/tasks/46-wp14r-resilient-execution-ticket-plan.md`;
7. `docs/benchmarking/wp14r-002-immutable-attempt-ledger-2026-08-26.md`;
8. `docs/benchmarking/wp14r-003-supervised-process-evidence-2026-08-26.md`;
9. `docs/benchmarking/wp14r-004-hard-crash-recovery-evidence-2026-08-26.md`;
10. `docs/benchmarking/wp14r-005-mechanics-resource-dimension-evidence-2026-08-27.md`;
11. research note
   `docs/research/wp14r-resilient-benchmark-full-pdf-evidence-2026-08-26.md`;
12. `docs/benchmarking/wp14r-007-protocol-freeze-v2-authorization-2026-08-28.md`;
13. `docs/research/wp14r-freeze-v2-full-pdf-evidence-2026-08-28.md`;
14. ADR-070/071/072 và `docs/19-requirement-traceability.md` §25;
15. source/test/schema protocol/freeze-v2/host gate liệt kê ở mục 7A.

Repository có **0 `.docx`** tại checkpoint này. “Docs/docx” trong yêu cầu thực tế là
Markdown dưới `docs/`; không được bịa một Word artifact đang tồn tại.

## 3. Full-PDF corpus đã kiểm tra

External read-only research corpus:

```text
E:\RideBoundData\research\pdf-20260826-wp14r-benchmark-methodology
```

| PDF | Trang | SHA-256 |
|---|---:|---|
| Kalibera & Jones 2013, *Rigorous Benchmarking in Reasonable Time* | 12/12 | `b50fb85079cbaea9524eb202393a60807dd3fc270d91eb80de5dba0faf02dbab` |
| Mytkowicz et al. 2009, *Producing Wrong Data Without Doing Anything Obviously Wrong!* | 12/12 | `67505bfc1f5a9a442d3ba7f5a5a22e05e55569237def1964a7eab2e7533ee2d6` |

`fulltext-inventory.json` trong corpus bind length/hash/pages/provenance. 24/24 trang
đã extract nonempty và render/visual QA. Applied boundary:

- tách host session / launcher process / simulator job / verifier variation;
- repeated same setup không loại systematic setup bias;
- fault injection là causal intervention cho mechanics, không cho service outcome;
- không copy repetition count/CI/numeric default từ paper;
- recovery attempt không phải replicate.

## 4. Ledger và supervisor v1 hiện hành

Source mới, không sửa file freeze-bound:

```text
benchmarks/schemas/wp14r/v1/attempt-record.schema.json
benchmarks/schemas/wp14r/v1/ledger-inspection.schema.json
benchmarks/schemas/wp14r/v1/mechanics-dimension-report.schema.json
benchmarks/schemas/wp14r/v1/recovery-receipt.schema.json
benchmarks/schemas/wp14r/v1/supervision-log-record.schema.json
benchmarks/schemas/wp14r/v1/supervision-report.schema.json
simulators/fleetpy-ridebound/wp14r_attempt_ledger.py
simulators/fleetpy-ridebound/wp14r_fault_injection.py
simulators/fleetpy-ridebound/wp14r_resource_dimension.py
simulators/fleetpy-ridebound/wp14r_stale_open_recovery.py
simulators/fleetpy-ridebound/wp14r_supervised_process.py
simulators/fleetpy-ridebound/tests/test_wp14r_attempt_ledger.py
simulators/fleetpy-ridebound/tests/test_wp14r_fault_recovery.py
simulators/fleetpy-ridebound/tests/test_wp14r_resource_dimension.py
simulators/fleetpy-ridebound/tests/test_wp14r_supervised_process.py
```

Contract:

- exact `attempt-01` và optional `attempt-02`, maximum 2;
- canonical UTF-8, exclusive-create, `fsync`, SHA cross-binding;
- attempt 2 chỉ sau first mechanical failure; same freeze/job/command binding;
- pass cần manifest + verifier ID/hash + behavioral hash + exit 0/clean tree;
- pass chặn retry; attempt 2 fail exhausted;
- no outcome read, attempts not experimental units;
- recompute exact process-log/output inventories sau terminal;
- gap/extra/renamed entry, receipt/log/output tamper, timestamp reversal, links/
  junctions, unsafe path/forbidden overlap fail closed.

Vòng review thủ công của ledger đã sửa partial write, unstable hash inventory, poisoning
bằng binding mới trước attempt 2, renamed-entry escape và inconsistent process/verifier
semantics. Supervisor sau đó bổ sung:

- recompute command binding gồm executable bytes/path, raw-argument digest, cwd và
  allowlisted environment-value hashes nhưng không journal raw arguments/secrets;
- canonical LF NDJSON, exclusive create, gapless sequence/SHA chain và `fsync` từng
  record; stdout/stderr raw bytes theo bounded queue, base64 chunk và verified prefix;
- monotonic heartbeat/wall timeout, UTC provenance; Windows Job Object
  `KILL_ON_JOB_CLOSE` và POSIX process group;
- typed launch/containment/nonzero/timeout/cap/reader/cancel/tree failures; containment
  attach failure sau process creation giữ trung thực `treeUncertain`;
- strict semantic verifier cho canonical bytes, chain, chunks/EOF/totals, state order và
  terminal mapping; resealed mutation vẫn bị reject;
- exit zero chỉ là `childExitedZeroAwaitingBundleVerification`; supervisor luôn ghi
  bundle status `notRun`, ledger còn Open cho independent bundle verifier.

`004` đã thêm durable `launchIntent`, supervisor PID và recovery receipt. Hard-kill
matrix chạy 11 supervisor point cùng recovery receipt→terminal point. Incomplete first
record, pre-containment ambiguity và tree uncertainty đều exhaust; chỉ proven-safe tree
mới mở attempt 2. Actual Windows Job Object kill child/grandchild; POSIX exact-group path
có contract test. Exit-zero complete journal vẫn Open chờ independent bundle verifier.

`005` giữ ba external roots bất biến: v1 pilot fail large timeout; v2 before-cache pass;
v3 after-cache pass. V3 bind full WP14R schema-tree hash, có 48/48 sample pass và fixed
8 MiB verifier peak `65,875,968` bytes dưới envelope 256 MiB. Cache compiled schema
validator giảm observed large launcher/verifier median `71.30/35.39 s` xuống
`4.68/1.96 s`; đây chỉ là sequential within-host descriptive before/after. Journal mới
bind log/report schema SHA; legacy journal vẫn verify qua explicit legacy branch.

## 5. Baseline authoritative

```text
Targeted WP14R, gồm host/freeze/protocol v2: 95/95, zero skip
Pinned Python/FleetPy: 337/337, zero skip
Required dotnet test RideBound.slnx: 908/908, zero skip
Benchmarking.Tests: 148/148 in 1 m 41 s; CPU ceiling 120 s unchanged
dotnet format --verify-no-changes --no-restore: PASS
JSON 1,350/1,350; Draft 2020-12 schemas 93/93
Markdown 287 files/385 local links/0 broken/0 unbalanced fences
New Python lines >88: 0
WP14-v1 read-only reverify: PASS, 160 jobs, 46 files, exact receipt hash
WP14R freeze-v2 reverify: PASS, 160 jobs, 24 source/schema/test files, exact receipt hash
```

Hai discovery runs không set `RIDEBOUND_FLEETPY_ROOT` có 13 skip và **không được tính**.
Command đúng:

```powershell
$env:PYTHONDONTWRITEBYTECODE='1'
$env:RIDEBOUND_FLEETPY_ROOT='E:\RideBoundData\wp7\FleetPy-1.0.2'
cd E:\Code\RideBound\simulators\fleetpy-ridebound\tests
& 'E:\RideBoundData\wp7\envs\fleetpy-1.0.2\python.exe' -B `
  -m unittest discover -s . -t . -p 'test_*.py'
```

Trước .NET medium gate:

```powershell
dotnet build-server shutdown
dotnet test RideBound.slnx
```

Không chạy Python/.NET/simulation nặng song song; medium drain nằm sát CPU ceiling.
Trước required .NET baseline, chạy `dotnet build-server shutdown`. Revalidation
2026-08-28 fail CPU gate hai lần khi chưa shutdown, rồi exact test `1/1` và full
`908/908` pass sau shutdown mà không đổi ceiling; xem evidence `wp14r-006`.

## 6. `RB-WP14R-005` closure

Authoritative report là v3:

```text
E:\RideBoundData\wp14r\mechanics-dimension-v3-20260827\mechanics-dimension-report.json
SHA-256 44dce55e89c9602daeedc601471e5d2873ab959f86c8bb2394460291baf78bce
```

V1 SHA `a2795a84…6817f` và v2 SHA `96e274a2…c2ce5` phải giữ nguyên. Full result,
limitations, eight-cell table và before/after optimization nằm trong evidence `005`.

## 7. `RB-WP14R-006` closure

Authoritative artifacts:

```text
E:\RideBoundData\wp14r\independent-fixtures-v2-20260827
receipt SHA 1e4a6450a40a65109592514bcf96df5ce5e1d15977ae3b59488b7da466372104

E:\RideBoundData\wp14r\independent-mutation-v4-20260827
report SHA 9d8aacf48a43449aadfec760c0efcc9f76dbf7fc3077730f90fffed4f5d72e1e
```

Clean complete-open, partial-recovery và legacy fixtures đều valid. Exact 15/15 class
bị reject đúng code, gồm path overlap, junction thật, receipt binding, attempt state,
journal chain/chunk/EOF/state/append và schema/source provenance. Source review sửa
Python 3.10 junction blind spot cùng journal/output inventory TOCTOU. V1/v2/v3 pilots
đều giữ nguyên. Full report:
[`wp14r-006-independent-verifier-evidence-2026-08-27.md`](../benchmarking/wp14r-006-independent-verifier-evidence-2026-08-27.md).

## 7A. `RB-WP14R-007` closure và next action `008`

`007 Done`: canonical receipt
`benchmarks/scenarios/wp14r-development/freeze-v2-authorization.json`, SHA
`6b34010861d60d6f0e869e3115ee1b20c6b5eb2eba3d6823a7e16148d1a31237`, bind exact
160-job base freeze, 24 source/schema/test files, evidence gate `002..006` và 34
full-PDF pages. New source: `wp14r_freeze_v2.py`, `wp14r_host_preflight.py`,
`wp14r_scientific_protocol.py`, v2 schemas và ba test modules. Wrapper giữ command
identity qua recovery nhưng tách attempt output; pair/matrix vẫn gọi same versioned
actual FleetPy/Runner preflight.

`008` chỉ được làm theo thứ tự B1→C1 đã freeze, sequential, maximum hai attempt/job,
không read outcome để authorize/recover. Trước **mỗi** launch, exact Windows host
fingerprint, AC online, Balanced GUID, 10×1 s CPU, 8 GiB memory và 25 GiB free disk
phải pass. Không tự thay power scheme. Current authorization preflight retained ở:

```text
E:\RideBoundData\wp14r\development-v2-control\authorization-preflight-20260828.json
SHA-256 642b23efaf107e1e8ea99b68494dc3b5b0b6b7fab363861701b38eecc06cd622
FAIL: POWER_SOURCE_NOT_AC
```

Khi người dùng cắm AC, chạy preflight mới qua protocol CLI rồi mới `run --phase paired`
cho B1; sau B1 independently succeeds mới được chạy C1. Nếu pair không đạt 2 valid/0
failed, write canonical paired-gate failure và dừng. Chỉ paired-gate pass mới mở `009`.

## 8. Ranh giới tuyệt đối

- Không sửa/move/delete raw H6, E1 hoặc WP14-v1 output/partial.
- Không sửa 46 repository files trong receipt hoặc dùng source-divergence để lách H6.
- Không retry C1 partial hay gọi attempt mới là replacement của v1.
- Không đọc completion/burden/routes để authorize recovery.
- Không nhân denominator/N bằng attempt/seed/rider.
- Không dùng B1-only projection để pass paired resource gate.
- Không launch `008` khi current host preflight fail; `009..012`, WP15/H7 vẫn chưa mở.
- Không claim effectiveness/SLA/population/causal mechanism từ mechanics pass.

## 9. Dirty worktree

Worktree có nhiều thay đổi WP13/WP14 của chủ repo/agent trước; tất cả phải được giữ.
Không reset/checkout/cleanup. Chỉ file `wp14r`/docs liên quan ở handoff này là phạm vi
mới. Luôn xem `git status --short` và `git diff -- <path>` trước khi sửa file trùng.
