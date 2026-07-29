# RB-WP2-001 — Refine online state và rolling baseline

> Work package: `WP2 — Online state và rolling baseline`
> Trạng thái: `READY`
> Cập nhật: 2026-07-29
> Loại ticket: refinement/decision, chưa viết thuật toán

## 1. Mục đích

Chuyển outcome WP2 thành một hàng đợi ticket nhỏ, có thứ tự và kiểm tra được,
dựa trên contract Q1 đã chạy thật. Ticket này không cài state machine, reducer
hoặc rolling insertion.

## 2. Nguồn phải đọc

1. `AGENTS.md`;
2. `../00-index.md`, `../01-research-charter.md` và
   `../18-status-and-decision-log.md`;
3. WP2 trong `../16-roadmap-and-work-packages.md`;
4. `../04-problem-model-and-notation.md`;
5. `../05-portable-core-architecture.md`;
6. `../06-event-contract-and-determinism.md`;
7. `../08-algorithms-baselines-and-solver.md`;
8. `../15-testing-reproducibility-and-quality-gates.md`;
9. `../19-requirement-traceability.md`.

## 3. Trong phạm vi refinement

- khóa ownership và transition của run/request/vehicle state;
- khóa cách event reducer nhận `EventBatchPayload` mà không đưa Contracts DTO
  vào Domain;
- khóa mô hình frozen prefix/mutable suffix và no-op plan;
- quyết định O-001: có cho vehicle reassignment trong B1 v1 hay khóa lại;
- chia B1 rolling insertion, physical validator và exact-small oracle thành
  ticket độc lập có fixture/test trước code;
- nêu dependency direction giữa Domain, Application và Algorithms;
- lập ordered queue WP2 với đúng một ticket `READY`.

## 4. Ngoài phạm vi

- không thêm C# domain/algorithm code;
- không thêm ledger, budget, promise revision hoặc certificate thật của WP3;
- không thêm OR-Tools của WP4;
- không tích hợp BeGo/FleetPy;
- không gọi decision shell `notProduced` của WP1 là online behavior.

## 5. Quyết định phải giữ

- accepted request không chuyển thành rejected trong normal operation;
- executed/frozen route prefix không bị viết lại;
- event order và `decisionApplied` lifecycle giữ nguyên protocol v1;
- B1 không có commitment constraint, nhưng vẫn dùng cùng physical constraints
  và stable tie-break dành cho policy sau;
- Domain/Application không phụ thuộc Contracts, EF, ASP.NET, simulator hoặc
  solver library;
- mỗi DTO/state/transition mới phải có fixture hoặc test chứng minh nhu cầu.

## 6. Artifacts khi ticket hoàn thành

- ADR khóa các open decision WP2 cần thiết;
- execution plan WP2 có scope, non-goal, dependency, BDD và acceptance criteria
  cho từng ticket;
- traceability từ F-001–F-004 và N-001 tới file/test dự kiến;
- cập nhật `00`, `16`, `18`, `19` với một next ticket duy nhất.

## 7. Acceptance criteria

- không còn hai cách hiểu về state ownership, reducer boundary, route prefix
  hoặc reassignment policy của B1;
- queue bắt đầu bằng contract/domain state nhỏ nhất, rồi reducer, validator,
  candidate generator, exact-small oracle và demo;
- mỗi ticket có test-first evidence và không kéo WP3/WP4 vào sớm;
- Q1 replay/hash baseline vẫn xanh sau mọi thay đổi docs;
- `18` chỉ ra đúng một next ticket implementation sau refinement.

## 8. Lệnh bắt đầu

```powershell
Get-Content docs/00-index.md -Encoding utf8
Get-Content docs/01-research-charter.md -Encoding utf8
Get-Content docs/18-status-and-decision-log.md -Encoding utf8
Get-Content docs/16-roadmap-and-work-packages.md -Encoding utf8
Get-Content docs/04-problem-model-and-notation.md -Encoding utf8
Get-Content docs/08-algorithms-baselines-and-solver.md -Encoding utf8
dotnet test RideBound.slnx -c Release --no-build --no-restore
```
