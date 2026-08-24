# WP13 — Post-H6 mechanism diagnostics ordered queue

> Work package: `WP13 COMPLETE`
> Refinement: `RB-WP13-001 DONE`
> Completed implementation tickets: `RB-WP13-002..013 DONE`
> Active implementation ticket: `none`
> Quy tắc: một ticket implementation active; ticket xa chỉ được refine khi queue head đóng

## 1. Queue

| ID | Kết quả review được | Trạng thái | Dependency |
|---|---|---|---|
| RB-WP13-001 | ADR-053, full-PDF boundary, sufficiency matrix, ordered queue | Done | WP9/WP10 |
| RB-WP13-002 | Canonical H6 evidence inventory và equal-observed-input alignment contract | Done | 001 |
| RB-WP13-003 | Versioned first-divergence record/schema trên exact paired units | Done | 002 |
| RB-WP13-004 | Paired behavioral comparator và immediate service classification | Done | 003 |
| RB-WP13-005 | Exact minimal-relaxation calculator từ recorded prune witnesses | Done | 004 |
| RB-WP13-006 | Mechanism classifier: physical, lock, budget, ranking/search omission, unknown | Done | 005 |
| RB-WP13-007 | H6-supported option-set covariates và explicit missing-field report | Done | 006 |
| RB-WP13-008 | Runner evidence vNext cho retained portfolio, chỉ nếu 007 chứng minh cần | Done | 007 |
| RB-WP13-009 | Exploratory paired replay freeze cho instrumentation mới | Done | 008 |
| RB-WP13-010 | Independent verifier và mutation/falsification matrix | Done | 009 |
| RB-WP13-011 | Cell/pair descriptive aggregation; không CI/population claim | Done | 010 |
| RB-WP13-012 | Full source/logic/claim audit của WP13 | Done | 011 |
| RB-WP13-013 | Closure evidence và decision mở/không mở WP14 | Done | 012 |

Mỗi ticket dự kiến 0,5–2 ngày và chỉ có một output chính. `009` chỉ được execute sau
khi freeze manifest đã được hash/verify; cancellation phải có evidence, không phải đoán.

## 2. RB-WP13-002

### Purpose

Chứng minh bằng máy H6 có những field nào cho mechanism diagnostics, đồng thời tạo
projection không nhầm policy-bearing state hash với hành vi khác nhau.

### Scope

- đọc raw H6 bundle read-only và kiểm declared inventory/receipts;
- pair B1/C1 theo frozen unit identity, không theo solver seed như replicate;
- emit coverage counts cho generation/pruned/selection/action fields;
- lockstep compare observed input, wire decision và operational decision projections;
- emit canonical JSON report có source SHA-256 và sufficiency verdict.

Ngoài scope: causal attribution, policy rerun, candidate reranking, thay C# solver,
aggregate scientific conclusion.

### BDD

- Given hai arm chỉ khác policy/state/decision hash nhưng event/action projection bằng
  nhau, when analyzer so sánh, then không báo false divergence.
- Given một operational action đổi trên cùng observed batch, then record đúng epoch và
  `operationalDecisionDivergenceOnEqualObservedInput`.
- Given chỉ generated publication ID/order đổi nhưng semantic publications giống,
  then ghi wire-only difference và tiếp tục tìm operational divergence.
- Given observed events khác trước decision comparison, then record
  `observedInputDivergence` và không gắn same-state label.
- Given manifest/transcript field bị thiếu, receipt sai hoặc arm không pair được, then
  fail closed và không tạo success report.

### Acceptance

- schema/report deterministic và canonical;
- fixture/mutation tests bao phủ bốn BDD;
- exact raw roots Panel A/B có report inventory;
- không ghi/sửa file trong H6 roots;
- full FleetPy tests và required .NET solution tests pass;
- ticket chuyển Done, `RB-WP13-003` là ticket Ready duy nhất.

### Closure

- analyzer/mutation suite 16/16 và full pinned FleetPy suite 111/111 pass, zero skip;
- raw H6 scan pass 100/100 bundle, 57.806 decision, với external report/tool hashes;
- required `dotnet test RideBound.slnx` pass 856/856 sau exact allocation-free
  canonical-number marker optimization; 120-second ceiling không đổi;
- `RB-WP13-002 Done`; `RB-WP13-003 Ready` là queue head duy nhất.

## 3. RB-WP13-003

### Purpose

Nâng first-divergence projection đã kiểm chứng ở `002` thành contract có version cho
từng exact B1/C1 paired unit, để các ticket mechanism sau không đọc cấu trúc report
ad-hoc hoặc lẫn `null` với evidence thật sự vắng.

### Scope

- consume đúng canonical inventory report của `002`, bind length/SHA/tool/panel
  inventory và claim boundary trước khi project record;
- phát JSON Schema Draft 2020-12 strict cho record và record set;
- emit một canonical record cho mỗi Panel A/B primary pair, có classification,
  observed-input relation, projection hashes/action/event types khi recorded;
- dùng field presence + conditional schema cho arm/epoch không tồn tại; không dùng
  `null` placeholder để bịa evidence;
- bind schema SHA và generator source SHA trong record set.

Ngoài scope: đọc/rerun solver, candidate-pool equality, minimal relaxation, mechanism
classification, downstream completion delta hoặc causal attribution.

### BDD

- Given operational decision khác trên equal observed input, then record relation
  `equal` và giữ đủ B1/C1 projection hashes/epoch/action types.
- Given observed input khác, then relation là `different`, không same-state claim.
- Given transcript length khác, then relation là `notComparable`; evidence arm thiếu
  bị omit và schema vẫn fail nếu record còn khai equality.
- Given không thấy divergence, then record chỉ có classification `noneObserved` cùng
  prefix length, không giả epoch/hash.
- Given source report hash/shape/panel inventory/claim boundary/duplicate unit bị đổi,
  then generator fail closed và không phát success artifact.

### Acceptance

- schemas strict, self-contained, source controlled và có mutation tests;
- canonical record set deterministic, 40/40 exact pair, classification counts khớp
  report `002` và mọi record bind source/schema/tool hashes;
- output nằm ngoài immutable H6 roots; source report không bị sửa;
- targeted/full pinned Python và required .NET solution tests pass;
- ticket chuyển Done, `RB-WP13-004` là ticket Ready duy nhất.

### Closure

- strict self-contained Draft 2020-12 schema và generator bind exact report `002`;
- canonical output 40/40 record, Panel A/B 20/20, artifact SHA
  `bef27519…25618`; classification count 40 operational divergence trên equal input;
- targeted 11/11, full pinned Python 122/122, independent schema/binding/invariant
  check và required Debug 856/856 pass;
- `RB-WP13-003 Done`; `RB-WP13-004 Ready` là queue head duy nhất.

## 4. RB-WP13-004

### Purpose

Đọc lại đúng first-divergence epoch từ raw primary transcripts, bind nó với 40 record
`003`, rồi mô tả khác biệt action-level và immediate request disposition mà không gọi
đó là downstream service effect hoặc causal mechanism.

### Scope

- verify exact record-set/schema/generator/report identities và frozen Panel A/B
  inventory trước comparison;
- scan 80 primary transcripts tới EOF, verify frame/file receipts và solver evidence;
- bind target epoch/time cùng observed/wire/operational projection hashes về record
  `003`;
- compare request dispositions, accepted vehicle assignment, vehicle-plan,
  publication, solver status và remaining action subsets;
- emit ordered difference classes, immediate accepted-count relation và per-request
  comparison không `null`.

Ngoài scope: completed outcomes, candidate/prune witness attribution, minimal
relaxation, rerank, causal effect, CI hoặc population inference.

### BDD

- Given B1 accepted và C1 rejected cùng arrived request, then class
  `requestDispositionDifference` và relation `c1LowerImmediateAcceptance`.
- Given cả hai accepted nhưng vehicle khác, then class
  `acceptedVehicleAssignmentDifference` với equal immediate acceptance count.
- Given disposition/vehicle bằng nhưng plan khác, then class `vehiclePlanDifference`.
- Given chỉ publication hoặc solver/other actions khác, then giữ exact typed class,
  không biến thành service loss.
- Given duplicate/missing outcome, target hash/epoch, inventory hoặc transcript receipt
  sai, then fail closed và không emit success artifact.

### Acceptance

- 40/40 pair bind exact record/raw evidence và có ít nhất một difference class;
- aggregate counts reconcile từ record-level data; immediate vs trajectory boundary
  explicit;
- targeted mutation tests, full pinned Python và required .NET solution pass;
- ticket chuyển Done, `RB-WP13-005` là ticket Ready duy nhất.

### Closure

- exact comparator bind record-set/schema/generator/report và frozen Panel A/B;
- full scan 80/80 primary transcript, 44.156/44.156 decisions verified tới EOF;
- 40/40 comparison records: Panel A immediate C1-lower 3, equal 17; Panel B
  C1-lower 5, equal 15; không C1-higher, per-request `null` count bằng 0;
- targeted 13/13, full pinned Python 135/135, independent canonical/hash/aggregate
  check và required Debug 856/856 pass;
- `RB-WP13-004 Done`; `RB-WP13-005 Ready` là queue head duy nhất.

## 5. RB-WP13-005

### Purpose

Link từng B1 actionful selected candidate ở exact first-divergence epoch với C1 raw
execution evidence, rồi tính mức tối thiểu để xóa **recorded witness hiện tại** khi
field đủ. Không gọi kết quả đó là candidate feasibility hoặc policy relaxation đủ.

### Scope

- bind exact `004` comparator report/source, record `003`, inventory analyzer và frozen
  Panel A/B trước khi đọc witness;
- scan 80 primary transcripts tới EOF, bind raw target và candidate identity; bắt buộc
  `requestAccepted.candidateId` khớp `vehiclePlanUpdated` cùng vehicle;
- phân biệt `prunedWithCommitmentWitness`, `selectedByC1`,
  `absentRetainedOrOmittedNotRecorded` và non-commitment prune;
- với budget witness, verify `after = before + delta`, `after > limit` và emit exact
  `requiredLimit = after`, `additiveLimitIncrease = after - limit`;
- với lock witness, emit categorical rule/dimension phải được disable; không tạo số
  giả cho categorical relaxation;
- emit strict versioned canonical record set, aggregate reconciliation và explicit
  validator fail-fast boundary.

Ngoài scope: tuyên bố candidate feasible sau relaxation, tìm blocker kế tiếp, route/
schedule reconstruction, retained-portfolio inference, rerank, policy rerun, downstream
service effect, CI hoặc population inference.

### BDD

- Given exact B1 candidate bị C1 prune bằng budget witness hợp lệ, then emit đúng
  required limit và additive integer increase cho từng recorded dimension.
- Given lock witness, then emit exact rule/dimension categorical disablement và không
  có numeric amount.
- Given candidate cũng được C1 select, then label `selectedByC1`, không tạo relaxation.
- Given candidate không có trong C1 selected action hoặc prune witness, then label
  `absentRetainedOrOmittedNotRecorded`, không suy là retained/omitted.
- Given duplicate candidate ID, accepted/plan mismatch, malformed arithmetic, target/
  receipt/source identity drift, then fail closed và không emit success artifact.

### Acceptance

- all 40 comparison records và mọi B1 actionful selected candidate được accounted
  exactly once; record/aggregate counts reconcile;
- every calculated relaxation traces tới exact candidate + raw witness hash; records
  không `null` và feasibility remains `notEvaluated`;
- targeted mutation, full pinned Python và required .NET solution pass;
- ticket chuyển Done, `RB-WP13-006` là ticket Ready duy nhất.

### Closure

- exact two-pass scan bind 40 records/41 B1 actionful selected candidates;
- 33 commitment-pruned, 7 absent/not-recorded, 1 selected-by-C1; 28 numeric budget
  clearances và 5 categorical lock clearances;
- numeric additive clearance min/median/max 10.128/93.060/301.765 ms; feasibility
  remains `notEvaluated` và retained portfolio `notRecorded`;
- canonical artifact SHA `cdd9a28d…9e411`; targeted 11/11, pinned Python 146/146,
  independent verification và required Debug 856/856 pass;
- `RB-WP13-005 Done`; `RB-WP13-006 Ready` là queue head duy nhất.

## 6. RB-WP13-006

### Purpose

Merge exact behavioral record `004` với candidate/witness record `005` thành
evidence-supported mechanism classes tại first divergence, giữ riêng immediate
acceptance relation và không đổi class thành causal attribution.

### Scope

- bind exact canonical inputs/tool/schema hashes và one-to-one 40 panel/unit records;
- classify links thành `recordedBudgetWitness`, `recordedLockWitness`,
  `recordedPhysicalPruneCode`, `sharedSelectedCandidate`,
  `rankingOrSearchOmissionIndeterminate` hoặc `unsupportedRecordedPrune`;
- emit ordered multi-label pair classes và cross-tab với immediate acceptance relation;
- preserve explicit evidence strength, strict schema và aggregate reconciliation.

Ngoài scope: causal service-loss decomposition, split ranking khỏi cap/work omission,
candidate feasibility, recovered completions, downstream trajectory, rerun, CI hoặc
population inference.

### BDD

- Numeric budget và categorical lock map đúng typed class, không scalarize lock.
- Exact physical prune code map physical; unsupported prune code giữ unknown.
- Selected-by-C1 thêm `sharedSelectedCandidate`, không gọi relaxation.
- Absent/not-recorded map `rankingOrSearchOmissionIndeterminate`, không tự chọn cause.
- Source mismatch, duplicate/missing unit, contradictory relation hoặc aggregate
  mutation đều fail closed.

### Acceptance

- 40/40 records và 41/41 links classified once; cross-tabs reconcile;
- every class carries noncausal/no-downstream evidence boundary;
- targeted mutation, full pinned Python và required .NET pass;
- ticket chuyển Done, `RB-WP13-007` là ticket Ready duy nhất.

### Closure

- strict classifier bind exact `004`/`005`, 40 records và 41 candidate links;
- pair-level occurrences: budget 28, lock 5, indeterminate 7, shared-selected 1;
  physical/unsupported 0;
- immediate cross-tab: C1-lower có 7 budget + 1 lock, equal có 21 budget + 4 lock +
  7 indeterminate; co-occurrence remains descriptive/noncausal;
- canonical artifact SHA `bcc6bed3…f9e9eb`; targeted 8/8, pinned Python 154/154,
  independent verifier và required Debug 856/856 pass;
- `RB-WP13-006 Done`; `RB-WP13-007 Ready` là queue head duy nhất.

## 7. RB-WP13-007

### Purpose

Đo đúng các count/work/omission/selection covariates H6 v1.0.0 đã ghi tại exact
first-divergence epoch và phát catalog field thiếu có thể kiểm bằng máy. Ticket phải
trả lời liệu H6 đủ cho candidate-level ranking/relaxation questions hay Runner evidence
vNext thực sự cần, không suy portfolio từ aggregate counts.

### Scope

- bind exact `003`–`006` artifacts, source/schema identities và 40 panel/unit targets;
- quét lại 80 primary raw target decisions tới EOF, verify manifest/transcript
  receipts và exact `evidenceVersion: 1.0.0`;
- emit per-arm generation, vehicle-loss, prune-witness và selection count/work
  covariates, cùng exact paired equality/delta relations;
- phân biệt `recordedCountOnly`, `notRecorded` và `notEvaluated` bằng field catalog;
- ghi recommendation evidence vNext chỉ từ unresolved candidate links + missing
  candidate-level fields, cùng exact question scope mà recommendation cho phép.

Ngoài scope: reconstruct retained candidate IDs/routes/schedules, rerank, clear later
validator blockers, candidate feasibility, policy rerun, downstream effect, CI hoặc
population inference.

### BDD

- Given generation count/work evidence đầy đủ, then aggregate theo typed integer/
  boolean fields và kiểm internal conservation relations trước emit.
- Given B1/C1 raw generation object giống hệt, then relation `exactEqual`; count bằng
  nhau không được tự nâng thành candidate-identity equality.
- Given cap/work/request/repair omission counter khác zero, then record exact flag/
  count và không gọi option set complete.
- Given candidate link unresolved trong `005`, then giữ identity status `notRecorded`
  dù aggregate generation không có omission.
- Given missing target/version/receipt, duplicate vehicle, contradictory count,
  mutated source input hoặc aggregate, then fail closed.

### Acceptance

- 40/40 pair và 80/80 arm-epochs có schema-valid count-only covariates, raw/domain
  hashes và paired relations; mọi aggregate reconcile từ records;
- field catalog nói rõ field nào có, field nào không ghi và question nào bị chặn;
- evidence-vNext verdict không đổi H6 và không tự mở exploratory rerun;
- targeted mutation, full pinned Python và required .NET pass;
- ticket chuyển Done; `RB-WP13-008` Done/Cancelled/Ready chỉ theo verdict có evidence.

### Closure

- 80/80 raw target decisions verified; exact generation evidence 40/40 pair và
  generation-complete 80/80 arm-epoch;
- zero cap/work/generation/selection omission; generator-retained counts equal across
  arms, nhưng candidate identity equality không được thiết lập;
- C1 có thêm 46 commitment-pruned candidates và 390 validation work units; bảy
  unresolved identities vẫn `notRecorded`;
- canonical artifact SHA `d71c669b…37258`; targeted 10/10, pinned Python 164/164,
  independent raw verifier và required Debug 856/856 pass;
- verdict cần evidence vNext cho candidate-level questions, không authorize rerun/
  backfill; `RB-WP13-007 Done`, `RB-WP13-008 Ready`.

## 8. RB-WP13-008

### Purpose

Thêm profile Runner evidence opt-in có version để ghi exact pre-policy generated
physical portfolio, post-policy eligible portfolio và solver-neutral objective inputs.
Default output phải byte-semantics tương thích; instrumentation không được đổi
candidate generation, filtering, selection, actions hoặc state.

### Scope

- thêm config profile explicit `retained-portfolio-v1`, chỉ hợp lệ khi solver evidence
  base bật và policy dùng solver-backed path;
- capture immutable generated/eligible candidate sets cùng exact
  `CandidateSelectionProblem` chỉ khi profile bật;
- emit `executionEvidence` v1.2.0 với candidate ID, vehicle/request IDs, full route,
  schedule, eligibility, exact selected candidate IDs và ordered objective
  levels/contributions;
- giữ v1.1.0 exact shape khi profile tắt; v1.0.0 H6 vẫn decode như lịch sử;
- strict version/field validation, deterministic ordering, no truncation và source-
  controlled schema cho portfolio evidence.

Ngoài scope: chạy simulator, backfill H6, đổi budget/cap/ranking, evaluate clearance,
policy ablation, downstream service, CI hoặc population inference.

### BDD

- Given profile absent, then decision/state/action/evidence v1.1 shape không đổi.
- Given profile bật, then generated IDs bằng snapshot pre-policy, eligible IDs là exact
  subset và bằng solver problem option IDs; mọi candidate có full route/schedule.
- Given eligible candidate, then objective contribution count/order khớp exact solver-
  neutral problem; policy-pruned candidate không được bịa objective vector.
- Given duplicate/missing candidate, cross-vehicle mismatch, objective mismatch hoặc
  unsupported evidence version/profile, then fail closed.
- Given profile bật nhưng base evidence tắt hoặc non-solver policy, then config reject.

### Acceptance

- opt-in v1.2 schema/mapper/snapshot/config path deterministic và backward compatible;
- capture-on/off differential giữ operational decision/state/action identical;
- legacy v1.0/v1.1 decode tests và new negative/mutation tests pass;
- full required `.NET`, pinned Python và formatting/static gates pass;
- ticket Done chỉ mở `RB-WP13-009 Ready`; không tự chạy exploratory replay.

### Closure

- profile `retained-portfolio-v1` bật v1.2.0 và giữ v1.1.0 khi vắng; v1.0.0 H6 vẫn
  decode;
- snapshot defensive-copy generated/eligible portfolios, giữ exact solver problem và
  selected candidate IDs; mapper ghi full route/schedule/objective inputs;
- strict decoder/schema reconcile count, identity, vehicle/no-op, selected set,
  objective và remaining-route/schedule invariants; pruned candidate không có invented
  objective;
- capture-on/off differential giữ status/state/actions; targeted .NET 4/4, schema 4/4,
  required .NET 860/860, pinned Python 168/168, format/diff/static gates pass;
- report `benchmarking/wp13-008-runner-retained-portfolio-evidence-2026-08-24.md`;
  không simulator run/H6 write; `RB-WP13-008 Done`, `RB-WP13-009 Ready`.

## 9. RB-WP13-009

### Purpose

Khóa và thực thi một replay exploratory mới chỉ khác H6 ở instrumentation v1.2.0,
để thu candidate-level evidence trên toàn bộ paired target set mà không chọn subset
theo outcome và không thay B1/C1 policy semantics.

### Scope

- tạo freeze manifest `E1` source-controlled trước execution, bind exact 40/40 targets
  từ record set `003`, toàn bộ Panel A/B, scenario/travel/input identities, arms, seeds,
  simulator/runtime/adapter/Runner/config/schema/source hashes;
- clone exact B1/C1 run configs và chỉ thêm base solver evidence +
  `solverExecutionEvidenceProfile: retained-portfolio-v1`; mọi budget/cap/objective/
  promise/lock/failure treatment giữ nguyên;
- freeze new output root ngoài H6, expected 80 arm runs, pairing/order, no-retry/
  failure retention, line budget và resource envelope;
- verify freeze/hash trước terminal execution, rồi chạy all-or-retain-failure raw replay;
- chỉ inventory receipts/v1.2 coverage ở ticket này; candidate comparison/aggregation
  chờ `010`/`011`.

Ngoài scope: chọn divergence-only cells, relax witness, rerank, policy v2, service
frontier, CI/population inference, H6 backfill hoặc overwrite raw result.

### BDD

- Given target inventory, then freeze phải chứa đủ 40 unique panel/unit pairs và cả
  hai arms; duplicate/missing/outcome-selected subset fail closed.
- Given cloned config, then canonical diff với source chỉ được gồm evidence opt-in và
  intentional policy/config identity binding; budget/cap/objective drift fail closed.
- Given freeze chưa verified hoặc source/hash/runtime khác, then không launch job.
- Given job fail/timeout/protocol error, then retain raw failure, không retry/cherry-pick
  denominator và không chạy paired arm như một replacement.
- Given success, then mọi produced solver decision cần v1.2 portfolio; non-solver epoch
  được phân loại explicit, không bịa missing candidate data.

### Acceptance

- freeze manifest/hash/independent preflight tồn tại trước raw execution;
- exact 40 paired targets/80 arm jobs hoặc typed retained failures, không partial reuse;
- H6 roots and receipts byte-identical/read-only; new root tách namespace;
- raw manifests/transcripts bind Runner/config/schema và profile v1.2;
- full required `.NET`, pinned Python, source/static/resource gates pass;
- ticket Done chỉ mở `RB-WP13-010 Ready`; chưa phát mechanism conclusion.

### Closure

- freeze receipt `9fcf2193…a4411` bind 40 pair/80 arm, 40 drivers, exact configs,
  source/runtime/Runner/schema trees, output/H6 roots và resource envelope trước run;
- Panel A/B hoàn tất 40/40 arm mỗi panel, zero failure, cùng repository inventory
  `22f4914e…f6afb`; không retry/replacement/H6 write;
- independent inventory đọc 80 bundle tới EOF: 8.640 requests, 44.156 solver
  decisions và 44.156 v1.2 portfolios, tổng 5.516.098.710 raw bytes;
- external canonical inventory SHA `a029b978…4674`, compact source receipt và report
  `benchmarking/wp13-009-exploratory-retained-portfolio-replay-2026-08-24.md`;
- targeted 22/22, required .NET 860/860, pinned Python 181/181, zero skip; format/
  diff/line gates pass;
- `RB-WP13-009 Done`, `RB-WP13-010 Ready`; chưa có candidate/mechanism conclusion.

## 10. RB-WP13-010

### Purpose

Falsify độc lập evidence contract E1 trước khi bất kỳ candidate-level aggregation nào
được phép chạy, để một portfolio corrupt nhưng có manifest hợp lệ không lọt sang `011`.

### Scope

- khóa một verifier/falsification receipt không import Runner mapper/decoder hoặc
  simulator/solver implementation; raw E1/H6 chỉ đọc;
- verify lại exact 80 bundles/44.156 decisions theo freeze/plan/inventory bindings và
  emit stable typed rejection layer/code;
- tạo deterministic mutation matrix từ recorded v1.2 portfolio samples, không copy/
  sửa raw roots: version/schema/profile, count/order/identity, eligible/pruned shape,
  no-op/selected vehicle/request disjointness, objective levels/contributions,
  route/schedule/time, transcript truncation/hash chain, summary/manifest/source bind;
- mỗi mutant chỉ đổi một intended invariant khi khả thi; record expected và actual
  rejection, không chấp nhận generic crash thay typed fail-closed;
- chỉ phát verifier coverage/falsification report; không so B1/C1 outcome hoặc suy
  mechanism.

Ngoài scope: candidate matching, reranking, relaxation, service aggregation, CI,
causal attribution, policy v2, H6 reinterpretation/backfill.

### BDD

- Given exact E1 roots, then verifier pass đủ 80 bundles và reconcile exact freeze,
  plan, scenario, source inventory, manifest, semantic và v1.2 coverage.
- Given one mutation in any required nested invariant, then verifier reject tại stable
  expected layer/code; mutation silently accepted hoặc only downstream mismatch fail.
- Given truncation, extra file, manifest/hash-chain/source binding drift, then reject
  trước khi portfolio record được tính valid.
- Given mutation corpus generation, then raw E1/H6 bytes remain unchanged and every
  mutant lives outside raw roots or only in memory.
- Given all gates pass, then output vẫn ghi `mechanismConclusion: notEvaluated` và chỉ
  mở `RB-WP13-011 Ready`.

### Acceptance

- source-controlled versioned verifier contract + mutation catalog/receipt;
- 80/80 raw bundles pass independent path; 100% declared mutants rejected at intended
  layer, zero unexpected pass/generic crash;
- exact source/raw/root hashes and mutation seeds/targets recorded;
- full required .NET, pinned Python, format/static/resource gates pass;
- ticket Done chỉ mở `RB-WP13-011 Ready`; không phát mechanism conclusion.

### Closure

- independent inventory được dựng lại byte-exact từ 80/80 E1 bundle, 44.156 solver
  decision và 44.156 retained portfolio;
- exact 31-mutant catalog phủ 9 layer; 31/31 bị reject đúng expected layer và typed
  code, zero unexpected pass/failure; generic error không được tính expected;
- read-only E1↔H6 comparator verify 80/80 same-arm behavioral projection equal,
  8.640 request/44.156 H6 decision, zero mismatch; semantic hash 0/80 equal là
  expected do instrumentation/config binding v1.2;
- receipt closure SHA `78bf6313…77785`, equivalence receipt SHA
  `4abb24f0…babfc`, compact receipt và report `benchmarking/wp13-010-e1-evidence-
  falsification-2026-08-24.md`;
- targeted 6/6, required .NET 860/860 và full sequential pinned Python 187/187,
  zero skip; format/diff/JSON/Markdown/line gates pass;
- `RB-WP13-010 Done`, `RB-WP13-011 In progress`; chưa có mechanism conclusion.

## 11. RB-WP13-011

### Purpose

Aggregate finite-panel candidate evidence ở exact first-divergence decisions, nhằm mô
tả B1-selected option tồn tại/eligible/pruned/selected thế nào trong C1 mà không biến
association thành causal effect hoặc population inference.

### Scope

- consume đúng `003` first-divergence set, `004` immediate comparison, verified E1
  inventory và hai receipt `010`; fail nếu provenance/hash/claim boundary lệch;
- đọc exact 80 E1 target transcripts tới EOF, lấy v1.2 portfolio ở đúng 40 paired
  first-divergence epoch và bind E1 behavior về H6 record;
- tạo policy-neutral candidate signature từ vehicle, request set, route và schedule;
  candidate ID equality chỉ là evidence phụ, không là khóa join duy nhất;
- với mỗi pair, phân loại B1-selected signature trong C1 thành selected, eligible-not-
  selected, pruned-with-recorded-witness hoặc absent-from-generated-set;
- ghi generated/eligible overlap, within-arm selection rank khi profile cho phép và
  explicit `notComparableAcrossObjectiveProfiles` cho raw objective contribution;
- aggregate pair trước rồi cell/panel, giữ exact denominators và link immediate
  acceptance/downstream completed delta bằng nhãn `trajectoryAssociatedNotCausal`.

Ngoài scope: CI/p-value/population claim, reranking/counterfactual completion, chọn
budget/lock mới, policy v2, simulator rerun, H6 backfill hoặc rescue confirmatory gate.

### BDD

- Given candidate có cùng semantic signature nhưng ID khác, then join theo signature,
  ghi ID drift và không báo absent giả.
- Given B1-selected signature có trong C1 nhưng pruned, then giữ exact recorded witness
  code; không suy feasible sau relaxation.
- Given signature eligible ở cả hai arm nhưng C1 không chọn, then ghi
  `eligibleNotSelectedAssociation`, không gọi causal ranking loss.
- Given signature thật sự vắng trong full C1 generated portfolio, then ghi
  `absentFromGeneratedSet`; không thay bằng H6 `notRecorded` cũ.
- Given B1/C1 objective profile khác, then không trừ hoặc xếp chung raw contribution;
  chỉ within-profile rank và typed incomparability được phép.
- Given duplicate signature, epoch/hash lệch, missing portfolio, unequal behavioral
  binding hoặc aggregate không reconcile record-level, then fail closed.

### Acceptance

- strict versioned schema/report có đúng 40 pair, Panel A/B 20/20 và exact cell
  denominators; mọi aggregate tái lập từ record-level data;
- 80 target transcript được independent-verify tới EOF; raw E1/H6 chỉ đọc;
- mutation tests phủ signature collision/ID drift, eligibility, objective
  incomparability, missing epoch và denominator mismatch;
- report chỉ finite-panel descriptive, giữ `trajectoryAssociatedNotCausal`, không CI;
- required .NET, pinned Python, format/static/resource gates pass;
- ticket Done chỉ mở `RB-WP13-012 Ready`; không tự mở WP14.

### Closure

- independent full scan verify 80/80 E1 bundle, 44.156 decision và exact 80 target
  portfolios; 40/40 generated signature sets equal, 390 signature/arm, zero collision,
  candidate-ID drift hoặc absent-from-generated link;
- 41 B1 actionful links: 33 C1-pruned (28 budget, 5 lock), 7 C1-eligible-not-
  selected, 1 C1-selected; bảy H6 `notRecorded` links được giải bằng evidence thật;
- all eight C1-lower cells có prune occurrence, nhưng 25 prune links khác nằm ở
  equal-immediate cells; four of seven eligible-not-selected links có within-vehicle
  ordinal 1, nên không suy causal/ranking-loss từ classification;
- Panel A/B H6 totals reconcile `1735→1581` và `966→860`; association rows được khóa
  `overlappingCellsNotAdditive`, `trajectoryAssociatedNotCausal`;
- closure report 116.985 byte SHA `0eba293c…ddc1c`, compact receipt và report
  `benchmarking/wp13-011-e1-candidate-descriptive-aggregation-2026-08-24.md`;
- targeted 4/4, required .NET 860/860, full sequential pinned Python 191/191,
  zero skip; format/diff/schema/JSON/Markdown/line gates pass;
- `RB-WP13-011 Done`, `RB-WP13-012 In progress`; WP14 chưa được mở.

## 12. RB-WP13-012

### Purpose

Audit toàn bộ source, schema, tests, evidence DAG và claim text của WP13 trước closure,
để không lỗi implementation/provenance hay diễn giải nào bị che bởi aggregate pass.

### Scope

- inventory và đọc từng file source/schema/test/docs do `001..011` tạo hoặc sửa,
  bao gồm Runner/Contracts/Algorithms instrumentation v1.2 và mọi Python analyzer;
- kiểm architecture boundary: Domain/Application không phụ thuộc simulator/solver;
  adapter/analyzer không reimplement core hoặc import simulator policy logic;
- walk từng invariant từ raw H6/E1 receipts qua reports/compact summaries/ADR và
  requirement traceability; recompute hash/length/count DAG độc lập;
- kiểm backward compatibility profile-off v1.1/H6 v1.0, defensive copy, canonical
  serialization, output-path/raw-root protection và current verifier provenance;
- review mutation coverage, signature/objective/association semantics, denominator,
  Panel A/B separation và mọi `notRecorded`/noncausal/non-additive label;
- chạy static searches cho forbidden novelty/population/causal/rescue claims và đối
  chiếu lại claim boundary với ba full PDF đã đọc 106/106 trang;
- review resource footprint 5.516.098.710 raw bytes và xác định limitation/next action,
  nhưng không tối ưu policy hoặc chọn parameter trong audit.

Ngoài scope: simulator rerun, policy v2, WP14 ablation, H6 backfill, sửa margin/panel,
CI/population inference hoặc counterfactual completion.

### BDD

- Given compact receipt, then mọi external path/length/hash/count phải tái lập từ
  canonical artifact; stale source hash hoặc superseded artifact làm audit fail.
- Given capture profile off, then output semantic status/state/actions phải giữ
  differential v1.1; profile on mới được phát v1.2 portfolio.
- Given analyzer import/source graph, then không có dependency ngược vào simulator
  policy/solver implementation hoặc Domain/Application boundary violation.
- Given report claim, then causal/population/ranking-loss/decomposition wording chỉ
  được tồn tại trong prohibited/caveat context, không thành conclusion.
- Given association rows overlap hoặc objective profiles khác, then audit bắt buộc
  thấy exact non-additive/incomparability labels ở schema, artifact, report và ADR.
- Given bất kỳ finding P0–P2, then sửa + regression test + rerun gates trước Done;
  không chuyển finding thành “future work” để đóng ticket.

### Acceptance

- source-controlled file inventory và Vietnamese line-by-line audit report;
- zero unresolved P0/P1/P2; P3/limitation có owner/next ticket rõ;
- external receipt DAG, H6/E1 immutability và all aggregate identities pass độc lập;
- required .NET, full pinned Python, format/build/static/link/schema/diff gates pass;
- audit không thêm scientific result hoặc mở policy v2;
- ticket Done chỉ mở `RB-WP13-013 Ready` để quyết định closure/WP14.

### Closure

- source-controlled inventory bind 80 file/1.076.880 byte, inventory SHA
  `33d89c17…01c0c4`; external canonical audit receipt 25.437 byte SHA
  `cd863988…fad76`;
- deep verification pass 100 H6 bundle/57.806 decision và 80 E1 arm/44.156 decision/
  portfolio; 62/62 E1 freeze files exact, zero DAG edge failure;
- một P2 verifier-composition gap được sửa bằng supplemental canonical integer,
  optional field và UTF-8 identifier guard với mutation regression; frozen verifier
  source không bị sửa; unresolved P0/P1/P2 bằng 0;
- 45 Domain/Application source, 115 analyzer imports và 12 stable claim documents
  được audit; zero reverse dependency, zero unsafe conclusion;
- targeted 8/8, required .NET 860/860 và pinned Python 199/199, zero skip; format/
  diff/schema/JSON/Markdown/line gates pass;
- `RB-WP13-012 Done`, `RB-WP13-013 In progress`; WP14 chưa được mở.

## 13. RB-WP13-013

### Purpose

Đóng WP13 bằng một evidence/decision bundle duy nhất: xử lý ba limitation P3 của audit,
đánh giá từng exit gate bằng artifact đã verify và quyết định rõ mở hay không mở WP14
mà không biến exploratory diagnostics thành rescue của H6.

### Scope

- bind exact audit receipt `012`, active WP13 compact receipts và H6/E1 authorities;
- phát source-controlled closure manifest có exact artifact path/length/SHA, ticket
  status, exit-gate verdict và claim boundary;
- khóa derived-output policy cho analyzer tương lai: raw-root protection, exclusive
  creation, canonical write-after-verify và supersession metadata;
- quyết định retention/archive cho 5.516 GB E1 bằng manifest/checksum và explicit
  preserve rule; không xóa, move hoặc recompress evidence trong ticket closure;
- định nghĩa successor-verifier requirement/version boundary thay vì mutate frozen E1
  verifier;
- đánh giá readiness của WP14 từ evidence sufficiency, scientific need, separation
  khỏi confirmatory H6 cells và resource feasibility;
- nếu gate pass, chỉ ticket hóa/refine WP14 sau khi WP13 Done; nếu fail, ghi exact
  blocker/owner, không tự mở policy v2.

Ngoài scope: chạy ablation, chọn budget/horizon, sửa C1, rerun H6/E1, archive/delete
raw evidence, claim causal mechanism hoặc population effect.

### BDD

- Given closure manifest, then mọi active receipt phải resolve exact path/length/SHA
  và mọi superseded receipt phải được nhận dạng, không dùng làm authority.
- Given raw/derived storage policy, then raw H6/E1 là preserve/read-only; future
  analyzer success output phải canonical, exclusive-create và write-after-verify.
- Given frozen verifier limitation, then closure giữ historical source/hash và yêu cầu
  successor version có supplemental constraints cùng regression; không silent mutate.
- Given WP14 readiness decision, then verdict phải tách scientific need, evidence
  sufficiency, leakage/freeze và resource gate; một gate fail thì verdict không mở.
- Given verdict mở WP14, then H6 result/margin/panels vẫn immutable và WP14 chỉ dùng
  development namespace/cells dưới freeze mới.

### Acceptance

- closure report và strict/canonical source-controlled manifest có independent check;
- ba P3 limitation có resolution/owner cụ thể, không còn unowned finding;
- mọi WP13 exit gate có evidence pointer và pass/fail verdict;
- decision WP14 explicit, rationale traceable và không có scientific overclaim;
- required .NET, pinned Python, schema/JSON/Markdown/link/diff gates pass;
- `RB-WP13-013 Done`; WP13 đóng. Chỉ khi verdict `open` mới refine một queue head
  WP14, còn WP15–WP20 giữ roadmap-level.

### Closure

- strict canonical closure manifest 4.463 byte SHA `4e410e23…a72c9c` bind exact
  `012` audit, 80-file inventory, active/superseded DAG và seven exit gates;
- ba P3 limitations resolved bằng accepted retention/successor policy: raw E1 giữ
  nguyên 5.516.098.710 byte, future derived success dùng exclusive create và evidence
  execution mới cần versioned successor verifier;
- verdict `openExploratoryAblationOnly`: WP14 dùng development namespace/cells mới,
  loại H6 panels khỏi configuration selection, freeze trước outcome và không authorize
  H7/policy v2;
- independent closure tests 6/6, required .NET 860/860, pinned Python 205/205,
  zero skip; format/diff/schema/JSON/Markdown/link/line gates pass;
- `RB-WP13-013 Done`; WP13 Complete. `RB-WP14-001` là refinement queue head mới.

## 14. Gates chung

- Không import simulator/solver implementation vào analyzer.
- Raw evidence là immutable input; derived report nằm trong repo và bind hash.
- Mọi field thiếu được ghi `notRecorded`, không được reconstruct bằng assumption.
- Downstream completed delta luôn mang nhãn `trajectoryAssociated`.
- Ticket code phải được review từng file thay đổi và có negative/mutation tests.
