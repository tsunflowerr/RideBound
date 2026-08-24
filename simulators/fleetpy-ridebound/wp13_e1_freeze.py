"""Build and verify the WP13 E1 exploratory instrumentation freeze."""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import subprocess

import jsonschema


_SOURCE_PLANS = {
    "A": "benchmarks/scenarios/wp9-confirmatory/execution-plan-v1.json",
    "B": (
        "benchmarks/scenarios/wp9-confirmatory/"
        "execution-plan-panel-b-v1.json"
    ),
}
_E1_PLANS = {
    "A": "benchmarks/scenarios/wp13-e1/execution-plan-panel-a-v1.json",
    "B": "benchmarks/scenarios/wp13-e1/execution-plan-panel-b-v1.json",
}
_FIXTURE_ROOTS = {
    "A": (
        "benchmarks/fixtures/wp6/public/fleetpy-manhattan-v1/"
        "wp9-confirmatory-fixed-panel-v2"
    ),
    "B": (
        "benchmarks/fixtures/wp6/public/fleetpy-manhattan-v1/"
        "wp9-confirmatory-fixed-panel-v3-veh4"
    ),
}
_SOURCE_CONFIGS = {
    "b1": (
        "benchmarks/configurations/"
        "wp9-fleetpy-rolling-cost-audited-v1.json"
    ),
    "c1": (
        "benchmarks/configurations/"
        "wp9-fleetpy-ridebound-hard-vector-audited-v1.json"
    ),
}
_E1_CONFIGS = {
    "b1": (
        "benchmarks/configurations/"
        "wp13-e1-fleetpy-rolling-cost-retained-v1.json"
    ),
    "c1": (
        "benchmarks/configurations/"
        "wp13-e1-fleetpy-ridebound-hard-vector-retained-v1.json"
    ),
}
_TIGHT_COMMITMENT = (
    "benchmarks/configurations/wp8-drop-eta-budget-tight-v1.json"
)
_RECORD_SET_ID = "first-divergence-record-set-v1"
_INVENTORY_ID = "h6-evidence-inventory-v1"
_OUTPUT_ROOTS = {
    "A": r"E:\RideBoundData\wp13\e1-retained-portfolio-panel-a",
    "B": r"E:\RideBoundData\wp13\e1-retained-portfolio-panel-b",
}
_FORBIDDEN_ROOTS = [
    r"E:\RideBoundData\wp9\confirmatory-h6-panela",
    r"E:\RideBoundData\wp9\confirmatory-h6-panelb",
]
_REPOSITORY_FILES = {
    *_SOURCE_PLANS.values(),
    *_E1_PLANS.values(),
    *_SOURCE_CONFIGS.values(),
    *_E1_CONFIGS.values(),
    _TIGHT_COMMITMENT,
    "benchmarks/schemas/wp13/v1/exploratory-replay-freeze.schema.json",
    (
        "benchmarks/schemas/wp13/v1/"
        "runner-retained-candidate-portfolio-evidence.schema.json"
    ),
    "simulators/fleetpy-ridebound/environment.lock.yml",
    "simulators/fleetpy-ridebound/actual_fleetpy_medium_preflight.py",
    "simulators/fleetpy-ridebound/actual_fleetpy_medium_verify.py",
    "simulators/fleetpy-ridebound/wp13_e1_freeze.py",
    "simulators/fleetpy-ridebound/wp13_e1_run_matrix.py",
    "src/RideBound.Algorithms/Policies/RollingCostDecisionModels.cs",
    "src/RideBound.Algorithms/Policies/SolverBackedFleetSelection.cs",
    "src/RideBound.Algorithms/Policies/SolverBackedRidePoolingPolicy.cs",
    "src/RideBound.Contracts/Protocol/DecisionMessages.cs",
    "src/RideBound.Runner/Configuration/Wp4RunnerConfiguration.cs",
    "src/RideBound.Runner/Protocol/SolverExecutionEvidenceMapper.cs",
}
_PLAN_FIELDS = {"schemaVersion", "planId", "panelId", "jobs"}
_JOB_FIELDS = {
    "jobId",
    "panelId",
    "unitId",
    "armId",
    "wp4Config",
    "sourceWp4Config",
    "commitmentConfig",
    "driver",
    "fixtureRoot",
    "masterSeed",
    "sourceScenarioContentSha256",
}
_HASH = "0123456789abcdef"


def _load(path):
    return json.loads(path.read_text(encoding="utf-8"))


def _sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _artifact(path, artifact_id):
    if not path.is_file() or path.stat().st_size < 1:
        raise RuntimeError(f"source artifact is missing/empty: {artifact_id}")
    return {
        "artifactId": artifact_id,
        "lengthBytes": path.stat().st_size,
        "sha256": _sha256(path),
    }


def _repository_file(repository, relative):
    path = (repository / relative).resolve()
    try:
        path.relative_to(repository)
    except ValueError as failure:
        raise RuntimeError("repository binding escaped root") from failure
    value = _artifact(path, relative)
    return {
        "path": relative,
        "lengthBytes": value["lengthBytes"],
        "sha256": value["sha256"],
    }


def _tree_sha256(root, domain, excluded_names=()):
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
        raise RuntimeError(f"tree seal is empty: {root}")
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


def _record_index(record_set_path):
    value = _load(record_set_path)
    records = value.get("records")
    if (
        value.get("reportType")
        != "ridebound-wp13-first-divergence-record-set-v1"
        or not isinstance(records, list)
        or len(records) != 40
    ):
        raise RuntimeError("first-divergence record set is not the exact 40 targets")
    result = {}
    labels = set()
    for record in records:
        key = (record.get("panelId"), record.get("unitId"))
        if (
            key in result
            or key[0] not in {"A", "B"}
            or not isinstance(key[1], str)
            or not key[1]
            or any(
                not isinstance(record.get(name), str)
                or not record[name]
                for name in ("b1Label", "c1Label")
            )
            or any(label in labels for label in (record["b1Label"], record["c1Label"]))
        ):
            raise RuntimeError("record target identity is malformed or duplicate")
        scenario_hash = record.get("sourceScenarioContentSha256")
        if (
            not isinstance(scenario_hash, str)
            or len(scenario_hash) != 64
            or any(character not in _HASH for character in scenario_hash)
        ):
            raise RuntimeError("record target scenario hash is invalid")
        result[key] = record
        labels.update((record["b1Label"], record["c1Label"]))
    if {panel: sum(key[0] == panel for key in result) for panel in ("A", "B")} != {
        "A": 20,
        "B": 20,
    }:
        raise RuntimeError("record target panel counts differ")
    return result


def _verify_config_diffs(repository):
    for arm in ("b1", "c1"):
        source = _load(repository / _SOURCE_CONFIGS[arm])
        instrumented = _load(repository / _E1_CONFIGS[arm])
        expected = dict(source)
        expected["solverExecutionEvidenceProfile"] = "retained-portfolio-v1"
        if instrumented != expected:
            raise RuntimeError(
                f"{arm} config differs beyond retained-portfolio evidence opt-in"
            )


def build_plans(repository, record_set_path):
    _verify_config_diffs(repository)
    records = _record_index(record_set_path)
    result = {}
    seen_labels = set()
    for panel in ("A", "B"):
        source = _load(repository / _SOURCE_PLANS[panel])
        jobs = [job for job in source.get("jobs", []) if job.get("phase") == "primary"]
        if len(jobs) != 40:
            raise RuntimeError(f"Panel {panel} source primary count differs")
        built = []
        for job in jobs:
            key = (panel, job.get("cellId"))
            record = records.get(key)
            arm = job.get("armId")
            expected_label = None if record is None else record.get(f"{arm}Label")
            if (
                arm not in {"b1", "c1"}
                or job.get("jobId") != expected_label
                or job.get("wp4Config") != _SOURCE_CONFIGS[arm]
                or job.get("commitmentConfig") != _TIGHT_COMMITMENT
                or job.get("masterSeed") != 7
                or job["jobId"] in seen_labels
            ):
                raise RuntimeError(f"Panel {panel} source job differs from record set")
            seen_labels.add(job["jobId"])
            scenario = (
                repository
                / _FIXTURE_ROOTS[panel]
                / job["cellId"]
                / "scenario-content.json"
            )
            driver = _load(repository / job["driver"])
            if (
                _sha256(scenario) != record["sourceScenarioContentSha256"]
                or driver.get("sourceScenarioContentSha256")
                != record["sourceScenarioContentSha256"]
                or driver.get("expectedRequestCount") != 108
                or driver.get("expectedVehicleCount")
                != (8 if panel == "A" else 4)
                or driver.get("measuredRepeatsPerArm") != 1
                or driver.get("fleetpyVersion") != "1.0.2"
                or driver.get("fleetpyCommit")
                != "053aa9d4fcfde91c5d303435d5748f9206c071b0"
            ):
                raise RuntimeError(
                    f"Panel {panel} source scenario/driver binding differs"
                )
            built.append(
                {
                    "jobId": job["jobId"],
                    "panelId": panel,
                    "unitId": job["cellId"],
                    "armId": arm,
                    "wp4Config": _E1_CONFIGS[arm],
                    "sourceWp4Config": _SOURCE_CONFIGS[arm],
                    "commitmentConfig": job["commitmentConfig"],
                    "driver": job["driver"],
                    "fixtureRoot": _FIXTURE_ROOTS[panel],
                    "masterSeed": 7,
                    "sourceScenarioContentSha256": record[
                        "sourceScenarioContentSha256"
                    ],
                }
            )
        result[panel] = {
            "schemaVersion": "1.0.0",
            "planId": f"wp13-e1-retained-portfolio-panel-{panel.lower()}-v1",
            "panelId": panel,
            "jobs": built,
        }
    if len(seen_labels) != 80:
        raise RuntimeError("E1 plans do not cover all 80 source labels")
    return result


def _encoded(value):
    return (
        json.dumps(value, ensure_ascii=False, sort_keys=True, indent=2) + "\n"
    )


def write_plans(repository, record_set_path):
    plans = build_plans(repository, record_set_path)
    for panel, plan in plans.items():
        path = repository / _E1_PLANS[panel]
        if path.exists():
            raise RuntimeError(f"E1 plan already exists: {path}")
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(_encoded(plan), encoding="utf-8", newline="\n")


def verify_plans(repository, record_set_path):
    expected = build_plans(repository, record_set_path)
    for panel, value in expected.items():
        path = repository / _E1_PLANS[panel]
        if _load(path) != value or path.read_text(encoding="utf-8") != _encoded(value):
            raise RuntimeError(f"Panel {panel} E1 plan is not canonical/exact")
        if set(value) != _PLAN_FIELDS or any(
            set(job) != _JOB_FIELDS for job in value["jobs"]
        ):
            raise RuntimeError(f"Panel {panel} E1 plan fields differ")
    return expected


def _runtime(fleetpy_root, python, dotnet):
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
    dotnet_sdk_version = subprocess.run(
        [str(dotnet), "--version"],
        check=True,
        capture_output=True,
        text=True,
    ).stdout.strip()
    dotnet_runtimes = subprocess.run(
        [str(dotnet), "--list-runtimes"],
        check=True,
        capture_output=True,
        text=True,
    ).stdout.splitlines()
    netcore_versions = [
        line.split()[1]
        for line in dotnet_runtimes
        if line.startswith("Microsoft.NETCore.App ")
    ]
    if netcore_versions != ["10.0.9"]:
        raise RuntimeError(".NET runtime set differs from the exact E1 pin")
    if dirty or commit != "053aa9d4fcfde91c5d303435d5748f9206c071b0":
        raise RuntimeError("FleetPy source is dirty or not the exact 1.0.2 pin")
    return {
        "fleetPyVersion": "1.0.2",
        "fleetPyCommit": commit,
        "pythonVersion": python_version,
        "pythonExecutableSha256": _sha256(python),
        "dotnetSdkVersion": dotnet_sdk_version,
        "dotnetRuntimeVersion": netcore_versions[0],
        "dotnetExecutableSha256": _sha256(dotnet),
        "repositoryInventoryAlgorithm": "RideBound.Wp9RepositoryInventory.v1",
    }


def build_receipt(
    repository,
    runner_root,
    fleetpy_root,
    python,
    dotnet,
    record_set_path,
    inventory_path,
    frozen_at_utc,
):
    if not isinstance(frozen_at_utc, str) or not frozen_at_utc.endswith("Z"):
        raise RuntimeError("frozenAtUtc must be an explicit UTC Z timestamp")
    plans = verify_plans(repository, record_set_path)
    repository_files = set(_REPOSITORY_FILES)
    repository_files.update(
        job["driver"]
        for plan in plans.values()
        for job in plan["jobs"]
    )
    receipt = {
        "schemaVersion": "1.0.0",
        "freezeId": "wp13-e1-retained-portfolio-replay-v1",
        "frozenAtUtc": frozen_at_utc,
        "claimBoundary": {
            "experiment": "postOutcomeExploratoryInstrumentationReplay",
            "h6": "readOnlyImmutableHistoricalEvidence",
            "interpretation": "descriptiveOnlyNotConfirmatoryNotCausal",
            "policySelection": "unchangedB1C1NoRelaxationNoReranking",
        },
        "design": {
            "panelIds": ["A", "B"],
            "pairedTargetCount": 40,
            "plannedArmRunCount": 80,
            "armIds": ["b1", "c1"],
            "masterSeed": 7,
            "requestCountPerRun": 108,
            "solverSeedsAreReplicates": False,
            "evidenceProfile": "retained-portfolio-v1",
            "failureTreatment": "retainTypedFailureNoRetryNoReplacement",
        },
        "execution": {
            "outputRoots": _OUTPUT_ROOTS,
            "forbiddenRoots": _FORBIDDEN_ROOTS,
            "repeatsPerArm": 1,
            "maximumParallelJobs": 4,
            "runnerTimeoutSeconds": 60,
            "maximumInputLineBytes": 64 * 1024 * 1024,
            "maximumOutputLineBytes": 64 * 1024 * 1024,
            "maximumRunnerStderrBytes": 1024 * 1024,
        },
        "sourceArtifacts": sorted(
            [
                _artifact(record_set_path, _RECORD_SET_ID),
                _artifact(inventory_path, _INVENTORY_ID),
            ],
            key=lambda value: value["artifactId"],
        ),
        "repositoryFiles": [
            _repository_file(repository, relative)
            for relative in sorted(repository_files)
        ],
        "runtime": _runtime(fleetpy_root, python, dotnet),
        "treeSeals": {
            "adapterPackageSha256": _tree_sha256(
                repository / "simulators/fleetpy-ridebound/ridebound_fleetpy",
                b"RideBound.Wp13E1AdapterPackage.v1",
                {"__pycache__"},
            ),
            "panelADerivativeSha256": _tree_sha256(
                repository / _FIXTURE_ROOTS["A"],
                b"RideBound.Wp13E1PanelA.v1",
            ),
            "panelBDerivativeSha256": _tree_sha256(
                repository / _FIXTURE_ROOTS["B"],
                b"RideBound.Wp13E1PanelB.v1",
            ),
            "runnerArtifactSha256": _tree_sha256(
                runner_root,
                b"RideBound.Wp13E1RunnerArtifact.v1",
            ),
            "runnerDllSha256": _sha256(runner_root / "RideBound.Runner.dll"),
        },
    }
    schema = _load(
        repository
        / "benchmarks/schemas/wp13/v1/exploratory-replay-freeze.schema.json"
    )
    jsonschema.Draft202012Validator(
        schema,
        format_checker=jsonschema.FormatChecker(),
    ).validate(receipt)
    return receipt


def verify_receipt(
    receipt_path,
    repository,
    runner_root,
    fleetpy_root,
    python,
    dotnet,
    record_set_path,
    inventory_path,
):
    actual = _load(receipt_path)
    expected = build_receipt(
        repository,
        runner_root,
        fleetpy_root,
        python,
        dotnet,
        record_set_path,
        inventory_path,
        actual.get("frozenAtUtc"),
    )
    if actual != expected or receipt_path.read_text(encoding="utf-8") != _encoded(actual):
        raise RuntimeError("E1 freeze receipt differs or is not canonical")
    return {
        "freezeReceiptSha256": _sha256(receipt_path),
        "plannedArmRunCount": actual["design"]["plannedArmRunCount"],
        "repositoryFileCount": len(actual["repositoryFiles"]),
        "sourceArtifactCount": len(actual["sourceArtifacts"]),
        "status": "pass",
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--repository", required=True, type=pathlib.Path)
    parser.add_argument("--record-set", required=True, type=pathlib.Path)
    parser.add_argument("--inventory", required=True, type=pathlib.Path)
    parser.add_argument("--runner-root", type=pathlib.Path)
    parser.add_argument("--fleetpy-root", type=pathlib.Path)
    parser.add_argument("--python", type=pathlib.Path)
    parser.add_argument("--dotnet", type=pathlib.Path)
    parser.add_argument("--receipt", type=pathlib.Path)
    parser.add_argument("--frozen-at-utc")
    parser.add_argument("--write-plans", action="store_true")
    parser.add_argument("--write-receipt", action="store_true")
    arguments = parser.parse_args()
    repository = arguments.repository.resolve()
    record_set = arguments.record_set.resolve()
    inventory = arguments.inventory.resolve()
    if arguments.write_plans:
        write_plans(repository, record_set)
        return 0
    required = (
        arguments.runner_root,
        arguments.fleetpy_root,
        arguments.python,
        arguments.dotnet,
        arguments.receipt,
    )
    if any(value is None for value in required):
        parser.error("runtime paths and --receipt are required after plan generation")
    if arguments.write_receipt:
        if not arguments.frozen_at_utc:
            parser.error("--frozen-at-utc is required when writing a receipt")
        if arguments.receipt.exists():
            raise RuntimeError("E1 freeze receipt already exists")
        receipt = build_receipt(
            repository,
            arguments.runner_root.resolve(),
            arguments.fleetpy_root.resolve(),
            arguments.python.resolve(),
            arguments.dotnet.resolve(),
            record_set,
            inventory,
            arguments.frozen_at_utc,
        )
        arguments.receipt.parent.mkdir(parents=True, exist_ok=True)
        arguments.receipt.write_text(
            _encoded(receipt),
            encoding="utf-8",
            newline="\n",
        )
        return 0
    result = verify_receipt(
        arguments.receipt.resolve(),
        repository,
        arguments.runner_root.resolve(),
        arguments.fleetpy_root.resolve(),
        arguments.python.resolve(),
        arguments.dotnet.resolve(),
        record_set,
        inventory,
    )
    print(json.dumps(result, sort_keys=True, separators=(",", ":")))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
