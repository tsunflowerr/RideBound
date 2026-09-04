# RB-WP14-009 — Paired dry-run resource gate

> Ngày: 2026-08-26  
> Trạng thái: `Closed — FAIL CLOSED`  
> Hệ quả: `RB-WP14-010` không được phép chạy

## 1. Verdict

Paired dry-run không đạt acceptance `2 completed / 0 failed`. B1 hoàn tất và được
independent verifier xác nhận; tiến trình bị chấm dứt ngoài đường timeout có kiểm
soát khi C1 đang ghi transcript. C1 vì thế chỉ còn partial transcript, thiếu
`bundle-manifest.json`. Nguồn chấm dứt chính xác không được process ghi lại nên
không được gán một nguyên nhân hạ tầng cụ thể.

Recovery không chạy lại mô phỏng. Matrix runner chỉ:

1. verify lại B1 thành `reusedVerified`;
2. phát hiện C1 tồn tại nhưng invalid;
3. ghi summary schema-valid `1 completed / 1 failed` rồi trả exit `1`.

Contract freeze ghi `retainTypedFailureNoRetryNoReplacement`; vì vậy partial C1
được giữ nguyên, không xóa/dời/ghi đè và không tạo freeze mới để thay thế kết quả
không thuận lợi.

## 2. Exact evidence

| Thuộc tính | Giá trị |
|---|---:|
| Freeze receipt | `1ce26ff0…37a55` |
| B1 Runner wall | 755.379 ms |
| B1 solver decisions | 824 |
| B1 transcript | 125.230.809 byte |
| B1 full bundle | 125.237.277 byte |
| B1 transcript byte/decision | `125.230.809 / 824 = 151.979,137` |
| C1 partial transcript | 83.364.599 byte |
| Output root sau failure | 208.602.549 byte |
| Free disk tại recovery | 145.146.146.816 byte |

B1 verifier chạy với cả `--include-behavioral-hash` và
`--require-audited-solver-evidence`: 108 request, 824 epoch, 2.479 frame,
3.883 event và 3.174 publication; status `pass`. Behavioral projection hash là
`5f2af778…32c5c0`.

Summary recovery ngoài bundle root:

- path: `E:\RideBoundData\wp14\dryrun-summary-v1.json`;
- length: 2.077 byte;
- SHA-256: `acc521bc5556ed2ed0f2523bcc889e7513ff6b3aad911551e2acd165ac9a4cc7`;
- Draft 2020-12 schema: pass;
- selected IDs: exact hai job đã freeze;
- result: B1 `reusedVerified`, C1 independent-verifier failure do thiếu manifest.

Compact source-controlled evidence:
[`wp14-009-paired-dry-run-resource-gate-v1-summary.json`](evidence/wp14-009-paired-dry-run-resource-gate-v1-summary.json).

## 3. Resource interpretation

Một valid job không đủ chứng minh paired envelope. Chỉ để kiểm tra trần bảo thủ,
nếu giả mọi job lớn/chậm như B1 thì 160 bundle chiếm khoảng 20.037.964.320 byte
và wall ở parallelism 4 khoảng 30.215.160 ms (8,39 giờ). Hai số này vẫn nằm dưới
ceiling freeze 20 GiB/16 giờ, nhưng **không làm resource gate pass** vì không có
valid C1 observation và không được gọi là speedup claim.

Outcome completion/burden của dry-run không được đọc hoặc dùng để đổi factor,
denominator hay freeze.

## 4. Queue consequence

`RB-WP14-009` được đóng bằng failure receipt thay vì đánh dấu Done. Theo acceptance
đã freeze: nếu paired dry-run fail thì dừng trước matrix. Vì vậy `010..014` không
được authorize trên freeze v1; `006/007` vẫn Deferred. H6, WP10, WP13 và receipt
`008` giữ nguyên.

