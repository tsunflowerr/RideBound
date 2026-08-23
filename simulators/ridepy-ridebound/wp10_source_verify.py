#!/usr/bin/env python3
"""Verify the exact external RidePy source and optional container environment."""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import subprocess
from dataclasses import dataclass
from typing import Any, Iterable


@dataclass(frozen=True)
class SourcePin:
    version: str
    commit: str
    tree_sha256: str
    file_count: int
    license_sha256: str
    pyproject_sha256: str


PIN = SourcePin(
    version="2.10.1",
    commit="bf1863e49a432f2f1f6230f86b2777a5ef5b9f14",
    tree_sha256="d99ffac89d4bcc09e04dcd82c9ae6b08ce5891cb2f39b1af18cd9e5311cd891e",
    file_count=527,
    license_sha256="87e0c317105d31484c536b97fc7bc0789f4cc7b3c3df44f858b5fc49ed511798",
    pyproject_sha256="eb7bd5a17c69f14bc742c375ab8a5cd6eba7489e53a34684f7f9dda09f55e4f8",
)
BASE_DIGEST = "sha256:a365ce6a50b09176855d085c69da3fc1204a48432e36087e9a208f6e5860e235"
SUBMODULES = (
    ("13f30ad33a227a3e9682578c450777380ecddfcf", "src/lru-cache"),
    (
        "a2b8a8e07628e5fd60644b6dd99c1b5e7d7f1f47",
        "src/lru-cache/tests/googletest",
    ),
)


class VerificationFailure(RuntimeError):
    def __init__(self, code: str, detail: str) -> None:
        super().__init__(f"{code}: {detail}")
        self.code = code
        self.detail = detail


def _sha256(path: pathlib.Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def source_inventory(root: pathlib.Path) -> tuple[int, str]:
    rows: list[str] = []
    for path in root.rglob("*"):
        relative = path.relative_to(root)
        if not path.is_file() or ".git" in relative.parts:
            continue
        rows.append(f"{relative.as_posix()}\t{_sha256(path)}")
    payload = ("\n".join(sorted(rows)) + "\n").encode("utf-8")
    return len(rows), hashlib.sha256(payload).hexdigest()


def _run(arguments: Iterable[str]) -> str:
    completed = subprocess.run(
        list(arguments),
        check=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        encoding="utf-8",
    )
    return completed.stdout.rstrip("\r\n")


def verify_source(root: pathlib.Path, pin: SourcePin = PIN) -> dict[str, Any]:
    root = root.resolve()
    if not root.is_dir():
        raise VerificationFailure("RBWP10_SOURCE_ROOT_MISSING", str(root))
    try:
        commit = _run(["git", "-C", str(root), "rev-parse", "HEAD"])
    except (OSError, subprocess.CalledProcessError) as exc:
        raise VerificationFailure("RBWP10_SOURCE_GIT_INVALID", str(exc)) from exc
    if commit != pin.commit:
        raise VerificationFailure(
            "RBWP10_SOURCE_COMMIT_MISMATCH", f"actual={commit}; expected={pin.commit}"
        )
    status = _run(["git", "-C", str(root), "status", "--porcelain"])
    if status:
        raise VerificationFailure("RBWP10_SOURCE_DIRTY", status)
    submodule_output = _run(
        ["git", "-C", str(root), "submodule", "status", "--recursive"]
    )
    actual_submodules: list[tuple[str, str]] = []
    for line in submodule_output.splitlines():
        if not line or line[0] != " ":
            raise VerificationFailure("RBWP10_SOURCE_SUBMODULE_INVALID", line)
        parts = line[1:].split()
        if len(parts) < 2:
            raise VerificationFailure("RBWP10_SOURCE_SUBMODULE_INVALID", line)
        actual_submodules.append((parts[0], parts[1].replace("\\", "/")))
    if tuple(actual_submodules) != SUBMODULES:
        raise VerificationFailure(
            "RBWP10_SOURCE_SUBMODULE_MISMATCH",
            f"actual={actual_submodules!r}; expected={SUBMODULES!r}",
        )

    pyproject = root / "pyproject.toml"
    license_path = root / "LICENSE"
    if not pyproject.is_file() or not license_path.is_file():
        raise VerificationFailure("RBWP10_SOURCE_REQUIRED_FILE_MISSING", str(root))
    pyproject_hash = _sha256(pyproject)
    if pyproject_hash != pin.pyproject_sha256:
        raise VerificationFailure(
            "RBWP10_SOURCE_PYPROJECT_MISMATCH",
            f"actual={pyproject_hash}; expected={pin.pyproject_sha256}",
        )
    license_hash = _sha256(license_path)
    if license_hash != pin.license_sha256:
        raise VerificationFailure(
            "RBWP10_SOURCE_LICENSE_MISMATCH",
            f"actual={license_hash}; expected={pin.license_sha256}",
        )
    text = pyproject.read_text(encoding="utf-8")
    if f'version = "{pin.version}"' not in text:
        raise VerificationFailure("RBWP10_SOURCE_VERSION_MISMATCH", pin.version)
    file_count, tree_hash = source_inventory(root)
    if file_count != pin.file_count or tree_hash != pin.tree_sha256:
        raise VerificationFailure(
            "RBWP10_SOURCE_TREE_MISMATCH",
            f"files={file_count}/{pin.file_count}; sha256={tree_hash}/{pin.tree_sha256}",
        )
    return {
        "version": pin.version,
        "commit": commit,
        "submodules": [
            {"commit": submodule_commit, "path": path}
            for submodule_commit, path in actual_submodules
        ],
        "fileCount": file_count,
        "treeSha256": tree_hash,
        "licenseSha256": license_hash,
        "pyprojectSha256": pyproject_hash,
    }


def verify_image(image: str) -> dict[str, Any]:
    try:
        inspection = json.loads(_run(["docker", "image", "inspect", image]))[0]
    except (OSError, subprocess.CalledProcessError, json.JSONDecodeError, IndexError) as exc:
        raise VerificationFailure("RBWP10_ENV_IMAGE_INVALID", str(exc)) from exc
    labels = inspection.get("Config", {}).get("Labels") or {}
    expected_labels = {
        "org.opencontainers.image.version": PIN.version,
        "ridebound.ridepy.commit": PIN.commit,
        "ridebound.ridepy.tree-sha256": PIN.tree_sha256,
        "ridebound.base.digest": BASE_DIGEST,
        "ridebound.ridepy.lru-cache-commit": SUBMODULES[0][0],
        "ridebound.ridepy.googletest-commit": SUBMODULES[1][0],
    }
    for key, expected in expected_labels.items():
        if labels.get(key) != expected:
            raise VerificationFailure(
                "RBWP10_ENV_LABEL_MISMATCH",
                f"{key}: actual={labels.get(key)!r}; expected={expected!r}",
            )
    probe_script = (
        "import importlib.metadata as m,json,platform,ridepy;"
        "from ridepy.util.spaces import Graph;"
        "assert Graph(vertices=[0,1],edges=[(0,1)]).t(0,1)==1;"
        "print(json.dumps({'ridepyVersion':m.version('ridepy'),"
        "'pythonVersion':platform.python_version(),'platform':platform.system()}))"
    )
    try:
        runtime = json.loads(
            _run(["docker", "run", "--rm", image, "python", "-c", probe_script])
        )
        dotnet_runtimes = _run(
            ["docker", "run", "--rm", image, "dotnet", "--list-runtimes"]
        )
    except (OSError, subprocess.CalledProcessError, json.JSONDecodeError) as exc:
        raise VerificationFailure("RBWP10_ENV_RUNTIME_PROBE_FAILED", str(exc)) from exc
    if runtime.get("ridepyVersion") != PIN.version or runtime.get("platform") != "Linux":
        raise VerificationFailure("RBWP10_ENV_RUNTIME_MISMATCH", repr(runtime))
    dotnet_version = next(
        (
            line.split()[1]
            for line in dotnet_runtimes.splitlines()
            if line.startswith("Microsoft.NETCore.App ") and len(line.split()) >= 2
        ),
        "",
    )
    if not dotnet_version.startswith("10."):
        raise VerificationFailure("RBWP10_ENV_DOTNET_MISMATCH", dotnet_runtimes)
    return {
        "image": image,
        "imageId": inspection.get("Id"),
        "labels": expected_labels,
        "runtime": runtime,
        "dotnetVersion": dotnet_version,
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-root", required=True, type=pathlib.Path)
    parser.add_argument("--image")
    parser.add_argument("--output", type=pathlib.Path)
    arguments = parser.parse_args()
    report: dict[str, Any] = {
        "schemaVersion": "1.0.0",
        "status": "pass",
        "source": verify_source(arguments.source_root),
    }
    if arguments.image:
        report["environment"] = verify_image(arguments.image)
    encoded = json.dumps(report, ensure_ascii=False, sort_keys=True, separators=(",", ":"))
    if arguments.output:
        arguments.output.parent.mkdir(parents=True, exist_ok=True)
        arguments.output.write_text(encoded + "\n", encoding="utf-8", newline="\n")
    print(encoded)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
