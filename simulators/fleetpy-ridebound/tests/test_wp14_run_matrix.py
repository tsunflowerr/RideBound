import importlib.util
import json
import pathlib
import subprocess
import tempfile
import unittest
from types import SimpleNamespace
from unittest import mock


ROOT = pathlib.Path(__file__).resolve().parents[3]
MODULE_PATH = ROOT / "simulators/fleetpy-ridebound/wp14_run_matrix.py"
SPEC = importlib.util.spec_from_file_location("wp14_matrix_under_test", MODULE_PATH)
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class Wp14RunMatrixTests(unittest.TestCase):
    def test_job_selection_rejects_duplicates_and_unknown_jobs(self):
        receipt = {
            "design": {
                "jobs": [
                    {"jobId": "one"},
                    {"jobId": "two"},
                ]
            }
        }
        self.assertEqual(
            ["two"],
            [job["jobId"] for job in MODULE.select_jobs(receipt, ["two"])],
        )
        with self.assertRaisesRegex(MODULE.MatrixError, "duplicated"):
            MODULE.select_jobs(receipt, ["one", "one"])
        with self.assertRaisesRegex(MODULE.MatrixError, "absent"):
            MODULE.select_jobs(receipt, ["missing"])

    def test_execution_must_match_roots_and_parallel_ceiling(self):
        output = pathlib.Path(r"E:\data\wp14")
        forbidden = [pathlib.Path(r"E:\data\h6")]
        receipt = {
            "execution": {
                "outputRoot": str(output),
                "forbiddenRoots": [str(forbidden[0])],
                "maximumParallelJobs": 4,
            }
        }
        MODULE.validate_environment(receipt, output, forbidden, 4)
        with self.assertRaisesRegex(MODULE.MatrixError, "parallelism"):
            MODULE.validate_environment(receipt, output, forbidden, 5)
        with self.assertRaisesRegex(MODULE.MatrixError, "output root"):
            MODULE.validate_environment(
                receipt, pathlib.Path(r"E:\data\other"), forbidden, 4
            )

    def test_independent_verifier_requires_both_strict_flags(self):
        completed = SimpleNamespace(returncode=0, stdout="", stderr="")
        with mock.patch.object(
            MODULE.subprocess, "run", return_value=completed
        ) as invoked:
            MODULE.verify_bundle(
                pathlib.Path("verify.py"),
                pathlib.Path("python.exe"),
                pathlib.Path("bundle"),
                {},
            )
        command = invoked.call_args.args[0]
        self.assertIn("--include-behavioral-hash", command)
        self.assertIn("--require-audited-solver-evidence", command)
        self.assertEqual("-B", command[1])

    def test_failed_verification_and_timeout_fail_closed(self):
        failed = SimpleNamespace(returncode=1, stdout="bad", stderr="")
        with mock.patch.object(MODULE.subprocess, "run", return_value=failed):
            with self.assertRaisesRegex(MODULE.MatrixError, "verification failed"):
                MODULE.verify_bundle(
                    pathlib.Path("verify.py"),
                    pathlib.Path("python.exe"),
                    pathlib.Path("bundle"),
                    {},
                )
        with mock.patch.object(
            MODULE.subprocess,
            "run",
            side_effect=subprocess.TimeoutExpired("verify", 1),
        ):
            with self.assertRaises(subprocess.TimeoutExpired):
                MODULE.verify_bundle(
                    pathlib.Path("verify.py"),
                    pathlib.Path("python.exe"),
                    pathlib.Path("bundle"),
                    {},
                )

    def test_verified_bundle_must_still_belong_to_the_exact_job(self):
        job = {
            "jobId": "expected-job",
            "scenarioContentSha256": "a" * 64,
        }
        valid = {
            "status": "pass",
            "label": "expected-job",
            "repeatCount": 1,
            "sourceScenarioContentSha256": "a" * 64,
            "repositoryInventorySha256": "b" * 64,
        }
        with tempfile.TemporaryDirectory() as directory:
            bundle = pathlib.Path(directory)
            summary = bundle / "summary.json"
            summary.write_text(json.dumps(valid), encoding="utf-8")
            self.assertEqual("b" * 64, MODULE.validate_output_binding(bundle, job))
            for field, mutation in (
                ("label", "another-job"),
                ("repeatCount", 2),
                ("sourceScenarioContentSha256", "c" * 64),
                ("repositoryInventorySha256", "not-a-hash"),
            ):
                mutant = dict(valid)
                mutant[field] = mutation
                summary.write_text(json.dumps(mutant), encoding="utf-8")
                with self.assertRaises(MODULE.MatrixError):
                    MODULE.validate_output_binding(bundle, job)
    def test_resource_envelope_and_exclusive_summary_are_enforced(self):
        envelope = {
            "minimumFreeDiskBytesBeforeRun": 100,
            "maximumOutputBytes": 200,
        }
        usage = SimpleNamespace(free=99)
        with mock.patch.object(MODULE.shutil, "disk_usage", return_value=usage):
            with self.assertRaisesRegex(MODULE.MatrixError, "free disk"):
                MODULE.resource_preflight(pathlib.Path("unused"), envelope)

        with tempfile.TemporaryDirectory() as directory:
            path = pathlib.Path(directory) / "summary.json"
            MODULE.write_exclusive(path, b"first\n")
            with self.assertRaises(FileExistsError):
                MODULE.write_exclusive(path, b"second\n")
            self.assertEqual(b"first\n", path.read_bytes())


class FreezeBuilderSelectionTests(unittest.TestCase):
    """A receipt must be verified by the builder that actually wrote it.

    The runner used to hardcode wp14_freeze.py. That was correct while v1 was
    the only design, and wrong the moment the adapter defect forced a re-freeze:
    a v2 receipt handed to the v1 builder fails with 'freeze receipt differs
    from its sources', which reads like corruption rather than a stale builder.
    """

    def test_each_freeze_identity_resolves_to_its_own_builder(self):
        self.assertEqual(
            MODULE.FREEZE_MODULES,
            {
                "wp14-development-ablation-v1": "wp14_freeze.py",
                "wp14-development-ablation-v2": "wp14_freeze_v2.py",
            },
        )
        for identity, filename in MODULE.FREEZE_MODULES.items():
            module = MODULE.load_freeze_module(ROOT, {"freezeId": identity})
            self.assertEqual(module.FREEZE_ID, identity)
            self.assertTrue(
                str(module.__spec__.origin).endswith(filename), filename
            )

    def test_an_unknown_or_missing_identity_fails_closed(self):
        for receipt in ({"freezeId": "wp14-development-ablation-v9"}, {}):
            with self.assertRaises(MODULE.MatrixError):
                MODULE.load_freeze_module(ROOT, receipt)

    def test_the_summary_contract_follows_the_freeze(self):
        self.assertEqual(
            MODULE.SUMMARY_CONTRACTS["wp14-development-ablation-v1"],
            (
                "benchmarks/schemas/wp14/v1/matrix-run-summary.schema.json",
                "ridebound-wp14-matrix-run-summary-v1",
                "1.0.0",
            ),
        )
        self.assertEqual(
            MODULE.SUMMARY_CONTRACTS["wp14-development-ablation-v2"],
            (
                "benchmarks/schemas/wp14/v2/matrix-run-summary.schema.json",
                "ridebound-wp14-matrix-run-summary-v2",
                "2.0.0",
            ),
        )
        for identity in MODULE.FREEZE_MODULES:
            relative, report, version = MODULE.summary_contract(
                {"freezeId": identity}
            )
            schema = json.loads((ROOT / relative).read_text(encoding="utf-8"))
            self.assertEqual(schema["properties"]["freezeId"]["const"], identity)
            self.assertEqual(
                schema["properties"]["reportType"]["const"], report
            )
            self.assertEqual(
                schema["properties"]["schemaVersion"]["const"], version
            )
        with self.assertRaises(MODULE.MatrixError):
            MODULE.summary_contract({"freezeId": "wp14-development-ablation-v9"})

    def test_the_two_builders_share_one_scientific_design(self):
        v1 = MODULE.load_freeze_module(
            ROOT, {"freezeId": "wp14-development-ablation-v1"}
        )
        v2 = MODULE.load_freeze_module(
            ROOT, {"freezeId": "wp14-development-ablation-v2"}
        )
        self.assertEqual(v1.GRID_ID, v2.GRID_ID)
        self.assertEqual(v1.CELL_COUNT, v2.CELL_COUNT)
        self.assertEqual(v1.ARM_COUNT, v2.ARM_COUNT)
        self.assertEqual(v1.MASTER_SEED, v2.MASTER_SEED)

if __name__ == "__main__":
    unittest.main()
