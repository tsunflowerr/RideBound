import copy
import pathlib
import sys
import tempfile
import unittest

import jsonschema

TEST_ROOT = pathlib.Path(__file__).resolve().parent
ADAPTER_ROOT = TEST_ROOT.parent
for import_root in (TEST_ROOT, ADAPTER_ROOT):
    if str(import_root) not in sys.path:
        sys.path.insert(0, str(import_root))

import wp13_full_audit as audit
from test_medium_verifier import _audited_evidence_v12


class Wp13FullAuditTests(unittest.TestCase):
    def test_fast_audit_reconciles_repository_and_external_dag(self):
        result = audit.build_audit(deep_raw_verify=False)

        self.assertEqual("RB-WP13-012", result["auditTicket"])
        self.assertGreaterEqual(result["repositoryInventory"]["fileCount"], 70)
        self.assertEqual(0, result["evidenceDag"]["failedEdgeCount"])
        self.assertEqual(
            0,
            result["findings"]["unresolvedP0Count"]
            + result["findings"]["unresolvedP1Count"]
            + result["findings"]["unresolvedP2Count"],
        )
        self.assertEqual("notRequested", result["deepRawVerification"]["status"])

    def test_duplicate_json_and_noncanonical_json_are_rejected(self):
        with self.assertRaisesRegex(ValueError, "duplicate JSON field"):
            audit._loads('{"a":1,"a":2}')
        with self.assertRaisesRegex(ValueError, "non-integer JSON number"):
            audit._loads('{"a":1.0}')

        with tempfile.TemporaryDirectory() as root:
            path = pathlib.Path(root) / "noncanonical.json"
            path.write_text('{"b":2, "a":1}\n', encoding="utf-8")
            with self.assertRaisesRegex(RuntimeError, "not exact canonical"):
                audit._load_canonical(path)

    def test_artifact_reference_discovery_preserves_superseded_status(self):
        value = {
            "active": {
                "path": "active.json",
                "lengthBytes": 1,
                "sha256": "0" * 64,
            },
            "supersededReport": {
                "path": "old.json",
                "lengthBytes": 2,
                "sha256": "1" * 64,
            },
        }

        references = audit._artifact_references(value, "summary.json")

        self.assertEqual(2, len(references))
        self.assertFalse(references[0]["superseded"])
        self.assertTrue(references[1]["superseded"])

    def test_output_path_rejects_every_immutable_raw_root(self):
        for root in audit._FORBIDDEN_OUTPUT_ROOTS:
            with self.assertRaisesRegex(RuntimeError, "outside immutable root"):
                audit._require_safe_output(root / "audit.json")

    def test_composite_portfolio_guard_closes_frozen_verifier_optional_field_gap(self):
        evidence = _audited_evidence_v12()
        portfolio = evidence["candidatePortfolio"]
        portfolio["candidates"][0]["repairedIncumbentRequestId"] = 123

        with self.assertRaisesRegex(RuntimeError, "schema differs"):
            audit._validate_retained_portfolio_schema_and_identifiers(
                portfolio,
                "portfolio",
            )

        verifier = audit._harden_retained_verifier(
            audit._load_module(
                ADAPTER_ROOT / "actual_fleetpy_medium_verify.py",
                "ridebound_wp13_audit_mutation_verifier",
            )
        )
        with self.assertRaisesRegex(RuntimeError, "protocol identifier"):
            verifier._verify_audited_solver_evidence(
                {"status": "completed", "executionEvidence": evidence},
                1,
            )

        evidence = _audited_evidence_v12()
        portfolio = evidence["candidatePortfolio"]
        portfolio["candidates"][0]["repairedIncumbentRequestId"] = "x" * 129
        with self.assertRaisesRegex(RuntimeError, "128-byte"):
            audit._validate_retained_portfolio_schema_and_identifiers(
                portfolio,
                "portfolio",
            )

        evidence = _audited_evidence_v12()
        portfolio = evidence["candidatePortfolio"]
        portfolio["selectionProblem"]["objectiveLevels"][0]["levelIndex"] = False
        with self.assertRaisesRegex(RuntimeError, "canonical integer"):
            audit._validate_retained_portfolio_schema_and_identifiers(
                portfolio,
                "portfolio",
                full_schema=False,
            )

    def test_schema_rejects_unresolved_priority_two_and_claim_drift(self):
        result = audit.build_audit(deep_raw_verify=False)
        schema = audit._load_strict(audit._SCHEMA)

        unresolved = copy.deepcopy(result)
        unresolved["findings"]["unresolvedP2Count"] = 1
        with self.assertRaises(jsonschema.ValidationError):
            jsonschema.Draft202012Validator(schema).validate(unresolved)

        causal = copy.deepcopy(result)
        causal["evidenceDag"]["aggregateInvariants"]["interpretation"] = (
            "causalDecomposition"
        )
        with self.assertRaises(jsonschema.ValidationError):
            jsonschema.Draft202012Validator(schema).validate(causal)

    def test_canonical_encoding_round_trips_exactly(self):
        value = {"z": [2, 1], "a": {"value": 0}}
        encoded = audit._encoded(value)

        self.assertEqual(value, audit._loads(encoded))
        self.assertEqual(
            '{"a":{"value":0},"z":[2,1]}\n',
            encoded,
        )

    def test_claim_context_requires_an_explicit_caveat(self):
        self.assertTrue(
            audit._claim_context_is_caveat(
                ["Không được dùng output để nói:", "causal decomposition"],
                1,
            )
        )
        self.assertFalse(
            audit._claim_context_is_caveat(
                ["The result establishes causal decomposition."],
                0,
            )
        )


if __name__ == "__main__":
    unittest.main()
