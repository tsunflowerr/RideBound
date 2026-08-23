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


def _json(path: pathlib.Path) -> Any:
    with path.open("r", encoding="utf-8") as source:
        return json.load(source)


def _canonical_event(event: dict[str, Any]) -> dict[str, Any]:
    time_fields = {
        "timestamp": "timestampMs",
        "pickup_timewindow_min": "pickupTimewindowMinMs",
        "pickup_timewindow_max": "pickupTimewindowMaxMs",
        "delivery_timewindow_min": "deliveryTimewindowMinMs",
        "delivery_timewindow_max": "deliveryTimewindowMaxMs",
    }
    return {
        time_fields.get(key, key): (
            seconds_to_milliseconds(value, f"$.simulatorEvent.{key}")
            if key in time_fields
            else value
        )
        for key, value in event.items()
    }


def _materialize(manifest: dict[str, Any], job: dict[str, Any]):
    cell = next(value for value in manifest["cells"] if value["cellId"] == job["cellId"])
    realization = next(
        value for value in manifest["realizations"] if value["realizationId"] == job["realizationId"]
    )
    graph = manifest["graph"]
    weights = [
        base + ((index + realization["rotation"]) % 3) * realization["weightDeltaSeconds"]
        for index, base in enumerate(graph["baseTravelSeconds"])
    ]
    space = Graph(
        vertices=graph["vertices"],
        edges=[tuple(edge) for edge in graph["edges"]],
        weights=weights,
        velocity=graph["velocity"],
    )
    rotate = realization["rotation"]
    od = [((origin + rotate) % 8, (destination + rotate) % 8) for origin, destination in manifest["baseOd"]]
    requests = []
    base_id = cell["cellOrdinal"] * 100 + rotate * 10
    for ordinal in range(cell["earlyRequestCount"]):
        latest = (
            cell["constrainedLatestPickupSeconds"]
            if ordinal == cell.get("constrainedOrdinal")
            else cell["earlyLatestPickupSeconds"]
        )
        requests.append(
            TransportationRequest(
                request_id=base_id + ordinal,
                creation_timestamp=0,
                origin=od[ordinal][0],
                destination=od[ordinal][1],
                pickup_timewindow_min=0,
                pickup_timewindow_max=latest,
                delivery_timewindow_min=0,
                delivery_timewindow_max=cell["earlyLatestDeliverySeconds"],
            )
        )
    for offset in range(cell["lateRequestCount"]):
        ordinal = cell["earlyRequestCount"] + offset
        requests.append(
            TransportationRequest(
                request_id=base_id + ordinal,
                creation_timestamp=cell["lateArrivalSeconds"],
                origin=od[ordinal][0],
                destination=od[ordinal][1],
                pickup_timewindow_min=cell["lateArrivalSeconds"],
                pickup_timewindow_max=cell["lateLatestPickupSeconds"],
                delivery_timewindow_min=cell["lateArrivalSeconds"],
                delivery_timewindow_max=cell["lateLatestDeliverySeconds"],
            )
        )
    initial_locations = {0: rotate, 1: (rotate + 4) % 8}
    return cell, realization, space, requests, initial_locations, weights


def _run_job(arguments, manifest, manifest_hash, job, arm, output):
    cell, realization, space, requests, initial_locations, initial_weights = _materialize(manifest, job)
    wp4_path = arguments.b1_config if arm == "B1" else arguments.c1_config
    wp4 = _json(wp4_path)
    output.mkdir(parents=True, exist_ok=False)
    transcript = output / "protocol-transcript.ndjson"
    adapter_root = pathlib.Path(__file__).parent
    artifacts = tuple(
        dict.fromkeys(
            (
                *(path for path in sorted(arguments.runner_root.rglob("*")) if path.is_file()),
                arguments.commitment_config,
                wp4_path,
                arguments.source_receipt,
                arguments.manifest,
                *sorted((adapter_root / "ridebound_ridepy").glob("*.py")),
                pathlib.Path(__file__).resolve(),
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
            str(wp4_path),
            "--solver-seed-source",
            "manifest-master-seed",
            "--maximum-line-bytes",
            str(64 * 1024 * 1024),
        ),
        runner_dll=arguments.runner_root / "RideBound.Runner.dll",
        commitment_config=arguments.commitment_config,
        wp4_config=wp4_path,
        artifact_paths=artifacts,
        run_id=f"wp10-{job['jobId']}-{arm.lower()}",
        scenario_id=f"wp10-{job['jobId']}",
        master_seed=realization["masterSeed"],
        core_commit=arguments.core_commit,
        policy_id=wp4["policyId"],
        policy_version=wp4["policyVersion"],
        commitment_policy_id="wp6-synthetic-policy-overlay-v1",
        transcript_path=transcript,
    )
    state = CommitFleetState(
        initial_locations=initial_locations,
        space=space,
        seat_capacities=2,
        session=RideBoundRidePySession(settings),
        declared_requests=requests,
    )
    simulator_events: list[dict[str, Any]] = []
    snapshots: list[str] = []
    started = time.monotonic()
    try:
        state.start()
        for ordinal, request in enumerate(requests):
            simulator_events.extend(state.fast_forward(request.creation_timestamp))
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
            update = cell["trafficUpdate"]
            if update is not None and ordinal == update["afterRequestOrdinal"]:
                changes = []
                for edge_index, addition in zip(update["edgeIndexes"], update["addSeconds"]):
                    origin, destination = manifest["graph"]["edges"][edge_index]
                    changes.append((origin, destination, initial_weights[edge_index] + addition))
                snapshots.append(state.update_travel_times(request.creation_timestamp, changes)["snapshotHash"])
        simulator_events.extend(state.fast_forward(2000))
        checkpoint = state.checkpoint()
    finally:
        state.close()
    counts: dict[str, int] = {}
    for event in simulator_events:
        counts[event["event_type"]] = counts.get(event["event_type"], 0) + 1
    arrived = len(requests)
    completed = sum(value == "completed" for value in state.request_states.values())
    rejected = sum(value == "rejected" for value in state.request_states.values())
    summary = {
        "schemaVersion": "1.0.0",
        "bundleType": "ridebound-wp10-subset-job-v1",
        "status": "pass",
        "arm": arm,
        "jobId": job["jobId"],
        "cellId": job["cellId"],
        "realizationId": job["realizationId"],
        "masterSeed": realization["masterSeed"],
        "manifestSha256": manifest_hash,
        "arrived": arrived,
        "completed": completed,
        "rejected": rejected,
        "eventCounts": dict(sorted(counts.items())),
        "nativeLifecycleCount": len(state.native_events),
        "decisionCount": len(state.decisions),
        "promisePublicationCount": len(state.publications),
        "trafficSnapshotHashes": snapshots,
        "assignments": [[key, state.assignments[key]] for key in sorted(state.assignments)],
        "requestStates": [[key, state.request_states[key]] for key in sorted(state.request_states)],
        "manifestHash": state.session.manifest_hash,
        "checkpointHash": checkpoint["payload"]["checkpointHash"],
        "elapsedMilliseconds": round((time.monotonic() - started) * 1000),
        "artifactReceipts": state.session.artifact_receipts,
        "inputHashes": {
            "sourceReceipt": _sha256(arguments.source_receipt),
            "commitmentConfig": _sha256(arguments.commitment_config),
            "wp4Config": _sha256(wp4_path),
            "runnerDll": _sha256(arguments.runner_root / "RideBound.Runner.dll"),
        },
    }
    (output / "simulator-events.json").write_bytes(
        canonical_json_bytes([_canonical_event(event) for event in simulator_events]) + b"\n"
    )
    (output / "summary.json").write_bytes(canonical_json_bytes(summary) + b"\n")
    files = [
        {"path": path.name, "sha256": _sha256(path), "bytes": path.stat().st_size}
        for path in sorted(output.iterdir())
        if path.is_file()
    ]
    (output / "bundle-manifest.json").write_bytes(
        canonical_json_bytes(
            {
                "schemaVersion": "1.0.0",
                "bundleType": "ridebound-wp10-subset-job-v1",
                "files": files,
            }
        )
        + b"\n"
    )
    print(
        json.dumps(
            {
                "jobId": job["jobId"],
                "arm": arm,
                "arrived": arrived,
                "completed": completed,
                "rejected": rejected,
                "status": "pass",
            },
            sort_keys=True,
        ),
        flush=True,
    )


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--manifest", required=True, type=pathlib.Path)
    parser.add_argument("--output-root", required=True, type=pathlib.Path)
    parser.add_argument("--core-commit", required=True)
    parser.add_argument("--runner-root", required=True, type=pathlib.Path)
    parser.add_argument("--commitment-config", required=True, type=pathlib.Path)
    parser.add_argument("--b1-config", required=True, type=pathlib.Path)
    parser.add_argument("--c1-config", required=True, type=pathlib.Path)
    parser.add_argument("--source-receipt", required=True, type=pathlib.Path)
    arguments = parser.parse_args()
    if arguments.output_root.exists():
        raise RuntimeError(f"output root already exists: {arguments.output_root}")
    arguments.output_root.mkdir(parents=True)
    manifest = _json(arguments.manifest)
    manifest_hash = _sha256(arguments.manifest)
    for job in manifest["jobs"]:
        for arm in ("B1", "C1"):
            _run_job(
                arguments,
                manifest,
                manifest_hash,
                job,
                arm,
                arguments.output_root / job["jobId"] / arm,
            )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
