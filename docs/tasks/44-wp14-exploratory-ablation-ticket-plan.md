# WP14 — Exploratory ablation và Pareto frontier: ordered queue

> Work package: `WP14 FREEZE V1 STOPPED FAIL-CLOSED`
> Refinement: `RB-WP14-001 DONE`
> Active implementation ticket: `NONE`; `RB-WP14-010..014` không được authorize
> Quy tắc: một ticket implementation active; ticket xa chỉ được refine khi queue head đóng

## 1. Queue

| ID | Kết quả review được | Trạng thái | Dependency |
|---|---|---|---|
| RB-WP14-001 | ADR-066, full-PDF boundary, factor matrix, gate, ordered queue | Done | WP13 |
| RB-WP14-002 | Constant-level pre-solve skip; quyết định bất biến, trung tính giữa hai arm | Done | 001 |
| RB-WP14-003 | Full witness set trong evidence profile, không đổi hot path | Done | 002 |
| RB-WP14-004 | Development panel: nguồn, chọn mẫu, provenance, leakage audit | Done | 003 |
| RB-WP14-005 | Factor implementation F1/F2 (pickup lock scope, ratchet) | Done | 004 |
| RB-WP14-006 | Factor implementation F3 (penalty band) và F4 (vị trí operational-cost) | Deferred | 005 |
| RB-WP14-007 | Factor implementation F5 (hold) và F6 (mục tiêu phân phối) | Deferred | 005 |
| RB-WP14-008 | Freeze manifest: matrix, denominator, analyzer, resource envelope | Done | 005 |
| RB-WP14-009 | Dry-run schema + tiny; ước lượng byte/decision; cancellation receipt | Closed — FAIL CLOSED | 008 |
| RB-WP14-010 | Execute matrix trên development cells | Not authorized under freeze v1 | 009 |
| RB-WP14-011 | Independent verifier và mutation matrix | Not authorized under freeze v1 | 010 |
| RB-WP14-012 | Frontier report hai trục kèm đuôi per-rider | Not authorized under freeze v1 | 011 |
| RB-WP14-013 | Full source/logic/claim audit của WP14 | Not authorized under freeze v1 | 012 |
| RB-WP14-014 | Closure evidence và quyết định mở/không mở WP15 | Not authorized under freeze v1 | 013 |

Ticket xa cố ý chỉ có một dòng. Chúng sẽ được refine khi queue head đóng, theo
đúng progressive elaboration đã dùng từ WP1.

### Hoãn F3–F6 sau `005`

`006` và `007` được hoãn theo quyết định của chủ nghiên cứu ngày 2026-08-25. Lý do:
đo đạc của `001` cho thấy toàn bộ mất mát dịch vụ nằm ở **gate**, và ranking gần như
bất hoạt (0,738% choice set đổi lựa chọn cục bộ). Chín factor level của `005` đã phủ
đúng hai cơ chế thực sự ràng buộc — `pickup_eta_ms` lock (17% prune) và
`drop_eta_total_ms` budget (83% prune). F3 penalty band, F4 vị trí objective, F5 hold
và F6 distributional target đều là tính năng optimizer mới đáng kể; làm trước khi có
frontier đầu tiên nghĩa là trả chi phí lớn cho giả thuyết chưa được kiểm.

Hoãn, **không huỷ**: nếu `012` cho thấy frontier từ F1/F2 và budget sweep không đạt,
F3–F6 được mở lại. Ghi rõ ở đây để không ai tưởng chúng đã bị loại bằng bằng chứng
như bốn factor ở `tasks/43` mục 3.

## 2. RB-WP14-002 — Constant-level pre-solve skip

### Purpose

Giảm chi phí solver mà **không đổi một quyết định nào**, để ablation matrix của
WP14 khả thi về tài nguyên. WP13-002 đã từng fail đúng ceiling CPU 120 s
(120.062/120.000 ms) trên medium public drain; matrix sẽ nhân chi phí đó lên.

### Mệnh đề cần chứng minh

Với bài toán chọn đúng một candidate mỗi vehicle, nếu ở level `i` mọi candidate
của mọi vehicle đóng góp cùng một giá trị `c_v`, thì:

- với aggregation `sum`, mọi lời giải khả thi có giá trị `Σ_v c_v` — hằng số;
- với aggregation `maximum`, mọi lời giải khả thi có giá trị `max_v c_v` — hằng số.

Do đó pass CP-SAT cho level `i` luôn trả về hằng số đó, và ràng buộc
`objective_i == optimum_i` mà nó sinh cho các level sau là thoả bởi **mọi** lời
giải khả thi, tức vô hiệu. Bỏ pass và ghi thẳng hằng số là biến đổi bảo toàn
quyết định.

Ràng buộc request-uniqueness chỉ thu hẹp tập khả thi nên không phá lập luận.

### Phát hiện làm đổi contract của ticket

`executionEvidence` nằm bên trong hash projection của decision, và nó chứa
`consumedDeterministicTimeMicros`. Bớt pass làm con số đó giảm **thật**, nên
không thể có transcript byte-identical mà không bịa số đo. Ticket vì vậy được
thực hiện dưới dạng **opt-in, mặc định tắt**:

- mặc định tắt ⇒ mọi configuration đã publish và mọi hash đã ghi không đổi;
- khi bật ⇒ action, candidate được chọn và `stateAfterHash` bất biến; optimum của
  mọi level bất biến; `consumedDeterministicTimeMicros`, `detailCode` và do đó
  `decisionHash` thay đổi một cách trung thực.

### Scope

- pre-solve check `O(candidates × levels)` ở tầng Application
  (`CandidateSelectionProblem.ConstantObjectiveLevelValues`), adapter tiêu thụ;
- cờ `solverExecution.skipConstantObjectiveLevels`, allowed nhưng không required;
- ghi hằng số vào `objectiveBounds` với `isProvenOptimal: true` và
  `detailCode = ORTOOLS_OPTIMAL_CONSTANT_LEVELS_SKIPPED`, để evidence không bao
  giờ ngụ ý một pass đã chạy trong khi không chạy;
- luôn để lại ít nhất một level được giải để vẫn có incumbent;
- giữ nguyên `SelectionExecution` semantics và fallback path.

Ngoài scope: đổi objective hierarchy, đổi gate, đổi generation, đổi bất kỳ ngữ
nghĩa quyết định nào.

### BDD

- Given một level mà mọi candidate của mọi vehicle đóng góp cùng giá trị, when
  solver chạy, then không có model nào được dựng cho level đó và bound ghi đúng
  hằng số.
- Given một level mà ít nhất một vehicle có hai giá trị khác nhau, then level đó
  vẫn được giải bình thường.
- Given cờ tắt, then mọi byte của decision bằng đúng trước khi thay đổi.
- Given cờ bật, then action set, candidate được chọn và state hash bằng nhau,
  còn `consumedDeterministicTimeMicros` giảm và `detailCode` nói rõ đã bỏ pass.
- Given fallback path hoặc model invalid, then hành vi không đổi.
- Given aggregation `maximum` với các vehicle có hằng số khác nhau, then vẫn coi
  là thoái hoá và hằng số là `max`.

### Evidence bắt buộc

- differential 64 seed trên production C1 mapping: selected IDs, objective values
  và mọi bound bằng nhau khi bật/tắt cờ;
- toàn bộ test cũ pass không sửa dòng nào, chứng minh mặc định tắt tương thích;
- mutation test: một level phân biệt được không bị coi là hằng số;
- edge: hierarchy toàn hằng số vẫn tạo được incumbent;
- không claim wall-clock; envelope thực đo ở `009`.

### Kỳ vọng định lượng

Từ recorded evidence của H6/E1 panel (chỉ để ước lượng chi phí, **không** phải
tuning input): 94,40% / 97,40% / 93,81% / 97,78% level là hằng số ở
Panel A B1 / Panel A C1 / Panel B B1 / Panel B C1. Con số thực tế trên
development panel sẽ khác và phải được đo lại.

### Kết quả

Done. Báo cáo:
[`wp14-002-constant-level-skip-2026-08-24.md`](../benchmarking/wp14-002-constant-level-skip-2026-08-24.md).
Required .NET 873/873, pinned Python 212/212, zero skip. Chỉ `RB-WP14-003`
chuyển Ready.

## 3. RB-WP14-003 — Full witness set trong evidence profile

### Purpose

`CommitmentDecisionValidator` return ở request fail đầu tiên và ở layer fail đầu
tiên trong request đó. Attribution prune vì vậy phụ thuộc thứ tự ID. Điều này
đúng cho hot path nhưng làm evidence không đủ để phân rã layer.

### Scope

Khi evidence profile bật, chạy đủ mọi request và mọi layer rồi ghi toàn bộ
witness set. Khi profile tắt, hành vi và chi phí không đổi.

Ngoài scope: đổi thứ tự đánh giá, đổi feasibility, đổi hot path.

### BDD

- Given profile tắt, then witness và chi phí bằng hiện tại.
- Given profile bật và một candidate vi phạm cả budget lẫn lock ở hai request
  khác nhau, then cả hai witness được ghi.
- Given profile bật, then feasibility verdict không đổi so với profile tắt.

### Kết quả

Done, nhưng contract phải đổi giữa chừng: bản đầu suy cờ từ
`retained-portfolio-v1`, đúng profile mà hai config E1 đã freeze khai báo, nên sẽ
đổi evidence và `decisionHash` của E1. Sửa bằng profile riêng
`retained-portfolio-full-witness-v1`. Báo cáo:
[`wp14-003-full-witness-set-2026-08-24.md`](../benchmarking/wp14-003-full-witness-set-2026-08-24.md).

## 4. RB-WP14-004 — Development panel và leakage audit

Done. 16 cell trên 2018-11-12/13, hai cửa sổ trong ngày, 0 giao với H6 trên cả bảy
trục so sánh. Báo cáo:
[`wp14-004-development-panel-2026-08-24.md`](../benchmarking/wp14-004-development-panel-2026-08-24.md).
ADR-068 bổ sung declaration `source-divergence-v1.json` cho frozen source file mà
WP14 sửa hợp lệ.

## 5. RB-WP14-005 — Factor F1/F2

Done. F1 dùng `freezeHorizonMs` + `freezeHorizonLocks` đã có sẵn trong domain nhưng
chưa configuration nào từng dùng. F2 thêm `ratchetLocks` vào `CommitmentPolicy`: một
ETA lock được nêu ở đó chỉ bị vi phạm khi candidate đẩy promise **muộn hơn**, còn
sớm hơn thì cho phép — đúng cơ chế one-sided của Alonso-Mora et al. Chỉ hai trường
ETA được ratchet vì vehicle/stop không có thứ tự.

Chín factor level trong `benchmarks/configurations/wp14-c1-*.json`, mỗi level khác
reference đúng một điều đã khai báo, có test khẳng định từng điều đó.

## 6. RB-WP14-008 — Freeze manifest

Done. Receipt pre-outcome
[`freeze-receipt-v1.json`](../../benchmarks/scenarios/wp14-development/freeze-receipt-v1.json)
khóa exact 16 cell × 10 arm = 160 job, 108 arrivals/job, 1.728 arrivals/arm,
paired-cell comparison, seed 7 non-replicate, bốn forbidden H6/E1 roots, source/
runtime/tree seals, analyzer và resource envelope. Receipt 101.719 byte, SHA-256
`1ce26ff0…37a55`; verify canonical pass. Báo cáo:
[`wp14-008-development-ablation-freeze-2026-08-26.md`](../benchmarking/wp14-008-development-ablation-freeze-2026-08-26.md).

Review đã sửa trước freeze các gap mà test-count không bắt được: config/runtime/
analyzer mutation hậu freeze, bundle hợp lệ bị đặt nhầm job, p95 off-by-one, float
evidence và duplicate arm-cell overwrite. Required .NET 908/908, pinned Python
242/242; targeted/mutation 17/17, zero skip.

## 7. RB-WP14-009 — Paired dry-run và resource gate

### Purpose

Chạy đúng hai job `b1-ref`/`c1-h6ref` trên cùng cell đầu tiên để chứng minh schema,
Runner, independent verifier, resume và resource envelope hoạt động trên dữ liệu
thật trước khi mở matrix 160 job. Chỉ đo resource; không dùng completion/burden để
đổi factor, denominator hoặc freeze.

### Frozen inputs

- jobs `w14-d20181112-s10-r1-w08-b1-ref-s7` và
  `w14-d20181112-s10-r1-w08-c1-h6ref-s7`;
- output `E:\RideBoundData\wp14\development-ablation`, parallelism 1;
- summary `E:\RideBoundData\wp14\dryrun-summary-v1.json`, exclusive-create;
- exact runtime/external audit/resource arguments trong receipt `008`.

### Acceptance

- receipt verify lại trước launch; E: còn ít nhất 25 GiB;
- cả hai bundle pass independent verifier với behavioral hash và audited solver
  evidence, rồi bind đúng job/scenario/repeat;
- summary schema-valid, exact two selected IDs, 2 completed/0 failed;
- ghi elapsed wall, transcript bytes, bundle bytes, byte/decision và extrapolation
  160 job; không claim speedup từ hai điểm;
- controlled job/global timeout phải xuất typed failure trong summary; partial/
  invalid output không được retry hoặc thay thế;
- chạy lại cùng hai job chỉ được `reusedVerified`, không ghi đè bundle/summary.

### Kết quả

Closed — **FAIL CLOSED**. B1 hoàn tất và pass independent verifier; C1 chỉ còn
partial transcript thiếu `bundle-manifest.json`. Recovery không chạy lại mô phỏng:
B1 `reusedVerified`, C1 rejected, summary schema-valid `1 completed / 1 failed`.
Freeze v1 yêu cầu giữ partial, không retry/replacement; vì vậy `010..014` không được
authorize. Báo cáo:
[`wp14-009-paired-dry-run-resource-gate-2026-08-26.md`](../benchmarking/wp14-009-paired-dry-run-resource-gate-2026-08-26.md).

## 8. Ràng buộc áp cho toàn bộ queue

- không sửa, ghi, move hay xoá bất kỳ raw root H6/E1 nào;
- không dùng số đo của H6/E1 panel để chọn mức factor;
- không mở H7, lifecycle policy v2 hay backfill H6;
- mọi artifact thành công phải exclusive-create sau khi verify đầy đủ, theo
  contract trong
  [`wp13-evidence-retention-and-successor-policy-v1.md`](../benchmarking/wp13-evidence-retention-and-successor-policy-v1.md);
- mỗi ticket Done chỉ mở đúng một ticket kế tiếp.
