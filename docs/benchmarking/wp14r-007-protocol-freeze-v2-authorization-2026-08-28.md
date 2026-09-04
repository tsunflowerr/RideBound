# RB-WP14R-007 — protocol/freeze-v2 authorization

> Ngày: 2026-08-28 (Asia/Bangkok)  
> Verdict: `DONE — PROTOCOL AUTHORIZED; EXECUTION PRECONDITIONS REQUIRED`  
> Queue consequence: `RB-WP14R-008 READY`, `009..012 UNAUTHORIZED`  
> Scientific execution trong ticket này: `0 jobs`

## 1. Quyết định

Authorize đúng một pre-outcome protocol freeze v2 cho successor WP14R. Authorization
này mở ticket paired resource gate `RB-WP14R-008`, nhưng không tự cho phép launch khi
host preflight fail. Preflight thật ngay sau freeze fail đúng
`POWER_SOURCE_NOT_AC`; vì vậy B1/C1 chưa chạy và attempt ledger vẫn trống.

WP14-v1/ADR-070 tiếp tục terminal FAIL CLOSED. Receipt v2 không sửa, retry, replace
hoặc diễn giải lại B1 valid/C1 partial của v1; bốn raw H6/E1 root và toàn WP14-v1
output được đưa vào exact forbidden-root set.

## 2. Authoritative freeze receipt

```text
Path: benchmarks/scenarios/wp14r-development/freeze-v2-authorization.json
Length: 9,034 bytes
SHA-256: 6b34010861d60d6f0e869e3115ee1b20c6b5eb2eba3d6823a7e16148d1a31237
Freeze ID: wp14r-resilient-development-v2
Base scientific jobs: 160
Source-controlled protocol files: 24
Mechanics gate artifacts: 3
Full methodology PDFs: 3 / 34 pages
```

Receipt là canonical JSON, strict Draft 2020-12 schema và rebuild/verify từ current
source. Nó tham chiếu byte-exact WP14-v1 scientific design SHA
`1ce26ff0…37a55`; không sao chép hoặc định nghĩa lại arm/cell/factor. Mọi lần verify
rebuild receipt từ base freeze, runtime, source, mechanics artifacts và active power
scheme rồi so sánh object/bytes.

## 3. Audit gate `002..006`

| Gate | Input authoritative | Kết quả audit trong freeze |
|---|---|---|
| `002` immutable ledger | source + v1 schemas | exact maximum two attempts; same freeze/job/command binding; no attempt 3 |
| `003` supervisor | source + log/report schemas | incremental journal, exact command/environment, process-tree containment |
| `004` recovery | recovery source/schema | mechanical validity only; tree uncertainty exhausts |
| `005` resource dimension | report SHA `44dce55e…78bce` | 48/48 mechanics cells pass; 256 MiB observed gate retained |
| `006` independent verifier | fixture SHA `1e4a6450…372104`; mutation SHA `9d8aacf4…72e1e` | current ledger/supervisor/recovery/verifier hashes still equal fixture; 15/15 mutation gate retained |

Freeze builder không chỉ hash ba report. Nó parse fixture/mutation provenance rồi
đối chiếu current source của ledger, fixture builder, supervisor, recovery,
independent verifier, mutation tool, v1 schema tree và pinned Python. Bất kỳ drift nào
đều fail trước authorization.

## 4. Integration gap được sửa trước khi ký

Audit source phát hiện ledger yêu cầu recovery giữ cùng `commandSha256`, nhưng một
launcher trực tiếp dùng `attempt-01/output` rồi `attempt-02/output` sẽ đổi arguments và
command hash. Test mechanics trước đây chưa ghép toàn chuỗi nên không làm lộ mâu thuẫn.

Protocol v2 dùng một source-bound wrapper command cố định. Wrapper chỉ đọc current
open-attempt receipt để suy ra output riêng `attempt-XX/output`, kiểm exact job binding,
rồi gọi cùng `actual_fleetpy_medium_preflight.py`/versioned Runner đã được WP14-v1
freeze. Vì đường dẫn attempt không nằm trong wrapper arguments/working directory/
environment binding, recovery giữ cùng command hash nhưng không ghi đè output cũ.

Chuỗi exact cho một job:

```text
freeze verify
  -> phase/order/resource authorization
  -> host preflight (append-only receipt; fail = no attempt consumed)
  -> immutable attempt start
  -> separate supervisor process + contained fixed wrapper
  -> same-team bundle validity verifier
  -> immutable terminal receipt or typed stale-open recovery
  -> separate independent ledger verifier
```

Nếu attempt 1 có mechanical failure an toàn, cùng invocation có thể đi tiếp attempt 2
sau một host preflight mới. Valid pass dừng ngay; tree uncertainty exhausts; attempt 3
không tồn tại. Pair/matrix decision không parse completed, burden hoặc route fields.

## 5. Exact execution policy

- paired jobs, đúng thứ tự:
  1. `w14-d20181112-s10-r1-w08-b1-ref-s7`;
  2. `w14-d20181112-s10-r1-w08-c1-h6ref-s7`;
- `maximumParallelJobs=1`, maximum job wall 2.700 giây;
- stream cap 16 MiB mỗi stream, heartbeat 1 giây, tree grace 2 giây;
- pair gate cần `2 valid / 0 failed`; một exhausted job làm gate fail;
- matrix chạy đúng base 160-job order, bỏ qua hai job pair đã independently valid;
- matrix chỉ mở bằng canonical paired-gate receipt bind current protocol source;
- retained output toàn ledger không vượt 20 GiB; prelaunch free disk ít nhất 25 GiB;
- attempts retained toàn bộ và không phải experimental units.

## 6. Host conditioning

Host policy được khóa trước outcome:

- exact Windows host fingerprint `efebacc0…c6f81`;
- AC line phải `online`;
- power scheme GUID exact Balanced `381b4222-f694-41f0-9685-ff5bb260df2e`;
- 10 CPU interval × 1 giây; mean ≤20%, từng sample ≤60%;
- available memory ≥8 GiB; free disk ≥25 GiB;
- không ghi arbitrary process name/command line.

Các threshold là conservative local engineering rule từ audit host và mechanics
evidence, không copy numeric recipe của paper. Chúng chỉ conditioning exact host,
không tạo between-host hoặc power-scheme superiority claim.

Preflight thật sau freeze:

```text
Receipt: E:\RideBoundData\wp14r\development-v2-control\authorization-preflight-20260828.json
SHA-256: 642b23efaf107e1e8ea99b68494dc3b5b0b6b7fab363861701b38eecc06cd622
Decision: FAIL — POWER_SOURCE_NOT_AC
CPU: 10/10 samples, mean 11.790%, max 14.656%
Memory: 8,861,749,248 bytes
Free disk: 144,954,671,104 bytes
Outcome fields read: false
```

Failure không tiêu thụ attempt. Không tự đổi power scheme, không hạ threshold và không
launch scientific job trên pin.

## 7. Verification hiện tại

```text
New host/freeze/protocol tests: 31/31 pass
All targeted WP14R tests: 95/95 pass in 171.964 s
Pinned Python/FleetPy: 337/337 zero skip in 87.180 s
Required `dotnet test RideBound.slnx`: 908/908; Benchmarking.Tests 148/148 in 1m41
JSON/schema: 1.350/1.350 and 93/93; Markdown: 287 files/385 local links, zero broken
Format: pass; `git diff --check`: pass (only historical LF→CRLF warning in wp13 audit)
Freeze-v2 rebuild/verify: PASS, 160 jobs / 24 files / exact SHA
WP14-v1 read-only reverify: PASS, 160 jobs / 46 files / exact SHA
Scientific jobs launched: 0
```

Các baseline trên là closure verification của ticket; chúng kiểm code/protocol/freeze,
không phải scientific service/burden result.

## 8. Claim boundary và queue

`RB-WP14R-007 Done`. `RB-WP14R-008 Ready` về implementation/protocol nhưng đang bị
host precondition chặn cho tới khi AC online và exact preflight pass. `009..012` vẫn
Unauthorized. Authorization này không phải result, không phải speedup/effectiveness
evidence, không rescue H6 và không mở WP15/H7.
