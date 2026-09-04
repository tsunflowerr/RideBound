#!/usr/bin/env python3
"""Prove the WP14 development panel shares nothing with the frozen H6 panels.

ADR-065 forbids H6 Panel A/B from taking part in any WP14 tuning or selection.
That is only credible if it is checkable, so this audit compares the declared
grids and the generated derivatives on every axis a realization could leak
through: source demand member, travel-factor member, local date, intra-day
window, scenario id, cell id, and the content hash of the generated scenario.

Any overlap is a hard failure. The audit is read-only and writes its receipt
with exclusive create.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import pathlib
import sys

sys.dont_write_bytecode = True

SCHEMA_ID = "https://ridebound.local/schemas/wp14/v1/development-panel-audit.schema.json"
AUDIT_VERSION = "1.0.0"
COMPARED_AXES = (
    "demandMemberPath",
    "travelFactorMemberPath",
    "sourceLocalDate",
    "sourceWindow",
    "scenarioId",
    "cellId",
    "scenarioHash",
)


class AuditError(RuntimeError):
    """A fail-closed audit condition."""


def read_json(path):
    if not path.is_file():
        raise AuditError(f"required file not found: {path}")
    return json.loads(path.read_text(encoding="utf-8"))


def sha256_file(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def cell_axes(cell, scenario_hash):
    start = cell["sourceWindowStartSeconds"]
    end = cell["sourceWindowEndSeconds"]
    return {
        "demandMemberPath": cell["demandMemberPath"],
        "travelFactorMemberPath": cell["travelFactorMemberPath"],
        "sourceLocalDate": cell["sourceLocalDate"],
        "sourceWindow": f"{cell['sourceLocalDate']}T{start}-{end}",
        "scenarioId": cell["scenarioId"],
        "cellId": f"{cell['gridId']}/{cell['cellId']}",
        "scenarioHash": scenario_hash,
    }


def load_grid(repository, manifest_path, require_fixtures):
    manifest = read_json(manifest_path)
    grid_id = manifest["gridId"]
    fixture_root = (
        repository
        / "benchmarks"
        / "fixtures"
        / "wp6"
        / "public"
        / "fleetpy-manhattan-v1"
        / grid_id
    )
    rows = []
    for cell in manifest["cells"]:
        cell = dict(cell, gridId=grid_id)
        scenario = fixture_root / cell["cellId"] / "scenario-content.json"
        if scenario.is_file():
            report = read_json(
                fixture_root / cell["cellId"] / "normalization-report.json"
            )
            scenario_hash = report["scenarioHash"]
        elif require_fixtures:
            raise AuditError(f"generated fixture missing: {scenario}")
        else:
            # A frozen grid whose derivative is not checked out still contributes
            # its declared axes; only the generated hash is unavailable.
            scenario_hash = None
        rows.append(cell_axes(cell, scenario_hash))
    return grid_id, rows


def audit(repository, development_manifest, frozen_manifests):
    development_id, development = load_grid(
        repository, development_manifest, require_fixtures=True
    )
    frozen = []
    frozen_ids = []
    for manifest in frozen_manifests:
        grid_id, rows = load_grid(repository, manifest, require_fixtures=False)
        frozen_ids.append(grid_id)
        frozen.extend(rows)

    if not frozen:
        raise AuditError("at least one frozen grid is required for comparison")

    overlaps = {}
    for axis in COMPARED_AXES:
        development_values = {
            row[axis] for row in development if row[axis] is not None
        }
        frozen_values = {row[axis] for row in frozen if row[axis] is not None}
        shared = sorted(development_values & frozen_values)
        overlaps[axis] = shared

    leaking = {axis: shared for axis, shared in overlaps.items() if shared}
    if leaking:
        raise AuditError(
            "development panel overlaps the frozen panels on "
            + ", ".join(f"{axis}={shared}" for axis, shared in leaking.items())
        )

    if len({row["cellId"] for row in development}) != len(development):
        raise AuditError("development cell ids are not unique")

    manifest_bytes = development_manifest.read_bytes()
    return {
        "schemaId": SCHEMA_ID,
        "auditVersion": AUDIT_VERSION,
        "claimBoundary": [
            "developmentPanelOnlyNotConfirmatory",
            "frozenPanelsNeverUsedForTuningOrSelection",
        ],
        "developmentGridId": development_id,
        "developmentManifestSha256": hashlib.sha256(manifest_bytes).hexdigest(),
        "developmentManifestLengthBytes": len(manifest_bytes),
        "developmentCellCount": len(development),
        "frozenGridIds": sorted(frozen_ids),
        "frozenCellCount": len(frozen),
        "comparedAxes": list(COMPARED_AXES),
        "overlapCountByAxis": {axis: len(overlaps[axis]) for axis in COMPARED_AXES},
        "developmentDates": sorted({row["sourceLocalDate"] for row in development}),
        "frozenDates": sorted({row["sourceLocalDate"] for row in frozen}),
        "developmentCells": [
            {
                "cellId": row["cellId"],
                "scenarioId": row["scenarioId"],
                "scenarioHash": row["scenarioHash"],
                "sourceWindow": row["sourceWindow"],
            }
            for row in sorted(development, key=lambda value: value["cellId"])
        ],
    }


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


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repository", required=True, type=pathlib.Path)
    parser.add_argument("--development", required=True, type=pathlib.Path)
    parser.add_argument("--frozen", action="append", required=True, type=pathlib.Path)
    parser.add_argument("--output", type=pathlib.Path)
    arguments = parser.parse_args(argv)
    try:
        document = audit(
            arguments.repository.resolve(),
            arguments.development,
            arguments.frozen,
        )
        encoded = canonical(document) + b"\n"
        if arguments.output is None:
            sys.stdout.buffer.write(encoded)
        else:
            write_exclusive(arguments.output, encoded)
            print(
                f"{arguments.output} {len(encoded)} "
                f"{hashlib.sha256(encoded).hexdigest()}"
            )
    except (OSError, AuditError, ValueError, TypeError, KeyError) as error:
        print(f"wp14_development_panel_audit: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
