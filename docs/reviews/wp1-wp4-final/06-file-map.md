# File map và code walkthrough

Mục này map từng production file có logic WP1–WP4 hoặc bị thay đổi trong đợt
audit. Các `AssemblyReference.cs` và `.csproj` thuần metadata được gom theo project;
không có business branch ẩn trong chúng.

## Contracts

| File | Logic được sở hữu |
|---|---|
| `Contracts/Protocol/CanonicalUnits.cs` | canonical unit/range validation |
| `ProtocolPrimitives.cs` | schema/message/version primitives và protocol limits |
| `ProtocolIdentityPrimitives.cs` | typed identity/time/hash values |
| `ProtocolEnvelope*.cs` | strict envelope shape, canonical encode/decode |
| `ProtocolPayloadValidation.cs` | shared exact-field/type/path validation helpers |
| `ProtocolVersionCompatibility.cs` | compatibility matrix semantics |
| `HelloMessages.cs` | capability negotiation payloads |
| `InitializeRunMessages.cs` | manifest/config identity |
| `EventBatchMessages.cs`, `OnlineEventModels.cs` | typed online input union |
| `DecisionMessages.cs`, `OnlineDecisionActions.cs` | decision/action/solver/certificate shells và cross-checks |
| `CommitmentContracts.cs` | wire promise/vector/witness/certificate codecs |
| `CheckpointMessages.cs` | outer checkpoint content/hash/restore payload |
| `ErrorMessage.cs` | error code/disposition payload |
| `Serialization/CanonicalJson.cs` | integer-only canonical JSON bytes |
| `Serialization/ProtocolHash.cs` | domain-framed manifest/state/decision/checkpoint hashes |

## Domain

| File | Logic được sở hữu |
|---|---|
| `Common/DomainPrimitives.cs` | IDs, time/duration, canonical limits, `DomainResult` |
| `Requests/RideRequest.cs` | request lifecycle/assignment/pickup-window transitions |
| `Routes/RoutePlan.cs` | version, executed/frozen/mutable route invariants |
| `Vehicles/VehicleState.cs` | capacity, rider sets, position và route transitions |
| `Runs/RideBoundRun.cs` | aggregate transitions và cross-object rehydrate validation |
| `Validation/PhysicalPlanValidator.cs` | independent route/capacity/window/ride/reassignment gate |
| `Validation/ITravelTimeLookup.cs`, `IStopDistanceLookup.cs` | provider-neutral lookup ports |
| `Commitments/CommitmentDimension.cs` | fixed ten-dimension vocabulary/order |
| `Commitments/CommitmentVector.cs` | checked component-wise vector arithmetic |
| `Commitments/CommitmentPolicy.cs` | hard limits, basis, phases, material rule, locks |
| `Commitments/CommitmentBudgetEvaluator.cs` | exact hard budget gate |
| `Commitments/CommitmentLockEvaluator.cs` | assignment/onboard/freeze/final locks |
| `Commitments/RiderPromise.cs` | versioned published projection/service tokens |
| `Commitments/CommitmentLedger.cs` | immutable history, exact-next version, no refund |
| `Incidents/OperationalIncidentLedger.cs` | incident/breach lifecycle tách normal revision |

## Application

| File | Logic được sở hữu |
|---|---|
| `Events/OnlineEvents.cs` | internal typed reducer events |
| `State/OnlineState.cs` | run/travel/ledger/incident/plan-pool state |
| `State/EventReducer.cs` | atomic batch fold và boundary checks |
| `State/EventReductionCoordinator.cs` | committed/proposed/staged transaction |
| `State/VersionedPlanPool.cs` | canonical fleet-plan identity, pool version/rehydrate |
| `Travel/TravelTimeSnapshot.cs` | directed versioned arc lookup/hash |
| `Scheduling/RouteScheduleProjector.cs` | shared node/edge schedule and cost |
| `Promises/PromiseProjector.cs` | route-derived active rider promise |
| `Promises/PromiseDeltaCalculator.cs` | exogenous/decision/visible ten-vector |
| `Commitments/CommitmentDecisionValidator.cs` | independent full publication reconstruction |
| `Optimization/CandidateSelectionModel.cs` | canonical portable assignment/options/objectives |
| `Optimization/CandidateSelectionSolution.cs` | model constraint and aggregate revalidation |
| `Optimization/CandidateSelectionSolver.cs` | budget/status/bound/gap/diagnostics/solver port |
| `Optimization/CandidateSelectionExecution.cs` | separate budgets, loss accounting, validated fallback portfolio |

## Algorithms — candidates và commitments

| File | Logic được sở hữu |
|---|---|
| `Candidates/CandidateModels.cs` | candidate/schedule/options/prune/loss contracts |
| `Candidates/CandidateIdentity.cs` | domain-framed route/candidate/search/omission IDs |
| `Candidates/CandidateScheduleEvaluator.cs` | adapter sang shared schedule projector |
| `Candidates/ForwardSlackProfile.cs` | conservative slack builder và full-identity cache |
| `Candidates/OriginHoldCandidateTransformer.cs` | executable current-node waiting relocation |
| `Candidates/WaitingIncumbentRepairSeedBuilder.cs` | atomic same-vehicle one-pair repair seeds |
| `Candidates/InsertionCandidateGenerator.cs` | deterministic best-first expansion, validate, cap/loss |
| `Commitments/CommitmentCandidateFilter.cs` | WP3 scoped hard gate trên applied candidate state |
| `Commitments/CommitmentCandidateAssessor.cs` | B2/B3 effective policy và revision assessment |
| `Commitments/HardVectorCandidateAssessor.cs` | C1/C2 hard filter, PPM, warning/revision vectors |
| `Commitments/CommitmentWarningProfile.cs` | explicit ten-limit C2 warning catalog |

## Algorithms — policies

| File | Logic được sở hữu |
|---|---|
| `Policies/CandidateFleetSelector.cs` | exact B1 Cartesian oracle/selector |
| `Policies/RollingCostDecisionModels.cs` | selection/decision/witness/diagnostic records |
| `Policies/RollingCostPolicy.cs` | physical revalidate, apply routes, request outcomes |
| `Policies/RevisionPenaltyFleetSelector.cs` | exact B2 hierarchy oracle |
| `Policies/CommitmentMechanismPolicies.cs` | B2/B3/B4 named non-solver behavior/helpers |
| `Policies/HardVectorPolicy.cs` | exact C1 policy/oracle path |
| `Policies/SoftHardHybridPolicy.cs` | exact C2 policy/oracle path |
| `Policies/MultiplePlanPolicy.cs` | B5 enumeration, Pareto/diversity/consensus/rebase |
| `Policies/SolverBackedFleetSelection.cs` | objective mapping, portable solve, independent validation |
| `Policies/SolverBackedRidePoolingPolicy.cs` | shared generation + named mechanism + production execution |

## Solver và Runner

| File | Logic được sở hữu |
|---|---|
| `Solvers.OrTools/OrToolsCandidateSelectionSolver.cs` | deterministic exact multi-pass CP-SAT adapter |
| `Solvers.OrTools.csproj` | sole pinned `Google.OrTools 9.15.6755` ownership |
| `Runner/Configuration/CommitmentPolicyConfiguration.cs` | strict WP3 config + content hash + policy catalog |
| `Runner/Configuration/Wp4RunnerConfiguration.cs` | strict variant fields, budgets/warnings, combined hash |
| `Runner/Online/OnlineEventMapper.cs` | protocol input to typed internal events |
| `Runner/Online/OnlineDecisionActionMapper.cs` | typed decision/publication actions |
| `Runner/Online/OnlineStateCanonicalizer.cs` | canonical full online state/hash including plan pool |
| `Runner/Online/OnlineStateCheckpointCodec.cs` | typed rehydrate and structural/tamper checks |
| `Runner/Protocol/CapabilityNegotiator.cs` | hello capability choice |
| `Runner/Protocol/EventBatchOrderingValidator.cs` | epoch/sequence ordering |
| `Runner/Protocol/InitializeRunValidator.cs` | manifest initialization gate |
| `Runner/Protocol/NdjsonReader.cs`, `NdjsonWriter.cs` | bounded one-object-per-line transport |
| `Runner/Protocol/RunnerSession.cs` | end-to-end state machine, policy, validation, pending/ACK/checkpoint |
| `Runner/Protocol/RunnerHost.cs` | long-lived process loop and dependency wiring |
| `Runner/Program.cs` | CLI mode/config loading and combined config hash |

## Tests đọc cùng code

| Test area | Điều nó bảo vệ |
|---|---|
| `Contracts.Tests/Protocol`, `Serialization` | exact schema/bytes/hash/tamper |
| `Domain.Tests/Validation`, `Commitments`, `Incidents` | physical + ten-vector + ledger correctness |
| `Application.Tests/Scheduling`, `Promises`, `State` | shared projection, atomic state/checkpoint |
| `Application.Tests/Optimization` | portable model, diagnostics, fallback truthfulness |
| `Algorithms.Tests/Candidates` | slack/cache/hold/best-first/loss/repair |
| `Algorithms.Tests/Policies` | B1–B5/C1/C2 semantics, pool, mapper, fallback |
| `Algorithms.Tests/Oracle` | independent exact-small generator/commitment oracle |
| `Solvers.OrTools.Tests` | adapter status/bound và 64-seed policy differential |
| `Runner.Tests/Configuration`, `Online` | strict binding, actual solve, ACK/retry/checkpoint/CLI |
| `ArchitectureTests` | forbidden dependencies và sole package pin |

`Infrastructure` hiện chỉ là boundary project; chưa có EF/provider behavior để
review. Điều đó đúng với WP4, không phải implementation bị bỏ quên.
