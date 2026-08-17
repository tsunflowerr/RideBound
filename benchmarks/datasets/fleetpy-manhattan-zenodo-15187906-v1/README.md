# FleetPy Manhattan v1 — WP6 public-source registration

This directory contains metadata and instructions only. The 408.9 MB publisher ZIP
is never committed to RideBound.

## Locked source

- Dataset: *FleetPy: Input Data for Manhattan Case Study*, version 1.0
- Authors: Roman Engelhardt and Florian Dandl
- Record DOI: <https://doi.org/10.5281/zenodo.15187906>
- All-version DOI: <https://doi.org/10.5281/zenodo.15187905>
- File: `FleetPy_Manhattan.zip`
- Publisher MD5: `8b11882ae9c6d87f666bf6e006806744`
- Exact publisher length: `408878341` bytes
- Local SHA-256: `d9e86f33645e5eec287d387f8d63ad41ddf41d4ef648138b65d636482e2c599e`
- License: CC BY 4.0
- Published: 2025-04-10

The in-app Browser rechecked the official record on 2026-08-09. The page states that
the demand derives from NYC TLC trips for 2018-11-11 through 2018-11-18 filtered to
Manhattan origins/destinations, and that the network derives from Manhattan OSM data.

## Opt-in acquisition

Choose an absolute cache outside this repository and explicitly accept the license:

```powershell
dotnet run --project tools/RideBound.Wp6Dataset -- download `
  --cache E:\RideBoundData\wp6 `
  --repository E:\Code\RideBound `
  --accept-license CC-BY-4.0 `
  --extract
```

The tool streams to a new staging file, caps bytes, verifies publisher MD5 and local
SHA-256, atomically promotes to a read-only content address, preflights every ZIP
member, then atomically promotes a verified extraction. It rejects traversal,
absolute/backslash/drive/device/non-NFC paths, duplicate/case/file-directory
collisions, symlink/reparse metadata and entry/count/total/ratio bombs.

The first successful download writes the exact byte length, local SHA-256 and UTC
retrieval time into `dataset-descriptor.json` beside the cached object. A rerun cannot
overwrite that receipt or the object.

Verified acquisition on 2026-08-09 produced 335 file members, total uncompressed
length `1022750557`, and canonical inventory SHA-256
`f9b28bb17850881e2e1a0784d7ff0aa1885b7175ab9be8db5e8ac6d969240bbd`.

## Deterministic public derivatives

After acquisition, regenerate and byte-verify both bounded derivatives:

```powershell
dotnet run --project tools/RideBound.Wp6Normalize --configuration Release -- normalize `
  --cache E:\RideBoundData\wp6 `
  --repository E:\Code\RideBound `
  --accept-license CC-BY-4.0 `
  --profile all
```

The normalizer rehashes every registered input member, parses exact demand/network/
factor contracts, verifies directed strong connectivity, greedily builds a dense
policy-independent real-node pool under the bound, then HMAC-ranks eligible rows.
Directed shortest paths use only source arcs and ties-to-even integer-ms conversion.
Current exact outputs are tiny 8 requests/2 vehicles/16 nodes/240 arcs and medium
128/32/96/9,120. Both conserve all 21,400 source rows. Existing non-identical output
is never overwritten; an exact rerun reports `reusedExactDerivative=true`.

## Claim boundary

This release is useful for mechanical normalization and later simulator scenarios.
It does not contain observed RideBound commitment preferences, satisfaction labels,
or evidence that any policy is effective, non-inferior or production-ready. WP6 must
label any commitment policy assignment as `syntheticPolicyOverlay`.
