#!/usr/bin/env python3
"""Measure the exact WP14R v2 host gate without reading outcomes."""

from __future__ import annotations

import argparse
import ctypes
import datetime
import hashlib
import importlib.util
import json
import os
import pathlib
import re
import shutil
import subprocess
import sys
import time

import jsonschema

sys.dont_write_bytecode = True

SCHEMA_ID = (
    "https://ridebound.local/schemas/wp14r/v2/"
    "host-preflight-receipt.schema.json"
)
SCHEMA_RELATIVE = pathlib.Path(
    "benchmarks/schemas/wp14r/v2/host-preflight-receipt.schema.json"
)
PREFLIGHT_VERSION = "wp14r-host-preflight-v2"
CLAIM_BOUNDARY = [
    "mechanicalOnly",
    "doesNotReadScientificOutcome",
    "withinHostConditioningOnly",
    "doesNotCreateExperimentalUnits",
    "doesNotSupersedeWp14V1",
]
POWER_SCHEME_PATTERN = re.compile(
    r"([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-"
    r"[0-9a-fA-F]{4}-[0-9a-fA-F]{12})"
)


class HostPreflightError(RuntimeError):
    """A fail-closed host preflight construction error."""


def canonical(document):
    return json.dumps(
        document,
        sort_keys=True,
        separators=(",", ":"),
        ensure_ascii=False,
    ).encode("utf-8")


def sha256_bytes(content):
    return hashlib.sha256(content).hexdigest()


def sha256_file(path):
    digest = hashlib.sha256()
    with pathlib.Path(path).open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def utc_now():
    return (
        datetime.datetime.now(datetime.timezone.utc)
        .isoformat(timespec="microseconds")
        .replace("+00:00", "Z")
    )


def repository_root():
    return pathlib.Path(__file__).resolve().parents[2]


def load_local_module(name, filename):
    path = pathlib.Path(__file__).resolve().with_name(filename)
    specification = importlib.util.spec_from_file_location(name, path)
    if specification is None or specification.loader is None:
        raise HostPreflightError(f"cannot load dependency: {path}")
    module = importlib.util.module_from_spec(specification)
    specification.loader.exec_module(module)
    return module


def validate_receipt(receipt):
    schema = json.loads(
        (repository_root() / SCHEMA_RELATIVE).read_text(encoding="utf-8")
    )
    try:
        jsonschema.Draft202012Validator(
            schema,
            format_checker=jsonschema.FormatChecker(),
        ).validate(receipt)
    except jsonschema.ValidationError as error:
        raise HostPreflightError(
            f"host preflight schema failed: {error.message}"
        ) from error


class _SystemPowerStatus(ctypes.Structure):
    _fields_ = [
        ("acLineStatus", ctypes.c_ubyte),
        ("batteryFlag", ctypes.c_ubyte),
        ("batteryLifePercent", ctypes.c_ubyte),
        ("systemStatusFlag", ctypes.c_ubyte),
        ("batteryLifeTime", ctypes.c_uint32),
        ("batteryFullLifeTime", ctypes.c_uint32),
    ]


class _FileTime(ctypes.Structure):
    _fields_ = [
        ("low", ctypes.c_uint32),
        ("high", ctypes.c_uint32),
    ]


class _MemoryStatusEx(ctypes.Structure):
    _fields_ = [
        ("length", ctypes.c_uint32),
        ("memoryLoad", ctypes.c_uint32),
        ("totalPhysical", ctypes.c_uint64),
        ("availablePhysical", ctypes.c_uint64),
        ("totalPageFile", ctypes.c_uint64),
        ("availablePageFile", ctypes.c_uint64),
        ("totalVirtual", ctypes.c_uint64),
        ("availableVirtual", ctypes.c_uint64),
        ("availableExtendedVirtual", ctypes.c_uint64),
    ]


def _kernel32():
    if os.name != "nt":
        raise HostPreflightError("WP14R v2 host gate requires Windows")
    return ctypes.WinDLL("kernel32", use_last_error=True)


def read_ac_line_status():
    status = _SystemPowerStatus()
    kernel32 = _kernel32()
    kernel32.GetSystemPowerStatus.argtypes = [
        ctypes.POINTER(_SystemPowerStatus)
    ]
    kernel32.GetSystemPowerStatus.restype = ctypes.c_int
    if not kernel32.GetSystemPowerStatus(ctypes.byref(status)):
        raise HostPreflightError("GetSystemPowerStatus failed")
    return {0: "offline", 1: "online", 255: "unknown"}.get(
        status.acLineStatus,
        "unknown",
    )


def read_active_power_scheme():
    completed = subprocess.run(
        ["powercfg", "/getactivescheme"],
        check=False,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        timeout=10,
    )
    if completed.returncode != 0:
        raise HostPreflightError("powercfg could not read the active scheme")
    match = POWER_SCHEME_PATTERN.search(completed.stdout or "")
    if match is None:
        raise HostPreflightError("active power-scheme GUID was not found")
    return match.group(1).lower()


def _file_time_value(value):
    return (value.high << 32) | value.low


def read_system_times():
    idle = _FileTime()
    kernel = _FileTime()
    user = _FileTime()
    kernel32 = _kernel32()
    kernel32.GetSystemTimes.argtypes = [
        ctypes.POINTER(_FileTime),
        ctypes.POINTER(_FileTime),
        ctypes.POINTER(_FileTime),
    ]
    kernel32.GetSystemTimes.restype = ctypes.c_int
    if not kernel32.GetSystemTimes(
        ctypes.byref(idle),
        ctypes.byref(kernel),
        ctypes.byref(user),
    ):
        raise HostPreflightError("GetSystemTimes failed")
    return (
        _file_time_value(idle),
        _file_time_value(kernel),
        _file_time_value(user),
    )


def read_available_memory_bytes():
    status = _MemoryStatusEx()
    status.length = ctypes.sizeof(_MemoryStatusEx)
    kernel32 = _kernel32()
    kernel32.GlobalMemoryStatusEx.argtypes = [
        ctypes.POINTER(_MemoryStatusEx)
    ]
    kernel32.GlobalMemoryStatusEx.restype = ctypes.c_int
    if not kernel32.GlobalMemoryStatusEx(ctypes.byref(status)):
        raise HostPreflightError("GlobalMemoryStatusEx failed")
    return int(status.availablePhysical)


def cpu_busy_percent(previous, current):
    idle_delta = current[0] - previous[0]
    total_delta = (
        current[1] - previous[1] + current[2] - previous[2]
    )
    if idle_delta < 0 or total_delta <= 0 or idle_delta > total_delta:
        raise HostPreflightError("system CPU counters moved inconsistently")
    return round(100 * (total_delta - idle_delta) / total_delta, 3)


def sample_cpu_busy(
    sample_count,
    interval_ms,
    times_reader=read_system_times,
    sleeper=time.sleep,
):
    previous = times_reader()
    samples = []
    for _ in range(sample_count):
        sleeper(interval_ms / 1000)
        current = times_reader()
        samples.append(cpu_busy_percent(previous, current))
        previous = current
    return samples


def disk_probe_path(path):
    candidate = pathlib.Path(path).absolute()
    while not candidate.exists():
        parent = candidate.parent
        if parent == candidate:
            raise HostPreflightError("no existing ancestor for disk probe")
        candidate = parent
    if not candidate.is_dir():
        candidate = candidate.parent
    return candidate


def evaluate(
    policy,
    platform_name,
    fingerprint,
    ac_line_status,
    power_scheme_guid,
    cpu_samples,
    available_memory_bytes,
    free_disk_bytes,
):
    if len(cpu_samples) != policy["sampleCount"]:
        raise HostPreflightError("CPU sample count differs from the freeze")
    rounded = [round(float(value), 3) for value in cpu_samples]
    if any(value < 0 or value > 100 for value in rounded):
        raise HostPreflightError("CPU sample is outside 0..100 percent")
    mean_cpu = round(sum(rounded) / len(rounded), 3)
    maximum_cpu = max(rounded)
    failures = []
    checks = (
        (
            platform_name != policy["requiredPlatform"],
            "PLATFORM_MISMATCH",
        ),
        (
            fingerprint != policy["requiredHostFingerprintSha256"],
            "HOST_FINGERPRINT_MISMATCH",
        ),
        (
            ac_line_status != policy["requiredAcLineStatus"],
            "POWER_SOURCE_NOT_AC",
        ),
        (
            power_scheme_guid != policy["requiredPowerSchemeGuid"],
            "POWER_SCHEME_MISMATCH",
        ),
        (
            mean_cpu > policy["maximumMeanCpuBusyPercent"],
            "CPU_MEAN_ABOVE_LIMIT",
        ),
        (
            maximum_cpu > policy["maximumSingleCpuBusyPercent"],
            "CPU_SAMPLE_ABOVE_LIMIT",
        ),
        (
            available_memory_bytes
            < policy["minimumAvailableMemoryBytes"],
            "MEMORY_BELOW_MINIMUM",
        ),
        (
            free_disk_bytes < policy["minimumFreeDiskBytes"],
            "DISK_BELOW_MINIMUM",
        ),
    )
    for failed, code in checks:
        if failed:
            failures.append(code)
    return {
        "status": "pass" if not failures else "fail",
        "failureCodes": failures,
        "cpuSamples": rounded,
        "meanCpu": mean_cpu,
        "maximumCpu": maximum_cpu,
    }


def collect_preflight(
    freeze_receipt,
    freeze_receipt_sha256,
    job_id,
    prospective_attempt_number,
    control_root,
    observed_utc=None,
    platform_and_fingerprint=None,
    ac_line_status_reader=read_ac_line_status,
    power_scheme_reader=read_active_power_scheme,
    cpu_sampler=sample_cpu_busy,
    memory_reader=read_available_memory_bytes,
    disk_reader=None,
):
    policy = freeze_receipt["protocol"]["hostPolicy"]
    if platform_and_fingerprint is None:
        supervisor = load_local_module(
            "wp14r_supervisor_for_host_preflight",
            "wp14r_supervised_process.py",
        )
        platform_and_fingerprint = supervisor.host_fingerprint()
    platform_name, fingerprint = platform_and_fingerprint
    ac_line_status = ac_line_status_reader()
    power_scheme_guid = power_scheme_reader().lower()
    cpu_samples = cpu_sampler(
        policy["sampleCount"],
        policy["sampleIntervalMs"],
    )
    available_memory_bytes = int(memory_reader())
    if disk_reader is None:
        free_disk_bytes = shutil.disk_usage(
            disk_probe_path(control_root)
        ).free
    else:
        free_disk_bytes = int(disk_reader(control_root))
    decision = evaluate(
        policy,
        platform_name,
        fingerprint,
        ac_line_status,
        power_scheme_guid,
        cpu_samples,
        available_memory_bytes,
        free_disk_bytes,
    )
    receipt = {
        "schemaVersion": "2.0.0",
        "schemaId": SCHEMA_ID,
        "recordType": "ridebound-wp14r-host-preflight-v2",
        "preflightVersion": PREFLIGHT_VERSION,
        "status": decision["status"],
        "claimBoundary": CLAIM_BOUNDARY,
        "observedUtc": observed_utc or utc_now(),
        "freezeId": freeze_receipt["freezeId"],
        "freezeReceiptSha256": freeze_receipt_sha256,
        "jobId": job_id,
        "prospectiveAttemptNumber": prospective_attempt_number,
        "host": {
            "platform": platform_name,
            "hostFingerprintSha256": fingerprint,
        },
        "power": {
            "acLineStatus": ac_line_status,
            "activeSchemeGuid": power_scheme_guid,
        },
        "quiescence": {
            "sampleCount": len(decision["cpuSamples"]),
            "sampleIntervalMs": policy["sampleIntervalMs"],
            "cpuBusyPercentSamples": decision["cpuSamples"],
            "meanCpuBusyPercent": decision["meanCpu"],
            "maximumCpuBusyPercent": decision["maximumCpu"],
            "availableMemoryBytes": available_memory_bytes,
            "freeDiskBytes": free_disk_bytes,
        },
        "failureCodes": decision["failureCodes"],
        "outcomeFieldsRead": False,
        "preflightToolSha256": sha256_file(__file__),
    }
    validate_receipt(receipt)
    return receipt


def write_exclusive(path, content):
    path = pathlib.Path(path)
    supervisor = load_local_module(
        "wp14r_supervisor_for_preflight_publication",
        "wp14r_supervised_process.py",
    )
    supervisor.reject_link_ancestry(path.parent, "preflight output parent")
    path.parent.mkdir(parents=True, exist_ok=True)
    supervisor.reject_link_ancestry(path.parent, "preflight output parent")
    flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL
    flags |= getattr(os, "O_BINARY", 0)
    descriptor = os.open(path, flags, 0o600)
    try:
        with os.fdopen(descriptor, "wb", closefd=False) as stream:
            stream.write(content)
            stream.flush()
            os.fsync(stream.fileno())
    finally:
        os.close(descriptor)


def build_parser():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--freeze", type=pathlib.Path, required=True)
    parser.add_argument("--job-id", required=True)
    parser.add_argument("--attempt-number", type=int, required=True)
    parser.add_argument("--control-root", type=pathlib.Path, required=True)
    parser.add_argument("--output", type=pathlib.Path)
    return parser


def main(argv=None):
    arguments = build_parser().parse_args(argv)
    try:
        freeze_module = load_local_module(
            "wp14r_freeze_v2_for_host_preflight",
            "wp14r_freeze_v2.py",
        )
        freeze = freeze_module.verify_receipt(arguments.freeze)
        raw = pathlib.Path(arguments.freeze).read_bytes()
        receipt = collect_preflight(
            freeze,
            sha256_bytes(raw),
            arguments.job_id,
            arguments.attempt_number,
            arguments.control_root,
        )
        payload = canonical(receipt) + b"\n"
        if arguments.output is not None:
            write_exclusive(arguments.output, payload)
        sys.stdout.buffer.write(payload)
        return 0 if receipt["status"] == "pass" else 2
    except (OSError, HostPreflightError) as error:
        print(f"WP14R_HOST_PREFLIGHT_ERROR: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
