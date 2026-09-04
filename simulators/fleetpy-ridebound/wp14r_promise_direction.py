"""Signed direction of promise movement, split by decision versus exogenous.

Why this tool exists as a separate tool.

`wp14_frontier_analyze.py` already splits pickup improvement from worsening, but
it computes that split on the *published* promise sequence with no filter on
`decisionDelta`. A published ETA moves for two unrelated reasons: the decision
inserted work into the route, or the exogenous travel-time projection drifted.
Counting both as "improvement" is correct for describing what a rider saw; it is
wrong for asking whether a *policy* mechanism ever fired.

Factor F2 (`ratchetLocks`) relaxes an ETA lock only for a candidate that makes
the promise EARLIER. Answering "does F2 ever fire?" therefore needs the
decision-induced direction, which the frozen analyzer does not record. The
analyzer is one of the 46 files bound by the WP14 freeze receipt, so it cannot be
widened in place — the same rule that forced `retained-portfolio-full-witness-v1`
to be a new profile rather than a redefinition of the old one. This tool is
additive: it reads the same transcripts and reports the missing split.

Boundaries. Read-only on every input. Refuses to write inside a forbidden root.
Reports association, not causation: `decisionDelta` being non-zero for a
dimension says the decision moved that dimension, not by how much of the
published change.
"""

import argparse
import base64
import collections
import hashlib
import json
import os
import pathlib
import sys

DIMENSIONS = (("pickup", "pickupEtaMs", "pickupEtaTotalMs"),
              ("drop", "dropEtaMs", "dropEtaTotalMs"))

CLAIM_BOUNDARY = (
    "decisionMovedMeansDecisionDeltaNonZeroForThatDimension",
    "publishedDirectionMixesDecisionAndExogenousMagnitude",
    "associationNotCausation",
    "readOnlyOverRetainedEvidence",
    "doesNotModifyOrSupersedeTheFrozenFrontierAnalyzer",
)


class DirectionError(RuntimeError):
    """The tool refuses to produce a report."""


def decisions_in(transcript):
    """Yield every decision payload in one recorded transcript, in order."""
    with transcript.open("r", encoding="utf-8") as handle:
        for ordinal, line in enumerate(handle, start=1):
            line = line.strip()
            if not line:
                continue
            record = json.loads(line)
            if record.get("direction") != "runnerToAdapter":
                continue
            frame = json.loads(
                base64.b64decode(record["frameBase64"], validate=True)
            )
            if frame.get("messageType") != "decision":
                continue
            payload = frame.get("payload")
            if not isinstance(payload, dict):
                raise DirectionError(
                    f"{transcript}: decision {ordinal} carries no payload object"
                )
            yield payload


def scan_bundle(bundle):
    """Return the signed movement counters for one bundle."""
    transcript = bundle / "transcript-00.ndjson"
    if not transcript.is_file():
        raise DirectionError(f"bundle has no transcript: {bundle}")
    previous = {}
    counts = collections.Counter()
    for payload in decisions_in(transcript):
        for action in payload.get("actions") or []:
            if action.get("decisionType") != "promisePublished":
                continue
            body = action["payload"]
            request = body["promise"]["requestId"]
            delta = body.get("decisionDelta") or {}
            current = {
                name: body["promise"][field]
                for name, field, _ in DIMENSIONS
            }
            counts["publications"] += 1
            if request not in previous:
                counts["firstPromises"] += 1
                previous[request] = current
                continue
            counts["revisions"] += 1
            for name, _, budget in DIMENSIONS:
                before = previous[request][name]
                after = current[name]
                moved = "decisionMoved" if delta.get(budget) else "exogenousOnly"
                if after < before:
                    counts[f"{name}.earlier.{moved}"] += 1
                    counts[f"{name}.earlierMs.{moved}"] += before - after
                elif after > before:
                    counts[f"{name}.later.{moved}"] += 1
                    counts[f"{name}.laterMs.{moved}"] += after - before
                else:
                    counts[f"{name}.equal.{moved}"] += 1
            previous[request] = current
    return counts


def summarise(counts):
    """Fold raw counters into the question F2 actually asks."""
    summary = {}
    for name, _, _ in DIMENSIONS:
        earlier = counts[f"{name}.earlier.decisionMoved"]
        later = counts[f"{name}.later.decisionMoved"]
        equal = counts[f"{name}.equal.decisionMoved"]
        summary[name] = {
            "decisionMovedPublications": earlier + later + equal,
            "decisionMovedEarlier": earlier,
            "decisionMovedEarlierMs": counts[f"{name}.earlierMs.decisionMoved"],
            "decisionMovedLater": later,
            "decisionMovedLaterMs": counts[f"{name}.laterMs.decisionMoved"],
            "decisionMovedEqual": equal,
            "exogenousOnlyEarlier": counts[f"{name}.earlier.exogenousOnly"],
            "exogenousOnlyLater": counts[f"{name}.later.exogenousOnly"],
            "ratchetAdmissibleObservations": earlier,
        }
    return summary


def build_report(bundles, label):
    totals = collections.Counter()
    per_bundle = []
    for bundle in bundles:
        counts = scan_bundle(bundle)
        totals += counts
        per_bundle.append({
            "bundle": bundle.name,
            "publications": counts["publications"],
            "revisions": counts["revisions"],
            "summary": summarise(counts),
        })
    summary = summarise(totals)
    admissible = sum(
        summary[name]["ratchetAdmissibleObservations"]
        for name, _, _ in DIMENSIONS
    )
    moved = sum(
        summary[name]["decisionMovedPublications"] for name, _, _ in DIMENSIONS
    )
    return {
        "schemaVersion": "1.0.0",
        "reportType": "ridebound-wp14r-promise-direction-v1",
        "label": label,
        "bundleCount": len(per_bundle),
        "publications": totals["publications"],
        "revisions": totals["revisions"],
        "byDimension": summary,
        "decisionMovedObservations": moved,
        "ratchetAdmissibleObservations": admissible,
        "ratchetInertOnThisEvidence": moved > 0 and admissible == 0,
        "claimBoundary": list(CLAIM_BOUNDARY),
        "perBundle": per_bundle,
        "toolSha256": sha256_file(pathlib.Path(__file__)),
    }


def sha256_file(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def canonical(document):
    return json.dumps(
        document, ensure_ascii=False, sort_keys=True, separators=(",", ":")
    ).encode("utf-8")


def overlaps(first, second):
    first, second = pathlib.Path(first), pathlib.Path(second)
    return first == second or first in second.parents or second in first.parents


def write_exclusive(path, content):
    path = pathlib.Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL | getattr(os, "O_BINARY", 0)
    descriptor = os.open(path, flags, 0o600)
    with os.fdopen(descriptor, "wb") as stream:
        stream.write(content)
        stream.flush()
        os.fsync(stream.fileno())


def collect_bundles(roots, explicit):
    bundles = [pathlib.Path(path).resolve() for path in explicit]
    for root in roots:
        root = pathlib.Path(root).resolve()
        if not root.is_dir():
            raise DirectionError(f"bundle root is not a directory: {root}")
        bundles.extend(
            sorted(
                child for child in root.iterdir()
                if child.is_dir() and (child / "transcript-00.ndjson").is_file()
            )
        )
    if not bundles:
        raise DirectionError("no bundle with a transcript was found")
    return bundles


def build_parser():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--bundle", action="append", default=[])
    parser.add_argument("--bundle-root", action="append", default=[])
    parser.add_argument("--label", required=True)
    parser.add_argument("--output", type=pathlib.Path)
    parser.add_argument("--forbidden-root", action="append", default=[])
    return parser


def main(argv=None):
    arguments = build_parser().parse_args(argv)
    bundles = collect_bundles(arguments.bundle_root, arguments.bundle)
    report = build_report(bundles, arguments.label)
    payload = canonical(report)
    if arguments.output is None:
        print(payload.decode("utf-8"))
        return 0
    output = arguments.output.resolve()
    for root in arguments.forbidden_root:
        if overlaps(output, pathlib.Path(root).resolve()):
            raise DirectionError("report must stay outside every forbidden root")
    for bundle in bundles:
        if overlaps(output, bundle):
            raise DirectionError("report must stay outside every bundle")
    write_exclusive(output, payload)
    print(json.dumps({
        "output": str(output),
        "sha256": hashlib.sha256(payload).hexdigest(),
        "ratchetInertOnThisEvidence": report["ratchetInertOnThisEvidence"],
    }, indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main())
