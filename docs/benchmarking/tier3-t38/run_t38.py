"""T3.8/T3.9 driver (plan: T38-PLAN.md, sealed in T38-PLAN.sha256 and pushed before any job).

Real traffic only, the 49 panel A episodes of ../calib/panelA (days 14-18/11 x r1..r4 x windows 07, 08, 17, minus the
11 cells the normalizer could not build), split by split-t38-v1.json:
  phase calib: C-30 on the 15 calibration episodes (15 jobs);
  phase test:  U, C-30, Mplus-30, Mplus-60, V-30 on the 34 test episodes (170 jobs). Refused until the calibrated
               threshold file GAMMA_FILE exists (so gamma is fixed before any test run).
World: the measured day and window of the episode (../calib/panelA/worlds/W5-<day>-<window>.json, sigma =
shockRate = 0) through world wrapper v1.2; recovery rule (b) for every arm (runner-tier3-v2 + wp4-tier3-v2); fixtures
in C:/RideBoundData/research/fixtures-panelA-v1 and drivers in ../calib/panelA/drivers (outside every repository);
cwd E:/Code/RideBound-deadline (never modified) with --expected-repository-inventory-sha256.

Safety as run_w07x.py: AC power only (a job waits up to 20 min for AC before it is deferred), STOP file next to this
script, outputs never reused, logs mode "x" (a pending job whose log exists is reported, not retried), Windows kept
awake.
Usage: python -B run_t38.py plan|run|report calib|test
"""
from __future__ import annotations

import concurrent.futures
import hashlib
import json
import os
import pathlib
import subprocess
import sys
import time

HERE = pathlib.Path(__file__).resolve().parent
TANG3 = HERE.parent
for sub in ("pilot", "w07", "world"):
    sys.path.insert(0, str(TANG3 / sub))

import preflight_world  # noqa: E402
import preflight_world_v1_2 as v12  # noqa: E402
import run_pilot  # noqa: E402
import run_w07  # noqa: E402

PANEL = TANG3 / "calib" / "panelA"
SPLIT = json.loads((HERE / "split-t38-v1.json").read_text(encoding="utf-8"))
SPLIT_SHA256 = "b72ac0f731fa7425891f364f0976054ae172a3117a507155ebe7e5c122ea03f1"
PHASES = {
    "calib": (("C-30",), tuple(SPLIT["calibration"])),
    "test": (("U", "C-30", "Mplus-30", "Mplus-60", "V-30"), tuple(SPLIT["test"])),
}
CONFIG_SHA256 = {  # the files task B and task A ran with (make_configs_w07x.log proves C-30, Mplus-60, V-30)
    "U": "998a0a94e7865c3b6e890f1bed9bf27f7f6c9e0de1f73d02a349ddc3de91c273",
    "C-30": "d6124f3f964d8385db381d53b75c142cf2ac870b22823d6675325c3808808beb",
    "Mplus-30": "cc7d7838836137f432dc025b7c45aa19c10bb82010663a9fb6f1dc52ec7c8f7d",
    "Mplus-60": "f8d4c74913224cdfcf13f4a1e8ebffe3e87850fd507e542c8ba333ee5d1f22cc",
    "V-30": "2462484ab8d7e61efc36de8dc1cd00054dd258e9ec0276f734e9a01212f005d5",
}
OUT_ROOT = pathlib.Path(r"C:/RideBoundData/research/tier3-t38-v1")
FIXTURE_ROOT = pathlib.Path(r"C:/RideBoundData/research/fixtures-panelA-v1")
DRIVER_ROOT = PANEL / "drivers"
WORLDS = PANEL / "worlds"
LOG_ROOT = HERE / "logs"
STOP_FILE = HERE / "STOP"
GAMMA_FILE = HERE / "GAMMA-t38-v1.json"
POWER_WAIT_S = 1200


def world_file(cell: str) -> pathlib.Path:
    return WORLDS / f"W5-{cell[:9]}-{cell[-3:]}.json"


def jobs(phase: str):
    arms, cells = PHASES[phase]
    for cell in cells:              # cell by cell, so each cell's arms finish close together
        for arm in arms:
            yield f"{arm}-{cell}", arm, cell


def state_of(name: str) -> str:
    output = OUT_ROOT / name
    if not output.exists():
        return "pending"
    summary = output / "summary.json"
    if summary.exists():
        return "status-" + json.loads(summary.read_text(encoding="utf-8")).get("status", "unknown")
    return "exists-no-summary"


def command(name: str, arm: str, cell: str) -> list[str]:
    fixture = FIXTURE_ROOT / cell
    return [
        str(run_pilot.PYTHON), "-B", str(run_w07.PREFLIGHT),
        "--label", f"{name}-t38",
        "--fleetpy-root", str(run_pilot.FLEETPY),
        "--runner-root", str(run_w07.RUNNER),
        "--dotnet", str(run_pilot.DOTNET),
        "--commitment-config", str(run_pilot.CONFIGS / f"{arm}.json"),
        "--wp4-config", str(run_w07.WP4),
        "--scenario", str(fixture / "scenario-content.json"),
        "--derivative-manifest", str(fixture / "derivative-manifest.json"),
        "--normalization-report", str(fixture / "normalization-report.json"),
        "--selection-frame", str(fixture / "selection-frame.json"),
        "--driver", str(DRIVER_ROOT / f"{cell}.driver.json"),
        "--output", str(OUT_ROOT / name),
        "--repeats", "1",
        "--master-seed", "7",
        "--expected-repository-inventory-sha256", run_w07.EXPECTED_INVENTORY,
    ]


def wait_for_power() -> bool:
    deadline = time.time() + POWER_WAIT_S
    while not run_pilot.may_start_job():
        if time.time() > deadline or STOP_FILE.exists():
            return False
        time.sleep(30)
    return True


def run_job(name, arm, cell):
    if state_of(name) != "pending":
        return name, state_of(name)
    log = LOG_ROOT / f"{name}.log"
    if log.exists():
        return name, "log-exists-pending"
    if STOP_FILE.exists():
        return name, "deferred-stop"
    if not wait_for_power():
        return name, "deferred-on-battery"
    env = dict(os.environ, PYTHONDONTWRITEBYTECODE="1", PYTHONIOENCODING="utf-8", RB_WORLD_CONFIG=str(world_file(cell)))
    started = time.time()
    completed = subprocess.run(command(name, arm, cell), cwd=str(run_pilot.REPO), check=False, capture_output=True,
                               text=True, encoding="utf-8", errors="replace", env=env)
    with open(log, "x", encoding="utf-8", newline="\n") as handle:
        handle.write(f"returncode={completed.returncode} seconds={time.time() - started:.0f}\n"
                     + completed.stdout + completed.stderr)
    return name, state_of(name)


def check_inputs(phase: str) -> list[str]:
    problems = run_w07.check_inputs()           # wrapper present, runner v2 tree, WP4 v2 (its own cells are task B's)
    if hashlib.sha256((HERE / "split-t38-v1.json").read_bytes()).hexdigest() != SPLIT_SHA256:
        problems.append("split manifest SHA-256 mismatch")
    if phase == "test" and not GAMMA_FILE.exists():
        problems.append(f"phase test refused: {GAMMA_FILE.name} does not exist (calibrate first)")
    arms, cells = PHASES[phase]
    for arm in arms:
        path = run_pilot.CONFIGS / f"{arm}.json"
        if not path.exists() or hashlib.sha256(path.read_bytes()).hexdigest() != CONFIG_SHA256[arm]:
            problems.append(f"config {arm} missing or SHA-256 mismatch")
    saved = preflight_world.ALLOWED_WINDOW_STARTS
    preflight_world.ALLOWED_WINDOW_STARTS = v12.WINDOW_STARTS_V1_2
    try:
        for cell in cells:
            fixture, driver, world = FIXTURE_ROOT / cell, DRIVER_ROOT / f"{cell}.driver.json", world_file(cell)
            if not (fixture.exists() and driver.exists() and world.exists()):
                problems.append(f"missing fixture, driver or world for {cell}")
                continue
            declared = json.loads(driver.read_text(encoding="utf-8"))["sourceScenarioContentSha256"]
            if hashlib.sha256((fixture / "scenario-content.json").read_bytes()).hexdigest() != declared:
                problems.append(f"{cell}: fixture scenario SHA-256 differs from the driver")
            try:                                 # the wrapper's own guard and factor load, without running anything
                config = json.loads(world.read_text(encoding="utf-8"))
                checked = v12.check_measured_base(config, fixture / "scenario-content.json")
                base = config["base"]
                factors = preflight_world.measured_base(base["day"], 1.0, int(base["windowStart"]), 24)
                if checked != (f"2018-11-{cell[7:9]}", window_start(cell)) or len(factors) != 24:
                    problems.append(f"{cell}: world check {checked}, {len(factors)} factors")
            except SystemExit as error:
                problems.append(f"{cell}: world refused: {error}")
    finally:
        preflight_world.ALLOWED_WINDOW_STARTS = saved
    return problems


def window_start(cell: str) -> int:
    return {"w07": 25200, "w08": 28800, "w17": 61200}[cell[-3:]]


def main() -> int:
    if len(sys.argv) != 3 or sys.argv[1] not in ("plan", "run", "report") or sys.argv[2] not in PHASES:
        print(__doc__)
        return 2
    phase = sys.argv[2]
    items = list(jobs(phase))
    if sys.argv[1] in ("plan", "report"):
        for name, *_ in items:
            print(f"{name}\t{state_of(name)}")
        states = [state_of(name) for name, *_ in items]
        print(len(items), {state: states.count(state) for state in sorted(set(states))})
        if sys.argv[1] == "plan":
            print("input problems:", check_inputs(phase) or "none")
            print("example:", " ".join(command(*items[0])))
        return 0
    problems = check_inputs(phase)
    if problems:
        raise SystemExit(f"input problems: {problems}")
    OUT_ROOT.mkdir(parents=True, exist_ok=True)
    LOG_ROOT.mkdir(parents=True, exist_ok=True)
    run_pilot.keep_awake(True)
    started = time.time()
    deferred = 0
    try:
        with concurrent.futures.ThreadPoolExecutor(run_pilot.WORKERS) as pool:
            futures = [pool.submit(run_job, *item) for item in items]
            for done, future in enumerate(concurrent.futures.as_completed(futures), start=1):
                name, label = future.result()
                deferred += label.startswith("deferred") or label == "log-exists-pending"
                print(f"[{time.time() - started:7.0f}s] {name:<34} {label:<22} ({done}/{len(items)})", flush=True)
    finally:
        run_pilot.keep_awake(False)
    return 3 if deferred else 0


if __name__ == "__main__":
    raise SystemExit(main())
