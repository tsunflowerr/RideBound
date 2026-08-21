# RideBound WP9 fixed-panel preregistration v1

> Freeze payload. Tệp này không chứa outcome holdout. SHA-256 của chính bytes tệp
> là gốc của chuỗi freeze; receipt được lưu ở tài liệu riêng để tránh self-reference.

## 1. Câu hỏi và giả thuyết

Trên fixed panel Manhattan đã khóa, C1 với drop-ETA budget 30 giây có giảm tổng
decision-induced promise burden so với B1, đồng thời không làm tỷ lệ hoàn thành
giảm quá 1,0 điểm phần trăm hay không?

Kết luận “practical within this panel” chỉ được đưa ra nếu đồng thời:

- tổng burden C1 nhỏ hơn tổng burden B1;
- `Δ_service_panel > −1,0 pp`.

Nếu burden giảm nhưng service gate trượt, kết luận bắt buộc là trade-off không
thực dụng tại cấu hình/panel này. Không có claim dân số hay ngoại suy production.

## 2. Primary outcome

`total_decision_induced_burden_ms` là tổng số nguyên của
`decisionDelta.pickupEtaTotalMs + decisionDelta.dropEtaTotalMs` trên mọi action
`promisePublished` trong một run. Calculator production và executable oracle độc
lập phải khớp canonical bytes. Rider/action được gộp lên run trước khi pairing.

Phải báo riêng pickup/drop. Vì C1 khóa pickup ETA, phần pickup giảm là do định
nghĩa; phần drop giảm trên chiều không khóa là phần kiếm được. Không được chỉ báo
một phần trăm tổng hợp che khuất phân rã này.

## 3. Service gate và margin

Service là `distinct passengerAlighted / distinct requestArrived`. Panel có cùng
denominator ở hai arm. Margin cố định 100 basis points = 1,0 pp:

`10,000 × (completed_C1 − completed_B1) > −100 × arrived_per_arm`.

Đây là exact integer gate trên finite panel, không phải population NI test/CI.
Biên giữ nguyên dù pilot gợi ý C1 có thể trượt.

## 4. Secondary diagnostics

Material ETA revision count, disruptive-decision count, pre-pickup insertion,
exogenous pickup/drop burden, rejection reason và solver/generation evidence.
Secondary giải thích cơ chế; không cứu primary/service gate trượt. Exogenous
khác biệt bất thường là falsification signal cần điều tra trước khi kết luận.

## 5. Dataset, panel và thời gian

- FleetPy Manhattan v1, Zenodo `10.5281/zenodo.15187906`, CC BY 4.0,
  source artifact `d9e86f33645e5eec287d387f8d63ad41ddf41d4ef648138b65d636482e2c599e`.
- Holdout: `2018-11-14` đến `2018-11-18`, 5 ngày.
- Mỗi ngày: publisher demand file `sample_10_1` đến `sample_10_4`.
- Cửa sổ `08:00–10:00`, request target 128, 8 xe, capacity 4,
  pickup window 600.000 ms, maximum ride time 1.500 permille.
- Tổng 20 cell. Đơn vị là scenario/demand/travel realization; `masterSeed`
  không thuộc experimental-unit identity.

Mọi ngày đã được kiểm có day-specific `tt_factors.csv` schema 9 cột trước freeze.
Pilot `2018-11-12/13` không được tái sử dụng trong confirmatory panel.

## 6. Selection key và freeze chain

Không chọn key thủ công. Gọi `H0 = SHA-256(bytes của tệp preregistration này)`.
Mọi confirmatory grid cell dùng:

- `selectionLabel = ridebound-wp9-confirmatory-selection-` + `H0`;
- `pseudonymizationLabel = ridebound-wp9-confirmatory-pseudonymization-` + `H0`.

Normalizer hash tiếp các label theo contract hiện hành. Sau materialization, grid
hash `H1` và derivative hashes được ghi vào freeze receipt. Execution manifest
bind `H0`, `H1`, config hashes, Runner hash và analysis hash thành `H2`. Chuỗi này
tránh self-reference nhưng không để lại lựa chọn sau outcome.

## 7. Arms và configuration hashes

Primary pair dùng cùng commitment file tight; baseline tự gỡ hard limits bằng
`EffectivePolicies`, còn treatment giữ chúng.

| Vai trò | File | SHA-256 |
|---|---|---|
| B1 | `wp9-fleetpy-rolling-cost-audited-v1.json` | `60d1e7197672d41299e5d35281bf5f42506687df230f0e852083c86570c35c85` |
| C1 | `wp9-fleetpy-ridebound-hard-vector-audited-v1.json` | `abfd1c608e3c0e4324fcc7cdc0feb7095de37057a135088162f7788a9c96ee2f` |
| tight commitment | `wp8-drop-eta-budget-tight-v1.json` | `d6124f3f964d8385db381d53b75c142cf2ac870b22823d6675325c3808808beb` |
| unbounded commitment | `wp6-public-mechanical-commitment-v1.json` | `cc2be4ede4a868ca78958773fe39060aa6c034e9b80b5f81c6d08c070326679b` |
| loose commitment | `wp8-drop-eta-budget-loose-v1.json` | `ac0f97589427919c5175e9cc952849305702ced45313a5a2db88167dff030520` |
| exploratory C2 | `wp9-fleetpy-soft-hard-hybrid-audited-v1.json` | `54d4ea4075ed050c24d5c43ca14d3d91a58f1b6823615ff900feb2e52aeef517` |

B1/C1 primary policy files khác nhau đúng `policyId`; evidence opt-in và mọi
compute/candidate budget giống nhau.

## 8. Runtime identities

- FleetPy `1.0.2`, commit `053aa9d4fcfde91c5d303435d5748f9206c071b0`.
- CPython `3.10.20` từ environment lock.
- Runner `RideBound.Runner.dll` SHA-256
  `16f3b5e8d4d774a0c35f6738ac95a4c0afab30e556fc8db97609ae26c5393aad`.
- OR-Tools `9.15.6755`, one worker, deterministic-time budget.
- Preflight SHA-256
  `295737ccf571a7c8583d440e2f16c6f1bfb27d46ef9fa9008770e9999ec715e2`.
- Independent verifier SHA-256
  `872135877c1241c591975a1f745a095c466df78558bb9201c386e63bf121a490`.

## 9. Seed và cỡ panel

Primary dùng `masterSeed=7` ở cả hai arm. Panel size 20 do danh sách demand/travel
realization hữu hạn, không do power formula. Seed 19 chỉ dùng robustness trên
subset đã khóa ở §15, không tăng N và không được gộp như replication.

Với 5 date cluster, exact sign-flip p-value nhỏ nhất 0,03125; vì vậy không phát
population p-value/CI ở alpha 0,025. Đây là finite-panel evaluation.

## 10. Compute budget

`maximumCandidatesPerVehicle=100`, `maximumNewRequestsPerVehicle=2`,
`maximumExplorationWorkUnits=10000`, generation/validation work 10.000,
solver work 100.000, deterministic time 1.000.000 microsecond-equivalent,
earliest-feasible schedule và service-set stability portfolio. Mọi run primary
bắt buộc phát solver execution evidence.

## 11. Estimand và analysis implementation

Primary estimand là aggregate hữu hạn trên đủ 20 cell, kèm toàn bộ paired delta,
mean/median/min/max mô tả. Không bootstrap rider, run repeat hay solver seed.

Analysis file `simulators/fleetpy-ridebound/wp9_fixed_panel_analyze.py` SHA-256
`ea244b6a7fae173caf790f27c27b24f28bcdda747b63c4a828a2a38990bccdbc`.
Nó gọi independent verifier trước khi đọc metric và in canonical JSON.

## 12. Multiplicity và claim

Chỉ một primary burden và một service gate đồng thời. Secondary là diagnostic,
không có confirmatory p-value. Không có subgroup winner claim. Claim tối đa là
finite-panel result tại đúng cấu hình và dữ liệu này.

## 13. Inclusion, failure và exclusion

Mỗi primary bundle phải:

- exit 0 và `repeatCount=1`;
- qua verifier integrity/conservation/hash-chain;
- qua `--require-audited-solver-evidence`: generation accounting exact, không
  omission/saturation, solver completed/optimal, validated incumbent, không
  fallback, objective bounds exact;
- có 128 arrivals và cùng denominator trong pair.

Schema/data invalid đã khai trước mới được exclude. Adapter/protocol/certificate/
verifier failure là failed. `planned=succeeded+failed+excluded`; không đổi failure
thành metric 0 và không loại vì outcome xấu.

## 14. Stop, restart và source freeze

Không sửa/tạo/xóa file repo trong lúc matrix chạy. Preflight chụp HEAD và source
inventory trước/sau. Infrastructure failure được rerun toàn pair với supersession
receipt; outcome xấu không được rerun. Sửa bug ảnh hưởng confirmatory bắt buộc
invalidate và chạy lại toàn bộ affected grid bằng artifact/thư mục mới, giữ bản cũ.

## 15. Planned output và robustness

Primary: 20 B1 tight-config bundle + 20 C1 tight bundle, cùng seed 7.

Robustness khóa trước trên cell `sample_10_1` của mỗi ngày (5 cell):

- C1 unbounded để tách giá lock/ranking khỏi giá budget;
- C2 loose-hard/60-second warning, exploratory;
- B1/C1 tight với seed 19 để kiểm tie-breaking robustness.

Các run robustness không tham gia N/gate chính. Planned tables: 20-row paired
table, aggregate gate, locked/earned decomposition, rejection/evidence table và
robustness comparison. Không claim satisfaction, demographic fairness, SLA,
novelty hoặc external validity.
