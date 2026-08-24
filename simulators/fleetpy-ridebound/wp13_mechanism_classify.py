#!/usr/bin/env python3
"""Classify first-divergence evidence without making causal claims."""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import sys
from collections import Counter

import jsonschema


_ROOT = pathlib.Path(__file__).parent.resolve()
_REPOSITORY = _ROOT.parent.parent
_SCHEMA = (
    _REPOSITORY
    / "benchmarks"
    / "schemas"
    / "wp13"
    / "v1"
    / "mechanism-classification-set.schema.json"
).resolve()
_RELAXATION_SCHEMA = (
    _REPOSITORY
    / "benchmarks"
    / "schemas"
    / "wp13"
    / "v1"
    / "recorded-witness-relaxation-set.schema.json"
).resolve()
_BEHAVIOR_LENGTH = 79_864
_BEHAVIOR_SHA256 = "3717f093c62c37a339da0b826323fb1604a684bd9990630d9d9dc5563fd4f7e3"
_BEHAVIOR_SOURCE_SHA256 = (
    "f2c55e1f7fbe9cb341cb6c75764a192254aa2e375de0547780c94c83b01dd0ee"
)
_RELAXATION_LENGTH = 70_531
_RELAXATION_SHA256 = "cdd9a28dd12b91253aa4f848e074d3563312bd0cc13569bc98f17898f739e411"
_RELAXATION_SOURCE_SHA256 = (
    "1ee0abdc060c8cd2d51a3ea6c1331dd059cb8a5b471fa3df6747e3ec61a5acff"
)
_RELAXATION_SCHEMA_SHA256 = (
    "7834f04e8868bf8ea673e28f5d5f4a590ab269f641a912820967bcdb8b18fb1c"
)
_SCHEMA_SHA256 = "060ef7d063a502752e8cd52765f2e3acdb442b1e1670f612cd4e133d1f25249d"
_CLASSES = (
    "recordedBudgetWitness",
    "recordedLockWitness",
    "recordedPhysicalPruneCode",
    "sharedSelectedCandidate",
    "rankingOrSearchOmissionIndeterminate",
    "unsupportedRecordedPrune",
)
_RELATIONS = (
    "c1HigherImmediateAcceptance",
    "c1LowerImmediateAcceptance",
    "equalImmediateAcceptance",
)
_PHYSICAL_CODES = {
    "UNKNOWN_VEHICLE",
    "UNKNOWN_REQUEST",
    "PLAN_VERSION",
    "FROZEN_PREFIX",
    "ROUTE_CONNECTIVITY",
    "INVALID_POSITION",
    "INVALID_ROUTE_STOP",
    "STOP_LOCATION",
    "PRECEDENCE",
    "CAPACITY",
    "PICKUP_WINDOW",
    "MAX_RIDE_TIME",
    "ONBOARD_PRESERVATION",
    "ACCEPTED_PRESERVATION",
    "REASSIGNMENT",
    "SCHEDULE_OVERFLOW",
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
    return hashlib.sha256(
        b"RideBound.Wp13." + domain.encode("ascii") + b".v1\0" + _canonical(value)
    ).hexdigest()


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


def _load_schema(path, expected_hash, description):
    data = path.read_bytes()
    if hashlib.sha256(data).hexdigest() != expected_hash:
        raise RuntimeError(f"{description} schema identity differs")
    try:
        schema = json.loads(data, object_pairs_hook=_strict_object)
        jsonschema.Draft202012Validator.check_schema(schema)
    except (ValueError, TypeError, json.JSONDecodeError, jsonschema.SchemaError) as error:
        raise RuntimeError(f"{description} schema is invalid") from error
    return schema


def _read_inputs(behavior_path, relaxation_path):
    behavior = _read_exact(
        behavior_path,
        _BEHAVIOR_LENGTH,
        _BEHAVIOR_SHA256,
        "behavioral report",
    )
    relaxation = _read_exact(
        relaxation_path,
        _RELAXATION_LENGTH,
        _RELAXATION_SHA256,
        "relaxation report",
    )
    relaxation_schema = _load_schema(
        _RELAXATION_SCHEMA,
        _RELAXATION_SCHEMA_SHA256,
        "relaxation",
    )
    jsonschema.Draft202012Validator(relaxation_schema).validate(relaxation)
    if behavior.get("toolIdentity", {}).get("comparatorSourceSha256") != (
        _BEHAVIOR_SOURCE_SHA256
    ):
        raise RuntimeError("behavioral source identity differs")
    if relaxation.get("toolIdentity") != {
        "calculatorSourceSha256": _RELAXATION_SOURCE_SHA256,
        "schemaSha256": _RELAXATION_SCHEMA_SHA256,
    }:
        raise RuntimeError("relaxation tool identity differs")
    if relaxation.get("inputIdentity", {}).get("behavioralComparatorSha256") != (
        _BEHAVIOR_SHA256
    ):
        raise RuntimeError("relaxation report does not bind behavioral input")
    return behavior, relaxation


def _classify_link(link):
    status = link["status"]
    if status == "prunedWithCommitmentWitness":
        kinds = {value["clearanceKind"] for value in link["witnessClearances"]}
        supported = {
            "numericBudgetLimitIncrease": "recordedBudgetWitness",
            "categoricalLockDisablement": "recordedLockWitness",
        }
        if not kinds or not kinds <= set(supported):
            raise RuntimeError("commitment clearance kind is unsupported")
        classes = [name for name in _CLASSES if name in {supported[k] for k in kinds}]
        strength = "recordedCandidateLinkNotCausal"
    elif status == "selectedByC1":
        classes = ["sharedSelectedCandidate"]
        strength = "recordedCandidateLinkNotCausal"
    elif status == "absentRetainedOrOmittedNotRecorded":
        classes = ["rankingOrSearchOmissionIndeterminate"]
        strength = "indeterminateMissingPortfolio"
    elif status == "prunedWithoutCommitmentWitness":
        if link["pruneCode"] in _PHYSICAL_CODES:
            classes = ["recordedPhysicalPruneCode"]
            strength = "recordedCandidateLinkNotCausal"
        else:
            classes = ["unsupportedRecordedPrune"]
            strength = "unsupportedRecordedEvidence"
    else:
        raise RuntimeError(f"unsupported candidate-link status {status!r}")
    return {
        "candidateId": link["candidateId"],
        "sourceCandidateLinkSha256": _value_hash("RecordedWitnessCandidateLink", link),
        "sourceStatus": status,
        "evidenceClasses": classes,
        "evidenceStrength": strength,
    }


def _empty_class_counts():
    return {name: 0 for name in _CLASSES}


def _empty_cross_tab():
    return {name: {relation: 0 for relation in _RELATIONS} for name in _CLASSES}


def _summarize(records):
    class_counts = Counter()
    cross_tab = _empty_cross_tab()
    for record in records:
        relation = record["immediateAcceptanceRelation"]
        for evidence_class in record["evidenceClasses"]:
            class_counts[evidence_class] += 1
            cross_tab[evidence_class][relation] += 1
    return (
        {name: class_counts[name] for name in _CLASSES},
        cross_tab,
    )


def build(behavior, relaxation, schema):
    if len(behavior["records"]) != 40 or len(relaxation["records"]) != 40:
        raise RuntimeError("source record inventory differs")
    behavior_records = {
        (record["panelId"], record["unitId"]): record
        for record in behavior["records"]
    }
    relaxation_records = {
        (record["panelId"], record["unitId"]): record
        for record in relaxation["records"]
    }
    if (
        len(behavior_records) != 40
        or len(relaxation_records) != 40
        or set(behavior_records) != set(relaxation_records)
    ):
        raise RuntimeError("source record inventory differs")
    records = []
    for key in sorted(behavior_records):
        behavior_record = behavior_records[key]
        relaxation_record = relaxation_records[key]
        expected_behavior_hash = _value_hash(
            "BehavioralComparisonRecord",
            behavior_record,
        )
        if (
            relaxation_record["sourceBehavioralComparisonSha256"]
            != expected_behavior_hash
            or relaxation_record["epochId"] != behavior_record["epochId"]
            or relaxation_record["simTimeMs"] != behavior_record["simTimeMs"]
        ):
            raise RuntimeError(f"{key}: behavioral/relaxation binding differs")
        immediate = behavior_record["immediateRequestComparison"]
        relation = immediate["acceptanceRelation"]
        delta = immediate["acceptedCountDeltaC1MinusB1"]
        expected_relation = (
            "c1LowerImmediateAcceptance"
            if delta < 0
            else "c1HigherImmediateAcceptance"
            if delta > 0
            else "equalImmediateAcceptance"
        )
        if relation != expected_relation or (
            delta != 0
            and behavior_record["primaryDifferenceClass"]
            != "requestDispositionDifference"
        ):
            raise RuntimeError(f"{key}: immediate acceptance relation contradicts behavior")
        candidates = [
            _classify_link(link) for link in relaxation_record["candidateLinks"]
        ]
        if len(candidates) != len({value["candidateId"] for value in candidates}):
            raise RuntimeError(f"{key}: duplicate candidate classification")
        class_set = {
            evidence_class
            for candidate in candidates
            for evidence_class in candidate["evidenceClasses"]
        }
        classes = [name for name in _CLASSES if name in class_set]
        records.append(
            {
                "schemaVersion": "1.0.0",
                "recordType": "ridebound-wp13-mechanism-classification-v1",
                "panelId": key[0],
                "unitId": key[1],
                "epochId": behavior_record["epochId"],
                "simTimeMs": behavior_record["simTimeMs"],
                "sourceBehavioralComparisonSha256": expected_behavior_hash,
                "sourceRelaxationRecordSha256": _value_hash(
                    "RecordedWitnessRelaxationRecord", relaxation_record
                ),
                "immediateAcceptanceRelation": relation,
                "acceptedCountDeltaC1MinusB1": delta,
                "primaryBehaviorDifferenceClass": behavior_record[
                    "primaryDifferenceClass"
                ],
                "evidenceClasses": classes,
                "candidateClassifications": candidates,
                "causalAttribution": "notEstablished",
                "downstreamTrajectory": "notEvaluated",
            }
        )
    all_candidates = [
        candidate
        for record in records
        for candidate in record["candidateClassifications"]
    ]
    counts, cross_tab = _summarize(records)
    panel_summaries = []
    for panel_id in ("A", "B"):
        selected = [record for record in records if record["panelId"] == panel_id]
        panel_counts, panel_cross_tab = _summarize(selected)
        panel_summaries.append(
            {
                "panelId": panel_id,
                "recordCount": len(selected),
                "candidateLinkCount": sum(
                    len(record["candidateClassifications"]) for record in selected
                ),
                "evidenceClassOccurrenceCounts": panel_counts,
                "acceptanceRelationCrossTab": panel_cross_tab,
            }
        )
    result = {
        "schemaVersion": "1.0.0",
        "reportType": "ridebound-wp13-mechanism-classification-set-v1",
        "toolIdentity": {
            "classifierSourceSha256": _sha256(pathlib.Path(__file__).resolve()),
            "schemaSha256": _SCHEMA_SHA256,
        },
        "inputIdentity": {
            "behavioralReportLengthBytes": _BEHAVIOR_LENGTH,
            "behavioralReportSha256": _BEHAVIOR_SHA256,
            "behavioralComparatorSourceSha256": _BEHAVIOR_SOURCE_SHA256,
            "relaxationReportLengthBytes": _RELAXATION_LENGTH,
            "relaxationReportSha256": _RELAXATION_SHA256,
            "relaxationCalculatorSourceSha256": _RELAXATION_SOURCE_SHA256,
            "relaxationSchemaSha256": _RELAXATION_SCHEMA_SHA256,
        },
        "claimBoundary": {
            "analysisClass": "postOutcomeExploratory",
            "classificationScope": "firstDivergenceEvidenceCooccurrence",
            "causalAttribution": "notEstablished",
            "downstreamTrajectory": "notEvaluated",
            "rankingVsSearchOmission": "notDistinguishableWhenPortfolioMissing",
            "candidateFeasibility": "notEvaluated",
            "interpretation": "descriptiveNotCausal",
            "h6Artifacts": "readOnlyImmutableInputs",
            "confirmatoryGate": None,
        },
        "recordCount": len(records),
        "candidateLinkCount": len(all_candidates),
        "evidenceClassOccurrenceCounts": counts,
        "acceptanceRelationCrossTab": cross_tab,
        "panelSummaries": panel_summaries,
        "records": records,
    }
    jsonschema.Draft202012Validator(schema).validate(result)
    return result


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--behavioral-report", required=True, type=pathlib.Path)
    parser.add_argument("--relaxation-report", required=True, type=pathlib.Path)
    parser.add_argument("--output", type=pathlib.Path)
    arguments = parser.parse_args(argv)
    try:
        if arguments.output is not None and arguments.output.resolve() in {
            arguments.behavioral_report.resolve(),
            arguments.relaxation_report.resolve(),
        }:
            raise RuntimeError("output must not overwrite an input report")
        behavior, relaxation = _read_inputs(
            arguments.behavioral_report,
            arguments.relaxation_report,
        )
        schema = _load_schema(_SCHEMA, _SCHEMA_SHA256, "classification")
        encoded = _canonical(build(behavior, relaxation, schema)) + b"\n"
        if arguments.output is None:
            sys.stdout.buffer.write(encoded)
        else:
            arguments.output.parent.mkdir(parents=True, exist_ok=True)
            arguments.output.write_bytes(encoded)
    except (OSError, RuntimeError, ValueError, TypeError, jsonschema.ValidationError) as error:
        print(f"wp13_mechanism_classify: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
