# Báo cáo benchmark paired — WP8 pilot, 2026-08-19

> Phân loại bằng chứng: **pilot**. Không phải confirmatory, không phải kết quả
> effectiveness.
>
> Confirmatory holdout `2018-11-14` → `2018-11-18` chưa được sinh và chưa bị chạm.

Tài liệu này báo cáo đúng những gì đã chạy và đo được. Phần diễn giải thiết kế nằm ở
[`tasks/37`](../tasks/37-wp8-pilot-and-preregistration-refinement.md); bằng chứng về
điểm vận hành nằm ở [`wp8-001`](wp8-001-pilot-operating-point-evidence-2026-08-19.md).

## 1. Cấu hình đã chạy

| Thành phần | Danh tính |
|---|---|
| Runner | `candidate-portfolio-v8-identity-hotpath`, SHA-256 `13bf5d9b1dfbcb677d2d64c24038dba2c9adc22e664d2a6adecbf1905dcc179e` |
| FleetPy | tag `1.0.2`, commit `053aa9d4fcfde91c5d303435d5748f9206c071b0`, MIT |
| Python | CPython `3.10.20`, lock trong `simulators/fleetpy-ridebound/environment.lock.yml` |
| Nguồn dữ liệu | FleetPy Manhattan v1, Zenodo DOI `10.5281/zenodo.15187906`, CC BY 4.0, artifact SHA-256 `d9e86f33…599e` |
| B1 | `benchmarks/configurations/wp7-fleetpy-rolling-cost-v1.json` |
| C1 | `benchmarks/configurations/wp7-fleetpy-ridebound-hard-vector-v1.json` |

**Điều bảo đảm công bằng, kiểm bằng diff chứ không bằng lời:** hai file config của hai
arm giống nhau **từng byte trừ đúng trường `policyId`**. Cùng scenario, cùng seed, cùng
Runner binary, cùng work/solver budget, cùng retention strategy, cùng promise trigger.

## 2. Dữ liệu — thật, không custom

Grid được sinh bằng `tools/RideBound.Wp6Normalize --grid`, đọc trực tiếp từ bộ dữ liệu
công khai đã xác minh. Mọi cell bảo toàn đủ bản ghi nguồn:

| Cell | Nguồn | Bản ghi hợp lệ | Chọn | Loại |
|---|---|---:|---:|---:|
| pilot 24h, 4 cell | `2018-11-12_sample_10_{1..4}` | 21.400 | 128 | 0 |
| pilot 24h, 4 cell | `2018-11-13_sample_10_{1..4}` | 24.076 | 128 | 0 |
| `L1-peak2h-veh8` | `2018-11-12_sample_10_1`, cửa sổ `08:00–10:00` | 2.286 | 128 | 0 |
| `C-d20181112-r2` | `2018-11-12_sample_10_2`, cửa sổ `08:00–10:00` | 2.175 | 128 | 0 |
| `C-d20181113-r1` | `2018-11-13_sample_10_1`, cửa sổ `08:00–10:00` | 2.767 | 128 | 0 |
| `C-d20181113-r2` | `2018-11-13_sample_10_2`, cửa sổ `08:00–10:00` | 2.640 | 128 | 0 |

Các sample replicate là **sample chính thức của publisher**, không phải do dự án tự chia.

## 3. Điểm vận hành A — 24 giờ, 32 xe (điểm WP6/WP7)

Ba repeat mỗi arm, HEAD đóng băng `d48b115b`, verifier độc lập pass cả hai bundle.

| | B1 rolling-cost | C1 hard-vector |
|---|---:|---:|
| semantic hash | `723185de…60111` | `0f185e18…1ad3e` |
| checkpoint | `24800d18…5ddfd` | `96af727d…88408` |
| mỗi repeat | 128 request, 13.277 event, 3.082 frame, 1.025 epoch | như B1 |
| publication | 482 | 495 |
| rider hoàn thành | 128 | 128 |
| **decision-induced pickup ETA** | **0** | **0** |
| exogenous = visible | 3.459 ms | 3.640 ms |
| material revision | 3 | 0 |
| Σdelta so budget tích luỹ | 0 lệch | 0 lệch |

**Kết luận điểm A: không phân biệt được gì.** 100% chuyển động lời hứa là ngoại sinh;
phần do thuật toán gây ra bằng đúng 0 ở cả hai arm. Primary endpoint mà `docs/11` §2 đề
xuất bằng 0 đồng nhất, nên không cỡ mẫu nào cứu được.

## 4. Điểm vận hành B — cao điểm 2 giờ thật, 8 xe

Chỉ đổi cửa sổ và số xe so với điểm A; mọi tham số khác giữ nguyên. Một repeat mỗi arm
mỗi cell, vì tính tất định đã được chứng minh ở điểm A.

### 4.1 Bảng paired

| Đơn vị | hoàn thành B1/C1 | burden B1 | burden C1 | Δ burden | material B1/C1 | prePickupInserted B1/C1 |
|---|---|---:|---:|---:|---|---|
| `C-d20181112-r2` | 107 / 99 | 3.023.018 | 758.435 | −2.264.583 | 33 / 10 | 13 / 3 |
| `C-d20181113-r1` | 117 / 110 | 2.531.022 | 552.796 | −1.978.226 | 31 / 5 | 10 / 0 |
| `C-d20181113-r2` | 120 / 119 | 4.461.860 | 331.675 | −4.130.185 | 43 / 4 | 13 / 0 |
| `L1-peak2h-veh8` | 117 / 117 | 4.755.808 | 760.220 | −3.995.588 | 46 / 12 | 12 / 0 |

`burden` = tổng decision-induced pickup ETA + drop ETA trên toàn bộ rider. Dịch vụ đo ở
mốc **hoàn thành chuyến** (`passengerAlighted / requestArrived`), không phải mốc được hứa.

### 4.2 Thống kê

- Δ burden: âm ở **cả bốn** đơn vị; median `−3.130.086 ms`; mean `−3.092.146 ms`;
  sd `1.128.334 ms`. Mức giảm 75–93%.
- Δ tỷ lệ hoàn thành: `−6,25` / `−5,47` / `−0,78` / `0,00` điểm phần trăm;
  mean `−3,13 pp`; sd `3,19 pp`.

**Không có khoảng tin cậy nào được báo cáo.** Bốn đơn vị là một ước lượng phương sai để
tính cỡ mẫu, không phải một ước lượng hiệu ứng.

### 4.3 Lý do từ chối — kiểm tra công bằng then chốt

| Run | nhận | từ chối | lý do |
|---|---:|---:|---|
| `C-d20181112-r2-b1` | 107 | 21 | `MAX_RIDE_TIME` 8, `PICKUP_WINDOW` 13 |
| `C-d20181112-r2-c1` | 99 | 29 | `MAX_RIDE_TIME` 7, `PICKUP_WINDOW` 22 |
| `C-d20181113-r1-b1` | 117 | 11 | `MAX_RIDE_TIME` 1, `PICKUP_WINDOW` 10 |
| `C-d20181113-r1-c1` | 110 | 18 | `MAX_RIDE_TIME` 5, `PICKUP_WINDOW` 13 |
| `C-d20181113-r2-b1` | 120 | 8 | `MAX_RIDE_TIME` 2, `PICKUP_WINDOW` 6 |
| `C-d20181113-r2-c1` | 119 | 9 | `PICKUP_WINDOW` 9 |

**100% rejection ở cả hai arm là vật lý.** Không một rejection nào do ngân sách cam kết.
Thiệt hại dịch vụ do `PhysicalPlanValidator` tuyên bố — trọng tài dùng chung, giống hệt
nhau ở hai arm — chứ không do luật nào dự án tự viết ra cho có lợi.

## 5. Stratum nới lỏng — giới hạn cứng hoá ra vô tác dụng

Tạo `wp8-relaxed-stopswitch-commitment-v1.json` bằng cách bỏ `hardLimit` khỏi
`pickup_stop_switch_count` và `drop_stop_switch_count`, giữ `vehicle_switch_count = 0`
vì O-001 ràng buộc cả hai arm. Chạy lại C1 trên `C-d20181112-r2`:

| | completed | burden | material | prePickupInserted | semantic hash |
|---|---:|---:|---:|---:|---|
| C1 @ chặt | 99 | 758.435 | 10 | 3 | `9fa0fd24…` |
| C1 @ nới lỏng | **99** | **758.435** | **10** | **3** | `f30a202d…` |

Mọi chỉ số hành vi **giống hệt tới từng chữ số**; chỉ semantic hash khác vì
`policyConfigurationHash` nằm trong manifest. Đây đồng thời là minh hoạ sạch cho quy tắc
"hash khác không đồng nghĩa hành vi khác".

**Kết luận:** hai giới hạn đó chưa bao giờ cắt một candidate nào. Ở cấu hình hiện tại,
C1 **thuần tuý là chính sách xếp hạng theo revision**; phần hard vector không hoạt động,
nên mọi claim về "hard commitment budget" hiện chưa có bằng chứng chống lưng.

## 6. Bất đối xứng cấu trúc giữa hai arm

`SolverBackedRidePoolingPolicy.Decide`: `RollingCost` đi thẳng qua `switch` mà **không có
bộ lọc cam kết nào**; `RideBoundHardVector` chạy `AssessAndFilter` và loại bỏ candidate.
Vì vậy **pool của C1 là tập con của pool của B1**, và với accepted-count xếp đầu trong
objective, accepted count của C1 không thể vượt B1 ở từng epoch.

Hệ quả bắt buộc: đây **không phải hai đối thủ ngang hàng** mà là *có ràng buộc* so với
*không ràng buộc*. Phát biểu hợp lệ duy nhất là đường đánh đổi, không phải thắng thua.

## 7. Đối chiếu với tiêu chí dự án tự đặt trước

`docs/11` §5 đặt margin non-inferiority minh hoạ `1` điểm phần trăm; §14 quy định rằng
cải thiện revision mà trượt cổng service thì kết luận là *trade-off không thực dụng ở
cấu hình đó*.

Thiếu hụt dịch vụ trung bình `−3,13 pp` — gấp khoảng ba lần margin — và 3/4 đơn vị nằm
dưới ngưỡng. **Theo đúng tiêu chí đã đặt trước, C1 ở cấu hình hiện tại không vượt cổng
service tại điểm vận hành này.**

## 8. Giới hạn claim

- `n = 4`, một điểm vận hành, hai ngày. Không CI, không suy luận thống kê.
- Điểm vận hành được chọn **sau** khi quan sát null ở điểm A. Hợp lệ với pilot nhưng bắt
  buộc phải nằm trong preregistration trước khi chạy confirmatory.
- Chính sách cam kết vẫn là synthetic overlay, và §5 cho thấy phần cứng của nó vô tác dụng.
- Số resource và wall time được giữ nguyên làm chẩn đoán, **không** dùng cho bất kỳ phát
  biểu hiệu năng nào.
- Không claim effectiveness, non-inferiority, fairness, satisfaction, SLA hay novelty nào
  được cấp bởi tài liệu này.

## 9. Thư mục bằng chứng ngoài repo

| Nội dung | Đường dẫn |
|---|---|
| Điểm A, 3 repeat, verifier | `E:\RideBoundData\wp7\results\candidate-portfolio-v8-frozen-20260818` |
| Điểm B, cell `L1` | `E:\RideBoundData\wp8\load-probe-20260818c` |
| Điểm B, 3 cell còn lại | `E:\RideBoundData\wp8\contended-20260819` |
| Stratum nới lỏng | `E:\RideBoundData\wp8\pareto-relaxed-20260819` |

Cổng chất lượng tại thời điểm báo cáo: `dotnet test RideBound.slnx` **798/798**, 0 skipped;
`dotnet format --verify-no-changes` sạch; pinned Python adapter **50/50**.
