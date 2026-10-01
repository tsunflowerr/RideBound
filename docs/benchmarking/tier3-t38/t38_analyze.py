"""T3.8/T3.9 test-phase analysis (T38-PLAN.md §3.3, §4, §5), read-only; every output file is created with mode "x".

Inputs: split-t38-v1.json, GAMMA-t38-v1.json (written by t38_calibrate.py before any test job), the 170 test runs and
the 15 calibration runs in run_t38.OUT_ROOT. Reuses sweep_analyze.job_row (M1-M8 and the v/e/p audit),
pilot_analyze.fleet_decisions, w07x_analyze.policy_hash / rider_v, t38_calibrate.episode_score / run_problems and
conformal.covered / clopper_pearson_upper.
  H3a: test episodes with S_j > gamma <= 3/34 (10%); Clopper-Pearson 95% upper bound reported, not a pass rule.
  H3b: beta + gamma <= 300 s; 120 s reported.
  check: in every covered test episode, max_r V_r <= beta + gamma (V_r <= C_r + X_r and C_r <= 30 s).
  H2 (all four): 95% paired cell-bootstrap intervals (B = 10000, random.Random(20261002), one resample shared by every
  statistic, bounds = the 250th and the 9751st of the sorted means) of mean(C-30 - Mplus-30) and mean(C-30 - V-30)
  served inside [-2, 2]; 0 non-normal C-30 decisions in the 34 test runs; upper bound of mean(E2 C-30 - E2 V-30) and of
  mean(E2 C-30 - E2 Mplus-30) <= 0.02; upper bound of mean(U - C-30) served <= 5.
Then descriptive tables by window and by day, and the predictions P1-P8.
Usage: python -B t38_analyze.py <out-prefix> [partial]
"""
from __future__ import annotations

import json
import pathlib
import random
import statistics
import sys

HERE = pathlib.Path(__file__).resolve().parent
TANG3 = HERE.parent
for sub in ("sweep", "pilot", "evidence", "world", "w07", "w07x", "calib"):
    sys.path.insert(0, str(TANG3 / sub))

import conformal  # noqa: E402
import run_t38  # noqa: E402
import sweep_analyze  # noqa: E402
import t38_calibrate  # noqa: E402
import w07x_analyze  # noqa: E402
from pilot_analyze import fleet_decisions  # noqa: E402

TEST_ARMS, TEST = run_t38.PHASES["test"]
CALIBRATION = run_t38.PHASES["calib"][1]
B = 10_000
BOOT_SEED = 20261002
EQUIVALENCE = 2
DELTA_R = 0.02
DELTA_S = 5
WINDOWS = ("w07", "w08", "w17")
DAYS = ("14", "15", "16", "17", "18")


def bootstrap(columns: dict[str, list[float]]) -> dict[str, tuple[float, float, float]]:
    """name -> (mean, lower, upper); one shared resample of the cells per replicate."""
    m = len(next(iter(columns.values())))
    rng = random.Random(BOOT_SEED)
    means = {name: [] for name in columns}
    for _ in range(B):
        idx = [rng.randrange(m) for _ in range(m)]
        for name, values in columns.items():
            means[name].append(sum(values[i] for i in idx) / m)
    out = {}
    for name, values in means.items():
        ordered = sorted(values)
        out[name] = (statistics.mean(columns[name]), ordered[249], ordered[9750])
    return out


def main() -> int:
    if len(sys.argv) not in (2, 3) or (len(sys.argv) == 3 and sys.argv[2] != "partial"):
        print(__doc__)
        return 2
    prefix, partial = sys.argv[1], len(sys.argv) == 3
    gamma_info = json.loads(run_t38.GAMMA_FILE.read_text(encoding="utf-8"))
    beta_ms, gamma_ms, infinite = gamma_info["betaMs"], gamma_info["gammaMs"], gamma_info["infinite"]
    threshold = conformal.Threshold(gamma_info["n"], 1, 10, gamma_info["k"], gamma_ms, infinite, gamma_info["reason"])
    sweep_analyze.OUT_ROOT, sweep_analyze.effective, sweep_analyze.state_of = run_t38.OUT_ROOT, (lambda n: n), run_t38.state_of

    rows, fleets, hashes, problems, vown = {}, {}, {}, [], {}
    for cell in TEST:
        for arm in TEST_ARMS:
            name = t38_calibrate.effective(f"{arm}-{cell}")
            state = run_t38.state_of(name)
            if not state.startswith("status-"):
                if not partial:
                    problems.append(f"{name}: state {state}")
                continue
            found, policy = t38_calibrate.run_problems(name, cell)
            problems.extend(found)
            row = sweep_analyze.job_row(name, arm, "W5", cell)
            if row.get("audit") != "ok":
                problems.append(f"{name}: audit {row.get('audit')}")
                continue
            rows[(arm, cell)] = row
            hashes[(arm, cell)] = policy
            transcript = run_t38.OUT_ROOT / name / "transcript-00.ndjson"
            fleets[(arm, cell)] = [d for _, d in fleet_decisions(transcript)]
            if arm == "V-30":
                values = w07x_analyze.rider_v(transcript)
                vown[cell] = sum(1 for v in values.values() if v > 30_000) / len(values) if values else 0.0
    arm_hash = {}
    for arm in TEST_ARMS:
        found = {hashes[(arm, c)] for c in TEST if (arm, c) in hashes}
        if len(found) > 1:
            problems.append(f"{arm}: {len(found)} policyConfigurationHash values across cells")
        arm_hash[arm] = next(iter(found), None)
    known = [h for h in arm_hash.values() if h]
    if len(set(known)) != len(known):
        problems.append("two arms share one policyConfigurationHash")
    if arm_hash.get("C-30") not in (None, gamma_info["policyConfigurationHash"]):
        problems.append("C-30 policyConfigurationHash differs from the calibrated policy")

    out = []
    say = out.append
    say(f"# t38_analyze.py {'PARTIAL' if partial else 'FULL'}; test jobs analysed {len(rows)}/{len(TEST) * len(TEST_ARMS)}")
    say(f"== validity: {len(problems)} problem(s)")
    for problem in problems or ["none"]:
        say("  " + problem)
    say(f"  policyConfigurationHash distinct per arm: {len(set(known))} hashes for {len(known)} arms")

    # H3
    say("\n== H3 (T3.8): calibrated gamma from GAMMA-t38-v1.json")
    say(f"  n = {threshold.n}, k = {threshold.k}, gamma = {'INFINITE' if infinite else f'{gamma_ms / 1000:.3f} s'}; "
        f"beta + gamma = {'INFINITE' if infinite else f'{(beta_ms + gamma_ms) / 1000:.3f} s'}")
    scores, cover, vmax = {}, {}, {}
    for cell in TEST:
        if ("C-30", cell) not in rows:
            continue
        score, riders, above60, _ = t38_calibrate.episode_score(run_t38.OUT_ROOT / t38_calibrate.effective(f"C-30-{cell}"))
        scores[cell] = score
        cover[cell] = conformal.covered(score, threshold)
        vmax[cell] = max(w07x_analyze.rider_v(run_t38.OUT_ROOT / t38_calibrate.effective(f"C-30-{cell}") / "transcript-00.ndjson").values(), default=0)
    m = len(scores)
    violations = sorted(c for c in scores if not cover[c])
    say(f"  test episodes scored {m}/{len(TEST)}; S_j > gamma: {len(violations)} {violations}")
    if m:
        cp = conformal.clopper_pearson_upper(len(violations), m)
        say(f"  violation share {len(violations)}/{m} = {len(violations) / m:.3f}; Clopper-Pearson 95% upper bound {cp:.3f}")
    h3a = None if m < len(TEST) else len(violations) <= 3
    h3b = None if infinite else beta_ms + gamma_ms <= 300_000
    say(f"  H3a (violations <= 3/34): {h3a}; H3b (beta + gamma <= 300 s): {h3b}; "
        f"within 120 s: {None if infinite else beta_ms + gamma_ms <= 120_000}")
    say(f"  H3 {'PASS' if h3a and h3b else 'FAIL' if h3a is not None and h3b is not None else 'incomplete'}")
    bad = [c for c in scores if cover[c] and not infinite and vmax[c] > beta_ms + gamma_ms]
    say(f"  check max_r V_r <= beta + gamma in covered episodes: {len(bad)} failure(s) {bad}")
    for group, members in [(w, [c for c in scores if c.endswith(w)]) for w in WINDOWS] + \
                          [(f"201811{d}", [c for c in scores if c[7:9] == d]) for d in DAYS]:
        if members:
            s = [scores[c] / 1000 for c in members if scores[c] != conformal.INFINITE]
            say(f"  {group}: {len(members)} episodes, S_j median {statistics.median(s):.1f} max {max(s):.1f} s, "
                f"violations {sum(1 for c in members if not cover[c])}")
    say("  per episode: S_j (s), covered, max V_r (s)")
    for cell in TEST:
        if cell in scores:
            shown = "INF" if scores[cell] == conformal.INFINITE else f"{scores[cell] / 1000:.1f}"
            say(f"    {cell}: {shown}  {cover[cell]}  {vmax[cell] / 1000:.1f}")

    # H2
    say("\n== H2 (T3.9): cells with all five arms passing")
    complete = [c for c in TEST if all((a, c) in rows for a in TEST_ARMS)]
    served = lambda a, c: rows[(a, c)]["M1_completed"]  # noqa: E731
    e2 = lambda a, c: float(rows[(a, c)]["M2_E2"])  # noqa: E731
    say(f"  complete cells {len(complete)}/{len(TEST)}")
    h2 = {}
    if complete:
        columns = {
            "D_M": [served("C-30", c) - served("Mplus-30", c) for c in complete],
            "D_V": [served("C-30", c) - served("V-30", c) for c in complete],
            "F": [served("U", c) - served("C-30", c) for c in complete],
            "dE2_V": [e2("C-30", c) - e2("V-30", c) for c in complete],
            "dE2_M": [e2("C-30", c) - e2("Mplus-30", c) for c in complete],
        }
        boot = bootstrap(columns)
        for name, (mean, lo, hi) in boot.items():
            say(f"  {name}: mean {mean:+.4f}, 95% interval [{lo:+.4f}, {hi:+.4f}]")
        nonnormal = [c for c in TEST if ("C-30", c) in rows and float(rows[("C-30", c)]["M5_nonNormalShare"] or 0) > 0]
        h2["a"] = all(-EQUIVALENCE <= boot[k][1] and boot[k][2] <= EQUIVALENCE for k in ("D_M", "D_V"))
        h2["b"] = not nonnormal and all(("C-30", c) in rows for c in TEST)
        h2["c"] = boot["dE2_V"][2] <= DELTA_R and boot["dE2_M"][2] <= DELTA_R
        h2["floor"] = boot["F"][2] <= DELTA_S
        say(f"  H2a (both intervals inside [-2, 2]): {h2['a']}")
        say(f"  H2b (0 non-normal C-30 decisions in the test runs): {h2['b']} {nonnormal}")
        say(f"  H2c (upper bounds of mean dE2 vs V-30 and vs Mplus-30 <= 0.02): {h2['c']}")
        say(f"  floor (upper bound of mean U - C-30 <= 5): {h2['floor']}")
        full = len(complete) == len(TEST)
        say(f"  H2 {'PASS' if full and all(h2.values()) else 'FAIL' if full else 'incomplete'}")
        say(f"  totals over {len(complete)} cells: " + ", ".join(f"{a} {sum(served(a, c) for c in complete)}" for a in TEST_ARMS))
        say(f"  per-cell |D_M| > 2: {[c for c, d in zip(complete, columns['D_M']) if abs(d) > 2]}; "
            f"|D_V| > 2: {[c for c, d in zip(complete, columns['D_V']) if abs(d) > 2]}")
        say(f"  cells where C-30 and Mplus-30 take the same fleet actions: "
            f"{sum(1 for c in complete if fleets[('C-30', c)] == fleets[('Mplus-30', c)])}/{len(complete)}; "
            f"C-30 and V-30: {sum(1 for c in complete if fleets[('C-30', c)] == fleets[('V-30', c)])}/{len(complete)}")

    # descriptive
    say("\n== descriptive (not pass rules): means over cells; own promise broken, forced share, prunes, E2, served")
    own = {"C-30": lambda c: float(rows[("C-30", c)]["M4_shareC_gt_x"] or 0),
           "Mplus-30": lambda c: float(rows[("Mplus-30", c)]["M3_shareA_gt_x"] or 0),
           "Mplus-60": lambda c: float(rows[("Mplus-60", c)]["M3_shareA_gt_x"] or 0),
           "V-30": lambda c: vown[c]}
    for group, members in [("all", complete)] + [(w, [c for c in complete if c.endswith(w)]) for w in WINDOWS] + \
                          [(f"201811{d}", [c for c in complete if c[7:9] == d]) for d in DAYS]:
        if not members:
            continue
        say(f"  {group} ({len(members)} cells):")
        for arm in TEST_ARMS:
            parts = [f"served {sum(served(arm, c) for c in members)}",
                     f"forced {statistics.mean(float(rows[(arm, c)]['M5_nonNormalShare'] or 0) for c in members):.3f}",
                     f"prunes {sum(int(rows[(arm, c)]['M7_gatePrunes']) for c in members)}",
                     f"E2 {statistics.mean(e2(arm, c) for c in members):.3f}",
                     f"A > 60 s {statistics.mean(float(rows[(arm, c)]['M3_shareA_gt_60s'] or 0) for c in members):.3f}"]
            if arm in own:
                values = [own[arm](c) for c in members]
                parts.append(f"own promise broken {statistics.mean(values):.3f} (cells > 0: {sum(1 for v in values if v > 0)})")
            say(f"    {arm:<9} " + " | ".join(parts))

    # calibration C-30 non-normal (P4)
    calib_nonnormal = []
    for cell in CALIBRATION:
        name = t38_calibrate.effective(f"C-30-{cell}")
        if run_t38.state_of(name) == "status-pass":
            row = sweep_analyze.job_row(name, "C-30", "W5", cell)
            if float(row.get("M5_nonNormalShare") or 0) > 0:
                calib_nonnormal.append(cell)
    say(f"\n  calibration C-30 runs with non-normal decisions: {len(calib_nonnormal)}/{len(CALIBRATION)} {calib_nonnormal}")

    # predictions
    say("\n== predictions (T38-PLAN §5)")
    done = len(complete) == len(TEST) and not infinite
    grade = lambda ok: ("HELD" if ok else "NOT HELD") if done else "incomplete"  # noqa: E731
    if not infinite:
        say(f"P1 gamma in [100, 250] s: {gamma_ms / 1000:.1f} s -> {'HELD' if 100_000 <= gamma_ms <= 250_000 else 'NOT HELD'}")
        width = beta_ms + gamma_ms
        say(f"P2 beta + gamma <= 300 s and > 120 s: {width / 1000:.1f} s -> {'HELD' if 120_000 < width <= 300_000 else 'NOT HELD'}")
    else:
        say("P1, P2: gamma INFINITE -> NOT HELD")
    w07v = sum(1 for c in violations if c.endswith("w07"))
    p3 = len(violations) <= 3 and (not violations or w07v * 2 > len(violations))
    say(f"P3 violations <= 3/34 and, if any, most in w07: {len(violations)} ({w07v} in w07) -> {grade(p3)}")
    c30_nonnormal = (len([c for c in TEST if ("C-30", c) in rows and float(rows[('C-30', c)]['M5_nonNormalShare'] or 0) > 0])
                     + len(calib_nonnormal))
    say(f"P4 0 non-normal decisions in all 49 C-30 jobs: {c30_nonnormal} -> {grade(c30_nonnormal == 0)}")
    if complete:
        p5 = h2["a"] and all(abs(d) <= 2 for d in columns["D_M"] + columns["D_V"])
        say(f"P5 H2a and every |D_j| <= 2: {grade(p5)}")
        p6 = h2["c"] and h2["floor"] and 0 <= boot["F"][0] <= 3
        say(f"P6 H2c, floor, and mean F in [0, 3] (mean F {boot['F'][0]:+.3f}): {grade(p6)}")
        w07 = [c for c in complete if c.endswith("w07")]
        w17 = [c for c in complete if c.endswith("w17")]
        b07 = sum(1 for c in w07 if own["Mplus-30"](c) > 0)
        b17 = sum(1 for c in w17 if own["Mplus-30"](c) > 0)
        say(f"P7 Mplus-30 own promise broken in >= 8/9 w07 cells ({b07}/{len(w07)}) and <= 3/11 w17 cells ({b17}/{len(w17)}): "
            f"{grade(b07 >= 8 and b17 <= 3)}")
        fv = statistics.mean(float(rows[("V-30", c)]["M5_nonNormalShare"] or 0) for c in complete)
        fm = statistics.mean(float(rows[("Mplus-30", c)]["M5_nonNormalShare"] or 0) for c in complete)
        say(f"P8 mean forced share V-30 > Mplus-30: {fv:.3f} vs {fm:.3f} -> {grade(fv > fm)}")

    columns_tsv = sorted({k for r in rows.values() for k in r if not k.startswith("_")},
                         key=lambda k: (k not in ("job", "arm", "cell", "state", "audit"), k))
    with open(f"{prefix}-jobs.tsv", "x", encoding="utf-8", newline="\n") as handle:
        handle.write("\t".join(columns_tsv + ["policyConfigurationHash", "V_own_broken", "S_j_ms", "covered"]) + "\n")
        for (arm, cell), r in rows.items():
            extra = [str(hashes.get((arm, cell))), str(vown.get(cell, "")) if arm == "V-30" else "",
                     str(scores.get(cell, "")) if arm == "C-30" else "", str(cover.get(cell, "")) if arm == "C-30" else ""]
            handle.write("\t".join(["" if r.get(c) is None else str(r.get(c)) for c in columns_tsv] + extra) + "\n")
    text = "\n".join(out) + "\n"
    with open(f"{prefix}-report.txt", "x", encoding="utf-8", newline="\n") as handle:
        handle.write(text)
    sys.stdout.write(text)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
