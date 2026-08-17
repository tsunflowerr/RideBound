#!/usr/bin/env python3
"""Independently verify a WP7 actual-FleetPy medium evidence bundle."""

from __future__ import annotations

import argparse
import base64
import hashlib
import json
import pathlib
import re


_HASH = re.compile(r"^[0-9a-f]{64}$")
_RECORD_FIELDS = {
    "schemaVersion",
    "ordinal",
    "direction",
    "frameLengthBytes",
    "frameSha256",
    "frameBase64",
}


def _strict_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"duplicate JSON field {key!r}")
        result[key] = value
    return result


def _reject_number(text):
    raise ValueError(f"non-integer JSON number {text!r}")


def _loads(data):
    return json.loads(
        data,
        object_pairs_hook=_strict_object,
        parse_float=_reject_number,
        parse_constant=_reject_number,
    )


def _canonical(value):
    return json.dumps(
        value,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
        allow_nan=False,
    ).encode("utf-8")


def _sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _read_json(path):
    with path.open("rb") as source:
        value = _loads(source.read())
    if not isinstance(value, dict):
        raise RuntimeError(f"{path.name}: root is not an object")
    return value


def decode_transcript(path):
    decoded = []
    with path.open("rb") as source:
        for expected_ordinal, raw_line in enumerate(source, 1):
            if not raw_line.endswith(b"\n") or raw_line.endswith(b"\r\n"):
                raise RuntimeError(
                    f"{path.name}:{expected_ordinal}: non-canonical LF framing"
                )
            line = raw_line[:-1]
            record = _loads(line)
            if not isinstance(record, dict) or set(record) != _RECORD_FIELDS:
                raise RuntimeError(
                    f"{path.name}:{expected_ordinal}: transcript fields differ"
                )
            if _canonical(record) != line:
                raise RuntimeError(
                    f"{path.name}:{expected_ordinal}: record is not canonical JSON"
                )
            if (
                record["schemaVersion"] != "1.0.0"
                or record["ordinal"] != expected_ordinal
                or record["direction"]
                not in {"adapterToRunner", "runnerToAdapter"}
                or isinstance(record["frameLengthBytes"], bool)
                or not isinstance(record["frameLengthBytes"], int)
                or record["frameLengthBytes"] < 1
                or not isinstance(record["frameSha256"], str)
                or _HASH.fullmatch(record["frameSha256"]) is None
                or not isinstance(record["frameBase64"], str)
            ):
                raise RuntimeError(
                    f"{path.name}:{expected_ordinal}: invalid transcript metadata"
                )
            try:
                frame = base64.b64decode(
                    record["frameBase64"],
                    validate=True,
                )
            except (ValueError, TypeError) as exc:
                raise RuntimeError(
                    f"{path.name}:{expected_ordinal}: invalid frame base64"
                ) from exc
            if (
                len(frame) != record["frameLengthBytes"]
                or hashlib.sha256(frame).hexdigest() != record["frameSha256"]
            ):
                raise RuntimeError(
                    f"{path.name}:{expected_ordinal}: frame length/hash mismatch"
                )
            envelope = _loads(frame)
            if not isinstance(envelope, dict) or _canonical(envelope) != frame:
                raise RuntimeError(
                    f"{path.name}:{expected_ordinal}: frame is not canonical JSON"
                )
            decoded.append((record["direction"], envelope))
    if not decoded:
        raise RuntimeError(f"{path.name}: transcript is empty")
    return decoded


def _expect(frames, index, direction, message_type):
    try:
        actual_direction, envelope = frames[index]
    except IndexError as exc:
        raise RuntimeError(
            f"transcript ended before {direction}/{message_type}"
        ) from exc
    if actual_direction != direction or envelope.get("messageType") != message_type:
        raise RuntimeError(
            f"frame {index + 1}: expected {direction}/{message_type}; "
            f"actual={actual_direction}/{envelope.get('messageType')!r}"
        )
    return envelope


def verify_transcript(path, report):
    frames = decode_transcript(path)
    hello = _expect(frames, 0, "adapterToRunner", "hello")
    hello_ack = _expect(frames, 1, "runnerToAdapter", "helloAck")
    initialize = _expect(frames, 2, "adapterToRunner", "initializeRun")
    initialized = _expect(frames, 3, "runnerToAdapter", "initialized")
    if hello.get("schemaVersion") != "1.0.0" or hello_ack.get(
        "schemaVersion"
    ) != "1.0.0":
        raise RuntimeError("hello schema negotiation drifted")
    run_id = initialize.get("runId")
    scenario_id = initialize.get("scenarioId")
    if (
        not isinstance(run_id, str)
        or not run_id
        or not isinstance(scenario_id, str)
        or not scenario_id
        or initialized.get("runId") != run_id
        or initialized.get("scenarioId") != scenario_id
    ):
        raise RuntimeError("initialize identity mismatch")
    manifest_hash = initialized.get("payload", {}).get("manifestHash")
    if manifest_hash != report["semantic"]["manifestHash"]:
        raise RuntimeError("initialized/report manifest hash mismatch")

    next_epoch = 1
    next_event_sequence = 1
    prior_sim_time = 0
    prior_decision_hash = None
    prior_state_after = None
    lifecycle = {
        "requestArrived": [],
        "bookingConfirmed": [],
        "passengerBoarded": [],
        "passengerAlighted": [],
        "requestCancelledBeforeAcceptance": [],
        "requestCancelledAfterAcceptance": [],
    }
    accepted = []
    rejected = []
    publications = []
    checkpoint = None
    index = 4
    while index < len(frames):
        direction, envelope = frames[index]
        message_type = envelope.get("messageType")
        if direction != "adapterToRunner":
            raise RuntimeError(f"frame {index + 1}: unexpected unpaired Runner output")
        if message_type == "shutdown":
            if index != len(frames) - 1:
                raise RuntimeError("shutdown is not the final transcript frame")
            index += 1
            break
        if message_type == "checkpoint":
            checkpoint = _expect(
                frames,
                index + 1,
                "runnerToAdapter",
                "checkpoint",
            )
            index += 2
            continue
        if message_type != "eventBatch":
            raise RuntimeError(
                f"frame {index + 1}: unexpected adapter message {message_type!r}"
            )
        batch = envelope
        if (
            batch.get("runId") != run_id
            or batch.get("scenarioId") != scenario_id
            or batch.get("epochId") != next_epoch
            or isinstance(batch.get("simTimeMs"), bool)
            or not isinstance(batch.get("simTimeMs"), int)
            or batch["simTimeMs"] < prior_sim_time
        ):
            raise RuntimeError(f"epoch {next_epoch}: context/time is not gapless")
        events = batch.get("payload", {}).get("events")
        if not isinstance(events, list) or not events:
            raise RuntimeError(f"epoch {next_epoch}: event list is empty/invalid")
        for event in events:
            if event.get("eventSeq") != next_event_sequence:
                raise RuntimeError(
                    f"event sequence gap: expected={next_event_sequence}; "
                    f"actual={event.get('eventSeq')!r}"
                )
            event_type = event.get("eventType")
            payload = event.get("payload")
            if not isinstance(payload, dict):
                raise RuntimeError(f"event {next_event_sequence}: invalid payload")
            if event_type in lifecycle:
                request_id = (
                    payload.get("request", {}).get("requestId")
                    if event_type == "requestArrived"
                    else payload.get("requestId")
                )
                if not isinstance(request_id, str) or not request_id:
                    raise RuntimeError(
                        f"event {next_event_sequence}: lifecycle identity missing"
                    )
                lifecycle[event_type].append(request_id)
            next_event_sequence += 1

        decision = _expect(
            frames,
            index + 1,
            "runnerToAdapter",
            "decision",
        )
        if any(
            decision.get(field) != batch.get(field)
            for field in ("runId", "scenarioId", "epochId", "simTimeMs")
        ):
            raise RuntimeError(f"epoch {next_epoch}: decision context mismatch")
        payload = decision.get("payload", {})
        decision_hash = payload.get("decisionHash")
        if not isinstance(decision_hash, str) or _HASH.fullmatch(decision_hash) is None:
            raise RuntimeError(f"epoch {next_epoch}: invalid decision hash")
        if prior_decision_hash is not None and payload.get(
            "previousDecisionHash"
        ) != prior_decision_hash:
            raise RuntimeError(f"epoch {next_epoch}: decision hash chain broke")
        if prior_state_after is not None and payload.get(
            "stateBeforeHash"
        ) != prior_state_after:
            raise RuntimeError(f"epoch {next_epoch}: state hash chain broke")
        state_after = payload.get("stateAfterHash")
        if not isinstance(state_after, str) or _HASH.fullmatch(state_after) is None:
            raise RuntimeError(f"epoch {next_epoch}: invalid stateAfterHash")
        actions = payload.get("actions")
        if not isinstance(actions, list):
            raise RuntimeError(f"epoch {next_epoch}: actions are invalid")
        for action in actions:
            action_type = action.get("decisionType")
            action_payload = action.get("payload", {})
            if action_type == "requestAccepted":
                accepted.append(action_payload.get("requestId"))
            elif action_type == "requestRejected":
                rejected.append(action_payload.get("requestId"))
            elif action_type == "promisePublished":
                promise = action_payload.get("promise", {})
                publications.append(
                    {
                        "requestId": promise.get("requestId"),
                        "publicationId": action_payload.get("publicationId"),
                        "promiseVersion": action_payload.get("promiseVersion"),
                        "reasonCode": action_payload.get("reasonCode"),
                        "sourceEventSeq": action_payload.get("sourceEventSeq"),
                    }
                )
        acknowledgement = _expect(
            frames,
            index + 2,
            "adapterToRunner",
            "decisionApplied",
        )
        if (
            any(
                acknowledgement.get(field) != decision.get(field)
                for field in ("runId", "scenarioId", "epochId", "simTimeMs")
            )
            or acknowledgement.get("payload") != {"decisionHash": decision_hash}
        ):
            raise RuntimeError(f"epoch {next_epoch}: ACK is not exact")
        prior_decision_hash = decision_hash
        prior_state_after = state_after
        prior_sim_time = batch["simTimeMs"]
        next_epoch += 1
        index += 3

    if index != len(frames) or checkpoint is None:
        raise RuntimeError("transcript lacks final checkpoint/shutdown closure")
    content = checkpoint.get("payload", {}).get("content")
    if not isinstance(content, dict):
        raise RuntimeError("final checkpoint content is missing")
    if (
        content.get("manifestHash") != manifest_hash
        or content.get("previousDecisionHash") != prior_decision_hash
        or content.get("stateHash") != prior_state_after
        or content.get("appliedEpoch") != next_epoch - 1
        or content.get("nextEventSeq") != next_event_sequence
        or content.get("simTimeMs") != prior_sim_time
    ):
        raise RuntimeError("final checkpoint identity/state boundary mismatch")

    semantic = report["semantic"]
    if (
        semantic["requestCount"] != 128
        or semantic["nextEpoch"] != next_epoch
        or semantic["nextEventSeq"] != next_event_sequence
        or semantic["finalSimulationTimeMs"] != prior_sim_time
        or semantic["travelSnapshotVersion"] != 1
    ):
        raise RuntimeError("report/transcript terminal counters differ")
    arrived = lifecycle["requestArrived"]
    booked = lifecycle["bookingConfirmed"]
    boarded = lifecycle["passengerBoarded"]
    alighted = lifecycle["passengerAlighted"]
    if not (
        len(arrived)
        == len(set(arrived))
        == len(booked)
        == len(set(booked))
        == len(boarded)
        == len(set(boarded))
        == len(alighted)
        == len(set(alighted))
        == semantic["requestCount"]
        and set(arrived) == set(booked) == set(boarded) == set(alighted)
        and not lifecycle["requestCancelledBeforeAcceptance"]
        and not lifecycle["requestCancelledAfterAcceptance"]
        and len(accepted) == len(set(accepted)) == semantic["requestCount"]
        and set(accepted) == set(arrived)
        and not rejected
    ):
        raise RuntimeError("request lifecycle/outcome conservation failed")
    if len(publications) != semantic["publicationCount"]:
        raise RuntimeError("publication count differs from raw decisions")
    publication_digest = hashlib.sha256(_canonical(publications)).hexdigest()
    if publication_digest != semantic["publicationDigest"]:
        raise RuntimeError("publication digest differs from raw decisions")
    online_state = content.get("onlineState")
    if (
        not isinstance(online_state, dict)
        or len(online_state.get("requests", [])) != semantic["requestCount"]
        or any(
            request.get("lifecycle") != "completed"
            for request in online_state.get("requests", [])
        )
        or len(online_state.get("vehicles", [])) != 32
    ):
        raise RuntimeError("terminal checkpoint did not drain requests/fleet")
    return {
        "frameCount": len(frames),
        "epochCount": next_epoch - 1,
        "eventCount": next_event_sequence - 1,
        "requestCount": len(arrived),
        "publicationCount": len(publications),
        "checkpointHash": checkpoint["payload"].get("checkpointHash"),
    }


def verify_bundle(directory):
    manifest_path = directory / "bundle-manifest.json"
    manifest = _read_json(manifest_path)
    if set(manifest) != {"schemaVersion", "bundleType", "files"} or (
        manifest["schemaVersion"] != "1.0.0"
        or manifest["bundleType"] != "ridebound-wp7-actual-fleetpy-medium-v1"
        or not isinstance(manifest["files"], list)
    ):
        raise RuntimeError("bundle manifest contract differs")
    declared = {}
    for item in manifest["files"]:
        if set(item) != {"path", "lengthBytes", "sha256"}:
            raise RuntimeError("bundle manifest file record differs")
        relative = item["path"]
        if (
            not isinstance(relative, str)
            or not relative
            or pathlib.PurePosixPath(relative).is_absolute()
            or ".." in pathlib.PurePosixPath(relative).parts
            or relative in declared
        ):
            raise RuntimeError("bundle manifest path is invalid/duplicate")
        declared[relative] = item
    actual = {
        path.relative_to(directory).as_posix()
        for path in directory.rglob("*")
        if path.is_file() and path != manifest_path
    }
    if actual != set(declared):
        raise RuntimeError(
            f"bundle inventory differs: missing={sorted(set(declared)-actual)!r}; "
            f"extra={sorted(actual-set(declared))!r}"
        )
    for relative, item in declared.items():
        path = directory / pathlib.PurePosixPath(relative)
        if (
            path.stat().st_size != item["lengthBytes"]
            or _sha256(path) != item["sha256"]
        ):
            raise RuntimeError(f"bundle artifact drift: {relative}")
    summary = _read_json(directory / "summary.json")
    if summary.get("status") != "pass" or summary.get("repeatCount") < 1:
        raise RuntimeError("medium summary is not a pass")
    runs = []
    for repeat in range(summary["repeatCount"]):
        report = _read_json(directory / f"run-{repeat:02d}.json")
        if (
            report.get("status") != "succeeded"
            or report.get("repeat") != repeat
            or report.get("semanticHash") != summary.get("semanticHash")
            or report.get("artifactReceiptsEqual") is not True
        ):
            raise RuntimeError(f"run-{repeat:02d}: report/summary mismatch")
        runs.append(
            verify_transcript(
                directory / f"transcript-{repeat:02d}.ndjson",
                report,
            )
        )
    return {
        "schemaVersion": "1.0.0",
        "status": "pass",
        "repeatCount": len(runs),
        "semanticHash": summary["semanticHash"],
        "runs": runs,
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--bundle", required=True, type=pathlib.Path)
    arguments = parser.parse_args()
    result = verify_bundle(arguments.bundle.resolve())
    print(json.dumps(result, sort_keys=True, separators=(",", ":")))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
