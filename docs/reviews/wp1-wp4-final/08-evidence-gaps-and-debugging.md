# Verification, promising signal, gaps và debugging

## Gate cuối WP4

Ngày 2026-08-03 trên Windows/.NET 10:

| Gate | Kết quả |
|---|---|
| `dotnet test RideBound.slnx` | 557/557, 0 failed, 0 skipped |
| Contracts | 133 |
| Domain | 135 |
| Application | 69 |
| Algorithms | 134 |
| Solvers.OrTools | 6 |
| Runner | 71 |
| Architecture | 9 |
| Release `--no-restore /warnaserror` | 0 warning, 0 error |
| `dotnet format ... --verify-no-changes` | pass |
| NuGet direct/transitive vulnerability audit | không có package bị báo vulnerable |
| `git diff --check` | pass |

Windows Application Control `0x800711C7` từng chặn Contracts/Runner DLL ở mốc
cũ. Nó **không** tái xuất hiện trong gate này; required full solution pass thật,
không được ghi thành environment blocker hiện hành.

## Independent evidence, không chỉ expected-case tests

- B1 generator + selector so với independent enumerator trên 64 fixtures.
- C1 production mapper + actual OR-Tools so với independent enumeration trên 64
  fixtures; selected IDs bằng nhau, mọi level `OPTIMAL`, exact gap numerator 0.
- Hard-gate mutation test bắt buộc raw candidate count lớn hơn hard-feasible count;
  bỏ gate không thể pass do fixture vô tác dụng.
- Actual bounded generator omission truyền count/digest đến execution diagnostics;
  solver `UNKNOWN` được ghi riêng và no-op chỉ thành fallback sau validation.
- Infinite-limit C1=B1, C2 disabled=C1.
- Cache hit/miss/mutation equivalence; plan-pool checkpoint/tamper; retry/ACK,
  validation-budget exhaustion và safe fallback đều có behavior tests.

## Synthetic microbenchmark

Tool: `tools/RideBound.Wp4Microbenchmark`, 7 measured repetitions sau warm-up,
OR-Tools 9.15.6755, .NET 10.0.9, Windows 10.0.26200, X64, 12 logical processors.

| Vehicles × options | Boolean vars | p50 wall | p95 wall | p50 deterministic | p50 conflicts | Status |
|---|---:|---:|---:|---:|---:|---|
| 2 × 2 | 4 | 2,389 µs | 2,602 µs | 4 µs | 0 | optimal |
| 4 × 4 | 16 | 12,160 µs | 15,325 µs | 399 µs | 4 | optimal |
| 8 × 4 | 32 | 21,406 µs | 24,629 µs | 1,486 µs | 19 | optimal |
| 16 × 8 | 128 | 91,004 µs | 97,981 µs | 42,023 µs | 24 | optimal |

Đường cong này là synthetic candidate-selection model, không gồm travel lookup,
candidate generation, full commitment validation, process I/O hoặc demand thật.
Wall time phụ thuộc máy/JIT. Nó chỉ là tín hiệu rằng adapter hoàn tất exact small/
medium synthetic instances và cost tăng rõ theo problem size; không chứng minh
production scale hay service benefit.

## Những gì chưa chứng minh

- paired B1–B5/C1/C2 acceptance/service/runtime trên cùng real/replayed demand;
- candidate-loss bias dưới tight/medium caps;
- future acceptance/flexibility benefit của hold/repair/plan pool;
- non-inferiority margins, user-derived budgets hoặc user satisfaction;
- FleetPy/BeGo adapter correctness và cross-system reproducibility;
- incident recovery optimizer;
- HTTP/gRPC boundary hoặc persistence crash recovery.

WP5 refinement phải khóa Layer-1 adapter/transaction/evidence trước implementation;
WP6–WP9 mới đủ chỗ để kết luận effectiveness.

## Trace một lỗi thực tế

1. Decode/schema: envelope/payload witness.
2. Reducer: epoch/time/sequence và atomic event failure.
3. Generator: `CandidatePruneWitness` và `CandidateGenerationDiagnostics`.
4. Policy: hard/revision/warning assessment và effective policy.
5. Solver: status, per-level bound/gap, consumed deterministic work.
6. Execution: validation witnesses, primary rejected, fallback path.
7. Full validator: physical/lock/delta/budget/ledger witness.
8. Runner: certificate/state hashes, pending decision và ACK context/hash.
9. Restore: outer checkpoint hash, inner canonical state, plan pool/ledger links.

Không dùng reason code cuối như toàn bộ nguyên nhân. Candidate loss, solver loss và
publication rejection có ý nghĩa khác nhau và phải được đọc ở đúng stage.
