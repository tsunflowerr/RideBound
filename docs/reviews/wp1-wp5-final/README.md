# Final source review WP1–WP5

> Ngày audit: 2026-08-09  
> RideBound source baseline: `44ef6a7cacdc58e7c6c0576430fcd7bb02e76c7a`  
> BeGo source baseline trước WP5: `ebe0d34365ec4751bd5c629677733032490a1a0d`  
> Phạm vi: source, migration, process lifecycle, database concurrency, privacy,
> artifact và claim; không suy kết luận chỉ từ số test.

## Kết luận ngắn

**GO có điều kiện cho `RB-WP6-001` (refinement common benchmark harness).** WP1–WP5
không còn correctness blocker đã biết đối với bước thiết kế harness. Kết luận này
**không** phải là GO cho main experiment, production rollout, SLA hay tuyên bố C1
tốt hơn B1.

WP5-014 đã phát hiện và sửa bốn vấn đề mà test cũ chưa bao phủ đầy đủ:

1. `commit_subject_links` tham gia authorization nhưng chưa append-only; migration
   mới cấm `UPDATE/DELETE` ở PostgreSQL.
2. Outbox T2 có thể được claim trước ACK/T3; outbox giờ bắt buộc có
   `operation_id`, chọn absolute earliest unpublished head trước rồi chỉ publish
   nếu chính head đó có operation `Applied`; row sau không thể vượt head chưa T3.
3. Một SignalR send chậm có thể chặn cả batch khác run; batch giờ chạy đồng thời
   qua DI scope/DbContext độc lập, vẫn chỉ một head mỗi run.
4. Ba lỗi format backend cũ và một Node module warning đã được dọn mà không đổi
   semantics.

## Bằng chứng cuối

| Gate | Kết quả |
|---|---|
| RideBound bắt buộc | 557/557, 0 skip |
| BeGo Debug + PostgreSQL/Runner/fault evidence | 154/154, 0 skip |
| BeGo Release `/p:TreatWarningsAsErrors=true` | 154/154, 0 skip |
| Frontend | 9/9; ESLint, TypeScript, Next production build pass |
| Format | `dotnet format ... --verify-no-changes` pass |
| Dependency audit | NuGet cả hai repo và npm: 0 vulnerability được báo |
| WP5-013 artifact | 18/18 file rehash; manifest `e21fb087...1061ca2` |

## Cách đọc folder này

1. [01-architecture-flow-and-verdict.md](01-architecture-flow-and-verdict.md) —
   kiến trúc, luồng và phạm vi của verdict.
2. [02-wp1-wp4-logic-audit.md](02-wp1-wp4-logic-audit.md) — logic portable core.
3. [03-wp5-durable-flow.md](03-wp5-durable-flow.md) — T1/T2/T3, recovery,
   outbox, rollout và privacy.
4. [04-ridebound-file-guide.md](04-ridebound-file-guide.md) — map từng file
   production của RideBound.
5. [05-wp5-file-guide.md](05-wp5-file-guide.md) — map từng file WP5 trong BeGo.
6. [06-key-code-walkthrough.md](06-key-code-walkthrough.md) — giải thích từng
   block logic quan trọng theo execution path.
7. [07-paper-to-code-and-optimization.md](07-paper-to-code-and-optimization.md) —
   paper → mechanism → code → giới hạn claim.
8. [08-verification-and-reproduction.md](08-verification-and-reproduction.md) —
   lệnh và evidence có thể chạy lại.
9. [09-risks-debt-and-handoff.md](09-risks-debt-and-handoff.md) — debt còn lại,
   stop conditions và WP6 handoff.

Review WP1–WP4 trước đó tại `docs/reviews/wp1-wp4-final` vẫn là historical audit;
folder này thay nó làm closure source-of-truth vì đã thêm WP5 và re-audit boundary.
