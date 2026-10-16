import copy
import json
from pathlib import Path
import unittest
import driver
import analysis

class AdmissionTests(unittest.TestCase):
    def test_exact_reservation_boundary(self):
        self.assertIsNone(driver.admission_reason(30720+410,40960,0,True,False))
        self.assertEqual(driver.admission_reason(30720+409,40960,0,True,False),'disk-C')
    def test_concurrent_reservations_count(self):
        self.assertIsNone(driver.admission_reason(30720+2460,40960,2050,True,False))
        self.assertEqual(driver.admission_reason(30720+2460,40960,2460,True,False),'disk-C')
    def test_E_floor(self):self.assertEqual(driver.admission_reason(999999,40959,0,True,False),'disk-E')
    def test_ac_only(self):self.assertEqual(driver.admission_reason(999999,999999,0,False,False),'battery')
    def test_stop_takes_priority(self):self.assertEqual(driver.admission_reason(0,0,0,False,True),'stop')

class ContractTests(unittest.TestCase):
    def setUp(self):
        self.root=Path('E:/Code/Report_INT3508/ridebound-scratchpad/tang3/configs')
        self.original=(self.root/'C-30.json').read_bytes()
    def test_only_cap_changes(self):
        for arm in driver.ARMS:self.assertIsNone(driver.config_problem(arm,(self.root/(arm+'.json')).read_bytes(),self.original))
    def test_unrelated_change_is_refused(self):
        value=json.loads((self.root/'C-60.json').read_bytes());value['policies'][0]['budgetBasis']='total'
        self.assertIsNotNone(driver.config_problem('C-60',json.dumps(value),self.original))
    def test_wrong_cap_is_refused(self):self.assertEqual(driver.config_problem('C-120',(self.root/'C-60.json').read_bytes(),self.original),'wrong hardLimit')
    def test_original_binding_reproduced(self):
        wp4=(driver.OLD/'wp4-tier3-v2-costfirst.json').read_bytes()
        self.assertEqual(analysis.binding(self.original,wp4),'e6b33be5649dc2b1208b42b90cef227d00721cba59e4340fdbc4e96f21796f76')
    def test_grid_is_exact(self):
        jobs=driver.items('main');self.assertEqual(len(jobs),68);self.assertEqual(len({x[0] for x in jobs}),68)
        self.assertEqual(len({x[2] for x in jobs}),34);self.assertEqual(len(driver.items('smoke')),2)
    def test_original_output_root_untouched(self):
        b=driver.base();self.assertEqual(b.OUT_ROOT,Path('C:/RideBoundData/research/tier3-stage1b-v1'))
        name,arm,cell=driver.items('main')[0];cmd=driver.command('main',name,arm,cell)
        self.assertEqual(Path(cmd[cmd.index('--output')+1]),driver.OUT/'main'/name)
        self.assertEqual(Path(cmd[cmd.index('--runner-root')+1]),b.RUNNER)
        self.assertEqual(Path(cmd[cmd.index('--wp4-config')+1]),b.WP4_ON)

class MetricTests(unittest.TestCase):
    def test_boundary_and_served_semantics(self):
        def row(a,served=1):return dict(served=served,A=a,p0=0,D0=30000,shared=1,nonzeroC=2,sumAbsC=1000)
        t=analysis.summarize([row(60000),row(60001),row('',0)],[dict(requests=3,latePickups=1,ledgerBudgetMismatches=[])])
        self.assertEqual((t['served'],t['late60'],t['timely60'],t['forecastLate60']),(2,1,1,0))
    def test_quantile_is_predeclared(self):self.assertEqual(analysis.quantile_nearest(list(range(1,101)),.95),95)

if __name__=='__main__':unittest.main()
