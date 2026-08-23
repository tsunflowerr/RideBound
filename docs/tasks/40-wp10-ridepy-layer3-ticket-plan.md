# WP10 — RidePy cross-system Layer 3 ticket plan

> Trạng thái: **COMPLETE — NEGATIVE CAPABILITY RESULT**  
> Work package: `WP10`  
> Dependency: WP1–WP9 Complete; ADR-048/049; H6 negative result giữ nguyên  
> Upstream: RidePy `v2.10.1`, commit
> `bf1863e49a432f2f1f6230f86b2777a5ef5b9f14`, MIT  
> Decision: ADR-050/051  
> Ticket Ready duy nhất: không có; `RB-WP10-001..010 Done`

## 1. Outcome và claim boundary

WP10 kiểm tra liệu cùng RideBound Runner có chạy đúng qua một simulation stack độc
lập hay không. Nó không chạy lại hoặc cứu primary WP9. Kết quả Layer 3 chỉ là paired,
descriptive heterogeneity trên subset đã khai báo; không được pool với H6, không tạo
population/SLA/satisfaction/fairness/novelty claim.

```text
RidePy event clock + VehicleState + TransportSpace
  → RidePy-specific canonical mapping
  → same versioned RideBound Runner process
  → atomic stoplist application
  → native RidePy pickup/delivery events + sidecar evidence
```

Python không được chứa hard-vector decision rule, candidate search, budget charging,
promise projection hoặc solver logic. Mọi quyết định B1/C1 phải đến từ Runner.

## 2. Preflight decisions đã khóa

1. **Source pin.** Tag `v2.10.1` hiện trỏ đúng commit `bf1863e…9f14`. External
   checkout ở ngoài repository. Exact submodule pins là `lru-cache@13f30ad3…ddfcf`
   và nested `googletest@a2b8a8e0…1f47`. Materialized tree inventory 527 file
   (không tính `.git`) có SHA-256 canonical (UTF-8, ordinal path order)
   `d99ffac8…d891e`; `LICENSE` SHA-256 `87e0c317…1798`.
2. **Environment.** Upstream khai báo POSIX/Linux và không phát hành wheel Windows
   cho CPython 3.10 probe. Adapter chạy trong Linux container pin base
   `mcr.microsoft.com/dotnet/runtime@sha256:a365ce6a…0e235`, không sửa vendor.
3. **Extension point.** Subclass `FleetState`, giữ native `VehicleState.fast_forward_time`
   và `TransportSpace`; override request handling/application ở adapter.
4. **Position downgrade.** RidePy graph CPE không expose directed-edge fraction ổn
   định. Capability là `nodeOnly`, được đàm phán explicit; không suy diễn progress.
5. **Request limitation.** RidePy v2.10.1 request không mang party size/service class.
   Main subset pin `partySize=1`, `serviceClass=standard`; deviation được log.
6. **Reassignment.** Main subset pin `reassignment=false` cho cả B1/C1. Một Runner
   decision đụng nhiều stoplist vẫn phải validate rồi apply atomically.
7. **Traffic.** Chỉ cập nhật qua RidePy `Graph` weights và recompute native shortest
   path cache trước khi phát cùng canonical `travelTimesUpdated`; không có clock-derived
   travel inference.
8. **Comparison.** B1 và C1 trong mỗi cell dùng cùng request sequence, initial fleet,
   travel-update sequence, Runner artifact và master seed. Solver seed không nhân N.

## 3. Ordered queue

| Ticket | Việc làm | Gate đóng |
|---|---|---|
| `RB-WP10-001` **Done** | Refinement, official source audit, environment/claim decisions | ADR-050; exact tag/commit/license/tree/base-image pin; queue decision-complete |
| `RB-WP10-002` **Done** | Reproducible Linux environment và source verifier | exact image `5468b9cb…e573`; import/version/runtime receipt; 8/8 source-verifier tests |
| `RB-WP10-003` **Done** | Generic same-Runner client capability parameter | FleetPy defaults unchanged; explicit `nodeOnly`/required-capability negotiation; 14/14 client tests |
| `RB-WP10-004` **Done** | RidePy identity/time/request/travel/route mapper | exact integers; reversible IDs; complete graph snapshot; malformed/mutation tests |
| `RB-WP10-005` **Done** | `CommitFleetState` lifecycle và atomic apply | native incremental fast-forward; pickup/drop once; validate-all-before-apply; no Python decision logic |
| `RB-WP10-006` **Done** | 2-vehicle/5-request canonical scenario + traffic update | B1/C1 cùng Runner, 5/5 complete, 5 pickup + 5 drop, 22 decisions, five verifier mutations caught |
| `RB-WP10-007` **Done** | Fixed representative paired subset/freeze | 3 cells × 4 realizations × 2 arms; freeze v3 binds manifest/source/image/Runner/adapter before final execution |
| `RB-WP10-008` **Done — negative terminal** | Execute all B1/C1 subset jobs | 22 pass, 1 B1 fail closed, 1 paired arm not run; no partial reuse; failure retained verbatim |
| `RB-WP10-009` **Done** | Independent verifier + heterogeneity analysis | 11 valid pairs verified; exact terminal inventory/full Runner/seed binding; seven mutation classes caught; descriptive totals only |
| `RB-WP10-010` **Done** | Full source/claim review và closure | ADR-051, final capability matrix/docs/report; Layer 3 claim explicitly not established |

Queue đã chạy đúng thứ tự. Canonical gate pass nên không kích hoạt fallback AMoD2 ở
`006`; representative subset sau đó phát hiện giới hạn năng lực thật tại `008`.
ADR-051 đóng WP10 bằng negative capability result thay vì đổi simulator hoặc hạ gate
sau khi đã thấy outcome. AMoD2 chỉ còn là một work package độc lập trong tương lai.

## 4. Capability matrix bắt buộc

| Capability | Expected | Executable evidence | Fail code |
|---|---|---|---|
| exact tag/commit/submodules/license/tree | exact | source receipt + mutation verifier | `RBWP10_SOURCE_*` |
| isolated supported runtime | Linux container | import/version/upstream smoke | `RBWP10_ENV_*` |
| request clock/order | sequential, exact | out-of-order/duplicate mutation | `RBWP10_CLOCK_*` |
| position | `nodeOnly` | Runner negotiation + CPE probe | `RBWP10_POSITION_*` |
| request identity | reversible | collision/type/order tests | `RBWP10_ID_*` |
| dynamic travel times | explicit graph update | native cache + canonical snapshot equality | `RBWP10_TRAVEL_*` |
| old-plan projection | required | route round-trip and mutation | `RBWP10_ROUTE_*` |
| multi-vehicle atomic apply | required | validate-all-before-mutate witness | `RBWP10_ATOMIC_*` |
| reassignment | disabled main | manifest/config assertion | `RBWP10_REASSIGNMENT_*` |
| native lifecycle | required | pickup/drop reconciliation | `RBWP10_LIFECYCLE_*` |
| same Runner | required | pre/post full publish-tree hashes | `RBWP10_RUNNER_*` |
| independent bundle verify | required | tamper/no-extra/source/arm/input tests | `RBWP10_VERIFY_*` |

### Capability matrix cuối

| Capability | Kết quả | Bằng chứng/kết luận |
|---|---|---|
| exact source/runtime | **PASS** | RidePy 2.10.1, exact source tree/submodules/license, pinned Linux image |
| same Runner/no Python reimplementation | **PASS** | publish tree `be2b9b14…1d53`, pre/post identity và transcript |
| identity/time/travel mapping | **PASS** | strict mapping và explicit graph updates; unit/mutation tests |
| native lifecycle/atomic application | **PASS** | canonical B1/C1 đều reconcile 5 pickup + 5 drop |
| canonical 2-vehicle/5-request | **PASS** | 5/5 completed mỗi arm; verifier và artifact seals pass |
| representative subset | **FAIL CLOSED** | `nodeOnly` không biểu diễn xe đồng thời đang giữa cạnh; một stress job dừng tại epoch 17 |
| independent verification | **PASS** | 11 valid pairs + exact terminal inventory/failure transcript; seven falsification mutations caught |
| Layer 3 cross-system claim | **NOT ESTABLISHED** | không có complete paired subset; không pool hoặc rescue H6 |

## 5. Canonical scenario

Fixed small undirected graph, two vehicles and five unit-party requests:

- two early requests establish assignments/promises;
- one later request exercises insertion;
- one explicit graph travel-time update occurs between request epochs;
- one constrained request exercises rejection/budget pressure;
- clock drains at least one pickup and one delivery through native RidePy events.

The gate is lifecycle/physical/protocol equivalence, not identical service outcome
between B1 and C1. Any decision difference must be attributable to the named Runner
policy and must retain the exact same exogenous inputs.

## 6. Representative subset and estimand

Freeze a small deterministic grid before running outcome analysis:

- at least three workload cells spanning uncongested, insertion pressure and
  travel-update stress;
- at least four exogenous realizations per cell, paired B1/C1;
- primary descriptive outcomes: completed/arrived, rejection reason counts,
  decision-induced burden vector, native pickup/drop reconciliation;
- heterogeneity: oriented `C1−B1` per cell and comparison of sign/magnitude with
  WP9 panels, with no pooled gate or cross-system CI.

## 7. Verification baseline

Every code ticket runs:

```powershell
dotnet test RideBound.slnx
```

Plus full FleetPy Python suite, RidePy unit suite in the pinned container, actual
canonical/paired verifier, `dotnet format RideBound.slnx --verify-no-changes`, Release
build with warnings as errors, JSON/link/fence checks and manual file-by-file review.

## 8. Outcome đã đóng

- Canonical: B1 `5/5`, C1 `5/5`; cùng bảy exogenous events, native lifecycle và
  full Runner tree khớp; assignment cuối khác nhau nhưng là heterogeneity mô tả.
- Subset plan: 24 arm jobs. Final attempt: 22 pass, một B1 fail closed, một C1
  không chạy theo no-partial-reuse policy. Mười một cặp hợp lệ có B1 `54/62`, C1
  `49/62`, `Δ = −5 = −8.06 pp`; đây không phải estimand đầy đủ của planned subset.
- Failure tại `travel-update-stress-r3`, `t=116000`, epoch 17: pickup native xảy ra
  khi Runner `nodeOnly` còn giữ ETA `178000`, lệch 62 giây. Một event ở xe khác đã
  tạo epoch trong lúc xe này giữa cạnh; không có directed-edge progress để project
  promise đúng. Mã đóng: `RBWP10_NODEONLY_CONCURRENT_MIDEDGE_UNSUPPORTED`.
- Không nội suy progress từ clock, không đổi manifest sau outcome, không bỏ failure,
  không chuyển AMoD2 để tạo kết quả đẹp hơn. Báo cáo đầy đủ ở
  [`wp10-ridepy-layer3-negative-capability-result-2026-08-23.md`](../benchmarking/wp10-ridepy-layer3-negative-capability-result-2026-08-23.md).
