# Post-WP10 exact-reuse optimization evidence

> Date: 2026-08-23  
> Decision: ADR-052  
> Evidence class: mechanical equivalence and machine-local performance  
> Claim boundary: no service-effectiveness, population, SLA or paper speed-up claim

## 1. Why this optimization was selected

The full text of six well-known dynamic ride-pooling/DARP papers was read page by
page from locally retained PDFs before code was changed. Their exact hashes and
the applicable/rejected mechanisms are recorded in
[`docs/21-paper-to-design-evidence.md`](../21-paper-to-design-evidence.md). The
relevant common lesson from Gschwind–Drexl and Schulz–Pfeiffer is that expensive
route artifacts may be reused only under an exact state/route/travel identity and
must be invalidated explicitly.

RideBound deliberately did **not** import their full constant-time insertion
test. Its physical validator also enforces capacity, connectivity, frozen-prefix,
accepted/onboard preservation and plan-version rules, while a generic travel
snapshot does not promise the triangle inequality. It would therefore be unsafe
to treat a temporal failure as a monotone subtree proof. Direction, random and
sparse candidate filters from other papers were also rejected because they change
the candidate pool without a RideBound loss bound.

The accepted change removes only exact work already implied by the current key:

1. `ForwardSlackCacheKey` previously allocated a textual position fingerprint on
   every lookup even though the immutable `VehicleState` reference in the same key
   already binds position, load, rider sets and active route.
2. A terminal search node previously allocated and hashed a second key to verify a
   prefetched lookup created by that exact node. `Matches` now compares the already
   materialized key directly against all exact inputs.

The full physical validator still runs. Cache failures are still not retained.
Search order, work budgets, candidate identities, candidate cap and solver input do
not change.

## 2. Reproducible benchmark protocol

The source-controlled harness is
`tools/RideBound.CandidateHotPathBenchmark`. It runs Release/net10.0 on a fixed
complete directed travel snapshot and records:

- seven within-process repetitions for each sample;
- 250,000 key constructions at routes with 4/8/16 remaining stops;
- complete generator calls at 4/8/12 mutable stops;
- current-thread allocated bytes;
- exact exploration/evaluation/feasible/omission/retention/slack-miss counters.

Three clean processes were executed before and three after the change. Raw JSON
is retained outside the repository at `E:\RideBoundData\optimization`:

| Artifact | SHA-256 |
|---|---|
| `candidate-hot-path-baseline-20260823-r1.json` | `f5ae4681632cef45eb456dd12db7111bf535b3284795abb7e6191daa5ebfd4f5` |
| `candidate-hot-path-baseline-20260823-r2.json` | `ee68b52e933a47674968dd8f3e39824ba30404ec215d638966b5281a2bd74c79` |
| `candidate-hot-path-baseline-20260823-r3.json` | `8846587e7c9a6b2a41350974e792e1518d100caa650ac57526ff61eb744fae06` |
| `candidate-hot-path-optimized-20260823-r1.json` | `9a6d08a46628917c6761e26f08099ec5a524ac830e260b6156d0b7c02b8f4985` |
| `candidate-hot-path-optimized-20260823-r2.json` | `9761e7b17d744c0163f8d7293a67fe4ddb66435cd85aa9bcc0baf7bd6e466dea` |
| `candidate-hot-path-optimized-20260823-r3.json` | `cc5aaa92e4c1dd8415b3dc04de4b4c4803227a10efee2b832f477455cb2b65cc` |

Each table value below is the median of the three process-level p50 values. Timing
is descriptive because the machine was not an isolated laboratory host.

## 3. Result

### Cache-key component

| Route stops | Baseline µs / 250k | Optimized µs / 250k | Change | Baseline bytes | Optimized bytes | Allocation change |
|---:|---:|---:|---:|---:|---:|---:|
| 4 | 48,170 | 43,955 | −8.8% | 40,000,072 | 28,000,072 | **−30.0%** |
| 8 | 58,469 | 54,894 | −6.1% | 40,000,072 | 28,000,072 | **−30.0%** |
| 16 | 80,861 | 77,082 | −4.7% | 40,000,072 | 28,000,072 | **−30.0%** |

The 12,000,000-byte reduction is exact in all nine key samples: 48 bytes per
lookup. That is the removed position string allocation. Timing moves in the same
direction for the isolated component but is not promoted to an SLA.

### Complete generator

| Mutable stops | Baseline p50 µs | Optimized p50 µs | Timing change | Baseline bytes | Optimized bytes | Allocation change |
|---:|---:|---:|---:|---:|---:|---:|
| 4 | 28,649 | 27,251 | −4.9% | 7,267,328 | 7,172,816 | −1.30% |
| 8 | 171,967 | 176,784 | +2.8% | 62,238,624 | 61,599,792 | −1.03% |
| 12 | 540,335 | 530,113 | −1.9% | 268,696,928 | 266,578,328 | −0.79% |

End-to-end timing is mixed/noisy and is reported exactly that way. Heap reduction
is consistent and the change is kept for that mechanical benefit, not for a speed
claim.

## 4. Semantic-equivalence gates

Every baseline/optimized process reported the same work profile:

| Incumbents | Work units | Evaluated paths | Feasible before cap | Omitted paths | Retained | Slack misses |
|---:|---:|---:|---:|---:|---:|---:|
| 2 | 468 | 450 | 451 | 0 | 100 | 452 |
| 4 | 3,108 | 3,060 | 3,061 | 0 | 100 | 3,062 |
| 6 | 10,000 | 9,908 | 9,909 | 1,194 | 100 | 11,013 |

`CandidateSearchWorkProfileTests` and the cached/uncached structural mutation tests
also pass. The final required .NET suite is 855/855; Release build has zero warnings
and errors. Therefore the accepted conclusion is narrow: exact cache identity now
allocates less, with no observed semantic or bounded-search work change.

## 5. Limits

- This is not Gschwind–Drexl's reported `3.8×` result and does not implement that
  paper's full feasibility test.
- It does not improve the negative WP9 service result or the WP10 capability result.
- It does not prove speed or allocation behavior on a production workload.
- It does not authorize heuristic candidate pruning, reassignment, a new horizon,
  a changed service margin or a different confirmatory arm.
