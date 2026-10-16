# Cost-first cumulative-cap sensitivity — 2026-10-16

The owner explicitly authorized GitHub handoff and additional cost-first C60/C120
runs in this chat on 2026-10-16. This is a new exploratory sensitivity study.
The completed C30/U study, its seal, raw data, analysis and negative results stay
unchanged. Its H7 failure (7 < 10) and closed per-step Stage 2 remain in force.
This study does not compare cumulative and per-step policies or repair forecasting.

## Fixed design before any new job

- Runner: the unchanged published runner-tier3-v3; source commit
  dce84ec4e51b9b7489402d81bb0dda28afbda0a0, DLL SHA-256
  1839a54413b2729cf353db7ef8b2359d32f4698709bba75a96eca492e36c69a9.
- Same cost-first WP4, wrapper v1.2, recovery rule, seed 7, fixtures, drivers,
  measured traffic, simulator, interpreter, harness repository and 34 test cells.
- C60 and C120 differ from C30 only in decision-induced drop_eta_total_ms
  hardLimit = 60000 and 120000. All other configuration values are identical.
- Two smoke jobs on the original calibration cell d20181114-s10-r1-w08,
  then exactly 68 main jobs (34 cells x 2 caps), interleaved by cell.
- Existing CF-C30 and CF-U are comparison anchors from the completed study,
  with the same runner and WP4. No existing run is rerun or overwritten.
- Forecasting remains the original read-only display replay in the analysis;
  no new forecast enters vehicle decisions in these sensitivity runs.

## Execution and stop contract

New output only: C:/RideBoundData/research/tier3-cost-cap-sensitivity-v1.
New harness, logs, receipts and analysis: this directory on C.
AC only; free C >= 30 GiB and E >= 40 GiB; reserve 410 MiB per active job;
at most six workers. The prior 68 main bundles occupy 4739 MiB total, so about
5 GiB of persistent output is a planning estimate, not a guaranteed bound.
Never lower floors, delete data, retry failed/partial/anchor-only jobs or clear
STOP/failure/input-drift receipts. Existing original latches also stop new work.
An OS instance lock prevents duplicate drivers. A started marker is exclusive
and precedes the child process. Per-job logs record the actual child returncode.
Each completed bundle must match all listed lengths/hashes and pass/succeeded.

The successor authenticates the unchanged v7 driver before importing it, reuses
its command builder and frozen-input verification, and changes only the new
command's output, label and commitment-config arguments. It does not patch v7.
An exclusive new manifest binds this plan, new driver/analysis/tests/launcher,
both configs, original freeze/seals/binding, baseline result bytes and baseline
bundle manifests. Full original/new checks run before work and after each phase.
Between full checks, original stat/tree-addition checks plus new-file stat checks
run at admission; they are metadata checks, not proof against transient edits.
New smoke must pass the existing pinned reader's identity, ledger and cap checks
before main admission. Any native failure, malformed/partial output, integrity
failure, accounting mismatch or cap overshoot latches a new durable failure.

## Analysis fixed before new results

Analysis starts only after all 68 main jobs pass and full final checks pass.
The unchanged sealed extract_stage1b.extract_run reader extracts C60/C120, using
explicit new identities/configuration bindings, authenticates every bundle,
checks request identities against C30/U and retains per-rider/per-run evidence.
The new report combines the two new roles with the preserved C30/U TSV/JSON.
Report for every cap: served, late >60s and timely <=60/120/300s against raw
initial plan p0, late >60s against forecast display D0, shared completed riders,
riders revised/repeated, late pickups, cumulative burden p95/max, accounting
mismatches and cap overshoots. Report totals, each cell and each day. Count
served as completed AND alighted, exactly as the original analysis.

No significance test, fitted optimum, adaptive extra cap, user-satisfaction,
realized cost/profit, forecast-integration or generalization claim is planned.
All 34 cells were previously seen: this is sensitivity on the existing panel,
not an unseen confirmation set. Higher caps need not improve realized outcomes
monotonically. Failed runs are reported, never omitted or rescued.
Output files are exclusive. Analysis failure is latched; it is not retried.

## Handoff

Publish code, configs, plans, lightweight summaries, hashes, failures and thesis
source/PDF to GitHub branches without rewriting history. Keep raw transcripts,
vendor trees, environments, downloaded papers and local secrets on this machine.
The handoff inventory records excluded files and required external inputs.
Do not run this exact local launcher on a second machine concurrently. A new
machine must provision and rebind paths/environment in a new execution manifest.
