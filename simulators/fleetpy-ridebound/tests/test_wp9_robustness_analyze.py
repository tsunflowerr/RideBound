import importlib.util
import pathlib
import unittest


_ROOT = pathlib.Path(__file__).parents[1]
_SPEC = importlib.util.spec_from_file_location(
    "wp9_robustness_analyze",
    _ROOT / "wp9_robustness_analyze.py",
)
_MODULE = importlib.util.module_from_spec(_SPEC)
_SPEC.loader.exec_module(_MODULE)


def _observation(arrived, completed, burden, disruptive=0):
    return {
        "arrived": arrived,
        "completed": completed,
        "decisionDropEtaTotalMs": burden,
        "decisionPickupEtaTotalMs": 0,
        "disruptiveDecisionCount": disruptive,
        "exogenousDropEtaTotalMs": 0,
        "exogenousPickupEtaTotalMs": 0,
        "materialEtaRevisionCount": 0,
        "prePickupInsertedStopCount": 0,
        "totalDecisionInducedBurdenMs": burden,
    }


def _variants(arrived=100):
    return {
        "primaryBaseline": _observation(arrived, 90, 1000, 10),
        "primaryTreatment": _observation(arrived, 88, 100, 2),
        "unboundedTreatment": _observation(arrived, 89, 300, 4),
        "hybridLoose": _observation(arrived, 90, 500, 6),
        "seed19Baseline": _observation(arrived, 91, 1100, 11),
        "seed19Treatment": _observation(arrived, 90, 120, 3),
    }


class RobustnessAnalysisTests(unittest.TestCase):
    def test_reports_predeclared_exact_contrasts_without_a_gate(self):
        result = _MODULE._robustness_result(
            "analysis",
            [("cell", _variants())],
        )

        self.assertIsNone(result["confirmatoryGate"])
        self.assertEqual(
            "descriptiveOnlyCannotRescuePrimary",
            result["interpretation"],
        )
        budget = result["aggregateContrasts"]["tightMinusUnboundedTreatment"]
        self.assertEqual(-1, budget["deltaCompleted"])
        self.assertEqual(-200, budget["deltaTotalDecisionInducedBurdenMs"])

    def test_aggregates_cells_before_forming_contrasts(self):
        result = _MODULE._robustness_result(
            "analysis",
            [("a", _variants()), ("b", _variants())],
        )

        primary = result["aggregateContrasts"]["primaryTreatmentMinusBaseline"]
        self.assertEqual(-4, primary["deltaCompleted"])
        self.assertEqual(-1800, primary["deltaTotalDecisionInducedBurdenMs"])

    def test_different_variant_denominators_are_rejected(self):
        variants = _variants()
        variants["seed19Treatment"] = _observation(99, 90, 120, 3)

        with self.assertRaisesRegex(RuntimeError, "arrival counts"):
            _MODULE._robustness_result("analysis", [("cell", variants)])

    def test_seed19_bundle_cannot_be_bound_as_primary(self):
        job = {
            "armId": "b1",
            "cellId": "cell",
            "commitmentConfig": (
                "benchmarks/configurations/wp8-drop-eta-budget-tight-v1.json"
            ),
            "driver": "benchmarks/scenarios/wp9-confirmatory/cell.driver.json",
            "jobId": "r-cell-b1-tight-s19",
            "masterSeed": 19,
            "phase": "robustness",
            "wp4Config": (
                "benchmarks/configurations/"
                "wp9-fleetpy-rolling-cost-audited-v1.json"
            ),
        }

        with self.assertRaisesRegex(RuntimeError, "job binding"):
            _MODULE._validate_variant_job(
                job,
                "cell",
                "primaryBaseline",
                job["jobId"],
            )


if __name__ == "__main__":
    unittest.main()
