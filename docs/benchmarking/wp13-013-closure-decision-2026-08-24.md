# RB-WP13-013 — WP13 closure và WP14 readiness decision

Ngày quyết định: 2026-08-24  
Trạng thái: `Done`  
Verdict: `openExploratoryAblationOnly`

## 1. Kết luận

WP13 đạt exit gate và được đóng. WP14 được phép mở **chỉ** để refine/freeze một
exploratory ablation và Pareto-frontier program trên development namespace/cells mới.
Quyết định này không chọn policy v2, không mở H7, không sửa/rerun H6 và không biến
candidate association thành causal mechanism.

Canonical closure manifest:

- `benchmarking/evidence/wp13-closure-decision-v1.json`;
- length 4.463 byte;
- SHA-256
  `4e410e2311caa4073ed219ae57b47323e3159540001298a6a085cf0858a72c9c`;
- strict schema
  `benchmarks/schemas/wp13/v1/closure-decision.schema.json`, SHA-256
  `a4cbfb1438b672f10ea976b9abfe56af10617de2ba37a96b178ac618c9bffaca`.

## 2. WP13 exit gates

| Gate | Verdict | Evidence |
|---|---|---|
| Source/logic/claim audit | Pass | 80 file, zero unresolved P0/P1/P2 sau P2 regression |
| Provenance | Pass | 10 active + 3 superseded receipts resolve exact; 62/62 E1 freeze files match |
| Architecture | Pass | 45 Domain/Application files, 115 analyzer imports, zero reverse dependency |
| Claim boundary | Pass | 12 docs, 24 caveat/prohibited occurrences, zero unsafe conclusion |
| Raw immutability | Pass | deep read-only H6/E1 verification; closure không move/delete/rewrite raw |
| Resource readiness | Pass có guard | E1 5.516 GB retained/frozen; WP14 phải stage và declare envelope trước run |
| WP14 separation | Pass có constraint | development cells/namespace mới; H6 panels bị loại khỏi configuration selection |

H6 negative result vẫn là authority: Panel A `-154`, Panel B `-106`; burden gần zero
không được mô tả như quality optimization. WP13 chỉ cho biết generated sets bằng nhau
40/40 pair và phân loại observed candidate associations, không cho causal shares hay
counterfactual completions.

## 3. Ba limitation P3 đã có resolution

### WP13-AUDIT-P3-001 — Derived-output transaction

Future success artifacts bắt buộc raw-root rejection, complete verify trước write,
canonical UTF-8/LF, exclusive create và explicit supersession metadata. Analyzer lịch
sử không bị mutate; exact receipt/hash giữ authority.

### WP13-AUDIT-P3-002 — E1 retention/archive

Giữ và freeze toàn bộ 5.516.098.710 verified raw bytes; WP13 không archive/delete.
Future migration cần source/destination byte-exact inventories, ít nhất một verified
copy và independent verification trước cutover.

### WP13-AUDIT-P3-003 — Successor verifier

Frozen E1 verifier hash tiếp tục bất biến. Evidence execution tương lai cần versioned
successor giữ historical checks và thêm canonical integer, optional field/omission,
1–128 UTF-8 byte identifier, strict schema và regression vectors đã tìm ở `012`.

Contract đầy đủ nằm tại
`benchmarking/wp13-evidence-retention-and-successor-policy-v1.md`, length 3.751 byte,
SHA-256 `fe10a9bef62cffa8974c65900e561a56f26ab200cb4ba48c663e2d8bb91e656e`.

## 4. Vì sao WP14 được mở

### Scientific need

H6 fail ở cả 8 và 4 vehicles. WP13 xác nhận cost xuất hiện dù generated candidate
sets bằng nhau và phân bố qua recorded lock/budget prunes cùng eligible-not-selected
associations. Chỉ descriptive evidence hiện tại không trả lời frontier nào giữ service
tốt hơn với burden thực sự giảm; một paired development ablation là bước đúng tiếp
theo.

### Evidence sufficiency

E1 v1.2 có exact generated/eligible/selected portfolio, full route/schedule và
solver-neutral objective inputs cho 44.156 decisions; instrumentation-only equality
E1↔H6 pass 80/80. Evidence đủ để thiết kế factors và falsification gates, nhưng chưa
đủ để chọn policy hoặc causal estimator.

### Leakage và resource separation

WP14 phải:

- dùng new development namespace/cells; H6 Panel A/B không tham gia tuning/selection;
- freeze factor matrix, denominator, analyzer, output/resource envelope trước outcome;
- báo paired service–burden Pareto frontier, không chọn hậu outcome bằng một scalar;
- chạy schema/tiny dry-run trước matrix, estimate bytes per decision và có cancellation
  receipt;
- không authorize H7 hoặc lifecycle policy v2.

Nếu một constraint không thể freeze hoặc resource envelope không đạt, WP14 execution
phải fail closed; quyết định mở refinement không phải quyết định chạy matrix.

## 5. Verification và handoff

- independent closure/schema/hash/mutation tests: 6/6, zero skip;
- required `dotnet test RideBound.slnx`: 860/860, zero skip;
- full sequential pinned CPython/FleetPy suite: 205/205, zero skip;
- exact active/superseded external artifact length/SHA resolution: pass;
- canonical JSON, Draft 2020-12 schema, format/diff/Markdown/link/line gates: pass.

`RB-WP13-013 Done`; WP13 `001..013` hoàn thành. `RB-WP14-001` được mở chỉ để đọc lại
full-PDF boundary, refine factors/gates và lập ordered queue/freeze plan; WP15–WP20 vẫn
roadmap-level.
