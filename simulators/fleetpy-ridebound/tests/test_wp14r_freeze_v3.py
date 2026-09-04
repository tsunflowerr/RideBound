"""Tests for WP14R protocol freeze v3.

The first test in this file is the one that did not exist for freeze v2, and its
absence is the whole reason `RB-WP14R-008` burned both attempts of its first job
without running a single simulation epoch. Freeze v2 was only ever verified from a
full developer shell. The supervised child runs under the receipt's own environment
allowlist, and under that allowlist `platform.machine()` returned an empty string on
Windows, so the child computed a host fingerprint the receipt could never match.

A freeze that its own child cannot verify is not a freeze. That property is now
pinned by executing the verifier in a subprocess whose environment is exactly the
allowlist the receipt grants.
"""

import importlib.util
import json
import os
import pathlib
import subprocess
import sys
import unittest

_HERE = pathlib.Path(__file__).resolve().parent
_ADAPTER = _HERE.parent
_REPOSITORY = _ADAPTER.parent.parent
_FREEZE_V3 = _ADAPTER / "wp14r_freeze_v3.py"
_RECEIPT = (
    _REPOSITORY
    / "benchmarks/scenarios/wp14r-development/freeze-v3-authorization.json"
)

_spec = importlib.util.spec_from_file_location("wp14r_freeze_v3_under_test", _FREEZE_V3)
freeze = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(freeze)


def _child_environment(receipt):
    """Exactly the environment the receipt grants a supervised child."""
    names = receipt["protocol"]["inheritedEnvironmentNames"]
    folded = {key.casefold(): value for key, value in os.environ.items()}
    folded["pythondontwritebytecode"] = "1"
    missing = [name for name in names if name.casefold() not in folded]
    if missing:
        raise AssertionError(f"host lacks allowlisted variables: {missing}")
    return {name: folded[name.casefold()] for name in names}


class FreezeV3ChildEnvironmentTests(unittest.TestCase):
    def setUp(self):
        self.assertTrue(_RECEIPT.is_file(), "freeze v3 receipt must be built")
        self.receipt = json.loads(_RECEIPT.read_text(encoding="utf-8"))

    def test_receipt_is_superseded_and_no_longer_rebuildable(self):
        """Freeze v3 is now a retained record, not a live protocol.

        v3 fixed the child-environment defect, and that regression lives on in
        `test_wp14r_freeze_v4.py`. v3 itself is superseded: its receipt binds the
        verifier source that freeze v4 had to narrow after a 1.846 s host clock
        step made a completed, otherwise clean run unverifiable. Rebuilding it
        must therefore fail loudly rather than silently drift.
        """
        completed = subprocess.run(
            [
                sys.executable, "-B", str(_FREEZE_V3),
                "--repository", str(_REPOSITORY),
                "--output", str(_RECEIPT),
            ],
            env=_child_environment(self.receipt),
            capture_output=True, text=True, encoding="utf-8", check=False,
        )
        self.assertNotEqual(completed.returncode, 0)
        # The reason moved as later freezes landed: first the rebuilt 006
        # gate, then the re-frozen scientific design. Both are supersession,
        # so the test pins that it fails loudly rather than which message.
        self.assertIn("WP14R_FREEZE_V3_ERROR", completed.stderr)

    def test_the_retained_receipt_bytes_are_untouched(self):
        raw = _RECEIPT.read_bytes()
        self.assertEqual(
            freeze.sha256_bytes(raw),
            "07baeda2b79f31b5d79318755afbe917b7a8a47a7509b3f09a1756a484fa9227",
        )
        self.assertEqual(raw, freeze.canonical(json.loads(raw)) + b"\n")

    def test_host_fingerprint_is_stable_under_the_child_environment(self):
        """The exact root cause: machine() read an environment variable."""
        script = (
            "import importlib.util;"
            f"s=importlib.util.spec_from_file_location('sup',r'{_ADAPTER}\\\\"
            "wp14r_supervised_process.py');"
            "m=importlib.util.module_from_spec(s);s.loader.exec_module(m);"
            "print(m.host_fingerprint()[1])"
        )
        parent = subprocess.run(
            [sys.executable, "-B", "-c", script],
            capture_output=True, text=True, encoding="utf-8", check=True,
        ).stdout.strip()
        child = subprocess.run(
            [sys.executable, "-B", "-c", script],
            env=_child_environment(self.receipt),
            capture_output=True, text=True, encoding="utf-8", check=True,
        ).stdout.strip()
        self.assertEqual(parent, child)
        self.assertEqual(
            parent, self.receipt["protocol"]["hostPolicy"][
                "requiredHostFingerprintSha256"
            ]
        )


class FreezeV3ContractTests(unittest.TestCase):
    def setUp(self):
        self.assertTrue(_RECEIPT.is_file(), "freeze v3 receipt must be built")
        self.receipt = json.loads(_RECEIPT.read_text(encoding="utf-8"))

    def test_environment_allowlist_grants_processor_architecture(self):
        names = self.receipt["protocol"]["inheritedEnvironmentNames"]
        self.assertIn("PROCESSOR_ARCHITECTURE", names)
        self.assertEqual(names, sorted(names, key=str.casefold))
        self.assertEqual(len(names), len(set(names)))

    def test_identity_is_v3_and_roots_are_separate_from_v2(self):
        self.assertEqual(self.receipt["freezeId"], "wp14r-resilient-development-v3")
        self.assertEqual(self.receipt["schemaVersion"], "3.0.0")
        isolation = self.receipt["protocol"]["isolation"]
        self.assertNotIn("development-v2", isolation["ledgerRoot"])
        self.assertNotIn("development-v2", isolation["controlRoot"])

    def test_the_exhausted_v2_roots_are_forbidden(self):
        forbidden = self.receipt["protocol"]["isolation"]["forbiddenRoots"]
        self.assertEqual(len(forbidden), 7)
        # Literal paths on purpose: comparing against the module constants
        # would pass even if those constants were wrong.
        self.assertIn(r"E:\RideBoundData\wp14r\development-v2-ledger", forbidden)
        self.assertIn(r"E:\RideBoundData\wp14r\development-v2-control", forbidden)
        self.assertTrue(str(freeze.V2_LEDGER_ROOT).endswith("-v2-ledger"))
        self.assertTrue(str(freeze.V2_CONTROL_ROOT).endswith("-v2-control"))

    def test_ledger_and_control_roots_are_both_v3(self):
        isolation = self.receipt["protocol"]["isolation"]
        self.assertTrue(isolation["ledgerRoot"].endswith("development-v3-ledger"))
        self.assertTrue(isolation["controlRoot"].endswith("development-v3-control"))
        self.assertNotEqual(isolation["ledgerRoot"], isolation["controlRoot"])

    def test_scientific_design_is_inherited_byte_exact_from_wp14_v1(self):
        base = self.receipt["baseScientificFreeze"]
        self.assertEqual(base["jobCount"], 160)
        self.assertEqual(
            base["artifact"]["sha256"], freeze.EXPECTED_BASE_RECEIPT_SHA256
        )
        self.assertEqual(self.receipt["protocol"]["fullMatrix"]["jobCount"], 160)

    def test_host_thresholds_are_unchanged_from_v2(self):
        policy = self.receipt["protocol"]["hostPolicy"]
        self.assertEqual(policy["minimumAvailableMemoryBytes"], 8 * 1024**3)
        self.assertEqual(policy["maximumMeanCpuBusyPercent"], 20)
        self.assertEqual(policy["requiredAcLineStatus"], "online")

    def test_receipt_bytes_are_canonical(self):
        raw = _RECEIPT.read_bytes()
        self.assertEqual(raw, freeze.canonical(json.loads(raw)) + b"\n")

    def test_a_mutated_receipt_is_rejected(self):
        # build() can no longer run for a superseded freeze, so the retained
        # receipt stands in for it. The property under test is unchanged: a
        # mutated receipt must not verify against the authorized document.
        import tempfile
        from unittest import mock

        mutated = json.loads(_RECEIPT.read_text(encoding="utf-8"))
        mutated["protocol"]["inheritedEnvironmentNames"] = ["PATH"]
        with tempfile.TemporaryDirectory() as directory:
            path = pathlib.Path(directory) / "freeze.json"
            path.write_bytes(freeze.canonical(mutated) + b"\n")
            with mock.patch.object(freeze, "build", return_value=self.receipt):
                with self.assertRaises(freeze.FreezeV3Error):
                    freeze.verify_receipt(path, _REPOSITORY)


if __name__ == "__main__":
    unittest.main()
