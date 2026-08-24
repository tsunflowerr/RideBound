# RB-WP13-012 — Full source, logic và claim audit

Ngày audit: 2026-08-24  
Trạng thái: `Done`  
Claim class: assurance/closure; không thêm scientific result

## 1. Kết luận

Audit đọc và bind 80 file ổn định của WP13, đi lại evidence DAG từ raw H6/E1 tới
schema, compact receipt, analyzer, test và report. Kết quả cuối:

- zero unresolved P0/P1/P2;
- một finding P2 thực sự được phát hiện và sửa bằng supplemental verifier guard cùng
  regression mutation, không sửa source verifier E1 đã freeze;
- 45 file Domain/Application được kiểm dependency và 115 analyzer imports được kiểm
  source graph; zero reverse-dependency violation, không thấy simulator-policy
  reimplementation;
- 13 external artifact references đều canonical, gồm 10 active và 3 superseded;
  mọi active edge, length, SHA-256, schema và aggregate invariant đều pass;
- deep raw verification pass cho 100 H6 bundle và 80 E1 arm;
- 24 high-risk claim-term occurrences trong 12 tài liệu đều nằm trong caveat hoặc
  prohibited context; unsafe conclusion count bằng 0.

Kết luận này chỉ đóng assurance gate của WP13. Nó không rescue H6, không tạo CI hay
population/causal inference, không chọn policy v2 và không tự mở WP14.

## 2. Receipt và inventory source-controlled

External canonical audit receipt:

- path: `E:\RideBoundData\wp13\wp13-full-source-logic-claim-audit-v1.json`;
- length: 25.437 byte;
- SHA-256:
  `cd8639884fc2c421a15e137b21522cde3b2fcc80f9b7eebbdcc2454b373fad76`;
- schema: `benchmarks/schemas/wp13/v1/full-source-logic-claim-audit.schema.json`;
- schema SHA-256:
  `323aec7a2bc34fdc61fa9bce9287f64361f958c1d4b7d70eec760442999a0ea4`.

Source-controlled compact receipt và exact path/role/SHA inventory nằm tại
`benchmarking/evidence/wp13-full-source-logic-claim-audit-v1-summary.json`. Inventory
có 80 file, 1.076.880 byte và SHA-256 tổng hợp
`33d89c17caf1596ef943cd2714bacdfd820c33ca266d2cf2bef3efe5371010c4`.

| Nhóm | File | Dòng | Byte |
|---|---:|---:|---:|
| Compact evidence | 9 | 9 | 28.009 |
| Core instrumentation | 10 | 6.344 | 219.138 |
| .NET regression tests | 5 | 2.772 | 107.971 |
| Frozen configurations | 5 | 1.487 | 77.838 |
| Python analyzers | 14 | 9.649 | 357.915 |
| Python regression tests | 14 | 3.376 | 121.410 |
| Schemas | 11 | 1.934 | 102.441 |
| Stable research/reports | 12 | 1.220 | 62.158 |
| **Tổng** | **80** | **26.791** | **1.076.880** |

Audit analyzer SHA-256 là
`6c91535d7571d8ac8fd8171a8a275b621c2a8f397cd55bfcaaaf329c7f90bcd4`;
test source SHA-256 là
`e2e4933e1189527c000499d60c4e3b0e84fc63b0aa80ce4516395ea64e184626`.

## 3. Review code và contract

### 3.1. C# instrumentation

`CandidatePortfolio` giữ snapshot độc lập của request/schedule arrays; generated và
eligible candidates phải cùng vehicle; eligible là identity-preserving subset của
generated; selection options phải đúng exact eligible set; selected candidate phải
trỏ đúng feasible solution. `RoutePlan` và `CandidateSelectionProblem` tiếp tục giữ
copy/immutable boundary.

Retained-portfolio capture là opt-in:

- H6 historical evidence giữ version `1.0.0`;
- profile off giữ version `1.1.0` và operational behavior cũ;
- chỉ profile on mới phát `1.2.0` với portfolio.

V1.0/v1.1 reject portfolio; v1.2 decode, validate và canonicalize đầy đủ nested
candidate, route, schedule, objective và selection bindings. E1↔H6 operational
projection bằng nhau 80/80 arm, zero mismatch; instrumentation không đổi quyết định.

### 3.2. Python analyzers và repository boundaries

Analyzer chỉ đọc protocol/evidence artifacts; không import OptiGo/FleetPy policy hay
solver để tái hiện quyết định. Raw H6/E1 roots bị cấm làm output; audit output dùng
exclusive creation và chỉ được tạo sau khi mọi check pass. Domain/Application không
phụ thuộc EF Core, ASP.NET, map provider, OR-Tools hoặc simulator library.

Historical pre-E1 analyzers đều bảo vệ raw roots nhưng không phải mọi derived output
đều dùng exclusive creation. Đây là limitation P3 có owner `RB-WP13-013`, không phải
lỗi raw-evidence immutability.

### 3.3. Signature, objective và association semantics

Candidate semantic signature loại đúng ba policy/reporting field: `candidateId`,
`policyEligibility` và `objectiveContributions`; mọi physical route/schedule/request
semantics vẫn nằm trong signature. Generated set bằng nhau 40/40 pair, zero candidate
ID drift.

Objective vectors B1/C1 dùng profile khác nhau nên không được so trực tiếp. Within-
vehicle ordinal chỉ là mô tả nội bộ, không phải global fleet rank. Association rows
có overlap, vì vậy tổng naive không được dùng làm service decomposition. Exact labels
được bắt buộc xuyên schema/artifact/report:

- `overlappingCellsNotAdditive`;
- `trajectoryAssociatedNotCausal` / `descriptiveAssociationNotCausal`;
- `notComparableAcrossObjectiveProfiles`;
- `notApplicableCannotRescueH6`.

## 4. Finding P2 đã sửa

### WP13-AUDIT-P2-001

Frozen E1 Python verifier tự nó đã accept một invalid optional field như
`repairedIncumbentRequestId: 123`; historical equality check cũng có thể coi boolean
`levelIndex` như integer. C# decoder và JSON Schema đã reject các giá trị này, nhưng
closure không được phép dựa vào việc hai lớp khác vô tình bù cho verifier thiếu.

Resolution giữ provenance đúng:

- không sửa frozen E1 verifier;
- closure audit compose historical verifier với supplemental canonical-integer,
  optional-field và exact UTF-8 128-byte identifier guard;
- regression reject wrong-type/overlength `repairedIncumbentRequestId` và boolean
  objective `levelIndex`;
- schema tiếp tục được kiểm độc lập trên fixture contract và external reports.

Sau resolution, unresolved P0/P1/P2 đều bằng 0. Evidence tương lai phải dùng successor
verifier có version thay vì mutate source đã freeze.

## 5. Evidence DAG và deep raw verification

Audit tái lập 9 compact summaries, 11 schemas và 13 external artifacts. Historical H6
verifier hash được recover từ Git HEAD là
`3eebec96b8370db2c4879adeaede3e67b7344571299a496953afcbc599dd93e5`;
current E1 verifier hash là
`89a9e9a797e7d7f004490bff3bc37da14cd792c14ff60513873ed51b96c06a17`.
Toàn bộ 62 repository files trong E1 freeze receipt match exact, zero mismatch.

Deep H6 check:

- 2 panel, 100 bundle, 57.806 solver decisions;
- exact panel objects bằng evidence authority.

Deep E1 check:

- 80 arm, 8.640 requests, 44.156 solver decisions và 44.156 portfolios;
- exact run và panel objects bằng evidence authority;
- repository inventory SHA-256:
  `22f4914e9f61163f8e33089a2f24786bcd4bf0b4c50d42a860fbf8916a3f6afb`.

Aggregate identities vẫn là Panel A `-154`, Panel B `-106`, 40 paired cells, 41
actionful links và 40/40 equal generated-signature sets. Không aggregate nào được đổi
thành counterfactual completion hoặc causal share.

## 6. Claim audit và resource boundary

Static scan đọc 12 stable research/report documents. Có 24 occurrence của các term
nhạy cảm; occurrence inventory SHA-256 là
`d145515c37d9ac53b67f0212a7ac15de5309d598082727098aa6f2fc96080b8a`.
Tất cả đều nằm trong caveat/prohibited context; unsafe conclusion count bằng 0.

E1 chiếm 5.516.098.710 raw byte. Đây là chi phí instrumentation/retention quan sát
được, không phải performance benchmark hay lý do tự động thay policy. `RB-WP13-013`
phải quyết định retention/archive policy trước bất kỳ WP14 replay nào.

## 7. Verification

Closure gates:

- targeted audit/mutation tests: 8/8, zero skip;
- required `dotnet test RideBound.slnx`: 860/860, zero skip;
- full sequential pinned CPython/FleetPy suite: 199/199, zero skip;
- canonical/schema validation cho external receipt và mọi changed JSON/schema: pass;
- `dotnet format RideBound.slnx --verify-no-changes`: pass;
- `git diff --check`, Markdown link/fence và Python line-length gates: pass.

Ba P3 limitation có owner `RB-WP13-013`: exclusive-create policy cho successor
derived analyzers, retention/archive của 5.516 GB E1, và versioned successor verifier.
`RB-WP13-012 Done`; chỉ `RB-WP13-013` được mở để lập closure evidence và quyết định có
mở WP14 hay không.
