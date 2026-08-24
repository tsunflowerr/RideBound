import copy
import json
import pathlib
import unittest

import jsonschema


SCHEMA_PATH = (
    pathlib.Path(__file__).resolve().parents[3]
    / "benchmarks"
    / "schemas"
    / "wp13"
    / "v1"
    / "runner-retained-candidate-portfolio-evidence.schema.json"
)


def _candidate(candidate_id, eligibility):
    candidate = {
        "candidateId": candidate_id,
        "vehicleId": "vehicle-1",
        "newRequestIds": [] if candidate_id == "noop" else ["request-1"],
        "isNoOp": candidate_id == "noop",
        "scheduleStrategy": "earliestFeasible",
        "relocatedWaitMs": 0,
        "route": {
            "planVersion": 1,
            "executedStopCount": 0,
            "frozenPrefix": [],
            "mutableSuffix": [
                {
                    "stopId": "pickup-1",
                    "nodeId": "node-1",
                    "kind": "pickup",
                    "requestId": "request-1",
                    "serviceDurationMs": 0,
                }
            ]
            if candidate_id != "noop"
            else [],
        },
        "schedule": {
            "operationalCost": 10 if candidate_id != "noop" else 0,
            "stops": [
                {
                    "stopId": "pickup-1",
                    "arrivalTimeMs": 10,
                    "serviceStartTimeMs": 10,
                    "departureTimeMs": 10,
                }
            ]
            if candidate_id != "noop"
            else [],
        },
        "policyEligibility": eligibility,
    }
    if eligibility == "eligible":
        candidate["objectiveContributions"] = [0, 10]
    return candidate


def _portfolio():
    return {
        "portfolioVersion": "1.0.0",
        "schemaId": (
            "https://ridebound.local/schemas/wp13/v1/"
            "runner-retained-candidate-portfolio-evidence.schema.json"
        ),
        "objectiveProfile": "rollingCost",
        "generatedCandidateCount": 3,
        "policyEligibleCandidateCount": 2,
        "selectedCandidateIds": ["accept"],
        "selectionProblem": {
            "vehicleIds": ["vehicle-1"],
            "requestIds": ["request-1"],
            "objectiveLevels": [
                {
                    "levelIndex": 0,
                    "name": "accepted-request-count",
                    "sense": "maximize",
                    "aggregation": "sum",
                },
                {
                    "levelIndex": 1,
                    "name": "operational-cost",
                    "sense": "minimize",
                    "aggregation": "sum",
                },
            ],
        },
        "candidates": [
            _candidate("accept", "eligible"),
            _candidate("noop", "eligible"),
            _candidate("pruned", "pruned"),
        ],
    }


class RunnerRetainedCandidatePortfolioSchemaTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.schema = json.loads(SCHEMA_PATH.read_text(encoding="utf-8"))
        jsonschema.Draft202012Validator.check_schema(cls.schema)
        cls.validator = jsonschema.Draft202012Validator(cls.schema)

    def test_complete_eligible_and_pruned_shapes_are_valid(self):
        self.validator.validate(_portfolio())

    def test_unknown_field_and_identity_mutations_fail(self):
        for field, value in (
            ("unexpected", True),
            ("portfolioVersion", "2.0.0"),
            ("objectiveProfile", "unknown"),
            ("generatedCandidateCount", 0),
            ("selectedCandidateIds", []),
        ):
            with self.subTest(field=field):
                mutated = _portfolio()
                mutated[field] = value
                with self.assertRaises(jsonschema.ValidationError):
                    self.validator.validate(mutated)

    def test_eligibility_controls_objective_presence(self):
        eligible_without_objectives = _portfolio()
        del eligible_without_objectives["candidates"][0][
            "objectiveContributions"
        ]
        with self.assertRaises(jsonschema.ValidationError):
            self.validator.validate(eligible_without_objectives)

        pruned_with_objectives = _portfolio()
        pruned_with_objectives["candidates"][2][
            "objectiveContributions"
        ] = [0, 10]
        with self.assertRaises(jsonschema.ValidationError):
            self.validator.validate(pruned_with_objectives)

    def test_route_stop_request_binding_is_strict(self):
        pickup_without_request = _portfolio()
        del pickup_without_request["candidates"][0]["route"][
            "mutableSuffix"
        ][0]["requestId"]
        with self.assertRaises(jsonschema.ValidationError):
            self.validator.validate(pickup_without_request)

        waypoint_with_request = copy.deepcopy(_portfolio())
        stop = waypoint_with_request["candidates"][0]["route"][
            "mutableSuffix"
        ][0]
        stop["kind"] = "waypoint"
        with self.assertRaises(jsonschema.ValidationError):
            self.validator.validate(waypoint_with_request)


if __name__ == "__main__":
    unittest.main()
