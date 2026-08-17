#!/usr/bin/env python3
"""Execute the source-controlled WP7 tiny case through FleetPy's real clock."""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import sys
import tempfile


def _strict_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"duplicate JSON field {key!r}")
        result[key] = value
    return result


def _read_scenario(path):
    with path.open("r", encoding="utf-8", newline="") as source:
        value = json.load(source, object_pairs_hook=_strict_object)
    if not isinstance(value, dict):
        raise RuntimeError("scenario root must be an object")
    return value


def _sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def run(arguments):
    adapter_root = pathlib.Path(__file__).parent.resolve()
    repository_root = adapter_root.parents[1]
    fleetpy_root = arguments.fleetpy_root.resolve()
    scenario_path = arguments.scenario.resolve()
    scenario_root = scenario_path.parent
    sys.path.insert(0, str(adapter_root))
    sys.path.insert(0, str(fleetpy_root))

    from ridebound_fleetpy.mapping import canonical_json_bytes
    from src.BatchOfferSimulation import BatchOfferSimulation
    from src.misc.globals import G_DIR_OUTPUT, get_directory_dict

    class SourceControlledBatchOfferSimulation(BatchOfferSimulation):
        def __init__(self, parameters, fixture_root, output_root):
            self._ridebound_fixture_root = fixture_root
            self._ridebound_output_root = output_root
            self.ridebound_exact_boundary_drains = 0
            super().__init__(parameters)

        def get_directory_dict(self, parameters, operator_values):
            directories = get_directory_dict(
                parameters,
                operator_values,
                abs_fleetpy_dir=str(self._ridebound_fixture_root),
            )
            directories[G_DIR_OUTPUT] = str(self._ridebound_output_root)
            return directories

        def step(self, sim_time):
            super().step(sim_time)
            self._drain_exact_physical_boundaries(sim_time)

        def _drain_exact_physical_boundaries(self, sim_time):
            from src.misc.globals import G_DRIVING_STATUS

            for _microstep in range(64):
                started = any(
                    vehicle.start_next_leg_first
                    for vehicle in self.sim_vehicles.values()
                )
                if started:
                    # Upstream's zero-width update executes start_next_leg and
                    # forwards boarding/start-alighting callbacks without
                    # advancing physical time.
                    self.update_sim_state_fleets(
                        sim_time,
                        sim_time,
                        force_update_plan=True,
                    )
                    for operator in self.operators:
                        operator.time_trigger(sim_time)
                    self.ridebound_exact_boundary_drains += 1

                ended = False
                for (op_id, vid), vehicle in sorted(self.sim_vehicles.items()):
                    if (
                        not vehicle.assigned_route
                        or vehicle.status in G_DRIVING_STATUS
                        or vehicle.cl_remaining_time != 0
                        or vehicle.start_next_leg_first
                    ):
                        continue
                    alighting, finished = vehicle.end_current_leg(sim_time)
                    vehicle.start_next_leg_first = bool(vehicle.assigned_route)
                    for rid in alighting:
                        self.demand.user_ends_alighting(rid, vid, op_id, sim_time)
                        self.broker.acknowledge_user_alighting(
                            op_id,
                            rid,
                            vid,
                            sim_time,
                        )
                    self.broker.receive_status_update(
                        op_id,
                        vid,
                        sim_time,
                        [finished],
                        True,
                    )
                    ended = True
                if ended:
                    for operator in self.operators:
                        operator.time_trigger(sim_time)
                    self.ridebound_exact_boundary_drains += 1
                if not started and not ended:
                    return
            raise RuntimeError("tiny exact physical-boundary drain did not converge")

    base = _read_scenario(scenario_path)
    runtime_values = {
        "op_ridebound_dotnet_path": str(arguments.dotnet.resolve()),
        "op_ridebound_runner_root": str(arguments.runner_root.resolve()),
        "op_ridebound_commitment_config": str(
            arguments.commitment_config.resolve()
        ),
        "op_ridebound_wp4_config": str(arguments.wp4_config.resolve()),
        "op_ridebound_fleetpy_root": str(fleetpy_root),
        "op_ridebound_repository_root": str(repository_root),
        "op_ridebound_scenario_root": str(scenario_root),
        "op_ridebound_run_id": f"wp7-clock-{arguments.label}",
    }
    semantic_runs = []
    for repeat in range(2):
        parameters = dict(base)
        parameters.update(runtime_values)
        with tempfile.TemporaryDirectory(prefix="ridebound-wp7-clock-") as output:
            simulation = SourceControlledBatchOfferSimulation(
                parameters,
                scenario_root,
                pathlib.Path(output),
            )
            operator = simulation.operators[0]
            try:
                simulation.run()
                # FleetPy's record_remaining_assignments performs one final
                # status callback even for an already drained vehicle, but it
                # does not call the fleet-control timer. Reconcile that exact
                # end_time callback before asserting/checkpointing the adapter.
                operator.time_trigger(simulation.end_time)
                vehicle = simulation.sim_vehicles[(0, 0)]
                if operator._rb_states.get(101) != "closed":
                    raise RuntimeError(
                        f"repeat {repeat}: adapter request did not close"
                    )
                if simulation.demand.rq_db:
                    raise RuntimeError(
                        f"repeat {repeat}: FleetPy demand retained a request"
                    )
                if vehicle.pax or vehicle.assigned_route:
                    raise RuntimeError(
                        f"repeat {repeat}: FleetPy vehicle did not finish"
                    )
                route = operator._rb_routes[0]
                if route.remaining_stops:
                    raise RuntimeError(
                        f"repeat {repeat}: protocol route did not finish"
                    )
                if len(operator._rb_publications) != 1:
                    raise RuntimeError(
                        f"repeat {repeat}: expected one initial publication"
                    )
                publication = operator._rb_publications[0]
                if publication["reasonCode"] != "INITIAL_BOOKING_CONFIRMATION":
                    raise RuntimeError(
                        f"repeat {repeat}: wrong publication reason"
                    )
                if operator._rb_travel_version != 3:
                    raise RuntimeError(
                        f"repeat {repeat}: expected three travel snapshots"
                    )
                operator.checkpoint_runner()
                semantic = {
                    "manifestHash": operator._rb_session.manifest_hash,
                    "publication": publication,
                    "checkpointBindingHash": operator._rb_checkpoint_binding_hash,
                    "travelSnapshotVersion": operator._rb_travel_version,
                    "nextEventSeq": operator._rb_events.next_event_sequence,
                    "nextEpoch": operator._rb_events.next_epoch,
                    "vehiclePosition": list(vehicle.pos),
                    "requestState": operator._rb_states[101],
                    "exactPhysicalBoundaryDrainCount":
                        simulation.ridebound_exact_boundary_drains,
                }
                semantic_hash = hashlib.sha256(
                    b"RideBound.Wp7ActualFleetPyTiny.v1\0"
                    + canonical_json_bytes(semantic)
                ).hexdigest()
            finally:
                operator.close()
            receipts = operator._rb_session.artifact_receipts
            if receipts["before"] != receipts["after"]:
                raise RuntimeError(f"repeat {repeat}: artifact receipt drift")
            semantic_runs.append(
                {
                    "repeat": repeat,
                    "semanticHash": semantic_hash,
                    "semantic": semantic,
                    "artifactCount": len(receipts["before"]),
                    "artifactReceiptsEqual": True,
                }
            )
    if semantic_runs[0]["semanticHash"] != semantic_runs[1]["semanticHash"]:
        raise RuntimeError("two clean FleetPy clocks were not semantically exact")
    return {
        "schemaVersion": "1.0.0",
        "status": "pass",
        "label": arguments.label,
        "fleetpyClockClass": SourceControlledBatchOfferSimulation.__mro__[1].__name__,
        "scenarioSha256": _sha256(scenario_path),
        "semanticHash": semantic_runs[0]["semanticHash"],
        "repeatCount": len(semantic_runs),
        "runs": semantic_runs,
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--label", required=True)
    parser.add_argument("--fleetpy-root", required=True, type=pathlib.Path)
    parser.add_argument("--runner-root", required=True, type=pathlib.Path)
    parser.add_argument("--dotnet", required=True, type=pathlib.Path)
    parser.add_argument("--commitment-config", required=True, type=pathlib.Path)
    parser.add_argument("--wp4-config", required=True, type=pathlib.Path)
    parser.add_argument("--scenario", required=True, type=pathlib.Path)
    result = run(parser.parse_args())
    print(
        json.dumps(
            result,
            ensure_ascii=False,
            sort_keys=True,
            separators=(",", ":"),
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
