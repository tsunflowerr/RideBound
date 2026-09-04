#!/usr/bin/env python3
"""Build a paired service-burden frontier from verified WP14 bundles."""

from __future__ import annotations

import argparse
import base64
import collections
import hashlib
import importlib.util
import json
import os
import pathlib
import subprocess
import sys

import jsonschema

sys.dont_write_bytecode = True

SCHEMA_ID = "https://ridebound.local/schemas/wp14/v1/frontier-report.schema.json"
REPORT_TYPE = "ridebound-wp14-paired-frontier-v1"
COMMITMENT_CODES = ("COMMITMENT_BUDGET_EXCEEDED", "COMMITMENT_PHASE_LOCK")
LIFECYCLE_EVENTS = ("requestArrived", "passengerAlighted")
VERIFIER_TIMEOUT_SECONDS = 5 * 60


class FrontierError(RuntimeError):
    """A fail-closed analysis condition."""


def sha256_file(path):
    if not path.is_file():
        raise FrontierError(f"required file not found: {path}")
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def load_module(path, name):
    specification = importlib.util.spec_from_file_location(name, path)
    if specification is None or specification.loader is None:
        raise FrontierError(f"cannot load module: {path}")
    module = importlib.util.module_from_spec(specification)
    specification.loader.exec_module(module)
    return module


def nonnegative_integer(value, label):
    if isinstance(value, bool) or not isinstance(value, int) or value < 0:
        raise FrontierError(f"{label} is not a nonnegative integer")
    return value


def percentile95(ordered):
    """Nearest-rank p95: x[ceil(0.95*n)-1], expressed without floats."""
    if not ordered:
        return 0
    index = (95 * len(ordered) + 99) // 100 - 1
    return ordered[index]


def counter_sum(observations, name):
    result = collections.Counter()
    for observation in observations:
        result.update(observation[name])
    return dict(sorted(result.items()))


def verify_bundle(verifier, python, bundle, environment):
    completed = subprocess.run(
        [
            str(python),
            "-B",
            str(verifier),
            "--bundle",
            str(bundle),
            "--include-behavioral-hash",
            "--require-audited-solver-evidence",
        ],
        check=False,
        capture_output=True,
        text=True,
        encoding="utf-8",
        env=environment,
        timeout=VERIFIER_TIMEOUT_SECONDS,
    )
    if completed.returncode != 0:
        detail = (completed.stderr or completed.stdout or "").strip()
        raise FrontierError(f"bundle verification failed for {bundle}: {detail}")


def read_bundle(bundle, job, verifier, python, environment):
    """Verify and measure one bundle, reading its transcript to EOF."""
    verify_bundle(verifier, python, bundle, environment)
    summary_path = bundle / "summary.json"
    summary = json.loads(summary_path.read_text(encoding="utf-8"))
    if (
        summary.get("status") != "pass"
        or summary.get("label") != job["jobId"]
        or summary.get("repeatCount") != 1
        or summary.get("sourceScenarioContentSha256")
        != job["scenarioContentSha256"]
    ):
        raise FrontierError(f"bundle does not bind to its frozen job: {bundle}")
    repository_inventory = summary.get("repositoryInventorySha256")
    if (
        not isinstance(repository_inventory, str)
        or len(repository_inventory) != 64
        or any(
            character not in "0123456789abcdef"
            for character in repository_inventory
        )
    ):
        raise FrontierError(f"bundle repository inventory is invalid: {bundle}")

    lifecycle = dict.fromkeys(LIFECYCLE_EVENTS, 0)
    burden = collections.Counter()
    exogenous = collections.Counter()
    pruned = collections.Counter()
    prune_dimensions = collections.Counter()
    consumption = {}
    previous = {}
    experienced = collections.Counter()
    pickup_improvement_count = 0
    pickup_improvement_ms = 0
    pickup_worsening_count = 0
    pickup_worsening_ms = 0
    disruptive = 0
    decisions = 0

    transcript = bundle / "transcript-00.ndjson"
    with transcript.open("r", encoding="utf-8") as stream:
        for line_number, line in enumerate(stream, start=1):
            envelope = json.loads(line)
            frame = json.loads(
                base64.b64decode(envelope["frameBase64"], validate=True).decode(
                    "utf-8"
                )
            )
            kind = frame.get("messageType")
            if kind == "eventBatch":
                for event in frame["payload"]["events"]:
                    if event["eventType"] in lifecycle:
                        lifecycle[event["eventType"]] += 1
                continue
            if kind != "decision":
                continue

            decisions += 1
            payload = frame["payload"]
            evidence = (payload.get("solver") or {}).get("executionEvidence") or {}
            for entry in evidence.get("prunedCandidates") or []:
                if entry["code"] not in COMMITMENT_CODES:
                    continue
                pruned[entry["code"]] += 1
                for witness in entry.get("commitmentWitnesses") or []:
                    dimension = witness.get("dimension")
                    if dimension:
                        prune_dimensions[dimension] += 1

            frame_disruptive = False
            for action in payload["actions"]:
                if action.get("decisionType") != "promisePublished":
                    continue
                body = action["payload"]
                decision = body["decisionDelta"]
                for name in ("pickupEtaTotalMs", "dropEtaTotalMs"):
                    burden[name] += nonnegative_integer(
                        decision[name], f"line {line_number} decisionDelta.{name}"
                    )
                    exogenous[name] += nonnegative_integer(
                        body["exogenousDelta"][name],
                        f"line {line_number} exogenousDelta.{name}",
                    )
                frame_disruptive = frame_disruptive or any(
                    nonnegative_integer(
                        value,
                        f"line {line_number} decisionDelta.{dimension}",
                    )
                    != 0
                    for dimension, value in decision.items()
                )

                request = body["promise"]["requestId"]
                pickup = nonnegative_integer(
                    body["promise"]["pickupEtaMs"],
                    f"line {line_number} pickupEtaMs",
                )
                drop = nonnegative_integer(
                    body["promise"]["dropEtaMs"],
                    f"line {line_number} dropEtaMs",
                )
                if request in previous:
                    before_pickup, before_drop = previous[request]
                    pickup_change = pickup - before_pickup
                    experienced["pickupEtaTotalMs"] += abs(pickup_change)
                    experienced["dropEtaTotalMs"] += abs(drop - before_drop)
                    if pickup_change < 0:
                        pickup_improvement_count += 1
                        pickup_improvement_ms += -pickup_change
                    elif pickup_change > 0:
                        pickup_worsening_count += 1
                        pickup_worsening_ms += pickup_change
                previous[request] = (pickup, drop)
                drop_consumption = nonnegative_integer(
                    body["budgetAfter"]["dropEtaTotalMs"],
                    f"line {line_number} budgetAfter.dropEtaTotalMs",
                )
                consumption[request] = max(
                    consumption.get(request, 0), drop_consumption
                )
            if frame_disruptive:
                disruptive += 1

    arrived = lifecycle["requestArrived"]
    completed = lifecycle["passengerAlighted"]
    if arrived != 108 or completed > arrived:
        raise FrontierError(f"bundle denominator/completion is invalid: {bundle}")

    values = sorted(consumption.values())
    return {
        "arrived": arrived,
        "completed": completed,
        "decisions": decisions,
        "attributedPickupMs": burden["pickupEtaTotalMs"],
        "attributedDropMs": burden["dropEtaTotalMs"],
        "attributedTotalMs": sum(burden.values()),
        "exogenousTotalMs": sum(exogenous.values()),
        "experiencedTotalMs": sum(experienced.values()),
        "pickupEtaImprovementCount": pickup_improvement_count,
        "pickupEtaImprovementMs": pickup_improvement_ms,
        "pickupEtaWorseningCount": pickup_worsening_count,
        "pickupEtaWorseningMs": pickup_worsening_ms,
        "disruptiveDecisions": disruptive,
        "commitmentPrunedByCode": dict(sorted(pruned.items())),
        "commitmentPrunedByDimension": dict(sorted(prune_dimensions.items())),
        "ridersWithOpenPromise": len(consumption),
        "ridersCharged": sum(1 for value in values if value > 0),
        "riderDropConsumptionP95Ms": percentile95(values),
        "riderDropConsumptionMaxMs": values[-1] if values else 0,
        "semanticHash": summary["semanticHash"],
        "repositoryInventorySha256": repository_inventory,
        "_riderDropConsumptionValuesMs": values,
    }


def public_observation(observation):
    return {
        key: value
        for key, value in observation.items()
        if not key.startswith("_")
    }


def analyze(receipt, output_root, verifier, python):
    by_arm = collections.defaultdict(dict)
    seen_jobs = set()
    environment = dict(os.environ)
    environment["PYTHONDONTWRITEBYTECODE"] = "1"
    for job in receipt["design"]["jobs"]:
        key = (job["armId"], job["cellId"])
        if job["jobId"] in seen_jobs or job["cellId"] in by_arm[job["armId"]]:
            raise FrontierError(f"duplicate job or arm-cell pair: {job['jobId']}")
        seen_jobs.add(job["jobId"])
        bundle = output_root / job["jobId"]
        if not bundle.is_dir():
            raise FrontierError(f"job has not been executed: {job['jobId']}")
        by_arm[key[0]][key[1]] = read_bundle(
            bundle, job, verifier, python, environment
        )

    jobs = receipt["design"]["jobs"]
    cells = sorted({job["cellId"] for job in jobs})
    arms = [arm["armId"] for arm in receipt["design"]["arms"]]
    if len(cells) != 16 or len(arms) != 10 or len(set(arms)) != 10:
        raise FrontierError("freeze does not contain the exact 16-cell/10-arm design")
    for arm in arms:
        if set(by_arm[arm]) != set(cells):
            raise FrontierError(f"arm {arm} does not contain the exact cell set")

    reference = "c1-h6ref"
    baseline = "b1-ref"
    if reference not in by_arm or baseline not in by_arm:
        raise FrontierError("the frontier needs baseline and H6-reference arms")
    baseline_completed = sum(
        by_arm[baseline][cell]["completed"] for cell in cells
    )
    reference_completed = sum(
        by_arm[reference][cell]["completed"] for cell in cells
    )
    baseline_burden = sum(
        by_arm[baseline][cell]["attributedTotalMs"] for cell in cells
    )
    reference_burden = sum(
        by_arm[reference][cell]["attributedTotalMs"] for cell in cells
    )

    points = []
    for arm in arms:
        observations = [by_arm[arm][cell] for cell in cells]
        arrived = sum(value["arrived"] for value in observations)
        completed = sum(value["completed"] for value in observations)
        if arrived != receipt["design"]["arrivalsPerArm"]:
            raise FrontierError(f"arm {arm} denominator differs from freeze")
        baseline_deltas = sorted(
            by_arm[arm][cell]["completed"]
            - by_arm[baseline][cell]["completed"]
            for cell in cells
        )
        rider_values = sorted(
            value
            for observation in observations
            for value in observation["_riderDropConsumptionValuesMs"]
        )
        burden = sum(value["attributedTotalMs"] for value in observations)
        points.append(
            {
                "armId": arm,
                "factorLevel": next(
                    entry["factorLevel"]
                    for entry in receipt["design"]["arms"]
                    if entry["armId"] == arm
                ),
                "arrived": arrived,
                "completed": completed,
                "completionRatePartsPerMillionFloor": (
                    completed * 1_000_000 // arrived
                ),
                "completedDeltaVersusBaseline": completed - baseline_completed,
                "completedDeltaVersusReference": completed - reference_completed,
                "perCellCompletedDeltaVersusBaseline": {
                    cell: by_arm[arm][cell]["completed"]
                    - by_arm[baseline][cell]["completed"]
                    for cell in cells
                },
                "perCellCompletedDeltaVersusReference": {
                    cell: by_arm[arm][cell]["completed"]
                    - by_arm[reference][cell]["completed"]
                    for cell in cells
                },
                "medianCellDeltaVersusBaselineTimesTwo": (
                    baseline_deltas[7] + baseline_deltas[8]
                ),
                "attributedBurdenMs": burden,
                "attributedBurdenDeltaVersusBaselineMs": (
                    burden - baseline_burden
                ),
                "attributedBurdenDeltaVersusReferenceMs": (
                    burden - reference_burden
                ),
                "experiencedMovementMs": sum(
                    value["experiencedTotalMs"] for value in observations
                ),
                "pickupEtaImprovementCount": sum(
                    value["pickupEtaImprovementCount"] for value in observations
                ),
                "pickupEtaImprovementMs": sum(
                    value["pickupEtaImprovementMs"] for value in observations
                ),
                "pickupEtaWorseningCount": sum(
                    value["pickupEtaWorseningCount"] for value in observations
                ),
                "pickupEtaWorseningMs": sum(
                    value["pickupEtaWorseningMs"] for value in observations
                ),
                "disruptiveDecisions": sum(
                    value["disruptiveDecisions"] for value in observations
                ),
                "commitmentPrunedByCode": counter_sum(
                    observations, "commitmentPrunedByCode"
                ),
                "commitmentPrunedByDimension": counter_sum(
                    observations, "commitmentPrunedByDimension"
                ),
                "ridersWithOpenPromise": sum(
                    value["ridersWithOpenPromise"] for value in observations
                ),
                "ridersCharged": sum(
                    value["ridersCharged"] for value in observations
                ),
                "riderDropConsumptionP95Ms": percentile95(rider_values),
                "riderDropConsumptionMaxMs": (
                    rider_values[-1] if rider_values else 0
                ),
                "worstCellP95RiderDropConsumptionMs": max(
                    value["riderDropConsumptionP95Ms"]
                    for value in observations
                ),
            }
        )

    for point in points:
        dominators = sorted(
            other["armId"]
            for other in points
            if other["armId"] != point["armId"]
            and other["completed"] >= point["completed"]
            and other["attributedBurdenMs"] <= point["attributedBurdenMs"]
            and (
                other["completed"] > point["completed"]
                or other["attributedBurdenMs"] < point["attributedBurdenMs"]
            )
        )
        point["isParetoEfficient"] = not dominators
        point["dominatedByArmIds"] = dominators

    return {
        "schemaVersion": "1.0.0",
        "schemaId": SCHEMA_ID,
        "reportType": REPORT_TYPE,
        "freezeId": receipt["freezeId"],
        "claimBoundary": receipt["claimBoundary"]
        + [
            "frontierNotARanking",
            "noPostOutcomeScalarSelection",
            "perRiderTailReportedBesideFleetTotals",
        ],
        "design": {
            "cellCount": len(cells),
            "armCount": len(arms),
            "bundleCount": len(seen_jobs),
            "arrivalsPerArm": receipt["design"]["arrivalsPerArm"],
            "cells": cells,
            "baselineArmId": baseline,
            "referenceArmId": reference,
            "frontierAxes": {
                "service": "completedMaximize",
                "burden": "attributedDecisionInducedMillisecondsMinimize",
            },
            "dominanceRule": "weakOnBothAxesAndStrictOnAtLeastOne",
        },
        "points": sorted(points, key=lambda value: value["armId"]),
        "perCell": {
            arm: {
                cell: public_observation(by_arm[arm][cell]) for cell in cells
            }
            for arm in sorted(arms)
        },
        "verification": {
            "verifiedBundleCount": len(seen_jobs),
            "duplicateJobCount": 0,
            "duplicateArmCellCount": 0,
            "repositoryInventorySha256es": sorted(
                {
                    by_arm[arm][cell]["repositoryInventorySha256"]
                    for arm in arms
                    for cell in cells
                }
            ),
            "status": "pass",
        },
    }


def canonical(document):
    return json.dumps(
        document, ensure_ascii=False, sort_keys=True, separators=(",", ":")
    ).encode("utf-8")


def write_exclusive(output, payload):
    output.parent.mkdir(parents=True, exist_ok=True)
    flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL
    flags |= getattr(os, "O_BINARY", 0)
    descriptor = os.open(output, flags)
    try:
        os.write(descriptor, payload)
    finally:
        os.close(descriptor)


def overlaps(first, second):
    return first == second or first in second.parents or second in first.parents


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repository", required=True, type=pathlib.Path)
    parser.add_argument("--freeze", required=True, type=pathlib.Path)
    parser.add_argument("--output-root", required=True, type=pathlib.Path)
    parser.add_argument(
        "--forbidden-root", action="append", required=True, type=pathlib.Path
    )
    parser.add_argument("--runner-root", required=True, type=pathlib.Path)
    parser.add_argument("--fleetpy-root", required=True, type=pathlib.Path)
    parser.add_argument("--python", required=True, type=pathlib.Path)
    parser.add_argument("--dotnet", required=True, type=pathlib.Path)
    parser.add_argument(
        "--development-panel-audit", required=True, type=pathlib.Path
    )
    parser.add_argument(
        "--resource-planning-evidence", required=True, type=pathlib.Path
    )
    parser.add_argument("--output", required=True, type=pathlib.Path)
    arguments = parser.parse_args(argv)
    try:
        repository = arguments.repository.resolve()
        freeze_path = arguments.freeze.resolve()
        output_root = arguments.output_root.resolve()
        output = arguments.output.resolve()
        forbidden = [root.resolve() for root in arguments.forbidden_root]
        if output.exists():
            raise FrontierError("frontier report already exists")
        if overlaps(output, output_root):
            raise FrontierError("frontier report must stay outside the bundle root")
        if any(overlaps(output, root) for root in forbidden):
            raise FrontierError("frontier report overlaps a frozen H6/E1 root")

        receipt = json.loads(freeze_path.read_text(encoding="utf-8"))
        freeze = load_module(
            repository / "simulators/fleetpy-ridebound/wp14_freeze.py",
            "ridebound_wp14_freeze_for_frontier",
        )
        freeze.verify_receipt(
            freeze_path,
            repository,
            output_root,
            forbidden,
            freeze.MAXIMUM_PARALLEL_JOBS,
            arguments.runner_root.resolve(),
            arguments.fleetpy_root.resolve(),
            arguments.python.resolve(),
            arguments.dotnet.resolve(),
            arguments.development_panel_audit.resolve(),
            arguments.resource_planning_evidence.resolve(),
        )
        verifier = (
            repository
            / "simulators/fleetpy-ridebound/actual_fleetpy_medium_verify.py"
        )
        report = analyze(
            receipt, output_root, verifier, arguments.python.resolve()
        )
        report["freezeReceiptSha256"] = sha256_file(freeze_path)
        report["sourceIdentity"] = {
            "analyzerSourceSha256": sha256_file(
                repository / "simulators/fleetpy-ridebound/wp14_frontier_analyze.py"
            ),
            "independentVerifierSourceSha256": sha256_file(verifier),
        }
        schema = json.loads(
            (
                repository
                / "benchmarks/schemas/wp14/v1/frontier-report.schema.json"
            ).read_text(encoding="utf-8")
        )
        jsonschema.Draft202012Validator(schema).validate(report)
        encoded = canonical(report) + b"\n"
        write_exclusive(output, encoded)
        print(f"{output} {len(encoded)} {hashlib.sha256(encoded).hexdigest()}")
    except (
        OSError,
        FrontierError,
        ValueError,
        TypeError,
        KeyError,
        StopIteration,
        subprocess.SubprocessError,
        jsonschema.ValidationError,
    ) as error:
        print(f"wp14_frontier_analyze: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
