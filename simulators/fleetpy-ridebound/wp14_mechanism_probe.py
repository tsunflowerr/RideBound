#!/usr/bin/env python3
"""Measure, from frozen E1 v1.2 bundles only, where the C1 mechanism spends
service and how much of the lexicographic objective actually decides anything.

The probe answers four separable questions for one arm of one panel.

1. Gate attribution. Which commitment layer and which dimension removed a
   candidate, and how often that left a request unreachable in the same
   decision. A request is counted as immediately blocked only when the
   commitment gate removed an actionful option that could serve it and no
   eligible actionful option for it remained anywhere in the fleet.
2. Objective informativeness. For every lexicographic level the Runner built,
   whether the level could discriminate at all inside the option set the solver
   received. Sum and maximum aggregation over an exactly-one-per-vehicle
   selection are both constant when every vehicle's eligible options share one
   contribution, so such a level proves the optimum of a constant.
3. Promise movement. Attributed decision-induced burden, exactly as the WP8/WP9
   gate metric defines it, next to the movement a rider actually sees between
   consecutive published promises regardless of cause.
4. Consumption shape. The distribution of cumulative recorded consumption per
   request, which shows whether a scalar budget level is a usable knob.

Read-only. Raw roots are never written and the output is exclusive-create.

The result is descriptive diagnosis of an already-terminal frozen panel. It is
not a causal decomposition, not a counterfactual completion estimate, and must
never be used to select a configuration for any future confirmatory run.
"""

from __future__ import annotations

import argparse
import base64
import collections
import hashlib
import json
import os
import pathlib
import sys

sys.dont_write_bytecode = True

SCHEMA_ID = "https://ridebound.local/schemas/wp14/v1/mechanism-probe.schema.json"
PROBE_VERSION = "1.0.0"
COMMITMENT_CODES = ("COMMITMENT_BUDGET_EXCEEDED", "COMMITMENT_PHASE_LOCK")
BURDEN_DIMENSIONS = ("pickupEtaTotalMs", "dropEtaTotalMs")
CONSUMPTION_THRESHOLDS_MS = (0, 30_000, 60_000, 120_000, 300_000)


class ProbeError(RuntimeError):
    """A fail-closed probe condition."""


def decisions_in(transcript):
    """Yield every decision payload in one recorded transcript, in order."""
    with transcript.open("r", encoding="utf-8") as handle:
        for ordinal, line in enumerate(handle, start=1):
            line = line.strip()
            if not line:
                continue
            record = json.loads(line)
            if record.get("direction") != "runnerToAdapter":
                continue
            frame = json.loads(base64.b64decode(record["frameBase64"]))
            if frame.get("messageType") != "decision":
                continue
            payload = frame.get("payload")
            if not isinstance(payload, dict):
                raise ProbeError(
                    f"{transcript}: decision {ordinal} carries no payload object"
                )
            yield payload


def degenerate_levels(levels, eligible_by_vehicle):
    """Return the indices of levels that cannot discriminate in this decision."""
    found = []
    for index in range(len(levels)):
        constant = True
        for options in eligible_by_vehicle.values():
            distinct = {
                option[index] for option in options if index < len(option)
            }
            if len(distinct) > 1:
                constant = False
                break
        if constant:
            found.append(index)
    return found


def percentiles(values):
    ordered = sorted(values)
    count = len(ordered)
    if count == 0:
        return {}
    result = {}
    for quantile in (0.5, 0.75, 0.9, 0.95, 0.99):
        index = min(count - 1, max(0, int(quantile * count) - 1))
        result[f"p{int(quantile * 100)}"] = ordered[index]
    result["p100"] = ordered[-1]
    return result


def analyze(transcripts):
    """Measure one arm of one panel from its exact recorded transcripts."""
    if not transcripts:
        raise ProbeError("at least one transcript is required")

    decisions = 0
    candidate_observations = 0
    commitment_pruned = collections.Counter()
    witness_dimensions = collections.Counter()
    immediate_blocks = 0
    blocks_by_dimension = collections.Counter()
    vehicle_sets = 0
    vehicle_sets_gate_emptied = 0
    levels_built = collections.Counter()
    levels_degenerate = collections.Counter()
    attributed = collections.Counter()
    exogenous = collections.Counter()
    experienced = collections.Counter()
    previous_promise = {}
    consumption = {}
    promises_opened = set()
    promises_moved = set()
    promises_charged = set()

    for transcript in transcripts:
        if not transcript.is_file():
            raise ProbeError(f"transcript not found: {transcript}")
        tag = transcript.parent.name
        for payload in decisions_in(transcript):
            evidence = (payload.get("solver") or {}).get("executionEvidence") or {}
            portfolio = evidence.get("candidatePortfolio")
            if portfolio is not None:
                decisions += 1
                pruned = {}
                for entry in evidence.get("prunedCandidates") or []:
                    pruned[entry["candidateId"]] = entry
                    if entry["code"] not in COMMITMENT_CODES:
                        continue
                    for witness in entry.get("commitmentWitnesses") or []:
                        dimension = witness.get("dimension")
                        rule = witness.get("rule") or "-"
                        witness_dimensions[
                            f"{entry['code']}:{dimension}:{rule}"
                        ] += 1

                levels = sorted(
                    portfolio["selectionProblem"]["objectiveLevels"],
                    key=lambda level: level["levelIndex"],
                )
                eligible_by_vehicle = collections.defaultdict(list)
                all_by_vehicle = collections.defaultdict(list)
                reachable = set()
                blocked = {}
                for candidate in portfolio["candidates"]:
                    candidate_observations += 1
                    all_by_vehicle[candidate["vehicleId"]].append(candidate)
                    if candidate.get("policyEligibility") == "eligible":
                        eligible_by_vehicle[candidate["vehicleId"]].append(
                            candidate["objectiveContributions"]
                        )
                        if not candidate["isNoOp"]:
                            reachable.update(candidate["newRequestIds"])
                        continue
                    entry = pruned.get(candidate["candidateId"])
                    if entry is None or entry["code"] not in COMMITMENT_CODES:
                        continue
                    commitment_pruned[entry["code"]] += 1
                    if candidate["isNoOp"]:
                        continue
                    for request in candidate["newRequestIds"]:
                        blocked.setdefault(request, []).append(entry)

                for candidates in all_by_vehicle.values():
                    vehicle_sets += 1
                    eligible_actionful = any(
                        candidate.get("policyEligibility") == "eligible"
                        and not candidate["isNoOp"]
                        for candidate in candidates
                    )
                    gate_removed = any(
                        candidate.get("policyEligibility") != "eligible"
                        and not candidate["isNoOp"]
                        and (pruned.get(candidate["candidateId"]) or {}).get("code")
                        in COMMITMENT_CODES
                        for candidate in candidates
                    )
                    if not eligible_actionful and gate_removed:
                        vehicle_sets_gate_emptied += 1

                for request, entries in blocked.items():
                    if request in reachable:
                        continue
                    immediate_blocks += 1
                    dimensions = sorted(
                        {
                            witness["dimension"]
                            for entry in entries
                            for witness in entry.get("commitmentWitnesses") or []
                            if witness.get("dimension")
                        }
                    )
                    key = "+".join(dimensions) or "(unrecorded)"
                    blocks_by_dimension[key] += 1

                for level in levels:
                    levels_built[level["name"].split(":")[0]] += 1
                for index in degenerate_levels(levels, eligible_by_vehicle):
                    levels_degenerate[levels[index]["name"].split(":")[0]] += 1

            for action in payload.get("actions") or []:
                if action["decisionType"] != "promisePublished":
                    continue
                body = action["payload"]
                key = (tag, body["promise"]["requestId"])
                promises_opened.add(key)
                for dimension in BURDEN_DIMENSIONS:
                    attributed[dimension] += body["decisionDelta"][dimension]
                    exogenous[dimension] += body["exogenousDelta"][dimension]
                if any(body["decisionDelta"][name] for name in body["decisionDelta"]):
                    promises_charged.add(key)
                pickup = body["promise"]["pickupEtaMs"]
                drop = body["promise"]["dropEtaMs"]
                if key in previous_promise:
                    before_pickup, before_drop = previous_promise[key]
                    moved_pickup = abs(pickup - before_pickup)
                    moved_drop = abs(drop - before_drop)
                    experienced["pickupEtaTotalMs"] += moved_pickup
                    experienced["dropEtaTotalMs"] += moved_drop
                    if moved_pickup or moved_drop:
                        promises_moved.add(key)
                previous_promise[key] = (pickup, drop)
                consumption[key] = max(
                    consumption.get(key, 0), body["budgetAfter"]["dropEtaTotalMs"]
                )

    if decisions == 0:
        raise ProbeError("no decision carried v1.2 retained-portfolio evidence")

    values = list(consumption.values())
    built = sum(levels_built.values())
    degenerate = sum(levels_degenerate.values())
    return {
        "schemaId": SCHEMA_ID,
        "probeVersion": PROBE_VERSION,
        "claimBoundary": [
            "descriptiveFinitePanelNotCausal",
            "notACounterfactualCompletionEstimate",
            "mustNotSelectAnyFutureConfiguration",
        ],
        "bundles": [transcript.parent.name for transcript in transcripts],
        "decisions": decisions,
        "gate": {
            "candidateObservations": candidate_observations,
            "commitmentPrunedByCode": dict(sorted(commitment_pruned.items())),
            "commitmentWitnessDimensions": dict(sorted(witness_dimensions.items())),
            "vehicleChoiceSets": vehicle_sets,
            "vehicleChoiceSetsEmptiedByGate": vehicle_sets_gate_emptied,
            "immediateAcceptanceBlocks": immediate_blocks,
            "immediateBlocksByDimension": dict(sorted(blocks_by_dimension.items())),
        },
        "objective": {
            "lexicographicLevelsBuilt": built,
            "lexicographicLevelsDegenerate": degenerate,
            "degenerateFraction": (
                None if built == 0 else round(degenerate / built, 6)
            ),
            "byFamily": {
                family: {
                    "built": levels_built[family],
                    "degenerate": levels_degenerate[family],
                }
                for family in sorted(levels_built)
            },
        },
        "promiseMovement": {
            "requestsWithOpenPromise": len(promises_opened),
            "attributedDecisionInducedMs": dict(sorted(attributed.items())),
            "attributedTotalMs": sum(attributed.values()),
            "exogenousMs": dict(sorted(exogenous.items())),
            "experiencedPublishedToPublishedMs": dict(sorted(experienced.items())),
            "experiencedTotalMs": sum(experienced.values()),
            "requestsWhosePromiseEverMoved": len(promises_moved),
            "requestsChargedAnyDecisionInducedBurden": len(promises_charged),
        },
        "consumptionShape": {
            "dimension": "dropEtaTotalMs",
            "requests": len(values),
            "zeroConsumption": sum(1 for value in values if value == 0),
            "percentilesMs": percentiles(values),
            "requestsAboveThresholdMs": {
                str(threshold): sum(1 for value in values if value > threshold)
                for threshold in CONSUMPTION_THRESHOLDS_MS
            },
        },
    }


def canonical(document):
    encoded = json.dumps(
        document, ensure_ascii=False, sort_keys=True, separators=(",", ":")
    )
    return encoded.encode("utf-8")


def require_output_outside_inputs(output, transcripts):
    resolved = output.resolve()
    for transcript in transcripts:
        root = transcript.resolve().parent.parent
        if resolved == transcript.resolve() or root in resolved.parents:
            raise ProbeError(f"output must stay outside the raw root {root}")


def write_exclusive(output, payload):
    output.parent.mkdir(parents=True, exist_ok=True)
    flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL
    flags |= getattr(os, "O_BINARY", 0)
    descriptor = os.open(output, flags)
    try:
        os.write(descriptor, payload)
    finally:
        os.close(descriptor)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--transcript", action="append", required=True, type=pathlib.Path
    )
    parser.add_argument("--output", type=pathlib.Path)
    arguments = parser.parse_args(argv)
    try:
        transcripts = list(arguments.transcript)
        if arguments.output is not None:
            require_output_outside_inputs(arguments.output, transcripts)
        encoded = canonical(analyze(transcripts)) + b"\n"
        if arguments.output is None:
            sys.stdout.buffer.write(encoded)
        else:
            write_exclusive(arguments.output, encoded)
            digest = hashlib.sha256(encoded).hexdigest()
            print(f"{arguments.output} {len(encoded)} {digest}")
    except (OSError, ProbeError, ValueError, TypeError, KeyError) as error:
        print(f"wp14_mechanism_probe: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
