"""Aggregate verified E1 candidate evidence at exact H6 first divergences."""

from __future__ import annotations

import argparse
import base64
from collections import Counter, defaultdict
import hashlib
import importlib.util
import json
import pathlib

import jsonschema


SCHEMA_ID = (
    "https://ridebound.local/schemas/wp13/v1/"
    "e1-candidate-descriptive-aggregation.schema.json"
)
_RECORD_SET_SHA256 = (
    "bef27519b5dae4482029be83cd8d1c2b1e0ef2afa72ea63e6645f4991e425618"
)
_COMPARATOR_SHA256 = (
    "3717f093c62c37a339da0b826323fb1604a684bd9990630d9d9dc5563fd4f7e3"
)
_INVENTORY_SHA256 = (
    "a029b9786aa8faa8663957d59163fa6a269b2515f771678306c8f0df5c054674"
)
_FALSIFICATION_SHA256 = (
    "78bf631392fb9551103f8e1ce4dd2e101ef5deed32d4dd9d95297a28e8377785"
)
_EQUIVALENCE_SHA256 = (
    "4abb24f0d789f6baccf8fbf163bfbbe19738f712b6f3bb25cdd949c2260babfc"
)
_CLASSIFICATIONS = (
    "prunedWithRecordedWitness",
    "eligibleNotSelectedAssociation",
    "selectedByC1",
    "absentFromGeneratedSet",
)
_IMMEDIATE_RELATIONS = (
    "c1LowerImmediateAcceptance",
    "equalImmediateAcceptance",
    "c1HigherImmediateAcceptance",
)
_EXPECTED_PANEL = {
    "A": {"vehicleCount": 8, "b1Completed": 1735, "c1Completed": 1581},
    "B": {"vehicleCount": 4, "b1Completed": 966, "c1Completed": 860},
}


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


def _canonical_bytes(value):
    return json.dumps(
        value,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
        allow_nan=False,
    ).encode("utf-8")


def _encoded(value):
    return _canonical_bytes(value).decode("utf-8") + "\n"


def _load_canonical(path):
    raw = path.read_bytes()
    value = json.loads(raw)
    canonical = _canonical_bytes(value)
    if raw not in {canonical, canonical + b"\n"}:
        raise RuntimeError(f"non-canonical JSON input: {path}")
    return value


def _value_hash(domain, value):
    return hashlib.sha256(
        domain.encode("utf-8") + b"\0" + _canonical_bytes(value)
    ).hexdigest()


def _signature_projection(candidate):
    excluded = {"candidateId", "policyEligibility", "objectiveContributions"}
    return {key: value for key, value in candidate.items() if key not in excluded}


def _candidate_signature(candidate):
    return _value_hash(
        "RideBound.Wp13.CandidateSignature.v1",
        _signature_projection(candidate),
    )


def _signature_index(candidates, arm):
    result = {}
    for candidate in candidates:
        signature = _candidate_signature(candidate)
        if signature in result:
            raise RuntimeError(f"{arm} candidate signature collision")
        result[signature] = candidate
    return result


def _signature_set_hash(signatures):
    return _value_hash("RideBound.Wp13.CandidateSignatureSet.v1", sorted(signatures))


def _objective_vector_hash(candidate):
    return _value_hash(
        "RideBound.Wp13.CandidateObjectiveVector.v1",
        candidate["objectiveContributions"],
    )


def _objective_key(candidate, objective_levels):
    contributions = candidate["objectiveContributions"]
    if len(contributions) != len(objective_levels):
        raise RuntimeError("candidate objective vector length differs")
    return tuple(
        -value if objective["sense"] == "maximize" else value
        for value, objective in zip(contributions, objective_levels)
    ) + (candidate["candidateId"],)


def _within_vehicle_ordinal(portfolio, candidate):
    eligible = [
        value
        for value in portfolio["candidates"]
        if value["vehicleId"] == candidate["vehicleId"]
        and value["policyEligibility"] == "eligible"
    ]
    ordered = sorted(
        eligible,
        key=lambda value: _objective_key(
            value,
            portfolio["selectionProblem"]["objectiveLevels"],
        ),
    )
    identifiers = [value["candidateId"] for value in ordered]
    if len(identifiers) != len(set(identifiers)) or candidate["candidateId"] not in (
        identifiers
    ):
        raise RuntimeError("within-vehicle objective ordinal is ambiguous")
    return identifiers.index(candidate["candidateId"]) + 1, len(identifiers)


def _pruned_witness_index(evidence):
    result = {}
    for witness in evidence["prunedCandidates"]:
        candidate_id = witness["candidateId"]
        if candidate_id in result:
            raise RuntimeError("duplicate C1 pruned candidate witness")
        result[candidate_id] = witness
    return result


def _prune_evidence(candidate, witness):
    if (
        witness["vehicleId"] != candidate["vehicleId"]
        or witness["newRequestIds"] != candidate["newRequestIds"]
    ):
        raise RuntimeError("C1 prune witness identity differs from portfolio")
    result = {
        "pruneCode": witness["code"],
        "commitmentWitnessCount": len(witness["commitmentWitnesses"]),
        "commitmentWitnessCodes": sorted(
            {value["code"] for value in witness["commitmentWitnesses"]}
        ),
        "hasPhysicalWitness": "physicalWitness" in witness,
        "witnessSha256": _value_hash(
            "RideBound.Wp13.CandidatePruneWitness.v1",
            witness,
        ),
    }
    if "physicalWitness" in witness:
        result["physicalWitnessCode"] = witness["physicalWitness"]["code"]
    return result


def _classify_link(b1_candidate, c1_index, c1_selected_ids, c1_evidence):
    signature = _candidate_signature(b1_candidate)
    c1_candidate = c1_index.get(signature)
    if c1_candidate is None:
        return "absentFromGeneratedSet", None, None
    if c1_candidate["policyEligibility"] == "pruned":
        witness = _pruned_witness_index(c1_evidence).get(
            c1_candidate["candidateId"]
        )
        if witness is None:
            raise RuntimeError("C1 pruned portfolio candidate lacks recorded witness")
        return (
            "prunedWithRecordedWitness",
            c1_candidate,
            _prune_evidence(c1_candidate, witness),
        )
    if c1_candidate["candidateId"] in c1_selected_ids:
        return "selectedByC1", c1_candidate, None
    return "eligibleNotSelectedAssociation", c1_candidate, None


def _target_decision(path, target_epoch):
    pending_batch = None
    with path.open("r", encoding="utf-8", newline="") as source:
        for raw_line in source:
            record = json.loads(raw_line)
            frame = json.loads(base64.b64decode(record["frameBase64"]))
            if record["direction"] == "adapterToRunner" and frame.get(
                "messageType"
            ) == "eventBatch":
                pending_batch = frame
                continue
            if record["direction"] != "runnerToAdapter" or frame.get(
                "messageType"
            ) != "decision":
                continue
            if pending_batch is None or pending_batch["epochId"] != frame["epochId"]:
                raise RuntimeError("target transcript decision is not paired")
            if frame["epochId"] == target_epoch:
                return pending_batch, frame
            pending_batch = None
    raise RuntimeError(f"target epoch {target_epoch} is absent")


def _observed_projection(batch):
    return {
        "epochId": batch["epochId"],
        "simTimeMs": batch["simTimeMs"],
        "events": [
            {"eventType": event["eventType"], "payload": event["payload"]}
            for event in batch["payload"]["events"]
        ],
    }


def _direct_child(root, name, label):
    path = (root / name).resolve()
    if path.parent != root or not path.is_dir():
        raise RuntimeError(f"{label} bundle is missing or escapes root: {name}")
    return path


def _verify_e1_runs(inventory, roots, verifier):
    verified = {}
    for record in inventory["runs"]:
        key = (record["panelId"], record["jobId"])
        if key in verified:
            raise RuntimeError("duplicate E1 inventory job")
        bundle = _direct_child(roots[record["panelId"]], record["jobId"], "E1")
        if _sha256(bundle / "bundle-manifest.json") != record[
            "bundleManifestSha256"
        ]:
            raise RuntimeError(f"E1 manifest differs: {record['jobId']}")
        summary = _load_canonical(bundle / "summary.json")
        if (
            summary["label"] != record["jobId"]
            or summary["sourceScenarioContentSha256"]
            != record["sourceScenarioContentSha256"]
        ):
            raise RuntimeError(f"E1 job identity differs: {record['jobId']}")
        result = verifier.verify_bundle(
            bundle,
            include_behavioral_hash=True,
            require_audited_solver_evidence=True,
            require_retained_candidate_portfolio=True,
        )
        if (
            result["repeatCount"] != 1
            or result["semanticHash"] != record["semanticHash"]
            or result["behavioralProjectionHash"]
            != record["behavioralProjectionHash"]
            or result["runs"][0]["requestCount"] != record["requestCount"]
            or result["runs"][0]["epochCount"] != record["solverDecisionCount"]
            or result["runs"][0]["retainedPortfolioEvidenceCount"]
            != record["retainedPortfolioEvidenceCount"]
        ):
            raise RuntimeError(f"E1 independent verification differs: {record['jobId']}")
        verified[key] = bundle
    if len(verified) != 80:
        raise RuntimeError("E1 verification must cover exact 80 jobs")
    return verified


def _h6_completed(bundle, equivalence_record, source_hash):
    if _sha256(bundle / "bundle-manifest.json") != equivalence_record[
        "h6BundleManifestSha256"
    ]:
        raise RuntimeError("H6 bundle manifest differs from equivalence receipt")
    report = _load_canonical(bundle / "run-00.json")
    semantic = report["semantic"]
    accepted = semantic["acceptedRequestIds"]
    rejected = semantic["rejectedRequestIds"]
    if (
        report["semanticHash"] != equivalence_record["h6SemanticHash"]
        or semantic["sourceScenarioContentSha256"] != source_hash
        or semantic["requestCount"] != 108
        or len(accepted) != len(set(accepted))
        or len(rejected) != len(set(rejected))
        or set(accepted) & set(rejected)
        or len(accepted) + len(rejected) != 108
    ):
        raise RuntimeError("H6 terminal outcome identity/conservation differs")
    return len(accepted)


def _validate_inputs(
    inventory,
    record_set,
    comparator,
    falsification,
    equivalence,
):
    if (
        inventory["totals"]["completedVerifiedArmRunCount"] != 80
        or inventory["totals"]["solverDecisionCount"] != 44156
        or inventory["totals"]["retainedPortfolioEvidenceCount"] != 44156
        or falsification["verification"]
        != {
            "verifiedArmRunCount": 80,
            "verifiedSolverDecisionCount": 44156,
            "verifiedPortfolioCount": 44156,
            "mutationCount": 31,
            "expectedRejectionCount": 31,
            "unexpectedPassCount": 0,
            "unexpectedFailureCount": 0,
        }
        or equivalence["totals"]["armRunCount"] != 80
        or equivalence["totals"]["behaviorallyEqualArmRunCount"] != 80
        or equivalence["totals"]["mismatchCount"] != 0
        or record_set["recordCount"] != 40
        or len(record_set["records"]) != 40
        or len(comparator["records"]) != 40
        or falsification["inputEvidence"]["inventorySha256"]
        != _INVENTORY_SHA256
        or equivalence["inputEvidence"]["e1InventorySha256"]
        != _INVENTORY_SHA256
        or falsification["claimBoundary"]["mechanismConclusion"]
        != "notEvaluated"
        or equivalence["claimBoundary"]["mechanismConclusion"]
        != "notEvaluated"
    ):
        raise RuntimeError("WP13 input denominator or closure gate differs")
    run_by_job = {value["jobId"]: value for value in inventory["runs"]}
    equivalence_by_job = {
        value["jobId"]: value for value in equivalence["records"]
    }
    source_by_key = {
        (value["panelId"], value["unitId"]): value
        for value in record_set["records"]
    }
    comparator_by_key = {
        (value["panelId"], value["unitId"]): value
        for value in comparator["records"]
    }
    if (
        len(run_by_job) != 80
        or len(equivalence_by_job) != 80
        or len(source_by_key) != 40
        or set(source_by_key) != set(comparator_by_key)
    ):
        raise RuntimeError("WP13 input identities are duplicate or incomplete")
    for job_id, run in run_by_job.items():
        equivalent = equivalence_by_job.get(job_id)
        if (
            equivalent is None
            or equivalent["operationallyEqual"] is not True
            or equivalent["e1BundleManifestSha256"]
            != run["bundleManifestSha256"]
            or equivalent["e1SemanticHash"] != run["semanticHash"]
            or equivalent["e1BehavioralProjectionHash"]
            != run["behavioralProjectionHash"]
        ):
            raise RuntimeError(f"E1/H6 receipt binding differs: {job_id}")
    return run_by_job, equivalence_by_job, source_by_key, comparator_by_key


def _link_record(
    b1_candidate,
    b1_portfolio,
    c1_index,
    c1_portfolio,
    c1_evidence,
):
    c1_selected = set(c1_portfolio["selectedCandidateIds"])
    classification, c1_candidate, prune_evidence = _classify_link(
        b1_candidate,
        c1_index,
        c1_selected,
        c1_evidence,
    )
    b1_ordinal, b1_count = _within_vehicle_ordinal(
        b1_portfolio,
        b1_candidate,
    )
    result = {
        "candidateSignatureSha256": _candidate_signature(b1_candidate),
        "vehicleId": b1_candidate["vehicleId"],
        "newRequestIds": b1_candidate["newRequestIds"],
        "b1CandidateId": b1_candidate["candidateId"],
        "candidateIdRelation": (
            "notPresent"
            if c1_candidate is None
            else "same"
            if c1_candidate["candidateId"] == b1_candidate["candidateId"]
            else "different"
        ),
        "classification": classification,
        "b1ObjectiveVectorSha256": _objective_vector_hash(b1_candidate),
        "b1WithinVehicleObjectiveOrdinal": b1_ordinal,
        "b1EligibleCandidateCountForVehicle": b1_count,
    }
    if c1_candidate is not None:
        result["c1CandidateId"] = c1_candidate["candidateId"]
    if c1_candidate is not None and c1_candidate["policyEligibility"] == "eligible":
        c1_ordinal, c1_count = _within_vehicle_ordinal(
            c1_portfolio,
            c1_candidate,
        )
        result.update(
            {
                "c1ObjectiveVectorSha256": _objective_vector_hash(c1_candidate),
                "c1WithinVehicleObjectiveOrdinal": c1_ordinal,
                "c1EligibleCandidateCountForVehicle": c1_count,
            }
        )
    if prune_evidence is not None:
        result["pruneEvidence"] = prune_evidence
    return result


def _count_classes(links):
    values = Counter(link["classification"] for link in links)
    return {name: values[name] for name in _CLASSIFICATIONS}


def _count_immediate(records):
    values = Counter(record["immediate"]["acceptanceRelation"] for record in records)
    return {name: values[name] for name in _IMMEDIATE_RELATIONS}


def _aggregate(records, verified_solver_decisions):
    keys = {(value["panelId"], value["unitId"]) for value in records}
    if len(records) != 40 or len(keys) != 40 or verified_solver_decisions != 44156:
        raise RuntimeError("candidate aggregation must cover exact frozen denominator")
    panels = []
    for panel_id in ("A", "B"):
        selected = [value for value in records if value["panelId"] == panel_id]
        links = [link for value in selected for link in value["links"]]
        expected = _EXPECTED_PANEL[panel_id]
        b1_completed = sum(
            value["trajectory"]["h6B1Completed"] for value in selected
        )
        c1_completed = sum(
            value["trajectory"]["h6C1Completed"] for value in selected
        )
        if (
            len(selected) != 20
            or b1_completed != expected["b1Completed"]
            or c1_completed != expected["c1Completed"]
        ):
            raise RuntimeError(f"Panel {panel_id} denominator/outcome differs from H6")
        panels.append(
            {
                "panelId": panel_id,
                "vehicleCount": expected["vehicleCount"],
                "recordCount": 20,
                "arrivalsPerArm": 2160,
                "actionfulLinkCount": len(links),
                "classificationCounts": _count_classes(links),
                "immediateRelationCounts": _count_immediate(selected),
                "generatedSignatureEqualPairCount": sum(
                    value["generatedOverlap"]["relation"] == "equal"
                    for value in selected
                ),
                "candidateIdDriftCount": sum(
                    value["generatedOverlap"]["candidateIdDriftCount"]
                    for value in selected
                ),
                "h6B1Completed": b1_completed,
                "h6C1Completed": c1_completed,
                "h6CompletedDeltaC1MinusB1": c1_completed - b1_completed,
            }
        )
    all_links = [link for value in records for link in value["links"]]
    rows = []
    for classification in _CLASSIFICATIONS:
        for relation in _IMMEDIATE_RELATIONS:
            selected = [
                value
                for value in records
                if value["immediate"]["acceptanceRelation"] == relation
                and any(
                    link["classification"] == classification
                    for link in value["links"]
                )
            ]
            link_count = sum(
                link["classification"] == classification
                for value in selected
                for link in value["links"]
            )
            if selected:
                rows.append(
                    {
                        "classification": classification,
                        "acceptanceRelation": relation,
                        "cellCount": len(selected),
                        "linkCount": link_count,
                        "trajectoryCompletedDeltaSum": sum(
                            value["trajectory"]["h6CompletedDeltaC1MinusB1"]
                            for value in selected
                        ),
                    }
                )
    totals = {
        "recordCount": 40,
        "verifiedArmRunCount": 80,
        "verifiedSolverDecisionCount": verified_solver_decisions,
        "targetPortfolioCount": 80,
        "generatedCandidateObservationCount": sum(
            value["generatedOverlap"]["b1GeneratedCount"]
            + value["generatedOverlap"]["c1GeneratedCount"]
            for value in records
        ),
        "generatedSignatureIntersectionCount": sum(
            value["generatedOverlap"]["intersectionCount"] for value in records
        ),
        "generatedSignatureEqualPairCount": sum(
            value["generatedOverlap"]["relation"] == "equal" for value in records
        ),
        "generatedSignatureDifferentPairCount": sum(
            value["generatedOverlap"]["relation"] == "different"
            for value in records
        ),
        "candidateIdDriftCount": sum(
            value["generatedOverlap"]["candidateIdDriftCount"]
            for value in records
        ),
        "actionfulLinkCount": len(all_links),
        "classificationCounts": _count_classes(all_links),
        "immediateRelationCounts": _count_immediate(records),
    }
    if totals["actionfulLinkCount"] != 41:
        raise RuntimeError("actionful B1 link denominator differs from frozen evidence")
    return totals, panels, rows


def build_report(repository, inputs, e1_roots, h6_roots):
    verifier_path = (
        repository / "simulators/fleetpy-ridebound/actual_fleetpy_medium_verify.py"
    )
    verifier = _load_module(
        verifier_path,
        "ridebound_medium_verifier_for_e1_candidate_aggregation",
    )
    for name, expected_hash in (
        ("inventory", _INVENTORY_SHA256),
        ("recordSet", _RECORD_SET_SHA256),
        ("comparator", _COMPARATOR_SHA256),
        ("falsification", _FALSIFICATION_SHA256),
        ("equivalence", _EQUIVALENCE_SHA256),
    ):
        if _sha256(inputs[name]) != expected_hash:
            raise RuntimeError(f"{name} input hash differs from frozen identity")
    inventory = _load_canonical(inputs["inventory"])
    record_set = _load_canonical(inputs["recordSet"])
    comparator = _load_canonical(inputs["comparator"])
    falsification = _load_canonical(inputs["falsification"])
    equivalence = _load_canonical(inputs["equivalence"])
    verifier_hash = _sha256(verifier_path)
    if (
        falsification["sourceIdentity"]["independentVerifierSourceSha256"]
        != verifier_hash
        or equivalence["sourceIdentity"]["independentVerifierSourceSha256"]
        != verifier_hash
    ):
        raise RuntimeError("closure receipts bind a different independent verifier")
    (
        run_by_job,
        equivalence_by_job,
        source_by_key,
        comparator_by_key,
    ) = _validate_inputs(
        inventory,
        record_set,
        comparator,
        falsification,
        equivalence,
    )
    verified_e1 = _verify_e1_runs(inventory, e1_roots, verifier)
    records = []
    for key in sorted(source_by_key):
        source = source_by_key[key]
        comparison = comparator_by_key[key]
        panel_id = source["panelId"]
        epoch = source["firstDivergence"]["b1Evidence"]["epochId"]
        if (
            source["firstDivergence"]["classification"]
            != "operationalDecisionDivergenceOnEqualObservedInput"
            or source["firstDivergence"]["c1Evidence"]["epochId"] != epoch
            or comparison["epochId"] != epoch
            or source["firstDivergence"]["b1Evidence"][
                "observedInputProjectionSha256"
            ]
            != source["firstDivergence"]["c1Evidence"][
                "observedInputProjectionSha256"
            ]
            or comparison["observedInputProjectionSha256"]
            != source["firstDivergence"]["b1Evidence"][
                "observedInputProjectionSha256"
            ]
        ):
            raise RuntimeError("source first-divergence target differs")
        arm_values = {}
        source_binding = {
            "firstDivergenceRecordSha256": _value_hash(
                "RideBound.Wp13.FirstDivergenceRecord.v1",
                source,
            ),
            "behavioralComparisonRecordSha256": _value_hash(
                "RideBound.Wp13.BehavioralComparisonRecord.v1",
                comparison,
            ),
            "observedInputProjectionSha256": source["firstDivergence"][
                "b1Evidence"
            ]["observedInputProjectionSha256"],
        }
        for arm in ("b1", "c1"):
            job_id = source[f"{arm}Label"]
            run = run_by_job[job_id]
            bundle = verified_e1[(panel_id, job_id)]
            batch, decision = _target_decision(
                bundle / "transcript-00.ndjson",
                epoch,
            )
            if (
                batch["simTimeMs"] != comparison["simTimeMs"]
                or _value_hash(
                    "RideBound.Wp13.ObservedProjection.v1",
                    _observed_projection(batch),
                )
                != source_binding["observedInputProjectionSha256"]
            ):
                raise RuntimeError(f"E1 target observed input differs: {job_id}")
            solver = decision["payload"]["solver"]
            verifier._verify_audited_solver_evidence(solver, epoch)
            portfolio = solver["executionEvidence"]["candidatePortfolio"]
            expected_profile = "rollingCost" if arm == "b1" else "hardVector"
            if portfolio["objectiveProfile"] != expected_profile:
                raise RuntimeError(f"E1 target objective profile differs: {job_id}")
            arm_values[arm] = {
                "run": run,
                "portfolio": portfolio,
                "evidence": solver["executionEvidence"],
                "index": _signature_index(portfolio["candidates"], arm),
            }
            source_binding[f"{arm}BundleManifestSha256"] = run[
                "bundleManifestSha256"
            ]
            source_binding[f"{arm}PortfolioSha256"] = _value_hash(
                "RideBound.Wp13.CandidatePortfolio.v1",
                portfolio,
            )
        b1 = arm_values["b1"]
        c1 = arm_values["c1"]
        b1_signatures = set(b1["index"])
        c1_signatures = set(c1["index"])
        intersection = b1_signatures & c1_signatures
        candidate_id_drift = sum(
            b1["index"][signature]["candidateId"]
            != c1["index"][signature]["candidateId"]
            for signature in intersection
        )
        b1_selected = set(b1["portfolio"]["selectedCandidateIds"])
        actionful = sorted(
            (
                candidate
                for candidate in b1["portfolio"]["candidates"]
                if candidate["candidateId"] in b1_selected
                and candidate["newRequestIds"]
            ),
            key=lambda value: value["candidateId"],
        )
        if not actionful:
            raise RuntimeError("B1 first divergence has no actionful selected candidate")
        links = [
            _link_record(
                candidate,
                b1["portfolio"],
                c1["index"],
                c1["portfolio"],
                c1["evidence"],
            )
            for candidate in actionful
        ]
        completed = {}
        for arm in ("b1", "c1"):
            job_id = source[f"{arm}Label"]
            h6_bundle = _direct_child(h6_roots[panel_id], job_id, "H6")
            completed[arm] = _h6_completed(
                h6_bundle,
                equivalence_by_job[job_id],
                source["sourceScenarioContentSha256"],
            )
        immediate = comparison["immediateRequestComparison"]
        record = {
            "panelId": panel_id,
            "vehicleCount": _EXPECTED_PANEL[panel_id]["vehicleCount"],
            "unitId": source["unitId"],
            "epochId": epoch,
            "simTimeMs": comparison["simTimeMs"],
            "b1JobId": source["b1Label"],
            "c1JobId": source["c1Label"],
            "sourceScenarioContentSha256": source[
                "sourceScenarioContentSha256"
            ],
            "sourceBinding": source_binding,
            "objectiveProfiles": {
                "b1": "rollingCost",
                "c1": "hardVector",
                "crossArmComparison": "notComparableAcrossObjectiveProfiles",
                "ordinalInterpretation": (
                    "withinVehicleDescriptiveNotGlobalSolverRank"
                ),
            },
            "generatedOverlap": {
                "b1GeneratedCount": len(b1_signatures),
                "c1GeneratedCount": len(c1_signatures),
                "intersectionCount": len(intersection),
                "b1OnlyCount": len(b1_signatures - c1_signatures),
                "c1OnlyCount": len(c1_signatures - b1_signatures),
                "candidateIdDriftCount": candidate_id_drift,
                "b1SignatureSetSha256": _signature_set_hash(b1_signatures),
                "c1SignatureSetSha256": _signature_set_hash(c1_signatures),
                "relation": (
                    "equal" if b1_signatures == c1_signatures else "different"
                ),
            },
            "immediate": {
                "acceptanceRelation": immediate["acceptanceRelation"],
                "acceptedCountDeltaC1MinusB1": immediate[
                    "acceptedCountDeltaC1MinusB1"
                ],
            },
            "trajectory": {
                "arrivalsPerArm": 108,
                "h6B1Completed": completed["b1"],
                "h6C1Completed": completed["c1"],
                "h6CompletedDeltaC1MinusB1": completed["c1"] - completed["b1"],
                "interpretation": "trajectoryAssociatedNotCausal",
            },
            "linkClassCounts": _count_classes(links),
            "links": links,
        }
        records.append(record)
    totals, panels, association_rows = _aggregate(
        records,
        inventory["totals"]["solverDecisionCount"],
    )
    schema_path = (
        repository
        / "benchmarks/schemas/wp13/v1/"
        "e1-candidate-descriptive-aggregation.schema.json"
    )
    result = {
        "schemaVersion": "1.0.0",
        "schemaId": SCHEMA_ID,
        "reportType": "ridebound-wp13-e1-candidate-descriptive-aggregation-v1",
        "claimBoundary": {
            "analysisClass": "postOutcomeExploratoryFinitePanel",
            "interpretation": "descriptiveAssociationNotCausal",
            "trajectoryOutcome": "trajectoryAssociatedNotCausal",
            "objectiveComparison": "notComparableAcrossObjectiveProfiles",
            "associationRows": "overlappingCellsNotAdditive",
            "populationInference": "notEvaluatedNoCiOrPValue",
            "counterfactualCompletion": "notEvaluated",
            "confirmatoryGate": "notApplicableCannotRescueH6",
            "rawArtifacts": "readOnlyImmutableInputs",
        },
        "sourceIdentity": {
            "analyzerSourceSha256": _sha256(pathlib.Path(__file__).resolve()),
            "independentVerifierSourceSha256": _sha256(verifier_path),
            "schemaSha256": _sha256(schema_path),
        },
        "inputIdentity": {
            "firstDivergenceRecordSetSha256": _RECORD_SET_SHA256,
            "behavioralComparatorSha256": _COMPARATOR_SHA256,
            "e1InventorySha256": _INVENTORY_SHA256,
            "e1FalsificationReceiptSha256": _FALSIFICATION_SHA256,
            "e1H6EquivalenceReceiptSha256": _EQUIVALENCE_SHA256,
            "repositoryInventorySha256": inventory["totals"][
                "repositoryInventorySha256"
            ],
        },
        "totals": totals,
        "associationRows": association_rows,
        "panels": panels,
        "records": records,
    }
    schema = json.loads(schema_path.read_text(encoding="utf-8"))
    jsonschema.Draft202012Validator(schema).validate(result)
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--repository", required=True, type=pathlib.Path)
    parser.add_argument("--inventory", required=True, type=pathlib.Path)
    parser.add_argument("--record-set", required=True, type=pathlib.Path)
    parser.add_argument("--comparator", required=True, type=pathlib.Path)
    parser.add_argument("--falsification", required=True, type=pathlib.Path)
    parser.add_argument("--equivalence", required=True, type=pathlib.Path)
    parser.add_argument("--e1-panel-a-root", required=True, type=pathlib.Path)
    parser.add_argument("--e1-panel-b-root", required=True, type=pathlib.Path)
    parser.add_argument("--h6-panel-a-root", required=True, type=pathlib.Path)
    parser.add_argument("--h6-panel-b-root", required=True, type=pathlib.Path)
    parser.add_argument("--output", type=pathlib.Path)
    arguments = parser.parse_args()
    repository = arguments.repository.resolve()
    result = build_report(
        repository,
        {
            "inventory": arguments.inventory.resolve(),
            "recordSet": arguments.record_set.resolve(),
            "comparator": arguments.comparator.resolve(),
            "falsification": arguments.falsification.resolve(),
            "equivalence": arguments.equivalence.resolve(),
        },
        {
            "A": arguments.e1_panel_a_root.resolve(),
            "B": arguments.e1_panel_b_root.resolve(),
        },
        {
            "A": arguments.h6_panel_a_root.resolve(),
            "B": arguments.h6_panel_b_root.resolve(),
        },
    )
    encoded = _encoded(result)
    if arguments.output:
        output = arguments.output.resolve()
        input_paths = {
            arguments.inventory.resolve(),
            arguments.record_set.resolve(),
            arguments.comparator.resolve(),
            arguments.falsification.resolve(),
            arguments.equivalence.resolve(),
        }
        raw_roots = {
            arguments.e1_panel_a_root.resolve(),
            arguments.e1_panel_b_root.resolve(),
            arguments.h6_panel_a_root.resolve(),
            arguments.h6_panel_b_root.resolve(),
        }
        if output in input_paths or any(
            output == root or root in output.parents for root in raw_roots
        ):
            raise RuntimeError("output must not overwrite an input")
        output.parent.mkdir(parents=True, exist_ok=True)
        with output.open("x", encoding="utf-8", newline="\n") as target:
            target.write(encoded)
    else:
        print(encoded, end="")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
