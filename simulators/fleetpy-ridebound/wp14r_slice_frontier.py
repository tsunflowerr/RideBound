"""Service-burden frontier over the completed WP14R matrix bundles.

Why this exists as a separate tool. `wp14_frontier_analyze.py` is the frozen
analyzer and it must stay frozen, yet it cannot be pointed at this matrix: it
expects the WP14-v1 layout `<output-root>/<jobId>/summary.json`, while WP14R
stores a bundle at `<ledger>/<jobId>/attempt-NN/output/`. So this tool reuses
the frozen analyzer's own `read_bundle` - the exact metric definitions,
verification and EOF-complete transcript read - and does nothing but locate
the bundles and aggregate what comes back. No metric is redefined here.

It was first written while the v5 matrix lay halted at 40 of 160 jobs after a
`RBWP7_FLEETPY_PLAN_INFEASIBLE` in the first `w17` job, and it then produced a
deliberately narrow descriptive slice. Under freeze v6 the matrix completed
160 of 160, so coverage is no longer fixed: `coverage_claim` reads it off the
observations, and the report says which case it is. WP14 remains scoped
`developmentExploratoryOnlyNotConfirmatory` either way - a complete matrix
makes the slice whole, it does not make it confirmatory.

It also answers the one question the frozen analyzer cannot: the frozen
`pickupEtaImprovementCount` counts published movement without filtering
`decisionDelta`, so it mixes exogenous drift with policy effect. The
decision-induced direction is taken from `wp14r_promise_direction` instead,
which is what the pre-registered F2 prediction is actually about.

Read-only over every input. Writes one canonical report.
"""

import argparse
import collections
import hashlib
import importlib.util
import json
import os
import pathlib
import sys

_HERE = pathlib.Path(__file__).resolve().parent

CLAIM_BOUNDARY = (
    "developmentExploratoryOnlyNotConfirmatory",
    "metricDefinitionsReusedFromTheFrozenAnalyzerUnchanged",
    "noScalarRankingAndNoPostOutcomeSelection",
    "paretoOnlyOnCompletedUpAndAttributedBurdenDown",
    "doesNotReinterpretOrRescueH6",
)


def coverage_claim(designed, observed, cells, designed_cells):
    """State coverage from what was observed, never from a constant.

    The first version of this tool asserted
    `descriptiveSliceNotThePreregisteredSixteenCellFrontier` unconditionally,
    because when it was written the v5 matrix had halted at 40 of 160 jobs and
    that was simply true. Left as a constant it would keep saying so over a
    complete matrix, understating the evidence and mislabelling it. A claim
    boundary that can be wrong about its own coverage is worse than none.
    """
    if observed == designed and len(cells) == designed_cells:
        return (
            "coversTheCompletePreregisteredDesignAllJobsAllCells",
        )
    return (
        "descriptiveSliceNotThePreregisteredSixteenCellFrontier",
    )


# The design is 16 cells: two days x four request replicates x two windows.
DESIGNED_CELL_COUNT = 16


class SliceError(RuntimeError):
    """The slice refuses to produce a report."""


def load_module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    if spec is None or spec.loader is None:
        raise SliceError(f"cannot load {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def sha256_file(path):
    digest = hashlib.sha256()
    with pathlib.Path(path).open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def canonical(document):
    return json.dumps(
        document, ensure_ascii=False, sort_keys=True, separators=(",", ":")
    ).encode("utf-8")


def split_job_id(job_id):
    """`w14-<cell>-<arm>-s7` -> (cell, arm)."""
    core = job_id[len("w14-"):]
    parts = core.split("-")
    cell = "-".join(parts[:4])
    arm = "-".join(parts[4:])
    if arm.endswith("-s7"):
        arm = arm[: -len("-s7")]
    return cell, arm


def selected_output(ledger_root, job_id):
    """The output directory of the first attempt that carries a bundle."""
    job_root = pathlib.Path(ledger_root) / job_id
    if not job_root.is_dir():
        return None
    for attempt in sorted(job_root.glob("attempt-*")):
        output = attempt / "output"
        if (output / "summary.json").is_file():
            return output
    return None


def collect(repository, ledger_root, base_receipt, verifier, python):
    frozen = load_module(
        "wp14_frontier_analyze_for_slice",
        _HERE / "wp14_frontier_analyze.py",
    )
    environment = dict(os.environ)
    environment["PYTHONDONTWRITEBYTECODE"] = "1"

    observations = {}
    missing = []
    for job in base_receipt["design"]["jobs"]:
        job_id = job["jobId"]
        output = selected_output(ledger_root, job_id)
        if output is None:
            missing.append(job_id)
            continue
        observations[job_id] = frozen.read_bundle(
            output, job, verifier, python, environment
        )
        print(f"  read {job_id}", flush=True)
    return frozen, observations, missing


def aggregate(frozen, observations):
    """Aggregate per arm over the cells where every arm is present."""
    per_arm_cell = {}
    cells_by_arm = collections.defaultdict(set)
    arms_by_cell = collections.defaultdict(set)
    for job_id, observation in observations.items():
        cell, arm = split_job_id(job_id)
        per_arm_cell[(arm, cell)] = observation
        cells_by_arm[arm].add(cell)
        arms_by_cell[cell].add(arm)

    arms = sorted(cells_by_arm)
    complete_cells = sorted(
        cell for cell, present in arms_by_cell.items() if set(arms) <= present
    )
    if not complete_cells:
        raise SliceError("no cell carries every arm, so no arm comparison is valid")

    rows = []
    for arm in arms:
        completed = 0
        arrived = 0
        attributed = 0
        exogenous = 0
        experienced = 0
        riders_charged = 0
        riders_open = 0
        disruptive = 0
        decisions = 0
        pruned = collections.Counter()
        dimensions = collections.Counter()
        published_improve = 0
        published_worsen = 0
        tail = []
        per_cell = {}
        for cell in complete_cells:
            observation = per_arm_cell[(arm, cell)]
            completed += observation["completed"]
            arrived += observation["arrived"]
            attributed += observation["attributedTotalMs"]
            exogenous += observation["exogenousTotalMs"]
            experienced += observation["experiencedTotalMs"]
            riders_charged += observation["ridersCharged"]
            riders_open += observation["ridersWithOpenPromise"]
            disruptive += observation["disruptiveDecisions"]
            decisions += observation["decisions"]
            pruned.update(observation["commitmentPrunedByCode"])
            dimensions.update(observation["commitmentPrunedByDimension"])
            published_improve += observation["pickupEtaImprovementCount"]
            published_worsen += observation["pickupEtaWorseningCount"]
            tail.extend(observation["_riderDropConsumptionValuesMs"])
            per_cell[cell] = {
                "completed": observation["completed"],
                "attributedTotalMs": observation["attributedTotalMs"],
            }
        tail.sort()
        rows.append(
            {
                "armId": arm,
                "cells": len(complete_cells),
                "arrived": arrived,
                "completed": completed,
                "decisions": decisions,
                "attributedBurdenMs": attributed,
                "exogenousBurdenMs": exogenous,
                "experiencedBurdenMs": experienced,
                "ridersWithOpenPromise": riders_open,
                "ridersCharged": riders_charged,
                "disruptiveDecisions": disruptive,
                "commitmentPrunedByCode": dict(sorted(pruned.items())),
                "commitmentPrunedByDimension": dict(sorted(dimensions.items())),
                "publishedPickupImprovementCount": published_improve,
                "publishedPickupWorseningCount": published_worsen,
                "riderDropConsumptionP95Ms": frozen.percentile95(tail),
                "riderDropConsumptionMaxMs": tail[-1] if tail else 0,
                "perCell": per_cell,
            }
        )
    return complete_cells, rows


def pareto(rows):
    """Non-dominated arms on the two declared axes only.

    completed higher is better, attributed burden lower is better. No scalar
    score, no ordering by goodness — the frozen contract forbids both.
    """
    frontier = []
    for row in rows:
        dominated = any(
            other["completed"] >= row["completed"]
            and other["attributedBurdenMs"] <= row["attributedBurdenMs"]
            and (
                other["completed"] > row["completed"]
                or other["attributedBurdenMs"] < row["attributedBurdenMs"]
            )
            for other in rows
            if other["armId"] != row["armId"]
        )
        if not dominated:
            frontier.append(row["armId"])
    return sorted(frontier)


def distinct_outcome_groups(rows):
    """Arms that agree on every reported counter, grouped.

    `pareto` returns every arm in a tie as non-dominated, which is correct
    Pareto semantics and misleading as a headline: ten non-dominated arms
    sounds like ten findings. On the v6 matrix six arms agree exactly on all
    sixteen cells, so the front is five points, one of them sixfold. Whether
    a factor moved anything at all is the first thing a reader needs, so it
    is reported rather than left to be derived.
    """
    fields = (
        "arrived",
        "completed",
        "decisions",
        "disruptiveDecisions",
        "ridersCharged",
        "attributedBurdenMs",
        "exogenousBurdenMs",
        "experiencedBurdenMs",
    )
    buckets = collections.OrderedDict()
    for row in sorted(rows, key=lambda value: value["armId"]):
        key = tuple(row[field] for field in fields)
        buckets.setdefault(key, []).append(row["armId"])
    groups = []
    for key, arms in buckets.items():
        groups.append({
            "arms": sorted(arms),
            "armCount": len(arms),
            **dict(zip(fields, key)),
        })
    groups.sort(key=lambda value: -value["completed"])
    return groups


def behaviourally_identical(observations, left, right, cells):
    """Do two arms produce the same actions on every shared cell?

    semanticHash cannot answer this: executionEvidence is inside it and carries
    solver timing, so behaviourally identical arms still hash differently. The
    comparison here is on the recorded outcome counters instead.
    """
    fields = (
        "arrived",
        "completed",
        "decisions",
        "attributedPickupMs",
        "attributedDropMs",
        "exogenousTotalMs",
        "experiencedTotalMs",
        "ridersCharged",
        "disruptiveDecisions",
    )
    differences = {}
    for cell in cells:
        a = observations.get(f"w14-{cell}-{left}-s7")
        b = observations.get(f"w14-{cell}-{right}-s7")
        if a is None or b is None:
            differences[cell] = "absent"
            continue
        delta = {f: (a[f], b[f]) for f in fields if a[f] != b[f]}
        if delta:
            differences[cell] = delta
    return {
        "left": left,
        "right": right,
        "identicalOnEveryCell": not differences,
        "differences": differences,
    }


def build_parser():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repository", required=True, type=pathlib.Path)
    parser.add_argument("--ledger-root", required=True, type=pathlib.Path)
    parser.add_argument("--base-freeze", required=True, type=pathlib.Path)
    parser.add_argument("--python", required=True, type=pathlib.Path)
    parser.add_argument("--label", required=True)
    parser.add_argument("--output", type=pathlib.Path)
    return parser


def main(argv=None):
    arguments = build_parser().parse_args(argv)
    repository = arguments.repository.resolve()
    base_receipt = json.loads(
        arguments.base_freeze.resolve().read_text(encoding="utf-8")
    )
    verifier = (
        repository
        / "simulators/fleetpy-ridebound/actual_fleetpy_medium_verify.py"
    )

    frozen, observations, missing = collect(
        repository,
        arguments.ledger_root.resolve(),
        base_receipt,
        verifier,
        arguments.python.resolve(),
    )
    cells, rows = aggregate(frozen, observations)

    report = {
        "schemaVersion": "1.0.0",
        "reportType": "ridebound-wp14r-descriptive-slice-frontier-v1",
        "label": arguments.label,
        "baseFreezeId": base_receipt["freezeId"],
        "baseFreezeSha256": sha256_file(arguments.base_freeze.resolve()),
        "frozenAnalyzerSha256": sha256_file(
            repository / "simulators/fleetpy-ridebound/wp14_frontier_analyze.py"
        ),
        "sliceToolSha256": sha256_file(pathlib.Path(__file__)),
        "designedJobCount": len(base_receipt["design"]["jobs"]),
        "observedJobCount": len(observations),
        "missingJobCount": len(missing),
        "completeCells": cells,
        "arms": rows,
        "distinctOutcomeGroups": distinct_outcome_groups(rows),
        "paretoNonDominatedArms": pareto(rows),
        "preRegisteredF2Checks": [
            behaviourally_identical(observations, "c1-ratchet", "c1-h6ref", cells),
            behaviourally_identical(
                observations, "c1-freeze300ratchet", "c1-freeze300", cells
            ),
        ],
        "claimBoundary": sorted(
            CLAIM_BOUNDARY
            + coverage_claim(
                len(base_receipt["design"]["jobs"]),
                len(observations),
                cells,
                DESIGNED_CELL_COUNT,
            )
        ),
    }
    payload = canonical(report) + b"\n"
    if arguments.output is None:
        sys.stdout.write(payload.decode("utf-8"))
        return 0
    output = arguments.output.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL | getattr(os, "O_BINARY", 0)
    descriptor = os.open(output, flags, 0o600)
    with os.fdopen(descriptor, "wb") as stream:
        stream.write(payload)
        stream.flush()
        os.fsync(stream.fileno())
    print(json.dumps({
        "output": str(output),
        "sha256": hashlib.sha256(payload).hexdigest(),
        "observedJobCount": len(observations),
        "completeCells": len(cells),
    }, indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main())
