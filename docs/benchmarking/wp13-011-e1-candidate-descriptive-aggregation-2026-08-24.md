# RB-WP13-011 — E1 candidate descriptive aggregation

> Ngày phân tích: 2026-08-24  
> Trạng thái: `Done`  
> Claim class: post-outcome finite-panel association; không causal/population

## 1. Kết quả chính

Analyzer đọc/xác minh lại đủ 80 E1 bundle và 44.156 solver decision trước khi lấy
80 portfolio ở exact first-divergence epochs. Policy-neutral semantic signature cho
kết quả:

- generated candidate set bằng nhau ở 40/40 B1/C1 pair;
- 390 signature/arm, 780 candidate observations; zero b1-only/c1-only;
- zero signature collision và zero candidate-ID drift;
- 41 B1 actionful selected links: 33 C1-pruned, 7 C1-eligible-not-selected,
  1 C1-selected, zero absent.

Bảy link mà H6 v1.0 chỉ có thể ghi `absentRetainedOrOmittedNotRecorded` nay được giải
quyết bằng evidence thật: cả bảy đều có cùng semantic candidate trong C1 và đều
eligible, nhưng không nằm trong global selected set. Không link nào mất ở generation.

## 2. Panel và candidate classification

| Panel | Xe | Pair | Link | C1 pruned | C1 eligible, không chọn | C1 chọn | Absent |
|---|---:|---:|---:|---:|---:|---:|---:|
| A | 8 | 20 | 21 | 17 | 3 | 1 | 0 |
| B | 4 | 20 | 20 | 16 | 4 | 0 | 0 |
| **Tổng** | — | **40** | **41** | **33** | **7** | **1** | **0** |

Prune codes giữ đúng recorded witness: 28 `COMMITMENT_BUDGET_EXCEEDED` và 5
`COMMITMENT_PHASE_LOCK`. Cross-tab với immediate acceptance:

| Recorded class | C1 lower immediate | Equal immediate |
|---|---:|---:|
| budget prune | 7 | 21 |
| phase-lock prune | 1 | 4 |
| eligible-not-selected | 0 | 7 |
| selected-by-C1 | 1 | 0 |

Mọi 8 cell C1-lower có một B1-selected candidate bị C1 prune, nhưng 25 link pruned
khác nằm trong cell có immediate accepted count bằng nhau. Do đó recorded prune là
đồng xuất hiện nhất quán với loss tại divergence, không phải sufficient cause hay
decomposition. Cell `A/d20181116-s10-r1` có hai B1 actionful links: C1 chọn một và
prune một, nên row `selectedByC1` vẫn đi cùng `c1LowerImmediateAcceptance`.

## 3. Objective/ranking boundary

B1 dùng `rollingCost`, C1 dùng `hardVector`; raw objective vectors có số chiều và
ý nghĩa khác nhau nên analyzer cấm trừ hoặc xếp chung. Ordinal chỉ được tính trong
cùng arm, cùng vehicle và mang nhãn
`withinVehicleDescriptiveNotGlobalSolverRank`.

Trong bảy C1-eligible-not-selected links, ordinal within-vehicle của C1 là:

| Ordinal | Link |
|---:|---:|
| 1 | 4 |
| 2 | 2 |
| 3 | 1 |

Bốn candidate đứng đầu trong vehicle riêng nhưng vẫn không được global solver chọn,
phù hợp với fleet-wide request-disjointness/coupling. Vì vậy gọi bảy trường hợp này
là “ranking loss” hoặc “search omission” sẽ quá mức evidence; tên được giữ là
`eligibleNotSelectedAssociation`.

## 4. Trajectory association không cộng được

Exact H6 outcomes được bind qua receipt equivalence và raw manifest:

| Panel | Arrivals/arm | B1 completed | C1 completed | Delta |
|---|---:|---:|---:|---:|
| A | 2.160 | 1.735 | 1.581 | −154 |
| B | 2.160 | 966 | 860 | −106 |

Association rows giữ nhãn `trajectoryAssociatedNotCausal`:

| Candidate class | Immediate relation | Cell | Link | Sum completed delta |
|---|---|---:|---:|---:|
| pruned | C1 lower | 8 | 8 | −49 |
| pruned | equal | 25 | 25 | −151 |
| eligible-not-selected | equal | 7 | 7 | −60 |
| selected-by-C1 | C1 lower | 1 | 1 | −6 |

Các row **overlap theo cell và không additive**. Cộng ngây thơ cho −266 trong khi hai
panel thực là −260 vì một cell nằm ở hai class. Bảng này không ước lượng candidate
counterfactual completion, không chia service loss theo mechanism và không được dùng
để cứu/thay confirmatory H6 gate.

## 5. Artifact và provenance

External canonical report:
`E:\RideBoundData\wp13\e1-candidate-descriptive-aggregation-v1-closure.json`,
116.985 byte, SHA-256
`0eba293c61ae7be8cba52c5c3085b6fb50807b4381a28f5ddb9b3a3464fddc1c`.
Compact source-controlled receipt:
[`wp13-e1-candidate-descriptive-aggregation-v1-summary.json`](evidence/wp13-e1-candidate-descriptive-aggregation-v1-summary.json).

| Thành phần | SHA-256 |
|---|---|
| analyzer | `d938d3d70287b033a91fa373b8271d3fb7e566cf242bc9d998c5ebc0b7b7a906` |
| strict schema | `8bb5a0db81e5c1ca1301ba1459b630bc7f33b0af24c41844cb579028d7351984` |
| tests | `de13c824babf7b96905bf49fec93bae4654cf095cf5ea14db9818fce5bf6163d` |
| independent verifier | `89a9e9a797e7d7f004490bff3bc37da14cd792c14ff60513873ed51b96c06a17` |

Inputs bind exact record set `bef27519…25618`, comparator `3717f093…4f7e3`, E1
inventory `a029b978…4674`, falsification `78bf6313…77785`, equivalence
`4abb24f0…babfc` và repository inventory `22f4914e…f6afb`.

## 6. Review và verification

Review artifact đầu 116.937 byte SHA `eefcfc01…cbb2a` phát hiện association rows có
cell overlap nhưng claim boundary chưa nói rõ non-additivity. Artifact đó được giữ
và superseded. Closure bổ sung `overlappingCellsNotAdditive`, observed-input
four-way binding và current-verifier equality với cả hai receipt `010`.

Verification cuối:

- targeted signature/classification/ordinal/denominator/schema tests: 4/4;
- independent E1 full scan: 80/80 bundle, 44.156 decisions, 80 target portfolios;
- exact 40-record/41-link reconciliation và H6 Panel A/B totals;
- required `dotnet test RideBound.slnx`: 860/860, zero skip;
- full sequential pinned CPython/FleetPy suite: 191/191, zero skip;
- `dotnet format --verify-no-changes`, `git diff --check`, schema/JSON/Markdown và
  Python line gates: pass.

`RB-WP13-011 Done`; chỉ `RB-WP13-012` được mở cho full source/logic/claim audit.
