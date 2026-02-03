# Game Type Extensibility Analysis (Updated)

This document provides a comprehensive analysis of all places in the codebase where code makes decisions based on game types or game phases, identifying areas that would require code changes when introducing a new poker variant.

---

## Executive Summary

The codebase has **significantly improved extensibility** compared to the original analysis through the introduction of:
- `IGameFlowHandler` interface with `GameFlowHandlerFactory` (assembly scanning)
- `PokerGameMetadataRegistry` with assembly scanning for `IPokerGame` implementations
- `PokerGameRulesRegistry` with assembly scanning for `GameRules`
- `HandEvaluatorFactory` for game-specific hand evaluation

However, there are still **hardcoded game type checks** in several areas that violate the Open-Closed Principle. Adding a new game type currently requires modifications in **8-12 locations** across multiple projects (reduced from 15+ in the original analysis).

> ⚠️ **Critical Limitation:** The current architecture assumes all games are **poker variants**. The `IPokerGame` interface, `PokerGameMetadataAttribute`, poker hand evaluation, chip-based betting, and showdown mechanics are deeply embedded throughout the codebase.

### Risk Rating by Area

| Area | Extensibility | Risk | Files to Modify | Notes |
|------|--------------|------|-----------------|-------|
| Game Metadata Registry | ✅ High | 🟢 | 0 | Assembly scanning with `IPokerGame` + `PokerGameMetadataAttribute` |
| Game Rules Registry | ✅ High | 🟢 | 0 | Assembly scanning via `IPokerGame.GetGameRules()` |
| Game Flow Handlers | ✅ High | 🟢 | 0 (new file only) | Assembly scanning via `IGameFlowHandler` |
| API Endpoint Mapping | ✅ High | 🟢 | 0 | Assembly scanning with `EndpointMapGroupAttribute` |
| Background Service | ✅ High | 🟢 | 0 | Delegates to `IGameFlowHandler` |
| Hand Evaluator Factory | 🟡 Medium | 🟡 | 1 | Manual dictionary registration |
| Table State Builder | 🔴 Low | 🔴 | 1 | Multiple hardcoded game type checks |
| Auto Action Service | 🔴 Low | 🔴 | 1 | Hardcoded phase sets and game type checks |
| Blazor TablePlay | 🔴 Low | 🔴 | 1 | API client injection and boolean helpers |
| Phase Registry | 🟡 Medium | 🟡 | 1 | Hardcoded valid game types HashSet |
| PokerGameMetadataRegistry Helpers | 🟡 Medium | 🟡 | 1 | Static `IsXxx()` methods |
| Feature Command Handlers | N/A | 🟢 | N/A | New files required |

---

## 1. Registry & Metadata Layer

### 1.1 `PokerGameMetadataRegistry.cs` ✅ IMPROVED
**File:** `CardGames.Poker.Api/Games/PokerGameMetadataRegistry.cs`

**Status:** Now uses assembly scanning to discover `IPokerGame` implementations.

```csharp
// Lines 29-52: Assembly scanning with reflection
static PokerGameMetadataRegistry()
{
    var pokerGameInterface = typeof(IPokerGame);
    var assembly = pokerGameInterface.Assembly;

    var gameTypes = assembly.GetTypes()
        .Where(t => t is { IsClass: true, IsAbstract: false } && pokerGameInterface.IsAssignableFrom(t));

    foreach (var gameType in gameTypes)
    {
        var attribute = gameType.GetCustomAttribute<PokerGameMetadataAttribute>(inherit: false);
        if (attribute is not null)
        {
            metadataDict[attribute.Code] = attribute;
            gameTypeDict[attribute.Code] = gameType;
        }
    }
}
```

**Remaining Issue:** Static constants and helper methods still exist for backward compatibility:

```csharp
// Lines 17-24: Constants kept for existing code references
public const string HoldEmCode = "HOLDEM";
public const string FiveCardDrawCode = "FIVECARDDRAW";
public const string TwosJacksManWithTheAxeCode = "TWOSJACKSMANWITHTHEAXE";
public const string OmahaCode = "OMAHA";
public const string SevenCardStudCode = "SEVENCARDSTUD";
public const string KingsAndLowsCode = "KINGSANDLOWS";
public const string FollowTheQueenCode = "FOLLOWTHEQUEEN";

// Lines 115-150: Static helper methods
public static bool IsKingsAndLows(string? gameTypeCode)
public static bool IsSevenCardStud(string? gameTypeCode)
public static bool IsTwosJacksManWithTheAxe(string? gameTypeCode)
public static bool IsFiveCardDraw(string? gameTypeCode)
public static bool IsHoldEm(string? gameTypeCode)
```

**Recommendation:** These helper methods are useful for code clarity but should be used sparingly. Consider a registry-based approach for game type capabilities instead.

---

### 1.2 `PokerGameRulesRegistry.cs` ✅ IMPROVED
**File:** `CardGames.Poker.Api/Games/PokerGameRulesRegistry.cs`

**Status:** Now uses assembly scanning via `IPokerGame.GetGameRules()`.

```csharp
// Lines 21-56: Assembly scanning for game rules
static PokerGameRulesRegistry()
{
    var pokerGameInterface = typeof(IPokerGame);
    var assembly = pokerGameInterface.Assembly;

    var gameTypes = assembly.GetTypes()
        .Where(t => t is { IsClass: true, IsAbstract: false } && pokerGameInterface.IsAssignableFrom(t));

    foreach (var gameType in gameTypes)
    {
        var instance = CreateGameInstance(gameType);
        if (instance is not null)
        {
            var rules = instance.GetGameRules();
            rulesDict[attribute.Code] = rules;
        }
    }
}
```

**Extensibility:** ✅ New games are automatically discovered.

---

### 1.3 `PokerGamePhaseRegistry.cs` 🟡 NEEDS WORK
**File:** `CardGames.Poker.Api/Games/PokerGamePhaseRegistry.cs`

**Issue:** Hardcoded HashSet of valid game types.

```csharp
// Lines 20-30: Manual game type list
private static readonly HashSet<string> ValidGameTypes = new(StringComparer.OrdinalIgnoreCase)
{
    "HOLDEM",
    "FIVECARDDRAW",
    "FOLLOWTHEQUEEN",
    "KINGSANDLOWS",
    "OMAHA",
    "SEVENCARDSTUD",
    "TWOSJACKSMANWITHTHEAXE",
    "BASEBALL"
};
```

**Recommendation:** Use `PokerGameMetadataRegistry.IsRegistered()` instead of a hardcoded HashSet.

---

## 2. Game Flow Handler Layer ✅ NEW ARCHITECTURE

### 2.1 `IGameFlowHandler.cs`
**File:** `CardGames.Poker.Api/GameFlow/IGameFlowHandler.cs`

**Status:** New interface for encapsulating game-specific flow logic.

```csharp
public interface IGameFlowHandler
{
    string GameTypeCode { get; }
    GameRules GetGameRules();
    string GetInitialPhase(Game game);
    string? GetNextPhase(Game game, string currentPhase);
    DealingConfiguration GetDealingConfiguration();
    bool SkipsAnteCollection { get; }
    IReadOnlyList<string> SpecialPhases { get; }
    
    Task OnHandStartingAsync(Game game, CancellationToken cancellationToken = default);
    Task OnHandCompletedAsync(Game game, CancellationToken cancellationToken = default);
    Task DealCardsAsync(context, game, eligiblePlayers, now, cancellationToken);
    
    bool SupportsInlineShowdown { get; }
    Task<ShowdownResult> PerformShowdownAsync(...);
    Task<string> ProcessDrawCompleteAsync(...);
    Task<string> ProcessPostShowdownAsync(...);
    
    bool RequiresChipCoverageCheck { get; }
    ChipCheckConfiguration GetChipCheckConfiguration();
}
```

**Extensibility:** ✅ New games implement this interface.

---

### 2.2 `GameFlowHandlerFactory.cs` ✅ IMPROVED
**File:** `CardGames.Poker.Api/GameFlow/GameFlowHandlerFactory.cs`

**Status:** Uses assembly scanning to discover handlers.

```csharp
// Lines 55-84: Assembly scanning for flow handlers
var handlerInterface = typeof(IGameFlowHandler);
var assembly = Assembly.GetExecutingAssembly();

var handlerTypes = assembly.GetTypes()
    .Where(t => t is { IsClass: true, IsAbstract: false } && handlerInterface.IsAssignableFrom(t));

foreach (var handlerType in handlerTypes)
{
    if (Activator.CreateInstance(handlerType) is IGameFlowHandler handler)
    {
        handlersDict[handler.GameTypeCode] = handler;
    }
}
```

**Extensibility:** ✅ New handlers are automatically discovered.

---

## 3. API Endpoint Layer ✅ IMPROVED

### 3.1 `MapFeatureEndpoints.cs`
**File:** `CardGames.Poker.Api/Features/MapFeatureEndpoints.cs`

**Status:** Now uses assembly scanning with `EndpointMapGroupAttribute`.

```csharp
// Lines 13-28: Assembly scanning for endpoint groups
public static void AddFeatureEndpoints(this IEndpointRouteBuilder app)
{
    var endpointMapGroupTypes = Assembly.GetExecutingAssembly()
        .GetTypes()
        .Where(t => t.GetCustomAttribute<EndpointMapGroupAttribute>() is not null);

    foreach (var mapGroupType in endpointMapGroupTypes)
    {
        var mapMethod = mapGroupType.GetMethod(
            MapEndpointsMethodName,
            BindingFlags.Public | BindingFlags.Static,
            [typeof(IEndpointRouteBuilder)]);

        mapMethod?.Invoke(null, [app]);
    }
}
```

**Extensibility:** ✅ New API map groups are automatically discovered.

---

### 3.2 Game-Specific Feature Folders
**Location:** `CardGames.Poker.Api/Features/Games/{GameType}/`

**Current Games with Feature Folders:**
- `FiveCardDraw/` - Standard draw poker
- `SevenCardStud/` - Stud poker with streets
- `KingsAndLows/` - Draw variant with wild cards and pot matching
- `TwosJacksManWithTheAxe/` - Wild card draw variant
- `Generic/` - Shared handlers (StartHand, PerformShowdown)

**Required for New Game:**
- New folder: `{NewGameType}/`
- `{NewGameType}ApiMapGroup.cs` (with `EndpointMapGroupAttribute`)
- Commands specific to game variant
- Queries specific to game variant

**Note:** The `Generic/` folder provides reusable handlers that can be shared.

---

## 4. Background Services ✅ SIGNIFICANTLY IMPROVED

### 4.1 `ContinuousPlayBackgroundService.cs`
**File:** `CardGames.Poker.Api/Services/ContinuousPlayBackgroundService.cs`

**Status:** Now delegates to `IGameFlowHandler` for game-specific logic.

```csharp
// Lines 287-289: Uses flow handler factory
var flowHandlerFactory = scope.ServiceProvider.GetRequiredService<IGameFlowHandlerFactory>();
var flowHandler = flowHandlerFactory.GetHandler(game.GameType?.Code);

// Lines 341-428: Chip coverage check via flow handler
if (flowHandler.RequiresChipCoverageCheck)
{
    var chipCheckConfig = flowHandler.GetChipCheckConfiguration();
    // ... generic chip check logic
}

// Lines 582-587: Dealing configuration from flow handler
var dealingConfig = flowHandler.GetDealingConfiguration();

// Lines 587: Initial phase from flow handler
game.CurrentPhase = flowHandler.GetInitialPhase(game);

// Lines 596: Game-specific initialization
await flowHandler.OnHandStartingAsync(game, cancellationToken);

// Lines 610-613: Ante collection via flow handler
if (!flowHandler.SkipsAnteCollection)
{
    await CollectAntesAsync(...);
}

// Lines 616: Card dealing via flow handler
await flowHandler.DealCardsAsync(context, game, eligiblePlayers, now, cancellationToken);
```

**DrawComplete Processing (Lines 167-229):**
```csharp
// Uses flow handler instead of hardcoded game type check
var flowHandler = flowHandlerFactory.GetHandler(game.GameType?.Code);
var nextPhase = await flowHandler.ProcessDrawCompleteAsync(
    context, game, handHistoryRecorder, now, cancellationToken);

if (nextPhase == nameof(Phases.Showdown) && flowHandler.SupportsInlineShowdown)
{
    var showdownResult = await flowHandler.PerformShowdownAsync(...);
    var postShowdownPhase = await flowHandler.ProcessPostShowdownAsync(...);
}
```

**Extensibility:** ✅ No hardcoded game type checks in background service.

---

### 4.2 `AutoActionService.cs` 🔴 NEEDS WORK
**File:** `CardGames.Poker.Api/Services/AutoActionService.cs`

**Issue:** Hardcoded phase sets and game type checks.

```csharp
// Lines 23-43: Hardcoded phase sets
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

**Lines 258-287: Hardcoded game type check for draw commands:**
```csharp
if (gameTypeCode.Equals("KINGSANDLOWS", StringComparison.OrdinalIgnoreCase))
{
    var command = new DrawCardsCommand(gameId, playerId, []);
    // Kings and Lows specific command
}
else
{
    // Five Card Draw and other games
    var command = new ProcessDrawCommand(gameId, []);
}
```

**Lines 302-306: Drop/Stay only for Kings and Lows:**
```csharp
if (!gameTypeCode.Equals("KINGSANDLOWS", StringComparison.OrdinalIgnoreCase))
{
    _logger.LogDebug("Drop/Stay phase only applies to Kings and Lows games");
    return;
}
```

**Recommendation:** 
- Use `GameRules.Phases` with `PhaseCategory` to determine valid phases
- Create `IAutoActionHandler` interface per game type, or
- Add auto-action configuration to `IGameFlowHandler`

---

## 5. Table State & UI Support 🔴 NEEDS SIGNIFICANT WORK

### 5.1 `TableStateBuilder.cs`
**File:** `CardGames.Poker.Api/Services/TableStateBuilder.cs`

**CRITICAL ISSUE:** Multiple hardcoded game type checks throughout the file.

#### Lines 66-105: Seven Card Stud Debug Logging
```csharp
var isSevenCardStudGame = PokerGameMetadataRegistry.IsSevenCardStud(game.GameType?.Code);
if (isSevenCardStudGame)
{
    // Special logging for stud card ordering
}
```

#### Lines 296-335: Hand Evaluation Description
```csharp
if (PokerGameMetadataRegistry.IsSevenCardStud(game.GameType?.Code))
{
    // Seven Card Stud specific evaluation
    var studHand = new SevenCardStudHand(initialHoleCards, openCards, downCard);
}
else if (PokerGameMetadataRegistry.IsTwosJacksManWithTheAxe(game.GameType?.Code))
{
    drawHand = new TwosJacksManWithTheAxeDrawHand(playerCards);
}
else if (PokerGameMetadataRegistry.IsKingsAndLows(game.GameType?.Code))
{
    drawHand = new KingsAndLowsDrawHand(playerCards);
}
else
{
    drawHand = new DrawHand(playerCards);
}
```

#### Lines 481-493: Card Ordering for Display
```csharp
var isSevenCardStud = PokerGameMetadataRegistry.IsSevenCardStud(gameTypeCode);
var playerCards = OrderCardsForDisplay(filteredCards, isSevenCardStud).ToList();
```

#### Lines 576-587: Private Hand Building
```csharp
var isSevenCardStud = PokerGameMetadataRegistry.IsSevenCardStud(gameTypeCode);
var orderedCards = OrderCardsForDisplay(filteredCards, isSevenCardStud).ToList();
```

#### Lines 615-624: Available Actions Phases
```csharp
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

#### Lines 708-801: Showdown Evaluation
```csharp
var isTwosJacksAxe = string.Equals(game.GameType?.Code, PokerGameMetadataRegistry.TwosJacksManWithTheAxeCode, ...);
var isSevenCardStud = string.Equals(game.GameType?.Code, PokerGameMetadataRegistry.SevenCardStudCode, ...);
var isKingsAndLows = string.Equals(game.GameType?.Code, PokerGameMetadataRegistry.KingsAndLowsCode, ...);

if (isTwosJacksAxe)
{
    var wildHand = new TwosJacksManWithTheAxeDrawHand(coreCards);
}
else if (isSevenCardStud)
{
    var studHand = new SevenCardStudHand(initialHoleCards, openCards, downCard);
}
else if (isKingsAndLows)
{
    var kingsAndLowsHand = new KingsAndLowsDrawHand(coreCards);
}
else
{
    var drawHand = new DrawHand(coreCards);
}
```

#### Lines 959-1038: Kings and Lows Player vs Deck Scenario
```csharp
if (isKingsAndLows && playerResults.Count == 1)
{
    // Check for deck cards (player-vs-deck scenario)
    // ... Kings and Lows specific deck vs player logic
}
```

#### Lines 1051-1086: Pot Calculation
```csharp
if (PokerGameMetadataRegistry.IsKingsAndLows(game.GameType?.Code))
{
    // Kings and Lows has different pot tracking
    var isWaitingForNextHand = game.CurrentPhase == "Complete" || 
                               game.CurrentPhase == "PotMatching" ||
                               game.IsPausedForChipCheck;
    // ... Kings and Lows specific pot calculation
}
```

**Recommendation:**
- Create `ITableStateStrategy` interface per game type
- Use `IHandEvaluatorFactory` for hand creation
- Add card ordering configuration to `GameRules` or `IGameFlowHandler`
- Move pot calculation logic to flow handlers

---

## 6. Hand Evaluator Factory 🟡 NEEDS MINOR WORK

### 6.1 `HandEvaluatorFactory.cs`
**File:** `CardGames.Poker/Evaluation/HandEvaluatorFactory.cs`

**Issue:** Manual dictionary registration instead of assembly scanning.

```csharp
// Lines 36-43: Hardcoded dictionary
private static readonly FrozenDictionary<string, IHandEvaluator> EvaluatorsByGameCode =
    new Dictionary<string, IHandEvaluator>(StringComparer.OrdinalIgnoreCase)
    {
        [FiveCardDrawCode] = new DrawHandEvaluator(),
        [TwosJacksManWithTheAxeCode] = new TwosJacksManWithTheAxeHandEvaluator(),
        [KingsAndLowsCode] = new KingsAndLowsHandEvaluator(),
        [SevenCardStudCode] = new SevenCardStudHandEvaluator(),
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
```

**Recommendation:** Use assembly scanning with an `IHandEvaluator.GameTypeCode` property or attribute.

---

## 7. Blazor Web Project 🔴 NEEDS WORK

### 7.1 `TablePlay.razor`
**File:** `CardGames.Poker.Web/Components/Pages/TablePlay.razor`

**Issue:** Multiple API client injections and game type boolean helpers.

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

**Improved Patterns (Already Present):**
The component does use improved patterns from `GameRules` via SignalR:

```csharp
// Lines 536-553: Phase category detection from SignalR state
private bool IsBettingPhase =>
    _tableState?.CurrentPhaseCategory?.Equals("Betting", StringComparison.OrdinalIgnoreCase) == true;

// Lines 590-600: Special rules from SignalR state
private bool HasWildCards => _tableState?.SpecialRules?.HasWildCards == true;
private bool HasSevensSplit => _tableState?.SpecialRules?.HasSevensSplit == true;
private bool HasDropOrStay => _tableState?.SpecialRules?.HasDropOrStay == true;
private bool HasPotMatching => _tableState?.SpecialRules?.HasPotMatching == true;
```

**Recommendation:**
- Use a single `IGameApiRouter` that dispatches to the correct API based on game type
- Continue leveraging `GameRules.Phases` and `GameRules.SpecialRules` for UI decisions
- Move game-specific overlays into dynamically loaded components

---

## 8. Domain Layer

### 8.1 `Phases.cs` (Unified Phase Enum)
**File:** `CardGames.Poker/Betting/Phases.cs`

**Status:** Single enum containing phases for ALL game variants.

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

**Note:** This is acceptable for persistence as a unified enum. Game-specific phase sequences are managed via `GameRules.Phases`.

---

## 9. Summary of Required Changes for New Game Type

### 9.1 Adding a New Poker Variant (e.g., "Razz")

To add a new poker variant (e.g., "Razz"), you would need to:

### Required New Files (Game Logic)
1. 📁 Create `CardGames.Poker/Games/Razz/` folder with:
   - `RazzGame.cs` (implements `IPokerGame` with `PokerGameMetadataAttribute`) ✅ Auto-discovered
   - `RazzRules.cs` (creates `GameRules`) ✅ Auto-discovered via `IPokerGame.GetGameRules()`

2. 📁 Create `CardGames.Poker/Hands/StudHands/RazzHand.cs` (if unique hand evaluation)

3. 📁 Create `CardGames.Poker.Api/GameFlow/RazzFlowHandler.cs` (implements `IGameFlowHandler`) ✅ Auto-discovered

### Required New Files (API)
4. 📁 Create `CardGames.Poker.Api/Features/Games/Razz/` folder with:
   - `RazzApiMapGroup.cs` (with `EndpointMapGroupAttribute`) ✅ Auto-discovered
   - Game-specific commands (if any unique phases)

### Modifications Required (Low Extensibility Areas)

5. ✏️ `CardGames.Poker/Evaluation/HandEvaluatorFactory.cs` - Add evaluator registration
   ```csharp
   [RazzCode] = new RazzHandEvaluator(),
   ```

6. ✏️ `CardGames.Poker.Api/Services/TableStateBuilder.cs` - Add hand evaluation logic (if unique)
   ```csharp
   else if (PokerGameMetadataRegistry.IsRazz(game.GameType?.Code))
   {
       // Razz-specific hand evaluation
   }
   ```

7. ✏️ `CardGames.Poker.Api/Services/AutoActionService.cs` - Add phase mappings (if new phases)

8. ✏️ `CardGames.Poker.Web/Components/Pages/TablePlay.razor` - Add API client and overlays (if unique UI)

### Optional Updates
- Add to `PokerGameMetadataRegistry` helper constants (for code clarity)
- Add to `PokerGamePhaseRegistry.ValidGameTypes` HashSet (if using that registry)
- Add new phases to `Phases.cs` enum (if unique phases)

---

## 10. Recommendations for Further Improved Extensibility

### Short-term (Low Risk)
1. ✅ **Done:** Assembly scanning for `PokerGameMetadataRegistry`
2. ✅ **Done:** Assembly scanning for `PokerGameRulesRegistry`
3. ✅ **Done:** Assembly scanning for `GameFlowHandlerFactory`
4. ✅ **Done:** Assembly scanning for `MapFeatureEndpoints`
5. 🔴 **TODO:** Add assembly scanning to `HandEvaluatorFactory`
6. 🔴 **TODO:** Replace `PokerGamePhaseRegistry.ValidGameTypes` HashSet with registry call

### Medium-term (Medium Risk)
1. 🔴 **TODO:** Create `ITableStateStrategy` interface for game-specific table state building
2. 🔴 **TODO:** Create `IAutoActionHandler` interface for game-specific auto-actions
3. 🔴 **TODO:** Add card ordering configuration to `GameRules` or `DealingConfiguration`

### Long-term (Higher Risk)
1. 🔴 **TODO:** Create unified `IGameApiRouter` for Blazor to reduce API client injection proliferation
2. 🔴 **TODO:** Move all phase checks to use `GameRules.Phases` with `PhaseCategory`
3. 🔴 **TODO:** Dynamic component rendering in Blazor based on `GameRules` instead of hardcoded overlays

---

## Appendix A: Game Type Code Constants

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

## Appendix B: Files with Hardcoded Game Type Checks

### High Priority (Most Impacted)
| File | Line Numbers | Game Types | Impact |
|------|--------------|------------|--------|
| `TableStateBuilder.cs` | 66, 296-356, 481-493, 576-587, 708-801, 959-1038, 1051-1086 | All variants | Hand eval, card ordering, pot calc |
| `AutoActionService.cs` | 23-43, 258-287, 302-306 | KINGSANDLOWS | Phase routing, draw commands |
| `TablePlay.razor` | 22-25, 525-527, 226-291, 281 | All variants | API injection, UI overlays |

### Medium Priority
| File | Line Numbers | Game Types | Impact |
|------|--------------|------------|--------|
| `HandEvaluatorFactory.cs` | 36-43 | All variants | Hand evaluator registration |
| `PokerGamePhaseRegistry.cs` | 20-30 | All variants | Valid game types |
| `PokerGameMetadataRegistry.cs` | 17-24, 115-150 | All variants | Constants, helper methods |

### Low Priority (Reference Only)
| File | Line Numbers | Game Types | Impact |
|------|--------------|------------|--------|
| `PhaseDescriptionResolver.cs` | 4-10 | All variants | Imports only |

---

## Appendix C: Extensibility Comparison

| Component | Original Analysis | Current Status | Change |
|-----------|-------------------|----------------|--------|
| PokerGameMetadataRegistry | Manual dictionary | Assembly scanning | ✅ Improved |
| PokerGameRulesRegistry | Manual dictionary | Assembly scanning | ✅ Improved |
| MapFeatureEndpoints | Manual method calls | Assembly scanning | ✅ Improved |
| ContinuousPlayBackgroundService | ~500 lines hardcoded | Delegates to IGameFlowHandler | ✅ Significantly improved |
| HandEvaluatorFactory | Manual dictionary | Manual dictionary | ❌ Same |
| TableStateBuilder | Hardcoded checks | Hardcoded checks | ❌ Same |
| AutoActionService | Hardcoded phase sets | Hardcoded phase sets | ❌ Same |
| TablePlay.razor | Hardcoded API clients | Hardcoded API clients | ❌ Same |

**Overall Improvement:** 4 of 8 major components are now extensible via assembly scanning or strategy patterns.
