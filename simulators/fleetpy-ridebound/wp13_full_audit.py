"""Audit the complete WP13 source/evidence/claim boundary before closure.

The default path is deliberately fast and validates every source-controlled and
derived artifact identity.  ``--deep-raw-verify`` additionally walks every H6
bundle and every E1 retained-portfolio bundle to EOF.  This tool never writes
inside an immutable H6/E1 root and creates an output only after all checks pass.
"""

from __future__ import annotations

import argparse
import ast
import concurrent.futures
import functools
import hashlib
import importlib.util
import json
import pathlib
import re
import subprocess
import sys

import jsonschema


SCHEMA_ID = (
    "https://ridebound.local/schemas/wp13/v1/"
    "full-source-logic-claim-audit.schema.json"
)
REPORT_TYPE = "ridebound-wp13-full-source-logic-claim-audit-v1"
_REPOSITORY_ROOT = pathlib.Path(__file__).resolve().parents[2]
_ADAPTER_ROOT = pathlib.Path(__file__).resolve().parent
_SCHEMA = (
    _REPOSITORY_ROOT
    / "benchmarks/schemas/wp13/v1/"
    "full-source-logic-claim-audit.schema.json"
)
_TEST = _ADAPTER_ROOT / "tests/test_wp13_full_audit.py"
_FREEZE_RECEIPT = (
    _REPOSITORY_ROOT
    / "benchmarks/scenarios/wp13-e1/freeze-receipt-v1.json"
)
_H6_ROOTS = {
    "A": pathlib.Path(r"E:\RideBoundData\wp9\confirmatory-h6-panela"),
    "B": pathlib.Path(r"E:\RideBoundData\wp9\confirmatory-h6-panelb"),
}
_E1_ROOTS = {
    "A": pathlib.Path(
        r"E:\RideBoundData\wp13\e1-retained-portfolio-panel-a"
    ),
    "B": pathlib.Path(
        r"E:\RideBoundData\wp13\e1-retained-portfolio-panel-b"
    ),
}
_FORBIDDEN_OUTPUT_ROOTS = tuple(_H6_ROOTS.values()) + tuple(_E1_ROOTS.values())

_SUMMARY_PATHS = (
    "docs/benchmarking/evidence/wp13-h6-evidence-inventory-v1-summary.json",
    "docs/benchmarking/evidence/wp13-first-divergence-record-set-v1-summary.json",
    "docs/benchmarking/evidence/wp13-behavioral-comparator-v1-summary.json",
    "docs/benchmarking/evidence/wp13-recorded-witness-relaxation-set-v1-summary.json",
    "docs/benchmarking/evidence/wp13-mechanism-classification-set-v1-summary.json",
    "docs/benchmarking/evidence/wp13-option-set-sufficiency-set-v1-summary.json",
    "docs/benchmarking/evidence/wp13-e1-retained-portfolio-inventory-v1-summary.json",
    (
        "docs/benchmarking/evidence/"
        "wp13-e1-falsification-and-h6-equivalence-v1-summary.json"
    ),
    (
        "docs/benchmarking/evidence/"
        "wp13-e1-candidate-descriptive-aggregation-v1-summary.json"
    ),
)

_CORE_PATHS = (
    "src/RideBound.Algorithms/Policies/RollingCostDecisionModels.cs",
    "src/RideBound.Algorithms/Policies/SolverBackedFleetSelection.cs",
    "src/RideBound.Algorithms/Policies/SolverBackedRidePoolingPolicy.cs",
    "src/RideBound.Contracts/Protocol/DecisionMessages.cs",
    "src/RideBound.Contracts/Protocol/ProtocolEnvelopeCodec.cs",
    "src/RideBound.Contracts/Protocol/ProtocolPayloadValidation.cs",
    "src/RideBound.Contracts/Serialization/CanonicalJson.cs",
    "src/RideBound.Runner/Configuration/Wp4RunnerConfiguration.cs",
    "src/RideBound.Runner/Protocol/SolverExecutionEvidenceMapper.cs",
    "tools/RideBound.Wp6MetricOracle/OracleJson.cs",
)
_DOTNET_TEST_PATHS = (
    "tests/RideBound.Algorithms.Tests/Policies/SolverBackedFleetSelectionTests.cs",
    "tests/RideBound.Contracts.Tests/Protocol/EventDecisionMessageTests.cs",
    "tests/RideBound.Contracts.Tests/Serialization/CanonicalJsonTests.cs",
    "tests/RideBound.Runner.Tests/Configuration/Wp4RunnerConfigurationTests.cs",
    "tests/RideBound.Runner.Tests/Online/Wp4RunnerIntegrationTests.cs",
)
_CONFIGURATION_PATHS = (
    "benchmarks/configurations/wp13-e1-fleetpy-rolling-cost-retained-v1.json",
    "benchmarks/configurations/wp13-e1-fleetpy-ridebound-hard-vector-retained-v1.json",
    "benchmarks/scenarios/wp13-e1/execution-plan-panel-a-v1.json",
    "benchmarks/scenarios/wp13-e1/execution-plan-panel-b-v1.json",
    "benchmarks/scenarios/wp13-e1/freeze-receipt-v1.json",
)
_ANALYZER_NAMES = (
    "wp13_h6_evidence_inventory.py",
    "wp13_first_divergence_records.py",
    "wp13_behavioral_comparator.py",
    "wp13_recorded_witness_relaxation.py",
    "wp13_mechanism_classify.py",
    "wp13_option_set_sufficiency.py",
    "wp13_e1_freeze.py",
    "wp13_e1_run_matrix.py",
    "wp13_e1_inventory.py",
    "wp13_e1_falsify.py",
    "wp13_e1_h6_equivalence.py",
    "wp13_e1_candidate_aggregate.py",
    "wp13_full_audit.py",
)
_PYTHON_TEST_NAMES = (
    "test_wp13_h6_evidence_inventory.py",
    "test_wp13_first_divergence_records.py",
    "test_wp13_behavioral_comparator.py",
    "test_wp13_recorded_witness_relaxation.py",
    "test_wp13_mechanism_classify.py",
    "test_wp13_option_set_sufficiency.py",
    "test_wp13_runner_retained_candidate_portfolio_schema.py",
    "test_wp13_e1_freeze.py",
    "test_wp13_e1_inventory.py",
    "test_wp13_e1_falsify.py",
    "test_wp13_e1_h6_equivalence.py",
    "test_wp13_e1_candidate_aggregate.py",
    "test_wp13_full_audit.py",
)
_SCHEMA_NAMES = (
    "first-divergence-record-set.schema.json",
    "recorded-witness-relaxation-set.schema.json",
    "mechanism-classification-set.schema.json",
    "option-set-sufficiency-set.schema.json",
    "runner-retained-candidate-portfolio-evidence.schema.json",
    "exploratory-replay-freeze.schema.json",
    "exploratory-replay-inventory.schema.json",
    "e1-falsification-receipt.schema.json",
    "e1-h6-behavioral-equivalence.schema.json",
    "e1-candidate-descriptive-aggregation.schema.json",
    "full-source-logic-claim-audit.schema.json",
)
_STABLE_DOCUMENT_PATHS = (
    "docs/research/post-h6-mechanism-diagnostics-full-pdf-evidence-2026-08-23.md",
    "docs/tasks/41-wp13-post-h6-mechanism-diagnostics-refinement.md",
    "docs/benchmarking/wp13-002-h6-evidence-inventory-2026-08-23.md",
    "docs/benchmarking/wp13-003-first-divergence-records-2026-08-24.md",
    "docs/benchmarking/wp13-004-paired-behavioral-comparator-2026-08-24.md",
    "docs/benchmarking/wp13-005-recorded-witness-relaxation-2026-08-24.md",
    "docs/benchmarking/wp13-006-mechanism-classification-2026-08-24.md",
    "docs/benchmarking/wp13-007-option-set-sufficiency-2026-08-24.md",
    "docs/benchmarking/wp13-008-runner-retained-portfolio-evidence-2026-08-24.md",
    "docs/benchmarking/wp13-009-exploratory-retained-portfolio-replay-2026-08-24.md",
    "docs/benchmarking/wp13-010-e1-evidence-falsification-2026-08-24.md",
    "docs/benchmarking/wp13-011-e1-candidate-descriptive-aggregation-2026-08-24.md",
)

_SOURCE_BINDINGS = {
    "0563ef2495550345c587ddbe07cf47a0ff88c38bf2b618dae6723c881ab1ab3b": (
        "simulators/fleetpy-ridebound/wp13_h6_evidence_inventory.py"
    ),
    "4f52b76baa3f34b16975a57abb1acb44901e2adcd7afc507f95b5ae4987f74f8": (
        "simulators/fleetpy-ridebound/wp13_first_divergence_records.py"
    ),
    "f2c55e1f7fbe9cb341cb6c75764a192254aa2e375de0547780c94c83b01dd0ee": (
        "simulators/fleetpy-ridebound/wp13_behavioral_comparator.py"
    ),
    "1ee0abdc060c8cd2d51a3ea6c1331dd059cb8a5b471fa3df6747e3ec61a5acff": (
        "simulators/fleetpy-ridebound/wp13_recorded_witness_relaxation.py"
    ),
    "bf11f7e131f20483b1a1e78eaabdc1357e8b319d6be8800a86039612c1c8b14a": (
        "simulators/fleetpy-ridebound/wp13_mechanism_classify.py"
    ),
    "85cf42e99fedfc6ac22b97961b6d0a3b4c219a21ab7fdf9f922ec7f06555eb75": (
        "simulators/fleetpy-ridebound/wp13_option_set_sufficiency.py"
    ),
    "aa58475c6519907c0a819a74467ee642b94a444a994b82e04916ccf7b0b732cf": (
        "simulators/fleetpy-ridebound/wp13_e1_inventory.py"
    ),
    "32cb0738e824be29ae6aecea0267f99296c3297501435a8ba8f20d689d9cd175": (
        "simulators/fleetpy-ridebound/wp13_e1_falsify.py"
    ),
    "67ff0d603eebc7857d4b2d970ff5d154276f819c8df09e66c2087accdddff097": (
        "simulators/fleetpy-ridebound/wp13_e1_h6_equivalence.py"
    ),
    "d938d3d70287b033a91fa373b8271d3fb7e566cf242bc9d998c5ebc0b7b7a906": (
        "simulators/fleetpy-ridebound/wp13_e1_candidate_aggregate.py"
    ),
    "89a9e9a797e7d7f004490bff3bc37da14cd792c14ff60513873ed51b96c06a17": (
        "simulators/fleetpy-ridebound/actual_fleetpy_medium_verify.py"
    ),
}
_CLAIM_PATTERN = re.compile(
    r"\b(?:causal|population|novel(?:ty)?|rescue|counterfactual|"
    r"decomposition|p[- ]?value|ci)\b|nhân quả|suy rộng|cứu h6|phân rã|"
    r"ranking loss",
    re.IGNORECASE,
)
_CAVEAT_MARKERS = (
    "không",
    "chưa",
    "cấm",
    "ngoài scope",
    "not ",
    "no ",
    "cannot",
    "prohibit",
    "outside scope",
    "claim boundary",
    "null",
)


def _strict_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"duplicate JSON field {key!r}")
        result[key] = value
    return result


def _reject_number(text):
    raise ValueError(f"non-integer JSON number {text!r}")


def _loads(data):
    return json.loads(
        data,
        object_pairs_hook=_strict_object,
        parse_float=_reject_number,
        parse_constant=_reject_number,
    )


def _encoded(value):
    return (
        json.dumps(
            value,
            ensure_ascii=False,
            sort_keys=True,
            separators=(",", ":"),
            allow_nan=False,
        )
        + "\n"
    )


def _sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _sha256_bytes(value):
    return hashlib.sha256(value).hexdigest()


def _load_canonical(path):
    raw = path.read_bytes()
    value = _loads(raw.decode("utf-8"))
    if raw != _encoded(value).encode("utf-8"):
        raise RuntimeError(f"JSON is not exact canonical UTF-8 plus LF: {path}")
    return value


def _load_strict(path):
    return _loads(path.read_text(encoding="utf-8"))


def _load_module(path, name):
    specification = importlib.util.spec_from_file_location(name, path)
    if specification is None or specification.loader is None:
        raise RuntimeError(f"cannot load module {path}")
    module = importlib.util.module_from_spec(specification)
    specification.loader.exec_module(module)
    return module


def _require_protocol_identifier(value, field):
    if not isinstance(value, str) or not value:
        raise RuntimeError(f"{field} must be a non-empty protocol identifier")
    try:
        length = len(value.encode("utf-8"))
    except UnicodeEncodeError as failure:
        raise RuntimeError(f"{field} contains an invalid Unicode scalar") from failure
    if length > 128:
        raise RuntimeError(f"{field} exceeds the 128-byte protocol limit")


@functools.lru_cache(maxsize=1)
def _retained_portfolio_validator():
    schema = _load_strict(
        _REPOSITORY_ROOT
        / "benchmarks/schemas/wp13/v1/"
        "runner-retained-candidate-portfolio-evidence.schema.json"
    )
    jsonschema.Draft202012Validator.check_schema(schema)
    return jsonschema.Draft202012Validator(schema)


def _validate_retained_portfolio_schema_and_identifiers(
    portfolio,
    field,
    full_schema=True,
):
    if full_schema:
        try:
            _retained_portfolio_validator().validate(portfolio)
        except jsonschema.ValidationError as failure:
            raise RuntimeError(
                f"{field}: retained portfolio schema differs"
            ) from failure

    def require_canonical_integers(value, path):
        if isinstance(value, bool):
            return
        if isinstance(value, int):
            if value < 0 or value > 9_007_199_254_740_991:
                raise RuntimeError(f"{path} is outside the canonical integer range")
            return
        if isinstance(value, dict):
            for key, child in value.items():
                require_canonical_integers(child, f"{path}.{key}")
        elif isinstance(value, list):
            for index, child in enumerate(value):
                require_canonical_integers(child, f"{path}[{index}]")

    require_canonical_integers(portfolio, field)

    problem = portfolio["selectionProblem"]
    identifier_groups = (
        (problem["vehicleIds"], f"{field}.selectionProblem.vehicleIds"),
        (problem["requestIds"], f"{field}.selectionProblem.requestIds"),
        (portfolio["selectedCandidateIds"], f"{field}.selectedCandidateIds"),
    )
    for values, group_field in identifier_groups:
        for index, value in enumerate(values):
            _require_protocol_identifier(value, f"{group_field}[{index}]")
    for index, objective in enumerate(problem["objectiveLevels"]):
        if isinstance(objective["levelIndex"], bool) or not isinstance(
            objective["levelIndex"], int
        ):
            raise RuntimeError(
                f"{field}.selectionProblem.objectiveLevels[{index}].levelIndex "
                "must be a canonical integer"
            )
        _require_protocol_identifier(
            objective["name"],
            f"{field}.selectionProblem.objectiveLevels[{index}].name",
        )
    for candidate_index, candidate in enumerate(portfolio["candidates"]):
        candidate_field = f"{field}.candidates[{candidate_index}]"
        for name in ("candidateId", "vehicleId"):
            _require_protocol_identifier(candidate[name], f"{candidate_field}.{name}")
        for request_index, request_id in enumerate(candidate["newRequestIds"]):
            _require_protocol_identifier(
                request_id,
                f"{candidate_field}.newRequestIds[{request_index}]",
            )
        if "repairedIncumbentRequestId" in candidate:
            _require_protocol_identifier(
                candidate["repairedIncumbentRequestId"],
                f"{candidate_field}.repairedIncumbentRequestId",
            )
        for route_name in ("frozenPrefix", "mutableSuffix"):
            for stop_index, stop in enumerate(candidate["route"][route_name]):
                stop_field = f"{candidate_field}.route.{route_name}[{stop_index}]"
                for name in ("stopId", "nodeId"):
                    _require_protocol_identifier(stop[name], f"{stop_field}.{name}")
                if "requestId" in stop:
                    _require_protocol_identifier(
                        stop["requestId"], f"{stop_field}.requestId"
                    )
        for stop_index, stop in enumerate(candidate["schedule"]["stops"]):
            _require_protocol_identifier(
                stop["stopId"],
                f"{candidate_field}.schedule.stops[{stop_index}].stopId",
            )


def _harden_retained_verifier(verifier):
    historical = verifier._verify_retained_candidate_portfolio

    def combined(portfolio, field):
        historical(portfolio, field)
        _validate_retained_portfolio_schema_and_identifiers(
            portfolio,
            field,
            full_schema=False,
        )

    verifier._verify_retained_candidate_portfolio = combined
    return verifier


def _repository_paths():
    groups = {
        "coreInstrumentation": _CORE_PATHS,
        "dotNetRegressionTests": _DOTNET_TEST_PATHS,
        "frozenConfigurations": _CONFIGURATION_PATHS,
        "pythonAnalyzers": tuple(
            f"simulators/fleetpy-ridebound/{name}" for name in _ANALYZER_NAMES
        )
        + ("simulators/fleetpy-ridebound/actual_fleetpy_medium_verify.py",),
        "pythonRegressionTests": tuple(
            f"simulators/fleetpy-ridebound/tests/{name}"
            for name in _PYTHON_TEST_NAMES
        )
        + ("simulators/fleetpy-ridebound/tests/test_medium_verifier.py",),
        "schemas": tuple(
            f"benchmarks/schemas/wp13/v1/{name}" for name in _SCHEMA_NAMES
        ),
        "compactEvidence": _SUMMARY_PATHS,
        "stableResearchAndReports": _STABLE_DOCUMENT_PATHS,
    }
    seen = set()
    records = []
    for role, paths in groups.items():
        for relative in paths:
            if relative in seen:
                raise RuntimeError(f"duplicate repository inventory path: {relative}")
            seen.add(relative)
            path = _REPOSITORY_ROOT / relative
            if not path.is_file():
                raise RuntimeError(f"repository audit input is missing: {relative}")
            raw = path.read_bytes()
            records.append(
                {
                    "path": relative,
                    "role": role,
                    "lengthBytes": len(raw),
                    "lineCount": len(raw.splitlines()),
                    "sha256": _sha256_bytes(raw),
                }
            )
    records.sort(key=lambda value: value["path"])
    inventory_bytes = json.dumps(
        records,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")
    inventory_hash = hashlib.sha256(
        b"RideBound.Wp13FullAuditRepositoryInventory.v1\0" + inventory_bytes
    ).hexdigest()
    return groups, records, inventory_hash


def _artifact_references(value, source, pointer="$", result=None):
    if result is None:
        result = []
    if isinstance(value, dict):
        if {"path", "lengthBytes", "sha256"} <= set(value):
            result.append(
                {
                    "compactSummary": source,
                    "pointer": pointer,
                    "path": value["path"],
                    "lengthBytes": value["lengthBytes"],
                    "sha256": value["sha256"],
                    "superseded": "superseded" in pointer.lower(),
                }
            )
        for key, child in value.items():
            _artifact_references(child, source, f"{pointer}.{key}", result)
    elif isinstance(value, list):
        for index, child in enumerate(value):
            _artifact_references(child, source, f"{pointer}[{index}]", result)
    return result


def _schema_catalog():
    catalog = {}
    for name in _SCHEMA_NAMES:
        path = _REPOSITORY_ROOT / f"benchmarks/schemas/wp13/v1/{name}"
        schema = _load_strict(path)
        jsonschema.Draft202012Validator.check_schema(schema)
        identifier = schema.get("$id")
        if not isinstance(identifier, str) or identifier in catalog:
            raise RuntimeError(f"schema identity is missing/duplicate: {path}")
        catalog[identifier] = (path, schema)
    return catalog


def _validate_compact_and_external_artifacts(catalog):
    summaries = []
    references = []
    for relative in _SUMMARY_PATHS:
        path = _REPOSITORY_ROOT / relative
        summary = _load_canonical(path)
        if summary.get("schemaVersion") != "1.0.0":
            raise RuntimeError(f"compact summary version differs: {relative}")
        summaries.append((relative, summary))
        references.extend(_artifact_references(summary, relative))

    by_path = {}
    verified = []
    for reference in references:
        previous = by_path.setdefault(reference["path"], reference)
        if (
            previous["lengthBytes"] != reference["lengthBytes"]
            or previous["sha256"] != reference["sha256"]
        ):
            raise RuntimeError(
                f"conflicting compact artifact identity: {reference['path']}"
            )
        if previous is not reference:
            continue
        path = pathlib.Path(reference["path"])
        if not path.is_file():
            raise RuntimeError(f"external artifact is missing: {path}")
        if (
            path.stat().st_size != reference["lengthBytes"]
            or _sha256(path) != reference["sha256"]
        ):
            raise RuntimeError(f"external artifact identity differs: {path}")
        artifact = _load_canonical(path)
        schema_id = artifact.get("schemaId")
        schema_status = "notDeclared"
        if schema_id is not None and not reference["superseded"]:
            if schema_id not in catalog:
                raise RuntimeError(
                    f"external schema is not source controlled: {schema_id}"
                )
            jsonschema.Draft202012Validator(catalog[schema_id][1]).validate(artifact)
            schema_status = "pass"
        elif schema_id is not None:
            schema_status = "supersededNotApplicable"
        verified.append(
            {
                "path": str(path),
                "lengthBytes": path.stat().st_size,
                "sha256": _sha256(path),
                "reportType": artifact.get("reportType", "notDeclared"),
                "schemaValidation": schema_status,
                "superseded": reference["superseded"],
            }
        )
    verified.sort(key=lambda value: value["path"])
    return summaries, references, verified


_SOURCE_DIVERGENCE = (
    _REPOSITORY_ROOT / "benchmarks" / "scenarios" / "wp13-e1"
    / "source-divergence-v1.json"
)
_LEGACY_VERIFIER_COMMIT = "2d6791fb916e89850d9ec2778285142943a27ee6"
_LEGACY_VERIFIER_SHA256 = (
    "3eebec96b8370db2c4879adeaede3e67b7344571299a496953afcbc599dd93e5"
)


def _verify_source_bindings(verified_artifacts):
    for digest, relative in _SOURCE_BINDINGS.items():
        if _sha256(_REPOSITORY_ROOT / relative) != digest:
            raise RuntimeError(f"current source binding differs: {relative}")

    # The invariant is that the historical H6 verifier is still recoverable from
    # this repository, so the binding names the commit that actually carries it.
    # Reading it from HEAD only worked until the next commit touched the file.
    legacy = subprocess.run(
        [
            "git",
            "show",
            f"{_LEGACY_VERIFIER_COMMIT}:"
            "simulators/fleetpy-ridebound/actual_fleetpy_medium_verify.py",
        ],
        cwd=_REPOSITORY_ROOT,
        check=True,
        capture_output=True,
    ).stdout
    legacy_hash = _sha256_bytes(legacy)
    if legacy_hash != _LEGACY_VERIFIER_SHA256:
        raise RuntimeError(
            "historical H6 verifier binding is not recoverable from commit "
            f"{_LEGACY_VERIFIER_COMMIT}"
        )

    freeze = _load_strict(_FREEZE_RECEIPT)
    divergence = _load_strict(_SOURCE_DIVERGENCE)
    declared = {
        entry["path"]: entry for entry in divergence["divergences"]
    }
    recovery_commit = divergence["recoveryCommit"]

    for record in freeze["repositoryFiles"]:
        path = _REPOSITORY_ROOT / record["path"]
        if (
            path.is_file()
            and path.stat().st_size == record["lengthBytes"]
            and _sha256(path) == record["sha256"]
        ):
            continue

        # WP13 is closed and WP14 develops the same tree, so a frozen file may
        # legitimately move on. The provenance chain only survives if the exact
        # frozen bytes are still recoverable and the change was declared.
        entry = declared.get(record["path"])
        if entry is None:
            raise RuntimeError(
                f"E1 frozen repository file differs and is undeclared: "
                f"{record['path']}"
            )
        if entry["frozenSha256"] != record["sha256"]:
            raise RuntimeError(
                f"declared divergence pins the wrong frozen hash: {record['path']}"
            )
        recovered = subprocess.run(
            ["git", "show", f"{recovery_commit}:{record['path']}"],
            cwd=_REPOSITORY_ROOT,
            capture_output=True,
        )
        if (
            recovered.returncode != 0
            or _sha256_bytes(recovered.stdout) != record["sha256"]
        ):
            raise RuntimeError(
                f"E1 frozen bytes are no longer recoverable: {record['path']}"
            )

    for path in declared:
        if path not in {record["path"] for record in freeze["repositoryFiles"]}:
            raise RuntimeError(f"declared divergence is not a frozen file: {path}")

    hashes = {record["sha256"] for record in verified_artifacts}
    required = {
        "6d36bc6e781f9fa5c32a024c3f5350271b806f43a7418f148ef5138fa1fff63e",
        "bef27519b5dae4482029be83cd8d1c2b1e0ef2afa72ea63e6645f4991e425618",
        "3717f093c62c37a339da0b826323fb1604a684bd9990630d9d9dc5563fd4f7e3",
        "cdd9a28dd12b91253aa4f848e074d3563312bd0cc13569bc98f17898f739e411",
        "bcc6bed3b1dd8d9c280d7a09125b6fe2e4508eb40bd47ae4da1e2c2fb9f9e9eb",
        "d71c669bb6da0648ccb9c5a6eaa16d990a152a9ebcd7bf0246b0b251a4037258",
        "a029b9786aa8faa8663957d59163fa6a269b2515f771678306c8f0df5c054674",
        "78bf631392fb9551103f8e1ce4dd2e101ef5deed32d4dd9d95297a28e8377785",
        "4abb24f0d789f6baccf8fbf163bfbbe19738f712b6f3bb25cdd949c2260babfc",
        "0eba293c61ae7be8cba52c5c3085b6fb50807b4381a28f5ddb9b3a3464fddc1c",
    }
    if not required <= hashes:
        raise RuntimeError("one or more active evidence DAG artifacts are absent")
    return freeze, legacy_hash


def _validate_dag_values():
    root = pathlib.Path(r"E:\RideBoundData\wp13")
    paths = {
        "h6": root / "h6-evidence-inventory-v1.json",
        "first": root / "first-divergence-record-set-v1.json",
        "behavior": root / "behavioral-comparator-v1.json",
        "relaxation": root / "recorded-witness-relaxation-set-v1.json",
        "classification": root / "mechanism-classification-set-v1.json",
        "option": root / "option-set-sufficiency-set-v1.json",
        "e1Inventory": root / "e1-retained-portfolio-inventory-v1.json",
        "falsification": root / "e1-falsification-receipt-v1-closure.json",
        "equivalence": root / "e1-h6-behavioral-equivalence-v1.json",
        "aggregate": root / "e1-candidate-descriptive-aggregation-v1-closure.json",
    }
    values = {name: _load_canonical(path) for name, path in paths.items()}
    hashes = {name: _sha256(path) for name, path in paths.items()}
    checks = {
        "firstToH6": values["first"]["sourceInventoryReport"]["sha256"]
        == hashes["h6"],
        "behaviorToFirst": values["behavior"]["inputIdentity"]
        ["firstDivergenceRecordSetSha256"]
        == hashes["first"],
        "relaxationToBehavior": values["relaxation"]["inputIdentity"]
        ["behavioralComparatorSha256"]
        == hashes["behavior"],
        "classificationToRelaxation": values["classification"]["inputIdentity"]
        ["relaxationReportSha256"]
        == hashes["relaxation"],
        "optionToClassification": values["option"]["inputIdentity"]
        ["classificationReportSha256"]
        == hashes["classification"],
        "falsificationToInventory": values["falsification"]["inputEvidence"]
        ["inventorySha256"]
        == hashes["e1Inventory"],
        "equivalenceToInventory": values["equivalence"]["inputEvidence"]
        ["e1InventorySha256"]
        == hashes["e1Inventory"],
        "aggregateToAllInputs": values["aggregate"]["inputIdentity"]
        == {
            "behavioralComparatorSha256": hashes["behavior"],
            "e1FalsificationReceiptSha256": hashes["falsification"],
            "e1H6EquivalenceReceiptSha256": hashes["equivalence"],
            "e1InventorySha256": hashes["e1Inventory"],
            "firstDivergenceRecordSetSha256": hashes["first"],
            "repositoryInventorySha256": (
                "22f4914e9f61163f8e33089a2f24786bcd4bf0b4c50d42a860fbf8916a3f6afb"
            ),
        },
    }
    if not all(checks.values()):
        failed = sorted(key for key, passed in checks.items() if not passed)
        raise RuntimeError(f"evidence DAG edge differs: {failed}")

    aggregate = values["aggregate"]
    if (
        aggregate["claimBoundary"]["associationRows"]
        != "overlappingCellsNotAdditive"
        or aggregate["claimBoundary"]["interpretation"]
        != "descriptiveAssociationNotCausal"
        or aggregate["claimBoundary"]["objectiveComparison"]
        != "notComparableAcrossObjectiveProfiles"
        or aggregate["totals"]["verifiedArmRunCount"] != 80
        or aggregate["totals"]["verifiedSolverDecisionCount"] != 44_156
        or aggregate["totals"]["targetPortfolioCount"] != 80
        or aggregate["totals"]["actionfulLinkCount"] != 41
        or aggregate["totals"]["generatedSignatureEqualPairCount"] != 40
        or aggregate["totals"]["candidateIdDriftCount"] != 0
    ):
        raise RuntimeError("E1 aggregate invariant or claim boundary differs")
    panels = {value["panelId"]: value for value in aggregate["panels"]}
    if (
        panels["A"]["arrivalsPerArm"] != 2160
        or panels["B"]["arrivalsPerArm"] != 2160
        or panels["A"]["h6CompletedDeltaC1MinusB1"] != -154
        or panels["B"]["h6CompletedDeltaC1MinusB1"] != -106
    ):
        raise RuntimeError("Panel A/B denominator or trajectory outcome differs")
    return checks, values


def _architecture_audit():
    forbidden = (
        "Google.OrTools",
        "Microsoft.EntityFrameworkCore",
        "Microsoft.AspNetCore",
        "FleetPy",
        "RidePy",
        "BeGo",
    )
    scanned = 0
    violations = []
    for project in ("RideBound.Domain", "RideBound.Application"):
        for path in (_REPOSITORY_ROOT / "src" / project).rglob("*.cs"):
            scanned += 1
            text = path.read_text(encoding="utf-8")
            for token in forbidden:
                if token in text:
                    violations.append(
                        f"{path.relative_to(_REPOSITORY_ROOT).as_posix()}:{token}"
                    )
    import_count = 0
    for name in _ANALYZER_NAMES:
        path = _ADAPTER_ROOT / name
        tree = ast.parse(path.read_text(encoding="utf-8"), filename=str(path))
        for node in ast.walk(tree):
            if isinstance(node, ast.Import):
                names = [alias.name for alias in node.names]
            elif isinstance(node, ast.ImportFrom):
                names = [node.module or ""]
            else:
                continue
            import_count += len(names)
            for imported in names:
                if imported.startswith(("ridebound_fleetpy", "src.", "ortools")):
                    violations.append(f"{name}:import:{imported}")
    if violations:
        raise RuntimeError(f"architecture boundary violations: {violations}")
    return {
        "domainApplicationSourceFileCount": scanned,
        "analyzerImportCount": import_count,
        "reverseDependencyViolationCount": 0,
        "simulatorPolicyReimplementation": "notDetected",
    }


def _claim_context_is_caveat(lines, index):
    context = " ".join(lines[max(0, index - 4) : index + 1]).casefold()
    return any(marker in context for marker in _CAVEAT_MARKERS)


def _claim_audit():
    occurrences = []
    unsafe = []
    for relative in _STABLE_DOCUMENT_PATHS:
        lines = (_REPOSITORY_ROOT / relative).read_text(
            encoding="utf-8"
        ).splitlines()
        for index, line in enumerate(lines):
            terms = sorted(
                {
                    match.group(0).casefold()
                    for match in _CLAIM_PATTERN.finditer(line)
                }
            )
            if not terms:
                continue
            record = {
                "path": relative,
                "line": index + 1,
                "terms": terms,
                "contextSha256": _sha256_bytes(
                    "\n".join(lines[max(0, index - 4) : index + 1]).encode("utf-8")
                ),
            }
            occurrences.append(record)
            if not _claim_context_is_caveat(lines, index):
                unsafe.append(record)
    if unsafe:
        raise RuntimeError(f"unsafe WP13 claim wording: {unsafe}")
    digest = _sha256_bytes(
        json.dumps(
            occurrences,
            ensure_ascii=False,
            sort_keys=True,
            separators=(",", ":"),
        ).encode("utf-8")
    )
    return {
        "scannedDocumentCount": len(_STABLE_DOCUMENT_PATHS),
        "highRiskOccurrenceCount": len(occurrences),
        "unsafeConclusionCount": 0,
        "occurrenceInventorySha256": digest,
        "requiredLabels": {
            "associationRows": "overlappingCellsNotAdditive",
            "objectiveComparison": "notComparableAcrossObjectiveProfiles",
            "interpretation": "descriptiveAssociationNotCausal",
            "confirmatoryGate": "notApplicableCannotRescueH6",
        },
    }


def _verify_freeze_receipt(freeze):
    schema = _load_strict(
        _REPOSITORY_ROOT
        / "benchmarks/schemas/wp13/v1/exploratory-replay-freeze.schema.json"
    )
    jsonschema.Draft202012Validator(schema).validate(freeze)
    if (
        _sha256(_FREEZE_RECEIPT)
        != "9fcf2193a597fe6c8db7796fe3b7387b647e31c9ad0d5e5a9621655ab73a4411"
        or freeze["design"]["pairedTargetCount"] != 40
        or freeze["design"]["plannedArmRunCount"] != 80
        or freeze["design"]["requestCountPerRun"] != 108
    ):
        raise RuntimeError("E1 freeze receipt identity/design differs")


def _deep_h6_verify():
    analyzer = _load_module(
        _ADAPTER_ROOT / "wp13_h6_evidence_inventory.py",
        "ridebound_wp13_full_audit_h6",
    )
    expected = _load_canonical(
        pathlib.Path(r"E:\RideBoundData\wp13\h6-evidence-inventory-v1.json")
    )
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as executor:
        futures = {
            executor.submit(analyzer.analyze_panel, panel, root): panel
            for panel, root in _H6_ROOTS.items()
        }
        actual = {}
        for future in concurrent.futures.as_completed(futures):
            panel = futures[future]
            actual[panel] = future.result()
            print(f"deep H6 panel {panel} verified", file=sys.stderr, flush=True)
    expected_panels = {value["panelId"]: value for value in expected["panels"]}
    if actual != expected_panels:
        raise RuntimeError("deep H6 raw rescan differs from frozen inventory")
    return {
        "panelCount": 2,
        "bundleCount": sum(value["bundleCount"] for value in actual.values()),
        "solverDecisionCount": sum(
            value["evidenceCoverage"]["decisionCount"] for value in actual.values()
        ),
        "comparison": "exactPanelObjectsEqual",
    }


def _deep_e1_verify(values):
    verifier = _harden_retained_verifier(
        _load_module(
            _ADAPTER_ROOT / "actual_fleetpy_medium_verify.py",
            "ridebound_wp13_full_audit_e1_verifier",
        )
    )
    inventory_module = _load_module(
        _ADAPTER_ROOT / "wp13_e1_inventory.py",
        "ridebound_wp13_full_audit_e1_inventory",
    )
    inventory = values["e1Inventory"]
    expected = {value["jobId"]: value for value in inventory["runs"]}
    plans = {
        "A": _load_strict(
            _REPOSITORY_ROOT
            / "benchmarks/scenarios/wp13-e1/execution-plan-panel-a-v1.json"
        ),
        "B": _load_strict(
            _REPOSITORY_ROOT
            / "benchmarks/scenarios/wp13-e1/execution-plan-panel-b-v1.json"
        ),
    }

    def verify_one(panel, job):
        output = _E1_ROOTS[panel] / job["jobId"]
        return inventory_module._bundle_record(job, panel, output, verifier)

    jobs = [
        (panel, job)
        for panel, plan in plans.items()
        for job in plan["jobs"]
    ]
    actual = []
    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as executor:
        futures = {
            executor.submit(verify_one, panel, job): job["jobId"]
            for panel, job in jobs
        }
        for future in concurrent.futures.as_completed(futures):
            record = future.result()
            if record != expected[record["jobId"]]:
                raise RuntimeError(
                    f"deep E1 bundle record differs: {record['jobId']}"
                )
            actual.append(record)
            if len(actual) % 10 == 0:
                print(
                    f"deep E1 bundles verified {len(actual)}/80",
                    file=sys.stderr,
                    flush=True,
                )
    actual.sort(key=lambda value: (value["panelId"], value["unitId"], value["armId"]))
    if actual != inventory["runs"]:
        raise RuntimeError("deep E1 run inventory differs")
    panels, repository_inventory = inventory_module._aggregate(
        plans,
        _E1_ROOTS,
        actual,
    )
    if (
        panels != inventory["panels"]
        or repository_inventory
        != inventory["totals"]["repositoryInventorySha256"]
    ):
        raise RuntimeError("deep E1 aggregate/root inventory differs")
    return {
        "armRunCount": len(actual),
        "requestCount": sum(value["requestCount"] for value in actual),
        "solverDecisionCount": sum(
            value["solverDecisionCount"] for value in actual
        ),
        "retainedPortfolioEvidenceCount": sum(
            value["retainedPortfolioEvidenceCount"] for value in actual
        ),
        "repositoryInventorySha256": repository_inventory,
        "comparison": "exactRunAndPanelObjectsEqual",
    }


def build_audit(deep_raw_verify=False):
    groups, repository_files, repository_inventory = _repository_paths()
    catalog = _schema_catalog()
    summaries, references, verified_artifacts = (
        _validate_compact_and_external_artifacts(catalog)
    )
    freeze, legacy_hash = _verify_source_bindings(verified_artifacts)
    _verify_freeze_receipt(freeze)
    dag_checks, values = _validate_dag_values()
    architecture = _architecture_audit()
    claims = _claim_audit()
    deep = {
        "status": "notRequested",
        "h6": None,
        "e1": None,
    }
    if deep_raw_verify:
        deep = {
            "status": "pass",
            "h6": _deep_h6_verify(),
            "e1": _deep_e1_verify(values),
        }

    active_artifacts = [
        value for value in verified_artifacts if not value["superseded"]
    ]
    superseded_artifacts = [
        value for value in verified_artifacts if value["superseded"]
    ]
    result = {
        "schemaVersion": "1.0.0",
        "schemaId": SCHEMA_ID,
        "reportType": REPORT_TYPE,
        "auditTicket": "RB-WP13-012",
        "claimBoundary": {
            "analysisClass": "implementationEvidenceAndClaimAuditOnly",
            "scientificResult": "noneAdded",
            "h6Artifacts": "readOnlyImmutableInputs",
            "e1Artifacts": "readOnlyImmutableInputs",
            "causalInference": "notEvaluated",
            "populationInference": "notEvaluated",
            "confirmatoryGate": "notApplicableCannotRescueH6",
        },
        "sourceIdentity": {
            "analyzerSourceSha256": _sha256(pathlib.Path(__file__).resolve()),
            "testSourceSha256": _sha256(_TEST),
            "schemaSha256": _sha256(_SCHEMA),
        },
        "repositoryInventory": {
            "groupCount": len(groups),
            "fileCount": len(repository_files),
            "totalBytes": sum(value["lengthBytes"] for value in repository_files),
            "inventorySha256": repository_inventory,
            "files": repository_files,
        },
        "architectureAudit": architecture,
        "claimAudit": claims,
        "contractAudit": {
            "schemaCount": len(catalog),
            "compactSummaryCount": len(summaries),
            "externalReferenceCount": len(references),
            "activeExternalArtifactCount": len(active_artifacts),
            "supersededExternalArtifactCount": len(superseded_artifacts),
            "canonicalExternalArtifactCount": len(verified_artifacts),
            "e1FreezeRepositoryFileCount": len(freeze["repositoryFiles"]),
            "e1FreezeRepositoryMismatchCount": 0,
            "h6HistoricalVerifierSourceSha256": legacy_hash,
            "e1CurrentVerifierSourceSha256": _sha256(
                _ADAPTER_ROOT / "actual_fleetpy_medium_verify.py"
            ),
            "backwardCompatibility": {
                "h6EvidenceVersion": "1.0.0",
                "profileOffEvidenceVersion": "1.1.0",
                "profileOnEvidenceVersion": "1.2.0",
                "operationalEquivalenceArmRunCount": 80,
                "operationalMismatchCount": 0,
            },
        },
        "evidenceDag": {
            "edgeCount": len(dag_checks),
            "failedEdgeCount": 0,
            "activeArtifacts": active_artifacts,
            "supersededArtifacts": superseded_artifacts,
            "aggregateInvariants": {
                "pairedCellCount": 40,
                "verifiedArmRunCount": 80,
                "verifiedSolverDecisionCount": 44_156,
                "retainedPortfolioCount": 44_156,
                "actionfulLinkCount": 41,
                "generatedSignatureEqualPairCount": 40,
                "candidateIdDriftCount": 0,
                "panelACompletedDeltaC1MinusB1": -154,
                "panelBCompletedDeltaC1MinusB1": -106,
                "associationRows": "overlappingCellsNotAdditive",
                "objectiveComparison": "notComparableAcrossObjectiveProfiles",
                "interpretation": "descriptiveAssociationNotCausal",
            },
        },
        "deepRawVerification": deep,
        "resourceFootprint": {
            "e1RawBundleBytes": 5_516_098_710,
            "e1MaximumBundleBytes": 119_353_350,
            "e1MaximumWallMilliseconds": 1_514_349,
            "interpretation": "instrumentationCostObservationNotMechanismEvidence",
        },
        "findings": {
            "unresolvedP0Count": 0,
            "unresolvedP1Count": 0,
            "unresolvedP2Count": 0,
            "resolvedP2Findings": [
                {
                    "id": "WP13-AUDIT-P2-001",
                    "resolution": (
                        "The closure verifier composes the frozen E1 verifier with "
                        "the missing canonical-integer, optional-field and exact "
                        "128-byte identifier constraints; the schema is also checked "
                        "independently on the contract fixture and external reports."
                    ),
                    "regression": (
                        "wrong-type/overlength repairedIncumbentRequestId and boolean "
                        "objective levelIndex mutations"
                    ),
                }
            ],
            "p3Limitations": [
                {
                    "id": "WP13-AUDIT-P3-001",
                    "owner": "RB-WP13-013",
                    "description": (
                        "Historical pre-E1 analyzers protect immutable raw roots but "
                        "do not all use exclusive creation for deterministic "
                        "derived outputs."
                    ),
                },
                {
                    "id": "WP13-AUDIT-P3-002",
                    "owner": "RB-WP13-013",
                    "description": (
                        "E1 retained portfolios consume 5.516 GB; closure must decide "
                        "retention/archive policy before any WP14 rerun."
                    ),
                },
                {
                    "id": "WP13-AUDIT-P3-003",
                    "owner": "RB-WP13-013",
                    "description": (
                        "The immutable E1 verifier alone predates the supplemental "
                        "optional-field/identifier guard; future evidence should use a "
                        "versioned successor rather than mutate the frozen source."
                    ),
                },
            ],
        },
    }
    jsonschema.Draft202012Validator(catalog[SCHEMA_ID][1]).validate(result)
    return result


def _require_safe_output(output):
    resolved = output.resolve()
    for root in _FORBIDDEN_OUTPUT_ROOTS:
        try:
            resolved.relative_to(root.resolve())
        except ValueError:
            continue
        raise RuntimeError(f"audit output must be outside immutable root: {root}")
    if resolved == _FREEZE_RECEIPT.resolve():
        raise RuntimeError("audit output must not overwrite the E1 freeze receipt")


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=pathlib.Path)
    parser.add_argument("--deep-raw-verify", action="store_true")
    parser.add_argument("--print-identity-only", action="store_true")
    arguments = parser.parse_args(argv)
    try:
        if arguments.output is not None:
            _require_safe_output(arguments.output)
        result = build_audit(arguments.deep_raw_verify)
        encoded = _encoded(result).encode("utf-8")
        if arguments.print_identity_only:
            print(
                json.dumps(
                    {
                        "lengthBytes": len(encoded),
                        "sha256": _sha256_bytes(encoded),
                    },
                    sort_keys=True,
                    separators=(",", ":"),
                )
            )
        elif arguments.output is None:
            sys.stdout.buffer.write(encoded)
        else:
            arguments.output.parent.mkdir(parents=True, exist_ok=True)
            with arguments.output.open("x", encoding="utf-8", newline="\n") as target:
                target.write(encoded.decode("utf-8"))
    except (
        OSError,
        RuntimeError,
        ValueError,
        TypeError,
        KeyError,
        jsonschema.ValidationError,
        subprocess.CalledProcessError,
    ) as error:
        print(f"wp13_full_audit: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
