# Trạng thái và decision log

> Tệp sống — cập nhật ở cuối mọi task RideBound
> Cập nhật gần nhất: 2026-08-23

## 1. Trạng thái tổng thể

| Mục | Trạng thái |
|---|---|
| Research direction | `LOCKED_FOR_IMPLEMENTATION_PLANNING` |
| Documentation | `MIGRATED_AND_VERIFIED_V1` |
| Implementation | `WP1_Q1_COMPLETE; WP2_COMPLETE; WP3_COMPLETE_14_OF_14; WP4_COMPLETE_14_OF_14; WP5_COMPLETE_14_OF_14; WP6_COMPLETE_14_OF_14; WP7_COMPLETE_14_OF_14; WP8_COMPLETE_14_OF_14; WP9_COMPLETE_001_TO_009; WP10_COMPLETE_NEGATIVE_CAPABILITY_001_TO_010` |
| Current work package | `Post-WP10 assurance COMPLETE: full review, paper-driven optimization, benchmark and rendered final report` |
| Repository | `https://github.com/tsunflowerr/RideBound` |
| Main baseline | B1 `rolling-cost` |
| Main treatment | C1 `ridebound-hard-vector` |
| Layer 2 | FleetPy 1.0.2 |
| Layer 3 | RidePy v2.10.1 evaluated; canonical PASS, representative subset FAIL CLOSED; AMoD2 unevaluated alternate |

## 2. Đã hoàn thành

- Kiểm toán kiến trúc BeGo hiện tại.
- Xác nhận `Session` không cho thay pickup sau computation.
- Xác nhận benchmark hiện tại là snapshot, không có promise history.
- Backend 25/25 test pass.
- Frontend 7/7 test pass.
- Đọc/đối chiếu evidence trực tiếp cho RideBound.
- Xác minh official repos/versions:
  - FleetPy 1.0.2 / `053aa9d...`;
  - RidePy v2.10.1 / `bf1863e...`;
  - AMoD2 / `aaa66dd...` tại ngày kiểm tra;
  - AMoDeus;
  - OpenRidepoolSimulator.
- Kiểm source extension points của FleetPy/RidePy/AMoD2.
- Tạo repository Git độc lập `E:\Code\RideBound`.
- Tạo `RideBound.slnx` với 7 source project và 2 test project.
- Khóa dependency Clean Architecture/DDD bằng architecture tests.
- Thêm `global.json`, shared build policy, `.editorconfig`, CI và README.
- Sửa architecture test để đọc `ProjectReference` dùng cả dấu phân cách Windows và
  Linux; bổ sung regression cases cho hai kiểu đường dẫn.
- Mở rộng CI thành các gate format, Release build/test/coverage, dependency audit,
  main runner artifact, Sonar Quality Gate tùy cấu hình và PR-Agent tùy cấu hình.
- Chuyển 23 tài liệu lõi cùng archive evidence liên quan sang `docs/` và tạo root `AGENTS.md`.
- Xác minh 37 tệp Markdown/6.821 dòng: 0 link nội bộ hỏng, 0 code fence lệch,
  0 dấu hiệu mojibake; kèm 3 JSON evidence machine-readable.
- Giữ nguyên cây mã nguồn `E:\Code\BeGo`; không copy `BeGo/src`.
- Xác minh WP0:
  - RideBound restore/build Release: 0 warning, 0 error;
  - RideBound tests hiện tại: 8/8 pass;
  - BeGo backend: 25/25 pass;
  - BeGo frontend: 7/7 pass.
- Hoàn thành `RB-WP1-001`: khóa schema version, unit/range, position union,
  event ordering, envelope/payload/manifest boundary, error taxonomy, canonical
  JSON/hash framing và fixture taxonomy bằng ADR-014.
- Hoàn thành `RB-WP1-002`: thêm Contracts test project, UTF-8 fixture loader và
  smoke fixture dùng chung.
- Hoàn thành `RB-WP1-003`: thêm protocol primitives, typed envelope,
  encode/decode và structural validation có reason code.
- Hoàn thành `RB-WP1-004`: thêm canonical unit conversions, canonical JSON
  byte writer và source-controlled golden byte vector.
- Hoàn thành `RB-WP1-005`: thêm machine-readable JSON Schema v1, schema inventory,
  compatibility matrix và executable version/unknown-field policy.
- Hoàn thành `RB-WP1-006`: thêm hello/helloAck contracts, capability vocabulary
  và deterministic fail-fast/named-downgrade negotiation.
- Hoàn thành `RB-WP1-007`: thêm immutable initialize manifest/initial state
  identity cùng validation chéo envelope/hello/ack không mutate.
- Hoàn thành `RB-WP1-008`: event vocabulary/payload schema và ordering validator
  cho sequence, epoch, simulation time, gap/overlap/overflow.
- Hoàn thành `RB-WP1-009`: decision/error/certificate shell; Q1 luôn ghi rõ
  `notProduced`, không phát action/certificate/solver giả.
- Hoàn thành `RB-WP1-010`: SHA-256 domain separation, tagged length framing,
  manifest/state/decision vectors và chain/tamper/order tests.
- Hoàn thành `RB-WP1-011`: async NDJSON reader/writer có UTF-8/size/EOF/LF/flush
  semantics và diagnostic tách khỏi stdout.
- Hoàn thành `RB-WP1-012`: executable long-lived runner
  `new → negotiated → initialized → awaitingDecisionApplied`, memory pipe và
  child-process integration tests.
- Hoàn thành `RB-WP1-013`: retry nguyên batch trả cached response không advance;
  conflict/overlap/gap/hash/version có đúng disposition; cache giữ một response.
- Hoàn thành `RB-WP1-014`: đúng 10 required golden fixture, full tiny transcript,
  exact expected output/final hash, replay hai lần và tamper proof.
- Hoàn thành `RB-WP1-015`: đóng Q1 bằng ADR-016, traceability/gate evidence và
  tạo ticket refinement duy nhất `RB-WP2-001`.
- Revalidate end-to-end toàn WP1: phát hiện và sửa cache idempotency từng coi
  batch đổi `simTimeMs` là retry hợp lệ; exact retry hiện hash toàn canonical
  eventBatch envelope + payload theo ADR-017.
- Bổ sung regression test cho changed-time duplicate và replay exact transcript
  qua hai runner process sạch; WP1 revalidation inventory đạt 161/161.
- Hoàn thành `RB-WP2-001`: ADR-018 khóa state/reducer/route/reassignment boundary
  và tạo ordered queue `RB-WP2-002..012` trong execution plan WP2.
- Hoàn thành `RB-WP2-002`: typed request/vehicle/position/route/travel contracts,
  strict event payload schema dispatch, thay `fixtureIntent` và thêm
  bootstrap/two-epoch fixtures.
- Hoàn thành `RB-WP2-003`: immutable Domain run/request/vehicle state machine,
  exhaustive lifecycle table và accepted-never-rejected evidence.
- Hoàn thành `RB-WP2-004`: exact frozen prefix, ordered route leg, mutable suffix,
  no-op, planVersion và monotonic reached-stop progress.
- Hoàn thành `RB-WP2-005`: Runner contract mapper, Application internal events,
  manifest-bound travel snapshot, atomic reducer và pending/ack coordinator.
- Hoàn thành `RB-WP2-006`: independent physical validator tự dựng schedule và
  deterministic witness cho capacity/window/max-ride/precedence/connectivity/
  prefix/onboard/accepted/reassignment.
- Hoàn thành `RB-WP2-007`: deterministic pickup/drop insertion generator,
  canonical candidate/stop identity, exact/bounded caps, stable ordering,
  physical prune witness và no-op retention.
- Hoàn thành `RB-WP2-008`: exhaustive B1 fleet selection tối đa accepted count,
  tối thiểu integer route cost, stable tie-break, accept/reject/defer và staged
  apply không reassign incumbent.
- Hoàn thành `RB-WP2-009`: independent exact-small brute-force oracle với 32
  published seeds; generator/selection gap bằng 0 trong bound 2 vehicle/2
  pending request.
- Hoàn thành `RB-WP2-010`: Runner default online produced typed decisions,
  full state/action hash, ACK-only commit và named `--mode conformance` giữ Q1
  transcript oracle.
- Hoàn thành `RB-WP2-011`: source-controlled four-epoch tiny demo chạy hai clean
  self-contained process, byte-exact golden/final hash và tamper proof.
- Hoàn thành `RB-WP2-012`: đóng WP2 physical/B1 bằng ADR-020, đồng bộ gate/
  traceability và tạo đúng một next refinement ticket `RB-WP3-001`.
- Hoàn thành `RB-WP3-001`: đọc lại paper trực tiếp, khóa ADR-021 và tạo ordered
  queue 14 ticket trong `tasks/28-wp3-ledger-certificate-ticket-plan.md`.
- Hoàn thành `RB-WP3-002`: pure Domain promise/version/service-order model, stable
  10-dimension vector và explicit policy zero/unbounded/phase/material/lock types.
- Hoàn thành `RB-WP3-003`: shared `RouteScheduleProjector`, candidate evaluator
  dùng lại projector và `PromiseProjector` cho initial/onboard promise.
- Hoàn thành `RB-WP3-004`: three-way exogenous/decision/visible delta đủ
  ETA/material/vehicle/stop/order/insertion dimension cùng distance port/witness.
- Hoàn thành `RB-WP3-005`: immutable initial/revision ledger, exact version/P1
  conservation/no-refund và ledger nằm trong pending `OnlineState`/ACK transaction.
- Hoàn thành `RB-WP3-006`: hard vector budget evaluator cho đủ 10 dimension,
  exact before/delta/after witness, hard zero, unbounded và monotonic feasible set.
- Hoàn thành `RB-WP3-007`: accepted assignment, onboard, freeze-horizon và final
  confirmation lock evaluator với policy flag/witness rõ.
- Hoàn thành `RB-WP3-008`: typed incident open/resolve, affected-rider derivation,
  immutable breach record với chronology, vehicle và budget relation; không
  reset/refund normal ledger.
- Hoàn thành `RB-WP3-009`: independent combined validator tự dựng lại physical
  plan, state boundary, promise, three-way delta, locks và hard-vector budget;
  candidate filter chỉ là early pruning, Runner revalidate toàn fleet trước publish.
- Hoàn thành `RB-WP3-010`: certificate/action/schema strict cho normal operation
  và witness; input/proposed state hash cùng publication IDs bị cross-check với
  containing decision/actions.
- Hoàn thành `RB-WP3-011`: named commitment policy configuration/content hash,
  Runner `commitment` mode, atomic promise/ledger/certificate/state hash và
  matching ACK/retry semantics.
- Hoàn thành `RB-WP3-012`: canonical full-state checkpoint/restore với content
  hash, manifest/travel/reachable-state/tamper validation và cấm checkpoint khi
  decision còn pending.
- Hoàn thành `RB-WP3-013`: 10-dimension mutation-killing tests, 64×12 generated
  ledger histories, 16-seed independent exact-small P2/P3, two-process replay và
  checkpoint suffix equivalence.
- Hoàn thành `RB-WP3-014`: audit toàn code WP1–WP3, Browser research recheck,
  ADR-022, review giải thích chi tiết và chỉ `RB-WP4-001` refinement READY.
- Hoàn thành `RB-WP5-001`: khóa BeGo/RideBound source provenance, NDJSON Runner
  ownership, append-only schema, idempotency fingerprint, short local transaction,
  outbox, per-run claim/lease, crash recovery qua checkpoint + replay + exact hash,
  bootstrap field provenance, privacy/feature flag và paired B1/C1 Layer-1 protocol
  bằng ADR-025. In-app Browser đọc paper/tài liệu primary; queue `RB-WP5-002..014`
  có đúng một implementation ticket `002 READY`, chưa có WP5 production code.
- Hoàn thành `RB-WP5-002`: BeGo Application có immutable validated contract/port
  cho run/operation/idempotency/Runner/timeline; exhaustive operation/run state
  transition, monotonic revision/time, contiguous epoch/event cursor, strict UTF-8
  hash và actor/resource/payload-bound idempotency. Runner frame phải single-line,
  duplicate-free và embedded `messageType` khớp declaration; decision/certificate/
  outbox hash/order bị guard, T3 contract bắt buộc checkpoint proof sau ACK. Ba
  architecture tests giữ Application khỏi EF/Npgsql/ASP.NET/SignalR/RideBound.
- Hoàn thành `RB-WP5-005`: BeGo Infrastructure có pinned long-lived Runner
  supervisor, một session/process mỗi run và pool bounded. Strict UTF-8 NDJSON,
  line/stderr/time bounds, exact schema/capability/manifest/context binding,
  atomic ACK+checkpoint và process-tree cleanup đều fail closed. Lifecycle audit
  sửa race dispose/start và orphan child. Stub process gate cover adversarial
  framing/lifecycle; published RideBound Runner Release thật hoàn tất online
  bootstrap/decision/ACK/checkpoint cycle.
- Hoàn thành `RB-WP5-006`: mapper chụp immutable BeGo bootstrap source trước
  external I/O, pseudonym HMAC run-local, E7/ms ties-to-even, complete directed
  matrix có node cap, field provenance và exact negotiated manifest/domain hash.
  Old assignment/snapshot chỉ là hashed provenance; generated bootstrap chạy
  xuyên published Runner thật và full BeGo Release pass 98/98 không skip.
- Hoàn thành `RB-WP5-007`: authenticated host/member HTTP service, strict bounded
  DTO, RFC Problem Details, stable idempotent response và explicit write rate
  limit. Sửa request fingerprint để không chứa server-owned sequence, serialize
  create bằng composite advisory lock và pin patched `Microsoft.OpenApi 2.7.5`.
  Full Release PostgreSQL + Runner thật pass 116/116, vulnerability audit sạch.
- Hoàn thành `RB-WP5-008`: T2 ghi exact decision/certificate/projection/timeline/
  outbox atomically; T3 chỉ ghi matching ACK/checkpoint dưới owner+revision+DB-time
  fence. Fresh reconstruction phát lại hello/init/checkpoint/event và yêu cầu exact
  decision bytes/hash; mismatch fail closed `Diverged`. Audit bổ sung semantic
  binding cho promise service order. Tám crash windows chạy với PostgreSQL 17 và
  published Runner thật khớp clean oracle, không duplicate committed effect;
  full BeGo Debug/Release đều 125/125, frontend 7/7, RideBound 557/557.
- Hoàn thành `RB-WP5-009`: outbox claim exact per-run head bằng DB-time lease và
  monotonic attempt fence, commit trước external I/O, mark chỉ sau SignalR send,
  retry bounded và stale completion bị từ chối. Exact user-safe allowlist không
  phát route/node/budget/certificate witness/raw identity. Source audit phát hiện
  late sender có thể tạo stale duplicate sau lease takeover; stable wire
  `aggregateSequence`/message/hash cùng frontend monotonic delivery gate chặn
  callback cũ. Real PostgreSQL cover crash/reclaim/order/cross-run/T2 rollback;
  full BeGo Debug/Release 131/131, frontend 9/9 + production build, RideBound
  557/557 và NuGet/npm vulnerability audits sạch.
- Hoàn thành `RB-WP5-010`: exact `(sequence,id)` audit keyset, server-owned member
  scope, operator-only raw evidence, repeatable-read append-log rebuild/live hash
  và fail-closed pseudonymous export. Source audit phát hiện/sửa cross-member
  request access, JSONB canonical/hash mismatch, prefix cursor plan, partial
  migration downgrade, eager policy dependency và message-controlled exception
  classification. Real PostgreSQL cover concurrent append, drift/mutation,
  authorization, migration up/down/re-up và 12.000-row indexed `EXPLAIN`; full
  BeGo Debug/Release 138/138, frontend 9/9 + production build, RideBound 557/557.
- Hoàn thành `RB-WP5-011`: default Disabled không đăng ký COMMIT hosted worker;
  Shadow chỉ decision, Live mới relay. Exact Runner artifact preflight chặn claim/
  member API khi unhealthy. Durable immutable namespace lọc decision và hard-code
  outbox Live-only, nên shadow không publish sau restart/chuyển mode. PostgreSQL
  kiểm lease reclaim, shadow/live separation, old Session route snapshots và
  guarded rollback; full BeGo Debug/Release 147/147, frontend 9/9, RideBound 557/557.
- Hoàn thành `RB-WP5-012`: source-controlled BeGo-domain-shaped pseudonymous
  fixture bind raw/canonical workload, provenance, common policy và exact B1/C1
  config. Chỉ `policyId` được allowlist; effective config hash bind cả policy catalog
  và arm config. Harness stage exact copies để tránh preflight/use TOCTOU, rồi chạy
  hai clean process mỗi arm bằng cùng Runner DLL/work budgets. Exact materializer
  kiểm decision/certificate, checkpoint validator tính lại state/hash chain;
  normalized inputs giống nhau và repeat input/output/decision/checkpoint hash exact.
  Self-verifying bundle bind mọi file, harness source và executing BeGo assemblies;
  final manifest SHA-256 `b843bd20cbe9bf887d00998d4eaad54258848eb41d87ae49fd18a2142a0cb807`.
  BeGo Debug/Release 152/152, 0 skip; RideBound 557/557.
- Hoàn thành `RB-WP5-013`: independent test-owned transition oracle chạy 256×64
  bước, exact-set claim dưới 2/3/4 PostgreSQL worker, hard process crash tại đủ 8
  decision + 4 outbox durable boundary và fresh-Runner recovery exact. Năm mutant
  correctness bắt buộc đều bị phát hiện; queue 8/32/64 × worker 1/2/4 giữ raw
  warm-up/repetition/machine/row-count evidence. Self-verifying manifest SHA-256
  `e21fb0877fbc6d61bf6f1e24adcda24e09a29fea95a9f44d1b61bf4fc1061ca2`;
  BeGo Debug/Release 153/153, 0 skip; RideBound 557/557. Đây không phải LDFI/Elle/
  QuickCheck execution, mutation percentage, production SLA hoặc effectiveness.
- Hoàn thành `RB-WP5-014`: source-level WP1–WP5 audit phát hiện và sửa ba boundary
  thật: `commit_subject_links` trở thành append-only authorization evidence;
  `commit_outbox.operation_id` bắt buộc, chọn absolute head trước và chỉ claim khi
  exact same-run operation của head đã `Applied` (không skip head chưa T3); outbox
  batch tạo scope/DbContext độc lập theo run để run chậm không
  chặn run khác. Real PostgreSQL regression kiểm migration/immutability/pre-T3 claim,
  coordinated relay regression kiểm cross-run progress. BeGo Debug/Release trên hai
  fresh database + published Runner đạt 154/154, 0 skip; frontend 9/9, lint,
  TypeScript/build; full format và vulnerability audits sạch. Review WP1–WP5 kết
  luận GO chỉ cho refinement WP6, NO-GO cho main experiment/SLA/effectiveness.
- Hoàn thành `RB-WP6-001`: đọc toàn bộ 82 Markdown hiện có và nghiên cứu primary
  sources bằng in-app Browser. ADR-026 khóa FleetPy Manhattan v1/CC BY 4.0,
  canonical scenario/result contracts, addressable HMAC seed hierarchy, exact pinned
  Runner boundary, typed failure/exclusion/denominator rules, independent raw metric
  oracle, strict BagIt-compatible bundle, resource accounting và claim checker.
  Tạo contract decision-complete trong `docs/benchmarking/`, research evidence và
  ordered queue `RB-WP6-002..014`.
- Hoàn thành `RB-WP6-002`: thêm pure benchmark-contract project, 10 strict typed
  codecs/models, semantic validator, 10 Draft 2020-12 document schemas + common
  definitions, positive/negative fixtures và domain-separated identities. Hai clean
  process tái tạo exact six-hash published vector. Targeted 28/28, format sạch và
  required full solution 586/586; chỉ `RB-WP6-003` Ready, chưa download public data.
- Hoàn thành `RB-WP6-003`: registry khóa exact Zenodo FleetPy Manhattan v1 source;
  resumable downloader đã tải/xác minh `408878341` byte với publisher MD5 và local
  SHA-256; safe extractor preflight 335 members/`1022750557` uncompressed byte và
  inventory SHA-256. Rerun thật rehash rồi reuse object/extraction không chạm mạng.
  Targeted 26/26, format sạch và required full solution 612/612.
- Hoàn thành `RB-WP6-004`: ADR-027 sửa impossible scenario/report hash cycle thành
  provenance DAG schema `1.0.1`; deterministic normalizer rehash exact members, strict
  parse, directed SCC/Dijkstra, dense node-pool coverage optimizer + HMAC row rank,
  ties-to-even và typed source/time failures. Hai clean process tái tạo exact tiny
  8/2/16/240 và medium 128/32/96/9120 từ 21400 conserved public rows, kèm CC-BY/
  synthetic-overlay caveat. Targeted 29+32 và full solution 619/619.
- Hoàn thành `RB-WP6-005`: framed HMAC addressable seeds + published cross-process/
  int32 vectors; plan compiler bind caller config/protocol/candidate/validator/solver/
  work/capability/pairing, reject asymmetry/B5 mixing/noncanonical/oversized grid;
  materialize full warm-up/measured run set với collision-free repeat IDs và HMAC arm
  order trước outcome. Targeted 32+37, format sạch và full solution 627/627.
- Hoàn thành `RB-WP6-006`: ADR-028/contract `1.0.2` sửa gap typed failure cho
  cancellation/process-count/stream bytes; external-only supervisor pre/postflight
  exact Runner/runtime/config/source, isolated run root, canonical protocol
  hello/init/event/decision/ACK/checkpoint/shutdown và monotonic sampled process-tree
  limits. 15/15 fake/real supervisor cases pass; actual published WP3 fixture chạy
  qua pinned Runner process, không linked-core fallback. Contract 37/37,
  Benchmarking 52/52, format sạch và full solution 647/647; Windows Application
  Control `0x800711C7` không tái hiện.
- Hoàn thành `RB-WP6-007`: ADR-029/contract `1.0.3`; immutable per-plan/per-run
  intents, six raw roles, regenerated observation index, exactly-one terminal record,
  shared gapless failure/exclusion hash chain và planned terminal conservation. Seven
  injected crash boundaries, seal/in-flight race, 12-arm concurrency, tamper/path/
  denominator/resource/artifact/manifest/checkpoint mutations và full-grid-only
  authorized rerun đều fail/recover đúng. Artifact inventory được tự rederive và
  pre/postflight bind cùng launch; success transcript bind exact plan scenario/config/
  Runner binary. Benchmarking 77/77, Contracts 38/38, format sạch và required full
  solution 673/673, 0 failed/skipped; `0x800711C7` không tái hiện.
- Hoàn thành `RB-WP6-008`: canonical registry 36 định nghĩa và production calculator
  sinh đúng 132 integer rows/run với arrival-cohort, decision-time window, explicit
  numerator/denominator/missing/unit và semantic/resource evidence identity. Source
  audit bổ sung state-machine chặn time/epoch regression, terminal outcome conflict,
  defer/completion sai lifecycle và resource-terminal drift trước khi emit metric.
  `RideBound.Wp6MetricOracle` là executable BCL-only không ProjectReference, tự parse/
  canonicalize/hash/reconstruct rồi so toàn bộ row/evidence/metric-set byte-exact.
  McKeeman differential testing được áp dụng bằng request/action/promise/vector/window/
  order/resource/denominator/overflow mutations; Dolan–Moré chỉ khóa run-level paired
  evidence cho WP8/WP9, không tạo profile/effectiveness claim ở WP6. Benchmarking
  86/86, format sạch, Release `-warnaserror` sạch và required full solution 682/682,
  0 failed/skipped; WAC `0x800711C7` không tái hiện.
- Hoàn thành `RB-WP6-009`: deterministic strict BagIt 1.0 builder tạo exact LF/
  SHA-256 payload/tag manifests, oxum, logical no-self manifest, reviewed verifier
  script, build lock/private staging/atomic publication và không overwrite artifact.
  Exact Git dirty/source inventory, scenario/dataset, runtime/Runner/Contracts/
  harness/oracle/verifier assemblies, machine, registry và run-store grid/denominator
  được cross-bind. Ten-stage verifier dùng lại WP6-007 portable run verifier để kiểm
  raw file/artifact receipts/resources/transcript/ACK/checkpoint, kiểm global failure/
  exclusion sequence, production=oracle và còn tái tính production metric từ raw.
  Fresh-process verifier tự hash binary và chỉ tạo new external sidecar. Deterministic
  all-success + mixed-terminal fixtures và missing/extra/tamper/length/type/traversal/
  case/reparse/script/scenario/provenance/grid/transcript/log/oracle/correlated-metric
  mutations pass/fail đúng stage. Browser đối chiếu lại RFC 8493 + LOC conformance
  suite; ADR-031 khóa boundary. Benchmarking 92/92, format sạch, Release
  `-warnaserror` sạch và required full solution 688/688, 0 failed/skipped; WAC
  `0x800711C7` không tái hiện.
- Hoàn thành `RB-WP6-010`: ADR-032 khóa machine-readable
  `wp6-mechanical-only-v1` trong source/verifier; builder tự sinh profile và exact
  claim report, bind profile SHA-256 trong `reproducibility.json` `1.0.1`, caller/CLI
  không thể cấp profile hoặc report thay thế. Checker chỉ đọc README, manifest/plan/
  packaging-report labels và selected provenance flags; không đọc transcript, public
  trip/scenario/dataset hoặc source prose. Sáu caveat exact được mask trước scan;
  bounded NFKC/casefold/diacritic, punctuation dual skeleton, common Greek/Cyrillic
  confusable mapping và unsafe Unicode rejection trả typed code/rule/category/path/
  selector/original/normalized witness. Stage 10 tái tính exact report và chặn case,
  punctuation, confusable, default-ignorable, synonym, caveat, report, provenance,
  forged-result và profile-switch mutations đã reseal. ACM/NASEM/Unicode cùng Peng
  2011/Munafò 2017 khóa same-team/minimum-standard/non-confirmatory boundary.
  Benchmarking 95/95, format sạch, Release `-warnaserror` 0 warning/error và required
  full solution 691/691, 0 failed/skipped; WAC `0x800711C7` không tái hiện.
- Hoàn thành `RB-WP6-011`: ADR-033/contract `1.0.4` khóa tiny paired E2E correction.
  Source fixture có 1 xe/3 request/2 complete travel snapshot/6 epoch/16 event; B1/C1
  đều đi qua accept, capacity reject, revision và lifecycle completion. Tight feasible
  request ở epoch 2 buộc chèn một stop trước incumbent pickup nên exact certificate có
  decision-induced `prePickupInsertedStopCount=1`; traffic projection tách riêng
  pickup `+50 ms`, drop `+150 ms`. Plan bind cả WP3 commitment + WP4 arm config, và
  Runner chỉ lấy per-run solver seed qua explicit `manifest-master-seed`. Hai clean
  Release harness process, mỗi process B1/C1 × 3 measured repeat, khớp exact mọi
  semantic identity/per-run hash; resource samples và bundle chứa chúng được phép
  khác. Independent oracle summary được strict verifier kiểm lại theo assembly/raw/
  semantic/resource/row/metric hashes. Timeout/crash/solver unknown/incomplete/
  postflight/input/metric/missing/extra/tamper/selective-rerun matrix đều typed đúng
  stage. Bundle ignored `artifacts/wp6/tiny-paired-20260812-release/` có SHA-256
  `0936f8c26b9edb1086696e5a33a99a3a158459fbc1f31a3f53ce147fb03a1671`,
  fresh Release verifier + claim checker pass. Benchmarking 104/104, Algorithms
  136/136, format/Release sạch, required full solution 705/705; WAC không tái hiện.
- Hoàn thành `RB-WP6-012`: ADR-034/contract `1.0.5` khóa medium public-data gate.
  Verified Zenodo artifact SHA-256 `d9e86f33...599e`, MD5/license/length đạt; hai clean
  root sinh exact 128-request/32-vehicle/96-node/9.120-arc scenario `88a8730a...0e88`
  với 21.400 = 21.400 + 0 conservation. Dedicated synthetic-policy config sửa đúng
  identity mismatch từng gây `FLEET_SELECTION_CONFLICT`; validator/data không bị nới.
  Exact Runner conversation kiểm init/ACK/checkpoint/action/lifecycle. Hai fresh Release
  process đều chạy B1/C1 × 3 = 6/6 success và khớp plan/scenario/source/runtime/grid/
  transcript/decision/semantic metric cùng mọi per-run semantic hash; external verifier
  xác nhận bundles `4f3aa1fd...aa90` và `193c5616...8b44`. Resource/full/bundle hashes
  khác đúng contract. Instant-drain driver là nonphysical mechanics, cấm KPI/effectiveness
  claim. Format pass; required full solution 710/710, WAC không tái hiện.
- Hoàn thành `RB-WP6-013`: ADR-035/contract `1.0.6` làm executable warm-up 1 +
  measured 3 mỗi arm, đưa provenance/policy binding lên early preflight, derive
  conservation từ compiled grid và canonicalize driver failure code trước store.
  Required matrix cover nested canonical permutation/parallel, 21 failure-stage, 8
  exclusion, actual process/resource/store/bundle/metric/claim mutations và source
  nondeterminism audit. Fresh medium D/E đều 8/8 success; 13 semantic field cùng mọi
  per-run semantic hash exact, còn 8/8 full resource rows khác đúng contract. Raw
  negative strata giữ C1 chậm hơn B1 ở cả sáu local measured pair nhưng không tạo
  effectiveness/SLA claim. Release/format/dependency/schema/link gates sạch và exact
  full solution cuối 770/770; một run trước 769/770 do medium CPU control được giữ
  đúng là resource variance, không báo thành WAC.
- Hoàn thành `RB-WP6-014`: đọc lại toàn bộ Markdown và source WP1–WP6, audit cả
  protocol/state/physical/commitment/solver/BeGo durable boundary và WP6
  data→process→store→oracle→bundle logic. Không tìm thấy unresolved correctness/
  contract defect và không thêm heuristic chỉ dựa trên paper. Fresh tiny 8/8,
  medium H/I trên exact source cuối 8/8 mỗi process; 16/16 top-level + 72/72 per-run
  semantic fields exact,
  8/8 full resource rows khác hợp lệ, ba bundle external-verify. Required exact
  `dotnet test RideBound.slnx` pass 770/770; WAC không tái hiện. ADR-036, evidence và
  review `docs/reviews/wp1-wp6-final/` đóng WP6; không effectiveness/SLA claim.

## 3. Chưa làm

- Chưa có bằng chứng RideBound tốt hơn B1. WP9 cho kết quả âm có điều kiện ở 4/8 xe;
  WP10 không rescue và cũng không thiết lập Layer 3 claim.
- Layer 3 còn thiếu simulator có đủ observable mid-edge state. RidePy `nodeOnly`
  fail closed trên một workload concurrent-mid-edge; AMoD2 chưa được thực thi.
- O-001 vẫn khóa cross-vehicle reassignment. B4 chỉ là same-vehicle waiting-
  incumbent repair; không được báo thành reassignment optimizer.
- Incident recovery optimizer chưa có; ledger chỉ ghi breach đúng và không che nó
  bằng certificate normal-operation.
- Chưa có cross-city inference, SLA, satisfaction, fairness hoặc novelty claim.
- Công việc hiện hành theo yêu cầu người dùng: review file-by-file WP1–WP10, đối
  chiếu full PDF, thử optimization exploratory không rescue H6, benchmark và báo cáo
  PDF cuối.

## 4. Baseline verification

### RideBound

```text
.NET SDK 10.0.301
Build Release: 0 warnings, 0 errors
Architecture tests: 7 passed
Domain smoke tests: 1 passed
Date: 2026-07-28
```

### RB-WP1-001 protocol decision ticket

```text
dotnet test RideBound.slnx: passed
Architecture tests: 7 passed
Domain smoke tests: 1 passed
Markdown files checked: 48
Broken internal links: 0
Unbalanced code fences: 0
git diff --check: passed
Date: 2026-07-29
```

### RB-WP1-002–004 contract foundation

```text
.NET SDK 10.0.301
Release build: passed, 0 warnings, 0 errors
Contracts tests: 66 passed, 0 failed
Architecture tests from independent artifacts path: 7 passed, 0 failed
dotnet format --verify-no-changes: passed
NuGet direct/transitive vulnerability audit: no vulnerable packages
Full RideBound solution test: 73 passed; Domain smoke 1 blocked/reported failed
by Windows Application Control 0x800711C7, same pre-existing local blocker
Date: 2026-07-29
```

### RB-WP1-005–007 schema, handshake và initialize identity

```text
.NET SDK 10.0.301
Contracts tests: 95 passed, 0 failed
Runner boundary tests: 11 passed, 0 failed
Required dotnet test RideBound.slnx: 114 passed, 0 failed
Release build: passed, 0 warnings, 0 errors
dotnet format --verify-no-changes: passed
NuGet direct/transitive vulnerability audit: no vulnerable packages
Release full-suite attempt: 113 passed; Domain smoke 1 blocked/reported failed
by Windows Application Control 0x800711C7, same configuration-specific blocker
Date: 2026-07-29
```

### RB-WP1-008–015 Q1 closure và current revalidation

```text
.NET SDK 10.0.301
Release build: passed, 0 warnings, 0 errors
Release full solution at Q1 closure: 157 passed, 0 failed
  Contracts at closure: 114 passed
  Runner: 35 passed
  Architecture: 7 passed
  Domain: 1 passed
WP1 inventory after schema/vocabulary assertion và ba regression/E2E tests: 161
Current Debug full solution attempt:
  Contracts: 115 passed, 0 failed
  Architecture: 7 passed, 0 failed
  Domain: 1 passed, 0 failed
  Runner: 38 blocked before discovery by Windows Application Control 0x800711C7
WP1 Release full solution revalidation:
  Contracts: 115 passed, 0 failed
  Runner: 38 passed, 0 failed
  Architecture: 7 passed, 0 failed
  Domain: 1 passed, 0 failed
Current published CIBuild runner:
  RideBound.Runner.dll SHA-256:
  f3baa7daaec9b9167b52d9e110ac536a1bceff64e4f87498cc3e9d1be9d0c7c0
  Two clean processes: exit 0, empty stderr, exact expected output, byte-equivalent
Historical final Release full-suite attempt at Q1 closure:
  123 passed before Runner load
  Runner: 35 blocked before assertions by Windows Application Control
  At that closure point Runner source/tests were unchanged from their 35/35 pass
Real child-process NDJSON test at Q1 closure: passed
Current exact transcript replay through two clean child processes: passed
Changed-simTime duplicate corruption regression: passed
Published transcript replay twice/exact output/final hash at Q1 closure: passed
Tampered event changes decision hash at Q1 closure: passed
Required golden fixture inventory: exactly 10
dotnet format --verify-no-changes: passed
NuGet direct/transitive vulnerability audit: no vulnerable packages
JSON artifact parse audit: 66 files, 0 invalid
Current Markdown audit: 52 files, 0 broken internal links, 0 unbalanced fences
Default Debug full-suite final attempt: Architecture 7 passed; Domain 1 was
blocked; Runner reported 5 passed and 30 load-policy failures; Contracts reported
15 passed and 85 load-policy failures before completing its full 115-case
inventory. Enterprise Code Integrity policy
0283ac0f-fff1-49ae-ada1-8a933130cad6 blocked fresh DLL loads with 0x800711C7.
The Release rerun likewise blocked fresh Runner.dll. Event IDs 3033/3077 confirm
the signing-level policy; no assertion failure is used as correctness evidence.
Policy blocker reproduced for the fresh Debug Runner.Tests.dll but not for the
then-current WP1 Release suite, which passed 161/161.
Date: 2026-07-29
```

### RB-WP2-002–006 typed state, reducer và physical validator

```text
.NET SDK 10.0.301
Required dotnet test RideBound.slnx (Debug): 278 passed, 0 failed
Release full solution: 278 passed, 0 failed
  Contracts: 127 passed
  Domain: 89 passed
  Application: 13 passed
  Runner: 42 passed
  Architecture: 7 passed
Release build: 0 warnings, 0 errors
Whitespace format verification: passed
NuGet direct/transitive vulnerability audit: no vulnerable packages
Source JSON parse audit: 85 files, 0 invalid
Markdown audit: 52 files, 95 local links valid, 0 unbalanced fences
Git diff whitespace/error audit: passed
Typed WP2 schema additions: 16
Published WP2 fixture flow: bootstrap map/reduce/ack + epoch two pass
Lifecycle transition matrix: exhaustive pass
Small route precedence permutations: 24/24 match expected feasibility
Physical mutation dimensions: capacity, pickup window, max ride, precedence,
  connectivity, stop location, frozen prefix, plan version,
  onboard/accepted preservation, reassignment
Q1 exact transcript/hash/idempotency regression: pass
Date: 2026-07-29
```

### RB-WP2-007–012 B1, exact-small, online Runner và WP2 closure

```text
.NET SDK 10.0.301
Release build --warnaserror: passed, 0 warnings, 0 errors
dotnet format --verify-no-changes: passed; 0/137 files changed
Logical source-controlled test inventory: 333
  Contracts: 128
  Domain: 89
  Application: 15
  Algorithms: 45
  Runner: 49
  Architecture: 7
Required dotnet test RideBound.slnx (Debug): 333/333 passed
  Contracts: 128/128 passed
  Domain: 89/89 passed
  Application: 15/15 passed
  Algorithms: 45/45 passed
  Runner: 49/49 passed
  Architecture: 7/7 passed
Release full-solution xUnit attempt:
  Contracts: 128/128 passed
  Domain: 89/89 passed
  Architecture: 7/7 passed
  Application/Algorithms/Runner: Windows Application Control blocked fresh
  unsigned Application/Runner DLL loads with 0x800711C7 before assertions
Policy-safe supplemental execution for Release artifacts:
  Application: 15/15 passed
  Algorithms: 45/45 passed
  Runner non-child-process: 46/46 passed
Runner child-process cases verified separately: 3/3
  Q1 conformance two-process exact replay: passed
  stdout/stderr diagnostic isolation: passed
  WP2 online two-process exact replay: passed
NuGet direct/transitive vulnerability audit: no vulnerable packages
Portable Domain/Application forbidden-dependency scan: passed
Source JSON parse audit: 89 files, 0 invalid
Markdown audit: 53 files, 0 broken local links, 0 unbalanced fences
Git diff whitespace/error audit: passed
Algorithms detail:
  Hand-enumerated generator/policy cases: 13
  Independent exact-small published seeds: 32/32
  Generator gap: 0 in published bound
  Selection gap: 0 in published bound
Tiny online demo:
  epochs: 4
  lifecycle: accept -> pickup/board -> drop/alight
  physical rejection: r-2 / CAPACITY
  clean self-contained processes: 2/2 byte-exact
  final decision hash:
  56825f3591fb5d10f4c258d2c05897c016d82cb91c1318ffa23731c920146680
WP2 scope exclusion audit:
  no ledger/certificate produced, no hard budget, no OR-Tools behavior,
  no simulator adapter
Date: 2026-07-30
```

### RB-WP3-001–014 closure: commitment correctness boundary

```text
Logical source-controlled test inventory: 414
  Contracts: 133
  Domain: 134
  Application: 34
  Algorithms: 48
  Runner: 58
  Architecture: 7

Required command revalidation on 2026-08-03:
  dotnet test RideBound.slnx
  Result: 414/414 passed; exit code 0; 0 failed; 0 skipped.
  Contracts: 133/133 passed.
  Domain: 134/134 passed.
  Application: 34/34 passed.
  Algorithms: 48/48 passed.
  Runner: 58/58 passed.
  Architecture: 7/7 passed.
  Windows Application Control 0x800711C7 is no longer a current blocker.

Historical host-policy evidence on 2026-08-02:
  required attempts were blocked while loading fresh unsigned Contracts,
  Application and Runner DLLs. Code Integrity events 3033/3077 identified Smart
  App Control policy {0283ac0f-fff1-49ae-ada1-8a933130cad6}. This remains an
  environment record, not a current test failure.

Supplemental same-tree assertion evidence:
  Contracts Release: 133/133 passed.
  Domain Debug in required attempt: 134/134 passed.
  Application Debug in required attempt: 34/34 passed.
  Algorithms Debug in required attempt: 48/48 passed.
  Architecture Debug in required attempt: 7/7 passed.
  Runner exact xUnit methods through self-contained policy-safe harness:
    54/54 non-child-process cases passed.
  Runner child-process cases: 4/4 behavior passed independently:
    Q1 transcript two byte-exact clean processes;
    Q1 stdout/stderr diagnostic isolation;
    WP2 demo two byte-exact clean processes;
    WP3 commitment demo + checkpoint restore clean processes.

Quality/replay gates:
  Release build --warnaserror: 0 warnings, 0 errors.
  dotnet format --verify-no-changes --no-restore: passed after import-order fix.
  source JSON parse: 104/104 passed.
  Markdown: 58 files, 112 relative links, balanced fences.
  portable dependency scan: passed; NuGet direct/transitive audit: clean.
  git diff --check: passed (only configured LF→CRLF worktree warnings).
  WP2 final decision hash:
    c95c3f7e651a5ff5f366051538ecc53663696baa13fbec967d769af5f3c5d90f
  WP3 final decision hash:
    54ebbbdda6753654aab43d522d9d24bffefe56426275035d685ecc8588371589
  WP3 final state hash:
    d91c91c661dd3a2d2de6d5e214bef2a55a9384d635520ca7d5bdbe9d15694527
  Checkpoint restore suffix: byte-equal to uninterrupted genesis replay.
  WP2/WP3 scripts write explicit UTF-8 without BOM and reject non-empty stderr,
    removing PowerShell 5/7 native-pipeline encoding ambiguity.

WP3 correctness evidence:
  all 10 vector dimensions have exact-boundary and killing mutations;
  hard zero/unbounded/overflow/unknown vocabulary semantics are explicit;
  64 seeds × 12 generated ledger revisions preserve P1/no-refund;
  16 independent exact-small seeds match P2 normal hard-gate behavior;
  16 P3 seeds prove relaxing ETA hard limit 40→160 cannot shrink feasible set;
  physical/state-boundary/lock/budget order is independently recomputed;
  incident breach, certificate publication IDs and checkpoint relations are
    structurally cross-validated rather than trusted from solver output.

Browser research recheck using the in-app Browser:
  Gaul et al. 2021 rolling-horizon MILP;
  Schulz & Pfeiffer 2026 forward slack/precomputation;
  Geržinič et al. 2023 stated-preference survey;
  Tiwari et al. 2024 weighted/Pareto/lexicographic objectives;
  Ackermann & Rieck 2025 multiple-plan dynamic DARP.
  Outcome: no numeric paper default adopted; hard gate stays outside objective;
  schedule strategy, bounded precompute and multiple-plan belong to WP4.

Claim limit:
  414/414 is now a full-solution Debug xUnit pass on this host.
  WP3 still proves mechanical correctness in published small bounds, not scale,
  effectiveness, solver optimality or user satisfaction.
Final recheck date: 2026-08-03
```

### RB-WP4-002 closure: solver-neutral selection boundary

```text
Logical source-controlled test inventory: 435
  Contracts: 133
  Domain: 134
  Application: 54
  Algorithms: 48
  Runner: 58
  Architecture: 8

Required command on 2026-08-03:
  dotnet test RideBound.slnx
  Result: 435/435 passed; exit code 0; 0 failed; 0 skipped.

Production boundary:
  CandidateSelectionProblem canonicalizes vehicles/requests/options while
  retaining declared lexicographic objective order.
  Exactly one no-op is required per vehicle; a validated solution selects
  exactly one option per vehicle and accepts each request at most once.
  Sum/Maximum aggregation fails closed on canonical-integer overflow.
  Deterministic work/time/seed budget is separate from observed wall time.
  Bound direction, exact rational gap, bound order and incumbent/solution match
  are validated before OPTIMAL/FEASIBLE may be reported.
  OPTIMAL, FEASIBLE, INFEASIBLE, UNKNOWN, MODEL_INVALID and SAFE_FALLBACK remain
  distinct; no Google.OrTools or other solver package entered Application.

Adversarial evidence:
  20 new Application test cases include missing/duplicate no-op, unknown entity,
  duplicate request, invalid vector/range, aggregation overflow, lexicographic
  dominance, reversed bound, exceeded deterministic budget, reordered bound and
  false-optimal rejection. Architecture adds the Application port-location gate.
```

### RB-WP4-003 closure: executable scheduling and conservative slack

```text
Logical source-controlled test inventory: 444
  Contracts: 133
  Domain: 134
  Application: 54
  Algorithms: 57
  Runner: 58
  Architecture: 8

Required command on 2026-08-03:
  dotnet test RideBound.slnx
  Result: 444/444 passed; exit code 0; 0 failed; 0 skipped.
  dotnet format RideBound.slnx --verify-no-changes --no-restore: passed.

Mechanism:
  Backward slack combines pickup-arrival deadline, drop-off ride-time deadline,
  waiting absorption and future-stop slack under a fixed projected schedule.
  The value is named CertifiedDelay: delay <= certificate is sufficient for
  time feasibility; delay > certificate is never treated as infeasibility.
  Cache key binds exact immutable run snapshot, full vehicle snapshot and
  position, structural route fingerprint, evaluation time, travel version/hash.
  Cache is bounded and failures are not cached.
  Cache may rank a frontier node but cannot admit it; PhysicalPlanValidator
  always runs before a profile can enter a retained candidate.
  origin-hold-relocated-wait moves only first-pickup waiting already present to
  a current-node waypoint with real service duration; edge progress and
  unexecuted frozen prefix are refused. The transformed route is fully
  revalidated and must preserve original stop service/departure times and cost.

Mutation/equivalence evidence:
  9 new Algorithms tests cover backward arithmetic, every delay through the
  certificate boundary, executable hold equivalence, edge refusal, independent
  run/vehicle/position/route/time/travel invalidation, travel-duration mutation,
  cached/uncached equality, cache-cannot-bypass-validator and repeated-build hits.
```

### RB-WP4-004 closure: bounded best-first generation and loss accounting

```text
Logical source-controlled test inventory: 449
  Contracts: 133
  Domain: 134
  Application: 54
  Algorithms: 62
  Runner: 58
  Architecture: 8

Required command on 2026-08-03:
  dotnet test RideBound.slnx
  Result: 449/449 passed; exit code 0; 0 failed; 0 skipped.
  dotnet format RideBound.slnx --verify-no-changes --no-restore: passed.

Bounded semantics:
  request priority = (latestPickup, arrivalTime, requestId);
  global search priority = potential accepted count, mandatory-service lower
  bound, conservative forward slack, stable digest;
  one deterministic work unit = one best frontier node dequeued;
  unexpanded frontier subtrees are counted combinatorially, with explicit
  canonical saturation instead of overflow or a fabricated exact number;
  cap retains the required safety no-op, then orders feasible candidates by
  accepted count, exact operational cost, slack and candidate ID.

Loss boundary:
  REQUEST_BOUND_OMISSION identifies known omitted requests;
  WORK_BOUND_OMISSION counts raw paths whose feasibility remains unknown;
  CANDIDATE_CAP_OMISSION counts already validated feasible candidates;
  every category has stable digest and count, separate from later solver loss;
  exact mode fails if request/work/candidate omission would occur.

Evidence:
  5 new Algorithms tests cover urgent-request priority, exact work fail-closed,
  exhaustive path conservation, best-first high-acceptance retention, feasible
  cap conservation/digest stability and work-monotonic acceptance.
```

### RB-WP4-005 closure: B2 revision penalty and B3 fixed freeze

```text
Logical source-controlled test inventory: 458
  Contracts: 133
  Domain: 135
  Application: 54
  Algorithms: 70
  Runner: 58
  Architecture: 8

Required command on 2026-08-03:
  dotnet test RideBound.slnx
  Result: 458/458 passed; exit code 0; 0 failed; 0 skipped.
  dotnet format RideBound.slnx --verify-no-changes --no-restore: passed.

B2 rolling-penalty:
  mechanism provider replaces all ten cumulative hard limits with unbounded and
  removes optional freeze/final-confirmation locks, while preserving material
  rule, budget basis and the global O-001 accepted-assignment lock;
  every raw candidate is assessed through the full validator; assessment does
  not mutate/prune the shared raw pool;
  selector order is accepted count, material ETA revision count, the stable ten
  revision dimensions, canonical operational cost and candidate-ID vector.

B3 fixed-freeze-horizon:
  constructor requires a positive explicit horizon and non-empty valid lock mask;
  all cumulative limits remain unbounded and no numeric default exists;
  freeze activates inclusively at timeToPickup <= horizon and source hard budgets
  cannot accidentally prune outside the configured horizon.

Additional correctness fix:
  CommitmentVector.Add and both exact fleet selectors now fail before canonical
  overflow instead of allowing a non-canonical total or risking runtime overflow.

Evidence:
  8 new Algorithms cases include lexicographic precedence, dimension order,
  explicit configuration, canonical cost, B2 raw-pool preservation over 16
  seeds, and B3 exact horizon boundary; 1 Domain vector-overflow regression.
```

### RB-WP4-006 closure: B4 same-vehicle waiting-incumbent repair

```text
Logical source-controlled test inventory: 465
  Contracts: 133
  Domain: 135
  Application: 54
  Algorithms: 77
  Runner: 58
  Architecture: 8

Required command on 2026-08-03:
  dotnet test RideBound.slnx
  Result: 465/465 passed; exit code 0; 0 failed; 0 skipped.
  dotnet format RideBound.slnx --verify-no-changes --no-restore: passed.

B4 repair boundary:
  disabled by default so the B1 candidate set and choice remain unchanged;
  an enabled positive cap admits only Accepted/WaitingPickup incumbents assigned
  to the same vehicle, not onboard, with one pickup/drop pair wholly inside the
  mutable suffix and no unexecuted frozen request stop;
  a seed removes exactly that pair and enumerates every precedence-preserving
  reinsertion; it never combines two repaired pairs or mutates source routes;
  exact mode fails if the repair-request cap omits an eligible incumbent;
  bounded mode reports repair omission count/digest separately and marks the
  diagnostics incomplete;
  every repaired route is physically revalidated and O-001 still prevents
  cross-vehicle reassignment.

Correctness defect found by adversarial testing:
  the original frontier stable ID reused an order-insensitive omission digest,
  so route permutations could collapse into one search identity;
  search nodes now use an order-sensitive token digest while omission-set
  digests remain canonical and order-insensitive.

Evidence:
  7 new Algorithms tests cover atomic pair reinsertion and input immutability,
  frozen/onboard exclusion, exact cap failure, bounded loss stability, disabled
  B1 equivalence, repaired route diversity and cheaper B4 selection without
  reassignment.
```

### RB-WP4-007 closure: B5 canonical multiple-plan pool

```text
Logical source-controlled test inventory: 477
  Contracts: 133
  Domain: 135
  Application: 57
  Algorithms: 82
  Runner: 62
  Architecture: 8

Required command on 2026-08-03:
  dotnet test RideBound.slnx
  Result: 477/477 passed; exit code 0; 0 failed; 0 skipped.
  dotnet format RideBound.slnx --verify-no-changes --no-restore: passed.

Canonical state/checkpoint boundary:
  pool version zero is the only empty value; every non-empty replacement advances
  the previous version exactly once;
  exact plan SHA-256 binds source epoch, ordered vehicle IDs, route version,
  progress, frozen/mutable order and every executable stop field;
  checkpoint restore recomputes the ID and checks the exact vehicle set,
  request-stop assignment membership, frozen/executed compatibility, physical
  feasibility and distinguished-plan equality with the online run.

B5 selection:
  one shared generated candidate set feeds deterministic fleet enumeration;
  pool size and combination work cap are explicit configuration, with exact
  fail-closed and bounded truncation diagnostics;
  alternatives must preserve the distinguished new-request assignment;
  semantic duplicates are removed, Pareto dominance uses accepted count,
  operational cost and conservative forward slack, then top-K uses greedy
  max-min route distance;
  distinguished control maximizes shared executable-prefix consensus before
  operational/stable tie-breaks;
  only distinguished request actions/routes are applied or exposed.

Executable alternative correction:
  adversarial review found that all candidates originate before publication, so
  a non-distinguished route can have the old or same version after the chosen
  route is applied;
  every different retained alternative is therefore rebuilt at exactly
  distinguished route version + 1 and physically validated against the proposed
  run before it can survive checkpoint restore.

Evidence:
  3 Application identity/version/rehydration cases;
  5 Algorithms dominance, stable diversity/consensus, exact/bounded work,
  assignment compatibility and distinguished-only publication cases;
  4 Runner canonical round-trip, forged-ID, forged-distinguished and actual
  policy-output checkpoint cases.
```

### RB-WP4-008 closure: C1 hard-vector lexicographic policy

```text
Logical source-controlled test inventory: 483
  Contracts: 133
  Domain: 135
  Application: 57
  Algorithms: 88
  Runner: 62
  Architecture: 8

Required command on 2026-08-03:
  dotnet test RideBound.slnx
  Result: 483/483 passed; exit code 0; 0 failed; 0 skipped.
  dotnet format RideBound.slnx --verify-no-changes --no-restore: passed.

C1 hard boundary:
  the common raw physical pool is generated once;
  each candidate is applied and full WP3 commitment validation both removes
  hard-invalid candidates and returns the authoritative validated ledger in one
  pass; C1 neither adds candidates nor repeats the validator as a separate filter;
  feasibility remains exact per request/dimension/phase and never uses PPM.

Lexicographic ranking:
  maximize accepted requests;
  minimize the worst cumulative BudgetAfter/hard-limit ceiling PPM across active
  riders and applicable finite dimensions;
  minimize the stable ten decision-induced revision dimensions in vocabulary
  order, then canonical operational cost and candidate-ID vector;
  UInt128 multiplication prevents overflow at the canonical integer maximum;
  a feasible zero-limit/zero-usage dimension ranks as 1,000,000 ppm because it
  has no reserve, while non-zero usage remains hard-invalid;
  when no applicable finite hard limit exists, utilization and revision ranking
  are disabled so C1 is semantically identical to B1.

Evidence:
  6 new Algorithms tests cover accepted/utilization/revision/cost dominance,
  exact dimension order, one-pass retained-set equality with the reference hard
  filter, 1/3 and canonical-maximum ceiling arithmetic, zero-limit semantics and
  unbounded no-lock exact-small B1 equivalence.
```

### RB-WP4-009 closure: C2 warning/soft-hard hybrid

```text
Logical source-controlled test inventory: 489
  Contracts: 133
  Domain: 135
  Application: 57
  Algorithms: 94
  Runner: 62
  Architecture: 8

Required command on 2026-08-03:
  dotnet test RideBound.slnx
  Result: 489/489 passed; exit code 0; 0 failed; 0 skipped.
  dotnet format RideBound.slnx --verify-no-changes --no-restore: passed.

C2 configuration and hard boundary:
  every warning profile explicitly defines all ten dimensions once; null means
  disabled and no numeric warning default exists;
  an enabled warning requires a finite hard limit and warning <= hard;
  C2 calls the same one-pass C1 validator/assessor, so warning never admits a
  hard-invalid candidate or creates a candidate absent from the shared raw pool.

Objective:
  maximize accepted count, minimize worst hard PPM, then minimize the ordered
  ten-dimension warning-excess vector, ordered decision-induced revision vector,
  canonical operational cost and candidate-ID vector;
  warning excess is accumulated per scoped vehicle/rider with checked canonical
  arithmetic, preserving ms/count/mm dimensions instead of a weighted scalar;
  if every warning is disabled, C2 delegates directly to the C1 selector and
  produces no synthetic warning objective/output.

Evidence:
  6 new Algorithms tests cover warning-before-revision/cost dominance, explicit
  ten-dimension profile shape, exact C1/C2 retained hard set, non-zero boundary
  excess, warning-above-hard rejection and disabled-warning C1 equivalence.
```

### RB-WP4-010 closure: deterministic OR-Tools adapter

```text
Logical source-controlled test inventory: 495
  Contracts: 133
  Domain: 135
  Application: 57
  Algorithms: 94
  Solvers.OrTools: 5
  Runner: 62
  Architecture: 9

Required command on 2026-08-03:
  dotnet test RideBound.slnx
  Result: 495/495 passed; exit code 0; 0 failed; 0 skipped.
  dotnet format RideBound.slnx --verify-no-changes --no-restore: passed.

Adapter boundary and model:
  Google.OrTools 9.15.6755 is pinned only in RideBound.Solvers.OrTools;
  BoolVar selection enforces exactly one option per vehicle and at most one
  assignment per request;
  integer Sum objectives use weighted sums, Maximum objectives use an auxiliary
  integer variable with AddMaxEquality;
  canonical upper-bound arithmetic fails ModelInvalid before native model build
  when an aggregate could exceed Int64.

Lexicographic solve and diagnostics:
  every objective pass rebuilds the model with equality constraints for prior
  objective values proven OPTIMAL; a merely FEASIBLE pass is never fixed as if
  optimal;
  one worker, explicit seed, remaining conflict budget and deterministic-time
  budget make the outcome independent of observed wall time;
  OPTIMAL, FEASIBLE, UNKNOWN, INFEASIBLE and MODEL_INVALID remain distinct;
  selected IDs are revalidated by CandidateSelectionSolution.Create and exact
  bounds are rounded conservatively according to minimization/maximization.

Evidence:
  5 solver tests cover four-pass Sum/Maximum trade-offs plus request uniqueness,
  acceptance-before-cost, eight identical deterministic repetitions, aggregate
  overflow and diagnostic budget/version detail;
  1 architecture test prevents the native package from leaking outside the
  solver adapter project.
```

### RB-WP4-011 closure: deterministic deadline and safe fallback

```text
Logical source-controlled test inventory: 507
  Contracts: 133
  Domain: 135
  Application: 69
  Algorithms: 94
  Solvers.OrTools: 5
  Runner: 62
  Architecture: 9

Required command on 2026-08-03:
  dotnet test RideBound.slnx
  Result: 507/507 passed; exit code 0; 0 failed; 0 skipped.
  dotnet format RideBound.slnx --no-restore: passed.

Budget and loss boundary:
  deterministic execution budget has independent generation-work,
  semantic-validation-work and solver conflict/deterministic-time limits;
  observed wall time remains a metric and cannot change replay selection;
  pre-solve accounting preserves omitted candidate count, canonical lowercase
  SHA-256 digest and saturation, separately from primary solver status/loss.

Independent validation and fallback:
  every primary solution, including OPTIMAL or FEASIBLE, must pass the injected
  semantic/full-state validator before it can leave the executor;
  fallback order is canonical no-op, then every one-request insertion sorted by
  the exact objective vector and selected option IDs;
  each attempted solution consumes one validation work unit and records a typed
  rejection witness with path and selected IDs;
  exhaustion or an entirely rejected portfolio returns UNKNOWN with no solution;
  no incident result can be fabricated at this solver-neutral boundary;
  primary bounds stay in audit diagnostics, while a fallback result has no
  mismatched incumbent bounds.

Evidence:
  12 new Application cases cover validated optimal, truthful feasible, all three
  no-solution statuses, rejected incumbent, ordered single-request rescue,
  validation exhaustion, rejected portfolio, separate candidate/solver loss,
  accounting contract and cross-budget misuse.
```

### RB-WP4-012 closure: named policy/solver Runner integration

```text
Logical source-controlled test inventory: 523
  Contracts: 133
  Domain: 135
  Application: 69
  Algorithms: 101
  Solvers.OrTools: 5
  Runner: 71
  Architecture: 9

Required command on 2026-08-03:
  dotnet test RideBound.slnx
  Result: 523/523 passed; exit code 0; 0 failed; 0 skipped.
  dotnet format RideBound.slnx --no-restore: passed.

Configuration and identity:
  one canonical registry round-trips the seven published B1–B5/C1/C2 names;
  strict WP4 JSON declares generation cap/work/schedule, solver stage budgets and
  only the mechanism-specific B3 freeze, B4 repair, B5 pool or C2 warning fields;
  C2 has one explicit ten-dimension profile per commitment policy and every
  enabled warning is bounded by a finite hard limit;
  WP4 config hash is domain-bound to the commitment config hash, and initialize
  fails before state creation unless manifest policy ID/version/combined hash
  match the loaded implementation exactly.

Solver and publication path:
  B1–B4/C1/C2 generate one shared physical pool and map their exact hierarchy to
  candidate-selection objectives; vehicle-ordered ID ranks preserve the existing
  deterministic final tie-break without weighted scalarization;
  OR-Tools output and fallback pass full semantic validation with the baseline's
  effective commitment provider; Runner independently validates again;
  B5 keeps deterministic plan-pool enumeration and publishes only distinguished;
  ledger, certificate, plan pool and state hash stay in the pending transaction,
  and only matching ACK commits them; solver completed/safeFallback is part of
  the hashed decision shell, so retry is byte-identical.

Evidence:
  7 Algorithms cases cover registry round-trip, B1 request uniqueness/cost, B2
  material+dimension hierarchy, C1 maximum utilization, C2 warning hierarchy,
  unbounded C1=B1 and semantic-validator fallback;
  9 Runner cases cover strict variant config, warning/hard boundary, binding,
  actual OR-Tools decision, retry/wrong ACK/commit, injected UNKNOWN fallback,
  manifest mismatch, B5 ACK/checkpoint restore and real child-process CLI.
```

### RB-WP4-013 closure: independent evidence

```text
Logical source-controlled test inventory: 557
  Contracts: 133
  Domain: 135
  Application: 69
  Algorithms: 134
  Solvers.OrTools: 6
  Runner: 71
  Architecture: 9

Required command on 2026-08-03:
  dotnet test RideBound.slnx
  Result: 557/557 passed; exit code 0; 0 failed; 0 skipped.

Independent correctness:
  B1 production generator/selector matches an independent exact enumerator over
  64 deterministic fixtures within the published 2-vehicle/2-request bound;
  C1 production objective mapper plus actual OR-Tools matches a separately coded
  enumerator over 64 fixtures, selecting identical candidate IDs with OPTIMAL
  status and exact zero gap on every objective level;
  the hard-gate mutation fixture proves the raw set is strictly larger than the
  hard-feasible set, so deleting the gate is observably killed;
  an actual bounded request omission reaches execution count/digest/saturation
  accounting separately from an injected solver UNKNOWN and validated no-op.

Cross-ticket evidence retained:
  cache on/off and route/travel invalidation equivalence; infinite C1=B1 and
  disabled C2=C1; plan-pool checkpoint/tamper; deterministic deadline and
  fallback; ACK/retry publication gates.

Synthetic performance signal:
  4/16/32/128 Boolean-option models all reached exact OPTIMAL. Observed p50 wall
  times were 2.389/12.160/21.406/91.004 ms on .NET 10.0.9, Windows 10.0.26200,
  X64, 12 processors. This is machine-local candidate-selection evidence only;
  it is not a demand-scale, service-quality or effectiveness claim.
```

### RB-WP4-014 closure: full audit and handoff

```text
Quality gates on 2026-08-03:
  dotnet test RideBound.slnx: 557/557 passed
  Release build --no-restore /warnaserror: 0 warnings, 0 errors
  WP4 microbenchmark Release build: 0 warnings, 0 errors
  dotnet format --verify-no-changes: passed
  NuGet direct/transitive vulnerability audit: no vulnerable packages reported
  JSON/Markdown internal-link/fence/diff/process gates: passed

Logic audit:
  reviewed contract/state/physical/commitment/candidate/policy/solver/Runner paths,
  not only test summaries; no production TODO/placeholder or solver dependency
  leak was found. Candidate loss, solver loss and publication failure stay
  distinct. Every solver/fallback selection is independently validated, Runner
  validates again, and only matching ACK commits route/ledger/pool/hash state.

Artifacts:
  ADR-024; tasks/30 complete; docs/reviews/wp1-wp4-final explains WP1-WP4 flow,
  each important production file, paper-to-code optimization, test evidence,
  synthetic curve and unproven claims. Historical wp1-wp3 review is preserved.

Handoff:
  WP4 is Complete and Q2 mechanical correctness is closed. The only READY ticket
  is refinement-only RB-WP5-001; no BeGo implementation ticket exists yet.
```

### Historical CI hardening checkpoint — 2026-07-28

```text
Release build: passed, 0 warnings, 0 errors
Whitespace format verification: passed
NuGet vulnerability audit: no vulnerable direct/transitive packages
Runner publish smoke: passed
Architecture reference graph with normalized separators: passed
Local xUnit execution at this historical checkpoint: blocked by Windows Application Control (0x800711C7)
Linux CI confirmation: pending
Date: 2026-07-28
```

### BeGo backend

```text
.NET SDK 10.0.301
Passed: 25
Failed: 0
Skipped: 0
Date: 2026-08-05 (WP5 refinement recheck)
```

### BeGo frontend

```text
Passed: 7
Failed: 0
Warning: package type/module performance warning
Date: 2026-08-05 (WP5 refinement recheck)
```

### RB-WP5-001 BeGo integration refinement

```text
RideBound checkout: 44ef6a7cacdc58e7c6c0576430fcd7bb02e76c7a
BeGo checkout: ebe0d34365ec4751bd5c629677733032490a1a0d
dotnet test RideBound.slnx: 557/557 passed
BeGo dotnet test src\OptiGo.slnx --no-restore --verbosity minimal: 25/25 passed
BeGo frontend npm test: 7/7 passed
Browser research: Saltzer/Reed/Clark; Helland; transactional outbox; EF
  transactions/concurrency; PostgreSQL SKIP LOCKED; hosted services; expired
  IETF Idempotency-Key draft (prior art only)
Artifacts: ADR-025, tasks/32, research/wp5-distributed-integration-evidence...
Implementation at this checkpoint: none; only RB-WP5-002 READY
Date: 2026-08-05
```

### RB-WP5-002 Application boundary và durable state invariants

```text
Targeted Debug integration tests: 32/32 passed
Targeted Release build /warnaserror: 0 warnings, 0 errors
Targeted Release integration tests: 32/32 passed
Full BeGo backend: 57/57 passed
Required RideBound solution: 557/557 passed
Targeted dotnet format --verify-no-changes: passed
Logic audit: exhaustive transition pair matrix; terminal/revision/time/sequence/
  canonical-range/default-invalid/UTF-8/frame/payload-conflict/hash/order/checkpoint
  boundaries covered; no TODO/framework/core dependency leak
Full BeGo Release /warnaserror: NOT PASSED — pre-existing transitive
  Microsoft.OpenApi 2.0.0 high-severity advisory through
  Microsoft.AspNetCore.OpenApi 10.0.1. Assemblies compiled, build exit 1 on NU1903.
Date: 2026-08-05
```

### RB-WP5-003 append-only EF/PostgreSQL persistence foundation

```text
Migration: 20260805155554_AddCommitIntegrationPersistence
Schema: 11 commit_* tables; five append-only evidence triggers
Real PostgreSQL: postgres:17-alpine, 1/1 Debug and 38/38 targeted Release passed
Real DB cases: guarded empty up/down/re-up; data-loss rollback refusal; duplicate
  event sequence/decision epoch/idempotency; one active op/run; cross-run FK;
  optimistic revision conflict; Session SET NULL without evidence loss
Full BeGo backend: 62 passed, 1 opt-in PostgreSQL test explicitly skipped
Required RideBound solution: 557/557 passed
Targeted dotnet format --verify-no-changes: passed
Date: 2026-08-05
```

### RB-WP5-004 durable T1 intake/lease store

```text
Store: PostgresCommitIntakeStore implements narrow ICommitIntakeStore
T1: run row lock + exact frame binding + idempotency + event/op/run atomic commit
Claim: ordered FOR UPDATE SKIP LOCKED; DB timestamp lease; transaction committed
  before returning work; expired lease reclaim increments revision/attempt
Backpressure: short transaction advisory lock + bounded pending count
Canonical replay: exact bytea, never jsonb-rendered text
Real PostgreSQL clean-run stress: 5/5 passed
Targeted Release with PostgreSQL: 40/40 passed
Full BeGo backend: 64 passed, 1 opt-in PostgreSQL test explicitly skipped
Required RideBound solution: 557/557 passed
Date: 2026-08-05
```

### RB-WP5-005 pinned long-lived Runner process supervisor

```text
Runtime: one long-lived process/session per run; bounded configurable pool
Pinning: absolute command/artifact path + exact artifact SHA-256 + core commit
Protocol: strict UTF-8 NDJSON input/output bound; bounded stderr drain; exact
  hello schema/capability and initialize manifest/provenance binding
Failure semantics: timeout/cancellation/malformed/context mismatch discards
  session and kills the owned process tree; uncertain process is never reused
Atomic client step: decisionApplied write + checkpoint write/read under one gate
Adversarial process tests: 16/16 non-opt-in passed
Published RideBound Runner Release online cycle: 1/1 passed
Targeted Debug/Release /warnaserror with actual Runner: 17/17 passed
Full BeGo Debug without opt-ins: 80 passed, 2 explicit integration skips
Full BeGo Release with PostgreSQL 17 + published Runner: 82/82, 0 skip
BeGo frontend: 7/7 passed (existing module-type performance warning retained)
Required RideBound solution: 557/557 passed
Targeted dotnet format and git diff --check: passed
Date: 2026-08-05
```

### RB-WP5-006 deterministic BeGo bootstrap mapper

```text
Capture boundary: immutable semantic preparation completes before travel/Runner I/O
Privacy: run-local HMAC-SHA256 pseudonyms; restricted subject links; secret buffers zeroed
Mapping: only necessary venue/eligible-vehicle/active-passenger nodes; no legacy
  assignment, route, ledger, raw name/email/account ID enters Runner protocol
Units: WGS84 -> E7 and seconds -> milliseconds with round-ties-to-even evidence
Travel: exact square directed matrix; finite/range/sentinel/diagonal/off-diagonal
  reachability validation; configurable <=4096 node cap checked before O(n²) I/O
Determinism: ordinal semantic order, contiguous events, canonical JSON/hash stable
Negotiation: manifest binds exact helloAck selection; domain-separated manifest hash
Mapper tests with actual Runner enabled: 16/16 passed
Supervisor + mapper targeted Release: 31/31 passed
Full BeGo Release, fresh PostgreSQL 17 + published Runner: 98/98, 0 skip
Required RideBound solution: 557/557 passed
Date: 2026-08-05
```

### RB-WP5-007 authenticated idempotent HTTP boundary

```text
Authorization: authenticated fallback; host create/finalize; current member read/event
Input: strict unknown-field rejection; <=32 KiB writes; no client sequence/raw frame/
  route/ledger/certificate; member v1 event allowlist is exact timerTick only
Idempotency: HMAC actor scope + resource/scope/key + canonical HTTP semantics;
  server-owned epoch/eventSeq excluded from fingerprint and allocated in T1
Create race: composite advisory transaction lock before lookup/insert; run/event/
  operation/subject-links/provenance commit atomically
HTTP replay: pending returns stable 202 operation; completed returns exact cached bytes;
  changed semantic payload is RFC Problem Details 422
Rate limit: explicit 30 writes/min; action policy precedence exercised through TestServer
Security remediation: Microsoft.OpenApi 2.7.5; direct/transitive vulnerability audit clean
Targeted Application/controller/TestServer/PostgreSQL/Runner: 28/28 + 19/19 passed
Full BeGo Release /warnaserror, fresh PostgreSQL 17 + published Runner: 116/116
BeGo frontend: 7/7 passed; required RideBound: 557/557 passed
Date: 2026-08-05
```

### RB-WP5-008 decision transaction, ACK/checkpoint và crash recovery

```text
Migration: 20260809032051_AddCommitRecoveryFencing
T2: exact decision + certificate + user-safe projection + timeline + outbox atomic
T3: matching decisionApplied + independently validated checkpoint under live fence
Fencing: database UTC + lease owner + revision; stale T2/T3 rejected after takeover
Recovery: fresh pinned Runner + exact hello/init/checkpoint/event reconstruction;
  pending decision must match persisted canonical bytes/hash before ACK
Failure injection: before/after Runner, T2, ACK and T3 all exercised against a
  clean-replay oracle on real PostgreSQL 17 + published RideBound.Runner
Mutation gates: wrong stored decision hash never ACK; replay mismatch Diverged;
  missing/invalid certificate never publishes; nested promise order must preserve
  unique stops, exact request/stop binding and pickup-before-drop semantics
Full BeGo Debug: 125/125 passed, 0 skipped
Full BeGo Release /warnaserror: 125/125 passed, 0 skipped
BeGo frontend: 7/7 passed
Required RideBound command: 557/557 passed, exit code 0
Targeted format verify: passed; vulnerability audit: no vulnerable packages
Published Runner SHA-256:
  EC5F224C058D69F6121E127A39F447F421C36E94094E6106517294CE222AD9BC
Research mapping: RIFL unique request/completion records support durable idempotent
  retry; Gray-Cheriton leases support bounded liveness, while correctness remains
  in revision/owner fencing and exact replay. No exactly-once delivery claim.
Date: 2026-08-09
```

### RB-WP5-009 transactional outbox và SignalR relay

```text
Claim: exact unpublished head per run; DISTINCT ON + ordered FOR UPDATE SKIP LOCKED
Lease/fence: PostgreSQL transaction time + owner + incremented attempt_count
I/O boundary: claim transaction commits before SignalR; mark only after send;
  failed send reschedules exponential bounded backoff without holding row lock
Wire: schema v1, stable messageId/runId/aggregateSequence/payloadHash + exact
  nested canonical user-safe payload; retry attempt/owner never leaks into wire
Privacy/auth: exact event/data allowlist; no route/node/budget/certificate witness/
  raw identity; canonical Session GUID group; authenticated member join required
Failure evidence: crash after send -> same ID/payload/hash; expired claim reclaims;
  stale attempt cannot mark; run B progresses while run A leased; run-local order;
  failed send retry; no-audience row not published; T2 rollback leaves no outbox
Late-side-effect audit: lease cannot stop an already-started send. Stable sequence
  plus frontend per-run monotonic/recent-ID gate discards stale duplicate callback;
  disconnected-client catch-up remains RB-WP5-010, not an exactly-once claim
Full BeGo Debug, fresh PostgreSQL + published Runner: 131/131, 0 skipped
Full BeGo Release /warnaserror, separate fresh DB + Runner: 131/131, 0 skipped
Frontend: 9/9; lint, tsc --noEmit and Next 16.3.0 production build passed
Security: Microsoft.OpenApi 2.7.5 retained; Next 16.3.0, NextAuth beta.32 and
  transitive patches; NuGet and npm audits report 0 vulnerable packages
Required RideBound command: 557/557 passed, exit code 0
Targeted format, TypeScript, Markdown/link/fence/diff gates: passed
Date: 2026-08-09
```

### RB-WP5-010 rebuildable audit timeline, privacy và observability

```text
Query contract: strict canonical (sequence,id), UTF-8/limit bounds, deterministic
  order; production PostgreSQL row-value keyset, not OR/prefix pagination
Authorization: server-owned member scope maps own pickup requests through restricted
  raw-subject links; cross-request denied; operator raw policy default deny
Evidence boundary: member timeline is exact canonical user-safe payload only;
  operator endpoint separately returns exact decision/certificate bytes + hashes
Rebuild: repeatable-read snapshot over append-only decision/certificate/operation;
  contiguous epoch, previous hash, input/output state and materializer/certificate
  bindings checked before canonical rebuilt/live projection+timeline hash comparison
Drift/export: mismatch is explicit and blocks pseudonymous export; recursive guard
  rejects subject/token/coordinates/route/witness/budget/manifest/raw fields
Plan evidence: composite (run,sequence,id) and (run,request,sequence,id) indexes;
  PostgreSQL EXPLAIN on 12,000 representative rows uses index + row tuple condition
Migration evidence: up/down/re-up pass; guarded Down refuses before any drop when
  commit data exists, so failed rollback cannot leave a partial downgrade
Privacy/log evidence: commit exceptions do not log/echo raw details; mutation covers
  secret, subject and coordinate; telemetry carries only stable safe metadata
Full BeGo Debug, fresh PostgreSQL + published Runner: 138/138, 0 skipped
Full BeGo Release /warnaserror, separate fresh DB + Runner: 138/138, 0 skipped
Frontend: 9/9; lint, tsc --noEmit and Next production build passed
Security: NuGet and npm audits report 0 vulnerable packages
Required RideBound command: 557/557 passed, exit code 0; WAC did not recur
Published Runner SHA-256:
  EC5F224C058D69F6121E127A39F447F421C36E94094E6106517294CE222AD9BC
Claim: correctness/rebuildability/privacy only; no exactly-once, production SLA,
  throughput or ridepooling-effectiveness claim
Date: 2026-08-09
```

### RB-WP5-011 default-off rollout, compatibility và rollback

```text
Default: RideBound:Commit:Rollout:Mode = Disabled; no COMMIT hosted service
Activation: Shadow = decision worker only; Live = decision + live-only outbox relay
Preflight: exact pinned Runner artifact SHA-256; no process start; cached bounded
  refresh; unhealthy -> /api/health/commit 503 and no claim/member service resolution
Durable namespace: commit_runs.rollout_namespace IN (Shadow,Live), immutable trigger;
  active uniqueness is (session,policy,namespace); existing rows backfilled Shadow
Claim boundary: decision runner/ACK SQL joins exact namespace; outbox SQL can only
  claim Live rows. Shadow outbox remains unpublished/attempt_count=0 across mode switch
Shutdown/restart: cancellation stops new cycles; inflight lease remains durable and
  same-namespace worker reclaims after DB-time expiry using existing revision fence
Compatibility: old /api/health exact shape and Session latest/final route snapshots
  unchanged; operator audit evidence remains available; no append log is deleted
Migration: up/down/re-up real PostgreSQL; Down guard precedes drop and preserves
  rollout column/index/trigger when data exists
Logic fix: renamed active-run unique constraint is mapped back to typed RunUnavailable
Full BeGo Debug fresh PostgreSQL + published Runner: 147/147, 0 skipped
Full BeGo Release /warnaserror separate fresh DB + Runner: 147/147, 0 skipped
Frontend: 9/9; lint, tsc --noEmit and Next production build passed
Security: NuGet and npm audits report 0 vulnerable packages
Required RideBound command: 557/557 passed; WAC did not recur
Repository format: WP5-targeted clean; three pre-existing non-WP5 whitespace files
  remain recorded for RB-WP5-014, so no false full-format claim
Claim: rollout/recovery/compatibility correctness only; no effectiveness/SLA claim
Date: 2026-08-09
```

### RB-WP5-012 paired B1/C1 Layer-1 replay artifact

```text
Fixture: BeGo-domain-shaped pseudonymous source fixture; explicit provenance says
  no production/account/raw-coordinate data; raw + canonical file hashes pinned
Pairing: B1 rolling-cost vs C1 ridebound-hard-vector; same Runner DLL/core commit,
  workload/seed/graph/travel, candidate caps, OR-Tools adapter and deterministic
  generation/validation/solver work limits; only config /policyId allowlisted
Effective config: domain-separated hash binds common commitment policy + exact arm
  config; initialize may differ only policyId/effective config hash
TOCTOU: exact validated config bytes staged once; hash checked before/after every
  process; Runner independently rejects an inconsistent effective config
Execution: B1 x2 + C1 x2 clean processes; each 2 decisions, 2 produced certificates,
  2 checkpoints, exit 0 and empty stderr
Validation: BeGo exact decision/certificate materializer + independent checkpoint
  content/state/hash validator; normalized protocol input identical across arms
Repeat hashes: B1 output 88ffde16... x2; C1 output 13b32d81... x2
Artifact: every payload file enumerated with byte count/SHA-256; manifest sidecar,
  unmanifested-file rejection and transcript tamper test; exact harness source and
  executing BeGo assembly hashes included
Final bundle: E:\Code\BeGo\artifacts\ridebound\layer1-paired-v1\wp5-012-20260809-final
Artifact manifest SHA-256:
  b843bd20cbe9bf887d00998d4eaad54258848eb41d87ae49fd18a2142a0cb807
Full BeGo Debug fresh PostgreSQL + published Runner: 152/152, 0 skipped
Full BeGo Release /warnaserror separate fresh DB + Runner: 152/152, 0 skipped
Security/quality: targeted changed-code format clean; NuGet vulnerability audit 0;
  git diff --check clean (line-ending notices only)
Required RideBound command: 557/557 passed; WAC did not recur
Claim: Layer-1 mechanical/correctness/reproducibility evidence only; no
  effectiveness, non-inferiority, production SLA or novelty claim
Date: 2026-08-09
```

### RB-WP5-013 independent failure/concurrency/mutation/performance evidence

```text
Method sources: LDFI, Elle, QuickCheck, DeMillo-Lipton-Sayward mutation testing,
  Georges-Buytaert-Eeckhout performance evaluation; mechanisms applied with explicit
  limits, no claim that the external tools/formal analyses themselves were run
Transition oracle: test-owned table, no production transition call; 256 histories x
  64 steps = 16,384; accepted 12,261, rejected 4,123; exact seed/step trace retained
Contention: real PostgreSQL exact expected/observed operation sets; 2/3/4 workers,
  queue depths 24/36/48; every worker claimed a bounded share; lost/duplicate = 0
Decision faults: separate OS process Environment.FailFast at all 8 worker failpoints;
  nonzero exit + exact marker + fresh Runner reconstruction + decision/certificate/
  checkpoint equality + stale T2/T3 fence rejection
Outbox faults: separate OS process Environment.FailFast at all 4 relay failpoints;
  BeforePublish invokes once; AfterPublish/BeforeMark retries the same stable delivery;
  AfterMark remains once; exactly one committed outbox row becomes published
Resource cleanup: no newly orphaned dotnet process; Runner active session count 0;
  PostgreSQL connections return 1 -> 1
Required mutants: 5/5 killed — active-run unique index, ACK/checkpoint gate,
  T2/outbox atomicity, semantic idempotency fingerprint, canonical message hash;
  explicit mutants only, no external mutation score/percentage
Local curves: queue 8/32/64 x workers 1/2/4; deterministic randomized order, one
  warm-up + five measured repetitions, raw intake/claim/drain/ops samples and machine/
  PostgreSQL/append-row provenance retained; no latency threshold assertion
Observed median claim-drain ms (w1/w2/w4): q8 5.553/7.130/7.246;
  q32 8.848/8.822/7.867; q64 10.920/10.957/8.938
Artifact: E:\Code\BeGo\artifacts\ridebound\wp5-independent-v1\wp5-013-20260809-final
Manifest SHA-256:
  e21fb0877fbc6d61bf6f1e24adcda24e09a29fea95a9f44d1b61bf4fc1061ca2
Independent rehash: sidecar exact; 18/18 manifest files present/size/hash exact;
  missing/extra/reparse/tamper rejected
Full BeGo Debug, fresh PostgreSQL + published Runner: 153/153, 0 skipped
Full BeGo Release /p:TreatWarningsAsErrors=true, separate fresh DB: 153/153, 0 skipped
Security/quality: changed-code format clean; all BeGo/RideBound NuGet vulnerability
  audits 0; git diff --check clean apart from pre-existing line-ending notices
Required RideBound command: 557/557 passed; WAC 0x800711C7 did not recur
Claim: independent bounded systems-correctness evidence only; no formal LDFI/Elle,
  exhaustive state space, mutation percentage, end-to-end throughput, SLA,
  ridepooling effectiveness or non-inferiority claim
Date: 2026-08-09
```

### RB-WP5-014 closure/source audit

```text
Source findings fixed:
  1. commit_subject_links UPDATE/DELETE rejected by append-only DB trigger
  2. commit_outbox.operation_id non-null; absolute head chosen before Applied gate
  3. claimed per-run heads publish concurrently in independent DI scopes/DbContexts
Real PostgreSQL targeted migration/publication gate: 1/1 passed, 0 skipped
Targeted rollout/outbox/model gate: 20/20 passed
Full BeGo Debug, fresh PostgreSQL + published Runner: 154/154, 0 skipped
Full BeGo Release /p:TreatWarningsAsErrors=true, separate fresh DB: 154/154, 0 skipped
Frontend: npm test 9/9; ESLint, TypeScript --noEmit and Next production build passed
Quality/security: full dotnet format verify passed; NuGet/npm vulnerability audits 0
Required RideBound command: 557/557 passed; WAC 0x800711C7 did not recur
Verdict: GO for RB-WP6-001 refinement only; NO-GO for main experiment,
  production SLA, effectiveness/non-inferiority or user-satisfaction claims
Date: 2026-08-09
```

### RB-WP6-006 external Runner/resource supervisor closure

```text
Contract correction: ADR-028; umbrella 1.0.2; FailureRecord 1.0.1;
  wp6-failure-v1.0.1; five missing cancellation/process/stream codes typed
Actual Runner gate: published WP3 commitment fixture; hello + initialize + 4 event
  batches + 4 decisions + dynamic ACK + checkpoint + shutdown; exit 0, stderr empty
Pinned pre/postflight: dotnet host, 189 .NET 10.0.9 runtime files, 12 non-PDB Runner
  deployment files, exact policy config and exact scenario source
Independent harness checks: capability selection, manifest hash, decision payload/hash,
  ACK context and checkpoint manifest/epoch/previous-decision chain
Adversarial supervisor cases: 15/15 passed; wall/CPU/memory/process count, stdin/
  stdout/stderr, crash, caller cancel, child tree, mutation, incomplete output,
  existing root and unpinned executable all fail closed with retained evidence
Architecture/source boundary: Benchmarking references only Benchmarking.Contracts and
  Contracts; no Runner/Domain/Application/core result path
Targeted: Benchmarking.Contracts 37/37; Benchmarking 52/52
Quality: dotnet format RideBound.slnx --verify-no-changes --no-restore passed
Required command: dotnet test RideBound.slnx passed 647/647; WAC 0x800711C7 did not recur
Claim: local bounded controls/mechanical repeatability only; no SLA/effectiveness claim
Date: 2026-08-09
```

### RB-WP6-013 adversarial determinism/failure/resource closure

```text
Contract correction: ADR-035; umbrella 1.0.6; no public JSON field/failure code removed
Plan: B1/C1 × (1 isolated warm-up + 3 isolated measured) = 8 terminal runs
Canonical/property gate: 10 document types, nested reversal, 16 parallel decode each;
  plan permutation + 32 parallel compile; both HMAC arm-order strata observed
Taxonomy gate: 21 canonical failure/stage cases, 8 pre-outcome exclusions,
  21 deterministic terminal raw-evidence mappings
Actual failure gate: start/crash/cancel, wall/CPU/memory/process/stdin/stdout/stderr,
  postflight/incomplete/unsupported conversation; partial evidence retained
Source boundary: no mutable RNG/runtime hash; exact staging/provenance allowlist;
  metric call sites remain producer + verifier recomputation only
Fresh medium D/E: 8/8 success each; 13 top-level semantic fields and 8 per-run
  semantic records exact; all 8 sampled-resource metric hashes legitimately differ
Bundle D/E: cb6597d89a844099d5af4849f895d0c5f7af3d7351be5345cfcbf558180324a0 /
  27c7f69e5df77b4f1136c75252e8abb9371231e76e5df55c2a5b42c427d2514e; externally valid
Negative local strata retained: C1 wall/CPU > B1 in 6/6 measured pairs; diagnostic only
Required-mutant result: 100% of declared matrix; not a general mutation score
Quality: Release 0 warning/error; format pass; no vulnerable NuGet package;
  schema 4/4; Markdown 91 files/180 internal links/0 broken/0 unbalanced fences
Required command final: dotnet test RideBound.slnx passed 770/770, 0 fail/skip
Preceding run: 769/770 from medium CPU control; standalone pass then exact rerun pass;
  Contracts/Runner loaded and WAC 0x800711C7 did not recur
Evidence: docs/benchmarking/wp6-013-adversarial-closure-evidence-2026-08-12.md
Date: 2026-08-12
```

### RB-WP6-014 source/claim closure

```text
Source verdict: no unresolved correctness/contract issue; no paper-only heuristic added
Fresh tiny A: 8/8 success; bundle 79cb321a...b04; external verify valid
Fresh medium H/I on final exact source: 8/8 success each;
  16/16 top-level semantic exact; 72/72 per-run semantic exact;
  8/8 full resource rows different as expected
Bundle H/I: 89a43921...d9d8 / a954db62...94e9; both externally valid
Required exact command: dotnet test RideBound.slnx passed 770/770, exit 0
WAC: Contracts/Runner loaded; 0x800711C7 did not recur
Claim: WP6 mechanical correctness/reproducibility only; no effectiveness/SLA
BeGo read-only baseline: backend 149 pass + 5 explicit opt-in skip (154 discovered),
  frontend 9/9; no false 154/154 integration claim for this closure run
Evidence: docs/benchmarking/wp6-014-source-claim-closure-evidence-2026-08-13.md
Review: docs/reviews/wp1-wp6-final/README.md
Date: 2026-08-13
```

### RB-WP10-001..010 RidePy Layer 3 closure

```text
Exact source/environment verifier: PASS; 8/8 tests
RidePy pinned-container unit/integration/analyzer suite: 23/23
Generic RunnerClient targeted regression: 14/14
Canonical actual B1/C1: 5/5 completed each; 5 pickup + 5 drop each
Canonical independent verifier: PASS; 5/5 mutation classes caught
Frozen representative subset: 24 planned arm jobs; 22 PASS; 1 FAIL CLOSED; 1 NOT RUN
Subset independent verifier: PASS over 11 valid pairs + retained failure transcript
WP10 full .NET/FleetPy/format/Release/static gates: PASS in final cross-WP review
Date: 2026-08-23
```

## 5. Next action

WP1–WP10 Complete. WP9 H6 vẫn âm ở cả hai điểm năng lực: service gate FAIL tại 8
xe (`−7.1296 pp`) và 4 xe (`−4.9074 pp`). WP10 canonical pass nhưng representative
subset fail closed bằng `RBWP10_NODEONLY_CONCURRENT_MIDEDGE_UNSUPPORTED`; Layer 3
claim chưa được thiết lập. Không được thay margin, pool panel, bỏ failed job hoặc dùng
WP10 để cứu primary.

Final review/optimization goal đã hoàn tất bằng ADR-052. Evidence hiện hành gồm
[WP9 result](benchmarking/wp9-confirmatory-result-2026-08-23.md),
[WP10 negative capability report](benchmarking/wp10-ridepy-layer3-negative-capability-result-2026-08-23.md),
[optimization benchmark](benchmarking/post-wp10-exact-reuse-optimization-2026-08-23.md),
[WP1–WP10 review](reviews/wp1-wp10-final/README.md) và
[rendered PDF report](../output/pdf/RideBound-WP1-WP10-final-review-2026-08-23.pdf).

Không tự động mở WP11/WP12. Next action cần user chọn Product UX hay manuscript/
release rồi tạo refinement/ADR mới. Mọi hướng sau phải giữ H6/WP10 negative outcome,
không đổi margin/panel/failed-job treatment hoặc dùng intermediate policy để rescue
confirmatory result hậu outcome.

## 6. Open decisions

| ID | Câu hỏi | Khi nào khóa |
|---|---|---|
| — | Không còn open decision từ O-001..O-008; hướng optimization hậu WP10 phải có ADR/evidence mới và giữ exploratory boundary | Khi chọn optimization |

O-001 đã được khóa bởi ADR-018: B1 WP2 không cho incumbent accepted request đổi
vehicle; WP4 chỉ mở lại bằng ADR superseding và atomic multi-vehicle evidence.
O-007 được khóa bởi ADR-025: WP5 dùng versioned long-lived NDJSON child process;
HTTP/gRPC chỉ mở lại khi có cross-host operational requirement và ADR mới.
O-006 được khóa bởi ADR-037 và executable probe trên exact FleetPy 1.0.2: position
`(start,end,relative)` có direction/range ổn định và `SimulationVehicle._move` cập nhật
`veh_obj.pos`; drift phải fail closed, không suy diễn fraction từ clock.
O-002/O-003/O-004/O-008 được khóa trong WP8/ADR-040..044. O-005 được khóa bởi
ADR-050/051: RidePy là framework đã đánh giá cho WP10 và cho kết quả năng lực âm;
AMoD2 chỉ là hướng tương lai riêng.

## 7. Decision log

### ADR-001 — 2026-07-27 — Accepted

**Context:** BeGo planner hiện phụ thuộc domain/session và là snapshot.

**Decision:** Xây RideBound thành các project độc lập với portable core; BeGo dùng adapter.

**Consequence:** Tốn contract/mapping ban đầu nhưng tránh khóa core vào product.

### ADR-002 — 2026-07-27 — Accepted

**Decision:** Novelty chỉ đặt ở per-rider, multi-dimensional, cumulative/switch budget qua nhiều epoch kèm certificate.

**Rejected:** claim dynamic insertion/ETA threshold/least-commitment nói chung.

### ADR-003 — 2026-07-27 — Accepted

**Decision:** Layer 1 BeGo và Layer 2 FleetPy là bằng chứng chính; Layer 3 cross-system là bổ sung.

### ADR-004 — 2026-07-27 — Accepted

**Decision:** FleetPy pin `1.0.2` commit `053aa9d4fcfde91c5d303435d5748f9206c071b0`.

### ADR-005 — 2026-07-27 — Accepted

**Decision:** RidePy v2.10.1 là Layer 3 mặc định; AMoD2 là alternate. OpenRidepoolSimulator không nằm critical path.

### ADR-006 — 2026-07-27 — Accepted

**Decision:** B0 BeGo hiện tại chỉ là context; B1 rolling online cùng core là baseline chính.

### ADR-007 — 2026-07-27 — Accepted

**Decision:** Budget synthetic là service-policy stress test, không là user preference truth.

### ADR-008 — 2026-07-27 — Accepted

**Decision:** NDJSON long-lived runner là canonical cross-language interface; in-process BeGo phải pass cùng contract.

### ADR-009 — 2026-07-27 — Accepted

**Decision:** Primary algorithm metric dự kiến tách decision-induced revision khỏi traffic-induced revision.

**Note:** exact primary endpoint chưa preregistered.

### ADR-010 — 2026-07-28 — Accepted

**Context:** Người dùng yêu cầu BeGo và hệ thống nghiên cứu có hai GitHub
repository, lịch sử và vòng đời phát hành riêng.

**Decision:** Đổi tên dự án thành RideBound và đặt tại repository độc lập
`https://github.com/tsunflowerr/RideBound`. Không copy `BeGo/src`. BeGo B0 nằm
ở repository cũ; B1 và C1 cùng nằm trong RideBound để so sánh công bằng.

**Consequence:** Tích hợp BeGo phải qua protocol/scenario/adapter rõ ràng.
Regression BeGo được chạy từ repository ngoài khi task chạm integration.

### ADR-011 — 2026-07-28 — Accepted

**Context:** Cấu trúc `Core` cũ chưa biểu diễn rõ Clean Architecture/DDD.

**Decision:** Dùng các layer `Domain`, `Application`, `Contracts`,
`Algorithms`, `Solvers.OrTools`, `Infrastructure` và `Runner`. Domain không
tham chiếu project/package; Application chỉ tham chiếu Domain. Architecture
tests kiểm project graph và từ khóa framework bị cấm.

**Consequence:** Chỉ thêm adapter/persistence project khi có behavior thật;
không scaffold hàng loạt assembly rỗng.

### ADR-012 — 2026-07-28 — Accepted

**Context:** Architecture test đọc dấu `\` trong `ProjectReference` bằng API path
phụ thuộc hệ điều hành, tạo false positive trên Linux CI. CI WP0 cũng mới chỉ có
restore/build/test nên chưa hiện thực đầy đủ PR fast gates trong tài liệu `15`.

**Decision:** Chuẩn hóa separator trước khi lấy project name và khóa lỗi bằng hai
regression cases Windows/Linux. Tách CI thành format, Release build/test/coverage,
NuGet/dependency review và package runner sau main. Sonar và PR-Agent được khai báo
nhưng chỉ chạy khi repository secrets/variables tương ứng đã tồn tại.

**Consequence:** Architecture rule giữ nguyên; chỉ cách đọc `.csproj` trở nên
cross-platform. AI review không phải required correctness gate. Sonar chỉ được đặt
required sau khi cấu hình và bootstrap scan thành công.

### ADR-013 — 2026-07-28 — Accepted

**Context:** Roadmap đã có 12 work package nhưng chưa có đơn vị delivery đủ nhỏ.
Ticket hóa chi tiết tất cả package ngay khi WP1 chưa khóa contract sẽ tạo dependency
và acceptance criteria dựa trên giả định chưa được kiểm chứng.

**Decision:** Dùng progressive elaboration: giữ WP1–WP12 ở mức topic/outcome/gate,
chỉ chia topic hiện hành thành ticket tuần tự. WP1 có 15 ticket trong `24`; mặc
định WIP là một implementation ticket. Topic kế tiếp chỉ được refinement khi topic
trước đạt exit gate.

**Alternatives considered:** Ticket hóa toàn bộ WP1–WP12 ngay; triển khai trực tiếp
từ deliverable cấp WP.

**Consequence:** Bước tiếp theo luôn nhỏ và kiểm tra được, nhưng backlog xa phải
được refinement lại bằng evidence thực trước khi triển khai.

**Evidence:** `23-delivery-backlog-and-ticket-policy.md`,
`24-wp1-contracts-ticket-plan.md`.

### ADR-014 — 2026-07-29 — Accepted

**Context:** Protocol draft còn cho phép nhiều cách hiểu về `schemaVersion`,
distance unit, node/edge position, batch sequence, field ownership, error
severity và hash concatenation. FleetPy có network position/plan locks trong khi
RidePy có thể chỉ cung cấp node-level state; một contract ngầm chọn một bên sẽ
làm cross-system replay không còn so sánh được.

**Decision:** Protocol v1 bắt đầu ở exact version `1.0.0`; dùng JSON integer-only
trong common safe range, millisecond, millimeter, WGS84 E7 và micro-cost có
`costUnitId`. Position là tagged union `node`/`edgeProgress` với capability
`nodeOnly`/`directedEdgeProgress`. Event sequence liên tiếp trên toàn run, epoch
chỉ tiến sau `decisionApplied`; gap/overlap làm session failed. Envelope chỉ giữ
message routing/identity, payload giữ nội dung message và initialize manifest giữ
config bất biến. Error dùng stable code cùng disposition
`rejectMessage`/`failSession`/`terminateProcess`. Canonical JSON là RFC 8785
subset integer-only; SHA-256 dùng domain prefix và tagged length frames. Fixture
phân loại `schema-only`, `runner-executable` hoặc `future-behavior`.

Chi tiết normative, exact range, field table, framing bytes và checklist nằm
trong `06-event-contract-and-determinism.md`, mục 2–14.

**Alternatives considered:** giữ distance là “mm hoặc meter”; chỉ hỗ trợ node;
ép mọi adapter phát edge progress; nối raw JSON/string khi hash; coi mọi fixture
là executable.

**Consequences:** Contract tests có thể viết exact bytes/error/order mà không tự
chọn semantics. Adapter thiếu edge progress phải công bố `nodeOnly` và
fail/downgrade có tên; không âm thầm bịa position. Mọi thay đổi unit, position
meaning, ordering hoặc hash input sau ADR này là breaking change, cần schema
major, ADR superseding và fixture migration.

**Evidence:** `05-portable-core-architecture.md`,
`06-event-contract-and-determinism.md`, `12-fleetpy-adapter.md`,
`13-cross-system-adapters.md`, `tasks/24-wp1-contracts-ticket-plan.md`.

**Supersedes / superseded by:** Không supersede ADR trước. Đóng phần contract của
O-006; chỉ giữ executable FleetPy capability preflight cho WP7.

### ADR-015 — 2026-07-29 — Accepted

**Context:** ADR-014 khóa semantics chung nhưng RB-WP1-005–007 vẫn cần exact
receiver behavior cho patch/minor/major, capability wire vocabulary và field
identity bất biến của initialize manifest. Nếu receiver tự bỏ field minor,
capability tự mặc định hoặc manifest lặp identity không nhất quán, cross-system
replay có thể dùng input khác nhau mà vẫn tưởng là cùng run.

**Decision:** Version phát hành hiện tại vẫn là exact `1.0.0`; receiver v1 nhận
cùng patch line `1.0.x`, từ chối higher minor nếu chưa có explicit safe-forward
profile và fail session với major khác trước unknown-field check. Safe-forward
profile phải machine-readable và current list rỗng. Capability v1 dùng
single-valued `positionModel`, semantic set có vocabulary cố định và explicit
fleet/request scale. Required capability thiếu/không biết phải fail; downgrade
chỉ hợp lệ khi có `downgradePolicyId`. `initializeRun` giữ run/scenario ID ở
envelope, manifest chỉ giữ content/config hashes, seed, policy, unit conversion,
exact negotiated selection, adapter/simulator, core commit và binary identity.
Pure validation không đọc Git/environment, không mutate active identity và cấm
re-initialize.

**Consequences:** Adapter có machine-readable schema/inventory để kiểm trước khi
gọi runner; patch bug fix không buộc migration semantics. Không có future minor
nào được nhận ngầm. Manifest không lặp ID nên mismatch scenario được kiểm với
envelope/session context, còn nội dung scenario được kiểm bằng hash. Hash values
trong WP1-007 là contract fields do caller cung cấp; calculation/vector vẫn thuộc
RB-WP1-010.

**Evidence:** `benchmarks/schemas/v1`, `ProtocolVersionCompatibility`,
`HelloMessages`, `InitializeRunMessages`, `CapabilityNegotiator`,
`InitializeRunValidator`, Contracts/Runner boundary tests và
`06-event-contract-and-determinism.md` mục 2.1, 4.2, 9.

**Supersedes / superseded by:** Bổ sung cách hiện thực ADR-014; không thay unit,
ordering, lifecycle hay hash framing đã khóa.

### ADR-016 — 2026-07-29 — Accepted

**Context:** RB-WP1-008–015 cần làm event/order/hash/NDJSON/session chạy thật
nhưng WP1 chưa có Domain reducer, solver hay commitment validator. Nếu runner
phát action hoặc certificate rỗng, fixture có thể bị hiểu nhầm là online
behavior đã tồn tại. State identity hash và dedup cache cũng cần exact,
bounded semantics để replay không phụ thuộc runtime.

**Decision:** Event payload v1 giữ exact input order và structural validation
chỉ kiểm schema/sequence/epoch/simulation time. Runner WP1 trả message
`decision` với `status = notProduced`, reason `WP1_STRUCTURAL_ONLY`, không action,
certificate `notProduced` và solver `notRun`; đây là acknowledgement có hash,
không phải routing decision. State identity structural dùng domain
`RideBound.StateIdentityHash.v1\0` và tagged frame
`canonicalStateIdentity`. Manifest/decision hash giữ nguyên ADR-014. Session chỉ
commit epoch/next sequence/previous decision hash sau exact `decisionApplied`.
Dedup giữ đúng một canonical batch/response gần nhất: exact retry trả byte-equivalent
response, cùng key khác payload hoặc partial overlap làm session failed.
Stdout chỉ có canonical NDJSON; diagnostic code đi stderr.

Q1 tính 9 required scenario là `future-behavior`; chỉ duplicate fixture và full
tiny transcript là runner-executable. Mốc đóng Q1 dùng full Release suite 157/157.
Sau khi thêm một assertion đồng bộ vocabulary, Contracts pass 115/115 và inventory
thành 158. Enterprise Code Integrity trên máy này có thể chặn unsigned/fresh DLL
theo lần build với `0x800711C7`; event log 3033/3077 ghi policy
`0283ac0f-fff1-49ae-ada1-8a933130cad6`. Lần full-suite cuối chặn fresh Runner.dll
cả ở Release. Đây là environment/configuration exception được ghi rõ, không phải
bỏ test, sửa policy hay assertion failure.

**Alternatives considered:** phát decision/action giả; dùng object certificate
rỗng; advance state ngay khi nhận event; cache mọi batch không giới hạn; nhận
partial retry; ghi banner/diagnostic vào stdout; bỏ Domain smoke khỏi test count.

**Consequences:** WP2 có executable protocol oracle nhưng vẫn phải cài behavior
thật qua reducer/validator. Replay phát hiện sửa/thứ tự/hash; retry không làm
advance state và memory dedup bounded. Q1 không chứng minh portable cross-system,
online insertion, hard budget hoặc certificate soundness. Default Debug test
phải được chạy lại trên CI/máy không có policy này, nhưng không chặn evidence
Release đã pass đầy đủ.

**Evidence:** `EventBatchMessages`, `DecisionMessages`, `ErrorMessage`,
`ProtocolHash`, `NdjsonReader`, `NdjsonWriter`, `RunnerSession`, `RunnerHost`,
`benchmarks/schemas/v1`, `benchmarks/schemas/fixtures/golden/required`,
`benchmarks/schemas/fixtures/runner`, 115 Contracts tests, 35 Runner tests,
7 Architecture tests và 1 Domain test; exact per-run evidence ở Q1 closure block.

**Supersedes / superseded by:** Bổ sung executable semantics cho ADR-014/015;
không đổi version, unit, event order, manifest/decision hash framing hoặc claim
boundary.

### ADR-017 — 2026-07-29 — Accepted

**Context:** Revalidation end-to-end WP1 phát hiện cache idempotency dùng key
run/scenario/epoch/sequence range nhưng chỉ hash `payload.events`. Một retry giữ
nguyên event payload và sequence nhưng đổi envelope `simTimeMs` được trả lại
decision cũ có time/hash của batch ban đầu. Điều này trái nghĩa “exact retry”,
có thể che transcript corruption và làm canonical input thực tế khác response
được cache.

**Decision:** Identity của retry nguyên batch gồm key run/scenario/epoch/sequence
range và SHA-256 của toàn canonical `eventBatch` envelope + payload. Mọi thay đổi
canonical context, gồm `schemaVersion`, `runId`, `scenarioId`, `epochId`,
`simTimeMs`, hoặc payload dưới cùng batch key là
`DUPLICATE_PAYLOAD_CONFLICT`/`failSession`. Exact retry tiếp tục trả
byte-equivalent cached response và không advance state/hash.

**Alternatives considered:** thêm riêng `simTimeMs` vào cache key rồi phân loại
thành overlap; chỉ so raw JSON bytes; giữ payload-only hash và tin client không
đổi context.

**Consequences:** Property order/whitespace khác nhưng canonical batch tương
đương vẫn idempotent; thay đổi semantic context không còn bị nhận nhầm. Đây là
bug fix patch-compatible cho behavior invalid, không đổi schema/unit/order/hash
framing của decision. Regression test changed-time duplicate và clean-process
replay được giữ trong Runner suite.

**Evidence:** `RunnerSession.CalculateCanonicalBatchHash`,
`Same_duplicate_key_with_changed_simulation_time_fails_session`,
`Published_transcript_replays_twice_through_clean_processes` và
`Canonically_equal_duplicate_ignores_json_formatting`; Runner Release 38/38 và
full solution Release 161/161. Debug fresh Runner test assembly bị host
policy chặn trước discovery; 123 non-Runner test pass.

**Supersedes / superseded by:** Làm rõ “exact retry” trong ADR-016; không
supersede lifecycle/hash-chain decision khác.

### ADR-018 — 2026-07-29 — Accepted

**Context:** Sau Q1, WP2 cần state/reducer/B1 thật nhưng chưa có quyết định chính
xác về ownership, bootstrap vehicle/travel state, atomic event reduction,
frozen-prefix semantics hoặc O-001 reassignment. Đưa Contracts DTO vào Domain,
commit từng event giữa batch hoặc mở reassignment incumbent ngay sẽ phá Clean
Architecture và làm exact-small baseline khó kiểm chứng.

**Decision:** Domain sở hữu run/request/vehicle/route state và invariant thuần;
Application sở hữu internal ordered-event reducer/orchestration; Runner boundary
map Contracts DTO sang internal events. Batch được validate/fold nguyên tử và
domain/plan state chỉ commit tại matching `decisionApplied`. Route dùng exact
executed/locked frozen prefix, mutable suffix và no-op candidate bắt buộc.
Vehicle/travel bootstrap đi qua typed epoch-one events. B1 WP2 cho pending
request chọn vehicle nhưng cấm incumbent accepted request đổi vehicle; mở lại
cần ADR superseding cùng atomic multi-vehicle/exact-small evidence. B1 chỉ dùng
physical constraints, accepted preservation, integer operational cost và stable
tie-break; không có commitment gate.

**Alternatives considered:** đặt reducer trong Contracts; cho Domain nhận
`EventBatchPayload`/`JsonElement`; mutate state từng event; đưa initial mutable
state vào manifest; cho reassignment ngay; kéo OR-Tools/WP3 ledger vào WP2.

**Consequences:** WP2 có queue nhỏ và test-first, giữ Domain/Application độc lập
và cô lập B1 physical baseline. B4/no-reassignment chưa là baseline phân biệt
trong WP2; WP4 phải ghi rõ equivalence hoặc mở reassignment B1 trước khi đánh giá
B4. Q1 structural conformance transcript được giữ bằng path có tên, còn online
B1 không được trả `WP1_STRUCTURAL_ONLY`.

**Evidence:** `04`, `05`, `06`, `08`, `15`,
`tasks/25-wp2-online-state-refinement.md`,
`tasks/26-wp2-online-baseline-ticket-plan.md`.

**Supersedes / superseded by:** Đóng O-001 cho WP2 và bổ sung ADR-011/016; không
đổi protocol v1 hoặc quyết định ledger/certificate của WP3.

### ADR-019 — 2026-07-29 — Accepted

**Context:** `RB-WP2-002..006` cần biến ADR-018 thành types/transition có thể
chạy, nhưng các chi tiết wire route/travel, proposed-state ownership, edge
progress scheduling và witness order chưa có executable semantics. Nếu reducer
nhận raw JSON, snapshot ngoài ghi đè route core hoặc validator tin cost/schedule
do candidate gửi, Q2 correctness không còn độc lập.

**Decision:** Protocol event v1 dispatch exact typed payload theo `eventType`.
Vehicle snapshot mang full canonical position/rider sets và route gồm
`planVersion`, `executedStopCount`, exact `frozenPrefix` cùng `mutableSuffix`;
stop pickup/drop mang request ID, waypoint không mang. Travel snapshot là directed
arc semantic set có version/hash; snapshot đầu phải khớp manifest và bản sau tăng
đúng một. Domain state/route là immutable và transition trả state mới hoặc stable
witness. Runner map toàn wire batch sang internal event trước khi Application
fold; một lỗi bỏ toàn proposed state. Committed state chỉ đổi qua matching
acknowledgement. Physical validator tự suy schedule từ current position,
remaining route và travel lookup; với edge progress nó cộng phần thời gian cạnh
còn lại bằng integer ceiling. Thứ tự witness deterministic bắt planVersion,
frozen prefix, connectivity/schedule, physical service và incumbent preservation.

**Alternatives considered:** giữ `JsonElement` payload; dùng catch-all
`fixtureIntent`; mutate aggregate từng event; cho external vehicle snapshot thay
route core; dùng schedule/cost candidate làm proof; đưa network/solver vào
validator; cho incident behavior chạy sớm.

**Consequences:** Contracts không phụ thuộc Domain; Application chỉ phụ thuộc
Domain; online fixture có thể map/reduce hai epoch mà chưa cần B1. Invalid event
cuối không tạo partial commit. Validator bắt capacity, pickup window,
max-ride-time, precedence, stop location, connectivity, plan/prefix,
onboard/accepted preservation và incumbent reassignment bằng witness máy đọc
được. Incident vẫn typed ở wire nhưng reducer trả unsupported tới WP3. WP2/Q2
chưa hoàn thành cho tới candidate/B1/exact-small/Runner online tickets.

**Evidence:** `OnlineEventModels`, `EventBatchPayloadCodec`,
`RideBoundRun`, `RideRequest`, `VehicleState`, `RoutePlan`,
`TravelTimeSnapshot`, `EventReducer`, `EventReductionCoordinator`,
`OnlineEventMapper`, `PhysicalPlanValidator`; published fixtures trong
`benchmarks/schemas/fixtures/wp2`; Debug/Release 278/278 cùng transition,
mutation và 24-permutation tests.

**Supersedes / superseded by:** Hiện thực hóa ADR-018; không đổi lifecycle/hash
chain của ADR-016/017 và không kéo commitment semantics WP3 vào WP2.

### ADR-020 — 2026-07-30 — Accepted

**Context:** `RB-WP2-007..012` phải biến state/reducer/physical validator của
ADR-018/019 thành baseline B1 chạy online, nhưng vẫn cần giữ candidate
completeness kiểm được, no-op, deterministic selection, independent oracle và
Q1 transcript oracle. Nếu generator tự chứng nhận, exact mode âm thầm truncate,
Runner commit trước ACK hoặc B1 phát certificate commitment, WP2 sẽ vượt claim
và làm mất oracle cho WP3/WP4.

**Decision:** `RideBound.Algorithms` sinh mọi precedence-preserving pickup/drop
insertion trong exact-small bound trên mutable suffix, giữ exact frozen prefix
và luôn xét no-op. Mỗi leaf được `PhysicalPlanValidator` kiểm; exact bound/cap
vượt giới hạn fail rõ, bounded mode cắt theo stable ID và giữ no-op. Fleet
selector exhaustive chọn đúng một candidate/vehicle, không serve request hai
lần, tối đa accepted count, tối thiểu checked integer operational cost rồi
tie-break bằng candidate ID ordinal. Selected candidate được validator độc lập
kiểm lại trước immutable apply; incumbent accepted không đổi vehicle và không
thành rejected. Runner default là online B1; Q1 shell chỉ còn trong named
`--mode conformance`. Full committed online state, event batch và typed actions
đi vào state/decision hash; route/request state chỉ commit sau exact matching
`decisionApplied`. Certificate WP2 luôn `notProduced`.

Exact-small oracle trong test tự enumerate/evaluate, không gọi production
generator/selector/validator. Published differential bound là tối đa 2 vehicle,
2 pending request và 32 deterministic seeds. Tiny demo khóa four-epoch
accept/pickup/board, physical capacity reject, drop/alight transcript cùng exact
final hash. WP2 được đóng chỉ cho physical/B1; P1/P2/P3 commitment, hard budget,
ledger, incident breach, checkpoint và certificate `produced` thuộc WP3.

**Alternatives considered:** chỉ sinh single-request candidate; dùng production
generator làm oracle; dùng weighted scalar/tie ngẫu nhiên; truncate exact mode;
commit ngay khi phát decision; overwrite Q1 golden bằng B1 output; coi
`PhysicalPlanValidator` là commitment certificate; kéo OR-Tools hoặc ledger vào
WP2.

**Consequences:** F-003/F-004 có executable B1 evidence, Q1 vẫn là regression
oracle có tên và WP3 nhận state/hash/ACK boundary thật. Baseline exhaustive chỉ
claim correctness trong published small bound, không claim scale/performance.
Required Debug `dotnet test RideBound.slnx` pass 333/333. Release xUnit vẫn bị
Windows Application Control `0x800711C7` chặn fresh unsigned Application/
Runner DLL trước assertion; policy-safe bundles và process checks pass cho đúng
Release artifacts đó, nhưng không được gọi là Release full-solution xUnit pass.

**Evidence:** `Candidates`, `Policies`, `ExactSmallOracle`,
`OnlineDecisionActionMapper`, `OnlineStateCanonicalizer`, online `RunnerSession`,
typed action schemas/tests, `benchmarks/scenarios/wp2-tiny`,
`scripts/run-wp2-tiny-demo.ps1` và execution plan `26`.

**Supersedes / superseded by:** Hoàn tất ADR-018/019 cho phạm vi WP2; không
supersede protocol/hash framing ADR-014/016/017. WP3 có thể bổ sung commitment
gate/certificate bằng ADR mới nhưng không được đổi physical B1 oracle âm thầm.

### ADR-021 — 2026-07-30 — Accepted

**Context:** WP3 phải thêm promise history, three-way delta, vector budget, phase
locks, incident, certificate và checkpoint lên state/hash/ACK thật của WP2.
Tài liệu `07` trước đó liệt kê `decisionHash` bên trong ledger/certificate, nhưng
nhúng current decision hash vào chính state/body đang được hash sẽ tạo vòng tự
tham chiếu. Candidate evaluator cũng đang sở hữu schedule riêng, dễ làm promise
projection lệch physical schedule. Mức số O-002/O-003 chưa được pilot khóa.

**Decision:** Domain sở hữu stable 10-dimension vocabulary, canonical vector/
explicit policy, promise/version/service tokens, immutable append-only ledger,
budget và phase-lock invariant. Application sở hữu một `RouteScheduleProjector`
dùng chung cho Algorithms và promise flow, `PromiseProjector` cùng
`PromiseDeltaCalculator`; đổi stop node bắt buộc có `IStopDistanceLookup` mm.
Three-way delta tính độc lập `old→exo`, `exo→new`, `old→new`; không giả
`visible=exo+decision`. Initial promise version 1 không tiêu budget; revision tăng
đúng một, round-trip không refund. Ledger nằm trong pending `OnlineState` và chỉ
commit cùng route sau matching ACK.

Ledger dùng stable `publicationId`, không nhúng current `decisionHash` vào state.
Ticket certificate/Runner sau sẽ bind input/state/publication bằng containing
decision envelope hash. `null` hard limit là unbounded, zero là hard zero; không có
default numerical profile. Accepted assignment luôn lock theo O-001; onboard khóa
pickup, freeze/final confirmation chỉ qua explicit policy flags. Incident là breach
record riêng ở ticket `008`.

**Paper/claim evidence:** Full text Multiple-plan dynamic DARP xác nhận dynamic
insertion, plan pool, consensus và least-commitment đã có; Time-consistent DARP
xác nhận consistency/time classes/cost trade-off đã có; forward-looking dispatch
xác nhận rolling/future-aware matching/detour safeguards đã có. RideBound chỉ giữ
claim hẹp per-rider, path-dependent, multi-dimensional cumulative/switch ledger
với machine-checkable certificate.

**Alternatives considered:** schedule commitment riêng trong Algorithms; nén
budget thành weighted scalar; dùng travel time thay stop distance; lưu current
decision hash trong state; chọn default “medium” profile; nối ngay vào Runner khi
incident/validator/schema chưa tồn tại.

**Consequences:** `RB-WP3-001..007` tạo foundation và executable tests nhưng
default B1 Runner vẫn certificate `notProduced`; chưa có C1/P2 guarantee. Queue có
14 ticket, đúng nửa đầu DONE và `RB-WP3-008` là next duy nhất.

**Evidence:** `Domain/Commitments`, `Application/Scheduling`,
`Application/Promises`, refactored `CandidateScheduleEvaluator`, Domain/
Application tests, `tasks/28-wp3-ledger-certificate-ticket-plan.md`; required
Current-tree suite evidence 378/378; host policy exception 0x800711C7 được
ghi riêng, không tính là assertion failure.

**Supersedes / superseded by:** Bổ sung ADR-020 và chỉnh ledger/certificate
self-binding trong `07`; không đổi protocol hash framing ADR-014/016/017, không
mở reassignment O-001 và không chọn O-002/O-003.

### ADR-022 — 2026-08-02 — Accepted

**Context:** `RB-WP3-008..014` phải biến vocabulary/foundation của ADR-021 thành
publication gate thực sự. Audit toàn tuyến phát hiện các đường đi mà test cục bộ
trước đó chưa khóa: candidate có thể mutate state ngoài route, genesis vehicle
có thể preload pending stop, checkpoint có thể chụp pending decision hoặc dựng
state không reachable, certificate có thể không khớp publication actions, breach
có thể không khớp normal ledger và initial state hash chưa bao phủ full state.
Đồng thời B1 chỉ tối ưu exhaustive trong candidate set đã sinh; earliest-feasible,
single-plan, four-pending bound và incumbent-order preservation không phải
state-of-the-art optimization đầy đủ.

**Decision:** Hoàn thành đủ 14 ticket WP3 như một hard correctness boundary.
Incident/breach là immutable ledger riêng. `CommitmentDecisionValidator` tự dựng
lại physical feasibility, immutable state boundary, promise/delta, locks và
budget; candidate filter chỉ early-prune, Runner luôn full-fleet revalidate.
Produced certificate phải bind exact input/proposed state hash và tập publication
ID trong actions. Commitment policy là canonical named config có content hash.
Checkpoint chỉ được phát khi không có pending decision và restore phải qua hash,
manifest, travel identity, genesis/post-genesis reachability cùng ledger/breach
cross-relations. Publication vẫn commit duy nhất tại matching ACK.

**Optimization/claim decision:** Hard vector không được nén vào weighted scalar
hoặc đưa vào objective như soft preference. WP3 chứng minh feasibility/auditability,
không chứng minh C1 tốt hơn B1. Inversion/relocation/vehicle-switch dimensions
được validator hỗ trợ nhưng B1 hiện không chủ động sinh chúng. Schedule strategy,
candidate loss, forward slack/precompute, multiple-plan pool, lexicographic/Pareto
selection và OR-Tools thuộc `RB-WP4-001` refinement. Không sao chép số horizon,
runtime hoặc stated preference từ paper thành default.

**Paper/claim evidence:** In-app Browser đối chiếu Gaul et al. (2021), Schulz &
Pfeiffer (2026), Geržinič et al. (2023), Tiwari et al. (2024), Ackermann & Rieck
(2025). Evidence hỗ trợ rolling horizon, slack/precompute, history sensitivity,
lexicographic/Pareto và multiple-plan baselines nhưng không cấp một universal
numeric policy hoặc novelty claim mới. Mapping chi tiết nằm trong `03` và `21`.

**Alternatives considered:** trust solver-provided delta/certificate; chỉ thêm
if/else vào `RollingCostPolicy`; serialize partial checkpoint; dùng incident để
refund budget; triển khai OR-Tools ngay; gọi current B1 globally optimal; lấy
10–15 phút/99.5%/survey coefficients làm default.

**Consequences:** WP3 Complete; logical inventory 414. Required full-solution
command tại thời điểm chấp nhận ADR bị Windows Application Control `0x800711C7`
chặn fresh DLL, nên
evidence được tách minh bạch thành unaffected suites, 54/54 policy-safe Runner
methods và bốn clean-process cases; không gọi đó là full-solution pass. Chỉ
`RB-WP4-001` refinement READY, không production WP4 implementation nào READY.
Revalidation sau đó ngày 2026-08-03 đã pass full solution 414/414; ADR không đổi
claim boundary vì đây chỉ là thay đổi trạng thái host-policy evidence.

**Evidence:** `Domain/Incidents`, `Application/Commitments`, commitment filter,
strict Contracts/schema, `Runner/Configuration`, `OnlineStateCheckpointCodec`,
WP3 tiny scenario/script, exact-small/property/mutation tests,
`reviews/wp1-wp3/README.md`, `tasks/28` và `tasks/29`; Release build/format và
published replay hashes trong mục 4.

**Supersedes / superseded by:** Hoàn tất executable semantics của ADR-021 và
đóng WP3; không supersede ADR-014/016/017 hash framing, ADR-018 O-001 hoặc
ADR-020 physical B1 semantics. ADR-023 của WP4 chỉ được bổ sung sau refinement.

### ADR-023 — 2026-08-03 — Accepted

**Context:** Audit WP1–WP3 cho thấy hard-vector gate là correctness mechanism
thật nhưng B1 vẫn dùng earliest-feasible, single plan, four-request/ID cap và
Cartesian selector. Nếu C1 được cấp raw candidate khác, prune sau cap không được
ghi, hoặc OR-Tools incumbent tự publish, đánh giá sẽ trộn commitment effect với
compute/candidate bias. Multiple-plan/waiting/repair còn yêu cầu state và
checkpoint semantics, không thể thêm bằng vài nhánh `if/else` trong B1.

**Research evidence:** In-app Browser đọc lại Gaul et al. 2021, Schulz &
Pfeiffer 2026, Tiwari et al. 2024 và Ackermann & Rieck 2025; đọc bổ sung
Mitrović-Minić & Laporte 2004 về drive/wait/dynamic waiting, Masson–Lehuédé–
Péton 2013 và Gschwind 2019 về forward-time-slack/incremental feasibility,
Ackermann & Rieck 2022 về future insertion guidance, cùng official OR-Tools/
NuGet. Evidence hỗ trợ mechanism/baseline, không cho universal horizon, pool
size, weight, budget hoặc effectiveness target.

**Decision:** Khóa đủ 12 quyết định trong `tasks/30`:

1. B1–B5/C1/C2 dùng cùng raw physical candidate set và cap trước policy gate;
   report request/candidate omission, hard-prune và solver loss riêng.
2. Main schedule là `earliest-feasible`; named wait control chuyển waiting slack
   có thật thành current-node hold waypoint, không chỉ sửa ETA nội bộ.
3. Exact mode fail nếu omit. Bounded priority là latest pickup/arrival/ID; cap
   theo accepted count, admissible operational key, slack reserve và stable ID.
4. Slack/precompute cache bind full route/position/time/travel identity; chỉ
   early-prune, cache miss khi key đổi và cached/uncached phải tương đương.
5. Repair chỉ remove/reinsert waiting incumbent trong cùng vehicle, giữ O-001;
   B4 được ghi rõ `no-reassignment-repair`.
6. B5 plan pool/version/distinguished plan nằm trong canonical state/checkpoint;
   alternative incompatible với executed/frozen decisions bị loại.
7. Multi-pass lexicographic là accepted → policy utilization/warning → 10
   revision dimensions → operational cost → candidate-ID vector; không scalar
   hard vector. Normalized utilization chỉ là checked ranking ppm.
8. C1/C2 cùng hard gate; C2 warning chỉ xếp hạng trong hard-feasible set.
9. Solver-neutral port/model ở Application, policy ở Algorithms, package
   `Google.OrTools 9.15.6755` chỉ ở Solvers.OrTools.
10. Replay dùng deterministic work/CP deterministic-time budget, one worker và
    explicit seed. Wall time chỉ metric; status/bound/gap/fallback truthful.
11. Exact-small bound 2 vehicle/2 pending/1 repair incumbent/pool 4, ít nhất 64
    seeds; infinite budget/locks off/earliest/no-repair/single-plan bằng B1.
12. Solver/pool không publish trực tiếp; full WP3 validator, certificate, state
    hash, pending transaction và matching ACK vẫn là gate cuối.

**Alternatives considered:** hard gate thành weighted penalty; C1 sinh thêm raw
candidate sau prune; cap bằng hash ID; cache không bind travel/version; latest ETA
chỉ trên paper mà không có route hold; mở cross-vehicle reassignment; in-memory
plan pool không checkpoint; CP-SAT nhiều thread; dùng wall-clock timeout làm
replay outcome; báo FEASIBLE thành OPTIMAL; lấy số paper làm default.

**Consequences:** `RB-WP4-001` Done, queue `RB-WP4-002..014` được phép thực hiện
tuần tự và chỉ `002` Ready. WP4 vẫn ở claim Implemented/Mechanically valid cho
từng ticket; hiệu quả chỉ được gọi là tín hiệu micro/exact-small trước paired
Layer 1/2. O-001/O-002/O-003/O-004 không đổi.

**Evidence:** `tasks/29`, `tasks/30`, Browser sources trong `21`; required suite
baseline 414/414 trước production WP4.

**Supersedes / superseded by:** Bổ sung ADR-020/022 cho policy/solver quality;
không đổi protocol/hash, reassignment O-001 hay validator/certificate semantics.

### ADR-024 — 2026-08-03 — Accepted

**Context:** `RB-WP4-002..012` đã tạo đầy đủ mechanisms nhưng closure còn cần bằng
chứng độc lập và audit source-level để tránh kết luận từ test happy path. Đặc biệt
cần chứng minh hard gate thực sự loại candidate, mapper/OR-Tools không cùng lỗi với
expected code, bounded omission đi xuyên diagnostics, và machine-local timing
không bị nâng thành effectiveness claim.

**Decision:** Đóng WP4 và Q2 core mechanical correctness vì:

1. B1 generator/selector khớp independent enumeration trên 64 fixtures trong
   published exact-small bound.
2. Production C1 mapper + actual pinned OR-Tools khớp một enumerator độc lập khác
   trên 64 fixtures; mọi objective level optimal với exact gap 0.
3. Hard-gate mutation fixture có raw set lớn hơn hard-feasible set; actual bounded
   omission truyền count/digest tách khỏi solver loss và validated fallback.
4. Cache/infinite-equivalence/plan-pool/checkpoint/deadline/replay/publication gates
   từ tickets trước vẫn pass trong full suite 557/557.
5. Source audit xác nhận objective không scalarize hard vector, solver không tự
   publish, candidate/solver/publication loss tách biệt và matching ACK là commit.
6. Synthetic runtime curve chỉ được ghi là promising machine-local signal; paired
   Layer 1/2, scale, service effect và user satisfaction vẫn unproven.
7. Review `docs/reviews/wp1-wp4-final/` là handoff hiện hành; historical review
   WP1–WP3 được giữ nguyên.

**Alternatives considered:** đóng chỉ vì 523 tests pass; dùng production comparer
làm oracle; ghi microbenchmark thành production SLA; mở luôn BeGo migration mà
chưa khóa process/transaction ownership; xóa historical environment blocker.

**Consequences:** `RB-WP4-001..014` Done, WP4 Complete. Windows Application Control
`0x800711C7` không tái xuất hiện ở closure run nhưng historical record không bị
xóa. Chỉ refinement-only `RB-WP5-001` Ready; không có WP5 implementation ticket.
O-001/O-002/O-003/O-004 và protocol/hash/validator/certificate semantics không đổi.

**Evidence:** closure blocks `RB-WP4-013/014`, `tasks/30`, `tasks/31`, final review,
required suite 557/557, Release/format/vulnerability/JSON/Markdown/process/diff gates.

**Supersedes / superseded by:** Đóng execution của ADR-023; không supersede
ADR-014/016/017/020/022 hoặc claim boundary trong `03`/`21`.

### ADR-025 — 2026-08-05 — Accepted

**Context:** WP4 đã đóng core mechanical correctness nhưng BeGo hiện là snapshot
outing application: `Session`/`PickupRequest` không phải online RideBound aggregate,
không có append-only event/decision/ACK/outbox state, và flow cũ không thể cho biết
Runner call đã commit qua các crash window. Gọi child process rồi `SaveChanges`/
SignalR bằng happy-path `if/else` sẽ tạo khoảng mất/nhân đôi decision và có thể
manufacture ACK/certificate sai.

**Research evidence:** In-app Browser đọc paper Saltzer–Reed–Clark về end-to-end
duplicate suppression/ack/crash recovery; Helland về local transaction entity,
at-least-once messaging và durable activity state; transactional outbox pattern;
official EF Core transaction/optimistic concurrency; PostgreSQL locking/
`SKIP LOCKED`; official ASP.NET hosted service; và IETF HTTPAPI Idempotency-Key
draft-07. Draft cuối đã expired/archived ngày audit nên chỉ dùng như prior art,
không claim RFC compliance. Chi tiết và URL ở
`research/wp5-distributed-integration-evidence-2026-08-05.md`.

**Decision:**

1. Adapter/API/EF/SignalR code nằm trong tracked BeGo `src`; RideBound không copy/
   reference source BeGo và BeGo không reference RideBound core assemblies. BeGo
   chỉ gọi exact versioned `RideBound.Runner` artifact qua NDJSON.
2. O-007 đóng cho WP5 bằng long-lived child process. Config tách command path,
   artifact path, expected binary SHA-256, core commit, mode/policy/config hash;
   preflight mismatch fail closed.
3. Một `CommitRun` là serialization entity: một pending operation/decision, event
   sequence/epoch liên tiếp. Nhiều worker có thể xử lý nhiều run; database lease +
   partial uniqueness ngăn hai owner cùng run.
4. Chỉ local transaction ngắn: T1 append idempotency/event/work; external Runner;
   T2 persist exact decision/certificate/rebuildable projection/outbox; external
   matching ACK; T3 persist ACK/checkpoint. Không giữ DB lock qua process/SignalR.
5. Delivery là at-least-once với idempotent effect, không claim exactly-once.
   Composite idempotency scope bind actor/route/run/key và canonical payload hash;
   same key khác fingerprint conflict, in-flight retry không cấp sequence mới.
6. Outbox cùng T2; relay dùng stable message ID, deterministic per-run order và
   lease. `SKIP LOCKED` chỉ dùng queue claim, không dùng audit query.
7. Crash/ACK uncertain buộc bỏ process handle, start đúng binary, initialize cùng
   manifest, restore checkpoint, replay committed suffix, replay pending event,
   compare exact decision hash rồi mới ACK. Mismatch chuyển `Diverged`, không publish.
8. Bootstrap tạo run-local pseudonymous ID/node map, E7/ms ties-to-even, full
   directed travel matrix và provenance từng field. Time window/max ride phải từ
   explicit override hoặc named stored profile; thiếu thì fail, không có hidden default.
9. Feature flag mặc định off giữ Session/endpoints hiện hành. Runtime rollback là
   stop claim/disable; không xóa append-only evidence. Raw identity link tách khỏi
   pseudonymous research export; log/realtime payload không chứa exact location,
   token hoặc raw witness mặc định.
10. Paired B1/C1 dùng cùng source input, seed, binary/work rules và allowlist duy
    nhất policy/config fields; replay cùng arm phải byte/hash exact. Kết quả chỉ là
    Layer-1 mechanical/descriptive signal trước WP8/WP9.
11. Ordered queue `RB-WP5-002..014` thực hiện tuần tự; mỗi code ticket chạy BeGo
    targeted/full backend, required RideBound suite, frontend khi surface đổi,
    và real PostgreSQL gate khi liên quan persistence/concurrency.

**Alternatives considered:** đặt adapter trong RideBound và project-reference qua
repo; gắn fields vào `Session`; distributed transaction qua DB/process/SignalR;
call Runner trong DB transaction; in-memory `Channel` làm durable queue; key-only
dedup; spawn process mỗi event; retry ACK trên uncertain live process; import old
snapshot thành ledger; auto default missing time windows; HTTP/gRPC ngay WP5;
drop evidence khi rollback.

**Consequences:** `RB-WP5-001` Done, no production implementation claim. Queue
`tasks/32` có đúng `RB-WP5-002 READY`. Persistence/recovery phức tạp hơn direct
call nhưng mọi crash window có durable interpretation, Runner vẫn là decision
authority và BeGo cũ có default-off rollback.

**Evidence:** pinned checkouts/baselines trong mục 4; `tasks/31`, `tasks/32`,
research evidence; Browser excerpts; source audit BeGo Domain/Application/
Infrastructure/API; required RideBound 557/557, BeGo backend 25/25, frontend 7/7.

**Implementation amendment 2026-08-05 (`RB-WP5-003..004`):** T1 không nhận
caller wall clock; lease dùng `transaction_timestamp()` trong DB. Event metadata
epoch/time/contiguous sequence phải bind exact canonical frame; canonical batch
hash bảo vệ bytes của Runner. `jsonb` chỉ phục vụ query; mọi replay/Runner write lấy strict
UTF-8 canonical `bytea`, vì PostgreSQL được phép normalize JSON text. Event batch
lưu cả first/last sequence; same-run composite FK ngăn operation/decision/
checkpoint cross-link. Queue claim dùng ordered `SKIP LOCKED`, bounded capacity
và commit lease trước khi trả work ra ngoài transaction.

**Implementation amendment 2026-08-05 (`RB-WP5-005`):** Runner artifact được
pin bằng absolute command/artifact path, SHA-256 và core commit; process pool có
giới hạn rõ và session là run-local. Client tự kiểm schema/capability/manifest/
run/epoch/time ở cả hai chiều, giới hạn exact UTF-8 line và stderr, gom
`decisionApplied` + checkpoint vào một I/O critical section. Mọi timeout,
cancellation, malformed response hoặc identity mismatch remove session rồi kill
toàn process tree. Dispose/start được serialize để không rò process qua race.

**Implementation amendment 2026-08-05 (`RB-WP5-006`):** Bootstrap được tách
thành synchronous immutable source capture trước external I/O và completion sau
exact capability negotiation. Manifest hash không còn là input trước `helloAck`;
nó được tính từ canonical manifest bằng đúng domain `RideBound.ManifestHash.v1`
sau khi bind exact selection. Adapter chỉ materialize graph node cần thiết, áp
node cap trước complete O(n²) matrix call, fail closed mọi missing/unreachable/
ambiguous conversion và giữ legacy state dưới dạng hashed provenance-only.

**Implementation amendment 2026-08-05 (`RB-WP5-007`):** Sửa một coupling sai
trong `003..004`: idempotency fingerprint bind canonical HTTP method/resource/
path/body semantics, không bind `eventSeq`/epoch do server cấp; exact eventBatch
bytes tiếp tục có hash riêng. Create-run khóa composite idempotency bằng PostgreSQL
advisory transaction lock trước lookup/insert để cùng key luôn replay cùng winner,
không phụ thuộc thứ tự unique index. HTTP chỉ expose user-safe views và exact
cached response; controller không nhận raw protocol/ledger/certificate input.

**Implementation amendment 2026-08-09 (`RB-WP5-008`):** Runner response được
materialize lại trong T2 từ exact canonical frame; decision, certificate,
projection, timeline và outbox commit cùng transaction. Claim bằng raw SQL phải
clear committed EF snapshots có guard trước khi T2/T3 lock row, nếu không identity
map có thể trả revision trước claim và làm fence sai. ACK outcome không chắc chắn
luôn bỏ session, reconstruct fresh Runner và so byte/hash exact; T3 dùng DB time,
owner và revision. Promise service order ở BeGo boundary tái lập invariant Domain,
không chỉ schema field checks. Cơ chế này là at-least-once retry với durable
idempotent effect theo RIFL/outbox prior art, không phải exactly-once delivery.

**Implementation amendment 2026-08-09 (`RB-WP5-009`):** Outbox claim chọn exact
unpublished head của mỗi run trước khi xét availability/lease, nên backoff hoặc
slow head không cho sequence sau overtaking; `SKIP LOCKED` vẫn cho run khác tiến.
Claim tăng `attempt_count` bằng DB time và commit trước SignalR; mark/reschedule
phải khớp message/owner/attempt/unexpired lease. Payload được tái kiểm exact
user-safe allowlist và Session target bị migration trigger khóa không retarget.
Source audit áp dụng đúng giới hạn Gray-Cheriton: lease không fence external send
đã bắt đầu. Wire vì vậy mang stable aggregate sequence/hash và frontend bỏ
duplicate/stale callback theo per-run cursor; offline gap vẫn cần timeline `010`.
`SendAsync` không được ghi thành durable client acknowledgement hoặc exactly-once.

**Implementation amendment 2026-08-09 (`RB-WP5-010`):** Audit timeline dùng exact
row-value `(sequence,id)` keyset và server-owned access scope; raw subject link chỉ
phục vụ ownership join, raw decision/certificate chỉ qua operator policy mặc định
deny. JSONB phải canonicalize rồi kiểm hash/allowlist lại ở end-to-end boundary.
Projection không được coi là source of truth: repeatable-read rebuild tái tạo từ
append-only decision/certificate/operation, kiểm full hash/state/materializer chain
và so rebuilt/live hash; mismatch chặn export. Audit read không dùng `SKIP LOCKED`.
Migration rollback guard phải chạy trước destructive `Down`. Logging/telemetry/
export không mang subject, token, coordinate, route, witness hoặc raw evidence.
Các cơ chế này là correctness/rebuildability/privacy prior-art application, không
phải exactly-once, throughput/SLA, effectiveness hoặc novelty claim.

**Implementation amendment 2026-08-09 (`RB-WP5-011`):** Rollout mode là
`Disabled/Shadow/Live`; omission/default là Disabled và không đăng ký COMMIT hosted
service. Namespace Shadow/Live phải persist bất biến trên run, decision claim phải
lọc namespace và outbox store chỉ claim Live — ngừng đăng ký relay trong RAM là
không đủ vì live restart có thể phát shadow backlog. Existing rows backfill Shadow.
Mọi active worker/member boundary phải qua exact Runner artifact hash preflight;
preflight không spawn process. Disable/cancel ngừng claim mới, để durable lease hết
hạn và same-namespace worker reclaim theo existing fence. Feature rollback không
xóa append-only evidence hoặc sửa Session route; Down guard chạy trước destructive
DDL. Đây là operational correctness/compatibility, không phải effectiveness claim.

**Implementation amendment 2026-08-09 (`RB-WP5-012`):** Layer-1 pair dùng cùng
commitment-mode publication/validator path cho cả B1 và C1; không cho B1 né
certificate bằng online mode khác. Raw/canonical workload, provenance, common
policy và mỗi arm config đều hash-bound; effective config hash domain-separate bind
hai config. Ngoài `/policyId`, normalized config phải exact; initialize chỉ được
khác policy ID/effective hash và `decisionApplied.decisionHash` được phân loại rõ là
output-derived control. Validated config bytes được stage rồi kiểm trước/sau từng
clean process để tránh TOCTOU. Decision phải qua BeGo exact materializer, checkpoint
phải được tính lại độc lập, repeats phải exact, và bundle manifest reject file thiếu,
thừa hoặc tamper. Bundle bind harness source + executing assemblies vì working tree
chưa commit không được giả thành reproducible chỉ bằng base commit. Đây là
mechanical/correctness evidence, không phải effectiveness/non-inferiority/SLA claim.

**Implementation amendment 2026-08-09 (`RB-WP5-013`):** Failure evidence phải
hard-kill executable riêng tại từng durable decision/outbox boundary, có marker trước
crash và fresh-process recovery; exception/finally trong test process không được gọi
là hard crash. Expected state/claim được dựng bởi test-owned observed-history oracle,
không gọi production transition table. Concurrency phải so exact operation set trên
PostgreSQL thật với nhiều worker, không chỉ đếm tổng. Mutation gate gồm năm fault model
độc lập phá unique active-run, ACK/checkpoint, T2/outbox, fingerprint và canonical hash;
`5/5` chỉ là required-mutant result, không phải external mutation percentage.
Performance evidence randomize scenario order, warm-up, nhiều repetition và lưu raw
sample/machine/database/row counts; không assert SLA. LDFI, Elle, QuickCheck, mutation
testing và rigorous-performance papers cung cấp phương pháp, nhưng implementation
không chạy external LDFI/Elle/QuickCheck/mutation engine và không claim formal proof.

**Implementation/closure amendment 2026-08-09 (`RB-WP5-014`):** Dữ liệu dùng
để authorize member phải immutable: `commit_subject_links` dùng cùng append-only
reject trigger như evidence tables. Mọi outbox row phải bind non-null operation cùng
run. Query phải chọn absolute earliest unpublished head trước rồi kiểm head `Applied`,
vì T2 không được lộ trước T3 và row sau không được vượt head chưa T3. Per-run-head
SQL phải đi kèm application-level independent scope/DbContext
cho mỗi run; otherwise một SignalR send chậm vẫn gây cross-run head-of-line blocking.
Migration rollback tiếp tục fail closed khi còn dữ liệu và cần explicit guard. Closure
review không biến mechanical paired/fault/local-curve evidence thành formal delivery,
SLA hoặc effectiveness claim; nó chỉ cho phép mở đúng `RB-WP6-001` refinement.

**Supersedes / superseded by:** Khóa O-007 và thay proposed WP5 project placement
trong `19` bằng BeGo-owned adapter + artifact boundary. Không đổi protocol/hash,
O-001/O-002/O-003/O-004, WP3 publication gate hoặc ADR-024 claim boundary.

### ADR-026 — 2026-08-09 — Accepted

**Context:** WP1–WP5 đã đóng mechanical correctness/integration gate nhưng chưa có
common data-to-result harness. WP5 paired bundle chứng minh cùng exact Runner path
trên một fixture, không có dataset normalizer, public scenario identity, general run
grid, failure/exclusion denominator contract, independent metric oracle hoặc
experiment bundle. Viết harness trước khi khóa các semantics đó sẽ tạo survivorship
bias, metric leakage và artifact có checksum nhưng không tái lập được.

Primary evidence được đọc bằng in-app Browser và ghi tại
`docs/research/wp6-benchmark-reproducibility-evidence-2026-08-09.md`: FleetPy
Manhattan Zenodo/paper, RFC 8785 JCS, Random123, RFC 8493 BagIt, W3C PROV-DM,
FAIR, Datasheets for Datasets, Sandve reproducibility rules và ACM artifact
terminology. Contract đầy đủ nằm tại `docs/benchmarking/wp6-contract-v1.md`.

**Decision 1 — public dataset/source boundary:** Khóa FleetPy Manhattan case-study
data v1, DOI `10.5281/zenodo.15187906`, file `FleetPy_Manhattan.zip`, Zenodo MD5
`8b11882ae9c6d87f666bf6e006806744`, license `CC BY 4.0` làm public source chính.
Downloader phải kiểm publisher length/MD5 và local SHA-256 exact bytes. Raw archive
nằm trong ignored content-addressed read-only cache, không commit/overwrite. Safe
extractor fail closed với traversal/symlink/reparse/duplicate/case/size/ratio attack.
Derivative tiny/medium phải có attribution, transform recipe, source selection hash
và không suy observed preference/satisfaction/identity từ TLC/FleetPy trip records.

**Decision 2 — scenario identity/canonical units:** Scenario content là strict
versioned solver/simulator-neutral document. Canonical bytes dùng RideBound Canonical
JSON v1 — accepted-domain subset hẹp của JCS: UTF-8/no BOM, duplicate/invalid Unicode
reject, recursive ordinal property sort, sequence order preserved, no null/float, safe
integers only. Quantities dùng explicit scaled units; time semantics dùng relative ms.
Semantic identity dùng SHA-256 domain `RideBound.Wp6.Scenario.v1` và length-prefixed
frames, tách khỏi plain file SHA-256. Source bytes, member inventory, selection,
normalizer source/version/config và report hash đều được bind.

**Decision 3 — demand/event semantics:** Scenario khai báo source timezone/window,
warm-up/scoring/horizon/drain boundaries, explicit initial fleet, requests, directed
travel snapshots, driver semantics và event ordering. Nếu upstream có validated total
sequence thì giữ nó; nếu không dùng `(simTimeMs,typeRank,sourceOrdinal,stableId)` theo
`ridebound-event-order-v1`. Missing/unreachable arc làm source exclusion hoặc scenario
invalid; cấm zero/max/Euclidean/reverse imputation. Mọi source row có đúng một
selected/not-selected/excluded disposition và conservation report.

**Decision 4 — plan/pairing:** Benchmark plan được materialize trước outcome, chứa
scenario hashes, exact Runner/runtime/source, arm policy/version/config/effective
hash, candidate/validator/solver/work/capability binding, warm-up/repeats và resource/
failure/exclusion/metric profiles. `wp4-common-candidate-v1` chỉ pair B1/B2/B3/B4/
C1/C2 khi common raw candidate/work semantics exact; B5 ở
`wp4-multiple-plan-v1`. Một fresh isolated Runner process cho mỗi arm/repeat/attempt;
arm order HMAC-counterbalanced. Rerun có authorization tạo new plan/attempt và giữ
old evidence, không rerun seed xấu chọn lọc.

**Decision 5 — seed hierarchy:** Master seed là 256-bit hex. Component seed dùng
HMAC-SHA-256 domain `RideBound.Wp6.Seed.v1` trên scenario hash, repeat index,
component ID và stable item ID. Full digest được lưu; int32 conversion là first-four
big-endian masked `0x7fffffff`. Sampling/order dùng hash rank stable ID. Cấm clock,
GUID, thread/process ID, `Random.Shared`, implicit default, global call-count RNG và
unstable enumeration. Đây là addressable-randomness mechanism học từ Random123;
implementation không dùng/copy Random123 và không kế thừa claim của nó.

**Decision 6 — exact Runner boundary:** Harness/fixture/adapter chỉ phát exact
RideBound protocol input và consume exact output từ pinned external Runner process.
Không reference/call policy/core để tạo alternative result. Pre/postflight bind
assembly, runtime, config, source and staged input bytes. Fixture driver chỉ sở hữu
declared exogenous event/execution semantics; FleetPy control adapter vẫn thuộc WP7.
Mọi decision đi qua WP3 independent publication validator, certificate, state hash,
matching ACK/checkpoint path.

**Decision 7 — raw result and terminal conservation:** Mỗi planned run có immutable
input/output/stderr/resource/preflight/postflight files, observation index và đúng một
terminal record `succeeded|failed|excluded`. Index giữ event/request/vehicle/run/
scenario/arm/repeat locator nhưng không thay raw NDJSON. Plan conservation bắt buộc:
`planned = succeeded + failed + excluded`. Negative/policy-worse output vẫn được giữ.

**Decision 8 — failure taxonomy:** Typed failures gồm invalid input/artifact mismatch,
capability divergence, start/crash, wall/CPU/memory/resource breach, solver unknown,
invalid/incomplete protocol output, state divergence, metric oracle mismatch và
bundle invalid. Failure không thành metric 0/infeasible, không bị silent drop và
không được chuyển thành exclusion sau outcome.

**Decision 9 — exclusion/denominator:** Exclusion chỉ trước outcome theo exact
predeclared rules: license/checksum/invalid source/unreachable/capability/position/
pairing incompatibility. Append-only row ghi rule/source/stage/subject/evidence và
retained denominators. Mỗi metric ghi unit/window/scope/numerator/denominator ID/value/
missing semantics. Valid-run descriptions luôn đi cùng planned failure/exclusion
counts; denominator zero là missing, không phải rate 0.

**Decision 10 — metric ownership:** WP6 registry v1 tính unique arrived/accepted/
rejected/completed, defer actions, decision epochs, promise publications/revisions,
breaches, non-normal certificates, ten decision-delta sum/max và exact resource
metrics. Không lưu IEEE float; rate dùng integer PPM ties-to-even và checked wider
intermediate. Production calculator chỉ đọc raw evidence. Independent oracle nằm ở
separate source/executable, không reference production calculator/models, và phải tạo
byte-identical sorted rows/hash trên tiny bound. Mismatch invalidates bundle trước
aggregate/statistics. Runner/simulator aggregate column không là sole source.

**Decision 11 — resource accounting:** Plan khai báo wall/CPU/working-set/process/
stream byte limits, deterministic policy work budgets, enforcement kind, warm-up và
repeats. Supervisor dùng monotonic time, raw samples và process-tree termination;
machine/OS/CPU/memory/.NET/container/git/source/assembly/command provenance bắt buộc.
Local limit/timing là experiment control, không phải production throughput/SLA.

**Decision 12 — bundle integrity/provenance:** Result bundle dùng strict
BagIt-compatible SHA-256 profile: payload dưới `data/`, payload/tag manifests, logical
bundle manifest, README/verifier, exact role/media type/length/hash/producer/source
derivation. Strict RideBound verifier reject absolute/traversal/symlink/reparse/
case-collision, missing, extra, tamper, source/config/runtime mismatch, run
conservation, transcript/hash, failure/exclusion, metric oracle hoặc claim failure.
Logical manifest không tự hash; BagIt payload manifest hash nó. PROV-like entity/
activity/agent và Datasheet/FAIR metadata được lưu nhưng không claim full external
PROV/FAIR certification. Bag validity không chứng minh scientific validity.

**Decision 13 — tiny/medium gate:** Tiny có tối đa 8 request/2 vehicle/16 node/256
arc, ba measured repeats cho B1/C1 trong hai clean harness processes, non-zero
accept/reject-or-defer/revision/delta coverage, exact raw-to-oracle equality và failure/
bundle mutations. Medium là deterministic CC BY derivative từ verified FleetPy
Manhattan, target 128 request/32 vehicle/max 96 node/9.120 non-self directed arc,
HMAC selection độc lập policy, two-clean normalization và ít nhất ba mechanical
repeats. Nếu source không thỏa mà không fabricate semantics thì stop/amend ADR, không
đổi lén. WP6 medium gate là normalizer/harness mechanics, không phải FleetPy Layer-2
effectiveness; WP7 vẫn sở hữu control adapter.

**Decision 14 — claim checker:** WP6 chỉ cho `mechanical|development` evidence và câu
“same-team clean-process repeatability”. Bundle bắt buộc caveat no effectiveness,
non-inferiority, production SLA, ACM badge, independent reproducibility/replicability,
novel ETA/reassignment/satisfaction claim. Claim profile machine-readable chặn các từ/
synonym tương ứng. Functional/Reusable-like properties là target nội bộ, không phải
ACM award. WP6 không chọn O-002/O-003/O-004 và không gọi public data là consented
human behavior.

**Alternatives rejected:** Một JSON/CSV aggregate duy nhất; seeded `System.Random`
global; fixed arm order; in-process policy call; simulator aggregate as truth; timeout
as zero/infeasible; drop failed/outlier seed; zip-only bundle; checksum-only verifier;
commit 408.9 MB raw archive; reuse WP5 paired/local curve as WP6 effectiveness; mở
FleetPy adapter trong refinement.

**Consequences:** WP6 có nhiều typed/integrity work trước khi có graph/table đẹp, nhưng
data loss, arm asymmetry, hidden RNG, metric circularity và claim inflation trở thành
testable failures. Contract v1 là equivalent contract cho `RB-WP6-001`; JSON schemas
và primitives được hiện thực ở ticket nhỏ nhất `RB-WP6-002`. Ordered queue
`RB-WP6-002..014` nằm trong `docs/tasks/34-wp6-common-benchmark-harness-ticket-plan.md`.
Chỉ `002` Ready; chưa có WP6 executable/result tại thời điểm ADR được accept.

**Supersedes / superseded by:** Thay ví dụ manifest có `TBD` trong `docs/10` bằng
contract v1 cho WP6 implementation. Không đổi WP1 protocol/hash, WP3 publication gate,
ADR-024 policy semantics, ADR-025 BeGo boundary, O-001 hoặc O-002/O-003/O-004.

### ADR-027 — 2026-08-09 — Accepted

**Context:** Khi `RB-WP6-004` chuẩn bị tạo scenario thật, executable contract `1.0.0`
cho scenario chứa `normalizationReportHash`, đồng thời normalization report chứa
`scenarioContentSha256` và `scenarioHash`. Fixture dùng các hash giả độc lập nên test
schema vẫn pass, nhưng artifact thật đòi hai canonical documents là cryptographic
fixed point của nhau. Không có thứ tự tính hoặc placeholder nào thỏa contract đó.

**Decision:** Sửa WP6 scenario/report schemas lên `1.0.1`. Bỏ
`normalizationReportHash` khỏi `ScenarioContent`; giữ scenario bind exact raw source,
member selection, normalizer source/version/config và validation summary. Tính
canonical scenario/plain SHA/domain-separated identity trước; sau đó report ghi hai
hash scenario cùng conservation/selection/exclusion evidence và mới nhận report hash.
Logical bundle bind scenario lẫn report. Dataset/plan/run/metric/bundle document shape
không đổi và tiếp tục `1.0.0`. Runtime validator dùng exact per-document version.

**Evidence required:** schema/runtime property parity, old cyclic field bị reject như
unknown, clean-process identity vectors được republish và regression trực tiếp tạo
scenario identity trước rồi bind nó vào report. Không được tái diễn giải field cũ,
dùng zero hash, lặp đến gần fixed point hoặc bỏ report khỏi bundle.

**Consequences:** provenance trở thành Merkle DAG một chiều có thể tạo và verify độc
lập. Đây là correction trước WP6 executable/result đầu tiên, không phải experiment
protocol change và không làm phát sinh effectiveness claim.

**Supersedes / superseded by:** Supersede riêng câu “report hash được bind vào
scenario” của ADR-026 Decision 2 và contract `1.0.0`; không đổi các decision còn lại,
WP1 protocol/hash, WP3 publication gate hay O-001/O-002/O-003/O-004.

### ADR-028 — 2026-08-09 — Accepted

**Context:** Khi hiện thực `RB-WP6-006`, supervisor bắt buộc phải giới hạn và ghi
typed terminal evidence cho process-tree count, stdin/stdout/stderr bytes và caller
cancellation. ADR-026 Decision 8/11 cùng ticket 006 đã yêu cầu các nhánh này, nhưng
failure schema/rule set `1.0.0` chỉ liệt kê wall/CPU/memory. Vì vậy một implementation
tuân ticket sẽ tạo record bị schema từ chối, còn ép các nhánh đó thành
`process.crash`/`protocol.invalid-output` sẽ làm sai nguyên nhân và audit denominator.

**Decision:** Nâng umbrella contract lên `1.0.2`, riêng `FailureRecord` lên `1.0.1`
và failure rule set thành `wp6-failure-v1.0.1`. Bổ sung đúng năm terminal codes:
`process.cancelled`, `resource.process-count-exceeded`,
`resource.stdin-bytes-exceeded`, `resource.stdout-bytes-exceeded` và
`resource.stderr-bytes-exceeded`. Tất cả có stage `execution`; partial raw evidence
vẫn phải giữ. Benchmark plan fixture/identity vector phải được republish vì rule-set
identity là một phần canonical plan. Scenario/report version và identity không đổi.

**Evidence required:** runtime/schema parity; mỗi code mới decode/validate; supervisor
fake-child mutation kích hoạt đúng code; plan/vector clean-process byte exact; full
solution pass. Không được dùng mã mới để biến local resource control thành SLA.

**Consequences:** failure accounting phản ánh đúng cơ chế đã predeclare thay vì có
nhánh implementation không thể serialize. Đây là contract correction trước WP6 run
record/bundle đầu tiên, không phải thay đổi outcome, exclusion hay claim boundary.

**Supersedes / superseded by:** Bổ sung phần liệt kê cụ thể cho ADR-026 Decision 8/11
và contract `1.0.1`; không đổi WP1 protocol, WP3 publication gate, policy semantics,
O-001 hay O-002/O-003/O-004.

### ADR-029 — 2026-08-09 — Accepted

**Context:** Khi hiện thực crash-safe `RB-WP6-007`, một planned intent có thể tồn tại
nhưng harness chết giữa copy/index/detail/log/atomic-directory boundaries. Failure
rule set `1.0.1` không có code cho nhánh này. Ghi `process.crash` sẽ sai nếu external
Runner đã exit 0; bỏ run sẽ vi phạm terminal conservation. Observation contract cũng
yêu cầu `certificateHash` nhưng chưa khóa projection byte cụ thể.

**Decision:** Nâng umbrella contract lên `1.0.3`, `FailureRecord` lên `1.0.2` và
failure rule set thành `wp6-failure-v1.0.2`. Thêm
`harness.persistence-incomplete` tại stage `persistence`; recovery/seal phải giữ
partial staging evidence rồi tạo một terminal failure mới, không overwrite và không
đổ lỗi cho Runner. `certificateHash` trong observation index là plain SHA-256 của
exact RideBound-canonical certificate-body JSON; envelope/decision identities vẫn
giữ domain hiện có và không bị thay thế bởi locator này.

**Evidence required:** crash injection tại mọi write boundary; retry/recovery cho ra
một terminal directory hoặc typed incomplete, log sequence/hash chain không gap;
failure không sinh zero metric; schema/runtime/vector/full-solution gates pass.

**Consequences:** plan identity vector phải republish vì failure rule-set ID đổi.
Đây là persistence/accounting correction trước bundle đầu tiên, không thay outcome,
policy, exclusion-before-outcome hay claim boundary.

**Implementation amendment 2026-08-11 (`RB-WP6-007`):** Plan/run publication dùng
private staging rồi atomic directory rename; duplicate semantic cell trong cùng plan
bị cấm. Raw identity được pin/copy bằng streaming length/SHA, locator phải đúng exact
run path và layout reparse point bị từ chối. Observation index được tái sinh từ raw;
success bind initialize manifest với planned scenario/config/Runner binary, exact
decision/ACK và checkpoint applied epoch/time. Runtime inventory được rederive từ
role/file/length/SHA và pre/postflight phải cùng launch command. Failure/exclusion dùng
chung gapless previous-hash log; seal và in-flight commit hội tụ theo per-run lock.
Authorized rerun publish plan + `supersedes.json` nguyên tử, giữ trọn semantic grid/
denominator và recursively verify prior terminal evidence. Các invariant này thực thi
ADR-029; không đổi public contract/version hoặc claim boundary.

**Supersedes / superseded by:** Bổ sung ADR-026 Decision 7/8 và ADR-028; không đổi
WP1 protocol, WP3 validator/certificate, O-001 hay O-002/O-003/O-004.

### ADR-030 — 2026-08-11 — Accepted

**Context:** `RB-WP6-008` là lần đầu contract metric v1 được hiện thực. Chỉ decode JSON
và cộng counter sẽ cho kết quả có vẻ hợp lệ nhưng vẫn có thể tính sai khi transcript
đảo thời gian, request vừa accepted vừa rejected, completion trước acceptance, hoặc
window trộn request arrival với action/completion time. Một oracle gọi lại production
model/calculator cũng chỉ lặp cùng bug. Paper McKeeman về differential testing yêu cầu
các implementation thật sự khác nhau; Dolan–Moré nhắc rằng benchmark comparison cần
giữ phân phối/tập bài toán thay vì che mọi thứ trong một aggregate.

**Decision:** Giữ metric registry v1 ở đúng 36 definition/hash đã publish và khóa:

1. `warmup=[warmupStart,scoreStart)`, `scoring=[scoreStart,horizonEnd]`,
   `drain=(horizonEnd,drainEnd]`, `all=[warmupStart,drainEnd]`;
2. request outcome/rate/defer dùng arrival cohort; decision/certificate/promise/breach/
   decision-delta dùng decision-envelope time;
3. raw parser phải kiểm monotonic time/epoch, unique arrival/completion/terminal action,
   accepted/rejected exclusivity, arrival→defer/terminal→completion chronology và
   exact resource terminal maxima trước khi emit row;
4. ratio denominator 0 là missing, không phải 0; vector sum/ratio dùng `BigInteger`
   intermediate rồi fail typed nếu vượt canonical safe integer;
5. oracle là executable/source tree BCL-only, không ProjectReference tới production
   contracts/calculator/models; hai phía tự canonicalize/parse/state/hash và phải khớp
   toàn bộ 132 canonical rows, semantic/resource evidence và metric-set hash;
6. bất kỳ byte/hash mismatch nào là `metric.oracle-mismatch` và chặn bundle. Failed/
   excluded run không được có success metrics;
7. WP6 chỉ giữ run-level/pairing/planned-denominator evidence cho aggregate sau này;
   performance profile, failure penalty, estimand và conclusion thuộc WP8/WP9
   preregistration, không được tự thêm ở WP6.

**Consequences:** Metric path không còn là vài nhánh đếm đơn giản mà là hai state
reconstruction độc lập với lifecycle/window/evidence invariants. Agreement vẫn không
chứng minh specification đúng hay independent reproduction; protocol/store verifier,
mutation matrix và source/hash provenance vẫn bắt buộc. Đây là lần khóa đầu của semantic
boundary trước metric bundle đầu tiên, không đổi field/schema/registry/hash vector nên
contract vẫn `1.0.3`; thay đổi tương lai phải bump metric version và ADR.

**Supersedes / superseded by:** Hiện thực và làm rõ ADR-026 Decision 9/10; không đổi
WP1 protocol, WP3 commitment semantics, O-001/O-002/O-003/O-004 hay claim boundary.

### ADR-031 — 2026-08-11 — Accepted

**Context:** `RB-WP6-009` là bundle đầu tiên. RFC 8493 checksum/completeness một mình
không phát hiện scenario đặt sai address, base commit che dirty source, run bị đổi
seed/grid, transcript sai ACK/checkpoint, hoặc hai file production/oracle cùng bị sửa.
Contract cũng có một `metricSetHash` ở bundle trong khi ADR-030 identity gốc là per-run;
nếu dùng plain file SHA hoặc chọn tùy ý một run sẽ làm field đúng cú pháp nhưng sai
ngữ nghĩa. External verifier ghi report vào bag sẽ tự phá seal hoặc overwrite evidence.

**Decision:** Khóa strict bundle implementation boundary sau:

1. giữ RFC 8493 BagIt 1.0 exact LF/UTF-8, payload/tag SHA-256, every-payload-once,
   no-self tag manifest và payload oxum; RideBound bổ sung no-extra cùng cấm absolute,
   traversal, percent/control, reparse/junction, case/Unicode collision, Windows device,
   trailing dot/space;
2. logical manifest liệt kê đúng union `data/` trừ chính nó; self-reference chỉ được
   giải bằng payload manifest. `verify.ps1` phải byte-exact reviewed template;
3. bundle `metricSetHash` dùng domain mới
   `RideBound.Wp6.BundleMetricSet.v1(planHash, registryHash, exact LF all-run rows)`;
   per-run `RideBound.Wp6.MetricSet.v1` không đổi;
4. source inventory phải capture Git HEAD + raw porcelain status hash/dirty flag và
   exact selected component path/length/SHA. Harness/oracle/verifier source hashes
   được rederive từ entries, không tin base commit hay self-reported digest;
5. provenance cross-bind scenario/dataset, plan, machine, immutable metric registry,
   Runner executable/assembly, Contracts, harness, oracle, verifier assemblies và
   runtime inventory. Fresh verifier phải tự hash assembly đang chạy;
6. export exact canonical run-store plan gồm denominator + full intent grid. Public
   plan materializer, intent, terminal directory và run record phải one-to-one; exact
   solver component seed/runtime/config được so trước transcript. Bundle verifier dùng
   chung portable WP6-007 run verifier, không viết lại protocol logic nông hơn;
7. terminal failure/exclusion logs phải là exact detail union với một global gapless
   sequence. Metric stage yêu cầu production=oracle byte-exact, exact registry/window/
   succeeded-run coverage, bundle metric identity **và** production recomputation từ
   raw run/scenario evidence để correlated edit không qua;
8. builder pin/copy/recheck input, dùng per-destination lock/private staging/atomic
   rename và không overwrite existing/stale bag. External report luôn là new sidecar
   ngoài sealed bag hoặc artifact của derived bag.

**Evidence required:** deterministic two-root bundle equality; valid all-success và
mixed success/failure/exclusion; clean-process verifier/hash/sidecar; mutations tại
mọi ordered stage gồm missing/extra/tamper/length/type/path/traversal/case/reparse,
script, dirty source/provenance, scenario, grid/seed, transcript, terminal log,
oracle-only và correlated production+oracle; Release/format/full-solution gates.

**Consequences:** BagIt validity và RideBound semantic validity trở thành hai gate rõ
ràng; cả hai vẫn không chứng minh algorithm effectiveness, unbiased benchmark hoặc
independent reproduction. Đây là lần khóa đầu của bundle semantics; không đổi public
field/schema/registry hoặc failure rule nên umbrella contract giữ `1.0.3`. Source
evidence và Browser audit nằm trong research doc; claim enforcement tiếp tục ở 010.

**Supersedes / superseded by:** Hiện thực/làm rõ ADR-026 Decision 12 và ADR-030 bundle
handoff; không đổi WP1 protocol, WP3 commitment validator/certificate, O-001,
O-002/O-003/O-004 hay claim profile wording.

### ADR-032 — 2026-08-11 — Accepted

**Context:** `RB-WP6-009` chứng minh một bag đầy đủ và semantic-valid, nhưng checksum,
exact replay và same-team clean process vẫn có thể bị README/report/provenance gắn nhãn
`effective`, `production-ready`, `Results Reproduced` hoặc ACM badge. Quét substring
toàn repository sẽ vừa leak/false-positive raw trip/source prose, vừa tự bắt các câu
phủ định bắt buộc. Regex lowercase đơn giản lại bị né bằng punctuation, full-width,
default-ignorable và Greek/Cyrillic confusable. ACM version 1.1 yêu cầu team khác cho
Results Reproduced/Replicated; NASEM tách same-data/code reproducibility khỏi new-data
replication; Peng 2011 nói reproducibility không bảo đảm correctness/validity; Munafò
et al. 2017 cảnh báo HARKing/analytical flexibility/over-interpretation; Unicode UTS
#39 cung cấp anti-confusable mechanisms nhưng không phải general prose classifier.

**Decision:** Khóa artifact claim boundary sau:

1. `wp6-mechanical-only-v1` là canonical machine-readable profile được compile trong
   verifier và emit thành `data/provenance/claim-profile.json`. Profile ghi ADR,
   normalization ID, bounded surface size, evidence URI, exact scan selectors, caveat
   và forbidden/synonym rules; caller/CLI không có profile switch;
2. builder reserve/tự sinh cả profile và `data/claim-check.json`; payload caller không
   thể đưa kết quả `passed` giả. Profile SHA-256 được cross-bind trong
   `reproducibility.json`; required-field addition bump evidence shape nội bộ từ
   `1.0.0` lên `1.0.1`, public benchmark-contract/umbrella vẫn `1.0.3`;
3. phạm vi scan chỉ gồm README; manifest/plan identity, evidence, rule/resource labels;
   packaging report labels; machine `fileSystemType`/`powerModeNote`/
   `containerImageDigest`; repository `gitDirty`. Cấm quét run transcript, scenario,
   public trip/dataset, failure/metric rows hoặc source-code prose;
4. sáu caveat exact-once khóa mechanical/development, same-team clean-process,
   non-confirmatory, no effectiveness/non-inferiority/SLA/ACM/independent claim,
   local resource controls và absence of observed public-trip preference/satisfaction.
   Exact caveat spans được mask trước forbidden scan;
5. text matching bounded dùng NFKC, invariant casefold, diacritic removal, common
   Greek/Cyrillic confusable mapping và hai skeleton: punctuation-as-separator và
   punctuation-removed. Non-whitespace control/format/default-ignorable, private-use,
   surrogate/unassigned code point fail closed; không claim full UTS #39 conformance;
6. mỗi failure mang stable code, rule/category, relative path, selector, bounded
   original excerpt và normalized witness. Stage 10 dùng typed decoded fields để tái
   tính valid report byte-exact; forged report/profile hoặc consistently resealed
   claim mutation vẫn invalidates bundle;
7. future profile extension bắt buộc ADR + external evidence + source/profile/hash
   change. Không có CLI flag hay README wording để nâng claim ladder.

**Evidence required:** allowed exact wording; direct typed witness; forbidden
effectiveness/non-inferiority/SLA/production/novelty/satisfaction/ACM/reproduced/
replicated; case/punctuation/synonym/full-width/confusable/default-ignorable; missing
or duplicate caveat; report/provenance/profile/forged-check mutations sau khi reseal;
scoped-selection proof; deterministic two-bundle/fresh verifier; format/Release/full
solution gates.

**Consequences:** Claim checker là fail-closed artifact guard chứ không phải general NLP
hay bằng chứng scientific truth. Finite synonym profile giảm bypass trong export
surface đã khóa nhưng không cho phép suy luận rằng mọi prose bên ngoài repository đã
được kiểm. WP6 chỉ đạt mechanical same-team boundary; tiny/medium/adversarial gates và
independent team/main experiment vẫn chưa hoàn tất.

**Supersedes / superseded by:** Hiện thực và siết ADR-026 Decision 14/ADR-031 stage 10;
không đổi WP1 protocol, WP3 hard commitment semantics, O-001, O-002/O-003/O-004 hoặc
novelty boundary.

### ADR-033 — 2026-08-12 — Accepted

**Context:** `RB-WP6-001..010` đã có từng thành phần harness nhưng chưa chứng minh chúng
ghép thành một measurement path trung thực. Tiny draft đầu chỉ tạo ETA revision do
traffic và reject một request quá capacity, nên `decisionDelta` bằng zero; compiler
còn giả định đúng bốn batch. Plan từng hash một policy file trong khi Runner đọc cả
WP3 commitment config lẫn WP4 algorithm config. Derived `solver-rng` chưa có launch
contract nói rõ Runner phải lấy manifest seed. Production/oracle rows bằng nhau cũng
chưa tự chứng minh oracle process/binary/raw input tương ứng nếu thiếu execution
summary. Cuối cùng supervisor từng gắn mọi conversation failure vào stage `protocol`,
không thuộc failure taxonomy đã khóa. Các lỗ này có thể cho test xanh nhưng không cho
phép gọi là paired end-to-end reproduction.

**Decision:** Nâng umbrella contract lên `1.0.4` và khóa tiny E2E như sau:

1. fixture nguồn phải tự chứa lifecycle có thể chạy, không phát sinh decision bằng
   code test. Compiler chấp nhận 1–32 event batch và derive batch indexes, horizon,
   fleet/request/snapshot/event counts từ canonical fixture. Gate hiện tại dùng sáu
   epoch: accept incumbent, traffic projection + tight feasible insertion, capacity
   reject, confirm/board/drop/alight cả hai accepted request;
2. non-zero treatment witness phải là `decisionDelta`, không lấy exogenous traffic
   delta thay thế. Epoch 2 hiện có `prePickupInsertedStopCount=1`; exogenous pickup/
   drop ETA `+50/+150 ms` vẫn ghi riêng. Harness fail nếu bất kỳ measured run nào
   thiếu accept, complete, revision, reject/defer hoặc non-zero decision delta;
3. WP4 arm dùng `ridebound-wp4-policy-binding-v1` trên exact SHA-256 của cả WP3 và WP4
   config. Effective configuration tiếp tục bind policy/solver/budget/capability/
   launch contract. Per-run `solver-rng` int32 trở thành initialize master seed và
   Runner chỉ tiêu thụ qua pinned opt-in `--solver-seed-source manifest-master-seed`;
4. một run là một fresh exact external Runner process và isolated writable root.
   B1/C1 × ba measured repeat phải thành công trong mỗi hai clean harness process.
   So sánh exact plan/scenario/source/runtime/grid/transcript/decision/semantic metric
   và từng run input/output/index/decision/semantic metric; không đòi physical bundle
   hash giống nhau khi monotonic/resource evidence thật khác nhau;
5. independent oracle process emit canonical per-run summary bind oracle assembly,
   raw resource evidence, semantic evidence, row count và per-run metric-set hash.
   Strict bundle chỉ cho exact optional union một summary/successful run và stage 9
   tái tính toàn bộ; corrupted, missing trong partial union hoặc extra summary fail;
6. conversation failure map đúng taxonomy: negotiation/decision/parsing/completion/
   validation. Timeout/tree/resource/crash/postflight/incomplete/solver unknown cùng
   store/transcript/metric/bundle/selective-rerun mutations phải giữ typed terminal
   evidence, không đổi thành zero, drop hoặc selective success;
7. source audit tách model-mapping diagnostic: empty candidate set là “không có
   candidate khả thi”, duplicate ID là global identity collision. Hai invariant có
   regression riêng để benchmark diagnosis không che infeasibility.

Không đổi public JSON field hay bỏ failure code hiện hữu; `1.0.4` là backward-
compatible semantic/verification correction. Existing bundle không có oracle summary
vẫn được đọc; bundle đã cung cấp bất kỳ summary nào phải cung cấp exact complete union.

**Evidence required:** exact six-run conservation; non-zero decision witness trong raw
Runner output; two-clean-process semantic comparison; independent verifier sidecar;
claim report pass; oracle-summary tamper; timeout/crash/unknown/incomplete/input/
postflight/metric/missing/extra/tamper/selective-rerun mutations; format, Release
`-warnaserror` và required full solution.

**Consequences:** Bundle tại
`artifacts/wp6/tiny-paired-20260812-release/` là mechanical/development evidence của
same-team clean-process repeatability. Nó chứng minh harness chạy đúng và B1/C1 tạo
output khác theo semantics đã bind; nó không chứng minh C1 tốt hơn B1, không phải
FleetPy closed-loop effectiveness, independent reproduction, production SLA hay
confirmatory result. Full metric/bundle hash có resource samples nên không phải
semantic determinism key; semantic subset và provenance mới là cross-process gate.

**Supersedes / superseded by:** Làm rõ/hiện thực ADR-026 Decisions 5–14, ADR-028 launch
boundary, ADR-029 terminal conservation, ADR-030 metric oracle, ADR-031 strict bundle
và ADR-032 claim boundary. Không đổi WP1 protocol ordering, WP3 commitment validator/
certificate, O-001 hoặc tự chọn O-002/O-003/O-004.

### ADR-034 — 2026-08-12 — Accepted

**Context:** Medium derivative 128 request đã reproducible nhưng chưa có một exact
Runner conversation và paired bundle path. Draft đầu tái dùng WP3 boundary-test config
chỉ khai báo `uniform-v1`, trong khi public scenario ghi synthetic policy
`wp6-synthetic-policy-overlay-v1`; request vật lý khả thi vì thế trả no-op/defer
`FLEET_SELECTION_CONFLICT`. OR-Tools diagnostic 32 vehicle/34 objective level vẫn đạt
optimal trong budget, nên tăng budget hoặc nới validator sẽ che sai policy identity.
Một six-run attempt khác hoàn thành execution/oracle nhưng bundle preflight phát hiện
source entity IDs chưa sorted. Ngoài ra medium run chứa real resource samples nên yêu
cầu physical bundle hash giống nhau giữa process là sai determinism domain.

**Decision:** Nâng umbrella contract lên `1.0.5` và khóa:

1. medium compiler bind exact verified descriptor/scenario/report/manifest chain và
   exact 128 request, 32 vehicle, 96 node, 9.120 directed arc; 21.400 input phải bảo
   toàn thành eligible/excluded/selected, không raw-row loss hoặc fabricated arc;
2. Runner dùng dedicated commitment config khai báo đúng synthetic policy ID cùng WP4
   config qua existing composite binding. Policy mismatch fail closed; không đổi data,
   solver result hay validator để biến nó thành success;
3. driver `wp6-public-derivative-instant-drain-driver-v1` kiểm exact capability/init,
   decision context/hash/ACK/checkpoint, request/vehicle/candidate/plan/suffix binding
   và lifecycle. Historical frozen prefix chỉ hợp lệ khi executed count exact và không
   chứa request mới; drain decision không được emit allocation action;
4. instant drain ở cùng source timestamp chỉ là nonphysical state-machine mechanics.
   Zero wait/ride không được aggregate/rank/report thành effectiveness, service,
   fairness, satisfaction, non-inferiority hay SLA; WP7 sở hữu simulator semantics;
5. source claims và artifact pins phải unique/sorted ordinal ở early preflight trước
   expensive run. Attempt fail preflight không có receipt và không được gọi pass;
6. B1/C1 × ba measured repeat chạy trong mỗi hai fresh Release harness process. Exact
   semantic domains gồm plan/scenario/source/runtime/grid/transcript/decision/semantic
   metric và từng run input/output/index/decision/semantic rows. Monotonic resource
   samples, full metric/logical/physical bundle hashes được phép khác nhưng phải complete,
   provenance-bound và được external verifier xác nhận;
7. exact-Runner medium test giữ CPU regression ceiling 120 giây. Wall ceiling 180 giây
   cho full-solution scheduler contention; một 120-second wall failure phải ghi là test
   resource timeout, không được báo thành WAC hay algorithm pass.

**Evidence required:** publisher length/MD5 + local SHA/license; two-clean-root exact
normalization; request/exclusion conservation; exact Runner lifecycle; 6/6 × hai fresh
process; per-run semantic comparison; oracle/strict external verifier; rejected
unsorted provenance attempt; mechanical-only claim report; format và required full
solution.

**Evidence:**
[wp6-012-public-medium-evidence-2026-08-12.md](benchmarking/wp6-012-public-medium-evidence-2026-08-12.md)
ghi commands, identities, bundles B/C, fail-closed attempt A và claim caveat. Required
`dotnet test RideBound.slnx` cuối đạt 710/710; Contracts/Runner nạp/chạy và WAC
`0x800711C7` không tái hiện.

**Consequences:** WP6 có medium public-source harness evidence nhưng chưa có FleetPy
closed-loop control, vehicle motion hoặc physical KPI. B1/C1 cùng accept/complete mọi
request dưới instant-drain không chứng minh hai arm tương đương hay C1 tốt hơn. Mọi
effectiveness experiment vẫn phải chờ WP7 adapter và WP8 preregistration.

**Supersedes / superseded by:** Làm rõ ADR-026 medium gate và mở rộng ADR-033 generic
paired harness semantics; không đổi WP1 protocol, WP3 publication validator, WP4 solver
objective, O-001 hoặc O-002/O-003/O-004.

### ADR-035 — 2026-08-12 — Accepted

**Context:** Source/adversarial audit của `RB-WP6-013` phát hiện năm khoảng trống mà
bundle 012 vẫn có thể che: plan generic khai báo không warm-up; preflight message nói
claims/pins/sources sorted nhưng chỉ claims được kiểm; medium policy mismatch còn được
phát hiện sau expensive Runner work; conversation driver có thể phát code ngoài failure
taxonomy; terminal conservation hard-code sáu thay vì lấy compiled grid. Khi warm-up
được bật, literal sáu lập tức làm tiny harness fail, chứng minh đây là lỗi logic chứ
không phải bổ sung test trang trí. Resource evidence D/E cũng xác nhận semantic hash
phải ổn định trong khi full sampled-resource hash phải được phép khác.

**Decision:** Nâng umbrella contract lên `1.0.6` và khóa:

1. paired mechanical gate materialize một warm-up và ba measured repeat mỗi B1/C1;
   từng run có process/root/output riêng, repeat index collision-free, không cache/state
   donation. Conservation dùng exact `compiled.PlannedRuns.Count`;
2. claims, absolute pins và bundle sources phải non-empty, ordinal-sorted và
   case-insensitively unique; relative destination/media type cũng fail-closed tại
   preflight trước Runner execution;
3. public scenario phải derive đúng một commitment policy ID và exact Runner-visible
   config phải khai báo policy đó. Empty/multiple/mismatch fail trước run đầu tiên;
4. conversation chỉ được phát năm code do boundary sở hữu ở negotiation/decision/
   parsing/completion/validation. Code khác canonicalize thành
   `protocol.invalid-output/parsing`, không persist arbitrary taxonomy/stage;
5. required-mutant gate gồm 10 document nested permutation + parallel decode, plan/
   seed/order parallelism, đủ 21 failure/stage và 8 pre-outcome exclusion rules,
   actual supervisor/resource branches, store write/race/log boundaries, metric/
   bundle/claim mutations và exact source nondeterminism audit. `100% killed` chỉ nói
   về declared matrix này, không phải general mutation score;
6. cross-process equality dùng scenario/plan/source/runtime/grid/transcript/decision/
   semantic metric và per-run semantic hashes. UTC/monotonic resource, full metric,
   logical/physical bundle containing those samples được phép khác nhưng phải complete,
   provenance-bound và externally verified;
7. giữ mọi raw resource stratum, kể cả treatment chậm và resource-control failure.
   Không dùng local instant-drain rows làm effectiveness, non-inferiority, production
   latency/throughput hoặc SLA evidence.

Không public schema field/failure code nào bị thêm/bỏ; đây là backward-compatible
semantic/verification correction. WP3 validator/certificate và WP4 objective không
bị nới, public data không đổi, O-001/O-002/O-003/O-004 vẫn khóa.

**Evidence:** Hai fresh medium D/E đều 8/8 success và strict external verify valid;
13/13 top-level semantic fields cùng 8/8 per-run semantic records exact, trong khi
8/8 full resource row hashes khác đúng contract. C1 wall/CPU lớn hơn B1 trong 6/6
local measured pair được giữ như diagnostic âm. Declared mutation matrix pass; Release
0 warning/error, format/dependency/schema/link gates sạch và required exact full
solution cuối 770/770. Một run trước 769/770 do medium CPU control được ghi riêng,
standalone rồi exact rerun pass; WAC không tái hiện. Chi tiết tại
[wp6-013-adversarial-closure-evidence-2026-08-12.md](benchmarking/wp6-013-adversarial-closure-evidence-2026-08-12.md).

**Consequences:** `RB-WP6-013` Done và `RB-WP6-014` là ticket duy nhất In progress.
WP6 chưa đóng cho đến khi source/claim audit toàn WP1–WP6, toàn bộ Markdown, E2E/
artifact verifier và final Vietnamese review folder có evidence đầy đủ.

**Supersedes / superseded by:** Làm rõ ADR-026 determinism/resource/failure boundary,
ADR-033 generic paired harness và ADR-034 public medium gate; không supersede WP1
protocol, WP3 publication boundary, WP4 algorithm semantics hoặc claim profile.

### ADR-036 — 2026-08-13 — Accepted

**Context:** `RB-WP6-014` phải quyết định WP6 có thể đóng dựa trên source/logic và
fresh artifact evidence hay chỉ đang xanh do test. Audit cũng phải phân biệt source
provenance change với nondeterminism: medium D/E lịch sử không có cùng source inventory
với working tree closure hiện hành.

**Decision:** Đóng WP6 với các điều kiện:

1. review source theo chuỗi authority thực, không dùng test count làm kết luận;
2. WP3 full-fleet physical/commitment validator và ACK-only commit giữ nguyên; WP4
   heuristic/solver/filter không được tự cấp publication certificate;
3. WP5 chỉ orchestration/persistence và phải gọi pinned Runner; WP6 chỉ external
   supervision/measurement, không reference core decision path;
4. fresh repeat pair phải dùng cùng exact source inventory. So khác source inventory
   là provenance difference, không phải deterministic failure;
5. semantic equality bao gồm top-level và từng run input/output/index/decision/metric;
   sampled resource/full/bundle hash được phép khác nhưng phải complete/external-verify;
6. paper speed-up/sparse/multiple-plan result chỉ vào code sau claim-boundary,
   deterministic loss/oracle và hard revalidation evidence; closure không tự thêm;
7. final handoff phải giải thích logic/code/file/paper/risk/reproduction bằng tiếng
   Việt và giữ mọi negative result/caveat;
8. WP7 giữ Not Started; WP6 không cấp effectiveness, SLA, non-inferiority, fairness
   hay satisfaction claim.

**Evidence:** Fresh tiny A 8/8, bundle
`79cb321a2aa079c34ddfa49061387e78990f14b7bb368abb762e497c30b27b04` valid.
Medium H/I trên exact source cuối đều 8/8; 16 top-level field + 72 per-run semantic
field có 0 mismatch, 8/8 full resource row khác hợp lệ; bundles
`89a43921e46f57cfc47d9fcb0d63f8f18f58087a1f54d29fe65c7fecc4d6d9d8` và
`a954db621758a6404fba988a491f9f4575add45a771f0b852ce7ab7cd95494e9`
đều fresh-process verify. Exact full solution 770/770; Contracts/Runner load và WAC
không tái hiện. Evidence chi tiết ở
[wp6-014-source-claim-closure-evidence-2026-08-13.md](benchmarking/wp6-014-source-claim-closure-evidence-2026-08-13.md)
và [review WP1–WP6](reviews/wp1-wp6-final/README.md).

**Consequences:** `RB-WP6-001..014` Done, WP6 Complete. Không có ticket active;
WP7 chỉ được mở bằng refinement explicit. D/E, F/G và mọi artifact lỗi/âm vẫn được giữ.

**Supersedes / superseded by:** Đóng exit gate ADR-026 và các amendment ADR-027..035;
không thay protocol WP1, state/physical WP2, publication WP3, algorithm WP4, durable
boundary WP5 hoặc các open decisions O-002/O-003/O-004/O-006/O-008.

### ADR-037 — 2026-08-13 — Accepted

**Context:** Refinement WP7 phải giải quyết hai rủi ro trước khi viết adapter. Thứ
nhất, Candidate cap cũ xếp từng route variant riêng lẻ, nên nhiều variant cùng exact
service set có thể chiếm cap và làm mất một service set hữu ích ở fleet selection;
C1 còn có thể mất variant ít phá incumbent trước hard gate. Thứ hai, FleetPy adapter
không được suy đoán callback, edge progress hoặc vượt locked leg chỉ vì unit test giả
pass. Nghiên cứu lại Alonso-Mora, Engelhardt et al. và Zalesak et al. cho thấy các
RV/subset/random-direction filter có speed/quality trade-off hoặc đòi giả định không
được bảo đảm bởi arbitrary directed sparse RideBound snapshot.

**Decision:**

1. thêm config-bound `CandidateRetentionStrategy`; old WP1–WP6 config thiếu field
   phải parse thành `LegacyAcceptedCountCostSlack`, giữ behavior và content hash cũ;
2. strategy opt-in `ServiceSetStabilityPortfolioV1` chạy riêng từng accepted-count
   tier: giữ cheapest anchor cho mọi exact service set, rồi stability anchor theo
   unchanged incumbent prefix, inserted-before-incumbent-pickup và integer service-
   start shifts, cuối cùng fill bằng legacy rank;
3. no-op luôn được giữ; retained/omitted count và digest vẫn exact. Candidate filter
   không cấp publication authority: WP3 full-fleet physical/commitment validator vẫn
   revalidate trước decision;
4. chỉ nhận strategy mới sau bounded evidence: per-set B1 cost dominance tại mọi cap,
   fleet strict-positive, C1 real-validator/no-regression và permutation/conservation;
   đây không phải chứng minh tối ưu phổ quát hay effectiveness;
5. reject random request/vehicle order, random direction, forecast/reassignment hoặc
   paper-only pruning khỏi default; O-001 và O-002/O-003/O-004 không mở;
6. pin FleetPy tag `1.0.2`, annotated tag object
   `ca5a245243094236c84a0e93b32819ee502beeff`, commit
   `053aa9d4fcfde91c5d303435d5748f9206c071b0`, MIT và source/env hashes ngoài vendor;
7. executable probe phải fail trước adapter import khi commit/tag/dirty/source/env
   drift; probe actual abstract callbacks, position round-trip,
   `SimulationVehicle._move` mutation và non-forced assignment default;
8. khóa directed position model `(startNode,endNode,relativeProgress)` với node form
   `(node,None,None)` và `relativeProgress ∈ [0,1]`. Không có capability thì fail/
   named downgrade; không suy diễn từ clock;
9. FleetPy suffix mapping phải giữ exact active locked leg, không bao giờ gọi
   `force_assign=True`, và chỉ gọi cùng pinned external RideBound Runner; Python không
   port/reimplement Candidate, validator hoặc policy.

**Evidence:** Candidate tests chứng minh per-service-set anchor dominance, exact fleet
adversarial cùng cap tăng accepted `2 → 4`, stability anchor sống qua hard gate và
32 exact-small seeds qua real assessor/production policy không có substantive C1
regression, có strict-positive witness; permutation/loss/config compatibility đều
pass. Algorithm 141/141, Runner 73/73 và exact full solution 776/776. FleetPy probe
trên external clean checkout kiểm đúng annotated tag/commit và six critical source
hashes, Python 3.10.20/package lock, 13 abstract callbacks, position
`(11,12,0.375)` round-trip, `_move` đổi thành `(11,12,0.625)`, và
`force_assign=False` được truyền vào `force_ignore_lock`; bốn drift mutation test
fail đúng typed code.

**Consequences:** `RB-WP7-001..003` Done; `RB-WP7-004` là ticket duy nhất Ready.
Strategy mới là opt-in cho config WP7/future experiment; published WP4/WP6 config
không bị đổi. WP7 vẫn chỉ mechanical Layer-2 cho đến actual preflight/tiny/medium
closed loop; chưa có effectiveness, SLA, non-inferiority, fairness hay novelty claim.

**Supersedes / superseded by:** Đóng O-006 và mở ordered WP7 queue sau ADR-036;
không supersede WP1 protocol, WP2 physical state, WP3 publication gate, WP4 objective,
WP5 durable boundary, WP6 measurement contract hoặc open decisions còn lại.

### ADR-038 — 2026-08-16 — Accepted

**Context:** Sau ADR-037, WP7 phải đóng bằng current-source evidence thay vì chỉ unit
test hoặc một preflight giả. Audit Candidate tìm thấy một lỗi ranking thật: B4 repair
root có thể bị xếp theo slack của route chưa repair dưới work cap. Adapter FleetPy cũng
phải chứng minh actual callbacks, directed position, locked plan và process restart qua
cùng Runner binary ở B1/C1.

**Decision:**

1. Sửa B4 root priority để mọi repair seed xếp theo mutable suffix của chính route đã
   repair; regression ghi việc projection này xảy ra trước khi one-work budget chọn root.
2. Giữ portfolio là opt-in. B1 proof chỉ là substitution cùng vehicle/exact service set:
   cost anchor không đắt hơn và có cùng conflict columns; nó không bảo toàn CandidateId
   tie-break, không là global optimum và không là định lý C1 phổ quát.
3. Khi opt-in cap thực sự áp dụng, một candidate non-no-op phải giữ nguyên mọi no-op
   incumbent stop và tập stop request mới phải đúng `NewRequestIds`. Nhờ vậy label
   service set không thể che route khác nghĩa.
4. Python adapter chỉ map FleetPy và gọi external published Runner v6; không có core
   algorithm Python, không `force_assign=True`, không invented reverse/zero arc và
   locked/current leg mismatch fail typed.
5. Actual evidence bắt buộc dùng FleetPy 1.0.2 clean pin + CPython lock, Runner preflight,
   FleetControl preflight/lifecycle, two-repeat tiny clock và three-repeat public-medium
   B1/C1. Medium bundle phải qua verifier độc lập từ transcript/manifest.
6. Giữ caveat upstream FleetPy future-ABC warning và các raw resource/negative records.
   Không suppress warning, nâng CPU limit, hoặc suy diễn effectiveness/performance từ
   publication count, wall time hay semantic hash khác arm.

**Evidence:** Published Runner v6 SHA-256
`8a227fcd44e2c8e9814821bce317ea07f59c6fe9766dd26b6b8533a8129b75a2`; external
evidence root `E:\\RideBoundData\\wp7\\results\\candidate-portfolio-v6-20260815`;
hard-vector medium manifest `e8f03b56137d9ca54ebeef802cb5c3da0e3cab600c73c08ce42a4c13ae41274e`
and rolling-cost manifest `829eb76645a4c751af5a3bf25f298ed9608ac320351a1713a054b43c9838689f`.
Full .NET suite passes `790/790`; pinned Python adapter suite passes `49/49` without
skip; WAC did not recur. Exact receipts and claim boundary are in
`benchmarking/wp7-014-fleetpy-layer2-closure-evidence-2026-08-15.md`.

**Consequences:** `RB-WP7-001..014` Done and WP7 Complete for mechanical Layer-2
scope. WP8 is not started and needs an explicit refinement/preregistration decision.
No effectiveness, SLA, non-inferiority, fairness, satisfaction, novelty, independent
reproduction or global-optimality claim is authorized.

**Supersedes / superseded by:** Supersedes the open queue state of ADR-037; retains its
pin, claim boundary and all earlier protocol/state/commitment/solver decisions.

### ADR-039 — 2026-08-17 — Accepted

**Context:** Một audit source sau ADR-038 tìm thấy hai loại nợ. Thứ nhất, một nhóm
thay đổi ngữ nghĩa thật đã vào source mà **không có ADR nào khóa**: thời điểm mở
promise (`initialPromiseTrigger`), baseline của lock evaluator, cách reducer xử lý
`OfferDeclined` sau khi đã accept, một failure fail-closed mới ở C1, hai CLI flag mới
của Runner và việc phát plan update do event gây ra. Thứ hai, cổng benchmark medium
từng chạm trần CPU và nguyên nhân chưa từng được **đo**, chỉ được suy đoán. Ngoài ra
mọi receipt tài liệu vẫn ghi `790/790` trong khi source đã đi tiếp.

**Decision:**

1. Khóa `initialPromiseTrigger` là một thuộc tính config có hai giá trị hợp lệ.
   `initial-acceptance` giữ nguyên ngữ nghĩa WP1–WP6: promise mở khi request được
   accept. `booking-confirmation` mô hình hóa Layer-2 thật, nơi assignment chỉ là một
   **offer provisional**: nó được kiểm tra vật lý đầy đủ nhưng **không** tạo promise,
   và promise chỉ mở tại chuyển trạng thái `Accepted → WaitingPickup/Onboard` với
   reason code `INITIAL_BOOKING_CONFIRMATION`. Config thiếu field phải parse thành
   `initial-acceptance`, giữ nguyên content hash cũ.
2. `CommitmentLockEvaluator` so candidate với **exogenous projection**, còn published
   promise trước đó chỉ xác định horizon đang bị khóa. Traffic drift vì thế được ghi
   nhận nhưng không bị báo sai thành vi phạm lock. Đây là áp dụng đúng three-way delta
   đã khóa ở ADR-021, không phải nới lỏng lock.
3. `OfferDeclined` trên một request đang `Accepted` là `CancelAfterAcceptance`, không
   phải `RejectRequest`; từ chối một offer đã được cấp không được ghi thành chưa từng
   được phục vụ.
4. `C1_VEHICLE_HAS_NO_FEASIBLE_CANDIDATE` là fail-closed bắt buộc: nếu treatment loại
   hết mọi candidate của một vehicle — kể cả no-op an toàn — run phải dừng với witness
   **typed**, không được giao cho solver một vehicle không có lựa chọn nào. Witness
   mang `requestId`, `dimension`, `underlyingCode`, `before`, `after` và số lượng
   generated/rejected ở trường riêng, không nhồi vào prose.
5. Runner nhận `--maximum-line-bytes` (bounded `1 MiB..64 MiB`) và
   `--manifest-solver-seed`; plan update do event gây ra chỉ được phát khi caller bật
   tường minh và route thực sự khác route trước đó.
6. Tối ưu hiệu năng chỉ được chấp nhận khi **không đổi một byte kết quả nào** và được
   chứng minh bằng counter, không bằng đồng hồ. Khóa memo slack chuyển sang so sánh
   cấu trúc chính xác thay cho fingerprint SHA-256; framing identity ghi thẳng UTF-8;
   `RoutePlan.Create` phát hiện stop trùng bằng đếm hai lượt và báo đúng key cũ;
   StableId của search node và route đã chiếu được tính lười; generator rank một lần
   rồi đưa thứ tự cho retainer kèm kiểm tra fail-closed. `CandidateSearchWorkProfileTests`
   khóa chính xác work unit, evaluated path, feasible-before-cap, omitted path, retained
   count và số profile slack riêng biệt.
7. Semantic hash của actual FleetPy **không so sánh được giữa hai Runner artifact**, vì
   `RunManifestIdentity` cố ý bind `binarySha256`. Differential giữa hai binary phải so
   các trường hành vi, không so hash tổng hợp.

**Evidence:** Micro-harness xác định hot path không phải validator (`0,72 µs/route`)
mà là khóa memo slack (`19,63 µs/lookup`, ~39.000 lookup mỗi `Generate`). Sau thay đổi:
`0,64 µs/lookup`. Wall time mỗi `Generate` (min của ba lần chạy) giảm
`25,3 → 16,6 ms` (suffix 4), `220 → 170 ms` (suffix 8), `897 → 587 ms` (suffix 12),
`1662 → 1018 ms` (suffix 16); mọi counter giữ nguyên tuyệt đối. Benchmark medium
mechanical hoàn tất trong `1 m 50 s` dưới trần CPU 120 giây.
Required `dotnet test RideBound.slnx` pass **798/798**, 0 failed, 0 skipped:
Contracts 135, Domain 136, Application 73, Algorithms 154, Runner 77, Benchmarking 135,
Benchmarking.Contracts 71, OrTools 7, Architecture 10. `dotnet format --verify-no-changes`
sạch; Release `-warnaserror` 0 warning/0 error. Pinned Python adapter suite pass
**50/50** không skip.
Runner v8 SHA-256 `13bf5d9b1dfbcb677d2d64c24038dba2c9adc22e664d2a6adecbf1905dcc179e`
tại `E:\\RideBoundData\\wp7\\runner\\candidate-portfolio-v8-identity-hotpath`; evidence root
`E:\\RideBoundData\\wp7\\results\\candidate-portfolio-v8-identity-hotpath-20260817`.
Differential v7↔v8 trên tiny clock với label giống hệt: publication, `requestState`,
`travelSnapshotVersion`, `vehiclePosition`, `nextEpoch`, `nextEventSeq` và
`exactPhysicalBoundaryDrainCount` **giống hệt**; chỉ `manifestHash`/`checkpointBindingHash`
lệch, đúng bằng thiết kế bind binary.

**Consequences:** Mọi receipt `790/790` trong tài liệu được thay bằng `798/798` và mọi
receipt actual được gắn với Runner v8. WP7 vẫn Complete cho phạm vi mechanical Layer-2.
Không có effectiveness, SLA, non-inferiority, fairness, satisfaction, novelty hay
global-optimality claim nào được cấp. Đề xuất *lazy priority* bị **bác bỏ** và ghi lại
như kết quả âm. Constant-time feasibility test của Gschwind–Drexl 2019 được ghi nhận là
hướng WP8 nhưng **chưa áp dụng**, vì full text chưa đọc được tại ngày kiểm tra.

**Supersedes / superseded by:** Bổ sung, không thay thế, ADR-037/038. Không đụng tới
WP1 protocol, WP2 physical state, WP3 publication gate, WP4 objective, WP5 durable
boundary hay WP6 measurement contract.

### ADR-040 — 2026-08-18 — Accepted

**Context:** Hết WP7, RideBound có một đường so sánh cơ học đã công bằng — hai arm dùng
chung scenario, seed, Runner binary, work budget, candidate pool và đường publication,
và hai config chỉ khác đúng `policyId`. Cái chưa có là một **thí nghiệm**: chỉ một hiện
thực nhu cầu, chưa preregister, và cơ chế đắt nhất của treatment chưa thực sự bị ràng
buộc. WP8 phải quyết thí nghiệm trông thế nào rồi khoá lại, trước khi nhìn bất kỳ con
số nào.

**Decision:**

1. Grid scenario dựng **hoàn toàn từ dữ liệu công khai thật**. Bộ FleetPy Manhattan v1
   đã xác minh có 289 file demand trên 8 ngày liên tiếp, kèm sample fraction chính thức,
   tỷ lệ đặt trước và hệ số traffic theo ngày/giờ. Không sinh demand tự chế.
   `tools/RideBound.Wp6Normalize` chuyển từ hai profile hard-code sang chạy theo một
   grid manifest source-controlled; `FleetPyNormalizationConfiguration` vốn đã nhận đủ
   tham số nên đây là mở rộng CLI, không phải viết lại normalizer.
2. **Pilot** là `2018-11-11` và `2018-11-12`; **confirmatory holdout** là `2018-11-13`
   → `2018-11-18` và bị niêm phong cho tới khi preregistration đóng băng. `2018-11-12`
   đúng là ngày WP6/WP7 đã dùng, nên toàn bộ phần dữ liệu đã bị nhìn nằm gọn trong
   pilot; điều này phải được ghi trong preregistration.
3. Đơn vị thí nghiệm là `(scenario, seed, travel realization)`. Rider được gộp lên mức
   run trước khi bootstrap; cấm coi rider trong cùng run là mẫu độc lập.
4. Primary endpoint là `p95_decision_pickup_eta_total_variation_ms`, so paired
   difference `C1 − B1`. Calculator production phải khớp byte-exact với một oracle
   BCL-only không ProjectReference, theo đúng chuẩn đã dùng ở `RB-WP6-008`.
5. Commitment budget được **suy từ phân phối thực nghiệm trên chỉ dữ liệu pilot**, khoá
   thành ba mức strictness, giữ cấu hình unbounded hiện tại làm tầng đối chứng. Quy tắc
   suy dẫn phải khai báo trước khi đọc dữ liệu. Lý do: ở cấu hình hiện tại chỉ 3/10
   chiều có `hardLimit` và cả ba bằng 0, lại luôn thoả sẵn do O-001, nên phần hard
   vector của C1 gần như không ràng buộc gì và treatment thực chất chỉ đang được kiểm ở
   phần thứ tự revision.
6. Non-inferiority margin phải đặt trước, dùng cận một phía, và neo vào chính sách suy
   biến "từ chối hết yêu cầu" — chính sách đó có revision `= 0` và service rate `= 0`,
   nên margin phải đủ hẹp để treatment không thể thắng endpoint chính bằng cách hạ dịch
   vụ. Ghi rõ rằng có margin **không** tự bảo vệ khỏi degradation.
7. Sample size suy từ phương sai pilot và một minimum detectable effect có nghĩa vận
   hành, cộng dự phòng failed run. Cấm chọn số tròn.
8. Partition `planned = succeeded + failed + excluded` giữ nguyên; một primary endpoint,
   một non-inferiority gate, Holm cho key secondary family; exploratory phải ghi rõ.
9. Preregistration file đủ 15 mục của `docs/11` §11, được canonical hash và đóng băng;
   sau freeze chỉ một ADR mới sửa được.
10. WP8 **không** công bố bất kỳ kết quả effectiveness nào. Nó chỉ tạo ra một thí nghiệm
    đã khoá.

**Evidence:** Kiểm kê dataset đã xác minh (289 file demand, 8 ngày `2018-11-11` →
`2018-11-18`, sample/res fractions, `tt_factors` theo ngày và giờ) và xác nhận
`FleetPyNormalizationConfiguration` đã tham số hoá đủ day/window/selection-key/fleet/
window-tightness. Phương pháp non-inferiority đọc full text từ *Non-inferiority
statistics and equivalence studies* (PMC7808096) và *Choice of NI margins does not
protect against degradation* (PMC4117500); guideline EMA CHMP được ghi nhận là nguồn
quy chuẩn nhưng PDF không đọc được dạng text tại ngày kiểm tra nên không trích nội dung.
Nền cơ học kế thừa từ ADR-039: required suite `798/798`, Python `50/50`, và actual
FleetPy B1/C1 trên cây đóng băng `d48b115b` với verifier độc lập pass cho cả hai bundle.

**Consequences:** Tạo `docs/tasks/37` và `docs/tasks/38`; `RB-WP8-002` là ticket Ready
duy nhất. Chưa có production code WP8, chưa có scenario confirmatory nào được sinh, và
không claim nào được nâng. `O-001`/`O-002`/`O-003`/`O-004` vẫn đóng.

**Supersedes / superseded by:** Không supersede ADR nào. Nó mở WP8 sau ADR-039 và kế
thừa nguyên vẹn claim boundary của WP6/WP7.

### ADR-041 — 2026-08-19 — Accepted

**Context:** Pilot WP8 chạy thật trên dữ liệu công khai đã xác minh và buộc phải sửa ba
điểm trong thiết kế mà ADR-040 khoá. Cả ba đều phát hiện từ cơ chế hoặc từ chính pilot,
trước khi bất kỳ ngày confirmatory nào được sinh.

**Decision:**

1. **Điểm vận hành phải có tranh chấp.** Ở điểm WP6/WP7 (128 request rải 24 giờ, 32 xe),
   decision-induced delta bằng 0 cho mọi rider ở cả hai arm; exogenous bằng đúng visible.
   Primary endpoint khi đó bằng 0 đồng nhất và không cỡ mẫu nào cứu được. Điểm vận hành
   preregister là cửa sổ cao điểm thật `08:00–10:00` với 8 xe; chỉ cửa sổ và số xe thay
   đổi, dùng cấu trúc sẵn có của dữ liệu chứ không bịa tải.
2. **Primary endpoint là tổng decision-induced burden liên chiều**, không phải riêng
   pickup ETA. Pickup-ETA zero-inflated tới mức `p50 = p90 = 0` ở mọi run, nên `p95` treo
   trên 5–6 rider trong khoảng 110. Ghi rõ: đây **không** phải hằng đẳng thức cơ chế —
   `C-d20181112-r2-c1` có `prePickupInsertedStopCount = 3` mà pickup delta vẫn bằng 0 vì
   chèn vào slack sẵn có; lý do loại endpoint là tính giòn, không phải tính tất định.
3. **Service rate là cổng đồng thời**, không phải secondary metric, và phải đo ở mốc hoàn
   thành chuyến chứ không phải mốc được hứa.
4. Harness: cardinality do driver khai báo thay vì hard-code; trần frame Runner dùng đúng
   ceiling `64 MiB` mà ADR-039 đã khai, vì full-state checkpoint của kịch bản dày vượt
   `16 MiB`. Trần này là guard tài nguyên, không phải bất biến đúng đắn, và vượt trần là
   lỗi typed fail-closed.

**Evidence:** Normalizer chạy theo grid manifest sinh derivative từ dữ liệu công khai
thật; mỗi cell bảo toàn đủ bản ghi nguồn (ví dụ `21.400 → 128, 0 loại`). Bốn đơn vị
paired ở điểm có tải, hai ngày, sample replicate chính thức của publisher: Δ burden âm ở
cả bốn, median `−3.130.086 ms`, sd `1.128.334 ms`, giảm 75–93%. Δ tỷ lệ hoàn thành
`−6,25 / −5,47 / −0,78 / 0,00` điểm phần trăm, mean `−3,13 pp`, sd `3,19 pp`. Chi tiết và
giới hạn claim ở
`benchmarking/wp8-001-pilot-operating-point-evidence-2026-08-19.md`.

**Consequences:** Theo đúng tiêu chí `docs/11` §5/§14 mà dự án tự đặt trước, **C1 ở cấu
hình hiện tại không vượt cổng service ở điểm vận hành này**: thiếu hụt dịch vụ trung bình
gấp khoảng ba lần margin minh hoạ `1` điểm phần trăm. Đây là kết quả **pilot**, n = 4,
không có khoảng tin cậy và không phải effectiveness claim. Confirmatory holdout
`2018-11-14` → `2018-11-18` chưa được sinh và chưa bị chạm. `RB-WP8-002`/`003` Done;
`RB-WP8-004` là ticket Ready tiếp theo, và nó phải xử lý đánh đổi dịch vụ trước khi bàn
tới cỡ mẫu.

**Supersedes / superseded by:** Sửa ba quyết định thiết kế trong ADR-040; giữ nguyên phần
còn lại của ADR-040 và toàn bộ claim boundary WP6/WP7.

### ADR-042 — 2026-08-19 — Superseded

> Superseded bởi ADR-043. Giữ nguyên làm lịch sử phát hiện sai: unit đã gắn
> `masterSeed`, `N=62` không tồn tại trong holdout, và câu “toàn bộ khớp đúng” vượt
> quá bằng chứng review. Không được dùng ADR-042 làm source of truth hiện hành.

**Context:** Thực hiện các ticket tiếp theo của WP8 (`RB-WP8-004` đến `RB-WP8-007`) và tiến hành kiểm toán toàn diện mã nguồn/thuật toán từ WP1 đến WP7 theo đối chiếu văn hiến khoa học.

**Decision:**
1. **Khóa hợp đồng đơn vị thí nghiệm (`RB-WP8-004`):** Đơn vị thí nghiệm được định nghĩa chính xác là $u = (\text{scenarioHash}, \text{masterSeed}, \text{travelRealizationHash})$. Toàn bộ quan sát rider-level phải được gộp (aggregate) lên mức run trước khi thực hiện so sánh paired difference hay bootstrap (`docs/11` §1, §7). Cấm tuyệt đối coi các rider trong cùng run là mẫu độc lập.
2. **Calculator & Standalone Oracle cho Primary Endpoint (`RB-WP8-005`):** Khóa công thức đo lường `total_decision_induced_burden_ms` ($\Delta^{\text{pickETA}}_{\text{decision}} + \Delta^{\text{dropETA}}_{\text{decision}}$) cùng tỷ lệ hoàn thành chuyến (`completed_service_rate`), kèm theo triển khai Oracle BCL-only độc lập (`DecisionInducedBurdenOracle`) đối chiếu byte-for-byte.
3. **Phân phối thực nghiệm Pilot (`RB-WP8-006`):** Trích xuất phân phối gánh nặng thực nghiệm của B1 trên 4 đơn vị pilot tại điểm vận hành có tranh chấp (Manhattan Zenodo $08:00–10:00$, 8 xe). Trung bình B1 tạo ra $3.692.927\text{ ms}$ gánh nặng toàn mạng ($87,2\%$ ở drop ETA). C1 giảm gánh nặng $83,7\%$ nhưng làm giảm tỷ lệ hoàn thành $3,125\text{ pp}$.
4. **Power Analysis & Cỡ mẫu (`RB-WP8-007`):** Dựa trên $\sigma_{\Delta \text{burden}} = 1.128.334\text{ ms}$ và $\sigma_{\Delta \text{service}} = 3,189\text{ pp}$, tính toán cỡ mẫu yêu cầu cho giai đoạn Confirmatory là $N = 62\text{ đơn vị paired}$ (kèm $10\%$ dự phòng).
5. **Kiểm toán mã nguồn WP1–WP7:** Đã rà soát chi tiết toàn bộ các tầng Domain, Application, Algorithms, Solvers, Runner, Benchmarking và FleetPy adapter. Tất cả bất biến trạng thái, luật bảo toàn, cơ chế cắt tỉa và giải thuật lexicographic CP-SAT đều khớp đúng lý thuyết và văn hiến chuẩn mực.

**Evidence:**
- `docs/benchmarking/wp8-006-pilot-empirical-distributions-2026-08-19.md`
- `docs/benchmarking/wp8-007-power-and-sample-size-report-2026-08-19.md`
- Toàn bộ test suite pass 100%.

**Consequences:** `RB-WP8-004`, `005`, `006`, `007` chuyển sang **Done**. `RB-WP8-008` (Frontier B1 $\rightarrow$ C2 $\rightarrow$ C1) trở thành ticket **Ready** duy nhất.

### ADR-043 — 2026-08-21 — Accepted

**Context:** Tiếp tục handoff WP8, hoàn tất frontier và review adversarial trước khi
chạm outcome confirmatory. Review xác nhận bốn blocker F1/F7/F9/F10/F26 là thật,
đồng thời tìm thêm lỗi metric frame, absolute-delta additivity, Windows PID lineage
và analysis/source binding.

**Decision:**

1. Experimental unit là `(scenarioHash, demandRealizationHash,
   travelRealizationHash)`; `masterSeed` chỉ là robustness và không tăng N. Holdout
   khả dụng là fixed panel đúng 20 cell/5 travel-day cluster; không phát population
   p-value/NI CI.
2. Giữ strict service margin `m=1,0 pp`. Gate dùng exact integer aggregate trên
   denominator `20 × 108 = 2160`; equality tại biên là fail. Burden improvement
   không cứu service failure.
3. Frontier 25/25 đóng: B1→C1 unbounded là lock/ranking price; C1 unbounded→tight
   là budget price. Pickup-ETA reduction do lock định nghĩa và drop-ETA reduction
   không bị lock phải báo riêng; pilot xấp xỉ 18%/82% không được áp sẵn cho WP9.
4. Oracle burden phải chạy process BCL-only và mutation; pairing bind orientation,
   policy/unit/denominator; decision-frame count tối đa một lần/envelope. Verifier
   recompute lifecycle/checkpoint/report/behavior and audited solver evidence.
5. Node-cap failure v1 không được loại cell. Amendment pre-outcome đồng nhất
   request target 128→108, materialize 20/20 derivative v2.
6. Handoff review tìm thấy analyzer chưa bind bundle với exact plan/label/scenario và
   HEAD/status không bind nội dung dirty tree. Amendment pre-outcome `WP8-011b` thêm
   execution binding, full Git-visible content+HEAD inventory pre/post, path-safe IDs,
   robustness analyzer không gate và exact locked/earned decomposition.
7. Freeze receipt v1 `H2=97af95cf…d3049` giữ byte-nguyên làm lịch sử. Receipt v2
   `H3=d028eae4…dd14e` supersede operationally; executable verifier recompute 24
   file hashes, Runner và derivative/scenario tree seals trước outcome.
8. Full-PDF audit Alonso-Mora/Gschwind/Simonetto/Engelhardt/Zalesak/Schulz chỉ cho
   phép exact same-state/same-route reuse. Không thêm direction/random/sparse prune;
   work counters/output giữ exact, local process time giảm khoảng 20–23%, không SLA.

**Evidence:** `docs/benchmarking/wp8-006..014`, amendments `wp8-011a/011b`,
`docs/reviews/wp1-wp8-final`, frontier external 25/25, `.NET 840/840`, pinned Python
suite hiện hành, freeze verifier PASS với `H3` nêu trên.

**Consequences:** `RB-WP8-001..014 Done`; WP8 Complete. WP9 mở theo
`docs/tasks/39-wp9-main-experiment-ticket-plan.md`; `RB-WP9-001 Done`,
`RB-WP9-002` là ticket Ready duy nhất. Không confirmatory outcome tồn tại khi ADR
này được chấp nhận. Sau smoke, mọi bug outcome-bearing phải invalidate affected run,
không được vá lẻ hoặc đổi margin/treatment.

### ADR-044 — 2026-08-21 — Accepted

**Context:** Sau ADR-043 nhưng vẫn trước bất kỳ Layer-2/confirmatory outcome nào,
receipt của Layer-1 mechanical cho thấy harness đã gọi Runner Release hiện hành
`4c297a2c…bd2a8`, trong khi H2/H3 pin artifact cũ `16f3b5e8…3aad`. Hai DLL cùng
174.592 byte nhưng không byte-identical. Một publish sạch từ source đã review cho
đúng hash hiện hành; 19/19 file của publish tree byte-identical với Runner tree mà
Layer-1 đã dùng.

**Decision:**

1. Chấp nhận amendment pre-outcome `WP8-011c`; đây chỉ là sửa provenance, không
   đổi panel, policy, configs, seed, endpoint, strict margin hay analysis.
2. Giữ H2/H3 byte-nguyên làm lịch sử. Receipt v3
   `H4=2f7e6bf36c16784e06cb3266f9764f3103f2de6fc931f3c8e023bdc1a81a32dd`
   là freeze vận hành hiện hành; Runner DLL là `4c297a2c…bd2a8` và Runner-tree seal
   là `29a8195b…4589`.
3. Freeze verifier phải bind 25 file/Runner hashes và recompute ba tree seal
   (derivative, scenario plan, Runner artifact). Mọi job WP9 dùng đúng external
   publish root đã pin.
4. Layer-1 bundle ngoài repo `E:\RideBoundData\wp9\layer1\bundle-20260821-v1`
   PASS 8/8, external verify, evidence class `mechanical`; nó đóng `RB-WP9-003`
   nhưng không phải effectiveness/confirmatory evidence.

**Evidence:** `wp8-011c-pre-outcome-runner-artifact-repin.md`,
`freeze-receipt-v3.json`, freeze verifier PASS (25 hashes + 3 tree seals), Layer-1
receipt `bundle-20260821-v1.receipt.json`, per-file comparison 19/19 exact.

**Consequences:** `RB-WP9-001/003 Done`; `RB-WP9-002` audited smoke là ticket
Ready duy nhất. Chưa có confirmatory outcome. H4/Runner tree và repository inventory
phải giữ bất biến từ smoke tới full matrix; thay đổi outcome-bearing sau smoke phải
invalidate affected runs.

### ADR-045 — 2026-08-22 — Accepted

**Context:** Smoke WP9 chết ở **cả hai** arm với
`The active route could not be retained as the safety no-op candidate: MAX_RIDE_TIME`.
Cơ chế: giao thông xấu đi làm lộ trình *đang chạy* vi phạm `MAX_RIDE_TIME`,
`PhysicalPlanValidator` prune chính no-op đó, xe còn 0 candidate và run fail-closed.
Vì B1 cũng chết nên đây không phải lỗi cam kết; nó cũng không làm mất hiệu lực 25 run
frontier WP8 đã đóng. Gốc rễ là một khoảng trống ngữ nghĩa: `MAX_RIDE_TIME` chưa
bao giờ được định nghĩa là ràng buộc *lúc lập kế hoạch* hay ràng buộc *liên tục*.
`CommitmentBreachRecord`/`AppendBreach` đã tồn tại nhưng `AppendBreach` chỉ được gọi
khi deserialize checkpoint — không đường quyết định nào từng ghi breach.
`PICKUP_WINDOW` có đúng cùng lỗ hổng và cùng nguyên nhân ngoại sinh.

**Decision:**

1. Tách physical constraint thành hai lớp. **Structural** (`ROUTE_CONNECTIVITY`,
   `PRECEDENCE`, `CAPACITY`, `FROZEN_PREFIX`, `ONBOARD_PRESERVATION`,
   `ACCEPTED_PRESERVATION`, `PLAN_VERSION`, `STOP_LOCATION`, `INVALID_*`,
   `SCHEDULE_OVERFLOW`) là bất biến của một kế hoạch well-formed: vi phạm là defect,
   không phải giao thông, và vẫn strict ở mọi nơi. **Service-quality**
   (`MAX_RIDE_TIME`, `PICKUP_WINDOW`) là lời hứa về thời gian và có thể bị phá vỡ
   mà không ai quyết định gì.
2. Service-quality là ràng buộc **lúc tạo kế hoạch**, không phải bất biến liên tục.
   No-op an toàn luôn được giữ; nó không bao giờ bị prune bởi một dimension
   service-quality.
3. Vi phạm ngoại sinh được ghi nhận, không bị nuốt: `ProbeServiceQuality` chiếu lộ
   trình *không đổi* dưới travel snapshot hiện hành và phát
   `ExogenousServiceQualityBreach` (vehicle, request, code, dimension, contractual,
   exogenous) vào `CandidateGenerationDiagnostics`. Xe tiếp tục phục vụ.
4. **Chống rửa vi phạm.** Bound nới ra đúng bằng `max(contractual, exogenous)`, với
   `exogenous` là giá trị mà *không làm gì* đã hiện thực hoá. Do đó không candidate
   nào được phép tệ hơn no-op trên chính dimension đang breach; phần tệ hơn vẫn bị
   prune với `Expected` là bound hiệu lực. Đây đúng là cơ chế ba chiều
   `exogenous / decision-induced / visible` dự án đã có (`PromiseDeltaCalculator`),
   không thêm khái niệm mới.
5. Request chưa nằm trên lộ trình đang chạy (mọi request mới chèn) **không** có
   entry nào trong relaxation, nên vẫn bị enforce contractual tuyệt đối. Không thể
   nhận một khách mà lộ trình không phục vụ nổi.
6. Relaxation là hàm thuần của `(run, vehicle, travelSnapshot, evaluationTime)` và
   được áp dụng **đồng nhất ở cả hai arm**, nên không dịch chuyển arm nào so với arm
   nào. Ba đường re-validate downstream (`RollingCostPolicy`, `MultiplePlanPolicy`,
   `CommitmentDecisionValidator`, `OnlineStateCheckpointCodec`) đều chuyển sang
   `ValidateWithExogenousRelief` để không bác chính candidate mà generator giữ hợp lệ.
7. `ForwardSlackProfile` certify delay theo đúng bound mà validator enforce; cache key
   bind thêm digest của relaxation. Nếu hai bên lệch nhau, một route validator chấp
   nhận vẫn có thể mất slack certificate và bị prune.

**Evidence:** `ServiceQualityAllowance`/`ProbeServiceQuality`/
`ValidateWithExogenousRelief` trong `PhysicalPlanValidator`; 6 regression Domain
(breach ride-time, breach pickup-window, chống rửa, structural fail-closed, no-relief
khi không breach, scoping theo request/dimension) + 4 regression Algorithms (no-op
sống sót, breach được báo cáo đúng số, không breach thì không báo, candidate tệ hơn
no-op vẫn bị prune). Full solution 851/851 Debug và Release; Release `-warnaserror`
0 warning.

**Consequences:** `RB-WP9-002a/002b` hết bị chặn. Ngữ nghĩa đối xứng hai arm nên không
đụng tính công bằng đã khoá ở ADR-043. Ba giới hạn phải giữ trong báo cáo:
(a) `ExogenousServiceQualityBreach` hiện **chỉ tồn tại trong tiến trình**:
`SolverExecutionEvidenceMapper.WriteGeneration` liệt kê field tường minh nên breach
không được serialize vào `solver.executionEvidence`, và `SolverEvidenceFields` của
contract là tập đóng nên thêm nó là một thay đổi contract. Vì vậy breach hiện
**không** vào transcript, **không** vào `CommitmentBreachRecord`/`AppendBreach`, và
**không đo được** từ bundle. Bắc cầu cả hai — evidence và ledger — là `RB-WP9-002c`;
(b) cho tới khi `002c` đóng, không tài liệu nào được báo cáo breach count như một
đại lượng đã đo. Sau `002c` nó vẫn là secondary/descriptive, không phải endpoint đã
prereg, nên không cứu được gate nào; (c) thay đổi này là outcome-bearing với mọi run WP9 chạy sau nó,
nên freeze chain phải repin trước smoke và mọi run trước đó không được trộn vào cùng
estimand.


### ADR-046 — 2026-08-22 — Accepted

**Context:** Review toàn bộ WP8 trước khi mở ma trận WP9 tìm được ba defect trong
đường phân tích confirmatory — nơi con số kết luận thực sự được sinh ra — mà toàn
bộ test suite trước đó không bắt được.

1. **Blocker.** `wp9_fixed_panel_analyze.py` không chạy được trên chính manifest đã
   đóng băng. Manifest khai arm bằng danh tính preregistered
   (`b1-rolling-cost`, `c1-hard-vector-tight-30s`), còn analyzer nối thẳng chuỗi đó
   vào `f"p-{cell}-{arm}-tight-s7"` để dựng jobId kỳ vọng, trong khi execution plan
   dùng token ngắn `b1`/`c1`. Mọi cell raise `primary job binding differs`. Nghĩa
   là `RB-WP9-005` chưa từng chạy end-to-end; unit test cũ chỉ truyền token ngắn
   tổng hợp nên không bao giờ chạm vào artifact thật.
2. **Fail-open.** Cả analyzer chính lẫn analyzer robustness chỉ kiểm manifest
   "không rỗng, cellId không trùng". Một manifest liệt kê 19/20 cell qua được mọi
   kiểm tra và cho ra verdict `pass` trên mẫu số nhỏ hơn mà không nói gì.
3. **Fail-open.** Orientation arm không bị chặn ở tầng Python. `PairedComparisonDesign`
   trong `ExperimentalUnitModels.cs` chặn đúng, nhưng analyzer confirmatory là một
   cài đặt Python độc lập không dùng contract đó; `experimentalUnitModelsSha256`
   trong freeze receipt vì thế cho một cảm giác an toàn sai.

**Decision:**

1. Analyzer bind arm bằng registry tường minh `_PRIMARY_ARMS`
   (danh tính preregistered → token plan + wp4 config) và pin orientation
   `_PRIMARY_ORIENTATION`; arm lạ hoặc đảo chỗ baseline/treatment fail closed.
2. Cả hai analyzer bắt buộc tập cell của manifest **bằng đúng** tập cell primary/
   robustness của execution plan đã đóng băng. Không còn phân tích được panel cụt.
3. Regression mới bind vào **artifact thật** (`analysis-manifest-v1.json` +
   `execution-plan-v1.json`), không phải dict tổng hợp. Đây là gốc rễ vì sao lỗi 1
   sống sót qua nhiều vòng review.
4. Cell dùng lại một bundle cho cả hai arm fail closed.

**Evidence:** Python suite 77 → **86** pass, 0 skip (với `RIDEBOUND_FLEETPY_ROOT`).
`FrozenManifestBindingTests` chứng minh 20/20 cell của manifest thật bind đúng vào
plan thật; `CapacityPanelBindingTests` chứng minh Panel A/B không lẫn nhau.

**Consequences:** `analysisProgramSha256` và `robustnessAnalysisProgramSha256` đổi,
nên `H4` mất hiệu lực và `H5` phải repin — trùng với yêu cầu của ADR-045. Bài học
giữ lại: một freeze receipt pin hash của file không chứng minh file đó *chạy được*;
mọi program nằm trên đường outcome phải có ít nhất một test chạy nó trên đúng
artifact đã đóng băng.

### ADR-047 — 2026-08-22 — Accepted

**Context:** Panel B (`veh4`) bắt được hai defect mà Panel A (`veh8`) chạy 60/60
sạch vẫn không lộ. Đây chính là lý do phải có điểm năng lực căng: ở 8 xe lộ trình
còn dư thời gian nên không bao giờ chạm biên deadline; ở 4 xe thì chạm liên tục.
3/9 job Panel B đầu tiên chết với `RBWP7_FLEETPY_PLAN_INFEASIBLE`.

**Defect 1 — phạm vi relief của ADR-045 quá rộng.** ADR-045 nới bound
service-quality cho *mọi* candidate. Nhưng adapter chỉ gửi kế hoạch sang FleetPy
khi lộ trình **đổi** (`fleet_control.py` bỏ qua route không đổi), và FleetPy giữ
`VehiclePlan.is_feasible()` của riêng nó theo deadline hợp đồng gốc. Hệ quả:
RideBound đề xuất một kế hoạch mà simulator từ chối. Relief đúng ra chỉ cần cho
no-op — thứ không bao giờ được gửi đi.

**Defect 2 — lượng tử hoá vị trí xe làm ETA lạc quan.** `mapping.py` mã hoá tiến
độ trên cạnh thành permille bằng `ROUND_HALF_EVEN`. Permille là 1/1000 cạnh, nên
nửa permille của một cạnh 130 s là 65 ms. Làm tròn tới gần nhất có thể đặt xe
*trước* vị trí thật, khiến mọi ETA hạ nguồn lạc quan. Một job chết vì vượt cửa sổ
đón đúng **13 ms** (`latest 1916000 ms`, FleetPy tính `1916013 ms`) trong khi
RideBound tính ra đúng hạn. Defect này có từ trước ADR-045 và độc lập với nó.

**Decision:**

1. Relief chỉ áp cho **safety no-op**. Mọi candidate đã đổi được sinh dưới
   `ServiceQualityAllowance.Strict`. RideBound vì thế không bao giờ đề xuất kế
   hoạch mà FleetPy từ chối, và tính chống rửa vi phạm mạnh lên mức tối đa.
   Hệ quả hành vi phải nói rõ: một xe có lộ trình đang breach sẽ **chỉ còn no-op**
   cho tới khi breach trôi qua — đó là chi phí dịch vụ thật, không được giấu.
2. Lượng tử hoá permille đổi sang `ROUND_FLOOR`. Kết hợp với `DivideRoundUp` sẵn
   có cho phần thời gian còn lại của cạnh, RideBound bảo thủ ở **cả hai** nửa và
   không bao giờ lạc quan hơn FleetPy.
3. Freeze receipt lên schema `5.0.0` và bind thêm
   `adapterPackageTreeSealSha256` trên `simulators/fleetpy-ridebound/ridebound_fleetpy`.
   Toàn bộ package adapter là outcome-bearing nhưng trước đó **không được pin**:
   một dòng đổi rounding trong `mapping.py` dịch chuyển mọi ETA mà freeze không
   hề biết.

**Evidence:** 3/3 job Panel B từng hỏng nay `completedVerified` — 2 job đầu do
Decision 1, job còn lại chỉ qua sau Decision 2. `.NET 852/852`; pinned Python
86/86, 0 skip.

**Consequences:** Đây là thay đổi **outcome-bearing**. Theo `RB-WP8-014` §5,
mọi dữ liệu confirmatory đã chạy dưới `H5` bị **vô hiệu** và phải chạy lại dưới
freeze mới:

- Panel A 60/60 dưới `H5` và kết quả `Δ_service_panel = −7,08 pp` **không còn là
  outcome confirmatory**. Nó được giữ nguyên byte làm bằng chứng vận hành và
  bằng chứng rằng đường phân tích chạy được end-to-end, **không** được trích dẫn
  như kết quả.
- Panel B 6/40 dưới `H5` cũng bị bỏ.
- Không được tái sử dụng bundle `H5` nào bằng cách so hash rồi giữ lại cái trùng:
  đó là chọn lọc phụ thuộc outcome. Chạy lại toàn bộ hai panel.

Bài học: điểm năng lực căng không chỉ là một điểm dữ liệu thêm — nó là công cụ
kiểm lỗi. Hai defect này sống sót qua toàn bộ WP7/WP8 vì mọi thứ trước đó đều
chạy ở mức đội xe dư.

### ADR-048 — 2026-08-23 — Accepted

**Context:** Toàn bộ ma trận H6 đã hoàn tất: Panel A 60/60 bundle (40 primary,
20 robustness) và Panel B 40/40 primary. Hai analyzer canonical chạy trên đúng
manifest/plan đã freeze. Service gate preregistered thất bại ở cả hai capacity,
trong khi burden gate đạt. Robustness tách được chi phí lock/ranking và hard
budget nhưng không thể thay đổi gate primary.

**Decision:**

1. Chấp nhận và công bố kết quả âm: Panel A `1735 → 1581`, `−7.1296 pp`; Panel B
   `966 → 860`, `−4.9074 pp`; margin là `−1.00 pp`, không đổi hậu outcome.
2. Không pool hai panel. Demand/travel realization giống nhau nhưng fleet state và
   `scenarioHash` khác; between-capacity chỉ là heterogeneity mô tả.
3. Burden reduction phải đi cùng locked/earned decomposition và service result.
   Không gọi `99%+` là cùng công việc được làm tốt hơn khi treatment có thể từ chối
   công việc. Pickup-ETA lock là definitional.
4. Robustness giữ `confirmatoryGate:null`; C1 unbounded, C2 loose và seed19 không
   cứu primary. Seed19 là non-replicate và tăng N bằng 0.
5. Giữ finite-panel boundary: 20 cell nhưng 5 travel realization, precision đạt
   khoảng `1.40 pp`, sign-flip floor `0.03125`; không CI/p-value population.

**Evidence:** `wp9-confirmatory-result-2026-08-23.md`; analysis SHA-256 Panel A
`72f052d7…880e0`, Panel B `3f6a339c…bbe3f`, robustness `ce87ea75…9533b`;
receipt Panel A `8c7cf66a…96a5a`, Panel B `cb86aa4a…2165`.

**Consequences:** `RB-WP9-004..008 Done`. WP9 verdict là kết quả âm có điều kiện
theo đúng hai điểm 4/8 xe. Không có claim population, SLA, satisfaction, fairness
hoặc novelty. WP10 phải giữ kết quả này và chỉ đo heterogeneity cross-system.

### ADR-049 — 2026-08-23 — Accepted

**Context:** ADR-045 tạo `ExogenousServiceQualityBreach` trong generation
diagnostics nhưng evidence v1.0 không serialize nó và runtime không append ledger.
Vì H6 đã hoàn tất, bridge có thể được cài đặt mà không làm thay đổi estimand đã
freeze. Review deserialize cũng cho thấy redundant safety/budget fields cần được
Domain kiểm trực tiếp, không chỉ dựa vào outer checkpoint hash.

**Decision:**

1. Evidence v1.1 thêm canonical `exogenousServiceQualityBreaches`; contract và
   verifier vẫn nhận v1.0 để tái kiểm H6, nhưng bác version/shape/value/order sai.
2. Thêm loại ledger `ExogenousServiceQuality` không cần operational incident giả.
   Exogenous projection phải bằng safety projection, decision delta bằng zero,
   visible bằng exogenous, budget không đổi, witness phải là overrun đúng dimension.
3. Runtime bridge reproject reduced pre-decision state, append breach trước stage,
   và hash certificate/state sau bridge. Lỗi bridge fail session, không nuốt evidence.
4. Checkpoint cũ giữ byte shape; record mới có explicit kind/witness array và decode
   kiểm toàn bộ projection/delta/budget/witness đã serialize.
5. Mọi probe v1.1 là post-outcome mechanism evidence, không được trộn vào H6.

**Evidence:** `.NET` targeted Domain/Runner tests PASS; Python verifier 93/93 trước
final full gate. Real FleetPy Panel A/B1 probe PASS independent verifier: 800 epoch,
43 evidence observation và đúng 43 ledger record; zero decision charge, zero budget
change, zero invalid witness. Empty branch Panel B/C1 PASS 353 epoch với 0/0.
Hash chi tiết ở `benchmarking/evidence/wp9-009-breach-bridge-smoke-v1.json`.

**Consequences:** `RB-WP9-009 Done`; ADR-045 consequence “breach chưa vào transcript/
ledger” bị supersede. H6 outcome và freeze không đổi. WP9 `001..009` đóng; WP10 là
work package active kế tiếp.

### ADR-050 — 2026-08-23 — Accepted

**Context:** WP9 đã đóng với negative confirmatory result. WP10 cần một framework
độc lập nhưng không được thay Runner hoặc reimplement decision logic. Official source
audit xác nhận RidePy tag `v2.10.1` ở commit `bf1863e…9f14`, MIT, có extension point
`FleetState`/`VehicleState`/`TransportSpace`. Upstream pin POSIX/Linux và Windows
CPython 3.10 không có binary wheel. Graph CPE không expose directed-edge fraction
ổn định.

**Decision:** (1) RidePy là Layer 3 final mặc định; AMoD2 chỉ fallback nếu named gate
thất bại. (2) Checkout/vendor build nằm ngoài repo, bind commit + exact `lru-cache`/
`googletest` submodule commits + 527-file tree inventory `d99ffac8…d891e` + license
hash. (3) Environment là Linux container với
base digest `a365ce6a…0e235`; không sửa vendor để chạy Windows. (4) Adapter subclass
`FleetState`, dùng native fast-forward/space/events nhưng mọi B1/C1 decision đi qua
cùng versioned Runner. (5) Position model downgrade explicit thành `nodeOnly`;
party size pin 1; main reassignment false. (6) WP10 subset chỉ là descriptive paired
heterogeneity, không pool hoặc rescue H6. (7) Queue/gates chính thức là
`tasks/40-wp10-ridepy-layer3-ticket-plan.md`.

**Consequences:** `RB-WP10-001 Done`, `RB-WP10-002 Ready`. Source/env verifier phải
pass trước code adapter; canonical scenario phải reconcile native pickup/drop trước
freeze paired subset. O-005 được đóng chọn RidePy, nhưng có rollback named sang
AMoD2 nếu capability gate fail.

### ADR-051 — 2026-08-23 — Accepted

**Context:** Exact RidePy source/runtime và same-Runner canonical scenario đều pass.
Canonical B1/C1 mỗi arm hoàn thành 5/5 request, reconcile 5 pickup + 5 drop và giữ
Runner publish tree bất biến. Final frozen representative subset đã chạy 22/24 arm
jobs: 22 pass, B1 `travel-update-stress-r3` fail closed tại epoch 17 và C1 paired arm
không chạy theo no-partial-reuse policy. Failure xảy ra sau native pickup tại 116 giây
trong khi Runner chỉ quan sát node cuối và còn giữ pickup ETA 178 giây. Một event xe
khác đã tạo epoch khi xe này giữa cạnh; RidePy CPE không cung cấp directed-edge
progress cho capability `nodeOnly`.

**Decision:** (1) Đóng `RB-WP10-002..010` và WP10 bằng **negative capability result**.
(2) Gán mã `RBWP10_NODEONLY_CONCURRENT_MIDEDGE_UNSUPPORTED`; không suy diễn progress
từ clock, không đổi manifest, không bỏ failed job và không chạy paired arm còn lại.
(3) Giữ freeze/output v1/v2/v3 để audit; chỉ v3 là terminal attempt được diễn giải.
(4) Báo 11 valid pairs riêng: B1 `54/62`, C1 `49/62`, `−8.06 pp`, descriptive only;
không coi đây là planned-subset estimand, không CI/population inference và không pool
H6. (5) Không chuyển AMoD2 hậu-outcome; nếu làm sẽ là work package/ADR mới.

**Consequences:** Exact-source/environment, same Runner, mapping, native lifecycle,
canonical và verifier gates PASS; representative subset gate FAIL CLOSED. Layer 3
cross-system claim **not established**. WP10 vẫn hoàn tất vì failure được giữ và gate
được đánh giá trung thực. Evidence chính là
`benchmarking/wp10-ridepy-layer3-negative-capability-result-2026-08-23.md`; external
receipts bind source `2b431062…0775`, freeze v3 `18a74fa3…6672`, strengthened
subset analysis v2 `be3e9077…cca3` và failure transcript `0ee5e3ec…a85`.

### ADR-052 — 2026-08-23 — Accepted

**Context:** Final WP1–WP10 review đọc full PDF Alonso-Mora, Gschwind–Drexl,
Simonetto, Engelhardt, Zalesak và Schulz–Pfeiffer, đồng thời đo lại candidate hot
path. Full constant-time temporal insertion check không thể được nhập nguyên xi:
RideBound còn kiểm structural/commitment constraints và generic travel snapshot không
cam kết triangle inequality. Review cũng tìm thấy `ForwardSlackCacheKey` tạo textual
position identity dù immutable `VehicleState` đã bind position, và terminal node tạo
key thứ hai chỉ để xác nhận lookup do chính node đó prefetched.

**Decision:** (1) Không thêm heuristic prune, sparse/direction/random filter hay
paper-derived default. (2) Bỏ position string dư khỏi process-local cache key; vehicle
reference tiếp tục bind toàn immutable snapshot. (3) Thêm exact `Matches` trên key đã
tạo để kiểm run/vehicle/route/evaluation/travel/allowance mà không allocate/hash key
thứ hai. (4) Giữ nguyên full physical validator, cache-failure lifecycle, search order,
work/candidate caps, candidate identity và solver input. (5) Thêm source-controlled
Release harness và chạy 3 baseline + 3 optimized process; timing chỉ descriptive.

**Evidence:** Key construction giảm allocation `40,000,072 → 28,000,072` byte mỗi
250.000 lookup (−30%) ở cả route 4/8/16 stop. Complete generator giảm 0,79–1,30%
allocation; timing mixed (`−4,9%`, `+2,8%`, `−1,9%`) nên không có speed claim. Sáu
semantic work counters exact ở mọi run; required .NET 855/855, FleetPy 95/95,
RidePy 23/23, Release/format/vulnerability/static gates pass. Evidence ở
`benchmarking/post-wp10-exact-reuse-optimization-2026-08-23.md` và review
`reviews/wp1-wp10-final/README.md`.

**Consequences:** Tối ưu được giữ vì allocation benefit xác định và semantic
equivalence, không vì một effectiveness/SLA claim. H6/WP9 và WP10 terminal verdict
không đổi. Exact RidePy image đã archive/load lại với SHA-256
`4783c541…9a872`; đây là restore evidence, không biến Dockerfile apt/pip thành future
byte-reproducible rebuild. Final PDF 12 trang đã render/inspect, SHA-256
`066168872d7ead11362b3f0f7b5832e8e1147bb655f281cc3ec08d939c29b20b`.

**Final test execution note:** Một attempt chạy suite đồng thời với Release build/
format làm medium public-drain chạm đúng `resource.cpu-time-exceeded` (`854/855`).
Không đổi ceiling và không reclassify. Rerun exact `dotnet test RideBound.slnx`
hoàn toàn đơn độc pass 855/855; đó là final baseline.
## 8. Work package tracker

| WP | Trạng thái | Bắt đầu | Kết thúc | Evidence |
|---|---|---|---|---|
| WP0 Scaffold | Complete | 2026-07-28 | 2026-07-28 | build + 8 RideBound + 25 backend + 7 frontend tests |
| WP1 Contracts | Complete; Q1 Release revalidated with host-policy exception | 2026-07-29 | 2026-07-29 | ADR-014–017 + 157/157 closure + WP1 revalidation 161/161 + replay/hash proof |
| WP2 Online baseline | Complete; physical/B1 gate, Debug 333/333; Release host-policy exception recorded | 2026-07-29 | 2026-07-30 | ADR-018–020 + Debug 333/333 + Release bundles + two-process tiny replay |
| WP3 Ledger/certificate | Complete; `001..014` DONE; Debug 414/414 | 2026-07-31 | 2026-08-02 | ADR-021/022 + `tasks/28` + full-solution 414/414 + WP3 process/checkpoint replay |
| WP4 Algorithms/solver | Complete; `001..014` Done; Q2 mechanical gate closed | 2026-08-03 | 2026-08-03 | ADR-023/024 + independent oracles + named policy/solver/Runner path + 557/557 + final review |
| WP5 BeGo integration | Complete; `001..014` Done; Q3 mechanical gate closed | 2026-08-05 | 2026-08-09 | ADR-025 + durable adapter/rollout + paired bundle + independent evidence + source/claim review; BeGo 154/154 Debug/Release |
| WP6 Benchmark harness | Complete; `001..014` Done; common mechanical harness gate closed | 2026-08-09 | 2026-08-13 | ADR-026..036 + contract v1.0.6; fresh tiny A + medium H/I exact-source semantic reproduction, strict external verify, final review, 770/770 |
| WP7 FleetPy | Complete; `001..014` Done; mechanical Layer-2 closed | 2026-08-13 | 2026-08-17 | ADR-037/038/039 + `tasks/35`/`tasks/36` + Runner v8 actual B1/C1 preflight/lifecycle/tiny/medium + external verifier |
| WP8 Pilot/prereg | Complete; `001..014` Done | 2026-08-18 | 2026-08-21 | ADR-040/041/043/044 + frontier 25/25 + oracle/verifier + fixed panel/amendments; H6 kế thừa freeze chain |
| WP9 Main experiments | Complete; `001..009` Done; negative result | 2026-08-21 | 2026-08-23 | ADR-045..049 + H6 100/100 raw bundles verified; both service gates FAIL, burden gates PASS; robustness descriptive; breach bridge post-outcome verified |
| WP10 Cross-system | Complete; `001..010 Done`; negative capability result | 2026-08-23 | 2026-08-23 | ADR-050/051 + exact RidePy/Linux image + same-Runner canonical PASS; representative subset FAIL CLOSED; Layer 3 not established |
| Post-WP10 assurance | Complete | 2026-08-23 | 2026-08-23 | ADR-052 + WP1–WP10 final review + 3+3 process optimization benchmark + 12-page rendered PDF |
| WP11 Product UX | Not started | — | — | — |
| WP12 Paper/release | Not started | — | — | — |

## 9. Change history

- 2026-08-23: Chấp nhận ADR-052 và hoàn tất final review WP1–WP10. Review toàn cây
  sửa WP10 analyzer để bind exact terminal inventory/freeze/full Runner/seed, sửa
  format drift, và giữ negative outcomes không đổi. Full-PDF audit chỉ cho phép exact
  cache reuse: key allocation giảm 30%, complete generator heap giảm 0,79–1,30%,
  semantic work counters exact; timing mixed nên không claim speed. Final gates:
  .NET 855/855, FleetPy 95/95, RidePy 23/23, Release/format/NuGet/static pass. Exact
  RidePy image archive load lại đúng image ID; future Dockerfile rebuild vẫn không
  được claim byte-reproducible. Báo cáo PDF 12 trang đã render/inspect, SHA-256
  `066168872d7ead11362b3f0f7b5832e8e1147bb655f281cc3ec08d939c29b20b`.
- 2026-08-23: Đóng WP10 `001..010` bằng ADR-051. Exact RidePy 2.10.1 source/Linux
  image và same-Runner canonical B1/C1 pass; mỗi arm 5/5 completed, đủ native pickup/
  drop, artifact tree bất biến và five-class mutation verifier pass. Frozen subset
  plan 24 arm jobs kết thúc 22 pass, một B1 fail closed, paired C1 không chạy. Mười
  một valid pairs cho B1 `54/62`, C1 `49/62` (`−8.06 pp`) nhưng chỉ descriptive.
  Failure `RBWP10_NODEONLY_CONCURRENT_MIDEDGE_UNSUPPORTED` chứng minh `nodeOnly`
  không đủ khi các xe đồng thời giữa cạnh; không nội suy state, không đổi manifest,
  không fallback hậu-outcome. Layer 3 claim chưa được thiết lập.
- 2026-08-23: Đóng WP9 `001..009` với kết quả âm dưới H6. Panel A (8 xe)
  `1735 → 1581`, `−7.1296 pp`; Panel B (4 xe) `966 → 860`, `−4.9074 pp`; cả hai
  service gate FAIL so với `−1 pp`, burden gate PASS. Independent verifier đọc
  100/100 raw bundle, bốn falsification condition và repeat deterministic PASS;
  seed19 tăng N bằng 0. Robustness cho thấy lock/ranking và 30-second budget mỗi
  phần `−3.7037 pp`; C2 không rescue. Sau khi matrices đóng mới cài evidence v1.1
  và exogenous ledger bridge; real FleetPy probe ghi 43/43 evidence/ledger breach,
  không charge decision budget. Đây chỉ là mechanism evidence hậu-kết-quả. Chấp
  nhận ADR-048/049; WP10 trở thành next work package.
- 2026-08-22 (tối): Chấp nhận ADR-047 và **vô hiệu hoá toàn bộ dữ liệu confirmatory
  dưới `H5`**. Panel A đã chạy 60/60 sạch và analyzer cho `Δ_service_panel =
  −7,08 pp` (20/20 cell âm, service gate trượt, burden gate đạt) — nhưng kết quả
  đó không còn là outcome confirmatory vì semantics đã đổi sau đó. Panel B (`veh4`)
  phát hiện hai defect mà Panel A không thể lộ: (1) relief của ADR-045 áp cho cả
  candidate đã đổi, khiến RideBound đề xuất kế hoạch mà `VehiclePlan.is_feasible()`
  của FleetPy từ chối; (2) `mapping.py` lượng tử hoá tiến độ cạnh bằng
  `ROUND_HALF_EVEN`, đặt xe trước vị trí thật và làm ETA lạc quan — một job vượt
  cửa sổ đón đúng 13 ms. Sửa: relief chỉ cho no-op; permille dùng `ROUND_FLOOR`.
  3/3 job từng hỏng nay `completedVerified`. Freeze lên schema 5.0.0, bind thêm
  `adapterPackageTreeSealSha256` vì toàn bộ package adapter là outcome-bearing mà
  trước đó không được pin. `.NET 852/852`, Python 86/86.
- 2026-08-22 (chiều): Chấp nhận ADR-046 và đóng `RB-WP9-002a`. Review WP8 tìm ba
  defect trong đường phân tích confirmatory: analyzer chính **không chạy được**
  trên manifest đã đóng băng (arm khai bằng danh tính preregistered nhưng bị nối
  thẳng vào jobId), và cả hai analyzer chấp nhận manifest thiếu cell hoặc đảo
  orientation. Đã sửa cả ba, regression giờ bind vào artifact thật; Python 77 → 86
  pass, 0 skip. Materialize Panel B (`grid-v3-veh4`, 20/20 cell, 108 request/cell)
  và **đo được** điều kiện tiên quyết: 20/20 cell chọn đúng cùng tập request và
  cùng travel realization như Panel A; chỉ vị trí đội xe khác, đã ghi thành giới
  hạn. Freeze `H5=6720acca…25da6` (schema 4.0.0) bind 30 file hash + 4 tree seal,
  verifier PASS, 4/4 mutation bị từ chối. Xác nhận ADR-045 trên dữ liệu thật:
  cặp diagnostic `d20181114-s10-r1` chạy hết ở **cả hai arm** (B1 738.769 ms,
  C1 650.215 ms, `status: pass`, C1 `completedVerified`), trong khi cùng cặp đó
  trước ADR-045 chết giữa chừng ở transcript 14,7 MB. Một lần chạy hỏng vì tài liệu
  bị sửa giữa ma trận — frozen-source guard bắt đúng, ghi lại làm quy tắc vận hành.
- 2026-08-22: Chấp nhận ADR-045 trước outcome confirmatory và mở khóa WP9. Smoke
  chết ở cả hai arm vì `MAX_RIDE_TIME` bị enforce như bất biến liên tục, prune
  chính safety no-op. Tách physical constraint thành structural (strict) và
  service-quality (plan-time); no-op luôn được giữ; vi phạm ngoại sinh được ghi
  làm `ExogenousServiceQualityBreach` với bound chống rửa `max(contractual,
  exogenous)`; `PICKUP_WINDOW` cùng lớp nên xử lý cùng cách. 851/851 Debug và
  Release, Release `-warnaserror` 0 warning. Cùng đợt: amendment `wp8-011d` thêm
  capacity stratum `veh4` bên cạnh `veh8` đã prereg (80 primary job, N không tăng,
  kết luận thành có điều kiện theo năng lực); `wp8-010` bỏ exogenous burden làm
  negative control — sai vì exogenous baseline là hàm của quyết định trước đó —
  và thay bằng identity chuỗi demand+travel bốn điều kiện; `wp8-008` ghi ba điều
  bất lợi (service không đơn điệu, trượt biên −4,69 pp ở 8 xe và toàn bộ về phía
  budget, 18% mức giảm là definitional theo lock). Đo lại tính độc lập của panel:
  bốn file `sample_10_*` cùng ngày chồng 8,3–10,7% request id ở tầng nguồn nhưng
  **không** lan tới panel — 2.160 slot chứa 2.157 request phân biệt, chỉ 3 lần
  dùng lại; ràng buộc thật là travel factor hằng số theo ngày, tức 5 realization
  chứ không phải 20. `H4` hết hiệu lực vận hành; `RB-WP9-002a` phải repin `H5`.
- 2026-08-21: Chấp nhận ADR-044 trước outcome confirmatory. Kiểm Layer-1 receipt
  phát hiện stale Runner pin trong H2/H3; publish sạch xác nhận DLL hiện hành
  `4c297a2c…bd2a8` và 19/19 file khớp Runner tree Layer-1. Amendment `WP8-011c` và
  current freeze `H4=2f7e6bf3…a32dd` bind thêm Runner-tree seal; verifier 25 hashes
  + 3 tree seals PASS. Layer-1 mechanical 8/8 đóng `RB-WP9-003`; `RB-WP9-002` Ready.
- 2026-08-21: Supersede ADR-042 bằng ADR-043; đóng WP8 `001..014`. Frontier
  25/25, treatment-only fairness, BCL-only burden oracle, oriented pairing,
  contended verifier, audited solver evidence và exact hot-path reuse đã qua gate.
  Bác `N=62`: fixed panel có 20 unit/5 ngày và denominator 2160/arm. Hai amendment
  pre-outcome materialize đồng nhất 108 requests và khóa analysis/source binding.
  Freeze tại thời điểm ADR-043 là `H3=d028eae4…dd14e`; chưa có outcome WP9. Review WP1–WP8 ghi
  đầy đủ defect đã sửa và giới hạn claim; `RB-WP9-002` là việc tiếp theo.
- 2026-08-19: Hoàn thành `RB-WP8-002`/`003` và chấp nhận ADR-041. Normalizer chạy theo
  grid manifest, harness nhận cardinality từ driver, và pilot chạy thật trên dữ liệu
  Manhattan công khai. Pilot bác bỏ điểm vận hành cũ (decision delta bằng 0 ở cả hai arm
  nên endpoint bằng 0 đồng nhất), loại primary endpoint pickup-ETA vì zero-inflated tới
  mức `p50 = p90 = 0`, và nâng service rate thành cổng đồng thời đo ở mốc hoàn thành
  chuyến. Bốn đơn vị paired ở điểm cao điểm thật cho Δ burden âm ở cả bốn (giảm 75–93%,
  median `−3.130.086 ms`) nhưng Δ tỷ lệ hoàn thành `−6,25/−5,47/−0,78/0,00` điểm phần
  trăm. Theo tiêu chí `docs/11` §5/§14 tự đặt trước, C1 ở cấu hình hiện tại **không vượt
  cổng service** ở điểm này. Đây là pilot n = 4, không CI, không effectiveness claim;
  confirmatory holdout chưa sinh.
- 2026-08-18: Hoàn thành `RB-WP8-001` và chấp nhận ADR-040, mở WP8. Kiểm kê nguồn xác
  nhận grid thí nghiệm dựng được **hoàn toàn từ dữ liệu công khai thật**: 289 file
  demand trên 8 ngày `2018-11-11` → `2018-11-18`, kèm sample fraction, tỷ lệ đặt trước
  và `tt_factors` theo ngày/giờ; normalizer đã tham số hoá đủ nên chỉ cần grid manifest
  thay cho hai profile hard-code. Pilot khoá ở `11-11`/`11-12` — bao trọn phần dữ liệu
  WP6/WP7 đã nhìn — và sáu ngày còn lại niêm phong làm confirmatory holdout. Commitment
  budget sẽ suy từ chỉ dữ liệu pilot rồi phân tầng ba mức, vì cấu hình hiện tại chỉ có
  3/10 chiều `hardLimit` và cả ba luôn thoả sẵn do O-001, khiến hard vector của C1 thực
  tế chưa bị ràng buộc. Margin non-inferiority phải đặt trước và neo vào chính sách suy
  biến từ chối hết. Tạo `tasks/37`/`tasks/38`; chỉ `RB-WP8-002` Ready; chưa có production
  code, chưa sinh scenario confirmatory, không claim nào được nâng.
- 2026-08-17: Chấp nhận ADR-039. Audit source khóa bằng ADR các thay đổi ngữ nghĩa
  từng vào source mà chưa có quyết định: `initialPromiseTrigger` hai giá trị, lock
  evaluator dùng baseline exogenous, `OfferDeclined` sau accept là cancel-after-
  acceptance, fail-closed `C1_VEHICLE_HAS_NO_FEASIBLE_CANDIDATE` với witness typed,
  hai CLI flag Runner và event-induced plan update phải opt-in. Hot path được **đo**
  chứ không suy đoán: chi phí nằm ở khóa memo slack (`19,63 → 0,64 µs`), không ở
  validator; wall time mỗi `Generate` giảm `23–39%` với toàn bộ counter bất biến, và
  benchmark medium về `1 m 50 s` dưới trần CPU. Đề xuất lazy priority bị bác bỏ bằng
  phân tích và được giữ làm kết quả âm. Required suite `798/798`, Python `50/50`,
  format/Release sạch; Runner v8 `13bf5d9b…c179e` chạy lại toàn bộ actual FleetPy gate.
  Vẫn không có effectiveness/SLA/novelty claim.
- 2026-08-16: Chấp nhận ADR-038 và đóng `RB-WP7-004..014`. Audit Candidate sửa B4
  repair-root priority để bounded best-first chấm đúng repaired suffix, đồng thời
  portfolio opt-in cấm hidden introduced request. FleetPy 1.0.2 actual B1/C1 dùng cùng
  Runner v6 qua preflight, lifecycle, tiny và public-medium three-repeat verifier;
  .NET 790/790 và Python 49/49 pass, WAC không tái hiện. Đây chỉ là mechanical Layer-2
  closure; raw timing/publication khác nhau không được suy thành effectiveness hay SLA.
- 2026-08-13: Hoàn thành `RB-WP7-001..003` và chấp nhận ADR-037. Browser/source
  research loại paper-only/random pruning; Candidate portfolio opt-in giữ cost/stability
  anchor theo service set, exact loss accounting và old-config legacy semantics. Bounded
  adversarial tăng accepted `2 → 4`; 32-seed real C1 gate không substantive regression
  và có strict positive. FleetPy exact annotated tag/commit/source/env probe pass,
  actual directed-edge `_move` và non-forced lock path được thực thi; bốn drift mutation
  fail closed. Full solution 776/776; chuyển duy nhất `RB-WP7-004` sang Ready.
- 2026-08-13: Hoàn thành `RB-WP6-014`, chấp nhận ADR-036 và đóng WP6. Source-level
  audit WP1–WP6 không phát hiện unresolved correctness/contract defect; WP3 full
  publication gate, WP4 fairness/fallback, WP5 pinned Runner/durable recovery và WP6
  raw/oracle/bundle authority giữ nguyên. Fresh tiny A 8/8; medium H/I trên exact
  source cuối mỗi process 8/8, 16/16 top-level + 72/72 per-run semantic exact,
  8/8 resource rows khác hợp lệ;
  external verifier valid. Required full solution 770/770, WAC không tái hiện. Tạo
  final Vietnamese review và closure evidence; WP7 giữ Not Started, không active ticket.
- 2026-08-12: Hoàn thành `RB-WP6-013` và chấp nhận ADR-035/contract `1.0.6`.
  Audit sửa executable warm-up, complete provenance/policy preflight, plan-derived
  conservation và canonical failure taxonomy. Declared permutation/parallel/failure/
  exclusion/process/store/metric/bundle/claim/source matrix pass. Medium D/E đều 8/8,
  semantic exact và strict verify valid; mọi sampled-resource row khác được giữ, gồm
  C1 chậm hơn B1 ở 6/6 local measured pair nhưng không có effectiveness/SLA claim.
  Release/format/dependency/schema/link sạch, exact full solution cuối 770/770; một
  preceding CPU-control failure không bị báo sai thành WAC. Chuyển `RB-WP6-014` In progress.
- 2026-08-12: Hoàn thành `RB-WP6-012` và chấp nhận ADR-034/contract `1.0.5`.
  Verified FleetPy source và two-clean-root normalization khớp exact medium derivative;
  dedicated synthetic-policy binding cùng exact Runner instant-drain lifecycle đóng lỗi
  config identity mà không nới validator/data. Hai fresh Release harness B/C đều 6/6,
  semantic/per-run hashes exact và external bundle verify valid; resource/full/bundle
  hashes khác đúng phạm vi. Attempt A unsorted provenance fail-closed được giữ lại.
  Format sạch, full solution cuối 710/710; một run trước fail wall-time contention được
  ghi đúng là timeout test, không phải WAC. Chuyển `RB-WP6-013` In progress.
- 2026-08-12: Hoàn thành `RB-WP6-011` và chấp nhận ADR-033/contract `1.0.4`.
  Sáu-epoch source fixture tạo accept/reject/revision/completion và exact non-zero
  decision-induced insertion witness. Composite WP3+WP4 policy binding, manifest
  solver seed opt-in, canonical failure-stage mapping và independently verified
  per-run oracle summaries đóng các lỗ provenance/measurement. Hai clean Release
  harness process khớp toàn bộ semantic identity; verified mechanical bundle
  `0936f8c...a1671` được publish dưới ignored `artifacts/`. Benchmarking 104/104,
  Algorithms 136/136, format/Release sạch, full solution 705/705; WAC không tái hiện.
  Chuyển đúng `RB-WP6-012` sang In progress.
- 2026-08-11: Hoàn thành `RB-WP6-010` và chấp nhận ADR-032. Builder reserve/tự sinh
  canonical claim profile/report, profile SHA được bind trong reproducibility evidence
  `1.0.1`; stage 10 chỉ scan selected claim surfaces rồi recompute report byte-exact.
  Exact caveat masking cùng Unicode/punctuation/confusable/synonym matcher trả typed
  witness và chặn resealed README/report/provenance/profile/forged-check mutations,
  không đọc raw trip/transcript/source prose. Browser đối chiếu ACM, NASEM, Unicode,
  Peng và Munafò; không nâng same-team repeatability thành validity/effectiveness/
  independent reproduction. Benchmarking 95/95, format/Release sạch, required full
  solution 691/691; WAC không tái hiện. Chuyển đúng `RB-WP6-011` In progress.
- 2026-08-11: Hoàn thành `RB-WP6-009` và chấp nhận ADR-031. Strict BagIt builder
  phát deterministic no-extra payload/tag/oxum/logical manifests qua private staging
  và atomic publication. Source capture bind exact dirty working tree; provenance
  rederive source/runtime/Runner/oracle/verifier/grid identities. Ordered verifier tái
  dùng WP6-007 transcript gate, bảo toàn mixed terminal logs, so oracle và recompute
  raw production metrics; external process chỉ emit immutable sidecar. Sáu bundle/
  source tests cùng full mutation matrix pass; Benchmarking 92/92, format và Release
  `-warnaserror` sạch, required full solution 688/688, WAC không tái hiện. Chuyển đúng
  `RB-WP6-010` In progress.
- 2026-08-11: Hoàn thành `RB-WP6-008` và chấp nhận ADR-030. Metric registry exact
  36 definition; production calculator sinh 132 canonical integer rows với explicit
  cohort/window/lifecycle/denominator/missing/resource rules. External BCL-only oracle
  không ProjectReference tự tái dựng và khớp byte-exact mọi row/evidence/metric-set;
  request/action/promise/vector/window/order/resource/zero-denominator/overflow
  mutations bị phát hiện hoặc từ chối typed. Browser evidence bổ sung McKeeman
  differential testing và Dolan–Moré performance-profile boundary; không mở aggregate/
  effectiveness claim. Benchmarking 86/86, format và Release `-warnaserror` sạch,
  required full solution 682/682; WAC không tái hiện. Chuyển đúng `RB-WP6-009`
  In progress.
- 2026-08-11: Hoàn thành `RB-WP6-007`. Append-only store publish plan/run bằng atomic
  rename, exact raw/index/detail/log identities, gapless failure/exclusion hash chain,
  terminal conservation, typed persistence seal, concurrent per-run locking và
  authorized full-grid supersession. Source audit bổ sung streaming hash, exact path/
  reparse guards, canonical resource samples, plan-bound manifest/checkpoint checks,
  independent artifact-inventory/launch verification và supervisor-store mapper.
  23 store + 2 mapper cases; Benchmarking 77/77, Contracts 38/38, format sạch và
  required full solution 673/673, không failed/skipped; WAC `0x800711C7` không tái
  hiện. Chuyển đúng một ticket `RB-WP6-008` In progress.
- 2026-08-09: Hoàn thành `RB-WP6-006`. Source implementation phát hiện failure
  taxonomy thiếu cancellation/process-count/stream-byte branch; ADR-028 + contract
  1.0.2/failure 1.0.1 republish plan/run/metric identity vectors. Supervisor chỉ gọi
  external pinned process, hash pre/post 189 .NET runtime files + 12 Runner deploy
  files/config/source trong actual fixture gate, kiểm độc lập capability/manifest/
  decision/ACK/checkpoint chain và giữ bounded partial evidence. 15/15 supervisor,
  contract 37/37, Benchmarking 52/52, format sạch, full 647/647; WAC 0x800711C7
  không tái hiện. Chuyển `RB-WP6-007` In progress.
- 2026-08-09: Hoàn thành `RB-WP6-005`. Framed HMAC seed vectors tái tạo qua hai clean
  processes; plan compiler bind exact config/protocol/work/capability/pairing, reject
  asymmetric/B5/noncanonical/oversized grids và materialize collision-free full run
  grid với HMAC arm order trước outcome. Permutation/32-way parallel exact; targeted
  32+37, format sạch, full 627/627. Chuyển `RB-WP6-006` In progress.
- 2026-08-09: Hoàn thành `RB-WP6-004`. Source-level review phát hiện cyclic scenario/
  report identities; ADR-027/schema 1.0.1 chuyển thành acyclic hash DAG. Normalizer
  kiểm exact member bytes, directed SCC/Dijkstra, dense coverage node pool + HMAC row
  ranking, ties-to-even/pseudonym/conservation và typed failures. Two clean processes
  byte-exact cho tiny 8/2/16/240 và medium 128/32/96/9120; 21400 input rows conserved,
  targeted 29+32, format sạch, full 619/619. Chuyển `RB-WP6-005` In progress.
- 2026-08-09: Hoàn thành `RB-WP6-003`. Public FleetPy Manhattan ZIP được opt-in
  download/resume và khóa exact length/MD5/SHA-256; safe preflight/extract tạo receipt
  335 members, `1022750557` uncompressed bytes và stable inventory hash. Rerun xác
  minh cache/extraction idempotent không chạm mạng; targeted 26/26, format sạch và
  full solution 612/612. Chuyển `RB-WP6-004` sang In progress.
- 2026-08-09: Hoàn thành `RB-WP6-002`. Thêm project contract thuần, 10 strict
  document codecs/models, semantic/path/pairing/topology/terminal validators, Draft
  2020-12 schemas/inventory, positive/negative fixtures và framed identity helpers.
  Hai clean process khớp published vector sáu hash; targeted 28/28, format sạch,
  required full solution 586/586. Chuyển duy nhất `RB-WP6-003` sang Ready.
- 2026-08-09: Hoàn thành `RB-WP6-001` refinement. Đọc đủ 82 Markdown và dùng
  in-app Browser kiểm FleetPy/Zenodo, RFC 8785, Random123, RFC 8493 BagIt, W3C
  PROV, FAIR, Datasheets, Sandve và ACM artifact terminology. ADR-026/contract v1
  khóa scenario/data/pairing/seed/Runner/failure/exclusion/metric/bundle/resource/
  claim semantics. Tạo queue `RB-WP6-002..014`, chỉ `002` Ready; chưa có harness,
  public download hoặc effectiveness result.
- 2026-08-09: Hoàn thành `RB-WP5-014` và đóng WP5/Q3 mechanical gate. Source audit
  sửa subject-link immutability, pre-T3 outbox publication và cross-run relay scope;
  BeGo Debug/Release 154/154, frontend/format/vulnerability gates sạch. Tạo detailed
  WP1–WP5 review với GO chỉ cho refinement và NO-GO cho experiment/SLA/effectiveness.
  Mở đúng một ticket `RB-WP6-001 READY`, chưa có WP6 implementation.
- 2026-08-09: Hoàn thành `RB-WP5-013`. Thêm executable hard-crash riêng tại đủ
  8 decision + 4 outbox boundary, fresh-Runner exact recovery, test-owned 16.384-step
  transition oracle, exact-set 2/3/4-worker PostgreSQL contention, `5/5` required
  mutants và raw randomized warm-up/repetition local curves. Artifact manifest
  `e21fb08...` rehash đủ 18 file; BeGo Debug/Release 153/153, RideBound 557/557,
  vulnerability audit sạch. Browser mapping ghi rõ LDFI/Elle/QuickCheck/mutation/
  performance mechanism và giới hạn claim. Chuyển duy nhất `RB-WP5-014` sang
  In progress; chưa đóng Q3 hoặc mở WP6 trước source-level closure audit.
- 2026-08-09: Hoàn thành `RB-WP5-012`. Thêm strict paired replay preflight,
  staged exact configs, B1/C1 × two clean Runner processes, exact materializer/
  checkpoint validation, repeat/common-input proof và self-verifying bundle bind
  source + assemblies. Final manifest `b843bd20...`; BeGo Debug/Release 152/152,
  RideBound 557/557, vulnerability audit sạch. Chuyển duy nhất `RB-WP5-013`
  sang In progress; không diễn giải bundle thành effectiveness hoặc SLA.
- 2026-08-09: Hoàn thành `RB-WP5-011`. Thêm default-off conditional hosted
  registration, exact artifact preflight health/gate và durable immutable Shadow/
  Live namespace. Decision claim lọc namespace; outbox hard-code Live-only nên
  shadow backlog không publish sau mode switch. PostgreSQL kiểm same-Session dual
  namespace, old Session snapshot unchanged, expired lease reclaim và guarded
  rollback; logic audit sửa unique-constraint typed mapping. Debug/Release 147/147,
  frontend 9/9, RideBound 557/557, dependency audits sạch. Chuyển duy nhất
  `RB-WP5-012` sang In progress.
- 2026-08-09: Hoàn thành `RB-WP5-010`. Thêm exact canonical audit cursor,
  server-owned member ownership, operator-only raw evidence, repeatable-read
  append-log rebuild/live hash và fail-closed pseudonymous export. Logic audit sửa
  cross-member access, JSONB canonical/hash mismatch, prefix cursor plan, partial
  migration downgrade, eager HMAC authorization resolution và exception
  message-controlled classification. PostgreSQL concurrent append/drift/migration/
  12.000-row indexed-plan gates pass; BeGo Debug/Release 138/138, frontend 9/9,
  RideBound 557/557, audits sạch. Chuyển duy nhất `RB-WP5-011` sang In progress.
- 2026-08-09: Hoàn thành `RB-WP5-009`. Thêm bounded outbox relay contract,
  PostgreSQL exact-per-run-head claim/attempt fence/backoff, canonical authorized
  SignalR publisher và strict user-safe payload gate. Crash-after-send phát cùng
  stable ID/payload/hash; stale owner không mark được và slow run không chặn run
  khác. Logic audit phát hiện external send có thể hoàn tất sau lease takeover;
  bổ sung stable aggregate sequence/hash wire envelope và frontend monotonic
  duplicate/stale gate, đồng thời ghi rõ SignalR enqueue không phải durable client
  ACK. Hai fresh PostgreSQL + published Runner gates đều 131/131 ở Debug/Release,
  frontend 9/9 + lint/tsc/build. Audit dependency phát hiện và vá Auth.js/Next/
  transitive advisories; NuGet/npm về 0 vulnerability. Chuyển duy nhất
  `RB-WP5-010` sang In progress.
- 2026-08-09: Hoàn thành `RB-WP5-008`. Thêm exact decision/certificate
  materializer, atomic T2 projection/timeline/outbox, fenced ACK/T3 checkpoint và
  fresh-process reconstruction. Source audit phát hiện/sửa EF identity-map stale
  revision sau raw SQL claim và thiếu semantic cross-binding trong promise
  service order. Migration recovery frames/FK/immutability/guarded Down được kiểm
  trên PostgreSQL 17. Tám failpoint khớp clean published-Runner oracle; BeGo Debug
  và Release `/warnaserror` 125/125 không skip, frontend 7/7, RideBound required
  command 557/557. Bổ sung paper mapping RIFL/leases, giữ claim at-least-once.
  Chuyển duy nhất `RB-WP5-009` sang In progress.
- 2026-08-05: Hoàn thành refinement `RB-WP5-001` bằng ADR-025. Audit exact
  RideBound/BeGo checkouts và re-run ba baseline độc lập 557/557, 25/25, 7/7.
  In-app Browser đọc primary systems evidence về end-to-end ACK/dedup, local
  transaction activity, outbox, EF concurrency/transaction, PostgreSQL worker
  locking, hosted service và Idempotency-Key draft (ghi rõ expired). Khóa adapter
  trong BeGo gọi exact hashed NDJSON Runner, T1/T2/T3 short transactions,
  per-run lease, checkpoint/replay/hash recovery, explicit bootstrap provenance,
  default-off rollback và same-input paired B1/C1. Tạo queue `RB-WP5-002..014`;
  chỉ `002` Ready và chưa claim WP5 production implementation.
- 2026-08-05: Hoàn thành `RB-WP5-002`. Thêm BeGo Application pure contracts/
  ports và exhaustive state/idempotency/hash/protocol guards cùng 32 targeted
  tests, gồm 3 architecture boundary cases. BeGo full 57/57 và RideBound 557/557 pass; targeted
  Release/format sạch. Full BeGo Release `/warnaserror` phát hiện dependency nền
  `Microsoft.OpenApi 2.0.0` có advisory High qua ASP.NET OpenAPI 10.0.1; ghi rõ
  chưa pass thay vì tắt warning. Chuyển duy nhất `RB-WP5-003` sang In progress.
- 2026-08-05: Hoàn thành `RB-WP5-003`. Thêm 11-table EF/PostgreSQL model và
  migration guarded bằng same-run composite FK, partial unique indexes, năm
  append-only triggers và explicit empty-only Down. PostgreSQL 17 thật pass
  migration/constraint/concurrency/evidence cases; BeGo 62 pass + 1 opt-in skip,
  targeted Release 38/38 và RideBound 557/557. Chuyển duy nhất `RB-WP5-004`
  sang In progress.
- 2026-08-05: Hoàn thành `RB-WP5-004`. T1 store khóa row run và bind exact
  fingerprint/epoch/time/range, atomic event/op/run, bounded capacity; claim dùng
  DB time + ordered `SKIP LOCKED` rồi commit trước external work. Fix replay đọc
  canonical `bytea`, không đọc text đã bị `jsonb` chuẩn hóa. PostgreSQL clean-run
  race pass 5/5; Release 40/40, BeGo 64 pass + 1 opt-in skip, RideBound 557/557.
  Chuyển duy nhất `RB-WP5-005` sang In progress.
- 2026-08-05: Hoàn thành `RB-WP5-005`. Thêm pinned long-lived Runner supervisor,
  bounded pool/NDJSON/stderr/timeout và exact negotiation/provenance/context/
  ACK-checkpoint guards. Audit sửa cleanup semaphore, process-tree child leak,
  dispose/start và removed-Lazy races. 16 adversarial process tests cùng một
  published RideBound Runner online gate pass; full BeGo Release với PostgreSQL
  và Runner thật 82/82 không skip, frontend 7/7, RideBound 557/557. Chuyển duy
  nhất `RB-WP5-006` sang In progress.
- 2026-08-05: Hoàn thành `RB-WP5-006`. Thêm two-phase immutable bootstrap mapper,
  HMAC run-local pseudonymization, restricted subject links, per-field provenance,
  E7/ms ties-to-even, bounded complete directed travel matrix và exact canonical
  negotiated manifest. Audit sửa circular pre-negotiation manifest identity,
  semantic conversion ordering và zeroization path. Mapper 16/16, supervisor +
  mapper 31/31, full BeGo Release trên fresh PostgreSQL 17 và published Runner
  thật 98/98 không skip; RideBound 557/557. Chuyển duy nhất `RB-WP5-007` sang
  In progress.
- 2026-08-05: Hoàn thành `RB-WP5-007`. Thêm host/member authenticated API,
  strict DTO/request bounds, Problem Details, stable cached replay, create/finalize/
  safe queries và server-owned timer sequence. Audit sửa semantic fingerprint,
  composite idempotency locking và rate-policy precedence; PostgreSQL→published
  Runner path thực pass. Pin `Microsoft.OpenApi 2.7.5` vá GHSA-v5pm-xwqc-g5wc;
  Release `/warnaserror` 0 warning, full BeGo 116/116, frontend 7/7, RideBound
  557/557. Chuyển duy nhất `RB-WP5-008` sang In progress.
- 2026-08-03: Hoàn thành `RB-WP4-013..014` và đóng WP4 bằng ADR-024. B1
  generator/selector khớp independent oracle trên 64 fixtures; production C1
  mapper + actual OR-Tools khớp independent enumerator trên 64 fixtures, mọi
  objective optimal/gap 0. Thêm hard-gate mutation witness, actual bounded-loss
  propagation và synthetic 4–128 option curve. Final source/config/Runner/claim
  audit ở `reviews/wp1-wp4-final`; required suite 557/557, Release/format/package/
  JSON/Markdown/process/diff gates pass. Windows Application Control 0x800711C7
  không tái xuất hiện và chỉ còn historical record. Chỉ `RB-WP5-001` refinement Ready.
- 2026-08-03: Hoàn thành `RB-WP4-012`. Thêm canonical B1–B5/C1/C2 registry,
  strict WP4 configuration và domain-bound commitment+algorithm hash; manifest
  phải khớp policy ID/version/hash. B1–B4/C1/C2 map exact hierarchy sang OR-Tools,
  B5 giữ pool selector; Runner revalidate effective policy rồi stage
  ledger/certificate/plan-pool/state/hash/ACK. Solver status nằm trong hashed
  decision. 7 Algorithms + 9 Runner cases mới, gồm child-process CLI; format sạch,
  required suite 523/523. Chỉ `RB-WP4-013` chuyển Ready.
- 2026-08-03: Hoàn thành `RB-WP4-011`. Tách deterministic work budget cho
  generation/validation/solver, giữ candidate omission digest/saturation độc lập
  solver loss, và buộc mọi incumbent qua semantic validator injected. Portfolio
  fallback thử no-op rồi single-request theo lexicographic/ID; hết validation
  budget hoặc không pass trả Unknown không solution, không bịa incident. 12
  Application cases mới, format sạch, required suite 507/507. Chỉ
  `RB-WP4-012` chuyển Ready.
- 2026-08-03: Hoàn thành `RB-WP4-010`. Thêm project adapter pin
  `Google.OrTools 9.15.6755`, exact-one/at-most-one CP-SAT constraints,
  Sum/Maximum integer objectives và multi-pass lexicographic optimum fixing.
  Một worker/seed/conflict/deterministic-time budget explicit; status và bound
  không bị nâng sai, solution được canonical revalidation. 5 solver cases + 1
  architecture case mới, format sạch, required suite 495/495. Chỉ
  `RB-WP4-011` chuyển Ready.
- 2026-08-03: Hoàn thành `RB-WP4-009`. C2 dùng explicit 10-dimension warning
  profile, cùng one-pass hard gate C1, ordered warning-excess vector trước raw
  revision và không scalar hóa đơn vị. Warning phải finite-hard-bounded; toàn
  warning tắt gọi đúng selector C1. 6 Algorithms cases mới, format sạch,
  required suite 489/489. Chỉ `RB-WP4-010` chuyển Ready.
- 2026-08-03: Hoàn thành `RB-WP4-008`. C1 assess+hard-filter trong cùng WP3
  validator pass, rank accepted/worst exact ceiling PPM/ordered 10-vector/cost/
  IDs; zero hard reserve rank saturated nhưng không đổi feasibility. Khi mọi
  hard limit unbounded, bỏ treatment ranking để semantic decision đúng B1. 6
  Algorithms cases mới, format sạch, required suite 483/483. Chỉ `RB-WP4-009`
  chuyển Ready.
- 2026-08-03: Hoàn thành `RB-WP4-007`. Thêm versioned canonical plan pool vào
  Application state/checkpoint, exact semantic plan ID, shared-pool enumeration
  với exact/bounded work semantics, Pareto dominance, max-min diversity và
  executable-prefix consensus. Chỉ distinguished được apply; alternative khác
  được rebase đúng next route version. Restore kiểm tra identity, assignment/
  frozen/physical và run equality. 12 cases mới (3 Application, 5 Algorithms,
  4 Runner), format sạch, required suite 477/477. Chỉ `RB-WP4-008` chuyển Ready.
- 2026-08-03: Hoàn thành `RB-WP4-006`. Thêm B4 one-pair same-vehicle
  remove/reinsert cho waiting incumbent hoàn toàn trong mutable suffix, explicit
  cap và exact/bounded repair-loss accounting; frozen/onboard/assignment giữ
  nguyên, mọi route qua physical validator. Tách order-sensitive search-node
  digest khỏi order-insensitive omission-set digest sau khi differential case
  phát hiện route permutations bị đồng nhất. 7 Algorithms cases mới; format
  sạch, required suite 465/465. Chỉ `RB-WP4-007` chuyển Ready.
- 2026-08-03: Hoàn thành `RB-WP4-005`. B2 đánh giá cùng raw pool với mọi
  cumulative limit unbounded và chọn accepted/material/10-vector/cost/ID; B3 chỉ
  hard-freeze theo horizon/lock explicit, inclusive boundary, không có numeric
  default và không rò source budget. Sửa canonical overflow ở vector và fleet
  cost. 8 Algorithms + 1 Domain cases mới, gồm B2 16-seed raw preservation;
  format sạch, required suite 458/458. Chỉ `RB-WP4-006` chuyển Ready.
- 2026-08-03: Hoàn thành `RB-WP4-004`. Thay request-ID/hash cap bằng global
  deterministic best-first frontier; thêm work cap, tổ hợp count/saturation và
  stable omission digest. Diagnostics tách request omission, unknown-feasibility
  raw paths và known-feasible cap loss; exact mode fail-closed. 5 Algorithms
  conservation/priority/monotonic cases mới, format sạch, required suite 449/449.
  Chỉ `RB-WP4-005` chuyển Ready.
- 2026-08-03: Hoàn thành `RB-WP4-003`. Thêm backward forward-slack certificate,
  bounded cache bind run/vehicle/position/route/time/travel, executable
  current-node origin hold và revalidation + exact service equivalence. Cache
  không đảo vai validator; vượt certificate không bị suy thành infeasible. 9
  Algorithms mutation/equivalence cases mới, format sạch và required suite
  444/444. Chỉ `RB-WP4-004` chuyển Ready.
- 2026-08-03: Hoàn thành `RB-WP4-002`. Thêm solver-neutral
  `CandidateSelectionProblem`/solution/port trong Application: canonical model,
  đúng một option/vehicle, request uniqueness, ordered lexicographic
  Sum/Maximum, deterministic budget, exact bound/gap diagnostics và status
  truthful tách OPTIMAL/FEASIBLE/INFEASIBLE/UNKNOWN/MODEL_INVALID/SAFE_FALLBACK.
  20 Application adversarial cases + một Architecture boundary case; required
  `dotnet test RideBound.slnx` pass 435/435. Chỉ `RB-WP4-003` chuyển Ready.
- 2026-08-03: Hoàn thành `RB-WP4-001` refinement bằng ADR-023 và ordered queue
  `RB-WP4-002..014`. In-app Browser đọc lại nguồn bắt buộc và bổ sung waiting,
  forward-slack/feasibility, future-guidance cùng official OR-Tools/NuGet. Khóa
  common raw candidate/cap, executable origin hold, same-vehicle repair,
  canonical plan pool, multi-pass objective, deterministic solver budget,
  exact-small equivalence và WP3 publication gate. Chỉ `RB-WP4-002` Ready;
  chưa claim hay ghi production WP4 implemented tại mốc refinement.
- 2026-08-03: Re-run đúng required `dotnet test RideBound.slnx` sau khi Smart App
  Control không còn chặn fresh DLL: full solution pass 414/414 — Contracts 133,
  Domain 134, Application 34, Algorithms 48, Runner 58, Architecture 7; exit 0,
  không failed/skipped. Chuyển `0x800711C7` thành historical environment record,
  không còn là current blocker.
- 2026-08-02: Hoàn thành `RB-WP3-008..014` và đóng WP3 bằng ADR-022. Thêm
  incident/breach separation, independent full-state commitment validator,
  strict certificate/action/schema cross-binding, named configuration hash,
  Runner commitment publication/ACK, canonical checkpoint/restore và evidence
  property/mutation/exact-small/process. Audit toàn WP1–WP3 sửa thêm state-boundary,
  genesis-route, sequence exhaustion, pickup-window, breach/ledger và checkpoint
  reachability bugs; sửa cả WP2/WP3 demo pipe để stdin UTF-8 không BOM và không
  phụ thuộc PowerShell host. Browser recheck 5 paper khóa claim/tối ưu còn thiếu cho WP4.
  Inventory 414; Release build/format và WP1/WP2/WP3 clean replay pass. Required
  full solution bị host policy `0x800711C7`, được tách minh bạch thành suite/
  policy-safe/process evidence. Tạo review `docs/reviews/wp1-wp3/README.md`; chỉ
  `RB-WP4-001` refinement READY, chưa có production WP4 code.
- 2026-08-02: Bắt đầu `RB-WP3-008` sau khi đọc lại toàn bộ tài liệu bắt buộc,
  README, đặc tả và execution plan WP1–WP3; baseline full-solution tái hiện đúng
  Windows Application Control `0x800711C7` đã ghi trước đó. Chưa nâng trạng thái
  implementation; đang audit source và incident/breach boundary trước code.
- 2026-07-30: Revalidate WP2 end-to-end rồi commit riêng `07432ce`. Hoàn thành
  refinement ADR-021/queue 14 ticket và triển khai đúng nửa WP3 `001..007`:
  promise/policy/vector, shared schedule/promise projection, three-way delta,
  append-only ledger trong ACK boundary, budget và phase locks. Debug inventory
  378/378 suite evidence; chưa có incident/certificate/Runner/checkpoint, next duy nhất
  `RB-WP3-008`.
- 2026-07-30: Hoàn thành `RB-WP2-007..012`: deterministic candidate generator,
  exhaustive B1 selection/apply, independent exact-small oracle 32/32 seeds,
  default online produced Runner decisions/ACK hash chain và four-epoch tiny
  demo chạy hai clean single-file process với exact final hash. Logical test
  inventory là 333; required Debug solution pass 333/333, Release build/format
  pass và Release blocked suites pass qua policy-safe bundles/process checks.
  Release xUnit host policy chặn fresh unsigned DLL bằng `0x800711C7` trước
  assertion, được ghi như environment exception chứ không tính Release pass.
  ADR-020 đóng WP2 physical/B1; next
  duy nhất là `RB-WP3-001` refinement, chưa có ledger code.
- 2026-07-29: Hoàn thành `RB-WP2-002..006`: typed online schemas/fixtures,
  immutable Domain lifecycle/route, manifest-bound travel snapshot, atomic
  mapper/reducer/ack và independent physical validator. Debug/Release full
  solution pass 278/278; 24 small route permutations và mutation dimensions
  pass. ADR-019 khóa executable semantics; next duy nhất là `RB-WP2-007`.
- 2026-07-29: Revalidate toàn bộ WP1: Release 161/161; Debug pass 123 non-Runner
  test rồi fresh Runner assembly bị host policy chặn trước discovery. Phát hiện
  payload-only dedup hash nhận nhầm retry đổi `simTimeMs`; sửa bằng ADR-017 để
  hash toàn canonical eventBatch, thêm regression conflict và replay qua hai
  clean runner process. Hoàn thành `RB-WP2-001` refinement bằng ADR-018 và
  execution plan `RB-WP2-002..012`; next duy nhất là `RB-WP2-002`.
- 2026-07-29: Hoàn thành `RB-WP1-008..015` và đóng WP1/Q1: event/decision/error
  contracts, hash chain, async NDJSON runner, lifecycle/idempotency/failure
  semantics, đúng 10 golden fixture và exact replay/tamper proof. Release full
  suite pass 157/157 tại mốc đóng; assertion đồng bộ vocabulary thêm sau đó pass,
  đưa Contracts lên 115 và inventory lên 158. Format/build/vulnerability audit
  sạch. Lần full-suite cuối bị enterprise Code Integrity chặn fresh Runner DLL
  với `0x800711C7` trước assertion cả ở Release; Debug cũng bị policy này chặn
  các fresh DLL. Next duy nhất là refinement `RB-WP2-001`, chưa có WP2 code.
- 2026-07-29: Hoàn thành `RB-WP1-005..007`: schema/inventory/compatibility
  assets, hello capability negotiation và immutable initialize identity.
  Required full solution test pass 114/114; Release build/format/dependency audit
  sạch. Release-only Domain smoke vẫn bị Windows Application Control chặn
  `0x800711C7` sau khi 113 test khác pass; next là `RB-WP1-008`.
- 2026-07-29: Hoàn thành `RB-WP1-002..004`: contract fixture harness, typed
  envelope/validation, canonical unit conversion và exact canonical JSON bytes.
  Contracts 66/66 và Architecture 7/7 pass từ independent artifacts path;
  Release build/format/dependency audit sạch. Full solution test vẫn bị Windows
  Application Control chặn Domain smoke (`0x800711C7`); next là `RB-WP1-005`.
- 2026-07-29: Hoàn thành docs ticket `RB-WP1-001` bằng ADR-014 và protocol
  decision checklist; thu hẹp O-006 sang FleetPy executable preflight; chuyển
  `RB-WP1-002` thành next/`READY`. Không thêm runtime/schema code và chưa nâng
  trạng thái implementation của WP1. Full RideBound regression pass 8/8; kiểm
  48 Markdown file có 0 internal link hỏng và 0 code fence lệch.
- 2026-07-28: Chuyển WP1 sang `READY`, thêm delivery policy và 15 ticket chi tiết;
  next action là `RB-WP1-001`. `dotnet test RideBound.slnx` hiện pass 8/8;
  không có implementation status nào được nâng.
- 2026-07-28: Sửa false positive architecture test trên Linux và mở rộng CI quality
  gates; Release build local sạch, test local chờ Linux CI xác nhận do Windows
  Application Control chặn nạp DLL test.
- 2026-07-28: Tách RideBound thành Git repository riêng, hoàn tất WP0 scaffold và chuyển next action sang WP1.
- 2026-07-27: Hoàn tất kiểm tra cấu trúc và mã hóa; chuyển docs sang `COMPLETE_V1_VERIFIED_PENDING_USER_REVIEW`.
- 2026-07-27: Khởi tạo status log và docs v1.
