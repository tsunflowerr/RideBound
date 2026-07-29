# RB-WP3-001 — Refinement ledger, budget và certificate

> Topic ID: `RB-WP3`
> Ticket: `RB-WP3-001`
> Trạng thái: `READY`
> Cập nhật: 2026-07-30
> Dependency: `RB-WP2-012 DONE`
> Kết quả kế tiếp dự kiến: execution plan WP3, chưa phải ledger code

## 1. Mục đích

Chuyển deliverable WP3 trong roadmap thành một execution plan có thể review và
thực hiện tuần tự, dựa trên state, route, schedule, B1 và hash/ACK lifecycle thật
của WP2. Ticket này khóa semantics trước khi thêm promise ledger hoặc phát
certificate `produced`.

## 2. Input bắt buộc

- mô hình promise, total variation, switch budget và P1–P5 trong `04`;
- ownership/dependency của portable core trong `05`;
- protocol/hash/ACK lifecycle trong `06`;
- ledger, phase lock, incident, certificate và checkpoint trong `07`;
- candidate/policy boundary trong `08`;
- Q2 property/mutation/replay gate trong `15`;
- WP2 closure evidence và ADR-018–020 trong `18`;
- traceability R-007–R-009, F-005–F-009 và N-003/N-004 trong `19`;
- implementation thật của `OnlineState`, `EventReductionCoordinator`,
  `PhysicalPlanValidator`, `RollingCostPolicy` và online Runner.

## 3. Trong phạm vi

- audit gap giữa WP2 state/B1 và WP3 deliverable;
- khóa owner của promise, ledger, projection, validator, certificate và
  checkpoint theo Clean Architecture;
- định nghĩa versioned data model và transaction/ACK boundary;
- khóa exact delta semantics cho `exogenous`, `decision-induced` và `visible`;
- khóa vector budget, zero/infinite semantics, total variation và switch count;
- khóa initial promise, phase locks và incident breach separation;
- khóa independent commitment validator và machine-readable witness;
- khóa certificate/hash/checkpoint compatibility;
- tạo ADR và ordered ticket queue WP3 với scope, BDD, acceptance criteria,
  verification và rollback cho từng ticket;
- chỉ định đúng một ticket implementation WP3 tiếp theo.

## 4. Ngoài phạm vi

- viết promise/ledger/certificate/checkpoint runtime code;
- chọn mức loose/medium/tight cuối cùng của O-002 hoặc material threshold O-003;
- mở incumbent reassignment;
- cài C1, B2–B5, OR-Tools hoặc lexicographic solver của WP4;
- persistence database, BeGo/FleetPy adapter hoặc experiment;
- gọi P2 là guarantee trước khi independent validator và mutation/property tests
  tương ứng pass.

## 5. Các quyết định refinement phải khóa

1. **Ownership:** Domain giữ value object/invariant nào; Application giữ
   projection/transaction use case nào; Contracts chỉ chứa DTO/schema; Runner
   chỉ compose/map.
2. **Promise publication:** initial accepted promise được ghi lúc nào, version
   bắt đầu từ đâu, initial promise không tiêu revision ra sao.
3. **Projection:** cách diễn lại old plan dưới travel snapshot mới và cách tính
   ba delta mà không giả định `visible = exogenous + decision`.
4. **Budget vector:** integer unit, overflow, zero/infinite, phase applicability,
   material/raw counter và stable dimension vocabulary. Mức số cuối vẫn để WP8.
5. **Locks:** executed prefix, decision point, onboard, freeze horizon và final
   confirmation kết hợp cumulative budget theo thứ tự nào.
6. **Incident:** event mở/đóng, breach record, normal-operation separation và
   quy tắc không reset lịch sử.
7. **Atomicity/idempotency:** route, published promise, ledger append và decision
   hash cùng commit sau matching `decisionApplied`; retry cùng hash không append
   lần hai.
8. **Independent validation:** validator tự dựng promise/delta/ledger balance,
   không tin số liệu do candidate/solver gửi.
9. **Certificate:** version, witness, input/decision hash binding, trạng thái
   `produced/notProduced` và compatibility với schema v1 hiện hành.
10. **Checkpoint:** canonical content, hash, restore equivalence, version conflict
    và giới hạn repository/persistence.

## 6. Rules

- Domain/Application tiếp tục không phụ thuộc Contracts, EF Core, ASP.NET,
  simulator, map provider hoặc OR-Tools.
- Candidate chưa publish không tiêu budget và không append ledger.
- Ledger append-only; counter cumulative không giảm; đổi rồi quay lại vẫn tiêu
  switch/total variation.
- Initial accepted promise tạo baseline nhưng không tính là revision.
- Commitment reject phải có ít nhất một exact dimension witness.
- Incident breach không được ghi thành budget satisfied và không hoàn lại lịch sử.
- Numerical budget profile cho thí nghiệm chưa được bịa ở WP3; tests dùng named
  boundary fixtures, không tự nhận là user preference thật.
- WP3 dùng cùng versioned Runner/ACK path; không tạo implementation riêng trong
  adapter hay script.

## 7. BDD

```gherkin
Scenario: Refinement giữ publish atomicity
  Given WP2 chỉ commit route/state sau matching decisionApplied
  When thiết kế ledger transaction của WP3
  Then route, promise và ledger không có trạng thái commit lệch nhau
  And exact retry cùng decision hash không append lần hai

Scenario: Traffic shock tách khỏi quyết định
  Given old plan và promise cũ dưới travel snapshot mới
  When thiết kế promise projection
  Then exogenous, decision-induced và customer-visible delta có định nghĩa riêng
  And validator có thể tính lại từ full before-state

Scenario: Candidate vượt một dimension
  Given mọi physical invariant pass
  And pickup ETA total variation vượt hard limit
  When independent commitment validator kiểm proposed decision
  Then candidate bị loại bằng witness pickup_eta_total_ms
  And ledger chưa mutate

Scenario: Incident buộc breach
  Given route cũ không còn physical-feasible do incident rõ danh tính
  When safe fallback thay đổi promise vượt budget
  Then breach được ghi riêng với affected riders và source event
  And certificate không đánh dấu normal-operation budget satisfied
```

## 8. Artifact đầu ra

- một ADR mới trong `18` khóa semantics/ownership WP3;
- một execution plan mới `docs/tasks/28-...md` chứa ordered queue WP3;
- cập nhật `00`, `16`, `18`, `19`, `23` và tài liệu chuyên môn bị ảnh hưởng;
- đúng một ticket implementation nhỏ nhất chuyển `READY`;
- gap/risk matrix nối mỗi deliverable WP3 với test và rollback.

Không artifact nào của ticket refinement được khai là implementation.

## 9. Acceptance criteria

- mọi quyết định ở mục 5 có một đáp án hoặc một open decision có owner/gate rõ;
- execution plan bao phủ promise, projection, budget, locks, incident,
  validator, certificate, checkpoint và Runner integration;
- mỗi ticket có purpose, in/out scope, dependency, rules, BDD, acceptance,
  verification, traceability và rollback khi có state migration;
- P1/P2/P3 có exact property/mutation evidence dự kiến, không overclaim;
- numeric pilot policy O-002/O-003 vẫn mở đúng WP8;
- architecture/package audit không kéo framework/solver/adapter vào
  Domain/Application;
- `18` chỉ có một next ticket WP3;
- link/Markdown/`git diff --check` sạch;
- nếu chỉ sửa tài liệu, không nâng trạng thái runtime hoặc certificate.

## 10. Verification

```powershell
dotnet test RideBound.slnx
rg -n "EF Core|Microsoft.AspNetCore|OR-Tools|FleetPy|BeGo" `
  src/RideBound.Domain src/RideBound.Application
git diff --check
```

Kiểm thủ công thêm:

- mọi link nội bộ trong tài liệu mới resolve;
- không có hai ticket `READY`;
- không có claim budget/certificate đã implemented;
- execution plan giữ đúng dependency WP2 → WP3 → WP4.

## 11. Rollback

Vì ticket chỉ refinement, rollback bằng cách revert ADR/execution plan/status
trong cùng thay đổi. Không tạo schema/runtime migration và không sửa golden
output WP2 để hợp thức hóa thiết kế WP3.

## 12. Traceability

R-007, R-008, R-009, R-015; F-005–F-009; N-001, N-003, N-004, N-007.
