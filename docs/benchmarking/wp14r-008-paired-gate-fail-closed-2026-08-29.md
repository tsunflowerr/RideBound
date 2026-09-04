# RB-WP14R-008 — paired resource gate: FAIL CLOSED

> Ngày: 2026-08-29 (Asia/Bangkok)
> Verdict: **FAIL CLOSED** — job đầu tiên `exhausted`, paired gate không thể đạt
> `2 valid / 0 failed`
> Nguyên nhân: **defect của protocol freeze v2**, không phải của host
> Scientific outcome được sinh ra: **0** — mô phỏng chưa từng khởi động

## 1. Điều đã xảy ra

Host preflight cuối cùng **pass** sau khi giải phóng bộ nhớ:

```text
Receipt: preflight-attempt-01-observation-0004.json
status: pass · failureCodes: []
available memory 8.917.082.112 B · CPU mean 4,065% max 11,226%
AC online · Balanced GUID khớp
```

Attempt 1 mở, supervisor khởi chạy child, và child **chết sau vài giây**. Recovery
authorize đúng một attempt thứ hai; attempt 2 chết **giống hệt**. Không có attempt 3.

```text
[1/160] w14-d20181112-s10-r1-w08-b1-ref-s7   -> exhausted in 32,5 s
attemptCount 2 · independentVerificationStatus "valid"
exitClassification "processExitFailure" · retryDisposition "attemptsExhausted"
```

Mô phỏng chưa chạy một epoch nào. Bundle không tồn tại. `retainedOutputBytes = 0`.

## 2. Nguyên nhân gốc — defect trong freeze v2

Cả hai attempt chết ở cùng một chỗ, nguyên văn từ journal của supervisor:

```text
File "…/wp14r_scientific_protocol.py", line 547, in run_scientific_child
    freeze_receipt, freeze_sha256, dependencies = read_freeze(
File "…/wp14r_freeze_v2.py", line 509, in verify_receipt
    raise FreezeV2Error("freeze-v2 receipt differs from its exact sources")
```

Child verify lại freeze v2 trước khi làm bất cứ việc gì — đúng thiết kế. Nhưng nó
**không thể** verify, vì:

- `verify_receipt` dựng lại toàn bộ receipt và so sánh, trong đó có
  `hostPolicy.requiredHostFingerprintSha256`;
- fingerprint đó do `wp14r_supervised_process.host_fingerprint()` tính, gồm
  `platform.machine()`;
- trên Windows, `platform.machine()` đọc biến môi trường **`PROCESSOR_ARCHITECTURE`**;
- `protocol.inheritedEnvironmentNames` của freeze v2 chỉ cho phép đúng năm biến:
  `PATH`, `PYTHONDONTWRITEBYTECODE`, `SystemRoot`, `TEMP`, `TMP`.

Child vì thế thấy `platform.machine() == ''` thay vì `'AMD64'`, tính ra một fingerprint
khác, và freeze verify fail. Bằng chứng khép kín:

| Môi trường | `platform.machine()` | Host fingerprint |
|---|---|---|
| Receipt yêu cầu | — | `efebacc06c704d767ea59df7788ff107e2737c65dba1ae67506054abd53c6f81` |
| Parent (env đầy đủ) | `AMD64` | `efebacc0…c6f81` ✅ |
| Child (đúng allowlist) | `''` | `85e172e3ef9ca98d480e5661650dcba5391728cac5b56a24dafe87e1fb9048f8` ❌ |
| Child + `PROCESSOR_ARCHITECTURE` | `AMD64` | `efebacc0…c6f81` ✅ |

Chỉ **một** biến môi trường thiếu gây ra toàn bộ.

### Vì sao đây là defect chứ không phải sự cố vận hành

Nó **tất định và độc lập host**: bất kỳ ai launch freeze v2 trên Windows cũng gặp,
ở job đầu tiên, mọi lần. Parent verify pass rồi mới giao cho child một môi trường mà
trong đó chính receipt đó không verify được — hai bên bất đồng theo cấu tạo.

Test không bắt được vì `test_wp14r_scientific_protocol.py` kiểm parent path và fake
child, còn `RB-WP14R-007` verify freeze từ shell đầy đủ. Không test nào chạy
`verify_receipt` **dưới đúng allowlist** mà protocol áp cho child. Đây cùng một lớp lỗi
với gap mà `007` đã tự bắt được (`commandSha256` đổi giữa attempt 1 và 2): một mâu
thuẫn chỉ lộ ra khi ghép nguyên chuỗi.

## 3. Hệ quả theo contract đã freeze

- Job `w14-d20181112-s10-r1-w08-b1-ref-s7` là `exhausted`; `recoveryPolicy` là
  `oneInitialOneMechanicalRecoveryRetainAllNoThirdAttempt` nên **không có attempt 3**.
- Paired gate cần `2 valid / 0 failed`; một job exhausted làm gate fail vĩnh viễn dưới
  freeze v2.
- `authorize_phase` sẽ từ chối mọi job matrix khi còn một job exhausted, nên
  `RB-WP14R-009..012` **không được authorize** dưới freeze v2.
- C1 `w14-d20181112-s10-r1-w08-c1-h6ref-s7` chưa từng được chạm.

## 4. Điều machinery đã làm **đúng**

Đây là lần đầu WP14R chạy thật, và nó hành xử đúng như được thiết kế:

- preflight fail ba lần trên bộ nhớ mà **không** tiêu attempt nào;
- khi child chết, journal giữ đủ stdout/stderr thật để chẩn đoán tận dòng traceback —
  chính là năng lực mà `RB-WP14R-003` được tạo ra để có, và là thứ WP14-v1 **không**
  có khi C1 bị chấm dứt;
- recovery cho đúng một attempt, rồi `attemptsExhausted`, không có attempt 3;
- independent ledger verifier báo cấu trúc ledger `valid`;
- không outcome nào bị đọc để authorize recovery; không byte nào bị ghi đè.

Nói cách khác: WP14R đã bắt được một defect ở job đầu tiên trong 32 giây, với bằng
chứng đủ để chỉ đúng một biến môi trường. WP14-v1 ở tình huống tương tự chỉ để lại một
transcript cụt không giải thích được.

## 5. Artifact được giữ lại

```text
E:\RideBoundData\wp14r\development-v2-ledger\w14-d20181112-s10-r1-w08-b1-ref-s7
  attempt-01/attempt-start.json       1.085 B  af2d913241…
  attempt-01/attempt-terminal.json    1.016 B  9789fa4bfd…
  attempt-01/process.log             10.481 B  f08e0f5f9b…
  attempt-02/attempt-start.json       1.128 B  682a194969…
  attempt-02/attempt-terminal.json    1.015 B  06128099a8…
  attempt-02/process.log             10.480 B  b7b892f4fa…

E:\RideBoundData\wp14r\development-v2-control\w14-d20181112-s10-r1-w08-b1-ref-s7
  5 preflight receipts: 0001–0003 fail MEMORY_BELOW_MINIMUM, 0004 pass
```

Toàn bộ được giữ nguyên, không xoá, không ghi đè, không thay bằng lần chạy thuận lợi
hơn — đúng `retainTypedFailureNoRetryNoReplacement`.

## 6. Quyết định cần chủ nghiên cứu đưa ra

Sửa **không** thuộc thẩm quyền của agent, vì `wp14r_scientific_protocol.py`,
`wp14r_supervised_process.py` và `wp14r_freeze_v2.py` đều nằm trong 24 file bị freeze v2
bind, và receipt bind `inheritedEnvironmentNames`.

Đường hợp lệ là một **freeze v3** trước outcome, với một trong hai lựa chọn:

| Lựa chọn | Nội dung | Đánh đổi |
|---|---|---|
| A | Thêm `PROCESSOR_ARCHITECTURE` vào `inheritedEnvironmentNames` | Sửa tối thiểu, giữ nguyên định nghĩa fingerprint; nhưng vẫn để fingerprint phụ thuộc môi trường |
| B | Bỏ `platform.machine()` khỏi `host_fingerprint()`, hoặc lấy kiến trúc từ nguồn không phải env | Fingerprint trở nên độc lập môi trường; nhưng đổi định nghĩa fingerprint nên mọi receipt cũ phải rebuild |

Khuyến nghị: **A cho freeze v3** để mở khoá `008` ngay với thay đổi nhỏ nhất và kiểm
được, kèm một regression test chạy `verify_receipt` **dưới đúng allowlist của child** —
đó là test còn thiếu, và nó mới là thứ ngăn lỗi này lặp lại. B đáng làm nhưng thuộc một
ADR riêng vì nó đổi ngữ nghĩa fingerprint.

Dù chọn gì: job đã exhausted **không** được hồi sinh dưới freeze v2, và freeze v3 phải
được ký **trước** khi có bất kỳ outcome nào — không sửa giữa run.

## 7. Điều **không** được kết luận từ tài liệu này

- Không phải kết quả khoa học: không có completion, burden hay route nào được sinh ra.
- Không nói gì về H6, WP10, WP13 hay WP14-v1; tất cả vẫn nguyên vẹn.
- Không phải bằng chứng về hiệu năng, tài nguyên hay khả năng của matrix — hai con số
  mà paired gate lẽ ra phải đo (wall thật của C1 với cờ skip, và bytes thật của bundle
  C1) **vẫn chưa có**.
- Không authorize `009..012`, WP15 hay H7.

---

## 8. Hậu freeze v3 — defect thứ hai, ở tầng verifier

ADR-073 đã sửa đúng defect thứ nhất. Bằng chứng trực tiếp: dưới freeze v3, child
verify được receipt và B1 **chạy trọn mô phỏng** thay vì chết trong 16 giây.

| | Freeze v2 | Freeze v3 |
|---|---|---|
| Child verify receipt | fail ngay | **pass** |
| B1 wall | 32,5 s cho cả hai attempt | **754,4 s**, một attempt |
| Bundle sinh ra | 0 byte | **125.237.277 byte** |
| Ledger state | `exhausted` | **`succeeded`** |

Nhưng job vẫn không qua được gate: `WP14R_PROTOCOL_ERROR: independent ledger
verification failed`, typed `JOURNAL_CHAIN`.

### Nguyên nhân

`wp14r_independent_verify.py` kiểm chuỗi journal và reject khi

```python
record["monotonicElapsedMs"] < previous_elapsed
or (previous_observed is not None and observed < previous_observed)
```

Kiểm độc lập cả hai journal cho thấy hash chain, sequence và `monotonicElapsedMs`
**sạch tuyệt đối ở cả hai attempt**. Cái fail là vế thứ hai:

```text
attempt-01  record 496 (heartbeat)
  observedUtc 2026-08-28T19:54:15.636123Z -> 2026-08-28T19:54:13.790470Z
  đồng hồ tường lùi 1,846 giây
attempt-02  0 clock regression
```

Đây là một bước chỉnh NTP của Windows Time trong lúc chạy, không phải hành vi của
protocol và không nằm trong tầm kiểm soát của thí nghiệm.

### Vì sao đây là defect chứ không phải dữ liệu xấu

Contract của `RB-WP14R-003` nói rõ: *"monotonic clock quyết định heartbeat/timeout;
UTC chỉ provenance"*. Nhưng verifier lại cưỡng chế UTC monotonicity như một bất biến
cứng của chuỗi. Hai phần của cùng một protocol mâu thuẫn nhau, và một bước NTP 1,8 giây
đủ để biến một attempt đã thành công và hoàn toàn sạch thành không thể verify.

Nặng hơn: verifier duyệt **mọi** attempt được giữ lại. Attempt-01 đã fail và được giữ
đúng contract; chính việc giữ nó làm hỏng verification của attempt-02 vốn sạch.

### Trách nhiệm

Attempt-01 tồn tại là **do lỗi của agent**: trong lúc B1 chạy lần đầu, tài liệu trong
repo bị sửa, làm `actual_fleetpy_medium_preflight.py` phát hiện
`repository content inventory drifted` và kết thúc job. Nếu B1 chạy sạch ngay attempt 1
thì không có attempt-01 nào để mang bước NTP, và gate nhiều khả năng đã pass. Lỗ hổng
`__pycache__` đã được vá trong `.gitignore` sau đó, nhưng attempt đã tiêu thì không lấy
lại được.

### Hệ quả

- B1 đã dùng hết 2/2 attempt; không có attempt 3.
- Ledger nói `succeeded`, nhưng independent verification là gate bắt buộc và nó fail,
  nên `pair_gate_row` sẽ ghi B1 là `invalid` và paired gate không thể đạt
  `2 valid / 0 failed`.
- C1 chưa từng được chạm.
- **Freeze v3 vì thế cũng bị chặn**, bởi một defect khác với freeze v2.

### Vì sao không thể sửa như đã sửa v2

`wp14r_independent_verify.py` nằm trong đúng năm file bị
`validate_mechanics_gate_provenance` khoá theo fixture của gate `006`
(`attemptLedger`, `builder`, `independentVerifier`, `recovery`, `supervisor`). Sửa nó
làm mọi freeze builder fail ngay ở bước provenance, tức phá bằng chứng mechanics
`002..006`. Đường v2→v3 dùng được vì chỉ chạm builder và protocol; đường này thì không.

### Lựa chọn thuộc về chủ nghiên cứu

| | Nội dung | Cái giá |
|---|---|---|
| A | Dựng lại gate `006` (fixture + mutation matrix) với verifier đã sửa quy tắc UTC, rồi freeze v4 | Mở lại mechanics gate; công việc lớn nhất nhưng sửa đúng mâu thuẫn contract |
| B | Giữ nguyên verifier; chấp nhận rằng job nào có attempt giữ lại dính clock step là mất | B1 không còn attempt ⇒ paired gate của v3 vĩnh viễn không đạt |
| C | Freeze v4 chỉ đổi phạm vi verify (ví dụ chỉ chuỗi của attempt được chọn) | Vẫn phải sửa verifier ⇒ vẫn vướng provenance `006` |

Không lựa chọn nào được thực hiện mà không có ADR mới. Không attempt nào bị hồi sinh,
không receipt nào bị sửa, và mọi artifact của v2/v3 được giữ nguyên.

## 9. Kết quả phụ có giá trị: tái lập byte-exact xuyên freeze

Attempt-02 của B1 dưới freeze v3 được so với chính job đó chạy dưới WP14-v1 ba ngày
trước. Khác freeze, khác protocol version, khác ledger root, khác ngày:

| | WP14-v1 (2026-08-26) | WP14R freeze v3 (2026-08-29) |
|---|---:|---:|
| transcript | 125.230.809 B | **125.230.809 B** |
| full bundle | 125.237.277 B | **125.237.277 B** |
| `semanticHash` | `d072b931…c8393d` | **`d072b931…c8393d`** |
| wall | 755.379 ms | 754.400 ms |

Byte-exact và hash-exact. Đây là lần **tái lập xuyên freeze đầu tiên** của dự án, và nó
xác nhận trực tiếp rằng pipeline sinh outcome là tất định: cùng scenario content hash
`9f9c177d…47cd4`, cùng seed, cùng Runner ⇒ cùng transcript tới từng byte, bất kể
protocol bọc ngoài là v1 hay v3.

Wall chênh `979` ms giữa hai lần chạy là biến động host, không phải khác biệt hành vi.

Kết quả này **không** phải outcome khoa học: không completion, burden hay route nào được
đọc. Nó là bằng chứng determinism ở tầng cơ học, và nó đứng độc lập với việc paired gate
có pass hay không.
