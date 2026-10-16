"""Fixed descriptive analysis; uses the original authenticated run reader unchanged."""
from __future__ import annotations
import argparse
from concurrent.futures import ProcessPoolExecutor
import csv
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import sys
import time

HERE=Path(__file__).resolve().parent
OLD=Path('E:/Code/Report_INT3508/ridebound-scratchpad/tang3/stage1b/harness')
OUT=Path('C:/RideBoundData/research/tier3-cost-cap-sensitivity-v1')
PRIOR=Path('C:/RideBoundContinuation/2026-10-15/results')
ROLES=('CF-C30','CF-C60','CF-C120','CF-U')
_EX=None

def sha(path):
    h=hashlib.sha256()
    with open(path,'rb') as f:
        for chunk in iter(lambda:f.read(1048576),b''):h.update(chunk)
    return h.hexdigest()

def reader():
    global _EX
    if _EX is None:
        path=OLD/'extract_stage1b.py';raw=path.read_bytes()
        if hashlib.sha256(raw).hexdigest()!='6ff7f303f51a36d30afd22ea2c40c66290e8b9c1ee5a361461e2dca57083429c':
            raise RuntimeError('original reader hash mismatch')
        spec=importlib.util.spec_from_loader('cap_study_sealed_reader',loader=None)
        mod=importlib.util.module_from_spec(spec);mod.__file__=str(path);sys.modules[spec.name]=mod
        exec(compile(raw,str(path),'exec'),mod.__dict__);_EX=mod
    return _EX

def manifest(want):
    path=HERE/'study-manifest.json'
    if not want or sha(path)!=want:raise RuntimeError('study manifest mismatch')
    doc=json.loads(path.read_text())
    for p,h in doc['files'].items():
        if sha(p)!=h:raise RuntimeError('frozen file mismatch '+p)
    return doc

def binding(config,wp4):
    # These fixed configurations contain ASCII keys, integer numbers, no nulls.
    def canonical(raw):return json.dumps(json.loads(raw),sort_keys=True,separators=(',',':'),ensure_ascii=False).encode()
    return hashlib.sha256(b'RideBound.Wp4RunnerConfigurationBinding.v1\0'+
                          hashlib.sha256(canonical(config)).digest()+hashlib.sha256(canonical(wp4)).digest()).hexdigest()

def spec(phase,arm,cell):
    ex=reader();name=f'CF-{arm}-{cell}'
    path=OLD.parent.parent/'configs'/f'{arm}.json'
    return {'folder':str(OUT/phase/name),'job':name,'label':name+'-caps-v1',
            'policy':binding(path.read_bytes(),(OLD/'wp4-tier3-v2-costfirst.json').read_bytes()),
            'dll':ex.runner_v3_dll(),'pin':None}

def read_task(task):
    phase,arm,cell=task;ex=reader();s=spec(phase,arm,cell)
    problems=ex.folder_problems(s)
    if problems:raise RuntimeError(repr(problems))
    rows,rec=ex.extract_run(('CF-'+arm.replace('-',''),cell,s))
    limit=int(arm.split('-')[1])*1000
    if rec['ledgerBudgetMismatches'] or any(row[8]>limit for row in rows):
        raise RuntimeError('ledger mismatch or cap overshoot '+s['job'])
    if rec['completedWithoutAlight'] or rec['alightedNotCompleted'] or rec['alightedWithoutPromise']:
        raise RuntimeError('lifecycle anomaly '+s['job'])
    started=json.loads((HERE/'logs'/phase/(s['job']+'.started.json')).read_text())
    finished=json.loads((HERE/'logs'/phase/(s['job']+'.finished.json')).read_text())
    if started['manifestSha256']!=sha(HERE/'study-manifest.json') or started['manifestSha256']!=finished['manifestSha256'] or finished['returncode']!=0:
        raise RuntimeError('job receipt mismatch '+s['job'])
    if finished['bundleManifestSha256']!=sha(Path(s['folder'])/'bundle-manifest.json'):
        raise RuntimeError('job bundle binding mismatch '+s['job'])
    return rows,rec

def quantile_nearest(values,q):
    import math
    return sorted(values)[max(0,math.ceil(q*len(values))-1)] if values else 0

def summarize(rows,records):
    served=[r for r in rows if int(r['served'])]
    def late(r,field,threshold):return float(r['A'])-float(r[field])>threshold
    return {'requests':sum(r['requests'] for r in records),'served':len(served),
            'late60':sum(late(r,'p0',60000) for r in served),
            **{f'timely{seconds}':sum(not late(r,'p0',seconds*1000) for r in served) for seconds in (60,120,300)},
            'forecastLate60':sum(late(r,'D0',60000) for r in served),
            'shared':sum(int(r['shared']) for r in served),
            'revisedServed':sum(int(r['nonzeroC'])>0 for r in served),
            'repeatedServed':sum(int(r['nonzeroC'])>=2 for r in served),
            'latePickups':sum(r['latePickups'] for r in records),
            'p95C_ms':quantile_nearest([int(r['sumAbsC']) for r in served],.95),
            'maxC_ms':max([int(r['sumAbsC']) for r in rows],default=0),
            'ledgerMismatches':sum(len(r['ledgerBudgetMismatches']) for r in records)}

def gate(phase,want,require_complete):
    doc=manifest(want);ex=reader();ex.require_main_state()
    stops=list(HERE.glob('*-FAILED.txt'))+[p for p in (HERE/'STOP',HERE/'INPUT-DRIFT-STOP.txt',OLD/'STOP') if p.exists()]
    if stops:raise RuntimeError('stop/failure '+repr(stops))
    if require_complete:
        done=json.loads((HERE/(phase+'-complete.json')).read_text())
        if done['manifestSha256']!=want or len(done['bundles'])!=68:raise RuntimeError('main completion missing/mismatched')
        actual={p.name for p in (OUT/'main').iterdir()}
        if actual!=set(done['bundles']):raise RuntimeError('unexpected/missing main folder')
        for name,h in done['bundles'].items():
            if sha(OUT/'main'/name/'bundle-manifest.json')!=h:raise RuntimeError('changed bundle manifest '+name)
    return doc

def smoke(want):
    gate('smoke',want,False)
    for arm in ('C-60','C-120'):
        rows,rec=read_task(('smoke',arm,'d20181114-s10-r1-w08'))
        print('smoke identity/ledger/cap PASS',arm,len(rows),flush=True)
    manifest(want)

def analyze(want):
    doc=gate('main',want,True);ex=reader();cells=doc['cells']
    tasks=[('main',arm,cell) for cell in cells for arm in ('C-60','C-120')]
    results=[]
    with ProcessPoolExecutor(max_workers=2) as pool:
        for i,result in enumerate(pool.map(read_task,tasks),1):
            results.append(result);print(f'extracted {i}/68',flush=True)
    old=json.loads((PRIOR/'stage1b-runs.json').read_text())
    records=[r for r in old['runs'] if r['role'] in ('CF-C30','CF-U')]
    rows=[r for r in csv.DictReader(io.StringIO((PRIOR/'stage1b-riders.tsv').read_text()),delimiter='\t') if r['role'] in ('CF-C30','CF-U')]
    header=['role','cell','requestId','lifecycle','t0','p0','D0','A','served','sumAbsC','nonzeroC','netC','maxAbsC','shared']
    for rr,rec in results:
        role=rec['source'];rec['role']=role;records.append(rec)
        rows += [dict(zip(header,[role]+row)) for row in rr]
    index={(r['role'],r['cell']):r for r in records}
    if len(index)!=136:raise RuntimeError('expected 136 distinct cost-first role/cell records')
    for cell in cells:
        baseline=index[('CF-C30',cell)]
        for role in ROLES:
            rec=index[(role,cell)]
            for k in ('requests','requestIdsSha256','sourceScenarioContentSha256'):
                if rec[k]!=baseline[k]:raise RuntimeError('paired episode mismatch '+k)
            for k in ex.CELL_EQUAL_KEYS+('binarySha256',):
                if rec['manifest'][k]!=baseline['manifest'][k]:raise RuntimeError('paired manifest mismatch '+k)
    totals={role:summarize([r for r in rows if r['role']==role],[r for r in records if r['role']==role]) for role in ROLES}
    for role,expected in [('CF-C30',(2364,72,432)),('CF-U',(2502,379,1027))]:
        if tuple(totals[role][k] for k in ('served','late60','shared'))!=expected:raise RuntimeError('baseline arithmetic differs')
    cells_table=[dict(role=role,cell=cell,**summarize([r for r in rows if r['role']==role and r['cell']==cell],[index[(role,cell)]])) for cell in cells for role in ROLES]
    days_table=[dict(role=role,day=day,**summarize([r for r in rows if r['role']==role and r['cell'][:9]==day],[r for r in records if r['role']==role and r['cell'][:9]==day])) for day in sorted({c[:9] for c in cells}) for role in ROLES]
    output={'schema':'cost-cap-sensitivity-results-v1','manifestSha256':want,'totals':totals,'perCell':cells_table,'perDay':days_table,'runs':records}
    lines=['# Kết quả độ nhạy trần biến động kế hoạch — ưu tiên chi phí','',
           'Thăm dò trên 34 ô đã từng được xem; cùng runner v3. Không có kiểm định mức hài lòng, tối ưu toàn cục, lợi nhuận hay so sánh cộng dồn với từng lần.','',
           '| Trần | Phục vụ | Muộn >60s (p0) | Đúng trong 60s | Ghép chung | Bị sửa giờ* | Sửa lặp* | Đón muộn | Muộn >60s (D0) |',
           '|---|---:|---:|---:|---:|---:|---:|---:|---:|']
    for role in ROLES:
        t=totals[role];lines.append('| '+role+' | '+' | '.join(str(t[k]) for k in ('served','late60','timely60','shared','revisedServed','repeatedServed','latePickups','forecastLate60'))+' |')
    lines+=['','*Trong số khách hoàn thành. p0 là mốc kế hoạch ban đầu; D0 là mốc phát lại có dự báo. C là tổng biến động do điều phối, không phải thời gian khách chờ.',
            '', 'C30/U tái sử dụng kết quả đã khóa; C60/C120 là 68 lượt chính mới. Toàn bộ ô/ngày nằm trong results.json. Không diễn giải bộ thử này là kiểm định trên ngày chưa thấy.',
            '', 'H6/E1 cũ và H7=7<10 được giữ nguyên; nghiên cứu này không mở nhánh per-step hoặc sửa tích hợp dự báo.','']
    # Prepare all data before final refusal checks; never publish a partial-panel report.
    buf=io.StringIO();writer=csv.DictWriter(buf,fieldnames=header,delimiter='\t',lineterminator='\n');writer.writeheader();writer.writerows(rows)
    payloads={'REPORT.md':'\n'.join(lines).encode(),'results.json':(json.dumps(output,ensure_ascii=False,indent=1)+'\n').encode(),'riders.tsv':buf.getvalue().encode()}
    gate('main',want,True)
    if ex.pin_problems():raise RuntimeError('reader inputs drifted')
    root=HERE/'results';root.mkdir(exist_ok=True)
    for name,raw in payloads.items():
        with open(root/name,'xb') as f:f.write(raw)
    with open(root/'results.sha256','x',encoding='utf-8') as f:
        f.write(''.join(f'{hashlib.sha256(raw).hexdigest()}  {name}\n' for name,raw in payloads.items()))
    print('analysis complete '+str(root),flush=True)

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('mode',choices=('smoke','analyze'));p.add_argument('--manifest-sha',required=True)
    a=p.parse_args();smoke(a.manifest_sha) if a.mode=='smoke' else analyze(a.manifest_sha)
