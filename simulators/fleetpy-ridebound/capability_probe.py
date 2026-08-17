#!/usr/bin/env python3
"""Fail-closed capability probe for the externally pinned FleetPy source tree."""

from __future__ import annotations

import argparse
import hashlib
import importlib.metadata
import inspect
import json
import math
import pathlib
import subprocess
import sys
from dataclasses import dataclass
from typing import Any, NoReturn


MATRIX_PATH = pathlib.Path(__file__).with_name("capability-matrix.json")


class ProbeFailure(RuntimeError):
    """A stable-code capability failure intended for machine consumption."""

    def __init__(self, code: str, detail: str) -> None:
        super().__init__(detail)
        self.code = code
        self.detail = detail


def _fail(code: str, detail: str) -> NoReturn:
    raise ProbeFailure(code, detail)


def _sha256(path: pathlib.Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _git(root: pathlib.Path, *arguments: str) -> str:
    completed = subprocess.run(
        ["git", "-C", str(root), *arguments],
        check=False,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        encoding="utf-8",
        errors="strict",
    )
    if completed.returncode != 0:
        _fail("RBWP7_SOURCE_GIT_ERROR", completed.stderr.strip())
    return completed.stdout.strip()


def _verify_source(root: pathlib.Path, matrix: dict[str, Any]) -> dict[str, Any]:
    expected = matrix["fleetPy"]
    if not root.is_dir():
        _fail("RBWP7_SOURCE_ROOT_MISSING", str(root))

    head = _git(root, "rev-parse", "HEAD")
    tag_object = _git(root, "rev-parse", f"refs/tags/{expected['tag']}")
    tag_type = _git(root, "cat-file", "-t", f"refs/tags/{expected['tag']}")
    tag_commit = _git(root, "rev-list", "-n", "1", f"refs/tags/{expected['tag']}")
    if head != expected["commit"] or tag_commit != expected["commit"]:
        _fail(
            "RBWP7_SOURCE_COMMIT_DRIFT",
            f"head={head}; tagCommit={tag_commit}; expected={expected['commit']}",
        )
    if tag_type != "tag" or tag_object != expected["annotatedTagObject"]:
        _fail(
            "RBWP7_SOURCE_TAG_DRIFT",
            f"type={tag_type}; object={tag_object}",
        )
    dirty = _git(root, "status", "--porcelain", "--untracked-files=all")
    if dirty:
        _fail("RBWP7_SOURCE_DIRTY", dirty.splitlines()[0])

    verified_hashes: dict[str, str] = {}
    for relative, expected_hash in expected["sourceHashes"].items():
        source = root / pathlib.PurePosixPath(relative)
        if not source.is_file():
            _fail("RBWP7_SOURCE_FILE_MISSING", relative)
        actual_hash = _sha256(source)
        if actual_hash != expected_hash:
            _fail(
                "RBWP7_SOURCE_HASH_DRIFT",
                f"{relative}: actual={actual_hash}; expected={expected_hash}",
            )
        verified_hashes[relative] = actual_hash

    return {
        "tag": expected["tag"],
        "annotatedTagObject": tag_object,
        "commit": head,
        "clean": True,
        "sourceHashes": verified_hashes,
    }


def _verify_environment(matrix: dict[str, Any]) -> dict[str, Any]:
    expected = matrix["python"]
    actual_python = [sys.version_info.major, sys.version_info.minor, sys.version_info.micro]
    if actual_python[:2] != [expected["major"], expected["minor"]]:
        _fail(
            "RBWP7_PYTHON_VERSION_DRIFT",
            f"actual={actual_python}; expected={expected['major']}.{expected['minor']}.x",
        )

    actual_packages: dict[str, str] = {}
    for distribution, expected_version in expected["packages"].items():
        try:
            actual_version = importlib.metadata.version(distribution)
        except importlib.metadata.PackageNotFoundError:
            _fail("RBWP7_PACKAGE_MISSING", distribution)
        if actual_version != expected_version:
            _fail(
                "RBWP7_PACKAGE_VERSION_DRIFT",
                f"{distribution}: actual={actual_version}; expected={expected_version}",
            )
        actual_packages[distribution] = actual_version
    return {"version": actual_python, "packages": actual_packages}


@dataclass
class _RoutingProbe:
    new_position: tuple[int, int, float]

    def move_along_route(self, *_: Any, **__: Any) -> tuple[Any, float, float, list[Any], list[Any]]:
        return self.new_position, 5.0, -1.0, [], []


def _verify_fleetpy_imports(root: pathlib.Path, matrix: dict[str, Any]) -> dict[str, Any]:
    sys.path.insert(0, str(root))
    try:
        from src.fleetctrl.FleetControlBase import FleetControlBase
        from src.fleetctrl.planning.VehiclePlan import PlanStop, VehiclePlan
        from src.routing.NetworkBase import return_position_from_str, return_position_str
        from src.simulation.Offers import Rejection, TravellerOffer
        from src.simulation.Vehicles import SimulationVehicle
    except Exception as exc:  # FleetPy has import-time optional dependency paths.
        _fail("RBWP7_FLEETPY_IMPORT_FAILED", f"{type(exc).__name__}: {exc}")

    callbacks = sorted(FleetControlBase.__abstractmethods__)
    if callbacks != matrix["abstractCallbacks"]:
        _fail(
            "RBWP7_CALLBACK_CONTRACT_DRIFT",
            f"actual={callbacks}; expected={matrix['abstractCallbacks']}",
        )

    node = return_position_from_str("11;-1;-1")
    edge = return_position_from_str("11;12;0.375")
    if node != (11, None, None) or edge != (11, 12, 0.375):
        _fail("RBWP7_EDGE_PROGRESS_UNAVAILABLE", f"node={node}; edge={edge}")
    if return_position_str(edge) != "11;12;0.375":
        _fail("RBWP7_EDGE_PROGRESS_ROUNDTRIP_FAILED", return_position_str(edge))
    if not isinstance(edge[0], int) or not isinstance(edge[1], int):
        _fail("RBWP7_EDGE_PROGRESS_TYPE_INVALID", repr(edge))
    if not isinstance(edge[2], float) or not math.isfinite(edge[2]) or not 0 <= edge[2] <= 1:
        _fail("RBWP7_EDGE_PROGRESS_RANGE_INVALID", repr(edge))

    vehicle = SimulationVehicle.__new__(SimulationVehicle)
    vehicle.routing_engine = _RoutingProbe((11, 12, 0.625))
    vehicle.cl_remaining_route = [12]
    vehicle.pos = edge
    vehicle.op_id = 7
    vehicle.vid = 9
    vehicle.replay_flag = False
    vehicle.cl_driven_distance = 0.0
    vehicle.soc = 1.0
    vehicle.compute_soc_consumption = lambda _distance: 0.0
    vehicle.cl_driven_route = []
    vehicle.cl_driven_route_times = []
    vehicle.cl_toll_costs = 0.0
    arrival = SimulationVehicle._move(vehicle, 100.0, 1.0, 100.0)
    if arrival != -1.0 or vehicle.pos != (11, 12, 0.625):
        _fail(
            "RBWP7_VEHICLE_POSITION_UPDATE_FAILED",
            f"arrival={arrival}; position={vehicle.pos}",
        )

    signature = inspect.signature(FleetControlBase.assign_vehicle_plan)
    force_default = signature.parameters["force_assign"].default
    assignment_source = inspect.getsource(FleetControlBase.assign_vehicle_plan)
    expected_call = "force_ignore_lock=force_assign"
    if force_default is not False or expected_call not in assignment_source:
        _fail(
            "RBWP7_FORCE_ASSIGNMENT_PATH",
            f"default={force_default}; delegated={expected_call in assignment_source}",
        )

    return {
        "callbacks": callbacks,
        "imports": [
            FleetControlBase.__name__,
            VehiclePlan.__name__,
            PlanStop.__name__,
            SimulationVehicle.__name__,
            TravellerOffer.__name__,
            Rejection.__name__,
        ],
        "nodePosition": list(node),
        "directedEdgePosition": list(vehicle.pos),
        "positionRoundTrip": return_position_str(edge),
        "vehicleMoveMutatesPosition": True,
        "forceAssignDefault": force_default,
        "delegatesForceFlag": True,
    }


def run_probe(root: pathlib.Path) -> dict[str, Any]:
    matrix = json.loads(MATRIX_PATH.read_text(encoding="utf-8"))
    source = _verify_source(root.resolve(), matrix)
    environment = _verify_environment(matrix)
    capabilities = _verify_fleetpy_imports(root.resolve(), matrix)
    return {
        "schemaVersion": "1.0.0",
        "status": "pass",
        "source": source,
        "environment": environment,
        "capabilities": capabilities,
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--fleetpy-root", required=True, type=pathlib.Path)
    arguments = parser.parse_args()
    try:
        report = run_probe(arguments.fleetpy_root)
    except ProbeFailure as failure:
        report = {
            "schemaVersion": "1.0.0",
            "status": "fail",
            "failureCode": failure.code,
            "detail": failure.detail,
        }
        print(json.dumps(report, ensure_ascii=False, sort_keys=True, separators=(",", ":")))
        return 2
    print(json.dumps(report, ensure_ascii=False, sort_keys=True, separators=(",", ":")))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
