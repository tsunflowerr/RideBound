import copy
import importlib.util
import json
import pathlib
import tempfile
import unittest


TEST_ROOT = pathlib.Path(__file__).resolve().parent
ADAPTER_ROOT = TEST_ROOT.parent
REPOSITORY_ROOT = TEST_ROOT.parents[2]
AUDIT_PATH = ADAPTER_ROOT / "wp14_development_panel_audit.py"
_SPEC = importlib.util.spec_from_file_location("wp14_development_panel_audit", AUDIT_PATH)
audit = importlib.util.module_from_spec(_SPEC)
_SPEC.loader.exec_module(audit)

DEVELOPMENT = (
    REPOSITORY_ROOT / "benchmarks" / "scenarios" / "wp14-development" / "grid-v1.json"
)
FROZEN = [
    REPOSITORY_ROOT / "benchmarks" / "scenarios" / "wp9-confirmatory" / "grid-v2.json",
    REPOSITORY_ROOT
    / "benchmarks"
    / "scenarios"
    / "wp9-confirmatory"
    / "grid-v3-veh4.json",
]


class Wp14DevelopmentPanelAuditTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.result = audit.audit(REPOSITORY_ROOT, DEVELOPMENT, FROZEN)

    def test_the_real_panels_do_not_overlap_on_any_axis(self):
        self.assertEqual(
            self.result["overlapCountByAxis"],
            {axis: 0 for axis in audit.COMPARED_AXES},
        )

    def test_the_declared_dates_are_disjoint(self):
        development = set(self.result["developmentDates"])
        frozen = set(self.result["frozenDates"])

        self.assertEqual(development & frozen, set())
        self.assertEqual(frozen, {f"2018-11-{day}" for day in (14, 15, 16, 17, 18)})

    def test_every_development_cell_has_a_generated_scenario_hash(self):
        self.assertEqual(self.result["developmentCellCount"], 16)
        hashes = [cell["scenarioHash"] for cell in self.result["developmentCells"]]
        self.assertTrue(all(isinstance(value, str) for value in hashes))
        self.assertEqual(len(set(hashes)), len(hashes))

    def test_a_reused_demand_realization_fails_closed(self):
        """Mutation guard: borrowing one frozen demand file must be rejected."""
        with tempfile.TemporaryDirectory() as directory:
            manifest = json.loads(DEVELOPMENT.read_text(encoding="utf-8"))
            frozen = json.loads(FROZEN[0].read_text(encoding="utf-8"))
            leaking = copy.deepcopy(manifest)
            leaking["cells"][0]["demandMemberPath"] = frozen["cells"][0][
                "demandMemberPath"
            ]
            path = pathlib.Path(directory) / "leaking.json"
            path.write_text(json.dumps(leaking), encoding="utf-8")

            with self.assertRaises(audit.AuditError) as raised:
                audit.audit(REPOSITORY_ROOT, path, FROZEN)

            self.assertIn("demandMemberPath", str(raised.exception))

    def test_a_reused_scenario_id_fails_closed(self):
        with tempfile.TemporaryDirectory() as directory:
            manifest = json.loads(DEVELOPMENT.read_text(encoding="utf-8"))
            frozen = json.loads(FROZEN[0].read_text(encoding="utf-8"))
            leaking = copy.deepcopy(manifest)
            leaking["cells"][0]["scenarioId"] = frozen["cells"][0]["scenarioId"]
            path = pathlib.Path(directory) / "leaking.json"
            path.write_text(json.dumps(leaking), encoding="utf-8")

            with self.assertRaises(audit.AuditError):
                audit.audit(REPOSITORY_ROOT, path, FROZEN)

    def test_a_missing_generated_fixture_fails_closed(self):
        with tempfile.TemporaryDirectory() as directory:
            manifest = json.loads(DEVELOPMENT.read_text(encoding="utf-8"))
            manifest["cells"][0]["cellId"] = "d20181112-s10-r9-w08"
            path = pathlib.Path(directory) / "missing.json"
            path.write_text(json.dumps(manifest), encoding="utf-8")

            with self.assertRaises(audit.AuditError) as raised:
                audit.audit(REPOSITORY_ROOT, path, FROZEN)

            self.assertIn("generated fixture missing", str(raised.exception))

    def test_comparison_requires_at_least_one_frozen_grid(self):
        with self.assertRaises(audit.AuditError):
            audit.audit(REPOSITORY_ROOT, DEVELOPMENT, [])


if __name__ == "__main__":
    unittest.main()
