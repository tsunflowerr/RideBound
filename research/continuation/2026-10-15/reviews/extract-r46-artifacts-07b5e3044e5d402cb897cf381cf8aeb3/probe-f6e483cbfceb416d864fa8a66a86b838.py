import sys, pathlib, types, importlib.util, importlib.machinery, importlib._bootstrap_external as be, io, csv, json
from unittest import mock
source = pathlib.Path('C:\\RideBoundContinuation\\2026-10-15\\analysis-r46\\extract_stage1b.py')
ex = types.ModuleType('fresh_extractor')
ex.__file__ = str(source)
sys.modules[ex.__name__] = ex
exec(compile(source.read_bytes(),str(source),'exec'),ex.__dict__)
ex.SCRATCH = pathlib.Path('C:\\RideBoundContinuation\\2026-10-15\\reviews\\extract-r46-artifacts-07b5e3044e5d402cb897cf381cf8aeb3\\source-copies')
ex.READER_PATHS = tuple(ex.SCRATCH / p for p in ('tang3/evidence','optimize','optimize/codex-debate/claude-work/r13-final-judge'))
valid = 0
for name,(relative,want) in ex.PINNED_MODULES.items():
    path = ex.SCRATCH / relative
    stat = path.stat()
    poison = 'poisoned-cache-' + name
    code = compile(path.read_bytes() + ('\nCACHE_POISON = ' + repr(poison) + '\n').encode(),str(path),'exec')
    cache = pathlib.Path(importlib.util.cache_from_source(str(path)))
    cache.parent.mkdir(parents=True,exist_ok=True)
    cache.write_bytes(be._code_to_timestamp_pyc(code,int(stat.st_mtime),stat.st_size))
    assert poison in importlib.machinery.SourceFileLoader(name,str(path)).get_code(name).co_consts
    valid += 1
assert valid == 10
R = ex.readers()
assert len(R['verifiedSources']) == 10
assert all(not hasattr(sys.modules[name],'CACHE_POISON') for name in ex.PINNED_MODULES)
dl = R['display_layer']
before = dl.display(0,1800000,dl.profile('2018-11-14','w08','causal-no15'))
assert before == 1847580.4741262272, before
v13 = sys.modules['preflight_world_v1_3']
frozen_source = v13.preflight_world
original_open = pathlib.Path.open
disk_reads = []
def transient_open(path,*args,**kwargs):
    if ex.norm(path).startswith(ex.norm(ex.FACTORS)+__import__('os').sep):
        disk_reads.append(str(path))
        raw = R['verifiedData'][ex.norm(path)]
        rows = list(csv.DictReader(io.StringIO(raw.decode())))
        for row in rows:
            row['travel_time_factor'] = str(float(row['travel_time_factor']) * (10 if int(row['simulation_time']) >= 29700 else 1))
        output = io.StringIO()
        writer = csv.DictWriter(output,fieldnames=list(rows[0]))
        writer.writeheader()
        writer.writerows(rows)
        return io.StringIO(output.getvalue())
    return original_open(path,*args,**kwargs)
with mock.patch.object(pathlib.Path,'open',transient_open):
    v13.preflight_world = frozen_source.original
    vulnerable = dl.display(0,1800000,dl.profile('2018-11-14','w08','causal-no15'))
    assert vulnerable != before
    assert len(disk_reads) == 2
    v13.preflight_world = frozen_source
    disk_reads.clear()
    after = dl.display(0,1800000,dl.profile('2018-11-14','w08','causal-no15'))
    assert after == before
    assert disk_reads == []
    assert ex.readers() is R
print(json.dumps({'valid_poisoned_caches':valid,'verified_source_modules':len(R['verifiedSources']),
                   'D0_ms':before,'transient_vulnerable_D0_ms':vulnerable,'frozen_D0_ms':after,
                   'disk_factor_reads_after_freezing':len(disk_reads)}))
