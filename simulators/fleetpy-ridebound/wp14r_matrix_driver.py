"""Batch driver for the WP14R development matrix.

The frozen protocol runs exactly one job per invocation and refuses any job that
is not the next one in the frozen order, so a 160-job matrix is 160 separate
calls. This driver only decides *when to stop*; it never decides what to run.
It reads the ledger to find the next unfinished job, shells out to the exact
frozen protocol CLI for that job, and stops on the first job that does not
succeed.

It exists for two reasons the protocol deliberately does not cover:

1.  The matrix is resumable but not batched. `--max-jobs` lets an operator run a
    bounded slice per session instead of holding the host for the whole matrix.
2.  `authorize_phase` raises when retained output exceeds the frozen
    `maximumOutputBytes`, which would surface *after* a job has already spent its
    wall time. `--stop-at-output-percent` stops before starting a job that is
    likely to cross the cap, so the failure is a clean stop rather than a burnt
    job.

The driver has no authority of its own. It cannot reorder jobs, retry a job,
skip a failure, or relax a gate: every one of those is enforced inside the
protocol, which re-verifies the freeze on every call.
"""

import argparse
import json
import pathlib
import subprocess
import sys
import time

_HERE = pathlib.Path(__file__).resolve().parent
_PROTOCOL = _HERE / "wp14r_scientific_protocol.py"


class DriverError(RuntimeError):
    """The driver refuses to continue."""


def load_protocol():
    """Import the frozen protocol module for its read-only helpers."""
    import importlib.util

    spec = importlib.util.spec_from_file_location(
        "wp14r_scientific_protocol_for_driver", _PROTOCOL
    )
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def matrix_state(protocol, repository, freeze_path):
    """Return the frozen job order, the next unfinished job and byte usage."""
    receipt, _, dependencies = protocol.read_freeze(freeze_path, None)
    repository = pathlib.Path(repository).resolve()
    base = protocol.base_receipt(repository, receipt)
    ledger_root, _, forbidden = protocol.roots(receipt)
    ledger = dependencies["ledger"]

    order = [job["jobId"] for job in base["design"]["jobs"]]
    succeeded, exhausted, pending = [], [], []
    for job_id in order:
        state = ledger.inspect_ledger(ledger_root, job_id, forbidden)
        label = state["ledgerState"]
        if label == "succeeded":
            succeeded.append(job_id)
        elif label == "exhausted":
            exhausted.append(job_id)
        else:
            pending.append(job_id)

    retained = protocol.retained_output_bytes(base, ledger_root, forbidden, ledger)
    maximum = base["execution"]["resourceEnvelope"]["maximumOutputBytes"]
    return {
        "order": order,
        "succeeded": succeeded,
        "exhausted": exhausted,
        "pending": pending,
        "nextJobId": pending[0] if pending else None,
        "retainedOutputBytes": retained,
        "maximumOutputBytes": maximum,
        "outputPercent": round(100.0 * retained / maximum, 3) if maximum else 0.0,
    }


def run_job(python, repository, freeze_path, job_id, phase):
    """Invoke the frozen protocol CLI for exactly one job."""
    completed = subprocess.run(
        [
            str(python),
            "-B",
            str(_PROTOCOL),
            "run",
            "--repository",
            str(repository),
            "--freeze",
            str(freeze_path),
            "--phase",
            phase,
            "--job-id",
            job_id,
        ],
        check=False,
        capture_output=True,
        text=True,
        encoding="utf-8",
    )
    text = (completed.stdout or "").strip()
    try:
        result = json.loads(text.splitlines()[-1]) if text else {}
    except (ValueError, IndexError):
        result = {}
    result.setdefault("status", "unparsedProtocolOutput")
    result["exitCode"] = completed.returncode
    if not text:
        result["stderr"] = (completed.stderr or "").strip()[:2000]
    return result


def drive(
    repository,
    freeze_path,
    python,
    phase="matrix",
    max_jobs=None,
    stop_at_output_percent=90.0,
    protocol=None,
    runner=None,
    printer=print,
):
    """Run jobs in frozen order until a stop condition fires."""
    protocol = protocol or load_protocol()
    runner = runner or run_job
    executed = []
    started = time.time()
    previous_job_id = None

    while True:
        state = matrix_state(protocol, repository, freeze_path)
        if previous_job_id is not None and state["nextJobId"] == previous_job_id:
            # The protocol reported success but the ledger did not advance. Never
            # call the same job again: that is the shape of an infinite loop, and
            # a job that cannot advance is a fail-closed condition, not a retry.
            return _stop(
                "ledgerDidNotAdvance", state, executed, started,
                f"{previous_job_id} did not become terminal after running",
                printer,
            )
        if state["exhausted"]:
            return _stop(
                "exhaustedJobPresent", state, executed, started,
                "the matrix stopped at an exhausted frozen job", printer,
            )
        if state["nextJobId"] is None:
            return _stop(
                "matrixComplete", state, executed, started,
                "every frozen job has succeeded", printer,
            )
        if state["outputPercent"] >= stop_at_output_percent:
            return _stop(
                "outputBudgetGuard", state, executed, started,
                "retained output reached the driver guard before the frozen cap",
                printer,
            )
        if max_jobs is not None and len(executed) >= max_jobs:
            return _stop(
                "batchLimitReached", state, executed, started,
                "the requested batch size completed", printer,
            )

        job_id = state["nextJobId"]
        index = state["order"].index(job_id) + 1
        printer(
            f"[{index}/{len(state['order'])}] {job_id} "
            f"(retained {state['outputPercent']:.2f}% of cap)"
        )
        job_started = time.time()
        previous_job_id = job_id
        result = runner(python, repository, freeze_path, job_id, phase)
        elapsed = round(time.time() - job_started, 1)
        executed.append({"jobId": job_id, "elapsedSeconds": elapsed, **result})
        printer(f"    -> {result.get('status')} in {elapsed}s")
        if result.get("status") != "succeeded":
            state = matrix_state(protocol, repository, freeze_path)
            return _stop(
                "jobDidNotSucceed", state, executed, started,
                f"{job_id} returned {result.get('status')}", printer,
            )


def _stop(reason, state, executed, started, detail, printer):
    report = {
        "reportType": "ridebound-wp14r-matrix-driver-v1",
        "stopReason": reason,
        "detail": detail,
        "jobsExecutedThisSession": len(executed),
        "succeededTotal": len(state["succeeded"]),
        "pendingTotal": len(state["pending"]),
        "exhaustedTotal": len(state["exhausted"]),
        "retainedOutputBytes": state["retainedOutputBytes"],
        "maximumOutputBytes": state["maximumOutputBytes"],
        "outputPercent": state["outputPercent"],
        "elapsedSeconds": round(time.time() - started, 1),
        "jobs": executed,
        "claimBoundary": [
            "driverHasNoAuthorityProtocolEnforcesOrderAndGates",
            "doesNotReadScientificOutcome",
            "doesNotRetryOrSkipAnyJob",
        ],
    }
    printer(json.dumps(report, indent=1))
    return report


def build_parser():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repository", required=True, type=pathlib.Path)
    parser.add_argument("--freeze", required=True, type=pathlib.Path)
    parser.add_argument("--python", required=True, type=pathlib.Path)
    parser.add_argument("--phase", choices=("paired", "matrix"), default="matrix")
    parser.add_argument("--max-jobs", type=int)
    parser.add_argument("--stop-at-output-percent", type=float, default=90.0)
    parser.add_argument("--status-only", action="store_true")
    return parser


def main(argv=None):
    arguments = build_parser().parse_args(argv)
    if arguments.max_jobs is not None and arguments.max_jobs < 1:
        raise DriverError("--max-jobs must be at least 1")
    if not 0 < arguments.stop_at_output_percent <= 100:
        raise DriverError("--stop-at-output-percent must be in (0, 100]")
    protocol = load_protocol()
    if arguments.status_only:
        state = matrix_state(protocol, arguments.repository, arguments.freeze)
        print(json.dumps(state, indent=1))
        return 0
    report = drive(
        arguments.repository,
        arguments.freeze,
        arguments.python,
        phase=arguments.phase,
        max_jobs=arguments.max_jobs,
        stop_at_output_percent=arguments.stop_at_output_percent,
        protocol=protocol,
    )
    return 0 if report["stopReason"] in {"matrixComplete", "batchLimitReached"} else 1


if __name__ == "__main__":
    sys.exit(main())
