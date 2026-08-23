#!/usr/bin/env python3
from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import subprocess
from typing import Iterable


def _sha256(path: pathlib.Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _tree(paths: Iterable[pathlib.Path], root: pathlib.Path) -> dict[str, object]:
    rows = []
    files = []
    for path in sorted(paths, key=lambda value: value.relative_to(root).as_posix()):
        relative = path.relative_to(root).as_posix()
        digest = _sha256(path)
        rows.append(f"{relative}\t{digest}")
        files.append({"path": relative, "sha256": digest, "bytes": path.stat().st_size})
    payload = ("\n".join(rows) + "\n").encode("utf-8")
    return {"fileCount": len(files), "treeSha256": hashlib.sha256(payload).hexdigest(), "files": files}


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repository", required=True, type=pathlib.Path)
    parser.add_argument("--manifest", required=True, type=pathlib.Path)
    parser.add_argument("--runner-root", required=True, type=pathlib.Path)
    parser.add_argument("--source-receipt", required=True, type=pathlib.Path)
    parser.add_argument("--image", required=True)
    parser.add_argument("--output", required=True, type=pathlib.Path)
    arguments = parser.parse_args()
    repository = arguments.repository.resolve()
    adapter_root = repository / "simulators" / "ridepy-ridebound"
    adapter_files = [
        *adapter_root.glob("*.py"),
        *(adapter_root / "ridebound_ridepy").glob("*.py"),
        adapter_root / "Dockerfile",
    ]
    diff = subprocess.run(
        ["git", "-C", str(repository), "diff", "--binary", "HEAD"],
        check=True,
        stdout=subprocess.PIPE,
    ).stdout
    head = subprocess.run(
        ["git", "-C", str(repository), "rev-parse", "HEAD"],
        check=True,
        stdout=subprocess.PIPE,
        text=True,
        encoding="ascii",
    ).stdout.strip()
    image = json.loads(
        subprocess.run(
            ["docker", "image", "inspect", arguments.image],
            check=True,
            stdout=subprocess.PIPE,
            text=True,
            encoding="utf-8",
        ).stdout
    )[0]
    configs = repository / "benchmarks" / "configurations"
    report = {
        "schemaVersion": "1.0.0",
        "freezeId": "wp10-ridepy-paired-subset-freeze-v1",
        "status": "frozenBeforeOutcomeExecution",
        "coreCommit": head,
        "dirtyTrackedDiffSha256": hashlib.sha256(diff).hexdigest(),
        "subsetManifest": {
            "path": arguments.manifest.name,
            "sha256": _sha256(arguments.manifest),
        },
        "sourceReceiptSha256": _sha256(arguments.source_receipt),
        "image": {"name": arguments.image, "id": image["Id"]},
        "runnerPublish": _tree(
            (path for path in arguments.runner_root.rglob("*") if path.is_file()),
            arguments.runner_root,
        ),
        "adapter": _tree((path for path in adapter_files if path.is_file()), adapter_root),
        "configurationHashes": {
            "commitment": _sha256(configs / "wp8-drop-eta-budget-tight-v1.json"),
            "B1": _sha256(configs / "wp9-fleetpy-rolling-cost-audited-v1.json"),
            "C1": _sha256(configs / "wp9-fleetpy-ridebound-hard-vector-audited-v1.json"),
        },
        "claimBoundary": "pairedDescriptiveHeterogeneityOnlyCannotRescueH6",
    }
    encoded = json.dumps(report, ensure_ascii=False, sort_keys=True, separators=(",", ":"))
    arguments.output.parent.mkdir(parents=True, exist_ok=True)
    arguments.output.write_text(encoded + "\n", encoding="utf-8", newline="\n")
    print(encoded)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
