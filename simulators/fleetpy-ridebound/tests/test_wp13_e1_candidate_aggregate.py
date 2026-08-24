import copy
import importlib.util
import json
import pathlib
import unittest

import jsonschema


ROOT = pathlib.Path(__file__).resolve().parents[3]
MODULE_PATH = (
    ROOT
    / "simulators/fleetpy-ridebound/"
    "wp13_e1_candidate_aggregate.py"
)
SPEC = importlib.util.spec_from_file_location(
    "wp13_e1_candidate_aggregate_under_test",
    MODULE_PATH,
)
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


def _candidate(candidate_id="c1", vehicle_id="v1", requests=None):
    return {
        "candidateId": candidate_id,
        "vehicleId": vehicle_id,
        "newRequestIds": [] if requests is None else requests,
        "isNoOp": requests is None,
        "scheduleStrategy": "earliestFeasible",
        "relocatedWaitMs": 0,
        "route": {
            "planVersion": 1,
            "executedStopCount": 0,
            "frozenPrefix": [],
            "mutableSuffix": [],
        },
        "schedule": {"operationalCost": 10, "stops": []},
        "policyEligibility": "eligible",
        "objectiveContributions": [1, 10],
    }


def _portfolio(candidates):
    return {
        "candidates": candidates,
        "selectedCandidateIds": [candidates[0]["candidateId"]],
        "selectionProblem": {
            "objectiveLevels": [
                {"sense": "maximize"},
                {"sense": "minimize"},
            ]
        },
    }


class Wp13E1CandidateAggregateTests(unittest.TestCase):
    def test_schema_is_strict_draft_2020_12(self):
        schema = json.loads(
            (
                ROOT
                / "benchmarks/schemas/wp13/v1/"
                "e1-candidate-descriptive-aggregation.schema.json"
            ).read_text(encoding="utf-8")
        )
        jsonschema.Draft202012Validator.check_schema(schema)
        self.assertFalse(schema["additionalProperties"])
        self.assertEqual(40, schema["properties"]["records"]["minItems"])
        self.assertEqual(
            "overlappingCellsNotAdditive",
            schema["$defs"]["claimBoundary"]["properties"][
                "associationRows"
            ]["const"],
        )

    def test_signature_is_policy_neutral_and_collision_fails_closed(self):
        baseline = _candidate(requests=["r1"])
        treatment = copy.deepcopy(baseline)
        treatment["candidateId"] = "other-id"
        treatment["policyEligibility"] = "pruned"
        del treatment["objectiveContributions"]
        self.assertEqual(
            MODULE._candidate_signature(baseline),
            MODULE._candidate_signature(treatment),
        )
        changed = copy.deepcopy(treatment)
        changed["route"]["planVersion"] = 2
        self.assertNotEqual(
            MODULE._candidate_signature(baseline),
            MODULE._candidate_signature(changed),
        )
        with self.assertRaisesRegex(RuntimeError, "signature collision"):
            MODULE._signature_index([baseline, treatment], "fixture")

    def test_link_classes_and_within_vehicle_ordinal_are_explicit(self):
        selected = _candidate("selected", requests=["r1"])
        worse = _candidate("worse", requests=["r2"])
        worse["objectiveContributions"] = [1, 20]
        portfolio = _portfolio([selected, worse])
        self.assertEqual(
            (1, 2),
            MODULE._within_vehicle_ordinal(portfolio, selected),
        )
        treatment = copy.deepcopy(selected)
        treatment["candidateId"] = "treatment"
        treatment["policyEligibility"] = "pruned"
        del treatment["objectiveContributions"]
        index = MODULE._signature_index([treatment], "c1")
        evidence = {
            "prunedCandidates": [
                {
                    "candidateId": "treatment",
                    "vehicleId": "v1",
                    "newRequestIds": ["r1"],
                    "code": "COMMITMENT_BUDGET_EXCEEDED",
                    "commitmentWitnesses": [
                        {"code": "COMMITMENT_BUDGET_EXCEEDED"}
                    ],
                }
            ]
        }
        classification, candidate, prune = MODULE._classify_link(
            selected,
            index,
            set(),
            evidence,
        )
        self.assertEqual("prunedWithRecordedWitness", classification)
        self.assertEqual("treatment", candidate["candidateId"])
        self.assertEqual(1, prune["commitmentWitnessCount"])
        treatment["policyEligibility"] = "eligible"
        treatment["objectiveContributions"] = [1, 15]
        index = MODULE._signature_index([treatment], "c1")
        self.assertEqual(
            "eligibleNotSelectedAssociation",
            MODULE._classify_link(selected, index, set(), evidence)[0],
        )
        self.assertEqual(
            "selectedByC1",
            MODULE._classify_link(selected, index, {"treatment"}, evidence)[0],
        )
        self.assertEqual(
            "absentFromGeneratedSet",
            MODULE._classify_link(selected, {}, set(), evidence)[0],
        )

    def test_aggregate_enforces_panel_denominators_without_pooling_service(self):
        completion = {
            "A": ([87] * 15 + [86] * 5, [80] + [79] * 19),
            "B": ([49] * 6 + [48] * 14, [43] * 20),
        }
        records = []
        ordinal = 0
        for panel_id in ("A", "B"):
            for index in range(20):
                b1, c1 = completion[panel_id][0][index], completion[panel_id][1][
                    index
                ]
                links = [{"classification": "prunedWithRecordedWitness"}]
                if ordinal == 0:
                    links.append(
                        {"classification": "eligibleNotSelectedAssociation"}
                    )
                records.append(
                    {
                        "panelId": panel_id,
                        "unitId": f"{panel_id}-{index}",
                        "generatedOverlap": {
                            "b1GeneratedCount": 2,
                            "c1GeneratedCount": 2,
                            "intersectionCount": 2,
                            "candidateIdDriftCount": 0,
                            "relation": "equal",
                        },
                        "immediate": {
                            "acceptanceRelation": (
                                "c1LowerImmediateAcceptance"
                                if ordinal < 8
                                else "equalImmediateAcceptance"
                            )
                        },
                        "trajectory": {
                            "h6B1Completed": b1,
                            "h6C1Completed": c1,
                            "h6CompletedDeltaC1MinusB1": c1 - b1,
                        },
                        "links": links,
                    }
                )
                ordinal += 1
        totals, panels, rows = MODULE._aggregate(records, 44156)
        self.assertEqual(41, totals["actionfulLinkCount"])
        self.assertEqual([-154, -106], [
            panel["h6CompletedDeltaC1MinusB1"] for panel in panels
        ])
        self.assertTrue(rows)
        mutated = copy.deepcopy(records)
        mutated[0]["trajectory"]["h6B1Completed"] -= 1
        with self.assertRaisesRegex(RuntimeError, "outcome differs"):
            MODULE._aggregate(mutated, 44156)


if __name__ == "__main__":
    unittest.main()
