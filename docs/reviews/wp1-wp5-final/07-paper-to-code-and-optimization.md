# Paper-to-code và optimization audit

## Cách dùng nguồn

Paper cung cấp mechanism/risk/test strategy; không cung cấp numeric default hoặc
novelty tự động. Mọi dòng dưới đã đối chiếu primary source qua in-app Browser và
giữ claim limit ở `docs/21-paper-to-design-evidence.md`.

| Nguồn | Mechanism dùng | Code/evidence | Không được suy diễn |
|---|---|---|---|
| Dial-a-ride/dynamic ridesharing literature | rolling insertion, time/capacity feasibility | WP2 B1, route schedule, physical validator | dynamic insertion là novelty |
| Gaul et al. 2021 | rolling-horizon optimization, fair compute accounting | WP4 candidate/solver boundaries | paper runtime/service figures là target RideBound |
| Schulz & Pfeiffer 2026 | forward slack, reuse/precompute, horizon tradeoff | `ForwardSlackProfile`, versioned cache | horizon paper là default; reassignment được mở |
| Tiwari et al. 2024 | compare weighted/Pareto/lexicographic objectives | multi-pass CP-SAT, hard-before-soft | weighted score chứng minh fairness |
| Ackermann & Rieck 2025 | multiple plans/commitment-flexibility tradeoff | B5 `VersionedPlanPool`/`MultiplePlanPolicy` | nhiều solve time luôn tốt hơn |
| Commitment/ETA stability literature | history/material revision nên được đo riêng | 10-vector ledger, visible delta | satisfaction hoặc novelty |
| LDFI, Alvaro et al. | enumerate failure boundaries from dataflow dependencies | 8 decision + 4 outbox hard-crash points | implementation là formal LDFI hoặc exhaustive proof |
| Elle, Kingsbury & Alvaro | independent history checking thay happy-path assertions | test-owned transition/exact-set contention oracle | proof serializability hoặc dùng Elle checker |
| QuickCheck, Claessen & Hughes | generated histories + shrinkable invariant mindset | 256×64 deterministic transition histories | dùng QuickCheck tool hoặc formal completeness |
| DeMillo–Lipton–Sayward / mutation testing | perturb guard and require oracle to detect semantic fault | five explicit unique/ACK/outbox/fingerprint/hash mutants | external mutation score; 5/5 đại diện toàn code |
| Georges–Buytaert–Eeckhout | warm-up, repeated samples, raw disclosure, cautious comparison | randomized 1 warm-up + 5 reps; raw queue curves | statistical significance, production SLA |
| Transactional outbox/idempotency systems guidance | atomic local write + retryable stable message ID | T2 outbox, attempt fence, client dedup | exactly-once transport/client receipt |

## Optimization được áp dụng thật

1. **Algorithmic:** best-first candidate enumeration + admissible bound + forward
   slack; cache invalidated theo version; deterministic stable merge.
2. **Solver:** multi-pass lexicographic objective, overflow proof, time/status-aware
   validator-pass fallback.
3. **Persistence:** per-run row serialization nhưng cross-run `SKIP LOCKED`
   parallelism; partial unique indexes và keyset pagination.
4. **Recovery:** checkpoint + suffix replay thay full history; long-lived bounded
   Runner process nhưng fresh reconstruction khi outcome uncertain.
5. **Publication:** per-run-head claim, no DB lock over network, exponential bounded
   retry, concurrent scope per different run, T3 `Applied` gate.
6. **Evidence:** deterministic randomized order, independent oracle, real abrupt
   process death, source/config/assembly-bound artifacts.

## Tối ưu cố ý không làm

- không thêm speculative reassignment vì O-001/no-reassignment đã khóa;
- không hedged duplicate Runner execution vì có thể tăng cost và phức tạp fence;
- không dùng arbitrary weighted sum để rút ngắn CP-SAT passes;
- không giữ DB transaction qua Runner/SignalR I/O;
- không thêm ML demand forecast hay học user tolerance từ synthetic data;
- không gọi local claim-drain curve là throughput của hệ thống end-to-end.

Kết luận: paper mechanisms được hiện thực ở nơi có contract/evidence phù hợp; không
có đoạn code được thêm chỉ để “có paper citation”.
