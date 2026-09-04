#!/usr/bin/env python3
"""Independently verify retained WP14R attempt-ledger mechanics."""

from __future__ import annotations

import argparse
import base64
import datetime
import hashlib
import json
import os
import pathlib
import re
import stat
import sys

import jsonschema

sys.dont_write_bytecode = True

VERIFIER_VERSION = "wp14r-independent-verifier-v1"
REPORT_SCHEMA_ID = (
    "https://ridebound.local/schemas/wp14r/v1/"
    "independent-verification-report.schema.json"
)
REPORT_TYPE = "ridebound-wp14r-independent-verification-report-v1"
CLAIM_BOUNDARY = [
    "mechanicalOnly",
    "independentReadOnlyImplementation",
    "doesNotReadScientificOutcomeFields",
    "doesNotAuthorizeRecoveryOrFreezeV2",
    "attemptsAreNotExperimentalUnits",
    "doesNotSupersedeWp14V1",
]
ZERO_SHA256 = "0" * 64
EMPTY_SHA256 = hashlib.sha256(b"").hexdigest()
MAXIMUM_ATTEMPTS = 2
MAXIMUM_LINE_BYTES = 1024 * 1024
JOB_PATTERN = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$")
ATTEMPT_PATTERN = re.compile(r"^attempt-(0[12])$")
UTC_PATTERN = re.compile(
    r"^[0-9]{4}-[0-9]{2}-[0-9]{2}T"
    r"[0-9]{2}:[0-9]{2}:[0-9]{2}(\.[0-9]{1,6})?Z$"
)
PROVENANCE_FIELDS = (
    "supervisionEvidenceVersion",
    "logSchemaSha256",
    "reportSchemaSha256",
)


class VerificationError(RuntimeError):
    """A typed, fail-closed independent-verification rejection."""

    def __init__(self, code, message):
        self.code = code
        super().__init__(f"{code}: {message}")


def reject(code, message):
    raise VerificationError(code, message)


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


def parse_utc(value, code="TIMESTAMP_ORDER"):
    if not isinstance(value, str) or not UTC_PATTERN.fullmatch(value):
        reject(code, "timestamp is not canonical UTC")
    try:
        parsed = datetime.datetime.fromisoformat(value[:-1] + "+00:00")
    except ValueError:
        reject(code, "timestamp is not a real UTC instant")
    if parsed.utcoffset() != datetime.timedelta(0):
        reject(code, "timestamp is not UTC")
    return parsed


def repository_root():
    return pathlib.Path(__file__).resolve().parents[2]


def schema_root():
    return repository_root() / "benchmarks/schemas/wp14r/v1"


def schema_path(name):
    return schema_root() / name


def _is_link_or_junction(path):
    junction_test = getattr(os.path, "isjunction", None)
    if path.is_symlink() or bool(junction_test and junction_test(path)):
        return True
    try:
        attributes = getattr(path.lstat(), "st_file_attributes", 0)
    except OSError:
        return False
    reparse = getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400)
    return bool(attributes & reparse)


def _overlaps(first, second):
    return first == second or first in second.parents or second in first.parents


def _stable_measure(path, code="INVENTORY_MISMATCH"):
    if not path.is_file() or _is_link_or_junction(path):
        reject(code, f"not a regular file: {path}")
    try:
        before = path.stat()
        digest = hashlib.sha256()
        length = 0
        with path.open("rb") as stream:
            for block in iter(lambda: stream.read(1024 * 1024), b""):
                digest.update(block)
                length += len(block)
        after = path.stat()
    except OSError as error:
        reject(code, f"cannot hash file: {path}: {type(error).__name__}")
    if (
        length != before.st_size
        or after.st_size != before.st_size
        or after.st_mtime_ns != before.st_mtime_ns
    ):
        reject(code, f"file changed while hashed: {path}")
    return {"bytes": length, "sha256": digest.hexdigest()}


def _load_schema(name):
    path = schema_path(name)
    try:
        raw = path.read_bytes()
        document = json.loads(raw.decode("utf-8"))
        jsonschema.Draft202012Validator.check_schema(document)
    except (
        OSError,
        UnicodeError,
        json.JSONDecodeError,
        jsonschema.SchemaError,
    ) as error:
        reject("SCHEMA_SOURCE", f"invalid schema source {path}: {error}")
    return document


def _validator(name):
    return jsonschema.Draft202012Validator(_load_schema(name))


def _validate(document, schema_name, code):
    try:
        _validator(schema_name).validate(document)
    except jsonschema.ValidationError as error:
        reject(code, f"{schema_name}: {error.message}")


def _schema_tree_sha256():
    entries = []
    try:
        candidates = sorted(
            schema_root().glob("*.schema.json"), key=lambda item: item.name
        )
    except OSError as error:
        reject("SCHEMA_SOURCE", f"cannot enumerate schemas: {error}")
    if not candidates:
        reject("SCHEMA_SOURCE", "WP14R schema tree is empty")
    for path in candidates:
        measurement = _stable_measure(path, "SCHEMA_SOURCE")
        entries.append(
            {
                "path": path.name,
                "bytes": measurement["bytes"],
                "sha256": measurement["sha256"],
            }
        )
    return sha256_bytes(canonical(entries))


def _normalized_path(path):
    value = str(pathlib.Path(path).absolute().resolve())
    return value.casefold() if os.name == "nt" else value


def _path_sha256(path):
    return sha256_bytes(_normalized_path(path).encode("utf-8"))


def _validate_paths(ledger_root, forbidden_roots):
    raw_root = pathlib.Path(ledger_root).absolute()
    current = raw_root
    while True:
        if current.exists() and _is_link_or_junction(current):
            reject("PATH_UNSAFE", "ledger ancestry contains a link or junction")
        if current.parent == current:
            break
        current = current.parent
    if not forbidden_roots:
        reject("PATH_UNSAFE", "at least one forbidden root is required")
    root = raw_root.resolve()
    forbidden = [pathlib.Path(path).absolute().resolve() for path in forbidden_roots]
    if any(_overlaps(root, path) for path in forbidden):
        reject("PATH_UNSAFE", "ledger root overlaps a forbidden root")
    hashes = sorted({_path_sha256(path) for path in forbidden})
    if len(hashes) != len(forbidden):
        reject("PATH_UNSAFE", "forbidden roots are duplicated")
    return root, forbidden, hashes


def _job_root(ledger_root, job_id):
    if not isinstance(job_id, str) or not JOB_PATTERN.fullmatch(job_id):
        reject("PATH_UNSAFE", "jobId is not a safe canonical identifier")
    raw_path = ledger_root / job_id
    if raw_path.exists() and _is_link_or_junction(raw_path):
        reject("PATH_UNSAFE", "job root is a link or junction")
    path = raw_path.resolve()
    try:
        path.relative_to(ledger_root)
    except ValueError:
        reject("PATH_UNSAFE", "job path escapes the ledger root")
    if path.exists() and _is_link_or_junction(path):
        reject("PATH_UNSAFE", "job root is a link or junction")
    return path


def _read_canonical_json(path, schema_name, code):
    if not path.is_file() or _is_link_or_junction(path):
        reject(code, f"canonical record is not a regular file: {path}")
    try:
        before = path.stat()
        raw = path.read_bytes()
        after = path.stat()
        document = json.loads(raw.decode("utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        reject(code, f"cannot decode canonical JSON: {type(error).__name__}")
    if (
        len(raw) != before.st_size
        or after.st_size != before.st_size
        or after.st_mtime_ns != before.st_mtime_ns
    ):
        reject(code, f"canonical record changed while read: {path}")
    if raw != canonical(document):
        reject("CANONICAL_JSON", f"record is not byte-canonical: {path}")
    _validate(document, schema_name, "SCHEMA_RECORD")
    return document, raw


def _file_inventory(path):
    if not path.exists():
        return {"exists": False, "bytes": 0, "sha256": None}
    measurement = _stable_measure(path)
    return {
        "exists": True,
        "bytes": measurement["bytes"],
        "sha256": measurement["sha256"],
    }


def _directory_inventory(path):
    empty_hash = sha256_bytes(canonical([]))
    if not path.exists():
        return {
            "exists": False,
            "fileCount": 0,
            "bytes": 0,
            "treeSha256": empty_hash,
        }, 0
    if not path.is_dir() or _is_link_or_junction(path):
        reject("PATH_UNSAFE", "attempt output is not a regular directory")
    candidates = sorted(path.rglob("*"), key=lambda item: item.as_posix())
    signature = []
    entries = []
    for candidate in candidates:
        if _is_link_or_junction(candidate):
            reject("PATH_UNSAFE", f"output contains a link: {candidate}")
        if candidate.is_dir():
            signature.append((candidate.relative_to(path).as_posix(), "directory"))
            continue
        if not candidate.is_file():
            reject("PATH_UNSAFE", f"output contains a non-file: {candidate}")
        measured = _stable_measure(candidate)
        signature.append((candidate.relative_to(path).as_posix(), "file"))
        entries.append(
            {
                "path": candidate.relative_to(path).as_posix(),
                "bytes": measured["bytes"],
                "sha256": measured["sha256"],
            }
        )
    after_candidates = sorted(
        path.rglob("*"), key=lambda item: item.as_posix()
    )
    after_signature = []
    for candidate in after_candidates:
        if _is_link_or_junction(candidate):
            reject("PATH_UNSAFE", f"output contains a link: {candidate}")
        kind = "directory" if candidate.is_dir() else "file"
        if kind == "file" and not candidate.is_file():
            reject("PATH_UNSAFE", f"output contains a non-file: {candidate}")
        after_signature.append((candidate.relative_to(path).as_posix(), kind))
    if after_signature != signature:
        reject("INVENTORY_MISMATCH", "output changed while inventoried")
    return {
        "exists": True,
        "fileCount": len(entries),
        "bytes": sum(item["bytes"] for item in entries),
        "treeSha256": sha256_bytes(canonical(entries)),
    }, len(entries)


def _stream_state():
    return {
        "digest": hashlib.sha256(),
        "bytes": 0,
        "chunks": 0,
        "eof": False,
    }


def _current_supervision_provenance():
    return {
        "supervisionEvidenceVersion": (
            "wp14r-supervision-schema-provenance-v1"
        ),
        "logSchemaSha256": _stable_measure(
            schema_path("supervision-log-record.schema.json"),
            "SCHEMA_SOURCE",
        )["sha256"],
        "reportSchemaSha256": _stable_measure(
            schema_path("supervision-report.schema.json"),
            "SCHEMA_SOURCE",
        )["sha256"],
    }


def _verify_supervision_provenance(start):
    present = [field in start for field in PROVENANCE_FIELDS]
    if not any(present):
        return "legacy"
    if not all(present):
        reject("SCHEMA_PROVENANCE", "journal provenance is partial")
    expected = _current_supervision_provenance()
    if any(start[field] != expected[field] for field in PROVENANCE_FIELDS):
        reject("SCHEMA_PROVENANCE", "journal schema provenance is stale")
    return "bound"


def _empty_journal(status, truncated_tail=0, inventory=None):
    return {
        "report": {
            "status": status,
            "recordCount": 0,
            "truncatedTailBytes": truncated_tail,
            "finalRecordSha256": None,
            "terminalStatus": None,
            "processExitCode": None,
            "treeStatus": "notObserved",
            "schemaProvenance": "notApplicable",
        },
        "start": None,
        "intent": None,
        "child": None,
        "launchFailure": None,
        "childExit": None,
        "terminal": None,
        "inventory": inventory
        or {"exists": False, "bytes": 0, "sha256": None},
    }


def _verify_journal(path):
    if not path.exists():
        return _empty_journal("absent")
    if not path.is_file() or _is_link_or_junction(path):
        reject("PATH_UNSAFE", "process journal is not a regular file")
    validator = _validator("supervision-log-record.schema.json")
    streams = {"stdout": _stream_state(), "stderr": _stream_state()}
    previous_hash = ZERO_SHA256
    previous_elapsed = -1
    previous_observed = None
    digest = hashlib.sha256()
    total_bytes = 0
    count = 0
    truncated_tail = 0
    start = None
    intent = None
    child = None
    launch_failure = None
    child_exit = None
    terminal = None
    limit = None
    reader_failures = set()
    final_hash = None
    try:
        before = path.stat()
        with path.open("rb") as stream:
            while True:
                line = stream.readline(MAXIMUM_LINE_BYTES + 1)
                if not line:
                    break
                digest.update(line)
                total_bytes += len(line)
                if len(line) > MAXIMUM_LINE_BYTES:
                    reject("JOURNAL_FORMAT", "journal line exceeds hard bound")
                if not line.endswith(b"\n"):
                    truncated_tail = len(line)
                    if stream.read(1):
                        reject("JOURNAL_FORMAT", "oversized truncated tail")
                    break
                try:
                    record = json.loads(line[:-1].decode("utf-8"))
                except (UnicodeError, json.JSONDecodeError):
                    reject("JOURNAL_FORMAT", "journal line is not UTF-8 JSON")
                if line != canonical(record) + b"\n":
                    reject("CANONICAL_JSON", "journal line is not canonical")
                try:
                    validator.validate(record)
                except jsonschema.ValidationError as error:
                    reject("SCHEMA_RECORD", f"journal record: {error.message}")
                projection = {
                    key: value
                    for key, value in record.items()
                    if key != "recordSha256"
                }
                expected_hash = sha256_bytes(canonical(projection))
                # observedUtc is still parsed, so a malformed stamp is rejected,
                # but it is NOT part of the chain invariant. The supervisor
                # contract (RB-WP14R-003) makes the monotonic clock the authority
                # for heartbeat and timeout and keeps UTC as provenance only. A
                # wall clock is not monotonic: an NTP step during a run moves it
                # backwards, which is legitimate host behaviour outside
                # experimental control. Enforcing it here contradicted that
                # contract and made a completed, otherwise clean attempt
                # permanently unverifiable — see the 1.846 s step that failed
                # RB-WP14R-008 under freeze v3. `monotonicElapsedMs` remains a
                # hard invariant and is immune to clock adjustment.
                observed = parse_utc(record["observedUtc"], "JOURNAL_CHAIN")
                if (
                    record["sequence"] != count
                    or record["previousRecordSha256"] != previous_hash
                    or record["recordSha256"] != expected_hash
                    or record["monotonicElapsedMs"] < previous_elapsed
                ):
                    reject(
                        "JOURNAL_CHAIN",
                        "journal hash, sequence, or monotonic chain failed",
                    )
                if terminal is not None:
                    reject("JOURNAL_SEMANTICS", "record follows terminal")
                record_type = record["recordType"]
                payload = record["payload"]
                if count == 0:
                    if record_type != "supervisorStart":
                        reject(
                            "JOURNAL_SEMANTICS",
                            "first record is not supervisorStart",
                        )
                    start = payload
                elif record_type == "supervisorStart":
                    reject("JOURNAL_SEMANTICS", "duplicate supervisor start")
                else:
                    (
                        intent,
                        child,
                        launch_failure,
                        child_exit,
                        terminal,
                        limit,
                    ) = _consume_journal_event(
                        record_type,
                        payload,
                        start,
                        streams,
                        intent,
                        child,
                        launch_failure,
                        child_exit,
                        terminal,
                        limit,
                        reader_failures,
                    )
                previous_hash = expected_hash
                previous_elapsed = record["monotonicElapsedMs"]
                previous_observed = observed
                final_hash = record["recordSha256"]
                count += 1
        after = path.stat()
    except VerificationError:
        raise
    except OSError as error:
        reject("JOURNAL_FORMAT", f"cannot stream journal: {error}")
    if (
        total_bytes != before.st_size
        or after.st_size != before.st_size
        or after.st_mtime_ns != before.st_mtime_ns
    ):
        reject("JOURNAL_FORMAT", "journal changed while streamed")
    inventory = {
        "exists": True,
        "bytes": total_bytes,
        "sha256": digest.hexdigest(),
    }
    if count == 0:
        return _empty_journal("noCompleteRecord", truncated_tail, inventory)
    provenance = _verify_supervision_provenance(start)
    expected_supervisor_hash = _stable_measure(
        pathlib.Path(__file__).with_name("wp14r_supervised_process.py"),
        "SOURCE_PROVENANCE",
    )["sha256"]
    if start["supervisorSha256"] != expected_supervisor_hash:
        reject("SOURCE_PROVENANCE", "journal supervisor source hash is stale")
    if terminal is not None and truncated_tail:
        reject("JOURNAL_FORMAT", "bytes follow a terminal journal")
    if terminal is not None:
        _verify_journal_terminal(
            start,
            streams,
            intent,
            child,
            launch_failure,
            child_exit,
            terminal,
            limit,
            reader_failures,
        )
    report = {
        "status": "validComplete" if terminal else "validPartial",
        "recordCount": count,
        "truncatedTailBytes": truncated_tail,
        "finalRecordSha256": final_hash,
        "terminalStatus": terminal["status"] if terminal else None,
        "processExitCode": terminal["processExitCode"] if terminal else None,
        "treeStatus": terminal["treeStatus"] if terminal else "notObserved",
        "schemaProvenance": provenance,
    }
    return {
        "report": report,
        "start": start,
        "intent": intent,
        "child": child,
        "launchFailure": launch_failure,
        "childExit": child_exit,
        "terminal": terminal,
        "inventory": inventory,
    }


def _consume_journal_event(
    record_type,
    payload,
    start,
    streams,
    intent,
    child,
    launch_failure,
    child_exit,
    terminal,
    limit,
    reader_failures,
):
    if child_exit is not None and record_type != "supervisorTerminal":
        reject("JOURNAL_SEMANTICS", "record follows child exit")
    if launch_failure is not None and record_type != "supervisorTerminal":
        reject("JOURNAL_SEMANTICS", "record follows launch failure")
    if record_type == "launchIntent":
        if (
            intent is not None
            or child is not None
            or launch_failure is not None
            or payload["commandSha256"] != start["commandSha256"]
            or payload["executableSha256"] != start["executableSha256"]
        ):
            reject("JOURNAL_SEMANTICS", "launch intent is inconsistent")
        intent = payload
    elif record_type == "childStarted":
        if child is not None or intent is None or launch_failure is not None:
            reject("JOURNAL_SEMANTICS", "child start is inconsistent")
        child = payload
    elif record_type == "streamChunk":
        if child is None:
            reject("JOURNAL_SEMANTICS", "stream chunk precedes child")
        state = streams[payload["stream"]]
        if state["eof"] or payload["streamSequence"] != state["chunks"]:
            reject("JOURNAL_SEMANTICS", "stream chunk sequence is invalid")
        try:
            content = base64.b64decode(payload["dataBase64"], validate=True)
        except ValueError:
            reject("JOURNAL_SEMANTICS", "stream chunk base64 is invalid")
        if (
            not content
            or len(content) != payload["byteCount"]
            or sha256_bytes(content) != payload["chunkSha256"]
            or len(content) > start["policy"]["chunkBytes"]
        ):
            reject("JOURNAL_SEMANTICS", "stream chunk bytes are invalid")
        state["digest"].update(content)
        state["bytes"] += len(content)
        state["chunks"] += 1
        if (
            state["bytes"] != payload["cumulativeCapturedBytes"]
            or state["bytes"] > start["policy"]["maximumStreamBytes"]
        ):
            reject("JOURNAL_SEMANTICS", "stream cumulative bytes are invalid")
    elif record_type == "streamEof":
        if child is None:
            reject("JOURNAL_SEMANTICS", "stream EOF precedes child")
        state = streams[payload["stream"]]
        digest = state["digest"].hexdigest()
        if (
            state["eof"]
            or payload["capturedBytes"] != state["bytes"]
            or payload["capturedChunkCount"] != state["chunks"]
            or payload["capturedSha256"] != digest
            or payload["observedBytes"] < state["bytes"]
            or (
                payload["observedBytes"] == state["bytes"]
                and payload["observedSha256"] != digest
            )
        ):
            reject("JOURNAL_SEMANTICS", "stream EOF summary is invalid")
        state["eof"] = True
    elif record_type == "heartbeat":
        if child is None:
            reject("JOURNAL_SEMANTICS", "heartbeat precedes child")
        if (
            payload["stdoutCapturedBytes"] != streams["stdout"]["bytes"]
            or payload["stderrCapturedBytes"] != streams["stderr"]["bytes"]
        ):
            reject("JOURNAL_SEMANTICS", "heartbeat stream counts are invalid")
    elif record_type == "limitTriggered":
        if child is None or limit is not None:
            reject("JOURNAL_SEMANTICS", "limit state is inconsistent")
        _verify_limit(payload, start["policy"])
        limit = payload["limit"]
    elif record_type == "readerFailure":
        if child is None or payload["stream"] in reader_failures:
            reject("JOURNAL_SEMANTICS", "reader failure is inconsistent")
        reader_failures.add(payload["stream"])
    elif record_type == "launchFailure":
        if intent is None or child is not None or launch_failure is not None:
            reject("JOURNAL_SEMANTICS", "launch failure is inconsistent")
        launch_failure = payload
    elif record_type == "childExit":
        if child is None or child_exit is not None:
            reject("JOURNAL_SEMANTICS", "child exit is inconsistent")
        child_exit = payload
    elif record_type == "supervisorTerminal":
        terminal = payload
    else:
        reject("JOURNAL_SEMANTICS", f"unknown journal event: {record_type}")
    return (
        intent,
        child,
        launch_failure,
        child_exit,
        terminal,
        limit,
    )


def _verify_limit(payload, policy):
    limit = payload["limit"]
    observed = payload["observedValue"]
    maximum = payload["maximumValue"]
    if limit == "wallTimeout":
        expected = policy["wallTimeoutMs"]
        valid = maximum == expected and observed >= maximum
    elif limit in ("stdoutBytes", "stderrBytes"):
        expected = policy["maximumStreamBytes"]
        valid = maximum == expected and observed > maximum
    else:
        valid = limit == "cancellation" and observed == 1 and maximum == 0
    if not valid:
        reject("JOURNAL_SEMANTICS", "limit evidence differs from policy")


def _verify_journal_terminal(
    start,
    streams,
    intent,
    child,
    launch_failure,
    child_exit,
    terminal,
    limit,
    reader_failures,
):
    if terminal["bundleVerificationStatus"] != "notRun":
        reject("JOURNAL_SEMANTICS", "supervisor claims bundle verification")
    for name in ("stdout", "stderr"):
        if (
            terminal[f"{name}CapturedBytes"] != streams[name]["bytes"]
            or terminal[f"{name}CapturedSha256"]
            != streams[name]["digest"].hexdigest()
        ):
            reject("JOURNAL_SEMANTICS", "terminal stream inventory differs")
    status = terminal["status"]
    if status == "launchFailure":
        if (
            launch_failure is None
            or launch_failure["stage"] != "createProcess"
            or intent is None
            or child is not None
            or child_exit is not None
            or terminal["treeStatus"] != "notObserved"
        ):
            reject("JOURNAL_SEMANTICS", "launch-failure terminal is invalid")
    elif status == "treeUncertain" and launch_failure is not None:
        if (
            launch_failure["stage"] != "containment"
            or intent is None
            or child is not None
            or child_exit is not None
            or terminal["treeStatus"] != "uncertain"
        ):
            reject("JOURNAL_SEMANTICS", "containment terminal is invalid")
    elif intent is None or child is None or child_exit is None:
        reject("JOURNAL_SEMANTICS", "complete journal lacks child exit")
    elif (
        terminal["processExitCode"] != child_exit["processExitCode"]
        or terminal["treeStatus"] != child_exit["treeStatus"]
    ):
        reject("JOURNAL_SEMANTICS", "terminal differs from child exit")
    if status == "childExitedZeroAwaitingBundleVerification" and (
        terminal["processExitCode"] != 0
        or terminal["treeStatus"] != "exitedCleanly"
    ):
        reject("JOURNAL_SEMANTICS", "exit-zero terminal is not clean")
    if status in (
        "childExitedZeroAwaitingBundleVerification",
        "childExitFailure",
    ) and not all(state["eof"] for state in streams.values()):
        reject("JOURNAL_SEMANTICS", "normal terminal lacks stream EOF")
    if status == "childExitFailure" and terminal["processExitCode"] in (None, 0):
        reject("JOURNAL_SEMANTICS", "child failure lacks non-zero exit")
    if status == "readerFailure" and not reader_failures:
        reject("JOURNAL_SEMANTICS", "reader terminal lacks failure event")
    if status == "treeLeakTerminated" and (
        child_exit is None
        or not child_exit["cleanupRequired"]
        or terminal["treeStatus"] != "terminated"
    ):
        reject("JOURNAL_SEMANTICS", "tree-leak terminal is invalid")
    if status == "treeUncertain" and terminal["treeStatus"] != "uncertain":
        reject("JOURNAL_SEMANTICS", "tree uncertainty is not explicit")
    expected_limits = {
        "wallTimeout": "wallTimeout",
        "stdoutLimit": "stdoutBytes",
        "stderrLimit": "stderrBytes",
        "cancelled": "cancellation",
    }
    if status in expected_limits and limit != expected_limits[status]:
        reject("JOURNAL_SEMANTICS", "terminal limit classification differs")


def _list_attempts(job_path):
    if not job_path.exists():
        return []
    if not job_path.is_dir() or _is_link_or_junction(job_path):
        reject("PATH_UNSAFE", "job root is not a regular directory")
    attempts = []
    try:
        children = list(job_path.iterdir())
    except OSError as error:
        reject("PATH_UNSAFE", f"cannot enumerate job root: {error}")
    for child in children:
        match = ATTEMPT_PATTERN.fullmatch(child.name)
        if not match:
            reject("ENTRY_UNEXPECTED", f"unexpected ledger entry: {child.name}")
        if not child.is_dir() or _is_link_or_junction(child):
            reject("PATH_UNSAFE", "attempt path is not a regular directory")
        attempts.append((int(match.group(1)), child))
    attempts.sort(key=lambda item: item[0])
    numbers = [number for number, _ in attempts]
    if numbers != list(range(1, len(numbers) + 1)):
        reject("ATTEMPT_SEQUENCE", "attempt directories contain a gap")
    if len(numbers) > MAXIMUM_ATTEMPTS:
        reject("ATTEMPT_SEQUENCE", "more than two attempts are retained")
    return attempts


def _validate_attempt_entries(attempt_path):
    allowed = {
        "attempt-start.json",
        "attempt-terminal.json",
        "process.log",
        "recovery-receipt.json",
        "output",
    }
    try:
        children = list(attempt_path.iterdir())
    except OSError as error:
        reject("PATH_UNSAFE", f"cannot enumerate attempt: {error}")
    unexpected = sorted(child.name for child in children if child.name not in allowed)
    if unexpected:
        reject(
            "ENTRY_UNEXPECTED",
            f"attempt has unexpected entries: {', '.join(unexpected)}",
        )
    output = attempt_path / "output"
    if output.exists() and (not output.is_dir() or _is_link_or_junction(output)):
        reject("PATH_UNSAFE", "output is not a regular directory")
    for name in (
        "attempt-start.json",
        "attempt-terminal.json",
        "process.log",
        "recovery-receipt.json",
    ):
        candidate = attempt_path / name
        if candidate.exists() and (
            not candidate.is_file() or _is_link_or_junction(candidate)
        ):
            reject("PATH_UNSAFE", f"{name} is not a regular file")


def _expected_disposition(number, status, classification, tree_status):
    if status == "pass":
        return "terminalSuccess"
    if classification == "processTreeUncertain" or tree_status == "uncertain":
        return "attemptsExhausted"
    if number < MAXIMUM_ATTEMPTS:
        return "recoveryAuthorized"
    return "attemptsExhausted"


def _verify_journal_start(start, ledger_start, start_raw):
    if start is None:
        return
    expected = {
        "jobId": ledger_start["jobId"],
        "attemptId": ledger_start["attemptId"],
        "attemptNumber": ledger_start["attemptNumber"],
        "startReceiptSha256": sha256_bytes(start_raw),
        "commandSha256": ledger_start["commandSha256"],
    }
    if any(start[field] != value for field, value in expected.items()):
        reject("START_BINDING", "journal start differs from attempt start")


def _verify_terminal_journal_binding(terminal, journal):
    report = journal["report"]
    if report["status"] != "validComplete":
        reject("TERMINAL_SEMANTICS", "non-recovery terminal lacks a complete journal")
    if (
        terminal["processExitCode"] != report["processExitCode"]
        or terminal["processTreeStatus"] != report["treeStatus"]
    ):
        reject("TERMINAL_SEMANTICS", "ledger terminal differs from journal")
    classification = terminal["exitClassification"]
    status = report["terminalStatus"]
    verification = terminal["bundleVerification"]["status"]
    valid_statuses = {
        "success": {"childExitedZeroAwaitingBundleVerification"},
        "controlledTimeout": {"wallTimeout"},
        "processExitFailure": {"childExitFailure"},
        "verifierFailure": {"childExitedZeroAwaitingBundleVerification"},
    }
    if (
        classification not in valid_statuses
        or status not in valid_statuses[classification]
    ):
        reject(
            "TERMINAL_SEMANTICS",
            "ledger exit classification differs from journal terminal",
        )
    if classification == "success" and verification != "pass":
        reject("TERMINAL_SEMANTICS", "success does not bind a verified bundle")
    if classification == "verifierFailure" and verification != "fail":
        reject("TERMINAL_SEMANTICS", "verifier failure lacks failed verification")
    if classification in ("controlledTimeout", "processExitFailure") and (
        verification != "notRun"
    ):
        reject(
            "TERMINAL_SEMANTICS",
            "process failure cannot claim bundle verification",
        )


def _recovery_projection(journal):
    report = journal["report"]
    start = journal["start"]
    intent = journal["intent"]
    child = journal["child"]
    launch_failure = journal["launchFailure"]
    child_exit = journal["childExit"]
    terminal = journal["terminal"]
    if start is None:
        return {
            "launchState": "beforeSupervisorStart",
            "childProcessId": None,
            "containment": None,
            "cleanupAction": "notPossible",
            "treeStatus": "uncertain",
            "treeSafe": False,
        }
    if intent is None:
        return {
            "launchState": "supervisorStarted",
            "childProcessId": None,
            "containment": None,
            "cleanupAction": "none",
            "treeStatus": "notObserved",
            "treeSafe": True,
        }
    if (
        child is None
        and launch_failure is not None
        and launch_failure["stage"] == "createProcess"
    ):
        return {
            "launchState": (
                "supervisorTerminal" if terminal else "launchFailedBeforeChild"
            ),
            "childProcessId": None,
            "containment": None,
            "cleanupAction": "none",
            "treeStatus": "notObserved",
            "treeSafe": True,
        }
    if child is None:
        return {
            "launchState": "launchAmbiguous",
            "childProcessId": None,
            "containment": None,
            "cleanupAction": "notPossible",
            "treeStatus": "uncertain",
            "treeSafe": False,
        }
    if terminal and report["terminalStatus"] == (
        "childExitedZeroAwaitingBundleVerification"
    ):
        reject(
            "RECOVERY_BINDING",
            "exit-zero journal cannot authorize orphan recovery",
        )
    return {
        "launchState": (
            "supervisorTerminal"
            if terminal
            else "childExitObserved"
            if child_exit
            else "contained"
        ),
        "childProcessId": child["processId"],
        "containment": child["containment"],
        "cleanupAction": (
            "observeWindowsJobClose"
            if child["containment"] == "windowsJobObject"
            else "terminatePosixProcessGroup"
        ),
        "treeStatus": None,
        "treeSafe": None,
    }


def _verify_recovery_receipt(
    receipt,
    receipt_raw,
    start,
    start_raw,
    terminal,
    journal,
    actual_log_inventory,
):
    if parse_utc(receipt["observedUtc"]) < parse_utc(start["startedUtc"]):
        reject("RECOVERY_BINDING", "recovery predates attempt start")
    expected = {
        "jobId": start["jobId"],
        "attemptId": start["attemptId"],
        "attemptNumber": start["attemptNumber"],
        "startReceiptSha256": sha256_bytes(start_raw),
        "commandSha256": start["commandSha256"],
        "processLogInventory": actual_log_inventory,
        "journalState": (
            "noCompleteRecord"
            if journal["report"]["status"] in ("absent", "noCompleteRecord")
            else journal["report"]["status"]
        ),
        "journalRecordCount": journal["report"]["recordCount"],
        "journalTruncatedTailBytes": journal["report"]["truncatedTailBytes"],
        "journalFinalRecordSha256": journal["report"]["finalRecordSha256"],
        "journalTerminalStatus": journal["report"]["terminalStatus"],
    }
    if any(receipt[field] != value for field, value in expected.items()):
        reject("RECOVERY_BINDING", "recovery receipt evidence differs")
    source_expectations = {
        "supervisorToolSha256": _stable_measure(
            pathlib.Path(__file__).with_name("wp14r_supervised_process.py"),
            "SOURCE_PROVENANCE",
        )["sha256"],
        "recoveryToolSha256": _stable_measure(
            pathlib.Path(__file__).with_name("wp14r_stale_open_recovery.py"),
            "SOURCE_PROVENANCE",
        )["sha256"],
    }
    if any(receipt[field] != value for field, value in source_expectations.items()):
        reject("SOURCE_PROVENANCE", "recovery source provenance is stale")
    if journal["start"] is not None and (
        receipt["expectedSupervisorProcessId"]
        != journal["start"]["supervisorProcessId"]
    ):
        reject("RECOVERY_BINDING", "recovery supervisor PID differs")
    projection = _recovery_projection(journal)
    for field in (
        "launchState",
        "childProcessId",
        "containment",
        "cleanupAction",
    ):
        if receipt[field] != projection[field]:
            reject("RECOVERY_BINDING", f"recovery {field} differs")
    if projection["treeSafe"] is None:
        if receipt["containment"] == "windowsJobObject":
            safe = receipt["childAbsent"] is True
            if receipt["processGroupAbsent"] is not None:
                reject("RECOVERY_BINDING", "Windows recovery claims a process group")
        else:
            safe = (
                receipt["childAbsent"] is True
                and receipt["processGroupAbsent"] is True
            )
        if not safe:
            expected_tree = "uncertain"
        elif journal["childExit"] is not None and journal["childExit"][
            "treeStatus"
        ] in ("exitedCleanly", "terminated"):
            expected_tree = journal["childExit"]["treeStatus"]
        else:
            expected_tree = "terminated"
    else:
        safe = projection["treeSafe"]
        expected_tree = projection["treeStatus"]
        if (
            receipt["childAbsent"] is not None
            or receipt["processGroupAbsent"] is not None
        ):
            reject("RECOVERY_BINDING", "pre-child recovery claims child absence")
    classification = (
        "launcherRecoveredOrphanedStart" if safe else "processTreeUncertain"
    )
    disposition = _expected_disposition(
        start["attemptNumber"], "notRun", classification, expected_tree
    )
    state_expectations = {
        "supervisorAbsent": True,
        "treeStatus": expected_tree,
        "treeSafe": safe,
        "ledgerExitClassification": classification,
        "retryAuthorized": disposition == "recoveryAuthorized",
        "ledgerRetryDisposition": disposition,
        "outcomeFieldsRead": False,
    }
    if any(receipt[field] != value for field, value in state_expectations.items()):
        reject("RECOVERY_BINDING", "recovery decision is inconsistent")
    if terminal is not None:
        if (
            terminal["recoveryReceiptSha256"] != sha256_bytes(receipt_raw)
            or terminal["terminalUtc"] != receipt["observedUtc"]
            or terminal["exitClassification"] != classification
            or terminal["processTreeStatus"] != expected_tree
            or terminal["retryDisposition"] != disposition
            or terminal["processExitCode"] is not None
        ):
            reject("RECOVERY_BINDING", "terminal differs from recovery receipt")
        elapsed = max(
            0,
            round(
                (
                    parse_utc(receipt["observedUtc"])
                    - parse_utc(start["startedUtc"])
                ).total_seconds()
                * 1000
            ),
        )
        if terminal["elapsedMs"] != elapsed:
            reject("RECOVERY_BINDING", "recovery elapsed time is inconsistent")


def _verify_terminal(
    terminal,
    start,
    start_raw,
    attempt_path,
    journal,
    actual_log_inventory,
    actual_output_inventory,
):
    if (
        terminal["jobId"] != start["jobId"]
        or terminal["attemptId"] != start["attemptId"]
        or terminal["attemptNumber"] != start["attemptNumber"]
        or terminal["startReceiptSha256"] != sha256_bytes(start_raw)
    ):
        reject("TERMINAL_BINDING", "terminal cross-binding failed")
    if parse_utc(terminal["terminalUtc"]) < parse_utc(start["startedUtc"]):
        reject("TIMESTAMP_ORDER", "terminal predates attempt start")
    if terminal["processLogInventory"] != actual_log_inventory:
        reject("INVENTORY_MISMATCH", "process journal changed after terminal")
    if terminal["outputInventory"] != actual_output_inventory:
        reject("INVENTORY_MISMATCH", "output changed after terminal")
    verification = terminal["bundleVerification"]
    status = verification["status"]
    classification = terminal["exitClassification"]
    exit_code = terminal["processExitCode"]
    tree_status = terminal["processTreeStatus"]
    expected_disposition = _expected_disposition(
        start["attemptNumber"], status, classification, tree_status
    )
    if terminal["retryDisposition"] != expected_disposition:
        reject("TERMINAL_SEMANTICS", "retry disposition is inconsistent")
    if status == "pass":
        if (
            classification != "success"
            or exit_code != 0
            or tree_status != "exitedCleanly"
        ):
            reject("TERMINAL_SEMANTICS", "verified pass is not clean success")
        manifest = attempt_path / "output/bundle-manifest.json"
        if not manifest.is_file() or _is_link_or_junction(manifest):
            reject("TERMINAL_SEMANTICS", "verified pass lacks bundle manifest")
        evidence_fields = (
            "verifierId",
            "verifierSha256",
            "bundleManifestSha256",
            "behavioralHash",
        )
        if any(verification[field] is None for field in evidence_fields):
            reject("TERMINAL_SEMANTICS", "verified pass lacks evidence")
        if _stable_measure(manifest)["sha256"] != verification[
            "bundleManifestSha256"
        ]:
            reject("INVENTORY_MISMATCH", "bundle manifest hash differs")
    elif classification == "success":
        reject("TERMINAL_SEMANTICS", "success lacks verified pass")
    if classification == "processExitFailure" and exit_code in (None, 0):
        reject("TERMINAL_SEMANTICS", "process failure lacks non-zero exit")
    if classification == "processTreeUncertain" and tree_status != "uncertain":
        reject("TERMINAL_SEMANTICS", "tree uncertainty is not explicit")
    verifier_fields = (
        verification["verifierId"],
        verification["verifierSha256"],
        verification["behavioralHash"],
    )
    if status == "notRun" and any(value is not None for value in verifier_fields):
        reject("TERMINAL_SEMANTICS", "notRun claims verifier evidence")
    if status == "fail" and any(value is None for value in verifier_fields[:2]):
        reject("TERMINAL_SEMANTICS", "failed verifier lacks identity")
    if classification not in (
        "launcherRecoveredOrphanedStart",
        "processTreeUncertain",
    ):
        if terminal["recoveryReceiptSha256"] is not None or (
            attempt_path / "recovery-receipt.json"
        ).exists():
            reject("RECOVERY_BINDING", "non-recovery terminal binds recovery")
        _verify_terminal_journal_binding(terminal, journal)


def verify_ledger(
    ledger_root,
    job_id,
    forbidden_roots,
    verified_utc=None,
):
    """Verify one ledger without mutating it or reading outcome fields."""
    root, _, forbidden_hashes = _validate_paths(ledger_root, forbidden_roots)
    job_path = _job_root(root, job_id)
    schema_tree_before = _schema_tree_sha256()
    source_path = pathlib.Path(__file__).absolute().resolve()
    source_before = _stable_measure(source_path, "SOURCE_PROVENANCE")["sha256"]
    attempts = _list_attempts(job_path)
    rows = []
    previous_id = None
    previous_start = None
    previous_terminal_utc = None
    prior_disposition = None
    open_attempt = None
    selected = None
    canonical_records = 0
    journal_records = 0
    output_files = 0
    for index, (number, attempt_path) in enumerate(attempts):
        if prior_disposition not in (None, "recoveryAuthorized"):
            reject("ATTEMPT_ORDER", "attempt follows success or exhaustion")
        _validate_attempt_entries(attempt_path)
        start, start_raw = _read_canonical_json(
            attempt_path / "attempt-start.json",
            "attempt-record.schema.json",
            "START_BINDING",
        )
        canonical_records += 1
        if start["recordType"] != "attemptStart":
            reject("START_BINDING", "attempt-start has the wrong record type")
        expected_id = f"{job_id}-attempt-{number:02d}"
        if (
            start["jobId"] != job_id
            or start["attemptId"] != expected_id
            or start["attemptNumber"] != number
            or start["maximumAttempts"] != MAXIMUM_ATTEMPTS
            or start["previousAttemptId"] != previous_id
        ):
            reject("START_BINDING", "attempt start cross-binding failed")
        if previous_start is not None:
            immutable = (
                "freezeId",
                "freezeReceiptSha256",
                "jobBindingSha256",
                "commandSha256",
            )
            if any(start[field] != previous_start[field] for field in immutable):
                reject("START_BINDING", "recovery changed immutable job binding")
            if parse_utc(start["startedUtc"]) < parse_utc(previous_terminal_utc):
                reject("TIMESTAMP_ORDER", "recovery predates prior terminal")
        journal = _verify_journal(attempt_path / start["processLogRelativePath"])
        _verify_journal_start(journal["start"], start, start_raw)
        journal_records += journal["report"]["recordCount"]
        log_inventory = _file_inventory(
            attempt_path / start["processLogRelativePath"]
        )
        if log_inventory != journal["inventory"]:
            reject("INVENTORY_MISMATCH", "journal changed after streaming")
        output_inventory, file_count = _directory_inventory(
            attempt_path / start["outputRelativePath"]
        )
        output_files += file_count
        terminal_path = attempt_path / "attempt-terminal.json"
        terminal = None
        terminal_raw = None
        if terminal_path.exists():
            terminal, terminal_raw = _read_canonical_json(
                terminal_path,
                "attempt-record.schema.json",
                "TERMINAL_BINDING",
            )
            canonical_records += 1
            if terminal["recordType"] != "attemptTerminal":
                reject("TERMINAL_BINDING", "terminal has the wrong record type")
            _verify_terminal(
                terminal,
                start,
                start_raw,
                attempt_path,
                journal,
                log_inventory,
                output_inventory,
            )
        recovery_path = attempt_path / "recovery-receipt.json"
        recovery = None
        recovery_raw = None
        if recovery_path.exists():
            recovery, recovery_raw = _read_canonical_json(
                recovery_path,
                "recovery-receipt.schema.json",
                "RECOVERY_BINDING",
            )
            canonical_records += 1
            _verify_recovery_receipt(
                recovery,
                recovery_raw,
                start,
                start_raw,
                terminal,
                journal,
                log_inventory,
            )
        if terminal is not None:
            recovery_classification = terminal["exitClassification"] in (
                "launcherRecoveredOrphanedStart",
                "processTreeUncertain",
            )
            if recovery_classification != (recovery is not None):
                reject("RECOVERY_BINDING", "recovery evidence presence differs")
            verification_status = terminal["bundleVerification"]["status"]
            disposition = terminal["retryDisposition"]
            prior_disposition = disposition
            previous_terminal_utc = terminal["terminalUtc"]
            if verification_status == "pass":
                if selected is not None:
                    reject("TERMINAL_SEMANTICS", "multiple valid attempts exist")
                selected = start["attemptId"]
        else:
            if index != len(attempts) - 1:
                reject("ATTEMPT_ORDER", "later attempt follows an open attempt")
            open_attempt = number
            prior_disposition = "open"
            verification_status = "open"
            disposition = "open"
        rows.append(
            {
                "attemptNumber": number,
                "attemptId": start["attemptId"],
                "startReceiptSha256": sha256_bytes(start_raw),
                "terminalReceiptSha256": (
                    sha256_bytes(terminal_raw) if terminal_raw is not None else None
                ),
                "recoveryReceiptSha256": (
                    sha256_bytes(recovery_raw) if recovery_raw is not None else None
                ),
                "bundleVerificationStatus": verification_status,
                "retryDisposition": disposition,
                "journal": journal["report"],
            }
        )
        previous_id = start["attemptId"]
        previous_start = start
    if not rows:
        state = "readyInitial"
    elif open_attempt is not None:
        state = "attemptOpen"
    elif prior_disposition == "recoveryAuthorized":
        state = "recoveryAuthorized"
    elif prior_disposition == "terminalSuccess":
        state = "succeeded"
    else:
        state = "exhausted"
    proposed_utc = verified_utc or utc_now()
    parse_utc(proposed_utc, "REPORT_TIME")
    report = {
        "schemaVersion": "1.0.0",
        "schemaId": REPORT_SCHEMA_ID,
        "reportType": REPORT_TYPE,
        "verifierVersion": VERIFIER_VERSION,
        "status": "valid",
        "claimBoundary": CLAIM_BOUNDARY,
        "verifiedUtc": proposed_utc,
        "verifierSourceSha256": source_before,
        "schemaTreeSha256": schema_tree_before,
        "ledgerRootPathSha256": _path_sha256(root),
        "forbiddenRootPathSha256s": forbidden_hashes,
        "jobId": job_id,
        "ledgerState": state,
        "attemptCount": len(rows),
        "openAttemptNumber": open_attempt,
        "selectedValidAttemptId": selected,
        "attempts": rows,
        "checks": {
            "canonicalJsonRecords": canonical_records,
            "journalRecords": journal_records,
            "outputFilesHashed": output_files,
            "scientificOutcomeFieldsRead": False,
        },
    }
    _validate(
        report,
        "independent-verification-report.schema.json",
        "REPORT_SCHEMA",
    )
    if _schema_tree_sha256() != schema_tree_before:
        reject("SCHEMA_SOURCE", "schema tree changed during verification")
    if _stable_measure(source_path, "SOURCE_PROVENANCE")["sha256"] != source_before:
        reject("SOURCE_PROVENANCE", "verifier changed during verification")
    return report


def build_parser():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--ledger-root", type=pathlib.Path, required=True)
    parser.add_argument("--job-id", required=True)
    parser.add_argument(
        "--forbidden-root",
        type=pathlib.Path,
        action="append",
        required=True,
    )
    parser.add_argument("--verified-utc")
    return parser


def main(argv=None):
    arguments = build_parser().parse_args(argv)
    try:
        report = verify_ledger(
            arguments.ledger_root,
            arguments.job_id,
            arguments.forbidden_root,
            arguments.verified_utc,
        )
    except VerificationError as error:
        print(f"WP14R_INDEPENDENT_VERIFY_ERROR: {error}", file=sys.stderr)
        return 2
    sys.stdout.buffer.write(canonical(report) + b"\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
