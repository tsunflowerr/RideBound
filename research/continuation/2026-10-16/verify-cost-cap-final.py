"""Read-only independent arithmetic check of the completed extraction, not a raw decoder.

Run only after the native driver exits 0 and analysis-complete.json exists.
No simulation, analysis rerun, output repair, significance test or thesis edit.
"""
from pathlib import Path
from decimal import Decimal, ROUND_CEILING
from collections import defaultdict
import csv
import datetime
import hashlib
import json
import re

CAP = Path('C:/RideBoundContinuation/2026-10-16/cost-cap-sensitivity-v1')
ROOT = CAP.parent
OUT = Path('C:/RideBoundData/research/tier3-cost-cap-sensitivity-v1')
OLD = Path('E:/Code/Report_INT3508/ridebound-scratchpad/tang3/stage1b/harness')
PRIOR = Path('C:/RideBoundContinuation/2026-10-15/results')
PIN = 'cedecbb1a97d31ec8ab295fd16c99fb607f14d622779247d8df6d9b219b42d6f'
ROLES = ('CF-C30', 'CF-C60', 'CF-C120', 'CF-U')
checks = []

def sha(path):
    h = hashlib.sha256()
    with open(path, 'rb') as f:
        for chunk in iter(lambda: f.read(1048576), b''):
            h.update(chunk)
    return h.hexdigest()

def check(label, actual, expected):
    checks.append({'label': label, 'pass': actual == expected,
                   'actual': actual, 'expected': expected})

def metrics(rows, records):
    finished = [r for r in rows if r['served']]
    delays = [r['A'] - r['p0'] for r in finished]
    budgets = sorted(r['sumAbsC'] for r in finished)
    index = int((Decimal('0.95') * len(budgets)).to_integral_value(rounding=ROUND_CEILING)) - 1
    return {
        'requests': sum(r['requests'] for r in records), 'served': len(finished),
        'late60': sum(delay > 60000 for delay in delays),
        'timely60': sum(delay <= 60000 for delay in delays),
        'timely120': sum(delay <= 120000 for delay in delays),
        'timely300': sum(delay <= 300000 for delay in delays),
        'forecastLate60': sum(Decimal(r['A']) - r['D0'] > 60000 for r in finished),
        'shared': sum(r['shared'] for r in finished),
        'revisedServed': sum(r['nonzeroC'] > 0 for r in finished),
        'repeatedServed': sum(r['nonzeroC'] >= 2 for r in finished),
        'latePickups': sum(r['latePickups'] for r in records),
        'p95C_ms': budgets[index] if budgets else 0,
        'maxC_ms': max((r['sumAbsC'] for r in rows), default=0),
        'ledgerMismatches': sum(len(r['ledgerBudgetMismatches']) for r in records),
    }

def main():
    text = (CAP / 'run-20261016-1401.log').read_text(encoding='utf-8').strip()
    if not text.splitlines()[-1].startswith('exit=0 at '):
        raise SystemExit('not ready: native launcher has not exited 0')
    if not (CAP / 'analysis-complete.json').is_file():
        raise SystemExit('not ready: analysis-complete.json absent')
    manifest = json.loads((CAP / 'study-manifest.json').read_text())
    check('study manifest identity', sha(CAP / 'study-manifest.json'), PIN)
    for path, want in manifest['files'].items():
        check('frozen ' + path, sha(path), want)
    stops = [str(p) for root in (CAP, OLD) for p in root.glob('*-FAILED.txt')]
    stops += [str(p) for root in (CAP, OLD) for p in (root/'STOP', root/'INPUT-DRIFT-STOP.txt') if p.exists()]
    policy_stop = Path('C:/RideBoundContinuation/2026-10-15/resource30-v3/RESOURCE-POLICY-FAILED.txt')
    if policy_stop.exists():
        stops.append(str(policy_stop))
    check('no stop/failure receipts', stops, [])
    complete = json.loads((CAP/'analysis-complete.json').read_text())
    check('analysis completion binding', complete['manifestSha256'], PIN)
    result = CAP / 'results'
    check('result file set', sorted(p.name for p in result.iterdir()),
          sorted(['REPORT.md', 'results.json', 'riders.tsv', 'results.sha256']))
    for name, want in complete['files'].items():
        check('completion hash ' + name, sha(result/name), want)
    for line in (result/'results.sha256').read_text().splitlines():
        want, name = line.split('  ', 1)
        check('result manifest hash ' + name, sha(result/name), want)
    done = json.loads((CAP/'main-complete.json').read_text())
    check('main completion binding', done['manifestSha256'], PIN)
    check('main completion count', len(done['bundles']), 68)
    check('main folder set', sorted(p.name for p in (OUT/'main').iterdir()), sorted(done['bundles']))
    for job, want in done['bundles'].items():
        check('new bundle binding ' + job, sha(OUT/'main'/job/'bundle-manifest.json'), want)
        start = json.loads((CAP/'logs/main'/(job+'.started.json')).read_text())
        finish = json.loads((CAP/'logs/main'/(job+'.finished.json')).read_text())
        check('job admission binding ' + job, start['manifestSha256'], PIN)
        check('job finish binding ' + job, finish['manifestSha256'], PIN)
        check('job native exit ' + job, finish['returncode'], 0)
        check('job bundle receipt ' + job, finish['bundleManifestSha256'], want)
    output = json.loads((result/'results.json').read_text())
    check('result study binding', output['manifestSha256'], PIN)
    cells = manifest['cells']
    records = output['runs']
    check('role/run count', len(records), 136)
    index = {(r['role'], r['cell']): r for r in records}
    check('unique role/cell count', len(index), 136)
    check('role/cell coverage', sorted(index), sorted((r, c) for r in ROLES for c in cells))
    with open(result/'riders.tsv', encoding='utf-8', newline='') as f:
        rows = list(csv.DictReader(f, delimiter='\t'))
    for row in rows:
        for name in ('t0', 'p0', 'served', 'sumAbsC', 'nonzeroC', 'netC', 'maxAbsC', 'shared'):
            row[name] = int(row[name])
        row['D0'] = Decimal(row['D0'])
        row['A'] = int(row['A']) if row['A'] else None
    check('rider keys unique', len({(r['role'],r['cell'],r['requestId']) for r in rows}), len(rows))
    check('valid served rows', sum(r['served'] and (r['A'] is None or r['lifecycle']!='completed') for r in rows), 0)
    check('finite forecast timestamps', all(r['D0'].is_finite() for r in rows), True)
    check('nonnegative cumulative budgets', all(r['sumAbsC']>=abs(r['netC']) and r['sumAbsC']>=r['maxAbsC']>=0 for r in rows), True)
    by_cell = defaultdict(list)
    for row in rows:
        by_cell[(row['role'], row['cell'])].append(row)
    per_cell = {(r['role'],r['cell']):r for r in output['perCell']}
    per_day = {(r['role'],r['day']):r for r in output['perDay']}
    check('cell table unique rows', len(per_cell), 136)
    check('day table unique rows', len(per_day), len(ROLES)*len({c[:9] for c in cells}))
    dll = '1839a54413b2729cf353db7ef8b2359d32f4698709bba75a96eca492e36c69a9'
    for cell in cells:
        base = index[('CF-C30',cell)]
        for role in ROLES:
            rec = index[(role,cell)]
            check('requests '+role+cell, rec['requests'], 108)
            for key in ('requestIdsSha256','sourceScenarioContentSha256'):
                check(key+role+cell, rec[key], base[key])
            check('same runner '+role+cell, rec['manifest']['binarySha256'], dll)
            for key in ('scenarioContentHash','graphSnapshotHash','travelTimeSnapshotHash'):
                check(key+role+cell, rec['manifest'][key], base['manifest'][key])
            check('ledger accounting '+role+cell, rec['ledgerBudgetMismatches'], [])
            for key in ('completedWithoutAlight','alightedNotCompleted','alightedWithoutPromise'):
                check(key+role+cell, rec[key], 0)
            actual = metrics(by_cell[(role,cell)], [rec])
            check('cell served extraction '+role+cell, actual['served'], rec['served'])
            for key,value in actual.items():
                check('cell metric '+role+cell+key, per_cell[(role,cell)][key], value)
    totals = {}
    for role in ROLES:
        role_rows = [r for r in rows if r['role']==role]
        role_records = [r for r in records if r['role']==role]
        totals[role] = metrics(role_rows, role_records)
        for key,value in totals[role].items():
            check('total metric '+role+key, output['totals'][role][key], value)
        if role!='CF-U':
            cap_ms = int(role.removeprefix('CF-C'))*1000
            check('cap overshoots all promised '+role, sum(r['sumAbsC']>cap_ms for r in role_rows), 0)
        for day in sorted({c[:9] for c in cells}):
            actual = metrics([r for r in role_rows if r['cell'][:9]==day],
                             [r for r in role_records if r['cell'][:9]==day])
            for key,value in actual.items():
                check('day metric '+role+day+key, per_day[(role,day)][key], value)
    anchors = {'CF-C30':{'served':2364,'late60':72,'timely60':2292,'timely120':2353,'timely300':2364,
                         'forecastLate60':11,'shared':432,'revisedServed':106,'repeatedServed':2,'latePickups':44},
               'CF-U':{'served':2502,'late60':379,'timely60':2123,'timely120':2280,'timely300':2466,
                        'forecastLate60':318,'shared':1027,'revisedServed':434,'repeatedServed':50,'latePickups':39}}
    for role, anchor in anchors.items():
        for key,value in anchor.items():
            check('preserved baseline '+role+key, totals[role][key], value)
    failed = [c for c in checks if not c['pass']]
    report = {'schema':'cost-cap-final-independent-arithmetic-v1',
              'verifiedAt':datetime.datetime.now(datetime.timezone(datetime.timedelta(hours=7))).isoformat(),
              'checkerSha256':sha(__file__), 'manifestSha256':PIN, 'checks':len(checks),
              'passed':len(checks)-len(failed),'failed':failed, 'totals':totals,
              'inputs':{name:sha(result/name) for name in ('REPORT.md','results.json','riders.tsv','results.sha256')},
              'scope':'Independent extraction-arithmetic, identity and receipt checks. Not independent raw-ledger or forecast decoding; not significance or generalization evidence.',
              'details':checks}
    target = ROOT/'cost-cap-final-independent-arithmetic.json'
    with target.open('x',encoding='utf-8') as f:
        f.write(json.dumps(report,ensure_ascii=False,indent=1)+'\n')
    print(json.dumps({key:report[key] for key in ('checks','passed','failed','totals')},ensure_ascii=False))
    if failed:
        raise SystemExit(1)

if __name__=='__main__':
    main()
