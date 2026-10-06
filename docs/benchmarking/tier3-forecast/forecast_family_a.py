"""Family A of FORECAST-PLAN.md: zero-run promise-layer ("display") replay, pads, ledger, cluster inference.

Read-only on the runs; every output file is created with mode "x".
  population dev  : task B C-30/Mplus-30/U runs on 12-13/11 w07 (tier3-w07-v1); rule must be loo-no15 (causal is
                    inactive there). Allowed before the seal.
  population test : the 34 T3.8 test cells x U, C-30, Mplus-30, Mplus-60, V-30 (tier3-t38-v1, reruns via
                    t38_calibrate.effective). REFUSED unless FORECAST-PLAN.sha256 exists (sealed and pushed first).
Per served rider (drop coordinate; pickup for the price only):
  t0, P0 = publishedAtMs and p of the initialPromise; A = first passengerAlighted simTime;
  D_i = display(t_i, p_i, profile) for every non-closed revision; D0 = first displayed promise;
  dispatch = sum over drop revisions of (p - e); oracle S_true = stretch of P0 - t0 along the cell day's true factors.
Cell metrics: Y = share A - D0 > 60 s; E = share A - D0 < -60 s; Vis = share sum|D_i - D_{i-1}| > 60 s (promised
riders); naive versions use P0 and sum |z|; extension D0 - P0; late120/300; MAE; pickup extension; pickups shown after
latestPickupMs; ledger for riders late vs P0.
Pads (A3): k_w = sum(D0 - P0) / sum(P0 - t0) over all weekday C-30 test riders of window w, computed from promises
only and written to a file BEFORE any alight time is read; pad promise = P0 + k_w (P0 - t0) (revisions shift by the
same constant, so pad churn = naive churn).
Inference: stratified day-window cluster bootstrap (strata = windows, clusters = day-windows, weights = cell counts),
B = 10000, random.Random(20261007), percentile indices 249 / 9750; stratified cluster-t (df = clusters - strata);
leave-one-day-out; per-day-window sign counts.
Usage: python -B forecast_family_a.py <dev|test> <rule> <out-prefix>
"""
from __future__ import annotations

import json
import math
import pathlib
import random
import statistics
import sys

HERE = pathlib.Path(__file__).resolve().parent
TANG3 = HERE.parent / "tang3"
for sub in ("sweep", "evidence", "w07", "t38"):
    sys.path.insert(0, str(TANG3 / sub))
sys.path.insert(0, str(HERE))
import audit_vep  # noqa: E402
import display_layer as dl  # noqa: E402
import promise_stretch as ps  # noqa: E402
import run_t38  # noqa: E402
import run_w07  # noqa: E402
import sweep_analyze  # noqa: E402
import t38_calibrate  # noqa: E402

SEAL = HERE / "FORECAST-PLAN.sha256"
ARMS_TEST = ("U", "C-30", "Mplus-30", "Mplus-60", "V-30")
ARMS_DEV = ("U", "C-30", "Mplus-30")
B = 10_000
SEED = 20261007
T_975_DF = {1: 12.706, 2: 4.303, 3: 3.182, 4: 2.776, 5: 2.571, 6: 2.447, 7: 2.365, 8: 2.306}


def day_of(cell: str) -> str:
    return f"2018-11-{cell[7:9]}"


def run_dir(population: str, arm: str, cell: str) -> pathlib.Path:
    if population == "dev":
        return run_w07.OUT_ROOT / f"{arm}-{cell}"
    return run_t38.OUT_ROOT / t38_calibrate.effective(f"{arm}-{cell}")


def riders(run: pathlib.Path, cell: str, prof, floor: bool = True) -> list[dict]:
    """Per served rider records; reads the ledger, requests and alight events."""
    transcript = run / "transcript-00.ndjson"
    _, _, state = audit_vep.load(transcript)
    table, _ = audit_vep.audit(transcript)
    _, alighted = sweep_analyze.event_times(transcript)
    latest = {r["requestId"]: r["latestPickupMs"] for r in state["requests"]}
    window = cell[-3:]
    day = day_of(cell)
    show = (lambda t, p: dl.display(t, p, prof)) if floor else (lambda t, p: two_sided(t, p, prof))
    seqs: dict[tuple[str, str], list] = {}
    for request, coordinate, seq, kind, published, v, e, p, x, c, z, closed in table:
        seqs.setdefault((request, coordinate), []).append((seq, kind, published, v, e, p, z, closed))
    out = []
    for (request, coordinate), rows in seqs.items():
        if coordinate != "drop" or request not in alighted:
            continue
        rows.sort()
        first = rows[0]
        if first[1] != "initialPromise":
            raise SystemExit(f"{run}: {request} first drop entry is {first[1]}")
        t0, p0 = first[2], first[5]
        open_rows = [r for r in rows if not r[7]]
        shown = [show(r[2], r[5]) for r in open_rows]
        pick = sorted(seqs.get((request, "pickup"), []))
        tp, pp = (pick[0][2], pick[0][5]) if pick else (None, None)
        dp = show(tp, pp) if pick else None
        a = alighted[request]
        dispatch = sum(r[5] - r[4] for r in rows[1:])
        out.append({
            "request": request, "t0": t0, "P0": p0, "D0": shown[0], "A": a,
            "vis_naive": sum(abs(r[6]) for r in open_rows[1:]),
            "vis_disp": sum(abs(shown[i] - shown[i - 1]) for i in range(1, len(shown))),
            "dispatch": dispatch,
            "oracle": ps.stretch(t0, p0 - t0, ps.true_weight(day, dl.WINDOW_STARTS[window], t0)),
            "pick_ext": (dp - pp) if pick else None,
            "pick_after_window": bool(pick) and dp > latest[request],
        })
    return out


def two_sided(t, p, prof):
    if prof is None or p <= t:
        return float(p)
    return ps.stretch(t, p - t, ps.forecast_weight(prof, t))


def cell_metrics(rs: list[dict], pad_k: float | None) -> dict:
    n = len(rs)
    share = lambda f: sum(1 for r in rs if f(r)) / n if n else 0.0  # noqa: E731
    ext = sorted(r["D0"] - r["P0"] for r in rs)
    m = {
        "served": n,
        "Y_naive": share(lambda r: r["A"] - r["P0"] > 60_000), "Y_disp": share(lambda r: r["A"] - r["D0"] > 60_000),
        "E_naive": share(lambda r: r["A"] - r["P0"] < -60_000), "E_disp": share(lambda r: r["A"] - r["D0"] < -60_000),
        "Vis_naive": share(lambda r: r["vis_naive"] > 60_000), "Vis_disp": share(lambda r: r["vis_disp"] > 60_000),
        "Vis120_naive": share(lambda r: r["vis_naive"] > 120_000), "Vis120_disp": share(lambda r: r["vis_disp"] > 120_000),
        "late120_naive": share(lambda r: r["A"] - r["P0"] > 120_000), "late120_disp": share(lambda r: r["A"] - r["D0"] > 120_000),
        "late300_naive": share(lambda r: r["A"] - r["P0"] > 300_000), "late300_disp": share(lambda r: r["A"] - r["D0"] > 300_000),
        "late30_naive": share(lambda r: r["A"] - r["P0"] > 30_000), "late30_disp": share(lambda r: r["A"] - r["D0"] > 30_000),
        "MAE_naive_s": statistics.mean(abs(r["A"] - r["P0"]) for r in rs) / 1000 if n else 0.0,
        "MAE_disp_s": statistics.mean(abs(r["A"] - r["D0"]) for r in rs) / 1000 if n else 0.0,
        "bias_naive_s": statistics.mean(r["A"] - r["P0"] for r in rs) / 1000 if n else 0.0,
        "bias_disp_s": statistics.mean(r["A"] - r["D0"] for r in rs) / 1000 if n else 0.0,
        "ext_mean_s": statistics.mean(ext) / 1000 if n else 0.0,
        "ext_p90_s": ext[max(0, -(-9 * n // 10) - 1)] / 1000 if n else 0.0,
        "pick_ext_mean_s": statistics.mean(r["pick_ext"] for r in rs if r["pick_ext"] is not None) / 1000 if n else 0.0,
        "pick_after_window": sum(1 for r in rs if r["pick_after_window"]),
        "oracle_ok": sum(1 for r in rs if r["dispatch"] == 0 and abs(r["A"] - r["oracle"]) <= 2_000),
        "oracle_n": sum(1 for r in rs if r["dispatch"] == 0),
    }
    late = [r for r in rs if r["A"] - r["P0"] > 60_000]
    m["ledger_dispatch_s"] = sum(r["dispatch"] for r in late) / 1000
    m["ledger_anticipated_s"] = sum(r["D0"] - r["P0"] for r in late) / 1000
    m["ledger_unanticipated_s"] = sum(r["A"] - r["D0"] - r["dispatch"] for r in late) / 1000
    m["ledger_total_s"] = sum(r["A"] - r["P0"] for r in late) / 1000
    if pad_k is not None:
        m["Y_pad"] = share(lambda r: r["A"] - (r["P0"] + pad_k * (r["P0"] - r["t0"])) > 60_000)
        m["E_pad"] = share(lambda r: r["A"] - (r["P0"] + pad_k * (r["P0"] - r["t0"])) < -60_000)
    return m


def strat_boot(diffs: dict[str, float]) -> tuple[float, float, float]:
    """diffs: cell -> paired difference. Strata = windows, clusters = day-windows. Returns (point, lo, hi)."""
    strata: dict[str, dict[str, list[float]]] = {}
    for cell, d in diffs.items():
        strata.setdefault(cell[-3:], {}).setdefault(cell[:9] + cell[-4:], []).append(d)
    total = len(diffs)
    point = sum(diffs.values()) / total
    rng = random.Random(SEED)
    means = []
    keys = sorted(strata)
    for _ in range(B):
        value = 0.0
        for w in keys:
            clusters = [strata[w][k] for k in sorted(strata[w])]
            n_cells = sum(len(c) for c in clusters)
            draw = [clusters[rng.randrange(len(clusters))] for _ in clusters]
            pooled = [x for c in draw for x in c]
            value += (n_cells / total) * (sum(pooled) / len(pooled))
        means.append(value)
    means.sort()
    return point, means[249], means[9750]


def cluster_t(diffs: dict[str, float]) -> tuple[float, float, float, int]:
    strata: dict[str, dict[str, list[float]]] = {}
    for cell, d in diffs.items():
        strata.setdefault(cell[-3:], {}).setdefault(cell[:9] + cell[-4:], []).append(d)
    total = len(diffs)
    point, var, n_clusters = 0.0, 0.0, 0
    for w, cl in strata.items():
        cells = [x for c in cl.values() for x in c]
        mean = sum(cells) / len(cells)
        weight = len(cells) / total
        point += weight * mean
        g = len(cl)
        n_clusters += g
        if g > 1:
            totals = [sum(x - mean for x in c) for c in cl.values()]
            var += weight ** 2 * (g / (g - 1)) * sum(t * t for t in totals) / len(cells) ** 2
    df = n_clusters - len(strata)
    half = T_975_DF.get(df, 1.96) * math.sqrt(var)
    return point, point - half, point + half, df


def main(population: str, rule: str, prefix: str) -> int:
    if population == "test" and not SEAL.exists():
        raise SystemExit("test population refused: FORECAST-PLAN.sha256 does not exist (seal and push the plan first)")
    if population == "dev":
        cells, arms = list(run_w07.CELLS), ARMS_DEV
    else:
        cells, arms = list(run_t38.PHASES["test"][1]), ARMS_TEST
    weekday = [c for c in cells if day_of(c) in dl.WEEKDAYS and (population == "dev" or dl.sources(day_of(c), rule))]
    profiles = {c: dl.profile(day_of(c), c[-3:], rule) for c in cells}
    out = [f"# forecast_family_a.py population={population} rule={rule}; cells {len(cells)}; active weekday cells {len(weekday)}"]
    # 1. promises only: pads per window (no alight time used)
    data = {}
    for arm in arms:
        for c in cells:
            data[(arm, c)] = riders(run_dir(population, arm, c), c, profiles[c])
    promises = {c: [{key: r[key] for key in ("t0", "P0", "D0")} for r in data[("C-30", c)]] for c in weekday}
    assert all("A" not in r for rs in promises.values() for r in rs)          # pads see promises only
    k = {}
    for w in sorted({c[-3:] for c in weekday}):
        num = sum(r["D0"] - r["P0"] for c in weekday if c.endswith(w) for r in promises[c])
        den = sum(r["P0"] - r["t0"] for c in weekday if c.endswith(w) for r in promises[c])
        k[w] = num / den if den else 0.0
    citywide = (sum(r["D0"] - r["P0"] for c in weekday for r in promises[c])
                / sum(r["P0"] - r["t0"] for c in weekday for r in promises[c]))
    with open(f"{prefix}-pads.json", "x", encoding="utf-8", newline="\n") as handle:
        json.dump({"k_per_window": k, "k_citywide": citywide, "note": "computed from promises only (D0, P0, t0)"}, handle, indent=1)
    out.append(f"pads (promises only): per window {k}; citywide {citywide:.6f}")
    # 2. outcomes
    cm = {(arm, c): cell_metrics(data[(arm, c)], k.get(c[-3:], 0.0)) for arm in arms for c in cells}
    cmc = {(arm, c): cell_metrics(data[(arm, c)], citywide) for arm in ("C-30",) for c in cells}
    say = out.append
    for arm in arms:
        for group, members in [("active weekday", weekday)] + [(w, [c for c in weekday if c.endswith(w)]) for w in ("w07", "w08", "w17")] + [("all cells", cells)]:
            if not members:
                continue
            mean = lambda key: statistics.mean(cm[(arm, c)][key] for c in members)  # noqa: E731
            say(f"{arm:<9} {group:<15} n={len(members):>2} | Y naive {mean('Y_naive'):.4f} disp {mean('Y_disp'):.4f} pad {mean('Y_pad'):.4f}"
                f" | E naive {mean('E_naive'):.4f} disp {mean('E_disp'):.4f} | Vis naive {mean('Vis_naive'):.4f} disp {mean('Vis_disp'):.4f}"
                f" | ext {mean('ext_mean_s'):.1f}s p90 {mean('ext_p90_s'):.1f}s pick {mean('pick_ext_mean_s'):.1f}s"
                f" | MAE {mean('MAE_naive_s'):.1f}->{mean('MAE_disp_s'):.1f}s bias {mean('bias_naive_s'):+.1f}->{mean('bias_disp_s'):+.1f}s"
                f" | late120 {mean('late120_naive'):.4f}->{mean('late120_disp'):.4f} late300 {mean('late300_naive'):.4f}->{mean('late300_disp'):.4f}")
    oracle_ok = sum(cm[("C-30", c)]["oracle_ok"] for c in cells)
    oracle_n = sum(cm[("C-30", c)]["oracle_n"] for c in cells)
    say(f"oracle (C-30, dispatch part 0): |A - true stretch| <= 2 s for {oracle_ok}/{oracle_n}")
    say(f"pickups displayed after latestPickup (C-30, all cells): {sum(cm[('C-30', c)]['pick_after_window'] for c in cells)}")
    led = {key: sum(cm[("C-30", c)][key] for c in weekday) for key in ("ledger_dispatch_s", "ledger_anticipated_s", "ledger_unanticipated_s", "ledger_total_s")}
    say(f"ledger C-30 active weekday, riders late > 60 s vs naive: total {led['ledger_total_s']:.1f}s = dispatch {led['ledger_dispatch_s']:.1f}"
        f" + anticipated {led['ledger_anticipated_s']:.1f} + unanticipated {led['ledger_unanticipated_s']:.1f}")
    # 3. inference (C-30, active weekday cells)
    if population == "test" or len({c[-3:] for c in weekday}) >= 1:
        diffs = {name: {c: f(c) for c in weekday} for name, f in (
            ("dY", lambda c: cm[("C-30", c)]["Y_disp"] - cm[("C-30", c)]["Y_naive"]),
            ("dE", lambda c: cm[("C-30", c)]["E_disp"] - cm[("C-30", c)]["E_naive"]),
            ("dVis", lambda c: cm[("C-30", c)]["Vis_disp"] - cm[("C-30", c)]["Vis_naive"]),
            ("dPad", lambda c: cm[("C-30", c)]["Y_disp"] - cm[("C-30", c)]["Y_pad"]),
            ("dPadCity", lambda c: cmc[("C-30", c)]["Y_disp"] - cmc[("C-30", c)]["Y_pad"]),
        )}
        say("\ninference on C-30 active weekday cells (stratified day-window cluster bootstrap; cluster-t)")
        res = {}
        for name, d in diffs.items():
            point, lo, hi = strat_boot(d)
            tp, tlo, thi, df = cluster_t(d)
            res[name] = (point, lo, hi, tlo, thi)
            say(f"  {name:<9} point {100 * point:+.3f} pp  boot95 [{100 * lo:+.3f}, {100 * hi:+.3f}]  cluster-t(df={df}) [{100 * tlo:+.3f}, {100 * thi:+.3f}]")
        w07dw = sorted({c[:9] + c[-4:] for c in weekday if c.endswith("w07")})
        signs = {dw: statistics.mean(diffs["dY"][c] for c in weekday if c[:9] + c[-4:] == dw) for dw in w07dw}
        say(f"  dY per weekday-w07 day-window: { {k2: round(100 * v, 3) for k2, v in signs.items()} }")
        lodo = {}
        for d in sorted({day_of(c) for c in weekday}):
            rest = {c: v for c, v in diffs["dY"].items() if day_of(c) != d}
            lodo[d] = sum(rest.values()) / len(rest) if rest else None
        say(f"  leave-one-day-out point dY (pp): { {k2: (None if v is None else round(100 * v, 3)) for k2, v in lodo.items()} }")
        a1 = (res["dY"][0] <= -0.02 and res["dY"][2] < 0 and all(v < 0 for v in signs.values()) and len(signs) == 3
              and res["dE"][0] <= 0.01)
        a2 = a1 and res["dVis"][2] <= 0.01
        a3 = a2 and res["dPad"][2] < 0
        labels = []
        if any(v is not None and v >= 0 for v in lodo.values()):
            labels.append("driven by one day")
        if res["dY"][4] >= 0:
            labels.append("fragile to small-cluster inference")
        say(f"  A1 {'PASS' if a1 else 'FAIL'}; A2 {'PASS' if a2 else 'FAIL' if a1 else 'not tested'}; "
            f"A3 {'PASS' if a3 else 'FAIL' if a2 else 'not tested'}; labels: {labels or 'none'}")
        deploy = {c: (cm[("C-30", c)]["Y_disp"] - cm[("C-30", c)]["Y_naive"]) if c in weekday else 0.0 for c in cells}
        say(f"  34-cell deployment mean dY (inactive cells = 0): {100 * statistics.mean(deploy.values()):+.3f} pp")
        rngc = random.Random(SEED)
        vals = list(diffs["dY"].values())
        boots = sorted(sum(vals[rngc.randrange(len(vals))] for _ in vals) / len(vals) for _ in range(B))
        say(f"  T3.8-style cell bootstrap dY: [{100 * boots[249]:+.3f}, {100 * boots[9750]:+.3f}] pp")
    with open(f"{prefix}-cells.tsv", "x", encoding="utf-8", newline="\n") as handle:
        keys = sorted(next(iter(cm.values())).keys())
        handle.write("arm\tcell\t" + "\t".join(keys) + "\n")
        for (arm, c), m in cm.items():
            handle.write(f"{arm}\t{c}\t" + "\t".join(str(m[key]) for key in keys) + "\n")
    text = "\n".join(out) + "\n"
    with open(f"{prefix}-report.txt", "x", encoding="utf-8", newline="\n") as handle:
        handle.write(text)
    sys.stdout.write(text)
    return 0


if __name__ == "__main__":
    if len(sys.argv) != 4 or sys.argv[1] not in ("dev", "test"):
        raise SystemExit(__doc__)
    raise SystemExit(main(*sys.argv[1:]))
