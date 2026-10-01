"""Checks for the T3.8 scripts, on outputs whose values are already recorded elsewhere (no new simulation).

1. t38_calibrate.episode_score on the 8 task B C-30 runs equals ../calib/h3-estimate-2026-09-30.txt (0.1 s).
2. t38_calibrate.log_problems accepts the 8 task B C-30 logs (07:00 window) and rejects them when told the window is
   08:00 (mutation), and when the world file differs (mutation).
3. run_t38.check_inputs rejects a world of the wrong window and of the wrong day (mutations).
4. split-t38-v1.json: 49 episodes, disjoint, no group on both sides, forced group in calibration, and the manifest is
   reproduced byte for byte by make_split_t38.main.
5. conformal.rank for n = 15, alpha = 1/10 is 15 (gamma = the largest calibration score).
Usage: python -B test_t38.py   (prints each check; exits 1 on any failure or on fewer checks than expected)
"""
from __future__ import annotations

import json
import pathlib
import sys
import tempfile

HERE = pathlib.Path(__file__).resolve().parent
TANG3 = HERE.parent
sys.path.insert(0, str(HERE))
for sub in ("calib", "w07"):
    sys.path.insert(0, str(TANG3 / sub))

import conformal  # noqa: E402
import make_split_t38  # noqa: E402
import run_t38  # noqa: E402
import run_w07  # noqa: E402
import t38_calibrate  # noqa: E402

EXPECTED_CHECKS = 25
H3 = {"d20181112-s10-r1-w07": 123.8, "d20181112-s10-r2-w07": 112.2, "d20181112-s10-r3-w07": 112.3,
      "d20181112-s10-r4-w07": 164.4, "d20181113-s10-r1-w07": 106.2, "d20181113-s10-r2-w07": 204.1,
      "d20181113-s10-r3-w07": 94.4, "d20181113-s10-r4-w07": 119.7}
results = []


def check(name: str, ok: bool, detail: str = "") -> None:
    results.append(ok)
    print(f"{'PASS' if ok else 'FAIL'} {name} {detail}")


for cell, expected in H3.items():
    score, riders, _, _ = t38_calibrate.episode_score(run_w07.OUT_ROOT / f"C-30-{cell}")
    check(f"score {cell}", score != conformal.INFINITE and abs(score / 1000 - expected) < 0.05,
          f"{score} ms vs {expected} s; riders {riders}")

saved = (run_t38.LOG_ROOT, run_t38.world_file, run_t38.window_start)
run_t38.LOG_ROOT = run_w07.LOG_ROOT
run_t38.world_file = run_w07.world_file
try:
    clean = [p for cell in H3 for p in t38_calibrate.log_problems(f"C-30-{cell}", cell)]
    check("task B logs accepted", clean == [], str(clean[:2]))
    run_t38.window_start = lambda cell: 28800
    wrong = t38_calibrate.log_problems("C-30-d20181112-s10-r1-w07", "d20181112-s10-r1-w07")
    check("wrong window rejected", any("guard" in p for p in wrong) and any("factor" in p for p in wrong), str(wrong))
    run_t38.window_start = saved[2]
    run_t38.world_file = lambda cell: run_t38.WORLDS / "W5-d20181114-w07.json"
    wrong = t38_calibrate.log_problems("C-30-d20181112-s10-r1-w07", "d20181112-s10-r1-w07")
    check("wrong world file rejected", any("SHA-256" in p for p in wrong), str(wrong))
finally:
    run_t38.LOG_ROOT, run_t38.world_file, run_t38.window_start = saved

check("inputs clean (calib)", run_t38.check_inputs("calib") == [])
saved_phases, saved_world = dict(run_t38.PHASES), run_t38.world_file
try:
    run_t38.PHASES["calib"] = (("C-30",), ("d20181114-s10-r1-w07",))
    run_t38.world_file = lambda cell: run_t38.WORLDS / "W5-d20181114-w08.json"
    found = run_t38.check_inputs("calib")
    check("world of the wrong window refused", any("does not match the scenario start" in p for p in found), str(found))
    run_t38.world_file = lambda cell: run_t38.WORLDS / "W5-d20181115-w07.json"
    found = run_t38.check_inputs("calib")
    check("world of the wrong day refused", any("does not match the scenario day" in p for p in found), str(found))
finally:
    run_t38.PHASES.clear()
    run_t38.PHASES.update(saved_phases)
    run_t38.world_file = saved_world

split = json.loads((HERE / "split-t38-v1.json").read_text(encoding="utf-8"))
calibration, test = set(split["calibration"]), set(split["test"])
check("49 episodes", len(calibration | test) == 49 and len(calibration) + len(test) == 49)
check("disjoint", not calibration & test)
check("no group on both sides", not {e[:16] for e in calibration} & {e[:16] for e in test})
check("forced group in calibration", {f"d20181114-s10-r1-{w}" for w in ("w07", "w08", "w17")} <= calibration)
check("calibration groups match episodes", sorted({e[:16] for e in calibration}) == split["calibrationGroups"])
with tempfile.TemporaryDirectory() as tmp:
    manifest, log = pathlib.Path(tmp) / "m.json", pathlib.Path(tmp) / "m.log"
    make_split_t38.main(str(manifest), str(log))
    check("split reproduced byte for byte", manifest.read_bytes() == (HERE / "split-t38-v1.json").read_bytes())
check("rank n=15 alpha=1/10", conformal.rank(15, 1, 10) == 15)
check("rank n=14 alpha=1/10", conformal.rank(14, 1, 10) == 14)

saved_state, saved_out = run_t38.state_of, run_t38.OUT_ROOT
with tempfile.TemporaryDirectory() as tmp:
    run_t38.OUT_ROOT = pathlib.Path(tmp)
    (pathlib.Path(tmp) / "J").mkdir()
    check("effective: failed folder without rerun stays", t38_calibrate.effective("J") == "J")
    (pathlib.Path(tmp) / "J-rerun1").mkdir()
    check("effective: failed folder with rerun goes to rerun", t38_calibrate.effective("J") == "J-rerun1")
    (pathlib.Path(tmp) / "J" / "summary.json").write_text('{"status": "pass"}', encoding="utf-8")
    check("effective: finished folder is never replaced", t38_calibrate.effective("J") == "J")
run_t38.state_of, run_t38.OUT_ROOT = saved_state, saved_out

failures = results.count(False)
print(f"{len(results)} checks, {failures} failure(s)")
raise SystemExit(1 if failures or len(results) != EXPECTED_CHECKS else 0)
