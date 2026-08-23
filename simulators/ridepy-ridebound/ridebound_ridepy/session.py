from __future__ import annotations

import hashlib
import pathlib
from dataclasses import dataclass
from typing import Any, Iterable

from ridebound_fleetpy.mapping import canonical_json_bytes, wp4_policy_binding_hash
from ridebound_fleetpy.runner_client import RunnerClient
from ridebound_fleetpy.transcript import ProtocolTranscriptRecorder


RIDEPY_VERSION = "2.10.1"
RIDEPY_COMMIT = "bf1863e49a432f2f1f6230f86b2777a5ef5b9f14"
ADAPTER_ID = "ridepy-ridebound"
ADAPTER_VERSION = "1.0.0"
MAXIMUM_LINE_BYTES = 64 * 1024 * 1024


def _sha256(path: pathlib.Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


@dataclass(frozen=True)
class RidePySessionSettings:
    runner_command: tuple[str, ...]
    runner_dll: pathlib.Path
    commitment_config: pathlib.Path
    wp4_config: pathlib.Path
    artifact_paths: tuple[pathlib.Path, ...]
    run_id: str
    scenario_id: str
    master_seed: int
    core_commit: str
    policy_id: str
    policy_version: str
    commitment_policy_id: str
    service_class: str = "standard"
    timeout_seconds: float = 60.0
    transcript_path: pathlib.Path | None = None

    @property
    def policy_binding_hash(self) -> str:
        return wp4_policy_binding_hash(self.commitment_config, self.wp4_config)


class RideBoundRidePySession:
    """Own one exact Runner process with RidePy's explicit node-only contract."""

    def __init__(self, settings: RidePySessionSettings) -> None:
        self.settings = settings
        self._transcript = (
            ProtocolTranscriptRecorder(settings.transcript_path)
            if settings.transcript_path is not None
            else None
        )
        self._client = RunnerClient(
            settings.runner_command,
            settings.artifact_paths,
            timeout_seconds=settings.timeout_seconds,
            maximum_input_line_bytes=MAXIMUM_LINE_BYTES,
            maximum_output_line_bytes=MAXIMUM_LINE_BYTES,
            expected_position_model="nodeOnly",
            required_capabilities=(
                "exactEventOrdering",
                "dynamicTravelTimes",
                "oldPlanProjection",
            ),
            transcript_writer=(
                self._transcript.record if self._transcript is not None else None
            ),
        )
        self._initialized = False
        self._closed = False
        self._manifest_hash: str | None = None

    @property
    def manifest_hash(self) -> str | None:
        return self._manifest_hash

    @property
    def artifact_receipts(self) -> dict[str, list[dict[str, str]]]:
        return self._client.artifact_receipts

    @property
    def client_state(self) -> str:
        return self._client.state

    def initialize(
        self,
        snapshot: dict[str, Any],
        node_ids: Iterable[str],
        request_ids: Iterable[str],
        vehicle_ids: Iterable[str],
    ) -> dict[str, Any]:
        nodes = sorted(set(node_ids))
        requests = sorted(set(request_ids))
        vehicles = sorted(set(vehicle_ids))
        if not nodes or not vehicles or self._initialized or self._closed:
            raise RuntimeError("RBWP10_SESSION_INITIALIZE_INVALID")
        scenario = {
            "scenarioId": self.settings.scenario_id,
            "nodes": nodes,
            "requestIds": requests,
            "vehicleIds": vehicles,
            "travelSnapshotHash": snapshot["snapshotHash"],
        }
        scenario_hash = hashlib.sha256(
            b"RideBound.Wp10Scenario.v1\0" + canonical_json_bytes(scenario)
        ).hexdigest()
        graph_hash = hashlib.sha256(
            b"RideBound.Wp10Graph.v1\0" + canonical_json_bytes({"nodes": nodes})
        ).hexdigest()
        capability = {
            "status": "accepted",
            "positionModel": "nodeOnly",
            "capabilities": [
                "dynamicTravelTimes",
                "exactEventOrdering",
                "oldPlanProjection",
            ],
            "maxFleetSize": 5000,
            "maxRequestCount": 100000,
        }
        initialize = {
            "schemaVersion": "1.0.0",
            "messageType": "initializeRun",
            "runId": self.settings.run_id,
            "scenarioId": self.settings.scenario_id,
            "payload": {
                "manifest": {
                    "protocolVersion": "1.0.0",
                    "masterSeed": self.settings.master_seed,
                    "policyId": self.settings.policy_id,
                    "policyVersion": self.settings.policy_version,
                    "policyConfigurationHash": self.settings.policy_binding_hash,
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
                    "capabilitySelection": capability,
                    "adapter": {
                        "adapterId": ADAPTER_ID,
                        "adapterVersion": ADAPTER_VERSION,
                    },
                    "simulator": {
                        "simulatorId": "ridepy",
                        "simulatorVersion": RIDEPY_VERSION,
                        "upstreamCommitSha": RIDEPY_COMMIT,
                    },
                    "coreCommitSha": self.settings.core_commit,
                    "binarySha256": _sha256(self.settings.runner_dll),
                }
            },
        }
        self._client.start()
        self._client.negotiate(_hello())
        initialized = self._client.initialize(initialize)
        self._manifest_hash = initialized["payload"]["manifestHash"]
        self._initialized = True
        return initialized

    def decide(self, event_batch: dict[str, Any]) -> dict[str, Any]:
        if not self._initialized or self._closed:
            raise RuntimeError("RBWP10_SESSION_STATE_INVALID")
        return self._client.decide(event_batch)

    def acknowledge(self, decision: dict[str, Any]) -> None:
        self._client.acknowledge(decision["payload"]["decisionHash"])

    def checkpoint(self) -> dict[str, Any]:
        if not self._initialized or self._closed:
            raise RuntimeError("RBWP10_SESSION_STATE_INVALID")
        return self._client.request_checkpoint()

    def close(self) -> None:
        if self._closed:
            return
        try:
            self._client.shutdown()
        finally:
            if self._transcript is not None:
                self._transcript.close()
            self._closed = True


def _hello() -> dict[str, Any]:
    return {
        "schemaVersion": "1.0.0",
        "messageType": "hello",
        "payload": {
            "adapterId": ADAPTER_ID,
            "adapterVersion": ADAPTER_VERSION,
            "supportedSchemaVersions": ["1.0.0"],
            "positionModel": "nodeOnly",
            "capabilities": [
                "oldPlanProjection",
                "exactEventOrdering",
                "dynamicTravelTimes",
            ],
            "maxFleetSize": 5000,
            "maxRequestCount": 100000,
        },
    }
