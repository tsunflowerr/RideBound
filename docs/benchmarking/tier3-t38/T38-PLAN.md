# T3.8 + T3.9: khoảng dự phòng cho lời hứa giờ trả (H3) và so số khách với hạn chót, luật theo giờ khách thấy (H2) trên panel A, chỉ tắc đường thật (THĂM DÒ CÓ NIÊM PHONG TRƯỚC)

Trạng thái: viết 2026-10-01. Niêm phong bằng SHA-256 (`T38-PLAN.sha256`). Commit và push lên nhánh nghiên cứu
`research/tier3-no-worse` **trước job đầu tiên**. Sau khi hiệu chỉnh xong và **trước job kiểm tra đầu tiên**, tệp γ
(`GAMMA-t38-v1.json`) cũng được commit và push.

## 0. Ai quyết định gì, và những gì đã được xem trước

- **8 điểm đề xuất** được chủ nghiên cứu duyệt ngày 2026-09-30 ("Sau khi xong thì làm luôn cái này đi"):
  1. giả thuyết "chở nhiều khách hơn" đổi thành "cùng số khách, chênh ≤ 2 khách mỗi ô; chỉ luật của khóa luận không cần xử lý kẹt";
  2. chỉ dùng dữ liệu thật, ngày 14–18/11 × 4 mẫu × 3 khung; ô `d20181114-s10-r1` không vào tập kiểm tra;
  3. có tính cuối tuần, chia đều theo ngày và báo riêng từng nhóm;
  4. mức tin cậy 90%;
  5. chỉ đo giờ trả, lấy khách bị dời nhiều nhất trong ô;
  6. định nghĩa "hữu ích" do chủ nghiên cứu chọn;
  7. không chọn luật trên dữ liệu: cố định `C-30` từ đầu;
  8. thăm dò có niêm phong trước.
- **Các giá trị còn mở do Claude chọn** theo ủy quyền ngày 2026-10-01 ("Bạn tự quyết cái nào là hợp lý và chạy đi"):
  - ngưỡng hữu ích 300 s;
  - biên tương đương ±2 khách;
  - sàn δ_s = 5 khách;
  - δ_r = 2 điểm %;
  - quy tắc bootstrap;
  - cách chia theo nhóm (ngày, mẫu).

  Lý do của từng giá trị ở §3.
- **Đã xem trước khi viết kế hoạch** (vì vậy đây là thăm dò):
  - kết quả Việc A, Việc B, w07x và phép quét trên panel phát triển;
  - điểm số S_j trên panel phát triển, 94–204 s ở 7–9h và 65/84 s ở hai ô sau giờ cao điểm (`../calib/h3-estimate-2026-09-30.txt`).

  Ngưỡng hữu ích 300 s được chọn **sau khi** đã thấy các số này. Mốc của nó là một thuộc tính của kịch bản, không phải
  của kết quả, xem §3.3.
- **Panel A:** 20 ô khung 8–10h đã được dùng ở Tầng 2, với thế giới và runner khác. Ô `d20181114-s10-r1-w08` đã bị xem ở
  smoke T3.5/T3.6. 29 ô khung 7–9h và 17–19h là ô **mới**, chưa từng chạy mô phỏng.
- **Chưa có lần chạy nào** trên 49 ô với cấu hình của kế hoạch này. Cách chia và mọi quy tắc được chốt trước lần chạy đầu tiên.

## 1. Tập episode

- **49 episode.** Ngày 14–18/11/2018 × mẫu r1..r4 × khung 07:00–09:00, 08:00–10:00, 17:00–19:00. Tổng 60 ô, trừ 11 ô
  bộ chuẩn hóa không dựng được.
  - Các ô bị loại: 15/11 17h ×4; 17/11 7h r1, r2, r4; 18/11 7h ×4.
  - Lý do: không chọn đủ 108 yêu cầu trong giới hạn 96 nút, kể cả sau ba lần thử lại theo quy tắc cố định
    (`../calib/panelA/normalize_panelA.py`, `normalize-per-cell-2026-10-01.log`).
  - Mọi lần thử lại chỉ đổi nhãn chọn mẫu, không liên quan kết quả mô phỏng nào.
- **Chuẩn bị.** `../calib/panelA/prepare_panelA.py` → `prepare-2026-10-01.log`, 0 vấn đề. Mỗi ô có 108 yêu cầu, 8 xe,
  96 nút, 9.120 cạnh. Ngày và giờ bắt đầu khớp tệp thế giới. Mã băm của driver khớp fixture.
  - Mọi yêu cầu có cửa sổ đón đúng 600 s (5.292/5.292, kiểm 2026-10-01).
  - Fixture nằm ở `C:\RideBoundData\research\fixtures-panelA-v1\`, driver ở `../calib/panelA/drivers/`, ngoài mọi kho.
  - Driver 7h và 17h là bản sao driver 8h cùng (ngày, mẫu), chỉ đổi `scenarioId` và hai mã băm kịch bản.
- **Chồng khách.** Ba khung của cùng một (ngày, mẫu) lấy từ cùng tệp nhu cầu. Khung 7h và 8h có chung 3–20 dòng
  nguồn trên 108.
- **Chia tập** (`make_split_t38.py` → `split-t38-v1.json`, SHA-256 `b72ac0f7…a03f1`, `make_split_t38.log`):
  - Đơn vị chia là nhóm (ngày, mẫu), để cùng một khách không nằm ở cả hai bên.
  - Nhóm `d20181114-s10-r1` vào tập hiệu chỉnh (ô bị xem ở smoke).
  - Rồi mỗi ngày bốc thêm 1 nhóm vào tập hiệu chỉnh bằng `random.Random(20261001)`; các nhóm còn lại vào tập kiểm tra.
  - **Hiệu chỉnh:** 15 episode, 6 nhóm (w07 4, w08 6, w17 5).
  - **Kiểm tra:** 34 episode, 14 nhóm (w07 9, w08 14, w17 11).
  - Không nhóm nào nằm ở cả hai bên.

## 2. Thiết kế chạy

| Mục | Giá trị |
|---|---|
| Thế giới | chỉ ngày và khung đo thật của từng ô (`../calib/panelA/worlds/W5-<ngày>-<khung>.json`, sigma = shockRate = 0), wrapper v1.2. Driver kiểm trước khi chạy rằng mỗi tệp thế giới đúng ngày, đúng khung, đọc được đủ 24 hệ số |
| Luật phục hồi | (b) cho mọi nhánh: runner `C:\RideBoundData\research\runner-tier3-v2` (tree `ac251ef3…cff3f`) + `../configs/wp4-tier3-v2.json` |
| Pha hiệu chỉnh | `C-30` trên 15 episode hiệu chỉnh = 15 job |
| Pha kiểm tra | `U`, `C-30`, `Mplus-30`, `Mplus-60`, `V-30` trên 34 episode kiểm tra = 170 job. `run_t38.py` **từ chối** pha này khi chưa có `GAMMA-t38-v1.json` |
| Cấu hình | các tệp trong `../configs/` mà Việc A và Việc B đã chạy, SHA-256 ghi trong `run_t38.py`. `C-30`, `Mplus-60` và `V-30` được chứng minh trùng byte trong `../w07x/make_configs_w07x.log` |
| Chạy | `run_t38.py run calib`, rồi `run_t38.py run test`. cwd `E:\Code\RideBound-deadline` (không sửa), `--expected-repository-inventory-sha256 0da63eaf…4cbe`. Output **mới** `C:\RideBoundData\research\tier3-t38-v1`, log `logs/` mode `x`, công tắc `STOP`, nhãn `<job>-t38`. Chỉ chạy khi cắm sạc, chờ tối đa 20 phút nếu đọc nguồn điện sai |
| Ước tính | hiệu chỉnh khoảng 1 giờ; kiểm tra khoảng 9–10 giờ (w07x: 1.159 s mỗi job, 6 luồng, khoảng 198 s mỗi job tính chung). Khoảng 15 GB (80 MB mỗi lần chạy). Ổ C: còn 84 GB trước khi chạy |
| Kiểm thử script | `test_t38.py`: 22/22 đạt. Điểm số của 8 ô Việc B trùng `h3-estimate`. Kiểm log bắt được sai khung và sai tệp thế giới. Kiểm đầu vào bắt được thế giới sai ngày và sai khung. Cách chia dựng lại trùng từng byte |

**Lỗi khi chạy.**
- Lỗi hạ tầng (máy ngủ, runner quá hạn phản hồi, `resource.cpu-time-exceeded`, mất điện): chạy lại **một lần** vào
  `<job>-rerun1` và ghi phụ lục. Thư mục lỗi giữ nguyên.
- Lỗi khác của một job `C-30`: điểm số của episode đó là **vô hạn**.
- Lỗi khác của một nhánh so sánh: ô đó bị loại khỏi phép so có nhánh ấy, và báo rõ.

## 3. H3: khoảng dự phòng (T3.8)

### 3.1 Điểm số

- **X_r** = tổng |x| trên các lần sửa giờ trả của khách r, với x = e − v là phần giờ trả dời đi khi giữ nguyên lộ trình
  (phần do đường). Đo bằng ms nguyên, từ audit v/e/p. Tổng theo khách phải khớp `sumDropX` của audit.
- **S_j** = max_r X_r trên mọi khách đã được hứa của episode j, chạy dưới `C-30`.
- Nếu có khách đã được hứa mà không bao giờ xuống xe, S_j = **vô hạn**, không mã hóa thành số. Episode không có khách
  nào được hứa có S_j = 0 (chưa gặp).
- **Vì sao đo như vậy:** V_r ≤ C_r + X_r, vì z = c + x ở mỗi lần sửa. `C-30` giữ C_r ≤ 30 s theo cấu tạo, vì không bao
  giờ bị ép. Vậy trong một episode có S_j ≤ γ, **mọi** khách có V_r ≤ 30 s + γ. Nghĩa là giờ trả khách thấy trên
  ứng dụng dời tổng cộng không quá β + γ.

### 3.2 Hiệu chỉnh

- α = 1/10.
- k = ⌈(n + 1)(1 − α)⌉ tính bằng số nguyên (`../calib/conformal.py`, 30 test, 11/11 đột biến bị bắt). Với n = 15 thì
  k = 15, tức γ = **điểm số lớn nhất** trong 15 episode hiệu chỉnh.
- Nếu một điểm số là vô hạn thì γ vô hạn.
- `t38_calibrate.py` kiểm hợp lệ trước khi tính. Điều kiện: 15/15 `status: pass`, cùng inventory, một
  `policyConfigurationHash`, và dòng log thế giới đúng ngày, đúng khung, mốc `bin × 900 s`, hệ số khớp tệp hệ số.
- Sau đó ghi `GAMMA-t38-v1.json` ở mode `x`. Tệp này được commit và push trước pha kiểm tra.

### 3.3 Quy tắc đạt H3 (cả hai điều kiện)

- **H3a, độ phủ:** tỉ lệ episode kiểm tra có S_j > γ phải ≤ 10%, tức ≤ 3/34.
  - Báo kèm cận trên Clopper–Pearson 95% một phía. Cận này **không** phải điều kiện đạt: với 34 episode, chỉ 0 vi phạm
    mới cho cận ≤ 10%.
  - Báo kèm cảnh báo: với n = 15, độ phủ thật (có điều kiện theo tập hiệu chỉnh) có phân bố Beta(15, 1). Xác suất nó
    dưới 90% là 0,9^15 ≈ 20,6%.
- **H3b, hữu ích:** β + γ ≤ **300 s** (β = 30 s).
  - Lý do: 300 s bằng một nửa cửa sổ đón 600 s mà mọi yêu cầu trong kịch bản đã có. Dời giờ trả nhiều hơn nửa cửa sổ ấy
    thì lời hứa khó còn nghĩa.
  - Ngưỡng này được chọn sau khi đã thấy điểm số trên panel phát triển (§0).
  - Báo thêm, **không** làm điều kiện: so với 120 s (2 phút), mức ví dụ trong đề xuất đã duyệt.
  - Không dùng đề xuất cũ của T3.1 (26,5 s): nó chắc chắn thất bại, vì chỉ riêng β đã là 30 s.
- **Kiểm tính đúng:** trong mọi episode kiểm tra được phủ, max_r V_r ≤ β + γ. Nếu sai thì đó là lỗi, phải tìm nguyên
  nhân trước khi báo.
- **Báo thêm:** S_j và độ phủ theo khung và theo ngày (mô tả). Không cắt γ.

## 4. H2: so số khách và gánh phục hồi (T3.9, trên 34 episode kiểm tra)

Với mỗi ô j:
- D_j^M = khách hoàn tất `C-30` − `Mplus-30`;
- D_j^V = khách hoàn tất `C-30` − `V-30`;
- F_j = `U` − `C-30`;
- ΔE2 = E2(`C-30`) − E2(so sánh), với E2 là tỉ lệ khách có V_r > 60 s.

**Bootstrap ghép cặp theo ô.** B = 10.000 vòng, `random.Random(20261002)`. Mỗi vòng bốc lại 34 ô có hoàn lại, và mọi
thống kê dùng chung một phép bốc. Khoảng 95% lấy trung bình vòng thứ 250 và thứ 9.751 trong 10.000 trung bình đã xếp
tăng dần. Tính trên các ô mà đủ năm nhánh đều `status: pass`.

**Quy tắc đạt H2** (phải đạt **cả bốn**; vì là phép giao nên không cần hiệu chỉnh đa phép so):
- **H2a, cùng số khách:** khoảng 95% của trung bình D^M **và** của trung bình D^V đều nằm trong [−2, +2] khách mỗi ô.
- **H2b, không cần xử lý kẹt:** `C-30` có 0 quyết định non-normal ở cả 34 episode kiểm tra (15 episode hiệu chỉnh báo kèm).
- **H2c, trải nghiệm không kém:** cận trên 95% của trung bình ΔE2 so với `V-30` **và** so với `Mplus-30` đều ≤ 0,02
  (δ_r = 2 điểm %).
- **Sàn:** cận trên 95% của trung bình F ≤ 5 khách mỗi ô (δ_s = 5).

**Lý do chọn các giá trị này:**
- ±2: một khách là 0,9–1,5% của một ô. Riêng việc sửa thời điểm đổi mốc đã dời C − V 2 khách (`world\README.md`).
- δ_s = 5: giá của cổng khi đường không đổi là 4–5 trên 108 khách. `C-30` so với `U` từ −5 đến +1 (T3.7).
- δ_r = 2 điểm %: E2 của ba luật trùng nhau ở mọi mức trên khung 7–9h (w07x). 2 điểm % tương đương khoảng 2 khách mỗi ô.

**Báo thêm (mô tả, không phải điều kiện):**
- tỉ lệ thất hứa theo thước của chính mỗi luật: `C` (C_r > 30 s), `Mplus-30` (A > 30 s), `Mplus-60` (A > 60 s),
  `V-30` (V_r > 30 s);
- tỉ lệ quyết định bị ép;
- số cách ghép bị chặn;
- tất cả chia theo khung và theo ngày;
- số ô mà `C-30` và `Mplus-30` có cùng chuỗi hành động.

## 5. Dự đoán (viết trước khi chạy; sai thì báo sai)

| # | Dự đoán | Cơ sở |
|---|---|---|
| P1 | γ nằm trong [100, 250] s | S_j phát triển: 94–204 s ở 7–9h, 65 và 84 s sau giờ cao điểm. Tập hiệu chỉnh có 4 ô 7–9h |
| P2 | H3b đạt (β + γ ≤ 300 s), nhưng β + γ > 120 s | từ P1 |
| P3 | H3a đạt: ≤ 3/34 episode kiểm tra vượt γ. Nếu có vi phạm, đa số ở khung 7–9h | hoán đổi được: kỳ vọng khoảng 34/16 ≈ 2,1 vi phạm |
| P4 | 0 quyết định non-normal ở cả 49 job `C-30` | Định lý 4; 0 ở mọi lần chạy `C` trước đây |
| P5 | H2a đạt: cả hai khoảng 95% nằm trong [−2, +2]; hơn nữa \|D_j^M\| ≤ 2 và \|D_j^V\| ≤ 2 ở mọi ô | luật (b): `C-30` = `Mplus-30` = `V-30` về số khách ở 8/8 ô 7–9h (Việc B), ở 30 s không ô nào lệch (w07x) |
| P6 | H2c và sàn đạt; trung bình F nằm trong [0, 3] khách mỗi ô | Việc B: `U` 665 so với `C-30` 660 trên 8 ô; W5 8h: 92/90; 17h: 100/99 |
| P7 | `Mplus-30` thất hứa theo thước của chính nó (A > 30 s) ở ≥ 8/9 ô 7–9h kiểm tra, và ở ≤ 3/11 ô 17–19h | Việc B: 8/8 ô 7–9h ở 30 s (24%); W5 17h: 0 |
| P8 | Tỉ lệ bị ép trung bình của `V-30` lớn hơn của `Mplus-30` trên cả 34 ô kiểm tra | 30 s ở 7–9h: 63,2 so với 58,6% (w07x); W5 17h: `V-30` bị ép 55% |

## 6. Sau khi chạy

- Phân tích bằng `t38_analyze.py`, viết trong lúc pha kiểm tra chạy, theo đúng §3–§5.
- Kiểm độc lập: một agent viết mã riêng và tính lại γ, độ phủ, H2a–H2c, sàn và P4 từ bản ghi thô.
- Báo cáo `T38-REPORT.md`: mọi kết quả, kể cả âm; dự đoán đúng và sai.
- Báo chủ nghiên cứu **trước**. Chỉ sửa luận văn khi chủ nghiên cứu đồng ý.
- Mọi thay đổi so với kế hoạch này ghi vào `T38-AMENDMENTS.md`, kèm thời điểm và lý do.
