# Adding a New Game (v2.0) - Repo Playbook


## 1. Quick Orientation

The codebase mixes two patterns:
- Metadata-driven discovery for game registration (domain game class + attributes).
- Explicit wiring in API/web orchestration for behavior differences (betting/draw/street progression/UI routing).

Phil's Mom is implemented as an Irish Hold 'Em-style variant with:
- 4 initial hole cards
- exactly 1 discard before flop
- exactly 1 discard after flop
- then Turn/River/Showdown

## 2. Minimal Diff Path (fastest path to "it runs")

Use this when adding a variant that is mostly a tweak of an existing flow family (for example, Hold 'Em-derivative, Draw-derivative).

1. Domain game type + rules metadata
- Add `src/CardGames.Poker/Games/<NewGame>/<NewGame>Game.cs` with `[PokerGameMetadata(...)]`.
- Add `GetGameRules()` returning `<NewGame>Rules.CreateGameRules()`.
- Add `src/CardGames.Poker/Games/<NewGame>/<NewGame>Rules.cs` with phases, dealing config, betting config, drawing config, showdown config.
- Ensure `code` in metadata and `GameTypeCode` in rules match exactly (all caps convention).

2. API game code constant(s) used by orchestration
- Add a constant in `src/CardGames.Poker.Api/Games/PokerGameMetadataRegistry.cs` (pattern used by Phil's Mom: `PhilsMomCode`).
- Even with reflection discovery, constants are still used in API/web conditional logic.

3. Flow handler for hand setup/auto actions
- Add `src/CardGames.Poker.Api/GameFlow/<NewGame>FlowHandler.cs` implementing `IGameFlowHandler` (typically via `BaseGameFlowHandler`).
- Set `GameTypeCode` and override only what differs (example: blinds collection, initial phase, draw timeout behavior).
- Rely on `GameFlowHandlerFactory` reflection scan for handler discovery.

4. Reuse or extend existing command handlers
- If the variant reuses an existing endpoint family, wire it there instead of creating a new endpoint.
- Phil's Mom reuses Hold 'Em betting and Irish discard endpoints, but required variant checks in:
  - `ProcessBettingActionCommandHandler`
  - `ProcessDiscardCommandHandler`
  - `PerformShowdownCommandHandler`
  - `TableStateBuilder`

5. Web router + game-type conditionals
- Add routing constants and maps in `src/CardGames.Poker.Web/Services/IGameApiRouter.cs`.
- Add game code in UI conditionals where behavior differs:
  - draw/discard constraints
  - Hold 'Em-style display toggles
  - blind-based setup lists in create/edit/dealer-choice/table canvas

6. Static asset
- Add game image in `src/CardGames.Poker.Web/wwwroot/images/games/<image>.png`.

7. Focused integration coverage
- Add a lifecycle integration test near related family tests.
- Phil's Mom adds `src/Tests/CardGames.IntegrationTests/Games/IrishHoldEm/PhilsMomHandLifecycleTests.cs`.

## 3. Full Production-Ready Path

Use this for a complete launch-quality addition.

1. Domain and metadata
- [ ] Add `<NewGame>Game.cs` with complete `PokerGameMetadata` (players, card counts, draw flags, betting structure, image name).
- [ ] Add `<NewGame>Rules.cs` with explicit ordered phases and descriptors.
- [ ] Confirm variant type alignment (`VariantType`) for UI grouping.

2. API flow and street machine
- [ ] Add `<NewGame>FlowHandler.cs` and ensure it handles first phase/dealing semantics.
- [ ] Verify auto-action behavior for timeout-sensitive phases (draw, betting).
- [ ] Update phase transitions in existing handlers where this variant deviates.
- [ ] Ensure betting-round records and street progression remain consistent when extra discard windows are inserted.

3. Command handler validation rules
- [ ] Update discard validation to be variant-aware (required count, allowed index range, duplicate protection, draw-round state).
- [ ] Update betting action normalization if variant semantics differ (for example, check-to-call conversion under pressure).
- [ ] Update showdown inclusion logic so the variant gets correct hand resolution path.

4. State projection and evaluation
- [ ] Update `TableStateBuilder` for interim card-count states if evaluation should still be computed (example: 3 hole cards + community cards in transitional phase).
- [ ] Ensure public/private state remains coherent during nonstandard phase transitions.

5. Web rendering and interaction
- [ ] Add game code helpers in `TablePlay.razor` (`Is<NewGame>` and any shared family predicates).
- [ ] Wire draw panel constraints (`GetDrawPanelMaxDiscards`, required discards, panel title).
- [ ] Include game in animation/display capability flags when needed.
- [ ] Add game to blind-based lists where applicable:
  - `CreateTable.razor`
  - `EditTable.razor`
  - `DealerChoiceModal.razor`
  - `TableCanvas.razor`

6. API client routing
- [ ] Add game constant and entries in `GameApiRouter` betting and draw route dictionaries.
- [ ] Reuse existing API endpoints where possible; add dedicated endpoints only when behavior cannot be represented in existing command model.

7. Tests
- [ ] Integration lifecycle tests for full hand progression (deal -> action windows -> showdown).
- [ ] Negative-path tests (invalid discard count/index/state).
- [ ] State-builder regression tests for hand evaluation during transitional phases.
- [ ] Optional unit tests for helper methods introduced in handlers.

8. Documentation and rollout
- [ ] Add or update docs if rules differ from existing family assumptions.
- [ ] Validate image and metadata visibility in table creation and play views.
- [ ] Run full solution build and targeted tests before merge.

## 4. Concrete Phil's Mom Change Map

### Domain / game rules
- Added new game class + metadata:
  - `src/CardGames.Poker/Games/PhilsMom/PhilsMomGame.cs`
- Added new game rules metadata object:
  - `src/CardGames.Poker/Games/PhilsMom/PhilsMomRules.cs`

### API wiring and behavior
- Added game code constant reference:
  - `src/CardGames.Poker.Api/Games/PokerGameMetadataRegistry.cs`
- Added game flow handler:
  - `src/CardGames.Poker.Api/GameFlow/PhilsMomFlowHandler.cs`
- Updated Hold 'Em betting command handler for variant semantics and phase progression:
  - `src/CardGames.Poker.Api/Features/Games/HoldEm/v1/Commands/ProcessBettingAction/ProcessBettingActionCommandHandler.cs`
- Updated discard command handler for 1-card discard and dual discard rounds:
  - `src/CardGames.Poker.Api/Features/Games/IrishHoldEm/v1/Commands/ProcessDiscard/ProcessDiscardCommandHandler.cs`
- Included variant in community-card showdown flow:
  - `src/CardGames.Poker.Api/Features/Games/Generic/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs`
- Updated table state evaluation for transitional (3-hole-card) phase:
  - `src/CardGames.Poker.Api/Services/TableStateBuilder.cs`

### Web/UI and routing
- Added variant-specific state helpers and discard UI constraints:
  - `src/CardGames.Poker.Web/Components/Pages/TablePlay.razor`
- Added router mappings for betting and draw:
  - `src/CardGames.Poker.Web/Services/IGameApiRouter.cs`
- Added blind-based game handling in table setup/edit/selector views:
  - `src/CardGames.Poker.Web/Components/Pages/CreateTable.razor`
  - `src/CardGames.Poker.Web/Components/Pages/EditTable.razor`
  - `src/CardGames.Poker.Web/Components/Shared/DealerChoiceModal.razor`
  - `src/CardGames.Poker.Web/Components/Shared/TableCanvas.razor`
- Added image asset:
  - `src/CardGames.Poker.Web/wwwroot/images/games/philsmom.png`

### Tests
- Added lifecycle integration tests:
  - `src/Tests/CardGames.IntegrationTests/Games/IrishHoldEm/PhilsMomHandLifecycleTests.cs`

## 5. API Wiring and Contracts Guidance

Phil's Mom demonstrates this rule:
- If your new game can be represented by existing contract shapes, prefer reusing existing API contracts/endpoints.

In practice:
- Betting uses existing Hold 'Em process betting endpoint path.
- Draw/discard uses existing Irish discard endpoint shape (`IrishHoldEmDiscardRequest`).
- Web client routing handles game-code to endpoint mapping.

Add new contracts/endpoints only when:
- Existing request/response types cannot represent the variant's actions/state.
- You need distinct API semantics not safely branchable by game code.

If you add new endpoints/contracts:
- Update `CardGames.Contracts` contracts.
- Regenerate Refit clients via `dotnet build src/CardGames.Poker.Refitter`.

## 6. Registration and Discovery Points Checklist

For each new game, verify all discovery points:
- [ ] Domain game class is discoverable by metadata scan (`IPokerGame` + `PokerGameMetadataAttribute`).
- [ ] Flow handler is discoverable by flow-handler factory reflection (`IGameFlowHandler`).
- [ ] Game code constants exist where conditional logic relies on them.
- [ ] Web router dictionaries include game code for all relevant actions.
- [ ] UI family predicates include game when family behavior applies.

## 7. Common Pitfalls Observed (from Phil's Mom)

1. "Auto-discovery means no other wiring" assumption is false.
- Metadata/handler reflection helps registration, but behavior still depends on explicit game-code conditionals across API and UI.

2. Discard logic tied to one variant leaks quickly.
- Irish-specific assumptions (fixed discard count, round progression) required refactoring to variant-aware logic.

3. Transitional hand-evaluation gaps.
- Without `TableStateBuilder` updates, mid-hand phases with nonstandard hole-card counts can produce incorrect/empty evaluation descriptions.

4. UI parity misses in setup views.
- If blind-based game lists are not updated in all setup/edit/modals/canvas checks, the game behaves inconsistently across pages.

5. Router mapping omissions.
- Missing `GameApiRouter` entries can silently route to wrong default behavior.

6. Showdown path omissions for community-card variants.
- Generic showdown inclusion lists must be updated for the new game code.

## 8. Build, Run, and Verification Commands

Use these in sequence from repo root:

```bash
dotnet build src/CardGames.sln
```

Focused integration tests for Phil's Mom lifecycle:

```bash
dotnet test src/Tests/CardGames.IntegrationTests/CardGames.IntegrationTests.csproj --filter "FullyQualifiedName~PhilsMomHandLifecycleTests"
```

Run API and Web for manual validation:

```bash
dotnet run --project src/CardGames.Poker.Api
dotnet run --project src/CardGames.Poker.Web
```

Optional contract regeneration (only if contracts were changed):

```bash
dotnet build src/CardGames.Poker.Refitter
```

## 9. Practical Pre-PR Checklist

- [ ] New game appears in create/edit experiences with correct betting mode options.
- [ ] Hand starts and progresses through all intended phases.
- [ ] Draw/discard constraints are enforced correctly in both API and UI.
- [ ] Showdown works for winner and non-winner clients.
- [ ] Integration tests cover lifecycle and at least one invalid-action path.
- [ ] Full solution build is clean.

## 10. Notes on Scope for This Document

This playbook is intentionally derived from currently uncommitted Phil's Mom work and existing repository docs.
It should be updated when future game additions introduce new endpoint families or orchestration modes.

## 11. Crazy Pineapple Mapping (CRAZYPINEAPPLE)

Crazy Pineapple follows the same endpoint family reuse pattern as Phil's Mom and Irish Hold 'Em:

- Domain metadata/rules:
  - `src/CardGames.Poker/Games/CrazyPineapple/CrazyPineappleGame.cs`
  - `src/CardGames.Poker/Games/CrazyPineapple/CrazyPineappleRules.cs`
- API flow + behavior:
  - `src/CardGames.Poker.Api/GameFlow/CrazyPineappleFlowHandler.cs`
  - Hold 'Em betting handler branches for `CRAZYPINEAPPLE` pre-flop -> flop (deal) -> discard transition.
  - Irish discard handler reused for mandatory 1-card discard after flop and before flop betting.
- Web routing/UI reuse:
  - `IGameApiRouter` maps `CRAZYPINEAPPLE` betting to Hold 'Em endpoint and discard to Irish discard endpoint.
  - Hold'em-family predicates in setup/play pages include `CRAZYPINEAPPLE` for blind fields and discard constraints.

Reusable pattern confirmed:
- If a variant differs only by phase order/card counts but still uses existing request shapes,
  reuse endpoint families and branch by game code in handlers.
- Add a dedicated flow handler for dealing/auto-action semantics and keep contracts unchanged.

## 12. Red River Mapping (REDRIVER)

Red River follows the Hold 'Em endpoint family and keeps the same betting-round model.
It introduces one conditional board behavior at the end of the River round:

- If the river card suit is hearts or diamonds, deal one additional community card.
- Do not create an extra betting round.
- Proceed directly to Showdown after the conditional deal.

Implementation touchpoints:

- Domain metadata/rules:
  - `src/CardGames.Poker/Games/RedRiver/RedRiverGame.cs`
  - `src/CardGames.Poker/Games/RedRiver/RedRiverRules.cs`
- API flow + behavior:
  - `src/CardGames.Poker.Api/GameFlow/RedRiverFlowHandler.cs`
  - `src/CardGames.Poker.Api/Features/Games/HoldEm/v1/Commands/StartHand/StartHandCommandHandler.cs`
    - Resolve flow handler from `game.GameType.Code` (do not hardcode `HOLDEM`).
    - This prevents Hold 'Em-family variants from being started with the wrong flow semantics.
  - `src/CardGames.Poker.Api/Features/Games/HoldEm/v1/Commands/ProcessBettingAction/ProcessBettingActionCommandHandler.cs`
    - Add conditional post-river community-card deal logic.
    - Ensure all-in runout path also applies the same conditional logic.
  - `src/CardGames.Poker.Api/Features/Games/Generic/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs`
    - Include `REDRIVER` in shared-community showdown path.
  - `src/CardGames.Poker.Api/Services/TableStateBuilder.cs`
    - Treat `REDRIVER` as Hold 'Em-family for private/public state evaluation.
- Web routing/UI reuse:
  - `src/CardGames.Poker.Web/Components/Pages/TablePlay.razor`
    - Include `REDRIVER` in the Hold 'Em-family start branch that calls `HoldEmStartHandAsync`.
    - If omitted, the UI can fall through to the default Five Card Draw start/deal path.
  - Add `REDRIVER` to `IGameApiRouter` Hold 'Em betting route map.
  - Include `REDRIVER` in blind-based and Hold 'Em-family predicates in:
    - `CreateTable.razor`
    - `EditTable.razor`
    - `DealerChoiceModal.razor`
    - `TableCanvas.razor`
    - `TablePlay.razor`

Reusable pattern confirmed:
- Variants can inject conditional board-dealing behavior inside the betting handler's
  phase-completion path while reusing existing Hold 'Em contracts/endpoints.

## 13. South Dakota Mapping (SOUTHDAKOTA)

South Dakota follows the Nebraska-style hand construction (exactly 3 hole + 2 community)
while changing street structure:

- 5 hole cards per player
- PreFlop betting
- Flop deals 2 community cards (not 3)
- Flop betting
- Turn deals 1 community card
- Turn betting
- Showdown (no River)

Implementation touchpoints:

- Domain metadata/rules:
  - `src/CardGames.Poker/Games/SouthDakota/SouthDakotaGame.cs`
  - `src/CardGames.Poker/Games/SouthDakota/SouthDakotaRules.cs`
- API flow + behavior:
  - `src/CardGames.Poker.Api/GameFlow/SouthDakotaFlowHandler.cs`
  - `src/CardGames.Poker.Api/Features/Games/HoldEm/v1/Commands/ProcessBettingAction/ProcessBettingActionCommandHandler.cs`
    - South Dakota-specific street progression (Turn -> Showdown)
    - South Dakota-specific community dealing counts/order (Flop=2, Turn=1, no River)
  - `src/CardGames.Poker.Api/Features/Games/Generic/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs`
    - Include SOUTHDAKOTA in shared-community showdown routing
  - `src/CardGames.Poker.Api/Services/TableStateBuilder.cs`
    - Treat SOUTHDAKOTA as Nebraska-style for evaluation/projection
- Evaluation and odds:
  - `src/CardGames.Poker/Evaluation/Evaluators/NebraskaHandEvaluator.cs`
    - Also registered for `SOUTHDAKOTA`
  - `src/CardGames.Poker/Evaluation/OddsCalculator.cs`
    - `CalculateSouthDakotaOdds` runs to 4 community cards
  - `src/CardGames.Poker.Web/Services/DashboardHandOddsCalculator.cs`
    - SOUTHDAKOTA route uses South Dakota odds calculator
- Web routing/UI reuse:
  - Add SOUTHDAKOTA to Hold'em-family mappings in:
    - `src/CardGames.Poker.Web/Services/IGameApiRouter.cs`
    - `src/CardGames.Poker.Web/Components/Pages/TablePlay.razor`
    - `src/CardGames.Poker.Web/Components/Pages/CreateTable.razor`
    - `src/CardGames.Poker.Web/Components/Pages/EditTable.razor`
    - `src/CardGames.Poker.Web/Components/Shared/DealerChoiceModal.razor`
    - `src/CardGames.Poker.Web/Components/Shared/TableCanvas.razor`
  - Add image asset:
    - `src/CardGames.Poker.Web/wwwroot/images/games/southdakota.png`

Reusable pattern confirmed:
- If a variant keeps existing action contracts and only changes street progression/card counts,
  keep endpoint family reuse and branch by game code in handler progression/dealing logic.

## 14. Pair Pressure Mapping (PAIRPRESSURE)

Pair Pressure is a stud-family variant that keeps Seven Card Stud's lifecycle intact:

- CollectingAntes -> ThirdStreet -> FourthStreet -> FifthStreet -> SixthStreet -> SeventhStreet -> Showdown -> Complete
- Wild ranks are derived from duplicate face-up ranks in global deal order.
- Only the two most recent distinct paired ranks stay wild.

Implementation touchpoints:

- Domain metadata and hand logic:
  - `src/CardGames.Poker/Games/PairPressure/PairPressureGame.cs`
  - `src/CardGames.Poker/Games/PairPressure/PairPressureRules.cs`
  - `src/CardGames.Poker/Hands/WildCards/PairPressureWildCardRules.cs`
  - `src/CardGames.Poker/Hands/StudHands/PairPressureHand.cs`
- API flow and evaluation wiring:
  - `src/CardGames.Poker.Api/GameFlow/PairPressureFlowHandler.cs`
    - Reuse Seven Card Stud street/dealing behavior rather than cloning new lifecycle logic.
  - `src/CardGames.Poker.Api/Games/PokerGameMetadataRegistry.cs`
    - Add `PairPressureCode` for shared conditionals.
  - `src/CardGames.Poker.Api/Features/Games/Generic/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs`
    - Route `PAIRPRESSURE` through stud-style showdown and pass ordered face-up cards into Pair Pressure evaluation.
  - `src/CardGames.Poker.Api/Services/TableStateBuilder.cs`
    - Compute dynamic paired wild ranks from ordered face-up cards for public rules, interim evaluation, and showdown overlays.

Recommended targeted coverage:

- Unit tests for the paired-rank queue logic:
  - first paired rank becomes wild
  - third distinct paired rank evicts the oldest tracked rank
  - repeated duplicates do not resurrect an evicted older rank
- Unit tests for Pair Pressure hand behavior:
  - natural hand when no face-up pairs exist
  - hand improvement when one or two tracked ranks are wild
  - regression that an evicted older paired rank is not still treated as wild
- Integration tests:
  - flow-handler resolution returns a stud-style handler instead of fallback
  - street sequence matches Seven Card Stud
  - showdown winner evaluation uses Pair Pressure wild-card rules
  - table-state special rules expose only the two most recent distinct paired ranks

Reusable pattern confirmed:

- For stud variants that reuse Seven Card Stud dealing and betting but have dynamic wilds based on the global face-up sequence,
  keep the Seven Card Stud flow handler shape, add a dedicated wild-rank helper plus stud hand type, and wire ordered face-up-card evaluation into both showdown and table-state projection.

## 15. Tollbooth Mapping (TOLLBOOTH)

Tollbooth is a stud-family variant that introduces a **tollbooth offer** mechanic between betting streets.
Cards 4th through 7th are not dealt from the deck automatically — instead, after each betting street,
a TollboothOffer phase lets each player choose one of three options:

| Choice    | Cost     | Card source                                     |
|-----------|----------|--------------------------------------------------|
| Furthest  | Free     | The community display card with the lowest deal order  |
| Nearest   | 1× ante  | The community display card with the highest deal order |
| Deck      | 2× ante  | Top card from the deck (no display replenishment)      |

Two **display-only community cards** are visible to all players but are excluded from hand evaluation.
When a player takes a display card, it is replenished from the deck.

Phase sequence:
- CollectingAntes → ThirdStreet → TollboothOffer → FourthStreet → TollboothOffer → FifthStreet → TollboothOffer → SixthStreet → TollboothOffer → SeventhStreet → Showdown → Complete

Implementation touchpoints:

- Domain metadata:
  - `src/CardGames.Poker/Games/Tollbooth/TollboothGame.cs` — `[PokerGameMetadata("TOLLBOOTH", ...)]`, VariantType.Stud, 2-7 players
  - `src/CardGames.Poker/Games/Tollbooth/TollboothRules.cs` — phase list, SpecialRules with `TollboothOffer` and `TollboothDisplayCards` keys
- API flow:
  - `src/CardGames.Poker.Api/GameFlow/TollboothFlowHandler.cs` — SpecialPhases includes TollboothOffer; GetNextPhase routes streets→TollboothOffer and resolves TollboothOffer→next street via `TollboothVariantState`
  - `src/CardGames.Poker.Api/Features/Games/Tollbooth/TollboothVariantState.cs` — JSON-serialized in `game.GameSettings`, tracks `PreviousBettingStreet`
- Tollbooth endpoint family (`src/CardGames.Poker.Api/Features/Games/Tollbooth/`):
  - `ChooseCard` command — validates phase/turn, calculates cost, assigns card (Board for 4th-6th, Hole for 7th), replenishes display, charges player, advances round
  - `StartHand`, `CollectAntes`, `DealHands` (full), `ProcessBettingAction`, `PerformShowdown`, `GetCurrentPlayerTurn` (thin delegates to SevenCardStud/Generic)
- Betting progression interception:
  - `SevenCardStud/ProcessBettingActionCommandHandler.cs` — after standard AdvanceToNextPhase, checks `game.GameTypeCode == "TOLLBOOTH"` and redirects 4th-7th streets to TollboothOffer; handles all-in resolution
- State projection:
  - `src/CardGames.Poker.Api/Services/TableStateBuilder.cs` — added to `StudGameCodes`; `BuildTollboothOfferPrivateDto` queries display cards and projects `TollboothOfferPrivateDto`
  - `src/CardGames.Contracts/SignalR/PrivateStateDto.cs` — `TollboothOfferPrivateDto` record (IsMyTurnToChoose, HasChosenThisRound, costs, DisplayCards)
- Web integration:
  - `src/CardGames.Contracts/TollboothApiExtensions.cs` — `ITollboothApi` Refit interface
  - `src/CardGames.Poker.Web/Services/IGameApiRouter.cs` — Tollbooth constant, routing, `TollboothChooseCardAsync`
  - `src/CardGames.Poker.Web/Components/Shared/TollboothOfferOverlay.razor` — 3-button overlay with cost display and timer
  - `src/CardGames.Poker.Web/Components/Pages/TablePlay.razor` — overlay integration, `HandleTollboothChoiceAsync`

Recommended targeted coverage:

- Domain tests: metadata attributes, phase count/order, special rules, betting config
- Flow handler tests: GetNextPhase routing for all street→TollboothOffer and TollboothOffer→street transitions, variant state resolution
- ChooseCard integration tests: cost validation (Furthest free, Nearest 1× ante, Deck 2× ante), display replenishment, phase/turn guards, affordability checks, round completion transitions
- Regression: existing GameApiRouterTests updated with `ITollboothApi` mock

Reusable patterns introduced:

- **Display-only community cards**: Cards stored with `CommunityCard` type and visible through `CommunityCards` query, but excluded from hand evaluation. The `TableStateBuilder` projects them for UI independently from evaluation inputs. This pattern can be reused by any variant that needs visible shared cards that don't participate in hand ranking.
- **Tollbooth offer (special phase with per-player choice)**: A special phase between betting streets where each player makes an individual card-acquisition choice. Uses `CurrentDrawPlayerIndex` and `HasDrawnThisRound` to track turn order. The flow handler's `SpecialPhases` list and `TollboothVariantState` (previous betting street) enable the offer→street→offer cycle. This pattern generalizes to any variant needing per-player decisions between standard betting rounds.