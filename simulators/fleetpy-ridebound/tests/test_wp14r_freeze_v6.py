"""Tests for WP14R protocol freeze v4.

Freeze v4 carries two fixes that were each found by actually running, not by
testing: freeze v2 could not verify its own receipt inside the supervised child,
and freeze v3 could run a job but its independent verifier then rejected the
completed run because a host NTP step moved the wall clock backwards inside a
retained attempt.

Two of the tests here exist because a generated-by-substitution module silently
swapped `LEDGER_ROOT` with `V3_LEDGER_ROOT` — `V3_LEDGER_ROOT` contains
`LEDGER_ROOT` as a substring. Asserting against the module constants would have
passed while the constants themselves were wrong, so the root assertions compare
against literal paths.
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
_FREEZE_V6 = _ADAPTER / "wp14r_freeze_v6.py"
_RECEIPT = (
    _REPOSITORY
    / "benchmarks/scenarios/wp14r-development/freeze-v6-authorization.json"
)

_spec = importlib.util.spec_from_file_location("wp14r_freeze_v6_under_test", _FREEZE_V6)
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


class FreezeV6Tests(unittest.TestCase):
    def setUp(self):
        self.assertTrue(_RECEIPT.is_file(), "freeze v6 receipt must be built")
        self.receipt = json.loads(_RECEIPT.read_text(encoding="utf-8"))

    def test_receipt_verifies_under_the_exact_child_environment(self):
        """The freeze-v2 regression, carried forward."""
        completed = subprocess.run(
            [
                sys.executable, "-B", str(_FREEZE_V6),
                "--repository", str(_REPOSITORY),
                "--output", str(_RECEIPT),
            ],
            env=_child_environment(self.receipt),
            capture_output=True, text=True, encoding="utf-8", check=False,
        )
        self.assertEqual(
            completed.returncode, 0,
            f"child-environment verify failed: {completed.stderr.strip()}",
        )
        self.assertEqual(
            json.loads(completed.stdout.strip().splitlines()[-1])["status"], "valid"
        )

    def test_environment_allowlist_grants_processor_architecture(self):
        names = self.receipt["protocol"]["inheritedEnvironmentNames"]
        self.assertIn("PROCESSOR_ARCHITECTURE", names)
        self.assertEqual(names, sorted(names, key=str.casefold))

    def test_live_roots_are_v6_literal_paths(self):
        isolation = self.receipt["protocol"]["isolation"]
        self.assertEqual(
            isolation["ledgerRoot"],
            r"E:\RideBoundData\wp14r\development-v6-ledger",
        )
        self.assertEqual(
            isolation["controlRoot"],
            r"E:\RideBoundData\wp14r\development-v6-control",
        )

    def test_both_predecessor_histories_are_forbidden(self):
        forbidden = self.receipt["protocol"]["isolation"]["forbiddenRoots"]
        self.assertEqual(len(forbidden), 13)
        for literal in (
            r"E:\RideBoundData\wp14r\development-v2-ledger",
            r"E:\RideBoundData\wp14r\development-v2-control",
            r"E:\RideBoundData\wp14r\development-v3-ledger",
            r"E:\RideBoundData\wp14r\development-v3-control",
            r"E:\RideBoundData\wp14r\development-v4-ledger",
            r"E:\RideBoundData\wp14r\development-v4-control",
            r"E:\RideBoundData\wp14r\development-v5-ledger",
            r"E:\RideBoundData\wp14r\development-v5-control",
        ):
            self.assertIn(literal, forbidden)

    def test_live_roots_are_not_in_the_forbidden_set(self):
        isolation = self.receipt["protocol"]["isolation"]
        forbidden = isolation["forbiddenRoots"]
        self.assertNotIn(isolation["ledgerRoot"], forbidden)
        self.assertNotIn(isolation["controlRoot"], forbidden)

    def test_gate_006_artifacts_are_the_rebuilt_ones(self):
        paths = [
            item["path"] for item in self.receipt["sourceIdentity"][
                "mechanicsGateArtifacts"
            ]
        ]
        joined = " ".join(paths)
        self.assertIn("independent-fixtures-v3-20260831", joined)
        self.assertIn("independent-mutation-v5-20260831", joined)

    def test_scientific_design_is_inherited_byte_exact_from_wp14_v1(self):
        base = self.receipt["baseScientificFreeze"]
        self.assertEqual(base["jobCount"], 160)
        self.assertEqual(
            base["artifact"]["sha256"], freeze.EXPECTED_BASE_RECEIPT_SHA256
        )
        self.assertEqual(self.receipt["protocol"]["fullMatrix"]["jobCount"], 160)

    def test_host_thresholds_are_unchanged(self):
        policy = self.receipt["protocol"]["hostPolicy"]
        self.assertEqual(policy["minimumAvailableMemoryBytes"], 8 * 1024**3)
        self.assertEqual(policy["maximumMeanCpuBusyPercent"], 20)
        self.assertEqual(policy["requiredAcLineStatus"], "online")

    def test_identity_is_v6(self):
        self.assertEqual(self.receipt["freezeId"], "wp14r-resilient-development-v6")
        self.assertEqual(self.receipt["schemaVersion"], "6.0.0")
        self.assertEqual(
            self.receipt["protocol"]["protocolId"],
            "wp14r-supervised-scientific-job-v6",
        )

    def test_receipt_bytes_are_canonical(self):
        raw = _RECEIPT.read_bytes()
        self.assertEqual(raw, freeze.canonical(json.loads(raw)) + b"\n")


if __name__ == "__main__":
    unittest.main()
