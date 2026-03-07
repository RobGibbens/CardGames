# PRD: Irish Hold 'Em Enablement
**Repo:** CardGames
**Date:** 2026-03-07
**Status:** Released (Phase 3 — 2026-03-07)

## 1) Goals / Non-Goals

**Goals**
- Ship Irish Hold 'Em using existing Omaha/Texas Hold 'Em architecture patterns, with the unique Irish discard mechanic.
- Enforce Irish Hold 'Em rules: 4 hole cards dealt; after the flop betting round, each player must discard exactly 2 hole cards (keeping 2); from the turn onwards play continues as Hold 'Em (best 5 from 2 hole + 5 community, may use 0-2 hole cards).
- Add a new discard overlay (reusing the existing `DrawPanel` component) that appears after the flop betting round completes, requiring each player to select exactly 2 of their 4 hole cards to discard.
- Make Irish Hold 'Em selectable in Create Table and Dealer's Choice flows.
- Preserve existing game behavior and contracts for all currently shipped variants.

**Non-Goals**
- No Irish Hi/Lo or split-pot variant in this phase.
- No large UI redesign beyond reusing DrawPanel for the post-flop discard.
- No restructuring of the existing generic vs game-specific endpoint architecture.

---

## 2) Game Rules Summary

- **Players:** 2–10
- **Cards:** Standard 52-card deck, no wild cards
- **Hole cards:** 4 per player (face-down, private) — dealt pre-flop
- **Community cards:** 5 total, dealt in stages:
  - **Flop:** 3 cards face-up after the first betting round (pre-flop)
  - **Turn:** 1 card face-up after the discard phase
  - **River:** 1 card face-up after the third betting round (turn)
- **Discard phase (unique to Irish):** After the flop betting round completes, each player must discard exactly 2 of their 4 hole cards, keeping exactly 2.
- **Betting structure:** Blinds (small blind / big blind), no antes
- **Betting rounds:** 4 (Pre-Flop, Flop, Turn, River)
- **Phase progression:**
  1. `CollectingBlinds` — Post small/big blind
  2. `Dealing` — Deal 4 hole cards to each player
  3. `PreFlop` — First betting round (action starts left of big blind)
  4. `Flop` — Deal 3 community cards, second betting round (action starts left of dealer)
  5. `Discarding` — Each player MUST discard exactly 2 of their 4 hole cards (**NEW PHASE**)
  6. `Turn` — Deal 4th community card, third betting round
  7. `River` — Deal 5th community card, fourth betting round
  8. `Showdown` — Determine winner
  9. `Complete` — Hand complete
- **Hand evaluation (post-discard):** Best 5-card hand from any combination of 2 hole cards + 5 community cards (may use 0, 1, or 2 hole cards) — **identical to Texas Hold 'Em evaluation**, NOT Omaha's "must use exactly 2" constraint.
- **Blinds:** Same as Hold 'Em — dealer button rotates clockwise, SB left of dealer, BB next left. Heads-up: dealer is SB.

### Key Distinction from Omaha
In Omaha, players keep all 4 hole cards through showdown and must use exactly 2 + exactly 3 community. In Irish Hold 'Em, players **discard down to 2 hole cards after the flop**, and from that point the hand evaluation is standard Hold 'Em (0–2 hole cards in best 5).

---

## 3) Current-State Audit (Exists vs Missing)

### A. UI Branch Points

| File | Current State | Gap for Irish Hold 'Em |
|---|---|---|
| [CreateTable.razor](src/CardGames.Poker.Web/Components/Pages/CreateTable.razor) | `IsBlindBasedGame()` returns true for `"HOLDEM"` or `"OMAHA"` only (L461–463). Variant cards populated from `AvailableGamesApi`. | Irish Hold 'Em must be added to `IsBlindBasedGame()`. Will auto-appear in variant grid once backend metadata is registered. |
| [DealerChoiceModal.razor](src/CardGames.Poker.Web/Components/Shared/DealerChoiceModal.razor) | `IsBlindBasedGame()` checks `"HOLDEM"` or `"OMAHA"` (L136–138). Controls blind vs ante/min-bet input display. | Must add `"IRISHHOLDEM"` to `IsBlindBasedGame()`. |
| [TablePlay.razor](src/CardGames.Poker.Web/Components/Pages/TablePlay.razor) | `IsHoldEm` / `IsOmaha` properties (L674–675). Start hand branches at L2536–2556. Showdown branches at L2647–2655. Draw panel conditionally shown when `IsDrawingPhase` (L284–298). `UsesCardDealAnimation` includes HoldEm/Omaha. | Need `IsIrishHoldEm` property. Add Irish branches to StartHandAsync, TryLoadShowdownAsync. Irish enters draw phase after flop, so `IsDrawingPhase` + `DrawPanel` will naturally activate. Must add to `UsesCardDealAnimation`. |
| [TableCanvas.razor](src/CardGames.Poker.Web/Components/Shared/TableCanvas.razor) | `IsBlindBasedGame` checks `"HOLDEM"` or `"OMAHA"` (L231–233). Controls SB/BB badge display. | Must add `"IRISHHOLDEM"`. |
| [IGameApiRouter.cs](src/CardGames.Poker.Web/Services/IGameApiRouter.cs) | Constants: `HoldEm`, `Omaha` (L94–95). Betting routes include HoldEm and Omaha (L139–140). Draw routes for HoldEm/Omaha return "not supported" (L150–151, L242–248). | Need new constant `IrishHoldEm = "IRISHHOLDEM"`. Add betting route (reuse HoldEm betting endpoint). Add **real** draw route for Irish discard phase (new endpoint). |
| [DrawPanel.razor](src/CardGames.Poker.Web/Components/Shared/DrawPanel.razor) | Generic discard-selection UI. Shows player's cards, allows toggling selection, submits draw action. Supports `MaxDiscards` parameter. | No changes needed — reuse as-is with `MaxDiscards = 2`. |
| [DashboardHandOddsCalculator.cs](src/CardGames.Poker.Web/Services/DashboardHandOddsCalculator.cs) | Has `"OMAHA"` check for odds calculation (L55, L63). | Add `"IRISHHOLDEM"` branch — pre-discard: Omaha-style odds; post-discard: Hold'Em-style odds. |
| [Program.cs (Web)](src/CardGames.Poker.Web/Program.cs) | Registers `IHoldEmApi` Refit client. No Omaha-specific client (Omaha uses generic). | Irish Hold 'Em can use the generic API client (same as Omaha) OR reuse `IHoldEmApi` for betting actions. Needs draw endpoint wiring. |

### B. Backend / Domain Branch Points

| File | Current State | Irish Hold 'Em Impact |
|---|---|---|
| [PokerGameMetadataRegistry.cs](src/CardGames.Poker.Api/Games/PokerGameMetadataRegistry.cs) | Assembly-scans `[PokerGameMetadata]` attributes. Constants for existing games (L18–26). | Add `public const string IrishHoldEmCode = "IRISHHOLDEM"`. Auto-discovered via attribute on new `IrishHoldEmGame` class. |
| [PokerGameRulesRegistry.cs](src/CardGames.Poker.Api/Games/PokerGameRulesRegistry.cs) | Assembly-scans `IPokerGame.GetGameRules()`. | Auto-discovered from new `IrishHoldEmGame`. |
| [PokerGamePhaseRegistry.cs](src/CardGames.Poker.Api/Games/PokerGamePhaseRegistry.cs) | Validates registered codes, parses global `Phases` enum. | `Discarding` phase name must be resolvable. Existing `DrawPhase` could be reused or a new phase `"Discarding"` added. |
| [GameFlowHandlerFactory.cs](src/CardGames.Poker.Api/GameFlow/GameFlowHandlerFactory.cs) | Reflection auto-registers all `IGameFlowHandler`. | Auto-discovered from new `IrishHoldEmFlowHandler`. |
| [OmahaFlowHandler.cs](src/CardGames.Poker.Api/GameFlow/OmahaFlowHandler.cs) | 4 hole cards, blinds, community-card pattern. Sends betting actions via HoldEm's `ProcessBettingActionCommand`. | **Baseline pattern** for Irish flow handler. Irish adds discard phase orchestration. |
| [HoldEmFlowHandler.cs](src/CardGames.Poker.Api/GameFlow/HoldEmFlowHandler.cs) | 2 hole cards, blinds, community-card pattern. | Reference for post-discard flow behavior. |
| [StartHandCommandHandler.cs](src/CardGames.Poker.Api/Features/Games/Generic/v1/Commands/StartHand/StartHandCommandHandler.cs) | Uses flow factory from `game.GameType?.Code`. Generic orchestration. | Irish can start through generic flow if flow handler is registered. |
| [PerformShowdownCommandHandler.cs](src/CardGames.Poker.Api/Features/Games/Generic/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs) | Branching: `communityHand is OmahaHand` → exact 2+3 constraint; else → Hold'Em-style 0–2 hole (L690–724). | Post-discard, Irish players have 2 hole cards → `HoldemHand` evaluation → Hold'Em path. No special branching needed if hand type is `HoldemHand`. |
| [TableStateBuilder.cs](src/CardGames.Poker.Api/Services/TableStateBuilder.cs) | Dispatches by hole card count: `Count == 2` → `HoldemHand`, `Count == 4` → `OmahaHand` (L390–404). Showdown: `isOmaha` flag drives separate eval path (L874–921). | **Before discard:** player has 4 cards → Omaha eval (correct for pre-flop/flop private hand display). **After discard:** player has 2 cards → Hold'Em eval (correct). The count-based dispatch handles this automatically. Showdown path must exclude Irish from `isOmaha` since post-discard it uses Hold'Em rules. |
| [CreateGameCommandHandler.cs](src/CardGames.Poker.Api/Features/Games/Common/v1/Commands/CreateGame/CreateGameCommandHandler.cs) | Accepts any registered game code. Persists `SmallBlind`/`BigBlind`. | No changes — Irish creation works once metadata is registered and UI sends blind values. |
| [ChooseDealerGameCommandHandler.cs](src/CardGames.Poker.Api/Features/Games/Common/v1/Commands/ChooseDealerGame/ChooseDealerGameCommandHandler.cs) | Validates game code exists; validates/applies blinds when present. | No changes — supports Irish mechanically once UI provides blind inputs. |
| [ContinuousPlayBackgroundService.cs](src/CardGames.Poker.Api/Services/ContinuousPlayBackgroundService.cs) | Phase array includes community-card phases and `DrawPhase` (L136–148). | If Irish discard uses `"Discarding"` as phase name, add it to the phase array. If it reuses `"DrawPhase"`, no change needed. |
| [HandEvaluatorFactory.cs](src/CardGames.Poker/Evaluation/HandEvaluatorFactory.cs) | Assembly-scans `[HandEvaluator]` attributes. | Auto-discovered from new `IrishHoldEmHandEvaluator` (or reuse `HoldemHandEvaluator` with additional attribute). |
| [OmahaHandEvaluator.cs](src/CardGames.Poker/Evaluation/Evaluators/OmahaHandEvaluator.cs) | `[HandEvaluator("OMAHA")]`. Takes first 4 as hole, rest as community. Creates `OmahaHand`. | Do NOT reuse for Irish — post-discard, Irish uses Hold'Em evaluation rules (0–2 hole cards in best 5). |
| [HoldemHandEvaluator.cs](src/CardGames.Poker/Evaluation/Evaluators/HoldemHandEvaluator.cs) | `[HandEvaluator("HOLDEM")]`. 2-hole-card semantics. | Irish post-discard evaluation is identical. Options: (a) Add `[HandEvaluator("IRISHHOLDEM")]` to this class, or (b) create a thin `IrishHoldEmHandEvaluator` that delegates to the same logic. |
| [OmahaGame.cs](src/CardGames.Poker/Games/Omaha/OmahaGame.cs) | Full Omaha game class: 4 hole cards, blinds, community cards, showdown with `OmahaHand`. | **Primary template** for `IrishHoldEmGame`. Clone and add discard phase + Hold'Em-style evaluation post-discard. |
| [OmahaRules.cs](src/CardGames.Poker/Games/Omaha/OmahaRules.cs) | Phases list, card dealing config, betting config. | Template for `IrishHoldEmRules`. Add `Discarding` phase between Flop and Turn. Set `hasDrawPhase: true`. |
| [OmahaGamePlayer.cs](src/CardGames.Poker/Games/Omaha/OmahaGamePlayer.cs) | Simple model: `PokerPlayer` + `List<Card> HoleCards`. `AddHoleCard()`, `ResetHand()`. | Template for `IrishHoldEmGamePlayer`. Add `DiscardCards(List<int> indices)` method. |

### C. Database / Schema

| Aspect | Current State | Gap |
|---|---|---|
| `GameTypes` table | Runtime auto-creation via create/choose handlers. | Irish game type auto-created on first use. |
| `SmallBlind`/`BigBlind` columns | Exist on `Game` entity. | No schema change. |
| `GameCard.IsDiscarded` | Exists as bool column on `GameCard`. | Used to mark discarded hole cards during Irish discard phase. No schema change. |
| `GameCard.Location` enum | Includes `Community`, `PlayerHand`, etc. | No enum change needed. Discarded cards stay in `PlayerHand` with `IsDiscarded = true`. |
| `GameCard.DealtAtPhase` | String field tagging when card was dealt. | No change — hole cards tagged at deal phase. |
| Migration | N/A | **No migration required.** All necessary columns/relationships already exist. |

### D. Testing / Regression Points

| File | Current State | Gap |
|---|---|---|
| [OmahaHandTests.cs](src/Tests/CardGames.Poker.Tests/Hands/OmahaHandTests.cs) | Omaha exact-2-hole-card tests. | Keep unchanged. |
| [OmahaGameTests.cs](src/Tests/CardGames.Poker.Tests/Games/OmahaGameTests.cs) | Game-level Omaha unit tests. | Keep unchanged. |
| [HoldEmHandLifecycleTests.cs](src/Tests/CardGames.IntegrationTests/Games/HoldEm/HoldEmHandLifecycleTests.cs) | Hold'Em lifecycle integration baseline. | Must remain green. |
| [ChooseDealerGameCommandTests.cs](src/Tests/CardGames.IntegrationTests/Features/Commands/ChooseDealerGameCommandTests.cs) | Multi-game tests include Hold'Em but not Irish. | Add Irish Dealer's Choice selection + blind assertion cases. |
| [CreateGameCommandHandlerTests.cs](src/Tests/CardGames.IntegrationTests/Features/Commands/CreateGameCommandHandlerTests.cs) | Variant matrix includes Hold'Em, Omaha. | Add Irish create-game cases with blind values. |
| [DealersChoiceContinuousPlayTests.cs](src/Tests/CardGames.IntegrationTests/Services/DealersChoiceContinuousPlayTests.cs) | Dealer's Choice continuous-play coverage. | Add Irish-in-DC continuation scenarios. |
| [IntegrationTestBase.cs](src/Tests/CardGames.IntegrationTests/Infrastructure/IntegrationTestBase.cs) | Seeds game types including HOLDEM, OMAHA (L100). | Add `CreateGameType("IRISHHOLDEM", "Irish Hold 'Em", 2, 10, 4, 0, 5, 9)` seed. |

---

## 4) Detailed Requirements by Layer

### Domain / Gameplay

1. **Create `src/CardGames.Poker/Games/IrishHoldEm/` folder** with:
   - `IrishHoldEmGame.cs` — Clone from `OmahaGame.cs` with these modifications:
     - Game code: `"IRISHHOLDEM"`, name: `"Irish Hold 'Em"`
     - Same 4 hole cards, blinds, community card dealing
     - New `Phases.Discarding` phase inserted between `Flop` and `Turn`
     - `AdvanceToNextPhase()`: after `Flop` betting completes → transition to `Discarding` (not `Turn`)
     - New methods: `DiscardCards(string playerName, List<int> discardIndices)` — validates exactly 2 discards, removes cards from player's hand
     - `IsDiscardingComplete()` — checks all active players have discarded
     - After all players discard → auto-advance to `Turn`
     - Showdown uses `HoldemHand` (not `OmahaHand`) since players have 2 hole cards post-discard
   - `IrishHoldEmGamePlayer.cs` — Clone from `OmahaGamePlayer.cs`, add:
     - `bool HasDiscarded { get; private set; }`
     - `void DiscardCards(List<int> indices)` — removes 2 cards from `HoleCards`, sets `HasDiscarded = true`
   - `IrishHoldEmRules.cs` — Clone from `OmahaRules.cs` with:
     - Game code: `"IRISHHOLDEM"`
     - Phases include `Discarding` between `Flop` and `Turn` with `Category = "Drawing"`, `RequiresPlayerAction = true`
     - `Drawing` config: `MaxDiscards = 2`, `MinDiscards = 2`, `RequireExactDiscard = true`
     - `Showdown.HandRanking = "HoldEm"` (not "Omaha")
   - `IrishHoldEmShowdownResult.cs` — Can reuse or clone `OmahaShowdownResult.cs`

2. **`[PokerGameMetadata]` attribute** on `IrishHoldEmGame`:
   ```
   code: "IRISHHOLDEM"
   name: "Irish Hold 'Em"
   description: "Deal 4 hole cards, discard 2 after the flop, then play like Hold 'Em with community cards."
   minimumNumberOfPlayers: 2
   maximumNumberOfPlayers: 10
   initialHoleCards: 4
   initialBoardCards: 0
   maxCommunityCards: 5
   maxPlayerCards: 4
   hasDrawPhase: true
   maxDiscards: 2
   wildCardRule: WildCardRule.None
   bettingStructure: BettingStructure.Blinds
   imageName: "irishholdem.png"
   ```

3. **`Phases` enum** — Add `Discarding` value if not already present. If a new global phase is undesirable, use the string `"Discarding"` directly (as Hold 'Em/Omaha community phases already use literal strings like `"PreFlop"`, `"Flop"`, etc.).

4. **Hand evaluator** — Create `IrishHoldEmHandEvaluator.cs` in `Evaluation/Evaluators/`:
   - Decorate with `[HandEvaluator("IRISHHOLDEM")]`
   - Post-discard, players have 2 hole cards → create `HoldemHand(holeCards, communityCards)`
   - Pre-discard (edge case: all-in before flop/discard), players have 4 hole cards → use Omaha-style evaluation OR force discard before evaluation. **Decision: if all players are all-in before the discard phase, run the community cards out and require discards before showdown.**

### API / Contracts

5. **Create `IrishHoldEmFlowHandler.cs`** in `GameFlow/`:
   - `GameTypeCode => "IRISHHOLDEM"`
   - `GetDealingConfiguration()` → `PatternType.CommunityCard`, `InitialCardsPerPlayer = 4`, `AllFaceDown = true`
   - `SkipsAnteCollection => true` (uses blinds)
   - `GetInitialPhase()` → `"CollectingBlinds"`
   - `DealCardsAsync()` → collect blinds + deal 4 hole cards (same as Omaha)
   - `SendBettingActionAsync()` → reuse HoldEm's `ProcessBettingActionCommand` (same as Omaha)
   - **New**: Override or extend phase transition logic so that after `Flop` betting → phase becomes `Discarding`

6. **Create `ProcessDiscardCommand` / `ProcessDiscardCommandHandler`** in `Features/Games/IrishHoldEm/v1/Commands/ProcessDiscard/`:
   - Input: `GameId`, `PlayerId` (implicit from auth), `DiscardIndices` (list of 2 card indices)
   - Validation: exactly 2 indices, valid range (0–3), player is in hand, game is in `Discarding` phase
   - Logic:
     - Mark selected `GameCard` entities as `IsDiscarded = true`
     - Check if all active players have discarded
     - If yes → advance phase to `Turn`, deal 4th community card, start new betting round
     - Broadcast updated state via SignalR
   - **Alternative**: Reuse the existing `ProcessDrawCommandHandler` pattern from Five Card Draw, which already handles discard-and-redraw. For Irish, the "redraw" count is 0 — just mark cards as discarded, no replacement cards dealt. Investigate whether the generic draw handler can be parameterized for "discard only, no replacement."

7. **API endpoint** — Either:
   - (a) Create a dedicated `POST /api/v1/games/irishholdem/{gameId}/discard` endpoint, or
   - (b) Reuse the generic draw endpoint `POST /api/v1/games/generic/{gameId}/draw` with the flow handler determining no replacement cards are dealt.
   - **Recommendation**: Option (b) — reuse generic draw endpoint. The `ProcessDrawCommandHandler` already supports per-game-type flow routing. Configure Irish flow to handle draws as "discard only." This minimizes new endpoint creation and leverages existing infrastructure.

8. **Contracts** — If using the generic draw endpoint, no new contract types needed. If creating a dedicated discard endpoint, add:
   - `ProcessDiscardRequest` DTO with `DiscardIndices`
   - Response DTO (or reuse `ProcessDrawSuccessful`)

9. **PokerGameMetadataRegistry.cs** — Add constant:
   ```csharp
   public const string IrishHoldEmCode = "IRISHHOLDEM";
   ```

10. **ContinuousPlayBackgroundService.cs** — Add `"Discarding"` to the `inProgressPhases` array if using that phase name. If reusing `"DrawPhase"`, no change needed (already present).

11. **TableStateBuilder.cs** — The existing count-based dispatch (`Count == 4` → Omaha, `Count == 2` → HoldEm) handles Irish naturally:
    - Before discard: 4 hole cards → Omaha evaluation for private hand description (shows "best hand if you keep 2")
    - After discard: 2 hole cards → HoldEm evaluation
    - **Enhancement**: Add explicit `IsIrishHoldEmGame()` check for the showdown path to ensure it does NOT follow the Omaha `isOmaha` path. Post-discard Irish uses HoldEm evaluation.

### Web / UI

12. **`CreateTable.razor`** — Update `IsBlindBasedGame()`:
    ```csharp
    private static bool IsBlindBasedGame(string gameCode)
    {
        return gameCode is "HOLDEM" or "OMAHA" or "IRISHHOLDEM";
    }
    ```

13. **`DealerChoiceModal.razor`** — Update `IsBlindBasedGame()`:
    ```csharp
    private static bool IsBlindBasedGame(string? code) =>
        string.Equals(code, "HOLDEM", StringComparison.OrdinalIgnoreCase)
        || string.Equals(code, "OMAHA", StringComparison.OrdinalIgnoreCase)
        || string.Equals(code, "IRISHHOLDEM", StringComparison.OrdinalIgnoreCase);
    ```

14. **`TableCanvas.razor`** — Update `IsBlindBasedGame`:
    ```csharp
    private bool IsBlindBasedGame =>
        string.Equals(GameTypeCode, "HOLDEM", StringComparison.OrdinalIgnoreCase)
        || string.Equals(GameTypeCode, "OMAHA", StringComparison.OrdinalIgnoreCase)
        || string.Equals(GameTypeCode, "IRISHHOLDEM", StringComparison.OrdinalIgnoreCase);
    ```

15. **`TablePlay.razor`** — Add:
    - New property: `private bool IsIrishHoldEm => string.Equals(_gameTypeCode, "IRISHHOLDEM", StringComparison.OrdinalIgnoreCase);`
    - Add `|| IsIrishHoldEm` to `UsesCardDealAnimation`
    - Add `IsHoldEm="@(IsHoldEm || IsOmaha || IsIrishHoldEm)"` where `IsHoldEm` is passed to `GameInfoOverlay` (or rename parameter to `IsCommunityCardBlindGame`)
    - `StartHandAsync()` — add Irish branch using generic start (same as Omaha):
      ```csharp
      else if (IsIrishHoldEm)
      {
          var startResponse = await GamesApiClient.GenericStartHandAsync(GameId);
          // ... error handling
      }
      ```
    - `TryLoadShowdownAsync()` — add Irish branch using generic showdown (same as Omaha):
      ```csharp
      else if (IsIrishHoldEm)
      {
          showdownResponse = await GamesApiClient.GenericPerformShowdownAsync(GameId);
      }
      ```
    - The `DrawPanel` display is already gated on `IsDrawingPhase` (which checks `CurrentPhaseCategory == "Drawing"`). When the Irish game enters `Discarding` phase with `Category = "Drawing"`, the panel will automatically appear. Ensure `MaxDiscards` returns 2 for Irish:
      ```csharp
      private int GetMaxDiscards()
      {
          // ... existing logic ...
          if (IsIrishHoldEm) return 2;
          // ...
      }
      ```
    - **Enforce exactly 2 discards**: The `DrawPanel` currently allows 0..MaxDiscards selections. For Irish, the player MUST discard exactly 2 (not 0 or 1). Add `MinDiscards` parameter to `DrawPanel` or validate server-side and show error in UI. **Recommendation**: Server-side validation rejects != 2 discards; UI shows a message "You must discard exactly 2 cards" and disables the confirm button until exactly 2 are selected.

16. **`IGameApiRouter.cs`** — Add:
    - Constant: `private const string IrishHoldEm = "IRISHHOLDEM";`
    - Betting routes: `[IrishHoldEm] = RouteIrishHoldEmBettingActionAsync` — delegate to `_holdEmApi.HoldEmProcessBettingActionAsync` (same as Omaha)
    - Draw routes: `[IrishHoldEm] = RouteIrishHoldEmDrawAsync` — delegate to a **real** draw/discard endpoint (unlike the "not supported" entries for HoldEm/Omaha)
    - New private method:
      ```csharp
      private async Task<RouterResponse<ProcessDrawResult>> RouteIrishHoldEmDrawAsync(
          Guid gameId, Guid playerId, List<int> discardIndices)
      {
          // Route to generic draw endpoint or dedicated Irish discard endpoint
      }
      ```

17. **`DashboardHandOddsCalculator.cs`** — Add `"IRISHHOLDEM"` branch:
    - Before discard: calculate Omaha-style odds (4 hole cards)
    - After discard: calculate Hold'Em-style odds (2 hole cards)

18. **Game image** — Add `irishholdem.png` to `src/CardGames.Poker.Web/wwwroot/images/games/`.

### Database / Seeding

19. **No migration required.** All columns (`SmallBlind`, `BigBlind`, `DealerPosition`, `GameCard.IsDiscarded`, `CardLocation.Community`) already exist.

20. **Test seed data** — Add Irish Hold 'Em to `IntegrationTestBase.cs`:
    ```csharp
    CreateGameType("IRISHHOLDEM", "Irish Hold 'Em", 2, 10, 4, 0, 5, 9)
    ```

---

## 5) Game-Type Branch Audit Checklist

### UI Checklist
- [x] [CreateTable.razor](src/CardGames.Poker.Web/Components/Pages/CreateTable.razor): `IsBlindBasedGame` includes `"IRISHHOLDEM"`.
- [x] [DealerChoiceModal.razor](src/CardGames.Poker.Web/Components/Shared/DealerChoiceModal.razor): `IsBlindBasedGame` includes `"IRISHHOLDEM"`.
- [x] [TablePlay.razor](src/CardGames.Poker.Web/Components/Pages/TablePlay.razor): `IsIrishHoldEm` property; start/showdown branches; `UsesCardDealAnimation`; `MaxDiscards` for Irish; `IsHoldEm` overlay parameter includes Irish.
- [x] [TableCanvas.razor](src/CardGames.Poker.Web/Components/Shared/TableCanvas.razor): `IsBlindBasedGame` includes `"IRISHHOLDEM"`.
- [x] [IGameApiRouter.cs](src/CardGames.Poker.Web/Services/IGameApiRouter.cs): `IrishHoldEm` constant; betting route; draw route (real, not "not supported").
- [x] [DrawPanel.razor](src/CardGames.Poker.Web/Components/Shared/DrawPanel.razor): `MinDiscards` parameter added with "exactly N" enforcement for Irish's must-discard-2 rule.
- [x] [DashboardHandOddsCalculator.cs](src/CardGames.Poker.Web/Services/DashboardHandOddsCalculator.cs): Add `"IRISHHOLDEM"` odds calculation.
- [x] Add game image `irishholdem.png`.

### Backend / Domain Checklist
- [x] [IrishHoldEmGame.cs](src/CardGames.Poker/Games/IrishHoldEm/IrishHoldEmGame.cs): Full game class with `Discarding` phase.
- [x] [IrishHoldEmGamePlayer.cs](src/CardGames.Poker/Games/IrishHoldEm/IrishHoldEmGamePlayer.cs): Player model with discard support.
- [x] [IrishHoldEmRules.cs](src/CardGames.Poker/Games/IrishHoldEm/IrishHoldEmRules.cs): Phase descriptors including `Discarding`.
- [x] [IrishHoldEmShowdownResult.cs](src/CardGames.Poker/Games/IrishHoldEm/IrishHoldEmShowdownResult.cs): Showdown result model.
- [x] [IrishHoldEmHandEvaluator.cs](src/CardGames.Poker/Evaluation/Evaluators/IrishHoldEmHandEvaluator.cs): `[HandEvaluator("IRISHHOLDEM")]` using Hold'Em hand logic.
- [x] [IrishHoldEmFlowHandler.cs](src/CardGames.Poker.Api/GameFlow/IrishHoldEmFlowHandler.cs): Flow handler with discard phase orchestration.
- [x] [ProcessDiscard command](src/CardGames.Poker.Api/Features/Games/IrishHoldEm/v1/Commands/): Dedicated discard endpoint for 2-of-4 hole cards.
- [x] [PokerGameMetadataRegistry.cs](src/CardGames.Poker.Api/Games/PokerGameMetadataRegistry.cs): `IrishHoldEmCode` constant.
- [x] [HandEvaluatorFactory.cs](src/CardGames.Poker/Evaluation/HandEvaluatorFactory.cs): Auto-discovered via `[HandEvaluator("IRISHHOLDEM")]`.
- [x] [TableStateBuilder.cs](src/CardGames.Poker.Api/Services/TableStateBuilder.cs): Irish excluded from Omaha path; dedicated Irish showdown block added.
- [x] [PerformShowdownCommandHandler.cs](src/CardGames.Poker.Api/Features/Games/Generic/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs): `UsesSharedCommunityCards` includes Irish; post-discard 2 hole cards → `HoldemHand`.
- [x] [ContinuousPlayBackgroundService.cs](src/CardGames.Poker.Api/Services/ContinuousPlayBackgroundService.cs): `DrawPhase` already in `inProgressPhases` (reused for Irish discard).
- [x] [BaseGameFlowHandler.cs](src/CardGames.Poker.Api/GameFlow/BaseGameFlowHandler.cs): Blind collection works for Irish via `SkipsAnteCollection = true`.

### Testing Checklist
- [x] New: `IrishHoldEmGameTests.cs` — Unit tests for game flow, discard mechanics, phase progression.
- [x] New: `IrishHoldEmHandEvaluatorTests.cs` — Evaluator produces correct hands (includes pre-discard/post-discard evaluation).
- [x] New: `IrishHoldEmHandEvaluatorTests.cs` — Evaluator produces correct hands.
- [x] Update: [IntegrationTestBase.cs](src/Tests/CardGames.IntegrationTests/Infrastructure/IntegrationTestBase.cs) — seed `"IRISHHOLDEM"` game type.
- [x] Update: [ChooseDealerGameCommandTests.cs](src/Tests/CardGames.IntegrationTests/Features/Commands/ChooseDealerGameCommandTests.cs) — add Irish DC selection cases.
- [x] Update: [CreateGameCommandHandlerTests.cs](src/Tests/CardGames.IntegrationTests/Features/Commands/CreateGameCommandHandlerTests.cs) — add Irish creation cases with blinds.
- [x] New: `IrishHoldEmHandLifecycleTests.cs` — Full lifecycle: create → start → preflop bet → flop → discard → turn bet → river bet → showdown.
- [x] Regression: All existing Hold'Em, Omaha, and Five Card Draw tests pass unchanged (617 unit + 525 integration).

---

## 6) Backward Compatibility Strategy

- All existing game codes, endpoints, and contracts remain unchanged.
- Irish Hold 'Em is additive — new game class, flow handler, evaluator, and UI branches only.
- `IsBlindBasedGame` checks are expanded (not rewritten) to include `"IRISHHOLDEM"`.
- Omaha and Hold'Em semantics are untouched. No changes to their game classes, evaluators, or flow handlers.
- The existing `DrawPanel` component handles the discard UI without modification (or minimal `MinDiscards` addition).
- The generic start/showdown endpoints are reused (no new API surface for basic lifecycle).
- Fallback behavior for unknown game codes remains unchanged.

---

## 7) Acceptance Criteria

1. Irish Hold 'Em appears as selectable in Create Table with blind inputs (small/big blind).
2. Irish Hold 'Em appears in Dealer's Choice and prompts for small/big blind (not ante/min-bet).
3. Starting an Irish hand deals 4 hole cards per player and posts blinds.
4. Pre-flop betting round begins with correct action order (left of big blind).
5. After pre-flop betting, 3 community cards (flop) are dealt.
6. After flop betting round completes, each player sees the discard overlay (DrawPanel) requiring them to select exactly 2 of their 4 hole cards to discard.
7. Players cannot proceed until exactly 2 cards are selected and confirmed.
8. After all players have discarded, the 4th community card (turn) is dealt and a new betting round starts.
9. After turn betting, the 5th community card (river) is dealt and the final betting round starts.
10. Showdown evaluates each player's best 5-card hand from 2 hole + 5 community using standard Hold 'Em ranking (0–2 hole cards may be used).
11. Blind badges (SB/BB) display correctly on the table canvas.
12. Continuous play advances Irish hands without breaking Dealer's Choice rotation.
13. All existing game variants pass their current integration tests unchanged.
14. No database migration is required.
15. Fold-to-win works correctly at any phase (including during discard phase — if all but one player fold during flop betting, skip discard and award pot).

---

## 8) Test Strategy and Rollout Phases

### Test Strategy
- **Unit:** Irish game class: phase progression, discard enforcement (exactly 2), fold-during-discard, showdown with HoldemHand.
- **Unit:** Irish evaluator: correct hand ranking post-discard.
- **Integration:** Create-game, choose-dealer-game, full lifecycle (blinds → preflop → flop → discard → turn → river → showdown).
- **Integration:** Continuous play with Irish in Dealer's Choice rotation.
- **Regression:** Full Hold'Em, Omaha, and Five Card Draw integration suites.
- **UI/Manual:** Create Table selection, Dealer's Choice selection, DrawPanel discard UX (selecting 2 cards), blind badges, correct card display before/after discard.

### Rollout Phases
- **Phase 0 (Build):** Implement domain, flow handler, evaluator, UI branches. Incremental commits.
- **Phase 1 (Validate):** Run targeted Irish + regression suites.
- **Phase 2 (Staged deploy):**
  - [x] Deploy to non-prod.
  - [x] Smoke: Create Table → Irish selectable with blinds.
  - [x] Smoke: Dealer's Choice → Irish prompts blinds.
  - [x] Smoke: Full hand lifecycle including discard overlay.
  - [x] Smoke: Showdown produces correct winner with Hold'Em ranking.
  - [x] Rollback gate: any lifecycle failure → disable.
  - [x] Promotion: all smoke + regression green → prod.
- **Phase 3 (Prod release):** ✅ Released 2026-03-07. Merged to main.

---

## 9) Risks / Open Questions

1. **Discard enforcement timing:** If all remaining players are all-in before the flop, the community cards run out automatically. Should the discard phase still occur? **Recommendation:** Yes — always require the discard after the flop, even if all-in. This matches standard Irish Hold 'Em rules and avoids a complex "skip discard" branch.

2. **DrawPanel `MinDiscards`:** The existing DrawPanel supports `MaxDiscards` but may not enforce a minimum. Irish requires exactly 2 discards (not 0 or 1). Determine whether to add `MinDiscards` to DrawPanel or handle purely via server-side validation with a UI hint.

3. **Pre-discard hand display:** Before the discard, the private hand evaluation shows "best possible Omaha hand" (since player has 4 hole cards). This may confuse players who don't know Omaha rules. Consider a custom description: "Select 2 cards to discard" during the discard phase instead of showing hand strength.

4. **Game image asset:** Need to create/source `irishholdem.png` for the variant card display. Use a shamrock/Irish theme or a modified Hold 'Em image.

5. **Generic draw vs. dedicated discard endpoint:** Reusing the generic draw endpoint is lower effort but may conflate "draw" (replace cards) with "discard" (just remove cards). If the generic handler's "draw 0 replacement cards" path is clean, reuse it. Otherwise, a dedicated discard command is safer.

6. **Metadata max players:** Omaha and Hold 'Em both use max 10. Irish should match. Verify deck math: 10 players × 4 hole cards = 40 + 5 community = 45 < 52 ✓. Post-discard: 10 × 2 + 5 = 25 cards in play. Comfortable.

7. **Action timer during discard:** If action timers are enabled, the discard phase needs a timer per player. Verify the existing DrawPanel action timer infrastructure handles this.
