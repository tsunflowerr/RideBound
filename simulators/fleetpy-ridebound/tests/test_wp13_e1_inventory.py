import importlib.util
import json
import pathlib
import unittest

import jsonschema


ROOT = pathlib.Path(__file__).resolve().parents[3]
MODULE_PATH = (
    ROOT / "simulators" / "fleetpy-ridebound" / "wp13_e1_inventory.py"
)
SPEC = importlib.util.spec_from_file_location("wp13_e1_inventory_under_test", MODULE_PATH)
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


def _records():
    result = []
    for panel in ("A", "B"):
        for index in range(40):
            result.append(
                {
                    "panelId": panel,
                    "jobId": f"{panel}-{index}",
                    "repositoryInventorySha256": "a" * 64,
                    "retainedPortfolioEvidenceCount": 3,
                    "solverDecisionCount": 3,
                    "requestCount": 108,
                    "bundleBytes": 10,
                }
            )
    return result


class Wp13E1InventoryTests(unittest.TestCase):
    def test_inventory_schema_is_strict_draft_2020_12(self):
        schema = json.loads(
            (
                ROOT
                / "benchmarks/schemas/wp13/v1/"
                "exploratory-replay-inventory.schema.json"
            ).read_text(encoding="utf-8")
        )
        jsonschema.Draft202012Validator.check_schema(schema)
        self.assertFalse(schema["additionalProperties"])
        self.assertEqual(80, schema["properties"]["runs"]["minItems"])

    def test_aggregate_fails_on_missing_or_incomplete_coverage(self):
        records = _records()
        plans = {
            panel: {
                "planId": f"plan-{panel}",
                "jobs": [
                    {"jobId": f"{panel}-{index}"} for index in range(40)
                ],
            }
            for panel in ("A", "B")
        }
        roots = {"A": pathlib.Path("A"), "B": pathlib.Path("B")}
        with self.assertRaisesRegex(RuntimeError, "exact 80"):
            MODULE._aggregate(plans, roots, records[:-1])
        records[0]["retainedPortfolioEvidenceCount"] = 2
        with self.assertRaisesRegex(RuntimeError, "coverage"):
            MODULE._aggregate(plans, roots, records)

    def test_aggregate_rejects_source_inventory_drift(self):
        records = _records()
        records[-1]["repositoryInventorySha256"] = "b" * 64
        plans = {
            panel: {
                "planId": f"plan-{panel}",
                "jobs": [
                    {"jobId": f"{panel}-{index}"} for index in range(40)
                ],
            }
            for panel in ("A", "B")
        }
        roots = {"A": pathlib.Path("A"), "B": pathlib.Path("B")}
        with self.assertRaisesRegex(RuntimeError, "inventories differ"):
            MODULE._aggregate(plans, roots, records)


if __name__ == "__main__":
    unittest.main()
