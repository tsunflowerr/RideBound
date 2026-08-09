# Verification và reproduction ledger

## 1. Pinned inputs

- RideBound commit: `44ef6a7cacdc58e7c6c0576430fcd7bb02e76c7a`.
- BeGo pre-WP5 commit: `ebe0d34365ec4751bd5c629677733032490a1a0d`.
- Runner DLL:
  `C:\Users\quang\AppData\Local\Temp\ridebound-wp5-runner-20260805\RideBound.Runner.dll`.
- Runner SHA-256:
  `ec5f224c058d69f6121e127a39f447f421c36e94094e6106517294ce222ad9bc`.
- PostgreSQL evidence runtime: PostgreSQL 17 in local Docker, fresh DB per full gate.

## 2. Commands đã chạy trong closure audit

```powershell
# RideBound authority
dotnet test RideBound.slnx

# BeGo Debug, với OPTIGO_WP5_POSTGRES/RUNNER_* /RIDEBOUND_ROOT đã set
dotnet test src/OptiGo.slnx --no-restore

# BeGo Release warning gate trên fresh DB khác
dotnet test src/OptiGo.slnx -c Release --no-restore /p:TreatWarningsAsErrors=true

# Format
dotnet format src/OptiGo.slnx --verify-no-changes --no-restore

# Frontend
npm test
npm run lint
npx tsc --noEmit
npm run build

# Dependency audits
dotnet list src/OptiGo.slnx package --vulnerable --include-transitive
dotnet list RideBound.slnx package --vulnerable --include-transitive
npm audit --audit-level=high
```

## 3. Kết quả

| Gate | Passed | Failed | Skipped | Ghi chú |
|---|---:|---:|---:|---|
| RideBound Debug | 557 | 0 | 0 | Contracts 133, Domain 135, Application 69, Algorithms 134, Solver 6, Runner 71, Architecture 9 |
| BeGo Debug full | 154 | 0 | 0 | real PostgreSQL + published Runner + paired + independent hard-crash evidence |
| BeGo Release warn-as-error | 154 | 0 | 0 | fresh PostgreSQL; full evidence rerun |
| Frontend Node | 9 | 0 | 0 | duplicate/stale/malformed/rebound realtime cases included |
| Format | — | — | — | exit 0 after three mechanical legacy whitespace fixes |
| NuGet/npm vulnerability | — | — | — | 0 vulnerability reported by configured sources |

## 4. WP5-013 artifact

Path:
`E:\Code\BeGo\artifacts\ridebound\wp5-independent-v1\wp5-013-20260809-final`.

- manifest SHA-256:
  `e21fb0877fbc6d61bf6f1e24adcda24e09a29fea95a9f44d1b61bf4fc1061ca2`;
- 18 listed files, independent rehash 18 match / 0 mismatch;
- transition oracle: 16,384 steps = 12,261 accepted + 4,123 rejected;
- contention: exact expected/observed sets at 2/3/4 workers;
- hard crash: 8 decision + 4 outbox boundaries;
- explicit mutants: 5/5 killed;
- queue curves: 8/32/64 × 1/2/4 workers, randomized order, one warm-up + five reps.

Paired Layer-1 artifact manifest remains
`b843bd20cbe9bf887d00998d4eaad54258848eb41d87ae49fd18a2142a0cb807`.

## 5. Coverage meaning

Các gate chứng minh deterministic mechanics, database constraints/concurrency,
crash recovery, artifact integrity và bounded local cost. Chúng không chứng minh:

- mọi possible fault schedule đã được exhaust;
- PostgreSQL serializability theo formal history checker;
- production traffic SLA/tail latency;
- SignalR client durable receipt;
- effectiveness/non-inferiority của C1;
- generalization ngoài fixture/data chưa xây ở WP6–WP9.

## 6. Reproduction hygiene

Luôn dùng fresh DB cho migration down/up test; không tái dùng DB có commit data vì
guard cố ý từ chối destructive rollback. Không sửa/publish lại Runner DLL tại cùng
path sau khi ghi hash. Giữ Debug và Release counts riêng; không cộng RideBound,
BeGo và frontend thành một con số marketing.
