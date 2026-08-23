"""Run the frozen WP9 matrix with bounded parallelism and independent verification."""

from __future__ import annotations

import argparse
import concurrent.futures
import hashlib
import json
import os
import pathlib
import re
import subprocess
import sys


_JOB_FIELDS = {
    "jobId",
    "phase",
    "cellId",
    "armId",
    "wp4Config",
    "commitmentConfig",
    "driver",
    "masterSeed",
}
_WP4_BY_ARM = {
    "b1": "benchmarks/configurations/wp9-fleetpy-rolling-cost-audited-v1.json",
    "c1": "benchmarks/configurations/wp9-fleetpy-ridebound-hard-vector-audited-v1.json",
    "c2": "benchmarks/configurations/wp9-fleetpy-soft-hard-hybrid-audited-v1.json",
}
_COMMITMENT_BY_LEVEL = {
    "tight": "benchmarks/configurations/wp8-drop-eta-budget-tight-v1.json",
    "unbounded": "benchmarks/configurations/wp6-public-mechanical-commitment-v1.json",
    "loose": "benchmarks/configurations/wp8-drop-eta-budget-loose-v1.json",
}
_CELL_ID = re.compile(r"^d[0-9]{8}-s[1-9][0-9]*-r[1-9][0-9]*$")
_JOB_ID = re.compile(
    r"^(?:p|r|pb)-d[0-9]{8}-s[1-9][0-9]*-r[1-9][0-9]*-"
    r"(?:b1|c1|c2)-(?:tight|unbounded|loose)-s[0-9]+$"
)

# WP8-011d declares a second capacity panel. Fleet size lives inside the frozen
# derivative, not in this plan, so panel B is a separate derivative tree, driver
# set and execution plan rather than a field on panel A's jobs.  Panel A stays
# byte-for-byte identical to what the preregistration froze.
_PANELS = {
    "a": {
        "fixtureRoot": (
            "benchmarks/fixtures/wp6/public/fleetpy-manhattan-v1/"
            "wp9-confirmatory-fixed-panel-v2"
        ),
        "driverSuffix": ".driver.json",
        "jobPrefixes": {"primary": "p", "robustness": "r"},
        "primaryJobCount": 40,
        "robustnessJobCount": 20,
        "robustnessCellCount": 5,
    },
    "b": {
        "fixtureRoot": (
            "benchmarks/fixtures/wp6/public/fleetpy-manhattan-v1/"
            "wp9-confirmatory-fixed-panel-v3-veh4"
        ),
        "driverSuffix": ".veh4.driver.json",
        "jobPrefixes": {"primary": "pb", "robustness": "rb"},
        "primaryJobCount": 40,
        "robustnessJobCount": 0,
        "robustnessCellCount": 0,
    },
}


def _load_plan(path):
    plan = json.loads(path.read_text(encoding="utf-8"))
    if set(plan) != {"schemaVersion", "planId", "jobs"}:
        raise RuntimeError("matrix plan fields differ")
    if plan["schemaVersion"] != "1.0.0":
        raise RuntimeError("matrix plan version differs")
    jobs = plan["jobs"]
    if (
        not isinstance(jobs, list)
        or not jobs
        or any(set(job) != _JOB_FIELDS for job in jobs)
        or len({job["jobId"] for job in jobs}) != len(jobs)
    ):
        raise RuntimeError("matrix jobs are empty, malformed, or duplicate")
    for job in jobs:
        if job["phase"] not in {"primary", "robustness"}:
            raise RuntimeError("matrix phase is invalid")
        if (
            isinstance(job["masterSeed"], bool)
            or not isinstance(job["masterSeed"], int)
            or not 0 <= job["masterSeed"] <= 2_147_483_647
        ):
            raise RuntimeError("matrix masterSeed is invalid")
        for field in _JOB_FIELDS - {"masterSeed"}:
            if not isinstance(job[field], str) or not job[field]:
                raise RuntimeError(f"matrix {field} is invalid")
        if _CELL_ID.fullmatch(job["cellId"]) is None:
            raise RuntimeError("matrix cellId is not a safe frozen identifier")
        if _JOB_ID.fullmatch(job["jobId"]) is None:
            raise RuntimeError("matrix jobId is not a safe frozen identifier")
    return plan


def _validate_frozen_design(plan, panel="a"):
    specification = _PANELS[panel]
    jobs = plan["jobs"]
    primary = [job for job in jobs if job["phase"] == "primary"]
    robustness = [job for job in jobs if job["phase"] == "robustness"]
    primary_cells = {job["cellId"] for job in primary}
    robustness_cells = {job["cellId"] for job in robustness}
    if (
        len(primary) != specification["primaryJobCount"]
        or len(robustness) != specification["robustnessJobCount"]
        or len(primary_cells) != 20
        or len(robustness_cells) != specification["robustnessCellCount"]
        or not robustness_cells.issubset(primary_cells)
    ):
        raise RuntimeError("frozen WP9 matrix denominators differ")

    for cell_id in primary_cells:
        expected = {
            ("b1", "tight", 7),
            ("c1", "tight", 7),
        }
        actual = {
            (
                job["armId"],
                "tight",
                job["masterSeed"],
            )
            for job in primary
            if job["cellId"] == cell_id
        }
        if actual != expected:
            raise RuntimeError(f"primary design differs for cell {cell_id}")

    for cell_id in robustness_cells:
        expected = {
            ("c1", "unbounded", 7),
            ("c2", "loose", 7),
            ("b1", "tight", 19),
            ("c1", "tight", 19),
        }
        actual = set()
        for job in robustness:
            if job["cellId"] != cell_id:
                continue
            level = next(
                (
                    name
                    for name, path in _COMMITMENT_BY_LEVEL.items()
                    if path == job["commitmentConfig"]
                ),
                None,
            )
            actual.add((job["armId"], level, job["masterSeed"]))
        if actual != expected:
            raise RuntimeError(f"robustness design differs for cell {cell_id}")

    suffix = specification["driverSuffix"]
    for job in jobs:
        level = next(
            (
                name
                for name, path in _COMMITMENT_BY_LEVEL.items()
                if path == job["commitmentConfig"]
            ),
            None,
        )
        prefix = specification["jobPrefixes"][job["phase"]]
        expected_job_id = (
            f"{prefix}-{job['cellId']}-{job['armId']}-{level}-s{job['masterSeed']}"
        )
        if (
            level is None
            or job["wp4Config"] != _WP4_BY_ARM.get(job["armId"])
            or job["driver"]
            != f"benchmarks/scenarios/wp9-confirmatory/{job['cellId']}{suffix}"
            or job["jobId"] != expected_job_id
        ):
            raise RuntimeError(f"frozen WP9 job binding differs: {job['jobId']}")


def _sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _repository_inventory_sha256(repository):
    head = subprocess.run(
        ["git", "-C", str(repository), "rev-parse", "HEAD"],
        check=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        timeout=10,
    ).stdout.strip()
    completed = subprocess.run(
        [
            "git",
            "-C",
            str(repository),
            "ls-files",
            "-z",
            "--cached",
            "--others",
            "--exclude-standard",
        ],
        check=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        timeout=30,
    )
    relative_paths = sorted(
        value for value in completed.stdout.split(b"\0") if value
    )
    if len(relative_paths) != len(set(relative_paths)):
        raise RuntimeError("repository inventory contains duplicate paths")
    digest = hashlib.sha256(b"RideBound.Wp9RepositoryInventory.v1\0")
    digest.update(len(head).to_bytes(8, "big"))
    digest.update(head)
    for relative_bytes in relative_paths:
        relative = pathlib.Path(os.fsdecode(relative_bytes))
        path = (repository / relative).resolve()
        try:
            path.relative_to(repository)
        except ValueError as failure:
            raise RuntimeError("repository inventory path escaped root") from failure
        if not path.is_file():
            raise RuntimeError(f"repository inventory file is missing: {relative}")
        digest.update(len(relative_bytes).to_bytes(8, "big"))
        digest.update(relative_bytes)
        length = path.stat().st_size
        digest.update(length.to_bytes(8, "big"))
        with path.open("rb") as source:
            for chunk in iter(lambda: source.read(1024 * 1024), b""):
                digest.update(chunk)
    return digest.hexdigest()


def _validate_output_binding(
    output,
    expected_label,
    expected_scenario_sha256,
    expected_repository_inventory_sha256,
):
    summary = json.loads((output / "summary.json").read_text(encoding="utf-8"))
    if summary.get("label") != expected_label:
        raise RuntimeError(f"output label differs: {expected_label}")
    if summary.get("sourceScenarioContentSha256") != expected_scenario_sha256:
        raise RuntimeError(f"output source scenario differs: {expected_label}")
    if (
        summary.get("repositoryInventorySha256")
        != expected_repository_inventory_sha256
    ):
        raise RuntimeError(f"output repository inventory differs: {expected_label}")


def _resolve_under(root, relative, field):
    path = (root / relative).resolve()
    try:
        path.relative_to(root)
    except ValueError as failure:
        raise RuntimeError(f"{field} escapes repository root") from failure
    if not path.is_file():
        raise RuntimeError(f"{field} does not exist: {relative}")
    return path


def _select_jobs(plan, requested_job_ids):
    if not requested_job_ids:
        return list(plan["jobs"])
    if len(requested_job_ids) != len(set(requested_job_ids)):
        raise RuntimeError("selected WP9 job IDs are duplicate")
    requested = set(requested_job_ids)
    jobs = [job for job in plan["jobs"] if job["jobId"] in requested]
    if len(jobs) != len(requested):
        missing = sorted(requested - {job["jobId"] for job in jobs})
        raise RuntimeError(f"selected WP9 jobs are absent from frozen plan: {missing}")
    return jobs


def _run_job(job, arguments, repository, verifier_path):
    output = (arguments.bundle_root / job["jobId"]).resolve()
    log_root = arguments.bundle_root / "_logs"
    log_root.mkdir(parents=True, exist_ok=True)
    log_path = log_root / f"{job['jobId']}.log"
    wp4 = _resolve_under(repository, job["wp4Config"], "wp4Config")
    commitment = _resolve_under(
        repository, job["commitmentConfig"], "commitmentConfig"
    )
    driver = _resolve_under(repository, job["driver"], "driver")
    fixture = (
        repository / _PANELS[arguments.panel]["fixtureRoot"] / job["cellId"]
    )
    expected_scenario_sha256 = _sha256(fixture / "scenario-content.json")
    environment = dict(os.environ)
    environment["PYTHONDONTWRITEBYTECODE"] = "1"

    if output.exists():
        if not arguments.resume_verified:
            raise RuntimeError(f"output already exists: {output}")
        command = [
            sys.executable,
            str(verifier_path),
            "--bundle",
            str(output),
            "--include-behavioral-hash",
            "--require-audited-solver-evidence",
        ]
        verified = subprocess.run(
            command,
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
            arguments.repository_inventory_sha256,
        )
        return {
            "jobId": job["jobId"],
            "repositoryInventorySha256": arguments.repository_inventory_sha256,
            "status": "reusedVerified",
        }

    command = [
        sys.executable,
        str(repository / "simulators/fleetpy-ridebound/actual_fleetpy_medium_preflight.py"),
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
        arguments.repository_inventory_sha256,
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
            sys.executable,
            str(verifier_path),
            "--bundle",
            str(output),
            "--include-behavioral-hash",
            "--require-audited-solver-evidence",
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
        arguments.repository_inventory_sha256,
    )
    receipt = json.loads(verified.stdout)
    return {
        "behavioralProjectionHash": receipt["behavioralProjectionHash"],
        "jobId": job["jobId"],
        "repositoryInventorySha256": arguments.repository_inventory_sha256,
        "semanticHash": receipt["semanticHash"],
        "status": "completedVerified",
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--plan", required=True, type=pathlib.Path)
    parser.add_argument("--repository", required=True, type=pathlib.Path)
    parser.add_argument("--bundle-root", required=True, type=pathlib.Path)
    parser.add_argument("--fleetpy-root", required=True, type=pathlib.Path)
    parser.add_argument("--runner-root", required=True, type=pathlib.Path)
    parser.add_argument("--dotnet", required=True, type=pathlib.Path)
    parser.add_argument("--parallelism", type=int, default=4)
    parser.add_argument("--resume-verified", action="store_true")
    parser.add_argument("--job-id", action="append", dest="job_ids")
    parser.add_argument("--panel", choices=sorted(_PANELS), default="a")
    arguments = parser.parse_args()
    if not 1 <= arguments.parallelism <= 4:
        parser.error("--parallelism must be in [1,4]")

    repository = arguments.repository.resolve()
    plan = _load_plan(arguments.plan.resolve())
    _validate_frozen_design(plan, arguments.panel)
    selected_jobs = _select_jobs(plan, arguments.job_ids)
    verifier = repository / (
        "simulators/fleetpy-ridebound/actual_fleetpy_medium_verify.py"
    )
    arguments.bundle_root = arguments.bundle_root.resolve()
    arguments.bundle_root.mkdir(parents=True, exist_ok=True)
    arguments.repository_inventory_sha256 = _repository_inventory_sha256(
        repository
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
            ): job["jobId"]
            for job in selected_jobs
        }
        for future in concurrent.futures.as_completed(futures):
            job_id = futures[future]
            try:
                receipt = future.result()
                receipts.append(receipt)
                print(json.dumps(receipt, sort_keys=True), flush=True)
            except Exception as failure:  # noqa: BLE001 - matrix must retain all failures
                failures.append({"jobId": job_id, "error": str(failure)})
                print(json.dumps(failures[-1], sort_keys=True), flush=True)

    summary = {
        "failureCount": len(failures),
        "failures": sorted(failures, key=lambda value: value["jobId"]),
        "planId": plan["planId"],
        "receipts": sorted(receipts, key=lambda value: value["jobId"]),
        "selectedJobIds": sorted(job["jobId"] for job in selected_jobs),
        "successCount": len(receipts),
    }
    print(json.dumps(summary, sort_keys=True, separators=(",", ":")))
    return 0 if not failures and len(receipts) == len(selected_jobs) else 1


if __name__ == "__main__":
    raise SystemExit(main())
