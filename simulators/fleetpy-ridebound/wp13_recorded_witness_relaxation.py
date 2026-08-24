#!/usr/bin/env python3
"""Calculate exact clearance of recorded C1 witnesses for B1-selected candidates."""

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
_COMPARATOR_SOURCE = (_ROOT / "wp13_behavioral_comparator.py").resolve()
_SCHEMA = (
    _REPOSITORY
    / "benchmarks"
    / "schemas"
    / "wp13"
    / "v1"
    / "recorded-witness-relaxation-set.schema.json"
).resolve()
_COMPARATOR_SOURCE_SHA256 = (
    "f2c55e1f7fbe9cb341cb6c75764a192254aa2e375de0547780c94c83b01dd0ee"
)
_COMPARATOR_REPORT_LENGTH = 79_864
_COMPARATOR_REPORT_SHA256 = (
    "3717f093c62c37a339da0b826323fb1604a684bd9990630d9d9dc5563fd4f7e3"
)
_SCHEMA_SHA256 = (
    "7834f04e8868bf8ea673e28f5d5f4a590ab269f641a912820967bcdb8b18fb1c"
)
_MAX_INTEGER = 9_007_199_254_740_991
_STATUSES = (
    "absentRetainedOrOmittedNotRecorded",
    "prunedWithCommitmentWitness",
    "prunedWithoutCommitmentWitness",
    "selectedByC1",
)
_CLEARANCE_KINDS = (
    "categoricalLockDisablement",
    "numericBudgetLimitIncrease",
)

sys.dont_write_bytecode = True


def _sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _load_comparator():
    if _sha256(_COMPARATOR_SOURCE) != _COMPARATOR_SOURCE_SHA256:
        raise RuntimeError("behavioral comparator source identity differs")
    spec = importlib.util.spec_from_file_location(
        "wp13_behavioral_comparator_for_relaxation",
        _COMPARATOR_SOURCE,
    )
    if spec is None or spec.loader is None:
        raise RuntimeError("cannot load exact behavioral comparator")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    if pathlib.Path(module.__file__).resolve() != _COMPARATOR_SOURCE:
        raise RuntimeError("behavioral comparator resolved outside exact path")
    return module


comparator = _load_comparator()
inventory = comparator.inventory


def _read_behavioral_report(path):
    data = path.read_bytes()
    if len(data) != _COMPARATOR_REPORT_LENGTH or hashlib.sha256(data).hexdigest() != (
        _COMPARATOR_REPORT_SHA256
    ):
        raise RuntimeError("behavioral comparator report identity differs")
    if not data.endswith(b"\n") or data.endswith(b"\r\n"):
        raise RuntimeError("behavioral comparator report framing differs")
    try:
        value = inventory._loads(data[:-1])
    except (UnicodeDecodeError, ValueError, TypeError, json.JSONDecodeError) as error:
        raise RuntimeError("behavioral comparator report JSON is invalid") from error
    if inventory._canonical(value) != data[:-1]:
        raise RuntimeError("behavioral comparator report is not canonical JSON")
    if (
        value.get("reportType")
        != "ridebound-wp13-paired-behavioral-comparator-v1"
        or value.get("toolIdentity", {}).get("comparatorSourceSha256")
        != _COMPARATOR_SOURCE_SHA256
        or value.get("claimBoundary", {}).get("interpretation")
        != "descriptiveNotCausal"
    ):
        raise RuntimeError("behavioral comparator report contract differs")
    return value


def _load_schema():
    data = _SCHEMA.read_bytes()
    if hashlib.sha256(data).hexdigest() != _SCHEMA_SHA256:
        raise RuntimeError("recorded-witness relaxation schema identity differs")
    try:
        schema = json.loads(data, object_pairs_hook=inventory._strict_object)
        jsonschema.Draft202012Validator.check_schema(schema)
    except (ValueError, TypeError, json.JSONDecodeError, jsonschema.SchemaError) as error:
        raise RuntimeError("recorded-witness relaxation schema is invalid") from error
    return schema


def _raw_target_decision(bundle, target_epoch):
    target = None
    for direction, envelope in inventory._decoded_frames(bundle):
        if (
            direction == "runnerToAdapter"
            and envelope.get("messageType") == "decision"
            and envelope.get("epochId") == target_epoch
        ):
            if target is not None:
                raise RuntimeError(f"{bundle['label']}: duplicate raw target decision")
            target = envelope
    if target is None:
        raise RuntimeError(f"{bundle['label']}: raw target decision is absent")
    return target


def _bind_raw_decision(record, arm, envelope):
    expected = record["firstDivergence"][f"{arm}Evidence"]
    payload = envelope.get("payload")
    if not isinstance(payload, dict):
        raise RuntimeError(f"{arm}: raw target payload is invalid")
    actions = payload.get("actions")
    solver = payload.get("solver")
    if not isinstance(actions, list) or not isinstance(solver, dict):
        raise RuntimeError(f"{arm}: raw target actions/solver are invalid")
    operational = {
        "solverStatus": solver.get("status"),
        "actions": inventory._operational_action_projection(actions),
    }
    wire = {
        "solverStatus": solver.get("status"),
        "actions": inventory._strip_generated_action_fields(actions),
    }
    actual = {
        "epochId": envelope.get("epochId"),
        "simTimeMs": envelope.get("simTimeMs"),
        "operationalDecisionProjectionSha256": inventory._projection_hash(operational),
        "wireDecisionProjectionSha256": inventory._projection_hash(wire),
        "actionTypes": [action.get("decisionType") for action in actions],
    }
    for name, value in actual.items():
        if expected[name] != value:
            raise RuntimeError(f"{arm}: raw target {name} binding differs")
    return actions, solver["executionEvidence"]


def _text(value, field):
    if not isinstance(value, str) or not value:
        raise RuntimeError(f"{field} must be non-empty text")
    return value


def _safe_integer(value, field):
    if isinstance(value, bool) or not isinstance(value, int) or not 0 <= value <= _MAX_INTEGER:
        raise RuntimeError(f"{field} must be a nonnegative safe integer")
    return value


def _selected_plans(actions, arm):
    plans = {}
    accepted = {}
    accepted_request_ids = set()
    for index, action in enumerate(actions):
        if not isinstance(action, dict) or set(action) != {"decisionType", "payload"}:
            raise RuntimeError(f"{arm} action {index}: shape differs")
        payload = action["payload"]
        if not isinstance(payload, dict):
            raise RuntimeError(f"{arm} action {index}: payload differs")
        if action["decisionType"] == "vehiclePlanUpdated":
            candidate_id = _text(payload.get("candidateId"), "plan candidateId")
            vehicle_id = _text(payload.get("vehicleId"), "plan vehicleId")
            if candidate_id in plans:
                raise RuntimeError(f"{arm}: duplicate selected candidate ID")
            plans[candidate_id] = {"vehicleId": vehicle_id, "action": action}
        elif action["decisionType"] == "requestAccepted":
            candidate_id = _text(payload.get("candidateId"), "accept candidateId")
            request_id = _text(payload.get("requestId"), "accept requestId")
            vehicle_id = _text(payload.get("vehicleId"), "accept vehicleId")
            if request_id in accepted_request_ids:
                raise RuntimeError(f"{arm}: duplicate accepted request ID")
            accepted_request_ids.add(request_id)
            requests = accepted.setdefault(candidate_id, {})
            requests[request_id] = vehicle_id
    for candidate_id, requests in accepted.items():
        if candidate_id not in plans or any(
            vehicle_id != plans[candidate_id]["vehicleId"]
            for vehicle_id in requests.values()
        ):
            raise RuntimeError(f"{arm}: accepted candidate/plan binding differs")
    for candidate_id, plan in plans.items():
        plan["acceptedRequestIds"] = sorted(accepted.get(candidate_id, {}))
    return plans


def _witness_clearance(witness, candidate_vehicle):
    stage = witness.get("stage")
    if stage == "budget":
        expected = {
            "stage", "code", "vehicleId", "requestId", "dimension",
            "limit", "before", "delta", "after",
        }
        if set(witness) != expected or witness["code"] != "COMMITMENT_BUDGET_EXCEEDED":
            raise RuntimeError("budget witness shape/code differs")
        limit = _safe_integer(witness["limit"], "budget limit")
        before = _safe_integer(witness["before"], "budget before")
        delta = _safe_integer(witness["delta"], "budget delta")
        after = _safe_integer(witness["after"], "budget after")
        if before + delta != after or after <= limit:
            raise RuntimeError("budget witness arithmetic does not prove an overrun")
        result = dict(witness)
        result.update(
            {
                "clearanceKind": "numericBudgetLimitIncrease",
                "witnessSha256": comparator._value_hash(
                    "RecordedCommitmentWitness", witness
                ),
                "requiredLimit": after,
                "additiveLimitIncrease": after - limit,
            }
        )
    elif stage == "lock":
        expected = {"stage", "code", "vehicleId", "requestId", "dimension", "rule"}
        if set(witness) != expected or witness["code"] != "COMMITMENT_PHASE_LOCK":
            raise RuntimeError("lock witness shape/code differs")
        result = dict(witness)
        result.update(
            {
                "clearanceKind": "categoricalLockDisablement",
                "witnessSha256": comparator._value_hash(
                    "RecordedCommitmentWitness", witness
                ),
                "requiredChange": "disableRecordedRuleForDimension",
            }
        )
    else:
        raise RuntimeError(f"unsupported commitment witness stage {stage!r}")
    if _text(witness.get("vehicleId"), "witness vehicleId") != candidate_vehicle:
        raise RuntimeError("commitment witness vehicle differs from candidate")
    _text(witness.get("requestId"), "witness requestId")
    _text(witness.get("dimension"), "witness dimension")
    return result


def _candidate_links(b1_actions, c1_actions, c1_evidence):
    b1_plans = _selected_plans(b1_actions, "b1")
    c1_plans = _selected_plans(c1_actions, "c1")
    if not b1_plans:
        raise RuntimeError("B1 has no actionful selected candidate at divergence")
    pruned_values = c1_evidence["prunedCandidates"]
    pruned = {}
    for witness in pruned_values:
        candidate_id = _text(witness.get("candidateId"), "prune candidateId")
        if candidate_id in pruned:
            raise RuntimeError("duplicate C1 pruned candidate ID")
        pruned[candidate_id] = witness
    if set(pruned) & set(c1_plans):
        raise RuntimeError("C1 candidate cannot be both selected and pruned")
    links = []
    for candidate_id, plan in sorted(b1_plans.items()):
        base = {
            "candidateId": candidate_id,
            "vehicleId": plan["vehicleId"],
            "acceptedRequestIds": plan["acceptedRequestIds"],
            "sourceB1PlanActionSha256": comparator._value_hash(
                "SelectedPlanAction", plan["action"]
            ),
            "retainedCandidatePortfolio": "notRecorded",
            "candidateFeasibilityAfterClearance": "notEvaluated",
        }
        if candidate_id in pruned:
            witness = pruned[candidate_id]
            if witness["vehicleId"] != plan["vehicleId"] or sorted(
                witness["newRequestIds"]
            ) != plan["acceptedRequestIds"]:
                raise RuntimeError("pruned candidate identity differs from B1 action")
            base.update(
                {
                    "sourceC1PruneWitnessSha256": comparator._value_hash(
                        "CandidatePruneWitness", witness
                    ),
                    "pruneCode": _text(witness.get("code"), "prune code"),
                }
            )
            commitments = witness["commitmentWitnesses"]
            if commitments:
                if "physicalWitness" in witness:
                    raise RuntimeError(
                        "pruned candidate mixes physical and commitment witnesses"
                    )
                witness_hashes = {
                    comparator._value_hash("RecordedCommitmentWitness", value)
                    for value in commitments
                }
                witness_codes = {value.get("code") for value in commitments}
                if (
                    len(witness_hashes) != len(commitments)
                    or witness_codes != {base["pruneCode"]}
                ):
                    raise RuntimeError(
                        "commitment witnesses are duplicate or disagree with prune code"
                    )
                base["status"] = "prunedWithCommitmentWitness"
                base["witnessClearances"] = [
                    _witness_clearance(value, plan["vehicleId"])
                    for value in commitments
                ]
            else:
                base["status"] = "prunedWithoutCommitmentWitness"
                base["clearanceStatus"] = "notCalculableFromCommitmentWitness"
        elif candidate_id in c1_plans:
            if c1_plans[candidate_id]["vehicleId"] != plan["vehicleId"]:
                raise RuntimeError("C1 selected candidate vehicle differs")
            base.update(
                {
                    "status": "selectedByC1",
                    "sourceC1PlanActionSha256": comparator._value_hash(
                        "SelectedPlanAction", c1_plans[candidate_id]["action"]
                    ),
                    "clearanceStatus": "notApplicableCandidateSelected",
                }
            )
        else:
            base.update(
                {
                    "status": "absentRetainedOrOmittedNotRecorded",
                    "clearanceStatus": "notRecorded",
                }
            )
        links.append(base)
    return links


def _count_values(links):
    statuses = Counter(link["status"] for link in links)
    kinds = Counter(
        clearance["clearanceKind"]
        for link in links
        for clearance in link.get("witnessClearances", [])
    )
    return (
        {name: statuses[name] for name in _STATUSES},
        {name: kinds[name] for name in _CLEARANCE_KINDS},
    )


def analyze(record_set, behavioral_report, panel_roots, schema):
    recomputed = comparator.analyze(record_set, panel_roots)
    if recomputed != behavioral_report:
        raise RuntimeError("behavioral comparator report does not reproduce exactly")
    bundles = {
        panel_id: comparator._prepare_panel(panel_id, panel_roots[panel_id])
        for panel_id in ("A", "B")
    }
    source_records = {
        (record["panelId"], record["unitId"]): record
        for record in record_set["records"]
    }
    behavior_records = {
        (record["panelId"], record["unitId"]): record
        for record in behavioral_report["records"]
    }
    records = []
    for key in sorted(source_records):
        source = source_records[key]
        behavior = behavior_records[key]
        epoch = behavior["epochId"]
        actions = {}
        evidence = {}
        raw_receipts = {}
        for arm in ("b1", "c1"):
            bundle = bundles[key[0]][source[f"{arm}Label"]]
            envelope = _raw_target_decision(bundle, epoch)
            actions[arm], evidence[arm] = _bind_raw_decision(source, arm, envelope)
            raw_receipts[f"{arm}BundleManifestSha256"] = bundle[
                "bundleManifestSha256"
            ]
            raw_receipts[f"{arm}TranscriptSha256"] = bundle["transcriptSha256"]
        links = _candidate_links(actions["b1"], actions["c1"], evidence["c1"])
        records.append(
            {
                "schemaVersion": "1.0.0",
                "recordType": "ridebound-wp13-recorded-witness-relaxation-v1",
                "panelId": key[0],
                "unitId": key[1],
                "epochId": epoch,
                "simTimeMs": behavior["simTimeMs"],
                "sourceBehavioralComparisonSha256": comparator._value_hash(
                    "BehavioralComparisonRecord", behavior
                ),
                "rawEvidence": raw_receipts,
                "candidateLinks": links,
            }
        )
    all_links = [link for record in records for link in record["candidateLinks"]]
    status_counts, kind_counts = _count_values(all_links)
    panel_summaries = []
    for panel_id in ("A", "B"):
        selected = [record for record in records if record["panelId"] == panel_id]
        links = [link for record in selected for link in record["candidateLinks"]]
        panel_statuses, panel_kinds = _count_values(links)
        panel_summaries.append(
            {
                "panelId": panel_id,
                "recordCount": len(selected),
                "candidateLinkCount": len(links),
                "statusCounts": panel_statuses,
                "clearanceKindCounts": panel_kinds,
            }
        )
    result = {
        "schemaVersion": "1.0.0",
        "reportType": "ridebound-wp13-recorded-witness-relaxation-set-v1",
        "toolIdentity": {
            "calculatorSourceSha256": _sha256(pathlib.Path(__file__).resolve()),
            "schemaSha256": _SCHEMA_SHA256,
        },
        "inputIdentity": {
            "behavioralComparatorLengthBytes": _COMPARATOR_REPORT_LENGTH,
            "behavioralComparatorSha256": _COMPARATOR_REPORT_SHA256,
            "behavioralComparatorSourceSha256": _COMPARATOR_SOURCE_SHA256,
            "firstDivergenceRecordSetLengthBytes": comparator._RECORD_SET_LENGTH,
            "firstDivergenceRecordSetSha256": comparator._RECORD_SET_SHA256,
            "inventoryAnalyzerSourceSha256": comparator._INVENTORY_ANALYZER_SHA256,
            "sourceInventoryReportSha256": comparator._SOURCE_INVENTORY_REPORT[
                "sha256"
            ],
            "solverEvidenceVerifierSourceSha256": comparator._SOURCE_INVENTORY_REPORT[
                "solverEvidenceVerifierSourceSha256"
            ],
        },
        "claimBoundary": {
            "analysisClass": "postOutcomeExploratory",
            "clearanceScope": "recordedWitnessOnly",
            "candidateFeasibilityAfterClearance": "notEvaluated",
            "retainedCandidatePortfolio": "notRecorded",
            "validatorTraversal": "failFastMayHideSubsequentBlockers",
            "interpretation": "descriptiveNotCausal",
            "h6Artifacts": "readOnlyImmutableInputs",
            "confirmatoryGate": None,
        },
        "recordCount": len(records),
        "candidateLinkCount": len(all_links),
        "statusCounts": status_counts,
        "clearanceKindCounts": kind_counts,
        "panelSummaries": panel_summaries,
        "records": records,
    }
    jsonschema.Draft202012Validator(schema).validate(result)
    return result


def _panel_argument(value):
    return comparator._panel_argument(value)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--record-set", required=True, type=pathlib.Path)
    parser.add_argument("--behavioral-report", required=True, type=pathlib.Path)
    parser.add_argument("--panel", action="append", required=True, type=_panel_argument)
    parser.add_argument("--output", type=pathlib.Path)
    arguments = parser.parse_args(argv)
    try:
        panel_roots = dict(arguments.panel)
        if len(panel_roots) != len(arguments.panel):
            raise RuntimeError("duplicate panel identity")
        if arguments.output is not None:
            comparator._require_output_outside_inputs(
                arguments.output, arguments.record_set, panel_roots
            )
            if arguments.output.resolve() == arguments.behavioral_report.resolve():
                raise RuntimeError("output must not overwrite behavioral report")
        record_set = comparator._read_record_set(arguments.record_set)
        behavioral = _read_behavioral_report(arguments.behavioral_report)
        schema = _load_schema()
        result = analyze(record_set, behavioral, panel_roots, schema)
        encoded = inventory._canonical(result) + b"\n"
        if arguments.output is None:
            sys.stdout.buffer.write(encoded)
        else:
            arguments.output.parent.mkdir(parents=True, exist_ok=True)
            arguments.output.write_bytes(encoded)
    except (OSError, RuntimeError, ValueError, TypeError, jsonschema.ValidationError) as error:
        print(f"wp13_recorded_witness_relaxation: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
