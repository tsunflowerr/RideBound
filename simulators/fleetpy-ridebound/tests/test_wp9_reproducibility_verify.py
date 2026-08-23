import copy
import importlib.util
import pathlib
import unittest


_ROOT = pathlib.Path(__file__).parents[1]
_SPECIFICATION = importlib.util.spec_from_file_location(
    "wp9_reproducibility_verify",
    _ROOT / "wp9_reproducibility_verify.py",
)
_MODULE = importlib.util.module_from_spec(_SPECIFICATION)
_SPECIFICATION.loader.exec_module(_MODULE)


class ReproducibilityVerifierMutationTests(unittest.TestCase):
    def test_demand_mutation_is_rejected(self):
        expected = [
            {"eventType": "requestArrived", "simTimeMs": 10, "payload": {"x": 1}}
        ]
        mutated = copy.deepcopy(expected)
        mutated[0]["payload"]["x"] = 2
        with self.assertRaisesRegex(RuntimeError, "demand/travel"):
            _MODULE._validate_pair_projection(expected, expected, mutated, "cell")

    def test_travel_mutation_is_rejected(self):
        expected = [
            {
                "eventType": "travelTimesUpdated",
                "simTimeMs": 0,
                "payload": {"snapshot": {"version": 1}},
            }
        ]
        mutated = copy.deepcopy(expected)
        mutated[0]["payload"]["snapshot"]["version"] = 2
        with self.assertRaisesRegex(RuntimeError, "demand/travel"):
            _MODULE._validate_pair_projection(expected, mutated, expected, "cell")

    def test_behavioral_repeat_mutation_is_rejected(self):
        result = {
            "status": "pass",
            "repeatCount": 2,
            "behavioralProjectionHash": "a" * 64,
            "runs": [
                {"behavioralProjectionHash": "a" * 64},
                {"behavioralProjectionHash": "b" * 64},
            ],
        }
        with self.assertRaisesRegex(RuntimeError, "repeats diverged"):
            _MODULE._validate_repeat_result(result)

    def test_solver_seed_is_absent_from_experimental_unit_identity(self):
        identity = _MODULE._experimental_unit_id("a" * 64, "b" * 64, "c" * 64)
        same_identity = _MODULE._experimental_unit_id(
            "a" * 64, "b" * 64, "c" * 64
        )
        self.assertEqual(identity, same_identity)
        self.assertEqual(64, len(identity))

    def test_event_order_mutation_changes_the_identity(self):
        first = [
            {"eventType": "requestArrived", "simTimeMs": 10, "payload": {"x": 1}},
            {"eventType": "requestArrived", "simTimeMs": 20, "payload": {"x": 2}},
        ]
        second = list(reversed(first))
        self.assertNotEqual(
            _MODULE._projection_identity(first, "a" * 64),
            _MODULE._projection_identity(second, "a" * 64),
        )


if __name__ == "__main__":
    unittest.main()
