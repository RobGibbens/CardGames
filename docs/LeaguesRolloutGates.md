# Leagues Rollout Gates (P1 First Slice)

This document defines the first enforceable rollout-gate slice for Leagues P1.

## Source of truth

- Manifest: `.github/ops/leagues-rollout-gates.json`
- CI validator: `.github/scripts/validate_leagues_rollout_gates.py`
- CI workflow gate: `.github/workflows/squad-ci.yml`

## Enforced policy

The CI validator fails builds when any of the following are missing or malformed:

- Required SLO endpoints: `join`, `request`, `approve`
- SLO evaluation window: `24h`
- Required integrity monitors:
  - `duplicate_active_membership`
  - `orphaned_role_state`
  - `unauthorized_mutation_success`
  - `join_failure_reason_spike`
- Required rollout stages in order: `internal` → `beta` → `ga`
- Required stage criteria:
  - `p0_ci_gate_green`
  - `slo_within_budget`
  - `integrity_monitors_green`

## Why this slice

This establishes measurable go/no-go criteria in source control and blocks drift in pull requests before broad rollout.
Leagues endpoints now emit funnel-attempt and endpoint-latency metrics for `join`, `request`, `approve`, and `first_play` journey steps.
