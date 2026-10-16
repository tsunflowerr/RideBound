"""R13 final judge: candidate-level test of the CUMULATIVE clause of rule C, existing transcripts only (read-only).

Question: in the runs that ENFORCED a drop budget B (C-B arms), how many dispatch decisions could a PER-STEP cap of the
same B have decided differently?  A per-step cap rejects a candidate only if some rider's single increment
(witness.delta) exceeds B.  Rule C rejects if before + delta > B.  So a budget-pruned candidate is
  - "per-step also": its reported drop witness has delta > limit  (a per-step cap rejects it too), or
  - "cumulative-only (reported)": before > 0, delta <= limit, after > limit (only the cumulative clause rejects it).
Other budget witnesses (zero-limit switch counts) are lock-like and reported separately.

Caveat (code): the validator is fail-fast (CommitmentDecisionValidator.cs:222-231); only the first failing rider (by
request id) is reported. A 'cumulative-only (reported)' candidate may still be rejected by an unreported rider under a
per-step cap, so every cumulative-only count below is an UPPER BOUND on decisions a per-step cap could change
(first order: ignores what would happen afterwards).

Only acknowledged decisions (decisionApplied) are counted.  Requests: final lifecycle from the last checkpoint.
Usage: python -B cumulative_decisive_check.py <out.txt>   (out opened with mode "x")."""
import base64, collections, json, pathlib, sys
from concurrent.futures import ProcessPoolExecutor

OPT = pathlib.Path("E:/Code/Report_INT3508/ridebound-scratchpad/optimize")


def t38_test_runs():
    """The 34 T3.8 test-cell C-30 run folders, resolved through the owner's run_dir (handles '-rerun1' folders)."""
    sys.path.insert(0, str(OPT))
    import forecast_family_a as fa  # noqa: E402  (owner's analysis module, read-only use)
    return [fa.run_dir("test", "C-30", cell) for cell in fa.run_t38.PHASES["test"][1]]


def jobs():
    out = []
    for run in t38_test_runs():
        out.append(("T3.8 measured, 34 test cells", "C-30", run / "transcript-00.ndjson"))
    for root, arms in [("C:/RideBoundData/research/tier3-w07-v1", ["C-30", "C-60", "C-120", "C-300"]),
                       ("C:/RideBoundData/research/tier3-w07x-v1", ["C-90", "C-150", "C-180", "C-240"])]:
        for arm in arms:
            for p in sorted(pathlib.Path(root).glob(f"{arm}-d*-w07/transcript-00.ndjson")):
                out.append(("w07/w07x measured, 8 cells 07-09h", arm, p))
    wp = pathlib.Path("E:/RideBoundData/wp14r/development-v6-ledger")
    for arm, slug in [("C-30", "c1-h6ref"), ("C-60", "c1-budget60"), ("C-120", "c1-budget120")]:
        for p in sorted(wp.glob(f"w14-*-{slug}-s7/attempt-01/output/transcript-00.ndjson")):
            out.append(("WP14r fixed traffic, 16 cells", arm, p))
    for p in sorted(pathlib.Path("E:/RideBoundData/research/tier3-sweep-v1").glob("C-120-*/transcript-00.ndjson")):
        out.append(("synthetic worlds tier3-sweep-v1", "C-120", p))
    return out


def analyze(job):
    population, arm, path = job
    decisions = {}  # hash -> list of pruned budget candidates (class, newRequestIds, witness summary)
    acked = set()
    last_checkpoint = None
    n_decisions = 0
    with open(path, "rb") as fh:
        for line in fh:
            if not line.strip():
                continue
            row = json.loads(line)
            frame = base64.b64decode(row["frameBase64"])
            head = frame[:120]
            if row["direction"] == "adapterToRunner":
                if b'"decisionApplied"' in head:
                    acked.add(json.loads(frame)["payload"]["decisionHash"])
                continue
            if b'"checkpoint"' in head:
                last_checkpoint = frame
                continue
            if b'"decision"' not in head:
                continue
            env = json.loads(frame)
            if env.get("messageType") != "decision":
                continue
            n_decisions += 1
            payload = env["payload"]
            ev = ((payload.get("solver") or {}).get("executionEvidence") or {})
            rows = []
            for pc in ev.get("prunedCandidates", []):
                if pc.get("code") != "COMMITMENT_BUDGET_EXCEEDED":
                    continue
                ws = [w for w in pc.get("commitmentWitnesses", []) if w.get("code") == "COMMITMENT_BUDGET_EXCEEDED"]
                drop = [w for w in ws if w.get("dimension") == "drop_eta_total_ms"]
                other = [w for w in ws if w.get("dimension") != "drop_eta_total_ms"]
                if drop:
                    if any(w["delta"] > w["limit"] for w in drop):
                        cls = "per-step-also"
                    elif all(w["before"] > 0 and w["delta"] <= w["limit"] < w["after"] for w in drop):
                        cls = "cumulative-only"
                    else:
                        cls = "unclassified"
                    detail = [(w["requestId"], w["before"], w["delta"], w["after"], w["limit"]) for w in drop]
                elif other:
                    cls = "lock-like:" + ",".join(sorted({w["dimension"] for w in other}))
                    detail = []
                else:
                    cls = "no-witness"
                    detail = []
                rows.append((cls, tuple(pc.get("newRequestIds", [])), detail))
            decisions[payload["decisionHash"]] = rows
    final = json.loads(last_checkpoint)["payload"]["content"]["onlineState"]
    life = {}
    for r in final["requests"]:
        rid = r.get("requestId") or r.get("id")
        life[rid] = r["lifecycle"]
    cand = collections.Counter()
    decisions_with_cum = 0
    req_cum = set()
    examples = []
    n_acked = 0
    for h, rows in decisions.items():
        if h not in acked:
            continue
        n_acked += 1
        has_cum = False
        for cls, newreq, detail in rows:
            cand[cls] += 1
            if cls == "cumulative-only":
                has_cum = True
                req_cum.update(newreq)
                if len(examples) < 5:
                    examples.append((path.parent.name if path.parent.name != "output" else path.parents[2].name,
                                     newreq, detail))
        decisions_with_cum += has_cum
    served = sum(1 for v in life.values() if v == "completed")
    return {
        "population": population, "arm": arm, "path": str(path), "decisions": n_decisions,
        "acked_decisions": n_acked, "requests": len(life), "served": served, "candidates": dict(cand),
        "decisions_with_cumulative_only": decisions_with_cum,
        "requests_with_cumulative_only_candidate": len(req_cum),
        "of_which_finally_rejected": sum(1 for r in req_cum if life.get(r) == "rejected"),
        "examples": examples,
    }


def main():
    out = sys.argv[1]
    js = jobs()
    with ProcessPoolExecutor(max_workers=4) as pool:
        recs = list(pool.map(analyze, js))
    lines = ["# cumulative_decisive_check.py: could a PER-STEP cap of the same B have decided differently? (existing C-B runs)",
             "# counts of budget-pruned candidates by class; cumulative-only counts are UPPER BOUNDS (fail-fast witnesses)", ""]
    groups = collections.OrderedDict()
    for r in recs:
        groups.setdefault((r["population"], r["arm"]), []).append(r)
    for (pop, arm), rs in groups.items():
        cand = collections.Counter()
        for r in rs:
            cand.update(r["candidates"])
        lines.append(f"## {pop} | {arm} | runs {len(rs)} | requests {sum(r['requests'] for r in rs)} | served {sum(r['served'] for r in rs)} | "
                     f"acknowledged decisions {sum(r['acked_decisions'] for r in rs)} (decision frames {sum(r['decisions'] for r in rs)})")
        lines.append("  budget-pruned candidates by class: " + ", ".join(f"{k} {v}" for k, v in sorted(cand.items())))
        lines.append(f"  decisions with >=1 cumulative-only candidate: {sum(r['decisions_with_cumulative_only'] for r in rs)}; "
                     f"distinct new requests in such candidates: {sum(r['requests_with_cumulative_only_candidate'] for r in rs)}, "
                     f"of which finally rejected: {sum(r['of_which_finally_rejected'] for r in rs)}")
        for r in rs:
            for ex in r["examples"]:
                lines.append(f"    example: run {ex[0]} new {list(ex[1])} witness (rider, before_ms, delta_ms, after_ms, limit_ms) {ex[2]}")
    text = "\n".join(lines) + "\n"
    with open(out, "x", encoding="utf-8", newline="\n") as f:
        f.write(text)
    print(text)


if __name__ == "__main__":
    main()
