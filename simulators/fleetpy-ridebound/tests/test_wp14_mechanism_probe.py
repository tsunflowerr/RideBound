import base64
import importlib.util
import json
import pathlib
import tempfile
import unittest


TEST_ROOT = pathlib.Path(__file__).resolve().parent
ADAPTER_ROOT = TEST_ROOT.parent
PROBE_PATH = ADAPTER_ROOT / "wp14_mechanism_probe.py"
_SPEC = importlib.util.spec_from_file_location("wp14_mechanism_probe", PROBE_PATH)
probe = importlib.util.module_from_spec(_SPEC)
_SPEC.loader.exec_module(probe)


def _frame(payload):
    body = json.dumps(
        {"messageType": "decision", "payload": payload, "schemaVersion": "1.0.0"}
    ).encode("utf-8")
    return {
        "direction": "runnerToAdapter",
        "frameBase64": base64.b64encode(body).decode("ascii"),
        "schemaVersion": "1.0.0",
    }


def _candidate(candidate_id, vehicle_id, requests, contributions, eligible=True,
               is_no_op=False):
    value = {
        "candidateId": candidate_id,
        "vehicleId": vehicle_id,
        "newRequestIds": list(requests),
        "isNoOp": is_no_op,
        "policyEligibility": "eligible" if eligible else "pruned",
    }
    if eligible:
        value["objectiveContributions"] = list(contributions)
    return value


def _levels(names):
    return [
        {
            "levelIndex": index,
            "name": name,
            "sense": "minimize",
            "aggregation": "sum",
        }
        for index, name in enumerate(names)
    ]


def _promise(request_id, pickup, drop, decision_delta, exogenous_delta,
             budget_after):
    zero = {
        "pickupEtaTotalMs": 0,
        "dropEtaTotalMs": 0,
        "materialEtaRevisionCount": 0,
        "vehicleSwitchCount": 0,
        "pickupStopRelocationMm": 0,
        "pickupStopSwitchCount": 0,
        "dropStopRelocationMm": 0,
        "dropStopSwitchCount": 0,
        "incumbentOrderInversionCount": 0,
        "prePickupInsertedStopCount": 0,
    }
    return {
        "decisionType": "promisePublished",
        "payload": {
            "promise": {
                "requestId": request_id,
                "pickupEtaMs": pickup,
                "dropEtaMs": drop,
            },
            "decisionDelta": zero | decision_delta,
            "exogenousDelta": zero | exogenous_delta,
            "budgetAfter": zero | budget_after,
        },
    }


def _write(path, payloads):
    with path.open("w", encoding="utf-8", newline="\n") as handle:
        for payload in payloads:
            handle.write(json.dumps(_frame(payload)) + "\n")


class Wp14MechanismProbeTests(unittest.TestCase):
    def setUp(self):
        self._directory = tempfile.TemporaryDirectory()
        root = pathlib.Path(self._directory.name) / "p-cell-c1"
        root.mkdir(parents=True)
        self.transcript = root / "transcript-00.ndjson"

    def tearDown(self):
        self._directory.cleanup()

    def test_only_commitment_codes_are_attributed_to_the_gate(self):
        """Physical generation prunes belong to both arms and must not be charged."""
        payload = {
            "actions": [],
            "solver": {
                "executionEvidence": {
                    "prunedCandidates": [
                        {
                            "candidateId": "c-physical",
                            "code": "MAX_RIDE_TIME",
                            "commitmentWitnesses": [],
                        },
                        {
                            "candidateId": "c-gate",
                            "code": "COMMITMENT_BUDGET_EXCEEDED",
                            "commitmentWitnesses": [
                                {"dimension": "drop_eta_total_ms", "rule": None}
                            ],
                        },
                    ],
                    "candidatePortfolio": {
                        "selectionProblem": {
                            "objectiveLevels": _levels(["operational-cost"])
                        },
                        "candidates": [
                            _candidate("c-noop", "v1", [], [0], is_no_op=True),
                            _candidate(
                                "c-physical", "v1", ["r1"], None, eligible=False
                            ),
                            _candidate("c-gate", "v1", ["r1"], None, eligible=False),
                        ],
                    },
                }
            },
        }
        _write(self.transcript, [payload])
        result = probe.analyze([self.transcript])
        self.assertEqual(
            result["gate"]["commitmentPrunedByCode"],
            {"COMMITMENT_BUDGET_EXCEEDED": 1},
        )
        self.assertEqual(result["gate"]["immediateAcceptanceBlocks"], 1)
        self.assertEqual(
            result["gate"]["immediateBlocksByDimension"], {"drop_eta_total_ms": 1}
        )
        self.assertEqual(result["gate"]["vehicleChoiceSetsEmptiedByGate"], 1)

    def test_a_request_still_reachable_elsewhere_is_not_a_block(self):
        payload = {
            "actions": [],
            "solver": {
                "executionEvidence": {
                    "prunedCandidates": [
                        {
                            "candidateId": "c-gate",
                            "code": "COMMITMENT_PHASE_LOCK",
                            "commitmentWitnesses": [
                                {"dimension": "pickup_eta_ms",
                                 "rule": "final_confirmation"}
                            ],
                        }
                    ],
                    "candidatePortfolio": {
                        "selectionProblem": {
                            "objectiveLevels": _levels(["operational-cost"])
                        },
                        "candidates": [
                            _candidate("c-gate", "v1", ["r1"], None, eligible=False),
                            _candidate("c-alt", "v2", ["r1"], [7]),
                        ],
                    },
                }
            },
        }
        _write(self.transcript, [payload])
        result = probe.analyze([self.transcript])
        self.assertEqual(result["gate"]["commitmentPrunedByCode"],
                         {"COMMITMENT_PHASE_LOCK": 1})
        self.assertEqual(result["gate"]["immediateAcceptanceBlocks"], 0)

    def test_a_level_is_degenerate_only_when_every_vehicle_agrees(self):
        payload = {
            "actions": [],
            "solver": {
                "executionEvidence": {
                    "prunedCandidates": [],
                    "candidatePortfolio": {
                        "selectionProblem": {
                            "objectiveLevels": _levels(
                                ["constant-level", "deciding-level"]
                            )
                        },
                        "candidates": [
                            _candidate("a", "v1", [], [5, 1]),
                            _candidate("b", "v1", [], [5, 2]),
                            _candidate("c", "v2", [], [9, 3]),
                        ],
                    },
                }
            },
        }
        _write(self.transcript, [payload])
        result = probe.analyze([self.transcript])
        families = result["objective"]["byFamily"]
        self.assertEqual(families["constant-level"], {"built": 1, "degenerate": 1})
        self.assertEqual(families["deciding-level"], {"built": 1, "degenerate": 0})
        self.assertEqual(result["objective"]["lexicographicLevelsBuilt"], 2)
        self.assertEqual(result["objective"]["lexicographicLevelsDegenerate"], 1)

    def test_experienced_movement_is_separated_from_attributed_burden(self):
        base = {
            "solver": {
                "executionEvidence": {
                    "prunedCandidates": [],
                    "candidatePortfolio": {
                        "selectionProblem": {
                            "objectiveLevels": _levels(["operational-cost"])
                        },
                        "candidates": [_candidate("a", "v1", [], [0])],
                    },
                }
            }
        }
        first = dict(base)
        first["actions"] = [
            _promise("r1", 1_000, 5_000, {}, {}, {})
        ]
        second = dict(base)
        second["actions"] = [
            _promise(
                "r1",
                1_400,
                5_900,
                {"dropEtaTotalMs": 500},
                {"pickupEtaTotalMs": 400, "dropEtaTotalMs": 400},
                {"dropEtaTotalMs": 500},
            )
        ]
        _write(self.transcript, [first, second])
        result = probe.analyze([self.transcript])
        movement = result["promiseMovement"]
        self.assertEqual(movement["attributedTotalMs"], 500)
        self.assertEqual(movement["experiencedTotalMs"], 400 + 900)
        self.assertEqual(movement["requestsWithOpenPromise"], 1)
        self.assertEqual(movement["requestsWhosePromiseEverMoved"], 1)
        self.assertEqual(movement["requestsChargedAnyDecisionInducedBurden"], 1)
        self.assertEqual(result["consumptionShape"]["percentilesMs"]["p100"], 500)

    def test_a_bundle_without_v12_evidence_fails_closed(self):
        _write(self.transcript, [{"actions": [], "solver": {}}])
        with self.assertRaises(probe.ProbeError):
            probe.analyze([self.transcript])

    def test_output_inside_a_raw_root_is_refused(self):
        inside = self.transcript.parent / "probe.json"
        with self.assertRaises(probe.ProbeError):
            probe.require_output_outside_inputs(inside, [self.transcript])

    def test_exclusive_create_refuses_an_existing_output(self):
        target = pathlib.Path(self._directory.name) / "out.json"
        probe.write_exclusive(target, b"{}\n")
        with self.assertRaises(OSError):
            probe.write_exclusive(target, b"{}\n")


if __name__ == "__main__":
    unittest.main()
