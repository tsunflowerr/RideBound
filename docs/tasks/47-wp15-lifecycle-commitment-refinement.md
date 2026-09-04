# WP15 — refinement cho lifecycle-aware commitment v2

> Trạng thái: `REFINEMENT DRAFT — UNAUTHORIZED TO RUN`
> Ngày: 2026-08-28
> Điều kiện mở: **chỉ** sau khi WP14R đóng `008..012` và có frontier được verify độc lập
> Evidence nền: [wp15-commitment-design-new-paper-evidence-2026-08-28.md](../research/wp15-commitment-design-new-paper-evidence-2026-08-28.md)

## 0. Vì sao file này tồn tại và nó **không** phải cái gì

Chủ nghiên cứu yêu cầu tiến hành WP15. WP15 **không thể execute** ở checkpoint này vì
input của nó — service–burden frontier từ `RB-WP14R-011` — chưa tồn tại. Thiết kế
policy v2 mà không có frontier chính là failure mode mà ADR-065/066 và toàn bộ ordered
queue được dựng để chặn, và handoff WP14 ghi rõ: *"Không ép frontier phải đẹp."*

Vì vậy file này là **refinement**, đúng vai của `tasks/43` cho WP14 và `tasks/45` cho
WP14R: khoá trước design space, falsification condition và ranh giới claim, để khi
frontier có thật thì không ai phải thiết kế dưới áp lực của một con số vừa nhìn thấy.

File này **không**:

- authorize bất kỳ execution nào, kể cả development;
- đổi H6, E1, WP14-v1, freeze v1/v2, margin, panel, denominator hay failure treatment;
- mở H7, WP16, WP17 hoặc lifecycle policy v2 như một arm được đo;
- ticket hoá `RB-WP15-xxx`. Ticket chỉ được sinh sau một ADR riêng.

## 1. Câu hỏi của WP15

WP9/H6 trả lời: ràng buộc *số lần và mức độ sửa* một lời hứa **điểm** làm mất
`−7,1296 pp` (8 xe) và `−4,9074 pp` (4 xe), trong khi margin đã prereg là `−1,00 pp`.
WP13/WP14-001 chỉ ra toàn bộ mất mát nằm ở **hard gate**, không ở ranking, và tập
trung vào đúng hai cấu hình: budget `drop_eta_total_ms` 30 s (83% prune) và
final-confirmation lock trên `pickup_eta_ms` (17% prune).

Câu hỏi WP15 vì thế **không** phải "nới budget bao nhiêu thì đủ" — phân phối lưỡng cực
đã loại khả năng đó (76,8% request tiêu thụ bằng 0; đi từ 30 s lên 60 s chỉ gỡ 1,6 pp).
Câu hỏi đúng là:

> Có thể **đặt lời hứa khác đi** để không phải sửa nó, thay vì tiếp tục ràng buộc việc
> sửa một lời hứa đặt quá chặt?

## 2. Ba trục ứng viên và điều kiện phủ định của từng trục

Mỗi trục phải khai báo trước điều kiện làm nó **vô hiệu**, đúng khuôn ADR-066 đã dùng
để loại bốn factor bằng phép đo.

### Trục B — lời hứa là cửa sổ có mức bảo đảm dẫn xuất

Thay `pickupEta`/`dropEta` điểm bằng cửa sổ `[ℓ, u]`. Theo Hosseini, Rostami & Araghi
(2025), với route đã cho thì cửa sổ tối ưu là **quantile của phân phối thời điểm tới**,
ở mức đúng bằng tỷ số penalty:

```text
Pr(τ ≤ ℓ) = a_w / a_ℓ          Pr(τ ≤ u) = 1 − a_w / a_u
```

Nghĩa là mức bảo đảm là **tham số khai báo được**, không phải một `hardLimit` chọn tay.
Lời hứa chỉ bị coi là vi phạm khi thực tế rơi ra ngoài cửa sổ; mọi biến động **bên
trong** cửa sổ không tiêu budget và không sinh witness.

- **Đo được gì:** số revision tiêu budget phải giảm mạnh ở cùng mức bảo đảm, vì phần
  lớn biến động bị hấp thụ trong độ rộng thay vì bị tính là sửa lời hứa.
- **Điều kiện phủ định B1:** nếu để đạt cùng service như B1 baseline mà độ rộng cửa sổ
  cần thiết vượt quá `pickupWindowMs` đã có (600.000 ms) hoặc vượt p90 của biến động đã
  đo (154.821 ms) tới mức lời hứa mất nghĩa vận hành, thì trục B là **vô hiệu**: nó chỉ
  đổi tên việc "không hứa gì" thành "hứa một cửa sổ rất rộng". Chính paper đã chứng minh
  giới hạn này: `a_w → 0` cho nghiệm `[0, +∞]`.
- **Điều kiện phủ định B2:** nếu phân phối thời điểm tới **không** ước lượng được tất
  định từ state hiện có, trục B không certify được và phải dừng. RideBound bắt buộc mọi
  quyết định kèm certificate máy kiểm; một cửa sổ dựa trên phân phối đoán được không
  thoả điều đó.
- **Ràng buộc thiết kế:** cửa sổ phải là một **promise field mới**, không được nới nghĩa
  của `pickup_eta_ms`/`drop_eta_total_ms` đang có. Đây là bài học ADR-067/WP14-003: mọi
  thay đổi làm đổi evidence đều phải là field/profile mới, mặc định tắt.

### Trục C — freeze horizon phụ thuộc trạng thái

F1 của WP14 đã cài `freezeHorizonMs` + `freezeHorizonLocks` nhưng là **hằng số**
(300 s / 600 s). Milosevic et al. (2026) cho một framing: vi phạm không tiêu budget mà
**làm ngắn horizon**. Bản deterministic tương ứng cho RideBound: mức khoá là hàm của
lifecycle của request (đã accept / gần đón / đã đón / gần trả), không phải một ngưỡng
phẳng theo thời gian tuyệt đối.

- **Đo được gì:** trục C nhắm đúng 17% prune do pickup lock. Nếu lock chỉ siết lại khi
  request thực sự gần được đón, phần prune xa thời điểm đón phải biến mất.
- **Điều kiện phủ định C1:** nếu phân bố thời điểm xảy ra prune do lock **không** tập
  trung gần thời điểm đón — tức lock chặn đều trên toàn lifecycle — thì horizon phụ
  thuộc lifecycle không gỡ được gì và trục C là vô hiệu. Đây là phép đo phải làm
  **trước** khi cài, trên frontier của WP14R, không phải trên H6/E1.
- **Ràng buộc thiết kế:** phải giữ deterministic và certify được. Không dùng
  continuation probability, không soft constraint, không Lagrangian. Horizon là hàm bậc
  thang trên lifecycle state đã có trong ledger.

### Trục D — giảm nguồn phương sai

Lotze et al. (2023) giảm travel time fluctuation ở cùng fleet size bằng adaptive stop
pooling. Trục này không ràng buộc lời hứa mà làm giảm cái sinh ra nhu cầu sửa.

- **Trạng thái:** ứng viên xa nhất. Nó đòi đổi tập stop, tức đổi ngữ nghĩa của
  `pickup_stop_switch_count` và `drop_stop_switch_count` — hai dimension hiện có
  `hardLimit = 0` và **0 witness** trên 44.156 decision.
- **Điều kiện phủ định D1:** nếu walking/stop pooling bị loại khỏi scope sản phẩm (BeGo
  không có khái niệm điểm đón chung), trục D không được đo. Ghi lại để không ai đề xuất
  lại như ý tưởng mới.
- **Bắt buộc:** ADR riêng trước khi chạm hai dimension đó, vì đây là đổi **định nghĩa
  lời hứa**, không phải nới ràng buộc.

## 3. Điều đã được xác nhận từ bên ngoài và phải giữ nguyên

Laupichler et al. (2026), Mt-KaRRi, dùng ràng buộc feasibility neo vào **lời hứa khách
đã chấp nhận** (`T_p + t_max_wait`, `α·T_t + β`) chứ không neo vào chuyến lý tưởng, và
nói rõ lý do đổi: neo vào shortest-path làm rider có thể chặn mọi detour, "making
pooling impossible and paralyzing fleets".

Hai hệ quả bắt buộc cho WP15:

1. **Không đổi anchor sang outcome-anchored.** Trước đây đây là một hướng mở
   ("rider quan tâm cái nào hơn"). Bằng chứng 2026 cho thấy anchor lời hứa là lựa chọn
   mà một dispatcher scale lớn độc lập hội tụ về. Nếu muốn thử outcome-anchored thì phải
   là arm **thêm vào**, không phải thay thế, và phải báo cả hai.
2. **Bất kỳ policy v2 nào cũng phải bảo đảm tập khả thi không rỗng theo cấu tạo.**
   Failure mode "gate làm rỗng vehicle choice set" đã xảy ra 534 lần (Panel A) và 339
   lần (Panel B) trong RideBound, và cũng đã xảy ra với nhóm khác. Đây là invariant thiết
   kế cho v2, không phải một tuning parameter.

## 4. Ràng buộc bất di bất dịch của WP15

| # | Ràng buộc |
|---|---|
| 1 | Không mở WP15 trước khi WP14R có frontier verified và ADR authorize |
| 2 | Không dùng số đo H6/E1/WP14-v1 để chọn mức của bất kỳ trục nào |
| 3 | Mọi promise field/lock/profile mới đều mặc định **tắt**; không nới nghĩa cái cũ |
| 4 | Mọi quyết định vẫn phải phát certificate máy kiểm được; không soft/probabilistic constraint |
| 5 | Không lấy tham số số học từ paper; paper chỉ định hình cấu trúc |
| 6 | Trục nào bị điều kiện phủ định bắt được thì báo là **vô hiệu**, không ép cho đẹp |
| 7 | Kết quả âm của WP15 là kết quả hợp lệ, giống H6 |

## 5. Thứ tự phụ thuộc

```text
RB-WP14R-008 paired gate
  -> 009 matrix
  -> 010 independent verifier
  -> 011 two-axis frontier
  -> 012 audit + closure decision
  -> [ADR mở WP15]  <-- file này chỉ chuẩn bị cho bước này
  -> WP15 refinement chính thức + ticket hoá
```

Nếu `012` kết luận cái giá của hard commitment là **cấu trúc** chứ không phải lỗi tham
số, thì trục A đã cạn và WP15 phải bắt đầu từ trục B — nhưng kết luận đó cũng là một
đóng góp hoàn chỉnh cho bài báo, và WP15 không bắt buộc phải tồn tại để bài báo đứng
được.
