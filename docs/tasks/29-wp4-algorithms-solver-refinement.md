# RB-WP4-001 — refinement RideBound policies và solver

> Trạng thái: `DONE`
> Work package: `WP4`
> Implementation WP4 được phép trước khi ticket này DONE: `NONE`
> Kết quả: ADR-023 + ordered queue
> [30-wp4-algorithms-solver-ticket-plan.md](30-wp4-algorithms-solver-ticket-plan.md)
> Nguồn trạng thái: [18-status-and-decision-log.md](../18-status-and-decision-log.md)

## 1. Mục tiêu

Khóa semantics, fairness boundary, candidate/compute budget, solver ownership và
ordered ticket queue cho B2/B3/B4/B5, C1/C2 và OR-Tools trước khi viết code WP4.
Refinement phải biến các hạn chế đã audit của B1 thành work có thể đo, không thay
validator WP3 bằng heuristic hoặc trọng số tùy ý.

## 2. Phạm vi

### In scope

- audit executable B1/C1 boundary sau WP3;
- xác định schedule strategy: earliest-feasible và tối thiểu một đối chứng
  wait/hold có tên;
- candidate loss, solver loss và deadline accounting riêng;
- bounded generation với admissible lower bound, forward slack, cache hoặc
  precomputation có invalidation theo travel snapshot;
- lexicographic/Pareto semantics cho accepted count, hard-vector utilization,
  revision và operational cost;
- B2 penalty, B3 freeze, B4 no-reassignment và B5 multiple-plan fair baselines;
- C1/C2 policy ownership;
- OR-Tools deterministic configuration, status/bound/gap/fallback contract;
- exact-small equivalence và infinite-budget degeneration về B1;
- ordered implementation tickets, dependency graph, rollback và exit gate.

### Out of scope

- viết solver/policy production;
- chọn budget O-002, material threshold O-003 hoặc non-inferiority O-004;
- mở reassignment O-001;
- adapter BeGo/FleetPy/RidePy;
- performance/effectiveness claim;
- dùng horizon 10–15 phút hoặc survey preference làm default số.

## 3. Inputs bắt buộc

1. `00`, `01`, `03`, `04`, `07`, `08`, `15`, `16`, `18`, `19`, `20`, `21`.
2. WP2 plans `25`/`26`, WP3 refinement/plan `27`/`28`.
3. Review code [WP1–WP3](../reviews/wp1-wp3/README.md).
4. Năm nguồn Browser recheck ghi trong `21`, đặc biệt:
   - [Gaul et al. 2021](https://doi.org/10.4230/OASIcs.ATMOS.2021.8);
   - [Schulz & Pfeiffer 2026](https://doi.org/10.1007/s00291-026-00847-0);
   - [Tiwari et al. 2024](https://www.mdpi.com/2071-1050/16/13/5788/html);
   - [Ackermann & Rieck 2025](https://link.springer.com/article/10.1007/s00291-025-00809-y).

## 4. Mười hai quyết định phải khóa

1. **Candidate fairness:** B1/C1 có dùng cùng raw candidates hay cùng generation
   algorithm nhưng C1 prune hard gate; cách report candidate loss là gì?
2. **Schedule semantics:** earliest-feasible có phải distinguished schedule; đối
   chứng wait/hold nào không làm thay đổi physical feasibility?
3. **Bounded generation:** request bound, candidate cap, best-first key và điều
   kiện không silent-omit trong exact mode.
4. **Slack/precomputation:** cache key gồm state/route/travel version nào; khi nào
   invalid; bằng chứng equivalence trước/sau cache.
5. **Repair/reorder:** có cho intra-vehicle remove/reinsert incumbent không; cách
   kích hoạt inversion dimension mà vẫn giữ O-001.
6. **Multiple plan:** plan pool identity, distinguished plan, dominance và
   consensus baseline; không claim least-commitment mới.
7. **Objective:** thứ tự lexicographic/Pareto; nếu dùng scaled integer weights phải
   chứng minh dominance và overflow bound.
8. **C1/C2:** hard gate luôn độc lập objective; warning/soft term không được biến
   hard violation thành feasible.
9. **OR-Tools ownership:** chỉ project `RideBound.Solvers.OrTools`; Domain/
   Application không tham chiếu solver types.
10. **Deadline/fallback:** generation/validation/solver budget, status
    `OPTIMAL/FEASIBLE/UNKNOWN`, best bound/gap và fallback đã validator-pass.
11. **Equivalence:** exact-small oracle bound/seeds; infinite budgets/locks off cho
    C1 phải trả cùng semantic decision B1.
12. **Publication:** solver output không được tự publish; cùng independent WP3
    validator, certificate, pending hash và ACK transaction.

## 5. Research-to-design guard

- Gaul et al. cho thấy rolling-horizon MILP có thể tốt trên instance paper, nhưng
  số 99,5%/30 giây/2,8 giây không phải acceptance target của RideBound.
- Schulz & Pfeiffer hỗ trợ forward slack/reuse/precompute; horizon recommendation
  phụ thuộc instance và reassignment conflict với O-001.
- Tiwari et al. hỗ trợ so weighted/Pareto/lexicographic; WP4 ưu tiên multi-pass
  lexicographic vì dễ audit.
- Ackermann & Rieck yêu cầu B5 plan pool và cảnh báo thêm optimization có thể giảm
  flexibility; WP4 phải đo, không giả định monotonic quality theo solve time.
- Geržinič et al. chỉ hỗ trợ việc đo history/material changes; không chọn user
  profile số.

## 6. BDD

```gherkin
Given B1 và C1 dùng cùng event/state/travel/candidate/compute boundary
When C1 có infinite hard budgets và mọi optional lock tắt
Then semantic decision bằng B1 trong exact-small bound
And mọi khác biệt ngoài bound được phân rã thành candidate loss hoặc solver loss
```

```gherkin
Given một candidate vi phạm một hard commitment dimension
When lexicographic hoặc weighted objective cho operational gain rất lớn
Then candidate vẫn infeasible trước solver ranking
And certificate không thể báo normal operation cho candidate đó
```

```gherkin
Given cached slack/precomputation từ travel snapshot k
When route, position hoặc travel snapshot đổi
Then cache key/invalidation ngăn dùng evidence cũ
And result không cache và có cache bằng nhau trong exact-small test
```

## 7. Artifacts khi ticket DONE

- ADR-023 khóa 12 quyết định;
- một ordered WP4 ticket plan với đúng một implementation ticket kế tiếp `READY`;
- cập nhật `00/08/15/16/18/19/20/21/23`;
- published exact-small bound, deterministic solver settings và performance
  measurement protocol;
- traceability B2–B5/C1/C2 tới code/test/evidence dự kiến;
- không có production code.

## 8. Queue ứng viên để refinement quyết định

Các dòng sau là **workstream ứng viên**, chưa phải ticket `READY` và chưa được phép
implementation trước khi ADR-023 khóa dependency:

1. schedule-strategy experiment;
2. bounded generation + slack/precomputation;
3. B2 marginal/penalty baseline;
4. B3 freeze baseline;
5. B4 O-001-preserving intra-route repair;
6. B5 deterministic multiple-plan pool;
7. C1 hard-vector-aware lexicographic/Pareto selection;
8. C2 soft/hard hybrid;
9. OR-Tools candidate selector;
10. exact-small/infinite-budget equivalence;
11. deadline/fallback/process evidence;
12. WP4 closure.

## 9. Acceptance và verification

- Mười hai quyết định có answer/owner/test/rollback rõ.
- Chỉ một implementation ticket nhỏ nhất có dependency đủ được `READY`.
- Không có numeric default từ paper/survey nếu chưa pilot O-002/O-003.
- O-001 giữ nguyên hoặc phải có ADR superseding riêng; ticket này không được tự mở.
- `dotnet test RideBound.slnx`, format/link/diff check không đổi baseline vì
  refinement không sửa production.
- Claim tiếp tục ở mức implemented/mechanically valid; chưa nâng effectiveness.

## 10. Rollback

Nếu refinement không khóa được fairness, objective dominance hoặc exact-small
equivalence, giữ WP4 `REFINEMENT_IN_PROGRESS`, không tạo solver code và để B1 +
WP3 validator/certificate làm executable reference.

## 11. Completion evidence — 2026-08-03

- In-app Browser đọc lại bốn nguồn bắt buộc và bổ sung Mitrović-Minić & Laporte
  (waiting strategies), Masson–Lehuédé–Péton (forward-time-slack insertion),
  Gschwind (route feasibility/slack) và Ackermann–Rieck 2022 (future insertion
  guidance), cùng official NuGet/OR-Tools CP-SAT status/deadline documentation.
- ADR-023 trả lời đủ 12 quyết định với owner, test và rollback; O-001 giữ nguyên,
  O-002/O-003/O-004 không bị tự chọn.
- Queue `RB-WP4-002..014` có đúng một next implementation ticket `002 READY`.
- Research không cấp default số: test configuration phải gắn nhãn boundary hoặc
  microbenchmark, không phải user preference/production recommendation.
