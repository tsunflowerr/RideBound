import importlib.util
import json
import pathlib
import unittest

import jsonschema


ROOT = pathlib.Path(__file__).resolve().parents[3]
MODULE_PATH = (
    ROOT / "simulators" / "fleetpy-ridebound" / "wp13_e1_falsify.py"
)
SPEC = importlib.util.spec_from_file_location("wp13_e1_falsify_under_test", MODULE_PATH)
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class Wp13E1FalsificationTests(unittest.TestCase):
    def test_schema_is_strict_draft_2020_12(self):
        schema = json.loads(
            (
                ROOT
                / "benchmarks/schemas/wp13/v1/"
                "e1-falsification-receipt.schema.json"
            ).read_text(encoding="utf-8")
        )
        jsonschema.Draft202012Validator.check_schema(schema)
        self.assertFalse(schema["additionalProperties"])
        self.assertEqual(31, schema["properties"]["mutations"]["minItems"])
        self.assertEqual(31, schema["properties"]["mutations"]["maxItems"])

    def test_binding_validator_has_stable_rejection_codes(self):
        value = {
            "jobId": "job",
            "expectedJobId": "job",
            "summaryJobId": "job",
            "expectedSummaryJobId": "job",
            "manifestSha256": "a" * 64,
            "expectedManifestSha256": "a" * 64,
            "bundleFilePaths": ["summary.json", "transcript-00.ndjson"],
            "expectedBundleFilePaths": [
                "summary.json",
                "transcript-00.ndjson",
            ],
            "repositoryInventorySha256": "b" * 64,
            "expectedRepositoryInventorySha256": "b" * 64,
            "transcriptLengthBytes": 100,
            "expectedTranscriptLengthBytes": 100,
            "transcriptSha256": "c" * 64,
            "expectedTranscriptSha256": "c" * 64,
            "firstFrameSha256": "d" * 64,
            "expectedFirstFrameSha256": "d" * 64,
        }
        MODULE._verify_binding(value)
        mutations = (
            ("jobId", "other", "RBWP13F_JOB_IDENTITY"),
            ("summaryJobId", "other", "RBWP13F_SUMMARY_IDENTITY"),
            ("manifestSha256", "0" * 64, "RBWP13F_MANIFEST_HASH"),
            (
                "bundleFilePaths",
                ["summary.json", "transcript-00.ndjson", "extra.bin"],
                "RBWP13F_BUNDLE_FILE_INVENTORY",
            ),
            (
                "repositoryInventorySha256",
                "0" * 64,
                "RBWP13F_SOURCE_INVENTORY",
            ),
            ("transcriptLengthBytes", 99, "RBWP13F_TRANSCRIPT_TRUNCATED"),
            ("transcriptSha256", "0" * 64, "RBWP13F_TRANSCRIPT_HASH"),
            (
                "firstFrameSha256",
                "0" * 64,
                "RBWP13F_TRANSCRIPT_FRAME_HASH",
            ),
        )
        for field, replacement, code in mutations:
            mutated = dict(value)
            mutated[field] = replacement
            with self.assertRaisesRegex(MODULE.BindingRejection, code):
                MODULE._verify_binding(mutated)

    def test_rejection_classifier_is_typed_and_rejects_generic_failures(self):
        self.assertEqual(
            "RBWP13F_SELECTED_BINDING",
            MODULE._classify_rejection(
                "epoch 1 candidatePortfolio.selectedCandidateIds[1] differs"
            ),
        )
        self.assertEqual(
            "RBWP13F_SELECTED_UNIQUE",
            MODULE._classify_rejection(
                "candidatePortfolio.selectedCandidateIds must contain unique "
                "non-empty strings"
            ),
        )
        with self.assertRaisesRegex(RuntimeError, "unclassified"):
            MODULE._classify_rejection("generic crash")

    def test_binding_shape_rejects_extra_fields(self):
        with self.assertRaisesRegex(
            MODULE.BindingRejection,
            "RBWP13F_BINDING_SHAPE",
        ):
            MODULE._verify_binding({"invented": True})


if __name__ == "__main__":
    unittest.main()
