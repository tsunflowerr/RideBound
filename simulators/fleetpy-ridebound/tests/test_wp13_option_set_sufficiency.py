import copy
import importlib.util
import pathlib
import tempfile
import unittest

import jsonschema


MODULE_PATH = (
    pathlib.Path(__file__).resolve().parents[1]
    / "wp13_option_set_sufficiency.py"
)
SPEC = importlib.util.spec_from_file_location(
    "wp13_option_set_sufficiency_under_test",
    MODULE_PATH,
)
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


def _vehicle_loss(vehicle_id="veh-1"):
    return {
        "vehicleId": vehicle_id,
        "explorationWorkUnits": 3,
        "evaluatedCandidatePathCount": 1,
        "uniqueFeasibleCandidateCountBeforeCap": 2,
        "retainedCandidateCount": 2,
        "physicallyOrSchedulePrunedCount": 0,
        "omittedUnexpandedCandidatePathCount": 0,
        "omittedFeasibleCandidateCountByCap": 0,
        "workBudgetExhausted": False,
        "candidateCapApplied": False,
        "omissionCountWasSaturated": False,
        "eligibleRepairRequestCount": 0,
        "consideredRepairRequestCount": 0,
        "omittedRepairRequestCount": 0,
    }


def _selection():
    return {
        "consumedGenerationWorkUnits": 3,
        "consumedValidationWorkUnits": 1,
        "omittedCandidateCount": 0,
        "omissionCountWasSaturated": False,
        "primarySolveStatus": "optimal",
        "primarySolverDiagnostics": {},
        "finalSolveStatus": "optimal",
        "finalSolverDiagnostics": {},
        "executionPath": "validatedIncumbent",
        "fallbackValidationAttempts": 0,
        "primaryIncumbentRejected": False,
        "validationWitnesses": [],
    }


def _evidence():
    return {
        "evidenceVersion": "1.0.0",
        "generation": {
            "totalPendingRequestCount": 1,
            "consideredRequestCount": 1,
            "omittedRequestCount": 0,
            "vehicleLosses": [_vehicle_loss()],
            "omissions": [],
        },
        "prunedCandidates": [],
        "selection": _selection(),
    }


def _sources(panel_id="A", unit_id="u00", unresolved=True):
    source = {
        "panelId": panel_id,
        "unitId": unit_id,
        "payload": "source",
    }
    behavior = {
        "epochId": 1,
        "simTimeMs": 100,
        "sourceFirstDivergenceRecordSha256": MODULE._value_hash(
            "FirstDivergenceRecord",
            source,
        ),
    }
    behavior_hash = MODULE._value_hash("BehavioralComparisonRecord", behavior)
    candidate_links = []
    candidate_classifications = []
    classes = ["recordedBudgetWitness"]
    if unresolved:
        candidate_links.append(
            {
                "candidateId": "candidate-1",
                "status": "absentRetainedOrOmittedNotRecorded",
            }
        )
        candidate_classifications.append({"candidateId": "candidate-1"})
        classes = ["rankingOrSearchOmissionIndeterminate"]
    relaxation_record = {
        "sourceBehavioralComparisonSha256": behavior_hash,
        "candidateLinks": candidate_links,
        "rawEvidence": {
            "b1BundleManifestSha256": "a" * 64,
            "b1TranscriptSha256": "b" * 64,
            "c1BundleManifestSha256": "c" * 64,
            "c1TranscriptSha256": "d" * 64,
        },
    }
    classification_record = {
        "sourceBehavioralComparisonSha256": behavior_hash,
        "sourceRelaxationRecordSha256": MODULE._value_hash(
            "RecordedWitnessRelaxationRecord",
            relaxation_record,
        ),
        "candidateClassifications": candidate_classifications,
        "immediateAcceptanceRelation": "equalImmediateAcceptance",
        "evidenceClasses": classes,
    }
    arms = {
        arm: MODULE._arm_covariates(_evidence()) for arm in ("b1", "c1")
    }
    return source, behavior, relaxation_record, classification_record, arms


def _record(panel_id="A", unit_id="u00", unresolved=True):
    return MODULE._record(
        *_sources(panel_id, unit_id, unresolved),
    )


class OptionSetSufficiencyTests(unittest.TestCase):
    def test_schema_is_valid_and_identity_is_pinned(self):
        schema = MODULE._load_schema(
            MODULE._SCHEMA,
            MODULE._SCHEMA_SHA256,
            "option-set sufficiency",
        )

        self.assertEqual(
            "ridebound-wp13-option-set-sufficiency-set-v1",
            schema["properties"]["reportType"]["const"],
        )

    def test_arm_covariates_are_typed_and_complete(self):
        result = MODULE._arm_covariates(_evidence())

        self.assertTrue(result["generationCompleteFromRecordedCounters"])
        self.assertEqual(2, result["generationCounts"]["retainedCandidateCount"])
        self.assertEqual(3, result["selectionCounts"]["consumedGenerationWorkUnits"])
        self.assertEqual("optimal", result["selectionStatuses"]["finalSolveStatus"])
        self.assertEqual(64, len(result["generationEvidenceSha256"]))

    def test_generation_omission_marks_counts_incomplete(self):
        evidence = _evidence()
        loss = evidence["generation"]["vehicleLosses"][0]
        loss["retainedCandidateCount"] = 1
        loss["omittedFeasibleCandidateCountByCap"] = 1
        loss["candidateCapApplied"] = True

        result = MODULE._arm_covariates(evidence)

        self.assertFalse(result["generationCompleteFromRecordedCounters"])
        self.assertEqual(
            1,
            result["generationCounts"]["candidateCapAppliedVehicleCount"],
        )

    def test_generation_conservation_and_duplicate_vehicle_fail_closed(self):
        evidence = _evidence()
        evidence["generation"]["omittedRequestCount"] = 1
        with self.assertRaisesRegex(RuntimeError, "request counts do not conserve"):
            MODULE._arm_covariates(evidence)

        evidence = _evidence()
        evidence["generation"]["vehicleLosses"].append(_vehicle_loss())
        evidence["selection"]["consumedGenerationWorkUnits"] = 6
        with self.assertRaisesRegex(RuntimeError, "duplicate vehicle loss"):
            MODULE._arm_covariates(evidence)

        evidence = _evidence()
        evidence["generation"]["vehicleLosses"][0][
            "uniqueFeasibleCandidateCountBeforeCap"
        ] = 3
        with self.assertRaisesRegex(RuntimeError, "candidate-cap counts"):
            MODULE._arm_covariates(evidence)

        evidence = _evidence()
        loss = evidence["generation"]["vehicleLosses"][0]
        loss["retainedCandidateCount"] = 1
        loss["omittedFeasibleCandidateCountByCap"] = 1
        with self.assertRaisesRegex(RuntimeError, "lacks applied flag"):
            MODULE._arm_covariates(evidence)

    def test_prune_partition_and_duplicate_candidate_fail_closed(self):
        physical = {
            "candidateId": "physical",
            "vehicleId": "veh-1",
            "newRequestIds": ["rq-1"],
            "code": "CAPACITY",
            "physicalWitness": {"code": "CAPACITY", "vehicleId": "veh-1"},
            "commitmentWitnesses": [],
        }
        commitment = {
            "candidateId": "commitment",
            "vehicleId": "veh-1",
            "newRequestIds": ["rq-1"],
            "code": "BUDGET",
            "commitmentWitnesses": [
                {
                    "stage": "budget",
                    "code": "BUDGET",
                    "vehicleId": "veh-1",
                }
            ],
        }
        untyped = {
            "candidateId": "untyped",
            "vehicleId": "veh-1",
            "newRequestIds": ["rq-1"],
            "code": "UNKNOWN",
            "commitmentWitnesses": [],
        }

        result = MODULE._prune_counts([physical, commitment, untyped])

        self.assertEqual(3, result["prunedCandidateCount"])
        self.assertEqual(1, result["physicalWitnessCandidateCount"])
        self.assertEqual(1, result["commitmentWitnessCandidateCount"])
        self.assertEqual(1, result["untypedPrunedCandidateCount"])
        with self.assertRaisesRegex(RuntimeError, "duplicate candidate ID"):
            MODULE._prune_counts([physical, copy.deepcopy(physical)])

    def test_evidence_version_selection_work_and_shape_fail_closed(self):
        evidence = _evidence()
        evidence["evidenceVersion"] = "1.1.0"
        with self.assertRaisesRegex(RuntimeError, "version differs"):
            MODULE._arm_covariates(evidence)

        evidence = _evidence()
        evidence["selection"]["consumedGenerationWorkUnits"] = 2
        with self.assertRaisesRegex(RuntimeError, "work units disagree"):
            MODULE._arm_covariates(evidence)

        evidence = _evidence()
        evidence["generation"]["retainedCandidates"] = []
        with self.assertRaisesRegex(RuntimeError, "unsupported fields"):
            MODULE._arm_covariates(evidence)

    def test_record_binding_and_identity_gap_fail_closed(self):
        values = _sources()
        record = MODULE._record(*values)

        self.assertEqual("notRecorded", record["candidateIdentityGap"]["resolutionStatus"])
        self.assertEqual(
            "notEstablishedByAggregateEquality",
            record["pairedRelations"]["candidateIdentityEquality"],
        )

        values = list(_sources())
        values[3]["sourceRelaxationRecordSha256"] = "0" * 64
        with self.assertRaisesRegex(RuntimeError, "source record binding differs"):
            MODULE._record(*values)

        values = list(_sources())
        values[3]["evidenceClasses"] = ["recordedBudgetWitness"]
        with self.assertRaisesRegex(RuntimeError, "unresolved candidate class differs"):
            MODULE._record(*values)

    def test_complete_inventory_is_schema_valid_and_reconciled(self):
        records = []
        for panel_id in ("A", "B"):
            records.extend(
                _record(panel_id, f"u{index:02d}", unresolved=index == 0)
                for index in range(20)
            )
        schema = MODULE._load_schema(
            MODULE._SCHEMA,
            MODULE._SCHEMA_SHA256,
            "option-set sufficiency",
        )

        result = MODULE.build(records, schema)

        self.assertEqual(40, result["recordCount"])
        self.assertEqual(80, result["armEpochCount"])
        self.assertEqual(2, result["evidenceVNextDecision"]["unresolvedPairCount"])
        self.assertFalse(
            result["evidenceVNextDecision"]["authorizesExploratoryRerun"]
        )
        self.assertEqual(
            20,
            result["panelSummaries"][0]["exactGenerationEvidencePairCount"],
        )

        mutated = copy.deepcopy(result)
        mutated["panelSummaries"][0]["recordCount"] = 19
        with self.assertRaises(jsonschema.ValidationError):
            jsonschema.Draft202012Validator(schema).validate(mutated)

    def test_duplicate_extra_or_missing_inventory_fails_closed(self):
        records = []
        for panel_id in ("A", "B"):
            records.extend(
                _record(panel_id, f"u{index:02d}") for index in range(20)
            )
        schema = MODULE._load_schema(
            MODULE._SCHEMA,
            MODULE._SCHEMA_SHA256,
            "option-set sufficiency",
        )

        with self.assertRaisesRegex(RuntimeError, "inventory differs"):
            MODULE.build(records[:-1], schema)
        with self.assertRaisesRegex(RuntimeError, "inventory differs"):
            MODULE.build(records + [copy.deepcopy(records[0])], schema)
        duplicate = copy.deepcopy(records)
        duplicate[-1] = copy.deepcopy(duplicate[0])
        with self.assertRaisesRegex(RuntimeError, "identity/order differs"):
            MODULE.build(duplicate, schema)

    def test_mutated_exact_input_is_rejected_before_json_use(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = pathlib.Path(temporary) / "input.json"
            path.write_bytes(b"{}\n")

            with self.assertRaisesRegex(RuntimeError, "identity differs"):
                MODULE._read_exact(path, 4, "0" * 64, "input")


if __name__ == "__main__":
    unittest.main()
