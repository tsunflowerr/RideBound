# RB-WP14-004 — Development panel và leakage audit

> Ngày: 2026-08-24
> Trạng thái: `Done`
> Claim class: development data only; không confirmatory, không chạm H6

## 1. Vì sao ticket này là nút thắt

ADR-065 cấm H6 Panel A/B tham gia tuning hoặc selection của WP14. Lời cấm chỉ có
giá trị nếu kiểm được bằng máy. Ticket này dựng một panel phát triển từ **cùng
nguồn dữ liệu thật** nhưng **realization hoàn toàn rời**, rồi chứng minh sự rời đó
trên mọi trục mà một realization có thể rò rỉ qua.

## 2. Panel

| Thuộc tính | Giá trị |
|---|---|
| Grid | `wp14-development-panel-v1`, 16 cells |
| Ngày | 2018-11-12, 2018-11-13 |
| Realization | `sample_10_1..4` |
| Cửa sổ trong ngày | `w08` = 28.800–36.000 s và `w17` = 61.200–68.400 s giờ địa phương |
| Xe | 8, sức chứa 4 |
| Request mỗi cell | 108 |
| Ràng buộc vật lý | pickup window 600.000 ms, max ride time 1.500 permille — giống H6 |
| Node | 96, 9.120 directed arc |
| Dung lượng fixture | 67.416.839 byte, 16 cell × 7 file |

Nguồn vẫn là FleetPy Manhattan public derivative (Zenodo DOI
`10.5281/zenodo.15187906`, CC BY 4.0), qua đúng normalizer đã dùng cho H6, với
`greedy-induced-coverage-node-pool-hmac-row-v1` và nhãn selection/pseudonymization
**riêng** cho WP14.

### Vì sao chỉ hai ngày

H6 chiếm 2018-11-14 đến 2018-11-18. Trong bộ dữ liệu chỉ còn 2018-11-11, 11-12,
11-13. Ngày 2018-11-11 **không có** `2018-11-11_tt_factors.csv` trong verified
inventory (chỉ có bản `_hourly`), nên normalizer fail-closed ở preflight và ngày đó
bị loại — không thay bằng file khác vì đó là đổi semantics dữ liệu.

Vì vậy độ đa dạng tải đến từ **hai cửa sổ trong ngày** thay vì thêm ngày. Đây là
một ràng buộc thật của bộ dữ liệu, được ghi lại chứ không che.

## 3. Leakage audit

[`wp14_development_panel_audit.py`](../../simulators/fleetpy-ridebound/wp14_development_panel_audit.py)
so panel phát triển với cả hai grid H6 đã đóng băng trên bảy trục. Bất kỳ giao nào
là fail cứng.

| Trục | Số phần tử giao |
|---|---:|
| `demandMemberPath` | 0 |
| `travelFactorMemberPath` | 0 |
| `sourceLocalDate` | 0 |
| `sourceWindow` | 0 |
| `scenarioId` | 0 |
| `cellId` | 0 |
| `scenarioHash` | 0 |

- development: 16 cell, ngày `2018-11-12`, `2018-11-13`;
- frozen: 40 cell qua `wp9-confirmatory-fixed-panel-v2` và
  `wp9-confirmatory-fixed-panel-v3-veh4`, ngày `2018-11-14`…`2018-11-18`.

Receipt ngoài repo: `E:\RideBoundData\wp14\development-panel-audit-v1.json`,
5.109 byte, SHA-256 `f1c731f6…bfd099ba`. Compact receipt trong repo:
[`wp14-004-development-panel-v1-summary.json`](evidence/wp14-004-development-panel-v1-summary.json).

## 4. Verification

| Kiểm tra | Kết quả |
|---|---|
| Không giao trên cả bảy trục, dữ liệu thật | pass |
| Ngày development và frozen rời nhau | pass |
| 16/16 cell có `scenarioHash` sinh ra, không trùng nhau | pass |
| **Mutation**: mượn một `demandMemberPath` của H6 ⇒ fail closed | pass |
| **Mutation**: mượn một `scenarioId` của H6 ⇒ fail closed | pass |
| Thiếu fixture đã sinh ⇒ fail closed | pass |
| Không có frozen grid nào để so ⇒ fail closed | pass |
| Full pinned CPython/FleetPy | 225/225, zero skip |

Hai mutation test là phần quan trọng nhất: chúng chứng minh audit thực sự bắt được
leakage chứ không phải luôn trả “0 giao”.

## 5. Điều không claim

Panel này **không** là confirmatory và không thay thế H6. Nó không được dùng để
diễn giải lại kết quả H6, và kết quả chạy trên nó không được so trực tiếp với
`−154` / `−106` như thể cùng một estimand: khác ngày, khác cửa sổ, khác realization.

16 cell × một fleet size là nhỏ hơn H6 (20 cell × hai fleet size). Đó là giới hạn
của dữ liệu còn lại sau khi loại H6, không phải lựa chọn thiết kế, và nó giới hạn
độ phân giải của frontier mà `012` được phép tuyên bố.

## 6. Hệ quả

`RB-WP14-004 Done`, `RB-WP14-005 Ready`. Mức số của F1–F6 giờ có thể được derive từ
panel này mà không chạm H6.
