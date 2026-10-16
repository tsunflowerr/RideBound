"""Owner-authorized resource amendment; execute the unchanged, pinned v7 driver.

The sole driver-global change is FLOOR_MIB=30*1024. Original input verification,
analysis binding, instance lock, admission, reservations and job commands remain.
This entry point is explicitly recorded as amended execution, not plain v7.
"""
from __future__ import annotations

import hashlib
import json
import pathlib
import re
import shutil
import sys

HERE = pathlib.Path(__file__).resolve().parent
HARNESS = pathlib.Path("E:/Code/Report_INT3508/ridebound-scratchpad/tang3/stage1b/harness")
DRIVER = HARNESS / "run_stage1b-v7.py"
DRIVER_SHA = "7495dd515154e81932ce61ea537f648739d940099b37425d0eb6b58f34e64811"
POLICY_FILES = {"resource30_driver.py", "launch-resource30.ps1", "test_resource30.py",
                "STAGE1B-AMENDMENTS.md", "baseline-20.json"}
MAIN_ROOT = pathlib.Path("C:/RideBoundData/research/tier3-stage1b-v1/main")


def verify_policy(expected_sha: str) -> None:
    raw = (HERE / "resource30.sha256").read_bytes()
    if hashlib.sha256(raw).hexdigest() != expected_sha:
        raise RuntimeError("resource policy manifest differs from the pinned identity")
    listed = set()
    for line in raw.decode("utf-8").splitlines():
        match = re.fullmatch(r"([0-9a-f]{64})  ([A-Za-z0-9_.-]+)", line)
        if not match or match[2] not in POLICY_FILES or match[2] in listed:
            raise RuntimeError("invalid resource policy inventory")
        listed.add(match[2])
        if hashlib.sha256((HERE / match[2]).read_bytes()).hexdigest() != match[1]:
            raise RuntimeError(f"resource policy file changed: {match[2]}")
    if listed != POLICY_FILES:
        raise RuntimeError("incomplete resource policy inventory")


def load_driver() -> dict:
    source = DRIVER.read_bytes()
    if hashlib.sha256(source).hexdigest() != DRIVER_SHA:
        raise RuntimeError("original sealed v7 driver bytes changed")
    namespace = {"__name__": "stage1b_v7_resource30", "__file__": str(DRIVER), "__package__": None}
    exec(compile(source, str(DRIVER), "exec"), namespace)
    if namespace["FLOOR_MIB"] != 46_080 or namespace["NEED_MIB"] != 410:
        raise RuntimeError("unexpected original resource constants")
    namespace["FLOOR_MIB"] = 30_720
    return namespace


def main() -> int:
    if len(sys.argv) != 4 or sys.argv[1] != "--policy-sha" or sys.argv[3] not in ("selftest", "run"):
        raise SystemExit("usage: resource30_driver.py --policy-sha <sha256> selftest|run")
    expected_sha, mode = sys.argv[2:]
    failure = HERE / "RESOURCE-POLICY-FAILED.txt"
    if failure.exists():
        raise SystemExit(f"refused: {failure} exists; preserve it for owner review")
    try:
        verify_policy(expected_sha)
    except Exception as error:
        with failure.open("x", encoding="utf-8") as handle:
            handle.write(f"{type(error).__name__}: {error}\n")
        raise
    print(f"OWNER RESOURCE AMENDMENT policy-sha256={expected_sha}; C-floor=30720MiB; "
          "reserve=410MiB/job; E-floor=40GiB; original-v7/analysis unchanged", flush=True)
    if mode == "selftest":
        import unittest
        suite = unittest.defaultTestLoader.discover(str(HERE), pattern="test_resource30.py")
        result = unittest.TextTestRunner(verbosity=2).run(suite)
        verify_policy(expected_sha)
        return 0 if result.wasSuccessful() else 1
    if shutil.disk_usage("E:/").free < 40 * 1024 ** 3:
        raise SystemExit("resource deferral: E is below the unchanged40GiB floor")
    baseline = json.loads((HERE / "baseline-20.json").read_text(encoding="utf-8"))
    if len(baseline["CompletedJobs"]) != 20:
        raise RuntimeError("unexpected pre-amendment baseline")
    for name in baseline["CompletedJobs"]:
        folder = MAIN_ROOT / name
        if (json.loads((folder / "summary.json").read_text())["status"] != "pass"
                or json.loads((folder / "run-00.json").read_text())["status"] != "succeeded"):
            raise RuntimeError(f"pre-amendment completed job changed: {name}")
    driver = load_driver()
    print(f"original-driver-sha256={DRIVER_SHA}; effective-floor={driver['FLOOR_MIB']}MiB; "
          f"workers={driver['run_pilot'].WORKERS}; pending-only via original run_job", flush=True)
    sys.argv = [str(DRIVER), "run", "main"]
    try:
        return driver["main"]()
    finally:
        try:
            verify_policy(expected_sha)
        except Exception as error:
            if not failure.exists():
                with failure.open("x", encoding="utf-8") as handle:
                    handle.write(f"after phase: {type(error).__name__}: {error}\n")
            raise


if __name__ == "__main__":
    raise SystemExit(main())
