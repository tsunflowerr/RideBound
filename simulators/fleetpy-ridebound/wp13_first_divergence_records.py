#!/usr/bin/env python3
"""Project the frozen WP13-002 inventory into versioned first-divergence records."""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import re
import sys
from collections import Counter

import jsonschema


_ROOT = pathlib.Path(__file__).parent.resolve()
_SCHEMA = (
    _ROOT.parent.parent
    / "benchmarks"
    / "schemas"
    / "wp13"
    / "v1"
    / "first-divergence-record-set.schema.json"
).resolve()
_SOURCE_REPORT_LENGTH = 73_102
_SOURCE_REPORT_SHA256 = (
    "6d36bc6e781f9fa5c32a024c3f5350271b806f43a7418f148ef5138fa1fff63e"
)
_SOURCE_ANALYZER_SHA256 = (
    "0563ef2495550345c587ddbe07cf47a0ff88c38bf2b618dae6723c881ab1ab3b"
)
_SOLVER_VERIFIER_SHA256 = (
    "3eebec96b8370db2c4879adeaede3e67b7344571299a496953afcbc599dd93e5"
)
_PANEL_INVENTORIES = {
    "A": "0467c4d8a4d7dca41e543b4e2335c9e73c0074036b564dde4ece1024516ac53d",
    "B": "e36708efa51ede63195a45cf892a407c7083d6cffe5f30dbbfd23254c0f24c9e",
}
_CLASSIFICATIONS = (
    "noneObserved",
    "observedInputDivergence",
    "operationalDecisionDivergenceOnEqualObservedInput",
    "transcriptLengthDivergence",
)
_PAIR_FIELDS = {
    "unitId",
    "sourceScenarioContentSha256",
    "b1Label",
    "c1Label",
    "equalObservedDecisionEpochCountBeforeDivergence",
    "stateHashMismatchBeforeDivergence",
    "firstStateHashMismatchEpoch",
    "wireOnlyDifferenceBeforeDivergence",
    "firstWireOnlyDifferenceEpoch",
    "firstDivergence",
}
_DIVERGENCE_FIELDS = {
    "classification",
    "observedInputEqual",
    "b1EpochId",
    "c1EpochId",
    "b1SimTimeMs",
    "c1SimTimeMs",
    "b1ObservedInputProjectionSha256",
    "c1ObservedInputProjectionSha256",
    "b1OperationalDecisionProjectionSha256",
    "c1OperationalDecisionProjectionSha256",
    "b1WireDecisionProjectionSha256",
    "c1WireDecisionProjectionSha256",
    "b1EventTypes",
    "c1EventTypes",
    "b1ActionTypes",
    "c1ActionTypes",
}
_HASH = re.compile(r"^[0-9a-f]{64}$")
_UNIT = re.compile(r"^d[0-9]{8}-s[0-9]+-r[0-9]+$")
_SAFE_INTEGER_MAXIMUM = 9_007_199_254_740_991

sys.dont_write_bytecode = True


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


def _sha256_bytes(value):
    return hashlib.sha256(value).hexdigest()


def _source_identity():
    return {
        "schemaVersion": "1.0.0",
        "reportType": "ridebound-wp13-h6-evidence-inventory-v1",
        "lengthBytes": _SOURCE_REPORT_LENGTH,
        "sha256": _SOURCE_REPORT_SHA256,
        "analyzerSourceSha256": _SOURCE_ANALYZER_SHA256,
        "solverEvidenceVerifierSourceSha256": _SOLVER_VERIFIER_SHA256,
    }


def _claim_boundary():
    return {
        "alignment": "equalObservedInputNotFullInternalState",
        "interpretation": "descriptiveFirstDivergenceNotCausal",
        "h6Artifacts": "readOnlyImmutableInputs",
    }


def _read_source_report(path):
    data = path.read_bytes()
    if len(data) != _SOURCE_REPORT_LENGTH or _sha256_bytes(data) != (
        _SOURCE_REPORT_SHA256
    ):
        raise RuntimeError("source inventory report identity differs from WP13-002")
    if not data.endswith(b"\n") or data.endswith(b"\r\n"):
        raise RuntimeError("source inventory report framing is not canonical LF")
    try:
        report = _loads(data[:-1])
    except (UnicodeDecodeError, ValueError, TypeError, json.JSONDecodeError) as error:
        raise RuntimeError("source inventory report JSON is invalid") from error
    if _canonical(report) != data[:-1]:
        raise RuntimeError("source inventory report is not canonical JSON")
    return report


def _load_schema():
    data = _SCHEMA.read_bytes()
    try:
        schema = json.loads(data, object_pairs_hook=_strict_object)
        jsonschema.Draft202012Validator.check_schema(schema)
    except (ValueError, TypeError, json.JSONDecodeError, jsonschema.SchemaError) as error:
        raise RuntimeError("first-divergence schema is invalid") from error
    if schema.get("$id") != (
        "https://ridebound.local/schemas/wp13/v1/"
        "first-divergence-record-set.schema.json"
    ):
        raise RuntimeError("first-divergence schema identity differs")
    return schema, _sha256_bytes(data)


def _require_hash(value, field):
    if not isinstance(value, str) or _HASH.fullmatch(value) is None:
        raise RuntimeError(f"{field} is not an exact lowercase SHA-256")
    return value


def _require_nonnegative(value, field, positive=False):
    minimum = 1 if positive else 0
    if (
        isinstance(value, bool)
        or not isinstance(value, int)
        or value < minimum
        or value > _SAFE_INTEGER_MAXIMUM
    ):
        raise RuntimeError(f"{field} is not a canonical integer")
    return value


def _arm_evidence(divergence, arm):
    epoch = divergence[f"{arm}EpochId"]
    values = [
        divergence[f"{arm}SimTimeMs"],
        divergence[f"{arm}ObservedInputProjectionSha256"],
        divergence[f"{arm}OperationalDecisionProjectionSha256"],
        divergence[f"{arm}WireDecisionProjectionSha256"],
        divergence[f"{arm}EventTypes"],
        divergence[f"{arm}ActionTypes"],
    ]
    if epoch is None:
        if any(value not in (None, []) for value in values):
            raise RuntimeError(f"{arm} evidence exists without an epoch")
        return None
    _require_nonnegative(epoch, f"{arm}EpochId", positive=True)
    sim_time = _require_nonnegative(values[0], f"{arm}SimTimeMs")
    for field, value in zip(
        (
            "ObservedInputProjectionSha256",
            "OperationalDecisionProjectionSha256",
            "WireDecisionProjectionSha256",
        ),
        values[1:4],
    ):
        _require_hash(value, f"{arm}{field}")
    for field, value in (("EventTypes", values[4]), ("ActionTypes", values[5])):
        if not isinstance(value, list) or any(
            not isinstance(item, str) or not item for item in value
        ):
            raise RuntimeError(f"{arm}{field} is invalid")
    return {
        "epochId": epoch,
        "simTimeMs": sim_time,
        "observedInputProjectionSha256": values[1],
        "operationalDecisionProjectionSha256": values[2],
        "wireDecisionProjectionSha256": values[3],
        "eventTypes": values[4],
        "actionTypes": values[5],
    }


def _project_divergence(value):
    classification = value.get("classification")
    if classification not in _CLASSIFICATIONS:
        raise RuntimeError("first-divergence classification is invalid")
    if classification == "noneObserved":
        if set(value) != {"classification", "observedInputEqual"} or (
            value["observedInputEqual"] is not True
        ):
            raise RuntimeError("noneObserved source shape is invalid")
        return {
            "classification": classification,
            "observedInputRelation": "equalThroughTranscript",
        }
    if set(value) != _DIVERGENCE_FIELDS:
        raise RuntimeError("first-divergence source fields differ")
    b1 = _arm_evidence(value, "b1")
    c1 = _arm_evidence(value, "c1")
    expected_relation = {
        "observedInputDivergence": (False, "different"),
        "operationalDecisionDivergenceOnEqualObservedInput": (True, "equal"),
        "transcriptLengthDivergence": (False, "notComparable"),
    }[classification]
    if value["observedInputEqual"] is not expected_relation[0]:
        raise RuntimeError("observed-input relation contradicts classification")
    if classification == "transcriptLengthDivergence":
        if (b1 is None) == (c1 is None):
            raise RuntimeError("length divergence must have exactly one arm evidence")
    elif b1 is None or c1 is None:
        raise RuntimeError("paired divergence is missing arm evidence")
    if classification == "operationalDecisionDivergenceOnEqualObservedInput":
        if (
            b1["epochId"] != c1["epochId"]
            or b1["simTimeMs"] != c1["simTimeMs"]
            or b1["observedInputProjectionSha256"]
            != c1["observedInputProjectionSha256"]
            or b1["operationalDecisionProjectionSha256"]
            == c1["operationalDecisionProjectionSha256"]
        ):
            raise RuntimeError("equal-input operational divergence is contradictory")
    if classification == "observedInputDivergence" and (
        b1["observedInputProjectionSha256"]
        == c1["observedInputProjectionSha256"]
    ):
        raise RuntimeError("observed-input divergence has equal projection hashes")
    result = {
        "classification": classification,
        "observedInputRelation": expected_relation[1],
    }
    if b1 is not None:
        result["b1Evidence"] = b1
    if c1 is not None:
        result["c1Evidence"] = c1
    return result


def _project_record(
    panel_id,
    panel_inventory,
    pair,
    contract_identity,
):
    if set(pair) != _PAIR_FIELDS:
        raise RuntimeError(f"panel {panel_id}: pair fields differ")
    unit = pair["unitId"]
    if not isinstance(unit, str) or _UNIT.fullmatch(unit) is None:
        raise RuntimeError(f"panel {panel_id}: paired unit identity is invalid")
    kind = "p" if panel_id == "A" else "pb"
    expected_b1 = f"{kind}-{unit}-b1-tight-s7"
    expected_c1 = f"{kind}-{unit}-c1-tight-s7"
    if pair["b1Label"] != expected_b1 or pair["c1Label"] != expected_c1:
        raise RuntimeError(f"panel {panel_id}/{unit}: arm labels differ")
    _require_hash(pair["sourceScenarioContentSha256"], "source scenario hash")
    prefix_count = _require_nonnegative(
        pair["equalObservedDecisionEpochCountBeforeDivergence"],
        "equal prefix count",
    )
    result = {
        "schemaVersion": "1.0.0",
        "recordType": "ridebound-wp13-first-divergence-v1",
        "panelId": panel_id,
        "unitId": unit,
        "sourceScenarioContentSha256": pair["sourceScenarioContentSha256"],
        "b1Label": pair["b1Label"],
        "c1Label": pair["c1Label"],
        "alignmentContractId": "equal-observed-input-operational-v1",
        "equalObservedDecisionEpochCountBeforeDivergence": prefix_count,
        "stateHashMismatchBeforeDivergence": pair[
            "stateHashMismatchBeforeDivergence"
        ],
        "wireOnlyDifferenceBeforeDivergence": pair[
            "wireOnlyDifferenceBeforeDivergence"
        ],
        "firstDivergence": _project_divergence(pair["firstDivergence"]),
        "evidenceBinding": {
            "sourceInventoryReportSha256": _SOURCE_REPORT_SHA256,
            "sourceInventoryReportLengthBytes": _SOURCE_REPORT_LENGTH,
            "panelBundleInventorySha256": panel_inventory,
        },
        "contractIdentity": contract_identity,
        "claimBoundary": _claim_boundary(),
    }
    divergence = result["firstDivergence"]
    for arm in ("b1", "c1"):
        evidence = divergence.get(f"{arm}Evidence")
        if evidence is not None and evidence["epochId"] != prefix_count + 1:
            raise RuntimeError(
                f"panel {panel_id}/{unit}: divergence epoch differs from prefix"
            )
    for flag, epoch_field in (
        ("stateHashMismatchBeforeDivergence", "firstStateHashMismatchEpoch"),
        ("wireOnlyDifferenceBeforeDivergence", "firstWireOnlyDifferenceEpoch"),
    ):
        if not isinstance(pair[flag], bool):
            raise RuntimeError(f"panel {panel_id}/{unit}: {flag} is invalid")
        epoch = pair[epoch_field]
        if pair[flag]:
            result[epoch_field] = _require_nonnegative(
                epoch,
                epoch_field,
                positive=True,
            )
            if result[epoch_field] > prefix_count:
                raise RuntimeError(
                    f"panel {panel_id}/{unit}: {epoch_field} is not before divergence"
                )
        elif epoch is not None:
            raise RuntimeError(f"panel {panel_id}/{unit}: {epoch_field} is unexpected")
    return result


def build_record_set(report, schema_sha256, generator_sha256):
    if set(report) != {
        "schemaVersion",
        "reportType",
        "toolIdentity",
        "claimBoundary",
        "panels",
    }:
        raise RuntimeError("source inventory report fields differ")
    if report["schemaVersion"] != "1.0.0" or report["reportType"] != (
        "ridebound-wp13-h6-evidence-inventory-v1"
    ):
        raise RuntimeError("source inventory report version differs")
    if report["toolIdentity"] != {
        "analyzerSourceSha256": _SOURCE_ANALYZER_SHA256,
        "solverEvidenceVerifierSourceSha256": _SOLVER_VERIFIER_SHA256,
    }:
        raise RuntimeError("source inventory tool identity differs")
    if report["claimBoundary"] != {
        "analysisClass": "postOutcomeExploratory",
        "alignment": "equalObservedInputNotFullInternalState",
        "downstreamInterpretation": "trajectoryAssociatedNotCausal",
        "h6Artifacts": "readOnlyImmutableInputs",
        "confirmatoryGate": None,
    }:
        raise RuntimeError("source inventory claim boundary differs")
    _require_hash(schema_sha256, "record-set schema hash")
    _require_hash(generator_sha256, "record generator hash")
    contract_identity = {
        "recordSetSchemaSha256": schema_sha256,
        "generatorSourceSha256": generator_sha256,
    }
    panels = report["panels"]
    if not isinstance(panels, list) or [panel.get("panelId") for panel in panels] != [
        "A",
        "B",
    ]:
        raise RuntimeError("source inventory panel order differs")
    records = []
    units = set()
    source_counts = Counter()
    panel_counts = {}
    for panel in panels:
        panel_id = panel["panelId"]
        if set(panel) != {
            "panelId",
            "bundleCount",
            "primaryPairCount",
            "nonPrimaryBundleCount",
            "declaredBundleInventorySha256",
            "evidenceCoverage",
            "primaryAlignment",
        }:
            raise RuntimeError(f"panel {panel_id}: report fields differ")
        expected_shape = (60, 20, 20) if panel_id == "A" else (40, 20, 0)
        if (
            panel["bundleCount"],
            panel["primaryPairCount"],
            panel["nonPrimaryBundleCount"],
        ) != expected_shape or panel["declaredBundleInventorySha256"] != (
            _PANEL_INVENTORIES[panel_id]
        ):
            raise RuntimeError(f"panel {panel_id}: frozen inventory differs")
        alignment = panel["primaryAlignment"]
        if set(alignment) != {
            "classificationCounts",
            "stateHashMismatchBeforeDivergencePairCount",
            "pairs",
        } or not isinstance(alignment["pairs"], list):
            raise RuntimeError(f"panel {panel_id}: alignment fields differ")
        projected = []
        for pair in alignment["pairs"]:
            record = _project_record(
                panel_id,
                _PANEL_INVENTORIES[panel_id],
                pair,
                contract_identity,
            )
            key = (panel_id, record["unitId"])
            if key in units:
                raise RuntimeError(f"duplicate paired unit {panel_id}/{record['unitId']}")
            units.add(key)
            projected.append(record)
            source_counts[record["firstDivergence"]["classification"]] += 1
        projected.sort(key=lambda value: value["unitId"])
        if len(projected) != 20:
            raise RuntimeError(f"panel {panel_id}: paired record count differs")
        actual_panel_counts = Counter(
            record["firstDivergence"]["classification"] for record in projected
        )
        if dict(actual_panel_counts) != alignment["classificationCounts"]:
            raise RuntimeError(f"panel {panel_id}: classification counts differ")
        state_mismatch_count = sum(
            record["stateHashMismatchBeforeDivergence"] for record in projected
        )
        if state_mismatch_count != alignment[
            "stateHashMismatchBeforeDivergencePairCount"
        ]:
            raise RuntimeError(f"panel {panel_id}: state mismatch count differs")
        records.extend(projected)
        panel_counts[panel_id] = len(projected)
    classification_counts = {
        classification: source_counts[classification]
        for classification in _CLASSIFICATIONS
    }
    return {
        "schemaVersion": "1.0.0",
        "reportType": "ridebound-wp13-first-divergence-record-set-v1",
        "contractIdentity": contract_identity,
        "sourceInventoryReport": _source_identity(),
        "claimBoundary": _claim_boundary(),
        "recordCount": len(records),
        "panelRecordCounts": panel_counts,
        "classificationCounts": classification_counts,
        "records": records,
    }


def _require_output_outside_inputs(output, source, immutable_roots):
    if output.resolve() == source.resolve():
        raise RuntimeError("output must not overwrite the source inventory report")
    for root in immutable_roots:
        resolved_root = root.resolve()
        if not resolved_root.is_dir():
            raise RuntimeError(f"immutable input root does not exist: {resolved_root}")
        try:
            output.resolve().relative_to(resolved_root)
        except ValueError:
            continue
        raise RuntimeError("output must be outside every immutable H6 input root")


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--inventory-report", required=True, type=pathlib.Path)
    parser.add_argument(
        "--immutable-root",
        action="append",
        required=True,
        type=pathlib.Path,
    )
    parser.add_argument("--output", type=pathlib.Path)
    arguments = parser.parse_args(argv)
    try:
        if arguments.output is not None:
            _require_output_outside_inputs(
                arguments.output,
                arguments.inventory_report,
                arguments.immutable_root,
            )
        report = _read_source_report(arguments.inventory_report)
        schema, schema_hash = _load_schema()
        generator_hash = _sha256(pathlib.Path(__file__).resolve())
        result = build_record_set(report, schema_hash, generator_hash)
        jsonschema.Draft202012Validator(schema).validate(result)
        encoded = _canonical(result) + b"\n"
        if arguments.output is None:
            sys.stdout.buffer.write(encoded)
        else:
            arguments.output.parent.mkdir(parents=True, exist_ok=True)
            arguments.output.write_bytes(encoded)
    except (
        OSError,
        RuntimeError,
        ValueError,
        TypeError,
        jsonschema.ValidationError,
    ) as error:
        print(f"wp13_first_divergence_records: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
