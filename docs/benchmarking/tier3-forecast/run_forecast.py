"""FORECAST-PLAN.md driver: gates, Family B closed loop (world wrapper v1.3, unchanged), snow tier.

Groups (run one group per invocation, in this order):
  gates : S1 Mplus-30F smoke on development cell d20181112-s10-r1-w07 (task B inputs, forecast-check-k2.json);
          E1 Mplus-30 forecast OFF and E2 C-30 forecast K=1 sham on seeded weekday test cells (T3.8 inputs).
  tier1 : C-30F, Mplus-30F on the 8 weekday 07:00 test cells     } refused until GATES-PASSED.txt exists
  tier2 : C-30F, Mplus-30F on the 8 weekday 08:00 test cells     } (written by check_gates.py after E1/E2 pass)
  snow  : C-30 forecast OFF (v1.3) on the 8 snow cells 15/11 14:00/15:00 (descriptive, scored by the display layer)
Forecast configs: configs/forecast-k2-<day>.json (forecast-v1, horizonBins 2, causal-no15 sources) and the E2 sham
configs/forecast-k1-<day>.json; the expected per-bin ratios are in configs/r-table.json (sealed with the plan).
Everything else is the T3.8 command (runner v2, WP4 v2, configs C-30/Mplus-30, fixtures, drivers, worlds, inventory),
with the wrapper replaced by preflight_world_v1_3.py. Outputs C:/RideBoundData/research/tier3-forecast-v1/<job>.
Safety: AC power (waits up to 20 min), STOP file, disk gate per job (C: free - 51,200 MiB >= 410 MiB), logs mode x,
outputs never reused, seeded order within a group (random.Random(20261008)).
Usage: python -B run_forecast.py plan|run <group>
"""
from __future__ import annotations

import concurrent.futures
import hashlib
import json
import os
import pathlib
import random
import shutil
import subprocess
import sys
import time

HERE = pathlib.Path(__file__).resolve().parent
TANG3 = HERE.parent / "tang3"
for sub in ("pilot", "w07", "t38", "world"):
    sys.path.insert(0, str(TANG3 / sub))
sys.path.insert(0, str(HERE))
import display_layer as dl  # noqa: E402
import preflight_world  # noqa: E402
import preflight_world_v1_2 as v12  # noqa: E402
import preflight_world_v1_3 as v13  # noqa: E402
import run_pilot  # noqa: E402
import run_t38  # noqa: E402
import run_w07  # noqa: E402

WRAPPER = TANG3 / "world" / "preflight_world_v1_3.py"
OUT_ROOT = pathlib.Path(r"C:/RideBoundData/research/tier3-forecast-v1")
LOG_ROOT = HERE / "forecast-logs"
STOP_FILE = HERE / "STOP"
GATES_PASSED = HERE / "GATES-PASSED.txt"
CONFIGS = HERE / "configs"
SEED = 20261008
FLOOR_MIB, NEED_MIB = 51_200, 410
POWER_WAIT_S = 1200
W07 = tuple(f"d201811{d}-s10-r{r}-w07" for d, rs in (("14", (3, 4)), ("15", (1, 2, 4)), ("16", (1, 2, 4))) for r in rs)
W08 = tuple(c[:-3] + "w08" for c in W07)
SNOW = tuple(f"d20181115-s10-r{r}-{w}" for w in ("w14", "w15") for r in (1, 2, 3, 4))
E1_CELL = random.Random(SEED).choice(sorted(W08))
E2_CELL = random.Random(SEED + 1).choice(sorted(W07))
DEV_CELL = "d20181112-s10-r1-w07"


def day(cell):
    return f"2018-11-{cell[7:9]}"


def jobs(group):
    """(job name, arm config, cell, kind) with kind in dev / test / snow; forecast config path or None."""
    if group == "gates":
        items = [(f"Mplus-30F-{DEV_CELL}-S1", "Mplus-30", DEV_CELL, "dev", HERE / "forecast-check-k2.json"),
                 (f"Mplus-30-{E1_CELL}", "Mplus-30", E1_CELL, "test", None),
                 (f"C-30-{E2_CELL}", "C-30", E2_CELL, "test", CONFIGS / f"forecast-k1-{day(E2_CELL)}.json")]
    elif group in ("tier1", "tier2"):
        cells = W07 if group == "tier1" else W08
        items = [(f"{arm}F-{c}", arm, c, "test", CONFIGS / f"forecast-k2-{day(c)}.json") for c in cells for arm in ("C-30", "Mplus-30")]
    elif group == "snow":
        items = [(f"C-30-{c}", "C-30", c, "snow", None) for c in SNOW]
    else:
        raise SystemExit(f"unknown group {group}")
    if group != "gates":
        random.Random(SEED).shuffle(items)
    return items


def inputs(cell, kind):
    if kind == "dev":
        return run_w07.FIXTURE_ROOT / cell, run_w07.DRIVER_ROOT / f"{cell}.driver.json", run_w07.world_file(cell)
    if kind == "snow":
        return (pathlib.Path(r"C:/RideBoundData/research/fixtures-snow-v1") / cell, HERE / "snow" / "drivers" / f"{cell}.driver.json",
                HERE / "snow" / "worlds" / f"W5-{cell[:9]}-{cell[-3:]}.json")
    return run_t38.FIXTURE_ROOT / cell, run_t38.DRIVER_ROOT / f"{cell}.driver.json", run_t38.world_file(cell)


def command(name, arm, cell, kind):
    fixture, driver, _ = inputs(cell, kind)
    label = f"{arm}-{cell}-t38" if kind == "test" and not name.startswith(arm + "F") else f"{name}-fc"
    return [str(run_pilot.PYTHON), "-B", str(WRAPPER), "--label", label, "--fleetpy-root", str(run_pilot.FLEETPY),
            "--runner-root", str(run_w07.RUNNER), "--dotnet", str(run_pilot.DOTNET),
            "--commitment-config", str(run_pilot.CONFIGS / f"{arm}.json"), "--wp4-config", str(run_w07.WP4),
            "--scenario", str(fixture / "scenario-content.json"),
            "--derivative-manifest", str(fixture / "derivative-manifest.json"),
            "--normalization-report", str(fixture / "normalization-report.json"),
            "--selection-frame", str(fixture / "selection-frame.json"),
            "--driver", str(driver), "--output", str(OUT_ROOT / name), "--repeats", "1", "--master-seed", "7",
            "--expected-repository-inventory-sha256", run_w07.EXPECTED_INVENTORY]


def free_mib():
    return shutil.disk_usage("C:\\").free // (1024 * 1024)


def state_of(name):
    out = OUT_ROOT / name
    if not out.exists():
        return "pending"
    summary = out / "summary.json"
    return "status-" + json.loads(summary.read_text(encoding="utf-8")).get("status", "?") if summary.exists() else "exists-no-summary"


def run_job(item):
    name, arm, cell, kind, fconfig = item
    if state_of(name) != "pending":
        return name, state_of(name)
    log = LOG_ROOT / f"{name}.log"
    if log.exists():
        return name, "log-exists-pending"
    if STOP_FILE.exists():
        return name, "deferred-stop"
    if free_mib() - FLOOR_MIB < NEED_MIB:
        return name, f"deferred-disk ({free_mib()} MiB free)"
    deadline = time.time() + POWER_WAIT_S
    while not run_pilot.may_start_job():
        if time.time() > deadline or STOP_FILE.exists():
            return name, "deferred-on-battery"
        time.sleep(30)
    env = dict(os.environ, PYTHONDONTWRITEBYTECODE="1", PYTHONIOENCODING="utf-8", RB_WORLD_CONFIG=str(inputs(cell, kind)[2]))
    env.pop("RB_FORECAST_CONFIG", None)
    if fconfig is not None:
        env["RB_FORECAST_CONFIG"] = str(fconfig)
    started = time.time()
    done = subprocess.run(command(name, arm, cell, kind), cwd=str(run_pilot.REPO), capture_output=True, text=True,
                          encoding="utf-8", errors="replace", env=env)
    with open(log, "x", encoding="utf-8", newline="\n") as handle:
        handle.write(f"returncode={done.returncode} seconds={time.time() - started:.0f}\n" + done.stdout + done.stderr)
    return name, state_of(name)


def check_inputs(group):
    problems = run_w07.check_inputs()
    for arm in ("C-30", "Mplus-30"):
        p = run_pilot.CONFIGS / f"{arm}.json"
        if hashlib.sha256(p.read_bytes()).hexdigest() != run_t38.CONFIG_SHA256[arm]:
            problems.append(f"config {arm} SHA-256 mismatch")
    if group in ("tier1", "tier2") and not GATES_PASSED.exists():
        problems.append("tier refused: GATES-PASSED.txt does not exist")
    saved = preflight_world.ALLOWED_WINDOW_STARTS
    preflight_world.ALLOWED_WINDOW_STARTS = v13.WINDOW_STARTS_V1_3
    try:
        for name, arm, cell, kind, fconfig in jobs(group):
            fixture, driver, world = inputs(cell, kind)
            if not (fixture.exists() and driver.exists() and world.exists()):
                problems.append(f"{name}: missing fixture, driver or world")
                continue
            if json.loads(driver.read_text(encoding="utf-8"))["sourceScenarioContentSha256"] != \
                    hashlib.sha256((fixture / "scenario-content.json").read_bytes()).hexdigest():
                problems.append(f"{name}: driver hash differs from the fixture")
            try:
                checked = v12.check_measured_base(json.loads(world.read_text(encoding="utf-8")), fixture / "scenario-content.json")
                if checked is None or checked[0] != day(cell):
                    problems.append(f"{name}: world check {checked}")
                if fconfig is not None:
                    v13.load_forecast(fconfig, json.loads(world.read_text(encoding="utf-8"))["base"])
            except SystemExit as error:
                problems.append(f"{name}: refused: {error}")
    finally:
        preflight_world.ALLOWED_WINDOW_STARTS = saved
    return problems


def main():
    if len(sys.argv) != 3 or sys.argv[1] not in ("plan", "run"):
        print(__doc__)
        return 2
    group = sys.argv[2]
    items = jobs(group)
    if sys.argv[1] == "plan":
        for item in items:
            print(f"{item[0]}\t{state_of(item[0])}\tforecast={item[4].name if item[4] else 'off'}")
        print("E1 cell", E1_CELL, "E2 cell", E2_CELL, "| free MiB", free_mib())
        print("input problems:", check_inputs(group) or "none")
        print("example:", " ".join(command(*items[0][:4])))
        return 0
    problems = check_inputs(group)
    if problems:
        raise SystemExit(f"input problems: {problems}")
    OUT_ROOT.mkdir(parents=True, exist_ok=True)
    LOG_ROOT.mkdir(exist_ok=True)
    run_pilot.keep_awake(True)
    started, deferred = time.time(), 0
    try:
        with concurrent.futures.ThreadPoolExecutor(run_pilot.WORKERS) as pool:
            futures = [pool.submit(run_job, item) for item in items]
            for done, future in enumerate(concurrent.futures.as_completed(futures), start=1):
                name, label = future.result()
                deferred += label.startswith("deferred") or label == "log-exists-pending"
                print(f"[{time.time() - started:7.0f}s] {name:<40} {label:<24} ({done}/{len(items)})", flush=True)
    finally:
        run_pilot.keep_awake(False)
    return 3 if deferred else 0


if __name__ == "__main__":
    raise SystemExit(main())
