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
- 2026-02-19 (league invite permission validation): create/detail/my-leagues API projections now normalize current-user `Owner` to `Manager`, preserving persisted `Owner` membership while aligning invite-creation UX expectations.
- 2026-02-19 (league invite permission validation): web role gating already treats `Owner` as management-capable, but pending-join action hydration in `Leagues.razor` also must include `Owner` to keep permission behavior coherent across dashboard and detail flows.
- 2026-02-19 (team update): #216 governance authority is finalized so active admins and managers can run admin promote/demote operations; integration gates should retain invariant-conflict coverage for no-manager/no-governance safety states.
- 2026-03-02 (Dealer's Choice test strategy): Produced comprehensive test plan at `docs/DealersChoiceTestStrategy.md`. Key test file locations: `Features/Commands/CreateDealersChoiceGameTests.cs`, `Features/Commands/ChooseDealerGameCommandTests.cs`, `EndToEnd/DealersChoiceGameFlowTests.cs`, `Services/DealersChoiceContinuousPlayTests.cs`, `GameFlow/DealersChoiceDealerRotationTests.cs`. All under `src/Tests/CardGames.IntegrationTests/`.
- 2026-03-02: DC dealer rotation tests must separately track `DcDealerPosition` (insulated from K&L internal dealing) — critical assertion: after K&L concludes, DC dealer goes to `next player after the player who chose K&L`, not whoever dealt the last K&L sub-hand.
- 2026-03-02: `CreateGameCommand.GameCode` is currently a required `string` — DC feature requires making it nullable (or accepting empty string) to signal Dealer's Choice mode. Backward compat regression tests must verify all existing game codes still resolve correctly.
- 2026-03-02: `ContinuousPlayBackgroundService.MoveDealer()` is a static private method — DC changes may need this logic to be aware of DC vs regular dealer rotation. Tests should verify both paths.
- 2026-03-02: `DatabaseSeeder` needs a `CreateDealersChoiceGameSetupAsync` helper for DC-specific test setup with no fixed ante/minBet and IsDealersChoice flag.
- 2026-03-02: New phase `WaitingForDealerChoice` needed between hands in DC mode — ContinuousPlay must NOT auto-start CollectingAntes for DC games; it must pause and wait for dealer's choice.
- 2026-03-06 (community-card odds regression): Added Hold'em odds regression for hole `8c Kh` + flop `7d Kc Jc` in `src/Tests/CardGames.Poker.Tests/Evaluation/OddsCalculatorTests.cs`; invariant is no `HighCard` outcome and Pair+ total probability ≈ 100% when board already pairs hero.
