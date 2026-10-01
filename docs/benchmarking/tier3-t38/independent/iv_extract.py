"""Independent verifier: extract per-run raw metrics from transcripts (own code)."""
import json, base64, os, sys, collections
from multiprocessing import Pool
ROOT = r'C:\RideBoundData\research\tier3-t38-v1'
OUT = r'E:\Code\Report_INT3508\ridebound-scratchpad\tang3\t38\independent\cache'
ARMS = ['U', 'C-30', 'Mplus-30', 'Mplus-60', 'V-30']
MAN = r'E:\Code\Report_INT3508\ridebound-scratchpad\tang3\t38\split-t38-v1.json'

def resolve(arm, cell):
    job = f'{arm}-{cell}'
    for cand in (job, job + '-rerun1'):
        d = os.path.join(ROOT, cand)
        if os.path.exists(os.path.join(d, 'summary.json')):
            return cand, d
    return None, None

def extract(job_dir_pair):
    arm, cell = job_dir_pair
    name, d = resolve(arm, cell)
    if name is None:
        return (arm, cell, None)
    outp = os.path.join(OUT, name + '.json')
    if os.path.exists(outp):
        return (arm, cell, name)
    summ = json.load(open(os.path.join(d, 'summary.json'), encoding='utf-8'))
    decisions = {}          # hash -> (index, normalOperation)
    acked = set()
    alight = {}             # requestId -> simTimeMs of batch
    last_ckpt = None
    idx = 0
    for line in open(os.path.join(d, 'transcript-00.ndjson'), encoding='utf-8'):
        r = json.loads(line)
        env = json.loads(base64.b64decode(r['frameBase64']))
        mt = env['messageType']
        dr = r['direction']
        if mt == 'decision' and dr == 'runnerToAdapter':
            pl = env['payload']
            cert = pl.get('certificate')
            body = cert.get('body') if isinstance(cert, dict) else None
            non_normal = bool(isinstance(body, dict) and body.get('normalOperation') is False)
            decisions.setdefault(pl['decisionHash'], (idx, non_normal))
        elif mt == 'decisionApplied' and dr == 'adapterToRunner':
            h = env['payload']['decisionHash']
            if h in decisions and decisions[h][0] < idx:
                acked.add(h)
        elif mt == 'eventBatch' and dr == 'adapterToRunner':
            for e in env['payload']['events']:
                if e['eventType'] == 'passengerAlighted':
                    alight[e['payload']['requestId']] = env.get('simTimeMs')
        elif mt == 'checkpoint' and dr == 'runnerToAdapter':
            last_ckpt = env
        idx += 1
    st = last_ckpt['payload']['content']['onlineState']
    riders = {}
    for h in st['commitmentLedger']:
        ent = h['entries']
        assert ent[0]['kind'] == 'initialPromise' and all(e['kind'] == 'revision' for e in ent[1:])
        p0 = ent[0]['publishedPromise']['projection']
        row = {'p0_drop': p0['dropEtaMs'], 'p0_pick': p0['pickupEtaMs'], 'rev': []}
        for e in ent[1:]:
            v = e['previousPromise']['projection']; ex = e['exogenousProjection']; p = e['publishedPromise']['projection']
            row['rev'].append({
                'v_d': v['dropEtaMs'], 'e_d': ex['dropEtaMs'], 'p_d': p['dropEtaMs'],
                'dl_x': e['deltas']['exogenous']['dropEtaTotalMs'],
                'dl_c': e['deltas']['decisionInduced']['dropEtaTotalMs'],
                'dl_z': e['deltas']['visible']['dropEtaTotalMs']})
        riders[h['requestId']] = row
    lc = collections.Counter(q['lifecycle'] for q in st['requests'])
    res = {
        'job': name, 'arm': arm, 'cell': cell, 'status': summ.get('status'),
        'requests': len(st['requests']), 'lifecycle': dict(lc), 'served': lc.get('completed', 0),
        'acked': len(acked), 'nonNormalAcked': sum(1 for h in acked if decisions[h][1]),
        'nonNormalAll': sum(1 for h, v in decisions.items() if v[1]),
        'decisionsAll': len(decisions),
        'alight': alight, 'riders': riders,
    }
    json.dump(res, open(outp, 'w'))
    return (arm, cell, name)

if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    m = json.load(open(MAN, encoding='utf-8'))
    jobs = [('C-30', c) for c in m['calibration']] + [(a, c) for c in m['test'] for a in ARMS]
    with Pool(6) as pool:
        for r in pool.imap_unordered(extract, jobs):
            print(r, flush=True)
