# Texas Hold 'Em — Product Requirements Document

**Author:** Squad (Aragorn, Gimli, Arwen)  
**Requested by:** Rob Gibbens  
**Date:** March 5, 2026  
**Status:** Draft — Awaiting Approval  

---

## 1. Executive Summary

Add **Texas Hold 'Em** — the world's most popular poker variant — to the Friday Night Poker platform. Players receive two private hole cards and share five community cards dealt in stages (flop, turn, river) across four betting rounds. The game uses blinds (small blind / big blind) instead of antes.

Significant domain scaffolding already exists (`HoldEmGame.cs`, `HoldEmRules.cs`, `HoldemHand.cs`, `HoldEmFlowHandler.cs`). The database schema is fully ready (Game entity has `SmallBlind`/`BigBlind`/`DealerPosition`; GameCard supports `CardLocation.Community`). The primary work is in the **API orchestration layer** — wiring the domain game logic through the command pipeline, dealing community cards between betting rounds, and adapting the UI for blind-based games.

### Key Constraints

- **Must not break existing games.** All changes must be additive or behind game-type guards.
- **Must work with Dealer's Choice.** Hold 'Em appears as a selectable variant when the dealer picks the game each hand.
- **Must follow the rule-driven architecture** described in `docs/ARCHITECTURE.md`. No game-specific UI conditionals beyond what existing games already use.

---

## 2. Game Rules Summary

*(Full rules at `src/TexasHoldEmRules.md`)*

- **Players:** 2–10  
- **Cards:** Standard 52-card deck, no wild cards  
- **Hole cards:** 2 per player (face-down, private)  
- **Community cards:** 5 total, dealt in stages:
  - **Flop:** 3 cards face-up after the first betting round
  - **Turn:** 1 card face-up after the second betting round  
  - **River:** 1 card face-up after the third betting round
- **Betting structure:** Blinds (small blind / big blind), no antes
- **Betting rounds:** 4 (Pre-Flop, Flop, Turn, River)
- **Hand evaluation:** Best 5-card hand from any combination of 2 hole cards + 5 community cards (may use 0, 1, or 2 hole cards)
- **Blinds:** Dealer button rotates clockwise. Small blind is immediately left of dealer, big blind is next left. Heads-up (2 players): dealer is small blind.
- **Pre-flop action:** Starts left of big blind ("under the gun"). Big blind is a live bet.
- **Post-flop action:** Starts left of dealer (first active player clockwise from button).
- **Showdown:** Last bettor shows first. If no bet on final round, first active player left of button shows first. Best 5-card hand wins. Ties split the pot. All suits are equal.

---

## 3. Current State — What Already Exists

### Domain Layer (Ready)

| Component | File | Status |
|-----------|------|--------|
| Game orchestration | `src/CardGames.Poker/Games/HoldEm/HoldEmGame.cs` (621 lines) | **Complete** — blinds, 4 streets, community dealing, showdown, side pots, dealer rotation |
| Player model | `src/CardGames.Poker/Games/HoldEm/HoldEmGamePlayer.cs` | **Complete** |
| Game rules metadata | `src/CardGames.Poker/Games/HoldEm/HoldEmRules.cs` | **Complete** — 7 phases, community card config, blinds structure |
| Showdown result | `src/CardGames.Poker/Games/HoldEm/HoldEmShowdownResult.cs` | **Complete** |
| Hand evaluator | `src/CardGames.Poker/Hands/CommunityCardHands/HoldemHand.cs` | **Complete** — uses `CommunityCardsHand` base |
| Game metadata attribute | Decorated on `HoldEmGame` class | **Complete** — code `"HOLDEM"`, 2-14 players, blinds, no wilds |
| Domain tests | `src/Tests/CardGames.Poker.Tests/Games/HoldEmGameTests.cs` (463 lines, 20+ tests) | **Complete** |

### Database (Ready — No Migrations Needed)

| Column / Feature | Entity | Status |
|-----------------|--------|--------|
| `SmallBlind` (int?) | `Game` | **Exists** |
| `BigBlind` (int?) | `Game` | **Exists** |
| `DealerPosition` (int) | `Game` | **Exists** |
| `CardLocation.Community` (enum=3) | `GameCard` | **Exists** |
| `DealtAtPhase` (string) | `GameCard` | **Exists** — tags "Flop", "Turn", "River" |
| `IsVisible` (bool) | `GameCard` | **Exists** — face-up community cards |
| `GamePlayerId` nullable | `GameCard` | **Exists** — null for community cards |
| `DealOrder` (int) | `GameCard` | **Exists** — 1-5 for community |

### Registry (Auto-Discovered)

| Registry | Status |
|----------|--------|
| `PokerGameMetadataRegistry` | **Auto-discovers** HOLDEM via attribute scanning |
| `PokerGameRulesRegistry` | **Auto-discovers** HOLDEM via assembly scanning |
| `GameFlowHandlerFactory` | **Auto-discovers** `HoldEmFlowHandler` |
| Constant `PokerGameMetadataRegistry.HoldEmCode = "HOLDEM"` | **Exists** |

### API Layer (Partially Implemented)

| Component | File | Status |
|-----------|------|--------|
| Flow handler | `src/CardGames.Poker.Api/GameFlow/HoldEmFlowHandler.cs` | **Partial** — blind collection + initial deal work; missing community card dealing, phase progression override |
| Phase descriptions | `PhaseDescriptionResolver.cs` | **Works** — PreFlop/Flop/Turn/River descriptions resolve |
| Table state community cards | `TableStateBuilder.cs` L182-197 | **Works** — queries `CardLocation.Community` generically |
| Private hand evaluation | `TableStateBuilder.cs` L386-390 | **Works** — `playerCards.Count == 2` creates `HoldemHand` |
| Leave game phases | `LeaveGameCommandHandler.cs` | **Works** — includes Hold 'Em phase names |
| Table settings (edit) | `UpdateTableSettingsCommandHandler.cs` | **Works** — SmallBlind/BigBlind setting support |

### UI Layer (Partially Ready)

| Component | Status |
|-----------|--------|
| Community card rendering (`TableCanvas.razor`) | **Works** — renders `CommunityCards` ordered by `DealOrder` |
| Action panel (all 6 actions) | **Works** — fold, check, bet, call, raise, all-in |
| Dealer button display | **Works** — gold "D" on dealer seat |
| Game image asset | **Works** — `holdem.png` exists |
| SignalR real-time state | **Works** — `TableStatePublicDto` has `CommunityCards`, `DealerSeatIndex` etc. |
| `GetGameResponse.SmallBlind/BigBlind` | **Works** — API returns blind values |
| Edit table blind fields | **Works** — `EditTable.razor` conditionally shows blind inputs |

---

## 4. What's Missing — Work Items

### P0 — Required for Hold 'Em to Function

#### 4.1. API: Community Card Dealing Between Betting Rounds

**Problem:** No API-layer mechanism exists to deal the flop (3 cards), turn (1 card), or river (1 card) to `CardLocation.Community` during gameplay. The domain `HoldEmGame` has `DealFlop()`/`DealTurn()`/`DealRiver()` in-memory methods but these don't persist to the database.

**Solution:** Extend `HoldEmFlowHandler` (or create a dedicated command handler) to deal community cards as part of phase transitions. When a betting round completes:
1. Advance `CurrentPhase` to the next street (Flop/Turn/River)
2. Create `GameCard` entities with `CardLocation.Community`, `IsVisible = true`, and `DealtAtPhase = "Flop"/"Turn"/"River"`
3. Assign sequential `DealOrder` values (1-3 for flop, 4 for turn, 5 for river)
4. Broadcast updated state via SignalR

**Precedent:** `GoodBadUglyFlowHandler` creates community cards in `DealCardsAsync` with `CardLocation.Community`. The pattern exists — it just needs to happen mid-game for Hold 'Em rather than at deal time.

**Files to create/modify:**
- Create `src/CardGames.Poker.Api/Features/Games/HoldEm/` feature folder
- Create `DealCommunityCardsCommandHandler.cs` (or integrate into flow handler phase transitions)
- Modify `HoldEmFlowHandler.cs` — override `GetNextPhase()` and add community card dealing hooks

**Risk:** Medium — new dealing pattern, but uses existing `GameCard` entity and `CardLocation.Community`

---

#### 4.2. API: Hold 'Em Betting Action Handler

**Problem:** The existing `ProcessBettingAction` handlers (FiveCardDraw, SevenCardStud) have game-specific phase transition logic. FiveCardDraw transitions: `FirstBettingRound → DrawPhase → SecondBettingRound → Showdown`. Hold 'Em needs: `PreFlop → (deal flop) → Flop → (deal turn) → Turn → (deal river) → River → Showdown`. This is fundamentally different — after each betting round, community cards must be dealt before the next round begins.

**Solution:** Create a Hold 'Em-specific `ProcessBettingActionCommandHandler` in `Features/Games/HoldEm/v1/Commands/ProcessBettingAction/` that:
1. Processes the betting action using the existing `BettingRound` infrastructure
2. When a betting round completes:
   - Determines the next street based on `CurrentPhase`
   - Deals the appropriate community cards (flop: 3, turn: 1, river: 1)
   - Creates a new `BettingRound` entity for the next street
   - Updates `CurrentPhase`
   - Handles the "all-in runout" case (deals remaining community cards and goes to showdown)
3. Uses correct first-to-act position (UTG pre-flop, left-of-dealer post-flop)

**Files to create:**
- `src/CardGames.Poker.Api/Features/Games/HoldEm/v1/Commands/ProcessBettingAction/ProcessBettingActionCommand.cs`
- `src/CardGames.Poker.Api/Features/Games/HoldEm/v1/Commands/ProcessBettingAction/ProcessBettingActionCommandHandler.cs`
- `src/CardGames.Poker.Api/Features/Games/HoldEm/v1/Commands/ProcessBettingAction/ProcessBettingActionCommandValidator.cs`

**Risk:** High — core game loop; must handle all-in, side pots, and fold-to-win correctly

---

#### 4.3. API: Hold 'Em Hand Evaluator

**Problem:** No `[HandEvaluator("HOLDEM")]`-attributed evaluator exists. The `HandEvaluatorFactory` falls back to `DrawHandEvaluator` for unknown game codes, which evaluates hands incorrectly for community card games.

**Solution:** Create `HoldemHandEvaluator` implementing `IHandEvaluator` with `[HandEvaluator("HOLDEM")]` attribute that:
1. Loads player hole cards (`CardLocation.Hole`, `GamePlayerId = playerId`)
2. Loads community cards (`CardLocation.Community`, `GamePlayerId = null`)
3. Creates `HoldemHand(holeCards, communityCards)` for each player
4. Ranks hands using the existing `HoldemHand.Strength` comparison

**Files to create:**
- `src/CardGames.Poker.Api/Evaluation/Evaluators/HoldemHandEvaluator.cs`

**Risk:** Low — `HoldemHand` class already handles all hand evaluation logic

---

#### 4.4. API: Showdown with Community Cards

**Problem:** The generic `PerformShowdownCommandHandler` only loads player-owned cards (`GamePlayerId != null`). Community cards (`GamePlayerId = null`) are not loaded or combined with player hands for evaluation.

**Solution:** Either:
- (A) Extend the generic showdown handler to detect community card games and include community cards, OR  
- (B) Create a Hold 'Em-specific showdown handler in `Features/Games/HoldEm/`

Option (B) is recommended because it also cleanly handles the "all-in runout with remaining community cards" scenario and avoids touching the generic handler used by all other games.

**Additionally:** Add a Hold 'Em showdown branch to `TableStateBuilder.cs` in the showdown evaluation section (~L747) alongside the existing GBU, SevenCardStud, etc. branches. Create an `IsHoldEmGame()` helper method (and consider an `IsCommunityCardGame()` helper for shared Omaha use).

**Files to create/modify:**
- Create `src/CardGames.Poker.Api/Features/Games/HoldEm/v1/Commands/PerformShowdown/`
- Modify `src/CardGames.Poker.Api/Services/TableStateBuilder.cs` — add Hold 'Em showdown evaluation branch

**Risk:** Medium — must handle split pots, side pots, and edge cases

---

#### 4.5. HoldEmFlowHandler: Phase Progression Fix

**Problem:** The current `HoldEmFlowHandler` returns `"Dealing"` as the initial phase, but the phases defined in `HoldEmRules.cs` start with `"WaitingToStart"` and jump to `"PreFlop"`. `"Dealing"` is not in the phase list. The base `GetNextPhase()` walks the phases array linearly — it will fail to find `"Dealing"` and return null, breaking phase progression.

**Solution:** Align the phases:
- Option A: Add `"CollectingBlinds"` and `"Dealing"` to the `HoldEmRules.Phases` list
- Option B: Override `GetNextPhase()` in `HoldEmFlowHandler` to handle the Hold 'Em-specific progression:
  ```
  CollectingBlinds → Dealing → PreFlop → Flop → Turn → River → Showdown → Complete
  ```

Option A is cleaner and more consistent with the rule-driven architecture.

**Files to modify:**
- `src/CardGames.Poker/Games/HoldEm/HoldEmRules.cs` — add missing phases
- `src/CardGames.Poker.Api/GameFlow/HoldEmFlowHandler.cs` — override `GetNextPhase()` if needed

**Risk:** Low — alignment fix

---

#### 4.6. UI: CreateTable.razor — Hold 'Em Availability + Blind Fields

**Problem:** 
1. `IsGameAvailable()` has a hardcoded string list that doesn't include "Texas Hold 'Em"
2. The form shows Ante/MinBet fields. Hold 'Em uses Small Blind/Big Blind instead.
3. `CreateGameCommand` lacks `SmallBlind`/`BigBlind` properties.

**Solution:**
1. Add `"Texas Hold 'Em"` to the `IsGameAvailable()` string match
2. Detect when the selected variant uses blinds (check `_selectedVariant.Code == "HOLDEM"` or inspect metadata `BettingStructure`). Show Small Blind/Big Blind inputs instead of Ante/Min Bet.
3. Add `SmallBlind` and `BigBlind` to `CreateGameCommand` — either via the OpenAPI spec (regenerate Refit) or via the existing `CreateGameCommandExtensions.cs` partial record.

**Implementation detail:** The `EditTable.razor` already has conditional blind field rendering that should be mirrored. The detection logic uses `hasBlindFields` based on the `GetTableSettingsResponse`.

**Files to modify:**
- `src/CardGames.Poker.Web/Components/Pages/CreateTable.razor` — add string to availability check, add conditional blind fields
- `src/CardGames.Contracts/CreateGameCommandExtensions.cs` — add SmallBlind/BigBlind properties
- `src/CardGames.Poker.Api/Features/Games/Common/v1/Commands/CreateGame/CreateGameCommandHandler.cs` — read and apply blind values

**Risk:** Low — additive changes, EditTable.razor provides the pattern to follow

---

#### 4.7. UI: TablePlay.razor — Game Type Detection + Routing

**Problem:** No `IsHoldEm` helper exists. The `IGameApiRouter`/`GameApiRouter` needs Hold 'Em routes.

**Solution:**
1. Add `IsHoldEm` property: `private bool IsHoldEm => string.Equals(_gameTypeCode, "HOLDEM", StringComparison.OrdinalIgnoreCase);`
2. Add `IsHoldEm` to `UsesCardDealAnimation`
3. Add HOLDEM routing to `GameApiRouter` for betting action dispatching
4. Show SB/BB in the table info strip instead of Ante/MinBet for Hold 'Em games

**Files to modify:**
- `src/CardGames.Poker.Web/Components/Pages/TablePlay.razor`
- `src/CardGames.Poker.Web/Services/GameApiRouter.cs` (or wherever routing lives)

**Risk:** Low — follows established pattern from other games

---

### P1 — Enhanced Experience

#### 4.8. UI: Community Card Labels + Visual Grouping

**Problem:** Community cards render as a flat row with no visual distinction between flop/turn/river.

**Solution:**
1. Extend `GetCommunityCardLabel()` in `TableCanvas.razor` to return "Flop" (cards 0-2), "Turn" (card 3), "River" (card 4) for Hold 'Em games
2. Add CSS spacing/gaps between the flop group (3 cards) and turn/river:
   ```css
   .community-card-slot:nth-child(4),
   .community-card-slot:nth-child(5) { margin-left: 0.75rem; }
   ```

**Files to modify:**
- `src/CardGames.Poker.Web/Components/Shared/TableCanvas.razor` — `GetCommunityCardLabel()` extension
- `src/CardGames.Poker.Web/wwwroot/app.css` — flop/turn/river visual grouping

**Risk:** Low — CSS-only + minor Razor change

---

#### 4.9. UI: Small Blind / Big Blind Position Indicators

**Problem:** The dealer button shows but there are no SB/BB badges on the relevant seats.

**Solution:**
1. Compute SB/BB seat positions client-side from `DealerSeatIndex` and seat ordering (or add `SmallBlindSeatIndex`/`BigBlindSeatIndex` to `TableStatePublicDto`)
2. Render "SB" and "BB" badges in `TableSeat.razor` similar to the existing dealer button
3. Style with distinct colors (e.g., blue for SB, red for BB) following the dealer button gold pattern

**Files to modify:**
- `src/CardGames.Poker.Web/Components/Shared/TableSeat.razor`
- `src/CardGames.Poker.Web/wwwroot/app.css`
- Optionally: `src/CardGames.Contracts/` DTO and `TableStateBuilder.cs` if computing server-side

**Risk:** Low — visual-only, no game logic impact

---

#### 4.10. UI: Street Progress Indicator

**Problem:** No visual indicator of which street the game is on during active play.

**Solution:** Add a horizontal breadcrumb near the community cards:
```
Pre-Flop → [Flop] → Turn → River → Showdown
```
Highlight the current street. This could be integrated into the community cards area of `TableCanvas.razor`.

**Files to modify:**
- `src/CardGames.Poker.Web/Components/Shared/TableCanvas.razor`
- `src/CardGames.Poker.Web/wwwroot/app.css`

**Risk:** Low — visual-only

---

#### 4.11. UI: Dealer's Choice Modal — Blind Support

**Problem:** When a Dealer's Choice dealer picks Hold 'Em, the modal shows Ante/MinBet fields instead of Small Blind/Big Blind.

**Solution:** Extend `DealerChoiceModal.razor` to detect when the selected variant uses blinds and show SB/BB fields accordingly. Detection can check `selectedVariant.Code == "HOLDEM"` or use a `UsesBlinds` flag from game metadata.

**Files to modify:**
- `src/CardGames.Poker.Web/Components/Shared/DealerChoiceModal.razor`

**Risk:** Low — follows CreateTable.razor pattern

---

#### 4.12. UI: Community Card Deal Animation

**Problem:** Community cards appear instantly. No staged reveal animation exists.

**Solution:** Add fly-in and flip animations for community cards:
- Flop: 3 cards slide in and flip simultaneously
- Turn: 1 card slides in and flips
- River: 1 card slides in and flips

This mirrors the player card deal animation already in the system.

**Files to modify:**
- `src/CardGames.Poker.Web/Components/Shared/TableCanvas.razor`
- `src/CardGames.Poker.Web/wwwroot/app.css`

**Risk:** Low-Medium — animation timing/coordination

---

### P2 — Polish & Infrastructure

#### 4.13. Backend: Consolidate Blind Collection Logic

**Problem:** Blind collection code is copy-pasted between `HoldEmFlowHandler` and `OmahaFlowHandler`.

**Solution:** Extract shared blind collection to `BaseGameFlowHandler` or a utility method. Both flow handlers delegate to the shared implementation.

**Files to modify:**
- `src/CardGames.Poker.Api/GameFlow/BaseGameFlowHandler.cs`
- `src/CardGames.Poker.Api/GameFlow/HoldEmFlowHandler.cs`
- `src/CardGames.Poker.Api/GameFlow/OmahaFlowHandler.cs`

**Risk:** Low — refactoring only, no behavior change

---

#### 4.14. Backend: ContinuousPlay Service Hold 'Em Support

**Problem:** The `ContinuousPlayBackgroundService` auto-advances phases between hands. It needs to correctly handle Hold 'Em's deal-community-cards-then-bet cycle.

**Solution:** Verify the service delegates to flow handler methods for phase advancement. If it does, Hold 'Em support comes for free through the flow handler. If there's hardcoded phase logic, add Hold 'Em phase awareness.

**Files to verify/modify:**
- `src/CardGames.Poker.Api/Services/ContinuousPlayBackgroundService.cs`

**Risk:** Medium — if the service has hardcoded assumptions about phase patterns

---

#### 4.15. Backend: AutoAction Service — Blind-Aware Auto-Fold

**Problem:** The auto-action service (timeout handling) may not correctly handle the blind-posting phase or the pre-flop position calculation.

**Solution:** Verify `AutoActionService` delegates to flow handler's `PerformAutoActionAsync`. The base implementation already handles standard betting auto-actions (check if possible, else fold). May need a Hold 'Em override for the pre-flop UTG starting position.

**Files to verify:**
- `src/CardGames.Poker.Api/Services/AutoActionService.cs`
- `src/CardGames.Poker.Api/GameFlow/HoldEmFlowHandler.cs` — `PerformAutoActionAsync` override

**Risk:** Low-Medium

---

#### 4.16. Tests: API Integration Tests

**Problem:** Domain tests exist (20+ passing), but no API-level integration tests for the Hold 'Em command pipeline.

**Solution:** Create Hold 'Em-specific integration tests covering:
- Game creation with blinds
- Full hand lifecycle (blinds → deal → preflop betting → flop → flop betting → turn → turn betting → river → river betting → showdown)
- Fold-to-win (only one player remains)
- All-in and side pot scenarios
- Heads-up blind positions

**Files to create:**
- `src/Tests/CardGames.IntegrationTests/Games/HoldEm/`

**Risk:** Low — testing only

---

#### 4.17. API: Pot-Size Betting Support

**Problem:** The ActionPanel's "½ Pot" and "Pot" quick-bet buttons use an approximation. Proper pot-sized betting in Hold 'Em requires the actual current pot total.

**Solution:** Ensure `TableStatePublicDto.TotalPot` (or equivalent) is accurate and wire it to the ActionPanel's pot calculation.

**Files to verify/modify:**
- `src/CardGames.Poker.Web/Components/Shared/ActionPanel.razor`
- `src/CardGames.Poker.Api/Services/TableStateBuilder.cs`

**Risk:** Low

---

## 5. Game-Type Decision Points — Full Audit

These are ALL locations in the codebase that make decisions based on game type code. Each must be verified to either (a) correctly handle HOLDEM or (b) not need changes.

| File | Location | What It Checks | Hold 'Em Impact | Action |
|------|----------|---------------|----------------|--------|
| `TableStateBuilder.cs` | L29-46 | `StudGameCodes`, `DrawHandFactories`, `StudVariantEvaluators` | Hold 'Em is neither stud nor draw | **Verify no false match** |
| `TableStateBuilder.cs` | L182-197 | Community cards query | **Already works** | None |
| `TableStateBuilder.cs` | L340-395 | Private hand eval branching | **Already works** (L386-390) | None |
| `TableStateBuilder.cs` | L747-760 | Showdown evaluation per game type | **GAP** — no Hold 'Em branch | **Add Hold 'Em showdown branch** |
| `TableStateBuilder.cs` | L2253-2275 | Helper methods (IsStudGame, IsBaseballGame, etc.) | No Hold 'Em helper | **Add `IsHoldEmGame()` helper** |
| `CreateTable.razor` | L433 | `IsGameAvailable()` hardcoded list | Not listed | **Add "Texas Hold 'Em"** |
| `TablePlay.razor` | Game type helpers | IsXxx properties | No `IsHoldEm` | **Add `IsHoldEm`** |
| `GameApiRouter` | Route mapping | Game code → API client | No HOLDEM route | **Add HOLDEM route** |
| `PhaseDescriptionResolver.cs` | Phase → description | Hold 'Em phase descriptions | **Already works** | None |
| `LeaveGameCommandHandler.cs` | L181 | Valid phases list | **Already includes** Hold 'Em phases | None |
| `ChooseDealerGameCommandHandler.cs` | L220 | Available games for Dealer's Choice | **Auto-discovers** HOLDEM | None |
| `ContinuousPlayBackgroundService.cs` | L236, L454 | Flow handler resolution | **Auto-discovers** via factory | **Verify phase cycle works** |
| `AutoActionService.cs` | L55 | Flow handler resolution | **Auto-discovers** via factory | **Verify blind-aware auto-action** |
| `DealerChoiceModal.razor` | Variant selection | Shows ante/minbet fields | Shows wrong fields for Hold 'Em | **Add blind field detection** |
| `UpdateTableSettingsCommandHandler.cs` | L192-254 | SmallBlind/BigBlind support | **Already works** | None |
| `GetGameMapper.cs` | L20 | Game type → DTO | **Generic — works** | None |

---

## 6. Implementation Order

### Phase 1 — Core Game Loop (P0)

```
Week 1:
├── 4.5  Fix HoldEmFlowHandler phase progression
├── 4.3  Create HoldemHandEvaluator
├── 4.1  Community card dealing between betting rounds
└── 4.2  Hold 'Em betting action handler

Week 2:
├── 4.4  Showdown with community cards  
├── 4.6  CreateTable.razor — availability + blind fields
├── 4.7  TablePlay.razor — game type detection + routing
└──      End-to-end manual testing
```

### Phase 2 — Enhanced Experience (P1)

```
Week 3:
├── 4.8   Community card labels + visual grouping
├── 4.9   SB/BB position indicators
├── 4.10  Street progress indicator
├── 4.11  Dealer's Choice modal blind support
└── 4.12  Community card deal animation
```

### Phase 3 — Polish & Hardening (P2)

```
Week 4:
├── 4.13  Consolidate blind collection logic
├── 4.14  ContinuousPlay service verification
├── 4.15  AutoAction service verification  
├── 4.16  API integration tests
└── 4.17  Pot-size betting support
```

---

## 7. Architecture Decisions

### 7.1. Hold 'Em-specific feature folder vs. generic handlers

**Decision:** Create a `Features/Games/HoldEm/` feature folder with Hold 'Em-specific command handlers for `ProcessBettingAction` and `PerformShowdown`.

**Rationale:** The deal-community-cards-between-betting-rounds pattern is fundamentally different from draw games (draw between rounds) and stud games (deal individual cards per street). A separate handler provides clear ownership and avoids complex branching in generic handlers.

### 7.2. Community card dealing as flow handler callback vs. separate command

**Decision:** Deal community cards as part of the betting action handler's post-round logic (within the same transaction), not as a separate command.

**Rationale:** Community card dealing is atomic with the betting round completion. Separating it into a separate command creates a window where the game is in an inconsistent state (betting complete but no new cards dealt). Keeping it in one transaction ensures consistency.

### 7.3. CreateGameCommand extension for blinds

**Decision:** Add `SmallBlind`/`BigBlind` to `CreateGameCommandExtensions.cs` (partial record) rather than modifying the OpenAPI spec and regenerating Refit.

**Rationale:** The existing `IsDealersChoice` extension property follows this pattern. Regenerating Refit clients has broader blast radius. The extension approach is isolated and non-breaking.

### 7.4. Client-side vs. server-side SB/BB position computation

**Decision:** Compute SB/BB positions client-side from `DealerSeatIndex` and seat ordering.

**Rationale:** The calculation is deterministic (dealer+1 = SB, dealer+2 = BB, with heads-up exception). Adding fields to `TableStatePublicDto` increases the contract surface for all games. Client-side keeps changes isolated to Hold 'Em UI components.

---

## 8. Non-Goals / Out of Scope

- **Limit Hold 'Em** — This PRD covers No Limit Hold 'Em only. Limit and Pot Limit structures can be added later via `GameSettings`.
- **Tournament mode** — Cash game only. Tournament blind level progression is a separate feature.
- **Omaha** — While Omaha shares the same community card gaps, this PRD only covers Hold 'Em. However, the infrastructure built here (community card dealing, hand evaluator pattern) should be designed to support Omaha with minimal additional work.
- **Heads-up display (HUD) / statistics** — No player tracking or stats overlay.
- **Custom blind structures** — Fixed SB/BB per table. No escalating blinds.

---

## 9. Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Breaking existing games with TableStateBuilder changes | Medium | High | Guard all new branches with `IsHoldEmGame()` checks; run full test suite |
| Community card dealing race condition | Low | High | Keep dealing atomic within betting action transaction |
| Pre-flop position logic bugs | Medium | Medium | Comprehensive tests for 2-player (heads-up) and 3+ player scenarios |
| Side pot calculation with community cards | Medium | Medium | Leverages existing `PotManager` which already handles side pots |
| Continuous play service incompatibility | Low | Medium | Service delegates to flow handlers; verify with integration test |
| CreateGameCommand contract change breaking existing clients | Low | Low | Using partial record extension (additive, non-breaking) |

---

## 10. Success Criteria

1. A player can create a Texas Hold 'Em table from CreateTable.razor with small blind and big blind settings
2. Texas Hold 'Em appears as a selectable variant in Dealer's Choice
3. The full hand lifecycle works: blinds → deal 2 hole cards → pre-flop betting → deal 3 flop cards → flop betting → deal turn → turn betting → deal river → river betting → showdown
4. Community cards display with flop/turn/river visual grouping
5. Dealer button, SB, and BB positions display correctly and rotate
6. Heads-up (2 player) blind positions follow correct rules (dealer = SB)
7. All-in with side pots works correctly
8. Fold-to-win (all others fold) awards pot without showdown
9. All existing game types continue to pass their test suites
10. Continuous play mode works for Hold 'Em (auto-advances between hands)

---

## 11. Appendix: File Inventory

### Files to Create

| File | Purpose |
|------|---------|
| `src/CardGames.Poker.Api/Features/Games/HoldEm/v1/Commands/ProcessBettingAction/ProcessBettingActionCommand.cs` | Command record |
| `src/CardGames.Poker.Api/Features/Games/HoldEm/v1/Commands/ProcessBettingAction/ProcessBettingActionCommandHandler.cs` | Betting + community card dealing |
| `src/CardGames.Poker.Api/Features/Games/HoldEm/v1/Commands/ProcessBettingAction/ProcessBettingActionCommandValidator.cs` | Input validation |
| `src/CardGames.Poker.Api/Features/Games/HoldEm/v1/Commands/PerformShowdown/PerformShowdownCommand.cs` | Command record |
| `src/CardGames.Poker.Api/Features/Games/HoldEm/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs` | Showdown with community cards |
| `src/CardGames.Poker.Api/Evaluation/Evaluators/HoldemHandEvaluator.cs` | Hand evaluator factory entry |
| `src/Tests/CardGames.IntegrationTests/Games/HoldEm/HoldEmHandLifecycleTests.cs` | Full lifecycle tests |
| `src/Tests/CardGames.IntegrationTests/Games/HoldEm/HoldEmShowdownTests.cs` | Showdown-specific tests |

### Files to Modify

| File | Change |
|------|--------|
| `src/CardGames.Poker/Games/HoldEm/HoldEmRules.cs` | Align phase list with flow handler expectations |
| `src/CardGames.Poker.Api/GameFlow/HoldEmFlowHandler.cs` | Community card dealing, phase progression, GetNextPhase override |
| `src/CardGames.Poker.Api/Services/TableStateBuilder.cs` | Add Hold 'Em showdown branch, `IsHoldEmGame()` helper |
| `src/CardGames.Contracts/CreateGameCommandExtensions.cs` | Add SmallBlind/BigBlind properties |
| `src/CardGames.Poker.Api/Features/Games/Common/v1/Commands/CreateGame/CreateGameCommandHandler.cs` | Read and apply blind values |
| `src/CardGames.Poker.Web/Components/Pages/CreateTable.razor` | Add availability, blind form fields |
| `src/CardGames.Poker.Web/Components/Pages/TablePlay.razor` | Add `IsHoldEm`, card animation, info strip |
| `src/CardGames.Poker.Web/Components/Shared/TableCanvas.razor` | Community card labels, street indicator |
| `src/CardGames.Poker.Web/Components/Shared/TableSeat.razor` | SB/BB badges |
| `src/CardGames.Poker.Web/Components/Shared/DealerChoiceModal.razor` | Blind field detection |
| `src/CardGames.Poker.Web/wwwroot/app.css` | Flop/turn/river visual grouping, SB/BB badges, animations |

### Files Verified (No Changes Needed)

| File | Why No Change |
|------|--------------|
| `PokerGameMetadataRegistry.cs` | HOLDEM auto-discovered, constant already exists |
| `PokerGameRulesRegistry.cs` | HOLDEM auto-discovered via assembly scanning |
| `GameFlowHandlerFactory.cs` | Auto-discovers HoldEmFlowHandler |
| `PhaseDescriptionResolver.cs` | Hold 'Em phases already resolve correctly |
| `LeaveGameCommandHandler.cs` | Hold 'Em phases already included |
| Database schema / migrations | All needed columns exist, no migrations required |
| `HoldEmGame.cs` | Domain layer complete |
| `HoldemHand.cs` | Hand evaluation complete |
| `HoldEmGameTests.cs` | Tests passing |
| `ActionPanel.razor` | All 6 betting actions already supported |
| SignalR infrastructure | Generic — handles all game types |
