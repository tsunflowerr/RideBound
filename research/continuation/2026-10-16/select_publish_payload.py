"""Select lightweight publication; excluded copies and all originals remain intact."""
from pathlib import Path
import collections
import json
import re

ROOT=Path('C:/RideBoundContinuation/2026-10-16')
data=json.loads((ROOT/'github-handoff-inventory.json').read_text(encoding='utf-8'))
ride=Path('C:/Code/RideBound-handoff-20261016');report=Path('C:/Code/Report_INT3508-handoff-20261016')
report_groups={'internship owner working state','root handoff/report','report figures','current thesis','research source and reports'}
keep=[];omit=[];byroot={ride:[],report:[]}
for entry in data['copied']:
    root=report if entry['group'] in report_groups else ride
    rel=Path(entry['target']);s=rel.as_posix();why=None
    if entry['group']=='public derivative fixtures':why='large derivative fixture snapshot; provision externally'
    if entry['group']=='research source and reports':
        parts=set(x.lower() for x in rel.parts)
        if any(x.startswith('retained-states') for x in parts):why='raw retained states; publish aggregate reports'
        if parts & {'pdf','papers','corpus','docs-reading','skills','pdf-text','paper-text','pdfs'}:why='third-party paper/skill/source corpus; publish citations and audit only'
        if rel.suffix.lower() in {'.png','.jpg','.jpeg'}:why='historical render/screenshot; current thesis retained'
        if rel.suffix.lower()=='.json' and entry['bytes']>512*1024:why='large detailed extraction/state/inventory; publish aggregate reports'
        if rel.suffix.lower()=='.tsv' and entry['bytes']>2*1024*1024 and '.sha256.' not in rel.name:why='large detailed extraction; publish aggregate reports'
        if rel.suffix.lower()=='.txt' and re.search(r'paper|pdf|patent|full.?text|hoerl|horl|schulz|fielbaum',s,re.I):why='downloaded/extracted paper text; retain citations and local source'
        if rel.suffix.lower()=='.txt' and not re.search(r'report|result|summary|receipt|selftest|output|probe|devlog|passed|failed|inspection|binding|inventory|stop',rel.name,re.I):
            why='unclassified extracted/historical text; publish source, reports and citations'
    if why:
        omitted=dict(entry,reason=why);omit.append(omitted);byroot[root].append('/'+s)
    else:keep.append(dict(entry,repository='Report_INT3508' if root==report else 'RideBound'))

for root,patterns in byroot.items():
    path=root/'.gitignore';old=path.read_text(encoding='utf-8') if path.exists() else ''
    marker='# Handoff payload: raw/large/third-party copies remain local, summaries are published.'
    if marker in old:old=old[:old.index(marker)].rstrip()+'\n'
    # Literal paths: escape metacharacters used in gitignore patterns.
    def escape(s):return ''.join('\\'+c if c in '*?[]!#' else c for c in s)
    path.write_text(old+'\n'+marker+'\n'+'\n'.join(escape(s) for s in sorted(set(patterns)))+'\n',encoding='utf-8')
    (root/'HANDOFF-INVENTORY.json').write_text(json.dumps({'files':[x for x in keep if (x['repository']=='Report_INT3508')==(root==report)],
        'copyInventory':'Local C:/RideBoundContinuation/2026-10-16/github-handoff-inventory.json',
        'note':'Inventory binds copied source/evidence bytes; live status/checkpoints may be updated before commit. Git commit is the final published identity.',
        'secretScan':'No high-confidence credential/private-key patterns found in exported copies.'},ensure_ascii=False,indent=1)+'\n',encoding='utf-8')

excluded=data['excludedFiles']+omit
(ride/'EXTERNAL-ARTIFACTS.json').write_text(json.dumps({'neverDeleted':True,'rawBundleIndex':'research/continuation/2026-10-15/raw-bundle-index.json',
 'requiredExternalRoots':['C:/RideBoundData/research','E:/RideBoundData/wp6','E:/RideBoundData/wp7','E:/Code/RideBound-deadline'],
 'excludedFiles':excluded,'directoryPolicy':'Vendor/env/build/caches/local configurations are excluded as whole directories by export_handoff.py; raw transcripts outside the source tree are represented by raw-bundle-index.json.'},ensure_ascii=False,indent=1)+'\n',encoding='utf-8')
summary={'publishFiles':len(keep),'publishMiB':round(sum(x['bytes'] for x in keep)/1048576,2),'additionalCopiesRetainedLocal':len(omit)}
(ROOT/'publication-selection.json').write_text(json.dumps(summary,indent=1)+'\n',encoding='utf-8')
print(summary)
