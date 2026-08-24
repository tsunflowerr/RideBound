# WP14 — Exploratory ablation và Pareto frontier: ordered queue

> Work package: `WP14 IN PROGRESS`
> Refinement: `RB-WP14-001 DONE`
> Active implementation ticket: `RB-WP14-002`
> Quy tắc: một ticket implementation active; ticket xa chỉ được refine khi queue head đóng

## 1. Queue

| ID | Kết quả review được | Trạng thái | Dependency |
|---|---|---|---|
| RB-WP14-001 | ADR-066, full-PDF boundary, factor matrix, gate, ordered queue | Done | WP13 |
| RB-WP14-002 | Degenerate-level pre-solve skip; quyết định bất biến, trung tính giữa hai arm | Ready | 001 |
| RB-WP14-003 | Full witness set trong evidence profile, không đổi hot path | Blocked | 002 |
| RB-WP14-004 | Development panel: nguồn, chọn mẫu, provenance, leakage audit | Blocked | 003 |
| RB-WP14-005 | Factor implementation F1/F2 (pickup lock scope, ratchet) | Blocked | 004 |
| RB-WP14-006 | Factor implementation F3 (penalty band) và F4 (vị trí operational-cost) | Blocked | 005 |
| RB-WP14-007 | Factor implementation F5 (hold) và F6 (mục tiêu phân phối) | Blocked | 006 |
| RB-WP14-008 | Freeze manifest: matrix, denominator, analyzer, resource envelope | Blocked | 007 |
| RB-WP14-009 | Dry-run schema + tiny; ước lượng byte/decision; cancellation receipt | Blocked | 008 |
| RB-WP14-010 | Execute matrix trên development cells | Blocked | 009 |
| RB-WP14-011 | Independent verifier và mutation matrix | Blocked | 010 |
| RB-WP14-012 | Frontier report hai trục kèm đuôi per-rider | Blocked | 011 |
| RB-WP14-013 | Full source/logic/claim audit của WP14 | Blocked | 012 |
| RB-WP14-014 | Closure evidence và quyết định mở/không mở WP15 | Blocked | 013 |

Ticket xa cố ý chỉ có một dòng. Chúng sẽ được refine khi queue head đóng, theo
đúng progressive elaboration đã dùng từ WP1.

## 2. RB-WP14-002 — Degenerate-level pre-solve skip

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

### Scope

- pre-solve check `O(candidates × levels)` trong `SolverBackedFleetSelection`
  hoặc adapter, chạy trước khi dựng model;
- ghi hằng số vào `objectiveBounds` với `isProvenOptimal: true` và một
  `detailCode` mới nói rõ level bị bỏ qua vì thoái hoá, để evidence không mất
  thông tin;
- giữ nguyên `SelectionExecution` semantics, fallback path, và mọi hash.

Ngoài scope: đổi objective hierarchy, đổi gate, đổi generation, đổi bất kỳ ngữ
nghĩa quyết định nào.

### BDD

- Given một level mà mọi candidate của mọi vehicle đóng góp cùng giá trị, when
  solver chạy, then không có model nào được dựng cho level đó và bound ghi đúng
  hằng số.
- Given một level mà ít nhất một vehicle có hai giá trị khác nhau, then level đó
  vẫn được giải bình thường.
- Given cùng một input, then decision hash, action set và state hash **bằng
  byte** với trước khi thay đổi.
- Given fallback path hoặc model invalid, then hành vi không đổi.
- Given aggregation `maximum` với các vehicle có hằng số khác nhau, then vẫn coi
  là thoái hoá và hằng số là `max`.

### Evidence bắt buộc

- differential trên fixture: exact byte-equal decision transcript trước/sau;
- mutation test: cố ý coi một level không thoái hoá là thoái hoá → phải fail;
- đo lại tỷ lệ pass tiết kiệm được trên fixture, không claim wall-clock nếu chưa
  đo trong điều kiện kiểm soát;
- required `dotnet test RideBound.slnx` và full pinned Python suite.

### Kỳ vọng định lượng

Từ recorded evidence của H6/E1 panel (chỉ để ước lượng chi phí, **không** phải
tuning input): 94,40% / 97,40% / 93,81% / 97,78% level là thoái hoá ở
Panel A B1 / Panel A C1 / Panel B B1 / Panel B C1. Con số thực tế trên
development panel sẽ khác và phải được đo lại.

### Định nghĩa Done

Quyết định bất biến đã chứng minh bằng differential byte-exact, mutation test
pass, hai baseline pass, evidence ghi rõ level nào bị bỏ qua, và chỉ
`RB-WP14-003` chuyển Ready.

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

### Định nghĩa Done

Capture-on/off differential chứng minh verdict bằng nhau, witness set đầy đủ,
hai baseline pass, chỉ `RB-WP14-004` chuyển Ready.

## 4. Ràng buộc áp cho toàn bộ queue

- không sửa, ghi, move hay xoá bất kỳ raw root H6/E1 nào;
- không dùng số đo của H6/E1 panel để chọn mức factor;
- không mở H7, lifecycle policy v2 hay backfill H6;
- mọi artifact thành công phải exclusive-create sau khi verify đầy đủ, theo
  contract trong
  [`wp13-evidence-retention-and-successor-policy-v1.md`](../benchmarking/wp13-evidence-retention-and-successor-policy-v1.md);
- mỗi ticket Done chỉ mở đúng một ticket kế tiếp.
