# Scribe — Omaha Phase 2 Staged Deploy Implementation

**Date:** 2026-03-06
**Requested by:** Rob Gibbens

## Decision
For Omaha Phase 2 staged deploy:
- Add config-gated Omaha availability via `GameAvailability:EnableOmaha`, default `false` in base config and `true` in development.
- Enforce server-authoritative gating in both available-games query and command handlers (`CreateGame` and `ChooseDealerGame`).
- Add Phase 2 docs/checklists for non-prod rollout and validation.

## Rationale
- Supports safe staged rollout with non-prod validation before broader availability.
- Prevents client-side bypass by enforcing availability gates server-side across discovery and mutation paths.
