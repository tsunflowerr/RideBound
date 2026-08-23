from __future__ import annotations

import hashlib
import json
import pathlib
import subprocess
import tempfile
import unittest
from unittest import mock


ROOT = pathlib.Path(__file__).parents[1]
import sys

sys.path.insert(0, str(ROOT))

from wp10_source_verify import (  # noqa: E402
    BASE_DIGEST,
    SUBMODULES,
    SourcePin,
    VerificationFailure,
    source_inventory,
    verify_image,
    verify_source,
)


class SourceVerifierTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory()
        self.root = pathlib.Path(self.temp.name)
        (self.root / "LICENSE").write_text("MIT\n", encoding="utf-8", newline="\n")
        (self.root / "pyproject.toml").write_text(
            '[project]\nversion = "2.10.1"\n', encoding="utf-8", newline="\n"
        )
        count, tree_hash = source_inventory(self.root)
        self.pin = SourcePin(
            version="2.10.1",
            commit="c" * 40,
            tree_sha256=tree_hash,
            file_count=count,
            license_sha256=hashlib.sha256(b"MIT\n").hexdigest(),
            pyproject_sha256=hashlib.sha256(
                b'[project]\nversion = "2.10.1"\n'
            ).hexdigest(),
        )

    def tearDown(self) -> None:
        self.temp.cleanup()

    @mock.patch("wp10_source_verify._run")
    def test_exact_source_passes(self, run: mock.Mock) -> None:
        run.side_effect = [self.pin.commit, "", self._submodule_status()]
        report = verify_source(self.root, self.pin)
        self.assertEqual(self.pin.tree_sha256, report["treeSha256"])
        self.assertEqual(2, report["fileCount"])

    @mock.patch("wp10_source_verify._run")
    def test_commit_mutation_fails(self, run: mock.Mock) -> None:
        run.return_value = "d" * 40
        with self.assertRaisesRegex(VerificationFailure, "RBWP10_SOURCE_COMMIT_MISMATCH"):
            verify_source(self.root, self.pin)

    @mock.patch("wp10_source_verify._run")
    def test_license_mutation_fails(self, run: mock.Mock) -> None:
        run.side_effect = [self.pin.commit, "", self._submodule_status()]
        (self.root / "LICENSE").write_text("changed", encoding="utf-8")
        with self.assertRaisesRegex(VerificationFailure, "RBWP10_SOURCE_LICENSE_MISMATCH"):
            verify_source(self.root, self.pin)

    @mock.patch("wp10_source_verify._run")
    def test_version_mutation_fails(self, run: mock.Mock) -> None:
        run.side_effect = [self.pin.commit, "", self._submodule_status()]
        text = '[project]\nversion = "2.10.0"\n'
        (self.root / "pyproject.toml").write_text(text, encoding="utf-8", newline="\n")
        mutated = SourcePin(
            **{
                **self.pin.__dict__,
                "pyproject_sha256": hashlib.sha256(text.encode()).hexdigest(),
            }
        )
        with self.assertRaisesRegex(VerificationFailure, "RBWP10_SOURCE_VERSION_MISMATCH"):
            verify_source(self.root, mutated)

    @mock.patch("wp10_source_verify._run")
    def test_tree_extra_file_mutation_fails(self, run: mock.Mock) -> None:
        run.side_effect = [self.pin.commit, "", self._submodule_status()]
        (self.root / "unexpected.py").write_text("pass\n", encoding="utf-8")
        with self.assertRaisesRegex(VerificationFailure, "RBWP10_SOURCE_TREE_MISMATCH"):
            verify_source(self.root, self.pin)

    @mock.patch("wp10_source_verify._run")
    def test_uninitialized_submodule_mutation_fails(self, run: mock.Mock) -> None:
        run.side_effect = [self.pin.commit, "", "-" + SUBMODULES[0][0] + " src/lru-cache"]
        with self.assertRaisesRegex(VerificationFailure, "RBWP10_SOURCE_SUBMODULE_INVALID"):
            verify_source(self.root, self.pin)

    @staticmethod
    def _submodule_status() -> str:
        return "\n".join(f" {commit} {path}" for commit, path in SUBMODULES)


class ImageVerifierTests(unittest.TestCase):
    @mock.patch("wp10_source_verify._run")
    def test_image_labels_and_runtime_pass(self, run: mock.Mock) -> None:
        from wp10_source_verify import PIN

        inspection = [
            {
                "Id": "sha256:image",
                "Config": {
                    "Labels": {
                        "org.opencontainers.image.version": PIN.version,
                        "ridebound.ridepy.commit": PIN.commit,
                        "ridebound.ridepy.tree-sha256": PIN.tree_sha256,
                        "ridebound.base.digest": BASE_DIGEST,
                        "ridebound.ridepy.lru-cache-commit": SUBMODULES[0][0],
                        "ridebound.ridepy.googletest-commit": SUBMODULES[1][0],
                    }
                },
            }
        ]
        run.side_effect = [
            json.dumps(inspection),
            json.dumps(
                {
                    "ridepyVersion": PIN.version,
                    "pythonVersion": "3.12.3",
                    "platform": "Linux",
                }
            ),
            "Microsoft.NETCore.App 10.0.11 [/usr/share/dotnet/shared/Microsoft.NETCore.App]",
        ]
        self.assertEqual("sha256:image", verify_image("ridebound-test")["imageId"])

    @mock.patch("wp10_source_verify._run")
    def test_image_label_mutation_fails(self, run: mock.Mock) -> None:
        run.return_value = json.dumps([{"Id": "x", "Config": {"Labels": {}}}])
        with self.assertRaisesRegex(VerificationFailure, "RBWP10_ENV_LABEL_MISMATCH"):
            verify_image("ridebound-test")


if __name__ == "__main__":
    unittest.main()
