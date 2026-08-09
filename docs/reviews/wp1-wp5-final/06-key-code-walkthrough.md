# Walkthrough các block code quan trọng

Tài liệu này giải thích execution semantics; tên method/class trùng source để có
thể mở file và đối chiếu từng block.

## 1. Canonical input và hash

`CanonicalJson`/`CommitCanonicalJson` đi theo thứ tự:

1. parse strict JSON và yêu cầu đúng một root value;
2. object property được sắp `StringComparer.Ordinal`;
3. array giữ nguyên order vì order mang semantics;
4. number phải là integer trong JavaScript-safe range;
5. string encode strict UTF-8, invalid surrogate bị từ chối;
6. serialize không whitespace rồi hash bằng domain tag tương ứng.

Vì vậy hai request chỉ khác key order cho cùng canonical hash; hai array khác order
không bị nhập một. DB giữ cả `jsonb` để query và exact canonical bytes để replay/hash.

## 2. Event reduction

`EventReductionCoordinator` không mutate state rồi hy vọng rollback. Nó giữ local
`next`, gọi reducer lần lượt với exact event sequence, validate result, và chỉ trả
snapshot cuối khi toàn batch thành công. Exception giữa batch làm caller vẫn giữ
state cũ. RunnerSession chỉ advance cursor sau coordinator success.

## 3. Candidate generation và pruning

`InsertionCandidateGenerator` tạo search item theo admissible lower-bound key. Nó
expand insertion positions deterministic; `ForwardSlackProfile` cho biết remaining
delay budget để bỏ branch chắc chắn infeasible. Cache key chứa state/route/travel
version, nên snapshot mới không reuse slack cũ. Cap được ghi thành candidate loss,
không giả “không có candidate”. Mỗi candidate sống sót lại qua schedule và physical
validation trước policy.

## 4. Commitment publication

`CommitmentDecisionValidator.Validate` không đọc cờ từ solver:

```text
reproject candidate
→ PhysicalPlanValidator
→ PromiseProjector + three-way delta
→ hard lock evaluator
→ vector budget evaluator
→ normal certificate hoặc exact witness
```

Runner tạo pending decision và decision hash từ exact state/input/output/certificate.
State chưa đổi. Chỉ `decisionApplied` có đúng run/scenario/epoch/hash mới cho phép
checkpoint và commit state. Đây là two-step publication protocol, không phải một
boolean `accepted`.

## 5. CP-SAT lexicographic solve

`OrToolsCandidateSelectionSolver` dựng decision variable cho candidate và hard
one-per-request/vehicle constraints. Mỗi objective pass tối ưu một chiều, đọc optimum
rồi thêm equality khóa optimum trước pass kế. Cách này tránh phụ thuộc magic weight.
Nếu scaled dominance cần dùng, code tính bound bằng checked integer và từ chối
overflow. Output còn bị map về known candidate IDs và validator kiểm lại.

## 6. PostgreSQL T1

`PostgresCommitIntakeStore.AcceptAsync`:

1. mở transaction read committed;
2. `SELECT commit_runs ... FOR UPDATE`;
3. tìm exact idempotency scope/key;
4. cùng fingerprint → replay, khác fingerprint → conflict;
5. kiểm active state, contiguous epoch/event sequence, non-regressing sim time;
6. insert event + operation, advance run cursor/revision;
7. commit.

Partial unique active-run index là lớp cuối chống hai pending operation cùng run.

## 7. Claim và fence

Claim SQL chọn rows eligible + expired bằng `transaction_timestamp()`, lock với
`SKIP LOCKED`, rồi update owner/expiry/attempt/revision trong CTE. Lease trả exact
revision mới. T2/T3 khóa row lại và `RequireLease` so state, revision, owner, expiry
với database time. Worker cũ hết lease có response đúng vẫn không được commit.

## 8. Recovery và divergence

Worker load stored hello/init/previous checkpoint/event bytes. Fresh Runner phải
trả exact prior restore and pending decision. Nếu decision hash khác, worker không
ghi đè evidence; nó chuyển typed `Diverged`. Nếu pipe chết sau ACK nhưng trước T3,
fresh reconstruction gửi lại matching ACK và checkpoint; DB uniqueness/fence đảm
bảo chỉ một T3 thắng.

## 9. Applied-only outbox

T2 tạo outbox nhưng operation còn `DecisionPersisted`. `PostgresCommitOutboxStore`
join same-run operation và yêu cầu `state='Applied'`; do đó row không claim được.
Sau T3, earliest unpublished head/run mới eligible. Message payload identity được
validator đối chiếu lại với outer message/run/type/hash trước SendAsync.

## 10. Concurrent batch không phá per-run order

DB claim trả tối đa một head mỗi run. `ScopedConcurrentCommitOutboxBatchProcessor`
kiểm duplicate message/run rồi `Task.WhenAll` mỗi lease trong scope mới. Vì store/
DbContext độc lập, fast run có thể mark published khi slow run còn chờ network.
Không có hai lease cùng run trong batch nên concurrency không reorder aggregate.

## 11. Authorization và privacy rebuild

Member request IDs được lấy từ server Session aggregate, không từ query body. Store
map raw source IDs qua append-only subject links rồi filter pseudonymous timeline.
Operator rebuild đọc repeatable-read snapshot, rematerialize decision/effect chain,
so rebuilt hash với live projection. Export chỉ chạy nếu match và recursive scanner
không thấy forbidden raw/route/witness fields.

## 12. Paired replay fairness

Fixture loader kiểm raw + canonical hashes và exact difference allowlist. Executor
stage config bytes một lần, kiểm hash trước/sau process, chạy mỗi arm hai process
sạch. Input normalization chỉ bỏ policy ID/config hash và output-derived ACK hash
đã khai báo. Bất kỳ khác biệt khác là `WorkloadMismatch`, không được bỏ qua như
“noise”. Artifact enumerates tất cả file và assembly/source hash để tự verify.
