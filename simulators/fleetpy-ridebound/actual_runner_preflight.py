#!/usr/bin/env python3
"""Run a tiny protocol lifecycle against an actual published RideBound Runner."""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import subprocess
from dataclasses import dataclass

from ridebound_fleetpy.mapping import (
    FleetPyProtocolMapper,
    canonical_json_bytes,
    wp4_policy_binding_hash,
)
from ridebound_fleetpy.runner_client import RunnerClient


FLEETPY_COMMIT = "053aa9d4fcfde91c5d303435d5748f9206c071b0"


@dataclass
class _PlanRequest:
    o_pos: tuple[int, None, None] = (1, None, None)
    d_pos: tuple[int, None, None] = (2, None, None)
    rq_time: int = 1
    t_pu_earliest: int = 1
    t_pu_latest: int = 3
    max_trip_time: int = 10
    nr_pax: int = 1

    def get_rid_struct(self) -> int:
        return 101


def _sha256(path: pathlib.Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _git_commit(repository: pathlib.Path) -> str:
    completed = subprocess.run(
        ["git", "-C", str(repository), "rev-parse", "HEAD"],
        check=True,
        stdout=subprocess.PIPE,
        text=True,
        encoding="ascii",
    )
    return completed.stdout.strip()


def _hello() -> dict:
    return {
        "schemaVersion": "1.0.0",
        "messageType": "hello",
        "payload": {
            "adapterId": "fleetpy-ridebound",
            "adapterVersion": "1.0.0",
            "supportedSchemaVersions": ["1.0.0"],
            "positionModel": "directedEdgeProgress",
            "capabilities": [
                "oldPlanProjection",
                "cancellations",
                "exactEventOrdering",
                "dynamicTravelTimes",
            ],
            "maxFleetSize": 5000,
            "maxRequestCount": 100000,
        },
    }


def run(arguments: argparse.Namespace) -> dict:
    repository = pathlib.Path(__file__).parents[2]
    runner_root = arguments.runner_root.resolve()
    runner_dll = runner_root / "RideBound.Runner.dll"
    published_files = sorted(path for path in runner_root.iterdir() if path.is_file())
    artifact_paths = [arguments.dotnet.resolve(), *published_files, arguments.commitment_config.resolve(), arguments.wp4_config.resolve()]
    mapper = FleetPyProtocolMapper()
    snapshot = mapper.travel_snapshot(
        1,
        [
            (0, 1, 1),
            (1, 0, 1),
            (0, 2, 2),
            (2, 0, 2),
            (1, 2, 1),
            (2, 1, 1),
        ],
    )
    request = mapper.request(
        _PlanRequest(),
        "standard",
        "wp6-synthetic-policy-overlay-v1",
    )
    vehicle_id = mapper.vehicles.register((0, 1))
    node_zero = mapper.position((0, None, None))
    scenario_content = {
        "scenarioId": "wp7-runner-client-preflight-v1",
        "nodes": sorted([mapper.node_id(0), mapper.node_id(1), mapper.node_id(2)]),
        "requestId": request["requestId"],
        "vehicleId": vehicle_id,
        "travelSnapshotHash": snapshot["snapshotHash"],
    }
    scenario_hash = hashlib.sha256(
        b"RideBound.Wp7Scenario.v1\0" + canonical_json_bytes(scenario_content)
    ).hexdigest()
    graph_hash = hashlib.sha256(
        b"RideBound.Wp7Graph.v1\0" + canonical_json_bytes({"nodes": scenario_content["nodes"]})
    ).hexdigest()
    wp4 = json.loads(arguments.wp4_config.read_text(encoding="utf-8"))
    run_id = "wp7-actual-runner-preflight"
    scenario_id = scenario_content["scenarioId"]
    policy_binding = wp4_policy_binding_hash(arguments.commitment_config, arguments.wp4_config)
    initialize = {
        "schemaVersion": "1.0.0",
        "messageType": "initializeRun",
        "runId": run_id,
        "scenarioId": scenario_id,
        "payload": {
            "manifest": {
                "protocolVersion": "1.0.0",
                "masterSeed": 7,
                "policyId": wp4["policyId"],
                "policyVersion": wp4["policyVersion"],
                "policyConfigurationHash": policy_binding,
                "scenarioContentHash": scenario_hash,
                "graphSnapshotHash": graph_hash,
                "travelTimeSnapshotHash": snapshot["snapshotHash"],
                "costUnitId": "abstract-generalized-cost-v1",
                "sourceUnitConversions": [
                    {
                        "quantity": "time",
                        "sourceUnit": "second",
                        "canonicalUnit": "millisecond",
                        "roundingRule": "roundTiesToEven",
                    },
                    {
                        "quantity": "distance",
                        "sourceUnit": "meter",
                        "canonicalUnit": "millimeter",
                        "roundingRule": "roundTiesToEven",
                    },
                ],
                "capabilitySelection": {
                    "status": "accepted",
                    "positionModel": "directedEdgeProgress",
                    "capabilities": [
                        "dynamicTravelTimes",
                        "exactEventOrdering",
                        "oldPlanProjection",
                    ],
                    "maxFleetSize": 5000,
                    "maxRequestCount": 100000,
                },
                "adapter": {
                    "adapterId": "fleetpy-ridebound",
                    "adapterVersion": "1.0.0",
                },
                "simulator": {
                    "simulatorId": "fleetpy",
                    "simulatorVersion": "1.0.2",
                    "upstreamCommitSha": FLEETPY_COMMIT,
                },
                "coreCommitSha": _git_commit(repository),
                "binarySha256": _sha256(runner_dll),
            }
        },
    }
    event_batch = {
        "schemaVersion": "1.0.0",
        "messageType": "eventBatch",
        "runId": run_id,
        "scenarioId": scenario_id,
        "epochId": 1,
        "simTimeMs": 1000,
        "payload": {
            "events": [
                {
                    "eventSeq": 1,
                    "eventType": "travelTimesUpdated",
                    "payload": {"snapshot": snapshot},
                },
                {
                    "eventSeq": 2,
                    "eventType": "requestArrived",
                    "payload": {"request": request},
                },
                {
                    "eventSeq": 3,
                    "eventType": "vehicleAdvanced",
                    "payload": {
                        "vehicle": {
                            "vehicleId": vehicle_id,
                            "capacity": 4,
                            "occupiedSeats": 0,
                            "position": node_zero,
                            "onboardRequestIds": [],
                            "acceptedRequestIds": [],
                            "route": {
                                "planVersion": 0,
                                "executedStopCount": 0,
                                "frozenPrefix": [],
                                "mutableSuffix": [],
                            },
                        }
                    },
                },
            ]
        },
    }
    booking_confirmation_batch = {
        "schemaVersion": "1.0.0",
        "messageType": "eventBatch",
        "runId": run_id,
        "scenarioId": scenario_id,
        "epochId": 2,
        "simTimeMs": 1000,
        "payload": {
            "events": [
                {
                    "eventSeq": 4,
                    "eventType": "bookingConfirmed",
                    "payload": {"requestId": request["requestId"]},
                }
            ]
        },
    }
    command = [
        str(arguments.dotnet.resolve()),
        str(runner_dll),
        "--mode",
        "commitment",
        "--policy-config",
        str(arguments.commitment_config.resolve()),
        "--wp4-config",
        str(arguments.wp4_config.resolve()),
        "--solver-seed-source",
        "manifest-master-seed",
    ]
    client = RunnerClient(command, artifact_paths, timeout_seconds=60)
    try:
        client.start()
        acknowledgement = client.negotiate(_hello())
        initialized = client.initialize(initialize)
        offer_decision = client.decide(event_batch)
        offer_checkpoint = client.acknowledge_and_checkpoint(
            offer_decision["payload"]["decisionHash"]
        )
        offer_action_types = [
            action["decisionType"] for action in offer_decision["payload"]["actions"]
        ]
        if "requestAccepted" not in offer_action_types:
            raise RuntimeError(
                "provisional offer did not create an accepted assignment: "
                f"actions={offer_action_types!r}"
            )
        if "vehiclePlanUpdated" not in offer_action_types:
            raise RuntimeError(
                "provisional offer did not create a FleetPy-applicable plan: "
                f"actions={offer_action_types!r}"
            )
        if "promisePublished" in offer_action_types:
            raise RuntimeError("provisional offer published a rider promise")

        confirmation_decision = client.decide(booking_confirmation_batch)
        confirmation_checkpoint = client.acknowledge_and_checkpoint(
            confirmation_decision["payload"]["decisionHash"]
        )
        confirmation_action_types = [
            action["decisionType"]
            for action in confirmation_decision["payload"]["actions"]
        ]
        if confirmation_action_types.count("promisePublished") != 1:
            raise RuntimeError("booking confirmation did not publish exactly one promise")
        if "requestAccepted" in confirmation_action_types:
            raise RuntimeError("booking confirmation accepted the request a second time")
        client.shutdown()
        return {
            "schemaVersion": "1.0.0",
            "status": "pass",
            "policyId": wp4["policyId"],
            "policyBindingHash": policy_binding,
            "helloAck": acknowledgement["payload"],
            "manifestHash": initialized["payload"]["manifestHash"],
            "offerDecisionHash": offer_decision["payload"]["decisionHash"],
            "offerActionTypes": offer_action_types,
            "offerCheckpointHash": offer_checkpoint["payload"]["checkpointHash"],
            "confirmationDecisionHash": confirmation_decision["payload"]["decisionHash"],
            "confirmationActionTypes": confirmation_action_types,
            "confirmationCheckpointHash": confirmation_checkpoint["payload"]["checkpointHash"],
            "artifactCount": len(artifact_paths),
            "artifactReceipts": client.artifact_receipts,
        }
    finally:
        client.shutdown()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--runner-root", required=True, type=pathlib.Path)
    parser.add_argument("--dotnet", required=True, type=pathlib.Path)
    parser.add_argument("--commitment-config", required=True, type=pathlib.Path)
    parser.add_argument("--wp4-config", required=True, type=pathlib.Path)
    arguments = parser.parse_args()
    report = run(arguments)
    print(json.dumps(report, ensure_ascii=False, sort_keys=True, separators=(",", ":")))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
