"""FORECAST-PLAN.md snow tier (descriptive; negative control for unanticipated congestion): display-layer replay on the
8 C-30 forecast-off runs of 15/11 14:00 and 15:00 (tier3-forecast-v1), causal-no15 sources (12, 13, 14/11), using the
same functions as forecast_family_a.py. PS1: C-30 naive late60 cell mean in [3%, 15%]; the display removes <= 15% of
it; >= 90% of late seconds (riders late > 60 s vs the naive promise) are unanticipated (A - D0 - dispatch part).
Usage: python -B family_snow.py <out-prefix>
"""
from __future__ import annotations

import pathlib
import statistics
import sys

HERE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import display_layer as dl  # noqa: E402
import forecast_family_a as fa  # noqa: E402
import run_forecast as rf  # noqa: E402


def main(prefix: str) -> int:
    lines = ["# family_snow.py (snow tier, descriptive)"]
    per = {}
    for cell in rf.SNOW:
        prof = dl.profile("2018-11-15", cell[-3:], "causal-no15")
        per[cell] = fa.cell_metrics(fa.riders(rf.OUT_ROOT / f"C-30-{cell}", cell, prof), None)
        m = per[cell]
        lines.append(f"{cell}: served {m['served']} Y naive {m['Y_naive']:.4f} disp {m['Y_disp']:.4f} E disp {m['E_disp']:.4f} "
                     f"Vis {m['Vis_naive']:.4f}->{m['Vis_disp']:.4f} ext {m['ext_mean_s']:.1f}s late300 {m['late300_naive']:.4f} "
                     f"ledger total {m['ledger_total_s']:.0f}s dispatch {m['ledger_dispatch_s']:.0f} anticipated {m['ledger_anticipated_s']:.0f} "
                     f"unanticipated {m['ledger_unanticipated_s']:.0f}")
    for group, members in [("all 8", list(rf.SNOW)), ("w14", [c for c in rf.SNOW if c.endswith("w14")]), ("w15", [c for c in rf.SNOW if c.endswith("w15")])]:
        mean = lambda k: statistics.mean(per[c][k] for c in members)  # noqa: E731
        tot = lambda k: sum(per[c][k] for c in members)  # noqa: E731
        removed = (mean("Y_naive") - mean("Y_disp")) / mean("Y_naive") if mean("Y_naive") else 0.0
        unant = tot("ledger_unanticipated_s") / tot("ledger_total_s") if tot("ledger_total_s") else 0.0
        lines.append(f"{group}: Y naive {mean('Y_naive'):.4f} disp {mean('Y_disp'):.4f} (removed {100 * removed:.1f}%); "
                     f"late120 {mean('late120_naive'):.4f} late300 {mean('late300_naive'):.4f}; Vis {mean('Vis_naive'):.4f}->{mean('Vis_disp'):.4f}; "
                     f"ext {mean('ext_mean_s'):.1f}s; unanticipated share of late seconds {100 * unant:.1f}% "
                     f"(dispatch {tot('ledger_dispatch_s'):.0f}s, anticipated {tot('ledger_anticipated_s'):.0f}s, unanticipated {tot('ledger_unanticipated_s'):.0f}s)")
        if group == "all 8":
            ps1 = 0.03 <= mean("Y_naive") <= 0.15 and removed <= 0.15 and unant >= 0.90
            lines.append(f"PS1 {'HELD' if ps1 else 'NOT HELD'} (naive late60 in [3%,15%]: {0.03 <= mean('Y_naive') <= 0.15}; "
                         f"removed <= 15%: {removed <= 0.15}; unanticipated >= 90%: {unant >= 0.90})")
    text = "\n".join(lines) + "\n"
    with open(f"{prefix}-report.txt", "x", encoding="utf-8", newline="\n") as handle:
        handle.write(text)
    sys.stdout.write(text)
    return 0


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit(__doc__)
    raise SystemExit(main(sys.argv[1]))
