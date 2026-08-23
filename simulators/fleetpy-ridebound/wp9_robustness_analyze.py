"""Deterministic descriptive analysis for the frozen WP9 robustness subset.

This program deliberately exposes no confirmatory gate.  It verifies every
bundle independently, aggregates the six preregistered variants, and reports
only exact integer contrasts.  Robustness evidence therefore cannot rescue or
replace the primary B1-versus-C1 result.
"""

from __future__ import annotations

import argparse
import importlib.util
import json
import pathlib


_VARIANTS = (
    "primaryBaseline",
    "primaryTreatment",
    "unboundedTreatment",
    "hybridLoose",
    "seed19Baseline",
    "seed19Treatment",
)
_BUNDLE_FIELDS = {variant: f"{variant}Bundle" for variant in _VARIANTS}
_CONTRASTS = (
    ("primaryTreatmentMinusBaseline", "primaryBaseline", "primaryTreatment"),
    ("unboundedTreatmentMinusBaseline", "primaryBaseline", "unboundedTreatment"),
    ("tightMinusUnboundedTreatment", "unboundedTreatment", "primaryTreatment"),
    ("hybridLooseMinusBaseline", "primaryBaseline", "hybridLoose"),
    ("seed19TreatmentMinusBaseline", "seed19Baseline", "seed19Treatment"),
    ("seed19MinusSeed7Baseline", "primaryBaseline", "seed19Baseline"),
    ("seed19MinusSeed7Treatment", "primaryTreatment", "seed19Treatment"),
)
_ADDITIVE_FIELDS = (
    "arrived",
    "completed",
    "decisionDropEtaTotalMs",
    "decisionPickupEtaTotalMs",
    "disruptiveDecisionCount",
    "exogenousDropEtaTotalMs",
    "exogenousPickupEtaTotalMs",
    "materialEtaRevisionCount",
    "prePickupInsertedStopCount",
    "totalDecisionInducedBurdenMs",
)
_EXPECTED_VARIANTS = {
    "primaryBaseline": ("primary", "b1", "tight", 7),
    "primaryTreatment": ("primary", "c1", "tight", 7),
    "unboundedTreatment": ("robustness", "c1", "unbounded", 7),
    "hybridLoose": ("robustness", "c2", "loose", 7),
    "seed19Baseline": ("robustness", "b1", "tight", 19),
    "seed19Treatment": ("robustness", "c1", "tight", 19),
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


def _canonical(value):
    return json.dumps(
        value,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    )


def _load_primary_analyzer(adapter_root):
    path = adapter_root / "wp9_fixed_panel_analyze.py"
    specification = importlib.util.spec_from_file_location(
        "ridebound_wp9_primary_analyzer_for_robustness",
        path,
    )
    if specification is None or specification.loader is None:
        raise RuntimeError("cannot load the primary WP9 analyzer")
    module = importlib.util.module_from_spec(specification)
    specification.loader.exec_module(module)
    return module


def _contrast(before, after):
    return {
        "deltaCompleted": after["completed"] - before["completed"],
        "deltaDisruptiveDecisionCount": (
            after["disruptiveDecisionCount"]
            - before["disruptiveDecisionCount"]
        ),
        "deltaTotalDecisionInducedBurdenMs": (
            after["totalDecisionInducedBurdenMs"]
            - before["totalDecisionInducedBurdenMs"]
        ),
    }


def _robustness_result(analysis_id, rows):
    if not rows:
        raise RuntimeError("the robustness subset must not be empty")

    aggregate = {
        variant: {field: 0 for field in _ADDITIVE_FIELDS}
        for variant in _VARIANTS
    }
    output_rows = []
    for cell_id, observations in rows:
        if set(observations) != set(_VARIANTS):
            raise RuntimeError(f"cell {cell_id} variants differ")
        arrivals = {value["arrived"] for value in observations.values()}
        if len(arrivals) != 1:
            raise RuntimeError(f"cell {cell_id} variants have different arrival counts")

        cell_contrasts = {
            name: _contrast(observations[before], observations[after])
            for name, before, after in _CONTRASTS
        }
        output_rows.append(
            {
                "cellId": cell_id,
                "contrasts": cell_contrasts,
                "observations": observations,
            }
        )
        for variant, observation in observations.items():
            for field in _ADDITIVE_FIELDS:
                aggregate[variant][field] += observation[field]

    aggregate_contrasts = {
        name: _contrast(aggregate[before], aggregate[after])
        for name, before, after in _CONTRASTS
    }
    return {
        "aggregateContrasts": aggregate_contrasts,
        "aggregateObservations": aggregate,
        "analysisId": analysis_id,
        "cells": output_rows,
        "confirmatoryGate": None,
        "interpretation": "descriptiveOnlyCannotRescuePrimary",
        "schemaVersion": "1.0.0",
    }


def _read_manifest(path):
    manifest = json.loads(path.read_text(encoding="utf-8"))
    if set(manifest) != {"schemaVersion", "analysisId", "cells"}:
        raise RuntimeError("robustness manifest fields differ")
    if manifest["schemaVersion"] != "1.0.0":
        raise RuntimeError("robustness manifest version differs")
    cell_fields = {"cellId", *_BUNDLE_FIELDS.values()}
    cells = manifest["cells"]
    if (
        not isinstance(cells, list)
        or not cells
        or any(set(cell) != cell_fields for cell in cells)
        or any(
            not isinstance(value, str) or not value
            for cell in cells
            for value in cell.values()
        )
        or len({cell["cellId"] for cell in cells}) != len(cells)
    ):
        raise RuntimeError("robustness manifest cells are invalid or duplicate")
    return manifest


def _validate_variant_job(job, cell_id, variant, bundle_name):
    phase, arm, level, seed = _EXPECTED_VARIANTS[variant]
    prefix = "p" if phase == "primary" else "r"
    expected = {
        "armId": arm,
        "cellId": cell_id,
        "commitmentConfig": _COMMITMENT_BY_LEVEL[level],
        "driver": f"benchmarks/scenarios/wp9-confirmatory/{cell_id}.driver.json",
        "jobId": f"{prefix}-{cell_id}-{arm}-{level}-s{seed}",
        "masterSeed": seed,
        "phase": phase,
        "wp4Config": _WP4_BY_ARM[arm],
    }
    if bundle_name != expected["jobId"] or job != expected:
        raise RuntimeError(
            f"robustness job binding differs for cell {cell_id}, variant {variant}"
        )


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--manifest", required=True, type=pathlib.Path)
    parser.add_argument("--execution-plan", required=True, type=pathlib.Path)
    parser.add_argument("--repository", required=True, type=pathlib.Path)
    parser.add_argument("--bundle-root", required=True, type=pathlib.Path)
    parser.add_argument("--require-audited-solver-evidence", action="store_true")
    arguments = parser.parse_args()

    adapter_root = pathlib.Path(__file__).parent.resolve()
    primary = _load_primary_analyzer(adapter_root)
    matrix = primary._load_matrix_program(adapter_root)
    plan = matrix._load_plan(arguments.execution_plan.resolve())
    matrix._validate_frozen_design(plan)
    jobs = {job["jobId"]: job for job in plan["jobs"]}
    repository = arguments.repository.resolve()
    repository_inventory_sha256 = matrix._repository_inventory_sha256(repository)
    verifier = primary._load_verifier(adapter_root)
    manifest = _read_manifest(arguments.manifest.resolve())

    # Same fail-open as the primary analyzer: a manifest listing a subset of the
    # frozen robustness cells previously analysed and reported without saying so.
    planned_cells = {
        job["cellId"] for job in plan["jobs"] if job["phase"] == "robustness"
    }
    manifest_cells = {cell["cellId"] for cell in manifest["cells"]}
    if manifest_cells != planned_cells:
        raise RuntimeError(
            "robustness manifest is not the exact frozen robustness cell set: "
            f"{len(manifest_cells)} of {len(planned_cells)} cells"
        )

    rows = []
    for cell in sorted(manifest["cells"], key=lambda value: value["cellId"]):
        scenario = repository / (
            "benchmarks/fixtures/wp6/public/fleetpy-manhattan-v1/"
            "wp9-confirmatory-fixed-panel-v2"
        ) / cell["cellId"] / "scenario-content.json"
        scenario_sha256 = primary._sha256(scenario)
        observations = {}
        for variant, field in _BUNDLE_FIELDS.items():
            bundle_name = cell[field]
            job = jobs.get(bundle_name)
            if job is None:
                raise RuntimeError(
                    f"cell {cell['cellId']} job is absent from execution plan"
                )
            _validate_variant_job(job, cell["cellId"], variant, bundle_name)
            observations[variant] = primary._read_observation(
                (arguments.bundle_root / bundle_name).resolve(),
                verifier,
                arguments.require_audited_solver_evidence,
                bundle_name,
                scenario_sha256,
                repository_inventory_sha256,
            )
        rows.append((cell["cellId"], observations))

    print(_canonical(_robustness_result(manifest["analysisId"], rows)))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
