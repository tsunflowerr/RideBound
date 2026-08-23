from __future__ import annotations

import math
from collections.abc import Iterable
from typing import Any

from ridepy.data_structures import InternalRequest, Stop, StopAction, TransportationRequest
from ridepy.fleet_state import FleetState
from ridepy.vehicle_state import VehicleState

from ridebound_fleetpy.errors import AdapterFailure
from ridebound_fleetpy.protocol import (
    OrderedEventBuffer,
    ParsedDecision,
    ProtocolRoute,
    ProtocolStop,
    parse_decision,
)

from .mapping import RidePyProtocolMapper
from .session import RideBoundRidePySession


PHASE_REACHED = 10
PHASE_LIFECYCLE = 30
PHASE_VEHICLE = 40


def _fail(code: str, path: str, detail: str) -> AdapterFailure:
    return AdapterFailure(code, path, detail)


def _unused_dispatcher(**_arguments: Any) -> Any:
    raise _fail("RBWP10_NATIVE_DISPATCH_BYPASS", "$.dispatcher", "Runner is sole decision owner")


class CommitFleetState(FleetState):
    """RidePy FleetState whose stoplist decisions come only from RideBound Runner."""

    def __init__(
        self,
        *,
        initial_locations: dict[Any, int],
        space: Any,
        seat_capacities: int | dict[Any, int],
        session: RideBoundRidePySession,
        declared_requests: Iterable[TransportationRequest],
        mapper: RidePyProtocolMapper | None = None,
    ) -> None:
        super().__init__(
            initial_locations=initial_locations,
            vehicle_state_class=VehicleState,
            space=space,
            dispatcher=_unused_dispatcher,
            seat_capacities=seat_capacities,
        )
        self.mapper = mapper or RidePyProtocolMapper()
        self.session = session
        self.events = OrderedEventBuffer()
        self.routes = {
            vehicle_id: ProtocolRoute(0, 0, (), ()) for vehicle_id in self.fleet
        }
        self.requests: dict[Any, TransportationRequest] = {}
        for request in declared_requests:
            if request.request_id in self.requests:
                raise _fail(
                    "RBWP10_REQUEST_DUPLICATE",
                    "$.scenario.requests",
                    repr(request.request_id),
                )
            self.requests[request.request_id] = request
            self.mapper.requests.register(request.request_id)
        self.request_states = {request_id: "declared" for request_id in self.requests}
        self.assignments: dict[Any, Any] = {}
        self.publications: list[dict[str, Any]] = []
        self.native_events: list[dict[str, Any]] = []
        self.decisions: list[dict[str, Any]] = []
        self._travel_version = 1
        self._last_time = 0.0
        self._closed = False
        self._started = False
        self._last_checkpoint: dict[str, Any] | None = None

    def start(self) -> None:
        if self._started or self._closed:
            raise _fail("RBWP10_STATE_START_INVALID", "$.state", repr((self._started, self._closed)))
        snapshot = self.mapper.travel_snapshot(self._travel_version, self.space)
        vehicle_ids = self.mapper.register_vehicles(self.fleet)
        request_ids = [self.mapper.requests.register(value) for value in self.requests]
        node_ids = [self.mapper.node_id(value) for value in self.space.vertices]
        self.session.initialize(snapshot, node_ids, request_ids, vehicle_ids)
        for vehicle_id in sorted(self.fleet, key=repr):
            self.events.append(
                0,
                PHASE_VEHICLE,
                "vehicleAdvanced",
                {"vehicle": self._vehicle_snapshot(vehicle_id)},
                dedupe_key=f"vehicle:{self.mapper.vehicles.register(vehicle_id)}",
            )
        self._started = True
        self._flush(0, prefix=(("travelTimesUpdated", {"snapshot": snapshot}),))

    def fast_forward(self, t: float):
        self._require_open()
        if not self._started:
            self.start()
        if isinstance(t, bool) or not isinstance(t, (int, float)) or not math.isfinite(t):
            raise _fail("RBWP10_CLOCK_INVALID", "$.clock", repr(t))
        if t < self._last_time:
            raise _fail("RBWP10_CLOCK_REGRESSION", "$.clock", f"{t} < {self._last_time}")
        native: list[dict[str, Any]] = []
        while True:
            next_service_times = [
                max(vehicle.stoplist[1].estimated_arrival_time, vehicle.stoplist[1].time_window_min)
                for vehicle in self.fleet.values()
                if len(vehicle.stoplist) > 1
            ]
            if not next_service_times:
                break
            next_time = min(next_service_times)
            if next_time > t:
                break
            ordered_events = []
            for vehicle_id in sorted(self.fleet, key=repr):
                for intra_vehicle_ordinal, event in enumerate(
                    self.fleet[vehicle_id].fast_forward_time(next_time)
                ):
                    ordered_events.append(
                        (
                            event["timestamp"],
                            repr(vehicle_id),
                            intra_vehicle_ordinal,
                            event,
                        )
                    )
            step_events = [record[3] for record in sorted(ordered_events)]
            if not step_events:
                raise _fail(
                    "RBWP10_CLOCK_STALLED",
                    "$.clock",
                    f"next service {next_time!r} produced no event",
                )
            self.t = next_time
            self._last_time = float(next_time)
            for event_ordinal, event in enumerate(step_events):
                self._record_native_stop(
                    event,
                    phase_base=PHASE_REACHED + 2 * event_ordinal,
                )
            self._flush(next_time)
            native.extend(step_events)
        tail_events = [
            event
            for vehicle in self.fleet.values()
            for event in vehicle.fast_forward_time(t)
        ]
        if tail_events:
            raise _fail("RBWP10_CLOCK_UNEXPECTED_TAIL_EVENT", "$.clock", repr(tail_events))
        self._last_time = float(t)
        self.t = t
        return native

    def handle_transportation_request(self, req: TransportationRequest):
        self._require_open()
        if req.request_id not in self.requests or self.requests[req.request_id] is not req:
            raise _fail("RBWP10_REQUEST_UNDECLARED", "$.request", repr(req.request_id))
        if self.request_states[req.request_id] != "declared":
            raise _fail("RBWP10_REQUEST_DUPLICATE", "$.request", repr(req.request_id))
        mapped = self.mapper.request(
            req,
            self.session.settings.service_class,
            self.session.settings.commitment_policy_id,
        )
        self.request_states[req.request_id] = "pending"
        self.events.append(
            self.t,
            PHASE_LIFECYCLE,
            "requestArrived",
            {"request": mapped},
            dedupe_key=f"request:{mapped['requestId']}",
        )
        self._flush(self.t)
        state = self.request_states[req.request_id]
        if state == "accepted":
            self.request_states[req.request_id] = "confirmed"
            self.events.append(
                self.t,
                PHASE_LIFECYCLE,
                "bookingConfirmed",
                {"requestId": mapped["requestId"]},
                dedupe_key=f"booking:{mapped['requestId']}",
            )
            self._flush(self.t)
            return {
                "event_type": "RequestAcceptanceEvent",
                "timestamp": self.t,
                "request_id": req.request_id,
                "origin": req.origin,
                "destination": req.destination,
                "pickup_timewindow_min": req.pickup_timewindow_min,
                "pickup_timewindow_max": req.pickup_timewindow_max,
                "delivery_timewindow_min": req.delivery_timewindow_min,
                "delivery_timewindow_max": req.delivery_timewindow_max,
            }
        if state == "rejected":
            return {
                "event_type": "RequestRejectionEvent",
                "timestamp": self.t,
                "request_id": req.request_id,
            }
        raise _fail("RBWP10_REQUEST_OUTCOME_MISSING", "$.decision", f"{req.request_id!r}: {state}")

    def handle_internal_request(self, req: InternalRequest):
        raise _fail("RBWP10_INTERNAL_REQUEST_UNSUPPORTED", "$.request", repr(req.request_id))

    def update_travel_times(
        self,
        t: float,
        updates: Iterable[tuple[int, int, float]],
    ) -> dict[str, Any]:
        self.fast_forward(t)
        prepared: list[tuple[int, int, float]] = []
        seen: set[tuple[int, int]] = set()
        for origin, destination, seconds in updates:
            edge = tuple(sorted((origin, destination)))
            if edge in seen:
                raise _fail("RBWP10_TRAVEL_UPDATE_DUPLICATE", "$.travelUpdate", repr(edge))
            seen.add(edge)
            if origin == destination or not self.space.G.has_edge(origin, destination):
                raise _fail("RBWP10_TRAVEL_UPDATE_EDGE_UNKNOWN", "$.travelUpdate", repr(edge))
            if (
                isinstance(seconds, bool)
                or not isinstance(seconds, (int, float))
                or not math.isfinite(seconds)
                or seconds <= 0
            ):
                raise _fail("RBWP10_TRAVEL_UPDATE_TIME_INVALID", "$.travelUpdate", repr(seconds))
            prepared.append((origin, destination, float(seconds) * self.space.velocity))
        if not prepared:
            raise _fail("RBWP10_TRAVEL_UPDATE_EMPTY", "$.travelUpdate", "no edges")
        for origin, destination, distance in prepared:
            self.space.G[origin][destination]["distance"] = distance
        self.space._update_distance_cache()
        for vehicle in self.fleet.values():
            vehicle.recompute_arrival_times_drive_first()
        self._travel_version += 1
        snapshot = self.mapper.travel_snapshot(self._travel_version, self.space)
        self._flush(t, prefix=(("travelTimesUpdated", {"snapshot": snapshot}),))
        return snapshot

    def checkpoint(self) -> dict[str, Any]:
        self._last_checkpoint = self.session.checkpoint()
        return self._last_checkpoint

    def close(self) -> None:
        if self._closed:
            return
        try:
            if self._started and self.session.client_state == "initialized":
                self.checkpoint()
        finally:
            self.session.close()
            self._closed = True

    def _flush(
        self,
        t: float,
        *,
        prefix: Iterable[tuple[str, dict[str, Any]]] = (),
    ) -> None:
        batch = self.events.drain(
            self.session.settings.run_id,
            self.session.settings.scenario_id,
            t,
            prefix=prefix,
        )
        decision = self.session.decide(batch)
        parsed = parse_decision(decision, self.mapper)
        self._apply_decision(parsed)
        self.session.acknowledge(decision)
        self.decisions.append(decision)

    def _apply_decision(self, parsed: ParsedDecision) -> None:
        prepared: dict[Any, tuple[ProtocolRoute, list[Stop], str]] = {}
        for action in parsed.plans:
            if action.raw_vehicle not in self.fleet:
                raise _fail("RBWP10_PLAN_VEHICLE_UNKNOWN", "$.decision.plan", repr(action.raw_vehicle))
            current = self.routes[action.raw_vehicle]
            if action.route == current:
                continue
            if (
                action.route.executed_stop_count != current.executed_stop_count
                or action.route.frozen_prefix != current.frozen_prefix
                or action.route.plan_version != current.plan_version + 1
            ):
                raise _fail(
                    "RBWP10_PLAN_CORE_BOUNDARY_MISMATCH",
                    f"$.vehicles[{action.raw_vehicle}].route",
                    "version/progress/frozen prefix changed",
                )
            prepared[action.raw_vehicle] = (
                action.route,
                self._prepare_stoplist(action.raw_vehicle, action.route),
                action.candidate_id,
            )
        for action in parsed.accepted:
            if self.request_states.get(action.raw_request) != "pending":
                raise _fail("RBWP10_ACCEPTANCE_STATE_INVALID", "$.decision.accepted", repr(action.raw_request))
            plan = prepared.get(action.raw_vehicle)
            route = plan[0] if plan is not None else self.routes.get(action.raw_vehicle)
            if route is None or plan is None or plan[2] != action.candidate_id:
                raise _fail("RBWP10_ACCEPTANCE_PLAN_MISMATCH", "$.decision.accepted", repr(action.raw_request))
            if not any(
                stop.raw_request == action.raw_request
                for stop in route.remaining_stops
            ):
                raise _fail("RBWP10_ACCEPTANCE_PLAN_MISMATCH", "$.decision.accepted", repr(action.raw_request))
        for outcome in parsed.outcomes:
            if self.request_states.get(outcome.raw_request) != "pending":
                raise _fail("RBWP10_OUTCOME_STATE_INVALID", "$.decision.outcome", repr(outcome.raw_request))
            if outcome.decision_type == "requestDeferred":
                raise _fail("RBWP10_DEFER_UNSUPPORTED", "$.decision.outcome", repr(outcome.raw_request))
        for promise in parsed.promises:
            if self.request_states.get(promise.raw_request) not in {"confirmed", "onboard", "completed"}:
                raise _fail("RBWP10_PROMISE_BEFORE_CONFIRMATION", "$.decision.promise", repr(promise.raw_request))

        # No RidePy object is changed until every action and every replacement
        # stoplist in the decision has passed the checks above.
        for vehicle_id, (route, stoplist, _candidate) in prepared.items():
            self.fleet[vehicle_id].stoplist = stoplist
            self.routes[vehicle_id] = route
        for action in parsed.accepted:
            self.request_states[action.raw_request] = "accepted"
            self.assignments[action.raw_request] = action.raw_vehicle
        for outcome in parsed.outcomes:
            self.request_states[outcome.raw_request] = "rejected"
        for promise in parsed.promises:
            self.publications.append(
                {
                    "requestId": promise.request_id,
                    "publicationId": promise.publication_id,
                    "promiseVersion": promise.promise_version,
                    "reasonCode": promise.reason_code,
                    "sourceEventSeq": promise.source_event_sequence,
                }
            )

    def _prepare_stoplist(self, vehicle_id: Any, route: ProtocolRoute) -> list[Stop]:
        vehicle = self.fleet[vehicle_id]
        cpe = vehicle.stoplist[0]
        result = [cpe]
        occupancy = cpe.occupancy_after_servicing
        previous_location = cpe.location
        previous_departure = max(cpe.estimated_arrival_time, cpe.time_window_min)
        for protocol_stop in route.remaining_stops:
            if protocol_stop.kind == "waypoint":
                raise _fail("RBWP10_WAYPOINT_UNSUPPORTED", "$.route.stop", protocol_stop.stop_id)
            if protocol_stop.service_duration_ms != 0:
                raise _fail(
                    "RBWP10_SERVICE_DURATION_UNSUPPORTED",
                    "$.route.stop.serviceDurationMs",
                    str(protocol_stop.service_duration_ms),
                )
            request = self.requests.get(protocol_stop.raw_request)
            if request is None:
                raise _fail("RBWP10_PLAN_REQUEST_UNKNOWN", "$.route.stop.requestId", repr(protocol_stop.raw_request))
            if protocol_stop.kind == "pickup":
                if protocol_stop.raw_node != request.origin:
                    raise _fail("RBWP10_PICKUP_NODE_MISMATCH", "$.route.stop.nodeId", repr(request.request_id))
                action = StopAction.pickup
                window_min = request.pickup_timewindow_min
                window_max = request.pickup_timewindow_max
                occupancy += 1
            elif protocol_stop.kind == "dropOff":
                if protocol_stop.raw_node != request.destination:
                    raise _fail("RBWP10_DROPOFF_NODE_MISMATCH", "$.route.stop.nodeId", repr(request.request_id))
                action = StopAction.dropoff
                window_min = request.delivery_timewindow_min
                window_max = request.delivery_timewindow_max
                occupancy -= 1
            else:
                raise _fail("RBWP10_STOP_KIND_INVALID", "$.route.stop.kind", protocol_stop.kind)
            if occupancy < 0 or occupancy > vehicle.seat_capacity:
                raise _fail("RBWP10_PLAN_CAPACITY_INVALID", "$.route.stop", str(occupancy))
            travel = self.space.t(previous_location, protocol_stop.raw_node)
            arrival = previous_departure + travel
            if not math.isfinite(arrival) or max(arrival, window_min) > window_max:
                raise _fail("RBWP10_PLAN_NATIVE_INFEASIBLE", "$.route.stop", protocol_stop.stop_id)
            stop = Stop(
                location=protocol_stop.raw_node,
                request=request,
                action=action,
                estimated_arrival_time=arrival,
                occupancy_after_servicing=occupancy,
                time_window_min=window_min,
                time_window_max=window_max,
            )
            result.append(stop)
            previous_location = stop.location
            previous_departure = stop.estimated_departure_time
        return result

    def _record_native_stop(
        self,
        event: dict[str, Any],
        *,
        phase_base: int = PHASE_REACHED,
    ) -> None:
        vehicle_id = event["vehicle_id"]
        request_id = event["request_id"]
        route = self.routes[vehicle_id]
        if not route.remaining_stops:
            raise _fail("RBWP10_LIFECYCLE_WITHOUT_ROUTE", "$.nativeEvent", repr(event))
        expected = route.remaining_stops[0]
        expected_kind = "pickup" if event["event_type"] == "PickupEvent" else "dropOff"
        if expected.raw_request != request_id or expected.kind != expected_kind:
            raise _fail(
                "RBWP10_LIFECYCLE_ORDER_MISMATCH",
                "$.nativeEvent",
                f"actual={event!r}; expected={expected!r}",
            )
        request_state = self.request_states.get(request_id)
        if expected_kind == "pickup" and request_state != "confirmed":
            raise _fail("RBWP10_PICKUP_STATE_INVALID", "$.nativeEvent", repr((request_id, request_state)))
        if expected_kind == "dropOff" and request_state != "onboard":
            raise _fail("RBWP10_DROPOFF_STATE_INVALID", "$.nativeEvent", repr((request_id, request_state)))
        self.events.append(
            event["timestamp"],
            phase_base,
            "vehicleReachedStop",
            {
                "vehicleId": self.mapper.vehicles.register(vehicle_id),
                "stopId": expected.stop_id,
                "planVersion": route.plan_version,
                "position": self.mapper.position(expected.raw_node),
            },
            dedupe_key=f"reached:{vehicle_id}:{route.plan_version}:{expected.stop_id}",
        )
        passenger_type = "passengerBoarded" if expected_kind == "pickup" else "passengerAlighted"
        self.events.append(
            event["timestamp"],
            phase_base + 1,
            passenger_type,
            {
                "vehicleId": self.mapper.vehicles.register(vehicle_id),
                "requestId": self.mapper.requests.register(request_id),
                "planVersion": route.plan_version,
            },
            dedupe_key=f"{passenger_type}:{self.mapper.requests.register(request_id)}",
        )
        self.routes[vehicle_id] = self._advance_route(route, expected)
        self.request_states[request_id] = "onboard" if expected_kind == "pickup" else "completed"
        self.native_events.append(dict(event))

    @staticmethod
    def _advance_route(route: ProtocolRoute, stop: ProtocolStop) -> ProtocolRoute:
        if route.executed_stop_count < len(route.frozen_prefix):
            if route.frozen_prefix[route.executed_stop_count] != stop:
                raise _fail("RBWP10_ROUTE_PROGRESS_MISMATCH", "$.route", stop.stop_id)
            return ProtocolRoute(
                route.plan_version,
                route.executed_stop_count + 1,
                route.frozen_prefix,
                route.mutable_suffix,
            )
        if not route.mutable_suffix or route.mutable_suffix[0] != stop:
            raise _fail("RBWP10_ROUTE_PROGRESS_MISMATCH", "$.route", stop.stop_id)
        return ProtocolRoute(
            route.plan_version,
            route.executed_stop_count + 1,
            route.frozen_prefix + (stop,),
            route.mutable_suffix[1:],
        )

    def _vehicle_snapshot(self, vehicle_id: Any) -> dict[str, Any]:
        vehicle = self.fleet[vehicle_id]
        cpe = vehicle.stoplist[0]
        onboard = sorted(
            self.mapper.requests.register(request_id)
            for request_id, assigned in self.assignments.items()
            if assigned == vehicle_id and self.request_states[request_id] == "onboard"
        )
        accepted = sorted(
            self.mapper.requests.register(request_id)
            for request_id, assigned in self.assignments.items()
            if assigned == vehicle_id
            and self.request_states[request_id] in {"accepted", "confirmed", "onboard"}
        )
        return {
            "vehicleId": self.mapper.vehicles.register(vehicle_id),
            "capacity": vehicle.seat_capacity,
            "occupiedSeats": cpe.occupancy_after_servicing,
            "position": self.mapper.position(cpe.location),
            "onboardRequestIds": onboard,
            "acceptedRequestIds": accepted,
            "route": self._route_contract(self.routes[vehicle_id]),
        }

    def _route_contract(self, route: ProtocolRoute) -> dict[str, Any]:
        return {
            "planVersion": route.plan_version,
            "executedStopCount": route.executed_stop_count,
            "frozenPrefix": [self._stop_contract(stop) for stop in route.frozen_prefix],
            "mutableSuffix": [self._stop_contract(stop) for stop in route.mutable_suffix],
        }

    def _stop_contract(self, stop: ProtocolStop) -> dict[str, Any]:
        return {
            "stopId": stop.stop_id,
            "nodeId": self.mapper.node_id(stop.raw_node),
            "kind": stop.kind,
            "requestId": (
                None if stop.raw_request is None else self.mapper.requests.register(stop.raw_request)
            ),
            "serviceDurationMs": stop.service_duration_ms,
        }

    def _require_open(self) -> None:
        if self._closed:
            raise _fail("RBWP10_STATE_CLOSED", "$.state", "closed")
