# WP14R — refinement cho resilient benchmark execution

> Trạng thái: `RB-WP14R-001 DONE`
> Quyết định: ADR-071
> Successor của mechanics WP14; không supersede freeze/result WP14-v1

## 1. Outcome

Tạo một execution boundary có thể kiểm toán khi launcher/child/host bị gián đoạn,
trước khi cân nhắc chạy lại development ablation dưới protocol mới. Boundary phải:

- giữ attempt directory và receipt bất biến;
- tách attempt khỏi experimental unit;
- phục hồi chỉ theo mechanical validity, không theo scientific outcome;
- ghi incremental process evidence trước terminal publication;
- có fault-injection và independent ledger verification;
- dimension tài nguyên trên chính mechanics mới trước một freeze v2.

## 2. Non-goals

- Không chạy lại hoặc thay thế C1 partial của WP14-v1.
- Không sửa 46 file được bind bởi freeze receipt H6/ADR-069.
- Không tune F1/F2, service outcome, panel, denominator hoặc failure treatment.
- Không mở F3–F6, WP15, H7, policy v2 hoặc population inference.
- Không dùng attempt như replicate và không chọn “best successful attempt”.

## 3. Fixed recovery policy

| Thuộc tính | Contract |
|---|---|
| Maximum | `1 initial + 1 recovery = 2 attempts/job` |
| Unit | job là unit; attempt không phải experimental unit |
| Retention | giữ mọi start/terminal/log/output, không overwrite/delete |
| Authorization | chỉ mechanical failure/invalidity; cấm đọc outcome |
| Valid bundle | first independently verified valid bundle là terminal success |
| Sau success | cấm mọi recovery |
| Sau attempt 2 fail | exhausted/fail-closed, không attempt 3 |
| WP14-v1 | không áp dụng hồi tố, không supersede receipt/failure |

Một start receipt bind freeze/protocol/job/command trước khi launch. Terminal receipt
cross-bind start hash và inventory thực tế. Open start còn lại sau crash chỉ được
terminalize bằng classification riêng; không được xóa để giả như attempt chưa xảy ra.

## 4. Dependency và gate

```text
full-PDF + source failure audit
  -> immutable attempt ledger
  -> supervised executor + incremental evidence
  -> fault injection / orphan recovery
  -> no-outcome resource dimensioning
  -> independent verifier / mutation matrix
  -> owner authorization + freeze v2
  -> paired resource gate
  -> matrix -> verifier -> frontier -> closure audit
```

`RB-WP14R-002..006` là mechanics-only. `007` là decision gate; không một scientific
execution ticket nào được Ready trước khi `007` chấp nhận exact protocol/freeze v2.

## 5. Biến cần tách

Theo Kalibera & Jones, successor phải phân biệt ít nhất host session, launcher
process, simulator job và verifier. Theo Mytkowicz et al., repeated same setup không
đủ để loại systematic bias; setup/command/host/process metadata phải được bind, và
fault injection phải kiểm tra kết luận recovery qua intervention có chủ đích.

Không copy số repetition hoặc confidence recipe từ paper. `RB-WP14R-005` sẽ đo
variance/cost của mechanics mới rồi mới đề xuất dimension cụ thể.

## 6. Exit gate WP14R

- ledger/supervisor/verifier schemas strict và independently kiểm tra được;
- fault matrix bắt được launcher/child/timeout/stale-open/tamper/process-tree cases;
- resource gate paired trên protocol mới pass theo envelope đã predeclare;
- matrix mới chỉ tồn tại sau freeze v2 và không chạm raw H6/E1/WP14-v1;
- frontier/report giữ claim descriptive development-only;
- full source/logic/claim audit zero unresolved P0–P2.

Nếu một gate fail, queue dừng fail-closed; WP15 vẫn roadmap-level.
