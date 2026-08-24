"""Verify same-arm operational equivalence between E1 and immutable H6."""

from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import pathlib

import jsonschema


SCHEMA_ID = (
    "https://ridebound.local/schemas/wp13/v1/"
    "e1-h6-behavioral-equivalence.schema.json"
)


def _load(path):
    return json.loads(path.read_text(encoding="utf-8"))


def _load_module(path, name):
    specification = importlib.util.spec_from_file_location(name, path)
    if specification is None or specification.loader is None:
        raise RuntimeError(f"cannot import {path}")
    module = importlib.util.module_from_spec(specification)
    specification.loader.exec_module(module)
    return module


def _sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _encoded(value):
    return (
        json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"))
        + "\n"
    )


def _root_hash(records):
    digest = hashlib.sha256(b"RideBound.Wp13H6TargetRootInventory.v1\0")
    for record in sorted(records, key=lambda value: value["jobId"]):
        for value in (record["jobId"], record["h6BundleManifestSha256"]):
            encoded = value.encode("utf-8")
            digest.update(len(encoded).to_bytes(8, "big"))
            digest.update(encoded)
    return digest.hexdigest()


def _aggregate(records):
    if len(records) != 80 or len({value["jobId"] for value in records}) != 80:
        raise RuntimeError("E1/H6 equivalence does not cover exact 80 targets")
    mismatches = [value for value in records if not value["operationallyEqual"]]
    if mismatches:
        raise RuntimeError(
            "E1 instrumentation changed same-arm behavior: "
            + ",".join(value["jobId"] for value in mismatches)
        )
    panels = []
    for panel in ("A", "B"):
        selected = [value for value in records if value["panelId"] == panel]
        panels.append(
            {
                "panelId": panel,
                "armRunCount": len(selected),
                "behaviorallyEqualArmRunCount": len(selected),
                "mismatchCount": 0,
                "h6SolverDecisionCount": sum(
                    value["h6SolverDecisionCount"] for value in selected
                ),
                "h6RootInventorySha256": _root_hash(selected),
            }
        )
    return panels


def build_report(repository, inventory_path, h6_roots):
    verifier_path = (
        repository / "simulators/fleetpy-ridebound/actual_fleetpy_medium_verify.py"
    )
    verifier = _load_module(
        verifier_path,
        "ridebound_medium_verifier_for_e1_h6_equivalence",
    )
    inventory = _load(inventory_path)
    records = []
    for e1 in inventory["runs"]:
        panel = e1["panelId"]
        bundle = (h6_roots[panel] / e1["jobId"]).resolve()
        if bundle.parent != h6_roots[panel] or not bundle.is_dir():
            raise RuntimeError(f"H6 target bundle is missing: {e1['jobId']}")
        summary = _load(bundle / "summary.json")
        if (
            summary.get("label") != e1["jobId"]
            or summary.get("sourceScenarioContentSha256")
            != e1["sourceScenarioContentSha256"]
        ):
            raise RuntimeError(f"H6 target identity differs: {e1['jobId']}")
        verified = verifier.verify_bundle(
            bundle,
            include_behavioral_hash=True,
            require_audited_solver_evidence=True,
        )
        if verified["repeatCount"] != 1:
            raise RuntimeError(f"H6 repeat count differs: {e1['jobId']}")
        run = verified["runs"][0]
        h6_behavior = verified["behavioralProjectionHash"]
        records.append(
            {
                "panelId": panel,
                "unitId": e1["unitId"],
                "armId": e1["armId"],
                "jobId": e1["jobId"],
                "sourceScenarioContentSha256": e1[
                    "sourceScenarioContentSha256"
                ],
                "e1BundleManifestSha256": e1["bundleManifestSha256"],
                "h6BundleManifestSha256": _sha256(
                    bundle / "bundle-manifest.json"
                ),
                "e1SemanticHash": e1["semanticHash"],
                "h6SemanticHash": verified["semanticHash"],
                "e1BehavioralProjectionHash": e1[
                    "behavioralProjectionHash"
                ],
                "h6BehavioralProjectionHash": h6_behavior,
                "h6SolverDecisionCount": run["epochCount"],
                "requestCount": run["requestCount"],
                "operationallyEqual": (
                    h6_behavior == e1["behavioralProjectionHash"]
                ),
            }
        )
    records.sort(
        key=lambda value: (value["panelId"], value["unitId"], value["armId"])
    )
    panels = _aggregate(records)
    schema_path = (
        repository
        / "benchmarks/schemas/wp13/v1/"
        "e1-h6-behavioral-equivalence.schema.json"
    )
    result = {
        "schemaVersion": "1.0.0",
        "schemaId": SCHEMA_ID,
        "reportType": "ridebound-wp13-e1-h6-behavioral-equivalence-v1",
        "claimBoundary": {
            "comparison": "sameArmSameScenarioOperationalProjection",
            "interpretation": "instrumentationEquivalenceOnlyNotCausal",
            "stateHashEquality": "notRequiredPolicyManifestBindsState",
            "mechanismConclusion": "notEvaluated",
        },
        "sourceIdentity": {
            "analyzerSourceSha256": _sha256(pathlib.Path(__file__).resolve()),
            "independentVerifierSourceSha256": _sha256(verifier_path),
            "schemaSha256": _sha256(schema_path),
        },
        "inputEvidence": {
            "e1InventorySha256": _sha256(inventory_path),
            "e1RepositoryInventorySha256": inventory["totals"][
                "repositoryInventorySha256"
            ],
            "h6PanelARoot": str(h6_roots["A"]),
            "h6PanelBRoot": str(h6_roots["B"]),
        },
        "totals": {
            "armRunCount": len(records),
            "behaviorallyEqualArmRunCount": len(records),
            "mismatchCount": 0,
            "requestCount": sum(value["requestCount"] for value in records),
            "h6SolverDecisionCount": sum(
                value["h6SolverDecisionCount"] for value in records
            ),
        },
        "panels": panels,
        "records": records,
    }
    jsonschema.Draft202012Validator(_load(schema_path)).validate(result)
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--repository", required=True, type=pathlib.Path)
    parser.add_argument("--inventory", required=True, type=pathlib.Path)
    parser.add_argument("--h6-panel-a-root", required=True, type=pathlib.Path)
    parser.add_argument("--h6-panel-b-root", required=True, type=pathlib.Path)
    parser.add_argument("--output", type=pathlib.Path)
    arguments = parser.parse_args()
    result = build_report(
        arguments.repository.resolve(),
        arguments.inventory.resolve(),
        {
            "A": arguments.h6_panel_a_root.resolve(),
            "B": arguments.h6_panel_b_root.resolve(),
        },
    )
    encoded = _encoded(result)
    if arguments.output:
        output = arguments.output.resolve()
        output.parent.mkdir(parents=True, exist_ok=True)
        with output.open("x", encoding="utf-8", newline="\n") as target:
            target.write(encoded)
    else:
        print(encoded, end="")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
