import sys,pathlib,types
source=pathlib.Path('C:\\RideBoundContinuation\\2026-10-15\\analysis-r46\\extract_stage1b.py')
ex=types.ModuleType('fresh_extractor'); ex.__file__=str(source)
exec(compile(source.read_bytes(),str(source),'exec'),ex.__dict__)
fake=types.ModuleType('display_layer');fake.__file__=str(ex.SCRATCH/ex.PINNED_MODULES['display_layer'][0])
sys.modules['display_layer']=fake
try:
    ex.readers()
except ex.Refusal as error:
    assert 'already loaded' in str(error)
    print('preloaded unverified module refused')
else:
    raise AssertionError('preloaded module accepted')
