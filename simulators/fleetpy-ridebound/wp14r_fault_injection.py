#!/usr/bin/env python3
"""Hard-kill a WP14R supervisor at one predeclared mechanics-only barrier."""

from __future__ import annotations

import argparse
import importlib.util
import json
import os
import pathlib
import subprocess
import sys
import time

sys.dont_write_bytecode = True

FAULT_POINTS = (
    "beforeSupervisorStart",
    "afterSupervisorStart",
    "afterLaunchIntent",
    "afterProcessCreatedBeforeContainment",
    "afterContainmentBeforeChildStarted",
    "afterChildStarted",
    "afterStreamChunk",
    "afterProcessExitBeforeStreamEof",
    "afterStreamsEofBeforeChildExit",
    "afterChildExit",
    "afterSupervisorTerminal",
)
CONFIG_KEYS = {
    "ledgerRoot",
    "jobId",
    "attemptNumber",
    "forbiddenRoots",
    "executable",
    "arguments",
    "workingDirectory",
    "inheritedEnvironmentNames",
    "wallTimeoutMs",
    "heartbeatIntervalMs",
    "maximumStreamBytes",
    "chunkBytes",
    "treeExitGraceMs",
    "faultPoint",
    "barrierPath",
}
RECOVERY_CONFIG_KEYS = {
    "ledgerRoot",
    "jobId",
    "attemptNumber",
    "forbiddenRoots",
    "expectedSupervisorProcessId",
    "treeProbeGraceMs",
    "observedUtc",
    "faultPoint",
    "barrierPath",
}


class FaultInjectionError(RuntimeError):
    """A fail-closed fault-harness condition."""


def load_supervisor():
    path = pathlib.Path(__file__).resolve().with_name(
        "wp14r_supervised_process.py"
    )
    specification = importlib.util.spec_from_file_location(
        "wp14r_supervisor_for_fault_injection", path
    )
    if specification is None or specification.loader is None:
        raise FaultInjectionError("cannot load WP14R supervisor")
    module = importlib.util.module_from_spec(specification)
    specification.loader.exec_module(module)
    return module


def load_recovery():
    path = pathlib.Path(__file__).resolve().with_name(
        "wp14r_stale_open_recovery.py"
    )
    specification = importlib.util.spec_from_file_location(
        "wp14r_recovery_for_fault_injection", path
    )
    if specification is None or specification.loader is None:
        raise FaultInjectionError("cannot load WP14R stale-open recovery")
    module = importlib.util.module_from_spec(specification)
    specification.loader.exec_module(module)
    return module


def read_canonical_config(
    path,
    supervisor,
    expected_keys=CONFIG_KEYS,
    allowed_points=FAULT_POINTS,
):
    path = pathlib.Path(path)
    try:
        raw = path.read_bytes()
        document = json.loads(raw.decode("utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise FaultInjectionError("cannot read fault-worker config") from error
    if raw != supervisor.canonical(document):
        raise FaultInjectionError("fault-worker config is not byte-canonical")
    if not isinstance(document, dict) or set(document) != expected_keys:
        raise FaultInjectionError("fault-worker config fields are not exact")
    if document["faultPoint"] not in allowed_points:
        raise FaultInjectionError("fault point is not predeclared")
    return document


def validate_control_root(control_root, config, supervisor):
    ledger = supervisor.load_ledger_module()
    forbidden = [
        pathlib.Path(config["ledgerRoot"]),
        *[pathlib.Path(value) for value in config["forbiddenRoots"]],
    ]
    try:
        return ledger.validate_ledger_root(control_root, forbidden)
    except ledger.LedgerError as error:
        raise FaultInjectionError("fault control root is unsafe") from error


class BarrierHook:
    def __init__(self, target, barrier_path, supervisor):
        self.target = target
        self.barrier_path = pathlib.Path(barrier_path)
        self.supervisor = supervisor
        self.triggered = False

    def __call__(self, point, details):
        if point != self.target or self.triggered:
            return
        self.triggered = True
        document = {
            "recordType": "wp14rFaultBarrier",
            "faultPoint": point,
            "workerProcessId": os.getpid(),
            "details": details,
        }
        self.supervisor.load_ledger_module().write_exclusive(
            self.barrier_path, self.supervisor.canonical(document)
        )
        while True:
            time.sleep(1)


def execute_worker(config_path):
    supervisor = load_supervisor()
    config = read_canonical_config(config_path, supervisor)
    validate_control_root(
        pathlib.Path(config["barrierPath"]).parent, config, supervisor
    )
    hook = BarrierHook(
        config["faultPoint"], config["barrierPath"], supervisor
    )
    supervisor.supervise_process(
        pathlib.Path(config["ledgerRoot"]),
        config["jobId"],
        config["attemptNumber"],
        [pathlib.Path(value) for value in config["forbiddenRoots"]],
        pathlib.Path(config["executable"]),
        config["arguments"],
        pathlib.Path(config["workingDirectory"]),
        config["inheritedEnvironmentNames"],
        config["wallTimeoutMs"],
        config["heartbeatIntervalMs"],
        config["maximumStreamBytes"],
        config["chunkBytes"],
        config["treeExitGraceMs"],
        os.environ,
        hook,
    )
    raise FaultInjectionError("supervisor completed before the target barrier")


def execute_recovery_worker(config_path):
    supervisor = load_supervisor()
    recovery = load_recovery()
    config = read_canonical_config(
        config_path,
        supervisor,
        RECOVERY_CONFIG_KEYS,
        ("afterRecoveryReceiptBeforeTerminal",),
    )
    validate_control_root(
        pathlib.Path(config["barrierPath"]).parent, config, supervisor
    )
    hook = BarrierHook(
        config["faultPoint"], config["barrierPath"], supervisor
    )
    recovery.recover_open_attempt(
        pathlib.Path(config["ledgerRoot"]),
        config["jobId"],
        config["attemptNumber"],
        [pathlib.Path(value) for value in config["forbiddenRoots"]],
        config["expectedSupervisorProcessId"],
        config["treeProbeGraceMs"],
        config["observedUtc"],
        hook,
    )
    raise FaultInjectionError("recovery completed before the target barrier")


def wait_for_barrier(
    path, worker, timeout_seconds, supervisor, expected_fault_point
):
    deadline = time.monotonic() + timeout_seconds
    path = pathlib.Path(path)
    while time.monotonic() <= deadline:
        if path.exists():
            before = path.stat()
            raw = path.read_bytes()
            after = path.stat()
            if (
                before.st_size != after.st_size
                or before.st_mtime_ns != after.st_mtime_ns
            ):
                raise FaultInjectionError("fault barrier changed while read")
            try:
                document = json.loads(raw.decode("utf-8"))
            except (UnicodeError, json.JSONDecodeError) as error:
                raise FaultInjectionError("fault barrier is invalid JSON") from error
            if raw != supervisor.canonical(document):
                raise FaultInjectionError("fault barrier is not byte-canonical")
            if (
                not isinstance(document, dict)
                or set(document)
                != {"recordType", "faultPoint", "workerProcessId", "details"}
                or document["recordType"] != "wp14rFaultBarrier"
                or document["faultPoint"] != expected_fault_point
                or not isinstance(document["details"], dict)
            ):
                raise FaultInjectionError("fault barrier fields are inconsistent")
            return document
        if worker.poll() is not None:
            raise FaultInjectionError(
                f"fault worker exited before barrier: {worker.returncode}"
            )
        time.sleep(0.01)
    raise FaultInjectionError("fault worker did not reach its barrier")


def run_hard_kill(config, control_root, timeout_seconds=10):
    supervisor = load_supervisor()
    if not isinstance(timeout_seconds, (int, float)) or not (
        0 < timeout_seconds <= 60
    ):
        raise FaultInjectionError("fault timeout is outside the contract")
    control_root = validate_control_root(control_root, config, supervisor)
    control_root.mkdir(parents=True, exist_ok=False)
    config_path = control_root / "worker-config.json"
    barrier_path = control_root / "barrier.json"
    stdout_path = control_root / "worker.stdout"
    stderr_path = control_root / "worker.stderr"
    document = dict(config)
    document["barrierPath"] = str(barrier_path)
    if set(document) != CONFIG_KEYS:
        raise FaultInjectionError("fault config fields are not exact")
    if document["faultPoint"] not in FAULT_POINTS:
        raise FaultInjectionError("fault point is not predeclared")
    supervisor.load_ledger_module().write_exclusive(
        config_path, supervisor.canonical(document)
    )
    options = {}
    if os.name == "nt":
        options["creationflags"] = subprocess.CREATE_NEW_PROCESS_GROUP
    else:
        options["start_new_session"] = True
    with stdout_path.open("xb") as stdout, stderr_path.open("xb") as stderr:
        worker = subprocess.Popen(
            [
                sys.executable,
                "-B",
                str(pathlib.Path(__file__).resolve()),
                "worker",
                "--config",
                str(config_path),
            ],
            stdin=subprocess.DEVNULL,
            stdout=stdout,
            stderr=stderr,
            **options,
        )
        try:
            barrier = wait_for_barrier(
                barrier_path,
                worker,
                timeout_seconds,
                supervisor,
                document["faultPoint"],
            )
        except Exception:
            worker.kill()
            worker.wait(timeout=5)
            raise
        if barrier.get("workerProcessId") != worker.pid:
            worker.kill()
            worker.wait(timeout=5)
            raise FaultInjectionError("barrier worker PID does not match its process")
        worker.kill()
        return_code = worker.wait(timeout=5)
        stdout.flush()
        stderr.flush()
        os.fsync(stdout.fileno())
        os.fsync(stderr.fileno())
    return {
        "faultPoint": document["faultPoint"],
        "workerProcessId": worker.pid,
        "workerExitCode": return_code,
        "barrier": barrier,
        "controlRoot": str(control_root),
    }


def run_recovery_hard_kill(config, control_root, timeout_seconds=10):
    supervisor = load_supervisor()
    if not isinstance(timeout_seconds, (int, float)) or not (
        0 < timeout_seconds <= 60
    ):
        raise FaultInjectionError("fault timeout is outside the contract")
    control_root = validate_control_root(control_root, config, supervisor)
    control_root.mkdir(parents=True, exist_ok=False)
    config_path = control_root / "recovery-worker-config.json"
    barrier_path = control_root / "recovery-barrier.json"
    stdout_path = control_root / "recovery-worker.stdout"
    stderr_path = control_root / "recovery-worker.stderr"
    document = dict(config)
    document["barrierPath"] = str(barrier_path)
    if set(document) != RECOVERY_CONFIG_KEYS:
        raise FaultInjectionError("recovery fault config fields are not exact")
    if document["faultPoint"] != "afterRecoveryReceiptBeforeTerminal":
        raise FaultInjectionError("recovery fault point is not predeclared")
    supervisor.load_ledger_module().write_exclusive(
        config_path, supervisor.canonical(document)
    )
    options = {}
    if os.name == "nt":
        options["creationflags"] = subprocess.CREATE_NEW_PROCESS_GROUP
    else:
        options["start_new_session"] = True
    with stdout_path.open("xb") as stdout, stderr_path.open("xb") as stderr:
        worker = subprocess.Popen(
            [
                sys.executable,
                "-B",
                str(pathlib.Path(__file__).resolve()),
                "recovery-worker",
                "--config",
                str(config_path),
            ],
            stdin=subprocess.DEVNULL,
            stdout=stdout,
            stderr=stderr,
            **options,
        )
        try:
            barrier = wait_for_barrier(
                barrier_path,
                worker,
                timeout_seconds,
                supervisor,
                document["faultPoint"],
            )
        except Exception:
            worker.kill()
            worker.wait(timeout=5)
            raise
        if barrier.get("workerProcessId") != worker.pid:
            worker.kill()
            worker.wait(timeout=5)
            raise FaultInjectionError("recovery barrier PID is inconsistent")
        worker.kill()
        return_code = worker.wait(timeout=5)
        stdout.flush()
        stderr.flush()
        os.fsync(stdout.fileno())
        os.fsync(stderr.fileno())
    return {
        "faultPoint": document["faultPoint"],
        "workerProcessId": worker.pid,
        "workerExitCode": return_code,
        "barrier": barrier,
        "controlRoot": str(control_root),
    }


def build_parser():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    worker = commands.add_parser("worker")
    worker.add_argument("--config", type=pathlib.Path, required=True)
    recovery_worker = commands.add_parser("recovery-worker")
    recovery_worker.add_argument("--config", type=pathlib.Path, required=True)
    return parser


def main(argv=None):
    arguments = build_parser().parse_args(argv)
    try:
        if arguments.command == "worker":
            execute_worker(arguments.config)
        else:
            execute_recovery_worker(arguments.config)
        return 0
    except Exception as error:  # noqa: BLE001 - worker must fail closed
        print(f"WP14R_FAULT_INJECTION_ERROR: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
