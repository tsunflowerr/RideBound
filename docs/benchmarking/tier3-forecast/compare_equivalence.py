"""Equivalence comparator for FORECAST-PLAN.md gates E1/E2 (and its positive control and self-test).

Two runs are EQUIVALENT when all of these are identical:
  (1) acknowledged fleet decisions in order, with their simulation times (pilot_analyze.fleet_decisions);
  (2) the signed v/e/p audit table (every promise revision of every rider);
  (3) the served set (lifecycle completed) and every passengerAlighted time;
  (4) every frame after decoding, with run-salted fields removed: keys runId, publicationId, publicationIds, breachId and any
      key ending in "Hash" or "hash", at any depth (run identity salts these; the rest must match exactly); the
      "actions" list inside a decision is compared as a set (sorted canonically), because its order follows salted
      ids (found on the development control, compare first frame 35). Route orders (serviceOrder) stay ordered.
Usage:
  python -B compare_equivalence.py <run-a> <run-b> <out-file>          compare two run folders (mode x output)
  python -B compare_equivalence.py --selftest <run> <other-run> <out>   mutation self-test: perturbing one decision time
                                                                       and one alight time must be detected, and
                                                                       <run> vs <other-run> (e.g. U vs C-30) must differ
"""
from __future__ import annotations

import base64
import copy
import json
import pathlib
import sys

HERE = pathlib.Path(__file__).resolve().parent
TANG3 = HERE.parent / "tang3"
for sub in ("pilot", "evidence", "sweep"):
    sys.path.insert(0, str(TANG3 / sub))
import audit_vep  # noqa: E402
import sweep_analyze  # noqa: E402
from pilot_analyze import fleet_decisions  # noqa: E402

SALTED = {"runId", "publicationId", "publicationIds", "breachId"}   # breachId: run-salted id (development control frame 2159)


def strip(value):
    if isinstance(value, dict):
        out = {k: strip(v) for k, v in value.items() if k not in SALTED and not k.endswith(("Hash", "hash"))}
        if isinstance(out.get("actions"), list):     # order of actions inside one decision follows salted ids
            out["actions"] = sorted(out["actions"], key=lambda item: json.dumps(item, sort_keys=True))
        return out
    if isinstance(value, list):
        return [strip(v) for v in value]
    return value


def facts(run: pathlib.Path) -> dict:
    transcript = run / "transcript-00.ndjson"
    _, _, state = audit_vep.load(transcript)
    table, _ = audit_vep.audit(transcript)
    _, alighted = sweep_analyze.event_times(transcript)
    frames = []
    with open(transcript, encoding="utf-8") as handle:
        for line in handle:
            record = json.loads(line)
            envelope = json.loads(base64.b64decode(record["frameBase64"]))
            frames.append((record["direction"], strip(envelope)))
    return {"decisions": fleet_decisions(transcript), "table": table,
            "served": sorted(r["requestId"] for r in state["requests"] if r["lifecycle"] == "completed"),
            "alighted": dict(sorted(alighted.items())), "frames": frames}


def compare(a: dict, b: dict) -> dict:
    result = {}
    for key in ("decisions", "table", "served", "alighted", "frames"):
        same = a[key] == b[key]
        detail = ""
        if not same and isinstance(a[key], list):
            n = min(len(a[key]), len(b[key]))
            first = next((i for i in range(n) if a[key][i] != b[key][i]), n)
            detail = f"lengths {len(a[key])}/{len(b[key])}, first difference at index {first}"
        elif not same:
            diff = [k for k in set(a[key]) | set(b[key]) if a[key].get(k) != b[key].get(k)]
            detail = f"{len(diff)} riders differ"
        result[key] = (same, detail)
    return result


def write(out: str, lines: list[str]) -> None:
    text = "\n".join(lines) + "\n"
    with open(out, "x", encoding="utf-8", newline="\n") as handle:
        handle.write(text)
    sys.stdout.write(text)


def main(argv: list[str]) -> int:
    if argv and argv[0] == "--selftest":
        run, other, out = pathlib.Path(argv[1]), pathlib.Path(argv[2]), argv[3]
        base = facts(run)
        lines = [f"# compare_equivalence.py self-test on {run.name} (vs {other.name})"]
        same = compare(base, copy.deepcopy(base))
        lines.append(f"identical copy judged equivalent: {all(s for s, _ in same.values())}")
        p1 = copy.deepcopy(base)
        t, d = p1["decisions"][len(p1["decisions"]) // 2]
        p1["decisions"][len(p1["decisions"]) // 2] = (t + 1, d)
        r1 = compare(base, p1)
        lines.append(f"one decision time +1 ms detected: {not r1['decisions'][0]}")
        p2 = copy.deepcopy(base)
        key = next(iter(p2["alighted"]))
        p2["alighted"][key] += 1
        r2 = compare(base, p2)
        lines.append(f"one alight time +1 ms detected: {not r2['alighted'][0]}")
        pc = compare(base, facts(other))
        lines.append(f"positive control {run.name} vs {other.name} differs: {not all(s for s, _ in pc.values())} "
                     f"({ {k: v[1] or 'same' for k, v in pc.items()} })")
        ok = all(s for s, _ in same.values()) and not r1["decisions"][0] and not r2["alighted"][0] and not all(s for s, _ in pc.values())
        lines.append(f"SELFTEST {'PASS' if ok else 'FAIL'}")
        write(out, lines)
        return 0 if ok else 1
    a, b, out = pathlib.Path(argv[0]), pathlib.Path(argv[1]), argv[2]
    result = compare(facts(a), facts(b))
    ok = all(s for s, _ in result.values())
    write(out, [f"# compare_equivalence.py {a} vs {b}"] + [f"{k}: {'identical' if s else 'DIFFERENT'} {d}" for k, (s, d) in result.items()]
          + [f"EQUIVALENT: {ok}"])
    return 0 if ok else 1


if __name__ == "__main__":
    if len(sys.argv) not in (4, 5):
        raise SystemExit(__doc__)
    raise SystemExit(main(sys.argv[1:]))
