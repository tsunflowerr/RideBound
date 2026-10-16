"""Export source/report handoffs; leave protected working directories untouched."""
from pathlib import Path
import hashlib
import json
import os
import re
import shutil
import subprocess

RIDE=Path('C:/Code/RideBound-handoff-20261016')
REPORT=Path('C:/Code/Report_INT3508-handoff-20261016')
SOURCE=Path('E:/Code/Report_INT3508')
CONT=Path('C:/RideBoundContinuation')
TEXT={'.md','.txt','.py','.ps1','.json','.yaml','.yml','.toml','.tex','.bib','.tsv','.csv','.sha256','.patch','.diff','.xml','.cs','.csproj','.props','.targets','.sln','.slnx','.svg','.gitignore','.editorconfig','.ini','.cfg','.lock','.template'}
SKIP_DIRS={'.git','.claude','.codex','.vscode','.vs','bin','obj','__pycache__','node_modules','vendor','tools','.venv','site-packages','preview'}
skipped=[];copied=[]

def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()

def copy(path,dest,group):
    dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(path,dest)
    copied.append({'group':group,'source':str(path),'target':str(dest.relative_to(RIDE if dest.is_relative_to(RIDE) else REPORT)),
                   'bytes':path.stat().st_size,'sha256':sha(path)})

def reason(path,rel,images=False,pdf=False):
    if set(rel.parts)&SKIP_DIRS:return 'generated/local configuration/vendor'
    if path.name.lower().startswith(('.env','secrets','credentials')) or path.suffix.lower() in {'.pem','.key','.pfx','.p12'}:return 'local secret/configuration'
    if path.stat().st_size>10*1024*1024:return 'large artifact; retain local'
    if path.suffix.lower()=='.pdf':return None if pdf else 'downloaded paper or historical/rendered PDF; retain local'
    if path.suffix.lower() in {'.png','.jpg','.jpeg'}:
        if images and not re.search(r'(?:page|contact|sheet|render)[-_]?\d',path.name,re.I) and path.stat().st_size<2*1024*1024:return None
        return 'render/screenshot or large image; retain local'
    if path.suffix.lower() not in TEXT and path.name not in {'.gitignore','.editorconfig','LICENSE'}:return 'binary/raw/generated artifact; retain local'
    return None

def tree(root,dest,group,images=False,pdf=False):
    for base,dirs,files in os.walk(root):
        dirs[:]=[d for d in dirs if d not in SKIP_DIRS]
        for name in files:
            path=Path(base)/name;rel=path.relative_to(root);why=reason(path,rel,images,pdf)
            if why:skipped.append({'source':str(path),'bytes':path.stat().st_size,'reason':why})
            else:copy(path,dest/rel,group)

# Uncommitted historical main documents are retained without altering that tree.
main=Path('E:/Code/RideBound')
names=subprocess.check_output(['git','-C',str(main),'diff','--name-only','-z']).decode().split('\0')
for rel in filter(None,names):
    if rel not in {'docs/18-status-and-decision-log.md','docs/19-requirement-traceability.md'}:copy(main/rel,RIDE/rel,'RideBound main working documents')
for rel in filter(None,subprocess.check_output(['git','-C',str(main),'ls-files','--others','--exclude-standard','-z','--','docs']).decode().split('\0')):
    path=main/rel
    if path.suffix=='.md':copy(path,RIDE/rel,'RideBound main untracked research reports')
now=Path('C:/Code/RideBound-noworse')
for rel in ('docs/18-status-and-decision-log.md','docs/19-requirement-traceability.md','docs/benchmarking/thesis-stage1b-continuation-2026-10-15.md'):
    copy(now/rel,RIDE/rel,'latest live status')
for directory in ('tier3-development-w07-v1','tier3-panel-a-extra-v1','tier3-snow-v1'):
    rel=Path('benchmarks/fixtures/wp6/public/fleetpy-manhattan-v1')/directory
    tree(now/rel,RIDE/rel,'public derivative fixtures')

old=CONT/'2026-10-15';target=RIDE/'research/continuation/2026-10-15'
for directory in ('results','evidence','analysis-r46','resource30','resource30-v2','resource30-v3','reviews'):
    tree(old/directory,target/directory,'continuation evidence')
for path in old.iterdir():
    if path.is_file() and path.suffix in TEXT:copy(path,target/path.name,'continuation source/status')
audit=CONT/'2026-10-16/thesis-readiness-audit'
for path in audit.iterdir():
    if path.name.endswith('-primary.txt') or path.name.startswith('uet-format-'):continue
    if path.is_file() and path.suffix in TEXT:copy(path,RIDE/'research/continuation/2026-10-16/thesis-readiness-audit'/path.name,'readiness audit')
tree(CONT/'2026-10-16/cost-cap-sensitivity-v1',RIDE/'research/continuation/2026-10-16/cost-cap-sensitivity-v1','new cap study')

# Preserve owner's existing report changes on a separate branch; no source edits.
for rel in filter(None,subprocess.check_output(['git','-C',str(SOURCE),'ls-files','-z']).decode().split('\0')):
    if (SOURCE/rel).is_file():copy(SOURCE/rel,REPORT/rel,'internship owner working state')
for path in SOURCE.iterdir():
    if path.is_file() and (path.suffix=='.md' or path.name.startswith('Baocaothuctap_')):copy(path,REPORT/path.name,'root handoff/report')
tree(SOURCE/'hinh',REPORT/'hinh','report figures',images=True)
tree(SOURCE/'ridebound',REPORT/'ridebound','current thesis',images=True,pdf=True)
tree(SOURCE/'ridebound-scratchpad',REPORT/'ridebound-scratchpad','research source and reports',images=True)

# Lightweight raw index carries proof hashes without copying transcripts.
raw=Path('C:/RideBoundData/research/tier3-stage1b-v1')
index=[]
for phase in ('gate0','smoke','main'):
    for p in sorted((raw/phase).glob('*/bundle-manifest.json')):
        index.append({'phase':phase,'job':p.parent.name,'manifestSha256':sha(p),'bundle':json.loads(p.read_text())})
out=RIDE/'research/continuation/2026-10-15/raw-bundle-index.json'
out.write_text(json.dumps({'root':str(raw),'bundles':index},indent=1)+'\n',encoding='utf-8')

# Scan candidate files without exposing matched credentials in logs.
patterns={'private-key':re.compile(rb'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----'),
          'github-token':re.compile(rb'\b(?:gh[pousr]_[A-Za-z0-9]{36,255}|github_pat_[A-Za-z0-9_]{60,255})\b'),
          'aws-access-key':re.compile(rb'\bAKIA[A-Z0-9]{16}\b'),
          'slack-token':re.compile(rb'\bxox[baprs]-[A-Za-z0-9-]{20,}\b')}
findings=[]
for root in (RIDE,REPORT):
    for p in root.rglob('*'):
        if not p.is_file() or '.git' in p.parts or p.stat().st_size>12*1024*1024:continue
        raw=p.read_bytes()
        for label,pattern in patterns.items():
            if pattern.search(raw):findings.append({'file':str(p),'kind':label})
receipt={'copied':copied,'excludedFiles':skipped,'secretScanFindings':findings,
         'note':'Excluded directories are enumerated by policy in export_handoff.py; no data was deleted.'}
path=CONT/'2026-10-16/github-handoff-inventory.json';path.write_text(json.dumps(receipt,ensure_ascii=False,indent=1)+'\n',encoding='utf-8')
summary={'copiedFiles':len(copied),'copiedMiB':round(sum(x['bytes'] for x in copied)/1048576,2),
         'excludedFiles':len(skipped),'secretFindings':findings,'rawMainBundles':sum(x['phase']=='main' for x in index)}
print(json.dumps(summary,ensure_ascii=False))
if findings:raise SystemExit('secret scan needs review; do not commit')
for root in (RIDE,REPORT):
    (root/'HANDOFF-INVENTORY.json').write_text(json.dumps({'files':[x for x in copied if (x['group'].startswith('internship') or x['group'] in {'root handoff/report','report figures','current thesis','research source and reports'})==(root==REPORT)],
        'excludedFileCount':len(skipped),'exclusionPolicy':'Generated binaries/caches, downloaded papers, raw/large artifacts and local configs stay local. No deletion.',
        'secretScan':'No high-confidence token/private-key patterns found.'},ensure_ascii=False,indent=1)+'\n',encoding='utf-8')
