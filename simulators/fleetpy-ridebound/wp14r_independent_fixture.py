#!/usr/bin/env python3
"""Build two source-bound synthetic fixtures for WP14R verification."""

from __future__ import annotations

import argparse
import datetime
import hashlib
import importlib.util
import json
import os
import pathlib
import stat
import sys

import jsonschema

sys.dont_write_bytecode = True

BUILDER_VERSION = "wp14r-independent-fixture-builder-v1"
RECEIPT_SCHEMA_ID = (
    "https://ridebound.local/schemas/wp14r/v1/"
    "independent-fixture-receipt.schema.json"
)
RECEIPT_TYPE = "ridebound-wp14r-independent-fixture-receipt-v1"
CLAIM_BOUNDARY = [
    "mechanicalOnly",
    "syntheticChildOnly",
    "noScientificOutcome",
    "fixturesAreNotExperimentalUnits",
    "doesNotAuthorizeRecoveryOrFreezeV2",
    "doesNotSupersedeWp14V1",
]
CLEAN_JOB_ID = "wp14r-independent-clean-v1"
RECOVERY_JOB_ID = "wp14r-independent-recovery-v1"


class FixtureError(RuntimeError):
    """A fail-closed fixture construction error."""


def load_module(name, filename):
    path = pathlib.Path(__file__).resolve().with_name(filename)
    specification = importlib.util.spec_from_file_location(name, path)
    if specification is None or specification.loader is None:
        raise FixtureError(f"cannot load fixture dependency: {filename}")
    module = importlib.util.module_from_spec(specification)
    specification.loader.exec_module(module)
    return module


def stable_hash(path):
    path = pathlib.Path(path)
    junction_test = getattr(os.path, "isjunction", None)
    is_link = path.is_symlink() or bool(junction_test and junction_test(path))
    try:
        attributes = getattr(path.lstat(), "st_file_attributes", 0)
    except OSError as error:
        raise FixtureError(f"cannot stat source file: {path}") from error
    reparse = getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400)
    if not path.is_file() or is_link or attributes & reparse:
        raise FixtureError(f"not a regular source file: {path}")
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
        raise FixtureError(f"source changed while hashed: {path}")
    return digest.hexdigest()


def job_tree_sha256(path, verifier):
    entries = []
    for candidate in sorted(path.rglob("*"), key=lambda item: item.as_posix()):
        if verifier._is_link_or_junction(candidate):
            raise FixtureError(f"fixture contains a link: {candidate}")
        relative = candidate.relative_to(path).as_posix()
        if candidate.is_dir():
            entries.append({"path": relative, "type": "directory"})
        elif candidate.is_file():
            entries.append(
                {
                    "path": relative,
                    "type": "file",
                    "bytes": candidate.stat().st_size,
                    "sha256": stable_hash(candidate),
                }
            )
        else:
            raise FixtureError(f"fixture contains a non-file: {candidate}")
    return verifier.sha256_bytes(verifier.canonical(entries))


def plus_seconds(value, seconds, verifier):
    parsed = verifier.parse_utc(value, "REPORT_TIME")
    return (
        (parsed + datetime.timedelta(seconds=seconds))
        .isoformat(timespec="microseconds")
        .replace("+00:00", "Z")
    )


def find_absent_process_id(recovery):
    for candidate in range(4294967294, 4294967194, -1):
        if not recovery.process_exists(candidate):
            return candidate
    raise FixtureError("cannot find an absent process identifier")


def begin(
    ledger,
    supervisor,
    ledger_root,
    job_id,
    arguments,
    work,
    environment_names,
    forbidden_roots,
    started_utc,
):
    metadata, _ = supervisor.build_command_binding(
        sys.executable,
        arguments,
        work,
        environment_names,
        os.environ,
    )
    start = ledger.begin_attempt(
        ledger_root,
        job_id,
        "wp14r-independent-fixture-freeze-v1",
        "a" * 64,
        "b" * 64,
        metadata["commandSha256"],
        forbidden_roots,
        started_utc,
    )
    return metadata, start


def build_clean(
    ledger,
    supervisor,
    ledger_root,
    work,
    environment_names,
    forbidden_roots,
    created_utc,
):
    arguments = [
        "-B",
        "-c",
        "import sys;sys.stdout.buffer.write(b'wp14r-mechanics-only')",
    ]
    begin(
        ledger,
        supervisor,
        ledger_root,
        CLEAN_JOB_ID,
        arguments,
        work,
        environment_names,
        forbidden_roots,
        created_utc,
    )
    supervisor.supervise_process(
        ledger_root,
        CLEAN_JOB_ID,
        1,
        forbidden_roots,
        sys.executable,
        arguments,
        work,
        environment_names,
        wall_timeout_ms=10000,
        heartbeat_interval_ms=50,
        maximum_stream_bytes=65536,
        chunk_bytes=16,
        tree_exit_grace_ms=1000,
    )


def build_recovery(
    ledger,
    supervisor,
    recovery,
    verifier,
    ledger_root,
    work,
    environment_names,
    forbidden_roots,
    created_utc,
):
    arguments = ["-B", "-c", "raise SystemExit('must-not-launch')"]
    metadata, start = begin(
        ledger,
        supervisor,
        ledger_root,
        RECOVERY_JOB_ID,
        arguments,
        work,
        environment_names,
        forbidden_roots,
        created_utc,
    )
    attempt = ledger_root / RECOVERY_JOB_ID / "attempt-01"
    writer = supervisor.JournalWriter(attempt / "process.log")
    platform_name, fingerprint = supervisor.host_fingerprint()
    absent_process_id = find_absent_process_id(recovery)
    writer.append(
        "supervisorStart",
        {
            "jobId": RECOVERY_JOB_ID,
            "attemptId": start["attemptId"],
            "attemptNumber": 1,
            "startReceiptSha256": stable_hash(attempt / "attempt-start.json"),
            **metadata,
            "supervisorSha256": stable_hash(supervisor.__file__),
            "supervisorProcessId": absent_process_id,
            **supervisor.current_schema_provenance(),
            "hostFingerprintSha256": fingerprint,
            "platform": platform_name,
            "policy": {
                "wallTimeoutMs": 10000,
                "heartbeatIntervalMs": 50,
                "maximumStreamBytes": 65536,
                "chunkBytes": 16,
                "treeExitGraceMs": 1000,
            },
        },
    )
    writer.close()
    recovery.recover_open_attempt(
        ledger_root,
        RECOVERY_JOB_ID,
        1,
        forbidden_roots,
        absent_process_id,
        tree_probe_grace_ms=10,
        observed_utc=plus_seconds(created_utc, 1, verifier),
    )


def write_exclusive(path, content):
    flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL
    flags |= getattr(os, "O_BINARY", 0)
    descriptor = os.open(path, flags, 0o600)
    try:
        offset = 0
        while offset < len(content):
            written = os.write(descriptor, content[offset:])
            if written <= 0:
                raise FixtureError("receipt write made no progress")
            offset += written
        os.fsync(descriptor)
    finally:
        os.close(descriptor)


def build_fixtures(output_root, forbidden_roots, created_utc):
    ledger = load_module("wp14r_fixture_ledger", "wp14r_attempt_ledger.py")
    supervisor = load_module(
        "wp14r_fixture_supervisor", "wp14r_supervised_process.py"
    )
    recovery = load_module(
        "wp14r_fixture_recovery", "wp14r_stale_open_recovery.py"
    )
    verifier = load_module(
        "wp14r_fixture_independent_verifier", "wp14r_independent_verify.py"
    )
    verifier.parse_utc(created_utc, "REPORT_TIME")
    output = pathlib.Path(output_root).absolute()
    if output.exists():
        raise FixtureError("output root already exists")
    output, _, _ = verifier._validate_paths(output, forbidden_roots)
    output.mkdir(parents=True, exist_ok=False)
    ledger_root = output / "ledger"
    work = output / "work"
    work.mkdir()
    environment_names = ["SystemRoot"] if os.name == "nt" else []
    source_paths = {
        "builder": pathlib.Path(__file__).absolute().resolve(),
        "attemptLedger": pathlib.Path(ledger.__file__).absolute().resolve(),
        "supervisor": pathlib.Path(supervisor.__file__).absolute().resolve(),
        "recovery": pathlib.Path(recovery.__file__).absolute().resolve(),
        "independentVerifier": pathlib.Path(verifier.__file__).absolute().resolve(),
    }
    source_hashes = {
        name: stable_hash(path) for name, path in source_paths.items()
    }
    schema_tree_hash = verifier._schema_tree_sha256()
    python_hash = stable_hash(sys.executable)
    build_clean(
        ledger,
        supervisor,
        ledger_root,
        work,
        environment_names,
        forbidden_roots,
        created_utc,
    )
    build_recovery(
        ledger,
        supervisor,
        recovery,
        verifier,
        ledger_root,
        work,
        environment_names,
        forbidden_roots,
        created_utc,
    )
    jobs = []
    definitions = (
        (CLEAN_JOB_ID, "cleanCompleteOpen", "attemptOpen", "validComplete"),
        (
            RECOVERY_JOB_ID,
            "partialRecoveryTerminal",
            "recoveryAuthorized",
            "validPartial",
        ),
    )
    for job_id, role, state, journal_status in definitions:
        report = verifier.verify_ledger(
            ledger_root,
            job_id,
            forbidden_roots,
            plus_seconds(created_utc, 2, verifier),
        )
        observed_journal = report["attempts"][0]["journal"]
        if (
            report["ledgerState"] != state
            or observed_journal["status"] != journal_status
            or observed_journal["schemaProvenance"] != "bound"
        ):
            raise FixtureError(f"fixture cross-check failed: {job_id}")
        jobs.append(
            {
                "jobId": job_id,
                "role": role,
                "expectedLedgerState": state,
                "observedLedgerState": report["ledgerState"],
                "journalStatus": observed_journal["status"],
                "journalSchemaProvenance": observed_journal[
                    "schemaProvenance"
                ],
                "jobTreeSha256": job_tree_sha256(
                    ledger_root / job_id, verifier
                ),
            }
        )
    receipt = {
        "schemaVersion": "1.0.0",
        "schemaId": RECEIPT_SCHEMA_ID,
        "receiptType": RECEIPT_TYPE,
        "builderVersion": BUILDER_VERSION,
        "status": "valid",
        "claimBoundary": CLAIM_BOUNDARY,
        "createdUtc": created_utc,
        "rootPathSha256": verifier._path_sha256(output),
        "sourceHashes": source_hashes,
        "schemaTreeSha256": schema_tree_hash,
        "pythonExecutableSha256": python_hash,
        "jobs": jobs,
        "scientificOutcomeFieldsRead": False,
    }
    schema = verifier._load_schema("independent-fixture-receipt.schema.json")
    try:
        jsonschema.Draft202012Validator(schema).validate(receipt)
    except jsonschema.ValidationError as error:
        raise FixtureError(f"fixture receipt schema failed: {error.message}")
    if any(
        stable_hash(path) != source_hashes[name]
        for name, path in source_paths.items()
    ):
        raise FixtureError("fixture source changed during construction")
    if verifier._schema_tree_sha256() != schema_tree_hash:
        raise FixtureError("schema tree changed during construction")
    if stable_hash(sys.executable) != python_hash:
        raise FixtureError("Python executable changed during construction")
    write_exclusive(
        output / "independent-fixture-receipt.json",
        verifier.canonical(receipt),
    )
    return receipt


def build_parser():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output-root", type=pathlib.Path, required=True)
    parser.add_argument(
        "--forbidden-root",
        type=pathlib.Path,
        action="append",
        required=True,
    )
    parser.add_argument("--created-utc", required=True)
    return parser


def main(argv=None):
    arguments = build_parser().parse_args(argv)
    try:
        receipt = build_fixtures(
            arguments.output_root,
            arguments.forbidden_root,
            arguments.created_utc,
        )
    except (FixtureError, OSError, ValueError) as error:
        print(f"WP14R_FIXTURE_ERROR: {error}", file=sys.stderr)
        return 2
    verifier = load_module(
        "wp14r_fixture_output_verifier", "wp14r_independent_verify.py"
    )
    sys.stdout.buffer.write(verifier.canonical(receipt) + b"\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
