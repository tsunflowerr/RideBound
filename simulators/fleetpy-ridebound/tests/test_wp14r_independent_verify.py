import ast
import hashlib
import importlib.util
import json
import os
import pathlib
import shutil
import subprocess
import sys
import tempfile
import unittest
from unittest import mock

import jsonschema


ROOT = pathlib.Path(__file__).resolve().parents[3]
ADAPTER = ROOT / "simulators/fleetpy-ridebound"


def load_module(name, filename):
    specification = importlib.util.spec_from_file_location(name, ADAPTER / filename)
    module = importlib.util.module_from_spec(specification)
    specification.loader.exec_module(module)
    return module


LEDGER = load_module("wp14r_ledger_for_independent_tests", "wp14r_attempt_ledger.py")
SUPERVISOR = load_module(
    "wp14r_supervisor_for_independent_tests",
    "wp14r_supervised_process.py",
)
RECOVERY = load_module(
    "wp14r_recovery_for_independent_tests",
    "wp14r_stale_open_recovery.py",
)
MODULE = load_module(
    "wp14r_independent_verifier_under_test",
    "wp14r_independent_verify.py",
)


class Wp14RIndependentVerifyTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.base = pathlib.Path(self.temporary.name)
        self.ledger = self.base / "ledger"
        self.forbidden = [self.base / "frozen"]
        self.job_id = "independent-job"
        self.environment_names = ["SystemRoot"] if os.name == "nt" else []

    def tearDown(self):
        self.temporary.cleanup()

    @staticmethod
    def arguments(code="print('independent mechanics')"):
        return ["-B", "-c", code]

    def prepare(self, arguments=None, job_id=None):
        arguments = arguments or self.arguments()
        job_id = job_id or self.job_id
        metadata, _ = SUPERVISOR.build_command_binding(
            sys.executable,
            arguments,
            self.base,
            self.environment_names,
            os.environ,
        )
        start = LEDGER.begin_attempt(
            self.ledger,
            job_id,
            "wp14r-independent-test-freeze",
            "a" * 64,
            "b" * 64,
            metadata["commandSha256"],
            self.forbidden,
            "2026-08-27T01:00:00Z",
        )
        return arguments, metadata, start

    def attempt(self, root=None, job_id=None):
        root = root or self.ledger
        job_id = job_id or self.job_id
        return root / job_id / "attempt-01"

    def complete_success(self, legacy=False, mutate=None):
        arguments, _, _ = self.prepare()
        report = SUPERVISOR.supervise_process(
            self.ledger,
            self.job_id,
            1,
            self.forbidden,
            sys.executable,
            arguments,
            self.base,
            self.environment_names,
            wall_timeout_ms=10000,
            heartbeat_interval_ms=50,
            maximum_stream_bytes=65536,
            chunk_bytes=16,
            tree_exit_grace_ms=1000,
        )
        if legacy:
            path = self.attempt() / "process.log"
            records = self.read_records(path)
            for field in MODULE.PROVENANCE_FIELDS:
                records[0]["payload"].pop(field)
            path.write_bytes(self.reseal(records))
        if mutate is not None:
            # Injected before terminalization on purpose: the terminal receipt
            # binds the journal inventory, so a post-terminal edit would trip
            # INVENTORY_MISMATCH instead of the rule under test.
            path = self.attempt() / "process.log"
            records = self.read_records(path)
            mutate(records)
            path.write_bytes(self.reseal(records))
        output = self.attempt() / "output"
        output.mkdir()
        (output / "bundle-manifest.json").write_text("{}", encoding="utf-8")
        LEDGER.terminalize_attempt(
            self.ledger,
            self.job_id,
            1,
            "success",
            100,
            report["treeStatus"],
            "pass",
            self.forbidden,
            process_exit_code=report["processExitCode"],
            verifier_id="synthetic-independent-bundle-verifier",
            verifier_sha256="c" * 64,
            behavioral_hash="d" * 64,
            terminal_utc="2026-08-27T01:01:00Z",
        )

    @staticmethod
    def read_records(path):
        return [json.loads(line) for line in path.read_bytes().splitlines()]

    @staticmethod
    def reseal(records):
        previous = MODULE.ZERO_SHA256
        lines = []
        for sequence, record in enumerate(records):
            record["sequence"] = sequence
            record["previousRecordSha256"] = previous
            projection = {
                key: value
                for key, value in record.items()
                if key != "recordSha256"
            }
            previous = MODULE.sha256_bytes(MODULE.canonical(projection))
            record["recordSha256"] = previous
            lines.append(MODULE.canonical(record) + b"\n")
        return b"".join(lines)

    def verify(self, ledger=None, job_id=None, forbidden=None):
        return MODULE.verify_ledger(
            ledger or self.ledger,
            job_id or self.job_id,
            forbidden or self.forbidden,
            "2026-08-27T02:00:00Z",
        )

    def assert_rejected(self, code, action):
        with self.assertRaises(MODULE.VerificationError) as captured:
            action()
        self.assertEqual(code, captured.exception.code)

    @staticmethod
    def create_directory_link(link, target):
        target.mkdir()
        if os.name != "nt":
            os.symlink(target, link, target_is_directory=True)
            return
        environment = os.environ.copy()
        environment["RB_TEST_LINK"] = str(link)
        environment["RB_TEST_TARGET"] = str(target)
        result = subprocess.run(
            [
                "powershell.exe",
                "-NoProfile",
                "-NonInteractive",
                "-Command",
                (
                    "New-Item -ItemType Junction "
                    "-Path $env:RB_TEST_LINK "
                    "-Target $env:RB_TEST_TARGET "
                    "-ErrorAction Stop | Out-Null"
                ),
            ],
            check=False,
            capture_output=True,
            env=environment,
            text=True,
            timeout=30,
        )
        if result.returncode != 0:
            raise AssertionError(result.stderr)

    def test_implementation_does_not_import_writer_or_existing_verifiers(self):
        source = pathlib.Path(MODULE.__file__).read_text(encoding="utf-8")
        tree = ast.parse(source)
        imported = set()
        for node in ast.walk(tree):
            if isinstance(node, ast.Import):
                imported.update(alias.name for alias in node.names)
            elif isinstance(node, ast.ImportFrom) and node.module:
                imported.add(node.module)
        self.assertNotIn("importlib", imported)
        self.assertFalse(
            imported
            & {
                "wp14r_attempt_ledger",
                "wp14r_supervised_process",
                "wp14r_stale_open_recovery",
            }
        )
        forbidden_calls = {
            "mkdir",
            "write_bytes",
            "write_text",
            "unlink",
            "rename",
        }
        called_attributes = {
            node.func.attr
            for node in ast.walk(tree)
            if isinstance(node, ast.Call)
            and isinstance(node.func, ast.Attribute)
        }
        self.assertFalse(forbidden_calls & called_attributes)
        self.assertNotIn("os.replace", source)

    def test_empty_and_complete_ledgers_validate_against_independent_schema(self):
        empty = self.verify()
        self.assertEqual("readyInitial", empty["ledgerState"])
        self.complete_success()
        report = self.verify()
        self.assertEqual("succeeded", report["ledgerState"])
        self.assertEqual("bound", report["attempts"][0]["journal"]["schemaProvenance"])
        self.assertFalse(report["checks"]["scientificOutcomeFieldsRead"])
        existing = LEDGER.inspect_ledger(
            self.ledger, self.job_id, self.forbidden
        )
        existing_journal = SUPERVISOR.verify_process_log(
            self.attempt() / "process.log"
        )
        self.assertEqual(existing["ledgerState"], report["ledgerState"])
        self.assertEqual(
            existing_journal["status"],
            report["attempts"][0]["journal"]["status"],
        )
        schema = json.loads(
            (
                ROOT
                / "benchmarks/schemas/wp14r/v1/"
                "independent-verification-report.schema.json"
            ).read_text(encoding="utf-8")
        )
        jsonschema.Draft202012Validator(schema).validate(report)

    def test_valid_legacy_journal_is_explicitly_labeled(self):
        self.complete_success(legacy=True)
        report = self.verify()
        self.assertEqual(
            "legacy",
            report["attempts"][0]["journal"]["schemaProvenance"],
        )

    def test_nonzero_and_bundle_verifier_failure_terminal_mappings(self):
        definitions = (
            (
                "nonzero-job",
                self.arguments("import sys;sys.exit(7)"),
                "processExitFailure",
                "notRun",
            ),
            (
                "verifier-failure-job",
                self.arguments("print('mechanical bundle candidate')"),
                "verifierFailure",
                "fail",
            ),
        )
        for job_id, arguments, classification, verification_status in definitions:
            with self.subTest(job_id=job_id):
                self.prepare(arguments, job_id)
                report = SUPERVISOR.supervise_process(
                    self.ledger,
                    job_id,
                    1,
                    self.forbidden,
                    sys.executable,
                    arguments,
                    self.base,
                    self.environment_names,
                    wall_timeout_ms=10000,
                    heartbeat_interval_ms=50,
                    maximum_stream_bytes=65536,
                    chunk_bytes=16,
                    tree_exit_grace_ms=1000,
                )
                LEDGER.terminalize_attempt(
                    self.ledger,
                    job_id,
                    1,
                    classification,
                    100,
                    report["treeStatus"],
                    verification_status,
                    self.forbidden,
                    process_exit_code=report["processExitCode"],
                    verifier_id=(
                        "synthetic-bundle-verifier"
                        if verification_status == "fail"
                        else None
                    ),
                    verifier_sha256=(
                        "a" * 64 if verification_status == "fail" else None
                    ),
                    terminal_utc="2026-08-27T01:01:00Z",
                )
                independent = self.verify(job_id=job_id)
                self.assertEqual(
                    "recoveryAuthorized", independent["ledgerState"]
                )

    def test_start_terminal_inventory_and_entry_mutants_are_typed(self):
        self.complete_success()
        original = self.ledger
        cases = []
        for name in ("noncanonical", "start-binding", "terminal", "output", "entry"):
            root = self.base / f"ledger-{name}"
            shutil.copytree(original, root)
            cases.append((name, root))
        path = self.attempt(cases[0][1]) / "attempt-start.json"
        path.write_bytes(path.read_bytes() + b"\n")
        path = self.attempt(cases[1][1]) / "attempt-start.json"
        start = json.loads(path.read_text(encoding="utf-8"))
        start["commandSha256"] = "e" * 64
        path.write_bytes(MODULE.canonical(start))
        path = self.attempt(cases[2][1]) / "attempt-terminal.json"
        terminal = json.loads(path.read_text(encoding="utf-8"))
        terminal["startReceiptSha256"] = "e" * 64
        path.write_bytes(MODULE.canonical(terminal))
        (self.attempt(cases[3][1]) / "output/bundle-manifest.json").write_text(
            "changed", encoding="utf-8"
        )
        (self.attempt(cases[4][1]) / "unexpected.txt").write_text(
            "unexpected", encoding="utf-8"
        )
        expected = {
            "noncanonical": "CANONICAL_JSON",
            "start-binding": "START_BINDING",
            "terminal": "TERMINAL_BINDING",
            "output": "INVENTORY_MISMATCH",
            "entry": "ENTRY_UNEXPECTED",
        }
        for name, root in cases:
            with self.subTest(name=name):
                self.assert_rejected(
                    expected[name], lambda root=root: self.verify(root)
                )

    def test_attempt_gap_and_forbidden_overlap_are_typed(self):
        self.complete_success()
        gap = self.base / "ledger-gap"
        shutil.copytree(self.ledger, gap)
        self.attempt(gap).rename(gap / self.job_id / "attempt-02")
        self.assert_rejected("ATTEMPT_SEQUENCE", lambda: self.verify(gap))
        self.assert_rejected(
            "PATH_UNSAFE",
            lambda: self.verify(self.ledger, forbidden=[self.ledger / "nested"]),
        )

    def test_real_link_or_windows_junction_is_rejected_by_both_verifiers(self):
        self.prepare()
        attempt = self.attempt()
        self.create_directory_link(attempt / "output", self.base / "link-target")
        self.assertTrue(MODULE._is_link_or_junction(attempt / "output"))
        self.assertTrue(LEDGER.is_link_or_junction(attempt / "output"))
        self.assert_rejected("PATH_UNSAFE", self.verify)
        with self.assertRaisesRegex(LEDGER.LedgerError, "regular directory"):
            LEDGER.inspect_ledger(self.ledger, self.job_id, self.forbidden)
        with self.assertRaisesRegex(SUPERVISOR.SupervisionError, "ancestry"):
            SUPERVISOR.build_command_binding(
                sys.executable,
                self.arguments(),
                attempt / "output",
                self.environment_names,
                os.environ,
            )

    def test_chain_chunk_eof_and_provenance_mutants_are_typed(self):
        self.complete_success()
        expected = {
            "chain": "JOURNAL_CHAIN",
            "time": "JOURNAL_CHAIN",
            "chunk": "JOURNAL_SEMANTICS",
            "eof": "JOURNAL_SEMANTICS",
            "schema": "SCHEMA_PROVENANCE",
            "source": "SOURCE_PROVENANCE",
        }
        for name, code in expected.items():
            with self.subTest(name=name):
                root = self.base / f"ledger-{name}"
                shutil.copytree(self.ledger, root)
                path = self.attempt(root) / "process.log"
                records = self.read_records(path)
                if name == "chain":
                    records[1]["previousRecordSha256"] = "f" * 64
                    path.write_bytes(
                        b"".join(MODULE.canonical(record) + b"\n" for record in records)
                    )
                elif name == "time":
                    # The monotonic clock is the chain invariant, not the wall
                    # clock. Raising the first stamp makes the second go
                    # backwards while staying non-negative and schema valid.
                    records[0]["monotonicElapsedMs"] = 10_000_000
                    path.write_bytes(self.reseal(records))
                elif name == "chunk":
                    record = next(
                        item for item in records if item["recordType"] == "streamChunk"
                    )
                    record["payload"]["dataBase64"] = "eA=="
                    path.write_bytes(self.reseal(records))
                elif name == "eof":
                    record = next(
                        item
                        for item in records
                        if item["recordType"] == "streamEof"
                        and item["payload"]["stream"] == "stdout"
                    )
                    record["payload"]["observedSha256"] = "f" * 64
                    path.write_bytes(self.reseal(records))
                elif name == "schema":
                    records[0]["payload"]["logSchemaSha256"] = "f" * 64
                    path.write_bytes(self.reseal(records))
                else:
                    records[0]["payload"]["supervisorSha256"] = "f" * 64
                    path.write_bytes(self.reseal(records))
                self.assert_rejected(code, lambda root=root: self.verify(root))

    def test_a_wall_clock_step_backwards_does_not_invalidate_the_journal(self):
        """A host NTP step is not a chain violation.

        `RB-WP14R-003` makes the monotonic clock the authority for heartbeat and
        timeout and keeps UTC as provenance only. Enforcing UTC monotonicity
        here once invalidated a completed, otherwise clean attempt: a 1.846 s
        step during `RB-WP14R-008` under freeze v3 made B1 unverifiable even
        though its hash chain, sequence and monotonic chain were all intact.
        """
        def step_the_wall_clock(records):
            stamp = records[1]["observedUtc"]
            records[1]["observedUtc"] = stamp.replace("2026-", "2025-", 1)

        self.complete_success(mutate=step_the_wall_clock)
        report = self.verify()
        self.assertEqual(report["status"], "valid")

    def test_a_monotonic_step_backwards_still_fails_closed(self):
        """The invariant that replaced it must really hold."""
        def step_the_monotonic_clock(records):
            # Raise the first stamp so the second one goes backwards while
            # every value stays non-negative and schema valid.
            records[0]["monotonicElapsedMs"] = 10_000_000

        self.complete_success(mutate=step_the_monotonic_clock)
        self.assert_rejected("JOURNAL_CHAIN", self.verify)

    def test_a_malformed_utc_stamp_is_still_rejected(self):
        """Dropping the ordering rule must not drop the format rule."""
        def break_the_stamp(records):
            records[1]["observedUtc"] = "not-a-timestamp"

        self.complete_success(mutate=break_the_stamp)
        self.assert_rejected("SCHEMA_RECORD", self.verify)

    def test_journal_and_output_inventory_races_fail_closed(self):
        arguments, _, _ = self.prepare()
        SUPERVISOR.supervise_process(
            self.ledger,
            self.job_id,
            1,
            self.forbidden,
            sys.executable,
            arguments,
            self.base,
            self.environment_names,
            wall_timeout_ms=10000,
            heartbeat_interval_ms=50,
            maximum_stream_bytes=65536,
            chunk_bytes=16,
            tree_exit_grace_ms=1000,
        )
        original_inventory = MODULE._file_inventory
        changed = False

        def mutate_after_stream(path):
            nonlocal changed
            if pathlib.Path(path).name == "process.log" and not changed:
                changed = True
                pathlib.Path(path).write_bytes(pathlib.Path(path).read_bytes() + b"x")
            return original_inventory(path)

        with mock.patch.object(MODULE, "_file_inventory", mutate_after_stream):
            self.assert_rejected("INVENTORY_MISMATCH", self.verify)

        output = self.base / "race-output"
        output.mkdir()
        (output / "first.bin").write_bytes(b"first")
        original_measure = MODULE._stable_measure
        added = False

        def add_after_measure(path, code="INVENTORY_MISMATCH"):
            nonlocal added
            measured = original_measure(path, code)
            if pathlib.Path(path).name == "first.bin" and not added:
                added = True
                (output / "late.bin").write_bytes(b"late")
            return measured

        with mock.patch.object(MODULE, "_stable_measure", add_after_measure):
            self.assert_rejected(
                "INVENTORY_MISMATCH",
                lambda: MODULE._directory_inventory(output),
            )

    def test_recovery_terminal_and_receipt_mutation_are_verified(self):
        arguments, metadata, start = self.prepare()
        del arguments
        path = self.attempt() / "process.log"
        writer = SUPERVISOR.JournalWriter(path)
        platform_name, fingerprint = SUPERVISOR.host_fingerprint()
        writer.append(
            "supervisorStart",
            {
                "jobId": self.job_id,
                "attemptId": start["attemptId"],
                "attemptNumber": 1,
                "startReceiptSha256": hashlib.sha256(
                    (self.attempt() / "attempt-start.json").read_bytes()
                ).hexdigest(),
                **metadata,
                "supervisorSha256": SUPERVISOR.stable_file_identity(
                    SUPERVISOR.__file__
                )[1],
                "supervisorProcessId": 4294967294,
                **SUPERVISOR.current_schema_provenance(),
                "hostFingerprintSha256": fingerprint,
                "platform": platform_name,
                "policy": {
                    "wallTimeoutMs": 10000,
                    "heartbeatIntervalMs": 50,
                    "maximumStreamBytes": 65536,
                    "chunkBytes": 16,
                    "treeExitGraceMs": 1000,
                },
            },
        )
        writer.close()
        RECOVERY.recover_open_attempt(
            self.ledger,
            self.job_id,
            1,
            self.forbidden,
            4294967294,
            tree_probe_grace_ms=10,
            observed_utc="2026-08-27T01:01:00Z",
        )
        report = self.verify()
        self.assertEqual("recoveryAuthorized", report["ledgerState"])
        existing = LEDGER.inspect_ledger(
            self.ledger, self.job_id, self.forbidden
        )
        existing_journal = SUPERVISOR.verify_process_log(
            self.attempt() / "process.log"
        )
        self.assertEqual(existing["ledgerState"], report["ledgerState"])
        self.assertEqual(
            existing_journal["status"],
            report["attempts"][0]["journal"]["status"],
        )
        mutant = self.base / "ledger-recovery-mutant"
        shutil.copytree(self.ledger, mutant)
        receipt_path = self.attempt(mutant) / "recovery-receipt.json"
        receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
        receipt["expectedSupervisorProcessId"] = 4294967293
        receipt_path.write_bytes(MODULE.canonical(receipt))
        self.assert_rejected("RECOVERY_BINDING", lambda: self.verify(mutant))


if __name__ == "__main__":
    unittest.main()
