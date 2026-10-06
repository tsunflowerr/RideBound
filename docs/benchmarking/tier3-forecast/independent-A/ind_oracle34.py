"""Independent verifier, Family A -- supplementary: oracle check over the 13 weekend T3.8 test cells.

Purpose: the brief's oracle claim (2305/2305) exceeds the 1471 served C-30 riders of the 21-cell
population, so test whether it refers to the full 34-cell T3.8 test set (21 + these 13 cells).
The oracle uses only the cell day's own factors (no forecast profile), so it is defined on weekend
days too. Reuses my own extraction and stretch code (ind_extract.py, ind_analyze.py).
"""
import json
import os
import sys
from fractions import Fraction
from multiprocessing import Pool

import ind_analyze as ia
import ind_extract as ie

WEEKEND = ["d20181117-s10-r3-w07", "d20181117-s10-r1-w08", "d20181117-s10-r2-w08", "d20181117-s10-r3-w08",
           "d20181117-s10-r1-w17", "d20181117-s10-r2-w17", "d20181117-s10-r3-w17", "d20181118-s10-r2-w08",
           "d20181118-s10-r3-w08", "d20181118-s10-r4-w08", "d20181118-s10-r2-w17", "d20181118-s10-r3-w17",
           "d20181118-s10-r4-w17"]


def oracle_rows(data):
    cell = data["cell"]
    day, w = ia.cell_day(cell), ia.cell_window(cell)
    ftrue = ia.own_f(day, ia.WSTART[w], True)
    out = []
    for r in data["riders"]:
        if r["A"] is None:
            continue
        ents = r["entries"]
        t0, P0, A = ents[0]["t"], ents[0]["p"], r["A"]
        dispatch = sum(e["p"] - e["e"] for e in ents[1:])
        S = ia.stretched(t0, P0 - t0, ftrue, True) if P0 - t0 > 0 else Fraction(t0)
        out.append((dispatch, abs(A - S), A - P0))
    return out


def main():
    os.makedirs(ie.OUT, exist_ok=True)
    jobs = [("C-30", c) for c in WEEKEND]
    with Pool(6) as pool:
        for res in pool.imap_unordered(ie.extract, jobs):
            ie.write(res, f"{res['arm']}-{res['cell']}.json")
            print(res["arm"], res["cell"], "rerun" if res["usedRerun"] else "", "badSha", res["badSha"],
                  "cp", res["finalCheckpoint"], "riders", len(res.get("riders", [])), flush=True)
    tot = {"21": [0, 0, 0, Fraction(0)], "13": [0, 0, 0, Fraction(0)]}
    for label, cells in (("21", ia.CELLS), ("13", WEEKEND)):
        for c in cells:
            data = ia.load("C-30", c)
            assert data["finalCheckpoint"]
            for dispatch, dev, _late in oracle_rows(data):
                tot[label][0] += 1
                if dispatch == 0:
                    tot[label][1] += 1
                    if dev <= 2000:
                        tot[label][2] += 1
                    tot[label][3] = max(tot[label][3], dev)
    lines = []
    for label in ("21", "13"):
        n, z, ok, mx = tot[label]
        lines.append(f"{label} cells: served {n}, dispatch==0 {z}, |A-S_true|<=2s {ok}/{z}, max dev {float(mx)/1000:.3f} s")
    n = tot["21"][0] + tot["13"][0]
    z = tot["21"][1] + tot["13"][1]
    ok = tot["21"][2] + tot["13"][2]
    lines.append(f"34 cells: served {n}, dispatch==0 {z}, |A-S_true|<=2s {ok}/{z}")
    print("\n".join(lines))
    with open(os.path.join(ia.HERE, "ind_oracle34_output.txt"), "w", encoding="utf-8") as fh:
        fh.write("\n".join(lines) + "\n")


if __name__ == "__main__":
    sys.exit(main())
