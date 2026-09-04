"""Unit tests for the signed promise-direction tool.

The whole point of the tool is one distinction the frozen analyzer does not
make: a promise that moved earlier because the decision moved it, versus one
that moved earlier because the exogenous projection drifted. These tests pin
that distinction, and pin that the tool refuses to write anywhere it should not.
"""

import base64
import importlib.util
import json
import pathlib
import tempfile
import unittest

_HERE = pathlib.Path(__file__).resolve().parent
_TOOL_PATH = _HERE.parent / "wp14r_promise_direction.py"

_spec = importlib.util.spec_from_file_location(
    "wp14r_promise_direction_under_test", _TOOL_PATH
)
tool = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(tool)


def _publication(request, pickup, drop, pickup_delta=0, drop_delta=0):
    return {
        "decisionType": "promisePublished",
        "payload": {
            "promise": {
                "requestId": request,
                "pickupEtaMs": pickup,
                "dropEtaMs": drop,
            },
            "decisionDelta": {
                "pickupEtaTotalMs": pickup_delta,
                "dropEtaTotalMs": drop_delta,
            },
        },
    }


def _transcript(directory, publications_per_decision):
    """Write a minimal but structurally faithful transcript."""
    bundle = pathlib.Path(directory)
    bundle.mkdir(parents=True, exist_ok=True)
    lines = []
    for actions in publications_per_decision:
        frame = {"messageType": "decision", "payload": {"actions": actions}}
        encoded = base64.b64encode(
            json.dumps(frame).encode("utf-8")
        ).decode("ascii")
        lines.append(json.dumps({
            "direction": "runnerToAdapter",
            "frameBase64": encoded,
        }))
    # A frame in the other direction must be ignored entirely.
    lines.insert(0, json.dumps({
        "direction": "adapterToRunner",
        "frameBase64": base64.b64encode(b'{"messageType":"hello"}').decode("ascii"),
    }))
    (bundle / "transcript-00.ndjson").write_text(
        "\n".join(lines) + "\n", encoding="utf-8"
    )
    return bundle


class PromiseDirectionTests(unittest.TestCase):
    def test_decision_moved_earlier_is_separated_from_exogenous_drift(self):
        with tempfile.TemporaryDirectory() as directory:
            bundle = _transcript(pathlib.Path(directory) / "b", [
                [_publication("r1", 1000, 5000)],
                # exogenous: pickup improves, decision touched nothing
                [_publication("r1", 900, 4900)],
                # decision moved pickup and it got later
                [_publication("r1", 1200, 4900, pickup_delta=300)],
                # decision moved pickup and it got earlier
                [_publication("r1", 1100, 4900, pickup_delta=100)],
            ])
            report = tool.build_report([bundle], "unit")
        pickup = report["byDimension"]["pickup"]
        self.assertEqual(pickup["decisionMovedPublications"], 2)
        self.assertEqual(pickup["decisionMovedEarlier"], 1)
        self.assertEqual(pickup["decisionMovedEarlierMs"], 100)
        self.assertEqual(pickup["decisionMovedLater"], 1)
        self.assertEqual(pickup["decisionMovedLaterMs"], 300)
        self.assertEqual(pickup["exogenousOnlyEarlier"], 1)
        self.assertFalse(report["ratchetInertOnThisEvidence"])

    def test_ratchet_is_inert_when_decisions_only_ever_worsen(self):
        with tempfile.TemporaryDirectory() as directory:
            bundle = _transcript(pathlib.Path(directory) / "b", [
                [_publication("r1", 1000, 5000)],
                [_publication("r1", 900, 4800)],  # exogenous improvement only
                [_publication("r1", 1300, 5200, pickup_delta=400, drop_delta=400)],
            ])
            report = tool.build_report([bundle], "unit")
        self.assertEqual(report["ratchetAdmissibleObservations"], 0)
        self.assertEqual(report["decisionMovedObservations"], 2)
        self.assertTrue(report["ratchetInertOnThisEvidence"])
        self.assertEqual(
            report["byDimension"]["pickup"]["exogenousOnlyEarlier"], 1
        )

    def test_no_decision_movement_at_all_is_not_called_inert(self):
        with tempfile.TemporaryDirectory() as directory:
            bundle = _transcript(pathlib.Path(directory) / "b", [
                [_publication("r1", 1000, 5000)],
                [_publication("r1", 900, 4800)],
            ])
            report = tool.build_report([bundle], "unit")
        self.assertEqual(report["decisionMovedObservations"], 0)
        self.assertFalse(report["ratchetInertOnThisEvidence"])

    def test_first_promise_is_not_counted_as_a_revision(self):
        with tempfile.TemporaryDirectory() as directory:
            bundle = _transcript(pathlib.Path(directory) / "b", [
                [_publication("r1", 1000, 5000), _publication("r2", 2000, 6000)],
            ])
            report = tool.build_report([bundle], "unit")
        self.assertEqual(report["publications"], 2)
        self.assertEqual(report["revisions"], 0)

    def test_report_refuses_to_land_inside_a_forbidden_root(self):
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            bundle = _transcript(root / "bundles" / "b", [
                [_publication("r1", 1000, 5000)],
            ])
            output = root / "forbidden" / "report.json"
            with self.assertRaises(tool.DirectionError):
                tool.main([
                    "--bundle", str(bundle),
                    "--label", "unit",
                    "--output", str(output),
                    "--forbidden-root", str(root / "forbidden"),
                ])
            self.assertFalse(output.exists())

    def test_report_is_exclusive_create_and_canonical(self):
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            bundle = _transcript(root / "b", [
                [_publication("r1", 1000, 5000)],
                [_publication("r1", 1100, 5000, pickup_delta=100)],
            ])
            output = root / "out" / "report.json"
            tool.main([
                "--bundle", str(bundle), "--label", "unit",
                "--output", str(output),
            ])
            first = output.read_bytes()
            self.assertEqual(first, tool.canonical(json.loads(first)))
            with self.assertRaises(FileExistsError):
                tool.main([
                    "--bundle", str(bundle), "--label", "unit",
                    "--output", str(output),
                ])
            self.assertEqual(output.read_bytes(), first)

    def test_missing_transcript_fails_closed(self):
        with tempfile.TemporaryDirectory() as directory:
            empty = pathlib.Path(directory) / "empty"
            empty.mkdir()
            with self.assertRaises(tool.DirectionError):
                tool.scan_bundle(empty)


if __name__ == "__main__":
    unittest.main()
