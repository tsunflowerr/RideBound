"""Independent verifier, Family A -- step 2: recompute all Family A quantities from extracted data.

Written from the task brief (and METRICS-SPEC.md) only; does not import or read the code under test.
Arithmetic: the stretch is computed EXACTLY with fractions.Fraction (CSV floats converted exactly);
a float re-computation is run alongside and any classification that differs is reported.
"""
import collections
import csv
import json
import math
import os
import random
import sys
from fractions import Fraction

HERE = os.path.dirname(os.path.abspath(__file__))
EXT = os.path.join(HERE, "extracted")
NET = (r"E:\RideBoundData\wp6\extracted\sha256\d9\d9e86f33645e5eec287d387f8d63ad41ddf41d4ef648138b65d636482e2c599e"
       r"\FleetPy_Manhattan\networks\Manhattan_2019_corrected")
BIN_MS = 900000
HOLD_BIN = 8
THR = 60000
WINDOWS = ["w07", "w08", "w17"]
WSTART = {"w07": 25200, "w08": 28800, "w17": 61200}
DAYS_W = {
    "w07": ["d20181114-s10-r3", "d20181114-s10-r4", "d20181115-s10-r1", "d20181115-s10-r2",
            "d20181115-s10-r4", "d20181116-s10-r1", "d20181116-s10-r2", "d20181116-s10-r4"],
    "w17": ["d20181114-s10-r3", "d20181114-s10-r4", "d20181116-s10-r1", "d20181116-s10-r2",
            "d20181116-s10-r4"],
}
DAYS_W["w08"] = list(DAYS_W["w07"])
CELLS = [f"{dr}-{w}" for w in WINDOWS for dr in DAYS_W[w]]
SOURCES = {"2018-11-14": ["2018-11-12", "2018-11-13"],
           "2018-11-15": ["2018-11-12", "2018-11-13", "2018-11-14"],
           "2018-11-16": ["2018-11-12", "2018-11-13", "2018-11-14"]}
SEED = 20261007
B = 10000


def cell_day(cell):
    d = cell.split("-")[0][1:]
    return f"{d[0:4]}-{d[4:6]}-{d[6:8]}"


def cell_window(cell):
    return cell.split("-")[-1]


# ---------------------------------------------------------------- traffic factors
_FCACHE = {}


def day_factors(day):
    if day not in _FCACHE:
        rows = {}
        with open(os.path.join(NET, f"{day}_tt_factors.csv"), newline="", encoding="utf-8") as fh:
            for r in csv.DictReader(fh):
                st = float(r["simulation_time"])
                assert st == int(st)
                assert int(st) not in rows
                rows[int(st)] = r["travel_time_factor"]
        _FCACHE[day] = rows
    return _FCACHE[day]


def abs_factor(day, wstart, b, exact):
    s = day_factors(day)[wstart + min(b, HOLD_BIN) * 900]
    return Fraction(float(s)) if exact else float(s)


def profile_m(day, wstart, exact):
    """m(b) for b = 0..HOLD_BIN (held afterwards): mean over source days of the absolute factor."""
    src = SOURCES[day]
    out = []
    for b in range(HOLD_BIN + 1):
        vals = [abs_factor(sd, wstart, b, exact) for sd in src]
        out.append(sum(vals, Fraction(0) if exact else 0.0) / len(vals))
    return out


def own_f(day, wstart, exact):
    return [abs_factor(day, wstart, b, exact) for b in range(HOLD_BIN + 1)]


# ---------------------------------------------------------------- stretch
def stretched(t, W, prof, exact):
    """S = t + D where integral_t^{t+D} 1/g(bin(tau)) dtau = W; g(b) = prof[b]/prof[b0], g(b0) = 1."""
    assert W > 0
    b0 = t // BIN_MS
    base = prof[min(b0, HOLD_BIN)]
    one = Fraction(1) if exact else 1.0
    R = Fraction(W) if exact else float(W)
    tau = Fraction(t) if exact else float(t)
    b = b0
    while True:
        g = one if b == b0 else prof[min(b, HOLD_BIN)] / base
        end = (b + 1) * BIN_MS
        cap = (end - tau) / g
        if R <= cap:
            return tau + R * g
        R -= cap
        tau = Fraction(end) if exact else float(end)
        b += 1


def displayed(t, p, prof, exact):
    W = p - t
    if W <= 0:          # no remaining time: nothing to stretch, display the published ETA
        return Fraction(p) if exact else float(p)
    S = stretched(t, W, prof, exact)
    return max(Fraction(p) if exact else float(p), S)


# ---------------------------------------------------------------- per cell
def load(arm, cell):
    with open(os.path.join(EXT, f"{arm}-{cell}.json"), encoding="utf-8") as fh:
        return json.load(fh)


def rider_rows(arm, cell, exact, diag):
    data = load(arm, cell)
    assert data["finalCheckpoint"], (arm, cell)
    day, w = cell_day(cell), cell_window(cell)
    prof = profile_m(day, WSTART[w], exact)
    ftrue = own_f(day, WSTART[w], exact)
    diag["badSha"] += data["badSha"]
    diag["dupLedgerIds"] += data["dupLedgerIds"]
    diag["alightedNotInLedger"] += data["alightedNotInLedger"]
    rows = []
    for r in data["riders"]:
        ents = r["entries"]
        diag["promised"] += 1
        assert ents[0]["kind"] == "initialPromise"
        assert all(e["kind"] == "revision" for e in ents[1:])
        for i in range(1, len(ents)):
            if ents[i]["v"] != ents[i - 1]["p"]:
                diag["dropChainBreaks"] += 1
            if ents[i]["p"] == ents[i - 1]["p"]:
                diag["revisionsDropUnchanged"] += 1
            if ents[i]["t"] < ents[i - 1]["t"]:
                diag["revisionsOutOfTimeOrder"] += 1
        for e in ents:
            if e["p"] - e["t"] < 0:
                diag["entriesWneg"] += 1
            elif e["p"] - e["t"] == 0:
                diag["entriesWzero"] += 1
        if r["nAlight"] > 1:
            diag["multiAlight"] += 1
        if r["A"] is None:
            diag["promisedNotServed"] += 1
            if r["lifecycle"] == "completed":
                diag["completedButNoAlight"] += 1
            # promise-only quantities still needed for the 'all promised riders' variant of k_w
            t0, P0 = ents[0]["t"], ents[0]["p"]
            rows.append({"served": False, "t0": t0, "P0": P0, "D0": displayed(t0, P0, prof, exact)})
            continue
        if r["lifecycle"] != "completed":
            diag["alightedButNotCompleted"] += 1
        A = r["A"]
        t0, P0 = ents[0]["t"], ents[0]["p"]
        for e in ents:
            if e["t"] > A:
                diag["entriesAfterAlight"] += 1
        D = [displayed(e["t"], e["p"], prof, exact) for e in ents]
        D0 = D[0]
        dispatch = sum(e["p"] - e["e"] for e in ents[1:])
        nonzero_c = sum(1 for e in ents[1:] if e["p"] != e["e"])
        vis_n = sum(abs(ents[i]["p"] - ents[i - 1]["p"]) for i in range(1, len(ents)))
        vis_d = sum(abs(D[i] - D[i - 1]) for i in range(1, len(D)))
        if P0 - t0 > 0:
            S_true = stretched(t0, P0 - t0, ftrue, exact)
        else:
            S_true = Fraction(t0) if exact else float(t0)
        rows.append({
            "served": True, "t0": t0, "P0": P0, "A": A, "D0": D0, "dispatch": dispatch,
            "nonzeroC": nonzero_c, "visN": vis_n, "visD": vis_d, "Strue": S_true,
        })
    return rows


def share(rows, pred):
    return Fraction(sum(1 for r in rows if pred(r)), len(rows))


def cell_metrics(rows, k_w):
    sv = [r for r in rows if r["served"]]
    m = {
        "nServed": len(sv), "nPromised": len(rows),
        "Y_naive": share(sv, lambda r: r["A"] - r["P0"] > THR),
        "Y_disp": share(sv, lambda r: r["A"] - r["D0"] > THR),
        "E_naive": share(sv, lambda r: r["A"] - r["P0"] < -THR),
        "E_disp": share(sv, lambda r: r["A"] - r["D0"] < -THR),
        "Vis_naive": share(sv, lambda r: r["visN"] > THR),
        "Vis_disp": share(sv, lambda r: r["visD"] > THR),
        "ext_ms": sum((r["D0"] - r["P0"] for r in sv), Fraction(0)) / len(sv),
    }
    if k_w is not None:
        m["Y_pad"] = share(sv, lambda r: r["A"] - (r["P0"] + k_w * (r["P0"] - r["t0"])) > THR)
    return m


# ---------------------------------------------------------------- bootstrap
def strata_clusters(cell_values):
    """{window: [ [values of cells of day 1], [day 2], ... ] } with clusters sorted by day."""
    out = {}
    for w in WINDOWS:
        by_day = collections.OrderedDict()
        for c in sorted((c for c in CELLS if cell_window(c) == w), key=lambda c: (cell_day(c), c)):
            by_day.setdefault(cell_day(c), []).append(cell_values[c])
        out[w] = list(by_day.values())
    return out


def bootstrap(cell_values, rng):
    st = strata_clusters(cell_values)
    n_cells = {w: sum(len(cl) for cl in st[w]) for w in WINDOWS}
    total = sum(n_cells.values())
    assert total == 21
    reps = []
    for _ in range(B):
        val = 0.0
        for w in WINDOWS:
            cls = st[w]
            pooled = []
            for _slot in range(len(cls)):
                pooled.extend(cls[rng.randrange(len(cls))])
            val += (n_cells[w] / total) * (sum(pooled) / len(pooled))
        reps.append(val)
    reps.sort()
    return reps[249], reps[9750]


# ---------------------------------------------------------------- main
def mean(xs):
    xs = list(xs)
    return sum(xs) / len(xs)


def main():
    exact = True
    diag = {"C-30": collections.Counter(), "U": collections.Counter()}
    rows = {arm: {c: rider_rows(arm, c, exact, diag[arm]) for c in CELLS} for arm in ("C-30", "U")}

    # float shadow computation for classification-stability check
    fdiag = {"C-30": collections.Counter(), "U": collections.Counter()}
    frows = {arm: {c: rider_rows(arm, c, False, fdiag[arm]) for c in CELLS} for arm in ("C-30", "U")}

    out = []

    def P(*a):
        s = " ".join(str(x) for x in a)
        print(s)
        out.append(s)

    P("== Diagnostics (population cells) ==")
    for arm in ("C-30", "U"):
        P(arm, dict(sorted(diag[arm].items())))

    # k_w from promises only (C-30). Primary: served riders; variant: all promised riders.
    k = {}
    k_all = {}
    for w in WINDOWS:
        cs = [c for c in CELLS if cell_window(c) == w]
        sv = [r for c in cs for r in rows["C-30"][c] if r["served"]]
        al = [r for c in cs for r in rows["C-30"][c]]
        k[w] = sum((r["D0"] - r["P0"] for r in sv), Fraction(0)) / sum(r["P0"] - r["t0"] for r in sv)
        k_all[w] = sum((r["D0"] - r["P0"] for r in al), Fraction(0)) / sum(r["P0"] - r["t0"] for r in al)
    P("\n== k_w (promises only, C-30 riders of the population cells of window w) ==")
    for w in WINDOWS:
        P(f"k_{w} = {float(k[w]):.7f}   (all promised riders incl. unserved: {float(k_all[w]):.7f})")

    met = {arm: {c: cell_metrics(rows[arm][c], k[cell_window(c)] if arm == "C-30" else None) for c in CELLS}
           for arm in ("C-30", "U")}
    fk = {w: float(k[w]) for w in WINDOWS}
    fmet = {arm: {c: cell_metrics(frows[arm][c], fk[cell_window(c)] if arm == "C-30" else None) for c in CELLS}
            for arm in ("C-30", "U")}
    diffs = []
    for arm in ("C-30", "U"):
        for c in CELLS:
            for key in met[arm][c]:
                if key in ("ext_ms",):
                    continue
                if met[arm][c][key] != fmet[arm][c][key]:
                    diffs.append((arm, c, key, met[arm][c][key], fmet[arm][c][key]))
    P("\nExact(Fraction) vs float classification differences:", len(diffs), diffs[:5])

    # per-cell table
    with open(os.path.join(HERE, "ind_cells_table.tsv"), "w", encoding="utf-8") as fh:
        keys = ["nPromised", "nServed", "Y_naive", "Y_disp", "Y_pad", "E_naive", "E_disp", "Vis_naive",
                "Vis_disp", "ext_ms"]
        fh.write("arm\tcell\t" + "\t".join(keys) + "\n")
        for arm in ("C-30", "U"):
            for c in CELLS:
                m = met[arm][c]
                fh.write(f"{arm}\t{c}\t" + "\t".join(
                    (f"{float(m[k_]):.6f}" if k_ in m else "") for k_ in keys) + "\n")

    def agg(arm, key, cells):
        return float(mean(met[arm][c][key] for c in cells))

    groups = [(w, [c for c in CELLS if cell_window(c) == w]) for w in WINDOWS] + [("all21", CELLS)]
    P("\n== C-30 (cell means) ==")
    P("group  n  Y_naive  Y_disp  Y_pad   E_naive  E_disp  Vis_naive Vis_disp ext_s")
    for g, cs in groups:
        P(f"{g:6s} {len(cs):2d} {agg('C-30','Y_naive',cs):.4f}  {agg('C-30','Y_disp',cs):.4f}  "
          f"{agg('C-30','Y_pad',cs):.4f}  {agg('C-30','E_naive',cs):.4f}   {agg('C-30','E_disp',cs):.4f}  "
          f"{agg('C-30','Vis_naive',cs):.4f}    {agg('C-30','Vis_disp',cs):.4f}   "
          f"{agg('C-30','ext_ms',cs)/1000:.1f}")
    P("\n== U (cell means) ==")
    P("group  n  Y_naive  Y_disp  E_naive E_disp Vis_naive Vis_disp ext_s")
    for g, cs in groups:
        P(f"{g:6s} {len(cs):2d} {agg('U','Y_naive',cs):.4f}  {agg('U','Y_disp',cs):.4f}  "
          f"{agg('U','E_naive',cs):.4f}  {agg('U','E_disp',cs):.4f} {agg('U','Vis_naive',cs):.4f}    "
          f"{agg('U','Vis_disp',cs):.4f}   {agg('U','ext_ms',cs)/1000:.1f}")

    # oracle
    zero = [r for c in CELLS for r in rows["C-30"][c] if r["served"] and r["dispatch"] == 0]
    ok = [r for r in zero if abs(r["A"] - r["Strue"]) <= 2000]
    zero_strict = [r for c in CELLS for r in rows["C-30"][c] if r["served"] and r["nonzeroC"] == 0]
    ok_strict = [r for r in zero_strict if abs(r["A"] - r["Strue"]) <= 2000]
    maxdev = max(abs(r["A"] - r["Strue"]) for r in zero)
    nserved_c30 = sum(1 for c in CELLS for r in rows["C-30"][c] if r["served"])
    P("\n== Oracle (C-30, 21 cells) ==")
    P(f"served C-30 riders: {nserved_c30}; dispatch part == 0: {len(zero)}; |A - S_true| <= 2 s: {len(ok)}/{len(zero)};"
      f" max |A - S_true| among them = {float(maxdev)/1000:.3f} s")
    P(f"(variant: every revision has p == e: {len(ok_strict)}/{len(zero_strict)})")

    # ledger
    late = [r for c in CELLS for r in rows["C-30"][c] if r["served"] and r["A"] - r["P0"] > THR]
    disp = sum(r["dispatch"] for r in late)
    ant = sum((r["D0"] - r["P0"] for r in late), Fraction(0))
    una = sum((r["A"] - r["D0"] - r["dispatch"] for r in late), Fraction(0))
    tot = sum(r["A"] - r["P0"] for r in late)
    P("\n== Lateness ledger (C-30 riders with A - P0 > 60 s, 21 cells) ==")
    P(f"riders {len(late)}; total A-P0 = {tot/1000:.1f} s = dispatch {disp/1000:.1f} + anticipated "
      f"{float(ant)/1000:.1f} + unanticipated {float(una)/1000:.1f}")

    # inference
    def cellvals(fn):
        return {c: float(fn(met["C-30"][c])) for c in CELLS}

    stats = {
        "dY": cellvals(lambda m: m["Y_disp"] - m["Y_naive"]),
        "dVis": cellvals(lambda m: m["Vis_disp"] - m["Vis_naive"]),
        "dPad": cellvals(lambda m: m["Y_disp"] - m["Y_pad"]),
    }
    P("\n== Inference (C-30, 21 cells; stratified day-window cluster bootstrap, B=10000) ==")
    P("primary: a fresh random.Random(20261007) for each statistic")
    res = {}
    for name, cv in stats.items():
        pt = mean(cv.values())
        lo, hi = bootstrap(cv, random.Random(SEED))
        res[name] = (pt, lo, hi)
        P(f"{name}: point {pt*100:+.3f} pp  interval [{lo*100:+.3f}, {hi*100:+.3f}] pp")
    P("variant: one random.Random(20261007) consumed sequentially dY -> dVis -> dPad")
    rng = random.Random(SEED)
    for name, cv in stats.items():
        lo, hi = bootstrap(cv, rng)
        P(f"{name}: interval [{lo*100:+.3f}, {hi*100:+.3f}] pp")

    # extra descriptive: per-day-window dY (w07)
    P("\nper day-window dY (C-30, pp):")
    for w in WINDOWS:
        for day in sorted({cell_day(c) for c in CELLS if cell_window(c) == w}):
            cs = [c for c in CELLS if cell_window(c) == w and cell_day(c) == day]
            P(f"  {w} {day}: {mean(stats['dY'][c] for c in cs)*100:+.3f} ({len(cs)} cells)")

    with open(os.path.join(HERE, "ind_analyze_output.txt"), "w", encoding="utf-8") as fh:
        fh.write("\n".join(out) + "\n")
    with open(os.path.join(HERE, "ind_results.json"), "w", encoding="utf-8") as fh:
        json.dump({
            "k": {w: float(k[w]) for w in WINDOWS},
            "k_allPromised": {w: float(k_all[w]) for w in WINDOWS},
            "C30": {g: {key: agg("C-30", key, cs) for key in
                        ("Y_naive", "Y_disp", "Y_pad", "E_naive", "E_disp", "Vis_naive", "Vis_disp", "ext_ms")}
                    for g, cs in groups},
            "U": {g: {key: agg("U", key, cs) for key in
                      ("Y_naive", "Y_disp", "E_naive", "E_disp", "Vis_naive", "Vis_disp", "ext_ms")}
                  for g, cs in groups},
            "oracle": [len(ok), len(zero)],
            "ledger_s": {"total": tot / 1000, "dispatch": disp / 1000, "anticipated": float(ant) / 1000,
                         "unanticipated": float(una) / 1000, "riders": len(late)},
            "inference": {n: list(v) for n, v in res.items()},
        }, fh, indent=1)


if __name__ == "__main__":
    sys.exit(main())
