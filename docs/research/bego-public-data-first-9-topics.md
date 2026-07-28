# BeGo / OptiGo: 9 research topics selected by a public-data-first audit

Date: 2026-07-16
Workspace: `E:\Code\BeGo`

## 1. Executive verdict

**BeGo-LTF should not be the primary thesis.** Its central empirical claim—fairness improves across repeated outings for the same real group—cannot currently be validated with a strong public dataset containing persistent real groups, multiple decisions per group, per-member burden and realized outcomes. Artificial histories remain useful for invariant and mechanism tests, but a synthetic-only result would not establish external validity.

The replacement search therefore applied one non-negotiable rule:

> The public dataset must contain the real observations needed for the topic's primary claim. A real dataset used merely to calibrate synthetic requests, or real users randomly assembled into synthetic groups, does not pass.

After re-auditing the 170-paper full-text corpus, reading ten additional full PDFs plus one open full-text paper, and verifying official dataset pages in the browser, nine topics survive. All nine can be evaluated without collecting a private dataset. The strongest overall choice is **T1, Context-Adaptive Shared-Stop Dynamic Ride-Pooling**, because it preserves BeGo's distinctive destination/pickup/routing core and now has an unusually complete 2026 public Manhattan benchmark.

If the intended thesis identity is different:

- choose **T4** for a pure operations-research/algorithmic thesis with optimality gaps and Pareto certificates;
- choose **T5** for a more novel human-centered/accessibility thesis with real labels from mobility-aid users;
- choose **T6** for an ML-heavy thesis with clean cross-city public trajectory evaluation.

## 2. What was audited

- **170 existing full papers:** 90 in the original corpus and 80 in the extension. Every extracted full text was remapped successfully; 79 mention at least one targeted public-data family.
- **Additional targeted reading:** 10 valid full PDFs, 188 pages and about 106,000 extracted words, plus the complete open HTML of the rejected random-group POI paper.
- **Official data verification:** Zenodo, Mendeley Data, NYC TLC, Chicago/Data.gov, UCI, Microsoft Research, Project Sidewalk/Hugging Face, the CHI authors' GitHub repository, SNAP, Yelp, Citi Bike and GTFS/Data.gov pages.
- **Current-system audit:** OptiGo already has route-aware venue search, passenger–driver assignment, shared-stop generation, exact/heuristic stop ordering, route pools, fairness metrics, native OR-Tools/PyVRP bridges, a .NET API and a Next.js collaborative UI. Its current public-benchmark transformation does not preserve all DARP/PDPTW semantics, so it must not be used unchanged for thesis claims.

Supporting evidence:

- [170-paper public-dataset audit](sources/public-dataset-corpus-audit.md)
- [Additional full-paper evidence matrix](bego-public-data-targeted-paper-evidence.md)
- [Original 90-paper review](bego-90-paper-review-and-8-topics.md)
- [Additional 80-paper evidence](sources/extra-80-paper-evidence.md)
- [Current-system audit](sources/current-system-audit.md)

## 3. Verified public data inventory

| Dataset | Verified public content | Access/terms | What it can prove | What it cannot prove |
|---|---|---|---|---|
| [Manhattan DARP 2026](https://zenodo.org/records/20452171) | 24 real NYC TLC days from 2015–2016; raw trips; virtual stops; taxi zones; OSRM travel-time matrix; 2 h, 4 h and 16 h solver-ready instances; fleet files for 500–3,000 vehicles | Open, CC BY 4.0, DOI `10.5281/zenodo.20452171`, 760.7 MB archive | Dynamic dispatch, shared stops, batching, transfer and fleet algorithms on real observed requests | Actual rider willingness to walk/share/transfer; social or demographic fairness |
| [NYC TLC Trip Records](https://www.nyc.gov/site/tlc/about/tlc-trip-record-data.page) | Monthly Parquet from 2009 onward; pickup/drop-off time and zone, trip distance, fare, passengers; taxi-zone files and dictionaries | Official public data; TLC warns completeness/accuracy are not guaranteed | Demand forecasting, OD replay, temporal distribution shift, cross-month robustness | Unobserved rejected requests, user identity, stated preference |
| [Chicago Taxi Trips 2024+](https://catalog.data.gov/dataset/taxi-trips-2024) | Official trip records with downloadable CSV/JSON; older 2013–2023 collection is also public | Public; privacy rounding/suppression documented | External-city demand and dispatch validation | Exact curb coordinates or rider identity |
| [DARP with meeting points](https://data.mendeley.com/datasets/h5392z6csr) | 12 Cordeau-derived instances with explicit pickup/delivery meeting-point roles, time windows, service duration and capacity | CC BY 4.0, DOI `10.17632/h5392z6csr.1` | Exact/heuristic DARP-MP feasibility, cost, runtime, gap and Pareto quality | Real human walking preference or observed adoption |
| [Li & Lim PDPTW](https://www.sintef.no/projectweb/top/pdptw/li-lim-benchmark/) | Standard 100–1,000 task pickup/delivery/time-window instances and best-known results | Public benchmark; check source terms when redistributing | Scalability and general PDPTW comparison | Urban realism and accessibility |
| [RampNet](https://huggingface.co/datasets/projectsidewalk/rampnet-dataset) | 214,385 rows; 214,376 panoramas; 849,895 labels; train 150k, validation 42.9k, test 21.4k; NYC, Portland and Bend; manual gold set | MIT; streamable via Hugging Face; full files about 462 GB | Curb-ramp perception/detection and city transfer | Complete sidewalk passability or a user's route preference by itself |
| [Accessibility for Whom](https://github.com/makeabilitylab/accessibility-for-whom) | Data/code from 190 mobility-aid users, five groups and 52 barrier images; routing prototypes and analysis | Public MIT repository; CHI 2025 | Group-specific barrier severity and preference cost | All disabilities, all cities, or realized route choice |
| [Porto Taxi trajectories](https://archive.ics.uci.edu/dataset/339/taxi+service+trajectory+prediction+challenge+ecml+pkdd+2015) | 1,710,671 trips from 442 taxis; GPS every 15 seconds; official train/test/solution files | CC BY 4.0, DOI `10.24432/C55W25` | ETA, trajectory and taxi-ID personalization | Modern traffic conditions or other-city generalization alone |
| [Microsoft T-Drive](https://www.microsoft.com/en-us/research/publication/t-drive-trajectory-data-sample/) | One week, 10,357 taxis, about 15 million GPS points and 9 million km; direct downloads and guide | Public Microsoft Research download | External-city trajectory/ETA robustness | Destination recommendations or exact passenger requests |
| [Gowalla SNAP](https://snap.stanford.edu/data/loc-gowalla.html) | 196,591 users, 950,327 social edges and 6,442,890 timestamped check-ins | Public direct downloads; cite the source paper and obey SNAP terms | Real next check-in prediction and temporal/city transfer | Real group decisions or personality |
| [Yelp Open Dataset](https://business.yelp.com/data/resources/open-dataset/) | 6,990,280 reviews, 150,346 businesses, 11 metropolitan areas, photos/check-ins/attributes; documented JSON download | Educational use; 4.35 GB compressed JSON package | Real timestamped revealed business choices and cross-city POI metadata | A true friendship/outing group or unobserved visits |
| [MTA GTFS static data](https://catalog.data.gov/dataset/mta-general-transit-feed-specification-gtfs-static-data) | Stops, routes, trips and schedules for subway, bus, LIRR and Metro-North; direct ZIP resources | Official public GTFS | Schedule-feasible transfer/feeder planning | Historical service realization unless an archived/realtime feed is also used |
| [OpenStreetMap](https://www.openstreetmap.org/copyright) | Road/pedestrian graph and POI geometry | ODbL; attribution and share-alike obligations apply to derivative databases | Common routing graph and candidate nodes | Ground-truth travel time or accessibility completeness |

No topic below depends on Kaggle re-uploads or an inaccessible author-only dataset as its primary source.

## 4. Ranking

Scores are 1–5 and reflect data strength, academic contribution, fit to current BeGo, end-to-end feasibility in roughly ten months, and clarity of falsifiable evaluation. They are comparative judgments, not experimental results.

| Rank | Topic | Data | Contribution | BeGo fit | Feasibility | Proof clarity | Overall |
|---:|---|---:|---:|---:|---:|---:|---:|
| 1 | T1 Context-Adaptive Shared-Stop Dynamic Ride-Pooling | 5.0 | 4.6 | 5.0 | 4.7 | 5.0 | **4.86** |
| 2 | T4 Certified Anytime Pareto DARP with Meeting Points | 4.8 | 4.8 | 4.8 | 4.4 | 5.0 | **4.76** |
| 3 | T5 Mobility-Aid-Specific Accessible Pickup and Routing | 4.8 | 4.9 | 4.5 | 4.3 | 4.8 | **4.66** |
| 4 | T2 Real-Trace Adaptive Matching-Time Control | 5.0 | 4.5 | 4.6 | 4.5 | 4.7 | **4.66** |
| 5 | T6 Cross-City Calibrated ETA for Robust Route Choice | 4.9 | 4.4 | 4.2 | 4.5 | 4.8 | **4.56** |
| 6 | T7 Cross-City Decision-Focused Demand and Dispatch | 5.0 | 4.5 | 4.3 | 4.1 | 4.7 | **4.52** |
| 7 | T3 Density- and Accessibility-Gated Transfer Pooling | 4.8 | 4.9 | 4.6 | 3.7 | 4.6 | **4.52** |
| 8 | T8 GTFS-Synchronized Semi-On-Demand Transit Feeders | 4.6 | 4.7 | 4.4 | 4.0 | 4.4 | **4.42** |
| 9 | T9 Route-Feasible Individual Next-POI Recommendation | 4.6 | 4.3 | 4.0 | 4.4 | 4.5 | **4.36** |

## 5. The nine topics

### T1. BeGo-CAST: Context-Adaptive Shared-Stop Dynamic Ride-Pooling

**Simple description.** Instead of always collecting each passenger at their door or using one fixed walking radius, the system chooses a small set of shared pickup stops and adapts each person's allowed walk and each area's stop density to the current demand. It aims to reduce vehicle detours without violating individual walking/time constraints.

**Research question.** Can adaptive shared-stop selection reduce vehicle time/VMT on real taxi requests while keeping served demand, p95 waiting, p95 walking and worst-passenger detour non-inferior to door-to-door and fixed-radius pooling?

**Contribution.** A constrained, context-adaptive stop-pooling policy that jointly chooses candidate stops, passenger-to-stop assignment, vehicle assignment and route. The novelty is not another weighted score; it is an explicit feasible-set model with per-request walk budgets, density-aware stop candidates, and a Pareto/lexicographic selector that prevents cheap solutions from hiding severe individual burden.

**Primary data and split.** [Manhattan DARP 2026](https://zenodo.org/records/20452171). Sort its 24 dates chronologically: first 14 days train/calibration, next 5 validation, last 5 untouched test. Report separately on 2 h, 4 h and 16 h windows and fleet sizes. External algorithmic validation uses the 12 [Mendeley DARP-MP](https://data.mendeley.com/datasets/h5392z6csr) instances with no retraining.

**Method.**

1. Preserve the benchmark's real timestamps, stop IDs, fleet sizes and travel-time matrix.
2. Generate feasible virtual-stop sets from the supplied stops, not arbitrary Cartesian points.
3. Estimate local request density and travel-time uncertainty from past days only.
4. Allocate an individual walk budget under hard maximums; never infer willingness from the observed taxi trip.
5. Solve joint stop/vehicle/route selection with an exact CP-SAT model on small instances and ALNS/large-neighborhood repair on large instances.
6. Use lexicographic constraints: maximize served requests, enforce hard walk/wait/detour limits, then minimize vehicle cost and tail burden. Generate the Pareto frontier instead of reporting one hand-picked scalar weight.

**Baselines.** Door-to-door rolling insertion; nearest virtual stop; fixed 150/300/500 m walking radii; static shared stops; current OptiGo; OR-Tools CP-SAT on small instances; a no-pooling individual-taxi lower reference.

**Metrics.** Served rate, vehicle operating time/VMT, empty VMT, mean/p95 wait, walk and detour, maximum burden, Gini only as a secondary descriptive metric, runtime/request, timeout rate and optimality gap on small cases. Also report the entire cost–walk–tail-burden Pareto hypervolume.

**Falsifiable success gate.** On untouched days, at least 3% lower vehicle operating time than the strongest fixed-radius baseline, with the 95% day-block-bootstrap CI excluding zero, while served rate drops by at most 0.5 percentage points and p95 wait/walk/detour remain inside preregistered bounds. If only a tuned scalar objective improves, the thesis fails.

**Ablations.** Fixed versus adaptive walk budget; no density feature; no uncertainty margin; no shared stop; scalar versus lexicographic/Pareto selector; exact versus ALNS; current straight-line access versus pedestrian-network access.

**BeGo implementation fit.** Reuse `StopCandidateGenerator`, `HybridOutingRoutePlanner`, route-pool logic, map UI, benchmark UI and solver bridges. Replace mutable `Session` input with immutable `PlanningProblem`; move all weights/limits into a versioned policy; fix the benchmark parser so time windows, capacity and pickup-delivery pairing remain intact.

**Main risks/claim boundary.** The data do not reveal real willingness to walk. Therefore the valid claim is “operational gains under explicit walking budgets,” not “riders prefer the proposed stops.” Run sensitivity over 100–800 m and report every constraint violation.

### T2. BeGo-WAIT: Real-Trace Adaptive Matching-Time Control

**Simple description.** The system learns when to wait a few seconds for more compatible requests and when to dispatch immediately. Waiting can improve pooling, but excessive waiting harms users; the decision is learned and tested by chronological replay of real request timestamps.

**Research question.** Does a safety-constrained adaptive batch trigger dominate fixed intervals and queue thresholds across varying demand density without generating requests from a fitted stochastic distribution?

**Contribution.** A real-trace, uncertainty-aware matching-time controller whose action is only `match now` or `wait Δt`; the downstream candidate construction and matching solver are held identical for all baselines. This isolates the scientific contribution and corrects the calibrated-synthetic limitation of recent work.

**Data/split.** Same 14/5/5 [Manhattan DARP](https://zenodo.org/records/20452171) day split as T1. Use real timestamps in event replay; no Poisson resampling. Test separate low/medium/high density periods, 2 h/4 h/16 h windows and fleet sizes 500–3,000.

**Method.** Contextual bandit or conservative offline RL over queue length, spatial entropy, estimated shareability, fleet availability and age of the oldest request. A safety shield forces dispatch before maximum wait. Use doubly robust offline evaluation only for tuning; the final result is produced by full simulator replay.

**Baselines.** Immediate dispatch; fixed 5/10/15/20/30/60 s; queue thresholds; oldest-request threshold; the rule from Bao et al.; oracle grid search per validation period; rolling insertion without batching.

**Metrics.** Served/matched/pooled rates, mean/p95 wait, detour, vehicle time/VMT, occupancy, cancellations induced by the explicit wait limit, runtime and policy violations. Include calibration of predicted pooling gain and regret to the per-window oracle.

**Success gate.** At least 5% lower mean wait-plus-detour or 3% lower vehicle time than the best fixed/threshold baseline, with served rate non-inferior within 0.5 percentage points and zero hard-wait violations on test days. Improvements must hold in at least four of five test days, not only in pooled aggregate.

**Ablations/risks.** Remove spatial entropy, uncertainty or shield; compare bandit/RL with interpretable decision tree. The system cannot claim real cancellation preference because cancellations are not observed; the wait limit is a service constraint, not a behavioral model.

**Implementation.** Add an event-replay clock and policy interface around the same candidate factory. This topic needs less production UI work than T1 and is feasible even if the final policy is a compact decision tree rather than deep RL.

### T3. BeGo-XFER: Density- and Accessibility-Gated One-Transfer Ride-Pooling

**Simple description.** A passenger may change once from one shared vehicle to another, but only when request density makes the transfer worthwhile and the transfer location satisfies time, safety and curb-accessibility constraints.

**Research question.** Under what observable conditions do single transfers create a real operational benefit, and can a learned gate avoid the low-density cases where transfers only add inconvenience?

**Contribution.** Joint transfer-gating and transfer-point selection. It combines the 2025 exact/heuristic transfer algorithm's open gap—safety/accessibility and time-dependent travel—with real Manhattan requests and public curb-ramp evidence. The output includes an interpretable density threshold and uncertainty interval, not just average improvement.

**Data/split.** [Manhattan DARP](https://zenodo.org/records/20452171) 14/5/5 chronological split; OSM virtual stops; [RampNet/Project Sidewalk](https://huggingface.co/datasets/projectsidewalk/rampnet-dataset) for an accessibility score where imagery exists. Primary operational analysis must also run without RampNet so incomplete imagery cannot silently exclude neighborhoods.

**Method.** Generate feasible single-transfer insertions; learn a conservative gate from density, route overlap and fleet state; rank transfer nodes by detour plus an accessibility constraint; solve exact on sampled small windows and CH-rank/ALNS heuristic at city scale. Bridge the authors' public KaRRiT implementation for a faithful baseline where practical.

**Baselines.** No transfer; transfer at nearest/high-centrality hub; exact KaRRiT on small instances; CH-rank heuristic; transfer everywhere; density-only gate; accessibility-blind gate.

**Metrics.** Vehicle operating time, occupancy, served rate, passenger wait/trip time, number and duration of transfers, missed/late handoffs, accessible-node coverage, runtime/request and optimality gap. Stratify by request density and trip length.

**Success gate.** In dense test windows, at least 3% lower vehicle time than no-transfer while p95 passenger generalized time rises no more than 2%; across all windows the gate must have no worse vehicle time than no-transfer by more than 1%. Report coverage separately when accessibility labels are missing.

**Risk/feasibility.** This is the hardest topic. Keep one transfer, fixed vehicle capacity and offline travel-time matrices. Do not attempt multi-transfer, live traffic and city-wide computer vision simultaneously. A valid result may be a negative finding that transfers are beneficial only above a reproducible density threshold.

### T4. BeGo-PARETO-MP: Certified Anytime Pareto Optimization for DARP with Meeting Points

**Simple description.** Build a solver that quickly returns a good feasible plan, continues improving it, and reports how far it may still be from the best solution. Instead of hiding cost and fairness in one score, it returns a certified frontier between vehicle cost, passenger walking and worst burden.

**Research question.** Can an anytime hybrid exact–heuristic solver obtain a better Pareto frontier and smaller certified gaps than standard solvers under the full DARP-MP semantics?

**Contribution.** A semantics-preserving epsilon-constraint/lexicographic formulation plus ALNS warm starts and CP-SAT/MIP bounds. It exposes feasibility certificates, lower bounds, timeouts, explored states and a reproducible archive. This directly fixes the current BeGo benchmark's missing semantics and diagnostics.

**Data/split.** [Mendeley DARP-MP](https://data.mendeley.com/datasets/h5392z6csr) is primary. Predeclare `a2-*` for tuning, `a3-*` for validation and `a4-*` as untouched scale-generalization test. [Li & Lim](https://www.sintef.no/projectweb/top/pdptw/li-lim-benchmark/) clustered/random/mixed families test general pickup-delivery scalability. Manhattan 2 h windows provide realism after the benchmark result is established.

**Method.** Preserve paired pickup/delivery nodes, time windows, service time, vehicle capacity, maximum ride time and allowed meeting-point roles. Use ALNS to construct warm starts; CP-SAT/MIP supplies bounds on small/medium instances; epsilon-constraint enumeration builds nondominated solutions. Every run exports policy hash, seed, wall-clock trace, incumbent, lower bound and gap.

**Baselines.** Published/best-known values where available; OR-Tools with an equivalent model; current OptiGo; cost-only ALNS; NSGA-II/MOEA; weighted-sum scalarization; exact CP-SAT/MIP on tractable cases.

**Metrics.** Feasibility, total distance/time, maximum ride/walk burden, p95 burden, hypervolume, epsilon indicator, number of nondominated points, time-to-first-feasible, anytime primal integral, final optimality gap and memory/runtime.

**Success gate.** Zero semantic/feasibility violations; hypervolume statistically above the strongest heuristic on the untouched `a4-*` family under equal time; median optimality gap no worse than the exact baseline and time-to-first-feasible below the interactive budget. A “fairer” point is valid only if it is nondominated under the preregistered objectives.

**Ablations.** No warm start, no bounds, no Pareto archive, weighted sum only, current lossy mapping, each destroy/repair operator removed. Use paired bootstrap by instance and performance profiles, not a single mean.

**Risks.** The public Mendeley suite is small, so it cannot be the only scale result; Li & Lim and Manhattan must test transfer and realism. A gap certificate is valid only for the exact same semantics/objective, so bounds from a simplified model may never be reported as a certificate for the full problem.

**Implementation.** This is the cleanest path if the goal is an algorithm paper. Most frontend work can be limited to a Pareto/anytime visualization; the major engineering task is extracting a pure solver kernel and replacing the 2,708-line benchmark service with typed dataset adapters.

### T5. BeGo-ACCESS: Mobility-Aid-Specific Accessible Pickup and Walking Routing

**Simple description.** A pickup point that is a 200 m walk for one person may be unusable for another. The system detects curb ramps/barriers and chooses different pedestrian paths or vehicle pickup points for cane, walker, scooter, manual-wheelchair and motorized-wheelchair users.

**Research question.** Does group-specific barrier modeling reduce exposure to barriers judged impassable by the target mobility-aid group without imposing excessive detour or excluding poorly mapped areas?

**Contribution.** A three-layer, auditable pipeline: curb-ramp perception, group-specific barrier-cost calibration from real user ratings, and uncertainty-constrained route/pickup selection. The contribution is evaluated at every layer so an image-model gain cannot masquerade as a routing gain.

**Data/split.** Use [RampNet](https://huggingface.co/datasets/projectsidewalk/rampnet-dataset)'s official 150k/42.9k/21.4k splits and manual 1,000-panorama gold set. Use participant-disjoint, mobility-group-stratified five-fold validation for the 190-person [CHI dataset](https://github.com/makeabilitylab/accessibility-for-whom); add leave-image-category-out analysis. Build the pedestrian graph from OSM/Seattle open sidewalks and overlay only labels available before each test route.

**Method.** Fine-tune or reuse RampNet with calibrated detection probabilities; fit a hierarchical/mixed-effects group-specific passability model; propagate uncertainty to edge costs; solve constrained shortest path and pickup-location selection with a hard chance constraint on severe barriers. Offer an abstain/unknown state when map evidence is insufficient.

**Baselines.** Shortest path; universal accessibility penalty; Project Sidewalk severity only; Wheelmap/OSM wheelchair tags; the CHI prototype rule; group-specific model without uncertainty; oracle manual labels on the small gold set.

**Metrics.** Detection mAP/F1/calibration; passability log loss/AUC and group calibration; route severe-barrier exposure, expected inaccessible edges, path-length/time overhead, no-route/abstention rate and geographic coverage. Report each mobility group, city and label-availability stratum.

**Success gate.** On the manual gold and held-out participants, at least 20% lower severe-barrier exposure than shortest path and a significant improvement over the universal-penalty baseline, while median route overhead stays below 15% and no group has a worse severe-barrier rate. Coverage must be reported, not imputed as success.

**Risks/claims.** The five groups are not interchangeable with every disabled traveler. RampNet detects curb ramps, not every obstacle. The valid claim is improved route selection for represented barrier types under measured coverage; user-study deployment is optional validation, not necessary for the primary public-data result.

**Implementation.** Reuse BeGo's stop-candidate pipeline and map UI, replace Haversine walk feasibility with a pedestrian graph, add accessibility profiles and uncertainty badges. Stream a city subset of RampNet rather than downloading the full 462 GB package.

### T6. BeGo-XETA: Cross-City Uncertainty-Calibrated ETA for Robust Route Choice

**Simple description.** Predict not only “the trip takes 18 minutes” but a calibrated interval, then choose a route/plan that is less likely to make the group late. Test whether a model trained in Porto transfers to Beijing taxi traces and vice versa.

**Research question.** Do calibrated, route/driver-aware ETA distributions reduce downstream route-choice regret and late-arrival rate under temporal and city shift compared with point ETA models?

**Contribution.** Cross-city conformal/quantile ETA with a decision-focused route-selection layer. Unlike a paper evaluated only by MAE on private Shanghai data, the thesis preregisters both prediction and downstream decision metrics on two downloadable datasets.

**Data/split.** [Porto UCI](https://archive.ics.uci.edu/dataset/339/taxi+service+trajectory+prediction+challenge+ecml+pkdd+2015): use chronological training data, reserve the final pre-challenge months for validation and use the official test/solution files as test; never use the published solution labels for tuning. Hold out taxis as a second test for unseen-driver generalization. [T-Drive](https://www.microsoft.com/en-us/research/publication/t-drive-trajectory-data-sample/) is the untouched external-city test after map matching and quality filtering. Reverse-transfer is secondary.

**Method.** Map-match with FMM/Valhalla; build segment/time/weather-free features available in both cities; train historical-average, tree and Transformer/DeepTTE-style models; add quantile loss and split-conformal calibration; feed ETA distributions into chance-constrained route/meeting-plan selection.

**Baselines.** Free-flow/OSRM; historical segment average; XGBoost/LightGBM; Transformer; DeepTTE/ProbTTE/CoDriver-style implementation; point model plus fixed safety margin; oracle realized-time selector.

**Metrics.** MAE/RMSE/MAPE by trip length and hour; interval coverage and width/Winkler score; calibration error; chosen-route regret, on-time probability, worst-member lateness and robustness under GPS dropout. Use paired trip bootstrap and Diebold–Mariano tests for temporal errors.

**Success gate.** Achieve 90% interval coverage within ±2 percentage points in both cities, lower interval width than the strongest calibrated baseline, and at least 10% lower downstream route-choice regret than the best point-ETA selector. Prediction-only improvement without decision improvement does not satisfy the thesis.

**Risk/claim boundary.** T-Drive is older and only one week; it is an external-shift stress test, not proof of present-day traffic accuracy. Stable taxi IDs permit a driver-style ablation, but the primary model must work for unseen IDs.

### T7. BeGo-XDISPATCH: Cross-City Decision-Focused Demand Forecasting and Fleet Dispatch

**Simple description.** Forecast demand by zone and time, but train/tune the forecast for what matters operationally: serving more real requests with less empty driving. Validate first in New York and then without full retraining in Chicago.

**Research question.** Does optimizing forecasts for downstream dispatch loss outperform the most accurate conventional forecast when evaluated by served demand, wait and deadhead VMT under city and temporal shift?

**Contribution.** A distributionally robust, decision-focused forecast-to-dispatch pipeline with an external-city test. It also reports spatial tail performance without pretending that taxi zones reveal protected demographic fairness.

**Data/split.** [NYC TLC](https://www.nyc.gov/site/tlc/about/tlc-trip-record-data.page) Jan–Jun 2023 training, Jul–Sep validation, Oct–Dec untouched temporal test; repeat on 2024/2025 as rolling-origin robustness. [Chicago Taxi Trips](https://catalog.data.gov/dataset/taxi-trips-2024) 2023 or 2024 is the external-city test using privacy-compliant grid/zone aggregation. OSM supplies travel cost. Freeze feature definitions before Chicago evaluation.

**Method.** Aggregate 15-minute zone OD demand; construct a common spatial graph; train forecasting baselines and a probabilistic model; optimize a differentiable/min-cost-flow surrogate or use predict-then-optimize gradients; solve receding-horizon repositioning with uncertainty sets; replay every observed request in timestamp order.

**Baselines.** Historical average, seasonal naive, XGBoost, STGCN, DCRNN, Graph WaveNet/TFT; no reposition, move-to-nearest-demand, forecast-proportional, myopic min-cost flow and oracle future-demand upper bound.

**Metrics.** Forecast MAE/RMSE/WAPE/CRPS; operational served rate, mean/p95 wait, deadhead VMT, occupied VMT, utilization and runtime; worst-decile zone service deficit and city-transfer degradation. Compare equal fleets and equal compute budgets.

**Success gate.** At least 3% lower deadhead VMT or 2 percentage points higher served rate than the strongest predict-then-optimize baseline on NYC temporal test, without worsening p95 wait; retain at least half of that relative gain on Chicago. A lower forecasting MAE alone is not success.

**Risks.** Chicago locations are rounded/suppressed, so use zones and matched spatial resolution. Neither dataset contains unserved demand; replay measures performance on observed requests only and must be described as such.

**Implementation.** Add a research/offline Python forecasting module and keep the .NET planning API as the deterministic replay/solver host. The dashboard can visualize forecasts, fleet movements and operational metrics.

### T8. BeGo-TRANSITSYNC: GTFS-Synchronized Semi-On-Demand Transit Feeders

**Simple description.** Group nearby taxi requests into a small feeder vehicle, choose the best transit station and time the trip to a specific scheduled train/bus so passengers do not miss the connection.

**Research question.** Can joint station choice, pooling and schedule synchronization reduce vehicle travel while maintaining connection reliability compared with direct taxi and nearest-station heuristics?

**Contribution.** A schedule-feasible feeder DARP that models missed-connection risk explicitly. The paper claim is operational potential on observed taxi ODs and a versioned GTFS snapshot—not actual mode adoption.

**Data/split.** [NYC TLC](https://www.nyc.gov/site/tlc/about/tlc-trip-record-data.page) trips whose origins/destinations lie in defined station catchments are treated as observed feeder-request candidates. Archive the exact [MTA GTFS](https://catalog.data.gov/dataset/mta-general-transit-feed-specification-gtfs-static-data) ZIP and hash used. Train/calibrate on Jan–Aug, validate Sep–Oct, test Nov–Dec of a chosen TLC year. A recent-year sensitivity run checks demand drift. GTFS schedules remain a fixed counterfactual service scenario unless historical feeds are obtained.

**Method.** Map each request to feasible stations/trips; build connection time windows; jointly choose station, vehicle grouping, route and departure; include transfer buffer uncertainty; solve small windows with CP-SAT and large windows with rolling ALNS or constrained RL zonal control.

**Baselines.** Direct taxi; nearest station separately; fixed feeder route/headway; greedy pooling to one station; timetable-aware but non-pooled; nominal zonal control; exact oracle on small cases.

**Metrics.** Connection success/miss rate, passenger access/wait/in-vehicle time, generalized cost, vehicle time/VMT, served/capacity rate, transfers and runtime. Stratify peak/off-peak, catchment distance and schedule frequency.

**Success gate.** At least 5% lower vehicle time than timetable-aware non-pooled feeder with connection success non-inferior within 1 percentage point; materially lower missed-connection rate than nearest-station pooling. Direct taxi is an efficiency/service reference, not a transit-adoption baseline.

**Risks.** Taxi OD near a station does not prove that the traveler would use transit. State “counterfactual feeder suitability.” If an exact historical GTFS snapshot cannot be sourced, do not claim reconstruction of historical connection outcomes.

### T9. BeGo-ROUTEPOI: Route-Feasible Individual Next-POI Recommendation

**Simple description.** Predict a person's next destination, then rerank candidates by whether they are actually reachable within the person's time/travel budget. It extends BeGo's destination selection without inventing a group.

**Research question.** Does decision-aware reranking improve the accuracy–travel-cost frontier and reduce route regret compared with pure next-POI ranking, especially for sparse users and cross-city transfer?

**Contribution.** A two-stage recommender with a route-feasibility layer and a constrained ranking objective. The real next check-in/review is the label; OSM travel cost evaluates the operational consequence. No synthetic personality or random group is used.

**Data/split.** [Gowalla SNAP](https://snap.stanford.edu/data/loc-gowalla.html): preserve timestamps; filter with a preregistered minimum history; chronological 80/10/10 split, users/POIs in primary transductive test, plus a separate unseen-user/POI inductive test. [Yelp Open Dataset](https://business.yelp.com/data/resources/open-dataset/) is an external metropolitan-area validation using timestamped review sequences and business coordinates/categories; keep its educational-use terms.

**Method.** Train popularity/Markov/sequential/graph recommenders; generate top-N candidates; compute route distance/time and open-hour feasibility when available; rerank by constrained utility or decision-focused loss; calibrate an abstention option for sparse histories.

**Baselines.** Popularity and nearest POI; FPMC, PRME, STGCN, STAN, GETNext, STHGCN and ReHDM-style model; route-only; weighted post-hoc rerank; oracle next POI.

**Metrics.** Acc@1/5/10, MRR, NDCG; route distance/time of recommendations; feasibility rate; excess route cost/regret among hits; coverage, novelty and sparse-user performance. Plot the accuracy–route-cost Pareto frontier and test paired user bootstrap.

**Success gate.** Acc@5 non-inferior within 1 percentage point to the strongest pure recommender while reducing mean route cost of top-five recommendations by at least 10%, or a statistically larger Pareto hypervolume. Gains must reproduce on the external Yelp city split.

**Risk/claim boundary.** A review is a revealed business choice, not every physical visit. This is an individual recommendation thesis; adding random groups would invalidate the primary evidence.

## 6. Topics deliberately rejected or demoted

| Direction | Decision | Reason |
|---|---|---|
| BeGo-LTF / mobility-burden debt | Reject as primary empirical thesis | No public repeated-real-group/outcome dataset; synthetic history proves mechanics only |
| Random-group destination fairness | Reject | Groups/personality/fairness labels are generated even when check-ins are public |
| FairTalk/negotiation | Reject | No public ground-truth group negotiation/outcome corpus tied to mobility decisions |
| StableShare/payment/acceptance | Reject | Real counterfactual acceptance and payment labels are unavailable |
| Pure privacy–fairness tradeoff | Demote | Technical privacy can be measured, but behavioral group utility would remain synthetic |
| Safety-aware pickup from crash data | Demote | Crash proximity is a weak causal proxy for pickup safety and no public ranking label validates the recommended curb |
| Bike-share rebalancing | Backup only | Public trips are strong, but historical station inventory, interventions and truly lost demand are incomplete; results would depend more heavily on replay assumptions than the nine selected topics |

## 7. Common end-to-end experimental contract

Every selected topic should use the same research discipline.

### 7.1 Data contract

1. Pin raw URLs, DOI/version, download date, SHA-256 and terms/license.
2. Keep raw data immutable; create a versioned manifest for every derived table/matrix.
3. Publish preprocessing code and row counts after every filter.
4. Split by time, city, taxi/user or instance family before normalization/tuning; prohibit random row leakage.
5. Keep a data card with missingness, spatial coverage, privacy transformation and the exact claim each field supports.

### 7.2 Baseline fairness

- All algorithms receive the same requests, travel matrix, time limit, hardware class and feasibility checker.
- External solver results count only when their native core actually runs; a BeGo wrapper must not be renamed as an external algorithm.
- Tune all learnable baselines on the same validation budget. Report both published/default and fairly tuned variants where possible.
- A common independent evaluator recomputes feasibility and metrics from exported routes.

### 7.3 Statistics

- At least five seeds for stochastic learning/heuristics; deterministic solvers still run across all days/instances.
- Day/instance/user is the resampling unit, not individual dependent rows.
- Report 95% confidence intervals, paired effect sizes and Holm correction for multiple primary comparisons.
- Include performance profiles and failure/timeout rates; never average infeasible solutions as if they were valid.
- Predeclare one primary operational metric and its non-inferiority guardrails. Other metrics are secondary.

### 7.4 Reproducibility and system validation

- Containerized runner, frozen dependency lock, seed, policy hash, dataset hash, solver version and commit in every result.
- Unit/property tests for capacity, time windows, pickup-before-delivery, each request exactly once, constraint monotonicity and permutation invariance.
- Golden replay tests on small public instances and cross-check against exact solutions.
- API contract tests, PostgreSQL integration tests and a small browser E2E flow for whichever topic is productized.
- Load test reports p50/p95/p99 latency, memory and cancellation behavior separately from algorithm-quality experiments.

## 8. Ten-month implementation shape

This sequence is shared; only the model/solver in months 4–6 changes by topic.

| Month | Deliverable and exit gate |
|---:|---|
| 1 | Freeze topic/RQs/claims; download public data; checksum/license/data card; reproduce one published baseline |
| 2 | Typed dataset adapters and semantics-preserving independent evaluator; leakage tests pass |
| 3 | Extract immutable planning kernel; version policies; deterministic replay; exact small-instance oracle |
| 4 | Implement first proposed method and strongest simple baselines |
| 5 | Complete exact/heuristic or ML pipeline; produce validation-only result; no test-set access |
| 6 | Ablations, uncertainty/calibration and runtime optimization; freeze design/hyperparameters |
| 7 | Run untouched temporal/instance-family test and external-city/dataset test |
| 8 | Statistical analysis, failure analysis, sensitivity and negative-result interpretation |
| 9 | Integrate API/UI demo, provenance export and reproducible experiment package |
| 10 | Independent rerun from clean environment; write thesis/paper; audit every figure/table back to raw results |

## 9. Required changes before using the current BeGo benchmark

1. Do not map every DARP/PDPTW delivery node to a common venue. Preserve pickup–delivery pairs and their precedence.
2. Preserve time windows, service duration, capacity, maximum ride time and meeting-point role.
3. Replace degree/coordinate reinterpretations with one typed adapter per dataset.
4. Separate objective from evaluator: pure cost, passenger burden and Pareto objectives must be recomputed independently.
5. Remove order-dependent candidate selection and expose explored states, truncation, bounds and gap.
6. Use network walking rather than Haversine for accessibility/walk constraints when geographic data are involved.
7. Store raw per-run output, not only aggregate averages, and include dataset/solver/policy hashes.

## 10. Final recommendation

### Recommended thesis: T1 BeGo-CAST

It has the best combination of:

- a new, direct and unusually complete public benchmark based on real NYC trips;
- a clear scientific gap at the intersection of dynamic ride-pooling, shared stops and individual constraints;
- several exact, heuristic and current-BeGo baselines;
- end-to-end metrics that are directly computable rather than behaviorally invented;
- high reuse of the current .NET/Next.js system;
- a bounded ten-month implementation path and an honest fallback: even if the proposed adaptive method does not win, the data and independent evaluator still support a publishable negative/threshold analysis.

Choose **T4** instead if mathematical certification/Pareto optimization is preferred over a broader systems contribution. Choose **T5** if the desired identity is accessibility and human-centered computing. The remaining topics are viable, but T1/T4/T5 have the strongest balance of novelty, public evidence and thesis defensibility.
