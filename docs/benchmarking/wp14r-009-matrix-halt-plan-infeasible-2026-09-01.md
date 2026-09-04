# RB-WP14R-009 — matrix dừng: `RBWP7_FLEETPY_PLAN_INFEASIBLE`

> Ngày: 2026-09-01 (Asia/Bangkok)
> Verdict: **MATRIX HALTED** — một job `exhausted`, `009` không thể hoàn thành dưới freeze v5
> Bản chất: **semantic divergence giữa RideBound và FleetPy**, không phải lỗi host/tài nguyên
> Tiến độ đạt được: **40/160 job**, tất cả đều `succeeded` và verify độc lập

## 1. Điều đã chạy được

Lần đầu tiên trong lịch sử WP14/WP14R, paired gate pass và matrix thực sự chạy:

| | |
|---|---:|
| Job hoàn tất | **40 / 160** |
| Cell hoàn tất | **4 / 16** (tất cả đều cửa sổ `w08`) |
| Job fail | 0 trong 40 |
| Attempt thứ hai phải dùng | 0 |
| Retained output | `4.723.568.734` B = 21,996% trần |
| Wall | ~8 giờ |

Mỗi job đều `independentVerificationStatus: valid`. Không job nào cần recovery.

## 2. Job dừng matrix

```text
jobId          w14-d20181112-s10-r1-w17-b1-ref-s7
elapsedSeconds 150.1        (cả hai attempt cộng lại)
attemptCount   2            -> exhausted, không có attempt 3
exitClassification  childExitFailure
independentVerificationStatus  valid   (ledger đúng; cái fail là job)
```

Đây là job đầu tiên của cửa sổ **`w17`**. Bốn cell đã xong đều là `w08`.

## 3. Nguyên nhân — nguyên văn từ journal

Cả hai attempt chết **giống hệt nhau**, cùng một dòng:

```text
ridebound_fleetpy.errors.AdapterFailure:
RBWP7_FLEETPY_PLAN_INFEASIBLE at $.vehicles[4].route:
veh plan for vid 4 feasible? False
  PS: (39,…) state BOARDING  bd {1: ['req-d11464…']}
      earl dep 1522   latest arr 2122   eta 2314.2439999999997
```

Đọc thẳng: Runner của RideBound phát ra một route trong đó xe tới điểm đón của
`req-d11464…` ở **giây 2314**, trong khi thời hạn đón muộn nhất của yêu cầu đó là
**giây 2122**. Trễ **192,24 giây**.

Con số khớp đúng cấu hình: `pickupWindowMs = 600.000` ms, và
`1522 + 600 = 2122`. Vậy `latest arr` chính là hạn đón của FleetPy.

`_apply_decision → _fleetpy_plan` là đường adapter **áp** quyết định của Runner vào
FleetPy. Nghĩa là RideBound đã **phát ra** kế hoạch này và validator của chính nó
chấp nhận; FleetPy mới là bên từ chối.

## 4. Vì sao đây là phát hiện chứ không phải sự cố

| Đặc điểm | Quan sát |
|---|---|
| Tất định | Hai attempt độc lập chết cùng một chỗ, cùng một vehicle, cùng request |
| Không phải host | Không liên quan RAM, CPU, đồng hồ, đĩa; preflight pass |
| Không phải làm tròn | Trễ 192 giây, không phải vài mili giây |
| Xảy ra ở arm **B1** | Nhóm **không** có ràng buộc commitment nào — nên không phải do cơ chế C1 |
| Chỉ lộ ở `w17` | 40 job `w08` liên tiếp đều pass |

`w17` là cửa sổ 61.200–68.400 giây, tức **17:00–19:00 giờ địa phương** — cao điểm
chiều. Feasibility chặt hơn hẳn `w08` (08:00–10:00). Chính vì thế edge case này chỉ
xuất hiện ở đó.

Đây là một **semantic divergence Layer-2**: validator vật lý của RideBound và
validator của FleetPy bất đồng về việc một route có khả thi hay không. WP7 đã đóng
"mechanical Layer-2" nhưng chưa từng chạy dưới điều kiện cao điểm của panel mới.

## 5. Phạm vi ảnh hưởng

Grid có **8 cell `w08` và 8 cell `w17`** — chia đôi. Job vừa fail là job đầu tiên của
`w17`, nên **80/160 job có nguy cơ cùng chế độ hỏng**. Chưa xác nhận vì chỉ mới chạm
một job, và xác nhận thêm sẽ tiêu attempt của job khác.

## 6. Trạng thái matrix

`authorize_phase` từ chối mọi job matrix khi còn một job `exhausted`, và
`recoveryPolicy` không có attempt 3. Vì vậy `RB-WP14R-009` **không thể hoàn thành**
dưới freeze v5. 40 bundle đã sinh vẫn nguyên vẹn và verify được.

## 7. Vì sao agent dừng ở đây

Mọi hướng sửa đều chạm vào **thiết kế khoa học**, không phải mechanics của protocol:

- `ridebound_fleetpy/fleet_control.py` nằm trong adapter tree mà **freeze v1 (ADR-069)**
  bind nguyên cây. Sửa nó làm freeze v1 không còn verify, và freeze v1 là base mà H6,
  E1, WP14-v1 và toàn bộ chuỗi WP14R tham chiếu byte-exact.
- Ba freeze v3/v4/v5 trước chỉ chạm **protocol mechanics**; đây là lần đầu chạm tới
  thiết kế khoa học, nên nó vượt thẩm quyền của agent.

## 8. Ba lựa chọn cho chủ nghiên cứu

| | Nội dung | Cái giá |
|---|---|---|
| **A** | Điều tra và sửa semantic gap giữa hai validator, rồi freeze scientific design mới | Chạm freeze v1 ⇒ ảnh hưởng nền của H6/E1; lớn nhất, nhưng sửa đúng gốc |
| **B** | Thu hẹp frontier về **chỉ `w08`** (8 cell × 10 arm = 80 job), khai báo `w17` ngoài phạm vi kèm typed failure làm bằng chứng | Cần freeze mới cho grid 8 cell; 40 job `w08` đã xong nhưng bound vào freezeId v5 nên phải chạy lại (~8 giờ) |
| **C** | Dừng `009`, báo cáo kết quả một phần: 40 job, 4 cell, cộng phát hiện divergence | Không có frontier; nhưng phát hiện này tự nó là kết quả đáng công bố |

Dù chọn gì: **không hồi sinh job đã exhausted**, không sửa file freeze-bound giữa run,
và 40 bundle đã sinh được giữ nguyên.

## 9. Điều **không** được kết luận

- Không phải kết quả về service/burden: chưa đọc completion, burden hay route nào.
- Không nói `w17` chắc chắn hỏng toàn bộ — mới quan sát đúng một job.
- Không nói RideBound sai và FleetPy đúng; mới chỉ biết **hai bên bất đồng**, và
  hướng của bất đồng là RideBound chấp nhận thứ FleetPy từ chối.
- Không đổi H6, E1, WP14-v1 hay bất kỳ kết quả đã đóng băng nào.
