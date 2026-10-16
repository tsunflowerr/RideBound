"""Resource-boundary checks against the actual unchanged v7 Admission implementation."""
import unittest
from types import SimpleNamespace
from unittest.mock import patch

import resource30_driver as policy


class ResourcePolicyTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.driver = policy.load_driver()

    def setUp(self):
        self.free = 30_720 + 410
        self.stop = False
        self.ac = True
        self.latches = []
        self.drift = []
        self.recorded = []
        self.replacement = patch.dict(self.driver, {
            'free_mib': lambda: self.free,
            'STOP_FILE': SimpleNamespace(exists=lambda: self.stop),
            'ac_only': lambda: self.ac,
            'latch_problems': lambda phase: self.latches,
            'stat_drift': lambda snapshot: self.drift,
            'record_input_drift': lambda *args: self.recorded.append(args),
        })
        self.replacement.start()
        self.addCleanup(self.replacement.stop)
        self.admission = self.driver['Admission']('main', {})

    def test_exact_floor_and_reservation(self):
        self.assertEqual((self.driver['FLOOR_MIB'], self.driver['NEED_MIB']), (30_720, 410))
        self.assertIsNone(self.admission.admit('one'))
        self.assertEqual(self.admission.reserved, 410)

    def test_below_floor_refuses(self):
        self.free = 30_719
        self.assertTrue(self.admission.admit('one').startswith('deferred-disk'))

    def test_floor_alone_refuses(self):
        self.free = 30_720
        self.assertTrue(self.admission.admit('one').startswith('deferred-disk'))

    def test_one_mib_short_refuses(self):
        self.free -= 1
        self.assertTrue(self.admission.admit('one').startswith('deferred-disk'))

    def test_six_jobs_cannot_spend_same_space(self):
        self.free = 30_720 + 6 * 410
        for index in range(6):
            self.assertIsNone(self.admission.admit(str(index)))
        self.assertTrue(self.admission.admit('seventh').startswith('deferred-disk'))
        self.admission.release()
        self.assertIsNone(self.admission.admit('after-release'))
        self.assertEqual(self.admission.reserved, 6 * 410)

    def test_battery_refuses_without_reservation(self):
        self.ac = False
        self.assertTrue(self.admission.admit('one').startswith('deferred-on-battery'))
        self.assertEqual(self.admission.reserved, 0)

    def test_stop_refuses(self):
        self.stop = True
        self.assertEqual(self.admission.admit('one'), 'deferred-stop')

    def test_failure_latch_refuses(self):
        self.latches = ['existing-failure']
        self.assertEqual(self.admission.admit('one'), 'deferred-latch')

    def test_input_drift_is_still_recorded(self):
        self.drift = ['changed-frozen-file']
        self.assertEqual(self.admission.admit('one'), 'stopped-input-drift')
        self.assertEqual(len(self.recorded), 1)
        self.assertEqual(self.admission.reserved, 0)

    def test_stop_arriving_during_scan_refuses(self):
        def scan(snapshot):
            self.stop = True
            return []
        self.driver['stat_drift'] = scan
        self.assertTrue(self.admission.admit('one').startswith('deferred-stop'))

    def test_latch_arriving_during_scan_refuses(self):
        def scan(snapshot):
            self.latches = ['new-failure']
            return []
        self.driver['stat_drift'] = scan
        self.assertTrue(self.admission.admit('one').startswith('deferred-latch'))

    def test_completed_job_is_skipped_without_child_launch(self):
        item = next(item for item in self.driver['jobs']('main')
                    if item[0] == 'CF-U-d20181115-s10-r1-w08')
        with patch.object(self.driver['subprocess'], 'run', side_effect=AssertionError('must not launch')):
            self.assertEqual(self.driver['run_job']('main', *item), (item[0], 'status-pass'))


if __name__ == '__main__':
    unittest.main()
