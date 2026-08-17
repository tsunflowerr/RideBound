# Bản đồ từng file code

Danh sách này mô tả trách nhiệm của mọi production `.cs` trong RideBound WP1–WP6.
Test file được nhóm theo project ở cuối; khi cần behavior cụ thể, tìm test trùng tên
class. `AssemblyReference.cs` chỉ là marker cho architecture/smoke tests.

## RideBound.Contracts

### Serialization

- `Serialization/CanonicalJson.cs`: canonical JSON byte writer; strict number, Unicode,
  duplicate/null/property-order rules.
- `Serialization/ProtocolHash.cs`: domain-separated manifest/state/decision SHA-256 với
  tagged length frames.

### Protocol shell và primitives

- `Protocol/ProtocolPrimitives.cs`: protocol bounds, schema/message/version primitives.
- `Protocol/ProtocolIdentityPrimitives.cs`: typed run/scenario/epoch/time/hash IDs.
- `Protocol/CanonicalUnits.cs`: integer unit conversion và rounding policy.
- `Protocol/ProtocolEnvelope.cs`: typed outer message envelope.
- `Protocol/ProtocolEnvelopeCodec.cs`: strict encode/decode/context validation.
- `Protocol/ProtocolPayloadValidation.cs`: shared exact-field/type helpers.
- `Protocol/ProtocolVersionCompatibility.cs`: patch/minor/major compatibility policy.
- `Protocol/HelloMessages.cs`: capability offer/selection handshake.
- `Protocol/InitializeRunMessages.cs`: manifest, initial identity và config payload.
- `Protocol/EventBatchMessages.cs`: ordered event-batch wire contract.
- `Protocol/OnlineEventModels.cs`: typed online event union.
- `Protocol/DecisionMessages.cs`: decision/certificate/solver wire payload.
- `Protocol/OnlineDecisionActions.cs`: accept/reject/defer/route/promise actions.
- `Protocol/ErrorMessage.cs`: stable failure code/disposition payload.
- `Protocol/CheckpointMessages.cs`: checkpoint/restore content and hash shell.
- `Protocol/CommitmentContracts.cs`: promise/vector/ledger/witness wire models.
- `Protocol/Wp4PolicyConfigurationBinding.cs`: decode exact WP4 config và semantic hash;
  tránh so arm bằng raw JSON hash đơn thuần.

## RideBound.Domain

- `Common/DomainPrimitives.cs`: safe integer IDs, time/duration/node/position/result.
- `Requests/RideRequest.cs`: request lifecycle và assignment/board/complete transitions.
- `Routes/RoutePlan.cs`: frozen prefix, mutable suffix, stop/version/execution progress.
- `Vehicles/VehicleState.cs`: capacity, position, accepted/onboard sets và route updates.
- `Runs/RideBoundRun.cs`: immutable aggregate phối hợp request + vehicle atomically.
- `Validation/ITravelTimeLookup.cs`: inward port cho directed travel duration.
- `Validation/IStopDistanceLookup.cs`: inward port cho relocation distance.
- `Validation/PhysicalPlanValidator.cs`: độc lập dựng schedule và kiểm toàn physical
  invariants/reassignment.
- `Commitments/CommitmentDimension.cs`: canonical 10-dimension vocabulary.
- `Commitments/CommitmentVector.cs`: fixed-size nonnegative vector và checked addition.
- `Commitments/RiderPromise.cs`: versioned promise/service-order/phase value object.
- `Commitments/CommitmentPolicy.cs`: limits, thresholds, phase/freeze/final flags.
- `Commitments/CommitmentBudgetEvaluator.cs`: component-wise cumulative budget witness.
- `Commitments/CommitmentLockEvaluator.cs`: assignment/onboard/freeze/final structural locks.
- `Commitments/CommitmentLedger.cs`: immutable initial/revision history, no refund.
- `Incidents/OperationalIncidentLedger.cs`: incident open/resolve và explicit breach history.

## RideBound.Application

### Online state

- `Events/OnlineEvents.cs`: internal typed events sau wire mapping.
- `Travel/TravelTimeSnapshot.cs`: versioned immutable directed travel lookup.
- `State/OnlineState.cs`: run + travel + cursor + commitments + incidents + plan pool.
- `State/EventReducer.cs`: atomic ordered batch reduction và bootstrap rules.
- `State/EventReductionCoordinator.cs`: committed/pending/ACK transaction boundary.
- `State/VersionedPlanPool.cs`: canonical distinguished/alternative plan-pool state.

### Promise/commitment

- `Scheduling/RouteScheduleProjector.cs`: recompute ETA/service schedule từ route thật.
- `Promises/PromiseProjector.cs`: schedule → accepted/onboard rider promise.
- `Promises/PromiseDeltaCalculator.cs`: old→exo, exo→new, old→new 10D deltas.
- `Commitments/CommitmentDecisionValidator.cs`: independent full-fleet physical/lock/
  budget/ledger/publication validator.

### Solver-neutral model

- `Optimization/CandidateSelectionModel.cs`: canonical vehicles/options/objectives/work.
- `Optimization/CandidateSelectionSolution.cs`: status, selection, bounds/gap diagnostics.
- `Optimization/CandidateSelectionSolver.cs`: inward solver port.
- `Optimization/CandidateSelectionExecution.cs`: model/solution validation và safe fallback.

## RideBound.Algorithms

### Candidate layer

- `Candidates/CandidateModels.cs`: candidate route/fleet/work/diagnostic records.
- `Candidates/CandidateIdentity.cs`: canonical stable candidate/stop identity.
- `Candidates/CandidateScheduleEvaluator.cs`: schedule/cost/slack evaluation.
- `Candidates/ForwardSlackProfile.cs`: backward slack certificate và bounded cache.
- `Candidates/OriginHoldCandidateTransformer.cs`: executable wait-at-origin transform.
- `Candidates/WaitingIncumbentRepairSeedBuilder.cs`: same-vehicle repair seeds.
- `Candidates/InsertionCandidateGenerator.cs`: exact-small/best-first bounded insertion,
  validation/cap/omission diagnostics.

### Commitment assessment

- `Commitments/CommitmentCandidateAssessor.cs`: project/delta assessment cho ranking.
- `Commitments/CommitmentCandidateFilter.cs`: safe early prune, không phải authority.
- `Commitments/CommitmentWarningProfile.cs`: slack/near-limit warnings.
- `Commitments/HardVectorCandidateAssessor.cs`: hard-vector feasibility/ranking input.

### Policies

- `Policies/RollingCostDecisionModels.cs`: policy input/output và diagnostics.
- `Policies/RollingCostPolicy.cs`: B1 rolling lexicographic selection/apply.
- `Policies/CandidateFleetSelector.cs`: exhaustive consistent fleet combination selection.
- `Policies/RevisionPenaltyFleetSelector.cs`: B2 revision-aware lexicographic selector.
- `Policies/CommitmentMechanismPolicies.cs`: named policy composition B/C families.
- `Policies/SoftHardHybridPolicy.cs`: soft preference dưới hard publication boundary.
- `Policies/HardVectorPolicy.cs`: hard-vector policy và repair path.
- `Policies/MultiplePlanPolicy.cs`: B5 pool, Pareto/diversity/consensus/distinguished plan.
- `Policies/SolverBackedFleetSelection.cs`: canonical solver model, status/bound validation,
  safe fallback và unique candidate guards.
- `Policies/SolverBackedRidePoolingPolicy.cs`: candidate generation + solver + full policy
  orchestration.

## RideBound.Solvers.OrTools

- `OrToolsCandidateSelectionSolver.cs`: exactly-one/at-most-one CP-SAT model, deterministic
  multi-pass lexicographic solve và truthful portable result.

## RideBound.Runner

### Configuration/composition

- `Configuration/CommitmentPolicyConfiguration.cs`: named WP3 policy parse/validation.
- `Configuration/Wp4RunnerConfiguration.cs`: WP4 arm/options parse, semantic binding và
  per-run solver seed derivation.
- `Program.cs`: CLI modes/options, dependency composition, stdout/stderr discipline.
- `Protocol/RunnerHost.cs`: long-lived read/process/write loop.
- `Protocol/RunnerSession.cs`: protocol state machine, retry, online decision,
  revalidation, ACK/checkpoint/restore authority.

### Protocol helpers

- `Protocol/NdjsonReader.cs`: bounded strict UTF-8 line reader.
- `Protocol/NdjsonWriter.cs`: canonical LF writer/flush.
- `Protocol/CapabilityNegotiator.cs`: deterministic capability selection.
- `Protocol/InitializeRunValidator.cs`: manifest/capability/config/context cross-binding.
- `Protocol/EventBatchOrderingValidator.cs`: wire sequence/epoch/time precheck.

### Online mapping/checkpoint

- `Online/OnlineEventMapper.cs`: protocol event → Application event.
- `Online/OnlineDecisionActionMapper.cs`: domain decision → strict wire actions.
- `Online/OnlineStateCanonicalizer.cs`: canonical state bytes/hash input.
- `Online/OnlineStateCheckpointCodec.cs`: full state encode/rehydrate/reachability checks.

## RideBound.Benchmarking.Contracts

- `BenchmarkModels.cs`: dataset/scenario/plan/run/failure/exclusion/metric/bundle models.
- `BenchmarkContractCodec.cs`: strict canonical encode/decode for WP6 documents.
- `BenchmarkContractValidator.cs`: cross-field/order/range/terminal/path invariants.
- `BenchmarkIdentity.cs`: domain-separated scenario/plan/run/metric/bundle identities.
- `BenchmarkSeed.cs`: registered-component HMAC seed hierarchy.

## RideBound.Benchmarking

### Dataset/normalization

- `Datasets/DatasetAcquisitionModels.cs`: acquisition/cache/extraction result models.
- `Datasets/DatasetSourceRegistry.cs`: immutable FleetPy artifact/member registry.
- `Datasets/VerifiedDatasetDownloader.cs`: bounded verified staging/cache promotion.
- `Datasets/SafeZipExtractor.cs`: topology/member/ratio/link/collision-safe extraction.
- `Normalization/StrictCsv.cs`: bounded deterministic CSV parser.
- `Normalization/DirectedTravelGraph.cs`: directed graph/SCC/shortest-path primitives.
- `Normalization/FleetPyNormalizationModels.cs`: normalized inputs/config/report records.
- `Normalization/FleetPyNormalizerSourceIdentity.cs`: exact normalizer source inventory.
- `Normalization/FleetPyManhattanNormalizer.cs`: verified source → canonical derivative.

### Planning/execution/storage

- `Planning/BenchmarkPlanningModels.cs`: arm definition, compiled plan/run records.
- `Planning/BenchmarkPlanCompiler.cs`: canonical binding, fair pairing, bounded HMAC grid.
- `Execution/ExternalProcessModels.cs`: limits, samples, terminal evidence models.
- `Execution/ProcessLaunchIdentity.cs`: executable/arguments/environment launch identity.
- `Execution/ProcessArtifactIdentity.cs`: pre/post artifact inventory and hashes.
- `Execution/ProcessTreeSnapshot.cs`: Windows descendant/resource sampling.
- `Execution/BoundedRecordingStreams.cs`: bounded stdin/stdout/stderr evidence streams.
- `Execution/ExternalProcessSupervisor.cs`: isolated external process lifecycle/limits.
- `Execution/RunnerProtocolFixtureConversation.cs`: real Runner handshake/event/ACK/checkpoint.
- `Execution/PublicDerivativeMechanicalDrainConversation.cs`: declared medium mechanical
  driver; not FleetPy simulator loop.
- `Storage/RunStoreModels.cs`: run directory/terminal/evidence records.
- `Storage/ProtocolObservationIndexer.cs`: transcript → canonical observation rows.
- `Storage/ExternalProcessTerminalMapper.cs`: process evidence → typed run terminal.
- `Storage/AppendOnlyRunStore.cs`: create-new persistence, recovery và portable verify.

### Metrics, claims, bundles

- `Metrics/MechanicalMetricModels.cs`: metric evidence/summary DTOs.
- `Metrics/MechanicalMetricRegistry.cs`: immutable 36-definition registry.
- `Metrics/MetricEvidenceIdentity.cs`: semantic/resource evidence hashes.
- `Metrics/MechanicalMetricCalculator.cs`: production raw-to-metric calculation.
- `Metrics/MechanicalMetricOracleVerifier.cs`: reference-free independent oracle compare.
- `Claims/ArtifactClaimModels.cs`: mechanical-only profile/report/witness models.
- `Claims/ArtifactClaimChecker.cs`: exact caveat + bounded normalized claim scan.
- `Bundles/StrictBundleModels.cs`: bundle/provenance/verification records.
- `Bundles/BundleSourceInventoryCapture.cs`: exact clean/dirty source provenance.
- `Bundles/StrictBagItBundleBuilder.cs`: sealed no-extra bundle construction.
- `Bundles/StrictBagItBundleVerifier.cs`: path→BagIt→semantic→oracle→claim verification.

### End-to-end harness

- `EndToEnd/MechanicalPairedHarnessModels.cs`: shared paths/receipt/run evidence.
- `EndToEnd/TinyPairedHarnessModels.cs`: tiny source fixture and config records.
- `EndToEnd/TinyProtocolFixtureCompiler.cs`: fixture → exact Runner input; bind solver seed.
- `EndToEnd/TinyPairedHarness.cs`: tiny B1/C1 full pipeline.
- `EndToEnd/PublicDerivativeMechanicalFixtureModels.cs`: medium derivative fixtures.
- `EndToEnd/PublicDerivativeMechanicalFixtureCompiler.cs`: public scenario → run inputs.
- `EndToEnd/PublicDerivativePairedHarness.cs`: medium acquisition-to-bundle pipeline.

## Tools

- `RideBound.Wp6Dataset`: verified acquire/extract CLI.
- `RideBound.Wp6Normalize`: clean-process normalizer CLI.
- `RideBound.Wp6ContractVectors` / `Wp6SeedVectors`: published contract/seed vectors.
- `RideBound.Wp6TinyHarness` / `Wp6MediumHarness`: E2E entry points.
- `RideBound.Wp6MetricOracle`: independent process oracle.
- `RideBound.Wp6BundleVerify`: external sealed-bundle verifier.
- `RideBound.Wp6FakeChild`: adversarial process fixture, không phải production adapter.

## Test projects

- `RideBound.Contracts.Tests`: canonical bytes/hash/schema/protocol/golden vectors.
- `RideBound.Domain.Tests`: lifecycle/route/physical/ledger/budget/lock/incident invariants.
- `RideBound.Application.Tests`: reducer/projector/delta/validator/solver-neutral state.
- `RideBound.Algorithms.Tests`: insertion/policies/plan pool và exact-small oracles.
- `RideBound.Solvers.OrTools.Tests`: model/status/differential behavior.
- `RideBound.Runner.Tests`: real session/process/ACK/checkpoint/retry integration.
- `RideBound.Benchmarking.Contracts.Tests`: schema/codec/seed/identity vectors.
- `RideBound.Benchmarking.Tests`: dataset/process/store/metric/oracle/bundle/claim/E2E,
  permutation/parallel/mutation/failure matrix.
- `RideBound.ArchitectureTests`: inward dependency và forbidden-reference rules.
