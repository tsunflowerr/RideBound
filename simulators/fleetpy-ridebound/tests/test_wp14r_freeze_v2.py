import copy
import importlib.util
import json
import pathlib
import tempfile
import unittest
from unittest import mock

import jsonschema


ROOT = pathlib.Path(__file__).resolve().parents[3]
MODULE_PATH = ROOT / "simulators/fleetpy-ridebound/wp14r_freeze_v2.py"
SPEC = importlib.util.spec_from_file_location(
    "wp14r_freeze_v2_under_test",
    MODULE_PATH,
)
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)
AUTHORIZED_AT = "2026-08-28T02:00:00Z"


RECEIPT_PATH = (
    ROOT / "benchmarks/scenarios/wp14r-development/freeze-v2-authorization.json"
)
RECEIPT_SHA256 = (
    "6b34010861d60d6f0e869e3115ee1b20c6b5eb2eba3d6823a7e16148d1a31237"
)


class Wp14RFreezeV2Tests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        # Freeze v2 is a superseded record. It binds a verifier source that
        # freeze v4 had to narrow after a host clock step made a completed run
        # unverifiable, so v2 can no longer be rebuilt from the working tree.
        # Its retained bytes stay the authority; source-divergence-v3.json
        # carries the recovery proof for every file that moved.
        cls.receipt = json.loads(RECEIPT_PATH.read_text(encoding="utf-8"))

    def test_the_retained_receipt_bytes_are_untouched(self):
        raw = RECEIPT_PATH.read_bytes()
        self.assertEqual(MODULE.sha256_bytes(raw), RECEIPT_SHA256)
        self.assertEqual(raw, MODULE.canonical(self.receipt) + b"\n")

    def test_the_receipt_is_no_longer_rebuildable_from_current_source(self):
        with self.assertRaises(MODULE.FreezeV2Error):
            MODULE.build(ROOT, AUTHORIZED_AT)

    def test_receipt_is_strict_schema_valid_and_authorizes_only_protocol(self):
        schema = json.loads(
            (ROOT / MODULE.SCHEMA_RELATIVE).read_text(encoding="utf-8")
        )
        jsonschema.Draft202012Validator.check_schema(schema)
        jsonschema.Draft202012Validator(
            schema,
            format_checker=jsonschema.FormatChecker(),
        ).validate(self.receipt)
        self.assertEqual(
            "protocolAuthorizedExecutionPreconditionsRequired",
            self.receipt["authorizationStatus"],
        )
        self.assertTrue(
            self.receipt["ownerAuthorization"][
                "scientificLaunchRequiresPassingHostPreflight"
            ]
        )

    def test_base_scientific_design_is_referenced_not_redefined(self):
        base = json.loads(
            (ROOT / MODULE.BASE_RECEIPT_RELATIVE).read_text(encoding="utf-8")
        )
        self.assertEqual(
            MODULE.EXPECTED_BASE_RECEIPT_SHA256,
            self.receipt["baseScientificFreeze"]["artifact"]["sha256"],
        )
        self.assertEqual(
            MODULE.sha256_bytes(MODULE.canonical(base["design"])),
            self.receipt["baseScientificFreeze"]["designSha256"],
        )
        self.assertNotIn("design", self.receipt)

    def test_pair_and_full_order_are_exact_pre_outcome_bindings(self):
        base = json.loads(
            (ROOT / MODULE.BASE_RECEIPT_RELATIVE).read_text(encoding="utf-8")
        )
        ids = [job["jobId"] for job in base["design"]["jobs"]]
        protocol = self.receipt["protocol"]
        self.assertEqual(
            MODULE.PAIR_JOB_IDS,
            protocol["pairedResourceGate"]["jobIds"],
        )
        self.assertEqual(
            MODULE.sha256_bytes(MODULE.canonical(ids)),
            protocol["fullMatrix"]["jobOrderSha256"],
        )
        self.assertFalse(protocol["pairedResourceGate"]["outcomeReadPermitted"])

    def test_power_and_quiescence_are_frozen_but_current_ac_is_not_faked(self):
        policy = self.receipt["protocol"]["hostPolicy"]
        self.assertEqual("online", policy["requiredAcLineStatus"])
        self.assertEqual(
            MODULE.EXPECTED_POWER_SCHEME_GUID,
            policy["requiredPowerSchemeGuid"],
        )
        self.assertEqual(10, policy["sampleCount"])
        self.assertEqual(20, policy["maximumMeanCpuBusyPercent"])
        self.assertFalse(policy["arbitraryProcessNamesOrCommandLinesRecorded"])

    def test_recovery_and_units_cannot_be_reinterpreted(self):
        protocol = self.receipt["protocol"]
        self.assertEqual(2, protocol["maximumAttemptsPerJob"])
        self.assertFalse(protocol["attemptsAreExperimentalUnits"])
        self.assertEqual(
            "oneInitialOneMechanicalRecoveryRetainAllNoThirdAttempt",
            protocol["recoveryPolicy"],
        )
        self.assertIn(
            "doesNotSupersedeWp14V1Failure",
            self.receipt["claimBoundary"],
        )

    def test_source_identity_binds_orchestrator_schemas_tests_and_gates(self):
        files = self.receipt["sourceIdentity"]["repositoryFiles"]
        paths = [value["path"] for value in files]
        self.assertEqual(len(paths), len(set(paths)))
        for expected in (
            "simulators/fleetpy-ridebound/wp14r_scientific_protocol.py",
            "simulators/fleetpy-ridebound/wp14r_host_preflight.py",
            "benchmarks/schemas/wp14r/v2/freeze-v2-authorization.schema.json",
            "benchmarks/schemas/wp14r/v2/paired-resource-gate-receipt.schema.json",
            "simulators/fleetpy-ridebound/tests/test_wp14r_freeze_v2.py",
        ):
            self.assertIn(expected, paths)
        self.assertEqual(
            3,
            len(self.receipt["sourceIdentity"]["mechanicsGateArtifacts"]),
        )

    def test_all_three_methodology_pdfs_are_full_artifacts(self):
        evidence = self.receipt["methodologyEvidence"]
        self.assertEqual([12, 12, 10], [value["pageCount"] for value in evidence])
        self.assertEqual(
            [value[3] for value in MODULE.METHODOLOGY_EVIDENCE],
            [value["artifact"]["sha256"] for value in evidence],
        )

    def test_isolation_includes_wp14_v1_output_and_four_frozen_roots(self):
        base = json.loads(
            (ROOT / MODULE.BASE_RECEIPT_RELATIVE).read_text(encoding="utf-8")
        )
        forbidden = self.receipt["protocol"]["isolation"]["forbiddenRoots"]
        self.assertEqual(5, len(forbidden))
        self.assertIn(base["execution"]["outputRoot"], forbidden)
        for value in base["execution"]["forbiddenRoots"]:
            self.assertIn(value, forbidden)

    def test_noncanonical_or_mutated_receipt_fails_verification(self):
        with tempfile.TemporaryDirectory() as directory:
            path = pathlib.Path(directory) / "freeze.json"
            path.write_bytes(MODULE.canonical(self.receipt) + b"\n")
            with mock.patch.object(
                MODULE,
                "build",
                return_value=self.receipt,
            ):
                self.assertEqual(self.receipt, MODULE.verify_receipt(path, ROOT))
                path.write_bytes(json.dumps(self.receipt).encode("utf-8"))
                with self.assertRaisesRegex(MODULE.FreezeV2Error, "canonical"):
                    MODULE.verify_receipt(path, ROOT)
            mutant = copy.deepcopy(self.receipt)
            mutant["protocol"]["execution"]["maximumParallelJobs"] = 4
            path.write_bytes(MODULE.canonical(mutant) + b"\n")
            with mock.patch.object(
                MODULE,
                "build",
                return_value=self.receipt,
            ):
                with self.assertRaisesRegex(MODULE.FreezeV2Error, "differs"):
                    MODULE.verify_receipt(path, ROOT)

    def test_missing_or_changed_mechanics_gate_fails_before_authorization(self):
        original = MODULE.checked_artifact

        def changed(path, expected, display_path=None):
            if "mechanics-dimension" in str(path):
                raise MODULE.FreezeV2Error("audited gate changed")
            return original(path, expected, display_path)

        with mock.patch.object(MODULE, "checked_artifact", side_effect=changed):
            with self.assertRaisesRegex(MODULE.FreezeV2Error, "audited gate"):
                MODULE.source_identity(
                    ROOT,
                    json.loads(
                        (ROOT / MODULE.BASE_RECEIPT_RELATIVE).read_text(
                            encoding="utf-8"
                        )
                    ),
                )


if __name__ == "__main__":
    unittest.main()
