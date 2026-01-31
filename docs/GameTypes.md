# Game Type Extensibility Analysis

This document provides a comprehensive analysis of all places in the codebase where code makes decisions based on game types or game phases, identifying areas that would require code changes when introducing a new poker variant.

---

## Executive Summary

The codebase has **partial extensibility** through the `IPokerGame` interface, `PokerGameMetadataAttribute`, and `GameRules` system. However, there are significant areas with **hardcoded game type checks** that violate the Open-Closed Principle. Adding a new game type currently requires modifications in **15+ locations** across multiple projects.

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

---

## Appendix: Game Type Code Constants

| Game | Constant | Location |
|------|----------|----------|
| Hold'em | `HOLDEM` | `PokerGameMetadataRegistry.HoldEmCode` |
| Five Card Draw | `FIVECARDDRAW` | `PokerGameMetadataRegistry.FiveCardDrawCode` |
| Twos, Jacks, Man with the Axe | `TWOSJACKSMANWITHTHEAXE` | `PokerGameMetadataRegistry.TwosJacksManWithTheAxeCode` |
| Omaha | `OMAHA` | `PokerGameMetadataRegistry.OmahaCode` |
| Seven Card Stud | `SEVENCARDSTUD` | `PokerGameMetadataRegistry.SevenCardStudCode` |
| Kings and Lows | `KINGSANDLOWS` | `PokerGameMetadataRegistry.KingsAndLowsCode` |
| Follow the Queen | `FOLLOWTHEQUEEN` | `PokerGameMetadataRegistry.FollowTheQueenCode` |
