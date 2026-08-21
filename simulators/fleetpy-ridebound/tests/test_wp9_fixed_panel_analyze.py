import importlib.util
import json
import pathlib
import tempfile
import unittest


_ROOT = pathlib.Path(__file__).parents[1]
_SPEC = importlib.util.spec_from_file_location(
    "wp9_fixed_panel_analyze",
    _ROOT / "wp9_fixed_panel_analyze.py",
)
_MODULE = importlib.util.module_from_spec(_SPEC)
_SPEC.loader.exec_module(_MODULE)


def _observation(arrived, completed, burden, disruptive=0, pickup=0):
    return {
        "arrived": arrived,
        "completed": completed,
        "decisionDropEtaTotalMs": burden - pickup,
        "decisionPickupEtaTotalMs": pickup,
        "disruptiveDecisionCount": disruptive,
        "totalDecisionInducedBurdenMs": burden,
    }


class FixedPanelAnalysisTests(unittest.TestCase):
    def test_exact_service_margin_uses_strict_one_percent_boundary(self):
        baseline = _observation(100, 90, 1000, 10)
        at_boundary = _observation(100, 89, 100, 1)
        above_boundary = _observation(100, 90, 100, 1)

        failed = _MODULE._panel_result(
            "panel", [("cell", baseline, at_boundary)], 100
        )
        passed = _MODULE._panel_result(
            "panel", [("cell", baseline, above_boundary)], 100
        )

        self.assertFalse(failed["aggregate"]["serviceGatePassed"])
        self.assertEqual("gateFailed", failed["status"])
        self.assertTrue(passed["aggregate"]["serviceGatePassed"])
        self.assertEqual("pass", passed["status"])

    def test_pair_with_different_arrival_denominator_is_rejected(self):
        with self.assertRaisesRegex(RuntimeError, "arrival counts"):
            _MODULE._panel_result(
                "panel",
                [("cell", _observation(100, 90, 10), _observation(99, 90, 9))],
                100,
            )

    def test_burden_improvement_does_not_rescue_failed_service_gate(self):
        result = _MODULE._panel_result(
            "panel",
            [("cell", _observation(100, 100, 1000), _observation(100, 98, 0))],
            100,
        )

        self.assertTrue(result["aggregate"]["burdenGatePassed"])
        self.assertFalse(result["aggregate"]["serviceGatePassed"])
        self.assertEqual("gateFailed", result["status"])

    def test_pickup_locked_and_drop_earned_reductions_are_exactly_decomposed(self):
        result = _MODULE._panel_result(
            "panel",
            [
                (
                    "cell",
                    _observation(100, 90, 1000, pickup=200),
                    _observation(100, 90, 300, pickup=0),
                )
            ],
            100,
        )

        decomposition = result["aggregate"]["burdenReductionDecomposition"]
        self.assertEqual(200, decomposition["pickupEtaDefinitionLockedComponentMs"])
        self.assertEqual(500, decomposition["dropEtaEarnedComponentMs"])
        self.assertEqual(700, decomposition["shareDenominatorMs"])

    def test_nonzero_treatment_pickup_delta_violates_lock_contract(self):
        with self.assertRaisesRegex(RuntimeError, "pickup-ETA lock"):
            _MODULE._panel_result(
                "panel",
                [
                    (
                        "cell",
                        _observation(100, 90, 1000, pickup=200),
                        _observation(100, 90, 300, pickup=1),
                    )
                ],
                100,
            )

    def test_swapped_primary_arm_binding_is_rejected(self):
        job = {
            "armId": "c1",
            "cellId": "cell",
            "commitmentConfig": (
                "benchmarks/configurations/wp8-drop-eta-budget-tight-v1.json"
            ),
            "driver": "benchmarks/scenarios/wp9-confirmatory/cell.driver.json",
            "jobId": "p-cell-c1-tight-s7",
            "masterSeed": 7,
            "phase": "primary",
            "wp4Config": (
                "benchmarks/configurations/"
                "wp9-fleetpy-ridebound-hard-vector-audited-v1.json"
            ),
        }

        with self.assertRaisesRegex(RuntimeError, "job binding"):
            _MODULE._validate_primary_job(job, "cell", "b1", job["jobId"])

    def test_valid_bundle_with_wrong_expected_label_is_rejected(self):
        class FakeVerifier:
            @staticmethod
            def verify_bundle(*_args, **_kwargs):
                return {
                    "behavioralProjectionHash": "b" * 64,
                    "repeatCount": 1,
                    "semanticHash": "s" * 64,
                }

        with tempfile.TemporaryDirectory() as directory:
            bundle = pathlib.Path(directory)
            (bundle / "summary.json").write_text(
                json.dumps(
                    {
                        "label": "actual-job",
                        "sourceScenarioContentSha256": "a" * 64,
                    }
                ),
                encoding="utf-8",
            )

            with self.assertRaisesRegex(RuntimeError, "bundle label differs"):
                _MODULE._read_observation(
                    bundle,
                    FakeVerifier(),
                    True,
                    "expected-job",
                    "a" * 64,
                )


if __name__ == "__main__":
    unittest.main()
