import importlib.util
import json
import pathlib
import tempfile
import unittest

import jsonschema


ROOT = pathlib.Path(__file__).resolve().parents[3]
ADAPTER = ROOT / "simulators/fleetpy-ridebound"


def load_module(name, filename):
    specification = importlib.util.spec_from_file_location(name, ADAPTER / filename)
    module = importlib.util.module_from_spec(specification)
    specification.loader.exec_module(module)
    return module


FIXTURE = load_module(
    "wp14r_independent_fixture_under_test",
    "wp14r_independent_fixture.py",
)
MUTATION = load_module(
    "wp14r_independent_mutation_under_test",
    "wp14r_independent_mutation.py",
)
VERIFIER = load_module(
    "wp14r_independent_verifier_for_matrix_tests",
    "wp14r_independent_verify.py",
)


class Wp14RIndependentMutationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.temporary = tempfile.TemporaryDirectory()
        cls.base = pathlib.Path(cls.temporary.name)
        cls.forbidden = [cls.base / "frozen"]
        cls.fixture_root = cls.base / "fixtures"
        cls.receipt = FIXTURE.build_fixtures(
            cls.fixture_root,
            cls.forbidden,
            "2026-08-27T06:00:00Z",
        )
        cls.clean_tree_before = FIXTURE.job_tree_sha256(
            cls.fixture_root / "ledger" / FIXTURE.CLEAN_JOB_ID,
            VERIFIER,
        )
        cls.recovery_tree_before = FIXTURE.job_tree_sha256(
            cls.fixture_root / "ledger" / FIXTURE.RECOVERY_JOB_ID,
            VERIFIER,
        )
        cls.matrix_root = cls.base / "matrix"
        cls.report = MUTATION.run_matrix(
            cls.fixture_root / "ledger",
            FIXTURE.CLEAN_JOB_ID,
            cls.fixture_root / "ledger",
            FIXTURE.RECOVERY_JOB_ID,
            cls.forbidden,
            cls.matrix_root,
            "2026-08-27T06:10:00Z",
        )

    @classmethod
    def tearDownClass(cls):
        cls.temporary.cleanup()

    def test_fixture_receipt_is_canonical_strict_and_cross_verified(self):
        path = self.fixture_root / "independent-fixture-receipt.json"
        self.assertEqual(VERIFIER.canonical(self.receipt), path.read_bytes())
        schema = VERIFIER._load_schema(
            "independent-fixture-receipt.schema.json"
        )
        jsonschema.Draft202012Validator(schema).validate(self.receipt)
        self.assertEqual(
            ["attemptOpen", "recoveryAuthorized"],
            [row["observedLedgerState"] for row in self.receipt["jobs"]],
        )
        self.assertFalse(self.receipt["scientificOutcomeFieldsRead"])

    def test_all_fifteen_mutation_classes_are_retained_and_caught(self):
        self.assertEqual(15, self.report["mutationClassCount"])
        self.assertEqual(15, self.report["caughtMutationClassCount"])
        self.assertTrue(all(row["caught"] for row in self.report["cases"]))
        link_case = next(
            row for row in self.report["cases"] if row["mutationClass"] == "pathLink"
        )
        link = (
            self.matrix_root
            / link_case["retainedRelativeRoot"]
            / FIXTURE.CLEAN_JOB_ID
            / "attempt-01/output"
        )
        self.assertTrue(VERIFIER._is_link_or_junction(link))
        self.assertEqual("PATH_UNSAFE", link_case["observedRejectionCode"])

    def test_report_is_canonical_and_valid_under_strict_schema(self):
        path = self.matrix_root / "independent-mutation-report.json"
        self.assertEqual(VERIFIER.canonical(self.report), path.read_bytes())
        schema = VERIFIER._load_schema(
            "independent-mutation-report.schema.json"
        )
        jsonschema.Draft202012Validator(schema).validate(self.report)
        mutant = dict(self.report)
        mutant["caughtMutationClassCount"] = 0
        with self.assertRaises(jsonschema.ValidationError):
            jsonschema.Draft202012Validator(schema).validate(mutant)

    def test_source_fixtures_are_unchanged_and_output_is_exclusive(self):
        self.assertEqual(
            self.clean_tree_before,
            FIXTURE.job_tree_sha256(
                self.fixture_root / "ledger" / FIXTURE.CLEAN_JOB_ID,
                VERIFIER,
            ),
        )
        self.assertEqual(
            self.recovery_tree_before,
            FIXTURE.job_tree_sha256(
                self.fixture_root / "ledger" / FIXTURE.RECOVERY_JOB_ID,
                VERIFIER,
            ),
        )
        with self.assertRaisesRegex(MUTATION.MutationError, "already exists"):
            MUTATION.run_matrix(
                self.fixture_root / "ledger",
                FIXTURE.CLEAN_JOB_ID,
                self.fixture_root / "ledger",
                FIXTURE.RECOVERY_JOB_ID,
                self.forbidden,
                self.matrix_root,
                "2026-08-27T06:20:00Z",
            )


if __name__ == "__main__":
    unittest.main()
