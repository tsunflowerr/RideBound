# RB-WP14R-010 — frontier descriptive trên slice 4 cell

> Ngày: 2026-09-02 (Asia/Bangkok)
> Phạm vi: **descriptive slice**, **không** phải frontier 16 cell đã đăng ký trước
> Nguồn: 40/160 job đã chạy trước khi matrix dừng ở
> [`wp14r-009`](wp14r-009-matrix-halt-plan-infeasible-2026-09-01.md)
> Report: `E:\RideBoundData\analysis\wp14r-slice-20260901\slice-frontier-v1.json`
> SHA-256 `02fd03a5f36be226a99f7f9609a26aee2679c20c83ef1baab744c6e0fd6d588f`

## 1. Vì sao phân tích được, và phân tích ở mức nào

Matrix dừng ở job 41 nên thiết kế 16 cell đã đăng ký **không tồn tại**. Nhưng 40 job
đã chạy tạo thành một lát cắt **hoàn chỉnh**: 4 cell × đủ 10 arm, tất cả `status pass`,
`repeatCount 1`.

Audit trước khi tin bất kỳ con số nào:

```text
repositoryInventorySha256   c8d4b108…de6c7   giống nhau ở CẢ 40 bundle
scenario content hash       1 distinct/cell   (4/4 cell)
arm mỗi cell                10/10             (4/4 cell)
label binding sai           0
status / repeatCount        pass / 1          (40/40)
```

Cùng một cây nguồn, cùng dữ liệu mỗi cell, cùng seed. Vì vậy các arm **so sánh được
với nhau**. Nếu inventory lệch giữa matrix thì mọi so sánh arm đã vô nghĩa.

Công cụ [`wp14r_slice_frontier.py`](../../simulators/fleetpy-ridebound/wp14r_slice_frontier.py)
**dùng lại nguyên `read_bundle` của analyzer đã freeze** — không định nghĩa lại một
metric nào. Nó chỉ định vị bundle theo layout WP14R (`<ledger>/<job>/attempt-NN/output`)
và tổng hợp. Analyzer đã freeze không tự chạy được vì đòi đủ 160 job và giả định layout
của WP14-v1.

## 2. Frontier

4 cell, cửa sổ `w08` ngày 2018-11-12, **432 lượt khách/arm**.

| arm | completed | Δ vs B1 | Δ pp | attributed burden (ms) | giảm vs B1 | riders charged | p95 drop |
|---|---:|---:|---:|---:|---:|---:|---:|
| `b1-ref` | 415 | — | — | 18.478.941 | — | 106 | 210.573 |
| `c1-nobudget` | 402 | −13 | **−3,009** | 2.064.716 | **88,83%** | 20 | 0 |
| `c1-budget120` | 398 | −17 | −3,935 | 1.036.827 | 94,39% | 16 | 0 |
| `c1-budget60` | 391 | −24 | −5,556 | 347.993 | 98,12% | 7 | 0 |
| `c1-h6ref` | 389 | −26 | −6,019 | 8.923 | **99,95%** | 1 | 0 |
| `c1-freeze300` | 389 | −26 | −6,019 | 8.923 | 99,95% | 1 | 0 |
| `c1-freeze600` | 389 | −26 | −6,019 | 8.923 | 99,95% | 1 | 0 |
| `c1-ratchet` | 389 | −26 | −6,019 | 8.923 | 99,95% | 1 | 0 |
| `c1-freeze300ratchet` | 389 | −26 | −6,019 | 8.923 | 99,95% | 1 | 0 |
| `c1-nopickuplock` | 389 | −26 | −6,019 | 8.923 | 99,95% | 1 | 0 |

Sáu arm cuối **trùng khít nhau**, nên frontier thực chất có **bốn điểm phân biệt**, không
phải mười. Pareto trên đúng hai trục đã khai báo (completed tăng, attributed burden
giảm) không loại được arm nào — hệ quả trực tiếp của việc sáu arm bằng nhau, không phải
một phát hiện.

## 3. Dự đoán đăng ký trước outcome — kết quả kiểm

Ba dự đoán được ghi trong
[`wp14r-prelaunch-analysis`](wp14r-prelaunch-analysis-2026-08-29.md) §5 và §5A, **trước
khi bất kỳ job nào chạy**.

### 3.1 F2 (ratchet) vô hiệu — **CONFIRMED**

```text
c1-ratchet           vs c1-h6ref     : giống hệt trên mọi counter, mọi cell
c1-freeze300ratchet  vs c1-freeze300 : giống hệt trên mọi counter, mọi cell
```

So sánh trên `arrived, completed, decisions, attributedPickupMs, attributedDropMs,
exogenousTotalMs, experiencedTotalMs, ridersCharged, disruptiveDecisions` — **không**
so `semanticHash`, vì `executionEvidence` nằm trong hash đó và chứa thời gian solver,
nên hai arm hành vi giống hệt vẫn hash khác nhau.

Cơ sở dự đoán: 916 lần decision dịch chuyển ETA trong corpus E1, **0 lần sớm hơn**.

### 3.2 Thứ tự budget — **CONFIRMED**

Dự đoán `h6ref ≲ budget60 < budget120 < nobudget ≤ b1-ref`.
Quan sát `389 ≤ 391 < 398 < 402 ≤ 415`.

Độ lớn cũng khớp: dự đoán budget 30→60 s gỡ ~7,6% khoảng cách; quan sát **2/26 = 7,7%**.
Dự đoán 30→120 s gỡ ~37,7%; quan sát **9/26 = 34,6%**.

### 3.3 "Cái giá là cấu trúc, không cấu hình nào cứu được" — **FALSIFIED**

Đây là nhận định của agent trong các phiên trước, và **dữ liệu bác bỏ nó**.

`c1-nobudget` cho **88,83% lợi ích gánh nặng với chỉ −3,009 pp**, so với `c1-h6ref`
là 99,95% với −6,019 pp. Tức **giảm một nửa mất mát dịch vụ mà vẫn giữ gần 90% lợi ích**.

Vùng trung gian tồn tại. Cách đọc "lưỡng cực ⇒ all-or-nothing" là sai, và phải sửa
trong mọi tài liệu về sau.

## 4. Phát hiện không nằm trong dự đoán nào

### 4.1 Toàn bộ nhóm pickup-side vô hiệu, kể cả bỏ hẳn lock

`c1-nopickuplock` bỏ **hoàn toàn** final-confirmation lock. Prune attribution xác nhận
điều đó có hiệu lực thật:

```text
c1-h6ref          drop_eta_total_ms 327 · pickup_eta_ms 77
c1-nopickuplock   drop_eta_total_ms 327 · pickup_eta_ms  0    <- lock đã bỏ thật
```

Vậy mà cả hai đều completed **389**. **77 witness pickup-lock không làm mất một khách
nào.** F1 (`freeze300`, `freeze600`) và F2 cũng đều dừng ở 389.

Hệ quả cho cách mô tả C1: nhóm ràng buộc điểm đón **sinh bằng chứng nhưng không tốn
dịch vụ** trên panel này. Điều này sắc hơn kết luận của WP13 (vốn chỉ nói attribution
83%/17% là first-witness và phụ thuộc thứ tự).

### 4.2 Hai ràng buộc không cộng được

| Bỏ gì | Gỡ được |
|---|---:|
| Bỏ lock, giữ budget (`c1-nopickuplock`) | **0 / 26** |
| Bỏ budget, giữ lock (`c1-nobudget`) | **13 / 26** |
| Bỏ cả hai (`b1-ref`) | 26 / 26 |

`0 + 13 ≠ 26`. Đây là bằng chứng trực tiếp, đo được, cho cảnh báo mà WP13 đã đưa ra:
**không được cộng attribution thành phân rã**. Phần chênh đến từ tương tác giữa hai
ràng buộc và việc gate làm rỗng tập lựa chọn của xe.

## 5. Ranh giới claim — bắt buộc giữ

- **4/16 cell.** 432 lượt khách/arm so với 2.160/arm mỗi panel của H6 ⇒ khoảng **1/5
  trọng số**. Đây **không** phải frontier đã đăng ký trước.
- **Không so trực tiếp** `−6,019 pp` ở đây với `−7,1296 pp` của H6: khác panel, khác
  ngày, khác cửa sổ, khác cỡ mẫu, khác số xe.
- Một ngày, một cửa sổ buổi sáng. Cửa sổ chiều `w17` **chưa có dữ liệu** vì matrix dừng
  ở đó; không được suy rộng sang cao điểm.
- Cột `publishedPickupImprovementCount` (2.629–2.768) **gộp cả exogenous drift**, đúng
  như đã chứng minh ở `wp14r-prelaunch-analysis` §4. Không dùng nó để nói về policy.
- Descriptive/exploratory, đúng nhãn `developmentExploratoryOnlyNotConfirmatory` mà
  WP14 vốn mang. Không rescue H6, không mở H7/WP15.

## 6. Điều slice này **không** trả lời

- Có tồn tại cấu hình tốt hơn trên **toàn** 16 cell hay không.
- Hành vi ở cao điểm chiều — đúng nơi semantic divergence xuất hiện.
- Vì sao validator của RideBound và của FleetPy bất đồng; đó là
  [`wp14r-009`](wp14r-009-matrix-halt-plan-infeasible-2026-09-01.md).
