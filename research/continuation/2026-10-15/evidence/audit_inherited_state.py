"""Read-only inventory and sealed-file verification at session continuation."""
import base64
import collections
import datetime
import hashlib
import json
import pathlib
import shutil

HARNESS = pathlib.Path('E:/Code/Report_INT3508/ridebound-scratchpad/tang3/stage1b/harness')
ROOT = pathlib.Path('C:/RideBoundData/research/tier3-stage1b-v1')
OUT = pathlib.Path('C:/RideBoundContinuation/2026-10-15/evidence/inherited-state.json')

def sha(path):
    h = hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            h.update(chunk)
    return h.hexdigest()

seal = HARNESS / 'STAGE1B-PLAN.sha256'
sealed = []
for line in seal.read_text(encoding='utf-8').splitlines():
    digest, name = line.split(maxsplit=1)
    name = name.lstrip('*')
    path = HARNESS / name
    actual = sha(path) if path.is_file() else None
    sealed.append({'name': name, 'expected': digest, 'actual': actual, 'matches': actual == digest})

phases = {}
for phase in ('gate0', 'smoke', 'main'):
    folder = ROOT / phase
    runs = []
    if folder.exists():
        for job in sorted(folder.iterdir()):
            summary_path = job / 'summary.json'
            if not summary_path.is_file():
                runs.append({'job': job.name, 'status': 'missing summary'})
                continue
            summary = json.loads(summary_path.read_text(encoding='utf-8'))
            run = json.loads((job / 'run-00.json').read_text(encoding='utf-8'))
            manifest = json.loads((job / 'bundle-manifest.json').read_text(encoding='utf-8'))
            file_checks = []
            for item in manifest['files']:
                path = job / item['path']
                file_checks.append({'path': item['path'], 'matches': path.is_file() and
                    path.stat().st_size == item['lengthBytes'] and sha(path) == item['sha256']})
            paths, certificates, shapes = collections.Counter(), collections.Counter(), collections.Counter()
            decisions = 0
            for line in (job / 'transcript-00.ndjson').open(encoding='utf-8'):
                rec = json.loads(line)
                raw = base64.b64decode(rec['frameBase64'])
                if hashlib.sha256(raw).hexdigest() != rec['frameSha256']:
                    raise ValueError('Frame hash mismatch: ' + job.name)
                envelope = json.loads(raw)
                if envelope.get('messageType') != 'decision':
                    continue
                decisions += 1
                payload = envelope.get('payload', {})
                solver = payload.get('solverDiagnostics', payload.get('solver', {}))
                paths[str(solver.get('executionPath'))] += 1
                certificate = payload.get('certificate', {})
                certificates[str(certificate.get('kind', certificate.get('status')))] += 1
                levels = solver.get('objectiveLevels', solver.get('levels', []))
                shapes[json.dumps(levels, sort_keys=True)] += 1
            runs.append({'job': job.name, 'summaryStatus': summary.get('status'), 'runStatus': run.get('status'),
                'label': summary.get('label'), 'bundleFileChecks': file_checks, 'decisions': decisions,
                'executionPaths': dict(paths), 'certificateMarkers': dict(certificates),
                'solverShapes': dict(shapes), 'runKeys': sorted(run)})
    phases[phase] = {'exists': folder.exists(), 'runs': runs}

receipts = {}
for name in ('GATE0-BEHAVIOURAL-PASSED.txt', 'SMOKE-PASSED.txt', 'SMOKE-INSPECTION.txt',
             'stage1b-analysis-freeze.sha256', 'stage1b-analysis-binding.txt'):
    path = HARNESS / name
    receipts[name] = {'exists': path.exists(), 'sha256': sha(path) if path.is_file() else None}
result = {'observedAt': datetime.datetime.now(datetime.timezone(datetime.timedelta(hours=7))).isoformat(),
    'sealSha256': sha(seal), 'sealedFiles': sealed, 'phases': phases, 'receipts': receipts,
    'stopReceipts': sorted(p.name for p in HARNESS.iterdir() if p.name.endswith('-FAILED.txt') or p.name == 'INPUT-DRIFT-STOP.txt'),
    'freeGiB': {drive: shutil.disk_usage(drive + ':/').free / 1024**3 for drive in ('C', 'E')}}
with OUT.open('x', encoding='utf-8', newline='\n') as stream:
    json.dump(result, stream, ensure_ascii=False, indent=2)
print(json.dumps({'out': str(OUT), 'sealedFiles': len(sealed), 'sealMismatches': [r['name'] for r in sealed if not r['matches']],
    'phaseCounts': {phase: len(data['runs']) for phase, data in phases.items()}, 'stops': result['stopReceipts'],
    'analysisFrozen': receipts['stage1b-analysis-freeze.sha256']['exists'], 'freeGiB': result['freeGiB']}, indent=2))
