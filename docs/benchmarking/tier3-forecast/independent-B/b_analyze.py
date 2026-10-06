# Independent-B analysis (written from scratch for this verification).
#
# Input: runs/<parent>__<run>.json produced by b_extract2.py from the raw transcripts.
# Output: printed report (tee to b_analyze_output.txt) and b_results.json, b_cells.tsv.
#
# All cell-level quantities are exact rationals (fractions.Fraction); the bootstrap is
# also done in exact arithmetic so interval end points are exact.
import csv, json, os, random, sys
from fractions import Fraction as Fr

HERE = os.path.dirname(os.path.abspath(__file__))
RUNS = os.path.join(HERE, 'runs')
NAIVE = 'tier3-t38-v1'
FC = 'tier3-forecast-v1'
TT_DIR = (r'E:\RideBoundData\wp6\extracted\sha256\d9\d9e86f33645e5eec287d387f8d63ad41ddf41d4ef648138b65d636482e2c599e'
          r'\FleetPy_Manhattan\networks\Manhattan_2019_corrected')

DAYS = ['d20181114', 'd20181115', 'd20181116']
CELL_IDS = {
    'd20181114': ['r3', 'r4'],
    'd20181115': ['r1', 'r2', 'r4'],
    'd20181116': ['r1', 'r2', 'r4'],
}
WINDOWS = ['w07', 'w08']
CELLS = ['%s-s10-%s-%s' % (d, r, w) for w in WINDOWS for d in DAYS for r in CELL_IDS[d]]
assert len(CELLS) == 16

out_lines = []


def say(*a):
    s = ' '.join(str(x) for x in a)
    out_lines.append(s)
    print(s, flush=True)


def load(parent, run):
    p = os.path.join(RUNS, parent + '__' + run + '.json')
    with open(p, encoding='utf-8') as f:
        return json.load(f)


def completed_run(r):
    return r['hasSummary'] and r['finalCheckpoint']


issues = []


def rider_table(r):
    """Per promised rider: P0, t0, A (first alight), V_drop, dispatch sum, list of entries."""
    alights = r['alights']
    req = {q['requestId']: q for q in r['requests']}
    riders = []
    seen = set()
    for h in r['ledger']:
        rid = h['requestId']
        if rid in seen:
            issues.append('%s: duplicate ledger requestId %s' % (r['run'], rid))
        seen.add(rid)
        es = h['entries']
        if es[0]['kind'] != 'initialPromise':
            issues.append('%s: first entry kind %s' % (r['run'], es[0]['kind']))
        prev_p = es[0]['p_drop']
        prev_pp = es[0]['p_pick']
        V = 0
        disp = 0
        for e in es[1:]:
            if e['kind'] != 'revision':
                issues.append('%s: non-initial kind %s' % (r['run'], e['kind']))
            if e['v_drop'] != prev_p or e['v_pick'] != prev_pp:
                issues.append('%s: chain break %s' % (r['run'], rid))
            z = e['p_drop'] - e['v_drop']
            c = e['p_drop'] - e['e_drop']
            x = e['e_drop'] - e['v_drop']
            if abs(z) != e['dvis_drop'] or abs(c) != e['ddec_drop'] or abs(x) != e['dexo_drop']:
                issues.append('%s: delta mismatch %s' % (r['run'], rid))
            V += abs(z)
            disp += c
            prev_p = e['p_drop']
            prev_pp = e['p_pick']
        al = alights.get(rid)
        if al is not None and len(al) != 1:
            issues.append('%s: rider %s alighted %d times' % (r['run'], rid, len(al)))
        A = min(al) if al else None
        lc = req.get(rid, {}).get('lifecycle')
        riders.append({'rid': rid, 't0': es[0]['t'], 'P0': es[0]['p_drop'], 'A': A, 'V': V,
                       'disp': disp, 'entries': es, 'lifecycle': lc})
    # consistency: alighted riders without history; completed vs alighted
    for rid in alights:
        if rid not in seen:
            issues.append('%s: alight without ledger history %s' % (r['run'], rid))
    for q in r['requests']:
        if q['lifecycle'] == 'completed' and q['requestId'] not in alights:
            issues.append('%s: completed but no alight %s' % (r['run'], q['requestId']))
        if q['lifecycle'] != 'completed' and q['requestId'] in alights:
            issues.append('%s: alighted but lifecycle %s' % (r['run'], q['lifecycle']))
    # cross-check 2: every ledger publicationId appears exactly once among acknowledged publications
    ack = r['ackPublications']
    ledger_pids = set()
    for h in r['ledger']:
        for e in h['entries']:
            pid = e['publicationId']
            ledger_pids.add(pid)
            lst = ack.get(pid)
            if lst is None or len(lst) != 1:
                issues.append('%s: publication %s ack count %s' % (r['run'], pid, None if lst is None else len(lst)))
            elif lst[0][0] != e['p_drop'] or lst[0][1] != e['p_pick'] or lst[0][2] != h['requestId']:
                issues.append('%s: publication %s mismatch' % (r['run'], pid))
    for pid in ack:
        if pid not in ledger_pids:
            issues.append('%s: acknowledged publication missing from ledger %s' % (r['run'], pid))
    return riders


def run_metrics(r):
    riders = rider_table(r)
    served = sum(1 for q in r['requests'] if q['lifecycle'] == 'completed')
    ack = r['decisionsAcknowledged']
    nn = r['nonNormalAcknowledged']
    alighted = [x for x in riders if x['A'] is not None]
    n_al = len(alighted)
    own = sum(1 for x in alighted if x['A'] - x['P0'] > 30000)
    l60 = sum(1 for x in alighted if x['A'] - x['P0'] > 60000)
    vis = sum(1 for x in riders if x['V'] > 60000)
    return {
        'served': served,
        'requests': len(r['requests']),
        'ack': ack,
        'nonNormal': nn,
        'forced': Fr(nn, ack),
        'promised': len(riders),
        'promisedAlighted': n_al,
        'ownBreak': Fr(own, n_al),
        'late60': Fr(l60, n_al),
        'visible60': Fr(vis, len(riders)),
        'ownBreakCount': own, 'late60Count': l60, 'visible60Count': vis,
        'missingBody': r['missingBodyAcknowledged'],
        'ackWithoutPriorDecision': r['ackWithoutPriorDecision'],
        'produced': r['decisionsProduced'],
    }


def cell_day(c):
    return c.split('-')[0]


def cell_win(c):
    return c.split('-')[-1]


def bootstrap(values, B=10000, seed=20261007):
    """values: dict cell -> Fraction. Stratified (w07, w08) cluster (day-window) bootstrap.
    Returns sorted list of B replicate statistics (Fractions) and the point estimate."""
    strata = []
    N = len(values)
    for w in WINDOWS:
        cl = []
        for d in DAYS:  # sorted by day
            cells = [c for c in CELLS if cell_win(c) == w and cell_day(c) == d and c in values]
            if cells:
                cl.append([values[c] for c in cells])
        n_s = sum(len(x) for x in cl)
        strata.append((cl, n_s))
    rng = random.Random(seed)
    reps = []
    for _ in range(B):
        stat = Fr(0)
        for cl, n_s in strata:
            k = len(cl)
            pooled = []
            for _j in range(k):
                pooled.extend(cl[rng.randrange(k)])
            stat += Fr(n_s, N) * (sum(pooled, Fr(0)) / len(pooled))
        reps.append(stat)
    reps.sort()
    point = sum((Fr(n_s, N) * (sum((v for cl_ in [cl] for c_ in cl_ for v in c_), Fr(0)) / n_s))
                for cl, n_s in strata)
    return reps, point, strata


def interval(reps, lo_1based, hi_1based):
    return reps[lo_1based - 1], reps[hi_1based - 1]


def f(x, nd=4):
    return ('%+.' + str(nd) + 'f') % float(x)


def pp(x, nd=2):
    return ('%+.' + str(nd) + 'f') % (100 * float(x))


results = {}

# ---------------------------------------------------------------- Family B
say('=' * 100)
say('FAMILY B')
say('=' * 100)
M = {}   # (arm, cell) -> metrics
status = {}
for c in CELLS:
    for arm, parent in (('C-30', NAIVE), ('Mplus-30', NAIVE), ('C-30F', FC), ('Mplus-30F', FC)):
        r = load(parent, arm + '-' + c)
        ok = completed_run(r)
        status[(arm, c)] = ok
        if r['hasSummary'] != r['finalCheckpoint']:
            issues.append('%s: summary=%s but finalCheckpoint=%s' % (r['run'], r['hasSummary'], r['finalCheckpoint']))
        if ok:
            M[(arm, c)] = run_metrics(r)
        if r['shaFailures']:
            issues.append('%s: sha failures %d' % (r['run'], r['shaFailures']))

failed = [(a, c) for (a, c), ok in status.items() if not ok]
say('Failed runs (no summary.json / no final checkpoint):')
for a, c in sorted(failed):
    say('   %s-%s' % (a, c))
results['failed'] = ['%s-%s' % (a, c) for a, c in sorted(failed)]
naive_failed = [x for x in failed if x[0] in ('C-30', 'Mplus-30')]
say('Naive runs failed:', len(naive_failed))

# per-cell table
say('')
say('Per-cell metrics (served, forced, ownBreak(>30s), late60, visible60, promised/alighted):')
hdr = ['cell', 'arm', 'served', 'requests', 'ack', 'nonNormal', 'forced', 'promised', 'alighted', 'ownBreak', 'late60', 'visible60']
rows = []
for c in CELLS:
    for arm in ('C-30', 'C-30F', 'Mplus-30', 'Mplus-30F'):
        m = M.get((arm, c))
        if m is None:
            say('%-24s %-10s FAILED' % (c, arm))
            rows.append([c, arm] + ['FAILED'] * (len(hdr) - 2))
            continue
        say('%-24s %-10s served=%3d req=%3d ack=%3d nn=%3d forced=%.4f prom=%3d al=%3d own=%.4f l60=%.4f vis60=%.4f' % (
            c, arm, m['served'], m['requests'], m['ack'], m['nonNormal'], float(m['forced']), m['promised'],
            m['promisedAlighted'], float(m['ownBreak']), float(m['late60']), float(m['visible60'])))
        rows.append([c, arm, m['served'], m['requests'], m['ack'], m['nonNormal'], '%.6f' % float(m['forced']),
                     m['promised'], m['promisedAlighted'], '%.6f' % float(m['ownBreak']), '%.6f' % float(m['late60']),
                     '%.6f' % float(m['visible60'])])
with open(os.path.join(HERE, 'b_cells.tsv'), 'w', encoding='utf-8', newline='') as g:
    w = csv.writer(g, delimiter='\t')
    w.writerow(hdr)
    w.writerows(rows)


def mean(xs):
    xs = list(xs)
    return sum(xs, Fr(0)) / len(xs)


def chain_m(metric):
    cells = [c for c in CELLS if status[('Mplus-30F', c)]]
    naive = {c: M[('Mplus-30', c)][metric] for c in cells}
    fc = {c: M[('Mplus-30F', c)][metric] for c in cells}
    diff = {c: fc[c] - naive[c] for c in cells}
    mn, mf = mean(naive.values()), mean(fc.values())
    red = (mn - mf) / mn
    # per day-window decreases
    dw = []
    for w in WINDOWS:
        for d in DAYS:
            cs = [c for c in cells if cell_win(c) == w and cell_day(c) == d]
            if cs:
                dw.append(('%s-%s' % (d, w), mean(diff[c] for c in cs), len(cs)))
    dec = sum(1 for _, v, _n in dw if v < 0)
    reps, point, strata = bootstrap(diff)
    lo, hi = interval(reps, 125, 9876)
    if red >= Fr(1, 2) and dec == len(dw) and hi < 0:
        verdict = 'ARTEFACT'
    elif red >= Fr(1, 4) and hi < 0:
        verdict = 'PARTIAL'
    elif red >= Fr(1, 4) and hi >= 0:
        verdict = 'INCONCLUSIVE'
    else:
        verdict = 'PERSISTS'
    say('')
    say('CHAIN M  metric=%s  cells=%d (Mplus-30F completed)' % (metric, len(cells)))
    say('   mean naive (Mplus-30)  = %.6f' % float(mn))
    say('   mean forecast (Mplus-30F) = %.6f' % float(mf))
    say('   relative reduction = %.4f%%' % (100 * float(red)))
    say('   mean diff (F - naive) = %s pp ; stratified point = %s pp' % (pp(mean(diff.values())), pp(point)))
    for name, v, n in dw:
        say('      day-window %s (n=%d): diff %s pp' % (name, n, pp(v)))
    say('   decreases in day-windows: %d/%d' % (dec, len(dw)))
    say('   97.5%% interval (125th, 9876th): [%s, %s] pp' % (pp(lo), pp(hi)))
    say('   strata sizes: %s' % [(len(cl), n) for cl, n in strata])
    say('   VERDICT: %s' % verdict)
    return {'cells': len(cells), 'meanNaive': float(mn), 'meanForecast': float(mf), 'reduction': float(red),
            'diffPP': 100 * float(mean(diff.values())), 'loPP': 100 * float(lo), 'hiPP': 100 * float(hi),
            'decreases': dec, 'dayWindows': len(dw), 'verdict': verdict,
            'perDayWindowPP': {n: 100 * float(v) for n, v, _ in dw}}


results['chainM_forced'] = chain_m('forced')
results['chainM_ownBreak'] = chain_m('ownBreak')

# Chain C
cellsC = [c for c in CELLS if status[('C-30F', c)]]
dserv = {c: Fr(M[('C-30F', c)]['served'] - M[('C-30', c)]['served']) for c in cellsC}
reps, point, strata = bootstrap(dserv)
lo, hi = interval(reps, 125, 9876)
bc1 = lo >= -2
say('')
say('CHAIN C  served(C-30F) - served(C-30), cells=%d (C-30F completed)' % len(cellsC))
for c in cellsC:
    say('      %s: %+d' % (c, int(dserv[c])))
say('   mean diff = %.4f (point stratified %.4f)' % (float(mean(dserv.values())), float(point)))
say('   97.5%% interval: [%.4f, %.4f]' % (float(lo), float(hi)))
say('   B-C1 (lower >= -2): %s' % ('PASS' if bc1 else 'FAIL'))
results['chainC'] = {'cells': len(cellsC), 'mean': float(mean(dserv.values())), 'lo': float(lo), 'hi': float(hi),
                     'BC1': 'PASS' if bc1 else 'FAIL'}

# Worst-case sensitivity (failed C-30F counted as 0 served) -- extra, not asked
dserv_wc = dict(dserv)
for c in CELLS:
    if not status[('C-30F', c)]:
        dserv_wc[c] = Fr(0 - M[('C-30', c)]['served'])
reps_wc, point_wc, _ = bootstrap(dserv_wc)
lo_wc, hi_wc = interval(reps_wc, 125, 9876)
say('   (extra) worst case, failed C-30F = 0 served, 16 cells: mean %.4f [%.4f, %.4f] -> B-C1 %s' % (
    float(mean(dserv_wc.values())), float(lo_wc), float(hi_wc), 'PASS' if lo_wc >= -2 else 'FAIL'))
results['chainC_worstCase'] = {'mean': float(mean(dserv_wc.values())), 'lo': float(lo_wc), 'hi': float(hi_wc)}

# C-30F descriptives
v60 = mean(M[('C-30F', c)]['visible60'] for c in cellsC)
l60 = mean(M[('C-30F', c)]['late60'] for c in cellsC)
nn = sum(M[('C-30F', c)]['nonNormal'] for c in cellsC)
v60n = mean(M[('C-30', c)]['visible60'] for c in cellsC)
l60n = mean(M[('C-30', c)]['late60'] for c in cellsC)
say('')
say('C-30F over its %d completed cells: mean visible60 = %.4f, mean late60 = %.4f' % (len(cellsC), float(v60), float(l60)))
say('   (context) C-30 naive over the same cells: visible60 = %.4f, late60 = %.4f' % (float(v60n), float(l60n)))
say('   non-normal acknowledged decisions in completed C-30F runs: %d' % nn)
nn_failed = [(c, load(FC, 'C-30F-' + c)['nonNormalAcknowledged'], load(FC, 'C-30F-' + c)['nonNormalProduced'])
             for c in CELLS if not status[('C-30F', c)]]
say('   (extra) non-normal in failed C-30F transcripts (acknowledged, produced): %s' % nn_failed)
vis_up = 0
dws = []
for w in WINDOWS:
    for d in DAYS:
        cs = [c for c in cellsC if cell_win(c) == w and cell_day(c) == d]
        if cs:
            dv = mean(M[('C-30F', c)]['visible60'] - M[('C-30', c)]['visible60'] for c in cs)
            dws.append(('%s-%s' % (d, w), float(dv)))
            if dv > 0:
                vis_up += 1
say('   (extra) visible60 C-30F - C-30 per day-window: %s ; higher in %d/%d' % (
    ', '.join('%s %+.4f' % x for x in dws), vis_up, len(dws)))
results['C30F'] = {'cells': len(cellsC), 'visible60': float(v60), 'late60': float(l60), 'nonNormal': nn}

# C-30F - Mplus-30F served
cellsCM = [c for c in CELLS if status[('C-30F', c)] and status[('Mplus-30F', c)]]
dcm = {c: Fr(M[('C-30F', c)]['served'] - M[('Mplus-30F', c)]['served']) for c in cellsCM}
reps, point, strata = bootstrap(dcm)
lo95, hi95 = interval(reps, 250, 9751)
say('')
say('served(C-30F) - served(Mplus-30F), cells=%d (both completed)' % len(cellsCM))
for c in cellsCM:
    say('      %s: %+d' % (c, int(dcm[c])))
say('   mean = %.4f ; 95%% interval (250th, 9751st): [%.4f, %.4f]' % (float(mean(dcm.values())), float(lo95), float(hi95)))
results['C30F_minus_Mplus30F'] = {'cells': len(cellsCM), 'mean': float(mean(dcm.values())), 'lo': float(lo95), 'hi': float(hi95)}

# w07 Mplus forced / own-break, cells where both arms completed
for w in WINDOWS:
    cs = [c for c in CELLS if cell_win(c) == w and status[('Mplus-30F', c)] and status[('Mplus-30', c)]]
    a = mean(M[('Mplus-30', c)]['forced'] for c in cs)
    b = mean(M[('Mplus-30F', c)]['forced'] for c in cs)
    a2 = mean(M[('Mplus-30', c)]['ownBreak'] for c in cs)
    b2 = mean(M[('Mplus-30F', c)]['ownBreak'] for c in cs)
    say('%s Mplus (cells=%d): forced %.4f -> %.4f ; own-break %.4f -> %.4f' % (w, len(cs), float(a), float(b), float(a2), float(b2)))
    results['Mplus_' + w] = {'cells': len(cs), 'forcedNaive': float(a), 'forcedF': float(b),
                             'ownNaive': float(a2), 'ownF': float(b2)}
# C-30F late60 at w07 (extra, PB7)
cs = [c for c in cellsC if cell_win(c) == 'w07']
say('(extra) C-30F w07 late60 = %.4f (cells=%d); C-30 naive w07 late60 = %.4f' % (
    float(mean(M[('C-30F', c)]['late60'] for c in cs)), len(cs), float(mean(M[('C-30', c)]['late60'] for c in cs))))

# Gate cross-check (extra): forecast-v1 copies of the naive runs vs T3.8
say('')
for run in ('Mplus-30-d20181115-s10-r4-w08', 'C-30-d20181115-s10-r2-w07'):
    a = run_metrics(load(NAIVE, run))
    b = run_metrics(load(FC, run))
    keys = ['served', 'ack', 'nonNormal', 'promised', 'ownBreakCount', 'late60Count', 'visible60Count']
    same = all(a[k] == b[k] for k in keys)
    say('(extra) gate run %s: forecast-v1 copy equals T3.8 on %s: %s' % (run, keys, same))

# ---------------------------------------------------------------- SNOW
say('')
say('=' * 100)
say('SNOW')
say('=' * 100)
SRC_DAYS = ['2018-11-12', '2018-11-13', '2018-11-14']
tt = {}
for d in SRC_DAYS:
    with open(os.path.join(TT_DIR, d + '_tt_factors.csv'), encoding='utf-8') as g:
        rd = csv.DictReader(g)
        tt[d] = {int(float(row['simulation_time'])): float(row['travel_time_factor']) for row in rd}

BIN = 900000


def profile(ws_sec):
    m = []
    for b in range(9):
        vals = [tt[d][ws_sec + b * 900] for d in SRC_DAYS]
        m.append(sum(Fr(v) for v in vals) / 3)
    return m  # m[b] for b=0..8; beyond 8 held at m[8]


def mprof(m, b):
    return m[min(b, 8)]


def display(t, p, m):
    """D(t,p) = max(p, S), with int_t^S 1/g = W = p - t; exact Fractions."""
    W = Fr(p - t)
    if W <= 0:
        return Fr(p)
    b0 = t // BIN
    m0 = mprof(m, b0)
    tau = Fr(t)
    rem = W
    b = b0
    end = Fr((b0 + 1) * BIN)
    # current bin, g = 1
    if rem <= end - tau:
        S = tau + rem
    else:
        rem -= end - tau
        tau = end
        b = b0 + 1
        while True:
            g = mprof(m, b) / m0
            cap = Fr(BIN) / g
            if b >= 8 or rem <= cap:
                # beyond bin 8 g is constant, so solve directly
                S = tau + rem * g
                if b < 8 and rem > cap:
                    raise AssertionError
                break
            rem -= cap
            tau += BIN
            b += 1
    return max(Fr(p), S)


snow_cells = []
for r_ in ['r1', 'r2', 'r3', 'r4']:
    for w, ws in (('w14', 50400), ('w15', 54000)):
        snow_cells.append(('d20181115-s10-%s-%s' % (r_, w), ws))

prof = {ws: profile(ws) for ws in (50400, 54000)}
for ws, m in prof.items():
    say('m(b) window start %d s: %s' % (ws, ', '.join('%.4f' % float(x) for x in m)))
    say('   g(b)/g(0) ratios relative to bin 0: %s' % ', '.join('%.4f' % float(x / m[0]) for x in m))

Yn, Yd = [], []
led = {'n': 0, 'total': Fr(0), 'dispatch': Fr(0), 'anticipated': Fr(0), 'unanticipated': Fr(0)}
float_mismatch = 0
snow_rows = []
for c, ws in snow_cells:
    r = load(FC, 'C-30-' + c)
    assert completed_run(r)
    riders = rider_table(r)
    m = prof[ws]
    served_riders = [x for x in riders if x['lifecycle'] == 'completed']
    n_served = sum(1 for q in r['requests'] if q['lifecycle'] == 'completed')
    if len(served_riders) != n_served:
        issues.append('%s: served %d but served with history %d' % (r['run'], n_served, len(served_riders)))
    yn = yd = 0
    for x in served_riders:
        D0 = display(x['t0'], x['P0'], m)
        late_n = x['A'] - x['P0'] > 60000
        late_d = x['A'] - D0 > 60000
        yn += late_n
        yd += late_d
        if late_n:
            led['n'] += 1
            led['total'] += x['A'] - x['P0']
            led['dispatch'] += x['disp']
            led['anticipated'] += D0 - x['P0']
            led['unanticipated'] += x['A'] - D0 - x['disp']
    Yn.append(Fr(yn, len(served_riders)))
    Yd.append(Fr(yd, len(served_riders)))
    snow_rows.append((c, len(served_riders), yn, yd))
    say('%-22s served=%3d late_naive=%2d (%.4f) late_disp=%2d (%.4f)' % (c, len(served_riders), yn, yn / len(served_riders), yd, yd / len(served_riders)))

yn_m = mean(Yn)
yd_m = mean(Yd)
say('Y_naive (cell mean over %d cells) = %.4f' % (len(Yn), float(yn_m)))
say('Y_disp  (cell mean over %d cells) = %.4f' % (len(Yd), float(yd_m)))
say('share of Y_naive removed by display = %.2f%%' % (100 * float((yn_m - yd_m) / yn_m)))
say('LEDGER over %d riders with A - P0 > 60 s:' % led['n'])
say('   total A - P0      = %.1f s' % (float(led['total']) / 1000))
say('   dispatch          = %.1f s' % (float(led['dispatch']) / 1000))
say('   anticipated       = %.1f s' % (float(led['anticipated']) / 1000))
say('   unanticipated     = %.1f s' % (float(led['unanticipated']) / 1000))
say('   unanticipated share of total = %.2f%%' % (100 * float(led['unanticipated'] / led['total'])))
say('   check: dispatch + anticipated + unanticipated - total = %s ms' % float(led['dispatch'] + led['anticipated'] + led['unanticipated'] - led['total']))
results['snow'] = {'Ynaive': float(yn_m), 'Ydisp': float(yd_m), 'ledgerRiders': led['n'],
                   'totalS': float(led['total']) / 1000, 'dispatchS': float(led['dispatch']) / 1000,
                   'anticipatedS': float(led['anticipated']) / 1000, 'unanticipatedS': float(led['unanticipated']) / 1000,
                   'unanticipatedShare': float(led['unanticipated'] / led['total'])}

# float re-check of the snow display (independent arithmetic path)


def display_float(t, p, m):
    W = float(p - t)
    if W <= 0:
        return float(p)
    b0 = t // BIN
    m0 = float(mprof(m, b0))
    S = None
    tau = float(t)
    rem = W
    end = float((b0 + 1) * BIN)
    if rem <= end - tau:
        S = tau + rem
    else:
        rem -= end - tau
        tau = end
        b = b0 + 1
        while S is None:
            g = float(mprof(m, b)) / m0
            if b >= 8 or rem <= BIN / g:
                S = tau + rem * g
            else:
                rem -= BIN / g
                tau += BIN
                b += 1
    return max(float(p), S)


fm = 0
for c, ws in snow_cells:
    r = load(FC, 'C-30-' + c)
    for x in rider_table(r):
        if x['lifecycle'] != 'completed':
            continue
        a = display(x['t0'], x['P0'], prof[ws])
        b = display_float(x['t0'], x['P0'], prof[ws])
        if abs(float(a) - b) > 1e-3 or ((x['A'] - a > 60000) != (x['A'] - b > 60000)):
            fm += 1
say('float vs exact display classification/value mismatches: %d' % fm)

say('')
say('INTEGRITY ISSUES (%d):' % len(issues))
for s in issues[:200]:
    say('   ' + s)

with open(os.path.join(HERE, 'b_results.json'), 'w', encoding='utf-8') as g:
    json.dump(results, g, indent=1)
with open(os.path.join(HERE, 'b_analyze_output.txt'), 'w', encoding='utf-8') as g:
    g.write('\n'.join(out_lines) + '\n')
