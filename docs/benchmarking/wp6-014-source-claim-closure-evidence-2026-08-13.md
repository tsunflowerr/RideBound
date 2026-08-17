# RB-WP6-014 source/claim closure evidence — 2026-08-13

## 1. Phạm vi

Audit này đọc lại toàn bộ Markdown, production source WP1–WP6, các boundary BeGo
WP5 liên quan, paper/standard primary bằng in-app Browser và chạy lại pipeline thật.
Mục tiêu là tìm lỗi logic/claim bị test pass che, không phải tăng test count.

## 2. Source verdict theo work package

| WP | Boundary đã kiểm | Verdict |
|---|---|---|
| WP1 | strict envelope/payload, canonical JSON, domain/length-framed hash, session/retry | Không có ambiguity/retry advance defect còn mở |
| WP2 | lifecycle, atomic reducer, pending/ACK, frozen route, full physical schedule | Candidate không thể tự hợp thức hóa route; no-reassignment giữ nguyên |
| WP3 | schedule→promise→three-way delta→lock→budget→ledger→certificate→checkpoint | Path-dependent 10D cumulative mechanism có substance; incident tách normal publication |
| WP4 | bounded generation/loss, exact-small oracle, B1–B5/C1/C2, CP-SAT/fallback | Comparator/work asymmetry được chặn; không false global-optimum claim |
| WP5 | pinned Runner, T1/T2/T3, lease/fence, replay/checkpoint, outbox/privacy | BeGo orchestration không tái hiện/nới WP3; same Runner vẫn là authority |
| WP6 | source/data/normalizer/plan/process/store/metric/oracle/bundle/claim | Terminal conservation, raw recomputation và semantic verifier giữ đầy đủ |

Không phát hiện unresolved correctness/contract defect yêu cầu code change. Một blank
line cơ học trong `RunnerSession` được format gate xử lý. Closure không thêm heuristic
mới: paper về sparse/direction filter nêu quality-loss, nên chưa đủ oracle/loss evidence.

## 3. Paper/claim audit

In-app Browser đối chiếu Alonso-Mora 2017, Simonetto 2019, Santi 2014, Ackermann &
Rieck 2025, speed-up heuristic, FleetPy framework/data và reproducibility sources.
Kết luận:

- dynamic insertion, ETA/time consistency, reassignment, shareability, multiple-plan
  và user satisfaction không được claim là novel;
- O-001 tiếp tục cấm accepted incumbent reassignment;
- B5 tách pairing vì multiple-plan/consensus không luôn tốt;
- sparse/direction/distance prune chỉ là future hypothesis với deterministic oracle,
  retained-loss diagnostics và full validator;
- WP6 same-team clean-process bundle là mechanical repeatability, không phải ACM badge,
  independent reproduction hoặc effectiveness.

## 4. Fresh tiny closure A

Command dùng `RideBound.Wp6TinyHarness` Release, new work/bundle/receipt paths.

| Field | Value |
|---|---|
| planned/succeeded/failed/excluded | `8/8/0/0` |
| plan | `5c769a8c1da891d152f917403c737aff2f7ac9c47b68482836dcecc9a462baea` |
| scenario | `8997836721d608a6bfc077f75016c4dbc0338886b4b4d73e02327a26d8c94e28` |
| semantic metric set | `c6ebfe7111feb322b526753b4665e878e1b11f2cd9edf70ea539ccfc7794acb1` |
| physical bundle | `79cb321a2aa079c34ddfa49061387e78990f14b7bb368abb762e497c30b27b04` |
| external verifier | exit 0, same bundle hash |

## 5. Fresh public-medium H/I trên exact source cuối

Mỗi process chạy B1/C1 × (1 warm-up + 3 measured) bằng same verified cache, source
hiện hành và new immutable destinations.

| Field | H | I | Gate |
|---|---|---|---|
| planned/succeeded/failed/excluded | `8/8/0/0` | `8/8/0/0` | exact |
| plan | `1b433b82...14d6` | same | exact |
| scenario | `88a8730a...e88` | same | exact |
| source inventory | `08b4f78b...3d26` | same | exact |
| runtime inventory | `1121a9b3...1dfd` | same | exact |
| run grid | `a8f5f572...70d0` | same | exact |
| transcript set | `95d4fa3a...69c` | same | exact |
| decision set | `5af895ee...917` | same | exact |
| semantic metric set | `0d47dee0...1f5` | same | exact |
| full metric set | `c1f45e68...f674` | `7999bf71...ae94` | expected different |
| physical bundle | `89a43921e46f57cfc47d9fcb0d63f8f18f58087a1f54d29fe65c7fecc4d6d9d8` | `a954db621758a6404fba988a491f9f4575add45a771f0b852ce7ab7cd95494e9` | expected different |
| external verifier | valid | valid | required |

Machine comparison:

```text
TopLevelCompared: 16
TopLevelMismatches: 0
RunCount: 8
PerRunFieldsCompared: 72
PerRunSemanticMismatches: 0
FullMetricRowsDifferent: 8
BundleHashesDifferent: true
```

H/I giữ cùng Git status hash và source inventory. Entry `RunnerSession.cs` trong cả hai
có length `42359` và SHA-256 `2a7b4589...97a`, trùng file source cuối. F/G và D/E là
pair lịch sử có source inventory khác; so chéo chúng với H/I là provenance change,
không phải repeat mismatch.

## 6. Required full solution và WAC

Exact command `dotnet test RideBound.slnx` pass 770/770, exit 0, khoảng 162 giây.
Contracts/Runner DLL nạp bình thường; `0x800711C7` không tái hiện. Historical WAC
records vẫn được giữ và không bị báo thành prior pass.

## 7. Claim verdict

WP6 được đóng cho common mechanical harness. Không có evidence cho C1 effectiveness,
non-inferiority, production SLA, FleetPy closed-loop, user satisfaction hoặc fairness.
WP7 giữ `NOT_STARTED`; WP8 giữ budget/threshold/margin decisions.

## 8. WP5 external repository regression

Không sửa BeGo. Baseline closure read-only:

- `dotnet test src\OptiGo.slnx --no-restore --verbosity minimal`: exit 0,
  149 pass + 5 explicit integration opt-in skip = 154 discovered;
- năm skip là published Runner/paired/PostgreSQL suites cần môi trường opt-in; lần này
  không được báo thành 154/154 full integration. Historical fresh-DB 154/154 evidence
  vẫn giữ riêng trong WP5 records;
- frontend `npm test`: 9/9 pass, 0 skip.
