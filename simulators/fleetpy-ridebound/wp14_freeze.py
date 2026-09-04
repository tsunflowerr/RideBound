#!/usr/bin/env python3
"""Freeze the WP14 development ablation matrix before any outcome is observed.

Every job, configuration, driver and output root is enumerated and hashed here so
the matrix cannot be reshaped after results start arriving. The receipt is written
with exclusive create and is the only input `wp14_run_matrix.py` will execute.

The frozen H6 and E1 roots are declared as forbidden so no run can write into an
already-terminal panel.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import pathlib
import subprocess
import sys

import jsonschema

sys.dont_write_bytecode = True

RECEIPT_VERSION = "1.0.0"
SCHEMA_ID = (
    "https://ridebound.local/schemas/wp14/v1/"
    "development-ablation-freeze.schema.json"
)
FREEZE_ID = "wp14-development-ablation-v1"
GRID_ID = "wp14-development-panel-v1"
MASTER_SEED = 7
CELL_COUNT = 16
ARM_COUNT = 10
ARRIVALS_PER_JOB = 108
MAXIMUM_PARALLEL_JOBS = 4
FLEETPY_COMMIT = "053aa9d4fcfde91c5d303435d5748f9206c071b0"
DOTNET_RUNTIME_VERSION = "10.0.9"
MINIMUM_FREE_DISK_BYTES = 25 * 1024 * 1024 * 1024
MINIMUM_FREE_DISK_RESERVE_BYTES = 5 * 1024 * 1024 * 1024
MAXIMUM_OUTPUT_BYTES = 20 * 1024 * 1024 * 1024
MAXIMUM_JOB_WALL_SECONDS = 45 * 60
MAXIMUM_MATRIX_WALL_SECONDS = 16 * 60 * 60
EXPECTED_FORBIDDEN_ROOTS = {
    pathlib.Path(r"E:\RideBoundData\wp9\confirmatory-h6-panela"),
    pathlib.Path(r"E:\RideBoundData\wp9\confirmatory-h6-panelb"),
    pathlib.Path(r"E:\RideBoundData\wp13\e1-retained-portfolio-panel-a"),
    pathlib.Path(r"E:\RideBoundData\wp13\e1-retained-portfolio-panel-b"),
}

SCHEMA_RELATIVE = (
    "benchmarks/schemas/wp14/v1/"
    "development-ablation-freeze.schema.json"
)
RUN_SUMMARY_SCHEMA_RELATIVE = (
    "benchmarks/schemas/wp14/v1/matrix-run-summary.schema.json"
)
FRONTIER_SCHEMA_RELATIVE = (
    "benchmarks/schemas/wp14/v1/frontier-report.schema.json"
)
STATIC_REPOSITORY_FILES = {
    SCHEMA_RELATIVE,
    RUN_SUMMARY_SCHEMA_RELATIVE,
    FRONTIER_SCHEMA_RELATIVE,
    "simulators/fleetpy-ridebound/environment.lock.yml",
    "simulators/fleetpy-ridebound/capability-matrix.json",
    "simulators/fleetpy-ridebound/capability_probe.py",
    "simulators/fleetpy-ridebound/actual_fleetpy_medium_preflight.py",
    "simulators/fleetpy-ridebound/actual_fleetpy_medium_verify.py",
    "simulators/fleetpy-ridebound/wp14_freeze.py",
    "simulators/fleetpy-ridebound/wp14_run_matrix.py",
    "simulators/fleetpy-ridebound/wp14_frontier_analyze.py",
    "simulators/fleetpy-ridebound/wp14_development_panel_audit.py",
    "simulators/fleetpy-ridebound/tests/test_wp14_freeze.py",
    "simulators/fleetpy-ridebound/tests/test_wp14_run_matrix.py",
    "simulators/fleetpy-ridebound/tests/test_wp14_frontier_analyze.py",
    "simulators/fleetpy-ridebound/tests/test_wp14_development_panel_audit.py",
    "docs/benchmarking/evidence/wp14-004-development-panel-v1-summary.json",
    "docs/research/wp14-ablation-pareto-full-pdf-evidence-2026-08-24.md",
}

# One baseline plus the nine declared C1 factor levels. Every level differs from
# the H6 reference in exactly one way; see docs/tasks/43 section 4.
ARMS = [
    ("b1-ref", "rolling-cost", "wp14-c1-h6-reference-v1",
     "baselineUnconstrainedReference"),
    ("c1-h6ref", "ridebound-hard-vector", "wp14-c1-h6-reference-v1",
     "h6ReferenceLevel"),
    ("c1-freeze300", "ridebound-hard-vector", "wp14-c1-f1-freeze300-v1",
     "f1PickupLockScope300s"),
    ("c1-freeze600", "ridebound-hard-vector", "wp14-c1-f1-freeze600-v1",
     "f1PickupLockScope600s"),
    ("c1-ratchet", "ridebound-hard-vector", "wp14-c1-f2-ratchet-v1",
     "f2PickupRatchet"),
    ("c1-freeze300ratchet", "ridebound-hard-vector",
     "wp14-c1-f1f2-freeze300-ratchet-v1", "f1PlusF2"),
    ("c1-nopickuplock", "ridebound-hard-vector", "wp14-c1-nopickuplock-v1",
     "pickupLockRemovedUpperBound"),
    ("c1-budget60", "ridebound-hard-vector", "wp14-c1-budget60-v1",
     "dropEtaBudget60s"),
    ("c1-budget120", "ridebound-hard-vector", "wp14-c1-budget120-v1",
     "dropEtaBudget120s"),
    ("c1-nobudget", "ridebound-hard-vector", "wp14-c1-nobudget-v1",
     "dropEtaBudgetRemoved"),
]

WP4_CONFIG_BY_POLICY = {
    "rolling-cost": "wp14-development-rolling-cost-v1",
    "ridebound-hard-vector": "wp14-development-ridebound-hard-vector-v1",
}


class FreezeError(RuntimeError):
    """A fail-closed freeze condition."""


def sha256_file(path):
    if not path.is_file():
        raise FreezeError(f"required file not found: {path}")
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def artifact(path):
    if not path.is_file() or path.stat().st_size < 1:
        raise FreezeError(f"required artifact is missing or empty: {path}")
    return {
        "path": str(path),
        "lengthBytes": path.stat().st_size,
        "sha256": sha256_file(path),
    }


def repository_artifact(repository, relative):
    path = (repository / relative).resolve()
    try:
        path.relative_to(repository)
    except ValueError as error:
        raise FreezeError(f"repository path escapes root: {relative}") from error
    value = artifact(path)
    value["path"] = pathlib.PurePosixPath(relative).as_posix()
    return value


def tree_sha256(root, domain, excluded_names=()):
    excluded = set(excluded_names)
    files = sorted(
        (
            path
            for path in root.rglob("*")
            if path.is_file()
            and not excluded.intersection(path.relative_to(root).parts)
        ),
        key=lambda path: path.relative_to(root).as_posix(),
    )
    if not files:
        raise FreezeError(f"tree seal is empty: {root}")
    digest = hashlib.sha256(domain + b"\0")
    for path in files:
        relative = path.relative_to(root).as_posix().encode("utf-8")
        digest.update(len(relative).to_bytes(8, "big"))
        digest.update(relative)
        digest.update(path.stat().st_size.to_bytes(8, "big"))
        with path.open("rb") as stream:
            for block in iter(lambda: stream.read(1024 * 1024), b""):
                digest.update(block)
    return digest.hexdigest()


def require_disjoint(output_root, forbidden_roots):
    for root in forbidden_roots:
        if (
            root == output_root
            or root in output_root.parents
            or output_root in root.parents
        ):
            raise FreezeError(f"output root overlaps a forbidden root: {root}")


def runtime_identity(repository, fleetpy_root, runner_root, python, dotnet):
    commit = subprocess.run(
        ["git", "-C", str(fleetpy_root), "rev-parse", "HEAD"],
        check=True,
        capture_output=True,
        text=True,
    ).stdout.strip()
    dirty = subprocess.run(
        ["git", "-C", str(fleetpy_root), "status", "--porcelain"],
        check=True,
        capture_output=True,
        text=True,
    ).stdout
    python_version = subprocess.run(
        [str(python), "--version"],
        check=True,
        capture_output=True,
        text=True,
    ).stdout.strip().removeprefix("Python ")
    dotnet_sdk = subprocess.run(
        [str(dotnet), "--version"],
        check=True,
        capture_output=True,
        text=True,
    ).stdout.strip()
    runtime_lines = subprocess.run(
        [str(dotnet), "--list-runtimes"],
        check=True,
        capture_output=True,
        text=True,
    ).stdout.splitlines()
    netcore_lines = [
        line for line in runtime_lines if line.startswith("Microsoft.NETCore.App ")
    ]
    netcore_versions = [line.split()[1] for line in netcore_lines]
    if dirty or commit != FLEETPY_COMMIT:
        raise FreezeError("FleetPy source is dirty or differs from the pin")
    if netcore_versions != [DOTNET_RUNTIME_VERSION]:
        raise FreezeError(".NET runtime set differs from the WP14 pin")
    runner_dll = runner_root / "RideBound.Runner.dll"
    if not runner_dll.is_file():
        raise FreezeError("published Runner artifact is missing its DLL")
    runtime_base = pathlib.Path(
        netcore_lines[0].rsplit("[", 1)[1].removesuffix("]")
    )
    runtime_root = runtime_base / netcore_versions[0]
    capability = subprocess.run(
        [
            str(python),
            "-B",
            str(
                repository
                / "simulators/fleetpy-ridebound/capability_probe.py"
            ),
            "--fleetpy-root",
            str(fleetpy_root),
        ],
        check=True,
        capture_output=True,
        text=True,
        encoding="utf-8",
    )
    capability_lines = [
        line for line in capability.stdout.splitlines() if line.strip()
    ]
    if capability_lines[:-1] != ["Loading modules from development content."]:
        raise FreezeError("FleetPy capability probe emitted an unexpected preamble")
    capability_result = json.loads(capability_lines[-1])
    if capability_result.get("status") != "pass":
        raise FreezeError("FleetPy capability probe did not pass")
    return {
        "fleetPyRoot": str(fleetpy_root),
        "fleetPyVersion": "1.0.2",
        "fleetPyCommit": commit,
        "pythonExecutable": str(python),
        "pythonVersion": python_version,
        "pythonExecutableSha256": sha256_file(python),
        "dotnetExecutable": str(dotnet),
        "dotnetSdkVersion": dotnet_sdk,
        "dotnetRuntimeVersion": netcore_versions[0],
        "dotnetExecutableSha256": sha256_file(dotnet),
        "dotnetRuntimeRoot": str(runtime_root),
        "dotnetRuntimeTreeSha256": tree_sha256(
            runtime_root,
            b"RideBound.Wp14DotnetRuntime.v1",
        ),
        "fleetPyCapabilityProbeResultSha256": hashlib.sha256(
            canonical(capability_result)
        ).hexdigest(),
        "runnerRoot": str(runner_root),
    }


def build(
    repository,
    output_root,
    forbidden_roots,
    maximum_parallel_jobs,
    runner_root,
    fleetpy_root,
    python,
    dotnet,
    development_panel_audit,
    resource_planning_evidence,
    frozen_at_utc,
):
    if not isinstance(frozen_at_utc, str) or not frozen_at_utc.endswith("Z"):
        raise FreezeError("frozenAtUtc must be an explicit UTC Z timestamp")
    if maximum_parallel_jobs != MAXIMUM_PARALLEL_JOBS:
        raise FreezeError("maximumParallelJobs must equal the resource-tested pin 4")
    if set(forbidden_roots) != EXPECTED_FORBIDDEN_ROOTS:
        raise FreezeError("forbiddenRoots are not the exact frozen H6/E1 roots")
    require_disjoint(output_root, forbidden_roots)
    grid_path = (
        repository / "benchmarks" / "scenarios" / "wp14-development" / "grid-v1.json"
    )
    grid = json.loads(grid_path.read_text(encoding="utf-8"))
    cells = grid.get("cells")
    if grid.get("gridId") != GRID_ID or not isinstance(cells, list):
        raise FreezeError("grid manifest is not the WP14 development panel")
    if len(cells) != CELL_COUNT:
        raise FreezeError("development grid must contain exactly 16 cells")
    cell_ids = [cell.get("cellId") for cell in cells]
    if any(not isinstance(cell_id, str) or not cell_id for cell_id in cell_ids):
        raise FreezeError("development grid has an invalid cell identifier")
    if len(set(cell_ids)) != CELL_COUNT:
        raise FreezeError("development grid cell identifiers are not unique")

    fixture_root = (
        repository / "benchmarks" / "fixtures" / "wp6" / "public"
        / "fleetpy-manhattan-v1" / GRID_ID
    )
    scenario_root = repository / "benchmarks" / "scenarios" / "wp14-development"
    configuration_root = repository / "benchmarks" / "configurations"

    arms = []
    for arm_id, policy_id, commitment, level in ARMS:
        wp4 = WP4_CONFIG_BY_POLICY[policy_id]
        arms.append({
            "armId": arm_id,
            "policyId": policy_id,
            "factorLevel": level,
            "commitmentConfig":
                f"benchmarks/configurations/{commitment}.json",
            "commitmentConfigSha256":
                sha256_file(configuration_root / f"{commitment}.json"),
            "wp4Config": f"benchmarks/configurations/{wp4}.json",
            "wp4ConfigSha256": sha256_file(configuration_root / f"{wp4}.json"),
        })
    if len(arms) != ARM_COUNT or len({arm["armId"] for arm in arms}) != ARM_COUNT:
        raise FreezeError("WP14 arm identifiers are not the exact ten levels")

    jobs = []
    repository_files = set(STATIC_REPOSITORY_FILES)
    repository_files.add(
        "benchmarks/scenarios/wp14-development/grid-v1.json"
    )
    repository_files.update(arm["commitmentConfig"] for arm in arms)
    repository_files.update(arm["wp4Config"] for arm in arms)
    for cell in cells:
        cell_id = cell["cellId"]
        driver = scenario_root / f"{cell_id}.driver.json"
        scenario = fixture_root / cell_id / "scenario-content.json"
        driver_value = json.loads(driver.read_text(encoding="utf-8"))
        scenario_sha256 = sha256_file(scenario)
        if (
            cell.get("requestTarget") != ARRIVALS_PER_JOB
            or cell.get("vehicleCount") != 8
            or driver_value.get("scenarioId") != cell.get("scenarioId")
            or driver_value.get("sourceScenarioContentSha256")
            != scenario_sha256
            or driver_value.get("expectedRequestCount") != ARRIVALS_PER_JOB
            or driver_value.get("expectedVehicleCount") != 8
            or driver_value.get("measuredRepeatsPerArm") != 1
            or driver_value.get("fleetpyVersion") != "1.0.2"
            or driver_value.get("fleetpyCommit") != FLEETPY_COMMIT
            or driver_value.get("claimClass")
            != "development-exploratory-only"
        ):
            raise FreezeError(f"development cell/driver binding differs: {cell_id}")
        repository_files.add(
            f"benchmarks/scenarios/wp14-development/{cell_id}.driver.json"
        )
        for arm in arms:
            jobs.append({
                "jobId": f"w14-{cell_id}-{arm['armId']}-s{MASTER_SEED}",
                "cellId": cell_id,
                "armId": arm["armId"],
                "driver":
                    f"benchmarks/scenarios/wp14-development/{cell_id}.driver.json",
                "driverSha256": sha256_file(driver),
                "scenarioContentSha256": scenario_sha256,
                "commitmentConfig": arm["commitmentConfig"],
                "wp4Config": arm["wp4Config"],
                "masterSeed": MASTER_SEED,
            })

    if len(jobs) != CELL_COUNT * ARM_COUNT:
        raise FreezeError("WP14 matrix does not contain exactly 160 jobs")
    if len({job["jobId"] for job in jobs}) != len(jobs):
        raise FreezeError("job identifiers are not unique")
    pairs = {(job["cellId"], job["armId"]) for job in jobs}
    expected_pairs = {
        (cell_id, arm["armId"])
        for cell_id in cell_ids
        for arm in arms
    }
    if pairs != expected_pairs:
        raise FreezeError("WP14 matrix is not the exact cell-by-arm product")

    panel_audit_value = json.loads(
        development_panel_audit.read_text(encoding="utf-8")
    )
    overlap = panel_audit_value.get("overlapCountByAxis")
    if (
        panel_audit_value.get("auditVersion") != "1.0.0"
        or panel_audit_value.get("developmentGridId") != GRID_ID
        or panel_audit_value.get("developmentCellCount") != CELL_COUNT
        or panel_audit_value.get("frozenCellCount") != 40
        or not isinstance(overlap, dict)
        or len(overlap) != 7
        or any(value != 0 for value in overlap.values())
    ):
        raise FreezeError(
            "development-panel audit does not prove seven-axis disjointness"
        )

    planning_value = json.loads(
        resource_planning_evidence.read_text(encoding="utf-8")
    )
    panel_a_wall = sorted(
        run.get("wallMilliseconds")
        for run in planning_value.get("runs", [])
        if run.get("panelId") == "A"
    )
    if (
        planning_value.get("reportType")
        != "ridebound-wp13-e1-retained-portfolio-inventory-v1"
        or len(panel_a_wall) != 40
        or any(not isinstance(value, int) or value < 0 for value in panel_a_wall)
        or (panel_a_wall[19] + panel_a_wall[20]) // 2 != 971998
    ):
        raise FreezeError(
            "resource-planning evidence differs from the E1 Panel A basis"
        )

    repository_artifacts = [
        repository_artifact(repository, relative)
        for relative in sorted(repository_files)
    ]
    runtime = runtime_identity(
        repository, fleetpy_root, runner_root, python, dotnet
    )
    receipt = {
        "schemaVersion": RECEIPT_VERSION,
        "schemaId": SCHEMA_ID,
        "freezeId": FREEZE_ID,
        "frozenAtUtc": frozen_at_utc,
        "claimBoundary": [
            "developmentExploratoryOnlyNotConfirmatory",
            "frozenPanelsNeverUsedForTuningOrSelection",
            "reportPairedFrontierNotAPostOutcomeScalar",
            "mustNotReinterpretOrRescueH6",
        ],
        "design": {
            "gridId": GRID_ID,
            "gridManifest": repository_artifact(
                repository,
                "benchmarks/scenarios/wp14-development/grid-v1.json",
            ),
            "cellCount": CELL_COUNT,
            "armCount": ARM_COUNT,
            "jobCount": CELL_COUNT * ARM_COUNT,
            "arrivalsPerJob": ARRIVALS_PER_JOB,
            "arrivalsPerArm": CELL_COUNT * ARRIVALS_PER_JOB,
            "comparisonUnit": "pairedDevelopmentCell",
            "masterSeed": MASTER_SEED,
            "solverSeedsAreReplicates": False,
            "failureTreatment": "retainTypedFailureNoRetryNoReplacement",
            "arms": arms,
            "jobs": jobs,
        },
        "execution": {
            "outputRoot": str(output_root),
            "forbiddenRoots": sorted(str(root) for root in forbidden_roots),
            "maximumParallelJobs": maximum_parallel_jobs,
            "repeatsPerJob": 1,
            "resourceEnvelope": {
                "planningBasis": "observedE1PanelAResourceOnlyNotOutcomeTuning",
                "observedE1PanelAJobCount": 40,
                "observedMedianJobWallMs": 971998,
                "projectedMatrixOutputBytes": 15600000000,
                "projectedMatrixWallMsAtMaximumParallelism": 39600000,
                "minimumFreeDiskBytesBeforeRun": MINIMUM_FREE_DISK_BYTES,
                "minimumFreeDiskReserveBytes": MINIMUM_FREE_DISK_RESERVE_BYTES,
                "maximumOutputBytes": MAXIMUM_OUTPUT_BYTES,
                "maximumJobWallSeconds": MAXIMUM_JOB_WALL_SECONDS,
                "maximumMatrixWallSeconds": MAXIMUM_MATRIX_WALL_SECONDS,
                "cancellationPolicy": (
                    "failClosedBeforeLaunchOrRetainTypedPartialReceipt"
                ),
            },
        },
        "sourceIdentity": {
            "developmentPanelAudit": artifact(development_panel_audit),
            "resourcePlanningEvidence": artifact(resource_planning_evidence),
            "repositoryFiles": repository_artifacts,
            "treeSeals": {
                "adapterPackageSha256": tree_sha256(
                    repository / "simulators/fleetpy-ridebound/ridebound_fleetpy",
                    b"RideBound.Wp14AdapterPackage.v1",
                    {"__pycache__"},
                ),
                "developmentFixtureSha256": tree_sha256(
                    fixture_root,
                    b"RideBound.Wp14DevelopmentFixture.v1",
                ),
                "runnerArtifactSha256": tree_sha256(
                    runner_root,
                    b"RideBound.Wp14RunnerArtifact.v1",
                ),
                "runnerDllSha256": sha256_file(
                    runner_root / "RideBound.Runner.dll"
                ),
            },
        },
        "runtime": runtime,
    }
    schema = json.loads(
        (repository / SCHEMA_RELATIVE).read_text(encoding="utf-8")
    )
    jsonschema.Draft202012Validator(
        schema,
        format_checker=jsonschema.FormatChecker(),
    ).validate(receipt)
    return receipt


def canonical(document):
    return json.dumps(
        document, ensure_ascii=False, sort_keys=True, separators=(",", ":")
    ).encode("utf-8")


def write_exclusive(output, payload):
    output.parent.mkdir(parents=True, exist_ok=True)
    flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL
    flags |= getattr(os, "O_BINARY", 0)
    descriptor = os.open(output, flags)
    try:
        os.write(descriptor, payload)
    finally:
        os.close(descriptor)


def verify_receipt(
    receipt_path,
    repository,
    output_root,
    forbidden_roots,
    maximum_parallel_jobs,
    runner_root,
    fleetpy_root,
    python,
    dotnet,
    development_panel_audit,
    resource_planning_evidence,
):
    actual = json.loads(receipt_path.read_text(encoding="utf-8"))
    expected = build(
        repository,
        output_root,
        forbidden_roots,
        maximum_parallel_jobs,
        runner_root,
        fleetpy_root,
        python,
        dotnet,
        development_panel_audit,
        resource_planning_evidence,
        actual.get("frozenAtUtc"),
    )
    if actual != expected or receipt_path.read_bytes() != canonical(actual) + b"\n":
        raise FreezeError("freeze receipt differs from its sources or is not canonical")
    return {
        "freezeReceiptSha256": sha256_file(receipt_path),
        "jobCount": actual["design"]["jobCount"],
        "repositoryFileCount": len(
            actual["sourceIdentity"]["repositoryFiles"]
        ),
        "status": "pass",
    }


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repository", required=True, type=pathlib.Path)
    parser.add_argument("--output-root", required=True, type=pathlib.Path)
    parser.add_argument("--forbidden-root", action="append", required=True,
                        type=pathlib.Path)
    parser.add_argument("--runner-root", required=True, type=pathlib.Path)
    parser.add_argument("--fleetpy-root", required=True, type=pathlib.Path)
    parser.add_argument("--python", required=True, type=pathlib.Path)
    parser.add_argument("--dotnet", required=True, type=pathlib.Path)
    parser.add_argument(
        "--development-panel-audit", required=True, type=pathlib.Path
    )
    parser.add_argument(
        "--resource-planning-evidence", required=True, type=pathlib.Path
    )
    parser.add_argument("--frozen-at-utc")
    parser.add_argument(
        "--maximum-parallel-jobs", type=int, default=MAXIMUM_PARALLEL_JOBS
    )
    parser.add_argument("--output", required=True, type=pathlib.Path)
    parser.add_argument("--write", action="store_true")
    arguments = parser.parse_args(argv)
    try:
        repository = arguments.repository.resolve()
        output_root = arguments.output_root.resolve()
        forbidden = [root.resolve() for root in arguments.forbidden_root]
        output = arguments.output.resolve()
        try:
            output.relative_to(repository)
        except ValueError as error:
            raise FreezeError("freeze receipt must be inside the repository") from error
        require_disjoint(output, forbidden)
        common = (
            repository,
            output_root,
            forbidden,
            arguments.maximum_parallel_jobs,
            arguments.runner_root.resolve(),
            arguments.fleetpy_root.resolve(),
            arguments.python.resolve(),
            arguments.dotnet.resolve(),
            arguments.development_panel_audit.resolve(),
            arguments.resource_planning_evidence.resolve(),
        )
        if arguments.write:
            if not arguments.frozen_at_utc:
                raise FreezeError("--frozen-at-utc is required with --write")
            document = build(*common, arguments.frozen_at_utc)
            encoded = canonical(document) + b"\n"
            write_exclusive(output, encoded)
            print(
                f"{output} {len(encoded)} "
                f"{hashlib.sha256(encoded).hexdigest()} "
                f"jobs={document['design']['jobCount']}"
            )
        else:
            if arguments.frozen_at_utc:
                raise FreezeError("--frozen-at-utc is only valid with --write")
            print(json.dumps(verify_receipt(output, *common), sort_keys=True))
    except (
        OSError,
        FreezeError,
        ValueError,
        TypeError,
        KeyError,
        subprocess.SubprocessError,
        jsonschema.ValidationError,
    ) as error:
        print(f"wp14_freeze: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
