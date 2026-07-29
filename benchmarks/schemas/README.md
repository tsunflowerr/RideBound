# RideBound protocol schemas

Machine-readable protocol assets live under `v1/`. All `$id` values are stable
repository URLs and all `$ref` values are relative, so the same files work from
the repository or from a copied artifact bundle.

- `v1/schema-inventory.json` maps contract types and messages to schema files.
- `v1/compatibility-matrix.json` records executable version behavior.
- `fixtures/` contains valid and invalid examples used by .NET contract tests
  and future cross-language adapters.

Schema v1 is strict: omitted optional fields are allowed only where a schema says
so, `null` is not used, and unknown fields are rejected. A future minor field is
safe only after an explicit compatibility profile names the field and its
ignore/default behavior.
