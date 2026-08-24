import copy
import importlib.util
import pathlib
import tempfile
import unittest


ROOT = pathlib.Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location(
    "wp13_mechanism_classify",
    ROOT / "wp13_mechanism_classify.py",
)
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


def _candidate(index, status, **extra):
    return {
        "candidateId": f"candidate-v1-{index:064x}",
        "status": status,
        **extra,
    }


def _budget_candidate(index=1):
    return _candidate(
        index,
        "prunedWithCommitmentWitness",
        witnessClearances=[{"clearanceKind": "numericBudgetLimitIncrease"}],
    )


def _lock_candidate(index=1):
    return _candidate(
        index,
        "prunedWithCommitmentWitness",
        witnessClearances=[{"clearanceKind": "categoricalLockDisablement"}],
    )


def _source_reports():
    behavior = {"records": []}
    relaxation = {"records": []}
    for panel in ("A", "B"):
        for index in range(1, 21):
            unit = f"unit-{panel.lower()}-{index:02d}"
            behavior_record = {
                "panelId": panel,
                "unitId": unit,
                "epochId": index,
                "simTimeMs": index * 1_000,
                "primaryDifferenceClass": "acceptedVehicleAssignmentDifference",
                "immediateRequestComparison": {
                    "acceptanceRelation": "equalImmediateAcceptance",
                    "acceptedCountDeltaC1MinusB1": 0,
                },
            }
            links = [_budget_candidate(index)]
            if panel == "A" and index == 1:
                links.append(
                    _candidate(
                        10_000,
                        "selectedByC1",
                        clearanceStatus="notApplicableCandidateSelected",
                    )
                )
            behavior["records"].append(behavior_record)
            relaxation["records"].append(
                {
                    "panelId": panel,
                    "unitId": unit,
                    "epochId": index,
                    "simTimeMs": index * 1_000,
                    "sourceBehavioralComparisonSha256": MODULE._value_hash(
                        "BehavioralComparisonRecord",
                        behavior_record,
                    ),
                    "candidateLinks": links,
                }
            )
    return behavior, relaxation


class LinkClassificationTests(unittest.TestCase):
    def test_budget_and_lock_keep_distinct_noncausal_classes(self):
        budget = MODULE._classify_link(_budget_candidate())
        lock = MODULE._classify_link(_lock_candidate())
        mixed = MODULE._classify_link(
            _candidate(
                2,
                "prunedWithCommitmentWitness",
                witnessClearances=[
                    {"clearanceKind": "categoricalLockDisablement"},
                    {"clearanceKind": "numericBudgetLimitIncrease"},
                ],
            )
        )

        self.assertEqual(["recordedBudgetWitness"], budget["evidenceClasses"])
        self.assertEqual(["recordedLockWitness"], lock["evidenceClasses"])
        self.assertEqual(
            ["recordedBudgetWitness", "recordedLockWitness"],
            mixed["evidenceClasses"],
        )
        self.assertEqual("recordedCandidateLinkNotCausal", budget["evidenceStrength"])

    def test_selected_and_absent_are_not_reinterpreted(self):
        selected = MODULE._classify_link(_candidate(1, "selectedByC1"))
        absent = MODULE._classify_link(
            _candidate(2, "absentRetainedOrOmittedNotRecorded")
        )

        self.assertEqual(["sharedSelectedCandidate"], selected["evidenceClasses"])
        self.assertEqual(
            ["rankingOrSearchOmissionIndeterminate"],
            absent["evidenceClasses"],
        )
        self.assertEqual("indeterminateMissingPortfolio", absent["evidenceStrength"])

    def test_physical_and_unknown_prune_codes_remain_distinct(self):
        physical = MODULE._classify_link(
            _candidate(1, "prunedWithoutCommitmentWitness", pruneCode="CAPACITY")
        )
        unknown = MODULE._classify_link(
            _candidate(2, "prunedWithoutCommitmentWitness", pruneCode="FUTURE_CODE")
        )

        self.assertEqual(["recordedPhysicalPruneCode"], physical["evidenceClasses"])
        self.assertEqual(["unsupportedRecordedPrune"], unknown["evidenceClasses"])
        self.assertEqual("unsupportedRecordedEvidence", unknown["evidenceStrength"])

    def test_unknown_status_or_clearance_kind_fails_closed(self):
        with self.assertRaisesRegex(RuntimeError, "unsupported candidate-link"):
            MODULE._classify_link(_candidate(1, "retained"))
        with self.assertRaisesRegex(RuntimeError, "clearance kind"):
            MODULE._classify_link(
                _candidate(
                    1,
                    "prunedWithCommitmentWitness",
                    witnessClearances=[{"clearanceKind": "weightedScore"}],
                )
            )


class ReportContractTests(unittest.TestCase):
    def test_complete_synthetic_inventory_is_schema_valid_and_reconciled(self):
        behavior, relaxation = _source_reports()
        schema = MODULE._load_schema(
            MODULE._SCHEMA,
            MODULE._SCHEMA_SHA256,
            "classification",
        )

        result = MODULE.build(behavior, relaxation, schema)

        self.assertEqual(40, result["recordCount"])
        self.assertEqual(41, result["candidateLinkCount"])
        self.assertEqual(40, result["evidenceClassOccurrenceCounts"]["recordedBudgetWitness"])
        self.assertEqual(1, result["evidenceClassOccurrenceCounts"]["sharedSelectedCandidate"])
        self.assertEqual(
            40,
            result["acceptanceRelationCrossTab"]["recordedBudgetWitness"][
                "equalImmediateAcceptance"
            ],
        )

    def test_source_binding_and_relation_contradictions_fail_closed(self):
        behavior, relaxation = _source_reports()
        schema = MODULE._load_schema(
            MODULE._SCHEMA,
            MODULE._SCHEMA_SHA256,
            "classification",
        )
        relaxation["records"][0]["epochId"] += 1
        with self.assertRaisesRegex(RuntimeError, "binding differs"):
            MODULE.build(behavior, relaxation, schema)

        behavior, relaxation = _source_reports()
        behavior["records"][0]["immediateRequestComparison"] = {
            "acceptanceRelation": "equalImmediateAcceptance",
            "acceptedCountDeltaC1MinusB1": -1,
        }
        relaxation["records"][0]["sourceBehavioralComparisonSha256"] = (
            MODULE._value_hash("BehavioralComparisonRecord", behavior["records"][0])
        )
        with self.assertRaisesRegex(RuntimeError, "relation contradicts"):
            MODULE.build(behavior, relaxation, schema)

    def test_duplicate_or_missing_unit_fails_closed(self):
        behavior, relaxation = _source_reports()
        schema = MODULE._load_schema(
            MODULE._SCHEMA,
            MODULE._SCHEMA_SHA256,
            "classification",
        )
        relaxation["records"][-1] = copy.deepcopy(relaxation["records"][0])

        with self.assertRaisesRegex(RuntimeError, "inventory differs"):
            MODULE.build(behavior, relaxation, schema)

        behavior, relaxation = _source_reports()
        relaxation["records"].append(copy.deepcopy(relaxation["records"][0]))

        with self.assertRaisesRegex(RuntimeError, "inventory differs"):
            MODULE.build(behavior, relaxation, schema)

    def test_mutated_input_identity_fails_before_json_use(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = pathlib.Path(temporary) / "behavior.json"
            path.write_bytes(b"{}\n")
            with self.assertRaisesRegex(RuntimeError, "identity differs"):
                MODULE._read_exact(
                    path,
                    MODULE._BEHAVIOR_LENGTH,
                    MODULE._BEHAVIOR_SHA256,
                    "behavioral report",
                )


if __name__ == "__main__":
    unittest.main()
