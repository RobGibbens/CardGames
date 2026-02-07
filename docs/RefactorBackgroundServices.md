# Refactor Background Services: Implementation Plan

This document provides an in-depth implementation plan for refactoring the background services as outlined in Section 3 of `GameTypes.md`. The goal is to extract game-specific logic from `ContinuousPlayBackgroundService` and `AutoActionService` into strategy classes implementing the `IGameFlowHandler` interface, making the system extensible for new poker variants.

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Current State Analysis](#2-current-state-analysis)
3. [Target Architecture](#3-target-architecture)
4. [Implementation Steps](#4-implementation-steps)
5. [Interface Enhancements](#5-interface-enhancements)
6. [Handler Implementation Details](#6-handler-implementation-details)
7. [Service Refactoring](#7-service-refactoring)
8. [Testing Strategy](#8-testing-strategy)
9. [Migration Guide](#9-migration-guide)
10. [File Change Summary](#10-file-change-summary)

---

## 1. Executive Summary

### Problem Statement

The `ContinuousPlayBackgroundService.cs` (1391 lines) contains extensive hardcoded game type checks that violate the Open-Closed Principle:

| Issue Location | Game Types Referenced | Lines Affected |
|---------------|----------------------|----------------|
| `ProcessDrawCompleteGamesAsync` | Kings and Lows | ~170-236 |
| `PerformKingsAndLowsShowdownAsync` | Kings and Lows | ~241-469 |
| Chip check logic | Kings and Lows | ~582-658 |
| Phase transition logic | Kings and Lows, Seven Card Stud | ~804-845 |
| Post-dealing phase transitions | Kings and Lows | ~976-1000 |
| Street-based dealing | Seven Card Stud | ~1078-1237 |

### Solution Overview

Extend the existing `IGameFlowHandler` interface to include:
- **Showdown evaluation** (`PerformShowdownAsync`)
- **Draw completion handling** (`ProcessDrawCompleteAsync`)
- **Post-showdown actions** (`ProcessPostShowdownAsync`)
- **Chip check handling** (`CheckChipCoverage`, `GetChipCheckConfiguration`)
- **Dealing delegation** (`DealCardsAsync`)

This allows `ContinuousPlayBackgroundService` to delegate game-specific operations to handlers.

---

## 2. Current State Analysis

### 2.1 Existing IGameFlowHandler Interface

**File:** `CardGames.Poker.Api/GameFlow/IGameFlowHandler.cs`

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
}
```

### 2.2 Existing Handler Implementations

| Handler | Game Code | Special Features |
|---------|-----------|-----------------|
| `FiveCardDrawFlowHandler` | FIVECARDDRAW | Linear phases, standard dealing |
| `SevenCardStudFlowHandler` | SEVENCARDSTUD | Street-based dealing, bring-in logic |
| `KingsAndLowsFlowHandler` | KINGSANDLOWS | DropOrStay, PotMatching, PlayerVsDeck |
| `TwosJacksManWithTheAxeFlowHandler` | TWOSJACKSMANWITHTHEAXE | Wild cards, standard draw flow |

### 2.3 Current Hardcoded Game Checks in ContinuousPlayBackgroundService

```csharp
// Line 581+: Kings and Lows chip check
var hasPotMatchingPhase = flowHandler.SpecialPhases.Contains(nameof(Phases.PotMatching), StringComparer.OrdinalIgnoreCase);

// Line 845-852: Dealing pattern type check
if (dealingConfig.PatternType == DealingPatternType.StreetBased)
{
    await DealSevenCardStudHandsAsync(...);  // Hardcoded method
}
else
{
    await DealHandsAsync(...);
}
```

---

## 3. Target Architecture

### 3.1 Enhanced IGameFlowHandler Interface

```
┌───────────────────────────────────────────────────────────────┐
│                    IGameFlowHandler                           │
├───────────────────────────────────────────────────────────────┤
│ Properties:                                                   │
│   - GameTypeCode                                              │
│   - SkipsAnteCollection                                       │
│   - SpecialPhases                                             │
│   - HasPotMatchingPhase (new)                                │
│   - HasPlayerVsDeckPhase (new)                               │
├───────────────────────────────────────────────────────────────┤
│ Phase Management:                                             │
│   - GetGameRules()                                            │
│   - GetInitialPhase(game)                                     │
│   - GetNextPhase(game, currentPhase)                         │
│   - GetDealingConfiguration()                                │
├───────────────────────────────────────────────────────────────┤
│ Hand Lifecycle (existing):                                    │
│   - OnHandStartingAsync(game, ct)                            │
│   - OnHandCompletedAsync(game, ct)                           │
├───────────────────────────────────────────────────────────────┤
│ NEW - Dealing Delegation:                                     │
│   - DealCardsAsync(context, game, players, now, ct)          │
├───────────────────────────────────────────────────────────────┤
│ NEW - Showdown Handling:                                      │
│   - PerformShowdownAsync(context, game, recorder, now, ct)   │
│   - SupportsInlineShowdown { get; }                          │
├───────────────────────────────────────────────────────────────┤
│ NEW - Post-Phase Processing:                                  │
│   - ProcessDrawCompleteAsync(context, game, recorder, ct)    │
│   - ProcessPostShowdownAsync(context, game, ct)              │
├───────────────────────────────────────────────────────────────┤
│ NEW - Chip Check:                                             │
│   - GetChipCheckConfiguration()                              │
│   - RequiresChipCoverageCheck { get; }                       │
└───────────────────────────────────────────────────────────────┘
```

### 3.2 Handler Inheritance Hierarchy

```
BaseGameFlowHandler (abstract)
    ├── FiveCardDrawFlowHandler
    ├── TwosJacksManWithTheAxeFlowHandler
    ├── SevenCardStudFlowHandler (override DealCardsAsync)
    └── KingsAndLowsFlowHandler (override PerformShowdownAsync, ProcessPostShowdownAsync)
```

---

## 4. Implementation Steps

### Step 1: Extend IGameFlowHandler Interface

**File:** `CardGames.Poker.Api/GameFlow/IGameFlowHandler.cs`

Add the following members to the existing interface:

```csharp
/// <summary>
/// Gets whether this game requires chip coverage check before starting new hands.
/// </summary>
/// <remarks>
/// Games like Kings and Lows require all players to be able to cover the pot
/// before a new hand can start. If a player cannot cover, the game pauses
/// for a configurable period to allow chip additions.
/// </remarks>
bool RequiresChipCoverageCheck { get; }

/// <summary>
/// Gets the chip check configuration for this game type.
/// </summary>
/// <returns>Configuration specifying pause duration and behavior.</returns>
ChipCheckConfiguration GetChipCheckConfiguration();

/// <summary>
/// Gets whether this game supports inline showdown processing by the background service.
/// </summary>
/// <remarks>
/// When true, the background service can call <see cref="PerformShowdownAsync"/>
/// directly during phase transitions (e.g., after DrawComplete in Kings and Lows).
/// When false, showdown is handled exclusively by the PerformShowdownCommandHandler.
/// </remarks>
bool SupportsInlineShowdown { get; }

/// <summary>
/// Deals cards to players for a new hand.
/// </summary>
/// <param name="context">The database context.</param>
/// <param name="game">The game entity.</param>
/// <param name="eligiblePlayers">Players to receive cards.</param>
/// <param name="now">The current timestamp.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>A task representing the async operation.</returns>
Task DealCardsAsync(
    CardsDbContext context,
    Game game,
    List<GamePlayer> eligiblePlayers,
    DateTimeOffset now,
    CancellationToken cancellationToken);

/// <summary>
/// Performs showdown evaluation, pot distribution, and state updates.
/// Called by the background service for games with <see cref="SupportsInlineShowdown"/> = true.
/// </summary>
/// <param name="context">The database context.</param>
/// <param name="game">The game entity.</param>
/// <param name="handHistoryRecorder">Service for recording hand history.</param>
/// <param name="now">The current timestamp.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>A result indicating success or failure.</returns>
Task<ShowdownResult> PerformShowdownAsync(
    CardsDbContext context,
    Game game,
    IHandHistoryRecorder handHistoryRecorder,
    DateTimeOffset now,
    CancellationToken cancellationToken);

/// <summary>
/// Processes the DrawComplete phase transition.
/// Called when a draw phase has completed and all players have drawn.
/// </summary>
/// <param name="context">The database context.</param>
/// <param name="game">The game entity.</param>
/// <param name="handHistoryRecorder">Service for recording hand history.</param>
/// <param name="now">The current timestamp.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>The next phase to transition to.</returns>
Task<string> ProcessDrawCompleteAsync(
    CardsDbContext context,
    Game game,
    IHandHistoryRecorder handHistoryRecorder,
    DateTimeOffset now,
    CancellationToken cancellationToken);

/// <summary>
/// Processes any post-showdown actions (e.g., pot matching in Kings and Lows).
/// </summary>
/// <param name="context">The database context.</param>
/// <param name="game">The game entity.</param>
/// <param name="showdownResult">Results from the showdown.</param>
/// <param name="now">The current timestamp.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>The next phase to transition to.</returns>
Task<string> ProcessPostShowdownAsync(
    CardsDbContext context,
    Game game,
    ShowdownResult showdownResult,
    DateTimeOffset now,
    CancellationToken cancellationToken);
```

### Step 2: Create Supporting Types

**File:** `CardGames.Poker.Api/GameFlow/ChipCheckConfiguration.cs` (NEW)

```csharp
namespace CardGames.Poker.Api.GameFlow;

/// <summary>
/// Configuration for chip coverage checking behavior.
/// </summary>
public sealed class ChipCheckConfiguration
{
    /// <summary>
    /// Gets whether chip coverage check is enabled.
    /// </summary>
    public required bool IsEnabled { get; init; }

    /// <summary>
    /// Gets the duration to pause for players to add chips.
    /// </summary>
    public required TimeSpan PauseDuration { get; init; }

    /// <summary>
    /// Gets the action to take when a player cannot cover the pot after the pause expires.
    /// </summary>
    public required ChipShortageAction ShortageAction { get; init; }

    /// <summary>
    /// Default configuration for games without chip check requirements.
    /// </summary>
    public static ChipCheckConfiguration Disabled => new()
    {
        IsEnabled = false,
        PauseDuration = TimeSpan.Zero,
        ShortageAction = ChipShortageAction.None
    };

    /// <summary>
    /// Configuration for Kings and Lows style chip check.
    /// </summary>
    public static ChipCheckConfiguration KingsAndLowsDefault => new()
    {
        IsEnabled = true,
        PauseDuration = TimeSpan.FromMinutes(2),
        ShortageAction = ChipShortageAction.AutoDrop
    };
}

/// <summary>
/// Action to take when a player cannot cover the pot.
/// </summary>
public enum ChipShortageAction
{
    /// <summary>
    /// No action - chip check is disabled.
    /// </summary>
    None,

    /// <summary>
    /// Automatically drop (fold) the player on the next DropOrStay phase.
    /// </summary>
    AutoDrop,

    /// <summary>
    /// Sit the player out from the next hand.
    /// </summary>
    SitOut
}
```

**File:** `CardGames.Poker.Api/GameFlow/ShowdownResult.cs` (NEW)

```csharp
namespace CardGames.Poker.Api.GameFlow;

/// <summary>
/// Result of a showdown operation performed by a game flow handler.
/// </summary>
public sealed class ShowdownResult
{
    /// <summary>
    /// Gets whether the showdown was successful.
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// Gets the winning player IDs (may be multiple for split pots).
    /// </summary>
    public required IReadOnlyList<Guid> WinnerPlayerIds { get; init; }

    /// <summary>
    /// Gets the player IDs of losers (for pot matching games).
    /// </summary>
    public required IReadOnlyList<Guid> LoserPlayerIds { get; init; }

    /// <summary>
    /// Gets the total pot amount that was awarded.
    /// </summary>
    public required int TotalPotAwarded { get; init; }

    /// <summary>
    /// Gets the winning hand description.
    /// </summary>
    public string? WinningHandDescription { get; init; }

    /// <summary>
    /// Gets whether the win was by fold (all other players folded).
    /// </summary>
    public bool WonByFold { get; init; }

    /// <summary>
    /// Gets any error message if the showdown failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates a successful showdown result.
    /// </summary>
    public static ShowdownResult Success(
        IReadOnlyList<Guid> winners,
        IReadOnlyList<Guid> losers,
        int potAwarded,
        string? handDescription = null,
        bool wonByFold = false) => new()
    {
        IsSuccess = true,
        WinnerPlayerIds = winners,
        LoserPlayerIds = losers,
        TotalPotAwarded = potAwarded,
        WinningHandDescription = handDescription,
        WonByFold = wonByFold
    };

    /// <summary>
    /// Creates a failed showdown result.
    /// </summary>
    public static ShowdownResult Failure(string errorMessage) => new()
    {
        IsSuccess = false,
        WinnerPlayerIds = [],
        LoserPlayerIds = [],
        TotalPotAwarded = 0,
        ErrorMessage = errorMessage
    };
}
```

### Step 3: Update BaseGameFlowHandler with Default Implementations

**File:** `CardGames.Poker.Api/GameFlow/BaseGameFlowHandler.cs`

Add default implementations for the new interface members:

```csharp
/// <inheritdoc />
public virtual bool RequiresChipCoverageCheck => false;

/// <inheritdoc />
public virtual ChipCheckConfiguration GetChipCheckConfiguration() => 
    ChipCheckConfiguration.Disabled;

/// <inheritdoc />
public virtual bool SupportsInlineShowdown => false;

/// <inheritdoc />
public virtual Task DealCardsAsync(
    CardsDbContext context,
    Game game,
    List<GamePlayer> eligiblePlayers,
    DateTimeOffset now,
    CancellationToken cancellationToken)
{
    // Default implementation for draw-style games (5 cards face down)
    return DealDrawStyleCardsAsync(context, game, eligiblePlayers, now, cancellationToken);
}

/// <inheritdoc />
public virtual Task<ShowdownResult> PerformShowdownAsync(
    CardsDbContext context,
    Game game,
    IHandHistoryRecorder handHistoryRecorder,
    DateTimeOffset now,
    CancellationToken cancellationToken)
{
    // Default: Use the generic PerformShowdownCommandHandler via MediatR
    // This is a fallback - games with inline showdown should override this
    throw new NotSupportedException(
        $"Inline showdown is not supported for {GameTypeCode}. " +
        $"Use the PerformShowdownCommand handler instead.");
}

/// <inheritdoc />
public virtual Task<string> ProcessDrawCompleteAsync(
    CardsDbContext context,
    Game game,
    IHandHistoryRecorder handHistoryRecorder,
    DateTimeOffset now,
    CancellationToken cancellationToken)
{
    // Default: Transition to second betting round
    return Task.FromResult(nameof(Phases.SecondBettingRound));
}

/// <inheritdoc />
public virtual Task<string> ProcessPostShowdownAsync(
    CardsDbContext context,
    Game game,
    ShowdownResult showdownResult,
    DateTimeOffset now,
    CancellationToken cancellationToken)
{
    // Default: Go directly to Complete phase
    return Task.FromResult(nameof(Phases.Complete));
}

/// <summary>
/// Standard dealing implementation for draw-style games.
/// Deals a fixed number of cards face-down to each player.
/// </summary>
protected async Task DealDrawStyleCardsAsync(
    CardsDbContext context,
    Game game,
    List<GamePlayer> eligiblePlayers,
    DateTimeOffset now,
    CancellationToken cancellationToken)
{
    // Implementation extracted from ContinuousPlayBackgroundService.DealHandsAsync
    // See Section 6.1 for full implementation
}

/// <summary>
/// Helper to find the first active player after the dealer.
/// </summary>
protected static int FindFirstActivePlayerAfterDealer(Game game, List<GamePlayer> activePlayers)
{
    // Implementation extracted from ContinuousPlayBackgroundService
}
```

### Step 4: Implement Game-Specific Handlers

#### 4.1 SevenCardStudFlowHandler Updates

**File:** `CardGames.Poker.Api/GameFlow/SevenCardStudFlowHandler.cs`

Override `DealCardsAsync` to implement street-based dealing:

```csharp
/// <inheritdoc />
public override async Task DealCardsAsync(
    CardsDbContext context,
    Game game,
    List<GamePlayer> eligiblePlayers,
    DateTimeOffset now,
    CancellationToken cancellationToken)
{
    // Implementation extracted from ContinuousPlayBackgroundService.DealSevenCardStudHandsAsync
    // See Section 6.2 for full implementation
}
```

#### 4.2 KingsAndLowsFlowHandler Updates

**File:** `CardGames.Poker.Api/GameFlow/KingsAndLowsFlowHandler.cs`

```csharp
/// <inheritdoc />
public override bool RequiresChipCoverageCheck => true;

/// <inheritdoc />
public override ChipCheckConfiguration GetChipCheckConfiguration() =>
    ChipCheckConfiguration.KingsAndLowsDefault;

/// <inheritdoc />
public override bool SupportsInlineShowdown => true;

/// <inheritdoc />
public override async Task<ShowdownResult> PerformShowdownAsync(
    CardsDbContext context,
    Game game,
    IHandHistoryRecorder handHistoryRecorder,
    DateTimeOffset now,
    CancellationToken cancellationToken)
{
    // Implementation extracted from ContinuousPlayBackgroundService.PerformKingsAndLowsShowdownAsync
    // See Section 6.3 for full implementation
}

/// <inheritdoc />
public override async Task<string> ProcessDrawCompleteAsync(
    CardsDbContext context,
    Game game,
    IHandHistoryRecorder handHistoryRecorder,
    DateTimeOffset now,
    CancellationToken cancellationToken)
{
    // For Kings and Lows, DrawComplete goes directly to Showdown
    // Clear the draw completed timestamp
    game.DrawCompletedAt = null;
    await context.SaveChangesAsync(cancellationToken);
    
    return nameof(Phases.Showdown);
}

/// <inheritdoc />
public override async Task<string> ProcessPostShowdownAsync(
    CardsDbContext context,
    Game game,
    ShowdownResult showdownResult,
    DateTimeOffset now,
    CancellationToken cancellationToken)
{
    // Kings and Lows: Losers must match the pot
    // Implementation extracted from ContinuousPlayBackgroundService
    // See Section 6.4 for full implementation
}
```

### Step 5: Refactor ContinuousPlayBackgroundService

**File:** `CardGames.Poker.Api/Services/ContinuousPlayBackgroundService.cs`

Replace hardcoded game type checks with handler delegation:

```csharp
// BEFORE (lines 168-206):
private async Task ProcessDrawCompleteGamesAsync(...)
{
    // Hardcoded Kings and Lows logic
}

// AFTER:
private async Task ProcessDrawCompleteGamesAsync(
    CardsDbContext context,
    IGameStateBroadcaster broadcaster,
    IHandHistoryRecorder handHistoryRecorder,
    DateTimeOffset now,
    CancellationToken cancellationToken)
{
    var drawCompleteDeadline = now.AddSeconds(-DrawCompleteDisplayDurationSeconds);

    var gamesReadyForTransition = await context.Games
        .Where(g => g.CurrentPhase == nameof(Phases.DrawComplete) &&
                    g.DrawCompletedAt != null &&
                    g.DrawCompletedAt <= drawCompleteDeadline &&
                    g.Status == GameStatus.InProgress)
        .Include(g => g.GamePlayers)
            .ThenInclude(gp => gp.Player)
        .Include(g => g.GameCards)
        .Include(g => g.GameType)
        .ToListAsync(cancellationToken);

    foreach (var game in gamesReadyForTransition)
    {
        try
        {
            // Use flow handler instead of hardcoded game type check
            var flowHandler = _flowHandlerFactory.GetHandler(game.GameType?.Code);
            var nextPhase = await flowHandler.ProcessDrawCompleteAsync(
                context, game, handHistoryRecorder, now, cancellationToken);

            game.CurrentPhase = nextPhase;
            game.UpdatedAt = now;

            // If transitioning to Showdown and handler supports inline showdown
            if (nextPhase == nameof(Phases.Showdown) && flowHandler.SupportsInlineShowdown)
            {
                var showdownResult = await flowHandler.PerformShowdownAsync(
                    context, game, handHistoryRecorder, now, cancellationToken);

                if (showdownResult.IsSuccess)
                {
                    var postShowdownPhase = await flowHandler.ProcessPostShowdownAsync(
                        context, game, showdownResult, now, cancellationToken);

                    game.CurrentPhase = postShowdownPhase;
                }
            }

            await context.SaveChangesAsync(cancellationToken);
            await broadcaster.BroadcastGameStateAsync(game.Id, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process DrawComplete for game {GameId}", game.Id);
        }
    }
}
```

---

## 5. Interface Enhancements

### 5.1 Complete Enhanced IGameFlowHandler Interface

**File:** `CardGames.Poker.Api/GameFlow/IGameFlowHandler.cs`

```csharp
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Games.GameFlow;

namespace CardGames.Poker.Api.GameFlow;

/// <summary>
/// Handles game-specific flow logic for poker variants.
/// Each game variant implements this to customize phase transitions,
/// dealing patterns, showdown behavior, and special mechanics.
/// </summary>
/// <remarks>
/// <para>
/// This interface is part of the Generic Command Handler Architecture that allows
/// poker variants to share common command handlers while encapsulating game-specific
/// logic in strategy implementations.
/// </para>
/// <para>
/// Implementations should be stateless and thread-safe as they may be registered
/// as singletons and used across multiple concurrent games.
/// </para>
/// </remarks>
public interface IGameFlowHandler
{
    #region Identity & Rules

    /// <summary>
    /// Gets the game type code this handler supports.
    /// </summary>
    /// <remarks>
    /// This should match the <see cref="GameType.Code"/> value in the database.
    /// Examples: "FIVECARDDRAW", "SEVENCARDSTUD", "KINGSANDLOWS"
    /// </remarks>
    string GameTypeCode { get; }

    /// <summary>
    /// Gets the game rules for this variant.
    /// </summary>
    /// <returns>The <see cref="GameRules"/> describing this game's flow and mechanics.</returns>
    GameRules GetGameRules();

    #endregion

    #region Phase Management

    /// <summary>
    /// Determines the initial phase after starting a new hand.
    /// </summary>
    /// <param name="game">The game entity.</param>
    /// <returns>The phase name to transition to.</returns>
    /// <remarks>
    /// Most games start with "CollectingAntes", but some variants like Kings and Lows
    /// may start with "Dealing" and handle ante collection differently.
    /// </remarks>
    string GetInitialPhase(Game game);

    /// <summary>
    /// Determines the next phase after the current phase completes.
    /// </summary>
    /// <param name="game">The game entity with current state.</param>
    /// <param name="currentPhase">The current phase name.</param>
    /// <returns>The next phase name, or null if no automatic transition is needed.</returns>
    /// <remarks>
    /// Some phase transitions depend on game state (e.g., number of remaining players).
    /// Return null if the transition should be handled by a command handler explicitly.
    /// </remarks>
    string? GetNextPhase(Game game, string currentPhase);

    /// <summary>
    /// Gets the dealing configuration for this game.
    /// </summary>
    /// <returns>A <see cref="DealingConfiguration"/> describing how cards are dealt.</returns>
    DealingConfiguration GetDealingConfiguration();

    /// <summary>
    /// Determines if the game should skip ante collection phase.
    /// </summary>
    /// <remarks>
    /// Some games (e.g., Kings and Lows) collect antes during a different phase
    /// such as DropOrStay, rather than having a dedicated CollectingAntes phase.
    /// </remarks>
    bool SkipsAnteCollection { get; }

    /// <summary>
    /// Gets phases that are unique to this game variant and require special handling.
    /// </summary>
    /// <remarks>
    /// Examples: "DropOrStay", "PotMatching", "PlayerVsDeck", "BuyCardOffer"
    /// These phases typically have their own command handlers rather than using generic ones.
    /// </remarks>
    IReadOnlyList<string> SpecialPhases { get; }

    #endregion

    #region Hand Lifecycle

    /// <summary>
    /// Performs any game-specific initialization when starting a new hand.
    /// </summary>
    /// <param name="game">The game entity being started.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// Use this to reset game-specific player state, clear variant-specific flags,
    /// or perform any setup required before the first phase begins.
    /// </remarks>
    Task OnHandStartingAsync(Game game, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs any game-specific cleanup when a hand completes.
    /// </summary>
    /// <param name="game">The game entity that just completed.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// Use this to finalize game-specific state, record history,
    /// or prepare for the next hand.
    /// </remarks>
    Task OnHandCompletedAsync(Game game, CancellationToken cancellationToken = default);

    #endregion

    #region Dealing

    /// <summary>
    /// Deals cards to players for a new hand.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="game">The game entity.</param>
    /// <param name="eligiblePlayers">Players to receive cards.</param>
    /// <param name="now">The current timestamp.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    /// <remarks>
    /// Override this in handlers that require non-standard dealing patterns
    /// (e.g., Seven Card Stud with street-based dealing).
    /// </remarks>
    Task DealCardsAsync(
        CardsDbContext context,
        Game game,
        List<GamePlayer> eligiblePlayers,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    #endregion

    #region Showdown

    /// <summary>
    /// Gets whether this game supports inline showdown processing by the background service.
    /// </summary>
    /// <remarks>
    /// When true, the background service can call <see cref="PerformShowdownAsync"/>
    /// directly during phase transitions (e.g., after DrawComplete in Kings and Lows).
    /// When false, showdown is handled exclusively by the PerformShowdownCommandHandler.
    /// </remarks>
    bool SupportsInlineShowdown { get; }

    /// <summary>
    /// Performs showdown evaluation, pot distribution, and state updates.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="game">The game entity.</param>
    /// <param name="handHistoryRecorder">Service for recording hand history.</param>
    /// <param name="now">The current timestamp.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result indicating success or failure.</returns>
    /// <remarks>
    /// Only called by the background service when <see cref="SupportsInlineShowdown"/> is true.
    /// Most games should use the generic PerformShowdownCommandHandler instead.
    /// </remarks>
    Task<ShowdownResult> PerformShowdownAsync(
        CardsDbContext context,
        Game game,
        IHandHistoryRecorder handHistoryRecorder,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    #endregion

    #region Post-Phase Processing

    /// <summary>
    /// Processes the DrawComplete phase transition.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="game">The game entity.</param>
    /// <param name="handHistoryRecorder">Service for recording hand history.</param>
    /// <param name="now">The current timestamp.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The next phase to transition to.</returns>
    /// <remarks>
    /// Called when a draw phase has completed and all players have drawn.
    /// Default behavior transitions to SecondBettingRound.
    /// Override for games with different post-draw behavior.
    /// </remarks>
    Task<string> ProcessDrawCompleteAsync(
        CardsDbContext context,
        Game game,
        IHandHistoryRecorder handHistoryRecorder,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>
    /// Processes any post-showdown actions.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="game">The game entity.</param>
    /// <param name="showdownResult">Results from the showdown.</param>
    /// <param name="now">The current timestamp.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The next phase to transition to.</returns>
    /// <remarks>
    /// Used for games that have additional mechanics after showdown,
    /// such as pot matching in Kings and Lows.
    /// </remarks>
    Task<string> ProcessPostShowdownAsync(
        CardsDbContext context,
        Game game,
        ShowdownResult showdownResult,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    #endregion

    #region Chip Check

    /// <summary>
    /// Gets whether this game requires chip coverage check before starting new hands.
    /// </summary>
    /// <remarks>
    /// Games like Kings and Lows require all players to be able to cover the pot
    /// before a new hand can start. If a player cannot cover, the game pauses
    /// for a configurable period to allow chip additions.
    /// </remarks>
    bool RequiresChipCoverageCheck { get; }

    /// <summary>
    /// Gets the chip check configuration for this game type.
    /// </summary>
    /// <returns>Configuration specifying pause duration and behavior.</returns>
    ChipCheckConfiguration GetChipCheckConfiguration();

    #endregion
}
```

---

## 6. Handler Implementation Details

### 6.1 Draw-Style Dealing (BaseGameFlowHandler)

Extract and adapt from `ContinuousPlayBackgroundService.DealHandsAsync`:

```csharp
protected async Task DealDrawStyleCardsAsync(
    CardsDbContext context,
    Game game,
    List<GamePlayer> eligiblePlayers,
    DateTimeOffset now,
    CancellationToken cancellationToken)
{
    var config = GetDealingConfiguration();
    var cardsPerPlayer = config.InitialCardsPerPlayer;

    // Create a shuffled deck
    var deck = CreateShuffledDeck();

    // Persist all 52 cards with their shuffled order
    var deckCards = new List<GameCard>();
    var deckOrder = 0;
    foreach (var (suit, symbol) in deck)
    {
        var gameCard = new GameCard
        {
            GameId = game.Id,
            GamePlayerId = null,
            HandNumber = game.CurrentHandNumber,
            Suit = suit,
            Symbol = symbol,
            DealOrder = deckOrder++,
            Location = CardLocation.Deck,
            IsVisible = false,
            IsDiscarded = false,
            DealtAt = now
        };
        deckCards.Add(gameCard);
        context.GameCards.Add(gameCard);
    }

    var deckIndex = 0;

    // Sort players starting from left of dealer
    var dealerPosition = game.DealerPosition;
    var maxSeatPosition = game.GamePlayers.Max(gp => gp.SeatPosition);
    var totalSeats = maxSeatPosition + 1;

    var playersInDealOrder = eligiblePlayers
        .OrderBy(p => (p.SeatPosition - dealerPosition - 1 + totalSeats) % totalSeats)
        .ToList();

    // Deal cards to each player
    var dealOrder = 1;
    foreach (var player in playersInDealOrder)
    {
        for (var cardIndex = 0; cardIndex < cardsPerPlayer; cardIndex++)
        {
            if (deckIndex >= deckCards.Count) break;

            var card = deckCards[deckIndex++];
            card.GamePlayerId = player.Id;
            card.Location = CardLocation.Hand;
            card.DealOrder = dealOrder++;
            card.IsVisible = !config.AllFaceDown;
            card.DealtAt = now;
        }
    }

    // Reset CurrentBet for all players
    foreach (var player in game.GamePlayers)
    {
        player.CurrentBet = 0;
    }

    // Determine next phase and set up game state
    var nextPhase = GetNextPhase(game, nameof(Phases.Dealing));

    if (SpecialPhases.Contains(nextPhase ?? "", StringComparer.OrdinalIgnoreCase))
    {
        // Special phase - no betting round setup
        game.CurrentPhase = nextPhase!;
        game.CurrentPlayerIndex = -1;
    }
    else
    {
        // Standard poker - set up first betting round
        var firstActorIndex = FindFirstActivePlayerAfterDealer(game, eligiblePlayers);

        var bettingRound = new BettingRound
        {
            GameId = game.Id,
            HandNumber = game.CurrentHandNumber,
            RoundNumber = 1,
            Street = nextPhase ?? nameof(Phases.FirstBettingRound),
            CurrentBet = 0,
            MinBet = game.MinBet ?? 0,
            RaiseCount = 0,
            MaxRaises = 0,
            LastRaiseAmount = 0,
            PlayersInHand = eligiblePlayers.Count,
            PlayersActed = 0,
            CurrentActorIndex = firstActorIndex,
            LastAggressorIndex = -1,
            IsComplete = false,
            StartedAt = now
        };

        context.Set<BettingRound>().Add(bettingRound);

        game.CurrentPhase = nextPhase ?? nameof(Phases.FirstBettingRound);
        game.CurrentPlayerIndex = firstActorIndex;
    }

    game.UpdatedAt = now;
    await context.SaveChangesAsync(cancellationToken);
}

private static List<(CardSuit, CardSymbol)> CreateShuffledDeck()
{
    var suits = Enum.GetValues<CardSuit>();
    var symbols = Enum.GetValues<CardSymbol>();

    var deck = new List<(CardSuit, CardSymbol)>();
    foreach (var suit in suits)
    {
        foreach (var symbol in symbols)
        {
            deck.Add((suit, symbol));
        }
    }

    // Fisher-Yates shuffle
    var random = Random.Shared;
    for (var i = deck.Count - 1; i > 0; i--)
    {
        var j = random.Next(i + 1);
        (deck[i], deck[j]) = (deck[j], deck[i]);
    }

    return deck;
}
```

### 6.2 Street-Based Dealing (SevenCardStudFlowHandler)

Extract and adapt from `ContinuousPlayBackgroundService.DealSevenCardStudHandsAsync`:

```csharp
public override async Task DealCardsAsync(
    CardsDbContext context,
    Game game,
    List<GamePlayer> eligiblePlayers,
    DateTimeOffset now,
    CancellationToken cancellationToken)
{
    var deck = CreateShuffledDeck();
    var deckCards = new List<GameCard>();
    var deckOrder = 0;

    // Persist entire shuffled deck
    foreach (var (suit, symbol) in deck)
    {
        var gameCard = new GameCard
        {
            GameId = game.Id,
            GamePlayerId = null,
            HandNumber = game.CurrentHandNumber,
            Suit = suit,
            Symbol = symbol,
            DealOrder = deckOrder++,
            Location = CardLocation.Deck,
            IsVisible = false,
            IsDiscarded = false,
            DealtAt = now
        };
        deckCards.Add(gameCard);
        context.GameCards.Add(gameCard);
    }

    var deckIndex = 0;

    // Sort players starting from left of dealer
    var dealerPosition = game.DealerPosition;
    var maxSeatPosition = game.GamePlayers.Max(gp => gp.SeatPosition);
    var totalSeats = maxSeatPosition + 1;

    var playersInDealOrder = eligiblePlayers
        .OrderBy(p => (p.SeatPosition - dealerPosition - 1 + totalSeats) % totalSeats)
        .ToList();

    // Track up cards for bring-in determination
    var playerUpCards = new List<(GamePlayer Player, GameCard UpCard)>();

    // Deal Third Street: 2 hole cards + 1 board card per player
    var dealOrder = 1;
    foreach (var player in playersInDealOrder)
    {
        // 2 hole cards (face down)
        for (var i = 0; i < 2; i++)
        {
            if (deckIndex >= deckCards.Count) break;

            var card = deckCards[deckIndex++];
            card.GamePlayerId = player.Id;
            card.Location = CardLocation.Hole;
            card.DealOrder = dealOrder++;
            card.IsVisible = false;
            card.DealtAtPhase = nameof(Phases.ThirdStreet);
            card.DealtAt = now;
        }

        // 1 board card (face up)
        if (deckIndex >= deckCards.Count) break;

        var boardCard = deckCards[deckIndex++];
        boardCard.GamePlayerId = player.Id;
        boardCard.Location = CardLocation.Board;
        boardCard.DealOrder = dealOrder++;
        boardCard.IsVisible = true;
        boardCard.DealtAtPhase = nameof(Phases.ThirdStreet);
        boardCard.DealtAt = now;

        playerUpCards.Add((player, boardCard));
    }

    // Reset CurrentBet for all players
    foreach (var player in game.GamePlayers)
    {
        player.CurrentBet = 0;
    }

    // Determine bring-in player (lowest up card)
    var bringInPlayer = FindBringInPlayer(playerUpCards);
    var bringInSeatPosition = bringInPlayer?.SeatPosition ??
        playersInDealOrder.FirstOrDefault()?.SeatPosition ?? 0;

    // Post bring-in bet if configured
    var bringIn = game.BringIn ?? 0;
    var currentBet = 0;
    if (bringIn > 0 && bringInPlayer is not null)
    {
        var actualBringIn = Math.Min(bringIn, bringInPlayer.ChipStack);
        bringInPlayer.CurrentBet = actualBringIn;
        bringInPlayer.ChipStack -= actualBringIn;
        bringInPlayer.TotalContributedThisHand += actualBringIn;
        currentBet = actualBringIn;

        // Add to pot
        var pot = await context.Pots
            .FirstOrDefaultAsync(p => p.GameId == game.Id &&
                                      p.HandNumber == game.CurrentHandNumber &&
                                      p.PotType == PotType.Main,
                             cancellationToken);
        if (pot is not null)
        {
            pot.Amount += actualBringIn;
        }
    }

    // Create betting round for Third Street
    var minBet = game.SmallBet ?? game.MinBet ?? 0;
    var bettingRound = new BettingRound
    {
        GameId = game.Id,
        HandNumber = game.CurrentHandNumber,
        RoundNumber = 1,
        Street = nameof(Phases.ThirdStreet),
        CurrentBet = currentBet,
        MinBet = minBet,
        RaiseCount = 0,
        MaxRaises = 0,
        LastRaiseAmount = 0,
        PlayersInHand = eligiblePlayers.Count,
        PlayersActed = 0,
        CurrentActorIndex = bringInSeatPosition,
        LastAggressorIndex = -1,
        IsComplete = false,
        StartedAt = now
    };

    context.Set<BettingRound>().Add(bettingRound);

    // Update game state
    game.CurrentPhase = nameof(Phases.ThirdStreet);
    game.CurrentPlayerIndex = bringInSeatPosition;
    game.BringInPlayerIndex = bringInSeatPosition;
    game.UpdatedAt = now;

    await context.SaveChangesAsync(cancellationToken);
}

private static GamePlayer? FindBringInPlayer(
    List<(GamePlayer Player, GameCard UpCard)> playerUpCards)
{
    if (playerUpCards.Count == 0) return null;

    GamePlayer? lowestPlayer = null;
    GameCard? lowestCard = null;

    foreach (var (player, upCard) in playerUpCards)
    {
        if (lowestCard is null || CompareCardsForBringIn(upCard, lowestCard) < 0)
        {
            lowestCard = upCard;
            lowestPlayer = player;
        }
    }

    return lowestPlayer;
}

private static int CompareCardsForBringIn(GameCard a, GameCard b)
{
    var aValue = GetCardValue(a.Symbol);
    var bValue = GetCardValue(b.Symbol);

    if (aValue != bValue)
    {
        return aValue.CompareTo(bValue);
    }

    // Suit order: Clubs < Diamonds < Hearts < Spades
    return GetSuitRank(a.Suit).CompareTo(GetSuitRank(b.Suit));
}
```

### 6.3 Kings and Lows Showdown

Extract and adapt from `ContinuousPlayBackgroundService.PerformKingsAndLowsShowdownAsync`:

```csharp
public override async Task<ShowdownResult> PerformShowdownAsync(
    CardsDbContext context,
    Game game,
    IHandHistoryRecorder handHistoryRecorder,
    DateTimeOffset now,
    CancellationToken cancellationToken)
{
    var gamePlayersList = game.GamePlayers.OrderBy(gp => gp.SeatPosition).ToList();

    // Find staying players
    var stayingPlayers = gamePlayersList
        .Where(gp => !gp.HasFolded &&
                     gp.Status == GamePlayerStatus.Active &&
                     gp.DropOrStayDecision == DropOrStayDecision.Stay)
        .ToList();

    // Load main pot
    var mainPot = await context.Pots
        .FirstOrDefaultAsync(p => p.GameId == game.Id &&
                                  p.HandNumber == game.CurrentHandNumber,
                         cancellationToken);

    if (mainPot == null)
    {
        // No pot - complete the hand
        return ShowdownResult.Failure("No pot to award");
    }

    // Evaluate hands
    var playerHandEvaluations = new List<(GamePlayer player, long strength)>();

    foreach (var player in stayingPlayers)
    {
        var playerCards = game.GameCards
            .Where(gc => gc.GamePlayerId == player.Id &&
                         gc.HandNumber == game.CurrentHandNumber &&
                         !gc.IsDiscarded)
            .OrderBy(gc => gc.DealOrder)
            .Select(gc => new Card(
                (Suit)(int)gc.Suit,
                (Symbol)(int)gc.Symbol))
            .ToList();

        if (playerCards.Count >= 5)
        {
            var hand = new KingsAndLowsDrawHand(playerCards);
            playerHandEvaluations.Add((player, hand.Strength));
        }
    }

    if (playerHandEvaluations.Count == 0)
    {
        return ShowdownResult.Failure("No valid hands to evaluate");
    }

    // Find winners
    var maxStrength = playerHandEvaluations.Max(h => h.strength);
    var winners = playerHandEvaluations
        .Where(h => h.strength == maxStrength)
        .Select(h => h.player)
        .ToList();
    var losers = stayingPlayers.Where(p => !winners.Contains(p)).ToList();

    // Distribute pot
    var potAmount = mainPot.Amount;
    var sharePerWinner = potAmount / winners.Count;
    var remainder = potAmount % winners.Count;
    var payouts = new List<(Guid playerId, string name, int amount)>();

    foreach (var winner in winners)
    {
        var payout = sharePerWinner;
        if (remainder > 0)
        {
            payout++;
            remainder--;
        }
        winner.ChipStack += payout;
        payouts.Add((winner.PlayerId, winner.Player?.Name ?? "Unknown", payout));
    }

    // Mark pot as awarded
    mainPot.IsAwarded = true;
    mainPot.AwardedAt = now;
    mainPot.WinnerPayouts = JsonSerializer.Serialize(
        payouts.Select(p => new { playerId = p.playerId.ToString(), playerName = p.name, amount = p.amount }));

    await context.SaveChangesAsync(cancellationToken);

    // Build winning hand description
    string? winningHandDescription = null;
    if (winners.Count > 0)
    {
        var winnerPlayer = winners[0];
        var winnerCards = game.GameCards
            .Where(gc => gc.GamePlayerId == winnerPlayer.Id &&
                         gc.HandNumber == game.CurrentHandNumber &&
                         !gc.IsDiscarded)
            .OrderBy(gc => gc.DealOrder)
            .Select(gc => new Card((Suit)(int)gc.Suit, (Symbol)(int)gc.Symbol))
            .ToList();

        if (winnerCards.Count >= 5)
        {
            var winnerHand = new KingsAndLowsDrawHand(winnerCards);
            winningHandDescription = HandDescriptionFormatter.GetHandDescription(winnerHand);
        }
    }

    // Record hand history
    await RecordHandHistoryAsync(
        handHistoryRecorder, game, gamePlayersList, stayingPlayers,
        potAmount, winners, losers, winningHandDescription, now, cancellationToken);

    return ShowdownResult.Success(
        winners.Select(w => w.PlayerId).ToList(),
        losers.Select(l => l.PlayerId).ToList(),
        potAmount,
        winningHandDescription);
}
```

### 6.4 Kings and Lows Post-Showdown (Pot Matching)

```csharp
public override async Task<string> ProcessPostShowdownAsync(
    CardsDbContext context,
    Game game,
    ShowdownResult showdownResult,
    DateTimeOffset now,
    CancellationToken cancellationToken)
{
    // Get loser players
    var losers = game.GamePlayers
        .Where(gp => showdownResult.LoserPlayerIds.Contains(gp.PlayerId))
        .ToList();

    // Pot matching: losers must match the pot
    var matchAmount = showdownResult.TotalPotAwarded;
    var totalMatched = 0;

    foreach (var loser in losers)
    {
        var actualMatch = Math.Min(matchAmount, loser.ChipStack);
        loser.ChipStack -= actualMatch;
        totalMatched += actualMatch;
    }

    // Create pot for next hand
    if (totalMatched > 0)
    {
        var newPot = new Pot
        {
            GameId = game.Id,
            HandNumber = game.CurrentHandNumber + 1,
            PotType = PotType.Main,
            PotOrder = 0,
            Amount = totalMatched,
            IsAwarded = false,
            CreatedAt = now
        };
        context.Pots.Add(newPot);
    }

    // Complete the hand
    game.HandCompletedAt = now;
    game.NextHandStartsAt = now.AddSeconds(
        ContinuousPlayBackgroundService.ResultsDisplayDurationSeconds);

    MoveDealer(game);
    await context.SaveChangesAsync(cancellationToken);

    return nameof(Phases.Complete);
}

private static void MoveDealer(Game game)
{
    var occupiedSeats = game.GamePlayers
        .Where(gp => gp.Status == GamePlayerStatus.Active)
        .OrderBy(gp => gp.SeatPosition)
        .Select(gp => gp.SeatPosition)
        .ToList();

    if (occupiedSeats.Count == 0) return;

    var currentPosition = game.DealerPosition;
    var seatsAfterCurrent = occupiedSeats.Where(pos => pos > currentPosition).ToList();

    game.DealerPosition = seatsAfterCurrent.Count > 0
        ? seatsAfterCurrent.First()
        : occupiedSeats.First();
}
```

---

## 7. Service Refactoring

### 7.1 ContinuousPlayBackgroundService Changes

#### 7.1.1 Add Dependencies

```csharp
public sealed class ContinuousPlayBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ContinuousPlayBackgroundService> _logger;
    private readonly IGameFlowHandlerFactory _flowHandlerFactory;  // ADD
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(1);

    public ContinuousPlayBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<ContinuousPlayBackgroundService> logger,
        IGameFlowHandlerFactory flowHandlerFactory)  // ADD
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _flowHandlerFactory = flowHandlerFactory;  // ADD
    }
```

#### 7.1.2 Refactor StartNextHandAsync

Replace the hardcoded dealing pattern check with handler delegation:

```csharp
// BEFORE (lines 845-852):
if (dealingConfig.PatternType == DealingPatternType.StreetBased)
{
    await DealSevenCardStudHandsAsync(context, game, eligiblePlayers, now, cancellationToken);
}
else
{
    await DealHandsAsync(context, game, eligiblePlayers, flowHandler, now, cancellationToken);
}

// AFTER:
await flowHandler.DealCardsAsync(context, game, eligiblePlayers, now, cancellationToken);
```

#### 7.1.3 Refactor Chip Check Logic

Replace the hardcoded `hasPotMatchingPhase` check:

```csharp
// BEFORE (lines 581-582):
var hasPotMatchingPhase = flowHandler.SpecialPhases.Contains(
    nameof(Phases.PotMatching), StringComparer.OrdinalIgnoreCase);

// AFTER:
if (flowHandler.RequiresChipCoverageCheck)
{
    var chipCheckConfig = flowHandler.GetChipCheckConfiguration();
    // Use chipCheckConfig.PauseDuration, chipCheckConfig.ShortageAction, etc.
}
```

#### 7.1.4 Remove Game-Specific Methods

After handler delegation is complete, remove:

- `DealHandsAsync` (moved to `BaseGameFlowHandler.DealDrawStyleCardsAsync`)
- `DealSevenCardStudHandsAsync` (moved to `SevenCardStudFlowHandler.DealCardsAsync`)
- `PerformKingsAndLowsShowdownAsync` (moved to `KingsAndLowsFlowHandler.PerformShowdownAsync`)
- `FindBringInPlayer`, `CompareCardsForBringIn`, `GetCardValue`, `GetSuitRank` (moved to handlers)
- `CreateShuffledDeck` (moved to shared utility or base handler)
- `FormatCard` (already exists in shared utilities)

### 7.2 AutoActionService Changes

Replace hardcoded phase sets with handler-driven phase categorization:

```csharp
// BEFORE (lines 23-43):
private static readonly HashSet<string> BettingPhases = new(StringComparer.OrdinalIgnoreCase)
{
    "FirstBettingRound",
    "SecondBettingRound"
};

// AFTER:
private bool IsBettingPhase(IGameFlowHandler handler, string phase)
{
    var phaseDescriptor = handler.GetGameRules().Phases
        .FirstOrDefault(p => string.Equals(p.PhaseId, phase, StringComparison.OrdinalIgnoreCase));
    return string.Equals(phaseDescriptor?.Category, "Betting", StringComparison.OrdinalIgnoreCase);
}

// In PerformAutoActionAsync:
var flowHandler = _flowHandlerFactory.GetHandler(gameTypeCode);
if (IsBettingPhase(flowHandler, currentPhase))
{
    await PerformAutoBettingActionAsync(...);
}
else if (flowHandler.SpecialPhases.Contains(currentPhase, StringComparer.OrdinalIgnoreCase))
{
    // Handle special phase auto-actions
    await PerformAutoSpecialPhaseActionAsync(flowHandler, ...);
}
```

---

## 8. Testing Strategy

### 8.1 Unit Tests for New Interfaces

**File:** `Tests/CardGames.Poker.Tests/GameFlow/ChipCheckConfigurationTests.cs`

```csharp
public class ChipCheckConfigurationTests
{
    [Fact]
    public void Disabled_ReturnsCorrectConfiguration()
    {
        var config = ChipCheckConfiguration.Disabled;

        Assert.False(config.IsEnabled);
        Assert.Equal(TimeSpan.Zero, config.PauseDuration);
        Assert.Equal(ChipShortageAction.None, config.ShortageAction);
    }

    [Fact]
    public void KingsAndLowsDefault_ReturnsCorrectConfiguration()
    {
        var config = ChipCheckConfiguration.KingsAndLowsDefault;

        Assert.True(config.IsEnabled);
        Assert.Equal(TimeSpan.FromMinutes(2), config.PauseDuration);
        Assert.Equal(ChipShortageAction.AutoDrop, config.ShortageAction);
    }
}
```

### 8.2 Unit Tests for Handler Implementations

**File:** `Tests/CardGames.Poker.Tests/GameFlow/KingsAndLowsFlowHandlerTests.cs`

```csharp
public class KingsAndLowsFlowHandlerTests
{
    private readonly KingsAndLowsFlowHandler _handler = new();

    [Fact]
    public void RequiresChipCoverageCheck_ReturnsTrue()
    {
        Assert.True(_handler.RequiresChipCoverageCheck);
    }

    [Fact]
    public void SupportsInlineShowdown_ReturnsTrue()
    {
        Assert.True(_handler.SupportsInlineShowdown);
    }

    [Fact]
    public void GetNextPhase_FromDrawComplete_ReturnsShowdown()
    {
        var game = new Game { GamePlayers = [] };
        var nextPhase = _handler.GetNextPhase(game, nameof(Phases.DrawComplete));

        Assert.Equal(nameof(Phases.Showdown), nextPhase);
    }

    [Fact]
    public void GetChipCheckConfiguration_ReturnsKingsAndLowsDefault()
    {
        var config = _handler.GetChipCheckConfiguration();

        Assert.True(config.IsEnabled);
        Assert.Equal(TimeSpan.FromMinutes(2), config.PauseDuration);
    }
}
```

### 8.3 Integration Tests

**File:** `Tests/CardGames.Poker.Tests/Integration/BackgroundServiceRefactorTests.cs`

```csharp
public class BackgroundServiceRefactorTests : IClassFixture<TestDatabaseFixture>
{
    [Fact]
    public async Task SevenCardStud_DealCards_CreatesCorrectCardLayout()
    {
        // Arrange
        var handler = new SevenCardStudFlowHandler();
        var game = CreateTestGame("SEVENCARDSTUD", 4);

        // Act
        await handler.DealCardsAsync(context, game, eligiblePlayers, now, CancellationToken.None);

        // Assert
        var cards = await context.GameCards.Where(c => c.GameId == game.Id).ToListAsync();
        Assert.Equal(12, cards.Count(c => c.Location != CardLocation.Deck)); // 3 cards * 4 players
        Assert.Equal(8, cards.Count(c => c.Location == CardLocation.Hole)); // 2 hole * 4 players
        Assert.Equal(4, cards.Count(c => c.Location == CardLocation.Board)); // 1 up * 4 players
    }

    [Fact]
    public async Task KingsAndLows_PerformShowdown_AwardsPotCorrectly()
    {
        // Arrange
        var handler = new KingsAndLowsFlowHandler();
        var game = CreateKingsAndLowsGameAtShowdown();

        // Act
        var result = await handler.PerformShowdownAsync(
            context, game, handHistoryRecorder, now, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.WinnerPlayerIds);
        Assert.NotNull(result.WinningHandDescription);
    }
}
```

---

## 9. Migration Guide

### 9.1 Phase 1: Add New Interface Members (Non-Breaking)

1. Add new members to `IGameFlowHandler` with `virtual` defaults in `BaseGameFlowHandler`
2. Create `ChipCheckConfiguration` and `ShowdownResult` types
3. Add new properties to existing handlers
4. **No changes to ContinuousPlayBackgroundService yet**

### 9.2 Phase 2: Implement Handler Methods

1. Move dealing logic from service to `BaseGameFlowHandler.DealDrawStyleCardsAsync`
2. Override `DealCardsAsync` in `SevenCardStudFlowHandler`
3. Move showdown logic to `KingsAndLowsFlowHandler.PerformShowdownAsync`
4. Implement `ProcessPostShowdownAsync` for pot matching
5. **Test handlers in isolation**

### 9.3 Phase 3: Refactor Background Service

1. Inject `IGameFlowHandlerFactory` into `ContinuousPlayBackgroundService`
2. Replace `DealHandsAsync` / `DealSevenCardStudHandsAsync` with `flowHandler.DealCardsAsync`
3. Replace `PerformKingsAndLowsShowdownAsync` with handler delegation
4. Replace hardcoded chip check logic with `flowHandler.RequiresChipCoverageCheck`
5. **Run full integration tests**

### 9.4 Phase 4: Cleanup

1. Remove now-unused private methods from `ContinuousPlayBackgroundService`
2. Update AutoActionService to use handler phase categorization
3. Remove hardcoded game type constants where possible
4. Update documentation

### 9.5 Rollback Strategy

If issues arise during migration:
1. Phase 1: No rollback needed (additive changes only)
2. Phase 2: Can coexist with old service code; handlers not called
3. Phase 3: Revert service to call old methods (still present until Phase 4)
4. Phase 4: Requires re-adding removed methods if rollback needed

---

## 10. File Change Summary

### 10.1 New Files

| File | Purpose |
|------|---------|
| `CardGames.Poker.Api/GameFlow/ChipCheckConfiguration.cs` | Configuration type for chip coverage checks |
| `CardGames.Poker.Api/GameFlow/ShowdownResult.cs` | Result type for inline showdown operations |
| `Tests/CardGames.Poker.Tests/GameFlow/ChipCheckConfigurationTests.cs` | Unit tests |
| `Tests/CardGames.Poker.Tests/GameFlow/ShowdownResultTests.cs` | Unit tests |
| `Tests/CardGames.Poker.Tests/GameFlow/KingsAndLowsFlowHandlerShowdownTests.cs` | Handler tests |
| `Tests/CardGames.Poker.Tests/GameFlow/SevenCardStudFlowHandlerDealingTests.cs` | Handler tests |

### 10.2 Modified Files

| File | Changes |
|------|---------|
| `CardGames.Poker.Api/GameFlow/IGameFlowHandler.cs` | Add 8 new interface members |
| `CardGames.Poker.Api/GameFlow/BaseGameFlowHandler.cs` | Add default implementations (~200 lines) |
| `CardGames.Poker.Api/GameFlow/KingsAndLowsFlowHandler.cs` | Override showdown, post-showdown, chip check (~300 lines) |
| `CardGames.Poker.Api/GameFlow/SevenCardStudFlowHandler.cs` | Override DealCardsAsync (~150 lines) |
| `CardGames.Poker.Api/GameFlow/GameFlowHandlerFactory.cs` | Add CardsDbContext to constructors if needed |
| `CardGames.Poker.Api/Services/ContinuousPlayBackgroundService.cs` | Major refactoring (-500 lines game-specific code) |
| `CardGames.Poker.Api/Services/AutoActionService.cs` | Replace hardcoded phase sets (~50 lines) |

### 10.3 Estimated Line Changes

| Category | Lines Added | Lines Removed | Net Change |
|----------|-------------|---------------|------------|
| New types | ~150 | 0 | +150 |
| Interface additions | ~100 | 0 | +100 |
| Base handler defaults | ~250 | 0 | +250 |
| Game-specific handlers | ~500 | 0 | +500 |
| Background service refactor | ~100 | ~600 | -500 |
| AutoActionService refactor | ~30 | ~40 | -10 |
| Tests | ~400 | 0 | +400 |
| **Total** | **~1530** | **~640** | **+890** |

---

## Appendix A: Complete Handler Method Signatures

```csharp
// IGameFlowHandler - Complete interface after enhancement
public interface IGameFlowHandler
{
    // Identity
    string GameTypeCode { get; }
    GameRules GetGameRules();

    // Phase Management
    string GetInitialPhase(Game game);
    string? GetNextPhase(Game game, string currentPhase);
    DealingConfiguration GetDealingConfiguration();
    bool SkipsAnteCollection { get; }
    IReadOnlyList<string> SpecialPhases { get; }

    // Hand Lifecycle
    Task OnHandStartingAsync(Game game, CancellationToken ct = default);
    Task OnHandCompletedAsync(Game game, CancellationToken ct = default);

    // Dealing
    Task DealCardsAsync(CardsDbContext ctx, Game game, List<GamePlayer> players,
                        DateTimeOffset now, CancellationToken ct);

    // Showdown
    bool SupportsInlineShowdown { get; }
    Task<ShowdownResult> PerformShowdownAsync(CardsDbContext ctx, Game game,
                                               IHandHistoryRecorder recorder,
                                               DateTimeOffset now, CancellationToken ct);

    // Post-Phase Processing
    Task<string> ProcessDrawCompleteAsync(CardsDbContext ctx, Game game,
                                           IHandHistoryRecorder recorder,
                                           DateTimeOffset now, CancellationToken ct);
    Task<string> ProcessPostShowdownAsync(CardsDbContext ctx, Game game,
                                           ShowdownResult result,
                                           DateTimeOffset now, CancellationToken ct);

    // Chip Check
    bool RequiresChipCoverageCheck { get; }
    ChipCheckConfiguration GetChipCheckConfiguration();
}
```

---

## Appendix B: Adding a New Game Type Checklist

After this refactoring, adding a new poker variant requires:

1. ✅ Create `{GameName}FlowHandler.cs` inheriting from `BaseGameFlowHandler`
2. ✅ Set `GameTypeCode` property
3. ✅ Implement `GetGameRules()` returning game-specific rules
4. ✅ Override `GetDealingConfiguration()` if non-standard dealing
5. ✅ Override `DealCardsAsync()` if complex dealing (stud games)
6. ✅ Override `GetNextPhase()` for game-specific phase flow
7. ✅ Set `SpecialPhases` if game has unique phases
8. ✅ Override `OnHandStartingAsync()` for pre-hand setup
9. ✅ Set `SupportsInlineShowdown = true` and implement `PerformShowdownAsync()` if needed
10. ✅ Override `ProcessPostShowdownAsync()` for post-showdown mechanics

**No changes required to:**
- `ContinuousPlayBackgroundService`
- `AutoActionService`
- Generic command handlers

---

## Appendix C: Before/After Code Comparison

### Before: Hardcoded Game Type Check

```csharp
// ContinuousPlayBackgroundService.cs (lines 845-852)
var dealingConfig = flowHandler.GetDealingConfiguration();

if (dealingConfig.PatternType == DealingPatternType.StreetBased)
{
    await DealSevenCardStudHandsAsync(context, game, eligiblePlayers, now, cancellationToken);
}
else
{
    await DealHandsAsync(context, game, eligiblePlayers, flowHandler, now, cancellationToken);
}
```

### After: Handler Delegation

```csharp
// ContinuousPlayBackgroundService.cs
await flowHandler.DealCardsAsync(context, game, eligiblePlayers, now, cancellationToken);
```

### Before: Kings and Lows Specific Showdown

```csharp
// ContinuousPlayBackgroundService.cs (lines 173-206)
private async Task ProcessDrawCompleteGamesAsync(...)
{
    // 40+ lines of Kings and Lows specific logic
    await PerformKingsAndLowsShowdownAsync(...);
}

// 200+ lines of PerformKingsAndLowsShowdownAsync implementation
```

### After: Handler Delegation

```csharp
// ContinuousPlayBackgroundService.cs
private async Task ProcessDrawCompleteGamesAsync(...)
{
    var flowHandler = _flowHandlerFactory.GetHandler(game.GameType?.Code);
    var nextPhase = await flowHandler.ProcessDrawCompleteAsync(
        context, game, handHistoryRecorder, now, cancellationToken);

    if (nextPhase == nameof(Phases.Showdown) && flowHandler.SupportsInlineShowdown)
    {
        var result = await flowHandler.PerformShowdownAsync(
            context, game, handHistoryRecorder, now, cancellationToken);
        
        if (result.IsSuccess)
        {
            nextPhase = await flowHandler.ProcessPostShowdownAsync(
                context, game, result, now, cancellationToken);
        }
    }

    game.CurrentPhase = nextPhase;
}
```

---

*Document generated based on codebase analysis. Last updated: 2025*
