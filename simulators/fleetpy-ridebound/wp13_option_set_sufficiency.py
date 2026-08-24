#!/usr/bin/env python3
"""Inventory count-only H6 option-set evidence and explicit missing fields."""

from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import pathlib
import sys

import jsonschema

_ROOT = pathlib.Path(__file__).parent.resolve()
_RELAXATION_SPEC = importlib.util.spec_from_file_location(
    "wp13_recorded_witness_relaxation_for_option_set_sufficiency",
    _ROOT / "wp13_recorded_witness_relaxation.py",
)
relaxation = importlib.util.module_from_spec(_RELAXATION_SPEC)
_RELAXATION_SPEC.loader.exec_module(relaxation)
_REPOSITORY = _ROOT.parent.parent
_SCHEMA = (
    _REPOSITORY
    / "benchmarks"
    / "schemas"
    / "wp13"
    / "v1"
    / "option-set-sufficiency-set.schema.json"
).resolve()
_RELAXATION_SCHEMA = (
    _REPOSITORY
    / "benchmarks"
    / "schemas"
    / "wp13"
    / "v1"
    / "recorded-witness-relaxation-set.schema.json"
).resolve()
_CLASSIFICATION_SCHEMA = (
    _REPOSITORY
    / "benchmarks"
    / "schemas"
    / "wp13"
    / "v1"
    / "mechanism-classification-set.schema.json"
).resolve()
_SCHEMA_SHA256 = "d043d8141771e0e763d83f4d8860738701f18ab7d2023bec0705214249e0d634"
_RELAXATION_LENGTH = 70_531
_RELAXATION_SHA256 = "cdd9a28dd12b91253aa4f848e074d3563312bd0cc13569bc98f17898f739e411"
_RELAXATION_SOURCE_SHA256 = (
    "1ee0abdc060c8cd2d51a3ea6c1331dd059cb8a5b471fa3df6747e3ec61a5acff"
)
_RELAXATION_SCHEMA_SHA256 = (
    "7834f04e8868bf8ea673e28f5d5f4a590ab269f641a912820967bcdb8b18fb1c"
)
_CLASSIFICATION_LENGTH = 44_745
_CLASSIFICATION_SHA256 = (
    "bcc6bed3b1dd8d9c280d7a09125b6fe2e4508eb40bd47ae4da1e2c2fb9f9e9eb"
)
_CLASSIFICATION_SOURCE_SHA256 = (
    "bf11f7e131f20483b1a1e78eaabdc1357e8b319d6be8800a86039612c1c8b14a"
)
_CLASSIFICATION_SCHEMA_SHA256 = (
    "060ef7d063a502752e8cd52765f2e3acdb442b1e1670f612cd4e133d1f25249d"
)
_MAX_INTEGER = 9_007_199_254_740_991
_GENERATION_SUM_FIELDS = (
    "explorationWorkUnits",
    "evaluatedCandidatePathCount",
    "uniqueFeasibleCandidateCountBeforeCap",
    "retainedCandidateCount",
    "physicallyOrSchedulePrunedCount",
    "omittedUnexpandedCandidatePathCount",
    "omittedFeasibleCandidateCountByCap",
    "eligibleRepairRequestCount",
    "consideredRepairRequestCount",
    "omittedRepairRequestCount",
)
_GENERATION_COUNT_FIELDS = (
    "totalPendingRequestCount",
    "consideredRequestCount",
    "omittedRequestCount",
    "vehicleLossRecordCount",
    *_GENERATION_SUM_FIELDS,
    "generationOmissionRecordCount",
    "generationOmissionCountSum",
    "candidateCapAppliedVehicleCount",
    "workBudgetExhaustedVehicleCount",
    "omissionCountWasSaturatedVehicleCount",
)
_PRUNE_COUNT_FIELDS = (
    "prunedCandidateCount",
    "physicalWitnessCandidateCount",
    "commitmentWitnessCandidateCount",
    "mixedWitnessCandidateCount",
    "untypedPrunedCandidateCount",
    "commitmentWitnessCount",
)
_SELECTION_COUNT_FIELDS = (
    "consumedGenerationWorkUnits",
    "consumedValidationWorkUnits",
    "omittedCandidateCount",
    "fallbackValidationAttempts",
    "validationWitnessCount",
)
_SOLVE_STATUSES = {
    "optimal",
    "feasible",
    "infeasible",
    "unknown",
    "modelInvalid",
    "safeFallback",
}
_EXECUTION_PATHS = {
    "none",
    "validatedIncumbent",
    "safeNoOp",
    "greedySingleRequest",
}

sys.dont_write_bytecode = True


def _sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _strict_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"duplicate JSON property {key!r}")
        result[key] = value
    return result


def _canonical(value):
    return json.dumps(
        value,
        ensure_ascii=False,
        separators=(",", ":"),
        sort_keys=True,
    ).encode("utf-8")


def _value_hash(domain, value):
    prefix = b"RideBound.Wp13." + domain.encode("ascii") + b".v1\0"
    return hashlib.sha256(prefix + _canonical(value)).hexdigest()


def _read_exact(path, length, digest, description):
    data = path.read_bytes()
    if len(data) != length or hashlib.sha256(data).hexdigest() != digest:
        raise RuntimeError(f"{description} identity differs")
    if not data.endswith(b"\n") or data.endswith(b"\r\n"):
        raise RuntimeError(f"{description} framing differs")
    try:
        value = json.loads(data[:-1], object_pairs_hook=_strict_object)
    except (UnicodeDecodeError, ValueError, TypeError, json.JSONDecodeError) as error:
        raise RuntimeError(f"{description} JSON is invalid") from error
    if _canonical(value) != data[:-1]:
        raise RuntimeError(f"{description} is not canonical JSON")
    return value


def _load_schema(path, digest, description):
    data = path.read_bytes()
    if hashlib.sha256(data).hexdigest() != digest:
        raise RuntimeError(f"{description} schema identity differs")
    try:
        schema = json.loads(data, object_pairs_hook=_strict_object)
        jsonschema.Draft202012Validator.check_schema(schema)
    except (ValueError, TypeError, json.JSONDecodeError, jsonschema.SchemaError) as error:
        raise RuntimeError(f"{description} schema is invalid") from error
    return schema


def _read_inputs(record_path, behavior_path, relaxation_path, classification_path):
    record_set = relaxation.comparator._read_record_set(record_path)
    behavior = relaxation._read_behavioral_report(behavior_path)
    relaxation_report = _read_exact(
        relaxation_path,
        _RELAXATION_LENGTH,
        _RELAXATION_SHA256,
        "relaxation report",
    )
    classification = _read_exact(
        classification_path,
        _CLASSIFICATION_LENGTH,
        _CLASSIFICATION_SHA256,
        "classification report",
    )
    relaxation_schema = _load_schema(
        _RELAXATION_SCHEMA,
        _RELAXATION_SCHEMA_SHA256,
        "relaxation",
    )
    classification_schema = _load_schema(
        _CLASSIFICATION_SCHEMA,
        _CLASSIFICATION_SCHEMA_SHA256,
        "classification",
    )
    jsonschema.Draft202012Validator(relaxation_schema).validate(relaxation_report)
    jsonschema.Draft202012Validator(classification_schema).validate(classification)
    if relaxation_report.get("toolIdentity") != {
        "calculatorSourceSha256": _RELAXATION_SOURCE_SHA256,
        "schemaSha256": _RELAXATION_SCHEMA_SHA256,
    }:
        raise RuntimeError("relaxation tool identity differs")
    if classification.get("toolIdentity") != {
        "classifierSourceSha256": _CLASSIFICATION_SOURCE_SHA256,
        "schemaSha256": _CLASSIFICATION_SCHEMA_SHA256,
    }:
        raise RuntimeError("classification tool identity differs")
    if classification.get("inputIdentity", {}).get("relaxationReportSha256") != (
        _RELAXATION_SHA256
    ):
        raise RuntimeError("classification does not bind relaxation input")
    return record_set, behavior, relaxation_report, classification


def _safe_integer(value, field):
    if (
        isinstance(value, bool)
        or not isinstance(value, int)
        or not 0 <= value <= _MAX_INTEGER
    ):
        raise RuntimeError(f"{field} must be a nonnegative safe integer")
    return value


def _boolean(value, field):
    if not isinstance(value, bool):
        raise RuntimeError(f"{field} must be boolean")
    return value


def _text(value, field):
    if not isinstance(value, str) or not value:
        raise RuntimeError(f"{field} must be non-empty text")
    return value


def _hash_text(value, field):
    value = _text(value, field)
    if len(value) != 64 or any(character not in "0123456789abcdef" for character in value):
        raise RuntimeError(f"{field} must be a lowercase SHA-256")
    return value


def _exact_keys(value, required, optional, field):
    if not isinstance(value, dict) or not set(value) >= set(required):
        raise RuntimeError(f"{field} required shape differs")
    unexpected = set(value) - set(required) - set(optional)
    if unexpected:
        raise RuntimeError(f"{field} has unsupported fields {sorted(unexpected)!r}")


def _generation_counts(generation):
    _exact_keys(
        generation,
        (
            "totalPendingRequestCount",
            "consideredRequestCount",
            "omittedRequestCount",
            "vehicleLosses",
            "omissions",
        ),
        (),
        "generation",
    )
    total = _safe_integer(
        generation["totalPendingRequestCount"],
        "generation totalPendingRequestCount",
    )
    considered = _safe_integer(
        generation["consideredRequestCount"],
        "generation consideredRequestCount",
    )
    omitted = _safe_integer(
        generation["omittedRequestCount"],
        "generation omittedRequestCount",
    )
    if total != considered + omitted:
        raise RuntimeError("generation request counts do not conserve")
    losses = generation["vehicleLosses"]
    omissions = generation["omissions"]
    if not isinstance(losses, list) or not isinstance(omissions, list):
        raise RuntimeError("generation arrays differ")
    sums = {name: 0 for name in _GENERATION_SUM_FIELDS}
    flag_counts = {
        "candidateCapAppliedVehicleCount": 0,
        "workBudgetExhaustedVehicleCount": 0,
        "omissionCountWasSaturatedVehicleCount": 0,
    }
    vehicle_ids = set()
    loss_required = (
        "vehicleId",
        *_GENERATION_SUM_FIELDS,
        "workBudgetExhausted",
        "candidateCapApplied",
        "omissionCountWasSaturated",
    )
    for index, loss in enumerate(losses):
        _exact_keys(loss, loss_required, (), f"vehicle loss {index}")
        vehicle_id = _text(loss["vehicleId"], f"vehicle loss {index} vehicleId")
        if vehicle_id in vehicle_ids:
            raise RuntimeError("generation contains duplicate vehicle loss")
        vehicle_ids.add(vehicle_id)
        values = {}
        for name in _GENERATION_SUM_FIELDS:
            values[name] = _safe_integer(loss[name], f"vehicle loss {index} {name}")
            sums[name] += values[name]
        if values["uniqueFeasibleCandidateCountBeforeCap"] != (
            values["retainedCandidateCount"]
            + values["omittedFeasibleCandidateCountByCap"]
        ):
            raise RuntimeError("vehicle candidate-cap counts do not conserve")
        if values["eligibleRepairRequestCount"] != (
            values["consideredRepairRequestCount"]
            + values["omittedRepairRequestCount"]
        ):
            raise RuntimeError("vehicle repair-request counts do not conserve")
        if (
            values["omittedFeasibleCandidateCountByCap"] > 0
            and not loss["candidateCapApplied"]
        ):
            raise RuntimeError("candidate-cap omission lacks applied flag")
        if (
            values["omittedUnexpandedCandidatePathCount"] > 0
            and not loss["workBudgetExhausted"]
        ):
            raise RuntimeError("unexpanded omission lacks work-exhausted flag")
        for raw_name, output_name in (
            ("candidateCapApplied", "candidateCapAppliedVehicleCount"),
            ("workBudgetExhausted", "workBudgetExhaustedVehicleCount"),
            (
                "omissionCountWasSaturated",
                "omissionCountWasSaturatedVehicleCount",
            ),
        ):
            flag_counts[output_name] += int(
                _boolean(loss[raw_name], f"vehicle loss {index} {raw_name}")
            )
    omission_count_sum = 0
    for index, omission in enumerate(omissions):
        _exact_keys(
            omission,
            ("code", "count", "stableDigest", "countWasSaturated"),
            ("vehicleId", "requestIds"),
            f"generation omission {index}",
        )
        _text(omission["code"], f"generation omission {index} code")
        _hash_text(
            omission["stableDigest"],
            f"generation omission {index} digest",
        )
        omission_count_sum += _safe_integer(
            omission["count"],
            f"generation omission {index} count",
        )
        _boolean(
            omission["countWasSaturated"],
            f"generation omission {index} saturation",
        )
    counts = {
        "totalPendingRequestCount": total,
        "consideredRequestCount": considered,
        "omittedRequestCount": omitted,
        "vehicleLossRecordCount": len(losses),
        **sums,
        "generationOmissionRecordCount": len(omissions),
        "generationOmissionCountSum": omission_count_sum,
        **flag_counts,
    }
    complete = omitted == 0 and all(
        sums[name] == 0
        for name in (
            "omittedUnexpandedCandidatePathCount",
            "omittedFeasibleCandidateCountByCap",
            "omittedRepairRequestCount",
        )
    )
    return counts, complete


def _prune_counts(pruned):
    if not isinstance(pruned, list):
        raise RuntimeError("prunedCandidates must be an array")
    result = {name: 0 for name in _PRUNE_COUNT_FIELDS}
    result["prunedCandidateCount"] = len(pruned)
    candidate_ids = set()
    for index, witness in enumerate(pruned):
        _exact_keys(
            witness,
            (
                "candidateId",
                "vehicleId",
                "newRequestIds",
                "code",
                "commitmentWitnesses",
            ),
            ("physicalWitness",),
            f"prune witness {index}",
        )
        candidate_id = _text(
            witness["candidateId"],
            f"prune witness {index} candidateId",
        )
        if candidate_id in candidate_ids:
            raise RuntimeError("prune evidence contains duplicate candidate ID")
        candidate_ids.add(candidate_id)
        _text(witness["vehicleId"], f"prune witness {index} vehicleId")
        _text(witness["code"], f"prune witness {index} code")
        if not isinstance(witness["newRequestIds"], list) or not isinstance(
            witness["commitmentWitnesses"],
            list,
        ):
            raise RuntimeError("prune witness arrays differ")
        request_ids = [
            _text(value, f"prune witness {index} newRequestId")
            for value in witness["newRequestIds"]
        ]
        if not request_ids or len(request_ids) != len(set(request_ids)):
            raise RuntimeError("prune witness request identities differ")
        physical = "physicalWitness" in witness
        commitment = bool(witness["commitmentWitnesses"])
        if physical:
            physical_value = witness["physicalWitness"]
            if not isinstance(physical_value, dict) or (
                physical_value.get("code") != witness["code"]
                or physical_value.get("vehicleId") != witness["vehicleId"]
            ):
                raise RuntimeError("physical prune witness binding differs")
        commitment_hashes = set()
        for commitment_index, commitment_value in enumerate(
            witness["commitmentWitnesses"]
        ):
            if not isinstance(commitment_value, dict) or (
                commitment_value.get("code") != witness["code"]
                or commitment_value.get("vehicleId") != witness["vehicleId"]
                or commitment_value.get("stage")
                not in {"state", "physical", "projection", "lock", "budget", "ledger"}
            ):
                raise RuntimeError("commitment prune witness binding differs")
            commitment_hash = _value_hash(
                "OptionSetCommitmentWitness",
                commitment_value,
            )
            if commitment_hash in commitment_hashes:
                raise RuntimeError("duplicate commitment prune witness")
            commitment_hashes.add(commitment_hash)
        result["commitmentWitnessCount"] += len(witness["commitmentWitnesses"])
        if physical and commitment:
            result["mixedWitnessCandidateCount"] += 1
        elif physical:
            result["physicalWitnessCandidateCount"] += 1
        elif commitment:
            result["commitmentWitnessCandidateCount"] += 1
        else:
            result["untypedPrunedCandidateCount"] += 1
    if result["prunedCandidateCount"] != sum(
        result[name]
        for name in (
            "physicalWitnessCandidateCount",
            "commitmentWitnessCandidateCount",
            "mixedWitnessCandidateCount",
            "untypedPrunedCandidateCount",
        )
    ):
        raise RuntimeError("prune witness counts do not conserve")
    return result


def _selection_values(selection, generation_counts):
    _exact_keys(
        selection,
        (
            "consumedGenerationWorkUnits",
            "consumedValidationWorkUnits",
            "omittedCandidateCount",
            "omissionCountWasSaturated",
            "primarySolveStatus",
            "primarySolverDiagnostics",
            "finalSolveStatus",
            "finalSolverDiagnostics",
            "executionPath",
            "fallbackValidationAttempts",
            "primaryIncumbentRejected",
            "validationWitnesses",
        ),
        ("omissionDigest",),
        "selection",
    )
    if not isinstance(selection["validationWitnesses"], list):
        raise RuntimeError("selection validationWitnesses must be an array")
    counts = {
        name: _safe_integer(selection[name], f"selection {name}")
        for name in _SELECTION_COUNT_FIELDS
        if name != "validationWitnessCount"
    }
    counts["validationWitnessCount"] = len(selection["validationWitnesses"])
    omitted_count = counts["omittedCandidateCount"]
    if omitted_count > 0:
        _hash_text(selection.get("omissionDigest"), "selection omissionDigest")
    elif "omissionDigest" in selection:
        _hash_text(selection["omissionDigest"], "selection omissionDigest")
    if counts["consumedGenerationWorkUnits"] != generation_counts[
        "explorationWorkUnits"
    ]:
        raise RuntimeError("selection/generation work units disagree")
    statuses = {
        "omissionCountWasSaturated": _boolean(
            selection["omissionCountWasSaturated"],
            "selection omissionCountWasSaturated",
        ),
        "primarySolveStatus": selection["primarySolveStatus"],
        "finalSolveStatus": selection["finalSolveStatus"],
        "executionPath": selection["executionPath"],
        "primaryIncumbentRejected": _boolean(
            selection["primaryIncumbentRejected"],
            "selection primaryIncumbentRejected",
        ),
    }
    if (
        statuses["primarySolveStatus"] not in _SOLVE_STATUSES
        or statuses["finalSolveStatus"] not in _SOLVE_STATUSES
        or statuses["executionPath"] not in _EXECUTION_PATHS
    ):
        raise RuntimeError("selection categorical status is unsupported")
    return counts, statuses


def _arm_covariates(evidence):
    _exact_keys(
        evidence,
        ("evidenceVersion", "generation", "prunedCandidates", "selection"),
        (),
        "execution evidence",
    )
    if evidence["evidenceVersion"] != "1.0.0":
        raise RuntimeError("execution evidence version differs from frozen H6")
    generation_counts, complete = _generation_counts(evidence["generation"])
    prune_counts = _prune_counts(evidence["prunedCandidates"])
    selection_counts, selection_statuses = _selection_values(
        evidence["selection"],
        generation_counts,
    )
    return {
        "evidenceVersion": "1.0.0",
        "generationEvidenceSha256": _value_hash(
            "OptionSetGenerationEvidence",
            evidence["generation"],
        ),
        "selectionEvidenceSha256": _value_hash(
            "OptionSetSelectionEvidence",
            evidence["selection"],
        ),
        "generationCounts": generation_counts,
        "generationCompleteFromRecordedCounters": complete,
        "pruneCounts": prune_counts,
        "selectionCounts": selection_counts,
        "selectionStatuses": selection_statuses,
    }


def _delta(c1, b1, fields):
    return {name: c1[name] - b1[name] for name in fields}


def _record(
    source,
    behavior,
    relaxation_record,
    classification_record,
    arms,
):
    key = (source["panelId"], source["unitId"])
    expected_behavior_hash = _value_hash("BehavioralComparisonRecord", behavior)
    expected_relaxation_hash = _value_hash(
        "RecordedWitnessRelaxationRecord",
        relaxation_record,
    )
    if (
        behavior["sourceFirstDivergenceRecordSha256"]
        != _value_hash("FirstDivergenceRecord", source)
        or relaxation_record["sourceBehavioralComparisonSha256"]
        != expected_behavior_hash
        or classification_record["sourceBehavioralComparisonSha256"]
        != expected_behavior_hash
        or classification_record["sourceRelaxationRecordSha256"]
        != expected_relaxation_hash
    ):
        raise RuntimeError(f"{key}: source record binding differs")
    if len(relaxation_record["candidateLinks"]) != len(
        classification_record["candidateClassifications"]
    ):
        raise RuntimeError(f"{key}: candidate classification inventory differs")
    if [link["candidateId"] for link in relaxation_record["candidateLinks"]] != [
        value["candidateId"]
        for value in classification_record["candidateClassifications"]
    ]:
        raise RuntimeError(f"{key}: candidate classification identities differ")
    unresolved = sum(
        link["status"] == "absentRetainedOrOmittedNotRecorded"
        for link in relaxation_record["candidateLinks"]
    )
    has_indeterminate = (
        "rankingOrSearchOmissionIndeterminate"
        in classification_record["evidenceClasses"]
    )
    if (unresolved > 0) != has_indeterminate:
        raise RuntimeError(f"{key}: unresolved candidate class differs")
    b1 = arms["b1"]
    c1 = arms["c1"]
    all_b1_counts = (
        b1["generationCounts"],
        b1["pruneCounts"],
        b1["selectionCounts"],
    )
    all_c1_counts = (
        c1["generationCounts"],
        c1["pruneCounts"],
        c1["selectionCounts"],
    )
    return {
        "schemaVersion": "1.0.0",
        "recordType": "ridebound-wp13-option-set-sufficiency-v1",
        "panelId": key[0],
        "unitId": key[1],
        "epochId": behavior["epochId"],
        "simTimeMs": behavior["simTimeMs"],
        "sourceFirstDivergenceRecordSha256": _value_hash(
            "FirstDivergenceRecord",
            source,
        ),
        "sourceBehavioralComparisonSha256": expected_behavior_hash,
        "sourceRelaxationRecordSha256": expected_relaxation_hash,
        "sourceClassificationRecordSha256": _value_hash(
            "MechanismClassificationRecord",
            classification_record,
        ),
        "rawEvidence": relaxation_record["rawEvidence"],
        "immediateAcceptanceRelation": classification_record[
            "immediateAcceptanceRelation"
        ],
        "evidenceClasses": classification_record["evidenceClasses"],
        "candidateIdentityGap": {
            "unresolvedCandidateLinkCount": unresolved,
            "resolutionStatus": (
                "notRecorded" if unresolved else "notApplicableNoUnresolvedLink"
            ),
        },
        "arms": arms,
        "pairedRelations": {
            "generationEvidenceRelation": (
                "exactEqual"
                if b1["generationEvidenceSha256"]
                == c1["generationEvidenceSha256"]
                else "different"
            ),
            "allRecordedCountCovariatesRelation": (
                "exactEqual" if all_b1_counts == all_c1_counts else "different"
            ),
            "selectionEvidenceRelation": (
                "exactEqual"
                if b1["selectionEvidenceSha256"]
                == c1["selectionEvidenceSha256"]
                else "different"
            ),
            "generationCountDeltasC1MinusB1": _delta(
                c1["generationCounts"],
                b1["generationCounts"],
                _GENERATION_COUNT_FIELDS,
            ),
            "pruneCountDeltasC1MinusB1": _delta(
                c1["pruneCounts"],
                b1["pruneCounts"],
                _PRUNE_COUNT_FIELDS,
            ),
            "selectionCountDeltasC1MinusB1": _delta(
                c1["selectionCounts"],
                b1["selectionCounts"],
                _SELECTION_COUNT_FIELDS,
            ),
            "candidateIdentityEquality": "notEstablishedByAggregateEquality",
        },
        "causalAttribution": "notEstablished",
        "downstreamTrajectory": "notEvaluated",
    }


def _arm_totals(records, arm):
    values = [record["arms"][arm] for record in records]
    return {
        "armEpochCount": len(values),
        "generationCompleteEpochCount": sum(
            value["generationCompleteFromRecordedCounters"] for value in values
        ),
        "candidateCapAppliedEpochCount": sum(
            value["generationCounts"]["candidateCapAppliedVehicleCount"] > 0
            for value in values
        ),
        "workBudgetExhaustedEpochCount": sum(
            value["generationCounts"]["workBudgetExhaustedVehicleCount"] > 0
            for value in values
        ),
        "generationOmissionEpochCount": sum(
            not value["generationCompleteFromRecordedCounters"]
            for value in values
        ),
        "selectionOmissionEpochCount": sum(
            value["selectionCounts"]["omittedCandidateCount"] > 0
            for value in values
        ),
        "retainedCandidateCount": sum(
            value["generationCounts"]["retainedCandidateCount"] for value in values
        ),
        "prunedCandidateCount": sum(
            value["pruneCounts"]["prunedCandidateCount"] for value in values
        ),
        "commitmentWitnessCandidateCount": sum(
            value["pruneCounts"]["commitmentWitnessCandidateCount"]
            + value["pruneCounts"]["mixedWitnessCandidateCount"]
            for value in values
        ),
        "consumedValidationWorkUnits": sum(
            value["selectionCounts"]["consumedValidationWorkUnits"]
            for value in values
        ),
    }


def _summary(panel_id, records):
    return {
        "panelId": panel_id,
        "recordCount": len(records),
        "armEpochCount": len(records) * 2,
        "unresolvedCandidateLinkCount": sum(
            record["candidateIdentityGap"]["unresolvedCandidateLinkCount"]
            for record in records
        ),
        "unresolvedPairCount": sum(
            record["candidateIdentityGap"]["unresolvedCandidateLinkCount"] > 0
            for record in records
        ),
        "exactGenerationEvidencePairCount": sum(
            record["pairedRelations"]["generationEvidenceRelation"] == "exactEqual"
            for record in records
        ),
        "exactRecordedCountCovariatePairCount": sum(
            record["pairedRelations"]["allRecordedCountCovariatesRelation"]
            == "exactEqual"
            for record in records
        ),
        "armTotals": {
            arm: _arm_totals(records, arm) for arm in ("b1", "c1")
        },
    }


def build(records, schema):
    if len(records) != 40:
        raise RuntimeError("option-set record inventory differs")
    keys = [(record["panelId"], record["unitId"]) for record in records]
    if len(set(keys)) != 40 or keys != sorted(keys):
        raise RuntimeError("option-set record identity/order differs")
    panels = []
    for panel_id in ("A", "B"):
        selected = [record for record in records if record["panelId"] == panel_id]
        if len(selected) != 20:
            raise RuntimeError("panel record inventory differs")
        panels.append(_summary(panel_id, selected))
    unresolved_links = sum(
        record["candidateIdentityGap"]["unresolvedCandidateLinkCount"]
        for record in records
    )
    unresolved_pairs = sum(
        record["candidateIdentityGap"]["unresolvedCandidateLinkCount"] > 0
        for record in records
    )
    trigger_reasons = []
    if unresolved_links:
        trigger_reasons.append("unresolvedCandidateIdentityLinks")
    trigger_reasons.extend(
        ("retainedPortfolioNotRecorded", "postClearanceStateNotEvaluated")
    )
    result = {
        "schemaVersion": "1.0.0",
        "reportType": "ridebound-wp13-option-set-sufficiency-set-v1",
        "toolIdentity": {
            "analyzerSourceSha256": _sha256(pathlib.Path(__file__).resolve()),
            "schemaSha256": _SCHEMA_SHA256,
        },
        "inputIdentity": {
            "firstDivergenceRecordSetSha256": (
                relaxation.comparator._RECORD_SET_SHA256
            ),
            "behavioralReportSha256": relaxation._COMPARATOR_REPORT_SHA256,
            "relaxationReportLengthBytes": _RELAXATION_LENGTH,
            "relaxationReportSha256": _RELAXATION_SHA256,
            "relaxationCalculatorSourceSha256": _RELAXATION_SOURCE_SHA256,
            "relaxationSchemaSha256": _RELAXATION_SCHEMA_SHA256,
            "classificationReportLengthBytes": _CLASSIFICATION_LENGTH,
            "classificationReportSha256": _CLASSIFICATION_SHA256,
            "classificationSourceSha256": _CLASSIFICATION_SOURCE_SHA256,
            "classificationSchemaSha256": _CLASSIFICATION_SCHEMA_SHA256,
        },
        "claimBoundary": {
            "analysisClass": "postOutcomeExploratory",
            "optionSetScope": "recordedCountWorkOmissionAndSelectionCovariates",
            "candidateIdentityEquality": "notEstablishedByAggregateEquality",
            "candidateFeasibility": "notEvaluated",
            "causalAttribution": "notEstablished",
            "downstreamTrajectory": "notEvaluated",
            "interpretation": "descriptiveNotCausal",
            "h6Artifacts": "readOnlyImmutableInputs",
            "confirmatoryGate": None,
        },
        "fieldAvailability": {
            "generationCountWorkOmissionCovariates": "recordedCountOnly",
            "pruneWitnessCounts": "recordedCountOnly",
            "selectionCountWorkStatusCovariates": "recordedCountOnly",
            "fullRetainedCandidateIdentityPortfolio": "notRecorded",
            "retainedCandidateRouteSchedulePortfolio": "notRecorded",
            "perCandidateObjectiveVectorPortfolio": "notRecorded",
            "perCandidateCommitmentVectorPortfolio": "notRecorded",
            "prunedCandidateRouteSchedule": "notRecorded",
            "candidateRankingPosition": "notRecorded",
            "subsequentValidatorBlockersAfterFirstWitness": "notRecorded",
            "candidateFeasibilityAfterClearance": "notEvaluated",
        },
        "blockedQuestions": [
            "candidateLevelReranking",
            "retainedCandidateRouteScheduleComparison",
            "postRelaxationCandidateFeasibility",
            "laterValidatorBlockerEnumeration",
        ],
        "evidenceVNextDecision": {
            "verdict": "requiredForCandidateLevelPortfolioAndReplayQuestions",
            "unresolvedCandidateLinkCount": unresolved_links,
            "unresolvedPairCount": unresolved_pairs,
            "triggerReasons": trigger_reasons,
            "authorizesExploratoryRerun": False,
            "h6BackfillProhibited": True,
        },
        "recordCount": len(records),
        "armEpochCount": len(records) * 2,
        "panelSummaries": panels,
        "records": records,
    }
    jsonschema.Draft202012Validator(schema).validate(result)
    return result


def analyze(
    record_set,
    behavior,
    relaxation_report,
    classification,
    panel_roots,
    schema,
):
    if any(
        len(report["records"]) != 40
        for report in (record_set, behavior, relaxation_report, classification)
    ):
        raise RuntimeError("source record inventory differs")
    source_records = {
        (record["panelId"], record["unitId"]): record
        for record in record_set["records"]
    }
    behavior_records = {
        (record["panelId"], record["unitId"]): record
        for record in behavior["records"]
    }
    relaxation_records = {
        (record["panelId"], record["unitId"]): record
        for record in relaxation_report["records"]
    }
    classification_records = {
        (record["panelId"], record["unitId"]): record
        for record in classification["records"]
    }
    inventories = (
        source_records,
        behavior_records,
        relaxation_records,
        classification_records,
    )
    if any(len(values) != 40 for values in inventories) or any(
        set(values) != set(source_records) for values in inventories[1:]
    ):
        raise RuntimeError("source record inventory differs")
    bundles = {
        panel_id: relaxation.comparator._prepare_panel(
            panel_id,
            panel_roots[panel_id],
        )
        for panel_id in ("A", "B")
    }
    records = []
    for key in sorted(source_records):
        source = source_records[key]
        behavior_record = behavior_records[key]
        relaxation_record = relaxation_records[key]
        classification_record = classification_records[key]
        first = source["firstDivergence"]
        if not (
            first["b1Evidence"]["epochId"]
            == first["c1Evidence"]["epochId"]
            == behavior_record["epochId"]
            == relaxation_record["epochId"]
            == classification_record["epochId"]
        ) or not (
            first["b1Evidence"]["simTimeMs"]
            == first["c1Evidence"]["simTimeMs"]
            == behavior_record["simTimeMs"]
            == relaxation_record["simTimeMs"]
            == classification_record["simTimeMs"]
        ):
            raise RuntimeError(f"{key}: target epoch/time differs")
        arms = {}
        actual_receipts = {}
        for arm in ("b1", "c1"):
            bundle = bundles[key[0]][source[f"{arm}Label"]]
            envelope = relaxation._raw_target_decision(
                bundle,
                behavior_record["epochId"],
            )
            _, evidence = relaxation._bind_raw_decision(source, arm, envelope)
            arms[arm] = _arm_covariates(evidence)
            actual_receipts[f"{arm}BundleManifestSha256"] = bundle[
                "bundleManifestSha256"
            ]
            actual_receipts[f"{arm}TranscriptSha256"] = bundle["transcriptSha256"]
        if actual_receipts != relaxation_record["rawEvidence"]:
            raise RuntimeError(f"{key}: raw evidence receipts differ")
        records.append(
            _record(
                source,
                behavior_record,
                relaxation_record,
                classification_record,
                arms,
            )
        )
    return build(records, schema)


def _panel_argument(value):
    return relaxation._panel_argument(value)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--record-set", required=True, type=pathlib.Path)
    parser.add_argument("--behavioral-report", required=True, type=pathlib.Path)
    parser.add_argument("--relaxation-report", required=True, type=pathlib.Path)
    parser.add_argument("--classification-report", required=True, type=pathlib.Path)
    parser.add_argument("--panel", action="append", required=True, type=_panel_argument)
    parser.add_argument("--output", type=pathlib.Path)
    arguments = parser.parse_args(argv)
    try:
        panel_roots = dict(arguments.panel)
        if len(panel_roots) != len(arguments.panel) or set(panel_roots) != {"A", "B"}:
            raise RuntimeError("exact Panel A/B roots are required")
        if arguments.output is not None:
            relaxation.comparator._require_output_outside_inputs(
                arguments.output,
                arguments.record_set,
                panel_roots,
            )
            if arguments.output.resolve() in {
                arguments.behavioral_report.resolve(),
                arguments.relaxation_report.resolve(),
                arguments.classification_report.resolve(),
            }:
                raise RuntimeError("output must not overwrite an input report")
        inputs = _read_inputs(
            arguments.record_set,
            arguments.behavioral_report,
            arguments.relaxation_report,
            arguments.classification_report,
        )
        schema = _load_schema(_SCHEMA, _SCHEMA_SHA256, "option-set sufficiency")
        encoded = _canonical(analyze(*inputs, panel_roots, schema)) + b"\n"
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
        KeyError,
        jsonschema.ValidationError,
    ) as error:
        print(f"wp13_option_set_sufficiency: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
