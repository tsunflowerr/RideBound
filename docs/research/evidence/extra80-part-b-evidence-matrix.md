# BeGo-LTF extra80 / part-b — full-text evidence matrix

This matrix contains **exactly 40 papers disjoint from corpus90 and extra80/part-a**, focused on ride-pooling/DARP algorithms, uncertainty, multimodal integration, accessibility, group recommendation, explanation, and privacy.

- Full PDFs downloaded and extracted: **40/40**
- Total pages read: **884**
- Total extracted words scanned: **598,983**
- Low-text or failed PDFs: **0**
- Byte-identical PDFs inside this corpus: **0**
- Canonical-title/DOI overlap with corpus90 or part-a: **0**
- Structured method/data/result/limitations/use reviews: **40/40**
- Rendered first-page audit complete: **40/40**

## Quality rubric

- **A\***: flagship/top peer-reviewed source with unusually direct evidence.
- **A**: strong peer-reviewed source with direct methodological or empirical relevance.
- **A-**: peer-reviewed and useful, with material transfer or external-validity limits.
- **B+**: complete relevant preprint/workshop evidence; use only with explicit caution.

## Cross-paper synthesis for BeGo-LTF

The corpus supports a layered design: exact/event-based validation on small cases; a diverse heuristic PlanArchive for scale; a deterministic history-aware selector; and optional prediction only after simple baselines. It also supplies negative evidence: neural policies do not uniformly beat linear ones, strategy-name explanations do not reliably improve perceived fairness, and specialist accessibility routers can be worse than a mainstream baseline when map data are stale.

Accessibility must be participant-specific and provenance-aware. Nominal sidewalk geometry, a single crowdsourced tag, or a universal walking penalty is insufficient. Group recommendation evidence supports persistent history and explicit disagreement, but simulated ratings are not realized travel burden; BeGo therefore tests its own burden/debt model rather than importing recommendation metrics as validation.

## Paper-by-paper evidence

### 01. Event-based MILP models for ridepooling applications

- **Citation:** D. Gaul, K. Klamroth, Michael Stiglmayr (2022). *European Journal of Operational Research*.
- **Persistent link:** https://doi.org/10.1016/j.ejor.2021.11.053; [landing page](https://doi.org/10.1016/j.ejor.2021.11.053); full PDF không phân phối trong repo (local corpus: pdfs/01-event-based-milp-models-for-ridepooling-applications.pdf); structured review cục bộ (local corpus: structured-digests/01-structured-review.md); raw digest cục bộ (local corpus: digests/01-digest.txt).
- **Provenance:** Metadata verified from `https://doi.org/10.1016/j.ejor.2021.11.053`; full text is official proceedings, publisher, or institutional-author full text; peer-reviewed: **yes**.
- **Full-text audit:** 30 pages; 16,803 extracted words; SHA-256 `bc43505b1c80f503721b9bdc585af247f36491a90a0596d4ae989d1c12c090ee`.
- **Method:** Two event-based mixed-integer linear programming formulations for static dial-a-ride planning; the event graph encodes vehicle load states so that capacity, pickup-delivery pairing, and precedence are largely implicit. The paper also evaluates weighted combinations of accepted requests, routing cost, and user-oriented regret on a Wuppertal case study.
- **Data/evaluation:** Classical Cordeau DARP benchmarks plus a real Wuppertal, Germany ride-pooling case study; solved with a standard commercial MILP solver.
- **Numerical result or theorem:** The event-based models materially outperform a standard compact three-index formulation. Reported highlights include instances with 80 users solved in under seven seconds and smaller instances in under one second, while the case study exposes genuine trade-offs among acceptance, cost, and user regret.
- **Limitations:** Static and deterministic requests; exact performance depends on small vehicle capacities and graph size. A weighted-sum objective does not itself provide long-horizon fairness or a transparent priority order.
- **Direct BeGo-LTF use:** Use an event/state representation as the exact-small oracle and feasibility validator. Preserve separate objective components and an archive of alternatives instead of copying the paper's weighted sum into DARS.
- **Quality tier:** **A-** — Peer-reviewed publication with direct methodological relevance; transfer limitations are assessed in the structured review.

### 02. Solving the Dynamic Dial-a-Ride Problem Using a Rolling-Horizon Event-Based Graph

- **Citation:** Daniela Gaul, Kathrin Klamroth, Michael Stiglmayr (2021). *Open Access Series in Informatics (ATMOS 2021)*.
- **Persistent link:** https://doi.org/10.4230/oasics.atmos.2021.8; [landing page](https://doi.org/10.4230/oasics.atmos.2021.8); full PDF không phân phối trong repo (local corpus: pdfs/02-solving-the-dynamic-dial-a-ride-problem-using-a-rolling-horizon-even.pdf); structured review cục bộ (local corpus: structured-digests/02-structured-review.md); raw digest cục bộ (local corpus: digests/02-digest.txt).
- **Provenance:** Metadata verified from `https://doi.org/10.4230/oasics.atmos.2021.8`; full text is official proceedings, publisher, or institutional-author full text; peer-reviewed: **yes**.
- **Full-text audit:** 16 pages; 8,903 extracted words; SHA-256 `0fe0d86a0e400ced208f71c4c1507e8deb7d7fd8f3079e831e297c261e345449`.
- **Method:** Rolling-horizon dynamic DARP built on an event-based graph and MILP. At each decision epoch it updates the graph, respects already committed service, and optimizes the newly revealed request set.
- **Data/evaluation:** Synthetic and real Wuppertal-style instances, including dynamic cases with more than 500 requests; computational budgets are evaluated at operational decision epochs.
- **Numerical result or theorem:** The approach reports an average runtime around 2.8 seconds and finds a solution proven optimal for the current rolling-horizon subproblem in 99.5% of runs within a 30-second limit, demonstrating that event graphs can support practical replanning.
- **Limitations:** Current-horizon optimality is not global sequence optimality; committed decisions are irrevocable and future demand is not fully known. Results transfer only after BeGo-specific constraints and timeout behavior are revalidated.
- **Direct BeGo-LTF use:** Adopt immutable snapshots, explicit commitment boundaries, timeout statuses, and rolling replanning. Use the formulation as a benchmark oracle for small dynamic cases, not as proof that a myopic epoch is long-term fair.
- **Quality tier:** **A-** — Peer-reviewed publication with direct methodological relevance; transfer limitations are assessed in the structured review.

### 03. A linearly decreasing deterministic annealing algorithm for the multi-vehicle dial-a-ride problem

- **Citation:** Amir Mortazavi, Milad Ghasri, Tapabrata Ray (2024). *PLoS ONE*.
- **Persistent link:** https://doi.org/10.1371/journal.pone.0292683; [landing page](https://doi.org/10.1371/journal.pone.0292683); full PDF không phân phối trong repo (local corpus: pdfs/03-a-linearly-decreasing-deterministic-annealing-algorithm-for-the-mult.pdf); structured review cục bộ (local corpus: structured-digests/03-structured-review.md); raw digest cục bộ (local corpus: digests/03-digest.txt).
- **Provenance:** Metadata verified from `https://doi.org/10.1371/journal.pone.0292683`; full text is official proceedings, publisher, or institutional-author full text; peer-reviewed: **yes**.
- **Full-text audit:** 26 pages; 17,526 extracted words; SHA-256 `d386b20ab65176cb618526f5eb5598a89108ef4ef16fa909b6ffc88fc51a39e1`.
- **Method:** A linearly decreasing deterministic-annealing metaheuristic with feasibility-preserving moves for the multi-vehicle DARP, compared with established metaheuristics and benchmark best-known solutions.
- **Data/evaluation:** Standard multi-vehicle DARP benchmark instances spanning multiple sizes and vehicle configurations.
- **Numerical result or theorem:** The schedule reaches competitive or improved best-known solutions with shorter computational effort on many benchmark instances, supporting annealing as a strong diverse-candidate generator rather than an exact method.
- **Limitations:** Heuristic results have no instance-wise optimality guarantee and are sensitive to neighborhood and cooling choices; the benchmark objective is not BeGo's historical fairness objective.
- **Direct BeGo-LTF use:** Use annealing/ALNS only inside the single-event candidate factory, always followed by a hard-constraint validator and quality label. It should enrich the plan archive, not update fairness debt directly.
- **Quality tier:** **A** — Peer-reviewed publication with direct methodological relevance; transfer limitations are assessed in the structured review.

### 04. A ride time-oriented scheduling algorithm for dial-a-ride problems

- **Citation:** Claudia Bongiovanni, Nikolas Geroliminis, Mor Kaspi (2024). *Computers &amp; Operations Research*.
- **Persistent link:** https://doi.org/10.1016/j.cor.2024.106588; [landing page](https://doi.org/10.1016/j.cor.2024.106588); full PDF không phân phối trong repo (local corpus: pdfs/04-a-ride-time-oriented-scheduling-algorithm-for-dial-a-ride-problems.pdf); structured review cục bộ (local corpus: structured-digests/04-structured-review.md); raw digest cục bộ (local corpus: digests/04-digest.txt).
- **Provenance:** Metadata verified from `https://doi.org/10.1016/j.cor.2024.106588`; full text is official proceedings, publisher, or institutional-author full text; peer-reviewed: **yes**.
- **Full-text audit:** 12 pages; 9,582 extracted words; SHA-256 `23013b9ec440eacafe6d4da974167b5b385683e8caa199abea60b55d3ab0d1d3`.
- **Method:** A polynomial-time, LP-theory-inspired scheduling heuristic that minimizes excess ride time for a fixed DARP route, plus feasibility recovery and an electric-vehicle charging heuristic.
- **Data/evaluation:** About 21.5 million route schedules sampled from DARP and electric/autonomous DARP benchmark instances, compared with a linear program and established scheduling procedures.
- **Numerical result or theorem:** Only 27 of roughly 21.5 million DARP schedules were reported near-optimal rather than optimal, and the procedure was about 60% faster on average than the LP while improving solution quality over common scheduling heuristics.
- **Limitations:** It can falsely declare a route infeasible or return a suboptimal schedule; the battery routine is heuristic. The study schedules a given route and does not solve destination, grouping, vehicle assignment, and historical fairness jointly.
- **Direct BeGo-LTF use:** Separate route construction from fast schedule repair, but retain an exact validator/fallback for false infeasibility. This motivates resource-accounting modules and property tests around time windows and ride-time limits.
- **Quality tier:** **A-** — Peer-reviewed publication with direct methodological relevance; transfer limitations are assessed in the structured review.

### 05. Collaborative electric vehicle routing with meet points

- **Citation:** Fangting Zhou, Ala Arvidsson, Jiaming Wu, Balázs Kulcsár (2024). *Communications in Transportation Research*.
- **Persistent link:** https://doi.org/10.1016/j.commtr.2024.100135; [landing page](https://research.chalmers.se/en/publication/537741); full PDF không phân phối trong repo (local corpus: pdfs/05-collaborative-electric-vehicle-routing-with-meet-points.pdf); structured review cục bộ (local corpus: structured-digests/05-structured-review.md); raw digest cục bộ (local corpus: digests/05-digest.txt).
- **Provenance:** Metadata verified from `https://research.chalmers.se/en/publication/537741`; full text is Chalmers institutional copy of the published journal article; peer-reviewed: **yes**.
- **Full-text audit:** 21 pages; 19,649 extracted words; SHA-256 `70ede18e108c9ea272942e2633b25f39a13368e64861a80dfdbe05c5bdc9338d`.
- **Method:** A collaborative electric-vehicle routing model with meet points, an exact branching formulation for small cases, and an ALNS/linear-programming hybrid for larger instances.
- **Data/evaluation:** A Gothenburg-derived case study and generated instances reaching approximately 500 requests, with cost, energy, and operator outcomes compared across collaboration settings.
- **Numerical result or theorem:** Coordinated meet points can reduce operating cost and energy use and improve profit relative to non-collaborative routing; the hybrid method makes substantially larger instances tractable than the exact formulation.
- **Limitations:** The application is goods/EV collaboration rather than social outing groups; exact solving becomes impractical from moderate sizes and user walking/acceptance behavior is simplified.
- **Direct BeGo-LTF use:** Model a common pickup or transfer point as an explicit synchronization resource with participant-specific access costs. Transfer the exact-small/ALNS-large pattern, but validate walking limits and consent in BeGo's domain.
- **Quality tier:** **A** — Peer-reviewed operations research study with exact decomposition, ALNS matheuristic, real case and tests up to 500 customers.

### 06. Vehicle Dispatch in On-Demand Ride-Sharing with Stochastic Travel Times

- **Citation:** Cheng Li, David Parker, Qi Hao (2021). *2021 IEEE/RSJ International Conference on Intelligent Robots and Systems (IROS)*.
- **Persistent link:** https://doi.org/10.1109/iros51168.2021.9636499; [landing page](https://doi.org/10.1109/iros51168.2021.9636499); full PDF không phân phối trong repo (local corpus: pdfs/06-vehicle-dispatch-in-on-demand-ride-sharing-with-stochastic-travel-ti.pdf); structured review cục bộ (local corpus: structured-digests/06-structured-review.md); raw digest cục bộ (local corpus: digests/06-digest.txt).
- **Provenance:** Metadata verified from `https://doi.org/10.1109/iros51168.2021.9636499`; full text is official proceedings, publisher, or institutional-author full text; peer-reviewed: **yes**.
- **Full-text audit:** 8 pages; 6,959 extracted words; SHA-256 `9c68231aaefd2b5eb270f07d02b4378cc0329a1091b2cd2da3a68993362f1145`.
- **Method:** Approximate stochastic shortest-path dispatch with reliability-aware vehicle allocation under random travel times, evaluated against deterministic dispatch policies.
- **Data/evaluation:** New York City taxi-derived demand and simulated stochastic travel times under multiple fleet and peak-demand conditions.
- **Numerical result or theorem:** The reliability-aware policy reports improvements up to about 7.3% in reliability, 8.13% in profit, and 4.22% in service rate in tested peak scenarios.
- **Limitations:** Travel-time distributions and independence assumptions are simplified, and evidence comes from simulation rather than deployment. Profit/reliability are not equivalent to participant fairness.
- **Direct BeGo-LTF use:** Use scenario-based ETA uncertainty and expose confidence/quality labels. Keep stochastic residual prediction optional and never allow it to bypass hard arrival, pickup, or budget guards.
- **Quality tier:** **A-** — Peer-reviewed publication with direct methodological relevance; transfer limitations are assessed in the structured review.

### 07. Adaptive forecast-driven repositioning for dynamic ride-sharing

- **Citation:** Martin Pouls, Nitin Ahuja, Katharina Glock, Anne Meyer (2025). *Annals of Operations Research*.
- **Persistent link:** https://doi.org/10.1007/s10479-022-04560-3; [landing page](https://doi.org/10.1007/s10479-022-04560-3); full PDF không phân phối trong repo (local corpus: pdfs/07-adaptive-forecast-driven-repositioning-for-dynamic-ride-sharing.pdf); structured review cục bộ (local corpus: structured-digests/07-structured-review.md); raw digest cục bộ (local corpus: digests/07-digest.txt).
- **Provenance:** Metadata verified from `https://doi.org/10.1007/s10479-022-04560-3`; full text is official proceedings, publisher, or institutional-author full text; peer-reviewed: **yes**.
- **Full-text audit:** 34 pages; 16,973 extracted words; SHA-256 `6554ccea5c45dc5f94dcb32bc77b46653d1c7c9ba8c4aaf1d6ee58d33b911852`.
- **Method:** A forecast-driven mixed-integer repositioning model with adaptive parameter tuning, embedded in a dynamic ride-sharing dispatcher and tested with perfect and naive demand forecasts.
- **Data/evaluation:** Real-world demand from Hamburg, New York City/Manhattan, and Chengdu, evaluated through large-scale simulation against a reactive repositioning baseline.
- **Numerical result or theorem:** Across the experiments, forecast-driven repositioning reduces rejection rates by an average of 3.5 percentage points and improves customer waiting and ride times; perfect and naive forecasts bracket likely performance.
- **Limitations:** The result depends on forecast quality, simulated operations, fleet assumptions, and city-specific demand. Repositioning is outside BeGo's initial private-group scope.
- **Direct BeGo-LTF use:** If BeGo later adds fleet supply, gate prediction behind an offline comparison with a simple reactive baseline. For the 10-month scope, reuse only the forecast-quality and ablation discipline, not the repositioning module.
- **Quality tier:** **A** — Peer-reviewed publication with direct methodological relevance; transfer limitations are assessed in the structured review.

### 08. Dynamic vehicle routing with random requests: A literature review

- **Citation:** Jian Zhang, Tom Van Woensel (2023). *International Journal of Production Economics*.
- **Persistent link:** https://doi.org/10.1016/j.ijpe.2022.108751; [landing page](https://research.tue.nl/en/publications/dynamic-vehicle-routing-with-random-requests-a-literature-review); full PDF không phân phối trong repo (local corpus: pdfs/08-dynamic-vehicle-routing-with-random-requests-a-literature-review.pdf); structured review cục bộ (local corpus: structured-digests/08-structured-review.md); raw digest cục bộ (local corpus: digests/08-digest.txt).
- **Provenance:** Metadata verified from `https://research.tue.nl/en/publications/dynamic-vehicle-routing-with-random-requests-a-literature-review`; full text is TU/e institutional publisher PDF; peer-reviewed: **yes**.
- **Full-text audit:** 36 pages; 39,488 extracted words; SHA-256 `c56534d4d8a951fa8e4324148eff5b337f70572011172a902eeee420ebd720a3`.
- **Method:** Structured literature review and taxonomy of dynamic vehicle routing with random requests, covering models, objectives, solution families, datasets, and evaluation practice from 1980 through 2022.
- **Data/evaluation:** A corpus of 118 journal papers, classified into four DVRP-with-random-request variants and multiple modeling and algorithmic dimensions.
- **Numerical result or theorem:** The review finds rolling horizons, multi-stage stochastic approaches, and MDPs to be dominant abstractions; about 54% of studies use multiple objectives, while more than 80% rely on non-public instances, revealing weak reproducibility and benchmark fragmentation.
- **Limitations:** The review excludes some conference literature and other forms of dynamism, and its taxonomy does not directly address repeated interpersonal fairness.
- **Direct BeGo-LTF use:** Freeze public scenario manifests, seeds, baselines, and metric definitions before experiments. Report selector-only and end-to-end results separately so the contribution is reproducible rather than another private-instance comparison.
- **Quality tier:** **A** — Peer-reviewed systematic methodological review in a leading production/OR journal.

### 09. Future Aware Pricing and Matching for Sustainable On-Demand Ride Pooling

- **Citation:** Xianjie Zhang, Pradeep Varakantham, Hao Jiang (2023). *Proceedings of the AAAI Conference on Artificial Intelligence*.
- **Persistent link:** https://doi.org/10.1609/aaai.v37i12.26710; [landing page](https://doi.org/10.1609/aaai.v37i12.26710); full PDF không phân phối trong repo (local corpus: pdfs/09-future-aware-pricing-and-matching-for-sustainable-on-demand-ride-poo.pdf); structured review cục bộ (local corpus: structured-digests/09-structured-review.md); raw digest cục bộ (local corpus: digests/09-digest.txt).
- **Provenance:** Metadata verified from `https://doi.org/10.1609/aaai.v37i12.26710`; full text is official proceedings, publisher, or institutional-author full text; peer-reviewed: **yes**.
- **Full-text audit:** 9 pages; 7,893 extracted words; SHA-256 `878b70e2b613fe637b7a93748c72b147aeb57e6f10b02f8f9c10353c5b8e1d45`.
- **Method:** A two-layer future-aware framework combining reinforcement learning for anticipatory pricing/acceptance with centralized trip-vehicle matching.
- **Data/evaluation:** City-scale taxi-demand data replayed in simulation and compared with myopic pricing and matching baselines.
- **Numerical result or theorem:** In the studied settings, future-aware decisions increase platform revenue by up to roughly 17%, use about 14% fewer vehicles, and reduce average travel distance while maintaining service outcomes.
- **Limitations:** Acceptance, demand response, and price elasticity are simulated; objectives are platform-centric and learned policies are harder to audit. Results do not establish fairness for a recurring social group.
- **Direct BeGo-LTF use:** Treat attendance/acceptance prediction only as an optional candidate feature. The deterministic core must run without it, and any model must beat calibrated non-ML baselines under frozen splits without worsening protected guard metrics.
- **Quality tier:** **A-** — Peer-reviewed publication with direct methodological relevance; transfer limitations are assessed in the structured review.

### 10. Wait to be Faster: a Smart Pooling Framework for Dynamic Ridesharing

- **Citation:** Xiaoyao Zhong, Jiabao Jin, Peng Cheng, Wangze Ni, Libin Zheng, Lei Chen, Xuemin Lin (2024). *IEEE 40th International Conference on Data Engineering (ICDE 2024)*.
- **Persistent link:** https://doi.org/10.1109/ICDE60146.2024.00034; [landing page](https://doi.org/10.1109/ICDE60146.2024.00034); full PDF không phân phối trong repo (local corpus: pdfs/10-wait-to-be-faster-a-smart-pooling-framework-for-dynamic-ridesharing.pdf); structured review cục bộ (local corpus: structured-digests/10-structured-review.md); raw digest cục bộ (local corpus: digests/10-digest.txt).
- **Provenance:** Metadata verified from `https://doi.org/10.1109/ICDE60146.2024.00034`; full text is official proceedings, publisher, or institutional-author full text; peer-reviewed: **yes**.
- **Full-text audit:** 16 pages; 37,847 extracted words; SHA-256 `4673e938482c69c48113e6ed0cde2794b342b23523376c7fc9ae16a48176b13f`.
- **Method:** WATTER formulates Minimal Extra Time RideSharing, derives a convex per-order waiting-threshold subproblem, and uses an MDP/value function to decide how long an order may wait for a better pool.
- **Data/evaluation:** Three real ride-request datasets at multiple load levels, compared with immediate and fixed-batch dispatch baselines.
- **Numerical result or theorem:** Allowing bounded, individualized waiting reduces total extra time by about 12.2% to 40.1% in reported settings and improves service measures at high request volumes.
- **Limitations:** Learned waiting decisions rely on historical demand and may create unacceptable uncertainty for users; offline replay cannot fully capture abandonment or trust. The target is dispatch efficiency, not historical burden equity.
- **Direct BeGo-LTF use:** Support an optional, consented bounded-wait/replan policy with a hard deadline and visible reason. Keep it outside the primary DARS claim and compare it with zero-wait and fixed-window ablations.
- **Quality tier:** **A-** — Peer-reviewed publication with direct methodological relevance; transfer limitations are assessed in the structured review.

### 11. Efficient Algorithms for Stochastic Ridepooling Assignment with Mixed Fleets

- **Citation:** Qi Luo, Viswanath Nagarajan, Alexander Sundt, Yafeng Yin, John Vincent, Mehrdad Shahabi (2023). *Transportation Science*.
- **Persistent link:** https://doi.org/10.1287/trsc.2021.0349; [landing page](https://doi.org/10.1287/trsc.2021.0349); full PDF không phân phối trong repo (local corpus: pdfs/11-efficient-algorithms-for-stochastic-ridepooling-assignment-with-mixe.pdf); structured review cục bộ (local corpus: structured-digests/11-structured-review.md); raw digest cục bộ (local corpus: digests/11-digest.txt).
- **Provenance:** Metadata verified from `https://doi.org/10.1287/trsc.2021.0349`; full text is official proceedings, publisher, or institutional-author full text; peer-reviewed: **yes**.
- **Full-text audit:** 45 pages; 19,304 extracted words; SHA-256 `363567a413eb54f4e2a0ba92adb324b0eaabd869f991294eafaa96f45c7e31af`.
- **Method:** Two-stage stochastic ride-pooling assignment with mixed fleets on shareability hypergraphs. The paper proposes LP-rounding/local-search approximation algorithms for mid- and high-capacity vehicles plus sample-average approximation.
- **Data/evaluation:** Mixed-autonomy on-demand mobility simulations using real demand traces and multiple fleet compositions, sample sizes, capacities, and parallel-compute settings.
- **Numerical result or theorem:** The algorithms have worst-case guarantees of 1/p^2 and approximately (e-1)/(2e p ln p), with p equal to vehicle capacity plus one; empirical optimality gaps are much smaller than these conservative bounds and evaluation can be parallelized.
- **Limitations:** The stochastic model is two-stage, shareability-hypergraph construction can dominate runtime, and results target fleet sizing/profit rather than repeated participant burden.
- **Direct BeGo-LTF use:** Use shareability pruning and parallel candidate evaluation when vehicle assignment grows. Preserve a bounded/exact quality label and do not cite the approximation ratio for DARS, whose objective and feasible set differ.
- **Quality tier:** **A-** — Peer-reviewed publication with direct methodological relevance; transfer limitations are assessed in the structured review.

### 12. Neural Approximate Dynamic Programming for On-Demand Ride-Pooling

- **Citation:** Sanket Shah, Meghna Lowalekar, Pradeep Varakantham (2020). *AAAI Conference on Artificial Intelligence*.
- **Persistent link:** https://doi.org/10.1609/aaai.v34i01.5388; [landing page](https://ojs.aaai.org/index.php/AAAI/article/view/5388); full PDF không phân phối trong repo (local corpus: pdfs/12-neural-approximate-dynamic-programming-for-on-demand-ride-pooling.pdf); structured review cục bộ (local corpus: structured-digests/12-structured-review.md); raw digest cục bộ (local corpus: digests/12-digest.txt).
- **Provenance:** Metadata verified from `https://ojs.aaai.org/index.php/AAAI/article/view/5388`; full text is official proceedings, publisher, or institutional-author full text; peer-reviewed: **yes**.
- **Full-text audit:** 9 pages; 7,625 extracted words; SHA-256 `ef77b8250f871b17e5b1d1363d9af4a5143645834cd10662c1e7a46114bbf056`.
- **Method:** Neural approximate dynamic programming for integer ride-pool assignments: an offline-trained neural value function supplies future value terms to an online ILP assignment without relying on weak LP duals.
- **Data/evaluation:** Real city-scale ride-request data and simulator comparisons against leading myopic and anticipatory ride-pooling methods.
- **Numerical result or theorem:** NeurADP improves the served-demand objective by up to 16% over the reported state of the art while retaining online assignment times suitable for batched dispatch.
- **Limitations:** Training and evaluation use historical replay, the learned value is opaque and distribution dependent, and the objective does not measure long-term interpersonal fairness.
- **Direct BeGo-LTF use:** This is evidence that optional look-ahead can help, not that BeGo needs deep RL. First ship the deterministic selector; only add a learned residual/value model after an ablation proves incremental value and safe fallback behavior.
- **Quality tier:** **A-** — Peer-reviewed publication with direct methodological relevance; transfer limitations are assessed in the structured review.

### 13. ZAC: A Zone Path Construction Approach for Effective Real-Time Ridesharing

- **Citation:** Meghna Lowalekar, Pradeep Varakantham, Patrick Jaillet (2019). *Proceedings of the International Conference on Automated Planning and Scheduling*.
- **Persistent link:** https://doi.org/10.1609/icaps.v29i1.3519; [landing page](https://doi.org/10.1609/icaps.v29i1.3519); full PDF không phân phối trong repo (local corpus: pdfs/13-zac-a-zone-path-construction-approach-for-effective-real-time-ridesh.pdf); structured review cục bộ (local corpus: structured-digests/13-structured-review.md); raw digest cục bộ (local corpus: digests/13-digest.txt).
- **Provenance:** Metadata verified from `https://doi.org/10.1609/icaps.v29i1.3519`; full text is official proceedings, publisher, or institutional-author full text; peer-reviewed: **yes**.
- **Full-text audit:** 11 pages; 9,327 extracted words; SHA-256 `c285b902b680b36bbef0e70ecbc85d5be004c288df10bcc667e44987142ce9be`.
- **Method:** ZAC compresses many request combinations into offline/online-generated zone paths, then assigns vehicles to those paths to avoid explicit enumeration of all request trips.
- **Data/evaluation:** Real and synthetic ride-sharing datasets across demand, batching, and vehicle-capacity settings, compared with the trip-based TBF state of the art.
- **Numerical result or theorem:** ZAC consistently improves both solution quality/service rate and runtime, especially for higher-capacity vehicles where explicit trip enumeration grows exponentially.
- **Limitations:** Zone discretization can hide street-level accessibility and pickup details; the paper is myopic and its future-demand extension is left open.
- **Direct BeGo-LTF use:** Use coarse spatial buckets only for candidate retrieval, then recompute every shortlisted plan on the road graph with exact participant costs. Never use zones as final feasibility or fairness evidence.
- **Quality tier:** **A** — Peer-reviewed publication with direct methodological relevance; transfer limitations are assessed in the structured review.

### 14. When Hashing Met Matching: Efficient Spatio-Temporal Search for Ridesharing

- **Citation:** Chinmoy Dutta (2021). *AAAI Conference on Artificial Intelligence*.
- **Persistent link:** https://doi.org/10.1609/aaai.v35i1.16081; [landing page](https://ojs.aaai.org/index.php/AAAI/article/view/16081); full PDF không phân phối trong repo (local corpus: pdfs/14-when-hashing-met-matching-efficient-spatio-temporal-search-for-rides.pdf); structured review cục bộ (local corpus: structured-digests/14-structured-review.md); raw digest cục bộ (local corpus: digests/14-digest.txt).
- **Provenance:** Metadata verified from `https://ojs.aaai.org/index.php/AAAI/article/view/16081`; full text is official proceedings, publisher, or institutional-author full text; peer-reviewed: **yes**.
- **Full-text audit:** 9 pages; 7,605 extracted words; SHA-256 `0ed2c1c175f494edaed71bd3515dafc48a48ff1cfb32872a9618b2750a4052bb`.
- **Method:** A spatio-temporal vector representation and locality-sensitive hashing scheme casts candidate ride matching as near-neighbor search before exact utility evaluation and matching.
- **Data/evaluation:** Large real commute datasets with morning/evening loads up to about 20,000 rides, OSRM travel costs, and comparisons with proximity/Haversine heuristics and exhaustive candidate search.
- **Numerical result or theorem:** LSH achieves about six percentage points more matching utility than the next-best heuristic and can reduce a roughly 300-second computation to under 30 seconds with ten workers for the largest pool, while staying near the utility-optimal candidate network.
- **Limitations:** The embedding and discretization are tuned to commuting patterns, approximate search can miss candidates, and the final objective is aggregate matching utility.
- **Direct BeGo-LTF use:** Use approximate spatial indexing only as a recall-oriented prefilter. Measure candidate recall against exhaustive small cases, retain deterministic stable ordering, and validate all constraints after retrieval.
- **Quality tier:** **A-** — Peer-reviewed publication with direct methodological relevance; transfer limitations are assessed in the structured review.

### 15. Algorithms for Trip-Vehicle Assignment in Ride-Sharing

- **Citation:** Xiaohui Bei, Shengyu Zhang (2018). *Proceedings of the AAAI Conference on Artificial Intelligence*.
- **Persistent link:** https://doi.org/10.1609/aaai.v32i1.11298; [landing page](https://doi.org/10.1609/aaai.v32i1.11298); full PDF không phân phối trong repo (local corpus: pdfs/15-algorithms-for-trip-vehicle-assignment-in-ride-sharing.pdf); structured review cục bộ (local corpus: structured-digests/15-structured-review.md); raw digest cục bộ (local corpus: digests/15-digest.txt).
- **Provenance:** Metadata verified from `https://doi.org/10.1609/aaai.v32i1.11298`; full text is official proceedings, publisher, or institutional-author full text; peer-reviewed: **yes**.
- **Full-text audit:** 7 pages; 5,844 extracted words; SHA-256 `fffae998b7356aefea87efb95fb2443ff836c0dba9d2e6c6357eefc9d9972102`.
- **Method:** Formalizes pairing two requests with one vehicle as a combinatorial assignment, proves NP-hardness, and gives a two-phase minimum-matching algorithm under metric distances.
- **Data/evaluation:** Uniform and Gaussian-mixture synthetic locations under L1 and L2 distances, primarily with exactly twice as many requests as vehicles.
- **Numerical result or theorem:** The polynomial algorithm runs in O(n^3), guarantees cost at most 2.5 times optimal, and empirically obtains ratios around 1.1-1.2 on generated instances.
- **Limitations:** The guarantee assumes two riders per car, metric distances, a static setting, and no time windows; evidence is synthetic and has no fairness dimension.
- **Direct BeGo-LTF use:** Include a simple matching baseline with a known bound for restricted assignment cases. Do not transfer its guarantee to BeGo's richer pickup, capacity, time, and fairness constraints.
- **Quality tier:** **A-** — Peer-reviewed publication with direct methodological relevance; transfer limitations are assessed in the structured review.

### 16. The multi-vehicle dial-a-ride problem with interchange and perceived passenger travel times

- **Citation:** Konstantinos Gkiotsalitis, A. Nikolopoulou (2023). *Transportation Research Part C Emerging Technologies*.
- **Persistent link:** https://doi.org/10.1016/j.trc.2023.104353; [landing page](https://doi.org/10.1016/j.trc.2023.104353); full PDF không phân phối trong repo (local corpus: pdfs/16-the-multi-vehicle-dial-a-ride-problem-with-interchange-and-perceived.pdf); structured review cục bộ (local corpus: structured-digests/16-structured-review.md); raw digest cục bộ (local corpus: digests/16-digest.txt).
- **Provenance:** Metadata verified from `https://doi.org/10.1016/j.trc.2023.104353`; full text is official proceedings, publisher, or institutional-author full text; peer-reviewed: **yes**.
- **Full-text audit:** 23 pages; 19,726 extracted words; SHA-256 `e8465c27b601db4b6e5008d591881a5444e97a2f0b78edc597127a92674d3647`.
- **Method:** Introduces DARPi with a passenger interchange point and crowding-dependent perceived travel time, linearizes the nonlinear model, adds valid inequalities, and supplies branch-and-cut plus tabu search.
- **Data/evaluation:** Established DARP benchmarks, exact experiments up to ten requests, and heuristic instances up to 200 requests with sensitivity to the crowding penalty.
- **Numerical result or theorem:** Small cases are solved globally; tabu search handles up to 200 requests in under 30 minutes. Maintaining lower perceived crowding increases vehicle routing cost by about 2.09% in the reported sensitivity study.
- **Limitations:** Crowding valuations are transferred from prior stated/revealed-preference studies, the interchange location is fixed, and large-instance results are heuristic.
- **Direct BeGo-LTF use:** Represent transfer/synchronization and perceived discomfort as explicit components, but elicit or sensitivity-test their weights. Keep crowding and transfer penalties auditable rather than hiding them in one composite cost.
- **Quality tier:** **A** — Peer-reviewed publication with direct methodological relevance; transfer limitations are assessed in the structured review.

### 17. The Line-Based Dial-a-Ride Problem

- **Citation:** Reiter, Kendra, Schmidt, Marie, Michael Stiglmayr (2024). *24th Symposium on Algorithmic Approaches for Transportation Modelling, Optimization, and Systems (ATMOS 2024), OASIcs*.
- **Persistent link:** https://doi.org/10.4230/oasics.atmos.2024.14; [landing page](https://doi.org/10.4230/OASIcs.ATMOS.2024.14); full PDF không phân phối trong repo (local corpus: pdfs/17-the-line-based-dial-a-ride-problem.pdf); structured review cục bộ (local corpus: structured-digests/17-structured-review.md); raw digest cục bộ (local corpus: digests/17-digest.txt).
- **Provenance:** Metadata verified from `https://doi.org/10.4230/OASIcs.ATMOS.2024.14`; full text is official proceedings, publisher, or institutional-author full text; peer-reviewed: **yes**.
- **Full-text audit:** 20 pages; 10,136 extracted words; SHA-256 `dbf50234d71f10f88cdde4fad9af2e41d8a3310ae2ec6d077fc7d1bff20849b9`.
- **Method:** Defines line-based DARP on an ordered sequence of stops and compares three MILP formulations with an objective balancing accepted passengers and distance.
- **Data/evaluation:** Benchmark instances derived from a real bus line in Wuerzburg, Germany, compared with classical DARP behavior.
- **Numerical result or theorem:** The event-based formulation is fastest and solves instances with up to 50 requests in under one second; line constraints greatly simplify computation with only small increases in distance and average ride time in tested cases.
- **Limitations:** All requests are known in advance, vehicles follow a fixed stop order, and demand fluctuations and feeder synchronization remain future work.
- **Direct BeGo-LTF use:** When a meetup follows a transit corridor, generate a line-restricted candidate family as a fast, explainable alternative. It must coexist with unrestricted plans so the restriction cannot silently exclude a fairer destination.
- **Quality tier:** **A-** — Peer-reviewed publication with direct methodological relevance; transfer limitations are assessed in the structured review.

### 18. Recent advances in integrating demand management and vehicle routing: A methodological review

- **Citation:** David Fleckenstein, Robert Klein, Claudius Steinhardt (2023). *European Journal of Operational Research*.
- **Persistent link:** https://doi.org/10.1016/j.ejor.2022.04.032; [landing page](https://opus.bibliothek.uni-augsburg.de/opus4/frontdoor/index/index/docId/102388); full PDF không phân phối trong repo (local corpus: pdfs/18-recent-advances-integrating-demand-management-vehicle-routing.pdf); structured review cục bộ (local corpus: structured-digests/18-structured-review.md); raw digest cục bộ (local corpus: digests/18-digest.txt).
- **Provenance:** Metadata verified from `https://opus.bibliothek.uni-augsburg.de/opus4/frontdoor/index/index/docId/102388`; full text is University of Augsburg repository, published open-access PDF; peer-reviewed: **yes**.
- **Full-text audit:** 21 pages; 23,321 extracted words; SHA-256 `abc93ce1dbf8395c402a3c939669fd19d7b5fcaee354ca8f1008addc46c77094`.
- **Method:** Methodological review organized around a generic Markov/sequential decision process, separating demand-control actions, routing state, feasibility checks, value approximation, forecasts, and online decision policies.
- **Data/evaluation:** Peer-reviewed literature across attended home delivery, field service, same-day delivery, and mobility-on-demand; evidence is synthesized in a cross-application taxonomy rather than a new experiment.
- **Numerical result or theorem:** The review shows that approximate route-feasibility checks trade speed for false positives/negatives, and argues for common MDP formulations, clearer separation of prediction and optimization, and cross-application evaluation.
- **Limitations:** Heterogeneous applications make direct effect-size comparison impossible; the review emphasizes provider demand management and does not formulate long-term fairness among recurring participants.
- **Direct BeGo-LTF use:** Define state, action, exogenous information, transition, and outcome explicitly. Separate predictor, candidate factory, feasibility validator, and selector so learned demand signals cannot be confused with normative fairness decisions.
- **Quality tier:** **A*** — Invited peer-reviewed EJOR review with a unified sequential decision model and cross-application taxonomy.

### 19. Accessibility for Whom? Perceptions of Sidewalk Barriers Across Disability Groups and Implications for Designing Personalized Maps

- **Citation:** Chu Li, Rock Yuren Pang, Delphine Labbé, Yochai Eisenberg, Maryam Hosseini, Jon E. Froehlich (2025). *ACM CHI Conference on Human Factors in Computing Systems (CHI 2025)*.
- **Persistent link:** https://doi.org/10.1145/3706598.3713421; [landing page](https://doi.org/10.1145/3706598.3713421); full PDF không phân phối trong repo (local corpus: pdfs/19-accessibility-for-whom-personalized-maps.pdf); structured review cục bộ (local corpus: structured-digests/19-structured-review.md); raw digest cục bộ (local corpus: digests/19-digest.txt).
- **Provenance:** Metadata verified from `https://doi.org/10.1145/3706598.3713421`; full text is University of Washington author-hosted ACM paper; peer-reviewed: **yes**.
- **Full-text audit:** 19 pages; 13,991 extracted words; SHA-256 `c092aad413578ff2cee8a8aff773e4dc21ffaa055805cc9e36b00fab4aa550a8`.
- **Method:** Online image survey using ratings, rankings, adaptive pairwise comparisons, and free text across five mobility-aid groups; group-level profiles feed two proof-of-concept mapping/routing applications.
- **Data/evaluation:** N=190 participants, 52 images, nine sidewalk-barrier categories, and five mobility-aid groups; the dataset and prototype target personalized urban accessibility.
- **Numerical result or theorem:** Groups agree on many clearly low/high-severity barriers but diverge on mid-severity cases; scooter users are generally more cautious and wheeled users are especially sensitive to missing curb ramps. Personalized routes can differ materially by mobility profile.
- **Limitations:** Images cannot reproduce physical interaction or transient conditions, recruitment and self-report introduce bias, and the routing system is a proof of concept rather than a field deployment.
- **Direct BeGo-LTF use:** Never use one universal walking/accessibility cost. Store participant-specific constraints and provenance, apply hard exclusions before optimization, and label map-derived accessibility confidence.
- **Quality tier:** **A*** — Peer-reviewed flagship HCI paper with N=190 across five mobility-device groups and open design artifacts.

### 20. Optimal design of ride-pooling as on-demand feeder services

- **Citation:** Wenbo Fan, Weihua Gu, Meng Xu (2024). *Transportation Research Part B: Methodological*.
- **Persistent link:** https://doi.org/10.1016/j.trb.2024.102964; [landing page](https://doi.org/10.1016/j.trb.2024.102964); full PDF không phân phối trong repo (local corpus: pdfs/20-optimal-design-of-ride-pooling-as-on-demand-feeder-services.pdf); structured review cục bộ (local corpus: structured-digests/20-structured-review.md); raw digest cục bộ (local corpus: digests/20-digest.txt).
- **Provenance:** Metadata verified from `https://doi.org/10.1016/j.trb.2024.102964`; full text is official proceedings, publisher, or institutional-author full text; peer-reviewed: **yes**.
- **Full-text audit:** 34 pages; 14,534 extracted words; SHA-256 `a1735f8ecd7a877814bdade4efea85118491e8d6d9b9132d5252d59e7955c274`.
- **Method:** Continuous-approximation analytical design of ride-pooling feeder service with heterogeneous zones, fleet density, and a hold-dispatch pooling target; derives closed-form design relations and compares quick-dispatch and flexible-route transit.
- **Data/evaluation:** Extensive numerical experiments over spatial demand, terminal distance, operating cost, value of time, and service-design scenarios.
- **Numerical result or theorem:** Hold-dispatch outperforms quick-dispatch at higher, less heterogeneous demand and in costly suburbs, and generally outperforms flexible-route transit except with extremely high fleet cost or very low passenger value of time.
- **Limitations:** Macro-level steady-state and deterministic assumptions omit stochastic demand, congestion, a street network, explicit fleet dynamics, and operational pickup details.
- **Direct BeGo-LTF use:** Use the paper to motivate a bounded pooling-size/wait sensitivity study and heterogeneous scenario generation, not as an operational solver. BeGo must validate every candidate on the actual road graph and individual constraints.
- **Quality tier:** **A-** — Peer-reviewed publication with direct methodological relevance; transfer limitations are assessed in the structured review.

### 21. Improving public transportation via line-based integration of on-demand ridepooling

- **Citation:** Andrés Fielbaum, Alejandro Tirachini, Javier Alonso–Mora (2024). *Transportation Research Part A Policy and Practice*.
- **Persistent link:** https://doi.org/10.1016/j.tra.2024.104289; [landing page](https://doi.org/10.1016/j.tra.2024.104289); full PDF không phân phối trong repo (local corpus: pdfs/21-improving-public-transportation-via-line-based-integration-of-on-dem.pdf); structured review cục bộ (local corpus: structured-digests/21-structured-review.md); raw digest cục bộ (local corpus: digests/21-digest.txt).
- **Provenance:** Metadata verified from `https://doi.org/10.1016/j.tra.2024.104289`; full text is official proceedings, publisher, or institutional-author full text; peer-reviewed: **yes**.
- **Full-text audit:** 26 pages; 21,434 extracted words; SHA-256 `f08e8247f6a3fb9017103dbb1f6bdca84d8c9a42d4c6b63faca4ae6545a61ccd`.
- **Method:** Simulation-based integration of fixed bus lines with small flexible ride-pooling vehicles, adapting a receding-horizon trip-vehicle assignment and reducing fixed-line frequency where demand shifts.
- **Data/evaluation:** Four real bus lines in Santiago and Berlin, with observed operations, human-driven and automated cost assumptions, and fixed-only, mixed, and on-demand-only comparisons.
- **Numerical result or theorem:** A small automated flexible fleet can cut average walking from about 12 to 2 minutes while lowering operator cost; optimized mixed service reduces total cost by 13%-39%, and human-driven cases reduce total cost by more than 10% in all tested lines.
- **Limitations:** Origin/destination reconstruction, mode choice, congestion response, labor/automation cost, and collaboration between operators are modeled assumptions; the study does not establish real adoption.
- **Direct BeGo-LTF use:** Generate multimodal alternatives rather than forcing car-only plans. Score walking, waiting, transfer, and vehicle cost separately and present a Pareto archive so accessibility gains remain visible.
- **Quality tier:** **A** — Peer-reviewed publication with direct methodological relevance; transfer limitations are assessed in the structured review.

### 22. Beyond the last mile: different spatial strategies to integrate on-demand services into public transport in a simplified city

- **Citation:** Andrés Fielbaum, Sergio Jara‐Díaz, Javier Alonso–Mora (2024). *Public Transport*.
- **Persistent link:** https://doi.org/10.1007/s12469-023-00348-1; [landing page](https://doi.org/10.1007/s12469-023-00348-1); full PDF không phân phối trong repo (local corpus: pdfs/22-beyond-the-last-mile-different-spatial-strategies-to-integrate-on-de.pdf); structured review cục bộ (local corpus: structured-digests/22-structured-review.md); raw digest cục bộ (local corpus: digests/22-digest.txt).
- **Provenance:** Metadata verified from `https://doi.org/10.1007/s12469-023-00348-1`; full text is official proceedings, publisher, or institutional-author full text; peer-reviewed: **yes**.
- **Full-text audit:** 38 pages; 16,871 extracted words; SHA-256 `0d60acab99d06bb75d184c2d5ab41203e348a13302ab6e30a6d9d370f7c9901e`.
- **Method:** Continuous-approximation model of a stylized linear city comparing seven fixed/on-demand spatial integration structures: feeder-only, semi-direct, and direct services.
- **Data/evaluation:** Numerical sensitivity over demand volume/distribution, transfer penalty, vehicle size, and operator/user costs in the simplified city.
- **Numerical result or theorem:** The conventional feeder-trunk-feeder pattern is optimal only for narrow cases such as low-demand monocentric settings or negligible transfer penalties; direct and semi-direct structures often lower total cost by avoiding transfers and exploiting appropriate vehicle sizes.
- **Limitations:** Temporal homogeneity, a linear city, no explicit street graph, continuous vehicle capacities, and stylized demand make the results directional rather than deployment estimates.
- **Direct BeGo-LTF use:** Ensure the candidate factory includes direct, shared-pickup, and transit-transfer families. Do not assume the textbook first/last-mile topology dominates; test candidate-family ablations.
- **Quality tier:** **A** — Peer-reviewed publication with direct methodological relevance; transfer limitations are assessed in the structured review.

### 23. Trading off costs and service rates in a first-mile ride-sharing service

- **Citation:** Minyi Zheng, Giovanni Pantuso (2023). *Transportation Research Part C Emerging Technologies*.
- **Persistent link:** https://doi.org/10.1016/j.trc.2023.104099; [landing page](https://doi.org/10.1016/j.trc.2023.104099); full PDF không phân phối trong repo (local corpus: pdfs/23-trading-off-costs-and-service-rates-in-a-first-mile-ride-sharing-ser.pdf); structured review cục bộ (local corpus: structured-digests/23-structured-review.md); raw digest cục bộ (local corpus: digests/23-digest.txt).
- **Provenance:** Metadata verified from `https://doi.org/10.1016/j.trc.2023.104099`; full text is official proceedings, publisher, or institutional-author full text; peer-reviewed: **yes**.
- **Full-text audit:** 24 pages; 12,170 extracted words; SHA-256 `4597cee08cb12e1797621e77d07051c12551d5cf4117793208b673cd69250e92`.
- **Method:** Bi-objective mixed-integer formulation of first-mile ride-sharing and a specialized evolutionary algorithm using non-dominated sorting and problem-specific mutation/reproduction to approximate the cost-service Pareto front.
- **Data/evaluation:** Extensive instances generated from real-life first-mile data, with repeated long runs forming a reference non-dominated front.
- **Numerical result or theorem:** The algorithm covers more than 99% of the reference Pareto front produced by extensive runs, showing that a diverse archive can expose the service-rate versus operating-cost trade-off.
- **Limitations:** The reference front is produced by the same heuristic rather than an exact oracle, so 99% coverage is not a global optimality claim; the domain has one common destination and no historical fairness.
- **Direct BeGo-LTF use:** Maintain a diverse immutable PlanArchive before long-term selection. Benchmark archive recall against exact small instances and report archive coverage separately from DARS selector quality.
- **Quality tier:** **A** — Peer-reviewed publication with direct methodological relevance; transfer limitations are assessed in the structured review.

### 24. Alignment Work for Urban Accessibility: A Study of How Wheelchair Users Travel in Urban Spaces

- **Citation:** Yiying Wu, Xianghua Ding, Xuelan Dai, Peng Zhang, Tun Lu, Ning Gu (2022). *Proceedings of the ACM on Human-Computer Interaction, CSCW*.
- **Persistent link:** https://doi.org/10.1145/3555165; [landing page](https://eprints.gla.ac.uk/272079/); full PDF không phân phối trong repo (local corpus: pdfs/24-alignment-work-urban-accessibility-wheelchair-users.pdf); structured review cục bộ (local corpus: structured-digests/24-structured-review.md); raw digest cục bộ (local corpus: digests/24-digest.txt).
- **Provenance:** Metadata verified from `https://eprints.gla.ac.uk/272079/`; full text is University of Glasgow accepted manuscript; peer-reviewed: **yes**.
- **Full-text audit:** 23 pages; 15,359 extracted words; SHA-256 `be1fc35725dc906abd2c70c70256ea9d82ee93a67e0a58be045fb624f6735a42`.
- **Method:** One- to two-hour face-to-face semi-structured interviews, limited accompanied journeys/observation, transcription, and qualitative thematic analysis of everyday wheelchair travel practices.
- **Data/evaluation:** Fourteen wheelchair users in urban China, with demographic/context records and detailed narratives of planning, replanning, assistance, and breakdowns.
- **Numerical result or theorem:** Accessibility emerges from continuous alignment and realignment of companions, strangers, staff, assistive tools, facilities, policies, timing, and route knowledge; an environment label alone is insufficient.
- **Limitations:** Small self-selecting sample, socially active participants may be overrepresented, and the Chinese urban/resource context limits generalization; results are qualitative rather than an algorithm benchmark.
- **Direct BeGo-LTF use:** Support explicit assistance, companion, confidence, and replanning states. Explanations should reveal missing/uncertain resources and allow negotiation instead of claiming a route is universally accessible.
- **Quality tier:** **A** — Peer-reviewed PACM HCI qualitative study of 14 wheelchair users and real travel practices.

### 25. A comparison of reinforcement learning policies for dynamic vehicle routing problems with stochastic customer requests

- **Citation:** Fabian Akkerman, Martijn Mes, Willem van Jaarsveld (2025). *Computers & Industrial Engineering*.
- **Persistent link:** https://doi.org/10.1016/j.cie.2024.110747; [landing page](https://research.tue.nl/en/publications/a-comparison-of-reinforcement-learning-policies-for-dynamic-vehi); full PDF không phân phối trong repo (local corpus: pdfs/25-comparison-rl-policies-dynamic-vehicle-routing-stochastic-requests.pdf); structured review cục bộ (local corpus: structured-digests/25-structured-review.md); raw digest cục bộ (local corpus: digests/25-digest.txt).
- **Provenance:** Metadata verified from `https://research.tue.nl/en/publications/a-comparison-of-reinforcement-learning-policies-for-dynamic-vehi`; full text is TU/e institutional publisher PDF; peer-reviewed: **yes**.
- **Full-text audit:** 19 pages; 17,614 extracted words; SHA-256 `893372f55cc3a893f67898db99480adc49fc6cec28845fcb07fc8b66f72b79d5`.
- **Method:** Controlled comparison of LVFA, LPFA, NNVFA, NNPFA, PPO, and myopic policies across three decision-space variants, with engineered versus raw features and robustness tests.
- **Data/evaluation:** Stylized grids plus real-world-derived same-day parcel pickup in Amsterdam and automated storage/retrieval routing; source code and experimental settings are published.
- **Numerical result or theorem:** No architecture dominates: small problem changes reverse relative performance; NNPFA often beats NNVFA but costs more compute, while linear policies can win on large instances. Most anticipatory policies beat myopic and PPO baselines.
- **Limitations:** Results concern goods/robots, depend on MDP and feature design, and require simulation training; they do not test social-group fairness or real human acceptance.
- **Direct BeGo-LTF use:** Make a simple interpretable baseline mandatory before any AI module. Approve ML only if frozen tests show incremental benefit, calibrated uncertainty, acceptable latency, and no fairness/guard regression.
- **Quality tier:** **A** — Peer-reviewed controlled comparison of linear/neural, value/policy RL on several DVRP variants and realistic cases.

### 26. Open Area Path Finding to Improve Wheelchair Navigation

- **Citation:** Anahid Basiri (2020). *arXiv*.
- **Persistent link:** https://doi.org/10.48550/arXiv.2011.03850; [landing page](https://arxiv.org/abs/2011.03850); full PDF không phân phối trong repo (local corpus: pdfs/26-open-area-path-finding-wheelchair-navigation.pdf); structured review cục bộ (local corpus: structured-digests/26-structured-review.md); raw digest cục bộ (local corpus: digests/26-digest.txt).
- **Provenance:** Metadata verified from `https://arxiv.org/abs/2011.03850`; full text is primary arXiv full text; peer-reviewed: **no/preprint or workshop**.
- **Full-text audit:** 14 pages; 9,809 extracted words; SHA-256 `3cbc399ba74da6e62220f095be3dcd801eeffedf05fad1fe1e7cb5d5ae904b46`.
- **Method:** Hierarchical enhanced-visibility-graph construction plus trajectory mining and machine learning for wheelchair-specific edge weights, integrated with conventional graph routing.
- **Data/evaluation:** Nineteen wheelchair users recorded seven days of trajectories and then used suggested routes for eight days; routes are compared with Google Maps, OpenStreetMap, and unguided behavior using three trajectory-similarity measures.
- **Numerical result or theorem:** Sixteen of 19 participants were satisfied/extremely satisfied, 17 judged paths usable daily, and 14 found them replaceable. Depending on the metric, paths were 76.4%-89.8% closer to observed trajectories than Google and 78.5%-92.6% closer than OSM.
- **Limitations:** Preprint evidence, small short-duration sample, one accessibility mode, learned preferences and open-area geometry may not generalize, and scalability of multimodal integration remains untested.
- **Direct BeGo-LTF use:** Treat off-network/open-area walking as a separate optional routing adapter with confidence and user consent. Do not mix its learned score into the fairness debt until field-valid burden units are established.
- **Quality tier:** **B+** — Complete primary preprint with an implemented wheelchair-routing algorithm and user/trajectory evaluation; not used alone for a central claim.

### 27. Determining Accessible Sidewalk Width by Extracting Obstacle Information from Point Clouds

- **Citation:** Cláudia Fonseca Pinhão, Chris Eijgenstein, Iva Gornishka, Shayla Jansen, Diederik M. Roijers, Daan Bloembergen (2022). *ASSETS 2022 Workshop on the Future of Urban Accessibility*.
- **Persistent link:** https://doi.org/10.48550/arXiv.2211.04108; [landing page](https://openresearch.amsterdam/en/page/89378/determining-accessible-sidewalk-width-by-extracting-obstacle); full PDF không phân phối trong repo (local corpus: pdfs/27-determining-accessible-sidewalk-width-point-clouds.pdf); structured review cục bộ (local corpus: structured-digests/27-structured-review.md); raw digest cục bộ (local corpus: digests/27-digest.txt).
- **Provenance:** Metadata verified from `https://openresearch.amsterdam/en/page/89378/determining-accessible-sidewalk-width-by-extracting-obstacle`; full text is primary arXiv/City of Amsterdam workshop full text; peer-reviewed: **no/preprint or workshop**.
- **Full-text audit:** 4 pages; 3,076 extracted words; SHA-256 `713bc46e02364789f210cffd89b8bb93f982c7f4243cf318fe7aa16052c64996`.
- **Method:** Ground/height filtering, M3C2 change detection across two scans, obstacle clustering and polygonization, path extraction, and minimum-width measurement against municipal sidewalk polygons.
- **Data/evaluation:** City-scale Amsterdam point clouds and municipal BGT/AHN data, with width categories below 0.9 m, 0.9-1.8 m, 1.8-2.9 m, and above 2.9 m.
- **Numerical result or theorem:** Only 26.6% of measured obstacle-free widths exceed 2.9 m versus 47.3% of nominal full widths; 8.5% fall below 0.9 m versus 2.5% nominally. Sixteen percent of sub-0.9 m paths were nominally wider than 2.9 m.
- **Limitations:** Workshop/preprint, scans are snapshots, temporary obstacles and seasonal vegetation can be misclassified, unknown widths remain, and cross-city validation is absent.
- **Direct BeGo-LTF use:** Store effective width separately from nominal geometry, with observation time and provenance. Use it as a hard/person-specific accessibility filter only when confidence is sufficient; otherwise expose uncertainty.
- **Quality tier:** **B+** — Relevant ASSETS workshop paper with open code and municipal point-cloud validation; short and not a standalone source for effectiveness claims.

### 28. Exact matching of attractive shared rides (ExMAS) for system-wide strategic evaluations

- **Citation:** Rafał Kucharski, Oded Cats (2020). *Transportation Research Part B: Methodological*.
- **Persistent link:** https://doi.org/10.1016/j.trb.2020.06.006; [landing page](https://repository.tudelft.nl/record/uuid:6b0094de-1704-4d33-a9ac-2d0185671075); full PDF không phân phối trong repo (local corpus: pdfs/28-exact-matching-attractive-shared-rides-exmas.pdf); structured review cục bộ (local corpus: structured-digests/28-structured-review.md); raw digest cục bộ (local corpus: digests/28-digest.txt).
- **Provenance:** Metadata verified from `https://repository.tudelft.nl/record/uuid:6b0094de-1704-4d33-a9ac-2d0185671075`; full text is TU Delft institutional final published open-access PDF; peer-reviewed: **yes**.
- **Full-text audit:** 27 pages; 19,965 extracted words; SHA-256 `67d8cbfe002e98156ce95ab6929863adc10ca4ed004652c83b061c599f0f88cb`.
- **Method:** Utility-based attractive-ride filtering, predetermined pickup/drop-off sequence search, directed shareability multigraphs, and exact binary trip-ride assignment under passenger or operator objectives.
- **Data/evaluation:** Amsterdam travel demand with 500-3,000 trips per hour, sensitivity to discounts, planning horizon, demand, capacity, network speed, value of time, and willingness to share.
- **Numerical result or theorem:** For 3,000 trips and a 30% discount, 1,900 rides reach occupancy 1.67 and cut vehicle-hours about 30% while passenger-hours rise 17%. Filtering reduces a fourth-degree theoretical search near 1.55e16 to 226 searches; objective choice materially changes who benefits.
- **Limitations:** Demand-driven strategic analysis omits explicit fleet operations/rebalancing, uses deterministic utility parameters, and needs behavioral validation; exactness applies only within the defined attractiveness model.
- **Direct BeGo-LTF use:** Use reference-relative acceptability to prune candidates and exact-small archive construction. Preserve both participant and operator metrics and never call a plan attractive without showing the personal reference and assumptions.
- **Quality tier:** **A*** — Peer-reviewed Transportation Research Part B article with an exact, open, utility-aware ride-pooling method and Amsterdam evaluation.

### 29. Analyzing Accessibility Barriers Using Cost-Benefit Analysis to Design Reliable Navigation Services for Wheelchair Users

- **Citation:** Benjamin Tannert, Reuben Kirkham, Johannes Schöning (2019). *Lecture notes in computer science*.
- **Persistent link:** https://doi.org/10.1007/978-3-030-29381-9_13; [landing page](https://doi.org/10.1007/978-3-030-29381-9_13); full PDF không phân phối trong repo (local corpus: pdfs/29-analyzing-accessibility-barriers-using-cost-benefit-analysis-to-desi.pdf); structured review cục bộ (local corpus: structured-digests/29-structured-review.md); raw digest cục bộ (local corpus: digests/29-digest.txt).
- **Provenance:** Metadata verified from `https://doi.org/10.1007/978-3-030-29381-9_13`; full text is official proceedings, publisher, or institutional-author full text; peer-reviewed: **yes**.
- **Full-text audit:** 22 pages; 9,122 extracted words; SHA-256 `a0a0954fd3daf07cf6e29458a668e4eddb57218fd54ea8887d5ffa18683c05d3`.
- **Method:** Ground-truth cost-benefit evaluation of wheelchair A-to-B routes from OpenRouteService and Routino against Google pedestrian routes, including accessibility, distance, turns, overlap, and expert field inspection.
- **Data/evaluation:** Forty-five sampled route comparisons assessed for manual and powered wheelchair use in the study area, with confusion matrices and on-the-ground barrier verification.
- **Numerical result or theorem:** Specialist wheelchair routers were not significantly more likely to produce accessible routes; Routino was worse for powered wheelchairs, and specialist routes were often longer and more complex because of imaginary or stale barriers.
- **Limitations:** Small location-specific sample, only two specialist tools and Google, expert assessment rather than diverse end-user trials, and map products evolve over time.
- **Direct BeGo-LTF use:** Benchmark accessibility routing against simple mainstream baselines with field/audit truth. Penalize unsupported detours, publish false-safe and false-barrier rates, and show provenance/age rather than trusting an 'accessible' label.
- **Quality tier:** **A** — Peer-reviewed publication with direct methodological relevance; transfer limitations are assessed in the structured review.

### 30. Wheelmap: the wheelchair accessibility crowdsourcing platform

- **Citation:** Amin Mobasheri, Jonas Deister, Holger Dieterich (2017). *Open Geospatial Data Software and Standards*.
- **Persistent link:** https://doi.org/10.1186/s40965-017-0040-5; [landing page](https://doi.org/10.1186/s40965-017-0040-5); full PDF không phân phối trong repo (local corpus: pdfs/30-wheelmap-the-wheelchair-accessibility-crowdsourcing-platform.pdf); structured review cục bộ (local corpus: structured-digests/30-structured-review.md); raw digest cục bộ (local corpus: digests/30-digest.txt).
- **Provenance:** Metadata verified from `https://doi.org/10.1186/s40965-017-0040-5`; full text is official proceedings, publisher, or institutional-author full text; peer-reviewed: **yes**.
- **Full-text audit:** 7 pages; 3,928 extracted words; SHA-256 `e3fd63a0e4823bf7adb9bda515e3e5b6be48a54671f197eceaa0a73eb89d63d2`.
- **Method:** Technical and workflow description of Wheelmap's OpenStreetMap-backed crowdsourcing platform, REST API, simple traffic-light accessibility tags, photos, categories, and open contribution process.
- **Data/evaluation:** Operational platform evidence describing more than 800,000 community-rated points of interest at the time of publication.
- **Numerical result or theorem:** The platform demonstrates that a low-friction, open API and simple tagging model can scale accessibility contributions globally and support multimodal applications.
- **Limitations:** The article does not provide a systematic accuracy, recency, vandalism, inter-rater reliability, or route-outcome evaluation; free retagging and uneven coverage require downstream trust controls.
- **Direct BeGo-LTF use:** Ingest crowdsourced accessibility only with source, timestamp, confidence, disagreement, and moderation status. Never convert a single Wheelmap-style tag directly into an unqualified hard fact.
- **Quality tier:** **A** — Peer-reviewed publication with direct methodological relevance; transfer limitations are assessed in the structured review.

### 31. Demand-agnostic assessment of on-demand pooled transit services

- **Citation:** Olha Shulika, Hanna Vasiutina, Michał Bujak, Farnoud Ghasemi, Rafał Kucharski (2026). *European Transport Research Review*.
- **Persistent link:** https://doi.org/10.1186/s12544-026-00801-9; [landing page](https://link.springer.com/article/10.1186/s12544-026-00801-9); full PDF không phân phối trong repo (local corpus: pdfs/31-demand-agnostic-on-demand-pooled-transit.pdf); structured review cục bộ (local corpus: structured-digests/31-structured-review.md); raw digest cục bộ (local corpus: digests/31-digest.txt).
- **Provenance:** Metadata verified from `https://link.springer.com/article/10.1186/s12544-026-00801-9`; full text is author arXiv copy cross-checked against Springer version of record; peer-reviewed: **yes**.
- **Full-text audit:** 21 pages; 8,155 extracted words; SHA-256 `dd2d187b4d236bb1a8d2c9bab4e0b2226d90bd34fc785249f38baa732705a7bf`.
- **Method:** Population-based demand-fraction simulation, exact attractive shared-ride matching, three KPI threshold curves, and ranking of candidate area-hub pairs at the smallest viable demand fraction.
- **Data/evaluation:** Twelve low-density Krakow areas, address-point populations, candidate public-transport hubs, and demand fractions from very low uptake upward.
- **Numerical result or theorem:** Area 9 with Krakow Mydlniki ranks first; pooling appears from about 0.05% demand and the three KPI thresholds are met around 0.1%, 0.5%, and 0.7%. These are viability thresholds, explicitly not uptake forecasts.
- **Limitations:** ExMAS has no explicit fleet, assumes point-to-point feeder demand to one fixed hub, ignores mode choice and full journeys, and validates only one medium-sized European city.
- **Direct BeGo-LTF use:** Use threshold curves and scenario sweeps when real usage data are absent, clearly separating viability from prediction. Build synthetic group-sequence benchmarks before claiming deployment performance.
- **Quality tier:** **A** — Very recent peer-reviewed open-access study with a reproducible 12-area Kraków feeder-service case.

### 32. Learning for routing: A guided review of recent developments and future directions

- **Citation:** Fangting Zhou, Attila Lischka, Balázs Kulcsár, Jiaming Wu, Morteza Haghir Chehreghani (2025). *Transportation Research Part E: Logistics and Transportation Review*.
- **Persistent link:** https://doi.org/10.1016/j.tre.2025.104278; [landing page](https://research.chalmers.se/en/publication/547468); full PDF không phân phối trong repo (local corpus: pdfs/32-learning-for-routing-guided-review.pdf); structured review cục bộ (local corpus: structured-digests/32-structured-review.md); raw digest cục bộ (local corpus: digests/32-digest.txt).
- **Provenance:** Metadata verified from `https://research.chalmers.se/en/publication/547468`; full text is Chalmers institutional copy of the journal article; peer-reviewed: **yes**.
- **Full-text audit:** 36 pages; 30,883 extracted words; SHA-256 `426ca0a5b68473247b2c6cbf038194a5a63f0c4f1b175a68bc6f6d96b2884aad`.
- **Method:** Guided state-of-the-art review with a taxonomy of one-shot, incremental, heuristic, subproblem, and exact-algorithm-assisted learning methods for TSP/VRP.
- **Data/evaluation:** Published ML-for-routing studies and their reported synthetic/real datasets, instance sizes, baselines, optimality gaps, runtimes, and generalization experiments.
- **Numerical result or theorem:** Different ML families excel on different scales/problems; many models rely on uniformly sampled Euclidean instances and cannot be compared fairly because baselines and metrics differ. The review calls for realistic generators, standardization, and cross-distribution tests.
- **Limitations:** Reported performance is inherited from heterogeneous papers, the selected comparison table is explicitly non-exhaustive, and most evidence concerns classical routing rather than social fairness.
- **Direct BeGo-LTF use:** Keep ML optional, publish realistic clustered/grid/adversarial generators, and evaluate cross-size/cross-city generalization. Compare with strong OR heuristics and exact-small gaps, not only other neural models.
- **Quality tier:** **A** — Recent peer-reviewed guided review of construction/improvement ML for routing and hybrid OR-ML design.

### 33. Evaluating explainable social choice-based aggregation strategies for group recommendation

- **Citation:** Francesco Barile, Tim Draws, Oana Inel, Alisa Rieger, Shabnam Najafian, A. E. Fard, Rishav Hada, N. Tintarev (2023). *User modeling and user-adapted interaction*.
- **Persistent link:** https://doi.org/10.1007/s11257-023-09363-0; [landing page](https://doi.org/10.1007/s11257-023-09363-0); full PDF không phân phối trong repo (local corpus: pdfs/33-evaluating-explainable-social-choice-based-aggregation-strategies-fo.pdf); structured review cục bộ (local corpus: structured-digests/33-structured-review.md); raw digest cục bộ (local corpus: digests/33-digest.txt).
- **Provenance:** Metadata verified from `https://doi.org/10.1007/s11257-023-09363-0`; full text is official proceedings, publisher, or institutional-author full text; peer-reviewed: **yes**.
- **Full-text audit:** 58 pages; 23,568 extracted words; SHA-256 `cea2425d9913d33d216d89c64c387e0d197c0003eecad0c027f38b7322f8182e`.
- **Method:** Two preregistered user studies compare social-choice aggregation strategies and strategy-name textual explanations under uniform, minority, coalitional, and divergent preference configurations.
- **Data/evaluation:** Two online studies with N=399 and N=288 participants, measuring perceived fairness, consensus, satisfaction, and decision duration.
- **Numerical result or theorem:** No general benefit is found from social-choice-based explanations. Strategy effectiveness depends on group configuration: Most Pleasure should be avoided for minority groups, Fairness works well for uniform/coalitional groups, and Additive is safer when configuration is unclear.
- **Limitations:** Hypothetical/controlled groups and items reduce ecological validity, explanations were concise strategy descriptions, some strategy comparisons differ between studies, and real group discussion was absent.
- **Direct BeGo-LTF use:** Do not explain a plan by naming the algorithm. Provide participant-level burden, alternatives, guard status, and historical effect; test explanations with real groups and report null results.
- **Quality tier:** **A-** — Peer-reviewed publication with direct methodological relevance; transfer limitations are assessed in the structured review.

### 34. Sequential group recommendations based on satisfaction and disagreement scores

- **Citation:** Maria Stratigi, E. Pitoura, J. Nummenmaa, Kostas Stefanidis (2021). *Journal of Intelligence and Information Systems*.
- **Persistent link:** https://doi.org/10.1007/s10844-021-00652-x; [landing page](https://doi.org/10.1007/s10844-021-00652-x); full PDF không phân phối trong repo (local corpus: pdfs/34-sequential-group-recommendations-based-on-satisfaction-and-disagreem.pdf); structured review cục bộ (local corpus: structured-digests/34-structured-review.md); raw digest cục bộ (local corpus: digests/34-digest.txt).
- **Provenance:** Metadata verified from `https://doi.org/10.1007/s10844-021-00652-x`; full text is official proceedings, publisher, or institutional-author full text; peer-reviewed: **yes**.
- **Full-text audit:** 28 pages; 14,245 extracted words; SHA-256 `cc7ff210ac4b36f1e02e6cf81740a6d4e36326fb51d2ae309f0dbf5571b218e7`.
- **Method:** Three history-aware aggregation rules use prior satisfaction and within-group disagreement: SDAA switches group strategies, SIAA reweights individuals, and Average+ constructs a list incrementally to limit disagreement.
- **Data/evaluation:** MovieLens and GoodReads offline experiments for dissimilar, 4+1, and 3+2 groups under stable membership and ephemeral leave/join sequences.
- **Numerical result or theorem:** History-aware methods improve sequential satisfaction/fairness over static aggregation. SDAA is strongest in later stable rounds, while SIAA and Average+ are more robust for ephemeral groups; sparsity changes relative performance.
- **Limitations:** Groups and responses are simulated from ratings, satisfaction proxies are domain-specific, and there is no live negotiation or travel-cost burden.
- **Direct BeGo-LTF use:** Directly motivates persistent history, newcomer initialization, absent-member semantics, and separate stable/ephemeral benchmarks. BeGo uses normalized realized burden rather than movie-rating satisfaction.
- **Quality tier:** **A-** — Peer-reviewed publication with direct methodological relevance; transfer limitations are assessed in the structured review.

### 35. Rank-sensitive proportional aggregations in dynamic recommendation scenarios

- **Citation:** Stepán Balcar, Vít Škrhák, Ladislav Peška (2022). *User modeling and user-adapted interaction*.
- **Persistent link:** https://doi.org/10.1007/s11257-021-09311-w; [landing page](https://doi.org/10.1007/s11257-021-09311-w); full PDF không phân phối trong repo (local corpus: pdfs/35-rank-sensitive-proportional-aggregations-in-dynamic-recommendation-s.pdf); structured review cục bộ (local corpus: structured-digests/35-structured-review.md); raw digest cục bộ (local corpus: digests/35-digest.txt).
- **Provenance:** Metadata verified from `https://doi.org/10.1007/s11257-021-09311-w`; full text is official proceedings, publisher, or institutional-author full text; peer-reviewed: **yes**.
- **Full-text audit:** 62 pages; 27,866 extracted words; SHA-256 `17547e9b8c46fa1f922fe193740cd8aeb02cdf975ab4a53ff3c2f1aaf11dd13c`.
- **Method:** FuzzDA adapts D'Hondt proportional allocation to fuzzy item membership, rank sensitivity, repeated recommendation, contextual votes, and negative implicit feedback across multiple base recommenders.
- **Data/evaluation:** Offline simulations including MovieLens and production logs plus an online randomized A/B test, evaluated on CTR, iterative novelty, and per-user diversity.
- **Numerical result or theorem:** Variants often lie on or near the relevance-diversity Pareto front; contextual EP-FuzzDA attains the highest online CTR and per-user diversity among tested variants. Offline rankings do not fully predict online behavior.
- **Limitations:** Requires diverse overlapping base recommenders and partially stable preferences; noticeability and implicit-feedback assumptions may fail, and the offline-online gap remains unresolved.
- **Direct BeGo-LTF use:** Use a proportional-history baseline and report relevance/fairness trade-offs, but avoid quota lock-in. The failure of offline simulation to reproduce context supports an end-to-end replay track separate from selector-only tests.
- **Quality tier:** **A-** — Peer-reviewed publication with direct methodological relevance; transfer limitations are assessed in the structured review.

### 36. Explainable Fairness in Recommendation

- **Citation:** Yingqiang Ge, Juntao Tan, Yangchun Zhu, Yinglong Xia, Jiebo Luo, Shuchang Liu, Zuohui Fu, Shijie Geng, Zelong Li, Yongfeng Zhang (2022). *Annual International ACM SIGIR Conference on Research and Development in Information Retrieval*.
- **Persistent link:** https://doi.org/10.1145/3477495.3531973; [landing page](https://doi.org/10.1145/3477495.3531973); full PDF không phân phối trong repo (local corpus: pdfs/36-explainable-fairness-in-recommendation.pdf); structured review cục bộ (local corpus: structured-digests/36-structured-review.md); raw digest cục bộ (local corpus: digests/36-digest.txt).
- **Provenance:** Metadata verified from `https://doi.org/10.1145/3477495.3531973`; full text is official proceedings, publisher, or institutional-author full text; peer-reviewed: **yes**.
- **Full-text audit:** 11 pages; 10,482 extracted words; SHA-256 `6015d7857a26a20274229ffbe8c62b75bed22d2110728120be25cbc9cb646b31`.
- **Method:** Counterfactual Explainable Fairness (CEF) learns minimal feature perturbations that change exposure disparity, ranks features by fairness-utility trade-off, and uses them to guide fair retraining of feature-aware recommenders.
- **Data/evaluation:** Several public recommendation datasets, multiple recommender models, exposure-fairness metrics, feature-explanation baselines, and ablations for user/item representations.
- **Numerical result or theorem:** CEF identifies feature-level disparity drivers whose use in fair learning yields a better fairness-utility trade-off than tested explanation baselines.
- **Limitations:** Counterfactual features are model-level associations rather than causal explanations of human burden; evaluation targets exposure fairness and can choose only a limited explanation set greedily.
- **Direct BeGo-LTF use:** Use counterfactual perturbation as a developer audit: which input changes alter DARS selection? User-facing explanations must remain factual plan comparisons and must not claim causal fairness from model sensitivity.
- **Quality tier:** **A-** — Peer-reviewed publication with direct methodological relevance; transfer limitations are assessed in the structured review.

### 37. SAGA: A Submodular Greedy Algorithm For Group Recommendation

- **Citation:** S. Parambath, N. Vijayakumar, S. Chawla (2017). *AAAI Conference on Artificial Intelligence*.
- **Persistent link:** https://doi.org/10.1609/aaai.v32i1.11650; [landing page](https://doi.org/10.1609/aaai.v32i1.11650); full PDF không phân phối trong repo (local corpus: pdfs/37-saga-a-submodular-greedy-algorithm-for-group-recommendation.pdf); structured review cục bộ (local corpus: structured-digests/37-structured-review.md); raw digest cục bộ (local corpus: digests/37-digest.txt).
- **Provenance:** Metadata verified from `https://doi.org/10.1609/aaai.v32i1.11650`; full text is official proceedings, publisher, or institutional-author full text; peer-reviewed: **yes**.
- **Full-text audit:** 9 pages; 7,014 extracted words; SHA-256 `b0d891714f9c2c4231e6199173e73ac4400d845c1b83465aa4f02ae4ad6d6656`.
- **Method:** SAGA models selecting a fixed-size group recommendation bundle as monotone submodular maximization over item affinity, group relevance, coverage, and user saturation, solved greedily.
- **Data/evaluation:** MovieLens 1M with 2,945 sufficiently active users and 3,670 movies; random/similar synthetic groups, holdout validation, and comparisons with average/fairness baselines.
- **Numerical result or theorem:** The greedy algorithm has a (1-1/e) approximation guarantee for the defined monotone submodular objective and improves relevance/coverage metrics over tested group recommenders, particularly for larger groups.
- **Limitations:** The guarantee is objective-specific; groups are synthesized, evidence is offline and single-domain, and bundle diversity is not repeated interpersonal fairness.
- **Direct BeGo-LTF use:** Candidate-set diversity can use submodular selection after feasibility, but DARS should choose from the archive with its own lexicographic history objective. Never transfer SAGA's guarantee across objectives.
- **Quality tier:** **A-** — Peer-reviewed publication with direct methodological relevance; transfer limitations are assessed in the structured review.

### 38. LINet: A Location and Intention-Aware Neural Network for Hotel Group Recommendation

- **Citation:** Ruitao Zhu, Detao Lv, Yao Yu, Ruihao Zhu, Zhenzhe Zheng, Ke Bu, Quan Lu, Fan Wu (2023). *The Web Conference*.
- **Persistent link:** https://doi.org/10.1145/3543507.3583202; [landing page](https://doi.org/10.1145/3543507.3583202); full PDF không phân phối trong repo (local corpus: pdfs/38-linet-a-location-and-intention-aware-neural-network-for-hotel-group.pdf); structured review cục bộ (local corpus: structured-digests/38-structured-review.md); raw digest cục bộ (local corpus: digests/38-digest.txt).
- **Provenance:** Metadata verified from `https://doi.org/10.1145/3543507.3583202`; full text is official proceedings, publisher, or institutional-author full text; peer-reviewed: **yes**.
- **Full-text audit:** 11 pages; 9,650 extracted words; SHA-256 `ba58348d2e05bfdcd548b8bb8598cf6bc1ed0e5d53a6e1df02e32124cb430eab`.
- **Method:** LINet forms travel-intention user groups and combines recent interactions, long-term graph representations, location/time features, and an auxiliary location loss to rank hotel inventory opportunities.
- **Data/evaluation:** Large Fliggy production data, sparse-data experiments, ablations, offline metrics, and a June 2022 online comparison across city groups.
- **Numerical result or theorem:** LINet improves offline top-50 measures by at least about 2.3%-3% over the best baseline and produces an average 3.2% online lift in room nights; it was deployed in Fliggy operations.
- **Limitations:** Commercial supply-side objective, proprietary data and group formation, neural opacity, and observational/product effects limit transfer to social outing fairness.
- **Direct BeGo-LTF use:** Spatiotemporal intention features may help optional venue retrieval, but keep ranking independent of fairness debt and require a public/simple retrieval baseline plus privacy review.
- **Quality tier:** **A-** — Peer-reviewed publication with direct methodological relevance; transfer limitations are assessed in the structured review.

### 39. Integrating Collaboration and Leadership in Conversational Group Recommender Systems

- **Citation:** David Contreras, Maria Salamó, Ludovico Boratto (2021). *ACM Transactions on Information Systems*.
- **Persistent link:** https://doi.org/10.1145/3462759; [landing page](https://doi.org/10.1145/3462759); full PDF không phân phối trong repo (local corpus: pdfs/39-integrating-collaboration-and-leadership-in-conversational-group-rec.pdf); structured review cục bộ (local corpus: structured-digests/39-structured-review.md); raw digest cục bộ (local corpus: digests/39-digest.txt).
- **Provenance:** Metadata verified from `https://doi.org/10.1145/3462759`; full text is official proceedings, publisher, or institutional-author full text; peer-reviewed: **yes**.
- **Full-text audit:** 33 pages; 19,912 extracted words; SHA-256 `fc66f15d5518f7f836b5bac5c4c2d12d2f38c0b21b06baf7bbde031df8b966ad`.
- **Method:** A web-based conversational group recommender records individual and social interactions, infers collaboration/leadership scores, and compares collaboration-based consensus strategies with classical aggregation.
- **Data/evaluation:** Live study with 68 university participants in 17 groups of four choosing skiing packages, followed by role nomination, conflict-style, usefulness, and satisfaction questionnaires.
- **Numerical result or theorem:** The inferred leader matches the group's most-voted leader in all 17 groups, and leader/collaboration-aware strategies receive higher recommendation ratings than classical strategies in this study.
- **Limitations:** Small homogeneous student sample, one skiing domain, pandemic-era online context, and post-hoc leader labels; 100% is 17 groups, not a general accuracy guarantee.
- **Direct BeGo-LTF use:** Provide negotiation and proposal provenance, but do not automatically grant a persistent leader extra normative weight. If roles are surfaced, make them session-local, user-correctable, and separately consented.
- **Quality tier:** **A-** — Peer-reviewed publication with direct methodological relevance; transfer limitations are assessed in the structured review.

### 40. DeepGroup: Group Recommendation with Implicit Feedback

- **Citation:** Sarina Sajadi Ghaemmaghami, Amirali Salehi-Abari (2021). *ACM CIKM 2021*.
- **Persistent link:** https://doi.org/10.1145/3459637.3482081; [landing page](https://doi.org/10.1145/3459637.3482081); full PDF không phân phối trong repo (local corpus: pdfs/40-deepgroup-group-recommendation-with-implicit-feedback.pdf); structured review cục bộ (local corpus: structured-digests/40-structured-review.md); raw digest cục bộ (local corpus: digests/40-digest.txt).
- **Provenance:** Metadata verified from `https://doi.org/10.1145/3459637.3482081`; full text is official proceedings, publisher, or institutional-author full text; peer-reviewed: **yes**.
- **Full-text audit:** 5 pages; 4,824 extracted words; SHA-256 `69829d4710b0983cbb3fa0bc65a002b379dd26ef3f080f4ab094698be6f15fdd`.
- **Method:** DeepGroup learns group decisions from group membership and group-level implicit feedback, addressing new-group decision prediction and reverse social choice without observed individual preferences.
- **Data/evaluation:** Four real preference datasets (three Irish election datasets and sushi rankings), synthetically formed overlapping groups, and plurality, Borda, or mixed decision rules.
- **Numerical result or theorem:** DeepGroup outperforms adapted baselines in the tested prediction tasks and shows that published group decisions can leak concealed individual preferences; plurality leaks more than Borda in the experiments.
- **Limitations:** Group observations are synthetically generated from individual rankings, the paper is short, real social decisions may differ, and privacy leakage is empirical rather than a formal bound.
- **Direct BeGo-LTF use:** Minimize stored preference and explanation detail, separate public group outcomes from private burdens/debt, restrict analytics access, and explicitly test membership/decision inference attacks before release.
- **Quality tier:** **A-** — Peer-reviewed publication with direct methodological relevance; transfer limitations are assessed in the structured review.
