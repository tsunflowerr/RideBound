import importlib.util
import json
import pathlib
import tempfile
import unittest


_ROOT = pathlib.Path(__file__).parents[1]
_SPEC = importlib.util.spec_from_file_location(
    "wp9_run_matrix", _ROOT / "wp9_run_matrix.py"
)
_MODULE = importlib.util.module_from_spec(_SPEC)
_SPEC.loader.exec_module(_MODULE)


class MatrixPlanTests(unittest.TestCase):
    def test_frozen_plan_has_exact_primary_and_robustness_denominators(self):
        repository = pathlib.Path(__file__).parents[3]
        plan = _MODULE._load_plan(
            repository / "benchmarks/scenarios/wp9-confirmatory/execution-plan-v1.json"
        )
        _MODULE._validate_frozen_design(plan)

        primary = [job for job in plan["jobs"] if job["phase"] == "primary"]
        robustness = [job for job in plan["jobs"] if job["phase"] == "robustness"]
        self.assertEqual(40, len(primary))
        self.assertEqual(20, len(robustness))
        self.assertEqual(20, len({job["cellId"] for job in primary}))
        self.assertEqual({7}, {job["masterSeed"] for job in primary})

    def test_duplicate_job_id_and_boolean_seed_fail_closed(self):
        job = {
            "jobId": "job",
            "phase": "primary",
            "cellId": "cell",
            "armId": "b1",
            "wp4Config": "config",
            "commitmentConfig": "commitment",
            "driver": "driver",
            "masterSeed": True,
        }
        plan = {"schemaVersion": "1.0.0", "planId": "plan", "jobs": [job, job]}
        with tempfile.TemporaryDirectory() as directory:
            path = pathlib.Path(directory) / "plan.json"
            path.write_text(json.dumps(plan), encoding="utf-8")
            with self.assertRaisesRegex(RuntimeError, "duplicate"):
                _MODULE._load_plan(path)

    def test_path_traversal_cell_and_job_identifiers_fail_closed(self):
        job = {
            "jobId": "../../outside",
            "phase": "primary",
            "cellId": "../../outside",
            "armId": "b1",
            "wp4Config": "config",
            "commitmentConfig": "commitment",
            "driver": "driver",
            "masterSeed": 7,
        }
        plan = {"schemaVersion": "1.0.0", "planId": "plan", "jobs": [job]}
        with tempfile.TemporaryDirectory() as directory:
            path = pathlib.Path(directory) / "plan.json"
            path.write_text(json.dumps(plan), encoding="utf-8")

            with self.assertRaisesRegex(RuntimeError, "safe frozen identifier"):
                _MODULE._load_plan(path)

    def test_frozen_design_rejects_arm_configuration_swap(self):
        repository = pathlib.Path(__file__).parents[3]
        plan = _MODULE._load_plan(
            repository / "benchmarks/scenarios/wp9-confirmatory/execution-plan-v1.json"
        )
        altered = json.loads(json.dumps(plan))
        altered["jobs"][0]["wp4Config"] = (
            "benchmarks/configurations/"
            "wp9-fleetpy-ridebound-hard-vector-audited-v1.json"
        )

        with self.assertRaisesRegex(RuntimeError, "job binding"):
            _MODULE._validate_frozen_design(altered)

    def test_smoke_selection_is_an_exact_subset_of_the_frozen_plan(self):
        repository = pathlib.Path(__file__).parents[3]
        plan = _MODULE._load_plan(
            repository / "benchmarks/scenarios/wp9-confirmatory/execution-plan-v1.json"
        )
        selected = _MODULE._select_jobs(
            plan,
            [
                "p-d20181114-s10-r1-b1-tight-s7",
                "p-d20181114-s10-r1-c1-tight-s7",
            ],
        )

        self.assertEqual(2, len(selected))
        with self.assertRaisesRegex(RuntimeError, "absent from frozen plan"):
            _MODULE._select_jobs(plan, ["p-d20990101-s10-r1-b1-tight-s7"])

    def test_output_binding_rejects_valid_bundle_for_a_different_job(self):
        with tempfile.TemporaryDirectory() as directory:
            output = pathlib.Path(directory)
            (output / "summary.json").write_text(
                json.dumps(
                    {
                        "label": "other-job",
                        "sourceScenarioContentSha256": "a" * 64,
                    }
                ),
                encoding="utf-8",
            )

            with self.assertRaisesRegex(RuntimeError, "output label differs"):
                _MODULE._validate_output_binding(
                    output,
                    "expected-job",
                    "a" * 64,
                    "b" * 64,
                )

    def test_matrix_and_preflight_inventory_algorithms_agree(self):
        preflight_spec = importlib.util.spec_from_file_location(
            "wp9_preflight_inventory",
            _ROOT / "actual_fleetpy_medium_preflight.py",
        )
        preflight = importlib.util.module_from_spec(preflight_spec)
        preflight_spec.loader.exec_module(preflight)
        repository = pathlib.Path(__file__).parents[3].resolve()

        self.assertEqual(
            _MODULE._repository_inventory_sha256(repository),
            preflight._repository_inventory_sha256(repository),
        )


if __name__ == "__main__":
    unittest.main()
