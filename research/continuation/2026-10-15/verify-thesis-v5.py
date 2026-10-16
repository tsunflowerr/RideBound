"""Check newly incorporated thesis numbers against preserved extraction outputs.

Read-only on the study and thesis sources. This is arithmetic verification, not
an independent decoder of raw simulation transcripts or forecast profiles.
"""
import csv
import hashlib
import json
import re
from pathlib import Path

ROOT = Path('C:/RideBoundContinuation/2026-10-15')
THESIS = ROOT / 'thesis-v5'
checks = []


def check(name, actual, expected):
    checks.append(dict(name=name, actual=actual, expected=expected, passed=actual == expected))


for line in (ROOT / 'results/stage1b-results.sha256').read_text().splitlines():
    digest, rel = line.split('  ', 1)
    check('preserved result inventory: ' + rel, hashlib.sha256((ROOT / rel).read_bytes()).hexdigest(), digest)

rows = list(csv.DictReader((ROOT / 'results/stage1b-riders.tsv').open(encoding='utf-8'), delimiter='\t'))
runs = json.loads((ROOT / 'results/stage1b-runs.json').read_text())['runs']
roles = ['SF-U', 'SF-C30', 'CF-U', 'CF-C30']
metrics = {}
for role in roles:
    rr = [r for r in rows if r['role'] == role]
    served = [r for r in rr if r['served'] == '1']
    m = dict(served=len(served), shared=sum(r['shared'] == '1' for r in served),
             exposed=sum(int(r['sumAbsC']) > 0 for r in rr),
             repeated=sum(int(r['nonzeroC']) >= 2 for r in rr),
             pickup=sum(r['latePickups'] for r in runs if r['role'] == role),
             forecastLate=sum(int(r['A']) - float(r['D0']) > 60000 for r in served),
             maxC=max(int(r['sumAbsC']) for r in rr),
             witnesses120=sum(int(r['maxAbsC']) <= 120000 < int(r['sumAbsC']) for r in served),
             extension=sum(float(r['D0']) - int(r['p0']) for r in served)/len(served)/1000,
             medianHorizon=sorted(int(r['p0']) - int(r['t0']) for r in served)[(len(served)-1)//2]/1000)
    for t in (60, 120, 300):
        m['late'+str(t)] = sum(int(r['A']) - int(r['p0']) > t*1000 for r in served)
        m['timely'+str(t)] = len(served) - m['late'+str(t)]
    metrics[role] = m

src = (THESIS / 'noi-dung/08-chuong4.tex').read_text(encoding='utf-8')
table = src[src.index('\\label{bang:c4-doi-uutien}'):]
table = table[:table.index('\\end{table}')]
labels = {
    'Lượt phục vụ': 'served', 'Muộn quá một phút': 'late60',
    'Không muộn quá một phút': 'timely60', 'Không muộn quá hai phút': 'timely120',
    'Không muộn quá năm phút': 'timely300', 'Khách hoàn thành có đi chung': 'shared',
    'Khách bị sửa giờ trả do điều phối': 'exposed', 'Trong đó, bị sửa từ hai lần': 'repeated',
    'Đón muộn theo giới hạn dịch vụ': 'pickup',
    'Muộn quá một phút khi lời hứa có dự báo': 'forecastLate',
    'Tổng dời lớn nhất của một khách (s)': 'maxC',
}
for label, metric in labels.items():
    match = re.search(r'^' + re.escape(label) + r' & (.+?)\\\\', table, re.M)
    actual = [v.strip() for v in match[1].split('&')] if match else None
    expected = [f'{metrics[r][metric]:,}'.replace(',', '.') for r in roles] if metric != 'maxC' else [f'{metrics[r][metric]/1000:.1f}'.replace('.', ',') for r in roles]
    check('table: ' + label, actual, expected)

cf, sf, u = metrics['CF-C30'], metrics['SF-C30'], metrics['CF-U']
for key, expected in {'served': 26, 'shared': 119, 'late60': -4, 'timely60': 30, 'exposed': 73, 'repeated': 2, 'pickup': 1, 'forecastLate': 1}.items():
    check('capped selector contrast: '+key, cf[key]-sf[key], expected)
for key, expected in {'served': -138, 'shared': -595, 'late60': -307, 'timely60': 169}.items():
    check('cost-first cap contrast: '+key, cf[key]-u[key], expected)
check('stable revised percentage', f"{sf['exposed']/sf['served']*100:.2f}", '1.41')
check('cost-first revised percentage', f"{cf['exposed']/cf['served']*100:.2f}", '4.48')
check('cost-first forecast extension seconds', f"{cf['extension']:.2f}", '7.30')
check('uncapped cost-first forecast extension seconds', f"{u['extension']:.2f}", '7.02')
check('cost-first initial horizon median seconds', cf['medianHorizon'], 809.642)
check('stable initial horizon median seconds', sf['medianHorizon'], 815.060)
check('cost-first hypothetical 120s rider histories', u['witnesses120'], 7)
check('C30 cumulative-only prune records', sum(len(r['cumulativeOnlyOwn']) for r in runs if r['role'] == 'CF-C30'), 1)
old = json.loads((ROOT / 'evidence/analysis-independent-arithmetic-check.json').read_text())
check('preserved arithmetic checks pass', [old['allPassed'], old['checkCount']], [True, 83])
visible = '\n'.join(re.sub(r'(?<!\\)%.*', '', p.read_text(encoding='utf-8')) for p in (THESIS / 'noi-dung').glob('*.tex'))
check('no internal stage labels in visible thesis source', bool(re.search(r'\bstage\s*[12]', visible, re.I)), False)
check('no old/new selector framing', bool(re.search(r'bộ chọn (?:cũ|mới)', visible, re.I)), False)
result = dict(scope=__doc__, allPassed=all(c['passed'] for c in checks), checkCount=len(checks), checks=checks, metrics=metrics)
out = THESIS / 'thesis-numeric-check.json'
out.write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
print(json.dumps(dict(allPassed=result['allPassed'], checkCount=len(checks), failures=[c for c in checks if not c['passed']]), ensure_ascii=False, indent=2))
raise SystemExit(0 if result['allPassed'] else 1)
