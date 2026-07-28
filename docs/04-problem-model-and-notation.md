# Mô hình bài toán và ký hiệu

## 1. Đơn vị quyết định

Hệ thống hoạt động tại các **decision epoch** `e = 0, 1, ..., E`. Epoch được kích hoạt bởi:

- request mới;
- xe đến/rời stop;
- traffic/travel-time update;
- hành khách hủy trước khi được accept;
- incident;
- timer reoptimization.

Mọi event có thứ tự toàn phần bằng `(simTimeMs, eventSeq)`. Wall-clock không được dùng để thay đổi kết quả replay.

## 2. Tập và trạng thái

- `V`: tập xe.
- `R_e`: request đã biết ở epoch `e`.
- `A_e`: request đã được accept và chưa hoàn tất.
- `N_e`: request mới/pending có thể quyết định.
- `π_v^e`: route của xe `v`, gồm frozen prefix và mutable suffix.
- `x_e`: trạng thái ngoại sinh tại epoch `e`, ví dụ travel-time matrix.
- `L_i^e`: commitment ledger của rider `i`.

Trạng thái tối thiểu của xe:

`vehicle id, node/edge position, sim time, capacity, onboard riders, accepted riders, executed prefix, planned suffix`

Trạng thái tối thiểu của request:

`origin, destination, arrival time, pickup window, maximum ride time, party size, lifecycle status`

## 3. Lời hứa cho một hành khách

Sau khi request `i` được accept, lời hứa ở epoch `e` là:

\[
c_i^e =
(v_i^e,\ s_{i,p}^e,\ s_{i,d}^e,\ \hat t_{i,p}^e,\ \hat t_{i,d}^e,\ q_{i,p}^e,\ q_{i,d}^e)
\]

Trong đó:

- `v`: xe được gán;
- `s_p`, `s_d`: điểm đón/trả;
- `t̂_p`, `t̂_d`: ETA đón/trả đã tính;
- `q_p`, `q_d`: thông tin thứ tự phục vụ.

Không nhất thiết mọi deployment hiển thị tất cả trường, nhưng benchmark phải lưu đủ để kiểm tra.

## 4. Revision delta nhiều chiều

Giữa hai promise liên tiếp:

\[
\Delta_i^e =
(\Delta^{pickETA},\Delta^{dropETA},\Delta^{vehicle},
\Delta^{pickupStop},\Delta^{dropStop},\Delta^{order})
\]

Gợi ý định nghĩa chuẩn:

- `ΔpickETA = |t̂_p^e - t̂_p^{e-1}|`;
- `ΔdropETA = |t̂_d^e - t̂_d^{e-1}|`;
- `Δvehicle = 1` nếu vehicle đổi;
- `ΔpickupStop`: khoảng cách giữa hai pickup stop, cộng một switch count;
- `ΔdropStop`: tương tự;
- `Δorder`: số đảo cặp giữa các request incumbent còn active.

### Vì sao không dùng rank tuyệt đối duy nhất?

Khi thêm một rider mới trước rider cũ, rank tuyệt đối đổi dù thứ tự tương đối giữa các rider cũ không đổi. Vì vậy report hai metric:

- incumbent pairwise inversions;
- số stop mới được chèn trước pickup/drop của rider.

ETA drift vẫn phản ánh tác động thời gian.

## 5. Total variation và switch budget

Với mỗi dimension liên tục `k`:

\[
TV_{i,k}^{e} = \sum_{\tau=e_i^{accept}+1}^{e}\Delta_{i,k}^{\tau}
\]

Với dimension rời rạc:

\[
SW_{i,k}^{e} =
\sum_{\tau=e_i^{accept}+1}^{e}
\mathbb{1}[c_{i,k}^{\tau}\neq c_{i,k}^{\tau-1}]
\]

Ràng buộc:

\[
TV_{i,k}^{e} \le B_{i,k},
\qquad
SW_{i,k}^{e} \le K_{i,k}
\]

Dùng **vector budget**, không nén tất cả thành một số duy nhất. Một weighted score có thể dùng để xếp hạng candidate nhưng không được che việc một dimension đã vượt hard limit.

## 6. Revision “đáng kể” và chống gaming

UI có thể chỉ hiển thị ETA theo phút. Benchmark lưu hai lớp:

- raw ETA total variation;
- displayed/material revision count theo rule công khai.

Ví dụ, một revision material khi bucket ETA hiển thị đổi hoặc raw shift vượt threshold đã đăng ký trước.

Không chỉ đếm thresholded revision vì thuật toán có thể tạo nhiều thay đổi nhỏ ngay dưới threshold. Raw total variation luôn phải được báo.

## 7. Phân rã traffic và quyết định

Khi travel time đổi, ETA có thể đổi dù giữ nguyên route. Tại epoch `e`, tính:

1. `c_i^{e-1}`: promise cũ.
2. `c_{i,exo}^{e}`: chiếu **plan cũ** lên travel-time state mới.
3. `c_i^e`: promise sau quyết định mới.

Từ đó:

\[
\Delta^{exo}=d(c_{i,exo}^{e},c_i^{e-1})
\]

\[
\Delta^{decision}=d(c_i^e,c_{i,exo}^{e})
\]

\[
\Delta^{visible}=d(c_i^e,c_i^{e-1})
\]

Do dùng trị tuyệt đối, không khẳng định `visible = exo + decision`. Ba đại lượng được report riêng.

Primary endpoint của thuật toán dùng decision-induced revision. Customer-visible revision là secondary endpoint bắt buộc.

## 8. Hard feasibility

Mọi plan bình thường phải thỏa:

- capacity tại mọi leg;
- pickup time window;
- maximum ride time;
- pickup trước drop-off;
- onboard rider không bị bỏ route;
- frozen/executed prefix không đổi;
- next-stop lock khi xe đã vượt decision point;
- accepted request không bị chuyển thành rejected;
- event/epoch version tăng đơn điệu;
- route nối được theo travel-time oracle hiện hành.

Incident policy có thể ưu tiên an toàn và phá commitment budget, nhưng phải tạo breach record riêng; không được đánh dấu là “kept”.

## 9. Đầu vào chuẩn của một epoch

```text
Scenario manifest
+ Current simulation clock
+ Ordered events
+ Vehicle snapshots
+ Active requests
+ Previous plans
+ Promise ledgers
+ Commitment policy/budgets
+ Travel-time snapshot
+ Solver budget and deterministic seed
```

Không cho core đọc database, gọi Mapbox hoặc lấy `DateTime.UtcNow`.

## 10. Đầu ra chuẩn

```text
Accept/reject/defer decisions
+ Vehicle assignments
+ New mutable route suffixes
+ Promise revisions
+ Ledger deltas and remaining budgets
+ Feasibility/commitment certificate
+ Reject/prune witnesses
+ Runtime and solver diagnostics
+ Deterministic decision hash
```

## 11. Hàm mục tiêu

V1 dùng thứ tự từ điển:

1. không vi phạm physical/hard service constraints;
2. không đảo accepted → rejected;
3. tối đa số request được accept/serve;
4. tối thiểu worst normalized commitment utilization;
5. tối thiểu decision-induced revision burden;
6. tối thiểu operational cost, wait và detour;
7. tie-break theo stable identifier.

Có thể cài bằng nhiều solve liên tiếp hoặc epsilon constraints. Không dùng một bộ trọng số tùy ý làm cấu hình duy nhất.

## 12. Thuộc tính lý thuyết cần chứng minh hoặc test

### P1 — Ledger conservation

Ledger mới bằng ledger cũ cộng đúng delta của promise được phát.

### P2 — Hard-budget guarantee

Nếu validator chấp nhận decision bình thường, mọi budget và hard constraint đều được thỏa.

### P3 — Monotonic feasible set

Nới budget không làm mất candidate từng khả thi với budget chặt hơn, khi các đầu vào khác giữ nguyên.

### P4 — Baseline equivalence

Với budget vô hạn, switch cap vô hạn, hard lock tắt và cùng tie-break, policy RideBound-degenerate trả quyết định như rolling baseline.

### P5 — Replay determinism

Cùng manifest, event stream, version, seed và binary hash tạo cùng decision hash.

P1–P5 là mục tiêu formal/property testing. Chỉ P2 được gọi là “guarantee” sau khi validator và test tương ứng hoàn thành.
