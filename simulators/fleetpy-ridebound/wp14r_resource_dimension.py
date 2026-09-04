#!/usr/bin/env python3
"""Dimension WP14R execution mechanics without reading scientific outcomes."""

import argparse
import datetime
import hashlib
import importlib.util
import json
import os
import pathlib
import platform
import statistics
import subprocess
import sys
import time

import jsonschema
import psutil


SCHEMA_ID = (
    "https://ridebound.local/schemas/wp14r/v1/"
    "mechanics-dimension-report.schema.json"
)
ARTIFACT_SCHEMA_VERSION = "1.1.0"
REPORT_TYPE = "ridebound-wp14r-mechanics-dimension-report-v2"
CORPUS_ID = "wp14r-mechanics-corpus-v2"
POLICY_ID = "wp14r-mechanics-dimension-policy-v1"
FREEZE_ID = "wp14r-mechanics-dimension-v1"
CLAIM_BOUNDARY = [
    "mechanicalOnly",
    "doesNotReadScientificOutcome",
    "descriptiveWithinHostOnly",
    "attemptsAreNotReplicates",
    "noConfidenceOrPopulationClaim",
    "doesNotSupersedeWp14V1",
]
CASE_IDS = [
    "silentExit",
    "binaryStreams",
    "exactCapBoundary",
    "heartbeatIdle",
    "nonzeroExit",
    "lingeringGrandchild",
    "partialRecovery",
    "largeJournal",
]
POLICY = {
    "policyId": POLICY_ID,
    "pilotRepetitionsPerCell": 1,
    "measuredRepetitionsPerCell": 5,
    "processPollIntervalMs": 5,
    "maximumVerifierPeakRssBytes": 256 * 1024 * 1024,
    "summaryMethod": "lowerIntegerMedianMinimumMaximum",
    "fsyncModel": "oneFsyncPerJournalRecord",
    "pilotExcludedFromSummaries": True,
    "retainEverySample": True,
    "outlierDeletionAllowed": False,
}


class DimensionError(RuntimeError):
    """Raised when mechanics dimensioning cannot preserve its contract."""


def canonical(document):
    return json.dumps(
        document,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
        allow_nan=False,
    ).encode("utf-8")


def sha256_bytes(content):
    return hashlib.sha256(content).hexdigest()


def sha256_file(path):
    digest = hashlib.sha256()
    with pathlib.Path(path).open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def utc_now():
    return (
        datetime.datetime.now(datetime.timezone.utc)
        .isoformat(timespec="microseconds")
        .replace("+00:00", "Z")
    )


def repository_root():
    return pathlib.Path(__file__).resolve().parents[2]


def load_module(name, filename):
    path = pathlib.Path(__file__).resolve().with_name(filename)
    specification = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(specification)
    specification.loader.exec_module(module)
    return module


def load_dependencies():
    return {
        "ledger": load_module(
            "wp14r_dimension_ledger", "wp14r_attempt_ledger.py"
        ),
        "supervisor": load_module(
            "wp14r_dimension_supervisor", "wp14r_supervised_process.py"
        ),
        "recovery": load_module(
            "wp14r_dimension_recovery", "wp14r_stale_open_recovery.py"
        ),
        "faults": load_module(
            "wp14r_dimension_faults", "wp14r_fault_injection.py"
        ),
    }


def load_schema():
    path = (
        repository_root()
        / "benchmarks/schemas/wp14r/v1"
        / "mechanics-dimension-report.schema.json"
    )
    return json.loads(path.read_text(encoding="utf-8"))


def validate_report(report):
    try:
        jsonschema.Draft202012Validator(load_schema()).validate(report)
    except jsonschema.ValidationError as error:
        raise DimensionError(
            f"mechanics dimension report schema failed: {error.message}"
        ) from error


def fixed_corpus():
    common = {
        "wallTimeoutMs": 10000,
        "heartbeatIntervalMs": 100,
        "maximumStreamBytes": 1024 * 1024,
        "chunkBytes": 32768,
        "treeExitGraceMs": 1000,
        "payloadBytes": 0,
        "stderrBytes": 0,
        "idleMs": 0,
    }

    def case(case_id, expected, **changes):
        document = dict(common)
        document.update(changes)
        document.update(
            {
                "caseId": case_id,
                "operation": (
                    "recover" if case_id == "partialRecovery" else "supervise"
                ),
                "expectedTerminalStatus": expected,
            }
        )
        return document

    return [
        case("silentExit", "childExitedZeroAwaitingBundleVerification"),
        case(
            "binaryStreams",
            "childExitedZeroAwaitingBundleVerification",
            payloadBytes=4096,
            stderrBytes=2048,
            chunkBytes=512,
        ),
        case(
            "exactCapBoundary",
            "childExitedZeroAwaitingBundleVerification",
            payloadBytes=65536,
            maximumStreamBytes=65536,
            chunkBytes=4096,
        ),
        case(
            "heartbeatIdle",
            "childExitedZeroAwaitingBundleVerification",
            idleMs=300,
            heartbeatIntervalMs=50,
        ),
        case("nonzeroExit", "childExitFailure"),
        case(
            "lingeringGrandchild",
            "treeLeakTerminated",
            idleMs=30000,
            treeExitGraceMs=100,
        ),
        case(
            "partialRecovery",
            "launcherRecoveredOrphanedStart",
            idleMs=30000,
        ),
        case(
            "largeJournal",
            "childExitedZeroAwaitingBundleVerification",
            payloadBytes=8 * 1024 * 1024,
            maximumStreamBytes=8 * 1024 * 1024,
            chunkBytes=32768,
            wallTimeoutMs=180000,
        ),
    ]


def corpus_document():
    cases = fixed_corpus()
    return {
        "corpusId": CORPUS_ID,
        "corpusSha256": sha256_bytes(
            canonical({"corpusId": CORPUS_ID, "cases": cases})
        ),
        "caseIds": [case["caseId"] for case in cases],
    }


def policy_document():
    policy = dict(POLICY)
    policy["policySha256"] = sha256_bytes(canonical(POLICY))
    return policy


def toolchain_document(dependencies, python_executable):
    directory = pathlib.Path(__file__).resolve().parent
    files = {
        "dimensionerSha256": pathlib.Path(__file__).resolve(),
        "supervisorSha256": directory / "wp14r_supervised_process.py",
        "ledgerSha256": directory / "wp14r_attempt_ledger.py",
        "recoverySha256": directory / "wp14r_stale_open_recovery.py",
        "faultInjectionSha256": directory / "wp14r_fault_injection.py",
        "pythonExecutableSha256": pathlib.Path(python_executable).resolve(),
    }
    result = {key: sha256_file(path) for key, path in files.items()}
    result["wp14rSchemaTreeSha256"] = schema_tree_sha256()
    result["pythonVersion"] = platform.python_version()
    result["psutilVersion"] = psutil.__version__
    expected = {
        "supervisorSha256": dependencies["supervisor"].stable_file_identity(
            files["supervisorSha256"]
        )[1],
        "ledgerSha256": dependencies["ledger"].sha256_file(
            files["ledgerSha256"]
        ),
    }
    if any(result[key] != value for key, value in expected.items()):
        raise DimensionError("loaded WP14R dependency differs from source bytes")
    return result


def host_session_document(host_session_id):
    system = platform.system()
    normalized = system if system in ("Windows", "Linux", "Darwin") else "Other"
    identity = {
        "platform": normalized,
        "release": platform.release(),
        "machine": platform.machine(),
        "nodeSha256": sha256_bytes(platform.node().encode("utf-8")),
        "logicalCpuCount": os.cpu_count() or 1,
    }
    return {
        "hostSessionId": host_session_id,
        "hostFingerprintSha256": sha256_bytes(canonical(identity)),
        "platform": identity["platform"],
        "release": identity["release"] or "unknown",
        "machine": identity["machine"] or "unknown",
        "logicalCpuCount": identity["logicalCpuCount"],
    }


def path_hash(path):
    normalized = os.path.normcase(str(pathlib.Path(path).resolve()))
    return sha256_bytes(normalized.encode("utf-8"))


def schema_tree_sha256():
    root = repository_root() / "benchmarks/schemas/wp14r/v1"
    inventory = []
    for path in sorted(root.glob("*.schema.json"), key=lambda item: item.name):
        inventory.append(
            {
                "path": path.name,
                "bytes": path.stat().st_size,
                "sha256": sha256_file(path),
            }
        )
    if not inventory:
        raise DimensionError("WP14R schema tree is empty")
    return sha256_bytes(canonical(inventory))


def paths_overlap(first, second):
    first = pathlib.Path(first).resolve()
    second = pathlib.Path(second).resolve()
    try:
        first.relative_to(second)
        return True
    except ValueError:
        pass
    try:
        second.relative_to(first)
        return True
    except ValueError:
        return False


def validate_roots(output_root, forbidden_roots, ledger):
    output_root = pathlib.Path(output_root).absolute().resolve()
    if output_root.exists():
        raise DimensionError("dimension output root already exists")
    if paths_overlap(output_root, repository_root()):
        raise DimensionError("dimension output root must be outside the repository")
    normalized = [pathlib.Path(path).absolute().resolve() for path in forbidden_roots]
    ledger.validate_ledger_root(output_root / "ledger", normalized)
    if any(paths_overlap(output_root, path) for path in normalized):
        raise DimensionError("dimension output root overlaps a forbidden raw root")
    return output_root, normalized


def write_pattern(descriptor, byte_count, salt):
    block = bytes((index + salt) % 256 for index in range(65536))
    remaining = byte_count
    while remaining:
        chunk = block[: min(remaining, len(block))]
        written = os.write(descriptor, chunk)
        if written <= 0:
            raise DimensionError("fake child could not write its fixed payload")
        remaining -= written


def run_fake_child(case_id, payload_bytes, stderr_bytes, idle_ms):
    if case_id == "silentExit":
        return 0
    if case_id == "binaryStreams":
        write_pattern(sys.stdout.fileno(), payload_bytes, 17)
        write_pattern(sys.stderr.fileno(), stderr_bytes, 193)
        return 0
    if case_id in ("exactCapBoundary", "largeJournal"):
        write_pattern(sys.stdout.fileno(), payload_bytes, 71)
        return 0
    if case_id == "heartbeatIdle":
        time.sleep(idle_ms / 1000)
        return 0
    if case_id == "nonzeroExit":
        return 7
    if case_id == "lingeringGrandchild":
        subprocess.Popen(
            [
                sys.executable,
                "-B",
                str(pathlib.Path(__file__).resolve()),
                "fake-child",
                "--case-id",
                "sleepHelper",
                "--idle-ms",
                str(idle_ms),
            ],
            stdin=subprocess.DEVNULL,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        return 0
    if case_id == "sleepHelper":
        time.sleep(idle_ms / 1000)
        return 0
    if case_id == "partialRecovery":
        time.sleep(idle_ms / 1000)
        return 0
    raise DimensionError("fake child case is outside the fixed corpus")


def child_arguments(case):
    return [
        "-B",
        str(pathlib.Path(__file__).resolve()),
        "fake-child",
        "--case-id",
        case["caseId"],
        "--payload-bytes",
        str(case["payloadBytes"]),
        "--stderr-bytes",
        str(case["stderrBytes"]),
        "--idle-ms",
        str(case["idleMs"]),
    ]


def sample_identity(case, phase, repetition):
    sample_id = f"{phase}-{case['caseId']}-{repetition:02d}"
    return sample_id, f"wp14r-dim-{sample_id}"


def empty_process_measurement():
    return {
        "processId": None,
        "returnCode": None,
        "wallElapsedNs": None,
        "processCpuNs": None,
        "peakRssBytes": None,
        "stdoutBytes": None,
        "stderrBytes": None,
    }


def empty_journal_measurement():
    return {
        "bytes": None,
        "records": None,
        "fsyncCount": None,
        "supervisedChildProcessId": None,
        "cleanupLatencyNs": None,
        "stdout": {"observedBytes": None, "retainedBytes": None},
        "stderr": {"observedBytes": None, "retainedBytes": None},
    }


def sample_shell(case, phase, repetition):
    sample_id, job_id = sample_identity(case, phase, repetition)
    return {
        "sampleId": sample_id,
        "caseId": case["caseId"],
        "phase": phase,
        "repetition": repetition,
        "operation": case["operation"],
        "measurementStatus": "failed",
        "failureType": None,
        "failureMessageSha256": None,
        "startedUtc": utc_now(),
        "jobId": job_id,
        "expectedTerminalStatus": case["expectedTerminalStatus"],
        "terminalStatus": None,
        "treeStatus": None,
        "recoverySourceSupervisorProcessId": None,
        "launcher": empty_process_measurement(),
        "verifier": empty_process_measurement(),
        "journal": empty_journal_measurement(),
        "retained": True,
        "outcomeFieldsRead": False,
    }


def monitor_process(command, stdout_path, stderr_path, environment):
    stdout_path = pathlib.Path(stdout_path)
    stderr_path = pathlib.Path(stderr_path)
    started = time.perf_counter_ns()
    peak_rss = 0
    cpu_ns = 0
    with stdout_path.open("xb") as stdout, stderr_path.open("xb") as stderr:
        process = subprocess.Popen(
            command,
            stdin=subprocess.DEVNULL,
            stdout=stdout,
            stderr=stderr,
            env=environment,
        )
        observed = psutil.Process(process.pid)
        while True:
            try:
                memory = observed.memory_info().rss
                times = observed.cpu_times()
                peak_rss = max(peak_rss, memory)
                cpu_ns = max(cpu_ns, round((times.user + times.system) * 1e9))
            except (psutil.NoSuchProcess, psutil.AccessDenied):
                pass
            return_code = process.poll()
            if return_code is not None:
                break
            time.sleep(POLICY["processPollIntervalMs"] / 1000)
        process.wait(timeout=5)
        stdout.flush()
        stderr.flush()
        os.fsync(stdout.fileno())
        os.fsync(stderr.fileno())
    return {
        "processId": process.pid,
        "returnCode": return_code,
        "wallElapsedNs": time.perf_counter_ns() - started,
        "processCpuNs": cpu_ns,
        "peakRssBytes": peak_rss,
        "stdoutBytes": stdout_path.stat().st_size,
        "stderrBytes": stderr_path.stat().st_size,
    }


def read_json_result(path, label):
    raw = pathlib.Path(path).read_bytes()
    try:
        document = json.loads(raw.decode("utf-8"))
    except (UnicodeError, json.JSONDecodeError) as error:
        raise DimensionError(f"{label} did not emit one JSON document") from error
    if raw not in (canonical(document), canonical(document) + b"\n"):
        raise DimensionError(f"{label} JSON is not byte-canonical")
    return document


def journal_telemetry(path):
    path = pathlib.Path(path)
    records = 0
    child_id = None
    stream_eof_ms = []
    child_exit_ms = None
    streams = {
        "stdout": {"observedBytes": None, "retainedBytes": None},
        "stderr": {"observedBytes": None, "retainedBytes": None},
    }
    with path.open("rb") as journal:
        for line in journal:
            if not line.endswith(b"\n"):
                break
            record = json.loads(line.decode("utf-8"))
            records += 1
            payload = record["payload"]
            if record["recordType"] == "childStarted":
                child_id = payload["processId"]
            elif record["recordType"] == "streamEof":
                streams[payload["stream"]] = {
                    "observedBytes": payload["observedBytes"],
                    "retainedBytes": payload["capturedBytes"],
                }
                stream_eof_ms.append(record["monotonicElapsedMs"])
            elif record["recordType"] == "childExit":
                child_exit_ms = record["monotonicElapsedMs"]
    cleanup = None
    if child_exit_ms is not None and stream_eof_ms:
        cleanup = max(0, child_exit_ms - max(stream_eof_ms)) * 1_000_000
    return {
        "bytes": path.stat().st_size,
        "records": records,
        "fsyncCount": records,
        "supervisedChildProcessId": child_id,
        "cleanupLatencyNs": cleanup,
        "stdout": streams["stdout"],
        "stderr": streams["stderr"],
    }


def prepare_attempt(context, case, sample):
    arguments = child_arguments(case)
    metadata, _ = context["supervisor"].build_command_binding(
        context["pythonExecutable"],
        arguments,
        context["workingDirectory"],
        context["inheritedEnvironmentNames"],
        os.environ,
    )
    job_binding = sha256_bytes(
        canonical(
            {
                "sampleId": sample["sampleId"],
                "case": case,
                "commandSha256": metadata["commandSha256"],
            }
        )
    )
    context["ledger"].begin_attempt(
        context["ledgerRoot"],
        sample["jobId"],
        FREEZE_ID,
        context["corpus"]["corpusSha256"],
        job_binding,
        metadata["commandSha256"],
        context["forbiddenRoots"],
        sample["startedUtc"],
    )
    return arguments


def append_common_supervisor_arguments(command, context, case, sample, arguments):
    command.extend(
        [
            "run",
            "--ledger-root",
            str(context["ledgerRoot"]),
            "--job-id",
            sample["jobId"],
            "--attempt-number",
            "1",
            "--executable",
            str(context["pythonExecutable"]),
            "--working-directory",
            str(context["workingDirectory"]),
            "--wall-timeout-ms",
            str(case["wallTimeoutMs"]),
            "--heartbeat-interval-ms",
            str(case["heartbeatIntervalMs"]),
            "--maximum-stream-bytes",
            str(case["maximumStreamBytes"]),
            "--chunk-bytes",
            str(case["chunkBytes"]),
            "--tree-exit-grace-ms",
            str(case["treeExitGraceMs"]),
        ]
    )
    for path in context["forbiddenRoots"]:
        command.extend(["--forbidden-root", str(path)])
    for name in context["inheritedEnvironmentNames"]:
        command.extend(["--inherit-environment", name])
    for argument in arguments:
        command.append(f"--argument={argument}")


def verifier_measurement(context, sample_directory, process_log):
    stdout = sample_directory / "verifier.stdout"
    stderr = sample_directory / "verifier.stderr"
    command = [
        str(context["pythonExecutable"]),
        "-B",
        str(context["supervisorPath"]),
        "verify-log",
        "--process-log",
        str(process_log),
    ]
    measurement = monitor_process(command, stdout, stderr, os.environ.copy())
    if measurement["returnCode"] not in (0, 2):
        raise DimensionError("verifier process failed before a typed report")
    return measurement, read_json_result(stdout, "verifier")


def run_supervision_sample(context, case, sample, sample_directory):
    arguments = prepare_attempt(context, case, sample)
    command = [
        str(context["pythonExecutable"]),
        "-B",
        str(context["supervisorPath"]),
    ]
    append_common_supervisor_arguments(
        command, context, case, sample, arguments
    )
    stdout = sample_directory / "launcher.stdout"
    stderr = sample_directory / "launcher.stderr"
    sample["launcher"] = monitor_process(
        command, stdout, stderr, os.environ.copy()
    )
    if sample["launcher"]["returnCode"] not in (0, 2):
        raise DimensionError("supervisor failed before a typed report")
    launcher_report = read_json_result(stdout, "supervisor")
    process_log = (
        context["ledgerRoot"]
        / sample["jobId"]
        / "attempt-01/process.log"
    )
    sample["verifier"], verifier_report = verifier_measurement(
        context, sample_directory, process_log
    )
    if launcher_report != verifier_report:
        raise DimensionError("launcher and independent verifier reports differ")
    sample["terminalStatus"] = launcher_report["terminalStatus"]
    sample["treeStatus"] = launcher_report["treeStatus"]
    sample["journal"] = journal_telemetry(process_log)


def recovery_fault_config(context, case, sample, arguments):
    return {
        "ledgerRoot": str(context["ledgerRoot"]),
        "jobId": sample["jobId"],
        "attemptNumber": 1,
        "forbiddenRoots": [str(path) for path in context["forbiddenRoots"]],
        "executable": str(context["pythonExecutable"]),
        "arguments": arguments,
        "workingDirectory": str(context["workingDirectory"]),
        "inheritedEnvironmentNames": context["inheritedEnvironmentNames"],
        "wallTimeoutMs": case["wallTimeoutMs"],
        "heartbeatIntervalMs": case["heartbeatIntervalMs"],
        "maximumStreamBytes": case["maximumStreamBytes"],
        "chunkBytes": case["chunkBytes"],
        "treeExitGraceMs": case["treeExitGraceMs"],
        "faultPoint": "afterSupervisorStart",
    }


def run_recovery_sample(context, case, sample, sample_directory):
    arguments = prepare_attempt(context, case, sample)
    control = context["controlRoot"] / sample["sampleId"]
    killed = context["faults"].run_hard_kill(
        recovery_fault_config(context, case, sample, arguments),
        control,
        timeout_seconds=10,
    )
    sample["recoverySourceSupervisorProcessId"] = killed["workerProcessId"]
    command = [
        str(context["pythonExecutable"]),
        "-B",
        str(context["recoveryPath"]),
        "--ledger-root",
        str(context["ledgerRoot"]),
        "--job-id",
        sample["jobId"],
        "--attempt-number",
        "1",
        "--expected-supervisor-pid",
        str(killed["workerProcessId"]),
        "--tree-probe-grace-ms",
        "2000",
        "--observed-utc",
        utc_now(),
    ]
    for path in context["forbiddenRoots"]:
        command.extend(["--forbidden-root", str(path)])
    stdout = sample_directory / "launcher.stdout"
    stderr = sample_directory / "launcher.stderr"
    sample["launcher"] = monitor_process(
        command, stdout, stderr, os.environ.copy()
    )
    if sample["launcher"]["returnCode"] != 0:
        raise DimensionError("recovery failed before terminal publication")
    result = read_json_result(stdout, "recovery")
    receipt = result["recoveryReceipt"]
    terminal = result["attemptTerminal"]
    process_log = (
        context["ledgerRoot"]
        / sample["jobId"]
        / "attempt-01/process.log"
    )
    sample["verifier"], verifier_report = verifier_measurement(
        context, sample_directory, process_log
    )
    if verifier_report["status"] != "validPartial":
        raise DimensionError("recovery corpus did not retain a partial journal")
    sample["terminalStatus"] = terminal["exitClassification"]
    sample["treeStatus"] = receipt["treeStatus"]
    sample["journal"] = journal_telemetry(process_log)


def run_sample(context, case, phase, repetition):
    sample = sample_shell(case, phase, repetition)
    sample_directory = context["sampleRoot"] / sample["sampleId"]
    sample_directory.mkdir(parents=False, exist_ok=False)
    try:
        if case["operation"] == "recover":
            run_recovery_sample(context, case, sample, sample_directory)
        else:
            run_supervision_sample(context, case, sample, sample_directory)
        if sample["terminalStatus"] != sample["expectedTerminalStatus"]:
            raise DimensionError("sample terminal status differs from fixed corpus")
        sample["measurementStatus"] = "passed"
    except Exception as error:  # noqa: BLE001 - failure is retained and hashed
        sample["failureType"] = type(error).__name__
        sample["failureMessageSha256"] = sha256_bytes(
            str(error).encode("utf-8")
        )
    return sample


def integer_axis(values):
    values = sorted(value for value in values if value is not None)
    if not values:
        return None
    return {
        "median": statistics.median_low(values),
        "minimum": values[0],
        "maximum": values[-1],
    }


def nested_value(sample, *keys):
    value = sample
    for key in keys:
        value = value[key]
    return value


SUMMARY_AXES = {
    "launcherWallElapsedNs": ("launcher", "wallElapsedNs"),
    "launcherProcessCpuNs": ("launcher", "processCpuNs"),
    "launcherPeakRssBytes": ("launcher", "peakRssBytes"),
    "verifierWallElapsedNs": ("verifier", "wallElapsedNs"),
    "verifierProcessCpuNs": ("verifier", "processCpuNs"),
    "verifierPeakRssBytes": ("verifier", "peakRssBytes"),
    "journalBytes": ("journal", "bytes"),
    "journalRecords": ("journal", "records"),
    "journalFsyncCount": ("journal", "fsyncCount"),
    "cleanupLatencyNs": ("journal", "cleanupLatencyNs"),
}


def summarize_case(case_id, samples):
    selected = [
        sample
        for sample in samples
        if sample["caseId"] == case_id and sample["phase"] == "measured"
    ]
    passed = [
        sample for sample in selected if sample["measurementStatus"] == "passed"
    ]
    summary = {
        "caseId": case_id,
        "measuredSampleCount": len(selected),
        "passedSampleCount": len(passed),
        "failedSampleCount": len(selected) - len(passed),
    }
    for name, keys in SUMMARY_AXES.items():
        summary[name] = integer_axis(
            [nested_value(sample, *keys) for sample in passed]
        )
    return summary


def build_decision(samples, summaries, native_host_scope):
    large = next(
        summary for summary in summaries if summary["caseId"] == "largeJournal"
    )
    axis = large["verifierPeakRssBytes"]
    peak = axis["maximum"] if axis is not None else None
    memory_pass = (
        peak is not None
        and peak <= POLICY["maximumVerifierPeakRssBytes"]
    )
    all_pass = all(
        sample["measurementStatus"] == "passed" for sample in samples
    )
    return {
        "dimensioningStatus": (
            "withinPredeclaredEnvelope"
            if memory_pass and all_pass
            else "requiresRefinement"
        ),
        "verifierMemoryEnvelopeStatus": "pass" if memory_pass else "fail",
        "maximumVerifierPeakRssBytes": POLICY[
            "maximumVerifierPeakRssBytes"
        ],
        "largeJournalVerifierPeakRssBytes": peak,
        "hostSessionCount": 1,
        "betweenHostVarianceEstimated": False,
        "nativeHostScope": native_host_scope,
        "scalarBestConfigurationSelected": False,
    }


def build_context(
    output_root,
    forbidden_roots,
    python_executable,
    dimensioning_id,
    host_session_id,
):
    dependencies = load_dependencies()
    output_root, forbidden_roots = validate_roots(
        output_root, forbidden_roots, dependencies["ledger"]
    )
    python_executable = pathlib.Path(python_executable).absolute().resolve()
    if not python_executable.is_file():
        raise DimensionError("pinned Python executable is missing")
    if python_executable != pathlib.Path(sys.executable).resolve():
        raise DimensionError("dimensioner must run under the pinned Python executable")
    output_root.mkdir(parents=True, exist_ok=False)
    ledger_root = output_root / "ledger"
    control_root = output_root / "control"
    sample_root = output_root / "samples"
    working_directory = output_root / "work"
    control_root.mkdir()
    sample_root.mkdir()
    working_directory.mkdir()
    directory = pathlib.Path(__file__).resolve().parent
    corpus = corpus_document()
    policy = policy_document()
    host = host_session_document(host_session_id)
    toolchain = toolchain_document(dependencies, python_executable)
    forbidden_hashes = sorted({path_hash(path) for path in forbidden_roots})
    report_inputs = {
        "dimensioningId": dimensioning_id,
        "hostSession": host,
        "toolchain": toolchain,
        "corpus": corpus,
        "policy": policy,
        "forbiddenRootPathSha256s": forbidden_hashes,
    }
    return {
        **dependencies,
        "outputRoot": output_root,
        "ledgerRoot": ledger_root,
        "controlRoot": control_root,
        "sampleRoot": sample_root,
        "workingDirectory": working_directory,
        "pythonExecutable": python_executable,
        "supervisorPath": directory / "wp14r_supervised_process.py",
        "recoveryPath": directory / "wp14r_stale_open_recovery.py",
        "forbiddenRoots": forbidden_roots,
        "inheritedEnvironmentNames": ["SystemRoot"] if os.name == "nt" else [],
        "dimensioningId": dimensioning_id,
        "host": host,
        "toolchain": toolchain,
        "corpus": corpus,
        "policy": policy,
        "forbiddenRootPathSha256s": forbidden_hashes,
        "reportInputsSha256": sha256_bytes(canonical(report_inputs)),
    }


def run_dimensioning(
    output_root,
    forbidden_roots,
    python_executable,
    dimensioning_id,
    host_session_id,
):
    context = build_context(
        output_root,
        forbidden_roots,
        python_executable,
        dimensioning_id,
        host_session_id,
    )
    samples = []
    for phase, repetitions in (
        ("pilot", POLICY["pilotRepetitionsPerCell"]),
        ("measured", POLICY["measuredRepetitionsPerCell"]),
    ):
        for case in fixed_corpus():
            for repetition in range(1, repetitions + 1):
                samples.append(
                    run_sample(context, case, phase, repetition)
                )
    summaries = [summarize_case(case_id, samples) for case_id in CASE_IDS]
    decision = build_decision(
        samples, summaries, context["host"]["platform"]
    )
    current_toolchain = toolchain_document(
        context, context["pythonExecutable"]
    )
    if current_toolchain != context["toolchain"]:
        raise DimensionError("toolchain source changed during dimensioning")
    report = {
        "schemaVersion": ARTIFACT_SCHEMA_VERSION,
        "schemaId": SCHEMA_ID,
        "reportType": REPORT_TYPE,
        "status": (
            "complete"
            if all(sample["measurementStatus"] == "passed" for sample in samples)
            else "completeWithMeasurementFailures"
        ),
        "claimBoundary": CLAIM_BOUNDARY,
        "dimensioningId": dimensioning_id,
        "generatedUtc": utc_now(),
        "reportInputsSha256": context["reportInputsSha256"],
        "forbiddenRootPathSha256s": context[
            "forbiddenRootPathSha256s"
        ],
        "hostSession": context["host"],
        "toolchain": context["toolchain"],
        "corpus": context["corpus"],
        "policy": context["policy"],
        "samples": samples,
        "summaries": summaries,
        "decision": decision,
    }
    validate_report(report)
    report_path = context["outputRoot"] / "mechanics-dimension-report.json"
    context["ledger"].write_exclusive(report_path, canonical(report))
    return report


def build_parser():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    run = commands.add_parser("run")
    run.add_argument("--output-root", type=pathlib.Path, required=True)
    run.add_argument(
        "--forbidden-root", type=pathlib.Path, action="append", required=True
    )
    run.add_argument("--python-executable", type=pathlib.Path, required=True)
    run.add_argument("--dimensioning-id", required=True)
    run.add_argument("--host-session-id", required=True)
    child = commands.add_parser("fake-child")
    child.add_argument("--case-id", required=True)
    child.add_argument("--payload-bytes", type=int, default=0)
    child.add_argument("--stderr-bytes", type=int, default=0)
    child.add_argument("--idle-ms", type=int, default=0)
    return parser


def validate_identifier(value, label):
    if not value or not value[0].isalnum() or len(value) > 128 or not all(
        character.isalnum() or character in "._-" for character in value
    ):
        raise DimensionError(f"{label} is not a safe canonical identifier")


def main(argv=None):
    arguments = build_parser().parse_args(argv)
    try:
        if arguments.command == "fake-child":
            if min(
                arguments.payload_bytes,
                arguments.stderr_bytes,
                arguments.idle_ms,
            ) < 0:
                raise DimensionError("fake child parameters cannot be negative")
            return run_fake_child(
                arguments.case_id,
                arguments.payload_bytes,
                arguments.stderr_bytes,
                arguments.idle_ms,
            )
        validate_identifier(arguments.dimensioning_id, "dimensioningId")
        validate_identifier(arguments.host_session_id, "hostSessionId")
        report = run_dimensioning(
            arguments.output_root,
            arguments.forbidden_root,
            arguments.python_executable,
            arguments.dimensioning_id,
            arguments.host_session_id,
        )
        sys.stdout.buffer.write(canonical(report) + b"\n")
        return 0 if report["decision"]["dimensioningStatus"] == (
            "withinPredeclaredEnvelope"
        ) else 2
    except Exception as error:  # noqa: BLE001 - CLI is fail-closed
        print(f"WP14R_DIMENSION_ERROR: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
