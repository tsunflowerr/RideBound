"""Deterministic finite-panel analysis for the preregistered WP9 FleetPy study.

The independent bundle verifier is always run before metrics are read.  This
module does not implement RideBound decisions; it aggregates protocol evidence
produced by the versioned Runner.
"""

from __future__ import annotations

import argparse
import base64
import hashlib
import importlib.util
import json
import pathlib


_EVENTS = ("requestArrived", "passengerAlighted")


def _canonical(value):
    return json.dumps(
        value,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    )


def _load_verifier(adapter_root):
    path = adapter_root / "actual_fleetpy_medium_verify.py"
    specification = importlib.util.spec_from_file_location(
        "ridebound_wp9_independent_verifier",
        path,
    )
    if specification is None or specification.loader is None:
        raise RuntimeError("cannot load the independent FleetPy verifier")
    module = importlib.util.module_from_spec(specification)
    specification.loader.exec_module(module)
    return module


def _nonnegative_integer(value, field):
    if isinstance(value, bool) or not isinstance(value, int) or value < 0:
        raise RuntimeError(f"{field} must be a non-negative integer")
    return value


def _sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _read_observation(
    bundle,
    verifier,
    require_audited_solver_evidence,
    expected_label=None,
    expected_source_scenario_sha256=None,
    expected_repository_inventory_sha256=None,
):
    receipt = verifier.verify_bundle(
        bundle,
        include_behavioral_hash=True,
        require_audited_solver_evidence=require_audited_solver_evidence,
    )
    if receipt["repeatCount"] != 1:
        raise RuntimeError("WP9 panel bundles must contain exactly one repeat")
    summary = json.loads((bundle / "summary.json").read_text(encoding="utf-8"))
    if expected_label is not None and summary.get("label") != expected_label:
        raise RuntimeError(
            f"bundle label differs: expected {expected_label}, "
            f"actual {summary.get('label')!r}"
        )
    if (
        expected_source_scenario_sha256 is not None
        and summary.get("sourceScenarioContentSha256")
        != expected_source_scenario_sha256
    ):
        raise RuntimeError(f"bundle source scenario differs: {expected_label}")
    if (
        expected_repository_inventory_sha256 is not None
        and summary.get("repositoryInventorySha256")
        != expected_repository_inventory_sha256
    ):
        raise RuntimeError(f"bundle repository inventory differs: {expected_label}")

    lifecycle = dict.fromkeys(_EVENTS, 0)
    burden_pickup = 0
    burden_drop = 0
    exogenous_pickup = 0
    exogenous_drop = 0
    material_revision_count = 0
    pre_pickup_inserted_stop_count = 0
    disruptive_decision_count = 0

    with (bundle / "transcript-00.ndjson").open("r", encoding="utf-8") as stream:
        for line_number, line in enumerate(stream, start=1):
            envelope = json.loads(line)
            frame = json.loads(
                base64.b64decode(envelope["frameBase64"], validate=True).decode(
                    "utf-8"
                )
            )
            if frame.get("messageType") == "eventBatch":
                for event in frame["payload"]["events"]:
                    event_type = event["eventType"]
                    if event_type in lifecycle:
                        lifecycle[event_type] += 1
                continue
            if frame.get("messageType") != "decision":
                continue

            disruptive = False
            for action in frame["payload"]["actions"]:
                if action.get("decisionType") != "promisePublished":
                    continue
                payload = action["payload"]
                decision = payload["decisionDelta"]
                exogenous = payload["exogenousDelta"]
                pickup = _nonnegative_integer(
                    decision["pickupEtaTotalMs"],
                    f"line {line_number} decision pickup",
                )
                drop = _nonnegative_integer(
                    decision["dropEtaTotalMs"],
                    f"line {line_number} decision drop",
                )
                burden_pickup += pickup
                burden_drop += drop
                material_revision_count += _nonnegative_integer(
                    decision["materialEtaRevisionCount"],
                    f"line {line_number} material revisions",
                )
                pre_pickup_inserted_stop_count += _nonnegative_integer(
                    decision["prePickupInsertedStopCount"],
                    f"line {line_number} pre-pickup insertions",
                )
                exogenous_pickup += _nonnegative_integer(
                    exogenous["pickupEtaTotalMs"],
                    f"line {line_number} exogenous pickup",
                )
                exogenous_drop += _nonnegative_integer(
                    exogenous["dropEtaTotalMs"],
                    f"line {line_number} exogenous drop",
                )
                disruptive = disruptive or any(
                    _nonnegative_integer(
                        value,
                        f"line {line_number} decisionDelta.{dimension}",
                    )
                    != 0
                    for dimension, value in decision.items()
                )
            if disruptive:
                disruptive_decision_count += 1

    arrived = lifecycle["requestArrived"]
    completed = lifecycle["passengerAlighted"]
    if arrived <= 0 or completed > arrived:
        raise RuntimeError("request completion counts are invalid")

    return {
        "arrived": arrived,
        "behavioralProjectionHash": receipt["behavioralProjectionHash"],
        "completed": completed,
        "decisionDropEtaTotalMs": burden_drop,
        "decisionPickupEtaTotalMs": burden_pickup,
        "disruptiveDecisionCount": disruptive_decision_count,
        "exogenousDropEtaTotalMs": exogenous_drop,
        "exogenousPickupEtaTotalMs": exogenous_pickup,
        "materialEtaRevisionCount": material_revision_count,
        "prePickupInsertedStopCount": pre_pickup_inserted_stop_count,
        "semanticHash": receipt["semanticHash"],
        "sourceScenarioContentSha256": summary["sourceScenarioContentSha256"],
        "totalDecisionInducedBurdenMs": burden_pickup + burden_drop,
    }


def _panel_result(panel_id, observations, margin_basis_points):
    if not observations:
        raise RuntimeError("the fixed panel must not be empty")
    if isinstance(margin_basis_points, bool) or margin_basis_points < 0:
        raise RuntimeError("service margin basis points are invalid")

    rows = []
    baseline_arrived = 0
    treatment_arrived = 0
    baseline_completed = 0
    treatment_completed = 0
    baseline_burden = 0
    treatment_burden = 0
    baseline_pickup_burden = 0
    treatment_pickup_burden = 0
    baseline_drop_burden = 0
    treatment_drop_burden = 0

    for cell_id, baseline, treatment in observations:
        if baseline["arrived"] != treatment["arrived"]:
            raise RuntimeError(f"cell {cell_id} arms have different arrival counts")
        row = {
            "baseline": baseline,
            "cellId": cell_id,
            "deltaCompleted": treatment["completed"] - baseline["completed"],
            "deltaDisruptiveDecisionCount": (
                treatment["disruptiveDecisionCount"]
                - baseline["disruptiveDecisionCount"]
            ),
            "deltaDecisionDropEtaTotalMs": (
                treatment["decisionDropEtaTotalMs"]
                - baseline["decisionDropEtaTotalMs"]
            ),
            "deltaDecisionPickupEtaTotalMs": (
                treatment["decisionPickupEtaTotalMs"]
                - baseline["decisionPickupEtaTotalMs"]
            ),
            "deltaTotalDecisionInducedBurdenMs": (
                treatment["totalDecisionInducedBurdenMs"]
                - baseline["totalDecisionInducedBurdenMs"]
            ),
            "treatment": treatment,
        }
        rows.append(row)
        baseline_arrived += baseline["arrived"]
        treatment_arrived += treatment["arrived"]
        baseline_completed += baseline["completed"]
        treatment_completed += treatment["completed"]
        baseline_burden += baseline["totalDecisionInducedBurdenMs"]
        treatment_burden += treatment["totalDecisionInducedBurdenMs"]
        baseline_pickup_burden += baseline["decisionPickupEtaTotalMs"]
        treatment_pickup_burden += treatment["decisionPickupEtaTotalMs"]
        baseline_drop_burden += baseline["decisionDropEtaTotalMs"]
        treatment_drop_burden += treatment["decisionDropEtaTotalMs"]

    if baseline_arrived != treatment_arrived:
        raise RuntimeError("panel arm denominators differ")
    if treatment_pickup_burden != 0:
        raise RuntimeError("primary treatment violated the preregistered pickup-ETA lock")
    delta_completed = treatment_completed - baseline_completed
    # basis points of a rate have denominator 10,000.  A 100 bp margin is 1 pp.
    service_gate_passed = (
        10_000 * delta_completed
        > -margin_basis_points * baseline_arrived
    )
    burden_gate_passed = treatment_burden < baseline_burden
    pickup_reduction = baseline_pickup_burden - treatment_pickup_burden
    drop_reduction = baseline_drop_burden - treatment_drop_burden
    total_reduction = baseline_burden - treatment_burden
    if pickup_reduction + drop_reduction != total_reduction:
        raise RuntimeError("pickup/drop burden reduction does not conserve total")

    return {
        "aggregate": {
            "arrivedPerArm": baseline_arrived,
            "baselineCompleted": baseline_completed,
            "baselineDecisionDropEtaTotalMs": baseline_drop_burden,
            "baselineDecisionPickupEtaTotalMs": baseline_pickup_burden,
            "baselineTotalDecisionInducedBurdenMs": baseline_burden,
            "burdenReductionDecomposition": {
                "dropEtaEarnedComponentMs": drop_reduction,
                "pickupEtaDefinitionLockedComponentMs": pickup_reduction,
                "shareDenominatorMs": total_reduction,
                "shareDefined": total_reduction > 0,
                "totalReductionMs": total_reduction,
            },
            "burdenGatePassed": burden_gate_passed,
            "deltaCompleted": delta_completed,
            "deltaTotalDecisionInducedBurdenMs": (
                treatment_burden - baseline_burden
            ),
            "serviceGatePassed": service_gate_passed,
            "serviceMarginBasisPoints": margin_basis_points,
            "treatmentCompleted": treatment_completed,
            "treatmentDecisionDropEtaTotalMs": treatment_drop_burden,
            "treatmentDecisionPickupEtaTotalMs": treatment_pickup_burden,
            "treatmentTotalDecisionInducedBurdenMs": treatment_burden,
        },
        "cells": rows,
        "panelId": panel_id,
        "schemaVersion": "1.0.0",
        "status": "pass" if service_gate_passed and burden_gate_passed else "gateFailed",
    }


def _read_manifest(path):
    manifest = json.loads(path.read_text(encoding="utf-8"))
    required = {
        "schemaVersion",
        "panelId",
        "baselineArmId",
        "treatmentArmId",
        "serviceMarginBasisPoints",
        "cells",
    }
    if set(manifest) != required or manifest["schemaVersion"] != "1.0.0":
        raise RuntimeError("analysis manifest fields/version differ")
    cells = manifest["cells"]
    if (
        not isinstance(cells, list)
        or not cells
        or any(set(cell) != {"cellId", "baselineBundle", "treatmentBundle"}
               for cell in cells)
        or len({cell["cellId"] for cell in cells}) != len(cells)
    ):
        raise RuntimeError("analysis manifest cells are invalid or duplicate")
    return manifest


def _load_matrix_program(adapter_root):
    path = adapter_root / "wp9_run_matrix.py"
    specification = importlib.util.spec_from_file_location(
        "ridebound_wp9_matrix_for_analysis",
        path,
    )
    if specification is None or specification.loader is None:
        raise RuntimeError("cannot load the WP9 matrix validator")
    module = importlib.util.module_from_spec(specification)
    specification.loader.exec_module(module)
    return module


def _validate_primary_job(job, cell_id, arm_id, bundle_name):
    expected_wp4 = {
        "b1": "benchmarks/configurations/wp9-fleetpy-rolling-cost-audited-v1.json",
        "c1": "benchmarks/configurations/wp9-fleetpy-ridebound-hard-vector-audited-v1.json",
    }
    expected = {
        "armId": arm_id,
        "cellId": cell_id,
        "commitmentConfig": (
            "benchmarks/configurations/wp8-drop-eta-budget-tight-v1.json"
        ),
        "driver": f"benchmarks/scenarios/wp9-confirmatory/{cell_id}.driver.json",
        "jobId": f"p-{cell_id}-{arm_id}-tight-s7",
        "masterSeed": 7,
        "phase": "primary",
        "wp4Config": expected_wp4.get(arm_id),
    }
    if bundle_name != expected["jobId"] or job != expected:
        raise RuntimeError(
            f"primary job binding differs for cell {cell_id}, arm {arm_id}"
        )


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--manifest", required=True, type=pathlib.Path)
    parser.add_argument("--execution-plan", required=True, type=pathlib.Path)
    parser.add_argument("--repository", required=True, type=pathlib.Path)
    parser.add_argument("--bundle-root", required=True, type=pathlib.Path)
    parser.add_argument("--require-audited-solver-evidence", action="store_true")
    arguments = parser.parse_args()

    adapter_root = pathlib.Path(__file__).parent.resolve()
    manifest = _read_manifest(arguments.manifest.resolve())
    matrix = _load_matrix_program(adapter_root)
    plan = matrix._load_plan(arguments.execution_plan.resolve())
    matrix._validate_frozen_design(plan)
    jobs = {job["jobId"]: job for job in plan["jobs"]}
    repository = arguments.repository.resolve()
    repository_inventory_sha256 = matrix._repository_inventory_sha256(repository)
    verifier = _load_verifier(adapter_root)
    observations = []
    for cell in sorted(manifest["cells"], key=lambda value: value["cellId"]):
        baseline_job = jobs.get(cell["baselineBundle"])
        treatment_job = jobs.get(cell["treatmentBundle"])
        if baseline_job is None or treatment_job is None:
            raise RuntimeError(f"cell {cell['cellId']} job is absent from execution plan")
        _validate_primary_job(
            baseline_job,
            cell["cellId"],
            manifest["baselineArmId"],
            cell["baselineBundle"],
        )
        _validate_primary_job(
            treatment_job,
            cell["cellId"],
            manifest["treatmentArmId"],
            cell["treatmentBundle"],
        )
        scenario = repository / (
            "benchmarks/fixtures/wp6/public/fleetpy-manhattan-v1/"
            "wp9-confirmatory-fixed-panel-v2"
        ) / cell["cellId"] / "scenario-content.json"
        scenario_sha256 = _sha256(scenario)
        baseline = _read_observation(
            (arguments.bundle_root / cell["baselineBundle"]).resolve(),
            verifier,
            arguments.require_audited_solver_evidence,
            cell["baselineBundle"],
            scenario_sha256,
            repository_inventory_sha256,
        )
        treatment = _read_observation(
            (arguments.bundle_root / cell["treatmentBundle"]).resolve(),
            verifier,
            arguments.require_audited_solver_evidence,
            cell["treatmentBundle"],
            scenario_sha256,
            repository_inventory_sha256,
        )
        observations.append((cell["cellId"], baseline, treatment))

    print(_canonical(_panel_result(
        manifest["panelId"],
        observations,
        manifest["serviceMarginBasisPoints"],
    )))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
