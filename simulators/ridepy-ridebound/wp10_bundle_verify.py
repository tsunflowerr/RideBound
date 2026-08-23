#!/usr/bin/env python3
from __future__ import annotations

import argparse
import base64
import hashlib
import json
import pathlib
import shutil
import tempfile
from typing import Any


EXPECTED_SOURCE_RECEIPT = "2b43106207b142e7ccde39482f73d551678881b869f0954cdc638ec9e7840775"
EXPECTED_COMMITMENT = "d6124f3f964d8385db381d53b75c142cf2ac870b22823d6675325c3808808beb"
EXPECTED_RUNNER_DLL = "38da6c3ad858f3747a36d6364b4ba0ec1869dfd3106606c61fbe3f11e5effb4e"
EXPECTED_WP4 = {
    "B1": "60d1e7197672d41299e5d35281bf5f42506687df230f0e852083c86570c35c85",
    "C1": "abfd1c608e3c0e4324fcc7cdc0feb7095de37057a135088162f7788a9c96ee2f",
}


class VerificationFailure(RuntimeError):
    pass


def _strict_object(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            raise VerificationFailure(f"RBWP10_VERIFY_DUPLICATE_FIELD: {key}")
        result[key] = value
    return result


def _reject_number(value: str) -> Any:
    raise VerificationFailure(f"RBWP10_VERIFY_NON_INTEGER_NUMBER: {value}")


def _read_json(path: pathlib.Path) -> Any:
    try:
        with path.open("r", encoding="utf-8", newline="") as source:
            return json.load(
                source,
                object_pairs_hook=_strict_object,
                parse_float=_reject_number,
                parse_constant=_reject_number,
            )
    except (OSError, UnicodeError, json.JSONDecodeError) as exc:
        raise VerificationFailure(f"RBWP10_VERIFY_JSON_INVALID: {path}: {exc}") from exc


def _sha256(path: pathlib.Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def decode_transcript(path: pathlib.Path) -> list[tuple[str, dict[str, Any]]]:
    decoded: list[tuple[str, dict[str, Any]]] = []
    with path.open("r", encoding="utf-8", newline="") as source:
        for ordinal, line in enumerate(source, 1):
            try:
                record = json.loads(
                    line,
                    object_pairs_hook=_strict_object,
                    parse_float=_reject_number,
                    parse_constant=_reject_number,
                )
                if record["ordinal"] != ordinal or record["schemaVersion"] != "1.0.0":
                    raise VerificationFailure("RBWP10_VERIFY_TRANSCRIPT_ORDINAL")
                frame = base64.b64decode(record["frameBase64"], validate=True)
                if len(frame) != record["frameLengthBytes"] or hashlib.sha256(frame).hexdigest() != record["frameSha256"]:
                    raise VerificationFailure("RBWP10_VERIFY_TRANSCRIPT_FRAME_HASH")
                envelope = json.loads(
                    frame,
                    object_pairs_hook=_strict_object,
                    parse_float=_reject_number,
                    parse_constant=_reject_number,
                )
            except (KeyError, TypeError, ValueError, json.JSONDecodeError) as exc:
                raise VerificationFailure(f"RBWP10_VERIFY_TRANSCRIPT_INVALID: line={ordinal}: {exc}") from exc
            decoded.append((record["direction"], envelope))
    if not decoded:
        raise VerificationFailure("RBWP10_VERIFY_TRANSCRIPT_EMPTY")
    return decoded


def verify_bundle(root: pathlib.Path, expected_arm: str) -> dict[str, Any]:
    root = root.resolve()
    manifest = _read_json(root / "bundle-manifest.json")
    if manifest.get("schemaVersion") != "1.0.0" or manifest.get("bundleType") != "ridebound-wp10-canonical-v1":
        raise VerificationFailure("RBWP10_VERIFY_MANIFEST_CONTRACT")
    entries = manifest.get("files")
    if not isinstance(entries, list):
        raise VerificationFailure("RBWP10_VERIFY_MANIFEST_FILES")
    listed: set[str] = set()
    for entry in entries:
        relative = entry.get("path") if isinstance(entry, dict) else None
        if not isinstance(relative, str) or pathlib.PurePosixPath(relative).name != relative or relative in listed:
            raise VerificationFailure("RBWP10_VERIFY_MANIFEST_PATH")
        listed.add(relative)
        path = root / relative
        if not path.is_file() or path.stat().st_size != entry.get("bytes") or _sha256(path) != entry.get("sha256"):
            raise VerificationFailure(f"RBWP10_VERIFY_FILE_HASH: {relative}")
    actual = {path.name for path in root.iterdir() if path.is_file()}
    if actual != listed | {"bundle-manifest.json"}:
        raise VerificationFailure("RBWP10_VERIFY_BUNDLE_INVENTORY")

    summary = _read_json(root / "summary.json")
    if summary.get("arm") != expected_arm or summary.get("status") != "pass":
        raise VerificationFailure("RBWP10_VERIFY_ARM_STATUS")
    if summary.get("scenarioId") != "wp10-canonical-2v5-v1" or summary.get("seed") != 7:
        raise VerificationFailure("RBWP10_VERIFY_SCENARIO_INPUT")
    inputs = summary.get("inputs", {})
    if (
        inputs.get("sourceReceiptSha256") != EXPECTED_SOURCE_RECEIPT
        or inputs.get("commitmentConfigSha256") != EXPECTED_COMMITMENT
        or inputs.get("wp4ConfigSha256") != EXPECTED_WP4[expected_arm]
        or inputs.get("runnerDllSha256") != EXPECTED_RUNNER_DLL
    ):
        raise VerificationFailure("RBWP10_VERIFY_INPUT_BINDING")
    receipts = summary.get("artifactReceipts", {})
    if receipts.get("before") != receipts.get("after") or not receipts.get("before"):
        raise VerificationFailure("RBWP10_VERIFY_ARTIFACT_DRIFT")

    transcript = decode_transcript(root / "protocol-transcript.ndjson")
    if transcript[0][1].get("messageType") != "hello" or transcript[-1][1].get("messageType") != "shutdown":
        raise VerificationFailure("RBWP10_VERIFY_TRANSCRIPT_BOUNDARY")
    hello = transcript[0][1]
    if hello.get("payload", {}).get("positionModel") != "nodeOnly":
        raise VerificationFailure("RBWP10_VERIFY_POSITION_MODEL")
    initializes = [envelope for _direction, envelope in transcript if envelope.get("messageType") == "initializeRun"]
    if len(initializes) != 1:
        raise VerificationFailure("RBWP10_VERIFY_INITIALIZE_COUNT")
    simulator = initializes[0]["payload"]["manifest"]["simulator"]
    if simulator != {
        "simulatorId": "ridepy",
        "simulatorVersion": "2.10.1",
        "upstreamCommitSha": "bf1863e49a432f2f1f6230f86b2777a5ef5b9f14",
    }:
        raise VerificationFailure("RBWP10_VERIFY_SOURCE_BINDING")

    batches = [envelope for direction, envelope in transcript if direction == "adapterToRunner" and envelope.get("messageType") == "eventBatch"]
    decisions = [envelope for direction, envelope in transcript if direction == "runnerToAdapter" and envelope.get("messageType") == "decision"]
    acknowledgements = [envelope for direction, envelope in transcript if direction == "adapterToRunner" and envelope.get("messageType") == "decisionApplied"]
    if len(batches) != len(decisions) or len(decisions) != len(acknowledgements):
        raise VerificationFailure("RBWP10_VERIFY_DECISION_RECONCILIATION")
    expected_sequence = 1
    lifecycle: dict[str, list[dict[str, Any]]] = {}
    exogenous: list[dict[str, Any]] = []
    for epoch, (batch, decision, acknowledgement) in enumerate(zip(batches, decisions, acknowledgements), 1):
        if batch.get("epochId") != epoch or decision.get("epochId") != epoch or acknowledgement.get("epochId") != epoch:
            raise VerificationFailure("RBWP10_VERIFY_EPOCH_SEQUENCE")
        if acknowledgement.get("payload", {}).get("decisionHash") != decision.get("payload", {}).get("decisionHash"):
            raise VerificationFailure("RBWP10_VERIFY_ACK_HASH")
        for event in batch["payload"]["events"]:
            if event.get("eventSeq") != expected_sequence:
                raise VerificationFailure("RBWP10_VERIFY_EVENT_SEQUENCE")
            expected_sequence += 1
            event_type = event.get("eventType")
            lifecycle.setdefault(event_type, []).append(event)
            if event_type in {"requestArrived", "travelTimesUpdated"}:
                exogenous.append(
                    {
                        "simTimeMs": batch["simTimeMs"],
                        "eventType": event_type,
                        "payload": event["payload"],
                    }
                )

    accepted = [
        action["payload"]["requestId"]
        for decision in decisions
        for action in decision["payload"]["actions"]
        if action["decisionType"] == "requestAccepted"
    ]
    request_ids = [event["payload"]["request"]["requestId"] for event in lifecycle.get("requestArrived", [])]
    booking_ids = [event["payload"]["requestId"] for event in lifecycle.get("bookingConfirmed", [])]
    boarded_ids = [event["payload"]["requestId"] for event in lifecycle.get("passengerBoarded", [])]
    alighted_ids = [event["payload"]["requestId"] for event in lifecycle.get("passengerAlighted", [])]
    if not (
        len(request_ids) == 5
        and len(set(request_ids)) == 5
        and sorted(request_ids) == sorted(accepted) == sorted(booking_ids) == sorted(boarded_ids) == sorted(alighted_ids)
    ):
        raise VerificationFailure("RBWP10_VERIFY_LIFECYCLE_RECONCILIATION")
    if len(lifecycle.get("vehicleReachedStop", [])) != 10 or len(lifecycle.get("travelTimesUpdated", [])) != 2:
        raise VerificationFailure("RBWP10_VERIFY_PHYSICAL_RECONCILIATION")

    simulator_events = _read_json(root / "simulator-events.json")
    counts: dict[str, int] = {}
    for event in simulator_events:
        counts[event["event_type"]] = counts.get(event["event_type"], 0) + 1
    if counts != summary.get("eventCounts") or counts.get("PickupEvent") != 5 or counts.get("DeliveryEvent") != 5:
        raise VerificationFailure("RBWP10_VERIFY_NATIVE_RECONCILIATION")
    if summary.get("completedRequestIds") != [0, 1, 2, 3, 4] or summary.get("nativeLifecycleCount") != 10:
        raise VerificationFailure("RBWP10_VERIFY_COMPLETION_RECONCILIATION")
    return {"summary": summary, "exogenous": exogenous, "simulatorEvents": simulator_events}


def verify_pair(b1_root: pathlib.Path, c1_root: pathlib.Path) -> dict[str, Any]:
    b1 = verify_bundle(b1_root, "B1")
    c1 = verify_bundle(c1_root, "C1")
    if b1["exogenous"] != c1["exogenous"]:
        raise VerificationFailure("RBWP10_VERIFY_PAIRED_EXOGENOUS_MISMATCH")
    b1_submissions = [event for event in b1["simulatorEvents"] if event["event_type"] == "RequestSubmissionEvent"]
    c1_submissions = [event for event in c1["simulatorEvents"] if event["event_type"] == "RequestSubmissionEvent"]
    if b1_submissions != c1_submissions:
        raise VerificationFailure("RBWP10_VERIFY_PAIRED_DEMAND_MISMATCH")
    return {
        "schemaVersion": "1.0.0",
        "status": "pass",
        "interpretation": "descriptiveOnlyCannotRescuePrimary",
        "scenarioId": "wp10-canonical-2v5-v1",
        "arrivedPerArm": 5,
        "completed": {"B1": 5, "C1": 5, "delta": 0},
        "assignmentDifference": b1["summary"]["assignments"] != c1["summary"]["assignments"],
        "pairedExogenousEventCount": len(b1["exogenous"]),
    }


def _rehash_manifest(root: pathlib.Path, filename: str) -> None:
    path = root / filename
    manifest_path = root / "bundle-manifest.json"
    manifest = _read_json(manifest_path)
    for entry in manifest["files"]:
        if entry["path"] == filename:
            entry["sha256"] = _sha256(path)
            entry["bytes"] = path.stat().st_size
    manifest_path.write_text(json.dumps(manifest, sort_keys=True, separators=(",", ":")) + "\n", encoding="utf-8", newline="\n")


def mutation_self_test(b1_root: pathlib.Path, c1_root: pathlib.Path) -> dict[str, str]:
    outcomes: dict[str, str] = {}
    with tempfile.TemporaryDirectory() as directory:
        base = pathlib.Path(directory)
        for name in ("hash", "extra", "binding", "transcript", "native"):
            target = base / name
            shutil.copytree(b1_root, target)
            try:
                if name == "hash":
                    (target / "summary.json").write_bytes((target / "summary.json").read_bytes() + b" ")
                elif name == "extra":
                    (target / "extra.txt").write_text("mutation", encoding="utf-8")
                elif name == "binding":
                    summary = _read_json(target / "summary.json")
                    summary["inputs"]["sourceReceiptSha256"] = "0" * 64
                    (target / "summary.json").write_text(json.dumps(summary, sort_keys=True, separators=(",", ":")) + "\n", encoding="utf-8", newline="\n")
                    _rehash_manifest(target, "summary.json")
                elif name == "transcript":
                    rows = (target / "protocol-transcript.ndjson").read_text(encoding="utf-8").splitlines()
                    record = json.loads(rows[0])
                    record["frameSha256"] = "0" * 64
                    rows[0] = json.dumps(record, sort_keys=True, separators=(",", ":"))
                    (target / "protocol-transcript.ndjson").write_text("\n".join(rows) + "\n", encoding="utf-8", newline="\n")
                    _rehash_manifest(target, "protocol-transcript.ndjson")
                else:
                    events = _read_json(target / "simulator-events.json")
                    events.pop()
                    (target / "simulator-events.json").write_text(json.dumps(events, sort_keys=True, separators=(",", ":")) + "\n", encoding="utf-8", newline="\n")
                    _rehash_manifest(target, "simulator-events.json")
                verify_bundle(target, "B1")
            except VerificationFailure as exc:
                outcomes[name] = str(exc).split(":", 1)[0]
            else:
                raise VerificationFailure(f"RBWP10_VERIFY_MUTATION_SURVIVED: {name}")
    verify_pair(b1_root, c1_root)
    return outcomes


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--b1", required=True, type=pathlib.Path)
    parser.add_argument("--c1", required=True, type=pathlib.Path)
    parser.add_argument("--mutation-self-test", action="store_true")
    parser.add_argument("--output", type=pathlib.Path)
    arguments = parser.parse_args()
    report = verify_pair(arguments.b1, arguments.c1)
    if arguments.mutation_self_test:
        report["mutationSelfTest"] = mutation_self_test(arguments.b1, arguments.c1)
    encoded = json.dumps(report, sort_keys=True, separators=(",", ":"))
    if arguments.output:
        arguments.output.parent.mkdir(parents=True, exist_ok=True)
        arguments.output.write_text(encoded + "\n", encoding="utf-8", newline="\n")
    print(encoded)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
