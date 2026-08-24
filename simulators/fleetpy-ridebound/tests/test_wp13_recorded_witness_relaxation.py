import copy
import importlib.util
import pathlib
import tempfile
import unittest

import jsonschema


ROOT = pathlib.Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location(
    "wp13_recorded_witness_relaxation",
    ROOT / "wp13_recorded_witness_relaxation.py",
)
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


def _action(decision_type, **payload):
    return {"decisionType": decision_type, "payload": payload}


def _plan(candidate="candidate-v1-" + "1" * 64, vehicle="veh-" + "2" * 64):
    return _action(
        "vehiclePlanUpdated",
        candidateId=candidate,
        vehicleId=vehicle,
        route={"mutableSuffix": []},
    )


def _accepted(candidate="candidate-v1-" + "1" * 64, vehicle="veh-" + "2" * 64):
    return _action(
        "requestAccepted",
        candidateId=candidate,
        vehicleId=vehicle,
        requestId="rq-" + "3" * 64,
    )


def _prune(commitments):
    return {
        "candidateId": "candidate-v1-" + "1" * 64,
        "vehicleId": "veh-" + "2" * 64,
        "newRequestIds": ["rq-" + "3" * 64],
        "code": commitments[0]["code"] if commitments else "CAPACITY",
        "commitmentWitnesses": commitments,
    }


def _budget():
    return {
        "stage": "budget",
        "code": "COMMITMENT_BUDGET_EXCEEDED",
        "vehicleId": "veh-" + "2" * 64,
        "requestId": "rq-" + "4" * 64,
        "dimension": "drop_eta_total_ms",
        "limit": 30_000,
        "before": 10_000,
        "delta": 25_000,
        "after": 35_000,
    }


def _lock():
    return {
        "stage": "lock",
        "code": "COMMITMENT_PHASE_LOCK",
        "vehicleId": "veh-" + "2" * 64,
        "requestId": "rq-" + "4" * 64,
        "dimension": "pickup_eta_ms",
        "rule": "final_confirmation",
    }


class WitnessClearanceTests(unittest.TestCase):
    def test_budget_clearance_is_exact_integer_overrun(self):
        result = MODULE._witness_clearance(_budget(), "veh-" + "2" * 64)

        self.assertEqual("numericBudgetLimitIncrease", result["clearanceKind"])
        self.assertEqual(35_000, result["requiredLimit"])
        self.assertEqual(5_000, result["additiveLimitIncrease"])

    def test_lock_clearance_is_categorical_without_numeric_amount(self):
        result = MODULE._witness_clearance(_lock(), "veh-" + "2" * 64)

        self.assertEqual("categoricalLockDisablement", result["clearanceKind"])
        self.assertEqual("disableRecordedRuleForDimension", result["requiredChange"])
        self.assertNotIn("additiveLimitIncrease", result)

    def test_budget_arithmetic_and_unknown_stage_fail_closed(self):
        value = _budget()
        value["after"] += 1
        with self.assertRaisesRegex(RuntimeError, "arithmetic"):
            MODULE._witness_clearance(value, "veh-" + "2" * 64)

        value = _lock()
        value["stage"] = "projection"
        with self.assertRaisesRegex(RuntimeError, "unsupported"):
            MODULE._witness_clearance(value, "veh-" + "2" * 64)

    def test_boolean_and_vehicle_mutations_fail_closed(self):
        value = _budget()
        value["limit"] = True
        with self.assertRaisesRegex(RuntimeError, "safe integer"):
            MODULE._witness_clearance(value, "veh-" + "2" * 64)

        with self.assertRaisesRegex(RuntimeError, "vehicle differs"):
            MODULE._witness_clearance(_lock(), "veh-" + "9" * 64)


class CandidateLinkTests(unittest.TestCase):
    def test_commitment_selected_and_absent_statuses_are_distinct(self):
        b1 = [_accepted(), _plan()]
        commitment = MODULE._candidate_links(
            b1,
            [],
            {"prunedCandidates": [_prune([_budget()])]},
        )[0]
        selected = MODULE._candidate_links(
            b1,
            [_accepted(), _plan()],
            {"prunedCandidates": []},
        )[0]
        absent = MODULE._candidate_links(b1, [], {"prunedCandidates": []})[0]

        self.assertEqual("prunedWithCommitmentWitness", commitment["status"])
        self.assertEqual("selectedByC1", selected["status"])
        self.assertEqual("absentRetainedOrOmittedNotRecorded", absent["status"])
        self.assertEqual("notRecorded", absent["clearanceStatus"])
        self.assertEqual("notEvaluated", absent["candidateFeasibilityAfterClearance"])

    def test_noncommitment_prune_has_no_invented_clearance(self):
        result = MODULE._candidate_links(
            [_accepted(), _plan()],
            [],
            {"prunedCandidates": [_prune([])]},
        )[0]

        self.assertEqual("prunedWithoutCommitmentWitness", result["status"])
        self.assertNotIn("witnessClearances", result)

    def test_accepted_plan_and_prune_identity_mutations_fail_closed(self):
        accepted = _accepted(vehicle="veh-" + "9" * 64)
        with self.assertRaisesRegex(RuntimeError, "accepted candidate/plan"):
            MODULE._selected_plans([accepted, _plan()], "b1")

        witness = _prune([_budget()])
        witness["newRequestIds"] = []
        with self.assertRaisesRegex(RuntimeError, "pruned candidate identity"):
            MODULE._candidate_links(
                [_accepted(), _plan()],
                [],
                {"prunedCandidates": [witness]},
            )

    def test_duplicate_or_selected_and_pruned_candidate_fails_closed(self):
        witness = _prune([_budget()])
        with self.assertRaisesRegex(RuntimeError, "duplicate C1 pruned"):
            MODULE._candidate_links(
                [_accepted(), _plan()],
                [],
                {"prunedCandidates": [witness, copy.deepcopy(witness)]},
            )

        with self.assertRaisesRegex(RuntimeError, "both selected and pruned"):
            MODULE._candidate_links(
                [_accepted(), _plan()],
                [_accepted(), _plan()],
                {"prunedCandidates": [witness]},
            )

    def test_cross_candidate_request_and_mixed_witnesses_fail_closed(self):
        other_candidate = "candidate-v1-" + "5" * 64
        other_vehicle = "veh-" + "6" * 64
        with self.assertRaisesRegex(RuntimeError, "duplicate accepted request"):
            MODULE._selected_plans(
                [
                    _accepted(),
                    _plan(),
                    _accepted(other_candidate, other_vehicle),
                    _plan(other_candidate, other_vehicle),
                ],
                "b1",
            )

        witness = _prune([_budget()])
        witness["physicalWitness"] = {
            "code": "CAPACITY",
            "vehicleId": "veh-" + "2" * 64,
        }
        with self.assertRaisesRegex(RuntimeError, "mixes physical and commitment"):
            MODULE._candidate_links(
                [_accepted(), _plan()],
                [],
                {"prunedCandidates": [witness]},
            )

        witness = _prune([_budget()])
        witness["code"] = "COMMITMENT_PHASE_LOCK"
        with self.assertRaisesRegex(RuntimeError, "disagree with prune code"):
            MODULE._candidate_links(
                [_accepted(), _plan()],
                [],
                {"prunedCandidates": [witness]},
            )


class ContractTests(unittest.TestCase):
    def test_schema_is_exact_and_valid(self):
        schema = MODULE._load_schema()

        self.assertEqual(
            "RideBound WP13 recorded-witness relaxation set v1",
            schema["title"],
        )

        link_schema = {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "$ref": "#/$defs/candidateLink",
            "$defs": schema["$defs"],
        }
        links = [
            MODULE._candidate_links(
                [_accepted(), _plan()],
                [],
                {"prunedCandidates": [_prune([_budget()])]},
            )[0],
            MODULE._candidate_links(
                [_accepted(), _plan()],
                [],
                {"prunedCandidates": [_prune([_lock()])]},
            )[0],
            MODULE._candidate_links(
                [_accepted(), _plan()],
                [],
                {"prunedCandidates": [_prune([])]},
            )[0],
            MODULE._candidate_links(
                [_accepted(), _plan()],
                [_accepted(), _plan()],
                {"prunedCandidates": []},
            )[0],
            MODULE._candidate_links(
                [_accepted(), _plan()],
                [],
                {"prunedCandidates": []},
            )[0],
        ]
        validator = jsonschema.Draft202012Validator(link_schema)
        for link in links:
            validator.validate(link)

    def test_mutated_behavioral_report_identity_fails_before_json_use(self):
        with self.assertRaisesRegex(RuntimeError, "report identity differs"):
            with tempfile.TemporaryDirectory() as temporary:
                path = pathlib.Path(temporary) / "report.json"
                path.write_bytes(b"{}\n")
                MODULE._read_behavioral_report(path)


if __name__ == "__main__":
    unittest.main()
