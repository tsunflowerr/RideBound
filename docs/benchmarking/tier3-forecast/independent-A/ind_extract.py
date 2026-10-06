"""Independent verifier, Family A -- step 1: extract per-rider drop histories from raw transcripts.

Written from the task brief and METRICS-SPEC.md only. Reads each transcript-00.ndjson, verifies
frameSha256, takes the LAST runnerToAdapter checkpoint's onlineState (commitmentLedger, requests),
and collects passengerAlighted events (adapterToRunner eventBatch; time = envelope.simTimeMs).

Output: extracted/<ARM>-<CELL>.json (new files only, inside this folder).
"""
import base64
import collections
import hashlib
import json
import os
import sys
from multiprocessing import Pool

RUNS = r"C:\RideBoundData\research\tier3-t38-v1"
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "extracted")

DAYS_W = {
    "w07": ["d20181114-s10-r3", "d20181114-s10-r4", "d20181115-s10-r1", "d20181115-s10-r2",
            "d20181115-s10-r4", "d20181116-s10-r1", "d20181116-s10-r2", "d20181116-s10-r4"],
    "w17": ["d20181114-s10-r3", "d20181114-s10-r4", "d20181116-s10-r1", "d20181116-s10-r2",
            "d20181116-s10-r4"],
}
DAYS_W["w08"] = list(DAYS_W["w07"])
CELLS = [f"{dr}-{w}" for w in ("w07", "w08", "w17") for dr in DAYS_W[w]]
ARMS = ["C-30", "U"]


def run_folder(arm, cell):
    name = f"{arm}-{cell}"
    folder = os.path.join(RUNS, name)
    if not os.path.exists(os.path.join(folder, "summary.json")):
        alt = os.path.join(RUNS, name + "-rerun1")
        return alt, True
    return folder, False


def extract(job, folder_override=None):
    arm, cell = job
    if folder_override:
        folder, used_rerun = folder_override, True
    else:
        folder, used_rerun = run_folder(arm, cell)
    path = os.path.join(folder, "transcript-00.ndjson")
    last_cp = None
    alights = collections.defaultdict(list)
    n_frames = 0
    bad_sha = 0
    with open(path, "r", encoding="utf-8") as fh:
        for line in fh:
            if not line.strip():
                continue
            rec = json.loads(line)
            raw = base64.b64decode(rec["frameBase64"])
            n_frames += 1
            if hashlib.sha256(raw).hexdigest() != rec.get("frameSha256"):
                bad_sha += 1
            d = rec["direction"]
            if d == "runnerToAdapter":
                # cheap pre-filter, then parse
                env = json.loads(raw)
                if env.get("messageType") == "checkpoint":
                    last_cp = env
            elif d == "adapterToRunner":
                env = json.loads(raw)
                if env.get("messageType") == "eventBatch":
                    for ev in env.get("payload", {}).get("events", []):
                        if ev.get("eventType") == "passengerAlighted":
                            alights[ev["payload"]["requestId"]].append(env["simTimeMs"])
    out = {"arm": arm, "cell": cell, "folder": folder, "usedRerun": used_rerun,
           "frames": n_frames, "badSha": bad_sha, "finalCheckpoint": last_cp is not None}
    if last_cp is None:
        return out
    st = last_cp["payload"]["content"]["onlineState"]
    reqs = {r["requestId"]: r for r in st["requests"]}
    riders = []
    seen = set()
    dup = 0
    for h in st["commitmentLedger"]:
        rid = h["requestId"]
        if rid in seen:
            dup += 1
        seen.add(rid)
        ents = []
        for i, e in enumerate(h["entries"]):
            pp = e["publishedPromise"]
            row = {"kind": e["kind"], "t": pp["publishedAtMs"], "p": pp["projection"]["dropEtaMs"]}
            if i > 0:
                row["e"] = e["exogenousProjection"]["dropEtaMs"]
                row["v"] = e["previousPromise"]["projection"]["dropEtaMs"]
            ents.append(row)
        a = alights.get(rid)
        riders.append({"requestId": rid, "entries": ents,
                       "A": a[0] if a else None, "nAlight": len(a) if a else 0,
                       "lifecycle": reqs.get(rid, {}).get("lifecycle")})
    out.update({
        "checkpointSimTimeMs": last_cp["payload"]["content"].get("simTimeMs"),
        "nRequests": len(st["requests"]),
        "lifecycles": dict(collections.Counter(r["lifecycle"] for r in st["requests"])),
        "dupLedgerIds": dup,
        "alightedNotInLedger": sum(1 for r in alights if r not in seen),
        "riders": riders,
    })
    return out


def write(res, fname):
    with open(os.path.join(OUT, fname), "w", encoding="utf-8") as fh:
        json.dump(res, fh)


def main():
    os.makedirs(OUT, exist_ok=True)
    jobs = [(a, c) for a in ARMS for c in CELLS]
    assert len(CELLS) == 21
    with Pool(6) as pool:
        for res in pool.imap_unordered(extract, jobs):
            write(res, f"{res['arm']}-{res['cell']}.json")
            print(res["arm"], res["cell"], "rerun" if res["usedRerun"] else "", "frames", res["frames"],
                  "badSha", res["badSha"], "cp", res["finalCheckpoint"], "riders", len(res.get("riders", [])),
                  flush=True)
    # sensitivity: the original (non-rerun) folder of the one affected cell, if it has a final checkpoint
    orig = os.path.join(RUNS, "C-30-d20181114-s10-r3-w17")
    res = extract(("C-30", "d20181114-s10-r3-w17"), folder_override=orig)
    res["usedRerun"] = False
    write(res, "ORIGINAL-C-30-d20181114-s10-r3-w17.json")
    print("original C-30-d20181114-s10-r3-w17 frames", res["frames"], "cp", res["finalCheckpoint"],
          "riders", len(res.get("riders", [])))


if __name__ == "__main__":
    sys.exit(main())
