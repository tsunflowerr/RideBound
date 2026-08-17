# WP6: benchmark harness

## 1. WP6 giải quyết thiên lệch ở đâu?

Một benchmark dễ sai ngay cả khi Runner đúng: tải nhầm release, zip traversal, chọn row
theo thứ tự file, đổi seed giữa arm, drop timeout, tính metric từ summary tự khai, hoặc
bundle thiếu file. WP6 khóa từng boundary này thành contract/machine-checkable evidence.

## 2. Dataset và safe extraction

`DatasetSourceRegistry` pin dataset ID/version/DOI/license/URL/length/MD5/SHA-256 và
member inventory. `VerifiedDatasetDownloader` tải vào staging ngoài repo, giới hạn byte,
rehash rồi atomic promote; cache cũ cũng phải verify lại.

`SafeZipExtractor` inventory trước extraction; cấm absolute/traversal, case collision,
symlink/reparse, member lạ, oversize và compression bomb. Mỗi extracted member được
rehash; existing output chỉ reuse nếu exact, không overwrite.

## 3. Normalizer

`FleetPyManhattanNormalizer` rehash bốn member đã đăng ký, strict-parse CSV, giữ directed
arc, kiểm SCC và không invent reverse/zero/Euclidean arc. HMAC rank chọn row/node/fleet
độc lập policy; pseudonym cũng HMAC. Time/unit dùng integer/ties-to-even. Report giữ
conservation selected/not-selected/excluded; 21.400 input không thể biến mất âm thầm.

Public-medium hiện là deterministic derivative để kiểm mechanics. Driver instant-drain
không phải FleetPy closed-loop, nên wait/ride KPI không được dùng làm effectiveness.

## 4. Plan, pairing và seed

`BenchmarkPlanCompiler` yêu cầu canonical config/capability, semantic WP4 policy binding,
registered pairing class và bounded grid tối đa 1.000.000 run. Common pairing buộc cùng
candidate/validator/solver work. HMAC seed tree tách arm order, adapter RNG, simulator
RNG, solver RNG và failure injection; stable item ID bind arm khi cần.

Run ID bind plan/scenario/arm/repeat/attempt. Warm-up là executable run và có terminal
record nhưng không vào measured summary.

## 5. External Runner supervisor

`ExternalProcessSupervisor` chỉ chạy exact pinned artifact đã staged, clear environment,
bound stdin/stdout/stderr/wall/CPU/memory/process count và kiểm pre/post artifact
inventory. Windows dùng Toolhelp process tree; non-Windows ghi rõ root-only limitation.
Mọi failure giữ partial raw evidence và được `ExternalProcessTerminalMapper` đổi thành
typed terminal record; evidence thiếu thì persistence-incomplete, không phải metric 0.

## 6. Append-only store

`AppendOnlyRunStore` tạo run directory mới, ghi input/output/observation/resource/log/
failure/exclusion bằng create-new + hash. Recovery kiểm inventory/schema/contract/hash,
rebuild observation index từ transcript và byte-compare. Mỗi planned run phải có đúng
một terminal status succeeded/failed/excluded.

## 7. Metric và oracle

`MechanicalMetricCalculator` chỉ tính outcome metric cho succeeded run. Nó kiểm toàn
lifecycle, arrival cohort, decision window, promise/revision và resource samples; ratio
dùng integer/BigInteger ties-to-even. Semantic evidence hash bind raw input/output/index/
decision; resource rows được tách.

`MechanicalMetricOracleVerifier` là implementation độc lập, không reference production
calculator/model. Bundle verifier còn tự recompute production metric từ raw. Production
rows, oracle rows, registry/window/run coverage và metric-set identity phải byte-exact;
mismatch là `metric.oracle-mismatch` và chặn bundle.

## 8. Strict bundle và claim checker

Bundle là strict BagIt-compatible no-extra layout. Logical manifest liệt kê mọi data
artifact trừ chính nó; BagIt manifest giải self-reference. Verifier đi qua path/layout,
BagIt, logical/provenance, conservation, transcript/store, metric/oracle và claims.

`ArtifactClaimChecker` chỉ scan các surface được phép và yêu cầu sáu caveat exact. Nó
normalize NFKC/case/diacritic/punctuation và một tập confusable để chặn claim lách chữ;
không scan trip/scenario/transcript nhằm tránh false positive từ dữ liệu.

## 9. Fresh closure evidence 2026-08-13

- Tiny A: 8/8 success, bundle
  `79cb321a2aa079c34ddfa49061387e78990f14b7bb368abb762e497c30b27b04`,
  external verifier exit 0 cùng hash.
- Medium H/I trên exact source cuối: mỗi process 8/8 success, 0 failure/exclusion.
- H/I: 16/16 semantic top-level và 72/72 semantic per-run fields exact.
- 8/8 full resource rows khác nhau; physical bundles
  `89a43921...d9d8` và `a954db62...94e9` khác hợp lệ và đều external-verify.

Hash H/I khác F/G và D/E lịch sử vì exact source/status inventory khác; H/I mới là
repeat pair của source cuối. Không được so chéo các pair như nondeterminism khi
provenance khác.
