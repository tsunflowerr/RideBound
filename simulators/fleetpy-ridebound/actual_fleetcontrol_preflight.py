#!/usr/bin/env python3
"""Exercise the actual FleetPy FleetControlBase path against the exact Runner."""

from __future__ import annotations

import argparse
import json
import pathlib
import sys
import tempfile


class _Routing:
    def __init__(self) -> None:
        self.travel_multiplier = 1

    @staticmethod
    def get_number_network_nodes() -> int:
        return 3

    def return_travel_costs_1to1(self, origin, destination):
        distance = abs(destination[0] - origin[0])
        travel_time = distance * self.travel_multiplier
        return travel_time, travel_time, distance * 1000

    @staticmethod
    def return_best_route_1to1(origin, destination):
        return [origin[0], destination[0]]

    @staticmethod
    def move_along_route(
        route,
        last_position,
        time_step,
        sim_vid_id=None,
        new_sim_time=None,
        record_node_times=False,
    ):
        del sim_vid_id, record_node_times
        destination = route[0]
        if last_position[1] is None:
            origin = last_position[0]
            progress = 0.0
        else:
            origin = last_position[0]
            destination = last_position[1]
            progress = last_position[2]
        total_time = abs(destination - origin)
        remaining_time = total_time * (1.0 - progress)
        start_time = 0 if new_sim_time is None else new_sim_time
        if time_step >= remaining_time:
            return (
                (destination, None, None),
                remaining_time * 1000,
                start_time + remaining_time,
                [destination],
                [],
            )
        next_progress = progress + time_step / total_time
        return (
            (origin, destination, next_progress),
            time_step * 1000,
            -1,
            [],
            [],
        )

    @staticmethod
    def get_zones_external_route_costs(*_args, **_kwargs):
        return 0, 0, 0

    @staticmethod
    def return_position_str(position):
        return f"{position[0]};{position[1]};{position[2]}"


class _Request:
    def __init__(self, party_size=1) -> None:
        self.nr_pax = party_size
        self.rq_time = 1
        self.o_pos = (1, None, None)
        self.d_pos = (2, None, None)
        self.earliest_start_time = 1
        self.latest_start_time = 3
        self.max_trip_time = 10
        self.pu_time = None
        self.is_parcel = False

    @staticmethod
    def get_rid_struct() -> int:
        return 101

    @staticmethod
    def get_rid() -> int:
        return 101

def _vehicle(routing, request):
    from src.misc.globals import VRL_STATES
    from src.simulation.Vehicles import SimulationVehicle

    vehicle = SimulationVehicle.__new__(SimulationVehicle)
    vehicle.op_id = 0
    vehicle.vid = 0
    vehicle.routing_engine = routing
    vehicle.rq_db = {101: request}
    vehicle.op_output = []
    vehicle.record_route_flag = False
    vehicle.replay_flag = False
    vehicle.status = VRL_STATES.IDLE
    vehicle.pos = (0, None, None)
    vehicle.soc = 1.0
    vehicle.pax = []
    vehicle.assigned_route = []
    vehicle.start_next_leg_first = False
    vehicle.max_pax = 4
    vehicle.max_parcels = 0
    vehicle.soc_per_m = 0.0
    vehicle.battery_size = 1.0
    vehicle.veh_type = "wp7-test-vehicle"
    vehicle.distance_cost = 0.0
    vehicle.cl_start_time = None
    vehicle.cl_start_pos = None
    vehicle.cl_start_soc = None
    vehicle.cl_toll_costs = 0
    vehicle.cl_driven_distance = 0.0
    vehicle.cl_driven_route = []
    vehicle.cl_driven_route_times = []
    vehicle.cl_remaining_route = []
    vehicle.cl_remaining_time = None
    vehicle.cl_locked = False
    vehicle.cumulative_distance = 0.0
    return vehicle


def _forward_vehicle_callbacks(control, vehicle, current_time, next_time, request):
    boarding, alighting, passed, _starting_alight = vehicle.update_veh_state(
        current_time,
        next_time,
    )
    for rid, (boarding_time, _position) in boarding.items():
        request.pu_time = boarding_time
        control.acknowledge_boarding(rid, vehicle.vid, boarding_time)
    for rid, alighting_time in alighting.items():
        control.acknowledge_alighting(rid, vehicle.vid, alighting_time)
    control.receive_status_update(vehicle.vid, next_time, passed, True)
    return {
        "boarding": sorted(boarding),
        "alighting": sorted(alighting),
        "passedStatuses": [leg.status.name for leg in passed],
    }


def _finish_zero_duration_service(control, vehicle, simulation_time, request):
    from src.misc.globals import G_DRIVING_STATUS

    if (
        not vehicle.assigned_route
        or vehicle.status in G_DRIVING_STATUS
        or vehicle.cl_remaining_time != 0
    ):
        raise RuntimeError("expected an exact zero-duration FleetPy service boundary")
    alighting, passed = vehicle.end_current_leg(simulation_time)
    for rid in alighting:
        control.acknowledge_alighting(rid, vehicle.vid, simulation_time)
    control.receive_status_update(vehicle.vid, simulation_time, [passed], True)
    return {
        "boarding": [],
        "alighting": sorted(alighting),
        "passedStatuses": [passed.status.name],
    }


def run(arguments: argparse.Namespace) -> dict:
    adapter_root = pathlib.Path(__file__).parent.resolve()
    repository_root = adapter_root.parents[1]
    fleetpy_root = arguments.fleetpy_root.resolve()
    sys.path.insert(0, str(adapter_root))
    sys.path.insert(0, str(fleetpy_root))

    from src.misc.globals import (
        G_DIR_DATA,
        G_DIR_OUTPUT,
        G_OP_FLEET,
        G_OP_VR_CTRL_F,
        G_SCENARIO_NAME,
        G_SIM_START_TIME,
        G_SKIP_OUTPUT,
    )
    from src.misc.init_modules import load_fleet_control_module

    routing = _Routing()
    request = _Request()
    vehicle = _vehicle(routing, request)
    with tempfile.TemporaryDirectory(prefix="ridebound-wp7-") as temporary:
        temp_path = pathlib.Path(temporary)
        operator = {
            G_OP_VR_CTRL_F: "ridebound-external-runner",
            G_OP_FLEET: "wp7-preflight",
            "ridebound_dotnet_path": str(arguments.dotnet.resolve()),
            "ridebound_runner_root": str(arguments.runner_root.resolve()),
            "ridebound_commitment_config": str(arguments.commitment_config.resolve()),
            "ridebound_wp4_config": str(arguments.wp4_config.resolve()),
            "ridebound_fleetpy_root": str(fleetpy_root),
            "ridebound_repository_root": str(repository_root),
            "ridebound_run_id": f"wp7-fleetcontrol-{arguments.label}",
            "ridebound_scenario_id": "wp7-fleetcontrol-preflight-v1",
            "ridebound_master_seed": 7,
            "ridebound_service_class": "standard",
            "ridebound_commitment_policy_id": "wp6-synthetic-policy-overlay-v1",
            "ridebound_timeout_seconds": 60,
        }
        scenario = {
            "n_cpu_per_sim": 1,
            G_SIM_START_TIME: 0,
            G_SCENARIO_NAME: "wp7-fleetcontrol-preflight-v1",
            G_SKIP_OUTPUT: 1,
        }
        directories = {G_DIR_DATA: str(temp_path), G_DIR_OUTPUT: str(temp_path)}
        control_class = load_fleet_control_module("RideBoundFleetControl")
        control = control_class(
            0,
            operator,
            [vehicle],
            routing,
            None,
            scenario,
            directories,
        )
        try:
            control.user_request(request, 1)
            control.time_trigger(1)
            offer = control.get_current_offer(101)
            if offer is None or offer.service_declined():
                raise RuntimeError("actual FleetControl did not publish a service offer")
            if control._rb_publications:
                raise RuntimeError("provisional offer published a promise")
            if len(vehicle.assigned_route) != 4:
                raise RuntimeError(
                    f"expected four FleetPy VRLs, got {len(vehicle.assigned_route)}"
                )
            if any(leg.locked for leg in vehicle.assigned_route):
                raise RuntimeError("new mutable route was unexpectedly locked")

            control.user_confirms_booking(101, 1)
            first_movement = _forward_vehicle_callbacks(
                control,
                vehicle,
                1,
                2,
                request,
            )
            control.time_trigger(2)
            if len(control._rb_publications) != 1:
                raise RuntimeError("booking did not produce exactly one publication")
            publication = control._rb_publications[0]
            if publication["reasonCode"] != "INITIAL_BOOKING_CONFIRMATION":
                raise RuntimeError(f"unexpected publication reason {publication!r}")
            if first_movement["boarding"] != [101]:
                raise RuntimeError(f"pickup movement did not board: {first_movement!r}")
            control.checkpoint_runner()
            checkpoint_binding_before_restart = control._rb_checkpoint_binding_hash
            restored = control.restart_runner()
            if (
                control._rb_checkpoint_binding_hash
                != checkpoint_binding_before_restart
            ):
                raise RuntimeError("adapter checkpoint binding drifted on restart")

            second_movement = _forward_vehicle_callbacks(
                control,
                vehicle,
                2,
                3,
                request,
            )
            routing.travel_multiplier = 2
            control.inform_network_travel_time_update(3)
            control.time_trigger(3)
            if control._rb_travel_version != 2:
                raise RuntimeError("dynamic travel snapshot did not advance exactly once")
            third_movement = _finish_zero_duration_service(
                control,
                vehicle,
                3,
                request,
            )
            control.time_trigger(3)
            if third_movement["alighting"] != [101]:
                raise RuntimeError(f"drop movement did not alight: {third_movement!r}")
            if vehicle.pax or vehicle.assigned_route:
                raise RuntimeError("vehicle did not finish the physical FleetPy route")
            if control._rb_states[101] != "closed":
                raise RuntimeError("completed request was not finalized after ACK")
            report = {
                "schemaVersion": "1.0.0",
                "status": "pass",
                "label": arguments.label,
                "fleetControlClass": control_class.__name__,
                "offerWaitSeconds": offer.offered_waiting_time,
                "offerDriveSeconds": offer.offered_driving_time,
                "initialAssignedVrlCount": 4,
                "movement": [first_movement, second_movement, third_movement],
                "travelSnapshotVersion": control._rb_travel_version,
                "finalVehiclePosition": vehicle.pos,
                "finalAssignedVrlCount": len(vehicle.assigned_route),
                "requestState": control._rb_states[101],
                "publication": publication,
                "checkpointBindingHash": checkpoint_binding_before_restart,
                "restoredCheckpointHash": restored["payload"]["checkpointHash"],
                "manifestHash": control._rb_session.manifest_hash,
                "artifactCount": len(control._rb_settings.artifact_paths),
            }
        finally:
            control.close()
        receipts = control._rb_session.artifact_receipts
        if receipts["before"] != receipts["after"]:
            raise RuntimeError("artifact receipt drift after FleetControl lifecycle")
        report["artifactReceiptsEqual"] = True
        return report


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--label", required=True)
    parser.add_argument("--fleetpy-root", required=True, type=pathlib.Path)
    parser.add_argument("--runner-root", required=True, type=pathlib.Path)
    parser.add_argument("--dotnet", required=True, type=pathlib.Path)
    parser.add_argument("--commitment-config", required=True, type=pathlib.Path)
    parser.add_argument("--wp4-config", required=True, type=pathlib.Path)
    report = run(parser.parse_args())
    print(json.dumps(report, ensure_ascii=False, sort_keys=True, separators=(",", ":")))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
