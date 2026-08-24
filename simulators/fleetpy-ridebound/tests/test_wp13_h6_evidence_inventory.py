import base64
import hashlib
import importlib.util
import json
import pathlib
import tempfile
import unittest


ROOT = pathlib.Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location(
    "wp13_h6_evidence_inventory",
    ROOT / "wp13_h6_evidence_inventory.py",
)
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


def _record(epoch, event_value="same", action_value="same", state_hash="a"):
    observed = {
        "epochId": epoch,
        "simTimeMs": epoch * 1000,
        "events": [
            {
                "eventType": "timerTick",
                "payload": {"value": event_value},
            }
        ],
    }
    decision = {
        "solverStatus": "completed",
        "actions": [
            {
                "decisionType": "vehiclePlanUpdated",
                "payload": {"value": action_value},
            }
        ],
    }
    return {
        "epochId": epoch,
        "simTimeMs": epoch * 1000,
        "observedInputProjection": observed,
        "wireDecisionProjection": decision,
        "operationalDecisionProjection": decision,
        "stateBeforeHash": state_hash,
        "eventTypes": ["timerTick"],
        "actionTypes": ["vehiclePlanUpdated"],
    }


class ProjectionTests(unittest.TestCase):
    def test_policy_hash_difference_is_not_false_divergence(self):
        b1 = [_record(1, state_hash="b1"), _record(2, state_hash="b1-2")]
        c1 = [_record(1, state_hash="c1"), _record(2, state_hash="c1-2")]

        result = MODULE._compare_record_iterators(iter(b1), iter(c1))

        self.assertEqual("noneObserved", result["firstDivergence"]["classification"])
        self.assertEqual(2, result["equalObservedDecisionEpochCountBeforeDivergence"])
        self.assertTrue(result["stateHashMismatchBeforeDivergence"])
        self.assertEqual(1, result["firstStateHashMismatchEpoch"])

    def test_decision_divergence_on_equal_observed_input(self):
        b1 = [_record(1), _record(2, action_value="accepted")]
        c1 = [_record(1), _record(2, action_value="rejected")]

        result = MODULE._compare_record_iterators(iter(b1), iter(c1))

        divergence = result["firstDivergence"]
        self.assertEqual(
            "operationalDecisionDivergenceOnEqualObservedInput",
            divergence["classification"],
        )
        self.assertTrue(divergence["observedInputEqual"])
        self.assertEqual(2, divergence["b1EpochId"])

    def test_observed_input_divergence_is_not_same_state_claim(self):
        b1 = [_record(1), _record(2, event_value="left")]
        c1 = [_record(1), _record(2, event_value="right")]

        result = MODULE._compare_record_iterators(iter(b1), iter(c1))

        divergence = result["firstDivergence"]
        self.assertEqual("observedInputDivergence", divergence["classification"])
        self.assertFalse(divergence["observedInputEqual"])

    def test_transcript_length_divergence_fails_closed(self):
        result = MODULE._compare_record_iterators(
            iter([_record(1), _record(2)]),
            iter([_record(1)]),
        )

        divergence = result["firstDivergence"]
        self.assertEqual("transcriptLengthDivergence", divergence["classification"])
        self.assertEqual(2, divergence["b1EpochId"])
        self.assertIsNone(divergence["c1EpochId"])

    def test_generated_identifiers_are_the_only_removed_fields(self):
        value = {
            "decisionType": "vehiclePlanUpdated",
            "payload": {
                "candidateId": "generated",
                "publicationId": "generated-publication",
                "plan": {"vehicleId": "v1", "candidateId": "nested"},
            },
        }

        projected = MODULE._strip_generated_action_fields(value)

        self.assertEqual(
            {
                "decisionType": "vehiclePlanUpdated",
                "payload": {"plan": {"vehicleId": "v1"}},
            },
            projected,
        )

    def test_generated_publication_order_is_not_operational_divergence(self):
        first = {
            "decisionType": "promisePublished",
            "payload": {
                "publicationId": "z-generated",
                "promise": {"requestId": "r1", "dropEtaMs": 20},
            },
        }
        second = {
            "decisionType": "promisePublished",
            "payload": {
                "publicationId": "a-generated",
                "promise": {"requestId": "r2", "dropEtaMs": 30},
            },
        }
        b1 = _record(1)
        c1 = _record(1)
        b1["wireDecisionProjection"] = {
            "solverStatus": "completed",
            "actions": MODULE._strip_generated_action_fields([first, second]),
        }
        c1["wireDecisionProjection"] = {
            "solverStatus": "completed",
            "actions": MODULE._strip_generated_action_fields([second, first]),
        }
        b1["operationalDecisionProjection"] = {
            "solverStatus": "completed",
            "actions": MODULE._operational_action_projection([first, second]),
        }
        c1["operationalDecisionProjection"] = {
            "solverStatus": "completed",
            "actions": MODULE._operational_action_projection([second, first]),
        }

        result = MODULE._compare_record_iterators(iter([b1]), iter([c1]))

        self.assertEqual("noneObserved", result["firstDivergence"]["classification"])
        self.assertTrue(result["wireOnlyDifferenceBeforeDivergence"])
        self.assertEqual(1, result["firstWireOnlyDifferenceEpoch"])

    def test_publication_normalization_rejects_non_suffix_actions(self):
        actions = [
            {
                "decisionType": "promisePublished",
                "payload": {"publicationId": "p1", "promise": {"requestId": "r1"}},
            },
            {"decisionType": "vehiclePlanUpdated", "payload": {"route": {}}},
        ]

        with self.assertRaisesRegex(RuntimeError, "atomic decision suffix"):
            MODULE._operational_action_projection(actions)


class ReceiptTests(unittest.TestCase):
    def test_canonical_object_accepts_exactly_one_terminal_lf(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = pathlib.Path(temporary) / "value.json"
            path.write_bytes(MODULE._canonical({"value": 1}) + b"\n")

            self.assertEqual({"value": 1}, MODULE._read_canonical_object(path))

            path.write_bytes(MODULE._canonical({"value": 1}) + b"\n\n")
            with self.assertRaisesRegex(RuntimeError, "not a canonical JSON object"):
                MODULE._read_canonical_object(path)

    def test_action_coverage_counts_recorded_route_not_unrecorded_plan(self):
        coverage = MODULE._new_coverage()
        actions = [
            {
                "decisionType": "vehiclePlanUpdated",
                "payload": {
                    "candidateId": "candidate-1",
                    "route": {"mutableSuffix": []},
                },
            }
        ]

        MODULE._update_action_coverage(coverage, actions, 1)

        self.assertEqual(1, coverage["selectedCandidateActionCount"])
        self.assertEqual(1, coverage["selectedRouteActionCount"])

    def test_decoded_frames_rejects_frame_hash_mutation(self):
        envelope = MODULE._canonical(
            {"messageType": "hello", "schemaVersion": "1.0.0"}
        )
        record = {
            "schemaVersion": "1.0.0",
            "ordinal": 1,
            "direction": "adapterToRunner",
            "frameLengthBytes": len(envelope),
            "frameSha256": "0" * 64,
            "frameBase64": base64.b64encode(envelope).decode("ascii"),
        }
        raw = MODULE._canonical(record) + b"\n"
        with tempfile.TemporaryDirectory() as temporary:
            directory = pathlib.Path(temporary)
            (directory / "transcript-00.ndjson").write_bytes(raw)
            bundle = {
                "directory": directory,
                "label": "mutation",
                "transcriptLengthBytes": len(raw),
                "transcriptSha256": hashlib.sha256(raw).hexdigest(),
            }

            with self.assertRaisesRegex(RuntimeError, "frame receipt differs"):
                list(MODULE._decoded_frames(bundle))

    def test_prepare_bundle_rejects_missing_declared_inventory(self):
        with tempfile.TemporaryDirectory() as temporary:
            directory = pathlib.Path(temporary) / "p-d20181114-s10-r1-b1-tight-s7"
            directory.mkdir()
            manifest = {
                "bundleType": "ridebound-wp7-actual-fleetpy-medium-v1",
                "files": [],
                "schemaVersion": "1.0.0",
            }
            (directory / "bundle-manifest.json").write_bytes(
                MODULE._canonical(manifest)
            )

            with self.assertRaisesRegex(RuntimeError, "bundle inventory differs"):
                MODULE._prepare_bundle(directory)

    def test_label_parser_rejects_unknown_policy(self):
        with self.assertRaisesRegex(RuntimeError, "unsupported H6 bundle label"):
            MODULE._parse_label("p-d20181114-s10-r1-x9-tight-s7")

    def test_frozen_panel_contract_binds_shape_and_inventory_receipts(self):
        self.assertEqual(60, MODULE._EXPECTED_PANELS["a"]["bundleCount"])
        self.assertEqual(20, MODULE._EXPECTED_PANELS["a"]["primaryPairCount"])
        self.assertEqual({"p", "r"}, MODULE._EXPECTED_PANELS["a"]["allowedKinds"])
        self.assertRegex(
            MODULE._EXPECTED_PANELS["a"]["declaredBundleInventorySha256"],
            r"^[0-9a-f]{64}$",
        )
        self.assertEqual(40, MODULE._EXPECTED_PANELS["b"]["bundleCount"])
        self.assertEqual({"pb"}, MODULE._EXPECTED_PANELS["b"]["allowedKinds"])

        bundles = [{"identity": {"kind": "p"}} for _ in range(60)]
        MODULE._validate_frozen_panel_contract(
            "a",
            bundles,
            20,
            20,
            MODULE._EXPECTED_PANELS["a"]["declaredBundleInventorySha256"],
        )
        with self.assertRaisesRegex(RuntimeError, "frozen bundle shape differs"):
            MODULE._validate_frozen_panel_contract(
                "a",
                bundles[:-1],
                20,
                20,
                MODULE._EXPECTED_PANELS["a"]["declaredBundleInventorySha256"],
            )
        with self.assertRaisesRegex(RuntimeError, "frozen bundle receipts differ"):
            MODULE._validate_frozen_panel_contract(
                "a",
                bundles,
                20,
                20,
                "0" * 64,
            )


class ReportBoundaryTests(unittest.TestCase):
    def test_analyze_declares_noncausal_read_only_boundary(self):
        original = MODULE.analyze_panel
        MODULE.analyze_panel = lambda panel_id, root: {"panelId": panel_id}
        try:
            report = MODULE.analyze([("A", pathlib.Path("unused"))])
        finally:
            MODULE.analyze_panel = original

        self.assertEqual(
            "trajectoryAssociatedNotCausal",
            report["claimBoundary"]["downstreamInterpretation"],
        )
        self.assertEqual("readOnlyImmutableInputs", report["claimBoundary"]["h6Artifacts"])
        self.assertIsNone(report["claimBoundary"]["confirmatoryGate"])
        self.assertRegex(
            report["toolIdentity"]["analyzerSourceSha256"],
            r"^[0-9a-f]{64}$",
        )
        self.assertRegex(
            report["toolIdentity"]["solverEvidenceVerifierSourceSha256"],
            r"^[0-9a-f]{64}$",
        )
        self.assertEqual(MODULE._canonical(report), MODULE._canonical(report))

    def test_panel_order_is_canonical_and_duplicate_is_rejected(self):
        original = MODULE.analyze_panel
        MODULE.analyze_panel = lambda panel_id, root: {"panelId": panel_id}
        try:
            report = MODULE.analyze(
                [("b", pathlib.Path("b")), ("a", pathlib.Path("a"))]
            )
            with self.assertRaisesRegex(RuntimeError, "duplicate panel identity"):
                MODULE.analyze(
                    [("A", pathlib.Path("a")), ("a", pathlib.Path("again"))]
                )
        finally:
            MODULE.analyze_panel = original

        self.assertEqual(["A", "B"], [panel["panelId"] for panel in report["panels"]])

    def test_output_cannot_write_inside_immutable_input(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = pathlib.Path(temporary).resolve()
            with self.assertRaisesRegex(RuntimeError, "outside immutable input root"):
                MODULE._require_output_outside_inputs(
                    root / "derived.json",
                    [("A", root)],
                )


if __name__ == "__main__":
    unittest.main()
