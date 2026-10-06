# Independent-B extraction v2 (written from scratch for this verification).
# v2: output file name is <parent folder>__<run folder> in runs/, because two gate runs in
# tier3-forecast-v1 share their folder name with T3.8 naive runs.
#
# For each run folder, read transcript-00.ndjson once and write a compact JSON to
# extracted/<RUN>.json with:
#   - integrity: frame count, frameSha256 failures
#   - decisions: produced hashes, acknowledged hashes (decisionApplied after the decision),
#     non-normal acknowledged count (certificate.body.normalOperation == False)
#   - alights: requestId -> list of envelope simTimeMs of passengerAlighted events
#   - final state (last runnerToAdapter checkpoint): requests and commitment ledger (reduced)
#   - promisePublished actions of acknowledged decisions (publicationId -> drop/pickup ETA)
#
# Usage: python -B b_extract.py <run_dir> [<run_dir> ...]
import base64, hashlib, json, os, sys, time

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'runs')


def reduce_entry(e):
    pp = e['publishedPromise']
    r = {
        'kind': e.get('kind'),
        'publicationId': e.get('publicationId'),
        't': pp.get('publishedAtMs'),
        'p_drop': pp['projection']['dropEtaMs'],
        'p_pick': pp['projection']['pickupEtaMs'],
    }
    ex = e.get('exogenousProjection')
    if ex is not None:
        r['e_drop'] = ex.get('dropEtaMs')
        r['e_pick'] = ex.get('pickupEtaMs')
    pv = e.get('previousPromise')
    if pv is not None:
        r['v_drop'] = pv['projection']['dropEtaMs']
        r['v_pick'] = pv['projection']['pickupEtaMs']
    d = e.get('deltas') or {}
    r['dvis_drop'] = (d.get('visible') or {}).get('dropEtaTotalMs')
    r['ddec_drop'] = (d.get('decisionInduced') or {}).get('dropEtaTotalMs')
    r['dexo_drop'] = (d.get('exogenous') or {}).get('dropEtaTotalMs')
    return r


def extract(run_dir):
    name = os.path.basename(os.path.dirname(os.path.normpath(run_dir))) + '__' + os.path.basename(os.path.normpath(run_dir))
    path = os.path.join(run_dir, 'transcript-00.ndjson')
    t_start = time.time()
    nframes = 0
    sha_fail = 0
    decision_line = {}       # hash -> line index of first decision frame
    decision_info = {}       # hash -> dict(normal, simTimeMs, promises)
    acked = {}               # hash -> line index of first decisionApplied after the decision
    ack_before_decision = 0
    alights = {}
    last_ckpt = None
    msg_counts = {}
    with open(path, 'r', encoding='utf-8') as f:
        for idx, line in enumerate(f):
            line = line.strip()
            if not line:
                continue
            rec = json.loads(line)
            raw = base64.b64decode(rec['frameBase64'])
            nframes += 1
            if hashlib.sha256(raw).hexdigest() != rec.get('frameSha256'):
                sha_fail += 1
            env = json.loads(raw)
            mt = env.get('messageType')
            dirn = rec['direction']
            key = dirn + ':' + str(mt)
            msg_counts[key] = msg_counts.get(key, 0) + 1
            if dirn == 'runnerToAdapter' and mt == 'decision':
                pl = env['payload']
                h = pl['decisionHash']
                if h not in decision_line:
                    decision_line[h] = idx
                    cert = pl.get('certificate') or {}
                    body = cert.get('body') or {}
                    promises = {}
                    for a in pl.get('actions', []) or []:
                        if a.get('decisionType') == 'promisePublished':
                            ap = a.get('payload') or {}
                            pr = ap.get('promise') or {}
                            pid = ap.get('publicationId')
                            promises.setdefault(pid, []).append([pr.get('dropEtaMs'), pr.get('pickupEtaMs'), pr.get('requestId')])
                    decision_info[h] = {
                        'normal': body.get('normalOperation'),
                        'hasBody': bool(body),
                        'simTimeMs': env.get('simTimeMs'),
                        'promises': promises,
                    }
            elif dirn == 'adapterToRunner' and mt == 'decisionApplied':
                h = env['payload']['decisionHash']
                if h in decision_line:
                    if h not in acked:
                        acked[h] = idx
                else:
                    ack_before_decision += 1
            elif dirn == 'adapterToRunner' and mt == 'eventBatch':
                st = env.get('simTimeMs')
                for ev in env['payload'].get('events', []) or []:
                    if ev.get('eventType') == 'passengerAlighted':
                        rid = ev['payload']['requestId']
                        alights.setdefault(rid, []).append(st)
            elif dirn == 'runnerToAdapter' and mt == 'checkpoint':
                last_ckpt = env

    out = {
        'run': name,
        'frames': nframes,
        'shaFailures': sha_fail,
        'msgCounts': msg_counts,
        'hasSummary': os.path.exists(os.path.join(run_dir, 'summary.json')),
        'hasFailure': os.path.exists(os.path.join(run_dir, 'failure-00.json')),
        'decisionsProduced': len(decision_line),
        'decisionsAcknowledged': len(acked),
        'ackWithoutPriorDecision': ack_before_decision,
        'nonNormalAcknowledged': sum(1 for h in acked if decision_info[h]['normal'] is False),
        'normalTrueAcknowledged': sum(1 for h in acked if decision_info[h]['normal'] is True),
        'missingBodyAcknowledged': sum(1 for h in acked if not decision_info[h]['hasBody']),
        'nonNormalProduced': sum(1 for h in decision_info if decision_info[h]['normal'] is False),
        'alights': alights,
        'finalCheckpoint': last_ckpt is not None,
    }
    # acknowledged promise publications
    ack_pubs = {}
    for h in acked:
        for pid, lst in decision_info[h]['promises'].items():
            ack_pubs.setdefault(pid, []).extend(lst)
    out['ackPublications'] = ack_pubs
    if last_ckpt is not None:
        st = last_ckpt['payload']['content']['onlineState']
        out['stateSimulationTimeMs'] = st.get('simulationTimeMs')
        out['requests'] = [
            {
                'requestId': r['requestId'],
                'lifecycle': r.get('lifecycle'),
                'actualPickupTimeMs': r.get('actualPickupTimeMs'),
                'arrivalTimeMs': r.get('arrivalTimeMs'),
            }
            for r in st.get('requests', [])
        ]
        out['ledger'] = [
            {'requestId': h['requestId'], 'entries': [reduce_entry(e) for e in h['entries']]}
            for h in st.get('commitmentLedger', [])
        ]
    out['seconds'] = round(time.time() - t_start, 1)
    os.makedirs(OUT, exist_ok=True)
    with open(os.path.join(OUT, name + '.json'), 'w', encoding='utf-8') as g:
        json.dump(out, g)
    print('%-45s frames=%d sha_fail=%d ckpt=%s summary=%s dec=%d ack=%d nonnormal=%d alighted=%d %.1fs' % (
        name, nframes, sha_fail, out['finalCheckpoint'], out['hasSummary'], out['decisionsProduced'],
        out['decisionsAcknowledged'], out['nonNormalAcknowledged'], len(alights), out['seconds']), flush=True)


if __name__ == '__main__':
    for d in sys.argv[1:]:
        extract(d)
