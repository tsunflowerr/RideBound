# WP14R — phân tích trước khi launch matrix

> Ngày: 2026-08-29 (Asia/Bangkok)
> Trạng thái: **không phải ticket, không ADR, không authorize gì**
> Scientific execution: `0 jobs`, `0 attempt` tiêu thụ
> Mục đích: làm mọi việc kiểm chứng được **trước** khi tiêu ~34–43 giờ máy cho matrix

## 1. Vì sao có tài liệu này

`RB-WP14R-008` bị chặn bởi host precondition, nên có một cửa sổ để làm đúng thứ mà
một benchmark tốn hàng chục giờ cần: kiểm mọi thứ kiểm được trước, thay vì phát hiện
lỗi ở job thứ 140. Tài liệu ghi lại bốn kết quả, trong đó **hai cái làm đổi cách đọc
frontier report sắp tới**.

## 2. Host preflight — blocker đã đổi

Preflight thật cho `w14-d20181112-s10-r1-w08-b1-ref-s7` attempt 1:

| Gate | Ngưỡng | Quan sát | Kết quả |
|---|---|---:|---|
| AC line | `online` | `online` | **pass** — `POWER_SOURCE_NOT_AC` đã hết |
| Power scheme | Balanced GUID | khớp exact | pass |
| CPU 10×1 s | mean ≤20%, single ≤60% | mean `3.618%`, max `9.561%` | pass |
| Free disk | ≥25 GiB | `144.954.011.648` B | pass |
| Available memory | ≥`8.589.934.592` B | `8.533.823.488` B | **fail** — thiếu `56.111.104` B |

Hai observation append-only đã được ghi trong ngày:

| Receipt | Available memory | Thiếu | Verdict |
|---|---:|---:|---|
| `preflight-attempt-01-observation-0001.json` | `8.533.823.488` B | `56.111.104` B | `MEMORY_BELOW_MINIMUM` |
| `preflight-attempt-01-observation-0002.json` | `8.364.986.368` B | `224.948.224` B | `MEMORY_BELOW_MINIMUM` |

Cả hai đều `prospectiveAttemptNumber: 1`: **zero attempt tiêu thụ, zero job launched**,
`008` vẫn Ready. Lần thứ hai chạy ngay sau `dotnet build-server shutdown` và sau khi
dừng `msedgewebview2`; host vẫn không đạt vì webview2 tự khởi động lại và baseline của
IDE/browser chiếm phần còn lại. Đây là giới hạn của host, không phải của protocol, và
threshold **không** được hạ để lấy pass.

Sleep/hibernate trên scheme đã freeze đều là **Never** trên AC, nên giấc ngủ máy
**không** phải nghi vấn cho lần chấm dứt C1 của WP14-v1.

## 3. Verify lại WP14 — pass toàn bộ

| Kiểm tra | Kết quả |
|---|---|
| Freeze v1 rebuild/verify | pass — exact `jobs=160`, `repositoryFiles=46`, SHA `1ce26ff0…37a55` |
| Freeze v2 rebuild/verify | valid — `jobCount=160`, SHA `6b340108…a31237` |
| B1 bundle giữ lại, independent verifier | pass — behavioral hash `5f2af778…32c5c0`, đúng ADR-070 |
| C1 partial | `83.364.599` B, `bundle-manifest.json` vẫn vắng, không bị chạm |
| E1 source-divergence | frozen bytes `b550c14c…c06e54` khôi phục được từ commit `38d517d` |

Hai freeze vẫn verify exact **sau khi** thêm hai file source mới ở mục 6, xác nhận
tree binding không bắt file phụ trợ ngoài danh sách.

## 4. Analyzer shakeout — pass, và một giới hạn quan trọng

`analyze()` đòi đủ 160 job nên không chạy được trước matrix, nhưng `read_bundle()` —
nơi chứa verification, đọc transcript tới EOF, integer-canonical counter, nearest-rank
p95 và tách pickup improvement/worsening — thì gọi độc lập được. Chạy trên bundle B1
thật 125 MB:

```text
status: readBundleSucceeded
arrived 108 · completed 104 · decisions 824
attributedTotalMs 6.270.875 · exogenousTotalMs 409.109 · experiencedTotalMs 6.678.214
pickupEtaImprovementCount 654 · pickupEtaWorseningCount 703
ridersCharged 28 · riderDropConsumptionP95Ms 208.790 · max 538.934
semanticHash d072b931…c8393d  (khớp semanticHash của independent verifier)
```

Đường per-bundle của analyzer **chạy sạch trên dữ liệu thật**. Nhưng nó lộ ra một giới
hạn không thể sửa được:

> `read_bundle` tính `pickup_change = pickup - before_pickup` trên chuỗi promise **đã
> publish**, **không lọc theo `decisionDelta`**.

Nghĩa là `pickupEtaImprovementCount` gộp cả dịch chuyển do **exogenous travel-time
drift** lẫn do quyết định. Với mục đích mô tả "rider thấy gì" thì đúng; với câu hỏi
"cơ chế policy có bao giờ kích hoạt không" thì **sai**.

`wp14_frontier_analyze.py` nằm trong 46 file bị freeze v1 bind, nên **không được sửa**.
Cách xử lý đúng là công cụ **bổ sung**, giống hệt bài học ADR-067: tạo cái mới, không
nới nghĩa cái cũ.

## 5. Dự đoán đăng ký trước: F2 (ratchet) **vô hiệu**

Handoff WP14 bắt buộc frontier report trả lời: *"F2 (ratchet) có xảy ra lần nào không?
Nếu cải thiện pickup không bao giờ xảy ra thì F2 vô hiệu và phải báo là vô hiệu."*
Mục 4 vừa cho thấy analyzer **không** trả lời được câu đó. Vì vậy câu hỏi được trả lời
trước, từ evidence đã có, bằng công cụ mới ở mục 6.

Ratchet chỉ nới lock cho candidate làm ETA **sớm hơn**. Tập quan sát cần đếm là:
publication mà `decisionDelta` của đúng chiều đó khác 0, và promise dịch sớm hơn.

### Corpus E1 đầy đủ — 80 bundle

```text
Report: E:\RideBoundData\analysis\wp14r-promise-direction-20260829\e1-promise-direction-v1.json
SHA-256: 958843ef687465f660f83d1b50c8f253682bed0cef194f37dda31c3d3dad8869
```

| | pickup | drop |
|---|---:|---:|
| Publications decision dịch chuyển | 100 | 816 |
| … **sớm hơn** | **0** | **0** |
| … muộn hơn | 100 (`14.636.839` ms) | 816 (`105.040.829` ms) |
| Exogenous-only sớm hơn | 30.421 | 70.457 |
| Exogenous-only muộn hơn | 31.226 | 66.354 |

Tổng: **142.769 publication, 137.627 revision, 916 lần decision dịch chuyển ETA, 0 lần
sớm hơn.**

### Development panel — bundle B1 dry-run

```text
Report: …\dev-panel-b1-direction-v1.json
SHA-256: b3d3dc106a801837c410b494724f4f1cee6674e6167400f77e5d9442545071a7
```

48 lần decision dịch chuyển, **0 sớm hơn**. Và cross-validation quyết định:
analyzer đã freeze báo `pickupEtaImprovementCount = 654`; công cụ mới cho thấy **đúng
654 lần đó đều là `exogenousOnlyEarlier`, 0 do decision**. Hai công cụ độc lập, cùng
một bundle, số khớp tuyệt đối — và nó chứng minh trực tiếp rằng metric của analyzer
không dùng được cho câu hỏi F2.

### Giải thích cơ chế

Một quyết định **chèn** request mới vào route đang chạy. Chèn chỉ có thể thêm detour
cho rider đang trên xe hoặc đang chờ. O-001/ADR-018 cấm đổi xe cho request đã accept ở
**cả hai** arm, nên không còn cơ chế nào để một quyết định rút ngắn lời hứa của người
khác. Về nguyên tắc, sắp xếp lại thứ tự stop trong cùng một xe **có thể** cải thiện —
nên đây không phải bất khả thi theo cấu tạo — nhưng nó không xảy ra lần nào trong
916 quan sát.

### Dự đoán được đăng ký

> Arm `c1-ratchet` sẽ **giống hệt hành vi** `c1-h6ref`, và `c1-freeze300ratchet` sẽ
> giống hệt `c1-freeze300`, trên cả 16 cell. 32/160 job (~7 giờ) sẽ chứng minh một
> kết quả âm đã biết trước.

Đây là **dự đoán, không phải kết quả**, và nó **không** cho phép bỏ arm: freeze v1 cố
định exact 160 job và cấm giảm cell/arm. Giá trị của nó là hai chiều:

- nếu đúng, F2 được báo là vô hiệu với bằng chứng đăng ký **trước** outcome, mạnh hơn
  nhiều so với phát hiện sau;
- nếu **sai** — tức ratchet có kích hoạt trên development panel — thì dự đoán này bị
  bác bỏ và đó là phát hiện đáng chú ý, vì nó nghĩa là stop-reordering cải thiện lời
  hứa thật sự tồn tại ở panel mới.

Dự đoán này **không** được dùng để chọn hay đổi bất kỳ factor level nào; toàn bộ factor
đã bị freeze từ ADR-069 và không thể đổi.

## 5A. Dự đoán đăng ký trước cho cả 10 arm

Mục 5 khoá F2. Phần này mở rộng ra toàn matrix để `011` trở thành bài kiểm chứng
**confirmatory** đối chiếu dự đoán, thay vì đọc số rồi mới kể chuyện. Mọi con số dưới
đây đã tồn tại trước khi matrix chạy; không con số nào được dùng để **chọn** factor
level — toàn bộ factor đã bị ADR-069 freeze và không thể đổi.

### Cơ sở đo lường

Prune của C1 tách đúng hai nguồn: `drop_eta_total_ms` budget 30 s chiếm **83%**
(Panel A 780, Panel B 491) và final-confirmation lock trên `pickup_eta_ms` chiếm
**17%** (160 / 92). Tiêu thụ tích luỹ trên arm **không** bị ràng buộc (B1 Panel A,
n = 1.735) là lưỡng cực:

| Ngưỡng | Số request | % panel | % của tập bị ràng buộc |
|---|---:|---:|---:|
| bằng 0 | 1.332 | 76,8% | — |
| > 30.000 ms | 369 | 21,3% | 100% |
| > 60.000 ms | 341 | 19,7% | 92,4% |
| > 120.000 ms | 230 | 13,3% | 62,3% |
| > 300.000 ms | 48 | 2,8% | 13,0% |

### Dự đoán

| Arm | Cơ chế được nới | Dự đoán so với `c1-h6ref` | Điều kiện phủ định |
|---|---|---|---|
| `c1-budget60` | budget 30 → 60 s | service tăng **rất ít**: chỉ 28/369 ≈ **7,6%** tập bị ràng buộc được giải phóng | nếu service hồi phục lớn thì đọc lưỡng cực là sai |
| `c1-budget120` | budget 30 → 120 s | tăng **trung bình**: 139/369 ≈ **37,7%** | như trên |
| `c1-nobudget` | bỏ budget | tăng **lớn nhất trong nhóm budget**, xoá 100% nguồn prune 83%; phần còn lại chỉ là lock | nếu vẫn kém `b1-ref` nhiều thì còn nguồn mất mát thứ ba chưa biết |
| `c1-nopickuplock` | bỏ pickup lock | xoá nguồn prune 17%; **nhỏ hơn** `c1-nobudget` | nếu lớn hơn `c1-nobudget` thì tỷ lệ 83/17 không chuyển sang panel mới |
| `c1-freeze300` | lock cả pha → horizon 300 s | nằm **giữa** `c1-h6ref` và `c1-nopickuplock` | nếu bằng `c1-nopickuplock` thì mọi lock prune đều nằm ngoài 300 s ⇒ lock cả pha là thừa |
| `c1-freeze600` | horizon 600 s | nằm giữa `c1-h6ref` và `c1-freeze300` | nếu bằng `c1-freeze300` thì horizon không phân biệt trong khoảng đó |
| `c1-ratchet` | F2 | **trùng khít** `c1-h6ref` | bất kỳ khác biệt nào cũng bác dự đoán mục 5 |
| `c1-freeze300ratchet` | F1+F2 | **trùng khít** `c1-freeze300` | như trên |

Thứ tự service dự đoán:

```text
c1-h6ref ≲ c1-budget60 < c1-budget120 < c1-nobudget ≤ b1-ref
c1-h6ref < c1-freeze600 ≤ c1-freeze300 ≤ c1-nopickuplock
c1-ratchet ≡ c1-h6ref      c1-freeze300ratchet ≡ c1-freeze300
```

Burden dự đoán đi ngược chiều ở mọi cặp.

### Ranh giới của các dự đoán này

Tiêu thụ là **nội sinh**: dưới một budget khác, quỹ đạo đổi nên phân phối tiêu thụ
cũng đổi. Vì vậy đây là dự đoán **thứ tự và hình dạng**, không phải dự đoán điểm; không
được diễn giải `7,6%` hay `37,7%` thành số phần trăm service sẽ hồi phục. Chúng cũng
đo trên H6/E1 nên chỉ mô tả quá khứ; nếu development panel cho thứ tự khác thì **panel
mới đúng** và dự đoán bị bác — đó chính là giá trị của việc đăng ký trước.

## 6. Hai công cụ mới (không nằm trong freeze nào)

| File | Vai trò | Test |
|---|---|---:|
| [`wp14r_promise_direction.py`](../../simulators/fleetpy-ridebound/wp14r_promise_direction.py) | Tách chiều dịch chuyển promise theo decision vs exogenous — thứ analyzer đã freeze không ghi | 7/7 |
| [`wp14r_matrix_driver.py`](../../simulators/fleetpy-ridebound/wp14r_matrix_driver.py) | Chạy matrix theo lô, dừng ở exhausted/fail/batch limit/guard byte | 8/8 |

Driver **không có thẩm quyền riêng**: nó không thể đổi thứ tự, retry, bỏ qua failure
hay nới gate — mọi thứ đó do protocol cưỡng chế và protocol verify lại freeze mỗi lần
gọi. Driver chỉ quyết định *khi nào dừng*. Chạy `--status-only` trên freeze thật cho
đúng 160 job theo thứ tự, next `w14-d20181112-s10-r1-w08-b1-ref-s7`, retained 0 B.

Cả hai file đều **không** nằm trong freeze v1 (46 file) hay freeze v2 (24 file), và cả
hai freeze vẫn verify exact sau khi thêm chúng.

## 7. Trần retained-output: rủi ro thật nhưng nhỏ hơn ước lượng thô

### Số học của trần

```text
maximumOutputBytes (freeze v1, v2 kế thừa)  21.474.836.480 B
Chiếu thô ADR-070 (160 × bundle B1)         20.037.964.320 B  = 93,31% trần
16 job B1                                    2.003.796.432 B
Còn lại cho 144 job C1                      19.471.040.048 B
⇒ điểm hoà mỗi bundle C1                       135.215.555 B
```

`authorize_phase` **hard-fail** khi tổng retained vượt trần, và nó cộng **mọi attempt
của mọi job**, nên một recovery attempt ăn thêm nguyên một bundle.

### Nhưng chiếu thô đó giả định sai

Chiếu 93,31% coi mọi job đều to bằng B1. Đo kích thước transcript thật của 80 bundle
E1 cho thấy điều ngược lại:

| Arm | n | median | mean | min | max |
|---|---:|---:|---:|---:|---:|
| B1 | 40 | 65.325.166 | 72.455.250 | 30.982.984 | 119.346.889 |
| C1 | 40 | 60.313.073 | 65.434.360 | 29.268.448 | 109.547.646 |

**C1 nhỏ hơn B1**, tỷ lệ median `0,9233`. Lý do có cơ chế rõ: C1 bị gate cắt candidate
nên phục vụ ít request hơn, phát ít promise publication hơn — đúng như đo được ở mục 5
(E1 B1 có 77.873 publication, C1 có 64.896, tỷ lệ `0,833`).

Áp tỷ lệ đó lên bundle B1 của development panel:

```text
C1 ước lượng                115.628.410 B   (dưới điểm hoà 135.215.555 B)
Tổng matrix ước lượng    18.654.287.495 B   = 86,87% trần
Verdict                                       FITS, dư ~2,8 GB ≈ 22 attempt phục hồi
```

### Hai điều làm ước lượng này vẫn chưa đủ để yên tâm

1. E1 dùng `retained-portfolio-v1`; WP14 development dùng
   `retained-portfolio-full-witness-v1`, ghi **toàn bộ** witness set thay vì witness đầu
   tiên. Arm C1 mới là nơi có witness, nên profile mới đội kích thước C1 lên một lượng
   **chưa đo được**. Tỷ lệ `0,9233` vì thế là **cận dưới** của tỷ lệ thật.
2. Hai arm nới lỏng `c1-nobudget` và `c1-nopickuplock` prune ít nhất nên phục vụ nhiều
   nhất, và sẽ là những bundle lớn nhất trong 144 job C1 — không phải median.

⇒ Kết luận hiệu chỉnh: **matrix nhiều khả năng vừa trần**, không phải "gần chắc vỡ" như
chiếu thô gợi ý. Nhưng biên chỉ khoảng 13% và có hai nguồn đội giá chưa đo, nên
paired gate vẫn là phép đo quyết định: nó cho bundle C1 thật, đúng panel, đúng profile.

Driver mới có `--stop-at-output-percent` (mặc định 90%, tức `19.327.352.832` B) để dừng
**sạch trước khi bắt đầu** một job có nguy cơ, thay vì để `authorize_phase` ném lỗi
giữa job đã tiêu wall time.

Nếu paired gate cho thấy bundle C1 vượt `135.215.555` B, đây là **quyết định cấp
protocol của chủ nghiên cứu**: cần freeze v3 (nới `maximumOutputBytes` hoặc dùng
evidence profile nhẹ hơn cho arm C1), freeze **trước outcome**, tuyệt đối không sửa
giữa run.

## 8. Bảng verification hiện hành

```text
dotnet build                        0 warning / 0 error
pinned Python/FleetPy               353/353 pass, 0 skip, 67,172 s
  ├─ test_wp14r_matrix_driver         9/9
  └─ test_wp14r_promise_direction     7/7
dotnet test RideBound.slnx          908/908 pass, 0 fail, 0 skip
                                    Benchmarking.Tests 148/148 in 1 m 31 s (ceiling 120 s)
dotnet format --verify-no-changes   PASS
git diff --check                    PASS (chỉ warning LF→CRLF lịch sử)
Markdown  290 file / 394 local link / 0 broken / 0 unbalanced fence
JSON      1.350/1.350 parse hợp lệ
Python line >88 trên file mới       0
Freeze v1 / v2 rebuild-verify       PASS exact sau khi thêm hai file mới
```

## 9. Điều tài liệu này **không** làm

- Không authorize `008..012`, không mở WP15/H7, không sinh ADR.
- Không đổi margin, panel, denominator, factor level, arm set hay failure treatment.
- Không sửa `wp14_frontier_analyze.py` hay bất kỳ file nào trong hai freeze.
- Không ghi/di chuyển/xoá bất kỳ raw root H6, E1 hay WP14-v1 nào.
- Không claim causation: `decisionDelta` khác 0 nghĩa là quyết định có dịch chuyển
  chiều đó, không nói phần nào của thay đổi published là do nó.
- Không dùng dự đoán F2 để bỏ arm hay rút ngắn matrix.
