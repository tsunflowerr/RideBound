import copy
import importlib.util
import pathlib
import tempfile
import unittest
from unittest import mock


ROOT = pathlib.Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location(
    "wp13_behavioral_comparator",
    ROOT / "wp13_behavioral_comparator.py",
)
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


def _action(decision_type, **payload):
    return {"decisionType": decision_type, "payload": payload}


def _decision(actions, *, epoch=2, solver_status="succeeded", request_id="req-1"):
    observed = {
        "epochId": epoch,
        "simTimeMs": epoch * 1_000,
        "events": [
            {
                "eventType": "requestArrived",
                "payload": {"request": {"requestId": request_id}},
            }
        ],
    }
    operational = {"solverStatus": solver_status, "actions": actions}
    return {
        "epochId": epoch,
        "simTimeMs": epoch * 1_000,
        "observedInputProjection": observed,
        "operationalDecisionProjection": operational,
        "wireDecisionProjection": copy.deepcopy(operational),
        "eventTypes": ["requestArrived"],
        "actionTypes": [action["decisionType"] for action in actions],
    }


def _accepted(vehicle_id="veh-1"):
    return _action("requestAccepted", requestId="req-1", vehicleId=vehicle_id)


def _rejected(reason="NO_FEASIBLE_INSERTION"):
    return _action("requestRejected", requestId="req-1", reasonCode=reason)


def _classify(b1_actions, c1_actions, *, b1_status="succeeded", c1_status="succeeded"):
    return MODULE._classify_actions(
        _decision(b1_actions, solver_status=b1_status),
        _decision(c1_actions, solver_status=c1_status),
        ["req-1"],
    )


def _evidence(decision):
    return {
        "epochId": decision["epochId"],
        "simTimeMs": decision["simTimeMs"],
        "observedInputProjectionSha256": MODULE.inventory._projection_hash(
            decision["observedInputProjection"]
        ),
        "operationalDecisionProjectionSha256": MODULE.inventory._projection_hash(
            decision["operationalDecisionProjection"]
        ),
        "wireDecisionProjectionSha256": MODULE.inventory._projection_hash(
            decision["wireDecisionProjection"]
        ),
        "eventTypes": decision["eventTypes"],
        "actionTypes": decision["actionTypes"],
    }


class ActionClassificationTests(unittest.TestCase):
    def test_accept_to_reject_is_lower_immediate_acceptance(self):
        result = _classify([_accepted()], [_rejected()])

        self.assertEqual("requestDispositionDifference", result["primaryDifferenceClass"])
        self.assertEqual(
            "c1LowerImmediateAcceptance",
            result["immediateRequestComparison"]["acceptanceRelation"],
        )
        self.assertEqual(-1, result["immediateRequestComparison"]["acceptedCountDeltaC1MinusB1"])
        comparison = result["immediateRequestComparison"]["requests"][0]
        self.assertEqual("veh-1", comparison["b1VehicleId"])
        self.assertNotIn("c1VehicleId", comparison)
        self.assertNotIn(None, comparison.values())

    def test_different_accepted_vehicle_is_assignment_difference(self):
        result = _classify([_accepted("veh-1")], [_accepted("veh-2")])

        self.assertEqual(
            "acceptedVehicleAssignmentDifference",
            result["primaryDifferenceClass"],
        )
        self.assertEqual(
            "equalImmediateAcceptance",
            result["immediateRequestComparison"]["acceptanceRelation"],
        )
        self.assertNotIn("requestActionPayloadDifference", result["differenceClasses"])

    def test_same_outcome_and_vehicle_can_isolate_plan_difference(self):
        b1 = [
            _accepted(),
            _action("vehiclePlanUpdated", vehicleId="veh-1", route={"stops": [1]}),
        ]
        c1 = [
            _accepted(),
            _action("vehiclePlanUpdated", vehicleId="veh-1", route={"stops": [2]}),
        ]

        result = _classify(b1, c1)

        self.assertEqual(["vehiclePlanDifference"], result["differenceClasses"])

    def test_request_reason_difference_is_request_payload_difference(self):
        result = _classify([_rejected("A")], [_rejected("B")])

        self.assertEqual(
            ["requestActionPayloadDifference"],
            result["differenceClasses"],
        )

    def test_publication_solver_and_other_differences_keep_typed_classes(self):
        cases = [
            (
                [_rejected(), _action("promisePublished", promise={"version": 1})],
                [_rejected(), _action("promisePublished", promise={"version": 2})],
                "promiseProjectionDifference",
                "succeeded",
                "succeeded",
            ),
            ([_rejected()], [_rejected()], "solverStatusDifference", "ok", "timeout"),
            (
                [_rejected(), _action("nativeFleetPyOptimization", code="A")],
                [_rejected(), _action("nativeFleetPyOptimization", code="B")],
                "otherActionDifference",
                "succeeded",
                "succeeded",
            ),
        ]
        for b1, c1, expected, b1_status, c1_status in cases:
            with self.subTest(expected=expected):
                result = _classify(
                    b1,
                    c1,
                    b1_status=b1_status,
                    c1_status=c1_status,
                )
                self.assertEqual([expected], result["differenceClasses"])
                self.assertEqual(
                    "equalImmediateAcceptance",
                    result["immediateRequestComparison"]["acceptanceRelation"],
                )

    def test_duplicate_or_missing_request_outcome_fails_closed(self):
        with self.assertRaisesRegex(RuntimeError, "missing or duplicated"):
            _classify([_accepted(), _accepted()], [_rejected()])

        with self.assertRaisesRegex(RuntimeError, "bijectively cover"):
            _classify([_accepted()], [])

    def test_unclassifiable_divergence_fails_closed(self):
        with self.assertRaisesRegex(RuntimeError, "no classifiable difference"):
            _classify([_accepted()], [_accepted()])


class EvidenceBindingTests(unittest.TestCase):
    def test_target_binding_rejects_epoch_or_hash_mutation(self):
        decision = _decision([_accepted()])
        record = {
            "panelId": "A",
            "unitId": "unit-1",
            "firstDivergence": {"b1Evidence": _evidence(decision)},
        }
        MODULE._bind_target(record, "b1", decision)

        mutated = copy.deepcopy(decision)
        mutated["epochId"] += 1
        with self.assertRaisesRegex(RuntimeError, "target binding differs"):
            MODULE._bind_target(record, "b1", mutated)

        mutated = copy.deepcopy(decision)
        mutated["operationalDecisionProjection"]["actions"][0]["payload"][
            "vehicleId"
        ] = "veh-mutated"
        with self.assertRaisesRegex(RuntimeError, "target binding differs"):
            MODULE._bind_target(record, "b1", mutated)

    def test_target_scan_consumes_transcript_tail(self):
        def records(_bundle, _coverage):
            yield _decision([_accepted()], epoch=2)
            raise RuntimeError("tail receipt differs")

        with mock.patch.object(MODULE.inventory, "_decision_records", records):
            with self.assertRaisesRegex(RuntimeError, "tail receipt differs"):
                MODULE._target_decision({"label": "bundle"}, 2, {})

    def test_bundle_arm_unit_and_scenario_identity_are_bound(self):
        b1 = _decision([_accepted("veh-1")])
        c1 = _decision([_accepted("veh-2")])
        scenario_hash = "a" * 64
        record = {
            "panelId": "A",
            "unitId": "unit-1",
            "b1Label": "b1-label",
            "c1Label": "c1-label",
            "sourceScenarioContentSha256": scenario_hash,
            "firstDivergence": {
                "classification": "operationalDecisionDivergenceOnEqualObservedInput",
                "observedInputRelation": "equal",
                "b1Evidence": _evidence(b1),
                "c1Evidence": _evidence(c1),
            },
        }
        bundles = {
            "b1-label": {
                "label": "b1-label",
                "identity": {"arm": "c1", "unit": "unit-1"},
                "sourceScenarioContentSha256": scenario_hash,
            },
            "c1-label": {
                "label": "c1-label",
                "identity": {"arm": "c1", "unit": "unit-1"},
                "sourceScenarioContentSha256": scenario_hash,
            },
        }

        with self.assertRaisesRegex(RuntimeError, "bundle identity differs"):
            MODULE._compare_record(record, bundles, {})

    def test_mutated_record_set_identity_fails_before_json_use(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = pathlib.Path(temporary) / "record-set.json"
            path.write_bytes(b"{}\n")

            with self.assertRaisesRegex(RuntimeError, "record-set identity differs"):
                MODULE._read_record_set(path)

    def test_declared_inventory_report_identity_is_exact(self):
        value = {
            "contractIdentity": {
                "generatorSourceSha256": MODULE._RECORD_GENERATOR_SHA256,
                "recordSetSchemaSha256": MODULE._RECORD_SCHEMA_SHA256,
            },
            "sourceInventoryReport": copy.deepcopy(MODULE._SOURCE_INVENTORY_REPORT),
        }
        with mock.patch.object(MODULE.jsonschema, "Draft202012Validator"):
            MODULE._validate_record_set_identity(value)
            value["sourceInventoryReport"]["sha256"] = "0" * 64
            with self.assertRaisesRegex(RuntimeError, "inventory report identity"):
                MODULE._validate_record_set_identity(value)

    def test_missing_panel_root_and_output_inside_frozen_root_fail_closed(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = pathlib.Path(temporary)
            with self.assertRaisesRegex(RuntimeError, "no evidence bundles"):
                MODULE._prepare_panel("A", root)
            with self.assertRaisesRegex(RuntimeError, "outside immutable H6 roots"):
                MODULE._require_output_outside_inputs(
                    root / "nested" / "report.json",
                    root.parent / "record-set.json",
                    {"A": root},
                )


if __name__ == "__main__":
    unittest.main()
