#!/usr/bin/env python3
"""Run a child with bounded, incremental WP14R process evidence."""

from __future__ import annotations

import argparse
import base64
import ctypes
import datetime
import functools
import hashlib
import importlib.util
import json
import os
import pathlib
import platform
import queue
import re
import signal
import stat
import subprocess
import sys
import threading
import time

import jsonschema

sys.dont_write_bytecode = True

LOG_SCHEMA_ID = (
    "https://ridebound.local/schemas/wp14r/v1/"
    "supervision-log-record.schema.json"
)
REPORT_SCHEMA_ID = (
    "https://ridebound.local/schemas/wp14r/v1/"
    "supervision-report.schema.json"
)
LOG_VERSION = "wp14r-supervision-log-v1"
SCHEMA_PROVENANCE_VERSION = "wp14r-supervision-schema-provenance-v1"
ZERO_SHA256 = "0" * 64
EMPTY_SHA256 = hashlib.sha256(b"").hexdigest()
ENVIRONMENT_NAME = re.compile(r"^[A-Za-z_][A-Za-z0-9_]{0,127}$")
CLAIM_BOUNDARY = [
    "mechanicalOnly",
    "doesNotVerifyBundleOrOutcome",
    "exitZeroAwaitsIndependentBundleVerification",
    "doesNotSupersedeWp14V1",
]
MAXIMUM_LINE_BYTES = 1024 * 1024


class SupervisionError(RuntimeError):
    """A fail-closed supervised-process condition."""


def canonical(document):
    return json.dumps(
        document,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")


def sha256_bytes(content):
    return hashlib.sha256(content).hexdigest()


def utc_now():
    return (
        datetime.datetime.now(datetime.timezone.utc)
        .isoformat(timespec="microseconds")
        .replace("+00:00", "Z")
    )


def repository_root():
    return pathlib.Path(__file__).resolve().parents[2]


def schema_path(name):
    return repository_root() / "benchmarks/schemas/wp14r/v1" / name


@functools.lru_cache(maxsize=None)
def load_schema(name):
    path = schema_path(name)
    try:
        document = json.loads(path.read_text(encoding="utf-8"))
        jsonschema.Draft202012Validator.check_schema(document)
    except (OSError, json.JSONDecodeError, jsonschema.SchemaError) as error:
        raise SupervisionError(f"cannot load valid schema: {path}") from error
    return document


@functools.lru_cache(maxsize=None)
def schema_validator(name):
    return jsonschema.Draft202012Validator(load_schema(name))


def is_link_or_junction(path):
    path = pathlib.Path(path)
    junction_test = getattr(os.path, "isjunction", None)
    if path.is_symlink() or bool(junction_test and junction_test(path)):
        return True
    try:
        attributes = getattr(path.lstat(), "st_file_attributes", 0)
    except OSError:
        return False
    reparse = getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400)
    return bool(attributes & reparse)


def reject_link_ancestry(path, label):
    current = pathlib.Path(path).absolute()
    while True:
        if current.exists() and is_link_or_junction(current):
            raise SupervisionError(
                f"{label} ancestry contains a link or junction"
            )
        if current.parent == current:
            return
        current = current.parent


def load_ledger_module():
    path = pathlib.Path(__file__).resolve().with_name("wp14r_attempt_ledger.py")
    specification = importlib.util.spec_from_file_location(
        "wp14r_attempt_ledger_for_supervision", path
    )
    if specification is None or specification.loader is None:
        raise SupervisionError("cannot load the WP14R attempt ledger")
    module = importlib.util.module_from_spec(specification)
    specification.loader.exec_module(module)
    return module


def validate_log_record(record):
    try:
        schema_validator("supervision-log-record.schema.json").validate(record)
    except jsonschema.ValidationError as error:
        raise SupervisionError(
            f"supervision log schema failed: {error.message}"
        ) from error


def validate_report(report):
    try:
        schema_validator("supervision-report.schema.json").validate(report)
    except jsonschema.ValidationError as error:
        raise SupervisionError(
            f"supervision report schema failed: {error.message}"
        ) from error


def validate_policy(
    wall_timeout_ms,
    heartbeat_interval_ms,
    maximum_stream_bytes,
    chunk_bytes,
    tree_exit_grace_ms,
):
    bounds = (
        (wall_timeout_ms, 1, 86400000, "wall timeout"),
        (heartbeat_interval_ms, 50, 60000, "heartbeat interval"),
        (maximum_stream_bytes, 1, 1073741824, "stream cap"),
        (chunk_bytes, 1, 65536, "chunk bytes"),
        (tree_exit_grace_ms, 0, 60000, "tree exit grace"),
    )
    for value, minimum, maximum, label in bounds:
        if not isinstance(value, int) or not minimum <= value <= maximum:
            raise SupervisionError(f"{label} is outside the contract")


def stable_file_identity(path):
    raw_path = pathlib.Path(path).absolute()
    reject_link_ancestry(raw_path, "file")
    path = raw_path.resolve()
    if not path.is_file() or is_link_or_junction(path):
        raise SupervisionError(f"executable is not a regular file: {path}")
    before = path.stat()
    digest = hashlib.sha256()
    length = 0
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
            length += len(block)
    after = path.stat()
    if (
        length != before.st_size
        or after.st_size != before.st_size
        or after.st_mtime_ns != before.st_mtime_ns
    ):
        raise SupervisionError("executable changed while it was hashed")
    return path, digest.hexdigest()


def current_schema_provenance():
    return {
        "supervisionEvidenceVersion": SCHEMA_PROVENANCE_VERSION,
        "logSchemaSha256": stable_file_identity(
            schema_path("supervision-log-record.schema.json")
        )[1],
        "reportSchemaSha256": stable_file_identity(
            schema_path("supervision-report.schema.json")
        )[1],
    }


def verify_schema_provenance(start):
    fields = (
        "supervisionEvidenceVersion",
        "logSchemaSha256",
        "reportSchemaSha256",
    )
    present = [field in start for field in fields]
    if not any(present):
        return None
    if not all(present):
        raise SupervisionError("schema provenance fields are incomplete")
    expected = current_schema_provenance()
    if any(start[field] != expected[field] for field in fields):
        raise SupervisionError("journal schema provenance differs from source")
    return expected


def build_command_binding(
    executable,
    arguments,
    working_directory,
    inherited_environment_names,
    source_environment=None,
):
    executable, executable_sha256 = stable_file_identity(executable)
    raw_working_directory = pathlib.Path(working_directory).absolute()
    reject_link_ancestry(raw_working_directory, "working directory")
    working_directory = raw_working_directory.resolve()
    if (
        not working_directory.is_dir()
        or is_link_or_junction(working_directory)
    ):
        raise SupervisionError("working directory is not a regular directory")
    if any(
        not isinstance(value, str) or "\x00" in value for value in arguments
    ):
        raise SupervisionError("every child argument must be a string")
    sort_key = str.casefold if os.name == "nt" else lambda value: value
    names = sorted(inherited_environment_names, key=sort_key)
    comparison_names = [sort_key(name) for name in names]
    if len(comparison_names) != len(set(comparison_names)):
        raise SupervisionError("inherited environment names are duplicated")
    if any(not ENVIRONMENT_NAME.fullmatch(name) for name in names):
        raise SupervisionError("an inherited environment name is invalid")
    source = os.environ if source_environment is None else source_environment
    missing = [name for name in names if name not in source]
    if missing:
        raise SupervisionError(
            f"inherited environment value is missing: {', '.join(missing)}"
        )
    invalid_values = [
        name
        for name in names
        if not isinstance(source[name], str) or "\x00" in source[name]
    ]
    if invalid_values:
        raise SupervisionError("an inherited environment value is invalid")
    effective_environment = {name: source[name] for name in names}
    environment_items = [
        {"name": name, "value": effective_environment[name]} for name in names
    ]
    binding = {
        "bindingVersion": "wp14r-command-binding-v1",
        "executablePath": str(executable),
        "executableSha256": executable_sha256,
        "arguments": list(arguments),
        "workingDirectory": str(working_directory),
        "environment": environment_items,
    }
    metadata = {
        "commandSha256": sha256_bytes(canonical(binding)),
        "executablePath": str(executable),
        "executableSha256": executable_sha256,
        "argumentCount": len(arguments),
        "argumentsSha256": sha256_bytes(canonical(list(arguments))),
        "workingDirectory": str(working_directory),
        "inheritedEnvironmentNames": names,
        "environmentBindingSha256": sha256_bytes(
            canonical(environment_items)
        ),
    }
    return metadata, effective_environment


class JournalWriter:
    def __init__(self, path):
        self.path = pathlib.Path(path)
        self.started = time.monotonic()
        self.sequence = 0
        self.previous = ZERO_SHA256
        flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL
        flags |= getattr(os, "O_BINARY", 0)
        try:
            self.descriptor = os.open(self.path, flags, 0o600)
        except FileExistsError as error:
            raise SupervisionError("process journal already exists") from error

    def close(self):
        if self.descriptor is not None:
            os.close(self.descriptor)
            self.descriptor = None

    def _write_all(self, content):
        offset = 0
        while offset < len(content):
            written = os.write(self.descriptor, content[offset:])
            if written <= 0:
                raise SupervisionError("process journal write made no progress")
            offset += written
        os.fsync(self.descriptor)

    def append(self, record_type, payload):
        record = {
            "schemaVersion": "1.0.0",
            "schemaId": LOG_SCHEMA_ID,
            "logVersion": LOG_VERSION,
            "recordType": record_type,
            "sequence": self.sequence,
            "observedUtc": utc_now(),
            "monotonicElapsedMs": round(
                (time.monotonic() - self.started) * 1000
            ),
            "previousRecordSha256": self.previous,
            "payload": payload,
        }
        record_hash = sha256_bytes(canonical(record))
        record["recordSha256"] = record_hash
        validate_log_record(record)
        line = canonical(record) + b"\n"
        if len(line) > MAXIMUM_LINE_BYTES:
            raise SupervisionError("process journal line exceeds the hard bound")
        self._write_all(line)
        self.sequence += 1
        self.previous = record_hash
        return record


class PosixProcessGroup:
    containment = "posixProcessGroup"

    def __init__(self, process):
        self.process = process
        self.process_group = process.pid

    def _alive(self):
        try:
            os.killpg(self.process_group, 0)
            return True
        except ProcessLookupError:
            return False
        except PermissionError:
            return True

    def terminate(self, grace_ms):
        if self._alive():
            try:
                os.killpg(self.process_group, signal.SIGTERM)
            except ProcessLookupError:
                pass
        deadline = time.monotonic() + grace_ms / 1000
        while self._alive() and time.monotonic() < deadline:
            time.sleep(0.01)
        if self._alive():
            try:
                os.killpg(self.process_group, signal.SIGKILL)
            except ProcessLookupError:
                pass
        return "terminated" if not self._alive() else "uncertain"

    def finalize(self, grace_ms, cleanup_required):
        if cleanup_required:
            return self.terminate(grace_ms), True
        deadline = time.monotonic() + grace_ms / 1000
        while self._alive() and time.monotonic() < deadline:
            time.sleep(0.01)
        if not self._alive():
            return "exitedCleanly", False
        return self.terminate(grace_ms), True

    def close(self):
        return None


class _JobBasicLimitInformation(ctypes.Structure):
    _fields_ = [
        ("PerProcessUserTimeLimit", ctypes.c_longlong),
        ("PerJobUserTimeLimit", ctypes.c_longlong),
        ("LimitFlags", ctypes.c_uint32),
        ("MinimumWorkingSetSize", ctypes.c_size_t),
        ("MaximumWorkingSetSize", ctypes.c_size_t),
        ("ActiveProcessLimit", ctypes.c_uint32),
        ("Affinity", ctypes.c_size_t),
        ("PriorityClass", ctypes.c_uint32),
        ("SchedulingClass", ctypes.c_uint32),
    ]


class _JobIoCounters(ctypes.Structure):
    _fields_ = [("value", ctypes.c_ulonglong * 6)]


class _JobExtendedLimitInformation(ctypes.Structure):
    _fields_ = [
        ("BasicLimitInformation", _JobBasicLimitInformation),
        ("IoInfo", _JobIoCounters),
        ("ProcessMemoryLimit", ctypes.c_size_t),
        ("JobMemoryLimit", ctypes.c_size_t),
        ("PeakProcessMemoryUsed", ctypes.c_size_t),
        ("PeakJobMemoryUsed", ctypes.c_size_t),
    ]


class _JobBasicAccountingInformation(ctypes.Structure):
    _fields_ = [
        ("TotalUserTime", ctypes.c_longlong),
        ("TotalKernelTime", ctypes.c_longlong),
        ("ThisPeriodTotalUserTime", ctypes.c_longlong),
        ("ThisPeriodTotalKernelTime", ctypes.c_longlong),
        ("TotalPageFaultCount", ctypes.c_uint32),
        ("TotalProcesses", ctypes.c_uint32),
        ("ActiveProcesses", ctypes.c_uint32),
        ("TotalTerminatedProcesses", ctypes.c_uint32),
    ]


class WindowsJobObject:
    containment = "windowsJobObject"
    JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000
    JOB_OBJECT_EXTENDED_LIMIT_INFORMATION = 9
    JOB_OBJECT_BASIC_ACCOUNTING_INFORMATION = 1

    def __init__(self, process):
        self.process = process
        self.kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
        self.kernel32.CreateJobObjectW.restype = ctypes.c_void_p
        self.kernel32.CreateJobObjectW.argtypes = [ctypes.c_void_p, ctypes.c_wchar_p]
        self.kernel32.SetInformationJobObject.restype = ctypes.c_int
        self.kernel32.SetInformationJobObject.argtypes = [
            ctypes.c_void_p,
            ctypes.c_int,
            ctypes.c_void_p,
            ctypes.c_uint32,
        ]
        self.kernel32.AssignProcessToJobObject.restype = ctypes.c_int
        self.kernel32.AssignProcessToJobObject.argtypes = [
            ctypes.c_void_p,
            ctypes.c_void_p,
        ]
        self.kernel32.QueryInformationJobObject.restype = ctypes.c_int
        self.kernel32.QueryInformationJobObject.argtypes = [
            ctypes.c_void_p,
            ctypes.c_int,
            ctypes.c_void_p,
            ctypes.c_uint32,
            ctypes.c_void_p,
        ]
        self.kernel32.TerminateJobObject.restype = ctypes.c_int
        self.kernel32.TerminateJobObject.argtypes = [
            ctypes.c_void_p,
            ctypes.c_uint32,
        ]
        self.kernel32.CloseHandle.restype = ctypes.c_int
        self.kernel32.CloseHandle.argtypes = [ctypes.c_void_p]
        self.handle = self.kernel32.CreateJobObjectW(None, None)
        if not self.handle:
            raise SupervisionError("CreateJobObjectW failed")
        information = _JobExtendedLimitInformation()
        information.BasicLimitInformation.LimitFlags = (
            self.JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
        )
        if not self.kernel32.SetInformationJobObject(
            self.handle,
            self.JOB_OBJECT_EXTENDED_LIMIT_INFORMATION,
            ctypes.byref(information),
            ctypes.sizeof(information),
        ):
            self.close()
            raise SupervisionError("SetInformationJobObject failed")
        if not self.kernel32.AssignProcessToJobObject(
            self.handle, ctypes.c_void_p(process._handle)
        ):
            self.close()
            raise SupervisionError("AssignProcessToJobObject failed")

    def _active_processes(self):
        information = _JobBasicAccountingInformation()
        returned = ctypes.c_uint32()
        if not self.kernel32.QueryInformationJobObject(
            self.handle,
            self.JOB_OBJECT_BASIC_ACCOUNTING_INFORMATION,
            ctypes.byref(information),
            ctypes.sizeof(information),
            ctypes.byref(returned),
        ):
            raise SupervisionError("QueryInformationJobObject failed")
        return information.ActiveProcesses

    def _wait_empty(self, grace_ms):
        deadline = time.monotonic() + grace_ms / 1000
        while time.monotonic() <= deadline:
            if self._active_processes() == 0:
                return True
            time.sleep(0.01)
        return self._active_processes() == 0

    def terminate(self, grace_ms):
        if not self.kernel32.TerminateJobObject(self.handle, 1):
            return "uncertain"
        return "terminated" if self._wait_empty(grace_ms) else "uncertain"

    def finalize(self, grace_ms, cleanup_required):
        if cleanup_required:
            return self.terminate(grace_ms), True
        if self._wait_empty(grace_ms):
            return "exitedCleanly", False
        return self.terminate(grace_ms), True

    def close(self):
        if getattr(self, "handle", None):
            self.kernel32.CloseHandle(self.handle)
            self.handle = None


def create_process(command, working_directory, environment):
    options = {
        "cwd": str(working_directory),
        "env": environment,
        "stdin": subprocess.DEVNULL,
        "stdout": subprocess.PIPE,
        "stderr": subprocess.PIPE,
        "bufsize": 0,
    }
    if os.name == "nt":
        options["creationflags"] = subprocess.CREATE_NEW_PROCESS_GROUP
    else:
        options["start_new_session"] = True
    return subprocess.Popen(command, **options)


def create_containment(process):
    if os.name == "nt":
        return WindowsJobObject(process)
    return PosixProcessGroup(process)


def close_process_streams(process):
    for stream in (getattr(process, "stdout", None), getattr(process, "stderr", None)):
        if stream is not None:
            try:
                stream.close()
            except OSError:
                pass


def stream_reader(stream_name, stream, chunk_bytes, events):
    digest = hashlib.sha256()
    observed = 0
    try:
        while True:
            chunk = stream.read(chunk_bytes)
            if not chunk:
                break
            digest.update(chunk)
            observed += len(chunk)
            events.put(("chunk", stream_name, chunk))
    except Exception as error:  # noqa: BLE001 - recorded by type/hash only
        events.put(("readerFailure", stream_name, error))
    finally:
        events.put(("eof", stream_name, observed, digest.hexdigest()))
        try:
            stream.close()
        except OSError:
            pass


def host_fingerprint():
    system = platform.system()
    normalized = system if system in ("Windows", "Linux", "Darwin") else "Other"
    identity = {
        "platform": normalized,
        "release": platform.release(),
        "machine": platform.machine(),
        "nodeSha256": sha256_bytes(platform.node().encode("utf-8")),
        "python": platform.python_version(),
    }
    return normalized, sha256_bytes(canonical(identity))


def verify_open_attempt(
    ledger_root,
    job_id,
    attempt_number,
    forbidden_roots,
    command_sha256,
):
    ledger = load_ledger_module()
    report = ledger.inspect_ledger(
        ledger_root, job_id, forbidden_roots
    )
    if (
        report["ledgerState"] != "attemptOpen"
        or report["openAttemptNumber"] != attempt_number
    ):
        raise SupervisionError("supervisor requires the current open attempt")
    root = ledger.job_root(ledger_root, job_id, forbidden_roots)
    attempt_path = root / f"attempt-{attempt_number:02d}"
    start, start_raw = ledger.read_canonical_json(
        attempt_path / "attempt-start.json"
    )
    if start["commandSha256"] != command_sha256:
        raise SupervisionError("effective command differs from the start receipt")
    return ledger, attempt_path, start, start_raw


def _captured_stream_state():
    return {
        "digest": hashlib.sha256(),
        "bytes": 0,
        "chunks": 0,
        "eof": False,
        "observedBytes": None,
        "observedSha256": None,
    }


def _fault_checkpoint(fault_hook, point, **details):
    if fault_hook is not None:
        fault_hook(point, details)


def supervise_process(
    ledger_root,
    job_id,
    attempt_number,
    forbidden_roots,
    executable,
    arguments,
    working_directory,
    inherited_environment_names,
    wall_timeout_ms=60000,
    heartbeat_interval_ms=1000,
    maximum_stream_bytes=16 * 1024 * 1024,
    chunk_bytes=32768,
    tree_exit_grace_ms=2000,
    source_environment=None,
    fault_hook=None,
):
    validate_policy(
        wall_timeout_ms,
        heartbeat_interval_ms,
        maximum_stream_bytes,
        chunk_bytes,
        tree_exit_grace_ms,
    )
    metadata, environment = build_command_binding(
        executable,
        arguments,
        working_directory,
        inherited_environment_names,
        source_environment,
    )
    _, attempt_path, start, start_raw = verify_open_attempt(
        ledger_root,
        job_id,
        attempt_number,
        forbidden_roots,
        metadata["commandSha256"],
    )
    journal_path = attempt_path / start["processLogRelativePath"]
    writer = JournalWriter(journal_path)
    platform_name, fingerprint = host_fingerprint()
    policy = {
        "wallTimeoutMs": wall_timeout_ms,
        "heartbeatIntervalMs": heartbeat_interval_ms,
        "maximumStreamBytes": maximum_stream_bytes,
        "chunkBytes": chunk_bytes,
        "treeExitGraceMs": tree_exit_grace_ms,
    }
    schema_provenance = current_schema_provenance()
    try:
        _fault_checkpoint(fault_hook, "beforeSupervisorStart")
        writer.append(
            "supervisorStart",
            {
                "jobId": job_id,
                "attemptId": start["attemptId"],
                "attemptNumber": attempt_number,
                "startReceiptSha256": sha256_bytes(start_raw),
                **metadata,
                "supervisorSha256": stable_file_identity(__file__)[1],
                "supervisorProcessId": os.getpid(),
                **schema_provenance,
                "hostFingerprintSha256": fingerprint,
                "platform": platform_name,
                "policy": policy,
            },
        )
        _fault_checkpoint(fault_hook, "afterSupervisorStart")
    except Exception:
        writer.close()
        raise
    command = [str(pathlib.Path(executable).absolute().resolve()), *arguments]
    process = None
    containment = None
    streams = {
        "stdout": _captured_stream_state(),
        "stderr": _captured_stream_state(),
    }
    status = None
    process_exit_code = None
    tree_status = "notObserved"
    try:
        failure_stage = "createProcess"
        try:
            writer.append(
                "launchIntent",
                {
                    "commandSha256": metadata["commandSha256"],
                    "executableSha256": metadata["executableSha256"],
                },
            )
            _fault_checkpoint(fault_hook, "afterLaunchIntent")
            process = create_process(command, working_directory, environment)
            failure_stage = "containment"
            _fault_checkpoint(
                fault_hook,
                "afterProcessCreatedBeforeContainment",
                processId=process.pid,
            )
            containment = create_containment(process)
            _fault_checkpoint(
                fault_hook,
                "afterContainmentBeforeChildStarted",
                processId=process.pid,
                containment=containment.containment,
            )
        except Exception as error:  # noqa: BLE001 - type/hash are evidence
            if process is not None:
                try:
                    process.kill()
                    process.wait(timeout=5)
                except (OSError, subprocess.SubprocessError):
                    pass
                close_process_streams(process)
            writer.append(
                "launchFailure",
                {
                    "stage": failure_stage,
                    "errorType": type(error).__name__,
                    "messageSha256": sha256_bytes(str(error).encode("utf-8")),
                },
            )
            status = (
                "launchFailure"
                if failure_stage == "createProcess"
                else "treeUncertain"
            )
            failed_tree_status = (
                "notObserved"
                if failure_stage == "createProcess"
                else "uncertain"
            )
            writer.append(
                "supervisorTerminal",
                {
                    "status": status,
                    "processExitCode": None,
                    "treeStatus": failed_tree_status,
                    "bundleVerificationStatus": "notRun",
                    "stdoutCapturedBytes": 0,
                    "stdoutCapturedSha256": EMPTY_SHA256,
                    "stderrCapturedBytes": 0,
                    "stderrCapturedSha256": EMPTY_SHA256,
                },
            )
            writer.close()
            return verify_process_log(journal_path)

        writer.append(
            "childStarted",
            {
                "processId": process.pid,
                "containment": containment.containment,
            },
        )
        _fault_checkpoint(
            fault_hook,
            "afterChildStarted",
            processId=process.pid,
            containment=containment.containment,
        )
        events = queue.Queue(maxsize=8)
        readers = []
        for stream_name, stream in (
            ("stdout", process.stdout),
            ("stderr", process.stderr),
        ):
            reader = threading.Thread(
                target=stream_reader,
                args=(stream_name, stream, chunk_bytes, events),
                daemon=True,
            )
            reader.start()
            readers.append(reader)

        started = time.monotonic()
        next_heartbeat = started + heartbeat_interval_ms / 1000
        cleanup_required = False
        failure_status = None
        limit_recorded = False
        process_exit_before_eof_observed = False

        def trigger_limit(limit, observed, maximum, mapped_status):
            nonlocal cleanup_required, failure_status, limit_recorded
            if failure_status is None:
                failure_status = mapped_status
                cleanup_required = True
                if not limit_recorded:
                    writer.append(
                        "limitTriggered",
                        {
                            "limit": limit,
                            "observedValue": observed,
                            "maximumValue": maximum,
                        },
                    )
                    limit_recorded = True
                containment.terminate(tree_exit_grace_ms)

        while True:
            elapsed_ms = round((time.monotonic() - started) * 1000)
            if elapsed_ms >= wall_timeout_ms and failure_status is None:
                trigger_limit(
                    "wallTimeout",
                    elapsed_ms,
                    wall_timeout_ms,
                    "wallTimeout",
                )
            if time.monotonic() >= next_heartbeat:
                writer.append(
                    "heartbeat",
                    {
                        "childState": (
                            "running" if process.poll() is None else "exited"
                        ),
                        "stdoutCapturedBytes": streams["stdout"]["bytes"],
                        "stderrCapturedBytes": streams["stderr"]["bytes"],
                    },
                )
                next_heartbeat = (
                    time.monotonic() + heartbeat_interval_ms / 1000
                )
            try:
                event = events.get(timeout=0.02)
            except queue.Empty:
                event = None
            if event and event[0] == "chunk":
                _, stream_name, chunk = event
                state = streams[stream_name]
                remaining = maximum_stream_bytes - state["bytes"]
                retained = chunk[: max(0, remaining)]
                if retained:
                    state["digest"].update(retained)
                    state["bytes"] += len(retained)
                    writer.append(
                        "streamChunk",
                        {
                            "stream": stream_name,
                            "streamSequence": state["chunks"],
                            "byteCount": len(retained),
                            "chunkSha256": sha256_bytes(retained),
                            "cumulativeCapturedBytes": state["bytes"],
                            "dataBase64": base64.b64encode(retained).decode("ascii"),
                        },
                    )
                    state["chunks"] += 1
                    _fault_checkpoint(
                        fault_hook,
                        "afterStreamChunk",
                        stream=stream_name,
                        streamSequence=state["chunks"] - 1,
                    )
                if len(chunk) > len(retained) and failure_status is None:
                    trigger_limit(
                        f"{stream_name}Bytes",
                        state["bytes"] + len(chunk) - len(retained),
                        maximum_stream_bytes,
                        f"{stream_name}Limit",
                    )
            elif event and event[0] == "readerFailure":
                _, stream_name, error = event
                writer.append(
                    "readerFailure",
                    {
                        "stream": stream_name,
                        "errorType": type(error).__name__,
                        "messageSha256": sha256_bytes(
                            str(error).encode("utf-8")
                        ),
                    },
                )
                if failure_status is None:
                    failure_status = "readerFailure"
                    cleanup_required = True
                    containment.terminate(tree_exit_grace_ms)
            elif event and event[0] == "eof":
                _, stream_name, observed_bytes, observed_sha256 = event
                state = streams[stream_name]
                if state["eof"]:
                    raise SupervisionError("a stream emitted EOF twice")
                state["eof"] = True
                state["observedBytes"] = observed_bytes
                state["observedSha256"] = observed_sha256
                writer.append(
                    "streamEof",
                    {
                        "stream": stream_name,
                        "observedBytes": observed_bytes,
                        "observedSha256": observed_sha256,
                        "capturedBytes": state["bytes"],
                        "capturedSha256": state["digest"].hexdigest(),
                        "capturedChunkCount": state["chunks"],
                    },
                )
            process_exit_code = process.poll()
            if (
                process_exit_code is not None
                and not all(state["eof"] for state in streams.values())
                and not process_exit_before_eof_observed
            ):
                process_exit_before_eof_observed = True
                _fault_checkpoint(
                    fault_hook,
                    "afterProcessExitBeforeStreamEof",
                    processId=process.pid,
                    processExitCode=process_exit_code,
                )
            if (
                process_exit_code is not None
                and streams["stdout"]["eof"]
                and streams["stderr"]["eof"]
            ):
                break

        _fault_checkpoint(
            fault_hook,
            "afterStreamsEofBeforeChildExit",
            processId=process.pid,
            processExitCode=process_exit_code,
        )
        for reader in readers:
            reader.join(timeout=1)
            if reader.is_alive():
                failure_status = failure_status or "readerFailure"
                cleanup_required = True
        tree_status, tree_cleanup = containment.finalize(
            tree_exit_grace_ms, cleanup_required
        )
        cleanup_required = cleanup_required or tree_cleanup
        if tree_status == "uncertain":
            status = "treeUncertain"
        elif failure_status is not None:
            status = failure_status
        elif cleanup_required:
            status = "treeLeakTerminated"
        elif process_exit_code == 0:
            status = "childExitedZeroAwaitingBundleVerification"
        else:
            status = "childExitFailure"
        writer.append(
            "childExit",
            {
                "processExitCode": process_exit_code,
                "treeStatus": tree_status,
                "cleanupRequired": cleanup_required,
            },
        )
        _fault_checkpoint(
            fault_hook,
            "afterChildExit",
            processId=process.pid,
            processExitCode=process_exit_code,
            treeStatus=tree_status,
        )
        writer.append(
            "supervisorTerminal",
            {
                "status": status,
                "processExitCode": process_exit_code,
                "treeStatus": tree_status,
                "bundleVerificationStatus": "notRun",
                "stdoutCapturedBytes": streams["stdout"]["bytes"],
                "stdoutCapturedSha256": streams["stdout"]["digest"].hexdigest(),
                "stderrCapturedBytes": streams["stderr"]["bytes"],
                "stderrCapturedSha256": streams["stderr"]["digest"].hexdigest(),
            },
        )
        _fault_checkpoint(
            fault_hook,
            "afterSupervisorTerminal",
            processId=process.pid,
            processExitCode=process_exit_code,
            treeStatus=tree_status,
            terminalStatus=status,
        )
    except KeyboardInterrupt:
        if containment is not None:
            tree_status = containment.terminate(tree_exit_grace_ms)
        if process is not None:
            try:
                process_exit_code = process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                process_exit_code = process.poll()
        writer.append(
            "limitTriggered",
            {"limit": "cancellation", "observedValue": 1, "maximumValue": 0},
        )
        writer.append(
            "childExit",
            {
                "processExitCode": process_exit_code,
                "treeStatus": tree_status,
                "cleanupRequired": True,
            },
        )
        writer.append(
            "supervisorTerminal",
            {
                "status": "cancelled",
                "processExitCode": process_exit_code,
                "treeStatus": tree_status,
                "bundleVerificationStatus": "notRun",
                "stdoutCapturedBytes": streams["stdout"]["bytes"],
                "stdoutCapturedSha256": streams["stdout"]["digest"].hexdigest(),
                "stderrCapturedBytes": streams["stderr"]["bytes"],
                "stderrCapturedSha256": streams["stderr"]["digest"].hexdigest(),
            },
        )
    except Exception:
        if containment is not None:
            containment.terminate(tree_exit_grace_ms)
        elif process is not None and process.poll() is None:
            process.kill()
        if process is not None:
            close_process_streams(process)
        raise
    finally:
        if containment is not None:
            containment.close()
        writer.close()
    return verify_process_log(journal_path)


def _record_projection(record):
    return {key: value for key, value in record.items() if key != "recordSha256"}


def read_verified_process_log(path, allow_no_complete_record=False):
    path = pathlib.Path(path)
    try:
        raw = path.read_bytes()
    except OSError as error:
        raise SupervisionError(f"cannot read process journal: {path}") from error
    lines = raw.splitlines(keepends=True)
    truncated_tail = 0
    if lines and not lines[-1].endswith(b"\n"):
        truncated_tail = len(lines.pop())
    if not lines:
        if allow_no_complete_record:
            return raw, [], truncated_tail
        raise SupervisionError("process journal has no complete record")
    records = []
    previous = ZERO_SHA256
    previous_elapsed = -1
    for index, line in enumerate(lines):
        if len(line) > MAXIMUM_LINE_BYTES:
            raise SupervisionError("process journal line exceeds the hard bound")
        try:
            record = json.loads(line[:-1].decode("utf-8"))
        except (UnicodeError, json.JSONDecodeError) as error:
            raise SupervisionError("process journal contains invalid JSON") from error
        if line != canonical(record) + b"\n":
            raise SupervisionError("process journal record is not byte-canonical")
        validate_log_record(record)
        expected_hash = sha256_bytes(canonical(_record_projection(record)))
        if (
            record["sequence"] != index
            or record["previousRecordSha256"] != previous
            or record["recordSha256"] != expected_hash
            or record["monotonicElapsedMs"] < previous_elapsed
        ):
            raise SupervisionError("process journal hash/sequence/time chain failed")
        records.append(record)
        previous = expected_hash
        previous_elapsed = record["monotonicElapsedMs"]
    if records[0]["recordType"] != "supervisorStart":
        raise SupervisionError("process journal does not start with supervisorStart")
    if any(
        record["recordType"] == "supervisorStart" for record in records[1:]
    ):
        raise SupervisionError("process journal contains multiple start records")
    if any(
        record["recordType"] == "supervisorTerminal"
        for record in records[:-1]
    ):
        raise SupervisionError("process journal has records after terminal")
    if (
        records[-1]["recordType"] == "supervisorTerminal"
        and truncated_tail
    ):
        raise SupervisionError("bytes were appended after a terminal journal")
    return raw, records, truncated_tail


def verify_process_log(path):
    _, records, truncated_tail = read_verified_process_log(path)

    stream_states = {
        "stdout": _captured_stream_state(),
        "stderr": _captured_stream_state(),
    }
    start = records[0]["payload"]
    schema_provenance = verify_schema_provenance(start)
    launch_intent = 0
    child_started = 0
    child_exit = None
    limit = None
    launch_failure_stage = None
    reader_failures = set()
    terminal = None
    for record in records[1:]:
        record_type = record["recordType"]
        payload = record["payload"]
        if child_exit is not None and record_type != "supervisorTerminal":
            raise SupervisionError("a record appears after child exit")
        if launch_failure_stage is not None and record_type != "supervisorTerminal":
            raise SupervisionError("a record appears after launch failure")
        if record_type == "launchIntent":
            launch_intent += 1
            if (
                launch_intent > 1
                or child_started
                or launch_failure_stage is not None
                or payload["commandSha256"] != start["commandSha256"]
                or payload["executableSha256"] != start["executableSha256"]
            ):
                raise SupervisionError("launch intent state is inconsistent")
        elif record_type == "childStarted":
            child_started += 1
            if (
                child_started > 1
                or launch_intent != 1
                or launch_failure_stage is not None
            ):
                raise SupervisionError("child start state is inconsistent")
        elif record_type == "streamChunk":
            if child_started != 1:
                raise SupervisionError("stream chunk precedes child start")
            state = stream_states[payload["stream"]]
            if state["eof"] or payload["streamSequence"] != state["chunks"]:
                raise SupervisionError("stream chunk sequence is invalid")
            try:
                content = base64.b64decode(
                    payload["dataBase64"], validate=True
                )
            except ValueError as error:
                raise SupervisionError("stream chunk base64 is invalid") from error
            if (
                not content
                or len(content) != payload["byteCount"]
                or sha256_bytes(content) != payload["chunkSha256"]
            ):
                raise SupervisionError("stream chunk bytes/hash are invalid")
            state["digest"].update(content)
            state["bytes"] += len(content)
            state["chunks"] += 1
            if state["bytes"] != payload["cumulativeCapturedBytes"]:
                raise SupervisionError("stream cumulative byte count is invalid")
        elif record_type == "streamEof":
            if child_started != 1:
                raise SupervisionError("stream EOF precedes child start")
            state = stream_states[payload["stream"]]
            if state["eof"]:
                raise SupervisionError("stream EOF is duplicated")
            if (
                state["bytes"] != payload["capturedBytes"]
                or state["chunks"] != payload["capturedChunkCount"]
                or state["digest"].hexdigest() != payload["capturedSha256"]
                or payload["observedBytes"] < state["bytes"]
            ):
                raise SupervisionError("stream EOF summary is inconsistent")
            if (
                payload["observedBytes"] == state["bytes"]
                and payload["observedSha256"]
                != state["digest"].hexdigest()
            ):
                raise SupervisionError("complete stream observed hash is invalid")
            state["eof"] = True
            state["observedBytes"] = payload["observedBytes"]
            state["observedSha256"] = payload["observedSha256"]
        elif record_type == "limitTriggered":
            if child_started != 1:
                raise SupervisionError("process limit precedes child start")
            if limit is not None:
                raise SupervisionError("process journal contains multiple limits")
            limit = payload["limit"]
        elif record_type == "heartbeat":
            if child_started != 1:
                raise SupervisionError("heartbeat precedes child start")
        elif record_type == "readerFailure":
            if (
                child_started != 1
                or payload["stream"] in reader_failures
            ):
                raise SupervisionError("reader failure state is inconsistent")
            reader_failures.add(payload["stream"])
        elif record_type == "launchFailure":
            if (
                launch_intent != 1
                or child_started
                or launch_failure_stage is not None
            ):
                raise SupervisionError("launch failure state is inconsistent")
            launch_failure_stage = payload["stage"]
        elif record_type == "childExit":
            if child_exit is not None or child_started != 1:
                raise SupervisionError("child exit state is inconsistent")
            child_exit = payload
        elif record_type == "supervisorTerminal":
            terminal = payload

    if terminal is not None:
        if records[-1]["recordType"] != "supervisorTerminal":
            raise SupervisionError("supervisor terminal is not the final record")
        if terminal["bundleVerificationStatus"] != "notRun":
            raise SupervisionError("supervisor cannot verify a scientific bundle")
        for stream_name in ("stdout", "stderr"):
            state = stream_states[stream_name]
            if (
                terminal[f"{stream_name}CapturedBytes"] != state["bytes"]
                or terminal[f"{stream_name}CapturedSha256"]
                != state["digest"].hexdigest()
            ):
                raise SupervisionError("terminal stream inventory is inconsistent")
        terminal_status = terminal["status"]
        if terminal_status == "launchFailure":
            if (
                launch_failure_stage != "createProcess"
                or launch_intent != 1
                or child_started
                or child_exit is not None
                or terminal["treeStatus"] != "notObserved"
            ):
                raise SupervisionError("launch-failure terminal is inconsistent")
        elif terminal_status == "treeUncertain" and launch_failure_stage is not None:
            if (
                launch_failure_stage != "containment"
                or launch_intent != 1
                or child_started
                or child_exit is not None
                or terminal["treeStatus"] != "uncertain"
            ):
                raise SupervisionError("containment-failure terminal is inconsistent")
        else:
            if launch_intent != 1 or child_started != 1 or child_exit is None:
                raise SupervisionError("completed supervision lacks child exit")
            if (
                terminal["processExitCode"]
                != child_exit["processExitCode"]
                or terminal["treeStatus"] != child_exit["treeStatus"]
            ):
                raise SupervisionError("terminal child/tree state is inconsistent")
        if terminal_status == "childExitedZeroAwaitingBundleVerification" and (
            terminal["processExitCode"] != 0
            or terminal["treeStatus"] != "exitedCleanly"
        ):
            raise SupervisionError("exit-zero terminal is not clean")
        if terminal_status in (
            "childExitedZeroAwaitingBundleVerification",
            "childExitFailure",
        ) and not all(state["eof"] for state in stream_states.values()):
            raise SupervisionError("normal child terminal lacks stream EOF")
        if terminal_status == "childExitFailure" and (
            terminal["processExitCode"] in (None, 0)
        ):
            raise SupervisionError("child-exit failure lacks a non-zero code")
        if terminal_status == "readerFailure" and not reader_failures:
            raise SupervisionError("reader-failure terminal lacks its event")
        if terminal_status == "treeLeakTerminated" and (
            child_exit is None
            or not child_exit["cleanupRequired"]
            or terminal["treeStatus"] != "terminated"
        ):
            raise SupervisionError("tree-leak terminal is inconsistent")
        if terminal_status == "treeUncertain" and (
            terminal["treeStatus"] != "uncertain"
        ):
            raise SupervisionError("tree-uncertain terminal is inconsistent")
        limit_map = {
            "wallTimeout": "wallTimeout",
            "stdoutLimit": "stdoutBytes",
            "stderrLimit": "stderrBytes",
            "cancelled": "cancellation",
        }
        expected_limit = limit_map.get(terminal_status)
        if expected_limit is not None and limit != expected_limit:
            raise SupervisionError("terminal limit classification is inconsistent")

    report_streams = {}
    for stream_name, state in stream_states.items():
        report_streams[stream_name] = {
            "eof": state["eof"],
            "capturedBytes": state["bytes"],
            "capturedSha256": state["digest"].hexdigest(),
            "capturedChunkCount": state["chunks"],
        }
    report = {
        "schemaVersion": "1.0.0",
        "schemaId": REPORT_SCHEMA_ID,
        "reportType": "ridebound-wp14r-supervision-report-v1",
        "status": "validComplete" if terminal is not None else "validPartial",
        "claimBoundary": CLAIM_BOUNDARY,
        "jobId": start["jobId"],
        "attemptId": start["attemptId"],
        "attemptNumber": start["attemptNumber"],
        "commandSha256": start["commandSha256"],
        "recordCount": len(records),
        "truncatedTailBytes": truncated_tail,
        "finalRecordSha256": records[-1]["recordSha256"],
        "terminalStatus": terminal["status"] if terminal else None,
        "processExitCode": terminal["processExitCode"] if terminal else None,
        "treeStatus": terminal["treeStatus"] if terminal else "notObserved",
        "streams": report_streams,
    }
    if schema_provenance is not None:
        report.update(schema_provenance)
    validate_report(report)
    if (
        schema_provenance is not None
        and current_schema_provenance() != schema_provenance
    ):
        raise SupervisionError("schema source changed during journal verification")
    return report


def build_parser():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    run = commands.add_parser("run")
    run.add_argument("--ledger-root", type=pathlib.Path, required=True)
    run.add_argument("--job-id", required=True)
    run.add_argument("--attempt-number", type=int, required=True)
    run.add_argument(
        "--forbidden-root", type=pathlib.Path, action="append", required=True
    )
    run.add_argument("--executable", type=pathlib.Path, required=True)
    run.add_argument("--argument", action="append", default=[])
    run.add_argument("--working-directory", type=pathlib.Path, required=True)
    run.add_argument("--inherit-environment", action="append", default=[])
    run.add_argument("--wall-timeout-ms", type=int, default=60000)
    run.add_argument("--heartbeat-interval-ms", type=int, default=1000)
    run.add_argument("--maximum-stream-bytes", type=int, default=16 * 1024 * 1024)
    run.add_argument("--chunk-bytes", type=int, default=32768)
    run.add_argument("--tree-exit-grace-ms", type=int, default=2000)

    verify = commands.add_parser("verify-log")
    verify.add_argument("--process-log", type=pathlib.Path, required=True)
    return parser


def main(argv=None):
    arguments = build_parser().parse_args(argv)
    try:
        if arguments.command == "verify-log":
            report = verify_process_log(arguments.process_log)
        else:
            report = supervise_process(
                arguments.ledger_root,
                arguments.job_id,
                arguments.attempt_number,
                arguments.forbidden_root,
                arguments.executable,
                arguments.argument,
                arguments.working_directory,
                arguments.inherit_environment,
                arguments.wall_timeout_ms,
                arguments.heartbeat_interval_ms,
                arguments.maximum_stream_bytes,
                arguments.chunk_bytes,
                arguments.tree_exit_grace_ms,
            )
        sys.stdout.buffer.write(canonical(report) + b"\n")
        if report["terminalStatus"] in (
            None,
            "childExitedZeroAwaitingBundleVerification",
        ):
            return 0
        return 2
    except (OSError, SupervisionError) as error:
        print(f"WP14R_SUPERVISION_ERROR: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
