#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import pathlib
import shutil
import tempfile
from typing import Any

from wp10_bundle_verify import (
    EXPECTED_RUNNER_DLL,
    VerificationFailure,
    _read_json,
    _sha256,
    decode_transcript,
)


MANIFEST_SHA256 = "72ca34d29da7f065f6bc4d9127b9aa3dc72eb92e23d171893384ead0d416b337"
FREEZE_SHA256 = "18a74fa34f94a35ff92fbdc4ea2611e982527682760c52432419e0feef206672"
SOURCE_RECEIPT_SHA256 = "2b43106207b142e7ccde39482f73d551678881b869f0954cdc638ec9e7840775"
COMMITMENT_SHA256 = "d6124f3f964d8385db381d53b75c142cf2ac870b22823d6675325c3808808beb"
WP4_SHA256 = {
    "B1": "60d1e7197672d41299e5d35281bf5f42506687df230f0e852083c86570c35c85",
    "C1": "abfd1c608e3c0e4324fcc7cdc0feb7095de37057a135088162f7788a9c96ee2f",
}
FAILURE_JOB_ID = "travel-update-stress-r3"


def _verify_freeze(path: pathlib.Path, manifest_path: pathlib.Path) -> dict[str, Any]:
    freeze = _read_json(path)
    if _sha256(path) != FREEZE_SHA256:
        raise VerificationFailure("RBWP10_VERIFY_FREEZE_HASH")
    if (
        freeze.get("schemaVersion") != "1.0.0"
        or freeze.get("status") != "frozenBeforeOutcomeExecution"
        or freeze.get("subsetManifest")
        != {"path": manifest_path.name, "sha256": MANIFEST_SHA256}
        or freeze.get("sourceReceiptSha256") != SOURCE_RECEIPT_SHA256
        or freeze.get("configurationHashes")
        != {
            "commitment": COMMITMENT_SHA256,
            "B1": WP4_SHA256["B1"],
            "C1": WP4_SHA256["C1"],
        }
    ):
        raise VerificationFailure("RBWP10_VERIFY_FREEZE_BINDING")
    runner = freeze.get("runnerPublish", {})
    runner_files = runner.get("files")
    adapter_files = freeze.get("adapter", {}).get("files")
    if (
        not isinstance(runner_files, list)
        or not isinstance(adapter_files, list)
        or not any(
            entry.get("path") == "RideBound.Runner.dll"
            and entry.get("sha256") == EXPECTED_RUNNER_DLL
            for entry in runner_files
            if isinstance(entry, dict)
        )
    ):
        raise VerificationFailure("RBWP10_VERIFY_FREEZE_ARTIFACTS")
    return freeze


def _expected_artifact_receipts(
    freeze: dict[str, Any],
    arm: str,
) -> list[dict[str, str]]:
    rows = {
        "/evidence/wp10-source-environment-receipt-v1.json": SOURCE_RECEIPT_SHA256,
        "/workspace/benchmarks/configurations/wp10-ridepy-paired-subset-v1.json": MANIFEST_SHA256,
        "/workspace/benchmarks/configurations/wp8-drop-eta-budget-tight-v1.json": COMMITMENT_SHA256,
        (
            "/workspace/benchmarks/configurations/"
            + (
                "wp9-fleetpy-rolling-cost-audited-v1.json"
                if arm == "B1"
                else "wp9-fleetpy-ridebound-hard-vector-audited-v1.json"
            )
        ): WP4_SHA256[arm],
    }
    for entry in freeze["runnerPublish"]["files"]:
        rows[f"/runner/{entry['path']}"] = entry["sha256"]
    runtime_adapter_paths = {
        "ridebound_ridepy/__init__.py",
        "ridebound_ridepy/fleet_state.py",
        "ridebound_ridepy/mapping.py",
        "ridebound_ridepy/session.py",
        "wp10_subset_execute.py",
    }
    frozen_adapter = {
        entry["path"]: entry["sha256"] for entry in freeze["adapter"]["files"]
    }
    if not runtime_adapter_paths.issubset(frozen_adapter):
        raise VerificationFailure("RBWP10_VERIFY_FREEZE_ARTIFACTS")
    for relative in runtime_adapter_paths:
        rows[f"/workspace/simulators/ridepy-ridebound/{relative}"] = frozen_adapter[
            relative
        ]
    return [
        {"path": path, "sha256": rows[path]}
        for path in sorted(rows, key=str.casefold)
    ]


def _verify_result_inventory(
    manifest: dict[str, Any],
    root: pathlib.Path,
) -> None:
    job_ids = [job.get("jobId") for job in manifest.get("jobs", [])]
    if (
        len(job_ids) != 12
        or len(set(job_ids)) != len(job_ids)
        or FAILURE_JOB_ID not in job_ids
        or manifest.get("jobCount") != 12
        or manifest.get("armJobCount") != 24
    ):
        raise VerificationFailure("RBWP10_VERIFY_PLANNED_INVENTORY")
    actual_jobs = {path.name for path in root.iterdir() if path.is_dir()}
    root_files = [path.name for path in root.iterdir() if not path.is_dir()]
    if actual_jobs != set(job_ids) or root_files:
        raise VerificationFailure("RBWP10_VERIFY_RESULT_INVENTORY")
    for job_id in job_ids:
        job_root = root / job_id
        children = {path.name for path in job_root.iterdir() if path.is_dir()}
        files = [path.name for path in job_root.iterdir() if not path.is_dir()]
        expected = {"B1"} if job_id == FAILURE_JOB_ID else {"B1", "C1"}
        if children != expected or files:
            raise VerificationFailure("RBWP10_VERIFY_ARM_INVENTORY")
    failure_files = {
        path.name for path in (root / FAILURE_JOB_ID / "B1").iterdir()
        if path.is_file()
    }
    failure_directories = [
        path.name for path in (root / FAILURE_JOB_ID / "B1").iterdir()
        if path.is_dir()
    ]
    if failure_files != {"protocol-transcript.ndjson"} or failure_directories:
        raise VerificationFailure("RBWP10_VERIFY_FAILURE_INVENTORY")


def _verify_inventory(root: pathlib.Path, bundle_type: str) -> None:
    manifest = _read_json(root / "bundle-manifest.json")
    if manifest.get("schemaVersion") != "1.0.0" or manifest.get("bundleType") != bundle_type:
        raise VerificationFailure("RBWP10_VERIFY_MANIFEST_CONTRACT")
    listed = set()
    for entry in manifest.get("files", []):
        name = entry.get("path") if isinstance(entry, dict) else None
        if not isinstance(name, str) or pathlib.PurePosixPath(name).name != name or name in listed:
            raise VerificationFailure("RBWP10_VERIFY_MANIFEST_PATH")
        listed.add(name)
        path = root / name
        if not path.is_file() or path.stat().st_size != entry.get("bytes") or _sha256(path) != entry.get("sha256"):
            raise VerificationFailure(f"RBWP10_VERIFY_FILE_HASH: {name}")
    actual = {path.name for path in root.iterdir() if path.is_file()}
    if actual != listed | {"bundle-manifest.json"}:
        raise VerificationFailure("RBWP10_VERIFY_BUNDLE_INVENTORY")


def verify_job(
    root: pathlib.Path,
    job: dict[str, Any],
    arm: str,
    expected_seed: int,
    expected_receipts: list[dict[str, str]],
) -> dict[str, Any]:
    _verify_inventory(root, "ridebound-wp10-subset-job-v1")
    summary = _read_json(root / "summary.json")
    if (
        summary.get("status") != "pass"
        or summary.get("arm") != arm
        or summary.get("jobId") != job["jobId"]
        or summary.get("cellId") != job["cellId"]
        or summary.get("realizationId") != job["realizationId"]
        or summary.get("manifestSha256") != MANIFEST_SHA256
    ):
        raise VerificationFailure("RBWP10_VERIFY_JOB_IDENTITY")
    if summary.get("masterSeed") != expected_seed:
        raise VerificationFailure("RBWP10_VERIFY_SEED_BINDING")
    hashes = summary.get("inputHashes", {})
    if (
        hashes.get("sourceReceipt") != SOURCE_RECEIPT_SHA256
        or hashes.get("commitmentConfig") != COMMITMENT_SHA256
        or hashes.get("wp4Config") != WP4_SHA256[arm]
        or hashes.get("runnerDll") != EXPECTED_RUNNER_DLL
    ):
        raise VerificationFailure("RBWP10_VERIFY_INPUT_BINDING")
    receipts = summary.get("artifactReceipts", {})
    if (
        receipts.get("before") != expected_receipts
        or receipts.get("after") != expected_receipts
    ):
        raise VerificationFailure("RBWP10_VERIFY_ARTIFACT_DRIFT")
    transcript = decode_transcript(root / "protocol-transcript.ndjson")
    if transcript[0][1]["payload"].get("positionModel") != "nodeOnly" or transcript[-1][1].get("messageType") != "shutdown":
        raise VerificationFailure("RBWP10_VERIFY_TRANSCRIPT_BOUNDARY")
    initialize = next(envelope for _direction, envelope in transcript if envelope.get("messageType") == "initializeRun")
    if (
        initialize.get("runId") != f"wp10-{job['jobId']}-{arm.lower()}"
        or initialize.get("scenarioId") != f"wp10-{job['jobId']}"
        or initialize["payload"]["manifest"].get("masterSeed") != expected_seed
        or initialize["payload"]["manifest"].get("binarySha256")
        != EXPECTED_RUNNER_DLL
        or initialize["payload"]["manifest"]["simulator"] != {
        "simulatorId": "ridepy",
        "simulatorVersion": "2.10.1",
        "upstreamCommitSha": "bf1863e49a432f2f1f6230f86b2777a5ef5b9f14",
        }
    ):
        raise VerificationFailure("RBWP10_VERIFY_SOURCE_BINDING")
    batches = [envelope for direction, envelope in transcript if direction == "adapterToRunner" and envelope.get("messageType") == "eventBatch"]
    decisions = [envelope for direction, envelope in transcript if direction == "runnerToAdapter" and envelope.get("messageType") == "decision"]
    acknowledgements = [envelope for direction, envelope in transcript if direction == "adapterToRunner" and envelope.get("messageType") == "decisionApplied"]
    if len(batches) != len(decisions) or len(decisions) != len(acknowledgements):
        raise VerificationFailure("RBWP10_VERIFY_DECISION_RECONCILIATION")
    event_sequence = 1
    lifecycle: dict[str, list[dict[str, Any]]] = {}
    exogenous: list[dict[str, Any]] = []
    for epoch, (batch, decision, acknowledgement) in enumerate(zip(batches, decisions, acknowledgements), 1):
        if batch["epochId"] != epoch or decision["epochId"] != epoch or acknowledgement["epochId"] != epoch:
            raise VerificationFailure("RBWP10_VERIFY_EPOCH_SEQUENCE")
        if acknowledgement["payload"]["decisionHash"] != decision["payload"]["decisionHash"]:
            raise VerificationFailure("RBWP10_VERIFY_ACK_HASH")
        for event in batch["payload"]["events"]:
            if event["eventSeq"] != event_sequence:
                raise VerificationFailure("RBWP10_VERIFY_EVENT_SEQUENCE")
            event_sequence += 1
            lifecycle.setdefault(event["eventType"], []).append(event)
            if event["eventType"] in {"requestArrived", "travelTimesUpdated"}:
                exogenous.append(
                    {
                        "simTimeMs": batch["simTimeMs"],
                        "eventType": event["eventType"],
                        "payload": event["payload"],
                    }
                )
    accepted = [
        action["payload"]["requestId"]
        for decision in decisions
        for action in decision["payload"]["actions"]
        if action["decisionType"] == "requestAccepted"
    ]
    rejected = [
        action["payload"]["requestId"]
        for decision in decisions
        for action in decision["payload"]["actions"]
        if action["decisionType"] == "requestRejected"
    ]
    arrived = [event["payload"]["request"]["requestId"] for event in lifecycle.get("requestArrived", [])]
    booked = [event["payload"]["requestId"] for event in lifecycle.get("bookingConfirmed", [])]
    boarded = [event["payload"]["requestId"] for event in lifecycle.get("passengerBoarded", [])]
    alighted = [event["payload"]["requestId"] for event in lifecycle.get("passengerAlighted", [])]
    if (
        len(arrived) != summary["arrived"]
        or len(set(arrived)) != len(arrived)
        or sorted(arrived) != sorted(accepted + rejected)
        or sorted(accepted) != sorted(booked) or sorted(booked) != sorted(boarded) or sorted(boarded) != sorted(alighted)
        or len(accepted) != summary["completed"]
        or len(rejected) != summary["rejected"]
        or len(lifecycle.get("vehicleReachedStop", [])) != 2 * summary["completed"]
    ):
        raise VerificationFailure("RBWP10_VERIFY_LIFECYCLE_RECONCILIATION")
    simulator_events = _read_json(root / "simulator-events.json")
    counts: dict[str, int] = {}
    for event in simulator_events:
        counts[event["event_type"]] = counts.get(event["event_type"], 0) + 1
    if counts != summary["eventCounts"] or counts.get("PickupEvent", 0) != summary["completed"] or counts.get("DeliveryEvent", 0) != summary["completed"]:
        raise VerificationFailure("RBWP10_VERIFY_NATIVE_RECONCILIATION")
    return {"summary": summary, "exogenous": exogenous, "simulatorEvents": simulator_events}


def verify_failure(root: pathlib.Path, expected_seed: int) -> dict[str, Any]:
    transcript_path = root / "protocol-transcript.ndjson"
    transcript = decode_transcript(transcript_path)
    initialize = next(envelope for _direction, envelope in transcript if envelope.get("messageType") == "initializeRun")
    error = transcript[-1][1]
    if (
        initialize.get("runId") != "wp10-travel-update-stress-r3-b1"
        or initialize.get("scenarioId") != "wp10-travel-update-stress-r3"
        or initialize.get("payload", {}).get("manifest", {}).get("masterSeed")
        != expected_seed
        or initialize.get("payload", {}).get("manifest", {}).get("binarySha256")
        != EXPECTED_RUNNER_DLL
        or error.get("messageType") != "error"
        or error.get("payload", {}).get("code") != "INTERNAL_ERROR"
        or "PROMISE_PROJECTION_FAILED" not in error.get("payload", {}).get("message", "")
    ):
        raise VerificationFailure("RBWP10_VERIFY_EXPECTED_FAILURE_MISMATCH")
    batch = transcript[-2][1]
    event_types = [event["eventType"] for event in batch["payload"]["events"]]
    if event_types[:4] != [
        "vehicleReachedStop",
        "passengerAlighted",
        "vehicleReachedStop",
        "passengerBoarded",
    ]:
        raise VerificationFailure("RBWP10_VERIFY_FAILURE_EVENT_ORDER")
    boarded_request = next(
        event["payload"]["requestId"]
        for event in batch["payload"]["events"]
        if event["eventType"] == "passengerBoarded"
    )
    prior_pickup_etas = [
        action["payload"]["promise"]["pickupEtaMs"]
        for _direction, envelope in transcript[:-2]
        if envelope.get("messageType") == "decision"
        for action in envelope["payload"]["actions"]
        if action["decisionType"] == "promisePublished"
        and action["payload"]["promise"]["requestId"] == boarded_request
    ]
    if not prior_pickup_etas or prior_pickup_etas[-1] != 178000:
        raise VerificationFailure("RBWP10_VERIFY_FAILURE_ETA_WITNESS")
    return {
        "jobId": "travel-update-stress-r3",
        "arm": "B1",
        "code": "RBWP10_NODEONLY_CONCURRENT_MIDEDGE_UNSUPPORTED",
        "runnerErrorCode": "INTERNAL_ERROR",
        "runnerMessage": error["payload"]["message"],
        "terminalEpoch": error["epochId"],
        "simTimeMs": error["simTimeMs"],
        "boardedRequestId": boarded_request,
        "nativePickupTimeMs": batch["simTimeMs"],
        "runnerLastPublishedPickupEtaMs": prior_pickup_etas[-1],
        "nodeOnlyEtaGapMs": prior_pickup_etas[-1] - batch["simTimeMs"],
        "transcriptSha256": _sha256(transcript_path),
    }


def analyze(
    manifest_path: pathlib.Path,
    freeze_path: pathlib.Path,
    root: pathlib.Path,
) -> dict[str, Any]:
    manifest = _read_json(manifest_path)
    if _sha256(manifest_path) != MANIFEST_SHA256:
        raise VerificationFailure("RBWP10_VERIFY_MANIFEST_FREEZE_MISMATCH")
    freeze = _verify_freeze(freeze_path, manifest_path)
    _verify_result_inventory(manifest, root)
    realizations = {
        value["realizationId"]: value["masterSeed"]
        for value in manifest["realizations"]
    }
    cells: dict[str, dict[str, Any]] = {}
    pairs = []
    passed_jobs = 0
    for job in manifest["jobs"]:
        b1_path = root / job["jobId"] / "B1"
        c1_path = root / job["jobId"] / "C1"
        if job["jobId"] != FAILURE_JOB_ID:
            expected_seed = realizations[job["realizationId"]]
            b1 = verify_job(
                b1_path,
                job,
                "B1",
                expected_seed,
                _expected_artifact_receipts(freeze, "B1"),
            )
            c1 = verify_job(
                c1_path,
                job,
                "C1",
                expected_seed,
                _expected_artifact_receipts(freeze, "C1"),
            )
            if b1["exogenous"] != c1["exogenous"]:
                raise VerificationFailure("RBWP10_VERIFY_PAIRED_EXOGENOUS_MISMATCH")
            b1_submissions = [event for event in b1["simulatorEvents"] if event["event_type"] == "RequestSubmissionEvent"]
            c1_submissions = [event for event in c1["simulatorEvents"] if event["event_type"] == "RequestSubmissionEvent"]
            if b1_submissions != c1_submissions:
                raise VerificationFailure("RBWP10_VERIFY_PAIRED_DEMAND_MISMATCH")
            b1_completed = b1["summary"]["completed"]
            c1_completed = c1["summary"]["completed"]
            arrived = b1["summary"]["arrived"]
            pair = {
                "jobId": job["jobId"],
                "cellId": job["cellId"],
                "arrivedPerArm": arrived,
                "B1Completed": b1_completed,
                "C1Completed": c1_completed,
                "deltaCompleted": c1_completed - b1_completed,
            }
            pairs.append(pair)
            cell = cells.setdefault(job["cellId"], {"pairCount": 0, "arrivedPerArm": 0, "B1Completed": 0, "C1Completed": 0})
            cell["pairCount"] += 1
            cell["arrivedPerArm"] += arrived
            cell["B1Completed"] += b1_completed
            cell["C1Completed"] += c1_completed
            passed_jobs += 2
    failure_seed = realizations[next(
        job["realizationId"] for job in manifest["jobs"]
        if job["jobId"] == FAILURE_JOB_ID
    )]
    failure = verify_failure(root / FAILURE_JOB_ID / "B1", failure_seed)
    if passed_jobs != 22 or len(pairs) != 11:
        raise VerificationFailure("RBWP10_VERIFY_TERMINAL_COVERAGE")
    for cell in cells.values():
        cell["deltaCompleted"] = cell["C1Completed"] - cell["B1Completed"]
        cell["deltaServicePp"] = round(10000 * cell["deltaCompleted"] / cell["arrivedPerArm"]) / 100
    total_arrived = sum(pair["arrivedPerArm"] for pair in pairs)
    b1_total = sum(pair["B1Completed"] for pair in pairs)
    c1_total = sum(pair["C1Completed"] for pair in pairs)
    return {
        "schemaVersion": "1.0.0",
        "status": "negativeCapabilityResult",
        "interpretation": "descriptiveOnlyCannotRescuePrimary",
        "positionModel": "nodeOnly",
        "plannedArmJobs": manifest["armJobCount"],
        "terminalPassedArmJobs": passed_jobs,
        "terminalFailedArmJobs": 1,
        "notRunArmJobs": 1,
        "validPairCount": len(pairs),
        "validPairTotals": {
            "arrivedPerArm": total_arrived,
            "B1Completed": b1_total,
            "C1Completed": c1_total,
            "deltaCompleted": c1_total - b1_total,
            "deltaServicePp": round(10000 * (c1_total - b1_total) / total_arrived) / 100,
        },
        "cells": cells,
        "pairs": pairs,
        "failure": failure,
        "claimBoundary": "finitePairedSubsetOnlyNoPopulationInferenceNoH6Rescue",
    }


def _rehash(root: pathlib.Path, name: str) -> None:
    manifest_path = root / "bundle-manifest.json"
    manifest = _read_json(manifest_path)
    target = root / name
    for entry in manifest["files"]:
        if entry["path"] == name:
            entry["sha256"] = _sha256(target)
            entry["bytes"] = target.stat().st_size
    manifest_path.write_text(json.dumps(manifest, sort_keys=True, separators=(",", ":")) + "\n", encoding="utf-8", newline="\n")


def mutation_self_test(
    manifest_path: pathlib.Path,
    freeze_path: pathlib.Path,
    root: pathlib.Path,
) -> dict[str, str]:
    manifest = _read_json(manifest_path)
    freeze = _verify_freeze(freeze_path, manifest_path)
    job = manifest["jobs"][0]
    expected_seed = next(
        value["masterSeed"] for value in manifest["realizations"]
        if value["realizationId"] == job["realizationId"]
    )
    source = root / job["jobId"] / "B1"
    outcomes = {}
    with tempfile.TemporaryDirectory() as directory:
        for mutation in (
            "hash",
            "extra",
            "binding",
            "seed",
            "runner",
            "transcript",
            "native",
        ):
            target = pathlib.Path(directory) / mutation
            shutil.copytree(source, target)
            try:
                if mutation == "hash":
                    (target / "summary.json").write_bytes((target / "summary.json").read_bytes() + b" ")
                elif mutation == "extra":
                    (target / "extra.txt").write_text("mutation", encoding="utf-8")
                elif mutation == "binding":
                    summary = _read_json(target / "summary.json")
                    summary["manifestSha256"] = "0" * 64
                    (target / "summary.json").write_text(json.dumps(summary, sort_keys=True, separators=(",", ":")) + "\n", encoding="utf-8", newline="\n")
                    _rehash(target, "summary.json")
                elif mutation == "seed":
                    summary = _read_json(target / "summary.json")
                    summary["masterSeed"] += 1
                    (target / "summary.json").write_text(json.dumps(summary, sort_keys=True, separators=(",", ":")) + "\n", encoding="utf-8", newline="\n")
                    _rehash(target, "summary.json")
                elif mutation == "runner":
                    summary = _read_json(target / "summary.json")
                    summary["inputHashes"]["runnerDll"] = "0" * 64
                    (target / "summary.json").write_text(json.dumps(summary, sort_keys=True, separators=(",", ":")) + "\n", encoding="utf-8", newline="\n")
                    _rehash(target, "summary.json")
                elif mutation == "transcript":
                    rows = (target / "protocol-transcript.ndjson").read_text(encoding="utf-8").splitlines()
                    record = json.loads(rows[0])
                    record["frameSha256"] = "0" * 64
                    rows[0] = json.dumps(record, sort_keys=True, separators=(",", ":"))
                    (target / "protocol-transcript.ndjson").write_text("\n".join(rows) + "\n", encoding="utf-8", newline="\n")
                    _rehash(target, "protocol-transcript.ndjson")
                else:
                    events = _read_json(target / "simulator-events.json")
                    events.pop()
                    (target / "simulator-events.json").write_text(json.dumps(events, sort_keys=True, separators=(",", ":")) + "\n", encoding="utf-8", newline="\n")
                    _rehash(target, "simulator-events.json")
                verify_job(
                    target,
                    job,
                    "B1",
                    expected_seed,
                    _expected_artifact_receipts(freeze, "B1"),
                )
            except VerificationFailure as exc:
                outcomes[mutation] = str(exc).split(":", 1)[0]
            else:
                raise VerificationFailure(f"RBWP10_VERIFY_MUTATION_SURVIVED: {mutation}")
    return outcomes


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--manifest", required=True, type=pathlib.Path)
    parser.add_argument("--freeze", required=True, type=pathlib.Path)
    parser.add_argument("--results-root", required=True, type=pathlib.Path)
    parser.add_argument("--mutation-self-test", action="store_true")
    parser.add_argument("--output", type=pathlib.Path)
    arguments = parser.parse_args()
    report = analyze(arguments.manifest, arguments.freeze, arguments.results_root)
    if arguments.mutation_self_test:
        report["mutationSelfTest"] = mutation_self_test(
            arguments.manifest,
            arguments.freeze,
            arguments.results_root,
        )
    encoded = json.dumps(report, sort_keys=True, separators=(",", ":"))
    if arguments.output:
        arguments.output.parent.mkdir(parents=True, exist_ok=True)
        arguments.output.write_text(encoded + "\n", encoding="utf-8", newline="\n")
    print(encoded)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
