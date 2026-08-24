"""Execute the frozen WP13 E1 retained-portfolio replay matrix."""

from __future__ import annotations

import argparse
import concurrent.futures
import importlib.util
import json
import os
import pathlib
import subprocess
import sys


def _load_module(path, name):
    specification = importlib.util.spec_from_file_location(name, path)
    if specification is None or specification.loader is None:
        raise RuntimeError(f"cannot import {path}")
    module = importlib.util.module_from_spec(specification)
    specification.loader.exec_module(module)
    return module


def _inside(path, root):
    try:
        path.relative_to(root)
        return True
    except ValueError:
        return False


def _select_jobs(jobs, job_ids):
    if not job_ids:
        return list(jobs)
    if len(job_ids) != len(set(job_ids)):
        raise RuntimeError("selected E1 job IDs are duplicate")
    requested = set(job_ids)
    selected = [job for job in jobs if job["jobId"] in requested]
    if {job["jobId"] for job in selected} != requested:
        raise RuntimeError("selected E1 job is absent from the freeze")
    arms_by_unit = {}
    for job in selected:
        arms_by_unit.setdefault(job["unitId"], set()).add(job["armId"])
    if any(arms != {"b1", "c1"} for arms in arms_by_unit.values()):
        raise RuntimeError("selected E1 jobs must contain complete B1/C1 pairs")
    return selected


def _validate_execution_arguments(
    receipt,
    panel,
    bundle_root,
    forbidden_roots,
    parallelism,
):
    execution = receipt["execution"]
    expected_output = pathlib.Path(execution["outputRoots"][panel]).resolve()
    if bundle_root != expected_output:
        raise RuntimeError("E1 output root differs from the freeze")
    expected_forbidden = {
        pathlib.Path(root).resolve() for root in execution["forbiddenRoots"]
    }
    if (
        len(forbidden_roots) != len(expected_forbidden)
        or set(forbidden_roots) != expected_forbidden
    ):
        raise RuntimeError("immutable H6 roots differ from the freeze")
    if not 1 <= parallelism <= execution["maximumParallelJobs"]:
        raise RuntimeError("parallelism exceeds the frozen resource envelope")


def _validate_output_binding(
    output,
    expected_label,
    expected_scenario_sha256,
    expected_repository_inventory_sha256,
):
    summary = json.loads((output / "summary.json").read_text(encoding="utf-8"))
    if (
        summary.get("label") != expected_label
        or summary.get("sourceScenarioContentSha256")
        != expected_scenario_sha256
        or summary.get("repositoryInventorySha256")
        != expected_repository_inventory_sha256
    ):
        raise RuntimeError(f"output binding differs: {expected_label}")


def _run_job(
    job,
    arguments,
    repository,
    verifier_path,
    repository_inventory_sha256,
):
    output = (arguments.bundle_root / job["jobId"]).resolve()
    if output == arguments.bundle_root or not _inside(
        output,
        arguments.bundle_root,
    ):
        raise RuntimeError(f"job output escaped bundle root: {job['jobId']}")
    log_root = arguments.bundle_root / "_logs"
    log_root.mkdir(parents=True, exist_ok=True)
    log_path = log_root / f"{job['jobId']}.log"
    fixture = (repository / job["fixtureRoot"] / job["unitId"]).resolve()
    wp4 = (repository / job["wp4Config"]).resolve()
    commitment = (repository / job["commitmentConfig"]).resolve()
    driver = (repository / job["driver"]).resolve()
    expected_scenario_sha256 = job["sourceScenarioContentSha256"]
    environment = dict(os.environ)
    environment["PYTHONDONTWRITEBYTECODE"] = "1"

    if output.exists():
        if not arguments.resume_verified:
            raise RuntimeError(f"output already exists: {output}")
        verified = subprocess.run(
            [
                str(arguments.python),
                str(verifier_path),
                "--bundle",
                str(output),
                "--include-behavioral-hash",
                "--require-audited-solver-evidence",
                "--require-retained-candidate-portfolio",
            ],
            check=False,
            capture_output=True,
            text=True,
            encoding="utf-8",
            env=environment,
        )
        if verified.returncode != 0:
            raise RuntimeError(
                f"existing output is not independently valid: {job['jobId']}"
            )
        _validate_output_binding(
            output,
            job["jobId"],
            expected_scenario_sha256,
            repository_inventory_sha256,
        )
        verifier_receipt = json.loads(verified.stdout)
        return {
            "behavioralProjectionHash": verifier_receipt[
                "behavioralProjectionHash"
            ],
            "jobId": job["jobId"],
            "repositoryInventorySha256": repository_inventory_sha256,
            "semanticHash": verifier_receipt["semanticHash"],
            "status": "reusedVerified",
        }

    preflight = (
        repository
        / "simulators/fleetpy-ridebound/actual_fleetpy_medium_preflight.py"
    )
    command = [
        str(arguments.python),
        str(preflight),
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
        str(fixture / "scenario-content.json"),
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
        "1",
        "--master-seed",
        str(job["masterSeed"]),
        "--expected-repository-inventory-sha256",
        repository_inventory_sha256,
    ]
    completed = subprocess.run(
        command,
        check=False,
        capture_output=True,
        text=True,
        encoding="utf-8",
        env=environment,
    )
    log_path.write_text(
        completed.stdout + completed.stderr,
        encoding="utf-8",
        newline="\n",
    )
    if completed.returncode != 0:
        raise RuntimeError(
            f"preflight failed ({completed.returncode}): {job['jobId']}"
        )
    verified = subprocess.run(
        [
            str(arguments.python),
            str(verifier_path),
            "--bundle",
            str(output),
            "--include-behavioral-hash",
            "--require-audited-solver-evidence",
            "--require-retained-candidate-portfolio",
        ],
        check=False,
        capture_output=True,
        text=True,
        encoding="utf-8",
        env=environment,
    )
    if verified.returncode != 0:
        log_path.write_text(
            log_path.read_text(encoding="utf-8")
            + verified.stdout
            + verified.stderr,
            encoding="utf-8",
            newline="\n",
        )
        raise RuntimeError(f"independent verifier failed: {job['jobId']}")
    _validate_output_binding(
        output,
        job["jobId"],
        expected_scenario_sha256,
        repository_inventory_sha256,
    )
    receipt = json.loads(verified.stdout)
    return {
        "behavioralProjectionHash": receipt["behavioralProjectionHash"],
        "jobId": job["jobId"],
        "repositoryInventorySha256": repository_inventory_sha256,
        "semanticHash": receipt["semanticHash"],
        "status": "completedVerified",
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--plan", required=True, type=pathlib.Path)
    parser.add_argument("--receipt", required=True, type=pathlib.Path)
    parser.add_argument("--repository", required=True, type=pathlib.Path)
    parser.add_argument("--record-set", required=True, type=pathlib.Path)
    parser.add_argument("--inventory", required=True, type=pathlib.Path)
    parser.add_argument("--bundle-root", required=True, type=pathlib.Path)
    parser.add_argument("--fleetpy-root", required=True, type=pathlib.Path)
    parser.add_argument("--runner-root", required=True, type=pathlib.Path)
    parser.add_argument("--python", required=True, type=pathlib.Path)
    parser.add_argument("--dotnet", required=True, type=pathlib.Path)
    parser.add_argument(
        "--forbidden-root",
        required=True,
        action="append",
        type=pathlib.Path,
    )
    parser.add_argument("--parallelism", type=int, default=4)
    parser.add_argument("--resume-verified", action="store_true")
    parser.add_argument("--job-id", action="append", dest="job_ids")
    arguments = parser.parse_args()
    repository = arguments.repository.resolve()
    freeze = _load_module(
        repository / "simulators/fleetpy-ridebound/wp13_e1_freeze.py",
        "ridebound_wp13_e1_freeze_for_matrix",
    )
    freeze.verify_receipt(
        arguments.receipt.resolve(),
        repository,
        arguments.runner_root.resolve(),
        arguments.fleetpy_root.resolve(),
        arguments.python.resolve(),
        arguments.dotnet.resolve(),
        arguments.record_set.resolve(),
        arguments.inventory.resolve(),
    )
    receipt = json.loads(
        arguments.receipt.resolve().read_text(encoding="utf-8")
    )
    plans = freeze.verify_plans(repository, arguments.record_set.resolve())
    plan = json.loads(arguments.plan.resolve().read_text(encoding="utf-8"))
    panel = plan.get("panelId")
    if panel not in plans or plan != plans[panel]:
        raise RuntimeError("matrix plan differs from verified E1 freeze")
    jobs = list(plan["jobs"])
    jobs = _select_jobs(jobs, arguments.job_ids)
    arguments.bundle_root = arguments.bundle_root.resolve()
    forbidden = [root.resolve() for root in arguments.forbidden_root]
    _validate_execution_arguments(
        receipt,
        panel,
        arguments.bundle_root,
        forbidden,
        arguments.parallelism,
    )
    if any(
        _inside(arguments.bundle_root, root) or _inside(root, arguments.bundle_root)
        for root in forbidden
    ):
        raise RuntimeError("E1 output overlaps an immutable H6 root")
    arguments.bundle_root.mkdir(parents=True, exist_ok=True)
    wp9 = _load_module(
        repository / "simulators/fleetpy-ridebound/wp9_run_matrix.py",
        "ridebound_wp9_inventory_for_e1",
    )
    repository_inventory_sha256 = wp9._repository_inventory_sha256(repository)
    verifier = (
        repository
        / "simulators/fleetpy-ridebound/actual_fleetpy_medium_verify.py"
    )
    receipts = []
    failures = []
    with concurrent.futures.ThreadPoolExecutor(
        max_workers=arguments.parallelism
    ) as executor:
        futures = {
            executor.submit(
                _run_job,
                job,
                arguments,
                repository,
                verifier,
                repository_inventory_sha256,
            ): job["jobId"]
            for job in jobs
        }
        for future in concurrent.futures.as_completed(futures):
            job_id = futures[future]
            try:
                receipt = future.result()
                receipts.append(receipt)
                print(json.dumps(receipt, sort_keys=True), flush=True)
            except Exception as failure:  # noqa: BLE001 - retain every job failure
                failures.append(
                    {"jobId": job_id, "error": str(failure)}
                )
                print(json.dumps(failures[-1], sort_keys=True), flush=True)
    summary = {
        "failureCount": len(failures),
        "failures": sorted(failures, key=lambda value: value["jobId"]),
        "freezeReceiptSha256": freeze._sha256(arguments.receipt.resolve()),
        "panelId": panel,
        "planId": plan["planId"],
        "receipts": sorted(receipts, key=lambda value: value["jobId"]),
        "repositoryInventorySha256": repository_inventory_sha256,
        "selectedJobIds": sorted(job["jobId"] for job in jobs),
        "successCount": len(receipts),
    }
    print(json.dumps(summary, sort_keys=True, separators=(",", ":")))
    return 0 if not failures and len(receipts) == len(jobs) else 1


if __name__ == "__main__":
    raise SystemExit(main())
