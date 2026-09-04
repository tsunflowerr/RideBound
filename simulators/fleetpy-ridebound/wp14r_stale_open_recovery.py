#!/usr/bin/env python3
"""Recover a WP14R stale open attempt without reading scientific outcomes."""

from __future__ import annotations

import argparse
import ctypes
import datetime
import importlib.util
import os
import pathlib
import signal
import sys
import time

import jsonschema

sys.dont_write_bytecode = True

RECOVERY_SCHEMA_ID = (
    "https://ridebound.local/schemas/wp14r/v1/"
    "recovery-receipt.schema.json"
)
RECOVERY_VERSION = "wp14r-stale-open-recovery-v1"
CLAIM_BOUNDARY = [
    "mechanicalOnly",
    "attemptsNotExperimentalUnits",
    "doesNotReadScientificOutcome",
    "doesNotSupersedeWp14V1",
]


class RecoveryError(RuntimeError):
    """A fail-closed stale-open recovery condition."""


def load_module(name, filename):
    path = pathlib.Path(__file__).resolve().with_name(filename)
    specification = importlib.util.spec_from_file_location(name, path)
    if specification is None or specification.loader is None:
        raise RecoveryError(f"cannot load recovery dependency: {path}")
    module = importlib.util.module_from_spec(specification)
    specification.loader.exec_module(module)
    return module


def load_dependencies():
    ledger = load_module("wp14r_ledger_for_recovery", "wp14r_attempt_ledger.py")
    supervisor = load_module(
        "wp14r_supervisor_for_recovery", "wp14r_supervised_process.py"
    )
    return ledger, supervisor


def utc_now():
    return (
        datetime.datetime.now(datetime.timezone.utc)
        .isoformat(timespec="microseconds")
        .replace("+00:00", "Z")
    )


def process_exists(process_id):
    if not isinstance(process_id, int) or not 1 <= process_id <= 4294967295:
        raise RecoveryError("process id is outside the contract")
    if os.name == "nt":
        kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
        kernel32.OpenProcess.restype = ctypes.c_void_p
        kernel32.OpenProcess.argtypes = [
            ctypes.c_uint32,
            ctypes.c_int,
            ctypes.c_uint32,
        ]
        kernel32.GetExitCodeProcess.restype = ctypes.c_int
        kernel32.GetExitCodeProcess.argtypes = [
            ctypes.c_void_p,
            ctypes.POINTER(ctypes.c_uint32),
        ]
        kernel32.CloseHandle.restype = ctypes.c_int
        kernel32.CloseHandle.argtypes = [ctypes.c_void_p]
        handle = kernel32.OpenProcess(0x1000, False, process_id)
        if not handle:
            error = ctypes.get_last_error()
            if error == 87:
                return False
            return True
        try:
            exit_code = ctypes.c_uint32()
            if not kernel32.GetExitCodeProcess(handle, ctypes.byref(exit_code)):
                return True
            return exit_code.value == 259
        finally:
            kernel32.CloseHandle(handle)
    try:
        os.kill(process_id, 0)
        return True
    except ProcessLookupError:
        return False
    except PermissionError:
        return True


def wait_process_absent(process_id, grace_ms):
    deadline = time.monotonic() + grace_ms / 1000
    while time.monotonic() <= deadline:
        if not process_exists(process_id):
            return True
        time.sleep(0.01)
    return not process_exists(process_id)


def process_group_exists(process_group):
    try:
        os.killpg(process_group, 0)
        return True
    except ProcessLookupError:
        return False
    except PermissionError:
        return True


def terminate_posix_process_group(process_group, grace_ms):
    if os.name == "nt":
        raise RecoveryError("POSIX process-group recovery is unavailable on Windows")
    if process_group == os.getpgrp():
        raise RecoveryError("refusing to terminate the recovery process group")
    if process_group_exists(process_group):
        try:
            os.killpg(process_group, signal.SIGTERM)
        except ProcessLookupError:
            pass
    deadline = time.monotonic() + grace_ms / 1000
    while process_group_exists(process_group) and time.monotonic() <= deadline:
        time.sleep(0.01)
    if process_group_exists(process_group):
        try:
            os.killpg(process_group, signal.SIGKILL)
        except ProcessLookupError:
            pass
    deadline = time.monotonic() + grace_ms / 1000
    while process_group_exists(process_group) and time.monotonic() <= deadline:
        time.sleep(0.01)
    return not process_group_exists(process_group)


def validate_recovery_receipt(receipt, ledger):
    try:
        jsonschema.Draft202012Validator(
            ledger.load_schema("recovery-receipt.schema.json")
        ).validate(receipt)
    except jsonschema.ValidationError as error:
        raise RecoveryError(
            f"recovery receipt schema failed: {error.message}"
        ) from error


def fault_checkpoint(fault_hook, point, **details):
    if fault_hook is not None:
        fault_hook(point, details)


def record_of_type(records, record_type):
    matches = [record for record in records if record["recordType"] == record_type]
    return matches[-1] if matches else None


def classify_journal(process_log, supervisor):
    if not process_log.exists():
        return b"", [], 0, None
    raw, records, truncated_tail = supervisor.read_verified_process_log(
        process_log, allow_no_complete_record=True
    )
    report = supervisor.verify_process_log(process_log) if records else None
    return raw, records, truncated_tail, report


def build_recovery_decision(
    records,
    report,
    expected_supervisor_process_id,
    tree_probe_grace_ms,
):
    start = records[0]["payload"] if records else None
    if start is not None and (
        start["supervisorProcessId"] != expected_supervisor_process_id
    ):
        raise RecoveryError("expected supervisor PID differs from the journal")
    if not wait_process_absent(expected_supervisor_process_id, tree_probe_grace_ms):
        raise RecoveryError("supervisor process is still active")

    intent = record_of_type(records, "launchIntent")
    child = record_of_type(records, "childStarted")
    launch_failure = record_of_type(records, "launchFailure")
    child_exit = record_of_type(records, "childExit")
    terminal = record_of_type(records, "supervisorTerminal")
    child_process_id = child["payload"]["processId"] if child else None
    containment = child["payload"]["containment"] if child else None
    child_absent = None
    process_group_absent = None

    if not records:
        launch_state = "beforeSupervisorStart"
        cleanup_action = "notPossible"
        tree_status = "uncertain"
        tree_safe = False
    elif intent is None:
        launch_state = "supervisorStarted"
        cleanup_action = "none"
        tree_status = "notObserved"
        tree_safe = True
    elif (
        child is None
        and launch_failure is not None
        and launch_failure["payload"]["stage"] == "createProcess"
    ):
        launch_state = (
            "supervisorTerminal" if terminal else "launchFailedBeforeChild"
        )
        cleanup_action = "none"
        tree_status = "notObserved"
        tree_safe = True
    elif child is None:
        launch_state = "launchAmbiguous"
        cleanup_action = "notPossible"
        tree_status = "uncertain"
        tree_safe = False
    else:
        if terminal is not None:
            launch_state = "supervisorTerminal"
        elif child_exit is not None:
            launch_state = "childExitObserved"
        else:
            launch_state = "contained"
        if containment == "windowsJobObject":
            cleanup_action = "observeWindowsJobClose"
            child_absent = wait_process_absent(
                child_process_id, tree_probe_grace_ms
            )
            tree_safe = child_absent
        elif containment == "posixProcessGroup":
            cleanup_action = "terminatePosixProcessGroup"
            process_group_absent = terminate_posix_process_group(
                child_process_id, tree_probe_grace_ms
            )
            child_absent = wait_process_absent(
                child_process_id, tree_probe_grace_ms
            )
            tree_safe = process_group_absent and child_absent
        else:
            raise RecoveryError("journal containment type is unsupported")
        if not tree_safe:
            tree_status = "uncertain"
        elif child_exit is not None and child_exit["payload"]["treeStatus"] in (
            "exitedCleanly",
            "terminated",
        ):
            tree_status = child_exit["payload"]["treeStatus"]
        else:
            tree_status = "terminated"

    if (
        terminal is not None
        and terminal["payload"]["status"]
        == "childExitedZeroAwaitingBundleVerification"
    ):
        raise RecoveryError(
            "exit-zero journal awaits independent bundle verification"
        )
    classification = (
        "launcherRecoveredOrphanedStart" if tree_safe else "processTreeUncertain"
    )
    return {
        "launchState": launch_state,
        "childProcessId": child_process_id,
        "containment": containment,
        "cleanupAction": cleanup_action,
        "supervisorAbsent": True,
        "childAbsent": child_absent,
        "processGroupAbsent": process_group_absent,
        "treeStatus": tree_status,
        "treeSafe": tree_safe,
        "ledgerExitClassification": classification,
        "journalTerminalStatus": (
            report["terminalStatus"] if report is not None else None
        ),
    }


def validate_existing_receipt(
    receipt,
    ledger,
    start,
    start_raw,
    expected_supervisor_process_id,
    process_log_inventory,
    supervisor_sha256,
    recovery_sha256,
    expected_state,
):
    validate_recovery_receipt(receipt, ledger)
    expected = {
        "jobId": start["jobId"],
        "attemptId": start["attemptId"],
        "attemptNumber": start["attemptNumber"],
        "startReceiptSha256": ledger.sha256_bytes(start_raw),
        "commandSha256": start["commandSha256"],
        "supervisorToolSha256": supervisor_sha256,
        "recoveryToolSha256": recovery_sha256,
        "expectedSupervisorProcessId": expected_supervisor_process_id,
        "processLogInventory": process_log_inventory,
    }
    if any(receipt[field] != value for field, value in expected.items()):
        raise RecoveryError("existing recovery receipt changed its evidence binding")
    if any(receipt[field] != value for field, value in expected_state.items()):
        raise RecoveryError("existing recovery receipt changed its recovery decision")
    if ledger.parse_utc(receipt["observedUtc"]) < ledger.parse_utc(
        start["startedUtc"]
    ):
        raise RecoveryError("existing recovery receipt predates the attempt")


def recover_open_attempt(
    ledger_root,
    job_id,
    attempt_number,
    forbidden_roots,
    expected_supervisor_process_id,
    tree_probe_grace_ms=2000,
    observed_utc=None,
    fault_hook=None,
):
    if not isinstance(tree_probe_grace_ms, int) or not (
        0 <= tree_probe_grace_ms <= 60000
    ):
        raise RecoveryError("tree probe grace is outside the contract")
    ledger, supervisor = load_dependencies()
    inspection = ledger.inspect_ledger(ledger_root, job_id, forbidden_roots)
    if (
        inspection["ledgerState"] != "attemptOpen"
        or inspection["openAttemptNumber"] != attempt_number
    ):
        raise RecoveryError("recovery requires the current open attempt")
    root = ledger.job_root(ledger_root, job_id, forbidden_roots)
    attempt_path = root / f"attempt-{attempt_number:02d}"
    start, start_raw = ledger.read_canonical_json(
        attempt_path / "attempt-start.json"
    )
    process_log = attempt_path / start["processLogRelativePath"]
    raw, records, truncated_tail, report = classify_journal(
        process_log, supervisor
    )
    supervisor_sha256 = supervisor.stable_file_identity(supervisor.__file__)[1]
    recovery_sha256 = supervisor.stable_file_identity(__file__)[1]
    if records and records[0]["payload"]["supervisorSha256"] != supervisor_sha256:
        raise RecoveryError("journal supervisor hash differs from the recovery tool")
    decision = build_recovery_decision(
        records,
        report,
        expected_supervisor_process_id,
        tree_probe_grace_ms,
    )
    process_log_inventory = ledger.file_inventory(process_log)
    if process_log_inventory["bytes"] != len(raw) or (
        process_log_inventory["sha256"]
        != (ledger.sha256_bytes(raw) if process_log_inventory["exists"] else None)
    ):
        raise RecoveryError("process journal changed during recovery inspection")
    disposition = ledger.expected_disposition(
        attempt_number,
        "notRun",
        decision["ledgerExitClassification"],
        decision["treeStatus"],
    )
    journal_state = "noCompleteRecord" if not records else report["status"]
    expected_receipt_state = {
        "journalState": journal_state,
        "journalRecordCount": len(records),
        "journalTruncatedTailBytes": truncated_tail,
        "journalFinalRecordSha256": (
            records[-1]["recordSha256"] if records else None
        ),
        "journalTerminalStatus": decision["journalTerminalStatus"],
        "launchState": decision["launchState"],
        "childProcessId": decision["childProcessId"],
        "containment": decision["containment"],
        "cleanupAction": decision["cleanupAction"],
        "supervisorAbsent": decision["supervisorAbsent"],
        "childAbsent": decision["childAbsent"],
        "processGroupAbsent": decision["processGroupAbsent"],
        "treeStatus": decision["treeStatus"],
        "treeSafe": decision["treeSafe"],
        "ledgerExitClassification": decision["ledgerExitClassification"],
        "retryAuthorized": disposition == "recoveryAuthorized",
        "ledgerRetryDisposition": disposition,
        "outcomeFieldsRead": False,
    }
    receipt_path = attempt_path / "recovery-receipt.json"
    if receipt_path.exists():
        receipt, receipt_raw = ledger.read_canonical_recovery_receipt(receipt_path)
        validate_existing_receipt(
            receipt,
            ledger,
            start,
            start_raw,
            expected_supervisor_process_id,
            process_log_inventory,
            supervisor_sha256,
            recovery_sha256,
            expected_receipt_state,
        )
    else:
        proposed_observed_utc = ledger.normalize_utc(
            observed_utc or utc_now(), "observedUtc"
        )
        if ledger.parse_utc(proposed_observed_utc) < ledger.parse_utc(
            start["startedUtc"]
        ):
            raise RecoveryError("recovery observation predates the attempt")
        receipt = {
            "schemaVersion": "1.0.0",
            "schemaId": RECOVERY_SCHEMA_ID,
            "recordType": "wp14rRecoveryReceipt",
            "recoveryVersion": RECOVERY_VERSION,
            "claimBoundary": CLAIM_BOUNDARY,
            "jobId": job_id,
            "attemptId": start["attemptId"],
            "attemptNumber": attempt_number,
            "observedUtc": proposed_observed_utc,
            "startReceiptSha256": ledger.sha256_bytes(start_raw),
            "commandSha256": start["commandSha256"],
            "supervisorToolSha256": supervisor_sha256,
            "recoveryToolSha256": recovery_sha256,
            "expectedSupervisorProcessId": expected_supervisor_process_id,
            "processLogInventory": process_log_inventory,
            **expected_receipt_state,
        }
        validate_recovery_receipt(receipt, ledger)
        receipt_raw = ledger.canonical(receipt)
        try:
            ledger.write_exclusive(receipt_path, receipt_raw)
        except FileExistsError as error:
            raise RecoveryError("recovery receipt publication raced") from error
    fault_checkpoint(
        fault_hook,
        "afterRecoveryReceiptBeforeTerminal",
        recoveryReceiptSha256=ledger.sha256_bytes(receipt_raw),
    )
    if not wait_process_absent(expected_supervisor_process_id, tree_probe_grace_ms):
        raise RecoveryError("supervisor reappeared before terminal publication")
    if ledger.file_inventory(process_log) != receipt["processLogInventory"]:
        raise RecoveryError("process journal changed after recovery receipt")
    if (
        supervisor.stable_file_identity(supervisor.__file__)[1]
        != receipt["supervisorToolSha256"]
        or supervisor.stable_file_identity(__file__)[1]
        != receipt["recoveryToolSha256"]
    ):
        raise RecoveryError("recovery source changed before terminal publication")
    elapsed_ms = max(
        0,
        round(
            (
                ledger.parse_utc(receipt["observedUtc"])
                - ledger.parse_utc(start["startedUtc"])
            ).total_seconds()
            * 1000
        ),
    )
    terminal = ledger.terminalize_attempt(
        ledger_root,
        job_id,
        attempt_number,
        receipt["ledgerExitClassification"],
        elapsed_ms,
        receipt["treeStatus"],
        "notRun",
        forbidden_roots,
        process_exit_code=None,
        terminal_utc=receipt["observedUtc"],
        recovery_receipt_sha256=ledger.sha256_bytes(receipt_raw),
    )
    return {"recoveryReceipt": receipt, "attemptTerminal": terminal}


def build_parser():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--ledger-root", type=pathlib.Path, required=True)
    parser.add_argument("--job-id", required=True)
    parser.add_argument("--attempt-number", type=int, required=True)
    parser.add_argument(
        "--forbidden-root", type=pathlib.Path, action="append", required=True
    )
    parser.add_argument("--expected-supervisor-pid", type=int, required=True)
    parser.add_argument("--tree-probe-grace-ms", type=int, default=2000)
    parser.add_argument("--observed-utc")
    return parser


def main(argv=None):
    arguments = build_parser().parse_args(argv)
    try:
        result = recover_open_attempt(
            arguments.ledger_root,
            arguments.job_id,
            arguments.attempt_number,
            arguments.forbidden_root,
            arguments.expected_supervisor_pid,
            arguments.tree_probe_grace_ms,
            arguments.observed_utc,
        )
        ledger, _ = load_dependencies()
        sys.stdout.buffer.write(ledger.canonical(result) + b"\n")
        return 0
    except Exception as error:  # noqa: BLE001 - CLI collapses dependency failures
        print(f"WP14R_RECOVERY_ERROR: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
