# Evidence pack for the design debate (2026-10-06). Facts only, each with its source file. Read the sources if in doubt.

## 0. Context and goal

RideBound is a ride-pooling dispatcher thesis (UET/VNU, bachelor level). Research time left: until about mid-November 2026.
The advisor criticised it as "system-side, not UX; what is the business value?". The owner gave full authority to run a
new, properly designed experiment that adds real research AND practical value, with honest results.

The direction chosen so far (owner agreed to explore it):
- **Forecast-aware promises.** When the dispatcher (the "Runner") promises pickup/drop-off times, it should use a
  time-of-day travel-time forecast built from OTHER days, instead of assuming current traffic persists.
- **Stress test.** Use the real 15 Nov 2018 snowstorm (unanticipated congestion) as a stress set.

The direction may be challenged in the debate.

## 1. What the existing results say (all from real-traffic simulation, NYC Manhattan, Nov 2018, 8 vehicles, 108 requests per cell)

- **T3.8/T3.9 test set: 34 cells, 3,672 requests.** File: `E:\Code\Report_INT3508\ridebound-scratchpad\tang3\t38\T38-REPORT.md`; `analysis-2026-10-02-report.txt`.
  - Arms:
    - U = no gate;
    - C-30 = thesis rule, dispatch-caused drop ETA change per rider ≤ 30 s;
    - Mplus-30 = deadline rule of Schulz & Pfeiffer 2026, arrive no later than first promise + 30 s;
    - Mplus-60;
    - V-30 = cap on rider-visible change.
  - Served: U 2434, C-30 2338, Mplus-30 2338, Mplus-60 2347, V-30 2336.
  - C-30 is never forced (0/49 runs).
  - Mplus-30 is forced in 41.3% of decisions and breaks its own promise for 14.5% of riders (cell mean). In the 07–09 window this is 36.5%.
- **Rider experience, post-hoc.** File: `...\tang3\t38\rider-experience-2026-10-05-report.txt`; script `t38_rider_experience.py`.
  - Visible drop change > 60 s (cell-mean share): U 10.96%, C-30 5.78%, Mplus-30 5.70%, V-30 5.70%.
  - Late > 60 s vs first drop promise: U 9.10%, C-30 4.12%, Mplus-30 4.03%.
  - Late > 300 s: U 0.77%, the capped rules 0.
  - Wait ≈ 387 s for all arms. Ride time: U 485 s, C-30 474 s.
  - C-30 vs Mplus-30: every rider metric is equal within ±0.1 pp or ±1 s; 2,332 riders are served by both.
  - 07–09 window, all arms: late > 60 s is 12.7–14.5%; visible > 60 s is 15.2–17.0%. The caps barely help there.
  - Vehicle switches: 0 in every arm.
- **Attribution identity check.** File: `...\business-need\contract-split-check-2026-10-06.txt`; script `contract_split_check.py`.
  - For every served rider in 7 runs: final drop lateness = Σ signed dispatch part (p − e) + Σ signed traffic part (e − v) + execution residual (alight − last promise).
  - The identity is exact. The residual is 0, because the Runner snapshot equals FleetPy's travel times.
  - Example, 15/11 r1 07–09: 10 riders are late > 60 s, all 100% traffic, identical under U, C-30 and Mplus-30.
  - Example, 17/11 r1 17–19 under U: one rider is 266.3 s late, of which 245.9 s is dispatch.
- **Predictability of traffic change.** File: `...\optimize\forecast-predictability-2026-10-06.txt`; script `forecast_predictability.py`.
  - Method: citywide 15-min factor. The forecast is the mean of the other same-kind days (weekday from the other weekdays, weekend from the other weekend day). The error is |predicted − actual| of the ratio f(t+h)/f(t).

    | Window | Horizon | Actual change | Naive error | Forecast error |
    |---|---|---|---|---|
    | 07–09 | 30 min | 9.79% | 9.79% | 2.44% |
    | 07–09 | 45 min | 14.85% | 14.85% | 3.08% |
    | 08–10 | 30 min | 4.77% | 4.77% | 1.86% |
    | 17–19 | 30 min | 3.83% | 3.83% | 3.17% |

    In 17–19 the maximum forecast error (13.22%) exceeds the naive maximum (9.83%).
- **Day profiles.** File: `...\optimize\day-profiles-2026-10-06.txt`.
  - On 15/11 (snowstorm, confirmed by NBC/CBS/HuffPost news), the factor relative to the mean of the other weekdays is 1.36 at 14:00, 1.47 at 15:00, 1.92 at 16:00, 2.17 at 17:00 and 2.14 at 18:00.
  - Largest 15-min step 06–22h per day: 10.6–16.2%.
- **Demand in snowstorm windows.** File: `...\optimize\demand-windows-2026-10-06.txt`.
  - 15/11 sample 1: 14–16h 2,150 requests; 15–17h 1,529; 16–18h 992; 17–19h 986.
  - Other weekdays: about 2,500–3,100.
  - The demand files are completed TLC trips, so the snowstorm "demand" is what got served.
  - The panel A 15/11 17–19 cells failed normalization (node cap).
- **Snowstorm cells.** Being generated now: 15/11, windows 14–16h and 15–17h, samples r1–r4. Script: `...\optimize\snow\normalize_snow.py`; status is in the `.out` file next to it.
- **Business-need sources.** Verified quotes: `...\business-need\SOURCES-2026-10-05.md`.
  - Via field data: lateness vs the shown ETA reduces later rides; $5 credits.
  - 49 CFR 37.131(f)(3)(ii) excuses traffic "not anticipated at the time a trip was scheduled".
  - Appendix D: failing to plan for regular rush hour is the operator's fault.
  - NYC Access-A-Ride: $10 per late trip unless beyond control.
  - Delivery studies: lateness vs the communicated time hurts more than earliness helps.
  - Lyft experiment: request elasticity to shown wait is −0.0427.

## 2. Technical facts for the implementation

- **The Runner's travel snapshot** is built in `E:\Code\RideBound-deadline\simulators\fleetpy-ridebound\ridebound_fleetpy\fleet_control.py:549` (`_build_travel_snapshot`).
  - It reads `routing_engine.return_travel_costs_1to1` (the same edge times FleetPy uses to move vehicles).
  - It converts seconds to ms in `mapping.py:303`.
- **World wrapper** `...\tang3\world\preflight_world.py:171` (`install`) sets the edge times per 15-min bin and marks the snapshot dirty. v1.2 = `preflight_world_v1_2.py`.
  - In the measured-day world (sigma = shockRate = 0) the factor is uniform across all 9,120 edges. The log shows `factor=[x, x]`.
- **Proposed v1.3 wrapper** (scratchpad only, NO change to the repositories): patch the snapshot so that every arc the Runner sees = measured arc × r(t).
  - r(t) = forecast growth over a horizon H, i.e. the mean over [t, t+H] of m(τ)/m(t), with m = mean profile of the other same-kind days.
  - FleetPy still drives the measured times.
  - Uniform scaling leaves shortest paths unchanged and changes only the durations, i.e. promises and time-window feasibility.
  - This is not implemented yet.
- **Existing naive runs that could be reused**, if a v1.3 run with the forecast switched off reproduces them frame-identically:
  - T3.8 test: 34 cells × {U, C-30, Mplus-30, Mplus-60, V-30}, in `C:\RideBoundData\research\tier3-t38-v1`;
  - T3.8 calibration: 15 cells × C-30;
  - task B: 8 development cells, 07–09 on 12–13/11, many arms, in `tier3-w07-v1`.
- **Run cost.** About 1,160 s per job, 6 workers in parallel, about 200 s per job effective. About 80 MB per run.
- **Hard disk budget.**
  - C: has 57 GB free and the policy floor is 50 GB, so about 7 GB ≈ 85 runs.
  - E: has 42 GB free and the floor is 40 GB, so about 2 GB ≈ 25 runs.
  - Never delete anything to free space. Total ≈ 110 runs maximum.
- **Power and storage rules.** The laptop must be on AC power to run. New outputs go on C:.

## 3. Rules that bind every decision

- **No novelty claims** (AGENTS.md): dynamic insertion, ETA limits, reassignment, time consistency, least-commitment and user satisfaction are NOT novel. Time-of-day travel-time forecasting is standard and also NOT novel. A contribution can be a measured effect, a mechanism, or a tool.
- **No fabricated numbers.** Every number used later must come from a saved script and output. Untested items are labelled "unverified".
- **Seal first.** The plan and predictions are sealed (SHA-256), committed and pushed before the first job. Thresholds are fixed before seeing outcomes. Tuning on the test cells is not allowed.
- **Repositories.** Do not modify `E:\Code\RideBound-deadline` (jobs run from it) or the main tree `E:\Code\RideBound`. Delete no file.
- **Honest reporting.** Report negative results honestly. Do not choose data because it makes the system look good.
