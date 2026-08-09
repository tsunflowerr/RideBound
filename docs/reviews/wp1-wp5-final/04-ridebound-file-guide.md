# RideBound production file guide

File `.csproj` định nghĩa dependency; `AssemblyReference.cs` chỉ là marker cho test/
DI. Bảng dưới tập trung vai trò logic của từng file `.cs` hiện có.

## Project boundary files

| File | Dependency boundary |
|---|---|
| `RideBound.Contracts.csproj` | Wire/schema/canonicalization project độc lập; không tham chiếu Domain hay adapter. |
| `RideBound.Domain.csproj` | Aggregate/value-object thuần; không tham chiếu project khác. |
| `RideBound.Application.csproj` | Use case/port, chỉ tham chiếu Domain. |
| `RideBound.Algorithms.csproj` | Policy/candidate logic dùng Application + Domain, không framework/solver package. |
| `RideBound.Solvers.OrTools.csproj` | Isolated adapter duy nhất chứa pinned `Google.OrTools` package. |
| `RideBound.Infrastructure.csproj` | Adapter shell bên ngoài portable core; hiện chỉ marker, không kéo EF/ASP.NET vào core. |
| `RideBound.Runner.csproj` | Composition root/executable; ghép Contracts, core, Algorithms và solver adapter. |

## Contracts

| File | Vai trò và điều cần hiểu |
|---|---|
| `Protocol/CanonicalUnits.cs` | Unit IDs/conversion contract; ngăn simulator tự đổi đơn vị ngầm. |
| `Protocol/CheckpointMessages.cs` | Wire DTO cho checkpoint/restore và exact state/hash cursor. |
| `Protocol/CommitmentContracts.cs` | Wire promise vector, ledger/certificate/witness; không chứa policy logic. |
| `Protocol/DecisionMessages.cs` | Decision envelope, solver diagnostics và publication payload. |
| `Protocol/ErrorMessage.cs` | Typed protocol error an toàn, không dùng exception text làm wire contract. |
| `Protocol/EventBatchMessages.cs` | Ordered event batch, event range và simulation time. |
| `Protocol/HelloMessages.cs` | Capability offer/selection/downgrade handshake. |
| `Protocol/InitializeRunMessages.cs` | Manifest/config/provenance binding khi mở run. |
| `Protocol/OnlineDecisionActions.cs` | Closed union các action mà decision được phép phát. |
| `Protocol/OnlineEventModels.cs` | Closed union event đầu vào online. |
| `Protocol/ProtocolEnvelope.cs` | Envelope identity chung: schema/message/run/scenario/epoch/time/payload. |
| `Protocol/ProtocolEnvelopeCodec.cs` | Strict deserialize/serialize và reject unknown/malformed shape. |
| `Protocol/ProtocolIdentityPrimitives.cs` | Validated IDs/hash/version primitives. |
| `Protocol/ProtocolPayloadValidation.cs` | Cross-field/range/required-field validation cho payload. |
| `Protocol/ProtocolPrimitives.cs` | Safe integer, bounded text và protocol constants. |
| `Protocol/ProtocolVersionCompatibility.cs` | Exact compatibility/downgrade rule; không tự chấp nhận version lạ. |
| `Serialization/CanonicalJson.cs` | Ordinal object ordering, array preservation, integer-only canonical JSON, strict Unicode. |
| `Serialization/ProtocolHash.cs` | Domain-separated SHA-256 cho từng artifact type. |

## Domain

| File | Vai trò và điều cần hiểu |
|---|---|
| `Common/DomainPrimitives.cs` | Validated time/distance/capacity/value types và overflow/range guard. |
| `Requests/RideRequest.cs` | Request lifecycle, windows, max ride, assignment/promise references. |
| `Vehicles/VehicleState.cs` | Vehicle capacity/location/progress/current route state. |
| `Routes/RoutePlan.cs` | Executed prefix + mutable future suffix; stop/order/service invariants. |
| `Runs/RideBoundRun.cs` | Aggregate identity/lifecycle/cursor; không chứa adapter persistence. |
| `Validation/IStopDistanceLookup.cs` | Domain port cho distance lookup. |
| `Validation/ITravelTimeLookup.cs` | Domain port cho travel-time lookup/versioned source. |
| `Validation/PhysicalPlanValidator.cs` | Independent recomputation capacity/order/time-window/max-ride/prefix; publication gate đầu tiên. |
| `Commitments/CommitmentDimension.cs` | Closed 10-dimension vocabulary và ordinal order. |
| `Commitments/CommitmentVector.cs` | Safe vector arithmetic/comparison; không dùng float epsilon. |
| `Commitments/RiderPromise.cs` | Versioned published pickup/drop/vehicle/stop/order promise. |
| `Commitments/CommitmentPolicy.cs` | Named budget/lock policy values; không chọn experimental defaults. |
| `Commitments/CommitmentBudgetEvaluator.cs` | Consumption/remaining/violation recomputation per rider/dimension. |
| `Commitments/CommitmentLockEvaluator.cs` | Hard lock feasibility independent objective score. |
| `Commitments/CommitmentLedger.cs` | Immutable chained publication/incident history. |
| `Incidents/OperationalIncidentLedger.cs` | Exogenous incident evidence tách khỏi policy-caused revision. |

## Application

| File | Vai trò và điều cần hiểu |
|---|---|
| `Events/OnlineEvents.cs` | Validated internal event union sau wire mapping. |
| `State/OnlineState.cs` | Immutable online aggregate snapshot và deterministic indexes. |
| `State/EventReducer.cs` | Pure single-event transition; không partial mutation. |
| `State/EventReductionCoordinator.cs` | Atomic ordered batch reduction/cursor advance. |
| `State/VersionedPlanPool.cs` | Immutable deterministic multiple-plan versions/dominance state. |
| `Travel/TravelTimeSnapshot.cs` | Version/hash-bound travel matrix lookup và reachability. |
| `Scheduling/RouteScheduleProjector.cs` | Recompute stop arrivals/departures/wait/load/ride time. |
| `Promises/PromiseProjector.cs` | Project candidate plan thành per-rider promises. |
| `Promises/PromiseDeltaCalculator.cs` | Three-way exogenous/decision/visible delta. |
| `Commitments/CommitmentDecisionValidator.cs` | Physical→lock→budget→certificate/witness independent pipeline. |
| `Optimization/CandidateSelectionModel.cs` | Framework-neutral candidate/objective/constraint input. |
| `Optimization/CandidateSelectionSolution.cs` | Validated solver result/status/bounds/selected IDs. |
| `Optimization/CandidateSelectionSolver.cs` | Solver port; Application không biết OR-Tools. |
| `Optimization/CandidateSelectionExecution.cs` | Deadline/status/fallback orchestration và post-solve validation. |

## Algorithms — candidate và commitment

| File | Vai trò và điều cần hiểu |
|---|---|
| `Candidates/CandidateIdentity.cs` | Stable canonical candidate identity/dedup key. |
| `Candidates/CandidateModels.cs` | Candidate/schedule/loss/diagnostic records. |
| `Candidates/CandidateScheduleEvaluator.cs` | Exact schedule feasibility/cost from route projection. |
| `Candidates/ForwardSlackProfile.cs` | Backward/forward slack precomputation cho safe pruning. |
| `Candidates/InsertionCandidateGenerator.cs` | Bounded best-first insertion enumeration, cap/loss accounting. |
| `Candidates/OriginHoldCandidateTransformer.cs` | Real wait/hold schedule transformation, không chỉ score penalty. |
| `Candidates/WaitingIncumbentRepairSeedBuilder.cs` | B4 same-vehicle remove/reinsert seed, giữ no-reassignment. |
| `Commitments/CommitmentCandidateAssessor.cs` | Common assessor result/diagnostics abstraction. |
| `Commitments/CommitmentCandidateFilter.cs` | Filter invalid candidates before ranking/solver. |
| `Commitments/CommitmentWarningProfile.cs` | Soft-warning projection, không nới hard gate. |
| `Commitments/HardVectorCandidateAssessor.cs` | Independent per-dimension hard-vector utilization/violation. |

## Algorithms — policies và solver orchestration

| File | Vai trò và điều cần hiểu |
|---|---|
| `Policies/CommitmentMechanismPolicies.cs` | B2/B3/B4/B5 named mechanism wrappers. |
| `Policies/CandidateFleetSelector.cs` | Deterministic cross-vehicle candidate merge/selection port. |
| `Policies/RollingCostDecisionModels.cs` | Common rolling-cost keys/decision diagnostics. |
| `Policies/RollingCostPolicy.cs` | B1 distinguished deterministic policy. |
| `Policies/RevisionPenaltyFleetSelector.cs` | B2 marginal revision penalty while preserving feasibility. |
| `Policies/MultiplePlanPolicy.cs` | B5 deterministic plan-pool consensus/dominance. |
| `Policies/HardVectorPolicy.cs` | C1 hard-vector gate plus lexicographic selection. |
| `Policies/SoftHardHybridPolicy.cs` | C2 hard constraints + explicit soft warning objective. |
| `Policies/SolverBackedFleetSelection.cs` | Build common solver model, interpret status, validate chosen candidate. |
| `Policies/SolverBackedRidePoolingPolicy.cs` | End-to-end policy generation→assessment→solve→safe fallback. |

## OR-Tools, Runner và Infrastructure

| File | Vai trò và điều cần hiểu |
|---|---|
| `RideBound.Solvers.OrTools/OrToolsCandidateSelectionSolver.cs` | Deterministic CP-SAT multi-pass lexicographic solve, dominance/overflow guard, status/bound/gap. |
| `Runner/Configuration/CommitmentPolicyConfiguration.cs` | Strict named commitment catalog/config binding. |
| `Runner/Configuration/Wp4RunnerConfiguration.cs` | Candidate/solver limits and policy IDs bound into manifest hash. |
| `Runner/Online/OnlineEventMapper.cs` | Wire event → internal event; no simulator-specific algorithm. |
| `Runner/Online/OnlineDecisionActionMapper.cs` | Internal validated decision/certificate → closed wire actions. |
| `Runner/Online/OnlineStateCanonicalizer.cs` | Stable state representation/hash input. |
| `Runner/Online/OnlineStateCheckpointCodec.cs` | Exact checkpoint encode/restore validation. |
| `Runner/Protocol/CapabilityNegotiator.cs` | Hello subset/downgrade negotiation. |
| `Runner/Protocol/EventBatchOrderingValidator.cs` | Epoch/sequence/sim-time continuity before reducer. |
| `Runner/Protocol/InitializeRunValidator.cs` | Manifest/binary/core/config/source conversion binding. |
| `Runner/Protocol/NdjsonReader.cs` | Bounded strict line reader. |
| `Runner/Protocol/NdjsonWriter.cs` | Canonical one-frame-per-line writer. |
| `Runner/Protocol/RunnerSession.cs` | Per-run phase, pending decision, ACK/checkpoint state. |
| `Runner/Protocol/RunnerHost.cs` | Dispatch protocol commands and fail closed on phase/context errors. |
| `Runner/Program.cs` | Composition root only; wires solver/policies/session host. |
| `RideBound.Infrastructure/AssemblyReference.cs` | Infrastructure project intentionally empty ở WP1–WP5; adapters live ngoài portable core. |

Không có file trong bảng trên được phép trực tiếp gọi EF/ASP.NET/SignalR. Architecture
tests bảo vệ dependency, còn source audit xác nhận flow thực tế cũng giữ boundary.
