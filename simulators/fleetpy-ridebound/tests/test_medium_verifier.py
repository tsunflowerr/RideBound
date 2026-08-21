from __future__ import annotations

import base64
import hashlib
import json
import pathlib
import sys
import tempfile
import types
import unittest
from types import SimpleNamespace
from unittest import mock


ADAPTER_ROOT = pathlib.Path(__file__).parents[1]
sys.path.insert(0, str(ADAPTER_ROOT))

import actual_fleetpy_medium_preflight as preflight
import actual_fleetpy_medium_verify as verifier


class MediumVerifierTests(unittest.TestCase):
    def test_contended_bundle_passes_and_generated_ids_do_not_change_behavior(self):
        with tempfile.TemporaryDirectory() as first_root, tempfile.TemporaryDirectory() as second_root:
            first = pathlib.Path(first_root)
            second = pathlib.Path(second_root)
            _write_bundle(first, candidate_id="candidate-a")
            _write_bundle(second, candidate_id="candidate-b")

            first_result = verifier.verify_bundle(first, include_behavioral_hash=True)
            second_result = verifier.verify_bundle(second, include_behavioral_hash=True)

            self.assertEqual("pass", first_result["status"])
            self.assertEqual(2, first_result["runs"][0]["requestCount"])
            self.assertEqual(
                first_result["behavioralProjectionHash"],
                second_result["behavioralProjectionHash"],
            )

    def test_checkpoint_content_hash_is_independently_verified(self):
        with tempfile.TemporaryDirectory() as root:
            bundle = pathlib.Path(root)
            _write_bundle(bundle, invalid_checkpoint_hash=True)

            with self.assertRaisesRegex(RuntimeError, "checkpoint"):
                verifier.verify_bundle(bundle)

    def test_report_semantic_hash_is_independently_verified(self):
        with tempfile.TemporaryDirectory() as root:
            bundle = pathlib.Path(root)
            _write_bundle(bundle, invalid_semantic_hash=True)

            with self.assertRaisesRegex(RuntimeError, "report/transcript"):
                verifier.verify_bundle(bundle)

    def test_lifecycle_outcomes_must_partition_arrivals(self):
        with tempfile.TemporaryDirectory() as root:
            bundle = pathlib.Path(root)
            _write_bundle(bundle, omit_rejection=True)

            with self.assertRaisesRegex(RuntimeError, "conservation"):
                verifier.verify_bundle(bundle)

    def test_audited_solver_gate_requires_exact_optimal_evidence(self):
        with tempfile.TemporaryDirectory() as valid_root, tempfile.TemporaryDirectory() as legacy_root:
            valid = pathlib.Path(valid_root)
            legacy = pathlib.Path(legacy_root)
            _write_bundle(valid, audited_evidence=True)
            _write_bundle(legacy)

            self.assertEqual(
                "pass",
                verifier.verify_bundle(
                    valid,
                    require_audited_solver_evidence=True,
                )["status"],
            )
            with self.assertRaisesRegex(RuntimeError, "solver evidence"):
                verifier.verify_bundle(
                    legacy,
                    require_audited_solver_evidence=True,
                )


class MediumPreflightSeedTests(unittest.TestCase):
    def test_master_seed_is_explicitly_propagated_to_runner_settings(self):
        globals_module = types.ModuleType("src.misc.globals")
        globals_module.G_OP_FLEET = "fleet"
        globals_module.G_OP_VR_CTRL_F = "control"
        misc_module = types.ModuleType("src.misc")
        src_module = types.ModuleType("src")
        arguments = SimpleNamespace(
            dotnet=pathlib.Path("dotnet"),
            runner_root=pathlib.Path("runner"),
            commitment_config=pathlib.Path("commitment.json"),
            wp4_config=pathlib.Path("wp4.json"),
            fleetpy_root=pathlib.Path("fleetpy"),
            repository_root=pathlib.Path("repository"),
            label="seed-check",
            master_seed=41,
        )

        with mock.patch.dict(
            sys.modules,
            {
                "src": src_module,
                "src.misc": misc_module,
                "src.misc.globals": globals_module,
            },
        ):
            settings = preflight._operator_settings(
                arguments,
                pathlib.Path("scenario"),
                [],
                10,
                pathlib.Path("transcript.ndjson"),
            )

        self.assertEqual(41, settings["ridebound_master_seed"])

    def test_master_seed_is_limited_to_nonnegative_int32(self):
        self.assertEqual(0, preflight._validate_master_seed(0))
        self.assertEqual(2_147_483_647, preflight._validate_master_seed(2_147_483_647))
        for value in (-1, 2_147_483_648, True, "7"):
            with self.subTest(value=value):
                with self.assertRaises(ValueError):
                    preflight._validate_master_seed(value)


def _write_bundle(
    directory,
    *,
    candidate_id="candidate-a",
    invalid_checkpoint_hash=False,
    invalid_semantic_hash=False,
    omit_rejection=False,
    audited_evidence=False,
):
    manifest = {
        "protocolVersion": "1.0.0",
        "masterSeed": 7,
        "policyId": "policy",
        "policyVersion": "v1",
        "policyConfigurationHash": "a" * 64,
        "scenarioContentHash": "b" * 64,
        "graphSnapshotHash": "c" * 64,
        "travelTimeSnapshotHash": "d" * 64,
        "costUnitId": "cost",
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
            "positionModel": "nodeOnly",
            "capabilities": ["exactEventOrdering"],
            "maxFleetSize": 10,
            "maxRequestCount": 10,
        },
        "adapter": {"adapterId": "adapter", "adapterVersion": "1.0.0"},
        "simulator": {
            "simulatorId": "simulator",
            "simulatorVersion": "1.0.0",
            "upstreamCommitSha": "e" * 40,
        },
        "coreCommitSha": "f" * 40,
        "binarySha256": "1" * 64,
    }
    manifest_hash = verifier._manifest_hash(manifest)
    initial_state_hash = "2" * 64
    decision_hash = "3" * 64
    final_state_hash = "4" * 64
    run_id = "test-run"
    scenario_id = "test-scenario"
    context = {
        "schemaVersion": "1.0.0",
        "runId": run_id,
        "scenarioId": scenario_id,
    }
    events = [
        {
            "eventSeq": 1,
            "eventType": "requestArrived",
            "payload": {"request": {"requestId": "rq-a"}},
        },
        {
            "eventSeq": 2,
            "eventType": "requestArrived",
            "payload": {"request": {"requestId": "rq-b"}},
        },
        {
            "eventSeq": 3,
            "eventType": "bookingConfirmed",
            "payload": {"requestId": "rq-a"},
        },
        {
            "eventSeq": 4,
            "eventType": "passengerBoarded",
            "payload": {"requestId": "rq-a"},
        },
        {
            "eventSeq": 5,
            "eventType": "passengerAlighted",
            "payload": {"requestId": "rq-a"},
        },
    ]
    actions = [
        {
            "decisionType": "requestAccepted",
            "payload": {
                "candidateId": candidate_id,
                "requestId": "rq-a",
                "vehicleId": "veh-a",
            },
        }
    ]
    if not omit_rejection:
        actions.append(
            {
                "decisionType": "requestRejected",
                "payload": {"reasonCode": "NO_FEASIBLE_INSERTION", "requestId": "rq-b"},
            }
        )
    content = {
        "manifestHash": manifest_hash,
        "stateHash": final_state_hash,
        "previousDecisionHash": decision_hash,
        "appliedEpoch": 1,
        "nextEventSeq": 6,
        "simTimeMs": 100,
        "onlineState": {
            "requests": [
                {"requestId": "rq-a", "lifecycle": "completed"},
                {"requestId": "rq-b", "lifecycle": "rejected"},
            ],
            "vehicles": [{}],
        },
    }
    checkpoint_hash = verifier._checkpoint_hash(content)
    if invalid_checkpoint_hash:
        checkpoint_hash = "9" * 64
    solver = {"status": "optimal"}
    if audited_evidence:
        solver = {
            "status": "completed",
            "executionEvidence": _audited_evidence(),
        }
    frames = [
        ("adapterToRunner", {"schemaVersion": "1.0.0", "messageType": "hello", "payload": {}}),
        ("runnerToAdapter", {"schemaVersion": "1.0.0", "messageType": "helloAck", "payload": {}}),
        (
            "adapterToRunner",
            {**context, "messageType": "initializeRun", "payload": {"manifest": manifest}},
        ),
        (
            "runnerToAdapter",
            {
                **context,
                "messageType": "initialized",
                "payload": {
                    "manifestHash": manifest_hash,
                    "initialStateIdentity": {
                        "epochId": 0,
                        "nextEventSeq": 1,
                        "simTimeMs": 0,
                        "stateHash": initial_state_hash,
                    },
                },
            },
        ),
        (
            "adapterToRunner",
            {
                **context,
                "messageType": "eventBatch",
                "epochId": 1,
                "simTimeMs": 100,
                "payload": {"events": events},
            },
        ),
        (
            "runnerToAdapter",
            {
                **context,
                "messageType": "decision",
                "epochId": 1,
                "simTimeMs": 100,
                "payload": {
                    "decisionHash": decision_hash,
                    "previousDecisionHash": "0" * 64,
                    "stateBeforeHash": initial_state_hash,
                    "stateAfterHash": final_state_hash,
                    "actions": actions,
                    "solver": solver,
                },
            },
        ),
        (
            "adapterToRunner",
            {
                **context,
                "messageType": "decisionApplied",
                "epochId": 1,
                "simTimeMs": 100,
                "payload": {"decisionHash": decision_hash},
            },
        ),
        ("adapterToRunner", {**context, "messageType": "checkpoint", "payload": {}}),
        (
            "runnerToAdapter",
            {
                **context,
                "messageType": "checkpoint",
                "payload": {
                    "checkpointVersion": "1.0.0",
                    "checkpointHash": checkpoint_hash,
                    "content": content,
                },
            },
        ),
        ("adapterToRunner", {"schemaVersion": "1.0.0", "messageType": "shutdown", "payload": {}}),
    ]
    transcript = b"".join(
        _transcript_record(ordinal, direction, envelope)
        for ordinal, (direction, envelope) in enumerate(frames, 1)
    )
    (directory / "transcript-00.ndjson").write_bytes(transcript)
    semantic = {
        "sourceScenarioContentSha256": "5" * 64,
        "nodeMappingHash": "6" * 64,
        "manifestHash": manifest_hash,
        "checkpointBindingHash": "7" * 64,
        "requestCount": 2,
        "acceptedRequestIds": ["adapter-a"],
        "rejectedRequestIds": ["adapter-b"],
        "publicationCount": 0,
        "publicationDigest": hashlib.sha256(verifier._canonical([])).hexdigest(),
        "travelSnapshotVersion": 1,
        "nextEventSeq": 6,
        "nextEpoch": 2,
        "finalSimulationTimeMs": 100,
        "finalVehiclePositions": [[1, None, None]],
    }
    semantic_hash = verifier._semantic_hash(semantic)
    report = {
        "schemaVersion": "1.0.0",
        "status": "succeeded",
        "repeat": 0,
        "semanticHash": "8" * 64 if invalid_semantic_hash else semantic_hash,
        "semantic": semantic,
        "artifactReceiptsEqual": True,
    }
    summary = {
        "schemaVersion": "1.0.0",
        "status": "pass",
        "repeatCount": 1,
        "semanticHash": report["semanticHash"],
    }
    (directory / "run-00.json").write_bytes(verifier._canonical(report))
    (directory / "summary.json").write_bytes(verifier._canonical(summary))
    files = []
    for path in sorted(directory.iterdir()):
        data = path.read_bytes()
        files.append(
            {
                "path": path.name,
                "lengthBytes": len(data),
                "sha256": hashlib.sha256(data).hexdigest(),
            }
        )
    bundle_manifest = {
        "schemaVersion": "1.0.0",
        "bundleType": "ridebound-wp7-actual-fleetpy-medium-v1",
        "files": files,
    }
    (directory / "bundle-manifest.json").write_bytes(
        verifier._canonical(bundle_manifest)
    )


def _audited_evidence():
    solver_diagnostics = {
        "consumedWorkUnits": 2,
        "consumedDeterministicTimeMicros": 3,
        "objectiveBounds": [
            {
                "levelIndex": 0,
                "objectiveName": "acceptedRequests",
                "incumbentValue": 1,
                "bestBound": 1,
                "gapNumerator": 0,
                "gapDenominator": 1,
                "isProvenOptimal": True,
            }
        ],
    }
    return {
        "evidenceVersion": "1.0.0",
        "generation": {
            "totalPendingRequestCount": 1,
            "consideredRequestCount": 1,
            "omittedRequestCount": 0,
            "vehicleLosses": [
                {
                    "vehicleId": "veh-a",
                    "explorationWorkUnits": 5,
                    "evaluatedCandidatePathCount": 1,
                    "uniqueFeasibleCandidateCountBeforeCap": 1,
                    "retainedCandidateCount": 1,
                    "physicallyOrSchedulePrunedCount": 0,
                    "omittedUnexpandedCandidatePathCount": 0,
                    "omittedFeasibleCandidateCountByCap": 0,
                    "workBudgetExhausted": False,
                    "candidateCapApplied": False,
                    "omissionCountWasSaturated": False,
                    "eligibleRepairRequestCount": 0,
                    "consideredRepairRequestCount": 0,
                    "omittedRepairRequestCount": 0,
                }
            ],
            "omissions": [],
        },
        "prunedCandidates": [],
        "selection": {
            "consumedGenerationWorkUnits": 5,
            "consumedValidationWorkUnits": 1,
            "omittedCandidateCount": 0,
            "omissionCountWasSaturated": False,
            "primarySolveStatus": "optimal",
            "primarySolverDiagnostics": solver_diagnostics,
            "finalSolveStatus": "optimal",
            "finalSolverDiagnostics": dict(solver_diagnostics),
            "executionPath": "validatedIncumbent",
            "fallbackValidationAttempts": 0,
            "primaryIncumbentRejected": False,
            "validationWitnesses": [],
        },
    }


def _transcript_record(ordinal, direction, envelope):
    frame = verifier._canonical(envelope)
    record = {
        "schemaVersion": "1.0.0",
        "ordinal": ordinal,
        "direction": direction,
        "frameLengthBytes": len(frame),
        "frameSha256": hashlib.sha256(frame).hexdigest(),
        "frameBase64": base64.b64encode(frame).decode("ascii"),
    }
    return verifier._canonical(record) + b"\n"


if __name__ == "__main__":
    unittest.main()
