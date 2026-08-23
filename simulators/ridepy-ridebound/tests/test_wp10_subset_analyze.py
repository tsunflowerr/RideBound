from __future__ import annotations

import pathlib
import sys
import tempfile
import unittest


ROOT = pathlib.Path(__file__).parents[1]
sys.path.insert(0, str(ROOT))

from wp10_bundle_verify import VerificationFailure  # noqa: E402
from wp10_subset_analyze import (  # noqa: E402
    FAILURE_JOB_ID,
    _verify_result_inventory,
)


def _manifest() -> dict:
    job_ids = [f"job-{index}" for index in range(11)] + [FAILURE_JOB_ID]
    return {
        "jobs": [{"jobId": job_id} for job_id in job_ids],
        "jobCount": 12,
        "armJobCount": 24,
    }


def _materialize(root: pathlib.Path, manifest: dict) -> None:
    for job in manifest["jobs"]:
        job_root = root / job["jobId"]
        arms = ("B1",) if job["jobId"] == FAILURE_JOB_ID else ("B1", "C1")
        for arm in arms:
            arm_root = job_root / arm
            arm_root.mkdir(parents=True)
            if job["jobId"] == FAILURE_JOB_ID:
                (arm_root / "protocol-transcript.ndjson").write_text(
                    "failure\n", encoding="utf-8", newline="\n"
                )


class ResultInventoryTests(unittest.TestCase):
    def test_exact_terminal_inventory_passes(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            manifest = _manifest()
            _materialize(root, manifest)
            _verify_result_inventory(manifest, root)

    def test_missing_valid_pair_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            manifest = _manifest()
            _materialize(root, manifest)
            missing = root / "job-0" / "C1"
            missing.rmdir()
            with self.assertRaisesRegex(
                VerificationFailure, "RBWP10_VERIFY_ARM_INVENTORY"
            ):
                _verify_result_inventory(manifest, root)

    def test_unplanned_arm_or_failure_file_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            manifest = _manifest()
            _materialize(root, manifest)
            (root / FAILURE_JOB_ID / "C1").mkdir()
            with self.assertRaisesRegex(
                VerificationFailure, "RBWP10_VERIFY_ARM_INVENTORY"
            ):
                _verify_result_inventory(manifest, root)


if __name__ == "__main__":
    unittest.main()
