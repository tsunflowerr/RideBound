# Independent-B sensitivity checks on interpretation choices (own code, reads b_cells.tsv
# written by b_analyze.py and the extracted runs).
#  1. Bootstrap stratum weight: fixed (cells in stratum / total) vs per-replicate pooled count.
#  2. One RNG shared across all statistics (in the order forced, ownBreak, served C, served C-M)
#     instead of a fresh random.Random(20261007) per statistic.
#  3. Exact snow ledger components in ms.
import csv, json, os, random
from fractions import Fraction as Fr

HERE = os.path.dirname(os.path.abspath(__file__))
WINDOWS = ['w07', 'w08']
DAYS = ['d20181114', 'd20181115', 'd20181116']

rows = list(csv.DictReader(open(os.path.join(HERE, 'b_cells.tsv'), encoding='utf-8'), delimiter='\t'))
T = {}
for r in rows:
    if r['served'] == 'FAILED':
        continue
    T[(r['arm'], r['cell'])] = r
cells = sorted({r['cell'] for r in rows}, key=lambda c: (c.split('-')[-1], c.split('-')[0], c))


def frac(s):
    return Fr(s)  # six-decimal strings; only used for the served-count statistics below


def load_run(parent, run):
    return json.load(open(os.path.join(HERE, 'runs', parent + '__' + run + '.json'), encoding='utf-8'))


# exact per-cell forced / ownBreak from the runs (not rounded tsv values)
def exact(arm, parent, c):
    r = load_run(parent, arm + '-' + c)
    alights = r['alights']
    own = 0
    n = 0
    for h in r['ledger']:
        a = alights.get(h['requestId'])
        if a:
            n += 1
            own += (min(a) - h['entries'][0]['p_drop'] > 30000)
    return {'forced': Fr(r['nonNormalAcknowledged'], r['decisionsAcknowledged']), 'ownBreak': Fr(own, n),
            'served': sum(1 for q in r['requests'] if q['lifecycle'] == 'completed')}


E = {}
for c in cells:
    for arm, parent in (('C-30', 'tier3-t38-v1'), ('Mplus-30', 'tier3-t38-v1'), ('C-30F', 'tier3-forecast-v1'), ('Mplus-30F', 'tier3-forecast-v1')):
        if (arm, c) in T:
            E[(arm, c)] = exact(arm, parent, c)


def strata_of(values):
    st = []
    for w in WINDOWS:
        cl = []
        for d in DAYS:
            cs = [c for c in cells if c.split('-')[-1] == w and c.split('-')[0] == d and c in values]
            if cs:
                cl.append([values[c] for c in cs])
        st.append((cl, sum(len(x) for x in cl)))
    return st


def boot(values, rng, mode='fixed', B=10000):
    st = strata_of(values)
    N = len(values)
    reps = []
    for _ in range(B):
        if mode == 'fixed':
            s = Fr(0)
            for cl, n_s in st:
                k = len(cl)
                pooled = []
                for _j in range(k):
                    pooled.extend(cl[rng.randrange(k)])
                s += Fr(n_s, N) * sum(pooled, Fr(0)) / len(pooled)
        else:  # pooled-count weighting: grand mean of all drawn cells
            allc = []
            for cl, n_s in st:
                k = len(cl)
                for _j in range(k):
                    allc.extend(cl[rng.randrange(k)])
            s = sum(allc, Fr(0)) / len(allc)
        reps.append(s)
    reps.sort()
    return reps


def show(name, reps, scale, lo, hi):
    print('   %-38s [%+.4f, %+.4f]' % (name, scale * float(reps[lo - 1]), scale * float(reps[hi - 1])))


mcells = [c for c in cells if ('Mplus-30F', c) in E]
ccells = [c for c in cells if ('C-30F', c) in E]
cm = [c for c in cells if ('C-30F', c) in E and ('Mplus-30F', c) in E]
stats = [
    ('chainM forced (pp, 97.5%)', {c: E[('Mplus-30F', c)]['forced'] - E[('Mplus-30', c)]['forced'] for c in mcells}, 100, 125, 9876),
    ('chainM ownBreak (pp, 97.5%)', {c: E[('Mplus-30F', c)]['ownBreak'] - E[('Mplus-30', c)]['ownBreak'] for c in mcells}, 100, 125, 9876),
    ('chainC served (97.5%)', {c: Fr(E[('C-30F', c)]['served'] - E[('C-30', c)]['served']) for c in ccells}, 1, 125, 9876),
    ('C-30F - Mplus-30F served (95%)', {c: Fr(E[('C-30F', c)]['served'] - E[('Mplus-30F', c)]['served']) for c in cm}, 1, 250, 9751),
]
print('1. fresh RNG per statistic, fixed stratum weights (primary reading):')
for name, v, sc, lo, hi in stats:
    show(name, boot(v, random.Random(20261007)), sc, lo, hi)
print('2. fresh RNG per statistic, per-replicate pooled-count weighting:')
for name, v, sc, lo, hi in stats:
    show(name, boot(v, random.Random(20261007), mode='pooled'), sc, lo, hi)
print('3. one RNG shared across statistics in the listed order, fixed weights:')
rng = random.Random(20261007)
for name, v, sc, lo, hi in stats:
    show(name, boot(v, rng), sc, lo, hi)
print('4. fresh RNG, fixed weights, 0-based index read as 1-based (124th/9875th and 249th/9750th):')
for name, v, sc, lo, hi in stats:
    show(name, boot(v, random.Random(20261007)), sc, lo - 1, hi - 1)
