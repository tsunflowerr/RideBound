import copy
import hashlib
import importlib.util
import pathlib
import tempfile
import unittest

import jsonschema


ROOT = pathlib.Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location(
    "wp13_first_divergence_records",
    ROOT / "wp13_first_divergence_records.py",
)
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


def _arm_fields(arm, epoch=2):
    return {
        f"{arm}EpochId": epoch,
        f"{arm}SimTimeMs": 2_000,
        f"{arm}ObservedInputProjectionSha256": "1" * 64,
        f"{arm}OperationalDecisionProjectionSha256": (
            "2" * 64 if arm == "b1" else "3" * 64
        ),
        f"{arm}WireDecisionProjectionSha256": (
            "4" * 64 if arm == "b1" else "5" * 64
        ),
        f"{arm}EventTypes": ["requestArrived"],
        f"{arm}ActionTypes": ["requestAccepted"],
    }


def _divergence(classification="operationalDecisionDivergenceOnEqualObservedInput"):
    if classification == "noneObserved":
        return {"classification": classification, "observedInputEqual": True}
    value = {
        "classification": classification,
        "observedInputEqual": classification
        == "operationalDecisionDivergenceOnEqualObservedInput",
        **_arm_fields("b1"),
        **_arm_fields("c1"),
    }
    if classification == "observedInputDivergence":
        value["c1ObservedInputProjectionSha256"] = "9" * 64
    if classification == "transcriptLengthDivergence":
        value.update(
            {
                "c1EpochId": None,
                "c1SimTimeMs": None,
                "c1ObservedInputProjectionSha256": None,
                "c1OperationalDecisionProjectionSha256": None,
                "c1WireDecisionProjectionSha256": None,
                "c1EventTypes": [],
                "c1ActionTypes": [],
            }
        )
    return value


def _pair(panel_id, index, classification=None):
    unit = f"d20181114-s10-r{index}"
    kind = "p" if panel_id == "A" else "pb"
    actual_classification = (
        classification
        or "operationalDecisionDivergenceOnEqualObservedInput"
    )
    return {
        "unitId": unit,
        "sourceScenarioContentSha256": "a" * 64,
        "b1Label": f"{kind}-{unit}-b1-tight-s7",
        "c1Label": f"{kind}-{unit}-c1-tight-s7",
        "equalObservedDecisionEpochCountBeforeDivergence": 1,
        "stateHashMismatchBeforeDivergence": True,
        "firstStateHashMismatchEpoch": 1,
        "wireOnlyDifferenceBeforeDivergence": False,
        "firstWireOnlyDifferenceEpoch": None,
        "firstDivergence": _divergence(actual_classification),
    }


def _panel(panel_id):
    pairs = [_pair(panel_id, index) for index in range(1, 21)]
    return {
        "panelId": panel_id,
        "bundleCount": 60 if panel_id == "A" else 40,
        "primaryPairCount": 20,
        "nonPrimaryBundleCount": 20 if panel_id == "A" else 0,
        "declaredBundleInventorySha256": MODULE._PANEL_INVENTORIES[panel_id],
        "evidenceCoverage": {"unusedByProjection": True},
        "primaryAlignment": {
            "classificationCounts": {
                "operationalDecisionDivergenceOnEqualObservedInput": 20
            },
            "stateHashMismatchBeforeDivergencePairCount": 20,
            "pairs": pairs,
        },
    }


def _report():
    return {
        "schemaVersion": "1.0.0",
        "reportType": "ridebound-wp13-h6-evidence-inventory-v1",
        "toolIdentity": {
            "analyzerSourceSha256": MODULE._SOURCE_ANALYZER_SHA256,
            "solverEvidenceVerifierSourceSha256": MODULE._SOLVER_VERIFIER_SHA256,
        },
        "claimBoundary": {
            "analysisClass": "postOutcomeExploratory",
            "alignment": "equalObservedInputNotFullInternalState",
            "downstreamInterpretation": "trajectoryAssociatedNotCausal",
            "h6Artifacts": "readOnlyImmutableInputs",
            "confirmatoryGate": None,
        },
        "panels": [_panel("A"), _panel("B")],
    }


class RecordProjectionTests(unittest.TestCase):
    def test_valid_record_set_has_40_schema_validated_canonical_records(self):
        schema, schema_hash = MODULE._load_schema()

        result = MODULE.build_record_set(_report(), schema_hash, "b" * 64)

        jsonschema.Draft202012Validator(schema).validate(result)
        self.assertEqual(40, result["recordCount"])
        self.assertEqual({"A": 20, "B": 20}, result["panelRecordCounts"])
        self.assertEqual(
            40,
            result["classificationCounts"][
                "operationalDecisionDivergenceOnEqualObservedInput"
            ],
        )
        self.assertNotIn(b"null", MODULE._canonical(result))
        self.assertEqual(
            [("A", "d20181114-s10-r1"), ("A", "d20181114-s10-r10")],
            [
                (record["panelId"], record["unitId"])
                for record in result["records"][:2]
            ],
        )

    def test_classification_projection_has_explicit_input_relation(self):
        operational = MODULE._project_divergence(_divergence())
        observed = MODULE._project_divergence(
            _divergence("observedInputDivergence")
        )
        length = MODULE._project_divergence(
            _divergence("transcriptLengthDivergence")
        )
        none = MODULE._project_divergence(_divergence("noneObserved"))

        self.assertEqual("equal", operational["observedInputRelation"])
        self.assertEqual("different", observed["observedInputRelation"])
        self.assertEqual("notComparable", length["observedInputRelation"])
        self.assertIn("b1Evidence", length)
        self.assertNotIn("c1Evidence", length)
        self.assertEqual("equalThroughTranscript", none["observedInputRelation"])
        self.assertNotIn("b1Evidence", none)

    def test_relation_contradiction_fails_closed(self):
        value = _divergence()
        value["observedInputEqual"] = False

        with self.assertRaisesRegex(RuntimeError, "contradicts classification"):
            MODULE._project_divergence(value)

        value = _divergence()
        value["c1OperationalDecisionProjectionSha256"] = (
            value["b1OperationalDecisionProjectionSha256"]
        )
        with self.assertRaisesRegex(RuntimeError, "operational divergence"):
            MODULE._project_divergence(value)

    def test_divergence_epoch_must_follow_the_equal_prefix(self):
        pair = _pair("A", 1)
        pair["equalObservedDecisionEpochCountBeforeDivergence"] = 2

        with self.assertRaisesRegex(RuntimeError, "epoch differs from prefix"):
            MODULE._project_record(
                "A",
                MODULE._PANEL_INVENTORIES["A"],
                pair,
                {
                    "recordSetSchemaSha256": "a" * 64,
                    "generatorSourceSha256": "b" * 64,
                },
            )

    def test_duplicate_pair_and_panel_inventory_mutations_fail_closed(self):
        _, schema_hash = MODULE._load_schema()
        duplicate = _report()
        duplicate["panels"][0]["primaryAlignment"]["pairs"][1] = copy.deepcopy(
            duplicate["panels"][0]["primaryAlignment"]["pairs"][0]
        )
        with self.assertRaisesRegex(RuntimeError, "duplicate paired unit"):
            MODULE.build_record_set(duplicate, schema_hash, "b" * 64)

        inventory = _report()
        inventory["panels"][1]["declaredBundleInventorySha256"] = "0" * 64
        with self.assertRaisesRegex(RuntimeError, "frozen inventory differs"):
            MODULE.build_record_set(inventory, schema_hash, "b" * 64)

    def test_claim_boundary_mutation_fails_closed(self):
        _, schema_hash = MODULE._load_schema()
        report = _report()
        report["claimBoundary"]["downstreamInterpretation"] = "causal"

        with self.assertRaisesRegex(RuntimeError, "claim boundary differs"):
            MODULE.build_record_set(report, schema_hash, "b" * 64)


class SchemaAndReceiptTests(unittest.TestCase):
    def test_schema_accepts_each_classification_shape(self):
        schema, _ = MODULE._load_schema()
        record_schema = {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "$ref": "#/$defs/record",
            "$defs": schema["$defs"],
        }
        validator = jsonschema.Draft202012Validator(record_schema)
        contract = {
            "recordSetSchemaSha256": "a" * 64,
            "generatorSourceSha256": "b" * 64,
        }
        for classification in MODULE._CLASSIFICATIONS:
            pair = _pair("A", 1, classification)
            record = MODULE._project_record(
                "A",
                MODULE._PANEL_INVENTORIES["A"],
                pair,
                contract,
            )
            validator.validate(record)

    def test_schema_rejects_missing_conditional_epoch(self):
        schema, schema_hash = MODULE._load_schema()
        result = MODULE.build_record_set(_report(), schema_hash, "b" * 64)
        del result["records"][0]["firstStateHashMismatchEpoch"]

        with self.assertRaises(jsonschema.ValidationError):
            jsonschema.Draft202012Validator(schema).validate(result)

    def test_source_report_reader_rejects_unbound_canonical_json(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = pathlib.Path(temporary) / "inventory.json"
            payload = MODULE._canonical(_report()) + b"\n"
            path.write_bytes(payload)

            with self.assertRaisesRegex(RuntimeError, "identity differs"):
                MODULE._read_source_report(path)

    def test_contract_hashes_bind_exact_source_files(self):
        _, schema_hash = MODULE._load_schema()
        generator_hash = MODULE._sha256(ROOT / "wp13_first_divergence_records.py")

        self.assertRegex(schema_hash, r"^[0-9a-f]{64}$")
        self.assertRegex(generator_hash, r"^[0-9a-f]{64}$")
        self.assertEqual(
            MODULE._SOURCE_REPORT_SHA256,
            MODULE._source_identity()["sha256"],
        )

    def test_output_cannot_overwrite_source_report(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = pathlib.Path(temporary)
            source = root / "inventory.json"
            with self.assertRaisesRegex(RuntimeError, "must not overwrite"):
                MODULE._require_output_outside_inputs(source, source, [root])

            with self.assertRaisesRegex(RuntimeError, "outside every immutable"):
                MODULE._require_output_outside_inputs(
                    root / "derived.json",
                    root / "source.json",
                    [root],
                )


if __name__ == "__main__":
    unittest.main()
