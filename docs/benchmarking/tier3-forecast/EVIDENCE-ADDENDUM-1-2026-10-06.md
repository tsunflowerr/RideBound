# Evidence addendum 1 (2026-10-06): v1.3 wrapper checks on the DEVELOPMENT cell d20181112-s10-r1-w07, arm C-30

These are diagnostics on one development cell. They are not results, and no test cell was touched.

## Implementation status

- **Wrapper.** `E:\Code\Report_INT3508\ridebound-scratchpad\tang3\world\preflight_world_v1_3.py`.
- **Tests.** `test_preflight_world_v1_3.py` passes 23/23 checks. `mutate_preflight_world_v1_3.py` kills 8/8 mutants.
- **Forecast-off control reproduces v1.2 exactly.** File: `..\optimize\compare-v13-control-2026-10-06.txt`.
  - 718/718 fleet decisions are identical, including their times.
  - The signed v/e/p audit table is identical.
  - Served riders: 87 = 87. Frames: 2161 = 2161.
- **Forecast run with K = 2.** Source days are 13–16/11, and the ratio is applied to the Runner snapshot at each 15-min bin start.
  - Ratios per bin: 1.0559, 1.0575, 1.0312, 1.0283, 1.0223, 1.0183, 1.0140, 1.0106, then 1.0 from bin 8 on.
  - The run passed.

## Diagnostic

File: `..\optimize\smoke-breakdown-2026-10-06.txt`; script `smoke_breakdown.py`.

| | v1.2 (naive) | v1.3 forecast K = 2 |
|---|---|---|
| served | 87 | 85 |
| riders late > 60 s vs first drop promise | 7 | 0 |
| mean lateness vs first promise | +21.7 s | −1.7 s |
| sum of later drop revisions | 2079.7 s | 1606.5 s |
| sum of earlier drop revisions | 187.5 s | 1748.1 s |
| visible change > 60 s (share) | 0.080 | 0.224 |
| riders with a single ±60 s jump | 0.057 | 0.047 |

## Interpretation: a mechanism hypothesis, to be challenged

The simulated world is piecewise constant over 15-min bins: FleetPy changes edge times only at bin starts. The
Runner also receives a new snapshot only at bin starts, since the world marks the snapshot dirty only then.

A uniform ratio applied at the bin start therefore makes every leg driven inside the current bin look slower than it
really is. As the vehicle advances, ETAs are pulled earlier; at the next bin start they are pushed later again.

The forecast removes the lateness bias, but it creates two-way oscillation of the displayed ETA. The symmetric
visible-change metric counts that oscillation as burden.

The mean remaining time to drop-off at promise time is about 860 s, i.e. door-to-door
(`rider-experience-2026-10-05-report.txt`). Most of a trip promised at a bin start is driven inside that same bin.

## Candidate fixes (to debate)

- **(a) Time-dependent ETA in the Runner.** The Runner uses the current bin speed up to the bin end and forecast speeds after it. This is a core change in `RideBound.Domain`/`Application`: ADR, tests and `dotnet test`. It is the most faithful option and the most expensive.
- **(b) Anchored continuous horizon plus sub-bin snapshot updates (wrapper only).**
  - Every U seconds (e.g. 300 s) the snapshot is rebuilt with r(t) = (average factor over [t, t+D]) / (current factor). The current bin keeps the current factor; later bins use the current factor × forecast growth m(b+j)/m(b).
  - Inside a bin r(t) grows as the bin end approaches, so a trip promised early in the bin is barely inflated.
  - Extra clock stops change the event clock, so the naive comparator must be re-run with the same stops (ratio 1). That costs about twice the runs.
- **(c) Smaller inflation.** Use r = 1 + α(r_K − 1) with α < 1. This is a heuristic and does not remove the mechanism.
- **(d) Display rule.** Show the rider max(first promise, current ETA), i.e. never show an earlier time. This changes what "visible" means. Evidence says earliness hurts little, but this metric change is open to the charge of moving the goalposts.
- **(e) Drop the forecast direction.** Report the negative mechanism finding and pick another optimisation.

## Disk budget reminder

About 110 runs in total (C: 57 GB free with a 50 GB floor; E: 42 GB free with a 40 GB floor). Each run is about 80 MB.
