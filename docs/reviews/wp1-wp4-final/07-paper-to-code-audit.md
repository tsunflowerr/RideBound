# Paper-to-code optimization audit

Nguồn đầy đủ, ngày Browser recheck và claim boundary nằm ở
[../../21-paper-to-design-evidence.md](../../21-paper-to-design-evidence.md). Bảng
dưới chỉ trả lời: cơ chế nào đã thành code/test thật, và paper **không** cho phép
kết luận gì.

| Evidence | Code/decision đã áp dụng | Kiểm chứng | Không được suy diễn |
|---|---|---|---|
| [Gaul et al. 2021](https://doi.org/10.4230/OASIcs.ATMOS.2021.8) | rolling/event boundary, exact-small oracle, bounded solve/deadline | generator oracle, explicit work caps, microbenchmark | 30 giây hay reported insertion rate là default/universal result |
| [Schulz & Pfeiffer 2026](https://doi.org/10.1007/s00291-026-00847-0) | forward slack, cache, reuse boundary, future-potential guidance | cached/uncached equivalence, travel/route invalidation | horizon 10–15 phút hoặc policy effectiveness trên RideBound |
| [Tiwari et al. 2024](https://www.mdpi.com/2071-1050/16/13/5788/html) | auditable lexicographic/Pareto mechanisms | multi-pass objectives, B5 dominance/diversity tests | weighted score bất kỳ là fair hoặc hard violation có thể trade off |
| [Ackermann & Rieck 2025](https://doi.org/10.1007/s00291-025-00809-y) | B5 plan pool, consensus distinguished plan, flexibility warning | canonical pool/checkpoint/tamper, diversity/consensus tests | least-commitment/consensus là novelty; tối ưu lâu hơn luôn tốt hơn |
| [Mitrović-Minić & Laporte 2004](https://doi.org/10.1016/j.trb.2003.09.002) | named origin-hold relocated wait | executable waypoint + exact service/cost equivalence | một waiting strategy luôn tốt nhất |
| [Masson et al. 2013](https://doi.org/10.1016/j.orl.2013.01.007) | conservative forward-time-slack + update/cache discipline | mutation/invalidation and full revalidation | slack certificate thay thế complete DARP validator |
| [Gschwind 2019](https://doi.org/10.1007/s00291-018-0544-0) | incremental temporal feasibility boundary | route/travel identity key and equivalence | simplified profile chứng minh mọi capacity/assignment/commitment constraint |
| [Ackermann & Rieck 2022](https://doi.org/10.1007/978-3-031-08623-6_42) | slack reserve/diversity là secondary guidance | deterministic cap/pool ranking | distance/flexibility surrogate chắc chắn tăng future acceptance |
| [OR-Tools CP-SAT](https://developers.google.com/optimization/cp/cp_solver) | integer model, distinct statuses, bounds | actual adapter differential and gap checks | `FEASIBLE/UNKNOWN` là `OPTIMAL` hoặc wall timeout là replay-stable |

## Những tối ưu đã executable

- bounded best-first exploration thay vì request-ID/hash truncation;
- exact omission count/digest/saturation và stage-separated loss;
- forward slack + full invalidation cache;
- executable origin hold với equivalence gate;
- B4 same-vehicle atomic remove/reinsert;
- B5 versioned Pareto/diverse/consensus pool;
- C1 hard-feasible lexicographic utilization/revision objective;
- C2 explicit warning excess trong cùng hard set;
- exact multi-pass integer CP-SAT với truthful status/bound/gap;
- independently validated deterministic fallback;
- config/manifest/replay binding cho mọi mechanism.

## Những thứ cố ý không làm

- weighted scalar cho ten-dimensional hard vector;
- candidate set riêng ưu ái C1;
- cross-vehicle incumbent reassignment;
- lấy horizon/pool/budget/weight từ một paper làm hidden default;
- dùng wall-clock deadline để quyết replay outcome;
- cho solver/plan-pool publish thẳng;
- gọi route similarity, least commitment, ETA limits, dynamic insertion hoặc user
  satisfaction là novelty.

## Đánh giá “tối ưu thực sự”

Các thay đổi làm giảm/restructure search work và mở mechanism neighborhood thật,
không chỉ thêm điều kiện:

- best-first đổi thứ tự mở cả cây tổ hợp và có exact frontier accounting;
- slack/cache tránh chiếu lặp nhưng được thiết kế để không đổi result;
- repair tạo route permutations mới mà B1 insertion-only không có;
- multiple-plan giữ state tương lai thực và checkpointable;
- CP-SAT giải global request/vehicle coupling theo ordered objectives;
- fallback là portfolio được validate, không phải `if timeout then accept old`.

Tuy nhiên “tối ưu thực sự” về implementation không đồng nghĩa “hiệu quả hơn trên
dữ liệu thật”. Kết luận thứ hai cần paired replay/pilot ở WP5–WP9.
