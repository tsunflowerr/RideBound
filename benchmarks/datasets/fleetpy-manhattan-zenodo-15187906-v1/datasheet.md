# Datasheet — FleetPy Manhattan v1 registration

## Motivation and composition

The source is registered to replace synthetic-only WP6 checks with an independently
published, versioned urban ride-demand/network release. It contains FleetPy inputs:
Manhattan-filtered NYC TLC trip derivatives, an OSM-derived Manhattan network,
travel-time/distance arrays and zones. It was not collected for RideBound.

## Provenance and transformations

The publisher record is Zenodo `15187906`, version 1.0, published 2025-04-10. WP6
retains publisher filename/MD5/license/citation and computes a local SHA-256 before
any extraction. The verified 2026-08-09 receipt locks length `408878341`, SHA-256
`d9e86f...c599e`, 335 files, `1022750557` uncompressed bytes and inventory SHA-256
`f9b28b...0bbd`. `RB-WP6-004` owns deterministic parsing, filtering, rounding,
pseudonymization, conservation logs and scenario identities; this ticket performs no
semantic transformation.

## Distribution and maintenance

CC BY 4.0 permits reuse with attribution. Raw bytes live only in an explicitly
provided content-addressed local cache outside Git. A new Zenodo record/file/checksum
requires registry/ADR review; code never follows a mutable `latest` alias.

## Known limitations and prohibited interpretation

- TLC taxi trips are not the full travel population and cover one November 2018 week.
- O/D and road-network preprocessing embed source/model selection choices.
- There is no observed promise budget, commitment preference, satisfaction, protected
  group or RideBound outcome label.
- Mechanical use cannot support effectiveness, non-inferiority, fairness, satisfaction,
  deployment or production-SLA claims.
- Any later synthetic commitment-policy overlay must be reported as synthetic rather
  than inferred rider preference.

## Privacy and access

The registered descriptor records `removedBySource` for direct identifiers. WP6 does
not attempt re-identification and does not emit raw source identifiers in scenarios or
logs. If a future source contains restricted/direct identifiers, it requires a new
descriptor and must not reuse this retention profile.
