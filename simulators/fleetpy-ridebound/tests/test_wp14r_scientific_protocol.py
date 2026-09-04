import importlib.util
import json
import os
import pathlib
import subprocess
import sys
import tempfile
import types
import unittest
from unittest import mock


ROOT = pathlib.Path(__file__).resolve().parents[3]
ADAPTER = ROOT / "simulators/fleetpy-ridebound"


def load_module(name, filename):
    specification = importlib.util.spec_from_file_location(
        name,
        ADAPTER / filename,
    )
    module = importlib.util.module_from_spec(specification)
    specification.loader.exec_module(module)
    return module


MODULE = load_module(
    "wp14r_scientific_protocol_under_test",
    "wp14r_scientific_protocol.py",
)
SUPERVISOR = load_module(
    "wp14r_supervisor_for_protocol_tests",
    "wp14r_supervised_process.py",
)
JOB_ID = "w14-test-cell-b1-ref-s7"
OTHER_JOB_ID = "w14-test-cell-c1-h6ref-s7"
FREEZE_SHA = "a" * 64


def base():
    return {
        "runtime": {
            "pythonExecutable": sys.executable,
            "fleetPyRoot": "fleetpy",
            "runnerRoot": "runner",
            "dotnetExecutable": "dotnet",
        },
        "execution": {
            "repeatsPerJob": 1,
            "resourceEnvelope": {
                "maximumOutputBytes": 1000,
                "minimumFreeDiskReserveBytes": 10,
            },
        },
        "design": {
            "gridId": "test-grid",
            "jobs": [
                {
                    "jobId": JOB_ID,
                    "cellId": "test-cell",
                    "armId": "b1-ref",
                    "commitmentConfig": "commitment.json",
                    "wp4Config": "wp4.json",
                    "driver": "driver.json",
                    "driverSha256": "b" * 64,
                    "scenarioContentSha256": "c" * 64,
                    "masterSeed": 7,
                },
                {
                    "jobId": OTHER_JOB_ID,
                    "cellId": "test-cell",
                    "armId": "c1-h6ref",
                    "commitmentConfig": "commitment.json",
                    "wp4Config": "wp4.json",
                    "driver": "driver.json",
                    "driverSha256": "b" * 64,
                    "scenarioContentSha256": "c" * 64,
                    "masterSeed": 7,
                },
            ],
            "arms": [
                {
                    "armId": "b1-ref",
                    "commitmentConfig": "commitment.json",
                    "commitmentConfigSha256": "d" * 64,
                    "wp4Config": "wp4.json",
                    "wp4ConfigSha256": "e" * 64,
                },
                {
                    "armId": "c1-h6ref",
                    "commitmentConfig": "commitment.json",
                    "commitmentConfigSha256": "d" * 64,
                    "wp4Config": "wp4.json",
                    "wp4ConfigSha256": "e" * 64,
                },
            ],
        },
    }


def freeze(directory):
    return {
        "freezeId": "wp14r-resilient-development-v2",
        "baseScientificFreeze": {
            "artifact": {"path": "base.json", "sha256": "f" * 64}
        },
        "protocol": {
            "protocolId": "wp14r-supervised-scientific-job-v2",
            "inheritedEnvironmentNames": [
                "PATH",
                "PYTHONDONTWRITEBYTECODE",
                "SystemRoot",
                "TEMP",
                "TMP",
            ],
            "pairedResourceGate": {"jobIds": [JOB_ID, OTHER_JOB_ID]},
            "execution": {
                "maximumJobWallSeconds": 2700,
                "heartbeatIntervalMs": 1000,
                "maximumStreamBytes": 16777216,
                "chunkBytes": 32768,
                "treeExitGraceMs": 2000,
            },
            "isolation": {
                "ledgerRoot": str(directory / "ledger"),
                "controlRoot": str(directory / "control"),
                "forbiddenRoots": [str(directory / f"frozen-{i}") for i in range(5)],
            },
        },
    }


def environment():
    return {
        "PATH": os.environ.get("PATH", "path"),
        "PYTHONDONTWRITEBYTECODE": "0",
        "SystemRoot": os.environ.get("SystemRoot", r"C:\Windows"),
        "TEMP": os.environ.get("TEMP", r"C:\Temp"),
        "TMP": os.environ.get("TMP", r"C:\Temp"),
    }


class FakeLedger:
    def __init__(self, states=None):
        self.states = states or {}

    def inspect_ledger(self, ledger_root, job_id, forbidden):
        state = self.states.get(job_id, "readyInitial")
        return {
            "ledgerState": state,
            "attemptCount": 0,
            "openAttemptNumber": None,
            "selectedValidAttemptId": None,
            "attempts": [],
        }

    def job_root(self, ledger_root, job_id, forbidden):
        return pathlib.Path(ledger_root) / job_id

    def directory_inventory(self, output):
        return {"bytes": 0}


class Wp14RScientificProtocolTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.directory = pathlib.Path(self.temporary.name)
        self.freeze = freeze(self.directory)
        self.base = base()

    def tearDown(self):
        self.temporary.cleanup()

    def test_recovery_command_binding_is_attempt_path_independent(self):
        freeze_path = self.directory / "freeze.json"
        freeze_path.write_text("{}", encoding="utf-8")
        with mock.patch.object(
            MODULE,
            "base_receipt",
            return_value=self.base,
        ):
            first = MODULE.stable_child_command(
                self.directory,
                freeze_path,
                self.freeze,
                JOB_ID,
                SUPERVISOR,
                environment(),
            )
            (self.directory / "ledger/attempt-01/output").mkdir(
                parents=True
            )
            second = MODULE.stable_child_command(
                self.directory,
                freeze_path,
                self.freeze,
                JOB_ID,
                SUPERVISOR,
                environment(),
            )
        self.assertEqual(
            first[2]["commandSha256"],
            second[2]["commandSha256"],
        )
        self.assertNotIn("attempt-01", json.dumps(first[2]))

    def test_job_binding_changes_when_frozen_job_changes(self):
        first = MODULE.job_binding(
            self.freeze,
            FREEZE_SHA,
            self.base,
            JOB_ID,
        )[1]
        second = MODULE.job_binding(
            self.freeze,
            FREEZE_SHA,
            self.base,
            OTHER_JOB_ID,
        )[1]
        self.assertNotEqual(first, second)

    def test_environment_is_canonical_and_forces_no_bytecode(self):
        value = MODULE.normalized_child_environment(
            self.freeze,
            environment(),
        )
        self.assertEqual("1", value["PYTHONDONTWRITEBYTECODE"])
        self.assertEqual(
            self.freeze["protocol"]["inheritedEnvironmentNames"],
            list(value),
        )

    def test_supervisor_cli_preserves_dash_prefixed_child_arguments(self):
        forbidden = [pathlib.Path(value) for value in self.freeze["protocol"][
            "isolation"
        ]["forbiddenRoots"]]
        with mock.patch.object(
            MODULE,
            "base_receipt",
            return_value=self.base,
        ):
            command = MODULE.supervisor_cli_command(
                self.directory,
                self.freeze,
                self.directory / "ledger",
                forbidden,
                JOB_ID,
                1,
                pathlib.Path(sys.executable),
                ["-B", "--repository", str(self.directory)],
            )
        parsed = SUPERVISOR.build_parser().parse_args(command[3:])
        self.assertEqual(
            ["-B", "--repository", str(self.directory)],
            parsed.argument,
        )

    def test_second_paired_job_requires_first_success(self):
        ledger = FakeLedger()
        with mock.patch.object(
            MODULE,
            "base_receipt",
            return_value=self.base,
        ):
            with self.assertRaisesRegex(MODULE.ProtocolError, "frozen order"):
                MODULE.authorize_phase(
                    self.directory,
                    "paired",
                    OTHER_JOB_ID,
                    self.freeze,
                    FREEZE_SHA,
                    ledger,
                )
            ledger.states[JOB_ID] = "succeeded"
            MODULE.authorize_phase(
                self.directory,
                "paired",
                OTHER_JOB_ID,
                self.freeze,
                FREEZE_SHA,
                ledger,
            )

    def test_pair_phase_rejects_non_gate_job(self):
        changed = base()
        changed["design"]["jobs"].append(
            {**changed["design"]["jobs"][0], "jobId": "another-job"}
        )
        with mock.patch.object(MODULE, "base_receipt", return_value=changed):
            with self.assertRaisesRegex(MODULE.ProtocolError, "only the two"):
                MODULE.authorize_phase(
                    self.directory,
                    "paired",
                    "another-job",
                    self.freeze,
                    FREEZE_SHA,
                    FakeLedger(),
                )

    def test_retained_output_limit_blocks_every_new_launch(self):
        with (
            mock.patch.object(
                MODULE,
                "base_receipt",
                return_value=self.base,
            ),
            mock.patch.object(
                MODULE,
                "retained_output_bytes",
                return_value=1001,
            ),
        ):
            with self.assertRaisesRegex(MODULE.ProtocolError, "frozen maximum"):
                MODULE.authorize_phase(
                    self.directory,
                    "paired",
                    JOB_ID,
                    self.freeze,
                    FREEZE_SHA,
                    FakeLedger(),
                )

    def test_failed_bundle_verifier_yields_typed_mechanical_failure(self):
        completed = subprocess.CompletedProcess([], 2, b"", b"private error")
        with mock.patch.object(
            MODULE,
            "base_receipt",
            return_value=self.base,
        ):
            result = MODULE.bundle_verifier(
                ROOT,
                self.freeze,
                self.directory / "output",
                runner=lambda *args, **kwargs: completed,
            )
        self.assertEqual("fail", result["status"])
        self.assertIsNone(result["behavioralHash"])
        self.assertNotIn("private", json.dumps(result))

    def test_bundle_verifier_pass_keeps_only_behavioral_hash(self):
        report = {
            "status": "pass",
            "behavioralProjectionHash": "1" * 64,
            "completedService": 999,
            "burden": 123,
        }
        completed = subprocess.CompletedProcess(
            [],
            0,
            json.dumps(report).encode("utf-8"),
            b"",
        )
        with mock.patch.object(
            MODULE,
            "base_receipt",
            return_value=self.base,
        ):
            result = MODULE.bundle_verifier(
                ROOT,
                self.freeze,
                self.directory / "output",
                runner=lambda *args, **kwargs: completed,
            )
        serialized = json.dumps(result)
        self.assertEqual("pass", result["status"])
        self.assertNotIn("completed", serialized.lower())
        self.assertNotIn("burden", serialized.lower())

    def test_preflight_paths_are_append_only_for_same_attempt(self):
        root = self.directory / "control"
        first = MODULE.next_preflight_path(root, JOB_ID, 1)
        first.parent.mkdir(parents=True, exist_ok=True)
        first.write_text("first", encoding="utf-8")
        second = MODULE.next_preflight_path(root, JOB_ID, 1)
        self.assertNotEqual(first, second)
        self.assertTrue(str(second).endswith("observation-0002.json"))

    def test_pair_gate_schema_couples_pass_to_matrix_authorization(self):
        receipt = {
            "schemaVersion": "2.0.0",
            "schemaId": MODULE.PAIR_GATE_SCHEMA_ID,
            "recordType": "ridebound-wp14r-paired-resource-gate-v2",
            "gateId": "wp14r-paired-b1-c1-resource-gate-v2",
            "generatedUtc": "2026-08-28T01:00:00Z",
            "status": "pass",
            "freezeId": "wp14r-resilient-development-v2",
            "freezeReceiptSha256": FREEZE_SHA,
            "claimBoundary": MODULE.PAIR_GATE_CLAIM_BOUNDARY,
            "requiredValidJobs": 2,
            "requiredFailedJobs": 0,
            "jobs": [
                {
                    "jobId": value,
                    "ledgerState": "succeeded",
                    "selectedValidAttemptId": f"{value}-attempt-01",
                    "attemptCount": 1,
                    "terminalReceiptSha256": "2" * 64,
                    "elapsedMs": 1,
                    "retainedOutputBytes": 1,
                    "independentVerificationStatus": "valid",
                    "preflightReceipts": [],
                }
                for value in (JOB_ID, OTHER_JOB_ID)
            ],
            "totalElapsedMs": 2,
            "totalRetainedOutputBytes": 2,
            "maximumRetainedOutputBytes": 100,
            "minimumFreeDiskReserveBytes": 10,
            "freeDiskBytesAtGate": 50,
            "resourceEnvelopeStatus": "pass",
            "outcomeFieldsRead": False,
            "matrixAuthorized": False,
            "protocolToolSha256": "3" * 64,
        }
        with self.assertRaisesRegex(
            MODULE.ProtocolError,
            "schema failed",
        ):
            MODULE.validate_pair_gate_schema(ROOT, receipt)

    def test_child_derives_attempt_output_without_changing_start_binding(self):
        attempt = self.directory / "ledger" / JOB_ID / "attempt-02"
        attempt.mkdir(parents=True)

        class Ledger:
            def inspect_ledger(self, *args):
                return {"ledgerState": "attemptOpen", "openAttemptNumber": 2}

            def job_root(self, *args):
                return self_directory / "ledger" / JOB_ID

            def read_canonical_json(self, path):
                return (
                    {
                        "jobBindingSha256": "4" * 64,
                        "outputRelativePath": "output",
                    },
                    b"start",
                )

        self_directory = self.directory
        dependencies = {"ledger": Ledger()}
        completed = subprocess.CompletedProcess([], 7)
        with (
            mock.patch.object(
                MODULE,
                "read_freeze",
                return_value=(self.freeze, FREEZE_SHA, dependencies),
            ),
            mock.patch.object(
                MODULE,
                "base_receipt",
                return_value=self.base,
            ),
            mock.patch.object(
                MODULE,
                "job_binding",
                return_value=({}, "4" * 64),
            ),
            mock.patch.object(
                MODULE,
                "actual_scientific_command",
                return_value=["fake"],
            ) as command,
            mock.patch.object(
                MODULE.subprocess,
                "run",
                return_value=completed,
            ),
        ):
            result = MODULE.run_scientific_child(
                self.directory,
                self.directory / "freeze.json",
                JOB_ID,
                dependencies,
            )
        self.assertEqual(7, result)
        output = command.call_args.args[3]
        self.assertEqual(attempt / "output", output)


if __name__ == "__main__":
    unittest.main()
