# RB-WP5-001 — refinement BeGo adapter và persistence

> Trạng thái: `READY`
> Work package: `WP5`
> Loại ticket: refinement-only
> Dependency: `RB-WP4-014 DONE`, ADR-024
> Implementation được phép trước khi ticket này DONE: `NONE`

## 1. Outcome

Biến mô tả tích hợp trong `docs/14-bego-integration-api-persistence-ux.md`
thành một decision-complete ordered queue. Refinement phải khóa ownership giữa
BeGo và RideBound Runner, transaction/outbox/idempotency, persistence schema,
bootstrap provenance, feature flag/rollback và paired Layer-1 replay trước khi
thêm migration, endpoint hoặc adapter production.

## 2. Non-goals

- không copy source tree BeGo/OptiGo vào RideBound;
- không cho BeGo tái hiện solver, commitment validator hoặc certificate;
- không đổi protocol/hash/ACK/checkpoint đã đóng ở WP1–WP4;
- không migration database, endpoint, SignalR hoặc UI trong refinement;
- không chọn numeric budgets/effect margins từ paper hay microbenchmark WP4;
- không gọi paired replay là effectiveness evidence trước preregistration.

## 3. Câu hỏi bắt buộc phải khóa

1. BeGo source checkout/version/baseline và phạm vi thay đổi chính xác là gì?
2. Adapter map Session/member/vehicle/request/time/node ID sang manifest/event nào,
   với provenance và failure mode nào cho field thiếu?
3. Process ownership: BeGo gọi versioned NDJSON Runner như child process/service
   ra sao mà không reimplement core?
4. Idempotency key, event sequence, pending decision, matching ACK và retry được
   persist/transaction thế nào?
5. Schema append-only nào lưu event/decision/promise/ledger/certificate/checkpoint;
   projection nào rebuild được và index nào cần đo?
6. Decision + publication + certificate + ACK/outbox có transaction boundary và
   crash-recovery state machine nào?
7. Feature flag off, migration rollback, dual-run/shadow mode và existing endpoint
   compatibility được chứng minh ra sao?
8. Secrets/PII/location precision/retention và research pseudonymous IDs nằm ở
   boundary nào?
9. Paired B1/C1 replay dùng cùng input/manifest/config, failure/exclusion log và
   artifact hashes nào?
10. O-007 có cần HTTP/gRPC hay NDJSON process boundary đã đủ cho WP5?

## 4. Required artifacts

- current BeGo baseline audit với exact commands/counts và repository provenance;
- context/container/sequence diagrams cho bootstrap, online event, ACK và recovery;
- field-level adapter mapping + units/rounding/missing-data matrix;
- persistence/transaction/outbox decision table và migration/rollback plan;
- API/auth/idempotency/observability/privacy contract delta;
- paired Layer-1 replay protocol, oracle và non-claim boundary;
- ADR WP5 refinement;
- ordered implementation queue có đúng một ticket kế tiếp `READY`.

## 5. Acceptance

- Mọi write path giữ Runner là nguồn quyết định duy nhất và cùng versioned binary.
- BeGo không được tự append ledger/certificate hay tự đoán ACK success.
- Crash ở trước/sau Runner response và trước/sau DB commit có recovery/idempotency
  rõ, không double-apply event hoặc publication.
- Bootstrap data thiếu fail hoặc dùng explicit stored default với provenance;
  không giả làm user-provided value.
- Feature flag off giữ behavior BeGo hiện hành và migration có rollback/disable.
- Existing BeGo baseline và RideBound `dotnet test RideBound.slnx` đều được ghi và
  giữ pass, không trộn số test hai repository.
- Paired replay artifact bind source/config/binary hashes và chỉ được gọi là
  Layer-1 mechanical/effect signal theo claim boundary.
- `docs/18`, `docs/19`, ADR và ordered queue đồng bộ.

## 6. Rollback

Nếu ownership, transaction recovery, baseline hoặc source provenance chưa khóa,
giữ WP5 ở refinement, không tạo migration/endpoint. Runtime rollback mặc định là
feature flag off và BeGo cũ tiếp tục hoạt động; không xóa append-only RideBound
evidence để “dọn” một thử nghiệm thất bại.

## 7. Handoff

Khi ticket này DONE, tạo queue implementation WP5 theo template trong `tasks/23`.
Chỉ ticket nhỏ nhất có đủ dependency/evidence được chuyển `READY`; WP6/WP7 có thể
refine độc lập nhưng không được dùng để bỏ qua Layer-1 transaction gate.
