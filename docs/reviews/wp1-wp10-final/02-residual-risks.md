# Residual risks, limits and rejected fixes

## Residual risks that remain open

1. **Finite-panel inference.** WP9 contains only 20 cells and five independent
   travel-day realizations per capacity stratum. Its achieved precision is about
   1.40 pp against a 1.00 pp margin. It supports the finite-panel verdict only,
   not a population confidence interval or universal effect.
2. **Bounded candidate generation.** Work/candidate caps are transparent and
   deterministic but remain approximations. Their diagnostics are not a general
   quality-loss bound.
3. **RidePy position capability.** `nodeOnly` cannot expose concurrent mid-edge
   progress. Inferring position from time would invent simulator state, so the
   limitation remains a named failure rather than being patched around.
4. **Container rebuild versus restore.** The exact executed RidePy image is now
   archived and content-addressed, so it can be restored. The Dockerfile still uses
   evolving apt/pip indexes for transitive build dependencies, so a future clean
   build is not claimed byte-reproducible. The image ID/labels/source receipt and
   archive hash—not an overclaim about Dockerfile determinism—are the authority.
5. **Review strength.** Opening every file and passing static/test/mutation gates is
   strong assurance, not a formal proof. Rare environment-specific failures and
   untested workload shapes can remain.
6. **Dirty worktree ownership.** WP9/WP10 artifacts and generated `__pycache__`
   files belong to the ongoing user work. They were inspected where relevant and
   deliberately not deleted or reset.

## Tempting changes that were rejected

- Do not loosen the preregistered 1 pp margin or substitute C2 after seeing H6.
- Do not interpret near-zero C1 revision burden as better optimization when locks
  forbid revisions and service is refused.
- Do not pool WP10's 11 valid pairs with WP9 or drop the failed stress pair.
- Do not import random/direction/distance/sparse filtering from the literature
  without a RideBound-specific loss bound.
- Do not prune a temporally infeasible partial route as a whole subtree unless the
  travel contract first guarantees the monotonic assumptions needed by that proof.
- Do not copy BeGo/OptiGo entities into the portable core or reimplement the Runner
  algorithm inside Python/C++ adapters.
- Do not claim dynamic insertion, ETA/route stability, least commitment,
  reassignment or user satisfaction as novel.
