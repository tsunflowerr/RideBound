import importlib.util
import json
import pathlib
import tempfile
import unittest

import jsonschema


ROOT = pathlib.Path(__file__).resolve().parents[3]
MODULE_PATH = (
    ROOT / "simulators/fleetpy-ridebound/wp14r_attempt_ledger.py"
)
SPEC = importlib.util.spec_from_file_location(
    "wp14r_attempt_ledger_under_test", MODULE_PATH
)
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class Wp14RAttemptLedgerTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.base = pathlib.Path(self.temporary.name)
        self.ledger = self.base / "ledger"
        self.forbidden = [self.base / "frozen-raw"]
        self.job_id = "w14r-test-job"
        self.hash_a = "a" * 64
        self.hash_b = "b" * 64
        self.hash_c = "c" * 64

    def tearDown(self):
        self.temporary.cleanup()

    def begin(self, started="2026-08-26T01:00:00Z"):
        return MODULE.begin_attempt(
            self.ledger,
            self.job_id,
            "wp14r-test-freeze-v1",
            self.hash_a,
            self.hash_b,
            self.hash_c,
            self.forbidden,
            started,
        )

    def terminalize(
        self,
        number,
        status="notRun",
        classification="processExitFailure",
        terminal="2026-08-26T01:01:00Z",
        **overrides,
    ):
        values = {
            "ledger_root": self.ledger,
            "job_id": self.job_id,
            "attempt_number": number,
            "exit_classification": classification,
            "elapsed_ms": 1000,
            "process_tree_status": "terminated",
            "bundle_verification_status": status,
            "forbidden_roots": self.forbidden,
            "process_exit_code": 1,
            "terminal_utc": terminal,
        }
        values.update(overrides)
        return MODULE.terminalize_attempt(**values)

    def attempt_path(self, number):
        return self.ledger / self.job_id / f"attempt-{number:02d}"

    def test_empty_ledger_and_start_are_strict_and_canonical(self):
        report = MODULE.inspect_ledger(
            self.ledger, self.job_id, self.forbidden
        )
        self.assertEqual("readyInitial", report["ledgerState"])
        start = self.begin()
        self.assertFalse(
            start["retryPolicy"]["attemptsAreExperimentalUnits"]
        )
        self.assertFalse(
            start["outcomeAccessPolicy"][
                "mayReadScientificOutcomeToAuthorizeRecovery"
            ]
        )
        raw = (self.attempt_path(1) / "attempt-start.json").read_bytes()
        self.assertEqual(MODULE.canonical(start), raw)
        self.assertEqual(
            "attemptOpen",
            MODULE.inspect_ledger(
                self.ledger, self.job_id, self.forbidden
            )["ledgerState"],
        )

    def test_exclusive_start_and_prior_open_block_recovery(self):
        self.begin()
        with self.assertRaisesRegex(MODULE.LedgerError, "does not authorize"):
            self.begin("2026-08-26T01:02:00Z")
        self.assertEqual(
            ["attempt-01"],
            [
                child.name
                for child in (self.ledger / self.job_id).iterdir()
                if child.is_dir()
            ],
        )

    def test_first_failure_authorizes_exactly_one_recovery(self):
        self.begin()
        terminal = self.terminalize(1)
        self.assertEqual("recoveryAuthorized", terminal["retryDisposition"])
        second = self.begin("2026-08-26T01:02:00Z")
        self.assertEqual(2, second["attemptNumber"])
        self.assertEqual(
            f"{self.job_id}-attempt-01", second["previousAttemptId"]
        )
        report = MODULE.inspect_ledger(
            self.ledger, self.job_id, self.forbidden
        )
        self.assertEqual("attemptOpen", report["ledgerState"])
        self.assertEqual(2, report["openAttemptNumber"])

    def test_second_failure_exhausts_without_attempt_three(self):
        self.begin()
        self.terminalize(1)
        self.begin("2026-08-26T01:02:00Z")
        terminal = self.terminalize(
            2, terminal="2026-08-26T01:03:00Z"
        )
        self.assertEqual("attemptsExhausted", terminal["retryDisposition"])
        report = MODULE.inspect_ledger(
            self.ledger, self.job_id, self.forbidden
        )
        self.assertEqual("exhausted", report["ledgerState"])
        with self.assertRaisesRegex(MODULE.LedgerError, "does not authorize"):
            self.begin("2026-08-26T01:04:00Z")

    def test_verified_success_selects_first_bundle_and_blocks_retry(self):
        self.begin()
        output = self.attempt_path(1) / "output"
        output.mkdir()
        (output / "bundle-manifest.json").write_text(
            "{}", encoding="utf-8"
        )
        terminal = self.terminalize(
            1,
            status="pass",
            classification="success",
            process_exit_code=0,
            process_tree_status="exitedCleanly",
            verifier_id="independent-bundle-verifier-v1",
            verifier_sha256=self.hash_a,
            behavioral_hash=self.hash_b,
        )
        self.assertEqual("terminalSuccess", terminal["retryDisposition"])
        report = MODULE.inspect_ledger(
            self.ledger, self.job_id, self.forbidden
        )
        self.assertEqual("succeeded", report["ledgerState"])
        self.assertEqual(
            f"{self.job_id}-attempt-01", report["selectedValidAttemptId"]
        )
        with self.assertRaisesRegex(MODULE.LedgerError, "does not authorize"):
            self.begin("2026-08-26T01:02:00Z")

    def test_pass_requires_manifest_verifier_and_behavioral_hash(self):
        self.begin()
        with self.assertRaisesRegex(MODULE.LedgerError, "manifest"):
            self.terminalize(
                1,
                status="pass",
                classification="success",
                process_exit_code=0,
                process_tree_status="exitedCleanly",
                verifier_id="verifier",
                verifier_sha256=self.hash_a,
                behavioral_hash=self.hash_b,
            )
        output = self.attempt_path(1) / "output"
        output.mkdir()
        (output / "bundle-manifest.json").write_text(
            "{}", encoding="utf-8"
        )
        with self.assertRaisesRegex(MODULE.LedgerError, "independent evidence"):
            self.terminalize(
                1,
                status="pass",
                classification="success",
                process_exit_code=0,
                process_tree_status="exitedCleanly",
                verifier_id="verifier",
                verifier_sha256=self.hash_a,
            )

    def test_stale_open_cannot_claim_recovery_with_an_uncertain_tree(self):
        self.begin()
        report = MODULE.inspect_ledger(
            self.ledger, self.job_id, self.forbidden
        )
        self.assertEqual("attemptOpen", report["ledgerState"])
        with self.assertRaisesRegex(MODULE.LedgerError, "proven-safe"):
            self.terminalize(
                1,
                classification="launcherRecoveredOrphanedStart",
                process_exit_code=None,
                process_tree_status="uncertain",
            )
        self.assertEqual(
            "attemptsExhausted",
            MODULE.expected_disposition(
                1, "notRun", "processTreeUncertain", "uncertain"
            ),
        )

    def test_terminal_process_and_verifier_semantics_fail_closed(self):
        self.begin()
        with self.assertRaisesRegex(MODULE.LedgerError, "non-zero"):
            self.terminalize(1, process_exit_code=0)
        with self.assertRaisesRegex(MODULE.LedgerError, "verifier identity"):
            self.terminalize(
                1,
                status="fail",
                classification="verifierFailure",
                process_exit_code=0,
                process_tree_status="exitedCleanly",
            )

    def test_terminal_inventory_detects_log_and_output_tamper(self):
        self.begin()
        attempt = self.attempt_path(1)
        (attempt / "process.log").write_bytes(b"before")
        output = attempt / "output"
        output.mkdir()
        (output / "partial.bin").write_bytes(b"partial")
        self.terminalize(1)
        (attempt / "process.log").write_bytes(b"after")
        with self.assertRaisesRegex(MODULE.LedgerError, "log changed"):
            MODULE.inspect_ledger(self.ledger, self.job_id, self.forbidden)
        (attempt / "process.log").write_bytes(b"before")
        (output / "partial.bin").write_bytes(b"mutated")
        with self.assertRaisesRegex(MODULE.LedgerError, "output changed"):
            MODULE.inspect_ledger(self.ledger, self.job_id, self.forbidden)

    def test_receipt_tamper_and_noncanonical_bytes_fail_closed(self):
        self.begin()
        start_path = self.attempt_path(1) / "attempt-start.json"
        start_path.write_bytes(start_path.read_bytes() + b"\n")
        with self.assertRaisesRegex(MODULE.LedgerError, "byte-canonical"):
            MODULE.inspect_ledger(self.ledger, self.job_id, self.forbidden)

    def test_gap_extra_attempt_and_changed_recovery_binding_are_rejected(self):
        self.begin()
        self.terminalize(1)
        first = self.attempt_path(1)
        first.rename(self.attempt_path(2))
        with self.assertRaisesRegex(MODULE.LedgerError, "gap"):
            MODULE.inspect_ledger(self.ledger, self.job_id, self.forbidden)
        self.attempt_path(2).rename(first)
        (self.ledger / self.job_id / "attempt-03").mkdir()
        with self.assertRaisesRegex(MODULE.LedgerError, "unexpected ledger"):
            MODULE.inspect_ledger(self.ledger, self.job_id, self.forbidden)
        (self.ledger / self.job_id / "attempt-03").rmdir()
        with self.assertRaisesRegex(MODULE.LedgerError, "exact job binding"):
            MODULE.begin_attempt(
                self.ledger,
                self.job_id,
                "wp14r-test-freeze-v1",
                self.hash_a,
                self.hash_b,
                "d" * 64,
                self.forbidden,
                "2026-08-26T01:02:00Z",
            )
        self.assertFalse(self.attempt_path(2).exists())
        self.begin("2026-08-26T01:02:00Z")
        start_path = self.attempt_path(2) / "attempt-start.json"
        document = json.loads(start_path.read_text(encoding="utf-8"))
        document["commandSha256"] = "d" * 64
        start_path.write_bytes(MODULE.canonical(document))
        with self.assertRaisesRegex(MODULE.LedgerError, "changed its job binding"):
            MODULE.inspect_ledger(self.ledger, self.job_id, self.forbidden)

    def test_forbidden_overlap_and_unsafe_job_id_fail_closed(self):
        with self.assertRaisesRegex(MODULE.LedgerError, "overlaps"):
            MODULE.inspect_ledger(
                self.base / "frozen-raw/ledger",
                self.job_id,
                self.forbidden,
            )
        with self.assertRaisesRegex(MODULE.LedgerError, "canonical identifier"):
            MODULE.inspect_ledger(self.ledger, "../escape", self.forbidden)
        with self.assertRaisesRegex(MODULE.LedgerError, "forbidden root"):
            MODULE.inspect_ledger(self.ledger, self.job_id, [])

    def test_timestamp_order_and_inspection_schema_are_enforced(self):
        self.begin("2026-08-26T01:02:00Z")
        with self.assertRaisesRegex(MODULE.LedgerError, "precedes"):
            self.terminalize(1, terminal="2026-08-26T01:01:00Z")
        report = MODULE.inspect_ledger(
            self.ledger, self.job_id, self.forbidden
        )
        schema = MODULE.load_schema("ledger-inspection.schema.json")
        jsonschema.Draft202012Validator(schema).validate(report)
        mutant = dict(report)
        mutant["claimBoundary"] = ["mechanicalOnly"]
        with self.assertRaises(jsonschema.ValidationError):
            jsonschema.Draft202012Validator(schema).validate(mutant)


if __name__ == "__main__":
    unittest.main()
