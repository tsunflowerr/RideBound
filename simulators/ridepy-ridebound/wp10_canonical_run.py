#!/usr/bin/env python3
from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import time
from typing import Any

from ridepy.data_structures import TransportationRequest
from ridepy.util.spaces import Graph

from ridebound_fleetpy.mapping import canonical_json_bytes, seconds_to_milliseconds

from ridebound_ridepy.fleet_state import CommitFleetState
from ridebound_ridepy.session import RideBoundRidePySession, RidePySessionSettings


def _sha256(path: pathlib.Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _read_json(path: pathlib.Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8") as source:
        value = json.load(source)
    if not isinstance(value, dict):
        raise ValueError(f"expected object: {path}")
    return value


def _requests() -> list[TransportationRequest]:
    specifications = (
        (0, 0, 1, 4, 0, 80, 0, 250),
        (1, 0, 5, 2, 0, 80, 0, 250),
        (2, 0, 0, 3, 0, 100, 0, 300),
        (3, 0, 4, 1, 0, 45, 0, 160),
        (4, 250, 2, 5, 250, 320, 250, 450),
    )
    return [
        TransportationRequest(
            request_id=request_id,
            creation_timestamp=creation,
            origin=origin,
            destination=destination,
            pickup_timewindow_min=pickup_min,
            pickup_timewindow_max=pickup_max,
            delivery_timewindow_min=delivery_min,
            delivery_timewindow_max=delivery_max,
        )
        for (
            request_id,
            creation,
            origin,
            destination,
            pickup_min,
            pickup_max,
            delivery_min,
            delivery_max,
        ) in specifications
    ]


def _space() -> Graph:
    return Graph(
        vertices=[0, 1, 2, 3, 4, 5],
        edges=[(0, 1), (1, 2), (2, 3), (3, 4), (4, 5), (0, 5), (1, 4)],
        weights=[20.0, 18.0, 22.0, 17.0, 21.0, 30.0, 38.0],
        velocity=1,
    )


def _canonical_event(event: dict[str, Any]) -> dict[str, Any]:
    time_fields = {
        "timestamp": "timestampMs",
        "pickup_timewindow_min": "pickupTimewindowMinMs",
        "pickup_timewindow_max": "pickupTimewindowMaxMs",
        "delivery_timewindow_min": "deliveryTimewindowMinMs",
        "delivery_timewindow_max": "deliveryTimewindowMaxMs",
    }
    result: dict[str, Any] = {}
    for key, value in event.items():
        target = time_fields.get(key)
        if target is not None:
            result[target] = seconds_to_milliseconds(value, f"$.simulatorEvent.{key}")
        else:
            result[key] = value
    return result


def run(arguments: argparse.Namespace) -> dict[str, Any]:
    output = arguments.output.resolve()
    if output.exists() and any(output.iterdir()):
        raise RuntimeError(f"output directory is not empty: {output}")
    output.mkdir(parents=True, exist_ok=True)
    wp4 = _read_json(arguments.wp4_config)
    transcript = output / "protocol-transcript.ndjson"
    runner_files = tuple(sorted(path for path in arguments.runner_root.rglob("*") if path.is_file()))
    adapter_files = tuple(sorted((pathlib.Path(__file__).parent / "ridebound_ridepy").glob("*.py")))
    artifacts = tuple(
        dict.fromkeys(
            (
                *runner_files,
                arguments.commitment_config,
                arguments.wp4_config,
                arguments.source_receipt,
                *adapter_files,
            )
        )
    )
    settings = RidePySessionSettings(
        runner_command=(
            "dotnet",
            str(arguments.runner_root / "RideBound.Runner.dll"),
            "--mode",
            "commitment",
            "--policy-config",
            str(arguments.commitment_config),
            "--wp4-config",
            str(arguments.wp4_config),
            "--solver-seed-source",
            "manifest-master-seed",
            "--maximum-line-bytes",
            str(64 * 1024 * 1024),
        ),
        runner_dll=arguments.runner_root / "RideBound.Runner.dll",
        commitment_config=arguments.commitment_config,
        wp4_config=arguments.wp4_config,
        artifact_paths=artifacts,
        run_id=f"wp10-canonical-{arguments.arm.lower()}-seed{arguments.seed}",
        scenario_id="wp10-canonical-2v5-v1",
        master_seed=arguments.seed,
        core_commit=arguments.core_commit,
        policy_id=wp4["policyId"],
        policy_version=wp4["policyVersion"],
        commitment_policy_id="wp6-synthetic-policy-overlay-v1",
        transcript_path=transcript,
    )
    requests = _requests()
    session = RideBoundRidePySession(settings)
    state = CommitFleetState(
        initial_locations={0: 0, 1: 5},
        space=_space(),
        seat_capacities=2,
        session=session,
        declared_requests=requests,
    )
    simulator_events: list[dict[str, Any]] = []
    traffic_receipt: dict[str, Any] | None = None
    started = time.monotonic()
    try:
        state.start()
        for request in requests:
            simulator_events.extend(state.fast_forward(request.creation_timestamp))
            if request.request_id == 2:
                traffic_receipt = state.update_travel_times(
                    request.creation_timestamp,
                    ((1, 2, 31.0), (3, 4, 29.0)),
                )
            simulator_events.append(
                {
                    "event_type": "RequestSubmissionEvent",
                    "request_id": request.request_id,
                    "timestamp": request.creation_timestamp,
                    "origin": request.origin,
                    "destination": request.destination,
                }
            )
            simulator_events.append(state.handle_transportation_request(request))
        simulator_events.extend(state.fast_forward(1000))
        checkpoint = state.checkpoint()
    finally:
        state.close()
    elapsed = time.monotonic() - started
    counts: dict[str, int] = {}
    for event in simulator_events:
        counts[event["event_type"]] = counts.get(event["event_type"], 0) + 1
    completed = sorted(
        request_id for request_id, status in state.request_states.items() if status == "completed"
    )
    rejected = sorted(
        request_id for request_id, status in state.request_states.items() if status == "rejected"
    )
    summary = {
        "schemaVersion": "1.0.0",
        "bundleType": "ridebound-wp10-canonical-v1",
        "arm": arguments.arm,
        "seed": arguments.seed,
        "scenarioId": settings.scenario_id,
        "runId": settings.run_id,
        "status": "pass",
        "elapsedMilliseconds": round(elapsed * 1000),
        "manifestHash": session.manifest_hash,
        "checkpointHash": checkpoint["payload"]["checkpointHash"],
        "trafficSnapshotHash": traffic_receipt["snapshotHash"] if traffic_receipt else None,
        "requestStates": [[key, state.request_states[key]] for key in sorted(state.request_states)],
        "assignments": [[key, state.assignments[key]] for key in sorted(state.assignments)],
        "completedRequestIds": completed,
        "rejectedRequestIds": rejected,
        "eventCounts": dict(sorted(counts.items())),
        "decisionCount": len(state.decisions),
        "promisePublicationCount": len(state.publications),
        "nativeLifecycleCount": len(state.native_events),
        "artifactReceipts": session.artifact_receipts,
        "inputs": {
            "coreCommit": arguments.core_commit,
            "runnerDllSha256": _sha256(arguments.runner_root / "RideBound.Runner.dll"),
            "commitmentConfigSha256": _sha256(arguments.commitment_config),
            "wp4ConfigSha256": _sha256(arguments.wp4_config),
            "sourceReceiptSha256": _sha256(arguments.source_receipt),
        },
    }
    canonical_events = [_canonical_event(event) for event in simulator_events]
    (output / "simulator-events.json").write_bytes(canonical_json_bytes(canonical_events) + b"\n")
    (output / "summary.json").write_bytes(canonical_json_bytes(summary) + b"\n")
    files = []
    for path in sorted(output.iterdir()):
        if path.is_file() and path.name != "bundle-manifest.json":
            files.append({"path": path.name, "sha256": _sha256(path), "bytes": path.stat().st_size})
    manifest = {
        "schemaVersion": "1.0.0",
        "bundleType": "ridebound-wp10-canonical-v1",
        "files": files,
    }
    (output / "bundle-manifest.json").write_bytes(canonical_json_bytes(manifest) + b"\n")
    return summary


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--arm", required=True, choices=("B1", "C1"))
    parser.add_argument("--seed", required=True, type=int)
    parser.add_argument("--core-commit", required=True)
    parser.add_argument("--runner-root", required=True, type=pathlib.Path)
    parser.add_argument("--commitment-config", required=True, type=pathlib.Path)
    parser.add_argument("--wp4-config", required=True, type=pathlib.Path)
    parser.add_argument("--source-receipt", required=True, type=pathlib.Path)
    parser.add_argument("--output", required=True, type=pathlib.Path)
    arguments = parser.parse_args()
    print(json.dumps(run(arguments), ensure_ascii=False, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
