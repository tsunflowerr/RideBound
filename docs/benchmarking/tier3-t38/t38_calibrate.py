"""T3.8 calibration (T38-PLAN.md §3): episode scores of the 15 calibration C-30 runs, then the split-conformal gamma.

Episode score S_j (plan §3.1): the largest X_r over the episode's promised riders, X_r = sum |x| over the rider's drop
revisions (x = e - v, the change of the drop ETA seen when the route is kept), in integer milliseconds, read from the
v/e/p audit (evidence/audit_vep.py); per-rider X must add up to the audit's sumDropX. If a promised rider never
alighted, S_j is INFINITE (unobserved), never a number. Because V_r <= C_r + X_r and C-30 keeps C_r <= 30 s, a covered
episode (S_j <= gamma) has V_r <= 30 s + gamma for every promised rider.
gamma: conformal.threshold with alpha = 1/10 (k = ceil((n + 1) * 0.9); n = 15 gives k = 15, the largest score).
Validity before anything is computed: 15/15 runs status pass, audit ok, the expected inventory, one
policyConfigurationHash, and the world log lines of every run (v1.2 version line with the world file's SHA-256, the
guard naming the episode's own day and window start, bins at bin * 900 s with factors equal to the factor file).
Writes GAMMA_FILE (mode "x"; its presence unlocks run_t38.py phase test) and a log.
Usage: python -B t38_calibrate.py <new-log>
"""
from __future__ import annotations

import hashlib
import json
import pathlib
import sys

HERE = pathlib.Path(__file__).resolve().parent
TANG3 = HERE.parent
for sub in ("sweep", "pilot", "evidence", "world", "w07", "w07x", "calib"):
    sys.path.insert(0, str(TANG3 / sub))

import audit_vep  # noqa: E402
import conformal  # noqa: E402
import preflight_world as pw  # noqa: E402
import run_t38  # noqa: E402
import run_w07  # noqa: E402
import sweep_analyze  # noqa: E402
import w07_analyze  # noqa: E402
import w07x_analyze  # noqa: E402
from world_v1 import BIN_S  # noqa: E402

ALPHA = (1, 10)
BETA_MS = 30_000
USEFUL_MS = 300_000          # plan §3.3: beta + gamma <= 300 s (half the 600 s pickup window of every request)
REFERENCE_MS = 120_000       # plan §3.3: also reported against 120 s (2 min), not a pass rule


def effective(name: str) -> str:
    """Amendment A1: a job whose own folder has no summary.json (infrastructure failure) is read from its -rerun1."""
    rerun = f"{name}-rerun1"
    if run_t38.state_of(name) == "exists-no-summary" and (run_t38.OUT_ROOT / rerun).exists():
        return rerun
    return name


def episode_score(run_dir: pathlib.Path):
    """(score in ms or conformal.INFINITE, number of promised riders, riders with X_r > 60 s, detail)."""
    transcript = run_dir / "transcript-00.ndjson"
    table, summary = audit_vep.audit(transcript)
    if table is None:
        raise SystemExit(f"{run_dir}: no final checkpoint")
    x = {}
    for request, coordinate, _, kind, _, _, _, _, xv, _, _, _ in table:
        if coordinate != "drop":
            continue
        x.setdefault(request, 0)
        if kind != "initialPromise":
            x[request] += abs(xv)
    if sum(x.values()) != summary["sumDropX"]:
        raise SystemExit(f"{run_dir}: per-rider X does not add up to the audit's sumDropX")
    if len(x) != summary["promisedRiders"]:
        raise SystemExit(f"{run_dir}: {len(x)} riders with a drop promise, audit says {summary['promisedRiders']}")
    if not all(isinstance(v, int) for v in x.values()):
        raise SystemExit(f"{run_dir}: X is not in integer milliseconds")
    _, alighted = sweep_analyze.event_times(transcript)
    unfinished = sorted(r for r in x if r not in alighted)
    if not x:
        return 0, 0, 0, "no promised rider: score 0 (plan §3.1)"
    if unfinished:
        return conformal.INFINITE, len(x), None, f"{len(unfinished)} promised riders never alighted: {unfinished[:3]}"
    worst = max(x, key=lambda r: (x[r], r))
    return x[worst], len(x), sum(1 for v in x.values() if v > 60_000), f"largest X_r on {worst}"


def expected_base(day: str, start: int):
    saved = pw.ALLOWED_WINDOW_STARTS
    pw.ALLOWED_WINDOW_STARTS = (25200, 28800, 61200)
    try:
        return pw.measured_base(day, 1.0, start, 24)
    finally:
        pw.ALLOWED_WINDOW_STARTS = saved


def log_problems(name: str, cell: str) -> list[str]:
    """The world lines of a job log (as w07x_analyze.log_problems, for any day and window)."""
    log = run_t38.LOG_ROOT / f"{name}.log"
    if not log.exists():
        return [f"{name}: no log"]
    lines = log.read_text(encoding="utf-8", errors="replace").splitlines()
    day, start = f"2018-11-{cell[7:9]}", run_t38.window_start(cell)
    base = expected_base(day, start)
    problems = []
    if not lines or not lines[0].startswith("returncode=0 "):
        problems.append(f"{name}: {lines[0] if lines else 'empty log'}")
    world_sha = hashlib.sha256(run_t38.world_file(cell).read_bytes()).hexdigest()
    versions = [m for line in lines if (m := w07_analyze.VERSION.match(line))]
    if len(versions) != 1 or versions[0]["sha"] != world_sha:
        problems.append(f"{name}: v1.2 version line missing or world SHA-256 mismatch")
    guards = [m for line in lines if (m := w07_analyze.GUARD.match(line))]
    if len(guards) != 1 or (guards[0]["day"], int(guards[0]["start"])) != (day, start):
        problems.append(f"{name}: guard line missing or wrong")
    bins = [(int(m["bin"]), int(m["t"]), float(m["lo"]), float(m["hi"])) for line in lines
            if (m := w07_analyze.BIN.match(line))]
    if not bins or [b[0] for b in bins] != list(range(len(bins))) or any(t != b * BIN_S for b, t, _, _ in bins):
        problems.append(f"{name}: bins not 0..k at bin * {BIN_S} s")
    if any(abs(lo - base[b]) > 1e-4 or abs(hi - base[b]) > 1e-4 for b, _, lo, hi in bins):
        problems.append(f"{name}: a bin factor differs from the factor file of {day} at {start}")
    applied = [int(m["n"]) for line in lines if (m := w07_analyze.APPLIED.match(line))]
    if applied != [len(bins)]:
        problems.append(f"{name}: applied {applied} vs {len(bins)} bin lines")
    return problems


def run_problems(name: str, cell: str) -> tuple[list[str], str | None]:
    problems = []
    state = run_t38.state_of(name)
    if state != "status-pass":
        return [f"{name}: state {state}"], None
    summary = json.loads((run_t38.OUT_ROOT / name / "summary.json").read_text(encoding="utf-8"))
    if summary.get("repositoryInventorySha256") != run_w07.EXPECTED_INVENTORY:
        problems.append(f"{name}: inventory {summary.get('repositoryInventorySha256')}")
    problems.extend(log_problems(name, cell))
    return problems, w07x_analyze.policy_hash(run_t38.OUT_ROOT / name / "transcript-00.ndjson")


def main(log_path: str) -> int:
    cells = run_t38.PHASES["calib"][1]
    problems, hashes, scores, lines = [], set(), {}, []
    for cell in cells:
        name = effective(f"C-30-{cell}")
        found, policy = run_problems(name, cell)
        problems.extend(found)
        hashes.add(policy)
        if not found:
            score, riders, above60, detail = episode_score(run_t38.OUT_ROOT / name)
            scores[cell] = score
            shown = "INFINITE" if score == conformal.INFINITE else f"{score / 1000:.1f} s"
            lines.append(f"  {cell}: S_j = {shown}; promised riders {riders}; X_r > 60 s: {above60}; {detail}")
    if len(hashes) != 1 or None in hashes:
        problems.append(f"policyConfigurationHash values across calibration runs: {sorted(map(str, hashes))}")
    out = [f"# t38_calibrate.py; calibration episodes {len(cells)}; validity problems {len(problems)}"]
    out += ["  " + p for p in problems]
    out += ["== episode scores (C-30)"] + lines
    if problems or len(scores) != len(cells):
        out.append("NOT CALIBRATED: validity problems (no gamma file written)")
        text = "\n".join(out) + "\n"
        with open(log_path, "x", encoding="utf-8", newline="\n") as handle:
            handle.write(text)
        sys.stdout.write(text)
        return 1
    conformal.check_split(scores, {c: 0 for c in run_t38.PHASES["test"][1]},
                          {c: c[:16] for c in list(cells) + list(run_t38.PHASES["test"][1])})
    t = conformal.threshold([scores[c] for c in cells], *ALPHA)
    width = None if t.infinite else BETA_MS + t.gamma_ms
    result = {
        "splitId": run_t38.SPLIT["splitId"],
        "splitSha256": run_t38.SPLIT_SHA256,
        "policy": "C-30",
        "policyConfigSha256": run_t38.CONFIG_SHA256["C-30"],
        "policyConfigurationHash": next(iter(hashes)),
        "alpha": f"{ALPHA[0]}/{ALPHA[1]}",
        "n": t.n,
        "k": t.k,
        "infinite": t.infinite,
        "gammaMs": t.gamma_ms,
        "reason": t.reason,
        "betaMs": BETA_MS,
        "betaPlusGammaMs": width,
        "usefulThresholdMs": USEFUL_MS,
        "useful": None if width is None else width <= USEFUL_MS,
        "referenceThresholdMs": REFERENCE_MS,
        "withinReference": None if width is None else width <= REFERENCE_MS,
        "calibrationScoresMs": {c: scores[c] for c in cells},
    }
    text_json = json.dumps(result, indent=2) + "\n"
    with open(run_t38.GAMMA_FILE, "x", encoding="utf-8", newline="\n") as handle:
        handle.write(text_json)
    out.append(f"== gamma: {t.reason}; k = {t.k}, n = {t.n}, alpha = 1/10")
    out.append(f"   gamma = {'INFINITE' if t.infinite else f'{t.gamma_ms / 1000:.3f} s'}; beta + gamma = "
               f"{'INFINITE' if width is None else f'{width / 1000:.3f} s'}; useful (<= 300 s): {result['useful']}; "
               f"within 120 s: {result['withinReference']}")
    out.append(f"   wrote {run_t38.GAMMA_FILE.name} sha256 {hashlib.sha256(text_json.encode('utf-8')).hexdigest()}")
    text = "\n".join(out) + "\n"
    with open(log_path, "x", encoding="utf-8", newline="\n") as handle:
        handle.write(text)
    sys.stdout.write(text)
    return 0


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit(__doc__)
    raise SystemExit(main(sys.argv[1]))
