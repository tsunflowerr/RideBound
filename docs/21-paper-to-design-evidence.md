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
