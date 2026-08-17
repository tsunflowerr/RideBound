# WP5: BeGo durable boundary

## 1. Ai sở hữu logic nào?

RideBound sở hữu state transition, candidate/policy, commitment validator, certificate
và checkpoint semantics. BeGo sở hữu auth/API, mapping nguồn, durable queue, lease,
transaction, recovery, audit timeline, outbox/SignalR và rollout. Hai repository không
project-reference source của nhau; BeGo gọi pinned `RideBound.Runner` qua NDJSON.

## 2. Bootstrap không biến dữ liệu cũ thành promise giả

`CommitBootstrapMapper` snapshot BeGo source trước external I/O, pseudonym HMAC theo
run, đổi đơn vị E7/ms ties-to-even, tạo complete directed matrix và field provenance.
Old assignment/route snapshot chỉ là provenance; Runner mới quyết định B1/C1. Missing/
invalid field fail closed hoặc exclusion theo rule, không tự bịa time window/budget.

## 3. Durable T1/T2/T3

- T1: API accept canonical event batch bằng idempotency fingerprint; DB serialize
  per-run cursor và tạo operation.
- T2: worker claim bằng DB-time lease + revision fence, gọi Runner, materialize exact
  decision/certificate, lưu decision/projection/timeline/outbox atomically.
- T3: worker reconstruct session bằng hello/init/previous checkpoint/event, đòi exact
  decision replay, gửi `decisionApplied` + checkpoint, rồi lưu matching checkpoint và
  chuyển operation Applied trong transaction.

Crash giữa các boundary để lại typed recoverable/uncertain state. Stale worker không
thể complete sau lease takeover vì owner/revision/expiry fence.

## 4. Runner client

`RunnerProcessClient` giữ một long-lived process/session mỗi run trong pool bounded.
Nó pin executable SHA, clear/bound process I/O, strict UTF-8 single-line, kiểm expected
response phase/context/hash, và coi ACK+checkpoint là atomic client operation. Timeout,
EOF, oversized output, stderr overflow và process exit đều fail closed/cleanup tree.

## 5. Outbox và privacy

Outbox chỉ claim absolute per-run head khi operation tương ứng đã Applied. `FOR UPDATE
SKIP LOCKED` cho cross-run progress nhưng không skip một head chưa T3 trong cùng run.
Lease/attempt fence chặn stale completion; frontend dùng aggregate sequence để bỏ late
duplicate. Payload validator dùng exact allowlist, không phát route/node/raw budget/
certificate witness hoặc raw subject ID.

`commit_subject_links` là append-only authorization evidence. Audit API scope theo
member/operator, pseudonymous export fail closed, keyset `(sequence,id)` và rebuild hash
trên repeatable-read snapshot.

## 6. Rollout

Disabled không đăng ký worker. Shadow có thể chạy decision nhưng không tạo Live outbox.
Live mới publish. Artifact preflight chặn claim/member API nếu Runner pin không khỏe.
Namespace durable ngăn Shadow output bất ngờ phát sau restart/mode change.

## 7. Giới hạn claim

WP5 chứng minh same-input mechanical integration, recovery/concurrency và bounded local
curves. Nó không chứng minh production SLA hoặc C1 tốt hơn B1. Các test PostgreSQL thật
và paired bundle là correctness evidence, không phải confirmatory experiment.
