"""Independent read-only confirmation of the recorded smoke selector order."""
import base64
import collections
import hashlib
import json
import pathlib

root = pathlib.Path('C:/RideBoundData/research/tier3-stage1b-v1/smoke')
results = []
for folder in sorted(root.iterdir()):
    paths, final, primary, shapes = (collections.Counter() for _ in range(4))
    decisions = nonnormal = 0
    t = folder / 'transcript-00.ndjson'
    for line in t.open(encoding='utf-8'):
        record = json.loads(line)
        frame = base64.b64decode(record['frameBase64'])
        assert hashlib.sha256(frame).hexdigest() == record['frameSha256']
        envelope = json.loads(frame)
        if envelope.get('messageType') != 'decision':
            continue
        payload = envelope['payload']
        selected = payload['solver']['executionEvidence']['selection']
        objective_names = [x['objectiveName'] for x in selected['finalSolverDiagnostics']['objectiveBounds']]
        assert objective_names[:2] == ['accepted-request-count', 'operational-cost']
        assert len(objective_names) == 10 and all(x.startswith('candidate-id-rank:') for x in objective_names[2:])
        assert all(x['gapNumerator'] == 0 and x['isProvenOptimal'] for x in selected['finalSolverDiagnostics']['objectiveBounds'])
        paths[selected['executionPath']] += 1
        final[selected['finalSolveStatus']] += 1
        primary[selected['primarySolveStatus']] += 1
        shapes['accepted-request-count > operational-cost > candidate-id-rank:*x8'] += 1
        nonnormal += not payload['certificate']['body']['normalOperation']
        decisions += 1
    results.append({'job': folder.name, 'decisions': decisions, 'executionPath': dict(paths), 'finalStatus': dict(final),
        'primaryStatus': dict(primary), 'objectiveShape': dict(shapes), 'nonNormalCertificates': nonnormal,
        'transcriptSha256': hashlib.sha256(t.read_bytes()).hexdigest()})
out = pathlib.Path('C:/RideBoundContinuation/2026-10-15/evidence/smoke-selector-confirmation.json')
with out.open('x', encoding='utf-8', newline='\n') as handle:
    json.dump(results, handle, indent=2)
print(json.dumps(results, indent=2))
