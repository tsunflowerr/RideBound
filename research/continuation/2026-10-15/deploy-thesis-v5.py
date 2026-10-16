"""Back up actual thesis, then copy only verified final thesis files.

No deletion, moving, Git mutation, simulation, or study-input mutation.
"""
import datetime
import hashlib
import json
import re
import shutil
from pathlib import Path

BASE = Path('C:/RideBoundContinuation/2026-10-15')
SOURCE = BASE / 'thesis-v5'
TARGET = Path('E:/Code/Report_INT3508/ridebound')
assert TARGET.resolve() == Path('E:/Code/Report_INT3508/ridebound').resolve()
assert SOURCE.resolve().is_relative_to(BASE.resolve())


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def inventory(root):
    return {p.relative_to(root).as_posix(): dict(bytes=p.stat().st_size, sha256=digest(p))
            for p in sorted(root.rglob('*')) if p.is_file()}


qa = json.loads((SOURCE / 'qa-v5-05/automated-qa.json').read_text(encoding='utf-8'))
nums = json.loads((SOURCE / 'thesis-numeric-check.json').read_text(encoding='utf-8'))
assert qa['pdfSha256'] == digest(SOURCE / 'main.pdf')
assert not qa['logIssues'] and not qa['outsidePageChars'] and not qa['internalStageLabels']
assert qa['pages'] == qa['renderCount'] and nums['allPassed'] and nums['checkCount'] == 47
assert (SOURCE / 'chung-cu/VISUAL-REVIEW.md').is_file(), 'Human review must be completed first'
for drive, floor in [('C:/', 30), ('E:/', 40)]:
    assert shutil.disk_usage(drive).free >= floor*1024**3

inputs = re.findall(r'\\input\{([^}]+)\}', (SOURCE / 'main.tex').read_text(encoding='utf-8'))
payload = ['main.tex', 'main.pdf', 'main.log', 'README.md', 'hinh/logo-uet.png',
           'chung-cu/README.md', 'chung-cu/VISUAL-REVIEW.md']
payload += [p+'.tex' for p in inputs]
assert len(inputs) == 13, inputs
for rel in payload:
    assert (SOURCE / rel).is_file(), rel
    assert (TARGET / rel).resolve().is_relative_to(TARGET.resolve())
for file, newrel in [('thesis-numeric-check.json', 'chung-cu/thesis-numeric-check.json'),
                     ('qa-v5-05/automated-qa.json', 'chung-cu/automated-qa.json')]:
    shutil.copy2(SOURCE / file, SOURCE / newrel)
    payload.append(newrel)

protected = [TARGET.parent / name for name in ['main.tex', 'main.pdf', 'README.md']]
for folder in ['noi-dung', 'hinh']:
    if (TARGET.parent / folder).is_dir():
        protected += [p for p in (TARGET.parent / folder).rglob('*') if p.is_file()]
protected_before = {str(p): digest(p) for p in protected if p.is_file()}
before = inventory(TARGET)
now = datetime.datetime.now(datetime.timezone(datetime.timedelta(hours=7)))
backup = BASE / ('actual-thesis-backup-'+now.strftime('%Y%m%d-%H%M%S'))
assert not backup.exists()
shutil.copytree(TARGET, backup)
assert inventory(backup) == before
assert inventory(TARGET) == before, 'Actual thesis changed during backup'
(backup / 'BACKUP-MANIFEST.json').write_text(json.dumps(dict(original=str(TARGET), created=now.isoformat(), files=before), ensure_ascii=False, indent=2)+'\n', encoding='utf-8')

expected = {rel: dict(bytes=(SOURCE / rel).stat().st_size, sha256=digest(SOURCE / rel)) for rel in payload}
for rel in payload:
    (TARGET / rel).parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(SOURCE / rel, TARGET / rel)
after = inventory(TARGET)
assert all(after[rel] == info for rel, info in expected.items())
untouched = {rel: info for rel, info in before.items() if rel not in payload}
assert all(after[rel] == info for rel, info in untouched.items())
assert protected_before == {str(p): digest(p) for p in protected if p.is_file()}
receipt = dict(created=now.isoformat(), target=str(TARGET), staging=str(SOURCE), backup=str(backup),
               pages=qa['pages'], pdfSha256=qa['pdfSha256'], numericChecks=nums['checkCount'],
               allNumericChecksPassed=nums['allPassed'], verifiedPayload=expected,
               preservedUnrelatedFiles=list(untouched), protectedInternshipFiles=protected_before,
               scope='Thesis source/docs/layout only. No code, experiments, frozen inputs, Git history or timers changed.')
out = BASE / 'thesis-v5-deployment.json'
with out.open('x', encoding='utf-8') as stream:
    json.dump(receipt, stream, ensure_ascii=False, indent=2)
    stream.write('\n')
shutil.copy2(out, TARGET / 'chung-cu/DEPLOYMENT.json')
assert digest(TARGET / 'chung-cu/DEPLOYMENT.json') == digest(out)
print(json.dumps({k:v for k,v in receipt.items() if k not in ['verifiedPayload','protectedInternshipFiles']}, ensure_ascii=False, indent=2))
