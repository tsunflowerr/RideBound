#!/usr/bin/env python3
"""Compare action-level behavior at the frozen WP13 first-divergence epochs."""

from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import pathlib
import sys
from collections import Counter

import jsonschema


_ROOT = pathlib.Path(__file__).parent.resolve()
_REPOSITORY = _ROOT.parent.parent
_INVENTORY_ANALYZER = (_ROOT / "wp13_h6_evidence_inventory.py").resolve()
_RECORD_SCHEMA = (
    _REPOSITORY
    / "benchmarks"
    / "schemas"
    / "wp13"
    / "v1"
    / "first-divergence-record-set.schema.json"
).resolve()
_INVENTORY_ANALYZER_SHA256 = (
    "0563ef2495550345c587ddbe07cf47a0ff88c38bf2b618dae6723c881ab1ab3b"
)
_RECORD_SET_LENGTH = 102_746
_RECORD_SET_SHA256 = (
    "bef27519b5dae4482029be83cd8d1c2b1e0ef2afa72ea63e6645f4991e425618"
)
_RECORD_SCHEMA_SHA256 = (
    "47e24bb394a3949b54ffbfe697e24dfee01eed679b98b0954375f3138fa3d8b8"
)
_RECORD_GENERATOR_SHA256 = (
    "4f52b76baa3f34b16975a57abb1acb44901e2adcd7afc507f95b5ae4987f74f8"
)
_SOURCE_INVENTORY_REPORT = {
    "analyzerSourceSha256": _INVENTORY_ANALYZER_SHA256,
    "lengthBytes": 73_102,
    "reportType": "ridebound-wp13-h6-evidence-inventory-v1",
    "schemaVersion": "1.0.0",
    "sha256": "6d36bc6e781f9fa5c32a024c3f5350271b806f43a7418f148ef5138fa1fff63e",
    "solverEvidenceVerifierSourceSha256": (
        "3eebec96b8370db2c4879adeaede3e67b7344571299a496953afcbc599dd93e5"
    ),
}
_OUTCOME_TYPES = {
    "requestAccepted": "accepted",
    "requestRejected": "rejected",
    "requestDeferred": "deferred",
}
_DIFFERENCE_CLASS_ORDER = (
    "requestDispositionDifference",
    "acceptedVehicleAssignmentDifference",
    "requestActionPayloadDifference",
    "vehiclePlanDifference",
    "promiseProjectionDifference",
    "solverStatusDifference",
    "otherActionDifference",
)

sys.dont_write_bytecode = True


def _sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _load_inventory_analyzer():
    if _sha256(_INVENTORY_ANALYZER) != _INVENTORY_ANALYZER_SHA256:
        raise RuntimeError("WP13 inventory analyzer source identity differs")
    spec = importlib.util.spec_from_file_location(
        "wp13_h6_inventory_for_behavioral_comparison",
        _INVENTORY_ANALYZER,
    )
    if spec is None or spec.loader is None:
        raise RuntimeError("cannot load the exact WP13 inventory analyzer")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    if pathlib.Path(module.__file__).resolve() != _INVENTORY_ANALYZER:
        raise RuntimeError("WP13 inventory analyzer resolved outside its exact path")
    return module


inventory = _load_inventory_analyzer()


def _value_hash(domain, value):
    if not isinstance(domain, str) or not domain:
        raise RuntimeError("hash domain is invalid")
    return hashlib.sha256(
        b"RideBound.Wp13."
        + domain.encode("ascii")
        + b".v1\0"
        + inventory._canonical(value)
    ).hexdigest()


def _validate_record_set_identity(value):
    schema_bytes = _RECORD_SCHEMA.read_bytes()
    if hashlib.sha256(schema_bytes).hexdigest() != _RECORD_SCHEMA_SHA256:
        raise RuntimeError("first-divergence schema identity differs")
    try:
        schema = json.loads(
            schema_bytes,
            object_pairs_hook=inventory._strict_object,
        )
        jsonschema.Draft202012Validator.check_schema(schema)
        jsonschema.Draft202012Validator(schema).validate(value)
    except (
        ValueError,
        TypeError,
        json.JSONDecodeError,
        jsonschema.SchemaError,
        jsonschema.ValidationError,
    ) as error:
        raise RuntimeError("first-divergence record-set schema validation failed") from error
    if value["contractIdentity"] != {
        "generatorSourceSha256": _RECORD_GENERATOR_SHA256,
        "recordSetSchemaSha256": _RECORD_SCHEMA_SHA256,
    }:
        raise RuntimeError("first-divergence contract identity differs")
    if value["sourceInventoryReport"] != _SOURCE_INVENTORY_REPORT:
        raise RuntimeError("source inventory report identity differs")


def _read_record_set(path):
    data = path.read_bytes()
    if len(data) != _RECORD_SET_LENGTH or hashlib.sha256(data).hexdigest() != (
        _RECORD_SET_SHA256
    ):
        raise RuntimeError("first-divergence record-set identity differs")
    if not data.endswith(b"\n") or data.endswith(b"\r\n"):
        raise RuntimeError("first-divergence record-set framing differs")
    try:
        value = inventory._loads(data[:-1])
    except (UnicodeDecodeError, ValueError, TypeError, json.JSONDecodeError) as error:
        raise RuntimeError("first-divergence record-set JSON is invalid") from error
    if inventory._canonical(value) != data[:-1]:
        raise RuntimeError("first-divergence record set is not canonical JSON")
    _validate_record_set_identity(value)
    return value


def _prepare_panel(panel_id, root):
    normalized = panel_id.lower()
    root = root.resolve()
    if not root.is_dir():
        raise RuntimeError(f"panel {panel_id}: immutable root does not exist")
    directories = sorted(
        {
            path.parent
            for path in root.rglob("bundle-manifest.json")
            if path.parent.parent == root
        },
        key=lambda path: path.name,
    )
    bundles = [inventory._prepare_bundle(directory) for directory in directories]
    if not bundles:
        raise RuntimeError(f"panel {panel_id}: no evidence bundles found")
    by_label = {bundle["label"]: bundle for bundle in bundles}
    if len(by_label) != len(bundles):
        raise RuntimeError(f"panel {panel_id}: duplicate bundle label")
    primary_kind = "p" if panel_id == "A" else "pb"
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
        arms = grouped.setdefault(bundle["identity"]["unit"], {})
        arm = bundle["identity"]["arm"]
        if arm in arms:
            raise RuntimeError(f"panel {panel_id}: duplicate primary arm")
        arms[arm] = bundle
    if len(grouped) != 20 or any(set(arms) != {"b1", "c1"} for arms in grouped.values()):
        raise RuntimeError(f"panel {panel_id}: primary pairing differs")
    declared_inventory = [
        {
            "label": bundle["label"],
            "bundleManifestSha256": bundle["bundleManifestSha256"],
            "transcriptSha256": bundle["transcriptSha256"],
            "sourceScenarioContentSha256": bundle[
                "sourceScenarioContentSha256"
            ],
        }
        for bundle in bundles
    ]
    inventory_hash = hashlib.sha256(
        b"RideBound.Wp13.H6BundleInventory.v1\0"
        + inventory._canonical(declared_inventory)
    ).hexdigest()
    inventory._validate_frozen_panel_contract(
        normalized,
        bundles,
        len(grouped),
        len(bundles) - len(primary),
        inventory_hash,
    )
    return by_label


def _target_decision(bundle, target_epoch, coverage):
    target = None
    for decision in inventory._decision_records(bundle, coverage):
        if decision["epochId"] == target_epoch:
            if target is not None:
                raise RuntimeError(f"{bundle['label']}: duplicate target epoch")
            target = decision
    if target is None:
        raise RuntimeError(f"{bundle['label']}: target epoch is absent")
    return target


def _bind_target(record, arm, decision):
    expected = record["firstDivergence"][f"{arm}Evidence"]
    actual = {
        "epochId": decision["epochId"],
        "simTimeMs": decision["simTimeMs"],
        "observedInputProjectionSha256": inventory._projection_hash(
            decision["observedInputProjection"]
        ),
        "operationalDecisionProjectionSha256": inventory._projection_hash(
            decision["operationalDecisionProjection"]
        ),
        "wireDecisionProjectionSha256": inventory._projection_hash(
            decision["wireDecisionProjection"]
        ),
        "eventTypes": decision["eventTypes"],
        "actionTypes": decision["actionTypes"],
    }
    if actual != expected:
        raise RuntimeError(
            f"{record['panelId']}/{record['unitId']}/{arm}: target binding differs"
        )


def _arrived_request_ids(decision):
    result = []
    for event in decision["observedInputProjection"]["events"]:
        if event["eventType"] != "requestArrived":
            continue
        request = event["payload"].get("request")
        request_id = None if not isinstance(request, dict) else request.get("requestId")
        if not isinstance(request_id, str) or not request_id or request_id in result:
            raise RuntimeError("arrived request identity is invalid or duplicated")
        result.append(request_id)
    if not result:
        raise RuntimeError("first operational divergence has no arrived request")
    return result


def _request_actions(projection):
    result = {}
    for action in projection["actions"]:
        decision_type = action["decisionType"]
        if decision_type not in _OUTCOME_TYPES:
            continue
        payload = action["payload"]
        request_id = payload.get("requestId")
        if not isinstance(request_id, str) or not request_id or request_id in result:
            raise RuntimeError("request outcome is missing or duplicated")
        disposition = _OUTCOME_TYPES[decision_type]
        projected = {
            "requestId": request_id,
            "disposition": disposition,
            "actionProjectionSha256": _value_hash("RequestAction", action),
        }
        if disposition == "accepted":
            vehicle_id = payload.get("vehicleId")
            if not isinstance(vehicle_id, str) or not vehicle_id:
                raise RuntimeError("accepted request has no exact vehicle identity")
            projected["vehicleId"] = vehicle_id
        result[request_id] = projected
    return result


def _action_subset(projection, included=None, excluded=None):
    actions = projection["actions"]
    return [
        action
        for action in actions
        if (included is None or action["decisionType"] in included)
        and (excluded is None or action["decisionType"] not in excluded)
    ]


def _classify_actions(b1, c1, arrived_request_ids):
    b1_projection = b1["operationalDecisionProjection"]
    c1_projection = c1["operationalDecisionProjection"]
    b1_requests = _request_actions(b1_projection)
    c1_requests = _request_actions(c1_projection)
    expected_requests = set(arrived_request_ids)
    if set(b1_requests) != expected_requests or set(c1_requests) != expected_requests:
        raise RuntimeError("request outcomes do not bijectively cover arrivals")

    comparisons = []
    disposition_difference = False
    assignment_difference = False
    request_payload_difference = False
    for request_id in arrived_request_ids:
        b1_request = b1_requests[request_id]
        c1_request = c1_requests[request_id]
        comparison = {
            "requestId": request_id,
            "b1Disposition": b1_request["disposition"],
            "c1Disposition": c1_request["disposition"],
        }
        disposition_difference |= (
            b1_request["disposition"] != c1_request["disposition"]
        )
        if b1_request["disposition"] == "accepted":
            comparison["b1VehicleId"] = b1_request["vehicleId"]
        if c1_request["disposition"] == "accepted":
            comparison["c1VehicleId"] = c1_request["vehicleId"]
        if (
            b1_request["disposition"] == "accepted"
            and c1_request["disposition"] == "accepted"
            and b1_request["vehicleId"] != c1_request["vehicleId"]
        ):
            assignment_difference = True
        if (
            b1_request["disposition"] == c1_request["disposition"]
            and b1_request.get("vehicleId") == c1_request.get("vehicleId")
            and b1_request["actionProjectionSha256"]
            != c1_request["actionProjectionSha256"]
        ):
            request_payload_difference = True
        comparisons.append(comparison)

    typed_subsets = {
        "vehiclePlan": {"vehiclePlanUpdated"},
        "promise": {"promisePublished"},
    }
    subset_hashes = {}
    subset_differences = {}
    for name, decision_types in typed_subsets.items():
        b1_subset = _action_subset(b1_projection, included=decision_types)
        c1_subset = _action_subset(c1_projection, included=decision_types)
        subset_hashes[name] = {
            "b1Sha256": _value_hash(f"{name}Actions", b1_subset),
            "c1Sha256": _value_hash(f"{name}Actions", c1_subset),
        }
        subset_differences[name] = b1_subset != c1_subset
    typed = set(_OUTCOME_TYPES) | set().union(*typed_subsets.values())
    b1_other = _action_subset(b1_projection, excluded=typed)
    c1_other = _action_subset(c1_projection, excluded=typed)
    subset_hashes["other"] = {
        "b1Sha256": _value_hash("otherActions", b1_other),
        "c1Sha256": _value_hash("otherActions", c1_other),
    }
    subset_differences["other"] = b1_other != c1_other

    flags = {
        "requestDispositionDifference": disposition_difference,
        "acceptedVehicleAssignmentDifference": assignment_difference,
        "requestActionPayloadDifference": request_payload_difference,
        "vehiclePlanDifference": subset_differences["vehiclePlan"],
        "promiseProjectionDifference": subset_differences["promise"],
        "solverStatusDifference": (
            b1_projection["solverStatus"] != c1_projection["solverStatus"]
        ),
        "otherActionDifference": subset_differences["other"],
    }
    classes = [name for name in _DIFFERENCE_CLASS_ORDER if flags[name]]
    if not classes or b1_projection == c1_projection:
        raise RuntimeError("operational divergence has no classifiable difference")
    b1_accepted = sum(
        request["disposition"] == "accepted" for request in b1_requests.values()
    )
    c1_accepted = sum(
        request["disposition"] == "accepted" for request in c1_requests.values()
    )
    accepted_delta = c1_accepted - b1_accepted
    relation = (
        "c1LowerImmediateAcceptance"
        if accepted_delta < 0
        else "c1HigherImmediateAcceptance"
        if accepted_delta > 0
        else "equalImmediateAcceptance"
    )
    return {
        "primaryDifferenceClass": classes[0],
        "differenceClasses": classes,
        "immediateRequestComparison": {
            "arrivedRequestCount": len(arrived_request_ids),
            "b1AcceptedCount": b1_accepted,
            "c1AcceptedCount": c1_accepted,
            "acceptedCountDeltaC1MinusB1": accepted_delta,
            "acceptanceRelation": relation,
            "requests": comparisons,
        },
        "actionSubsetHashes": subset_hashes,
    }


def _compare_record(record, bundles, coverage):
    divergence = record["firstDivergence"]
    if divergence["classification"] != (
        "operationalDecisionDivergenceOnEqualObservedInput"
    ) or divergence["observedInputRelation"] != "equal":
        raise RuntimeError("behavioral comparator requires equal-input divergence")
    epoch = divergence["b1Evidence"]["epochId"]
    decisions = {}
    raw_evidence = {}
    for arm in ("b1", "c1"):
        label = record[f"{arm}Label"]
        if label not in bundles:
            raise RuntimeError(f"{record['panelId']}/{record['unitId']}: bundle absent")
        bundle = bundles[label]
        if (
            bundle["identity"]["arm"] != arm
            or bundle["identity"]["unit"] != record["unitId"]
            or bundle["sourceScenarioContentSha256"]
            != record["sourceScenarioContentSha256"]
        ):
            raise RuntimeError(
                f"{record['panelId']}/{record['unitId']}/{arm}: "
                "bundle identity differs"
            )
        decision = _target_decision(bundle, epoch, coverage)
        _bind_target(record, arm, decision)
        decisions[arm] = decision
        raw_evidence[f"{arm}BundleManifestSha256"] = bundle[
            "bundleManifestSha256"
        ]
        raw_evidence[f"{arm}TranscriptSha256"] = bundle["transcriptSha256"]
    if decisions["b1"]["observedInputProjection"] != (
        decisions["c1"]["observedInputProjection"]
    ):
        raise RuntimeError("recorded equal observed input differs in raw evidence")
    arrived_request_ids = _arrived_request_ids(decisions["b1"])
    comparison = _classify_actions(
        decisions["b1"],
        decisions["c1"],
        arrived_request_ids,
    )
    return {
        "schemaVersion": "1.0.0",
        "recordType": "ridebound-wp13-paired-behavioral-comparison-v1",
        "panelId": record["panelId"],
        "unitId": record["unitId"],
        "epochId": epoch,
        "simTimeMs": divergence["b1Evidence"]["simTimeMs"],
        "observedInputProjectionSha256": divergence["b1Evidence"][
            "observedInputProjectionSha256"
        ],
        "sourceFirstDivergenceRecordSha256": _value_hash(
            "FirstDivergenceRecord",
            record,
        ),
        "rawEvidence": raw_evidence,
        **comparison,
    }


def analyze(record_set, panel_roots):
    if set(panel_roots) != {"A", "B"}:
        raise RuntimeError("exact Panel A/B roots are required")
    bundles = {
        panel_id: _prepare_panel(panel_id, panel_roots[panel_id])
        for panel_id in ("A", "B")
    }
    coverage = inventory._new_coverage()
    records = [
        _compare_record(record, bundles[record["panelId"]], coverage)
        for record in record_set["records"]
    ]
    records.sort(key=lambda value: (value["panelId"], value["unitId"]))
    if len(records) != 40 or len(
        {(record["panelId"], record["unitId"]) for record in records}
    ) != 40:
        raise RuntimeError("behavioral record inventory differs")
    panel_summaries = []
    for panel_id in ("A", "B"):
        selected = [record for record in records if record["panelId"] == panel_id]
        primary_counts = Counter(
            record["primaryDifferenceClass"] for record in selected
        )
        relation_counts = Counter(
            record["immediateRequestComparison"]["acceptanceRelation"]
            for record in selected
        )
        difference_counts = Counter(
            difference_class
            for record in selected
            for difference_class in record["differenceClasses"]
        )
        panel_summaries.append(
            {
                "panelId": panel_id,
                "recordCount": len(selected),
                "primaryDifferenceClassCounts": dict(sorted(primary_counts.items())),
                "differenceClassCounts": dict(sorted(difference_counts.items())),
                "immediateAcceptanceRelationCounts": dict(
                    sorted(relation_counts.items())
                ),
                "acceptedCountDeltaC1MinusB1Sum": sum(
                    record["immediateRequestComparison"][
                        "acceptedCountDeltaC1MinusB1"
                    ]
                    for record in selected
                ),
            }
        )
    return {
        "schemaVersion": "1.0.0",
        "reportType": "ridebound-wp13-paired-behavioral-comparator-v1",
        "toolIdentity": {
            "comparatorSourceSha256": _sha256(pathlib.Path(__file__).resolve()),
            "inventoryAnalyzerSourceSha256": _INVENTORY_ANALYZER_SHA256,
        },
        "inputIdentity": {
            "firstDivergenceRecordSetLengthBytes": _RECORD_SET_LENGTH,
            "firstDivergenceRecordSetSha256": _RECORD_SET_SHA256,
            "firstDivergenceSchemaSha256": _RECORD_SCHEMA_SHA256,
            "firstDivergenceGeneratorSourceSha256": _RECORD_GENERATOR_SHA256,
            "sourceInventoryReport": _SOURCE_INVENTORY_REPORT,
        },
        "claimBoundary": {
            "analysisClass": "postOutcomeExploratory",
            "comparison": "immediateObservedActionsAtFirstDivergence",
            "downstreamService": "notEvaluatedByThisArtifact",
            "interpretation": "descriptiveNotCausal",
            "h6Artifacts": "readOnlyImmutableInputs",
            "confirmatoryGate": None,
        },
        "evidenceCoverage": {
            "primaryTranscriptCount": len(records) * 2,
            "transcriptTraversal": "completeToTerminalShutdownAndEof",
            "solverEvidenceVerification": "everyDecision",
            **inventory._finalize_coverage(coverage),
        },
        "panelSummaries": panel_summaries,
        "records": records,
    }


def _panel_argument(value):
    panel_id, separator, raw_path = value.partition("=")
    if not separator or panel_id.upper() not in {"A", "B"} or not raw_path:
        raise argparse.ArgumentTypeError("panel must be A=PATH or B=PATH")
    return panel_id.upper(), pathlib.Path(raw_path)


def _require_output_outside_inputs(output, record_set, panel_roots):
    resolved_output = output.resolve()
    if resolved_output == record_set.resolve():
        raise RuntimeError("output must not overwrite the first-divergence record set")
    for root in panel_roots.values():
        resolved_root = root.resolve()
        try:
            resolved_output.relative_to(resolved_root)
        except ValueError:
            continue
        raise RuntimeError("output must be outside immutable H6 roots")


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--record-set", required=True, type=pathlib.Path)
    parser.add_argument(
        "--panel",
        action="append",
        required=True,
        type=_panel_argument,
    )
    parser.add_argument("--output", type=pathlib.Path)
    arguments = parser.parse_args(argv)
    try:
        panel_roots = {}
        for panel_id, root in arguments.panel:
            if panel_id in panel_roots:
                raise RuntimeError(f"duplicate panel identity {panel_id}")
            panel_roots[panel_id] = root
        if arguments.output is not None:
            _require_output_outside_inputs(
                arguments.output,
                arguments.record_set,
                panel_roots,
            )
        record_set = _read_record_set(arguments.record_set)
        result = analyze(record_set, panel_roots)
        encoded = inventory._canonical(result) + b"\n"
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
        print(f"wp13_behavioral_comparator: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
