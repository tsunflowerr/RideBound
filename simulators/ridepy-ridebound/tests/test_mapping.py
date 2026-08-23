from __future__ import annotations

import math
import pathlib
import sys
import unittest
from dataclasses import dataclass


ROOT = pathlib.Path(__file__).parents[1]
FLEETPY_ADAPTER = ROOT.parent / "fleetpy-ridebound"
sys.path[:0] = [str(ROOT), str(FLEETPY_ADAPTER)]

from ridebound_fleetpy.errors import AdapterFailure  # noqa: E402
from ridebound_ridepy.mapping import RidePyProtocolMapper, _milliseconds  # noqa: E402


@dataclass
class Request:
    request_id: int | str
    creation_timestamp: float
    origin: int
    destination: int
    pickup_timewindow_min: float
    pickup_timewindow_max: float
    delivery_timewindow_min: float
    delivery_timewindow_max: float


class Space:
    vertices = [2, 0, 1]

    def t(self, origin, destination):
        return abs(origin - destination) + 0.25


class MappingTests(unittest.TestCase):
    def test_time_rounding_is_ties_to_even(self) -> None:
        self.assertEqual(0, _milliseconds(0.0005, "$.time"))
        self.assertEqual(2, _milliseconds(0.0015, "$.time"))

    def test_identity_is_reversible_and_type_sensitive(self) -> None:
        mapper = RidePyProtocolMapper()
        integer = mapper.requests.register(7)
        text = mapper.requests.register("7")
        self.assertNotEqual(integer, text)
        self.assertEqual(7, mapper.raw_request(integer))
        self.assertEqual("7", mapper.raw_request(text))

    def test_request_is_unit_party_and_finite(self) -> None:
        mapper = RidePyProtocolMapper()
        request = Request(1, 1.0, 0, 2, 2.0, 5.0, 4.0, 12.0)
        mapped = mapper.request(request, "standard", "policy")
        self.assertEqual(1, mapped["partySize"])
        self.assertEqual(10_000, mapped["maxRideTimeMs"])
        self.assertEqual(1, mapper.raw_request(mapped["requestId"]))

    def test_request_time_or_identity_mutation_fails(self) -> None:
        mapper = RidePyProtocolMapper()
        invalid_time = Request(1, 6.0, 0, 2, 2.0, 5.0, 4.0, 12.0)
        with self.assertRaisesRegex(AdapterFailure, "RBWP10_REQUEST_TIME_ORDER_INVALID"):
            mapper.request(invalid_time, "standard", "policy")
        invalid_id = Request(True, 1.0, 0, 2, 2.0, 5.0, 4.0, 12.0)
        with self.assertRaisesRegex(AdapterFailure, "RBWP10_REQUEST_ID_TYPE_INVALID"):
            mapper.request(invalid_id, "standard", "policy")

    def test_travel_snapshot_is_complete_directed_and_stable(self) -> None:
        mapper = RidePyProtocolMapper()
        first = mapper.travel_snapshot(1, Space())
        second = mapper.travel_snapshot(1, Space())
        self.assertEqual(6, len(first["arcs"]))
        self.assertEqual(first, second)
        self.assertTrue(all(arc["travelTimeMs"] > 0 for arc in first["arcs"]))

    def test_disconnected_graph_mutation_fails(self) -> None:
        class Disconnected(Space):
            def t(self, origin, destination):
                return math.inf

        with self.assertRaisesRegex(AdapterFailure, "RBWP10_TRAVEL_GRAPH_DISCONNECTED"):
            RidePyProtocolMapper().travel_snapshot(1, Disconnected())

    def test_node_and_vehicle_types_are_fail_closed(self) -> None:
        mapper = RidePyProtocolMapper()
        with self.assertRaisesRegex(AdapterFailure, "RBWP10_NODE_ID_TYPE_INVALID"):
            mapper.position("node")
        with self.assertRaisesRegex(AdapterFailure, "RBWP10_VEHICLE_ID_TYPE_INVALID"):
            mapper.register_vehicles([False])


if __name__ == "__main__":
    unittest.main()
