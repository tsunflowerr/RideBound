# RB-WP6-001 — refinement common benchmark harness

> Trạng thái: `DONE`
> Work package: `WP6`
> Loại ticket: refinement-only; không production code, không experiment result
> Dependency: WP4 và WP5 Complete; ADR-024/025; final review WP1–WP5
> Ticket implementation WP6 được phép trước khi ticket này DONE: `NONE`
> Decision evidence: ADR-026, `docs/benchmarking/wp6-contract-v1.md`,
> `docs/research/wp6-benchmark-reproducibility-evidence-2026-08-09.md`
> Ordered implementation queue: `docs/tasks/34-wp6-common-benchmark-harness-ticket-plan.md`

## 1. Outcome

Khóa decision-complete contract cho pipeline:

```text
raw dataset/source → normalized scenario → exact Runner input
→ raw result/failure log → deterministic metric rows → self-verifying bundle
```

Output của ticket là một ADR WP6 và ordered implementation queue có đúng một
ticket nhỏ nhất `READY`; không phải executable harness.

## 2. Non-goals

- không chạy pilot/main/confirmatory experiment;
- không chọn O-002 budget, O-003 material threshold hay O-004 margin;
- không adapter FleetPy/RidePy/AMoD2;
- không thay protocol/hash/certificate/Runner ownership WP1–WP5;
- không dùng paired WP5/local curves làm effectiveness hoặc SLA baseline;
- không tải/đưa dataset vào repo trước license/provenance/retention decision;
- không tạo metric sau khi xem policy outcome.

## 3. Inputs bắt buộc

1. `docs/01`, `03`, `06`, `09`, `10`, `11`, `15`, `18`, `19`, `20`, `21`.
2. ADR-024/025 và ticket plans `30`/`32`.
3. [Final WP1–WP5 review](../reviews/wp1-wp5-final/README.md).
4. WP5 paired manifest `b843bd20...` và independent manifest `e21fb087...`.
5. Primary sources về benchmark reproducibility, random seeds, measurement
   methodology, missing/failure handling và multiple-comparison/prereg boundaries;
   mỗi mechanism phải ghi claim limit.

## 4. Quyết định phải khóa

1. **Scenario identity:** canonical fields, units, coordinate/time representation,
   source checksum, normalizer version và scenario hash domain.
2. **Dataset boundary:** license, download/checksum, raw immutability, PII/location
   precision, retention và synthetic/public/real labels.
3. **Demand/event semantics:** ordering, ties, warm-up/horizon, fleet initial state,
   travel snapshot và unreachable handling.
4. **Pairing:** policy/config allowlist, common seed/work limits, run order và
   isolation giữa repeats/arms.
5. **Seed hierarchy:** master/scenario/repeat/component derivation, no hidden RNG,
   seed ghi vào manifest/result.
6. **Runner boundary:** same pinned binary/core/config; adapter chỉ phát exact
   protocol input và không reimplement metric-affecting core behavior.
7. **Failure taxonomy:** invalid input, capability exclusion, timeout, solver
   unknown, divergence, process crash, incomplete output; không đổi failure thành 0.
8. **Exclusion log:** append-only reason/source/stage; prereg rule nào được loại và
   denominator nào vẫn giữ.
9. **Metric computation:** raw fields, units, aggregation level, denominator,
   missingness, overflow/precision và independent recomputation oracle.
10. **Result schema:** per-event/request/vehicle/run/scenario/arm/repeat IDs,
    diagnostics, hashes, timestamps và negative-result retention.
11. **Bundle integrity:** exact file inventory, raw/canonical hashes, source/config/
    assembly/runtime provenance, self-verifier và no-extra-file rule.
12. **Resource accounting:** wall/CPU/memory/process limits, warm-up/repetitions,
    machine metadata; không biến local limits thành production SLA.
13. **Tiny/medium fixtures:** bound đủ cho deterministic replay và raw-to-metric
    oracle trước dataset lớn.
14. **Claim checker:** caveat/label bắt buộc để artifact WP6 không tự được gọi là
    effectiveness, non-inferiority hay confirmatory evidence.

## 5. Required artifacts khi DONE

- ADR-026 khóa 14 quyết định trên;
- JSON schema hoặc equivalent contract cho scenario, raw result, failure/exclusion,
  metric row và bundle manifest;
- field-level source→normalized→Runner→metric traceability matrix;
- threat/failure model và deterministic seed derivation;
- tiny/medium reproduction protocol với independent oracle plan;
- ordered WP6 implementation tickets, dependency graph, rollback/stop conditions;
- update `00/09/10/11/15/16/18/19/20/21/23`;
- đúng một implementation ticket nhỏ nhất `READY`.

## 6. BDD

```gherkin
Given cùng raw bytes, normalizer version và manifest
When scenario được build ở hai clean process
Then canonical scenario bytes/hash và exact Runner input phải giống nhau
And mọi khác biệt môi trường không nằm trong declared provenance làm gate fail
```

```gherkin
Given một arm timeout hoặc trả incomplete output
When metric pipeline chạy
Then failure được giữ bằng typed record và đúng denominator rule
And pipeline không thay nó bằng metric 0, bỏ row im lặng hoặc rerun chọn lọc
```

```gherkin
Given cùng raw result bundle
When production metric calculator và independent oracle tính metric
Then mọi row/unit/denominator/hash phải bằng nhau trong tiny bound
And mismatch làm bundle invalid trước mọi statistical analysis
```

## 7. Acceptance

- Tất cả field/hash/unit/seed/failure/exclusion/metric ownership có một source of
  truth, không còn `TBD` ảnh hưởng implementation ticket đầu.
- Scenario/result contracts không phụ thuộc BeGo/FleetPy entity type.
- Same Runner hash/config/candidate/compute pairing được giữ qua mọi layer.
- Failure và negative result được lưu, không survivorship bias do pipeline.
- Tiny/medium reproducibility và independent raw-to-metric oracle có acceptance
  test cụ thể, không chỉ checksum output cuối.
- Không decision nào trong ticket chọn O-002/O-003/O-004 hoặc tạo confirmatory claim.
- ADR/status/traceability/roadmap/paper evidence đồng bộ.

## 8. Rollback và stop

Nếu dataset license, metric denominator, exclusion rule, seed derivation hoặc
scenario identity chưa khóa được, giữ WP6 ở refinement; không scaffold harness để
ép decision về sau. Nếu primary source mâu thuẫn, ghi decision/risk và chọn contract
fail-closed/auditable thay vì tối ưu cho kết quả đẹp.

## 9. Handoff dự kiến

Sau khi `RB-WP6-001` DONE, tạo một ordered plan mới (dự kiến `tasks/34`) và chỉ
chuyển ticket schema/primitives nhỏ nhất sang `READY`. WP7 vẫn `NOT_STARTED` cho tới
khi common scenario/result boundary executable đạt tiny gate.

## 10. Closure evidence — 2026-08-09

- ADR-026 khóa đủ 14 decision, không chọn O-002/O-003/O-004.
- Contract v1 là equivalent contract decision-complete cho dataset/normalization,
  scenario, plan/arm, raw observation/run, failure, exclusion, metric row và logical
  bundle manifest; JSON Schema executable được giao riêng cho `RB-WP6-002`.
- FleetPy Manhattan Zenodo v1/CC BY 4.0, publisher MD5 và raw-cache/derivative
  boundary được khóa; local SHA-256 chỉ được điền sau exact download verification.
- Seed hierarchy dùng addressable HMAC-SHA-256; không mạo nhận dùng Random123.
- Bundle dùng strict BagIt-compatible profile, PROV-like derivation và no-extra rule.
- Tiny/medium, independent oracle, resource/failure/claim gates có acceptance cụ thể.
- Tại thời điểm refinement closure, `tasks/34` có đúng `RB-WP6-002 READY`; trạng
  thái thực thi hiện hành luôn lấy từ đầu `tasks/34` và `docs/18`.
