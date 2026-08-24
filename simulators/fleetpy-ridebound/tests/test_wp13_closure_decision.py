import copy
import hashlib
import json
import pathlib
import unittest

import jsonschema


TEST_ROOT = pathlib.Path(__file__).resolve().parent
REPOSITORY_ROOT = TEST_ROOT.parents[2]
MANIFEST_PATH = (
    REPOSITORY_ROOT
    / "docs"
    / "benchmarking"
    / "evidence"
    / "wp13-closure-decision-v1.json"
)
SCHEMA_PATH = (
    REPOSITORY_ROOT
    / "benchmarks"
    / "schemas"
    / "wp13"
    / "v1"
    / "closure-decision.schema.json"
)


def _load(path):
    return json.loads(path.read_text(encoding="utf-8"))


def _sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


class Wp13ClosureDecisionTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.manifest = _load(MANIFEST_PATH)
        cls.schema = _load(SCHEMA_PATH)
        cls.validator = jsonschema.Draft202012Validator(cls.schema)

    def test_schema_and_manifest_are_valid(self):
        jsonschema.Draft202012Validator.check_schema(self.schema)
        self.validator.validate(self.manifest)

    def test_manifest_is_exact_canonical_utf8_json(self):
        raw = MANIFEST_PATH.read_bytes()
        self.assertFalse(raw.startswith(b"\xef\xbb\xbf"))
        self.assertNotIn(b"\r", raw)
        expected = (
            json.dumps(
                self.manifest,
                ensure_ascii=False,
                sort_keys=True,
                separators=(",", ":"),
            )
            + "\n"
        ).encode("utf-8")
        self.assertEqual(expected, raw)

    def test_source_and_external_receipts_resolve_exactly(self):
        identity = self.manifest["sourceIdentity"]
        self.assertEqual(identity["schemaSha256"], _sha256(SCHEMA_PATH))

        policy = identity["policyDocument"]
        policy_path = REPOSITORY_ROOT / policy["path"]
        self.assertEqual(policy["lengthBytes"], policy_path.stat().st_size)
        self.assertEqual(policy["sha256"], _sha256(policy_path))

        audit_reference = self.manifest["closureInput"]["auditReceipt"]
        audit_path = pathlib.Path(audit_reference["path"])
        self.assertEqual(audit_reference["lengthBytes"], audit_path.stat().st_size)
        self.assertEqual(audit_reference["sha256"], _sha256(audit_path))

        audit = _load(audit_path)
        active = audit["evidenceDag"]["activeArtifacts"]
        superseded = audit["evidenceDag"]["supersededArtifacts"]
        self.assertEqual(10, len(active))
        self.assertEqual(3, len(superseded))
        for reference in active + superseded:
            path = pathlib.Path(reference["path"])
            self.assertEqual(reference["lengthBytes"], path.stat().st_size)
            self.assertEqual(reference["sha256"], _sha256(path))

    def test_all_p3_findings_and_wp14_constraints_are_exact(self):
        finding_ids = {
            item["findingId"] for item in self.manifest["p3Resolutions"]
        }
        self.assertEqual(
            {
                "WP13-AUDIT-P3-001",
                "WP13-AUDIT-P3-002",
                "WP13-AUDIT-P3-003",
            },
            finding_ids,
        )
        self.assertEqual(
            {
                "newDevelopmentNamespaceAndCells",
                "h6PanelsExcludedFromConfigurationSelection",
                "freezeBeforeOutcome",
                "pairedParetoReportNoScalarPostOutcomeSelection",
                "declaredDiskAndRuntimeEnvelope",
                "noH7OrPolicyV2Authorization",
            },
            set(self.manifest["wp14Decision"]["constraints"]),
        )

    def test_unknown_field_mutation_is_rejected(self):
        mutant = copy.deepcopy(self.manifest)
        mutant["unexpected"] = True
        errors = list(self.validator.iter_errors(mutant))
        self.assertTrue(errors)

    def test_boolean_count_and_missing_constraint_mutations_are_rejected(self):
        boolean_mutant = copy.deepcopy(self.manifest)
        boolean_mutant["verification"]["dotNetPassed"] = True
        self.assertTrue(list(self.validator.iter_errors(boolean_mutant)))

        constraint_mutant = copy.deepcopy(self.manifest)
        constraint_mutant["wp14Decision"]["constraints"].pop()
        self.assertTrue(list(self.validator.iter_errors(constraint_mutant)))


if __name__ == "__main__":
    unittest.main()
