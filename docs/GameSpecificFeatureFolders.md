# Generic Command Handlers Implementation Guide

## Feature 2.2: Game-Specific Feature Folders Refactoring

This document provides detailed implementation guidance for refactoring game-specific feature folders to use generic command handlers with GameRules and strategy patterns. Game-specific logic will be encapsulated in domain classes, following the Open-Closed Principle.

> ⚠️ **Scope Limitation:** This document focuses on refactoring **poker variant** feature folders. The proposed architecture assumes all games share poker fundamentals: hand evaluation, betting/pots, showdowns, etc. For non-poker card games (e.g., "Screw Your Neighbor", Blackjack, Uno), see [Section 9: Non-Poker Game Considerations](#9-non-poker-game-considerations) for additional architectural requirements.

---

## ⭐ Backward Compatibility Guarantee

> **This refactoring maintains full backward compatibility with the existing system:**
>
> | Requirement | Guarantee |
> |-------------|-----------|
> | **Frontend unchanged** | All v1 API endpoints remain operational. Frontend requires ZERO modifications. |
> | **Backend parallel development** | New generic handlers (v2) run alongside existing handlers. Enable via feature flags. |
> | **Integration testing** | Backend can be tested independently using Testcontainers and parity tests. See [Section 4A.5](#4a5-backend-integration-testing-strategy). |
> | **Database safety** | All schema changes are additive-only (nullable columns, new tables). No breaking migrations. See [Section 4A.3](#4a3-database-migration-strategy). |
> | **Instant rollback** | Feature flags allow immediate rollback to legacy handlers. See [Section 4A.7](#4a7-rollback-procedures). |
>
> 📋 **Full Details:** [Section 4A: Backward Compatibility Strategy](#4a-backward-compatibility-strategy)

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Current State Analysis](#2-current-state-analysis)
3. [Target Architecture](#3-target-architecture)
4. [Implementation Phases](#4-implementation-phases)
4A. [Backward Compatibility Strategy](#4a-backward-compatibility-strategy) ⭐ **NEW**
5. [Detailed Component Design](#5-detailed-component-design)
6. [Migration Strategy](#6-migration-strategy)
7. [Testing Strategy](#7-testing-strategy)
8. [Risk Assessment](#8-risk-assessment)
9. [Non-Poker Game Considerations](#9-non-poker-game-considerations)

---

## 1. Executive Summary

### Problem Statement

Currently, each poker variant requires its own feature folder with duplicate command handler patterns:

```
CardGames.Poker.Api/Features/Games/
├── FiveCardDraw/
│   └── v1/Commands/
│       ├── StartHand/
│       ├── DealHands/
│       ├── CollectAntes/
│       ├── ProcessBettingAction/
│       ├── ProcessDraw/
│       └── PerformShowdown/
├── SevenCardStud/
│   └── v1/Commands/ (similar structure)
├── KingsAndLows/
│   └── v1/Commands/ (similar structure + DropOrStay, PotMatching)
└── TwosJacksManWithTheAxe/
    └── v1/Commands/ (similar structure)
```

**Key Issues:**
- ~80% duplicate code across game-specific handlers
- Adding a new game requires creating 6-10 new command handler files
- Game type checks scattered throughout `ContinuousPlayBackgroundService.cs` and `TableStateBuilder.cs`
- Violates Open-Closed Principle (OCP)

### Solution Overview

Implement a **Generic Command Handler Architecture** for poker variants where:

1. **Generic Command Handlers** process game-agnostic poker operations (StartHand, CollectAntes, ProcessBettingAction, PerformShowdown)
2. **Game Flow Strategies** (`IGameFlowHandler`) encapsulate game-specific phase transitions
3. **Hand Evaluators** (`IHandEvaluator`) handle game-specific poker hand evaluation (already implemented)
4. **Phase Handlers** (`IPhaseHandler`) handle game-specific phase logic (DropOrStay, PotMatching)
5. **GameRules** metadata drives all routing and behavior decisions

> **Note:** This solution applies to poker variants only. Non-poker games require additional abstractions. See [Section 9](#9-non-poker-game-considerations).

---

## 2. Current State Analysis

### 2.1 Existing Extensibility Infrastructure

The codebase already has significant extensibility infrastructure:

| Component | Location | Purpose | Extensibility |
|-----------|----------|---------|---------------|
| `IPokerGame` | `CardGames.Poker/Games/IPokerGame.cs` | Game interface | ✅ Good |
| `PokerGameMetadataAttribute` | `CardGames.Poker/Games/PokerGameMetadataAttribute.cs` | Game metadata decoration | ✅ Good |
| `PokerGameMetadataRegistry` | `CardGames.Poker.Api/Games/PokerGameMetadataRegistry.cs` | Auto-discovery via reflection | ✅ Good |
| `PokerGameRulesRegistry` | `CardGames.Poker.Api/Games/PokerGameRulesRegistry.cs` | Auto-discovery via reflection | ✅ Good |
| `GameRules` | `CardGames.Poker/Games/GameFlow/GameRules.cs` | Game flow metadata | ✅ Good |
| `IHandEvaluatorFactory` | `CardGames.Poker/Evaluation/IHandEvaluatorFactory.cs` | Hand evaluation routing | ✅ Good |
| `EndpointMapGroupAttribute` | `CardGames.Poker.Api/Features/IEndpointMapGroup.cs` | Auto endpoint registration | ✅ Good |

### 2.2 Current Command Handler Patterns

Examining `FiveCardDraw/v1/Commands/StartHand/StartHandCommandHandler.cs` and `SevenCardStud/v1/Commands/StartHand/StartHandCommandHandler.cs` reveals:

**Common Operations (duplicated across all games):**
1. Load game with players
2. Validate game phase (WaitingToStart or Complete)
3. Process pending leave requests
4. Apply pending chips
5. Auto-sit-out players with insufficient chips
6. Get eligible players
7. Reset player states
8. Remove previous hand's cards

**Game-Specific Operations:**
1. Initial phase transition (most games → CollectingAntes, Kings and Lows → DropOrStay after dealing)
2. Dealing patterns (5 cards vs 7-card stud streets)
3. Showdown evaluation (different hand types)

### 2.3 Hardcoded Game Type Checks (Current)

From `ContinuousPlayBackgroundService.cs`:

```csharp
// Lines 804-806: Multiple hardcoded game type checks
var isKingsAndLows = string.Equals(game.GameType?.Code, "KINGSANDLOWS", StringComparison.OrdinalIgnoreCase);
var isSevenCardStud = string.Equals(game.GameType?.Code, "SEVENCARDSTUD", StringComparison.OrdinalIgnoreCase);

// Lines 811-813: Game-type specific phase transitions
game.CurrentPhase = isKingsAndLows
    ? nameof(Phases.Dealing)
    : nameof(Phases.CollectingAntes);
```

---

## 3. Target Architecture

### 3.1 High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    API Layer (Generic)                          │
├─────────────────────────────────────────────────────────────────┤
│  GenericGamesApiMapGroup.cs                                     │
│    POST /api/games/{gameId}/start-hand                          │
│    POST /api/games/{gameId}/collect-antes                       │
│    POST /api/games/{gameId}/deal                                │
│    POST /api/games/{gameId}/betting-action                      │
│    POST /api/games/{gameId}/showdown                            │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│               MediatR Generic Command Handlers                  │
├─────────────────────────────────────────────────────────────────┤
│  StartHandCommandHandler                                        │
│  CollectAntesCommandHandler                                     │
│  DealHandsCommandHandler                                        │
│  ProcessBettingActionCommandHandler                             │
│  PerformShowdownCommandHandler                                  │
│                                                                 │
│  Each handler uses:                                             │
│    - IGameFlowHandlerFactory → IGameFlowHandler                │
│    - IHandEvaluatorFactory → IHandEvaluator                    │
│    - GameRules (from registry)                                  │
└─────────────────────────────────────────────────────────────────┘
                              │
              ┌───────────────┼───────────────┐
              ▼               ▼               ▼
┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐
│ IGameFlowHandler│ │ IPhaseHandler   │ │ IHandEvaluator  │
│ (Strategy)      │ │ (Per Phase)     │ │ (Evaluation)    │
├─────────────────┤ ├─────────────────┤ ├─────────────────┤
│ FiveCardDraw    │ │ DropOrStay      │ │ DrawHand        │
│ SevenCardStud   │ │ PotMatching     │ │ SevenCardStud   │
│ KingsAndLows    │ │ PlayerVsDeck    │ │ KingsAndLows    │
│ TwosJacksAxe    │ │ BuyCard         │ │ TwosJacksAxe    │
└─────────────────┘ └─────────────────┘ └─────────────────┘
```

### 3.2 New Project Structure

```
CardGames.Poker.Api/
├── Features/
│   └── Games/
│       ├── Generic/                              # NEW: Generic handlers
│       │   ├── GenericGamesApiMapGroup.cs
│       │   └── v1/
│       │       ├── Commands/
│       │       │   ├── StartHand/
│       │       │   │   ├── StartHandCommand.cs
│       │       │   │   ├── StartHandCommandHandler.cs
│       │       │   │   └── StartHandResult.cs
│       │       │   ├── CollectAntes/
│       │       │   ├── DealHands/
│       │       │   ├── ProcessBettingAction/
│       │       │   ├── ProcessDraw/
│       │       │   └── PerformShowdown/
│       │       └── Queries/
│       │           └── GetCurrentPlayerTurn/
│       │
│       ├── PhaseHandlers/                        # NEW: Phase-specific handlers
│       │   ├── IPhaseHandler.cs
│       │   ├── IPhaseHandlerFactory.cs
│       │   ├── PhaseHandlerFactory.cs
│       │   ├── DropOrStay/
│       │   │   ├── DropOrStayPhaseHandler.cs
│       │   │   └── DropOrStayCommand.cs
│       │   ├── PotMatching/
│       │   └── PlayerVsDeck/
│       │
│       └── [Deprecated: FiveCardDraw/, SevenCardStud/, etc.]
│
└── GameFlow/                                     # NEW: Game flow strategies
    ├── IGameFlowHandler.cs
    ├── IGameFlowHandlerFactory.cs
    ├── GameFlowHandlerFactory.cs
    ├── BaseGameFlowHandler.cs
    ├── FiveCardDrawFlowHandler.cs
    ├── SevenCardStudFlowHandler.cs
    ├── KingsAndLowsFlowHandler.cs
    └── TwosJacksManWithTheAxeFlowHandler.cs

CardGames.Poker/
└── Games/
    └── GameFlow/
        ├── GameRules.cs                          # EXISTS
        ├── IGamePhase.cs                         # EXISTS
        ├── PhaseCategory.cs                      # NEW: Enum for phase categories
        └── PhaseTransition.cs                    # NEW: Describes valid transitions
```

---

## 4. Implementation Phases

### Phase 1: Create Core Abstractions (Week 1)

**Goal:** Define interfaces and base classes without breaking existing functionality.

#### Step 1.1: Create IGameFlowHandler Interface

**File:** `CardGames.Poker.Api/GameFlow/IGameFlowHandler.cs`

```csharp
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Games.GameFlow;

namespace CardGames.Poker.Api.GameFlow;

/// <summary>
/// Handles game-specific flow logic for poker variants.
/// Each game variant implements this to customize phase transitions,
/// dealing patterns, and showdown behavior.
/// </summary>
public interface IGameFlowHandler
{
    /// <summary>
    /// Gets the game type code this handler supports.
    /// </summary>
    string GameTypeCode { get; }

    /// <summary>
    /// Gets the game rules for this variant.
    /// </summary>
    GameRules GetGameRules();

    /// <summary>
    /// Determines the initial phase after starting a new hand.
    /// </summary>
    /// <param name="game">The game entity.</param>
    /// <returns>The phase name to transition to.</returns>
    string GetInitialPhase(Game game);

    /// <summary>
    /// Determines the next phase after the current phase completes.
    /// </summary>
    /// <param name="game">The game entity.</param>
    /// <param name="currentPhase">The current phase name.</param>
    /// <returns>The next phase name, or null if no transition is needed.</returns>
    string? GetNextPhase(Game game, string currentPhase);

    /// <summary>
    /// Gets the dealing configuration for this game.
    /// </summary>
    DealingConfiguration GetDealingConfiguration();

    /// <summary>
    /// Determines if the game should skip ante collection (e.g., Kings and Lows collects during DropOrStay).
    /// </summary>
    bool SkipsAnteCollection { get; }

    /// <summary>
    /// Gets phases that are unique to this game variant and require special handling.
    /// </summary>
    IReadOnlyList<string> SpecialPhases { get; }

    /// <summary>
    /// Performs any game-specific initialization when starting a new hand.
    /// </summary>
    Task OnHandStartingAsync(Game game, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs any game-specific cleanup when a hand completes.
    /// </summary>
    Task OnHandCompletedAsync(Game game, CancellationToken cancellationToken = default);
}
```

#### Step 1.2: Create DealingConfiguration

**File:** `CardGames.Poker.Api/GameFlow/DealingConfiguration.cs`

```csharp
namespace CardGames.Poker.Api.GameFlow;

/// <summary>
/// Describes how cards are dealt for a specific poker variant.
/// </summary>
public sealed class DealingConfiguration
{
    /// <summary>
    /// Gets or sets the dealing pattern type.
    /// </summary>
    public required DealingPatternType PatternType { get; init; }

    /// <summary>
    /// Gets or sets the initial cards dealt to each player (for draw games).
    /// </summary>
    public int InitialCardsPerPlayer { get; init; }

    /// <summary>
    /// Gets or sets the dealing rounds for stud games.
    /// </summary>
    public IReadOnlyList<DealingRoundConfig>? DealingRounds { get; init; }

    /// <summary>
    /// Gets or sets whether all cards are dealt face down initially.
    /// </summary>
    public bool AllFaceDown { get; init; } = true;
}

public enum DealingPatternType
{
    /// <summary>
    /// All cards dealt at once (Five Card Draw, Kings and Lows).
    /// </summary>
    AllAtOnce,

    /// <summary>
    /// Cards dealt in rounds with betting between (Seven Card Stud).
    /// </summary>
    StreetBased,

    /// <summary>
    /// Community cards dealt in stages (Hold'em, Omaha).
    /// </summary>
    CommunityCard
}

public sealed class DealingRoundConfig
{
    public required string PhaseName { get; init; }
    public required int HoleCards { get; init; }
    public required int BoardCards { get; init; }
    public required bool HasBettingAfter { get; init; }
}
```

#### Step 1.3: Create IGameFlowHandlerFactory

**File:** `CardGames.Poker.Api/GameFlow/IGameFlowHandlerFactory.cs`

```csharp
namespace CardGames.Poker.Api.GameFlow;

/// <summary>
/// Factory for creating game-specific flow handlers.
/// Uses the game type code to resolve the appropriate handler.
/// </summary>
public interface IGameFlowHandlerFactory
{
    /// <summary>
    /// Gets the flow handler for the specified game type.
    /// </summary>
    /// <param name="gameTypeCode">The game type code (e.g., "FIVECARDDRAW").</param>
    /// <returns>The appropriate game flow handler.</returns>
    IGameFlowHandler GetHandler(string gameTypeCode);

    /// <summary>
    /// Attempts to get a handler for the specified game type.
    /// </summary>
    /// <param name="gameTypeCode">The game type code.</param>
    /// <param name="handler">The handler if found.</param>
    /// <returns>True if a handler was found; otherwise, false.</returns>
    bool TryGetHandler(string gameTypeCode, out IGameFlowHandler? handler);
}
```

#### Step 1.4: Create PhaseCategory Enum

**File:** `CardGames.Poker/Games/GameFlow/PhaseCategory.cs`

```csharp
namespace CardGames.Poker.Games.GameFlow;

/// <summary>
/// Categories of game phases for routing and UI purposes.
/// </summary>
public enum PhaseCategory
{
    /// <summary>
    /// Setup phases (WaitingToStart, WaitingForPlayers).
    /// </summary>
    Setup,

    /// <summary>
    /// Ante/blind collection phases.
    /// </summary>
    Collection,

    /// <summary>
    /// Card dealing phases.
    /// </summary>
    Dealing,

    /// <summary>
    /// Betting phases (FirstBettingRound, ThirdStreet, etc.).
    /// </summary>
    Betting,

    /// <summary>
    /// Drawing/discard phases.
    /// </summary>
    Drawing,

    /// <summary>
    /// Decision phases (DropOrStay).
    /// </summary>
    Decision,

    /// <summary>
    /// Special game-specific phases (PotMatching, PlayerVsDeck, BuyCard).
    /// </summary>
    Special,

    /// <summary>
    /// Resolution phases (Showdown, Complete).
    /// </summary>
    Resolution
}
```

### Phase 2: Implement Base Flow Handler (Week 1-2)

#### Step 2.1: Create BaseGameFlowHandler

**File:** `CardGames.Poker.Api/GameFlow/BaseGameFlowHandler.cs`

```csharp
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Betting;
using CardGames.Poker.Games.GameFlow;

namespace CardGames.Poker.Api.GameFlow;

/// <summary>
/// Base implementation of <see cref="IGameFlowHandler"/> with common poker game logic.
/// Game-specific handlers inherit from this and override specific behaviors.
/// </summary>
public abstract class BaseGameFlowHandler : IGameFlowHandler
{
    public abstract string GameTypeCode { get; }
    
    public abstract GameRules GetGameRules();

    public virtual string GetInitialPhase(Game game)
    {
        // Default: Start with ante collection
        return nameof(Phases.CollectingAntes);
    }

    public virtual string? GetNextPhase(Game game, string currentPhase)
    {
        var rules = GetGameRules();
        var phases = rules.Phases;
        
        var currentIndex = phases
            .Select((p, i) => new { Phase = p, Index = i })
            .FirstOrDefault(x => string.Equals(x.Phase.PhaseId, currentPhase, StringComparison.OrdinalIgnoreCase))
            ?.Index ?? -1;

        if (currentIndex < 0 || currentIndex >= phases.Count - 1)
        {
            return null;
        }

        return phases[currentIndex + 1].PhaseId;
    }

    public abstract DealingConfiguration GetDealingConfiguration();

    public virtual bool SkipsAnteCollection => false;

    public virtual IReadOnlyList<string> SpecialPhases => [];

    public virtual Task OnHandStartingAsync(Game game, CancellationToken cancellationToken = default)
    {
        // Default: No special initialization
        return Task.CompletedTask;
    }

    public virtual Task OnHandCompletedAsync(Game game, CancellationToken cancellationToken = default)
    {
        // Default: No special cleanup
        return Task.CompletedTask;
    }

    /// <summary>
    /// Determines if the specified phase is a betting phase.
    /// </summary>
    protected bool IsBettingPhase(string phase)
    {
        var rules = GetGameRules();
        var phaseDescriptor = rules.Phases
            .FirstOrDefault(p => string.Equals(p.PhaseId, phase, StringComparison.OrdinalIgnoreCase));
        
        return string.Equals(phaseDescriptor?.Category, "Betting", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines if the specified phase is a drawing phase.
    /// </summary>
    protected bool IsDrawingPhase(string phase)
    {
        var rules = GetGameRules();
        var phaseDescriptor = rules.Phases
            .FirstOrDefault(p => string.Equals(p.PhaseId, phase, StringComparison.OrdinalIgnoreCase));
        
        return string.Equals(phaseDescriptor?.Category, "Drawing", StringComparison.OrdinalIgnoreCase);
    }
}
```

#### Step 2.2: Implement FiveCardDrawFlowHandler

**File:** `CardGames.Poker.Api/GameFlow/FiveCardDrawFlowHandler.cs`

```csharp
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Games.FiveCardDraw;
using CardGames.Poker.Games.GameFlow;

namespace CardGames.Poker.Api.GameFlow;

/// <summary>
/// Game flow handler for Five Card Draw poker.
/// </summary>
public sealed class FiveCardDrawFlowHandler : BaseGameFlowHandler
{
    public override string GameTypeCode => "FIVECARDDRAW";

    public override GameRules GetGameRules() => FiveCardDrawRules.CreateGameRules();

    public override DealingConfiguration GetDealingConfiguration()
    {
        return new DealingConfiguration
        {
            PatternType = DealingPatternType.AllAtOnce,
            InitialCardsPerPlayer = 5,
            AllFaceDown = true
        };
    }
}
```

#### Step 2.3: Implement KingsAndLowsFlowHandler

**File:** `CardGames.Poker.Api/GameFlow/KingsAndLowsFlowHandler.cs`

```csharp
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Betting;
using CardGames.Poker.Games.GameFlow;
using CardGames.Poker.Games.KingsAndLows;

namespace CardGames.Poker.Api.GameFlow;

/// <summary>
/// Game flow handler for Kings and Lows poker.
/// </summary>
public sealed class KingsAndLowsFlowHandler : BaseGameFlowHandler
{
    public override string GameTypeCode => "KINGSANDLOWS";

    public override GameRules GetGameRules() => KingsAndLowsRules.CreateGameRules();

    public override string GetInitialPhase(Game game)
    {
        // Kings and Lows: Deal first, then players decide to drop or stay
        return nameof(Phases.Dealing);
    }

    public override string? GetNextPhase(Game game, string currentPhase)
    {
        // Kings and Lows has a unique flow:
        // Dealing → DropOrStay → (if multiple players) DrawPhase → DrawComplete → Showdown
        //                      → (if single player) PlayerVsDeck → Complete
        return currentPhase switch
        {
            nameof(Phases.Dealing) => nameof(Phases.DropOrStay),
            nameof(Phases.DropOrStay) => DeterminePostDropPhase(game),
            nameof(Phases.DrawPhase) => nameof(Phases.DrawComplete),
            nameof(Phases.DrawComplete) => nameof(Phases.Showdown),
            nameof(Phases.PlayerVsDeck) => nameof(Phases.Complete),
            nameof(Phases.PotMatching) => DeterminePostPotMatchingPhase(game),
            nameof(Phases.Showdown) => nameof(Phases.PotMatching),
            _ => base.GetNextPhase(game, currentPhase)
        };
    }

    public override DealingConfiguration GetDealingConfiguration()
    {
        return new DealingConfiguration
        {
            PatternType = DealingPatternType.AllAtOnce,
            InitialCardsPerPlayer = 5,
            AllFaceDown = true
        };
    }

    public override bool SkipsAnteCollection => false;

    public override IReadOnlyList<string> SpecialPhases => 
        [nameof(Phases.DropOrStay), nameof(Phases.PotMatching), nameof(Phases.PlayerVsDeck)];

    public override Task OnHandStartingAsync(Game game, CancellationToken cancellationToken = default)
    {
        // Reset all players' DropOrStay decisions
        foreach (var player in game.GamePlayers)
        {
            player.DropOrStayDecision = DropOrStayDecision.Undecided;
            player.HasMatchedPot = false;
        }
        return Task.CompletedTask;
    }

    private static string DeterminePostDropPhase(Game game)
    {
        var stayingPlayers = game.GamePlayers
            .Count(gp => gp is { Status: GamePlayerStatus.Active, HasFolded: false });

        return stayingPlayers switch
        {
            0 => nameof(Phases.Complete),
            1 => nameof(Phases.PlayerVsDeck),
            _ => nameof(Phases.DrawPhase)
        };
    }

    private static string DeterminePostPotMatchingPhase(Game game)
    {
        // If there are still losers who need to match, stay in PotMatching
        // Otherwise, transition to Complete
        var losersNeedingToMatch = game.GamePlayers
            .Any(gp => gp.IsLoser && !gp.HasMatchedPot);

        return losersNeedingToMatch ? nameof(Phases.PotMatching) : nameof(Phases.Complete);
    }
}
```

#### Step 2.4: Implement SevenCardStudFlowHandler

**File:** `CardGames.Poker.Api/GameFlow/SevenCardStudFlowHandler.cs`

```csharp
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Betting;
using CardGames.Poker.Games.GameFlow;
using CardGames.Poker.Games.SevenCardStud;

namespace CardGames.Poker.Api.GameFlow;

/// <summary>
/// Game flow handler for Seven Card Stud poker.
/// </summary>
public sealed class SevenCardStudFlowHandler : BaseGameFlowHandler
{
    public override string GameTypeCode => "SEVENCARDSTUD";

    public override GameRules GetGameRules() => SevenCardStudRules.CreateGameRules();

    public override DealingConfiguration GetDealingConfiguration()
    {
        return new DealingConfiguration
        {
            PatternType = DealingPatternType.StreetBased,
            DealingRounds = new List<DealingRoundConfig>
            {
                new() { PhaseName = "ThirdStreet", HoleCards = 2, BoardCards = 1, HasBettingAfter = true },
                new() { PhaseName = "FourthStreet", HoleCards = 0, BoardCards = 1, HasBettingAfter = true },
                new() { PhaseName = "FifthStreet", HoleCards = 0, BoardCards = 1, HasBettingAfter = true },
                new() { PhaseName = "SixthStreet", HoleCards = 0, BoardCards = 1, HasBettingAfter = true },
                new() { PhaseName = "SeventhStreet", HoleCards = 1, BoardCards = 0, HasBettingAfter = true }
            }
        };
    }

    public override string? GetNextPhase(Game game, string currentPhase)
    {
        // Seven Card Stud phases: CollectingAntes → ThirdStreet → FourthStreet → ... → SeventhStreet → Showdown
        return currentPhase switch
        {
            nameof(Phases.CollectingAntes) => nameof(Phases.ThirdStreet),
            nameof(Phases.ThirdStreet) => nameof(Phases.FourthStreet),
            nameof(Phases.FourthStreet) => nameof(Phases.FifthStreet),
            nameof(Phases.FifthStreet) => nameof(Phases.SixthStreet),
            nameof(Phases.SixthStreet) => nameof(Phases.SeventhStreet),
            nameof(Phases.SeventhStreet) => nameof(Phases.Showdown),
            nameof(Phases.Showdown) => nameof(Phases.Complete),
            _ => base.GetNextPhase(game, currentPhase)
        };
    }
}
```

#### Step 2.5: Implement GameFlowHandlerFactory

**File:** `CardGames.Poker.Api/GameFlow/GameFlowHandlerFactory.cs`

```csharp
using System.Collections.Frozen;
using System.Reflection;
using CardGames.Poker.Games;

namespace CardGames.Poker.Api.GameFlow;

/// <summary>
/// Factory for creating game-specific flow handlers.
/// Uses assembly scanning to discover all <see cref="IGameFlowHandler"/> implementations.
/// </summary>
public sealed class GameFlowHandlerFactory : IGameFlowHandlerFactory
{
    private readonly FrozenDictionary<string, IGameFlowHandler> _handlers;
    private readonly IGameFlowHandler _defaultHandler;

    public GameFlowHandlerFactory()
    {
        var handlersDict = new Dictionary<string, IGameFlowHandler>(StringComparer.OrdinalIgnoreCase);

        // Discover all IGameFlowHandler implementations via reflection
        var handlerInterface = typeof(IGameFlowHandler);
        var assembly = Assembly.GetExecutingAssembly();

        var handlerTypes = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && handlerInterface.IsAssignableFrom(t));

        foreach (var handlerType in handlerTypes)
        {
            try
            {
                if (Activator.CreateInstance(handlerType) is IGameFlowHandler handler)
                {
                    handlersDict[handler.GameTypeCode] = handler;
                }
            }
            catch
            {
                // Skip handlers that can't be instantiated
            }
        }

        _handlers = handlersDict.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        _defaultHandler = new FiveCardDrawFlowHandler(); // Default fallback
    }

    /// <inheritdoc />
    public IGameFlowHandler GetHandler(string gameTypeCode)
    {
        if (string.IsNullOrWhiteSpace(gameTypeCode))
        {
            return _defaultHandler;
        }

        return _handlers.TryGetValue(gameTypeCode, out var handler) ? handler : _defaultHandler;
    }

    /// <inheritdoc />
    public bool TryGetHandler(string gameTypeCode, out IGameFlowHandler? handler)
    {
        if (string.IsNullOrWhiteSpace(gameTypeCode))
        {
            handler = null;
            return false;
        }

        return _handlers.TryGetValue(gameTypeCode, out handler);
    }
}
```

### Phase 3: Create Generic Command Handlers (Week 2-3)

#### Step 3.1: Create Generic StartHandCommand

**File:** `CardGames.Poker.Api/Features/Games/Generic/v1/Commands/StartHand/StartHandCommand.cs`

```csharp
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.Generic.v1.Commands.StartHand;

/// <summary>
/// Generic command to start a new hand in any poker game.
/// The game type is determined from the game entity.
/// </summary>
/// <param name="GameId">The unique identifier of the game.</param>
public record StartHandCommand(Guid GameId) 
    : IRequest<OneOf<StartHandSuccessful, StartHandError>>, IGameStateChangingCommand;
```

#### Step 3.2: Create Generic StartHandCommandHandler

**File:** `CardGames.Poker.Api/Features/Games/Generic/v1/Commands/StartHand/StartHandCommandHandler.cs`

```csharp
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.GameFlow;
using CardGames.Poker.Betting;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.Generic.v1.Commands.StartHand;

/// <summary>
/// Generic handler for starting a new hand in any poker variant.
/// Uses <see cref="IGameFlowHandlerFactory"/> to route to game-specific logic.
/// </summary>
public sealed class StartHandCommandHandler(
    CardsDbContext context,
    IGameFlowHandlerFactory flowHandlerFactory,
    ILogger<StartHandCommandHandler> logger)
    : IRequestHandler<StartHandCommand, OneOf<StartHandSuccessful, StartHandError>>
{
    public async Task<OneOf<StartHandSuccessful, StartHandError>> Handle(
        StartHandCommand command,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        // 1. Load the game with its players
        var game = await context.Games
            .Include(g => g.GamePlayers)
            .Include(g => g.GameType)
            .FirstOrDefaultAsync(g => g.Id == command.GameId, cancellationToken);

        if (game is null)
        {
            return new StartHandError
            {
                Message = $"Game with ID '{command.GameId}' was not found.",
                Code = StartHandErrorCode.GameNotFound
            };
        }

        // 2. Get the game flow handler for this game type
        var gameTypeCode = game.GameType?.Code ?? "FIVECARDDRAW";
        var flowHandler = flowHandlerFactory.GetHandler(gameTypeCode);

        logger.LogInformation(
            "Starting hand for game {GameId} using {GameType} flow handler",
            game.Id, flowHandler.GameTypeCode);

        // 3. Validate game state allows starting a new hand
        var validPhases = new[] { nameof(Phases.WaitingToStart), nameof(Phases.Complete) };

        if (!validPhases.Contains(game.CurrentPhase))
        {
            return new StartHandError
            {
                Message = $"Cannot start a new hand. Game is in '{game.CurrentPhase}' phase.",
                Code = StartHandErrorCode.InvalidGameState
            };
        }

        // 4. Common operations (same for all game types)
        ProcessPendingLeaveRequests(game, now);
        ApplyPendingChips(game);
        AutoSitOutPlayersWithInsufficientChips(game);

        var eligiblePlayers = GetEligiblePlayers(game);
        if (eligiblePlayers.Count < 2)
        {
            return new StartHandError
            {
                Message = "Not enough eligible players to start a new hand.",
                Code = StartHandErrorCode.NotEnoughPlayers
            };
        }

        ResetPlayerStates(game);
        await RemovePreviousHandCards(game, cancellationToken);

        // 5. Game-specific initialization
        await flowHandler.OnHandStartingAsync(game, cancellationToken);

        // 6. Update game state
        game.CurrentHandNumber++;
        game.HandStartedAt = now;
        game.UpdatedAt = now;
        
        // Use the flow handler to determine the initial phase
        game.CurrentPhase = flowHandler.GetInitialPhase(game);

        await context.SaveChangesAsync(cancellationToken);

        return new StartHandSuccessful
        {
            GameId = game.Id,
            HandNumber = game.CurrentHandNumber,
            InitialPhase = game.CurrentPhase
        };
    }

    private static void ProcessPendingLeaveRequests(Game game, DateTimeOffset now)
    {
        var playersLeaving = game.GamePlayers
            .Where(gp => gp.Status == GamePlayerStatus.Active && gp.LeftAtHandNumber != -1)
            .ToList();

        foreach (var player in playersLeaving)
        {
            player.Status = GamePlayerStatus.Left;
            player.LeftAt = now;
            player.FinalChipCount = player.ChipStack;
            player.IsSittingOut = true;
        }
    }

    private static void ApplyPendingChips(Game game)
    {
        var playersWithPendingChips = game.GamePlayers
            .Where(gp => gp.Status == GamePlayerStatus.Active && gp.PendingChipsToAdd > 0)
            .ToList();

        foreach (var player in playersWithPendingChips)
        {
            player.ChipStack += player.PendingChipsToAdd;
            player.PendingChipsToAdd = 0;
        }
    }

    private static void AutoSitOutPlayersWithInsufficientChips(Game game)
    {
        var ante = game.Ante ?? 0;
        var playersWithInsufficientChips = game.GamePlayers
            .Where(gp => gp.Status == GamePlayerStatus.Active &&
                         !gp.IsSittingOut &&
                         gp.ChipStack < ante)
            .ToList();

        foreach (var player in playersWithInsufficientChips)
        {
            player.IsSittingOut = true;
            player.Status = GamePlayerStatus.SittingOut;
        }
    }

    private static List<GamePlayer> GetEligiblePlayers(Game game)
    {
        var ante = game.Ante ?? 0;
        return game.GamePlayers
            .Where(gp => gp.Status == GamePlayerStatus.Active &&
                         !gp.IsSittingOut &&
                         (ante == 0 || gp.ChipStack >= ante))
            .ToList();
    }

    private static void ResetPlayerStates(Game game)
    {
        foreach (var gamePlayer in game.GamePlayers.Where(gp => gp.Status == GamePlayerStatus.Active))
        {
            gamePlayer.CurrentBet = 0;
            gamePlayer.TotalContributedThisHand = 0;
            gamePlayer.IsAllIn = false;
            gamePlayer.HasDrawnThisRound = false;
            gamePlayer.HasFolded = gamePlayer.IsSittingOut;
        }
    }

    private async Task RemovePreviousHandCards(Game game, CancellationToken cancellationToken)
    {
        var existingCards = await context.GameCards
            .Where(gc => gc.GameId == game.Id)
            .ToListAsync(cancellationToken);

        if (existingCards.Count > 0)
        {
            context.GameCards.RemoveRange(existingCards);
        }
    }
}
```

#### Step 3.3: Create Generic PerformShowdownCommandHandler

**File:** `CardGames.Poker.Api/Features/Games/Generic/v1/Commands/PerformShowdown/PerformShowdownCommandHandler.cs`

```csharp
using CardGames.Core.French.Cards;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.GameFlow;
using CardGames.Poker.Betting;
using CardGames.Poker.Evaluation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.Generic.v1.Commands.PerformShowdown;

/// <summary>
/// Generic handler for performing showdown in any poker variant.
/// Uses <see cref="IHandEvaluatorFactory"/> for game-specific hand evaluation.
/// </summary>
public sealed class PerformShowdownCommandHandler(
    CardsDbContext context,
    IGameFlowHandlerFactory flowHandlerFactory,
    IHandEvaluatorFactory handEvaluatorFactory,
    ILogger<PerformShowdownCommandHandler> logger)
    : IRequestHandler<PerformShowdownCommand, OneOf<PerformShowdownSuccessful, PerformShowdownError>>
{
    public async Task<OneOf<PerformShowdownSuccessful, PerformShowdownError>> Handle(
        PerformShowdownCommand command,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        // 1. Load the game
        var game = await context.Games
            .Include(g => g.GamePlayers)
                .ThenInclude(gp => gp.Player)
            .Include(g => g.GameCards)
            .Include(g => g.GameType)
            .Include(g => g.Pots)
            .FirstOrDefaultAsync(g => g.Id == command.GameId, cancellationToken);

        if (game is null)
        {
            return new PerformShowdownError
            {
                Message = $"Game with ID '{command.GameId}' was not found.",
                Code = PerformShowdownErrorCode.GameNotFound
            };
        }

        // 2. Get handlers
        var gameTypeCode = game.GameType?.Code ?? "FIVECARDDRAW";
        var flowHandler = flowHandlerFactory.GetHandler(gameTypeCode);
        var handEvaluator = handEvaluatorFactory.GetEvaluator(gameTypeCode);

        logger.LogInformation(
            "Performing showdown for game {GameId} using {GameType} evaluator",
            game.Id, gameTypeCode);

        // 3. Get players still in the hand
        var activePlayers = game.GamePlayers
            .Where(gp => gp is { Status: GamePlayerStatus.Active, HasFolded: false })
            .ToList();

        // 4. Evaluate hands using the game-specific evaluator
        var playerHands = new List<(GamePlayer Player, long Strength, string Description)>();

        foreach (var player in activePlayers)
        {
            var cards = GetPlayerCards(game, player, handEvaluator);
            if (cards.Count >= 5)
            {
                var hand = handEvaluator.SupportsPositionalCards
                    ? EvaluatePositionalHand(game, player, handEvaluator)
                    : handEvaluator.CreateHand(cards);

                var description = HandDescriptionFormatter.GetHandDescription(hand);
                playerHands.Add((player, hand.Strength, description));
            }
        }

        // 5. Determine winner(s)
        if (playerHands.Count == 0)
        {
            return new PerformShowdownError
            {
                Message = "No players with valid hands for showdown.",
                Code = PerformShowdownErrorCode.NoValidHands
            };
        }

        var maxStrength = playerHands.Max(ph => ph.Strength);
        var winners = playerHands.Where(ph => ph.Strength == maxStrength).ToList();

        // 6. Distribute pot
        var pot = game.Pots.FirstOrDefault(p => p.HandNumber == game.CurrentHandNumber);
        if (pot != null)
        {
            var winningsPerPlayer = pot.Amount / winners.Count;
            foreach (var (winner, _, _) in winners)
            {
                winner.ChipStack += winningsPerPlayer;
                winner.IsWinner = true;
            }

            // Mark losers
            foreach (var loser in playerHands.Where(ph => ph.Strength < maxStrength))
            {
                loser.Player.IsLoser = true;
            }
        }

        // 7. Transition to next phase
        game.CurrentPhase = flowHandler.GetNextPhase(game, nameof(Phases.Showdown)) 
            ?? nameof(Phases.Complete);
        game.UpdatedAt = now;

        if (game.CurrentPhase == nameof(Phases.Complete))
        {
            game.HandCompletedAt = now;
            await flowHandler.OnHandCompletedAsync(game, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);

        return new PerformShowdownSuccessful
        {
            GameId = game.Id,
            Winners = winners.Select(w => new WinnerInfo
            {
                PlayerId = w.Player.PlayerId,
                PlayerName = w.Player.Player.Name,
                HandDescription = w.Description,
                ChipsWon = pot?.Amount / winners.Count ?? 0
            }).ToList(),
            NextPhase = game.CurrentPhase
        };
    }

    private static List<Card> GetPlayerCards(Game game, GamePlayer player, IHandEvaluator evaluator)
    {
        return game.GameCards
            .Where(gc => gc.GamePlayerId == player.Id && 
                         gc.HandNumber == game.CurrentHandNumber && 
                         !gc.IsDiscarded)
            .OrderBy(gc => gc.DealOrder)
            .Select(gc => new Card((Suit)(int)gc.Suit, (Symbol)(int)gc.Symbol))
            .ToList();
    }

    private Hands.HandBase EvaluatePositionalHand(Game game, GamePlayer player, IHandEvaluator evaluator)
    {
        var allCards = game.GameCards
            .Where(gc => gc.GamePlayerId == player.Id && 
                         gc.HandNumber == game.CurrentHandNumber && 
                         !gc.IsDiscarded)
            .OrderBy(gc => gc.DealOrder)
            .ToList();

        var holeCards = allCards
            .Where(c => c.Location == CardLocation.Hole)
            .Select(c => new Card((Suit)(int)c.Suit, (Symbol)(int)c.Symbol))
            .ToList();

        var boardCards = allCards
            .Where(c => c.Location == CardLocation.Board)
            .Select(c => new Card((Suit)(int)c.Suit, (Symbol)(int)c.Symbol))
            .ToList();

        var downCards = allCards
            .Where(c => c.Location == CardLocation.Hole)
            .Skip(2) // First 2 are initial hole cards
            .Select(c => new Card((Suit)(int)c.Suit, (Symbol)(int)c.Symbol))
            .ToList();

        return evaluator.CreateHand(holeCards.Take(2).ToList(), boardCards, downCards);
    }
}
```

### Phase 4: Create Phase Handlers (Week 3-4)

#### Step 4.1: Create IPhaseHandler Interface

**File:** `CardGames.Poker.Api/Features/Games/PhaseHandlers/IPhaseHandler.cs`

```csharp
using CardGames.Poker.Api.Data.Entities;
using OneOf;
using OneOf.Types;

namespace CardGames.Poker.Api.Features.Games.PhaseHandlers;

/// <summary>
/// Handles game-specific phase logic.
/// Each special phase (DropOrStay, PotMatching, BuyCard) has its own handler.
/// </summary>
public interface IPhaseHandler
{
    /// <summary>
    /// Gets the phase ID this handler supports.
    /// </summary>
    string PhaseId { get; }

    /// <summary>
    /// Gets the game type codes this handler applies to.
    /// Empty means it applies to all games with this phase.
    /// </summary>
    IReadOnlyList<string> ApplicableGameTypes { get; }

    /// <summary>
    /// Processes a player action during this phase.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TSuccess">The success result type.</typeparam>
    /// <typeparam name="TError">The error result type.</typeparam>
    Task<OneOf<TSuccess, TError>> ProcessActionAsync<TRequest, TSuccess, TError>(
        Game game,
        GamePlayer player,
        TRequest request,
        CancellationToken cancellationToken = default)
        where TSuccess : class
        where TError : class;

    /// <summary>
    /// Determines if all players have completed their actions for this phase.
    /// </summary>
    bool IsPhaseComplete(Game game);

    /// <summary>
    /// Gets the next phase after this phase completes.
    /// </summary>
    string GetNextPhase(Game game);
}
```

#### Step 4.2: Create DropOrStayPhaseHandler

**File:** `CardGames.Poker.Api/Features/Games/PhaseHandlers/DropOrStay/DropOrStayPhaseHandler.cs`

```csharp
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Betting;

namespace CardGames.Poker.Api.Features.Games.PhaseHandlers.DropOrStay;

/// <summary>
/// Handles the Drop or Stay phase for Kings and Lows.
/// </summary>
public sealed class DropOrStayPhaseHandler : IPhaseHandler
{
    public string PhaseId => nameof(Phases.DropOrStay);

    public IReadOnlyList<string> ApplicableGameTypes => ["KINGSANDLOWS"];

    public Task<OneOf.OneOf<TSuccess, TError>> ProcessActionAsync<TRequest, TSuccess, TError>(
        Game game,
        GamePlayer player,
        TRequest request,
        CancellationToken cancellationToken = default)
        where TSuccess : class
        where TError : class
    {
        // Implementation delegated to existing DropOrStayCommandHandler logic
        throw new NotImplementedException("Use DropOrStayCommand for now");
    }

    public bool IsPhaseComplete(Game game)
    {
        var activePlayers = game.GamePlayers
            .Where(gp => gp is { Status: GamePlayerStatus.Active, IsSittingOut: false, HasFolded: false })
            .ToList();

        return activePlayers.All(p => 
            p.DropOrStayDecision.HasValue && 
            p.DropOrStayDecision.Value != DropOrStayDecision.Undecided);
    }

    public string GetNextPhase(Game game)
    {
        var stayingPlayers = game.GamePlayers
            .Count(gp => gp is { Status: GamePlayerStatus.Active, HasFolded: false });

        return stayingPlayers switch
        {
            0 => nameof(Phases.Complete),
            1 => nameof(Phases.PlayerVsDeck),
            _ => nameof(Phases.DrawPhase)
        };
    }
}
```

### Phase 5: Refactor ContinuousPlayBackgroundService (Week 4-5)

#### Step 5.1: Extract Game Type Logic to Flow Handlers

Replace hardcoded checks like:

```csharp
// BEFORE (hardcoded)
var isKingsAndLows = string.Equals(game.GameType?.Code, "KINGSANDLOWS", StringComparison.OrdinalIgnoreCase);
if (isKingsAndLows)
{
    game.CurrentPhase = nameof(Phases.Dealing);
}
else
{
    game.CurrentPhase = nameof(Phases.CollectingAntes);
}
```

With:

```csharp
// AFTER (using flow handler)
var flowHandler = _flowHandlerFactory.GetHandler(game.GameType?.Code);
game.CurrentPhase = flowHandler.GetInitialPhase(game);
```

#### Step 5.2: Inject IGameFlowHandlerFactory

```csharp
public sealed class ContinuousPlayBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ContinuousPlayBackgroundService> _logger;

    // Dependencies are resolved per-scope since this is a singleton background service
    
    private async Task StartNextHandAsync(...)
    {
        // Get flow handler from DI
        var flowHandlerFactory = scope.ServiceProvider
            .GetRequiredService<IGameFlowHandlerFactory>();
        var flowHandler = flowHandlerFactory.GetHandler(game.GameType?.Code);
        
        // Use handler for phase transitions
        game.CurrentPhase = flowHandler.GetInitialPhase(game);
        
        // Call game-specific initialization
        await flowHandler.OnHandStartingAsync(game, cancellationToken);
    }
}
```

### Phase 6: Register Services (Week 5)

#### Step 6.1: Add DI Registration

**File:** `CardGames.Poker.Api/Program.cs` (additions)

```csharp
// Add game flow handlers
builder.Services.AddSingleton<IGameFlowHandlerFactory, GameFlowHandlerFactory>();
builder.Services.AddSingleton<IHandEvaluatorFactory, HandEvaluatorFactory>();

// Add phase handlers
builder.Services.AddTransient<IPhaseHandler, DropOrStayPhaseHandler>();
builder.Services.AddTransient<IPhaseHandler, PotMatchingPhaseHandler>();
builder.Services.AddTransient<IPhaseHandler, PlayerVsDeckPhaseHandler>();

// Register phase handler factory
builder.Services.AddSingleton<IPhaseHandlerFactory>(sp =>
{
    var handlers = sp.GetServices<IPhaseHandler>();
    return new PhaseHandlerFactory(handlers);
});
```

---

## 4A. Backward Compatibility Strategy

> **Critical Requirement:** The current backend system MUST remain fully functional during the entire refactoring process. The frontend remains unchanged and continues to work with existing endpoints. Integration tests validate backend behavior independently.

### 4A.1 Parallel Running Architecture

The new generic handlers will run **alongside** (not replace) existing handlers until fully validated:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           API Layer (Routing)                               │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  Existing Endpoints (Keep Working)          New Endpoints (Add in Parallel) │
│  ─────────────────────────────────          ────────────────────────────────│
│  POST /api/v1/games/five-card-draw/         POST /api/v2/games/{id}/        │
│       {gameId}/start-hand                        start-hand                 │
│  POST /api/v1/games/kings-and-lows/         POST /api/v2/games/{id}/        │
│       {gameId}/start-hand                        collect-antes              │
│  POST /api/v1/games/seven-card-stud/        POST /api/v2/games/{id}/        │
│       {gameId}/start-hand                        betting-action             │
│                                                                             │
│  ▲ Frontend uses these                      ▲ Backend tests use these       │
│                                                                            │
└─────────────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                     Shared Database (No Schema Changes)                     │
│                                                                             │
│  Games, GamePlayers, GameCards, Pots, etc. - Same tables, same columns     │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

**Key Principles:**

1. **No Endpoint Removal** - Existing v1 game-specific endpoints stay operational
2. **New v2 Generic Endpoints** - Added in parallel at different paths
3. **Shared Database** - Both handler systems read/write to same tables
4. **No Breaking Schema Changes** - Any new columns must be nullable with defaults

### 4A.2 API Versioning Strategy

Use ASP.NET API versioning to maintain backward compatibility:

```csharp
// Program.cs - Add API versioning support
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("X-Api-Version")
    );
});
```

#### Endpoint Coexistence Example

```csharp
// EXISTING: Game-specific endpoints (v1) - DO NOT MODIFY
[EndpointMapGroup]
public static class FiveCardDrawApiMapGroup  // Unchanged
{
    public static void MapEndpoints(IEndpointRouteBuilder app)
    {
        var games = app.NewVersionedApi("FiveCardDraw");
        var v1 = games.MapGroup("/api/v1/games/five-card-draw/{gameId:guid}")
            .HasApiVersion(1.0);
        
        v1.MapPost("/start-hand", StartHandAsync);  // Frontend uses this
        // ... other endpoints unchanged
    }
}

// NEW: Generic endpoints (v2) - Add alongside
[EndpointMapGroup]
public static class GenericGamesApiMapGroup  // New file
{
    public static void MapEndpoints(IEndpointRouteBuilder app)
    {
        var games = app.NewVersionedApi("Games");
        var v2 = games.MapGroup("/api/v2/games/{gameId:guid}")
            .HasApiVersion(2.0);
        
        v2.MapPost("/start-hand", GenericStartHandAsync);  // Tests use this
        v2.MapPost("/collect-antes", GenericCollectAntesAsync);
        v2.MapPost("/betting-action", GenericProcessBettingActionAsync);
        // ... other generic endpoints
    }
}
```

**Frontend Impact: ZERO** - Frontend continues using `/api/v1/games/five-card-draw/{gameId}/...` endpoints.

### 4A.3 Database Migration Strategy

#### Rule 1: Additive-Only Schema Changes

All schema changes during the refactoring MUST be backward compatible:

| Change Type | Allowed? | Example |
|-------------|----------|---------|
| Add nullable column | ✅ Yes | `ALTER TABLE Games ADD UseGenericHandlers BIT NULL` |
| Add column with default | ✅ Yes | `ALTER TABLE GamePlayers ADD FlowHandlerVersion INT NOT NULL DEFAULT 1` |
| Add new table | ✅ Yes | `CREATE TABLE GameFlowAuditLog (...)` |
| Drop column | ❌ No | (Wait until v1 deprecated) |
| Rename column | ❌ No | (Wait until v1 deprecated) |
| Change column type | ❌ No | (Wait until v1 deprecated) |
| Remove table | ❌ No | (Wait until v1 deprecated) |

#### Rule 2: Feature Flag Columns

If the generic handlers need new state, add it as optional:

```csharp
// Entity change - nullable field
public class Game 
{
    // Existing fields unchanged...
    
    /// <summary>
    /// When non-null, indicates this game uses generic flow handlers.
    /// Null = legacy handlers, value = handler version used.
    /// </summary>
    public int? GenericHandlerVersion { get; set; }  // NEW - nullable
}

// Migration - always Up and Down safe
public partial class AddGenericHandlerVersion : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "GenericHandlerVersion",
            table: "Games",
            type: "int",
            nullable: true);  // NULLABLE - legacy games have NULL
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "GenericHandlerVersion",
            table: "Games");
    }
}
```

#### Rule 3: Dual-Write Pattern for New State

When new handlers need to track additional state, write to BOTH old and new locations:

```csharp
// Generic handler writes to both old (VariantState) and new locations
public async Task OnHandStartingAsync(Game game, CancellationToken ct)
{
    // Write to existing VariantState (legacy handlers read this)
    var variantState = new KingsAndLowsVariantState
    {
        DropOrStayDeadline = DateTimeOffset.UtcNow.AddSeconds(30)
    };
    game.GameSettings = JsonSerializer.Serialize(variantState);
    
    // Also set individual fields if needed (new handlers use these)
    foreach (var player in game.GamePlayers)
    {
        player.DropOrStayDecision = DropOrStayDecision.Undecided;
    }
}
```

### 4A.4 Feature Flags for Gradual Rollout

Implement a feature flag system to control which handler system is used:

```csharp
// Configuration-based feature flags
public class GameHandlerFeatureFlags
{
    /// <summary>
    /// When true, new games will use generic handlers.
    /// Existing games continue with their original handler type.
    /// </summary>
    public bool UseGenericHandlersForNewGames { get; set; } = false;
    
    /// <summary>
    /// List of game type codes that should use generic handlers.
    /// Empty = all games use legacy, "*" = all games use generic.
    /// </summary>
    public List<string> GenericHandlerGameTypes { get; set; } = [];
    
    /// <summary>
    /// When true, both handler systems run in parallel for comparison testing.
    /// Results are logged but only legacy results are returned.
    /// </summary>
    public bool EnableParallelComparison { get; set; } = false;
}

// appsettings.json
{
    "GameHandlers": {
        "UseGenericHandlersForNewGames": false,
        "GenericHandlerGameTypes": [],
        "EnableParallelComparison": false
    }
}
```

#### Feature Flag Usage in ContinuousPlayBackgroundService

```csharp
public sealed class ContinuousPlayBackgroundService : BackgroundService
{
    private readonly GameHandlerFeatureFlags _featureFlags;
    private readonly IGameFlowHandlerFactory _flowHandlerFactory;
    
    private async Task StartNextHandAsync(Game game, ...)
    {
        // Determine which handler system to use
        var useGenericHandler = ShouldUseGenericHandler(game);
        
        if (useGenericHandler)
        {
            var flowHandler = _flowHandlerFactory.GetHandler(game.GameType?.Code);
            game.CurrentPhase = flowHandler.GetInitialPhase(game);
            await flowHandler.OnHandStartingAsync(game, cancellationToken);
        }
        else
        {
            // Existing hardcoded logic (unchanged)
            var isKingsAndLows = string.Equals(game.GameType?.Code, "KINGSANDLOWS", ...);
            game.CurrentPhase = isKingsAndLows
                ? nameof(Phases.Dealing)
                : nameof(Phases.CollectingAntes);
        }
    }
    
    private bool ShouldUseGenericHandler(Game game)
    {
        // Game already created with specific handler version
        if (game.GenericHandlerVersion.HasValue)
            return game.GenericHandlerVersion.Value > 0;
        
        // Check feature flags
        if (!_featureFlags.UseGenericHandlersForNewGames)
            return false;
        
        if (_featureFlags.GenericHandlerGameTypes.Contains("*"))
            return true;
        
        return _featureFlags.GenericHandlerGameTypes
            .Contains(game.GameType?.Code ?? "", StringComparer.OrdinalIgnoreCase);
    }
}
```

### 4A.5 Backend Integration Testing Strategy

Run integration tests exclusively against the backend, independent of frontend:

#### Test Project Structure

```
Tests/
├── CardGames.Poker.Api.IntegrationTests/
│   ├── Infrastructure/
│   │   ├── PostgresTestContainer.cs      # Testcontainers for real DB
│   │   ├── ApiFactory.cs                 # WebApplicationFactory setup
│   │   └── TestAuthHandler.cs            # Bypass auth for tests
│   │
│   ├── Legacy/                           # Tests for v1 (existing) endpoints
│   │   ├── FiveCardDraw/
│   │   │   └── StartHandTests.cs
│   │   ├── KingsAndLows/
│   │   │   └── DropOrStayTests.cs
│   │   └── SevenCardStud/
│   │       └── BettingRoundTests.cs
│   │
│   ├── Generic/                          # Tests for v2 (new) endpoints
│   │   ├── StartHandTests.cs
│   │   ├── CollectAntesTests.cs
│   │   ├── ProcessBettingActionTests.cs
│   │   └── PerformShowdownTests.cs
│   │
│   └── Parity/                           # Tests that compare v1 vs v2
│       ├── StartHandParityTests.cs
│       └── ShowdownParityTests.cs
```

#### Integration Test Base Class

```csharp
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected HttpClient Client { get; private set; } = null!;
    protected CardsDbContext DbContext { get; private set; } = null!;
    
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .Build();
    
    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Replace DB connection
                    services.RemoveAll<DbContextOptions<CardsDbContext>>();
                    services.AddDbContext<CardsDbContext>(options =>
                        options.UseNpgsql(_postgres.GetConnectionString()));
                    
                    // Add test authentication
                    services.AddAuthentication("Test")
                        .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
                });
            });
        
        Client = factory.CreateClient();
        
        // Get scoped DbContext for test assertions
        var scope = factory.Services.CreateScope();
        DbContext = scope.ServiceProvider.GetRequiredService<CardsDbContext>();
        await DbContext.Database.MigrateAsync();
    }
    
    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }
}
```

#### Parity Test Example

```csharp
/// <summary>
/// Ensures generic handlers produce identical results to legacy handlers.
/// </summary>
public class StartHandParityTests : IntegrationTestBase
{
    [Theory]
    [InlineData("FIVECARDDRAW")]
    [InlineData("KINGSANDLOWS")]
    [InlineData("SEVENCARDSTUD")]
    public async Task StartHand_GenericHandler_MatchesLegacyHandler(string gameTypeCode)
    {
        // Arrange: Create two identical games
        var legacyGameId = await CreateGame(gameTypeCode, useGenericHandler: false);
        var genericGameId = await CreateGame(gameTypeCode, useGenericHandler: true);
        
        await JoinPlayers(legacyGameId, count: 4);
        await JoinPlayers(genericGameId, count: 4);
        
        // Act: Start hand on both
        var legacyResponse = await Client.PostAsync(
            $"/api/v1/games/{GetGamePath(gameTypeCode)}/{legacyGameId}/start-hand", null);
        var genericResponse = await Client.PostAsync(
            $"/api/v2/games/{genericGameId}/start-hand", null);
        
        // Assert: Both succeed with matching state
        legacyResponse.EnsureSuccessStatusCode();
        genericResponse.EnsureSuccessStatusCode();
        
        var legacyGame = await DbContext.Games
            .Include(g => g.GamePlayers)
            .FirstAsync(g => g.Id == legacyGameId);
        var genericGame = await DbContext.Games
            .Include(g => g.GamePlayers)
            .FirstAsync(g => g.Id == genericGameId);
        
        // Same phase after start
        Assert.Equal(legacyGame.CurrentPhase, genericGame.CurrentPhase);
        
        // Same hand number
        Assert.Equal(legacyGame.CurrentHandNumber, genericGame.CurrentHandNumber);
        
        // Same player states
        Assert.Equal(
            legacyGame.GamePlayers.Count(p => !p.HasFolded),
            genericGame.GamePlayers.Count(p => !p.HasFolded));
    }
}
```

#### Running Integration Tests

```bash
# Run all integration tests
dotnet test Tests/CardGames.Poker.Api.IntegrationTests/

# Run only legacy endpoint tests (ensure no regressions)
dotnet test Tests/CardGames.Poker.Api.IntegrationTests/ --filter "Category=Legacy"

# Run only generic endpoint tests (validate new handlers)
dotnet test Tests/CardGames.Poker.Api.IntegrationTests/ --filter "Category=Generic"

# Run parity tests (compare old vs new)
dotnet test Tests/CardGames.Poker.Api.IntegrationTests/ --filter "Category=Parity"
```

### 4A.6 Rollout Plan

| Phase | Duration | Feature Flags | Testing | Risk |
|-------|----------|---------------|---------|------|
| **Phase 1: Develop** | 2 weeks | `GenericHandlerGameTypes: []` | Unit tests only | None |
| **Phase 2: Shadow** | 1 week | `EnableParallelComparison: true` | Both handlers run, log diffs | None |
| **Phase 3: Canary** | 1 week | `GenericHandlerGameTypes: ["FIVECARDDRAW"]` | Test one game type | Low |
| **Phase 4: Expand** | 2 weeks | Add more game types | Monitor production | Medium |
| **Phase 5: Default** | Ongoing | `UseGenericHandlersForNewGames: true` | All new games | Medium |
| **Phase 6: Migrate** | 2 weeks | Mark v1 deprecated | Inform API consumers | Low |
| **Phase 7: Sunset v1** | After 3mo | Remove v1 endpoints | - | Low |

### 4A.7 Rollback Procedures

#### Immediate Rollback (< 5 minutes)
```json
// appsettings.json change only
{
    "GameHandlers": {
        "UseGenericHandlersForNewGames": false,
        "GenericHandlerGameTypes": []
    }
}
```
No deployment needed if using dynamic configuration (Azure App Configuration, etc.).

#### Active Game Rollback
Games already started with generic handlers continue using them. If issues occur:

1. Set `GenericHandlerGameTypes: []` to stop new games using generic handlers
2. Active games with issues: manually update `GenericHandlerVersion = NULL` in database
3. These games will revert to legacy handlers on next action

#### Database Rollback
Since all migrations are additive-only:
- There's no need to roll back database schema
- New nullable columns are simply ignored by legacy handlers
- In worst case, run migration `Down` to drop new columns

---

## 5. Detailed Component Design

### 5.1 GameRules Extensions

Extend `GamePhaseDescriptor` to include category as an enum:

```csharp
public class GamePhaseDescriptor
{
    // ... existing properties
    
    /// <summary>
    /// Gets the category enum for programmatic routing.
    /// </summary>
    public PhaseCategory CategoryType { get; init; }
    
    /// <summary>
    /// Gets the valid transitions from this phase.
    /// </summary>
    public IReadOnlyList<string> ValidNextPhases { get; init; } = [];
}
```

### 5.2 IHandEvaluatorFactory Registration Pattern

The existing `HandEvaluatorFactory` uses a static dictionary. Update it to use assembly scanning similar to `PokerGameMetadataRegistry`:

```csharp
public sealed class HandEvaluatorFactory : IHandEvaluatorFactory
{
    private static readonly FrozenDictionary<string, IHandEvaluator> EvaluatorsByGameCode;

    static HandEvaluatorFactory()
    {
        var evaluatorsDict = new Dictionary<string, IHandEvaluator>(StringComparer.OrdinalIgnoreCase);
        
        // Scan for IHandEvaluator implementations with GameTypeCode attribute
        var evaluatorInterface = typeof(IHandEvaluator);
        var assembly = evaluatorInterface.Assembly;
        
        var evaluatorTypes = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && 
                        evaluatorInterface.IsAssignableFrom(t));

        foreach (var evalType in evaluatorTypes)
        {
            var attr = evalType.GetCustomAttribute<HandEvaluatorForAttribute>();
            if (attr is not null)
            {
                var instance = Activator.CreateInstance(evalType) as IHandEvaluator;
                if (instance is not null)
                {
                    evaluatorsDict[attr.GameTypeCode] = instance;
                }
            }
        }

        EvaluatorsByGameCode = evaluatorsDict.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }
}
```

### 5.3 Generic API Endpoints

Create a unified API that routes based on game type:

```csharp
[EndpointMapGroup]
public static class GenericGamesApiMapGroup
{
    public static void MapEndpoints(IEndpointRouteBuilder app)
    {
        var games = app.NewVersionedApi("Games");
        
        var v1 = games.MapGroup("/api/v1/games/{gameId:guid}")
            .HasApiVersion(1.0);
        
        v1.MapPost("/start-hand", StartHandAsync);
        v1.MapPost("/collect-antes", CollectAntesAsync);
        v1.MapPost("/deal", DealHandsAsync);
        v1.MapPost("/betting-action", ProcessBettingActionAsync);
        v1.MapPost("/draw", ProcessDrawAsync);
        v1.MapPost("/showdown", PerformShowdownAsync);
        
        // Phase-specific endpoints are still needed for special phases
        v1.MapPost("/drop-or-stay", ProcessDropOrStayAsync);
        v1.MapPost("/pot-match", ProcessPotMatchAsync);
    }
}
```

---

## 6. Migration Strategy

> 📋 **See Also:** For detailed backward compatibility requirements, API versioning, database migration rules, feature flags, and integration testing strategy, refer to [Section 4A: Backward Compatibility Strategy](#4a-backward-compatibility-strategy).

### 6.1 Parallel Implementation Approach

> **Key Requirement:** The frontend must continue working unchanged throughout the entire migration.

1. **Create new generic handlers alongside existing handlers**
   - Don't delete existing game-specific handlers immediately
   - New handlers use different route paths (v2 endpoints)
   - Frontend continues using v1 endpoints without modification

2. **Feature flag for gradual rollout** (see [Section 4A.4](#4a4-feature-flags-for-gradual-rollout) for details)
   ```csharp
   if (_featureFlags.UseGenericHandlers)
   {
       await mediator.Send(new Generic.StartHandCommand(gameId));
   }
   else
   {
       // Route to game-specific handler based on game type
       await RouteToLegacyHandler(gameTypeCode, gameId);
   }
   ```

3. **Test parity between old and new handlers** (see [Section 4A.5](#4a5-backend-integration-testing-strategy))
   - Run both handlers in parallel in test environment
   - Compare results to ensure identical behavior
   - Run parity tests before each deployment

4. **Gradual deprecation** (see [Section 4A.6](#4a6-rollout-plan))
   - Mark old handlers as `[Obsolete]`
   - Remove after successful validation period (minimum 3 months)

### 6.2 File Deprecation Checklist

After migration, these files can be deprecated:

| File | Replacement | Priority |
|------|-------------|----------|
| `FiveCardDraw/v1/Commands/StartHand/StartHandCommandHandler.cs` | `Generic/v1/Commands/StartHand/StartHandCommandHandler.cs` | High |
| `SevenCardStud/v1/Commands/StartHand/StartHandCommandHandler.cs` | Same generic handler | High |
| `KingsAndLows/v1/Commands/StartHand/StartHandCommandHandler.cs` | Same generic handler | High |
| `TwosJacksManWithTheAxe/v1/Commands/StartHand/StartHandCommandHandler.cs` | Same generic handler | High |
| `FiveCardDraw/v1/Commands/PerformShowdown/...` | Generic showdown handler | Medium |
| ... (similar for other commands) | ... | ... |

**Keep game-specific folders for:**
- Truly unique endpoints (DropOrStay, PotMatching, BuyCard)
- Game-specific queries that need custom response shapes

---

## 7. Testing Strategy

### 7.1 Unit Tests for Flow Handlers

```csharp
public class KingsAndLowsFlowHandlerTests
{
    private readonly KingsAndLowsFlowHandler _handler = new();

    [Fact]
    public void GetInitialPhase_ReturnsDealing()
    {
        var game = new Game();
        
        var result = _handler.GetInitialPhase(game);
        
        Assert.Equal("Dealing", result);
    }

    [Theory]
    [InlineData("Dealing", "DropOrStay")]
    [InlineData("DropOrStay", null)] // Depends on game state
    [InlineData("DrawPhase", "DrawComplete")]
    public void GetNextPhase_ReturnsExpectedPhase(string current, string? expected)
    {
        var game = CreateGameWithActivePlayers(3);
        
        var result = _handler.GetNextPhase(game, current);
        
        Assert.Equal(expected, result);
    }
}
```

### 7.2 Integration Tests for Generic Handlers

```csharp
public class GenericStartHandIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    [Theory]
    [InlineData("FIVECARDDRAW", "CollectingAntes")]
    [InlineData("KINGSANDLOWS", "Dealing")]
    [InlineData("SEVENCARDSTUD", "CollectingAntes")]
    public async Task StartHand_TransitionsToCorrectInitialPhase(string gameType, string expectedPhase)
    {
        // Arrange
        var gameId = await CreateGameOfType(gameType);
        
        // Act
        var result = await _mediator.Send(new StartHandCommand(gameId));
        
        // Assert
        result.Match(
            success => Assert.Equal(expectedPhase, success.InitialPhase),
            error => Assert.Fail(error.Message));
    }
}
```

### 7.3 Parity Tests

```csharp
public class HandlerParityTests
{
    [Fact]
    public async Task GenericHandler_ProducesSameResult_AsLegacyHandler()
    {
        // Arrange
        var gameId = await CreateFiveCardDrawGame();
        
        // Act
        var legacyResult = await ExecuteLegacyStartHand(gameId);
        await ResetGame(gameId);
        var genericResult = await ExecuteGenericStartHand(gameId);
        
        // Assert
        AssertEquivalent(legacyResult, genericResult);
    }
}
```

---

## 8. Risk Assessment

### 8.1 Implementation Risks

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Breaking existing game behavior | Medium | High | Extensive parity testing, feature flags |
| Performance regression | Low | Medium | Benchmark critical paths, cache evaluators |
| Missing edge cases in generic handlers | Medium | Medium | Copy existing handler tests, add coverage |
| DI complexity increase | Low | Low | Clear factory patterns, documentation |

### 8.2 Rollback Plan

> 📋 **Detailed Procedures:** See [Section 4A.7: Rollback Procedures](#4a7-rollback-procedures) for step-by-step instructions.

**Summary:**

1. **Immediate rollback (< 5 minutes):** Change `GenericHandlerGameTypes: []` in configuration
2. **Active game rollback:** Set `GenericHandlerVersion = NULL` in database for affected games
3. **Code rollback:** Feature flag allows instant rollback to legacy handlers without deployment
4. **Keep legacy handlers** for minimum 3 release cycles (per rollout plan)
5. **Database schema:** All changes are additive-only, no rollback needed

### 8.3 Success Metrics (Poker Variants Only)

| Metric | Target | Current |
|--------|--------|---------|
| Lines of code to add new poker game | < 500 | ~2000 |
| Files to modify for new poker game | < 5 | 15+ |
| Test coverage for handlers | > 90% | ~70% |
| Time to implement new poker variant | < 1 day | 3-5 days |

---

## 9. Non-Poker Game Considerations

### 9.1 Architectural Limitations

The generic command handler architecture described in this document is designed for **poker variants** that share common concepts:

- **Poker hand evaluation** (5-card hands, hand rankings)
- **Chip-based betting** (antes, blinds, betting rounds, pots)
- **Showdown mechanics** (compare hands, award pot to winner)
- **Common phases** (dealing, betting, drawing, showdown)

Games that don't follow this model (e.g., "Screw Your Neighbor", "Acey-Deucey", Blackjack, Uno) cannot use the proposed architecture without significant modifications.

### 9.2 Case Study: "Screw Your Neighbor"

**Game Overview:**
- Players are dealt 1 card each (not 5-7)
- Players have 3 "lives" instead of chips
- Each round, players can KEEP or SWAP with neighbor to the left
- Kings are revealed and cannot be swapped
- All cards reveal simultaneously - lowest card loses a life
- Last player with lives wins

**Why Generic Poker Handlers Won't Work:**

| Proposed Handler | Purpose | Screw Your Neighbor Need |
|------------------|---------|--------------------------|
| `StartHandCommand` | Reset for new poker hand | Need `StartRoundCommand` (multiple rounds per game) |
| `CollectAntesCommand` | Collect chip antes | ❌ No antes, lives-based |
| `DealHandsCommand` | Deal 5-7 cards | Deal exactly 1 card per player |
| `ProcessBettingActionCommand` | Bet, Call, Raise, Fold | ❌ No betting, need Keep/Swap actions |
| `ProcessDrawCommand` | Discard and draw from deck | ❌ Swap with neighbor, not deck |
| `PerformShowdownCommand` | Evaluate poker hands, award pot | Reveal cards, lowest loses life |

**Missing Concepts:**

1. **Lives System** - Not handled by `IGameFlowHandler`
```csharp
// Current: Chip-based
player.ChipStack += potAmount;

// Needed: Lives-based
player.Lives--;
if (player.Lives == 0) player.IsEliminated = true;
```

2. **Neighbor Interaction** - No equivalent in poker
```csharp
// New requirement: Swap cards between players
public interface INeighborSwapHandler
{
    Task SwapCardsAsync(GamePlayer current, GamePlayer neighbor);
}
```

3. **Multi-Round Game State** - Poker hands are independent
```csharp
// Poker: Each hand is complete
game.CurrentHandNumber++;

// Screw Your Neighbor: Game spans many rounds
game.CurrentRoundNumber++;
game.IsGameComplete = game.GamePlayers.Count(p => !p.IsEliminated) == 1;
```

4. **Immunity Mechanics** - No poker equivalent
```csharp
// King immunity: Cannot be targeted for swap
public bool IsImmuneToSwap(Card card) => card.Symbol == Symbol.King;
```

### 9.3 Case Study: "Acey-Deucey" (In-Between)

**Game Overview:**
- 2+ players bet against a central pot
- All players place minimum bet into pot at start
- Dealer places two cards face-up with space between (the "spread")
- Active player can pass or bet any amount up to the pot size
- Dealer reveals third card - if its rank is between the spread cards, player wins
- If third card is outside the range, player loses bet to pot
- If third card matches a spread card's rank, player "posts" (2x bet to pot)
- First Ace can be high or low (player's choice); second Ace is always high
- Game ends when pot is empty

**Why Generic Poker Handlers Won't Work:**

| Proposed Handler | Purpose | Acey-Deucey Need |
|------------------|---------|------------------|
| `StartHandCommand` | Reset for new poker hand | `StartTurnCommand` (individual turns, not hands) |
| `CollectAntesCommand` | Collect chip antes | Only at game start, not per turn |
| `DealHandsCommand` | Deal cards to players | `DealSpreadCommand` - deal 2 cards to table center |
| `ProcessBettingActionCommand` | Bet, Call, Raise, Fold | `PlaceBetOrPassCommand` - 0 to pot size |
| `ProcessDrawCommand` | Discard and draw | ❌ Not applicable |
| `PerformShowdownCommand` | Compare hands | `RevealAndEvaluateCommand` - check if in-between |

**Fundamental Differences:**

1. **No Player Hands** - Cards are dealt to table, not to players
2. **Individual Turns** - One player acts per turn vs parallel betting rounds
3. **Pot-Centric Betting** - Bet against pot, not against other players
4. **Posting Penalty** - Unique mechanic (2x loss on match) has no poker equivalent
5. **Ace Choice** - Player decides high/low per-turn, not fixed game rule
6. **Turn-Based Game** - Rotates through players until pot empty, not fixed hand structure

**Required New Components:**

```csharp
// CardGames.BettingGames/Games/AceyDeucey/AceyDeuceyGame.cs
public class AceyDeuceyGame : IBettingPotGame
{
    public string Code => "ACEYDEUCEY";
    public string Name => "Acey-Deucey";
    public int MinPlayers => 2;
    public int MaxPlayers => 10;
    
    public AceyDeuceyRules GetRules() => new()
    {
        PostingMultiplier = 2,
        FirstAceFlexible = true,
        SecondAceAlwaysHigh = true,
        MaxBet = MaxBetRule.PotSize
    };
}

// CardGames.BettingGames.Api/GameFlow/AceyDeuceyFlowHandler.cs
public sealed class AceyDeuceyFlowHandler : IBettingGameFlowHandler
{
    public string GameTypeCode => "ACEYDEUCEY";
    
    public string GetInitialPhase(IGameState game) => "WaitingForPlayers";
    
    public string? GetNextPhase(IGameState game, string currentPhase)
    {
        return currentPhase switch
        {
            "WaitingForPlayers" => HasEnoughPlayers(game) ? "CollectInitialAntes" : null,
            "CollectInitialAntes" => "DealSpread",
            "DealSpread" => HasAceFirst(game) ? "AceDecision" : "PlaceBet",
            "AceDecision" => "PlaceBet",
            "PlaceBet" => "RevealThirdCard",
            "RevealThirdCard" => "ProcessResult",
            "ProcessResult" => IsPotEmpty(game) ? "GameComplete" : "NextPlayer",
            "NextPlayer" => "DealSpread",
            _ => null
        };
    }
}

// CardGames.BettingGames.Api/Features/AceyDeucey/v1/Commands/
// - PlaceBetOrPass/PlaceBetOrPassCommand.cs
// - DealSpread/DealSpreadCommand.cs
// - ChooseAceValue/ChooseAceValueCommand.cs
// - RevealThirdCard/RevealThirdCardCommand.cs
```

**Database Entity Differences:**

```csharp
// Poker: GamePlayer has hand-related fields
public class GamePlayer
{
    public int CurrentBet { get; set; }
    public int TotalContributedThisHand { get; set; }
    public bool HasFolded { get; set; }
    public bool IsAllIn { get; set; }
}

// Acey-Deucey: Minimal player state (most state is turn-based)
public class AceyDeuceyPlayer
{
    public int ChipStack { get; set; }
    public bool IsActive { get; set; }
    public int SeatPosition { get; set; }
}

// Acey-Deucey: Turn state (ephemeral, per-turn)
public class AceyDeuceyTurn
{
    public Guid PlayerId { get; set; }
    public Card SpreadCardLow { get; set; }
    public Card SpreadCardHigh { get; set; }
    public Card? RevealedCard { get; set; }
    public AceChoice? AceChoice { get; set; }
    public int BetAmount { get; set; }
    public TurnOutcome? Outcome { get; set; }
}
```

### 9.4 Required Additional Abstractions

To support non-poker games with the generic handler pattern, these additional abstractions would be needed:

#### 9.4.1 Abstract Game Interface Hierarchy

```csharp
// Level 0: Base card game (all games)
public interface ICardGame
{
    string Code { get; }
    string Name { get; }
    IGameRulesBase GetRules();
}

// Level 1a: Poker games (current focus)
public interface IPokerGame : ICardGame
{
    GameRules GetGameRules();  // Poker-specific rules
}

// Level 1b: Elimination games (new)
public interface IEliminationGame : ICardGame
{
    EliminationRules GetEliminationRules();
    int StartingLives { get; }
    string LossCondition { get; }
}

// Level 1c: Pot betting games (new)
public interface IBettingPotGame : ICardGame
{
    BettingPotRules GetBettingPotRules();
    string WinCondition { get; }
    string MaxBetRule { get; }
}

// Level 1d: Trick-taking games (future)
public interface ITrickTakingGame : ICardGame { ... }
```

#### 9.4.2 Abstract Flow Handler Hierarchy

```csharp
// Base handler for all card games
public interface ICardGameFlowHandler
{
    string GameTypeCode { get; }
    string GetInitialPhase(IGameState game);
    string? GetNextPhase(IGameState game, string currentPhase);
}

// Poker-specific (current)
public interface IPokerFlowHandler : ICardGameFlowHandler
{
    DealingConfiguration GetDealingConfiguration();
    bool SkipsAnteCollection { get; }
}

// Elimination-specific (new)
public interface IEliminationFlowHandler : ICardGameFlowHandler
{
    int GetStartingLives();
    bool IsPlayerEliminated(IPlayerState player);
    IPlayerState DetermineRoundLoser(IEnumerable<IPlayerState> players);
}

// Betting pot-specific (new)
public interface IBettingGameFlowHandler : ICardGameFlowHandler
{
    int GetMaxBet(IGameState game);
    SpreadResult EvaluateSpread(Card low, Card high, Card revealed);
    int CalculatePayout(int betAmount, SpreadResult result);
}
```

#### 9.4.3 Screw Your Neighbor Flow Handler

```csharp
public sealed class ScrewYourNeighborFlowHandler : IEliminationFlowHandler
{
    public string GameTypeCode => "SCREWYOURNEIGHBOR";
    
    public int GetStartingLives() => 3;
    
    public string GetInitialPhase(IGameState game) => "Dealing";
    
    public string? GetNextPhase(IGameState game, string currentPhase)
    {
        return currentPhase switch
        {
            "Dealing" => "PassKeepRound",
            "PassKeepRound" => "SimultaneousReveal",
            "SimultaneousReveal" => "LosesLife",
            "LosesLife" => CheckForWinner(game) ? "GameComplete" : "Dealing",
            _ => null
        };
    }
    
    public IPlayerState DetermineRoundLoser(IEnumerable<IPlayerState> players)
    {
        return players
            .Where(p => !p.IsEliminated)
            .OrderBy(p => p.CurrentCard.Rank)
            .First();
    }
    
    private bool CheckForWinner(IGameState game)
    {
        return game.Players.Count(p => !p.IsEliminated) == 1;
    }
}
```

#### 9.4.5 Flow Handler for Acey-Deucey

```csharp
public sealed class AceyDeuceyFlowHandler : IBettingGameFlowHandler
{
    public string GameTypeCode => "ACEYDEUCEY";
    
    public string GetInitialPhase(IGameState game) => "CollectInitialAntes";
    
    public string? GetNextPhase(IGameState game, string currentPhase)
    {
        var pot = game.Pot;
        
        return currentPhase switch
        {
            "CollectInitialAntes" => "DealSpread",
            "DealSpread" => HasFlexibleAce(game) ? "AceDecision" : "PlaceBet",
            "AceDecision" => "PlaceBet",
            "PlaceBet" => game.CurrentBet > 0 ? "RevealThirdCard" : "NextPlayer",
            "RevealThirdCard" => "ProcessResult",
            "ProcessResult" => pot.Amount <= 0 ? "GameComplete" : "NextPlayer",
            "NextPlayer" => "DealSpread",
            _ => null
        };
    }
    
    public int GetMaxBet(IGameState game) => game.Pot.Amount;
    
    public SpreadResult EvaluateSpread(Card low, Card high, Card revealed)
    {
        var lowRank = GetRankValue(low);
        var highRank = GetRankValue(high);
        var revealedRank = GetRankValue(revealed);
        
        if (revealedRank == lowRank || revealedRank == highRank)
            return SpreadResult.Matched;
        if (revealedRank > lowRank && revealedRank < highRank)
            return SpreadResult.InBetween;
        return SpreadResult.Outside;
    }
    
    public int CalculatePayout(int betAmount, SpreadResult result)
    {
        return result switch
        {
            SpreadResult.InBetween => betAmount,    // Win: get bet from pot
            SpreadResult.Outside => -betAmount,      // Lose: bet goes to pot
            SpreadResult.Matched => -betAmount * 2,  // Post: 2x bet goes to pot
            _ => 0
        };
    }
    
    private bool HasFlexibleAce(IGameState game)
    {
        // First dealt card is an Ace
        return game.SpreadCards.First().Symbol == Symbol.Ace;
    }
}
```

#### 9.4.6 Phase Handler for Pass/Keep

```csharp
public sealed class PassKeepPhaseHandler : IPhaseHandler
{
    public string PhaseId => "PassKeepRound";
    public IReadOnlyList<string> ApplicableGameTypes => ["SCREWYOURNEIGHBOR"];
    
    public IReadOnlyList<string> AvailableActions => ["Keep", "SwapLeft"];
    
    public async Task ProcessActionAsync(
        Game game, 
        GamePlayer player, 
        string action,
        CancellationToken ct)
    {
        if (action == "SwapLeft")
        {
            var neighbor = GetLeftNeighbor(game, player);
            
            // Check for King immunity
            if (HasKing(neighbor))
            {
                // Player must keep their card
                player.Decision = PassKeepDecision.ForcedKeep;
            }
            else
            {
                await SwapCards(player, neighbor, ct);
                player.Decision = PassKeepDecision.Swapped;
            }
        }
        else
        {
            player.Decision = PassKeepDecision.Kept;
        }
    }
}
```

### 9.5 Project Structure for Non-Poker Games

```
CardGames.sln
├── CardGames.Core/                    # Shared (cards, deck, suits, etc.)
│   ├── Cards/
│   ├── Deck/
│   └── Games/
│       └── ICardGame.cs              # NEW: Base interface
│
├── CardGames.Poker/                   # Existing poker domain
│   └── Games/
│       ├── IPokerGame.cs             # MODIFIED: Inherits from ICardGame
│       └── ...
│
├── CardGames.Elimination/             # NEW: Elimination games domain
│   ├── Games/
│   │   ├── IEliminationGame.cs
│   │   └── ScrewYourNeighbor/
│   │       ├── ScrewYourNeighborGame.cs
│   │       └── ScrewYourNeighborRules.cs
│   ├── Scoring/
│   │   └── LowestCardLosesDeterminer.cs
│   └── Phases/
│       └── ScrewYourNeighborPhases.cs
│
├── CardGames.BettingGames/            # NEW: Pot betting games domain
│   ├── Games/
│   │   ├── IBettingPotGame.cs
│   │   └── AceyDeucey/
│   │       ├── AceyDeuceyGame.cs
│   │       ├── AceyDeuceyRules.cs
│   │       └── SpreadCardEvaluator.cs
│   ├── Betting/
│   │   └── PotBettingService.cs
│   └── Phases/
│       └── AceyDeuceyPhases.cs
│
├── CardGames.Poker.Api/               # Existing poker API
│
├── CardGames.Elimination.Api/         # NEW: Elimination games API
│   ├── GameFlow/
│   │   ├── IEliminationFlowHandler.cs
│   │   └── ScrewYourNeighborFlowHandler.cs
│   ├── Features/
│   │   └── ScrewYourNeighbor/
│   │       └── v1/Commands/
│   │           ├── StartRound/
│   │           ├── ProcessPassKeep/
│   │           ├── RevealCards/
│   │           └── ProcessElimination/
│   └── Hubs/
│       └── EliminationGameHub.cs
│
├── CardGames.BettingGames.Api/        # NEW: Pot betting games API
│   ├── GameFlow/
│   │   ├── IBettingGameFlowHandler.cs
│   │   └── AceyDeuceyFlowHandler.cs
│   ├── Features/
│   │   └── AceyDeucey/
│   │       └── v1/Commands/
│   │           ├── PlaceBetOrPass/
│   │           ├── DealSpread/
│   │           ├── ChooseAceValue/
│   │           └── RevealThirdCard/
│   └── Hubs/
│       └── BettingGameHub.cs
│
└── CardGames.Web/                     # Shared web (or separate per game type)
    └── Components/
        ├── EliminationGames/
        │   └── ScrewYourNeighborTable.razor
        └── BettingGames/
            └── AceyDeuceyTable.razor
```

### 9.6 Recommendations

#### Immediate (If Adding Screw Your Neighbor Now)

1. **Create separate project**: `CardGames.Elimination` and `CardGames.Elimination.Api`
2. **Share only core components**: Cards, Deck, User management, SignalR infrastructure
3. **Independent feature folders**: Don't try to fit into poker generic handlers
4. **Separate database tables**: `EliminationGamePlayer` instead of extending `GamePlayer`

#### Future (When Multiple Game Types Exist)

1. **Extract `ICardGame` base interface** from `IPokerGame`
2. **Create `ICardGameFlowHandler` base** from `IGameFlowHandler`
3. **Implement composite registries** that aggregate multiple game type registries
4. **Build unified game lobby** that routes to appropriate game-specific UI

### 9.7 Effort Estimation for Non-Poker Games

#### 9.7.1 Screw Your Neighbor

| Component | Approach A: Separate Project | Approach B: Extend Current |
|-----------|------------------------------|---------------------------|
| Domain layer | 1 week (clean) | 2 weeks (abstraction) |
| API layer | 1 week (clean) | 2-3 weeks (refactoring) |
| Database | 2-3 days (new tables) | 1-2 weeks (migrations) |
| UI | 1 week (new components) | 1-2 weeks (abstract + implement) |
| Testing | 1 week | 1-2 weeks |
| **Total** | **4-5 weeks** | **7-10 weeks** |

#### 9.7.2 Acey-Deucey

| Component | Approach A: Separate Project | Approach B: Extend Current |
|-----------|------------------------------|---------------------------|
| Domain layer | 3-4 days (simpler game) | 1-2 weeks (abstraction) |
| API layer | 3-4 days (fewer commands) | 2 weeks (refactoring) |
| Database | 1-2 days (minimal tables) | 1 week (migrations) |
| UI | 3-4 days (simple layout) | 1 week (abstract + implement) |
| Testing | 3-4 days | 1 week |
| **Total** | **2-3 weeks** | **5-7 weeks** |

**Note:** Acey-Deucey is simpler than Screw Your Neighbor because:
- No player-to-player card swapping
- No immunity mechanics
- No lives/elimination tracking
- Simpler UI (just 3 cards and bet controls)
- Fewer game phases

**Recommendation:** Use **Approach A (Separate Project)** initially. Acey-Deucey and Screw Your Neighbor have so little overlap that abstracting now provides minimal benefit.

---

## Appendix A: Complete File List for New Architecture

### New Files to Create

```
CardGames.Poker.Api/
├── GameFlow/
│   ├── IGameFlowHandler.cs
│   ├── IGameFlowHandlerFactory.cs
│   ├── GameFlowHandlerFactory.cs
│   ├── BaseGameFlowHandler.cs
│   ├── DealingConfiguration.cs
│   ├── FiveCardDrawFlowHandler.cs
│   ├── SevenCardStudFlowHandler.cs
│   ├── KingsAndLowsFlowHandler.cs
│   └── TwosJacksManWithTheAxeFlowHandler.cs
│
├── Features/Games/
│   ├── Generic/
│   │   ├── GenericGamesApiMapGroup.cs
│   │   └── v1/
│   │       ├── Commands/
│   │       │   ├── StartHand/
│   │       │   │   ├── StartHandCommand.cs
│   │       │   │   ├── StartHandCommandHandler.cs
│   │       │   │   ├── StartHandSuccessful.cs
│   │       │   │   └── StartHandError.cs
│   │       │   ├── CollectAntes/
│   │       │   │   └── (similar structure)
│   │       │   ├── DealHands/
│   │       │   ├── ProcessBettingAction/
│   │       │   ├── ProcessDraw/
│   │       │   └── PerformShowdown/
│   │       └── V1.cs
│   │
│   └── PhaseHandlers/
│       ├── IPhaseHandler.cs
│       ├── IPhaseHandlerFactory.cs
│       ├── PhaseHandlerFactory.cs
│       └── DropOrStay/
│           └── DropOrStayPhaseHandler.cs

CardGames.Poker/
└── Games/GameFlow/
    └── PhaseCategory.cs
```

### Files to Modify

```
CardGames.Poker.Api/
├── Program.cs                                    # Add DI registration
├── Services/ContinuousPlayBackgroundService.cs   # Use IGameFlowHandlerFactory
├── Services/TableStateBuilder.cs                 # Use IHandEvaluatorFactory
└── Services/AutoActionService.cs                 # Use GameRules.Phases
```

### Files to Eventually Deprecate

```
CardGames.Poker.Api/Features/Games/
├── FiveCardDraw/v1/Commands/StartHand/StartHandCommandHandler.cs
├── SevenCardStud/v1/Commands/StartHand/StartHandCommandHandler.cs
├── KingsAndLows/v1/Commands/StartHand/StartHandCommandHandler.cs
├── TwosJacksManWithTheAxe/v1/Commands/StartHand/StartHandCommandHandler.cs
└── (similar for other duplicated command handlers)
```

---

## Appendix B: Example - Adding a New Poker Game (Razz)

> **Scope:** This example demonstrates adding a **poker variant** (Razz). For non-poker games, see [Section 9](#9-non-poker-game-considerations).

With the new architecture, adding Razz (7-card stud lowball) requires:

### 1. Domain Layer (CardGames.Poker)

**File:** `CardGames.Poker/Games/Razz/RazzGame.cs`

```csharp
[PokerGameMetadata(Code = "RAZZ", Name = "Razz", Description = "Seven Card Stud Lowball")]
public class RazzGame : IPokerGame
{
    public string Name => "Razz";
    public string Description => "Seven Card Stud played for low hands only";
    public int MinimumNumberOfPlayers => 2;
    public int MaximumNumberOfPlayers => 8;

    public GameRules GetGameRules() => RazzRules.CreateGameRules();
}
```

**File:** `CardGames.Poker/Games/Razz/RazzRules.cs`

```csharp
public static class RazzRules
{
    public static GameRules CreateGameRules() => new()
    {
        GameTypeCode = "RAZZ",
        GameTypeName = "Razz",
        Description = "Seven Card Stud Lowball - best low hand wins",
        MinPlayers = 2,
        MaxPlayers = 8,
        Phases = SevenCardStudRules.GetStudPhases(), // Reuse stud phases
        // ... rest of configuration
    };
}
```

**File:** `CardGames.Poker/Evaluation/Evaluators/RazzHandEvaluator.cs`

```csharp
[HandEvaluatorFor("RAZZ")]
public class RazzHandEvaluator : IHandEvaluator
{
    // Low hand evaluation logic
}
```

### 2. API Layer (CardGames.Poker.Api)

**File:** `CardGames.Poker.Api/GameFlow/RazzFlowHandler.cs`

```csharp
public sealed class RazzFlowHandler : SevenCardStudFlowHandler
{
    public override string GameTypeCode => "RAZZ";
    
    public override GameRules GetGameRules() => RazzRules.CreateGameRules();
    
    // Inherits all stud behavior, just uses different evaluator
}
```

### 3. No Additional Changes Required!

- Generic handlers automatically work via flow handler factory
- Hand evaluation uses RazzHandEvaluator via evaluator factory
- Endpoints are already registered via assembly scanning
- UI adapts via GameRules metadata

**Total new files:** 4
**Files modified:** 0
**Time to implement:** < 4 hours

---

## Appendix C: Example - Adding a Non-Poker Game (Screw Your Neighbor)

> **Contrast:** Unlike poker variants (Appendix B), non-poker games require significant additional infrastructure.

### Required New Infrastructure

Before implementing "Screw Your Neighbor", these foundational components are needed:

#### 1. Core Abstractions (One-Time Setup)

```csharp
// CardGames.Core/Games/ICardGame.cs - NEW
public interface ICardGame
{
    string Code { get; }
    string Name { get; }
    int MinPlayers { get; }
    int MaxPlayers { get; }
}

// CardGames.Core/Games/IEliminationGame.cs - NEW
public interface IEliminationGame : ICardGame
{
    int StartingLives { get; }
    string LossCondition { get; }  // "LowestCard", "HighestCard", etc.
}
```

#### 2. Domain Layer (`CardGames.Elimination` - NEW project)

**File:** `CardGames.Elimination/Games/ScrewYourNeighbor/ScrewYourNeighborGame.cs`

```csharp
public class ScrewYourNeighborGame : IEliminationGame
{
    public string Code => "SCREWYOURNEIGHBOR";
    public string Name => "Screw Your Neighbor";
    public int MinPlayers => 3;
    public int MaxPlayers => 10;
    public int StartingLives => 3;
    public string LossCondition => "LowestCard";
    
    public ScrewYourNeighborRules GetRules() => new()
    {
        CardsPerPlayer = 1,
        PassDirection = PassDirection.Left,
        ImmunityCard = Symbol.King,
        DealerCanSwapWithDeck = true
    };
}
```

**File:** `CardGames.Elimination/Games/ScrewYourNeighbor/ScrewYourNeighborRules.cs`

```csharp
public class ScrewYourNeighborRules
{
    public int CardsPerPlayer { get; init; }
    public PassDirection PassDirection { get; init; }
    public Symbol ImmunityCard { get; init; }
    public bool DealerCanSwapWithDeck { get; init; }
}

public enum PassDirection { Left, Right }
```

**File:** `CardGames.Elimination/Scoring/LowestCardLosesDeterminer.cs`

```csharp
public class LowestCardLosesDeterminer : IRoundOutcomeDeterminer
{
    public RoundOutcome DetermineOutcome(IEnumerable<PlayerCard> playerCards)
    {
        var loser = playerCards
            .OrderBy(pc => pc.Card.Rank)
            .First();
            
        return new RoundOutcome
        {
            LosingPlayerId = loser.PlayerId,
            LosingCard = loser.Card,
            LivesLost = 1
        };
    }
}
```

#### 3. API Layer (`CardGames.Elimination.Api` - NEW project)

**File:** `CardGames.Elimination.Api/GameFlow/ScrewYourNeighborFlowHandler.cs`

```csharp
public sealed class ScrewYourNeighborFlowHandler : IEliminationFlowHandler
{
    public string GameTypeCode => "SCREWYOURNEIGHBOR";
    
    public string GetInitialPhase(IGameState game) => "Dealing";
    
    public string? GetNextPhase(IGameState game, string currentPhase)
    {
        return currentPhase switch
        {
            "Dealing" => "PassKeepRound",
            "PassKeepRound" => AllPlayersActed(game) ? "SimultaneousReveal" : "PassKeepRound",
            "SimultaneousReveal" => "LoseLife",
            "LoseLife" => HasWinner(game) ? "GameComplete" : "Dealing",
            _ => null
        };
    }
    
    private bool HasWinner(IGameState game)
    {
        return game.Players.Count(p => p.Lives > 0) == 1;
    }
}
```

**File:** `CardGames.Elimination.Api/Features/ScrewYourNeighbor/v1/Commands/ProcessPassKeep/ProcessPassKeepCommandHandler.cs`

```csharp
public sealed class ProcessPassKeepCommandHandler
    : IRequestHandler<ProcessPassKeepCommand, OneOf<Success, Error>>
{
    public async Task<OneOf<Success, Error>> Handle(
        ProcessPassKeepCommand command,
        CancellationToken ct)
    {
        var game = await _context.EliminationGames
            .Include(g => g.Players)
            .FirstOrDefaultAsync(g => g.Id == command.GameId, ct);
            
        var player = game.Players.First(p => p.Id == command.PlayerId);
        
        if (command.Action == PassKeepAction.Keep)
        {
            player.Decision = PassKeepDecision.Kept;
        }
        else // SwapLeft
        {
            var neighbor = GetLeftNeighbor(game, player);
            
            if (neighbor.Card.Symbol == Symbol.King)
            {
                // King immunity - cannot swap
                player.Decision = PassKeepDecision.BlockedByKing;
            }
            else
            {
                // Perform swap
                (player.Card, neighbor.Card) = (neighbor.Card, player.Card);
                player.Decision = PassKeepDecision.Swapped;
            }
        }
        
        await _context.SaveChangesAsync(ct);
        return new Success();
    }
}
```

#### 4. Database Changes (NEW tables)

```csharp
// New entity - doesn't extend GamePlayer
public class EliminationGamePlayer
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public Guid PlayerId { get; set; }
    public int SeatPosition { get; set; }
    public int Lives { get; set; }
    public bool IsEliminated { get; set; }
    public Guid? CurrentCardId { get; set; }
    public PassKeepDecision? Decision { get; set; }
}

public enum PassKeepDecision
{
    Undecided,
    Kept,
    Swapped,
    BlockedByKing
}
```

#### 5. UI Components (NEW Blazor components)

```razor
@* ScrewYourNeighborTable.razor *@
<div class="syn-table">
    @foreach (var player in Players)
    {
        <div class="player-seat @(player.IsEliminated ? "eliminated" : "")">
            <LivesIndicator Lives="@player.Lives" />
            <SingleCardDisplay 
                Card="@player.Card" 
                FaceDown="@(!IsRevealPhase)" />
            @if (IsMyTurn && player.Id == CurrentPlayerId)
            {
                <PassKeepButtons 
                    OnKeep="HandleKeep"
                    OnSwapLeft="HandleSwapLeft"
                    NeighborHasKing="@NeighborHasKing" />
            }
        </div>
    }
</div>
```

### Summary Comparison

| Aspect | Razz (Poker Variant) | Screw Your Neighbor | Acey-Deucey |
|--------|---------------------|---------------------|-------------|
| New projects | 0 | 2 (`Elimination`, `Elimination.Api`) | 2 (`BettingGames`, `BettingGames.Api`) |
| New interface layers | 0 | 3 (`ICardGame`, `IEliminationGame`, `IEliminationFlowHandler`) | 3 (`ICardGame`, `IBettingPotGame`, `IBettingGameFlowHandler`) |
| Domain files | 4 | 8-12 | 5-8 |
| API files | 1 (flow handler) | 10-15 (commands, handlers, flow) | 6-10 (fewer phases) |
| Database changes | 0 (uses existing) | New tables + migrations | New tables + migrations |
| UI components | 0 (reuses poker table) | 5-8 new components | 3-5 new components |
| **Total effort** | **< 4 hours** | **4-5 weeks** | **2-3 weeks** |

---

*Document Version: 1.0*
*Last Updated: 2025*
*Author: Development Team*
