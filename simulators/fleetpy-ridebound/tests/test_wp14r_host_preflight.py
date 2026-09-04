import importlib.util
import json
import pathlib
import tempfile
import unittest
from unittest import mock


ROOT = pathlib.Path(__file__).resolve().parents[3]
MODULE_PATH = (
    ROOT / "simulators/fleetpy-ridebound/wp14r_host_preflight.py"
)
SPEC = importlib.util.spec_from_file_location(
    "wp14r_host_preflight_under_test",
    MODULE_PATH,
)
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)
FINGERPRINT = "a" * 64
FREEZE_SHA = "b" * 64


def policy():
    return {
        "requiredPlatform": "Windows",
        "requiredHostFingerprintSha256": FINGERPRINT,
        "requiredAcLineStatus": "online",
        "requiredPowerSchemeGuid": (
            "381b4222-f694-41f0-9685-ff5bb260df2e"
        ),
        "sampleCount": 10,
        "sampleIntervalMs": 1000,
        "maximumMeanCpuBusyPercent": 20,
        "maximumSingleCpuBusyPercent": 60,
        "minimumAvailableMemoryBytes": 8 * 1024**3,
        "minimumFreeDiskBytes": 25 * 1024**3,
        "arbitraryProcessNamesOrCommandLinesRecorded": False,
    }


def freeze():
    return {
        "freezeId": "wp14r-resilient-development-v2",
        "protocol": {"hostPolicy": policy()},
    }


class Wp14RHostPreflightTests(unittest.TestCase):
    def test_cpu_busy_uses_windows_kernel_counter_semantics(self):
        previous = (100, 300, 200)
        current = (140, 380, 260)
        self.assertEqual(71.429, MODULE.cpu_busy_percent(previous, current))
        with self.assertRaisesRegex(
            MODULE.HostPreflightError,
            "inconsistently",
        ):
            MODULE.cpu_busy_percent(current, previous)

    def test_sampling_uses_exact_count_and_interval(self):
        snapshots = iter(
            [
                (0, 100, 0),
                (50, 200, 0),
                (100, 300, 0),
            ]
        )
        sleeps = []
        samples = MODULE.sample_cpu_busy(
            2,
            250,
            times_reader=lambda: next(snapshots),
            sleeper=sleeps.append,
        )
        self.assertEqual([50.0, 50.0], samples)
        self.assertEqual([0.25, 0.25], sleeps)

    def test_exact_pass_has_no_failure_code(self):
        decision = MODULE.evaluate(
            policy(),
            "Windows",
            FINGERPRINT,
            "online",
            "381b4222-f694-41f0-9685-ff5bb260df2e",
            [10] * 10,
            9 * 1024**3,
            26 * 1024**3,
        )
        self.assertEqual("pass", decision["status"])
        self.assertEqual([], decision["failureCodes"])

    def test_every_host_axis_fails_typed_and_no_process_name_is_recorded(self):
        decision = MODULE.evaluate(
            policy(),
            "Linux",
            "c" * 64,
            "offline",
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            [70] * 10,
            1,
            1,
        )
        self.assertEqual("fail", decision["status"])
        self.assertEqual(
            {
                "PLATFORM_MISMATCH",
                "HOST_FINGERPRINT_MISMATCH",
                "POWER_SOURCE_NOT_AC",
                "POWER_SCHEME_MISMATCH",
                "CPU_MEAN_ABOVE_LIMIT",
                "CPU_SAMPLE_ABOVE_LIMIT",
                "MEMORY_BELOW_MINIMUM",
                "DISK_BELOW_MINIMUM",
            },
            set(decision["failureCodes"]),
        )
        self.assertNotIn("process", json.dumps(decision).lower())

    def test_single_cpu_spike_is_not_hidden_by_the_mean(self):
        values = [1] * 9 + [61]
        decision = MODULE.evaluate(
            policy(),
            "Windows",
            FINGERPRINT,
            "online",
            "381b4222-f694-41f0-9685-ff5bb260df2e",
            values,
            9 * 1024**3,
            26 * 1024**3,
        )
        self.assertEqual(
            ["CPU_SAMPLE_ABOVE_LIMIT"],
            decision["failureCodes"],
        )

    def test_collect_preflight_is_strict_schema_valid_and_outcome_free(self):
        receipt = MODULE.collect_preflight(
            freeze(),
            FREEZE_SHA,
            "w14-test-job",
            1,
            pathlib.Path("unused"),
            "2026-08-28T01:02:03Z",
            ("Windows", FINGERPRINT),
            ac_line_status_reader=lambda: "online",
            power_scheme_reader=lambda: (
                "381b4222-f694-41f0-9685-ff5bb260df2e"
            ),
            cpu_sampler=lambda count, interval: [5] * count,
            memory_reader=lambda: 9 * 1024**3,
            disk_reader=lambda path: 26 * 1024**3,
        )
        self.assertEqual("pass", receipt["status"])
        self.assertFalse(receipt["outcomeFieldsRead"])
        self.assertNotIn("completed", json.dumps(receipt).lower())
        self.assertNotIn("burden", json.dumps(receipt).lower())

    def test_wrong_sample_count_fails_before_receipt(self):
        with self.assertRaisesRegex(
            MODULE.HostPreflightError,
            "sample count",
        ):
            MODULE.collect_preflight(
                freeze(),
                FREEZE_SHA,
                "w14-test-job",
                1,
                pathlib.Path("unused"),
                "2026-08-28T01:02:03Z",
                ("Windows", FINGERPRINT),
                ac_line_status_reader=lambda: "online",
                power_scheme_reader=lambda: (
                    "381b4222-f694-41f0-9685-ff5bb260df2e"
                ),
                cpu_sampler=lambda count, interval: [5] * (count - 1),
                memory_reader=lambda: 9 * 1024**3,
                disk_reader=lambda path: 26 * 1024**3,
            )

    def test_power_scheme_parser_ignores_localized_display_name(self):
        completed = mock.Mock(
            returncode=0,
            stdout=(
                "Power Scheme GUID: "
                "381B4222-F694-41F0-9685-FF5BB260DF2E  (Balanced)"
            ),
        )
        with mock.patch.object(
            MODULE.subprocess,
            "run",
            return_value=completed,
        ):
            self.assertEqual(
                "381b4222-f694-41f0-9685-ff5bb260df2e",
                MODULE.read_active_power_scheme(),
            )

    def test_receipt_write_is_exclusive(self):
        with tempfile.TemporaryDirectory() as directory:
            path = pathlib.Path(directory) / "receipt.json"
            MODULE.write_exclusive(path, b"first")
            with self.assertRaises(FileExistsError):
                MODULE.write_exclusive(path, b"second")
            self.assertEqual(b"first", path.read_bytes())


if __name__ == "__main__":
    unittest.main()
