import importlib.util
import json
import pathlib
import unittest

import jsonschema


ROOT = pathlib.Path(__file__).resolve().parents[3]
MODULE_PATH = (
    ROOT / "simulators" / "fleetpy-ridebound" / "wp13_e1_h6_equivalence.py"
)
SPEC = importlib.util.spec_from_file_location(
    "wp13_e1_h6_equivalence_under_test",
    MODULE_PATH,
)
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


def _records():
    return [
        {
            "panelId": panel,
            "jobId": f"{panel}-{index}",
            "operationallyEqual": True,
            "h6SolverDecisionCount": 1,
            "h6BundleManifestSha256": f"{index:064x}",
        }
        for panel in ("A", "B")
        for index in range(40)
    ]


class Wp13E1H6EquivalenceTests(unittest.TestCase):
    def test_schema_is_strict_draft_2020_12(self):
        schema = json.loads(
            (
                ROOT
                / "benchmarks/schemas/wp13/v1/"
                "e1-h6-behavioral-equivalence.schema.json"
            ).read_text(encoding="utf-8")
        )
        jsonschema.Draft202012Validator.check_schema(schema)
        self.assertFalse(schema["additionalProperties"])
        self.assertEqual(80, schema["properties"]["records"]["minItems"])

    def test_aggregate_requires_exact_targets_and_equality(self):
        records = _records()
        panels = MODULE._aggregate(records)
        self.assertEqual([40, 40], [panel["armRunCount"] for panel in panels])
        with self.assertRaisesRegex(RuntimeError, "exact 80"):
            MODULE._aggregate(records[:-1])
        records[0]["operationallyEqual"] = False
        with self.assertRaisesRegex(RuntimeError, "changed same-arm behavior"):
            MODULE._aggregate(records)


if __name__ == "__main__":
    unittest.main()
