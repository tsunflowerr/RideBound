# Phân phối pilot và phân rã burden — RB-WP8-006

> Phân loại: pilot, không phải confirmatory. Sửa số liệu ngày 2026-08-21 sau khi
> đối chiếu hai extractor độc lập với transcript thô.

## Phạm vi

Bốn đơn vị pilot dùng dữ liệu Manhattan công khai, cửa sổ `08:00–10:00`, 8 xe,
128 yêu cầu. Mỗi đơn vị chạy B1 `rolling-cost` và C1
`ridebound-hard-vector` trên cùng Runner v8. Đơn vị là hiện thực hữu hạn của
scenario/demand/travel; seed solver không tạo thêm đơn vị độc lập.

## Quan sát run-level

| Đơn vị | B1 hoàn thành | B1 pickup | B1 drop | B1 burden | C1 hoàn thành | C1 pickup | C1 drop/burden |
|---|---:|---:|---:|---:|---:|---:|---:|
| `C-d20181112-r2` | 107 | 599.604 | 2.423.414 | 3.023.018 | 99 | 0 | 758.435 |
| `C-d20181113-r1` | 117 | 142.148 | 2.388.874 | 2.531.022 | 110 | 0 | 552.796 |
| `C-d20181113-r2` | 120 | 764.047 | 3.697.813 | 4.461.860 | 119 | 0 | 331.675 |
| `L1-peak2h-veh8` | 117 | 665.634 | 4.090.174 | 4.755.808 | 117 | 0 | 760.220 |
| **Tổng** | **461** | **2.171.433** | **12.600.275** | **14.771.708** | **445** | **0** | **2.403.126** |

Đơn vị của burden là millisecond cộng dồn trên toàn bộ rider. Các tổng burden
trong bản trước đúng, nhưng ba split pickup/drop đầu sai; bảng này thay thế chúng.

Paired delta C1−B1:

| Đơn vị | Δ burden (ms) | Δ hoàn thành (pp) |
|---|---:|---:|
| `C-d20181112-r2` | −2.264.583 | −6,250 |
| `C-d20181113-r1` | −1.978.226 | −5,469 |
| `C-d20181113-r2` | −4.130.185 | −0,781 |
| `L1-peak2h-veh8` | −3.995.588 | 0,000 |

Mean Δ burden là −3.092.146 ms, sample SD 1.128.334 ms. Pooled service
rate là 90,039% cho B1 và 86,914% cho C1, chênh −3,125 pp. Đây là mô tả
pilot `n=4`, không phải CI hay effectiveness claim.

## Phần do định nghĩa và phần kiếm được

Lock `pickupEta` làm decision-induced pickup burden của C1 bằng 0 theo cấu hình.
Vì vậy không được trình bày toàn bộ mức giảm như thành tích tối ưu.

Trên ba ô contended nguyên thủy, tổng giảm là 8.372.994 ms:

- 1.505.799 ms, **17,98%**, đến từ chiều pickup bị khóa theo định nghĩa;
- 6.867.195 ms, **82,02%**, đến từ giảm drop-ETA trên chiều không bị khóa.

Nếu tính cả ô `L1`, tỷ lệ tương ứng là 17,56% và 82,44%. Luận văn phải nêu
cả tử số, mẫu số và phạm vi của phép phân rã; không được làm tròn thành một
claim chung không có phạm vi.

## Hàm ý cho frontier

Các ngưỡng pilot cố định trước holdout là 30.000/60.000/120.000 ms trên
`drop_eta_total_ms`, cộng mức unbounded. Đây là mức strictness thăm dò, không
phải threshold học từ sở thích người dùng. `forbiddenClaims` về preference và
satisfaction giữ nguyên.
