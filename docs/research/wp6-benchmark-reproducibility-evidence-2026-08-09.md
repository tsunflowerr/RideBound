# WP6 benchmark/reproducibility evidence — 2026-08-09

## 1. Phạm vi và cách đọc

Tài liệu này ghi phần nghiên cứu bổ sung dùng để khóa `RB-WP6-001`. Các nguồn được
đọc bằng in-app Browser từ trang chính thức/publisher. Mục tiêu là chuyển từng nguồn
thành một cơ chế kiểm toán được cho WP6, không dùng uy tín của paper/standard để thay
thế test của RideBound.

WP6 chỉ tạo common harness, normalizer, result contract, metric oracle và bundle tái
lập. WP6 **không** chạy confirmatory experiment, không tự tạo ACM badge, không chứng
minh effectiveness/non-inferiority/SLA và không chọn O-002/O-003/O-004.

## 2. Public dataset được khóa cho WP6

### 2.1 FleetPy Manhattan case-study data

- Dataset: [FleetPy: Input Data for Manhattan Case Study](https://doi.org/10.5281/zenodo.15187906).
- All-versions DOI: `10.5281/zenodo.15187905`.
- Version: `1.0`, công bố ngày 2025-04-10.
- Tác giả: Roman Engelhardt, Florian Dandl.
- License hiển thị trên Zenodo: `CC BY 4.0`.
- File: `FleetPy_Manhattan.zip`, khoảng `408.9 MB`.
- Checksum do Zenodo công bố: MD5
  `8b11882ae9c6d87f666bf6e006806744`.
- Nội dung README của archive mô tả demand NYC TLC trong tuần
  `2018-11-11` đến `2018-11-18`, lọc origin/destination Manhattan, OSM network,
  các demand fractions 5/10/20/50/75%, travel-time factors, node/edge/GeoJSON,
  matrices và zones.

Quyết định chuyển giao:

1. Đây là source public chính cho WP6 normalizer và WP7 Layer 2.
2. Downloader phải kiểm remote MD5 **và** ghi local SHA-256 trên exact downloaded
   bytes. MD5 chỉ kiểm khớp artifact Zenodo; SHA-256 là identity nội bộ.
3. Raw archive nằm trong local ignored cache, read-only sau verification; không sửa
   archive và không commit file 408.9 MB.
4. Source-controlled tiny/medium derivative chỉ được tạo sau khi kèm attribution,
   exact transformation recipe, source-row/member inventory hash và CC BY notice.
5. Trip/request data là public operational trace, không phải user-consented label cho
   willingness-to-share, satisfaction, commitment budget, disability hay fairness.
6. Request ID của benchmark phải là pseudonymous ID từ source row ordinal/hash;
   không tạo hoặc suy diễn person identity.

### 2.2 FleetPy reproducibility paper

[FleetPy paper](https://doi.org/10.1186/s12544-026-00823-3), xuất bản ngày
2026-07-15, mô tả simulator modular và common benchmark data Manhattan/Chicago/
Munich. Paper cũng mô tả `zip_studies.py`: scan scenario configuration rồi gom
inputs, configs, evaluation scripts, source và optional results.

Quyết định chuyển giao:

- WP6 bundle học cơ chế “scenario config + exact inputs + evaluation source +
  optional raw results”, nhưng dùng contract/hash/verifier riêng của RideBound.
- Không suy rằng dùng FleetPy dataset làm kết quả tự động comparable. Comparability
  chỉ tồn tại khi event semantics, fleet, travel realization, budgets, failures và
  metric definition đều được công bố.
- DOI liên quan được paper ghi: Manhattan `10.5281/zenodo.15187906`, Chicago
  `10.5281/zenodo.15189440`, Munich `10.5281/zenodo.15195726`. WP6 chỉ khóa
  Manhattan; hai city còn lại là future robustness source, không tự mở scope.

## 3. Canonical representation và hashing

### 3.1 RFC 8785 — JSON Canonicalization Scheme

[RFC 8785](https://www.rfc-editor.org/rfc/rfc8785) là RFC Informational, không phải
Internet Standards Track. Nó định nghĩa canonical JSON bằng I-JSON constraints,
ECMAScript primitive serialization, không whitespace và recursive deterministic
property sorting. RFC yêu cầu reject duplicate property, invalid Unicode, NaN và
Infinity; số vượt IEEE-754 interoperable range nên biểu diễn bằng string.

Quyết định chuyển giao:

- WP6 dùng **RideBound Canonical JSON v1**, là strict JCS-compatible subset đã có
  trong WP1: UTF-8, unique property, ordinal UTF-16 property order, array order giữ
  nguyên, valid Unicode, không `null`, không float, chỉ safe integer.
- Time/distance/rate/resource quantities dùng scaled integer với unit trong field
  name/contract; hash/large unsigned number/timestamp subtype dùng string khi cần.
- Không gọi implementation hiện tại là “full RFC 8785 implementation”; nó cố ý hẹp
  hơn. Cross-process vectors phải chứng minh accepted-domain bytes giống nhau.
- Mỗi hash dùng domain separator và length-prefixed frames; không hash JSON tùy ý
  từ serializer/platform khác.

Claim limit: canonical bytes giúp repeatable hashing; nó không chứng minh semantic
scenario đúng hoặc experiment tái lập nếu source/config/runtime chưa được pin.

## 4. Deterministic seed hierarchy

### 4.1 Random123

[Parallel Random Numbers: As Easy as 1, 2, 3](https://doi.org/10.1145/2063384.2063405)
và [official Random123 repository](https://github.com/DEShawResearch/random123) dùng
counter/key thay cho mutable global RNG state. Một tuple counter/key xác định output,
phù hợp parallel execution và không phụ thuộc số lần gọi trước đó.

Quyết định chuyển giao:

- WP6 lấy **cơ chế addressable randomness**, không copy algorithm/library Random123.
- Implementation dùng HMAC-SHA-256 domain-separated với master seed 256-bit và
  length-prefixed labels: scenario hash, repeat index, component ID và optional
  stable item ID.
- Sampling/order dùng hash ranking trên stable IDs. Cấm `Random.Shared`, GUID, clock,
  process order, dictionary enumeration và call-count-dependent hidden RNG.
- Mọi derived seed/digest được ghi vào plan/result manifest. Component cần `int32`
  lấy 31 low-risk deterministic bits theo contract đã version.

Claim limit: đây là deterministic seed derivation học từ counter-based design; WP6
không được viết rằng mình “dùng Random123” hoặc kế thừa statistical guarantees của
Random123.

## 5. Bundle integrity và provenance

### 5.1 RFC 8493 — BagIt

[RFC 8493](https://www.rfc-editor.org/rfc/rfc8493) mô tả BagIt: payload dưới
`data/`, `bagit.txt`, payload manifests, optional tag files/tagmanifest, completeness
và validity dựa trên cryptographic hashes.

Quyết định chuyển giao:

- WP6 bundle là BagIt-compatible bag dùng SHA-256.
- RideBound thêm strict profile: exact allowlisted path/type/role, normalized `/`
  path, no traversal/symlink, no duplicate/case-collision và no extra file.
- `manifest-sha256.txt` bảo vệ payload, `tagmanifest-sha256.txt` bảo vệ tag files.
- Logical `bundle-manifest.json` không tự hash chính nó để tránh self-reference;
  BagIt payload manifest hash file này và verifier kiểm union inventory đầy đủ.
- Missing, extra, tamper, wrong length/type/hash, source/runtime mismatch hoặc metric
  oracle mismatch đều làm bag invalid trước thống kê.

Claim limit: BagIt validity chứng minh inventory/hash consistency, không chứng minh
kết quả khoa học đúng.

### 5.2 W3C PROV-DM

[W3C PROV-DM Recommendation](https://www.w3.org/TR/prov-dm/) mô hình hóa entity,
activity, agent, derivation, attribution, collection và bundle.

Quyết định chuyển giao:

- Dataset/archive/scenario/transcript/metric/bundle là entities.
- Download/normalize/execute/recompute/package/verify là activities.
- Tool/source commit/runtime/machine/operator role là agents hoặc software agents.
- WP6 dùng field-level derivation table và machine-readable provenance record; không
  cần triển khai toàn bộ PROV ontology trong ticket đầu.

### 5.3 FAIR principles

[FAIR Guiding Principles](https://doi.org/10.1038/sdata.2016.18) nhấn mạnh
findability, accessibility, interoperability, reuse, persistent identifiers,
metadata và provenance cho máy và người.

Quyết định chuyển giao:

- Dataset descriptor giữ DOI/version/license/citation/checksum và retrieval time.
- Scenario/result identifiers là content-bound, không chỉ filename.
- Metadata vẫn phải còn khi raw data không được redistribute.

Claim limit: WP6 cải thiện FAIRness của artifact; không tự chấm hoặc tuyên bố đạt một
certification FAIR bên ngoài.

### 5.4 Datasheets for Datasets

[Datasheets for Datasets](https://arxiv.org/abs/1803.09010) yêu cầu ghi động lực,
thành phần, collection process, preprocessing, recommended use, distribution,
maintenance và giới hạn.

Quyết định chuyển giao:

- Mỗi dataset descriptor có motivation, composition, collection/provenance,
  transformation, allowed/forbidden use, PII/location notes, retention và caveats.
- WP6 không dùng một URL/checksum đơn lẻ thay cho dataset documentation.

## 6. Reproducible computational research

### 6.1 Sandve et al. — Ten Simple Rules

[Ten Simple Rules for Reproducible Computational Research](https://doi.org/10.1371/journal.pcbi.1003285)
đề xuất: track cách tạo mọi result, tránh manual data manipulation, archive exact
external versions, version scripts, giữ intermediate standardized results, record
seeds, giữ raw data sau plots, hierarchical outputs, nối claim với results và công
bố scripts/runs/results.

Quyết định chuyển giao:

- Mọi generated file có producing activity/source/config hash.
- Không có spreadsheet/manual edit trong data-to-metric path.
- Raw transcript và typed negative/failure records được giữ.
- Metric calculator và independent oracle đều được hash-bound.
- Bundle layout theo scenario/arm/repeat/run và có command reproduction cụ thể.

Claim limit: tuân thủ discipline không đồng nghĩa người khác đã tái tạo kết quả.

### 6.2 ACM artifact terminology/badging

[ACM New Changes to Badging Terminology](https://www.acm.org/publications/badging-terms)
khóa terminology version 1.1:

- repeatability: cùng team, cùng setup;
- reproducibility: team khác, cùng artifact/setup;
- replicability: team khác, artifact/setup độc lập;
- các artifact badge và result badge là đánh giá bên ngoài, không phải nhãn do tác giả
  tự cấp.

Functional artifact cần documented, consistent, complete, exercisable và có evidence
để validate; Reusable thêm cấu trúc/tài liệu/community norms; Available cần permanent
public archival repository; Results Reproduced/Replicated cần independent team.

Quyết định chuyển giao:

- WP6 target internal properties tương tự Functional/Reusable: documented,
  consistent, complete, exercisable và self-verifying.
- Chỉ ghi `same-team-clean-process repeatability verified` khi đúng.
- Không ghi “ACM badge”, “Artifacts Evaluated”, “Available”, “Results Reproduced”
  hoặc “Replicated” nếu chưa qua quy trình/independent team tương ứng.

### 6.3 NASEM, Peng, Munafò và Unicode — claim boundary thực thi

[NASEM, *Reproducibility and Replicability in Science* (2019)](https://www.nationalacademies.org/read/25303/chapter/2)
định nghĩa computational reproducibility là cùng input data/code/steps/conditions,
trong khi replicability dùng study/data mới. [Peng, *Reproducible Research in
Computational Science* (Science, 2011)](https://pmc.ncbi.nlm.nih.gov/articles/PMC3383002/)
gọi reproducibility là minimum standard khi full replication chưa khả thi và nói rõ
reproducible analysis không bảo đảm quality, correctness hoặc validity.

[Munafò et al., *A manifesto for reproducible science* (Nature Human Behaviour,
2017)](https://www.nature.com/articles/s41562-016-0021) nêu analytical flexibility,
HARKing, confirmation/hindsight bias và over-interpretation là threat cần transparency,
preregistration và iterative evaluation. [Unicode UTS #39](https://www.unicode.org/reports/tr39/)
khóa rủi ro NFKC/default-ignorable/confusable; RideBound chỉ áp dụng scoped skeleton và
không claim full UTS conformance.

Áp dụng cụ thể vào `RB-WP6-010`:

- `wp6-mechanical-only-v1` nằm trong source/verifier, được emit canonical và SHA-bind;
  không có CLI chọn profile mềm hơn;
- sáu caveat exact tách mechanical/same-team/local controls khỏi confirmatory,
  effectiveness, ACM và independent-team conclusions;
- scanner chỉ đọc explicit claim-bearing selectors, không quét raw trip/transcript hay
  source prose; câu caveat được mask trước forbidden scan;
- NFKC/casefold/diacritic, punctuation dual skeleton, common Greek/Cyrillic confusable
  và default-ignorable rejection trả typed path/selector/original/normalized witness;
- stage 10 recompute report; mutation vừa sửa checksum/logical manifest vẫn không thể
  biến bundle mechanical thành effectiveness/reproduction artifact.

### 6.4 McKeeman — Differential Testing for Software

[Differential Testing for Software](https://shiftleft.com/mirrors/www.hpl.hp.com/hpjournal/dtj/vol10num1/vol10num1art9.pdf)
(William M. McKeeman, *Digital Technical Journal* 10(1), 1998, trang 100–107)
đưa cùng input qua nhiều implementation rồi dùng khác biệt output như một oracle tìm
lỗi khi không có một đáp án triển khai duy nhất đủ đáng tin. Bài này được mở lại bằng
in-app Browser ngày 2026-08-11; metadata tìm kiếm xác nhận đúng tác giả, tạp chí, năm
và số trang.

Quyết định chuyển giao cho `RB-WP6-008`:

- production calculator và oracle là hai executable/source tree khác nhau;
- oracle chỉ dùng BCL `JsonDocument`, primitive collections, `BigInteger` và SHA-256;
  không ProjectReference tới Contracts/Benchmarking và không gọi model/calculator
  production;
- hai phía tự parse, tự dựng lifecycle/cohort/window/vector/resource/evidence identity,
  rồi so toàn bộ 132 canonical rows và metric-set hash byte-exact;
- mutation matrix thay đổi request, action, promise vector, window, thứ tự transcript,
  denominator, resource sample và overflow; mismatch là `metric.oracle-mismatch`, input
  sai chronology/lifecycle bị từ chối có kiểu;
- source/assembly oracle được hash-bind để bundle sau này chứng minh executable nào đã
  tạo reference rows.

Claim limit: differential agreement làm giảm rủi ro lỗi triển khai đơn lẻ nhưng không
chứng minh specification đúng, không loại trừ correlated bug và không phải independent
scientific reproduction. Vì vậy WP6 vẫn cần protocol/store verifier, mutation tests và
raw evidence; không dùng “hai chương trình cùng ra một số” thay cho semantic review.

### 6.5 Dolan & Moré — performance profiles

[Benchmarking Optimization Software with Performance Profiles](https://arxiv.org/abs/cs/0102001)
(Elizabeth D. Dolan, Jorge J. Moré, *Mathematical Programming* 91, 2002, 201–213)
đề xuất biểu diễn phân phối performance ratio trên toàn bộ tập bài toán thay vì kết
luận từ một trung bình đơn. Trang arXiv/paper metadata và abstract được mở lại bằng
in-app Browser ngày 2026-08-11.

Quyết định chuyển giao có giới hạn:

- WP6 giữ metric ở mức từng run/scenario/arm/repeat và giữ cả failure/exclusion/planned
  denominator; không chỉ xuất một mean dễ che khuất strata hoặc selective failure;
- exact pairing class, scenario identity và complete plan grid được giữ để WP9 có thể
  xây performance profile trên các cặp thực sự so sánh được;
- WP6 **không** tự tạo performance ratio/profile: metric direction, zero/negative
  handling, failure penalty, statistical estimand và confirmatory scenario set phải
  được preregister ở WP8/WP9 trước khi aggregate;
- một profile về runtime/objective sau này cũng không được thay thế các dimension
  commitment riêng hoặc planned-run failure report.

Claim limit: bài báo cải thiện kỷ luật so sánh solver/algorithm; nó không cho phép gọi
tiny/medium mechanical gate là bằng chứng C1 hiệu quả hơn B1.

### 6.6 RFC 8493 + Library of Congress BagIt conformance suite — implementation audit

Ngày 2026-08-11, in-app Browser mở lại
[RFC 8493 — The BagIt File Packaging Format v1.0](https://datatracker.ietf.org/doc/html/rfc8493)
và [Library of Congress BagIt conformance suite](https://github.com/LibraryOfCongress/bagit-conformance-suite).
RFC phân biệt **complete** (đủ payload/tag inventory) với **valid** (mọi checksum đã
verify), yêu cầu payload manifest liệt kê mọi payload file đúng một lần, dùng `/`,
không trỏ ra ngoài `data/`; tag manifest phải liệt kê payload manifest và không được
liệt kê chính tag manifest. RFC cũng khuyến nghị tránh tên chỉ khác case/Unicode
normalization. Conformance suite tổ chức riêng valid/invalid/warning và lưu ý line
ending conversion có thể làm validation sai.

Áp dụng cụ thể vào `RB-WP6-009`:

- builder phát exact UTF-8/LF `bagit.txt`, payload/tag SHA-256 manifests,
  `Payload-Oxum`, reviewed `verify.ps1`; logical manifest không tự hash và chỉ được
  BagIt payload manifest hash từ bên ngoài;
- profile RideBound chặt hơn RFC portability floor: từ chối absolute/traversal,
  percent/control, reparse/junction, case/Unicode-normalization collision, Windows
  device/trailing-dot/space và mọi missing/extra file;
- BagIt byte validity chỉ là stage 3. Stages sau tái dựng plan/grid, scenario/dataset,
  runtime/source provenance, Runner transcript/ACK/checkpoint, terminal denominator
  và metric từ raw evidence. Vì vậy checksum đúng nhưng semantic sai vẫn fail;
- exact Git HEAD + raw dirty-status hash + per-file path/length/SHA ràng buộc working
  tree thật; không thay thế bằng base commit. Runner/Contracts/harness/oracle/verifier
  assemblies đều có role/hash và external verifier phải tự hash chính binary của nó;
- production/oracle equality không được dùng vòng tròn: verifier còn tái tính
  production rows từ raw transcript. Mutation sửa đồng thời hai file metric vẫn fail;
- clean-process verifier không sửa sealed bag và chỉ tạo sidecar mới bên ngoài.

Claim limit: RFC/LOC giúp chứng minh packaging completeness/integrity và test taxonomy;
chúng không chứng minh thuật toán đúng, benchmark không bias, hiệu quả, độc lập tái lập
hay đủ điều kiện ACM badge.

### 6.7 FleetPy 2026 architecture paper — simulator boundary cho medium gate

Ngày 2026-08-12, in-app Browser đọc trực tiếp bài open-access
[FleetPy: an open source simulator for reproducible research on mobility-on-demand services](https://doi.org/10.1186/s12544-026-00823-3).
Bài báo mô tả FleetPy là simulator agent-based, modular giữa network, demand/user,
fleet control, vehicles và output evaluation. Fleet control dùng `PlanRequest`,
`VehiclePlan`, ordered `PlanStop`; assignment phải giữ pickup/drop-off time constraints,
capacity và tối đa một plan trên mỗi vehicle. Bài phân biệt immediate offer với batch
offer, nêu insertion/Jaw, Alonso-Mora và Simonetto là các trade-off khác nhau, đồng
thời cho phép graph travel times tĩnh, global factor hoặc edge-specific dynamic.

Áp dụng cụ thể:

- WP6 medium chỉ dùng versioned Manhattan input và exact RideBound Runner mechanics;
  nó chưa có FleetPy simulation clock, vehicle motion, user choice hoặc control adapter;
- source request/network/fleet và synthetic policy overlay phải tách provenance; public
  taxi data không chứa RideBound commitment preference hoặc satisfaction label;
- exact action phải bind request/vehicle/ordered stops và hard constraints, nhưng
  instant-drain lifecycle chỉ kiểm state machine, không được thay FleetPy movement;
- wait/ride/occupancy/distance/emission do nonphysical driver tạo ra bị cấm dùng làm KPI;
  WP7 phải nối cùng Runner vào FleetPy module/interface thay vì reimplement policy;
- future comparison phải pin service/network/demand/fleet-control/user-model resolution,
  vì bài báo nhấn mạnh khác setup làm fair comparison khó.

Claim limit: modular simulator và public input cải thiện comparability/reproducibility;
chúng không biến WP6 instant-drain gate thành Layer-2 simulation hay effectiveness.

### 6.8 Engelhardt–Dandl–Bogenberger speed-up heuristic — tối ưu có loss contract

In-app Browser cũng đọc trực tiếp
[Speed-up Heuristic for an On-Demand Ride-Pooling Algorithm](https://arxiv.org/abs/2007.14877).
Bài báo cho thấy multi-step ride-pooling có bước tốn kém tăng nhanh theo fleet/request;
prefilter vehicle theo ba rule đạt hơn 8× ở bước đắt nhất và khoảng 2,5× toàn pipeline
trong case study, trong khi giữ request served gần như không đổi và khoảng 70% saved
distance. Rule I ưu tiên assigned vehicle theo route/request compatibility; Rule II
cluster idle vehicle theo travel direction; Rule III chọn idle vehicle gần với load
balancing. Paper xử lý đây là heuristic giảm search space, không phải exact pruning.

Áp dụng/không áp dụng cho RideBound:

- `InsertionCandidateGenerator` hiện enumerates all vehicle để giữ exact-small
  completeness. Đây là bottleneck đáng benchmark ở `RB-WP6-013/014`, không phải lý do
  âm thầm bỏ candidate;
- nếu thêm scalable prefilter, exact-small vẫn phải enumerate toàn bộ và làm oracle;
  medium/large phải ghi eligible/generated/pruned counts, loss bound/diagnostic, hard
  feasibility preservation và paired B1/C1 fairness trên cùng prefilter contract;
- random request order và random initialized 2-D vehicle vectors trong paper không được
  copy trực tiếp. RideBound phải dùng canonical event order hoặc addressable HMAC rank/
  vector bind seed+request+vehicle, rồi chứng minh permutation/parallel determinism;
- nearest/direction/compatibility chỉ là ranking heuristic. Candidate sau prefilter vẫn
  phải đi qua WP2 physical và WP3 commitment validator; không heuristic nào được đổi
  hard violation thành feasible;
- phải báo quality/runtime curve và negative strata trước khi chọn threshold. Con số
  speed-up của Munich case study không được coi là guarantee cho RideBound/FleetPy data.

Claim limit: paper chỉ tạo thiết kế cho deterministic, auditable prefilter experiment;
WP6 chưa được claim speed-up hoặc solution-quality preservation nếu chưa có paired loss
evidence và exact-small agreement.

## 7. Failure/missingness và reporting discipline

Các paper/review đã có trong corpus RideBound (`docs/11`, `docs/21`, extra80 B08/B32)
cho thấy benchmark routing thường dùng private instances, baseline/metric không đồng
nhất và dễ bỏ failure. Sandve/ACM/BagIt bổ sung audit discipline nhưng không tự định
nghĩa denominator.

Quyết định WP6:

1. Planned run grid là denominator gốc; mọi planned arm/repeat phải sinh đúng một
   terminal run record.
2. Pre-outcome capability/data exclusion dùng only preregistered rule và vẫn giữ
   exclusion row cùng affected denominator.
3. Timeout, crash, solver unknown, divergence và incomplete output là failure, không
   phải metric 0 và không được silently drop/rerun.
4. Mỗi metric row ghi numerator, denominator ID/value, unit, scope và missing status.
5. Production calculator phải khớp independent raw-transcript oracle trên tiny bound;
   mismatch invalidate bundle trước mọi aggregate/statistics.
6. Negative results và policy-worse strata vẫn nằm trong bundle.

## 8. Tóm tắt paper/standard → mechanism → gate → claim limit

| Nguồn | Mechanism được áp dụng | Gate WP6 | Không được claim |
|---|---|---|---|
| FleetPy/Zenodo | immutable public source + exact normalizer | checksum/license/derivative provenance | standard data tự động làm comparison fair |
| RFC 8785 | canonical accepted-domain bytes | cross-process vectors/hash | full JCS conformance ngoài subset |
| Random123 | addressable seed hierarchy | order/parallel invariance | dùng Random123/statistical guarantee |
| RFC 8493 + LOC conformance suite | strict BagIt payload/tag/oxum/script + ordered semantic verifier | missing/extra/tamper/path/reparse/provenance/raw-metric mutation rejection | scientific validity/independent reproduction |
| W3C PROV | entity/activity/agent derivation | field/file provenance closure | full ontology conformance |
| FAIR | DOI/license/metadata/reuse | dataset descriptor completeness | external FAIR certification |
| Datasheets | composition/use/limit documentation | descriptor required fields | absence of bias/privacy risk |
| Sandve | raw/intermediate/seed/version discipline | clean-process reproduction | independent reproduction |
| ACM policy | precise artifact vocabulary | claim checker | ACM badge/reproduced/replicated |
| NASEM 2019 + Peng 2011 | reproducibility là cùng computation/minimum standard, không phải correctness/replication | exact same-team caveat + no independent/validity promotion | reproduced/replicated/correctness/effectiveness |
| Munafò et al. 2017 | analytical flexibility/HARKing/over-interpretation cần reporting boundary | immutable profile + non-confirmatory caveat | post-hoc confirmatory/effectiveness conclusion |
| Unicode UTS #39 | NFKC/default-ignorable/confusable anti-spoof mechanisms | bounded dual skeleton + typed Unicode mutation rejection | full UTS #39 conformance/general NLP completeness |
| McKeeman differential testing | hai implementation độc lập trên cùng raw evidence | exact 132-row/hash comparison + mutation matrix | specification correctness/independent reproduction |
| Dolan–Moré performance profiles | giữ run-level paired rows và failure denominator để aggregate sau preregistration | complete grid/pairing/negative evidence | WP6 effectiveness/ranking/profile claim |
| FleetPy 2026 architecture | modular simulator boundary, exact service/network/demand/control inputs | public source mechanics tách khỏi WP7 closed loop | instant-drain là simulator/KPI/effectiveness |
| Engelhardt et al. speed-up heuristic | vehicle compatibility/direction/distance prefilter là heuristic có measured loss | exact-small oracle + deterministic HMAC rank/vector + pruning/loss diagnostics nếu hiện thực | copy random shortcut hoặc claim published speed-up/quality guarantee |

## 9. Kết luận cho ADR-026

Evidence đủ để khóa `RB-WP6-001` theo hướng fail-closed:

- FleetPy Manhattan v1/CC BY 4.0 là source public chính;
- normalized contracts dùng integer/string canonical subset và content hashes;
- seed là addressable HMAC hierarchy;
- execution luôn gọi exact pinned Runner process;
- failures/exclusions/negative results là first-class append-only evidence;
- metric phải tái tính độc lập từ raw transcript;
- bundle dùng BagIt-compatible strict inventory + PROV-like derivation;
- claim checker chỉ cho mechanical/development wording ở WP6.

Không nguồn nào cho phép WP6 chọn commitment budget/material threshold/margin hoặc
nâng mechanical reproducibility thành effectiveness. Các quyết định đó vẫn thuộc
WP8/pilot/preregistration như roadmap hiện hành.
