# Kiểm tra cỡ mẫu và giới hạn suy luận — RB-WP8-007

> Phân loại: design-adequacy report. Bản 2026-08-21 thay thế kết luận `N=62`
> của bản đầu vì bản đó đếm seed solver như một mẫu độc lập và giả định hiệu
> ứng dịch vụ bằng 0 trái với chính số pilot đã ghi.

## Kết luận trước

Holdout đã khóa có 5 ngày × 4 demand realization = **20 đơn vị hữu hạn**.
Không có cách hợp lệ để biến chúng thành 62 đơn vị độc lập bằng cách đổi
`masterSeed`: seed chỉ đổi tie-breaking/robustness của solver trên cùng demand và
travel realization. Rider trong run và các seed solver của cùng cell đều không
phải mẫu độc lập.

Vì vậy WP9 dùng một **fixed-panel estimand** trên đúng 20 cell và giới hạn kết
luận vào panel đó. Không phát population-level p-value hay non-inferiority CI.

## Vì sao phép tính cũ sai

Bản đầu dùng công thức chuẩn với `Δ_true = 0`, rồi chuyển sang C2 chưa chạy và
nới margin từ 1,0 lên 1,5 pp để nhận `N=62`. Trong khi đó pilot ghi
`Δ_service = −3,125 pp`. Một NI calculation phải dùng khoảng cách tới margin:
với hiệu ứng pilot, khoảng cách `Δ_true - (−1 pp)` là âm, nên tăng N không thể
biến một hiệu ứng thật dưới margin thành non-inferior.

`N=62` cũng không tồn tại trong khung dữ liệu: chỉ có 20 demand realizations đã
khóa. Tùy chọn `--master-seed` vẫn được thêm vào harness, nhưng chỉ cho robustness
và blocking; nó không tham gia `ExperimentalUnitIdentity` và không tăng N.

## Chẩn đoán độ chính xác, không phải power claim

Nếu tạm dùng sample SD pilot 3,189 pp và công thức Normal để minh họa, nửa bề
rộng 95% tại 20 đơn vị xấp xỉ 1,40 pp. Con số này lớn hơn margin 1,0 pp và chỉ là
chẩn đoán: `n=4` không đủ để ước lượng ổn định variance, còn 20 cell chia sẻ chỉ
5 travel-day clusters.

Với 5 cụm ngày, exact sign-flip test có p-value nhỏ nhất `1/2^5 = 0,03125`, lớn
hơn mức một phía 0,025. Vì vậy một confirmatory population claim ở alpha đó là
không khả dụng từ holdout này, bất kể chạy thêm bao nhiêu solver seed.

## Estimand đã chốt

Với mỗi cell `j`, chạy cùng demand/travel realization cho B1 và treatment. Báo
cáo toàn bộ 20 paired deltas và hai aggregate hữu hạn:

Sau amendment node-cap pre-outcome, mỗi arm có denominator cố định
`20 × 108 = 2160`:

`Δ_service_panel = Σ completed_treatment / 2160 − Σ completed_B1 / 2160`.

Cổng service đạt chỉ khi `Δ_service_panel > −1,0 pp`. Primary burden là tổng và
phân phối của paired run-level delta trên 20 cell. Không dùng bootstrap/p-value
để nâng kết luận ra ngoài panel; interval/bootstrap nếu có chỉ ghi exploratory.

## Hệ quả

- Giữ nguyên margin 1,0 pp, dù pilot dự báo treatment có thể trượt.
- Không thay ngày, sample hay selection key sau khi nhìn outcome.
- Solver seeds chỉ nằm trong robustness WP9, báo riêng và không cộng vào N.
- Muốn population inference sau WP9 phải thu thập thêm ngày/travel realizations
  độc lập trong một work package mới; không được hồi tố đổi estimand hiện tại.

## Kiểm tra độc lập thực sự của 20 cell (đo 2026-08-22)

Nghi vấn đặt ra là bốn file `sample_10_*` của cùng một ngày chồng nhau khoảng
10% request id, khiến 20 cell không phải 20 đơn vị độc lập. Đo trực tiếp trên
source artifact `d9e86f33…c599e` cho kết quả **đúng ở tầng file nguồn nhưng
không lan tới panel đã phân tích**.

**Tầng file nguồn.** Overlap request id từng cặp trong cùng ngày:

| Phạm vi | Min | Max |
|---|---:|---:|
| Toàn ngày 24h (30 cặp) | 9,72% | 10,36% |
| Khung eligible `08:00–10:00` (30 cặp) | 8,30% | 10,69% |

Đây đúng là dấu hiệu của bốn lần rút 10% độc lập từ cùng một tổng thể ngày:
kỳ vọng overlap của hai mẫu 10% chính là 10%.

**Tầng panel đã phân tích.** Mỗi cell chỉ chạy 108 request được chọn từ
1.284–2.844 request eligible. Ánh xạ `sourceRecordOrdinal` của
`selection-frame.json` ngược về `request_id` cho 20 cell:

- tổng 20 × 108 = 2.160 slot chứa **2.157 request id phân biệt**;
- chỉ **3** request bị dùng lại (`d20181117` r3/r4: 1; `d20181118` r1/r2: 2);
- overlap trung bình từng cặp trong ngày là 0,10/108 = **0,09%**;
- overlap khác ngày là 0.

Vậy **không được** ghi "10% nhiễm chéo request" như một khuyết tật của panel.
Con số 10% là thuộc tính của file nguồn, không phải của dữ liệu đã phân tích.

**Ràng buộc thật sự vẫn nằm ở travel.** Bốn cell của cùng một ngày dùng đúng
cùng một `travelFactorMemberPath`
(`FleetPy_Manhattan/networks/Manhattan_2019_corrected/2018-11-DD_tt_factors.csv`),
kiểm trên cả 20 `normalizer-configuration.json`. Travel realization là hằng số
theo ngày, nên panel có **5 travel realization chứ không phải 20**, cộng thêm
cùng fleet/config. Đây mới là lý do 20 cell không phải 20 đơn vị độc lập, và nó
trùng đúng với 5 cụm ngày đã dùng để suy ra sàn p-value `1/2^5 = 0,03125`.

**Hệ quả cho thiết kế.** Không đổi kết luận đã chốt, chỉ đổi lý do:

1. Giữ fixed panel 20 cell và công bố độ chính xác đạt được — nửa bề rộng 95%
   xấp xỉ **1,40 pp** so với margin **1,0 pp**, tức thiết kế không đủ sức phân
   giải margin của chính nó. Con số này phải xuất hiện trong báo cáo WP9, không
   được để ẩn.
2. Muốn có 62 — hay bất kỳ N nào lớn hơn 20 — phải bổ sung **ngày thật sự độc
   lập**, vì mỗi ngày mới mới thêm một travel realization. Thêm
   `sample_10_*` của ngày cũ chỉ thêm demand draw trên cùng travel, và thêm
   solver seed thì không thêm gì cả.
3. `masterSeed` vẫn không thuộc `ExperimentalUnitIdentity`. Không hợp thức hoá N
   bằng seed, và không hợp thức hoá bằng bootstrap rider trong run.
