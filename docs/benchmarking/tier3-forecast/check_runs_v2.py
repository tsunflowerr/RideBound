"""v2 (FORECAST-AMENDMENTS A1): world lines compared on the common bins; extra lines must be held bins.
Per-run checks for the Family B and snow runs of FORECAST-PLAN.md (after `run_forecast.py run tier1|tier2|snow`):
  status pass; v/e/p audit ok; inventory as in T3.8;
  forecast runs: every "[forecast] t=... bin=b ratio=x" line equals configs/r-table.json[cell][min(b, 8)] to 1e-6,
    and the "[world] t=... bin=..." lines (bins and factors) equal those of the paired T3.8 naive log;
  C-30F runs: 0 non-normal decisions;  snow runs: "[forecast] off" and the v1.3 guard line for 15/11 at 50400/54000.
Usage: python -B check_runs.py <tier1|tier2|snow> <out-log>
"""
from __future__ import annotations

import json
import pathlib
import re
import sys

HERE = pathlib.Path(__file__).resolve().parent
TANG3 = HERE.parent / "tang3"
for sub in ("sweep", "evidence", "w07", "t38"):
    sys.path.insert(0, str(TANG3 / sub))
sys.path.insert(0, str(HERE))
import audit_vep  # noqa: E402
import run_forecast as rf  # noqa: E402
import run_t38  # noqa: E402
import run_w07  # noqa: E402

RATIO = re.compile(r"^\[forecast\] t=\s*\d+s bin=(\d+) ratio=([\d.]+)$")
WORLD_BIN = re.compile(r"^\[world\] t=\s*\d+s bin=\d+ edges=\d+ factor=\[[\d.]+, [\d.]+\]$")
GUARD = re.compile(r"^\[world\] measured base check: day=(\S+) windowStart=(\d+) matches the scenario$")


def main(group: str, out: str) -> int:
    table = json.loads((rf.CONFIGS / "r-table.json").read_text(encoding="utf-8"))
    lines, problems = [f"# check_runs.py {group}"], []
    for name, arm, cell, kind, fconfig in rf.jobs(group):
        run = rf.OUT_ROOT / name
        summary = run / "summary.json"
        if not summary.exists():
            problems.append(f"{name}: no summary")
            continue
        s = json.loads(summary.read_text(encoding="utf-8"))
        if s.get("status") != "pass":
            problems.append(f"{name}: status {s.get('status')}")
        if s.get("repositoryInventorySha256") != run_w07.EXPECTED_INVENTORY:
            problems.append(f"{name}: inventory {s.get('repositoryInventorySha256')}")
        tbl, summ = audit_vep.audit(run / "transcript-00.ndjson")
        if tbl is None:
            problems.append(f"{name}: audit has no final checkpoint")
            continue
        log = (rf.LOG_ROOT / f"{name}.log").read_text(encoding="utf-8", errors="replace").splitlines()
        if fconfig is not None:
            ratios = [(int(m.group(1)), float(m.group(2))) for l in log if (m := RATIO.match(l))]
            bad = [(b, r) for b, r in ratios if abs(r - table[cell][min(b, 8)]) > 1e-6]
            if not ratios or bad:
                problems.append(f"{name}: ratio lines {len(ratios)}, mismatches {bad[:3]}")
            naive_log = (run_t38.LOG_ROOT / f"{arm}-{cell}.log")
            if not naive_log.exists():
                naive_log = run_t38.LOG_ROOT / f"{arm}-{cell}-rerun1.log"
            ours = [l for l in log if WORLD_BIN.match(l)]
            theirs = [l for l in naive_log.read_text(encoding="utf-8", errors="replace").splitlines() if WORLD_BIN.match(l)]
            common = min(len(ours), len(theirs))
            factor = lambda line: line.split("factor=")[1]  # noqa: E731
            held = [l for l in (ours[common:] + theirs[common:])]
            if ours[:common] != theirs[:common] or any(factor(l) != factor((ours or theirs)[min(8, common - 1)]) for l in held):
                problems.append(f"{name}: [world] bin lines differ from {naive_log.name} beyond held bins")
            if arm == "C-30" and summ["nonNormalCertificates"]:
                problems.append(f"{name}: {summ['nonNormalCertificates']} non-normal decisions")
            lines.append(f"{name}: pass; ratio lines {len(ratios)} ok; world bins {len(ours)} = naive; non-normal {summ['nonNormalCertificates']}")
        else:
            guard = [m.groups() for l in log if (m := GUARD.match(l))]
            if "[forecast] off" not in log or guard != [(rf.day(cell), str(rf.dl.WINDOW_STARTS[cell[-3:]]))]:
                problems.append(f"{name}: forecast-off or guard line wrong {guard}")
            lines.append(f"{name}: pass; guard {guard}; non-normal {summ['nonNormalCertificates']}")
    lines.append(f"problems: {len(problems)}")
    lines += ["  " + p for p in problems]
    text = "\n".join(lines) + "\n"
    with open(out, "x", encoding="utf-8", newline="\n") as handle:
        handle.write(text)
    sys.stdout.write(text)
    return 1 if problems else 0


if __name__ == "__main__":
    if len(sys.argv) != 3:
        raise SystemExit(__doc__)
    raise SystemExit(main(*sys.argv[1:]))
