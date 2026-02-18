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
