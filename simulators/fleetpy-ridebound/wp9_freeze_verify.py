"""Verify the current pre-outcome WP9 freeze receipt from raw files."""

from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import pathlib


_REPOSITORY_HASH_FIELDS = {
    "supersedesFreezeReceiptV2Sha256": (
        "benchmarks/scenarios/wp9-confirmatory/freeze-receipt-v2.json"
    ),
    "preregistrationSha256": "docs/benchmarking/wp8-011-preregistration-v1.md",
    "preOutcomeNodeCapAmendmentSha256": (
        "docs/benchmarking/wp8-011a-pre-outcome-node-cap-amendment.md"
    ),
    "preOutcomeAnalysisIntegrityAmendmentSha256": (
        "docs/benchmarking/wp8-011b-pre-outcome-analysis-integrity-amendment.md"
    ),
    "preOutcomeRunnerRepinAmendmentSha256": (
        "docs/benchmarking/wp8-011c-pre-outcome-runner-artifact-repin.md"
    ),
    "gridSha256": "benchmarks/scenarios/wp9-confirmatory/grid-v2.json",
    "executionPlanSha256": (
        "benchmarks/scenarios/wp9-confirmatory/execution-plan-v1.json"
    ),
    "analysisManifestSha256": (
        "benchmarks/scenarios/wp9-confirmatory/analysis-manifest-v1.json"
    ),
    "robustnessManifestSha256": (
        "benchmarks/scenarios/wp9-confirmatory/robustness-manifest-v1.json"
    ),
    "analysisProgramSha256": "simulators/fleetpy-ridebound/wp9_fixed_panel_analyze.py",
    "robustnessAnalysisProgramSha256": (
        "simulators/fleetpy-ridebound/wp9_robustness_analyze.py"
    ),
    "matrixProgramSha256": "simulators/fleetpy-ridebound/wp9_run_matrix.py",
    "freezeVerifierProgramSha256": (
        "simulators/fleetpy-ridebound/wp9_freeze_verify.py"
    ),
    "preflightSha256": (
        "simulators/fleetpy-ridebound/actual_fleetpy_medium_preflight.py"
    ),
    "verifierSha256": (
        "simulators/fleetpy-ridebound/actual_fleetpy_medium_verify.py"
    ),
    "baselineConfigurationSha256": (
        "benchmarks/configurations/wp9-fleetpy-rolling-cost-audited-v1.json"
    ),
    "treatmentConfigurationSha256": (
        "benchmarks/configurations/wp9-fleetpy-ridebound-hard-vector-audited-v1.json"
    ),
    "hybridConfigurationSha256": (
        "benchmarks/configurations/wp9-fleetpy-soft-hard-hybrid-audited-v1.json"
    ),
    "tightCommitmentSha256": (
        "benchmarks/configurations/wp8-drop-eta-budget-tight-v1.json"
    ),
    "looseCommitmentSha256": (
        "benchmarks/configurations/wp8-drop-eta-budget-loose-v1.json"
    ),
    "unboundedCommitmentSha256": (
        "benchmarks/configurations/wp6-public-mechanical-commitment-v1.json"
    ),
    "experimentalUnitModelsSha256": (
        "src/RideBound.Benchmarking.Contracts/ExperimentalUnitModels.cs"
    ),
    "burdenCalculatorSha256": (
        "src/RideBound.Benchmarking/Metrics/DecisionInducedBurdenCalculator.cs"
    ),
    "burdenOracleSha256": (
        "tools/RideBound.Wp6MetricOracle/DecisionInducedBurdenOracle.cs"
    ),
}
_LITERAL_FIELDS = {
    "schemaVersion",
    "freezeId",
    "frozenAtUtc",
    "legacyDerivativeTreeSha256",
    "derivativeTreeSealSha256",
    "scenarioPlanSealSha256",
    "runnerSha256",
    "runnerTreeSealSha256",
    "plannedPrimaryRunCount",
    "plannedRobustnessRunCount",
    "experimentalUnitCount",
    "requestCountPerRun",
    "solverSeedsAreReplicates",
    "repositoryInventoryAlgorithm",
}


def _sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _tree_sha256(root, domain, excluded_names=()):
    excluded = set(excluded_names)
    files = sorted(
        (
            path
            for path in root.rglob("*")
            if path.is_file() and path.name not in excluded
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
        length = path.stat().st_size
        digest.update(length.to_bytes(8, "big"))
        with path.open("rb") as source:
            for block in iter(lambda: source.read(1024 * 1024), b""):
                digest.update(block)
    return digest.hexdigest()


def _load_matrix(repository):
    path = repository / "simulators/fleetpy-ridebound/wp9_run_matrix.py"
    specification = importlib.util.spec_from_file_location(
        "ridebound_wp9_matrix_for_freeze_verifier",
        path,
    )
    if specification is None or specification.loader is None:
        raise RuntimeError("cannot load the frozen matrix validator")
    module = importlib.util.module_from_spec(specification)
    specification.loader.exec_module(module)
    return module


def verify_receipt(receipt_path, repository, runner_root):
    receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
    required = set(_REPOSITORY_HASH_FIELDS) | _LITERAL_FIELDS
    if set(receipt) != required or receipt["schemaVersion"] != "3.0.0":
        raise RuntimeError("freeze receipt fields/version differ")
    for field, relative in _REPOSITORY_HASH_FIELDS.items():
        actual = _sha256(repository / relative)
        if receipt[field] != actual:
            raise RuntimeError(f"freeze hash differs: {field}")

    runner_sha256 = _sha256(runner_root / "RideBound.Runner.dll")
    if receipt["runnerSha256"] != runner_sha256:
        raise RuntimeError("freeze hash differs: runnerSha256")
    runner_tree_seal = _tree_sha256(
        runner_root,
        b"RideBound.Wp9RunnerArtifact.v1",
    )
    if receipt["runnerTreeSealSha256"] != runner_tree_seal:
        raise RuntimeError("freeze hash differs: runnerTreeSealSha256")

    derivative_root = repository / (
        "benchmarks/fixtures/wp6/public/fleetpy-manhattan-v1/"
        "wp9-confirmatory-fixed-panel-v2"
    )
    derivative_seal = _tree_sha256(
        derivative_root,
        b"RideBound.Wp9DerivativeTree.v2",
    )
    if receipt["derivativeTreeSealSha256"] != derivative_seal:
        raise RuntimeError("freeze hash differs: derivativeTreeSealSha256")
    scenario_seal = _tree_sha256(
        repository / "benchmarks/scenarios/wp9-confirmatory",
        b"RideBound.Wp9ScenarioPlanTree.v2",
        {
            "freeze-receipt-v1.json",
            "freeze-receipt-v2.json",
            "freeze-receipt-v3.json",
        },
    )
    if receipt["scenarioPlanSealSha256"] != scenario_seal:
        raise RuntimeError("freeze hash differs: scenarioPlanSealSha256")

    matrix = _load_matrix(repository)
    plan = matrix._load_plan(
        repository / "benchmarks/scenarios/wp9-confirmatory/execution-plan-v1.json"
    )
    matrix._validate_frozen_design(plan)
    primary_count = sum(job["phase"] == "primary" for job in plan["jobs"])
    robustness_count = sum(job["phase"] == "robustness" for job in plan["jobs"])
    unit_count = len(
        {job["cellId"] for job in plan["jobs"] if job["phase"] == "primary"}
    )
    if (
        receipt["plannedPrimaryRunCount"] != primary_count
        or receipt["plannedRobustnessRunCount"] != robustness_count
        or receipt["experimentalUnitCount"] != unit_count
        or receipt["requestCountPerRun"] != 108
        or receipt["solverSeedsAreReplicates"] is not False
        or receipt["repositoryInventoryAlgorithm"]
        != "RideBound.Wp9RepositoryInventory.v1"
    ):
        raise RuntimeError("freeze design counts/semantics differ")
    return {
        "checkedFileHashCount": len(_REPOSITORY_HASH_FIELDS) + 1,
        "derivativeTreeSealSha256": derivative_seal,
        "freezeReceiptSha256": _sha256(receipt_path),
        "runnerTreeSealSha256": runner_tree_seal,
        "scenarioPlanSealSha256": scenario_seal,
        "status": "pass",
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--receipt", required=True, type=pathlib.Path)
    parser.add_argument("--repository", required=True, type=pathlib.Path)
    parser.add_argument("--runner-root", required=True, type=pathlib.Path)
    arguments = parser.parse_args()
    result = verify_receipt(
        arguments.receipt.resolve(),
        arguments.repository.resolve(),
        arguments.runner_root.resolve(),
    )
    print(json.dumps(result, sort_keys=True, separators=(",", ":")))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
