import importlib.util
import json
import pathlib
import tempfile
import unittest
from unittest import mock

import jsonschema


ROOT = pathlib.Path(__file__).resolve().parents[3]
MODULE_PATH = (
    ROOT / "simulators" / "fleetpy-ridebound" / "wp13_e1_freeze.py"
)
SPEC = importlib.util.spec_from_file_location("wp13_e1_freeze_under_test", MODULE_PATH)
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)
MATRIX_PATH = (
    ROOT / "simulators" / "fleetpy-ridebound" / "wp13_e1_run_matrix.py"
)
MATRIX_SPEC = importlib.util.spec_from_file_location(
    "wp13_e1_run_matrix_under_test",
    MATRIX_PATH,
)
MATRIX = importlib.util.module_from_spec(MATRIX_SPEC)
MATRIX_SPEC.loader.exec_module(MATRIX)


def _sha256(path):
    import hashlib

    return hashlib.sha256(path.read_bytes()).hexdigest()


def _record_set():
    records = []
    for panel in ("A", "B"):
        plan = json.loads(
            (ROOT / MODULE._SOURCE_PLANS[panel]).read_text(encoding="utf-8")
        )
        jobs = [job for job in plan["jobs"] if job["phase"] == "primary"]
        by_unit = {}
        for job in jobs:
            by_unit.setdefault(job["cellId"], {})[job["armId"]] = job["jobId"]
        for unit_id, labels in sorted(by_unit.items()):
            scenario = (
                ROOT
                / MODULE._FIXTURE_ROOTS[panel]
                / unit_id
                / "scenario-content.json"
            )
            records.append(
                {
                    "panelId": panel,
                    "unitId": unit_id,
                    "b1Label": labels["b1"],
                    "c1Label": labels["c1"],
                    "sourceScenarioContentSha256": _sha256(scenario),
                }
            )
    return {
        "reportType": "ridebound-wp13-first-divergence-record-set-v1",
        "records": records,
    }


class Wp13E1FreezeTests(unittest.TestCase):
    def test_exact_configs_and_all_40_pairs_build_canonical_plans(self):
        with tempfile.TemporaryDirectory() as root:
            record_set = pathlib.Path(root) / "records.json"
            record_set.write_text(json.dumps(_record_set()), encoding="utf-8")

            plans = MODULE.build_plans(ROOT, record_set)

        self.assertEqual({"A", "B"}, set(plans))
        self.assertEqual(40, len(plans["A"]["jobs"]))
        self.assertEqual(40, len(plans["B"]["jobs"]))
        self.assertEqual(
            80,
            len(
                {
                    job["jobId"]
                    for plan in plans.values()
                    for job in plan["jobs"]
                }
            ),
        )
        MODULE._verify_config_diffs(ROOT)

    def test_missing_or_outcome_selected_record_set_fails_closed(self):
        value = _record_set()
        value["records"].pop()
        with tempfile.TemporaryDirectory() as root:
            record_set = pathlib.Path(root) / "records.json"
            record_set.write_text(json.dumps(value), encoding="utf-8")
            with self.assertRaisesRegex(RuntimeError, "exact 40 targets"):
                MODULE.build_plans(ROOT, record_set)

    def test_label_mutation_and_plan_projection_mutation_fail_closed(self):
        value = _record_set()
        value["records"][0]["b1Label"] = "outcome-selected-substitute"
        with tempfile.TemporaryDirectory() as root:
            record_set = pathlib.Path(root) / "records.json"
            record_set.write_text(json.dumps(value), encoding="utf-8")
            with self.assertRaisesRegex(RuntimeError, "differs from record set"):
                MODULE.build_plans(ROOT, record_set)

        value = _record_set()
        with tempfile.TemporaryDirectory() as root:
            record_set = pathlib.Path(root) / "records.json"
            record_set.write_text(json.dumps(value), encoding="utf-8")
            actual_load = MODULE._load

            def mutated_load(path):
                loaded = actual_load(path)
                if pathlib.Path(path) == ROOT / MODULE._E1_PLANS["A"]:
                    loaded["jobs"][0]["masterSeed"] = 19
                return loaded

            with mock.patch.object(MODULE, "_load", side_effect=mutated_load):
                with self.assertRaisesRegex(RuntimeError, "not canonical/exact"):
                    MODULE.verify_plans(ROOT, record_set)

    def test_driver_binding_mutation_fails_closed(self):
        value = _record_set()
        with tempfile.TemporaryDirectory() as root:
            record_set = pathlib.Path(root) / "records.json"
            record_set.write_text(json.dumps(value), encoding="utf-8")
            actual_load = MODULE._load

            def mutated_load(path):
                loaded = actual_load(path)
                if str(path).endswith("d20181114-s10-r1.driver.json"):
                    loaded["expectedRequestCount"] = 107
                return loaded

            with mock.patch.object(MODULE, "_load", side_effect=mutated_load):
                with self.assertRaisesRegex(RuntimeError, "driver binding"):
                    MODULE.build_plans(ROOT, record_set)

    def test_freeze_schema_is_strict_draft_2020_12(self):
        schema = json.loads(
            (
                ROOT
                / "benchmarks/schemas/wp13/v1/"
                "exploratory-replay-freeze.schema.json"
            ).read_text(encoding="utf-8")
        )
        jsonschema.Draft202012Validator.check_schema(schema)
        self.assertFalse(schema["additionalProperties"])
        self.assertEqual(
            64 * 1024 * 1024,
            schema["properties"]["execution"]["properties"][
                "maximumOutputLineBytes"
            ]["const"],
        )
        runtime = schema["properties"]["runtime"]["properties"]
        self.assertEqual("#/$defs/gitCommit", runtime["fleetPyCommit"]["$ref"])
        self.assertEqual("10.0.301", runtime["dotnetSdkVersion"]["const"])
        self.assertEqual("10.0.9", runtime["dotnetRuntimeVersion"]["const"])
        commit_validator = jsonschema.Draft202012Validator(
            schema["$defs"]["gitCommit"]
        )
        self.assertFalse(
            list(
                commit_validator.iter_errors(
                    "053aa9d4fcfde91c5d303435d5748f9206c071b0"
                )
            )
        )
        self.assertTrue(list(commit_validator.iter_errors("0" * 64)))

    def test_tree_seal_excludes_cache_directories(self):
        with tempfile.TemporaryDirectory() as root:
            tree = pathlib.Path(root)
            source = tree / "source.py"
            source.write_text("value = 1\n", encoding="utf-8")
            cache = tree / "__pycache__"
            cache.mkdir()
            compiled = cache / "source.pyc"
            compiled.write_bytes(b"first")
            first = MODULE._tree_sha256(
                tree,
                b"test-domain",
                {"__pycache__"},
            )
            compiled.write_bytes(b"second")
            second = MODULE._tree_sha256(
                tree,
                b"test-domain",
                {"__pycache__"},
            )
            self.assertEqual(first, second)

    def test_staged_selection_requires_complete_frozen_pairs(self):
        jobs = [
            {"jobId": "u1-b1", "unitId": "u1", "armId": "b1"},
            {"jobId": "u1-c1", "unitId": "u1", "armId": "c1"},
            {"jobId": "u2-b1", "unitId": "u2", "armId": "b1"},
            {"jobId": "u2-c1", "unitId": "u2", "armId": "c1"},
        ]
        selected = MATRIX._select_jobs(jobs, ["u1-c1", "u1-b1"])
        self.assertEqual(
            {"u1-b1", "u1-c1"},
            {job["jobId"] for job in selected},
        )
        with self.assertRaisesRegex(RuntimeError, "complete B1/C1 pairs"):
            MATRIX._select_jobs(jobs, ["u1-b1"])
        with self.assertRaisesRegex(RuntimeError, "duplicate"):
            MATRIX._select_jobs(jobs, ["u1-b1", "u1-b1"])
        with self.assertRaisesRegex(RuntimeError, "absent"):
            MATRIX._select_jobs(jobs, ["missing-b1", "missing-c1"])

    def test_execution_arguments_must_match_frozen_roots_and_resources(self):
        receipt = {
            "execution": {
                "outputRoots": {"A": "E:/frozen/a"},
                "forbiddenRoots": ["E:/h6/a", "E:/h6/b"],
                "maximumParallelJobs": 4,
            }
        }
        output = pathlib.Path("E:/frozen/a").resolve()
        forbidden = [
            pathlib.Path("E:/h6/a").resolve(),
            pathlib.Path("E:/h6/b").resolve(),
        ]
        MATRIX._validate_execution_arguments(
            receipt,
            "A",
            output,
            forbidden,
            4,
        )
        with self.assertRaisesRegex(RuntimeError, "output root"):
            MATRIX._validate_execution_arguments(
                receipt,
                "A",
                pathlib.Path("E:/other").resolve(),
                forbidden,
                4,
            )
        with self.assertRaisesRegex(RuntimeError, "resource envelope"):
            MATRIX._validate_execution_arguments(
                receipt,
                "A",
                output,
                forbidden,
                5,
            )


if __name__ == "__main__":
    unittest.main()
