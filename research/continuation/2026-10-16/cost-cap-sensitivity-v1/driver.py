"""Separate cost-first C60/C120 study. Never mutates the original sealed driver."""
from __future__ import annotations
import argparse
import concurrent.futures
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import threading
import time

HERE = Path(__file__).resolve().parent
OLD = Path('E:/Code/Report_INT3508/ridebound-scratchpad/tang3/stage1b/harness')
OUT = Path('C:/RideBoundData/research/tier3-cost-cap-sensitivity-v1')
MANIFEST = HERE / 'study-manifest.json'
ARMS = ('C-60', 'C-120')
FLOOR = 30720
NEED = 410
_BASE = None
_LOCK_HANDLE = None

def digest(path):
    h = hashlib.sha256()
    with open(path, 'rb') as stream:
        for chunk in iter(lambda: stream.read(1024*1024), b''): h.update(chunk)
    return h.hexdigest()

def exclusive(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    with open(path, 'x', encoding='utf-8', newline='\n') as f:
        f.write(json.dumps(value, ensure_ascii=False, indent=2) + '\n')

def base():
    global _BASE
    if _BASE is None:
        path = OLD / 'run_stage1b-v7.py'
        raw = path.read_bytes()
        if hashlib.sha256(raw).hexdigest() != '7495dd515154e81932ce61ea537f648739d940099b37425d0eb6b58f34e64811':
            raise RuntimeError('original v7 source hash changed')
        spec = importlib.util.spec_from_loader('cost_cap_original_v7', loader=None)
        mod = importlib.util.module_from_spec(spec)
        mod.__file__ = str(path)
        sys.modules[spec.name] = mod
        exec(compile(raw, str(path), 'exec'), mod.__dict__)
        _BASE = mod
    return _BASE

def config_problem(arm, raw, original):
    config = json.loads(raw)
    reference = json.loads(original)
    limits = config['policies'][0]['limits']
    entry = next(v for v in limits if v['dimension'] == 'drop_eta_total_ms')
    want = int(arm.split('-')[1])*1000
    if type(entry.get('hardLimit')) is not int or entry['hardLimit'] != want:
        return 'wrong hardLimit'
    entry['hardLimit'] = 30000
    return None if config == reference else 'configuration differs outside drop cap'

def items(phase):
    b = base()
    cells = [b.SMOKE_CELL] if phase == 'smoke' else b.TEST_CELLS
    return [(f'CF-{arm}-{cell}', arm, cell) for cell in cells for arm in ARMS]

def command(phase, name, arm, cell):
    b = base()
    cmd = b.command('main', name, arm, cell, 'on', f'{name}-caps-v1')
    cmd[cmd.index('--output')+1] = str(OUT/phase/name)
    return cmd

def latch(reason, details):
    path = HERE / (reason + '.txt')
    if not path.exists(): exclusive(path, {'time':time.time(),'details':details})

def latches():
    b = base()
    return ([str(p) for p in HERE.glob('*-FAILED.txt')] +
            [str(p) for p in (HERE/'STOP', HERE/'INPUT-DRIFT-STOP.txt') if p.exists()] +
            b.latch_problems('main') +
            [str(p) for p in (OLD/'STOP',Path('C:/RideBoundContinuation/2026-10-15/resource30-v3/RESOURCE-POLICY-FAILED.txt')) if p.exists()])

def load_manifest(want):
    if not want or digest(MANIFEST) != want: raise RuntimeError('manifest identity mismatch')
    doc=json.loads(MANIFEST.read_text(encoding='utf-8'))
    doc['_heldManifestSha256']=want
    return doc

def own_problems(doc):
    problems=[p for p, sha in doc['files'].items() if not Path(p).is_file() or digest(p) != sha]
    if doc.get('_heldManifestSha256') and digest(MANIFEST)!=doc['_heldManifestSha256']:
        problems.append('held study manifest changed')
    return problems

def full_check(doc):
    print('full original/new frozen-input check begins', flush=True)
    problems = own_problems(doc)
    problems += base().frozen_input_problems('main')
    if problems: raise RuntimeError('input verification: '+repr(problems[:20]))
    print('full original/new frozen-input check PASS', flush=True)

def freeze():
    b = base()
    if OUT.exists() or MANIFEST.exists(): raise RuntimeError('new study already initialized')
    for arm in ARMS:
        p = b.run_pilot.CONFIGS/f'{arm}.json'
        error = config_problem(arm,p.read_bytes(),(b.run_pilot.CONFIGS/'C-30.json').read_bytes())
        if error: raise RuntimeError(arm+': '+error)
    paths = [HERE/name for name in ('PLAN.md','driver.py','analysis.py','test_driver.py','launch.ps1')]
    paths += [b.run_pilot.CONFIGS/f'{a}.json' for a in ARMS]
    paths += [b.SEAL,b.FREEZE,b.ANALYSIS_FREEZE,b.ANALYSIS_BINDING,b.RUNNER_MANIFEST]
    paths += list(b.analysis_paths())
    prior = Path('C:/RideBoundContinuation/2026-10-15')
    paths += list((prior/'results').glob('*'))
    paths += [prior/'evidence/analysis-completion.json',prior/'evidence/main-final-admission.json']
    paths += list((b.OUT_ROOT/'main').glob('*/bundle-manifest.json'))
    doc = {'schema':'cost-cap-sensitivity-v1','createdUtc':time.strftime('%Y-%m-%dT%H:%M:%SZ',time.gmtime()),
           'cells':list(b.TEST_CELLS),'arms':list(ARMS),'mainJobs':68,'smokeJobs':2,
           'files':{str(p):digest(p) for p in sorted(set(paths))}}
    full_check(doc)
    exclusive(MANIFEST,doc)
    print('MANIFEST_SHA256='+digest(MANIFEST),flush=True)
    return 0

def state(phase,name):
    path=OUT/phase/name
    if not path.exists():
        return 'admitted-no-output' if (HERE/'logs'/phase/(name+'.started.json')).exists() else 'pending'
    if not (path/'summary.json').exists(): return 'partial'
    try:
        s=json.loads((path/'summary.json').read_text()); r=json.loads((path/'run-00.json').read_text())
        return 'pass' if s.get('status')=='pass' and r.get('status')=='succeeded' else 'failed'
    except (OSError,ValueError):return 'failed'

def verify_bundle(phase,name):
    folder=OUT/phase/name
    expected={'summary.json','run-00.json','transcript-00.ndjson','bundle-manifest.json'}
    if set(p.name for p in folder.iterdir()) != expected: raise RuntimeError('unexpected/incomplete bundle '+name)
    doc=json.loads((folder/'bundle-manifest.json').read_text())
    entries=doc['files']
    if len(entries)!=3 or {e['path'] for e in entries} != expected-{'bundle-manifest.json'}:
        raise RuntimeError('invalid bundle inventory '+name)
    for entry in entries:
        p=folder/entry['path']
        if p.stat().st_size!=entry['lengthBytes'] or digest(p)!=entry['sha256']:
            raise RuntimeError('bundle integrity mismatch '+str(p))
    if state(phase,name)!='pass': raise RuntimeError('bundle is not pass '+name)
    receipt=json.loads((HERE/'logs'/phase/(name+'.finished.json')).read_text())
    if receipt['returncode']!=0 or receipt['bundleManifestSha256']!=digest(folder/'bundle-manifest.json'):
        raise RuntimeError('child completion binding mismatch '+name)
    return digest(folder/'bundle-manifest.json')

def admission_reason(c_mib,e_mib,reserved,ac,stop):
    if stop:return 'stop'
    if not ac:return 'battery'
    if e_mib<40960:return 'disk-E'
    if c_mib-FLOOR-reserved<NEED:return 'disk-C'
    return None

def run_phase(phase,doc,manifest_sha):
    b=base(); snapshot=b.stat_snapshot(b.frozen_paths('main')+[Path(p) for p in doc['files']]+[MANIFEST])
    lock=threading.Lock(); reserved=0
    (HERE/'logs'/phase).mkdir(parents=True,exist_ok=True)
    def job(item):
        nonlocal reserved
        name,arm,cell=item
        with lock:
            current=state(phase,name)
            if current=='pass':verify_bundle(phase,name);return name,'pass'
            if current!='pending':latch('RUN-FAILED',[name,current]);return name,current
            if (HERE/'logs'/phase/(name+'.log')).exists():
                latch('RUN-FAILED',[name,'log exists without completed bundle']);return name,'refused-existing-log'
            drift=b.stat_drift(snapshot)
            if drift:latch('INPUT-DRIFT-STOP',drift);return name,'stopped-input-drift'
            reason=admission_reason(shutil.disk_usage('C:/').free//1048576,
                                    shutil.disk_usage('E:/').free//1048576,reserved,b.ac_only(),bool(latches()))
            if reason:return name,'deferred-'+reason
            if latches():return name,'deferred-stop'
            reserved+=NEED
            cmd=command(phase,name,arm,cell)
            exclusive(HERE/'logs'/phase/(name+'.started.json'),
                      {'time':time.time(),'manifestSha256':manifest_sha,'command':cmd,'cwd':str(b.run_pilot.REPO)})
        try:
            env=dict(os.environ,PYTHONDONTWRITEBYTECODE='1',PYTHONIOENCODING='utf-8',
                     RB_WORLD_CONFIG=str(b.run_t38.world_file(cell)))
            env.pop('RB_FORECAST_CONFIG',None)
            with open(HERE/'logs'/phase/(name+'.log'),'x',encoding='utf-8') as log:
                completed=subprocess.run(cmd,cwd=str(b.run_pilot.REPO),env=env,stdout=log,stderr=subprocess.STDOUT,check=False)
            receipt={'time':time.time(),'manifestSha256':manifest_sha,'returncode':completed.returncode,
                     'bundleManifestSha256':digest(OUT/phase/name/'bundle-manifest.json') if (OUT/phase/name/'bundle-manifest.json').exists() else None}
            exclusive(HERE/'logs'/phase/(name+'.finished.json'),receipt)
            if completed.returncode!=0:raise RuntimeError(f'native returncode {completed.returncode}')
            verify_bundle(phase,name)
            return name,'pass'
        except Exception as e:
            latch('RUN-FAILED',[name,repr(e)]);raise
        finally:
            with lock:reserved-=NEED
    b.run_pilot.keep_awake(True)
    error=None
    try:
        with concurrent.futures.ThreadPoolExecutor(max_workers=6) as pool:
            futures=[pool.submit(job,item) for item in items(phase)]
            for i,future in enumerate(concurrent.futures.as_completed(futures),1):
                name,status=future.result();print(f'{phase} {i}/{len(futures)} {name} {status}',flush=True)
    except BaseException as exc:
        error=exc
    finally:
        b.run_pilot.keep_awake(False)
        try:full_check(doc)
        except Exception as exc:latch('INPUT-DRIFT-STOP',[repr(exc)]);raise
    if error:raise error
    counts={s:sum(state(phase,n)==s for n,_,_ in items(phase)) for s in ('pass','pending','partial','failed','admitted-no-output')}
    print(json.dumps({'phase':phase,'counts':counts}),flush=True)
    return counts['pass']==len(items(phase))

def lock_instance():
    global _LOCK_HANDLE
    import msvcrt
    _LOCK_HANDLE=open(HERE/'instance.lock','a+b');_LOCK_HANDLE.seek(0)
    msvcrt.locking(_LOCK_HANDLE.fileno(),msvcrt.LK_NBLCK,1)

def main():
    p=argparse.ArgumentParser();p.add_argument('mode',choices=('freeze','selftest','run','status'));p.add_argument('--manifest-sha')
    args=p.parse_args()
    if args.mode=='selftest':
        import unittest
        suite=unittest.defaultTestLoader.discover(str(HERE),pattern='test_driver.py')
        return 0 if unittest.TextTestRunner(stream=sys.stdout,verbosity=2).run(suite).wasSuccessful() else 1
    if args.mode=='freeze':return freeze()
    if args.mode=='status':
        for phase in ('smoke','main'):
            values=[state(phase,n) for n,_,_ in items(phase)]
            print(phase,{s:values.count(s) for s in sorted(set(values))})
        print('latches',latches());return 0
    lock_instance();doc=load_manifest(args.manifest_sha)
    if latches():raise RuntimeError('stop/failure latched '+repr(latches()))
    full_check(doc)
    for phase in ('smoke','main'):
        done=HERE/(phase+'-complete.json')
        if done.exists():
            prior=json.loads(done.read_text())
            if prior['manifestSha256']!=args.manifest_sha:raise RuntimeError('phase binding changed')
            for name,_,_ in items(phase):verify_bundle(phase,name)
            continue
        if not run_phase(phase,doc,args.manifest_sha):return 3
        if phase=='smoke':
            checked=subprocess.run([str(base().run_pilot.PYTHON),'-B',str(HERE/'analysis.py'),
                                    'smoke','--manifest-sha',args.manifest_sha],check=False)
            if checked.returncode:
                latch('SMOKE-FAILED',['reader/identity/ledger/cap inspection exit',checked.returncode])
                return 4
        exclusive(done,{'time':time.time(),'manifestSha256':args.manifest_sha,
                        'bundles':{n:verify_bundle(phase,n) for n,_,_ in items(phase)}})
    if not (HERE/'results'/'REPORT.md').exists():
        exclusive(HERE/'analysis-started.json',{'time':time.time(),'manifestSha256':args.manifest_sha})
        try:
            checked=subprocess.run([str(base().run_pilot.PYTHON),'-B',str(HERE/'analysis.py'),
                                    'analyze','--manifest-sha',args.manifest_sha],check=False)
            if checked.returncode:raise RuntimeError('analysis exit '+str(checked.returncode))
        except Exception as e:latch('ANALYSIS-FAILED',[repr(e)]);raise
        full_check(doc)
        exclusive(HERE/'analysis-complete.json',{'time':time.time(),'manifestSha256':args.manifest_sha,
                   'files':{p.name:digest(p) for p in (HERE/'results').iterdir() if p.is_file()}})
    print('ALL MAIN + FINAL VERIFICATION + ANALYSIS COMPLETE',flush=True)
    return 0

if __name__=='__main__':
    try:raise SystemExit(main())
    except Exception as error:
        print(type(error).__name__+': '+str(error),flush=True)
        raise
