#!/usr/bin/env python3
"""Inventory immutable H6 evidence and compare policy-neutral projections.

This tool is a read-only post-outcome analyzer.  It does not run RideBound, rebuild
candidates, or infer unrecorded routes.  Cross-arm alignment intentionally excludes
policy-bearing state and decision hashes.
"""

from __future__ import annotations

import argparse
import base64
import hashlib
import itertools
import json
import pathlib
import re
import sys
from collections import Counter


_ADAPTER_ROOT = pathlib.Path(__file__).parent.resolve()
if str(_ADAPTER_ROOT) not in sys.path:
    sys.path.insert(0, str(_ADAPTER_ROOT))
# Evidence analysis must not mutate the source tree through interpreter bytecode.
sys.dont_write_bytecode = True

import actual_fleetpy_medium_verify as medium_verifier  # noqa: E402


_VERIFIER_SOURCE = (_ADAPTER_ROOT / "actual_fleetpy_medium_verify.py").resolve()
if pathlib.Path(medium_verifier.__file__).resolve() != _VERIFIER_SOURCE:
    raise RuntimeError("solver evidence verifier resolved outside the adapter root")

_GENERATED_ACTION_FIELDS = {"candidateId", "publicationId"}
_HASH = re.compile(r"^[0-9a-f]{64}$")
_LABEL = re.compile(
    r"^(?P<kind>p|pb|r)-(?P<unit>d[0-9]{8}-s[0-9]+-r[0-9]+)-"
    r"(?P<arm>b1|c1|c2)-(?P<budget>tight|loose|unbounded)-s(?P<seed>[0-9]+)$"
)
_EXPECTED_PANELS = {
    "a": {
        "allowedKinds": {"p", "r"},
        "bundleCount": 60,
        "primaryPairCount": 20,
        "nonPrimaryBundleCount": 20,
        "declaredBundleInventorySha256": (
            "0467c4d8a4d7dca41e543b4e2335c9e73c0074036b564dde4ece1024516ac53d"
        ),
    },
    "b": {
        "allowedKinds": {"pb"},
        "bundleCount": 40,
        "primaryPairCount": 20,
        "nonPrimaryBundleCount": 0,
        "declaredBundleInventorySha256": (
            "e36708efa51ede63195a45cf892a407c7083d6cffe5f30dbbfd23254c0f24c9e"
        ),
    },
}
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


def _projection_hash(value):
    return hashlib.sha256(
        b"RideBound.Wp13.ObservedProjection.v1\0" + _canonical(value)
    ).hexdigest()


def _read_canonical_object(path):
    data = path.read_bytes()
    payload = data[:-1] if data.endswith(b"\n") and not data.endswith(b"\r\n") else data
    try:
        value = _loads(payload)
    except (UnicodeDecodeError, ValueError, TypeError, json.JSONDecodeError) as error:
        raise RuntimeError(f"{path}: invalid JSON") from error
    if (
        not isinstance(value, dict)
        or _canonical(value) != payload
        or data not in {payload, payload + b"\n"}
    ):
        raise RuntimeError(f"{path}: root is not a canonical JSON object")
    return value


def _strip_generated_action_fields(value):
    if isinstance(value, dict):
        return {
            key: _strip_generated_action_fields(child)
            for key, child in value.items()
            if key not in _GENERATED_ACTION_FIELDS
        }
    if isinstance(value, list):
        return [_strip_generated_action_fields(child) for child in value]
    return value


def _operational_action_projection(actions):
    """Remove generated IDs and neutralize publication-ID-derived ordering only.

    Runner request and plan actions retain their protocol order. Promise publications
    are produced as one atomic suffix but sorted on generated publication IDs, so the
    same semantic publications can appear in a different cross-policy order. Their
    original slots are filled by canonical semantic order; no other action moves.
    """

    projected = _strip_generated_action_fields(actions)
    publication_indexes = [
        index
        for index, action in enumerate(projected)
        if action.get("decisionType") == "promisePublished"
    ]
    if publication_indexes and publication_indexes != list(
        range(publication_indexes[0], len(projected))
    ):
        raise RuntimeError(
            "promisePublished actions must form the atomic decision suffix"
        )
    publications = sorted(
        (projected[index] for index in publication_indexes),
        key=_canonical,
    )
    for index, publication in zip(publication_indexes, publications):
        projected[index] = publication
    return projected


def _parse_label(label):
    match = _LABEL.fullmatch(label)
    if match is None:
        raise RuntimeError(f"unsupported H6 bundle label {label!r}")
    result = match.groupdict()
    result["seed"] = int(result["seed"])
    return result


def _prepare_bundle(directory):
    manifest_path = directory / "bundle-manifest.json"
    manifest = _read_canonical_object(manifest_path)
    if set(manifest) != {"schemaVersion", "bundleType", "files"} or (
        manifest["schemaVersion"] != "1.0.0"
        or manifest["bundleType"]
        != "ridebound-wp7-actual-fleetpy-medium-v1"
        or not isinstance(manifest["files"], list)
    ):
        raise RuntimeError(f"{directory.name}: invalid bundle manifest contract")

    declared = {}
    for record in manifest["files"]:
        if not isinstance(record, dict) or set(record) != {
            "path",
            "lengthBytes",
            "sha256",
        }:
            raise RuntimeError(f"{directory.name}: invalid bundle file record")
        relative = record["path"]
        if (
            not isinstance(relative, str)
            or not relative
            or pathlib.PurePosixPath(relative).is_absolute()
            or ".." in pathlib.PurePosixPath(relative).parts
            or relative in declared
            or isinstance(record["lengthBytes"], bool)
            or not isinstance(record["lengthBytes"], int)
            or record["lengthBytes"] < 0
            or not isinstance(record["sha256"], str)
            or _HASH.fullmatch(record["sha256"]) is None
        ):
            raise RuntimeError(f"{directory.name}: invalid bundle file identity")
        declared[relative] = record

    expected_files = {"run-00.json", "summary.json", "transcript-00.ndjson"}
    actual_files = {
        path.relative_to(directory).as_posix()
        for path in directory.rglob("*")
        if path.is_file() and path != manifest_path
    }
    if set(declared) != expected_files or actual_files != expected_files:
        raise RuntimeError(f"{directory.name}: bundle inventory differs")

    for relative in ("run-00.json", "summary.json"):
        path = directory / relative
        receipt = declared[relative]
        if (
            path.stat().st_size != receipt["lengthBytes"]
            or _sha256(path) != receipt["sha256"]
        ):
            raise RuntimeError(f"{directory.name}: receipt mismatch for {relative}")

    report = _read_canonical_object(directory / "run-00.json")
    summary = _read_canonical_object(directory / "summary.json")
    label = directory.name
    identity = _parse_label(label)
    if (
        summary.get("label") != label
        or summary.get("status") != "pass"
        or summary.get("repeatCount") != 1
        or report.get("status") != "succeeded"
        or report.get("repeat") != 0
        or report.get("semanticHash") != summary.get("semanticHash")
    ):
        raise RuntimeError(f"{label}: run/summary identity differs")
    semantic = report.get("semantic")
    source_hash = summary.get("sourceScenarioContentSha256")
    if (
        not isinstance(semantic, dict)
        or semantic.get("sourceScenarioContentSha256") != source_hash
        or not isinstance(source_hash, str)
        or _HASH.fullmatch(source_hash) is None
    ):
        raise RuntimeError(f"{label}: source scenario identity differs")

    transcript = declared["transcript-00.ndjson"]
    path = directory / "transcript-00.ndjson"
    if path.stat().st_size != transcript["lengthBytes"]:
        raise RuntimeError(f"{label}: transcript length receipt differs")
    return {
        "directory": directory,
        "label": label,
        "identity": identity,
        "sourceScenarioContentSha256": source_hash,
        "bundleManifestSha256": _sha256(manifest_path),
        "transcriptLengthBytes": transcript["lengthBytes"],
        "transcriptSha256": transcript["sha256"],
    }


def _decoded_frames(bundle):
    path = bundle["directory"] / "transcript-00.ndjson"
    file_digest = hashlib.sha256()
    byte_count = 0
    with path.open("rb") as source:
        for expected_ordinal, raw_line in enumerate(source, 1):
            file_digest.update(raw_line)
            byte_count += len(raw_line)
            if not raw_line.endswith(b"\n") or raw_line.endswith(b"\r\n"):
                raise RuntimeError(
                    f"{bundle['label']}:{expected_ordinal}: non-canonical LF framing"
                )
            line = raw_line[:-1]
            try:
                record = _loads(line)
            except (ValueError, TypeError, json.JSONDecodeError) as error:
                raise RuntimeError(
                    f"{bundle['label']}:{expected_ordinal}: invalid record JSON"
                ) from error
            if (
                not isinstance(record, dict)
                or set(record) != _RECORD_FIELDS
                or _canonical(record) != line
                or record.get("schemaVersion") != "1.0.0"
                or record.get("ordinal") != expected_ordinal
                or record.get("direction")
                not in {"adapterToRunner", "runnerToAdapter"}
                or isinstance(record.get("frameLengthBytes"), bool)
                or not isinstance(record.get("frameLengthBytes"), int)
                or record["frameLengthBytes"] < 1
                or not isinstance(record.get("frameSha256"), str)
                or _HASH.fullmatch(record["frameSha256"]) is None
                or not isinstance(record.get("frameBase64"), str)
            ):
                raise RuntimeError(
                    f"{bundle['label']}:{expected_ordinal}: invalid record contract"
                )
            try:
                frame = base64.b64decode(record["frameBase64"], validate=True)
            except (ValueError, TypeError) as error:
                raise RuntimeError(
                    f"{bundle['label']}:{expected_ordinal}: invalid frame base64"
                ) from error
            if (
                len(frame) != record["frameLengthBytes"]
                or hashlib.sha256(frame).hexdigest() != record["frameSha256"]
            ):
                raise RuntimeError(
                    f"{bundle['label']}:{expected_ordinal}: frame receipt differs"
                )
            try:
                envelope = _loads(frame)
            except (ValueError, TypeError, json.JSONDecodeError) as error:
                raise RuntimeError(
                    f"{bundle['label']}:{expected_ordinal}: invalid frame JSON"
                ) from error
            if not isinstance(envelope, dict) or _canonical(envelope) != frame:
                raise RuntimeError(
                    f"{bundle['label']}:{expected_ordinal}: frame is not canonical"
                )
            yield record["direction"], envelope
    if (
        byte_count != bundle["transcriptLengthBytes"]
        or file_digest.hexdigest() != bundle["transcriptSha256"]
    ):
        raise RuntimeError(f"{bundle['label']}: transcript file receipt differs")


def _expect_frame(frames, direction, message_type, label):
    try:
        actual_direction, envelope = next(frames)
    except StopIteration as error:
        raise RuntimeError(
            f"{label}: transcript ended before {direction}/{message_type}"
        ) from error
    if actual_direction != direction or envelope.get("messageType") != message_type:
        raise RuntimeError(
            f"{label}: expected {direction}/{message_type}; "
            f"actual={actual_direction}/{envelope.get('messageType')!r}"
        )
    return envelope


def _new_coverage():
    return {
        "decisionCount": 0,
        "evidenceVersionCounts": Counter(),
        "generationVehicleLossCount": 0,
        "retainedCandidateCountSum": 0,
        "prunedCandidateCount": 0,
        "prunedPhysicalWitnessCount": 0,
        "prunedCommitmentWitnessCount": 0,
        "prunedCandidateRouteOrScheduleRecordCount": 0,
        "selectedCandidateActionCount": 0,
        "selectedRouteActionCount": 0,
        "retainedCandidatePortfolioRecordCount": 0,
    }


def _update_coverage(coverage, solver, actions, epoch):
    medium_verifier._verify_audited_solver_evidence(solver, epoch)
    evidence = solver["executionEvidence"]
    coverage["decisionCount"] += 1
    coverage["evidenceVersionCounts"][evidence["evidenceVersion"]] += 1
    generation = evidence["generation"]
    losses = generation["vehicleLosses"]
    coverage["generationVehicleLossCount"] += len(losses)
    coverage["retainedCandidateCountSum"] += sum(
        loss["retainedCandidateCount"] for loss in losses
    )
    if "retainedCandidates" in evidence or "retainedCandidates" in generation:
        coverage["retainedCandidatePortfolioRecordCount"] += 1
    for witness in evidence["prunedCandidates"]:
        coverage["prunedCandidateCount"] += 1
        coverage["prunedPhysicalWitnessCount"] += int("physicalWitness" in witness)
        coverage["prunedCommitmentWitnessCount"] += len(
            witness["commitmentWitnesses"]
        )
        if {"route", "schedule", "stops"} & set(witness):
            coverage["prunedCandidateRouteOrScheduleRecordCount"] += 1
    _update_action_coverage(coverage, actions, epoch)


def _update_action_coverage(coverage, actions, epoch):
    for action in actions:
        if (
            not isinstance(action, dict)
            or not isinstance(action.get("decisionType"), str)
            or not action["decisionType"]
        ):
            raise RuntimeError(f"epoch {epoch}: action contract is invalid")
        payload = action.get("payload")
        if not isinstance(payload, dict):
            raise RuntimeError(f"epoch {epoch}: action payload is not an object")
        coverage["selectedCandidateActionCount"] += int("candidateId" in payload)
        coverage["selectedRouteActionCount"] += int(
            action.get("decisionType") == "vehiclePlanUpdated"
            and isinstance(payload.get("route"), dict)
        )


def _decision_records(bundle, coverage):
    frames = iter(_decoded_frames(bundle))
    _expect_frame(frames, "adapterToRunner", "hello", bundle["label"])
    _expect_frame(frames, "runnerToAdapter", "helloAck", bundle["label"])
    _expect_frame(frames, "adapterToRunner", "initializeRun", bundle["label"])
    _expect_frame(frames, "runnerToAdapter", "initialized", bundle["label"])
    saw_checkpoint = False
    saw_shutdown = False
    while True:
        try:
            direction, envelope = next(frames)
        except StopIteration:
            break
        message_type = envelope.get("messageType")
        if direction != "adapterToRunner":
            raise RuntimeError(f"{bundle['label']}: unpaired Runner output")
        if message_type == "checkpoint":
            _expect_frame(
                frames,
                "runnerToAdapter",
                "checkpoint",
                bundle["label"],
            )
            saw_checkpoint = True
            continue
        if message_type == "shutdown":
            saw_shutdown = True
            try:
                next(frames)
            except StopIteration:
                break
            raise RuntimeError(f"{bundle['label']}: shutdown is not final")
        if message_type != "eventBatch":
            raise RuntimeError(
                f"{bundle['label']}: unexpected adapter message {message_type!r}"
            )
        decision = _expect_frame(
            frames,
            "runnerToAdapter",
            "decision",
            bundle["label"],
        )
        acknowledgement = _expect_frame(
            frames,
            "adapterToRunner",
            "decisionApplied",
            bundle["label"],
        )
        for field in ("epochId", "simTimeMs"):
            if (
                decision.get(field) != envelope.get(field)
                or acknowledgement.get(field) != envelope.get(field)
            ):
                raise RuntimeError(
                    f"{bundle['label']}: {field} differs within decision triple"
                )
        epoch = envelope.get("epochId")
        sim_time = envelope.get("simTimeMs")
        events = envelope.get("payload", {}).get("events")
        payload = decision.get("payload")
        if (
            isinstance(epoch, bool)
            or not isinstance(epoch, int)
            or epoch < 1
            or isinstance(sim_time, bool)
            or not isinstance(sim_time, int)
            or sim_time < 0
            or not isinstance(events, list)
            or not events
            or not isinstance(payload, dict)
            or not isinstance(payload.get("actions"), list)
            or not isinstance(payload.get("solver"), dict)
        ):
            raise RuntimeError(f"{bundle['label']}: invalid decision triple payload")
        projected_events = []
        for event in events:
            if (
                not isinstance(event, dict)
                or not isinstance(event.get("eventType"), str)
                or not event["eventType"]
                or not isinstance(event.get("payload"), dict)
            ):
                raise RuntimeError(f"{bundle['label']}: invalid observed event")
            projected_events.append(
                {"eventType": event["eventType"], "payload": event["payload"]}
            )
        actions = payload["actions"]
        solver = payload["solver"]
        _update_coverage(coverage, solver, actions, epoch)
        yield {
            "epochId": epoch,
            "simTimeMs": sim_time,
            "observedInputProjection": {
                "epochId": epoch,
                "simTimeMs": sim_time,
                "events": projected_events,
            },
            "wireDecisionProjection": {
                "solverStatus": solver.get("status"),
                "actions": _strip_generated_action_fields(actions),
            },
            "operationalDecisionProjection": {
                "solverStatus": solver.get("status"),
                "actions": _operational_action_projection(actions),
            },
            "stateBeforeHash": payload.get("stateBeforeHash"),
            "eventTypes": [event["eventType"] for event in projected_events],
            "actionTypes": [action.get("decisionType") for action in actions],
        }
    if not saw_checkpoint or not saw_shutdown:
        raise RuntimeError(f"{bundle['label']}: terminal closure is incomplete")


def _divergence_record(classification, b1, c1):
    input_equal = (
        b1 is not None
        and c1 is not None
        and b1["observedInputProjection"] == c1["observedInputProjection"]
    )
    return {
        "classification": classification,
        "observedInputEqual": input_equal,
        "b1EpochId": None if b1 is None else b1["epochId"],
        "c1EpochId": None if c1 is None else c1["epochId"],
        "b1SimTimeMs": None if b1 is None else b1["simTimeMs"],
        "c1SimTimeMs": None if c1 is None else c1["simTimeMs"],
        "b1ObservedInputProjectionSha256": (
            None if b1 is None else _projection_hash(b1["observedInputProjection"])
        ),
        "c1ObservedInputProjectionSha256": (
            None if c1 is None else _projection_hash(c1["observedInputProjection"])
        ),
        "b1OperationalDecisionProjectionSha256": (
            None
            if b1 is None
            else _projection_hash(b1["operationalDecisionProjection"])
        ),
        "c1OperationalDecisionProjectionSha256": (
            None
            if c1 is None
            else _projection_hash(c1["operationalDecisionProjection"])
        ),
        "b1WireDecisionProjectionSha256": (
            None if b1 is None else _projection_hash(b1["wireDecisionProjection"])
        ),
        "c1WireDecisionProjectionSha256": (
            None if c1 is None else _projection_hash(c1["wireDecisionProjection"])
        ),
        "b1EventTypes": [] if b1 is None else b1["eventTypes"],
        "c1EventTypes": [] if c1 is None else c1["eventTypes"],
        "b1ActionTypes": [] if b1 is None else b1["actionTypes"],
        "c1ActionTypes": [] if c1 is None else c1["actionTypes"],
    }


def _compare_record_iterators(b1_records, c1_records):
    first_divergence = None
    equal_epochs = 0
    state_hash_mismatch_epochs = []
    wire_only_difference_epochs = []
    for b1, c1 in itertools.zip_longest(b1_records, c1_records):
        if first_divergence is not None:
            continue
        if b1 is None or c1 is None:
            first_divergence = _divergence_record(
                "transcriptLengthDivergence", b1, c1
            )
        elif b1["observedInputProjection"] != c1["observedInputProjection"]:
            first_divergence = _divergence_record("observedInputDivergence", b1, c1)
        elif (
            b1["operationalDecisionProjection"]
            != c1["operationalDecisionProjection"]
        ):
            first_divergence = _divergence_record(
                "operationalDecisionDivergenceOnEqualObservedInput", b1, c1
            )
        else:
            equal_epochs += 1
            if b1["wireDecisionProjection"] != c1["wireDecisionProjection"]:
                wire_only_difference_epochs.append(b1["epochId"])
            if b1["stateBeforeHash"] != c1["stateBeforeHash"]:
                state_hash_mismatch_epochs.append(b1["epochId"])
    if first_divergence is None:
        first_divergence = {
            "classification": "noneObserved",
            "observedInputEqual": True,
        }
    return {
        "equalObservedDecisionEpochCountBeforeDivergence": equal_epochs,
        "stateHashMismatchBeforeDivergence": bool(state_hash_mismatch_epochs),
        "firstStateHashMismatchEpoch": (
            None if not state_hash_mismatch_epochs else state_hash_mismatch_epochs[0]
        ),
        "wireOnlyDifferenceBeforeDivergence": bool(wire_only_difference_epochs),
        "firstWireOnlyDifferenceEpoch": (
            None
            if not wire_only_difference_epochs
            else wire_only_difference_epochs[0]
        ),
        "firstDivergence": first_divergence,
    }


def _compare_pair(b1, c1, coverage):
    if b1["sourceScenarioContentSha256"] != c1["sourceScenarioContentSha256"]:
        raise RuntimeError(
            f"{b1['label']}/{c1['label']}: source scenario identity differs"
        )
    comparison = _compare_record_iterators(
        _decision_records(b1, coverage),
        _decision_records(c1, coverage),
    )
    return {
        "unitId": b1["identity"]["unit"],
        "sourceScenarioContentSha256": b1["sourceScenarioContentSha256"],
        "b1Label": b1["label"],
        "c1Label": c1["label"],
        **comparison,
    }


def _finalize_coverage(coverage):
    result = dict(coverage)
    result["evidenceVersionCounts"] = dict(
        sorted(coverage["evidenceVersionCounts"].items())
    )
    portfolio_count = coverage["retainedCandidatePortfolioRecordCount"]
    result["fullRetainedCandidatePortfolio"] = (
        "notRecorded"
        if portfolio_count == 0
        else "recordedEveryDecision"
        if portfolio_count == coverage["decisionCount"]
        else "partiallyRecorded"
    )
    pruned_route_count = coverage["prunedCandidateRouteOrScheduleRecordCount"]
    result["prunedCandidateRouteOrSchedule"] = (
        "notRecorded"
        if pruned_route_count == 0
        else "recordedEveryWitness"
        if pruned_route_count == coverage["prunedCandidateCount"]
        else "partiallyRecorded"
    )
    return result


def _validate_frozen_panel_contract(
    normalized_panel_id,
    bundles,
    primary_pair_count,
    non_primary_count,
    inventory_hash,
):
    expected = _EXPECTED_PANELS[normalized_panel_id]
    if len(bundles) != expected["bundleCount"] or any(
        bundle["identity"]["kind"] not in expected["allowedKinds"]
        for bundle in bundles
    ):
        raise RuntimeError(
            f"panel {normalized_panel_id.upper()}: frozen bundle shape differs"
        )
    if (
        primary_pair_count != expected["primaryPairCount"]
        or non_primary_count != expected["nonPrimaryBundleCount"]
    ):
        raise RuntimeError(
            f"panel {normalized_panel_id.upper()}: frozen pair inventory differs"
        )
    if inventory_hash != expected["declaredBundleInventorySha256"]:
        raise RuntimeError(
            f"panel {normalized_panel_id.upper()}: frozen bundle receipts differ"
        )


def analyze_panel(panel_id, root):
    normalized_panel_id = panel_id.lower()
    if normalized_panel_id not in {"a", "b"}:
        raise RuntimeError(f"unsupported panel identity {panel_id!r}")
    root = root.resolve()
    if not root.is_dir():
        raise RuntimeError(f"panel {panel_id}: root does not exist: {root}")
    directories = sorted(
        {
            path.parent
            for path in root.rglob("bundle-manifest.json")
            if path.parent.parent == root
        },
        key=lambda path: path.name,
    )
    if not directories:
        raise RuntimeError(f"panel {panel_id}: no evidence bundles found")
    bundles = [_prepare_bundle(directory) for directory in directories]
    by_label = {bundle["label"]: bundle for bundle in bundles}
    if len(by_label) != len(bundles):
        raise RuntimeError(f"panel {panel_id}: duplicate bundle label")

    primary_kind = "p" if normalized_panel_id == "a" else "pb"
    primary = [
        bundle
        for bundle in bundles
        if bundle["identity"]["kind"] == primary_kind
        and bundle["identity"]["budget"] == "tight"
        and bundle["identity"]["seed"] == 7
        and bundle["identity"]["arm"] in {"b1", "c1"}
    ]
    grouped = {}
    for bundle in primary:
        grouped.setdefault(bundle["identity"]["unit"], {})[
            bundle["identity"]["arm"]
        ] = bundle
    if not grouped or any(set(arms) != {"b1", "c1"} for arms in grouped.values()):
        raise RuntimeError(f"panel {panel_id}: primary B1/C1 pairing is incomplete")
    non_primary_count = len(bundles) - len(primary)

    inventory = [
        {
            "label": bundle["label"],
            "bundleManifestSha256": bundle["bundleManifestSha256"],
            "transcriptSha256": bundle["transcriptSha256"],
            "sourceScenarioContentSha256": bundle["sourceScenarioContentSha256"],
        }
        for bundle in bundles
    ]
    inventory_hash = hashlib.sha256(
        b"RideBound.Wp13.H6BundleInventory.v1\0" + _canonical(inventory)
    ).hexdigest()
    _validate_frozen_panel_contract(
        normalized_panel_id,
        bundles,
        len(grouped),
        non_primary_count,
        inventory_hash,
    )

    coverage = _new_coverage()
    processed = set()
    pairs = []
    for unit_id, arms in sorted(grouped.items()):
        pairs.append(_compare_pair(arms["b1"], arms["c1"], coverage))
        processed.update({arms["b1"]["label"], arms["c1"]["label"]})
    for bundle in bundles:
        if bundle["label"] in processed:
            continue
        for _ in _decision_records(bundle, coverage):
            pass

    classifications = Counter(
        pair["firstDivergence"]["classification"] for pair in pairs
    )
    return {
        "panelId": panel_id,
        "bundleCount": len(bundles),
        "primaryPairCount": len(pairs),
        "nonPrimaryBundleCount": non_primary_count,
        "declaredBundleInventorySha256": inventory_hash,
        "evidenceCoverage": _finalize_coverage(coverage),
        "primaryAlignment": {
            "classificationCounts": dict(sorted(classifications.items())),
            "stateHashMismatchBeforeDivergencePairCount": sum(
                pair["stateHashMismatchBeforeDivergence"] for pair in pairs
            ),
            "pairs": pairs,
        },
    }


def analyze(panels):
    normalized = []
    seen = set()
    for panel_id, root in panels:
        canonical_id = panel_id.upper()
        if canonical_id not in {"A", "B"}:
            raise RuntimeError(f"unsupported panel identity {panel_id!r}")
        if canonical_id in seen:
            raise RuntimeError(f"duplicate panel identity {canonical_id!r}")
        seen.add(canonical_id)
        normalized.append((canonical_id, root))
    panel_reports = [
        analyze_panel(panel_id, root)
        for panel_id, root in sorted(normalized, key=lambda item: item[0])
    ]
    return {
        "schemaVersion": "1.0.0",
        "reportType": "ridebound-wp13-h6-evidence-inventory-v1",
        "toolIdentity": {
            "analyzerSourceSha256": _sha256(pathlib.Path(__file__).resolve()),
            "solverEvidenceVerifierSourceSha256": _sha256(
                _VERIFIER_SOURCE
            ),
        },
        "claimBoundary": {
            "analysisClass": "postOutcomeExploratory",
            "alignment": "equalObservedInputNotFullInternalState",
            "downstreamInterpretation": "trajectoryAssociatedNotCausal",
            "h6Artifacts": "readOnlyImmutableInputs",
            "confirmatoryGate": None,
        },
        "panels": panel_reports,
    }


def _panel_argument(value):
    panel_id, separator, raw_path = value.partition("=")
    if not separator or not panel_id or not raw_path:
        raise argparse.ArgumentTypeError("panel must be PANEL_ID=PATH")
    return panel_id, pathlib.Path(raw_path)


def _require_output_outside_inputs(output, panels):
    output = output.resolve()
    for panel_id, root in panels:
        resolved_root = root.resolve()
        try:
            output.relative_to(resolved_root)
        except ValueError:
            continue
        raise RuntimeError(
            f"output for panel {panel_id} must be outside immutable input root"
        )


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--panel",
        action="append",
        required=True,
        type=_panel_argument,
        help="panel identity and immutable evidence root as PANEL_ID=PATH",
    )
    parser.add_argument("--output", type=pathlib.Path)
    arguments = parser.parse_args(argv)
    try:
        if arguments.output is not None:
            _require_output_outside_inputs(arguments.output, arguments.panel)
        report = analyze(arguments.panel)
        encoded = _canonical(report) + b"\n"
        if arguments.output is None:
            sys.stdout.buffer.write(encoded)
        else:
            arguments.output.parent.mkdir(parents=True, exist_ok=True)
            arguments.output.write_bytes(encoded)
    except (OSError, RuntimeError, ValueError) as error:
        print(f"wp13_h6_evidence_inventory: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
