"""Unit tests for the WP14R matrix batch driver.

The driver has no authority of its own, so these tests pin exactly that: it
runs the next frozen job and nothing else, and it stops rather than pushing
past an exhausted job, a failed job, the batch size or the output guard.
"""

import importlib.util
import pathlib
import unittest

_HERE = pathlib.Path(__file__).resolve().parent
_DRIVER_PATH = _HERE.parent / "wp14r_matrix_driver.py"

_spec = importlib.util.spec_from_file_location(
    "wp14r_matrix_driver_under_test", _DRIVER_PATH
)
driver = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(driver)

JOBS = ["job-01", "job-02", "job-03"]
MAXIMUM = 1000


class _FakeLedger:
    def __init__(self, states):
        self.states = states

    def inspect_ledger(self, ledger_root, job_id, forbidden):
        return {"ledgerState": self.states.get(job_id, "notStarted")}


class _FakeProtocol:
    """Only the read-only helpers the driver is allowed to use."""

    def __init__(self, states, retained=0):
        self.ledger = _FakeLedger(states)
        self.retained = retained

    def read_freeze(self, freeze_path, dependencies):
        return {"freezeId": "fake"}, "sha", {"ledger": self.ledger}

    def base_receipt(self, repository, receipt):
        return {
            "design": {"jobs": [{"jobId": job} for job in JOBS]},
            "execution": {"resourceEnvelope": {"maximumOutputBytes": MAXIMUM}},
        }

    def roots(self, receipt):
        return pathlib.Path("ledger"), pathlib.Path("control"), []

    def retained_output_bytes(self, base, ledger_root, forbidden, ledger):
        return self.retained


def _runner(protocol, status="succeeded", per_job_bytes=0):
    """Return a runner stub that advances the fake ledger like the real one."""
    calls = []

    def run(python, repository, freeze_path, job_id, phase):
        calls.append((job_id, phase))
        protocol.ledger.states[job_id] = (
            "succeeded" if status == "succeeded" else "exhausted"
        )
        protocol.retained += per_job_bytes
        return {"status": status, "jobId": job_id}

    run.calls = calls
    return run


def _drive(protocol, runner, **kwargs):
    return driver.drive(
        pathlib.Path("repo"),
        pathlib.Path("freeze.json"),
        pathlib.Path("python.exe"),
        protocol=protocol,
        runner=runner,
        printer=lambda *args, **kw: None,
        **kwargs,
    )


class MatrixDriverTests(unittest.TestCase):
    def test_runs_every_pending_job_in_frozen_order(self):
        protocol = _FakeProtocol({})
        runner = _runner(protocol)
        report = _drive(protocol, runner)
        self.assertEqual([call[0] for call in runner.calls], JOBS)
        self.assertEqual([call[1] for call in runner.calls], ["matrix"] * 3)
        self.assertEqual(report["stopReason"], "matrixComplete")
        self.assertEqual(report["succeededTotal"], 3)

    def test_skips_already_succeeded_jobs(self):
        protocol = _FakeProtocol({"job-01": "succeeded"})
        runner = _runner(protocol)
        _drive(protocol, runner)
        self.assertEqual([call[0] for call in runner.calls], ["job-02", "job-03"])

    def test_batch_limit_stops_without_touching_later_jobs(self):
        protocol = _FakeProtocol({})
        runner = _runner(protocol)
        report = _drive(protocol, runner, max_jobs=2)
        self.assertEqual([call[0] for call in runner.calls], ["job-01", "job-02"])
        self.assertEqual(report["stopReason"], "batchLimitReached")
        self.assertEqual(report["pendingTotal"], 1)

    def test_exhausted_job_stops_the_matrix_before_running_anything(self):
        protocol = _FakeProtocol({"job-01": "exhausted"})
        runner = _runner(protocol)
        report = _drive(protocol, runner)
        self.assertEqual(runner.calls, [])
        self.assertEqual(report["stopReason"], "exhaustedJobPresent")

    def test_a_job_that_does_not_succeed_stops_the_run(self):
        protocol = _FakeProtocol({})
        runner = _runner(protocol, status="preflightFailed")
        report = _drive(protocol, runner)
        self.assertEqual([call[0] for call in runner.calls], ["job-01"])
        self.assertEqual(report["stopReason"], "jobDidNotSucceed")

    def test_output_guard_stops_before_starting_the_next_job(self):
        protocol = _FakeProtocol({}, retained=0)
        runner = _runner(protocol, per_job_bytes=460)
        report = _drive(protocol, runner, stop_at_output_percent=90.0)
        # 0% -> run, 46% -> run, 92% -> guard fires before the third job.
        self.assertEqual([call[0] for call in runner.calls], ["job-01", "job-02"])
        self.assertEqual(report["stopReason"], "outputBudgetGuard")
        self.assertGreaterEqual(report["outputPercent"], 90.0)

    def test_guard_percent_is_reported_against_the_frozen_maximum(self):
        protocol = _FakeProtocol({"job-01": "succeeded"}, retained=250)
        state = driver.matrix_state(
            protocol, pathlib.Path("repo"), pathlib.Path("freeze.json")
        )
        self.assertEqual(state["nextJobId"], "job-02")
        self.assertEqual(state["maximumOutputBytes"], MAXIMUM)
        self.assertEqual(state["outputPercent"], 25.0)
        self.assertEqual(state["succeeded"], ["job-01"])
        self.assertEqual(state["pending"], ["job-02", "job-03"])

    def test_a_ledger_that_does_not_advance_stops_instead_of_looping(self):
        protocol = _FakeProtocol({})
        calls = []

        def stuck(python, repository, freeze_path, job_id, phase):
            # Claims success but never marks the ledger terminal.
            calls.append(job_id)
            return {"status": "succeeded", "jobId": job_id}

        report = _drive(protocol, stuck)
        self.assertEqual(calls, ["job-01"])
        self.assertEqual(report["stopReason"], "ledgerDidNotAdvance")

    def test_cli_rejects_out_of_range_arguments(self):
        with self.assertRaises(driver.DriverError):
            driver.main(
                [
                    "--repository", "repo", "--freeze", "f.json",
                    "--python", "p.exe", "--max-jobs", "0",
                ]
            )
        with self.assertRaises(driver.DriverError):
            driver.main(
                [
                    "--repository", "repo", "--freeze", "f.json",
                    "--python", "p.exe", "--stop-at-output-percent", "0",
                ]
            )


if __name__ == "__main__":
    unittest.main()
