#!/usr/bin/env python3
"""Plan and execute exact WP14R v2 jobs through the resilient mechanics."""

from __future__ import annotations

import argparse
import datetime
import hashlib
import importlib.util
import json
import os
import pathlib
import shutil
import subprocess
import sys

import jsonschema

sys.dont_write_bytecode = True

PAIR_GATE_SCHEMA_ID = (
    "https://ridebound.local/schemas/wp14r/v2/"
    "paired-resource-gate-receipt.schema.json"
)
PAIR_GATE_SCHEMA_RELATIVE = pathlib.Path(
    "benchmarks/schemas/wp14r/v2/"
    "paired-resource-gate-receipt.schema.json"
)
PAIR_GATE_FILENAME = "paired-resource-gate-receipt.json"
PAIR_GATE_CLAIM_BOUNDARY = [
    "resourceAndMechanicalValidityOnly",
    "doesNotReadScientificOutcome",
    "singleWithinHostPairNoSpeedupClaim",
    "attemptsAreNotExperimentalUnits",
    "doesNotSupersedeWp14V1",
]
VERIFIER_ID = "actual-fleetpy-medium-verifier-v1"
INDEPENDENT_VERIFIER_TIMEOUT_SECONDS = 300
BUNDLE_VERIFIER_TIMEOUT_SECONDS = 900


class ProtocolError(RuntimeError):
    """A fail-closed WP14R v2 protocol condition."""


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


def load_module(name, filename):
    path = pathlib.Path(__file__).resolve().with_name(filename)
    specification = importlib.util.spec_from_file_location(name, path)
    if specification is None or specification.loader is None:
        raise ProtocolError(f"cannot load protocol dependency: {path}")
    module = importlib.util.module_from_spec(specification)
    specification.loader.exec_module(module)
    return module


# A receipt is owned by exactly one builder. Selecting the builder from the
# receipt's own freezeId is safe because the builder then rebuilds the whole
# receipt from source and compares it: a receipt that lied about its identity
# only picks a verifier that rejects it.
FREEZE_MODULES = {
    "wp14r-resilient-development-v2": "wp14r_freeze_v2.py",
    "wp14r-resilient-development-v3": "wp14r_freeze_v3.py",
    "wp14r-resilient-development-v4": "wp14r_freeze_v4.py",
    "wp14r-resilient-development-v5": "wp14r_freeze_v5.py",
    "wp14r-resilient-development-v6": "wp14r_freeze_v6.py",
}


def freeze_module_for(path):
    try:
        declared = json.loads(pathlib.Path(path).read_bytes())["freezeId"]
    except (OSError, UnicodeDecodeError, json.JSONDecodeError, KeyError) as error:
        raise ProtocolError("freeze receipt declares no readable identity") from error
    filename = FREEZE_MODULES.get(declared)
    if filename is None:
        raise ProtocolError(f"no builder owns freeze identity: {declared}")
    return load_module(f"wp14r_freeze_for_protocol_{declared}", filename)


def load_dependencies(freeze_path=None):
    return {
        "freeze": freeze_module_for(freeze_path) if freeze_path else load_module(
            "wp14r_freeze_v2_for_protocol",
            "wp14r_freeze_v2.py",
        ),
        "host": load_module(
            "wp14r_host_preflight_for_protocol",
            "wp14r_host_preflight.py",
        ),
        "ledger": load_module(
            "wp14r_attempt_ledger_for_protocol",
            "wp14r_attempt_ledger.py",
        ),
        "recovery": load_module(
            "wp14r_stale_recovery_for_protocol",
            "wp14r_stale_open_recovery.py",
        ),
        "supervisor": load_module(
            "wp14r_supervisor_for_protocol",
            "wp14r_supervised_process.py",
        ),
    }


def read_freeze(path, dependencies=None):
    dependencies = dependencies or load_dependencies(path)
    receipt = dependencies["freeze"].verify_receipt(path)
    raw = pathlib.Path(path).read_bytes()
    return receipt, sha256_bytes(raw), dependencies


def base_receipt(repository, freeze_receipt):
    relative = freeze_receipt["baseScientificFreeze"]["artifact"]["path"]
    path = (pathlib.Path(repository) / relative).resolve()
    raw = path.read_bytes()
    if sha256_bytes(raw) != freeze_receipt["baseScientificFreeze"][
        "artifact"
    ]["sha256"]:
        raise ProtocolError("base scientific freeze changed after v2 authorization")
    try:
        receipt = json.loads(raw)
    except (UnicodeDecodeError, json.JSONDecodeError) as error:
        raise ProtocolError("base scientific freeze is invalid JSON") from error
    return receipt


def select_job(base, job_id):
    matches = [job for job in base["design"]["jobs"] if job["jobId"] == job_id]
    if len(matches) != 1:
        raise ProtocolError("job is absent or duplicated in the base freeze")
    return matches[0]


def select_arm(base, arm_id):
    matches = [arm for arm in base["design"]["arms"] if arm["armId"] == arm_id]
    if len(matches) != 1:
        raise ProtocolError("arm is absent or duplicated in the base freeze")
    return matches[0]


def resolve_under(root, relative, label):
    root = pathlib.Path(root).resolve()
    path = (root / relative).resolve()
    if path != root and root not in path.parents:
        raise ProtocolError(f"{label} escapes the repository")
    if not path.is_file() or path.is_symlink():
        raise ProtocolError(f"{label} is not a regular file")
    return path


def job_binding(freeze_receipt, freeze_sha256, base, job_id):
    job = select_job(base, job_id)
    arm = select_arm(base, job["armId"])
    binding = {
        "bindingVersion": "wp14r-scientific-job-binding-v2",
        "freezeId": freeze_receipt["freezeId"],
        "freezeReceiptSha256": freeze_sha256,
        "baseScientificFreezeSha256": freeze_receipt[
            "baseScientificFreeze"
        ]["artifact"]["sha256"],
        "protocolId": freeze_receipt["protocol"]["protocolId"],
        "job": job,
        "arm": arm,
    }
    return binding, sha256_bytes(canonical(binding))


def normalized_child_environment(receipt, source_environment=None):
    source = os.environ if source_environment is None else source_environment
    casefolded = {key.casefold(): value for key, value in source.items()}
    casefolded["pythondontwritebytecode"] = "1"
    result = {}
    for name in receipt["protocol"]["inheritedEnvironmentNames"]:
        key = name.casefold()
        if key not in casefolded:
            raise ProtocolError(f"required environment variable is absent: {name}")
        result[name] = casefolded[key]
    return result


def stable_child_command(
    repository,
    freeze_path,
    freeze_receipt,
    job_id,
    supervisor,
    source_environment=None,
):
    repository = pathlib.Path(repository).resolve()
    freeze_path = pathlib.Path(freeze_path).resolve()
    executable = pathlib.Path(
        base_receipt(repository, freeze_receipt)["runtime"][
            "pythonExecutable"
        ]
    ).resolve()
    arguments = [
        "-B",
        str(pathlib.Path(__file__).resolve()),
        "child",
        "--repository",
        str(repository),
        "--freeze",
        str(freeze_path),
        "--job-id",
        job_id,
    ]
    environment = normalized_child_environment(
        freeze_receipt,
        source_environment,
    )
    metadata, effective = supervisor.build_command_binding(
        executable,
        arguments,
        repository,
        freeze_receipt["protocol"]["inheritedEnvironmentNames"],
        environment,
    )
    return executable, arguments, metadata, effective


def roots(freeze_receipt):
    isolation = freeze_receipt["protocol"]["isolation"]
    return (
        pathlib.Path(isolation["ledgerRoot"]).absolute(),
        pathlib.Path(isolation["controlRoot"]).absolute(),
        [pathlib.Path(value).resolve() for value in isolation["forbiddenRoots"]],
    )


def safe_control_root(freeze_receipt, ledger):
    _, control_root, forbidden = roots(freeze_receipt)
    return ledger.validate_ledger_root(control_root, forbidden)


def paired_gate_path(freeze_receipt):
    dependencies = load_dependencies()
    control_root = safe_control_root(
        freeze_receipt,
        dependencies["ledger"],
    )
    return control_root / PAIR_GATE_FILENAME


def read_canonical_json(path, label):
    raw = pathlib.Path(path).read_bytes()
    try:
        document = json.loads(raw)
    except (UnicodeDecodeError, json.JSONDecodeError) as error:
        raise ProtocolError(f"{label} is not canonical JSON") from error
    if raw != canonical(document) + b"\n":
        raise ProtocolError(f"{label} bytes are not canonical")
    return document, raw


def validate_pair_gate_schema(repository, document):
    schema = json.loads(
        (pathlib.Path(repository) / PAIR_GATE_SCHEMA_RELATIVE).read_text(
            encoding="utf-8"
        )
    )
    try:
        jsonschema.Draft202012Validator(
            schema,
            format_checker=jsonschema.FormatChecker(),
        ).validate(document)
    except jsonschema.ValidationError as error:
        raise ProtocolError(
            f"paired resource gate schema failed: {error.message}"
        ) from error


def verify_pair_gate(repository, freeze_receipt, freeze_sha256, path=None):
    dependencies = load_dependencies()
    control_root = safe_control_root(
        freeze_receipt,
        dependencies["ledger"],
    )
    path = pathlib.Path(path or (control_root / PAIR_GATE_FILENAME)).resolve()
    document, _ = read_canonical_json(path, "paired resource gate")
    validate_pair_gate_schema(repository, document)
    if (
        document["freezeId"] != freeze_receipt["freezeId"]
        or document["freezeReceiptSha256"] != freeze_sha256
        or document["protocolToolSha256"] != sha256_file(__file__)
    ):
        raise ProtocolError("paired resource gate source/freeze binding failed")
    base = base_receipt(repository, freeze_receipt)
    envelope = base["execution"]["resourceEnvelope"]
    row_pass = all(
        row["ledgerState"] == "succeeded"
        and row["independentVerificationStatus"] == "valid"
        for row in document["jobs"]
    )
    total_retained = sum(
        row["retainedOutputBytes"] for row in document["jobs"]
    )
    resource_pass = (
        document["maximumRetainedOutputBytes"]
        == envelope["maximumOutputBytes"]
        and document["minimumFreeDiskReserveBytes"]
        == envelope["minimumFreeDiskReserveBytes"]
        and total_retained == document["totalRetainedOutputBytes"]
        and total_retained <= document["maximumRetainedOutputBytes"]
        and document["freeDiskBytesAtGate"]
        >= document["minimumFreeDiskReserveBytes"]
    )
    expected_status = "pass" if row_pass and resource_pass else "fail"
    if (
        document["status"] != expected_status
        or document["resourceEnvelopeStatus"]
        != ("pass" if resource_pass else "fail")
        or document["matrixAuthorized"] != (expected_status == "pass")
    ):
        raise ProtocolError("paired resource gate decision is inconsistent")
    host = load_module(
        "wp14r_host_preflight_for_pair_gate_verification",
        "wp14r_host_preflight.py",
    )
    for row in document["jobs"]:
        for artifact in row["preflightReceipts"]:
            candidate = (control_root / artifact["path"]).resolve()
            if candidate != control_root and control_root not in candidate.parents:
                raise ProtocolError("preflight receipt escapes the control root")
            if (
                not candidate.is_file()
                or candidate.stat().st_size != artifact["lengthBytes"]
                or sha256_file(candidate) != artifact["sha256"]
            ):
                raise ProtocolError("preflight receipt changed after the pair gate")
            preflight, _ = read_canonical_json(
                candidate,
                "host preflight receipt",
            )
            host.validate_receipt(preflight)
            if (
                preflight["freezeId"] != freeze_receipt["freezeId"]
                or preflight["freezeReceiptSha256"] != freeze_sha256
                or preflight["jobId"] != row["jobId"]
                or preflight["status"] != artifact["status"]
                or preflight["prospectiveAttemptNumber"]
                != artifact["prospectiveAttemptNumber"]
                or preflight["preflightToolSha256"]
                != sha256_file(host.__file__)
            ):
                raise ProtocolError("preflight receipt source/freeze binding failed")
    return document


def authorize_phase(
    repository,
    phase,
    job_id,
    freeze_receipt,
    freeze_sha256,
    ledger,
):
    base = base_receipt(repository, freeze_receipt)
    job_ids = [job["jobId"] for job in base["design"]["jobs"]]
    if job_id not in job_ids:
        raise ProtocolError("job is not authorized by the base freeze")
    ledger_root, _, forbidden = roots(freeze_receipt)
    retained = retained_output_bytes(base, ledger_root, forbidden, ledger)
    maximum = base["execution"]["resourceEnvelope"]["maximumOutputBytes"]
    if retained > maximum:
        raise ProtocolError("retained scientific output exceeds the frozen maximum")
    pair_ids = freeze_receipt["protocol"]["pairedResourceGate"]["jobIds"]
    if phase == "paired":
        if job_id not in pair_ids:
            raise ProtocolError("paired phase accepts only the two frozen gate jobs")
        position = pair_ids.index(job_id)
        if position:
            previous = ledger.inspect_ledger(
                ledger_root,
                pair_ids[position - 1],
                forbidden,
            )
            if previous["ledgerState"] != "succeeded":
                raise ProtocolError("paired jobs must execute in the frozen order")
        return
    gate = verify_pair_gate(
        repository,
        freeze_receipt,
        freeze_sha256,
    )
    if gate["status"] != "pass" or not gate["matrixAuthorized"]:
        raise ProtocolError("paired resource gate does not authorize the matrix")
    for candidate in job_ids:
        inspection = ledger.inspect_ledger(
            ledger_root,
            candidate,
            forbidden,
        )
        if inspection["ledgerState"] == "succeeded":
            continue
        if candidate != job_id:
            raise ProtocolError("matrix job differs from the next frozen job")
        if inspection["ledgerState"] == "exhausted":
            raise ProtocolError("matrix stopped at an exhausted frozen job")
        return
    raise ProtocolError("the full matrix is already complete")


def retained_output_bytes(base, ledger_root, forbidden, ledger):
    total = 0
    for job in base["design"]["jobs"]:
        root = ledger.job_root(
            ledger_root,
            job["jobId"],
            forbidden,
        )
        if not root.exists():
            continue
        for attempt in sorted(root.glob("attempt-*")):
            output = attempt / "output"
            if output.exists():
                total += ledger.directory_inventory(output)["bytes"]
    return total


def plan_job(
    repository,
    freeze_path,
    phase,
    job_id,
    dependencies=None,
    source_environment=None,
):
    freeze_receipt, freeze_sha256, dependencies = read_freeze(
        freeze_path,
        dependencies,
    )
    repository = pathlib.Path(repository).resolve()
    authorize_phase(
        repository,
        phase,
        job_id,
        freeze_receipt,
        freeze_sha256,
        dependencies["ledger"],
    )
    base = base_receipt(repository, freeze_receipt)
    _, binding_sha256 = job_binding(
        freeze_receipt,
        freeze_sha256,
        base,
        job_id,
    )
    _, _, metadata, _ = stable_child_command(
        repository,
        freeze_path,
        freeze_receipt,
        job_id,
        dependencies["supervisor"],
        source_environment,
    )
    ledger_root, control_root, forbidden = roots(freeze_receipt)
    return {
        "schemaVersion": "2.0.0",
        "reportType": "ridebound-wp14r-scientific-job-plan-v2",
        "status": "authorizedPlan",
        "phase": phase,
        "freezeId": freeze_receipt["freezeId"],
        "freezeReceiptSha256": freeze_sha256,
        "jobId": job_id,
        "jobBindingSha256": binding_sha256,
        "commandSha256": metadata["commandSha256"],
        "ledgerRootPathSha256": sha256_bytes(str(ledger_root).encode("utf-8")),
        "controlRootPathSha256": sha256_bytes(str(control_root).encode("utf-8")),
        "forbiddenRootPathSha256s": [
            sha256_bytes(str(path).encode("utf-8")) for path in forbidden
        ],
        "outcomeFieldsRead": False,
    }


def actual_scientific_command(repository, freeze_receipt, job_id, output):
    base = base_receipt(repository, freeze_receipt)
    job = select_job(base, job_id)
    arm = select_arm(base, job["armId"])
    wp4 = resolve_under(repository, job["wp4Config"], "WP4 config")
    commitment = resolve_under(
        repository,
        job["commitmentConfig"],
        "commitment config",
    )
    driver = resolve_under(repository, job["driver"], "driver")
    if sha256_file(wp4) != arm["wp4ConfigSha256"]:
        raise ProtocolError("WP4 config differs from the frozen arm")
    if sha256_file(commitment) != arm["commitmentConfigSha256"]:
        raise ProtocolError("commitment config differs from the frozen arm")
    if sha256_file(driver) != job["driverSha256"]:
        raise ProtocolError("driver differs from the frozen job")
    fixture = (
        pathlib.Path(repository)
        / "benchmarks/fixtures/wp6/public/fleetpy-manhattan-v1"
        / base["design"]["gridId"]
        / job["cellId"]
    ).resolve()
    scenario = fixture / "scenario-content.json"
    if sha256_file(scenario) != job["scenarioContentSha256"]:
        raise ProtocolError("scenario differs from the frozen job")
    runtime = base["runtime"]
    return [
        runtime["pythonExecutable"],
        "-B",
        str(
            pathlib.Path(repository)
            / "simulators/fleetpy-ridebound/actual_fleetpy_medium_preflight.py"
        ),
        "--label",
        job_id,
        "--fleetpy-root",
        runtime["fleetPyRoot"],
        "--runner-root",
        runtime["runnerRoot"],
        "--dotnet",
        runtime["dotnetExecutable"],
        "--commitment-config",
        str(commitment),
        "--wp4-config",
        str(wp4),
        "--scenario",
        str(scenario),
        "--derivative-manifest",
        str(fixture / "derivative-manifest.json"),
        "--normalization-report",
        str(fixture / "normalization-report.json"),
        "--selection-frame",
        str(fixture / "selection-frame.json"),
        "--driver",
        str(driver),
        "--output",
        str(output),
        "--repeats",
        str(base["execution"]["repeatsPerJob"]),
        "--master-seed",
        str(job["masterSeed"]),
    ]


def run_scientific_child(repository, freeze_path, job_id, dependencies=None):
    freeze_receipt, freeze_sha256, dependencies = read_freeze(
        freeze_path,
        dependencies,
    )
    repository = pathlib.Path(repository).resolve()
    ledger_root, _, forbidden = roots(freeze_receipt)
    inspection = dependencies["ledger"].inspect_ledger(
        ledger_root,
        job_id,
        forbidden,
    )
    if inspection["ledgerState"] != "attemptOpen":
        raise ProtocolError("scientific child requires the current open attempt")
    attempt_number = inspection["openAttemptNumber"]
    job_root = dependencies["ledger"].job_root(
        ledger_root,
        job_id,
        forbidden,
    )
    attempt_path = job_root / f"attempt-{attempt_number:02d}"
    start, _ = dependencies["ledger"].read_canonical_json(
        attempt_path / "attempt-start.json"
    )
    base = base_receipt(repository, freeze_receipt)
    _, expected_binding = job_binding(
        freeze_receipt,
        freeze_sha256,
        base,
        job_id,
    )
    if start["jobBindingSha256"] != expected_binding:
        raise ProtocolError("open attempt differs from the frozen job binding")
    output = attempt_path / start["outputRelativePath"]
    if output.exists():
        raise ProtocolError("scientific output already exists before child launch")
    command = actual_scientific_command(
        repository,
        freeze_receipt,
        job_id,
        output,
    )
    environment = dict(os.environ)
    environment["PYTHONDONTWRITEBYTECODE"] = "1"
    completed = subprocess.run(
        command,
        check=False,
        env=environment,
    )
    return completed.returncode


def next_preflight_path(control_root, job_id, attempt_number):
    root = pathlib.Path(control_root) / job_id
    root.mkdir(parents=True, exist_ok=True)
    prefix = f"preflight-attempt-{attempt_number:02d}-observation-"
    existing = sorted(root.glob(f"{prefix}*.json"))
    sequence = len(existing) + 1
    return root / f"{prefix}{sequence:04d}.json"


def record_preflight(
    freeze_receipt,
    freeze_sha256,
    job_id,
    attempt_number,
    dependencies,
):
    control_root = safe_control_root(
        freeze_receipt,
        dependencies["ledger"],
    )
    dependencies["ledger"].job_root(
        control_root,
        job_id,
        roots(freeze_receipt)[2],
    )
    receipt = dependencies["host"].collect_preflight(
        freeze_receipt,
        freeze_sha256,
        job_id,
        attempt_number,
        control_root,
    )
    path = next_preflight_path(control_root, job_id, attempt_number)
    dependencies["host"].write_exclusive(
        path,
        dependencies["host"].canonical(receipt) + b"\n",
    )
    return receipt, path


def supervisor_cli_command(
    repository,
    freeze_receipt,
    ledger_root,
    forbidden,
    job_id,
    attempt_number,
    executable,
    arguments,
):
    base = base_receipt(repository, freeze_receipt)
    policy = freeze_receipt["protocol"]["execution"]
    command = [
        base["runtime"]["pythonExecutable"],
        "-B",
        str(
            pathlib.Path(repository)
            / "simulators/fleetpy-ridebound/wp14r_supervised_process.py"
        ),
        "run",
        "--ledger-root",
        str(ledger_root),
        "--job-id",
        job_id,
        "--attempt-number",
        str(attempt_number),
        "--executable",
        str(executable),
        "--working-directory",
        str(pathlib.Path(repository).resolve()),
        "--wall-timeout-ms",
        str(policy["maximumJobWallSeconds"] * 1000),
        "--heartbeat-interval-ms",
        str(policy["heartbeatIntervalMs"]),
        "--maximum-stream-bytes",
        str(policy["maximumStreamBytes"]),
        "--chunk-bytes",
        str(policy["chunkBytes"]),
        "--tree-exit-grace-ms",
        str(policy["treeExitGraceMs"]),
    ]
    for path in forbidden:
        command.append(f"--forbidden-root={path}")
    for value in arguments:
        command.append(f"--argument={value}")
    for name in freeze_receipt["protocol"]["inheritedEnvironmentNames"]:
        command.append(f"--inherit-environment={name}")
    return command


def run_supervisor_cli(command, environment, wall_seconds):
    process = subprocess.Popen(
        command,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        env=environment,
    )
    try:
        stdout, stderr = process.communicate(timeout=wall_seconds + 60)
    except subprocess.TimeoutExpired:
        process.kill()
        stdout, stderr = process.communicate()
    return {
        "processId": process.pid,
        "returnCode": process.returncode,
        "stdout": stdout,
        "stderrSha256": sha256_bytes(stderr or b""),
    }


def process_log_path(ledger, ledger_root, job_id, attempt_number, forbidden):
    root = ledger.job_root(ledger_root, job_id, forbidden)
    attempt = root / f"attempt-{attempt_number:02d}"
    start, _ = ledger.read_canonical_json(attempt / "attempt-start.json")
    return attempt / start["processLogRelativePath"]


def parse_supervisor_result(result, supervisor, process_log):
    if process_log.exists():
        return supervisor.verify_process_log(process_log)
    if result["stdout"]:
        try:
            return json.loads(result["stdout"])
        except (UnicodeDecodeError, json.JSONDecodeError) as error:
            raise ProtocolError("supervisor stdout is not valid JSON") from error
    return None


def bundle_verifier(
    repository,
    freeze_receipt,
    output,
    runner=subprocess.run,
):
    base = base_receipt(repository, freeze_receipt)
    verifier = (
        pathlib.Path(repository)
        / "simulators/fleetpy-ridebound/actual_fleetpy_medium_verify.py"
    ).resolve()
    command = [
        base["runtime"]["pythonExecutable"],
        "-B",
        str(verifier),
        "--bundle",
        str(output),
        "--include-behavioral-hash",
        "--require-audited-solver-evidence",
    ]
    completed = runner(
        command,
        check=False,
        capture_output=True,
        timeout=BUNDLE_VERIFIER_TIMEOUT_SECONDS,
    )
    if completed.returncode != 0:
        return {
            "status": "fail",
            "verifierId": VERIFIER_ID,
            "verifierSha256": sha256_file(verifier),
            "behavioralHash": None,
        }
    try:
        report = json.loads(completed.stdout)
    except (UnicodeDecodeError, json.JSONDecodeError) as error:
        raise ProtocolError("bundle verifier stdout is invalid JSON") from error
    behavioral_hash = report.get("behavioralProjectionHash")
    if report.get("status") != "pass" or not isinstance(
        behavioral_hash,
        str,
    ):
        raise ProtocolError("bundle verifier did not return exact pass evidence")
    return {
        "status": "pass",
        "verifierId": VERIFIER_ID,
        "verifierSha256": sha256_file(verifier),
        "behavioralHash": behavioral_hash,
    }


def journal_elapsed_ms(supervisor, process_log):
    _, records, _ = supervisor.read_verified_process_log(process_log)
    return records[-1]["monotonicElapsedMs"]


def terminalize_complete_attempt(
    repository,
    freeze_receipt,
    job_id,
    attempt_number,
    report,
    dependencies,
):
    ledger_root, _, forbidden = roots(freeze_receipt)
    ledger = dependencies["ledger"]
    supervisor = dependencies["supervisor"]
    log_path = process_log_path(
        ledger,
        ledger_root,
        job_id,
        attempt_number,
        forbidden,
    )
    elapsed_ms = journal_elapsed_ms(supervisor, log_path)
    terminal_status = report["terminalStatus"]
    if terminal_status == "childExitedZeroAwaitingBundleVerification":
        root = ledger.job_root(ledger_root, job_id, forbidden)
        output = root / f"attempt-{attempt_number:02d}" / "output"
        verification = bundle_verifier(
            repository,
            freeze_receipt,
            output,
        )
        classification = (
            "success" if verification["status"] == "pass" else "verifierFailure"
        )
        return ledger.terminalize_attempt(
            ledger_root,
            job_id,
            attempt_number,
            classification,
            elapsed_ms,
            report["treeStatus"],
            verification["status"],
            forbidden,
            process_exit_code=report["processExitCode"],
            verifier_id=verification["verifierId"],
            verifier_sha256=verification["verifierSha256"],
            behavioral_hash=verification["behavioralHash"],
        )
    mapping = {
        "wallTimeout": "controlledTimeout",
        "childExitFailure": "processExitFailure",
    }
    if terminal_status in mapping:
        return ledger.terminalize_attempt(
            ledger_root,
            job_id,
            attempt_number,
            mapping[terminal_status],
            elapsed_ms,
            report["treeStatus"],
            "notRun",
            forbidden,
            process_exit_code=report["processExitCode"],
        )
    return None


def recover_open(
    freeze_receipt,
    job_id,
    attempt_number,
    supervisor_process_id,
    dependencies,
):
    ledger_root, _, forbidden = roots(freeze_receipt)
    return dependencies["recovery"].recover_open_attempt(
        ledger_root,
        job_id,
        attempt_number,
        forbidden,
        supervisor_process_id,
        freeze_receipt["protocol"]["execution"]["treeExitGraceMs"],
    )


def independent_verify(
    repository,
    freeze_receipt,
    job_id,
    runner=subprocess.run,
):
    base = base_receipt(repository, freeze_receipt)
    ledger_root, _, forbidden = roots(freeze_receipt)
    verifier = (
        pathlib.Path(repository)
        / "simulators/fleetpy-ridebound/wp14r_independent_verify.py"
    ).resolve()
    command = [
        base["runtime"]["pythonExecutable"],
        "-B",
        str(verifier),
        "--ledger-root",
        str(ledger_root),
        "--job-id",
        job_id,
    ]
    for root in forbidden:
        command.append(f"--forbidden-root={root}")
    completed = runner(
        command,
        check=False,
        capture_output=True,
        timeout=INDEPENDENT_VERIFIER_TIMEOUT_SECONDS,
    )
    if completed.returncode != 0:
        raise ProtocolError("independent ledger verification failed")
    try:
        report = json.loads(completed.stdout)
    except (UnicodeDecodeError, json.JSONDecodeError) as error:
        raise ProtocolError("independent verifier stdout is invalid JSON") from error
    if report.get("status") != "valid" or report.get("jobId") != job_id:
        raise ProtocolError("independent verifier returned the wrong ledger")
    return report


def finalize_open_attempt(
    repository,
    freeze_receipt,
    job_id,
    inspection,
    dependencies,
    known_supervisor_process_id=None,
):
    ledger_root, _, forbidden = roots(freeze_receipt)
    attempt_number = inspection["openAttemptNumber"]
    log_path = process_log_path(
        dependencies["ledger"],
        ledger_root,
        job_id,
        attempt_number,
        forbidden,
    )
    report = None
    supervisor_process_id = known_supervisor_process_id
    if log_path.exists():
        raw, records, _ = dependencies[
            "supervisor"
        ].read_verified_process_log(log_path, allow_no_complete_record=True)
        if records:
            supervisor_process_id = records[0]["payload"][
                "supervisorProcessId"
            ]
            report = dependencies["supervisor"].verify_process_log(log_path)
        elif raw:
            raise ProtocolError("process journal has bytes but no valid record")
    if report is not None and report["status"] == "validComplete":
        terminal = terminalize_complete_attempt(
            repository,
            freeze_receipt,
            job_id,
            attempt_number,
            report,
            dependencies,
        )
        if terminal is not None:
            return terminal
    if supervisor_process_id is None:
        raise ProtocolError("open attempt lacks a supervisor process identity")
    return recover_open(
        freeze_receipt,
        job_id,
        attempt_number,
        supervisor_process_id,
        dependencies,
    )["attemptTerminal"]


def execute_job(
    repository,
    freeze_path,
    phase,
    job_id,
    dependencies=None,
    source_environment=None,
):
    freeze_receipt, freeze_sha256, dependencies = read_freeze(
        freeze_path,
        dependencies,
    )
    repository = pathlib.Path(repository).resolve()
    ledger = dependencies["ledger"]
    authorize_phase(
        repository,
        phase,
        job_id,
        freeze_receipt,
        freeze_sha256,
        ledger,
    )
    base = base_receipt(repository, freeze_receipt)
    _, binding_sha256 = job_binding(
        freeze_receipt,
        freeze_sha256,
        base,
        job_id,
    )
    executable, arguments, metadata, environment = stable_child_command(
        repository,
        freeze_path,
        freeze_receipt,
        job_id,
        dependencies["supervisor"],
        source_environment,
    )
    ledger_root, _, forbidden = roots(freeze_receipt)
    while True:
        inspection = ledger.inspect_ledger(ledger_root, job_id, forbidden)
        state = inspection["ledgerState"]
        if state == "succeeded":
            independent = independent_verify(
                repository,
                freeze_receipt,
                job_id,
            )
            return {
                "status": "succeeded",
                "jobId": job_id,
                "attemptCount": inspection["attemptCount"],
                "independentVerificationStatus": independent["status"],
                "outcomeFieldsReadForAuthorization": False,
            }
        if state == "exhausted":
            independent = independent_verify(
                repository,
                freeze_receipt,
                job_id,
            )
            return {
                "status": "exhausted",
                "jobId": job_id,
                "attemptCount": inspection["attemptCount"],
                "independentVerificationStatus": independent["status"],
                "outcomeFieldsReadForAuthorization": False,
            }
        if state == "attemptOpen":
            finalize_open_attempt(
                repository,
                freeze_receipt,
                job_id,
                inspection,
                dependencies,
            )
            independent_verify(repository, freeze_receipt, job_id)
            continue
        attempt_number = inspection["attemptCount"] + 1
        preflight, preflight_path = record_preflight(
            freeze_receipt,
            freeze_sha256,
            job_id,
            attempt_number,
            dependencies,
        )
        if preflight["status"] != "pass":
            return {
                "status": "preflightFailed",
                "jobId": job_id,
                "prospectiveAttemptNumber": attempt_number,
                "preflightReceipt": str(preflight_path),
                "failureCodes": preflight["failureCodes"],
                "outcomeFieldsReadForAuthorization": False,
            }
        ledger.begin_attempt(
            ledger_root,
            job_id,
            freeze_receipt["freezeId"],
            freeze_sha256,
            binding_sha256,
            metadata["commandSha256"],
            forbidden,
        )
        supervisor_command = supervisor_cli_command(
            repository,
            freeze_receipt,
            ledger_root,
            forbidden,
            job_id,
            attempt_number,
            executable,
            arguments,
        )
        supervisor_result = run_supervisor_cli(
            supervisor_command,
            environment,
            freeze_receipt["protocol"]["execution"][
                "maximumJobWallSeconds"
            ],
        )
        current = ledger.inspect_ledger(ledger_root, job_id, forbidden)
        if current["ledgerState"] != "attemptOpen":
            raise ProtocolError("supervisor unexpectedly terminalized the ledger")
        finalize_open_attempt(
            repository,
            freeze_receipt,
            job_id,
            current,
            dependencies,
            supervisor_result["processId"],
        )
        independent_verify(repository, freeze_receipt, job_id)


def pair_gate_row(
    repository,
    freeze_receipt,
    freeze_sha256,
    job_id,
    dependencies,
):
    ledger_root, _, forbidden = roots(freeze_receipt)
    inspection = dependencies["ledger"].inspect_ledger(
        ledger_root,
        job_id,
        forbidden,
    )
    independent_status = "notRun"
    try:
        independent_status = independent_verify(
            repository,
            freeze_receipt,
            job_id,
        )["status"]
    except ProtocolError:
        independent_status = "invalid"
    if inspection["ledgerState"] == "succeeded":
        state = "succeeded"
    elif inspection["ledgerState"] == "exhausted":
        state = "exhausted"
    else:
        state = "incomplete"
    selected_id = inspection["selectedValidAttemptId"]
    terminal_hash = None
    elapsed_ms = None
    retained_bytes = 0
    if selected_id is not None:
        row = next(
            value
            for value in inspection["attempts"]
            if value["attemptId"] == selected_id
        )
        terminal_hash = row["terminalReceiptSha256"]
        attempt_number = row["attemptNumber"]
        root = dependencies["ledger"].job_root(
            ledger_root,
            job_id,
            forbidden,
        )
        terminal, _ = dependencies["ledger"].read_canonical_json(
            root / f"attempt-{attempt_number:02d}" / "attempt-terminal.json"
        )
        elapsed_ms = terminal["elapsedMs"]
        retained_bytes = terminal["outputInventory"]["bytes"]
    else:
        root = dependencies["ledger"].job_root(
            ledger_root,
            job_id,
            forbidden,
        )
        if root.exists():
            for attempt in root.glob("attempt-*/output"):
                retained_bytes += dependencies["ledger"].directory_inventory(
                    attempt
                )["bytes"]
    control_root = safe_control_root(
        freeze_receipt,
        dependencies["ledger"],
    )
    preflight_receipts = []
    preflight_root = control_root / job_id
    if preflight_root.exists():
        for path in sorted(preflight_root.glob("preflight-*.json")):
            document, raw = read_canonical_json(path, "host preflight receipt")
            dependencies["host"].validate_receipt(document)
            if (
                document["freezeId"] != freeze_receipt["freezeId"]
                or document["freezeReceiptSha256"] != freeze_sha256
                or document["jobId"] != job_id
                or document["preflightToolSha256"]
                != sha256_file(dependencies["host"].__file__)
            ):
                raise ProtocolError("host preflight receipt binding failed")
            preflight_receipts.append(
                {
                    "path": str(path.relative_to(control_root)).replace("\\", "/"),
                    "lengthBytes": len(raw),
                    "sha256": sha256_bytes(raw),
                    "status": document["status"],
                    "prospectiveAttemptNumber": document[
                        "prospectiveAttemptNumber"
                    ],
                }
            )
    return {
        "jobId": job_id,
        "ledgerState": state,
        "selectedValidAttemptId": selected_id,
        "attemptCount": inspection["attemptCount"],
        "terminalReceiptSha256": terminal_hash,
        "elapsedMs": elapsed_ms,
        "retainedOutputBytes": retained_bytes,
        "independentVerificationStatus": independent_status,
        "preflightReceipts": preflight_receipts,
    }


def build_pair_gate(
    repository,
    freeze_path,
    generated_utc,
    dependencies=None,
):
    freeze_receipt, freeze_sha256, dependencies = read_freeze(
        freeze_path,
        dependencies,
    )
    rows = [
        pair_gate_row(
            repository,
            freeze_receipt,
            freeze_sha256,
            job_id,
            dependencies,
        )
        for job_id in freeze_receipt["protocol"]["pairedResourceGate"][
            "jobIds"
        ]
    ]
    if any(row["ledgerState"] == "incomplete" for row in rows) and not any(
        row["ledgerState"] == "exhausted" for row in rows
    ):
        raise ProtocolError("paired resource gate is not terminal yet")
    jobs_passed = all(
        row["ledgerState"] == "succeeded"
        and row["independentVerificationStatus"] == "valid"
        for row in rows
    )
    base = base_receipt(repository, freeze_receipt)
    envelope = base["execution"]["resourceEnvelope"]
    total_retained = sum(row["retainedOutputBytes"] for row in rows)
    _, control_root, _ = roots(freeze_receipt)
    free_disk = shutil.disk_usage(
        dependencies["host"].disk_probe_path(control_root)
    ).free
    resource_passed = (
        total_retained <= envelope["maximumOutputBytes"]
        and free_disk >= envelope["minimumFreeDiskReserveBytes"]
    )
    passed = jobs_passed and resource_passed
    receipt = {
        "schemaVersion": "2.0.0",
        "schemaId": PAIR_GATE_SCHEMA_ID,
        "recordType": "ridebound-wp14r-paired-resource-gate-v2",
        "gateId": "wp14r-paired-b1-c1-resource-gate-v2",
        "generatedUtc": generated_utc,
        "status": "pass" if passed else "fail",
        "freezeId": freeze_receipt["freezeId"],
        "freezeReceiptSha256": freeze_sha256,
        "claimBoundary": PAIR_GATE_CLAIM_BOUNDARY,
        "requiredValidJobs": 2,
        "requiredFailedJobs": 0,
        "jobs": rows,
        "totalElapsedMs": sum(row["elapsedMs"] or 0 for row in rows),
        "totalRetainedOutputBytes": total_retained,
        "maximumRetainedOutputBytes": envelope["maximumOutputBytes"],
        "minimumFreeDiskReserveBytes": envelope[
            "minimumFreeDiskReserveBytes"
        ],
        "freeDiskBytesAtGate": free_disk,
        "resourceEnvelopeStatus": "pass" if resource_passed else "fail",
        "outcomeFieldsRead": False,
        "matrixAuthorized": passed,
        "protocolToolSha256": sha256_file(__file__),
    }
    validate_pair_gate_schema(repository, receipt)
    return receipt


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
    commands = parser.add_subparsers(dest="command", required=True)
    for name in ("plan", "run"):
        command = commands.add_parser(name)
        command.add_argument("--repository", type=pathlib.Path, required=True)
        command.add_argument("--freeze", type=pathlib.Path, required=True)
        command.add_argument("--phase", choices=("paired", "matrix"), required=True)
        command.add_argument("--job-id", required=True)
    child = commands.add_parser("child")
    child.add_argument("--repository", type=pathlib.Path, required=True)
    child.add_argument("--freeze", type=pathlib.Path, required=True)
    child.add_argument("--job-id", required=True)
    gate = commands.add_parser("paired-gate")
    gate.add_argument("--repository", type=pathlib.Path, required=True)
    gate.add_argument("--freeze", type=pathlib.Path, required=True)
    gate.add_argument("--generated-utc", required=True)
    gate.add_argument("--output", type=pathlib.Path)
    verify = commands.add_parser("verify-paired-gate")
    verify.add_argument("--repository", type=pathlib.Path, required=True)
    verify.add_argument("--freeze", type=pathlib.Path, required=True)
    verify.add_argument("--input", type=pathlib.Path)
    return parser


def main(argv=None):
    arguments = build_parser().parse_args(argv)
    try:
        if arguments.command == "child":
            return run_scientific_child(
                arguments.repository,
                arguments.freeze,
                arguments.job_id,
            )
        if arguments.command == "plan":
            result = plan_job(
                arguments.repository,
                arguments.freeze,
                arguments.phase,
                arguments.job_id,
            )
        elif arguments.command == "run":
            result = execute_job(
                arguments.repository,
                arguments.freeze,
                arguments.phase,
                arguments.job_id,
            )
        elif arguments.command == "paired-gate":
            result = build_pair_gate(
                arguments.repository,
                arguments.freeze,
                arguments.generated_utc,
            )
            output = arguments.output
            if output is None:
                freeze_receipt, _, _ = read_freeze(arguments.freeze)
                output = paired_gate_path(freeze_receipt)
            write_exclusive(output, canonical(result) + b"\n")
        else:
            freeze_receipt, freeze_sha256, _ = read_freeze(arguments.freeze)
            result = verify_pair_gate(
                arguments.repository,
                freeze_receipt,
                freeze_sha256,
                arguments.input,
            )
        sys.stdout.buffer.write(canonical(result) + b"\n")
        return 0
    except (OSError, ProtocolError, subprocess.SubprocessError) as error:
        print(f"WP14R_PROTOCOL_ERROR: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
