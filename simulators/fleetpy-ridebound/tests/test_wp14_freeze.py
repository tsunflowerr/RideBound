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
MODULE_PATH = ROOT / "simulators/fleetpy-ridebound/wp14_freeze.py"
SPEC = importlib.util.spec_from_file_location("wp14_freeze_under_test", MODULE_PATH)
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


class Wp14FreezeTests(unittest.TestCase):
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
