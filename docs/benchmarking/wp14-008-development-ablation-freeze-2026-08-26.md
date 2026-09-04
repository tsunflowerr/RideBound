# RB-WP14-008 — Development ablation freeze

> Ngày: 2026-08-26 (UTC freeze: 2026-08-25)
> Trạng thái: `Done`
> Claim class: development exploratory only; không confirmatory, không rescue H6

## 1. Kết quả

WP14 development ablation đã được đóng băng **trước outcome** bằng receipt
[`freeze-receipt-v1.json`](../../benchmarks/scenarios/wp14-development/freeze-receipt-v1.json):

| Thuộc tính | Giá trị |
|---|---:|
| Cell | 16 |
| Arm | 10 |
| Job | 160, exact cell × arm product |
| Arrivals/job | 108 |
| Arrivals/arm | 1.728 |
| Comparison unit | paired development cell |
| Master seed | 7, không phải replicate |
| Receipt | 101.719 byte |
| SHA-256 | `1ce26ff0f7d87c30d050e57107ad3e118af7f4b88fe04e62e48376ab34c37a55` |

Receipt được ghi bằng exclusive-create và verify lại byte-canonical từ toàn bộ
nguồn. Lần ghi thứ hai bị từ chối; verify mode chỉ dựng lại expected receipt và so
exact object/bytes.

Compact evidence:
[`wp14-008-development-ablation-freeze-v1-summary.json`](evidence/wp14-008-development-ablation-freeze-v1-summary.json).

## 2. Integrity boundary

Review trước freeze phát hiện một bản manifest chỉ hash grid/config là chưa đủ:
config, analyzer, runner hoặc runtime có thể đổi sau freeze mà 160 job vẫn chạy.
Contract cuối cùng bind:

- exact 16 cells, 10 arms, 160 unique job IDs và exact cross product;
- grid, 16 driver, mọi commitment/WP4 config, ba analyzer/runner script, ba schema,
  regression tests và full-PDF research boundary — tổng 46 repository files;
- whole-tree seals cho adapter, development fixture và published Runner, cộng exact
  `RideBound.Runner.dll`;
- FleetPy 1.0.2 clean commit `053aa9d…071b0`, CPython 3.10.20 và kết quả capability
  probe (package/source/import contract) hash `59125f77…d27167`;
- .NET SDK 10.0.301, runtime 10.0.9 và **whole runtime tree** hash
  `6d5d52dd…fe43d`, không chỉ hash launcher `dotnet.exe`;
- external seven-axis leakage audit 16-vs-40 cells, zero overlap, và E1 Panel A
  resource-only planning evidence.

Bốn forbidden roots là exact H6 Panel A/B và E1 Panel A/B. Output hoặc derived
report chồng lấn theo bất kỳ chiều ancestor/descendant nào đều fail closed.

## 3. Runner và analyzer đã khóa

Matrix runner không chỉ gọi generic verifier. Sau verify, nó còn bind
`label == jobId`, `repeatCount == 1`, scenario SHA và repository-inventory SHA; vì
vậy một bundle hợp lệ bị đặt nhầm dưới job khác không được reuse. Mỗi output mới
được verify độc lập với behavioral hash và audited solver evidence trước khi ghi
success result. Summary cũng exclusive-create, schema-strict và liệt kê exact job
IDs đã chọn.

Analyzer đọc từng transcript tới EOF và verify lại đủ bundle. Hai lỗi logic của
bản nháp đã được sửa trước freeze:

- p95 dùng nearest-rank `ceil(0,95n)-1`, không dùng index floor bị off-by-one;
- completion rate và median paired delta dùng integer-canonical representation,
  không phát sinh float evidence.

Nó còn tách pickup ETA **sớm hơn** khỏi **muộn hơn**, vì ratchet chỉ cấm chiều
muộn; phát hiện duplicate arm/cell; báo tail per-rider cạnh fleet total; và đánh
dấu Pareto dominance chỉ trên hai trục `completed` maximize / attributed burden
minimize. Không có scalar, ranking hoặc chọn arm hậu outcome.

## 4. Full-PDF evidence

In-app Browser xác nhận MIT CSAIL cung cấp trực tiếp bản PDF Alonso-Mora et al.
sáu trang. Bản cục bộ SHA `edbb6215…4c12ea` được kiểm tra bằng `pypdf`, render và
xem đủ 6/6 trang; text layer có ở mọi trang. Kết luận áp dụng vào analyzer là phải
tách pickup improvement/worsening và giữ frontier đa mục tiêu. Không copy budget,
horizon, batch interval hay default số học nào từ paper.

Chi tiết và inventory 77/77 trang:
[`wp14-ablation-pareto-full-pdf-evidence-2026-08-24.md`](../research/wp14-ablation-pareto-full-pdf-evidence-2026-08-24.md).

## 5. Resource envelope

Envelope chỉ dùng resource fields của 40 E1 Panel A jobs, không đọc outcome để
tuning:

| Gate | Frozen value |
|---|---:|
| Observed median job wall | 971.998 ms |
| Projected output | 15.600.000.000 byte |
| Projected wall at parallelism 4 | 39.600.000 ms (11 giờ) |
| Minimum free disk before launch | 25 GiB |
| Reserve | 5 GiB |
| Maximum output | 20 GiB |
| Maximum job wall | 2.700 s |
| Maximum matrix wall | 57.600 s |

Tại freeze, ổ E còn 135,37 GiB và output root chưa tồn tại. `RB-WP14-009` phải đo
hai job thật trước khi chạy toàn matrix; số ở đây là planning envelope, chưa phải
speedup claim cho constant-level skip.

## 6. Verification

| Gate | Kết quả |
|---|---:|
| WP14 freeze/matrix/frontier targeted + mutation | 17/17 |
| Full pinned CPython/FleetPy | 242/242, zero skip |
| Required `dotnet test RideBound.slnx` | 908/908, zero skip |
| `dotnet format RideBound.slnx --verify-no-changes` | pass |
| Non-negative JSON parse / Draft 2020-12 schemas | 1.639 / 81, pass |
| Markdown links / fences | 0 broken / 0 unbalanced |
| Python changed-file line length ≤ 88 | pass |
| `git diff --check` | pass |

Mutation coverage gồm missing config/cell, analyzer/runtime hash, noncanonical
receipt, wrong roots/parallelism, bundle/job misbinding, verifier failure/timeout,
resource ceiling, exclusive create, percentile và Pareto dominance.

## 7. Claim và queue consequence

Receipt chỉ cho phép development exploratory analysis. Nó không đổi verdict H6,
không tạo CI/population/causal claim, không authorize H7 hay lifecycle policy v2,
và không biến resource estimate thành outcome.

`RB-WP14-008 Done`; `RB-WP14-009` là queue head duy nhất. `006/007` vẫn Deferred,
không bị coi là đã falsify hay cancel.
