import importlib.util
import json
import os
import pathlib
import signal
import sys
import tempfile
import unittest
from unittest import mock


ROOT = pathlib.Path(__file__).resolve().parents[3]
ADAPTER = ROOT / "simulators/fleetpy-ridebound"


def load_module(name, filename):
    specification = importlib.util.spec_from_file_location(
        name, ADAPTER / filename
    )
    module = importlib.util.module_from_spec(specification)
    specification.loader.exec_module(module)
    return module


LEDGER = load_module("wp14r_ledger_for_fault_tests", "wp14r_attempt_ledger.py")
SUPERVISOR = load_module(
    "wp14r_supervisor_for_fault_tests", "wp14r_supervised_process.py"
)
RECOVERY = load_module(
    "wp14r_recovery_under_test", "wp14r_stale_open_recovery.py"
)
FAULTS = load_module(
    "wp14r_fault_injection_under_test", "wp14r_fault_injection.py"
)


class Wp14RFaultRecoveryTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.base = pathlib.Path(self.temporary.name)
        self.ledger_root = self.base / "ledger"
        self.forbidden = [self.base / "frozen"]
        self.environment_names = ["SystemRoot"] if os.name == "nt" else []
        self.freeze_hash = "a" * 64
        self.job_hash = "b" * 64

    def tearDown(self):
        self.temporary.cleanup()

    @staticmethod
    def arguments(code):
        return ["-B", "-c", code]

    def prepare(self, job_id, arguments, started_utc="2026-08-26T01:00:00Z"):
        metadata, _ = SUPERVISOR.build_command_binding(
            sys.executable,
            arguments,
            self.base,
            self.environment_names,
            os.environ,
        )
        start = LEDGER.begin_attempt(
            self.ledger_root,
            job_id,
            "wp14r-fault-test-freeze",
            self.freeze_hash,
            self.job_hash,
            metadata["commandSha256"],
            self.forbidden,
            started_utc,
        )
        return start

    def fault_config(self, job_id, arguments, fault_point):
        return {
            "ledgerRoot": str(self.ledger_root),
            "jobId": job_id,
            "attemptNumber": 1,
            "forbiddenRoots": [str(path) for path in self.forbidden],
            "executable": sys.executable,
            "arguments": arguments,
            "workingDirectory": str(self.base),
            "inheritedEnvironmentNames": self.environment_names,
            "wallTimeoutMs": 10000,
            "heartbeatIntervalMs": 50,
            "maximumStreamBytes": 65536,
            "chunkBytes": 16,
            "treeExitGraceMs": 1000,
            "faultPoint": fault_point,
        }

    def run_fault(self, job_id, arguments, fault_point):
        control = self.base / f"control-{job_id}"
        return FAULTS.run_hard_kill(
            self.fault_config(job_id, arguments, fault_point),
            control,
            timeout_seconds=10,
        )

    def recover(self, job_id, worker_process_id, attempt_number=1):
        return RECOVERY.recover_open_attempt(
            self.ledger_root,
            job_id,
            attempt_number,
            self.forbidden,
            worker_process_id,
            tree_probe_grace_ms=2000,
            observed_utc="2026-08-26T01:01:00Z",
        )

    def process_log(self, job_id, attempt_number=1):
        return (
            self.ledger_root
            / job_id
            / f"attempt-{attempt_number:02d}"
            / "process.log"
        )

    def test_prelaunch_prefixes_recover_but_launch_ambiguity_exhausts(self):
        cases = (
            ("beforeSupervisorStart", "attemptsExhausted", False),
            ("afterSupervisorStart", "recoveryAuthorized", True),
            ("afterLaunchIntent", "attemptsExhausted", False),
            (
                "afterProcessCreatedBeforeContainment",
                "attemptsExhausted",
                False,
            ),
            (
                "afterContainmentBeforeChildStarted",
                "attemptsExhausted",
                False,
            ),
        )
        for index, (point, disposition, tree_safe) in enumerate(cases):
            with self.subTest(point=point):
                job_id = f"prelaunch-{index}"
                arguments = self.arguments("import time;time.sleep(.5)")
                self.prepare(job_id, arguments)
                killed = self.run_fault(job_id, arguments, point)
                self.assertFalse(
                    RECOVERY.process_exists(killed["workerProcessId"])
                )
                recovered = self.recover(job_id, killed["workerProcessId"])
                receipt = recovered["recoveryReceipt"]
                terminal = recovered["attemptTerminal"]
                self.assertEqual(tree_safe, receipt["treeSafe"])
                self.assertEqual(disposition, terminal["retryDisposition"])
                self.assertEqual(
                    "launcherRecoveredOrphanedStart"
                    if tree_safe
                    else "processTreeUncertain",
                    terminal["exitClassification"],
                )
                state = LEDGER.inspect_ledger(
                    self.ledger_root, job_id, self.forbidden
                )["ledgerState"]
                self.assertEqual(
                    "recoveryAuthorized"
                    if disposition == "recoveryAuthorized"
                    else "exhausted",
                    state,
                )
                child_id = killed["barrier"]["details"].get("processId")
                if child_id is not None:
                    self.assertTrue(
                        RECOVERY.wait_process_absent(child_id, 3000)
                    )

    def test_live_supervisor_pid_blocks_stale_open_recovery(self):
        job_id = "live-supervisor"
        arguments = self.arguments("print('not launched')")
        self.prepare(job_id, arguments)
        with self.assertRaisesRegex(RECOVERY.RecoveryError, "still active"):
            RECOVERY.recover_open_attempt(
                self.ledger_root,
                job_id,
                1,
                self.forbidden,
                os.getpid(),
                100,
                "2026-08-26T01:01:00Z",
            )
        attempt = self.ledger_root / job_id / "attempt-01"
        self.assertFalse((attempt / "recovery-receipt.json").exists())
        self.assertFalse((attempt / "attempt-terminal.json").exists())

    def test_posix_recovery_targets_only_the_recorded_process_group(self):
        with (
            mock.patch.object(RECOVERY.os, "name", "posix"),
            mock.patch.object(
                RECOVERY.os, "getpgrp", return_value=91, create=True
            ),
            mock.patch.object(
                RECOVERY,
                "process_group_exists",
                side_effect=[True, False, False, False, False],
            ),
            mock.patch.object(
                RECOVERY.os, "killpg", create=True
            ) as kill_group,
        ):
            self.assertTrue(RECOVERY.terminate_posix_process_group(92, 10))
        kill_group.assert_called_once_with(92, signal.SIGTERM)

    def test_fault_control_root_cannot_overlap_ledger_or_frozen_roots(self):
        job_id = "unsafe-control-root"
        arguments = self.arguments("print('not launched')")
        self.prepare(job_id, arguments)
        with self.assertRaisesRegex(
            FAULTS.FaultInjectionError, "control root is unsafe"
        ):
            FAULTS.run_hard_kill(
                self.fault_config(
                    job_id, arguments, "beforeSupervisorStart"
                ),
                self.ledger_root,
                timeout_seconds=1,
            )

    def test_contained_fault_points_retain_valid_prefix_and_authorize_once(self):
        cases = (
            (
                "afterChildStarted",
                "import time;time.sleep(30)",
            ),
            (
                "afterStreamChunk",
                "import sys,time;print('chunk',flush=True);time.sleep(30)",
            ),
            (
                "afterStreamsEofBeforeChildExit",
                "print('done')",
            ),
            (
                "afterChildExit",
                "print('done')",
            ),
            (
                "afterSupervisorTerminal",
                "import sys;print('failed');sys.exit(7)",
            ),
        )
        for index, (point, code) in enumerate(cases):
            with self.subTest(point=point):
                job_id = f"contained-{index}"
                arguments = self.arguments(code)
                self.prepare(job_id, arguments)
                killed = self.run_fault(job_id, arguments, point)
                report = SUPERVISOR.verify_process_log(self.process_log(job_id))
                expected_status = (
                    "validComplete"
                    if point == "afterSupervisorTerminal"
                    else "validPartial"
                )
                self.assertEqual(expected_status, report["status"])
                recovered = self.recover(job_id, killed["workerProcessId"])
                receipt = recovered["recoveryReceipt"]
                self.assertTrue(receipt["treeSafe"])
                self.assertEqual(
                    "recoveryAuthorized",
                    recovered["attemptTerminal"]["retryDisposition"],
                )
                child_id = killed["barrier"]["details"].get("processId")
                if child_id is not None:
                    self.assertTrue(
                        RECOVERY.wait_process_absent(child_id, 3000)
                    )

    def test_parent_exit_before_pipe_eof_kills_the_contained_grandchild(self):
        grandchild_pid = self.base / "grandchild.pid"
        grandchild_code = "import time;time.sleep(30)"
        code = (
            "import pathlib,subprocess,sys;"
            f"p=subprocess.Popen([sys.executable,'-B','-c',{grandchild_code!r}]);"
            f"pathlib.Path({str(grandchild_pid)!r}).write_text("
            "str(p.pid),encoding='ascii');"
            "print('parent-exits',flush=True)"
        )
        arguments = self.arguments(code)
        job_id = "parent-exit-before-eof"
        self.prepare(job_id, arguments)
        killed = self.run_fault(
            job_id, arguments, "afterProcessExitBeforeStreamEof"
        )
        recovered = self.recover(job_id, killed["workerProcessId"])
        self.assertTrue(recovered["recoveryReceipt"]["treeSafe"])
        self.assertTrue(grandchild_pid.is_file())
        descendant = int(grandchild_pid.read_text(encoding="ascii"))
        self.assertTrue(RECOVERY.wait_process_absent(descendant, 3000))

    def test_exit_zero_complete_journal_stays_open_for_bundle_verification(self):
        job_id = "complete-exit-zero"
        arguments = self.arguments("print('bundle candidate')")
        self.prepare(job_id, arguments)
        killed = self.run_fault(
            job_id, arguments, "afterSupervisorTerminal"
        )
        with self.assertRaisesRegex(
            RECOVERY.RecoveryError, "awaits independent bundle"
        ):
            self.recover(job_id, killed["workerProcessId"])
        report = LEDGER.inspect_ledger(
            self.ledger_root, job_id, self.forbidden
        )
        self.assertEqual("attemptOpen", report["ledgerState"])
        self.assertFalse(
            (
                self.ledger_root
                / job_id
                / "attempt-01/recovery-receipt.json"
            ).exists()
        )

    def test_recovery_hard_crash_reuses_the_immutable_receipt(self):
        job_id = "recovery-crash-window"
        arguments = self.arguments("import time;time.sleep(30)")
        self.prepare(job_id, arguments)
        killed = self.run_fault(job_id, arguments, "afterChildStarted")
        recovery_config = {
            "ledgerRoot": str(self.ledger_root),
            "jobId": job_id,
            "attemptNumber": 1,
            "forbiddenRoots": [str(path) for path in self.forbidden],
            "expectedSupervisorProcessId": killed["workerProcessId"],
            "treeProbeGraceMs": 2000,
            "observedUtc": "2026-08-26T01:01:00Z",
            "faultPoint": "afterRecoveryReceiptBeforeTerminal",
        }
        recovery_killed = FAULTS.run_recovery_hard_kill(
            recovery_config,
            self.base / "control-recovery-crash",
            timeout_seconds=10,
        )
        self.assertFalse(
            RECOVERY.process_exists(recovery_killed["workerProcessId"])
        )
        attempt = self.ledger_root / job_id / "attempt-01"
        receipt_path = attempt / "recovery-receipt.json"
        self.assertTrue(receipt_path.is_file())
        self.assertFalse((attempt / "attempt-terminal.json").exists())
        before = receipt_path.read_bytes()
        recovered = self.recover(job_id, killed["workerProcessId"])
        self.assertEqual(before, receipt_path.read_bytes())
        self.assertEqual(
            "recoveryAuthorized",
            recovered["attemptTerminal"]["retryDisposition"],
        )

    def test_resealed_preterminal_recovery_decision_is_rejected(self):
        job_id = "recovery-receipt-preterminal-mutation"
        arguments = self.arguments("import time;time.sleep(30)")
        self.prepare(job_id, arguments)
        killed = self.run_fault(job_id, arguments, "afterChildStarted")
        recovery_config = {
            "ledgerRoot": str(self.ledger_root),
            "jobId": job_id,
            "attemptNumber": 1,
            "forbiddenRoots": [str(path) for path in self.forbidden],
            "expectedSupervisorProcessId": killed["workerProcessId"],
            "treeProbeGraceMs": 2000,
            "observedUtc": "2026-08-26T01:01:00Z",
            "faultPoint": "afterRecoveryReceiptBeforeTerminal",
        }
        FAULTS.run_recovery_hard_kill(
            recovery_config,
            self.base / "control-recovery-mutation",
            timeout_seconds=10,
        )
        receipt_path = (
            self.ledger_root
            / job_id
            / "attempt-01/recovery-receipt.json"
        )
        receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
        receipt["treeSafe"] = False
        receipt["treeStatus"] = "uncertain"
        receipt["ledgerExitClassification"] = "processTreeUncertain"
        receipt["retryAuthorized"] = False
        receipt["ledgerRetryDisposition"] = "attemptsExhausted"
        receipt_path.write_bytes(LEDGER.canonical(receipt))
        with self.assertRaisesRegex(
            RECOVERY.RecoveryError, "changed its recovery decision"
        ):
            self.recover(job_id, killed["workerProcessId"])
        self.assertFalse(
            (
                self.ledger_root
                / job_id
                / "attempt-01/attempt-terminal.json"
            ).exists()
        )

    def test_recovery_receipt_tamper_and_second_failure_fail_closed(self):
        job_id = "receipt-and-attempt-limit"
        arguments = self.arguments("import time;time.sleep(30)")
        first = self.prepare(job_id, arguments)
        killed = self.run_fault(job_id, arguments, "afterChildStarted")
        self.recover(job_id, killed["workerProcessId"])
        second = LEDGER.begin_attempt(
            self.ledger_root,
            job_id,
            first["freezeId"],
            first["freezeReceiptSha256"],
            first["jobBindingSha256"],
            first["commandSha256"],
            self.forbidden,
            "2026-08-26T01:02:00Z",
        )
        self.assertEqual(2, second["attemptNumber"])
        config = self.fault_config(job_id, arguments, "beforeSupervisorStart")
        config["attemptNumber"] = 2
        killed_second = FAULTS.run_hard_kill(
            config,
            self.base / "control-second-attempt",
            timeout_seconds=10,
        )
        second_recovery = RECOVERY.recover_open_attempt(
            self.ledger_root,
            job_id,
            2,
            self.forbidden,
            killed_second["workerProcessId"],
            2000,
            "2026-08-26T01:03:00Z",
        )
        self.assertEqual(
            "attemptsExhausted",
            second_recovery["attemptTerminal"]["retryDisposition"],
        )
        with self.assertRaisesRegex(LEDGER.LedgerError, "does not authorize"):
            LEDGER.begin_attempt(
                self.ledger_root,
                job_id,
                first["freezeId"],
                first["freezeReceiptSha256"],
                first["jobBindingSha256"],
                first["commandSha256"],
                self.forbidden,
                "2026-08-26T01:04:00Z",
            )
        receipt_path = (
            self.ledger_root
            / job_id
            / "attempt-01/recovery-receipt.json"
        )
        receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
        receipt["treeSafe"] = False
        receipt_path.write_bytes(LEDGER.canonical(receipt))
        with self.assertRaises(LEDGER.LedgerError):
            LEDGER.inspect_ledger(self.ledger_root, job_id, self.forbidden)


if __name__ == "__main__":
    unittest.main()
