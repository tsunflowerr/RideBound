# WP5 distributed-integration evidence — 2026-08-05

> Phạm vi: cơ chế adapter, transaction, outbox, idempotency, worker lease,
> crash recovery và end-to-end replay của BeGo/RideBound.
> Cách đọc: các nguồn dưới đây hỗ trợ **mechanism**, không cung cấp budget,
> timeout, retention hay effectiveness threshold mặc định cho RideBound.

## 1. Phương pháp và nguồn

Các trang được mở và đọc trực tiếp bằng Codex in-app Browser ngày 2026-08-05.
Nguồn ưu tiên là paper gốc, tài liệu vendor chính thức và specification primary.
Hai paper hệ thống bổ sung (RIFL và Leases) được kiểm lại ngày 2026-08-09 trước
khi đóng recovery ticket; chúng bổ sung cơ chế, không thay đổi claim boundary.

| Nguồn | Điều được kiểm chứng | Ràng buộc đưa vào WP5 |
|---|---|---|
| Saltzer, Reed & Clark, [End-to-End Arguments in System Design](https://deepplum.com/Papers/EndtoEnd.html), 1984 | Duplicate suppression, crash recovery và acknowledgement hoàn chỉnh cần tri thức ở application endpoint; lower layer chỉ có thể là performance aid. | BeGo phải lưu request fingerprint/result và chỉ coi decision applied khi application-level ACK khớp; pipe/process reliability không thay thế end-to-end replay/hash check. |
| Lee et al., [Implementing Linearizability at Large Scale and Low Latency (RIFL)](https://doi.org/10.1145/2815400.2815416), SOSP 2015 | Mỗi RPC có định danh duy nhất; completion record giữ kết quả để retry nhận lại cùng outcome thay vì thực thi effect mới. Cơ chế còn cần quản lý vòng đời metadata, không biến transport thành exactly-once. | `commit_operations` giữ composite idempotency scope/fingerprint, stable operation ID và exact cached result; T2 effect IDs ổn định theo run/decision/action. Retry được gọi trung thực là durable duplicate suppression/idempotent effect. |
| Gray & Cheriton, [Leases: An Efficient Fault-Tolerant Mechanism for Distributed File Cache Consistency](https://doi.org/10.1145/74850.74870), SOSP 1989 | Quyền sở hữu có thời hạn giúp hệ thống tự phục hồi khi holder chết; duration/renewal là trade-off liveness/overhead và clock semantics phải rõ. Paper không tự cung cấp fencing cho side effects bên ngoài. | Lease BeGo dùng PostgreSQL `transaction_timestamp()` và ngắn, chỉ để reclaim/liveness. T2/T3 correctness bắt buộc thêm owner + monotonic revision fence; không giữ lease/row lock như bằng chứng ACK đã apply. |
| Helland, [Life Beyond Distributed Transactions](https://queue.acm.org/detail.cfm?id=3025012), 2007/2016 | Khi không có distributed transaction, at-least-once delivery, duplicate/out-of-order tolerance và durable per-partner activity state là cơ chế thực tế. | Mỗi `CommitRun` là serialization/transaction entity; event/decision/ACK state được persist theo run; retry phải idempotent và có state machine chứ không phải `try/catch` tạm thời. |
| Richardson, [Transactional Outbox](https://microservices.io/patterns/data/transactional-outbox.html) | Ghi business change và outbox trong cùng local transaction; relay có thể publish lặp lại sau crash nên consumer phải idempotent. | Decision/certificate/projection/outbox chung một EF transaction; SignalR relay là at-least-once và message có stable ID, không đánh dấu sent trước publish. |
| Microsoft, [EF Core transactions](https://learn.microsoft.com/en-us/ef/core/saving/transactions) | Một `SaveChanges` là atomic khi provider hỗ trợ; manual transaction/savepoint có giới hạn và phải được test với provider. | WP5 dùng explicit short transaction cho multi-step persistence, không giữ transaction khi gọi Runner/SignalR; integration test dùng Npgsql/PostgreSQL semantics. |
| Microsoft, [EF Core optimistic concurrency](https://learn.microsoft.com/en-us/ef/core/saving/concurrency) | Concurrency token làm update fail nếu row đã đổi; application phải resolve/retry với current database state. | Mutable run projection có application-managed revision token; append-only uniqueness vẫn là authority. Conflict không được ghi đè last-writer-wins. |
| PostgreSQL, [`SELECT ... FOR UPDATE ... SKIP LOCKED`](https://www.postgresql.org/docs/current/sql-select.html#SQL-FOR-UPDATE-SHARE) | `SKIP LOCKED` cho queue-like tables giảm tranh chấp nhưng cho inconsistent view và không phù hợp general-purpose query. | Chỉ worker claim dùng ordered `FOR UPDATE SKIP LOCKED`; audit/read model không dùng nó. Claim có lease expiry, owner và deterministic order. |
| IETF HTTPAPI WG, [Idempotency-Key draft-07](https://datatracker.ietf.org/doc/draft-ietf-httpapi-idempotency-key-header/) | Draft mô tả key + request fingerprint, replay completed result, conflict khi đang xử lý và reject khi cùng key khác payload. Tại ngày đọc, draft **expired/archived**, không phải RFC. | Dùng như prior-art, không claim standards compliance: composite scope `(actor, route, run, key)`, canonical payload SHA-256, cached response, 409 pending, 422 key/payload conflict. |
| Microsoft, [Background tasks with hosted services](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services) | Hosted service không có DI scope mặc định; shutdown có thể không gọi `StopAsync`; bounded channel tạo backpressure nhưng không phải durable queue. | Worker tạo scope cho mỗi claim; PostgreSQL là durable source, in-memory signal chỉ đánh thức; mọi state phục hồi được nếu process chết không graceful. |

## 2. Kết luận thiết kế

### 2.1. Không có “exactly once” xuyên DB, process và SignalR

WP5 chỉ có thể xây:

1. exactly-once **effect trong từng local database transaction** bằng unique key,
   fingerprint và optimistic concurrency;
2. at-least-once Runner retry và outbox delivery;
3. end-to-end duplicate suppression/replay ở BeGo bằng canonical hash;
4. deterministic reconstruction ở Runner bằng checkpoint + suffix replay.

Vì vậy không dùng tên/metric `exactlyOnceDelivery`. Evidence phải nói rõ
`idempotent effect under retry` hoặc `at-least-once delivery with deduplication`.

### 2.2. Transaction boundary

Không giữ PostgreSQL transaction mở qua child process hoặc SignalR. Luồng đúng:

```text
T1: idempotency + event append + work item commit
outside DB: claim lease -> call/recover Runner
T2: decision + certificate + rebuildable projection + outbox commit
outside DB: send matching decisionApplied to Runner
T3: ACK/checkpoint state commit
outside DB: outbox relay -> SignalR (retryable)
```

Crash tại mọi khe được phân loại bằng durable state. Không có bước nào dựa vào
memory flag để quyết định event đã apply.

### 2.3. Serialization theo run

Runner protocol yêu cầu epoch/event sequence liên tiếp và một pending decision.
Do đó một `CommitRun` chỉ có một work item active. Nhiều worker có thể xử lý
nhiều run, nhưng unique partial index + lease ngăn hai worker xử lý cùng run.
`SKIP LOCKED` chỉ là claim optimization; invariant được bảo vệ thêm bằng database
constraint/concurrency revision.

### 2.4. Idempotency không chỉ so key

Cùng key và cùng canonical request fingerprint trả lại stable result. Cùng key
khác fingerprint là conflict, không được trả result cũ. Request đang xử lý trả
pending/conflict có `operationId`; client query operation/decision thay vì gửi
key mới để né serialization.

Fingerprint loại header không semantic và bind ít nhất actor, route, run,
schema version, event envelope/payload canonical. Low-entropy key không được dùng
đơn lẻ làm lookup xuyên tenant/session.

### 2.5. Recovery là replay có chứng minh

Khi outcome của Runner call/ACK không chắc chắn, adapter không “đoán success”. Nó:

1. dừng/loại process handle không chắc chắn;
2. mở fresh Runner đúng binary hash;
3. hello + initialize cùng immutable manifest;
4. restore checkpoint cuối đã ACK;
5. replay suffix event/decisionApplied đã persist;
6. replay pending event và yêu cầu decision hash byte-exact với DB;
7. chỉ sau đó mới gửi/ghi matching ACK.

Hash lệch chuyển run sang `Diverged`, không auto retry tiếp và không publish như
normal decision.

RIFL giải thích vì sao operation ID/completion record phải durable; nó không cho
phép RideBound tuyên bố exactly-once qua PostgreSQL, child process và SignalR.
Leases giải thích cơ chế reclaim, nhưng lease expiry một mình không ngăn worker cũ
ghi muộn. Vì vậy WP5 kiểm `(state, owner, revision, expiry theo DB time)` ngay trong
T2/T3 transaction và so exact replay trước ACK.

## 3. Những “tối ưu” được phép áp dụng

- bounded worker concurrency theo run thay vì global single worker;
- ordered claim + `SKIP LOCKED` để giảm head-of-line blocking giữa các run;
- short local transaction, không giữ lock qua I/O;
- rebuildable projection/index cho audit query thay vì parse toàn JSON log;
- long-lived Runner mỗi active run, checkpoint theo policy explicit;
- in-memory wake-up channel chỉ giảm polling latency, không chứa authority;
- outbox batching có deterministic order và lease, nhưng consumer vẫn dedup.

Các tối ưu trên không được làm yếu event order, certificate, ACK hoặc hash gate.
Không chọn batch size, poll interval, lease duration, retention hay checkpoint
frequency từ paper; chúng là configuration có validation và phải đo ở ticket
performance/failure-injection.

## 4. Anti-pattern bị loại

- gọi Runner và SignalR trong một DB transaction dài;
- `SaveChanges` decision rồi gửi SignalR trực tiếp mà không outbox;
- coi pipe write/read thành bằng chứng action đã apply;
- retry POST bằng key mới;
- chỉ unique `Idempotency-Key` mà không bind actor/payload;
- worker queue chỉ ở `Channel<T>`;
- dùng `SKIP LOCKED` cho audit timeline;
- tự dựng ledger/certificate hoặc parse rồi “sửa” decision trong BeGo;
- spawn một Runner cho mỗi event trong happy path;
- xóa append-only evidence khi rollback feature flag.

## 5. Claim boundary

Paper và tài liệu systems ở đây chứng minh lý do chọn cơ chế, không chứng minh
RideBound giảm revision, đạt throughput production hay tốt hơn baseline. WP5
paired B1/C1 chỉ là Layer-1 mechanical/effect signal; effectiveness vẫn cần WP8
preregistration và WP9 confirmatory runs.

## 6. Bằng chứng đã áp dụng ở RB-WP5-008

Paper-to-code mapping không dừng ở tên pattern:

- RIFL → `CommitOperationEntity`/`PostgresCommitIntakeStore`: scoped key + semantic
  fingerprint, stable operation, exact cached HTTP bytes/hash và unique completion;
- Leases → hai claim SQL dùng database time, ordered `SKIP LOCKED`, owner/expiry và
  tăng revision; T2/T3 `RequireLease` kiểm lại toàn fence dưới row lock;
- End-to-End Argument → `CommitDecisionWorker` không coi pipe response là durable
  commit; uncertain ACK bỏ process, reconstruct fresh Runner và kiểm exact decision;
- Transactional Outbox → T2 ghi decision/certificate/projection/timeline/outbox
  trong một local transaction; relay `009` gọi SignalR at-least-once và chỉ mark
  sau send;
- independent semantic gate → BeGo materializer tái kiểm route, promise service
  order, 10-dimension vectors, certificate state/publication binding trước T2.

Real PostgreSQL 17 + published Runner chạy tám failpoint trước/sau Runner, T2, ACK
và T3; recovered bytes/hashes phải bằng clean replay oracle và không được tăng số
effect/outbox/timeline. Đây là correctness/recovery evidence, chưa phải throughput,
production SLA hay ridepooling-effectiveness evidence.

## 7. Bằng chứng đã áp dụng ở RB-WP5-009

- PostgreSQL chọn đúng unpublished head mỗi run bằng `DISTINCT ON`, sau đó
  ordered `FOR UPDATE SKIP LOCKED`; một run đang lease/backoff không mở message
  sau nhưng không chặn head của run khác.
- Claim tăng `attempt_count` dưới database-time lease và commit trước network I/O.
  Mark/reschedule khóa lại row và kiểm exact `(message, owner, attempt, expiry)`;
  stale owner không thể ghi completion muộn.
- Relay không giữ DB transaction qua SignalR, mark chỉ sau send, và failed send
  dùng exponential backoff có cap. Crash sau send tạo duplicate cùng immutable
  ID/payload/hash — đúng giới hạn outbox, không được gọi exactly-once.
- Source audit nhận ra lease không fence được external send đã bắt đầu: sender cũ
  có thể hoàn tất sau takeover. Do đó wire mang stable `aggregateSequence` ngoài
  exact nested payload; frontend giữ per-run monotonic cursor và recent ID set để
  bỏ duplicate/stale callback. Cơ chế bounded in-memory này không thay durable
  catch-up sau disconnect; `RB-WP5-010` phải cung cấp timeline cursor.
- Exact payload allowlist loại route/node/budget/certificate witness/raw identity;
  canonical Session group dùng GUID normalized và hub chỉ cho authenticated member
  join. Migration trigger cấm retarget non-null `session_id` sau create.
- Real PostgreSQL kiểm crash/reclaim/stale fence, per-run order, cross-run
  non-blocking, retry backoff, no-audience row và T2 rollback không để outbox.
  Published Runner gate cùng full BeGo Debug/Release đạt 131/131, 0 skip.

SignalR `SendAsync` chỉ là server invocation/enqueue evidence. Nó không phải ACK
từ từng browser và client offline có thể bỏ lỡ live event. Vì vậy claim chính xác
là **at-least-once relay invocation with stable dedup/order metadata**, kết hợp
rebuildable timeline đã được hiện thực ở `RB-WP5-010`.

## 8. Bằng chứng đã áp dụng ở RB-WP5-010

- Audit read được tách khỏi worker claim: không dùng `SKIP LOCKED`; exact
  `(sequence,id)` row-value keyset cùng stable tie-breaker và composite index được
  kiểm bằng PostgreSQL `EXPLAIN` trên 12.000 dòng.
- End-to-end boundary không tin payload chỉ vì đã committed: JSONB được
  canonicalize, kiểm lại SHA-256 và exact user-safe allowlist trước khi trả. Member
  scope được server suy từ ownership; cross-request access fail closed.
- Raw decision/certificate evidence chỉ ở operator endpoint default-deny. Timeline
  member không chứa raw subject, token, route, coordinate, witness hoặc budget.
- Rebuild dùng repeatable-read snapshot từ append-only decision/certificate/
  operation, kiểm contiguous epoch, previous-decision hash, state chain, exact
  materializer/certificate rồi so canonical rebuilt/live hashes. Drift chặn export.
- Pseudonymous export có recursive forbidden-field guard; telemetry/logging chỉ
  dùng stable run/access/count/hash metadata và privacy mutation tests.
- Migration downgrade guard chạy trước mọi `DROP`; rollback bị từ chối giữ nguyên
  schema/evidence thay vì tạo partial downgrade.

Real PostgreSQL concurrent pagination, raw authorization, rebuild/mutation,
migration up/down/re-up và indexed-plan gates pass; published Runner full Debug và
Release đạt 138/138, 0 skip. Evidence này đóng cơ chế correctness/rebuildability/
privacy của `010`; không biến SignalR thành durable receipt, không chứng minh
exactly-once, production throughput/SLA hoặc ridepooling effectiveness.

## 9. Bằng chứng đã áp dụng ở RB-WP5-011

- Default-off là composition-root property: không đăng ký COMMIT hosted worker và
  member boundary fail closed trước khi resolve Runner. Operator append-log audit
  vẫn độc lập để rollback không che evidence.
- Exact artifact hash preflight không spawn process; unhealthy state chặn claim.
  Runner client vẫn hash lại trước `Process.Start`, giữ end-to-end provenance gate.
- Shadow/Live namespace được persist immutable. Decision/ACK claim lọc namespace;
  outbox SQL chỉ claim Live. Đây là phần cần thiết để live restart không phát shadow
  backlog — một in-memory publisher flag không bảo đảm được điều đó.
- Disable/cancel không thu hồi giả tạo external effect; nó ngừng cycle mới và để
  lease expire, sau đó exact owner/revision fence cho same-namespace reclaim.
- Migration backfill existing run thành Shadow, bỏ default sau backfill và guarded
  Down trước drop. PostgreSQL mutation xác nhận failed rollback giữ column/index/
  trigger cùng append evidence.

Full Debug/Release trên fresh PostgreSQL + published Runner đạt 147/147, 0 skip;
frontend/required RideBound/dependency gates sạch. Đây chỉ là rollout/recovery/
compatibility correctness, chưa phải paired B1/C1 hoặc effectiveness evidence.

## 10. Bằng chứng đã áp dụng ở RB-WP5-012

- B1 `rolling-cost` và C1 `ridebound-hard-vector` đều chạy cùng commitment-mode
  Runner path để certificate/publication validation không trở thành confound.
- Source manifest bind Runner SHA/core commit, raw/canonical workload, explicit
  pseudonymous provenance, common policy catalog và config mỗi arm. Normalize config
  bỏ duy nhất `/policyId`; candidate/solver/work budgets phải exact.
- Effective configuration hash domain-separate bind policy catalog + arm config;
  Runner kiểm lại ở initialize. Validated bytes được stage và hash lại trước/sau
  từng process để ngăn preflight/use TOCTOU.
- B1×2/C1×2 clean process có cùng normalized protocol input. Mỗi arm lặp exact
  input/output/decision/certificate/checkpoint hashes, exit 0, stderr rỗng.
- BeGo exact materializer kiểm full certificate/action/state binding; independent
  checkpoint validator tính lại content hash, manifest/state/decision/cursor chain.
- Artifact manifest enumerate mọi file với bytes/SHA, exact sidecar và reject
  missing/extra/tamper. Bundle snapshot harness source + executing assembly hashes,
  đồng thời có explicit empty failure/exclusion log.

Final manifest là
`b843bd20cbe9bf887d00998d4eaad54258848eb41d87ae49fd18a2142a0cb807`;
BeGo Debug/Release đạt 152/152 và RideBound 557/557. Bằng chứng này chỉ đóng paired
mechanical/correctness gate; không phải kết quả effectiveness, SLA hay confirmatory.

## 11. Bằng chứng độc lập đã áp dụng ở RB-WP5-013

Ngày 2026-08-09, in-app Browser kiểm tra lại năm nguồn primary cho phương pháp
evidence: [LDFI](https://doi.org/10.1145/2723372.2723711),
[Elle](https://doi.org/10.14778/3430915.3430918),
[QuickCheck](https://doi.org/10.1145/351240.351266),
[mutation testing ban đầu](https://doi.org/10.1109/C-M.1978.218136) và
[rigorous performance evaluation](https://doi.org/10.1145/1297027.1297033).
Các paper này được dùng theo cơ chế hẹp sau:

- fault injection đi qua toàn bộ 8 decision + 4 outbox durable boundary đã xác
  định, ở executable riêng và hard-kill thật; không giả hard crash bằng exception
  trong test process và không claim đã chạy LDFI/SAT;
- expected state/claim history do oracle test-owned dựng từ observed operation,
  không gọi production transition table; không claim chạy Elle hay chứng minh
  isolation level tổng quát;
- 256 seeded history, mỗi history 64 bước, lưu exact trace và outcome để tái hiện;
  không claim QuickCheck shrinking hoặc exhaustive state space;
- năm mutant được chọn từ năm correctness boundary độc lập và đều bị phát hiện.
  Không có external mutation runner nên kết quả chỉ là `5/5 required mutants`,
  không phải mutation score;
- performance chạy deterministic randomized order, một warm-up và năm measured
  repetition cho từng queue `8/32/64 × worker 1/2/4`, giữ raw samples, CPU/OS/.NET/
  PostgreSQL và append-row counts. Không assert latency threshold hay SLA.

Test còn chứng minh exact-set claim dưới 2/3/4 PostgreSQL worker, stale owner/revision
fence, fresh-Runner decision/certificate/checkpoint equality, stable at-least-once
outbox duplicate semantics và bounded cleanup của process/session/DB connection.
Artifact final:

```text
path: E:\Code\BeGo\artifacts\ridebound\wp5-independent-v1\wp5-013-20260809-final
manifest sha256: e21fb0877fbc6d61bf6f1e24adcda24e09a29fea95a9f44d1b61bf4fc1061ca2
transition histories: 256 × 64 = 16,384 steps
accepted / rejected: 12,261 / 4,123
worker contention: 2 / 3 / 4 workers, no lost or duplicate claim
abrupt crash cases: 8 decision + 4 outbox
required mutants killed: 5 / 5
failure / exclusion rows: 0 / 0
Runner active sessions after gate: 0
PostgreSQL connection baseline: 1 -> 1
```

Median claim-drain observations trên máy audit lần lượt là `5.553/7.130/7.246 ms`
cho queue 8 với 1/2/4 worker, `8.848/8.822/7.867 ms` cho queue 32 và
`10.920/10.957/8.938 ms` cho queue 64. Worker coordination tạo overhead ở queue
nhỏ; bốn worker cho tín hiệu tốt hơn ở queue 32/64. Đây là descriptive local curve,
không phải end-to-end Runner/SignalR throughput hoặc policy-effectiveness result.

Gate final dùng fresh PostgreSQL 17 và published Runner thật: BeGo Debug 153/153,
BeGo Release `/p:TreatWarningsAsErrors=true` 153/153, đều 0 skip; RideBound required
suite 557/557. Artifact verifier độc lập rehash 18 file và sidecar/manifest đều khớp.

## 12. Closure evidence áp dụng ở RB-WP5-014

Full source audit phát hiện ba gap chưa được test count WP5-013 diễn đạt:

- authorization phụ thuộc `commit_subject_links`, do đó bảng này phải append-only
  như timeline/effect/outbox evidence;
- outbox T2 chưa được publication-eligible trước khi T3 chuyển operation sang
  `Applied`; schema bắt buộc operation ID, query chọn absolute head trước rồi kiểm
  exact same-run state để row phía sau không vượt head chưa T3;
- per-run-head SQL chưa đủ chống head-of-line blocking nếu một application cycle
  dùng cùng scope để gửi tuần tự; mỗi run nay dùng async scope/DbContext độc lập.

Migration và real PostgreSQL regression kiểm immutability, non-null, pre/post-T3
claim cùng guarded rollback. Coordinated relay regression chứng minh run nhanh được
mark published trong khi publisher run chậm vẫn đang bị giữ. Final BeGo gate trên
hai fresh PostgreSQL 17 database và published Runner đạt Debug 154/154, Release
`/p:TreatWarningsAsErrors=true` 154/154, đều 0 skip. Frontend 9/9, lint, TypeScript,
production build; format và vulnerability audits sạch. Required RideBound suite
vẫn 557/557.

Review [WP1–WP5 final](../reviews/wp1-wp5-final/README.md) kết luận GO chỉ cho
refinement common benchmark harness. Evidence hiện tại không chứng minh external
exactly-once delivery, production SLA, dataset-scale performance, policy
effectiveness, non-inferiority hoặc user satisfaction.
