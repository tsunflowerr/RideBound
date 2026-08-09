# WP5 BeGo file guide

Đường dẫn trong file này tính từ `E:\Code\BeGo`. Các controller/service BeGo cũ
chỉ được sửa để giữ auth/error/route compatibility; core COMMIT nằm ở các file sau.

## Application/RideBound

| File | Vai trò và invariant chính |
|---|---|
| `CommitIntegrationPrimitives.cs` | Validated run/operation/hash/sequence/idempotency primitives; strict UTF-8 và bounded IDs. |
| `CommitCanonicalJson.cs` | Adapter canonicalizer tương thích protocol subset; object ordinal/integer-only/no null. |
| `CommitProtocolHash.cs` | Domain-separated manifest/checkpoint/hash helpers cần để kiểm boundary, không reimplement solver. |
| `CommitOperationStateMachine.cs` | Explicit run/operation graph, terminal states, exact epoch/sequence/revision advance. |
| `CommitIntegrationPorts.cs` | Ports và immutable contracts cho T1/claim/T2/ACK/T3/Runner; giữ Application không biết EF/Npgsql. |
| `CommitBootstrapContracts.cs` | Source snapshot, profiles, field evidence, subject links, exact package. |
| `CommitBootstrapMapper.cs` | BeGo Session/venue → pseudonymous units/matrix/manifest/hello/init/bootstrap event. |
| `CommitApiContracts.cs` | Strict HTTP commands/results/store ports và semantic fingerprints. |
| `CommitApiService.cs` | Auth/member/host orchestration, replay-before-work, server-owned timer event, finalize/query. |
| `CommitDecisionProcessing.cs` | Exact Runner decision materialization, certificate/effect and independent checkpoint validation. |
| `CommitDecisionWorker.cs` | Claim→recover→Runner→T2 và claim ACK→reconstruct→T3; typed retry/divergence. |
| `CommitOutboxRelay.cs` | User-safe payload allowlist, lease processing, stable retry/published semantics và failpoints. |
| `CommitHostedCycles.cs` | Decision/outbox cycle ports; outbox batch delegated cho independent scoped processor. |
| `CommitAuditContracts.cs` | Member/operator scope, keyset cursor, rebuild/export records và telemetry-safe dimensions. |
| `CommitAuditService.cs` | Operator auth gate, timeline/rebuild/export orchestration, no raw evidence for member. |
| `CommitRolloutContracts.cs` | Disabled/Shadow/Live, bounded claim/lease/poll options và preflight result. |

## Infrastructure/Persistence/Commit

| File | Vai trò và invariant chính |
|---|---|
| `CommitPersistenceEntities.cs` | EF rows; evidence setters internal; outbox bắt buộc same-run operation. |
| `CommitPersistenceConfigurations.cs` | Table/column/check/FK/unique/partial index/concurrency metadata. |
| `CommitIntakeStoreOptions.cs` | Absolute bound cho pending queue; invalid config fail startup. |
| `CommitOutboxStoreOptions.cs` | Base/max exponential retry delay bounds. |
| `PostgresCommitApiStore.cs` | Advisory-locked create idempotency, atomic bootstrap, exact cached response, finalize và member timeline. |
| `PostgresCommitIntakeStore.cs` | Run row-lock T1, DB-time SKIP LOCKED claims, fenced/rematerialized T2/T3, recovery bundle. |
| `PostgresCommitOutboxStore.cs` | Chọn absolute Live per-run head rồi kiểm head `Applied`; DB-time attempt fence, mark/reschedule. |
| `PostgresCommitAuditStore.cs` | Repeatable-read append-log rebuild, live comparison, privacy-safe export và exact evidence validation. |

## Migrations

| File | Thay đổi |
|---|---|
| `20260805155554_AddCommitIntegrationPersistence.*` | 11 bảng, constraints/FKs/indexes, năm evidence append-only triggers, guarded Down. |
| `20260809032051_AddCommitRecoveryFencing.*` | durable hello/init recovery frames, outbox operation FK, immutable run/outbox identity. |
| `20260809054157_AddCommitAuditTimelineIndexes.*` | keyset indexes `(run,sequence,id)` và `(run,request,sequence,id)`. |
| `20260809062352_AddCommitRolloutNamespace.*` | immutable Shadow/Live namespace, active uniqueness theo namespace. |
| `20260809180000_HardenCommitPublicationBoundary.cs` | `operation_id NOT NULL`, subject-link append-only trigger, data-preserving guarded Down. |
| `OptiGoDbContextModelSnapshot.cs` | Current EF truth; outbox operation relationship required. |

Migration hardening không tự “đoán” operation cho row null cũ. Nó dừng với SQLSTATE
`55000`; operator phải audit/repair provenance trước upgrade. Đây là fail-closed,
không phải compatibility bug bị che.

## Infrastructure/Runner và rollout

| File | Vai trò và invariant chính |
|---|---|
| `RunnerProcessOptions.cs` | Absolute paths, pinned hash/core, process/line/stderr/time bounds và safe argument validation. |
| `RunnerProcessClient.cs` | Long-lived bounded process pool, per-run I/O gate, phase/context validation, timeout/tree cleanup, exact apply+checkpoint. |
| `CommitBootstrapCatalog.cs` | Named source profiles/policies, runner binding, HMAC actor/run pseudonyms và key zeroization. |
| `CommitRolloutPolicy.cs` | Mode namespace check và cached exact artifact SHA preflight. |
| `DependencyInjection.cs` | Registers ports/stores/Runner as correct singleton/scoped lifetimes; no RideBound assembly reference. |

## API

| File | Vai trò và invariant chính |
|---|---|
| `Controllers/CommitController.cs` | Strict bounded member routes, Idempotency-Key, 202 Location/query/timeline. |
| `Controllers/CommitAuditController.cs` | Separate operator-only raw/rebuild/export routes. |
| `Middleware/CommitRolloutGateMiddleware.cs` | Member COMMIT API fail closed khi Disabled/unhealthy, không chặn operator audit. |
| `Middleware/ApiExceptionMiddleware.cs` | Typed RFC problem mapping; commit exception log không chứa payload/raw exception. |
| `Services/CommitAuditAuthorization.cs` | Permission or fixed-time configured actor-hash authorization. |
| `Services/SignalRCommitOutboxPublisher.cs` | Canonical Session group + stable envelope; không log payload. |
| `Services/CommitRolloutHostedServices.cs` | Mode-exact hosted registration, cancellation/polling và concurrent per-lease scopes. |
| `Program.cs` | Auth/rate limit/SignalR/commit composition; default mode comes from validated config. |
| `appsettings.json` | Default `Disabled`, pinned paths/hashes/catalog/bounds placeholders; secrets không hard-code. |

Các sửa ở `SessionsController`, `VoteController`, `OptimizerController`,
`BenchmarksController`, `ChatController`, `SessionHub` và Google auth giữ/siết auth,
rate/error/group compatibility của BeGo hiện hữu; chúng không chứa RideBound logic.

## Realtime frontend

| File | Vai trò |
|---|---|
| `src/optigo-frontend/src/lib/ridebound-realtime.ts` | Validate wire identity/hash/safe sequence, bounded per-run monotonic and message-ID dedup gate. |
| `src/optigo-frontend/src/hooks/useSignalR.ts` | Register authenticated event callback, rejoin canonical group, reset gate theo hook lifecycle. |
| `src/optigo-frontend/src/types/room.ts` | Closed TypeScript envelope/payload types. |
| `src/optigo-frontend/tests/frontend-utils.test.mjs` | Duplicate/stale/malformed/rebound identity tests. |
| `src/optigo-frontend/package.json` | SignalR dependency, test/lint/build scripts và explicit ES module type. |

## Paired replay và independent evidence

| File | Vai trò |
|---|---|
| `OptiGo.PairedReplay/PairedReplayModels.cs` | Strict manifest/source/arm/result records và typed failure taxonomy. |
| `PairedReplayFixtureLoader.cs` | Safe leaf paths, size/hash/canonical/config allowlist, source provenance validation. |
| `PairedReplayExecutor.cs` | Stage exact configs, run B1/C1 × clean processes, normalize only approved fields, materialize/validate outputs. |
| `PairedReplayArtifact.cs` | Enumerate/bind/self-verify bundle files, sources và assemblies; reject missing/extra/tamper. |
| `OptiGo.PairedReplay/Program.cs` | CLI argument/cancellation/stable exit code boundary. |
| `OptiGo.CommitFaultHarness/Program.cs` | Separate process that reaches named boundary then `FailFast`; avoids fake in-process crash. |
| `Wp5IndependentTransitionOracle.cs` | Test-owned transition model, không gọi production graph. |
| `CommitIndependentEvidenceTests.cs` | 16,384 steps, contention, 8+4 hard crashes, 5 mutants, randomized local curves, artifact. |
| `CommitPostgreSqlIntegrationTests.cs` | Fresh real schema, rollback/trigger/FK/race/T1–T3/outbox/audit/rollout/end-to-end gates. |

### Project/support files

| File | Vai trò |
|---|---|
| `OptiGo.PairedReplay.csproj` | Executable tool, tham chiếu Application/Infrastructure và copy fixture có kiểm soát. |
| `OptiGo.CommitFaultHarness.csproj` | Executable process riêng tối thiểu cho hard-crash evidence. |
| `OptiGo.RunnerStub.csproj` + `Program.cs` | Test-only adversarial NDJSON child process: timeout, malformed/context/error/exit behavior; không phải simulator/Runner thay thế. |
| `OptiGo.Tests.csproj` | Test dependencies, copy published fixtures/tools và opt-in PostgreSQL/Runner environment. |
| `OptiGo.Api.csproj`, `OptiGo.slnx` | Kéo đúng project/executable WP5 vào build graph; không tạo project reference sang RideBound source. |

### Mọi file test WP5

| File | Boundary được kiểm |
|---|---|
| `CommitApiBoundaryTests.cs` | DTO/range/Problem Details/auth/rate surface. |
| `CommitApiServiceTests.cs` | Host/member orchestration, idempotent replay, finalize/query. |
| `CommitAuditServiceTests.cs` | Member/operator scope, rebuild/export và drift result. |
| `CommitBootstrapMapperTests.cs` | Snapshot/provenance/pseudonym/unit/matrix/manifest determinism. |
| `CommitDecisionProcessingTests.cs` | Materializer/certificate/effect/checkpoint independent validation. |
| `CommitDecisionWorkerTests.cs` | T1→Runner→T2 và ACK→reconstruct→T3 retry/divergence flow. |
| `CommitHttpIntegrationTests.cs` | Authenticated HTTP composition through real DI/API surface. |
| `CommitIdempotencyContractTests.cs` | Semantic fingerprint và exact cached response rules. |
| `CommitIntegrationArchitectureTests.cs` | Dependency/repository/forbidden-reference boundaries. |
| `CommitIntegrationPortContractTests.cs` | Immutable port contracts, bounds và strict validation. |
| `CommitOperationStateMachineTests.cs` | Exhaustive permitted/rejected run/operation transitions. |
| `CommitOutboxRelayTests.cs` | Payload allowlist, lease/fence, duplicate-send và retry semantics. |
| `CommitPersistenceModelTests.cs` | EF keys/FKs/checks/required fields/index/trigger model. |
| `CommitPrivacyLoggingTests.cs` | Log/Problem Details không lộ raw actor/payload/evidence. |
| `CommitRolloutTests.cs` | Disabled/Shadow/Live DI, preflight, namespace, restart và independent-scope relay. |
| `PairedReplayTests.cs` | Same-input/work-rule/repeat/certificate/checkpoint/bundle tamper gates. |
| `RunnerProcessClientTests.cs` | Pinned artifact, protocol phases, timeouts, process-tree/session cleanup. |
| `CommitIndependentEvidenceTests.cs` | Independent oracle, contention, process crash, five mutants và local curves. |
| `CommitPostgreSqlIntegrationTests.cs` | Real PostgreSQL authority cho migrations/constraints/concurrency/T1–T3/outbox/audit/API. |
| `Wp5IndependentTransitionOracle.cs` | Test-owned expected state graph, intentionally independent from production state machine. |
| `Wp5EvidenceArtifact.cs` | Build/self-verify manifest, exact file/hash/size allowlist và tamper rejection. |
| `Wp5EvidenceModels.cs` | Typed raw observation/failure/exclusion/performance evidence rows. |
| `Wp5PostgreSqlCollection.cs` | Serialize shared opt-in PostgreSQL integration fixtures; không giả DB semantics. |

Unit/fake tests giúp định vị lỗi; chỉ `CommitPostgreSqlIntegrationTests.cs` là
authority cho PostgreSQL constraint, locking, database-time và concurrency claim.
