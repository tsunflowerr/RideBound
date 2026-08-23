# RideBound FleetPy 1.0.2 adapter

This directory contains only RideBound-owned adapter code, executable probes and
environment locks. The FleetPy checkout, Python environment and result artifacts
must stay outside this repository.

## Pinned source

- FleetPy tag: `1.0.2`
- commit: `053aa9d4fcfde91c5d303435d5748f9206c071b0`
- license: MIT
- supported platform lock: `win-64`, CPython 3.10

The capability probe verifies the Git identity, clean checkout, critical source
hashes, abstract callback set, imports, node/edge position round-trip, actual
`SimulationVehicle._move` position update and the default non-forced assignment
path before adapter code is imported.

## Reproduce the environment

From PowerShell, with a trusted Micromamba 2.3.2 binary:

```powershell
$externalRoot = 'E:\RideBoundData\wp7'
& "$externalRoot\tools\micromamba-2.3.2\Library\bin\micromamba.exe" `
  create -y -p "$externalRoot\envs\fleetpy-1.0.2-repro" `
  -f .\simulators\fleetpy-ridebound\environment.lock.yml
```

FleetPy itself is not installed into the environment. Clone it into an external
directory, check out the exact tag, and give that root to the probe:

```powershell
& "$externalRoot\envs\fleetpy-1.0.2-repro\python.exe" `
  .\simulators\fleetpy-ridebound\capability_probe.py `
  --fleetpy-root "$externalRoot\FleetPy-1.0.2"
```

The process exits non-zero and emits a typed JSON failure if source, environment,
position semantics or the non-forced assignment path drifts. A successful report
is diagnostic evidence only; it is not a simulator-effectiveness claim.

## Run the adapter test suite

The suite lives in `tests/` and has no `__init__.py`, so discovery must start
inside that directory. Thirteen FleetPy contract tests skip silently unless
`RIDEBOUND_FLEETPY_ROOT` points at the pinned checkout — a bare run reports
`OK (skipped=13)`, which is not the documented 77/77 baseline:

```powershell
$env:RIDEBOUND_FLEETPY_ROOT = 'E:\RideBoundData\wp7\FleetPy-1.0.2'
cd .\simulators\fleetpy-ridebound\tests
& 'E:\RideBoundData\wp7\envs\fleetpy-1.0.2\python.exe' -m unittest discover -s . -t . -p 'test_*.py'
```

`pytest` is not installed in the pinned environment; `unittest` is the supported
runner.
