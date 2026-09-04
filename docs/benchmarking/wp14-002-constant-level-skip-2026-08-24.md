# RB-WP14-002 — Bỏ qua lexicographic level hằng số

> Ngày: 2026-08-24
> Trạng thái: `Done`
> Claim class: mechanical optimization; không đổi kết quả khoa học nào

## 1. Vấn đề

Solver giải một model CP-SAT cho **mỗi** lexicographic level, cố định optimum của
level trước làm ràng buộc cho level sau. Đo trên evidence đã ghi của H6/E1, phần
lớn các pass đó không quyết định gì:

| Nhóm | level dựng | level hằng số | tỷ lệ |
|---|---:|---:|---:|
| Panel A B1 | 141.330 | 133.409 | 94,40% |
| Panel A C1 | 273.884 | 266.765 | 97,40% |
| Panel B B1 | 53.016 | 49.735 | 93,81% |
| Panel B C1 | 136.860 | 133.822 | 97,78% |

C1 giải trung bình 20,93 model mỗi decision; chỉ khoảng 0,54 trong số đó thực sự
phân biệt được hai lựa chọn.

## 2. Mệnh đề

Một assignment khả thi chọn đúng một option mỗi vehicle. Nếu ở level `i` mọi option
của **mọi** vehicle đóng góp cùng một giá trị `c_v`, thì:

- `sum`: mọi assignment khả thi có giá trị `Σ_v c_v`;
- `maximum`: mọi assignment khả thi có giá trị `max_v c_v` (các đóng góp là số
  nguyên canonical không âm nên bằng đúng giá trị model tính).

Cả hai đều không phụ thuộc lựa chọn. Ràng buộc request-uniqueness chỉ **bớt**
assignment nên không phá đẳng thức. Do đó level `i` là hằng số trên toàn tập khả
thi, pass của nó chỉ chứng minh optimum của một hằng số, và ràng buộc
`objective_i == optimum_i` mà nó sinh cho các level sau thoả với **mọi** assignment
khả thi — tức vô hiệu. Bỏ pass và ghi thẳng hằng số là biến đổi bảo toàn quyết định.

Nếu tổng vượt canonical range thì level được báo là **không** hằng số, giữ nguyên
đường xử lý overflow cũ.

### Tại sao assignment trả về không đổi

Bỏ một ràng buộc vô hiệu không đổi tập khả thi, nhưng về nguyên tắc có thể đổi
assignment nào được trả về nếu optimum không duy nhất. Production mapping thêm một
level `candidate-id-rank:<vehicle>` cho mỗi vehicle, và level đó hằng số **đúng
khi** vehicle chỉ có một option. Vậy mọi vehicle hoặc bị ép (một option) hoặc bị
ghim bởi một rank level không hằng số. Optimum là duy nhất, nên assignment không
đổi. Tài liệu API ghi rõ điều kiện này cho caller tự dựng hierarchy khác.

## 3. Vì sao phải opt-in

`executionEvidence` nằm **bên trong** hash projection của decision
(`DecisionPayloadCodec.Encode(shell, hashProjection: true)` ghi nó trước khi bỏ
`decisionHash`/`previousDecisionHash`). Evidence chứa
`consumedDeterministicTimeMicros`, và con số đó **giảm thật** khi bớt pass.

Nên tối ưu này không thể byte-identical ở mức transcript. Nó bảo toàn:

- danh sách action và candidate được chọn;
- `stateAfterHash`;
- optimum của mọi level (`incumbentValue`, `bestBound`, `gap`, `isProvenOptimal`).

Nó **đổi**: `consumedDeterministicTimeMicros`, `detailCode`, và do đó `decisionHash`.

Ghi số giờ solver không thực sự tiêu là bịa số đo, nên không làm. Thay vào đó cờ
`solverExecution.skipConstantObjectiveLevels` mặc định **tắt**: mọi configuration
đã publish parse y nguyên, mọi run đã ghi giữ nguyên hash, và freeze chain H6/E1
không bị chạm. WP14 development runs bật cờ.

Khi bật, `detailCode` là `ORTOOLS_OPTIMAL_CONSTANT_LEVELS_SKIPPED` và `detail` ghi
rõ bao nhiêu level trên tổng bao nhiêu đã được báo mà không giải — evidence không
bao giờ ngụ ý một pass đã chạy trong khi không chạy.

## 4. Verification

| Kiểm tra | Kết quả |
|---|---|
| Decision invariance trên production C1 mapping, 64 seed | pass; selected IDs, objective values và mọi bound bằng nhau |
| Unit `ConstantObjectiveLevelValues` (sum, maximum, bất đồng trong vehicle, overflow, single-option) | 5/5 |
| Adapter: skip cho cùng assignment/optima, ghi ít work hơn, mọi level vẫn có bound | 6/6 |
| Mutation: level phân biệt được **không** bị coi là hằng số | pass |
| Edge: hierarchy toàn hằng số vẫn tạo được incumbent | pass |
| Config: cờ vắng mặt ⇒ tắt; `true` ⇒ bật và đổi content hash; sai kiểu ⇒ reject | pass |
| Required `dotnet test RideBound.slnx` | 873/873, zero skip |
| Full pinned CPython/FleetPy | 212/212, zero skip |
| `dotnet format --verify-no-changes`, `git diff --check` | pass |

Toàn bộ 860 test cũ pass **không sửa một dòng nào** — bằng chứng trực tiếp rằng
mặc định tắt là tương thích ngược.

## 5. Điều cố ý không claim

Không claim wall-clock. Theo ADR-052 repo đã từ chối claim tốc độ khi timing chưa
đo trong điều kiện kiểm soát. Cái được claim là **số pass giảm**, đại lượng tất
định và đếm được. Envelope tài nguyên thực tế sẽ được đo ở `RB-WP14-009` trên
development panel, nơi bắt buộc phải khai báo envelope trước matrix.

## 6. Hệ quả

`RB-WP14-002 Done`, `RB-WP14-003 Ready`. H6/WP10/WP13 outcomes, margin, panels và
mọi frozen receipt không đổi vì cờ mặc định tắt.
