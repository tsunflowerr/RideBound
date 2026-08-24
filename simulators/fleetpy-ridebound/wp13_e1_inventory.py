"""Inventory independently verified WP13 E1 retained-portfolio bundles."""

from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import pathlib

import jsonschema


SCHEMA_ID = (
    "https://ridebound.local/schemas/wp13/v1/"
    "exploratory-replay-inventory.schema.json"
)
REPORT_TYPE = "ridebound-wp13-e1-retained-portfolio-inventory-v1"


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


def _canonical(value):
    return (
        json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"))
        + "\n"
    )


def _root_inventory_sha256(records):
    digest = hashlib.sha256(b"RideBound.Wp13E1VerifiedRootInventory.v1\0")
    for record in sorted(records, key=lambda value: value["jobId"]):
        for value in (record["jobId"], record["bundleManifestSha256"]):
            encoded = value.encode("utf-8")
            digest.update(len(encoded).to_bytes(8, "big"))
            digest.update(encoded)
    return digest.hexdigest()


def _bundle_record(job, panel, output, verifier):
    summary_path = output / "summary.json"
    manifest_path = output / "bundle-manifest.json"
    summary = _load(summary_path)
    if (
        summary.get("label") != job["jobId"]
        or summary.get("sourceScenarioContentSha256")
        != job["sourceScenarioContentSha256"]
    ):
        raise RuntimeError(f"E1 output identity differs: {job['jobId']}")
    repository_inventory = summary.get("repositoryInventorySha256")
    if not isinstance(repository_inventory, str) or len(repository_inventory) != 64:
        raise RuntimeError(f"E1 repository inventory is invalid: {job['jobId']}")
    verified = verifier.verify_bundle(
        output,
        include_behavioral_hash=True,
        require_audited_solver_evidence=True,
        require_retained_candidate_portfolio=True,
    )
    if verified["repeatCount"] != 1 or len(verified["runs"]) != 1:
        raise RuntimeError(f"E1 repeat count differs: {job['jobId']}")
    run = verified["runs"][0]
    solver_decisions = run["epochCount"]
    retained = run.get("retainedPortfolioEvidenceCount")
    if retained != solver_decisions:
        raise RuntimeError(f"E1 v1.2 coverage differs: {job['jobId']}")
    manifest = _load(manifest_path)
    bundle_bytes = manifest_path.stat().st_size + sum(
        file["lengthBytes"] for file in manifest["files"]
    )
    resources = summary["runs"][0]["resources"]
    return {
        "panelId": panel,
        "unitId": job["unitId"],
        "armId": job["armId"],
        "jobId": job["jobId"],
        "sourceScenarioContentSha256": job["sourceScenarioContentSha256"],
        "repositoryInventorySha256": repository_inventory,
        "bundleManifestSha256": _sha256(manifest_path),
        "semanticHash": verified["semanticHash"],
        "behavioralProjectionHash": verified["behavioralProjectionHash"],
        "requestCount": run["requestCount"],
        "solverDecisionCount": solver_decisions,
        "retainedPortfolioEvidenceCount": retained,
        "bundleFileCount": len(manifest["files"]) + 1,
        "bundleBytes": bundle_bytes,
        "wallMilliseconds": resources["wallMilliseconds"],
        "userCpuMilliseconds": resources["userCpuMilliseconds"],
        "systemCpuMilliseconds": resources["systemCpuMilliseconds"],
        "rssBeforeBytes": resources["rssBeforeBytes"],
        "rssAfterBytes": resources["rssAfterBytes"],
    }


def _aggregate(plans, roots, records):
    expected = {
        (panel, job["jobId"])
        for panel, plan in plans.items()
        for job in plan["jobs"]
    }
    observed = {(record["panelId"], record["jobId"]) for record in records}
    inventories = {record["repositoryInventorySha256"] for record in records}
    if len(records) != 80 or observed != expected or len(observed) != 80:
        raise RuntimeError("E1 inventory does not cover exact 80 frozen arm jobs")
    if len(inventories) != 1:
        raise RuntimeError("E1 repository inventories differ across bundles")
    if any(
        record["retainedPortfolioEvidenceCount"]
        != record["solverDecisionCount"]
        for record in records
    ):
        raise RuntimeError("E1 retained-portfolio coverage is incomplete")
    panels = []
    for panel in ("A", "B"):
        selected = [record for record in records if record["panelId"] == panel]
        log_root = roots[panel] / "_logs"
        logs = list(log_root.glob("*.log")) if log_root.is_dir() else []
        panels.append(
            {
                "panelId": panel,
                "planId": plans[panel]["planId"],
                "root": str(roots[panel]),
                "rootInventorySha256": _root_inventory_sha256(selected),
                "armRunCount": len(selected),
                "requestCount": sum(value["requestCount"] for value in selected),
                "solverDecisionCount": sum(
                    value["solverDecisionCount"] for value in selected
                ),
                "retainedPortfolioEvidenceCount": sum(
                    value["retainedPortfolioEvidenceCount"] for value in selected
                ),
                "totalBundleBytes": sum(
                    value["bundleBytes"] for value in selected
                ),
                "logFileCount": len(logs),
                "logBytes": sum(path.stat().st_size for path in logs),
            }
        )
    return panels, next(iter(inventories))


def build_inventory(repository, receipt_path, record_set_path, roots):
    freeze = _load_module(
        repository / "simulators/fleetpy-ridebound/wp13_e1_freeze.py",
        "ridebound_wp13_e1_freeze_for_inventory",
    )
    verifier_path = (
        repository / "simulators/fleetpy-ridebound/actual_fleetpy_medium_verify.py"
    )
    verifier = _load_module(
        verifier_path,
        "ridebound_actual_medium_verifier_for_e1_inventory",
    )
    plans = freeze.verify_plans(repository, record_set_path)
    receipt = _load(receipt_path)
    for panel in ("A", "B"):
        if roots[panel] != pathlib.Path(
            receipt["execution"]["outputRoots"][panel]
        ).resolve():
            raise RuntimeError(f"Panel {panel} root differs from freeze receipt")
    records = []
    for panel in ("A", "B"):
        for job in plans[panel]["jobs"]:
            output = (roots[panel] / job["jobId"]).resolve()
            if output.parent != roots[panel] or not output.is_dir():
                raise RuntimeError(f"E1 output directory differs: {job['jobId']}")
            records.append(_bundle_record(job, panel, output, verifier))
    records.sort(key=lambda value: (value["panelId"], value["unitId"], value["armId"]))
    panels, repository_inventory = _aggregate(plans, roots, records)
    schema_path = (
        repository
        / "benchmarks/schemas/wp13/v1/exploratory-replay-inventory.schema.json"
    )
    result = {
        "schemaVersion": "1.0.0",
        "schemaId": SCHEMA_ID,
        "reportType": REPORT_TYPE,
        "claimBoundary": {
            "experiment": "postOutcomeExploratoryInstrumentationReplay",
            "interpretation": "descriptiveExecutionInventoryOnlyNotCausal",
            "h6": "readOnlyImmutableHistoricalEvidence",
            "outcomeSelection": "allFrozenTargetsNoSubset",
            "confirmatoryGate": "notApplicableCannotRescueH6",
            "mechanismConclusion": "notEvaluatedInThisTicket",
        },
        "freezeReceipt": {
            "freezeId": receipt["freezeId"],
            "frozenAtUtc": receipt["frozenAtUtc"],
            "lengthBytes": receipt_path.stat().st_size,
            "sha256": _sha256(receipt_path),
        },
        "sourceIdentity": {
            "analyzerSourceSha256": _sha256(pathlib.Path(__file__).resolve()),
            "independentVerifierSourceSha256": _sha256(verifier_path),
            "schemaSha256": _sha256(schema_path),
            "planASha256": _sha256(repository / freeze._E1_PLANS["A"]),
            "planBSha256": _sha256(repository / freeze._E1_PLANS["B"]),
        },
        "totals": {
            "pairedTargetCount": 40,
            "plannedArmRunCount": 80,
            "completedVerifiedArmRunCount": len(records),
            "failureCount": 0,
            "requestCount": sum(value["requestCount"] for value in records),
            "solverDecisionCount": sum(
                value["solverDecisionCount"] for value in records
            ),
            "retainedPortfolioEvidenceCount": sum(
                value["retainedPortfolioEvidenceCount"] for value in records
            ),
            "repositoryInventorySha256": repository_inventory,
            "totalBundleBytes": sum(value["bundleBytes"] for value in records),
            "maximumBundleBytes": max(value["bundleBytes"] for value in records),
            "maximumWallMilliseconds": max(
                value["wallMilliseconds"] for value in records
            ),
        },
        "panels": panels,
        "runs": records,
    }
    jsonschema.Draft202012Validator(
        _load(schema_path),
        format_checker=jsonschema.FormatChecker(),
    ).validate(result)
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--repository", required=True, type=pathlib.Path)
    parser.add_argument("--receipt", required=True, type=pathlib.Path)
    parser.add_argument("--record-set", required=True, type=pathlib.Path)
    parser.add_argument("--panel-a-root", required=True, type=pathlib.Path)
    parser.add_argument("--panel-b-root", required=True, type=pathlib.Path)
    parser.add_argument("--output", type=pathlib.Path)
    arguments = parser.parse_args()
    repository = arguments.repository.resolve()
    result = build_inventory(
        repository,
        arguments.receipt.resolve(),
        arguments.record_set.resolve(),
        {
            "A": arguments.panel_a_root.resolve(),
            "B": arguments.panel_b_root.resolve(),
        },
    )
    encoded = _canonical(result)
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
