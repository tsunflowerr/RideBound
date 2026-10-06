# Evidence addendum 2 (2026-10-06): v1.4 on the same DEVELOPMENT cell, and the core's travel-time interface

These are diagnostics on one development cell (d20181112-s10-r1-w07, arm C-30). They are not results.

## v1.4 wrapper

- **Code.** `E:\Code\Report_INT3508\ridebound-scratchpad\tang3\world\preflight_world_v1_4.py`.
- **Tests.** `test_preflight_world_v1_4.py` passes 21/21 checks. `mutate_preflight_world_v1_4.py` kills 8/8 mutants.
- **What it does.**
  - The Runner snapshot is rebuilt every U = 300 s. The event clock also stops at multiples of U, for both the naive and the forecast mode.
  - Ratio: r(t) = (1/D) ∫_t^{t+D} g, where g = 1 in the current 15-min bin and g = forecast growth m(b)/m(b0) after it.
  - Naive mode uses the same clock with r = 1.

## Results

File: `..\optimize\smoke-breakdown-v14-2026-10-06.txt`. Script: `smoke_breakdown_v14.py`.

| Run | Served | Late > 60 s vs first promise | Mean lateness | Later / earlier drop revisions | Visible > 60 s | Single ±60 s jump |
|---|---|---|---|---|---|---|
| v1.2 naive (bin clock) | 87 | 7 | +21.7 s | 2079.7 / 187.5 s | 0.080 | 0.057 |
| v1.3 forecast, K = 2, per-bin ratio | 85 | 0 | −1.7 s | 1606.5 / 1748.1 s | 0.224 | 0.047 |
| v1.4 naive, U = 300 | 87 | 5 | +20.5 s | 1978.1 / 195.7 s | 0.057 | 0.046 |
| v1.4 forecast, D = 900, U = 300 | 84 | 0 | +4.2 s | 1694.9 / 1342.0 s | 0.167 | 0.000 |
| v1.4 forecast, D = 1800, U = 300 | 86 | 0 (3 riders early > 60 s) | −16.9 s | 1476.5 / 2930.7 s | 0.314 | 0.000 |

Reading:
- **The forecast removes the lateness bias** relative to the first promise: late > 60 s goes from 5–7 riders to 0, and mean lateness from about +21 s to about 0.
- **It creates two-way ETA churn.** Earlier revisions go from about 190 s to 1300–2900 s, so the symmetric visible-change metric gets worse.
- **Large single jumps disappear** under v1.4: the share of riders with a ±60 s single jump drops to 0.
- **Root cause.** The Runner has ONE static travel matrix. Inflating it to mimic future congestion also inflates the legs driven in the next few minutes at the current speed. Vehicles then run "ahead of the plan", and ETAs are pulled earlier at every update. Sub-bin updates with a continuous horizon reduce the effect, but do not remove it.

## The core's travel-time interface

- `C:\Code\RideBound-noworse\src\RideBound.Domain\Validation\ITravelTimeLookup.cs` exposes only `TryGetTravelTime(from, to, out Duration)`. There is no departure time.
- It is used in 7 places, including:
  - `RideBound.Application\Scheduling\RouteScheduleProjector.cs` (ETAs; 179 lines);
  - `RideBound.Algorithms\Candidates\CandidateScheduleEvaluator.cs` (90 lines);
  - `ForwardSlackProfile.cs` (567 lines; slack-based insertion checks assume additive static delays);
  - `RideBound.Domain\Validation\PhysicalPlanValidator.cs` (837 lines).
- A correct time-dependent model would change all of these. An example is the Ichoua–Gendreau–Potvin piecewise-speed model, which preserves FIFO: travel time from departure t = solve ∫ ds/f(s) = base time.
- That change would also need ADR, tests, `dotnet test`, review, a new runner and protocol/adapter changes. It is a core change, rated large and risky for a time budget of about 5 weeks that also includes thesis writing.

## Candidate ways forward (for the round-2 debate)

- **(A) Core time-dependent ETAs.**
  - Implement them in the projector and validator only, or end to end.
  - This is the faithful fix, with high cost and risk.
  - A partial version (projector only, static feasibility) creates inconsistencies between the promises and the feasibility checks.
- **(B) Static forecast as an honest trade-off study (wrapper only).**
  - On the test cells, measure the accuracy-vs-churn trade-off of adding a time-of-day correction to a static ETA model.
  - Pre-register both views:
    - symmetric churn (visible change, ±jumps);
    - asymmetric "arrive-by" display: the rider is shown the running maximum of the promise, i.e. never an earlier time. Visible burden is then only the increases above the first promise, and lateness is measured against the first promise.
  - Delivery and Via evidence says lateness against the communicated time is what hurts. The arrive-by display is how "arrives by" products show it. But adding this metric after seeing development data must be declared.
- **(C) Pivot away from the forecast to another optimisation.** The development evidence above would then be reported as a negative/mechanism finding.

## Budget reminders

- **Disk.** About 110 runs in total. 5 development runs of about 80 MB each are already used, in `C:\RideBoundData\research\tier3-forecast-checks-v1`.
- **Time.** Research until about mid-November 2026.
