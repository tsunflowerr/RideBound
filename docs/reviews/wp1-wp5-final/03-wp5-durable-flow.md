# WP5 durable integration: từ HTTP tới publication

## 1. Bootstrap và create-run

`CommitApiService.CreateRunAsync` xác thực thành viên/host, hash actor, tạo semantic
idempotency fingerprint rồi snapshot Session/venue trước external I/O.
`CommitBootstrapMapper`:

1. kiểm unique member/request và named profile;
2. pseudonymize ID theo HMAC key riêng mỗi run;
3. đổi tọa độ E7 và thời gian ms bằng ties-to-even;
4. dựng directed complete travel matrix, fail missing/unreachable/sentinel;
5. ghi field provenance, manifest, hello/init/event bytes và hashes;
6. zeroize pseudonym key.

Runner hello/init được thực thi trước DB insert. `PostgresCommitApiStore.CreateRunAsync`
dùng advisory idempotency lock + capacity check rồi commit run, bootstrap evidence,
subject links, event và operation atomically.

## 2. T1 — intake và claim

Append request không được tự gửi epoch/sequence; server đọc current run cursor và
`AcceptAsync` khóa run row. Transaction kiểm run active, exact next epoch/sequence,
simulation time monotonic và active-operation uniqueness, sau đó append event/work.

Claim dùng `FOR UPDATE SKIP LOCKED`, database `transaction_timestamp()`, bounded
limit và immutable rollout namespace. Lease fence gồm owner + expected revision +
expiry; process clock không quyết định durable ownership.

## 3. Runner work ngoài transaction

`CommitDecisionWorker` load exact recovery bundle, khởi tạo hoặc reconstruct fresh
Runner bằng stored hello/init, previous checkpoint và event. `RunnerProcessClient`
hash DLL trước mỗi session, giới hạn process count/line/stderr/time, serialize I/O
theo run và kill cả process tree khi timeout/protocol error.

Decision response được `CommitDecisionMaterializer` parse lại: exact action set,
promise order, route/vector/witness/certificate body và stable user-safe effect ID.
Adapter không reimplement RideBound decision-hash formula; nó bind exact Runner
artifact/response và độc lập kiểm các cross-field/certificate/checkpoint invariants.

## 4. T2 — durable pending decision

Dưới operation + run row lock và lease fence, store rematerialize exact response.
Một DB transaction ghi:

- decision canonical bytes/hash + input/output state hash;
- certificate canonical bytes/hash;
- request projection;
- user-safe timeline;
- outbox message có stable ID, sequence, exact payload bytes/hash và mandatory
  same-run `operation_id`;
- operation → `DecisionPersisted`.

Nếu bất kỳ insert/constraint/failpoint lỗi, không có partial projection/outbox.

## 5. ACK và T3

ACK worker claim decision/certificate exact. Khi process outcome uncertain, worker
abandon process cũ, reconstruct fresh Runner tới pending decision, so decision hash,
rồi gửi canonical `decisionApplied` + checkpoint trong cùng Runner I/O critical
section. `CommitCheckpointValidator` tính lại checkpoint domain hash, manifest,
state, epoch, cursor và sim-time binding.

T3 chỉ commit checkpoint/cached HTTP result và operation → `Applied` dưới cùng
owner/revision/expiry fence. Mismatch tạo typed `Diverged`; không manufacture ACK.

## 6. Outbox publication fence và concurrency

WP5-014 thêm boundary còn thiếu. Query trước hết chọn absolute earliest unpublished
outbox theo `(run, aggregate_sequence, id)`, sau đó candidates mới join operation:

```sql
JOIN commit_operations operation
  ON operation.run_id = outbox.run_id
 AND operation.id = outbox.operation_id
WHERE operation.state = 'Applied'
```

Do đó effect T2 chưa ACK không thể ra SignalR và row `Applied` phía sau cũng không
được vượt head chưa T3. Schema mới `SET NOT NULL` `operation_id`; migration fail
closed nếu legacy row chưa repair.

Query chỉ claim earliest unpublished head mỗi run, dùng `SKIP LOCKED`, DB-time lease,
stable attempt fence và Live namespace. I/O không giữ DB row lock. Batch processor
chạy mỗi khác-run lease trong scope/DbContext riêng; slow run không giữ lease khác
run chờ tuần tự. Cùng run vẫn không song song vì claim trả tối đa một head/run.

Send thành công mới mark published; lỗi reschedule exponential bounded. Crash sau
send có thể gửi lại cùng message ID/payload/hash. Frontend gate bỏ duplicate/stale
bounded; durable catch-up là timeline, không phải claim exactly-once client receipt.

## 7. Audit/privacy/rollout

Member timeline chỉ map pickup request thật của chính member qua restricted
subject link sang pseudonym. WP5-014 làm subject link append-only để authorization
không thể bị retarget/delete sau bootstrap. Operator policy riêng mới được rebuild/
export raw evidence. Rebuild chạy repeatable-read, tính append-log/projection hash
và fail nếu live projection drift; export có recursive forbidden-field guard.

Rollout mặc định `Disabled`; `Shadow` chỉ decision worker, `Live` thêm outbox.
Preflight hash mismatch/config invalid ngăn API/worker claim. Namespace persisted
immutable nên shadow backlog không thể vô tình được live relay publish sau restart.
