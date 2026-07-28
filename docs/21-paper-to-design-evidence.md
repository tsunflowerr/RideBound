# Từ paper tới quyết định thiết kế

## 1. Cách đọc bảng

Paper không được liệt kê để “trang trí”. Mỗi nguồn phải làm ít nhất một việc:

- chỉ ra phần đã cũ;
- tạo constraint/baseline;
- cung cấp simulator/data;
- gợi ý metric;
- giới hạn claim.

## 2. Bằng chứng trực tiếp

| Nguồn | Phát hiện dùng cho RideBound | Quyết định |
|---|---|---|
| [Ackermann & Rieck 2025, Multiple-plan dynamic DARP](https://doi.org/10.1007/s00291-025-00809-y) | Dynamic requests, quick accept/reject, plan pool, least-commitment/time consensus | B5; không claim insertion/consensus mới |
| [Tellez et al., Time-Consistent DARP](https://doi.org/10.1002/net.22063) | Consistency qua nhiều ngày/time classes, epsilon trade-off | Phân biệt intra-trip revision history với multi-day consistency |
| [Unreliability in ridesharing, 2020](https://www.sciencedirect.com/science/article/pii/S0968090X2030735X) | Initial information có thể khác execution, bất ổn là vấn đề thật | Đo first promise, visible revision và realized error |
| [Anticipatory walking, TR-C 2026](https://www.sciencedirect.com/science/article/pii/S0968090X26001336) | Reference/no-worse protection khi đổi drop-off | Không claim one-step no-worse mới; giữ full ledger scope |
| [Alonso-Mora et al., PNAS 2017](https://doi.org/10.1073/pnas.1611675114) | Request-trip-vehicle batch assignment | Candidate/assignment baseline nền |
| [Optimal Online Dispatch, ICRA 2021](https://www.cs.bham.ac.uk/~parkerdx/papers/icra21samod.pdf) | OSP, complete feasible schedules trong AMoD2 | AMoD2 native sanity baseline |
| [FleetPy, 2026](https://doi.org/10.1186/s12544-026-00823-3) | Modular/reproducible MoD comparisons, benchmark data | Chọn Layer 2 |
| [RidePy, JOSS 2024](https://doi.org/10.21105/joss.06241) | Modular fleet/vehicle/dispatcher/event analytics | Chọn Layer 3 mặc định |

## 3. Patent/product evidence

| Nguồn | Điều đã tồn tại | Claim bị loại |
|---|---|---|
| [US11754407B2](https://patents.google.com/patent/US11754407B2/en) | ETA update, added rider, max extra time/distance | Per-rider delay threshold |
| [US11674811B2](https://patents.google.com/patent/US11674811B2/en) | Reassignment theo threshold, notification, consent/incentive | Reassignment/consent đơn lẻ |
| Public ridepool descriptions | Route cập nhật sau khi thêm rider | Dynamic route update |

## 4. Evidence dẫn tới metric

| Khái niệm | Metric |
|---|---|
| Lịch sử khác dù final plan giống | cumulative total variation, switch count |
| Bất ổn tập trung ở một số rider | p95/p99/max, fraction `>=3` revisions |
| Traffic ngoài kiểm soát | exogenous/decision/visible decomposition |
| Efficiency trade-off | service rate, wait, detour, VHT/VMT |
| Online feasibility | latency/timeout/fallback |
| Auditability | violation/certificate/witness rate |

## 5. Evidence dẫn tới baseline

- Dynamic rolling routing → B1.
- Penalty route stability → B2.
- Near-term locks/frozen decisions → B3.
- Assignment stability → B4.
- Least-commitment/multiple plan → B5.
- Current BeGo static → B0 context only.

## 6. Corpus trong repo

Research trước đó đã tạo:

- [bego-90-paper-evidence-matrix.md](research/bego-90-paper-evidence-matrix.md);
- [bego-90-paper-review-and-8-topics.md](research/bego-90-paper-review-and-8-topics.md);
- [giai-thich-9-de-tai-bego-de-lua-chon.md](research/giai-thich-9-de-tai-bego-de-lua-chon.md);
- [audit-t3-t9-va-de-tai-thay-the-2026.md](research/audit-t3-t9-va-de-tai-thay-the-2026.md);
- [bego-public-data-targeted-paper-evidence.md](research/bego-public-data-targeted-paper-evidence.md).

Research record mô tả corpus 180 paper toàn văn sau các vòng 90 + 80 + novelty audit. Thư mục raw có thêm duplicate/rejected/replacement; không lấy số file PDF thô làm số paper hợp lệ.

RideBound không cần trích cả 180 paper như nhau. Nhóm trực tiếp ở mục 2 quyết định claim; corpus rộng dùng cho context và kiểm tra hướng thay thế.

## 7. Quy trình cập nhật evidence

Trước pilot và trước submission:

1. Tìm từ khóa chính xác:
   - ridepool promise stability;
   - post-acceptance revision;
   - cumulative ETA revision budget;
   - schedule churn passenger;
   - path-dependent commitment routing.
2. Ưu tiên publisher/conference/repo chính thức.
3. Đọc full text nguồn va chạm.
4. Ghi phần trùng, phần khác, impact lên claim.
5. Nếu claim thay đổi, cập nhật `01`, `03`, `18`, `19`.

## 8. Quy tắc citation

- Dùng DOI/publisher/repo chính thức.
- Ghi đúng version/date cho software.
- Patent/web product là evidence khác paper phản biện.
- Không nói “180 paper đều ủng hộ RideBound”.
- Không dùng abstract để khẳng định paper không có một cơ chế; với va chạm trực tiếp phải đọc full text.
