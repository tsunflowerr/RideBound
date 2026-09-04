"""Tests for the WP9 confirmatory H6 freeze v6.

Three properties, each one a defect this project actually hit.

A freeze must seal its own builder. `wp14_freeze_v2.py` was copied from v1
and kept sealing v1's builder, so a v2 receipt would have verified while its
own source drifted freely.

A seal must not depend on compiled bytecode. v5 filtered excluded names with
`path.name not in excluded`, and __pycache__ is a directory, so every seal
hashed .pyc files. That is why v5's adapter seal
7fc7e1b8f84ff7777cdb144655ac18de395c8e94d2dd1983d4ecd8a5040a128d is not
reproducible from the working tree nor from any of the four commits that ever
touched the package: the bytes H6 ran under existed only in an uncommitted
tree, and the bytecode could not have been reproduced even if they had not.

A superseded receipt must stay exactly as it is and must refuse to verify.
freeze-receipt-v5.json is the record of what the original H6 run executed
under, including that provenance gap, so it is retained untouched.
"""

import hashlib
import importlib.util
import json
import pathlib
import shutil
import subprocess
import sys
import tempfile
import unittest


_HERE = pathlib.Path(__file__).resolve().parent
_ADAPTER = _HERE.parent
_REPOSITORY = _ADAPTER.parent.parent
_MODULE_PATH = _ADAPTER / "wp9_freeze_v6.py"
_RECEIPT = (
    _REPOSITORY
    / "benchmarks/scenarios/wp9-confirmatory/freeze-receipt-v6.json"
)
_RETAINED_V5 = (
    _REPOSITORY
    / "benchmarks/scenarios/wp9-confirmatory/freeze-receipt-v5.json"
)
_V5_MODULE_PATH = _ADAPTER / "wp9_freeze_verify.py"
_RUNNER = pathlib.Path(r"E:\RideBoundData\wp9\runner\h6-confirmatory-v1")

_SPEC = importlib.util.spec_from_file_location("wp9_freeze_v6", _MODULE_PATH)
_MODULE = importlib.util.module_from_spec(_SPEC)
_SPEC.loader.exec_module(_MODULE)


class SelfSealTests(unittest.TestCase):
    def test_the_receipt_seals_its_own_builder(self):
        sealed = _MODULE._REPOSITORY_HASH_FIELDS
        self.assertEqual(
            sealed["freezeVerifierProgramSha256"],
            "simulators/fleetpy-ridebound/wp9_freeze_v6.py",
        )
        self.assertNotIn(
            "simulators/fleetpy-ridebound/wp9_freeze_verify.py",
            sealed.values(),
        )

    def test_the_retained_v5_receipt_is_bound_by_hash(self):
        self.assertEqual(
            _MODULE._REPOSITORY_HASH_FIELDS["supersedesFreezeReceiptV5Sha256"],
            "benchmarks/scenarios/wp9-confirmatory/freeze-receipt-v5.json",
        )


class SealExcludesBytecodeTests(unittest.TestCase):
    def test_a_pycache_file_cannot_change_a_seal(self):
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            (root / "mapping.py").write_bytes(b"source")
            cache = root / "__pycache__"
            cache.mkdir()
            (cache / "mapping.cpython-310.pyc").write_bytes(b"first")
            before = _MODULE._tree_sha256(root, b"d", {"__pycache__"})
            (cache / "mapping.cpython-310.pyc").write_bytes(b"second-different")
            self.assertEqual(
                before, _MODULE._tree_sha256(root, b"d", {"__pycache__"})
            )
            (root / "mapping.py").write_bytes(b"changed")
            self.assertNotEqual(
                before, _MODULE._tree_sha256(root, b"d", {"__pycache__"})
            )

    def test_v5_hashed_bytecode_and_v6_does_not(self):
        """The exact defect, pinned by comparing the two implementations."""
        spec = importlib.util.spec_from_file_location(
            "wp9_freeze_v5_for_contrast", _V5_MODULE_PATH
        )
        v5 = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(v5)
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            (root / "mapping.py").write_bytes(b"source")
            cache = root / "__pycache__"
            cache.mkdir()
            (cache / "mapping.cpython-310.pyc").write_bytes(b"first")
            v5_before = v5._tree_sha256(root, b"d", {"__pycache__"})
            v6_before = _MODULE._tree_sha256(root, b"d", {"__pycache__"})
            (cache / "mapping.cpython-310.pyc").write_bytes(b"recompiled")
            self.assertNotEqual(
                v5_before, v5._tree_sha256(root, b"d", {"__pycache__"}),
                "v5 was supposed to be sensitive to bytecode",
            )
            self.assertEqual(
                v6_before, _MODULE._tree_sha256(root, b"d", {"__pycache__"})
            )

    def test_the_live_adapter_seal_ignores_the_real_pycache(self):
        package = _REPOSITORY / "simulators/fleetpy-ridebound/ridebound_fleetpy"
        live = _MODULE._tree_sha256(
            package, b"RideBound.Wp9AdapterPackage.v1", {"__pycache__"}
        )
        with tempfile.TemporaryDirectory() as directory:
            copy = pathlib.Path(directory) / "pkg"
            shutil.copytree(
                package, copy, ignore=shutil.ignore_patterns("__pycache__")
            )
            self.assertEqual(
                live,
                _MODULE._tree_sha256(
                    copy, b"RideBound.Wp9AdapterPackage.v1", {"__pycache__"}
                ),
            )


class RetainedV5Tests(unittest.TestCase):
    def test_the_v5_receipt_bytes_are_untouched(self):
        raw = _RETAINED_V5.read_bytes()
        self.assertEqual(
            hashlib.sha256(raw).hexdigest(),
            "84f6eff31addbdd12349a19201d79c872fbd05aaf5e0aa45dd73aee6d5c3dee2",
        )

    def test_v5_no_longer_verifies(self):
        completed = subprocess.run(
            [
                sys.executable, "-B", str(_V5_MODULE_PATH),
                "--receipt", str(_RETAINED_V5),
                "--repository", str(_REPOSITORY),
                "--runner-root", str(_RUNNER),
            ],
            capture_output=True, text=True, encoding="utf-8", check=False,
        )
        self.assertNotEqual(completed.returncode, 0)
        self.assertIn("freeze hash differs", completed.stderr)


class ReceiptTests(unittest.TestCase):
    def setUp(self):
        if not _RECEIPT.is_file():                      # pragma: no cover
            self.skipTest("freeze-receipt-v6.json is not built yet")
        self.receipt = json.loads(_RECEIPT.read_text(encoding="utf-8"))

    def test_identity_is_v6(self):
        self.assertEqual(self.receipt["schemaVersion"], "6.0.0")
        self.assertEqual(self.receipt["freezeId"], _MODULE.FREEZE_ID)

    def test_the_design_counts_match_v5_exactly(self):
        retained = json.loads(_RETAINED_V5.read_text(encoding="utf-8"))
        for field in (
            "plannedPrimaryRunCount",
            "plannedRobustnessRunCount",
            "plannedPanelBPrimaryRunCount",
            "experimentalUnitCount",
            "requestCountPerRun",
            "solverSeedsAreReplicates",
            "gridSha256",
            "executionPlanSha256",
            "panelBExecutionPlanSha256",
            "analysisManifestSha256",
            "panelBAnalysisManifestSha256",
            "robustnessManifestSha256",
            "preregistrationSha256",
        ):
            with self.subTest(field=field):
                self.assertEqual(self.receipt[field], retained[field])

    def test_it_verifies_and_the_builder_reproduces_it(self):
        result = _MODULE.verify_receipt(_RECEIPT, _REPOSITORY, _RUNNER)
        self.assertEqual(result["status"], "pass")
        rebuilt = _MODULE.build(
            _REPOSITORY, _RUNNER, self.receipt["frozenAtUtc"]
        )
        self.assertEqual(rebuilt, self.receipt)
        self.assertEqual(
            _RECEIPT.read_bytes(), _MODULE.canonical(rebuilt) + b"\r\n"
        )

    def test_write_never_overwrites(self):
        completed = subprocess.run(
            [
                sys.executable, "-B", str(_MODULE_PATH),
                "--receipt", str(_RECEIPT),
                "--repository", str(_REPOSITORY),
                "--runner-root", str(_RUNNER),
                "--frozen-at-utc", "2026-09-02T00:00:00Z",
                "--write",
            ],
            capture_output=True, text=True, encoding="utf-8", check=False,
        )
        self.assertNotEqual(completed.returncode, 0)
        self.assertIn("already exists", completed.stderr)


if __name__ == "__main__":
    unittest.main()
