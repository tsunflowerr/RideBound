"""Build and verify the WP9 confirmatory H6 freeze receipt v6.

Same scientific design as v5 - the same grids, execution plans, analysis
manifests, 40 primary + 20 robustness + 40 panel-B runs, 20 experimental
units, 108 requests per run and configurations. Only the sealed source and
the sealing rule differ.

v6 exists because two of v5's checks no longer hold. `verifierSha256` drifted
before RB-WP14R-009 and was already reconciled as a backward-compatible
verifier in docs/benchmarking/evidence/wp9-h6-reproducibility-v1.json.
`adapterPackageTreeSealSha256` moved when mapping.py stopped reporting a
vehicle that had entered an edge as standing on the node behind it - a node
it could only regain the long way round, which made every ETA from that
vehicle optimistic. ADR-047's own note in v5 records that this rounding is
outcome-bearing, so H6 is re-run rather than patched in place.

v6 also fixes the sealing rule. v5 filtered excluded names with
`path.name not in excluded`, and __pycache__ is a directory, so every tree
seal hashed compiled bytecode. A seal over bytecode cannot survive a
recompilation: the H6-era adapter seal is not reproducible from the working
tree nor from any of the four commits that ever touched the package. v6
tests the relative path's parts, so a seal covers source only.

`wp9_freeze_verify.py` and freeze-receipt-v5.json are left exactly as they
are. They remain the record of what the original H6 run executed under,
including that provenance gap.
"""

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
    "supersedesFreezeReceiptV3Sha256": (
        "benchmarks/scenarios/wp9-confirmatory/freeze-receipt-v3.json"
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
    "preOutcomeCapacityPanelAmendmentSha256": (
        "docs/benchmarking/wp8-011d-pre-outcome-capacity-stratum-amendment.md"
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
    "panelBGridSha256": "benchmarks/scenarios/wp9-confirmatory/grid-v3-veh4.json",
    "panelBExecutionPlanSha256": (
        "benchmarks/scenarios/wp9-confirmatory/execution-plan-panel-b-v1.json"
    ),
    "panelBAnalysisManifestSha256": (
        "benchmarks/scenarios/wp9-confirmatory/analysis-manifest-panel-b-v1.json"
    ),
    "analysisProgramSha256": "simulators/fleetpy-ridebound/wp9_fixed_panel_analyze.py",
    "robustnessAnalysisProgramSha256": (
        "simulators/fleetpy-ridebound/wp9_robustness_analyze.py"
    ),
    "matrixProgramSha256": "simulators/fleetpy-ridebound/wp9_run_matrix.py",
    "freezeVerifierProgramSha256": (
        "simulators/fleetpy-ridebound/wp9_freeze_v6.py"
    ),
    "supersedesFreezeReceiptV5Sha256": (
        "benchmarks/scenarios/wp9-confirmatory/freeze-receipt-v5.json"
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
    "panelBDerivativeTreeSealSha256",
    "adapterPackageTreeSealSha256",
    "plannedPanelBPrimaryRunCount",
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
            # Test the whole relative path, not just the leaf name.
            # __pycache__ is a directory, so a leaf-name test excluded
            # nothing and the seal hashed compiled bytecode, which cannot
            # survive a recompilation. This is why v5's adapter seal is not
            # reproducible from any state of the package.
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


_DERIVED_SEALS = (
    (
        "derivativeTreeSealSha256",
        "benchmarks/fixtures/wp6/public/fleetpy-manhattan-v1/"
        "wp9-confirmatory-fixed-panel-v2",
        b"RideBound.Wp9DerivativeTree.v2",
        frozenset(),
    ),
    (
        "panelBDerivativeTreeSealSha256",
        "benchmarks/fixtures/wp6/public/fleetpy-manhattan-v1/"
        "wp9-confirmatory-fixed-panel-v3-veh4",
        b"RideBound.Wp9DerivativeTree.v3Veh4",
        frozenset(),
    ),
    (
        # ADR-047. The adapter package decides what RideBound sees and what
        # it may propose: a one-line rounding change in mapping.py moved
        # every ETA. It was outcome-bearing and completely unpinned, so the
        # freeze would have accepted a silently different experiment.
        "adapterPackageTreeSealSha256",
        "simulators/fleetpy-ridebound/ridebound_fleetpy",
        b"RideBound.Wp9AdapterPackage.v1",
        frozenset({"__pycache__"}),
    ),
    (
        "scenarioPlanSealSha256",
        "benchmarks/scenarios/wp9-confirmatory",
        b"RideBound.Wp9ScenarioPlanTree.v2",
        frozenset(
            {
                "freeze-receipt-v1.json",
                "freeze-receipt-v2.json",
                "freeze-receipt-v3.json",
                "freeze-receipt-v4.json",
                "freeze-receipt-v5.json",
                "freeze-receipt-v6.json",
                # The declaration that records this seal cannot live
                # inside it, for the same reason the receipts cannot.
                "source-divergence-v5.json",
            }
        ),
    ),
)


def _derived(repository, runner_root):
    """Every value a receipt must agree with, computed once.

    The builder emits this and the verifier compares against it, so the two
    cannot disagree. v5 kept the derivation inline in the verifier and had no
    builder at all, which is why its receipt could not be rebuilt.
    """
    values = {}
    for field, relative in _REPOSITORY_HASH_FIELDS.items():
        values[field] = _sha256(repository / relative)
    values["runnerSha256"] = _sha256(runner_root / "RideBound.Runner.dll")
    values["runnerTreeSealSha256"] = _tree_sha256(
        runner_root,
        b"RideBound.Wp9RunnerArtifact.v1",
    )
    for field, relative, domain, excluded in _DERIVED_SEALS:
        values[field] = _tree_sha256(repository / relative, domain, excluded)

    matrix = _load_matrix(repository)
    plan = matrix._load_plan(
        repository / "benchmarks/scenarios/wp9-confirmatory/execution-plan-v1.json"
    )
    matrix._validate_frozen_design(plan, "a")
    panel_b_plan = matrix._load_plan(
        repository
        / "benchmarks/scenarios/wp9-confirmatory/execution-plan-panel-b-v1.json"
    )
    matrix._validate_frozen_design(panel_b_plan, "b")
    values["plannedPrimaryRunCount"] = sum(
        job["phase"] == "primary" for job in plan["jobs"]
    )
    values["plannedRobustnessRunCount"] = sum(
        job["phase"] == "robustness" for job in plan["jobs"]
    )
    values["experimentalUnitCount"] = len(
        {job["cellId"] for job in plan["jobs"] if job["phase"] == "primary"}
    )
    values["plannedPanelBPrimaryRunCount"] = len(panel_b_plan["jobs"])
    values["requestCountPerRun"] = 108
    values["solverSeedsAreReplicates"] = False
    values["repositoryInventoryAlgorithm"] = (
        "RideBound.Wp9RepositoryInventory.v1"
    )
    values["schemaVersion"] = "6.0.0"
    return values


# Recorded by v5 and never derived from anything, so it is carried forward
# rather than recomputed. It names a derivative tree that no longer exists.
LEGACY_DERIVATIVE_TREE_SHA256 = (
    "623ebfae905355007aa9bedca4687646973d8a9c9da8b25d67e5409b02abc943"
)
FREEZE_ID = "wp9-confirmatory-h6-rbwp14r009-entered-edge-position"


def build(repository, runner_root, frozen_at_utc):
    receipt = dict(_derived(repository, runner_root))
    receipt["freezeId"] = FREEZE_ID
    receipt["frozenAtUtc"] = frozen_at_utc
    receipt["legacyDerivativeTreeSha256"] = LEGACY_DERIVATIVE_TREE_SHA256
    required = set(_REPOSITORY_HASH_FIELDS) | _LITERAL_FIELDS
    if set(receipt) != required:
        missing = sorted(required - set(receipt))
        extra = sorted(set(receipt) - required)
        raise RuntimeError(
            f"built receipt fields differ: missing={missing} extra={extra}"
        )
    return receipt


def canonical(document):
    """The v5 receipt's own form: two-space indent, sorted keys, CRLF."""
    rendered = json.dumps(
        document, ensure_ascii=False, indent=2, sort_keys=True
    )
    return rendered.replace("\n", "\r\n").encode("utf-8")


def verify_receipt(receipt_path, repository, runner_root):
    receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
    required = set(_REPOSITORY_HASH_FIELDS) | _LITERAL_FIELDS
    if set(receipt) != required or receipt["schemaVersion"] != "6.0.0":
        raise RuntimeError("freeze receipt fields/version differ")
    values = _derived(repository, runner_root)
    for field, expected in sorted(values.items()):
        if receipt[field] != expected:
            raise RuntimeError(f"freeze hash differs: {field}")
    return {
        "checkedFileHashCount": len(_REPOSITORY_HASH_FIELDS) + 1,
        "derivativeTreeSealSha256": values["derivativeTreeSealSha256"],
        "panelBDerivativeTreeSealSha256": values[
            "panelBDerivativeTreeSealSha256"
        ],
        "adapterPackageTreeSealSha256": values["adapterPackageTreeSealSha256"],
        "freezeReceiptSha256": _sha256(receipt_path),
        "runnerTreeSealSha256": values["runnerTreeSealSha256"],
        "scenarioPlanSealSha256": values["scenarioPlanSealSha256"],
        "status": "pass",
    }

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--receipt", required=True, type=pathlib.Path)
    parser.add_argument("--repository", required=True, type=pathlib.Path)
    parser.add_argument("--runner-root", required=True, type=pathlib.Path)
    parser.add_argument("--frozen-at-utc")
    parser.add_argument("--write", action="store_true")
    arguments = parser.parse_args()
    repository = arguments.repository.resolve()
    runner_root = arguments.runner_root.resolve()
    receipt_path = arguments.receipt.resolve()
    if arguments.write:
        if not arguments.frozen_at_utc:
            raise RuntimeError("--write requires --frozen-at-utc")
        if receipt_path.exists():
            raise RuntimeError("freeze receipt already exists")
        document = build(repository, runner_root, arguments.frozen_at_utc)
        receipt_path.write_bytes(canonical(document) + b"\r\n")
    elif arguments.frozen_at_utc:
        raise RuntimeError("--frozen-at-utc is only valid with --write")
    result = verify_receipt(receipt_path, repository, runner_root)
    print(json.dumps(result, sort_keys=True, separators=(",", ":")))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
