# WP6 planning and seed vectors

`seed-vectors.json` publishes exact HMAC-SHA-256 and non-negative int32 outputs for
changes to master seed, scenario, repeat, component and stable item address. Recreate
them in a clean process with:

```powershell
dotnet run --project tools/RideBound.Wp6SeedVectors -- E:\Code\RideBound
```

The compiler accepts caller-supplied canonical policy configuration and capability
bytes, hashes them, binds policy/protocol/candidate/validator/solver/work/pairing
mechanics into `effectiveConfigurationSha256`, then validates comparability. It sorts
scenario/arm semantic sets, hashes the canonical plan and materializes the complete
warm-up + measured grid before any result exists.

Warm-up repeat addresses precede measured addresses in one collision-free repeat
space. Within each `(scenario, repeat)`, arm order is HMAC-counterbalanced. Adapter and
simulator seeds are shared across paired arms; solver/failure seeds include `armId`.
The compiler does not choose commitment budgets, ETA materiality thresholds or a
non-inferiority margin; those remain caller-owned open decisions O-002/O-003/O-004.
