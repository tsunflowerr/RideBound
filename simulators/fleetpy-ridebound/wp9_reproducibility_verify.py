#!/usr/bin/env python3
"""Independently verify the complete H6 WP9 reproducibility package.

This is deliberately separate from the frozen outcome analyzers.  It verifies
all raw bundles and the four falsification conditions preregistered in WP8-010:
frozen unit identity, derivative provenance, exact exogenous input projection,
and deterministic behavioral repetition without treating solver seeds as new
experimental units.
"""

from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import pathlib
import sys


_PANELS = {
    "a": {
        "fixture": (
            "benchmarks/fixtures/wp6/public/fleetpy-manhattan-v1/"
            "wp9-confirmatory-fixed-panel-v2"
        ),
        "grid": "benchmarks/scenarios/wp9-confirmatory/grid-v2.json",
        "plan": (
            "benchmarks/scenarios/wp9-confirmatory/execution-plan-v1.json"
        ),
        "manifest": (
            "benchmarks/scenarios/wp9-confirmatory/analysis-manifest-v1.json"
        ),
    },
    "b": {
        "fixture": (
            "benchmarks/fixtures/wp6/public/fleetpy-manhattan-v1/"
            "wp9-confirmatory-fixed-panel-v3-veh4"
        ),
        "grid": "benchmarks/scenarios/wp9-confirmatory/grid-v3-veh4.json",
        "plan": (
            "benchmarks/scenarios/wp9-confirmatory/"
            "execution-plan-panel-b-v1.json"
        ),
        "manifest": (
            "benchmarks/scenarios/wp9-confirmatory/"
            "analysis-manifest-panel-b-v1.json"
        ),
    },
}
_PROVENANCE_FIELDS = (
    "sourceArtifactSha256",
    "sourceMemberInventorySha256",
    "normalizerSourceSha256",
    "selectionFrameSha256",
    "scenarioContentSha256",
)
# The verifier is also imported by unittest through a file specification, where
# Python does not automatically add the adapter root to sys.path.  The mapping
# package is needed to reproduce the exact opaque node/request identities that
# crossed the protocol boundary.
_ADAPTER_ROOT = pathlib.Path(__file__).parent.resolve()
if str(_ADAPTER_ROOT) not in sys.path:
    sys.path.insert(0, str(_ADAPTER_ROOT))


def _load_module(name, path):
    specification = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(specification)
    specification.loader.exec_module(module)
    return module


def _read_json(path):
    with path.open("r", encoding="utf-8") as source:
        value = json.load(source)
    if not isinstance(value, dict):
        raise RuntimeError(f"{path}: root is not an object")
    return value


def _sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _canonical(value):
    return json.dumps(
        value,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
        allow_nan=False,
    ).encode("utf-8")


def _append_frame(digest, tag, value):
    tag_bytes = tag.encode("utf-8")
    if len(tag_bytes) > 65535:
        raise ValueError("hash frame tag is too long")
    digest.update(len(tag_bytes).to_bytes(2, "big"))
    digest.update(tag_bytes)
    digest.update(len(value).to_bytes(8, "big"))
    digest.update(value)


def _projection_hash(domain, tag, value):
    digest = hashlib.sha256()
    digest.update(domain.encode("utf-8") + b"\0")
    _append_frame(digest, tag, _canonical(value))
    return digest.hexdigest()


def _experimental_unit_id(scenario_hash, demand_hash, travel_hash):
    digest = hashlib.sha256()
    digest.update(b"RideBound.Wp8.ExperimentalUnit.v1\0")
    for tag, value in (
        ("scenarioHash", scenario_hash),
        ("demandRealizationHash", demand_hash),
        ("travelRealizationHash", travel_hash),
    ):
        try:
            decoded = bytes.fromhex(value)
        except ValueError as error:
            raise RuntimeError(f"{tag} is not hexadecimal") from error
        if len(decoded) != 32:
            raise RuntimeError(f"{tag} is not SHA-256")
        _append_frame(digest, tag, decoded)
    return digest.hexdigest()


def _expected_exogenous_projection(scenario):
    from decimal import Decimal

    from ridebound_fleetpy.mapping import FleetPyProtocolMapper

    snapshots = scenario.get("travelSnapshots")
    events = scenario.get("events")
    if not isinstance(snapshots, list) or len(snapshots) != 1:
        raise RuntimeError("WP9 scenario must contain one frozen travel snapshot")
    if not isinstance(events, list) or not events:
        raise RuntimeError("WP9 scenario request event list is invalid")
    mapper = FleetPyProtocolMapper()
    source_snapshot = snapshots[0]
    source_arcs = source_snapshot.get("arcs", [])
    source_nodes = sorted(
        {arc.get("fromNodeId") for arc in source_arcs}
        | {arc.get("toNodeId") for arc in source_arcs}
    )
    if any(not isinstance(value, str) or not value for value in source_nodes):
        raise RuntimeError("scenario source node identity is invalid")
    # actual_fleetpy_medium_preflight materializes the closed metric graph as a
    # deterministic FleetPy network numbered by the lexical source-node order.
    # Reproduce that boundary exactly before applying the adapter's opaque-ID
    # registry; hashing source node labels directly would verify the wrong wire.
    node_index = {node_id: index for index, node_id in enumerate(source_nodes)}
    mapped_snapshot = mapper.travel_snapshot(
        source_snapshot.get("version"),
        (
            (
                node_index[arc.get("fromNodeId")],
                node_index[arc.get("toNodeId")],
                Decimal(arc.get("travelTimeMs")) / Decimal(1000),
            )
            for arc in source_arcs
        ),
    )
    result = [
        {
            "simTimeMs": 0,
            "eventType": "travelTimesUpdated",
            "payload": {"snapshot": mapped_snapshot},
        }
    ]
    for expected_sequence, event in enumerate(events, 1):
        if (
            event.get("eventSequence") != expected_sequence
            or event.get("eventType") != "requestArrived"
        ):
            raise RuntimeError("scenario request event order/type differs")
        try:
            payload_bytes = bytes.fromhex(event["payloadCanonicalJsonHex"])
            payload = json.loads(payload_bytes)
        except (KeyError, ValueError, TypeError, json.JSONDecodeError) as error:
            raise RuntimeError("scenario request payload is invalid") from error
        if (
            _canonical(payload) != payload_bytes
            or hashlib.sha256(payload_bytes).hexdigest()
            != event.get("payloadSha256")
        ):
            raise RuntimeError("scenario request payload hash/canonical form differs")
        request = payload.get("request")
        if not isinstance(request, dict):
            raise RuntimeError("scenario request event has no request object")
        mapped_request = dict(request)
        mapped_request["requestId"] = mapper.requests.register(request["requestId"])
        mapped_request["originNodeId"] = mapper.node_id(
            node_index[request["originNodeId"]]
        )
        mapped_request["destinationNodeId"] = mapper.node_id(
            node_index[request["destinationNodeId"]]
        )
        result.append(
            {
                "simTimeMs": event["simTimeMs"],
                "eventType": "requestArrived",
                "payload": {"request": mapped_request},
            }
        )
    return result


def _actual_exogenous_projection(verifier, transcript):
    result = []
    for direction, envelope in verifier.decode_transcript(transcript):
        if direction != "adapterToRunner" or envelope.get("messageType") != "eventBatch":
            continue
        for event in envelope.get("payload", {}).get("events", []):
            if event.get("eventType") in {"requestArrived", "travelTimesUpdated"}:
                result.append(
                    {
                        "simTimeMs": envelope.get("simTimeMs"),
                        "eventType": event.get("eventType"),
                        "payload": event.get("payload"),
                    }
                )
    return result


def _projection_identity(projection, scenario_hash):
    demand = [value for value in projection if value["eventType"] == "requestArrived"]
    travel = [
        value for value in projection if value["eventType"] == "travelTimesUpdated"
    ]
    demand_hash = _projection_hash(
        "RideBound.Wp9DemandRealization.v1",
        "canonicalRequestArrivalProjection",
        demand,
    )
    travel_hash = _projection_hash(
        "RideBound.Wp9TravelRealization.v1",
        "canonicalTravelUpdateProjection",
        travel,
    )
    return {
        "scenarioHash": scenario_hash,
        "demandRealizationHash": demand_hash,
        "travelRealizationHash": travel_hash,
        "experimentalUnitId": _experimental_unit_id(
            scenario_hash,
            demand_hash,
            travel_hash,
        ),
        "exogenousEventProjectionHash": _projection_hash(
            "RideBound.Wp9ExogenousEventProjection.v1",
            "canonicalExogenousEvents",
            projection,
        ),
        "arrivalCount": len(demand),
        "travelUpdateCount": len(travel),
    }


def _validate_provenance(fixture, driver):
    derivative = _read_json(fixture / "derivative-manifest.json")
    report = _read_json(fixture / "normalization-report.json")
    scenario = _read_json(fixture / "scenario-content.json")
    configuration = _read_json(fixture / "normalizer-configuration.json")
    selection_sha = _sha256(fixture / "selection-frame.json")
    scenario_sha = _sha256(fixture / "scenario-content.json")
    sources = {
        "derivative": derivative,
        "normalizationReport": report,
        "scenario": scenario,
        "configuration": configuration,
        "driver": driver,
    }
    expected = {
        "sourceArtifactSha256": derivative.get("sourceArtifactSha256"),
        "sourceMemberInventorySha256": derivative.get(
            "sourceMemberInventorySha256"
        ),
        "normalizerSourceSha256": derivative.get("normalizerSourceSha256"),
        "selectionFrameSha256": derivative.get("selectionFrameSha256"),
        "scenarioContentSha256": derivative.get("scenarioContentSha256"),
    }
    if expected["selectionFrameSha256"] != selection_sha:
        raise RuntimeError("selectionFrameSha256 differs from derivative file")
    if expected["scenarioContentSha256"] != scenario_sha:
        raise RuntimeError("scenarioContentSha256 differs from derivative file")
    for field, value in expected.items():
        if not isinstance(value, str) or len(value) != 64:
            raise RuntimeError(f"derivative provenance field is invalid: {field}")
        present = [
            (name, source[field])
            for name, source in sources.items()
            if field in source
        ]
        if not present or any(candidate != value for _, candidate in present):
            raise RuntimeError(
                f"derivative provenance mismatch: {field}: {present!r}"
            )
    if configuration.get("normalizerSourceSha256") != expected[
        "normalizerSourceSha256"
    ]:
        raise RuntimeError("normalizer configuration source provenance differs")
    return derivative, scenario, expected


def _validate_pair_projection(expected, baseline, treatment, cell_id):
    if baseline != expected or treatment != expected:
        raise RuntimeError(
            f"{cell_id}: demand/travel event projection differs from frozen input"
        )


def _verify_receipt(path, plan):
    records = []
    with path.open("r", encoding="utf-8") as source:
        for line_number, line in enumerate(source, 1):
            try:
                value = json.loads(line)
            except json.JSONDecodeError as error:
                raise RuntimeError(f"receipt line {line_number} is invalid") from error
            if not isinstance(value, dict):
                raise RuntimeError(f"receipt line {line_number} is not an object")
            records.append(value)
    if len(records) < 2:
        raise RuntimeError("matrix receipt has no terminal records/summary")
    terminals, summary = records[:-1], records[-1]
    expected_jobs = {job["jobId"] for job in plan["jobs"]}
    terminal_jobs = [value.get("jobId") for value in terminals]
    summary_jobs = {value.get("jobId") for value in summary.get("receipts", [])}
    inventories = {
        value.get("repositoryInventorySha256")
        for value in terminals + summary.get("receipts", [])
    }
    if (
        len(terminal_jobs) != len(set(terminal_jobs))
        or set(terminal_jobs) != expected_jobs
        or summary_jobs != expected_jobs
        or set(summary.get("selectedJobIds", [])) != expected_jobs
        or summary.get("successCount") != len(expected_jobs)
        or summary.get("failureCount") != 0
        or summary.get("failures") != []
        or len(inventories) != 1
        or None in inventories
        or any(
            value.get("status") not in {"completedVerified", "reusedVerified"}
            for value in terminals
        )
    ):
        raise RuntimeError("matrix receipt coverage/status/inventory differs")
    return {
        "receiptSha256": _sha256(path),
        "terminalJobCount": len(terminals),
        "repositoryInventorySha256": next(iter(inventories)),
    }


def _validate_repeat_result(result, expected_repeats=2):
    hashes = {
        run.get("behavioralProjectionHash") for run in result.get("runs", [])
    }
    if (
        result.get("status") != "pass"
        or result.get("repeatCount") != expected_repeats
        or len(result.get("runs", [])) != expected_repeats
        or len(hashes) != 1
        or None in hashes
        or result.get("behavioralProjectionHash") != next(iter(hashes))
    ):
        raise RuntimeError("behavioral projection repeats diverged")


def _verify_analysis(path, expected_cells, expected_arrived):
    result = _read_json(path)
    cells = result.get("cells")
    if (
        not isinstance(cells, list)
        or {value.get("cellId") for value in cells} != expected_cells
        or result.get("aggregate", {}).get("arrivedPerArm") != expected_arrived
    ):
        raise RuntimeError(f"analysis artifact coverage differs: {path}")
    return {"sha256": _sha256(path), "status": result.get("status")}


def _verify_panel(
    repository,
    panel,
    bundle_root,
    receipt_path,
    analysis_path,
    verifier,
    matrix,
):
    specification = _PANELS[panel]
    plan = matrix._load_plan(repository / specification["plan"])
    matrix._validate_frozen_design(plan, panel)
    manifest = _read_json(repository / specification["manifest"])
    grid = _read_json(repository / specification["grid"])
    jobs = {job["jobId"]: job for job in plan["jobs"]}
    manifest_cells = {value["cellId"]: value for value in manifest["cells"]}
    grid_cells = {value["cellId"]: value for value in grid["cells"]}
    if set(manifest_cells) != set(grid_cells) or len(manifest_cells) != 20:
        raise RuntimeError(f"panel {panel}: grid/manifest cell coverage differs")

    verified_bundles = {}
    inventory_records = []
    for job_id in sorted(jobs):
        bundle = (bundle_root / job_id).resolve()
        verified_bundles[job_id] = verifier.verify_bundle(
            bundle,
            include_behavioral_hash=True,
            require_audited_solver_evidence=True,
        )
        inventory_records.append(
            {
                "jobId": job_id,
                "bundleManifestSha256": _sha256(bundle / "bundle-manifest.json"),
            }
        )

    cells = []
    for cell_id in sorted(manifest_cells):
        cell = manifest_cells[cell_id]
        grid_cell = grid_cells[cell_id]
        fixture = repository / specification["fixture"] / cell_id
        driver = _read_json(repository / jobs[cell["baselineBundle"]]["driver"])
        derivative, scenario, provenance = _validate_provenance(fixture, driver)
        if (
            grid_cell.get("scenarioId") != derivative.get("scenarioId")
            or driver.get("sourceScenarioHash") != derivative.get("scenarioHash")
        ):
            raise RuntimeError(f"{cell_id}: grid/driver scenario binding differs")
        expected = _expected_exogenous_projection(scenario)
        baseline_bundle = bundle_root / cell["baselineBundle"]
        treatment_bundle = bundle_root / cell["treatmentBundle"]
        baseline = _actual_exogenous_projection(
            verifier, baseline_bundle / "transcript-00.ndjson"
        )
        treatment = _actual_exogenous_projection(
            verifier, treatment_bundle / "transcript-00.ndjson"
        )
        _validate_pair_projection(expected, baseline, treatment, cell_id)
        identity = _projection_identity(expected, derivative["scenarioHash"])
        if identity["arrivalCount"] != grid_cell.get("requestTarget"):
            raise RuntimeError(f"{cell_id}: arrival denominator differs")
        for bundle in (baseline_bundle, treatment_bundle):
            summary = _read_json(bundle / "summary.json")
            if summary.get("sourceScenarioContentSha256") != provenance[
                "scenarioContentSha256"
            ]:
                raise RuntimeError(f"{cell_id}: bundle scenario provenance differs")
        cells.append({"cellId": cell_id, **identity, **provenance})

    receipt = _verify_receipt(receipt_path, plan)
    analysis = _verify_analysis(
        analysis_path,
        set(manifest_cells),
        sum(value["requestTarget"] for value in grid_cells.values()),
    )
    return {
        "panel": panel,
        "status": "pass",
        "cellCount": len(cells),
        "verifiedBundleCount": len(verified_bundles),
        "rawBundleInventoryHash": _projection_hash(
            "RideBound.Wp9RawBundleInventory.v1",
            "canonicalBundleManifestHashes",
            inventory_records,
        ),
        "receipt": receipt,
        "analysis": analysis,
        "cells": cells,
    }


def verify(arguments):
    repository = arguments.repository.resolve()
    adapter_root = pathlib.Path(__file__).parent.resolve()
    verifier = _load_module(
        "wp9_repro_medium_verifier",
        adapter_root / "actual_fleetpy_medium_verify.py",
    )
    matrix = _load_module(
        "wp9_repro_matrix",
        adapter_root / "wp9_run_matrix.py",
    )
    freeze = _load_module(
        "wp9_repro_freeze",
        adapter_root / "wp9_freeze_verify.py",
    )
    freeze_result = freeze.verify_receipt(
        arguments.freeze_receipt.resolve(),
        repository,
        arguments.runner_root.resolve(),
    )
    panels = [
        _verify_panel(
            repository,
            "a",
            arguments.panel_a_bundle_root.resolve(),
            arguments.panel_a_receipt.resolve(),
            arguments.panel_a_analysis.resolve(),
            verifier,
            matrix,
        ),
        _verify_panel(
            repository,
            "b",
            arguments.panel_b_bundle_root.resolve(),
            arguments.panel_b_receipt.resolve(),
            arguments.panel_b_analysis.resolve(),
            verifier,
            matrix,
        ),
    ]
    panel_a = {value["cellId"]: value for value in panels[0]["cells"]}
    panel_b = {value["cellId"]: value for value in panels[1]["cells"]}
    for cell_id in panel_a:
        for field in ("demandRealizationHash", "travelRealizationHash"):
            if panel_a[cell_id][field] != panel_b[cell_id][field]:
                raise RuntimeError(f"{cell_id}: capacity panels change {field}")
        if panel_a[cell_id]["scenarioHash"] == panel_b[cell_id]["scenarioHash"]:
            raise RuntimeError(f"{cell_id}: capacity panel scenario hashes collapsed")

    repeat_result = verifier.verify_bundle(
        arguments.repeat_bundle.resolve(),
        include_behavioral_hash=True,
        require_audited_solver_evidence=True,
    )
    _validate_repeat_result(repeat_result)
    robustness = _read_json(arguments.robustness_analysis.resolve())
    contrasts = robustness.get("aggregateContrasts", {})
    if (
        robustness.get("confirmatoryGate") is not None
        or robustness.get("interpretation")
        != "descriptiveOnlyCannotRescuePrimary"
        or any(
            contrasts.get(name, {}).get(field) != 0
            for name in (
                "seed19MinusSeed7Baseline",
                "seed19MinusSeed7Treatment",
            )
            for field in (
                "deltaCompleted",
                "deltaDisruptiveDecisionCount",
                "deltaTotalDecisionInducedBurdenMs",
            )
        )
    ):
        raise RuntimeError("solver-seed non-replicate evidence differs")
    return {
        "schemaVersion": "1.0.0",
        "status": "pass",
        "verificationId": "wp9-h6-reproducibility-v1",
        "freeze": freeze_result,
        "panels": panels,
        "crossPanelDemandTravelIdentity": "pass",
        "behavioralRepeat": {
            "status": "pass",
            "repeatCount": repeat_result["repeatCount"],
            "behavioralProjectionHash": repeat_result[
                "behavioralProjectionHash"
            ],
            "bundleManifestSha256": _sha256(
                arguments.repeat_bundle.resolve() / "bundle-manifest.json"
            ),
        },
        "solverSeedInterpretation": {
            "status": "pass",
            "isIndependentReplicate": False,
            "aggregateMetricDeltasSeed19MinusSeed7": "allZero",
            "confirmatorySampleSizeIncrement": 0,
            "robustnessAnalysisSha256": _sha256(
                arguments.robustness_analysis.resolve()
            ),
        },
        "verifiedRawBundleCount": sum(
            value["verifiedBundleCount"] for value in panels
        ),
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--repository", required=True, type=pathlib.Path)
    parser.add_argument("--runner-root", required=True, type=pathlib.Path)
    parser.add_argument("--freeze-receipt", required=True, type=pathlib.Path)
    parser.add_argument("--panel-a-bundle-root", required=True, type=pathlib.Path)
    parser.add_argument("--panel-b-bundle-root", required=True, type=pathlib.Path)
    parser.add_argument("--panel-a-receipt", required=True, type=pathlib.Path)
    parser.add_argument("--panel-b-receipt", required=True, type=pathlib.Path)
    parser.add_argument("--panel-a-analysis", required=True, type=pathlib.Path)
    parser.add_argument("--panel-b-analysis", required=True, type=pathlib.Path)
    parser.add_argument("--robustness-analysis", required=True, type=pathlib.Path)
    parser.add_argument("--repeat-bundle", required=True, type=pathlib.Path)
    arguments = parser.parse_args()
    print(_canonical(verify(arguments)).decode("utf-8"))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
