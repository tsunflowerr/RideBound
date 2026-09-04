#!/usr/bin/env python3
"""Create and verify immutable WP14R attempt-ledger records."""

from __future__ import annotations

import argparse
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

RECORD_SCHEMA_ID = (
    "https://ridebound.local/schemas/wp14r/v1/attempt-record.schema.json"
)
INSPECTION_SCHEMA_ID = (
    "https://ridebound.local/schemas/wp14r/v1/ledger-inspection.schema.json"
)
RECOVERY_SCHEMA_ID = (
    "https://ridebound.local/schemas/wp14r/v1/"
    "recovery-receipt.schema.json"
)
LEDGER_VERSION = "wp14r-attempt-ledger-v1"
MAXIMUM_ATTEMPTS = 2
ATTEMPT_PATTERN = re.compile(r"^attempt-(0[12])$")
JOB_PATTERN = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$")
SHA256_PATTERN = re.compile(r"^[0-9a-f]{64}$")
IDENTIFIER_PATTERN = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$")
UTC_PATTERN = re.compile(
    r"^[0-9]{4}-[0-9]{2}-[0-9]{2}T"
    r"[0-9]{2}:[0-9]{2}:[0-9]{2}(\.[0-9]{1,6})?Z$"
)
CLAIM_BOUNDARY = [
    "mechanicalOnly",
    "attemptsNotExperimentalUnits",
    "noScientificOutcomeAuthorization",
    "doesNotSupersedeWp14V1",
]


class LedgerError(RuntimeError):
    """A fail-closed WP14R ledger condition."""


def canonical(document):
    """Return the canonical UTF-8 representation used by this ledger."""
    return json.dumps(
        document,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")


def sha256_bytes(content):
    return hashlib.sha256(content).hexdigest()


def sha256_file(path):
    return measure_file(path)["sha256"]


def measure_file(path):
    if not path.is_file() or is_link_or_junction(path):
        raise LedgerError(f"expected a regular file: {path}")
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
        raise LedgerError(f"file changed while it was inventoried: {path}")
    return {"bytes": length, "sha256": digest.hexdigest()}


def write_exclusive(path, content):
    path.parent.mkdir(parents=True, exist_ok=True)
    flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL
    flags |= getattr(os, "O_BINARY", 0)
    descriptor = os.open(path, flags, 0o600)
    try:
        offset = 0
        while offset < len(content):
            written = os.write(descriptor, content[offset:])
            if written <= 0:
                raise LedgerError(f"exclusive write made no progress: {path}")
            offset += written
        os.fsync(descriptor)
    finally:
        os.close(descriptor)


def is_link_or_junction(path):
    junction_test = getattr(os.path, "isjunction", None)
    if path.is_symlink() or bool(junction_test and junction_test(path)):
        return True
    try:
        attributes = getattr(path.lstat(), "st_file_attributes", 0)
    except OSError:
        return False
    reparse = getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400)
    return bool(attributes & reparse)


def overlaps(first, second):
    return first == second or first in second.parents or second in first.parents


def require_sha256(value, label):
    if not SHA256_PATTERN.fullmatch(value):
        raise LedgerError(f"{label} must be a lowercase SHA-256")


def normalize_utc(value, label):
    if not UTC_PATTERN.fullmatch(value):
        raise LedgerError(f"{label} must be canonical UTC with a Z suffix")
    try:
        parsed = datetime.datetime.fromisoformat(value[:-1] + "+00:00")
    except ValueError as error:
        raise LedgerError(f"{label} is not a valid timestamp") from error
    if parsed.utcoffset() != datetime.timedelta(0):
        raise LedgerError(f"{label} is not UTC")
    return value


def parse_utc(value):
    normalize_utc(value, "timestamp")
    return datetime.datetime.fromisoformat(value[:-1] + "+00:00")


def utc_now():
    return (
        datetime.datetime.now(datetime.timezone.utc)
        .isoformat(timespec="microseconds")
        .replace("+00:00", "Z")
    )


def repository_root():
    return pathlib.Path(__file__).resolve().parents[2]


def load_schema(name):
    path = repository_root() / "benchmarks/schemas/wp14r/v1" / name
    try:
        document = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        raise LedgerError(f"cannot load schema: {path}") from error
    try:
        jsonschema.Draft202012Validator.check_schema(document)
    except jsonschema.SchemaError as error:
        raise LedgerError(f"invalid schema: {path}: {error.message}") from error
    return document


def validate_record(document):
    try:
        jsonschema.Draft202012Validator(
            load_schema("attempt-record.schema.json")
        ).validate(document)
    except jsonschema.ValidationError as error:
        raise LedgerError(f"attempt record schema failed: {error.message}") from error


def validate_inspection(document):
    try:
        jsonschema.Draft202012Validator(
            load_schema("ledger-inspection.schema.json")
        ).validate(document)
    except jsonschema.ValidationError as error:
        raise LedgerError(
            f"ledger inspection schema failed: {error.message}"
        ) from error


def validate_recovery_receipt(document):
    try:
        jsonschema.Draft202012Validator(
            load_schema("recovery-receipt.schema.json")
        ).validate(document)
    except jsonschema.ValidationError as error:
        raise LedgerError(
            f"recovery receipt schema failed: {error.message}"
        ) from error


def validate_job_id(job_id):
    if not JOB_PATTERN.fullmatch(job_id):
        raise LedgerError("jobId is not a safe canonical identifier")


def validate_ledger_root(ledger_root, forbidden_roots):
    raw_root = pathlib.Path(ledger_root).absolute()
    current = raw_root
    while True:
        if current.exists() and is_link_or_junction(current):
            raise LedgerError("ledger root ancestry cannot contain links or junctions")
        if current.parent == current:
            break
        current = current.parent
    ledger_root = raw_root.resolve()
    if not forbidden_roots:
        raise LedgerError("at least one forbidden root is required")
    normalized = [pathlib.Path(root).resolve() for root in forbidden_roots]
    if any(overlaps(ledger_root, root) for root in normalized):
        raise LedgerError("ledger root overlaps a forbidden raw root")
    return ledger_root


def job_root(ledger_root, job_id, forbidden_roots):
    validate_job_id(job_id)
    root = validate_ledger_root(ledger_root, forbidden_roots)
    raw_path = root / job_id
    if raw_path.exists() and is_link_or_junction(raw_path):
        raise LedgerError("job root cannot be a link or junction")
    path = raw_path.resolve()
    try:
        path.relative_to(root)
    except ValueError as error:
        raise LedgerError("job path escapes the ledger root") from error
    if path.exists() and is_link_or_junction(path):
        raise LedgerError("job root cannot be a link or junction")
    return path


def read_canonical_json(path):
    if not path.is_file() or is_link_or_junction(path):
        raise LedgerError(f"canonical record is not a regular file: {path}")
    try:
        before = path.stat()
        raw = path.read_bytes()
        after = path.stat()
        document = json.loads(raw.decode("utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise LedgerError(f"cannot read canonical record: {path}") from error
    if (
        len(raw) != before.st_size
        or after.st_size != before.st_size
        or after.st_mtime_ns != before.st_mtime_ns
    ):
        raise LedgerError(f"canonical record changed while read: {path}")
    if raw != canonical(document):
        raise LedgerError(f"record is not byte-canonical: {path}")
    validate_record(document)
    return document, raw


def read_canonical_recovery_receipt(path):
    if not path.is_file() or is_link_or_junction(path):
        raise LedgerError(f"recovery receipt is not a regular file: {path}")
    try:
        before = path.stat()
        raw = path.read_bytes()
        after = path.stat()
        document = json.loads(raw.decode("utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise LedgerError(f"cannot read recovery receipt: {path}") from error
    if (
        len(raw) != before.st_size
        or after.st_size != before.st_size
        or after.st_mtime_ns != before.st_mtime_ns
    ):
        raise LedgerError(f"recovery receipt changed while read: {path}")
    if raw != canonical(document):
        raise LedgerError(f"recovery receipt is not byte-canonical: {path}")
    validate_recovery_receipt(document)
    return document, raw


def file_inventory(path):
    if not path.exists():
        return {"exists": False, "bytes": 0, "sha256": None}
    if not path.is_file() or is_link_or_junction(path):
        raise LedgerError(f"process log is not a regular file: {path}")
    measurement = measure_file(path)
    return {
        "exists": True,
        "bytes": measurement["bytes"],
        "sha256": measurement["sha256"],
    }


def directory_inventory(path):
    empty_tree_hash = sha256_bytes(canonical([]))
    if not path.exists():
        return {
            "exists": False,
            "fileCount": 0,
            "bytes": 0,
            "treeSha256": empty_tree_hash,
        }
    if not path.is_dir() or is_link_or_junction(path):
        raise LedgerError(f"output is not a regular directory: {path}")
    candidates = sorted(path.rglob("*"), key=lambda item: item.as_posix())
    signature = []
    entries = []
    for candidate in candidates:
        if is_link_or_junction(candidate):
            raise LedgerError(f"output contains a link or junction: {candidate}")
        if candidate.is_dir():
            signature.append((candidate.relative_to(path).as_posix(), "directory"))
            continue
        if not candidate.is_file():
            raise LedgerError(f"output contains a non-file: {candidate}")
        relative = candidate.relative_to(path).as_posix()
        measurement = measure_file(candidate)
        signature.append((relative, "file"))
        entries.append(
            {
                "path": relative,
                "bytes": measurement["bytes"],
                "sha256": measurement["sha256"],
            }
        )
    after_candidates = sorted(
        path.rglob("*"), key=lambda item: item.as_posix()
    )
    after_signature = []
    for candidate in after_candidates:
        if is_link_or_junction(candidate):
            raise LedgerError(f"output contains a link or junction: {candidate}")
        kind = "directory" if candidate.is_dir() else "file"
        if kind == "file" and not candidate.is_file():
            raise LedgerError(f"output contains a non-file: {candidate}")
        after_signature.append((candidate.relative_to(path).as_posix(), kind))
    if after_signature != signature:
        raise LedgerError("output changed while it was inventoried")
    return {
        "exists": True,
        "fileCount": len(entries),
        "bytes": sum(entry["bytes"] for entry in entries),
        "treeSha256": sha256_bytes(canonical(entries)),
    }


def attempt_id(job_id, attempt_number):
    return f"{job_id}-attempt-{attempt_number:02d}"


def list_attempt_directories(path):
    if not path.exists():
        return []
    if not path.is_dir() or is_link_or_junction(path):
        raise LedgerError("job root is not a regular directory")
    attempts = []
    for child in path.iterdir():
        match = ATTEMPT_PATTERN.fullmatch(child.name)
        if not match:
            raise LedgerError(f"unexpected ledger entry: {child.name}")
        if not child.is_dir() or is_link_or_junction(child):
            raise LedgerError(f"attempt path is not a regular directory: {child}")
        attempts.append((int(match.group(1)), child))
    attempts.sort(key=lambda item: item[0])
    numbers = [number for number, _ in attempts]
    if numbers != list(range(1, len(numbers) + 1)):
        raise LedgerError("attempt directories contain a gap")
    if len(numbers) > MAXIMUM_ATTEMPTS:
        raise LedgerError("ledger contains more than two attempts")
    return attempts


def validate_attempt_contents(attempt_path):
    allowed = {
        "attempt-start.json",
        "attempt-terminal.json",
        "process.log",
        "recovery-receipt.json",
        "output",
    }
    unexpected = sorted(
        child.name for child in attempt_path.iterdir() if child.name not in allowed
    )
    if unexpected:
        raise LedgerError(
            f"attempt contains unexpected entries: {', '.join(unexpected)}"
        )
    output = attempt_path / "output"
    if output.exists() and (not output.is_dir() or is_link_or_junction(output)):
        raise LedgerError("attempt output is not a regular directory")
    log = attempt_path / "process.log"
    if log.exists() and (not log.is_file() or is_link_or_junction(log)):
        raise LedgerError("attempt process log is not a regular file")


def verify_start_binding(start, job_id, number, previous_id):
    expected_id = attempt_id(job_id, number)
    if (
        start["jobId"] != job_id
        or start["attemptId"] != expected_id
        or start["attemptNumber"] != number
        or start["maximumAttempts"] != MAXIMUM_ATTEMPTS
        or start["previousAttemptId"] != previous_id
    ):
        raise LedgerError(f"attempt start cross-binding failed: {expected_id}")


def expected_disposition(
    number,
    verification_status,
    exit_classification=None,
    process_tree_status=None,
):
    if verification_status == "pass":
        return "terminalSuccess"
    if (
        exit_classification == "processTreeUncertain"
        or process_tree_status == "uncertain"
    ):
        return "attemptsExhausted"
    if number < MAXIMUM_ATTEMPTS:
        return "recoveryAuthorized"
    return "attemptsExhausted"


def verify_terminal_binding(terminal, start, start_raw, attempt_path):
    number = start["attemptNumber"]
    if (
        terminal["jobId"] != start["jobId"]
        or terminal["attemptId"] != start["attemptId"]
        or terminal["attemptNumber"] != number
        or terminal["startReceiptSha256"] != sha256_bytes(start_raw)
    ):
        raise LedgerError(
            f"attempt terminal cross-binding failed: {start['attemptId']}"
        )
    if parse_utc(terminal["terminalUtc"]) < parse_utc(start["startedUtc"]):
        raise LedgerError("terminal timestamp precedes attempt start")
    verification = terminal["bundleVerification"]
    status = verification["status"]
    classification = terminal["exitClassification"]
    exit_code = terminal["processExitCode"]
    tree_status = terminal["processTreeStatus"]
    expected = expected_disposition(
        number, status, classification, tree_status
    )
    if terminal["retryDisposition"] != expected:
        raise LedgerError("terminal retry disposition is inconsistent")
    if status == "pass":
        if classification != "success":
            raise LedgerError("verified pass requires success classification")
        if exit_code != 0 or tree_status != "exitedCleanly":
            raise LedgerError("verified pass requires clean process exit zero")
        manifest = attempt_path / "output/bundle-manifest.json"
        if not manifest.is_file():
            raise LedgerError("verified pass lacks bundle-manifest.json")
        if any(
            verification[field] is None
            for field in (
                "verifierId",
                "verifierSha256",
                "bundleManifestSha256",
                "behavioralHash",
            )
        ):
            raise LedgerError("verified pass lacks independent evidence")
        if sha256_file(manifest) != verification["bundleManifestSha256"]:
            raise LedgerError("bundle manifest changed after verification")
    elif classification == "success":
        raise LedgerError("success classification requires verified pass")
    if classification == "processExitFailure" and (
        exit_code is None or exit_code == 0
    ):
        raise LedgerError("process exit failure requires a non-zero exit code")
    if classification == "processTreeUncertain" and tree_status != "uncertain":
        raise LedgerError("process-tree uncertainty must remain explicit")
    if classification == "launcherRecoveredOrphanedStart":
        if exit_code is not None:
            raise LedgerError("an orphaned start cannot assert a process exit code")
        if tree_status == "uncertain":
            raise LedgerError("orphan recovery requires a proven-safe process tree")
    verifier_fields = (
        verification["verifierId"],
        verification["verifierSha256"],
        verification["behavioralHash"],
    )
    if status == "notRun" and any(value is not None for value in verifier_fields):
        raise LedgerError("notRun cannot claim verifier or behavioral evidence")
    if status == "fail" and any(value is None for value in verifier_fields[:2]):
        raise LedgerError("verifier failure requires verifier identity and hash")
    recovery_path = attempt_path / "recovery-receipt.json"
    recovery_hash = terminal["recoveryReceiptSha256"]
    recovery_classifications = {
        "launcherRecoveredOrphanedStart",
        "processTreeUncertain",
    }
    if classification in recovery_classifications:
        receipt, receipt_raw = read_canonical_recovery_receipt(recovery_path)
        actual_recovery_hash = sha256_bytes(receipt_raw)
        if recovery_hash != actual_recovery_hash:
            raise LedgerError("recovery receipt hash does not match its bytes")
        if (
            receipt["jobId"] != start["jobId"]
            or receipt["attemptId"] != start["attemptId"]
            or receipt["attemptNumber"] != number
            or receipt["startReceiptSha256"] != sha256_bytes(start_raw)
            or receipt["commandSha256"] != start["commandSha256"]
            or receipt["ledgerExitClassification"] != classification
            or receipt["treeStatus"] != tree_status
            or receipt["ledgerRetryDisposition"] != expected
            or receipt["retryAuthorized"]
            != (expected == "recoveryAuthorized")
            or receipt["processLogInventory"]
            != terminal["processLogInventory"]
            or receipt["treeSafe"]
            != (classification == "launcherRecoveredOrphanedStart")
        ):
            raise LedgerError("recovery receipt cross-binding failed")
    elif recovery_hash is not None or recovery_path.exists():
        raise LedgerError("non-recovery terminal cannot bind recovery evidence")
    log = file_inventory(attempt_path / start["processLogRelativePath"])
    output = directory_inventory(attempt_path / start["outputRelativePath"])
    if terminal["processLogInventory"] != log:
        raise LedgerError("process log changed after terminal receipt")
    if terminal["outputInventory"] != output:
        raise LedgerError("output changed after terminal receipt")


def inspect_ledger(ledger_root, job_id, forbidden_roots):
    path = job_root(ledger_root, job_id, forbidden_roots)
    attempts = list_attempt_directories(path)
    rows = []
    previous_id = None
    previous_start = None
    previous_terminal_utc = None
    prior_disposition = None
    open_attempt = None
    selected = None
    for index, (number, attempt_path) in enumerate(attempts):
        if prior_disposition not in (None, "recoveryAuthorized"):
            raise LedgerError("attempt exists after terminal success or exhaustion")
        validate_attempt_contents(attempt_path)
        start, start_raw = read_canonical_json(
            attempt_path / "attempt-start.json"
        )
        if start["recordType"] != "attemptStart":
            raise LedgerError("attempt-start.json has the wrong record type")
        verify_start_binding(start, job_id, number, previous_id)
        if previous_start is not None:
            immutable_fields = (
                "freezeId",
                "freezeReceiptSha256",
                "jobBindingSha256",
                "commandSha256",
            )
            if any(
                start[field] != previous_start[field]
                for field in immutable_fields
            ):
                raise LedgerError("recovery attempt changed its job binding")
            if parse_utc(start["startedUtc"]) < parse_utc(previous_terminal_utc):
                raise LedgerError("recovery attempt predates the prior terminal")
        terminal_path = attempt_path / "attempt-terminal.json"
        terminal_hash = None
        if not terminal_path.exists():
            if index != len(attempts) - 1:
                raise LedgerError("a later attempt exists after an open attempt")
            open_attempt = number
            prior_disposition = "open"
            verification_status = "open"
            disposition = "open"
        else:
            terminal, terminal_raw = read_canonical_json(terminal_path)
            if terminal["recordType"] != "attemptTerminal":
                raise LedgerError(
                    "attempt-terminal.json has the wrong record type"
                )
            verify_terminal_binding(terminal, start, start_raw, attempt_path)
            terminal_hash = sha256_bytes(terminal_raw)
            verification_status = terminal["bundleVerification"]["status"]
            disposition = terminal["retryDisposition"]
            prior_disposition = disposition
            previous_terminal_utc = terminal["terminalUtc"]
            if verification_status == "pass":
                if selected is not None:
                    raise LedgerError("ledger contains multiple valid attempts")
                selected = start["attemptId"]
        rows.append(
            {
                "attemptNumber": number,
                "attemptId": start["attemptId"],
                "startReceiptSha256": sha256_bytes(start_raw),
                "terminalReceiptSha256": terminal_hash,
                "terminalStatus": (
                    "open" if terminal_hash is None else "terminal"
                ),
                "bundleVerificationStatus": verification_status,
                "retryDisposition": disposition,
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
    report = {
        "schemaVersion": "1.0.0",
        "schemaId": INSPECTION_SCHEMA_ID,
        "reportType": "ridebound-wp14r-ledger-inspection-v1",
        "ledgerVersion": LEDGER_VERSION,
        "status": "valid",
        "claimBoundary": CLAIM_BOUNDARY,
        "jobId": job_id,
        "maximumAttempts": MAXIMUM_ATTEMPTS,
        "ledgerState": state,
        "attemptCount": len(rows),
        "openAttemptNumber": open_attempt,
        "selectedValidAttemptId": selected,
        "attempts": rows,
    }
    validate_inspection(report)
    return report


def begin_attempt(
    ledger_root,
    job_id,
    freeze_id,
    freeze_receipt_sha256,
    job_binding_sha256,
    command_sha256,
    forbidden_roots,
    started_utc=None,
):
    for value, label in (
        (freeze_receipt_sha256, "freeze receipt hash"),
        (job_binding_sha256, "job binding hash"),
        (command_sha256, "command hash"),
    ):
        require_sha256(value, label)
    if not isinstance(freeze_id, str) or not IDENTIFIER_PATTERN.fullmatch(freeze_id):
        raise LedgerError("freezeId must be a canonical identifier")
    report = inspect_ledger(ledger_root, job_id, forbidden_roots)
    if report["ledgerState"] not in ("readyInitial", "recoveryAuthorized"):
        raise LedgerError("ledger state does not authorize another attempt")
    number = report["attemptCount"] + 1
    if number > MAXIMUM_ATTEMPTS:
        raise LedgerError("maximum attempt count is exhausted")
    previous_id = (
        report["attempts"][-1]["attemptId"] if report["attempts"] else None
    )
    proposed_started_utc = normalize_utc(
        started_utc or utc_now(), "startedUtc"
    )
    if number == 2:
        root = job_root(ledger_root, job_id, forbidden_roots)
        prior_path = root / "attempt-01"
        prior, _ = read_canonical_json(prior_path / "attempt-start.json")
        proposed = {
            "freezeId": freeze_id,
            "freezeReceiptSha256": freeze_receipt_sha256,
            "jobBindingSha256": job_binding_sha256,
            "commandSha256": command_sha256,
        }
        if any(prior[field] != proposed[field] for field in proposed):
            raise LedgerError("recovery attempt must preserve the exact job binding")
        terminal, _ = read_canonical_json(
            prior_path / "attempt-terminal.json"
        )
        if parse_utc(proposed_started_utc) < parse_utc(terminal["terminalUtc"]):
            raise LedgerError("recovery attempt predates the prior terminal")
    identifier = attempt_id(job_id, number)
    document = {
        "schemaVersion": "1.0.0",
        "schemaId": RECORD_SCHEMA_ID,
        "recordType": "attemptStart",
        "ledgerVersion": LEDGER_VERSION,
        "jobId": job_id,
        "attemptId": identifier,
        "attemptNumber": number,
        "maximumAttempts": MAXIMUM_ATTEMPTS,
        "startedUtc": proposed_started_utc,
        "freezeId": freeze_id,
        "freezeReceiptSha256": freeze_receipt_sha256,
        "jobBindingSha256": job_binding_sha256,
        "commandSha256": command_sha256,
        "outputRelativePath": "output",
        "processLogRelativePath": "process.log",
        "previousAttemptId": previous_id,
        "retryPolicy": {
            "policyId": "one-initial-one-recovery-v1",
            "maximumAttempts": MAXIMUM_ATTEMPTS,
            "attemptsAreExperimentalUnits": False,
            "retainAllAttempts": True,
            "retryAfterValidBundle": False,
        },
        "outcomeAccessPolicy": {
            "policyId": "mechanical-validity-only-v1",
            "mayReadScientificOutcomeToAuthorizeRecovery": False,
            "recoveryBasis": "mechanicalValidityOnly",
        },
    }
    validate_record(document)
    root = job_root(ledger_root, job_id, forbidden_roots)
    root.mkdir(parents=True, exist_ok=True)
    attempt_path = root / f"attempt-{number:02d}"
    try:
        attempt_path.mkdir(exist_ok=False)
        write_exclusive(attempt_path / "attempt-start.json", canonical(document))
    except FileExistsError as error:
        raise LedgerError(
            "attempt directory or start receipt already exists"
        ) from error
    return document


def terminalize_attempt(
    ledger_root,
    job_id,
    attempt_number,
    exit_classification,
    elapsed_ms,
    process_tree_status,
    bundle_verification_status,
    forbidden_roots,
    process_exit_code=None,
    verifier_id=None,
    verifier_sha256=None,
    behavioral_hash=None,
    terminal_utc=None,
    recovery_receipt_sha256=None,
):
    if attempt_number not in (1, 2):
        raise LedgerError("attempt number must be one or two")
    if not isinstance(elapsed_ms, int) or elapsed_ms < 0:
        raise LedgerError("elapsedMs must be a non-negative integer")
    if process_exit_code is not None and (
        not isinstance(process_exit_code, int)
        or not -(2**31) <= process_exit_code <= 2**31 - 1
    ):
        raise LedgerError("process exit code is outside int32")
    if verifier_sha256 is not None:
        require_sha256(verifier_sha256, "verifier hash")
    if behavioral_hash is not None:
        require_sha256(behavioral_hash, "behavioral hash")
    if recovery_receipt_sha256 is not None:
        require_sha256(recovery_receipt_sha256, "recovery receipt hash")
    report = inspect_ledger(ledger_root, job_id, forbidden_roots)
    if (
        report["ledgerState"] != "attemptOpen"
        or report["openAttemptNumber"] != attempt_number
    ):
        raise LedgerError("only the current open attempt can be terminalized")
    root = job_root(ledger_root, job_id, forbidden_roots)
    attempt_path = root / f"attempt-{attempt_number:02d}"
    start, start_raw = read_canonical_json(attempt_path / "attempt-start.json")
    manifest = attempt_path / "output/bundle-manifest.json"
    manifest_hash = sha256_file(manifest) if manifest.exists() else None
    verification = {
        "status": bundle_verification_status,
        "verifierId": verifier_id,
        "verifierSha256": verifier_sha256,
        "bundleManifestSha256": manifest_hash,
        "behavioralHash": behavioral_hash,
    }
    disposition = expected_disposition(
        attempt_number,
        bundle_verification_status,
        exit_classification,
        process_tree_status,
    )
    document = {
        "schemaVersion": "1.0.0",
        "schemaId": RECORD_SCHEMA_ID,
        "recordType": "attemptTerminal",
        "ledgerVersion": LEDGER_VERSION,
        "jobId": job_id,
        "attemptId": start["attemptId"],
        "attemptNumber": attempt_number,
        "terminalUtc": normalize_utc(
            terminal_utc or utc_now(), "terminalUtc"
        ),
        "startReceiptSha256": sha256_bytes(start_raw),
        "exitClassification": exit_classification,
        "processExitCode": process_exit_code,
        "elapsedMs": elapsed_ms,
        "processTreeStatus": process_tree_status,
        "recoveryReceiptSha256": recovery_receipt_sha256,
        "processLogInventory": file_inventory(
            attempt_path / start["processLogRelativePath"]
        ),
        "outputInventory": directory_inventory(
            attempt_path / start["outputRelativePath"]
        ),
        "bundleVerification": verification,
        "retryDisposition": disposition,
    }
    validate_record(document)
    verify_terminal_binding(document, start, start_raw, attempt_path)
    try:
        write_exclusive(
            attempt_path / "attempt-terminal.json", canonical(document)
        )
    except FileExistsError as error:
        raise LedgerError("attempt terminal receipt already exists") from error
    return document


def add_common_arguments(parser):
    parser.add_argument("--ledger-root", type=pathlib.Path, required=True)
    parser.add_argument("--job-id", required=True)
    parser.add_argument(
        "--forbidden-root",
        action="append",
        type=pathlib.Path,
        required=True,
    )


def build_parser():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)

    start = commands.add_parser("start")
    add_common_arguments(start)
    start.add_argument("--freeze-id", required=True)
    start.add_argument("--freeze-receipt-sha256", required=True)
    start.add_argument("--job-binding-sha256", required=True)
    start.add_argument("--command-sha256", required=True)
    start.add_argument("--started-utc")

    terminal = commands.add_parser("terminalize")
    add_common_arguments(terminal)
    terminal.add_argument("--attempt-number", type=int, required=True)
    terminal.add_argument("--exit-classification", required=True)
    terminal.add_argument("--elapsed-ms", type=int, required=True)
    terminal.add_argument("--process-tree-status", required=True)
    terminal.add_argument("--bundle-verification-status", required=True)
    terminal.add_argument("--process-exit-code", type=int)
    terminal.add_argument("--verifier-id")
    terminal.add_argument("--verifier-sha256")
    terminal.add_argument("--behavioral-hash")
    terminal.add_argument("--terminal-utc")
    terminal.add_argument("--recovery-receipt-sha256")

    inspect = commands.add_parser("inspect")
    add_common_arguments(inspect)
    inspect.add_argument("--output", type=pathlib.Path)
    return parser


def execute(arguments):
    if arguments.command == "start":
        return begin_attempt(
            arguments.ledger_root,
            arguments.job_id,
            arguments.freeze_id,
            arguments.freeze_receipt_sha256,
            arguments.job_binding_sha256,
            arguments.command_sha256,
            arguments.forbidden_root,
            arguments.started_utc,
        )
    if arguments.command == "terminalize":
        return terminalize_attempt(
            arguments.ledger_root,
            arguments.job_id,
            arguments.attempt_number,
            arguments.exit_classification,
            arguments.elapsed_ms,
            arguments.process_tree_status,
            arguments.bundle_verification_status,
            arguments.forbidden_root,
            arguments.process_exit_code,
            arguments.verifier_id,
            arguments.verifier_sha256,
            arguments.behavioral_hash,
            arguments.terminal_utc,
            arguments.recovery_receipt_sha256,
        )
    return inspect_ledger(
        arguments.ledger_root,
        arguments.job_id,
        arguments.forbidden_root,
    )


def main(argv=None):
    parser = build_parser()
    arguments = parser.parse_args(argv)
    try:
        document = execute(arguments)
        content = canonical(document)
        if arguments.command == "inspect" and arguments.output:
            ledger_job = job_root(
                arguments.ledger_root,
                arguments.job_id,
                arguments.forbidden_root,
            )
            output = arguments.output.absolute().resolve()
            if output == ledger_job or ledger_job in output.parents:
                raise LedgerError("inspection output cannot be inside the job ledger")
            write_exclusive(arguments.output, content)
        else:
            sys.stdout.buffer.write(content + b"\n")
        return 0
    except (LedgerError, FileExistsError) as error:
        print(f"WP14R_LEDGER_ERROR: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
