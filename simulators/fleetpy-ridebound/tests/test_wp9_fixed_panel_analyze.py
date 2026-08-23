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
        # The job carries the execution-plan token; the caller passes the arm's
        # declared preregistered identity. A C1 bundle offered as the baseline
        # must not validate.
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
            _MODULE._validate_primary_job(
                job, "cell", "b1-rolling-cost", job["jobId"]
            )

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


class FrozenManifestBindingTests(unittest.TestCase):
    """The unit tests used synthetic short arm tokens, so they never noticed that
    the frozen manifest names arms by their declared preregistered identity.
    Every cell raised "primary job binding differs" and the confirmatory analysis
    could not run at all.  These bind against the real frozen artifacts."""

    _REPOSITORY = _ROOT.parents[1]
    _SCENARIOS = _REPOSITORY / "benchmarks/scenarios/wp9-confirmatory"

    def _manifest(self):
        return json.loads(
            (self._SCENARIOS / "analysis-manifest-v1.json").read_text(
                encoding="utf-8"
            )
        )

    def _plan_jobs(self):
        plan = json.loads(
            (self._SCENARIOS / "execution-plan-v1.json").read_text(
                encoding="utf-8"
            )
        )
        return {job["jobId"]: job for job in plan["jobs"]}, plan

    def test_every_frozen_cell_binds_to_its_frozen_execution_plan_job(self):
        manifest = self._manifest()
        jobs, _ = self._plan_jobs()
        for cell in manifest["cells"]:
            _MODULE._validate_primary_job(
                jobs[cell["baselineBundle"]],
                cell["cellId"],
                manifest["baselineArmId"],
                cell["baselineBundle"],
            )
            _MODULE._validate_primary_job(
                jobs[cell["treatmentBundle"]],
                cell["cellId"],
                manifest["treatmentArmId"],
                cell["treatmentBundle"],
            )

    def test_frozen_manifest_is_accepted_and_covers_the_whole_primary_panel(self):
        manifest = _MODULE._read_manifest(
            self._SCENARIOS / "analysis-manifest-v1.json"
        )
        _, plan = self._plan_jobs()
        planned = {
            job["cellId"] for job in plan["jobs"] if job["phase"] == "primary"
        }
        self.assertEqual(20, len(planned))
        self.assertEqual(
            planned, {cell["cellId"] for cell in manifest["cells"]}
        )

    def test_unknown_declared_arm_is_rejected_rather_than_spliced(self):
        jobs, _ = self._plan_jobs()
        with self.assertRaisesRegex(RuntimeError, "not preregistered"):
            _MODULE._validate_primary_job(
                jobs["p-d20181114-s10-r1-b1-tight-s7"],
                "d20181114-s10-r1",
                "b1",
                "p-d20181114-s10-r1-b1-tight-s7",
            )

    def test_swapped_arm_orientation_in_the_manifest_is_rejected(self):
        manifest = self._manifest()
        manifest["baselineArmId"], manifest["treatmentArmId"] = (
            manifest["treatmentArmId"],
            manifest["baselineArmId"],
        )
        with tempfile.TemporaryDirectory() as directory:
            path = pathlib.Path(directory) / "manifest.json"
            path.write_text(json.dumps(manifest), encoding="utf-8")
            with self.assertRaisesRegex(RuntimeError, "arm orientation"):
                _MODULE._read_manifest(path)

    def test_cell_reusing_one_bundle_for_both_arms_is_rejected(self):
        manifest = self._manifest()
        manifest["cells"][0]["treatmentBundle"] = manifest["cells"][0][
            "baselineBundle"
        ]
        with tempfile.TemporaryDirectory() as directory:
            path = pathlib.Path(directory) / "manifest.json"
            path.write_text(json.dumps(manifest), encoding="utf-8")
            with self.assertRaisesRegex(RuntimeError, "invalid or duplicate"):
                _MODULE._read_manifest(path)


class CapacityPanelBindingTests(unittest.TestCase):
    """WP8-011d adds a second capacity panel with its own derivative tree,
    drivers and execution plan. Panel A must stay byte-for-byte frozen, and a
    bundle or plan from one panel must never validate as the other."""

    _SCENARIOS = _ROOT.parents[1] / "benchmarks/scenarios/wp9-confirmatory"

    def _matrix(self):
        specification = importlib.util.spec_from_file_location(
            "wp9_run_matrix_for_panel_tests", _ROOT / "wp9_run_matrix.py"
        )
        module = importlib.util.module_from_spec(specification)
        specification.loader.exec_module(module)
        return module

    def test_panel_b_plan_and_manifest_bind_end_to_end(self):
        matrix = self._matrix()
        plan = matrix._load_plan(self._SCENARIOS / "execution-plan-panel-b-v1.json")
        matrix._validate_frozen_design(plan, "b")
        jobs = {job["jobId"]: job for job in plan["jobs"]}
        manifest = _MODULE._read_manifest(
            self._SCENARIOS / "analysis-manifest-panel-b-v1.json"
        )
        self.assertEqual(20, len(manifest["cells"]))
        self.assertEqual(40, len(plan["jobs"]))
        for cell in manifest["cells"]:
            _MODULE._validate_primary_job(
                jobs[cell["baselineBundle"]],
                cell["cellId"],
                manifest["baselineArmId"],
                cell["baselineBundle"],
                "b",
            )

    def test_panel_a_plan_is_rejected_as_panel_b(self):
        matrix = self._matrix()
        plan = matrix._load_plan(self._SCENARIOS / "execution-plan-v1.json")
        with self.assertRaisesRegex(RuntimeError, "denominators differ"):
            matrix._validate_frozen_design(plan, "b")

    def test_panel_a_job_is_rejected_when_analysed_as_panel_b(self):
        matrix = self._matrix()
        plan = matrix._load_plan(self._SCENARIOS / "execution-plan-v1.json")
        jobs = {job["jobId"]: job for job in plan["jobs"]}
        with self.assertRaisesRegex(RuntimeError, "job binding"):
            _MODULE._validate_primary_job(
                jobs["p-d20181114-s10-r1-b1-tight-s7"],
                "d20181114-s10-r1",
                "b1-rolling-cost",
                "p-d20181114-s10-r1-b1-tight-s7",
                "b",
            )

    def test_both_panels_declare_the_same_twenty_cells(self):
        a = _MODULE._read_manifest(self._SCENARIOS / "analysis-manifest-v1.json")
        b = _MODULE._read_manifest(
            self._SCENARIOS / "analysis-manifest-panel-b-v1.json"
        )
        self.assertEqual(
            {cell["cellId"] for cell in a["cells"]},
            {cell["cellId"] for cell in b["cells"]},
        )
        self.assertNotEqual(a["panelId"], b["panelId"])


if __name__ == "__main__":
    unittest.main()
