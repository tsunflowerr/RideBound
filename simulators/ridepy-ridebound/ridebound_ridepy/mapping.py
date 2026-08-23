from __future__ import annotations

import hashlib
import math
from typing import Any, Iterable

from ridebound_fleetpy.errors import AdapterFailure
from ridebound_fleetpy.mapping import (
    MAX_CANONICAL_INTEGER,
    CanonicalIdRegistry,
    canonical_json_bytes,
    seconds_to_milliseconds,
)


def _fail(code: str, path: str, detail: str) -> AdapterFailure:
    return AdapterFailure(code, path, detail)


def _positive_integer(value: Any, path: str) -> int:
    if isinstance(value, bool) or not isinstance(value, int):
        raise _fail("RBWP10_INTEGER_TYPE_INVALID", path, type(value).__name__)
    if value < 1 or value > MAX_CANONICAL_INTEGER:
        raise _fail("RBWP10_INTEGER_RANGE_INVALID", path, str(value))
    return value


def _milliseconds(value: Any, path: str) -> int:
    try:
        return seconds_to_milliseconds(value, path)
    except AdapterFailure as exc:
        raise _fail(exc.code.replace("RBWP7_", "RBWP10_"), exc.path, exc.detail) from exc


class RidePyProtocolMapper:
    """Strict integer/time/identity mapping for RidePy's graph model."""

    def __init__(self) -> None:
        self.requests = CanonicalIdRegistry("rq")
        self.vehicles = CanonicalIdRegistry("veh")
        self.nodes = CanonicalIdRegistry("node")
        self.stops = CanonicalIdRegistry("stop")

    def node_id(self, raw_node: Any) -> str:
        if isinstance(raw_node, bool) or not isinstance(raw_node, int):
            raise _fail("RBWP10_NODE_ID_TYPE_INVALID", "$.position.node", type(raw_node).__name__)
        return self.nodes.register(raw_node)

    def raw_node(self, node_id: str) -> int:
        return self.nodes.resolve(node_id)

    def raw_request(self, request_id: str) -> Any:
        return self.requests.resolve(request_id)

    def raw_vehicle(self, vehicle_id: str) -> Any:
        return self.vehicles.resolve(vehicle_id)

    def position(self, raw_node: Any) -> dict[str, Any]:
        return {"kind": "node", "nodeId": self.node_id(raw_node)}

    def request(
        self,
        request: Any,
        service_class: str,
        policy_id: str,
    ) -> dict[str, Any]:
        try:
            raw_id = request.request_id
            arrival = request.creation_timestamp
            origin = request.origin
            destination = request.destination
            earliest = request.pickup_timewindow_min
            latest = request.pickup_timewindow_max
            delivery_latest = request.delivery_timewindow_max
        except AttributeError as exc:
            raise _fail("RBWP10_REQUEST_CONTRACT_MISSING", "$.request", str(exc)) from exc
        if isinstance(raw_id, bool) or not isinstance(raw_id, (int, str)):
            raise _fail("RBWP10_REQUEST_ID_TYPE_INVALID", "$.request.requestId", type(raw_id).__name__)
        origin_id = self.node_id(origin)
        destination_id = self.node_id(destination)
        if origin_id == destination_id:
            raise _fail("RBWP10_REQUEST_ZERO_LENGTH", "$.request", origin_id)
        arrival_ms = _milliseconds(arrival, "$.request.arrivalTime")
        earliest_ms = _milliseconds(earliest, "$.request.earliestPickup")
        latest_ms = _milliseconds(latest, "$.request.latestPickup")
        delivery_latest_ms = _milliseconds(delivery_latest, "$.request.latestDelivery")
        if not arrival_ms <= earliest_ms <= latest_ms <= delivery_latest_ms:
            raise _fail(
                "RBWP10_REQUEST_TIME_ORDER_INVALID",
                "$.request",
                f"{arrival_ms}<={earliest_ms}<={latest_ms}<={delivery_latest_ms}",
            )
        max_ride_ms = delivery_latest_ms - earliest_ms
        if max_ride_ms < 1:
            raise _fail("RBWP10_REQUEST_MAX_RIDE_INVALID", "$.request.maxRideTime", str(max_ride_ms))
        if not isinstance(service_class, str) or not service_class:
            raise _fail("RBWP10_SERVICE_CLASS_INVALID", "$.request.serviceClass", repr(service_class))
        if not isinstance(policy_id, str) or not policy_id:
            raise _fail("RBWP10_POLICY_ID_INVALID", "$.request.commitmentPolicyId", repr(policy_id))
        return {
            "requestId": self.requests.register(raw_id),
            "arrivalTimeMs": arrival_ms,
            "originNodeId": origin_id,
            "destinationNodeId": destination_id,
            "earliestPickupMs": earliest_ms,
            "latestPickupMs": latest_ms,
            "maxRideTimeMs": max_ride_ms,
            "partySize": 1,
            "serviceClass": service_class,
            "commitmentPolicyId": policy_id,
        }

    def travel_snapshot(self, version: int, space: Any) -> dict[str, Any]:
        version = _positive_integer(version, "$.snapshot.version")
        try:
            raw_nodes = list(space.vertices)
        except (AttributeError, TypeError) as exc:
            raise _fail("RBWP10_TRAVEL_GRAPH_INVALID", "$.snapshot.nodes", str(exc)) from exc
        if len(raw_nodes) < 2 or len(set(raw_nodes)) != len(raw_nodes):
            raise _fail("RBWP10_TRAVEL_GRAPH_INVALID", "$.snapshot.nodes", repr(raw_nodes))
        nodes = sorted(raw_nodes)
        for node in nodes:
            self.node_id(node)
        arcs: list[dict[str, Any]] = []
        for origin in nodes:
            for destination in nodes:
                if origin == destination:
                    continue
                try:
                    seconds = space.t(origin, destination)
                except (ArithmeticError, KeyError, TypeError, ValueError) as exc:
                    raise _fail(
                        "RBWP10_TRAVEL_ARC_UNAVAILABLE",
                        "$.snapshot.arcs",
                        f"{origin}->{destination}: {exc}",
                    ) from exc
                if isinstance(seconds, bool) or not isinstance(seconds, (int, float)):
                    raise _fail("RBWP10_TRAVEL_TIME_INVALID", "$.snapshot.arcs", repr(seconds))
                if isinstance(seconds, float) and not math.isfinite(seconds):
                    raise _fail("RBWP10_TRAVEL_GRAPH_DISCONNECTED", "$.snapshot.arcs", f"{origin}->{destination}")
                milliseconds = _milliseconds(seconds, "$.snapshot.arcs.travelTime")
                if milliseconds < 1:
                    raise _fail("RBWP10_TRAVEL_TIME_INVALID", "$.snapshot.arcs", str(milliseconds))
                arcs.append(
                    {
                        "fromNodeId": self.node_id(origin),
                        "toNodeId": self.node_id(destination),
                        "travelTimeMs": milliseconds,
                    }
                )
        arcs.sort(key=lambda value: (value["fromNodeId"], value["toNodeId"]))
        content = {"version": version, "arcs": arcs}
        snapshot_hash = hashlib.sha256(
            b"RideBound.RidePyTravelSnapshot.v1\0" + canonical_json_bytes(content)
        ).hexdigest()
        return {"version": version, "snapshotHash": snapshot_hash, "arcs": arcs}

    def stop_id(self, request_id: str, kind: str) -> str:
        if kind not in {"pickup", "dropOff"}:
            raise _fail("RBWP10_STOP_KIND_INVALID", "$.stop.kind", repr(kind))
        raw_request = self.requests.resolve(request_id)
        return self.stops.register((raw_request, kind))

    def register_vehicles(self, vehicle_ids: Iterable[Any]) -> list[str]:
        result = []
        for vehicle_id in vehicle_ids:
            if isinstance(vehicle_id, bool) or not isinstance(vehicle_id, (int, str)):
                raise _fail("RBWP10_VEHICLE_ID_TYPE_INVALID", "$.vehicle.vehicleId", type(vehicle_id).__name__)
            result.append(self.vehicles.register(vehicle_id))
        return result
