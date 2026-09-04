import hashlib
import importlib.util
import json
import os
import pathlib
import sys
import tempfile
import unittest
from unittest import mock

import jsonschema


ROOT = pathlib.Path(__file__).resolve().parents[3]
ADAPTER = ROOT / "simulators/fleetpy-ridebound"


def load_module(name, filename):
    specification = importlib.util.spec_from_file_location(
        name, ADAPTER / filename
    )
    module = importlib.util.module_from_spec(specification)
    specification.loader.exec_module(module)
    return module


LEDGER = load_module("wp14r_ledger_for_supervision_tests", "wp14r_attempt_ledger.py")
MODULE = load_module(
    "wp14r_supervision_under_test", "wp14r_supervised_process.py"
)


class Wp14RSupervisedProcessTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.base = pathlib.Path(self.temporary.name)
        self.ledger_root = self.base / "ledger"
        self.forbidden = [self.base / "frozen"]
        self.job_id = "w14r-supervision-test"
        self.environment_names = ["SystemRoot"] if os.name == "nt" else []

    def tearDown(self):
        self.temporary.cleanup()

    def arguments(self, code):
        return ["-B", "-c", code]

    def prepare(self, arguments):
        metadata, _ = MODULE.build_command_binding(
            sys.executable,
            arguments,
            self.base,
            self.environment_names,
            os.environ,
        )
        LEDGER.begin_attempt(
            self.ledger_root,
            self.job_id,
            "wp14r-supervision-test-freeze",
            "a" * 64,
            "b" * 64,
            metadata["commandSha256"],
            self.forbidden,
            "2026-08-26T01:00:00Z",
        )
        return metadata

    def supervise(self, arguments, **overrides):
        values = {
            "ledger_root": self.ledger_root,
            "job_id": self.job_id,
            "attempt_number": 1,
            "forbidden_roots": self.forbidden,
            "executable": sys.executable,
            "arguments": arguments,
            "working_directory": self.base,
            "inherited_environment_names": self.environment_names,
            "wall_timeout_ms": 5000,
            "heartbeat_interval_ms": 50,
            "maximum_stream_bytes": 65536,
            "chunk_bytes": 16,
            "tree_exit_grace_ms": 1000,
            "source_environment": os.environ,
        }
        values.update(overrides)
        return MODULE.supervise_process(**values)

    def process_log(self):
        return (
            self.ledger_root
            / self.job_id
            / "attempt-01"
            / "process.log"
        )

    def records(self):
        return [
            json.loads(line)
            for line in self.process_log().read_text(encoding="utf-8").splitlines()
        ]

    def reseal(self, records):
        previous = MODULE.ZERO_SHA256
        lines = []
        for sequence, record in enumerate(records):
            record["sequence"] = sequence
            record["previousRecordSha256"] = previous
            record.pop("recordSha256", None)
            previous = MODULE.sha256_bytes(MODULE.canonical(record))
            record["recordSha256"] = previous
            lines.append(MODULE.canonical(record) + b"\n")
        return b"".join(lines)

    def test_command_binding_is_exact_but_log_metadata_redacts_values(self):
        source = {"RIDEBOUND_TEST_SECRET": "private-value"}
        first, _ = MODULE.build_command_binding(
            sys.executable,
            ["secret-argument"],
            self.base,
            ["RIDEBOUND_TEST_SECRET"],
            source,
        )
        changed_argument, _ = MODULE.build_command_binding(
            sys.executable,
            ["another-argument"],
            self.base,
            ["RIDEBOUND_TEST_SECRET"],
            source,
        )
        changed_environment, _ = MODULE.build_command_binding(
            sys.executable,
            ["secret-argument"],
            self.base,
            ["RIDEBOUND_TEST_SECRET"],
            {"RIDEBOUND_TEST_SECRET": "changed"},
        )
        self.assertNotEqual(
            first["commandSha256"], changed_argument["commandSha256"]
        )
        self.assertNotEqual(
            first["commandSha256"], changed_environment["commandSha256"]
        )
        serialized = json.dumps(first)
        self.assertNotIn("private-value", serialized)
        self.assertNotIn("secret-argument", serialized)

    def test_binary_streams_are_preserved_and_exit_zero_awaits_verifier(self):
        stdout = b"out\x00\xff"
        stderr = b"err\x80"
        code = (
            "import sys,time;"
            f"sys.stdout.buffer.write({stdout!r});sys.stdout.flush();"
            f"sys.stderr.buffer.write({stderr!r});sys.stderr.flush();"
            "time.sleep(.1)"
        )
        arguments = self.arguments(code)
        self.prepare(arguments)
        report = self.supervise(arguments)
        self.assertEqual(
            "childExitedZeroAwaitingBundleVerification",
            report["terminalStatus"],
        )
        self.assertEqual(
            hashlib.sha256(stdout).hexdigest(),
            report["streams"]["stdout"]["capturedSha256"],
        )
        self.assertEqual(
            hashlib.sha256(stderr).hexdigest(),
            report["streams"]["stderr"]["capturedSha256"],
        )
        self.assertNotIn(code.encode("utf-8"), self.process_log().read_bytes())
        ledger = LEDGER.inspect_ledger(
            self.ledger_root, self.job_id, self.forbidden
        )
        self.assertEqual("attemptOpen", ledger["ledgerState"])

    def test_command_mutation_fails_before_process_log_or_launch(self):
        declared = self.arguments("print('declared')")
        changed = self.arguments("print('changed')")
        self.prepare(declared)
        with self.assertRaisesRegex(MODULE.SupervisionError, "command differs"):
            self.supervise(changed)
        self.assertFalse(self.process_log().exists())

    def test_process_log_is_exclusive_create(self):
        arguments = self.arguments("import time;time.sleep(.1)")
        self.prepare(arguments)
        self.supervise(arguments)
        original = self.process_log().read_bytes()
        with self.assertRaisesRegex(MODULE.SupervisionError, "already exists"):
            self.supervise(arguments)
        self.assertEqual(original, self.process_log().read_bytes())

    def test_nonzero_exit_is_typed_and_streams_reach_eof(self):
        arguments = self.arguments(
            "import sys;sys.stderr.write('failure');sys.exit(7)"
        )
        self.prepare(arguments)
        report = self.supervise(arguments)
        self.assertEqual("childExitFailure", report["terminalStatus"])
        self.assertEqual(7, report["processExitCode"])
        self.assertTrue(report["streams"]["stdout"]["eof"])
        self.assertTrue(report["streams"]["stderr"]["eof"])

    def test_wall_timeout_is_monotonic_typed_and_tree_is_not_clean_success(self):
        arguments = self.arguments("import time;time.sleep(5)")
        self.prepare(arguments)
        report = self.supervise(
            arguments,
            wall_timeout_ms=100,
            tree_exit_grace_ms=1000,
        )
        self.assertEqual("wallTimeout", report["terminalStatus"])
        self.assertIn(report["treeStatus"], ("terminated", "uncertain"))
        limits = [
            record["payload"]["limit"]
            for record in self.records()
            if record["recordType"] == "limitTriggered"
        ]
        self.assertEqual(["wallTimeout"], limits)

    def test_lingering_grandchild_is_contained_and_typed_as_tree_leak(self):
        code = (
            "import subprocess,sys;"
            "subprocess.Popen([sys.executable,'-B','-c',"
            "'import time;time.sleep(5)'],"
            "stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL);"
            "print('parent-exits')"
        )
        arguments = self.arguments(code)
        self.prepare(arguments)
        report = self.supervise(
            arguments,
            tree_exit_grace_ms=100,
        )
        self.assertEqual("treeLeakTerminated", report["terminalStatus"])
        self.assertEqual("terminated", report["treeStatus"])
        started = next(
            record
            for record in self.records()
            if record["recordType"] == "childStarted"
        )
        self.assertIn(
            started["payload"]["containment"],
            ("windowsJobObject", "posixProcessGroup"),
        )

    def test_stream_cap_retains_a_verified_prefix_and_typed_overflow(self):
        arguments = self.arguments(
            "import sys,time;"
            "sys.stdout.buffer.write(b'x'*4096);sys.stdout.flush();"
            "time.sleep(.1)"
        )
        self.prepare(arguments)
        report = self.supervise(
            arguments,
            maximum_stream_bytes=31,
            chunk_bytes=16,
        )
        self.assertEqual("stdoutLimit", report["terminalStatus"])
        self.assertEqual(31, report["streams"]["stdout"]["capturedBytes"])
        verified = MODULE.verify_process_log(self.process_log())
        self.assertEqual(report, verified)

    def test_chain_chunk_and_terminal_append_mutations_fail_closed(self):
        arguments = self.arguments("print('chunk')")
        self.prepare(arguments)
        self.supervise(arguments)
        original = self.process_log().read_bytes()
        records = self.records()
        chunk = next(
            record for record in records if record["recordType"] == "streamChunk"
        )
        chunk["payload"]["dataBase64"] = "eA=="
        mutated = b"".join(
            MODULE.canonical(chunk if record is chunk else record) + b"\n"
            for record in records
        )
        self.process_log().write_bytes(mutated)
        with self.assertRaises(MODULE.SupervisionError):
            MODULE.verify_process_log(self.process_log())
        self.process_log().write_bytes(original + b"garbage")
        with self.assertRaisesRegex(MODULE.SupervisionError, "after a terminal"):
            MODULE.verify_process_log(self.process_log())

    def test_resealed_stream_and_terminal_semantic_mutations_fail_closed(self):
        arguments = self.arguments("print('semantic')")
        self.prepare(arguments)
        self.supervise(arguments)
        records = self.records()
        eof = next(
            record
            for record in records
            if record["recordType"] == "streamEof"
            and record["payload"]["stream"] == "stdout"
        )
        eof["payload"]["observedSha256"] = "f" * 64
        self.process_log().write_bytes(self.reseal(records))
        with self.assertRaisesRegex(MODULE.SupervisionError, "observed hash"):
            MODULE.verify_process_log(self.process_log())

    def test_launch_intent_is_durable_and_semantically_bound(self):
        arguments = self.arguments("print('launch intent')")
        self.prepare(arguments)
        self.supervise(arguments)
        records = self.records()
        start = records[0]
        intent = next(
            record for record in records if record["recordType"] == "launchIntent"
        )
        self.assertEqual(
            start["payload"]["commandSha256"],
            intent["payload"]["commandSha256"],
        )
        self.assertEqual(os.getpid(), start["payload"]["supervisorProcessId"])
        intent["payload"]["commandSha256"] = "f" * 64
        self.process_log().write_bytes(self.reseal(records))
        with self.assertRaisesRegex(MODULE.SupervisionError, "launch intent"):
            MODULE.verify_process_log(self.process_log())

    def test_schema_validators_are_cached_and_provenance_is_bound(self):
        MODULE.schema_validator.cache_clear()
        MODULE.load_schema.cache_clear()
        first = MODULE.schema_validator(
            "supervision-log-record.schema.json"
        )
        second = MODULE.schema_validator(
            "supervision-log-record.schema.json"
        )
        self.assertIs(first, second)
        arguments = self.arguments("print('schema provenance')")
        self.prepare(arguments)
        report = self.supervise(arguments)
        start = self.records()[0]["payload"]
        self.assertEqual(
            MODULE.SCHEMA_PROVENANCE_VERSION,
            start["supervisionEvidenceVersion"],
        )
        self.assertEqual(start["logSchemaSha256"], report["logSchemaSha256"])
        self.assertEqual(
            start["reportSchemaSha256"],
            report["reportSchemaSha256"],
        )
        records = self.records()
        records[0]["payload"]["logSchemaSha256"] = "f" * 64
        self.process_log().write_bytes(self.reseal(records))
        with self.assertRaisesRegex(
            MODULE.SupervisionError, "schema provenance"
        ):
            MODULE.verify_process_log(self.process_log())

    def test_legacy_journal_without_schema_provenance_remains_verifiable(self):
        arguments = self.arguments("print('legacy journal')")
        self.prepare(arguments)
        self.supervise(arguments)
        records = self.records()
        start = records[0]["payload"]
        for field in (
            "supervisionEvidenceVersion",
            "logSchemaSha256",
            "reportSchemaSha256",
        ):
            start.pop(field)
        self.process_log().write_bytes(self.reseal(records))
        report = MODULE.verify_process_log(self.process_log())
        self.assertEqual("validComplete", report["status"])
        self.assertNotIn("supervisionEvidenceVersion", report)

    def test_supervision_evidence_composes_with_terminal_ledger_receipt(self):
        arguments = self.arguments("print('synthetic bundle mechanics')")
        self.prepare(arguments)
        report = self.supervise(arguments)
        output = self.process_log().parent / "output"
        output.mkdir()
        (output / "bundle-manifest.json").write_text("{}", encoding="utf-8")
        LEDGER.terminalize_attempt(
            self.ledger_root,
            self.job_id,
            1,
            "success",
            100,
            report["treeStatus"],
            "pass",
            self.forbidden,
            process_exit_code=report["processExitCode"],
            verifier_id="synthetic-independent-verifier",
            verifier_sha256="c" * 64,
            behavioral_hash="d" * 64,
            terminal_utc="2026-08-26T01:01:00Z",
        )
        ledger = LEDGER.inspect_ledger(
            self.ledger_root, self.job_id, self.forbidden
        )
        self.assertEqual("succeeded", ledger["ledgerState"])

    def test_verified_prefix_accepts_only_a_truncated_nonterminal_tail(self):
        arguments = self.arguments("print('partial')")
        self.prepare(arguments)
        self.supervise(arguments)
        lines = self.process_log().read_bytes().splitlines(keepends=True)
        prefix = b"".join(lines[:-2]) + b'{"truncated"'
        self.process_log().write_bytes(prefix)
        report = MODULE.verify_process_log(self.process_log())
        self.assertEqual("validPartial", report["status"])
        self.assertGreater(report["truncatedTailBytes"], 0)
        self.assertIsNone(report["terminalStatus"])

    def test_create_process_failure_is_complete_mechanical_evidence(self):
        arguments = self.arguments("print('never launched')")
        self.prepare(arguments)
        with mock.patch.object(
            MODULE, "create_process", side_effect=OSError("synthetic launch")
        ):
            report = self.supervise(arguments)
        self.assertEqual("launchFailure", report["terminalStatus"])
        self.assertEqual("notObserved", report["treeStatus"])
        launch = next(
            record
            for record in self.records()
            if record["recordType"] == "launchFailure"
        )
        self.assertEqual("createProcess", launch["payload"]["stage"])
        self.assertNotIn("synthetic launch", self.process_log().read_text("utf-8"))

    def test_containment_failure_never_claims_no_process_was_started(self):
        arguments = self.arguments("import time;time.sleep(1)")
        self.prepare(arguments)
        with mock.patch.object(
            MODULE,
            "create_containment",
            side_effect=MODULE.SupervisionError("synthetic containment"),
        ):
            report = self.supervise(arguments)
        self.assertEqual("treeUncertain", report["terminalStatus"])
        self.assertEqual("uncertain", report["treeStatus"])
        launch = next(
            record
            for record in self.records()
            if record["recordType"] == "launchFailure"
        )
        self.assertEqual("containment", launch["payload"]["stage"])

    def test_heartbeat_and_strict_report_schema_are_present(self):
        arguments = self.arguments("import time;time.sleep(.2)")
        self.prepare(arguments)
        report = self.supervise(arguments, heartbeat_interval_ms=50)
        self.assertTrue(
            any(record["recordType"] == "heartbeat" for record in self.records())
        )
        schema = MODULE.load_schema("supervision-report.schema.json")
        jsonschema.Draft202012Validator(schema).validate(report)
        mutant = dict(report)
        mutant["claimBoundary"] = ["mechanicalOnly"]
        with self.assertRaises(jsonschema.ValidationError):
            jsonschema.Draft202012Validator(schema).validate(mutant)


if __name__ == "__main__":
    unittest.main()
