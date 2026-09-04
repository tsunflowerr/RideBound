import copy
import importlib.util
import json
import pathlib
import sys
import tempfile
import unittest

import jsonschema


ROOT = pathlib.Path(__file__).resolve().parents[3]
ADAPTER = ROOT / "simulators/fleetpy-ridebound"


def load_module(name, filename):
    specification = importlib.util.spec_from_file_location(
        name, ADAPTER / filename
    )
    module = importlib.util.module_from_spec(specification)
    specification.loader.exec_module(module)
    return module


MODULE = load_module(
    "wp14r_resource_dimension_under_test",
    "wp14r_resource_dimension.py",
)


class Wp14RResourceDimensionTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.base = pathlib.Path(self.temporary.name)
        self.forbidden = [self.base / "frozen"]

    def tearDown(self):
        self.temporary.cleanup()

    def context(self, suffix):
        return MODULE.build_context(
            self.base / f"dimension-{suffix}",
            self.forbidden,
            sys.executable,
            f"wp14r-dimension-{suffix}",
            f"host-session-{suffix}",
        )

    @staticmethod
    def case(case_id):
        return next(
            case for case in MODULE.fixed_corpus()
            if case["caseId"] == case_id
        )

    def test_fixed_corpus_policy_and_hashes_are_predeclared(self):
        corpus = MODULE.corpus_document()
        policy = MODULE.policy_document()
        self.assertEqual(MODULE.CASE_IDS, corpus["caseIds"])
        self.assertEqual(8, len(MODULE.fixed_corpus()))
        self.assertEqual(1, policy["pilotRepetitionsPerCell"])
        self.assertEqual(5, policy["measuredRepetitionsPerCell"])
        self.assertTrue(policy["pilotExcludedFromSummaries"])
        self.assertTrue(policy["retainEverySample"])
        self.assertFalse(policy["outlierDeletionAllowed"])
        self.assertEqual(64, len(corpus["corpusSha256"]))
        self.assertEqual(64, len(policy["policySha256"]))

    def test_monitor_uses_a_new_process_and_retains_raw_wrapper_streams(self):
        directory = self.base / "monitor"
        directory.mkdir()
        measurement = MODULE.monitor_process(
            [
                sys.executable,
                "-B",
                "-c",
                "import sys;sys.stdout.write('ok');sys.stderr.write('e')",
            ],
            directory / "stdout",
            directory / "stderr",
            dict(**__import__("os").environ),
        )
        self.assertEqual(0, measurement["returnCode"])
        self.assertNotEqual(__import__("os").getpid(), measurement["processId"])
        self.assertGreater(measurement["wallElapsedNs"], 0)
        self.assertGreater(measurement["peakRssBytes"], 0)
        self.assertEqual(2, measurement["stdoutBytes"])
        self.assertEqual(1, measurement["stderrBytes"])

    def test_silent_supervision_and_independent_verifier_are_measured(self):
        context = self.context("silent")
        sample = MODULE.run_sample(
            context,
            self.case("silentExit"),
            "pilot",
            1,
        )
        self.assertEqual("passed", sample["measurementStatus"])
        self.assertEqual(
            "childExitedZeroAwaitingBundleVerification",
            sample["terminalStatus"],
        )
        self.assertGreater(sample["launcher"]["peakRssBytes"], 0)
        self.assertGreater(sample["verifier"]["peakRssBytes"], 0)
        self.assertGreater(sample["journal"]["records"], 0)
        self.assertEqual(
            sample["journal"]["records"],
            sample["journal"]["fsyncCount"],
        )
        self.assertFalse(sample["outcomeFieldsRead"])
        self.assertTrue(sample["retained"])

    def test_exact_cap_and_nonzero_cells_preserve_typed_boundaries(self):
        context = self.context("typed")
        cap = MODULE.run_sample(
            context,
            self.case("exactCapBoundary"),
            "pilot",
            1,
        )
        nonzero = MODULE.run_sample(
            context,
            self.case("nonzeroExit"),
            "pilot",
            1,
        )
        self.assertEqual("passed", cap["measurementStatus"])
        self.assertEqual(
            65536,
            cap["journal"]["stdout"]["observedBytes"],
        )
        self.assertEqual(
            65536,
            cap["journal"]["stdout"]["retainedBytes"],
        )
        self.assertEqual("passed", nonzero["measurementStatus"])
        self.assertEqual("childExitFailure", nonzero["terminalStatus"])

    def test_partial_recovery_uses_one_fixed_safe_prelaunch_point(self):
        context = self.context("recovery")
        sample = MODULE.run_sample(
            context,
            self.case("partialRecovery"),
            "pilot",
            1,
        )
        self.assertEqual("passed", sample["measurementStatus"])
        self.assertEqual(
            "launcherRecoveredOrphanedStart",
            sample["terminalStatus"],
        )
        self.assertEqual("notObserved", sample["treeStatus"])
        self.assertIsNotNone(sample["recoverySourceSupervisorProcessId"])
        self.assertIsNone(sample["journal"]["supervisedChildProcessId"])
        control = context["controlRoot"] / sample["sampleId"]
        config = json.loads(
            (control / "worker-config.json").read_text(encoding="utf-8")
        )
        self.assertEqual("afterSupervisorStart", config["faultPoint"])

    def test_pilot_is_excluded_and_failure_is_retained_in_summary(self):
        case = self.case("silentExit")
        samples = []
        pilot = MODULE.sample_shell(case, "pilot", 1)
        pilot["measurementStatus"] = "passed"
        pilot["launcher"]["wallElapsedNs"] = 1
        samples.append(pilot)
        for repetition in range(1, 6):
            sample = MODULE.sample_shell(case, "measured", repetition)
            if repetition == 5:
                sample["failureType"] = "SyntheticFailure"
                sample["failureMessageSha256"] = "f" * 64
            else:
                sample["measurementStatus"] = "passed"
                sample["launcher"]["wallElapsedNs"] = repetition * 10
            samples.append(sample)
        summary = MODULE.summarize_case("silentExit", samples)
        self.assertEqual(5, summary["measuredSampleCount"])
        self.assertEqual(4, summary["passedSampleCount"])
        self.assertEqual(1, summary["failedSampleCount"])
        self.assertEqual(20, summary["launcherWallElapsedNs"]["median"])
        self.assertEqual(10, summary["launcherWallElapsedNs"]["minimum"])
        self.assertEqual(40, summary["launcherWallElapsedNs"]["maximum"])

    def synthetic_report(self):
        samples = []
        cases = MODULE.fixed_corpus()
        for phase, repetitions in (("pilot", 1), ("measured", 5)):
            for case in cases:
                for repetition in range(1, repetitions + 1):
                    sample = MODULE.sample_shell(case, phase, repetition)
                    sample["failureType"] = "SyntheticFailure"
                    sample["failureMessageSha256"] = "f" * 64
                    samples.append(sample)
        summaries = [
            MODULE.summarize_case(case_id, samples)
            for case_id in MODULE.CASE_IDS
        ]
        return {
            "schemaVersion": MODULE.ARTIFACT_SCHEMA_VERSION,
            "schemaId": MODULE.SCHEMA_ID,
            "reportType": MODULE.REPORT_TYPE,
            "status": "completeWithMeasurementFailures",
            "claimBoundary": MODULE.CLAIM_BOUNDARY,
            "dimensioningId": "synthetic-dimension",
            "generatedUtc": MODULE.utc_now(),
            "reportInputsSha256": "a" * 64,
            "forbiddenRootPathSha256s": ["b" * 64],
            "hostSession": {
                "hostSessionId": "synthetic-host",
                "hostFingerprintSha256": "c" * 64,
                "platform": "Windows",
                "release": "test",
                "machine": "test",
                "logicalCpuCount": 1,
            },
            "toolchain": {
                "dimensionerSha256": "d" * 64,
                "supervisorSha256": "d" * 64,
                "ledgerSha256": "d" * 64,
                "recoverySha256": "d" * 64,
                "faultInjectionSha256": "d" * 64,
                "wp14rSchemaTreeSha256": "d" * 64,
                "pythonExecutableSha256": "d" * 64,
                "pythonVersion": "test",
                "psutilVersion": "test",
            },
            "corpus": MODULE.corpus_document(),
            "policy": MODULE.policy_document(),
            "samples": samples,
            "summaries": summaries,
            "decision": MODULE.build_decision(samples, summaries, "Windows"),
        }

    def test_strict_report_schema_rejects_claim_or_extra_field_mutation(self):
        report = self.synthetic_report()
        MODULE.validate_report(report)
        changed_claim = copy.deepcopy(report)
        changed_claim["claimBoundary"] = ["mechanicalOnly"]
        with self.assertRaises(MODULE.DimensionError):
            MODULE.validate_report(changed_claim)
        extra = copy.deepcopy(report)
        extra["samples"][0]["scientificOutcome"] = 1
        with self.assertRaises(MODULE.DimensionError):
            MODULE.validate_report(extra)

    def test_schema_itself_is_valid_draft_2020_12(self):
        schema = MODULE.load_schema()
        jsonschema.Draft202012Validator.check_schema(schema)

    def test_output_root_refuses_repository_overlap_and_reuse(self):
        dependencies = MODULE.load_dependencies()
        with self.assertRaisesRegex(
            MODULE.DimensionError, "outside the repository"
        ):
            MODULE.validate_roots(
                ROOT / "tmp-wp14r-dimension",
                self.forbidden,
                dependencies["ledger"],
            )
        existing = self.base / "existing"
        existing.mkdir()
        with self.assertRaisesRegex(MODULE.DimensionError, "already exists"):
            MODULE.validate_roots(
                existing,
                self.forbidden,
                dependencies["ledger"],
            )


if __name__ == "__main__":
    unittest.main()
