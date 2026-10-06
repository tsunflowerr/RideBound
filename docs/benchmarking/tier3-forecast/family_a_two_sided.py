"""FORECAST-PLAN.md §5 sensitivity: two-sided display (no one-sided floor, D = S(t, p - t) even when shorter), C-30,
causal-no15 sources, the 21 active weekday test cells. Descriptive only. Output mode x.
Usage: python -B family_a_two_sided.py <out-prefix>
"""
from __future__ import annotations

import pathlib
import statistics
import sys

HERE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import display_layer as dl  # noqa: E402
import forecast_family_a as fa  # noqa: E402


def main(prefix: str) -> int:
    if not fa.SEAL.exists():
        raise SystemExit("refused: plan not sealed")
    cells = [c for c in fa.run_t38.PHASES["test"][1] if fa.day_of(c) in dl.WEEKDAYS and dl.sources(fa.day_of(c), "causal-no15")]
    lines = [f"# family_a_two_sided.py (sensitivity; two-sided display); C-30; {len(cells)} active weekday cells"]
    per = {}
    for c in cells:
        prof = dl.profile(fa.day_of(c), c[-3:], "causal-no15")
        per[c] = fa.cell_metrics(fa.riders(fa.run_dir("test", "C-30", c), c, prof, floor=False), None)
    for group, members in [("active weekday", cells)] + [(w, [c for c in cells if c.endswith(w)]) for w in ("w07", "w08", "w17")]:
        mean = lambda k: statistics.mean(per[c][k] for c in members)  # noqa: E731
        lines.append(f"{group:<15} n={len(members):>2} | Y naive {mean('Y_naive'):.4f} two-sided {mean('Y_disp'):.4f} | "
                     f"E naive {mean('E_naive'):.4f} two-sided {mean('E_disp'):.4f} | Vis naive {mean('Vis_naive'):.4f} "
                     f"two-sided {mean('Vis_disp'):.4f} | ext {mean('ext_mean_s'):+.1f}s")
    text = "\n".join(lines) + "\n"
    with open(f"{prefix}-report.txt", "x", encoding="utf-8", newline="\n") as handle:
        handle.write(text)
    sys.stdout.write(text)
    return 0


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit(__doc__)
    raise SystemExit(main(sys.argv[1]))
