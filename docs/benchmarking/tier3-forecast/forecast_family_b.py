"""Family B of FORECAST-PLAN.md: closed loop (forecast inside the Runner via world wrapper v1.3, K = 2).

Pairs each forecast run (C-30F, Mplus-30F) with the reused naive T3.8 run of the same arm and cell (valid only after
gates E1/E2 passed). Metrics per run come from the unchanged sweep_analyze.job_row (as t38_analyze uses them):
  forced   = M5_nonNormalShare; own-break (Mplus) = M3_shareA_gt_x, i.e. A > own first promise + 30 s;
  late60   = M3_shareA_gt_60s (A > own first promise + 60 s); visible60 = M2_E2; served = M1_completed;
plus common-rider late60 (riders served in both runs), later/earlier drop revision seconds, and action agreement.
Inference: stratified day-window cluster bootstrap (strata w07, w08), B = 10000, random.Random(20261007),
97.5% percentile interval = sorted indices 124 / 9875.
Chain M (Mplus-30F - Mplus-30, forced share; own-break the same way):
  ARTEFACT  iff relative reduction >= 50% and it decreases in 6/6 day-windows and UB97.5 < 0;
  PARTIAL   iff relative reduction >= 25% and UB97.5 < 0 (not ARTEFACT);
  INCONCLUSIVE iff relative reduction >= 25% and UB97.5 >= 0;  PERSISTS iff relative reduction < 25%.
Chain C: B-C1 passes iff LB97.5[served(C-30F) - served(C-30)] >= -2 riders per cell.
Deployment rule: integrate into the Runner only if B-C1 passes AND visible60(C-30F) <= Vis(C-30 display) + 1 pp AND
late60(C-30F, own first promise) <= Y(C-30 display) + 1 pp (points over the same cells; display values from the
Family A cells file); otherwise recommend the display layer.
Usage: python -B forecast_family_b.py <family-a-cells.tsv> <out-prefix> [dryrun]
"""
from __future__ import annotations

import csv
import pathlib
import random
import statistics
import sys

HERE = pathlib.Path(__file__).resolve().parent
TANG3 = HERE.parent / "tang3"
for sub in ("sweep", "pilot", "evidence", "w07", "t38"):
    sys.path.insert(0, str(TANG3 / sub))
sys.path.insert(0, str(HERE))
import audit_vep  # noqa: E402
import run_t38  # noqa: E402
import run_w07  # noqa: E402
import sweep_analyze  # noqa: E402
import t38_calibrate  # noqa: E402
from pilot_analyze import fleet_decisions  # noqa: E402

FC_ROOT = pathlib.Path(r"C:/RideBoundData/research/tier3-forecast-v1")
DEV_ROOT = pathlib.Path(r"C:/RideBoundData/research/tier3-forecast-checks-v1")
CELLS = tuple(f"d201811{d}-s10-r{r}-{w}" for w in ("w07", "w08") for d, rs in (("14", (3, 4)), ("15", (1, 2, 4)), ("16", (1, 2, 4))) for r in rs)
B = 10_000
SEED = 20261007


def row(root: pathlib.Path, name: str, arm: str, cell: str) -> dict:
    sweep_analyze.OUT_ROOT, sweep_analyze.effective = root, (lambda n: n)
    sweep_analyze.state_of = lambda n: "status-pass" if (root / n / "summary.json").exists() else "missing"
    r = sweep_analyze.job_row(name, arm, "W5", cell)
    if r.get("audit") != "ok":
        raise SystemExit(f"{root / name}: audit {r.get('audit')}")
    return r


def rider_late(root: pathlib.Path, name: str) -> tuple[dict, float, float]:
    transcript = root / name / "transcript-00.ndjson"
    table, _ = audit_vep.audit(transcript)
    _, alighted = sweep_analyze.event_times(transcript)
    first, later, earlier = {}, 0, 0
    for request, coordinate, _, kind, _, _, _, p, _, _, z, _ in table:
        if coordinate != "drop":
            continue
        if kind == "initialPromise":
            first[request] = p
        elif z > 0:
            later += z
        else:
            earlier -= z
    return {r: alighted[r] - first[r] for r in first if r in alighted}, later / 1000, earlier / 1000


def boot(diffs: dict[str, float], lo_idx=124, hi_idx=9875):
    strata: dict[str, dict[str, list[float]]] = {}
    for cell, d in diffs.items():
        strata.setdefault(cell[-3:], {}).setdefault(cell[:9], []).append(d)
    total = len(diffs)
    rng = random.Random(SEED)
    means = []
    for _ in range(B):
        value = 0.0
        for w in sorted(strata):
            clusters = [strata[w][k] for k in sorted(strata[w])]
            n_cells = sum(len(c) for c in clusters)
            pooled = [x for c in (clusters[rng.randrange(len(clusters))] for _ in clusters) for x in c]
            value += (n_cells / total) * statistics.mean(pooled)
        means.append(value)
    means.sort()
    return sum(diffs.values()) / total, means[lo_idx], means[hi_idx]


def classify(naive: dict, fc: dict, cells) -> tuple[str, float, tuple]:
    mn, mf = statistics.mean(naive[c] for c in cells), statistics.mean(fc[c] for c in cells)
    rel = (mn - mf) / mn if mn else 0.0
    point, lo, hi = boot({c: fc[c] - naive[c] for c in cells})
    dws = sorted({c[:9] + c[-4:] for c in cells})
    dec = sum(1 for dw in dws if statistics.mean(fc[c] - naive[c] for c in cells if c[:9] + c[-4:] == dw) < 0)
    if rel >= 0.5 and dec == len(dws) and hi < 0:
        verdict = "ARTEFACT"
    elif rel >= 0.25 and hi < 0:
        verdict = "PARTIAL"
    elif rel >= 0.25:
        verdict = "INCONCLUSIVE"
    else:
        verdict = "PERSISTS"
    return verdict, rel, (point, lo, hi, dec, len(dws))


def main(family_a_cells: str, prefix: str, dryrun: bool) -> int:
    disp = {}
    with open(family_a_cells, encoding="utf-8") as handle:
        for r in csv.DictReader(handle, delimiter="\t"):
            if r["arm"] == "C-30":
                disp[r["cell"]] = (float(r["Y_disp"]), float(r["Vis_disp"]))
    if dryrun:   # development: C-30F = the v1.3 K=2 smoke run on 12/11 r1 w07; naive = task B; no Mplus-30F exists
        pairs = {"C-30": [("d20181112-s10-r1-w07", (run_w07.OUT_ROOT, "C-30-d20181112-s10-r1-w07"), (DEV_ROOT, "smoke-k2"))]}
    else:
        pairs = {arm: [(c, (run_t38.OUT_ROOT, t38_calibrate.effective(f"{arm}-{c}")), (FC_ROOT, f"{arm}F-{c}")) for c in CELLS]
                 for arm in ("C-30", "Mplus-30")}
    out = [f"# forecast_family_b.py {'DRYRUN (development)' if dryrun else 'TEST'}"]
    say = out.append
    metrics = {}
    for arm, items in pairs.items():
        for cell, (nroot, nname), (froot, fname) in items:
            rn, rf = row(nroot, nname, arm, cell), row(froot, fname, arm, cell)
            ln, later_n, earlier_n = rider_late(nroot, nname)
            lf, later_f, earlier_f = rider_late(froot, fname)
            common = sorted(set(ln) & set(lf))
            same_actions = [d for _, d in fleet_decisions(nroot / nname / "transcript-00.ndjson")] == \
                           [d for _, d in fleet_decisions(froot / fname / "transcript-00.ndjson")]
            metrics[(arm, cell)] = {
                "served_n": rn["M1_completed"], "served_f": rf["M1_completed"],
                "forced_n": float(rn["M5_nonNormalShare"] or 0), "forced_f": float(rf["M5_nonNormalShare"] or 0),
                "own_n": float(rn["M3_shareA_gt_x"] or 0), "own_f": float(rf["M3_shareA_gt_x"] or 0),
                "late60_n": float(rn["M3_shareA_gt_60s"] or 0), "late60_f": float(rf["M3_shareA_gt_60s"] or 0),
                "vis_n": float(rn["M2_E2"] or 0), "vis_f": float(rf["M2_E2"] or 0),
                "common_late60_n": sum(1 for r in common if ln[r] > 60_000) / len(common) if common else 0.0,
                "common_late60_f": sum(1 for r in common if lf[r] > 60_000) / len(common) if common else 0.0,
                "later_n": later_n, "later_f": later_f, "earlier_n": earlier_n, "earlier_f": earlier_f,
                "same_actions": same_actions, "nonnormal_f": float(rf["M5_nonNormalShare"] or 0) > 0 and arm == "C-30",
            }
            m = metrics[(arm, cell)]
            say(f"{arm:<9} {cell}: served {m['served_n']}->{m['served_f']} forced {m['forced_n']:.3f}->{m['forced_f']:.3f} "
                f"own-break {m['own_n']:.3f}->{m['own_f']:.3f} late60 {m['late60_n']:.3f}->{m['late60_f']:.3f} "
                f"vis60 {m['vis_n']:.3f}->{m['vis_f']:.3f} common-late60 {m['common_late60_n']:.3f}->{m['common_late60_f']:.3f} "
                f"later/earlier s {m['later_n']:.0f}/{m['earlier_n']:.0f}->{m['later_f']:.0f}/{m['earlier_f']:.0f} same actions {same_actions}")
    if not dryrun:
        cells = list(CELLS)
        mp = {c: metrics[("Mplus-30", c)] for c in cells}
        cp = {c: metrics[("C-30", c)] for c in cells}
        for label, key in (("forced share", "forced"), ("own-break", "own")):
            verdict, rel, (point, lo, hi, dec, ndw) = classify({c: mp[c][f"{key}_n"] for c in cells}, {c: mp[c][f"{key}_f"] for c in cells}, cells)
            say(f"Chain M {label}: {verdict}; relative reduction {100 * rel:.1f}%; diff {100 * point:+.2f} pp "
                f"[{100 * lo:+.2f}, {100 * hi:+.2f}] (97.5%); decreases in {dec}/{ndw} day-windows")
        point, lo, hi = boot({c: cp[c]["served_f"] - cp[c]["served_n"] for c in cells})
        bc1 = lo >= -2
        say(f"Chain C B-C1: served C-30F - C-30 {point:+.3f} [{lo:+.3f}, {hi:+.3f}] per cell -> {'PASS' if bc1 else 'FAIL'}")
        vis_f = statistics.mean(cp[c]["vis_f"] for c in cells)
        late_f = statistics.mean(cp[c]["late60_f"] for c in cells)
        vis_d = statistics.mean(disp[c][1] for c in cells)
        y_d = statistics.mean(disp[c][0] for c in cells)
        deploy = bc1 and vis_f <= vis_d + 0.01 and late_f <= y_d + 0.01
        say(f"Deployment rule: visible60 C-30F {vis_f:.4f} vs display {vis_d:.4f}; late60 C-30F {late_f:.4f} vs display {y_d:.4f} "
            f"-> {'integrate into the Runner' if deploy else 'recommend the display layer'}")
        say(f"C-30F non-normal decisions: {sum(1 for c in cells if cp[c]['nonnormal_f'])} cells")
        point, lo, hi = boot({c: cp[c]["served_f"] - mp[c]["served_f"] for c in cells}, 249, 9750)
        say(f"H2a under forecast: served C-30F - Mplus-30F {point:+.3f} [{lo:+.3f}, {hi:+.3f}] (95%); "
            f"same actions C-30F/Mplus-30F not computed here")
        for w in ("w07", "w08"):
            ws = [c for c in cells if c.endswith(w)]
            say(f"  {w}: Mplus forced {statistics.mean(mp[c]['forced_n'] for c in ws):.3f}->{statistics.mean(mp[c]['forced_f'] for c in ws):.3f}; "
                f"own-break {statistics.mean(mp[c]['own_n'] for c in ws):.3f}->{statistics.mean(mp[c]['own_f'] for c in ws):.3f}; "
                f"C-30 late60 {statistics.mean(cp[c]['late60_n'] for c in ws):.4f}->{statistics.mean(cp[c]['late60_f'] for c in ws):.4f}; "
                f"C-30 vis60 {statistics.mean(cp[c]['vis_n'] for c in ws):.4f}->{statistics.mean(cp[c]['vis_f'] for c in ws):.4f}; "
                f"served C {sum(cp[c]['served_n'] for c in ws)}->{sum(cp[c]['served_f'] for c in ws)}")
    text = "\n".join(out) + "\n"
    with open(f"{prefix}-report.txt", "x", encoding="utf-8", newline="\n") as handle:
        handle.write(text)
    sys.stdout.write(text)
    return 0


if __name__ == "__main__":
    if len(sys.argv) not in (3, 4) or (len(sys.argv) == 4 and sys.argv[3] != "dryrun"):
        raise SystemExit(__doc__)
    raise SystemExit(main(sys.argv[1], sys.argv[2], len(sys.argv) == 4))
