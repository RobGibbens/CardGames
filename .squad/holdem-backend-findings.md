# Texas Hold 'Em Backend Integration — Deep Research Findings

**Author:** Gimli (Backend Dev)  
**Date:** 2026-03-05  
**Requested by:** Rob Gibbens  
**Purpose:** Comprehensive backend audit for Hold 'Em integration into the poker platform.

---

## Executive Summary

The Hold 'Em domain model is **well-scaffolded** in `CardGames.Poker` and the `HoldEmFlowHandler` is partially wired into the API layer. However, there are **critical missing pieces** that prevent a playable game: No API endpoints for Hold 'Em betting actions, no community card dealing between betting rounds, no Hold 'Em showdown handler, and the `CreateGameCommand` does not accept SmallBlind/BigBlind parameters. The phase transition machinery exists but has **no integration hook for dealing community cards** between betting rounds.

---

## 1. Phase Advancement for Community Card Games

### How BaseGameFlowHandler Works

`BaseGameFlowHandler.GetNextPhase()` walks the `GameRules.Phases` list sequentially — it finds the current phase index and returns `phases[currentIndex + 1]`. This is a linear chain. Games like GoodBadUgly override `GetNextPhase()` with explicit switch-based transitions (ThirdStreet → RevealTheGood → FourthStreet → etc.).

The base handler's `DealDrawStyleCardsAsync()` deals initial cards, then looks up `GetNextPhase(game, "Dealing")` to determine what phase comes after dealing. It creates a `BettingRound` entity and sets `game.CurrentPhase` accordingly. **There is no built-in mechanism for dealing community cards between phases.**

### How GoodBadUgly Handles Between-Phase Actions

GoodBadUgly uses `SpecialPhases` (RevealTheGood, RevealTheBad, RevealTheUgly) which are not betting phases. When a betting round completes, the Seven Card Stud ProcessBettingAction handler calls `AdvanceToNextPhase()` which advances to the next street. The GBU flow handler overrides `GetNextPhase()` to interleave reveal phases with betting streets.

The GBU dealing flow pre-deals 3 table cards as `CardLocation.Community` face-down during initial dealing, then reveals them during reveal phases. This is a **stud-style pattern**, not a community-card-dealing-between-rounds pattern.

### Key Takeaway

**No existing game demonstrates the Hold 'Em pattern** of dealing community cards from the deck between betting rounds (flop=3, turn=1, river=1). GoodBadUgly pre-deals its table cards. The platform currently lacks a "deal community cards when transitioning to a new betting round" hook.

---

## 2. HoldEmFlowHandler Completeness

### What Exists

- GameTypeCode: `"HOLDEM"`
- DealingPatternType: `CommunityCard` (but no code reads this enum to drive behavior)
- `SkipsAnteCollection`: `true` (uses blinds)
- `GetInitialPhase()`: returns `"Dealing"`
- `DealCardsAsync()`: Collects blinds, then calls `base.DealDrawStyleCardsAsync()` to deal 2 hole cards per player
- Blind collection logic: Proper heads-up handling (dealer=SB), correct SB/BB positioning

### What's Missing

1. **No `GetNextPhase()` override** — Falls back to base which scans `GameRules.Phases` linearly. The phases list starts with `WaitingToStart` and includes `PreFlop`, `Flop`, `Turn`, `River`, `Showdown`, `Complete`. But after `DealDrawStyleCardsAsync()` looks up `GetNextPhase(game, "Dealing")`, it won't find "Dealing" in the phases list — it's not declared as a phase. **The initial phase transition after dealing is broken.**

2. **No community card dealing between rounds** — No logic to deal Flop (3 cards), Turn (1 card), or River (1 card) as `CardLocation.Community` cards. The `DealDrawStyleCardsAsync` only deals hole cards.

3. **No `PerformShowdownAsync()` override** — `SupportsInlineShowdown` defaults to `false`. There's no Hold 'Em-specific showdown that combines hole cards + community cards using `HoldemHand`.

4. **No `SendBettingActionAsync()` override** — Falls back to the FiveCardDraw betting command, which is wrong for Hold 'Em.

5. **No pre-flop first-to-act logic** — Pre-flop, action starts left of the big blind (or dealer in heads-up). Post-flop, action starts left of the dealer. The `DealDrawStyleCardsAsync()` uses `FindFirstActivePlayerAfterDealer()` for all rounds — incorrect for pre-flop.

6. **No `GetNextPhase()` for PreFlop→Flop→Turn→River→Showdown** transitions with single-player-remaining checks.

---

## 3. Game Creation Flow

### CreateGameCommand

```csharp
public record CreateGameCommand(
    Guid GameId, string GameCode, string? GameName,
    int Ante, int MinBet,
    IReadOnlyList<PlayerInfo> Players,
    bool IsDealersChoice = false)
```

**Critical gap: No SmallBlind/BigBlind parameters.** The handler creates the `Game` entity with `Ante` and `MinBet` but never sets `SmallBlind` or `BigBlind`. These fields exist on the entity but are only populated via the `UpdateTableSettings` command (which does accept SmallBlind/BigBlind).

The handler calls `GetOrCreateGameTypeAsync()` which uses `PokerGameMetadataRegistry`. Hold 'Em IS registered there (via `[PokerGameMetadata]` attribute on `HoldEmGame.cs`), so `CreateGameCommand` with `GameCode = "HOLDEM"` would resolve the GameType correctly.

### UpdateTableSettings

The `UpdateTableSettingsCommand` already supports `SmallBlind` and `BigBlind` fields, and includes validation that `BigBlind >= SmallBlind`. So blinds CAN be set post-creation.

### Workaround vs Fix

Either: (a) Add SmallBlind/BigBlind to `CreateGameCommand`, or (b) rely on `UpdateTableSettings` after creation. Option (a) is cleaner.

---

## 4. Data Entities

### Game Entity

All required fields **already exist**:
- `SmallBlind` (int?) 
- `BigBlind` (int?)
- `DealerPosition` (int)
- `Ante` (int?)
- `MinBet` (int?)
- `BringIn` (int?)
- `SmallBet` / `BigBet` (int? — for structured limit)
- `IsDealersChoice` (bool) — needed for DC integration

These are already in the database (migrations exist going back to 2025-12).

### GameCard Entity

Fully supports community cards:
- `CardLocation.Community` = 3 — for shared community cards
- `CardLocation.Hole` = 1 — for player hole cards
- `DealtAtPhase` (string?) — can be set to "PreFlop", "Flop", "Turn", "River"
- `IsVisible` (bool) — community cards are visible to all
- `DealOrder` (int) — ordering within location

**No gaps here.** The entity model is ready for Hold 'Em community cards.

### BettingRound Entity

The `BettingRound` entity (data layer) stores:
- `Street` (string) — maps to phase name
- `RoundNumber` (int)
- `CurrentBet`, `MinBet`, `RaiseCount`, `MaxRaises`, `LastRaiseAmount`
- `CurrentActorIndex`, `LastAggressorIndex`
- `PlayersInHand`, `PlayersActed`

This supports multiple rounds per hand. Hold 'Em's 4 betting rounds (PreFlop, Flop, Turn, River) map naturally to 4 `BettingRound` records.

---

## 5. Betting Round (Domain)

### BettingRound.cs (Domain Model)

The domain `BettingRound` class supports:
- Configurable dealer position and min bet
- Optional initial forced bet (for blinds/bring-in)
- Check, Bet, Call, Raise, Fold, AllIn actions
- Round completion detection (all acted + bets matched)
- Last aggressor tracking

**This works for Hold 'Em.** The constructor accepting `initialBet` and `forcedBetPlayerIndex` is used by `HoldEmGame.StartPreFlopBettingRound()` to set the big blind as the initial bet. Post-flop rounds use the simpler constructor with no forced bet.

### First-to-Act Positions

The domain `HoldEmGame` correctly handles:
- Pre-flop: Left of BB (or dealer in heads-up)
- Post-flop: First active player left of dealer

However, this logic is **only in the domain model** — it has NOT been translated to the API layer. The `DealDrawStyleCardsAsync()` base method uses `FindFirstActivePlayerAfterDealer()` for all phases, which is only correct for post-flop rounds.

---

## 6. GameFlowHandlerFactory

`HoldEmFlowHandler` **will be auto-discovered.** The factory uses assembly scanning via `Assembly.GetExecutingAssembly()`, creating instances of all `IGameFlowHandler` implementations. Since `HoldEmFlowHandler` is a concrete class in the API project with a parameterless constructor (implicitly), it will be found and registered under `"HOLDEM"`.

Confirmed: The factory logs all discovered handlers at startup. No manual registration needed.

---

## 7. TableStateBuilder

### Community Cards — Already Handled

TableStateBuilder already queries community cards:
```csharp
var communityCards = await _context.GameCards
    .Where(c => c.GameId == gameId && !c.IsDiscarded
        && c.HandNumber == game.CurrentHandNumber
        && c.Location == CardLocation.Community)
    .OrderBy(c => c.DealOrder)
    ...
```
This produces a `List<CardPublicDto>` sent to clients with `IsFaceUp`, `Rank`, `Suit`, `DealOrder`. All community cards deal-ordered and visibility-checked. **This will work for Hold 'Em out of the box** as long as community cards are persisted with `Location = CardLocation.Community` and `IsVisible = true`.

### Hand Evaluation — Already Handled

TableStateBuilder's private state builder has explicit Hold 'Em handling:
```csharp
// Hold'em / Short-deck Hold'em style: 2 hole + up to 5 community
else if (playerCards.Count == 2)
{
    var holdemHand = new HoldemHand(playerCards, communityCards);
    handEvaluationDescription = HandDescriptionFormatter.GetHandDescription(holdemHand);
}
```

So once community cards are dealt correctly, hand evaluation descriptions will work in the UI.

### Gaps

- **SmallBlind/BigBlind in TableStatePublicDto**: The public DTO exposes `Ante` and `MinBet` but may not expose blind amounts. Players need to see who posted blinds and amounts. Need to verify DTO fields.
- **Dealer/SB/BB position indicators**: The DTO has `DealerSeatIndex` but not explicit SmallBlind/BigBlind seat indices. These would need to be derived client-side or added.

---

## 8. PhaseDescriptionResolver

The resolver uses a **generic approach** — it tries to parse phase names as the `Phases` enum and returns the `[Description]` attribute:

```csharp
return TryResolveEnumDescription<Phases>(currentPhase);
```

The `Phases` enum already includes `PreFlop`, `Flop`, `Turn`, `River` with descriptions "Pre-Flop", "Flop", "Turn", "River". **No changes needed.** Hold 'Em phases will resolve to human-readable descriptions automatically.

Note: The resolver imports `CardGames.Poker.Games.HoldEm` but doesn't use it — likely leftover from earlier development.

---

## 9. Dealer's Choice Integration

### How Dealer's Choice Works

The `Game` entity has:
- `IsDealersChoice` (bool)
- `CurrentHandGameTypeCode` (string?) — set when dealer picks
- `DealersChoiceDealerPosition` (int?)

The `ContinuousPlayBackgroundService` handles DC flow:
1. When a variant finishes (no multi-hand continuation), it sets `CurrentPhase = "WaitingForDealerChoice"` and rotates the DC dealer
2. The DC dealer picks a game type code → sets `CurrentHandGameTypeCode`, resolves the `GameType`, and triggers the hand start
3. The background service uses `flowHandlerFactory.GetHandler(game.CurrentHandGameTypeCode)` to get the right handler

### Would Hold 'Em Be Included?

**Yes, automatically.** Hold 'Em is registered in `PokerGameMetadataRegistry` via the `[PokerGameMetadata]` attribute on `HoldEmGame`. The DC game picker UI queries available game types from this registry. The flow handler factory will find `HoldEmFlowHandler` for the "HOLDEM" code.

**However**, the DC flow relies on the game type's betting being set up correctly. For Hold 'Em:
- The DC hand-start flow calls `flowHandler.DealCardsAsync()` (which collects blinds + deals)
- But blinds require `game.SmallBlind` and `game.BigBlind` to be set
- In DC mode, these would need to come from table configuration or the game type metadata

**Gap:** The DC "Choose Game" command handler may not set SmallBlind/BigBlind when switching to a blinds-based game. This needs investigation.

---

## 10. BettingStructure Enum

```csharp
public enum BettingStructure
{
    Ante = 0,
    Blinds = 1,
    AnteBringIn = 2,
    AntePotMatch = 3
}
```

**`Blinds = 1` exists.** The `HoldEmGame` metadata attribute correctly uses `BettingStructure.Blinds`. This is stored in the `GameType` table when auto-created.

---

## Summary: What Already Exists and Works

| Component | Status | Notes |
|-----------|--------|-------|
| Domain model (`HoldEmGame`) | **Complete** | Full game orchestration with blinds, all 4 betting rounds, community dealing, showdown |
| `HoldEmRules.CreateGameRules()` | **Complete** | Correct phases, dealing config, betting config |
| `HoldEmFlowHandler` (basic) | **Partial** | Blind collection + hole card dealing works. Missing phase transitions, community dealing, showdown |
| `Game` entity (SmallBlind/BigBlind) | **Complete** | Fields exist, migrations applied |
| `GameCard` entity (Community) | **Complete** | CardLocation.Community, DealtAtPhase, IsVisible all ready |
| `BettingRound` entity | **Complete** | Supports 4 rounds per hand |
| `Phases` enum | **Complete** | PreFlop, Flop, Turn, River, CollectingBlinds all present |
| `BettingStructure.Blinds` | **Complete** | Enum value exists |
| `PokerGameMetadataRegistry` | **Complete** | HoldEmCode constant, auto-discovery works |
| `GameFlowHandlerFactory` | **Complete** | Will auto-discover HoldEmFlowHandler |
| `TableStateBuilder` community cards | **Complete** | Queries and exposes community cards to UI |
| `TableStateBuilder` hand eval | **Complete** | HoldemHand evaluator wired in for 2-card hands |
| `PhaseDescriptionResolver` | **Complete** | Phase descriptions auto-resolve from enum |
| `UpdateTableSettings` (blinds) | **Complete** | SmallBlind/BigBlind can be set post-creation |
| Domain `BettingRound` | **Complete** | Supports blinds as initial forced bet |

## What's Partially Implemented

| Component | Gap | Impact |
|-----------|-----|--------|
| `HoldEmFlowHandler.DealCardsAsync()` | Deals hole cards + collects blinds, but doesn't set up correct pre-flop first-to-act | Pre-flop action starts at wrong player |
| `HoldEmFlowHandler.GetNextPhase()` | Not overridden — base linear scan won't find phases correctly because "Dealing" isn't in the phases list | Phase transitions broken after dealing |
| `HoldEmPhase.cs` | Entire file is commented out | Not blocking (Phases enum covers it), but indicates incomplete work |

## What's Completely Missing

| Component | Description | Effort Estimate |
|-----------|-------------|-----------------|
| **Hold 'Em ProcessBettingAction** | No API endpoint or command handler for processing betting actions in Hold 'Em games. Each game variant (FiveCardDraw, SevenCardStud, GoodBadUgly, TwosJacks) has its own. Hold 'Em needs one that handles PreFlop/Flop/Turn/River transitions + community card dealing. | HIGH |
| **Community card dealing between rounds** | When a betting round completes (PreFlop→Flop), 3 cards must be dealt as `CardLocation.Community`. Turn/River each deal 1 card. No existing pattern does this. | HIGH |
| **Pre-flop first-to-act** | Pre-flop: UTG (left of BB) acts first. Post-flop: left of dealer. This positional switch is not in the API layer. | MEDIUM |
| **CreateGameCommand blind support** | Command doesn't accept SmallBlind/BigBlind. New Hold 'Em games created via API will have null blinds. | LOW-MEDIUM |
| **Hold 'Em showdown handler** | API showdown must evaluate `HoldemHand(holeCards, communityCards)` for each player. The generic showdown handler likely doesn't handle community card games. | MEDIUM |
| **All-in runout** | When all players are all-in, remaining community cards should be dealt automatically without betting rounds. | MEDIUM |
| **Dealer's Choice blind propagation** | When DC dealer picks Hold 'Em, SmallBlind/BigBlind must be set on the game from table-level defaults or metadata. Currently no code does this. | LOW |
| **Feature folder** | `Features/Games/HoldEm/` doesn't exist. Needs: endpoints, commands, command handlers for ProcessBettingAction. | HIGH |
| **Hold 'Em auto-action override** | `SendBettingActionAsync`/`PerformAutoActionAsync` defaults to FiveCardDraw commands. Must override to use Hold 'Em-specific commands. | LOW |

## Integration Gaps & Potential Breaking Changes

1. **Pattern divergence from Stud games**: SevenCardStud's ProcessBettingAction handler has its own `AdvanceToNextPhase()` that hardcodes street progression + triggers `DealHandsCommand` for the next street. Hold 'Em would need a similar mechanism but dealing COMMUNITY cards rather than per-player street cards. This is a **new pattern** in the codebase.

2. **Eligible player check in ContinuousPlayBackgroundService**: The `ante >= chip_stack` check for eligibility uses `game.Ante ?? 0`. For blind-based games, this should check against BigBlind instead. Players with fewer chips than the big blind should be sat out. This would be a **behavioral change** affecting blind-based games.

3. **No "Dealing" phase in HoldEm rules**: The HoldEmRules phases go WaitingToStart → PreFlop → Flop → ... but the HoldEmFlowHandler returns "Dealing" as the initial phase. "Dealing" is not in the phases list, so `GetNextPhase(game, "Dealing")` returns null. The base `DealDrawStyleCardsAsync` falls back to `"FirstBettingRound"` which is wrong for Hold 'Em. Fix: either add "Dealing" to phases or override `GetNextPhase()`.

4. **CreateGameCommand backward compatibility**: Adding SmallBlind/BigBlind to the command is additive (nullable params) and should not break existing callers. The Refit client would need regeneration.

5. **Omaha is in the same state**: `OmahaFlowHandler` has the same structural gaps (no betting action handler, no community dealing between rounds, identical blind collection code duplicated from Hold 'Em). Any solution for Hold 'Em should also cover Omaha.

---

## Recommended Implementation Order

1. **Override `GetNextPhase()` in `HoldEmFlowHandler`** — PreFlop→Flop→Turn→River→Showdown with single-player checks
2. **Build community card dealing method** — Reusable for Hold 'Em + Omaha. Deals N cards as `CardLocation.Community` from the existing deck
3. **Create `Features/Games/HoldEm/` feature folder** with ProcessBettingAction command/handler/endpoint
4. **Integrate betting action handler** with phase transition + community dealing between rounds
5. **Add SmallBlind/BigBlind to CreateGameCommand**
6. **Build Hold 'Em showdown handler** using `HoldemHand` evaluation
7. **Wire auto-action overrides**
8. **Handle all-in runout** (deal remaining community cards without betting)
9. **Test Dealer's Choice integration** with blind propagation
10. **Create Omaha equivalents** sharing community card infrastructure
