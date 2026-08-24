# RB-WP13-009 — Exploratory retained-portfolio replay E1

> Ngày freeze/execution: 2026-08-24  
> Trạng thái: `Done`  
> Claim class: post-outcome exploratory inventory; không confirmatory, không causal

## 1. Kết quả execution

E1 đã chạy đủ toàn bộ target set khóa trước execution, không chọn cell theo outcome:

| Panel | Xe | Pair | Arm run | Request | Solver decision | v1.2 portfolio | Failure |
|---|---:|---:|---:|---:|---:|---:|---:|
| A | 8 | 20 | 40 | 4.320 | 27.217 | 27.217 | 0 |
| B | 4 | 20 | 40 | 4.320 | 16.939 | 16.939 | 0 |
| **Tổng** | — | **40** | **80** | **8.640** | **44.156** | **44.156** | **0** |

Mọi bundle pass preflight và verifier Python độc lập tới EOF. Coverage retained
portfolio bằng đúng solver decision count ở từng arm, không chỉ bằng ở aggregate.
Tất cả 80 summary bind cùng Git-visible source inventory SHA-256
`22f4914e9f61163f8e33089a2f24786bcd4bf0b4c50d42a860fbf8916a3f6afb`.

Ticket này **không** so candidate B1/C1, không phân loại mechanism, không tính
counterfactual/reranking và không sửa kết luận H6. Các việc đó chờ `010`/`011`.

## 2. Freeze và fail-closed boundary

Freeze receipt source-controlled được tạo trước raw execution lúc
`2026-08-24T04:37:02.2036172Z`, dài 15.575 byte, SHA-256
`9fcf2193a597fe6c8db7796fe3b7387b647e31c9ad0d5e5a9621655ab73a4411`.
Receipt bind:

- exact 40 pair/80 arm labels từ record set `003`, cả Panel A/B, seed 7 và 108
  requests/arm;
- 40 driver files, scenario/derivative tree, source plans, cloned B1/C1 configs,
  commitment config, schemas, preflight/verifier/freeze/matrix source;
- FleetPy 1.0.2 commit `053aa9d…071b0`, Python 3.10.20, .NET SDK 10.0.301/runtime
  10.0.9, executable hashes và published Runner tree;
- output roots mới riêng cho A/B, hai H6 roots cấm, repeats 1, parallelism tối đa 4,
  Runner exchange timeout 60 s, line cap 64 MiB và stderr cap 1 MiB;
- failure treatment `retainTypedFailureNoRetryNoReplacement`.

Canonical config diff chứng minh B1/C1 chỉ thêm
`solverExecutionEvidenceProfile: retained-portfolio-v1`; policy version, budget,
cap, objective, seed, schedule strategy, promise/lock và failure treatment không đổi.
Matrix cấm arm-only staged subset, bắt trọn B1/C1 pair, cấm output path escape/H6
overlap và chỉ reuse bundle đã independent-verify.

## 3. Raw inventory và resource observation

External canonical inventory:
`E:\RideBoundData\wp13\e1-retained-portfolio-inventory-v1.json`, 67.771 byte,
SHA-256 `a029b9786aa8faa8663957d59163fa6a269b2515f771678306c8f0df5c054674`.
Compact source-controlled receipt:
[`wp13-e1-retained-portfolio-inventory-v1-summary.json`](evidence/wp13-e1-retained-portfolio-inventory-v1-summary.json).

| Panel/arm | Epoch | Raw byte | Wall ms min / median / max |
|---|---:|---:|---:|
| A/B1 | 14.133 | 2.007.504.748 | 585.134 / 1.093.369 / 1.514.349 |
| A/C1 | 13.084 | 1.808.316.456 | 490.822 / 883.202 / 1.261.020 |
| B/B1 | 8.836 | 890.962.429 | 216.261 / 338.014 / 530.644 |
| B/C1 | 8.103 | 809.315.077 | 190.568 / 287.920 / 452.253 |

Tổng raw bundle là 5.516.098.710 byte; bundle lớn nhất 119.353.350 byte và arm lâu
nhất 1.514.349 ms. Đây là observation về chi phí instrumentation/full portfolio,
không phải bằng chứng B1/C1 tốt/xấu hoặc mechanism service. Panel root inventory
hashes là `304c2071…5c8d` (A) và `393c6004…92b9` (B).

## 4. Independent inventory contract

`wp13_e1_inventory.py` không gọi Runner/simulator/solver và không import C# decoder.
Nó đọc exact plans, bắt output identity/scenario/source-inventory, verify manifest,
transcript hash-chain/lifecycle/semantic hash và strict evidence v1.2 đến EOF, rồi
reconcile 80 exact jobs, 40 pair, requests, epoch coverage và root inventory hashes.
Output chỉ được tạo exclusive sau khi toàn bộ schema pass; partial scan không tạo
inventory pass.

Source identity:

| Thành phần | SHA-256 |
|---|---|
| freeze receipt | `9fcf2193a597fe6c8db7796fe3b7387b647e31c9ad0d5e5a9621655ab73a4411` |
| Panel A plan | `9dabc5b89cf45b2b5d75cc22c2e406648129bc187242ef26dba82ed9bb34ebfe` |
| Panel B plan | `3e2326794d4d7c64fa455eeb73830099749572f12726ecfd345499f0b0558fca` |
| inventory analyzer | `aa58475c6519907c0a819a74467ee642b94a444a994b82e04916ccf7b0b732cf` |
| independent verifier | `89a9e9a797e7d7f004490bff3bc37da14cd792c14ff60513873ed51b96c06a17` |
| inventory schema | `b4376e239a81ba86c24bf2e455e5ffcc5cc281faa88a89681fb59df3615cc175` |
| published Runner DLL | `fdc58c35eaa48137adca3c9227160d1ddbc92b16a64ffe48e5ce8e560d4090a5` |
| published Runner tree | `c367a71f574b1dd458738906afabcca5f2eaca47aad50634e62088b6c324d4aa` |

## 5. Review và verification

Review theo file đã bắt và sửa trước freeze các lỗi thực:

- resume receipt ban đầu thiếu semantic/behavioral hashes;
- staged selection cho phép arm đơn, output path chưa chặn escape;
- receipt chưa bind output roots, resource/line envelope và 40 driver hashes;
- tree seal loại `__pycache__` sai vì chỉ so file name, không so path parts;
- schema nhầm Git commit 40 hex thành SHA-256 64 hex;
- field .NET trộn SDK 10.0.301 với runtime 10.0.9;
- independent H6 rescan có SHA report khác, nhưng structural diff xác nhận chỉ field
  verifier-source provenance đổi; toàn raw H6-derived evidence còn lại exact-equal.

Verification cuối:

- targeted freeze/inventory/verifier/schema + mutations: 22/22;
- required `dotnet test RideBound.slnx`: 860/860, zero skip;
- full pinned CPython/FleetPy suite: 181/181, zero skip;
- `dotnet format --verify-no-changes`, `git diff --check` và Python line scan: pass;
- independent external raw scan: 80/80 bundles, 44.156 decisions, v1.2 coverage
  44.156/44.156, zero failure;
- H6 roots không bị ghi; E1 nằm ở namespace ngoài H6.

`RB-WP13-009 Done`; chỉ `RB-WP13-010` được mở để falsify evidence contract trước
khi `011` aggregate descriptive candidate-level results.
