# Repository guidance

## RideBound tasks

Before changing anything, read:

1. `docs/00-index.md`
2. `docs/01-research-charter.md`
3. `docs/18-status-and-decision-log.md`
4. The active work package in `docs/16-roadmap-and-work-packages.md`
5. The topic-specific document linked by the index

Use `docs/18-status-and-decision-log.md` as the live source of truth. Update it,
the relevant decision entry, and `docs/19-requirement-traceability.md` whenever
a task changes implementation status, contracts, claims, baselines, metrics,
repository boundaries, or next actions.

RideBound is an independent repository. Do not copy the BeGo/OptiGo source tree
into this repository. Reuse only audited generic code with provenance and tests;
keep BeGo-specific mapping in an adapter.

The Domain and Application layers must remain independent of OptiGo entities,
EF Core, ASP.NET, map providers, OR-Tools, and simulator libraries. Simulator
adapters must call the same versioned runner; never reimplement RideBound in
Python, C++, or adapter code.

Do not claim dynamic insertion, ETA limits, reassignment, route similarity,
least-commitment, time consistency, or user satisfaction as novel. The permitted
research boundary is documented in `docs/03-related-work-and-claim-boundary.md`.

Preserve user-owned and unrelated changes. Never delete research corpora,
downloaded data, vendor checkouts, result artifacts, or local configuration as
cleanup.

## Verification baseline

Run for every code change:

```powershell
dotnet test RideBound.slnx
```

Integration work involving BeGo must also keep its separately recorded baseline
passing. See `docs/18-status-and-decision-log.md` for the exact commands and
latest counts.
