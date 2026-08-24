# RB-WP13-010 — E1 evidence falsification and H6 equivalence

> Ngày kiểm chứng: 2026-08-24  
> Trạng thái: `Done`  
> Claim class: evidence-contract và instrumentation equivalence; không causal

## 1. Kết quả

`RB-WP13-010` đóng hai điều kiện bắt buộc trước candidate-level aggregation:

- independent inventory được dựng lại byte-exact từ toàn bộ 80 E1 bundle,
  44.156 solver decision và 44.156 retained portfolio;
- 31/31 mutant bị từ chối tại đúng layer và đúng typed rejection code, không có
  unexpected pass hoặc generic/unclassified failure;
- cùng 80 arm E1 được so read-only với exact H6 job: 80/80 behavioral projection
  bằng nhau, 0 mismatch, đủ 8.640 request và 44.156 H6 solver decision;
- semantic hash bằng nhau 0/80 là expected vì profile/config/state binding v1.2 đổi;
  behavioral equality mới là biên chứng minh instrumentation không đổi vận hành.

Kết quả này chỉ cho phép dùng E1 làm evidence exploratory cho `011`. Nó không sửa
service gate H6, không chứng minh mechanism và không tạo causal/counterfactual claim.

## 2. Artifact và provenance

| Artifact | Byte | SHA-256 |
|---|---:|---|
| `E:\RideBoundData\wp13\e1-falsification-receipt-v1-closure.json` | 13.515 | `78bf631392fb9551103f8e1ce4dd2e101ef5deed32d4dd9d95297a28e8377785` |
| `E:\RideBoundData\wp13\e1-h6-behavioral-equivalence-v1.json` | 66.597 | `4abb24f0d789f6baccf8fbf163bfbbe19738f712b6f3bb25cdd949c2260babfc` |
| E1 inventory input | 67.771 | `a029b9786aa8faa8663957d59163fa6a269b2515f771678306c8f0df5c054674` |
| E1 freeze receipt | 15.575 | `9fcf2193a597fe6c8db7796fe3b7387b647e31c9ad0d5e5a9621655ab73a4411` |

Compact source-controlled receipt:
[`wp13-e1-falsification-and-h6-equivalence-v1-summary.json`](evidence/wp13-e1-falsification-and-h6-equivalence-v1-summary.json).

Source identities:

| Thành phần | SHA-256 |
|---|---|
| falsification analyzer | `32cb0738e824be29ae6aecea0267f99296c3297501435a8ba8f20d689d9cd175` |
| falsification schema | `a5dae7b53fd1e4dc30b31b6049235cdbe16e20d8ae10b29981299b331ee0868c` |
| falsification tests | `24eb3510d0ca02c1fa09f8c2fa3d19efe29ba03c52e174d4d2a6c3fae13d5adb` |
| H6-equivalence analyzer | `67ff0d603eebc7857d4b2d970ff5d154276f819c8df09e66c2087accdddff097` |
| H6-equivalence schema | `ed5d2fa2a1e2c72fca907746dda421acdc8a10c328095e70d821a777f122f7b3` |
| H6-equivalence tests | `65353324cbbfb5541365893fa356fcaf30073e98f6d178fadab1e2d3e9f3f3ae` |
| independent bundle verifier | `89a9e9a797e7d7f004490bff3bc37da14cd792c14ff60513873ed51b96c06a17` |
| inventory analyzer | `aa58475c6519907c0a819a74467ee642b94a444a994b82e04916ccf7b0b732cf` |

## 3. Falsification matrix

Hai recorded structural samples được bind bằng canonical solver JSON hash:

- B1 `p-d20181114-s10-r1-b1-tight-s7`, epoch 2, 15 candidates,
  `958eae1d…1fb23`;
- C1 `p-d20181114-s10-r1-c1-tight-s7`, epoch 11, 13 candidates có pruned option,
  `86650da7…19285`.

| Layer | Mutant | Nội dung chính |
|---|---:|---|
| artifact binding | 8 | job/summary/manifest/file inventory/source, truncation, transcript và frame hash |
| evidence version | 1 | v1.2 portfolio bị gắn evidence version cũ |
| portfolio identity | 3 | version, schema ID, objective profile |
| candidate set | 5 | count, duplicate/order, undeclared request |
| eligibility | 3 | eligible/pruned objective shape, no-op contract |
| selection | 5 | unknown/duplicate/pruned candidate, sai vehicle, trùng request |
| objective | 2 | level index và contribution count |
| route/schedule | 3 | stop identity, route/schedule identity, time order |
| strict shape | 1 | extra nested field |
| **Tổng** | **31** | **31 expected code = 31 actual code** |

Catalog phát 25 typed rejection code. Unclassified failure làm cả tool fail, không
được quy thành expected rejection. Mutant chỉ nằm trong memory; raw E1/H6 không bị
copy, sửa hoặc backfill.

## 4. E1 ↔ H6 instrumentation equivalence

| Panel | Arm run | Behavioral equal | Mismatch | H6 decision |
|---|---:|---:|---:|---:|
| A | 40 | 40 | 0 | 27.217 |
| B | 40 | 40 | 0 | 16.939 |
| **Tổng** | **80** | **80** | **0** | **44.156** |

Comparator mở đúng H6 job cùng label/scenario cho từng E1 record, chạy independent
bundle verifier tới EOF rồi so operational behavioral projection. Nó không yêu cầu
semantic/state hash equality vì policy manifest bind state và E1 có instrumentation
profile mới. Đây là same-arm instrumentation equivalence, không phải B1/C1 effect.

## 5. Review corrections

Review theo file không chấp nhận receipt đầu tiên chỉ lật boolean truncation. Receipt
8.777 byte `7f737abc…064` được giữ nhưng superseded. Bản 8.999 byte
`d1252b44…3f4` đã có exact transcript length/hash và 26 mutant, sau đó cũng được
supersede vì chưa có typed rejection code và chưa phủ summary/extra-file/frame-hash,
selected-vehicle/request-disjointness.

Bản closure cuối thêm exact 31-mutant catalog, preflight toàn catalog, classifier
fail-closed cho generic error, exact schema cardinality và same-arm H6 comparison.
Hai lần dựng closure trung gian exit 1 ở expectation quá rộng; không lần nào tạo
success artifact. Điều này được giữ như negative execution evidence, không bị tính
thành pass.

## 6. Verification cuối

- targeted falsification/equivalence tests: 6/6 pass;
- recorded-sample mutation preflight: 31/31, 31 unique ID, 25 typed code;
- external full E1 rebuild: 80/80 bundle, 44.156/44.156 portfolio, byte-exact
  canonical inventory reproduction;
- external H6 comparison: 80/80 operationally equal, zero mismatch;
- required `dotnet test RideBound.slnx`: 860/860, zero skip;
- full pinned CPython/FleetPy suite: 187/187, zero skip;
- một concurrent .NET/Python run làm hai adversarial child-process case timeout;
  targeted sequential 1/1 và full sequential 187/187 pass, nên không dùng run song
  song đó làm baseline;
- `dotnet format --verify-no-changes`, `git diff --check`, JSON/Markdown/static line
  gates: pass.

`RB-WP13-010 Done`; chỉ `RB-WP13-011` được mở cho finite-panel descriptive
candidate aggregation. H6/WP10 và mọi confirmatory conclusion giữ nguyên.
