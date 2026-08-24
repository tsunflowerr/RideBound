"""Falsify the WP13 E1 retained-portfolio evidence contract in memory."""

from __future__ import annotations

import argparse
import base64
import copy
import hashlib
import importlib.util
import json
import pathlib

import jsonschema


SCHEMA_ID = (
    "https://ridebound.local/schemas/wp13/v1/"
    "e1-falsification-receipt.schema.json"
)


class BindingRejection(RuntimeError):
    pass


def _load(path):
    return json.loads(path.read_text(encoding="utf-8"))


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
    ).encode("utf-8")


def _encoded(value):
    return _canonical_bytes(value).decode("utf-8") + "\n"


def _verify_binding(value):
    required = {
        "jobId",
        "expectedJobId",
        "summaryJobId",
        "expectedSummaryJobId",
        "manifestSha256",
        "expectedManifestSha256",
        "bundleFilePaths",
        "expectedBundleFilePaths",
        "repositoryInventorySha256",
        "expectedRepositoryInventorySha256",
        "transcriptLengthBytes",
        "expectedTranscriptLengthBytes",
        "transcriptSha256",
        "expectedTranscriptSha256",
        "firstFrameSha256",
        "expectedFirstFrameSha256",
    }
    if not isinstance(value, dict) or set(value) != required:
        raise BindingRejection("RBWP13F_BINDING_SHAPE")
    if value["jobId"] != value["expectedJobId"]:
        raise BindingRejection("RBWP13F_JOB_IDENTITY")
    if value["summaryJobId"] != value["expectedSummaryJobId"]:
        raise BindingRejection("RBWP13F_SUMMARY_IDENTITY")
    if value["manifestSha256"] != value["expectedManifestSha256"]:
        raise BindingRejection("RBWP13F_MANIFEST_HASH")
    if value["bundleFilePaths"] != value["expectedBundleFilePaths"]:
        raise BindingRejection("RBWP13F_BUNDLE_FILE_INVENTORY")
    if value["transcriptLengthBytes"] != value["expectedTranscriptLengthBytes"]:
        raise BindingRejection("RBWP13F_TRANSCRIPT_TRUNCATED")
    if value["transcriptSha256"] != value["expectedTranscriptSha256"]:
        raise BindingRejection("RBWP13F_TRANSCRIPT_HASH")
    if value["firstFrameSha256"] != value["expectedFirstFrameSha256"]:
        raise BindingRejection("RBWP13F_TRANSCRIPT_FRAME_HASH")
    if (
        value["repositoryInventorySha256"]
        != value["expectedRepositoryInventorySha256"]
    ):
        raise BindingRejection("RBWP13F_SOURCE_INVENTORY")


_REJECTION_CODE_FRAGMENTS = (
    ("RBWP13F_JOB_IDENTITY", "RBWP13F_JOB_IDENTITY"),
    ("RBWP13F_SUMMARY_IDENTITY", "RBWP13F_SUMMARY_IDENTITY"),
    ("RBWP13F_MANIFEST_HASH", "RBWP13F_MANIFEST_HASH"),
    ("RBWP13F_BUNDLE_FILE_INVENTORY", "RBWP13F_BUNDLE_FILE_INVENTORY"),
    ("RBWP13F_TRANSCRIPT_TRUNCATED", "RBWP13F_TRANSCRIPT_TRUNCATED"),
    ("RBWP13F_TRANSCRIPT_HASH", "RBWP13F_TRANSCRIPT_HASH"),
    ("RBWP13F_TRANSCRIPT_FRAME_HASH", "RBWP13F_TRANSCRIPT_FRAME_HASH"),
    ("RBWP13F_SOURCE_INVENTORY", "RBWP13F_SOURCE_INVENTORY"),
    ("solver evidence fields differ", "RBWP13F_EVIDENCE_SHAPE"),
    ("candidatePortfolio fields differ", "RBWP13F_PORTFOLIO_SHAPE"),
    ("identity/profile differs", "RBWP13F_PORTFOLIO_IDENTITY"),
    ("candidate count differs", "RBWP13F_CANDIDATE_COUNT"),
    ("eligible/no-op counts differ", "RBWP13F_ELIGIBLE_NOOP_COUNT"),
    ("candidate identity differs", "RBWP13F_CANDIDATE_IDENTITY"),
    ("candidate order differs", "RBWP13F_CANDIDATE_ORDER"),
    (
        "eligibility/objective shape differs",
        "RBWP13F_ELIGIBILITY_OBJECTIVE_SHAPE",
    ),
    ("no-op contract differs", "RBWP13F_NOOP_CONTRACT"),
    (
        "selectedCandidateIds must contain unique non-empty strings",
        "RBWP13F_SELECTED_UNIQUE",
    ),
    ("selectedCandidateIds[", "RBWP13F_SELECTED_BINDING"),
    ("objective contract differs", "RBWP13F_OBJECTIVE_CONTRACT"),
    ("objective count differs", "RBWP13F_OBJECTIVE_COUNT"),
    (
        "schedule/remaining-route IDs differ",
        "RBWP13F_ROUTE_SCHEDULE_IDENTITY",
    ),
    ("schedule times differ", "RBWP13F_SCHEDULE_TIME_ORDER"),
    ("route stop contract differs", "RBWP13F_ROUTE_STOP_IDENTITY"),
    ("eligible request is undeclared", "RBWP13F_UNDECLARED_REQUEST"),
)


def _classify_rejection(message):
    for fragment, code in _REJECTION_CODE_FRAGMENTS:
        if fragment in message:
            return code
    raise RuntimeError(f"unclassified falsification rejection: {message!r}")


def _decision_evidence(path):
    with path.open("r", encoding="utf-8", newline="") as source:
        for raw_line in source:
            record = json.loads(raw_line)
            if record.get("direction") != "runnerToAdapter":
                continue
            frame = json.loads(base64.b64decode(record["frameBase64"]))
            if frame.get("messageType") != "decision":
                continue
            payload = frame.get("payload", {})
            solver = payload.get("solver", {})
            evidence = solver.get("executionEvidence")
            if isinstance(evidence, dict) and evidence.get("evidenceVersion") == "1.2.0":
                yield frame["epochId"], solver


def _route_stops(candidate):
    route = candidate["route"]
    return route["frozenPrefix"] + route["mutableSuffix"]


def _find_samples(inventory, roots):
    samples = {}
    for record in inventory["runs"]:
        path = roots[record["panelId"]] / record["jobId"] / "transcript-00.ndjson"
        for epoch, solver in _decision_evidence(path):
            portfolio = solver["executionEvidence"]["candidatePortfolio"]
            candidates = portfolio["candidates"]
            eligible_rich = next(
                (
                    candidate
                    for candidate in candidates
                    if candidate["policyEligibility"] == "eligible"
                    and not candidate["isNoOp"]
                    and len(candidate["schedule"]["stops"]) >= 2
                    and len(_route_stops(candidate)) >= 2
                ),
                None,
            )
            if "richEligible" not in samples and eligible_rich is not None:
                samples["richEligible"] = (record, epoch, copy.deepcopy(solver))
            pruned = [
                candidate
                for candidate in candidates
                if candidate["policyEligibility"] == "pruned"
            ]
            if "withPruned" not in samples and pruned:
                selected_vehicles = {
                    candidate["vehicleId"]
                    for candidate in candidates
                    if candidate["candidateId"] in portfolio["selectedCandidateIds"]
                }
                if any(candidate["vehicleId"] in selected_vehicles for candidate in pruned):
                    samples["withPruned"] = (record, epoch, copy.deepcopy(solver))
            if len(samples) == 2:
                return samples
    raise RuntimeError("E1 mutation samples do not satisfy structural prerequisites")


def _candidate(portfolio, predicate):
    return next(value for value in portfolio["candidates"] if predicate(value))


def _mutations(samples, binding):
    rich = samples["richEligible"][2]
    pruned = samples["withPruned"][2]
    definitions = []

    def add(mutation_id, layer, sample_id, expected, source, mutate):
        definitions.append((mutation_id, layer, sample_id, expected, source, mutate))

    add(
        "M01-binding-job-id",
        "artifactBinding",
        "binding",
        "RBWP13F_JOB_IDENTITY",
        binding,
        lambda value: value.update(jobId="mutated-job"),
    )
    add(
        "M02-binding-manifest-hash",
        "artifactBinding",
        "binding",
        "RBWP13F_MANIFEST_HASH",
        binding,
        lambda value: value.update(manifestSha256="0" * 64),
    )
    add(
        "M02b-binding-summary-identity",
        "artifactBinding",
        "binding",
        "RBWP13F_SUMMARY_IDENTITY",
        binding,
        lambda value: value.update(summaryJobId="mutated-summary"),
    )
    add(
        "M02c-binding-extra-file",
        "artifactBinding",
        "binding",
        "RBWP13F_BUNDLE_FILE_INVENTORY",
        binding,
        lambda value: value["bundleFilePaths"].append("invented-extra.bin"),
    )
    add(
        "M03-binding-source-inventory",
        "artifactBinding",
        "binding",
        "RBWP13F_SOURCE_INVENTORY",
        binding,
        lambda value: value.update(repositoryInventorySha256="0" * 64),
    )
    add(
        "M04-binding-transcript-truncation",
        "artifactBinding",
        "binding",
        "RBWP13F_TRANSCRIPT_TRUNCATED",
        binding,
        lambda value: value.update(
            transcriptLengthBytes=value["transcriptLengthBytes"] - 1
        ),
    )
    add(
        "M04b-binding-transcript-hash",
        "artifactBinding",
        "binding",
        "RBWP13F_TRANSCRIPT_HASH",
        binding,
        lambda value: value.update(transcriptSha256="0" * 64),
    )
    add(
        "M04c-binding-frame-hash",
        "artifactBinding",
        "binding",
        "RBWP13F_TRANSCRIPT_FRAME_HASH",
        binding,
        lambda value: value.update(firstFrameSha256="0" * 64),
    )
    add(
        "M05-evidence-version",
        "evidenceVersion",
        "richEligible",
        "solver evidence fields differ",
        rich,
        lambda value: value["executionEvidence"].update(evidenceVersion="1.1.0"),
    )
    add(
        "M06-portfolio-version",
        "portfolioIdentity",
        "richEligible",
        "identity/profile differs",
        rich,
        lambda value: value["executionEvidence"]["candidatePortfolio"].update(
            portfolioVersion="9.0.0"
        ),
    )
    add(
        "M07-schema-id",
        "portfolioIdentity",
        "richEligible",
        "identity/profile differs",
        rich,
        lambda value: value["executionEvidence"]["candidatePortfolio"].update(
            schemaId="https://invalid"
        ),
    )
    add(
        "M08-objective-profile",
        "portfolioIdentity",
        "richEligible",
        "identity/profile differs",
        rich,
        lambda value: value["executionEvidence"]["candidatePortfolio"].update(
            objectiveProfile="invented"
        ),
    )
    add(
        "M09-generated-count",
        "candidateSet",
        "richEligible",
        "candidate count differs",
        rich,
        lambda value: value["executionEvidence"]["candidatePortfolio"].update(
            generatedCandidateCount=(
                value["executionEvidence"]["candidatePortfolio"][
                    "generatedCandidateCount"
                ]
                + 1
            )
        ),
    )
    add(
        "M10-eligible-count",
        "candidateSet",
        "richEligible",
        "eligible/no-op counts differ",
        rich,
        lambda value: value["executionEvidence"]["candidatePortfolio"].update(
            policyEligibleCandidateCount=(
                value["executionEvidence"]["candidatePortfolio"][
                    "policyEligibleCandidateCount"
                ]
                + 1
            )
        ),
    )

    def duplicate_candidate(value):
        candidates = value["executionEvidence"]["candidatePortfolio"]["candidates"]
        candidates[1]["candidateId"] = candidates[0]["candidateId"]

    add(
        "M11-duplicate-candidate",
        "candidateSet",
        "richEligible",
        "candidate identity differs",
        rich,
        duplicate_candidate,
    )
    add(
        "M12-candidate-order",
        "candidateSet",
        "richEligible",
        "candidate order differs",
        rich,
        lambda value: value["executionEvidence"]["candidatePortfolio"][
            "candidates"
        ].reverse(),
    )

    def remove_eligible_objectives(value):
        portfolio = value["executionEvidence"]["candidatePortfolio"]
        candidate = _candidate(
            portfolio,
            lambda item: item["policyEligibility"] == "eligible",
        )
        del candidate["objectiveContributions"]

    add(
        "M13-eligible-objectives-missing",
        "eligibility",
        "richEligible",
        "eligibility/objective shape differs",
        rich,
        remove_eligible_objectives,
    )

    def add_pruned_objectives(value):
        portfolio = value["executionEvidence"]["candidatePortfolio"]
        candidate = _candidate(
            portfolio,
            lambda item: item["policyEligibility"] == "pruned",
        )
        candidate["objectiveContributions"] = [
            0 for _ in portfolio["selectionProblem"]["objectiveLevels"]
        ]

    add(
        "M14-pruned-objectives-invented",
        "eligibility",
        "withPruned",
        "eligibility/objective shape differs",
        pruned,
        add_pruned_objectives,
    )

    def no_op_request(value):
        portfolio = value["executionEvidence"]["candidatePortfolio"]
        candidate = _candidate(portfolio, lambda item: item["isNoOp"])
        candidate["newRequestIds"] = ["invented-request"]

    add(
        "M15-no-op-request",
        "eligibility",
        "richEligible",
        "no-op contract differs",
        rich,
        no_op_request,
    )
    add(
        "M16-selected-unknown",
        "selection",
        "richEligible",
        "selectedCandidateIds[0] differs",
        rich,
        lambda value: value["executionEvidence"]["candidatePortfolio"][
            "selectedCandidateIds"
        ].__setitem__(0, "unknown-candidate"),
    )

    def selected_duplicate(value):
        selected = value["executionEvidence"]["candidatePortfolio"][
            "selectedCandidateIds"
        ]
        selected[1] = selected[0]

    add(
        "M17-selected-duplicate",
        "selection",
        "richEligible",
        "selectedCandidateIds must contain unique non-empty strings",
        rich,
        selected_duplicate,
    )

    def select_pruned(value):
        portfolio = value["executionEvidence"]["candidatePortfolio"]
        candidate = _candidate(
            portfolio,
            lambda item: item["policyEligibility"] == "pruned",
        )
        vehicle_index = portfolio["selectionProblem"]["vehicleIds"].index(
            candidate["vehicleId"]
        )
        portfolio["selectedCandidateIds"][vehicle_index] = candidate["candidateId"]

    add(
        "M18-selected-pruned",
        "selection",
        "withPruned",
        "selectedCandidateIds[0] differs",
        pruned,
        select_pruned,
    )
    add(
        "M19-objective-level-index",
        "objective",
        "richEligible",
        "objective contract differs",
        rich,
        lambda value: value["executionEvidence"]["candidatePortfolio"][
            "selectionProblem"
        ]["objectiveLevels"][0].update(levelIndex=1),
    )

    def contribution_count(value):
        portfolio = value["executionEvidence"]["candidatePortfolio"]
        candidate = _candidate(
            portfolio,
            lambda item: item["policyEligibility"] == "eligible",
        )
        candidate["objectiveContributions"].pop()

    add(
        "M20-objective-contribution-count",
        "objective",
        "richEligible",
        "objective count differs",
        rich,
        contribution_count,
    )

    def schedule_missing(value):
        portfolio = value["executionEvidence"]["candidatePortfolio"]
        candidate = _candidate(
            portfolio,
            lambda item: item["policyEligibility"] == "eligible"
            and not item["isNoOp"]
            and len(item["schedule"]["stops"]) >= 2,
        )
        candidate["schedule"]["stops"].pop()

    add(
        "M21-schedule-route-mismatch",
        "routeSchedule",
        "richEligible",
        "schedule/remaining-route IDs differ",
        rich,
        schedule_missing,
    )

    def schedule_time(value):
        portfolio = value["executionEvidence"]["candidatePortfolio"]
        candidate = _candidate(
            portfolio,
            lambda item: item["policyEligibility"] == "eligible"
            and item["schedule"]["stops"],
        )
        stop = candidate["schedule"]["stops"][0]
        stop["arrivalTimeMs"] = stop["serviceStartTimeMs"] + 1

    add(
        "M22-schedule-time-order",
        "routeSchedule",
        "richEligible",
        "schedule times differ",
        rich,
        schedule_time,
    )

    def duplicate_route_stop(value):
        portfolio = value["executionEvidence"]["candidatePortfolio"]
        candidate = _candidate(
            portfolio,
            lambda item: len(_route_stops(item)) >= 2,
        )
        stops = _route_stops(candidate)
        stops[1]["stopId"] = stops[0]["stopId"]

    add(
        "M23-duplicate-route-stop",
        "routeSchedule",
        "richEligible",
        "route stop contract differs",
        rich,
        duplicate_route_stop,
    )

    def undeclared_request(value):
        portfolio = value["executionEvidence"]["candidatePortfolio"]
        candidate = _candidate(
            portfolio,
            lambda item: item["policyEligibility"] == "eligible"
            and not item["isNoOp"],
        )
        candidate["newRequestIds"].append("zz-undeclared-request")
        candidate["newRequestIds"].sort()

    add(
        "M24-undeclared-eligible-request",
        "candidateSet",
        "richEligible",
        "eligible request is undeclared",
        rich,
        undeclared_request,
    )
    add(
        "M25-extra-portfolio-field",
        "strictShape",
        "richEligible",
        "candidatePortfolio fields differ",
        rich,
        lambda value: value["executionEvidence"]["candidatePortfolio"].update(
            invented=True
        ),
    )

    def selected_vehicle_mismatch(value):
        selected = value["executionEvidence"]["candidatePortfolio"][
            "selectedCandidateIds"
        ]
        selected[0], selected[1] = selected[1], selected[0]

    add(
        "M26-selected-vehicle-mismatch",
        "selection",
        "richEligible",
        "selectedCandidateIds[0] differs",
        rich,
        selected_vehicle_mismatch,
    )

    def selected_request_overlap(value):
        portfolio = value["executionEvidence"]["candidatePortfolio"]
        selected_ids = portfolio["selectedCandidateIds"]
        by_id = {
            candidate["candidateId"]: candidate
            for candidate in portfolio["candidates"]
        }
        source_index = next(
            index
            for index, candidate_id in enumerate(selected_ids)
            if by_id[candidate_id]["newRequestIds"]
        )
        target_index = next(
            index
            for index, candidate_id in enumerate(selected_ids)
            if index > source_index and by_id[candidate_id]["isNoOp"]
        )
        source = by_id[selected_ids[source_index]]
        target = by_id[selected_ids[target_index]]
        replacement_no_op = next(
            candidate
            for candidate in portfolio["candidates"]
            if candidate["vehicleId"] == target["vehicleId"]
            and candidate["policyEligibility"] == "eligible"
            and not candidate["isNoOp"]
            and candidate["candidateId"] not in selected_ids
        )
        target["isNoOp"] = False
        target["newRequestIds"] = [source["newRequestIds"][0]]
        replacement_no_op["isNoOp"] = True
        replacement_no_op["newRequestIds"] = []

    add(
        "M27-selected-request-overlap",
        "selection",
        "richEligible",
        "selectedCandidateIds[1] differs",
        rich,
        selected_request_overlap,
    )
    return definitions


def _run_mutations(samples, binding, verifier):
    receipts = []
    for mutation_id, layer, sample_id, expected, source, mutate in _mutations(
        samples,
        binding,
    ):
        expected_code = _classify_rejection(expected)
        value = copy.deepcopy(source)
        mutate(value)
        try:
            if sample_id == "binding":
                _verify_binding(value)
            else:
                verifier._verify_audited_solver_evidence(value, 1)
        except (RuntimeError, BindingRejection) as failure:
            message = str(failure)
            actual_code = _classify_rejection(message)
            if expected not in message or actual_code != expected_code:
                raise RuntimeError(
                    f"{mutation_id}: unexpected rejection {message!r}"
                ) from failure
            receipts.append(
                {
                    "mutationId": mutation_id,
                    "layer": layer,
                    "sampleId": sample_id,
                    "expectedMessageContains": expected,
                    "expectedRejectionCode": expected_code,
                    "actualMessage": message,
                    "actualRejectionCode": actual_code,
                    "status": "rejectedAtExpectedLayer",
                }
            )
        else:
            raise RuntimeError(f"{mutation_id}: mutant unexpectedly passed")
    return receipts


def build_receipt(
    repository,
    inventory_path,
    receipt_path,
    record_set_path,
    roots,
):
    inventory_module_path = (
        repository / "simulators/fleetpy-ridebound/wp13_e1_inventory.py"
    )
    inventory_module = _load_module(
        inventory_module_path,
        "ridebound_wp13_e1_inventory_for_falsification",
    )
    verifier_path = (
        repository / "simulators/fleetpy-ridebound/actual_fleetpy_medium_verify.py"
    )
    verifier = _load_module(
        verifier_path,
        "ridebound_medium_verifier_for_e1_falsification",
    )
    inventory = _load(inventory_path)
    rebuilt_inventory = inventory_module.build_inventory(
        repository,
        receipt_path,
        record_set_path,
        roots,
    )
    if (
        _encoded(rebuilt_inventory)
        != inventory_path.read_text(encoding="utf-8")
    ):
        raise RuntimeError("E1 independent inventory reproduction differs")
    if inventory["freezeReceipt"]["sha256"] != _sha256(receipt_path):
        raise RuntimeError("E1 inventory/freeze receipt binding differs")
    samples = _find_samples(inventory, roots)
    first = inventory["runs"][0]
    first_manifest = _load(
        roots[first["panelId"]]
        / first["jobId"]
        / "bundle-manifest.json"
    )
    first_summary = _load(
        roots[first["panelId"]] / first["jobId"] / "summary.json"
    )
    transcript_path = (
        roots[first["panelId"]]
        / first["jobId"]
        / "transcript-00.ndjson"
    )
    transcript = next(
        file
        for file in first_manifest["files"]
        if file["path"] == "transcript-00.ndjson"
    )
    with transcript_path.open("r", encoding="utf-8", newline="") as source:
        first_transcript_record = json.loads(source.readline())
    bundle_file_paths = [file["path"] for file in first_manifest["files"]]
    binding = {
        "jobId": first["jobId"],
        "expectedJobId": first["jobId"],
        "summaryJobId": first_summary["label"],
        "expectedSummaryJobId": first["jobId"],
        "manifestSha256": first["bundleManifestSha256"],
        "expectedManifestSha256": first["bundleManifestSha256"],
        "bundleFilePaths": bundle_file_paths,
        "expectedBundleFilePaths": list(bundle_file_paths),
        "repositoryInventorySha256": first["repositoryInventorySha256"],
        "expectedRepositoryInventorySha256": first[
            "repositoryInventorySha256"
        ],
        "transcriptLengthBytes": transcript["lengthBytes"],
        "expectedTranscriptLengthBytes": transcript["lengthBytes"],
        "transcriptSha256": transcript["sha256"],
        "expectedTranscriptSha256": transcript["sha256"],
        "firstFrameSha256": first_transcript_record["frameSha256"],
        "expectedFirstFrameSha256": first_transcript_record["frameSha256"],
    }
    _verify_binding(binding)
    for _, epoch, solver in samples.values():
        verifier._verify_audited_solver_evidence(solver, epoch)
    mutations = _run_mutations(samples, binding, verifier)
    if len(mutations) != 31 or len(
        {mutation["mutationId"] for mutation in mutations}
    ) != 31:
        raise RuntimeError("E1 falsification catalog must contain exact 31 mutants")
    schema_path = (
        repository / "benchmarks/schemas/wp13/v1/e1-falsification-receipt.schema.json"
    )
    panel_by_id = {panel["panelId"]: panel for panel in inventory["panels"]}
    sample_records = []
    for sample_id in ("richEligible", "withPruned"):
        record, epoch, solver = samples[sample_id]
        portfolio = solver["executionEvidence"]["candidatePortfolio"]
        sample_records.append(
            {
                "sampleId": sample_id,
                "panelId": record["panelId"],
                "jobId": record["jobId"],
                "epochId": epoch,
                "objectiveProfile": portfolio["objectiveProfile"],
                "candidateCount": len(portfolio["candidates"]),
                "sha256": hashlib.sha256(_canonical_bytes(solver)).hexdigest(),
            }
        )
    result = {
        "schemaVersion": "1.0.0",
        "schemaId": SCHEMA_ID,
        "reportType": "ridebound-wp13-e1-falsification-receipt-v1",
        "claimBoundary": {
            "interpretation": "evidenceContractFalsificationOnly",
            "rawMutation": "noneInMemoryMutantsOnly",
            "mechanismConclusion": "notEvaluated",
            "confirmatoryGate": "notApplicableCannotRescueH6",
        },
        "inputEvidence": {
            "freezeReceiptSha256": _sha256(receipt_path),
            "inventorySha256": _sha256(inventory_path),
            "inventoryLengthBytes": inventory_path.stat().st_size,
            "repositoryInventorySha256": inventory["totals"][
                "repositoryInventorySha256"
            ],
            "panelARootInventorySha256": panel_by_id["A"][
                "rootInventorySha256"
            ],
            "panelBRootInventorySha256": panel_by_id["B"][
                "rootInventorySha256"
            ],
        },
        "sourceIdentity": {
            "analyzerSourceSha256": _sha256(pathlib.Path(__file__).resolve()),
            "independentVerifierSourceSha256": _sha256(verifier_path),
            "inventoryAnalyzerSourceSha256": _sha256(inventory_module_path),
            "schemaSha256": _sha256(schema_path),
        },
        "verification": {
            "verifiedArmRunCount": inventory["totals"][
                "completedVerifiedArmRunCount"
            ],
            "verifiedSolverDecisionCount": inventory["totals"][
                "solverDecisionCount"
            ],
            "verifiedPortfolioCount": inventory["totals"][
                "retainedPortfolioEvidenceCount"
            ],
            "mutationCount": len(mutations),
            "expectedRejectionCount": len(mutations),
            "unexpectedPassCount": 0,
            "unexpectedFailureCount": 0,
        },
        "samples": sample_records,
        "mutations": mutations,
    }
    jsonschema.Draft202012Validator(
        _load(schema_path),
        format_checker=jsonschema.FormatChecker(),
    ).validate(result)
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--repository", required=True, type=pathlib.Path)
    parser.add_argument("--inventory", required=True, type=pathlib.Path)
    parser.add_argument("--receipt", required=True, type=pathlib.Path)
    parser.add_argument("--record-set", required=True, type=pathlib.Path)
    parser.add_argument("--panel-a-root", required=True, type=pathlib.Path)
    parser.add_argument("--panel-b-root", required=True, type=pathlib.Path)
    parser.add_argument("--output", type=pathlib.Path)
    arguments = parser.parse_args()
    result = build_receipt(
        arguments.repository.resolve(),
        arguments.inventory.resolve(),
        arguments.receipt.resolve(),
        arguments.record_set.resolve(),
        {
            "A": arguments.panel_a_root.resolve(),
            "B": arguments.panel_b_root.resolve(),
        },
    )
    encoded = _encoded(result)
    if arguments.output:
        output = arguments.output.resolve()
        output.parent.mkdir(parents=True, exist_ok=True)
        with output.open("x", encoding="utf-8", newline="\n") as target:
            target.write(encoded)
    else:
        print(encoded, end="")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
