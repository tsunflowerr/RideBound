# BeGo-LTF extra80 / part-a — full-text evidence matrix

This matrix contains **exactly 40 new papers** on temporal/long-term fairness, repeated allocation, constrained sequential decisions, fairness debt/virtual queues, fair bandits, dynamic participation, and ridesharing mechanisms.

- Full PDFs downloaded and extracted: **40/40**
- Total pages read: **610**
- Total extracted words scanned: **403,363**
- Low-text or failed PDFs: **0**
- Byte-identical PDFs inside this corpus: **0**
- Canonical-title/DOI overlap with corpus90: **0**
- Every paper has a page-aware full-document digest and passed a rendered first-page visual audit.

## Quality rubric

- **A\***: flagship/top peer-reviewed venue with strong theory or empirical evidence.
- **A**: strong peer-reviewed venue/journal and directly usable evidence.
- **A-**: peer-reviewed and useful, but domain transfer or external validity is limited.
- **B+**: complete, relevant recent preprint/synthesis; use cautiously pending peer review or validation.

## Cross-paper synthesis for BeGo-LTF

The evidence supports a deliberately narrow core contribution: **long-term mobility-burden fairness with per-trip safety guardrails**, implemented first as a deterministic optimizer plus virtual debt queues. Full RL, causal bandits, federated learning, and monetary mechanism design are valuable extensions, but making them all mandatory would weaken feasibility and evaluation clarity.

Recommended state for member *i* after outing *t*:

`D_i,k(t+1) = max(0, rho_k * D_i,k(t) + realized_burden_i,k(t) - entitlement_i,k(t))`

where burden dimensions *k* include walking, waiting, passenger time, driver detour, driving-role frequency, and preference regret. Normalize into minutes-equivalent only for the scalar optimizer; keep raw dimensions for audit. Score each feasible candidate with immediate group cost plus a queue-weighted predicted burden term, a maximum-post-decision-debt term, and optionally a generalized-Gini/alpha-fair horizon welfare term.

The two-layer guarantee is essential:

1. **Per-trip guardrails:** accessibility, maximum walk/wait/detour, capacity, and maximum incremental debt are hard constraints (papers 3, 9, 10, 17, 39).
2. **Horizon fairness:** virtual queues repay cumulative burden when the contextual efficiency cost is smallest (papers 12, 16, 27, 28), with an optional deadline such as “compensate within L outings” (paper 29).

Evaluate against: current static BeGo score; current score plus max-min/Gini; round-robin driver/role; per-slot fairness; the proposed virtual-queue LTF; and an offline horizon MILP oracle. Report mean social cost, price of fairness, maximum and Gini debt, 95th-percentile walk/wait/detour, prefix unfairness, time-to-repayment, fairness regret to the offline oracle, hard-constraint violations, and retention/participation. Papers 16 and 22 make an efficiency loss below roughly 4-5% a useful research target, not a guaranteed result.

Key ablations are no debt decay, no contextual repayment, no hard cap, scalar versus multi-dimensional debt, and planned versus realized burden. A publishable behavioral extension models attendance or willingness-to-drive as a function of prior debt (papers 13-15 and 35).

## Paper-by-paper evidence

### 01. Fairness and Sequential Decision Making: Limits, Lessons, and Opportunities

- **Citation:** Samer B. Nashed, Justin Svegliato, Su Lin Blodgett (2023). *CoRR / arXiv*.
- **Persistent link:** https://doi.org/10.48550/arXiv.2301.05753; [landing page](https://arxiv.org/abs/2301.05753); full PDF không phân phối trong repo (local corpus: pdfs/01-fairness-and-sequential-decision-making-limits-lessons-and-opportunities.pdf); structured review cục bộ (local corpus: structured-digests/01-structured-review.md); raw digest cục bộ (local corpus: digests/01-digest.txt).
- **Provenance:** Metadata verified from `https://arxiv.org/abs/2301.05753`; full text is primary arXiv preprint; peer-reviewed: **no/preprint**.
- **Full-text audit:** 15 pages; 19,639 extracted words; SHA-256 `115d7418ad5902523be6466ad7df6883c3dc79c635b78ff8215f9b6466568b91`.
- **Method:** Cross-community critical review comparing algorithmic fairness for predictors with ethical sequential decision making and planning; analyzes normative targets, observability, intervention points, and evaluation difficulty.
- **Data/evaluation:** No new dataset or experiment; the evidence is a full literature synthesis across fairness, planning, and robot ethics.
- **Numerical result or theorem:** No standalone theorem or effect size. The central result is a reasoned limit: model/reward fidelity, protected-attribute access, and deployed outcome measurement can dominate the choice of a fairness metric.
- **Limitations:** Position/survey paper rather than a validated algorithm; recommendations remain conceptual and context dependent.
- **Direct BeGo-LTF use:** Use as the governance frame: document whose burden counts, audit realized mobility outcomes rather than only optimizer scores, and separate mathematical feasibility from the normative choice of fairness target.
- **Quality tier:** **B+** — Full survey/position paper with broad conceptual coverage, but not a peer-reviewed empirical contribution.

### 02. Adapting Static Fairness to Sequential Decision-Making: Bias Mitigation Strategies towards Equal Long-term Benefit Rate

- **Citation:** Yuancheng Xu, Chenghao Deng, Yanchao Sun, Ruijie Zheng, Xiyao Wang, Jieyu Zhao, Furong Huang (2024). *International Conference on Machine Learning (ICML), PMLR 235*.
- **Persistent link:** No DOI assigned; [landing page](https://proceedings.mlr.press/v235/xu24g.html); full PDF không phân phối trong repo (local corpus: pdfs/02-adapting-static-fairness-to-sequential-decision-making-bias-mitigation-s.pdf); structured review cục bộ (local corpus: structured-digests/02-structured-review.md); raw digest cục bộ (local corpus: digests/02-digest.txt).
- **Provenance:** Metadata verified from `https://proceedings.mlr.press/v235/xu24g.html`; full text is official publisher/proceedings full text; peer-reviewed: **yes**.
- **Full-text audit:** 21 pages; 14,152 extracted words; SHA-256 `2d6759e4a65481e89e5c92a96b115090d60ed47ee94ad25bb05094ccfbd1e28b`.
- **Method:** Defines Equal Long-term Benefit Rate (ELBERT), embeds a benefit-to-demand ratio in an MDP, derives a policy-gradient-compatible advantage, and implements ELBERT policy optimization on PPO-style learners.
- **Data/evaluation:** Multiple simulated sequential decision environments with demographic groups, supply, demand, and delayed outcomes; ablations compare group, reward-only, and alternative long-term objectives.
- **Numerical result or theorem:** The reported best residual bias is about 0.02; ELBERT-PO reduces bias by 87.5% versus G-PPO and by more than 75% versus R-PPO/A-PPO while preserving high utility.
- **Limitations:** Ratio fairness can hide low absolute benefit and depends on correctly defining demand; all validation is simulated and model misspecification is not resolved.
- **Direct BeGo-LTF use:** Define each member's cumulative benefit/burden rate over outings. The analytic ratio objective is useful, but BeGo should retain absolute accessibility and maximum-burden caps so a low ratio is not achieved by under-serving everyone.
- **Quality tier:** **A*** — Flagship peer-reviewed venue; formal derivation plus experiments across several sequential environments.

### 03. Reinforcement Learning with Stepwise Fairness Constraints

- **Citation:** Zhun Deng, He Sun, Zhiwei Steven Wu, Linjun Zhang, David C. Parkes (2023). *International Conference on Artificial Intelligence and Statistics (AISTATS), PMLR 206*.
- **Persistent link:** No DOI assigned; [landing page](https://proceedings.mlr.press/v206/deng23a.html); full PDF không phân phối trong repo (local corpus: pdfs/03-reinforcement-learning-with-stepwise-fairness-constraints.pdf); structured review cục bộ (local corpus: structured-digests/03-structured-review.md); raw digest cục bộ (local corpus: digests/03-digest.txt).
- **Provenance:** Metadata verified from `https://proceedings.mlr.press/v206/deng23a.html`; full text is official publisher/proceedings full text; peer-reviewed: **yes**.
- **Full-text audit:** 25 pages; 13,636 extracted words; SHA-256 `d6e1f75c54933f03ab9126374cf92975c52527d6f450704cc701c46b721d5c83`.
- **Method:** Tabular episodic model-based RL with confidence sets and demographic-parity/equal-opportunity constraints enforced at every step; derives finite-time reward-regret and fairness-violation bounds.
- **Data/evaluation:** Synthetic FICO-based population dynamics over an eight-step horizon, including Pareto frontiers and confidence-width ablations.
- **Numerical result or theorem:** Both reward regret and cumulative fairness violation vanish sublinearly with episodes under the stated tabular assumptions; the FICO experiments use horizon H=8 and show the expected reward-fairness Pareto frontier.
- **Limitations:** Tabular and group-metric specific; nonconvex constraints and larger continuous mobility states are not solved, and only two static notions are instantiated.
- **Direct BeGo-LTF use:** Use per-trip fairness as a hard guardrail around the long-horizon ledger: maximum walk, wait, detour, accessibility violation, and extreme incremental debt should never be traded away for future compensation.
- **Quality tier:** **A*** — Top peer-reviewed venue with finite-time reward-regret and fairness-violation guarantees.

### 04. Fair Resource Allocation in Weakly Coupled Markov Decision Processes

- **Citation:** Xiaohui Tu, Yossiri Adulyasak, Nima Akbarzadeh, Erick Delage (2025). *International Conference on Artificial Intelligence and Statistics (AISTATS), PMLR 258*.
- **Persistent link:** No DOI assigned; [landing page](https://proceedings.mlr.press/v258/tu25a.html); full PDF không phân phối trong repo (local corpus: pdfs/04-fair-resource-allocation-in-weakly-coupled-markov-decision-processes.pdf); structured review cục bộ (local corpus: structured-digests/04-structured-review.md); raw digest cục bộ (local corpus: digests/04-digest.txt).
- **Provenance:** Metadata verified from `https://proceedings.mlr.press/v258/tu25a.html`; full text is official publisher/proceedings full text; peer-reviewed: **yes**.
- **Full-text audit:** 23 pages; 14,061 extracted words; SHA-256 `c8471e7fff3c6072cf0c461013e4c2d964766463753d71386c0f81ed6af59956`.
- **Method:** Models N weakly coupled sub-MDPs with a generalized-Gini social-welfare objective; provides an exact occupancy-measure LP, a homogeneous-case utilitarian reduction, and a count-proportion deep-RL approximation.
- **Data/evaluation:** Synthetic machine/resource allocation, exact tests for N=3 to 5 and larger scalability experiments with multiple reward-cost curves.
- **Numerical result or theorem:** Theorem 3.4 shows that for symmetric sub-MDPs the generalized-Gini problem reduces to utilitarian optimization over permutation-invariant policies; CP-DRL consistently improves GGF scores over Whittle/vanilla baselines in the reported tests.
- **Limitations:** The general exact LP grows exponentially; experiments are synthetic and homogeneous symmetry is much stronger than a real group of travelers.
- **Direct BeGo-LTF use:** Use a generalized-Gini or ordered-weighted aggregation of members' cumulative utilities, and exploit exchangeability/count states only for genuinely similar members rather than forcing false symmetry.
- **Quality tier:** **A*** — Recent top-venue work combining a reduction theorem, exact formulation, and scalable experiments.

### 05. Learning Fair Policies in Decentralized Cooperative Multi-Agent Reinforcement Learning

- **Citation:** Matthieu Zimmer, Claire Glanois, Umer Siddique, Paul Weng (2021). *International Conference on Machine Learning (ICML), PMLR 139*.
- **Persistent link:** No DOI assigned; [landing page](https://proceedings.mlr.press/v139/zimmer21a.html); full PDF không phân phối trong repo (local corpus: pdfs/05-learning-fair-policies-in-decentralized-cooperative-multi-agent-reinforc.pdf); structured review cục bộ (local corpus: structured-digests/05-structured-review.md); raw digest cục bộ (local corpus: digests/05-digest.txt).
- **Provenance:** Metadata verified from `https://proceedings.mlr.press/v139/zimmer21a.html`; full text is official publisher/proceedings full text; peer-reviewed: **yes**.
- **Full-text audit:** 12 pages; 8,458 extracted words; SHA-256 `53fcb8c955dd56847e31af8e4d0e27acbdeee85c271eb0125b60636f2cf0fe0d`.
- **Method:** SOTO cooperative MARL optimizes differentiable social-welfare functions through self-oriented and team-oriented subnetworks; includes policy-gradient convergence analysis for efficiency-equity objectives.
- **Data/evaluation:** Matthew Effect, SUMO distributed traffic lights, and data-center control, compared with Independent, FEN, COMA, WQMIX, centralized-critic, and value-based methods.
- **Numerical result or theorem:** Theorem 5.1 establishes convergence to a stationary point under standard smoothness/step-size assumptions; across the tested domains SOTO reaches better efficiency-equity regions and lower coefficient of variation than the principal baselines.
- **Limitations:** Assumes a useful per-agent satisfaction signal, incurs MARL training complexity, and has no group-mobility or human-participation deployment.
- **Direct BeGo-LTF use:** Adopt the welfare-function idea (generalized Gini, alpha-fairness, Pigou-Dalton transfers) in BeGo's deterministic optimizer; full MARL is an optional later extension, not an MVP dependency.
- **Quality tier:** **A*** — Flagship peer-reviewed MARL paper with convergence analysis and several control domains.

### 06. Fairness in Reinforcement Learning

- **Citation:** Shahin Jabbari, Matthew Joseph, Michael Kearns, Jamie Morgenstern, Aaron Roth (2017). *International Conference on Machine Learning (ICML), PMLR 70*.
- **Persistent link:** No DOI assigned; [landing page](https://proceedings.mlr.press/v70/jabbari17a.html); full PDF không phân phối trong repo (local corpus: pdfs/06-fairness-in-reinforcement-learning.pdf); structured review cục bộ (local corpus: structured-digests/06-structured-review.md); raw digest cục bộ (local corpus: digests/06-digest.txt).
- **Provenance:** Metadata verified from `https://proceedings.mlr.press/v70/jabbari17a.html`; full text is official publisher/proceedings full text; peer-reviewed: **yes**.
- **Full-text audit:** 10 pages; 7,946 extracted words; SHA-256 `67dfb36ef4f48a6bce937894883f38bda482655b5037fec5c281c6b050ef7c15`.
- **Method:** Formalizes meritocratic fairness in an MDP, proves a hardness gap for exact fairness, and gives an approximately action-fair variant of E3 with polynomial dependence on the MDP size.
- **Data/evaluation:** Theory paper; illustrative MDP constructions rather than an empirical mobility dataset.
- **Numerical result or theorem:** Exact long-term fairness can require time exponential in the number of states before nontrivial optimality, whereas approximate-action fairness admits polynomial time except for unavoidable dependence on tolerance.
- **Limitations:** The merit-based pairwise notion is weak enough to permit some conditional discrimination; tabular assumptions and exact merit ordering are not realistic for BeGo preferences.
- **Direct BeGo-LTF use:** Do not promise exact pairwise fairness at every decision. Use bounded approximate debt, tolerance bands, and transparent hard constraints so the algorithm remains computationally and behaviorally feasible.
- **Quality tier:** **A*** — Foundational flagship-venue theory on the computational cost of exact sequential fairness.

### 07. Regret Guarantees for Model-Based Reinforcement Learning with Long-Term Average Constraints

- **Citation:** Mridul Agarwal, Qinbo Bai, Vaneet Aggarwal (2022). *Conference on Uncertainty in Artificial Intelligence (UAI), PMLR 180*.
- **Persistent link:** No DOI assigned; [landing page](https://proceedings.mlr.press/v180/agarwal22b.html); full PDF không phân phối trong repo (local corpus: pdfs/07-regret-guarantees-for-model-based-reinforcement-learning-with-long-term.pdf); structured review cục bộ (local corpus: structured-digests/07-structured-review.md); raw digest cục bộ (local corpus: digests/07-digest.txt).
- **Provenance:** Metadata verified from `https://proceedings.mlr.press/v180/agarwal22b.html`; full text is official publisher/proceedings full text; peer-reviewed: **yes**.
- **Full-text audit:** 10 pages; 6,914 extracted words; SHA-256 `56ff754093db11900bc71e086931ec7ee498c56a58f451ab310639fc679506dd`.
- **Method:** CMDP-PSRL uses posterior sampling for unknown ergodic CMDPs with K long-run average cost constraints and a slackness condition.
- **Data/evaluation:** Queue/MDP simulations support the finite-time theory; no human or mobility deployment.
- **Numerical result or theorem:** Reward regret and each constraint violation are bounded by roughly O(T_M S sqrt(A T)), with an additional slack/mixing-dependent term in the full bound.
- **Limitations:** Requires ergodicity, a feasible slack margin, model-based posterior sampling, and mixing-time control; a comparable model-free result is left open.
- **Direct BeGo-LTF use:** Treat average member debt as a long-run CMDP constraint if a transition model becomes available, but start with direct virtual queues because group mobility data will initially be too sparse for reliable model-based RL.
- **Quality tier:** **A** — Strong peer-reviewed theory for average-constrained ergodic MDPs; evaluation is simulation-only.

### 08. Online Learning in CMDPs: Handling Stochastic and Adversarial Constraints

- **Citation:** Francesco Emanuele Stradi, Jacopo Germano, Gianmarco Genalti, Matteo Castiglioni, Alberto Marchesi, Nicola Gatti (2024). *International Conference on Machine Learning (ICML), PMLR 235*.
- **Persistent link:** No DOI assigned; [landing page](https://proceedings.mlr.press/v235/stradi24a.html); full PDF không phân phối trong repo (local corpus: pdfs/08-online-learning-in-cmdps-handling-stochastic-and-adversarial-constraints.pdf); structured review cục bộ (local corpus: structured-digests/08-structured-review.md); raw digest cục bộ (local corpus: digests/08-digest.txt).
- **Provenance:** Metadata verified from `https://proceedings.mlr.press/v235/stradi24a.html`; full text is official publisher/proceedings full text; peer-reviewed: **yes**.
- **Full-text audit:** 30 pages; 15,747 extracted words; SHA-256 `196f05942991019dfb74607297062b70c1afa091a20e4922e6b66e7391344bb8`.
- **Method:** Primal-dual occupancy-measure online learning with unknown transitions; automatically handles stochastic or adversarial reward/constraint sequences.
- **Data/evaluation:** Theory and synthetic CMDP evaluation; no real mobility data.
- **Numerical result or theorem:** With a Slater condition it obtains approximately O(sqrt(T)) regret and constraint violation; without it the rate degrades to approximately O(T^(3/4)), and it provides the first guarantees for adversarial constraints in this setting.
- **Limitations:** Complex confidence-set and primal-dual updates, episodic finite-state assumptions, and limited empirical external validity.
- **Direct BeGo-LTF use:** Use as robustness justification for a primal-dual debt controller under seasonality or adversarial attendance; BeGo can implement the simpler queue analogue and stress-test stochastic versus bursty group sequences.
- **Quality tier:** **A*** — Flagship peer-reviewed best-of-both-worlds CMDP result for stochastic and adversarial constraints.

### 09. Anytime-Constrained Reinforcement Learning

- **Citation:** Jeremy McMahan, Xiaojin Zhu (2024). *International Conference on Artificial Intelligence and Statistics (AISTATS), PMLR 238*.
- **Persistent link:** No DOI assigned; [landing page](https://proceedings.mlr.press/v238/mcmahan24a.html); full PDF không phân phối trong repo (local corpus: pdfs/09-anytime-constrained-reinforcement-learning.pdf); structured review cục bộ (local corpus: structured-digests/09-structured-review.md); raw digest cục bộ (local corpus: digests/09-digest.txt).
- **Provenance:** Metadata verified from `https://proceedings.mlr.press/v238/mcmahan24a.html`; full text is official publisher/proceedings full text; peer-reviewed: **yes**.
- **Full-text audit:** 29 pages; 18,676 extracted words; SHA-256 `164b1b403f493cdceae7db315e27620724a84a7b47c430743e23efd7790a3c8e`.
- **Method:** Augments a constrained MDP state with cumulative cost so an anytime budget must hold almost surely at every prefix; gives exact fixed-parameter and approximate planning/learning reductions plus hardness results.
- **Data/evaluation:** Tabular timing experiments over horizon/budget/precision settings, including exact and approximate solvers.
- **Numerical result or theorem:** Optimal policies can be deterministic in the cost-augmented state; the general approximation problem is NP-hard, while the bounded-precision reduction is fixed-parameter tractable. One reported approximation handles H=100 in about 2 seconds whereas an exact H=B=15 case takes about one minute.
- **Limitations:** Tabular bounded-cost setting; state augmentation can explode and approximate feasibility is not the same as exact accessibility safety.
- **Direct BeGo-LTF use:** Maintain an explicit cumulative-burden state and reject candidates that cross an anytime cap. This is distinct from minimizing average unfairness and prevents 'we will compensate later' from excusing a severe current trip.
- **Quality tier:** **A*** — Top-venue hardness, exact reduction, and approximation results for constraints that must hold at every prefix.

### 10. Constrained Markov Decision Processes via Backward Value Functions

- **Citation:** Harsh Satija, Philip Amortila, Joelle Pineau (2020). *International Conference on Machine Learning (ICML), PMLR 119*.
- **Persistent link:** No DOI assigned; [landing page](https://proceedings.mlr.press/v119/satija20a.html); full PDF không phân phối trong repo (local corpus: pdfs/10-constrained-markov-decision-processes-via-backward-value-functions.pdf); structured review cục bộ (local corpus: structured-digests/10-structured-review.md); raw digest cục bộ (local corpus: digests/10-digest.txt).
- **Provenance:** Metadata verified from `https://proceedings.mlr.press/v119/satija20a.html`; full text is official publisher/proceedings full text; peer-reviewed: **yes**.
- **Full-text audit:** 10 pages; 6,519 extracted words; SHA-256 `c194b6913c833263cdfcb9ccb85e7f2760339fc22da89e12814c29c6d9858aff`.
- **Method:** Backward value functions transform a trajectory-level cumulative constraint into local state constraints, yielding an on-policy safe policy-improvement layer for A2C/PPO.
- **Data/evaluation:** 2-D grid navigation plus MuJoCo Point-Gather, SafeCheetah, and Point-Circle tasks; comparisons with unconstrained and Lyapunov approaches.
- **Numerical result or theorem:** Theorems 3.1 and 3.2 establish consistent feasibility and monotone policy improvement within the safe neighborhood; figures report means with 80% confidence intervals and show lower constraint cost than unconstrained learning.
- **Limitations:** Safe improvement is local and recovery is needed from an unsafe/random start; the experiments use discount 0.99 despite parts of the undiscounted analysis.
- **Direct BeGo-LTF use:** Add a feasibility filter before scoring routes: transform remaining walk/wait/detour/debt budget into action-local candidate checks, then optimize fairness and efficiency only over safe plans.
- **Quality tier:** **A*** — Flagship peer-reviewed safe-RL formulation with convergence results and multiple continuous-control tests.

### 11. Constrained Reinforcement Learning via Policy Splitting

- **Citation:** Haoxian Chen, Henry Lam, Fengpei Li, Amirhossein Meisami (2020). *Asian Conference on Machine Learning (ACML), PMLR 129*.
- **Persistent link:** No DOI assigned; [landing page](https://proceedings.mlr.press/v129/chen20b.html); full PDF không phân phối trong repo (local corpus: pdfs/11-constrained-reinforcement-learning-via-policy-splitting.pdf); structured review cục bộ (local corpus: structured-digests/11-structured-review.md); raw digest cục bộ (local corpus: digests/11-digest.txt).
- **Provenance:** Metadata verified from `https://proceedings.mlr.press/v129/chen20b.html`; full text is official publisher/proceedings full text; peer-reviewed: **yes**.
- **Full-text audit:** 16 pages; 7,227 extracted words; SHA-256 `d9b41f1897d4bf3fb746fdbf0a685611cc565a6296e460b267790e1667c6457f`.
- **Method:** Two-stage CMDP learning: find deterministic policies by dual Q-learning, then mix two policies using policy splitting to meet a discounted budget.
- **Data/evaluation:** Six months of sponsored-search data, 10 keyword groups, and a normalized budget around 0.45.
- **Numerical result or theorem:** The structural result states that an optimal randomized policy is deterministic or a mixture of two deterministic policies differing at one state under the paper's conditions; experiments recover near-feasible high-return mixtures.
- **Limitations:** Structural conditions are restrictive, accuracy can require many samples, and mixing policies is harder to explain when individual trips have hard constraints.
- **Direct BeGo-LTF use:** Expose two interpretable route/allocation endpoints and alternate or mix them across outings to satisfy a horizon debt target, while never mixing away per-trip feasibility.
- **Quality tier:** **A** — Peer-reviewed CMDP method with structural theorem and a real sponsored-search case study.

### 12. Cautious Regret Minimization: Online Optimization with Long-Term Budget Constraints

- **Citation:** Nikolaos Liakopoulos, Apostolos Destounis, Georgios Paschos, Thrasyvoulos Spyropoulos, Panayotis Mertikopoulos (2019). *International Conference on Machine Learning (ICML), PMLR 97*.
- **Persistent link:** No DOI assigned; [landing page](https://proceedings.mlr.press/v97/liakopoulos19a.html); full PDF không phân phối trong repo (local corpus: pdfs/12-cautious-regret-minimization-online-optimization-with-long-term-budget-c.pdf); structured review cục bộ (local corpus: structured-digests/12-structured-review.md); raw digest cục bộ (local corpus: digests/12-digest.txt).
- **Provenance:** Metadata verified from `https://proceedings.mlr.press/v97/liakopoulos19a.html`; full text is official publisher/proceedings full text; peer-reviewed: **yes**.
- **Full-text audit:** 9 pages; 6,664 extracted words; SHA-256 `28281e60abd87fe4a6bcaec9521cf6e372802f2912e6ab729326f876f3f566c2`.
- **Method:** COLD combines online gradient descent with a virtual predictor/deficit queue and compares against a K-window-feasible benchmark under adversarial long-term budgets.
- **Data/evaluation:** Synthetic online resource/budget experiments including benchmark-length and queue-parameter ablations.
- **Numerical result or theorem:** Regret is O(KT/V + sqrt(T)) and residual budget is O(sqrt(VT)); with K=T^(1-epsilon), V=T^(1-epsilon/2), the rates become T^(1-epsilon/2) regret and T^(1-epsilon/4) residual. A reported setting cuts excess loss by as much as 85% versus a one-window benchmark.
- **Limitations:** Convex losses/constraints and tuning V/K are required; guarantees are against a restricted benchmark rather than an unrestricted offline optimum.
- **Direct BeGo-LTF use:** This is the primary mathematical template for BeGo fairness debt: update one virtual queue per member/burden dimension and add queue-weighted future burden to the route objective.
- **Quality tier:** **A*** — Flagship peer-reviewed online-optimization paper; directly supports virtual-queue fairness debt.

### 13. On the Long-term Impact of Algorithmic Decision Policies: Effort Unfairness and Feature Segregation through Social Learning

- **Citation:** Hoda Heidari, Vedant Nanda, Krishna P. Gummadi (2019). *International Conference on Machine Learning (ICML), PMLR 97*.
- **Persistent link:** No DOI assigned; [landing page](https://proceedings.mlr.press/v97/heidari19a.html); full PDF không phân phối trong repo (local corpus: pdfs/13-on-the-long-term-impact-of-algorithmic-decision-policies-effort-unfairne.pdf); structured review cục bộ (local corpus: structured-digests/13-structured-review.md); raw digest cục bộ (local corpus: digests/13-digest.txt).
- **Provenance:** Metadata verified from `https://proceedings.mlr.press/v97/heidari19a.html`; full text is official publisher/proceedings full text; peer-reviewed: **yes**.
- **Full-text audit:** 10 pages; 7,057 extracted words; SHA-256 `b4000324ea9ff1d67a4fa7da73075875fe896f9ad6388da00ae70f8a1ebe02e4`.
- **Method:** Defines effort-based unfairness and uses a micro-level social-learning/imitation model to simulate how decision policies reshape features and segregation over time.
- **Data/evaluation:** Student-performance data with bounded, threshold, and effort-reward unfairness analyses plus clustering/evenness/centralization measures.
- **Numerical result or theorem:** No single effect-size theorem is claimed. Figure 5 shows a non-monotone intervention: small fairness strength tau reduces clustering, but larger tau can reverse the benefit and exceed initial segregation, while evenness changes little.
- **Limitations:** Behavioral imitation is a stylized unvalidated model; results need human or longitudinal validation and depend strongly on feature/action semantics.
- **Direct BeGo-LTF use:** Interpret mobility burden as the effort needed to remain included. Model whether repeated driving/detours cause withdrawal, and report post-decision participation/segregation rather than only allocation parity.
- **Quality tier:** **A*** — Flagship peer-reviewed work introducing effort unfairness and endogenous population response.

### 14. Delayed Impact of Fair Machine Learning

- **Citation:** Lydia T. Liu, Sarah Dean, Esther Rolf, Max Simchowitz, Moritz Hardt (2018). *International Conference on Machine Learning (ICML), PMLR 80*.
- **Persistent link:** No DOI assigned; [landing page](https://proceedings.mlr.press/v80/liu18c.html); full PDF không phân phối trong repo (local corpus: pdfs/14-delayed-impact-of-fair-machine-learning.pdf); structured review cục bộ (local corpus: structured-digests/14-structured-review.md); raw digest cục bộ (local corpus: digests/14-digest.txt).
- **Provenance:** Metadata verified from `https://proceedings.mlr.press/v80/liu18c.html`; full text is official publisher/proceedings full text; peer-reviewed: **yes**.
- **Full-text audit:** 9 pages; 6,823 extracted words; SHA-256 `070c6568f263a30010ee5401e2fddd7a913db51fa32123d1d67e6ca5a923f221`.
- **Method:** One-step feedback model characterizing how MaxUtil, demographic parity, and equal opportunity alter a group's future score distribution.
- **Data/evaluation:** FICO credit-score distribution with 301,536 observations and calibrated lending utility examples.
- **Numerical result or theorem:** The analysis fully characterizes improvement, stagnation, and active harm regions. In an example with c_-= -150 and c_+=75, both fairness constraints can help, stagnate, or harm; at loss/profit ratio -10 none beats active harm, while at -4 demographic parity can over-lend.
- **Limitations:** Only one feedback step, group-level score dynamics, and domain-specific behavioral response; predictive calibration does not identify real causal response.
- **Direct BeGo-LTF use:** Before deploying debt compensation, simulate whether a static equalization rule could reduce attendance or worsen future burden. Compare policies on dynamic participation, not merely current-trip Gini.
- **Quality tier:** **A*** — Foundational flagship-venue analysis of delayed impact with a large real credit-score distribution.

### 15. Long-term Dynamics of Fairness Intervention in Connection Recommender Systems

- **Citation:** Nil-Jana Akpinar, Cyrus DiCiccio, Preetam Nandy, Kinjal Basu (2022). *AAAI/ACM Conference on AI, Ethics, and Society (AIES)*.
- **Persistent link:** https://doi.org/10.1145/3514094.3534173; [landing page](https://dl.acm.org/doi/10.1145/3514094.3534173); full PDF không phân phối trong repo (local corpus: pdfs/15-long-term-dynamics-of-fairness-intervention-in-connection-recommender-sy.pdf); structured review cục bộ (local corpus: structured-digests/15-structured-review.md); raw digest cục bộ (local corpus: digests/15-digest.txt).
- **Provenance:** Metadata verified from `https://dl.acm.org/doi/10.1145/3514094.3534173`; full text is author arXiv manuscript linked to the published DOI; peer-reviewed: **yes**.
- **Full-text audit:** 15 pages; 13,868 extracted words; SHA-256 `a31e050dad6dc4172423d9ea563f72a1e31e5017dedea350f3f436478a6aabd0`.
- **Method:** Long-run simulation of connection recommendation with demographic exposure/utility interventions and a Polya-urn feedback process on both source and destination sides.
- **Data/evaluation:** Synthetic network/recommender dynamics over 2,500 time steps under multiple intervention targets and population skews.
- **Numerical result or theorem:** Across 2,500-step runs, interventions that look fair in aggregate can still amplify long-run representation bias because source-side participation and popularity feed back into later recommendations.
- **Limitations:** Simulation model, sensitive demographic attributes, and per-session optimization latency; no production causal estimate.
- **Direct BeGo-LTF use:** Plot each member's debt trajectory and prefix maxima, not only final equality. Add a feedback scenario where the already-popular/available driver is repeatedly selected and eventually dominates the allocation.
- **Quality tier:** **A** — Peer-reviewed long-horizon recommender simulation with explicit feedback dynamics.

### 16. Enabling Long-term Fairness in Dynamic Resource Allocation

- **Citation:** Tareq Si Salem, George Iosifidis, Giovanni Neglia (2022). *Proceedings of the ACM on Measurement and Analysis of Computing Systems, 6(3), Article 46*.
- **Persistent link:** https://doi.org/10.1145/3570606; [landing page](https://dl.acm.org/doi/10.1145/3570606); full PDF không phân phối trong repo (local corpus: pdfs/16-enabling-long-term-fairness-in-dynamic-resource-allocation.pdf); structured review cục bộ (local corpus: structured-digests/16-structured-review.md); raw digest cục bộ (local corpus: digests/16-digest.txt).
- **Provenance:** Metadata verified from `https://dl.acm.org/doi/10.1145/3570606`; full text is author arXiv manuscript linked to the published DOI; peer-reviewed: **yes**.
- **Full-text audit:** 36 pages; 20,103 extracted words; SHA-256 `f0d70d170e6d6bc9e9a7a16b94854c71fe4df39325a7f510fa8f3bb201565f21`.
- **Method:** Online horizon-fair resource allocation under alpha-fair utility; contrasts per-slot fairness with fairness of cumulative horizon utility and derives regret under bounded temporal/path variation.
- **Data/evaluation:** Cooperative caching simulations on Cycle, Tree, Grid, Abilene, and GEANT topologies with varying users, alpha, and demand variation.
- **Numerical result or theorem:** Vanishing horizon-fairness regret is impossible against an unrestricted adversary but achievable under sublinear variation. Experiments approach the offline horizon-fair benchmark with reported price of fairness below 4% in the studied cases.
- **Limitations:** Utilities are concave resource-allocation functions, not discrete routes; success requires variation restrictions and known normalization.
- **Direct BeGo-LTF use:** Make horizon fairness the core objective and per-trip fairness a guardrail. Use alpha-fair cumulative utility and evaluate price of fairness, targeting a small efficiency loss rather than forcing every outing to be perfectly equal.
- **Quality tier:** **A** — Strong peer-reviewed journal article with impossibility/result conditions and multi-topology experiments.

### 17. Temporal Fair Division

- **Citation:** Benjamin Cookson, Soroush Ebadian, Nisarg Shah (2025). *AAAI Conference on Artificial Intelligence (AAAI-25)*.
- **Persistent link:** https://doi.org/10.1609/aaai.v39i13.33500; [landing page](https://ojs.aaai.org/index.php/AAAI/article/view/33500); full PDF không phân phối trong repo (local corpus: pdfs/17-temporal-fair-division.pdf); structured review cục bộ (local corpus: structured-digests/17-structured-review.md); raw digest cục bộ (local corpus: digests/17-digest.txt).
- **Provenance:** Metadata verified from `https://ojs.aaai.org/index.php/AAAI/article/view/33500`; full text is official publisher/proceedings full text; peer-reviewed: **yes**.
- **Full-text audit:** 8 pages; 7,822 extracted words; SHA-256 `4c12179e34f4edd3da9b9bd2c5a97694bbe353447fe94b3c10716c83d891ad05`.
- **Method:** Temporal fair division studies simultaneous per-day, prefix, and overall fairness for repeated goods, using stochastic-dominance EF1 and proportional-up-to-one-good guarantees.
- **Data/evaluation:** Formal constructive proofs and counterexamples across general, two-agent, identical-preference, and same-goods special cases.
- **Numerical result or theorem:** In the general setting an allocation always exists that is SD-EF1 each day and PROP1 overall; for two agents, SD-EF1 per day and EF1 at every prefix can be guaranteed.
- **Limitations:** Indivisible goods with ordinal/additive preferences, not routing externalities; several stronger guarantee combinations remain impossible or open.
- **Direct BeGo-LTF use:** Specify a two-timescale contract: bounded unfairness on each trip plus prefix cumulative fairness. Role/vehicle assignment can use EF1-style 'one role away' explanations.
- **Quality tier:** **A*** — Top peer-reviewed venue; crisp existence guarantees for per-day and over-time fairness.

### 18. Fairness in Repeated Matching: A Maximin Perspective

- **Citation:** Eugene Lim, Tzeh Yuan Neoh, Nicholas Teh (2026). *AAAI Conference on Artificial Intelligence (AAAI-26)*.
- **Persistent link:** https://doi.org/10.1609/aaai.v40i20.38760; [landing page](https://ojs.aaai.org/index.php/AAAI/article/view/38760); full PDF không phân phối trong repo (local corpus: pdfs/18-fairness-in-repeated-matching-a-maximin-perspective.pdf); structured review cục bộ (local corpus: structured-digests/18-structured-review.md); raw digest cục bộ (local corpus: digests/18-digest.txt).
- **Provenance:** Metadata verified from `https://ojs.aaai.org/index.php/AAAI/article/view/38760`; full text is official publisher/proceedings full text; peer-reviewed: **yes**.
- **Full-text audit:** 9 pages; 8,073 extracted words; SHA-256 `66dd17a2eac98022edbd3b239408d022ae04a27cceff9babed457badadabda8c`.
- **Method:** Repeated bipartite matching maximizes the least-advantaged agent's utility either at the final horizon or after every round; develops LP/Birkhoff approximation, FPT algorithms, and tractable special cases.
- **Data/evaluation:** Complexity proofs, constructions, and algorithmic analysis; no mobility dataset.
- **Numerical result or theorem:** General optimal and anytime-optimal problems are NP-complete; LP plus Birkhoff decomposition obtains an additive error bounded by the largest single-item utility and converges relatively as the horizon grows.
- **Limitations:** Known repeated agent/item sets and additive utilities; no travel-time feasibility, strategic behavior, or empirical scaling study.
- **Direct BeGo-LTF use:** Model driver/vehicle/seat roles as repeated matching and compare final-horizon versus anytime maximin guarantees; use the additive bound as an interpretable approximation certificate.
- **Quality tier:** **A*** — New top-venue theory on optimal and anytime repeated matching, including complexity and approximation results.

### 19. Online Fair Allocations with Binary Valuations and Beyond

- **Citation:** Yuanyuan Wang, Tianze Wei (2026). *AAAI Conference on Artificial Intelligence (AAAI-26)*.
- **Persistent link:** https://doi.org/10.1609/aaai.v40i20.38778; [landing page](https://ojs.aaai.org/index.php/AAAI/article/view/38778); full PDF không phân phối trong repo (local corpus: pdfs/19-online-fair-allocations-with-binary-valuations-and-beyond.pdf); structured review cục bộ (local corpus: structured-digests/19-structured-review.md); raw digest cục bộ (local corpus: digests/19-digest.txt).
- **Provenance:** Metadata verified from `https://ojs.aaai.org/index.php/AAAI/article/view/38778`; full text is official publisher/proceedings full text; peer-reviewed: **yes**.
- **Full-text audit:** 9 pages; 8,024 extracted words; SHA-256 `64457c0160cce5d527d4d2f1a19d586af578a70cc28a62672601cc5017d564f6`.
- **Method:** Immediate irrevocable allocation of online indivisible goods or chores under binary and personalized bi-valued valuations, with EF1, MMS, and utilitarian-welfare analysis.
- **Data/evaluation:** Theoretical algorithms and tight counterexamples, not an empirical dataset.
- **Numerical result or theorem:** Marginal-greedy obtains tight 1/2-EF1 and 1/2-MMS-style guarantees in key binary settings while maximizing utilitarian welfare; for two agents with bi-valued costs the paper gives 1/2-EF1 and 1/3-MMS bounds.
- **Limitations:** Simplified discrete valuations and irrevocable arrivals; mobility burden is multi-dimensional, coupled by routes, and rarely binary.
- **Direct BeGo-LTF use:** Use as a conservative baseline for indivisible chores such as driving or taking the least convenient pickup: encode simplified acceptability tiers and report EF1/MMS violations alongside continuous debt.
- **Quality tier:** **A*** — New top-venue online-allocation theory with tight positive and impossibility results.

### 20. Learning Fair Division from Bandit Feedback

- **Citation:** Hakuei Yamada, Junpei Komiyama, Kenshi Abe, Atsushi Iwasaki (2024). *International Conference on Artificial Intelligence and Statistics (AISTATS), PMLR 238*.
- **Persistent link:** No DOI assigned; [landing page](https://proceedings.mlr.press/v238/yamada24a.html); full PDF không phân phối trong repo (local corpus: pdfs/20-learning-fair-division-from-bandit-feedback.pdf); structured review cục bộ (local corpus: structured-digests/20-structured-review.md); raw digest cục bộ (local corpus: digests/20-digest.txt).
- **Provenance:** Metadata verified from `https://proceedings.mlr.press/v238/yamada24a.html`; full text is official publisher/proceedings full text; peer-reviewed: **yes**.
- **Full-text audit:** 33 pages; 17,121 extracted words; SHA-256 `d8270e5ef8e97698d97625535d6bbfd73c774c3ddeab7f6028abe5eb15652a81`.
- **Method:** Bandit fair division under unknown item distributions and agent values; dual-averaging wrappers combine explore-then-commit or UCB with Nash-social-welfare market allocation.
- **Data/evaluation:** Synthetic instances plus Jester joke ratings and Household preference data, including cases around n=10 agents and m=50 item types.
- **Numerical result or theorem:** DA-EtC has approximately O((nm)^(1/3) T^(2/3)) regret; RDA-UCB improves the horizon dependence to approximately O(poly(n,m) sqrt(T)), matching an Omega(sqrt(mT)) lower bound in T.
- **Limitations:** Linear Fisher market and additive utilities; uniform exploration can be wasteful, and feedback assumes a usable noisy value after allocation.
- **Direct BeGo-LTF use:** Learn each member's private burden weights from post-trip ratings while allocating under Nash welfare. Keep an exploration budget and compare learned weights with fixed survey weights.
- **Quality tier:** **A*** — Top peer-reviewed bandit fair-division paper with regret guarantees and real preference datasets.

### 21. Improved Regret Bounds for Online Fair Division with Bandit Learning

- **Citation:** Benjamin Schiffer, Shirley Zhang (2025). *AAAI Conference on Artificial Intelligence (AAAI-25)*.
- **Persistent link:** https://doi.org/10.1609/aaai.v39i13.33541; [landing page](https://ojs.aaai.org/index.php/AAAI/article/view/33541); full PDF không phân phối trong repo (local corpus: pdfs/21-improved-regret-bounds-for-online-fair-division-with-bandit-learning.pdf); structured review cục bộ (local corpus: structured-digests/21-structured-review.md); raw digest cục bộ (local corpus: digests/21-digest.txt).
- **Provenance:** Metadata verified from `https://ojs.aaai.org/index.php/AAAI/article/view/33541`; full text is official publisher/proceedings full text; peer-reviewed: **yes**.
- **Full-text audit:** 8 pages; 7,317 extracted words; SHA-256 `21a73eabc8d4e38dfce6b4583fb6fcb2ae463718313b68aad84bd5eab47f8348`.
- **Method:** Two-stage UCB plus linear optimization for online fair division with unknown mean values, normalized utilities, and proportionality constraints.
- **Data/evaluation:** Theoretical stochastic-arrival analysis and computational illustrations; no human mobility dataset.
- **Numerical result or theorem:** With high probability it guarantees proportionality at every step and approximately O(n^5 m^3 sqrt(T)) regret, improving prior T^(2/3) dependence; envy-free learning still has a T^(2/3) lower bound.
- **Limitations:** Large polynomial factors and an expensive second optimization; only proportionality is covered and normalized values must be meaningful.
- **Direct BeGo-LTF use:** Use as a safety reference when learning preferences: exploration must preserve a minimum proportional entitlement, though BeGo should implement a simpler constrained learner rather than the full high-order method.
- **Quality tier:** **A*** — Top peer-reviewed theory improving online fair-division regret while preserving proportionality.

### 22. Regularized Online Allocation Problems: Fairness and Beyond

- **Citation:** Santiago Balseiro, Haihao Lu, Vahab Mirrokni (2021). *International Conference on Machine Learning (ICML), PMLR 139*.
- **Persistent link:** No DOI assigned; [landing page](https://proceedings.mlr.press/v139/balseiro21a.html); full PDF không phân phối trong repo (local corpus: pdfs/22-regularized-online-allocation-problems-fairness-and-beyond.pdf); structured review cục bộ (local corpus: structured-digests/22-structured-review.md); raw digest cục bộ (local corpus: digests/22-digest.txt).
- **Provenance:** Metadata verified from `https://proceedings.mlr.press/v139/balseiro21a.html`; full text is official publisher/proceedings full text; peer-reviewed: **yes**.
- **Full-text audit:** 10 pages; 7,247 extracted words; SHA-256 `93af03fe34ad28d21f665694ef66f1a0ece014909fcfbe5abe01d371ce20eefa`.
- **Method:** Dual online subgradient framework for allocation with a concave regularizer over cumulative resource consumption, covering max-min, load balancing, and other fairness goals.
- **Data/evaluation:** Online advertising allocation with 100 randomized trials and regularizer-strength ablations.
- **Numerical result or theorem:** The algorithm achieves optimal-order O(sqrt(T)) regret. In the advertising case, regularization roughly doubles the minimum delivery/fairness measure for about a 4% reward reduction at regularizer weight 0.01.
- **Limitations:** Convex divisible-resource model and i.i.d./regularity assumptions; ad delivery is not a coupled vehicle-routing problem.
- **Direct BeGo-LTF use:** Add a concave fairness regularizer or dual price to BeGo's existing assignment objective. Treat the roughly 4% empirical tradeoff as an informative benchmark, not a promised mobility result.
- **Quality tier:** **A*** — Flagship peer-reviewed online-allocation framework with optimal-order regret and industrially motivated experiments.

### 23. Universal and Tight Online Algorithms for Generalized-Mean Welfare

- **Citation:** Siddharth Barman, Arindam Khan, Arnab Maiti (2022). *AAAI Conference on Artificial Intelligence (AAAI-22)*.
- **Persistent link:** https://doi.org/10.1609/aaai.v36i5.20406; [landing page](https://ojs.aaai.org/index.php/AAAI/article/view/20406); full PDF không phân phối trong repo (local corpus: pdfs/23-universal-and-tight-online-algorithms-for-generalized-mean-welfare.pdf); structured review cục bộ (local corpus: structured-digests/23-structured-review.md); raw digest cục bộ (local corpus: digests/23-digest.txt).
- **Provenance:** Metadata verified from `https://ojs.aaai.org/index.php/AAAI/article/view/20406`; full text is official publisher/proceedings full text; peer-reviewed: **yes**.
- **Full-text audit:** 8 pages; 6,931 extracted words; SHA-256 `9374b89559f07feabe3073503bfdd3ed58c753b4046aa9cfa8e59e52901f6ebc`.
- **Method:** Universal online fractional allocation for generalized p-mean welfare, with one policy covering every p<=1 and specialized algorithms for fixed p.
- **Data/evaluation:** Competitive-analysis proofs and adversarial lower-bound instances.
- **Numerical result or theorem:** A universal policy attains O(sqrt(n) log n) competitiveness across p<=1; p-specific methods achieve O(log^3 n), with matching/tight lower bounds in the studied regimes.
- **Limitations:** Divisible goods, normalized additive valuations, and known scaling/horizon; routes and driver roles are indivisible and coupled.
- **Direct BeGo-LTF use:** Use p-mean/alpha-fair welfare as a tunable family from utilitarian to max-min, and report sensitivity across p rather than selecting one fairness ideology silently.
- **Quality tier:** **A*** — Top peer-reviewed online fair-division theory with universal and tight competitive bounds.

### 24. Class Fairness in Online Matching

- **Citation:** Hadi Hosseini, Zhiyi Huang, Ayumi Igarashi, Nisarg Shah (2023). *AAAI Conference on Artificial Intelligence (AAAI-23)*.
- **Persistent link:** https://doi.org/10.1609/aaai.v37i5.25704; [landing page](https://ojs.aaai.org/index.php/AAAI/article/view/25704); full PDF không phân phối trong repo (local corpus: pdfs/24-class-fairness-in-online-matching.pdf); structured review cục bộ (local corpus: structured-digests/24-structured-review.md); raw digest cục bộ (local corpus: digests/24-digest.txt).
- **Provenance:** Metadata verified from `https://ojs.aaai.org/index.php/AAAI/article/view/25704`; full text is official publisher/proceedings full text; peer-reviewed: **yes**.
- **Full-text audit:** 8 pages; 7,680 extracted words; SHA-256 `613069862d04e264f089017b064867a9e3b31a00f1cff9edf9fc88291dbbf1a8`.
- **Method:** Online bipartite matching where offline agents are partitioned into classes; MATCH-AND-SHIFT handles indivisible items and EQUAL-FILLING handles divisible allocations.
- **Data/evaluation:** Formal adversarial-arrival analysis and tight examples.
- **Numerical result or theorem:** MATCH-AND-SHIFT guarantees 1/2 class-EF1, class-MMS, and utilitarian welfare in the stated binary setting; EQUAL-FILLING attains (1-1/e) class envy/proportional fairness and 1/2 welfare for divisible items.
- **Limitations:** Binary class valuations and irrevocable matching; protected-class guarantees can mask within-class individual debt.
- **Direct BeGo-LTF use:** Audit family/subgroup fairness in addition to individual debt—for example accessibility needs, habitual drivers, or neighborhoods—while explicitly checking within-group maxima so class averages do not hide one overburdened member.
- **Quality tier:** **A*** — Top peer-reviewed theory initiating class-level fairness in online matching.

### 25. Fair and Efficient Online Allocations with Normalized Valuations

- **Citation:** Vasilis Gkatzelis, Alexandros Psomas, Xizhi Tan (2021). *AAAI Conference on Artificial Intelligence (AAAI-21)*.
- **Persistent link:** https://doi.org/10.1609/aaai.v35i6.16685; [landing page](https://ojs.aaai.org/index.php/AAAI/article/view/16685); full PDF không phân phối trong repo (local corpus: pdfs/25-fair-and-efficient-online-allocations-with-normalized-valuations.pdf); structured review cục bộ (local corpus: structured-digests/25-structured-review.md); raw digest cục bộ (local corpus: digests/25-digest.txt).
- **Provenance:** Metadata verified from `https://ojs.aaai.org/index.php/AAAI/article/view/16685`; full text is official publisher/proceedings full text; peer-reviewed: **yes**.
- **Full-text audit:** 8 pages; 7,342 extracted words; SHA-256 `41339d4b308b1b6c059b1f6489378debd8a357ce39d594f612be187dc02aa921`.
- **Method:** Guarded poly-proportional online allocation for two agents with normalized values, maximizing welfare subject to fairness.
- **Data/evaluation:** Theoretical adversarial sequences and numerical optimization of competitive constants.
- **Numerical result or theorem:** The main two-agent algorithm guarantees 91.6% of offline optimal social welfare; no fair online algorithm can exceed 93.3%. Quadratic and proportional baselines guarantee 89.4% and 82.8%, respectively.
- **Limitations:** Only two agents, divisible goods, and normalized values; not directly a multi-person route allocation.
- **Direct BeGo-LTF use:** Provide a simple proportional debt-repayment baseline and quantify its price of fairness. The tight two-agent result helps explain why some efficiency loss is unavoidable even with perfect normalization.
- **Quality tier:** **A*** — Top peer-reviewed paper with near-tight welfare guarantees under normalized valuations.

### 26. Fairness of Exposure in Stochastic Bandits

- **Citation:** Lequn Wang, Yiwei Bai, Wen Sun, Thorsten Joachims (2021). *International Conference on Machine Learning (ICML), PMLR 139*.
- **Persistent link:** No DOI assigned; [landing page](https://proceedings.mlr.press/v139/wang21b.html); full PDF không phân phối trong repo (local corpus: pdfs/26-fairness-of-exposure-in-stochastic-bandits.pdf); structured review cục bộ (local corpus: structured-digests/26-structured-review.md); raw digest cục bộ (local corpus: digests/26-digest.txt).
- **Provenance:** Metadata verified from `https://proceedings.mlr.press/v139/wang21b.html`; full text is official publisher/proceedings full text; peer-reviewed: **yes**.
- **Full-text audit:** 11 pages; 7,689 extracted words; SHA-256 `e356e82c6910fa6aca01699f8761692ed478ed6b31abe05d80d932a4066f7a5c`.
- **Method:** FairX-UCB/TS and linear-bandit variants allocate exposure proportional to a positive merit function while minimizing both reward and fairness regret.
- **Data/evaluation:** Yeast classification-to-bandit conversion and Yahoo recommendation logs, plus synthetic stochastic-bandit studies.
- **Numerical result or theorem:** Fairness and reward regret are sublinear, approximately O(L sqrt(KT)/gamma) for MAB and O(L d sqrt(T)/gamma) for the linear setting under Lipschitz/positive-merit assumptions.
- **Limitations:** Merit must be bounded away from zero and correctly specified; optimism can be nonconvex and item exposure is not identical to human burden.
- **Direct BeGo-LTF use:** When selecting destination/driver candidates probabilistically, allocate favorable exposure in proportion to willingness or need and track fairness regret against that declared merit rule.
- **Quality tier:** **A*** — Flagship peer-reviewed fair-bandit theory plus two real recommendation datasets.

### 27. BanditQ: Fair Bandits with Guaranteed Rewards

- **Citation:** Abhishek Sinha (2024). *Conference on Uncertainty in Artificial Intelligence (UAI), PMLR 244*.
- **Persistent link:** No DOI assigned; [landing page](https://proceedings.mlr.press/v244/sinha24a.html); full PDF không phân phối trong repo (local corpus: pdfs/27-banditq-fair-bandits-with-guaranteed-rewards.pdf); structured review cục bộ (local corpus: structured-digests/27-structured-review.md); raw digest cục bộ (local corpus: digests/27-digest.txt).
- **Provenance:** Metadata verified from `https://proceedings.mlr.press/v244/sinha24a.html`; full text is official publisher/proceedings full text; peer-reviewed: **yes**.
- **Full-text audit:** 18 pages; 11,276 extracted words; SHA-256 `9ef2e01670aadf3763ce05380890334b2d199afdf01f586c672c2e2b1c972adf`.
- **Method:** BanditQ combines virtual queues with adversarial-bandit updates to guarantee a minimum cumulative reward rate for every arm under bandit feedback.
- **Data/evaluation:** Synthetic full-information and bandit experiments, including N=5/k=2 and a large N=1000 appendix setting.
- **Numerical result or theorem:** With V=sqrt(T), the paper bounds regret and target-rate violation by O(T^(3/4)) up to dimension/log factors, so average regret and violation vanish.
- **Limitations:** Requires jointly feasible target rates; bounds are conservative and rewards for unchosen members remain unobserved.
- **Direct BeGo-LTF use:** Directly map each member to a queue whose target is a minimum compensation/benefit rate. Candidate routes receive a bonus for serving high-debt queues, with feasibility checked before setting targets.
- **Quality tier:** **A** — Strong peer-reviewed virtual-queue bandit work with formal reward-rate guarantees.

### 28. Fair Contextual Multi-Armed Bandits: Theory and Experiments

- **Citation:** Yifang Chen, Alex Cuellar, Haipeng Luo, Jignesh Modi, Heramb Nemlekar, Stefanos Nikolaidis (2020). *Conference on Uncertainty in Artificial Intelligence (UAI), PMLR 124*.
- **Persistent link:** No DOI assigned; [landing page](https://proceedings.mlr.press/v124/chen20a.html); full PDF không phân phối trong repo (local corpus: pdfs/28-fair-contextual-multi-armed-bandits-theory-and-experiments.pdf); structured review cục bộ (local corpus: structured-digests/28-structured-review.md); raw digest cục bộ (local corpus: digests/28-digest.txt).
- **Provenance:** Metadata verified from `https://proceedings.mlr.press/v124/chen20a.html`; full text is official publisher/proceedings full text; peer-reviewed: **yes**.
- **Full-text audit:** 10 pages; 7,498 extracted words; SHA-256 `160fd2812585c49e0d06090ac2f91e335de3c50922a16f701c6fa11df854cd63`.
- **Method:** Contextual FTRL bandit with minimum assignment-rate constraints, allowing fairness to be paid in low-cost contexts rather than uniformly.
- **Data/evaluation:** Synthetic tasks, MovieLens, and 37 participant pairs (54 completed the survey) in a human-robot allocation study.
- **Numerical result or theorem:** The fair contextual policy scores 24.833 versus 21.472 for the baseline in the human study, t(36)=-3.308, p=.002; algorithm and fairness effects are statistically significant.
- **Limitations:** Small laboratory study, minimum-rate rather than debt fairness, and task performance signals may not transfer to outing satisfaction.
- **Direct BeGo-LTF use:** Repay debt when context makes compensation cheap: favor a member on outings where their preferred pickup/destination causes the smallest added group cost, instead of rigid round-robin compensation.
- **Quality tier:** **A** — Strong peer-reviewed contextual-fairness work with theory, MovieLens, and a 37-pair human study.

### 29. Efficient Resource Allocation with Fairness Constraints in Restless Multi-Armed Bandits

- **Citation:** Dexun Li, Pradeep Varakantham (2022). *Conference on Uncertainty in Artificial Intelligence (UAI), PMLR 180*.
- **Persistent link:** No DOI assigned; [landing page](https://proceedings.mlr.press/v180/li22e.html); full PDF không phân phối trong repo (local corpus: pdfs/29-efficient-resource-allocation-with-fairness-constraints-in-restless-mult.pdf); structured review cục bộ (local corpus: structured-digests/29-structured-review.md); raw digest cục bộ (local corpus: digests/29-digest.txt).
- **Provenance:** Metadata verified from `https://proceedings.mlr.press/v180/li22e.html`; full text is official publisher/proceedings full text; peer-reviewed: **yes**.
- **Full-text audit:** 10 pages; 6,806 extracted words; SHA-256 `7548cacc4182455996a1b76ddd8d43a4dc694d2c432fdce44887447efd61c63a`.
- **Method:** Fair restless multi-armed bandit requiring each arm to be activated at least eta times in any L-window; develops Whittle-index and model-free Q-learning policies.
- **Data/evaluation:** Synthetic RMABs with N=50 to 500 arms, horizon T=1000, and 50 random runs under multiple fairness windows.
- **Numerical result or theorem:** The fairness-aware policies are near the oracle in most tested regimes and the analysis proves optimality/convergence under stated indexability/learning conditions; performance degrades most for the strict L=15 window.
- **Limitations:** Fully observable arm states, strong model/indexability conditions, and synthetic data; members' burdens interact through one shared route.
- **Direct BeGo-LTF use:** Maintain time-since-last-compensation and require a benefit within L outings, creating a clear service-level guarantee in addition to unbounded-horizon debt minimization.
- **Quality tier:** **A** — Strong peer-reviewed RMAB formulation with fairness-window guarantees and scale experiments.

### 30. Fairness and Privacy Guarantees in Federated Contextual Bandits

- **Citation:** Sambhav Solanki, Shweta Jain, Sujit Gujar (2025). *16th Asian Conference on Machine Learning (ACML), PMLR 260*.
- **Persistent link:** No DOI assigned; [landing page](https://proceedings.mlr.press/v260/solanki25a.html); full PDF không phân phối trong repo (local corpus: pdfs/30-fairness-and-privacy-guarantees-in-federated-contextual-bandits.pdf); structured review cục bộ (local corpus: structured-digests/30-structured-review.md); raw digest cục bộ (local corpus: digests/30-digest.txt).
- **Provenance:** Metadata verified from `https://proceedings.mlr.press/v260/solanki25a.html`; full text is official publisher/proceedings full text; peer-reviewed: **yes**.
- **Full-text audit:** 16 pages; 7,646 extracted words; SHA-256 `90c06ecaf789d15ac397db925b2d1dcbaf2984f6a850d1ae351fe3bbe74071b8`.
- **Method:** Federated FairX-LinUCB with a sparse communication protocol and a differentially private extension for merit-proportional exposure.
- **Data/evaluation:** Simulation-based federated contextual-bandit experiments up to horizon 100,000 with privacy-budget ablations.
- **Numerical result or theorem:** Total fairness regret is sublinear in the number of agents/rounds and communication is bounded by ceil(2 m d^2 log^2(1+T/d)); privacy epsilon at or above 1 gives comparatively moderate empirical degradation.
- **Limitations:** Simulation only, linear rewards, significant protocol/DP complexity, and the federation model is not needed for a single trusted BeGo server.
- **Direct BeGo-LTF use:** Reserve for a later privacy extension where friend groups retain local preference models; it is not necessary for the core long-term fairness contribution.
- **Quality tier:** **A** — Peer-reviewed fair contextual-bandit work with federated privacy guarantees; simulation-only validation.

### 31. Causal Logistic Bandits with Counterfactual Fairness Constraints

- **Citation:** Jiajun Chen, Jin Tian, Christopher John Quinn (2025). *International Conference on Machine Learning (ICML), PMLR 267*.
- **Persistent link:** No DOI assigned; [landing page](https://proceedings.mlr.press/v267/chen25bk.html); full PDF không phân phối trong repo (local corpus: pdfs/31-causal-logistic-bandits-with-counterfactual-fairness-constraints.pdf); structured review cục bộ (local corpus: structured-digests/31-structured-review.md); raw digest cục bộ (local corpus: digests/31-digest.txt).
- **Provenance:** Metadata verified from `https://proceedings.mlr.press/v267/chen25bk.html`; full text is official publisher/proceedings full text; peer-reviewed: **yes**.
- **Full-text audit:** 30 pages; 17,928 extracted words; SHA-256 `61cb8108de1c8bd06606a2bdcee85f588ed73b4713f8493f3af2d1bb66d34588`.
- **Method:** Primal-dual causal logistic bandit learns nonlinear counterfactual-fairness constraints and rewards under a known structural causal model.
- **Data/evaluation:** Synthetic SCM experiments around horizon 10,000, including tightened constraints and violation trajectories.
- **Numerical result or theorem:** CCLB obtains sublinear regret with leading term comparable to unconstrained logistic bandits and sublinear violations; epsilon-tightening yields zero upper-bound violation in the theorem, and empirical violation growth is nearly flat after roughly 6,000 rounds.
- **Limitations:** Known causal graph, Slater/identifiability assumptions, O(t) work per round, and synthetic validation make this high-risk for an initial BeGo thesis.
- **Direct BeGo-LTF use:** Treat as a publishable optional extension: audit whether sensitive attributes causally alter route recommendations, but do not make causal-bandit infrastructure a prerequisite for the debt-ledger system.
- **Quality tier:** **A*** — Recent flagship-venue constrained causal-bandit theory with finite-time regret and violation guarantees.

### 32. Achieving Counterfactual Fairness for Causal Bandit

- **Citation:** Wen Huang, Lu Zhang, Xintao Wu (2022). *AAAI Conference on Artificial Intelligence (AAAI-22)*.
- **Persistent link:** https://doi.org/10.1609/aaai.v36i6.20653; [landing page](https://ojs.aaai.org/index.php/AAAI/article/view/20653); full PDF không phân phối trong repo (local corpus: pdfs/32-achieving-counterfactual-fairness-for-causal-bandit.pdf); structured review cục bộ (local corpus: structured-digests/32-structured-review.md); raw digest cục bộ (local corpus: digests/32-digest.txt).
- **Provenance:** Metadata verified from `https://ojs.aaai.org/index.php/AAAI/article/view/20653`; full text is official publisher/proceedings full text; peer-reviewed: **yes**.
- **Full-text audit:** 8 pages; 6,823 extracted words; SHA-256 `632b3663ca8d5d244c5cc280ea844bf43dc8a5f48c87cc4488105b52df2ff26d`.
- **Method:** D-UCB exploits causal d-separation to reduce exploration; F-UCB restricts every action to a counterfactually fair set under a known causal graph.
- **Data/evaluation:** Email Campaign, Adult, and YouTube-style recommendation experiments over about 5,000 rounds and repeated seeds.
- **Numerical result or theorem:** F-UCB has zero unfair decisions in the reported comparisons; at fairness threshold 0.1 its regret is 392.12 while alternatives make about 3,030/3,176/3,473 unfair decisions, and in another Adult-video comparison Fair-LinUCB makes 2,053 unfair choices versus zero for F-UCB.
- **Limitations:** Requires a correct causal DAG and safe baseline/fair set; sensitive-variable semantics and interventions are difficult in social mobility choices.
- **Direct BeGo-LTF use:** Use its violation-count methodology for audits. A causal fair-set filter is a stretch goal only if BeGo can justify a causal preference model with domain evidence.
- **Quality tier:** **A*** — Top peer-reviewed causal-bandit paper with strict counterfactual-fairness algorithm and real datasets.

### 33. Fairness in Learning: Classic and Contextual Bandits

- **Citation:** Matthew Joseph, Michael Kearns, Jamie Morgenstern, Aaron Roth (2016). *Advances in Neural Information Processing Systems 29 (NeurIPS 2016)*.
- **Persistent link:** No DOI assigned; [landing page](https://proceedings.neurips.cc/paper_files/paper/2016/hash/eb163727917cbba1eea208541a643e74-Abstract.html); full PDF không phân phối trong repo (local corpus: pdfs/33-fairness-in-learning-classic-and-contextual-bandits.pdf); structured review cục bộ (local corpus: structured-digests/33-structured-review.md); raw digest cục bộ (local corpus: digests/33-digest.txt).
- **Provenance:** Metadata verified from `https://proceedings.neurips.cc/paper_files/paper/2016/hash/eb163727917cbba1eea208541a643e74-Abstract.html`; full text is official publisher/proceedings full text; peer-reviewed: **yes**.
- **Full-text audit:** 9 pages; 6,298 extracted words; SHA-256 `df7b41fa10bcb6c57fa2a85957d51650e33b929f2ef9d48bd96bc0f70a96b5e4`.
- **Method:** Introduces individual fairness for stochastic and contextual bandits using chained confidence intervals; establishes transformations between KWIK learning and fair contextual bandits.
- **Data/evaluation:** Theory and constructed hard instances rather than a real application dataset.
- **Numerical result or theorem:** Any exactly fair classic-bandit learner suffers constant per-round regret until on the order of k^3 log(1/delta) rounds in the hard case; general contextual classes can incur an exponential 2^d gap, while the proposed fair algorithms match the relevant polynomial bounds.
- **Limitations:** Exact merit ordering is demanding and differs from distributive burden fairness; results are worst-case and can be overly conservative in small stable groups.
- **Direct BeGo-LTF use:** Plan an explicit warm-up/exploration phase and communicate uncertainty. Prefer approximate confidence-aware fairness plus debt caps instead of claiming exact fairness before enough group history exists.
- **Quality tier:** **A*** — Foundational flagship-venue fair-bandit theory with matching lower bounds.

### 34. Distribution Fairness in Multiplayer AI Using Shapley Constraints

- **Citation:** Robert C. Gray, Jichen Zhu, Santiago Ontañón (2023). *AAAI Conference on Artificial Intelligence and Interactive Digital Entertainment (AIIDE-23)*.
- **Persistent link:** https://doi.org/10.1609/aiide.v19i1.27519; [landing page](https://ojs.aaai.org/index.php/AIIDE/article/view/27519); full PDF không phân phối trong repo (local corpus: pdfs/34-distribution-fairness-in-multiplayer-ai-using-shapley-constraints.pdf); structured review cục bộ (local corpus: structured-digests/34-structured-review.md); raw digest cục bộ (local corpus: digests/34-digest.txt).
- **Provenance:** Metadata verified from `https://ojs.aaai.org/index.php/AIIDE/article/view/27519`; full text is official publisher/proceedings full text; peer-reviewed: **yes**.
- **Full-text audit:** 11 pages; 9,455 extracted words; SHA-256 `2a92e643ab558cd8894be0b0e3fbef11bc1db9bb29a11a6c19373c3b992cbcf0`.
- **Method:** Shapley Bandit adds player-level distribution constraints derived from Shapley values to a greedy multiplayer experience-management bandit.
- **Data/evaluation:** Human pretest motivating non-adherence plus simulation of 2,400 virtual players across treatment conditions.
- **Numerical result or theorem:** The Shapley policy retains 99.2% of greedy effectiveness (about 0.75% performance cost) while significantly reducing distribution variance, p<.001.
- **Limitations:** Virtual-player evaluation, serious-game domain, Shapley computation/scaling, and no field retention study.
- **Direct BeGo-LTF use:** Use Shapley-inspired attribution to allocate compensation when a driver's sacrifice benefits several members, and measure whether fairer treatment improves attendance/adherence.
- **Quality tier:** **A-** — Peer-reviewed and empirically useful, but the serious-game domain and simulated players limit transfer validity.

### 35. Fairness as an Investment: Dynamic Participation and Long-Run Profit in Virtual Power Plants

- **Citation:** Liudong Chen, Bolun Xu (2026). *CoRR / arXiv*.
- **Persistent link:** https://doi.org/10.48550/arXiv.2606.02820; [landing page](https://arxiv.org/abs/2606.02820); full PDF không phân phối trong repo (local corpus: pdfs/35-fairness-as-an-investment-dynamic-participation-and-long-run-profit-in-v.pdf); structured review cục bộ (local corpus: structured-digests/35-structured-review.md); raw digest cục bộ (local corpus: digests/35-digest.txt).
- **Provenance:** Metadata verified from `https://arxiv.org/abs/2606.02820`; full text is primary arXiv preprint; peer-reviewed: **no/preprint**.
- **Full-text audit:** 10 pages; 8,275 extracted words; SHA-256 `cd473771e09375895f6aec1abd09a3ceff9affc04f2432543692dbcb526af4aa`.
- **Method:** Dynamic multi-period participation model links current service allocation to future consumer availability; compares strict fairness with a slack-augmented mechanism and derives when participation gains outweigh current cost.
- **Data/evaluation:** Real Norwegian consumer-behavior and electricity-market data, with analysis of the top 10% extreme-price/demand events (23 events in the reported slice).
- **Numerical result or theorem:** The paper derives sufficient conditions under which fairness-induced availability raises long-run profit; experiments show slack fairness preserves most participation gains while avoiding unnecessary procurement loss.
- **Limitations:** 2026 preprint without peer review, cross-domain VPP behavior, and participation response may not identify causal outing behavior.
- **Direct BeGo-LTF use:** Make dynamic participation a central hypothesis: repeated burden lowers future willingness to drive/join, so fairness can increase long-run group feasibility rather than being only a cost.
- **Quality tier:** **B+** — Very recent full preprint with real Norwegian data; not yet peer-reviewed and transfer is cross-domain.

### 36. A Polynomial-time, Truthful, Individually Rational and Budget Balanced Ridesharing Mechanism

- **Citation:** Tatsuya Iwase, Sebastian Stein, Enrico H. Gerding (2021). *International Joint Conference on Artificial Intelligence (IJCAI-21)*.
- **Persistent link:** https://doi.org/10.24963/ijcai.2021/38; [landing page](https://www.ijcai.org/proceedings/2021/38); full PDF không phân phối trong repo (local corpus: pdfs/36-a-polynomial-time-truthful-individually-rational-and-budget-balanced-rid.pdf); structured review cục bộ (local corpus: structured-digests/36-structured-review.md); raw digest cục bộ (local corpus: digests/36-digest.txt).
- **Provenance:** Metadata verified from `https://www.ijcai.org/proceedings/2021/38`; full text is official publisher/proceedings full text; peer-reviewed: **yes**.
- **Full-text audit:** 8 pages; 6,834 extracted words; SHA-256 `272198276c56dac05ff826aec2907725ec6e6808053a46f374c9f956f54bc906`.
- **Method:** GARS-NIR is a monotone polynomial-time greedy allocation plus payments designed to be truthful, individually rational, and budget balanced for general ridesharing.
- **Data/evaluation:** Synthetic instances and New York City taxi data; compared with optimal and naive mechanism baselines.
- **Numerical result or theorem:** The mechanism proves DSIC, IR, budget balance, and polynomial time; its average social cost is within 8.6% of optimal in the reported evaluation.
- **Limitations:** No worst-case efficiency approximation, payments may still feel unfair, and evaluation does not model long-term participation or debt.
- **Direct BeGo-LTF use:** Add incentive compatibility/IR/budget balance as secondary evaluation dimensions when costs or driver rewards are introduced; compare against GARS-NIR without confusing payment fairness with burden fairness.
- **Quality tier:** **A*** — Top peer-reviewed AI venue; formal mechanism properties plus NYC-taxi and synthetic evaluation.

### 37. Cost-Sharing Mechanism Design for Ride-Sharing

- **Citation:** Shichun Hu, Maged M. Dessouky, Nelson A. Uhan, Phebe Vayanos (2021). *Transportation Research Part B: Methodological, 150*.
- **Persistent link:** https://doi.org/10.1016/j.trb.2021.06.018; [landing page](https://doi.org/10.1016/j.trb.2021.06.018); full PDF không phân phối trong repo (local corpus: pdfs/37-cost-sharing-mechanism-design-for-ride-sharing.pdf); structured review cục bộ (local corpus: structured-digests/37-structured-review.md); raw digest cục bộ (local corpus: digests/37-digest.txt).
- **Provenance:** Metadata verified from `https://doi.org/10.1016/j.trb.2021.06.018`; full text is USC institutional author manuscript linked to the Elsevier DOI; peer-reviewed: **yes**.
- **Full-text audit:** 54 pages; 17,787 extracted words; SHA-256 `cb353dfc05a4bad989b647a6b04c2eb1d95a2decaa0bdffc470107c3662ac90f`.
- **Method:** General online cost-sharing framework decomposes driver direct cost, uncertain future-passenger prediction, and inconvenience-cost discounts; analyzes online fairness, immediate response, IR, budget balance, and ex-post IC.
- **Data/evaluation:** Downtown Los Angeles traffic plus generated demand; primary scenarios use 1,000 requests and 300 or 500 drivers under several time and willingness-to-pay settings.
- **Numerical result or theorem:** Theorem 5 gives immediate response, IR, budget balance, and ex-post IC for the inconvenience-cost discount but not online fairness. In Scenario 1, ICBD cuts no-passenger vehicles from 87.34 to 46.23, while Basic Discount raises requests served from 74.67% to 75.76% and cuts empty vehicles to 62.03.
- **Limitations:** Cheapest-insertion routing replaces optimal routing, online fairness conflicts with the strongest discount, behavior is price-threshold based, and truly dynamic arrivals are left for future work.
- **Direct BeGo-LTF use:** Translate realized detour/time inconvenience into compensation or debt credits; explicitly report which desirable properties conflict instead of presenting one opaque fairness score.
- **Quality tier:** **A*** — Leading transportation-methodology journal with formal properties and real Los Angeles traffic evaluation.

### 38. Allocation Problems in Ride-sharing Platforms: Online Matching with Offline Reusable Resources

- **Citation:** John P. Dickerson, Karthik A. Sankararaman, Aravind Srinivasan, Pan Xu (2021). *ACM Transactions on Economics and Computation, 9(3), Article 13*.
- **Persistent link:** https://doi.org/10.1145/3456756; [landing page](https://dl.acm.org/doi/10.1145/3456756); full PDF không phân phối trong repo (local corpus: pdfs/38-allocation-problems-in-ride-sharing-platforms.pdf); structured review cục bộ (local corpus: structured-digests/38-structured-review.md); raw digest cục bộ (local corpus: digests/38-digest.txt).
- **Provenance:** Metadata verified from `https://dl.acm.org/doi/10.1145/3456756`; full text is author-hosted manuscript linked to the ACM DOI; peer-reviewed: **yes**.
- **Full-text audit:** 17 pages; 8,102 extracted words; SHA-256 `3fb738fe72a060b6c39f6cd9230ca7402fb9c800c666ab9aefaf82ced90253d7`.
- **Method:** Online matching with offline reusable resources under known adversarial distributions; LP-based non-adaptive allocation plus simulation-based attenuation and practical heuristics.
- **Data/evaluation:** New York City Yellow Cab 2013 records, training on 12 days/month-style slices to estimate arrival and occupation-time distributions.
- **Numerical result or theorem:** The main algorithm achieves competitive ratio 1/2-epsilon against the LP, and no adaptive algorithm can beat 1/2+o(1) for that benchmark; empirical LP-based ratios are roughly 0.5 to 0.7 and outperform greedy under the modeled distributions.
- **Limitations:** Requires known/estimated arrival and occupation distributions, benchmark tightness is relative to its LP, and the taxi data validates dispatch rather than repeated-member fairness.
- **Direct BeGo-LTF use:** Use as a scalable reusable-vehicle/driver matching baseline; add debt as a dual price or fairness regularizer and measure the efficiency gap to the LP oracle.
- **Quality tier:** **A** — Strong peer-reviewed theory journal article with NYC taxi data and tight competitive-ratio analysis.

### 39. Joint Pricing and Matching for City-Scale Ride-Pooling

- **Citation:** Sanket Shah, Meghna Lowalekar, Pradeep Varakantham (2022). *International Conference on Automated Planning and Scheduling (ICAPS-22)*.
- **Persistent link:** https://doi.org/10.1609/icaps.v32i1.19836; [landing page](https://ojs.aaai.org/index.php/ICAPS/article/view/19836); full PDF không phân phối trong repo (local corpus: pdfs/39-joint-pricing-and-matching-for-city-scale-ride-pooling.pdf); structured review cục bộ (local corpus: structured-digests/39-structured-review.md); raw digest cục bộ (local corpus: digests/39-digest.txt).
- **Provenance:** Metadata verified from `https://ojs.aaai.org/index.php/ICAPS/article/view/19836`; full text is official publisher/proceedings full text; peer-reviewed: **yes**.
- **Full-text audit:** 9 pages; 7,833 extracted words; SHA-256 `01790f5e8060fe53b12c632246f2af43a564430ca0633643f17749eb71c1a368`.
- **Method:** Batched joint pricing and matching: a revenue-maximizing auction acts as a meta-level optimizer, then Monte Carlo samples yield scalable posted prices.
- **Data/evaluation:** City-scale New York Yellow Taxi simulator, more than 300,000 weekday requests, multiple demand periods, 1,500-2,000 vehicles, and capacity 2 or 4.
- **Numerical result or theorem:** All one-minute decision intervals run in under 60 seconds; Myerson-style pricing improves revenue by 2.28% over no surge in the reported setting. The simple asymptotic analysis approaches 1-1/e about 0.632, and at n=5 the implemented price remains nearly 10% below optimal posted-price revenue.
- **Limitations:** Revenue rather than fairness, simulated acceptance curves, one-city data, and future information/multi-step pricing are left open.
- **Direct BeGo-LTF use:** Reuse the scalable candidate-generation/matching layer, then enforce 5-minute pickup, 10-minute added-delay, capacity, and long-term debt constraints before any pricing objective.
- **Quality tier:** **A** — Strong peer-reviewed planning venue with city-scale matching/pricing experiments.

### 40. Incentives in Ridesharing with Deficit Control

- **Citation:** Dengji Zhao, Dongmo Zhang, Enrico H. Gerding, Yuko Sakurai, Makoto Yokoo (2014). *International Conference on Autonomous Agents and Multiagent Systems (AAMAS-14)*.
- **Persistent link:** https://doi.org/10.5555/2615731.2617408; [landing page](https://www.ifaamas.org/Proceedings/aamas2014/aamas/p1021.pdf); full PDF không phân phối trong repo (local corpus: pdfs/40-incentives-in-ridesharing-with-deficit-control.pdf); structured review cục bộ (local corpus: structured-digests/40-structured-review.md); raw digest cục bộ (local corpus: digests/40-digest.txt).
- **Provenance:** Metadata verified from `https://www.ifaamas.org/Proceedings/aamas2014/aamas/p1021.pdf`; full text is official publisher/proceedings full text; peer-reviewed: **yes**.
- **Full-text audit:** 8 pages; 8,066 extracted words; SHA-256 `00ce2bd7df27cc8b9f38cfaa56c6d850ecb780927780b591ddf2cbc99ae0702d`.
- **Method:** Mechanism-design model jointly assigns commuters as drivers/riders, routes them under time windows, and compares VCG, fixed prices, and VCG with two-sided reserve prices and detour limits.
- **Data/evaluation:** Formal proofs and small illustrative route examples; no large empirical experiment.
- **Numerical result or theorem:** Theorem 2 proves no truthful, efficient, IR ridesharing mechanism can avoid outside subsidy. Theorem 5 makes two-sided-reserve VCG truthful and IR iff r0>=-r1; Theorem 6 is budget balanced with no detour and bounds deficit by -n_d delta_max r1 - n_r r0 otherwise.
- **Limitations:** No realistic simulation, optimal scheduling complexity is largely deferred, efficiency-deficit bounds remain future work, and monetary utility may not capture social-group norms.
- **Direct BeGo-LTF use:** If BeGo introduces payments, treat truthfulness, participation, deficit, and detour caps as a formal tradeoff. For the core thesis, mirror the insight with non-monetary compensation credits and bounded role burden.
- **Quality tier:** **A** — Top multi-agent venue and directly relevant mechanism theory; older and without empirical validation.
