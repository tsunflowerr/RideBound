#!/usr/bin/env python3
"""Run cancellation/rejection/travel failure branches on actual FleetPy + Runner."""

from __future__ import annotations

import argparse
import json
import pathlib
import sys
import tempfile

from actual_fleetcontrol_preflight import _Request, _Routing, _vehicle


def _operator(arguments, run_id):
    from src.misc.globals import G_OP_FLEET, G_OP_VR_CTRL_F

    return {
        G_OP_VR_CTRL_F: "ridebound-external-runner",
        G_OP_FLEET: "wp7-preflight",
        "ridebound_dotnet_path": str(arguments.dotnet.resolve()),
        "ridebound_runner_root": str(arguments.runner_root.resolve()),
        "ridebound_commitment_config": str(
            arguments.commitment_config.resolve()
        ),
        "ridebound_wp4_config": str(arguments.wp4_config.resolve()),
        "ridebound_fleetpy_root": str(arguments.fleetpy_root.resolve()),
        "ridebound_repository_root": str(arguments.repository_root.resolve()),
        "ridebound_run_id": run_id,
        "ridebound_scenario_id": "wp7-fleetcontrol-lifecycle-matrix-v1",
        "ridebound_master_seed": 7,
        "ridebound_service_class": "standard",
        "ridebound_commitment_policy_id": "wp6-synthetic-policy-overlay-v1",
        "ridebound_timeout_seconds": 60,
    }


def _run_case(arguments, case_name, exercise, party_size=1):
    from src.misc.globals import (
        G_DIR_DATA,
        G_DIR_OUTPUT,
        G_SCENARIO_NAME,
        G_SIM_START_TIME,
        G_SKIP_OUTPUT,
    )
    from src.misc.init_modules import load_fleet_control_module

    routing = _Routing()
    request = _Request(party_size)
    vehicle = _vehicle(routing, request)
    with tempfile.TemporaryDirectory(prefix=f"ridebound-wp7-{case_name}-") as temp:
        scenario = {
            "n_cpu_per_sim": 1,
            G_SIM_START_TIME: 0,
            G_SCENARIO_NAME: "wp7-fleetcontrol-lifecycle-matrix-v1",
            G_SKIP_OUTPUT: 1,
        }
        directories = {G_DIR_DATA: temp, G_DIR_OUTPUT: temp}
        control_class = load_fleet_control_module("RideBoundFleetControl")
        control = control_class(
            0,
            _operator(arguments, f"wp7-{arguments.label}-{case_name}"),
            [vehicle],
            routing,
            None,
            scenario,
            directories,
        )
        try:
            evidence = exercise(control, request, vehicle, routing)
        finally:
            control.close()
        receipts = control._rb_session.artifact_receipts
        if receipts["before"] != receipts["after"]:
            raise RuntimeError(f"{case_name}: artifact receipt drift")
        evidence["artifactReceiptsEqual"] = True
        return evidence


def _assert_empty(case_name, control, vehicle):
    route = control._rb_routes[vehicle.vid]
    if vehicle.assigned_route or vehicle.pax or route.remaining_stops:
        raise RuntimeError(
            f"{case_name}: physical/protocol route was not empty: "
            f"legs={len(vehicle.assigned_route)}; pax={len(vehicle.pax)}; "
            f"protocol={route!r}"
        )
    if control.rid_to_assigned_vid:
        raise RuntimeError(f"{case_name}: request-to-vehicle index leaked")


def _pending_cancel(control, request, vehicle, _routing):
    control.user_request(request, 1)
    control.user_cancels_request(101, 1)
    control.time_trigger(1)
    if control._rb_states[101] != "closed" or control._rb_publications:
        raise RuntimeError("pending-cancel: lifecycle/publication mismatch")
    _assert_empty("pending-cancel", control, vehicle)
    return {"state": "closed", "publicationCount": 0}


def _capacity_reject(control, request, vehicle, _routing):
    control.user_request(request, 1)
    control.time_trigger(1)
    offer = control.get_current_offer(101)
    if offer is None or not offer.service_declined():
        raise RuntimeError("capacity-reject: request was not rejected")
    if control._rb_states[101] != "rejected" or control._rb_publications:
        raise RuntimeError("capacity-reject: lifecycle/publication mismatch")
    control.user_cancels_request(101, 2)
    if control._rb_states[101] != "closed":
        raise RuntimeError("capacity-reject: rejected request was not finalized")
    _assert_empty("capacity-reject", control, vehicle)
    return {"state": "closed", "publicationCount": 0}


def _offer_decline(control, request, vehicle, _routing):
    control.user_request(request, 1)
    control.time_trigger(1)
    offer = control.get_current_offer(101)
    if offer is None or offer.service_declined():
        raise RuntimeError("offer-decline: provisional offer missing")
    if control._rb_publications:
        raise RuntimeError("offer-decline: provisional offer published promise")
    control.user_cancels_request(101, 2)
    control.time_trigger(2)
    if control._rb_states[101] != "closed" or control._rb_publications:
        raise RuntimeError("offer-decline: lifecycle/publication mismatch")
    _assert_empty("offer-decline", control, vehicle)
    return {"state": "closed", "publicationCount": 0}


def _confirmed_cancel(control, request, vehicle, _routing):
    control.user_request(request, 1)
    control.time_trigger(1)
    control.user_confirms_booking(101, 1)
    control.time_trigger(2)
    if len(control._rb_publications) != 1:
        raise RuntimeError("confirmed-cancel: missing initial booking promise")
    control.user_cancels_request(101, 3)
    control.time_trigger(3)
    if control._rb_states[101] != "closed":
        raise RuntimeError("confirmed-cancel: request did not close")
    _assert_empty("confirmed-cancel", control, vehicle)
    return {
        "state": "closed",
        "publicationCount": len(control._rb_publications),
        "initialReason": control._rb_publications[0]["reasonCode"],
    }


def _dynamic_travel(control, request, vehicle, routing):
    control.user_request(request, 1)
    control.time_trigger(1)
    control.checkpoint_runner()
    initial_binding = control._rb_checkpoint_binding_hash
    routing.travel_multiplier = 0.5
    control.inform_network_travel_time_update(2)
    control.time_trigger(2)
    control.checkpoint_runner()
    if control._rb_travel_version != 2:
        raise RuntimeError("dynamic-travel: version did not advance to two")
    if control._rb_checkpoint_binding_hash == initial_binding:
        raise RuntimeError("dynamic-travel: checkpoint binding did not change")
    control.user_cancels_request(101, 3)
    control.time_trigger(3)
    _assert_empty("dynamic-travel", control, vehicle)
    return {
        "state": control._rb_states[101],
        "travelSnapshotVersion": control._rb_travel_version,
    }


def _unsafe_restart(control, request, _vehicle_value, _routing):
    from ridebound_fleetpy.errors import AdapterFailure

    control.user_request(request, 1)
    try:
        control.restart_runner()
    except AdapterFailure as failure:
        if failure.code != "RBWP7_RESTART_UNSAFE":
            raise
        return {"failureCode": failure.code}
    raise RuntimeError("unsafe-restart: restart unexpectedly succeeded")


def run(arguments):
    adapter_root = pathlib.Path(__file__).parent.resolve()
    sys.path.insert(0, str(adapter_root))
    sys.path.insert(0, str(arguments.fleetpy_root.resolve()))
    arguments.repository_root = adapter_root.parents[1]
    cases = {
        "pendingCancel": _run_case(
            arguments, "pending-cancel", _pending_cancel
        ),
        "capacityReject": _run_case(
            arguments, "capacity-reject", _capacity_reject, party_size=5
        ),
        "offerDecline": _run_case(
            arguments, "offer-decline", _offer_decline
        ),
        "confirmedCancel": _run_case(
            arguments, "confirmed-cancel", _confirmed_cancel
        ),
        "dynamicTravel": _run_case(
            arguments, "dynamic-travel", _dynamic_travel
        ),
        "unsafeRestart": _run_case(
            arguments, "unsafe-restart", _unsafe_restart
        ),
    }
    return {
        "schemaVersion": "1.0.0",
        "status": "pass",
        "label": arguments.label,
        "caseCount": len(cases),
        "cases": cases,
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--label", required=True)
    parser.add_argument("--fleetpy-root", required=True, type=pathlib.Path)
    parser.add_argument("--runner-root", required=True, type=pathlib.Path)
    parser.add_argument("--dotnet", required=True, type=pathlib.Path)
    parser.add_argument("--commitment-config", required=True, type=pathlib.Path)
    parser.add_argument("--wp4-config", required=True, type=pathlib.Path)
    print(
        json.dumps(
            run(parser.parse_args()),
            ensure_ascii=False,
            sort_keys=True,
            separators=(",", ":"),
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
