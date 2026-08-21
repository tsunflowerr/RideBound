# Budget frontier pilot — RB-WP8-008

> 25/25 bundle hoàn tất, exit 0; dữ liệu pilot, không phải confirmatory.
> Output bất biến nằm tại `E:\RideBoundData\wp8\frontier-20260820`.

## Frontier C1

| Cell | tight: completed / burden | medium | loose | unbounded |
|---|---:|---:|---:|---:|
| `C-d20181112-r2` | 99 / 37.474 | 99 / 117.780 | 96 / 267.682 | 99 / 758.435 |
| `C-d20181113-r1` | 108 / 51.329 | 108 / 51.329 | 109 / 126.351 | 110 / 552.796 |
| `C-d20181113-r2` | 118 / 13.819 | 118 / 13.819 | 118 / 76.995 | 119 / 331.675 |
| `L1-peak2h-veh8` | 111 / 51.752 | 112 / 101.720 | 115 / 562.139 | 117 / 760.220 |
| **Tổng** | **436 / 154.374** | **437 / 284.648** | **438 / 1.033.167** | **445 / 2.403.126** |

Burden tăng đơn điệu từ tight đến unbounded trong cả bốn cell. Service không
đơn điệu ở mọi cell (`C-d20181112-r2` loose phục vụ 96 nhưng tight/medium và
unbounded đều 99), phù hợp với path dependence của trạng thái đội xe; không được
vẽ một đường service monotonic giả.

## Hai mức giá tách biệt

B1 unbounded hoàn thành 461/512 và có burden 14.771.708 ms.

1. **Giá của lock/ranking:** B1 → C1 unbounded mất 16 khách, tức 3,125 pp,
   trong khi burden giảm 83,73%.
2. **Giá thêm của budget 30 giây:** C1 unbounded → C1 tight mất thêm 9 khách,
   tức 1,758 pp, và giảm 93,58% phần burden còn lại.

Tổng B1 → C1 tight mất 25/512 = 4,883 pp. Ở cell đầu, budget là miễn phí về
service: cùng 99 khách tại tight/medium/unbounded trong khi burden giảm
758.435 → 117.780 → 37.474 ms. Không được gộp −8 khách B1→C1 của cell này
thành “giá của budget”; đó là giá của lock/ranking.

## Falsification baseline

B1 tight và B1 unbounded cho dữ liệu hành vi giống nhau. `semanticHash` vẫn khác
vì hash đó bind provenance/configuration, nên tiêu chí bàn giao yêu cầu hai
`semanticHash` bằng nhau là sai. Verifier mới tính thêm behavioral projection hash
loại các ID sinh từ cấu hình; cả hai bundle cùng:

`7bea0d96da6f8b8afb9e83464def853b0067fde92a4b6a41d982dc9d8fdf0ef8`.

Đây mới là falsification instrument hợp lệ. Mọi tài liệu sau dùng semantic hash
cho integrity/provenance và behavioral hash cho equivalence hành vi.

## Độ dốc đội xe, chỉ để diễn giải

| Xe | B1 hoàn thành | C1 unbounded hoàn thành |
|---:|---:|---:|
| 4 | 80 | 77 |
| 8 | 117 | 117 |
| 16 | 128 | 128 |

Độ dốc B1 cục bộ 4→8 là 9,25 khách/xe. Thiếu hụt C1 unbounded trung bình
4 khách/cell tương đương khoảng 0,43 xe tại đúng cell/slope này. Đây là phép
diễn giải thăm dò, không tham gia kiểm định và không được ngoại suy tuyến tính.

## Verification

- verifier độc lập: 25/25 bundle pass;
- bundle WP7 cũ: output mặc định byte-identical trước/sau sửa verifier;
- mutation một byte: bị từ chối;
- conservation chấp nhận cả completed/rejected thay vì giả định mọi khách được
  phục vụ và 32 xe;
- full solution tại source state sau hardening: 835/835; adapter: 57/57.

## Ba điều bất lợi phải giữ trong mọi trích dẫn của báo cáo này

25/25 run đã đóng sạch, nhưng dữ liệu nói ba điều chống lại cách đọc lạc quan.
Không được trích bảng frontier mà bỏ ba mục dưới đây.

### 1. Service không đơn điệu theo budget

`C-d20181112-r2` phục vụ 99 ở tight, 99 ở medium, **96** ở loose và 99 ở
unbounded. Nới budget không đảm bảo phục vụ nhiều hơn: trạng thái đội xe có path
dependence, và một quyết định "tốt hơn" ở epoch này có thể đặt xe vào vị trí xấu
hơn ở epoch sau. Mọi hình vẽ service theo budget phải để nguyên điểm gãy này;
không nội suy, không vẽ đường đơn điệu.

### 2. Trượt biên ở 8 xe, và trượt hoàn toàn về phía budget

Margin service đã prereg là `Δ_service_panel > −1,0 pp`. Tại cấu hình 8 xe:

| Bước | Hoàn thành | Δ khách | Δ pp (mẫu số 128) |
|---|---:|---:|---:|
| B1 unbounded → C1 unbounded | 117 → 117 | 0 | 0,00 |
| C1 unbounded → C1 tight | 117 → 111 | −6 | **−4,69** |

Toàn bộ khoảng trượt là **giá của budget**, không phải giá của lock: ở 8 xe giá
lock đúng bằng 0. −4,69 pp là **4,7 lần** margin. Tổng bốn cell cũng trượt:
B1 unbounded 461/512 so với C1 tight 436/512 là −25/512 = **−4,88 pp**.

Nếu confirmatory tái hiện độ lớn này thì service gate trượt, và không diagnostic
secondary nào được dùng để cứu. Điều đó phải được nói trước, không phải sau.

### 3. Khoảng 18% mức giảm burden là do định nghĩa lock, không phải tối ưu

Phần pickup-ETA của C1 giảm **vì lock cấm sửa nó**, không vì thuật toán tìm được
kế hoạch tốt hơn. Chỉ phần drop-ETA — thứ lock không giữ — mới là phần "earned".
Pilot cho tỷ lệ xấp xỉ **18% definitional / 82% earned**. Vì vậy:

- không được trích "giảm 83,73% burden" như một con số tối ưu hoá;
- mọi báo cáo phải tách hai phần và ghi rõ phần definitional;
- tỷ lệ 18%/82% là số **pilot**; confirmatory phải tự đo lại, không được mặc định.

## Năng lực là nhân tố, không phải chú thích

Ba điểm 4/8/16 xe hiện chỉ tồn tại ở mức unbounded, nhưng chúng đã đủ để bác luận
điểm "cơ chế cam kết rẻ" ở dạng phổ quát:

| Xe | B1 | C1 unbounded | Giá lock (pp, mẫu số 128) |
|---:|---:|---:|---:|
| 4 | 80 | 77 | −2,34 |
| 8 | 117 | 117 | 0,00 |
| 16 | 128 | 128 | 0,00 |

Ở 16 xe đội xe bão hoà (128/128) nên phép đo mất khả năng phân biệt — đó là trần,
không phải bằng chứng cam kết miễn phí. Ở 4 xe giá lock là thật. Phát biểu đúng
và bảo vệ được là: **chi phí dịch vụ của cam kết là hàm của mức sử dụng năng lực;
ở năng lực dư nó bằng 0, ở năng lực căng nó thật và đáng kể.** Phát biểu phổ quát
"cam kết rẻ" không được dữ liệu này ủng hộ.

Hệ quả thiết kế cho WP9: chạy frontier ở ít nhất hai cỡ đội xe, nếu không kết
luận chỉ đúng cho 8 xe. Xem `wp8-011d-pre-outcome-capacity-stratum-amendment.md`.
