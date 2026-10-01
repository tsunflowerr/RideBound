"""Independent verifier: analysis of cached per-run raw metrics (own code)."""
import json, os, math, random, collections, statistics
CACHE = r'E:\Code\Report_INT3508\ridebound-scratchpad\tang3\t38\independent\cache'
MAN = r'E:\Code\Report_INT3508\ridebound-scratchpad\tang3\t38\split-t38-v1.json'
OUTF = r'E:\Code\Report_INT3508\ridebound-scratchpad\tang3\t38\independent\iv_analyze.out.txt'
GAMMAF = r'E:\Code\Report_INT3508\ridebound-scratchpad\tang3\t38\GAMMA-t38-v1.json'
ARMS = ['U', 'C-30', 'Mplus-30', 'Mplus-60', 'V-30']
INF = float('inf')
m = json.load(open(MAN, encoding='utf-8'))
calib, test = m['calibration'], m['test']
out = []


def P(*a):
    s = ' '.join(str(x) for x in a)
    print(s)
    out.append(s)


def load(arm, cell):
    for cand in (f'{arm}-{cell}', f'{arm}-{cell}-rerun1'):
        p = os.path.join(CACHE, cand + '.json')
        if os.path.exists(p):
            return json.load(open(p))
    raise SystemExit('missing ' + arm + cell)


R = {}
for c in calib:
    R[('C-30', c)] = load('C-30', c)
for c in test:
    for a in ARMS:
        R[(a, c)] = load(a, c)
P('status of all runs:', dict(collections.Counter(r['status'] for r in R.values())))
P('folders used for reruns:', [r['job'] for r in R.values() if r['job'].endswith('rerun1')])

# cross-check 1 (delta fields vs |x|,|c|,|z|), sanity for sign convention and reconstruction
bad = 0
nrev = 0
for r in R.values():
    for q in r['riders'].values():
        for v in q['rev']:
            nrev += 1
            if (abs(v['e_d'] - v['v_d']) != v['dl_x'] or abs(v['p_d'] - v['e_d']) != v['dl_c']
                    or abs(v['p_d'] - v['v_d']) != v['dl_z']):
                bad += 1
P('cross-check1 (drop deltas): revisions', nrev, 'mismatches', bad)


def XCV(rider):
    X = sum(abs(v['e_d'] - v['v_d']) for v in rider['rev'])
    C = sum(abs(v['p_d'] - v['e_d']) for v in rider['rev'])
    V = sum(abs(v['p_d'] - v['v_d']) for v in rider['rev'])
    return X, C, V


def score(r):
    if not r['riders']:
        return 0
    s = 0
    for rid, q in r['riders'].items():
        if rid not in r['alight']:
            return INF
        s = max(s, XCV(q)[0])
    return s


# 1 calibration
P('\n== 1. calibration ==')
cs = {c: score(R[('C-30', c)]) for c in calib}
for c in calib:
    r = R[('C-30', c)]
    P(f'  {c}: S={cs[c]} ms  promised={len(r["riders"])} alighted-of-promised={sum(1 for x in r["riders"] if x in r["alight"])} served={r["served"]}')
n = len(calib)
k = -(-((n + 1) * 9) // 10)   # ceil((n+1)(1-alpha)), alpha = 1/10, integer arithmetic
srt = sorted(cs.values())
gamma = srt[k - 1] if k <= n else INF
P('n =', n, 'k =', k, 'gamma(ms) =', gamma)
g = json.load(open(GAMMAF, encoding='utf-8'))
P('GAMMA file (read after computing):', json.dumps(g)[:700])

# 2 test scores
P('\n== 2. test scores ==')
ts = {c: score(R[('C-30', c)]) for c in test}
viol = []
for c in test:
    cov = ts[c] <= gamma
    if not cov:
        viol.append(c)
    P(f'  {c}: S={ts[c]} ms covered={cov}')
P('covered', len(test) - len(viol), 'violating', len(viol), [(c, ts[c]) for c in viol])

# 7
P('\n== 7. covered C-30 episodes: max_r V_r <= 30000+gamma ==')
worst = 0
fails = []
worstC = 0
for c in test:
    if ts[c] <= gamma:
        r = R[('C-30', c)]
        mv = max((XCV(q)[2] for q in r['riders'].values()), default=0)
        mc = max((XCV(q)[1] for q in r['riders'].values()), default=0)
        worst = max(worst, mv)
        worstC = max(worstC, mc)
        if mv > 30000 + gamma:
            fails.append((c, mv))
P('worst max V among covered =', worst, 'bound =', 30000 + gamma, 'failures:', fails, '| worst max C among covered =', worstC)
allV = max(max((XCV(q)[2] for q in R[('C-30', c)]['riders'].values()), default=0) for c in test)
P('max V over ALL 34 C-30 test episodes =', allV)

# 3 served
P('\n== 3. served ==')
served = {a: [R[(a, c)]['served'] for c in test] for a in ARMS}
P('cell | ' + ' | '.join(ARMS))
for i, c in enumerate(test):
    P(f'  {c} | ' + ' | '.join(str(served[a][i]) for a in ARMS))
P('totals:', {a: sum(served[a]) for a in ARMS})
D_M = [served['C-30'][i] - served['Mplus-30'][i] for i in range(34)]
D_V = [served['C-30'][i] - served['V-30'][i] for i in range(34)]
F = [served['U'][i] - served['C-30'][i] for i in range(34)]
P('D_M per cell', D_M)
P('D_V per cell', D_V)
P('F per cell', F)
for nm, v in (('D_M', D_M), ('D_V', D_V), ('F', F)):
    P(f'{nm}: mean {sum(v) / 34:+.4f}  #|diff|>2: {sum(1 for x in v if abs(x) > 2)}  min {min(v)} max {max(v)}')


def e2(r):
    ps = r['riders']
    if not ps:
        return 0.0
    return sum(1 for q in ps.values() if XCV(q)[2] > 60000) / len(ps)


dE2_V = [e2(R[('C-30', c)]) - e2(R[('V-30', c)]) for c in test]
dE2_M = [e2(R[('C-30', c)]) - e2(R[('Mplus-30', c)]) for c in test]
P('runs with no promised riders:', [(a, c) for (a, c), r in R.items() if not r['riders']])

# 4 bootstrap
P('\n== 4. bootstrap ==')
stats = {'D_M': D_M, 'D_V': D_V, 'F': F, 'dE2_V': dE2_V, 'dE2_M': dE2_M}
rng = random.Random(20261002)
B = 10000
means = {k_: [] for k_ in stats}
for _ in range(B):
    idxs = []
    for _j in range(34):
        idxs.append(rng.randrange(34))
    for k_, v in stats.items():
        means[k_].append(math.fsum(v[i] for i in idxs) / 34)
for k_, v in stats.items():
    s = sorted(means[k_])
    P(f'{k_}: mean {sum(v) / 34:+.6f}  95% interval [{s[249]:+.6f}, {s[9750]:+.6f}]')

# 5 non-normal
P('\n== 5. non-normal decisions ==')
nn_c = {c: R[('C-30', c)]['nonNormalAcked'] for c in calib + test}
P('C-30 runs counted:', len(nn_c), 'total non-normal (acked)', sum(nn_c.values()), 'nonzero:', {c: v for c, v in nn_c.items() if v})
P('C-30 non-normal over ALL decisions (not only acked):', sum(R[('C-30', c)]['nonNormalAll'] for c in calib + test))
for a in ('Mplus-30', 'V-30', 'Mplus-60', 'U', 'C-30'):
    sh = [R[(a, c)]['nonNormalAcked'] / R[(a, c)]['acked'] for c in test]
    P(f'{a}: mean share non-normal of acked decisions {sum(sh) / 34:.4f}; total non-normal {sum(R[(a, c)]["nonNormalAcked"] for c in test)}; total acked {sum(R[(a, c)]["acked"] for c in test)}; cells>0 {sum(1 for x in sh if x > 0)}')
P('acked vs all decisions mismatch runs:', sum(1 for r in R.values() if r['acked'] != r['decisionsAll']))

# 6 own-promise-broken


def broken(arm, cell, denom='alighted'):
    r = R[(arm, cell)]
    ps = r['riders']
    if arm == 'C-30':
        num = sum(1 for q in ps.values() if XCV(q)[1] > 30000)
        den = len(ps)
    elif arm == 'V-30':
        num = sum(1 for q in ps.values() if XCV(q)[2] > 30000)
        den = len(ps)
    else:
        thr = 30000 if arm == 'Mplus-30' else 60000
        al = {rid: q for rid, q in ps.items() if rid in r['alight']}
        num = sum(1 for rid, q in al.items() if r['alight'][rid] - q['p0_drop'] > thr)
        den = len(al) if denom == 'alighted' else len(ps)
    return num, den


P('\n== 6. own-promise-broken ==')
for arm in ('C-30', 'Mplus-30', 'Mplus-60', 'V-30'):
    for denom in (('alighted', 'promised') if arm.startswith('Mplus') else ('promised',)):
        for lab, sel in (('all', test), ('w07', [c for c in test if c.endswith('w07')]),
                         ('w08', [c for c in test if c.endswith('w08')]),
                         ('w17', [c for c in test if c.endswith('w17')])):
            sh = []
            pos = 0
            zero_den = 0
            for c in sel:
                nu, de = broken(arm, c, denom)
                if de == 0:
                    zero_den += 1
                sh.append(nu / de if de else 0.0)
                pos += (nu > 0)
            P(f'{arm} denom={denom} {lab}: mean share {sum(sh) / len(sh):.4f}  cells>0 {pos}/{len(sel)}  (cells with den 0: {zero_den})')
for arm in ('Mplus-30', 'Mplus-60'):
    tn = td = 0
    for c in test:
        nu, de = broken(arm, c)
        tn += nu
        td += de
    P(f'{arm} pooled over riders: {tn}/{td} = {tn / td:.4f}')
diffs = []
for c in test[:6]:
    r = R[('Mplus-30', c)]
    for rid, q in r['riders'].items():
        if rid in r['alight'] and q['rev']:
            diffs.append(r['alight'][rid] - q['rev'][-1]['p_d'])
P('sanity: alight - last published drop ETA (Mplus-30, first 6 cells): n', len(diffs), 'median', statistics.median(diffs), 'min', min(diffs), 'max', max(diffs))
P('promised riders without alight, per arm (test):', {a: sum(1 for c in test for rid in R[(a, c)]['riders'] if rid not in R[(a, c)]['alight']) for a in ARMS})
P('E2 means over test cells:', {a: round(sum(e2(R[(a, c)]) for c in test) / 34, 6) for a in ARMS})
open(OUTF, 'w', encoding='utf-8').write('\n'.join(out))
