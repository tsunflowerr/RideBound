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

## 19. Browser research và mechanism boundary cho ADR-026/WP6 — 2026-08-09

Các trang official/publisher được mở bằng in-app Browser và được lưu chi tiết tại
[WP6 reproducibility evidence](research/wp6-benchmark-reproducibility-evidence-2026-08-09.md).

| Nguồn | Cơ chế được áp dụng | Claim không được phép |
|---|---|---|
| FleetPy 2026 + Manhattan Zenodo v1 | public versioned source descriptor, DOI/license/checksum, deterministic normalizer/common scenario; module boundary tách demand/network/fleet-control/user model và WP7 simulator clock khỏi WP6 instant-drain mechanics | Không gọi WP6 là FleetPy effectiveness/simulator reproduction hoặc dùng zero wait/ride làm KPI |
| Engelhardt–Dandl–Bogenberger, Speed-up Heuristic for an On-Demand Ride-Pooling Algorithm | Nếu audit xác nhận candidate enumeration là bottleneck, thử compatibility/direction/distance prefilter với exact-small oracle, deterministic HMAC rank/vector, pruning/loss diagnostics và cùng contract cho B1/C1 | Không copy random order/vector, không bỏ hard validator, không claim paper speed-up/quality giữ nguyên trên RideBound khi chưa đo |
| RFC 8785 JCS | học các rủi ro duplicate/non-finite/ordering; dùng subset integer-only chặt hơn với vectors riêng | Không claim full JCS conformance |
| Salmon et al., Random123 | addressable randomness theo counter/key thay cho mutable RNG stream; hiện thực bằng labeled HMAC-SHA-256 | Không claim dùng Random123 implementation |
| RFC 8493 BagIt | payload/tag manifests và completeness/validity terminology | Không coi checksum-only là provenance/semantic validity |
| W3C PROV, FAIR, Datasheets | source→transform→scenario→run→metric lineage, license/limits/intended use | Không claim FAIR certification hay data không bias |
| Sandve et al. | exact versions, raw data, transformations, seeds và intermediate evidence | Không claim independent replication |
| ACM artifact terminology | tách repeatability/reproducibility/replicability và badge vocabulary | Không tự nhận ACM badge, `Results Reproduced` hoặc `Results Replicated` |
| McKeeman, Differential Testing for Software (1998) | `RB-WP6-008` dùng production calculator và executable oracle không ProjectReference/chung model; tự parse raw evidence rồi so 132 canonical rows + metric-set hash byte-exact, kèm mutation/overflow matrix | Agreement không chứng minh specification đúng, không loại correlated bug và không phải independent scientific reproduction |
| Dolan & Moré, Performance Profiles (2002) | Giữ metric từng run, exact pairing grid và planned failure/exclusion denominator để WP8/WP9 có thể preregister aggregate/profile hợp lệ | WP6 không phát performance profile, ranking, effectiveness hay non-inferiority claim |
| RFC 8493 + Library of Congress BagIt conformance suite (đối chiếu implementation 2026-08-11) | `RB-WP6-009` dùng exact payload/tag/oxum, no-self tag manifest, path portability gates và valid/invalid mutation taxonomy; RideBound bổ sung source/runtime/grid/transcript/raw-metric semantic stages | BagIt validity không phải scientific validity, algorithm correctness, effectiveness hay independent reproduction |
| ACM terminology + NASEM 2019 | `RB-WP6-010` phân biệt same-team computational repeatability với independent-team reproduced/replicated badge semantics bằng exact machine-readable caveat/profile | Self-verifying bundle không tự thành ACM badge, independent reproduction hoặc replication |
| Peng, Reproducible Research in Computational Science (2011) | Claim report luôn giữ `mechanical/development`, vì reproducibility chỉ là minimum standard và không bảo đảm correctness/validity | Không nâng exact rerun thành algorithm correctness/effectiveness |
| Munafò et al., A manifesto for reproducible science (2017) | Source-locked profile, non-confirmatory wording và ADR-only extension chống post-hoc HARKing/analytical-flexibility ở artifact label | Không dùng exploratory/tiny evidence làm confirmatory conclusion |
| Unicode UTS #39 | Scoped NFKC/casefold/diacritic + punctuation dual skeleton, common confusable mapping và default-ignorable rejection trong claim-bearing fields | Không claim full UTS #39 conformance hoặc quét NLP tổng quát |

ADR-026 chuyển các bài báo/standard này thành executable requirements của tickets
`RB-WP6-002..014`; chúng là provenance/reproducibility optimizations, không thay đổi
novelty boundary của thuật toán RideBound.

## 20. Closure research recheck cho RB-WP6-014 — 2026-08-13

In-app Browser đọc lại primary paper/preprint thay vì dùng search snippet làm design
authority. Một DOI author-preprint bị anti-bot chỉ được dùng để xác nhận metadata;
không có thay đổi code nào dựa trên nội dung không đọc được.

| Nguồn primary | Kết luận source audit | Quyết định áp dụng/không áp dụng |
|---|---|---|
| Alonso-Mora et al., [On-demand high-capacity ride-sharing via dynamic trip-vehicle assignment](https://www.pnas.org/doi/10.1073/pnas.1611675114), PNAS 2017 | RV→trip→RTV→ILP và anytime incumbent giải thích separation candidate/assignment; feasible clique dựa feasible subtrips | WP4 giữ candidate/fleet/solver/fallback; cap phải có loss diagnostics. Không copy pre-pickup reassignment vì O-001 |
| Simonetto et al., [Real-time city-scale ridesharing via linear assignment problems](https://arxiv.org/abs/1902.10676), TRC 2019 | batching/sparse LAP có thể tăng scale nhưng filter có thể mất quality | Chỉ future hypothesis; cần deterministic oracle/loss/revalidation trước production |
| Santi et al., [Quantifying the benefits of vehicle pooling with shareability networks](https://www.pnas.org/doi/10.1073/pnas.1403657111), PNAS 2014 | shareability graph đo potential trong model/data của paper | Không dùng làm commitment novelty hoặc suy savings cho RideBound |
| Ackermann & Rieck, [Multiple plan approach for the dynamic pickup and delivery problem](https://doi.org/10.1002/net.22063) | plan pool/executable plan/compatibility có ích; consensus/preemptive stopping không luôn tốt, overoptimization có thể giảm flexibility | B5 canonical bounded pool và distinguished-only apply; pairing tách, không universal-best claim |
| Engelhardt et al., [Speed-up heuristic for an on-demand ride-pooling algorithm](https://arxiv.org/abs/2007.14877) | direction/distance filter đánh đổi runtime và solution quality | Không thêm filter ở closure; chỉ thử khi có same-work differential/loss evidence |

Kết luận: thuật toán/ràng buộc hiện hành đã áp các cơ chế có evidence phù hợp. Việc
thêm một heuristic mới chỉ để benchmark nhanh/đẹp sẽ làm yếu comparator và không được
coi là “tối ưu thực sự”. ADR-036 đóng WP6 mà không đổi claim boundary.

## 21. Browser recheck và closure Candidate/FleetPy WP7 — 2026-08-16

In-app Browser được dùng lại để đọc primary/author sources sau khi implementation đã
có. Một trang PNAS/PMC yêu cầu CAPTCHA nên không được bypass và không dùng nội dung bị
chặn làm authority mới. Các nguồn có thể đọc trực tiếp được đối chiếu với source/test,
không được dùng để suy ra performance trên instance RideBound.

| Nguồn | Điều source cho phép rút ra | Áp dụng hoặc chủ động không áp dụng |
|---|---|---|
| Alonso-Mora et al., [On-demand high-capacity ride-sharing via dynamic trip-vehicle assignment](https://www.pnas.org/doi/10.1073/pnas.1611675114) | Phân tầng RV/trip/assignment giải thích tại sao một service set trùng có thể là redundancy ở fleet selection, nhưng không chứng minh mọi route variant thay thế nhau dưới commitment | Portfolio WP7 chỉ chứng minh B1 cùng-vehicle/same-set cost anchor; stable variant và C1 vẫn đi qua hard validator. Không thêm pre-pickup reassignment |
| Engelhardt, Dandl & Bogenberger, [Speed-up heuristic for an on-demand ride-pooling algorithm](https://arxiv.org/abs/2210.06972) | Filter/rolling-horizon heuristic có trade-off phụ thuộc network/order và cần định lượng loss | Không import direction/random/forecast filter. Thay vào đó sửa lỗi B4 ranking bằng own repaired suffix, có regression trước work cap; đây là correctness của bounded search order, không copy speed-up claim |
| Zalesak, Hu & Samaranayake, [Ridepooling with dynamic vehicle routes: exact and heuristic approaches](https://arxiv.org/abs/2504.10649) | Weighted set packing, bounded candidate generation và route/solution stability là các mechanism tách biệt cần deterministic order and scope | Cost anchor theo exact service set và stable route anchor là opt-in, deterministic và loss-accounted. Không dùng column generation/native simulator solver, không gọi mechanism này là novelty |
| FleetPy 1.0.2 pinned source | Callback, directed edge position and locked-plan semantics chỉ đáng tin khi verify trên exact source/version, không từ prose paper | Capability probe + actual FleetControl/tiny/medium call cùng Runner v6; no force assignment, no reverse/zero arc invention, and typed fail-closed mapping |

Đối chiếu này dẫn tới hai thay đổi source-state có tính tối ưu thực sự nhưng bounded:

1. B4 repair root được priority theo route repair thực tế, tránh dùng slack của route
   cũ khi budget chỉ đủ chọn ít root. Test quan sát projection route trước work-unit
   decision, nên nó kiểm search ordering chứ không chỉ nhánh điều kiện.
2. Portfolio opt-in cấm route có request introduced nhưng không khai trong
   `NewRequestIds`; cùng với preservation exact no-op stops, proof service-set/cost
   không còn dựa trên một nhãn có thể sai.

Evidence đầy đủ, raw identifiers và giới hạn claim ở
[WP7 Layer-2 closure evidence](benchmarking/wp7-014-fleetpy-layer2-closure-evidence-2026-08-15.md).
Các run actual chứng minh mechanics/repeatability, không chứng minh C1 tốt hơn B1,
quality, runtime/SLA, fairness, satisfaction hay optimality tổng quát.

## 22. Feasibility-testing research và hot-path audit — 2026-08-17

Vòng nghiên cứu này bắt đầu từ **số đo**, không từ paper. Một micro-harness xác định
được rằng chi phí sinh candidate không nằm ở validator (`0,72 µs/route`) mà ở việc
**tính lại identity**: khóa memo slack tính một fingerprint SHA-256 có framing trên
toàn bộ stop cho mỗi lần tra cứu (`19,6 µs`), và mỗi `Generate` thực hiện khoảng
39.000 lần tra cứu. Chỉ sau khi có con số đó mới đi tìm nguồn.

| Nguồn | Điều source cho phép rút ra | Áp dụng hoặc chủ động không áp dụng |
|---|---|---|
| Savelsbergh 1992, *The VRP with Time Windows: Minimizing Route Duration* | Forward time slack là lượng tối đa có thể lùi thời điểm bắt đầu phục vụ tại một node mà route vẫn khả thi | Đã có trong `ForwardSlackProfile` từ WP4; lần này chỉ xác nhận định nghĩa gốc, không đổi ngữ nghĩa `CertifiedDelay` |
| Cordeau & Laporte 2003, eight-step evaluation scheme | Kiểm khả thi DARP theo capacity/time window/ride time bằng cách dựng lại lịch; worst case `O(r²)` theo số stop | Đây đúng là lớp chi phí RideBound đang trả: mỗi candidate route dựng lại toàn bộ schedule + slack profile |
| Braekers et al. 2014 | Đề xuất preliminary check trước khi chạy eight-step để bỏ bớt tính toán thừa | Không áp dụng ở đây: RideBound đã có physical prune và exact loss accounting; thêm một tầng lọc nữa mà không đo được sẽ làm yếu comparator |
| **Gschwind & Drexl 2019**, *ALNS with a Constant-Time Feasibility Test for the DARP*, Transportation Science 53(2):480–491, [10.1287/trsc.2018.0837](https://doi.org/10.1287/trsc.2018.0837) | Kiểm khả thi chèn request trong thời gian hằng số khấu hao, **chỉ đánh giá hai node pickup/drop được chèn** thay vì cả route; báo cáo speed-up trung bình `3,8×` so với eight-step | **Chưa áp dụng.** Đây là hướng đúng cho WP8 vì nó *exact* chứ không phải filter heuristic, nên không làm yếu comparator. Nhưng nó chỉ phủ chiều thời gian; validator RideBound còn kiểm capacity, connectivity, frozen prefix và commitment budget, nên nó chỉ có thể thay phần schedule/slack và vẫn phải chạy full validator sau |
| Posada & Häll 2020 | Đã áp dụng constant-time check của Gschwind–Drexl cho một biến thể tích hợp nhưng không công bố chi tiết | Ghi nhận như tiền lệ áp dụng, không dùng làm authority kỹ thuật |

Ràng buộc đọc nguồn: bản Transportation Science bị paywall `403` và preprint Mainz
`LM-2016-08.pdf` trả `404` tại ngày kiểm tra. Vì vậy Gschwind–Drexl 2019 chỉ được
dùng ở mức **metadata/abstract**, và không có dòng code nào được viết dựa trên nội
dung chưa đọc được. Cơ chế chi tiết phải được đọc full text trước khi ticket hóa.

### 22.1 Điều đã thực sự thay đổi trong source

Không có heuristic mới nào được thêm. Các thay đổi đều là **loại bỏ công việc thừa
mà không đổi một byte kết quả nào**:

- khóa memo slack chuyển từ fingerprint SHA-256 sang so sánh cấu trúc chính xác
  (version, executed count, frozen prefix, mutable suffix theo từng phần tử). Khả năng
  phân biệt không đổi — đó đúng là thứ fingerprint mã hóa — nhưng bỏ được rủi ro va
  chạm hash và `~19 µs` mỗi lần tra cứu;
- framing identity ghi thẳng UTF-8 vào buffer thay vì cấp phát hai mảng mỗi frame;
- `RoutePlan.Create` phát hiện stop trùng bằng đếm hai lượt thay vì `GroupBy`, giữ
  nguyên đúng key được báo lỗi;
- StableId của search node và route đã chiếu được tính lười và nhớ lại, nên node nào
  không ai hỏi tới thì không phải trả một lần SHA-256;
- generator rank một lần rồi đưa thứ tự đó cho retainer, kèm kiểm tra fail-closed.

Bằng chứng bất biến: `CandidateSearchWorkProfileTests` khóa chính xác work unit,
evaluated path, feasible-before-cap, omitted path, retained count và **số profile slack
riêng biệt** cho bốn kích thước route; toàn bộ suite WP6 vẫn tái tạo đúng semantic hash.

### 22.2 Nửa còn lại của pipeline — solver stage

Vòng tìm nguồn cũng quét tầng assignment, vì đó là nửa còn lại của chi phí. Kết quả:
không có gì được áp dụng, và lý do là kỷ luật chứ không phải thiếu ứng viên.

- **Warm start xuyên epoch** (integral primal simplex, Springer 2024) và **column
  generation cho real-time ride-sharing**: cả hai tái dùng nghiệm/cột của epoch trước
  thay vì giải lại từ đầu. Repo đã từ chối column generation ở §21 (Zalesak 2025) và lý
  do đó không đổi: nó làm nghiệm của epoch `t` phụ thuộc đường đi lịch sử của solver,
  khiến hai arm không còn so được dưới cùng một pool.
- Đo lường cũng chưa biện minh cho việc động vào tầng này: ở fixture đã đo, solver không
  phải điểm nóng — sinh candidate mới là. Tối ưu một tầng chưa được chứng minh là
  bottleneck là đúng thứ mà `docs/11` cấm.

### 22.3 Kết quả âm được giữ lại

Đề xuất *lazy priority* (đưa node vào frontier bằng key rẻ, chỉ tính slack khi pop)
**đã bị bác bỏ bằng phân tích, không phải bằng cảm tính**. Lý do: stop được chèn có
`ServiceDuration = 0`, nên mọi insertion child của cùng một node bằng nhau ở cả
`potentialAccepted` lẫn `mandatoryService`. Cận dưới rẻ vì thế đồng hạng trên gần như
toàn bộ frontier; thuật toán sẽ phải refine lần lượt tất cả rồi mới pop được node
đầu tiên, tức là tính đúng bằng số slack như cũ cộng thêm chi phí xáo heap. Ghi lại ở
đây để lần sau không ai đề xuất lại mà không kèm cận dưới chặt hơn.

## 23. Consistency–cost trade-off là prior art — tra cứu cho WP8, 2026-08-19

Pilot WP8 cho thấy C1 là *có ràng buộc* so với B1 *không ràng buộc*, nên câu hỏi đúng là
cái giá của ràng buộc chứ không phải ai thắng. Tra cứu cho thấy đây là một dòng nghiên
cứu đã có tên và đã có kết quả.

| Nguồn | Điều rút ra | Áp dụng / không áp dụng |
|---|---|---|
| *A multi-period dial-a-ride problem with driver consistency* (DC-DARP), Transportation Research Part B | Giới hạn số tài xế khác nhau phục vụ một khách qua nhiều kỳ; khách nhạy cảm với thay đổi trong thói quen, gồm cả người lái | Tương tự `vehicle_switch_count`. Đây là **prior art**: RideBound không được claim ý tưởng "giới hạn thay đổi để giữ ổn định" là mới; khác biệt của RideBound phải nằm ở intra-trip, path-dependent revision ledger như `docs/03` đã khoá |
| Cùng nguồn | Báo cáo rằng phục vụ bằng **2 tài xế** là compromise tốt giữa consistency và cost | Điểm vận hành tốt nằm ở **mức trung gian**, không phải mức chặt nhất. Cấu hình C1 hiện tại đặt cả ba hard limit ở `0` — tức cực đoan nhất — và pilot cho thấy đúng là nó trả giá bằng dịch vụ |
| *On service consistency in multi-period vehicle routing*, EJOR | Kế hoạch nhất quán cho **mọi** khách thường quá đắt; phải tìm compromise giữa cost và consistency | Xác nhận rằng cách trình bày hợp lệ là **đường đánh đổi**, không phải một con số thắng thua. Đúng với `revision-service Pareto curve` mà `docs/11` §15 đã yêu cầu |

Giới hạn đọc nguồn: cả ba đều paywall tại ngày kiểm tra, nên chỉ dùng ở mức
abstract/metadata. Không dòng code nào được viết dựa trên nội dung chưa đọc được; kết
luận rút ra ở đây là về **cách thiết kế và trình bày thí nghiệm**, không phải về thuật
toán.

Hệ quả cho WP8: `RB-WP8-008` phải chạy ít nhất ba mức strictness và báo cáo frontier.
Chọn sẵn mức chặt nhất rồi báo cáo một con số là vừa sai phương pháp vừa mâu thuẫn với
kinh nghiệm đã công bố của chính dòng nghiên cứu này.

## 24. Full-PDF recheck và optimization boundary trước WP9 — 2026-08-21

Mục này supersede giới hạn đọc ở §22 dòng 382–385: full text đã được thu thập và
đọc từ PDF, không còn dựa trên title/abstract. File được giữ ngoài repo có chủ đích
tại `E:\RideBoundData\research\pdf-20260820`; hash dưới đây làm provenance:

| PDF đã đọc | Trang | SHA-256 | Kết luận áp dụng |
|---|---:|---|---|
| Alonso-Mora et al. 2017, main | 6 | `edbb62156e36479b742a1a7381e5920673a4b6a3130bba39aed74cb8364c12ea` | Giữ separation request/trip/vehicle/assignment và bounded feasible set; không copy reassignment |
| Alonso-Mora et al. 2017, supplement | 32 | `0d7e37aba541035bdbc60da0eb35e81a63859eb96850a28d9a8fba116760ddd5` | Xác nhận feasibility/assignment phụ thuộc exact state; reuse chỉ hợp lệ khi key bind toàn route/state |
| Gschwind & Drexl 2019 | 39 | `16b82b489c6ae925581bebd00223d4aa8bce7541f1b561b2bd2320529ad18e61` | Constant-time insertion dùng preprocessing/slack exact trong model của paper; không chuyển nguyên xi qua capacity/frozen-prefix/commitment validator |
| Simonetto, Monteil & Gambella 2019 | 30 | `9f5e31a8d69a63b1286fbe55577d07a8449585356aa6802df9fb734d75ae3868` | Sparse/batched linear assignment là hướng scale, nhưng đổi batching/candidate pool nên không dùng trước confirmatory |
| Engelhardt, Dandl & Bogenberger 2020 | 11 | `5b3d20b26e701da7837a149eb2953e1a828d0bf4fab780dbe5a50eb6defd01ed` | Direction/distance/random filters có runtime–quality trade-off; không đưa vào comparator khi chưa có loss bound |
| Zalesak, Hu & Samaranayake 2025 | 23 | `744193567e9033de631bc63604530239955d70575ddb8614b7d75fcf078ba086` | Route stability và exact/heuristic generation là các mechanism tách biệt; không coi stability là novelty RideBound |
| Schulz & Pfeiffer 2026 | 46 | `9a4d4997cdecc7242521ff733f8d474b6b57dc1856ce261734b7923b25a8c8d7` | Reoptimization/insertion cho thấy state reuse cần explicit invalidation; chỉ dùng như kiểm tra boundary, không nhập thuật toán chưa benchmark |

### Thay đổi code thực sự từ full-text audit

Không có heuristic prune mới. Candidate hot path chỉ bỏ công việc exact bị lặp:

- schedule/slack lookup tái sử dụng kết quả khi **cùng state và cùng route key cấu
  trúc**, không dựa hash có thể va chạm;
- stable identity được lazy-cache nhưng input framing/output không đổi;
- full physical/commitment validator vẫn chạy, candidate cap/loss diagnostics và
  solver pool không đổi giữa B1/C1.

Work-profile exact counters giữ nguyên; ba process measurement giảm khoảng 20–23%
wall/process time ở fixture đo. Đây là local engineering result, không phải claim
speed-up `3,8×` của Gschwind–Drexl, không phải SLA, và không suy quality preservation
ra workload khác ngoài exact differential đã chạy.

## 25. ADR-052 — exact cache identity reuse hậu WP10

Đọc full PDF không tự động cấp quyền nhập thuật toán paper. Gschwind–Drexl và
Schulz–Pfeiffer yêu cầu preprocessing/reuse gắn với route state chính xác; nhưng
constant-time temporal check của họ không chứng minh các constraint capacity,
connectivity, frozen-prefix, accepted/onboard và commitment của RideBound. Thêm nữa,
`ITravelTimeLookup` không khóa triangle inequality nên một temporal failure của partial
route không đủ để chứng minh mọi descendant insertion đều failure. Vì vậy subtree
prune và full constant-time test đều bị từ chối.

Thay đổi được nhận chỉ là hai phép khử lặp exact:

1. immutable `VehicleState` reference trong `ForwardSlackCacheKey` đã bind position;
   không tạo thêm textual position fingerprint trên từng lookup;
2. terminal node so trực tiếp prefetched key với exact run/vehicle/route/time/travel/
   allowance, không allocate/hash key thứ hai.

Ba process trước và ba process sau cho allocation key giảm đúng 30%; complete
generator giảm 0,79–1,30% heap với toàn bộ work/evaluated/feasible/omitted/retained/
slack-miss counter không đổi. Timing generator mixed nên không có speed claim.
Raw artifact hashes, protocol và giới hạn ở
[`post-wp10-exact-reuse-optimization-2026-08-23.md`](benchmarking/post-wp10-exact-reuse-optimization-2026-08-23.md).

## 26. ADR-053 — full-PDF evidence cho mechanism diagnostics hậu H6

Ba full PDF mới được Browser xác nhận, trích xuất và đọc tuần tự 106/106 trang. Corpus
ngoài repo: `E:\RideBoundData\research\pdf-20260823-post-h6`.

| Paper | Trang | SHA-256 | Áp dụng | Không áp dụng |
|---|---:|---|---|---|
| Pillac et al., review dynamic VRP | 29 | `770027591d40b271e3a2832b6cc9c4234220e8fa218fe747d04b7f2fe27f739d` | tách service guarantee, diversion, dynamism/urgency, reactiveness | không copy benchmark/threshold lịch sử |
| Ulmer et al., route-based MDP | 42 | `5bc8131bf4d5bb1711a6658486e1f256e9ddf504f2388709696ec2af17762695` | tách pre/post-decision state, action, exogenous info và trajectory | không coi `ForwardSlackProfile` là future value |
| Ackermann et al. 2025, multiple-plan DARP | 35 | `059a0bffc546e8588f1d9487ddb16344e7c24c559bc54dfa104979f98e223d3c` | plan-pool/secondary objective là exploratory, configuration-dependent | không copy slack/double-horizon/consensus/budget default |

Thiết kế nhận được là evidence-sufficiency-first: định vị first observed divergence,
link exact prune witness khi tồn tại, và ghi downstream result là
`trajectoryAssociated`. H6 không có full retained candidate route/schedule, nên không
được reconstruct/rerank. Chi tiết applied/rejected và provenance nằm tại
[`post-h6-mechanism-diagnostics-full-pdf-evidence-2026-08-23.md`](research/post-h6-mechanism-diagnostics-full-pdf-evidence-2026-08-23.md).
