# Game Type Extensibility Analysis

This document provides a comprehensive analysis of all places in the codebase where code makes decisions based on game types or game phases, identifying areas that would require code changes when introducing a new poker variant.

---

## Executive Summary

The codebase has **partial extensibility** through the `IPokerGame` interface, `PokerGameMetadataAttribute`, and `GameRules` system. However, there are significant areas with **hardcoded game type checks** that violate the Open-Closed Principle. Adding a new game type currently requires modifications in **15+ locations** across multiple projects.

> ⚠️ **Critical Limitation:** The current architecture assumes all games are **poker variants**. The `IPokerGame` interface, `PokerGameMetadataAttribute`, poker hand evaluation, chip-based betting, and showdown mechanics are deeply embedded throughout the codebase. Adding a **non-poker card game** (e.g., "Screw Your Neighbor", Blackjack, Uno) would require fundamental architectural changes beyond what is documented here. See [Section 10: Non-Poker Game Support](#10-non-poker-game-support-architectural-gap) for analysis.

### Risk Rating by Area

| Area | Extensibility | Risk | Files to Modify |
|------|--------------|------|-----------------|
| Game Metadata Registry | Medium | 🟡 | 1 |
| Game Rules Registry | Medium | 🟡 | 1 |
| API Endpoint Mapping | Low | 🔴 | 1 |
| Background Service | Low | 🔴 | 1 |
| Table State Builder | Low | 🔴 | 1 |
| Auto Action Service | Low | 🔴 | 1 |
| Blazor TablePlay | Low | 🔴 | 1 |
| Phase Description Resolver | Medium | 🟡 | 1 |
| Feature Command Handlers | N/A | 🟢 | N/A (new files) |
| **Non-Poker Games** | None | 🔴🔴🔴 | 30+ (architectural rewrite) |

---

## 1. Registry & Metadata Layer

### 1.1 `PokerGameMetadataRegistry.cs`
**File:** `CardGames.Poker.Api/Games/PokerGameMetadataRegistry.cs`

**Issue:** Hardcoded dictionary mapping game type codes to game classes.

```csharp
// Lines 16-34: Static dictionary requires modification for each new game
public const string HoldEmCode = "HOLDEM";
public const string FiveCardDrawCode = "FIVECARDDRAW";
public const string TwosJacksManWithTheAxeCode = "TWOSJACKSMANWITHTHEAXE";
public const string OmahaCode = "OMAHA";
public const string SevenCardStudCode = "SEVENCARDSTUD";
public const string KingsAndLowsCode = "KINGSANDLOWS";
public const string FollowTheQueenCode = "FOLLOWTHEQUEEN";

private static readonly FrozenDictionary<string, PokerGameMetadataAttribute> MetadataByGameTypeCode =
    new Dictionary<string, PokerGameMetadataAttribute>(StringComparer.OrdinalIgnoreCase)
    {
        [HoldEmCode] = GetMetadataOrThrow(typeof(HoldEmGame)),
        [FiveCardDrawCode] = GetMetadataOrThrow(typeof(FiveCardDrawGame)),
        // ... each game type explicitly listed
    }.ToFrozenDictionary();
```

**Recommendation:** Use assembly scanning with `PokerGameMetadataAttribute` reflection to auto-discover games.

---

### 1.2 `PokerGameRulesRegistry.cs`
**File:** `CardGames.Poker.Api/Games/PokerGameRulesRegistry.cs`

**Issue:** Hardcoded mapping of game codes to rule factory methods.

```csharp
// Lines 17-25: Manual registration of each game's rules
private static readonly FrozenDictionary<string, Func<GameRules>> RulesByGameTypeCode =
    new Dictionary<string, Func<GameRules>>(StringComparer.OrdinalIgnoreCase)
    {
        [PokerGameMetadataRegistry.FiveCardDrawCode] = FiveCardDrawRules.CreateGameRules,
        [PokerGameMetadataRegistry.TwosJacksManWithTheAxeCode] = TwosJacksManWithTheAxeRules.CreateGameRules,
        [PokerGameMetadataRegistry.KingsAndLowsCode] = KingsAndLowsRules.CreateGameRules,
        [PokerGameMetadataRegistry.SevenCardStudCode] = SevenCardStudRules.CreateGameRules,
    }.ToFrozenDictionary();
```

**Recommendation:** Have `IPokerGame.GetGameRules()` be the canonical source; discover via reflection.

---

## 2. API Endpoint Layer

### 2.1 `MapFeatureEndpoints.cs`
**File:** `CardGames.Poker.Api/Features/MapFeatureEndpoints.cs`

**Issue:** Each game variant has its own endpoint registration method called explicitly.

```csharp
// Lines 13-22: Manual endpoint registration for each variant
public static void AddFeatureEndpoints(this IEndpointRouteBuilder app)
{
    app.AddActiveGamesEndpoints();
    app.AddAvailablePokerGamesEndpoints();
    app.AddGamesEndpoints();
    app.AddFiveCardDrawEndpoints();      // Must add line for new game
    app.AddKingsAndLowsEndpoints();       // Must add line for new game
    app.AddSevenCardStudEndpoints();      // Must add line for new game
    app.AddTwosJacksManWithTheAxeEndpoints(); // Must add line for new game
}
```

**Recommendation:** Use assembly scanning to discover and register endpoint map groups.

---

### 2.2 Game-Specific Feature Folders
**Location:** `CardGames.Poker.Api/Features/Games/{GameType}/`

**Issue:** Each game variant requires its own feature folder with duplicate handler patterns:
- `StartHand/`
- `DealHands/`
- `CollectAntes/`
- `ProcessBettingAction/`
- `ProcessDraw/`
- `PerformShowdown/`

**Current Games with Feature Folders:**
- `FiveCardDraw/`
- `SevenCardStud/`
- `KingsAndLows/`
- `TwosJacksManWithTheAxe/`

**Recommendation:** Create generic command handlers that use `GameRules` and strategy patterns. Game-specific logic should be encapsulated in domain classes.

---

## 3. Background Services

### 3.1 `ContinuousPlayBackgroundService.cs`
**File:** `CardGames.Poker.Api/Services/ContinuousPlayBackgroundService.cs`

**CRITICAL ISSUE:** Extensive hardcoded game type checks throughout the service.

#### Lines 168-206: DrawComplete Phase Handling
```csharp
// Kings and Lows specific DrawComplete -> Showdown transition
private async Task ProcessDrawCompleteGamesAsync(...)
```

#### Lines 239-467: Kings and Lows Specific Showdown
```csharp
// Hardcoded Kings and Lows showdown logic with KingsAndLowsDrawHand
private async Task PerformKingsAndLowsShowdownAsync(...)
{
    var hand = new CardGames.Poker.Hands.DrawHands.KingsAndLowsDrawHand(cards);
    // ...game-specific showdown logic
}
```

#### Lines 573-651: Kings and Lows Chip Check
```csharp
// Line 574: Hardcoded game type check
var isKingsAndLowsGame = string.Equals(game.GameType?.Code, "KINGSANDLOWS", StringComparison.OrdinalIgnoreCase);
if (isKingsAndLowsGame)
{
    // ... Kings and Lows specific chip check logic
}
```

#### Lines 803-845: Phase Transition Logic
```csharp
// Lines 804-806: Multiple hardcoded game type checks
var isKingsAndLows = string.Equals(game.GameType?.Code, "KINGSANDLOWS", StringComparison.OrdinalIgnoreCase);
var isSevenCardStud = string.Equals(game.GameType?.Code, "SEVENCARDSTUD", StringComparison.OrdinalIgnoreCase);

// Lines 811-813: Game-type specific phase transitions
game.CurrentPhase = isKingsAndLows
    ? nameof(Phases.Dealing)
    : nameof(Phases.CollectingAntes);
```

#### Lines 836-844: Game-Type Specific Dealing
```csharp
// Seven Card Stud gets special dealing method
if (isSevenCardStud)
{
    await DealSevenCardStudHandsAsync(...);
}
else
{
    await DealHandsAsync(...);
}
```

#### Lines 976-989: Kings and Lows Specific Dealing Logic
```csharp
var isKingsAndLows = string.Equals(game.GameType?.Code, "KINGSANDLOWS", StringComparison.OrdinalIgnoreCase);

if (isKingsAndLows)
{
    game.CurrentPhase = nameof(Phases.DropOrStay);
    // Kings and Lows goes to DropOrStay phase after dealing
}
else
{
    // Standard poker variants go to first betting round
}
```

#### Lines 1066-1225: Seven Card Stud Specific Dealing
```csharp
// Entire method is game-specific
private async Task DealSevenCardStudHandsAsync(...)
{
    // Third Street dealing: 2 hole + 1 board card
    // Bring-in determination
    // Street-specific betting round creation
}
```

**Recommendation:** 
- Extract game-specific logic into strategy classes implementing an `IGameFlowHandler` interface
- Use `GameRules` to drive phase transitions
- Move showdown logic to domain layer with game-specific evaluators

---

### 3.2 `AutoActionService.cs`
**File:** `CardGames.Poker.Api/Services/AutoActionService.cs`

**Issue:** Hardcoded phase mappings and command routing.

```csharp
// Lines 23-43: Static phase sets require modification for new game phases
private static readonly HashSet<string> BettingPhases = new(StringComparer.OrdinalIgnoreCase)
{
    "FirstBettingRound",
    "SecondBettingRound"
    // Missing: ThirdStreet, FourthStreet, etc. for Seven Card Stud
};

private static readonly HashSet<string> DrawPhases = new(StringComparer.OrdinalIgnoreCase)
{
    "DrawPhase"
};

private static readonly HashSet<string> DropOrStayPhases = new(StringComparer.OrdinalIgnoreCase)
{
    "DropOrStay"
};
```

**Recommendation:** Use `GameRules.Phases` with category information instead of hardcoded phase sets.

---

## 4. Table State & UI Support

### 4.1 `TableStateBuilder.cs`
**File:** `CardGames.Poker.Api/Services/TableStateBuilder.cs`

**CRITICAL ISSUE:** Extensive game type checks for hand evaluation and state building.

#### Lines 66-105: Seven Card Stud Card Ordering Debug
```csharp
var isSevenCardStudGame = string.Equals(game.GameType?.Code, "SEVENCARDSTUD", StringComparison.OrdinalIgnoreCase);
if (isSevenCardStudGame)
{
    // Special logging for stud card ordering
}
```

#### Lines 296-376: Hand Evaluation Description (BuildPrivateStateAsync)
```csharp
// Lines 296-335: Seven Card Stud specific evaluation
if (string.Equals(game.GameType?.Code, "SEVENCARDSTUD", StringComparison.OrdinalIgnoreCase))
{
    var studHand = new SevenCardStudHand(initialHoleCards, openCards, downCard);
    handEvaluationDescription = HandDescriptionFormatter.GetHandDescription(studHand);
}
// Lines 345-356: TwosJacksManWithTheAxe and KingsAndLows specific evaluation
else if (string.Equals(game.GameType?.Code, "TWOSJACKSMANWITHTHEAXE", StringComparison.OrdinalIgnoreCase))
{
    drawHand = new TwosJacksManWithTheAxeDrawHand(playerCards);
}
else if (string.Equals(game.GameType?.Code, "KINGSANDLOWS", StringComparison.OrdinalIgnoreCase))
{
    drawHand = new KingsAndLowsDrawHand(playerCards);
}
```

#### Lines 468-494: Seat Card Display
```csharp
// Lines 483-484: Seven Card Stud visibility check
var isSevenCardStud = string.Equals(gameTypeCode, "SEVENCARDSTUD", StringComparison.OrdinalIgnoreCase);
```

#### Lines 576-604: Private Hand Building
```csharp
// Lines 586-587: Seven Card Stud card ordering
var isSevenCardStud = string.Equals(gameTypeCode, "SEVENCARDSTUD", StringComparison.OrdinalIgnoreCase);
var orderedCards = OrderCardsForDisplay(filteredCards, isSevenCardStud).ToList();
```

#### Lines 614-624: Available Actions Phases
```csharp
// Hardcoded betting phases list
var bettingPhases = new[]
{
    "FirstBettingRound",
    "SecondBettingRound",
    "ThirdStreet",
    "FourthStreet",
    "FifthStreet",
    "SixthStreet",
    "SeventhStreet"
};
```

#### Lines 707-802: Showdown Evaluation (BuildShowdownPublicDtoAsync)
```csharp
// Lines 708-721: Multiple game type checks
var isTwosJacksAxe = string.Equals(game.GameType?.Code, PokerGameMetadataRegistry.TwosJacksManWithTheAxeCode, ...);
var isSevenCardStud = string.Equals(game.GameType?.Code, PokerGameMetadataRegistry.SevenCardStudCode, ...);
var isKingsAndLows = string.Equals(game.GameType?.Code, PokerGameMetadataRegistry.KingsAndLowsCode, ...);

// Lines 737-801: Game-specific hand creation for showdown
if (isTwosJacksAxe) { /* TwosJacksManWithTheAxeDrawHand */ }
else if (isSevenCardStud) { /* SevenCardStudHand */ }
else if (isKingsAndLows) { /* KingsAndLowsDrawHand */ }
else { /* DrawHand */ }
```

**Recommendation:**
- Create `IHandEvaluatorFactory` that returns game-specific evaluators
- Use `GameRules` metadata to drive card visibility and ordering rules
- Move hand creation logic to the domain layer with factory pattern

---

## 5. Blazor Web Project

### 5.1 `TablePlay.razor`
**File:** `CardGames.Poker.Web/Components/Pages/TablePlay.razor`

**Issue:** Direct injection and conditional use of game-specific API clients.

#### Lines 22-25: Multiple API Client Injections
```razor
@inject IFiveCardDrawApi FiveCardDrawApiClient
@inject ITwosJacksManWithTheAxeApi TwosJacksManWithTheAxeApi
@inject IKingsAndLowsApi KingsAndLowsApi
@inject ISevenCardStudApi SevenCardStudApi
```

#### Lines 525-527: Boolean Helpers for Game Types
```csharp
private bool IsTwosJacksManWithTheAxe => string.Equals(_gameTypeCode, "TWOSJACKSMANWITHTHEAXE", StringComparison.OrdinalIgnoreCase);
private bool IsKingsAndLows => string.Equals(_gameTypeCode, "KINGSANDLOWS", StringComparison.OrdinalIgnoreCase);
private bool IsSevenCardStud => string.Equals(_gameTypeCode, "SEVENCARDSTUD", StringComparison.OrdinalIgnoreCase);
```

#### Lines 226-291: Game-Specific UI Overlays
```razor
<!-- Drop or Stay Overlay (for Kings and Lows) -->
@if (HasDropOrStay && IsDropOrStayPhase && IsParticipatingInHand) { ... }

<!-- Player vs Deck Overlay (for Kings and Lows) -->
@if (IsPlayerVsDeckPhase) { ... }

<!-- Pot Matching Overlay (for Kings and Lows) -->
@if (HasPotMatching && IsPotMatchingPhase) { ... }

<!-- Chip Coverage Pause Overlay (for Kings and Lows) -->
@if (_isPausedForChipCheck && IsKingsAndLows && IsCurrentPlayerShortOnChips) { ... }
```

**Recommendation:**
- Use a single `IGameApiRouter` that dispatches to the correct API based on game type
- Drive UI component visibility from `GameRules.Phases` and `GameRules.SpecialRules`
- Use dynamic component rendering based on game rules instead of hardcoded overlays

---


## 6. Phase Resolution

### 6.1 `PhaseDescriptionResolver.cs`
**File:** `CardGames.Poker.Api/Features/Games/ActiveGames/v1/Queries/GetActiveGames/PhaseDescriptionResolver.cs`

**Issue:** Uses single `Phases` enum, but has imports for game-specific phase types (unused currently but suggests future complexity).

```csharp
// Lines 4-10: Imports for game-specific phase types
using CardGames.Poker.Games.FiveCardDraw;
using CardGames.Poker.Games.FollowTheQueen;
using CardGames.Poker.Games.HoldEm;
using CardGames.Poker.Games.KingsAndLows;
using CardGames.Poker.Games.Omaha;
using CardGames.Poker.Games.SevenCardStud;
using CardGames.Poker.Games.TwosJacksManWithTheAxe;
```

---

## 7. Domain Layer

### 7.1 `Phases.cs` (Unified Phase Enum)
**File:** `CardGames.Poker/Betting/Phases.cs`

**Issue:** Single enum containing phases for ALL game variants. Growing enum becomes unwieldy.

```csharp
public enum Phases
{
    WaitingToStart,
    CollectingAntes,
    CollectingBlinds,
    Dealing,
    PreFlop,
    Flop,
    Turn,
    River,
    DropOrStay,           // Kings and Lows
    FirstBettingRound,
    DrawPhase,
    DrawComplete,
    PotMatching,          // Kings and Lows
    PlayerVsDeck,         // Kings and Lows
    SecondBettingRound,
    ThirdStreet,          // Seven Card Stud
    BuyCardOffer,         // Baseball
    FourthStreet,         // Seven Card Stud
    FifthStreet,          // Seven Card Stud
    SixthStreet,          // Seven Card Stud
    SeventhStreet,        // Seven Card Stud
    Showdown,
    Complete,
    WaitingForPlayers
}
```

**Recommendation:** Keep unified enum for persistence, but use `GameRules.Phases` for game-specific phase sequences.

---

## 8. Summary of Required Changes for New Game Type

### 8.1 Adding a New Poker Variant (e.g., "Razz")

To add a new poker variant (e.g., "Razz"), you would need to modify:

### Mandatory Changes (Hardcoded References)
1. ✏️ `PokerGameMetadataRegistry.cs` - Add constant and dictionary entry
2. ✏️ `PokerGameRulesRegistry.cs` - Add rules factory
3. ✏️ `MapFeatureEndpoints.cs` - Add endpoint registration
4. ✏️ `ContinuousPlayBackgroundService.cs` - Add game-type checks for dealing, phase transitions, showdown
5. ✏️ `TableStateBuilder.cs` - Add hand evaluation logic
6. ✏️ `AutoActionService.cs` - Add phase mappings if new phases
7. ✏️ `TablePlay.razor` - Add API client injection, boolean helpers, overlays
8. ✏️ `Phases.cs` - Add new phase enum values if needed

### New Files Required
10. 📁 Create `CardGames.Poker.Api/Features/Games/Razz/` folder with:
    - `RazzApiMapGroup.cs`
    - `v1/Commands/StartHand/`
    - `v1/Commands/DealHands/`
    - `v1/Commands/CollectAntes/`
    - `v1/Commands/ProcessBettingAction/`
    - `v1/Commands/PerformShowdown/`
    - `v1/Queries/GetCurrentPlayerTurn/`

11. 📁 Create `CardGames.Poker/Games/Razz/` folder with:
    - `RazzGame.cs` (with `PokerGameMetadataAttribute`)
    - `RazzRules.cs`
    - `RazzPhase.cs` (optional)
    - `RazzGamePlayer.cs`

12. 📁 Create `CardGames.Poker/Hands/StudHands/RazzHand.cs`

### 8.2 Adding a Non-Poker Game (e.g., "Screw Your Neighbor")

Adding a completely different card game like "Screw Your Neighbor" would require **fundamental architectural changes**:

#### Mandatory New Files (Core Infrastructure)
1. 📁 Create `CardGames.Core/Games/ICardGame.cs` - Abstract base interface for all card games
2. 📁 Create `CardGames.Elimination/` - New project for elimination-style games
3. 📁 Create `CardGames.Elimination/Games/IEliminationGame.cs` - Interface for elimination games
4. 📁 Create `CardGames.Elimination/Games/ScrewYourNeighbor/ScrewYourNeighborGame.cs`
5. 📁 Create `CardGames.Elimination/Scoring/LowestCardEliminator.cs` - Win/loss determination
6. 📁 Create `CardGames.Elimination/Phases/ScrewYourNeighborPhases.cs` - Game-specific phases
7. 📁 Create `CardGames.Elimination/Actions/PassKeepAction.cs` - Keep/SwapLeft actions
8. 📁 Create `CardGames.Elimination/Config/EliminationRules.cs` - Lives, loss conditions

#### Database Schema Changes
1. ✏️ Add `Lives` column to `GamePlayer` (or new `EliminationGamePlayer` table)
2. ✏️ Add `IsEliminated` column
3. ✏️ Add `RoundNumber` column (game spans multiple rounds)
4. ✏️ Remove/make nullable poker-specific columns (CurrentBet, TotalContributed, etc.)
5. 📁 Create migration for schema changes

#### Mandatory Modifications (Breaking Changes)
1. ✏️ `PokerGameMetadataRegistry.cs` → Extract to `CardGameRegistry.cs` base
2. ✏️ `IPokerGame.cs` → Make inherit from `ICardGame.cs`
3. ✏️ `ContinuousPlayBackgroundService.cs` → Abstract game loop or create separate service
4. ✏️ `TableStateBuilder.cs` → Abstract hand display or create separate builder
5. ✏️ `AutoActionService.cs` → Abstract action routing
6. ✏️ `Phases.cs` → Split into poker phases and game-agnostic phases

#### New API Endpoints
1. 📁 Create `CardGames.Elimination.Api/Features/ScrewYourNeighbor/`
   - `StartRound/` (not StartHand - multiple rounds per game)
   - `ProcessPassKeep/` (Keep or SwapLeft action)
   - `RevealCards/` (trigger simultaneous reveal)
   - `ProcessElimination/` (handle life loss)
   - `GetRoundState/` (current round status)

#### New UI Components
1. 📁 Create `ScrewYourNeighborTable.razor` - Single-card layout
2. 📁 Create `LivesIndicator.razor` - 3 lives display
3. 📁 Create `PassKeepButtons.razor` - Keep/Swap actions
4. 📁 Create `EliminationOverlay.razor` - Player eliminated display
5. ✏️ Modify routing to support non-poker game URLs

#### Estimated Total Impact
| Category | Files | Effort |
|----------|-------|--------|
| New files | 40-60 | 3-4 weeks |
| Modified files | 15-25 | 1-2 weeks |
| Database migrations | 3-5 | 2-3 days |
| UI components | 10-15 | 1-2 weeks |
| Testing | 20-30 | 1-2 weeks |
| **Total** | **88-135** | **6-10 weeks** |

---

## 9. Recommendations for Improved Extensibility

### Short-term (Low Risk)
1. **Add assembly scanning** to `PokerGameMetadataRegistry` and `PokerGameRulesRegistry`
2. **Extract game type checks** into helper methods with clear naming
3. **Create `IHandEvaluatorFactory`** interface in domain layer

### Medium-term (Medium Risk)
1. **Create `IGameFlowHandler`** interface with methods for:
   - `GetNextPhase(currentPhase, gameState)`
   - `Deal(players, deck)`
   - `EvaluateShowdown(hands)`
   - `GetValidActions(phase, player)`

2. **Refactor `ContinuousPlayBackgroundService`** to use game flow handlers

3. **Create generic API endpoints** that route to game-specific handlers via MediatR

### Long-term (Higher Risk)
1. **State machine approach** for game phases driven by `GameRules`
2. **Plugin architecture** where games are self-contained assemblies
3. **Dynamic UI rendering** in Blazor driven entirely by `GameRules` metadata
4. **Abstract base game interface** (`ICardGame`) from which `IPokerGame` inherits, enabling non-poker games

---

## 10. Non-Poker Game Support (Architectural Gap)

### 10.1 Problem Statement

The current architecture makes fundamental assumptions that all games are **poker variants**. This limits the system from supporting entirely different card games such as:

- **Screw Your Neighbor** (Ranter-Go-Round) - Pass/keep single cards, lives-based elimination
- **Acey-Deucey** (In-Between) - Bet on third card falling between two spread cards, pot-based
- **Blackjack** - Player vs dealer, hit/stand mechanics
- **Uno** - Color/number matching, special action cards
- **War** - Simple card comparison
- **Go Fish** - Asking for cards, collecting sets

### 10.2 Case Study: "Screw Your Neighbor" Game Analysis

**Game Rules Summary:**
- 3+ players, each dealt 1 card face-down
- Players have 3 "lives" (chip stacks) at start
- Going clockwise from dealer's left, each player can KEEP their card or SWAP with neighbor to their left
- Kings are revealed immediately and cannot be swapped
- Dealer can swap with deck instead of neighbor
- All cards revealed simultaneously - lowest card loses a life
- Last player with lives wins

**Fundamental Mismatches with Current Architecture:**

| Aspect | Current Poker System | Screw Your Neighbor |
|--------|---------------------|---------------------|
| Interface | `IPokerGame` | Needs `ICardGame` base |
| Cards per player | 5-7 cards | 1 card |
| Win condition | Best poker hand | NOT having lowest card |
| Hand evaluation | `IHandEvaluator` with poker rankings | Simple card value comparison |
| Betting | Chips wagered in pots | Lives lost on losing rounds |
| Chip flow | Betting rounds, pot collection | N/A - lives decrement |
| Currency | Chips (integer stack) | Lives (1-3, decrement only) |
| Game duration | Single hand = complete game | Game spans multiple rounds until one survivor |
| Player actions | Bet, Call, Raise, Fold, Check, Draw | Keep, Swap |
| Action flow | Sequential betting rounds | Circular pass/keep |
| Showdown | Compare 5-card poker hands | Reveal single card, lowest loses |
| Special cards | Wild cards affect hand ranking | King = immunity from swap |
| Swap target | Draw from deck | Swap with specific neighbor |
| Dealer role | Deals cards, rotates | Special swap-with-deck action |

### 10.3 Case Study: "Acey-Deucey" Game Analysis

**Game Rules Summary:**
- 2+ players, betting against a central pot
- All players place minimum bet into pot at start
- Dealer deals two cards face-up with space between them
- Active player chooses to pass or bet any amount up to the pot size
- If betting, dealer reveals a third card between the two face-up cards
- If third card's rank is between the two face-up cards, player wins their bet from pot
- If third card is outside the range or ties one of the face-up cards, bet is added to pot
- If third card exactly matches one face-up card's rank, player "posts" (pays 2x bet to pot)
- If first card is an Ace, player chooses if it's high or low before betting
- Second Ace is always high
- Game ends when pot runs out of chips

**Fundamental Mismatches with Current Architecture:**

| Aspect | Current Poker System | Acey-Deucey |
|--------|---------------------|-------------|
| Interface | `IPokerGame` | Needs `IBettingGame` or `IPotGame` base |
| Cards per player | 5-7 cards in hand | 0 cards in hand (cards dealt to table) |
| Cards per turn | N/A | 3 cards (2 spread + 1 reveal) |
| Win condition | Best poker hand | Third card falls between two spread cards |
| Hand evaluation | `IHandEvaluator` with poker rankings | Simple rank comparison (A < C < B?) |
| Betting target | Against other players | Against central pot |
| Betting amount | Fixed structures (limit, pot-limit, no-limit) | Any amount from 0 to pot size |
| Chip flow | Betting rounds, pot splitting | Win from pot or lose to pot |
| Penalty mechanics | None | "Posting" = 2x bet on match |
| Game structure | Fixed phases per hand | Individual turns until pot empty |
| Player turns | All players act in betting rounds | One player acts per turn |
| Ace handling | Fixed value or wild card | Player chooses high/low for first Ace only |
| Game end condition | Showdown completes hand | Pot depleted = game over |
| Dealer role | Deals, rotates position | Deals spread cards, reveals third card |
| Community cards | Shared by all players for hands | Per-turn spread cards, not shared |

**Why Generic Poker Handlers Won't Work:**

| Proposed Handler | Purpose | Acey-Deucey Need |
|------------------|---------|------------------|
| `StartHandCommand` | Reset for new poker hand | Need `StartTurnCommand` (individual player turns) |
| `CollectAntesCommand` | Collect chip antes | Only once at game start, not per hand |
| `DealHandsCommand` | Deal 5-7 cards to players | Deal 2 cards face-up to table center |
| `ProcessBettingActionCommand` | Bet, Call, Raise, Fold | Pass or BetAmount (0 to pot size) |
| `ProcessDrawCommand` | Discard and draw from deck | ❌ No drawing |
| `PerformShowdownCommand` | Evaluate poker hands, award pot | Reveal third card, check if in-between |

**Missing Concepts:**

1. **Spread Card Mechanics** - No equivalent in poker
```csharp
public class SpreadCards
{
    public Card LowCard { get; set; }
    public Card HighCard { get; set; }
    public Card? RevealedCard { get; set; }
    public bool IsAceFlexible { get; set; }  // First card is Ace
    public AceChoice? PlayerAceChoice { get; set; }  // High or Low
}

public enum AceChoice { High, Low }
```

2. **Pot-Based Betting** - Different from poker pots
```csharp
// Poker: Complex pot structure with side pots
public class Pot { ... multiple contributors, splitting rules ... }

// Acey-Deucey: Simple pot
public class BettingPot
{
    public int Amount { get; set; }
    public void AddBet(int amount) => Amount += amount;
    public void PayWinner(int amount) => Amount -= amount;
}
```

3. **Posting Penalty** - No poker equivalent
```csharp
public class TurnResult
{
    public TurnOutcome Outcome { get; set; }
    public int AmountWonOrLost { get; set; }
}

public enum TurnOutcome
{
    Passed,         // No bet, no change
    Won,            // Third card in-between, win bet from pot
    Lost,           // Third card outside, lose bet to pot
    Posted          // Third card matched spread card, lose 2x bet to pot
}
```

4. **Card Range Comparison** - Simple but unique logic
```csharp
public interface ISpreadCardEvaluator
{
    SpreadResult Evaluate(Card low, Card high, Card revealed);
}

public enum SpreadResult
{
    InBetween,      // Win
    Outside,        // Lose
    MatchedLow,     // Post (2x loss)
    MatchedHigh     // Post (2x loss)
}
```

5. **Ace Flexibility Decision** - Per-turn player choice
```csharp
// When first dealt card is an Ace, player must choose before betting
public interface IAceDecisionHandler
{
    Task<AceChoice> GetPlayerChoiceAsync(Guid playerId, Card aceCard);
}
```

### 10.4 Hardcoded Poker Assumptions

#### 10.3.1 `PokerGameMetadataAttribute.cs`

The game metadata attribute is entirely poker-specific:

```csharp
public sealed class PokerGameMetadataAttribute(
    string code,
    string name,
    string description,
    int minimumNumberOfPlayers,
    int maximumNumberOfPlayers,
    int initialHoleCards,        // ❌ Poker-specific (hole cards concept)
    int initialBoardCards,       // ❌ Poker-specific (board cards concept)
    int maxCommunityCards,       // ❌ Poker-specific (community cards)
    int maxPlayerCards,          // ❌ Assumes 5-7 card hands
    bool hasDrawPhase,           // ❌ Poker-specific phase
    int maxDiscards,             // ❌ Assumes poker draw mechanics
    WildCardRule wildCardRule,   // ❌ Poker wild card concept
    BettingStructure bettingStructure, // ❌ Poker betting structures
    string? imageName = null) : Attribute
```

**For Screw Your Neighbor:** None of these properties apply. The game has:
- 1 card per player
- No hole/board/community distinction
- No drawing (only pass/keep)
- No wild cards (Kings just can't be swapped)
- No betting structure (lives-based elimination)

**For Acey-Deucey:** None of these properties apply. The game has:
- 0 cards per player (cards dealt to table center)
- 2 "spread" cards + 1 reveal card per turn
- No drawing
- No wild cards (Aces have flexible value, not wild)
- Betting is against pot, not structured rounds

#### 10.3.2 `GameRules.cs`

The `GameRules` class has poker-centric configurations:

```csharp
public class GameRules
{
    public required CardDealingConfig CardDealing { get; init; }  // Assumes poker dealing
    public required BettingConfig Betting { get; init; }          // ❌ Screw Your Neighbor has no betting
    public DrawingConfig? Drawing { get; init; }                  // ❌ Different from pass/keep
    public required ShowdownConfig Showdown { get; init; }        // ❌ Not poker-style showdown
}

public class BettingConfig
{
    public required bool HasAntes { get; init; }       // ❌ N/A
    public bool HasBlinds { get; init; }               // ❌ N/A
    public required int BettingRounds { get; init; }   // ❌ N/A
    public required string Structure { get; init; }     // ❌ N/A
}
```

**For Screw Your Neighbor:** Would need:
```csharp
public class EliminationConfig
{
    public required int StartingLives { get; init; }           // 3 lives
    public required string LossCondition { get; init; }        // "LowestCard"
    public required bool LastSurvivorWins { get; init; }       // true
}

public class PassKeepConfig
{
    public required bool CanPassToNeighbor { get; init; }      // true
    public required string PassDirection { get; init; }        // "Left"
    public required string ImmunityCard { get; init; }         // "King"
    public required bool DealerCanSwapWithDeck { get; init; }  // true
}
```

**For Acey-Deucey:** Would need:
```csharp
public class PotBettingConfig
{
    public required int InitialAntePerPlayer { get; init; }    // Minimum bet to start
    public required int MinBet { get; init; }                  // 0 (can pass)
    public required string MaxBetRule { get; init; }           // "PotSize"
    public required bool GameEndsWhenPotEmpty { get; init; }   // true
}

public class SpreadCardConfig
{
    public required int SpreadCardsCount { get; init; }        // 2
    public required int RevealCardsCount { get; init; }        // 1
    public required string WinCondition { get; init; }         // "InBetween"
    public required string AceHandling { get; init; }          // "FirstAceFlexible"
    public required bool HasPostingPenalty { get; init; }      // true
    public required int PostingMultiplier { get; init; }       // 2
}
```

#### 10.3.3 `IHandEvaluator` and Hand Classes

The entire hand evaluation system assumes poker hands:

```csharp
public interface IHandEvaluator
{
    HandBase CreateHand(IReadOnlyCollection<Card> cards);  // Expects 5+ cards
    // ...
}

public abstract class HandBase
{
    public long Strength { get; }           // Poker hand strength calculation
    public HandType HandType { get; }       // Flush, Straight, Pair, etc.
}
```

**For Screw Your Neighbor:** Need simple card comparison:
```csharp
public interface ISingleCardComparer
{
    int CompareCards(Card a, Card b);  // Simple rank comparison
    Card GetLowestCard(IEnumerable<Card> cards);
}
```

#### 10.3.4 `Phases.cs` Enum

All phases are poker-centric:

```csharp
public enum Phases
{
    WaitingToStart,
    CollectingAntes,      // ❌ No antes in Screw Your Neighbor
    CollectingBlinds,     // ❌ No blinds
    Dealing,
    FirstBettingRound,    // ❌ No betting
    DrawPhase,            // ❌ No drawing (different from pass/keep)
    SecondBettingRound,   // ❌ No betting
    Showdown,             // ❌ Different - simultaneous reveal, lowest loses life
    Complete,
    // ... stud streets, etc.
}
```

**For Screw Your Neighbor:** Would need:
```csharp
public enum ScrewYourNeighborPhases
{
    WaitingToStart,
    Dealing,              // Deal 1 card to each player
    PassKeepRound,        // Players decide to keep or pass
    SimultaneousReveal,   // All reveal, determine lowest
    LosesLife,            // Lowest card player loses a life
    EliminationCheck,     // Check if any player is out of lives
    RoundComplete,        // Reset for next round
    GameComplete          // One survivor wins
}
```

#### 10.3.5 Database Entities

**`GamePlayer.cs`** has poker-specific columns:

```csharp
public class GamePlayer
{
    public int ChipStack { get; set; }              // ❌ Should be Lives for SYN
    public int CurrentBet { get; set; }             // ❌ N/A
    public int TotalContributedThisHand { get; set; } // ❌ N/A
    public bool HasFolded { get; set; }             // ❌ Not applicable
    public bool IsAllIn { get; set; }               // ❌ N/A
    public DropOrStayDecision? DropOrStayDecision { get; set; } // Close, but not exact
}
```

**For Screw Your Neighbor:**
```csharp
public class ScrewYourNeighborPlayer
{
    public int Lives { get; set; }                  // 0-3
    public bool IsEliminated { get; set; }          // Lives == 0
    public PassKeepDecision? Decision { get; set; } // Keep, PassLeft
    public bool HasKing { get; set; }               // Immune to swap
}
```

#### 10.3.6 UI Components

**`TablePlay.razor`** assumes poker table layout:
- Pot display (irrelevant for SYN)
- Betting action buttons (Bet, Call, Raise, Fold)
- Hand evaluation display
- Cards-in-hand visualization (expects 5-7 cards)

**For Screw Your Neighbor:**
- Lives indicator (3 hearts/tokens per player)
- Single card display (face-down until reveal)
- Keep/Swap action buttons
- Elimination status
- Round counter

### 10.5 Required Architectural Changes for Non-Poker Games

#### Level 1: Abstract Base Layer

Create a game-agnostic base layer:

```csharp
// New abstract base
public interface ICardGame
{
    string Code { get; }
    string Name { get; }
    string Description { get; }
    int MinPlayers { get; }
    int MaxPlayers { get; }
    IGameRulesBase GetRules();
}

// Existing poker games inherit from this
public interface IPokerGame : ICardGame
{
    PokerSpecificRules GetPokerRules();
}

// New non-poker games implement directly
public interface IEliminationCardGame : ICardGame
{
    EliminationRules GetEliminationRules();
}
```

#### Level 2: Registry Abstraction

```csharp
// Abstract registry
public interface ICardGameRegistry
{
    bool TryGetGame(string code, out ICardGame game);
    IEnumerable<ICardGame> GetAllGames();
}

// Existing poker registry becomes one implementation
public class PokerGameRegistry : ICardGameRegistry { ... }

// New registry for non-poker games
public class EliminationGameRegistry : ICardGameRegistry { ... }

// Composite registry
public class CompositeGameRegistry : ICardGameRegistry
{
    private readonly IEnumerable<ICardGameRegistry> _registries;
    // Aggregates all game types
}
```

#### Level 3: Outcome Determination Abstraction

```csharp
// Current poker-specific
public interface IHandEvaluator { ... }

// New abstract base
public interface IOutcomeDeterminer
{
    GameOutcome DetermineOutcome(IEnumerable<IPlayerState> players);
}

// Poker implementation
public interface IPokerOutcomeDeterminer : IOutcomeDeterminer
{
    HandBase EvaluateHand(IEnumerable<Card> cards);
}

// Screw Your Neighbor implementation
public class LowestCardLosesDeterminer : IOutcomeDeterminer
{
    public GameOutcome DetermineOutcome(IEnumerable<IPlayerState> players)
    {
        var lowestCard = players
            .Where(p => !p.IsEliminated)
            .OrderBy(p => p.SingleCard.Rank)
            .First();
        
        return new EliminationOutcome(losingPlayer: lowestCard.PlayerId);
    }
}
```

#### Level 4: Currency/Stakes Abstraction

```csharp
// Current: Chips (integer, additive)
public interface IPokerStakes
{
    int ChipStack { get; }
    void AddChips(int amount);
    void RemoveChips(int amount);
}

// New: Lives (integer 0-N, only decrements)
public interface IEliminationStakes
{
    int Lives { get; }
    void LoseLife();
    bool IsEliminated { get; }
}

// Abstract base
public interface IGameStakes
{
    bool CanContinuePlaying { get; }
}
```

#### Level 5: Action System Abstraction

```csharp
// Current poker actions
public enum PokerAction { Check, Bet, Call, Raise, Fold, AllIn, Draw }

// Screw Your Neighbor actions
public enum PassKeepAction { Keep, PassToLeft }

// Abstract action system
public interface IGameAction
{
    string ActionType { get; }
    bool Validate(IGameState state, IPlayerState player);
    IGameState Apply(IGameState state, IPlayerState player);
}
```

### 10.6 Effort Estimation for Non-Poker Support

| Component | Files Affected | Effort | Risk |
|-----------|----------------|--------|------|
| Base interfaces (`ICardGame`, etc.) | 5-10 new files | Medium | Low |
| Registry refactoring | 3-5 files | Medium | Medium |
| Outcome determination abstraction | 10-15 files | High | High |
| Currency/stakes abstraction | 8-12 files | High | High |
| Action system abstraction | 10-15 files | High | High |
| Database schema changes | 5-8 migrations | Very High | Very High |
| Background service refactoring | 1-2 files (major) | Very High | High |
| UI component abstraction | 15-20 files | Very High | High |
| API endpoint abstraction | 10-15 files | High | Medium |
| **Total Estimated** | **67-102 files** | **4-8 weeks** | **High** |

### 10.7 Recommended Approach for Non-Poker Games

#### Option A: Separate Application (Recommended for Now)

Create a separate `CardGames.Elimination` project that shares only:
- Core card models (`Card`, `Deck`, `Suit`, `Symbol`)
- Authentication/User infrastructure
- Basic SignalR infrastructure

**Pros:** No risk to existing poker games, clean architecture
**Cons:** Code duplication, separate deployments

#### Option B: Abstraction Layer (Long-term)

Implement the abstraction layers described above, then migrate poker games to use them.

**Pros:** Unified system, code reuse
**Cons:** High risk, extensive refactoring, long timeline

#### Option C: Hybrid Approach

1. Create `ICardGame` base interface
2. Make `IPokerGame` inherit from it
3. Add `ScrewYourNeighborGame` implementing `ICardGame` directly
4. Use separate feature folders and handlers
5. Share only: Cards, Deck, Users, SignalR

**Pros:** Incremental migration, moderate risk
**Cons:** Some duplication, parallel systems temporarily

---

## Appendix A: Game Type Code Constants (Poker Games)

| Game | Constant | Location |
|------|----------|----------|
| Hold'em | `HOLDEM` | `PokerGameMetadataRegistry.HoldEmCode` |
| Five Card Draw | `FIVECARDDRAW` | `PokerGameMetadataRegistry.FiveCardDrawCode` |
| Twos, Jacks, Man with the Axe | `TWOSJACKSMANWITHTHEAXE` | `PokerGameMetadataRegistry.TwosJacksManWithTheAxeCode` |
| Omaha | `OMAHA` | `PokerGameMetadataRegistry.OmahaCode` |
| Seven Card Stud | `SEVENCARDSTUD` | `PokerGameMetadataRegistry.SevenCardStudCode` |
| Kings and Lows | `KINGSANDLOWS` | `PokerGameMetadataRegistry.KingsAndLowsCode` |
| Follow the Queen | `FOLLOWTHEQUEEN` | `PokerGameMetadataRegistry.FollowTheQueenCode` |

---

## Appendix B: Non-Poker Game Type Considerations

### B.1 "Screw Your Neighbor" (Ranter-Go-Round)

**Would require new constants in a separate registry:**

| Property | Value |
|----------|-------|
| Code | `SCREWYOURNEIGHBOR` |
| Category | `Elimination` |
| CardsPerPlayer | `1` |
| StartingLives | `3` |
| WinCondition | `LastSurvivor` |
| LossCondition | `LowestCard` |
| SpecialCard | `King` (immunity) |

**Cannot use existing `PokerGameMetadataAttribute` because:**
- No hole/board/community cards concept
- No betting structure
- No drawing mechanics
- No wild cards (Kings serve different purpose)
- No showdown in poker sense

### B.2 "Acey-Deucey" (In-Between)

**Would require new constants in a separate registry:**

| Property | Value |
|----------|-------|
| Code | `ACEYDEUCEY` |
| Category | `BettingGame` |
| CardsPerPlayer | `0` (cards dealt to table, not players) |
| CardsPerTurn | `3` (2 face-up spread + 1 reveal) |
| BetTarget | `Pot` |
| WinCondition | `CardInBetween` |
| LossCondition | `CardOutsideOrMatch` |
| SpecialCard | `Ace` (flexible high/low) |
| PenaltyCondition | `Post` (match = 2x bet to pot) |

**Cannot use existing `PokerGameMetadataAttribute` because:**
- No player hands at all (cards dealt to table center)
- No hand evaluation (simple rank comparison: is card C between A and B?)
- Betting is against pot, not other players
- Individual player turns, not parallel betting rounds
- "Posting" penalty mechanic has no poker equivalent
- Ace flexibility (player chooses high/low) is unique per-turn decision
- Game ends when pot is empty, not after fixed hand structure

### B.3 Game Category Classification

Recommended game categorization for future extensibility:

| Category | Examples | Key Mechanics |
|----------|----------|---------------|
| **Poker** (current) | Hold'em, Draw, Stud | Hand rankings, betting, pots |
| **Elimination** | Screw Your Neighbor, Spoons | Lives, last survivor wins |
| **Betting/Pot Games** | Acey-Deucey, Red Dog | Bet against pot, card comparison |
| **Blackjack-style** | Blackjack, Baccarat | Player vs dealer, fixed rules |
| **Matching** | Uno, Crazy Eights | Match suit/rank, special actions |
| **Trick-taking** | Hearts, Spades | Win tricks, avoid/collect points |
| **Set Collection** | Rummy, Go Fish | Collect matching cards |
