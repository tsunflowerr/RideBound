# BeGo current-system audit for BeGo-LTF planning

Audit date: 2026-07-15
Repository: `E:\Code\BeGo`
Reviewed branch/commit: `main` / `ebe0d34365ec4751bd5c629677733032490a1a0d`
Scope: read-only architecture, domain, optimization, persistence, API, frontend, test, benchmark, configuration, and existing fairness/debt work. No production code or documentation was changed.

## 1. Executive verdict

BeGo is a credible **single-outing research prototype**. It already has useful shared-destination primitives: route-aware venue search, automatic passenger-to-driver assignment, shared pickup-stop generation, exact/heuristic stop ordering, route pools, operational fairness metrics, native solver adapters, and a usable collaborative UI.

It is **not yet a longitudinal-temporal-fairness (LTF) system**. The current durable model is an expiring `Session`; there is no stable user/group identity, repeated outing aggregate, immutable plan revision, per-member burden observation, counterfactual reference, debt account/transaction, policy version, realized outcome, or experiment provenance. Completed sessions are eligible for physical deletion after 24 hours. Therefore, the proposed mobility-burden-debt work cannot be implemented safely by adding fields to `Session` or another term to the current scalar score.

Recommended direction: retain a modular monolith, put a stable `Group`/`Outing` model and append-only `FairnessLedger` around an extracted pure planning kernel, and keep the present `Session` flow as a compatibility/projection layer during migration. Do not begin with microservices.

The most important pre-LTF blockers are:

1. Membership authorization is missing from the session read endpoint, which returns exact member coordinates, votes, and route snapshots to any authenticated caller who knows the UUID (`src/OptiGo.API/Controllers/SessionsController.cs:20-28`; `src/OptiGo.Application/UseCases/GetSessionQueryHandler.cs:17-76`).
2. Google token audience validation is conditional and becomes fail-open when no client ID is configured; the committed configuration has an empty list (`src/OptiGo.API/Services/GoogleBearerAuthenticationHandler.cs:34-40`; `src/OptiGo.API/appsettings.json:6-8`).
3. Sessions and all dependent historical evidence are deleted after the fixed 24-hour expiry (`src/OptiGo.Domain/Entities/Session.cs:36-42`; `src/OptiGo.API/Services/ExpiredSessionCleanupService.cs:34-45`).
4. There are no concurrency tokens, expected-version checks, or idempotency keys around votes, assignments, optimization, or completion. LTF ledger updates would be vulnerable to duplicate/lost updates.
5. The metric called `WorstMemberRegretSeconds` is not counterfactual regret; it is `max burden - median burden` (`src/OptiGo.Infrastructure/Routing/RoutingSolutionScorer.cs:103-113`). It cannot be used as the proposed avoidable-regret definition.
6. The benchmark harness is valuable engineering infrastructure, but its current public-dataset transformation ignores most original DARP/PDPTW semantics and lacks multi-seed statistical inference and full run provenance. It is not yet sufficient for thesis claims.

## 2. Audit method and verification

The audit traced the solution graph, all domain entities/value objects/services, all application ports/use cases, infrastructure registration and persistence configurations/migrations, routing implementations, API controllers/auth/hub/middleware, frontend room/benchmark/auth/API/state code, test names and fixtures, benchmark code/data/reports, and the existing debt proposal.

Verification results:

| Check | Result |
|---|---|
| `dotnet build src/OptiGo.slnx --no-restore` | Passed, 0 errors; one high-severity transitive `Microsoft.OpenApi 2.0.0` vulnerability warning (`GHSA-v5pm-xwqc-g5wc`). |
| `dotnet test src/OptiGo.slnx --no-restore` | Passed: 25/25 tests, 0 skipped. |
| `npm test` | Passed: 7/7 tests. One module-type performance warning. |
| `npm run lint` | Passed. |
| `npm run build` | Passed; static `/`, `/benchmark`, dynamic `/room/[id]`, and NextAuth route generated. |
| `dotnet list ... --vulnerable --include-transitive` | One high-severity transitive backend package group: `Microsoft.OpenApi 2.0.0`. |
| `npm audit --omit=dev --json` | Three production vulnerability groups: two high, one moderate. Direct `next 16.2.2` is affected; audit reported a non-major fix at `16.2.10`. A transitive `ws` finding is also high. |

Limitations: no live PostgreSQL/Redis/Google/Mapbox/Groq environment was exercised, no browser E2E suite exists, and the 120-scenario benchmark output was inspected rather than rerun (the recorded run took about 93.5 minutes). Build/test commands created only normal ignored build artifacts.

## 3. Current system inventory

### 3.1 Solution and dependency direction

`src/OptiGo.slnx` contains five .NET 10 projects:

| Project | Approx. C# files / lines | Role and dependency direction |
|---|---:|---|
| `OptiGo.Domain` | 20 / 906 | Entities, enums, coordinate, search-center heuristics. No project dependencies. |
| `OptiGo.Application` | 38 / 1,744 | MediatR use cases, ports, API-facing DTOs. Depends on Domain. |
| `OptiGo.Infrastructure` | 44 / 10,185 | EF/Postgres, Google/Mapbox/Groq, Redis cache, routing engine, 2,708-line benchmark service. Depends on Domain and Application. |
| `OptiGo.API` | 20 / 1,025 | Controllers, Google bearer auth, SignalR, validation, rate limiting, cleanup worker. Depends on Application and Infrastructure. |
| `OptiGo.Tests` | 11 / 1,110 | xUnit unit/component-style tests. Depends on Domain, Application, Infrastructure. |

The frontend is a separate Next.js 16.2.2 / React 19.2.4 application with 18 TypeScript/TSX source files (about 179 KB), NextAuth Google login, SignalR, Tailwind, and hand-maintained types (`src/optigo-frontend/package.json:1-31`).

The dependency direction is broadly clean, but the routing contracts are not an independent kernel: infrastructure implementations consume application `UseCases` DTOs, and `IOutingRoutePlanner` accepts a mutable EF-backed `Session` and `Venue` (`src/OptiGo.Application/Interfaces/IOutingRoutePlanner.cs:1-12`). `ISessionNotifier` also uses untyped `object` payloads (`src/OptiGo.Application/Interfaces/ISessionNotifier.cs:17-36`).

### 3.2 Runtime deployment shape

`docker-compose.yml` provisions PostGIS 16, Redis 7, and pgAdmin only. API and frontend are not containerized. PostgreSQL is configured through EF Core; Redis is optional and falls back to process-local distributed memory (`src/OptiGo.Infrastructure/DependencyInjection.cs:21-49`). No deployment manifests, CI workflow, tracing/metrics stack, or production secret store were found.

Configured external adapters:

- Google Places for venue discovery and details.
- Mapbox Directions/Matrix and Search for routes and pickup POIs.
- Groq for query-category fallback and review summarization, not route optimization.
- Redis plus process memory for route/matrix caching.
- Google OR-Tools, PyVRP bridge, and VROOM bridge for benchmarks; OR-Tools is not the production planner.

`Google.Cloud.AIPlatform.V1`, NetTopologySuite, and direct `StackExchange.Redis` references have no corresponding production code usages found. PostGIS is provisioned, but venue storage uses decimal latitude/longitude, a bounding box query, and no spatial index (`src/OptiGo.Infrastructure/Persistence/Repositories/VenueRepository.cs:21-41`).

## 4. Current product/domain behavior

### 4.1 Aggregate and state machine

The only workflow aggregate is `Session`, with members, votes, nominated venue IDs, and pickup requests (`src/OptiGo.Domain/Entities/Session.cs:9-32`). Its states are:

`WaitingForMembers -> Computing -> Voting -> RoutePreview -> Completed`

`Computing`, `Voting`, and `RoutePreview` may transition to `Failed`; `Failed` has no recovery transition (`src/OptiGo.Domain/Entities/Session.cs:202-222`). This makes transient provider failure terminal and forces users to recreate the session.

The host is not an explicit identity. It is inferred as the member with the earliest `JoinedAt` in both authorization and projections (`src/OptiGo.Application/UseCases/SessionAuthorization.cs:21-30`; `src/OptiGo.Application/UseCases/GetSessionQueryHandler.cs:24-50`). This is fragile under equal timestamps, rehydration/order changes, membership removal, and future group reuse.

`Member` contains session-local name/location, Google subject/email, transport mode, mobility role, optional driver ID, and join time (`src/OptiGo.Domain/Entities/Member.cs:8-45`). The role model has only `SelfTravel` and `NeedsPickup`; vehicle capacity is inferred from transport mode: motorbike 1, car 3, bus 10 (`src/OptiGo.Domain/ValueObjects/TransportCapacityDefaults.cs:5-13`). There is no accessibility profile, maximum walk preference, time window, vehicle instance, wheelchair/child-seat constraint, availability window, preference confidence, consent, or location precision policy.

Important aggregate gaps:

- `Session.AddMember` checks only duplicate member IDs, not matching `SessionId` or duplicate `AuthSubject` (`src/OptiGo.Domain/Entities/Session.cs:50-59`). Join always creates another member for the current subject (`src/OptiGo.Application/UseCases/JoinSessionCommand.cs:42-64`).
- The database index `(session_id, auth_subject)` is non-unique (`src/OptiGo.Infrastructure/Persistence/Configurations/MemberConfiguration.cs:70-74`), making authorization's `FirstOrDefault` ambiguous.
- `Member.SetDriver` only prevents self-assignment; role, driver capability, capacity, and same-session checks live elsewhere (`src/OptiGo.Domain/Entities/Member.cs:90-94`).
- Removing a driver does not release requests accepted by that driver; only the removed passenger's own request is removed (`src/OptiGo.Domain/Entities/Session.cs:61-76`). There is currently no actual leave command/endpoint, so this method is dead behavior.
- The UI's “leave” flow only broadcasts a SignalR event and clears local storage. `SessionHub.NotifyMemberLeft` does not mutate persistence (`src/OptiGo.API/Hubs/SessionHub.cs:63-101`).
- `SetNominatedVenues` has no state, non-empty, uniqueness, or persisted-venue invariant (`src/OptiGo.Domain/Entities/Session.cs:242-245`).
- Pickup request uniqueness is enforced in memory only; the database `(session_id, passenger_id)` index is not unique (`src/OptiGo.Infrastructure/Persistence/Configurations/PickupRequestConfiguration.cs:43-44`).
- `PickupRequest.Accept` permits reassignment of an already accepted request without an explicit transition/precondition (`src/OptiGo.Domain/Entities/PickupRequest.cs:34-42`).

### 4.2 Main single-outing flow

1. Create/join stores the authenticated Google subject on a session-local member.
2. Host starts optimization. Status is committed as `Computing` before provider calls (`src/OptiGo.Application/UseCases/FindMeetPointHandler.cs:58-75`).
3. A weighted search center is computed; Google text search asks for up to 50 venues from an initial 500 m radius, with Groq category fallback if fewer are found (`src/OptiGo.Application/UseCases/FindMeetPointHandler.cs:79-124`).
4. A straight-line offline filter and route-aware prefilter reduce candidates to 15; at most three plans are computed concurrently (`src/OptiGo.Application/UseCases/FindMeetPointHandler.cs:131-148,259-269`).
5. Three nominees are persisted and the whole result is serialized into a text JSON snapshot; status becomes `Voting` (`src/OptiGo.Application/UseCases/FindMeetPointHandler.cs:148-169`).
6. Every member casts exactly one vote. When all have voted, simple plurality selects the winner; a final route is replanned and another text JSON snapshot is stored (`src/OptiGo.Application/UseCases/SubmitVoteHandler.cs:61-83`). A tie has no explicit deterministic policy; it depends on group enumeration/order.
7. Host “locks departure,” which only timestamps and marks the session `Completed` (`src/OptiGo.Application/UseCases/LockDepartureCommand.cs:28-40`). No predicted burden, realized burden, attendance, deviation, or outcome is committed.

This flow has two distinct decision policies that are currently conflated: the optimizer recommends three candidates, but final selection is unweighted single-choice plurality and ignores optimizer scores. A future thesis must state whether fairness governs the candidate set, final collective choice, route plan, or all three.

## 5. Optimization engine audit

### 5.1 Reusable production pipeline

The strongest reusable part is the planning pipeline:

- `OutingSearchCenterCalculator` builds a weighted center and temporarily projects unassigned pickup demand toward eligible drivers.
- `RouteAwareVenuePrefilter` estimates route-aware venue cost.
- `StopCandidateGenerator` produces doorstep, corridor, directional, POI, centroid, and shared-cluster pickup stops.
- `SharedDestinationRouteOptimizer` enumerates set-cover alternatives, orders stops exactly up to nine stops and heuristically above that, then evaluates route burden.
- `HybridOutingRoutePlanner` jointly explores route-pool and passenger-driver assignment alternatives, applies capacity/accepted-assignment constraints, and compares candidates.
- `RoutingSolutionScorer` supplies common metrics, feasibility checks, cost/fairness scores, and venue quality bonus.
- `DefaultVenueEvaluator` creates a candidate-set-relative score and Pareto labels.

These should be extracted behind immutable `PlanningProblem`, `PlanningPolicy`, `PlanCandidate`, `BurdenVector`, and `PlanningDiagnostics` types. Passing the tracked `Session` into the planner makes replay, versioning, deterministic experiments, and shadow-policy comparison unnecessarily hard.

### 5.2 Hard-coded policy and model assumptions

All material planning limits/weights are internal constants, not a versioned policy: 500 m/8 min walking, 35 min passenger time, 15 min driver detour, 1.25 m/s walk speed, fairness/detour/wait weights, stop penalties, state caps, and cluster radii (`src/OptiGo.Infrastructure/Routing/RoutingDefaults.cs:3-43`). `MaxRefinementIterations = 200` is declared but unused.

Consequences:

- Historical results cannot be reproduced after a constant changes.
- Groups cannot express different mobility/accessibility constraints.
- Sensitivity/ablation studies require recompilation and cannot attach a policy hash to outputs.
- A score in one version has no durable semantic identity.

Transport modeling is approximate. Walking/cycling use Mapbox profiles, while motorbike, car, and bus all use `driving`; motorbike is multiplied by 0.85 and bus by 1.8 (`src/OptiGo.Infrastructure/ExternalServices/Mapbox/MapboxTransportModeMapper.cs:8-25`). This is neither transit routing nor a calibrated motorbike model. The default traffic snapshot is only a five-minute cache bucket with multiplier 1.0 and `IsLive=false` (`src/OptiGo.Infrastructure/Routing/DefaultTrafficSnapshotProvider.cs:8-24`), and the production planner explicitly sets `PreferTrafficAwareRoutes=false` (`src/OptiGo.Infrastructure/Routing/HybridOutingRoutePlanner.cs:103-111`).

### 5.3 Algorithmic correctness and diagnostics gaps

The engine is explicitly heuristic despite some “exact” components:

- Route-pool subsets are capped (1,024 per driver, then 50 retained) and generated skip-first, so truncation can be enumeration/order biased (`src/OptiGo.Infrastructure/Routing/HybridOutingRoutePlanner.cs:380-417`). Route-pool combination search has no wall-clock/state budget or optimality-gap diagnostic (`:419-496`).
- Assignment search silently stops after 200,000 states and retains 64 solutions (`:745-813`). The suffix “lower bound” adds static option costs, but future dynamic load terms can be negative because shared-cluster bonuses exceed load penalties (`:727-740,785-790`). Therefore that bound is not admissible and can prune the true best assignment.
- Candidate comparison is a sequence of asymmetric threshold overrides inside a 2% cost band (`:126-166`). This relation can be order-dependent/non-transitive; the selected plan may change with candidate enumeration order.
- The exact Held-Karp path for up to nine stops optimizes matrix duration plus service time only (`src/OptiGo.Infrastructure/Routing/SharedDestinationRouteOptimizer.cs:273-361`). It does not optimize the full wait, walking, risk, fairness, shared-stop, and detour objective used during final evaluation. Conversely, the heuristic ordering above nine stops includes `AccessPenaltySeconds`, but the final generalized-cost evaluation never adds that access penalty (`:473-492,550-669`). The search and evaluator therefore optimize inconsistent objectives.
- Above nine stops, cheapest insertion plus swap/reversal repeatedly evaluates routes without an explicit iteration/time budget (`:364-470`).
- Walking feasibility for generated stops uses Haversine/straight-line distance, not a pedestrian network or barrier/accessibility check (`src/OptiGo.Infrastructure/Routing/StopCandidateGenerator.cs:189-218,311-332`).
- Candidate deduplication can take the minimum access and minimum risk from different near-by candidates while retaining one location, producing an optimistic synthetic stop (`:358-395`).
- “Wait” is a capped heuristic derived from vehicle ETA and walking distance, not a departure-time/synchronization schedule (`src/OptiGo.Infrastructure/Routing/SharedDestinationRouteOptimizer.cs:857-864`). All vehicle routes effectively start at time zero; arrival spread compares route durations rather than optimized departure/arrival times.
- The planner contains a second unused copy of solution-metric, validation, and quality-bonus methods (`src/OptiGo.Infrastructure/Routing/HybridOutingRoutePlanner.cs:877-998`), increasing semantic-drift risk.

No result exposes explored states, truncation reason, candidate coverage, lower/upper bound, gap, random seed, provider/model version, cache-hit provenance, or deterministic replay key. Those are essential for an academic system.

### 5.4 Prefilter defect

Pending pickup passengers are excluded from self-travel venue cost, and if any unaccepted request exists, the same `+900` constant is added to every venue (`src/OptiGo.Infrastructure/Routing/RouteAwareVenuePrefilter.cs:50-66`). The constant cannot affect ordering. Thus the 15-venue shortlist can ignore the spatial/accessibility needs of unassigned passengers before the expensive planner gets a chance to evaluate them.

### 5.5 Dead/contradictory domain algorithms

`ScoringEngine`, `PickupPairValidator`, and `GeometricMedianCalculator` have no references outside their own files. `PickupPairValidator` states that one driver may pick up at most one passenger (`src/OptiGo.Domain/Services/PickupPairValidator.cs:27-37`), contradicting current multi-seat capacity semantics. These should not be treated as supported alternatives; remove or quarantine them after characterization tests.

## 6. What current “fairness” actually means

| Current name | Actual implementation | LTF limitation |
|---|---|---|
| Passenger burden | Ride time + `1.25 * walk` + `1.1 * heuristic wait` + stop risk (`SharedDestinationRouteOptimizer.cs:623-648`). | Predicted, not observed; no preference/accessibility normalization or policy version. |
| Driver burden | Driver route `GeneralizedCostSeconds`, which includes route duration plus aggregate passenger walking/wait/fairness/risk terms (`:652-699`). | Not the driver's individual experienced burden; it mixes other members' utility into the driver's record. |
| Passenger Gini | Gini over assigned pickup passengers only (`RoutingSolutionScorer.cs:80-86,113`). | Excludes self-travelers and drivers; incomparable when role composition changes. |
| Driver Gini | Gini over nonnegative route-duration detours (`:91-93,119-122`). | Single-event predicted detour only. |
| Worst member regret | `max(member burden) - median(member burden)` (`:103-113`). | Not regret and not avoidable regret; no per-member feasible counterfactual. |
| Arrival spread | Max/min driver route duration (`:100-102,123`). | No departure-time decision or common-clock arrival schedule. |
| Composite fairness | Fixed weighted scalar over group time, maxima, pseudo-regret, detours, Ginis, walking, stops, and venue bonus (`:8-31,53-72`). | No axioms, learned/calibrated weights, temporal state, or interpretable unit after mixed coefficients. |
| Venue final score | Min-max normalize within the current candidate set and weight cost/fairness/detour/walk 38/32/18/12 (`DefaultVenueEvaluator.cs:15-45`). | Rank/score can change merely by adding a dominated candidate; not stable across runs. |

`RoutingSolutionScorer.ValidateSolution` checks capacity, detour, walking, passenger duration, a max/median ratio, and route-duration spread (`src/OptiGo.Infrastructure/Routing/RoutingSolutionScorer.cs:173-235`). It does not itself assert exact assignment of every pickup member, uniqueness of assignment/stops, full member coverage, route-assignment consistency, candidate-set revision, or policy version. The benchmark adds an unassigned-pickup check separately (`src/OptiGo.Infrastructure/Routing/OutingBenchmarkService.cs:1374-1380`), demonstrating semantic duplication.

The existing `docs/thesis-mobility-burden-debt-plan.md` correctly proposes a distinction between structural cost, decision-induced cost, and avoidable regret, plus a debt-aware route-pool selector. None of `GroupHistory`, `MemberDebtState`, repeated scenarios, debt update/repayment, feasible reference computation, predictive/robust DARS, or outcome adjustment exists in production. The document should therefore be treated as a research hypothesis/specification, not current capability.

## 7. Persistence and history audit

EF exposes only six sets: sessions, members, votes, venues, pickup requests, chat messages (`src/OptiGo.Infrastructure/Persistence/OptiGoDbContext.cs:7-14`). There is no `User`, `Group`, `GroupMembership`, `Outing`, `ParticipantSnapshot`, `CandidateSet`, `PlanRevision`, `Decision`, `BurdenObservation`, `DebtAccount`, `DebtTransaction`, `Outcome`, `PolicyVersion`, `ExperimentRun`, or `AuditEvent`.

Current persistence concerns:

- Optimization and final-route snapshots are unversioned text JSON coupled to application DTOs (`src/OptiGo.Infrastructure/Persistence/Configurations/SessionConfiguration.cs:49-55`). `GetSessionQueryHandler` directly deserializes them and has no compatibility/error envelope (`src/OptiGo.Application/UseCases/GetSessionQueryHandler.cs:80-86`).
- `WinningVenueId` is limited to 100 characters while venue IDs allow 255 (`SessionConfiguration.cs:45-47`; `VenueConfiguration.cs:13-17`).
- No row version/concurrency token appears in configurations or entities.
- Session loading includes three collections in one query without split-query/projection (`src/OptiGo.Infrastructure/Persistence/Repositories/SessionRepository.cs:21-27`), creating a Cartesian expansion risk as members, votes, and pickup requests grow.
- Venue cache records are global and detached from source/provider version; only selected top-three venues are persisted.
- Cleanup selects every expired session regardless of state and cascades deletion. An in-flight computation can cross the 24-hour boundary, and completed evidence is not archived first (`SessionRepository.cs:30-36`; `ExpiredSessionCleanupService.cs:34-45`).

For LTF, an expiring collaboration session may remain, but it must not own the durable research/history ledger.

## 8. API, authorization, realtime, and privacy

### 8.1 Endpoint surface

The API offers create/get/join/update-query/test-member/assignment/accept/release/suggestions/lock, optimize, vote, chat, benchmark, health, and a SignalR hub (`src/OptiGo.API/Controllers/*.cs`; `src/OptiGo.API/Program.cs:136-145`). Global fallback authentication is enabled, with the health endpoint anonymous (`Program.cs:44-49,140-145`).

Positive controls: host/member authorization helpers are used by most mutating commands; chat and pickup suggestions require membership; the SignalR group join checks the current Google subject against session members (`src/OptiGo.API/Hubs/SessionHub.cs:18-44`). Input validators and standard/expensive/chat fixed-window rate policies exist.

Material gaps:

- Session GET lacks membership authorization and exposes exact coordinates and full decision/route data.
- Token validation accepts a token without restricting its audience when `ClientIds` is empty. Startup should fail closed if no allowed audience is configured.
- Multiple session members can share one Google subject. Acting-for-member authorization then uses the first match.
- The generic owner-or-host helper is used for voting and chat, so a host can submit another member's ballot or message (`src/OptiGo.Application/UseCases/SessionAuthorization.cs:33-40`; `src/OptiGo.Application/UseCases/SubmitVoteHandler.cs:51-56`; `src/OptiGo.Application/UseCases/ChatMessages.cs:88-96`). Assignment administration may legitimately be host-delegated, but ballots and authored speech require owner-only or explicit delegation semantics.
- No API versioning, idempotency header, expected aggregate version/ETag, request correlation, structured Problem Details contract, or durable audit trail.
- The benchmark is a synchronous authenticated GET that may run 180 public scenarios. It accepts a client-supplied `publicDataRoot`, resolves arbitrary existing directories, and enumerates/parses their `.txt` files (`BenchmarksController.cs:19-43`; `OutingBenchmarkService.cs:296-354`). This is a filesystem-probing/availability risk and should be an admin-only queued job with an allow-listed dataset ID.
- Rate limiting is process-local and ineffective across replicas; a long-running benchmark can consume far more than the request-rate limit expresses (`Program.cs:51-79`).
- SignalR “member left” trusts client-provided display fields and emits an event without state transition; reconnecting clients can observe a different member list from peers.
- Location privacy is all-or-nothing. There is no per-field visibility, precision reduction, consent event, retention policy, export/delete flow, or separation of operational GPS from research/anonymized data.

Local `.env` files contain credentials/API keys but are ignored and are not currently tracked or present in repository history. Production still needs a managed secret store, startup validation, key-rotation procedure, and log redaction. Do not place secret values in planning artifacts.

## 9. Frontend audit

The product UI is functional but tightly coupled:

- `src/optigo-frontend/src/app/room/[id]/page.tsx` is about 66 KB / 1,654 lines and contains most room workflow and presentation components.
- `useSession.ts` is 409 lines and combines server fetching, SignalR reconciliation, optimistic state, actions, chat, and host derivation.
- Backend contracts are manually mirrored in `src/types/room.ts` and `benchmark.ts`; there is no generated OpenAPI client or schema compatibility test.
- The active member ID is stored in browser `localStorage` (`room-[session]-memberId`), while real authorization is Google-subject based (`page.tsx:105-181`). This dual identity should be replaced by a server-returned current-membership projection.
- Client host status falls back to `members[0]` (`src/optigo-frontend/src/hooks/useSession.ts:369-370`), duplicating the fragile backend rule and potentially diverging under realtime event ordering.
- The room UI shows one-outing route details but has no group/history view, cumulative debt explanation, policy/consent display, plan-revision comparison, uncertainty, outcome correction, or “why this compensates member X” explanation.
- Only seven utility tests exist; there are no component, hook, auth, SignalR, accessibility, map, contract, or browser E2E tests.

Useful seams to retain: NextAuth login, API wrapper shape, SignalR connection code, map/route rendering, room status metadata, benchmark visualization components, and existing interaction vocabulary. Split by feature only after stable backend contracts are introduced.

## 10. Tests and benchmark readiness

### 10.1 Test coverage

The 25 backend tests cover Google Places/Groq adapters with fakes, search center, pickup suggestion, venue prefilter/evaluator, shared-stop generation, route timing/polyline, and five hybrid-planner cases. Missing high-value suites include:

- Domain aggregate/state-transition and invariant tests.
- Authorization tests for every query/command and cross-session/member attacks.
- Google audience/misconfiguration tests.
- EF/Postgres migration, constraint, cascade, transaction, and concurrency tests.
- API integration/contract tests and SignalR tests.
- Property/metamorphic tests: permutation invariance, all-pickups-exactly-once, monotonic capacity, translation/scale behavior, no dominated selection.
- Golden-master/replay tests for routing policies and snapshots.
- Search truncation/bound correctness tests and deterministic tie tests.
- Longitudinal debt update, late outcome adjustment, membership churn, and idempotency tests (not yet implementable because the model is absent).
- Load/cancellation/provider-failure/recovery tests.

### 10.2 Existing benchmark assets

Reusable assets include deterministic synthetic layouts, 12 DARP-MP files, three Li & Lim files, a common evaluator, OR-Tools/PyVRP/VROOM bridges, OptiGo ablations, raw per-scenario output, and weakness extraction. The recorded `.buildtmp/benchmark-public-120-final.json` contains 120 scenarios and 12 algorithms/variants. Its run was seed `20260505`, from 2026-05-23T16:59:30Z to 18:33:02Z.

Selected recorded aggregates (not independently rerun in this audit):

| Algorithm | Group | Feasible rate | Avg pure cost | Avg fairness score | Avg max burden |
|---|---|---:|---:|---:|---:|
| OptiGo cost-only | A | 0.958 | 5,202.8 | 867.1 | 1,406.1 |
| OR-Tools cost-first | A | 0.817 | 5,538.7 | 1,181.0 | 1,603.9 |
| PyVRP cost-first | A | 0.783 | 5,639.0 | 1,249.3 | 1,635.2 |
| VROOM cost-first | A | 0.783 | 5,653.2 | 1,254.0 | 1,634.5 |
| OptiGo hybrid | B | 0.958 | 5,269.3 | 826.0 | 1,359.3 |
| OR-Tools fairness 2s | B | 0.908 | 5,518.0 | 998.6 | 1,483.5 |

These results are promising engineering evidence, not final scientific evidence, for the following reasons:

1. Public instances are deterministically sliced into small outing scenarios. DARP planning horizon, maximum ride time, service durations, demand signs, depots, and time windows are not carried into the planning model; Li & Lim time windows/capacity/service semantics are not enforced. The parser retains essentially coordinates and pickup/delivery linkage (`src/OptiGo.Infrastructure/Routing/OutingBenchmarkService.cs:357-599`; `docs/public-benchmark-methodology.md:35-37`).
2. Candidate venues are derived from selected delivery nodes, so this is a transformed task, not a score on the canonical DARP/PDPTW objective or published best-known solution.
3. The public README calls the setup “unbiased and rigorous,” but it does not include authoritative dataset URLs, licenses, checksums, or a complete transformation manifest (`benchmarks/public/README.md`). That claim is too strong.
4. Group A is documented as using the same Held-Karp ordering, but VROOM cost-first preserves VROOM's order while OR-Tools/PyVRP cost-first are re-evaluated through the common doorstep ordering (`OutingBenchmarkService.cs:69-84,642-705,775-794,1319-1410`). Group definitions need correction.
5. PyVRP/VROOM “fair” variants mainly reuse cost-oriented solver output and select a venue with the OptiGo fairness evaluator; OR-Tools additionally uses a global span coefficient (`:708-816,947-964`). They are fairness-selected baselines, not equivalent native multi-objective algorithms.
6. Metrics and feasibility are defined by OptiGo itself. This is useful for a common system-level evaluator but can favor OptiGo's modeled advantages and cannot establish external validity alone.
7. The report has one seed and means, with no repeated-run confidence intervals, effect sizes, hypothesis tests, runtime hardware/environment, code commit, dependency/solver versions, policy hash, dataset checksums, or raw-run manifest. Moreover, public scenario loading does not consume `request.Seed`; the recorded public seed is metadata rather than a source of replicated public runs (`OutingBenchmarkService.cs:54-62,296-333`).
8. Existing benchmark documents conflict: the older native audit says VROOM must not be reported, while the final 120-run output contains successful VROOM rows. Documents need date/version/provenance labels.
9. The thesis debt proposal itself correctly warns that current DARP/Li-Lim results are not temporal evidence (`docs/thesis-mobility-burden-debt-plan.md:198-203`).

Before LTF experiments, introduce a versioned experiment manifest and repeated-outing generator, register hypotheses/primary metrics, separate feasibility from utility, use multiple seeds and scenario families, report distributions/CI/effect sizes, and validate small instances against an exact model/lower bound.

## 11. Prioritized issue register

| ID | Severity | Finding | Required disposition before LTF |
|---|---|---|---|
| SEC-01 | Critical | Session read is authenticated but not membership-authorized; exact locations/routes are exposed. | Add current-member authorization and privacy projection tests. |
| SEC-02 | Critical | Google audience validation is optional/fail-open on empty configuration. | Fail startup without explicit allowed audiences; add negative-token tests. |
| SEC-03 | High | Host can impersonate another member's ballot/chat through the shared owner-or-host helper. | Use action-specific authorization; ballot/chat owner-only unless an auditable delegation exists. |
| DATA-01 | Critical | Completed history is deleted with 24-hour sessions. | Separate durable outing/ledger records before any LTF shadow run. |
| DATA-02 | Critical | No concurrency/idempotency semantics. | Add aggregate versions, unique idempotency keys, atomic completion/ledger transaction. |
| FAIR-01 | Critical | “Regret” is max-minus-median, not feasible counterfactual avoidable regret. | Rename current metric and implement a formally versioned reference oracle. |
| FAIR-02 | High | Driver burden mixes passengers' aggregate penalties into driver burden. | Define individual burden vector by role; retain group objective separately. |
| FAIR-03 | High | No stable identity/group/repeated-outing model. | Introduce User/Group/Membership/Outing/ParticipantSnapshot. |
| OPT-01 | High | Non-admissible assignment pruning and silent state truncation. | Correct bound or label heuristic; expose state/gap/truncation diagnostics and tests. |
| OPT-02 | High | Order-dependent threshold comparator. | Use an explicit total/lexicographic order or Pareto selector with deterministic tie-break. |
| OPT-03 | High | Venue prefilter ignores pending passenger geography; uniform penalty is inert. | Add pending-demand lower-bound cost and shortlist-recall tests. |
| OPT-04 | High | Straight-line walking feasibility and optimistic merged risk/access. | Use pedestrian-network feasibility or explicitly bounded approximation; preserve provenance per stop. |
| OPT-05 | High | Stop access penalty influences heuristic ordering but is omitted from final generalized cost. | Define one versioned objective and test that search/evaluation use the same terms. |
| BENCH-01 | High | Transformed public benchmarks are not canonical and lack statistical/provenance rigor. | Version protocol/manifests; add repeated temporal scenarios and exact-small oracle. |
| API-01 | High | Synchronous benchmark GET with client filesystem root. | Admin-only queued POST using allow-listed dataset IDs. |
| DEP-01 | High | Known high-severity .NET and npm production dependency findings. | Upgrade/test dependencies before deployment; add CI vulnerability gate. |
| DOMAIN-01 | High | Host inferred by earliest member; duplicate auth subjects allowed. | Explicit host membership ID and unique session/group membership constraints. |
| DOMAIN-02 | Medium | Failed state is terminal; leave behavior is notification-only. | Model recoverable job attempt and real membership/participation transitions. |
| DATA-03 | High | Unversioned JSON DTO snapshots; length mismatch for winning venue ID. | Version immutable plan snapshots and normalize essential plan facts. |
| API-02 | Medium | Untyped realtime payloads/manual frontend contracts. | Generate contracts or add schema compatibility tests; use typed events. |
| OPS-01 | Medium | No tracing/metrics/experiment provenance; process-local limits/cache fallback. | Add correlation, structured optimization telemetry, distributed controls where needed. |
| CLEAN-01 | Low | Dead contradictory algorithms, unused constants/packages, duplicated scorer logic. | Characterize, then delete/quarantine to reduce semantic ambiguity. |

## 12. Recommended target modular boundary

Use one deployable modular monolith with explicit module schemas/namespaces and contracts:

| Module | Owns | May depend on |
|---|---|---|
| **Identity & Groups** | `UserProfile`, `Group`, `GroupMembership`, roles, consent, privacy defaults. | Shared kernel only. |
| **Outings & Participation** | `Outing`, state/version, `ParticipantSnapshot`, availability, mobility/accessibility constraints, attendance/outcome lifecycle. | Identity IDs; shared kernel. |
| **Collective Decision** | Versioned candidate set, preference/vote ballot, deterministic social-choice policy, decision outcome and explanation. | Outing snapshot; candidate summaries. |
| **Mobility Planning** | Immutable `PlanningProblem`, policy, route-pool generation, assignment/stops/routes, plan revisions, diagnostics. | Outing snapshot; integration ports. No EF aggregates. |
| **Fairness Ledger** | Burden definition/policy, feasible references, `BurdenObservation`, `DebtAccount`, immutable `DebtTransaction`, compensation rationale. | Final plan/outcome events and stable group-member IDs. Never depends on Session. |
| **Experiments & Evaluation** | Scenario/manifest, algorithm registry, run/seed/version, metrics, raw artifacts, statistics, exact-small oracle. | Read-only planning/fairness contracts. Separate runtime authorization. |
| **Integrations** | Google, Mapbox, traffic, Groq, Redis, clock, external solver runners. | Implements ports; contains no policy definitions. |
| **Delivery** | Versioned HTTP contracts, SignalR projections, Next.js UI. | Application facades only. |

Shared kernel should be small: strongly typed IDs, `Coordinate`, time/duration units, money if introduced, result/error primitives, and an injected clock. Do not put scoring policies or mutable entities in the shared kernel.

### 12.1 Core target records/aggregates

Minimum durable model:

- `Group(Id, Name, Version, FairnessPolicyId, CreatedAt)`
- `GroupMembership(Id, GroupId, UserId, Role, ActivePeriod, ConsentVersion)` with unique active membership per user/group
- `Outing(Id, GroupId, SequenceNo, Status, Version, ScheduledWindow, DecisionPolicyId, PlanningPolicyId)`
- `ParticipantSnapshot(OutingId, MembershipId, origin precision/provenance, role, constraints/preferences)`
- `CandidateSetRevision` and `VenueCandidateSnapshot`
- `Ballot` / `CollectiveDecision` with deterministic tie rule
- `PlanRevision` plus normalized assignments/stops/member burden vector and versioned opaque full snapshot
- `BurdenPolicyVersion` and `ReferenceComputation`
- `BurdenObservation(OutingId, MembershipId, predicted/observed, vector, provenance)`
- `DebtAccount(GroupId, MembershipId, PolicyId, Version, BalanceProjection)`
- immutable `DebtTransaction(IdempotencyKey, OutingId, PlanRevision, MembershipId, Delta, Reason, InputHash, CreatedAt)`
- `OutcomeObservation` and compensating adjustment transaction
- `ExperimentManifest` / `ExperimentRun` with code, dataset, solver, policy, environment, seed, and artifact hashes

### 12.2 Required invariants

1. One active group membership per user; one outing participant snapshot per membership.
2. Host/organizer is an explicit membership ID, never list position/time ordering.
3. Every command carries expected aggregate version and idempotency key.
4. A plan revision references exactly one immutable outing-input revision, candidate-set revision, planning-policy version, route-data snapshot/bucket, and algorithm version.
5. Every `NeedsPickup` participant appears exactly once in a feasible assignment; every route and stop is consistent with that assignment; capacity/access/time constraints are checked centrally.
6. Every participant has an individual burden vector with named dimensions, units, missing-data semantics, and policy version. Group objective terms are not recorded as individual burden.
7. Avoidable regret compares the chosen plan with a feasible, same-input, same-cost-budget reference. Reference status (`exact`, bounded, heuristic, failed) and gap are stored.
8. Debt is derived from immutable transactions; balance is a projection. Reprocessing an outing cannot duplicate debt.
9. Outing completion, accepted final plan, predicted burden observations, and provisional debt transactions commit atomically. A later realized outcome appends adjustment transactions rather than rewriting history.
10. Operational sessions may expire only after durable completion projection succeeds; research retention/anonymization is a separate consent-aware policy.

### 12.3 Atomic completion seam

The key application operation should be conceptually:

`FinalizeOuting(outingId, expectedVersion, planRevisionId, idempotencyKey)`

Inside one PostgreSQL transaction it must validate state/revision, persist the final decision and plan, calculate or attach the versioned predicted burden/reference, append one provisional debt transaction per participant, write an outbox event, and advance outing state. SignalR and downstream experiments consume the committed outbox event. Realized travel data later issues idempotent adjustment transactions.

This boundary prevents the current failure mode in which status/snapshot can be committed while external notification or future ledger updates fail independently.

## 13. Migration seams and sequence

### Phase 0 — Characterize and secure the baseline

- Fix SEC-01/SEC-02 and dependency advisories.
- Add API/domain/integration tests and routing golden masters before refactoring.
- Rename current pseudo-regret in contracts or mark it explicitly as `maxMedianBurdenGap`.
- Record current policy constants as `planning-policy-v1` even before making them configurable.
- Add optimization diagnostics and deterministic tie-breaks.

Exit: current single-outing behavior is replayable and protected by authorization/concurrency tests.

### Phase 1 — Extract the pure planning kernel

- Map `Session` to immutable `PlanningProblem`; return immutable candidates plus diagnostics.
- Move routing DTOs/contracts out of `Application.UseCases`; separate individual burden from group score.
- Keep current planner implementation behind `PlannerV1`; do not change algorithm and architecture simultaneously.

Exit: identical fixture inputs produce stable versioned outputs without EF/HTTP/provider objects.

### Phase 2 — Introduce stable groups and outings beside sessions

- Create identity/group/membership and outing/participant snapshot tables.
- On current create/join, dual-write through one application transaction or create a compatibility projection.
- Add explicit host membership and unique subject constraints; migrate existing active sessions best-effort.

Exit: two outings by the same group resolve to the same membership IDs.

### Phase 3 — Normalize plan/decision history

- Persist candidate/input/plan revisions and normalized assignments/burdens while retaining versioned JSON for audit.
- Replace plurality's implicit tie with a versioned decision policy.
- Stop deleting durable outing/plan evidence with session cleanup.

Exit: every completed outing can be replayed and explained after the collaboration session expires.

### Phase 4 — Fairness ledger in shadow mode

- Implement burden-policy version, feasible reference oracle, avoidable regret, debt transactions, and outcome adjustment.
- Calculate LTF recommendations without affecting user-visible selection; compare to PlannerV1.
- Validate invariants, idempotency, churn/absence/new-member policies, and privacy/retention.

Exit: shadow recomputation is deterministic, balances reconcile from transactions, and reference quality/gaps are reported.

### Phase 5 — Debt-aware selection and experiments

- Add cost-first, instant-fair, round-robin, max-min cumulative, and Myopic-DARS policies to a registry.
- Run registered multi-seed 20–50-outing scenarios, exact-small checks, ablations, sensitivity, CI/effect-size analysis, and semi-real road-network experiments.
- Gate any user-facing DARS rollout behind policy explanation, group consent, cost budget, and rollback flag.

Exit: scientific claims are supported by a frozen manifest/artifact set and production behavior remains reversible.

## 14. Reuse vs. replace summary

Reuse after extraction:

- Coordinate calculations and weighted search-center idea.
- Google/Mapbox/Groq ports and resilience/caching patterns.
- Stop candidate families, shared-destination route optimizer, route-pool/assignment search structure.
- Common route DTO concepts, map polyline rendering, realtime notification pattern.
- EF/MediatR modular-monolith foundation.
- Benchmark scenarios, native solver bridges, raw-run UI, weakness reporting.

Replace or redesign:

- `Session` as the owner of history/fairness.
- Host inference and browser member-ID identity.
- Unversioned JSON snapshots as the only plan record.
- Current pseudo-regret and mixed driver burden semantics.
- Hard-coded/unversioned policy constants and order-dependent selection.
- Public benchmark claims/protocol without canonical semantics, provenance, and statistics.
- Synchronous public benchmark endpoint and client-controlled filesystem root.

The safe thesis architecture is therefore an **evolution around the current optimizer, not a rewrite of the optimizer and not an extension of the expiring Session aggregate**.
