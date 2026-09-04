"""Tests for the WP14R service-burden frontier tool.

Two of these exist because the tool told a falsehood about its own coverage.
It was written while the v5 matrix lay halted at 40 of 160 jobs, and it asserted
`descriptiveSliceNotThePreregisteredSixteenCellFrontier` as a constant. Under
freeze v6 the matrix completed 160 of 160 and the constant kept asserting a
slice, understating the evidence and mislabelling it. A claim boundary that can
be wrong about its own coverage is worse than no claim boundary at all.

The third exists because `pareto` reports every arm in an exact tie as
non-dominated. That is correct Pareto semantics and a misleading headline: on
the v6 matrix six arms agree on all sixteen cells, so ten "non-dominated arms"
describes five points, one of them sixfold.
"""

import importlib.util
import pathlib
import unittest

_HERE = pathlib.Path(__file__).resolve().parent
_TOOL = _HERE.parent / "wp14r_slice_frontier.py"
_SPEC = importlib.util.spec_from_file_location("wp14r_slice_frontier", _TOOL)
_MODULE = importlib.util.module_from_spec(_SPEC)
_SPEC.loader.exec_module(_MODULE)


def _row(arm, completed, burden, **overrides):
    row = {
        "armId": arm,
        "arrived": 1728,
        "completed": completed,
        "decisions": 12325,
        "disruptiveDecisions": 9,
        "ridersCharged": 6,
        "attributedBurdenMs": burden,
        "exogenousBurdenMs": 7019577,
        "experiencedBurdenMs": 7102950,
    }
    row.update(overrides)
    return row


class CoverageClaimTests(unittest.TestCase):
    def test_a_complete_matrix_is_not_called_a_slice(self):
        claim = _MODULE.coverage_claim(160, 160, ["c"] * 16, 16)
        self.assertEqual(
            claim, ("coversTheCompletePreregisteredDesignAllJobsAllCells",)
        )
        self.assertNotIn(
            "descriptiveSliceNotThePreregisteredSixteenCellFrontier", claim
        )

    def test_a_short_matrix_is_still_called_a_slice(self):
        for designed, observed, cells in (
            (160, 40, ["c"] * 4),
            (160, 159, ["c"] * 16),
            (160, 160, ["c"] * 15),
        ):
            with self.subTest(observed=observed, cells=len(cells)):
                self.assertEqual(
                    _MODULE.coverage_claim(designed, observed, cells, 16),
                    ("descriptiveSliceNotThePreregisteredSixteenCellFrontier",),
                )

    def test_the_constant_boundary_never_asserts_coverage(self):
        # Coverage must come from observation only, so no coverage word may
        # survive in the constant part.
        joined = " ".join(_MODULE.CLAIM_BOUNDARY)
        self.assertNotIn("descriptiveSlice", joined)
        self.assertNotIn("coversTheComplete", joined)
        self.assertIn(
            "developmentExploratoryOnlyNotConfirmatory", _MODULE.CLAIM_BOUNDARY
        )

    def test_a_complete_matrix_is_still_not_confirmatory(self):
        # Completing the design makes the slice whole. It does not promote
        # WP14 out of its exploratory scope.
        self.assertIn(
            "developmentExploratoryOnlyNotConfirmatory", _MODULE.CLAIM_BOUNDARY
        )
        self.assertIn("doesNotReinterpretOrRescueH6", _MODULE.CLAIM_BOUNDARY)


class DistinctOutcomeGroupTests(unittest.TestCase):
    def test_arms_agreeing_on_every_counter_are_one_group(self):
        rows = [
            _row("c1-h6ref", 1534, 83373),
            _row("c1-ratchet", 1534, 83373),
            _row("c1-freeze600", 1534, 83373),
            _row("c1-budget60", 1540, 716125, ridersCharged=20),
            _row("b1-ref", 1629, 59250164, ridersCharged=388),
        ]
        groups = _MODULE.distinct_outcome_groups(rows)
        self.assertEqual(len(groups), 3)
        self.assertEqual(groups[0]["completed"], 1629)
        self.assertEqual(groups[0]["arms"], ["b1-ref"])
        tie = next(g for g in groups if g["armCount"] == 3)
        self.assertEqual(
            tie["arms"], ["c1-freeze600", "c1-h6ref", "c1-ratchet"]
        )
        self.assertEqual(tie["attributedBurdenMs"], 83373)

    def test_a_single_differing_counter_splits_a_group(self):
        rows = [
            _row("a", 1534, 83373),
            _row("b", 1534, 83373, disruptiveDecisions=10),
        ]
        groups = _MODULE.distinct_outcome_groups(rows)
        self.assertEqual([g["arms"] for g in groups], [["a"], ["b"]])

    def test_groups_are_ordered_by_service_and_arms_sorted(self):
        rows = [
            _row("z", 1500, 1),
            _row("a", 1600, 2),
            _row("m", 1600, 2),
        ]
        groups = _MODULE.distinct_outcome_groups(rows)
        self.assertEqual([g["completed"] for g in groups], [1600, 1500])
        self.assertEqual(groups[0]["arms"], ["a", "m"])


class ParetoTieTests(unittest.TestCase):
    def test_an_exact_tie_leaves_every_tied_arm_non_dominated(self):
        """Documents why pareto alone is a misleading headline."""
        rows = [
            _row("a", 1534, 83373),
            _row("b", 1534, 83373),
            _row("c", 1629, 59250164, ridersCharged=388),
        ]
        self.assertEqual(_MODULE.pareto(rows), ["a", "b", "c"])
        self.assertEqual(len(_MODULE.distinct_outcome_groups(rows)), 2)

    def test_a_dominated_arm_is_excluded(self):
        rows = [
            _row("better", 1600, 100),
            _row("worse", 1500, 200),
        ]
        self.assertEqual(_MODULE.pareto(rows), ["better"])


if __name__ == "__main__":
    unittest.main()
