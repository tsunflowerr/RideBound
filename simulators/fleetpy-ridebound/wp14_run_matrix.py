#!/usr/bin/env python3
"""Execute only jobs authorized by the canonical WP14 freeze receipt."""

from __future__ import annotations

import argparse
import concurrent.futures
import hashlib
import importlib.util
import json
import os
import pathlib
import shutil
import subprocess
import sys
import time

import jsonschema

sys.dont_write_bytecode = True

SCHEMA_ID_PREFIX = "https://ridebound.local/schemas/"
VERIFIER_TIMEOUT_SECONDS = 5 * 60


class MatrixError(RuntimeError):
    """A fail-closed execution condition."""


def sha256_file(path):
    if not path.is_file():
        raise MatrixError(f"required file not found: {path}")
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def load_module(path, name):
    specification = importlib.util.spec_from_file_location(name, path)
    if specification is None or specification.loader is None:
        raise MatrixError(f"cannot load module: {path}")
    module = importlib.util.module_from_spec(specification)
    specification.loader.exec_module(module)
    return module


# A freeze receipt is only meaningful against the builder that wrote it.
# Freeze v1 sealed an adapter with the stale-position defect, so its own
# builder can no longer re-derive it; freeze v2 seals the corrected tree.
# Choosing by declared identity keeps that safe, because the selected
# builder still rebuilds the whole receipt from source and compares it.
FREEZE_MODULES = {
    "wp14-development-ablation-v1": "wp14_freeze.py",
    "wp14-development-ablation-v2": "wp14_freeze_v2.py",
}

# The run summary names the freeze it belongs to, so its contract moves with
# the design rather than being pinned to whichever one came first.
SUMMARY_CONTRACTS = {
    "wp14-development-ablation-v1": (
        "benchmarks/schemas/wp14/v1/matrix-run-summary.schema.json",
        "ridebound-wp14-matrix-run-summary-v1",
        "1.0.0",
    ),
    "wp14-development-ablation-v2": (
        "benchmarks/schemas/wp14/v2/matrix-run-summary.schema.json",
        "ridebound-wp14-matrix-run-summary-v2",
        "2.0.0",
    ),
}


def summary_contract(receipt):
    declared = receipt.get("freezeId")
    contract = SUMMARY_CONTRACTS.get(declared)
    if contract is None:
        raise MatrixError(f"no run summary contract owns freeze: {declared}")
    return contract


def load_freeze_module(repository, receipt):
    declared = receipt.get("freezeId")
    filename = FREEZE_MODULES.get(declared)
    if filename is None:
        raise MatrixError(f"no builder owns freeze identity: {declared}")
    return load_module(
        repository / "simulators/fleetpy-ridebound" / filename,
        f"ridebound_wp14_freeze_for_matrix_{declared}",
    )

def resolve_under(repository, relative, label):
    path = (repository / relative).resolve()
    try:
        path.relative_to(repository)
    except ValueError as error:
        raise MatrixError(f"{label} escapes the repository: {relative}") from error
    if not path.is_file():
        raise MatrixError(f"{label} not found: {relative}")
    return path


def directory_size(root):
    if not root.exists():
        return 0
    return sum(path.stat().st_size for path in root.rglob("*") if path.is_file())


def write_exclusive(output, payload):
    output.parent.mkdir(parents=True, exist_ok=True)
    flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL
    flags |= getattr(os, "O_BINARY", 0)
    descriptor = os.open(output, flags)
    try:
        os.write(descriptor, payload)
    finally:
        os.close(descriptor)


def canonical(document):
    return json.dumps(
        document, ensure_ascii=False, sort_keys=True, separators=(",", ":")
    ).encode("utf-8")


def overlaps(first, second):
    return first == second or first in second.parents or second in first.parents


def validate_environment(receipt, output_root, forbidden_roots, parallelism):
    execution = receipt["execution"]
    if output_root != pathlib.Path(execution["outputRoot"]).resolve():
        raise MatrixError("output root differs from the freeze")
    expected = {
        pathlib.Path(root).resolve() for root in execution["forbiddenRoots"]
    }
    if set(forbidden_roots) != expected:
        raise MatrixError("forbidden roots differ from the freeze")
    if any(overlaps(output_root, root) for root in forbidden_roots):
        raise MatrixError("output root overlaps a frozen H6/E1 root")
    if not 1 <= parallelism <= execution["maximumParallelJobs"]:
        raise MatrixError("parallelism exceeds the frozen envelope")


def select_jobs(receipt, job_ids):
    jobs = receipt["design"]["jobs"]
    if not job_ids:
        return list(jobs)
    if len(job_ids) != len(set(job_ids)):
        raise MatrixError("selected job identifiers are duplicated")
    requested = set(job_ids)
    selected = [job for job in jobs if job["jobId"] in requested]
    if {job["jobId"] for job in selected} != requested:
        raise MatrixError("a selected job is absent from the freeze")
    return selected


def verify_bundle(verifier, python, bundle, environment):
    completed = subprocess.run(
        [
            str(python),
            "-B",
            str(verifier),
            "--bundle",
            str(bundle),
            "--include-behavioral-hash",
            "--require-audited-solver-evidence",
        ],
        check=False,
        capture_output=True,
        text=True,
        encoding="utf-8",
        env=environment,
        timeout=VERIFIER_TIMEOUT_SECONDS,
    )
    if completed.returncode != 0:
        detail = (completed.stderr or completed.stdout or "").strip()
        raise MatrixError(f"independent bundle verification failed: {detail}")


def validate_output_binding(bundle, job):
    summary = json.loads(
        (bundle / "summary.json").read_text(encoding="utf-8")
    )
    if (
        summary.get("status") != "pass"
        or summary.get("label") != job["jobId"]
        or summary.get("repeatCount") != 1
        or summary.get("sourceScenarioContentSha256")
        != job["scenarioContentSha256"]
    ):
        raise MatrixError(f"bundle belongs to a different job: {job['jobId']}")
    inventory = summary.get("repositoryInventorySha256")
    if (
        not isinstance(inventory, str)
        or len(inventory) != 64
        or any(character not in "0123456789abcdef" for character in inventory)
    ):
        raise MatrixError(f"bundle repository inventory is invalid: {job['jobId']}")
    return inventory


def resource_preflight(output_root, envelope):
    usage = shutil.disk_usage(output_root)
    output_bytes = directory_size(output_root)
    if usage.free < envelope["minimumFreeDiskBytesBeforeRun"]:
        raise MatrixError("free disk is below the frozen launch minimum")
    if output_bytes > envelope["maximumOutputBytes"]:
        raise MatrixError("matrix output already exceeds the frozen maximum")
    return usage.free, output_bytes


def run_job(job, arguments, repository, receipt, matrix_started):
    envelope = receipt["execution"]["resourceEnvelope"]
    elapsed_matrix = time.monotonic() - matrix_started
    remaining_matrix = envelope["maximumMatrixWallSeconds"] - elapsed_matrix
    if remaining_matrix <= 0:
        raise MatrixError("matrix wall-time envelope expired before launch")
    resource_preflight(arguments.output_root, envelope)

    output = (arguments.output_root / job["jobId"]).resolve()
    log_root = arguments.output_root / "_logs"
    log_root.mkdir(parents=True, exist_ok=True)
    log_path = log_root / f"{job['jobId']}.log"
    environment = dict(os.environ)
    environment["PYTHONDONTWRITEBYTECODE"] = "1"

    arm = next(
        value
        for value in receipt["design"]["arms"]
        if value["armId"] == job["armId"]
    )
    wp4 = resolve_under(repository, job["wp4Config"], "wp4Config")
    commitment = resolve_under(
        repository, job["commitmentConfig"], "commitmentConfig"
    )
    driver = resolve_under(repository, job["driver"], "driver")
    if sha256_file(wp4) != arm["wp4ConfigSha256"]:
        raise MatrixError(f"WP4 config differs from freeze: {job['jobId']}")
    if sha256_file(commitment) != arm["commitmentConfigSha256"]:
        raise MatrixError(f"commitment config differs from freeze: {job['jobId']}")
    if sha256_file(driver) != job["driverSha256"]:
        raise MatrixError(f"driver differs from freeze: {job['jobId']}")

    fixture = (
        repository
        / "benchmarks"
        / "fixtures"
        / "wp6"
        / "public"
        / "fleetpy-manhattan-v1"
        / receipt["design"]["gridId"]
        / job["cellId"]
    )
    scenario = fixture / "scenario-content.json"
    if sha256_file(scenario) != job["scenarioContentSha256"]:
        raise MatrixError(f"scenario differs from freeze: {job['jobId']}")

    verifier = (
        repository
        / "simulators"
        / "fleetpy-ridebound"
        / "actual_fleetpy_medium_verify.py"
    )
    started = time.monotonic()
    if output.exists():
        verify_bundle(verifier, arguments.python, output, environment)
        validate_output_binding(output, job)
        transcript = output / "transcript-00.ndjson"
        return {
            "jobId": job["jobId"],
            "status": "reusedVerified",
            "elapsedMs": round((time.monotonic() - started) * 1000),
            "transcriptBytes": transcript.stat().st_size,
            "bundleBytes": directory_size(output),
        }
    if log_path.exists():
        raise MatrixError(f"prior failed attempt cannot be retried: {job['jobId']}")

    command = [
        str(arguments.python),
        "-B",
        str(
            repository
            / "simulators"
            / "fleetpy-ridebound"
            / "actual_fleetpy_medium_preflight.py"
        ),
        "--label",
        job["jobId"],
        "--fleetpy-root",
        str(arguments.fleetpy_root),
        "--runner-root",
        str(arguments.runner_root),
        "--dotnet",
        str(arguments.dotnet),
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
        str(receipt["execution"]["repeatsPerJob"]),
        "--master-seed",
        str(job["masterSeed"]),
    ]
    timeout = min(envelope["maximumJobWallSeconds"], remaining_matrix)
    try:
        completed = subprocess.run(
            command,
            check=False,
            capture_output=True,
            text=True,
            encoding="utf-8",
            env=environment,
            timeout=timeout,
        )
        log = (completed.stdout or "") + (completed.stderr or "")
    except subprocess.TimeoutExpired as error:
        stdout = error.stdout or ""
        stderr = error.stderr or ""
        if isinstance(stdout, bytes):
            stdout = stdout.decode("utf-8", errors="replace")
        if isinstance(stderr, bytes):
            stderr = stderr.decode("utf-8", errors="replace")
        write_exclusive(log_path, (stdout + stderr).encode("utf-8"))
        raise MatrixError(f"job exceeded frozen wall time: {job['jobId']}") from error
    write_exclusive(log_path, log.encode("utf-8"))
    if completed.returncode != 0:
        raise MatrixError(
            f"job failed: {job['jobId']} (see _logs/{job['jobId']}.log)"
        )

    verify_bundle(verifier, arguments.python, output, environment)
    validate_output_binding(output, job)
    transcript = output / "transcript-00.ndjson"
    result = {
        "jobId": job["jobId"],
        "status": "executedVerified",
        "elapsedMs": round((time.monotonic() - started) * 1000),
        "transcriptBytes": transcript.stat().st_size,
        "bundleBytes": directory_size(output),
    }
    usage = shutil.disk_usage(arguments.output_root)
    if usage.free < envelope["minimumFreeDiskReserveBytes"]:
        raise MatrixError("free disk fell below the frozen reserve")
    if directory_size(arguments.output_root) > envelope["maximumOutputBytes"]:
        raise MatrixError("matrix output exceeded the frozen maximum")
    return result


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repository", required=True, type=pathlib.Path)
    parser.add_argument("--freeze", required=True, type=pathlib.Path)
    parser.add_argument("--output-root", required=True, type=pathlib.Path)
    parser.add_argument(
        "--forbidden-root", action="append", required=True, type=pathlib.Path
    )
    parser.add_argument("--fleetpy-root", required=True, type=pathlib.Path)
    parser.add_argument("--runner-root", required=True, type=pathlib.Path)
    parser.add_argument("--python", required=True, type=pathlib.Path)
    parser.add_argument("--dotnet", required=True, type=pathlib.Path)
    parser.add_argument(
        "--development-panel-audit", required=True, type=pathlib.Path
    )
    parser.add_argument(
        "--resource-planning-evidence", required=True, type=pathlib.Path
    )
    parser.add_argument("--parallelism", type=int, default=1)
    parser.add_argument("--job", action="append", default=[])
    parser.add_argument("--summary", required=True, type=pathlib.Path)
    arguments = parser.parse_args(argv)
    try:
        repository = arguments.repository.resolve()
        freeze_path = arguments.freeze.resolve()
        arguments.output_root = arguments.output_root.resolve()
        arguments.fleetpy_root = arguments.fleetpy_root.resolve()
        arguments.runner_root = arguments.runner_root.resolve()
        arguments.python = arguments.python.resolve()
        arguments.dotnet = arguments.dotnet.resolve()
        forbidden = [root.resolve() for root in arguments.forbidden_root]
        summary_path = arguments.summary.resolve()
        if summary_path.exists():
            raise MatrixError("run summary already exists")
        if overlaps(summary_path, arguments.output_root):
            raise MatrixError("run summary must stay outside the bundle root")
        if any(overlaps(summary_path, root) for root in forbidden):
            raise MatrixError("run summary overlaps a frozen H6/E1 root")

        receipt = json.loads(freeze_path.read_text(encoding="utf-8"))
        freeze = load_freeze_module(repository, receipt)
        freeze.verify_receipt(
            freeze_path,
            repository,
            arguments.output_root,
            forbidden,
            freeze.MAXIMUM_PARALLEL_JOBS,
            arguments.runner_root,
            arguments.fleetpy_root,
            arguments.python,
            arguments.dotnet,
            arguments.development_panel_audit.resolve(),
            arguments.resource_planning_evidence.resolve(),
        )
        validate_environment(
            receipt, arguments.output_root, forbidden, arguments.parallelism
        )
        jobs = select_jobs(receipt, arguments.job)
        arguments.output_root.mkdir(parents=True, exist_ok=True)
        envelope = receipt["execution"]["resourceEnvelope"]
        free_before, output_before = resource_preflight(
            arguments.output_root, envelope
        )

        matrix_started = time.monotonic()
        results = []
        failures = []
        with concurrent.futures.ThreadPoolExecutor(
            max_workers=arguments.parallelism
        ) as pool:
            futures = {
                pool.submit(
                    run_job,
                    job,
                    arguments,
                    repository,
                    receipt,
                    matrix_started,
                ): job
                for job in jobs
            }
            for future in concurrent.futures.as_completed(futures):
                job = futures[future]
                try:
                    result = future.result()
                except Exception as error:  # noqa: BLE001 - typed in receipt
                    failures.append({"jobId": job["jobId"], "error": str(error)})
                    print(f"FAIL {job['jobId']}: {error}", file=sys.stderr)
                    continue
                results.append(result)
                print(
                    f"{result['status']:16s} {result['jobId']} "
                    f"{result['elapsedMs']}ms {result['transcriptBytes']}B",
                    flush=True,
                )

        elapsed_ms = round((time.monotonic() - matrix_started) * 1000)
        free_after = shutil.disk_usage(arguments.output_root).free
        output_after = directory_size(arguments.output_root)
        schema_relative, report_type, schema_version = summary_contract(receipt)
        summary = {
            "schemaVersion": schema_version,
            "schemaId": SCHEMA_ID_PREFIX + schema_relative.split("schemas/")[1],
            "reportType": report_type,
            "freezeId": receipt["freezeId"],
            "freezeReceiptSha256": sha256_file(freeze_path),
            "claimBoundary": receipt["claimBoundary"],
            "requestedJobs": len(jobs),
            "selectedJobIds": sorted(job["jobId"] for job in jobs),
            "completed": len(results),
            "failed": len(failures),
            "failures": sorted(failures, key=lambda value: value["jobId"]),
            "results": sorted(results, key=lambda value: value["jobId"]),
            "resourceObservation": {
                "freeDiskBeforeBytes": free_before,
                "freeDiskAfterBytes": free_after,
                "outputBytesBefore": output_before,
                "outputBytesAfter": output_after,
                "transcriptBytes": sum(
                    result["transcriptBytes"] for result in results
                ),
                "elapsedWallMs": elapsed_ms,
            },
        }
        schema = json.loads(
            (repository / schema_relative).read_text(encoding="utf-8")
        )
        jsonschema.Draft202012Validator(schema).validate(summary)
        write_exclusive(summary_path, canonical(summary) + b"\n")
        print(
            f"completed={len(results)} failed={len(failures)} "
            f"of {len(jobs)} requested"
        )
        return 1 if failures else 0
    except (
        OSError,
        MatrixError,
        ValueError,
        TypeError,
        KeyError,
        StopIteration,
        subprocess.SubprocessError,
        jsonschema.ValidationError,
    ) as error:
        print(f"wp14_run_matrix: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
