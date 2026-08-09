# Từ paper tới quyết định thiết kế

## 1. Cách đọc bảng

Paper không được liệt kê để “trang trí”. Mỗi nguồn phải làm ít nhất một việc:

- chỉ ra phần đã cũ;
- tạo constraint/baseline;
- cung cấp simulator/data;
- gợi ý metric;
- giới hạn claim.

## 2. Bằng chứng trực tiếp

| Nguồn | Phát hiện dùng cho RideBound | Quyết định |
|---|---|---|
| [Ackermann & Rieck 2025, Multiple-plan dynamic DARP](https://doi.org/10.1007/s00291-025-00809-y) | Dynamic requests, quick accept/reject, plan pool, least-commitment/time consensus | B5; không claim insertion/consensus mới |
| [Tellez et al., Time-Consistent DARP](https://doi.org/10.1002/net.22063) | Consistency qua nhiều ngày/time classes, epsilon trade-off | Phân biệt intra-trip revision history với multi-day consistency |
| [Unreliability in ridesharing, 2020](https://www.sciencedirect.com/science/article/pii/S0968090X2030735X) | Initial information có thể khác execution, bất ổn là vấn đề thật | Đo first promise, visible revision và realized error |
| [Anticipatory walking, TR-C 2026](https://www.sciencedirect.com/science/article/pii/S0968090X26001336) | Reference/no-worse protection khi đổi drop-off | Không claim one-step no-worse mới; giữ full ledger scope |
| [Alonso-Mora et al., PNAS 2017](https://doi.org/10.1073/pnas.1611675114) | Request-trip-vehicle batch assignment | Candidate/assignment baseline nền |
| [Optimal Online Dispatch, ICRA 2021](https://www.cs.bham.ac.uk/~parkerdx/papers/icra21samod.pdf) | OSP, complete feasible schedules trong AMoD2 | AMoD2 native sanity baseline |
| [FleetPy, 2026](https://doi.org/10.1186/s12544-026-00823-3) | Modular/reproducible MoD comparisons, benchmark data | Chọn Layer 2 |
| [RidePy, JOSS 2024](https://doi.org/10.21105/joss.06241) | Modular fleet/vehicle/dispatcher/event analytics | Chọn Layer 3 mặc định |

## 3. Patent/product evidence

| Nguồn | Điều đã tồn tại | Claim bị loại |
|---|---|---|
| [US11754407B2](https://patents.google.com/patent/US11754407B2/en) | ETA update, added rider, max extra time/distance | Per-rider delay threshold |
| [US11674811B2](https://patents.google.com/patent/US11674811B2/en) | Reassignment theo threshold, notification, consent/incentive | Reassignment/consent đơn lẻ |
| Public ridepool descriptions | Route cập nhật sau khi thêm rider | Dynamic route update |

## 4. Evidence dẫn tới metric

| Khái niệm | Metric |
|---|---|
| Lịch sử khác dù final plan giống | cumulative total variation, switch count |
| Bất ổn tập trung ở một số rider | p95/p99/max, fraction `>=3` revisions |
| Traffic ngoài kiểm soát | exogenous/decision/visible decomposition |
| Efficiency trade-off | service rate, wait, detour, VHT/VMT |
| Online feasibility | latency/timeout/fallback |
| Auditability | violation/certificate/witness rate |

## 5. Evidence dẫn tới baseline

- Dynamic rolling routing → B1.
- Penalty route stability → B2.
- Near-term locks/frozen decisions → B3.
- Assignment stability → B4.
- Least-commitment/multiple plan → B5.
- Current BeGo static → B0 context only.

## 6. Corpus trong repo

Research trước đó đã tạo:

- [bego-90-paper-evidence-matrix.md](research/bego-90-paper-evidence-matrix.md);
- [bego-90-paper-review-and-8-topics.md](research/bego-90-paper-review-and-8-topics.md);
- [giai-thich-9-de-tai-bego-de-lua-chon.md](research/giai-thich-9-de-tai-bego-de-lua-chon.md);
- [audit-t3-t9-va-de-tai-thay-the-2026.md](research/audit-t3-t9-va-de-tai-thay-the-2026.md);
- [bego-public-data-targeted-paper-evidence.md](research/bego-public-data-targeted-paper-evidence.md).

Research record mô tả corpus 180 paper toàn văn sau các vòng 90 + 80 + novelty audit. Thư mục raw có thêm duplicate/rejected/replacement; không lấy số file PDF thô làm số paper hợp lệ.

RideBound không cần trích cả 180 paper như nhau. Nhóm trực tiếp ở mục 2 quyết định claim; corpus rộng dùng cho context và kiểm tra hướng thay thế.

## 7. Quy trình cập nhật evidence

Trước pilot và trước submission:

1. Tìm từ khóa chính xác:
   - ridepool promise stability;
   - post-acceptance revision;
   - cumulative ETA revision budget;
   - schedule churn passenger;
   - path-dependent commitment routing.
2. Ưu tiên publisher/conference/repo chính thức.
3. Đọc full text nguồn va chạm.
4. Ghi phần trùng, phần khác, impact lên claim.
5. Nếu claim thay đổi, cập nhật `01`, `03`, `18`, `19`.

## 8. Quy tắc citation

- Dùng DOI/publisher/repo chính thức.
- Ghi đúng version/date cho software.
- Patent/web product là evidence khác paper phản biện.
- Không nói “180 paper đều ủng hộ RideBound”.
- Không dùng abstract để khẳng định paper không có một cơ chế; với va chạm trực tiếp phải đọc full text.

## 9. Recheck phục vụ ADR-021 — 2026-07-30

Full text cục bộ được đọc lại cho ba nguồn liên quan trực tiếp:

- Multiple-plan dynamic DARP: pool nhiều plan, quick accept/reject, consensus và
  least-commitment/time-to-first-difference đã tồn tại; RideBound không claim các
  cơ chế này, chỉ dùng làm baseline/claim boundary.
- Time-consistent DARP: consistency bằng time classes, lexicographic refinement và
  cost-consistency Pareto trade-off đã tồn tại trong multi-period static planning;
  RideBound giữ khác biệt intra-trip, path-dependent revision ledger.
- Forward-looking dynamic ride-pooling dispatch: rolling insertion/matching,
  future opportunity và detour safeguard đã có; không dùng “future-aware” hay
  one-step no-worse làm novelty.

Ảnh trang đầu của hai paper va chạm chính và text abstract/model/conclusion đã
được đối chiếu. Kho hiện không có DOCX. Audit bổ sung ngày 2026-08-02 đã dùng
Browser trong ứng dụng để kiểm tra lại nguồn publisher/DOI; kết quả nằm ở mục 10.
Quyết định implementation tương ứng nằm trong ADR-021/ADR-022 và execution plan
`28`/`29`.

## 10. Browser recheck phục vụ đóng WP3 — 2026-08-02

| Nguồn bổ sung | Kết quả đọc | Tác động thực thi |
|---|---|---|
| [Gaul, Klamroth & Stiglmayr 2021](https://doi.org/10.4230/OASIcs.ATMOS.2021.8) | Rolling-horizon event-based MILP; thực nghiệm báo 99,5% insertion tối ưu đối với schedule hiện tại trong giới hạn 30 giây, trung bình 2,8 giây | Xác nhận B1 exhaustive-small là oracle correctness, không phải scale claim; WP4 cần deadline/performance evidence và bounded generation |
| [Schulz & Pfeiffer 2026](https://doi.org/10.1007/s00291-026-00847-0) | Immediate response, relative detour acceptance, forward slack, tái sử dụng feasible reinsertion và future potential | Đưa slack/precomputation/caching sang WP4; không sao chép khuyến nghị horizon 10–15 phút thành default vì phụ thuộc instance; không mở reassignment O-001 |
| [Geržinič et al. 2023](https://doi.org/10.1016/j.tbs.2023.100616) | Survey 936 người cho thấy unexpected wait, bất đối xứng sớm/muộn, cancellation và trải nghiệm gần nhất đều quan trọng | Chỉ dùng làm động lực cho history/material-revision; không suy ra budget số hay “user satisfaction” từ survey |
| [Tiwari, Nassir & Lavieri 2024](https://www.mdpi.com/2071-1050/16/13/5788/html) | Review phân loại weighted-sum, Pareto và lexicographic objectives trong ridepooling | Giữ hard vector gate tách khỏi objective; WP4 ưu tiên lexicographic/Pareto có thể audit, không dùng trọng số tùy ý để che vi phạm |
| [Ackermann & Rieck 2025](https://link.springer.com/article/10.1007/s00291-025-00809-y) | Multiple-plan pool, insertion rồi idle-time improvement; thêm tối ưu có thể làm giảm flexibility, một số cơ chế remove/reinsert không luôn có lợi ở mức động cao | B5/multiple-plan và distinguished plan thuộc WP4; phải đo candidate loss/flexibility, không mặc định “tối ưu lâu hơn luôn tốt hơn” |

Kết luận thiết kế: WP3 đúng khi chỉ làm **cổng khả thi cam kết độc lập** và bằng
chứng ledger/certificate. Các kỹ thuật plan pool, slack, precomputation, modified
dynamic wait và lexicographic objective là tối ưu policy/solver của WP4; đưa chúng
vào validator WP3 sẽ trộn objective với correctness và làm hỏng so sánh B1/C1.

## 11. Browser research phục vụ ADR-023 — 2026-08-03

| Nguồn đọc thêm | Bằng chứng dùng | Quyết định WP4 |
|---|---|---|
| [Mitrović-Minić & Laporte 2004](https://doi.org/10.1016/j.trb.2003.09.002) | Drive-first, wait-first, dynamic và advanced dynamic waiting cho thấy vị trí phân bổ waiting time ảnh hưởng chất lượng online | `earliest-feasible` giữ làm main; thêm named origin-hold control thi hành bằng route waypoint, không claim waiting mới |
| [Masson, Lehuédé & Péton 2013](https://doi.org/10.1016/j.orl.2013.01.007) | Forward-time-slack cho incremental insertion feasibility; preprocessing được cập nhật khi route đổi | Slack/cache chỉ early-prune với full key/invalidation; independent full validator vẫn bắt buộc |
| [Gschwind 2019](https://doi.org/10.1007/s00291-018-0544-0) | Forward slack là công cụ tổng quát cho feasibility testing các insertion có temporal/synchronization constraints | Thêm cached/uncached equivalence và route/travel mutation; không giả slack đơn giản là proof cho toàn DARP |
| [Ackermann & Rieck 2022](https://doi.org/10.1007/978-3-031-08623-6_42) | Distance guidance có thể lệch mục tiêu acceptance; future insertion potential là secondary guidance đã có | B5/plan diversity và slack reserve là baseline guidance, không novelty; phải đo future acceptance thay vì chỉ current cost |
| [Google.OrTools 9.15.6755](https://www.nuget.org/packages/Google.OrTools/9.15.6755) và [CP-SAT status](https://developers.google.com/optimization/cp/cp_solver) | Package target .NET 8+; CP-SAT integer-only, status phân biệt OPTIMAL/FEASIBLE/INFEASIBLE/MODEL_INVALID/UNKNOWN | Pin package trong solver project; one worker/seed/deterministic limit; không báo FEASIBLE/UNKNOWN thành OPTIMAL |

Browser cũng đọc lại full HTML/abstract của Gaul 2021, Schulz–Pfeiffer 2026,
Tiwari et al. 2024 và Ackermann–Rieck 2025. ADR-023 không sao chép 30 giây,
10–15 phút, pool size, survey coefficient hoặc paper weight thành default. Mọi
con số test WP4 phải ghi `boundary-test`/`microbenchmark`; O-002/O-003/O-004 vẫn
thuộc pilot/preregistration.

## 12. Implementation audit sau WP4 — 2026-08-03

Các mechanism từ mục 10–11 đã thành code/test, không còn chỉ là backlog:

- forward slack, full-key cache/invalidation và executable origin-hold;
- deterministic best-first generation với exact/bounded loss accounting;
- B4 same-vehicle remove/reinsert và B5 canonical plan pool/consensus;
- C1/C2 ordered hard/warning/revision objectives không scalarize;
- pinned deterministic multi-pass OR-Tools với truthful status/bound/gap;
- independent validation/fallback và Runner certificate/hash/ACK publication.

Evidence closure gồm 64-seed B1 oracle, 64-seed production C1 + actual OR-Tools
differential gap 0, hard-gate mutation, actual bounded-loss propagation và
synthetic microbenchmark. Kết quả timing chỉ được gọi là machine-local promising
signal. Không paper nào ở trên chứng minh RideBound scale, tăng acceptance, đạt
non-inferiority hoặc tăng user satisfaction trên paired demand; các claim đó vẫn
thuộc WP5–WP9. Mapping chi tiết ở
[reviews/wp1-wp4-final/07-paper-to-code-audit.md](reviews/wp1-wp4-final/07-paper-to-code-audit.md).

## 13. Browser research phục vụ ADR-025/WP5 — 2026-08-05

| Nguồn | Bằng chứng dùng | Quyết định WP5 |
|---|---|---|
| [Saltzer, Reed & Clark 1984](https://deepplum.com/Papers/EndtoEnd.html) | Duplicate suppression, crash recovery và acknowledgement hoàn chỉnh cần application endpoint knowledge; lower-level reliability chỉ hỗ trợ performance | Persist fingerprint/result và matching application ACK; process pipe không thay end-to-end checkpoint/replay/hash proof |
| [Lee et al. 2015 — RIFL](https://doi.org/10.1145/2815400.2815416) | Unique request IDs + durable completion records cho retry nhận lại cùng outcome; không tự biến transport thành exactly-once | Stable scoped operation/message IDs, semantic fingerprint và exact cached result; claim chỉ `idempotent effect under retry` |
| [Gray & Cheriton 1989 — Leases](https://doi.org/10.1145/74850.74870) | Time-bounded ownership cho reclaim/liveness; lease duration là trade-off và lease đơn lẻ không fence late side effect | PostgreSQL DB-time lease để reclaim; T2/T3 còn bắt buộc owner + monotonic revision fence và exact reconstruction |
| [Helland 2007/2016](https://queue.acm.org/detail.cfm?id=3025012) | Local transaction entity, at-least-once retry/out-of-order tolerance và durable activity state khi không dùng distributed transaction | Serialize mỗi `CommitRun`, durable operation state, idempotent retry; không cố transaction xuyên DB/Runner/SignalR |
| [Transactional Outbox](https://microservices.io/patterns/data/transactional-outbox.html) | Business state + message cùng local transaction; relay có thể publish lặp sau crash | Decision/certificate/projection/outbox atomic; SignalR wire giữ stable message ID, aggregate sequence, payload hash và consumer dedup |
| [EF Core transaction](https://learn.microsoft.com/en-us/ef/core/saving/transactions) và [concurrency](https://learn.microsoft.com/en-us/ef/core/saving/concurrency) | `SaveChanges` atomic theo provider; savepoint/retry/concurrency token cần application handling và provider tests | Short explicit T1/T2/T3, application-managed run revision, real PostgreSQL integration gate |
| [PostgreSQL locking clause](https://www.postgresql.org/docs/current/sql-select.html#SQL-FOR-UPDATE-SHARE) | `SKIP LOCKED` phù hợp queue-like consumer nhưng cho inconsistent view | Chỉ claim worker dùng ordered `SKIP LOCKED`; audit query không dùng; invariant còn có lease/unique/concurrency guard |
| [IETF Idempotency-Key draft-07](https://datatracker.ietf.org/doc/draft-ietf-httpapi-idempotency-key-header/) | Key + fingerprint, completed replay, in-flight conflict và changed-payload error | Dùng như prior art cho composite scope/fingerprint/cached response. Draft đã expired/archived, không claim RFC compliance |
| [ASP.NET hosted services](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services) | Background service cần tự tạo scope; ungraceful shutdown có thể không gọi `StopAsync`; bounded channel chỉ tạo backpressure | PostgreSQL là durable authority, channel chỉ wake-up; worker scoped per claim và mọi state recovery được sau hard crash |

Đây là systems-correctness evidence, không phải ridepooling-effectiveness paper.
Nó cho phép tối ưu claim/worker/outbox/recovery nhưng không cung cấp lease duration,
batch size, polling interval, retention, checkpoint frequency hay production SLA.
Chi tiết ở
[wp5-distributed-integration-evidence-2026-08-05.md](research/wp5-distributed-integration-evidence-2026-08-05.md).

`RB-WP5-009` còn áp dụng một giới hạn quan trọng của lease paper: lease không thể
fence một SignalR side effect đã bắt đầu. Server vì vậy chỉ mở message kế tiếp sau
mark của per-run head, nhưng wire vẫn mang `aggregateSequence`; frontend bỏ
duplicate/stale completion nếu sender cũ hoàn tất sau takeover. Đây là monotonic
live callback trong connection hiện tại, không phải durable client receipt;
timeline catch-up của `RB-WP5-010` xử lý disconnect/gap ở durable query boundary.

### 14. Cơ chế paper đã được kiểm ở RB-WP5-010

`RB-WP5-010` không thêm novelty claim; nó kiểm xem các cơ chế systems đã chọn có
được áp dụng đến end-to-end boundary thay vì chỉ xuất hiện dưới dạng tên pattern:

- End-to-End Argument: SignalR enqueue không phải durable receipt. Client lấy lại
  gap qua timeline cursor từ committed store, và server vẫn tái kiểm canonical
  payload/hash/authorization tại điểm trả dữ liệu;
- Transactional Outbox/Helland: projection/timeline là dữ liệu dẫn xuất có thể
  rebuild; source of truth vẫn là append-only decision/certificate/operation. Rebuild
  dùng snapshot nhất quán, kiểm chain và so content hash với live view;
- RIFL/lease boundary: audit read không phải worker claim nên không dùng
  `SKIP LOCKED`; production pagination dùng deterministic row-value keyset với
  stable tie-breaker, còn raw evidence nằm sau operator policy mặc định deny;
- least privilege/privacy: member scope được suy server-side từ ownership; raw
  subject link chỉ dùng cho authorization join. Export/log/metric chỉ mang
  pseudonymous, allowlisted evidence và fail closed khi projection drift;
- rollback/recoverability: migration guard phải chạy trước destructive downgrade;
  append-only evidence không bị feature rollback hoặc projection rebuild xóa.

Bằng chứng gồm concurrent append pagination, cross-request denial, canonical JSONB
hash mutation, append-log rebuild/live-drift mutation, recursive privacy mutation
và `EXPLAIN` trên 12.000 dòng. Kết quả chỉ hỗ trợ correctness/rebuildability/privacy,
không hỗ trợ throughput SLA, effectiveness, novelty hay exactly-once delivery.

### 15. Cơ chế systems đã được kiểm ở RB-WP5-011

Ticket rollout không thêm paper/novelty claim mới; nó áp dụng tiếp các giới hạn đã
khóa từ End-to-End Argument, leases và transactional outbox:

- preflight hash ở application boundary chỉ cho phép claim sau khi exact artifact
  provenance đã khớp; Runner vẫn tự hash/handshake lại trước process I/O;
- lease là recovery/liveness, không phải cờ enable. Disable ngừng claim mới; lease
  đã committed được để expire rồi owner/revision fence quyết định reclaim;
- shadow no-publish phải là thuộc tính durable. Persist namespace + live-only outbox
  query ngăn live restart phát effect shadow; chỉ bỏ hosted relay là không đủ;
- rollback bảo toàn append-only evidence và operator audit; migration guard chạy
  trước DDL mất namespace. Old Session route là boundary riêng không bị shadow sửa.

Đây là operational correctness/compatibility evidence. Không paper nào được dùng
để suy ra treatment hiệu quả, throughput production, SLA hoặc exactly-once delivery.

### 16. Cơ chế reproducibility đã được kiểm ở RB-WP5-012

Ticket không thêm novelty claim. Nó operationalize các nguyên tắc paired experiment,
end-to-end validation và artifact reproducibility đã khóa ở tài liệu `09`–`11`:

- cùng exogenous workload/seed/graph/travel/Runner/work rules; policy ID là khác biệt
  config duy nhất và output-bound ACK hash không bị gọi sai thành input treatment;
- B1/C1 cùng dùng commitment/certificate path, tránh confound do pipeline khác nhau;
- exact file + canonical + domain-separated effective-config hashes và Runner
  initialize validation tạo nhiều lớp provenance end-to-end;
- two clean repeats/arm kiểm deterministic bytes/hashes; BeGo materializer và
  checkpoint validator không chỉ tin status mà kiểm cấu trúc/state chain;
- staged exact input đóng preflight/use race; self-verifying manifest reject file
  thiếu/thừa/tamper và snapshot source/assembly để không overstate base commit;
- failure/exclusion log luôn hiện diện và cấm loại run theo metric outcome.

Tiny pseudonymous fixture chỉ tạo Layer-1 mechanical/correctness signal. Không dùng
nó để ước lượng acceptance, revision reduction, non-inferiority, latency SLA hoặc
khả năng tổng quát sang FleetPy/data thật.

## 17. Browser research và mechanism audit cho RB-WP5-013 — 2026-08-09

Các nguồn dưới đây được mở lại từ trang DOI/publisher/abstract chính thức bằng
in-app Browser. Chúng định hình **cách tạo evidence độc lập**, không thay đổi thuật
toán ridepooling và không tự cấp một chứng nhận hình thức cho implementation.

| Nguồn primary | Cơ chế rút ra | Cách đã áp dụng trong WP5-013 | Giới hạn claim bắt buộc |
|---|---|---|---|
| Alvaro, Rosen & Hellerstein, [Lineage-Driven Fault Injection](https://doi.org/10.1145/2723372.2723711), SIGMOD 2015 | Lỗi phân tán nên được chọn theo ranh giới nhân quả có khả năng phá outcome, thay vì chỉ fault ngẫu nhiên mù | Liệt kê hữu hạn mọi durable boundary của decision worker (`8`) và outbox relay (`4`), rồi kill process thật bằng `Environment.FailFast` tại từng boundary; fresh process phải reconstruct và so exact decision/certificate/checkpoint/effect | Không chạy solver SAT/lineage engine của LDFI, nên không claim fault-space completeness hay LDFI certification |
| Kingsbury & Alvaro, [Elle: Inferring Isolation Anomalies from Experimental Observations](https://doi.org/10.14778/3430915.3430918), PVLDB 2020 | Oracle nên suy từ history quan sát bên ngoài thay vì tin internal state/implementation under test | Test-owned transition table chạy `256 × 64 = 16.384` bước và PostgreSQL claim oracle so exact expected/observed operation set với `2/3/4` worker; oracle không gọi production transition table | Không chạy Elle và không chứng minh serializability/linearizability của toàn database; evidence chỉ bao phủ state/claim invariants đã khai báo |
| Claessen & Hughes, [QuickCheck: A Lightweight Tool for Random Testing of Haskell Programs](https://doi.org/10.1145/351240.351266), ICFP 2000 | Sinh input có cấu trúc từ property, giữ seed/counterexample để tái hiện | Sinh history có seed cố định, lưu exact seed/step/accepted-rejected trace và chạy invariant sau từng bước | Không dùng QuickCheck runtime/shrinker và không claim coverage đầy đủ ngoài generator/bounds đã công bố |
| DeMillo, Lipton & Sayward, [Hints on Test Data Selection: Help for the Practicing Programmer](https://doi.org/10.1109/C-M.1978.218136), IEEE Computer 1978 | Chất lượng test được thăm dò bằng các biến thể lỗi có chủ đích mà suite phải phân biệt | Năm mutant bắt buộc lần lượt phá unique active-run, ACK/checkpoint gate, T2/outbox atomicity, semantic fingerprint và canonical hash; cả `5/5` tạo vi phạm được oracle phát hiện | Đây là năm explicit mutation-killing cases, không phải external mutation tool hay mutation percentage |
| Georges, Buytaert & Eeckhout, [Statistically Rigorous Java Performance Evaluation](https://doi.org/10.1145/1297027.1297033), OOPSLA 2007 | Warm-up, nhiều repetition, raw observations và environment provenance cần được giữ để tránh kết luận timing từ một lần chạy | Mỗi cấu hình queue `8/32/64 × worker 1/2/4` có một warm-up, năm measured repetition, randomized scenario order, raw sample, machine/PostgreSQL config và row count trước/sau | Chỉ là claim/drain curve trên một máy; chưa có significance test, end-to-end throughput, capacity limit hay production SLA |

Artifact tự kiểm chứng của ticket có manifest SHA-256
`e21fb0877fbc6d61bf6f1e24adcda24e09a29fea95a9f44d1b61bf4fc1061ca2`.
Nó bind source snapshot, executing assembly, Runner artifact, raw history/crash/
mutation/performance observations và machine configuration. Kết quả cơ học là
không mất/nhân đôi committed operation trong phạm vi thử, mọi crash recovery khớp
oracle và `5/5` mutant bắt buộc bị giết; không phải bằng chứng C1 hiệu quả hơn B1.

## 18. Claim closure audit cho RB-WP5-014 — 2026-08-09

Review cuối đối chiếu lại toàn bộ WP1–WP5 với nguồn primary đã mở bằng in-app
Browser. Kết luận bảo thủ được giữ nguyên:

- durable operation/result reuse lấy cơ chế từ RIFL nhưng chỉ được gọi là durable
  duplicate suppression/idempotent effect under retry, không phải transport
  exactly-once;
- durable boundary/process-crash enumeration lấy cảm hứng LDFI nhưng không có
  lineage solver, nên không chứng nhận fault-space completeness;
- external-history oracle lấy cảm hứng Elle và structured random properties lấy
  cảm hứng QuickCheck, nhưng không chạy hai tool đó và không chứng minh toàn cục
  serializability/linearizability/property coverage;
- năm mutant là năm lỗi có chủ đích bị giết, không phải mutation score; local queue
  curves tuân thủ warm-up/repetition/provenance nhưng không phải SLA/significance.

Ba sửa đổi closure — subject-link append-only, absolute head chỉ publication-eligible
sau operation `Applied`, và scope/DbContext độc lập theo run — là correctness/scalability
mechanisms của integration boundary. Chúng không phải novelty claim. Source audit
cho phép refinement common harness tiếp tục; main experiment, production SLA và
effectiveness claim vẫn NO-GO cho đến khi WP6–WP9 cung cấp evidence tương ứng.
