# Game Variant Boundary

This document defines the **refactoring boundary around game variants**: the explicit,
repeatable model for how a poker variant is supported across every layer of the solution,
and the checklist you must complete to onboard a new variant.

The goal is to make *partial onboarding* impossible to do silently. Today a variant touches
many layers; the risk is adding it to some of them (for example the domain library and the
API) but not others (for example the active web router or its tests). This boundary makes
each responsibility explicit, points at the **active** architecture, and adds a test that
fails when a variant is wired into the domain but not into the active web router.

> TL;DR for a new variant: implement the domain game, expose API slices + contracts, wire the
> variant into the **active** dispatch-table web router (`GameApiRouter`), add table-state
> hooks only if its state differs, add UI hooks only if it needs special interaction, and add
> the tests listed below. The router boundary test will fail until the variant is wired into
> (or explicitly exempted from) the active web router.

## Variant support model (which layer owns what)

| # | Layer | Where it lives | Authoritative source | Mandatory? |
|---|-------|----------------|----------------------|------------|
| 1 | Domain / metadata | `src/CardGames.Poker/Games/{Variant}/` — an `IPokerGame` class decorated with `[PokerGameMetadata(...)]`, returning `GameRules` from `GetGameRules()` | `PokerGameMetadataRegistry` / `PokerGameRulesRegistry` (assembly-scanned) | **Yes** |
| 2 | API slice | `src/CardGames.Poker.Api/Features/Games/{Variant}/` — a `{Variant}ApiMapGroup` (`[EndpointMapGroup]`, auto-discovered by `MapFeatureEndpoints`) plus `v1/Commands/...` handlers + endpoints | The variant's map group + handlers | **Yes** (every variant needs at least create/start/showdown reachable) |
| 3 | Contracts / client surface | `src/CardGames.Contracts/` — Refit interfaces (`RefitInterface.v1.cs`, `I{Variant}Api.cs`) and `*Extensions.cs` partials | Refit interfaces registered via `GamesApiServiceCollectionExtensions` | **Yes** where the web app calls the variant |
| 4 | Active web router | `src/CardGames.Poker.Web/Services/IGameApiRouter.cs` — the dispatch-table `GameApiRouter` | The per-action dispatch dictionaries (see `docs/WebRouterDesign.md`) | **Betting/draw mandatory** for standard variants; the rest optional |
| 5 | Table-state / projection | `src/CardGames.Poker.Api/Services/TableStateBuilder.*.cs` and helpers (`TableVariantClassifier`, etc.) | `TableStateBuilder` partials | Only if the variant's read-model differs |
| 6 | UI behavior | `src/CardGames.Poker.Web/Components/Pages/TablePlay.razor` and overlays | `TablePlay.razor` + `Services/TableActions/*` | Only if the variant needs special client interaction |
| 7 | Tests | `src/Tests/CardGames.Poker.Tests` (unit/web) and `src/Tests/CardGames.IntegrationTests` (API/flow) | The test folders below | **Yes** |

### Mandatory vs optional vs intentionally-shared

* **Mandatory for every variant:** a domain `IPokerGame` with `[PokerGameMetadata]` (layer 1),
  an API slice reachable enough to create/start/settle a hand (layer 2), and tests (layer 7).
* **Mandatory for standard variants in the web router:** a **betting** route and (for draw
  games) a **draw** route. Missing betting/draw routes throw `NotSupportedException` at
  runtime, so they fail loudly.
* **Optional action families** (drop-or-stay, keep-or-trade, buy-card, acknowledge-pot-match):
  only the variants that need them are registered; a missing entry returns a friendly
  `RouterResponse.Failure`, not an exception.
* **Intentionally shared:** Hold 'Em-family variants (Red River, Omaha, Nebraska, South Dakota,
  Bob Barker, Irish Hold 'Em, Phil's Mom, Crazy Pineapple, Hold the Baseball, Klondike) reuse
  the single `RouteHoldEmBettingActionAsync` betting method instead of duplicating one per
  variant.
* **Intentionally not standard-betting (special-action variants):** a few variants drive play
  through their own action families instead of the standard betting table:
  * `KINGSANDLOWS` — no betting; uses drop-or-stay, draw, and acknowledge-pot-match.
  * `SCREWYORNEIGHBOR` (`SCREWYOURNEIGHBOR`) — uses keep-or-trade.
  * `INBETWEEN` — uses its own directly-exposed ace-choice / place-bet router methods.

  These are the **only** variants exempt from a standard betting route. The exemption list is
  encoded and enforced in `GameVariantBoundaryTests` so adding a fourth exemption is a
  deliberate, reviewed change rather than an accidental gap.

## Active architecture vs legacy drift

* The **active** web router is the dispatch-table `GameApiRouter` in
  `src/CardGames.Poker.Web/Services/IGameApiRouter.cs`. This is the one and only router.
* The older wrapper-based router stack (formerly under
  `CardGames.Poker.Web/Services/GameApi/`, e.g. `GameApiRouter` + `IGameApiClient`) has been
  **removed**. Do not reintroduce a wrapper-based router; wire new variants into the dispatch
  tables of the active router only. See `docs/WebRouterDesign.md`.

## Onboarding checklist for a new variant

Complete every applicable item. Items 1, 2, 3, 4, and 8 are mandatory for a standard variant.

1. **Domain/metadata.** Add `src/CardGames.Poker/Games/{Variant}/{Variant}Game.cs` implementing
   `IPokerGame`, decorated with `[PokerGameMetadata(...)]` (set `hasDrawPhase`/`maxDiscards`
   correctly), and return `GameRules` from `GetGameRules()`. No manual registration is needed —
   `PokerGameMetadataRegistry` and `PokerGameRulesRegistry` discover it by reflection.
2. **API slice.** Add `Features/Games/{Variant}/{Variant}ApiMapGroup.cs` (`[EndpointMapGroup]`)
   plus `v1/Commands/...` handlers and endpoints for the actions the variant supports. The map
   group is auto-registered by `MapFeatureEndpoints`.
3. **Contracts/client surface.** Add or extend the Refit interface + DTOs in
   `src/CardGames.Contracts/` and register the client in `GamesApiServiceCollectionExtensions`
   so the web app can call the new endpoints.
4. **Active web router.** In `GameApiRouter`:
   * add a `private const string` game-code constant,
   * add a `Route…Async` method (or reuse `RouteHoldEmBettingActionAsync` for Hold 'Em-family
     variants),
   * register the variant in **every** action dictionary it participates in (at minimum the
     betting dictionary, and the draw dictionary for draw games),
   * **or**, if the variant is a special-action variant with no standard betting, add it to the
     exemption set in `GameVariantBoundaryTests` with a one-line justification.
5. **Table-state / projection.** Only if the variant's read-model differs, add explicit hooks in
   the relevant `TableStateBuilder.*.cs` partial / classifier rather than ad hoc branches.
6. **UI behavior.** Only if the variant needs special client interaction, add a focused hook in
   `TablePlay.razor` / overlays via the existing `Services/TableActions` abstractions.
7. **Available-games discovery.** Confirm the variant appears via the `AvailablePokerGames`
   feature (it should, automatically, once layer 1 is in place).
8. **Tests.** Add/update at least:
   * domain-level rules/behavior tests in `Tests/CardGames.Poker.Tests/Games`,
   * an active-router mapping test in `Tests/CardGames.Poker.Tests/Web/GameApiRouterTests.cs`,
   * API handler / flow tests in `Tests/CardGames.IntegrationTests`,
   * table-state tests if you touched layer 5.

   `GameVariantBoundaryTests` (in `Tests/CardGames.Poker.Tests/Web`) cross-checks the domain
   metadata registry against the active router and will **fail** if you complete layer 1 but
   skip layer 4 — that is the guard against silent partial onboarding.

## What a future engineer can now answer

1. *Add to domain/metadata?* → checklist item 1 (`Games/{Variant}` + `[PokerGameMetadata]`).
2. *Expose through the API?* → checklist item 2 (`{Variant}ApiMapGroup` + `v1/Commands`).
3. *Wire into the active web router?* → checklist item 4 (dispatch dictionaries in `GameApiRouter`).
4. *Does table-state need support?* → only if the read-model differs (layer 5).
5. *Where does UI behavior live?* → `TablePlay.razor` + `Services/TableActions` (layer 6).
6. *What tests are required?* → checklist item 8.
7. *Which router path to ignore?* → the removed wrapper-based router; use `GameApiRouter` only.
