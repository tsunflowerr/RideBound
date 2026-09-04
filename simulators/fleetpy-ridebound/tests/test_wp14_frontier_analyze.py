import base64
import importlib.util
import json
import pathlib
import tempfile
import unittest
from unittest import mock

import jsonschema


ROOT = pathlib.Path(__file__).resolve().parents[3]
MODULE_PATH = ROOT / "simulators/fleetpy-ridebound/wp14_frontier_analyze.py"
SPEC = importlib.util.spec_from_file_location("wp14_frontier_under_test", MODULE_PATH)
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


def envelope(frame):
    payload = json.dumps(frame, separators=(",", ":")).encode("utf-8")
    return json.dumps({"frameBase64": base64.b64encode(payload).decode("ascii")})


def promise_frame(pickup):
    return {
        "messageType": "decision",
        "payload": {
            "solver": {"executionEvidence": {"prunedCandidates": []}},
            "actions": [
                {
                    "decisionType": "promisePublished",
                    "payload": {
                        "decisionDelta": {
                            "pickupEtaTotalMs": 0,
                            "dropEtaTotalMs": 0,
                            "materialEtaRevisionCount": 0,
                            "prePickupInsertedStopCount": 0,
                        },
                        "exogenousDelta": {
                            "pickupEtaTotalMs": 0,
                            "dropEtaTotalMs": 0,
                        },
                        "promise": {
                            "requestId": "r1",
                            "pickupEtaMs": pickup,
                            "dropEtaMs": 2000,
                        },
                        "budgetAfter": {"dropEtaTotalMs": 0},
                    },
                }
            ],
        },
    }


def observation(completed=100, burden=1000):
    return {
        "arrived": 108,
        "completed": completed,
        "decisions": 1,
        "attributedPickupMs": 0,
        "attributedDropMs": burden,
        "attributedTotalMs": burden,
        "exogenousTotalMs": 0,
        "experiencedTotalMs": 0,
        "pickupEtaImprovementCount": 0,
        "pickupEtaImprovementMs": 0,
        "pickupEtaWorseningCount": 0,
        "pickupEtaWorseningMs": 0,
        "disruptiveDecisions": 0,
        "commitmentPrunedByCode": {},
        "commitmentPrunedByDimension": {},
        "ridersWithOpenPromise": 1,
        "ridersCharged": 0,
        "riderDropConsumptionP95Ms": 0,
        "riderDropConsumptionMaxMs": 0,
        "semanticHash": "0" * 64,
        "repositoryInventorySha256": "1" * 64,
        "_riderDropConsumptionValuesMs": [0],
    }


class Wp14FrontierAnalyzeTests(unittest.TestCase):
    def test_nearest_rank_p95_has_no_off_by_one(self):
        self.assertEqual(20, MODULE.percentile95(list(range(1, 22))))
        self.assertEqual(19, MODULE.percentile95(list(range(1, 21))))
        self.assertEqual(0, MODULE.percentile95([]))

    def test_directional_pickup_changes_are_measured_separately(self):
        arrivals = {
            "messageType": "eventBatch",
            "payload": {
                "events": [
                    {"eventType": "requestArrived"} for _ in range(108)
                ]
            },
        }
        with tempfile.TemporaryDirectory() as directory:
            bundle = pathlib.Path(directory)
            (bundle / "summary.json").write_text(
                json.dumps(
                    {
                        "status": "pass",
                        "label": "job-r1",
                        "repeatCount": 1,
                        "sourceScenarioContentSha256": "b" * 64,
                        "repositoryInventorySha256": "c" * 64,
                        "semanticHash": "a" * 64,
                    }
                ),
                encoding="utf-8",
            )
            frames = [
                arrivals,
                promise_frame(1000),
                promise_frame(900),
                promise_frame(1100),
            ]
            (bundle / "transcript-00.ndjson").write_text(
                "\n".join(envelope(frame) for frame in frames) + "\n",
                encoding="utf-8",
            )
            with mock.patch.object(MODULE, "verify_bundle"):
                result = MODULE.read_bundle(
                    bundle,
                    {
                        "jobId": "job-r1",
                        "scenarioContentSha256": "b" * 64,
                    },
                    pathlib.Path("verify.py"),
                    pathlib.Path("python.exe"),
                    {},
                )
        self.assertEqual(1, result["pickupEtaImprovementCount"])
        self.assertEqual(100, result["pickupEtaImprovementMs"])
        self.assertEqual(1, result["pickupEtaWorseningCount"])
        self.assertEqual(200, result["pickupEtaWorseningMs"])
        self.assertEqual(300, result["experiencedTotalMs"])

    def test_pareto_dominance_uses_only_service_and_burden(self):
        cells = [f"d20181112-s10-r{i}-w08" for i in range(1, 17)]
        arms = [
            "b1-ref",
            "c1-h6ref",
            "c1-freeze300",
            "c1-freeze600",
            "c1-ratchet",
            "c1-freeze300ratchet",
            "c1-nopickuplock",
            "c1-budget60",
            "c1-budget120",
            "c1-nobudget",
        ]
        jobs = [
            {
                "jobId": f"job-{cell}-{arm}",
                "cellId": cell,
                "armId": arm,
            }
            for cell in cells
            for arm in arms
        ]
        receipt = {
            "freezeId": "wp14-development-ablation-v1",
            "claimBoundary": [
                "developmentExploratoryOnlyNotConfirmatory",
                "frozenPanelsNeverUsedForTuningOrSelection",
                "reportPairedFrontierNotAPostOutcomeScalar",
                "mustNotReinterpretOrRescueH6",
            ],
            "design": {
                "jobs": jobs,
                "arms": [
                    {"armId": arm, "factorLevel": f"level-{arm}"}
                    for arm in arms
                ],
                "arrivalsPerArm": 1728,
            },
        }

        def measured(bundle, job, verifier, python, environment):
            del job, verifier, python, environment
            name = bundle.name
            if name.endswith("b1-ref"):
                return observation(108, 2000)
            if name.endswith("c1-freeze300"):
                return observation(100, 900)
            if name.endswith("c1-h6ref"):
                return observation(100, 1000)
            return observation(99, 1100)

        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            for job in jobs:
                (root / job["jobId"]).mkdir()
            with mock.patch.object(MODULE, "read_bundle", side_effect=measured):
                report = MODULE.analyze(
                    receipt,
                    root,
                    pathlib.Path("verify.py"),
                    pathlib.Path("python.exe"),
                )
        report["freezeReceiptSha256"] = "2" * 64
        report["sourceIdentity"] = {
            "analyzerSourceSha256": "3" * 64,
            "independentVerifierSourceSha256": "4" * 64,
        }
        schema = json.loads(
            (
                ROOT / "benchmarks/schemas/wp14/v1/frontier-report.schema.json"
            ).read_text(encoding="utf-8")
        )
        jsonschema.Draft202012Validator(schema).validate(report)
        points = {point["armId"]: point for point in report["points"]}
        self.assertTrue(points["b1-ref"]["isParetoEfficient"])
        self.assertTrue(points["c1-freeze300"]["isParetoEfficient"])
        self.assertFalse(points["c1-h6ref"]["isParetoEfficient"])
        self.assertIn(
            "c1-freeze300", points["c1-h6ref"]["dominatedByArmIds"]
        )
        self.assertIsInstance(
            points["c1-h6ref"]["completionRatePartsPerMillionFloor"], int
        )
        self.assertIsInstance(
            points["c1-h6ref"]["medianCellDeltaVersusBaselineTimesTwo"], int
        )

    def test_frontier_schema_is_strict_draft_2020_12(self):
        schema = json.loads(
            (
                ROOT / "benchmarks/schemas/wp14/v1/frontier-report.schema.json"
            ).read_text(encoding="utf-8")
        )
        jsonschema.Draft202012Validator.check_schema(schema)
        self.assertFalse(schema["additionalProperties"])
        self.assertEqual(
            "weakOnBothAxesAndStrictOnAtLeastOne",
            schema["properties"]["design"]["properties"]["dominanceRule"][
                "const"
            ],
        )


if __name__ == "__main__":
    unittest.main()
