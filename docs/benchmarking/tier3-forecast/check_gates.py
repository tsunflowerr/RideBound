"""After `run_forecast.py run gates`: decide the gates of FORECAST-PLAN.md.
  S1: the Mplus-30F development smoke passed (status pass, audit ok, [forecast] lines present). Implementation only.
  E1: Mplus-30 forecast OFF (v1.3) is EQUIVALENT (compare_equivalence) to the T3.8 run of the same cell.
  E2: C-30 forecast K=1 sham (v1.3) is EQUIVALENT to the T3.8 run of the same cell, and its log shows ratio 1.000000
      on every scaled snapshot.
If E1 and E2 pass, GATES-PASSED.txt is written (mode x); otherwise GATES-FAILED.txt, and the plan's fallback applies.
Usage: python -B check_gates.py <out-log>
"""
from __future__ import annotations

import json
import pathlib
import re
import sys

HERE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import compare_equivalence as ce  # noqa: E402
import run_forecast as rf  # noqa: E402
import run_t38  # noqa: E402
import t38_calibrate  # noqa: E402

RATIO = re.compile(r"^\[forecast\] t=\s*\d+s bin=(\d+) ratio=([\d.]+)$")


def main(out: str) -> int:
    lines = ["# check_gates.py"]
    s1 = rf.OUT_ROOT / f"Mplus-30F-{rf.DEV_CELL}-S1"
    s1_status = json.loads((s1 / "summary.json").read_text(encoding="utf-8")).get("status") if (s1 / "summary.json").exists() else "missing"
    s1_lines = [l for l in (rf.LOG_ROOT / f"{s1.name}.log").read_text(encoding="utf-8", errors="replace").splitlines() if RATIO.match(l)] \
        if (rf.LOG_ROOT / f"{s1.name}.log").exists() else []
    s1_ok = s1_status == "pass" and len(s1_lines) > 0
    lines.append(f"S1 {s1.name}: status {s1_status}; ratio lines {len(s1_lines)} -> {'PASS' if s1_ok else 'FAIL'}")
    results = {}
    for gate, arm, cell in (("E1", "Mplus-30", rf.E1_CELL), ("E2", "C-30", rf.E2_CELL)):
        new = rf.OUT_ROOT / f"{arm}-{cell}"
        old = run_t38.OUT_ROOT / t38_calibrate.effective(f"{arm}-{cell}")
        if not (new / "summary.json").exists():
            results[gate] = False
            lines.append(f"{gate}: {new.name} has no summary -> FAIL")
            continue
        comparison = ce.compare(ce.facts(new), ce.facts(old))
        equivalent = all(s for s, _ in comparison.values())
        extra = ""
        if gate == "E2":
            log = (rf.LOG_ROOT / f"{new.name}.log").read_text(encoding="utf-8", errors="replace").splitlines()
            ratios = [float(m.group(2)) for l in log if (m := RATIO.match(l))]
            equivalent = equivalent and len(ratios) > 0 and all(r == 1.0 for r in ratios)
            extra = f"; sham ratio lines {len(ratios)}, all 1.000000: {all(r == 1.0 for r in ratios)}"
        results[gate] = equivalent
        lines.append(f"{gate} {new.name} vs {old}: " + "; ".join(f"{k} {'same' if s else 'DIFFERENT ' + d}" for k, (s, d) in comparison.items())
                     + f"{extra} -> {'PASS' if equivalent else 'FAIL'}")
    passed = s1_ok and results.get("E1") and results.get("E2")
    lines.append(f"GATES {'PASSED' if passed else 'FAILED'}")
    text = "\n".join(lines) + "\n"
    target = rf.GATES_PASSED if passed else HERE / "GATES-FAILED.txt"
    with open(target, "x", encoding="utf-8", newline="\n") as handle:
        handle.write(text)
    with open(out, "x", encoding="utf-8", newline="\n") as handle:
        handle.write(text)
    sys.stdout.write(text)
    return 0 if passed else 1


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit(__doc__)
    raise SystemExit(main(sys.argv[1]))
