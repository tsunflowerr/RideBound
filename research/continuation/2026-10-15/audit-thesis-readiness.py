"""Read-only trace of the delivered thesis and preserved experiment outputs.

Writes only a new audit directory on C; no experiments, repairs or source edits.
"""
import ast
import collections
import csv
import datetime
import hashlib
import json
import pathlib
import re
import urllib.request
from pypdf import PdfReader

BASE = pathlib.Path('C:/RideBoundContinuation/2026-10-15')
OUT = pathlib.Path('C:/RideBoundContinuation/2026-10-16/thesis-readiness-audit')
OUT.mkdir(parents=True, exist_ok=False)
THESIS = pathlib.Path('E:/Code/Report_INT3508/ridebound')
SCRATCH = pathlib.Path('E:/Code/Report_INT3508/ridebound-scratchpad')
receipt = json.loads((BASE / 'thesis-v5-deployment.json').read_text(encoding='utf-8'))
checks = []


def check(name, got, expected):
    checks.append(dict(name=name, actual=got, expected=expected, passed=got == expected))


for rel, info in receipt['verifiedPayload'].items():
    check('delivered '+rel, hashlib.sha256((THESIS / rel).read_bytes()).hexdigest(), info['sha256'])
for line in (BASE / 'results/stage1b-results.sha256').read_text().splitlines():
    digest, rel = line.split('  ',1)
    check('preserved result '+rel, hashlib.sha256((BASE / rel).read_bytes()).hexdigest(), digest)

reader = PdfReader(THESIS / 'main.pdf')
texts = [p.extract_text() or '' for p in reader.pages]
(OUT / 'thesis-all-pages.txt').write_text('\n\n'.join(f'=== Physical page {i+1} ===\n{t}' for i,t in enumerate(texts)),encoding='utf-8')
topics = ['Lời hứa theo dự báo', 'khoảng tin cậy 95', '34 trong 1.471', '3 trong 32',
          'Vì sao cần đổi', 'Bảy lịch sử', '7 lịch sử', 'không thỏa đủ bốn', 'thời gian thực',
          'chi phí tính toán', '1.033', 'Phần đóng góp', '30 giây là điểm thực nghiệm']
pages = {term:[i+1 for i,t in enumerate(texts) if term in t] for term in topics}

sources = {p.name:p.read_text(encoding='utf-8') for p in (THESIS / 'noi-dung').glob('*.tex')}
anchors = {}
for term in ['khoảng tin cậy', '34 trong 1.471', '3 trong 32', 'đã xem', 'bản chạy riêng',
             'chưa được chạy', 'chưa được cài đặt', 'Đóng góp', 'Phần đóng góp', '1.033',
             'cùng một bản', 'không có dự báo', 'bỏ ngày 15', 'khung 8 hệ số', 'gọi xe']:
    anchors[term] = [dict(file=str(THESIS/'noi-dung'/name),line=i+1,text=line)
                     for name, content in sources.items() for i,line in enumerate(content.splitlines())
                     if term in line and not line.lstrip().startswith('%')]

# Extract only the source-day function and literal constants, without importing
# the frozen project modules or changing their bytecode/environment.
path = SCRATCH / 'optimize/display_layer.py'
tree = ast.parse(path.read_text(encoding='utf-8'))
nodes = [n for n in tree.body if isinstance(n, ast.Assign) and
         any(isinstance(t,ast.Name) and t.id in ['WEEKDAYS','WEEKEND','INCIDENT','MIN_SOURCES'] for t in n.targets)]
nodes += [n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name=='sources']
ns = {}
exec(compile(ast.Module(body=nodes,type_ignores=[]), str(path), 'exec'), ns)
source_days = {d:list(ns['sources'](d,'causal-no15')) for d in ns['WEEKDAYS']+ns['WEEKEND']}
check('primary history uses only past dates', all(all(s<d for s in ss) for d,ss in source_days.items()), True)

data = json.loads((BASE / 'results/stage1b-runs.json').read_text())
rows = list(csv.DictReader((BASE / 'results/stage1b-riders.tsv').open(encoding='utf-8'),delimiter='\t'))
roles = ['SF-U','SF-C30','CF-U','CF-C30']
counts = {}
for role in roles:
    rr = [r for r in rows if r['role']==role and r['served']=='1']
    counts[role] = dict(served=len(rr),late60=sum(int(r['A'])-int(r['p0'])>60000 for r in rr),
        forecastLate60=sum(int(r['A'])-float(r['D0'])>60000 for r in rr),
        revised=sum(int(r['sumAbsC'])>0 for r in rr), repeated=sum(int(r['nonzeroC'])>=2 for r in rr),
        shared=sum(r['shared']=='1' for r in rr),
        activeCells=sum(r['forecastActive'] for r in data['runs'] if r['role']==role))
    check(role+' active forecast cells',counts[role]['activeCells'],21)

run = next(r for r in data['runs'] if r['role']=='CF-C30')
run_path = pathlib.Path(run['run'])
sample_run = dict(path=str(run_path),exists=run_path.exists(), files=[])
if run_path.exists():
    sample_run['files'] = [p.name for p in run_path.iterdir() if p.is_file()]
    for p in run_path.rglob('summary.json'):
        s = json.loads(p.read_text(encoding='utf-8-sig'))
        sample_run['summaryKeys']=list(s)
        sample_run['timingFields']={k:v for k,v in s.items() if any(x in k.lower() for x in ['time','duration','elapsed'])}
        break

downloads = {}
urls = {
    'uet-format-standard-clc.pdf':'https://uet.vnu.edu.vn/wp-content/uploads/2017/07/Quy-%C4%91i%CC%A3nh-v%C3%AA%CC%80-tri%CC%80nh-ba%CC%80y-kho%CC%81a-lu%C3%A2%CC%A3n-t%C3%B4%CC%81t-nghi%C3%AA%CC%A3pCLC.pdf',
    'horl-zwick-eth.pdf':'https://www.research-collection.ethz.ch/bitstreams/e0796b23-e061-4f00-a96a-1cb6b44ccb0f/download',
}
for name,url in urls.items():
    try:
        req=urllib.request.Request(url,headers={'User-Agent':'Mozilla/5.0'})
        with urllib.request.urlopen(req,timeout=25) as response:
            raw=response.read()
            assert raw.startswith(b'%PDF'), response.headers.get('Content-Type')
        (OUT/name).write_bytes(raw)
        rr=PdfReader(OUT/name)
        text='\n\f\n'.join(p.extract_text() or '' for p in rr.pages)
        (OUT/(name+'.txt')).write_text(text,encoding='utf-8')
        downloads[name]=dict(url=url,sha256=hashlib.sha256(raw).hexdigest(),pages=len(rr.pages))
    except Exception as exc:
        downloads[name]=dict(url=url,error=str(exc))

result=dict(created=datetime.datetime.now(datetime.timezone(datetime.timedelta(hours=7))).isoformat(),
    scope=__doc__, allMechanicalChecksPassed=all(c['passed'] for c in checks),checks=checks,
    pdfSha256=receipt['pdfSha256'],pages=len(texts),topicPages=pages,anchors=anchors,
    forecastSourceDays=source_days,counts=counts,sampleRun=sample_run,downloads=downloads)
(OUT/'trace-evidence.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({k:v for k,v in result.items() if k not in ['anchors','checks','sampleRun']},ensure_ascii=False,indent=2))
