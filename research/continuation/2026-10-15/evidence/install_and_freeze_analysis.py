"""Install reviewed UNSEALED analysis, preserve originals on C, create first analysis seal.
Never modify the seventeen previously sealed files or any core, runner, environment, or result.
"""
import datetime
import hashlib
import json
import os
import pathlib
import re

CONT = pathlib.Path('C:/RideBoundContinuation/2026-10-15')
HARNESS = pathlib.Path('E:/Code/Report_INT3508/ridebound-scratchpad/tang3/stage1b/harness')
MAIN = pathlib.Path('C:/RideBoundData/research/tier3-stage1b-v1/main')
BACKUP = CONT / 'evidence' / 'original-analysis-before-install'
FREEZE = HARNESS / 'stage1b-analysis-freeze.sha256'

def digest(raw):
    return hashlib.sha256(raw).hexdigest()

seal_bytes = (HARNESS / 'STAGE1B-PLAN.sha256').read_bytes()
assert digest(seal_bytes) == '92ec2df7629bd2310f814034856a4eb4ae2f20f414db3a45a054ff1099b699e5'
sealed = {}
for line in seal_bytes.decode().splitlines():
    value, name = line.split(maxsplit=1)
    sealed[name.lstrip('*')] = value
assert len(sealed) == 17

def verify_sealed():
    assert (HARNESS / 'STAGE1B-PLAN.sha256').read_bytes() == seal_bytes
    for name, expected in sealed.items():
        assert digest((HARNESS / name).read_bytes()) == expected, 'sealed drift: ' + name

verify_sealed()
assert not FREEZE.exists() and not (HARNESS / 'stage1b-analysis-binding.txt').exists()
assert not MAIN.exists() or not any(MAIN.iterdir()), 'main already has outputs'
assert not any(p.name.endswith('-FAILED.txt') or p.name in ('INPUT-DRIFT-STOP.txt', 'STOP') for p in HARNESS.iterdir())
judge = (CONT / 'reviews' / 'R46-FINAL-INVENTORY-JUDGE.md').read_text(encoding='utf-8')
assert '**Verdict: READY TO FREEZE**' in judge
audit = json.loads((CONT / 'thesis-review' / 'R46-FINAL-INVENTORY-AUDIT.json').read_text(encoding='utf-8'))
inventory = audit['static_inventory']
assert len(inventory) == 10
original_pins = {
    'extract_stage1b.py': '87bf04bab94317eeaa57f6f8ce740071b5f879dffcfd7abdde03c02db5b085f9',
    'report_stage1b.py': '908b5cde5ba0a644bb10784cd10e6b0b11513e73f6ae9f235343407a97293993',
    'STAGE1B-ANALYSIS-AMENDMENT.md': 'baa56e20ae792c6f89a6cabb51ec82bb18b13dd6cda37e880c2a0cd4e887f4d8',
}
prepared, originals = {}, {}
for name, entry in inventory.items():
    assert re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9._-]*', name)
    raw = pathlib.Path(entry['path']).read_bytes()
    assert digest(raw) == entry['sha256'], 'reviewed source changed: ' + name
    prepared[name] = raw
    target = HARNESS / name
    if target.exists():
        previous = target.read_bytes()
        if previous != raw:
            assert name in original_pins and digest(previous) == original_pins[name], 'unrelated existing file: ' + name
            assert name not in sealed
            originals[name] = previous
    if name in sealed:
        assert digest(raw) == sealed[name], 'cannot replace any previously sealed file'
assert b'inventory-coverage: complete' in prepared['STAGE1B-ANALYSIS-AMENDMENT.md']
assert b'# 192 checks: 192 passed, 0 failed' in prepared['analysis-selftest-2026-10-15-v3.log']

BACKUP.mkdir()
for name, raw in originals.items():
    with (BACKUP / name).open('xb') as handle:
        handle.write(raw)
with (BACKUP / 'preserved.sha256').open('x', encoding='utf-8', newline='\n') as handle:
    handle.write(''.join(f'{digest(raw)}  {name}\n' for name, raw in sorted(originals.items())))
for name, raw in prepared.items():
    target = HARNESS / name
    if target.exists() and target.read_bytes() == raw:
        continue
    with target.open('wb' if name in originals else 'xb') as handle:
        handle.write(raw)
        handle.flush()
        os.fsync(handle.fileno())
verify_sealed()
for name, raw in prepared.items():
    assert (HARNESS / name).read_bytes() == raw
manifest = ''.join(f'{digest(raw)}  {name}\n' for name, raw in sorted(prepared.items())).encode('utf-8')
with FREEZE.open('xb') as handle:
    handle.write(manifest)
    handle.flush()
    os.fsync(handle.fileno())
receipt = {'installedAt': datetime.datetime.now(datetime.timezone(datetime.timedelta(hours=7))).isoformat(),
    'analysisManifestSha256': digest(manifest), 'analysisFiles': {name: digest(raw) for name, raw in prepared.items()},
    'preservedOriginals': {name: digest(raw) for name, raw in originals.items()},
    'previousSealSha256': digest(seal_bytes), 'previousSealedFilesUnchanged': 17,
    'judgeSha256': digest(judge.encode('utf-8')), 'noMainAtSeal': True}
with (CONT / 'evidence' / 'analysis-install-and-seal.json').open('x', encoding='utf-8', newline='\n') as handle:
    json.dump(receipt, handle, indent=2)
print(json.dumps(receipt, indent=2))
