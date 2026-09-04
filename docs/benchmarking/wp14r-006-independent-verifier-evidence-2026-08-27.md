# RB-WP14R-006 — independent verifier và mutation matrix

> Ngày: 2026-08-27  
> Trạng thái: `Done`  
> Claim class: mechanics-only; không đọc/diễn giải scientific outcome  
> Quyết định kế tiếp: chỉ `RB-WP14R-007 Ready`; `008..012` vẫn Unauthorized

## 1. Kết quả

`RB-WP14R-006` đã thêm một verifier độc lập, read-only cho attempt ledger WP14R.
Verifier không import/call ledger writer, supervisor verifier hoặc recovery classifier;
nó tự đọc bytes/path, tự parse canonical JSON/NDJSON, tự dựng state machine và chỉ ghi
report ra stdout. Ba fixture hợp lệ và toàn bộ 15 lớp mutation predeclared đều đạt gate:

| Gate | Kết quả |
|---|---:|
| Clean complete journal, ledger còn Open | PASS |
| Partial journal + immutable recovery terminal | PASS |
| Legacy journal không có schema provenance | PASS, label `legacy` |
| Mutation classes caught đúng typed code | **15/15** |
| Scientific outcome fields read | **0** |
| Recovery/freeze-v2 authorization bởi verifier | **0** |

Đây là denominator của **mutation classes**, không phải experimental sample size và
không có CI, p-value, population claim, service claim hoặc benchmark ranking.

## 2. Implementation độc lập

Các artifact source-controlled mới:

- `benchmarks/schemas/wp14r/v1/independent-verification-report.schema.json`;
- `benchmarks/schemas/wp14r/v1/independent-fixture-receipt.schema.json`;
- `benchmarks/schemas/wp14r/v1/independent-mutation-report.schema.json`;
- `simulators/fleetpy-ridebound/wp14r_independent_verify.py`;
- `simulators/fleetpy-ridebound/wp14r_independent_fixture.py`;
- `simulators/fleetpy-ridebound/wp14r_independent_mutation.py`;
- `simulators/fleetpy-ridebound/tests/test_wp14r_independent_verify.py`;
- `simulators/fleetpy-ridebound/tests/test_wp14r_independent_mutation.py`.

Verifier tự kiểm:

1. ledger/job/attempt path không escape, overlap forbidden root, symlink hoặc Windows
   reparse-point junction;
2. exact attempt numbering, previous-attempt binding, immutable freeze/job/command
   binding và timestamp order;
3. canonical bytes + strict Draft 2020-12 schema cho start/terminal/recovery receipts;
4. journal theo streaming, hard bound 1 MiB/record, gapless sequence/SHA-256 chain,
   monotonic wall/UTC, base64 chunk bytes/hash/count, EOF, heartbeat, limit, child/tree
   và terminal state machine;
5. current schema/source provenance hoặc explicit `legacy`; partial/wrong/resealed
   provenance bị reject;
6. process-log/output inventory, manifest presence/hash mà không parse scientific
   outcome fields;
7. terminal, bundle-verifier, retry disposition và recovery receipt cross-binding;
8. schema tree và verifier source không đổi trong lần verify.

AST regression xác nhận verifier không import ba implementation writer/verifier cũ và
không có filesystem mutation call. Journal được xử lý từng record; bytes stream không
được materialize toàn file. Output file content chỉ đi qua incremental SHA-256.

## 3. Defect tìm thấy khi review source

### 3.1 Windows junction từng bị bỏ sót

Python pinned 3.10.20 không có `os.path.isjunction`. Helper cũ vì vậy nhận symlink
nhưng bỏ sót Windows junction thật. Pilot v2 đã tạo junction bằng PowerShell và làm lộ
gap này. Fix hiện dùng `st_file_attributes & FILE_ATTRIBUTE_REPARSE_POINT` trong cả:

- attempt ledger;
- supervisor executable/working-directory ancestry;
- independent verifier;
- fixture/mutation inventory.

Regression tạo junction thật, không mock, và cả writer-side lẫn independent verifier
đều reject. Đây là correction mechanics/path safety; không sửa hoặc tái diễn giải
WP14-v1/H6/E1 bytes.

### 3.2 Hai TOCTOU inventory window

Review từng hàm còn tìm thấy hai race ngoài mutation-at-rest:

- journal có thể đổi sau streaming semantic verification nhưng trước file inventory;
- file/entry có thể xuất hiện hoặc biến mất trong lúc output tree được enumerate/hash.

Fix bind streamed bytes/count/hash với inventory kế tiếp, kiểm stat trước/sau và so
directory signature trước/sau. Regression chủ động chèn byte/file giữa hai pha; cả hai
đều reject typed `INVENTORY_MISMATCH`. Canonical receipt reader của writer và verifier
cũng kiểm size/mtime trước/sau read.

## 4. Retained fixture receipt cuối

Authoritative source fixtures:

```text
Root: E:\RideBoundData\wp14r\independent-fixtures-v2-20260827
Receipt: independent-fixture-receipt.json
Receipt SHA-256: 1e4a6450a40a65109592514bcf96df5ce5e1d15977ae3b59488b7da466372104
Schema tree SHA-256: a83a2eb3e24e871c5b626badd9e372a90b34cc59989d5a4e0420d5e4a5c52e7b
```

| Fixture | Retained state | Journal | Job-tree SHA-256 |
|---|---|---|---|
| `wp14r-independent-clean-v1` | `attemptOpen` | `validComplete`, bound | `c79cd093…ef837f9` |
| `wp14r-independent-recovery-v1` | `recoveryAuthorized` | `validPartial`, bound | `2ef952ef…2a73fd` |

Clean fixture chỉ chạy synthetic Python child ghi literal mechanics bytes. Recovery
fixture giữ journal start-only rồi terminalize bằng immutable recovery receipt; child
command của recovery fixture không bao giờ được launch. Cả hai dùng cùng pinned Python,
current ledger/supervisor/recovery source hashes và không có scientific output.

## 5. Retained mutation matrix cuối

Authoritative report:

```text
Root: E:\RideBoundData\wp14r\independent-mutation-v4-20260827
Report: independent-mutation-report.json
Report SHA-256: 9d8aacf48a43449aadfec760c0efcc9f76dbf7fc3077730f90fffed4f5d72e1e
Verifier source SHA-256: 753a938f5ee93b7a3cece01b7ee3d2ec2f4d63e486ce5ec6235b5c90c07229ca
Mutation tool SHA-256: 4d4e662c323b443eeecb669b64f9d4ca63a07a23fe5969bb8a2d351b2b80522a
```

Mỗi case là byte-exact copy sang root riêng rồi chỉ nhận một mutation class:

| Case | Mutation class | Expected = observed |
|---|---|---|
| M01 | forbidden path overlap | `PATH_UNSAFE` |
| M02 | noncanonical receipt bytes | `CANONICAL_JSON` |
| M03 | start binding/hash | `START_BINDING` |
| M04 | terminal start-receipt hash | `TERMINAL_BINDING` |
| M05 | recovery-receipt hash | `RECOVERY_BINDING` |
| M06 | attempt gap | `ATTEMPT_SEQUENCE` |
| M07 | extra attempt | `ENTRY_UNEXPECTED` |
| M08 | journal hash chain | `JOURNAL_CHAIN` |
| M09 | resealed chunk bytes | `JOURNAL_SEMANTICS` |
| M10 | resealed EOF digest | `JOURNAL_SEMANTICS` |
| M11 | resealed duplicate state event | `JOURNAL_SEMANTICS` |
| M12 | bytes after terminal | `JOURNAL_FORMAT` |
| M13 | resealed schema provenance | `SCHEMA_PROVENANCE` |
| M14 | resealed supervisor source provenance | `SOURCE_PROVENANCE` |
| M15 | real Windows junction | `PATH_UNSAFE` |

Report được kiểm lại ngoài harness: canonical bytes, current strict schema, 15 unique
case ID, 15 unique class, exact expected/observed code và `caught=true` toàn bộ.

## 6. Pilot/failure retention

Không root nào bị overwrite hoặc xóa:

- `independent-mutation-v1-20260827`: 14/14 pre-junction matrix, report còn nguyên;
- `independent-mutation-v2-20260827`: failed pilot giữ partial cases; thất bại ở thao
  tác tạo junction và sau đó dùng để tái hiện gap detector;
- `independent-fixtures-v1-20260827` + `independent-mutation-v3-20260827`: pass sau
  junction correction nhưng trước final TOCTOU/schema hardening;
- fixture v2 + mutation v4 là authoritative artifact sau mọi correction.

Đây là protocol evolution có audit trail, không phải lặp để chọn kết quả tốt hơn. Mọi
run đều mechanics-only và mutation denominator không đi vào scientific analysis.

## 7. Verification cuối

```text
Targeted WP14R ledger/supervisor/fault/recovery/dimension/independent: 64/64 PASS
Full pinned Python/FleetPy: 306/306 PASS, 0 failed, 0 skipped
Required dotnet test RideBound.slnx: 908/908 PASS, 0 failed, 0 skipped
Benchmarking.Tests: 148/148 PASS in 1 m 49 s
dotnet format --verify-no-changes --no-restore: PASS
WP14-v1 read-only freeze: PASS, exact 160 jobs / 46 files / receipt SHA 1ce26ff0…37a55
JSON parse: 1,346/1,346 PASS (toàn cây, loại build output bin/obj)
Draft 2020-12 schemas: 90/90 PASS
Markdown/local links/fences: 285 files / 380 local links / 0 broken / 0 unbalanced
Changed WP14R Python lines >88: 0
Scientific execution: none
```

Full discovery thiếu `RIDEBOUND_FLEETPY_ROOT` có 13 skip và không được tính. Số
306/306 ở trên là command pinned đúng, có biến môi trường FleetPy và zero skip.

### Revalidation 2026-08-28

Documentation-close revalidation giữ nguyên source và mọi ceiling. Full pinned
Python/FleetPy pass lại `306/306` zero skip trong `200.231 s`. Lần chạy .NET đầu có
`907/908`: medium public drain chạm đúng typed `resource.cpu-time-exceeded`; isolated
repeat không shutdown build servers cũng fail. Sau precondition đã ghi từ WP14
(`dotnet build-server shutdown` và không chạy workload nặng song song), exact test
pass `1/1` và exact required command `dotnet test RideBound.slnx` pass `908/908`;
`Benchmarking.Tests` pass `148/148` trong `1 m 55 s`. CPU ceiling vẫn `120 s`.

Host lúc diagnostic dùng Windows Balanced và đang chạy bằng pin. Đây là setup-state
evidence, không phải lý do nới limit hoặc claim performance. `RB-WP14R-007` phải bind
power source/scheme và quiescence/build-server precondition nếu authorize freeze v2.

## 8. Claim boundary và next gate

Kết quả chỉ chứng minh implementation thứ hai bắt được các validity mutation đã khai
báo trên finite synthetic fixtures hiện hành. Nó không chứng minh không còn bug, không
độc lập theo nghĩa team/organization, không đo service/burden và không cứu WP14-v1.

`RB-WP14R-006 Done` chỉ làm `RB-WP14R-007 Ready` để owner audit và quyết định có đủ
cơ sở khóa exact protocol/freeze v2 hay không. Chưa có quyền chạy paired B1/C1 resource
gate, development matrix, bundle verifier, frontier hoặc closure scientific ticket
`008..012`.
