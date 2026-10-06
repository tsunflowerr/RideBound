"""Numbers quoted in FORECAST-REPORT.md that are not printed by the main analysis scripts (requested by the
number-trace review). Read-only on saved outputs and failure records. Usage: python -B report_extras.py <out> (mode x)"""
import csv, datetime, json, pathlib, re, statistics as st, sys
HERE = pathlib.Path(__file__).resolve().parents[1]
out = ["# report_extras.py"]
W07 = [f"d201811{d}-s10-r{r}-w07" for d, rs in (("14", (3, 4)), ("15", (1, 2, 4)), ("16", (1, 2, 4))) for r in rs]
W08 = [c[:-3] + "w08" for c in W07]
W17 = ["d20181114-s10-r3-w17", "d20181114-s10-r4-w17", "d20181116-s10-r1-w17", "d20181116-s10-r2-w17", "d20181116-s10-r4-w17"]
A = W07 + W08 + W17
rows = {(r["arm"], r["cell"]): r for r in csv.DictReader(open(HERE / "test-family-a-causal-no15-2026-10-06-cells.tsv", encoding="utf-8"), delimiter="\t")}
c = lambda cell: rows[("C-30", cell)]  # noqa: E731
out.append(f"Family A C-30 21 cells: served riders {sum(int(c(x)['served']) for x in A)}; first pickup promise shown after latestPickup {sum(int(c(x)['pick_after_window']) for x in A)} (w07 {sum(int(c(x)['pick_after_window']) for x in W07)})")
tot = sum(float(c(x)["ledger_total_s"]) for x in A)
out.append("ledger shares 21 cells: " + ", ".join(f"{k} {100 * sum(float(c(x)[k]) for x in A) / tot:.1f}%" for k in ("ledger_dispatch_s", "ledger_anticipated_s", "ledger_unanticipated_s")))
for d in ("14", "15", "16"):
    cs = [x for x in W07 if x.startswith(f"d201811{d}")]
    out.append(f"w07 day {d}/11: display Y {100 * st.mean(float(c(x)['Y_disp']) for x in cs):.3f}% naive {100 * st.mean(float(c(x)['Y_naive']) for x in cs):.3f}% ({len(cs)} cells)")
out.append(f"U w17 display Y {100 * st.mean(float(rows[('U', x)]['Y_disp']) for x in W17):.2f}% naive {100 * st.mean(float(rows[('U', x)]['Y_naive']) for x in W17):.2f}%")
out.append(f"C-30 21-cell oracle {sum(int(c(x)['oracle_ok']) for x in A)}/{sum(int(c(x)['oracle_n']) for x in A)}; 34 cells {sum(int(r['oracle_ok']) for (a, cell), r in rows.items() if a == 'C-30')}/{sum(int(r['oracle_n']) for (a, cell), r in rows.items() if a == 'C-30')}")
# Family B per-cell lines -> per-window means on the chain populations
lines = (HERE / "test-family-b-2026-10-06-report.txt").read_text(encoding="utf-8").splitlines()
pat = re.compile(r"^(C-30|Mplus-30) +(d\S+): served (\d+)->(\d+) forced ([\d.]+)->([\d.]+) own-break ([\d.]+)->([\d.]+) late60 ([\d.]+)->([\d.]+) vis60 ([\d.]+)->([\d.]+)")
per = {(m.group(1), m.group(2)): [float(g) for g in m.groups()[2:]] for l in lines if (m := pat.match(l))}
mp = {cell: v for (a, cell), v in per.items() if a == "Mplus-30"}
cp = {cell: v for (a, cell), v in per.items() if a == "C-30"}
for w in ("w07", "w08"):
    ms = [x for x in mp if x.endswith(w)]
    out.append(f"Mplus-30F chain-M cells {w} ({len(ms)}): forced {st.mean(mp[x][2] for x in ms):.3f}->{st.mean(mp[x][3] for x in ms):.3f}; own-break {st.mean(mp[x][4] for x in ms):.3f}->{st.mean(mp[x][5] for x in ms):.3f}")
out.append(f"Mplus-30F forced mean over its {len(mp)} completed cells: {st.mean(v[3] for v in mp.values()):.3f} (naive {st.mean(v[2] for v in mp.values()):.3f})")
dws = {}
for x, v in cp.items():
    dws.setdefault(x[:9] + x[-4:], []).append(v)
up = sum(1 for vs in dws.values() if st.mean(v[9] for v in vs) > st.mean(v[8] for v in vs))
out.append(f"PB3: C-30F visible60 higher than naive in {up}/{len(dws)} day-windows ({len(cp)} completed cells)")
for name in ("C-30F-d20181116-s10-r1-w08", "Mplus-30F-d20181116-s10-r1-w08", "C-30F-d20181114-s10-r3-w08"):
    msg = json.loads((pathlib.Path(r"C:/RideBoundData/research/tier3-forecast-v1") / name / "failure-00.json").read_text(encoding="utf-8"))["failureMessage"]
    late = [(float(m.group(2)), float(m.group(1))) for m in re.finditer(r"latest arr ([\d.]+) eta ([\d.]+)", msg)]
    worst = max(((eta - la, eta, la) for eta, la in late), default=None)
    out.append(f"{name}: worst stop eta - latest = {worst[0]:.2f} s (eta {worst[1]:.1f}, latest {worst[2]:.1f}, bin {int(worst[2] // 900)})")
out.append(f"calendar: 2018-11-11 is a {datetime.date(2018, 11, 11).strftime('%A')}; 2018-11-12 is a {datetime.date(2018, 11, 12).strftime('%A')}")
text = "\n".join(out) + "\n"
open(sys.argv[1], "x", encoding="utf-8", newline="\n").write(text); print(text)
