"""Tests for the WP14 development ablation freeze v2.

Same contract as the v1 test, against the re-frozen builder. v2 exists only
because the adapter that v1 sealed reported a vehicle which had entered an
edge as standing on the node behind it, so the core planned from a place the
vehicle could not cheaply return to and the v1 matrix halted on its first
evening job. The scientific design is untouched: the same 16 cells, 10 arms,
160 jobs, panel, denominators and seed 7.

The extra test here is the one v1 never needed: a freeze must seal its own
builder. Copying v1 forward left v2 sealing wp14_freeze.py, so a v2 receipt
would have verified while its own source drifted freely.
"""
import copy
import importlib.util
import json
import pathlib
import sys
import tempfile
import unittest
from unittest import mock

import jsonschema


ROOT = pathlib.Path(__file__).resolve().parents[3]
MODULE_PATH = ROOT / "simulators/fleetpy-ridebound/wp14_freeze_v2.py"
SPEC = importlib.util.spec_from_file_location("wp14_freeze_v2_under_test", MODULE_PATH)
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)
OUTPUT_ROOT = pathlib.Path(r"E:\RideBoundData\wp14\development-ablation")
FORBIDDEN = sorted(MODULE.EXPECTED_FORBIDDEN_ROOTS, key=str)
RUNNER = pathlib.Path(r"E:\RideBoundData\wp14\runner-v1")
FLEETPY = pathlib.Path(r"E:\RideBoundData\wp7\FleetPy-1.0.2")
PYTHON = pathlib.Path(sys.executable)
DOTNET = pathlib.Path(r"C:\Program Files\dotnet\dotnet.exe")
AUDIT = pathlib.Path(r"E:\RideBoundData\wp14\development-panel-audit-v1.json")
PLANNING = pathlib.Path(
    r"E:\RideBoundData\wp13\e1-retained-portfolio-inventory-v1.json"
)


def build():
    return MODULE.build(
        ROOT,
        OUTPUT_ROOT,
        FORBIDDEN,
        MODULE.MAXIMUM_PARALLEL_JOBS,
        RUNNER,
        FLEETPY,
        PYTHON,
        DOTNET,
        AUDIT,
        PLANNING,
        "2026-08-25T00:00:00Z",
    )


class Wp14FreezeV2Tests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.receipt = build()

    def test_real_freeze_is_exact_cross_product_and_schema_valid(self):
        design = self.receipt["design"]
        self.assertEqual(16, design["cellCount"])
        self.assertEqual(10, design["armCount"])
        self.assertEqual(160, design["jobCount"])
        self.assertEqual(1728, design["arrivalsPerArm"])
        pairs = {(job["cellId"], job["armId"]) for job in design["jobs"]}
        self.assertEqual(160, len(pairs))
        self.assertEqual(160, len({job["jobId"] for job in design["jobs"]}))
        schema = json.loads(
            (ROOT / MODULE.SCHEMA_RELATIVE).read_text(encoding="utf-8")
        )
        jsonschema.Draft202012Validator.check_schema(schema)
        jsonschema.Draft202012Validator(
            schema, format_checker=jsonschema.FormatChecker()
        ).validate(self.receipt)

    def test_every_driver_and_configuration_hash_resolves(self):
        files = {
            entry["path"]: entry["sha256"]
            for entry in self.receipt["sourceIdentity"]["repositoryFiles"]
        }
        for job in self.receipt["design"]["jobs"]:
            self.assertEqual(job["driverSha256"], files[job["driver"]])
        for arm in self.receipt["design"]["arms"]:
            self.assertEqual(
                arm["commitmentConfigSha256"], files[arm["commitmentConfig"]]
            )
            self.assertEqual(arm["wp4ConfigSha256"], files[arm["wp4Config"]])

    def test_missing_configuration_fails_before_receipt(self):
        actual = MODULE.sha256_file

        def missing(path):
            if pathlib.Path(path).name == "wp14-c1-f1-freeze300-v1.json":
                raise MODULE.FreezeError("required file not found")
            return actual(path)

        with mock.patch.object(MODULE, "sha256_file", side_effect=missing):
            with self.assertRaisesRegex(MODULE.FreezeError, "required file"):
                build()

    def test_only_exact_forbidden_roots_and_parallelism_are_accepted(self):
        with self.assertRaisesRegex(MODULE.FreezeError, "exact frozen H6/E1"):
            MODULE.build(
                ROOT,
                OUTPUT_ROOT,
                FORBIDDEN[:-1],
                4,
                RUNNER,
                FLEETPY,
                PYTHON,
                DOTNET,
                AUDIT,
                PLANNING,
                "2026-08-25T00:00:00Z",
            )
        with self.assertRaisesRegex(MODULE.FreezeError, "pin 4"):
            MODULE.build(
                ROOT,
                OUTPUT_ROOT,
                FORBIDDEN,
                5,
                RUNNER,
                FLEETPY,
                PYTHON,
                DOTNET,
                AUDIT,
                PLANNING,
                "2026-08-25T00:00:00Z",
            )

    def test_overlap_is_rejected_in_both_directions(self):
        forbidden = pathlib.Path(r"E:\RideBoundData\wp9\confirmatory-h6-panela")
        with self.assertRaisesRegex(MODULE.FreezeError, "overlaps"):
            MODULE.require_disjoint(forbidden / "child", [forbidden])
        with self.assertRaisesRegex(MODULE.FreezeError, "overlaps"):
            MODULE.require_disjoint(forbidden.parent, [forbidden])

    def test_receipt_mutations_and_noncanonical_bytes_are_rejected(self):
        analyzer = "simulators/fleetpy-ridebound/wp14_frontier_analyze.py"
        mutants = []
        missing_job = copy.deepcopy(self.receipt)
        missing_job["design"]["jobs"].pop()
        mutants.append(missing_job)
        changed_hash = copy.deepcopy(self.receipt)
        entry = next(
            value
            for value in changed_hash["sourceIdentity"]["repositoryFiles"]
            if value["path"] == analyzer
        )
        entry["sha256"] = "0" * 64
        mutants.append(changed_hash)
        changed_runtime = copy.deepcopy(self.receipt)
        changed_runtime["runtime"]["pythonExecutableSha256"] = "f" * 64
        mutants.append(changed_runtime)

        with tempfile.TemporaryDirectory() as directory:
            for index, mutant in enumerate(mutants):
                path = pathlib.Path(directory) / f"mutant-{index}.json"
                path.write_bytes(MODULE.canonical(mutant) + b"\n")
                with mock.patch.object(MODULE, "build", return_value=self.receipt):
                    with self.assertRaisesRegex(MODULE.FreezeError, "differs"):
                        MODULE.verify_receipt(
                            path,
                            ROOT,
                            OUTPUT_ROOT,
                            FORBIDDEN,
                            4,
                            RUNNER,
                            FLEETPY,
                            PYTHON,
                            DOTNET,
                            AUDIT,
                            PLANNING,
                        )

            pretty = pathlib.Path(directory) / "pretty.json"
            pretty.write_text(json.dumps(self.receipt, indent=2), encoding="utf-8")
            with mock.patch.object(MODULE, "build", return_value=self.receipt):
                with self.assertRaisesRegex(MODULE.FreezeError, "canonical"):
                    MODULE.verify_receipt(
                        pretty,
                        ROOT,
                        OUTPUT_ROOT,
                        FORBIDDEN,
                        4,
                        RUNNER,
                        FLEETPY,
                        PYTHON,
                        DOTNET,
                        AUDIT,
                        PLANNING,
                    )

    def test_exclusive_create_never_overwrites(self):
        with tempfile.TemporaryDirectory() as directory:
            path = pathlib.Path(directory) / "receipt.json"
            MODULE.write_exclusive(path, b"first\n")
            with self.assertRaises(FileExistsError):
                MODULE.write_exclusive(path, b"second\n")
            self.assertEqual(b"first\n", path.read_bytes())


if __name__ == "__main__":
    unittest.main()


class Wp14FreezeV2SelfSealTests(unittest.TestCase):
    def test_the_receipt_seals_the_v2_builder_and_its_own_test(self):
        sealed = set(MODULE.STATIC_REPOSITORY_FILES)
        self.assertIn("simulators/fleetpy-ridebound/wp14_freeze_v2.py", sealed)
        self.assertIn(
            "simulators/fleetpy-ridebound/tests/test_wp14_freeze_v2.py", sealed
        )
        self.assertNotIn("simulators/fleetpy-ridebound/wp14_freeze.py", sealed)
        self.assertNotIn(
            "simulators/fleetpy-ridebound/tests/test_wp14_freeze.py", sealed
        )

    def test_the_design_is_identical_to_v1(self):
        v1_path = ROOT / "simulators/fleetpy-ridebound/wp14_freeze.py"
        spec = importlib.util.spec_from_file_location("wp14_freeze_v1", v1_path)
        v1 = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(v1)
        self.assertEqual(MODULE.GRID_ID, v1.GRID_ID)
        self.assertEqual(MODULE.CELL_COUNT, v1.CELL_COUNT)
        self.assertEqual(MODULE.ARM_COUNT, v1.ARM_COUNT)
        self.assertEqual(MODULE.MASTER_SEED, v1.MASTER_SEED)
        self.assertNotEqual(MODULE.FREEZE_ID, v1.FREEZE_ID)
