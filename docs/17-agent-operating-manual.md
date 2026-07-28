# Sổ tay vận hành cho agent và người tiếp tục dự án

## 1. Bắt đầu một task

Đọc theo thứ tự:

1. `AGENTS.md` ở root.
2. `docs/00-index.md`.
3. `01-research-charter.md`.
4. `18-status-and-decision-log.md`.
5. Work package hiện hành trong `16-roadmap-and-work-packages.md`.
6. Tài liệu chuyên môn liên quan.
7. `AGENTS.md` lồng trong thư mục sẽ sửa, nếu có.

Sau đó:

- kiểm tra `git status` bằng safe-directory option nếu máy báo dubious ownership;
- không xóa/ghi đè untracked files của người dùng;
- kiểm tra code thực tế thay vì tin docs tuyệt đối;
- ghi task vào current queue/status trước hoặc cùng PR.

## 2. Nguồn sự thật

| Câu hỏi | Tệp |
|---|---|
| Đang làm gì/tiếp theo? | `18-status-and-decision-log.md` |
| Claim nào được phép? | `01`, `03` |
| Công thức chuẩn? | `04`, `07` |
| Protocol? | `06` |
| Baseline/policy? | `08` |
| Experiment? | `09`–`11` |
| Roadmap/gate? | `16` |
| Requirement coverage? | `19` |

Nếu code khác docs:

1. xác định code là bug hay quyết định mới;
2. không âm thầm sửa một bên;
3. ghi decision/ADR;
4. cập nhật docs, test và status cùng task.

## 3. Quy tắc thay đổi contract

Một thay đổi protocol/model được coi là breaking nếu:

- đổi semantics field;
- đổi unit/rounding;
- đổi lifecycle;
- bỏ dimension promise/budget;
- đổi event ordering;
- đổi hash input;
- đổi definition primary metric.

Breaking change cần:

- ADR;
- schema major bump;
- migration/golden fixtures;
- adapter updates;
- replay compatibility plan;
- traceability update.

Không đổi protocol chỉ để làm adapter dễ hơn nếu nó phá nghĩa nghiên cứu.

## 4. Quy tắc code portable

- Domain/Application không tham chiếu OptiGo/FleetPy/database/network.
- Adapter không chứa thuật toán RideBound.
- Solver qua port.
- Time/random/travel đều inject.
- Không dùng `DateTime.UtcNow` trong deterministic core.
- Không dùng double làm key/order.
- Stable sort/tie-break bắt buộc.
- Same runner binary cho cross-system evidence.

Architecture violation phải fail test hoặc review gate.

## 5. Quy tắc benchmark

- B1/C1 cùng input/candidate/compute.
- Không tune trên confirmatory set.
- Không bỏ seed xấu.
- Không gọi synthetic profile là user truth.
- Không dùng static BeGo làm online baseline duy nhất.
- Không dùng Li & Lim v1 hiện tại làm hard-TW evidence.
- Không báo `FEASIBLE` là `OPTIMAL`.
- Raw transcript phải tồn tại trước aggregate.

## 6. Quy tắc claim

Trước khi viết “mới”, “đầu tiên”, “guarantee”, “fair”:

- đọc `03`;
- kiểm evidence mới kể từ lần audit;
- trỏ tới theorem/test/result;
- nêu phạm vi simulator/dataset;
- tránh satisfaction/demographic claim.

Một certificate implementation chưa đủ chứng minh hiệu quả; một experiment chưa đủ chứng minh guarantee.

## 7. Quy tắc làm việc với vendor/simulator

- Checkout pin trong `tmp/vendor` hoặc build cache; không commit vendor source.
- Adapter ở `simulators/...`.
- Ghi upstream URL/tag/commit/license.
- Patch upstream chỉ khi không có extension point; patch phải tối thiểu, versioned và có diff.
- Không “sửa” native baseline để làm RideBound trông tốt hơn.

## 8. Quy tắc frontend

RideBound không chứa frontend BeGo. Nếu task cần thay đổi UI, chuyển sang
repository `E:\Code\BeGo` và tuân thủ
`src/optigo-frontend/AGENTS.md`, bao gồm việc đọc guide Next.js cục bộ.

UX không dùng ngôn ngữ overclaim. Mọi revision hiển thị phải phân biệt traffic/system khi dữ liệu cho phép.

## 9. Lệnh kiểm tra hiện trạng

```powershell
dotnet test RideBound.slnx

Push-Location E:\Code\BeGo
dotnet test src\OptiGo.slnx --no-restore --verbosity minimal
Push-Location src\optigo-frontend
npm test
Pop-Location
Pop-Location

git status --short
```

Chỉ cần chạy regression BeGo khi task thay đổi adapter, contract tích hợp hoặc
baseline so sánh; ghi rõ nếu task thuần RideBound không chạy chúng.

## 10. Kết thúc một task

- Test proportionate risk.
- Cập nhật status/current next action.
- Cập nhật decision nếu có.
- Cập nhật traceability và docs bị ảnh hưởng.
- Ghi file/migration/config đã thêm.
- Nêu test đã chạy và test chưa chạy.
- Không đánh dấu work package complete nếu exit gate chưa đủ.

## 11. Format decision

```text
ADR-ID / date / status
Context
Decision
Alternatives considered
Consequences
Evidence
Supersedes / superseded by
```

Decision không bao giờ bị xóa; nếu đổi, thêm ADR mới supersede.

## 12. Cách xử lý blocker

Trước khi hỏi người dùng:

- kiểm source/docs/tests;
- thử safe alternative;
- thu hẹp đúng blocker;
- nêu lựa chọn và tác động.

Cần hỏi khi:

- đổi research claim/scope;
- chọn commercial solver/cost;
- thu thập dữ liệu người thật;
- chọn margin/preregistration cuối;
- xóa/migrate dữ liệu;
- triển khai production.

## 13. Definition of handoff tốt

Người tiếp theo đọc `00`, `18` và work package là biết:

- hệ thống hiện có gì;
- code/artifact nào đã thật sự tồn tại;
- test nào pass;
- decision nào bị khóa;
- blocker/open question;
- lệnh đầu tiên cần chạy;
- file đầu tiên cần sửa.
