from __future__ import annotations

import pathlib
import sys
import unittest
from types import SimpleNamespace


ROOT = pathlib.Path(__file__).parents[1]
FLEETPY_ADAPTER = ROOT.parent / "fleetpy-ridebound"
sys.path[:0] = [str(ROOT), str(FLEETPY_ADAPTER)]

try:
    from ridepy.data_structures import TransportationRequest
    from ridepy.util.spaces import Graph

    from ridebound_fleetpy.errors import AdapterFailure
    from ridebound_fleetpy.protocol import (
        AcceptedAction,
        ParsedDecision,
        PlanAction,
        ProtocolRoute,
        ProtocolStop,
    )
    from ridebound_ridepy.fleet_state import CommitFleetState

    RIDEPY_AVAILABLE = True
except ModuleNotFoundError:
    RIDEPY_AVAILABLE = False


@unittest.skipUnless(RIDEPY_AVAILABLE, "RidePy wheel is exercised in the pinned Linux image")
class CommitFleetStateTests(unittest.TestCase):
    def setUp(self) -> None:
        self.space = Graph(
            vertices=[0, 1, 2],
            edges=[(0, 1), (1, 2), (0, 2)],
            weights=[1.0, 1.0, 3.0],
            velocity=1,
        )
        self.request = TransportationRequest(
            request_id=1,
            creation_timestamp=0,
            origin=1,
            destination=2,
            pickup_timewindow_min=0,
            pickup_timewindow_max=10,
            delivery_timewindow_min=0,
            delivery_timewindow_max=20,
        )
        self.request_two = TransportationRequest(
            request_id=2,
            creation_timestamp=0,
            origin=2,
            destination=0,
            pickup_timewindow_min=0,
            pickup_timewindow_max=10,
            delivery_timewindow_min=0,
            delivery_timewindow_max=20,
        )
        settings = SimpleNamespace(
            service_class="standard",
            commitment_policy_id="policy",
            run_id="run",
            scenario_id="scenario",
        )
        self.state = CommitFleetState(
            initial_locations={0: 0, 1: 2},
            space=self.space,
            seat_capacities=2,
            session=SimpleNamespace(settings=settings),
            declared_requests=[self.request, self.request_two],
        )
        self.request_id = self.state.mapper.requests.register(1)

    def route(self, *, service_duration_ms=0, raw_request=1) -> ProtocolRoute:
        return ProtocolRoute(
            1,
            0,
            (),
            (
                ProtocolStop("pickup", 1, "pickup", raw_request, service_duration_ms),
                ProtocolStop("drop", 2, "dropOff", raw_request, 0),
            ),
        )

    def test_native_stoplist_uses_graph_time_windows_and_capacity(self) -> None:
        stoplist = self.state._prepare_stoplist(0, self.route())
        self.assertEqual([0, 1, 2], [stop.location for stop in stoplist])
        self.assertEqual([0, 1, 0], [stop.occupancy_after_servicing for stop in stoplist])
        self.assertEqual(1, stoplist[1].estimated_arrival_time)
        self.assertEqual(2, stoplist[2].estimated_arrival_time)

    def test_unrepresentable_service_duration_fails(self) -> None:
        with self.assertRaisesRegex(AdapterFailure, "RBWP10_SERVICE_DURATION_UNSUPPORTED"):
            self.state._prepare_stoplist(0, self.route(service_duration_ms=1))

    def test_multi_vehicle_validation_is_atomic(self) -> None:
        original = {vehicle_id: vehicle.stoplist for vehicle_id, vehicle in self.state.fleet.items()}
        valid = PlanAction(
            self.state.mapper.vehicles.register(0),
            0,
            "candidate-0",
            self.route(),
        )
        invalid = PlanAction(
            self.state.mapper.vehicles.register(1),
            1,
            "candidate-1",
            self.route(raw_request=999),
        )
        parsed = ParsedDecision((), (), (valid, invalid), ())
        with self.assertRaisesRegex(AdapterFailure, "RBWP10_PLAN_REQUEST_UNKNOWN"):
            self.state._apply_decision(parsed)
        for vehicle_id, stoplist in original.items():
            self.assertIs(stoplist, self.state.fleet[vehicle_id].stoplist)
            self.assertEqual(0, self.state.routes[vehicle_id].plan_version)

    def test_native_pickup_and_drop_reconcile_exactly_once(self) -> None:
        self.state.request_states[1] = "pending"
        plan = PlanAction(
            self.state.mapper.vehicles.register(0),
            0,
            "candidate-0",
            self.route(),
        )
        accepted = AcceptedAction(
            self.request_id,
            1,
            self.state.mapper.vehicles.register(0),
            0,
            "candidate-0",
        )
        self.state._apply_decision(ParsedDecision((accepted,), (), (plan,), ()))
        self.state.request_states[1] = "confirmed"

        pickup = self.state.fleet[0].fast_forward_time(1)[0]
        self.state._record_native_stop(pickup)
        drop = self.state.fleet[0].fast_forward_time(2)[0]
        self.state._record_native_stop(drop)
        self.assertEqual("completed", self.state.request_states[1])
        self.assertEqual(["PickupEvent", "DeliveryEvent"], [e["event_type"] for e in self.state.native_events])
        with self.assertRaisesRegex(AdapterFailure, "RBWP10_LIFECYCLE_WITHOUT_ROUTE"):
            self.state._record_native_stop(drop)

    def test_same_timestamp_stops_preserve_native_intra_vehicle_order(self) -> None:
        route = ProtocolRoute(
            1,
            0,
            (),
            (
                ProtocolStop("p1", 1, "pickup", 1, 0),
                ProtocolStop("d1", 2, "dropOff", 1, 0),
                ProtocolStop("p2", 2, "pickup", 2, 0),
                ProtocolStop("d2", 0, "dropOff", 2, 0),
            ),
        )
        self.state.request_states[1] = "pending"
        self.state.request_states[2] = "pending"
        vehicle_id = self.state.mapper.vehicles.register(0)
        accepted = (
            AcceptedAction(self.state.mapper.requests.register(1), 1, vehicle_id, 0, "candidate"),
            AcceptedAction(self.state.mapper.requests.register(2), 2, vehicle_id, 0, "candidate"),
        )
        self.state._apply_decision(
            ParsedDecision(accepted, (), (PlanAction(vehicle_id, 0, "candidate", route),), ())
        )
        self.state.request_states[1] = "confirmed"
        self.state.request_states[2] = "confirmed"
        self.state._started = True
        batches = []
        self.state._flush = lambda event_time: batches.append(
            self.state.events.drain(
                "run",
                "scenario",
                event_time,
                append_timer_tick=False,
            )
        )
        events = self.state.fast_forward(20)
        self.assertEqual(
            [("PickupEvent", 1), ("DeliveryEvent", 1), ("PickupEvent", 2), ("DeliveryEvent", 2)],
            [(event["event_type"], event["request_id"]) for event in events],
        )
        simultaneous = batches[1]["payload"]["events"]
        self.assertEqual(
            [
                "vehicleReachedStop",
                "passengerAlighted",
                "vehicleReachedStop",
                "passengerBoarded",
            ],
            [event["eventType"] for event in simultaneous],
        )


if __name__ == "__main__":
    unittest.main()
