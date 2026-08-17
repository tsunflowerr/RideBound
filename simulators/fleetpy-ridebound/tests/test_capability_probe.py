from __future__ import annotations

import importlib.util
import json
import pathlib
import sys
import tempfile
import unittest
from unittest import mock


MODULE_PATH = pathlib.Path(__file__).parents[1] / "capability_probe.py"
SPEC = importlib.util.spec_from_file_location("ridebound_capability_probe", MODULE_PATH)
assert SPEC is not None and SPEC.loader is not None
PROBE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = PROBE
SPEC.loader.exec_module(PROBE)


class CapabilityProbeFailureTests(unittest.TestCase):
    def setUp(self) -> None:
        self.matrix = json.loads(PROBE.MATRIX_PATH.read_text(encoding="utf-8"))

    def test_commit_drift_fails_before_any_file_or_adapter_import(self) -> None:
        values = iter(
            [
                "0" * 40,
                self.matrix["fleetPy"]["annotatedTagObject"],
                "tag",
                self.matrix["fleetPy"]["commit"],
            ]
        )
        with tempfile.TemporaryDirectory() as directory, mock.patch.object(
            PROBE, "_git", side_effect=lambda *_args: next(values)
        ), mock.patch.object(PROBE, "_sha256") as sha:
            with self.assertRaisesRegex(PROBE.ProbeFailure, "head=") as raised:
                PROBE._verify_source(pathlib.Path(directory), self.matrix)
        self.assertEqual("RBWP7_SOURCE_COMMIT_DRIFT", raised.exception.code)
        sha.assert_not_called()

    def test_lightweight_tag_is_rejected(self) -> None:
        expected = self.matrix["fleetPy"]
        values = iter(
            [expected["commit"], expected["commit"], "commit", expected["commit"]]
        )
        with tempfile.TemporaryDirectory() as directory, mock.patch.object(
            PROBE, "_git", side_effect=lambda *_args: next(values)
        ):
            with self.assertRaises(PROBE.ProbeFailure) as raised:
                PROBE._verify_source(pathlib.Path(directory), self.matrix)
        self.assertEqual("RBWP7_SOURCE_TAG_DRIFT", raised.exception.code)

    def test_dirty_checkout_is_rejected_before_hashing(self) -> None:
        expected = self.matrix["fleetPy"]
        values = iter(
            [
                expected["commit"],
                expected["annotatedTagObject"],
                "tag",
                expected["commit"],
                " M src/routing/NetworkBase.py",
            ]
        )
        with tempfile.TemporaryDirectory() as directory, mock.patch.object(
            PROBE, "_git", side_effect=lambda *_args: next(values)
        ):
            with self.assertRaises(PROBE.ProbeFailure) as raised:
                PROBE._verify_source(pathlib.Path(directory), self.matrix)
        self.assertEqual("RBWP7_SOURCE_DIRTY", raised.exception.code)

    def test_package_version_drift_has_stable_failure_code(self) -> None:
        with mock.patch.object(PROBE.importlib.metadata, "version", return_value="0.0.0"):
            with self.assertRaises(PROBE.ProbeFailure) as raised:
                PROBE._verify_environment(self.matrix)
        self.assertEqual("RBWP7_PACKAGE_VERSION_DRIFT", raised.exception.code)


if __name__ == "__main__":
    unittest.main()
