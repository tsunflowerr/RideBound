#!/usr/bin/env python3
"""Build and verify the exact pre-outcome WP14R protocol freeze v3.

Freeze v3 exists because freeze v2 could not execute a single scientific job.

`host_fingerprint()` includes `platform.machine()`, and on Windows that reads the
`PROCESSOR_ARCHITECTURE` environment variable. Freeze v2 allowlisted only PATH,
PYTHONDONTWRITEBYTECODE, SystemRoot, TEMP and TMP for the supervised child, so the
child saw an empty machine string, computed a different host fingerprint, and could
not verify the very receipt its parent had just verified. The failure was
deterministic and host independent: `RB-WP14R-008` exhausted its first job in 32.5 s
without running one simulation epoch.

v3 changes exactly three things and nothing else:

1.  `PROCESSOR_ARCHITECTURE` joins `inheritedEnvironmentNames`, so parent and child
    agree. It also becomes part of the hashed command binding, which makes the
    architecture an explicit term of the execution contract rather than something
    silently derived.
2.  New ledger and control roots under `development-v3-*`, so the exhausted v2
    attempts stay exactly where they are.
3.  The v2 ledger and control roots join the forbidden set, so v3 can never write
    over the retained v2 failure.

The scientific design is untouched: v3 references the same byte-exact WP14-v1 base
freeze, the same 160 jobs, the same paired gate, the same host thresholds. It does
not rescue, retry or reinterpret the exhausted v2 job, which stays terminal.

`wp14r_freeze_v2.py` is left byte-identical on purpose so the v2 authorization and
its failure remain verifiable from source.
"""

from __future__ import annotations

import argparse
import datetime
import hashlib
import importlib.util
import json
import os
import pathlib
import sys

import jsonschema

sys.dont_write_bytecode = True

SCHEMA_ID = (
    "https://ridebound.local/schemas/wp14r/v3/"
    "freeze-v3-authorization.schema.json"
)
SCHEMA_RELATIVE = pathlib.Path(
    "benchmarks/schemas/wp14r/v3/freeze-v3-authorization.schema.json"
)
BASE_RECEIPT_RELATIVE = pathlib.Path(
    "benchmarks/scenarios/wp14-development/freeze-receipt-v1.json"
)
EXPECTED_BASE_RECEIPT_SHA256 = (
    "1ce26ff0f7d87c30d050e57107ad3e118af7f4b88fe04e62e48376ab34c37a55"
)
FREEZE_ID = "wp14r-resilient-development-v3"
V2_LEDGER_ROOT = pathlib.Path(
    r"E:\RideBoundData\wp14r\development-v2-ledger"
)
V2_CONTROL_ROOT = pathlib.Path(
    r"E:\RideBoundData\wp14r\development-v2-control"
)
LEDGER_ROOT = pathlib.Path(
    r"E:\RideBoundData\wp14r\development-v3-ledger"
)
CONTROL_ROOT = pathlib.Path(
    r"E:\RideBoundData\wp14r\development-v3-control"
)
EXPECTED_POWER_SCHEME_GUID = "381b4222-f694-41f0-9685-ff5bb260df2e"
PAIR_JOB_IDS = [
    "w14-d20181112-s10-r1-w08-b1-ref-s7",
    "w14-d20181112-s10-r1-w08-c1-h6ref-s7",
]
CLAIM_BOUNDARY = [
    "developmentExploratoryOnlyNotConfirmatory",
    "frozenPanelsNeverUsedForTuningOrSelection",
    "attemptsAreNotExperimentalUnits",
    "recoveryUsesMechanicalValidityOnly",
    "noPopulationOrSpeedupClaim",
    "doesNotReinterpretOrRescueH6",
    "doesNotSupersedeWp14V1Failure",
]
REPOSITORY_FILES = (
    "benchmarks/schemas/wp14r/v1/attempt-record.schema.json",
    "benchmarks/schemas/wp14r/v1/independent-fixture-receipt.schema.json",
    "benchmarks/schemas/wp14r/v1/independent-mutation-report.schema.json",
    "benchmarks/schemas/wp14r/v1/independent-verification-report.schema.json",
    "benchmarks/schemas/wp14r/v1/ledger-inspection.schema.json",
    "benchmarks/schemas/wp14r/v1/mechanics-dimension-report.schema.json",
    "benchmarks/schemas/wp14r/v1/recovery-receipt.schema.json",
    "benchmarks/schemas/wp14r/v1/supervision-log-record.schema.json",
    "benchmarks/schemas/wp14r/v1/supervision-report.schema.json",
    "benchmarks/schemas/wp14r/v3/freeze-v3-authorization.schema.json",
    "benchmarks/schemas/wp14r/v2/host-preflight-receipt.schema.json",
    "benchmarks/schemas/wp14r/v2/paired-resource-gate-receipt.schema.json",
    "simulators/fleetpy-ridebound/wp14r_attempt_ledger.py",
    "simulators/fleetpy-ridebound/wp14r_freeze_v3.py",
    "simulators/fleetpy-ridebound/wp14r_host_preflight.py",
    "simulators/fleetpy-ridebound/wp14r_independent_fixture.py",
    "simulators/fleetpy-ridebound/wp14r_independent_mutation.py",
    "simulators/fleetpy-ridebound/wp14r_independent_verify.py",
    "simulators/fleetpy-ridebound/wp14r_scientific_protocol.py",
    "simulators/fleetpy-ridebound/wp14r_stale_open_recovery.py",
    "simulators/fleetpy-ridebound/wp14r_supervised_process.py",
    "simulators/fleetpy-ridebound/tests/test_wp14r_freeze_v3.py",
    "simulators/fleetpy-ridebound/tests/test_wp14r_host_preflight.py",
    "simulators/fleetpy-ridebound/tests/test_wp14r_scientific_protocol.py",
)
MECHANICS_GATE_ARTIFACTS = (
    (
        pathlib.Path(
            r"E:\RideBoundData\wp14r\mechanics-dimension-v3-20260827"
            r"\mechanics-dimension-report.json"
        ),
        "44dce55e89c9602daeedc601471e5d2873ab959f86c8bb2394460291baf78bce",
    ),
    (
        pathlib.Path(
            r"E:\RideBoundData\wp14r\independent-fixtures-v2-20260827"
            r"\independent-fixture-receipt.json"
        ),
        "1e4a6450a40a65109592514bcf96df5ce5e1d15977ae3b59488b7da466372104",
    ),
    (
        pathlib.Path(
            r"E:\RideBoundData\wp14r\independent-mutation-v4-20260827"
            r"\independent-mutation-report.json"
        ),
        "9d8aacf48a43449aadfec760c0efcc9f76dbf7fc3077730f90fffed4f5d72e1e",
    ),
)
METHODOLOGY_EVIDENCE = (
    (
        "kalibera-jones-2013-rigorous-benchmarking",
        12,
        pathlib.Path(
            r"E:\RideBoundData\research\pdf-20260826-wp14r-benchmark-methodology"
            r"\kalibera-jones-2013-rigorous-benchmarking.pdf"
        ),
        "b50fb85079cbaea9524eb202393a60807dd3fc270d91eb80de5dba0faf02dbab",
    ),
    (
        "mytkowicz-et-al-2009-producing-wrong-data",
        12,
        pathlib.Path(
            r"E:\RideBoundData\research\pdf-20260826-wp14r-benchmark-methodology"
            r"\mytkowicz-et-al-2009-producing-wrong-data.pdf"
        ),
        "67505bfc1f5a9a442d3ba7f5a5a22e05e55569237def1964a7eab2e7533ee2d6",
    ),
    (
        "curtsinger-berger-2013-stabilizer",
        10,
        pathlib.Path(
            r"E:\RideBoundData\research\pdf-20260828-wp14r-freeze-v2-methodology"
            r"\curtsinger-berger-2013-stabilizer.pdf"
        ),
        "819c930cc8f51a65a24cdc46452a29ec2c872391724974d21589a8729dac9c49",
    ),
)


class FreezeV3Error(RuntimeError):
    """A fail-closed protocol-freeze construction or verification error."""


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
    path = pathlib.Path(path)
    if not path.is_file():
        raise FreezeV3Error(f"required file not found: {path}")
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def artifact(path, display_path=None):
    path = pathlib.Path(path)
    if not path.is_file() or path.is_symlink():
        raise FreezeV3Error(f"artifact is not a regular file: {path}")
    return {
        "path": str(display_path if display_path is not None else path),
        "lengthBytes": path.stat().st_size,
        "sha256": sha256_file(path),
    }


def checked_artifact(path, expected_sha256, display_path=None):
    value = artifact(path, display_path)
    if value["sha256"] != expected_sha256:
        raise FreezeV3Error(f"artifact differs from the audited gate: {path}")
    return value


def load_module(name, path):
    specification = importlib.util.spec_from_file_location(name, path)
    if specification is None or specification.loader is None:
        raise FreezeV3Error(f"cannot load dependency: {path}")
    module = importlib.util.module_from_spec(specification)
    specification.loader.exec_module(module)
    return module


def validate_schema(repository, receipt):
    schema = json.loads(
        (repository / SCHEMA_RELATIVE).read_text(encoding="utf-8")
    )
    try:
        jsonschema.Draft202012Validator.check_schema(schema)
        jsonschema.Draft202012Validator(
            schema,
            format_checker=jsonschema.FormatChecker(),
        ).validate(receipt)
    except jsonschema.SchemaError as error:
        raise FreezeV3Error(
            f"freeze-v3 schema is invalid: {error.message}"
        ) from error
    except jsonschema.ValidationError as error:
        raise FreezeV3Error(
            f"freeze-v3 receipt schema failed: {error.message}"
        ) from error


def overlaps(first, second):
    first = pathlib.Path(first).absolute().resolve()
    second = pathlib.Path(second).absolute().resolve()
    return first == second or first in second.parents or second in first.parents


def verify_base_freeze(repository, base_path):
    base_raw = base_path.read_bytes()
    if sha256_bytes(base_raw) != EXPECTED_BASE_RECEIPT_SHA256:
        raise FreezeV3Error("WP14-v1 freeze receipt hash changed")
    try:
        base = json.loads(base_raw)
    except json.JSONDecodeError as error:
        raise FreezeV3Error("WP14-v1 freeze receipt is invalid JSON") from error
    module = load_module(
        "wp14_freeze_for_wp14r_v3",
        repository / "simulators/fleetpy-ridebound/wp14_freeze.py",
    )
    execution = base["execution"]
    runtime = base["runtime"]
    source = base["sourceIdentity"]
    try:
        module.verify_receipt(
            base_path,
            repository,
            pathlib.Path(execution["outputRoot"]),
            [pathlib.Path(value) for value in execution["forbiddenRoots"]],
            execution["maximumParallelJobs"],
            pathlib.Path(runtime["runnerRoot"]),
            pathlib.Path(runtime["fleetPyRoot"]),
            pathlib.Path(runtime["pythonExecutable"]),
            pathlib.Path(runtime["dotnetExecutable"]),
            pathlib.Path(source["developmentPanelAudit"]["path"]),
            pathlib.Path(source["resourcePlanningEvidence"]["path"]),
        )
    except Exception as error:
        raise FreezeV3Error(
            f"WP14-v1 scientific freeze no longer verifies: {error}"
        ) from error
    return base, base_raw


def protocol_for(base, host_fingerprint):
    forbidden = sorted(
        [
            *base["execution"]["forbiddenRoots"],
            base["execution"]["outputRoot"],
            # The exhausted freeze-v2 attempts are evidence now. v3 must never be
            # able to write over them, so its predecessor roots are forbidden too.
            str(V2_LEDGER_ROOT),
            str(V2_CONTROL_ROOT),
        ]
    )
    if len(forbidden) != 7 or len(set(forbidden)) != 7:
        raise FreezeV3Error("isolation root set is not the exact seven roots")
    if any(overlaps(LEDGER_ROOT, root) for root in forbidden):
        raise FreezeV3Error("ledger root overlaps retained or frozen evidence")
    if any(overlaps(CONTROL_ROOT, root) for root in forbidden):
        raise FreezeV3Error("control root overlaps retained or frozen evidence")
    if overlaps(LEDGER_ROOT, CONTROL_ROOT):
        raise FreezeV3Error("ledger and control roots overlap")
    job_ids = [job["jobId"] for job in base["design"]["jobs"]]
    if len(job_ids) != 160 or len(set(job_ids)) != 160:
        raise FreezeV3Error("base scientific job identity is not exact")
    if any(job_id not in job_ids for job_id in PAIR_JOB_IDS):
        raise FreezeV3Error("paired gate job is absent from the base freeze")
    return {
        "protocolId": "wp14r-supervised-scientific-job-v3",
        "ledgerVersion": "wp14r-immutable-attempt-ledger-v1",
        "maximumAttemptsPerJob": 2,
        "attemptsAreExperimentalUnits": False,
        "recoveryPolicy": (
            "oneInitialOneMechanicalRecoveryRetainAllNoThirdAttempt"
        ),
        "outcomeAccessPolicy": (
            "authorizationAndRecoveryCannotReadScientificOutcome"
        ),
        "pairedResourceGate": {
            "jobIds": PAIR_JOB_IDS,
            "executionOrder": "listedSequentially",
            "requiredValidJobs": 2,
            "requiredFailedJobs": 0,
            "outcomeReadPermitted": False,
            "matrixLaunchOnFailure": False,
        },
        "fullMatrix": {
            "jobCount": 160,
            "jobOrderSha256": sha256_bytes(canonical(job_ids)),
            "requiresPairedGatePass": True,
        },
        "execution": {
            "maximumParallelJobs": 1,
            "maximumJobWallSeconds": 2700,
            "heartbeatIntervalMs": 1000,
            "maximumStreamBytes": 16 * 1024 * 1024,
            "chunkBytes": 32768,
            "treeExitGraceMs": 2000,
        },
        "hostPolicy": {
            "requiredPlatform": "Windows",
            "requiredHostFingerprintSha256": host_fingerprint,
            "requiredAcLineStatus": "online",
            "requiredPowerSchemeGuid": EXPECTED_POWER_SCHEME_GUID,
            "sampleCount": 10,
            "sampleIntervalMs": 1000,
            "maximumMeanCpuBusyPercent": 20,
            "maximumSingleCpuBusyPercent": 60,
            "minimumAvailableMemoryBytes": 8 * 1024 * 1024 * 1024,
            "minimumFreeDiskBytes": 25 * 1024 * 1024 * 1024,
            "arbitraryProcessNamesOrCommandLinesRecorded": False,
        },
        "isolation": {
            "ledgerRoot": str(LEDGER_ROOT),
            "controlRoot": str(CONTROL_ROOT),
            "forbiddenRoots": forbidden,
        },
        # PROCESSOR_ARCHITECTURE is the fix freeze v3 exists for: on Windows
        # platform.machine() reads it, so denying it to the child made the child
        # compute a host fingerprint the receipt could never match.
        "inheritedEnvironmentNames": [
            "PATH",
            "PROCESSOR_ARCHITECTURE",
            "PYTHONDONTWRITEBYTECODE",
            "SystemRoot",
            "TEMP",
            "TMP",
        ],
    }


def source_identity(repository, base):
    files = []
    for relative in sorted(REPOSITORY_FILES):
        files.append(artifact(repository / relative, relative))
    mechanics = [
        checked_artifact(path, expected)
        for path, expected in MECHANICS_GATE_ARTIFACTS
    ]
    python_path = pathlib.Path(base["runtime"]["pythonExecutable"])
    python_artifact = checked_artifact(
        python_path,
        base["runtime"]["pythonExecutableSha256"],
    )
    validate_mechanics_gate_provenance(
        repository,
        python_artifact["sha256"],
    )
    fleetpy_identity = {
        "root": base["runtime"]["fleetPyRoot"],
        "version": base["runtime"]["fleetPyVersion"],
        "commit": base["runtime"]["fleetPyCommit"],
        "capabilityProbeResultSha256": (
            base["runtime"]["fleetPyCapabilityProbeResultSha256"]
        ),
    }
    return {
        "repositoryFiles": files,
        "mechanicsGateArtifacts": mechanics,
        "pythonExecutable": python_artifact,
        "runnerTreeSha256": (
            base["sourceIdentity"]["treeSeals"]["runnerArtifactSha256"]
        ),
        "fleetPyIdentitySha256": sha256_bytes(canonical(fleetpy_identity)),
    }


def validate_mechanics_gate_provenance(repository, python_sha256):
    try:
        fixture = json.loads(
            pathlib.Path(MECHANICS_GATE_ARTIFACTS[1][0]).read_text(
                encoding="utf-8"
            )
        )
        mutation = json.loads(
            pathlib.Path(MECHANICS_GATE_ARTIFACTS[2][0]).read_text(
                encoding="utf-8"
            )
        )
    except (UnicodeDecodeError, json.JSONDecodeError) as error:
        raise FreezeV3Error("mechanics gate artifact is invalid JSON") from error
    paths = {
        "attemptLedger": "wp14r_attempt_ledger.py",
        "builder": "wp14r_independent_fixture.py",
        "independentVerifier": "wp14r_independent_verify.py",
        "recovery": "wp14r_stale_open_recovery.py",
        "supervisor": "wp14r_supervised_process.py",
    }
    adapter = repository / "simulators/fleetpy-ridebound"
    expected = fixture.get("sourceHashes")
    if not isinstance(expected, dict) or any(
        sha256_file(adapter / filename) != expected.get(name)
        for name, filename in paths.items()
    ):
        raise FreezeV3Error("current mechanics source differs from the 006 fixture")
    verifier = load_module(
        "wp14r_independent_verifier_for_freeze_v3_provenance",
        adapter / "wp14r_independent_verify.py",
    )
    schema_tree = verifier._schema_tree_sha256()
    if (
        fixture.get("schemaTreeSha256") != schema_tree
        or mutation.get("schemaTreeSha256") != schema_tree
        or mutation.get("verifierSourceSha256")
        != expected["independentVerifier"]
        or mutation.get("toolSourceSha256")
        != sha256_file(adapter / "wp14r_independent_mutation.py")
    ):
        raise FreezeV3Error("current verifier/schema differs from the 006 gate")
    if fixture.get("pythonExecutableSha256") != python_sha256:
        raise FreezeV3Error("Python differs from the independently verified fixture")


def methodology_evidence():
    return [
        {
            "citationId": citation_id,
            "pageCount": pages,
            "artifact": checked_artifact(path, expected),
        }
        for citation_id, pages, path, expected in METHODOLOGY_EVIDENCE
    ]


def build(repository, authorized_at_utc, request_date="2026-08-28"):
    repository = pathlib.Path(repository).resolve()
    if not isinstance(authorized_at_utc, str) or not authorized_at_utc.endswith("Z"):
        raise FreezeV3Error("authorizedAtUtc must be an explicit UTC Z timestamp")
    try:
        datetime.date.fromisoformat(request_date)
    except (TypeError, ValueError) as error:
        raise FreezeV3Error("owner authorization date is invalid") from error
    base_path = repository / BASE_RECEIPT_RELATIVE
    base, base_raw = verify_base_freeze(repository, base_path)
    supervisor = load_module(
        "wp14r_supervisor_for_freeze_v3",
        repository / "simulators/fleetpy-ridebound/wp14r_supervised_process.py",
    )
    platform_name, host_fingerprint = supervisor.host_fingerprint()
    if platform_name != "Windows":
        raise FreezeV3Error("freeze v3 is authorized only on the audited Windows host")
    host_module = load_module(
        "wp14r_host_preflight_for_freeze_v3",
        repository / "simulators/fleetpy-ridebound/wp14r_host_preflight.py",
    )
    if host_module.read_active_power_scheme() != EXPECTED_POWER_SCHEME_GUID:
        raise FreezeV3Error("active power scheme differs from the v3 protocol")
    receipt = {
        "schemaVersion": "3.0.0",
        "schemaId": SCHEMA_ID,
        "recordType": "ridebound-wp14r-freeze-v3-authorization",
        "freezeId": FREEZE_ID,
        "authorizedAtUtc": authorized_at_utc,
        "authorizationStatus": (
            "protocolAuthorizedExecutionPreconditionsRequired"
        ),
        "ownerAuthorization": {
            "basis": "explicitPersistentGoalContinueWp14rBenchmarks",
            "requestDate": request_date,
            "scientificLaunchRequiresPassingHostPreflight": True,
        },
        "claimBoundary": CLAIM_BOUNDARY,
        "baseScientificFreeze": {
            "artifact": artifact(base_path, str(BASE_RECEIPT_RELATIVE).replace(
                "\\", "/"
            )),
            "freezeId": base["freezeId"],
            "designSha256": sha256_bytes(canonical(base["design"])),
            "sourceIdentitySha256": sha256_bytes(
                canonical(base["sourceIdentity"])
            ),
            "jobCount": base["design"]["jobCount"],
        },
        "protocol": protocol_for(base, host_fingerprint),
        "sourceIdentity": source_identity(repository, base),
        "methodologyEvidence": methodology_evidence(),
    }
    if receipt["baseScientificFreeze"]["artifact"]["sha256"] != (
        sha256_bytes(base_raw)
    ):
        raise FreezeV3Error("base freeze bytes changed during v3 construction")
    validate_schema(repository, receipt)
    return receipt


def read_canonical_receipt(path):
    raw = pathlib.Path(path).read_bytes()
    try:
        document = json.loads(raw)
    except (UnicodeDecodeError, json.JSONDecodeError) as error:
        raise FreezeV3Error("freeze-v3 receipt is not canonical JSON") from error
    if raw != canonical(document) + b"\n":
        raise FreezeV3Error("freeze-v3 receipt bytes are not canonical")
    return document, raw


def verify_receipt(path, repository=None):
    path = pathlib.Path(path).resolve()
    repository = (
        pathlib.Path(repository).resolve()
        if repository is not None
        else pathlib.Path(__file__).resolve().parents[2]
    )
    actual, _ = read_canonical_receipt(path)
    expected = build(
        repository,
        actual.get("authorizedAtUtc"),
        actual.get("ownerAuthorization", {}).get("requestDate"),
    )
    if actual != expected:
        raise FreezeV3Error("freeze-v3 receipt differs from its exact sources")
    return actual


def write_exclusive(path, content):
    path = pathlib.Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
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
    parser.add_argument("--repository", type=pathlib.Path, required=True)
    parser.add_argument("--output", type=pathlib.Path, required=True)
    parser.add_argument("--authorized-at-utc")
    parser.add_argument("--request-date", default="2026-08-28")
    parser.add_argument("--write", action="store_true")
    return parser


def main(argv=None):
    arguments = build_parser().parse_args(argv)
    try:
        if arguments.write:
            if arguments.authorized_at_utc is None:
                raise FreezeV3Error("--write requires --authorized-at-utc")
            receipt = build(
                arguments.repository,
                arguments.authorized_at_utc,
                arguments.request_date,
            )
            write_exclusive(arguments.output, canonical(receipt) + b"\n")
            report = {
                "status": "written",
                "freezeId": receipt["freezeId"],
                "freezeReceiptSha256": sha256_file(arguments.output),
                "jobCount": receipt["baseScientificFreeze"]["jobCount"],
            }
        else:
            receipt = verify_receipt(
                arguments.output,
                arguments.repository,
            )
            report = {
                "status": "valid",
                "freezeId": receipt["freezeId"],
                "freezeReceiptSha256": sha256_file(arguments.output),
                "jobCount": receipt["baseScientificFreeze"]["jobCount"],
            }
        print(json.dumps(report, sort_keys=True, separators=(",", ":")))
        return 0
    except (OSError, FreezeV3Error) as error:
        print(f"WP14R_FREEZE_V3_ERROR: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
