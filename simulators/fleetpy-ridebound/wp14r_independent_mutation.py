#!/usr/bin/env python3
"""Run a retained mechanics-only mutation matrix against the WP14R verifier."""

from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import os
import pathlib
import shutil
import subprocess
import stat
import sys

import jsonschema

sys.dont_write_bytecode = True

TOOL_VERSION = "wp14r-independent-mutation-matrix-v1"
REPORT_SCHEMA_ID = (
    "https://ridebound.local/schemas/wp14r/v1/"
    "independent-mutation-report.schema.json"
)
REPORT_TYPE = "ridebound-wp14r-independent-mutation-report-v1"
CLAIM_BOUNDARY = [
    "mechanicalOnly",
    "mutationClassesAreNotExperimentalUnits",
    "noScientificOutcomeInterpretation",
    "doesNotAuthorizeRecoveryOrFreezeV2",
    "doesNotSupersedeWp14V1",
]


class MutationError(RuntimeError):
    """A fail-closed mutation-matrix construction error."""


def load_verifier():
    path = pathlib.Path(__file__).resolve().with_name(
        "wp14r_independent_verify.py"
    )
    specification = importlib.util.spec_from_file_location(
        "wp14r_independent_verifier_for_mutations", path
    )
    if specification is None or specification.loader is None:
        raise MutationError("cannot load independent verifier")
    module = importlib.util.module_from_spec(specification)
    specification.loader.exec_module(module)
    return module


def stable_measure(path):
    path = pathlib.Path(path)
    if not path.is_file() or is_link_or_junction(path):
        raise MutationError(f"not a regular file: {path}")
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
        raise MutationError(f"file changed while hashed: {path}")
    return {"bytes": length, "sha256": digest.hexdigest()}


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


def tree_sha256(path, verifier, allow_links=False):
    path = pathlib.Path(path)
    if not path.is_dir() or is_link_or_junction(path):
        raise MutationError(f"not a regular fixture directory: {path}")
    entries = []
    for candidate in sorted(path.rglob("*"), key=lambda item: item.as_posix()):
        relative = candidate.relative_to(path).as_posix()
        if is_link_or_junction(candidate):
            if not allow_links:
                raise MutationError(
                    f"fixture contains a link or junction: {candidate}"
                )
            entries.append(
                {
                    "path": relative,
                    "type": "linkOrJunction",
                    "targetPathSha256": verifier._path_sha256(
                        candidate.resolve()
                    ),
                }
            )
        elif candidate.is_dir():
            entries.append({"path": relative, "type": "directory"})
        elif candidate.is_file():
            measured = stable_measure(candidate)
            entries.append(
                {
                    "path": relative,
                    "type": "file",
                    "bytes": measured["bytes"],
                    "sha256": measured["sha256"],
                }
            )
        else:
            raise MutationError(f"fixture contains a non-file: {candidate}")
    return verifier.sha256_bytes(verifier.canonical(entries))


def overlaps(first, second):
    return first == second or first in second.parents or second in first.parents


def validate_output_root(output_root, source_roots, forbidden_roots):
    output = pathlib.Path(output_root).absolute()
    if output.exists():
        raise MutationError("output root already exists")
    resolved = output.resolve()
    protected = [
        pathlib.Path(path).absolute().resolve()
        for path in [*source_roots, *forbidden_roots]
    ]
    if any(overlaps(resolved, path) for path in protected):
        raise MutationError("output root overlaps a source or forbidden root")
    return resolved


def copy_fixture(source_root, job_id, destination, verifier):
    source_job = pathlib.Path(source_root).absolute().resolve() / job_id
    before = tree_sha256(source_job, verifier)
    ledger = destination / "ledger"
    ledger.mkdir(parents=True, exist_ok=False)
    shutil.copytree(source_job, ledger / job_id)
    after_source = tree_sha256(source_job, verifier)
    copied = tree_sha256(ledger / job_id, verifier)
    if before != after_source or copied != before:
        raise MutationError("source fixture changed or copy is not byte-exact")
    return ledger, before


def attempt_path(ledger, job_id):
    return ledger / job_id / "attempt-01"


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8"))


def read_records(path):
    return [json.loads(line) for line in path.read_bytes().splitlines()]


def reseal(records, verifier):
    previous = verifier.ZERO_SHA256
    lines = []
    for sequence, record in enumerate(records):
        record["sequence"] = sequence
        record["previousRecordSha256"] = previous
        projection = {
            key: value
            for key, value in record.items()
            if key != "recordSha256"
        }
        previous = verifier.sha256_bytes(verifier.canonical(projection))
        record["recordSha256"] = previous
        lines.append(verifier.canonical(record) + b"\n")
    return b"".join(lines)


def make_legacy(ledger, job_id, verifier):
    path = attempt_path(ledger, job_id) / "process.log"
    records = read_records(path)
    for field in verifier.PROVENANCE_FIELDS:
        records[0]["payload"].pop(field)
    path.write_bytes(reseal(records, verifier))


def create_directory_link(link, target):
    target.mkdir(parents=True, exist_ok=False)
    if os.name != "nt":
        os.symlink(target, link, target_is_directory=True)
        return
    script = (
        "New-Item -ItemType Junction -Path $env:RB_MUTATION_LINK "
        "-Target $env:RB_MUTATION_TARGET -ErrorAction Stop | Out-Null"
    )
    environment = os.environ.copy()
    environment["RB_MUTATION_LINK"] = str(link)
    environment["RB_MUTATION_TARGET"] = str(target)
    result = subprocess.run(
        [
            "powershell.exe",
            "-NoProfile",
            "-NonInteractive",
            "-Command",
            script,
        ],
        check=False,
        capture_output=True,
        env=environment,
        text=True,
        timeout=30,
    )
    if result.returncode != 0 or not is_link_or_junction(link):
        raise MutationError("cannot create retained junction mutant")


def mutate(case_id, ledger, job_id, verifier):
    attempt = attempt_path(ledger, job_id)
    journal = attempt / "process.log"
    if case_id == "M01-pathOverlap":
        return
    if case_id == "M15-pathLink":
        create_directory_link(attempt / "output", ledger.parent / "link-target")
        return
    if case_id == "M02-canonicalJson":
        path = attempt / "attempt-start.json"
        path.write_bytes(path.read_bytes() + b"\n")
    elif case_id == "M03-startBinding":
        path = attempt / "attempt-start.json"
        document = read_json(path)
        document["commandSha256"] = "e" * 64
        path.write_bytes(verifier.canonical(document))
    elif case_id == "M04-terminalBinding":
        path = attempt / "attempt-terminal.json"
        document = read_json(path)
        document["startReceiptSha256"] = "e" * 64
        path.write_bytes(verifier.canonical(document))
    elif case_id == "M05-recoveryBinding":
        path = attempt / "attempt-terminal.json"
        document = read_json(path)
        document["recoveryReceiptSha256"] = "f" * 64
        path.write_bytes(verifier.canonical(document))
    elif case_id == "M06-attemptGap":
        attempt.rename(ledger / job_id / "attempt-02")
    elif case_id == "M07-attemptExtra":
        (ledger / job_id / "attempt-03").mkdir()
    elif case_id == "M08-journalChain":
        records = read_records(journal)
        records[1]["previousRecordSha256"] = "f" * 64
        journal.write_bytes(
            b"".join(verifier.canonical(record) + b"\n" for record in records)
        )
    elif case_id == "M09-journalChunk":
        records = read_records(journal)
        record = next(
            item for item in records if item["recordType"] == "streamChunk"
        )
        record["payload"]["dataBase64"] = "eA=="
        journal.write_bytes(reseal(records, verifier))
    elif case_id == "M10-journalEof":
        records = read_records(journal)
        record = next(
            item
            for item in records
            if item["recordType"] == "streamEof"
            and item["payload"]["stream"] == "stdout"
        )
        record["payload"]["observedSha256"] = "f" * 64
        journal.write_bytes(reseal(records, verifier))
    elif case_id == "M11-journalState":
        records = read_records(journal)
        index = next(
            position
            for position, item in enumerate(records)
            if item["recordType"] == "launchIntent"
        )
        duplicate = json.loads(json.dumps(records[index]))
        records.insert(index + 1, duplicate)
        journal.write_bytes(reseal(records, verifier))
    elif case_id == "M12-journalTerminalAppend":
        journal.write_bytes(journal.read_bytes() + b'{"appended"')
    elif case_id == "M13-schemaProvenance":
        records = read_records(journal)
        records[0]["payload"]["logSchemaSha256"] = "f" * 64
        journal.write_bytes(reseal(records, verifier))
    elif case_id == "M14-sourceProvenance":
        records = read_records(journal)
        records[0]["payload"]["supervisorSha256"] = "f" * 64
        journal.write_bytes(reseal(records, verifier))
    else:
        raise MutationError(f"unknown mutation case: {case_id}")


def verify_valid_fixture(
    fixture_id,
    ledger,
    job_id,
    forbidden_roots,
    expected_state,
    expected_provenance,
    verifier,
    generated_utc,
):
    report = verifier.verify_ledger(
        ledger,
        job_id,
        forbidden_roots,
        generated_utc,
    )
    observed_provenance = report["attempts"][0]["journal"][
        "schemaProvenance"
    ]
    if (
        report["ledgerState"] != expected_state
        or observed_provenance != expected_provenance
    ):
        raise MutationError(f"valid fixture differs: {fixture_id}")
    return {
        "fixtureId": fixture_id,
        "expectedLedgerState": expected_state,
        "observedLedgerState": report["ledgerState"],
        "journalSchemaProvenance": observed_provenance,
        "valid": True,
    }


def run_case(
    definition,
    source,
    destination,
    forbidden_roots,
    verifier,
    generated_utc,
):
    case_id, mutation_class, fixture_id, expected_code = definition
    ledger, _ = copy_fixture(
        source[fixture_id]["root"],
        source[fixture_id]["jobId"],
        destination,
        verifier,
    )
    job_id = source[fixture_id]["jobId"]
    mutate(case_id, ledger, job_id, verifier)
    effective_forbidden = (
        [ledger] if case_id == "M01-pathOverlap" else forbidden_roots
    )
    observed = None
    try:
        verifier.verify_ledger(
            ledger,
            job_id,
            effective_forbidden,
            generated_utc,
        )
    except verifier.VerificationError as error:
        observed = error.code
    if observed != expected_code:
        raise MutationError(
            f"{case_id} expected {expected_code}, observed {observed}"
        )
    return {
        "caseId": case_id,
        "mutationClass": mutation_class,
        "baseFixtureId": fixture_id,
        "expectedRejectionCode": expected_code,
        "observedRejectionCode": observed,
        "caught": True,
        "retainedRelativeRoot": f"cases/{case_id}/ledger",
        "mutantTreeSha256": tree_sha256(
            destination,
            verifier,
            allow_links=case_id == "M15-pathLink",
        ),
    }


def write_exclusive(path, content):
    flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL
    flags |= getattr(os, "O_BINARY", 0)
    descriptor = os.open(path, flags, 0o600)
    try:
        offset = 0
        while offset < len(content):
            written = os.write(descriptor, content[offset:])
            if written <= 0:
                raise MutationError("report write made no progress")
            offset += written
        os.fsync(descriptor)
    finally:
        os.close(descriptor)


MUTATION_CASES = (
    ("M01-pathOverlap", "pathOverlap", "clean", "PATH_UNSAFE"),
    ("M02-canonicalJson", "canonicalJson", "clean", "CANONICAL_JSON"),
    ("M03-startBinding", "startBinding", "clean", "START_BINDING"),
    (
        "M04-terminalBinding",
        "terminalBinding",
        "recovery",
        "TERMINAL_BINDING",
    ),
    (
        "M05-recoveryBinding",
        "recoveryBinding",
        "recovery",
        "RECOVERY_BINDING",
    ),
    ("M06-attemptGap", "attemptGap", "clean", "ATTEMPT_SEQUENCE"),
    ("M07-attemptExtra", "attemptExtra", "clean", "ENTRY_UNEXPECTED"),
    ("M08-journalChain", "journalChain", "clean", "JOURNAL_CHAIN"),
    (
        "M09-journalChunk",
        "journalChunk",
        "clean",
        "JOURNAL_SEMANTICS",
    ),
    ("M10-journalEof", "journalEof", "clean", "JOURNAL_SEMANTICS"),
    (
        "M11-journalState",
        "journalState",
        "clean",
        "JOURNAL_SEMANTICS",
    ),
    (
        "M12-journalTerminalAppend",
        "journalTerminalAppend",
        "clean",
        "JOURNAL_FORMAT",
    ),
    (
        "M13-schemaProvenance",
        "schemaProvenance",
        "clean",
        "SCHEMA_PROVENANCE",
    ),
    (
        "M14-sourceProvenance",
        "sourceProvenance",
        "clean",
        "SOURCE_PROVENANCE",
    ),
    ("M15-pathLink", "pathLink", "clean", "PATH_UNSAFE"),
)


def run_matrix(
    clean_ledger_root,
    clean_job_id,
    recovery_ledger_root,
    recovery_job_id,
    forbidden_roots,
    output_root,
    generated_utc,
):
    verifier = load_verifier()
    verifier.parse_utc(generated_utc, "REPORT_TIME")
    output = validate_output_root(
        output_root,
        [clean_ledger_root, recovery_ledger_root],
        forbidden_roots,
    )
    source = {
        "clean": {
            "root": pathlib.Path(clean_ledger_root).absolute().resolve(),
            "jobId": clean_job_id,
            "role": "cleanCompleteOpen",
            "expectedState": "attemptOpen",
        },
        "recovery": {
            "root": pathlib.Path(recovery_ledger_root).absolute().resolve(),
            "jobId": recovery_job_id,
            "role": "partialRecoveryTerminal",
            "expectedState": "recoveryAuthorized",
        },
    }
    source_rows = []
    for fixture_id in ("clean", "recovery"):
        fixture = source[fixture_id]
        source_job = fixture["root"] / fixture["jobId"]
        fixture["treeSha256"] = tree_sha256(source_job, verifier)
        source_report = verifier.verify_ledger(
            fixture["root"],
            fixture["jobId"],
            forbidden_roots,
            generated_utc,
        )
        if source_report["ledgerState"] != fixture["expectedState"]:
            raise MutationError(f"source fixture has wrong state: {fixture_id}")
        source_rows.append(
            {
                "fixtureId": fixture_id,
                "role": fixture["role"],
                "sourceLedgerRootPathSha256": verifier._path_sha256(
                    fixture["root"]
                ),
                "sourceJobId": fixture["jobId"],
                "sourceJobTreeSha256": fixture["treeSha256"],
            }
        )
    output.mkdir(parents=True, exist_ok=False)
    valid_rows = []
    for fixture_id in ("clean", "recovery"):
        fixture = source[fixture_id]
        destination = output / "fixtures" / fixture_id
        ledger, _ = copy_fixture(
            fixture["root"], fixture["jobId"], destination, verifier
        )
        valid_rows.append(
            verify_valid_fixture(
                fixture_id,
                ledger,
                fixture["jobId"],
                forbidden_roots,
                fixture["expectedState"],
                "bound",
                verifier,
                generated_utc,
            )
        )
    legacy_destination = output / "fixtures" / "legacy"
    legacy_ledger, _ = copy_fixture(
        source["clean"]["root"],
        source["clean"]["jobId"],
        legacy_destination,
        verifier,
    )
    make_legacy(legacy_ledger, source["clean"]["jobId"], verifier)
    valid_rows.append(
        verify_valid_fixture(
            "legacy",
            legacy_ledger,
            source["clean"]["jobId"],
            forbidden_roots,
            "attemptOpen",
            "legacy",
            verifier,
            generated_utc,
        )
    )
    case_rows = []
    for definition in MUTATION_CASES:
        case_rows.append(
            run_case(
                definition,
                source,
                output / "cases" / definition[0],
                forbidden_roots,
                verifier,
                generated_utc,
            )
        )
    tool_path = pathlib.Path(__file__).absolute().resolve()
    tool_hash = stable_measure(tool_path)["sha256"]
    verifier_hash = stable_measure(verifier.__file__)["sha256"]
    schema_tree_hash = verifier._schema_tree_sha256()
    report = {
        "schemaVersion": "1.0.0",
        "schemaId": REPORT_SCHEMA_ID,
        "reportType": REPORT_TYPE,
        "toolVersion": TOOL_VERSION,
        "status": "pass",
        "claimBoundary": CLAIM_BOUNDARY,
        "generatedUtc": generated_utc,
        "toolSourceSha256": tool_hash,
        "verifierSourceSha256": verifier_hash,
        "schemaTreeSha256": schema_tree_hash,
        "outputRootPathSha256": verifier._path_sha256(output),
        "sourceFixtures": source_rows,
        "validFixtures": valid_rows,
        "mutationClassCount": len(case_rows),
        "caughtMutationClassCount": sum(
            1 for row in case_rows if row["caught"]
        ),
        "cases": case_rows,
        "scientificOutcomeFieldsRead": False,
    }
    schema = verifier._load_schema("independent-mutation-report.schema.json")
    try:
        jsonschema.Draft202012Validator(schema).validate(report)
    except jsonschema.ValidationError as error:
        raise MutationError(f"mutation report schema failed: {error.message}")
    for fixture_id in ("clean", "recovery"):
        fixture = source[fixture_id]
        if tree_sha256(
            fixture["root"] / fixture["jobId"], verifier
        ) != fixture["treeSha256"]:
            raise MutationError(f"source fixture changed: {fixture_id}")
    if stable_measure(tool_path)["sha256"] != tool_hash:
        raise MutationError("mutation tool changed during execution")
    if stable_measure(verifier.__file__)["sha256"] != verifier_hash:
        raise MutationError("verifier changed during execution")
    if verifier._schema_tree_sha256() != schema_tree_hash:
        raise MutationError("schema tree changed during execution")
    raw = verifier.canonical(report)
    write_exclusive(output / "independent-mutation-report.json", raw)
    return report


def build_parser():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--clean-ledger-root", type=pathlib.Path, required=True)
    parser.add_argument("--clean-job-id", required=True)
    parser.add_argument("--recovery-ledger-root", type=pathlib.Path, required=True)
    parser.add_argument("--recovery-job-id", required=True)
    parser.add_argument(
        "--forbidden-root",
        type=pathlib.Path,
        action="append",
        required=True,
    )
    parser.add_argument("--output-root", type=pathlib.Path, required=True)
    parser.add_argument("--generated-utc", required=True)
    return parser


def main(argv=None):
    arguments = build_parser().parse_args(argv)
    try:
        report = run_matrix(
            arguments.clean_ledger_root,
            arguments.clean_job_id,
            arguments.recovery_ledger_root,
            arguments.recovery_job_id,
            arguments.forbidden_root,
            arguments.output_root,
            arguments.generated_utc,
        )
    except (MutationError, OSError, ValueError) as error:
        print(f"WP14R_MUTATION_ERROR: {error}", file=sys.stderr)
        return 2
    verifier = load_verifier()
    sys.stdout.buffer.write(verifier.canonical(report) + b"\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
