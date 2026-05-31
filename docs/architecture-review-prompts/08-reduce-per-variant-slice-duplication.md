# Prompt 08 — Collapse duplicated per-variant Deal/Betting slices behind shared handlers

> Addresses **Top-10 item 8** / Vertical Slice & Code Quality Findings in
> [`docs/ArchitectureReview.md`](../ArchitectureReview.md). ~15 game variants each carry near-identical
> `DealHands`/`ProcessBettingAction` handlers even though variant behaviour already lives behind
> `IGameFlowHandlerFactory`.

---

## Paste this into GitHub Copilot

You are working in the `CardGames` .NET 10 solution (Vertical Slice + MediatR + a `GameFlow` handler
abstraction). Remove duplicated per-variant command slices by routing variants through shared
generic handlers, keeping per-variant slices only where behaviour genuinely diverges. Do this
incrementally and keep the variant-boundary guard tests green.

### Context (verify before editing)

- `src/CardGames.Poker.Api/Features/Games/<Variant>/v1/Commands/` exists for ~15+ variants
  (FiveCardDraw, SevenCardStud, HoldEm, Baseball, GoodBadUgly, PairPressure, FollowTheQueen,
  BobBarker, IrishHoldEm, KingsAndLows, TwosJacksManWithTheAxe, HoldTheBaseball, etc.) and each tends
  to define its own `DealHands` and `ProcessBettingAction` handlers with very similar bodies.
- Variant-specific behaviour is **already centralized** in
  `src/CardGames.Poker.Api/GameFlow/*FlowHandler.cs` (resolved via `IGameFlowHandlerFactory`), and a
  `Generic` slice exists at `Features/Games/Generic/v1/Commands/` (e.g. `PerformShowdown`).
- Cross-layer onboarding is documented in `docs/GameVariantBoundary.md` and enforced by
  `Tests/CardGames.Poker.Tests/Web/GameVariantBoundaryTests.cs`. Special-action exemptions already
  exist (KINGSANDLOWS, SCREWYOURNEIGHBOR, INBETWEEN). The metadata source of truth is
  `Games/PokerGameMetadataRegistry.cs`.

### Goal

1. Identify the per-variant command handlers that are **structurally identical** except for calling a
   variant `IGameFlowHandler` — start with `ProcessBettingAction` and `DealHands`, which are the most
   replicated.
2. Promote a **single generic** command + handler per action (e.g.
   `Features/Games/Generic/v1/Commands/ProcessBettingAction`, `.../DealHands`) that resolves the
   variant via `IGameFlowHandlerFactory` and delegates the divergent steps.
3. Repoint the per-variant endpoints/routes at the generic command (preserve the existing route
   templates and OpenAPI names so the public API and the Refit client in `CardGames.Contracts` /
   `CardGames.Poker.Refitter` do **not** change), then delete the now-redundant per-variant handler
   classes.
4. Keep genuinely divergent variants on their own slices and ensure they remain covered by the
   existing exemptions in `GameVariantBoundaryTests`.

### Constraints

- **Public HTTP contract must not change** — same routes, status codes, request/response shapes.
  Regenerate/verify the Refit client only if endpoints are intentionally unchanged.
- Do this **after** prompt 02 (showdown services) so the generic showdown path is already clean.
- Migrate **one action and one variant at a time**, building and testing between steps. Do not do a
  big-bang rewrite.
- Keep `GameVariantBoundaryTests`, `TableStateAssemblyTests`, and integration game-flow tests green
  at every step.

### Tests / verification

- The existing per-variant integration game-flow tests (`Tests/CardGames.IntegrationTests/GameFlow/*`,
  `Tests/CardGames.IntegrationTests/Games/*`) are the safety net — they must stay green after each
  variant is repointed.
- `GameVariantBoundaryTests` must continue to pass (update the documented exemption list only if a
  variant legitimately changes category).

### Acceptance criteria

- Duplicated `ProcessBettingAction`/`DealHands` handlers are consolidated into generic handlers; only
  genuinely divergent variants retain bespoke handlers.
- Net reduction of several thousand duplicated lines under `Features/Games/**`.
- No change to public routes/contract; Refit client unchanged.
- Build + integration tests green from `src`.
