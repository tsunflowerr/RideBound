"""Read-only independent arithmetic check of the frozen extraction and report.

This does not rederive rider rows/forecasts from raw transcripts and is not a new
analysis specification. It checks the prescribed counts, identities and gates.
No project analysis module is imported; all inputs remain unchanged.
"""
import collections
import csv
import hashlib
import io
import json
import pathlib
import re

ROOT = pathlib.Path('C:/RideBoundContinuation/2026-10-15')
RESULTS = ROOT / 'results'
ROLES = ('CF-U', 'CF-C30', 'SF-U', 'SF-C30')
doc = json.loads((RESULTS / 'stage1b-runs.json').read_text(encoding='utf-8'))
raw = (RESULTS / 'stage1b-riders.tsv').read_bytes()
report = (RESULTS / 'stage1b-report.txt').read_text(encoding='utf-8')
checks = []


def check(label, actual, expected):
    checks.append({'check': label, 'actual': actual, 'expected': expected, 'pass': actual == expected})


check('TSV SHA256', hashlib.sha256(raw).hexdigest(), doc['ridersTsv']['sha256'])
check('real main mode', [doc['mode'], doc['dryRun']], ['main', False])
check('real role sources', doc['roleSources'], {role: role for role in ROLES})
rows = list(csv.DictReader(io.StringIO(raw.decode('utf-8')), delimiter='\t'))
check('TSV rows', len(rows), doc['ridersTsv']['rows'])
by_role = {role: [r for r in rows if r['role'] == role] for role in ROLES}
check('four roles only', set(r['role'] for r in rows), set(ROLES))
check('34 cells', len(doc['cells']), 34)
check('136 role-runs', len(doc['runs']), 136)
check('no duplicate rider IDs per role/cell', len({(r['role'], r['cell'], r['requestId']) for r in rows}), len(rows))


def counts(rs, requests):
    served = [r for r in rs if r['served'] == '1']
    result = {'requests': requests, 'accepted': len(rs), 'served': len(served)}
    for threshold in (60, 120, 300):
        result[f'late{threshold}'] = sum(int(r['A']) - int(r['p0']) > threshold * 1000 for r in served)
        result[f'fc_late{threshold}'] = sum(int(r['A']) - float(r['D0']) > threshold * 1000 for r in served)
        result[f'timely{threshold}'] = len(served) - result[f'late{threshold}']
        result[f'fc_timely{threshold}'] = len(served) - result[f'fc_late{threshold}']
    result['shared'] = sum(r['shared'] == '1' for r in served)
    result['exposed'] = sum(int(r['sumAbsC']) > 0 for r in rs)
    result['repeated'] = sum(int(r['nonzeroC']) >= 2 for r in rs)
    result['h7_served'] = sum(int(r['maxAbsC']) <= 120000 < int(r['sumAbsC']) for r in served)
    result['over30'] = sum(int(r['sumAbsC']) > 30000 for r in rs)
    result['maxSumAbsC_ms'] = max((int(r['sumAbsC']) for r in rs), default=0)
    for suffix, cohort in (('accepted', rs), ('served', served)):
        horizons = sorted(int(r['p0']) - int(r['t0']) for r in cohort)
        result['horizon_nearest_rank_median_ms_' + suffix] = horizons[(len(horizons) - 1) // 2] if horizons else None
    return result


M = {}
for role in ROLES:
    runs = [run for run in doc['runs'] if run['role'] == role]
    check(role + ' runs', len(runs), 34)
    requests = sum(run['requests'] for run in runs)
    check(role + ' requests', requests, 3672)
    M[role] = counts(by_role[role], requests)
    check(role + ' completed has alight', all(r['lifecycle'] != 'completed' or r['served'] == '1' and r['A'] for r in by_role[role]), True)
    match = re.search(r'^' + re.escape(role) + r'\s*\|\s*(\d+) / (\d+).*?\|\s*(\d+) / (\d+).*?\|\s*(\d+) / (\d+)', report, re.M)
    actual = [int(value) for value in match.groups()] if match else None
    check(role + ' primary table counts', actual, [M[role]['served'], requests, M[role]['late60'], M[role]['served'], M[role]['timely60'], requests])


def interaction(metrics, field):
    return metrics['CF-U'][field] - metrics['CF-C30'][field] - metrics['SF-U'][field] + metrics['SF-C30'][field]


interaction_values = {field: interaction(M, field) for field in ('late60', 'served', 'timely60')}
check('pooled exact identity', interaction_values['late60'], interaction_values['served'] - interaction_values['timely60'])
for line_name, field in (('I_L', 'late60'), ('I_served', 'served'), ('I_timely60', 'timely60')):
    match = re.search(r'^' + line_name + r'\s*=.*= ([+-]\d+)', report, re.M)
    check(line_name + ' matches report', int(match[1]) if match else None, interaction_values[field])
cell_interactions = {}
for cell in doc['cells']:
    metrics = {role: counts([r for r in by_role[role] if r['cell'] == cell], 108) for role in ROLES}
    val = {field: interaction(metrics, field) for field in ('late60', 'served', 'timely60')}
    check(cell + ' exact identity', val['late60'], val['served'] - val['timely60'])
    cell_interactions[cell] = val
for group in sorted({cell[:9] for cell in doc['cells']}) + ['w07', 'w08', 'w17']:
    selected = [cell_interactions[cell] for cell in doc['cells'] if cell.startswith(group) or cell.endswith(group)]
    totals = {field: sum(v[field] for v in selected) for field in ('late60', 'served', 'timely60')}
    check(group + ' exact identity', totals['late60'], totals['served'] - totals['timely60'])

u, cap = M['CF-U'], M['CF-C30']
positive = sum(v['late60'] > 0 for cell, v in cell_interactions.items() if cell.endswith(('w08', 'w17')))
expected_gates = {
    'H2': u['exposed'] >= 261 and u['late60'] >= 243,
    'H3': (cap['late60'] * 10000 <= 425 * cap['served'] and cap['fc_late60'] * 100 <= cap['served']) if cap['served'] > 0 else None,
    'H4': interaction_values['late60'] >= 40 and positive >= 15,
    'H5': cap['fc_timely60'] - u['fc_timely60'] >= 31,
    'H6': cap['shared'] >= 345 and cap['served'] >= 2315,
    'H7': u['h7_served'] >= 10,
}
for label, expected in expected_gates.items():
    match = re.search(r'^' + label + r'\s+(PASS|FAIL|NOT EVALUABLE)', report, re.M)
    check(label + ' matches frozen arithmetic', match[1] if match else None,
          'NOT EVALUABLE' if expected is None else 'PASS' if expected else 'FAIL')
overshoot_match = re.search(r'^H1\s+.*riders with S_r > 30 s (\d+) / (\d+)', report, re.M)
check('C30 overshoot count matches report', [int(v) for v in overshoot_match.groups()] if overshoot_match else None,
      [cap['over30'], cap['accepted']])
check('SF-U served', M['SF-U']['served'], 2434)
check('SF-C30 served', M['SF-C30']['served'], 2338)
check('SF-U late60', M['SF-U']['late60'], 203)
check('SF-C30 late60', M['SF-C30']['late60'], 76)
check('baseline section passed', 'BASELINES ALL REPRODUCED' in report, True)
check('ledger accounting valid', 'ledger accounting validity: PASS' in report, True)

def jsonable(value):
    if isinstance(value, set):
        return sorted(value)
    raise TypeError(type(value).__name__)

result = {'scope': 'Independent arithmetic on frozen extraction; no raw/forecast rederivation',
          'allPassed': all(c['pass'] for c in checks), 'checkCount': len(checks), 'checks': checks,
          'pooled': M, 'interactions': interaction_values, 'positiveW0817Cells': positive,
          'expectedGates': expected_gates, 'cellInteractions': cell_interactions}
with (ROOT / 'evidence' / 'analysis-independent-arithmetic-check.json').open('x', encoding='utf-8') as handle:
    json.dump(result, handle, indent=2, default=jsonable)
    handle.write('\n')
print(json.dumps({'allPassed': result['allPassed'], 'checks': len(checks), 'failures': [c for c in checks if not c['pass']],
                  'pooled': M, 'interactions': interaction_values, 'gates': expected_gates}, default=jsonable))
raise SystemExit(0 if result['allPassed'] else 1)
