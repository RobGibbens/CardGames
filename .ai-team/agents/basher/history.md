# Basher History

## Project Learnings (from import)
- Test style: xUnit + FluentAssertions under src/Tests.
- Requested by: Rob Gibbens.
- Solution-level verification commands use dotnet build/test in src/CardGames.sln.

## Learnings

- OAuth-first auth page prioritization was consolidated in `.ai-team/decisions.md`; regression checks should ensure external providers remain primary while local forms stay available as fallback.
- Leagues testing should prioritize P0 invariants around temporal membership integrity (no overlapping active intervals), invite token lifecycle enforcement (active/revoked/expired), and league-scoped RBAC anti-escalation before broader UI/season coverage.
- Rollout confidence for Leagues depends on explicit production monitors for join failure reasons, unauthorized mutation attempts, and a zero-tolerance integrity alert for duplicate active membership intervals.
- Leagues P0-first release-gating decision and telemetry guardrails were merged into canonical `.ai-team/decisions.md` for test-plan traceability.
- Leagues API quality gates are most reliable when exercised as full endpoint journeys (preview -> join request -> moderation) instead of isolated handler tests, because HTTP status mapping and authz policies are part of the regression surface.
- Moderation semantics are intentionally asymmetric-idempotent: repeated deny returns `204 NoContent`, while cross-state moderation (approve denied / deny approved) returns `400` with deterministic messages.
- CI in this repository required explicit migration from placeholder Node checks to .NET restore/build/test steps to make integration tests enforceable for release gates.
- Session sync: #224 quality-gate decisions were merged to canonical `.ai-team/decisions.md` and cross-linked with #216 governance invariants so P0 regressions remain traceable across API behavior and CI enforcement.
