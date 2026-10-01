"""T3.8 single-job rerun after an infrastructure failure (T38-PLAN.md §2 "Lỗi khi chạy"; T38-AMENDMENTS.md A1).

Runs one job into the NEW output `<job>-rerun1` with label `<job>-rerun1-t38`; the failed folder is never touched.
Everything else is run_t38's own command (imported, not modified): same wrapper, runner, configs, fixtures, driver,
world, inventory. Refuses unless the failed folder exists WITHOUT a summary.json (so a finished job is never rerun),
the rerun folder does not exist, and the rerun is for a job named on the command line.
Usage: python -B run_t38_rerun.py <job-name>      e.g. C-30-d20181118-s10-r1-w17
"""
from __future__ import annotations

import sys
import time

import run_t38
import run_pilot


def main() -> int:
    if len(sys.argv) != 2:
        print(__doc__)
        return 2
    name = sys.argv[1]
    arm, cell = next(((a, c) for a in run_t38.CONFIG_SHA256 for c in run_t38.SPLIT["calibration"] + run_t38.SPLIT["test"]
                      if f"{a}-{c}" == name), (None, None))
    if arm is None:
        raise SystemExit(f"{name}: not a job of the plan")
    if run_t38.state_of(name) != "exists-no-summary":
        raise SystemExit(f"{name}: state {run_t38.state_of(name)}; a rerun is only for a folder without summary.json")
    rerun = f"{name}-rerun1"
    if (run_t38.OUT_ROOT / rerun).exists() or (run_t38.LOG_ROOT / f"{rerun}.log").exists():
        raise SystemExit(f"{rerun} already exists")
    phase = "calib" if cell in run_t38.SPLIT["calibration"] else "test"
    problems = run_t38.check_inputs(phase)
    if problems:
        raise SystemExit(f"input problems: {problems}")
    if run_t38.STOP_FILE.exists():
        raise SystemExit("STOP file present")
    run_pilot.keep_awake(True)
    started = time.time()
    try:
        done, label = run_t38.run_job(rerun, arm, cell)
    finally:
        run_pilot.keep_awake(False)
    print(f"[{time.time() - started:7.0f}s] {done} {label}", flush=True)
    return 0 if label == "status-pass" else 3


if __name__ == "__main__":
    raise SystemExit(main())
